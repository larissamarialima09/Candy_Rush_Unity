using System;
using System.Collections.Generic;
using CandyRush.Config;
using CandyRush.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace CandyRush.Track
{
    /// <summary>
    /// Tudo o que se VÊ da pista, montado por código a partir do CircuitPath
    /// (convertido de src/track/circuitSurface.ts, terrain.ts e da barreira de
    /// src/track/circuitScenery.ts).
    ///
    /// Puramente visual: a física consulta o CircuitPath diretamente.
    /// Como tudo sai da mesma função de altura, o que se vê e o que se dirige batem.
    /// </summary>
    public static class TrackView
    {
        /// <summary>Monta a pista inteira como filhos de parent.</summary>
        public static void Build(CircuitPath path, Transform parent)
        {
            var root = new GameObject("Pista").transform;
            root.SetParent(parent, false);

            BuildSurface(path, root);
            BuildTerrain(path, root);
            BuildHorizon(root);
            BuildBarriers(path, root);
        }

        // ------------------------------------------------------------------
        // Piso: escape, asfalto, zebras, linha central, largada
        // ------------------------------------------------------------------

        private static void BuildSurface(CircuitPath path, Transform root)
        {
            float Half(int i) => path.Samples[i].HalfWidth;
            float runoff = TrackConfig.RunoffWidth;
            const float runoffBleed = 0.8f;

            Material runoffMaterial = CandyMaterials.Ribbon(null, 0.55f, 5f);
            Material asphaltMaterial = CandyMaterials.Ribbon(ProceduralTextures.CandyTrack(), 0.42f, -2f);
            Material paintedMaterial = CandyMaterials.Ribbon(null, 0.4f, -3f);

            foreach (int side in new[] { 1, -1 })
            {
                AddRibbon(path, root, "Escape", runoffMaterial, CircuitConfig.Layers.Runoff,
                    i => Mathf.Max(side * (Half(i) + runoff), side * Half(i)),
                    i => Mathf.Min(side * (Half(i) + runoff), side * Half(i)),
                    i => CircuitConfig.Colors.Runoff);

                AddRibbon(path, root, "Escape (borda)", runoffMaterial, CircuitConfig.Layers.Runoff - 0.012f,
                    i => Mathf.Max(side * (Half(i) + runoff + runoffBleed), side * (Half(i) + runoff)),
                    i => Mathf.Min(side * (Half(i) + runoff + runoffBleed), side * (Half(i) + runoff)),
                    i => CircuitConfig.Colors.Runoff);
            }

            // Branco: a textura de confete manda na cor.
            AddRibbon(path, root, "Asfalto", asphaltMaterial, CircuitConfig.Layers.Asphalt,
                i => Half(i), i => -Half(i), i => Color.white);

            // Zebra quadriculada nas DUAS bordas, pela volta inteira, com a mesma fase.
            Color KerbColor(int i) => (i / CircuitConfig.Kerb.StripeSegments) % 2 == 0
                ? CircuitConfig.Colors.KerbA
                : CircuitConfig.Colors.KerbB;
            foreach (int side in new[] { 1, -1 })
            {
                AddRibbon(path, root, "Zebra", paintedMaterial, CircuitConfig.Layers.Kerb,
                    i => Mathf.Max(side * (Half(i) + CircuitConfig.Kerb.Width), side * Half(i)),
                    i => Mathf.Min(side * (Half(i) + CircuitConfig.Kerb.Width), side * Half(i)),
                    KerbColor);
            }

            float halfLine = CircuitConfig.CenterLineWidth / 2f;
            AddRibbon(path, root, "Linha central", paintedMaterial, CircuitConfig.Layers.EdgeLine,
                i => halfLine, i => -halfLine, i => CircuitConfig.Colors.CenterLine);

            BuildStartLine(path, root, paintedMaterial);
        }

        private static void AddRibbon(CircuitPath path, Transform root, string name, Material material, float y,
            Func<int, float> leftEdge, Func<int, float> rightEdge, Func<int, Color> color)
        {
            var builder = new MeshBuilder();
            int count = path.Count;
            float repeat = CircuitConfig.TextureRepeatMeters;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector3 a = path.PointAt(i, leftEdge(i), y);
                Vector3 b = path.PointAt(i, rightEdge(i), y);
                Vector3 c = path.PointAt(next, leftEdge(next), y);
                Vector3 d = path.PointAt(next, rightEdge(next), y);

                // V acompanha a distância percorrida: a textura não estica nas curvas.
                float vStart = path.Samples[i].Distance / repeat;
                float vEnd = vStart + Vector3.Distance(a, c) / repeat;
                builder.Quad(a, b, c, d, color(i), vStart, vEnd);
            }

            AddMesh(root, name, builder.Build(name), material, castShadows: false);
        }

        /// <summary>Faixa quadriculada de largada, construída sobre as amostras (acompanha a inclinação).</summary>
        private static void BuildStartLine(CircuitPath path, Transform root, Material material)
        {
            var builder = new MeshBuilder();
            float y = CircuitConfig.Layers.StartLine;
            float spacing = path.TotalLength / path.Count;
            int rows = Mathf.Max(2, Mathf.RoundToInt(CircuitConfig.StartLine.Depth / spacing));
            int squares = CircuitConfig.StartLine.Squares;

            for (int row = 0; row < rows; row++)
            {
                int near = row % path.Count;
                int far = (row + 1) % path.Count;
                float halfNear = path.Samples[near].HalfWidth;
                float halfFar = path.Samples[far].HalfWidth;

                for (int column = 0; column < squares; column++)
                {
                    float from = 1f - column * 2f / squares;
                    float to = 1f - (column + 1) * 2f / squares;
                    bool dark = (row + column) % 2 == 0;

                    builder.Quad(
                        path.PointAt(near, halfNear * from, y),
                        path.PointAt(near, halfNear * to, y),
                        path.PointAt(far, halfFar * from, y),
                        path.PointAt(far, halfFar * to, y),
                        dark ? CircuitConfig.Colors.StartLineB : CircuitConfig.Colors.StartLineA);
                }
            }

            AddMesh(root, "Largada", builder.Build("Largada"), material, castShadows: false);
        }

        // ------------------------------------------------------------------
        // Terreno
        // ------------------------------------------------------------------

        /// <summary>
        /// Malha do terreno ao redor do circuito. Células cobertas pela pista NÃO
        /// são geradas, e as da borda mergulham por baixo da fita: assim o
        /// terreno nunca atravessa o asfalto.
        /// </summary>
        private static void BuildTerrain(CircuitPath path, Transform root)
        {
            float margin = TrackConfig.Terrain.FadeEnd;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            foreach (PathSample sample in path.Samples)
            {
                minX = Mathf.Min(minX, sample.Position.x);
                maxX = Mathf.Max(maxX, sample.Position.x);
                minZ = Mathf.Min(minZ, sample.Position.z);
                maxZ = Mathf.Max(maxZ, sample.Position.z);
            }
            minX -= margin;
            maxX += margin;
            minZ -= margin;
            maxZ += margin;

            float cell = TrackConfig.Terrain.CellSize;
            int columns = Mathf.CeilToInt((maxX - minX) / cell) + 1;
            int rows = Mathf.CeilToInt((maxZ - minZ) / cell) + 1;

            var positions = new Vector3[columns * rows];
            var uvs = new Vector2[columns * rows];
            var covered = new float[columns * rows];
            const float tileMeters = 26f;
            // Profundidade máxima do mergulho sob a fita, e em quantos metros ela é atingida.
            const float maxSink = 0.9f;
            const float sinkRamp = 2.5f;
            float coverageMargin = Mathf.Min(cell * 0.45f, 1.4f);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int index = r * columns + c;
                    float x = minX + c * cell;
                    float z = minZ + r * cell;

                    float inside = TrackCoverage(path, x, z, coverageMargin);
                    covered[index] = inside;
                    float sink = inside <= 0f ? 0f : Mathf.Min(1f, inside / sinkRamp) * maxSink;

                    positions[index] = new Vector3(x, path.TerrainHeight(x, z) - sink, z);
                    uvs[index] = new Vector2(x / tileMeters, z / tileMeters);
                }
            }

            var triangles = new List<int>();
            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < columns - 1; c++)
                {
                    int a = r * columns + c;
                    int b = a + 1;
                    int d = a + columns;
                    int e = d + 1;

                    // Só some quando os QUATRO cantos (e o centro) estão sob a fita.
                    float centerX = minX + (c + 0.5f) * cell;
                    float centerZ = minZ + (r + 0.5f) * cell;
                    float coverage = Mathf.Max(Mathf.Max(covered[a], covered[b]), Mathf.Max(covered[d], covered[e]));
                    coverage = Mathf.Max(coverage, TrackCoverage(path, centerX, centerZ, coverageMargin));
                    if (coverage > 0f) continue;

                    // Enrolamento horário visto de cima (face da Unity virada para o céu).
                    triangles.Add(a);
                    triangles.Add(d);
                    triangles.Add(e);
                    triangles.Add(a);
                    triangles.Add(e);
                    triangles.Add(b);
                }
            }

            var mesh = new Mesh { name = "Terreno", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = positions;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Material material = CandyMaterials.Candy(CircuitConfig.Colors.Grass, 0.92f);
            material.SetTexture("_MainTex", ProceduralTextures.Meadow());
            material.SetFloat("_OffsetFactor", 4f);
            material.SetFloat("_OffsetUnits", 4f);
            AddMesh(root, "Terreno", mesh, material, castShadows: false);
        }

        /// <summary>Quanto um ponto está DENTRO da fita da pista (+ escape + zebra), em metros.</summary>
        private static float TrackCoverage(CircuitPath path, float x, float z, float margin)
        {
            int index = path.NearestSampleIndex(x, z);
            float lateral = path.LateralOffset(index, x, z);
            float edge = path.Samples[index].HalfWidth + TrackConfig.RunoffWidth + CircuitConfig.Kerb.Width + margin;
            return edge - Mathf.Abs(lateral);
        }

        /// <summary>Plano gigante de grama até o horizonte, um pouco abaixo do terreno.</summary>
        private static void BuildHorizon(Transform root)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Horizonte";
            UnityEngine.Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.transform.SetParent(root, false);
            plane.transform.localPosition = new Vector3(0f, CircuitConfig.HorizonPlaneHeight, 0f);
            // O Plane da Unity mede 10 x 10 m.
            float scale = CircuitConfig.HorizonPlaneSize / 10f;
            plane.transform.localScale = new Vector3(scale, 1f, scale);

            Material material = CandyMaterials.Candy(CircuitConfig.Colors.Grass, 0.92f);
            material.SetTexture("_MainTex", ProceduralTextures.Meadow());
            material.SetTextureScale("_MainTex", Vector2.one * (CircuitConfig.HorizonPlaneSize / 20f));
            var renderer = plane.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------
        // Barreiras
        // ------------------------------------------------------------------

        /// <summary>
        /// Barreira de bala: blocos arredondados nos pastéis do jogo, nas DUAS
        /// bordas e pela volta inteira, na MESMA distância que a barreira física
        /// (TrackConfig.Barriers). Barreira que se vê e que se sente são a mesma.
        /// Os blocos são fundidos numa malha por cor, para desenhar rápido.
        /// </summary>
        private static void BuildBarriers(CircuitPath path, Transform root)
        {
            var palette = new Color[FeaturesConfig.Pastels.Length + 2];
            for (int i = 0; i < FeaturesConfig.Pastels.Length; i++) palette[i] = FeaturesConfig.Pastels[i];
            palette[palette.Length - 2] = Color.white;
            palette[palette.Length - 1] = CircuitConfig.Hex(0xff9ec4);
            var materials = new Material[palette.Length];
            for (int i = 0; i < palette.Length; i++) materials[i] = CandyMaterials.Candy(palette[i], 0.18f);

            float radius = CircuitConfig.TireWall.Radius;
            Mesh block = ProceduralMeshes.RoundedBox(
                new Vector3(radius * 2.1f, radius * 2.1f, CircuitConfig.TireWall.Height + radius * 2f), radius * 0.5f, 3);

            var batch = new MeshBatch();
            int index = 0;
            TrackPlacement.Along(path, CircuitConfig.TireWall.Spacing, (sample, i) =>
            {
                Material material = materials[index++ % materials.Length];
                foreach (int side in new[] { 1, -1 })
                {
                    float lateral = side * (sample.HalfWidth + TrackConfig.Barriers.OffsetFromEdge);
                    batch.Add(block, material, TrackPlacement.At(path, i, lateral, 0f, radius), TrackPlacement.Facing(path, i), Vector3.one);
                }
            });
            batch.Build(root, "Barreira de bala");
        }

        private static void AddMesh(Transform root, string name, Mesh mesh, Material material, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }
    }
}
