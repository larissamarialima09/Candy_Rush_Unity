using UnityEngine;

namespace CandyRush.Rendering
{
    /// <summary>
    /// Texturas desenhadas por código (convertido de src/fx/proceduralTextures.ts
    /// e src/track/meadowTexture.ts). Na web elas eram pintadas num canvas; aqui
    /// os pixels são escritos direto numa Texture2D.
    ///
    /// Usam um gerador com semente: a textura é idêntica a cada execução.
    /// </summary>
    public static class ProceduralTextures
    {
        private const int Size = 512;

        /// <summary>Asfalto rosa com granulado sutil e confete discreto da mesma família de cor.</summary>
        public static Texture2D CandyTrack(int seed = 7)
        {
            var random = new System.Random(seed);
            Color baseColor = new Color32(0xf0, 0x67, 0x9f, 0xff);
            var pixels = new Color[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = baseColor;

            // Granulado sutil, para a pista não ficar um plástico chapado.
            var light = new Color(1f, 1f, 1f);
            var dark = new Color(190f / 255f, 110f / 255f, 150f / 255f);
            for (int i = 0; i < 5200; i++)
            {
                float alpha = 0.03f + (float)random.NextDouble() * 0.05f;
                Color tint = random.NextDouble() > 0.5 ? light : dark;
                BlendRect(pixels, random.Next(Size), random.Next(Size), 2, 2, tint, alpha);
            }

            // Confete discreto: branco e rosas claros, com 40% de opacidade.
            Color[] confetti = { new Color32(0xff, 0xff, 0xff, 0xff), new Color32(0xff, 0xd7, 0xe8, 0xff), new Color32(0xff, 0xc0, 0xdd, 0xff) };
            for (int i = 0; i < 46; i++)
            {
                float radius = 2.5f + (float)random.NextDouble() * 4f;
                BlendCircle(pixels, (float)random.NextDouble() * Size, (float)random.NextDouble() * Size, radius, confetti[i % confetti.Length], 0.4f);
            }

            return Finish(pixels, "CandyTrack");
        }

        /// <summary>Grama pastel: degradê diagonal, faixas suaves e pontinhos claros e escuros.</summary>
        public static Texture2D Meadow(int seed = 11)
        {
            var random = new System.Random(seed);
            var pixels = new Color[Size * Size];
            Color a = new Color32(0xd7, 0xf5, 0xbe, 0xff);
            Color b = new Color32(0xbf, 0xe5, 0x96, 0xff);
            Color c = new Color32(0xe9, 0xf8, 0xd4, 0xff);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float t = (x + y) / (2f * Size);
                    Color color = t < 0.48f ? Color.Lerp(a, b, t / 0.48f) : Color.Lerp(b, c, (t - 0.48f) / 0.52f);
                    pixels[y * Size + x] = color;
                }
            }

            // Faixas diagonais suaves, alternando dois verdes.
            Color stripeA = new Color32(0xcc, 0xee, 0xad, 0xff);
            Color stripeB = new Color32(0xee, 0xfa, 0xd9, 0xff);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Linha inclinada: sobe 84 px ao atravessar a textura, com 34 px de espessura.
                    float shifted = y - x * 84f / Size + 96f;
                    int band = Mathf.FloorToInt(shifted / 64f);
                    float within = shifted - band * 64f;
                    if (within > 34f) continue;
                    Color stripe = band % 2 == 0 ? stripeA : stripeB;
                    pixels[y * Size + x] = Color.Lerp(pixels[y * Size + x], stripe, 0.35f);
                }
            }

            var darkDot = new Color(74f / 255f, 137f / 255f, 69f / 255f);
            var lightDot = new Color(1f, 1f, 245f / 255f);
            for (int i = 0; i < 4200; i++)
            {
                float alpha = 0.05f + (float)random.NextDouble() * 0.08f;
                BlendRect(pixels, random.Next(Size), random.Next(Size), 1, 1, random.NextDouble() > 0.5 ? darkDot : lightDot, alpha);
            }

            return Finish(pixels, "Meadow");
        }

        /// <summary>Listras diagonais de bengala (duas cores).</summary>
        public static Texture2D Stripe(Color colorA, Color colorB)
        {
            const int size = 128;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Linhas de 26 px, a cada 52 px, a 45 graus (como no canvas da web).
                    float d = ((x - y) % 52 + 52) % 52;
                    pixels[y * size + x] = d < 26f ? colorA : colorB;
                }
            }
            return Finish(pixels, "Stripe", size);
        }

        /// <summary>Espiral de pirulito: faixas coloridas enroladas a partir do centro.</summary>
        public static Texture2D Swirl(params Color[] colors)
        {
            if (colors == null || colors.Length == 0)
                colors = new[] { Hex(0xff5c96), Color.white, Hex(0xffd24d), Hex(0x63d0e8) };

            const int size = 256;
            const float turns = 4.5f;
            const float maxRadius = 122f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - 128f, dy = y - 128f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    if (radius > maxRadius - 2f)
                    {
                        pixels[y * size + x] = Color.white; // borda branca do pirulito
                        continue;
                    }
                    // Em que faixa da espiral este ponto cai.
                    float angle = Mathf.Atan2(dy, dx);
                    float t = radius / maxRadius * turns - angle / (Mathf.PI * 2f);
                    float band = t - Mathf.Floor(t);
                    int index = Mathf.FloorToInt(band * colors.Length) % colors.Length;
                    pixels[y * size + x] = colors[index];
                }
            }
            return Finish(pixels, "Swirl", size);
        }

        /// <summary>Xadrez de bandeira de chegada.</summary>
        public static Texture2D Checker(int squares, Color colorA, Color colorB)
        {
            const int size = 128;
            var pixels = new Color[size * size];
            float cell = (float)size / squares;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int row = Mathf.FloorToInt(y / cell), column = Mathf.FloorToInt(x / cell);
                    pixels[y * size + x] = (row + column) % 2 == 0 ? colorA : colorB;
                }
            }
            Texture2D texture = Finish(pixels, "Checker", size);
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        /// <summary>
        /// Fundo do letreiro: moldura creme arredondada, miolo rosa e fileiras de
        /// granulado em cima e embaixo. O texto vai por cima, em 3D (TextMesh).
        /// </summary>
        public static Texture2D SignBackground(Color background)
        {
            const int width = 1024, height = 256;
            var pixels = new Color[width * height];
            var frame = Hex(0xfff6fa);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0, 0, 0, 0);

            FillRoundRect(pixels, width, height, 6, 6, 1012, 244, 72, frame);
            FillRoundRect(pixels, width, height, 28, 28, 968, 200, 54, background);

            Color[] sprinkles = { Color.white, Hex(0xffd94f), Hex(0x86d6f5), Hex(0x8fe3b4), Hex(0xc6a4f4) };
            for (int i = 0; i < 26; i++)
            {
                float x = 70f + i / 25f * 884f;
                foreach (float y in new[] { 46f, 210f })
                {
                    for (int py = -6; py <= 6; py++)
                    for (int px = -6; px <= 6; px++)
                    {
                        if (px * px + py * py > 36) continue;
                        int ix = Mathf.RoundToInt(x) + px, iy = height - 1 - (Mathf.RoundToInt(y) + py);
                        pixels[iy * width + ix] = sprinkles[i % sprinkles.Length];
                    }
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
            {
                name = "SignBackground",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static void FillRoundRect(Color[] pixels, int width, int height, int x0, int y0, int w, int h, int radius, Color color)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                for (int x = x0; x < x0 + w; x++)
                {
                    // Distância até o retângulo interno (cantos arredondados).
                    float cx = Mathf.Clamp(x, x0 + radius, x0 + w - radius);
                    float cy = Mathf.Clamp(y, y0 + radius, y0 + h - radius);
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > radius * radius) continue;
                    pixels[(height - 1 - y) * width + x] = color;
                }
            }
        }

        public static Color Hex(int rgb) => Config.CircuitConfig.Hex(rgb);

        private static Texture2D Finish(Color[] pixels, string name, int size = Size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static void BlendRect(Color[] pixels, int x0, int y0, int width, int height, Color color, float alpha)
        {
            for (int y = y0; y < y0 + height; y++)
            {
                for (int x = x0; x < x0 + width; x++)
                {
                    int index = Wrap(y) * Size + Wrap(x);
                    pixels[index] = Color.Lerp(pixels[index], color, alpha);
                }
            }
        }

        private static void BlendCircle(Color[] pixels, float cx, float cy, float radius, Color color, float alpha)
        {
            int r = Mathf.CeilToInt(radius);
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y > radius * radius) continue;
                    int index = Wrap(Mathf.FloorToInt(cy) + y) * Size + Wrap(Mathf.FloorToInt(cx) + x);
                    pixels[index] = Color.Lerp(pixels[index], color, alpha);
                }
            }
        }

        private static int Wrap(int value) => ((value % Size) + Size) % Size;
    }
}
