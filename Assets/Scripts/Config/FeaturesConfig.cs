using UnityEngine;
using static CandyRush.Config.CircuitConfig;

namespace CandyRush.Config
{
    /// <summary>
    /// Elementos da pista (convertido de src/config/trackFeatures.ts,
    /// donutTunnel.ts, startGate.ts e pickups.ts).
    /// "At" é sempre a fração da volta (0 = largada).
    /// </summary>
    public static class FeaturesConfig
    {
        /// <summary>Pastéis saturados de bala (CIRCUIT.colors.pastels).</summary>
        public static readonly Color[] Pastels =
        {
            Hex(0xff7fb5), Hex(0x63d0e8), Hex(0xc79bf5), Hex(0xffd24d), Hex(0x8fdb6a), Hex(0xffa14d),
        };

        /// <summary>Rampa de turbo: fita de bala com setas de neon, logo depois do Túnel de Donut.</summary>
        public static class SpeedRamp
        {
            public const float At = 0.5f;
            /// <summary>Comprimento ao longo da pista, em metros.</summary>
            public const float Length = 9f;
            public const float HalfWidth = 1.7f;
            public const float Lateral = 0f;
            public const float Height = 0.14f;
            public const float ArrowHeight = 0.17f;
            /// <summary>Nível do turbo entregue (o mesmo sistema do drift).</summary>
            public const int BoostLevel = 2;
            public const int Chevrons = 3;
            public static readonly Color PadLight = Hex(0xfff2f8);
            public static readonly Color PadStripe = Hex(0xff8cc0);
            public static readonly Color Arrow = Hex(0xff2e93);
            public static readonly Color Post = Hex(0xffffff);
            public static readonly Color PostTop = Hex(0xff5c96);
        }

        /// <summary>Geleias com rostinho no trecho final: seguram o kart e balançam quando batem.</summary>
        public static class JellyBlobs
        {
            public readonly struct Spot
            {
                public readonly float At, Lateral, Radius;
                public readonly int Color;
                public Spot(float at, float lateral, float radius, int color)
                {
                    At = at; Lateral = lateral; Radius = radius; Color = color;
                }
            }

            public static readonly Spot[] Spots =
            {
                new Spot(0.905f, -3.2f, 0.85f, 0), new Spot(0.905f, 0.6f, 0.75f, 1),
                new Spot(0.93f, -0.9f, 0.8f, 2), new Spot(0.93f, 3.3f, 0.9f, 3),
                new Spot(0.955f, -3.6f, 0.75f, 1), new Spot(0.955f, 1.8f, 0.85f, 0),
                new Spot(0.975f, -1.6f, 0.8f, 3),
            };

            /// <summary>Fração da velocidade que sobrevive a cada SEGUNDO dentro da geleia.</summary>
            public const float SlowPerSecond = 0.05f;
            /// <summary>Empurrão para fora, em m/s por passo.</summary>
            public const float Push = 1.6f;

            public const float WobbleAmplitude = 0.07f;
            public const float WobbleSpeed = 2.3f;
            public const float HitAmplitude = 0.34f;
            public const float HitDecay = 0.9f;

            public static readonly Color[] Colors = { Hex(0xff4f9a), Hex(0x4fc3f5), Hex(0x7fd36a), Hex(0xffc93d) };

            public const float EyeRadius = 0.17f;
            public const float EyeSpread = 0.36f;
            public const float MouthRadius = 0.16f;
            public static readonly Color Cheek = Hex(0xff9ec4);
            public static readonly Color Ink = Hex(0x5c2340);
        }

        /// <summary>O Túnel de Donut: um donut gigante em pé sobre a pista.</summary>
        public static class DonutTunnel
        {
            public const float At = 0.43f;
            public const float HoleRadius = 11.5f;
            public const float CenterHeight = 5.0f;
            public const float TubeRadius = 3.8f;
            public const int RingSegments = 44;
            public const int TubeSegments = 18;

            public const float FrostingThickness = 0.2f;
            public const float FrostingSpan = 1.85f;
            public const float FrostingWaveAmplitude = 0.22f;
            public const int FrostingWaves = 9;

            public const int SprinkleCount = 96;
            public const float SprinkleLength = 0.66f;
            public const float SprinkleThickness = 0.18f;

            public static readonly Color Dough = Hex(0xf0b46a);
            public static readonly Color DoughTop = Hex(0xffd89c);
            public static readonly Color DoughBottom = Hex(0xd98c3f);
            public static readonly Color Icing = Hex(0xff6fae);
            public static readonly Color IcingTop = Hex(0xffb2d6);
            public static readonly Color IcingBottom = Hex(0xf03e90);
        }

        /// <summary>O portal "CANDY RUSH" sobre a linha de largada.</summary>
        public static class StartGate
        {
            public const float OffsetFromEdge = 7.2f;

            public const float PillarRadius = 1.15f;
            public const float PillarHeight = 7.4f;
            public const float FootRadius = 1.75f;
            public const float FootHeight = 1.1f;
            public const float PlinthSize = 4.0f;
            public const float PlinthHeight = 0.55f;

            public const float ArchThickness = 1.0f;
            /// <summary>Altura do arco acima dos pilares (o arco é achatado).</summary>
            public const float ArchRise = 4.5f;

            public const string SignText = "CANDY RUSH";
            public const float SignWidth = 11.5f;
            public const float SignAboveArch = -0.3f;

            public const int LightCount = 4;
            public const float LightRadius = 0.34f;
            public const float LightSpacing = 1.3f;
            public const float LightBarHeight = 1.15f;
            public const float LightBarDepth = 0.5f;
            public const float LightBelowSign = 0.75f;

            public const float LollipopRadius = 1.9f;
            public const float LollipopThickness = 0.36f;
            public const float LollipopAt = 0.78f;

            public const float CloudRadius = 1.15f;
            public const float CloudAt = 0.5f;

            public const float StarOuterRadius = 1.15f;
            public const float StarInnerRadius = 0.48f;
            public const float StarAt = 0.36f;

            public const float FlagPoleHeight = 3.4f;
            public const float FlagPoleRadius = 0.12f;
            public const float FlagWidth = 2.3f;
            public const float FlagHeight = 1.45f;
            public const int FlagSquares = 4;

            public const float CrownBandRadius = 0.85f;
            public const float CrownBandHeight = 0.34f;
            public const float CrownSpikeHeight = 0.8f;
            public const float CrownSpikeRadius = 0.26f;

            public static readonly Color Pink = Hex(0xff7fb5);
            public static readonly Color HotPink = Hex(0xf5459a);
            public static readonly Color Frosting = Hex(0xfff6fb);
            public static readonly Color Gold = Hex(0xffc83d);
            public static readonly Color LightBar = Hex(0x3b2436);
            public static readonly Color LightOn = Hex(0xff5c96);
            public static readonly Color Cloud = Hex(0xfffdff);
            public static readonly Color Face = Hex(0x7a3a55);
            public static readonly Color CheekColor = Hex(0xffb3d0);
            public static readonly Color Star = Hex(0xffd94f);
        }

        /// <summary>Moedas de bala: fileira ondulando pela pista, voltam depois de um tempo.</summary>
        public static class Coins
        {
            public const float Spacing = 14f;
            public const float Height = 0.85f;
            public const float LateralFraction = 0.55f;
            public const float WeaveLength = 7f;
            public const float Radius = 0.6f;
            public const float Thickness = 0.16f;
            public const float PickupRadius = 1.7f;
            public const float RespawnSeconds = 9f;
            public const float SpinSpeed = 0.75f;
            public const float BobAmplitude = 0.14f;
            public const float BobSpeed = 1.6f;
            public static readonly Color Color = Hex(0xffd76a);
            public static readonly Color Emissive = Hex(0xff9ec4);
            public const float EmissiveIntensity = 1.4f;
        }
    }
}
