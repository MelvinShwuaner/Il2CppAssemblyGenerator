using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class AndroidBuildProcessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 0;
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string outputDir  = Path.GetDirectoryName(path);
        string stubsDir   = Path.Combine(outputDir, "Il2CppAssemblies");
        try
        {
            RunGenerator(path);
            if (!Directory.Exists(stubsDir))
            {
                UnityEngine.Debug.LogError("Il2CppAssemblies folder was not generated");
                return;
            }
            // Copy to StreamingAssets inside APK
            string zipPath = Path.Combine(path, "src/main/assets/Il2CppAssemblies.zip");
            File.Delete(zipPath);
            ZipFile.CreateFromDirectory(stubsDir, zipPath);
            
            Directory.Delete(stubsDir, true);

            UnityEngine.Debug.Log("Il2CppAssemblies injected into APK");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to generate Il2CppAssemblies: {e}");
        }
    }
    static void RunGenerator(string path)
    {
        string Path = System.IO.Path.Combine(Application.dataPath, "Editor");
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            Path = System.IO.Path.Combine(Path, "Il2CppAssemblyGenerator.exe");
        }
        else 
        {
            Path = System.IO.Path.Combine(Path, "Il2CppAssemblyGenerator");
        }
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = Path, 
            Arguments = $"gradle {path}", 
            UseShellExecute = false,
            RedirectStandardOutput = true, 
            RedirectStandardError = true
        };
        using Process process = Process.Start(startInfo);
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        Debug.Log("Il2CppGenerator output: " + output);
        if(!string.IsNullOrEmpty(error))
            Debug.LogError("Il2CppGenerator error: " + error);
        // 4. Wait for the application to close
        process.WaitForExit();
    }
}
public class IOSBuildProcessor
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }
        
        var projPath = PBXProject.GetPBXProjectPath(path);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string targetGuid = proj.GetUnityMainTargetGuid();

        proj.AddShellScriptBuildPhase(
            targetGuid,
            "Run Generator",
            "/bin/sh",
            @"
set -e

APP_PATH=$TARGET_BUILD_DIR/$FULL_PRODUCT_NAME
APP_PARENT=$(dirname ""$APP_PATH"")

GENERATOR=$SRCROOT/../Assets/Editor/Il2CppAssemblyGenerator

echo ""Running generator with APP_PATH: $APP_PATH""
""$GENERATOR"" app ""$APP_PATH""

ASSEMBLIES_DIR=$APP_PARENT/Il2CppAssemblies

if [ ! -d ""$ASSEMBLIES_DIR"" ]; then
    echo ""ERROR: missing Il2CppAssemblies at $ASSEMBLIES_DIR""
    ls -la $APP_PARENT
    exit 1
fi

cd $APP_PARENT
rm -f Il2CppAssemblies.zip
zip -r Il2CppAssemblies.zip Il2CppAssemblies

mkdir -p ""$APP_PATH/Data/Raw""
cp Il2CppAssemblies.zip ""$APP_PATH/Data/Raw/""

echo ""Done""
"
        );

        File.WriteAllText(projPath, proj.WriteToString());
    }
}
