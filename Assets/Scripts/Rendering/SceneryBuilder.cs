using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CandyRush.Rendering
{
    /// <summary>
    /// Ajudantes para montar o cenário por código.
    ///
    /// Part cria uma peça solta (algo que se mexe ou é único).
    /// MeshBatch junta muitas peças paradas do mesmo material numa só malha,
    /// o equivalente ao InstancedMesh da web: centenas de blocos de barreira
    /// viram uma única chamada de desenho, o que importa muito no iPhone.
    /// </summary>
    public static class SceneryBuilder
    {
        /// <summary>Cria uma peça filha de parent. rotation é local.</summary>
        public static Transform Part(Transform parent, string name, Mesh mesh, Material material,
            Vector3 position, Quaternion rotation, Vector3 scale, bool castShadows = true)
        {
            var go = new GameObject(name);
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return t;
        }

        public static Transform Part(Transform parent, string name, Mesh mesh, Material material,
            Vector3 position, bool castShadows = true) =>
            Part(parent, name, mesh, material, position, Quaternion.identity, Vector3.one, castShadows);

        /// <summary>Grupo vazio, para montar peças no referencial de um ponto da pista.</summary>
        public static Transform Group(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation;
            return t;
        }

        /// <summary>
        /// Rotação em radianos no padrão do Three.js (ordem XYZ), como vem no
        /// código da web. Use só para peças simétricas ou já espelhadas.
        /// </summary>
        public static Quaternion Euler(float x, float y, float z) =>
            Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right)
            * Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up)
            * Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward);

        /// <summary>Rotação em torno do eixo vertical, em radianos (rumo).</summary>
        public static Quaternion Yaw(float radians) => Quaternion.AngleAxis(radians * Mathf.Rad2Deg, Vector3.up);
    }

    /// <summary>Junta muitas cópias paradas de malhas, separadas por material, e cria uma malha por material.</summary>
    public sealed class MeshBatch
    {
        private readonly Dictionary<Material, List<CombineInstance>> _parts = new Dictionary<Material, List<CombineInstance>>();

        public void Add(Mesh mesh, Material material, Matrix4x4 transform)
        {
            if (!_parts.TryGetValue(material, out List<CombineInstance> list))
            {
                list = new List<CombineInstance>();
                _parts[material] = list;
            }
            list.Add(new CombineInstance { mesh = mesh, transform = transform });
        }

        public void Add(Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale) =>
            Add(mesh, material, Matrix4x4.TRS(position, rotation, scale));

        /// <summary>Cria os objetos na cena (um por material) como filhos de parent.</summary>
        public void Build(Transform parent, string name, bool castShadows = true)
        {
            foreach (KeyValuePair<Material, List<CombineInstance>> entry in _parts)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(entry.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                SceneryBuilder.Part(parent, $"{name} ({entry.Key.name})", mesh, entry.Key, Vector3.zero, Quaternion.identity, Vector3.one, castShadows);
            }
            _parts.Clear();
        }
    }
}
