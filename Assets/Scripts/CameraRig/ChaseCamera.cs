using CandyRush.Config;
using CandyRush.Core;
using CandyRush.Vehicle;
using UnityEngine;

namespace CandyRush.CameraRig
{
    /// <summary>
    /// Câmera perseguidora com peso (convertido de src/camera/chaseCamera.ts).
    ///
    /// Duas molas independentes (posição e mira) integradas no PASSO FIXO, mais
    /// três modulações: recuo por aceleração, antecipação da mira por
    /// velocidade e inclinação lateral nas curvas. FOV dinâmico e tremor dão a
    /// sensação de velocidade. O visual interpola entre os dois últimos passos.
    /// </summary>
    public sealed class ChaseCamera
    {
        public readonly Camera Camera;

        private Vector3 _position;
        private Vector3 _velocity;
        private Vector3 _aim;
        private Vector3 _aimVelocity;
        private float _rollAngle;

        private Vector3 _previousAnchor;
        private Vector3 _previousAimTarget;

        /// <summary>Aceleração longitudinal filtrada, em m/s².</summary>
        private float _smoothedAccel;
        private float _previousForwardSpeed;

        private float _fov = EffectsConfig.Fov.AtRest;
        private float _shakeMeters;
        private float _shakeClock;

        // Estados anteriores, para interpolação.
        private Vector3 _prevPosition;
        private Vector3 _prevAim;
        private float _prevRoll;
        private float _prevFov = EffectsConfig.Fov.AtRest;
        private float _prevShake;

        private bool _initialized;

        public ChaseCamera(Camera camera)
        {
            Camera = camera;
            Camera.nearClipPlane = CameraConfig.Near;
            Camera.farClipPlane = CameraConfig.Far;
            Camera.fieldOfView = EffectsConfig.Fov.AtRest;
        }

        /// <summary>Teleporta a câmera para a posição ideal atrás do kart.</summary>
        public void Reset(Kart kart)
        {
            Vector3 kartPosition = kart.Body.ChassisPosition;
            Vector3 forward = FlatForward(kart);
            Vector3 anchor = kartPosition - forward * CameraConfig.Offset.Distance + Vector3.up * CameraConfig.Offset.Height;
            Vector3 aimTarget = kartPosition + forward * CameraConfig.Look.AheadBase + Vector3.up * CameraConfig.Look.Height;
            SnapTo(anchor, aimTarget);
            _initialized = true;
        }

        /// <summary>Um passo FIXO de câmera. offTrack só intensifica o tremor.</summary>
        public void Update(float dt, Kart kart, bool offTrack)
        {
            _prevPosition = _position;
            _prevAim = _aim;
            _prevRoll = _rollAngle;
            _prevFov = _fov;
            _prevShake = _shakeMeters;
            _shakeClock += dt;

            Vector3 kartPosition = kart.Body.ChassisPosition;
            // Segue o RUMO do kart, não a atitude: copiar arfagem e rolagem daria enjoo.
            Vector3 forward = FlatForward(kart);
            float forwardSpeed = kart.Telemetry.ForwardSpeed;

            // Respawn ANTES de medir aceleração: o teletransporte não pode virar frenagem gigante.
            bool snapping = !_initialized || (kart.Telemetry.Respawned && CameraConfig.SnapOnRespawn);
            if (snapping)
            {
                _smoothedAccel = 0f;
                _previousForwardSpeed = forwardSpeed;
            }

            // --- Recuo por aceleração ---
            float rawAccel = (forwardSpeed - _previousForwardSpeed) / dt;
            _previousForwardSpeed = forwardSpeed;
            _smoothedAccel += (rawAccel - _smoothedAccel) * Mathf.Min(1f, CameraConfig.Acceleration.Smoothing * dt);

            float accelOffset = Mathf.Clamp(_smoothedAccel * CameraConfig.Acceleration.MetersPerAccel,
                -CameraConfig.Acceleration.MaxPullIn, CameraConfig.Acceleration.MaxPullBack);

            // --- Âncora: onde a câmera GOSTARIA de estar ---
            float distance = CameraConfig.Offset.Distance + accelOffset;
            Vector3 anchor = kartPosition - forward * distance + Vector3.up * CameraConfig.Offset.Height;

            // --- Mira: à frente do kart, antecipando mais com a velocidade ---
            float ahead = CameraConfig.Look.AheadBase + Mathf.Max(0f, forwardSpeed) * CameraConfig.Look.AheadPerSpeed;
            Vector3 aimTarget = kartPosition + forward * ahead + Vector3.up * CameraConfig.Look.Height;

            if (snapping)
            {
                SnapTo(anchor, aimTarget);
                _initialized = true;
            }

            // Velocidade dos alvos por diferença finita (pré-alimentação da mola).
            Vector3 anchorVelocity = (anchor - _previousAnchor) / dt;
            Vector3 aimTargetVelocity = (aimTarget - _previousAimTarget) / dt;
            _previousAnchor = anchor;
            _previousAimTarget = aimTarget;

            IntegrateSpring(ref _position, ref _velocity, anchor, anchorVelocity,
                CameraConfig.Follow.Stiffness, CameraConfig.Follow.Damping, dt);
            IntegrateSpring(ref _aim, ref _aimVelocity, aimTarget, aimTargetVelocity,
                CameraConfig.Aim.Stiffness, CameraConfig.Aim.Damping, dt);

            // Nunca atravessar o chão.
            if (_position.y < CameraConfig.MinHeightAboveGround)
            {
                _position.y = CameraConfig.MinHeightAboveGround;
                if (_velocity.y < 0f) _velocity.y = 0f;
            }

            UpdateRoll(dt, kart, forwardSpeed);
            UpdateFieldOfView(dt, kart);
            UpdateShake(dt, kart, offTrack);
        }

        /// <summary>FOV dinâmico: o efeito de velocidade mais importante do jogo.</summary>
        private void UpdateFieldOfView(float dt, Kart kart)
        {
            float speedT = Mathf.Clamp01(kart.Telemetry.Speed / EffectsConfig.Fov.ReferenceSpeed);
            float target = Mathf.Lerp(EffectsConfig.Fov.AtRest, EffectsConfig.Fov.AtTopSpeed, speedT)
                           + EffectsConfig.Fov.BoostBonus * kart.Drift.BoostTimeRemaining;
            _fov += (target - _fov) * Mathf.Min(1f, EffectsConfig.Fov.Responsiveness * dt);
        }

        /// <summary>Amplitude do tremor: cresce com a velocidade, salta no turbo, multiplica fora do asfalto.</summary>
        private void UpdateShake(float dt, Kart kart, bool offTrack)
        {
            float speed = kart.Telemetry.Speed;
            float target = 0f;
            if (speed > EffectsConfig.Shake.MinSpeed)
            {
                float t = Mathf.Clamp01((speed - EffectsConfig.Shake.MinSpeed) /
                                        (EffectsConfig.Shake.ReferenceSpeed - EffectsConfig.Shake.MinSpeed));
                target = EffectsConfig.Shake.SpeedAmplitude * t;
                target += EffectsConfig.Shake.BoostAmplitude * kart.Drift.BoostTimeRemaining;
                if (offTrack) target *= EffectsConfig.Shake.OffTrackMultiplier;
            }
            _shakeMeters += (target - _shakeMeters) * Mathf.Min(1f, 8f * dt);
        }

        /// <summary>Inclinação lateral: aceleração lateral estimada por guinada × velocidade.</summary>
        private void UpdateRoll(float dt, Kart kart, float forwardSpeed)
        {
            float target = 0f;
            if (Mathf.Abs(forwardSpeed) > CameraConfig.Roll.MinSpeed)
            {
                float lateralAccel = kart.Body.AngularVelocity.y * forwardSpeed;
                target = Mathf.Clamp(-lateralAccel * CameraConfig.Roll.DegreesPerLateralAccel,
                    -CameraConfig.Roll.MaxDegrees, CameraConfig.Roll.MaxDegrees);
            }
            _rollAngle += (target - _rollAngle) * Mathf.Min(1f, CameraConfig.Roll.Responsiveness * dt);
        }

        /// <summary>Corta a suavização: respawn e primeiro quadro.</summary>
        private void SnapTo(Vector3 position, Vector3 aim)
        {
            _position = position;
            _aim = aim;
            _velocity = Vector3.zero;
            _aimVelocity = Vector3.zero;
            _rollAngle = 0f;
            _smoothedAccel = 0f;
            _previousAnchor = position;
            _previousAimTarget = aim;
            _prevPosition = position;
            _prevAim = aim;
            _prevRoll = 0f;
            _fov = EffectsConfig.Fov.AtRest;
            _prevFov = _fov;
            _shakeMeters = 0f;
            _prevShake = 0f;
            _shakeClock = 0f;
        }

        /// <summary>Aplica o estado interpolado à câmera da Unity. alpha vem do acumulador do loop.</summary>
        public void ApplyToRender(float alpha)
        {
            Vector3 position = Vector3.Lerp(_prevPosition, _position, alpha);
            Vector3 aim = Vector3.Lerp(_prevAim, _aim, alpha);
            float roll = Mathf.Lerp(_prevRoll, _rollAngle, alpha);

            Vector3 viewDirection = aim - position;
            if (viewDirection.sqrMagnitude < 1e-8f) viewDirection = Vector3.forward;
            viewDirection.Normalize();

            // Roll: gira o vetor "cima" em torno da direção de visão antes do LookRotation.
            Vector3 up = Quaternion.AngleAxis(roll, viewDirection) * Vector3.up;
            Transform t = Camera.transform;
            t.SetPositionAndRotation(position, Quaternion.LookRotation(viewDirection, up));

            Camera.fieldOfView = Mathf.Lerp(_prevFov, _fov, alpha);
            ApplyShake(alpha);
        }

        /// <summary>Tremor nos eixos locais da câmera. Duas frequências incomensuráveis evitam pulsar.</summary>
        private void ApplyShake(float alpha)
        {
            float amplitude = Mathf.Lerp(_prevShake, _shakeMeters, alpha);
            if (amplitude < 1e-4f) return;

            float time = _shakeClock;
            float x = Mathf.Sin(time * Mathf.PI * 2f * EffectsConfig.Shake.FrequencyA);
            float y = Mathf.Sin(time * Mathf.PI * 2f * EffectsConfig.Shake.FrequencyB + 1.7f);

            Transform t = Camera.transform;
            t.position += t.right * (x * amplitude) + t.up * (y * amplitude);

            float maxAmplitude = EffectsConfig.Shake.SpeedAmplitude + EffectsConfig.Shake.BoostAmplitude;
            float normalized = Mathf.Clamp01(amplitude / maxAmplitude);
            t.Rotate(0f, 0f, EffectsConfig.Shake.RollDegrees * normalized * y, Space.Self);
        }

        private static Vector3 FlatForward(Kart kart)
        {
            Vector3 forward = MathUtils.Flat(kart.Body.DirectionToWorld(Vector3.forward));
            return forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
        }

        /// <summary>
        /// Mola amortecida semi-implícita. O amortecimento age sobre a velocidade
        /// RELATIVA ao alvo: movimento uniforme não gera atraso nenhum, e a mola
        /// só reage ao que muda (acelerar, frear, virar).
        /// </summary>
        private static void IntegrateSpring(ref Vector3 position, ref Vector3 velocity, Vector3 target,
            Vector3 targetVelocity, float stiffness, float damping, float dt)
        {
            Vector3 acceleration = (target - position) * stiffness - (velocity - targetVelocity) * damping;
            velocity += acceleration * dt;
            position += velocity * dt;
        }
    }
}
