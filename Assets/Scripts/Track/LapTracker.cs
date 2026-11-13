using CandyRush.Config;
using CandyRush.Vehicle;
using UnityEngine;

namespace CandyRush.Track
{
    /// <summary>Um portal atravessando a pista. A volta só vale se todos forem cruzados.</summary>
    public readonly struct Checkpoint
    {
        /// <summary>Índice da amostra do eixo onde o portal fica.</summary>
        public readonly int SampleIndex;
        public readonly Vector3 Position;
        /// <summary>Direção de corrida no portal (normal do plano do portal).</summary>
        public readonly Vector3 Forward;
        public readonly float HalfWidth;
        /// <summary>Rumo (rotação em Y, radianos) para reposicionar o kart.</summary>
        public readonly float Heading;

        public Checkpoint(int sampleIndex, Vector3 position, Vector3 forward, float halfWidth, float heading)
        {
            SampleIndex = sampleIndex;
            Position = position;
            Forward = forward;
            HalfWidth = halfWidth;
            Heading = heading;
        }
    }

    /// <summary>
    /// Checkpoints em sequência, contagem de voltas e cronômetro
    /// (convertido de src/track/lapTracker.ts).
    ///
    /// A volta só fecha se TODOS os portais forem cruzados na ordem: qualquer
    /// atalho pula um portal. A detecção é por cruzamento de plano (troca de
    /// lado), então funciona em qualquer velocidade.
    /// </summary>
    public sealed class LapTracker
    {
        public readonly Checkpoint[] Checkpoints;

        /// <summary>Índice do próximo portal a cruzar. 0 é a largada.</summary>
        public int NextCheckpoint { get; private set; } = 1;
        /// <summary>Último portal cruzado: é dele que sai o resgate.</summary>
        public int LastCheckpoint { get; private set; }

        public int Lap { get; private set; }
        public float CurrentLapTime { get; private set; }
        public float LastLapTime { get; private set; }
        public float BestLapTime { get; private set; } = float.PositiveInfinity;
        /// <summary>True apenas no passo em que uma volta foi fechada.</summary>
        public bool LapJustCompleted { get; private set; }

        /// <summary>Segundos acumulados fora da pista. Zera ao voltar ao asfalto.</summary>
        public float OffTrackTimer { get; private set; }
        /// <summary>True apenas no passo em que o kart foi devolvido à pista.</summary>
        public bool JustRescued { get; private set; }

        private readonly CircuitPath _path;
        /// <summary>Lado do plano de cada portal no passo anterior.</summary>
        private readonly int[] _previousSide;
        private bool _started;

        public LapTracker(CircuitPath path)
        {
            _path = path;
            int count = TrackConfig.Checkpoints.Count;
            Checkpoints = new Checkpoint[count];
            _previousSide = new int[count];

            for (int i = 0; i < count; i++)
            {
                int sampleIndex = path.IndexAtFraction((float)i / count);
                PathSample sample = path.Samples[sampleIndex];
                Checkpoints[i] = new Checkpoint(sampleIndex, sample.Position, sample.Tangent,
                    sample.HalfWidth + TrackConfig.Checkpoints.LateralMargin, path.HeadingAt(sampleIndex));
                _previousSide[i] = -1;
            }
        }

        /// <summary>Um passo fixo, depois da física do kart.</summary>
        public void Update(float dt, Kart kart)
        {
            LapJustCompleted = false;
            JustRescued = false;

            UpdateCrossings(kart.Body.ChassisPosition);

            if (_started) CurrentLapTime += dt;
            UpdateOffTrack(dt, kart);
        }

        private void UpdateCrossings(Vector3 kartPosition)
        {
            for (int i = 0; i < Checkpoints.Length; i++)
            {
                Checkpoint checkpoint = Checkpoints[i];
                Vector3 toKart = kartPosition - checkpoint.Position;

                int side = Vector3.Dot(toKart, checkpoint.Forward) >= 0f ? 1 : -1;
                int previous = _previousSide[i];
                _previousSide[i] = side;

                // Só interessa a travessia de trás para a frente, e dentro da largura.
                if (previous >= 0 || side <= 0) continue;
                Vector3 left = _path.Samples[checkpoint.SampleIndex].Left;
                float lateral = Mathf.Abs(toKart.x * left.x + toKart.z * left.z);
                if (lateral > checkpoint.HalfWidth) continue;

                OnCheckpointCrossed(i);
            }
        }

        private void OnCheckpointCrossed(int index)
        {
            if (index == 0)
            {
                // A largada só fecha volta se todos os outros portais já foram cruzados.
                if (!_started)
                {
                    _started = true;
                    CurrentLapTime = 0f;
                    NextCheckpoint = 1;
                    LastCheckpoint = 0;
                    return;
                }
                if (NextCheckpoint != 0) return;

                Lap++;
                LastLapTime = CurrentLapTime;
                if (LastLapTime < BestLapTime) BestLapTime = LastLapTime;
                CurrentLapTime = 0f;
                LapJustCompleted = true;
                NextCheckpoint = 1;
                LastCheckpoint = 0;
                return;
            }

            if (index != NextCheckpoint) return;
            LastCheckpoint = index;
            NextCheckpoint = (index + 1) % Checkpoints.Length;
        }

        /// <summary>
        /// Fora da pista: conta o tempo e, estourado o limite, devolve o kart ao
        /// ÚLTIMO portal válido (voltar ao mais próximo viraria atalho).
        /// </summary>
        private void UpdateOffTrack(float dt, Kart kart)
        {
            if (!kart.Telemetry.OffTrack || kart.Telemetry.WheelsOnGround == 0)
            {
                OffTrackTimer = 0f;
                return;
            }

            OffTrackTimer += dt;
            if (OffTrackTimer < TrackConfig.OffTrack.GraceSeconds) return;

            Rescue(kart);
        }

        /// <summary>Devolve o kart ao último portal cruzado, parado e apontado para a frente.</summary>
        public void Rescue(Kart kart)
        {
            Checkpoint checkpoint = Checkpoints[LastCheckpoint];
            Vector3 position = checkpoint.Position + Vector3.up * TrackConfig.OffTrack.RespawnHeight;
            kart.RespawnAt(position, checkpoint.Heading);
            OffTrackTimer = 0f;
            JustRescued = true;
        }

        public void Reset()
        {
            NextCheckpoint = 1;
            LastCheckpoint = 0;
            Lap = 0;
            CurrentLapTime = 0f;
            LastLapTime = 0f;
            BestLapTime = float.PositiveInfinity;
            OffTrackTimer = 0f;
            _started = false;
            for (int i = 0; i < _previousSide.Length; i++) _previousSide[i] = -1;
        }

        /// <summary>Fração de portais já cruzados nesta volta, 0..1 (para o HUD).</summary>
        public float LapProgress
        {
            get
            {
                int count = Checkpoints.Length;
                int done = NextCheckpoint == 0 ? count : NextCheckpoint;
                return (float)done / count;
            }
        }
    }
}
