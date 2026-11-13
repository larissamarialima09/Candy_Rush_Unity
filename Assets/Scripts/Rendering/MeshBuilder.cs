using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CandyRush.Rendering
{
    /// <summary>
    /// Acumulador de triângulos com cor e UV por vértice (o RibbonBuilder da web).
    /// Todo o piso do circuito sai daqui, então só existe um jeito de errar o enrolamento.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        public bool IsEmpty => _positions.Count == 0;

        /// <summary>
        /// Um quadrilátero. leftStart/leftEnd são a borda da ESQUERDA no sentido
        /// de corrida (maior deslocamento lateral). Na Unity, a face visível é a
        /// de enrolamento horário visto de cima: esquerda → frente → direita.
        /// </summary>
        public void Quad(Vector3 leftStart, Vector3 rightStart, Vector3 leftEnd, Vector3 rightEnd,
            Color color, float vStart = 0f, float vEnd = 1f)
        {
            Color vertexColor = CandyMaterials.VertexColor(color);
            Triangle(leftStart, leftEnd, rightEnd, new Vector2(0, vStart), new Vector2(0, vEnd), new Vector2(1, vEnd), vertexColor);
            Triangle(leftStart, rightEnd, rightStart, new Vector2(0, vStart), new Vector2(1, vEnd), new Vector2(1, vStart), vertexColor);
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC, Color color)
        {
            int start = _positions.Count;
            _positions.Add(a);
            _positions.Add(b);
            _positions.Add(c);
            _uvs.Add(uvA);
            _uvs.Add(uvB);
            _uvs.Add(uvC);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (_positions.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_positions);
            mesh.SetColors(_colors);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            // A pista tem elevação e inclinação: as normais precisam ser calculadas de verdade.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
