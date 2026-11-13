namespace CandyRush.Config
{
    /// <summary>
    /// Câmera perseguidora com peso (convertido de src/config/camera.ts).
    /// A câmera NÃO é filha do kart: é um corpo próprio puxado por molas.
    /// Desse atraso vem a sensação de peso.
    /// </summary>
    public static class CameraConfig
    {
        public const float Near = 0.1f;
        public const float Far = 2000f;

        public static class Offset
        {
            /// <summary>Distância atrás do kart.</summary>
            public const float Distance = 5.6f;
            /// <summary>Altura acima da origem do chassi.</summary>
            public const float Height = 2.25f;
        }

        /// <summary>Mola da POSIÇÃO. Estabilidade: damping*dt &lt; 2 e stiffness*dt² &lt; 4.</summary>
        public static class Follow
        {
            public const float Stiffness = 180f;
            public const float Damping = 27f;
        }

        /// <summary>Mola da MIRA, mais mole: o kart sai do centro da tela ao girar rápido.</summary>
        public static class Aim
        {
            public const float Stiffness = 125f;
            public const float Damping = 23f;
        }

        /// <summary>Para onde a câmera olha, à FRENTE do kart.</summary>
        public static class Look
        {
            public const float AheadBase = 3.8f;
            public const float AheadPerSpeed = 0.13f;
            public const float Height = 0.82f;
        }

        /// <summary>A câmera recua ao acelerar e chega mais perto ao frear.</summary>
        public static class Acceleration
        {
            public const float MetersPerAccel = 0.035f;
            public const float MaxPullBack = 0.55f;
            public const float MaxPullIn = 0.55f;
            public const float Smoothing = 8.0f;
        }

        /// <summary>Inclinação lateral nas curvas.</summary>
        public static class Roll
        {
            public const float DegreesPerLateralAccel = 0.16f;
            public const float MaxDegrees = 4.5f;
            public const float Responsiveness = 7.5f;
            public const float MinSpeed = 3.0f;
        }

        /// <summary>A câmera nunca desce abaixo desta altura acima do chão.</summary>
        public const float MinHeightAboveGround = 0.65f;

        /// <summary>Respawn teleporta a câmera em vez de varrer o mapa atrás do kart.</summary>
        public const bool SnapOnRespawn = true;
    }
}
