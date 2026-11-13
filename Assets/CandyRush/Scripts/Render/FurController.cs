using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CandyRush.Integrated
{
    /// <summary>One controller per rabbit. Preserves imported materials and animating mesh transforms.</summary>
    [DisallowMultipleComponent]
    public sealed class FurController : MonoBehaviour
    {
        public enum FurMode { Automatic, ShellFur, Lightweight }
        [Serializable]
        public sealed class FurRegion
        {
            public Renderer renderer;
            [Min(0)] public int materialSlot;
            [Tooltip("White = fur; black = eyes, helmet, nose, etc. Uses UV0.")]
            public Texture2D furMask;
            [Tooltip("White = smooth inner ear / nose; black = matte fur. Uses UV0.")]
            public Texture2D smoothnessMask;
        }

        [Header("Quality")]
        public FurMode mode = FurMode.Automatic;
        [Min(0)] public int shellMinimumQuality = 3;
        public bool allowShellOnMobile;
        [Range(8, 16)] public int layerCount = 12;
        [Header("Fur")]
        public Color baseColor = Color.white;
        [Tooltip("Use the portrait palette instead of the GLB coat color.")]
        public bool useCharacterPalette;
        public Color coatColor = Color.white;
        public Color markingsColor = Color.white;
        public Texture2D colorTexture, noiseTexture, normalMap;
        [Range(0, .08f)] public float furLength = .018f;
        [Range(.01f, 1)] public float density = .65f;
        [Range(1, 128)] public float textureTiling = 24;
        public Vector3 furDirection = new Vector3(0, -.35f, 0);
        public Color rimColor = Color.white;
        [Range(0, .25f)] public float rimStrength = .08f;
        [Range(1, 8)] public float rimPower = 3;
        [Header("Regions (empty = imported *_Fur and Belly slots)")]
        public FurRegion[] regions = Array.Empty<FurRegion>();

        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        readonly List<Material> materials = new List<Material>();
        readonly List<GameObject> shells = new List<GameObject>();
        readonly List<Mesh> meshes = new List<Mesh>();
        bool initialized, dirty;
        int quality = -1, builtLayers;
        FurMode appliedMode;
        bool appliedMobile;
        public bool ShellActive { get; private set; }
        public int ShellObjectCount => shells.Count;

        public void SetCharacterColors(Color coat, Color markings)
        {
            useCharacterPalette = true;
            coatColor = coat;
            markingsColor = markings;
            if (initialized) Refresh();
        }

        void Start() { if (!initialized) Initialize(); }
        void OnEnable() { if (initialized) Refresh(); }
        void OnValidate() { layerCount = Mathf.Clamp(layerCount, 8, 16); dirty = true; }
        void Update()
        {
            if (initialized && (dirty || quality != QualitySettings.GetQualityLevel()
                || appliedMode != mode || builtLayers != layerCount || appliedMobile != allowShellOnMobile)) Refresh();
        }

        public void Initialize()
        {
            if (initialized) return;
            if (regions == null || regions.Length == 0)
            {
                var found = new List<FurRegion>();
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                        if (slots[i] != null && (slots[i].name.Contains("_Fur") || slots[i].name.StartsWith("Belly", StringComparison.Ordinal)))
                            found.Add(new FurRegion { renderer = renderer, materialSlot = i });
                }
                regions = found.ToArray();
            }
            foreach (var region in regions)
                if (region != null && region.renderer != null && !originals.ContainsKey(region.renderer))
                    originals.Add(region.renderer, region.renderer.sharedMaterials);
            initialized = true;
            Refresh();
        }

        [ContextMenu("Refresh fur")]
        public void Refresh()
        {
            if (!initialized) { Initialize(); return; }
            Release();
            quality = QualitySettings.GetQualityLevel();
            appliedMode = mode; builtLayers = Mathf.Clamp(layerCount, 8, 16); appliedMobile = allowShellOnMobile;
            dirty = false;
            ShellActive = mode == FurMode.ShellFur || (mode == FurMode.Automatic
                && quality >= shellMinimumQuality && (!Application.isMobilePlatform || allowShellOnMobile));
            Shader lite = Shader.Find("CandyRush/FurLite");
            Shader shell = Shader.Find("CandyRush/ShellFur");
            if (lite == null || shell == null) { Debug.LogError("Candy Rush fur shaders missing.", this); ShellActive = false; return; }
            if (ShellActive && !shell.isSupported) ShellActive = false;
#if UNITY_EDITOR
            // Retain the opaque colored coat when an edited shell shader has errors.
            if (ShellActive && UnityEditor.ShaderUtil.ShaderHasError(shell))
            {
                ShellActive = false;
                Debug.LogWarning("Shell Fur has a shader error; using the lightweight colored coat.", this);
            }
#endif
            foreach (var pair in originals)
            {
                if (pair.Key == null) continue;
                var slots = (Material[])pair.Value.Clone();
                var selected = new List<int>();
                var shellMaterials = new List<Material>();
                foreach (var region in regions)
                {
                    if (region == null || region.renderer != pair.Key || region.materialSlot < 0 || region.materialSlot >= slots.Length) continue;
                    int slot = region.materialSlot;
                    var source = pair.Value[slot];
                    if (source == null || selected.Contains(slot)) continue;
                    Color color = useCharacterPalette
                        ? (source.name.StartsWith("Belly", StringComparison.Ordinal) ? markingsColor : coatColor) * baseColor
                        : VisualMaterialLibrary.ReadColor(source) * baseColor;
                    var baseMat = new Material(lite) { name = source.name + " FurLite (runtime)" };
                    materials.Add(baseMat);
                    baseMat.SetColor("_Color", color);
                    baseMat.SetTexture("_MainTex", colorTexture != null ? colorTexture : VisualMaterialLibrary.FurColor);
                    baseMat.SetTextureScale("_MainTex", Vector2.one * (colorTexture != null ? 1 : textureTiling));
                    baseMat.SetTexture("_BumpMap", normalMap != null ? normalMap : VisualMaterialLibrary.FurNormal);
                    baseMat.SetTextureScale("_BumpMap", Vector2.one * textureTiling);
                    baseMat.SetTexture("_SmoothMask", region.smoothnessMask != null ? region.smoothnessMask : Texture2D.blackTexture);
                    slots[slot] = baseMat;
                    selected.Add(slot);
                    if (!ShellActive) continue;
                    var shellMat = new Material(shell) { name = source.name + " ShellFur (runtime)" };
                    materials.Add(shellMat);
                    shellMat.SetColor("_Color", color);
                    shellMat.SetTexture("_MainTex", colorTexture != null ? colorTexture : Texture2D.whiteTexture);
                    shellMat.SetTexture("_NoiseTex", noiseTexture != null ? noiseTexture : VisualMaterialLibrary.FurNoise);
                    shellMat.SetTexture("_FurMask", region.furMask != null ? region.furMask : Texture2D.whiteTexture);
                    shellMat.SetFloat("_LayerCount", builtLayers);
                    shellMat.SetFloat("_FurLength", furLength);
                    shellMat.SetFloat("_Density", density);
                    shellMat.SetFloat("_NoiseScale", textureTiling);
                    shellMat.SetVector("_FurDirection", furDirection);
                    shellMat.SetColor("_RimColor", rimColor);
                    shellMat.SetFloat("_RimPower", rimPower);
                    shellMat.SetFloat("_RimStrength", Mathf.Clamp(rimStrength, 0, .25f));
                    shellMaterials.Add(shellMat);
                }
                pair.Key.sharedMaterials = slots;
                if (ShellActive && selected.Count > 0) CreateShell(pair.Key, selected, shellMaterials.ToArray());
            }
        }

        void CreateShell(Renderer source, List<int> slots, Material[] shellMaterials)
        {
            var skinned = source as SkinnedMeshRenderer;
            var filter = source.GetComponent<MeshFilter>();
            Mesh original = skinned != null ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
            if (original == null || !original.isReadable)
            {
                Debug.LogWarning("Shell Fur requires a readable MeshFilter or SkinnedMeshRenderer: " + source.name, source);
                return;
            }
            Mesh mesh = BuildShellMesh(original, slots, builtLayers);
            meshes.Add(mesh);
            // The GPU displaces vertices; enlarge CPU bounds so tips do not pop off-screen.
            var bounds = original.bounds;
            bounds.Expand(2 * furLength * (1 + source.transform.worldToLocalMatrix.MultiplyVector(furDirection).magnitude));
            mesh.bounds = bounds;
            var go = new GameObject("Fur shells") { layer = source.gameObject.layer };
            go.transform.SetParent(source.transform, false);
            shells.Add(go);
            Renderer target;
            if (skinned != null)
            {
                var skin = go.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh; skin.bones = skinned.bones; skin.rootBone = skinned.rootBone;
                skin.localBounds = bounds; skin.updateWhenOffscreen = skinned.updateWhenOffscreen;
                target = skin;
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                target = go.AddComponent<MeshRenderer>();
            }
            target.sharedMaterials = shellMaterials;
            target.shadowCastingMode = ShadowCastingMode.Off; // The opaque base casts the silhouette shadow.
            target.receiveShadows = source.receiveShadows;
            target.lightProbeUsage = source.lightProbeUsage;
            target.reflectionProbeUsage = source.reflectionProbeUsage;
        }

        static Mesh BuildShellMesh(Mesh source, List<int> slots, int layers)
        {
            var positions = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var layerUV = new List<Vector2>();
            var weights = new List<BoneWeight>();
            Vector3[] srcPos = source.vertices, srcNormal = source.normals;
            Vector2[] srcUV = source.uv;
            BoneWeight[] srcWeights = source.boneWeights;
            var triangles = new List<int[]>();
            foreach (int slot in slots)
            {
                var indices = new List<int>();
                var srcTriangles = source.GetTriangles(slot);
                for (int layer = 1; layer <= layers; layer++)
                {
                    var remap = new Dictionary<int, int>();
                    foreach (int old in srcTriangles)
                    {
                        if (!remap.TryGetValue(old, out int next))
                        {
                            next = positions.Count; remap.Add(old, next);
                            positions.Add(srcPos[old]); normals.Add(srcNormal[old]);
                            uv.Add(srcUV.Length > old ? srcUV[old] : Vector2.zero);
                            layerUV.Add(new Vector2(layer, 0));
                            if (srcWeights.Length > old) weights.Add(srcWeights[old]);
                        }
                        indices.Add(next);
                    }
                }
                triangles.Add(indices.ToArray());
            }
            var mesh = new Mesh { name = source.name + " Fur shells", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(2, layerUV);
            if (weights.Count == positions.Count) { mesh.boneWeights = weights.ToArray(); mesh.bindposes = source.bindposes; }
            mesh.subMeshCount = triangles.Count;
            for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateTangents();
            return mesh;
        }

        void OnDisable() { Release(); quality = -1; }
        void OnDestroy() { Release(); }
        void Release()
        {
            foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            foreach (var go in shells) { if (go != null) { go.SetActive(false); VisualMaterialLibrary.Dispose(go); } }
            foreach (var mesh in meshes) VisualMaterialLibrary.Dispose(mesh);
            foreach (var material in materials) VisualMaterialLibrary.Dispose(material);
            shells.Clear(); meshes.Clear(); materials.Clear(); ShellActive = false;
        }
    }
}
