using System;
using System.IO;
using System.Linq;
using CandyRush.Integrated;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public static class CandyVisualValidation
{
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    [MenuItem("CandyRush/Visuals/Validate visual upgrade")]
    public static void Run()
    {
        CandyVisualAssets.EnsureAssets();
        var world = new GameObject("Visual validation");
        world.transform.localScale = new Vector3(-1, 1, 1);
        int previousQuality = QualitySettings.GetQualityLevel();
        try
        {
            foreach (string name in new[] { "FurColor", "FurNormal", "FurNoise", "TireNormal" })
                Check(Resources.Load<Texture2D>("Candy/Visuals/" + name) != null, "Missing texture " + name);
            int index = 0;
            foreach (string color in RacerProfile.RabbitSprites)
            {
                var kart = new Kart();
                kart.Telemetry.Respawned = false;
                var view = new KartView(kart, world.transform, KartPalette.Rival(color));
                var fur = view.Root.GetComponentInChildren<FurController>();
                var visuals = view.Root.GetComponentInChildren<KartVisuals>();
                Check(fur != null && fur.regions.Length > 0 && visuals != null, color + ": missing visual controllers");
                fur.mode = FurController.FurMode.Lightweight; fur.Refresh();
                Check(!fur.ShellActive && fur.ShellObjectCount == 0, "Lightweight mode generated shells");
                ValidateImportedColors(view, color);
                foreach (var region in fur.regions)
                {
                    Check(region.renderer.sharedMaterials[region.materialSlot].shader.name == "CandyRush/FurLite", "FurLite not assigned");
                    Check(region.renderer.name != "Kart_Body" && !region.renderer.name.StartsWith("Wheel_"), "Fur assigned outside rabbit");
                }
                fur.mode = FurController.FurMode.Automatic;
                QualitySettings.SetQualityLevel(0); fur.Refresh();
                Check(!fur.ShellActive, "Low quality selected shell fur");
                QualitySettings.SetQualityLevel(5); fur.Refresh();
                Check(fur.ShellActive == !Application.isMobilePlatform, "Automatic quality selection failed");
                fur.mode = FurController.FurMode.ShellFur;
                foreach (int layers in new[] { 8, 16 })
                {
                    fur.layerCount = layers; fur.Refresh();
                    Check(fur.ShellActive && fur.ShellObjectCount > 0, "No shell meshes");
                    ValidateImportedColors(view, color);
                    foreach (var filter in fur.GetComponentsInChildren<MeshFilter>().Where(f => f.name == "Fur shells"))
                    {
                        var values = new System.Collections.Generic.List<Vector2>();
                        filter.sharedMesh.GetUVs(2, values);
                        Check(values.Count > 0 && values.Max(v => v.x) == layers, "Incorrect layer count");
                        Check(filter.sharedMesh.bounds.extents.sqrMagnitude > 0, "Empty fur bounds");
                    }
                }
                kart.Telemetry.ForwardSpeed = 10;
                kart.Telemetry.SteerAngleDeg = 20;
                kart.Telemetry.WheelsOnGround = 4;
                for (int i = 0; i < kart.Wheels.Length; i++)
                { kart.Wheels[i].SpinAngle = 1.4f; kart.Wheels[i].SteerAngle = kart.Wheels[i].Spec.Steered ? .2f : 0; }
                for (int frame = 0; frame < 60; frame++) view.Update(1, 1f / 60);
                Check(Quaternion.Angle(visuals.bodyRoot.localRotation, Quaternion.identity) > .5f, "Body tilt did not respond");
                Check(Mathf.Abs(view.Root.Find("Suspension_FL").localRotation.x) > .1f, "Wheel spin missing");
                var pausedRotation = visuals.bodyRoot.localRotation;
                view.Update(1, 0);
                Check(Quaternion.Angle(pausedRotation, visuals.bodyRoot.localRotation) < .001f, "Pause moved body");
                visuals.ResetMotion();
                float restY = visuals.bodyRoot.localPosition.y;
                visuals.Animate(10, 0, 0, false, -5, 1f / 60);
                visuals.Animate(10, 0, .1f, true, 0, 1f / 60);
                float travel = Mathf.Abs(visuals.bodyRoot.localPosition.y - restY);
                Check(travel > .0001f && travel <= visuals.suspensionTravel, "Landing suspension failed");
                Check(float.IsFinite(visuals.bodyRoot.localPosition.y), "Suspension produced NaN");
                view.Root.localPosition = new Vector3(index++ * 3 - 4.5f, .65f, 0);
                view.Root.localRotation = Quaternion.Euler(0, -20, 0);
                Debug.Log("VISUAL_CHARACTER_OK " + color);
            }
            var post = world.AddComponent<CandyPostProcessing>();
            // Execute Awake explicitly in edit-mode validation.
            if (post.Volume == null) post.SendMessage("Awake");
            Check(post.Volume != null && post.Volume.isGlobal && post.Volume.sharedProfile != null, "Global volume missing");
            var camera = new GameObject("Visual preview camera").AddComponent<Camera>();
            camera.transform.SetParent(world.transform, false);
            camera.transform.position = new Vector3(0, 4, 10);
            camera.transform.LookAt(new Vector3(0, 1, 0));
            camera.backgroundColor = new Color(.58f, .72f, .85f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.orthographic = true; camera.orthographicSize = 3.9f;
            CandyPostProcessing.ConfigureCamera(camera);
            Check(camera.GetComponent<PostProcessLayer>().volumeLayer == 1 && camera.allowHDR, "Camera PP settings missing");
            var light = new GameObject("Visual preview light").AddComponent<Light>();
            light.transform.SetParent(world.transform, false); light.type = LightType.Directional;
            light.intensity = 1.3f; light.transform.rotation = Quaternion.Euler(40, -30, 0);
            RenderSettings.ambientLight = Color.gray;
            var target = new RenderTexture(1600, 800, 24, RenderTextureFormat.ARGBHalf);
            var texture = new Texture2D(1600, 800, TextureFormat.RGB24, false);
            var previousTarget = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1600, 800), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.GetFullPath("../visual-upgrade-preview.png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previousTarget;
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            }
            foreach (string shaderName in new[] { "CandyRush/ShellFur", "CandyRush/FurLite" })
            {
                var shader = Shader.Find(shaderName);
                Check(shader != null && shader.isSupported, "Unsupported shader " + shaderName);
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    Check(message.severity.ToString() != "Error", shaderName + ": " + message.message);
            }
            Debug.Log("VISUAL_UPGRADE_VALIDATION_OK");
        }
        finally
        {
            QualitySettings.SetQualityLevel(previousQuality);
            UnityEngine.Object.DestroyImmediate(world);
        }
    }

    static void ValidateImportedColors(KartView view, string sprite)
    {
        int index = Array.IndexOf(RacerProfile.RabbitSprites, sprite);
        var prefab = Resources.Load<GameObject>("Candy/Characters/RabbitKart_" + RacerProfile.RabbitNames[index]);
        var imported = prefab.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials).Where(m => m != null)
            .GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());
        int checkedMaterials = 0;
        foreach (var material in view.Root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials))
        {
            if (material == null || !material.HasProperty("_Color")) continue;
            var source = imported.Values.FirstOrDefault(m => material.name.StartsWith(m.name + " ", StringComparison.Ordinal));
            if (source == null) continue; // Glow materials are unrelated to the imported model.
            // Read glTFast's actual property independently from the conversion helper.
            Check(source.HasProperty("baseColorFactor"), "Expected glTFast baseColorFactor on " + source.name);
            Color expected = source.GetColor("baseColorFactor"), actual = material.GetColor("_Color");
            var palette = KartPalette.Rival(sprite);
            if (source.name.Contains("_Fur")) expected = palette.FurColor;
            else if (source.name.StartsWith("Belly", StringComparison.Ordinal)) expected = palette.FurMarkingsColor;
            Check(Mathf.Abs(expected.r - actual.r) < .001f && Mathf.Abs(expected.g - actual.g) < .001f
                && Mathf.Abs(expected.b - actual.b) < .001f && Mathf.Abs(expected.a - actual.a) < .001f,
                sprite + ": imported color lost on " + material.name + "; expected " + expected + ", got " + actual);
            checkedMaterials++;
        }
        Check(checkedMaterials > 0, sprite + ": no converted material colors checked");
    }
}
