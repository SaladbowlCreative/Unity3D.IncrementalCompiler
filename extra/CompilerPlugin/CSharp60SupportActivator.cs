using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Scripting;
using UnityEditor.Scripting.Compilers;
using Harmony;

[InitializeOnLoad]
public static class CSharp60SupportActivator
{
	static CSharp60SupportActivator()
	{
        var unity5Field = typeof(ScriptCompilers).GetField("_supportedLanguages", BindingFlags.NonPublic | BindingFlags.Static);
        if (unity5Field != null)
        {
            var list = (List<SupportedLanguage>)unity5Field.GetValue(null);
            list.RemoveAll(language => language is CSharpLanguage);
            list.Add(new CustomCSharpLanguage());
        } else
        {
            // Unity 2017+
            var harmony = HarmonyInstance.Create("CSharpVNextSupport");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
	}

	private static List<SupportedLanguage> GetSupportedLanguages(FieldInfo fieldInfo)
	{
		var languages = (List<SupportedLanguage>)fieldInfo.GetValue(null);
		return languages;
	}
}


[HarmonyPatch(typeof(CSharpLanguage))]
[HarmonyPatch("CreateCompiler")]
internal static class Patch
{
    private static bool Prefix(ref ScriptCompilerBase __result, MonoIsland island, bool buildingForEditor, BuildTarget targetPlatform, bool runUpdater)
    {
        __result = new CustomCSharpCompiler(island, runUpdater);
        return false;
    }
}
