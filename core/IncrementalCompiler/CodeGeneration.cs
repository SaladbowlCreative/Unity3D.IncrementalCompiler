using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using GenerationAttributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MonadLib;
using static System.String;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace IncrementalCompiler
{
    public static class CodeGeneration
    {
        public const string GENERATED_FOLDER = "Generated";
        static readonly Type caseType = typeof(RecordAttribute);
        static readonly HashSet<SyntaxKind> kindsForExtensionClass = new HashSet<SyntaxKind>(new[] {
            SyntaxKind.PublicKeyword, SyntaxKind.InternalKeyword, SyntaxKind.PrivateKeyword
        });

        public class GeneratedFilesMapping
        {
            public Dictionary<string, List<string>> filesDict = new Dictionary<string, List<string>>();
            public Dictionary<string, List<JavaFile>> javaFilesDict = new Dictionary<string, List<JavaFile>>();
            int javaVersion = 1, lastUsedJavaVersion;
            SyntaxTree prevousTree;

            static void addValue<A>(Dictionary<string, List<A>> dict, string key, A value) {
                if (!dict.ContainsKey(key)) dict[key] = new List<A>();
                dict[key].Add(value);
            }

            static IEnumerable<A> enumerate<A>(Dictionary<string, List<A>> dict) =>
                dict.Values.SelectMany(_ => _);

            public void add(string key, string value) => addValue(filesDict, key, value);

            public bool tryAddJavaFile(string key, JavaFile value)
            {
                if (enumerate(javaFilesDict).Any(jf => jf.Module == value.Module && jf.Path == value.Path))
                {
                    return false;
                }
                else
                {
                    javaVersion++;
                    addValue(javaFilesDict, key, value);
                    return true;
                }
            }

            public CSharpCompilation updateCompilation(CSharpCompilation compilation, CSharpParseOptions options, string assemblyName, string generatedFilesDir) {
                if (lastUsedJavaVersion != javaVersion)
                {
                    lastUsedJavaVersion = javaVersion;
                    var newTree = generateTree(options, assemblyName, generatedFilesDir);
                    var path = newTree.FilePath;
                    {
                        if (File.Exists(path)) File.Delete(path);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.WriteAllText(path, newTree.GetText().ToString());
                    }
                    // this code smells a little
                    filesDict["GENERATED_JAVA"] = new List<string>(new []{ path });
                    var result =
                        prevousTree == null
                        ? compilation.AddSyntaxTrees(newTree)
                        : compilation.ReplaceSyntaxTree(prevousTree, newTree);
                    prevousTree = newTree;
                    return result;
                }
                return compilation;
            }

            static string asVerbatimString(string str) => $"@\"{str.Replace("\"", "\"\"")}\"";

            SyntaxTree generateTree(CSharpParseOptions options, string assemblyName, string generatedFilesDir) {
                var className = assemblyName.Replace("-", "");
                var data = enumerate(javaFilesDict).Select(jf =>
                    $"new JavaFile(module: \"{jf.Module}\", path: {asVerbatimString(jf.Path)}, contents: {asVerbatimString(jf.Contents)})"
                );
                var ns = "com.tinylabproductions.generated.java";
                var tree = CSharpSyntaxTree.ParseText(
                    "#if UNITY_EDITOR\n" +
                    "using GenerationAttributes;\n" +
                    $"namespace {ns} {{\n" +
                    $"public static class {className} {{\n" +
                    "public static readonly JavaFile[] javaFiles = {" +
                    Join(", ", data) +
                    "}; }}\n" +
                    "#endif",
                    options,
                    path: Path.Combine(generatedFilesDir, ns.Replace('.', Path.DirectorySeparatorChar), className + ".cs")
                );
                return CSharpSyntaxTree.Create(
                    tree.GetCompilationUnitRoot().NormalizeWhitespace(),
                    options,
                    path: tree.FilePath,
                    encoding: Encoding.UTF8
                );
            }

            public void removeFiles(IEnumerable<string> filesToRemove) {
                foreach (var filePath in filesToRemove) {
                    if (filesDict.TryGetValue(filePath, out var generatedFiles)) {
                        foreach (var generatedFile in generatedFiles) {
                            if (File.Exists(generatedFile)) File.Delete(generatedFile);
                        }
                        filesDict.Remove(filePath);
                    }
                    if (javaFilesDict.ContainsKey(filePath)) {
                        javaFilesDict.Remove(filePath);
                        javaVersion++;
                    }
                }
            }
        }

        interface IGenerationResult {}

        class ModifiedFile : IGenerationResult
        {
            public readonly SyntaxTree From;
            public readonly CompilationUnitSyntax To;

            public ModifiedFile(SyntaxTree from, CompilationUnitSyntax to) {
                From = from;
                To = to;
            }
        }

        abstract class GeneratedFile : IGenerationResult
        {
            public readonly string SourcePath;
            public readonly Location Location;

            protected GeneratedFile(string sourcePath, Location location) {
                SourcePath = sourcePath;
                Location = location;
            }
        }

        class GeneratedJavaFile : GeneratedFile
        {
            public readonly JavaFile JavaFile;

            public GeneratedJavaFile(string sourcePath, Location location, JavaFile javaFile) : base(sourcePath, location) {
                JavaFile = javaFile;
            }
        }

        class GeneratedCsFile : GeneratedFile
        {
            public readonly SyntaxTree Tree;
            public readonly string FilePath, Contents;

            public GeneratedCsFile(string sourcePath, Location location, SyntaxTree tree) : base(sourcePath, location) {
                Tree = tree;
                FilePath = tree.FilePath;
                Contents = tree.GetText().ToString();
            }
        }

        // TODO: clean this class
        // refactor parts to tlplib
        class JavaClassFile
        {
            public readonly Location Location;
            public readonly string Module, Imports, ClassBody;
            readonly INamedTypeSymbol Symbol;
            readonly List<string> Methods = new List<string>();
            string Package => "com.generated." + Module;
            public string PackageWithClass => Package + "." + Symbol.Name;
            readonly IMethodSymbol[] allMethods;

            public JavaClassFile(INamedTypeSymbol symbol, string module, string imports, string classBody, Location location) {
                Symbol = symbol;
                Module = module;
                Imports = imports;
                ClassBody = classBody;
                allMethods = AllInterfaceMembers(symbol).OfType<IMethodSymbol>().ToArray();
                Location = location;
            }

            static ImmutableArray<ISymbol> AllInterfaceMembers(INamedTypeSymbol symbol) =>
                symbol.GetMembers().AddRange(symbol.AllInterfaces.SelectMany(i => i.GetMembers()));

            public void AddMethod(string methodBody, IMethodSymbol methodSymbol) {
                var isConstructor = methodSymbol.MethodKind == MethodKind.Constructor;
                var modifier = methodSymbol.IsStatic ? "static " : "";
                var parameters = Join(", ",
                    methodSymbol.Parameters.Select(p => $"final {ToJavaType(p.Type)} {p.Name}").ToArray());
                var typeAndName = isConstructor
                    ? Symbol.Name
                    : $"{ToJavaType(methodSymbol.ReturnType)} {methodSymbol.Name}";
                Methods.Add(
                    $"public {modifier}{typeAndName}({parameters}) {{\n" +
                    $"{methodBody}\n" +
                    "}\n"
                );
            }

            public string GenerateJava() {
                return $"package {Package};\n\n" +
                       Imports +
                       $"public class {Symbol.Name} {{\n" +
                       $"{ClassBody}\n" +
                       Join("\n", Methods) +
                       "}";
            }

            public IEnumerable<MemberDeclarationSyntax> GenerateMembers() {
                var line = $"static UnityEngine.AndroidJavaClass jc = UnityEngine.Application.isEditor ? null : " +
                           $"new UnityEngine.AndroidJavaClass(\"{PackageWithClass}\");";
                var secondLine = Symbol.IsStatic ? "" : "readonly UnityEngine.AndroidJavaObject jo;";
                return ParseClassMembers(line + "\n" + secondLine);
            }

            static bool isSubType(ITypeSymbol type, string baseType) {
                if (type == null) return false;
                if (type.ToDisplayString() == baseType)
                    return true;
                return isSubType(type.BaseType, baseType);
            }

            public BaseMethodDeclarationSyntax GenerateMethod(IMethodSymbol symbol, BaseMethodDeclarationSyntax syntax) {
                var isConstructor = symbol.MethodKind == MethodKind.Constructor;
                var isVoid = symbol.ReturnType.SpecialType == SpecialType.System_Void;
                var returnStatement = isVoid ? "" : "return ";
                var callStetement = isConstructor ? "jo = new UnityEngine.AndroidJavaObject" : (symbol.IsStatic ? "jc.CallStatic" : "jo.Call");
                var firstParam = $"\"{(isConstructor ? PackageWithClass : symbol.Name)}\"";

                string parameterName(IParameterSymbol ps) {
                    var type = ps.Type;
                    var name = ps.Name;
                    if (isSubType(type, "com.tinylabproductions.TLPLib.Android.Bindings.Binding"))
                        return name + ".java";
                    return name;
                }

                var remainingParams = symbol.Parameters.Select(parameterName);
                var arguments = Join(", ", new []{firstParam}.Concat(remainingParams));

                switch (syntax)
                {
                    case MethodDeclarationSyntax mds:
                        var genericReturn = isVoid ? "" : "<" + mds.ReturnType + ">";
                        return mds
                            .WithBody(ParseBlock($"{returnStatement}{callStetement}{genericReturn}({arguments});"))
                            .WithExpressionBody(null)
                            .WithSemicolonToken(SF.Token(SyntaxKind.None));
                    case ConstructorDeclarationSyntax cds:
                        return cds
                            .WithBody(ParseBlock($"{returnStatement}{callStetement}({arguments});"))
                            .WithExpressionBody(null)
                            .WithSemicolonToken(SF.Token(SyntaxKind.None));
                }
                throw new Exception("Wrong syntax type: " + syntax.GetType());
            }

            static string ToJavaType(ITypeSymbol type) {
                if (!type.IsDefinition) return ToJavaType(type.OriginalDefinition);
                switch (type.SpecialType) {
                    case SpecialType.System_String: return "String";
                    case SpecialType.System_Boolean: return "boolean";
                    case SpecialType.System_Byte: return "byte";
                    case SpecialType.System_Char: return "char";
                    case SpecialType.System_Int16: return "short";
                    case SpecialType.System_Int32: return "int";
                    case SpecialType.System_Int64: return "long";
                    case SpecialType.System_Single: return "float";
                    case SpecialType.System_Double: return "double";
                    case SpecialType.System_Void: return "void";
                    case SpecialType.System_Nullable_T:
                        var arguments = type.BaseType.TypeArguments;
                        if (arguments.Length == 0) break;
                        switch (type.BaseType.TypeArguments[0].SpecialType) {
                            // this code is never reached.
                            // TODO: find a way to detect nullable types in C# (int?, bool?, ...)
                            case SpecialType.System_Boolean: return "Boolean";
                            case SpecialType.System_Byte:    return "Byte";
                            case SpecialType.System_Char:    return "Character";
                            case SpecialType.System_Int16:   return "Short";
                            case SpecialType.System_Int32:   return "Integer";
                            case SpecialType.System_Int64:   return "Long";
                            case SpecialType.System_Single:  return "Float";
                            case SpecialType.System_Double:  return "Double";
                        }
                        break;
                }


                foreach (var attrData in type.GetAttributes()) {
                    if (attrData.AttributeClass.ToDisplayString() == typeof(JavaBindingAttribute).FullName) {
                        var instance = CreateAttributeByReflection<JavaBindingAttribute>(attrData);
                        return instance.JavaClass;
                    }
                }

                if (isSubType(type, "UnityEngine.AndroidJavaProxy") || isSubType(type, "UnityEngine.AndroidJavaObject")) {
                    return "Object";
                }

                throw new Exception($"Unsupported type: {type.ToDisplayString()}");
            }

            IEnumerable<string> InterfaceMethods() {
                return allMethods.Select(m =>
                {
                    var parameters = m.Parameters.Select(p => $"final {ToJavaType(p.Type)} {p.Name}");
                    return $"void {m.Name}({Join(", ", parameters)});";
                });
            }

            public string GenerateJavaInterface() =>
                $"package {Package};\n\n" +
                $"public interface {Symbol.Name} " + Block(InterfaceMethods());

            public string JavaFilePath() =>
                PackageWithClass.Replace('.', Path.DirectorySeparatorChar) + ".java";

            public ClassDeclarationSyntax GetInterfaceClass() {
                return ParseClass(
                    // JavaBinding attribute does nothing here.
                    // Compiler does all code generation in one step,
                    // so we can't depend on generated classes when generating other code
                    // $"[GenerationAttributes.JavaBinding(\"{PackageWithClass}\")]\n" +
                    $"public class {Symbol.Name}Proxy : com.tinylabproductions.TLPLib.Android.JavaListenerProxy" +
                    Block(
                        Join("\n", allMethods.Select(m =>
                        {
                            if (m.ReturnType.SpecialType != SpecialType.System_Void) throw new Exception("Return type must be void");
                            var parameterTypes = m.Parameters.Select(p => p.Type.ToString()).ToArray();
                            var genericArgs = parameterTypes.Length == 0 ? "" : $"<{Join(", ", parameterTypes)}>";
                            return $"public event System.Action{genericArgs} {m.Name};";
                        })),
                        $"public {Symbol.Name}Proxy() : base(\"{PackageWithClass}\"){{}}" +
                            "protected override void invokeOnMain(string methodName, object[] args)" + Block(
                            "  switch(methodName)" + Block(
                                allMethods.Select(m => {
                                    var invokeParams = Join(", ", m.Parameters.Select((p, idx) => $"({p.Type.ToString()}) args[{idx}]"));
                                    return $"case \"{m.Name}\": {m.Name}?.Invoke({invokeParams}); return;";
                                })
                            ),
                            "base.invokeOnMain(methodName, args);"
                        ),
                        "public void registerLogger(string prefix, com.tinylabproductions.TLPLib.Logger.ILog log)" + Block(
                            allMethods.Select(m =>
                            {
                                var paramNames = m.Parameters.Select(p => p.Name).ToArray();
                                var paramsStr = paramNames.Length == 0 ? "\"\"" : Join(" + \", \" + ", paramNames.Select(p => $"{p}.ToString()"));
                                return $"{m.Name} += ({Join(", ", paramNames)}) => com.tinylabproductions.TLPLib.Logger.ILogExts.debug(log, prefix + \"{m.Name}(\" + {paramsStr} + \")\");";
                            })
                        )
                    )
                );
            }
        }

        static string Block(IEnumerable<string> contents) => "{" + Join("\n", contents) + "}";
        static string Block(params string[] contents) => Block((IEnumerable<string>)contents);

        // Directory.Delete(recursive: true) throws an exception in some cases
        static void DeleteFilesAndFoldersRecursively(string targetDir)
        {
            foreach (var file in Directory.GetFiles(targetDir))
                File.Delete(file);

            foreach (var subDir in Directory.GetDirectories(targetDir))
                DeleteFilesAndFoldersRecursively(subDir);

            try
            {
                Directory.Delete(targetDir);
            }
            catch (IOException)
            {
                // it fails one thime if Windows file explorer is opened in targetDir
                Directory.Delete(targetDir);
            }
        }

        public static (CSharpCompilation, ICollection<Diagnostic>) Run(
            bool incrementalRun,
            CSharpCompilation compilation,
            ImmutableArray<SyntaxTree> trees,
            CSharpParseOptions parseOptions,
            string assemblyName,
            ref GeneratedFilesMapping filesMapping,
            Dictionary<string, SyntaxTree> sourceMap
        ) {
            var generatedProjectFilesDirectory = Path.Combine(GENERATED_FOLDER, assemblyName);
            if (!incrementalRun && Directory.Exists(generatedProjectFilesDirectory))
            {
                DeleteFilesAndFoldersRecursively(generatedProjectFilesDirectory);
            }
            Directory.CreateDirectory(generatedProjectFilesDirectory);
            var oldCompilation = compilation;
            var diagnostic = new List<Diagnostic>();

            void tryAttribute<A>(AttributeData attr, Action<A> a) where A : Attribute {
                try {
                    var instance = CreateAttributeByReflection<A>(attr);
                    a(instance);
                }
                catch (Exception e) {
                    diagnostic.Add(Diagnostic.Create(new DiagnosticDescriptor(
                        "ER0001", "Error", $"Compiler error for attribute {typeof(A).Name}: {e.Message}({e.Source}) at {e.StackTrace}", "Error", DiagnosticSeverity.Error, true
                    ), attrLocation(attr)));
                }
            }

            var results = trees.AsParallel().SelectMany(originalTree =>
            {
                var tree = originalTree;

                var model = oldCompilation.GetSemanticModel(tree);
                var root = tree.GetCompilationUnitRoot();
                var newMembers = ImmutableList<MemberDeclarationSyntax>.Empty;
                var typesInFile = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToImmutableArray();
                var result = ImmutableList<IGenerationResult>.Empty;

                var treeEdited = false;
                var editsList = new List<(SyntaxNode, SyntaxNode)>();
                void replaceSyntax(SyntaxNode oldNode, SyntaxNode newNode) {
                    treeEdited = true;
                    editsList.Add((oldNode, newNode));
                }

                foreach (var tds in typesInFile)
                {
                    bool treeContains(SyntaxReference syntaxRef) =>
                        tree == syntaxRef.SyntaxTree && tds.Span.Contains(syntaxRef.Span);

                    var symbol = model.GetDeclaredSymbol(tds);
                    JavaClassFile javaClassFile = null;
                    foreach (var attr in symbol.GetAttributes())
                    {
                        if (!treeContains(attr.ApplicationSyntaxReference)) continue;
                        var attrClassName = attr.AttributeClass.ToDisplayString();
                        if (attrClassName == caseType.FullName)
                        {
                            tryAttribute<RecordAttribute>(attr, instance => {
                                newMembers = newMembers.AddRange(
                                    GenerateCaseClass(instance, model, tds)
                                    .Select(generatedClass =>
                                        AddAncestors(tds, generatedClass, onlyNamespace: false)
                                    )
                                );
                            });
                        }
                        if (attrClassName == typeof(MatcherAttribute).FullName)
                        {
                            tryAttribute<MatcherAttribute>(attr, _ => {
                                newMembers = newMembers.Add(
                                    AddAncestors(tds, GenerateMatcher(model, tds, typesInFile), onlyNamespace: true)
                                );
                            });
                        }
                        if (attrClassName == typeof(JavaClassAttribute).FullName)
                        {
                            tryAttribute<JavaClassAttribute>(attr, instance =>
                            {
                                javaClassFile = new JavaClassFile(symbol, module: instance.Module, imports: instance.Imports, classBody: instance.ClassBody, attrLocation(attr));
                                newMembers = newMembers.Add(AddAncestors(
                                    tds,
                                    CreatePartial(tds, javaClassFile.GenerateMembers(), Extensions.EmptyBaseList),
                                    onlyNamespace: false
                                 ));
                            });
                        }
                        if (attrClassName == typeof(JavaListenerInterfaceAttribute).FullName)
                        {
                            tryAttribute<JavaListenerInterfaceAttribute>(attr, instance =>
                            {
                                var javaInterface = new JavaClassFile(symbol, module: instance.Module, imports: "", classBody: "", attrLocation(attr));
                                result = result.Add(new GeneratedJavaFile(
                                    sourcePath: tree.FilePath,
                                    location: attrLocation(attr),
                                    javaFile: new JavaFile(
                                        module: javaInterface.Module,
                                        path: javaInterface.JavaFilePath(),
                                        contents: javaInterface.GenerateJavaInterface()
                                    )
                                ));
                                newMembers = newMembers.Add(AddAncestors(tds, javaInterface.GetInterfaceClass(), onlyNamespace: false));
                            });
                        }
                    }

                    var newClassMembers = ImmutableArray<string>.Empty;
                    foreach (var member in symbol.GetMembers())
                    {
                        switch (member)
                        {
                            case IFieldSymbol fieldSymbol:
                                foreach (var attr in fieldSymbol.GetAttributes())
                                {
                                    if (!treeContains(attr.ApplicationSyntaxReference)) continue;
                                    var attrClassName = attr.AttributeClass.ToDisplayString();
                                    if (attrClassName == typeof(PublicAccessor).FullName)
                                    {
                                        tryAttribute<PublicAccessor>(attr, _ =>
                                        {
                                            newClassMembers = newClassMembers.Add(GenerateAccessor(fieldSymbol));
                                        });
                                    }
                                    // TODO: generic way to add new attributes
                                    if (attrClassName == typeof(ThreadStaticAttribute).FullName)
                                    {
                                        tryAttribute<ThreadStaticAttribute>(attr, _ => throw new Exception($"Can't use {nameof(ThreadStaticAttribute)} in Unity"));
                                    }
                                }
                                break;
                            case IMethodSymbol methodSymbol:
                                foreach (var attr in methodSymbol.GetAttributes())
                                {
                                    if (!treeContains(attr.ApplicationSyntaxReference)) continue;
                                    var attrClassName = attr.AttributeClass.ToDisplayString();
                                    if (attrClassName == typeof(JavaMethodAttribute).FullName)
                                    {
                                        tryAttribute<JavaMethodAttribute>(attr, instance =>
                                        {
                                            if (javaClassFile == null) throw new Exception(
                                                $"must be used together with {nameof(JavaClassAttribute)}"
                                            );
                                            javaClassFile.AddMethod(instance.MethodBody, methodSymbol);
                                            var syntaxes = methodSymbol.DeclaringSyntaxReferences;
                                            if (syntaxes.Length != 1) throw new Exception($"code must be in one place");
                                            var syntax = (BaseMethodDeclarationSyntax) syntaxes[0].GetSyntax();
                                            var replacedSyntax = javaClassFile.GenerateMethod(methodSymbol, syntax);
                                            replaceSyntax(syntax, replacedSyntax);
                                        });
                                    }
                                }
                                break;
                        }
                    }

                    if (newClassMembers.Length > 0)
                    {
                        newMembers = newMembers.Add(AddAncestors(
                            tds,
                            CreatePartial(tds, ParseClassMembers(Join("\n", newClassMembers)), Extensions.EmptyBaseList),
                            onlyNamespace: false
                        ));
                    }

                    if (javaClassFile != null)
                    {
                        result = result.Add(new GeneratedJavaFile(
                            sourcePath: tree.FilePath,
                            location: javaClassFile.Location,
                            javaFile: new JavaFile(
                                module: javaClassFile.Module,
                                path: javaClassFile.JavaFilePath(),
                                contents: javaClassFile.GenerateJava()
                            )
                        ));
                    }
                }
                if (treeEdited)
                {
                    var newRoot = root.ReplaceNodes(
                        editsList.Select(t => t.Item1),
                        (toReplace, _) => editsList.First(t => t.Item1 == toReplace).Item2
                    );
                    result = result.Add(new ModifiedFile(tree, newRoot));
                }
                if (newMembers.Any())
                {
                    var nt = CSharpSyntaxTree.Create(
                        SF.CompilationUnit()
                            .WithUsings(cleanUsings(root.Usings))
                            .WithMembers(SF.List(newMembers))
                            .NormalizeWhitespace(),
                        path: Path.Combine(generatedProjectFilesDirectory, tree.FilePath),
                        options: parseOptions,
                        encoding: Encoding.UTF8);
                    result = result.Add(new GeneratedCsFile(sourcePath: tree.FilePath, tree: nt, location: root.GetLocation()));
                }
                return result.ToArray();
            }).ToArray();
            var csFiles = results.OfType<GeneratedCsFile>().ToArray();
            compilation = compilation.AddSyntaxTrees(csFiles.Select(_ => _.Tree));
            foreach (var file in csFiles)
            {
                sourceMap[file.FilePath] = file.Tree;
                var generatedPath = file.FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(generatedPath));
                if (File.Exists(generatedPath))
                {
                    diagnostic.Add(Diagnostic.Create(new DiagnosticDescriptor(
                        "ER0002", "Error", $"Could not generate file '{generatedPath}'. File already exists.", "Error", DiagnosticSeverity.Error, true
                    ), file.Location));
                }
                else
                {
                    File.WriteAllText(generatedPath, file.Contents);
                    filesMapping.add(file.SourcePath, generatedPath);
                }
            }
            foreach (var file in results.OfType<GeneratedJavaFile>())
            {
                if (!filesMapping.tryAddJavaFile(file.SourcePath, file.JavaFile))
                {
                    diagnostic.Add(Diagnostic.Create(new DiagnosticDescriptor(
                        "ER0002", "Error", $"Could not generate java file '{file.JavaFile.Path}'. File already exists.", "Error", DiagnosticSeverity.Error, true
                    ), file.Location));
                }
            }

            compilation = MacroProcessor.EditTrees(
                compilation, sourceMap, results.OfType<ModifiedFile>().Select(f => (f.From, f.To))
            );
            compilation = filesMapping.updateCompilation(compilation, parseOptions, assemblyName: assemblyName, generatedFilesDir: generatedProjectFilesDirectory);
            File.WriteAllLines(
                Path.Combine(GENERATED_FOLDER, $"Generated-files-{assemblyName}.txt"),
                filesMapping.filesDict.Values
                    .SelectMany(_ => _)
                    .Select(path => path.Replace("/", "\\")));
            return (compilation, diagnostic);
        }

        static Location attrLocation(AttributeData attr) => attr.ApplicationSyntaxReference.GetSyntax().GetLocation();

        static void SetNamedArguments(AttributeData attributeData, Attribute instance) {
            foreach (var arg in attributeData.NamedArguments)
            {
                // if some arguments are invelid they do not appear in NamedArguments list
                // because of that we do not check for errors
                var prop = caseType.GetProperty(arg.Key);
                prop.SetValue(instance, arg.Value.Value);
            }
        }

        static A CreateAttributeByReflection<A>(AttributeData attributeData) where A : Attribute{
            var type = typeof(A);
            var arguments = attributeData.ConstructorArguments;
            var ctor = type.GetConstructors().First(ci => ci.GetParameters().Length == arguments.Length);
            var res = (A) ctor.Invoke(arguments.Select(a => a.Value).ToArray());
            SetNamedArguments(attributeData, res);
            return res;
        }

        static string GenerateAccessor(IFieldSymbol fieldSymbol) {
            var name = fieldSymbol.Name;
            var newName = name.TrimStart('_');
            if (name == newName) newName += "_";
            return $"public {fieldSymbol.Type} {newName} => {name};";
        }

        private static MemberDeclarationSyntax GenerateMatcher(
            SemanticModel model, TypeDeclarationSyntax tds, ImmutableArray<TypeDeclarationSyntax> typesInFile)
        {
            // TODO: ban extendig this class in different files
            // TODO: generics ?

            var baseTypeSymbol = model.GetDeclaredSymbol(tds);
            var symbols = typesInFile
                .Select(t => model.GetDeclaredSymbol(t))
                // move current symbol to back
                .OrderBy(s => s.Equals(baseTypeSymbol));

            IEnumerable<INamedTypeSymbol> findTypes() { switch (tds) {
                case ClassDeclarationSyntax _:
                    return symbols.Where(s => {
                        if (!baseTypeSymbol.IsAbstract && s.Equals(baseTypeSymbol)) return true;
                        return s.BaseType?.Equals(baseTypeSymbol) ?? false;
                    });
                case InterfaceDeclarationSyntax _:
                    return symbols.Where(s => s.Interfaces.Contains(baseTypeSymbol));
                default:
                    throw new Exception($"{tds} - matcher should be added on class or interface");
            } }

            var childTypes = findTypes();

            /*
            public void match(Action<One> t1, Action<Two> t2) {
                var val1 = this as One;
                if (val1 != null) {
                    t1(val1);
                    return;
                }
                var val2 = this as Two;
                if (val2 != null) {
                    t2(val2);
                    return;
                }
            }
            */

            string toLowerFirstLetter(string s) => char.ToLowerInvariant(s[0]) + s.Substring(1);

            var childNames = childTypes.Select(s => (fullName: s.ToString(), varName: toLowerFirstLetter(s.Name))).ToArray();

            var firstParam = new[]{$"this {baseTypeSymbol} obj"};

            string VoidMatch()
            {
                var parameters = Join(", ", firstParam.Concat(childNames.Select(t => $"System.Action<{t.fullName}> {t.varName}")));
                var body = Join("\n", childNames.Select(t =>
                  $"var val_{t.varName} = obj as {t.fullName};" +
                  $"if (val_{t.varName} != null) {{ {t.varName}(val_{t.varName}); return; }}"));

                return $"public static void voidMatch({parameters}) {{{body}}}";
            }

            string Match()
            {
                var parameters = Join(", ", firstParam.Concat(childNames.Select(t => $"System.Func<{t.fullName}, A> {t.varName}")));
                var body = Join("\n", childNames.Select(t =>
                    $"var val_{t.varName} = obj as {t.fullName};" +
                    $"if (val_{t.varName} != null) return {t.varName}(val_{t.varName});"));

                return $"public static A match<A>({parameters}) {{" +
                       $"{body}" +
                       $"throw new ArgumentOutOfRangeException(\"obj\", obj, \"Should never reach this\");" +
                       $"}}";
            }

            return CreateStatic(tds, ParseClassMembers(VoidMatch() + Match()));
        }

        struct TypeWithIdentifier {
            public TypeSyntax type { get; }
            public SyntaxToken identifier { get; }

            public TypeWithIdentifier(TypeSyntax type, SyntaxToken identifier) {
                this.type = type;
                this.identifier = identifier;
            }
        }

        public class CaseClass : IEnumerable<TypeDeclarationSyntax> {
            readonly TypeDeclarationSyntax caseClass;
            readonly Maybe<TypeDeclarationSyntax> companion;

            public CaseClass(TypeDeclarationSyntax caseClass, Maybe<TypeDeclarationSyntax> companion) {
                this.caseClass = caseClass;
                this.companion = companion;
            }

            public IEnumerator<TypeDeclarationSyntax> GetEnumerator() {
                yield return caseClass;
                foreach (var c in companion.ToEnumerable()) yield return c;
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private static CaseClass GenerateCaseClass(
            RecordAttribute attr, SemanticModel model, TypeDeclarationSyntax cds
        ) {
            var properties = cds.Members.OfType<PropertyDeclarationSyntax>()
                .Where(prop => prop.Modifiers.HasNot(SyntaxKind.StaticKeyword))
                .Where(prop => prop.AccessorList?.Accessors.Any(ads =>
                    ads.IsKind(SyntaxKind.GetAccessorDeclaration)
                    && ads.Body == null
                    && ads.ExpressionBody == null
                ) ?? false)
                .Select(prop => (type: prop.Type, prop.Identifier));

            var fields = cds.Members.OfType<FieldDeclarationSyntax>()
                .Where(field => {
                    var modifiers = field.Modifiers;
                    return modifiers.HasNot(SyntaxKind.StaticKeyword) && modifiers.HasNot(SyntaxKind.ConstKeyword);
                })
                .SelectMany(field => {
                    var decl = field.Declaration;
                    var type = decl.Type;
                    return decl.Variables.Select(varDecl => (type, varDecl.Identifier));
                });

            var fieldsAndProps = fields.Concat(properties).ToArray();

            if (!fieldsAndProps.Any()) throw new Exception("The record has no fields and therefore cannot be created");

            var constructor = createIf(attr.GenerateConstructor, () =>
                ImmutableList.Create((MemberDeclarationSyntax) SF.ConstructorDeclaration(cds.Identifier)
                .WithModifiers(SF.TokenList(SF.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(SF.ParameterList(
                    SF.SeparatedList(fieldsAndProps.Select(f =>
                        SF.Parameter(f.Identifier).WithType(f.type)))))
                .WithBody(SF.Block(fieldsAndProps.Select(f => SF.ExpressionStatement(SF.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SF.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SF.ThisExpression(),
                        SF.IdentifierName(f.Identifier)), SF.IdentifierName(f.Identifier)))))))
            );

            var paramsStr =
                Join(", ", fieldsAndProps
                .Select(f => f.Identifier.ValueText)
                .Select(n => n + ": \" + " + n + " + \""));

            IEnumerable<MemberDeclarationSyntax> createIf(bool condition, Func<IEnumerable<MemberDeclarationSyntax>> a)
                => condition ? a() : Enumerable.Empty<MemberDeclarationSyntax>();

            var toString = createIf(
                attr.GenerateToString,
                () => ParseClassMembers(
                    $"public override string ToString() => \"{cds.Identifier.ValueText}(\" + \"{paramsStr})\";"
                )
            );

            var getHashCode = createIf(attr.GenerateGetHashCode, () => {
                var hashLines = Join("\n", fieldsAndProps.Select(f => {
                    var type = model.GetTypeInfo(f.type).Type;
                    var isValueType = type.IsValueType;
                    var name = f.Identifier.ValueText;
                    string ValueTypeHash(SpecialType sType)
                    {
                        switch (sType)
                        {
                            case SpecialType.System_Byte:
                            case SpecialType.System_SByte:
                            case SpecialType.System_Int16:
                            case SpecialType.System_Int32: return name;
                            case SpecialType.System_UInt32:
                            //TODO: `long` type enums should not cast
                            case SpecialType.System_Enum: return "(int) " + name;
                            default: return name + ".GetHashCode()";
                        }
                    }

                    var fieldHashCode = isValueType
                        ? ValueTypeHash(type.SpecialType)
                        : $"({name} == null ? 0 : {name}.GetHashCode())";
                    return $"hashCode = (hashCode * 397) ^ {(fieldHashCode)}; // {type.SpecialType}";
                }));
                return ParseClassMembers(
                $@"public override int GetHashCode() {{
                    unchecked {{
                        var hashCode = 0;
                        {hashLines}
                        return hashCode;
                    }}
                }}");
            });

            /*
            TODO: generic fields
            EqualityComparer<B>.Default.GetHashCode(valClass);
            EqualityComparer<B>.Default.Equals(valClass, other.valClass);
            */

            var typeName = cds.Identifier.ValueText + cds.TypeParameterList;

            var equals = createIf(attr.GenerateComparer, () => {
                var isStruct = cds.Kind() == SyntaxKind.StructDeclaration;
                var comparisons = fieldsAndProps.Select(f =>
                {
                    var type = model.GetTypeInfo(f.type).Type;
                    var name = f.Identifier.ValueText;
                    var otherName = "other." + name;
                    switch (type.SpecialType)
                    {
                        case SpecialType.System_Byte:
                        case SpecialType.System_SByte:
                        case SpecialType.System_Int16:
                        case SpecialType.System_UInt16:
                        case SpecialType.System_Int32:
                        case SpecialType.System_UInt32:
                        case SpecialType.System_Int64:
                        case SpecialType.System_UInt64:
                        case SpecialType.System_Enum: return $"{name} == {otherName}";
                        case SpecialType.System_String: return $"string.Equals({name}, {otherName})";
                        default: return $"{name}.Equals({otherName})";
                    }
                });
                var equalsExpr = isStruct ? "left.Equals(right)" : "Equals(left, right)";
                return ParseClassMembers(
                    $"public bool Equals({typeName} other) => {Join(" && ", comparisons)};" +
                    $"public override bool Equals(object obj) {{" +
                    $"  if (ReferenceEquals(null, obj)) return false;" +
                    (!isStruct ? "if (ReferenceEquals(this, obj)) return true;" : "") +
                    $"  return obj is {typeName} && Equals(({typeName}) obj);" +
                    $"}}" +
                    $"public static bool operator ==({typeName} left, {typeName} right) => {equalsExpr};" +
                    $"public static bool operator !=({typeName} left, {typeName} right) => !{equalsExpr};");
            });

            var baseList = attr.GenerateComparer
                // : IEquatable<TypeName>
                ? SF.BaseList(
                    SF.SingletonSeparatedList<BaseTypeSyntax>(
                        SF.SimpleBaseType(
                            SF.ParseTypeName($"System.IEquatable<{typeName}>")
                )))
                : Extensions.EmptyBaseList;
            var newMembers = constructor.Concat(toString).Concat(getHashCode).Concat(equals);

            // static apply bellow

            var propsAsStruct = fieldsAndProps.Select(_ => new TypeWithIdentifier(_.type, _.Identifier));
            var companion = Maybe.MZero<TypeDeclarationSyntax>();
            if (attr.GenerateStaticApply) {
                if (!attr.GenerateConstructor)
                    throw new Exception(
                        "Couldn't generate static apply because the record " +
                        "parameter GenerateConstructor is set to false."
                    );
                else {
                    if (cds.TypeParameterList == null) {
                        newMembers = newMembers.Concat(GenerateStaticApply(cds, propsAsStruct));
                    } else {
                        companion = Maybe.Just(GenerateCaseClassCompanion(attr, cds, propsAsStruct));
                    }
                }
            }

            var caseclass = CreatePartial(cds, newMembers, baseList);
            return new CaseClass(caseclass, companion);
        }

        static TypeDeclarationSyntax GenerateCaseClassCompanion(
            RecordAttribute attr, TypeDeclarationSyntax cds, IEnumerable<TypeWithIdentifier> props
        ) {
            var classModifiers =
                cds.Modifiers
                .RemoveOfKind(SyntaxKind.ReadOnlyKeyword)
                .Add(SyntaxKind.StaticKeyword);

            var applyMethod = GenerateStaticApply(cds, props);
            return SF.ClassDeclaration(cds.Identifier)
                    .WithModifiers(classModifiers)
                    .WithMembers(applyMethod);
        }

        static string joinCommaSeparated<A>(IEnumerable<A> collection, Func<A, string> mapper) =>
            collection
            .Select(mapper)
            .Aggregate((p1, p2) => p1 + ", " + p2);

        static SyntaxList<MemberDeclarationSyntax> GenerateStaticApply(
            TypeDeclarationSyntax cds, IEnumerable<TypeWithIdentifier> props
        ) {
            var genericArgsStr = cds.TypeParameterList?.ToFullString().TrimEnd() ?? "";
            var funcParamsStr = joinCommaSeparated(props, _ => _.type + " " + _.identifier.ValueText);
            var funcArgs = joinCommaSeparated(props, _ => _.identifier.ValueText);

            return ParseClassMembers(
                $"public static {cds.Identifier.ValueText}{genericArgsStr} a{genericArgsStr}" +
                $"({funcParamsStr}) => new {cds.Identifier.ValueText}{genericArgsStr}({funcArgs});"
            );
        }

        private static TypeDeclarationSyntax CreatePartial(
            TypeDeclarationSyntax originalType, IEnumerable<MemberDeclarationSyntax> newMembers, BaseListSyntax baseList
        ) =>
            CreateType(
                originalType.Kind(),
                originalType.Identifier,
                originalType.Modifiers.Add(SyntaxKind.PartialKeyword),
                originalType.TypeParameterList,
                SF.List(newMembers),
                baseList
            );

        private static TypeDeclarationSyntax CreateStatic(
            TypeDeclarationSyntax originalType, IEnumerable<MemberDeclarationSyntax> newMembers
        ) =>
            SF.ClassDeclaration(originalType.Identifier + "Matcher")
            .WithModifiers(SF
                .TokenList(originalType.Modifiers.Where(k => kindsForExtensionClass.Contains(k.Kind())))
                .Add(SyntaxKind.StaticKeyword))
            .WithMembers(SF.List(newMembers));

        public static TypeDeclarationSyntax CreateType(
            SyntaxKind kind, SyntaxToken identifier, SyntaxTokenList modifiers, TypeParameterListSyntax typeParams,
            SyntaxList<MemberDeclarationSyntax> members, BaseListSyntax baseList
        ) {
            switch (kind)
            {
                case SyntaxKind.ClassDeclaration:
                    return SF.ClassDeclaration(identifier)
                        .WithModifiers(modifiers)
                        .WithTypeParameterList(typeParams)
                        .WithMembers(members)
                        .WithBaseList(baseList);
                case SyntaxKind.StructDeclaration:
                    return SF.StructDeclaration(identifier)
                        .WithModifiers(modifiers)
                        .WithTypeParameterList(typeParams)
                        .WithMembers(members)
                        .WithBaseList(baseList);
                case SyntaxKind.InterfaceDeclaration:
                    return SF.InterfaceDeclaration(identifier)
                        .WithModifiers(modifiers)
                        .WithTypeParameterList(typeParams)
                        .WithMembers(members)
                        .WithBaseList(baseList);
                default:
                    throw new ArgumentOutOfRangeException(kind.ToString());
            }
        }

        // stolen from CodeGeneration.Roslyn
        static MemberDeclarationSyntax AddAncestors(MemberDeclarationSyntax memberNode, MemberDeclarationSyntax generatedType, bool onlyNamespace)
        {
            // Figure out ancestry for the generated type, including nesting types and namespaces.
            foreach (var ancestor in memberNode.Ancestors())
            {
                switch (ancestor)
                {
                    case NamespaceDeclarationSyntax a:
                        generatedType = SF.NamespaceDeclaration(a.Name)
                            .WithUsings(cleanUsings(a.Usings))
                            .WithMembers(SF.SingletonList(generatedType));
                        break;
                    case ClassDeclarationSyntax a:
                        if (onlyNamespace) break;
                        generatedType = a
                            .WithMembers(SF.SingletonList(generatedType))
                            .WithModifiers(a.Modifiers.Add(SyntaxKind.PartialKeyword))
                            .WithoutTrivia()
                            .WithCloseBraceToken(a.CloseBraceToken.WithoutTrivia())
                            .WithBaseList(Extensions.EmptyBaseList)
                            .WithAttributeLists(Extensions.EmptyAttributeList);
                        break;
                    case StructDeclarationSyntax a:
                        if (onlyNamespace) break;
                        generatedType = a
                            .WithMembers(SF.SingletonList(generatedType))
                            .WithModifiers(a.Modifiers.Add(SyntaxKind.PartialKeyword))
                            .WithoutTrivia()
                            .WithCloseBraceToken(a.CloseBraceToken.WithoutTrivia())
                            .WithBaseList(Extensions.EmptyBaseList)
                            .WithAttributeLists(Extensions.EmptyAttributeList);
                        break;
                }
            }
            return generatedType;
        }

        static SyntaxList<UsingDirectiveSyntax> cleanUsings(SyntaxList<UsingDirectiveSyntax> usings) =>
            SF.List(usings.Select(u =>
                u.WithUsingKeyword(u.UsingKeyword.WithoutTrivia())
            ));

        static ClassDeclarationSyntax ParseClass(string syntax)
        {
            var cls = (ClassDeclarationSyntax)CSharpSyntaxTree.ParseText(syntax).GetCompilationUnitRoot().Members[0];
            return cls;
        }

        static SyntaxList<MemberDeclarationSyntax> ParseClassMembers(string syntax)
        {
            var cls = (ClassDeclarationSyntax)CSharpSyntaxTree.ParseText($"class C {{ {syntax} }}").GetCompilationUnitRoot().Members[0];
            return cls.Members;
        }

        static BlockSyntax ParseBlock(string syntax) {
            return (BlockSyntax) SF.ParseStatement("{" + syntax + "}");
        }

        public static string Quote(string s) => $"\"{s}\"";
    }
}
