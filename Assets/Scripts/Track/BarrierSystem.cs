using CandyRush.Config;
using CandyRush.Core;
using CandyRush.Physics;
using CandyRush.Vehicle;
using UnityEngine;

namespace CandyRush.Track
{
    /// <summary>O que aconteceu no contato deste passo, para os efeitos usarem.</summary>
    public struct BarrierContact
    {
        public bool Touching;
        /// <summary>Ponto do mundo onde o kart raspou.</summary>
        public Vector3 Point;
        /// <summary>Velocidade com que o kart entrou na barreira, em m/s.</summary>
        public float ImpactSpeed;
        /// <summary>Velocidade ao longo da barreira, em m/s. Alta = raspão longo.</summary>
        public float SlideSpeed;
    }

    /// <summary>
    /// Barreiras laterais que RASPAM em vez de travar (convertido de src/track/barriers.ts).
    ///
    /// As barreiras seguem a spline: basta o deslocamento lateral do kart. Se
    /// passa do limite, o kart é empurrado de volta, a velocidade CONTRA o muro
    /// é quase toda absorvida e a velocidade AO LONGO do muro é preservada.
    /// </summary>
    public sealed class BarrierSystem
    {
        private readonly CircuitPath _path;

        public BarrierContact Contact;

        public BarrierSystem(CircuitPath path) => _path = path;

        /// <summary>Um passo fixo, depois da integração do kart.</summary>
        public void Update(float dt, Kart kart)
        {
            Contact = default;

            KartRigidBody body = kart.Body;
            Vector3 position = body.ChassisPosition;

            int index = _path.NearestSampleIndex(position.x, position.z);
            PathSample sample = _path.Samples[index];
            float lateral = _path.LateralOffset(index, position.x, position.z);

            float limit = sample.HalfWidth + TrackConfig.Barriers.OffsetFromEdge - TrackConfig.Barriers.KartRadius;
            float penetration = Mathf.Abs(lateral) - limit;
            if (penetration <= 0f) return;

            // Normal do muro, apontando PARA DENTRO da pista.
            float side = MathUtils.SignOrZero(lateral);
            if (side == 0f) side = 1f;
            Vector3 normal = sample.Left * -side;

            // Correção de posição: tira o kart de dentro do muro sem teletransporte.
            position += normal * penetration;
            body.Position += normal * penetration;

            float approaching = Vector3.Dot(body.Velocity, normal);
            if (approaching < 0f)
            {
                // Decompõe: normal (contra o muro) e tangencial (ao longo).
                Vector3 normalVelocity = normal * approaching;
                Vector3 tangentVelocity = body.Velocity - normalVelocity;

                // O atrito do raspão é uma taxa POR SEGUNDO, convertida para este passo.
                body.Velocity = tangentVelocity * Mathf.Pow(TrackConfig.Barriers.SlidePerSecond, dt)
                                - normalVelocity * TrackConfig.Barriers.Restitution;

                Contact.ImpactSpeed = -approaching;
                Contact.SlideSpeed = tangentVelocity.magnitude;
            }
            else
            {
                Contact.SlideSpeed = body.Velocity.magnitude;
            }

            // Alinhar o kart com o muro. Sem isso ele fica arando de través.
            Vector3 forward = MathUtils.Flat(body.DirectionToWorld(Vector3.forward));
            if (forward.sqrMagnitude > 1e-6f)
            {
                forward.Normalize();
                Vector3 wallDirection = MathUtils.Flat(sample.Tangent).normalized;
                // O muro serve nos dois sentidos: escolhe o que o kart está seguindo.
                if (Vector3.Dot(forward, wallDirection) < 0f) wallDirection = -wallDirection;

                float cross = forward.z * wallDirection.x - forward.x * wallDirection.z;
                float angle = Mathf.Atan2(cross, Vector3.Dot(forward, wallDirection));

                // CONVERGE para a guinada que endireita, em vez de somar a cada passo
                // (somando, o contato vira uma bomba de rotação).
                float desiredYawRate = angle * TrackConfig.Barriers.Alignment;
                Vector3 angular = body.AngularVelocity;
                angular.y += (desiredYawRate - angular.y) * Mathf.Clamp01(TrackConfig.Barriers.Alignment * dt);
                body.AngularVelocity = angular;
            }

            Contact.Touching = true;
            Contact.Point = position - normal * TrackConfig.Barriers.KartRadius;
            Contact.Point.y = _path.SurfaceHeight(index, lateral) + 0.3f;
        }
    }
}
