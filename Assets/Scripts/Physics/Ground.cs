using CandyRush.Config;
using UnityEngine;

namespace CandyRush.Physics
{
    /// <summary>Resultado de um raycast contra o chão (convertido de src/physics/ground.ts).</summary>
    public struct GroundHit
    {
        /// <summary>Distância percorrida ao longo do raio até o contato.</summary>
        public float Distance;
        public Vector3 Point;
        public Vector3 Normal;
        /// <summary>Material da superfície no ponto de contato.</summary>
        public SurfaceKind Surface;
    }

    /// <summary>
    /// Superfície contra a qual as rodas fazem raycast.
    /// Duas implementações: FlatGround (plano infinito, para testes controlados)
    /// e TrackSurface (a pista de verdade).
    /// </summary>
    public interface IGroundSampler
    {
        bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out GroundHit hit);
    }

    /// <summary>Plano infinito horizontal em y = Height. Sempre asfalto.</summary>
    public sealed class FlatGround : IGroundSampler
    {
        public float Height;

        public FlatGround(float height = 0f) => Height = height;

        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out GroundHit hit)
        {
            hit = default;
            // Só interessa raio descendo contra a face de cima do plano.
            if (direction.y >= -1e-4f) return false;

            float distance = (origin.y - Height) / -direction.y;
            if (distance < 0f || distance > maxDistance) return false;

            hit.Distance = distance;
            hit.Point = origin + direction * distance;
            hit.Normal = Vector3.up;
            hit.Surface = SurfaceKind.Asphalt;
            return true;
        }
    }
}
