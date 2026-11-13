namespace CandyRush.Config
{
    /// <summary>
    /// Controles (convertido de src/config/controls.ts e do trecho de gamepad de src/config/coop.ts).
    ///
    /// Teclado, um jogador: WASD ou setas, Espaço = freio de mão (drift),
    /// R = voltar para a pista, Esc ou P = pausa.
    ///
    /// Controle (layout Xbox/PlayStation):
    ///   acelerar = gatilho direito ou A/✕
    ///   frear/ré = gatilho esquerdo ou B/◯
    ///   freio de mão = RB/R1 ou X/▢
    ///   voltar para a pista = Y/△
    ///   pausa = Start/Options
    ///
    /// Toque (iOS) entra na etapa de iOS.
    /// </summary>
    public static class ControlsConfig
    {
        public static class Gamepad
        {
            /// <summary>Zona morta do analógico de direção.</summary>
            public const float Deadzone = 0.18f;
            /// <summary>Curva do analógico: acima de 1 = mais precisão no centro.</summary>
            public const float SteerCurve = 1.5f;
            /// <summary>A partir de que fração o gatilho conta como pisado.</summary>
            public const float TriggerThreshold = 0.12f;
        }
    }
}
