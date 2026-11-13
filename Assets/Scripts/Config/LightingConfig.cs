using UnityEngine;

namespace CandyRush.Config
{
    /// <summary>
    /// Luz e atmosfera (convertido de src/config/lighting.ts, já com os ajustes
    /// de applyLightingTuning do main.ts).
    ///
    /// As intensidades NÃO são as do Three.js: lá as luzes são físicas e passam
    /// por tone mapping ACES. Aqui foram reajustadas para o pipeline padrão da
    /// Unity, buscando o mesmo resultado visual.
    /// </summary>
    public static class LightingConfig
    {
        /// <summary>Cor do horizonte: fundo, névoa e céu perto do chão.</summary>
        public static readonly Color Horizon = CircuitConfig.Hex(0xffe6f4);

        public static class Ambient
        {
            public static readonly Color Sky = CircuitConfig.Hex(0xd8edff);
            public static readonly Color Equator = CircuitConfig.Hex(0xffe6f4);
            public static readonly Color Ground = CircuitConfig.Hex(0xffd9ef);
        }

        public static class Sun
        {
            public static readonly Color Color = CircuitConfig.Hex(0xfff4dc);
            public const float Intensity = 1.15f;
            /// <summary>Direção horizontal de onde vem o sol, em graus (web: 34).</summary>
            public const float BearingDegrees = 34f;
            public const float ElevationDegrees = 52f;
        }

        public static class Rim
        {
            public static readonly Color Color = CircuitConfig.Hex(0xbfdfff);
            public const float Intensity = 0.45f;
            public const float BearingDegrees = 34f + 155f;
            public const float ElevationDegrees = 20f;
        }

        public static class Fog
        {
            public const float Near = 210f;
            public const float Far = 780f;
        }

        /// <summary>
        /// Direção de onde a luz VEM, calculada como no main.ts (lightOffset).
        /// O X é invertido pelo mesmo espelhamento aplicado ao traçado.
        /// </summary>
        public static Vector3 DirectionFrom(float bearingDegrees, float elevationDegrees)
        {
            float bearing = bearingDegrees * Mathf.Deg2Rad;
            float elevation = elevationDegrees * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(elevation);
            return new Vector3(-Mathf.Sin(bearing) * horizontal, Mathf.Sin(elevation), Mathf.Cos(bearing) * horizontal).normalized;
        }
    }
}
