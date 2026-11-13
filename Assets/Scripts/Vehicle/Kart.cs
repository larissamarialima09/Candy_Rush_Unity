using CandyRush.Config;
using CandyRush.Core;
using CandyRush.Physics;
using UnityEngine;

namespace CandyRush.Vehicle
{
    /// <summary>Números que o HUD, a câmera e os efeitos leem. Só leitura.</summary>
    public struct KartTelemetry
    {
        public float Speed;
        public float ForwardSpeed;
        /// <summary>Ângulo entre para onde o kart aponta e para onde anda, em graus, com sinal (-180..180).</summary>
        public float SlipAngleDeg;
        public float SteerAngleDeg;
        public int WheelsOnGround;
        public float DriveForce;
        public bool Reversing;
        /// <summary>True apenas no passo em que o kart foi reposicionado (a câmera corta a suavização).</summary>
        public bool Respawned;
        /// <summary>Material predominante sob as rodas.</summary>
        public SurfaceKind Surface;
        /// <summary>True quando NENHUMA roda está sobre asfalto ou zebra.</summary>
        public bool OffTrack;
    }

    /// <summary>
    /// O kart: simulação pura em C#, sem MonoBehaviour (convertido de src/vehicle/kart.ts).
    /// Quem desenha é o KartView; quem chama Update em passo fixo é o CandyRushGame.
    ///
    /// Suspensão por raycast em cada roda, pneus com limite de grip proporcional
    /// à carga e círculo de atrito, tração traseira, freio de mão que solta a
    /// traseira, arrasto e downforce.
    /// </summary>
    public sealed class Kart
    {
        private static readonly Vector3 LocalForward = Vector3.forward;
        private static readonly Vector3 LocalDown = Vector3.down;
        private static readonly Vector3 LocalUp = Vector3.up;

        public readonly KartRigidBody Body;
        public readonly Wheel[] Wheels;
        /// <summary>Detector de drift e medidor de boost. Só observa a física.</summary>
        public readonly DriftSystem Drift = new DriftSystem();

        public KartTelemetry Telemetry;

        /// <summary>
        /// De onde o kart renasce na tecla R ou quando cai/capota. Se nulo, usa
        /// KartConfig.Spawn.
        ///
        /// Correção em relação à web: lá o respawn ia sempre para (0; 0,9; 0),
        /// mas a pista tem elevação e a largada fica ~1,6 m acima disso. O kart
        /// nascia DEBAIXO do asfalto e caía em loop. Aqui o jogo aponta este
        /// delegate para o último checkpoint (LapTracker.Rescue).
        /// </summary>
        public System.Action<Kart> RespawnHandler;

        /// <summary>Estado do volante depois da suavização, em -1..1.</summary>
        private float _steerInput;
        private float _invertedTimer;
        /// <summary>Teto de velocidade da superfície, medido no passo anterior (atraso de 16 ms é invisível).</summary>
        private float _surfaceSpeedFactor = 1f;

        // Estados para interpolação de render.
        private Vector3 _prevPosition;
        private Quaternion _prevOrientation = Quaternion.identity;
        private Vector3 _currPosition;
        private Quaternion _currOrientation = Quaternion.identity;

        public Kart()
        {
            Body = new KartRigidBody(
                KartConfig.Chassis.Mass,
                KartConfig.Chassis.Size,
                KartConfig.Chassis.CenterOfMass,
                KartConfig.Chassis.InertiaScale,
                KartConfig.Chassis.LinearDamping,
                KartConfig.Chassis.AngularDamping);

            Wheels = BuildWheels();
            Respawn();
        }

        /// <summary>Recoloca o kart no ponto de renascimento, parado.</summary>
        public void Respawn()
        {
            if (RespawnHandler != null)
            {
                RespawnHandler(this);
                return;
            }
            RespawnAt(KartConfig.Spawn.Position, KartConfig.Spawn.HeadingDegrees * Mathf.Deg2Rad);
        }

        /// <summary>Reposiciona o kart num ponto e rumo quaisquer, parado.</summary>
        public void RespawnAt(Vector3 position, float headingRadians)
        {
            Body.SetChassisTransform(position, Quaternion.AngleAxis(headingRadians * Mathf.Rad2Deg, Vector3.up));
            _steerInput = 0f;
            _invertedTimer = 0f;
            Telemetry.Respawned = true;
            Drift.Reset();
            foreach (Wheel wheel in Wheels)
            {
                wheel.Grounded = false;
                wheel.Compression = 0f;
                wheel.SuspensionForce = 0f;
                wheel.SteerAngle = 0f;
            }
            _currPosition = Body.ChassisPosition;
            _currOrientation = Body.Orientation;
            _prevPosition = _currPosition;
            _prevOrientation = _currOrientation;
        }

        /// <summary>Um passo de física de tamanho FIXO.</summary>
        public void Update(float dt, KartInput input, IGroundSampler ground)
        {
            _prevPosition = _currPosition;
            _prevOrientation = _currOrientation;
            Telemetry.Respawned = false;

            if (input.RespawnPressed) Respawn();

            Vector3 forward = Body.DirectionToWorld(LocalForward);
            float forwardSpeed = Vector3.Dot(Body.Velocity, forward);

            // O ângulo de deslizamento é medido ANTES de qualquer força deste passo.
            float slipAngleDeg = ComputeSlipAngle();
            Telemetry.SlipAngleDeg = slipAngleDeg;
            Drift.Update(dt, Body.Velocity.magnitude, slipAngleDeg, Telemetry.WheelsOnGround > 0);

            UpdateSteering(dt, input);

            // --- Estado do trem de força para este passo ---
            bool reversing = input.Brake > 0f && forwardSpeed < KartConfig.Engine.ReverseEngageSpeed;
            bool throttleCut = input.Handbrake && KartConfig.Handbrake.CutsThrottle;
            float throttle = throttleCut ? 0f : input.Throttle;

            float driveTotal = 0f;
            if (reversing)
            {
                float ratio = Mathf.Clamp01(-forwardSpeed / KartConfig.Engine.ReverseTopSpeed);
                driveTotal = -KartConfig.Engine.ReverseForce * (1f - ratio) * input.Brake;
            }
            else if (throttle > 0f)
            {
                // Curva de torque: empuxo cheio parado, caindo até zero no teto.
                // O boost levanta o teto; a superfície (grama, escape) o corta.
                float cutoff = (KartConfig.Engine.PowerCutoffSpeed + Drift.SpeedCeilingBonus) * _surfaceSpeedFactor;
                float ratio = Mathf.Clamp01(forwardSpeed / cutoff);
                float curve = Mathf.Max(0f, 1f - Mathf.Pow(ratio, KartConfig.Engine.PowerFalloffExponent));
                driveTotal = KartConfig.Engine.MaxDriveForce * curve * throttle;
            }

            float braking = !reversing && input.Brake > 0f ? input.Brake : 0f;
            // Sem acelerador e sem freio: freio-motor.
            bool coasting = throttle == 0f && braking == 0f && !reversing;

            int poweredCount = 0;
            foreach (Wheel wheel in Wheels) if (wheel.Spec.Powered) poweredCount++;
            float drivePerWheel = poweredCount > 0 ? driveTotal / poweredCount : 0f;

            ApplyAero();
            ApplyBoost();

            int onGround = 0;
            float speedFactorSum = 0f;
            int pavedWheels = 0;
            foreach (Wheel wheel in Wheels)
            {
                UpdateWheel(wheel, dt, ground, drivePerWheel, braking, coasting, input.Handbrake);
                if (!wheel.Grounded) continue;
                onGround++;
                SurfaceMaterial surface = TrackConfig.Surface(wheel.Surface);
                speedFactorSum += surface.MaxSpeed;
                if (!surface.OffTrack) pavedWheels++;
            }

            // Média das rodas no chão: duas na grama e duas no asfalto = meio termo.
            _surfaceSpeedFactor = onGround > 0 ? speedFactorSum / onGround : 1f;
            Telemetry.OffTrack = onGround > 0 && pavedWheels == 0;
            Telemetry.Surface = DominantSurface();

            Body.Integrate(dt, PhysicsConfig.Gravity);
            UpdateRecovery(dt);

            _currPosition = Body.ChassisPosition;
            _currOrientation = Body.Orientation;

            UpdateTelemetry(forwardSpeed, driveTotal, reversing, onGround);
        }

        /// <summary>
        /// Volante: a intenção crua vira um ângulo suavizado, e o ângulo máximo
        /// ENCOLHE com a velocidade. Sem isso um toque a 90 km/h roda o kart.
        /// </summary>
        private void UpdateSteering(float dt, KartInput input)
        {
            float rate = input.Steer == 0f ? KartConfig.Steering.ReturnRate : KartConfig.Steering.TurnRate;
            _steerInput = MathUtils.MoveTowards(_steerInput, input.Steer, rate * dt);

            float speed = Body.Velocity.magnitude;
            float t = Mathf.Clamp01(speed / KartConfig.Steering.FalloffSpeed);
            float factor = Mathf.Lerp(1f, KartConfig.Steering.HighSpeedFactor, t);
            float maxAngle = KartConfig.Steering.MaxAngleDegrees * Mathf.Deg2Rad;

            // Assistência de contra-esterço: derrapando, o kart devolve parte do
            // esterço que a alta velocidade tinha tirado.
            if (Drift.Drifting)
            {
                factor = Mathf.Lerp(factor, 1f, DriftConfig.Steering.DriftFalloffRelief);
                maxAngle *= DriftConfig.Steering.DriftSteerMultiplier;
            }

            float angle = maxAngle * factor * _steerInput;
            foreach (Wheel wheel in Wheels)
            {
                if (wheel.Spec.Steered) wheel.SteerAngle = angle;
            }
        }

        /// <summary>Rede de segurança: kart caído do mapa ou capotado e parado volta ao início.</summary>
        private void UpdateRecovery(float dt)
        {
            if (Body.Position.y < KartConfig.Recovery.FallThroughHeight)
            {
                Respawn();
                return;
            }

            bool upsideDown = Body.DirectionToWorld(LocalUp).y < 0.2f;
            bool stopped = Body.Velocity.magnitude < KartConfig.Recovery.StuckSpeed;

            if (upsideDown && stopped)
            {
                _invertedTimer += dt;
                if (_invertedTimer >= KartConfig.Recovery.InvertedTimeout) Respawn();
            }
            else
            {
                _invertedTimer = 0f;
            }
        }

        /// <summary>Ângulo entre a frente do kart e a direção em que ele anda, no plano horizontal.</summary>
        private float ComputeSlipAngle()
        {
            Vector3 velocity = MathUtils.Flat(Body.Velocity);
            // Abaixo de 1 m/s o ângulo é só ruído numérico.
            if (velocity.sqrMagnitude < 1f) return 0f;

            Vector3 forward = MathUtils.Flat(Body.DirectionToWorld(LocalForward));
            if (forward.sqrMagnitude < 1e-6f) return 0f;
            forward.Normalize();

            float along = Vector3.Dot(velocity, forward);
            float lateral = forward.z * velocity.x - forward.x * velocity.z;
            return Mathf.Atan2(lateral, along) * Mathf.Rad2Deg;
        }

        /// <summary>Empuxo do boost, no CENTRO DE MASSA: força pura, sem torque.</summary>
        private void ApplyBoost()
        {
            float force = Drift.BoostForce;
            if (force <= 0f) return;
            Body.AddForce(Body.DirectionToWorld(LocalForward) * force);
        }

        /// <summary>Arrasto (contra a velocidade) e downforce (para baixo no mundo).</summary>
        private void ApplyAero()
        {
            float speed = Body.Velocity.magnitude;
            if (speed < 1e-3f) return;

            Vector3 force = Body.Velocity * (-KartConfig.Aero.DragCoefficient * speed);
            force.y -= KartConfig.Aero.DownforceCoefficient * speed * speed;
            Body.AddForce(force);
        }

        private void UpdateWheel(Wheel wheel, float dt, IGroundSampler ground,
            float drivePerWheel, float braking, bool coasting, bool handbrake)
        {
            WheelSpec spec = wheel.Spec;
            Vector3 attach = Body.LocalToWorld(spec.PositionLocal);
            Vector3 down = Body.DirectionToWorld(LocalDown);

            // O raio vai do ponto de fixação até onde o pneu tocaria com a mola solta.
            float rayLength = KartConfig.Suspension.RestLength + spec.Radius;

            if (!ground.Raycast(attach, down, rayLength, out GroundHit hit))
            {
                wheel.Grounded = false;
                wheel.Surface = SurfaceKind.Asphalt;
                wheel.Compression = 0f;
                wheel.SuspensionForce = 0f;
                wheel.ForceLateral = 0f;
                wheel.ForceLongitudinal = 0f;
                wheel.Saturated = false;
                wheel.WorldCenter = attach + down * KartConfig.Suspension.RestLength;
                return;
            }

            wheel.Grounded = true;
            wheel.Surface = hit.Surface;
            wheel.Compression = Mathf.Clamp(rayLength - hit.Distance, 0f, KartConfig.Suspension.MaxTravel);
            wheel.ContactPoint = hit.Point;
            wheel.ContactNormal = hit.Normal;
            wheel.WorldCenter = attach + down * (KartConfig.Suspension.RestLength - wheel.Compression);

            Vector3 up = -down;

            // --- Suspensão: mola + amortecedor, ao longo do eixo do chassi ---
            // Positivo quando a mola está comprimindo.
            float compressionSpeed = -Vector3.Dot(Body.GetPointVelocity(attach), up);
            float damper = compressionSpeed > 0f ? KartConfig.Suspension.DamperBump : KartConfig.Suspension.DamperRebound;
            float springForce = KartConfig.Suspension.Stiffness * wheel.Compression + damper * compressionSpeed;
            // A suspensão empurra, nunca puxa.
            springForce = Mathf.Clamp(springForce, 0f, KartConfig.Suspension.MaxForce);
            wheel.SuspensionForce = springForce;
            Body.AddForceAtPoint(up * springForce, hit.Point);

            // --- Referencial do pneu, projetado no plano do chão ---
            Vector3 forward = Body.DirectionToWorld(LocalForward);
            if (wheel.SteerAngle != 0f) forward = Quaternion.AngleAxis(wheel.SteerAngle * Mathf.Rad2Deg, up) * forward;
            forward -= hit.Normal * Vector3.Dot(forward, hit.Normal);
            if (forward.sqrMagnitude < 1e-8f) return;
            forward.Normalize();
            Vector3 right = Vector3.Cross(hit.Normal, forward).normalized;

            Vector3 contactVelocity = Body.GetPointVelocity(hit.Point);
            float vLong = Vector3.Dot(contactVelocity, forward);
            float vLat = Vector3.Dot(contactVelocity, right);
            wheel.SlipLongitudinal = vLong;
            wheel.SlipLateral = vLat;

            SurfaceMaterial surface = TrackConfig.Surface(wheel.Surface);
            float load = springForce; // carga vertical: é ela que compra grip
            float massShare = Body.Mass * KartConfig.Tires.TireMassShare;

            // --- Grip lateral: força que zeraria a velocidade lateral neste passo, saturada ---
            float gripCoefficient = (spec.IsFront ? KartConfig.Tires.FrontGrip : KartConfig.Tires.RearGrip) * surface.Grip;
            if (handbrake && !spec.IsFront) gripCoefficient *= KartConfig.Handbrake.RearGripMultiplier;

            float maxLateral = Mathf.Min(gripCoefficient * load, KartConfig.Tires.MaxLateralForce);
            float desiredLateral = -vLat * massShare / dt;
            float lateral = Mathf.Clamp(desiredLateral, -maxLateral, maxLateral);

            // --- Força longitudinal: tração, freio, rolamento, freio-motor ---
            // Só as rodas de tração recebem empuxo: o kart é traseira pura.
            float longitudinal = spec.Powered ? drivePerWheel : 0f;

            float resistance = braking * KartConfig.Brakes.Force * spec.BrakeShare;
            if (handbrake && !spec.IsFront) resistance += KartConfig.Handbrake.RearBrakeForce * 0.5f;
            resistance += KartConfig.Brakes.RollingResistance * surface.RollingResistance * Mathf.Abs(vLong);
            if (coasting) resistance += KartConfig.Brakes.EngineBraking * Mathf.Abs(vLong);

            // Resistência nunca pode inverter o sentido da roda dentro de um passo.
            float maxStopping = Mathf.Abs(vLong) * massShare / dt;
            resistance = Mathf.Min(resistance, maxStopping);
            longitudinal -= MathUtils.SignOrZero(vLong) * resistance;

            float maxLongitudinal = KartConfig.Tires.LongitudinalGrip * surface.Grip * load;
            longitudinal = Mathf.Clamp(longitudinal, -maxLongitudinal, maxLongitudinal);

            // --- Círculo (elipse) de atrito: lateral e longitudinal dividem o pneu ---
            wheel.Saturated = Mathf.Abs(desiredLateral) > maxLateral;
            if (KartConfig.Tires.UseFrictionCircle && maxLateral > 1e-3f && maxLongitudinal > 1e-3f)
            {
                float a = longitudinal / maxLongitudinal;
                float b = lateral / maxLateral;
                float combined = Mathf.Sqrt(a * a + b * b);
                if (combined > 1f)
                {
                    longitudinal /= combined;
                    lateral /= combined;
                    wheel.Saturated = true;
                }
            }

            wheel.ForceLongitudinal = longitudinal;
            wheel.ForceLateral = lateral;

            // A força do pneu é aplicada ACIMA do contato: no chão, o braço de
            // alavanca até o centro de massa capotaria o kart antes de escorregar.
            Vector3 tireForce = forward * longitudinal + right * lateral;
            Vector3 applyPoint = hit.Point + hit.Normal * KartConfig.Tires.ForceApplicationHeight;
            Body.AddForceAtPoint(tireForce, applyPoint);

            // Giro visual da roda.
            wheel.SpinAngle += vLong / spec.Radius * dt;
        }

        private void UpdateTelemetry(float forwardSpeed, float driveTotal, bool reversing, int onGround)
        {
            Telemetry.Speed = Body.Velocity.magnitude;
            Telemetry.ForwardSpeed = forwardSpeed;
            Telemetry.DriveForce = driveTotal;
            Telemetry.Reversing = reversing;
            Telemetry.WheelsOnGround = onGround;
            float steer = 0f;
            foreach (Wheel wheel in Wheels)
            {
                if (!wheel.Spec.Steered) continue;
                steer = wheel.SteerAngle;
                break;
            }
            Telemetry.SteerAngleDeg = steer * Mathf.Rad2Deg;
        }

        /// <summary>Transformação interpolada para o visual (evita microtravamento).</summary>
        public void GetRenderTransform(float alpha, out Vector3 position, out Quaternion orientation)
        {
            position = Vector3.Lerp(_prevPosition, _currPosition, alpha);
            orientation = Quaternion.Slerp(_prevOrientation, _currOrientation, alpha);
        }

        /// <summary>Material mais repetido entre as rodas no chão.</summary>
        private SurfaceKind DominantSurface()
        {
            var counts = new int[4];
            SurfaceKind best = SurfaceKind.Asphalt;
            int bestCount = 0;
            foreach (Wheel wheel in Wheels)
            {
                if (!wheel.Grounded) continue;
                int count = ++counts[(int)wheel.Surface];
                if (count <= bestCount) continue;
                bestCount = count;
                best = wheel.Surface;
            }
            return best;
        }

        private static Wheel[] BuildWheels()
        {
            float frontBias = KartConfig.Brakes.FrontBias;
            return new[]
            {
                new Wheel(new WheelSpec
                {
                    Name = "FL",
                    PositionLocal = new Vector3(-KartConfig.Wheels.HalfTrackFront, KartConfig.Wheels.AttachY, KartConfig.Wheels.FrontAxleZ),
                    Radius = KartConfig.Wheels.RadiusFront, Steered = true, Powered = false, IsFront = true,
                    BrakeShare = frontBias / 2f,
                }),
                new Wheel(new WheelSpec
                {
                    Name = "FR",
                    PositionLocal = new Vector3(KartConfig.Wheels.HalfTrackFront, KartConfig.Wheels.AttachY, KartConfig.Wheels.FrontAxleZ),
                    Radius = KartConfig.Wheels.RadiusFront, Steered = true, Powered = false, IsFront = true,
                    BrakeShare = frontBias / 2f,
                }),
                new Wheel(new WheelSpec
                {
                    Name = "RL",
                    PositionLocal = new Vector3(-KartConfig.Wheels.HalfTrackRear, KartConfig.Wheels.AttachY, KartConfig.Wheels.RearAxleZ),
                    Radius = KartConfig.Wheels.RadiusRear, Steered = false, Powered = true, IsFront = false,
                    BrakeShare = (1f - frontBias) / 2f,
                }),
                new Wheel(new WheelSpec
                {
                    Name = "RR",
                    PositionLocal = new Vector3(KartConfig.Wheels.HalfTrackRear, KartConfig.Wheels.AttachY, KartConfig.Wheels.RearAxleZ),
                    Radius = KartConfig.Wheels.RadiusRear, Steered = false, Powered = true, IsFront = false,
                    BrakeShare = (1f - frontBias) / 2f,
                }),
            };
        }
    }
}
