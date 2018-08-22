using System;
using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Debug = UnityEngine.Debug;

public class CompilerSettings : EditorWindow
{
    public enum CompilerType
    {
        Auto,
        Mono3,
        Mono5,
        Mono6,
        Microsoft6,
        Incremental6,
    }

    public struct UniversalCompilerSettings
    {
        public CompilerType Compiler;
    }

    public enum DebugSymbolFileType
    {
        None,
        Pdb,
        PdbToMdb,
        Mdb
    }

    public enum PrebuiltOutputReuseType
    {
        None,
        WhenNoChange,
        WhenNoSourceChange
    }

    public struct IncrementalCompilerSettings
    {
        public DebugSymbolFileType DebugSymbolFile;
        public PrebuiltOutputReuseType PrebuiltOutputReuse;
    }

    private readonly string[] BuildTargets = { "Assembly-CSharp-firstpass", "Assembly-CSharp", "Assembly-CSharp-Editor" };
    private const string UcsFilePath = "./Compiler/UniversalCompiler.xml";
    private const string UcLogFilePath = "./Compiler/Temp/UniversalCompiler.log";
    private const string IcsFilePath = "./Compiler/IncrementalCompiler.xml";

    private DateTime _ucsLastWriteTime;
    private UniversalCompilerSettings _ucs;
    private string _ucVersion;
    private string[] _ucLastBuildLog = { "", "", "" };
    private DateTime _icsLastWriteTime;
    private IncrementalCompilerSettings _ics;
    private Process _icProcess;

    [MenuItem("Assets/Open C# Compiler Settings...")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(CompilerSettings));
    }

    public void OnDisable()
    {
        // When unity3d builds projec reloads built assemblies, build logs should be updated.
        // OnDisable is called just after starting building and it can make unity3d redraw this window.
        // http://answers.unity3d.com/questions/704066/callback-before-unity-reloads-editor-assemblies.html
        Repaint();
    }

    public void OnGUI()
    {
        OnGUI_Compiler();
        OnGUI_IncrementalCompilerSettings();
        OnGUI_IncrementalCompilerStatus();
    }

    private void OnGUI_Compiler()
    {
        GUILayout.Label("Compiler", EditorStyles.boldLabel);

        LoadUniversalCompilerSettings();
        UniversalCompilerSettings ucs;
        ucs.Compiler = (CompilerType)EditorGUILayout.EnumPopup("Compiler:", _ucs.Compiler);
        if (ucs.Equals(_ucs) == false)
        {
            _ucs = ucs;
            SaveUniversalCompilerSettings();
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Version", GetUniversalCompilerVersion());
        EditorGUI.BeginDisabledGroup(!File.Exists(UcLogFilePath));
        if (GUILayout.Button("Log"))
            ShowUniversalCompilerClientLog();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        var durations = GetUniversalCompilerLastBuildLogs();
        GUILayout.Label("Last Build Time");
        EditorGUI.indentLevel += 1;
        for (var i=0; i< BuildTargets.Length; i++)
            EditorGUILayout.LabelField("Assembly" + BuildTargets[i].Substring(15), durations[i]);
        EditorGUI.indentLevel -= 1;
    }

    private void LoadUniversalCompilerSettings()
    {
        var ucsLastWriteTime = GetFileLastWriteTime(UcsFilePath);
        if (_ucsLastWriteTime == ucsLastWriteTime)
            return;

        try
        {
            using (var fs = new FileStream(UcsFilePath, FileMode.Open, FileAccess.Read))
            {
                var xdoc = XDocument.Load(fs).Element("Settings");
                _ucs = new UniversalCompilerSettings
                {
                    Compiler = (CompilerType)Enum.Parse(typeof (CompilerType), xdoc.Element("Compiler").Value),
                };
                _ucsLastWriteTime = ucsLastWriteTime;
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception e)
        {
            Debug.LogWarning("LoadUniversalCompilerSettings:" + e);
        }
    }

    private void SaveUniversalCompilerSettings()
    {
        try
        {
            XElement xel = new XElement("Settings");
            try
            {
                using (var fs = new FileStream(UcsFilePath, FileMode.Open, FileAccess.Read))
                {
                    xel = XDocument.Load(fs, LoadOptions.PreserveWhitespace).Element("Settings");
                }
            }
            catch (Exception)
            {
            }

            SetXmlElementValue(xel, "Compiler", _ucs.Compiler.ToString());

            xel.Save(UcsFilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveUniversalCompilerSettings:" + e);
        }
    }

    private string GetUniversalCompilerVersion()
    {
        if (_ucVersion != null) {
            return _ucVersion;
        }

        var assemblyName = AssemblyName.GetAssemblyName("./Compiler/UniversalCompiler.exe");
        _ucVersion = assemblyName != null ? assemblyName.Version.ToString() : "";
        return _ucVersion;
    }

    private void ShowUniversalCompilerClientLog()
    {
        Process.Start(Path.GetFullPath(UcLogFilePath));
    }

    private string[] GetUniversalCompilerLastBuildLogs()
    {
        var icsLastWriteTime = GetFileLastWriteTime(UcLogFilePath);
        if (icsLastWriteTime == _icsLastWriteTime)
            return _ucLastBuildLog;

        _ucLastBuildLog = new[] {"", "", ""};
        if (!File.Exists(UcLogFilePath)) {
            return _ucLastBuildLog;
        }
        
        try
        {
            var lines = File.ReadAllLines(UcLogFilePath);

            var lastIdx = UcLogFilePath.Length;
            var elapsed = 0.0;
            foreach (var line in lines.Reverse())
            {
                if (line.StartsWith("Target assembly:"))
                {
                    // "Target assembly: Assembly-CSharp-Editor.dll";
                    var target = Path.GetFileNameWithoutExtension(line.Substring(17).Trim());
                    var idx = Array.FindIndex(BuildTargets, x => x == target);
                    if (idx != -1)
                    {
                        if (lastIdx <= idx)
                            break;
                        _ucLastBuildLog[idx] = elapsed.ToString("0.00") + " sec";
                        lastIdx = idx;
                    }
                    elapsed = 0;
                }
                else if (line.StartsWith("Elapsed time:"))
                {
                    // "Elapsed time: 0.82 sec";
                    double sec;
                    double.TryParse(line.Substring(13).Trim().Split()[0], out sec);
                    elapsed += sec;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("GetUniversalCompilerLastBuildLogs:" + e);
        }

        return _ucLastBuildLog;
    }

    private void OnGUI_IncrementalCompilerSettings()
    {
        GUILayout.Label("Incremental Compiler Settings", EditorStyles.boldLabel);

        LoadIncrementalCompilerSettings();
        IncrementalCompilerSettings ics;
        ics.DebugSymbolFile = (DebugSymbolFileType)EditorGUILayout.EnumPopup("DebugSymbolFile:", _ics.DebugSymbolFile);
        ics.PrebuiltOutputReuse = (PrebuiltOutputReuseType)EditorGUILayout.EnumPopup("PrebuiltOutputReuse:", _ics.PrebuiltOutputReuse);
        if (ics.Equals(_ics) == false)
        {
            _ics = ics;
            SaveIncrementalCompilerSettings();
        }
    }

    private void LoadIncrementalCompilerSettings()
    {
        var icsLastWriteTime = GetFileLastWriteTime(IcsFilePath);
        if (icsLastWriteTime == _icsLastWriteTime)
            return;

        try
        {
            using (var fs = new FileStream(IcsFilePath, FileMode.Open, FileAccess.Read))
            {
                var xdoc = XDocument.Load(fs).Element("Settings");
                _ics = new IncrementalCompilerSettings
                {
                    DebugSymbolFile = (DebugSymbolFileType)
                        Enum.Parse(typeof(DebugSymbolFileType), xdoc.Element("DebugSymbolFile").Value),
                    PrebuiltOutputReuse = (PrebuiltOutputReuseType)
                        Enum.Parse(typeof(PrebuiltOutputReuseType), xdoc.Element("PrebuiltOutputReuse").Value),
                };
                _icsLastWriteTime = icsLastWriteTime;
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception e)
        {
            Debug.LogWarning("LoadIncrementalCompilerSettings:" + e);
        }
    }

    private void SaveIncrementalCompilerSettings()
    {
        try
        {
            XElement xel = new XElement("Settings");
            try
            {
                using (var fs = new FileStream(IcsFilePath, FileMode.Open, FileAccess.Read))
                {
                    xel = XDocument.Load(fs, LoadOptions.PreserveWhitespace).Element("Settings");
                }
            }
            catch (Exception)
            {
            }

            SetXmlElementValue(xel, "DebugSymbolFile", _ics.DebugSymbolFile.ToString());
            SetXmlElementValue(xel, "PrebuiltOutputReuse", _ics.PrebuiltOutputReuse.ToString());

            xel.Save(IcsFilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveIncrementalCompilerSettings:" + e);
        }
    }

    private void OnGUI_IncrementalCompilerStatus()
    {
        GUILayout.Label("Incremental Compiler Status", EditorStyles.boldLabel);

        EditorGUILayout.TextField("Version", GetIncrementalCompilerVersion());

        var icsProcess = GetIncrementalCompilerProcess();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Server");
        if (icsProcess != null)
        {
            GUILayout.TextField("Running");
            if (GUILayout.Button("Kill"))
                icsProcess.Kill();
        }
        else
        {
            GUILayout.TextField("Stopped");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Log");
        if (GUILayout.Button("Client"))
            ShowIncrementalCompilerClientLog();
        if (GUILayout.Button("Server"))
            ShowIncrementalCompilerServerLog();
        EditorGUILayout.EndHorizontal();
    }

    private string GetIncrementalCompilerVersion()
    {
        var assemblyName = AssemblyName.GetAssemblyName("./Compiler/IncrementalCompiler.exe");
        return assemblyName != null ? assemblyName.Version.ToString() : "";
    }

    private Process GetIncrementalCompilerProcess()
    {
        if (_icProcess != null && _icProcess.HasExited == false)
            return _icProcess;

        _icProcess = null;
        try
        {
            var processes = Process.GetProcessesByName("IncrementalCompiler");
            var dir = Directory.GetCurrentDirectory();
            foreach (var process in processes)
            {
                if (process.MainModule.FileName.StartsWith(dir))
                {
                    _icProcess = process;
                    return _icProcess;
                }
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void ShowIncrementalCompilerClientLog()
    {
        Process.Start(Path.GetFullPath(@"./Temp/IncrementalCompiler.log"));
    }

    private void ShowIncrementalCompilerServerLog()
    {
        Process.Start(Path.GetFullPath(@"./Temp/IncrementalCompiler-Server.log"));
    }

    // workaround for Xelement.SetElementValue bug at Unity3D
    // http://stackoverflow.com/questions/26429930/xelement-setelementvalue-overwrites-elements
    private void SetXmlElementValue(XElement xel, XName name, string value)
    {
        var element = xel.Element(name);
        if (element != null)
            element.Value = value;
        else
            xel.Add(new XElement(name, value));
    }

    private DateTime GetFileLastWriteTime(string path)
    {
        try
        {
            var fi = new FileInfo(IcsFilePath);
            return fi.LastWriteTimeUtc;
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
    }
}
