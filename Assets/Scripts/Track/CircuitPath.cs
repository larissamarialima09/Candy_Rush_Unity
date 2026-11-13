using System;
using System.Collections.Generic;
using CandyRush.Config;
using UnityEngine;

namespace CandyRush.Track
{
    /// <summary>Um ponto amostrado do eixo da pista, com o referencial local pronto.</summary>
    public readonly struct PathSample
    {
        /// <summary>Posição no eixo central, já com a elevação em y.</summary>
        public readonly Vector3 Position;
        /// <summary>Direção de corrida, unitária, INCLUINDO a inclinação da subida.</summary>
        public readonly Vector3 Tangent;
        /// <summary>Perpendicular HORIZONTAL apontando para a ESQUERDA do sentido de corrida.</summary>
        public readonly Vector3 Left;
        /// <summary>Normal da superfície, já com a inclinação da curva.</summary>
        public readonly Vector3 Normal;
        /// <summary>Curvatura com sinal, em 1/metro. Positiva = curva à esquerda.</summary>
        public readonly float Curvature;
        /// <summary>Distância acumulada desde a largada, em metros.</summary>
        public readonly float Distance;
        /// <summary>Meia-largura do asfalto neste ponto, em metros.</summary>
        public readonly float HalfWidth;
        /// <summary>Inclinação transversal, em radianos. Positiva = lado direito mais alto.</summary>
        public readonly float Bank;

        public PathSample(Vector3 position, Vector3 tangent, Vector3 left, Vector3 normal,
            float curvature, float distance, float halfWidth, float bank)
        {
            Position = position;
            Tangent = tangent;
            Left = left;
            Normal = normal;
            Curvature = curvature;
            Distance = distance;
            HalfWidth = halfWidth;
            Bank = bank;
        }
    }

    /// <summary>
    /// Eixo da pista amostrado uniformemente, fechado, com elevação e inclinação
    /// nas curvas (convertido de src/track/circuitPath.ts).
    ///
    /// É a ÚNICA fonte de verdade da geometria: malha, física, cenário,
    /// checkpoints e barreiras saem daqui. Também é o índice espacial usado
    /// pela física para achar a amostra mais próxima de um ponto.
    ///
    /// Diferença para a web: aqui Left é a esquerda de verdade na tela da
    /// Unity (que tem eixos canhotos). As fórmulas que dependem do lado
    /// (curvatura, normal) foram ajustadas para isso.
    /// </summary>
    public sealed class CircuitPath
    {
        /// <summary>Tamanho da célula do índice espacial, em metros.</summary>
        private const float GridCell = 12f;

        public readonly PathSample[] Samples;
        public readonly float TotalLength;

        private readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();

        public int Count => Samples.Length;

        public CircuitPath(IReadOnlyList<Vector2> points, int sampleCount, int curvatureWindow = 1)
        {
            var controlPoints = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++) controlPoints[i] = new Vector3(points[i].x, 0f, points[i].y);

            // Pontos igualmente espaçados por comprimento de arco: essencial para
            // espaçar barreiras e postes por metro, e não por parâmetro da curva.
            Vector3d[] spaced = new CatmullRomCurve(controlPoints).GetSpacedPoints(sampleCount);
            int count = sampleCount;
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++) positions[i] = spaced[i].ToVector3();

            // --- Distâncias no plano, para parametrizar os perfis ---
            var distances = new float[count];
            float planarLength = 0f;
            for (int i = 0; i < count; i++)
            {
                distances[i] = planarLength;
                planarLength += Vector3.Distance(positions[i], positions[(i + 1) % count]);
            }
            TotalLength = planarLength;

            // --- Elevação, normalizada para o ponto mais baixo ficar em y = 0 ---
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                float elevation = SampleProfile(TrackConfig.ElevationProfile, distances[i] / planarLength);
                positions[i].y = elevation;
                lowest = Mathf.Min(lowest, elevation);
            }
            for (int i = 0; i < count; i++) positions[i].y -= lowest;

            // --- Referencial local ---
            var tangents = new Vector3[count];
            var lefts = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 next = positions[(i + 1) % count];
                Vector3 previous = positions[(i - 1 + count) % count];
                tangents[i] = (next - previous).normalized;
                // A esquerda é sempre horizontal: inclinada, torceria a pista.
                Vector3 flatTangent = new Vector3(tangents[i].x, 0f, tangents[i].z).normalized;
                lefts[i] = Vector3.Cross(flatTangent, Vector3.up).normalized;
            }

            // --- Curvatura, medida no plano e com janela ---
            var curvatures = new float[count];
            int window = Mathf.Max(1, curvatureWindow);
            for (int i = 0; i < count; i++)
            {
                int before = (i - window + count * 2) % count;
                int after = (i + window) % count;
                float arc = Vector3.Distance(positions[before], positions[after]);

                Vector3 flatBefore = new Vector3(tangents[before].x, 0f, tangents[before].z).normalized;
                Vector3 flatAfter = new Vector3(tangents[after].x, 0f, tangents[after].z).normalized;
                float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(flatBefore, flatAfter), -1f, 1f));
                // Nos eixos da Unity, curva à esquerda dá produto vetorial com y NEGATIVO.
                float sign = -Core.MathUtils.SignOrZero(Vector3.Cross(flatBefore, flatAfter).y);
                curvatures[i] = arc > 1e-6f ? angle * sign / arc : 0f;
            }

            // --- Inclinação: derivada da curvatura e depois alisada ---
            var rawBank = new float[count];
            for (int i = 0; i < count; i++)
            {
                float degrees = Mathf.Clamp(curvatures[i] * TrackConfig.Banking.DegreesPerCurvature,
                    -TrackConfig.Banking.MaxDegrees, TrackConfig.Banking.MaxDegrees);
                rawBank[i] = degrees * Mathf.Deg2Rad;
            }
            float[] bank = SmoothCyclic(rawBank, TrackConfig.Banking.SmoothingSamples);

            // --- Montagem final ---
            Samples = new PathSample[count];
            for (int i = 0; i < count; i++)
            {
                // Direção lateral SOBRE a superfície: a inclinação baixa o lado de dentro da curva.
                Vector3 lateralDir = (lefts[i] - Vector3.up * Mathf.Tan(bank[i])).normalized;
                Vector3 normal = Vector3.Cross(lateralDir, tangents[i]).normalized;

                Samples[i] = new PathSample(positions[i], tangents[i], lefts[i], normal,
                    curvatures[i], distances[i], TrackConfig.HalfWidth, bank[i]);
            }

            BuildGrid();
        }

        private void BuildGrid()
        {
            for (int i = 0; i < Samples.Length; i++)
            {
                Vector3 position = Samples[i].Position;
                long key = CellKey(position.x, position.z);
                if (!_grid.TryGetValue(key, out List<int> bucket))
                {
                    bucket = new List<int>();
                    _grid[key] = bucket;
                }
                bucket.Add(i);
            }
        }

        /// <summary>Índice da amostra mais próxima de um ponto do mundo (consulta as 9 células vizinhas).</summary>
        public int NearestSampleIndex(float x, float z)
        {
            int cx = Mathf.FloorToInt(x / GridCell);
            int cz = Mathf.FloorToInt(z / GridCell);

            int best = -1;
            float bestDistance = float.PositiveInfinity;

            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    if (!_grid.TryGetValue(HashCell(cx + ox, cz + oz), out List<int> bucket)) continue;
                    foreach (int index in bucket)
                    {
                        Vector3 position = Samples[index].Position;
                        float dx = position.x - x;
                        float dz = position.z - z;
                        float squared = dx * dx + dz * dz;
                        if (squared >= bestDistance) continue;
                        bestDistance = squared;
                        best = index;
                    }
                }
            }

            if (best >= 0) return best;

            // Longe de tudo (saiu voando do mapa): varredura completa.
            for (int i = 0; i < Samples.Length; i++)
            {
                Vector3 position = Samples[i].Position;
                float dx = position.x - x;
                float dz = position.z - z;
                float squared = dx * dx + dz * dz;
                if (squared >= bestDistance) continue;
                bestDistance = squared;
                best = i;
            }
            return best < 0 ? 0 : best;
        }

        /// <summary>Deslocamento lateral de um ponto: positivo para a esquerda da pista.</summary>
        public float LateralOffset(int index, float x, float z)
        {
            PathSample sample = Samples[index];
            return (x - sample.Position.x) * sample.Left.x + (z - sample.Position.z) * sample.Left.z;
        }

        /// <summary>
        /// Altura da superfície a um dado deslocamento lateral. A inclinação para
        /// na borda do asfalto, e a elevação some com a distância (terreno plano
        /// longe da pista). É a ÚNICA função de altura do jogo.
        /// </summary>
        public float SurfaceHeight(int index, float lateral)
        {
            PathSample sample = Samples[index];
            float paved = Mathf.Clamp(lateral, -sample.HalfWidth, sample.HalfWidth);
            float height = sample.Position.y - paved * Mathf.Tan(sample.Bank);

            float absolute = Mathf.Abs(lateral);
            if (absolute <= TrackConfig.Terrain.FadeStart) return height;
            if (absolute >= TrackConfig.Terrain.FadeEnd) return 0f;

            float k = (absolute - TrackConfig.Terrain.FadeStart) / (TrackConfig.Terrain.FadeEnd - TrackConfig.Terrain.FadeStart);
            return height * (1f - k * k * (3f - 2f * k));
        }

        /// <summary>Altura do terreno sob um ponto qualquer do mundo.</summary>
        public float TerrainHeight(float x, float z)
        {
            int index = NearestSampleIndex(x, z);
            return SurfaceHeight(index, LateralOffset(index, x, z));
        }

        /// <summary>Material da superfície em um deslocamento lateral.</summary>
        public SurfaceKind SurfaceAt(int index, float lateral)
        {
            PathSample sample = Samples[index];
            float absolute = Mathf.Abs(lateral);

            if (absolute <= sample.HalfWidth) return SurfaceKind.Asphalt;
            // A zebra está desenhada nas DUAS bordas, então vale nas duas.
            if (absolute <= sample.HalfWidth + CircuitConfig.Kerb.Width) return SurfaceKind.Kerb;
            if (absolute <= sample.HalfWidth + TrackConfig.RunoffWidth) return SurfaceKind.Sand;
            return SurfaceKind.Grass;
        }

        /// <summary>Ponto da superfície deslocado lateralmente, com a inclinação aplicada.</summary>
        public Vector3 PointAt(int index, float lateral, float heightOffset)
        {
            int wrapped = ((index % Samples.Length) + Samples.Length) % Samples.Length;
            PathSample sample = Samples[wrapped];
            Vector3 point = sample.Position + sample.Left * lateral;
            point.y = SurfaceHeight(wrapped, lateral) + heightOffset;
            return point;
        }

        /// <summary>Índice da amostra mais próxima de uma fração da volta (0..1).</summary>
        public int IndexAtFraction(float fraction)
        {
            int count = Samples.Length;
            int index = Mathf.RoundToInt(fraction * count) % count;
            return index < 0 ? index + count : index;
        }

        /// <summary>Rumo (rotação em Y, radianos) de uma amostra, para posicionar karts.</summary>
        public float HeadingAt(int index)
        {
            Vector3 tangent = Samples[index].Tangent;
            return Mathf.Atan2(tangent.x, tangent.z);
        }

        /// <summary>Distância horizontal de um ponto ao eixo da pista.</summary>
        public float DistanceToCenterline(float x, float z)
        {
            PathSample sample = Samples[NearestSampleIndex(x, z)];
            float dx = sample.Position.x - x;
            float dz = sample.Position.z - z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Interpola um perfil por fração da volta, de forma CÍCLICA e suavizada (smoothstep).</summary>
        private static float SampleProfile(TrackProfilePoint[] profile, float t)
        {
            int count = profile.Length;
            if (count == 0) return 0f;
            if (count == 1) return profile[0].Value;

            float wrapped = ((t % 1f) + 1f) % 1f;

            int previous = count - 1;
            for (int i = 0; i < count; i++)
            {
                if (profile[i].At <= wrapped) previous = i;
                else break;
            }
            int next = (previous + 1) % count;

            float from = profile[previous].At;
            float span = profile[next].At - from;
            if (span <= 0f) span += 1f;

            float local = wrapped - from;
            if (local < 0f) local += 1f;

            float k = Mathf.Clamp01(local / span);
            float smooth = k * k * (3f - 2f * k);
            return profile[previous].Value + (profile[next].Value - profile[previous].Value) * smooth;
        }

        /// <summary>Média móvel cíclica (alisa a inclinação das curvas).</summary>
        private static float[] SmoothCyclic(float[] values, int window)
        {
            int count = values.Length;
            int half = Math.Max(0, window / 2);
            if (half == 0) return values;

            var result = new float[count];
            for (int i = 0; i < count; i++)
            {
                float sum = 0f;
                for (int o = -half; o <= half; o++) sum += values[(i + o + count * 2) % count];
                result[i] = sum / (half * 2 + 1);
            }
            return result;
        }

        private static long CellKey(float x, float z) =>
            HashCell(Mathf.FloorToInt(x / GridCell), Mathf.FloorToInt(z / GridCell));

        private static long HashCell(int cx, int cz) => (cx + 4096L) * 8192L + (cz + 4096L);
    }
}
