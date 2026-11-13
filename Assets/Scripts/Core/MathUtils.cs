using UnityEngine;

namespace CandyRush.Core
{
    /// <summary>Funções matemáticas pequenas (convertido de src/core/mathUtils.ts).</summary>
    public static class MathUtils
    {
        /// <summary>Move current na direção de target no máximo maxDelta.</summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            float diff = target - current;
            if (Mathf.Abs(diff) <= maxDelta) return target;
            return current + Mathf.Sign(diff) * maxDelta;
        }

        /// <summary>Sinal que devolve 0 para 0 (Mathf.Sign devolve 1).</summary>
        public static float SignOrZero(float value)
        {
            if (value > 0f) return 1f;
            if (value < 0f) return -1f;
            return 0f;
        }

        /// <summary>
        /// Gerador pseudoaleatório com semente (mulberry32), idêntico ao
        /// createRandom da web: com a mesma semente e a mesma ordem de chamadas,
        /// o cenário sai exatamente igual.
        /// </summary>
        public static System.Func<float> CreateRandom(uint seed)
        {
            uint state = seed;
            return () =>
            {
                unchecked
                {
                    state += 0x6D2B79F5u;
                    uint t = (state ^ (state >> 15)) * (1u | state);
                    t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                    return (float)((t ^ (t >> 14)) / 4294967296.0);
                }
            };
        }

        /// <summary>Vetor com o Y zerado (projeção no plano horizontal).</summary>
        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
