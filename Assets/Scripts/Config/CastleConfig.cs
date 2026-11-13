using UnityEngine;
using static CandyRush.Config.CircuitConfig;

namespace CandyRush.Config
{
    /// <summary>
    /// O "Doce Castelo" sobre a pista (convertido de src/config/castle.ts).
    ///
    /// Coordenadas LOCAIS do castelo, como na web: origem na boca do túnel,
    /// +Z no sentido de corrida e +X para a ESQUERDA da pista. O CandyCastle
    /// cuida de levar isso para o mundo da Unity sem espelhar o castelo.
    /// </summary>
    public static class CastleConfig
    {
        public enum TowerBody { Peach, Pink, Blue, Orange, Cane }
        public enum TowerRoof { Cane, Blue, Dome, Gumdrop }

        public readonly struct Tower
        {
            public readonly float X, Z, Radius, Height;
            public readonly TowerBody Body;
            public readonly TowerRoof Roof;

            public Tower(float x, float z, float radius, float height, TowerBody body, TowerRoof roof)
            {
                X = x; Z = z; Radius = radius; Height = height; Body = body; Roof = roof;
            }
        }

        /// <summary>Fração da volta onde fica a boca do túnel.</summary>
        public const float At = 0.112f;
        /// <summary>Quanto os volumes descem abaixo do chão (para não sobrar fresta em terreno inclinado).</summary>
        public const float BuriedDepth = 2.5f;

        public static class Tunnel
        {
            public const float Length = 30f;
            /// <summary>Folga entre a barreira de bala e a parede do túnel.</summary>
            public const float WallMargin = 2.5f;
            public const float WallHeight = 4.2f;
            public const float ArchRise = 7.8f;
            public const float ShellThickness = 2.4f;
            public const float HoopLength = 1.5f;
            public const float PortalDepth = 2.2f;
            public const int MarshmallowCount = 15;
            public const float BellSpacing = 4.5f;
        }

        public const float TerraceHeight = 15.5f;

        public static class Animation
        {
            public const float BellGlowMin = 0.25f;
            public const float BellGlowMax = 0.75f;
            public const float BellGlowSpeed = 3.2f;
            public const float SignPulse = 0.04f;
            public const float SignPulseSpeed = 2.4f;
        }

        public static class Hearts
        {
            public const int Count = 8;
            public const float FromX = -15f;
            public const float ToX = 40f;
            public const float BaseHeight = 19.5f;
            public const float HeightStep = 2.8f;
            public const float Z = -5f;
            public const float ZStagger = 1.5f;
            public const float Size = 1.15f;
            public const float Thickness = 0.4f;
        }

        /// <summary>Área ocupada pelo castelo (local): o cenário sorteado não nasce aqui dentro.</summary>
        public static readonly Rect[] Footprint =
        {
            Rect.MinMaxRect(-23f, -4f, 48f, 36f),   // x = min/max X local, y = min/max Z local
            Rect.MinMaxRect(12f, -25f, 48f, -4f),
        };

        public static readonly Tower[] Towers =
        {
            new Tower(42f, 6f, 3.2f, 23f, TowerBody.Peach, TowerRoof.Cane),
            new Tower(40f, 22f, 2.4f, 27f, TowerBody.Orange, TowerRoof.Blue),
            new Tower(34f, 16f, 2.6f, 33f, TowerBody.Pink, TowerRoof.Blue),
            new Tower(30f, 25f, 2.2f, 36f, TowerBody.Pink, TowerRoof.Blue),
            new Tower(29f, 3.5f, 2.0f, 21f, TowerBody.Cane, TowerRoof.Gumdrop),
            new Tower(22f, 7.5f, 2.8f, 29f, TowerBody.Blue, TowerRoof.Dome),
            new Tower(15f, 20f, 2.3f, 39f, TowerBody.Pink, TowerRoof.Cane),
            new Tower(4f, 21f, 2.3f, 42f, TowerBody.Orange, TowerRoof.Cane),
            new Tower(-1.5f, 8.5f, 2.8f, 29f, TowerBody.Blue, TowerRoof.Dome),
            new Tower(-6f, 26f, 2.2f, 31f, TowerBody.Blue, TowerRoof.Dome),
            new Tower(-9f, 16f, 2.6f, 33f, TowerBody.Orange, TowerRoof.Cane),
            new Tower(-18.5f, 4f, 3.0f, 22f, TowerBody.Peach, TowerRoof.Cane),
        };

        public static class Colors
        {
            public static readonly Color Gingerbread = Hex(0xe29a5c);
            public static readonly Color Icing = Hex(0xfffaf3);
            public static readonly Color Pink = Hex(0xff9dc4);
            public static readonly Color HotPink = Hex(0xff5c95);
            public static readonly Color Blue = Hex(0x86d6f5);
            public static readonly Color Orange = Hex(0xffb163);
            public static readonly Color Peach = Hex(0xffcf9a);
            public static readonly Color Mint = Hex(0x8fe3b4);
            public static readonly Color Yellow = Hex(0xffd94f);
            public static readonly Color Lavender = Hex(0xc6a4f4);
            public static readonly Color Window = Hex(0x7a3a55);
            public static readonly Color Gold = Hex(0xffc83d);
            public static readonly Color HoopPink = Hex(0xff8fbb);
            public static readonly Color HoopWhite = Hex(0xfff3f8);
            public static readonly Color CaneStripe = Hex(0xe8394f);
            public static readonly Color SignBackground = Hex(0xff5c95);
        }
    }
}
