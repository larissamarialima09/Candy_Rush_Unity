using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CandyRush.Rendering
{
    /// <summary>
    /// Formas que a Unity não traz prontas e que a versão web usava do Three.js:
    /// caixa arredondada, cilindro com raios diferentes, cone, toro (inclusive
    /// só um arco), casca de glacê sobre um toro e estrela chapada.
    ///
    /// As normais são calculadas pela fórmula de cada forma (e não aproximadas
    /// depois), e o enrolamento de cada triângulo é conferido contra a normal:
    /// assim nenhuma face sai virada para dentro.
    ///
    /// Convenções iguais às do Three.js, para as medidas da web valerem direto:
    ///  - cilindro e cone em pé no eixo Y, centrados na origem;
    ///  - toro deitado no plano XY (eixo em Z), com o arco começando em +X.
    /// </summary>
    public static class ProceduralMeshes
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Caixa com cantos arredondados (o RoundedBoxGeometry da web).</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int subdivisions = 5)
        {
            string key = $"box {size} {radius} {subdivisions}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            Vector3 half = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) - 1e-4f);
            Vector3 inner = half - Vector3.one * radius;

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Cada face do cubo vira uma grade; cada vértice é "encolhido" até a
            // caixa interna e empurrado pelo raio na direção de onde veio.
            Vector3[] faceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 n in faceNormals)
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u);
                int start = positions.Count;
                int grid = subdivisions + 1;

                for (int j = 0; j <= subdivisions; j++)
                {
                    for (int i = 0; i <= subdivisions; i++)
                    {
                        float a = (float)i / subdivisions * 2f - 1f;
                        float b = (float)j / subdivisions * 2f - 1f;
                        Vector3 onCube = Vector3.Scale(n + u * a + v * b, half);
                        Vector3 clamped = new Vector3(
                            Mathf.Clamp(onCube.x, -inner.x, inner.x),
                            Mathf.Clamp(onCube.y, -inner.y, inner.y),
                            Mathf.Clamp(onCube.z, -inner.z, inner.z));
                        Vector3 normal = (onCube - clamped).normalized;
                        if (normal.sqrMagnitude < 1e-6f) normal = n;
                        positions.Add(clamped + normal * radius);
                        normals.Add(normal);
                        uvs.Add(new Vector2((float)i / subdivisions, (float)j / subdivisions));
                    }
                }

                for (int j = 0; j < subdivisions; j++)
                {
                    for (int i = 0; i < subdivisions; i++)
                    {
                        int p0 = start + j * grid + i;
                        AddQuad(triangles, positions, normals, p0, p0 + 1, p0 + grid + 1, p0 + grid);
                    }
                }
            }

            return Store(key, Build(key, positions, normals, uvs, triangles));
        }

        /// <summary>Cilindro em pé (eixo Y), centrado na origem, com tampas.</summary>
        public static Mesh Cylinder(float radiusTop, float radiusBottom, float height, int segments = 20)
        {
            string key = $"cyl {radiusTop} {radiusBottom} {height} {segments}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float halfHeight = height / 2f;
            float slope = (radiusBottom - radiusTop) / height;

            // Lateral.
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
                Vector3 normal = new Vector3(sin, slope, cos).normalized;
                positions.Add(new Vector3(sin * radiusTop, halfHeight, cos * radiusTop));
                normals.Add(normal);
                uvs.Add(new Vector2((float)i / segments, 1f));
                positions.Add(new Vector3(sin * radiusBottom, -halfHeight, cos * radiusBottom));
                normals.Add(normal);
                uvs.Add(new Vector2((float)i / segments, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                AddQuad(triangles, positions, normals, a, a + 2, a + 3, a + 1);
            }

            // Tampas.
            AddCap(positions, normals, uvs, triangles, radiusTop, halfHeight, Vector3.up, segments);
            AddCap(positions, normals, uvs, triangles, radiusBottom, -halfHeight, Vector3.down, segments);

            return Store(key, Build(key, positions, normals, uvs, triangles));
        }

        /// <summary>Cone em pé (ponta para cima), centrado na origem.</summary>
        public static Mesh Cone(float radius, float height, int segments = 12) => Cylinder(0f, radius, height, segments);

        /// <summary>
        /// Toro no plano XY (eixo em Z). arc = quanto da volta do ANEL existe
        /// (π = meio anel, de +X passando por +Y até -X, como no Three.js).
        /// </summary>
        public static Mesh Torus(float radius, float tube, int tubeSegments, int ringSegments, float arc = Mathf.PI * 2f)
        {
            string key = $"torus {radius} {tube} {tubeSegments} {ringSegments} {arc}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i <= ringSegments; i++)
            {
                float u = (float)i / ringSegments * arc;
                for (int j = 0; j <= tubeSegments; j++)
                {
                    float v = (float)j / tubeSegments * Mathf.PI * 2f;
                    Vector3 center = new Vector3(Mathf.Cos(u) * radius, Mathf.Sin(u) * radius, 0f);
                    Vector3 normal = new Vector3(Mathf.Cos(v) * Mathf.Cos(u), Mathf.Cos(v) * Mathf.Sin(u), Mathf.Sin(v));
                    positions.Add(center + normal * tube);
                    normals.Add(normal);
                    uvs.Add(new Vector2((float)i / ringSegments, (float)j / tubeSegments));
                }
            }

            int stride = tubeSegments + 1;
            for (int i = 0; i < ringSegments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    int a = i * stride + j;
                    AddQuad(triangles, positions, normals, a, a + stride, a + stride + 1, a + 1);
                }
            }

            return Store(key, Build(key, positions, normals, uvs, triangles));
        }

        /// <summary>
        /// Casca de glacê sobre um toro: o mesmo toro, um pouco mais gordo,
        /// varrido só de -span(u) a +span(u) na volta da SEÇÃO. O span variando
        /// ao longo do anel desenha a borda escorrida. (icingShellGeometry da web)
        /// </summary>
        public static Mesh TorusShell(float ringRadius, float tubeRadius, Func<float, float> span, int ringSegments, int tubeSegments)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i <= ringSegments; i++)
            {
                float u = (float)i / ringSegments * Mathf.PI * 2f;
                float limit = span(u);
                for (int j = 0; j <= tubeSegments; j++)
                {
                    float v = ((float)j / tubeSegments - 0.5f) * 2f * limit;
                    Vector3 normal = new Vector3(Mathf.Cos(v) * Mathf.Cos(u), Mathf.Cos(v) * Mathf.Sin(u), Mathf.Sin(v));
                    float radial = ringRadius + tubeRadius * Mathf.Cos(v);
                    positions.Add(new Vector3(radial * Mathf.Cos(u), radial * Mathf.Sin(u), tubeRadius * Mathf.Sin(v)));
                    normals.Add(normal);
                    uvs.Add(new Vector2((float)i / ringSegments, (float)j / tubeSegments));
                }
            }

            int stride = tubeSegments + 1;
            for (int i = 0; i < ringSegments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    int a = i * stride + j;
                    AddQuad(triangles, positions, normals, a, a + stride, a + stride + 1, a + 1);
                }
            }

            return Build("glace", positions, normals, uvs, triangles);
        }

        /// <summary>Estrela de cinco pontas chapada no plano XY (use material de dois lados).</summary>
        public static Mesh Star(float outerRadius, float innerRadius)
        {
            string key = $"star {outerRadius} {innerRadius}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            var positions = new List<Vector3> { Vector3.zero };
            var normals = new List<Vector3> { Vector3.back };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var triangles = new List<int>();
            const int points = 5;
            for (int i = 0; i < points * 2; i++)
            {
                float angle = (float)i / (points * 2) * Mathf.PI * 2f - Mathf.PI / 2f;
                float r = i % 2 == 0 ? outerRadius : innerRadius;
                positions.Add(new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
                normals.Add(Vector3.back);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }
            for (int i = 0; i < points * 2; i++)
            {
                AddTriangle(triangles, positions, normals, 0, 1 + i, 1 + (i + 1) % (points * 2));
            }

            return Store(key, Build(key, positions, normals, uvs, triangles));
        }

        /// <summary>
        /// Extrusão de um contorno 2D (plano XY) ao longo de +Z, de 0 até depth
        /// (o ExtrudeGeometry da web, sem bisel). O contorno pode ser côncavo
        /// (arco do túnel, janela, coração): as tampas são trianguladas por
        /// "corte de orelhas".
        /// </summary>
        public static Mesh Extrude(IReadOnlyList<Vector2> outline, float depth)
        {
            var polygon = new List<Vector2>(outline);
            if (polygon.Count > 2 && (polygon[0] - polygon[polygon.Count - 1]).sqrMagnitude < 1e-8f) polygon.RemoveAt(polygon.Count - 1);
            if (SignedArea(polygon) < 0f) polygon.Reverse(); // anti-horário

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Tampas: frente (z = 0, olhando para -Z) e fundo (z = depth, olhando para +Z).
            List<int> caps = Triangulate(polygon);
            foreach (float z in new[] { 0f, depth })
            {
                int start = positions.Count;
                Vector3 normal = z == 0f ? Vector3.back : Vector3.forward;
                foreach (Vector2 p in polygon)
                {
                    positions.Add(new Vector3(p.x, p.y, z));
                    normals.Add(normal);
                    uvs.Add(p * 0.1f);
                }
                for (int i = 0; i < caps.Count; i += 3)
                    AddTriangle(triangles, positions, normals, start + caps[i], start + caps[i + 1], start + caps[i + 2]);
            }

            // Laterais: um quadrilátero por aresta, com normal chapada.
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                Vector2 edge = b - a;
                if (edge.sqrMagnitude < 1e-10f) continue;
                // Contorno anti-horário: a normal de fora é a aresta girada -90°.
                Vector3 normal = new Vector3(edge.y, -edge.x, 0f).normalized;
                int start = positions.Count;
                positions.Add(new Vector3(a.x, a.y, 0f));
                positions.Add(new Vector3(b.x, b.y, 0f));
                positions.Add(new Vector3(b.x, b.y, depth));
                positions.Add(new Vector3(a.x, a.y, depth));
                for (int k = 0; k < 4; k++) normals.Add(normal);
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(edge.magnitude * 0.1f, 0));
                uvs.Add(new Vector2(edge.magnitude * 0.1f, depth * 0.1f));
                uvs.Add(new Vector2(0, depth * 0.1f));
                AddQuad(triangles, positions, normals, start, start + 1, start + 2, start + 3);
            }

            return Build("extrusão", positions, normals, uvs, triangles);
        }

        /// <summary>Tubo de raio fixo passando suavemente pelos pontos (o TubeGeometry com CatmullRomCurve3 da web).</summary>
        public static Mesh Tube(IReadOnlyList<Vector3> points, float radius, int radialSegments, int tubularSegments)
        {
            // Curva Catmull-Rom uniforme e aberta pelos pontos.
            var path = new Vector3[tubularSegments + 1];
            for (int i = 0; i <= tubularSegments; i++)
            {
                float t = (float)i / tubularSegments * (points.Count - 1);
                int k = Mathf.Min(Mathf.FloorToInt(t), points.Count - 2);
                float f = t - k;
                Vector3 p0 = points[Mathf.Max(k - 1, 0)], p1 = points[k], p2 = points[k + 1], p3 = points[Mathf.Min(k + 2, points.Count - 1)];
                path[i] = 0.5f * (2f * p1 + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f);
            }

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Referencial transportado ao longo da curva, para o tubo não torcer.
            Vector3 tangent = (path[1] - path[0]).normalized;
            Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i <= tubularSegments; i++)
            {
                Vector3 next = i < tubularSegments ? path[i + 1] - path[i] : path[i] - path[i - 1];
                Vector3 newTangent = next.normalized;
                normal = Vector3.ProjectOnPlane(normal, newTangent).normalized;
                Vector3 binormal = Vector3.Cross(newTangent, normal);
                for (int j = 0; j <= radialSegments; j++)
                {
                    float angle = (float)j / radialSegments * Mathf.PI * 2f;
                    Vector3 n = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                    positions.Add(path[i] + n * radius);
                    normals.Add(n);
                    uvs.Add(new Vector2((float)i / tubularSegments, (float)j / radialSegments));
                }
            }
            int stride = radialSegments + 1;
            for (int i = 0; i < tubularSegments; i++)
            for (int j = 0; j < radialSegments; j++)
            {
                int a = i * stride + j;
                AddQuad(triangles, positions, normals, a, a + stride, a + stride + 1, a + 1);
            }

            return Build("tubo", positions, normals, uvs, triangles);
        }

        /// <summary>Sólido de revolução em torno do eixo Y (o LatheGeometry da web). profile = (raio, altura).</summary>
        public static Mesh Lathe(IReadOnlyList<Vector2> profile, int segments)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
                for (int j = 0; j < profile.Count; j++)
                {
                    // Normal pela direção do perfil naquele ponto (girada para fora).
                    Vector2 before = profile[Mathf.Max(j - 1, 0)];
                    Vector2 after = profile[Mathf.Min(j + 1, profile.Count - 1)];
                    Vector2 direction = after - before;
                    Vector2 outward = new Vector2(direction.y, -direction.x).normalized;
                    positions.Add(new Vector3(profile[j].x * sin, profile[j].y, profile[j].x * cos));
                    normals.Add(new Vector3(outward.x * sin, outward.y, outward.x * cos).normalized);
                    uvs.Add(new Vector2((float)i / segments, (float)j / (profile.Count - 1)));
                }
            }
            int stride = profile.Count;
            for (int i = 0; i < segments; i++)
            for (int j = 0; j < profile.Count - 1; j++)
            {
                int a = i * stride + j;
                AddQuad(triangles, positions, normals, a, a + stride, a + stride + 1, a + 1);
            }

            return Build("torno", positions, normals, uvs, triangles);
        }

        /// <summary>Octaedro de faces chapadas (cristal de bala), raio 1.</summary>
        public static Mesh Octahedron()
        {
            const string key = "octahedron";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            Vector3[] v = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int[,] faces = { { 0, 2, 4 }, { 0, 4, 3 }, { 0, 3, 5 }, { 0, 5, 2 }, { 1, 2, 5 }, { 1, 5, 3 }, { 1, 3, 4 }, { 1, 4, 2 } };
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int f = 0; f < 8; f++)
            {
                Vector3 normal = (v[faces[f, 0]] + v[faces[f, 1]] + v[faces[f, 2]]).normalized;
                int start = positions.Count;
                for (int k = 0; k < 3; k++)
                {
                    positions.Add(v[faces[f, k]]);
                    normals.Add(normal);
                    uvs.Add(Vector2.zero);
                }
                AddTriangle(triangles, positions, normals, start, start + 1, start + 2);
            }
            return Store(key, Build(key, positions, normals, uvs, triangles));
        }

        /// <summary>Meia esfera (cúpula) de raio 1, com a base aberta em y = 0 (goma, telhado de goma).</summary>
        public static Mesh Dome(int widthSegments = 12, int heightSegments = 6)
        {
            string key = $"dome {widthSegments} {heightSegments}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int j = 0; j <= heightSegments; j++)
            {
                float theta = (float)j / heightSegments * Mathf.PI / 2f;
                for (int i = 0; i <= widthSegments; i++)
                {
                    float phi = (float)i / widthSegments * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Cos(phi));
                    positions.Add(n);
                    normals.Add(n);
                    uvs.Add(new Vector2((float)i / widthSegments, (float)j / heightSegments));
                }
            }
            int stride = widthSegments + 1;
            for (int j = 0; j < heightSegments; j++)
            for (int i = 0; i < widthSegments; i++)
            {
                int a = j * stride + i;
                AddQuad(triangles, positions, normals, a, a + 1, a + stride + 1, a + stride);
            }
            return Store(key, Build(key, positions, normals, uvs, triangles));
        }

        private static float SignedArea(List<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        /// <summary>Triangulação por corte de orelhas de um polígono simples anti-horário.</summary>
        private static List<int> Triangulate(List<Vector2> polygon)
        {
            var result = new List<int>();
            var remaining = new List<int>();
            for (int i = 0; i < polygon.Count; i++) remaining.Add(i);

            int guard = 0;
            while (remaining.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int ia = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    int ib = remaining[i];
                    int ic = remaining[(i + 1) % remaining.Count];
                    Vector2 a = polygon[ia], b = polygon[ib], c = polygon[ic];
                    // Vértice convexo?
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 1e-9f) continue;

                    bool containsOther = false;
                    foreach (int other in remaining)
                    {
                        if (other == ia || other == ib || other == ic) continue;
                        if (PointInTriangle(polygon[other], a, b, c)) { containsOther = true; break; }
                    }
                    if (containsOther) continue;

                    result.Add(ia);
                    result.Add(ib);
                    result.Add(ic);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // polígono degenerado: devolve o que deu
            }
            if (remaining.Count == 3)
            {
                result.Add(remaining[0]);
                result.Add(remaining[1]);
                result.Add(remaining[2]);
            }
            return result;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        /// <summary>Malha de uma forma primitiva da Unity (esfera, cápsula, quad...), sem criar objeto na cena.</summary>
        public static Mesh Primitive(PrimitiveType type)
        {
            string key = "prim " + type;
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temp);
            return Store(key, mesh);
        }

        // ------------------------------------------------------------------

        private static void AddCap(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            float radius, float y, Vector3 normal, int segments)
        {
            if (radius <= 0f) return;
            int center = positions.Count;
            positions.Add(new Vector3(0f, y, 0f));
            normals.Add(normal);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                positions.Add(new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius));
                normals.Add(normal);
                uvs.Add(new Vector2(0.5f + Mathf.Sin(angle) * 0.5f, 0.5f + Mathf.Cos(angle) * 0.5f));
            }
            for (int i = 0; i < segments; i++) AddTriangle(triangles, positions, normals, center, center + 1 + i, center + 2 + i);
        }

        private static void AddQuad(List<int> triangles, List<Vector3> positions, List<Vector3> normals, int a, int b, int c, int d)
        {
            AddTriangle(triangles, positions, normals, a, b, c);
            AddTriangle(triangles, positions, normals, a, c, d);
        }

        /// <summary>Adiciona um triângulo no enrolamento que deixa a face da Unity virada para a normal esperada.</summary>
        private static void AddTriangle(List<int> triangles, List<Vector3> positions, List<Vector3> normals, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            Vector3 expected = normals[a] + normals[b] + normals[c];
            if (face.sqrMagnitude < 1e-12f) return; // triângulo degenerado (ponta de cone)
            if (Vector3.Dot(face, expected) >= 0f)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
        }

        private static Mesh Build(string name, List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            if (positions.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Mesh Store(string key, Mesh mesh)
        {
            Cache[key] = mesh;
            return mesh;
        }
    }
}
