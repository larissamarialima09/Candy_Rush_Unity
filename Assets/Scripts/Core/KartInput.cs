namespace CandyRush.Core
{
    /// <summary>
    /// Estado de input já normalizado (convertido de InputState, src/core/input.ts).
    /// O kart nunca vê tecla: vê eixos. Isso deixa a porta aberta para
    /// controle, toque (iOS) e piloto automático (adversários).
    /// </summary>
    public struct KartInput
    {
        /// <summary>0..1</summary>
        public float Throttle;
        /// <summary>0..1 — vira ré quando o kart está parado.</summary>
        public float Brake;
        /// <summary>
        /// -1 (esquerda) .. +1 (direita), intenção crua, sem suavização.
        /// ATENÇÃO: na versão web o sinal era o contrário (+1 = esquerda).
        /// </summary>
        public float Steer;
        public bool Handbrake;
        /// <summary>Pulso: true apenas no passo em que o botão foi apertado.</summary>
        public bool RespawnPressed;
        /// <summary>Pulso da pausa.</summary>
        public bool PausePressed;
    }
}
