using CandyRush.Config;
using CandyRush.Rendering;
using UnityEngine;
using Donut = CandyRush.Config.FeaturesConfig.DonutTunnel;

namespace CandyRush.Track
{
    /// <summary>
    /// O Túnel de Donut: um donut gigante em pé sobre a pista, com cobertura
    /// rosa escorrida e granulado (convertido de addDonutTunnel e
    /// addDonutSprinkles em src/track/circuitScenery.ts). Só visual.
    ///
    /// O donut fica no plano XY local (furo ao longo de Z = sentido de corrida),
    /// então o kart passa pelo buraco.
    /// </summary>
    public static class DonutTunnel
    {
        public static void Build(CircuitPath path, Transform parent, int seed = 1337)
        {
            int index = path.IndexAtFraction(Donut.At);
            PathSample sample = path.Samples[index];
            float ringRadius = Donut.HoleRadius + Donut.TubeRadius;

            Vector3 basePosition = sample.Position;
            basePosition.y = path.SurfaceHeight(index, 0f);
            Transform group = SceneryBuilder.Group(parent, "Túnel de Donut", basePosition, SceneryBuilder.Yaw(path.HeadingAt(index)));

            Material dough = CandyMaterials.Coat(CandyMaterials.Glow(Donut.Dough, 0.03f), Donut.DoughTop, Donut.DoughBottom, 0.04f);
            dough.SetFloat("_Glossiness", 0.45f * 0.8f);
            Material icing = CandyMaterials.DoubleSided(CandyMaterials.Coat(CandyMaterials.Glow(Donut.Icing, 0.07f), Donut.IcingTop, Donut.IcingBottom, 0.11f));
            icing.SetFloat("_Glossiness", 0.88f * 0.8f);

            SceneryBuilder.Part(group, "Massa", ProceduralMeshes.Torus(ringRadius, Donut.TubeRadius, Donut.TubeSegments, Donut.RingSegments),
                dough, new Vector3(0f, Donut.CenterHeight, 0f));

            // Até onde a cobertura desce, por ponto do anel. A ondulação é o escorrido:
            // sem ela a borda do glacê seria um círculo perfeito.
            float IcingSpan(float u) => Donut.FrostingSpan + Mathf.Sin(u * Donut.FrostingWaves) * Donut.FrostingWaveAmplitude;

            SceneryBuilder.Part(group, "Cobertura",
                ProceduralMeshes.TorusShell(ringRadius, Donut.TubeRadius + Donut.FrostingThickness, IcingSpan, Donut.RingSegments, Donut.TubeSegments),
                icing, new Vector3(0f, Donut.CenterHeight, 0f));

            BuildSprinkles(group, ringRadius, IcingSpan, seed);
        }

        /// <summary>
        /// Granulado grudado na cobertura: cada barrinha DEITADA na superfície
        /// (eixo curto alinhado com a normal do toro) e girada ao acaso no plano tangente.
        /// </summary>
        private static void BuildSprinkles(Transform group, float ringRadius, System.Func<float, float> icingSpan, int seed)
        {
            var random = new System.Random(seed);
            float surfaceRadius = Donut.TubeRadius + Donut.FrostingThickness + 0.05f;

            var palette = new Material[FeaturesConfig.Pastels.Length + 1];
            for (int i = 0; i < FeaturesConfig.Pastels.Length; i++) palette[i] = CandyMaterials.Candy(FeaturesConfig.Pastels[i], 0.2f);
            palette[palette.Length - 1] = CandyMaterials.Candy(Color.white, 0.2f);

            Mesh sprinkle = ProceduralMeshes.RoundedBox(
                new Vector3(Donut.SprinkleLength, Donut.SprinkleThickness, Donut.SprinkleThickness), Donut.SprinkleThickness * 0.45f, 2);

            Transform holder = SceneryBuilder.Group(group, "Granulado", new Vector3(0f, Donut.CenterHeight, 0f), Quaternion.identity);
            var batch = new MeshBatch();
            for (int i = 0; i < Donut.SprinkleCount; i++)
            {
                float u = (float)random.NextDouble() * Mathf.PI * 2f;
                float v = ((float)random.NextDouble() * 2f - 1f) * icingSpan(u) * 0.85f;
                float radial = ringRadius + surfaceRadius * Mathf.Cos(v);

                var position = new Vector3(radial * Mathf.Cos(u), radial * Mathf.Sin(u), surfaceRadius * Mathf.Sin(v));
                var normal = new Vector3(Mathf.Cos(v) * Mathf.Cos(u), Mathf.Cos(v) * Mathf.Sin(u), Mathf.Sin(v)).normalized;

                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal)
                                      * Quaternion.AngleAxis((float)random.NextDouble() * 360f, Vector3.up);
                float scale = 0.85f + (float)random.NextDouble() * 0.45f;
                Material material = palette[random.Next(palette.Length)];
                batch.Add(sprinkle, material, position, rotation, Vector3.one * scale);
            }
            batch.Build(holder, "Granulado", castShadows: false);
        }
    }
}
