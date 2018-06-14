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

Target "Package" (fun _ -> 
    // pack IncrementalCompiler.exe with dependent module dlls to packed one
    let ilrepackExe = (getNugetPackage "ILRepack" "2.0.13") @@ "tools" @@ "ILRepack.exe"
    Shell.Exec(ilrepackExe,
               "/wildcards /out:IncrementalCompiler.packed.exe IncrementalCompiler.exe *.dll",
               "./core/IncrementalCompiler/bin/Release") |> ignore
    // fix roslyn compiler to work well with UnityVS
    Shell.Exec("./core/RoslynCompilerFix/bin/Release/RoslynCompilerFix.exe",
               "IncrementalCompiler.packed.exe IncrementalCompiler.packed.fixed.exe",
               "./core/IncrementalCompiler/bin/Release") |> ignore
    // code signing
    if hasBuildParam "sign" then (
        let certFile = getBuildParam "sign"
        let timeUrl = "http://timestamp.verisign.com/scripts/timstamp.dll"
        let exeFile = "./core/IncrementalCompiler/bin/Release/IncrementalCompiler.packed.fixed.exe"
        Shell.Exec(@"C:\Program Files\Microsoft SDKs\Windows\v7.1\Bin\signtool.exe",
                   sprintf "sign /v /f %s /t %s %s" certFile timeUrl exeFile,
                   ".") |> ignore
    )
    // let's make package
    for target in ["Unity4"; "Unity5"] do
        let targetDir = binDir @@ target
        let editorDir = targetDir @@ "Assets" @@ "CSharp vNext Support" @@ "Editor"
        let pluginsDir = targetDir @@ "Assets" @@ "Plugins" @@ "Incremental Compiler"
        let compilerDir = targetDir @@ "Compiler"
        // create dirs
        CreateDir targetDir
        CreateDir editorDir
        CreateDir compilerDir
        CreateDir pluginsDir
        // copy output files
        "./core/IncrementalCompiler/bin/Release/IncrementalCompiler.packed.fixed.exe" |> CopyFile (compilerDir @@ "IncrementalCompiler.exe")
        "./core/IncrementalCompiler/IncrementalCompiler.xml" |> CopyFile compilerDir
        "./core/UnityPackage/Assets/Editor/CompilerSettings.cs" |> CopyFile editorDir
        "./GenerationAttributes/bin/Release/GenerationAttributes.dll" |> CopyFile pluginsDir
        "./GenerationAttributes/bin/Release/GenerationAttributes.xml" |> CopyFile pluginsDir
        "./extra/CompilerPlugin." + target + "/bin/Release/Unity.PureCSharpTests.dll" |> CopyFile (editorDir @@ "CSharpVNextSupport.dll")
        "./extra/UniversalCompiler/bin/Release/UniversalCompiler.exe" |> CopyFile compilerDir
        "./extra/UniversalCompiler/UniversalCompiler.xml" |> CopyFile compilerDir
        "./tools/pdb2mdb/pdb2mdb.exe" |> CopyFile compilerDir
        // create zipped packages
        !! (targetDir @@ "**") |> Zip targetDir (binDir @@ "IncrementalCompiler." + target + ".zip")
)

Target "Help" <| fun _ -> 
    showUsage solution (fun name -> 
        if name = "package" then Some("Build package", "sign")
        else None)

"Clean"
  ==> "Restore"
  ==> "Build"
  ==> "Package"

RunTargetOrDefault "Help"
