using CandyRush.Config;
using CandyRush.Game;
using CandyRush.Vehicle;
using UnityEngine;

namespace CandyRush.UI
{
    /// <summary>
    /// HUD provisório da Etapa 1, desenhado com IMGUI (OnGUI): velocidade,
    /// volta, tempos, barra de carga do drift e aviso de turbo.
    ///
    /// A interface definitiva (placar, medidor redondo, telas) é uma etapa própria;
    /// esta existe para dar para testar a física com os números à vista.
    /// </summary>
    public sealed class RaceHud
    {
        private static readonly Color Ink = CircuitConfig.Hex(0x5a2a44);
        private static readonly Color Panel = new Color(1f, 0.97f, 0.99f, 0.85f);
        private static readonly Color[] LevelColors =
        {
            CircuitConfig.Hex(0xfff2f7), CircuitConfig.Hex(0xff9ec4), CircuitConfig.Hex(0xa89bf0), CircuitConfig.Hex(0xffd76a),
        };

        private GUIStyle _big;
        private GUIStyle _small;
        private GUIStyle _center;
        private GUIStyle _bigCenter;
        private Texture2D _white;

        public void Draw(CandyRushWorld world, bool paused)
        {
            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.height / 720f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            Kart kart = world.Kart;
            var laps = world.Laps;

            // --- Painel de velocidade e voltas ---
            Box(new Rect(16, 16, 250, 156));
            GUI.Label(new Rect(28, 20, 240, 50), $"{Mathf.RoundToInt(kart.Telemetry.Speed * 3.6f)} km/h", _big);
            GUI.Label(new Rect(28, 70, 240, 22), $"Volta {laps.Lap + 1}   ·   {FormatTime(laps.CurrentLapTime)}", _small);
            GUI.Label(new Rect(28, 94, 240, 22), $"Última {FormatTime(laps.LastLapTime)}", _small);
            GUI.Label(new Rect(28, 116, 240, 22),
                $"Melhor {(float.IsInfinity(laps.BestLapTime) ? "--:--.--" : FormatTime(laps.BestLapTime))}", _small);
            GUI.Label(new Rect(28, 138, 240, 22), $"Moedas {world.Coins.Collected}", _small);

            // --- Barra de drift ---
            DriftSystem drift = kart.Drift;
            float barWidth = 320f;
            var bar = new Rect((width - barWidth) / 2f, height - 64f, barWidth, 22f);
            Box(new Rect(bar.x - 8, bar.y - 30, bar.width + 16, bar.height + 40));
            string driftText = drift.IsBoosting ? $"TURBO {drift.BoostLevel}!" : drift.Drifting ? $"Drift — nível {drift.Level}" : "Drift";
            GUI.Label(new Rect(bar.x, bar.y - 28, bar.width, 24), driftText, _center);
            Fill(bar, new Color(0f, 0f, 0f, 0.08f));
            float progress = drift.Drifting ? drift.LevelProgress : drift.BoostTimeRemaining;
            Color fill = LevelColors[Mathf.Clamp(drift.IsBoosting ? drift.BoostLevel : drift.Level, 0, 3)];
            if (!drift.Drifting && !drift.IsBoosting) fill = LevelColors[1];
            Fill(new Rect(bar.x, bar.y, bar.width * progress, bar.height), fill);

            // --- Fora da pista ---
            if (kart.Telemetry.OffTrack && laps.OffTrackTimer > 0.4f)
                GUI.Label(new Rect(0, 150, width, 30), "Volte para a pista!", _center);

            if (paused)
            {
                Fill(new Rect(0, 0, width, height), new Color(1f, 0.9f, 0.96f, 0.5f));
                GUI.Label(new Rect(0, height / 2f - 40, width, 50), "PAUSADO", _bigCenter);
                GUI.Label(new Rect(0, height / 2f + 10, width, 30), "Esc ou P para continuar", _center);
            }

            GUI.matrix = previous;
        }

        private static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes:00}:{rest:00.00}";
        }

        private void Box(Rect rect) => Fill(rect, Panel);

        private void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (_big != null) return;
            _white = Texture2D.whiteTexture;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _big.normal.textColor = Ink;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _small.normal.textColor = Ink;
            _center = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _center.normal.textColor = Ink;
            _bigCenter = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
