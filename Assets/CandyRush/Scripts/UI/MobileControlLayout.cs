using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Layout em pixels da área segura, compartilhado pelo desenho e pelo toque.</summary>
    public struct MobileControlLayout
    {
        public Rect Area, Joystick, Brake, Throttle, Drift, Turbo, Meter, Pause;
        public float Scale;

        public static MobileControlLayout Create(Rect area)
        {
            float s = Mathf.Min(area.height / 540f, area.width / 960f);
            float pad = 24 * s;
            var layout = new MobileControlLayout { Area = area, Scale = s };
            layout.Joystick = new Rect(area.x + pad, area.yMax - 218 * s, 184 * s, 184 * s);
            layout.Brake = new Rect(area.x + 226 * s, area.yMax - 146 * s, 88 * s, 88 * s);
            // O pedal principal fica junto à borda; as ações menores ficam ao alcance do polegar.
            layout.Throttle = new Rect(area.xMax - pad - 104 * s, area.yMax - 160 * s, 104 * s, 104 * s);
            layout.Drift = new Rect(layout.Throttle.x - 98 * s, area.yMax - 138 * s, 82 * s, 82 * s);
            layout.Turbo = new Rect(layout.Throttle.center.x - 35 * s, layout.Throttle.y - 98 * s, 70 * s, 70 * s);
            layout.Meter = new Rect(area.center.x - 100 * s, area.yMax - 46 * s, 200 * s, 24 * s);
            layout.Pause = new Rect(area.x + 16 * s, area.y + 16 * s, 58 * s, 58 * s);
            return layout;
        }

        /// <summary>Zona morta circular do joystick, em fração do curso máximo.</summary>
        public const float StickDeadzone = .12f;

        /// <summary>
        /// Joystick analógico: a direção é a do dedo, em qualquer ângulo (diagonais incluídas),
        /// e a força cresce com a distância do centro. Só o círculo da zona morta é descartado.
        /// Entrada e saída estão em fração do curso máximo; a saída fica dentro de magnitude 1.
        /// </summary>
        public static Vector2 ShapeStick(Vector2 raw)
        {
            float magnitude = raw.magnitude;
            if (magnitude <= StickDeadzone) return Vector2.zero;
            float strength = Mathf.Clamp01((magnitude - StickDeadzone) / (1 - StickDeadzone));
            return raw / magnitude * strength;
        }
    }
}
