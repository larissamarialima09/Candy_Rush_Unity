using CandyRush.Rendering;
using UnityEngine;
using Gate = CandyRush.Config.FeaturesConfig.StartGate;

namespace CandyRush.Track
{
    /// <summary>
    /// O portal "CANDY RUSH" sobre a linha de largada (convertido de
    /// addStartGate em src/track/circuitSurface.ts).
    ///
    /// Dois pilares de bengala, um arco achatado, pirulitos gigantes,
    /// nuvenzinhas de rosto feliz, estrelas, letreiro com coroa, semáforo e
    /// bandeiras de chegada. Tudo olha para -Z local, o lado do grid: é de lá
    /// que o jogador vê o portal na largada. Só visual, sem colisão.
    /// </summary>
    public static class StartGate
    {
        public static void Build(CircuitPath path, Transform parent)
        {
            PathSample sample = path.Samples[0];
            float halfSpan = sample.HalfWidth + Gate.OffsetFromEdge;
            float springline = Gate.FootHeight + Gate.PillarHeight;

            Transform group = SceneryBuilder.Group(parent, "Portal CANDY RUSH", sample.Position,
                SceneryBuilder.Yaw(path.HeadingAt(0)));

            // Altura do arco a uma fração do vão (0 = topo, 1 = pilar): a elipse do arco achatado.
            float ArchHeightAt(float fraction) =>
                springline + Gate.ArchRise * Mathf.Sqrt(Mathf.Max(0f, 1f - fraction * fraction));

            Material cane = CandyMaterials.Textured(ProceduralTextures.Stripe(ProceduralTextures.Hex(0xff5c96), Color.white), 0.22f);
            Material frosting = CandyMaterials.Candy(Gate.Frosting, 0.25f);
            Material hotPink = CandyMaterials.Glow(Gate.HotPink, 0.06f);
            Material gold = CandyMaterials.Glow(Gate.Gold, 0.12f);

            BuildPillars(group, halfSpan, cane, frosting, hotPink);

            Transform arch = SceneryBuilder.Part(group, "Arco",
                ProceduralMeshes.Torus(halfSpan, Gate.ArchThickness, 14, 44, Mathf.PI), cane,
                new Vector3(0f, springline, 0f));
            arch.localScale = new Vector3(1f, Gate.ArchRise / halfSpan, 1f);

            BuildDecorations(group, halfSpan, ArchHeightAt);
            BuildSign(group, ArchHeightAt(0f), frosting, gold);
        }

        /// <summary>Os dois pilares de bengala, com pé alargado, bloco no chão e bandeira no topo.</summary>
        private static void BuildPillars(Transform group, float halfSpan, Material cane, Material frosting, Material hotPink)
        {
            Material flag = CandyMaterials.DoubleSided(CandyMaterials.Textured(
                ProceduralTextures.Checker(Gate.FlagSquares, ProceduralTextures.Hex(0x1d1d26), Color.white), 0.4f));

            foreach (int side in new[] { 1, -1 })
            {
                float x = side * halfSpan;

                SceneryBuilder.Part(group, "Base", ProceduralMeshes.RoundedBox(new Vector3(Gate.PlinthSize, Gate.PlinthHeight, Gate.PlinthSize), 0.22f),
                    hotPink, new Vector3(x, Gate.PlinthHeight / 2f, 0f));
                SceneryBuilder.Part(group, "Pé", ProceduralMeshes.Cylinder(Gate.PillarRadius * 1.1f, Gate.FootRadius, Gate.FootHeight),
                    frosting, new Vector3(x, Gate.PlinthHeight + Gate.FootHeight / 2f, 0f));
                SceneryBuilder.Part(group, "Pilar", ProceduralMeshes.Cylinder(Gate.PillarRadius, Gate.PillarRadius * 1.08f, Gate.PillarHeight),
                    cane, new Vector3(x, Gate.FootHeight + Gate.PillarHeight / 2f, 0f));

                float top = Gate.FootHeight + Gate.PillarHeight;
                SceneryBuilder.Part(group, "Mastro", ProceduralMeshes.Cylinder(Gate.FlagPoleRadius, Gate.FlagPoleRadius, Gate.FlagPoleHeight, 10),
                    frosting, new Vector3(x, top + Gate.FlagPoleHeight / 2f, 0f));
                SceneryBuilder.Part(group, "Bolinha", ProceduralMeshes.Primitive(PrimitiveType.Sphere), hotPink,
                    new Vector3(x, top + Gate.FlagPoleHeight, 0f), Quaternion.identity, Vector3.one * Gate.FlagPoleRadius * 4.8f);
                SceneryBuilder.Part(group, "Bandeira", ProceduralMeshes.Primitive(PrimitiveType.Quad), flag,
                    new Vector3(x + side * Gate.FlagWidth / 2f, top + Gate.FlagPoleHeight - Gate.FlagHeight * 0.7f, 0f),
                    Quaternion.identity, new Vector3(Gate.FlagWidth, Gate.FlagHeight, 1f));
            }
        }

        /// <summary>Pirulitos, nuvens de rosto feliz e estrelas, encostados no arco.</summary>
        private static void BuildDecorations(Transform group, float halfSpan, System.Func<float, float> archHeightAt)
        {
            Material lollipop = CandyMaterials.DoubleSided(CandyMaterials.Textured(ProceduralTextures.Swirl(), 0.16f));
            Material star = CandyMaterials.DoubleSided(CandyMaterials.Glow(Gate.Star, 0.18f));
            float front = -Gate.ArchThickness - 0.25f;

            foreach (int side in new[] { 1, -1 })
            {
                SceneryBuilder.Part(group, "Pirulito",
                    ProceduralMeshes.Cylinder(Gate.LollipopRadius, Gate.LollipopRadius, Gate.LollipopThickness, 28), lollipop,
                    new Vector3(side * halfSpan * Gate.LollipopAt, archHeightAt(Gate.LollipopAt), front),
                    SceneryBuilder.Euler(Mathf.PI / 2f, 0f, 0f), Vector3.one);

                BuildCloud(group, side * halfSpan * Gate.CloudAt, archHeightAt(Gate.CloudAt), front);

                SceneryBuilder.Part(group, "Estrela", ProceduralMeshes.Star(Gate.StarOuterRadius, Gate.StarInnerRadius), star,
                    new Vector3(side * halfSpan * Gate.StarAt, archHeightAt(Gate.StarAt), front - Gate.CloudRadius - 0.8f),
                    SceneryBuilder.Euler(0f, 0f, side * 0.25f), Vector3.one, castShadows: false);
            }
        }

        /// <summary>
        /// Nuvenzinha de marshmallow com rosto feliz. Os olhos são meio toro com a
        /// abertura para baixo (o "^" de olho fechado sorrindo); a boca é o mesmo meio toro virado.
        /// </summary>
        private static void BuildCloud(Transform group, float x, float y, float z)
        {
            float r = Gate.CloudRadius;
            Material cloud = CandyMaterials.Glow(Gate.Cloud, 0.1f);
            Material face = CandyMaterials.Candy(Gate.Face, 0.4f);
            Material cheek = CandyMaterials.Candy(Gate.CheekColor, 0.4f);
            Mesh sphere = ProceduralMeshes.Primitive(PrimitiveType.Sphere);

            (float px, float py, float scale)[] puffs =
            {
                (0f, 0f, 1f), (-r * 0.78f, -r * 0.22f, 0.72f), (r * 0.8f, -r * 0.18f, 0.68f), (-r * 0.16f, r * 0.58f, 0.6f),
            };
            foreach ((float px, float py, float scale) in puffs)
                SceneryBuilder.Part(group, "Nuvem", sphere, cloud, new Vector3(x + px, y + py, z), Quaternion.identity, Vector3.one * r * scale * 2f);

            float faceZ = z - r * 0.92f;
            Mesh halfRing = ProceduralMeshes.Torus(r * 0.17f, r * 0.045f, 6, 12, Mathf.PI);
            foreach (int side in new[] { 1, -1 })
            {
                SceneryBuilder.Part(group, "Olho", halfRing, face, new Vector3(x + side * r * 0.32f, y + r * 0.08f, faceZ), false);
                SceneryBuilder.Part(group, "Bochecha", sphere, cheek, new Vector3(x + side * r * 0.52f, y - r * 0.12f, faceZ),
                    Quaternion.identity, new Vector3(r * 0.26f, r * 0.26f, r * 0.26f * 0.35f), false);
            }
            SceneryBuilder.Part(group, "Boca", ProceduralMeshes.Torus(r * 0.19f, r * 0.05f, 6, 14, Mathf.PI), face,
                new Vector3(x, y - r * 0.18f, faceZ), SceneryBuilder.Euler(0f, 0f, Mathf.PI), Vector3.one, false);
        }

        /// <summary>Letreiro "CANDY RUSH", coroa em cima e semáforo pendurado embaixo.</summary>
        private static void BuildSign(Transform group, float archTop, Material frosting, Material gold)
        {
            float width = Gate.SignWidth;
            float height = width / 4f;
            float centerY = archTop + Gate.SignAboveArch + height / 2f;
            float signZ = -Gate.ArchThickness - 0.5f;

            SceneryBuilder.Part(group, "Moldura do letreiro", ProceduralMeshes.RoundedBox(new Vector3(width + 0.7f, height + 0.7f, 0.45f), 0.3f),
                frosting, new Vector3(0f, centerY, signZ + 0.28f));

            Material board = CandyMaterials.Textured(ProceduralTextures.SignBackground(ProceduralTextures.Hex(0xff4f93)), 0.35f);
            CandyMaterials.SetGlow(board, ProceduralTextures.Hex(0xff4f93), 0.3f);
            SceneryBuilder.Part(group, "Letreiro", ProceduralMeshes.Primitive(PrimitiveType.Quad), board,
                new Vector3(0f, centerY, signZ), Quaternion.identity, new Vector3(width, height, 1f), false);

            BuildSignText(group, new Vector3(0f, centerY, signZ - 0.03f));

            // --- Coroa ---
            float crownY = centerY + height / 2f + Gate.CrownBandHeight / 2f;
            SceneryBuilder.Part(group, "Coroa", ProceduralMeshes.Cylinder(Gate.CrownBandRadius, Gate.CrownBandRadius, Gate.CrownBandHeight, 18),
                gold, new Vector3(0f, crownY, signZ + 0.2f));
            foreach (int spike in new[] { -1, 0, 1 })
            {
                float tall = spike == 0 ? 1.35f : 1f;
                float spikeX = spike * Gate.CrownBandRadius * 0.72f;
                SceneryBuilder.Part(group, "Ponta da coroa", ProceduralMeshes.Cone(Gate.CrownSpikeRadius, Gate.CrownSpikeHeight * tall, 10), gold,
                    new Vector3(spikeX, crownY + Gate.CrownSpikeHeight * tall / 2f + Gate.CrownBandHeight / 2f, signZ + 0.2f));
                SceneryBuilder.Part(group, "Pérola", ProceduralMeshes.Primitive(PrimitiveType.Sphere), gold,
                    new Vector3(spikeX, crownY + Gate.CrownSpikeHeight * tall + Gate.CrownBandHeight / 2f, signZ + 0.2f),
                    Quaternion.identity, Vector3.one * Gate.CrownSpikeRadius * 1.1f);
            }

            // --- Semáforo de largada ---
            float barWidth = Gate.LightSpacing * Gate.LightCount + 0.8f;
            float barY = centerY - height / 2f - Gate.LightBelowSign - Gate.LightBarHeight / 2f;
            SceneryBuilder.Part(group, "Semáforo", ProceduralMeshes.RoundedBox(new Vector3(barWidth, Gate.LightBarHeight, Gate.LightBarDepth), 0.2f),
                CandyMaterials.Candy(Gate.LightBar, 0.4f), new Vector3(0f, barY, signZ + 0.1f));

            Material bulb = CandyMaterials.Glow(Gate.LightOn, 0.9f);
            for (int i = 0; i < Gate.LightCount; i++)
            {
                SceneryBuilder.Part(group, "Luz", ProceduralMeshes.Primitive(PrimitiveType.Sphere), bulb,
                    new Vector3((i - (Gate.LightCount - 1) / 2f) * Gate.LightSpacing, barY, signZ - Gate.LightBarDepth * 0.35f),
                    Quaternion.identity, new Vector3(Gate.LightRadius * 2f, Gate.LightRadius * 2f, Gate.LightRadius), false);
            }
        }

        /// <summary>
        /// O texto em 3D: branco com contorno rosa-escuro. O contorno são oito
        /// cópias deslocadas atrás do texto principal (o TextMesh não tem contorno).
        /// </summary>
        private static void BuildSignText(Transform group, Vector3 position)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;
            Material material = CandyMaterials.Text(font);
            // A fonte é dinâmica: quando a Unity refaz o atlas de letras, a textura muda.
            Font.textureRebuilt += rebuilt =>
            {
                if (rebuilt == font && material != null) material.mainTexture = font.material.mainTexture;
            };

            var root = SceneryBuilder.Group(group, "Texto CANDY RUSH", position, Quaternion.identity);
            const float outline = 0.07f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                AddText(root, font, material, new Vector3(Mathf.Cos(angle) * outline, Mathf.Sin(angle) * outline, 0.01f),
                    ProceduralTextures.Hex(0xb8245f));
            }
            AddText(root, font, material, Vector3.zero, Color.white);
        }

        private static void AddText(Transform parent, Font font, Material material, Vector3 offset, Color color)
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            var text = go.AddComponent<TextMesh>();
            text.text = Gate.SignText;
            text.font = font;
            text.fontSize = 120;
            text.fontStyle = FontStyle.Bold;
            text.characterSize = 0.1f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
