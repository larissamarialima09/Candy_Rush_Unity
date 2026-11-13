using CandyRush.Core;
using CandyRush.Track;
using CandyRush.Vehicle;
using UnityEngine;

namespace CandyRush.Game
{
    /// <summary>
    /// Piloto automático simples: mira um ponto do eixo da pista à frente e
    /// dosa acelerador e freio pelo ângulo até ele.
    ///
    /// Serve para os testes automáticos e para o modo vitrine. Os adversários
    /// de verdade (src/rivals/racerController.ts) entram na Etapa 2.
    /// </summary>
    public sealed class Autopilot
    {
        private readonly CircuitPath _path;

        /// <summary>Distância de mira, em metros, parado.</summary>
        public float LookAheadBase = 7f;
        /// <summary>Metros extras de mira por m/s.</summary>
        public float LookAheadPerSpeed = 0.55f;
        /// <summary>Ganho da direção: quanto de volante por grau de erro.</summary>
        public float SteerGain = 0.06f;
        /// <summary>Acima deste erro (graus) em alta velocidade, alivia e freia.</summary>
        public float BrakeAngle = 28f;

        public Autopilot(CircuitPath path) => _path = path;

        public KartInput Drive(Kart kart)
        {
            Vector3 position = kart.Body.ChassisPosition;
            int index = _path.NearestSampleIndex(position.x, position.z);

            float lookAhead = LookAheadBase + Mathf.Max(0f, kart.Telemetry.ForwardSpeed) * LookAheadPerSpeed;
            float spacing = _path.TotalLength / _path.Count;
            int targetIndex = (index + Mathf.RoundToInt(lookAhead / spacing)) % _path.Count;
            Vector3 target = _path.Samples[targetIndex].Position;

            Vector3 forward = MathUtils.Flat(kart.Body.DirectionToWorld(Vector3.forward)).normalized;
            Vector3 toTarget = MathUtils.Flat(target - position).normalized;
            // Positivo = alvo à direita (sentido horário visto de cima, na Unity).
            float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);

            var input = new KartInput
            {
                Steer = Mathf.Clamp(angle * SteerGain, -1f, 1f),
                Throttle = 1f,
            };

            if (Mathf.Abs(angle) > BrakeAngle && kart.Telemetry.Speed > 14f)
            {
                input.Throttle = 0f;
                input.Brake = 0.6f;
            }
            return input;
        }
    }
}
