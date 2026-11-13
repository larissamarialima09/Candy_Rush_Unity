using System;
using System.Linq;
using CandyRush.Integrated;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RabbitKartValidation
{
    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    [MenuItem("CandyRush/Validate Rabbit Models")]
    public static void Run()
    {
        var world = new GameObject("Rabbit validation");
        world.transform.localScale = new Vector3(-1, 1, 1);
        try
        {
            foreach (string color in RacerProfile.RabbitSprites)
            {
                var kart = new Kart();
                var view = new KartView(kart, world.transform, KartPalette.Rival(color));
                view.Update(1);
                var renderers = view.Root.GetComponentsInChildren<Renderer>().Where(r => r.name != "Fur shells").ToArray();
                foreach (var required in new[] { "Kart_Body", "Rabbit_Body", "Head", "Ear_L", "Ear_R", "Arm_L", "Arm_R" })
                    Check(view.Root.GetComponentsInChildren<Transform>().Any(t => t.name == required), color + ": parte ausente: " + required);
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                        Check(material != null && material.shader != null && material.shader.isSupported,
                            color + ": material inválido em " + renderer.name);
                for (int i = 0; i < kart.Wheels.Length; i++)
                {
                    var wheel = kart.Wheels[i];
                    var pivot = view.Root.Find("Suspension_" + wheel.Spec.Name);
                    Check(pivot != null, color + ": suspensão ausente");
                    var bounds = pivot.GetComponentInChildren<Renderer>().bounds;
                    Check(Mathf.Abs(bounds.size.y - wheel.Spec.Radius * 2) < .001f,
                        color + ": raio incorreto " + wheel.Spec.Name + " = " + bounds.size.y);
                    wheel.SpinAngle = 1.4f;
                    wheel.SteerAngle = wheel.Spec.Steered ? .2f : 0;
                }
                var ear = view.Root.GetComponentsInChildren<Transform>().First(t => t.name == "Ear_L");
                var rest = ear.localRotation;
                for (int frame = 0; frame < 60; frame++)
                {
                    kart.Telemetry.ForwardSpeed = frame / 6f;
                    kart.Telemetry.SteerAngleDeg = 20;
                    view.Update(1, 1f / 60);
                }
                Check(Quaternion.Angle(rest, ear.localRotation) > 1, color + ": orelhas sem animação");
                Check(Mathf.Abs(view.Root.Find("Suspension_FL").localRotation.x) > .1f, color + ": roda parada");
                var paused = ear.localRotation;
                view.Update(1, 0);
                Check(Quaternion.Angle(paused, ear.localRotation) < .01f, color + ": pausa não respeitada");
                view.SetVisible(false);
                Check(!view.Root.gameObject.activeSelf, color + ": visibilidade incorreta");
                view.SetVisible(true);
                view.Root.localPosition = new Vector3(Array.IndexOf(RacerProfile.RabbitSprites, color) * 3 - 4.5f, .65f, 0);
                view.Root.localRotation = Quaternion.Euler(0, -20, 0);
                Debug.Log("RABBIT_OK " + color + ": model, materials, tire radii, suspension, animation, pause");
            }
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.transform.SetParent(world.transform, false);
            camera.transform.position = new Vector3(0, 5, 11);
            camera.transform.LookAt(new Vector3(0, 1, 0));
            camera.backgroundColor = new Color(.65f, .78f, .9f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographic = true;
            camera.orthographicSize = 3.7f;
            var light = new GameObject("Preview Light").AddComponent<Light>();
            light.transform.SetParent(world.transform, false);
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(40, -30, 0);
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            var target = new RenderTexture(1400, 700, 24);
            var texture = new Texture2D(1400, 700, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1400, 700), 0, 0);
                texture.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath("../character-preview.png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
            Debug.Log("RABBIT_VALIDATION_OK");
        }
        finally { UnityEngine.Object.DestroyImmediate(world); }
    }
}
