using UnityEngine;

namespace CandyRush.Config
{
    /// <summary>
    /// Traçado e aparência do circuito (convertido de src/config/circuit.ts).
    ///
    /// ESPELHAMENTO: o Three.js usa um sistema de eixos destro e a Unity um
    /// canhoto. Com os mesmos números, a pista apareceria espelhada (as curvas
    /// para o lado contrário). Por isso o X de cada ponto do traçado é
    /// INVERTIDO aqui: assim o circuito na Unity fica idêntico ao da web.
    /// </summary>
    public static class CircuitConfig
    {
        /// <summary>Pontos do traçado na versão web (x, z), em metros. Curva fechada.</summary>
        private static readonly Vector2[] WebCenterline =
        {
            new Vector2(0, 0), new Vector2(0, 45), new Vector2(0, 85), new Vector2(-8, 110),
            new Vector2(-26, 128), new Vector2(-50, 135), new Vector2(-76, 134), new Vector2(-98, 126),
            new Vector2(-112, 110), new Vector2(-110, 90), new Vector2(-96, 80), new Vector2(-80, 74),
            new Vector2(-74, 56), new Vector2(-79, 34), new Vector2(-73, 14), new Vector2(-56, 4),
            new Vector2(-38, 6), new Vector2(-24, -6), new Vector2(-22, -24), new Vector2(-12, -38),
            new Vector2(4, -46), new Vector2(18, -40), new Vector2(20, -26), new Vector2(12, -13),
        };

        /// <summary>Pontos do traçado já espelhados para a Unity (x, z).</summary>
        public static Vector2[] Centerline()
        {
            var points = new Vector2[WebCenterline.Length];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(-WebCenterline[i].x, WebCenterline[i].y);
            return points;
        }

        /// <summary>Quantos segmentos ao longo de toda a volta.</summary>
        public const int Samples = 480;

        /// <summary>Janela, em amostras, para medir a curvatura sem ruído.</summary>
        public const int CurvatureWindow = 5;

        /// <summary>Alturas de desenho, em metros ACIMA da superfície (evitam briga de profundidade).</summary>
        public static class Layers
        {
            public const float Runoff = 0.03f;
            public const float Asphalt = 0.09f;
            public const float EdgeLine = 0.11f;
            public const float StartLine = 0.115f;
            public const float Kerb = 0.13f;
        }

        /// <summary>Paleta "Kawaii Candy World". Pastéis saturados, nada de cinza.</summary>
        public static class Colors
        {
            public static readonly Color Asphalt = Hex(0xf0679f);
            /// <summary>Escape (web: 0xbfdfa2, trocado em tempo de execução para 0xffd7e8).</summary>
            public static readonly Color Runoff = Hex(0xffd7e8);
            public static readonly Color CenterLine = Hex(0xffffff);
            public static readonly Color KerbA = Hex(0xf0518f);
            public static readonly Color KerbB = Hex(0xffffff);
            public static readonly Color StartLineA = Hex(0xffffff);
            public static readonly Color StartLineB = Hex(0x1a1a22);
            /// <summary>Chão verde-pistache (web: 0xb4dc93, trocado para 0xc5e9a7).</summary>
            public static readonly Color Grass = Hex(0xc5e9a7);
        }

        /// <summary>A textura de confete se repete a cada N metros de pista.</summary>
        public const float TextureRepeatMeters = 9f;

        public static class Kerb
        {
            public const float Width = 1.2f;
            /// <summary>Quantos segmentos por quadrado da zebra.</summary>
            public const int StripeSegments = 4;
        }

        public const float CenterLineWidth = 0.28f;

        public static class StartLine
        {
            /// <summary>Profundidade da faixa quadriculada, em metros.</summary>
            public const float Depth = 1.6f;
            /// <summary>Quantos quadrados ao longo da largura da pista.</summary>
            public const int Squares = 14;
        }

        /// <summary>Barreira de bala: cápsulas rosa e creme deitadas ao longo da borda.</summary>
        public static class TireWall
        {
            /// <summary>Espaçamento entre cápsulas; igual ao comprimento total da cápsula.</summary>
            public const float Spacing = 2.24f;
            public const float Radius = 0.32f;
            public const float Height = 1.6f;
            public static readonly Color ColorA = Hex(0xff7fa8);
            public static readonly Color ColorB = Hex(0xfff2f6);
        }

        /// <summary>Tamanho do plano de horizonte (m) e altura em que ele fica.</summary>
        public const float HorizonPlaneSize = 2500f;
        public const float HorizonPlaneHeight = -0.34f;

        /// <summary>Converte uma cor hexadecimal (0xRRGGBB) em Color.</summary>
        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
        }
    }
}
