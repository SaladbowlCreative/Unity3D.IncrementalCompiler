[![Build status](https://ci.appveyor.com/api/projects/status/iaofuixbjakmwl4q?svg=true)](https://ci.appveyor.com/project/veblush/unity3d-incrementalcompiler)

# Unity3D.IncrementalCompiler

Unity3D Incremental C# Compiler using [Roslyn](https://github.com/dotnet/roslyn).
- You can get faster compilation speed. Because it works as an
  [incremental compiler](https://en.wikipedia.org/wiki/Incremental_compiler)
- You can use C# 5 and 6 features.

This project is at an early development stage.
And still now it can support only windows platform.

### Setup

Unzip [a release zip file](https://github.com/SaladLab/Unity3D.IncrementalCompiler/releases)
to your unity project top-most directory.
(Unblocking zip file before decompressing zip file might be required to
avoid TypeLoadException of Unity-Mono.)

- When Unity 4.x, use IncrementalCompiler.Unity4.zip
- When Unity 5.x, use IncrementalCompiler.Unity5.zip

A release zip has a plugin dll which should be put at Editor folder and
a few compiler related files which should be put at Compiler folder.
Following before and after screenshot will helps for understanding install location.

![InstallScreenshot](https://raw.githubusercontent.com/SaladLab/Unity3D.IncrementalCompiler/master/docs/Install.png)

While an incremental compiler build your scripts, it saves log files to project/Temp
directory. With these log files, you can see how it works.

![LogFiles](https://raw.githubusercontent.com/SaladLab/Unity3D.IncrementalCompiler/master/docs/LogFiles.png)

### Configuration

You can configure how this compiler work by settings window.
Click "Assets/Open C# Compiler Settings..."  in UnityEditor.

![SettingsMenu](https://raw.githubusercontent.com/SaladLab/Unity3D.IncrementalCompiler/master/docs/SettingsMenu.png)

Not only settings but you can also check detailed log and information.

![SettingsWindow](https://raw.githubusercontent.com/SaladLab/Unity3D.IncrementalCompiler/master/docs/SettingsWindow.png)

Also you can manually modify a configure file which is located at project/Compiler/[IncrementalCompiler.xml](https://github.com/SaladLab/Unity3D.IncrementalCompiler/blob/master/core/IncrementalCompiler/IncrementalCompiler.xml)

### More information

- [Benchmark](./docs/Benchmark.md)
- [Under the hood](./docs/UnderTheHood.md)

### FAQ

- When incremental compiler is installed, Unity claims something wrong like:
```
TypeLoadException: Could not load type 'UnityEditor.Modules.CSharpCompiler' from assembly 'UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.
UnityEditor.Scripting.ScriptCompilers.CreateCompilerInstance (MonoIsland island, Boolean buildingForEditor, BuildTarget targetPlatform, Boolean runUpdater) (at C:/buildslave/unity/build/Editor/Mono/Scripting/ScriptCompilers.cs:93)
```
  This problem is usually caused by windows file blocking system.
  You need to unblock zip file before uncompressing this file.

### Related works

- An integration part between unity3d and incremental compiler is based on
  [alexzzzz](https://bitbucket.org/alexzzzz/unity-c-5.0-and-6.0-integration/src)'s works.
- Mono MDB writer for roslyn is based on
  [DavidKarlas](https://github.com/DavidKarlas)'s
  [work](https://github.com/mono/roslyn/pull/4).
