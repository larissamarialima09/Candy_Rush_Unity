using System.Collections.Generic;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>
    /// Arte da loja desenhada no Canvas2D: ícones dos itens, cadeado, check,
    /// pedestal de bolo e moldura de seleção. Tudo é gerado uma vez e cacheado.
    /// </summary>
    public static class ShopArt
    {
        static readonly Dictionary<string, Texture2D> icones = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Texture2D, Sprite> sprites = new Dictionary<Texture2D, Sprite>();
        static Texture2D cadeado, check, pedestal, moldura;
        static Sprite[] coelhos;

        static readonly Color[] CoresGranulados = { Hex.C(0xffe066), Hex.C(0x66d9ff), Hex.C(0x8ef07a), Color.white, Hex.C(0xb388ff) };

        public static Texture2D Icone(string id)
        {
            if (!icones.TryGetValue(id, out var tex))
            {
                tex = Desenhar(id);
                icones[id] = tex;
            }
            return tex;
        }

        /// <summary>
        /// Ícone de pintura (KARTS: faixa colorida) ou de aro (RODAS: anel colorido),
        /// para itens sem modelo 2D. Cacheado pelo id do item.
        /// </summary>
        public static Texture2D Amostra(ShopItem item)
        {
            if (!icones.TryGetValue(item.Id, out var tex))
            {
                var c = new Canvas2D(256, 200);
                if (item.Categoria == ShopCategoria.Roda)
                {
                    c.Fill(item.Cor); c.FillCircle(128, 100, 84);
                    Furo(c, 128, 100, 52);
                    c.Fill("#3b1d55"); c.FillCircle(128, 100, 14);
                }
                else
                {
                    c.FillGradient = Canvas2D.Linear(0, 52, 0, 148).AddStop(0, item.Cor).AddStop(1, Color.Lerp(item.Cor, Color.black, .25f));
                    c.FillRoundRect(28, 52, 200, 96, 40);
                    c.Fill(new Color(1, 1, 1, .55f)); c.FillRoundRect(56, 68, 70, 14, 7);
                }
                tex = c.ToTexture("loja-" + item.Id, false, false, false);
                icones[item.Id] = tex;
            }
            return tex;
        }

        public static Texture2D Cadeado => cadeado ??= DesenharCadeado();
        public static Texture2D Check => check ??= DesenharCheck();
        public static Texture2D Pedestal => pedestal ??= DesenharPedestal();
        public static Texture2D Moldura => moldura ??= DesenharMoldura();

        /// <summary>Sprite do coelho `indice` (0 a 3) no atlas do menu.</summary>
        public static Sprite Coelho(int indice)
        {
            if (coelhos == null)
            {
                coelhos = new Sprite[4];
                var atlas = Resources.Load<Texture2D>("Candy/Menu/Rabbits");
                if (atlas != null)
                    for (int i = 0; i < 4; i++)
                        coelhos[i] = Sprite.Create(atlas, new Rect(i * atlas.width / 4f, 0, atlas.width / 4f, atlas.height), Vector2.one * .5f);
            }
            return coelhos[Mathf.Clamp(indice, 0, 3)];
        }

        /// <summary>Sprite cacheado por textura. `borda` só vale na primeira chamada de cada textura.</summary>
        public static Sprite ParaSprite(Texture2D tex, Vector4 borda = default)
        {
            if (tex == null) return null;
            if (!sprites.TryGetValue(tex, out var sprite))
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * .5f, 100f, 0,
                    SpriteMeshType.FullRect, borda);
                sprites[tex] = sprite;
            }
            return sprite;
        }

        static Texture2D Desenhar(string id)
        {
            var c = new Canvas2D(256, 200);
            switch (id)
            {
                case "cupcake": Cupcake(c); break;
                case "unicornio": Unicornio(c); break;
                case "rosquinha": Rosquinha(c); break;
                case "coroa": Coroa(c); break;
                case "cachecol": Cachecol(c); break;
                default: Misterioso(c); break;
            }
            return c.ToTexture("loja-" + id, false, false, false);
        }

        // Capacetes: domo de 28 a 228 em x, de 40 a 180 em y. Viseira no meio, aro embaixo.
        // As orelhas do coelho passam por cima do domo, então a crina e o pico ficam nas laterais.

        static void Cupula(Canvas2D c, string topo, string fundo)
        {
            c.FillGradient = Canvas2D.Linear(0, 40, 0, 180).AddStop(0, topo).AddStop(1, fundo);
            c.FillEllipse(128, 110, 100, 70, 0);
        }

        static void Viseira(Canvas2D c, string cor)
        {
            c.Fill(cor); c.FillEllipse(128, 122, 66, 22, 0);
            c.Fill(new Color(1, 1, 1, .55f)); c.FillEllipse(100, 113, 22, 5, 0);
        }

        static void Aro(Canvas2D c, string cor)
        {
            c.Fill(cor); c.FillRoundRect(24, 150, 208, 34, 14);
        }

        static void Granulados(Canvas2D c, params (float x, float y)[] pontos)
        {
            for (int i = 0; i < pontos.Length; i++)
            {
                c.Fill(CoresGranulados[i % CoresGranulados.Length]);
                c.FillRoundRect(pontos[i].x - 5, pontos[i].y - 2, 10, 4, 2);
            }
        }

        static void Cupcake(Canvas2D c)
        {
            Cupula(c, "#ffc2e0", "#ff5fa8");
            Viseira(c, "#3b1d55");
            Aro(c, "#d93f86");
            // glacê em espiral, cereja e granulados
            c.Fill("#fff7fb"); c.FillEllipse(128, 62, 92, 30, 0);
            c.FillEllipse(128, 50, 66, 26, 0);
            Granulados(c, (92, 62), (112, 52), (136, 50), (160, 60), (78, 70), (124, 68), (176, 70));
            c.Fill("#e8103a"); c.FillCircle(128, 22, 16);
            c.Fill(new Color(1, 1, 1, .7f)); c.FillCircle(122, 17, 4);
        }

        static void Unicornio(Canvas2D c)
        {
            Crina(c, "#b388ff", 2);
            Crina(c, "#66d9ff", 1);
            Crina(c, "#ff7ac6", 0);
            Cupula(c, "#ffffff", "#ffd6ec");
            Viseira(c, "#2d1a4a");
            Aro(c, "#a77bd6");
            // chifre dourado com a ponta em espiral
            c.FillGradient = Canvas2D.Linear(0, 4, 0, 60).AddStop(0, "#fff4b0").AddStop(1, "#e0a526");
            c.BeginPath(); c.MoveTo(114, 60); c.LineTo(128, 6); c.LineTo(142, 60); c.ClosePath(); c.FillPath();
            c.Fill("#fff7c2"); c.FillCircle(72, 96, 5); c.FillCircle(186, 96, 4);
        }

        /// <summary>Três mechas da crina, de cada lado, do topo até o aro.</summary>
        static void Crina(Canvas2D c, string cor, float deslocamento)
        {
            c.Stroke(cor); c.LineWidth = 12;
            foreach (float lado in new[] { -1f, 1f })
            {
                c.BeginPath();
                c.MoveTo(128 + lado * (64 - deslocamento * 10), 70 + deslocamento * 8);
                c.QuadraticCurveTo(128 + lado * (86 - deslocamento * 6), 120, 128 + lado * (76 - deslocamento * 8), 186);
                c.StrokePath();
            }
        }

        static void Rosquinha(Canvas2D c)
        {
            // duas argolas com furo, ponte no meio
            c.FillGradient = Canvas2D.Linear(0, 40, 0, 170).AddStop(0, "#ffb3d9").AddStop(1, "#ff5ca8");
            c.FillCircle(62, 108, 50);
            c.FillCircle(194, 108, 50);
            Furo(c, 62, 108, 24);
            Furo(c, 194, 108, 24);
            c.Fill("#3b1d55"); c.FillRoundRect(106, 100, 44, 14, 7);
            var pontos = new List<(float, float)>();
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2 / 10f;
                pontos.Add((62 + Mathf.Cos(a) * 37, 108 + Mathf.Sin(a) * 37));
                pontos.Add((194 + Mathf.Cos(a) * 37, 108 + Mathf.Sin(a) * 37));
            }
            Granulados(c, pontos.ToArray());
        }

        /// <summary>Deixa transparente o miolo de um círculo.</summary>
        static void Furo(Canvas2D c, float cx, float cy, float r)
        {
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    float dx = x + .5f - cx, dy = y + .5f - cy;
                    if (dx * dx + dy * dy < r * r) c.Pixels[y * c.Width + x] = Color.clear;
                }
        }

        static void Coroa(Canvas2D c)
        {
            // aro dourado, pontas com joias e base rosa
            c.Fill("#f5a623"); c.FillRoundRect(24, 140, 208, 46, 18);
            c.FillGradient = Canvas2D.Linear(0, 34, 0, 150).AddStop(0, "#fff3a8").AddStop(1, "#e8a020");
            c.BeginPath();
            c.MoveTo(30, 150); c.LineTo(22, 64); c.LineTo(82, 108); c.LineTo(128, 34);
            c.LineTo(174, 108); c.LineTo(234, 64); c.LineTo(226, 150); c.ClosePath();
            c.FillPath();
            c.FillCircle(22, 64, 9); c.FillCircle(128, 34, 10); c.FillCircle(234, 64, 9);
            c.Fill("#ff5fa0"); c.FillRoundRect(40, 150, 176, 26, 12);
            c.Fill("#ff3d7f"); c.FillCircle(128, 104, 11);
            c.Fill("#4fc3ff"); c.FillCircle(82, 124, 8); c.FillCircle(174, 124, 8);
        }

        /// <summary>Cachecol de alcaçuz: faixas rosa e preto numa linha inclinada.</summary>
        static void Cachecol(Canvas2D c)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = 14 + i * 20, b = a + 21;
                c.Fill(i % 2 == 0 ? Hex.C(0xff5fa0) : Hex.C(0x2b1b2f));
                c.BeginPath();
                c.MoveTo(a, Eixo(a) - 30); c.LineTo(b, Eixo(b) - 30);
                c.LineTo(b, Eixo(b) + 30); c.LineTo(a, Eixo(a) + 30);
                c.ClosePath(); c.FillPath();
            }
        }

        static float Eixo(float x) => 100 + (x - 128) * .18f;

        static void Misterioso(Canvas2D c)
        {
            Cupula(c, "#b889dc", "#5b2f8a");
            Aro(c, "#2e1a4f");
            // correntes e cadeado
            c.Stroke("#dfe6f2"); c.LineWidth = 7;
            for (int i = 0; i < 4; i++)
            {
                c.BeginPath();
                c.Ellipse(60 + i * 45, 78, 16, 9, 0, 0, Mathf.PI * 2);
                c.StrokePath();
            }
            c.Stroke("#dfe6f2"); c.LineWidth = 9;
            c.BeginPath(); c.Arc(128, 118, 16, Mathf.PI, Mathf.PI * 2); c.StrokePath();
            c.Fill("#dfe6f2"); c.FillRoundRect(106, 118, 44, 36, 9);
            c.Fill("#5b2f8a"); c.FillCircle(128, 134, 5);
        }

        static Texture2D DesenharCadeado()
        {
            var c = new Canvas2D(96, 96);
            c.Stroke("#7a6a92"); c.LineWidth = 10;
            c.BeginPath(); c.Arc(48, 44, 22, Mathf.PI, Mathf.PI * 2); c.StrokePath();
            c.Fill("#7a6a92"); c.FillRoundRect(18, 44, 60, 46, 12);
            c.Fill("#ffffff"); c.FillCircle(48, 62, 6); c.FillRoundRect(45, 64, 6, 12, 3);
            return c.ToTexture("loja-cadeado", false, false, false);
        }

        static Texture2D DesenharCheck()
        {
            var c = new Canvas2D(64, 64);
            c.Fill("#3ec47a"); c.FillCircle(32, 32, 30);
            c.Stroke("#ffffff"); c.LineWidth = 8;
            c.BeginPath(); c.MoveTo(16, 34); c.LineTo(28, 46); c.LineTo(50, 20); c.StrokePath();
            return c.ToTexture("loja-check", false, false, false);
        }

        static Texture2D DesenharMoldura()
        {
            var c = new Canvas2D(160, 160);
            c.Fill("#ff5fa8"); c.FillRoundRect(0, 0, 160, 160, 44);
            return c.ToTexture("loja-moldura", false, false, false);
        }

        /// <summary>Pedestal redondo de bolo com glacê rosa, escorridos brancos e granulados.</summary>
        static Texture2D DesenharPedestal()
        {
            var c = new Canvas2D(960, 360);
            c.Fill(new Color(.48f, .18f, .39f, .25f)); c.FillEllipse(480, 320, 440, 34, 0);
            c.FillGradient = Canvas2D.Linear(0, 190, 0, 320).AddStop(0, "#ffd0e6").AddStop(1, "#ff9cc8");
            c.FillEllipse(480, 250, 440, 70, 0);
            c.Fill("#ffffff"); c.FillEllipse(480, 286, 440, 8, 0);
            c.FillGradient = Canvas2D.Linear(0, 140, 0, 250).AddStop(0, "#ffc4e2").AddStop(1, "#ff7fb6");
            c.FillEllipse(480, 190, 430, 80, 0);
            c.Fill("#fff3f9");
            for (int i = 0; i < 10; i++)
            {
                float x = 90 + i * 88, h = 28 + (i % 3) * 14;
                c.FillRoundRect(x - 14, 236, 28, h, 14);
            }
            // granulados espalhados só dentro do glacê
            var rnd = new System.Random(3);
            for (int i = 0; i < 70; i++)
            {
                float x = 60 + rnd.Next(840), y = 150 + rnd.Next(80);
                float dx = (x - 480) / 400f, dy = (y - 190) / 66f;
                if (dx * dx + dy * dy > .9f) continue;
                c.Fill(CoresGranulados[i % CoresGranulados.Length]);
                c.FillRoundRect(x - 8, y - 3, 16, 6, 3);
            }
            return c.ToTexture("loja-pedestal", false, false, false);
        }
    }
}
