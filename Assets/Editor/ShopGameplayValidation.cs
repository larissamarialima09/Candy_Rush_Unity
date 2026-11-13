using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CandyRush.Integrated;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Valida cliques por raycast, transações, prefabs e combinação da prévia sem salvar cenas.</summary>
[InitializeOnLoad]
public static class ShopGameplayValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Request = "Temp/CandyRushShopValidation.request";
    const string Status = "../shop-gameplay-validation.txt";
    const string PlayKey = "CandyRush.ShopValidation.Play";
    const string BatchKey = "CandyRush.ShopValidation.Batch";
    static int playFrames;
    static bool validating;
    static int Coelho => RacerProfile.Rabbit;

    static ShopGameplayValidation() => EditorApplication.update += Poll;

    public static void RunBatch()
    {
        SessionState.SetBool(BatchKey, true);
        RunRequest();
    }

    static void Poll()
    {
        if (validating) return;
        if (SessionState.GetBool(PlayKey, false) && EditorApplication.isPlaying && !EditorApplication.isCompiling)
        {
            if (++playFrames >= 20) RunRequest();
            return;
        }
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(Request);
        EditorApplication.delayCall += RunRequest;
    }

    [MenuItem("CandyRush/Loja/Validar compras e previa 3D")]
    static async void RunRequest()
    {
        if (validating) return;
        if (!Application.isPlaying)
        {
            ShopCosmeticAssets.Bake();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            SessionState.SetBool(PlayKey, true);
            playFrames = 0;
            File.WriteAllText(Status, "RUNNING: validacao de cliques e compras em Play Mode.\n");
            EditorApplication.EnterPlaymode();
            return;
        }
        validating = true;
        Application.runInBackground = true;
        int exitCode = 0;
        try { await Run(); File.WriteAllText(Status, "OK: raycasts, compras, saldo, equipamento, combinacao 3D, bloqueios, carteira e prefabs.\n"); }
        catch (Exception e) { exitCode = 1; File.WriteAllText(Status, "FAILED: " + e); Debug.LogException(e); }
        finally
        {
            validating = false;
            if (SessionState.GetBool(PlayKey, false))
            {
                SessionState.SetBool(PlayKey, false);
                EditorApplication.ExitPlaymode();
            }
            if (SessionState.GetBool(BatchKey, false))
            {
                SessionState.SetBool(BatchKey, false);
                EditorApplication.Exit(exitCode);
            }
        }
    }

    public static async Task Run()
    {
        if (!Application.isPlaying) ShopCosmeticAssets.Bake();
        using (ShopProfile.BeginValidationProfile()) CheckTransactions();
        using (ShopProfile.BeginValidationProfile()) await CheckScreen();
        Debug.Log("SHOP_GAMEPLAY_VALIDATION_OK: cliques reais, transacoes, moedas, equipamento e combinacao 3D.");
    }

    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);

    static void CheckTransactions()
    {
        var item = ShopCatalog.Find("unicornio");
        Check(ShopProfile.Buy(item, Coelho) && ShopProfile.Coins == 750, "Compra nao descontou o preco exato.");
        Check(ShopProfile.Owns(item, Coelho) && !ShopProfile.Owns(item, (Coelho + 1) % 4), "Compra nao respeitou o inventario por coelho.");
        Check(!ShopProfile.Buy(item, Coelho) && ShopProfile.Coins == 750, "Compra duplicada cobrou novamente.");
        Check(!ShopProfile.Buy(ShopCatalog.Find("coroa"), Coelho), "Compra sem saldo foi aceita.");
        Check(!ShopProfile.Buy(ShopCatalog.Find("misterioso"), Coelho), "Item bloqueado foi comprado.");
        Check(!ShopProfile.Buy(null, Coelho), "Compra nula aceita.");
        Check(ShopProfile.Equip(item, Coelho) && ShopProfile.EstaEquipado(item, Coelho), "Equipamento nao persistiu.");
        ShopProfile.Credit(15); ShopProfile.Credit(-10); ShopProfile.Save();
        Check(ShopProfile.Coins == 765, "Moedas coletadas nao entraram na carteira.");
        Check(!ShopProfile.Pay(-1) && !ShopProfile.Pay(1000) && ShopProfile.Coins == 765, "Pagamento invalido alterou a carteira.");
    }

    static async Task CheckScreen()
    {
        var root = new GameObject("Shop validation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.hideFlags = HideFlags.DontSave;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1672, 941); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var shopObject = new GameObject("Loja", typeof(RectTransform)); shopObject.transform.SetParent(root.transform, false);
        ShopScreen shop = null;
        EventSystem originalEvents = EventSystem.current;
        var testEventsObject = new GameObject("Shop test events", typeof(EventSystem));
        testEventsObject.transform.SetParent(root.transform, false);
        var testEvents = testEventsObject.GetComponent<EventSystem>();
        try
        {
            // Os componentes de input nao executam OnEnable em Edit Mode.
            if (!Application.isPlaying) Call(testEvents, "OnEnable");
            EventSystem.current = testEvents;
            shop = shopObject.AddComponent<ShopScreen>();
            if (Field(shop, "palcoDePrevia") == null) Call(shop, "Awake");
            if (!Application.isPlaying) Call(shop, "OnEnable");
            shop.GetComponent<Canvas>().sortingOrder = 10000;
            if (!Application.isPlaying)
                foreach (var raycaster in root.GetComponentsInChildren<GraphicRaycaster>()) Call(raycaster, "OnEnable");
            Canvas.ForceUpdateCanvases();
            await Click(shop.transform.Find("Palco/Painel/Cartao unicornio"));
            Check(((ShopItem)Field(shop, "selecionado")).Id == "unicornio", "Raycast nao selecionou o card.");
            await Click(shop.transform.Find("Palco/Testar"));
            await Click(shop.transform.Find("Palco/Aba KARTS"));
            await Click(shop.transform.Find("Palco/Painel/Cartao pintura_menta"));
            await Click(shop.transform.Find("Palco/Testar"));
            await Click(shop.transform.Find("Palco/Aba RODAS"));
            await Click(shop.transform.Find("Palco/Painel/Cartao aro_azul"));
            await Click(shop.transform.Find("Palco/Testar"));
            var tried = (Dictionary<ShopCategoria, ShopItem>)Field(shop, "provados");
            Check(tried.Count == 3 && ShopProfile.Coins == 1250, "Testes nao combinaram as categorias ou gastaram moedas.");
            var stage = (ShopPreviewStage)Field(shop, "palcoDePrevia");
            CheckOutfit(stage, "unicornio", ShopCatalog.Find("pintura_menta").Cor, ShopCatalog.Find("aro_azul").Cor);
            SavePreview(stage);
            await Click(shop.transform.Find("Palco/Comprar"));
            await Click(shop.transform.Find("Palco/Comprar"));
            Check(ShopProfile.Coins == 1100 && ShopProfile.Owns(ShopCatalog.Find("aro_azul")), "Compra via interface falhou ou cobrou duas vezes.");
            await Click(shop.transform.Find("Palco/Equipar"));
            Check(ShopProfile.EstaEquipado(ShopCatalog.Find("aro_azul"), Coelho), "Equipar via interface falhou.");
            await Click(shop.transform.Find("Palco/Aba KARTS"));
            await Click(shop.transform.Find("Palco/Painel/Cartao pintura_menta"));
            await Click(shop.transform.Find("Palco/Comprar")); await Click(shop.transform.Find("Palco/Equipar"));
            await Click(shop.transform.Find("Palco/Aba CAPACETES"));
            await Click(shop.transform.Find("Palco/Painel/Cartao unicornio"));
            await Click(shop.transform.Find("Palco/Comprar")); await Click(shop.transform.Find("Palco/Equipar"));
            Check(ShopProfile.Coins == 350 && ShopProfile.EquipadosEm(Coelho).Length == 3, "Visual completo nao foi comprado/equipado.");
            CheckOutfit(stage, "unicornio", ShopCatalog.Find("pintura_menta").Cor, ShopCatalog.Find("aro_azul").Cor);
            var raceWorld = new GameObject("Shop race validation");
            try
            {
                raceWorld.transform.localScale = new Vector3(-1, 1, 1);
                var kart = new Kart();
                var view = new KartView(kart, raceWorld.transform, KartPalette.Rival(RacerProfile.RabbitSprites[Coelho]));
                for (int frame = 0; frame < 3; frame++) view.Update(1, 1f / 60);
                CheckOutfit(view.Root, "unicornio", ShopCatalog.Find("pintura_menta").Cor, ShopCatalog.Find("aro_azul").Cor);
            }
            finally { Object.DestroyImmediate(raceWorld); }
            await Click(shop.transform.Find("Palco/Painel/Cartao misterioso"));
            Check(!shop.transform.Find("Palco/Testar").GetComponent<Button>().interactable
                && !shop.transform.Find("Palco/Comprar").GetComponent<Button>().interactable, "Bloqueio nao chegou aos botoes.");
            await Click(shop.transform.Find("Palco/Mais"));
            Check(shop.transform.Find("Carteira").gameObject.activeSelf, "Botao + nao abriu a carteira.");
            await Click(shop.transform.Find("Carteira/Conteudo/Fechar"));
            Check(!shop.transform.Find("Carteira").gameObject.activeSelf, "Carteira nao fechou.");
            var drag = new PointerEventData(EventSystem.current) { delta = new Vector2(80, 0) };
            float before = (float)Field(stage, "giroDoArrasto");
            ExecuteEvents.Execute(shop.transform.Find("Palco/Previa").gameObject, drag, ExecuteEvents.dragHandler);
            Check((float)Field(stage, "giroDoArrasto") > before, "Arrasto nao girou o modelo.");
            Call(shop, "OnDisable"); Call(shop, "OnEnable");
            Check(tried.Count == 0 && ShopProfile.EquipadosEm(Coelho).Length == 3 && ShopProfile.Coins == 350, "Reabrir perdeu compras ou manteve testes temporarios.");
            CheckOutfit(stage, "unicornio", ShopCatalog.Find("pintura_menta").Cor, ShopCatalog.Find("aro_azul").Cor);
            foreach (string id in new[] { "cupcake", "unicornio", "rosquinha", "coroa", "cachecol" })
            {
                var item = ShopCatalog.Find(id);
                Check(item.Asset != null && item.Asset.prefab != null && item.Asset.prefab.GetComponentsInChildren<MeshRenderer>().Length > 0, "Prefab 3D ausente: " + id);
                for (int rabbit = 0; rabbit < 4; rabbit++)
                {
                    stage.Montar(rabbit, new[] { item });
                    Check(((Transform)Field(stage, "modelo")).GetComponentsInChildren<Transform>().Any(t => t.name == "HeadSocket" && t.childCount == 1), "Acessorio nao encaixou: " + id);
                    CheckFraming(stage);
                }
            }
            bool raceRequested = false;
            shop.aoCorrer.AddListener(() => raceRequested = true);
            await Click(shop.transform.Find("Palco/Mais"));
            await Click(shop.transform.Find("Carteira/Conteudo/Correr"));
            Check(raceRequested && !shop.gameObject.activeSelf, "Carteira nao encaminhou para a corrida.");
        }
        finally
        {
            if (shop != null && !Application.isPlaying) { Call(shop, "OnDisable"); Call(shop, "OnDestroy"); }
            if (!Application.isPlaying)
            {
                foreach (var raycaster in root.GetComponentsInChildren<GraphicRaycaster>()) Call(raycaster, "OnDisable");
                Call(testEvents, "OnDisable");
            }
            Object.DestroyImmediate(root);
            if (originalEvents != null) EventSystem.current = originalEvents;
        }
    }

    static async Task Click(Transform target)
    {
        Check(target != null, "Controle ausente.");
        // A profundidade usada pelo GraphicRaycaster so existe depois do primeiro render.
        var targetGraphic = target.GetComponent<Graphic>();
        for (int wait = 0; wait < 100 && targetGraphic.depth < 0; wait++)
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorApplication.QueuePlayerLoopUpdate();
            await Task.Delay(25);
        }
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)target;
        var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0)
        {
            var graphic = target.GetComponent<Graphic>();
            var canvas = target.GetComponentInParent<Canvas>();
            throw new InvalidOperationException("Nenhum raycast em " + target.name + "; point=" + point
                + "; screen=" + Screen.width + "x" + Screen.height + "; rect=" + rect.rect
                + "; depth=" + graphic.depth + "; raycast=" + graphic.raycastTarget
                + "; canvas=" + canvas.renderMode + "/" + canvas.pixelRect + "/" + canvas.scaleFactor
                + "; raycasters=" + RaycasterManager.GetRaycasters().Count
                + "; graphics=" + GraphicRegistry.GetGraphicsForCanvas(canvas).Count);
        }
        var actual = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Check(actual == target.gameObject || !target.GetComponent<Button>().interactable,
            "Clique em " + target.name + " interceptado por " + hits[0].gameObject.name);
        if (actual != null) ExecuteEvents.Execute(actual, pointer, ExecuteEvents.pointerClickHandler);
    }

    static void CheckOutfit(ShopPreviewStage stage, string accessory, Color body, Color rim)
        => CheckOutfit((Transform)Field(stage, "modelo"), accessory, body, rim);

    static void CheckOutfit(Transform model, string accessory, Color body, Color rim)
    {
        Check(model.GetComponentsInChildren<Transform>().Any(t => t.name == "HeadSocket" && t.childCount == 1), "Modelo sem acessorio 3D: " + accessory);
        var fur = model.GetComponentInChildren<FurController>();
        Check(fur != null && fur.coatColor == RacerProfile.Palette.FurColor, "Pintura da loja alterou a cor do personagem.");
        int bodies = 0, wheels = 0;
        var block = new MaterialPropertyBlock();
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || !slots[i].name.Contains("_Main")) continue;
                bool isWheel = renderer.name.StartsWith("Wheel_") || renderer.name.StartsWith("AuxWheel_");
                if (!isWheel && renderer.name != "Kart_Body") continue;
                renderer.GetPropertyBlock(block, i);
                var cor = block.GetColor(slots[i].HasProperty("baseColorFactor") ? "baseColorFactor" : "_Color");
                Check(cor == (isWheel ? rim : body), "Pintura nao chegou ao material: " + renderer.name);
                if (isWheel) wheels++; else bodies++;
            }
        }
        Check(bodies > 0 && wheels == 4, "Pintura incompleta no chassi/quatro rodas.");
    }

    static void CheckFraming(ShopPreviewStage stage)
    {
        var model = (Transform)Field(stage, "modelo");
        var pivot = (Transform)Field(stage, "pivo");
        var camera = (Camera)Field(stage, "camera");
        foreach (int angle in new[] { 0, 90, 180, 270 })
        {
            pivot.localRotation = Quaternion.Euler(0, angle, 0);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var bounds = renderer.bounds;
                foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                {
                    var point = camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                    Check(point.z > camera.nearClipPlane && point.z < camera.farClipPlane && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1,
                        "Modelo/acessorio cortado na previa: " + renderer.name + " em " + angle);
                }
            }
        }
    }

    static void SavePreview(ShopPreviewStage stage)
    {
        var camera = (Camera)Field(stage, "camera"); camera.Render();
        var previous = RenderTexture.active;
        var texture = new Texture2D(stage.Textura.width, stage.Textura.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = stage.Textura;
            texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply();
            File.WriteAllBytes("../shop-preview-validado.png", texture.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); }
    }

}
