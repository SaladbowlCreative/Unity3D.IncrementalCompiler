using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Flinq;
using IncrementalCompiler.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Mono.CompilerServices.SymbolWriter;
using NLog;


namespace IncrementalCompiler
{
    public sealed class Compiler : IDisposable
    {
        readonly Logger _logger = LogManager.GetLogger("Compiler");
        CSharpCompilation _compilation;
        CompileOptions _options;
        FileTimeList _referenceFileList;
        FileTimeList _sourceFileList;
        Dictionary<string, MetadataReference> _referenceMap;
        Dictionary<string, SyntaxTree> _sourceMap;
        MemoryStream _outputDllStream;
        MemoryStream _outputDebugSymbolStream;
        string assemblyNameNoExtension;
        CSharpParseOptions parseOptions;
        CodeGeneration.GeneratedFilesMapping _filesMapping;
        ImmutableArray<DiagnosticAnalyzer> analyzers;
        const string analyzersPath = "./Analyzers";
        CompileResult previousResult;

        /// <summary>
        /// analyzers can only use dependencies that are already in this project
        /// dependency versions must match those of project dependencies
        /// </summary>
        (ImmutableArray<DiagnosticAnalyzer>, List<Diagnostic>) Analyzers() {
            // if Location.None is used instead, unity doesnt print the error to console.
            var defaultPos = Location.Create(
                "/Analyzers", TextSpan.FromBounds(0, 0), new LinePositionSpan()
            );

            try {
                var loader = new AnalyzerAssemblyLoader();
                var diagnostic = new List<Diagnostic>();

                if (!Directory.Exists(analyzersPath)) {
                    Directory.CreateDirectory(analyzersPath);
                    File.WriteAllText(analyzersPath + "/readme.txt", "Add Roslyn Analyzers here");
                    return (ImmutableArray<DiagnosticAnalyzer>.Empty, diagnostic);
                }

                _logger.Info("Analyzers:");
                var analyzers = 
                    Directory
                    .GetFiles(analyzersPath)
                    .Where(x => x.EndsWith(".dll"))
                    .Select(dll => {
                        var _ref = new AnalyzerFileReference(dll, loader);
                        _ref.AnalyzerLoadFailed += (_, e) => {
                            _logger.Error("failed to load analyzer: " + e.TypeName + "; " + e.Message);
                            diagnostic.Add(Diagnostic.Create(new DiagnosticDescriptor(
                                "A01",
                                "Error",
                                "Compiler couldn't load provided code analyzer: " + e.TypeName +
                                ". Please fix or remove from /Analyzers directory. More info in compiler log.",
                                "Error",
                                DiagnosticSeverity.Error,
                                true
                            ), defaultPos));
                        };

                        return _ref.GetAnalyzers(LanguageNames.CSharp);;
                    })
                    .Aggregate(new List<DiagnosticAnalyzer>(), (list, a) => {
                        a.ForEach(_logger.Info);
                        list.AddRange(a);
                        return list;
                    })
                    .ToImmutableArray();

                return (analyzers, diagnostic);
            } catch (Exception e) {
                _logger.Error(e);
                return (
                    ImmutableArray<DiagnosticAnalyzer>.Empty,
                    new List<Diagnostic> {Diagnostic.Create(new DiagnosticDescriptor(
                        "A02",
                        "Warning",
                        "Exception was thrown when loading analyzers: " + e.Message,
                        "Warning",
                        DiagnosticSeverity.Warning,
                        true
                    ), defaultPos)}
                );
            }
        }

        public CompileResult Build(CompileOptions options)
        {
            parseOptions = new CSharpParseOptions(LanguageVersion.CSharp7_2, DocumentationMode.Parse, SourceCodeKind.Regular, options.Defines)
                .WithFeatures(new []{new KeyValuePair<string, string>("IOperation", ""), });
            if (PlatformHelper.CurrentPlatform != Platform.Windows)
            {
                // OSX does not support pdb
                if (options.DebugSymbolFile == DebugSymbolFileType.Pdb ||
                    options.DebugSymbolFile == DebugSymbolFileType.PdbToMdb)
                {
                    options.DebugSymbolFile = DebugSymbolFileType.None;
                }
            }
            if (_compilation == null ||
                _options.WorkDirectory != options.WorkDirectory ||
                _options.AssemblyName != options.AssemblyName ||
                _options.Output != options.Output ||
                _options.NoWarnings.SequenceEqual(options.NoWarnings) == false ||
                _options.Defines.SequenceEqual(options.Defines) == false ||
                _options.DebugSymbolFile != options.DebugSymbolFile)
            {
                (previousResult, _compilation) = BuildFull(options);
                return previousResult;
            }
            else
            {
                return BuildIncremental(options);
            }
        }

        (CompileResult, CSharpCompilation) BuildFull(CompileOptions options)
        {
            var result = new CompileResult();

            var totalSW = Stopwatch.StartNew();
            var sw = Stopwatch.StartNew();

            _filesMapping = new CodeGeneration.GeneratedFilesMapping();

            void logTime(string label) {
                var elapsed = sw.Elapsed;
                _logger.Info($"Time elapsed {elapsed} on {label}");
                sw.Restart();
            }

            assemblyNameNoExtension = Path.GetFileNameWithoutExtension(options.AssemblyName);

            _logger.Info("BuildFull");
            _options = options;

            _referenceFileList = new FileTimeList();
            _referenceFileList.Update(options.References);

            _sourceFileList = new FileTimeList();
            _sourceFileList.Update(options.Files);

            _referenceMap = options.References.ToDictionary(
                keySelector: file => file,
                elementSelector: CreateReference
            );
            logTime("Loaded references");

            _sourceMap = options.Files.AsParallel().Select(
                fileName => (fileName, tree: ParseSource(fileName, parseOptions))).ToDictionary(t => t.fileName, t => t.tree);
            logTime("Loaded sources");


            var specificDiagnosticOptions = options.NoWarnings.ToDictionary(x => x, _ => ReportDiagnostic.Suppress);
            var compilation = CSharpCompilation.Create(
                options.AssemblyName,
                _sourceMap.Values,
                _referenceMap.Values,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithSpecificDiagnosticOptions(specificDiagnosticOptions)
                    .WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default)
                    .WithAllowUnsafe(options.Options.Contains("-unsafe"))

            );
            logTime("Compilation created");

            List<Diagnostic> diagnostic;

            (analyzers, diagnostic) = Analyzers();

            compilation = CodeGeneration.Run(
                false,
                compilation,
                compilation.SyntaxTrees,
                parseOptions,
                assemblyNameNoExtension,
                ref _filesMapping, _sourceMap
            ).tap((compAndDiag) => {
                diagnostic.AddRange(compAndDiag.Item2);
                return compAndDiag.Item1;
            });

            logTime("Code generated");

            compilation = MacroProcessor.Run(
                compilation,
                compilation.SyntaxTrees,
                _sourceMap
            );
            logTime("Macros completed");

            AnalyzeAndEmit(result, diagnostic, compilation, analyzers);
            logTime("Emitted dll");

            _logger.Info($"Total elapsed {totalSW.Elapsed}");

            previousResult = result;
            return (result, compilation);
        }

        void AnalyzeAndEmit(
            CompileResult result,
            ICollection<Diagnostic> diagnostic,
            CSharpCompilation compilation,
            ImmutableArray<DiagnosticAnalyzer> analyzers
        ) {
            diagnostic = diagnostic.Concat(AnalyzersDiagnostics(compilation, analyzers)).ToList();
            Emit(result, diagnostic, compilation);
        }

        static ImmutableArray<Diagnostic> AnalyzersDiagnostics(
            CSharpCompilation comp, ImmutableArray<DiagnosticAnalyzer> analyzers
        ) =>
            analyzers.Any()
            ? comp
                .WithAnalyzers(analyzers)
                .GetAnalysisResultAsync(new CancellationToken())
                .Result
                .GetAllDiagnostics()
            : ImmutableArray<Diagnostic>.Empty;

        CompileResult BuildIncremental(CompileOptions options)
        {
            _logger.Info("BuildIncremental");
            _options = options;

            // update reference files

            var referenceChanges = _referenceFileList.Update(options.References);
            foreach (var file in referenceChanges.Added)
            {
                _logger.Info("+ {0}", file);
                var reference = CreateReference(file);
                _compilation = _compilation.AddReferences(reference);
                _referenceMap.Add(file, reference);
            }
            foreach (var file in referenceChanges.Changed)
            {
                _logger.Info("* {0}", file);
                var reference = CreateReference(file);
                _compilation =_compilation.ReplaceReference(_referenceMap[file], reference);
                _referenceMap[file] = reference;
            }
            foreach (var file in referenceChanges.Removed)
            {
                _logger.Info("- {0}", file);
                _compilation = _compilation.RemoveReferences(_referenceMap[file]);
                _referenceMap.Remove(file);
            }

            // update source files

            var sourceChanges = _sourceFileList.Update(options.Files);

            var allTrees = _compilation.SyntaxTrees;

            var newTrees = sourceChanges.Added.AsParallel().Select(file => {
                var tree = ParseSource(file, parseOptions);
                return (file, tree);
            }).ToArray();

            foreach (var (file, tree) in newTrees) {
                _logger.Info("+ {0}", file);
                _sourceMap.Add(file, tree);
            }

            _compilation = _compilation.AddSyntaxTrees(newTrees.Select(t => t.tree));

            var changes = sourceChanges.Changed.AsParallel().Select(file => {
                var tree = ParseSource(file, parseOptions);
                return (file, tree);
            }).ToArray();

            foreach (var (file, tree) in changes) {
                _logger.Info("* {0}", file);
                _compilation = _compilation.ReplaceSyntaxTree(_sourceMap[file], tree);
                _sourceMap[file] = tree;
            }

            var removedTrees = sourceChanges.Removed.Select(file =>
            {
                _logger.Info("- {0}", file);
                var tree = _sourceMap[file];
                _sourceMap.Remove(file);
                return tree;
            }).ToArray();

            var generatedRemove = sourceChanges.Removed.Concat(sourceChanges.Changed).ToArray();
            var generatedFilesRemove = generatedRemove
                .Where(_filesMapping.filesDict.ContainsKey)
                .SelectMany(path => _filesMapping.filesDict[path])
                .Where(_sourceMap.ContainsKey)
                .Select(path => _sourceMap[path]);

            _compilation = _compilation.RemoveSyntaxTrees(removedTrees.Concat(generatedFilesRemove));

            _filesMapping.removeFiles(generatedRemove);

            var allAddedTrees = newTrees.Concat(changes).Select(t => t.tree).ToImmutableArray();

            List<Diagnostic> diagnostic;
            (_compilation, diagnostic) = CodeGeneration.Run(
                true, _compilation, allAddedTrees, parseOptions, assemblyNameNoExtension, ref _filesMapping, _sourceMap
            );

            //TODO: macros on new generated files

            var treeSet = allAddedTrees.Select(t => t.FilePath).ToImmutableHashSet();
            var treesForMacroProcessor =
                _compilation
                .SyntaxTrees
                .Where(t => treeSet.Contains(t.FilePath))
                .ToImmutableArray();

            _compilation = MacroProcessor.Run(
                _compilation,
                treesForMacroProcessor,
                _sourceMap
            );
            // emit or reuse prebuilt output

            diagnostic.AddRange(AnalyzersDiagnostics(_compilation, analyzers));

            var reusePrebuilt = previousResult.Succeeded && _outputDllStream != null && (
                (_options.PrebuiltOutputReuse == PrebuiltOutputReuseType.WhenNoChange &&
                 sourceChanges.Empty && referenceChanges.Empty) ||
                (_options.PrebuiltOutputReuse == PrebuiltOutputReuseType.WhenNoSourceChange &&
                 sourceChanges.Empty && referenceChanges.Added.Count == 0 && referenceChanges.Removed.Count == 0));

            if (reusePrebuilt)
            {
                _logger.Info("Reuse prebuilt output");

                // write dll

                var dllFile = Path.Combine(_options.WorkDirectory, _options.Output);
                WriteToFile(_outputDllStream, dllFile);

                // write pdb or mdb

                switch (_options.DebugSymbolFile)
                {
                    case DebugSymbolFileType.Pdb:
                        var pdbFile = Path.Combine(_options.WorkDirectory, Path.ChangeExtension(_options.Output, ".pdb"));
                        WriteToFile(_outputDebugSymbolStream, pdbFile);
                        break;

                    case DebugSymbolFileType.PdbToMdb:
                    case DebugSymbolFileType.Mdb:
                        var mdbFile = Path.Combine(_options.WorkDirectory, _options.Output + ".mdb");
                        WriteToFile(_outputDebugSymbolStream, mdbFile);
                        break;
                }

                return previousResult;
            }
            else
            {
                _logger.Info("Emit");

                var result = previousResult;
                result.Clear();
                AnalyzeAndEmit(result, diagnostic, _compilation, analyzers);
                return result;
            }
        }

        private MetadataReference CreateReference(string file)
        {
            return MetadataReference.CreateFromFile(Path.Combine(_options.WorkDirectory, file));
        }

        private SyntaxTree ParseSource(string file, CSharpParseOptions parseOption)
        {
            var fileFullPath = Path.Combine(_options.WorkDirectory, file);
            var text = File.ReadAllText(fileFullPath);
            return CSharpSyntaxTree.ParseText(text, parseOption, file, Encoding.UTF8);
        }

        private void Emit(CompileResult result, ICollection<Diagnostic> diagnostic, CSharpCompilation compilation)
        {
            _outputDllStream?.Dispose();
            _outputDllStream = new MemoryStream();
            _outputDebugSymbolStream?.Dispose();
            _outputDebugSymbolStream = _options.DebugSymbolFile != DebugSymbolFileType.None ? new MemoryStream() : null;

            // emit to memory

            var r = _options.DebugSymbolFile == DebugSymbolFileType.Mdb
                ? compilation.EmitWithMdb(_outputDllStream, _outputDebugSymbolStream)
                : compilation.Emit(_outputDllStream, _outputDebugSymbolStream);

            // memory to file

            var dllFile = Path.Combine(_options.WorkDirectory, _options.Output);
            var mdbFile = Path.Combine(_options.WorkDirectory, _options.Output + ".mdb");
            var pdbFile = Path.Combine(_options.WorkDirectory, Path.ChangeExtension(_options.Output, ".pdb"));


            // gather result

            var formatter = new DiagnosticFormatter();
            foreach (var d in diagnostic.Concat(r.Diagnostics)) {
                if (d.Severity == DiagnosticSeverity.Warning && !d.IsWarningAsError)
                    result.Warnings.Add(formatter.Format(d, CultureInfo.InvariantCulture));
                else if (d.Severity == DiagnosticSeverity.Error || d.IsWarningAsError)
                    result.Errors.Add(formatter.Format(d, CultureInfo.InvariantCulture));
            }

            result.Succeeded = r.Success;

            if (r.Success)
            {
                WriteToFile(_outputDllStream, dllFile);

                if (_outputDebugSymbolStream != null)
                {
                    var emitDebugSymbolFile = _options.DebugSymbolFile == DebugSymbolFileType.Mdb ? mdbFile : pdbFile;
                    WriteToFile(_outputDebugSymbolStream, emitDebugSymbolFile);
                }

                // pdb to mdb when required
                if (_options.DebugSymbolFile == DebugSymbolFileType.PdbToMdb)
                {
                    try {
                        var code = ConvertPdb2Mdb(dllFile, LogManager.GetLogger("Pdb2Mdb"), result.Errors);
                        _logger.Info("pdb2mdb exited with {0}", code);
                        File.Delete(pdbFile);

                        // read converted mdb file to cache contents
                        _outputDebugSymbolStream?.Dispose();
                        _outputDebugSymbolStream = new MemoryStream(File.ReadAllBytes(mdbFile));
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e);
                        result.Errors.Add($"Error while running pdb2mdb: {e}");
                    }

                }
            }
        }

        void WriteToFile(MemoryStream stream, string file) {
            _logger.Info($"Writing data to file {file}, size: {stream.Length:N0}");
            using (var dllStream = new FileStream(file, FileMode.Create))
            {
                stream.Seek(0L, SeekOrigin.Begin);
                stream.CopyTo(dllStream);
            }
        }

        private string GetDiagnosticString(Diagnostic diagnostic, string type)
        {
            var line = diagnostic.Location.GetLineSpan();

            // Path could be null
            if (string.IsNullOrEmpty(line.Path))
                return $"None: " + $"{type} {diagnostic.Id}: {diagnostic.GetMessage()}";

            // Unity3d must have a relative path starting with "Assets/".
            var path = (line.Path.StartsWith(_options.WorkDirectory + "/") || line.Path.StartsWith(_options.WorkDirectory + "\\"))
                ? line.Path.Substring(_options.WorkDirectory.Length + 1)
                : line.Path;

            var msg = diagnostic.GetMessage();
            return $"{path}({line.StartLinePosition.Line + 1},{line.StartLinePosition.Character + 1}): "
                + $"{type} {diagnostic.Id}: {msg}";
        }

        public static int ConvertPdb2Mdb(string dllFile, Logger logger, List<string> resultErrors)
        {
            var toolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "pdb2mdb.exe");
            if (!File.Exists(toolPath))
            {
                resultErrors.Add($"Could not find pdb2mdb tool at '{toolPath}'");
                return 666;
            }
            using (var process = new Process())
            {
                var startInfo = new ProcessStartInfo(toolPath, '"' + dllFile + '"') {
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                process.StartInfo = startInfo;

                process.OutputDataReceived += (sender, e) => logger.Info("Output :" + e.Data);

                logger.Info($"Process: {process.StartInfo.FileName}");
                logger.Info($"Arguments: {process.StartInfo.Arguments}");

                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();

                logger.Info($"Exit code: {process.ExitCode}");

                return process.ExitCode;
            }
        }

        #region IDisposable

        public void Dispose() {
            _outputDllStream?.Dispose();
            _outputDebugSymbolStream?.Dispose();
        }

        #endregion
    }
}
