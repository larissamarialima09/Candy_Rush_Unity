using System;
using System.Collections.Generic;
using CandyRush.Rendering;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>
    /// Palco 3D da loja: um pódio de glacê com o kart do coelho em cima, visto por
    /// uma câmera própria que desenha numa RenderTexture (a loja mostra num RawImage).
    /// Fica longe da pista, com luzes pontuais só para ele, e só renderiza com a loja aberta.
    /// </summary>
    public sealed class ShopPreviewStage : IDisposable
    {
        // Longe da pista: a câmera do palco nunca enxerga o circuito.
        static readonly Vector3 Origem = new Vector3(0f, -800f, 0f);
        const float VelocidadeAutomatica = 14f, SensibilidadeDoArrasto = .35f;

        readonly Transform raiz, pivo;
        readonly Camera camera;
        readonly RenderTexture textura;
        Transform modelo;
        RabbitKartAnimation animacao;
        float giroAutomatico, giroDoArrasto;

        public RenderTexture Textura => textura;

        public ShopPreviewStage()
        {
            raiz = new GameObject("ShopPreviewStage").transform;
            raiz.position = Origem;

            // Pódio: centrado na origem, então o topo fica em y = 0, onde o kart assenta.
            var pódio = ProceduralMeshes.Cylinder(1.45f, 1.6f, .5f, 40);
            SceneKit.Mesh(raiz, pódio, Mats.Candy(0xffc4e2, .35f), false, true, "Podio")
                .localPosition = new Vector3(0f, -.25f, 0f);

            pivo = SceneKit.Node("Pivo", raiz);

            textura = new RenderTexture(640, 512, 16, RenderTextureFormat.ARGB32) { antiAliasing = 2, name = "ShopPreview" };
            var camObj = new GameObject("ShopPreviewCamera");
            camObj.transform.SetParent(raiz, false);
            camera = camObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.fieldOfView = 30f;
            camera.aspect = 1.25f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 40f;
            camera.targetTexture = textura;

            // Luz principal e contorno (rim light). Pontuais e com alcance curto:
            // não iluminam a pista, e a loja não depende da cena de fundo.
            Luz("LuzPrincipal", new Vector3(-2.5f, 3.5f, -3.5f), Color.white, 1.6f, 14f);
            Luz("LuzContorno", new Vector3(2.6f, 2.2f, 2.5f), new Color(1f, .8f, .95f), 1.3f, 12f);

            raiz.gameObject.SetActive(false);
        }

        void Luz(string nome, Vector3 posicao, Color cor, float intensidade, float alcance)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(raiz, false);
            go.transform.localPosition = posicao;
            var luz = go.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = cor;
            luz.intensity = intensidade;
            luz.range = alcance;
            luz.shadows = LightShadows.None;
        }

        /// <summary>Liga ou desliga o palco. Desligado, nada é desenhado nem atualizado.</summary>
        public void Ativar(bool ligado)
        {
            if (raiz == null || raiz.gameObject.activeSelf == ligado) return;
            raiz.gameObject.SetActive(ligado);
        }

        /// <summary>Recria o coelho no pódio com os itens dados. Chame com o palco ligado.</summary>
        public void Montar(int coelho, IReadOnlyList<ShopItem> itens)
        {
            if (modelo != null) { modelo.gameObject.SetActive(false); VisualMaterialLibrary.Dispose(modelo.gameObject); }
            modelo = null;
            // Sem giro no pivô, a medição do kart em mundo vale também no local (ver Assentar).
            pivo.localRotation = Quaternion.identity;

            var prefab = Resources.Load<GameObject>("Candy/Characters/RabbitKart_" + RacerProfile.RabbitNames[Mathf.Clamp(coelho, 0, 3)]);
            if (prefab == null) { Debug.LogWarning("ShopPreviewStage: modelo do coelho " + coelho + " não importado."); return; }

            var obj = UnityEngine.Object.Instantiate(prefab, pivo, false).transform;
            // Na pista a escala X é espelhada (1,2 negativo) por causa do mundo; aqui não há espelho.
            obj.localScale = Vector3.one * 1.2f;
            modelo = obj;
            animacao = obj.gameObject.AddComponent<RabbitKartAnimation>();

            var palheta = KartPalette.Rival(RacerProfile.RabbitSprites[Mathf.Clamp(coelho, 0, 3)]);
            var fur = obj.gameObject.AddComponent<FurController>();
            fur.SetCharacterColors(palheta.FurColor, palheta.FurMarkingsColor);
            fur.Initialize();

            CosmeticRig.Aplicar(obj, itens);
            Assentar(obj);
        }

        /// <summary>Centraliza o kart em X/Z e apoia a base dele no topo do pódio (y = 0).</summary>
        void Assentar(Transform obj)
        {
            Bounds caixa = new Bounds();
            bool primeiro = true;
            foreach (var r in obj.GetComponentsInChildren<Renderer>())
            {
                if (primeiro) { caixa = r.bounds; primeiro = false; }
                else caixa.Encapsulate(r.bounds);
            }
            if (primeiro) return;

            // Pivô e raiz não têm rotação nem escala neste momento, então o deslocamento
            // em mundo vale também no local.
            // Os bounds são globais, mas o palco está em y=-800. Converter antes
            // de assentar evita levar o modelo 800 metros acima do pódio.
            Vector3 centro = raiz.InverseTransformPoint(caixa.center);
            float baseY = raiz.InverseTransformPoint(caixa.min).y;
            obj.localPosition += new Vector3(-centro.x, -baseY, -centro.z);

            // Câmera: enquadra o kart inteiro com uma folga.
            float largura = Mathf.Max(3.3f, new Vector2(caixa.size.x, caixa.size.z).magnitude);
            float altura = caixa.size.y + .7f;
            float distancia = Mathf.Max(largura, altura) * 1.18f / (2f * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad));
            camera.farClipPlane = distancia + largura + 10f;
            camera.transform.localPosition = new Vector3(0f, altura * .55f + distancia * .18f, distancia);
            camera.transform.LookAt(raiz.TransformPoint(new Vector3(0f, caixa.size.y * .42f, 0f)));
            giroAutomatico = -24f;
            giroDoArrasto = 0f;
        }

        /// <summary>Arrastar o dedo sobre a prévia gira o coelho.</summary>
        public void Girar(float deltaX) => giroDoArrasto += deltaX * SensibilidadeDoArrasto;

        /// <summary>Gira devagar sozinho e aplica o giro do arrasto. Só roda com o palco ligado.</summary>
        public void Atualizar(float dt)
        {
            if (raiz == null || !raiz.gameObject.activeSelf) return;
            giroAutomatico += VelocidadeAutomatica * dt;
            pivo.localRotation = Quaternion.Euler(0f, giroAutomatico + giroDoArrasto, 0f);
            if (animacao != null) { animacao.SetInput(.35f, .15f); animacao.Animate(dt); }
        }

        public void Dispose()
        {
            if (raiz != null) VisualMaterialLibrary.Dispose(raiz.gameObject);
            if (textura != null) { textura.Release(); VisualMaterialLibrary.Dispose(textura); }
        }
    }
}
