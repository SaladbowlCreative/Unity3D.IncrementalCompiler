#I @"packages/FAKE/tools"
#I @"packages/FAKE.BuildLib/lib/net451"
#r "FakeLib.dll"
#r "BuildLib.dll"

open Fake
open BuildLib

let solution = 
    initSolution
        "./IncrementalCompiler.sln" "Release" 
        [ ]

Target "Clean" <| fun _ -> cleanBin

Target "Restore" <| fun _ -> restoreNugetPackages solution

Target "Build" <| fun _ -> buildSolution solution

Target "Export" (fun _ -> 
    let target = "UnityCompiler";
    let targetDir = binDir @@ target
    let compilerDir = targetDir @@ "Compiler"
    let editorDir = targetDir @@ "Assets" @@ "CSharp vNext Support" @@ "Editor"
    let pluginsDir = targetDir @@ "Assets" @@ "Plugins" @@ "Incremental Compiler"
    CreateDir targetDir
    CreateDir compilerDir
    CreateDir editorDir
    CreateDir pluginsDir

    "./GenerationAttributes/bin/Release/GenerationAttributes.dll" |> CopyFile pluginsDir
    "./GenerationAttributes/bin/Release/GenerationAttributes.xml" |> CopyFile pluginsDir
    "./core/UnityPackage/Assets/Editor/CompilerSettings.cs" |> CopyFile editorDir
    "./tools/0Harmony.dll" |> CopyFile editorDir
    "./core/IncrementalCompiler/IncrementalCompiler.xml" |> CopyFile compilerDir
    "./extra/CompilerPlugin.Unity5/bin/Release/Unity.PureCSharpTests.dll" |> CopyFile (editorDir @@ "CSharpVNextSupport.dll")
    "./extra/UniversalCompiler/bin/Release/UniversalCompiler.exe" |> CopyFile compilerDir
    "./extra/UniversalCompiler/UniversalCompiler.xml" |> CopyFile compilerDir
    "./tools/pdb2mdb/pdb2mdb.exe" |> CopyFile compilerDir

    let dir = new System.IO.DirectoryInfo("./core/IncrementalCompiler/bin/Release/")
    filesInDir dir |> Array.iter (fun f -> f.FullName |> CopyFile compilerDir)
)

Target "Help" <| fun _ -> 
    showUsage solution (fun name -> 
        if name = "package" then Some("Build package", "sign")
        else None)

"Clean"
  ==> "Restore"
  ==> "Build"

"Build" ==> "Export"

RunTargetOrDefault "Help"
