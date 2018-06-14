using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal class MicrosoftCompiler : Compiler
{
	private readonly string compilerDirectory;
	private bool IsCSharp70Available => compilerDirectory.Contains("CSharp70Support");

	public override string Name => IsCSharp70Available ? "Microsoft C# 7.0" : "Microsoft C# 6.0";

	public override bool NeedsPdb2MdbConversion => true;

	public MicrosoftCompiler(Logger logger, string directory)
		: base(logger, Path.Combine(directory, "csc.exe"), Path.Combine(directory, "pdb2mdb.exe"))
	{
		compilerDirectory = directory;
	}

	public static bool IsAvailable(string directory) => File.Exists(Path.Combine(directory, "csc.exe")) &&
														File.Exists(Path.Combine(directory, "pdb2mdb.exe"));

	protected override Process CreateCompilerProcess(Platform platform, string unityEditorDataDir, string targetProfileDir, string responseFile)
	{
		if (platform == Platform.Mac)
		{
			string filename = responseFile.TrimStart('@');
			string content = File.ReadAllText(filename);
			content = content.Replace('\'', '\"');
			File.WriteAllText(filename, content);
		}

		string systemDllPath = Path.Combine(targetProfileDir, @"System.dll");
		string systemCoreDllPath = Path.Combine(targetProfileDir, @"System.Core.dll");
		string systemXmlDllPath = Path.Combine(targetProfileDir, @"System.Xml.dll");
		string mscorlibDllPath = Path.Combine(targetProfileDir, @"mscorlib.dll");

		string debugOption;
		if (platform == Platform.Mac)
		{
			debugOption = IsCSharp70Available ? "-debug:portable" : "-debug-";
		}
		else
		{
			debugOption = "";
		}
		string processArguments = $"-nostdlib+ -noconfig -nologo "
								  + $"-r:\"{mscorlibDllPath}\" "
								  + $"-r:\"{systemDllPath}\" "
								  + $"-r:\"{systemCoreDllPath}\" "
								  + $"-r:\"{systemXmlDllPath}\" " + responseFile + " " + debugOption;

		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR40, compilerPath, processArguments, unityEditorDataDir);
		return process;
	}

	public override void ConvertDebugSymbols(Platform platform, string targetAssemblyPath, string unityEditorDataDir)
	{
		outputLines.Clear();

		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR40, pbd2MdbPath, targetAssemblyPath, unityEditorDataDir);
		process.OutputDataReceived += (sender, e) => outputLines.Add(e.Data);

		logger?.Append($"Process: {process.StartInfo.FileName}");
		logger?.Append($"Arguments: {process.StartInfo.Arguments}");

		process.Start();
		process.BeginOutputReadLine();
		process.WaitForExit();
		logger?.Append($"Exit code: {process.ExitCode}");

		string pdbPath = Path.Combine("Temp", Path.GetFileNameWithoutExtension(targetAssemblyPath) + ".pdb");
		File.Delete(pdbPath);
	}

	public override void PrintCompilerOutputAndErrors()
	{
		// Microsoft's compiler writes all warnings and errors to the standard output channel,
		// so move them to the error channel

		errorLines.AddRange(outputLines);
		outputLines.Clear();

		base.PrintCompilerOutputAndErrors();
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