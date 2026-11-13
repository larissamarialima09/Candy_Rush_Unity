using UnityEngine;

namespace CandyRush.Config
{
    /// <summary>
    /// TODO o tuning do kart vive aqui (convertido de src/config/kart.ts).
    /// Nenhum destes números pode aparecer hardcoded dentro da lógica de física.
    ///
    /// Os valores JÁ INCLUEM os ajustes que o main.ts da versão web aplicava em
    /// tempo de execução (applyDrivingTuning). Onde o valor difere do kart.ts
    /// original, o comentário "web:" mostra o número de lá.
    ///
    /// Eixos no espaço local do chassi: +X = direita, +Y = cima, +Z = FRENTE.
    /// A origem do chassi é o centro geométrico da caixa.
    /// </summary>
    public static class KartConfig
    {
        public static class Chassis
        {
            /// <summary>Massa total (kart + piloto), em kg.</summary>
            public const float Mass = 165f;

            /// <summary>Extensões totais da caixa do chassi (largura, altura, comprimento), em metros.</summary>
            public static readonly Vector3 Size = new Vector3(1.05f, 0.5f, 1.85f);

            /// <summary>
            /// Centro de massa em coordenadas locais. Baixo = menos rolagem;
            /// levemente atrás = mais tração na saída de curva. (web: 0, -0.2, -0.09)
            /// </summary>
            public static readonly Vector3 CenterOfMass = new Vector3(0f, -0.24f, -0.12f);

            /// <summary>Multiplicadores sobre a inércia de caixa uniforme: x = pitch, y = yaw, z = roll.</summary>
            public static readonly Vector3 InertiaScale = new Vector3(1.0f, 0.75f, 1.6f);

            public const float LinearDamping = 0.02f;

            /// <summary>Segura o kart de ficar girando no ar. (web: 0.55)</summary>
            public const float AngularDamping = 0.75f;
        }

        public static class Wheels
        {
            public const float HalfTrackFront = 0.58f;
            public const float HalfTrackRear = 0.62f;
            public const float FrontAxleZ = 0.66f;
            public const float RearAxleZ = -0.66f;
            /// <summary>Altura do ponto de fixação da suspensão.</summary>
            public const float AttachY = -0.13f;
            public const float RadiusFront = 0.24f;
            public const float RadiusRear = 0.27f;
            /// <summary>Largura visual da roda (só render).</summary>
            public const float Width = 0.2f;
        }

        public static class Suspension
        {
            /// <summary>Comprimento da mola em repouso, do ponto de fixação até o centro da roda.</summary>
            public const float RestLength = 0.26f;
            /// <summary>Curso máximo de compressão.</summary>
            public const float MaxTravel = 0.2f;
            /// <summary>Rigidez da mola, em N/m, POR RODA.</summary>
            public const float Stiffness = 14000f;
            /// <summary>
            /// Amortecimento na compressão. Limite de estabilidade: o integrador é
            /// explícito a 1/60 s; forte demais e a roda oscila no passo de física.
            /// </summary>
            public const float DamperBump = 500f;
            public const float DamperRebound = 800f;
            /// <summary>Teto de força da suspensão por roda (N).</summary>
            public const float MaxForce = 14000f;
        }

        public static class Engine
        {
            /// <summary>Empuxo máximo somado das rodas de tração, em N. (web: 2050)</summary>
            public const float MaxDriveForce = 1880f;
            /// <summary>Velocidade (m/s) em que a curva de torque chega a zero. (web: 40)</summary>
            public const float PowerCutoffSpeed = 35f;
            /// <summary>Expoente da queda: força = máx * (1 - (v/corte)^expo).</summary>
            public const float PowerFalloffExponent = 2.0f;
            public const float ReverseForce = 1300f;
            public const float ReverseTopSpeed = 9f;
            /// <summary>Velocidade abaixo da qual segurar o freio engata a ré.</summary>
            public const float ReverseEngageSpeed = 0.6f;
        }

        public static class Brakes
        {
            /// <summary>Força total de frenagem, em N, somando as quatro rodas.</summary>
            public const float Force = 4200f;
            /// <summary>Fração da frenagem no eixo dianteiro.</summary>
            public const float FrontBias = 0.62f;
            /// <summary>Resistência ao rolamento por (m/s), por roda. (web: 4)</summary>
            public const float RollingResistance = 6f;
            /// <summary>Freio-motor sem acelerador nem freio, por (m/s), por roda. (web: 4)</summary>
            public const float EngineBraking = 7f;
        }

        public static class Handbrake
        {
            /// <summary>Multiplicador no grip lateral TRASEIRO com o freio de mão. (web: 0.5)</summary>
            public const float RearGripMultiplier = 0.62f;
            /// <summary>Força extra de frenagem só na traseira. Baixa: serve para soltar a traseira. (web: 600)</summary>
            public const float RearBrakeForce = 450f;
            /// <summary>Se true, o freio de mão corta o acelerador.</summary>
            public const bool CutsThrottle = false;
        }

        /// <summary>
        /// Pneus. O grip é proporcional à carga vertical da roda, então a
        /// transferência de peso funciona de graça.
        /// A diferença entre FrontGrip e RearGrip é o caráter do kart.
        /// </summary>
        public static class Tires
        {
            public const float FrontGrip = 1.75f;        // web: 1.5
            public const float RearGrip = 1.85f;         // web: 1.4
            public const float LongitudinalGrip = 2.25f; // web: 1.9
            /// <summary>Fração da massa usada para zerar a velocidade lateral em um passo. (web: 0.22)</summary>
            public const float TireMassShare = 0.27f;
            /// <summary>Altura acima do contato onde a força do pneu é aplicada (evita capotar).</summary>
            public const float ForceApplicationHeight = 0.16f;
            /// <summary>Força lateral e longitudinal dividem o mesmo limite de atrito.</summary>
            public const bool UseFrictionCircle = true;
            public const float MaxLateralForce = 9000f;
        }

        public static class Steering
        {
            public const float MaxAngleDegrees = 30f;   // web: 33
            /// <summary>Fração do ângulo disponível em FalloffSpeed. (web: 0.35)</summary>
            public const float HighSpeedFactor = 0.46f;
            public const float FalloffSpeed = 30f;      // web: 26
            /// <summary>Velocidade de giro do volante (unidades de input por segundo). (web: 5)</summary>
            public const float TurnRate = 7f;
            /// <summary>Velocidade de retorno do volante ao centro. (web: 8)</summary>
            public const float ReturnRate = 10f;
        }

        public static class Aero
        {
            /// <summary>Arrasto: força = coeficiente * v². (web: 1.15)</summary>
            public const float DragCoefficient = 1.45f;
            /// <summary>Downforce: força = coeficiente * v², para baixo. (web: 1.6)</summary>
            public const float DownforceCoefficient = 2.35f;
        }

        /// <summary>Rede de segurança para kart capotado ou caído.</summary>
        public static class Recovery
        {
            public const float FallThroughHeight = -3f;
            public const float InvertedTimeout = 1.5f;
            public const float StuckSpeed = 2.0f;
        }

        public static class Spawn
        {
            public static readonly Vector3 Position = new Vector3(0f, 0.9f, 0f);
            public const float HeadingDegrees = 0f;
        }
    }
}
