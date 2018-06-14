using System.Diagnostics;

internal class Mono30Compiler : Compiler
{
	public Mono30Compiler(Logger logger, string compilerPath) : base(logger, compilerPath, null) { }
	public override string Name => "Mono C# 3.0";
    public override bool NeedsPdb2MdbConversion => false;

	protected override Process CreateCompilerProcess(Platform platform, string unityEditorDataDir, string targetProfileDir, string responseFile)
	{
		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR20, compilerPath, responseFile, unityEditorDataDir);
		return process;
	}
}
