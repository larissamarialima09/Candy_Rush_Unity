using CandyRush.Config;
using UnityEngine;

namespace CandyRush.Vehicle
{
    /// <summary>Configuração imutável de uma roda (convertido de src/vehicle/wheel.ts).</summary>
    public sealed class WheelSpec
    {
        public string Name;
        /// <summary>Ponto de fixação da suspensão, local ao chassi.</summary>
        public Vector3 PositionLocal;
        public float Radius;
        public bool Steered;
        public bool Powered;
        public bool IsFront;
        /// <summary>Fração da força total de freio que cabe a esta roda.</summary>
        public float BrakeShare;
    }

    /// <summary>
    /// Estado por roda, recalculado a cada passo de física.
    /// A roda não tem lógica própria: quem resolve as forças é o Kart.
    /// </summary>
    public sealed class Wheel
    {
        public readonly WheelSpec Spec;

        public bool Grounded;
        /// <summary>Compressão da mola em metros (0 = totalmente estendida).</summary>
        public float Compression;
        /// <summary>Carga vertical no contato, em N. É o que limita o grip.</summary>
        public float SuspensionForce;

        public Vector3 ContactPoint;
        public Vector3 ContactNormal = Vector3.up;
        /// <summary>Centro da roda no mundo (só visual).</summary>
        public Vector3 WorldCenter;

        /// <summary>Ângulo de esterço desta roda, em radianos. Positivo = direita.</summary>
        public float SteerAngle;
        /// <summary>Rotação acumulada da roda, em radianos (visual).</summary>
        public float SpinAngle;

        /// <summary>Velocidades no ponto de contato, em m/s.</summary>
        public float SlipLongitudinal;
        public float SlipLateral;

        /// <summary>Forças aplicadas neste passo, em N.</summary>
        public float ForceLongitudinal;
        public float ForceLateral;
        /// <summary>True quando o pneu pede mais grip do que tem.</summary>
        public bool Saturated;

        /// <summary>Material sob esta roda.</summary>
        public SurfaceKind Surface = SurfaceKind.Asphalt;

        public Wheel(WheelSpec spec) => Spec = spec;
    }
}
