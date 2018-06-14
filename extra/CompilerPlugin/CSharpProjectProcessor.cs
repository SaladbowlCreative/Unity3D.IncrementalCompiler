using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

// This script modifies .csproj files:
// 1) enables unsafe code,
// 2) removes C# 4.0 version restriction introduced by Visual Studio Tools for Unity.

[InitializeOnLoad]
public class CSharpProjectPostprocessor : AssetPostprocessor
{
    #region Overrides of AssetPostprocessor

    // We need to run this after Rider postprocessor, because we override LangVersion
    // Rider postprocessor has order of 10
    public override int GetPostprocessOrder() => 11;

    #endregion

    // In case VSTU is installed
    static CSharpProjectPostprocessor() {
        OnGeneratedCSProjectFiles();
        /*foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.FullName.StartsWith("SyntaxTree.VisualStudio.Unity.Bridge") == false)
            {
                continue;
            }

            var projectFilesGeneratorType = assembly.GetType("SyntaxTree.VisualStudio.Unity.Bridge.ProjectFilesGenerator");
            if (projectFilesGeneratorType == null)
            {
                Debug.Log("Type 'SyntaxTree.VisualStudio.Unity.Bridge.ProjectFilesGenerator' not found");
                return;
            }

            var delegateType = assembly.GetType("SyntaxTree.VisualStudio.Unity.Bridge.FileGenerationHandler");
            if (delegateType == null)
            {
                Debug.Log("Type 'SyntaxTree.VisualStudio.Unity.Bridge.FileGenerationHandler' not found");
                return;
            }

            var projectFileGenerationField = projectFilesGeneratorType.GetField("ProjectFileGeneration", BindingFlags.Static | BindingFlags.Public);
            if (projectFileGenerationField == null)
            {
                Debug.Log("Field 'ProjectFileGeneration' not found");
                return;
            }

            var handlerMethodInfo = typeof(CSharpProjectPostprocessor).GetMethod(nameof(ModifyProjectFile), BindingFlags.Static | BindingFlags.NonPublic);
            var handlerDelegate = Delegate.CreateDelegate(delegateType, null, handlerMethodInfo);

            var delegateValue = (Delegate)projectFileGenerationField.GetValue(null);
            delegateValue = delegateValue == null ? handlerDelegate : Delegate.Combine(delegateValue, handlerDelegate);
            projectFileGenerationField.SetValue(null, delegateValue);

            return;
        }*/
    }

    // In case VSTU is not installed
    private static void OnGeneratedCSProjectFiles()
    {
        Debug.Log("Incremental compiler: " + nameof(OnGeneratedCSProjectFiles));
        /*if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName.StartsWith("SyntaxTree.VisualStudio.Unity.Bridge")))
        {
            return;
        }*/

        foreach (string projectFile in Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj"))
        {
            string content = File.ReadAllText(projectFile);
            content = ModifyProjectFile(Path.GetFileNameWithoutExtension(projectFile), content);
            File.WriteAllText(projectFile, content);
        }
    }

    private static string ModifyProjectFile(string name, string content)
    {
        var xdoc = XDocument.Parse(content);

        SetUpCorrectLangVersion(xdoc);
        EnableUnsafeCode(xdoc);
        RemoveAnnoyingReferences(xdoc);

        AddDefine(xdoc, CustomCSharpCompiler.COMPILER_DEFINE);
        {
            var ns = xdoc.Root.GetDefaultNamespace();
            var compileName = ns + "Compile";
            var includeName = (XName) "Include";
            const string GENERATED = "Generated";
            var generatedPathStart = GENERATED + Path.DirectorySeparatorChar;

            void RemoveOldGeneratedFiles()
            {
                foreach (var fileNode in xdoc.Root.Descendants(compileName))
                {
                    if (fileNode.Attribute(includeName)?.Value.StartsWith(generatedPathStart, StringComparison.Ordinal) ?? false)
                    {
                        fileNode.Remove();
                    }
                }
            }

            void AddGeneratedFiles()
            {
                var assemblyName = xdoc.Root.Descendants(ns + "AssemblyName").Select(el => el.Value).First();
                // .dll suffix appears here if we select VS2017 in unity preferences
                assemblyName = EnsureDoesNotEndWith(assemblyName, ".dll");
                var filename = Path.Combine(GENERATED, $"Generated-files-{assemblyName}.txt");
                if (!File.Exists(filename)) return;
                var filesToAdd = File.ReadAllLines(filename);
                var newFiles = filesToAdd.Select(
                    file => (object)new XElement(compileName, new XAttribute(includeName, file))
                );
                xdoc.Root.Add(new XElement(ns + "ItemGroup", newFiles.ToArray()));
            }

            RemoveOldGeneratedFiles();
            AddGeneratedFiles();
        }

        var writer = new Utf8StringWriter();
        xdoc.Save(writer);
        return writer.ToString();
    }



    static string EnsureDoesNotEndWith(string s, string suffix) =>
        s.EndsWith(suffix, StringComparison.Ordinal)
        ? s.Substring(s.Length - suffix.Length)
        : s;

    private class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    private static void SetUpCorrectLangVersion(XDocument xdoc)
    {
        var csharpVersion = "latest";
        /*
        if (Directory.Exists("CSharp70Support"))
        {
            csharpVersion = "7";
        }
        else if (Directory.Exists("CSharp60Support"))
        {
            csharpVersion = "6";
        }
        else
        {
            csharpVersion = "default";
        }*/

        var ns = xdoc.Root.GetDefaultNamespace();
        xdoc.Descendants(ns + "LangVersion").Remove();
        var propertyGroupElement = xdoc.Descendants(ns + "PropertyGroup").First();
        propertyGroupElement.Add(new XElement(ns + "LangVersion", csharpVersion));
    }

    private static void EnableUnsafeCode(XDocument xdoc)
    {
        var ns = xdoc.Root.GetDefaultNamespace();
        xdoc.Descendants(ns + "AllowUnsafeBlocks").Remove();
        var propertyGroup = xdoc.Descendants(ns + "PropertyGroup").First();
        propertyGroup.Add(new XElement(ns + "AllowUnsafeBlocks", "True"));
    }

    private static void RemoveAnnoyingReferences(XDocument xdoc)
    {
        var ns = xdoc.Root.GetDefaultNamespace();

        (from element in xdoc.Descendants(ns + "Reference")
         let include = element.Attribute("Include").Value
         where include == "Boo.Lang" || include == "UnityScript.Lang"
         select element).Remove();
    }

    private static void AddDefine(XDocument xdoc, string define)
    {
        var ns = xdoc.Root.GetDefaultNamespace();
        foreach (var defines in xdoc.Descendants(ns + "DefineConstants"))
        {
            defines.Value += ";" + define;
        }
    }
}
