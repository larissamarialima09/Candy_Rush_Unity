using CandyRush.Config;
using CandyRush.Core;
using UnityEngine;

namespace CandyRush.Vehicle
{
    /// <summary>
    /// Máquina de estados do drift e do boost (convertido de src/vehicle/driftSystem.ts).
    ///
    /// Não aplica força nenhuma: recebe velocidade, ângulo de deslizamento e se
    /// há rodas no chão, e devolve "quanto empuxo" e "quanto teto de velocidade
    /// extra". Quem aplica é o Kart. A física do drift continua valendo mesmo
    /// sem esta classe: ela só observa e recompensa.
    /// </summary>
    public sealed class DriftSystem
    {
        /// <summary>True enquanto o kart está oficialmente derrapando (carregando).</summary>
        public bool Drifting { get; private set; }
        /// <summary>Carga acumulada, na unidade de DriftConfig.Charge.Levels.</summary>
        public float Charge { get; private set; }
        /// <summary>Nível conquistado com a carga atual: 0 a 3.</summary>
        public int Level { get; private set; }
        /// <summary>Sentido do deslizamento: -1, 0 ou +1 (para os efeitos).</summary>
        public int Direction { get; private set; }

        /// <summary>Nível do boost em curso, 0 se não há boost.</summary>
        public int BoostLevel { get; private set; }
        /// <summary>True apenas no passo em que o boost foi disparado.</summary>
        public bool BoostJustFired { get; private set; }
        /// <summary>True apenas no passo em que a carga foi perdida por rodada.</summary>
        public bool SpunOut { get; private set; }

        private float _boostTimeLeft;
        private float _boostDuration;
        private float _belowExitTimer;

        /// <param name="slipAngleDeg">Ângulo de deslizamento COM SINAL.</param>
        public void Update(float dt, float speed, float slipAngleDeg, bool onGround)
        {
            BoostJustFired = false;
            SpunOut = false;

            UpdateBoost(dt);

            float absSlip = Mathf.Abs(slipAngleDeg);
            bool fastEnough = speed >= DriftConfig.Detection.MinSpeed;

            // Rodar não é derrapar: passar do limite queima toda a carga.
            if (Drifting && absSlip >= DriftConfig.Detection.SpinOutAngleDeg)
            {
                SpunOut = true;
                StopDrifting(false);
                return;
            }

            if (!Drifting)
            {
                if (fastEnough && onGround && absSlip >= DriftConfig.Detection.EnterAngleDeg)
                {
                    Drifting = true;
                    Charge = 0f;
                    Level = 0;
                    _belowExitTimer = 0f;
                }
                return;
            }

            // --- Já está derrapando ---
            Direction = (int)MathUtils.SignOrZero(slipAngleDeg);

            bool stillSliding = absSlip >= DriftConfig.Detection.ExitAngleDeg && fastEnough && onGround;
            if (stillSliding)
            {
                _belowExitTimer = 0f;
                Charge += ChargeRate(speed, absSlip) * dt;
                Level = LevelForCharge(Charge);
            }
            else
            {
                // Tolerância curta: corrigir a traseira por um instante não encerra o drift.
                _belowExitTimer += dt;
                if (_belowExitTimer >= DriftConfig.Detection.ExitGrace) StopDrifting(true);
            }
        }

        /// <summary>Taxa de carga: maior quanto mais fundo o deslizamento e maior a velocidade.</summary>
        private static float ChargeRate(float speed, float absSlip)
        {
            float depthSpan = Mathf.Max(1f, DriftConfig.Charge.IdealAngleDeg - DriftConfig.Detection.EnterAngleDeg);
            float depth = Mathf.Clamp01((absSlip - DriftConfig.Detection.EnterAngleDeg) / depthSpan);
            float speedFactor = Mathf.Clamp(speed / DriftConfig.Charge.SpeedReference, 0.4f, 1.25f);
            return DriftConfig.Charge.BaseRatePerSecond * (1f + depth * DriftConfig.Charge.DepthBonus) * speedFactor;
        }

        /// <summary>Encerra o drift. release decide se o boost conquistado é entregue.</summary>
        private void StopDrifting(bool release)
        {
            if (release && Level > 0) TriggerBoost(Level);
            Drifting = false;
            Charge = 0f;
            Level = 0;
            Direction = 0;
            _belowExitTimer = 0f;
        }

        /// <summary>
        /// Dispara um boost de um nível. Público porque largada e rampas também dão turbo.
        /// Um boost novo só substitui o atual se for pelo menos tão forte.
        /// </summary>
        public void TriggerBoost(int level)
        {
            if (level <= 0) return;
            if (level < BoostLevel && _boostTimeLeft > 0f) return;

            float duration = DriftConfig.Boost.Duration[level - 1];
            BoostLevel = level;
            _boostDuration = duration;
            _boostTimeLeft = duration;
            BoostJustFired = true;
        }

        private void UpdateBoost(float dt)
        {
            if (_boostTimeLeft <= 0f) return;
            _boostTimeLeft -= dt;
            if (_boostTimeLeft <= 0f)
            {
                _boostTimeLeft = 0f;
                BoostLevel = 0;
            }
        }

        public bool IsBoosting => BoostLevel > 0;

        /// <summary>Empuxo extra, em N, já com o decaimento aplicado.</summary>
        public float BoostForce
        {
            get
            {
                if (BoostLevel == 0) return 0f;
                float peak = DriftConfig.Boost.Force[BoostLevel - 1];
                return peak * Mathf.Pow(BoostTimeRemaining, DriftConfig.Boost.DecayExponent);
            }
        }

        /// <summary>Quanto sobe o teto de potência do motor, em m/s.</summary>
        public float SpeedCeilingBonus =>
            BoostLevel == 0 ? 0f : DriftConfig.Boost.SpeedCeilingBonus[BoostLevel - 1] * BoostTimeRemaining;

        /// <summary>Fração restante do boost, 0..1.</summary>
        public float BoostTimeRemaining =>
            _boostDuration <= 0f ? 0f : Mathf.Clamp01(_boostTimeLeft / _boostDuration);

        /// <summary>Progresso dentro do nível atual, 0..1 (o que a barra do HUD desenha).</summary>
        public float LevelProgress
        {
            get
            {
                float[] levels = DriftConfig.Charge.Levels;
                if (Level == 3) return 1f;
                float from = Level == 0 ? 0f : levels[Level - 1];
                float to = levels[Level];
                return Mathf.Clamp01((Charge - from) / Mathf.Max(1e-6f, to - from));
            }
        }

        public void Reset()
        {
            Drifting = false;
            Charge = 0f;
            Level = 0;
            Direction = 0;
            BoostLevel = 0;
            _boostTimeLeft = 0f;
            _boostDuration = 0f;
            _belowExitTimer = 0f;
            BoostJustFired = false;
            SpunOut = false;
        }

        private static int LevelForCharge(float charge)
        {
            float[] levels = DriftConfig.Charge.Levels;
            if (charge >= levels[2]) return 3;
            if (charge >= levels[1]) return 2;
            if (charge >= levels[0]) return 1;
            return 0;
        }
    }
}
