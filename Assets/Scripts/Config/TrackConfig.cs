using UnityEngine;

namespace CandyRush.Config
{
    /// <summary>Um ponto de um perfil definido por fração da volta (0 = largada, 1 = volta completa).</summary>
    public readonly struct TrackProfilePoint
    {
        public readonly float At;
        public readonly float Value;

        public TrackProfilePoint(float at, float value)
        {
            At = at;
            Value = value;
        }
    }

    /// <summary>Tipos de superfície. Cada um muda como o kart se comporta em cima dele.</summary>
    public enum SurfaceKind
    {
        Asphalt, // "asfalto": a pista
        Kerb,    // "zebra": a borda quadriculada
        Grass,   // "grama": campo de marshmallow, bem pior que o escape
        Sand     // "areia": área de escape (glacê)
    }

    public readonly struct SurfaceMaterial
    {
        /// <summary>Multiplicador sobre o grip dos pneus.</summary>
        public readonly float Grip;
        /// <summary>Multiplicador sobre o teto de velocidade do motor.</summary>
        public readonly float MaxSpeed;
        /// <summary>Multiplicador sobre a resistência ao rolamento.</summary>
        public readonly float RollingResistance;
        /// <summary>Se o kart está "fora da pista" quando as rodas estão aqui.</summary>
        public readonly bool OffTrack;

        public SurfaceMaterial(float grip, float maxSpeed, float rollingResistance, bool offTrack)
        {
            Grip = grip;
            MaxSpeed = maxSpeed;
            RollingResistance = rollingResistance;
            OffTrack = offTrack;
        }
    }

    /// <summary>
    /// A pista como SISTEMA: largura, elevação, inclinação, materiais,
    /// checkpoints e barreiras (convertido de src/config/track.ts).
    /// </summary>
    public static class TrackConfig
    {
        /// <summary>
        /// Meia-largura do asfalto, em metros.
        /// Na versão web existe um perfil variável (halfWidthProfile), mas o
        /// CircuitPath usa uma largura FIXA de 5.4 m. Mantido igual.
        /// </summary>
        public const float HalfWidth = 5.4f;

        /// <summary>Elevação do eixo da pista, em metros, ao longo da volta.</summary>
        public static readonly TrackProfilePoint[] ElevationProfile =
        {
            new TrackProfilePoint(0.00f, 0.0f),
            new TrackProfilePoint(0.16f, 2.6f),
            new TrackProfilePoint(0.30f, 3.4f),
            new TrackProfilePoint(0.45f, 0.8f),
            new TrackProfilePoint(0.58f, -1.6f),
            new TrackProfilePoint(0.72f, -0.4f),
            new TrackProfilePoint(0.86f, 1.8f),
        };

        public static class Banking
        {
            /// <summary>Graus de inclinação por unidade de curvatura (1/m).</summary>
            public const float DegreesPerCurvature = 110f;
            public const float MaxDegrees = 9f;
            /// <summary>Janela de suavização, em amostras.</summary>
            public const int SmoothingSamples = 14;
        }

        /// <summary>Faixa de escape, em metros de cada lado, além do asfalto.</summary>
        public const float RunoffWidth = 5.5f;

        public static class Terrain
        {
            /// <summary>Até esta distância do eixo (m), o terreno acompanha a pista inteira.</summary>
            public const float FadeStart = 45f;
            /// <summary>A partir daqui o terreno é plano em y = 0.</summary>
            public const float FadeEnd = 115f;
            /// <summary>Tamanho da célula da malha do terreno, em metros.</summary>
            public const float CellSize = 4f;
        }

        public static class Checkpoints
        {
            /// <summary>Portais ao longo da volta, contando a largada.</summary>
            public const int Count = 14;
            /// <summary>Folga lateral, além da borda do asfalto, para o portal contar.</summary>
            public const float LateralMargin = 6f;
        }

        public static class OffTrack
        {
            /// <summary>Segundos fora da pista antes do reposicionamento.</summary>
            public const float GraceSeconds = 2.6f;
            /// <summary>Altura acima da pista em que o kart reaparece.</summary>
            public const float RespawnHeight = 0.8f;
        }

        public static class Barriers
        {
            /// <summary>Distância entre a borda do ASFALTO e a barreira, em metros.</summary>
            public const float OffsetFromEdge = 5.0f;
            /// <summary>Raio de colisão do kart, em metros.</summary>
            public const float KartRadius = 0.75f;
            /// <summary>Quanto da velocidade contra o muro volta como repique.</summary>
            public const float Restitution = 0.2f;
            /// <summary>Fração da velocidade AO LONGO do muro que sobrevive a cada SEGUNDO de raspão.</summary>
            public const float SlidePerSecond = 0.55f;
            /// <summary>Quanto o impacto alinha o kart com a barreira (por segundo).</summary>
            public const float Alignment = 3.5f;
            /// <summary>Velocidade mínima contra o muro para contar como raspão.</summary>
            public const float ScrapeSpeed = 2.5f;
        }

        /// <summary>
        /// ESTES NÚMEROS DEFINEM O CUSTO DE ERRAR. Grama punitiva demais frustra;
        /// leve demais e cortar caminho fica mais rápido que fazer a curva.
        /// </summary>
        public static SurfaceMaterial Surface(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Kerb: return new SurfaceMaterial(0.9f, 1.0f, 1.4f, false);
                case SurfaceKind.Grass: return new SurfaceMaterial(0.5f, 0.55f, 3.5f, true);
                case SurfaceKind.Sand: return new SurfaceMaterial(0.7f, 0.72f, 2.2f, true);
                default: return new SurfaceMaterial(1.0f, 1.0f, 1.0f, false);
            }
        }
    }
}
