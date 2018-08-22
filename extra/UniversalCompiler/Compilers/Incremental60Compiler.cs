using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal class Incremental60Compiler : Compiler
{
	public override string Name => "Incremental C# Compiler 6.0";
	public override bool NeedsPdb2MdbConversion => false;

	public Incremental60Compiler(Logger logger, string directory)
		: base(logger, Path.Combine(directory, "IncrementalCompiler.exe"), Path.Combine(directory, "pdb2mdb.exe")) { }

	public static bool IsAvailable(string directory) => File.Exists(Path.Combine(directory, "IncrementalCompiler.exe"));

	protected override Process CreateCompilerProcess(Platform platform, string unityEditorDataDir, string targetProfileDir, string responseFile)
	{
	    if (platform == Platform.Mac)
        {
	        string filename = responseFile.TrimStart('@');
	        string content = File.ReadAllText(filename);
	        content = content.Replace('\'', '\"');
	        File.WriteAllText(filename, content);
	    }

        var systemDllPath = Path.Combine(targetProfileDir, "System.dll");
		var systemCoreDllPath = Path.Combine(targetProfileDir, "System.Core.dll");
		var systemXmlDllPath = Path.Combine(targetProfileDir, "System.Xml.dll");
		var mscorlibDllPath = Path.Combine(targetProfileDir, "mscorlib.dll");

		string processArguments = "-nostdlib+ -noconfig "
								  + $"-r:\"{mscorlibDllPath}\" "
								  + $"-r:\"{systemDllPath}\" "
								  + $"-r:\"{systemCoreDllPath}\" "
								  + $"-r:\"{systemXmlDllPath}\" " + responseFile;

		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR40, compilerPath, processArguments, unityEditorDataDir);
		return process;
	}

	public override void ConvertDebugSymbols(Platform platform, string libraryPath, string unityEditorDataDir)
	{
		outputLines.Clear();

		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR40, pbd2MdbPath, libraryPath, unityEditorDataDir);
		process.OutputDataReceived += (sender, e) => outputLines.Add(e.Data);

		logger?.Append($"Process: {process.StartInfo.FileName}");
		logger?.Append($"Arguments: {process.StartInfo.Arguments}");

		process.Start();
		process.BeginOutputReadLine();
		process.WaitForExit();
		logger?.Append($"Exit code: {process.ExitCode}");

		var pdbPath = Path.Combine("Temp", Path.GetFileNameWithoutExtension(libraryPath) + ".pdb");
		File.Delete(pdbPath);
	}

	public override void PrintPdb2MdbOutputAndErrors()
	{
		var lines = (from line in outputLines
					 let trimmedLine = line?.Trim()
					 where string.IsNullOrEmpty(trimmedLine) == false
					 select trimmedLine).ToList();

		logger?.Append($"- pdb2mdb.exe output ({lines.Count} {(lines.Count == 1 ? "line" : "lines")}):");

		for (int i = 0; i < lines.Count; i++)
		{
			Console.Out.WriteLine(lines[i]);
			logger?.Append($"{i}: {lines[i]}");
		}
	}
}
