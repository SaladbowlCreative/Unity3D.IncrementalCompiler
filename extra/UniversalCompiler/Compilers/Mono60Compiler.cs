using System.Diagnostics;
using System.IO;
using System.Linq;

internal class Mono60Compiler : Compiler
{
	public Mono60Compiler(Logger logger, string directory)
		: base(logger, Path.Combine(directory, "mcs.exe"), null) { }

	public override string Name => "Mono C# 6.0";
    public override bool NeedsPdb2MdbConversion => false;

	protected override Process CreateCompilerProcess(Platform platform, string unityEditorDataDir, string targetProfileDir, string responseFile)
	{
		string systemDllPath = Path.Combine(targetProfileDir, @"System.dll");
		string systemCoreDllPath = Path.Combine(targetProfileDir, @"System.Core.dll");
		string systemXmlDllPath = Path.Combine(targetProfileDir, @"System.Xml.dll");
		string mscorlibDllPath = Path.Combine(targetProfileDir, @"mscorlib.dll");

		string processArguments = "-nostdlib+ -noconfig -nologo "
								  + $"-r:\"{mscorlibDllPath}\" "
								  + $"-r:\"{systemDllPath}\" "
								  + $"-r:\"{systemCoreDllPath}\" "
								  + $"-r:\"{systemXmlDllPath}\" " + responseFile;

		FixTvosIosIssue(responseFile.TrimStart('@'));

		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR40, compilerPath, processArguments, unityEditorDataDir);
		return process;
	}

	private void FixTvosIosIssue(string responseFile)
	{
		var lines = File.ReadAllLines(responseFile).Select(line => line.Replace('\\', '/')).ToList();

		bool definedTVOS = lines.Contains("-define:UNITY_TVOS");
		if (definedTVOS == false)
		{
			lines.RemoveAll(line => line.Contains("/PlaybackEngines/AppleTVSupport/UnityEditor.iOS.Extensions."));
		}

		bool definedIOS = lines.Contains("-define:UNITY_IOS");
		if (definedIOS == false)
		{
			lines.RemoveAll(line => line.Contains("/PlaybackEngines/iOSSupport/UnityEditor.iOS.Extensions."));
		}

		File.WriteAllLines(responseFile, lines.ToArray());
	}

	public static bool IsAvailable(string directory) => File.Exists(Path.Combine(directory, "mcs.exe"));
}
