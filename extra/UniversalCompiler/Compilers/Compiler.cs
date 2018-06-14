using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal abstract class Compiler
{
	protected enum ProcessRuntime
	{
		CLR40,
		CLR20,
	}

	public abstract string Name { get; }
	public abstract bool NeedsPdb2MdbConversion { get; }

	protected readonly Logger logger;
	protected readonly string compilerPath;
	protected readonly string pbd2MdbPath;

	protected readonly List<string> outputLines = new List<string>();
	protected readonly List<string> errorLines = new List<string>();

	protected Compiler(Logger logger, string compilerPath, string pbd2MdbPath = null)
	{
		this.logger = logger;
		this.compilerPath = compilerPath;
		this.pbd2MdbPath = pbd2MdbPath;
	}

	public int Compile(Platform platform, string unityEditorDataDir, string targetProfileDir, string responseFile)
	{
		var process = CreateCompilerProcess(platform, unityEditorDataDir, targetProfileDir, responseFile);
		process.OutputDataReceived += (sender, e) => outputLines.Add(e.Data);
		process.ErrorDataReceived += (sender, e) => errorLines.Add(e.Data);

		logger?.Append($"Process: {process.StartInfo.FileName}");
		logger?.Append($"Arguments: {process.StartInfo.Arguments}");

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		process.WaitForExit();

		logger?.Append($"Exit code: {process.ExitCode}");

		return process.ExitCode;
	}

	public virtual void PrintCompilerOutputAndErrors()
	{
		var lines = (from line in outputLines
					 let trimmedLine = line?.Trim()
					 where string.IsNullOrEmpty(trimmedLine) == false
					 select trimmedLine).ToList();

		logger?.Append($"- Compiler output ({lines.Count} {(lines.Count == 1 ? "line" : "lines")}):");

		for (int i = 0; i < lines.Count; i++)
		{
			Console.Out.WriteLine(lines[i]);
			logger?.Append($"{i}: {lines[i]}");
		}

		lines = (from line in errorLines
				 let trimmedLine = line?.Trim()
				 where string.IsNullOrEmpty(trimmedLine) == false
				 select trimmedLine).ToList();

		logger?.Append("");
		logger?.Append($"- Compiler errors ({lines.Count} {(lines.Count == 1 ? "line" : "lines")}):");

		for (int i = 0; i < lines.Count; i++)
		{
			Console.Error.WriteLine(lines[i]);
			logger?.Append($"{i}: {lines[i]}");
		}
	}

	protected abstract Process CreateCompilerProcess(Platform platform, string unityEditorDataDir, string targetProfileDir, string responseFile);

	public virtual void ConvertDebugSymbols(Platform platform, string targetAssemblyPath, string unityEditorDataDir) { }

	protected static ProcessStartInfo CreateOSDependentStartInfo(Platform platform, ProcessRuntime processRuntime, string processPath,
																 string processArguments, string unityEditorDataDir)
	{
		ProcessStartInfo startInfo;

		if (platform == Platform.Windows)
		{
			startInfo = new ProcessStartInfo(processPath, processArguments);
		}
		else
		{
			string runtimePath;
			switch (processRuntime)
			{
				case ProcessRuntime.CLR40:
					if (File.Exists("/Library/Frameworks/Mono.framework/Commands/mono"))
					{
						runtimePath = "/Library/Frameworks/Mono.framework/Commands/mono";
					}
					else if (File.Exists("/usr/local/bin/mono"))
					{
						runtimePath = "/usr/local/bin/mono";
					}
					else
					{
						runtimePath = Path.Combine(unityEditorDataDir, "MonoBleedingEdge/bin/mono");
					}
					break;

				case ProcessRuntime.CLR20:
					runtimePath = Path.Combine(unityEditorDataDir, @"Mono/bin/mono");
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(processRuntime), processRuntime, null);
			}

			startInfo = new ProcessStartInfo(runtimePath, $"\"{processPath}\" {processArguments}");

			if (processRuntime != ProcessRuntime.CLR20)
			{
				// Since we already are running under old mono runtime, we need to remove
				// these variables before launching the different version of the runtime.
				var vars = startInfo.EnvironmentVariables;
				vars.Remove("MONO_PATH");
				vars.Remove("MONO_CFG_DIR");
			}
		}

		startInfo.RedirectStandardError = true;
		startInfo.RedirectStandardOutput = true;
		startInfo.UseShellExecute = false;

		return startInfo;
	}

	public virtual void PrintPdb2MdbOutputAndErrors()
	{
		throw new NotSupportedException();
	}
}
