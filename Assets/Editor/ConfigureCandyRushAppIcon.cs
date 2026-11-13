using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class ConfigureCandyRushAppIcon
{
    const string SourcePath = "Assets/AppIconSource/Coelhinho Correndo no Candy Rush.png";

    [MenuItem("Candy Rush/Set iOS App Icon From Source")]
    public static void Apply()
    {
        AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
        if (source == null)
            throw new System.InvalidOperationException("AppIconSource image could not be imported.");

        PlatformIconKind[] kinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS);
        int assigned = 0;
        foreach (PlatformIconKind kind in kinds)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
            if (icons.Length == 0) continue;
            foreach (PlatformIcon icon in icons)
            {
                icon.SetTextures(new[] { source });
                assigned++;
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"CANDY_RUSH_IOS_ICONS_ASSIGNED: {assigned} slots from {SourcePath}");
    }
}
