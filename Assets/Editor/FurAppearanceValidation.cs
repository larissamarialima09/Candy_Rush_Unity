using System;
using System.IO;
using System.Linq;
using CandyRush.Integrated;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Runs in the existing editor, one rabbit at a time, without entering Play or opening the race.</summary>
[InitializeOnLoad]
public static class FurAppearanceValidation
{
    const string RequestPath = "Temp/CandyRushFurAppearance.request";
    const string StatusPath = "Temp/CandyRushFurAppearance.status.txt";
    static string lastStatus;
    static FurAppearanceValidation() { EditorApplication.update += CheckRequest; }

    static void CheckRequest()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SetStatus("STOPPING_PLAY_MODE_FOR_VALIDATION");
            EditorApplication.isPlaying = false;
            return;
        }
        File.Delete(RequestPath);
        try { Run(); SetStatus("OK: four portrait palettes; lightweight, 8/16 shells; shader rendering and compilation."); }
        catch (Exception error) { SetStatus("FAILED: " + error); Debug.LogException(error); }
    }

    static void SetStatus(string status)
    {
        if (lastStatus == status) return;
        Directory.CreateDirectory("Temp");
        File.WriteAllText(StatusPath, status); lastStatus = status;
    }

    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    [MenuItem("CandyRush/Visuals/Validate rabbit fur colors")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play before validating fur.");
        // Explicit expected colors guard against every rabbit accidentally receiving the same coat.
        string[] sprites = { "blue", "pink", "yellow", "purple" };
        Color32[] coats = { new Color32(241,242,244,255), new Color32(198,157,114,255),
            new Color32(118,82,57,255), new Color32(184,180,191,255) };
        var preview = EditorSceneManager.NewPreviewScene();
        var previousTarget = RenderTexture.active;
        var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
        Camera camera = null;
        try
        {
            var cameraObject = new GameObject("Fur validation camera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            camera = cameraObject.AddComponent<Camera>();
            camera.scene = preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.25f, .25f, .25f);
            camera.transform.position = new Vector3(0, 2, 4);
            camera.transform.LookAt(new Vector3(0, 1, 0));
            camera.targetTexture = target;
            var sunObject = new GameObject("Fur validation sun");
            SceneManager.MoveGameObjectToScene(sunObject, preview);
            var sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional;
            sun.shadows = LightShadows.Hard; sun.transform.rotation = Quaternion.Euler(40, -20, 0);
            var pointObject = new GameObject("Fur validation fill");
            SceneManager.MoveGameObjectToScene(pointObject, preview);
            var point = pointObject.AddComponent<Light>(); point.type = LightType.Point;
            pointObject.transform.position = new Vector3(1, 2, 1); point.range = 8;
            for (int i = 0; i < sprites.Length; i++)
            {
                var world = new GameObject("Fur validation " + sprites[i]);
                SceneManager.MoveGameObjectToScene(world, preview);
                world.transform.localScale = new Vector3(-1, 1, 1);
                try
                {
                    var kart = new Kart();
                    var view = new KartView(kart, world.transform, KartPalette.Rival(sprites[i]));
                    view.Update(1, 0);
                    var fur = view.Root.GetComponentInChildren<FurController>();
                    Check(fur != null && fur.useCharacterPalette && fur.regions.Length > 0, "Missing portrait coat " + sprites[i]);
                    foreach (var mode in new[] { FurController.FurMode.Lightweight, FurController.FurMode.ShellFur })
                    {
                        fur.mode = mode;
                        foreach (int layers in mode == FurController.FurMode.ShellFur ? new[] { 8, 16 } : new[] { 8 })
                        {
                            fur.layerCount = layers; fur.Refresh();
                            Check(fur.ShellActive == (mode == FurController.FurMode.ShellFur), "Shell unavailable: " + sprites[i]);
                            foreach (var region in fur.regions)
                            {
                                var material = region.renderer.sharedMaterials[region.materialSlot];
                                bool markings = material.name.StartsWith("Belly", StringComparison.Ordinal);
                                Color actual = material.GetColor("_Color");
                                Color expected = markings ? KartPalette.Rival(sprites[i]).FurMarkingsColor : (Color)coats[i];
                                Check((new Vector4(actual.r,actual.g,actual.b,actual.a)
                                    - new Vector4(expected.r,expected.g,expected.b,expected.a)).sqrMagnitude < .00001f,
                                    sprites[i] + " coat color mismatch on " + material.name);
                            }
                            foreach (var renderer in fur.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Fur shells"))
                            {
                                renderer.shadowCastingMode = ShadowCastingMode.On;
                                foreach (var material in renderer.sharedMaterials)
                                {
                                    Check(material.GetColor("_Color").maxColorComponent <= 1, "HDR fur color");
                                    Check(material.GetFloat("_RimStrength") <= .25f, "Excessive fur emission");
                                }
                            }
                            camera.Render(); // Exercise the real Forward/ShadowCaster shader passes on this GPU.
                            foreach (string shaderName in new[] { "CandyRush/ShellFur", "CandyRush/FurLite" })
                            {
                                var shader = Shader.Find(shaderName);
                                Check(shader != null && shader.isSupported, "Unsupported " + shaderName);
                                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                                    Check(message.severity.ToString() != "Error", shaderName + ": " + message.message);
                            }
                        }
                    }
                    Debug.Log("FUR_APPEARANCE_OK " + sprites[i]);
                }
                finally { UnityEngine.Object.DestroyImmediate(world); }
            }
            Debug.Log("FUR_APPEARANCE_VALIDATION_OK");
        }
        finally
        {
            if (camera != null) camera.targetTexture = null;
            RenderTexture.active = previousTarget;
            UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
