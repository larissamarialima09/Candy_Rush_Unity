using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CandyRush.Integrated
{
    /// <summary>Menu inicial em Canvas; configurações, HUD, pausa e resultados em IMGUI.</summary>
    public sealed partial class GameUI : MonoBehaviour
    {
        /// <summary>O que o HUD de UM jogador precisa, coletado pelo GameManager a cada quadro.</summary>
        public struct HudEntry
        {
            public int Index;
            public string Name;
            public Kart Kart;
            public LapTracker Tracker;
            public Viewport Viewport;
            public int CoinsCollected;
            public bool JustCollectedCoin;
        }

        static readonly Color[] LevelColors =
        {
            Color.white,
            new Color(1f, 0.62f, 0.77f),
            new Color(0.66f, 0.61f, 0.94f),
            new Color(1f, 0.84f, 0.42f),
        };
        static readonly string[] LevelLabels = { "DRIFT", "TURBO 1", "TURBO 2", "TURBO 3" };

        GameState state;
        Action onResume;
        Action onRestart;
        Action onMenu;
        Action<int> startRace;
        CandyFrontEnd frontEnd;
        int playerCount = 1;
        GameObject logoOverlay;

        bool finishVisible;
        bool helpVisible;
        bool musicSettingsVisible;
        float startCountdown;
        IReadOnlyList<Competitor> finishCompetitors;
        int finishPlayerPosition;
        Competitor finishWinner;
        IReadOnlyList<SeriesStanding> finishSeries;
        int finishHeats;

        readonly Dictionary<int, float> messageTimer = new Dictionary<int, float>();
        readonly Dictionary<int, string> messageText = new Dictionary<int, string>();
        readonly Dictionary<int, float> coinPopTimer = new Dictionary<int, float>();
        readonly Dictionary<int, float> vignetteOpacity = new Dictionary<int, float>();
        readonly Dictionary<int, float> speedLinesOpacity = new Dictionary<int, float>();
        readonly Dictionary<int, float> chromaticOpacity = new Dictionary<int, float>();

        IReadOnlyList<HudEntry> hudEntries = Array.Empty<HudEntry>();

        GUIStyle labelStyle, bigLabelStyle, titleStyle, detailStyle, warningStyle, buttonStyle, rowStyle;
        GUIStyle pinkPillStyle, purplePillStyle, pillLabelStyle;

        public bool FinishVisible => finishVisible;

        public void SetStartCountdown(float remaining) => startCountdown = remaining;

        public void ShowWrongWay(int playerIndex)
        {
            messageText[playerIndex] = "CONTRAMÃO! DE VOLTA À LARGADA";
            messageTimer[playerIndex] = 3f;
        }

        public void Init(GameState gameState, Action<int> start, Action resume, Action restart, Action menu = null)
        {
            state = gameState;
            startRace = start;
            onResume = resume;
            onRestart = restart;
            onMenu = menu;
            UiArt.Build();
            MenuClickSound.Initialize();
            frontEnd = new CandyFrontEnd(start);
            BuildLogoOverlay();
        }

        public void SetPlayerCount(int count) => playerCount = count;

        MenuCandyRush animatedMenu;

        void BuildLogoOverlay()
        {
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<StandaloneInputModule>();
            }
            logoOverlay = new GameObject("CandyRushMenuCanvas", typeof(RectTransform));
            logoOverlay.SetActive(false);
            logoOverlay.transform.SetParent(transform, false);
            var canvas = logoOverlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = logoOverlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1672, 941);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            logoOverlay.AddComponent<GraphicRaycaster>();
            var content = new GameObject("MenuCandyRush", typeof(RectTransform));
            content.transform.SetParent(logoOverlay.transform, false);
            animatedMenu = content.AddComponent<MenuCandyRush>();
            animatedMenu.aoJogar.AddListener(() => { animatedMenu.SalvarNome(); frontEnd.Play(false); });
            animatedMenu.aoConfigurar.AddListener(() => { animatedMenu.SalvarNome(); frontEnd.OpenSettings(); });

            // A loja cobre o menu e some sozinha (VOLTAR ou Esc), então começa desligada.
            var lojaObj = new GameObject("LojaDoce", typeof(RectTransform));
            lojaObj.transform.SetParent(logoOverlay.transform, false);
            var loja = lojaObj.AddComponent<ShopScreen>();
            lojaObj.SetActive(false);
            animatedMenu.aoLoja.AddListener(() => { animatedMenu.SalvarNome(); loja.gameObject.SetActive(true); });
            // OFERTA DO DIA: a loja fecha e a escolha de coelho abre com a oferta em foco.
            loja.aoOferta.AddListener(indice => frontEnd.OpenSelection(indice));
            loja.aoCorrer.AddListener(() => frontEnd.Play());
        }

        void Update()
        {
            if (state != null && state.Name != GameStateName.Paused) musicSettingsVisible = false;
            if (state != null && state.Name != GameStateName.Paused) helpVisible = false;
            UpdateScreenControls();
            if (state == null || logoOverlay == null) return;
            bool visible = state.Name == GameStateName.Menu && frontEnd.CurrentPage == CandyFrontEnd.Page.Home;
            if (logoOverlay.activeSelf != visible)
            {
                logoOverlay.SetActive(visible);
                if (visible) animatedMenu.AtualizarPerfil();
            }
        }
        public void UpdateHud(IReadOnlyList<HudEntry> entries, Coins coins, Viewport[] viewports, float dt, float fps)
        {
            hudEntries = entries;
            foreach (var e in entries)
            {
                bool justCollected = e.JustCollectedCoin;
                if (justCollected) coinPopTimer[e.Index] = 0.14f;
                coinPopTimer.TryGetValue(e.Index, out float pop);
                coinPopTimer[e.Index] = Mathf.Max(0, pop - dt);

                var tracker = e.Tracker;
                if (tracker.LapJustCompleted)
                {
                    bool isBest = tracker.LastLapTime <= tracker.BestLapTime;
                    messageText[e.Index] = isBest
                        ? $"VOLTA MAIS RÁPIDA  {FormatTime(tracker.LastLapTime)}"
                        : $"VOLTA  {FormatTime(tracker.LastLapTime)}";
                    messageTimer[e.Index] = 2.4f;
                }
                if (tracker.JustRescued)
                {
                    messageText[e.Index] = "DE VOLTA À PISTA";
                    messageTimer[e.Index] = 1.6f;
                }
                messageTimer.TryGetValue(e.Index, out float mt);
                messageTimer[e.Index] = Mathf.Max(0, mt - dt);

                // Efeitos de tela: vinheta, linhas de velocidade e aberração cromática.
                var kart = e.Kart;
                float speed = kart.Telemetry.Speed;
                float boost = kart.Drift.BoostTimeRemaining;
                float speedT = Mathf.Clamp01((speed - EffectsConfig.Overlay.VignetteStartSpeed) / Mathf.Max(1, EffectsConfig.Overlay.ReferenceSpeed - EffectsConfig.Overlay.VignetteStartSpeed));
                float targetVignette = EffectsConfig.Overlay.VignetteMax * speedT;
                float targetLines = EffectsConfig.Overlay.SpeedLinesMax * Mathf.Max(speedT * speedT, boost);
                float targetChromatic = EffectsConfig.Overlay.ChromaticMax * boost;
                float t = Mathf.Min(1, EffectsConfig.Overlay.Responsiveness * dt);

                vignetteOpacity.TryGetValue(e.Index, out float v);
                speedLinesOpacity.TryGetValue(e.Index, out float sl);
                chromaticOpacity.TryGetValue(e.Index, out float ch);
                vignetteOpacity[e.Index] = v + (targetVignette - v) * t;
                speedLinesOpacity[e.Index] = sl + (targetLines - sl) * t;
                chromaticOpacity[e.Index] = ch + (targetChromatic - ch) * t;
            }
        }

        public void ShowFinish(IReadOnlyList<Competitor> competitors, int playerPosition, Competitor winner,
            IReadOnlyList<SeriesStanding> series, int heats)
        {
            if (finishVisible) return;
            finishVisible = true;
            finishCompetitors = competitors;
            finishPlayerPosition = playerPosition;
            finishWinner = winner;
            finishSeries = series;
            finishHeats = heats;
        }

        public void HideFinish() => finishVisible = false;

        void EnsureStyles()
        {
            if (labelStyle != null) return;
            Font font = UiArt.Font;

            labelStyle = new GUIStyle { font = font, fontSize = 16, normal = { textColor = Color.white } };
            bigLabelStyle = new GUIStyle { font = font, fontSize = 34, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            titleStyle = new GUIStyle { font = font, fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.4f, 0.65f) } };
            detailStyle = new GUIStyle { font = font, fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = new Color(0.35f, 0.22f, 0.43f) } };
            warningStyle = new GUIStyle { font = font, fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 19, fontStyle = FontStyle.Bold,
                border = new RectOffset(), alignment = TextAnchor.MiddleCenter };
            buttonStyle.normal.background = UiArt.RestartPill;
            buttonStyle.hover.background = UiArt.RestartHoverPill;
            buttonStyle.active.background = UiArt.RestartPressedPill;
            buttonStyle.focused.background = UiArt.RestartHoverPill;
            buttonStyle.normal.textColor = buttonStyle.hover.textColor = buttonStyle.active.textColor = buttonStyle.focused.textColor = new Color(.28f, .13f, .35f);
            rowStyle = new GUIStyle { font = font, fontSize = 15, normal = { textColor = Color.white } };

            pinkPillStyle = PauseButtonStyle(UiArt.PauseResumePill);
            purplePillStyle = PauseButtonStyle(UiArt.PauseHomePill);
            pillLabelStyle = new GUIStyle { font = font, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        static readonly Color LavenderText = new Color(0.42f, 0.22f, 0.6f);
        static readonly Color HudInk = new Color(0.2f, 0.1f, 0.2f);
        /// <summary>Fundo escuro para preservar o contraste dos textos sobre a pista.</summary>
        internal static readonly Color CardNavy = new Color(0.12f, 0.15f, 0.27f, .88f);
        internal static readonly Color MeterOutline = new Color(1, .86f, .94f, .85f);
        static readonly Color HudPink = new Color(1f, .36f, .72f);

        static GUIStyle PauseButtonStyle(Texture2D background)
        {
            // A textura tem a proporção final do botão, como no menu inicial.
            var style = new GUIStyle { border = new RectOffset() };
            style.normal.background = style.hover.background =
                style.active.background = style.focused.background = background;
            return style;
        }

        /// <summary>
        /// Botão em formato de pílula com ícone à esquerda e texto, como nas
        /// telas do menu inicial, com textura na proporção final para
        /// preservar as extremidades semicirculares.
        /// </summary>
        bool IconButton(Rect r, Texture2D icon, string label, GUIStyle pillStyle, float fontSize, Color? tint = null)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover)
            {
                float growth = Input.GetMouseButton(0) ? 1.07f : 1.035f;
                r = new Rect(r.center.x - r.width * growth / 2, r.center.y - r.height * growth / 2,
                    r.width * growth, r.height * growth);
            }
            var buttonColor = GUI.color;
            if (hover) GUI.color = Input.GetMouseButton(0) ? new Color(1, .72f, .9f) : new Color(1, .9f, .98f);
            bool clicked = GUI.Button(r, GUIContent.none, pillStyle);
            GUI.color = buttonColor;
            float iconSize = r.height * 0.46f;
            float iconX = r.x + r.height * 0.36f;
            var previousColor = GUI.color;
            if (tint.HasValue) GUI.color = tint.Value;
            GUI.DrawTexture(new Rect(iconX, r.y + (r.height - iconSize) / 2, iconSize, iconSize), icon);
            GUI.color = previousColor;
            var textStyle = new GUIStyle(pillLabelStyle) { fontSize = Mathf.RoundToInt(fontSize) };
            if (tint.HasValue) textStyle.normal.textColor = tint.Value;
            float textX = iconX + iconSize + 8;
            GUI.Label(new Rect(textX, r.y, r.xMax - textX - 12, r.height), label, textStyle);
            return clicked;
        }

        void OnGUI()
        {
            if (state == null) return;
            EnsureStyles();
            if (editingHud && state.Name == GameStateName.Paused) { DrawHudEditor(); return; }
            if (state.Name == GameStateName.Menu) { if (frontEnd.CurrentPage != CandyFrontEnd.Page.Home) frontEnd.Draw(); return; }

            if (!finishVisible && (state.Name == GameStateName.Racing || helpVisible))
            {
                foreach (var e in hudEntries) DrawPlayerHud(e);
                DrawScreenControls();
                DrawStartCountdown();
                if (helpVisible) { DrawControlHelp(); return; }
            }

            var previousMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 960f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 960 * scale) / 2,
                (Screen.height - 720 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            if (finishVisible) DrawFinishScreen();
            else if (state.Name == GameStateName.Paused)
            {
                if (musicSettingsVisible) DrawMusicSettings();
                else DrawPause();
            }
            GUI.matrix = previousMatrix;
        }

        // --- HUD de corrida ---

        void DrawStartCountdown()
        {
            if (startCountdown <= 0) return;
            string text = startCountdown > .8f ? Mathf.CeilToInt(startCountdown - .8f).ToString() : "GO";
            foreach (var entry in hudEntries)
            {
                var v = entry.Viewport;
                float size = Mathf.Min(v.Height * .32f, v.Width * .23f);
                var rect = new Rect(v.X, v.Y + v.Height * .25f, v.Width, size * 1.5f);
                var style = new GUIStyle(bigLabelStyle)
                {
                    fontSize = Mathf.RoundToInt(size),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                float edge = Mathf.Max(2f, size * .025f);
                style.normal.textColor = new Color(.3f, .07f, .28f, .9f);
                GUI.Label(new Rect(rect.x + edge * 2, rect.y + edge * 3, rect.width, rect.height), text, style);
                style.normal.textColor = Color.white;
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        if (x != 0 || y != 0)
                            GUI.Label(new Rect(rect.x + x * edge, rect.y + y * edge, rect.width, rect.height), text, style);
                style.normal.textColor = HudPink;
                GUI.Label(rect, text, style);
            }
        }

        void DrawPlayerHud(HudEntry e)
        {
            Rect vp = new Rect(e.Viewport.X, e.Viewport.Y, e.Viewport.Width, e.Viewport.Height);
            var kart = e.Kart;
            var tracker = e.Tracker;

            // Efeitos de tela (dentro da faixa deste jogador).
            vignetteOpacity.TryGetValue(e.Index, out float vig);
            if (vig > 0.001f) DrawFaded(UiArt.Vignette, vp, vig);
            speedLinesOpacity.TryGetValue(e.Index, out float lines);
            if (lines > 0.001f) DrawFaded(UiArt.SpeedLines, vp, lines);
            chromaticOpacity.TryGetValue(e.Index, out float chroma);
            if (chroma > 0.001f)
            {
                GUI.DrawTexture(new Rect(vp.x, vp.y, 30, vp.height), UiArt.Chromatic, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, chroma), 0, 0);
                GUI.DrawTexture(new Rect(vp.xMax - 30, vp.y, 30, vp.height), UiArt.Chromatic, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, chroma), 0, 0);
            }

            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            safe.y = Screen.height - safe.yMax;
            vp = Rect.MinMaxRect(Mathf.Max(vp.xMin, safe.xMin), Mathf.Max(vp.yMin, safe.yMin),
                Mathf.Min(vp.xMax, safe.xMax), Mathf.Min(vp.yMax, safe.yMax));
            float scale = MobileControlLayout.Create(vp).Scale;
            float pad = 14 * scale;

            // Tarja do piloto (só no coop).
            if (playerCount > 1)
            {
                var tag = new Rect(vp.x + pad, vp.y + pad, 180 * scale, 22 * scale);
                GUI.color = Color.white;
                HudRoundRect(tag, Color.white, tag.height / 2);
                var tagLabel = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(14 * scale) };
                tagLabel.normal.textColor = Color.black;
                GUI.Label(tag, e.Name, tagLabel);
            }

            // Card de corrida, canto superior direito: posição, velocidade e progresso da volta.
            float cardW = 236 * scale, cardH = 100 * scale;
            var card = new Rect(vp.xMax - cardW - pad, vp.y + pad, cardW, cardH);
            HudRoundRect(card, CardNavy, 22 * scale);

            var positionStyle = new GUIStyle(bigLabelStyle) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(38 * scale) };
            positionStyle.normal.textColor = HudPink;
            GUI.Label(new Rect(card.x + 14 * scale, card.y + 6 * scale, 64 * scale, 44 * scale), $"{RacePosition(kart)}º", positionStyle);
            var ofStyle = new GUIStyle(bigLabelStyle) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(22 * scale) };
            ofStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(card.x + 62 * scale, card.y + 14 * scale, 50 * scale, 30 * scale), $"/{raceDirector?.Competitors.Count ?? 1}", ofStyle);

            HudRoundRect(new Rect(card.x + 112 * scale, card.y + 14 * scale, 1.5f * scale, 36 * scale), new Color(1, 1, 1, .25f), 0);
            DrawIcon(new Rect(card.x + 124 * scale, card.y + 14 * scale, 30 * scale, 30 * scale), UiArt.GaugeIcon, HudPink);
            var speedStyle = new GUIStyle(bigLabelStyle) { alignment = TextAnchor.MiddleRight, fontSize = Mathf.RoundToInt(32 * scale) };
            speedStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(card.x + 156 * scale, card.y + 8 * scale, cardW - 168 * scale, 36 * scale),
                Mathf.RoundToInt(kart.Telemetry.Speed * 3.6f).ToString(), speedStyle);
            var unitStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleRight, fontSize = Mathf.RoundToInt(12 * scale), fontStyle = FontStyle.Bold };
            unitStyle.normal.textColor = new Color(1, 1, 1, .8f);
            GUI.Label(new Rect(card.x + 156 * scale, card.y + 42 * scale, cardW - 168 * scale, 16 * scale), "km/h", unitStyle);

            HudRoundRect(new Rect(card.x + 14 * scale, card.y + 58 * scale, cardW - 28 * scale, 1 * scale), new Color(1, 1, 1, .25f), 0);
            var lapStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(13 * scale), fontStyle = FontStyle.Bold };
            lapStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(card.x + 14 * scale, card.y + 64 * scale, 150 * scale, 18 * scale), $"VOLTA {tracker.Lap + 1}/{RaceConfig.Laps}", lapStyle);
            var lapTrack = new Rect(card.x + 14 * scale, card.y + 86 * scale, cardW - 28 * scale, 7 * scale);
            HudRoundRect(lapTrack, new Color(1, 1, 1, .22f), 3.5f * scale);
            float lapFraction = LapFraction(kart);
            if (lapFraction > 0) HudRoundRect(new Rect(lapTrack.x, lapTrack.y, lapTrack.width * lapFraction, lapTrack.height), HudPink, 3.5f * scale);

            // Moedas, logo abaixo do card.
            coinPopTimer.TryGetValue(e.Index, out float pop);
            float coinScale = pop > 0 ? 1.15f : 1f;
            var coinBox = new Rect(card.xMax - 90 * scale, card.yMax + 6 * scale, 90 * scale, 30 * scale * coinScale);
            HudRoundRect(coinBox, CardNavy, coinBox.height / 2);
            GUI.DrawTexture(new Rect(coinBox.x + 4, coinBox.y + 3, coinBox.height - 6, coinBox.height - 6), UiArt.CoinDot);
            var coinStyle = new GUIStyle(labelStyle) { fontSize = Mathf.RoundToInt(16 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            coinStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(coinBox.x, coinBox.y, coinBox.width - 10, coinBox.height), e.CoinsCollected.ToString(), coinStyle);

            DrawDriftMeter(vp, scale, kart);
            DrawMinimap(e);

            // Aviso central: fora da pista tem prioridade sobre a mensagem de volta.
            string warning = null;
            if (kart.Telemetry.OffTrack && tracker.OffTrackTimer > 0.35f)
            {
                float remaining = Mathf.Max(0, TrackConfig.OffTrackGraceSeconds - tracker.OffTrackTimer);
                warning = $"FORA DA PISTA  {remaining:0.0}";
            }
            else if (messageTimer.TryGetValue(e.Index, out float wt) && wt > 0)
            {
                messageText.TryGetValue(e.Index, out warning);
            }

            if (!string.IsNullOrEmpty(warning))
            {
                var warnRect = new Rect(vp.x + vp.width * 0.5f - 220 * scale, vp.y + 60 * scale, 440 * scale, 40 * scale);
                HudRoundRect(warnRect, Color.white, warnRect.height / 2);
                var ws = new GUIStyle(warningStyle) { fontSize = Mathf.RoundToInt(16 * scale) };
                ws.normal.textColor = new Color(0.2f, 0.1f, 0.2f);
                GUI.Label(warnRect, warning, ws);
            }
        }

        /// <summary>Carga compacta do turbo; o drift possui botão independente.</summary>
        void DrawDriftMeter(Rect vp, float scale, Kart kart)
        {
            float chargeRatio = kart.Turbo.Charge;
            float fillRatio = kart.Turbo.IsBoosting ? kart.Turbo.Remaining : chargeRatio;

            var meterRect = DriftBarRect(vp, scale);
            HudRoundRect(Grow(meterRect,2*Controls.Scale),MeterOutline,meterRect.height/2+2*Controls.Scale);
            HudRoundRect(meterRect, CardNavy, meterRect.height / 2);
            scale = meterRect.height / 24f;
            float inset = 4 * scale;
            var fillRect = new Rect(meterRect.x + inset, meterRect.y + inset, (meterRect.width - inset * 2) * Mathf.Clamp01(fillRatio), meterRect.height - inset * 2);
            if (fillRect.width > 0) HudRoundRect(fillRect, HudPink, fillRect.height / 2);
            var driftLabelStyle = new GUIStyle(labelStyle) { fontSize = Mathf.RoundToInt(12 * scale), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            driftLabelStyle.normal.textColor = Color.white;
            string meterLabel = kart.Turbo.IsBoosting ? "TURBO!" : kart.Turbo.Ready ? "TURBO PRONTO" : "TURBO";
            GUI.Label(meterRect, meterLabel, driftLabelStyle);
        }

        static void HudRoundRect(Rect rect, Color color, float radius)
        {
            GUI.DrawTexture(rect, UiArt.White, ScaleMode.StretchToFill, true, 0, color, 0, radius);
        }

        static void DrawFaded(Texture2D tex, Rect rect, float alpha)
        {
            var prev = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
            GUI.color = prev;
        }

        // --- Menu ---

        void DrawSweetBackdrop()
        {
            float scale = Mathf.Min(Screen.width / 960f, Screen.height / 720f);
            var fullBleed = new Rect((960 - Screen.width / scale) / 2, (720 - Screen.height / scale) / 2,
                Screen.width / scale, Screen.height / scale);
            GUI.DrawTexture(fullBleed, UiArt.MenuBackdropPhoto != null ? UiArt.MenuBackdropPhoto : UiArt.MenuSky,
                ScaleMode.ScaleAndCrop);
        }

        /// <summary>Cartão com borda rosa e cantos bem redondos. Desenhado com raio real, não com textura esticada.</summary>
        void DrawSweetCard(Rect rect)
        {
            const float radius = 56;
            HudRoundRect(new Rect(rect.x + 5, rect.y + 9, rect.width, rect.height), new Color(.47f, .25f, .55f, .18f), radius);
            HudRoundRect(rect, new Color(1f, .55f, .78f), radius);
            HudRoundRect(Grow(rect, -7), new Color(1f, .97f, .99f), radius - 7);
        }

        void DrawPause()
        {
            DrawSweetBackdrop();
            var card = new Rect(210, 90, 540, 540);
            var previousColor = GUI.color;
            GUI.color = new Color(.47f, .25f, .55f, .18f);
            GUI.DrawTexture(new Rect(card.x + 5, card.y + 9, card.width, card.height), UiArt.PausePanel);
            GUI.color = previousColor;
            GUI.DrawTexture(card, UiArt.PausePanel);


            GUI.Label(new Rect(card.x + 40, card.y + 65, card.width - 80, 46), "UMA PAUSA DOCE",
                new GUIStyle(titleStyle) { fontSize = 34 });
            GUI.Label(new Rect(card.x + 40, card.y + 120, card.width - 80, 34),
                "Respire. A pista espera por você!", detailStyle);
            if (IconButton(new Rect(card.x + 80, card.y + 195, 380, 82), UiArt.BackIcon,
                "VOLTAR À CORRIDA", pinkPillStyle, 21))
            {
                helpVisible = false;
                onResume?.Invoke();
            }
            if (IconButton(new Rect(card.x + 100, card.y + 285, 340, 64), UiArt.GearIcon,
                "CONFIGURAÇÕES", purplePillStyle, 21, LavenderText)) musicSettingsVisible = true;
            if (IconButton(new Rect(card.x + 100, card.y + 355, 340, 64), UiArt.GearIcon,
                "Ajuda", purplePillStyle, 23, LavenderText))
                OpenHelp();
            if (onMenu != null && IconButton(new Rect(card.x + 100, card.y + 425, 340, 60), UiArt.HomeIcon,
                "MENU INICIAL", purplePillStyle, 19, LavenderText)) onMenu();
        }

        void OpenMusicSettings()
        {
            helpVisible = false;
            if (state.IsRacing) state.TogglePause();
            musicSettingsVisible = true;
        }

        void DrawMusicSettings()
        {
            DrawSweetBackdrop();
            var card = new Rect(210, 90, 540, 540);
            GUI.DrawTexture(card, UiArt.PausePanel);
            GUI.Label(new Rect(card.x + 36, card.y + 65, card.width - 72, 46), "CONFIGURAÇÕES",
                new GUIStyle(titleStyle) { fontSize = 32 });
            GUI.Label(new Rect(card.x + 40, card.y + 128, card.width - 80, 60),
                "Quer continuar ouvindo a música?", new GUIStyle(detailStyle) { wordWrap = true, fontSize = 22 });
            bool enabled = CandyMusic.MusicEnabled;
            Rect toggle = new Rect(card.x + 65, card.y + 220, 410, 88);
            HudRoundRect(toggle, enabled ? HudPink : CardNavy, 44);
            GUI.Label(new Rect(toggle.x + 22, toggle.y, 230, toggle.height), "MÚSICA",
                new GUIStyle(pillLabelStyle) { fontSize = 26, normal = { textColor = Color.white } });
            Rect track = new Rect(toggle.xMax - 140, toggle.y + 22, 116, 44);
            HudRoundRect(track, new Color(1, 1, 1, .28f), 22);
            float knobX = enabled ? track.xMax - 40 : track.x + 4;
            HudRoundRect(new Rect(knobX, track.y + 4, 36, 36), Color.white, 18);
            GUI.Label(new Rect(toggle.x + 206, toggle.y, 80, toggle.height), enabled ? "ON" : "OFF",
                new GUIStyle(pillLabelStyle) { fontSize = 22, normal = { textColor = Color.white } });
            if (GUI.Button(toggle, GUIContent.none, GUIStyle.none)) CandyMusic.SetMusicEnabled(!enabled);
            DrawDrivingSettings(card);
            if (IconButton(new Rect(card.x + 65, card.y + 420, 410, 44), UiArt.GearIcon,
                "EDITAR CONTROLE", purplePillStyle, 22, LavenderText)) OpenControlEditor();
            if (IconButton(new Rect(card.x + 100, card.y + 478, 340, 42), UiArt.BackIcon,
                "VOLTAR", purplePillStyle, 24, LavenderText)) musicSettingsVisible = false;
        }

        // --- Fim de corrida ---

        void DrawFinishScreen()
        {
            DrawSweetBackdrop();

            var ordered = new List<Competitor>(finishCompetitors);
            ordered.Sort((a, b) => a.Position - b.Position);

            Competitor champion = finishWinner ?? ordered.Find(c => c.Position == 1);

            var humans = new List<Competitor>();
            foreach (var c in ordered) if (c.IsPlayer) humans.Add(c);
            bool coop = humans.Count > 1;
            bool won = champion != null && champion.IsPlayer;

            string winnerName = champion != null ? (champion.IsPlayer && !coop ? "Você" : champion.Name) : "Vencedor";

            float w = 540, h = Mathf.Max(450, 280 + (finishSeries != null ? finishSeries.Count : ordered.Count) * 28);
            var card = new Rect(480 - w / 2, 360 - h / 2, w, h);
            DrawSweetCard(card);

            var trophyRect = new Rect(card.x + card.width / 2 - 50, card.y + 10, 100, 105);
            GUI.DrawTexture(trophyRect, won ? UiArt.Trophy : UiArt.TrophyGray);

            var titleRect = new Rect(card.x, card.y + 118, card.width, 34);
            var titleS = new GUIStyle(titleStyle) { fontSize = 24 };
            GUI.Label(titleRect, won && !coop ? "VOCÊ VENCEU!" : $"{winnerName.ToUpperInvariant()} VENCEU!", titleS);

            string detail = coop
                ? CoopDetail(humans, champion, winnerName)
                : won
                    ? $"{RaceConfig.Laps} voltas, primeiro lugar. O troféu é seu!"
                    : $"Você terminou em {finishPlayerPosition}º. {winnerName} levou o troféu desta vez.";
            GUI.Label(new Rect(card.x + 24, card.y + 154, card.width - 48, 40), detail, detailStyle);

            float rowY = card.y + 204;
            if (finishSeries != null)
            {
                foreach (var s in finishSeries)
                {
                    DrawStandingRow(card, ref rowY, s.Position, s.Name, s.IsPlayer, s.Name == champion?.Name, $"{s.Points} pts");
                }
            }
            else
            {
                foreach (var c in ordered)
                {
                    string time = c.Finished ? FormatTime(c.FinishTime) : "—";
                    DrawStandingRow(card, ref rowY, c.Position, c.IsPlayer && !coop ? $"{c.Name} (você)" : c.Name, c.IsPlayer, c == champion, time);
                }
            }

            if (GUI.Button(new Rect(card.x + 100, card.yMax - 56, card.width - 200, 44), "CORRER DE NOVO", buttonStyle))
                onRestart?.Invoke();
        }

        /// <summary>Ouro, prata e bronze para o 1º, 2º e 3º; rosa para os demais.</summary>
        static Color MedalColor(int position)
        {
            if (position == 1) return new Color(1f, .84f, .3f, .95f);
            if (position == 2) return new Color(.8f, .84f, .9f, .95f);
            if (position == 3) return new Color(.85f, .55f, .3f, .9f);
            return new Color(1f, .62f, .8f, .8f);
        }

        void DrawStandingRow(Rect card, ref float y, int position, string name, bool isPlayer, bool isChampion, string trailing)
        {
            var rowRect = new Rect(card.x + 24, y, card.width - 48, 26);
            // A linha do jogador é que fica destacada, na cor da medalha do seu lugar.
            if (isPlayer) HudRoundRect(rowRect, MedalColor(position), rowRect.height / 2);
            var style = new GUIStyle(rowStyle);
            style.normal.textColor = isPlayer ? new Color(.68f, .18f, .43f) : new Color(.35f, .22f, .43f);
            GUI.Label(new Rect(rowRect.x + 6, rowRect.y + 3, 40, 20), $"{position}º", style);
            GUI.Label(new Rect(rowRect.x + 50, rowRect.y + 3, rowRect.width - 130, 20), isChampion ? $"Troféu - {name}" : name, style);
            var trailStyle = new GUIStyle(style) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(rowRect.xMax - 90, rowRect.y + 3, 84, 20), trailing, trailStyle);
            y += 28;
        }

        static string CoopDetail(List<Competitor> humans, Competitor champion, string winnerName)
        {
            humans.Sort((a, b) => a.Position - b.Position);
            var placings = new List<string>();
            foreach (var h in humans) placings.Add($"{h.Name} em {h.Position}º");
            string placingsText = string.Join(", ", placings);

            if (humans.Count < 2) return $"{placingsText}.";

            string duel = $"{humans[0].Name} levou a melhor sobre {humans[1].Name}";
            return champion != null && champion.IsPlayer
                ? $"{duel}. {placingsText}."
                : $"{duel}, mas {winnerName} levou o troféu. {placingsText}.";
        }

        static string FormatTime(float seconds)
        {
            if (!float.IsFinite(seconds)) return "--:--.--";
            int minutes = Mathf.FloorToInt(seconds / 60);
            float rest = seconds - minutes * 60;
            return $"{minutes}:{rest:00.00}";
        }
    }
}
