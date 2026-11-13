using System.IO;
using CandyRush.Integrated;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>Creates persistent, editable profiles and explicit build references to PP shaders.</summary>
[InitializeOnLoad]
public sealed class CandyVisualAssets : IPreprocessBuildWithReport
{
    const string Folder = "Assets/CandyRush/Resources/Candy/Visuals";
    public int callbackOrder => 0;
    static CandyVisualAssets() { EditorApplication.delayCall += EnsureAssets; }

    [MenuItem("CandyRush/Visuals/Create missing assets")]
    public static void EnsureAssets()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += EnsureAssets; return; }
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        string resourcePath = Folder + "/PostProcessResources.asset";
        if (!File.Exists(resourcePath) && !AssetDatabase.CopyAsset(
            "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset", resourcePath))
            throw new BuildFailedException("Unable to copy official PostProcessResources. Install com.unity.postprocessing 3.5.4.");
        CreateProfile("DesktopPostProcessing", false);
        CreateProfile("MobilePostProcessing", true);
        CreateReflections();
        CreateMaterial("KartBody", new Color(.18f, .55f, 1), 0, .88f);
        CreateMaterial("KartTire", new Color(.055f, .055f, .065f), 0, .13f);
        CreateMaterial("KartRim", new Color(.7f, .7f, .75f), .92f, .55f);
        AssetDatabase.SaveAssets();
    }

    static void CreateMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = Folder + "/" + name + ".mat";
        if (File.Exists(path)) return;
        var material = new Material(Shader.Find("Standard")) { name = name };
        material.SetColor("_Color", color); material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        if (name == "KartTire")
        {
            material.SetTexture("_BumpMap", VisualMaterialLibrary.TireNormal);
            material.SetTextureScale("_BumpMap", new Vector2(5, 2));
            material.SetFloat("_BumpScale", .65f); material.EnableKeyword("_NORMALMAP");
        }
        AssetDatabase.CreateAsset(material, path);
    }

    static void CreateProfile(string name, bool mobile)
    {
        string path = Folder + "/" + name + ".asset";
        if (File.Exists(path)) return;
        var profile = CandyPostProcessing.CreateProfile(mobile);
        profile.name = name;
        AssetDatabase.CreateAsset(profile, path);
        foreach (var setting in profile.settings)
        {
            setting.name = setting.GetType().Name;
            AssetDatabase.AddObjectToAsset(setting, profile);
        }
        EditorUtility.SetDirty(profile);
    }

    static void CreateReflections()
    {
        string path = Folder + "/CandyReflections.cubemap";
        if (File.Exists(path)) return;
        const int size = 64;
        var cube = new Cubemap(size, TextureFormat.RGBAHalf, true) { name = "CandyReflections" };
        Vector3 light = new Vector3(-.3f, .8f, .5f).normalized;
        for (int face = 0; face < 6; face++)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = 2 * (x + .5f) / size - 1, v = 2 * (y + .5f) / size - 1;
                    Vector3 dir;
                    switch ((CubemapFace)face)
                    {
                        case CubemapFace.PositiveX: dir = new Vector3(1, -v, -u); break;
                        case CubemapFace.NegativeX: dir = new Vector3(-1, -v, u); break;
                        case CubemapFace.PositiveY: dir = new Vector3(u, 1, v); break;
                        case CubemapFace.NegativeY: dir = new Vector3(u, -1, -v); break;
                        case CubemapFace.PositiveZ: dir = new Vector3(u, -v, 1); break;
                        default: dir = new Vector3(-u, -v, -1); break;
                    }
                    dir.Normalize();
                    Color color = Color.Lerp(new Color(.32f, .2f, .16f), new Color(.65f, .8f, 1), Mathf.Clamp01(dir.y * .5f + .5f));
                    color += Color.white * Mathf.Pow(Mathf.Max(0, Vector3.Dot(dir, light)), 64) * 2;
                    pixels[y * size + x] = color;
                }
            cube.SetPixels(pixels, (CubemapFace)face);
        }
        cube.Apply(true, false);
        // Averaged mipmaps provide inexpensive approximate rough reflections.
        cube.SmoothEdges();
        AssetDatabase.CreateAsset(cube, path);
    }

    public void OnPreprocessBuild(BuildReport report) { EnsureAssets(); }
}
