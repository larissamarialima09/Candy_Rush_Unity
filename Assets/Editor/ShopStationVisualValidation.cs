using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CandyRush.Integrated;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ShopStationVisualValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    [MenuItem("CandyRush/Visuals/Validate shop and station")]
    public static void Run()
    {
        CheckPreview();
        CheckStation();
        FuelGameplayValidation.Run();
        Debug.Log("SHOP_STATION_VISUAL_VALIDATION_OK: preview, horizontal fuel, station routes and fuel gameplay.");
    }

    public static void RunBatch()
    {
        try
        {
            Run();
            File.WriteAllText("shop-station-status.txt", "OK: preview, horizontal fuel, station routes and fuel gameplay.");
        }
        catch (Exception error)
        {
            File.WriteAllText("shop-station-status.txt", "FAILED: " + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static void CheckPreview()
    {
        using (var stage = new ShopPreviewStage())
        {
            stage.Ativar(true);
            var type = typeof(ShopPreviewStage);
            var root = (Transform)type.GetField("raiz", Private).GetValue(stage);
            var pivot = (Transform)type.GetField("pivo", Private).GetValue(stage);
            var camera = (Camera)type.GetField("camera", Private).GetValue(stage);
            // Reproduz um pivô importado deslocado no palco situado em y=-800.
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(pivot, false);
            model.transform.localPosition = new Vector3(2, 3, -1);
            model.transform.localScale = new Vector3(2.4f, 3.2f, 3.8f);
            type.GetMethod("Assentar", Private).Invoke(stage, new object[] { model.transform });
            var bounds = model.GetComponent<Renderer>().bounds;
            var center = root.InverseTransformPoint(bounds.center);
            Check(Mathf.Abs(center.x) < .001f && Mathf.Abs(center.z) < .001f, "Modelo fora do centro do pódio.");
            Check(Mathf.Abs(root.InverseTransformPoint(bounds.min).y) < .001f, "Modelo fora da base do pódio.");
            CheckFraming(model.transform, pivot, camera);
            UnityEngine.Object.DestroyImmediate(model);

            for (int rabbit = 0; rabbit < 4; rabbit++)
            {
                stage.Montar(rabbit, new List<ShopItem>());
                var actual = (Transform)type.GetField("modelo", Private).GetValue(stage);
                Check(actual != null, "Prévia ausente para o coelho " + rabbit);
                CheckFraming(actual, pivot, camera);
            }
        }
    }

    static void CheckFraming(Transform model, Transform pivot, Camera camera)
    {
        foreach (int angle in new[] { 0, 90, 180, 270 })
        {
            pivot.localRotation = Quaternion.Euler(0, angle, 0);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var bounds = renderer.bounds;
                foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    var point = camera.WorldToViewportPoint(corner);
                    Check(point.z > camera.nearClipPlane && point.z < camera.farClipPlane
                        && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1,
                        "Prévia cortada em " + angle + " graus: " + point);
                }
            }
        }
    }

    static void CheckStation()
    {
        var path = new CircuitPath(CircuitConfig.Centerline, CircuitConfig.Samples, CircuitConfig.CurvatureWindow);
        var settings = ScriptableObject.CreateInstance<FuelRaceSettings>();
        try
        {
            float edge = path.Samples[path.IndexAtFraction(CircuitConfig.PitAt)].HalfWidth;
            Check(path.PitLane.ServiceInner - edge >= 10, "Cobertura perto demais da pista.");
            for (int count = 1; count <= 4; count++)
            {
                var world = new GameObject("Validação do posto");
                world.transform.localScale = new Vector3(-1, 1, 1);
                try
                {
                    var pit = SceneKit.Node("Posto", world.transform).gameObject.AddComponent<CandyPitStop>();
                    settings.pumpCount = count;
                    pit.Configure(path, settings);
                    Check(pit.transform.Find("Loja de conveniência do posto") != null, "Prédio novo ausente.");
                    var routes = new List<Transform>();
                    routes.AddRange(pit.entryWaypoints); routes.AddRange(pit.exitWaypoints); routes.AddRange(pit.queueWaypoints);
                    foreach (var pump in pit.pumps)
                    {
                        routes.Add(pump.approach); routes.Add(pump.stop); routes.Add(pump.clear);
                        Check(path.PitLane.Coordinates(pump.stop.localPosition.x, pump.stop.localPosition.z).x > path.PitLane.ServiceInner,
                            "Bomba fora do recuo de serviço.");
                    }
                    foreach (var point in routes)
                        Check(path.PitLane.Contains(point.localPosition.x, point.localPosition.z, .01f), "Rota fora do piso: " + point.name);
                    if (count == 1) CheckFuelHUD(world, pit, settings);
                }
                finally { UnityEngine.Object.DestroyImmediate(world); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }

    static void CheckFuelHUD(GameObject world, CandyPitStop pit, FuelRaceSettings settings)
    {
        var state = new GameState(); state.Set(GameStateName.Racing);
        var hud = world.AddComponent<FuelHUD>(); hud.Initialize(state, pit);
        var kart = new Kart(settings);
        var entries = new[] { new GameUI.HudEntry { Index = 0, Kart = kart,
            Viewport = new Viewport { X = 0, Y = 0, Width = Screen.width, Height = Screen.height } } };
        hud.Display(entries, .1f);
        var meter = hud.transform.Find("Canvas Combustível/Gasolina jogador 1/Gasolina horizontal").GetComponent<Image>();
        Check(meter.type == Image.Type.Sliced && meter.rectTransform.rect.width > meter.rectTransform.rect.height,
            "Barra de combustível não está horizontal.");
        float fullWidth=meter.rectTransform.rect.width;
        kart.Fuel.TrySpend(50, "validação"); hud.Display(entries, 2);
        Check(Mathf.Abs(meter.rectTransform.rect.width/fullWidth - .5f) < .001f, "Barra não acompanhou o consumo.");
        foreach (var graphic in hud.GetComponentsInChildren<Graphic>()) Check(!graphic.raycastTarget, "HUD capturando toque.");
    }
}
