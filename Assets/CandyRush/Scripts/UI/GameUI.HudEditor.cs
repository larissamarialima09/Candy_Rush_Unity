using UnityEngine;

namespace CandyRush.Integrated
{
    public sealed partial class GameUI
    {
        bool editingHud;
        bool hudLayoutLoaded;
        float hudStickSize = .7f;
        Vector2 hudStickPosition;
        bool hudStickMoved;
        int? hudDragPointer;
        Vector2 hudDragOffset;
        const string HudPref = "CandyRush.Integrated.Hud.";

        void LoadHudLayout()
        {
            if (hudLayoutLoaded) return;
            hudLayoutLoaded = true;
            hudStickSize = Mathf.Clamp(PlayerPrefs.GetFloat(HudPref + "Size", .7f), .6f, .8f);
            hudStickMoved = PlayerPrefs.GetInt(HudPref + "Moved", 0) == 1;
            hudStickPosition = new Vector2(PlayerPrefs.GetFloat(HudPref + "X", .13f), PlayerPrefs.GetFloat(HudPref + "Y", .77f));
        }

        Rect CustomJoystickRect()
        {
            LoadHudLayout();
            Rect area = ControlArea;
            float size = Controls.Joystick.width * hudStickSize;
            Vector2 center = hudStickMoved
                ? area.position + Vector2.Scale(hudStickPosition, area.size)
                : Controls.Joystick.center;
            center.x = Mathf.Clamp(center.x, area.xMin + size / 2, area.center.x - size / 2);
            center.y = Mathf.Clamp(center.y, area.yMin + size / 2, area.yMax - size / 2);
            return new Rect(center.x - size / 2, center.y - size / 2, size, size);
        }

        void OpenControlEditor()
        {
            editingHud = true;
            musicSettingsVisible = false;
            hudDragPointer = null;
            joystickPointer = null;
            joystickOffset = Vector2.zero;
            heldButtons.Clear();
            TouchState.Steer = TouchState.Acceleration = 0;
            TouchState.Brake = TouchState.Handbrake = TouchState.DriftBoost = false;
            TouchState.DriftHeld=TouchState.TurboPressed=false;
            state.Set(GameStateName.Paused);
        }

        void UpdateHudEditor()
        {
            if (!editingHud) return;
            if (state == null || state.Name != GameStateName.Paused)
            {
                SaveHudLayout();
                editingHud = false;
                hudDragPointer = null;
                return;
            }
            if (!Application.isFocused) { hudDragPointer = null; return; }
            bool held = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                DragHudStick(t.fingerId, new Vector2(t.position.x, Screen.height - t.position.y), t.phase == TouchPhase.Began, ref held);
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0))
                DragHudStick(-999, new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y), Input.GetMouseButtonDown(0), ref held);
            if (!held && hudDragPointer.HasValue) { hudDragPointer = null; SaveHudLayout(); }
        }

        void DragHudStick(int id, Vector2 p, bool began, ref bool held)
        {
            if (began && !hudDragPointer.HasValue && JoystickRect.Contains(p))
            {
                hudDragPointer = id;
                hudDragOffset = JoystickRect.center - p;
            }
            if (hudDragPointer != id) return;
            held = true;
            Rect area = ControlArea;
            Vector2 center = p + hudDragOffset;
            hudStickPosition = new Vector2(Mathf.Clamp01((center.x - area.x) / area.width), Mathf.Clamp01((center.y - area.y) / area.height));
            hudStickMoved = true;
            center = JoystickRect.center;
            hudStickPosition = new Vector2((center.x - area.x) / area.width, (center.y - area.y) / area.height);
        }

        void SaveHudLayout()
        {
            PlayerPrefs.SetFloat(HudPref + "Size", hudStickSize);
            PlayerPrefs.SetFloat(HudPref + "X", hudStickPosition.x);
            PlayerPrefs.SetFloat(HudPref + "Y", hudStickPosition.y);
            PlayerPrefs.SetInt(HudPref + "Moved", hudStickMoved ? 1 : 0);
            PlayerPrefs.Save();
        }

        void DrawHudEditor()
        {
            DrawBrakeButton();
            DrawThrottleButton();
            DrawDriftButton();
            DrawTurboButton();
            DrawJoystick();
            float s = Controls.Scale;
            Rect card = new Rect(ControlArea.center.x - 230 * s, ControlArea.y + 20 * s, 460 * s, 220 * s);
            HudRoundRect(card, new Color(1, .94f, .98f, .98f), 22 * s);
            GUI.Label(new Rect(card.x + 16 * s, card.y + 12 * s, card.width - 32 * s, 34 * s),
                "Editar controle", new GUIStyle(titleStyle) { fontSize = Mathf.RoundToInt(24 * s) });
            GUI.Label(new Rect(card.x + 20 * s, card.y + 52 * s, card.width - 40 * s, 86 * s),
                "Deixe o controle confortável para seu polegar.\nUse − para diminuir e + para aumentar.\nArraste o controle até onde seu dedo descansa.",
                new GUIStyle(detailStyle) { fontSize = Mathf.RoundToInt(17 * s), wordWrap = true });
            if (ControlEditorButton(new Rect(card.x + 30 * s, card.y + 152 * s, 48 * s, 48 * s), "−", 26 * s))
            { hudStickSize = Mathf.Max(.6f, hudStickSize - .05f); SaveHudLayout(); }
            if (ControlEditorButton(new Rect(card.x + 94 * s, card.y + 152 * s, 48 * s, 48 * s), "+", 26 * s))
            { hudStickSize = Mathf.Min(.8f, hudStickSize + .05f); SaveHudLayout(); }
            if (ControlEditorButton(new Rect(card.x + 166 * s, card.y + 152 * s, 264 * s, 48 * s), "SALVAR E VOLTAR", 18 * s))
            {
                SaveHudLayout();
                editingHud = false;
                hudDragPointer = null;
                musicSettingsVisible = true;
            }
        }

        bool ControlEditorButton(Rect rect, string text, float fontSize)
        {
            bool hover = rect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            HudRoundRect(rect, hover ? new Color(1f, .35f, .65f) : HudPink, rect.height / 2);
            var style = new GUIStyle(pillLabelStyle) { fontSize = Mathf.RoundToInt(fontSize) };
            return GUI.Button(rect, text, style);
        }
    }
}
