using UnityEngine;

namespace CandyRush.Physics
{
    /// <summary>
    /// Corpo rígido de 6 graus de liberdade com integração semi-implícita
    /// (Euler simplético): primeiro a velocidade, depois a posição com a
    /// velocidade NOVA. Convertido de src/physics/rigidBody.ts.
    ///
    /// Não usa o Rigidbody da Unity de propósito: a sensação do kart depende
    /// desta integração exata, em passo fixo de 1/60 s.
    ///
    /// Convenções:
    ///  - Position é a posição de mundo do CENTRO DE MASSA, não da origem do chassi.
    ///  - Pontos locais informados de fora (rodas etc.) são relativos à ORIGEM DO CHASSI.
    ///  - Inércia diagonal no espaço local (aproximação de caixa). Termos
    ///    giroscópicos ignorados de propósito (só trariam instabilidade).
    /// </summary>
    public sealed class KartRigidBody
    {
        /// <summary>Posição de mundo do centro de massa.</summary>
        public Vector3 Position;
        public Quaternion Orientation = Quaternion.identity;
        public Vector3 Velocity;
        /// <summary>Velocidade angular de mundo, em rad/s.</summary>
        public Vector3 AngularVelocity;

        public readonly Vector3 CenterOfMassLocal;
        public readonly float Mass;
        public readonly float InvMass;
        /// <summary>Inversa da inércia no espaço LOCAL (diagonal).</summary>
        public readonly Vector3 InvInertiaLocal;

        public float LinearDamping;
        public float AngularDamping;

        private Vector3 _force;
        private Vector3 _torque;

        public KartRigidBody(float mass, Vector3 size, Vector3 centerOfMass, Vector3 inertiaScale,
            float linearDamping, float angularDamping)
        {
            Mass = mass;
            InvMass = 1f / mass;
            CenterOfMassLocal = centerOfMass;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;

            // Inércia de uma caixa sólida homogênea: I_x = m/12 * (y² + z²), etc.
            float k = mass / 12f;
            float ix = k * (size.y * size.y + size.z * size.z) * inertiaScale.x;
            float iy = k * (size.x * size.x + size.z * size.z) * inertiaScale.y;
            float iz = k * (size.x * size.x + size.y * size.y) * inertiaScale.z;
            InvInertiaLocal = new Vector3(1f / ix, 1f / iy, 1f / iz);
        }

        /// <summary>Posiciona o corpo informando onde a ORIGEM DO CHASSI deve ficar, parado.</summary>
        public void SetChassisTransform(Vector3 chassisPosition, Quaternion orientation)
        {
            Orientation = orientation;
            Position = chassisPosition + Orientation * CenterOfMassLocal;
            Velocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            _force = Vector3.zero;
            _torque = Vector3.zero;
        }

        /// <summary>Posição de mundo da origem do chassi (o que o visual usa).</summary>
        public Vector3 ChassisPosition => Position - Orientation * CenterOfMassLocal;

        /// <summary>Converte um ponto local (relativo à origem do chassi) para mundo.</summary>
        public Vector3 LocalToWorld(Vector3 local) => Orientation * (local - CenterOfMassLocal) + Position;

        /// <summary>Converte uma direção local para mundo (sem translação).</summary>
        public Vector3 DirectionToWorld(Vector3 local) => Orientation * local;

        /// <summary>Velocidade de um ponto de mundo solidário ao corpo: v + ω × r.</summary>
        public Vector3 GetPointVelocity(Vector3 worldPoint) =>
            Velocity + Vector3.Cross(AngularVelocity, worldPoint - Position);

        public void AddForce(Vector3 force) => _force += force;

        /// <summary>Força aplicada em um ponto de mundo: gera força linear E torque.</summary>
        public void AddForceAtPoint(Vector3 force, Vector3 worldPoint)
        {
            _force += force;
            _torque += Vector3.Cross(worldPoint - Position, force);
        }

        public void AddTorque(Vector3 torque) => _torque += torque;

        /// <summary>Avança um passo FIXO. A gravidade (negativa) é aplicada no centro de massa, sem torque.</summary>
        public void Integrate(float dt, float gravity)
        {
            // --- Linear ---
            Vector3 acceleration = _force * InvMass;
            acceleration.y += gravity;
            Velocity += acceleration * dt;
            Velocity *= Mathf.Max(0f, 1f - LinearDamping * dt);

            // --- Angular: α = R · (I_local⁻¹ · (Rᵀ · τ)) ---
            Vector3 localTorque = Quaternion.Inverse(Orientation) * _torque;
            Vector3 localAlpha = Vector3.Scale(localTorque, InvInertiaLocal);
            AngularVelocity += (Orientation * localAlpha) * dt;
            AngularVelocity *= Mathf.Max(0f, 1f - AngularDamping * dt);

            // --- Posição com a velocidade já atualizada ---
            Position += Velocity * dt;

            // dq/dt = ½ · ω(quatérnio puro) · q
            Quaternion spin = new Quaternion(AngularVelocity.x, AngularVelocity.y, AngularVelocity.z, 0f) * Orientation;
            float half = 0.5f * dt;
            Orientation = Quaternion.Normalize(new Quaternion(
                Orientation.x + spin.x * half,
                Orientation.y + spin.y * half,
                Orientation.z + spin.z * half,
                Orientation.w + spin.w * half));

            _force = Vector3.zero;
            _torque = Vector3.zero;
        }
    }
}
