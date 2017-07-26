using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Scripting;
using UnityEditor.Scripting.Compilers;

[InitializeOnLoad]
public static class CSharp60SupportActivator
{
	static CSharp60SupportActivator()
	{
		var list = GetSupportedLanguages();
		list.RemoveAll(language => language is CSharpLanguage);
		list.Add(new CustomCSharpLanguage());
	}

	private static List<SupportedLanguage> GetSupportedLanguages()
	{
        var fieldInfo = typeof(ScriptCompilers).GetField("SupportedLanguages", BindingFlags.NonPublic | BindingFlags.Static);
        var languages = (List<SupportedLanguage>)fieldInfo.GetValue(null);
		return languages;
	}
}