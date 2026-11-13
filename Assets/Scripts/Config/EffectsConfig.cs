namespace CandyRush.Config
{
    /// <summary>
    /// Sensação de velocidade (convertido de src/config/effects.ts).
    /// Ordem de importância: FOV dinâmico primeiro, partículas depois, tremor por último.
    /// </summary>
    public static class EffectsConfig
    {
        public static class Fov
        {
            /// <summary>Campo de visão vertical com o kart parado, em graus.</summary>
            public const float AtRest = 66f;
            public const float AtTopSpeed = 82f;
            /// <summary>Quanto o boost soma por cima, em graus.</summary>
            public const float BoostBonus = 7f;
            public const float ReferenceSpeed = 26f;
            public const float Responsiveness = 4.5f;
        }

        public static class Shake
        {
            public const float SpeedAmplitude = 0.018f;
            public const float BoostAmplitude = 0.028f;
            public const float OffTrackMultiplier = 2.6f;
            public const float ReferenceSpeed = 26f;
            public const float FrequencyA = 23.3f;
            public const float FrequencyB = 17.1f;
            public const float RollDegrees = 0.4f;
            public const float MinSpeed = 6f;
        }
    }
}
