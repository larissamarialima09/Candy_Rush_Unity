using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Executa um pedido local de build na instância do editor já aberta.</summary>
[InitializeOnLoad]
public static class ApkBuildRequest
{
    const string Request = "Temp/CandyRushApk.request";
    const string Status = "../apk-build-status.txt";
    static bool running;

    static ApkBuildRequest() => EditorApplication.update += Check;

    static void Check()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            return;
        }
        running = true;
        File.Delete(Request);
        File.WriteAllText(Status, "BUILDING");
        EditorApplication.delayCall += () =>
        {
            try
            {
                AndroidBuild.ExportForApk();
                File.WriteAllText(Status, "EXPORTED");
            }
            catch (Exception error)
            {
                File.WriteAllText(Status, "FAILED\n" + error);
                Debug.LogException(error);
            }
            finally { running = false; }
        };
    }
}
