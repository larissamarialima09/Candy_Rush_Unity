using System.Text;
using CandyRush.Config;
using CandyRush.Core;
using CandyRush.Game;
using CandyRush.Physics;
using CandyRush.Track;
using CandyRush.Vehicle;
using UnityEditor;
using UnityEngine;

namespace CandyRush.EditorTools
{
    /// <summary>
    /// Banco de provas da física, sem abrir o jogo (inspirado no `npm run sim`
    /// da versão web). Menu Candy Rush > Testar física, ou em linha de comando:
    ///   Unity -batchmode -quit -projectPath . -executeMethod CandyRush.EditorTools.CandyRushSimCheck.Run
    ///
    /// Mede: arrancada e velocidade final em chão plano, drift carregando
    /// turbo, e voltas completas na pista com o piloto automático.
    /// </summary>
    public static class CandyRushSimCheck
    {
        private const float Dt = PhysicsConfig.FixedTimeStep;

        [MenuItem("Candy Rush/Testar física")]
        public static void Run()
        {
            var report = new StringBuilder("[Candy Rush] Banco de provas\n");
            bool ok = true;
            ok &= StraightLine(report);
            ok &= DriftCharge(report);
            ok &= Laps(report);
            report.AppendLine(ok ? "RESULTADO: OK" : "RESULTADO: FALHOU");
            Debug.Log(report.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Acelerador cheio em chão plano: arrancada e velocidade final.</summary>
        private static bool StraightLine(StringBuilder report)
        {
            var kart = new Kart();
            var ground = new FlatGround();
            var input = new KartInput { Throttle = 1f };

            // Assentar na suspensão antes de medir.
            for (int i = 0; i < 60; i++) kart.Update(Dt, default, ground);

            float time = 0f, to30 = -1f, to60 = -1f;
            for (int i = 0; i < 60 * 20; i++)
            {
                kart.Update(Dt, input, ground);
                time += Dt;
                float kmh = kart.Telemetry.Speed * 3.6f;
                if (to30 < 0f && kmh >= 30f) to30 = time;
                if (to60 < 0f && kmh >= 60f) to60 = time;
            }
            float top = kart.Telemetry.Speed * 3.6f;
            float heading = Vector3.SignedAngle(Vector3.forward, CandyRush.Core.MathUtils.Flat(kart.Body.DirectionToWorld(Vector3.forward)), Vector3.up);

            report.AppendLine($"  Reta: 0-30 km/h em {to30:0.00}s, 0-60 em {to60:0.00}s, final {top:0.0} km/h, " +
                              $"rodas no chão {kart.Telemetry.WheelsOnGround}, desvio {heading:0.0}°");

            // Esperado ~77 km/h: o empuxo empata com arrasto + rolamento perto de 21,4 m/s.
            bool pass = to60 > 0f && top > 65f && top < 100f && kart.Telemetry.WheelsOnGround == 4 && Mathf.Abs(heading) < 1f;
            if (!pass) report.AppendLine("  ! Reta fora do esperado (arrancada, velocidade final ou kart torto).");
            return pass;
        }

        /// <summary>Entra numa curva com freio de mão: o drift precisa carregar e soltar turbo.</summary>
        private static bool DriftCharge(StringBuilder report)
        {
            var kart = new Kart();
            var ground = new FlatGround();
            for (int i = 0; i < 60 * 5; i++) kart.Update(Dt, new KartInput { Throttle = 1f }, ground);
            float entrySpeed = kart.Telemetry.Speed * 3.6f;

            int maxLevel = 0;
            float maxSlip = 0f;
            for (int i = 0; i < 60 * 2; i++)
            {
                kart.Update(Dt, new KartInput { Throttle = 1f, Steer = 1f, Handbrake = true }, ground);
                maxLevel = Mathf.Max(maxLevel, kart.Drift.Level);
                maxSlip = Mathf.Max(maxSlip, Mathf.Abs(kart.Telemetry.SlipAngleDeg));
            }

            bool boosted = false;
            for (int i = 0; i < 60 * 2; i++)
            {
                kart.Update(Dt, new KartInput { Throttle = 1f }, ground);
                boosted |= kart.Drift.IsBoosting;
            }

            // Virar à direita (+1) precisa mesmo virar à direita (sentido horário visto de cima).
            float heading = Vector3.SignedAngle(Vector3.forward, CandyRush.Core.MathUtils.Flat(kart.Body.DirectionToWorld(Vector3.forward)), Vector3.up);

            report.AppendLine($"  Drift: entrada {entrySpeed:0} km/h, deslizamento máx {maxSlip:0}°, nível máx {maxLevel}, " +
                              $"turbo {(boosted ? "sim" : "não")}, rumo final {heading:0}° (positivo = virou à direita)");

            bool pass = maxLevel >= 1 && boosted && heading > 20f;
            if (!pass) report.AppendLine("  ! Drift não carregou, não soltou turbo ou virou para o lado errado.");
            return pass;
        }

        /// <summary>Piloto automático na pista de verdade por 150 s.</summary>
        private static bool Laps(StringBuilder report)
        {
            var path = new CircuitPath(CircuitConfig.Centerline(), CircuitConfig.Samples, CircuitConfig.CurvatureWindow);
            var surface = new TrackSurface(path);
            var barriers = new BarrierSystem(path);
            var laps = new LapTracker(path);
            var kart = new Kart();
            kart.RespawnHandler = k => laps.Rescue(k);
            var autopilot = new Autopilot(path);

            // Fileira 0 do grid, como no jogo.
            Vector3 start = path.PointAt(0, RaceConfig.Grid.LateralOffset, TrackConfig.OffTrack.RespawnHeight);
            kart.RespawnAt(start, path.HeadingAt(0));

            int rescues = 0, touches = 0;
            float maxSpeed = 0f, lowestAboveTrack = float.PositiveInfinity;
            for (int i = 0; i < 60 * 150; i++)
            {
                kart.Update(Dt, autopilot.Drive(kart), surface);
                barriers.Update(Dt, kart);
                laps.Update(Dt, kart);
                if (laps.JustRescued) rescues++;
                if (barriers.Contact.Touching) touches++;
                maxSpeed = Mathf.Max(maxSpeed, kart.Telemetry.Speed * 3.6f);
                Vector3 p = kart.Body.ChassisPosition;
                lowestAboveTrack = Mathf.Min(lowestAboveTrack, p.y - path.TerrainHeight(p.x, p.z));
            }

            report.AppendLine($"  Pista: comprimento {path.TotalLength:0} m, voltas {laps.Lap}, " +
                              $"melhor volta {(float.IsInfinity(laps.BestLapTime) ? "-" : laps.BestLapTime.ToString("0.00") + "s")}, " +
                              $"vel. máx {maxSpeed:0} km/h, resgates {rescues}, passos raspando no muro {touches}, " +
                              $"menor altura sobre a pista {lowestAboveTrack:0.00} m");

            bool pass = laps.Lap >= 2 && lowestAboveTrack > -0.3f;
            if (!pass) report.AppendLine("  ! O piloto automático não completou 2 voltas ou o kart afundou na pista.");
            return pass;
        }
    }
}
