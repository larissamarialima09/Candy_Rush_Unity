using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Gera a APK de teste para Android, com os controles de toque.
/// Pelo menu: Candy Rush > Gerar APK Android.
/// Pela linha de comando: Unity -batchmode -quit -projectPath &lt;pasta&gt; -executeMethod AndroidBuild.BuildApk
/// </summary>
public static class AndroidBuild
{
    const string PackageName = "com.candyrush.game";
    const string OutputFile = "Builds/CandyRush-teste.apk";
    static bool exportForLowMemoryApk;

    public static void ExportForApk()
    {
        MobileControlsValidation.Run();
        exportForLowMemoryApk = true;
        try { BuildApk(); }
        finally
        {
            exportForLowMemoryApk = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        }
    }

    // Ferramentas Android fora do Program Files, para o Unity não precisar de administrador.
    const string ToolsRoot = @"C:\Users\laris\AppData\Local\UnityAndroidTools";

    [MenuItem("Candy Rush/Gerar APK Android")]
    public static void BuildApk()
    {
        AndroidExternalToolsSettings.jdkRootPath = ToolsRoot + @"\jdk-17.0.20.1+1";
        AndroidExternalToolsSettings.ndkRootPath = ToolsRoot + @"\android-ndk-r27c";
        AndroidExternalToolsSettings.sdkRootPath = ToolsRoot + @"\sdk";
        Environment.SetEnvironmentVariable("JAVA_HOME", AndroidExternalToolsSettings.jdkRootPath);
        Environment.SetEnvironmentVariable("ANDROID_HOME", AndroidExternalToolsSettings.sdkRootPath);
        Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", AndroidExternalToolsSettings.sdkRootPath);

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageName);
        PlayerSettings.bundleVersion = "1.5";
        PlayerSettings.Android.bundleVersionCode = 6;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = exportForLowMemoryApk;

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string projectFolder = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(projectFolder, exportForLowMemoryApk ? "Builds/AndroidProject" : OutputFile);
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

        if (exportForLowMemoryApk && report.summary.result == BuildResult.Succeeded)
        {
            string gradlePath = Path.Combine(output, "unityLibrary/build.gradle");
            string gradle = File.ReadAllText(gradlePath);
            gradle = gradle.Replace("commandLineArgs.add(\"--compile-cpp\")",
                "commandLineArgs.add(\"--compile-cpp\")\n    commandLineArgs.add(\"--jobs=1\")\n    commandLineArgs.add(\"--bee-jobs=1\")");
            gradle = gradle.Replace("commandLineArgs.add(\"--jobs=1 --bee-jobs=1\")", "");
            File.WriteAllText(gradlePath, gradle);
        }

        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"Build Android falhou: {report.summary.result}. Veja o console.");

        Debug.Log($"{(exportForLowMemoryApk ? "Projeto Android exportado" : "APK gerada")} em {output} ({report.summary.totalSize / 1048576} MB)");
    }
}
