using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CandyRush.Integrated
{
    /// <summary>Personagem escolhido no menu e cores das luzes do kart.</summary>
    public sealed class KartPalette
    {
        public string Sprite;
        public int Taillight, Exhaust;

        // Coat colors follow the selection portraits, independently of helmet/kart paint.
        public Color FurColor
        {
            get
            {
                switch (Sprite)
                {
                    case "blue": return Hex.C(0xf1f2f4); // Nuvem: white
                    case "yellow": return Hex.C(0x765239); // Caramelo: brown
                    case "purple": return Hex.C(0xb8b4bf); // Violeta: soft gray
                    default: return Hex.C(0xc69d72); // Amora: warm beige
                }
            }
        }

        public Color FurMarkingsColor
        {
            get
            {
                switch (Sprite)
                {
                    case "blue": return Hex.C(0xf7f6f3);
                    case "yellow": return Hex.C(0xdac3a3);
                    case "purple": return Hex.C(0xeeeaf0);
                    default: return Hex.C(0xf0dfc5);
                }
            }
        }

        public static KartPalette Player => Rival("pink");
        public static KartPalette Rival(string sprite) => new KartPalette
        {
            Sprite = sprite, Taillight = 0xff4f7d, Exhaust = 0xffc9dd,
        };
    }

    /// <summary>Modelos 3D dos pilotos ligados à física da corrida.</summary>
    public sealed class KartView
    {
        static readonly string[] SpriteOrder = { "blue", "pink", "yellow", "purple" };
        const float GroundOffset = .38f;

        // Câmera usada apenas pelos brilhos de freio e turbo.
        public static Camera ActiveCamera;

        static Texture2D glowTexture;
        static Texture2D GlowTexture => glowTexture ??= ProceduralTextures.Glow();

        public readonly Transform Root;
        readonly Kart kart;
        KartPalette palette;

        Transform model;
        RabbitKartAnimation animation;
        KartVisuals visuals;
        readonly Transform[] wheelGroups = new Transform[4];
        readonly Transform taillightGlow;
        readonly Material taillightMaterial;
        readonly Transform exhaustGlow;
        readonly Material exhaustMaterial;

        public KartView(Kart kart, Transform world, KartPalette palette)
        {
            this.kart = kart;
            this.palette = palette;

            Root = SceneKit.Node("kart", world);
            Root.gameObject.AddComponent<KartRaceSystems>().Bind(kart);
            ApplyCharacter();

            taillightMaterial = MakeGlowMaterial(palette.Taillight);
            taillightGlow = SceneKit.Mesh(world, Geo.Plane(0.55f, 0.55f),
                new Mat { Material = taillightMaterial }, false, false, "taillight-glow");

            exhaustMaterial = MakeGlowMaterial(palette.Exhaust);
            exhaustGlow = SceneKit.Mesh(world, Geo.Plane(0.4f, 0.4f),
                new Mat { Material = exhaustMaterial }, false, false, "exhaust-glow");
        }

        static Material MakeGlowMaterial(int color)
        {
            var m = new Material(Mats.UnlitShader) { name = $"glow-{color:x6}" };
            m.SetTexture("_MainTex", GlowTexture);
            m.SetColor("_Color", Hex.C(color, 0.3f));
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_UseFog", 0);
            m.renderQueue = (int)RenderQueue.Transparent + 5;
            return m;
        }

        void ApplyCharacter()
        {
            int index = Array.IndexOf(SpriteOrder, palette.Sprite);
            if (index < 0) index = 1;
            string character = RacerProfile.RabbitNames[index];
            var prefab = Resources.Load<GameObject>("Candy/Characters/RabbitKart_" + character);
            if (prefab == null)
                throw new InvalidOperationException("Modelo 3D não importado: " + character);
            if (model != null) { model.gameObject.SetActive(false); UnityEngine.Object.Destroy(model.gameObject); }
            foreach (var wheel in wheelGroups)
                if (wheel != null) { wheel.gameObject.SetActive(false); UnityEngine.Object.Destroy(wheel.gameObject); }
            model = UnityEngine.Object.Instantiate(prefab, Root, false).transform;
            // glTFast converte X para Unity; o mundo do jogo já espelha esse eixo.
            model.localScale = new Vector3(-1.2f, 1.2f, 1.2f);
            model.localPosition = new Vector3(0, KartConfig.Wheels.AttachY - KartConfig.Suspension.RestLength - .24f, 0);
            animation = model.gameObject.AddComponent<RabbitKartAnimation>();
            for (int i = 0; i < kart.Wheels.Length; i++)
            {
                Wheel wheel = kart.Wheels[i];
                Transform mesh = Array.Find(model.GetComponentsInChildren<Transform>(true), t => t.name == "Wheel_" + wheel.Spec.Name);
                if (mesh == null) throw new InvalidOperationException("Roda ausente: " + wheel.Spec.Name);
                Transform group = SceneKit.Node("Suspension_" + wheel.Spec.Name, Root);
                mesh.SetParent(group, false);
                mesh.localPosition = Vector3.zero;
                mesh.localRotation = Quaternion.identity;
                float scale = wheel.Spec.Radius / (wheel.Spec.IsFront ? .20f : .26f);
                mesh.localScale = new Vector3(-scale, scale, scale);
                wheelGroups[i] = group;
            }
            visuals = model.gameObject.AddComponent<KartVisuals>();
            visuals.Configure(model, wheelGroups, kart.Wheels);
            var fur = model.gameObject.AddComponent<FurController>();
            fur.SetCharacterColors(palette.FurColor, palette.FurMarkingsColor);
            fur.Initialize();
            // As rodas já estão nas suspensões e os materiais de acabamento já foram criados.
            if (index == RacerProfile.Rabbit) CosmeticRig.Aplicar(Root, ShopProfile.EquipadosEm(index));
        }
        /// <summary>Troca a paleta em tempo real (seleção de coelho no menu).</summary>
        public void SetPalette(KartPalette next)
        {
            palette = next;
            ApplyCharacter();
        }

        static Quaternion BillboardRotation(Vector3 fromThree)
        {
            Camera cam = ActiveCamera != null ? ActiveCamera : Camera.main;
            if (cam == null) return Quaternion.identity;
            Vector3 camThree = ThreeSpace.FromUnity(cam.transform.position);
            Vector3 direction = camThree - fromThree;
            if (direction.sqrMagnitude < 1e-6f) return Quaternion.identity;
            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// <summary>`alpha` vem do acumulador do loop.</summary>
        public void Update(float alpha, float frameDt = 0)
        {
            kart.GetRenderTransform(alpha, out Vector3 position, out Quaternion orientation);
            Vector3 basePos = new Vector3(position.x, position.y - GroundOffset, position.z);

            Root.localPosition = position;
            Root.localRotation = orientation;
            visuals.Animate(kart, frameDt);
            animation.SetInput(kart.Telemetry.ForwardSpeed,
                kart.Telemetry.SteerAngleDeg / KartConfig.Steering.MaxAngleDegrees);
            animation.Animate(frameDt);

            // As luzes ficam levemente atrás do kart, na direção real dele — não
            // da câmera —, senão elas "flutuam" na frente quando o carrinho vira.
            Vector3 backward = orientation * new Vector3(0, 0, -1);
            Vector3 tailPos = basePos + backward * 0.85f;
            tailPos.y = basePos.y + 0.25f;
            taillightGlow.localPosition = tailPos;
            taillightGlow.localRotation = BillboardRotation(tailPos);

            Vector3 exhaustPos = basePos + backward * 0.95f;
            exhaustPos.y = basePos.y + 0.12f;
            exhaustGlow.localPosition = exhaustPos;
            exhaustGlow.localRotation = BillboardRotation(exhaustPos);

            UpdateLights();
        }

        /// <summary>Lanternas no freio, escapamento no turbo.</summary>
        void UpdateLights()
        {
            KartTelemetry t = kart.Telemetry;
            bool braking = t.ForwardSpeed > 1 && t.DriveForce <= 0;
            taillightMaterial.SetColor("_Color", Hex.C(palette.Taillight, braking ? 0.85f : 0.35f));
            taillightGlow.localScale = Vector3.one * (braking ? 0.7f : 0.5f);

            float boost = kart.Drift.BoostTimeRemaining;
            exhaustMaterial.SetColor("_Color", Hex.C(palette.Exhaust, 0.15f + boost * 0.6f));
            exhaustGlow.localScale = Vector3.one * (0.35f + boost * 0.5f);
        }

        public void SetVisible(bool visible)
        {
            Root.gameObject.SetActive(visible);
            taillightGlow.gameObject.SetActive(visible);
            exhaustGlow.gameObject.SetActive(visible);
        }
    }
}
