using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Normal maps lineares, periódicos e compartilhados entre materiais.</summary>
    public static class SurfaceNormals
    {
        public enum Surface { Grass, Biscuit, Glaze }
        static readonly Texture2D[] cache = new Texture2D[3];

        public static Texture2D Get(Surface surface)
        {
            int index = (int)surface;
            if (cache[index] != null) return cache[index];
            const int size = 256;
            var heights = new float[size * size];
            var random = new System.Random(713 + index);
            // Integer wave frequencies give continuous derivatives across tile edges.
            for (int wave = 0; wave < 24; wave++)
            {
                int fx = random.Next(2, surface == Surface.Glaze ? 12 : 48);
                int fy = random.Next(2, surface == Surface.Grass ? 12 : 48);
                float phase = (float)random.NextDouble() * Mathf.PI * 2;
                float amplitude = 1f / (24 * Mathf.Sqrt(fx * fx + fy * fy));
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        heights[y * size + x] += amplitude * Mathf.Sin((fx * x + fy * y) * (2 * Mathf.PI / size) + phase);
            }
            if (surface == Surface.Biscuit)
                for (int pore = 0; pore < 180; pore++)
                {
                    int cx = random.Next(size), cy = random.Next(size);
                    int radius = random.Next(2, 6);
                    for (int dy = -radius; dy <= radius; dy++)
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            float d = (dx * dx + dy * dy) / (float)(radius * radius);
                            if (d >= 1) continue;
                            int pixel = ((cy + dy + size) % size) * size + (cx + dx + size) % size;
                            heights[pixel] -= .007f * (1 - d) * (1 - d);
                        }
                }
            var pixels = new Color[size * size];
            float At(int x, int y) => heights[((y + size) % size) * size + (x + size) % size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var normal = new Vector3(-(At(x + 1, y) - At(x - 1, y)) * size,
                        -(At(x, y + 1) - At(x, y - 1)) * size, 1).normalized;
                    // RGB + alpha=1 works with Unity's RGB and RG/AG normal unpacking.
                    pixels[y * size + x] = new Color(normal.x * .5f + .5f,
                        normal.y * .5f + .5f, normal.z * .5f + .5f, 1);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "normal-" + surface, wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear, anisoLevel = 8
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return cache[index] = texture;
        }

        public static Mat Apply(Mat material, Surface surface, float strength, float repeat = 1)
        {
            material.Material.SetTexture("_BumpMap", Get(surface));
            material.Material.SetFloat("_BumpScale", strength);
            material.Material.SetTextureScale("_BumpMap", Vector2.one * repeat);
            return material;
        }
    }
}
