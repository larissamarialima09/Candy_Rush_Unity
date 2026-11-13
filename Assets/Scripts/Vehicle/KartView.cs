using CandyRush.Config;
using CandyRush.Rendering;
using UnityEngine;

namespace CandyRush.Vehicle
{
    /// <summary>Cores de um kart. Cada piloto tem as suas.</summary>
    public sealed class KartPalette
    {
        public Color Body, BodyDark, Trim, Seat, Emblem, Tire, Hub, Fur, EarInner, Nose, Eye, Blush, Headlight, Taillight, Exhaust;

        /// <summary>O kart do jogador: coelho rosa.</summary>
        public static KartPalette Player => new KartPalette
        {
            Body = CircuitConfig.Hex(0xff8fbe),
            BodyDark = CircuitConfig.Hex(0xef6ba4),
            Trim = CircuitConfig.Hex(0xfff4f8),
            Seat = CircuitConfig.Hex(0x8f6bd6),
            Emblem = CircuitConfig.Hex(0xffd76a),
            Tire = CircuitConfig.Hex(0x4a3a44),
            Hub = CircuitConfig.Hex(0xffe3ee),
            Fur = CircuitConfig.Hex(0xfff8fb),
            EarInner = CircuitConfig.Hex(0xffb3cd),
            Nose = CircuitConfig.Hex(0xff8fb1),
            Eye = CircuitConfig.Hex(0x3b2430),
            Blush = CircuitConfig.Hex(0xff9ec4),
            Headlight = CircuitConfig.Hex(0xfff6d8),
            Taillight = CircuitConfig.Hex(0xff4f7d),
            Exhaust = CircuitConfig.Hex(0xffc9dd),
        };
    }

    /// <summary>
    /// Aparência do kart: carrinho de brinquedo rosa com um coelho dirigindo
    /// (convertido de src/vehicle/kartView.ts). Nenhuma decisão de jogo mora aqui.
    ///
    /// Na web as peças usavam caixas arredondadas; aqui usam as formas
    /// primitivas da Unity (cubo, esfera, cápsula, cilindro), nas mesmas medidas.
    /// As rodas usam a compressão da suspensão calculada pela física.
    /// </summary>
    public sealed class KartView
    {
        public readonly Transform Root;

        private readonly Kart _kart;
        private readonly Transform[] _wheels;
        private readonly Material _taillights;
        private readonly Material _exhaustGlow;
        private readonly Transform _exhaust;
        private readonly KartPalette _palette;

        public KartView(Kart kart, Transform parent, KartPalette palette = null)
        {
            _kart = kart;
            _palette = palette ?? KartPalette.Player;
            Root = new GameObject("Kart").transform;
            Root.SetParent(parent, false);

            Vector3 size = KartConfig.Chassis.Size;
            KartPalette p = _palette;

            // --- Carroceria ---
            Part(PrimitiveType.Cube, Root, new Vector3(0f, -0.04f, 0f), new Vector3(size.x * 0.94f, size.y * 0.8f, size.z * 0.9f), Vector3.zero, Candy(p.Body));
            // Faixa creme na barra.
            Part(PrimitiveType.Cube, Root, new Vector3(0f, -size.y * 0.36f, 0f), new Vector3(size.x * 0.98f, 0.16f, size.z * 0.86f), Vector3.zero, Candy(p.Trim));
            // Capô inclinado.
            Part(PrimitiveType.Cube, Root, new Vector3(0f, size.y * 0.4f, size.z * 0.27f), new Vector3(size.x * 0.78f, 0.15f, size.z * 0.36f), new Vector3(-0.1f, 0f, 0f), Candy(p.BodyDark));
            // Para-choque: o "sorriso" do carrinho.
            Part(PrimitiveType.Capsule, Root, new Vector3(0f, -0.14f, size.z * 0.48f), CapsuleScale(0.11f, size.x * 0.72f), new Vector3(0f, 0f, Mathf.PI / 2f), Candy(p.Trim));

            // Emblema de coração.
            Material emblem = CandyMaterials.Glow(p.Emblem, 0.55f);
            var heart = new GameObject("Coracao").transform;
            heart.SetParent(Root, false);
            heart.localPosition = new Vector3(0f, size.y * 0.5f, size.z * 0.27f);
            heart.localRotation = Rotation(new Vector3(-0.1f, 0f, 0f));
            foreach (int side in new[] { -1, 1 })
                Part(PrimitiveType.Sphere, heart, new Vector3(side * 0.055f, 0.02f, 0f), SphereScale(0.075f, new Vector3(1f, 0.8f, 0.6f)), Vector3.zero, emblem);
            Part(PrimitiveType.Sphere, heart, new Vector3(0f, -0.07f, 0f), SphereScale(0.075f, new Vector3(1.35f, 1.1f, 0.6f)), Vector3.zero, emblem);

            // --- Faróis e lanternas ---
            Material headlight = CandyMaterials.Glow(p.Headlight, 2.4f);
            foreach (int side in new[] { -1, 1 })
                Part(PrimitiveType.Sphere, Root, new Vector3(side * size.x * 0.3f, -0.02f, size.z * 0.46f), SphereScale(0.1f, new Vector3(1f, 0.8f, 0.7f)), Vector3.zero, headlight);

            _taillights = CandyMaterials.Glow(p.Taillight, 0.5f);
            foreach (int side in new[] { -1, 1 })
                Part(PrimitiveType.Sphere, Root, new Vector3(side * size.x * 0.28f, 0.04f, -size.z * 0.46f), SphereScale(0.075f, new Vector3(1.4f, 0.7f, 0.5f)), Vector3.zero, _taillights);

            // --- Aerofólio ---
            Part(PrimitiveType.Cube, Root, new Vector3(0f, size.y * 0.62f, -size.z * 0.46f), new Vector3(size.x * 0.9f, 0.07f, 0.26f), Vector3.zero, Candy(p.BodyDark));
            foreach (int side in new[] { -1, 1 })
                Part(PrimitiveType.Cube, Root, new Vector3(side * size.x * 0.3f, size.y * 0.5f, -size.z * 0.46f), new Vector3(0.07f, 0.24f, 0.09f), Vector3.zero, Candy(p.Trim));

            // --- Escapamento (brilha no turbo) ---
            _exhaustGlow = CandyMaterials.Glow(p.Exhaust, 0.2f);
            _exhaust = Part(PrimitiveType.Cylinder, Root, new Vector3(0f, -0.08f, -size.z * 0.5f), CylinderScale(0.1f, 0.22f), new Vector3(Mathf.PI / 2f, 0f, 0f), _exhaustGlow);

            // --- Cockpit ---
            Part(PrimitiveType.Cube, Root, new Vector3(0f, size.y * 0.42f, -size.z * 0.24f), new Vector3(size.x * 0.5f, 0.34f, 0.18f), Vector3.zero, Candy(p.Seat, 0.6f));
            // Volante (na web era um toro; aqui um disco fino).
            Part(PrimitiveType.Cylinder, Root, new Vector3(0f, size.y * 0.5f, size.z * 0.1f), new Vector3(0.3f, 0.015f, 0.3f), new Vector3(-0.5f + Mathf.PI / 2f, 0f, 0f), Candy(p.Trim));

            BuildBunny(size.y * 0.36f, -size.z * 0.06f);

            // --- Rodas ---
            _wheels = new Transform[kart.Wheels.Length];
            for (int i = 0; i < kart.Wheels.Length; i++)
            {
                WheelSpec spec = kart.Wheels[i].Spec;
                var group = new GameObject("Roda " + spec.Name).transform;
                group.SetParent(Root, false);

                Part(PrimitiveType.Cylinder, group, Vector3.zero, CylinderScale(spec.Radius, KartConfig.Wheels.Width), new Vector3(0f, 0f, Mathf.PI / 2f), Candy(p.Tire, 0.75f));
                Part(PrimitiveType.Cylinder, group, Vector3.zero, CylinderScale(spec.Radius * 0.52f, KartConfig.Wheels.Width * 1.08f), new Vector3(0f, 0f, Mathf.PI / 2f), Candy(p.Hub, 0.25f));
                // Três raios: sem eles é impossível ver a roda girando.
                for (int s = 0; s < 3; s++)
                    Part(PrimitiveType.Cube, group, Vector3.zero, new Vector3(KartConfig.Wheels.Width * 1.12f, 0.05f, spec.Radius * 0.9f), new Vector3(s / 3f * Mathf.PI, 0f, 0f), Candy(p.Body, 0.3f));

                _wheels[i] = group;
            }
        }

        /// <summary>O piloto coelho: cabeça, focinho, olhos, bochechas, orelhas e bracinhos.</summary>
        private void BuildBunny(float y, float z)
        {
            KartPalette p = _palette;
            var driver = new GameObject("Coelho").transform;
            driver.SetParent(Root, false);
            driver.localPosition = new Vector3(0f, y, z);

            Material fur = Candy(p.Fur, 0.55f);

            Part(PrimitiveType.Sphere, driver, new Vector3(0f, 0.1f, 0f), SphereScale(0.21f, new Vector3(1f, 0.9f, 0.85f)), Vector3.zero, fur);
            Part(PrimitiveType.Sphere, driver, new Vector3(0f, 0.42f, 0f), SphereScale(0.28f, Vector3.one), Vector3.zero, fur);
            Part(PrimitiveType.Sphere, driver, new Vector3(0f, 0.36f, 0.19f), SphereScale(0.15f, new Vector3(1.1f, 0.85f, 0.9f)), Vector3.zero, fur);
            Part(PrimitiveType.Sphere, driver, new Vector3(0f, 0.39f, 0.32f), SphereScale(0.038f, new Vector3(1.3f, 0.9f, 1f)), Vector3.zero, Candy(p.Nose, 0.35f));

            Material eye = Candy(p.Eye, 0.15f);
            Material spark = CandyMaterials.Glow(Color.white, 0.8f);
            Material blush = Candy(p.Blush, 0.5f);
            Material earInner = Candy(p.EarInner, 0.5f);

            foreach (int side in new[] { -1, 1 })
            {
                Part(PrimitiveType.Sphere, driver, new Vector3(side * 0.11f, 0.46f, 0.24f), SphereScale(0.052f, Vector3.one), Vector3.zero, eye);
                // Brilhinho: é o que faz o olho parecer vivo.
                Part(PrimitiveType.Sphere, driver, new Vector3(side * 0.125f, 0.485f, 0.275f), SphereScale(0.019f, Vector3.one), Vector3.zero, spark);
                Part(PrimitiveType.Sphere, driver, new Vector3(side * 0.2f, 0.38f, 0.19f), SphereScale(0.045f, new Vector3(1.3f, 0.7f, 0.5f)), Vector3.zero, blush);

                // Orelha: cápsula clara com miolo rosa.
                Part(PrimitiveType.Capsule, driver, new Vector3(side * 0.12f, 0.76f, -0.02f), CapsuleScale(0.072f, 0.32f), new Vector3(-0.16f, 0f, side * 0.2f), fur);
                Part(PrimitiveType.Capsule, driver, new Vector3(side * 0.128f, 0.77f, 0.03f), CapsuleScale(0.04f, 0.24f), new Vector3(-0.16f, 0f, side * 0.2f), earInner);

                // Bracinho esticado até o volante.
                Part(PrimitiveType.Capsule, driver, new Vector3(side * 0.17f, 0.16f, 0.13f), CapsuleScale(0.05f, 0.2f), new Vector3(1.1f, 0f, side * -0.4f), fur);
                Part(PrimitiveType.Sphere, driver, new Vector3(side * 0.1f, 0.17f, 0.25f), SphereScale(0.06f, Vector3.one), Vector3.zero, fur);
            }
        }

        /// <summary>Atualiza o visual. alpha vem do acumulador do loop (interpolação entre passos).</summary>
        public void Update(float alpha)
        {
            _kart.GetRenderTransform(alpha, out Vector3 position, out Quaternion orientation);
            Root.SetPositionAndRotation(position, orientation);

            float restLength = KartConfig.Suspension.RestLength;
            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel wheel = _kart.Wheels[i];
                Vector3 local = wheel.Spec.PositionLocal;
                // A mola só move a roda em -Y local.
                _wheels[i].localPosition = new Vector3(local.x, local.y - (restLength - wheel.Compression), local.z);
                _wheels[i].localRotation = Quaternion.AngleAxis(wheel.SteerAngle * Mathf.Rad2Deg, Vector3.up)
                                           * Quaternion.AngleAxis(wheel.SpinAngle * Mathf.Rad2Deg, Vector3.right);
            }

            UpdateLights();
        }

        /// <summary>Lanternas no freio, escapamento no turbo.</summary>
        private void UpdateLights()
        {
            KartTelemetry telemetry = _kart.Telemetry;
            bool braking = telemetry.ForwardSpeed > 1f && telemetry.DriveForce <= 0f;
            CandyMaterials.SetGlow(_taillights, _palette.Taillight, braking ? 2.6f : 0.5f);

            float boost = _kart.Drift.BoostTimeRemaining;
            CandyMaterials.SetGlow(_exhaustGlow, _palette.Exhaust, 0.2f + boost * 4.5f);
            // O escapamento incha um pouco no turbo: exagero de desenho animado.
            float bulge = 1f + boost * 0.35f;
            Vector3 baseScale = CylinderScale(0.1f, 0.22f);
            _exhaust.localScale = new Vector3(baseScale.x * bulge, baseScale.y, baseScale.z * bulge);
        }

        // ------------------------------------------------------------------
        // Peças
        // ------------------------------------------------------------------

        private static Material Candy(Color color, float roughness = 0.22f) => CandyMaterials.Candy(color, roughness);

        /// <summary>Cria uma peça primitiva sem colisor. eulerRadians segue a convenção da web (radianos, ordem XYZ).</summary>
        private static Transform Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Vector3 eulerRadians, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = Rotation(eulerRadians);
            t.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return t;
        }

        /// <summary>Rotação no padrão do Three.js (ordem XYZ, em radianos).</summary>
        private static Quaternion Rotation(Vector3 eulerRadians) =>
            Quaternion.AngleAxis(eulerRadians.x * Mathf.Rad2Deg, Vector3.right)
            * Quaternion.AngleAxis(eulerRadians.y * Mathf.Rad2Deg, Vector3.up)
            * Quaternion.AngleAxis(eulerRadians.z * Mathf.Rad2Deg, Vector3.forward);

        /// <summary>Esfera da Unity tem diâmetro 1.</summary>
        private static Vector3 SphereScale(float radius, Vector3 stretch) => Vector3.Scale(Vector3.one * radius * 2f, stretch);

        /// <summary>Cápsula da web: raio + comprimento do meio. A da Unity tem 2 m de altura e 0,5 m de raio.</summary>
        private static Vector3 CapsuleScale(float radius, float length) =>
            new Vector3(radius * 2f, (length + radius * 2f) / 2f, radius * 2f);

        /// <summary>Cilindro da Unity tem 2 m de altura e 0,5 m de raio.</summary>
        private static Vector3 CylinderScale(float radius, float height) => new Vector3(radius * 2f, height / 2f, radius * 2f);
    }
}
