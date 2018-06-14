#define LOGGING_ENABLED

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

internal class Program
{
    private const string LANGUAGE_SUPPORT_DIR = "Compiler";
    private const string CSHARP_60_SUPPORT_DIR = "CSharp60Support";
	private const string CSHARP_70_SUPPORT_DIR = "CSharp70Support";

	private static int Main(string[] args)
	{
		int exitCode;
		Logger logger = null;

		Settings settings;
		try
		{
			settings = Settings.Load() ?? Settings.Default;
		}
		catch (Exception e)
		{
			Console.Error.Write("Failed in loading settings: " + e);
			return 1;
		}

#if LOGGING_ENABLED
		using (logger = new Logger())
#endif
		{
			try
			{
				exitCode = Compile(args, logger, settings);
			}
			catch (Exception e)
			{
				exitCode = -1;
				Console.Error.Write($"Compiler redirection error: {e.GetType()}{Environment.NewLine}{e.Message} {e.StackTrace}");
			}
		}

		return exitCode;
	}

	private static int Compile(string[] args, Logger logger, Settings settings)
	{
		logger?.AppendHeader();

		string responseFile = args[0];
		var compilationOptions = File.ReadAllLines(responseFile.TrimStart('@'));
		string targetProfileDir = GetTargetProfileDir(compilationOptions);
		string unityEditorDataDir = GetUnityEditorDataDir();
		string projectDir = Directory.GetCurrentDirectory();
		string targetAssembly = compilationOptions.First(line => line.StartsWith("-out:"))
												  .Replace("'", "")
												  .Replace("\"", "")
												  .Substring(10);

		logger?.Append($"CSharpCompilerWrapper.exe version: {GetExecutingAssemblyFileVersion()}");
		logger?.Append($"Platform: {CurrentPlatform}");
		logger?.Append($"Target assembly: {targetAssembly}");
		logger?.Append($"Project directory: {projectDir}");
		logger?.Append($"Target profile directory: {targetProfileDir}");
		logger?.Append($"Unity 'Data' or 'Frameworks' directory: {unityEditorDataDir}");

		if (CurrentPlatform == Platform.Linux)
		{
			logger?.Append("");
			logger?.Append("Platform is not supported");
			return -1;
		}

	    var compiler = CreateCompiler(settings.Compiler, logger, CurrentPlatform, targetProfileDir, projectDir, compilationOptions, unityEditorDataDir);

        logger?.Append($"Compiler: {compiler.Name}");
		logger?.Append("");
		logger?.Append("- Compilation -----------------------------------------------");
		logger?.Append("");

		var stopwatch = Stopwatch.StartNew();
		int exitCode = compiler.Compile(CurrentPlatform, unityEditorDataDir, targetProfileDir, responseFile);
		stopwatch.Stop();

		logger?.Append($"Elapsed time: {stopwatch.ElapsedMilliseconds / 1000f:F2} sec");
		logger?.Append("");
		compiler.PrintCompilerOutputAndErrors();

		if (exitCode != 0 || compiler.NeedsPdb2MdbConversion == false)
		{
			return exitCode;
		}

		if (CurrentPlatform == Platform.Windows)
		{
			logger?.Append("");
			logger?.Append("- PDB to MDB conversion --------------------------------------");
			logger?.Append("");

			stopwatch.Reset();
			stopwatch.Start();

			string targetAssemblyPath = Path.Combine("Temp", targetAssembly);
			compiler.ConvertDebugSymbols(CurrentPlatform, targetAssemblyPath, unityEditorDataDir);

			stopwatch.Stop();
			logger?.Append($"Elapsed time: {stopwatch.ElapsedMilliseconds / 1000f:F2} sec");
			logger?.Append("");
			compiler.PrintPdb2MdbOutputAndErrors();
		}
		else // Roslyn creates only portable pdbs on Mac OS which are currently not supported in Unity.
		{
			string targetAssemblyPath = Path.Combine("Temp", targetAssembly);
			string pdbPath = Path.Combine("Temp", Path.GetFileNameWithoutExtension(targetAssemblyPath) + ".pdb");
			logger?.Append("");
			logger?.Append("PDB to MDB conversion skipped");
			if (File.Exists(pdbPath))
			{
				File.Delete(pdbPath);
				logger?.Append($"File \"{pdbPath}\" deleted");
			}
		}

		return 0;
	}


    // TODO: clean this mess
    private static Compiler CreateCompiler(CompilerType compilerType, Logger logger, Platform platform, string monoProfileDir, string projectDir, string[] compilationOptions, string unityEditorDataDir)
    {
        var compilerDirectory = Path.Combine(projectDir, LANGUAGE_SUPPORT_DIR);

        switch (compilerType)
        {
            case CompilerType.Auto:
                return FindSuitableCompiler(logger, platform, monoProfileDir, projectDir, compilationOptions, unityEditorDataDir);

            case CompilerType.Mono3:
                var stockCompilerPath = monoProfileDir.IndexOf("2.0") != -1
                    ? Path.Combine(unityEditorDataDir, @"Mono/lib/mono/2.0/gmcs.exe")
                    : Path.Combine(monoProfileDir, "smcs.exe");
                return new Mono30Compiler(logger, stockCompilerPath);

            case CompilerType.Mono6:
                if (Mono60Compiler.IsAvailable(compilerDirectory))
                    return new Mono60Compiler(logger, compilerDirectory);
                break;

            case CompilerType.Microsoft6:
                var roslynDirectory = Path.Combine(compilerDirectory, "Roslyn");
                if (MicrosoftCompiler.IsAvailable(roslynDirectory))
                    return new MicrosoftCompiler(logger, roslynDirectory);
                break;

            case CompilerType.Incremental6:
                if (Incremental60Compiler.IsAvailable(compilerDirectory))
                    return new Incremental60Compiler(logger, compilerDirectory);
                break;
        }

        return null;
    }

    private static Compiler FindSuitableCompiler(Logger logger, Platform platform, string monoProfileDir, string projectDir, string[] compilationOptions, string unityEditorDataDir)
    {
        Compiler compiler = null;

        // Looking for Incremental C# 6.0 compiler
        var icscDirectory = Path.Combine(projectDir, LANGUAGE_SUPPORT_DIR);
        if (Incremental60Compiler.IsAvailable(icscDirectory))
        {
            compiler = new Incremental60Compiler(logger, icscDirectory);
        }

        // Looking for Roslyn C# 6.0 or 7.0 compiler
        string roslyn60Directory = Path.Combine(Path.Combine(projectDir, CSHARP_60_SUPPORT_DIR), "Roslyn");
        string roslyn70Directory = Path.Combine(Path.Combine(projectDir, CSHARP_70_SUPPORT_DIR), "Roslyn");

        if (MicrosoftCompiler.IsAvailable(roslyn70Directory))
        {
            compiler = new MicrosoftCompiler(logger, roslyn70Directory);
        }
        else if (MicrosoftCompiler.IsAvailable(roslyn60Directory))
        {
            compiler = new MicrosoftCompiler(logger, roslyn60Directory);
        }

		if (compiler == null)
		{
			// Looking for Mono C# 6.0 compiler
			string mcsDirectory = Path.Combine(projectDir, CSHARP_60_SUPPORT_DIR);
			if (Mono60Compiler.IsAvailable(mcsDirectory))
			{
				compiler = new Mono60Compiler(logger, mcsDirectory);
			}
		}

        if (compiler == null)
        {
            // Using stock Mono C# 3.0 compiler
            string stockCompilerPath = Path.Combine(unityEditorDataDir, @"Mono/lib/mono/2.0/gmcs.exe");
            compiler = new Mono30Compiler(logger, stockCompilerPath);
        }

        return compiler;
    }

	private static Platform CurrentPlatform
	{
		get
		{
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Unix:
					// Well, there are chances MacOSX is reported as Unix instead of MacOSX.
					// Instead of platform check, we'll do a feature checks (Mac specific root folders)
					if (Directory.Exists("/Applications")
						& Directory.Exists("/System")
						& Directory.Exists("/Users")
						& Directory.Exists("/Volumes"))
					{
						return Platform.Mac;
					}
					return Platform.Linux;

				case PlatformID.MacOSX:
					return Platform.Mac;

				default:
					return Platform.Windows;
			}
		}
	}

	/// <summary>
	/// Returns the directory that contains Mono and MonoBleedingEdge directories
	/// </summary>
	private static string GetUnityEditorDataDir()
	{
		// Windows:
		// MONO_PATH: C:\Program Files\Unity\Editor\Data\Mono\lib\mono\2.0
		//
		// Mac OS X:
		// MONO_PATH: /Applications/Unity/Unity.app/Contents/Frameworks/Mono/lib/mono/2.0

		string monoPath = Environment.GetEnvironmentVariable("MONO_PATH").Replace("\\", "/");
		int index = monoPath.IndexOf("/Mono/lib/", StringComparison.InvariantCultureIgnoreCase);
		string path = monoPath.Substring(0, index);
		return path;
	}

	private static string GetTargetProfileDir(string[] compilationOptions)
	{
		/* Looking for something like
		-r:"C:\Program Files\Unity\Editor\Data\Mono\lib\mono\unity\System.Xml.Linq.dll"
		or
		-r:'C:\Program Files\Unity\Editor\Data\Mono\lib\mono\unity\System.Xml.Linq.dll'
		*/
		string reference = compilationOptions.First(line => line.StartsWith("-r:") && line.Contains("System.Xml.Linq.dll"));

		string systemXmlLinqPath = reference.Replace("'", "").Replace("\"", "").Substring(3);
		string profileDir = Path.GetDirectoryName(systemXmlLinqPath);
		return profileDir;
	}

	private static string GetExecutingAssemblyFileVersion()
	{
		var assembly = Assembly.GetExecutingAssembly();
		var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
		return fvi.FileVersion;
	}
}
