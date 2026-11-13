using CandyRush.Config;
using CandyRush.Physics;
using UnityEngine;

namespace CandyRush.Track
{
    /// <summary>
    /// A pista como superfície física (convertido de src/track/trackSurface.ts).
    ///
    /// O raycast NÃO é feito contra malha nem contra colliders: a altura e a
    /// normal em qualquer ponto saem direto da spline (amostra mais próxima +
    /// deslocamento lateral + inclinação). É mais rápido e nunca falha por o
    /// raio passar exatamente na aresta entre dois triângulos.
    ///
    /// O raio é intersectado com o PLANO TANGENTE da superfície sob a origem.
    /// </summary>
    public sealed class TrackSurface : IGroundSampler
    {
        private readonly CircuitPath _path;

        public TrackSurface(CircuitPath path) => _path = path;

        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out GroundHit hit)
        {
            hit = default;
            if (direction.y >= -1e-4f) return false;

            int index = _path.NearestSampleIndex(origin.x, origin.z);
            float lateral = _path.LateralOffset(index, origin.x, origin.z);

            var planePoint = new Vector3(origin.x, _path.SurfaceHeight(index, lateral), origin.z);
            // A normal inclinada só vale sobre o asfalto; fora dele o terreno é plano.
            bool paved = Mathf.Abs(lateral) <= _path.Samples[index].HalfWidth;
            Vector3 planeNormal = paved ? _path.Samples[index].Normal : Vector3.up;

            float denominator = Vector3.Dot(direction, planeNormal);
            if (denominator >= -1e-6f) return false;

            float distance = Vector3.Dot(planePoint - origin, planeNormal) / denominator;
            if (distance < 0f || distance > maxDistance) return false;

            hit.Distance = distance;
            hit.Point = origin + direction * distance;
            hit.Normal = planeNormal;
            // O material é lido no ponto de CONTATO: numa roda bem na borda é a
            // diferença entre grip de asfalto e de grama.
            hit.Surface = SurfaceAtPoint(hit.Point.x, hit.Point.z);
            return true;
        }

        /// <summary>Material sob um ponto qualquer do mundo, sem raycast.</summary>
        public SurfaceKind SurfaceAtPoint(float x, float z)
        {
            int index = _path.NearestSampleIndex(x, z);
            return _path.SurfaceAt(index, _path.LateralOffset(index, x, z));
        }

        /// <summary>Altura da superfície sob um ponto qualquer do mundo.</summary>
        public float HeightAtPoint(float x, float z) => _path.TerrainHeight(x, z);
    }
}
