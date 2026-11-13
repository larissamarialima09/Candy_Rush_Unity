using UnityEngine;

namespace CandyRush.Integrated
{
    public static class VisualMaterialLibrary
    {
        public static Texture2D FurColor => Resources.Load<Texture2D>("Candy/Visuals/FurColor");
        public static Texture2D FurNormal => Resources.Load<Texture2D>("Candy/Visuals/FurNormal");
        public static Texture2D FurNoise => Resources.Load<Texture2D>("Candy/Visuals/FurNoise");
        public static Texture2D TireNormal => Resources.Load<Texture2D>("Candy/Visuals/TireNormal");

        public static Color ReadColor(Material material)
        {
            if (material == null) return Color.white;
            // glTFast Built-in marks baseColorFactor as [MainColor]. Preserve the
            // imported value when converting to Standard or either fur shader.
            if (material.HasProperty("baseColorFactor")) return material.GetColor("baseColorFactor");
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        }

        public static void Dispose(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
    }
}
