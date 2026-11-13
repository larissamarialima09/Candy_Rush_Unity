using UnityEngine;

namespace CandyRush.Integrated
{
    public sealed partial class GameUI
    {
        CircuitPath hudCircuit;
        RaceDirector raceDirector;
        Texture2D minimap;
        Vector2 mapMin, mapMax;
        Vector2 joystickOffset;
        int? joystickPointer;
        readonly System.Collections.Generic.Dictionary<int, int> heldButtons = new System.Collections.Generic.Dictionary<int, int>();

        /// <summary>Quanto o joystick precisa ser arrastado (fração da largura) para o máximo. Maior que antes, para o comando ser proporcional.</summary>
        const float StickTravel = .2f;
        /// <summary>Tamanho do botão central do joystick, em fração da largura da base.</summary>
        const float StickKnob = .56f;

        public void SetCircuit(CircuitPath circuit, RaceDirector director)
        {
            hudCircuit = circuit;
            raceDirector = director;
            if (minimap != null) Destroy(minimap);
            mapMin = new Vector2(float.MaxValue, float.MaxValue);
            mapMax = new Vector2(float.MinValue, float.MinValue);
            foreach (var sample in circuit.Samples)
            {
                var p = new Vector2(sample.Position.x, sample.Position.z);
                mapMin = Vector2.Min(mapMin, p);
                mapMax = Vector2.Max(mapMax, p);
            }
            minimap = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            minimap.name = "Mapa da pista";
            var pixels = new Color[192 * 192];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
            minimap.SetPixels(pixels);
            for (int i = 0; i < circuit.Count; i++)
            {
                Vector2 a = MapPoint(circuit.Samples[i].Position) * 191;
                Vector2 b = MapPoint(circuit.Samples[(i + 1) % circuit.Count].Position) * 191;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
                for (int j = 0; j <= steps; j++)
                {
                    Vector2 p = Vector2.Lerp(a, b, j / (float)steps);
                    for (int x = -3; x <= 3; x++) for (int y = -3; y <= 3; y++)
                    {
                        int px = Mathf.RoundToInt(p.x) + x, py = Mathf.RoundToInt(p.y) + y;
                        if (px >= 0 && py >= 0 && px < 192 && py < 192)
                            minimap.SetPixel(px, py, new Color(1f, .42f, .74f));
                    }
                }
            }
            minimap.Apply();
        }

        Vector2 MapPoint(Vector3 position)
        {
            Vector2 extent = mapMax - mapMin;
            float size = Mathf.Max(1, Mathf.Max(extent.x, extent.y));
            return Vector2.one * .5f + (new Vector2(position.x, position.z) - (mapMin + mapMax) * .5f) / size * .8f;
        }

        int RacePosition(Kart kart)
        {
            if (raceDirector != null)
                foreach (var competitor in raceDirector.Competitors)
                    if (competitor.Kart == kart) return competitor.Position;
            return 1;
        }

        /// <summary>Quanto da volta atual o kart já percorreu, de 0 a 1.</summary>
        float LapFraction(Kart kart)
        {
            if (raceDirector == null || hudCircuit == null || hudCircuit.TotalLength <= 0) return 0;
            foreach (var competitor in raceDirector.Competitors)
                if (competitor.Kart == kart)
                    return Mathf.Clamp01((competitor.Progress - competitor.Lap * hudCircuit.TotalLength) / hudCircuit.TotalLength);
            return 0;
        }

        static Rect Grow(Rect r, float margin) => new Rect(r.x - margin, r.y - margin, r.width + margin * 2, r.height + margin * 2);

        static void DrawIcon(Rect rect, Texture2D icon, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, icon);
            GUI.color = previous;
        }

        void DrawMinimap(HudEntry entry)
        {
            if (minimap == null) return;
            float s = Controls.Scale;
            float size = 100 * s;
            // Abaixo do botão de pausa, no canto superior esquerdo.
            var rect = new Rect(ControlArea.x + 16 * s, ControlArea.y + 90 * s, size, size);
            HudRoundRect(Grow(rect, 6), CardNavy, 22);
            GUI.DrawTexture(rect, minimap);
            if (raceDirector != null) foreach (var competitor in raceDirector.Competitors)
            {
                Vector2 p = MapPoint(competitor.Kart.Body.Position);
                float dot = competitor.Kart == entry.Kart ? 12 : 6;
                var marker = new Rect(rect.x + p.x * rect.width - dot / 2,
                    rect.y + (1 - p.y) * rect.height - dot / 2, dot, dot);
                HudRoundRect(marker, competitor.Kart == entry.Kart ? Color.white : new Color(.85f, .8f, 1f, .9f), dot / 2);
                if (competitor.Kart == entry.Kart)
                    HudRoundRect(new Rect(marker.x + 3, marker.y + 3, 6, 6), HudPink, 3);
            }
        }

        /// <summary>Barra de carga do turbo no rodapé, separada dos pedais.</summary>
        static Rect DriftBarRect(Rect vp, float scale)
        {
            return MobileControlLayout.Create(vp).Meter;
        }
        Rect ControlArea
        {
            get
            {
                if (hudEntries.Count == 0) return new Rect(0, 0, Screen.width, Screen.height);
                var v = hudEntries[0].Viewport;
                var viewport = new Rect(v.X, v.Y, v.Width, v.Height);
                Rect safe = Screen.safeArea;
                if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
                safe.y = Screen.height - safe.yMax;
                return Rect.MinMaxRect(Mathf.Max(viewport.xMin, safe.xMin), Mathf.Max(viewport.yMin, safe.yMin),
                    Mathf.Min(viewport.xMax, safe.xMax), Mathf.Min(viewport.yMax, safe.yMax));
            }
        }

        Rect ControlRect(float x, float y, float width, float height)
        {
            Rect area = ControlArea;
            float scale = Mathf.Min(area.width / 960, area.height / 540);
            return new Rect(area.x + (area.width - 960 * scale) / 2 + x * scale,
                area.yMax - (540 - y) * scale, width * scale, height * scale);
        }

        MobileControlLayout Controls => MobileControlLayout.Create(ControlArea);
        Rect JoystickRect => CustomJoystickRect();
        Rect BrakeRect => Controls.Brake;
        Rect ThrottleRect => Controls.Throttle;
        Rect DriftRect => Controls.Drift;
        Rect TurboRect => Controls.Turbo;
        Rect PauseRect => Controls.Pause;
        /// <summary>A barra só libera o turbo quando está cheia (carga no nível 3), derrapando ou não.</summary>
        bool DriftBarFull
        {
            get
            {
                if (hudEntries.Count == 0) return false;
                return hudEntries[0].Kart.Turbo.Ready;
            }
        }

        void UpdateScreenControls()
        {
            UpdateHudEditor();
            TouchState.Steer = 0;
            TouchState.Brake = TouchState.Handbrake = false;
            TouchState.Acceleration = 0;
            TouchState.DriftBoost = false;
            TouchState.DriftHeld=TouchState.TurboPressed=false;
            if (editingHud || state == null || !state.IsRacing || finishVisible || !Application.isFocused)
            {
                joystickPointer = null;
                joystickOffset = Vector2.zero;
                heldButtons.Clear();
                return;
            }
            bool joystickHeld = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    heldButtons.Remove(touch.fingerId);
                    continue;
                }
                ReadControlPointer(touch.fingerId, new Vector2(touch.position.x, Screen.height - touch.position.y), ref joystickHeld, touch.phase == TouchPhase.Began);
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0))
                ReadControlPointer(-999, new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y), ref joystickHeld, Input.GetMouseButtonDown(0));
            if (!Input.GetMouseButton(0)) heldButtons.Remove(-999);
            if (!joystickHeld) { joystickPointer = null; joystickOffset = Vector2.zero; }
        }

        void ReadControlPointer(int id, Vector2 position, ref bool joystickHeld, bool began)
        {
            Rect joystick = JoystickRect;
            if (joystickPointer == id || (!joystickPointer.HasValue && began && joystick.Contains(position)))
            {
                joystickPointer = id;
                joystickHeld = true;
                Vector2 raw = (position - joystick.center) / (joystick.width * StickTravel);
                joystickOffset = MobileControlLayout.ShapeStick(raw);
                // Só direção; deslocamento vertical não aciona pedais nem reduz a curva.
                // Positivo = esquerda, como no teclado; na tela, x negativo é a esquerda.
                TouchState.Steer = -MobileControlLayout.ShapeStick(new Vector2(raw.x, 0)).x;
                return;
            }
            if (began)
            {
                if (ThrottleRect.Contains(position)) heldButtons[id] = 1;
                else if (BrakeRect.Contains(position)) heldButtons[id] = 2;
                else if (DriftRect.Contains(position)) heldButtons[id] = 3;
                else if (TurboRect.Contains(position)) heldButtons[id] = 4;
            }
            if (!heldButtons.TryGetValue(id, out int button)) return;
            if (button == 1) TouchState.Acceleration = 1;
            TouchState.Brake |= button == 2;
            if (button == 3)
            {
                // O drift é mantido pelo próprio dedo; turbo usa outro botão e outro toque.
                TouchState.DriftHeld = true;
            }
            if(button==4 && began && DriftBarFull) TouchState.TurboPressed=true;
        }

        /// <summary>Desenha a base do botão, com realce quando pressionado ou com o ponteiro em cima.</summary>
        void DrawPad(Rect rect, Texture2D texture, bool pressed)
        {
            var previous = GUI.color;
            bool hover = rect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            GUI.color = pressed ? new Color(1, .7f, .9f) : hover ? new Color(1, .9f, 1) : Color.white;
            GUI.DrawTexture(rect, texture);
            GUI.color = previous;
        }

        /// <summary>Etiqueta pequena em pílula escura, embaixo de um botão.</summary>
        void DrawLabelPill(Rect rect, string text)
        {
            HudRoundRect(rect, CardNavy, rect.height / 2);
            GUI.Label(rect, text, new GUIStyle(pillLabelStyle) { fontSize = Mathf.RoundToInt(rect.height * .6f), normal = { textColor = Color.white } });
        }

        /// <summary>Joystick quase transparente: anel fino, quatro setas no anel e o botão central rosa.</summary>
        void DrawJoystick()
        {
            Rect stick = JoystickRect;
            float unit = stick.width / 160f;
            float radius = stick.width / 2;
            HudRoundRect(stick, new Color(1, 1, 1, .3f), radius);
            HudRoundRect(Grow(stick, -3 * unit), new Color(1, 1, 1, .1f), radius - 3 * unit);

            var hint = new Color(1, 1, 1, .75f);
            float size = 16 * unit;
            DrawIcon(new Rect(stick.center.x - size / 2, stick.y + 12 * unit, size, size), UiArt.HintUp, hint);
            DrawIcon(new Rect(stick.center.x - size / 2, stick.yMax - 12 * unit - size, size, size), UiArt.HintDown, hint);
            DrawIcon(new Rect(stick.x + 12 * unit, stick.center.y - size / 2, size, size), UiArt.HintLeft, hint);
            DrawIcon(new Rect(stick.xMax - 12 * unit - size, stick.center.y - size / 2, size, size), UiArt.HintRight, hint);

            bool held = joystickPointer.HasValue;
            float knob = stick.width * StickKnob;
            Vector2 center = stick.center + joystickOffset * stick.width * StickTravel;
            var knobRect = new Rect(center.x - knob / 2, center.y - knob / 2, knob, knob);
            HudRoundRect(knobRect, held ? new Color(1, .36f, .72f, .9f) : new Color(1, .36f, .72f, .65f), knob / 2);
            HudRoundRect(new Rect(knobRect.x + knob * .2f, knobRect.y + knob * .12f, knob * .4f, knob * .22f),
                new Color(1, 1, 1, .45f), knob * .11f);
        }

        void DrawBrakeButton()
        {
            Rect brake = BrakeRect;
            Rect face = DrawCandyControl(brake, new Color(.88f,.34f,.62f), TouchState.Brake);
            float bar = face.width * .15f;
            HudRoundRect(new Rect(face.center.x-bar*1.5f,face.center.y-bar*1.5f,bar,bar*3),Color.white,bar*.3f);
            HudRoundRect(new Rect(face.center.x+bar*.5f,face.center.y-bar*1.5f,bar,bar*3),Color.white,bar*.3f);
            DrawControlCaption(brake,"FREAR");
        }

        void DrawThrottleButton()
        {
            Rect throttle = ThrottleRect;
            Rect face=DrawCandyControl(throttle,HudPink,TouchState.Acceleration>.5f);
            float size=face.width*.4f;
            DrawIcon(new Rect(face.center.x-size/2,face.center.y-size*.8f,size,size),UiArt.HintUp,Color.white);
            DrawIcon(new Rect(face.center.x-size/2,face.center.y-size*.15f,size,size),UiArt.HintUp,Color.white);
            DrawControlCaption(throttle,"ACELERAR");
        }

        /// <summary>
        /// Botão DRIFT compacto: pneu e faíscas; segurar e soltar controla a derrapagem.
        /// </summary>
        void DrawDriftButton()
        {
            Rect r = DriftRect;
            Rect face=DrawCandyControl(r,new Color(.65f,.35f,.86f),TouchState.DriftHeld);
            float tire=face.width*.57f;
            var wheel=new Rect(face.center.x-tire/2,face.center.y-tire/2,tire,tire);
            HudRoundRect(wheel,new Color(.23f,.15f,.34f),tire/2);
            HudRoundRect(Grow(wheel,-tire*.19f),new Color(1,.9f,.97f),tire/2);
            HudRoundRect(Grow(wheel,-tire*.31f),new Color(.52f,.3f,.68f),tire/2);
            float spark=face.width*.2f;
            DrawIcon(new Rect(wheel.xMax-spark*.2f,wheel.y-spark*.25f,spark,spark),UiArt.BoltIcon,new Color(1,.91f,.55f));
            DrawIcon(new Rect(wheel.x-spark*.7f,wheel.yMax-spark*.5f,spark,spark),UiArt.BoltIcon,new Color(1,.91f,.55f));
            DrawControlCaption(r,"DRIFT");
        }

        void DrawPauseButton()
        {
            Rect pause = PauseRect;
            HudRoundRect(pause, CardNavy, pause.width / 2);
            float bw = pause.width * .14f, bh = pause.height * .42f;
            HudRoundRect(new Rect(pause.center.x - bw * 1.4f, pause.center.y - bh / 2, bw, bh), Color.white, bw / 2);
            HudRoundRect(new Rect(pause.center.x + bw * .4f, pause.center.y - bh / 2, bw, bh), Color.white, bw / 2);
            if (!helpVisible && GUI.Button(pause, GUIContent.none, GUIStyle.none))
                state.TogglePause();
        }

        void DrawSettingsButton()
        {
            Rect r = PauseRect;
            r.x += r.width + 12 * Controls.Scale;
            HudRoundRect(r, CardNavy, r.width / 2);
            DrawIcon(Grow(r, -12 * Controls.Scale), UiArt.GearIcon, Color.white);
            if (!helpVisible && GUI.Button(r, GUIContent.none, GUIStyle.none)) OpenMusicSettings();
        }

        void DrawScreenControls()
        {
            DrawJoystick();
            DrawBrakeButton();
            DrawThrottleButton();
            DrawDriftButton();
            DrawTurboButton();
            DrawPauseButton();
            DrawSettingsButton();
        }

        /// <summary>Passo atual do guia de ajuda. Cada ENTENDI avança um controle; depois vem a pergunta final.</summary>
        int helpStep;

        static readonly string[] HelpTexts =
        {
            "JOYSTICK: arraste para virar e segure ACELERAR para andar. A aceleração automática é opcional nas configurações.",
            "FREAR: segure para reduzir a velocidade. Depois de parar, continue segurando para dar ré e manobrar. Siga a corrida no sentido correto da pista.",
            "ACELERAR: no modo manual, segure para andar. No automático, basta virar e frear.",
            "TURBO: a rampa e os drifts carregam a barra. Aperte o botão TURBO quando estiver cheia; custa 30% de combustível.",
            "DRIFT: segure enquanto vira e solte para um impulso pequeno. Nível azul custa 15%; nível rosa/roxo custa 20%.",
        };

        void OpenHelp()
        {
            helpVisible = true;
            helpStep = 0;
        }

        /// <summary>Área do jogador 1, onde ficam o minimapa, o card e a barra de drift.</summary>
        Rect HelpViewport()
        {
            if (hudEntries.Count == 0) return new Rect(0, 0, Screen.width, Screen.height);
            var v = hudEntries[0].Viewport;
            return new Rect(v.X, v.Y, v.Width, v.Height);
        }

        /// <summary>Redesenha, por cima do escurecimento, o controle que o passo atual explica: fica mais claro que o resto.</summary>
        void DrawHelpHighlight(int step)
        {
            float scale = Controls.Scale;
            switch (step)
            {
                case 0: DrawJoystick(); break;
                case 1: DrawBrakeButton(); break;
                case 2: DrawThrottleButton(); break;
                case 3:
                    if (hudEntries.Count > 0) DrawDriftMeter(HelpViewport(), scale, hudEntries[0].Kart);
                    break;
                default: DrawDriftButton(); break;
            }
        }

        /// <summary>Escurece a tela inteira, de leve, para o controle em destaque se sobressair.</summary>
        static void DrawHelpDim()
        {
            HudRoundRect(new Rect(0, 0, Screen.width, Screen.height), new Color(.08f, .04f, .1f, .35f), 0);
        }

        void DrawControlHelp()
        {
            DrawHelpDim();
            DrawHelpHighlight(helpStep);
            Rect card = ControlRect(240, 16, 460, 160);
            float s = card.height / 160f;
            HudRoundRect(card, new Color(1, .94f, .98f, .96f), 22 * s);
            var text = new GUIStyle(detailStyle) { fontSize = Mathf.RoundToInt(17 * s), wordWrap = true };
            GUI.Label(new Rect(card.x + 18 * s, card.y + 12 * s, card.width - 36 * s, card.height - 84 * s), HelpTexts[helpStep], text);
            float bw = 200 * s, bh = 52 * s;
            if (IconButton(new Rect(card.center.x - bw / 2, card.yMax - bh - 14 * s, bw, bh), UiArt.PlayIcon, "ENTENDI", pinkPillStyle, Mathf.RoundToInt(18 * s)))
            {
                helpStep++;
                // Depois do último controle, volta para a tela de pausa.
                if (helpStep >= HelpTexts.Length) helpVisible = false;
            }
        }


        void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                TouchState.Steer = 0;
                TouchState.Brake = TouchState.Handbrake = false;
                TouchState.Acceleration = 0;
                TouchState.DriftBoost = false;
                TouchState.DriftHeld=TouchState.TurboPressed=false;
                joystickPointer = null;
                joystickOffset = Vector2.zero;
                heldButtons.Clear();
            }
        }

        void OnDestroy()
        {
            if (minimap != null) Destroy(minimap);
            TouchState.Steer = 0;
            TouchState.Brake = TouchState.Handbrake = false;
            TouchState.Acceleration = 0;
            TouchState.DriftBoost = false;
            TouchState.DriftHeld=TouchState.TurboPressed=false;
        }
    }
}
