using System;
using System.Collections.Generic;
using CandyRush.Config;
using CandyRush.Core;
using CandyRush.Rendering;
using UnityEngine;
using Castle = CandyRush.Config.CastleConfig;

namespace CandyRush.Track
{
    /// <summary>
    /// O "Doce Castelo", com a pista passando por um túnel embaixo
    /// (convertido de src/track/candyCastle.ts).
    ///
    /// São centenas de peças (torres, glacê escorrido, gomas, janelas, jujubas
    /// da escada). Como na web, tudo que usa o mesmo material é FUNDIDO numa
    /// malha só (MeshBatch): o castelo inteiro vira umas duas dúzias de malhas.
    /// Os detalhes miúdos ficam numa malha que não projeta sombra.
    ///
    /// ESPELHAMENTO: as medidas são as da web (X local = esquerda da pista).
    /// As peças são montadas num grupo com escala X = -1, o que põe o castelo
    /// exatamente do mesmo lado da pista que na versão web. Os letreiros ficam
    /// FORA desse grupo, para o texto não sair invertido.
    ///
    /// Nada aqui tem colisão: a barreira de bala continua valendo dentro do
    /// túnel, e o túnel é largo o bastante para ficar atrás dela.
    /// </summary>
    public sealed class CandyCastle
    {
        private enum M
        {
            Ginger, Icing, Pink, HotPink, Blue, Orange, Peach, Mint, Yellow, Lavender,
            Window, Gold, HoopPink, HoopWhite, CaneRoof, CaneBody, CaneTrim, Swirl,
        }

        /// <summary>Cores de marshmallow e goma, na ordem em que se alternam.</summary>
        private static readonly M[] CandyColors = { M.Pink, M.Icing, M.Blue, M.Yellow, M.Lavender, M.Mint, M.Peach };
        private static readonly M[] GumdropColors = { M.HotPink, M.Blue, M.Yellow, M.Mint, M.Lavender, M.Orange };

        /// <summary>Referencial do castelo: origem na boca do túnel, eixos horizontais.</summary>
        public readonly struct Frame
        {
            public readonly Vector3 Origin;
            /// <summary>X local da web: esquerda da pista.</summary>
            public readonly Vector3 Left;
            /// <summary>Z local: sentido de corrida.</summary>
            public readonly Vector3 Forward;
            public readonly float Heading;
            public readonly int EntryIndex;

            public Frame(Vector3 origin, Vector3 left, Vector3 forward, float heading, int entryIndex)
            {
                Origin = origin; Left = left; Forward = forward; Heading = heading; EntryIndex = entryIndex;
            }
        }

        private readonly CircuitPath _path;
        private readonly Frame _frame;
        private readonly Dictionary<M, Material> _materials;
        private readonly MeshBatch _solid = new MeshBatch();
        private readonly MeshBatch _details = new MeshBatch();
        private readonly Func<float> _random = MathUtils.CreateRandom(1337 + 2024);
        /// <summary>Meia-largura interna do túnel, da linha de centro até a parede.</summary>
        private readonly float _inner;
        private readonly List<Transform> _signs = new List<Transform>();
        private float _time;

        public CandyCastle(CircuitPath path, Transform parent)
        {
            _path = path;
            _frame = ComputeFrame(path);
            _materials = CreateMaterials();
            _inner = path.Samples[_frame.EntryIndex].HalfWidth + TrackConfig.Barriers.OffsetFromEdge + Castle.Tunnel.WallMargin;

            Transform root = SceneryBuilder.Group(parent, "Doce Castelo", _frame.Origin, SceneryBuilder.Yaw(_frame.Heading));
            Transform mirrored = SceneryBuilder.Group(root, "Peças (medidas da web)", Vector3.zero, Quaternion.identity);
            mirrored.localScale = new Vector3(-1f, 1f, 1f);

            BuildTunnel();
            BuildPortal(true);
            BuildPortal(false);
            BuildBells();
            BuildBody();
            foreach (Castle.Tower tower in Castle.Towers) BuildTower(tower);
            BuildStairs();
            BuildGarden();
            BuildHearts();

            _solid.Build(mirrored, "Castelo");
            _details.Build(mirrored, "Castelo (detalhes)", castShadows: false);
            BuildSigns(root);
        }

        /// <summary>
        /// O eixo do túnel segue a CORDA entre a entrada e a saída, e não a
        /// tangente na entrada (que, projetada por 30 m, errava a saída por mais de um metro).
        /// </summary>
        public static Frame ComputeFrame(CircuitPath path)
        {
            int entryIndex = path.IndexAtFraction(Castle.At);
            float metersPerSample = path.TotalLength / path.Count;
            int exitIndex = (entryIndex + Mathf.RoundToInt(Castle.Tunnel.Length / metersPerSample)) % path.Count;
            Vector3 entry = path.Samples[entryIndex].Position;
            Vector3 exit = path.Samples[exitIndex].Position;
            Vector3 forward = MathUtils.Flat(exit - entry).normalized;
            Vector3 left = Vector3.Cross(forward, Vector3.up).normalized;
            return new Frame(entry, left, forward, Mathf.Atan2(forward.x, forward.z), entryIndex);
        }

        /// <summary>"Este ponto do mundo cai dentro do castelo?" — para o cenário sorteado não atravessar parede.</summary>
        public static Func<float, float, float, bool> Footprint(CircuitPath path)
        {
            Frame frame = ComputeFrame(path);
            return (x, z, margin) =>
            {
                float dx = x - frame.Origin.x, dz = z - frame.Origin.z;
                float localX = dx * frame.Left.x + dz * frame.Left.z;
                float localZ = dx * frame.Forward.x + dz * frame.Forward.z;
                foreach (Rect area in Castle.Footprint)
                {
                    if (localX >= area.xMin - margin && localX <= area.xMax + margin &&
                        localZ >= area.yMin - margin && localZ <= area.yMax + margin) return true;
                }
                return false;
            };
        }

        /// <summary>Animação só visual (com o tempo do quadro): os letreiros pulsam de leve.</summary>
        public void Animate(float dt)
        {
            _time += dt;
            for (int i = 0; i < _signs.Count; i++)
            {
                float scale = 1f + Mathf.Sin(_time * Castle.Animation.SignPulseSpeed + i * 1.7f) * Castle.Animation.SignPulse;
                _signs[i].localScale = new Vector3(scale, scale, 1f);
            }
        }

        // ------------------------------------------------------------------ túnel

        /// <summary>
        /// O bloco sobre a pista: um retângulo com o arco recortado por baixo,
        /// extrudado ao longo do túnel. Por dentro, anéis rosa e brancos forram o arco.
        /// </summary>
        private void BuildTunnel()
        {
            float w = _inner;
            float wo = w + Castle.Tunnel.ShellThickness;
            float bottom = -Castle.BuriedDepth;

            var block = new List<Vector2>
            {
                new Vector2(-wo, bottom), new Vector2(-wo, Castle.TerraceHeight),
                new Vector2(wo, Castle.TerraceHeight), new Vector2(wo, bottom),
            };
            block.AddRange(ArchPoints(w, Castle.Tunnel.WallHeight, Castle.Tunnel.ArchRise, bottom, 28));
            Add(M.Ginger, ProceduralMeshes.Extrude(block, Castle.Tunnel.Length), Matrix4x4.identity);

            const float liner = 0.22f;
            var hoop = new List<Vector2>(ArchPoints(w, Castle.Tunnel.WallHeight, Castle.Tunnel.ArchRise, bottom, 28));
            List<Vector2> innerArch = ArchPoints(w - liner, Castle.Tunnel.WallHeight, Castle.Tunnel.ArchRise - liner, bottom, 28);
            innerArch.Reverse();
            hoop.AddRange(innerArch);

            int count = Mathf.Max(1, Mathf.RoundToInt(Castle.Tunnel.Length / Castle.Tunnel.HoopLength));
            float hoopLength = Castle.Tunnel.Length / count;
            Mesh hoopMesh = ProceduralMeshes.Extrude(hoop, hoopLength);
            for (int i = 0; i < count; i++)
                Add(i % 2 == 0 ? M.HoopPink : M.HoopWhite, hoopMesh, At(0f, 0f, i * hoopLength), false);
        }

        /// <summary>Moldura da boca do túnel: marshmallows seguindo o arco, friso de bengala e de glacê. A entrada ganha placa.</summary>
        private void BuildPortal(bool isFront)
        {
            float w = _inner;
            float depth = Castle.Tunnel.PortalDepth;
            float zCenter = isFront ? -depth / 2f : Castle.Tunnel.Length + depth / 2f;

            // Linha média do anel, medida por comprimento de arco: marshmallows todos do mesmo tamanho.
            float grow = Castle.Tunnel.ShellThickness / 2f + 0.1f;
            List<Vector2> middle = ArchPoints(w + grow, Castle.Tunnel.WallHeight, Castle.Tunnel.ArchRise + grow, -0.8f, 64);
            var lengths = new List<float> { 0f };
            for (int i = 1; i < middle.Count; i++) lengths.Add(lengths[i - 1] + Vector2.Distance(middle[i], middle[i - 1]));
            float total = lengths[lengths.Count - 1];

            Vector2 PointAt(float distance)
            {
                float s = Mathf.Clamp(distance, 0f, total);
                int k = 1;
                while (k < lengths.Count - 1 && lengths[k] < s) k++;
                float span = lengths[k] - lengths[k - 1];
                if (span <= 0f) span = 1f;
                return Vector2.Lerp(middle[k - 1], middle[k], (s - lengths[k - 1]) / span);
            }

            float piece = total / Castle.Tunnel.MarshmallowCount;
            for (int i = 0; i < Castle.Tunnel.MarshmallowCount; i++)
            {
                float s = (i + 0.5f) * piece;
                Vector2 p = PointAt(s), a = PointAt(s - 0.4f), b = PointAt(s + 0.4f);
                float angle = Mathf.Atan2(b.y - a.y, b.x - a.x);
                Add(CandyColors[i % CandyColors.Length],
                    ProceduralMeshes.RoundedBox(new Vector3(piece - 0.28f, Castle.Tunnel.ShellThickness + 0.5f, depth), 0.6f, 3),
                    At(p.x, p.y, zCenter, 0f, 0f, angle));
            }

            float outerGrow = Castle.Tunnel.ShellThickness + 0.45f;
            List<Vector2> outer = ArchPoints(w + outerGrow, Castle.Tunnel.WallHeight, Castle.Tunnel.ArchRise + outerGrow, -0.8f, 40);
            Add(M.CaneTrim, ProceduralMeshes.Tube(outer.ConvertAll(q => new Vector3(q.x, q.y, zCenter)), 0.38f, 8, 70), Matrix4x4.identity);

            float lip = isFront ? -depth - 0.04f : Castle.Tunnel.Length + depth + 0.04f;
            List<Vector2> innerTrim = ArchPoints(w - 0.05f, Castle.Tunnel.WallHeight, Castle.Tunnel.ArchRise - 0.05f, -0.8f, 40);
            Add(M.Icing, ProceduralMeshes.Tube(innerTrim.ConvertAll(q => new Vector3(q.x, q.y, lip)), 0.28f, 6, 60), Matrix4x4.identity);

            if (!isFront) return;

            // Placa "KART TRACK" no alto do arco, com dois pirulitos.
            float ringTop = Castle.Tunnel.WallHeight + Castle.Tunnel.ArchRise + Castle.Tunnel.ShellThickness + 0.35f;
            Add(M.HotPink, ProceduralMeshes.RoundedBox(new Vector3(10.4f, 3.0f, 0.8f), 0.35f, 3), At(0f, ringTop + 1.5f, zCenter));
            foreach (float x in new[] { -6.6f, 6.6f }) AddLollipop(x, ringTop - 0.8f, zCenter, 4.6f, 1.25f);
        }

        /// <summary>Sinos dourados pendurados no teto do túnel, em duas fileiras.</summary>
        private void BuildBells()
        {
            const float liner = 0.22f;
            float halfWidth = _inner - liner;
            float rise = Castle.Tunnel.ArchRise - liner;
            var profile = new List<Vector2>
            {
                new Vector2(0.62f, 0f), new Vector2(0.6f, 0.08f), new Vector2(0.46f, 0.3f),
                new Vector2(0.38f, 0.62f), new Vector2(0.24f, 0.9f), new Vector2(0f, 1.0f),
            };
            Mesh bell = ProceduralMeshes.Lathe(profile, 12);
            Mesh cord = ProceduralMeshes.Cylinder(0.04f, 0.04f, 1.3f, 5);

            for (float z = 2.5f; z < Castle.Tunnel.Length - 1f; z += Castle.Tunnel.BellSpacing)
            {
                foreach (float x in new[] { -4.2f, 4.2f })
                {
                    float ratio = x / halfWidth;
                    float ceiling = Castle.Tunnel.WallHeight + rise * Mathf.Sqrt(Mathf.Max(0f, 1f - ratio * ratio));
                    float bellTop = ceiling - 1.1f;
                    Add(M.Gold, cord, At(x, ceiling - 0.5f, z), false);
                    Add(M.Gold, bell, At(x, bellTop - 1.0f, z), false);
                    Add(M.Gold, Sphere, At(x, bellTop - 1.08f, z, 0f, 0f, 0f, 0.34f), false);
                }
            }
        }

        // ------------------------------------------------------------------ corpo

        /// <summary>Terraço, ala lateral, segundo andar e a torre central com o frontão.</summary>
        private void BuildBody()
        {
            float wo = _inner + Castle.Tunnel.ShellThickness;
            float bottom = -Castle.BuriedDepth;
            float top = Castle.TerraceHeight;

            // Nenhum volume desce abaixo de 12 m sobre a pista (o teto do túnel chega a 12 m).
            Box(M.Ginger, wo, 44f, bottom, 13f, 0f, 28f);
            Box(M.Ginger, -wo - 5f, -wo, bottom, 12f, 0f, 22f);
            Box(M.Ginger, -12f, 38f, 12.6f, 23f, 5f, 28f);
            Box(M.Ginger, 2f, 18f, 22.6f, 31f, 8f, 22f);

            // Glacê escorrendo e gomas no alto de cada fachada.
            IcingEdge(-wo, wo, top, 0f);
            GumdropRow(-wo + 1f, wo - 1f, top + 0.35f, 1.3f, 1.9f, 0.72f);
            IcingEdge(wo, 44f, 13f, 0f);
            GumdropRow(wo + 1.2f, 43f, 13.35f, 1.1f, 1.9f, 0.72f);
            IcingEdge(-wo - 5f, -wo, 12f, 0f);
            GumdropRow(-wo - 4.2f, -wo - 0.8f, 12.35f, 1.1f, 1.7f, 0.62f);
            IcingEdge(-12f, 38f, 23f, 5f);
            GumdropRow(-11f, 37f, 23.35f, 6.1f, 1.8f, 0.66f);
            IcingEdge(2f, 18f, 31f, 8f);

            // Botões de confeito na fachada do terraço, entre as janelas.
            for (float x = wo + 2f; x < 43f; x += 1.55f)
                Add(GumdropColors[Mathf.FloorToInt(x) % GumdropColors.Length], Sphere, At(x, 10.6f, -0.05f, 0f, 0f, 0f, 0.56f, 0.56f, 0.252f), false);

            // Janelas em arco com moldura rosa.
            foreach (float x in new[] { 20f, 26f, 32f, 38f }) AddWindow(x, 5.2f, 0f, 2.2f, 3.6f);
            foreach (float x in new[] { -9f, -3f, 25f, 31f, 36f }) AddWindow(x, 16.4f, 5f, 2.0f, 3.2f);
            foreach (float x in new[] { 5.2f, 14.8f }) AddWindow(x, 23.6f, 8f, 1.6f, 2.4f);
            // Porta principal, sob o letreiro.
            AddWindow(10f, top, 5f, 4.0f, 5.6f);

            // Frontão triangular da torre central, com telhado rosa e beiral de glacê.
            const float gableWidth = 8.6f, gableHeight = 6.5f;
            var gable = new List<Vector2> { new Vector2(-gableWidth, 0f), new Vector2(gableWidth, 0f), new Vector2(0f, gableHeight) };
            Add(M.Ginger, ProceduralMeshes.Extrude(gable, 14.8f), At(10f, 31f, 7.6f));

            float slope = Mathf.Atan2(gableHeight, gableWidth);
            float slabLength = Mathf.Sqrt(gableWidth * gableWidth + gableHeight * gableHeight) + 0.8f;
            foreach (int side in new[] { -1, 1 })
            {
                float cx = 10f + side * (gableWidth / 2f);
                float cy = 31f + gableHeight / 2f;
                float nx = side * Mathf.Sin(slope);
                float ny = Mathf.Cos(slope);
                Add(M.HotPink, Cube, At(cx + nx * 0.35f, cy + ny * 0.35f, 15f, 0f, 0f, -side * slope, slabLength, 0.7f, 16f));
                Add(M.Icing, ProceduralMeshes.RoundedBox(new Vector3(slabLength, 0.8f, 0.8f), 0.3f, 3),
                    At(cx + nx * 0.5f, cy + ny * 0.5f, 7.1f, 0f, 0f, -side * slope));
            }
            Add(M.HotPink, Sphere, At(10f, 31f + gableHeight + 0.7f, 7.4f, 0f, 0f, 0f, 1.4f));
            Add(M.Swirl, ProceduralMeshes.Cylinder(1.3f, 1.3f, 0.3f, 24), At(10f, 33.4f, 7.45f, Mathf.PI / 2f, 0f, 0f), false);
            // Plaquinha rosa atrás do letreiro "DOCE CASTELO".
            Add(M.HotPink, ProceduralMeshes.RoundedBox(new Vector3(10.8f, 3.2f, 0.7f), 0.3f, 3), At(10f, 28.4f, 7.6f));
        }

        /// <summary>Uma torre: corpo, faixas, beiral de glacê, janela, telhado e bandeirinha.</summary>
        private void BuildTower(Castle.Tower tower)
        {
            float wo = _inner + Castle.Tunnel.ShellThickness;
            float x = tower.X, z = tower.Z, r = tower.Radius, h = tower.Height;
            // Torre sobre o túnel nasce no alto do bloco, nunca atravessando o arco.
            float baseY = Mathf.Abs(x) - r < wo ? Castle.TerraceHeight - 0.5f : -Castle.BuriedDepth;

            M body = tower.Body switch
            {
                Castle.TowerBody.Cane => M.CaneBody,
                Castle.TowerBody.Peach => M.Peach,
                Castle.TowerBody.Pink => M.Pink,
                Castle.TowerBody.Blue => M.Blue,
                _ => M.Orange,
            };
            Add(body, ProceduralMeshes.Cylinder(r, r * 1.05f, h - baseY, 18), At(x, (h + baseY) / 2f, z));

            // Faixas: uma rosa no meio, uma de glacê no alto (toro deitado).
            Mesh band = ProceduralMeshes.Torus(r + 0.06f, 0.22f, 5, 18);
            Add(tower.Body == Castle.TowerBody.Pink ? M.Icing : M.Pink, band, At(x, h - 5.5f, z, Mathf.PI / 2f, 0f, 0f));
            Add(M.Icing, band, At(x, h - 0.9f, z, Mathf.PI / 2f, 0f, 0f));
            Add(M.Icing, ProceduralMeshes.Cylinder(r + 0.45f, r + 0.35f, 0.9f, 18), At(x, h + 0.2f, z));

            for (int i = 0; i < 11; i++)
            {
                float angle = i / 11f * Mathf.PI * 2f + _random() * 0.2f;
                float length = 0.5f + _random() * 1.1f;
                Add(M.Icing, Sphere, At(x + Mathf.Cos(angle) * (r + 0.32f), h - 0.1f - length / 2f, z + Mathf.Sin(angle) * (r + 0.32f),
                    0f, 0f, 0f, 0.6f, length, 0.6f), false);
            }
            AddWindow(x, h - 4.4f, z - r + 0.12f, Mathf.Min(1.5f, r * 0.55f), 2.4f);

            float roofBase = h + 0.6f;
            float roofTop;
            if (tower.Roof == Castle.TowerRoof.Cane || tower.Roof == Castle.TowerRoof.Blue)
            {
                float coneRadius = r + 0.55f;
                float coneHeight = coneRadius * (tower.Roof == Castle.TowerRoof.Cane ? 3.3f : 2.6f);
                Add(tower.Roof == Castle.TowerRoof.Cane ? M.CaneRoof : M.Blue, ProceduralMeshes.Cone(coneRadius, coneHeight, 18),
                    At(x, roofBase + coneHeight / 2f, z));
                roofTop = roofBase + coneHeight;
                Add(M.Icing, Sphere, At(x, roofTop, z, 0f, 0f, 0f, 0.72f));
                if (tower.Roof == Castle.TowerRoof.Blue)
                {
                    // Glacê escorrendo pela borda do cone azul.
                    for (int i = 0; i < 9; i++)
                    {
                        float angle = i / 9f * Mathf.PI * 2f;
                        float length = 0.6f + _random() * 0.9f;
                        Add(M.Icing, Sphere, At(x + Mathf.Cos(angle) * coneRadius * 0.86f, roofBase + 0.5f, z + Mathf.Sin(angle) * coneRadius * 0.86f,
                            0f, 0f, 0f, 0.68f, length, 0.68f), false);
                    }
                }
            }
            else if (tower.Roof == Castle.TowerRoof.Dome)
            {
                // Cúpula de açúcar em forma de cebola, com confeitos em volta.
                float s = r + 0.6f;
                float[,] shape = { { 0.72f, 0f }, { 1.0f, 0.32f }, { 1.06f, 0.62f }, { 0.92f, 0.98f }, { 0.58f, 1.32f }, { 0.22f, 1.62f }, { 0f, 1.78f } };
                var profile = new List<Vector2>();
                for (int i = 0; i < shape.GetLength(0); i++) profile.Add(new Vector2(shape[i, 0] * s, shape[i, 1] * s));
                Add(M.Blue, ProceduralMeshes.Lathe(profile, 18), At(x, roofBase - 0.2f, z));
                for (int i = 0; i < 10; i++)
                {
                    float angle = i / 10f * Mathf.PI * 2f;
                    Add(GumdropColors[i % GumdropColors.Length], Sphere,
                        At(x + Mathf.Cos(angle) * s * 1.05f, roofBase - 0.2f + s * 0.62f, z + Mathf.Sin(angle) * s * 1.05f, 0f, 0f, 0f, 0.52f), false);
                }
                Add(M.Icing, ProceduralMeshes.Dome(14, 6), At(x, roofBase - 0.2f + s * 1.12f, z, 0f, 0f, 0f, s * 0.5f, s * 0.5f * 0.55f, s * 0.5f));
                roofTop = roofBase - 0.2f + s * 1.78f;
                Add(M.HotPink, Sphere, At(x, roofTop + 0.3f, z, 0f, 0f, 0f, 0.84f));
                roofTop += 0.6f;
            }
            else
            {
                float gum = r + 0.4f;
                Add(M.HotPink, ProceduralMeshes.Dome(18, 10), At(x, roofBase - 0.3f, z, 0f, 0f, 0f, gum, gum * 1.1f, gum));
                roofTop = roofBase - 0.3f + gum * 1.1f;
                Add(M.Icing, Sphere, At(x, roofTop + 0.2f, z, 0f, 0f, 0f, 0.8f));
                roofTop += 0.5f;
            }

            // Bandeirinha rosa no topo.
            Add(M.Icing, ProceduralMeshes.Cylinder(0.07f, 0.07f, 2.6f, 6), At(x, roofTop + 1.2f, z));
            Add(M.HotPink, Cube, At(x + 0.78f, roofTop + 2.0f, z, 0f, 0f, 0f, 1.5f, 0.9f, 0.08f), false);
        }

        /// <summary>Escada de jujubas descendo do terraço até o gramado, com postes de bengala e corrimão de glacê.</summary>
        private void BuildStairs()
        {
            var top = new Vector2(19.5f, -0.9f);
            var foot = new Vector2(40f, -20f);
            const int steps = 16;
            const float width = 5f;
            const float landing = 13f;
            float bottom = -Castle.BuriedDepth;

            Vector2 run = top - foot;
            float length = run.magnitude;
            run.Normalize();
            // Rotação que leva o X local do degrau para a direção da subida.
            float angle = Mathf.Atan2(-run.y, run.x);
            var across = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            float stepRun = length / steps;
            float rise = landing / steps;

            for (int i = 0; i < steps; i++)
            {
                float u = stepRun * (i + 0.5f);
                float cx = foot.x + run.x * u, cz = foot.y + run.y * u;
                float stepTop = rise * (i + 1);
                Add(M.Ginger, Cube, At(cx, (stepTop + bottom) / 2f, cz, 0f, angle, 0f, stepRun + 0.05f, stepTop - bottom, width));
                for (int k = 0; k < 4; k++)
                {
                    float w = (k - 1.5f) * 1.18f;
                    M color = CandyColors[(i * 3 + k) % CandyColors.Length];
                    if (color == M.Icing) color = M.HotPink;
                    Add(color, Sphere, At(cx + across.x * w, stepTop + 0.12f, cz + across.y * w, 0f, angle, 0f, 0.72f, 0.6f, 1.12f), false);
                }
            }

            // Postes de bengala nas duas bordas, a cada quatro degraus.
            var postTops = new[] { new List<Vector3>(), new List<Vector3>() };
            for (int i = 0; i <= steps; i += 4)
            {
                float u = stepRun * Mathf.Min(i, steps - 0.5f);
                float stepTop = rise * Mathf.Min(i + 1, steps);
                for (int side = 0; side < 2; side++)
                {
                    float w = (side == 0 ? -1f : 1f) * (width / 2f - 0.25f);
                    float px = foot.x + run.x * u + across.x * w;
                    float pz = foot.y + run.y * u + across.y * w;
                    float postHeight = i == 0 ? 4.2f : 2.3f;
                    Add(M.CaneBody, ProceduralMeshes.Cylinder(0.18f, 0.18f, postHeight, 10), At(px, stepTop + postHeight / 2f, pz));
                    if (i == 0)
                    {
                        Add(M.CaneBody, ProceduralMeshes.Torus(0.55f, 0.18f, 6, 12, Mathf.PI),
                            At(px - run.x * 0.55f, stepTop + postHeight, pz - run.y * 0.55f, 0f, angle + Mathf.PI, 0f));
                    }
                    postTops[side].Add(new Vector3(px, stepTop + Mathf.Min(postHeight, 2.3f), pz));
                }
            }
            foreach (List<Vector3> rail in postTops) Add(M.Icing, ProceduralMeshes.Tube(rail, 0.13f, 6, 40), Matrix4x4.identity, false);
        }

        /// <summary>Pirulitos, cristais de bala, gomas e arvorezinhas em volta da base.</summary>
        private void BuildGarden()
        {
            (float x, float z, float height, float radius)[] lollipops =
            {
                (-18.5f, -3.5f, 5.6f, 1.5f), (17.2f, -4.2f, 4.6f, 1.3f), (24f, -12.5f, 5.2f, 1.4f),
                (46.5f, -6f, 6.2f, 1.7f), (-22f, 12f, 5.0f, 1.3f),
            };
            foreach ((float x, float z, float height, float radius) in lollipops) AddLollipop(x, GroundAt(x, z) - 0.4f, z, height, radius);
            // Pirulitos também no alto do terraço e do segundo andar.
            AddLollipop(33f, 13f, 2.5f, 3.4f, 1.1f);
            AddLollipop(-10.5f, 23f, 7.2f, 3.2f, 1.0f);
            AddLollipop(36.5f, 23f, 7.5f, 3.2f, 1.0f);

            (float x, float z)[] crystals = { (19.5f, -1.8f), (-20.5f, -6.5f), (28f, -17.5f), (45f, -12f), (35.5f, -3f), (-17.5f, -2.5f) };
            M[] crystalColors = { M.Lavender, M.Blue, M.Pink, M.Mint, M.Yellow };
            Mesh octahedron = ProceduralMeshes.Octahedron();
            for (int c = 0; c < crystals.Length; c++)
            {
                float ground = GroundAt(crystals[c].x, crystals[c].z);
                for (int i = 0; i < 4; i++)
                {
                    // A ordem dos sorteios é a mesma da web, para o cenário sair igual.
                    float size = 0.55f + _random() * 0.6f;
                    float px = crystals[c].x + (_random() - 0.5f) * 2.2f;
                    float pz = crystals[c].z + (_random() - 0.5f) * 2.2f;
                    float rx = (_random() - 0.5f) * 0.5f;
                    float ry = _random() * Mathf.PI;
                    float rz = (_random() - 0.5f) * 0.5f;
                    Add(crystalColors[(c + i) % crystalColors.Length], octahedron,
                        At(px, ground + size * 0.9f, pz, rx, ry, rz, size * 0.6f, size * 1.5f, size * 0.6f), false);
                }
            }

            // Gomas no chão, em grupinhos.
            Mesh dome = ProceduralMeshes.Dome(10, 6);
            for (int g = 0; g < 16; g++)
            {
                bool onRight = g % 4 == 0;
                float gx = onRight ? -15.5f - _random() * 6f : 16f + _random() * 30f;
                float gz = onRight ? -2f - _random() * 8f : -1.5f - _random() * 3.5f;
                float ground = GroundAt(gx, gz);
                float size = 0.45f + _random() * 0.4f;
                Add(GumdropColors[g % GumdropColors.Length], dome, At(gx, ground - 0.05f, gz, 0f, 0f, 0f, size, size * 1.1f, size), false);
            }

            // Arvorezinhas de bala redondas.
            (float x, float z)[] trees = { (47f, 1.5f), (-23f, 3f), (-21.5f, 20f), (47f, 18f), (47.5f, 30f) };
            for (int i = 0; i < trees.Length; i++)
            {
                float ground = GroundAt(trees[i].x, trees[i].z);
                Add(M.Icing, ProceduralMeshes.Cylinder(0.2f, 0.26f, 2.4f, 8), At(trees[i].x, ground + 1.2f, trees[i].z));
                Add(GumdropColors[(i * 2 + 1) % GumdropColors.Length], Sphere, At(trees[i].x, ground + 3.3f, trees[i].z, 0f, 0f, 0f, 3f));
            }
        }

        /// <summary>Corações de goma flutuando diante da fachada, bem acima da pista.</summary>
        private void BuildHearts()
        {
            var shape = new List<Vector2>();
            Vector2 start = new Vector2(0f, -1f);
            AddBezier(shape, start, new Vector2(-0.3f, -0.7f), new Vector2(-1.1f, -0.2f), new Vector2(-1.1f, 0.35f));
            AddBezier(shape, new Vector2(-1.1f, 0.35f), new Vector2(-1.1f, 0.95f), new Vector2(-0.35f, 1.15f), new Vector2(0f, 0.6f));
            AddBezier(shape, new Vector2(0f, 0.6f), new Vector2(0.35f, 1.15f), new Vector2(1.1f, 0.95f), new Vector2(1.1f, 0.35f));
            AddBezier(shape, new Vector2(1.1f, 0.35f), new Vector2(1.1f, -0.2f), new Vector2(0.3f, -0.7f), start);
            Mesh heart = ProceduralMeshes.Extrude(shape, Castle.Hearts.Thickness);

            M[] colors = { M.HotPink, M.Pink, M.Lavender };
            for (int i = 0; i < Castle.Hearts.Count; i++)
            {
                float u = Castle.Hearts.Count > 1 ? (float)i / (Castle.Hearts.Count - 1) : 0.5f;
                float x = Castle.Hearts.FromX + (Castle.Hearts.ToX - Castle.Hearts.FromX) * u;
                float y = Castle.Hearts.BaseHeight + (i % 3) * Castle.Hearts.HeightStep;
                float z = Castle.Hearts.Z - (i % 2) * Castle.Hearts.ZStagger;
                // Extrusão vai de 0 a espessura; centraliza em Z.
                Add(colors[i % colors.Length], heart,
                    At(x, y, z, 0f, 0f, 0f, Castle.Hearts.Size) * Matrix4x4.Translate(new Vector3(0f, 0f, -Castle.Hearts.Thickness / 2f)), false);
            }
        }

        /// <summary>Os dois letreiros, fora do grupo espelhado (senão o texto sairia invertido).</summary>
        private void BuildSigns(Transform root)
        {
            float ringTop = Castle.Tunnel.WallHeight + Castle.Tunnel.ArchRise + Castle.Tunnel.ShellThickness + 0.35f;
            // X negado: o root não está espelhado, as coordenadas da web estão.
            _signs.Add(CandySign.Build(root, "KART TRACK", 9.6f, new Vector3(0f, ringTop + 1.5f, -Castle.Tunnel.PortalDepth / 2f - 0.42f), Castle.Colors.SignBackground, 0.28f));
            _signs.Add(CandySign.Build(root, "DOCE CASTELO", 10.2f, new Vector3(-10f, 28.4f, 7.22f), Castle.Colors.SignBackground, 0.28f));
        }

        // ------------------------------------------------------------------ peças

        private static Mesh Sphere => ProceduralMeshes.Primitive(PrimitiveType.Sphere);
        private static Mesh Cube => ProceduralMeshes.Primitive(PrimitiveType.Cube);

        private void Box(M material, float x0, float x1, float y0, float y1, float z0, float z1) =>
            Add(material, Cube, At((x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f, 0f, 0f, 0f, x1 - x0, y1 - y0, z1 - z0));

        /// <summary>Beiral de glacê no topo de uma fachada virada para -Z, com gotas escorrendo.</summary>
        private void IcingEdge(float x0, float x1, float yTop, float zFace)
        {
            Add(M.Icing, ProceduralMeshes.RoundedBox(new Vector3(x1 - x0 + 0.5f, 0.8f, 1.1f), 0.3f, 3), At((x0 + x1) / 2f, yTop - 0.05f, zFace + 0.3f));
            for (float x = x0 + 0.55f; x < x1 - 0.3f; x += 1.05f)
            {
                float length = 0.5f + _random() * 1.6f;
                Add(M.Icing, Sphere, At(x, yTop - 0.35f - length / 2f + 0.25f, zFace - 0.14f, 0f, 0f, 0f, 0.6f, length, 0.52f), false);
            }
        }

        /// <summary>Fileira de gomas coloridas em cima de um beiral.</summary>
        private void GumdropRow(float x0, float x1, float y, float z, float spacing, float radius)
        {
            Mesh dome = ProceduralMeshes.Dome(10, 5);
            int index = 0;
            for (float x = x0; x <= x1; x += spacing)
                Add(GumdropColors[index++ % GumdropColors.Length], dome, At(x, y, z, 0f, 0f, 0f, radius, radius * 1.15f, radius), false);
        }

        /// <summary>Janela (ou porta) em arco, virada para -Z, com moldura rosa e peitoril de glacê.</summary>
        private void AddWindow(float x, float yBottom, float zFace, float width, float height)
        {
            float half = width / 2f;
            float straight = Mathf.Max(0.2f, height - half);
            var opening = new List<Vector2> { new Vector2(-half, 0f), new Vector2(half, 0f), new Vector2(half, straight) };
            for (int i = 1; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI;
                opening.Add(new Vector2(Mathf.Cos(angle) * half, straight + Mathf.Sin(angle) * half));
            }
            opening.Add(new Vector2(-half, straight));
            Add(M.Window, ProceduralMeshes.Extrude(opening, 0.3f), At(x, yBottom, zFace - 0.2f), false);

            float frame = Mathf.Max(0.16f, width * 0.09f);
            Add(M.Pink, ProceduralMeshes.Torus(half + frame * 0.6f, frame, 5, 10, Mathf.PI), At(x, yBottom + straight, zFace - 0.22f), false);
            foreach (int side in new[] { -1, 1 })
                Add(M.Pink, Cube, At(x + side * (half + frame * 0.6f), yBottom + straight / 2f, zFace - 0.22f, 0f, 0f, 0f, frame * 2f, straight, frame * 2f), false);
            Add(M.Icing, ProceduralMeshes.RoundedBox(new Vector3(width + frame * 4f, 0.36f, 0.8f), 0.15f, 2), At(x, yBottom - 0.1f, zFace - 0.3f), false);
        }

        /// <summary>Pirulito: palito de glacê e disco em espiral virado para a pista.</summary>
        private void AddLollipop(float x, float yBase, float z, float height, float radius)
        {
            Add(M.Icing, ProceduralMeshes.Cylinder(0.12f, 0.12f, height, 8), At(x, yBase + height / 2f, z));
            Add(M.Swirl, ProceduralMeshes.Cylinder(radius, radius, 0.32f, 24), At(x, yBase + height + radius * 0.8f, z, Mathf.PI / 2f, 0f, 0f), false);
        }

        /// <summary>Altura do terreno num ponto do referencial local (medidas da web), relativa à origem.</summary>
        private float GroundAt(float localX, float localZ)
        {
            Vector3 world = _frame.Origin + _frame.Left * localX + _frame.Forward * localZ;
            return _path.TerrainHeight(world.x, world.z) - _frame.Origin.y;
        }

        private void Add(M material, Mesh mesh, Matrix4x4 matrix, bool castShadow = true) =>
            (castShadow ? _solid : _details).Add(mesh, _materials[material], matrix);

        /// <summary>Posição, rotação (radianos, ordem XYZ como na web) e escala.</summary>
        private static Matrix4x4 At(float x, float y, float z, float rx = 0f, float ry = 0f, float rz = 0f, float sx = 1f, float sy = float.NaN, float sz = float.NaN)
        {
            if (float.IsNaN(sy)) sy = sx;
            if (float.IsNaN(sz)) sz = sx;
            return Matrix4x4.TRS(new Vector3(x, y, z), SceneryBuilder.Euler(rx, ry, rz), new Vector3(sx, sy, sz));
        }

        /// <summary>Contorno do arco do túnel: parede reta da direita (+X), meia-elipse por cima e parede da esquerda.</summary>
        private static List<Vector2> ArchPoints(float halfWidth, float wallHeight, float rise, float bottom, int segments)
        {
            var points = new List<Vector2> { new Vector2(halfWidth, bottom) };
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI;
                points.Add(new Vector2(Mathf.Cos(angle) * halfWidth, wallHeight + Mathf.Sin(angle) * rise));
            }
            points.Add(new Vector2(-halfWidth, bottom));
            return points;
        }

        private static void AddBezier(List<Vector2> points, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            for (int i = 0; i < 10; i++)
            {
                float t = i / 10f, u = 1f - t;
                points.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
            }
        }

        private static Dictionary<M, Material> CreateMaterials()
        {
            Material Candy(Color color, float roughness, float emissive, int top, int bottom, float rim, Texture2D map = null, Vector2? tiling = null)
            {
                Material material = map != null ? CandyMaterials.Textured(map, roughness, tiling) : CandyMaterials.Candy(color, roughness);
                if (emissive > 0f) CandyMaterials.SetGlow(material, color, emissive);
                return CandyMaterials.Coat(material, CircuitConfig.Hex(top), CircuitConfig.Hex(bottom), rim);
            }

            Material Plain(Color color, float roughness, float emissive)
            {
                Material material = CandyMaterials.Candy(color, roughness);
                if (emissive > 0f) CandyMaterials.SetGlow(material, color, emissive);
                return material;
            }

            Texture2D stripes = ProceduralTextures.Stripe(Castle.Colors.CaneStripe, Color.white);
            Material swirl = CandyMaterials.Textured(ProceduralTextures.Swirl(), 0.14f);
            CandyMaterials.SetGlow(swirl, Color.white, 0.08f);

            return new Dictionary<M, Material>
            {
                [M.Ginger] = Candy(Castle.Colors.Gingerbread, 0.55f, 0.03f, 0xffe2c0, 0xc97a44, 0.05f),
                [M.Icing] = Candy(Castle.Colors.Icing, 0.28f, 0.07f, 0xffffff, 0xffe6f0, 0.08f),
                [M.Pink] = Candy(Castle.Colors.Pink, 0.22f, 0.06f, 0xffc9df, 0xff7fb0, 0.09f),
                [M.HotPink] = Candy(Castle.Colors.HotPink, 0.2f, 0.07f, 0xffaacd, 0xff3f86, 0.09f),
                [M.Blue] = Candy(Castle.Colors.Blue, 0.3f, 0.06f, 0xc9f5ff, 0x53bdeb, 0.1f),
                [M.Orange] = Candy(Castle.Colors.Orange, 0.3f, 0.05f, 0xffe18b, 0xff8c28, 0.06f),
                [M.Peach] = Candy(Castle.Colors.Peach, 0.34f, 0.05f, 0xffe8cc, 0xffa86a, 0.06f),
                [M.Mint] = Plain(Castle.Colors.Mint, 0.16f, 0.1f),
                [M.Yellow] = Plain(Castle.Colors.Yellow, 0.16f, 0.1f),
                [M.Lavender] = Plain(Castle.Colors.Lavender, 0.16f, 0.1f),
                [M.Window] = Plain(Castle.Colors.Window, 0.6f, 0f),
                [M.Gold] = Plain(Castle.Colors.Gold, 0.25f, 0.35f),
                [M.HoopPink] = Plain(Castle.Colors.HoopPink, 0.24f, 0.2f),
                [M.HoopWhite] = Plain(Castle.Colors.HoopWhite, 0.24f, 0.16f),
                [M.CaneRoof] = Candy(Color.white, 0.2f, 0.04f, 0xffffff, 0xffd6e0, 0.06f, stripes, new Vector2(4f, 2f)),
                [M.CaneBody] = Candy(Color.white, 0.2f, 0.04f, 0xffffff, 0xffd6e0, 0.06f, stripes, new Vector2(3f, 5f)),
                [M.CaneTrim] = Plain(Color.white, 0.2f, 0.08f),
                [M.Swirl] = swirl,
            };
        }
    }
}
