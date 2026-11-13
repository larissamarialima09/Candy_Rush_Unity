namespace CandyRush.Config
{
    /// <summary>
    /// Drift e boost, o coração do jogo (convertido de src/config/drift.ts).
    ///
    /// A DERRAPAGEM é física de verdade: o freio de mão reduz o grip traseiro.
    /// O JOGO só observa: se o ângulo de deslizamento passa de um limiar com
    /// velocidade suficiente, um medidor carrega; ao sair, vira turbo.
    /// </summary>
    public static class DriftConfig
    {
        public static class Detection
        {
            /// <summary>Abaixo desta velocidade (m/s) não existe drift.</summary>
            public const float MinSpeed = 8f;
            /// <summary>Ângulo de deslizamento (graus) que INICIA o drift.</summary>
            public const float EnterAngleDeg = 12f;
            /// <summary>Ângulo que ENCERRA. Menor que o de entrada (histerese).</summary>
            public const float ExitAngleDeg = 7f;
            /// <summary>Tolerância para cruzar o limiar de saída sem perder o drift.</summary>
            public const float ExitGrace = 0.2f;
            /// <summary>Acima deste ângulo o kart rodou: a carga é perdida.</summary>
            public const float SpinOutAngleDeg = 100f;
        }

        public static class Charge
        {
            public const float BaseRatePerSecond = 1.2f;
            public const float IdealAngleDeg = 40f;
            public const float DepthBonus = 0.9f;
            public const float SpeedReference = 18f;
            /// <summary>Carga acumulada necessária para os níveis 1, 2 e 3.</summary>
            public static readonly float[] Levels = { 0.35f, 0.85f, 1.5f };
        }

        public static class Boost
        {
            /// <summary>Empuxo extra, em N, por nível (índice 0 = nível 1).</summary>
            public static readonly float[] Force = { 700f, 1125f, 1550f };
            /// <summary>Duração, em segundos, por nível.</summary>
            public static readonly float[] Duration = { 0.8f, 1.3f, 1.85f };
            /// <summary>Quanto sobe o teto de potência do motor (m/s) durante o boost.</summary>
            public static readonly float[] SpeedCeilingBonus = { 3.5f, 5.5f, 8f };
            /// <summary>força = pico * (restante/duração)^expoente.</summary>
            public const float DecayExponent = 0.55f;
        }

        public static class Steering
        {
            /// <summary>Multiplicador no ângulo máximo de esterço durante o drift.</summary>
            public const float DriftSteerMultiplier = 1.4f;
            /// <summary>Quanto da queda de esterço por velocidade é aliviada no drift (0..1).</summary>
            public const float DriftFalloffRelief = 0.5f;
        }
    }
}
