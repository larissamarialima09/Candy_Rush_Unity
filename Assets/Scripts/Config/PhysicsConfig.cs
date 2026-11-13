namespace CandyRush.Config
{
    /// <summary>
    /// Constantes globais de simulação (convertido de src/config/physics.ts).
    /// A física roda em passo fixo: nada aqui é multiplicado por um deltaTime variável.
    /// </summary>
    public static class PhysicsConfig
    {
        /// <summary>Passo fixo da simulação, em segundos (60 Hz).</summary>
        public const float FixedTimeStep = 1f / 60f;

        /// <summary>Máximo de passos por quadro: melhor câmera lenta do que travar.</summary>
        public const int MaxStepsPerFrame = 5;

        /// <summary>Tempo máximo de quadro aceito pelo acumulador (segundos).</summary>
        public const float MaxFrameTime = 0.25f;

        /// <summary>Gravidade exagerada de propósito: kart mais colado e suspensão mais viva.</summary>
        public const float Gravity = -18.0f;
    }
}
