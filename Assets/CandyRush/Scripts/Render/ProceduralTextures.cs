using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>
    /// Texturas desenhadas na hora de carregar (fx/proceduralTextures.ts e
    /// track/meadowTexture.ts), feitas no <see cref="Canvas2D"/>.
    ///
    /// O original usava `Math.random`, então cada recarga de página dava um
    /// confete diferente. Aqui o sorteio tem semente fixa: a textura é sempre a
    /// mesma, o que facilita comparar capturas de tela.
    /// </summary>
    public static class ProceduralTextures
    {
        static System.Random random = new System.Random(20240917);
        static float Rand() => (float)random.NextDouble();

        static string Rgba(int r, int g, int b, float a) => $"#{r:x2}{g:x2}{b:x2}{Mathf.RoundToInt(Mathf.Clamp01(a) * 255):x2}";

        /// <summary>Asfalto de bala: base rosa com confete da mesma família.</summary>
        public static Texture2D CandyTrack(string baseColor = "#f0679f", string[] confetti = null)
        {
            confetti ??= new[] { "#ffffff", "#ffd7e8", "#ffc0dd" };
            var c = new Canvas2D(512, 512);
            c.Fill(baseColor);
            c.FillRect(0, 0, 512, 512);

            for (int i = 0; i < 5200; i++)
            {
                float alpha = 0.03f + Rand() * 0.05f;
                c.Fill(Rand() > 0.5f ? Rgba(255, 255, 255, alpha) : Rgba(190, 110, 150, alpha));
                c.FillRect(Rand() * 512, Rand() * 512, 2, 2);
            }

            for (int i = 0; i < 46; i++)
            {
                c.Fill(confetti[i % confetti.Length]);
                c.GlobalAlpha = 0.4f;
                float radius = 2.5f + Rand() * 4;
                c.FillCircle(Rand() * 512, Rand() * 512, radius);
            }
            c.GlobalAlpha = 1;
            return c.ToTexture("candy-track");
        }

        /// <summary>Mapa de normais do asfalto: confetes em alto-relevo (Sobel sobre um campo de altura).</summary>
        public static Texture2D CandyNormalMap()
        {
            const int size = 512;
            var c = new Canvas2D(size, size);
            c.Fill("#808080");
            c.FillRect(0, 0, size, size);

            for (int i = 0; i < 90; i++)
            {
                float x = Rand() * size, y = Rand() * size, radius = 3 + Rand() * 5;
                c.FillGradient = Canvas2D.Radial(x, y, 0, radius).AddStop(0, "#ffffff").AddStop(1, "#808080");
                c.BeginPath();
                c.Circle(x, y, radius);
                c.FillPath();
            }
            c.FillGradient = null;

            var height = new float[size * size];
            for (int i = 0; i < height.Length; i++)
            {
                float noise = (Rand() - 0.5f) * 14f / 255f;
                height[i] = Mathf.Clamp01(c.Pixels[i].r + noise);
            }

            float At(int x, int y) => height[((y + size) % size) * size + ((x + size) % size)];
            const float strength = 2.4f;
            var n = new Canvas2D(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = At(x + 1, y) - At(x - 1, y);
                    float dy = At(x, y + 1) - At(x, y - 1);
                    var normal = new Vector3(-dx * strength, -dy * strength, 1).normalized;
                    // Canvas tem y para baixo e a textura será invertida na
                    // entrega; o verde (y) troca de sinal para continuar certo.
                    n.Pixels[y * size + x] = new Color(normal.x * 0.5f + 0.5f, -normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1);
                }
            }
            return n.ToTexture("candy-normal", true, true);
        }

        /// <summary>Espiral de pirulito: uma faixa em espiral de Arquimedes por cor.</summary>
        public static Texture2D Swirl(string[] colors = null)
        {
            colors ??= new[] { "#ff5c96", "#ffffff", "#ffd24d", "#63d0e8" };
            var c = new Canvas2D(256, 256);
            c.Fill("#ffffff");
            c.FillRect(0, 0, 256, 256);

            const float center = 128, turns = 4.5f, maxRadius = 122;
            c.LineWidth = maxRadius / turns / 1.6f;
            c.RoundCaps = true;

            for (int band = 0; band < colors.Length; band++)
            {
                c.Stroke(colors[band]);
                c.BeginPath();
                bool started = false;
                for (float t = 0; t <= turns * Mathf.PI * 2; t += 0.05f)
                {
                    float radius = t / (turns * Mathf.PI * 2) * maxRadius;
                    float angle = t + (float)band / colors.Length * Mathf.PI * 2;
                    float x = center + Mathf.Cos(angle) * radius;
                    float y = center + Mathf.Sin(angle) * radius;
                    if (!started) { c.MoveTo(x, y); started = true; }
                    else c.LineTo(x, y);
                }
                c.StrokePath();
            }

            c.Stroke("#ffffff");
            c.LineWidth = 10;
            c.BeginPath();
            c.Arc(center, center, maxRadius + 2, 0, Mathf.PI * 2);
            c.StrokePath();
            return c.ToTexture("swirl");
        }

        /// <summary>Listra diagonal de bala, para barreiras, postes e bengalas.</summary>
        public static Texture2D Stripe(string colorA = "#ff8fb1", string colorB = "#fff4f7")
        {
            var c = new Canvas2D(128, 128);
            c.Fill(colorB);
            c.FillRect(0, 0, 128, 128);
            c.Stroke(colorA);
            c.LineWidth = 26;
            c.RoundCaps = false;
            for (int x = -128; x < 256; x += 52)
            {
                c.BeginPath();
                c.MoveTo(x, 0);
                c.LineTo(x + 128, 128);
                c.StrokePath();
            }
            return c.ToTexture($"stripe-{colorA}");
        }

        /// <summary>Bandeira quadriculada.</summary>
        public static Texture2D Checker(int squares = 4, string colorA = "#1d1d26", string colorB = "#ffffff")
        {
            var c = new Canvas2D(128, 128);
            float cell = 128f / squares;
            for (int row = 0; row < squares; row++)
            {
                for (int column = 0; column < squares; column++)
                {
                    c.Fill((row + column) % 2 == 0 ? colorA : colorB);
                    c.FillRect(column * cell, row * cell, cell, cell);
                }
            }
            return c.ToTexture("checker");
        }

        /// <summary>Placa de letreiro 4:1 com moldura de glacê, confeitos e texto contornado.</summary>
        public static Texture2D Sign(string text, string background = "#ff5c95", string ink = "#ffffff")
        {
            var c = new Canvas2D(1024, 256);
            c.Fill("#fff6fa");
            c.FillRoundRect(6, 6, 1012, 244, 72);
            c.Fill(background);
            c.FillRoundRect(28, 28, 968, 200, 54);

            string[] sprinkles = { "#ffffff", "#ffd94f", "#86d6f5", "#8fe3b4", "#c6a4f4" };
            for (int i = 0; i < 26; i++)
            {
                float x = 70 + i / 25f * 884;
                c.Fill(sprinkles[i % sprinkles.Length]);
                c.FillCircle(x, 46, 6);
                c.FillCircle(x, 210, 6);
            }

            c.Text(text, 512, 132, 840, 132, Hex.Css(ink), Hex.Css("#b8245f"), 16);
            return c.ToTexture($"sign-{text}", false);
        }

        /// <summary>Chão de marshmallow: faixas apagadas e confeitos miúdos.</summary>
        public static Texture2D Ground()
        {
            var c = new Canvas2D(256, 256);
            c.Fill("#ffffff");
            c.FillRect(0, 0, 256, 256);
            c.Fill(Rgba(228, 246, 234, 0.55f));
            for (int y = 0; y < 256; y += 64) c.FillRect(0, y, 256, 32);

            string[] sprinkles = { "#d9f0e2", "#ffe6ef", "#e6ecff", "#fff3d6" };
            for (int i = 0; i < 260; i++)
            {
                c.GlobalAlpha = 0.5f + Rand() * 0.4f;
                c.Fill(sprinkles[i % sprinkles.Length]);
                float x = Rand() * 256, y = Rand() * 256, size = 1.2f + Rand() * 2.4f;
                c.FillEllipse(x, y, size, size * 0.6f, Rand() * Mathf.PI);
            }
            c.GlobalAlpha = 1;
            return c.ToTexture("ground");
        }

        /// <summary>Gramado do terreno e do plano de horizonte (track/meadowTexture.ts).</summary>
        public static Texture2D Meadow()
        {
            var c = new Canvas2D(512, 512);
            // Verde de campo ao sol: saturado, com a base mais escura e o topo mais claro.
            c.FillGradient = Canvas2D.Linear(0, 0, 512, 512)
                .AddStop(0, "#6fb84f").AddStop(0.5f, "#8dcd62").AddStop(1, "#5ea946");
            c.FillRect(0, 0, 512, 512);
            c.FillGradient = null;

            // Manchas grandes e suaves de luz e sombra: é a variação de tom que faz o terreno parecer natural.
            for (int i = 0; i < 260; i++)
            {
                c.Fill(Rand() > 0.5f ? Rgba(190, 240, 130, 0.10f + Rand() * 0.08f) : Rgba(46, 110, 40, 0.10f + Rand() * 0.10f));
                float x = Rand() * 512, y = Rand() * 512, rx = 18 + Rand() * 46, ry = rx * (0.5f + Rand() * 0.4f), rot = Rand() * Mathf.PI;
                // Repete as manchas nas bordas para a textura ladrilhar sem costura.
                for (int dx = -512; dx <= 512; dx += 512)
                    for (int dy = -512; dy <= 512; dy += 512)
                        c.FillEllipse(x + dx, y + dy, rx, ry, rot);
            }

            // Lâminas de capim: muitas, curtas, quase verticais, com tons escuros e claros.
            c.RoundCaps = true;
            string[] bladeDark = { "#3b7d33", "#4c9440", "#2f6b2b" };
            string[] bladeLight = { "#8fd06a", "#a8e07c", "#bfeb94" };
            for (int i = 0; i < 9000; i++)
            {
                float x = Rand() * 512, y = Rand() * 512;
                float length = 4 + Rand() * 9;
                float angle = -Mathf.PI / 2 + (Rand() - 0.5f) * 0.9f;
                c.Stroke(Rand() > 0.35f ? bladeDark[i % bladeDark.Length] : bladeLight[i % bladeLight.Length]);
                c.GlobalAlpha = 0.45f + Rand() * 0.4f;
                c.LineWidth = 0.8f + Rand() * 0.9f;
                c.BeginPath();
                c.MoveTo(x, y);
                c.LineTo(x + Mathf.Cos(angle) * length, y + Mathf.Sin(angle) * length);
                c.StrokePath();
            }
            c.GlobalAlpha = 1;

            string[] flowers = { "#fff3a8", "#ffc7dd", "#d6c4ff", "#ffffff" };
            for (int i = 0; i < 58; i++)
            {
                float x = Rand() * 512, y = Rand() * 512, radius = 1.5f + Rand() * 2.3f;
                c.Fill(flowers[i % flowers.Length]);
                c.GlobalAlpha = 0.6f;
                c.FillCircle(x, y, radius);
            }
            c.GlobalAlpha = 1;
            return c.ToTexture("meadow");
        }

        /// <summary>Disco macio para as partículas (o original usava um círculo de 8 lados).</summary>
        public static Texture2D SoftDisc()
        {
            var c = new Canvas2D(64, 64);
            c.Fill("#ffffff");
            c.FillCircle(32, 32, 30);
            // Pré-multiplicado: a mistura aditiva só olha o RGB, então a borda
            // antisserrilhada tem que estar nele, não no alfa.
            for (int i = 0; i < c.Pixels.Length; i++)
            {
                Color p = c.Pixels[i];
                c.Pixels[i] = new Color(p.r * p.a, p.g * p.a, p.b * p.a, p.a);
            }
            return c.ToTexture("soft-disc", false);
        }

        /// <summary>
        /// Brilho radial branco->transparente, para as lanternas/escapamento dos
        /// karts sprite. Ao contrário de <see cref="SoftDisc"/>, o alfa NÃO é
        /// pré-multiplicado: esse aqui é lido por um material de alpha blend
        /// normal (_SrcBlend = SrcAlpha), não aditivo.
        /// </summary>
        public static Texture2D Glow()
        {
            var c = new Canvas2D(64, 64);
            c.FillGradient = Canvas2D.Radial(32, 32, 0, 32).AddStop(0, "#ffffffff").AddStop(1, "#ffffff00");
            c.FillCircle(32, 32, 32);
            return c.ToTexture("glow", false);
        }
    }
}
