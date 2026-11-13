using CandyRush.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CandyRush.UI
{
    /// <summary>Fixed touch controls. Coordinates and saved layout are relative to the safe area.</summary>
    public sealed class TouchControls
    {
        private const string Pref = "CandyRush.Touch.";
        private const int NoPointer = int.MinValue;
        private int _stickPointer = NoPointer;
        private Vector2 _axis;
        private Vector2 _position;
        private float _size;
        private Rect _safe;
        private float _unit;
        private Vector2 _center;
        private float _radius;
        private bool _editing;
        private bool _pausePressed;
        private bool _wasFocused = true;
        private Texture2D _circle;
        private GUIStyle _label;

        public bool Editing => _editing;
        public bool Visible => Application.isMobilePlatform || Touchscreen.current != null || Application.isEditor;
        public KartInput Input { get; private set; }

        public KartInput Sample()
        {
            KartInput result = Input;
            result.PausePressed = _pausePressed;
            _pausePressed = false;
            return result;
        }

        public TouchControls()
        {
            _size = Mathf.Clamp(PlayerPrefs.GetFloat(Pref + "Size", 0.7f), 0.6f, 0.8f);
            _position = new Vector2(
                Mathf.Clamp(PlayerPrefs.GetFloat(Pref + "X", 0.16f), 0f, 0.45f),
                Mathf.Clamp01(PlayerPrefs.GetFloat(Pref + "Y", 0.76f)));
        }

        private Rect Button(int column, int row = 0)
        {
            return new Rect(_safe.xMax - (column + 1) * 116f * _unit,
                _safe.yMax - (row + 1) * 76f * _unit, 104f * _unit, 64f * _unit);
        }

        private Rect EditButton => new Rect(_safe.xMax - 190f * _unit, _safe.y + 12f * _unit, 178f * _unit, 48f * _unit);
        private Rect SizeButton(bool larger) => new Rect(_safe.xMax - (larger ? 90f : 170f) * _unit,
            _safe.y + 72f * _unit, 70f * _unit, 48f * _unit);

        public void Update()
        {
            Input = default;
            if (!Visible || !Application.isFocused)
            {
                _stickPointer = NoPointer;
                _axis = Vector2.zero;
                _pausePressed = false;
                _wasFocused = false;
                return;
            }
            Rect area = Screen.safeArea;
            Rect safe = new Rect(area.x, Screen.height - area.yMax, area.width, area.height);
            if (safe != _safe || !_wasFocused) _stickPointer = NoPointer;
            _wasFocused = true;
            _safe = safe;
            _unit = Mathf.Min(_safe.width / 960f, _safe.height / 540f);
            Layout();
            bool stickHeld = false;
            KartInput input = default;
            var screen = Touchscreen.current;
            bool hasTouch = false;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    hasTouch = true;
                    Process(touch.touchId.ReadValue(), touch.position.ReadValue(), touch.press.wasPressedThisFrame,
                        ref input, ref stickHeld);
                }
            }
            // Mouse preview is available in the editor without duplicating mobile touches.
            if (!hasTouch && Application.isEditor && Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Process(-1, Mouse.current.position.ReadValue(), Mouse.current.leftButton.wasPressedThisFrame,
                    ref input, ref stickHeld);
            }
            if (!stickHeld)
            {
                if (_editing && _stickPointer != NoPointer) Save();
                _stickPointer = NoPointer;
                _axis = Vector2.zero;
            }
            input.Steer = _editing ? 0f : Shape(_axis.x);
            input.PausePressed = _pausePressed;
            Input = _editing ? default : input;
        }

        private void Layout()
        {
            _radius = 120f * _size * _unit;
            float margin = _radius + 12f * _unit;
            _center = new Vector2(
                Mathf.Clamp(_safe.x + _position.x * _safe.width, _safe.x + margin, _safe.center.x - margin),
                Mathf.Clamp(_safe.y + _position.y * _safe.height, _safe.y + margin, _safe.yMax - margin));
        }

        private void Process(int id, Vector2 screenPosition, bool began, ref KartInput input, ref bool stickHeld)
        {
            Vector2 p = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            if (began && EditButton.Contains(p))
            {
                _editing = !_editing;
                _stickPointer = NoPointer;
                _axis = Vector2.zero;
                Save();
                return;
            }
            if (_editing)
            {
                if (began && (SizeButton(false).Contains(p) || SizeButton(true).Contains(p)))
                {
                    _size = Mathf.Clamp(_size + (SizeButton(true).Contains(p) ? 0.05f : -0.05f), 0.6f, 0.8f);
                    Layout();
                    Save();
                    return;
                }
            }
            else
            {
                input.Throttle = Mathf.Max(input.Throttle, Button(0).Contains(p) ? 1f : 0f);
                input.Brake = Mathf.Max(input.Brake, Button(1).Contains(p) ? 1f : 0f);
                input.Handbrake |= Button(0, 1).Contains(p);
                if (began && Button(1, 1).Contains(p)) _pausePressed = true;
            }
            if (began && _stickPointer == NoPointer && Vector2.Distance(p, _center) <= _radius * 1.15f)
                _stickPointer = id;
            if (_stickPointer != id) return;
            stickHeld = true;
            if (_editing)
            {
                _position = new Vector2(Mathf.Clamp((p.x - _safe.x) / _safe.width, 0f, 0.45f),
                    Mathf.Clamp01((p.y - _safe.y) / _safe.height));
                Layout();
            }
            else
            {
                // Steering uses horizontal travel independently of vertical thumb drift.
                _axis = (p - _center) / (_radius * 0.7f);
                _axis.x = Mathf.Clamp(_axis.x, -1f, 1f);
                _axis.y = Mathf.Clamp(_axis.y, -1f, 1f);
            }
        }

        private static float Shape(float value)
        {
            const float deadzone = 0.05f;
            return Mathf.Sign(value) * Mathf.Clamp01((Mathf.Abs(value) - deadzone) / (1f - deadzone));
        }

        private void Save()
        {
            PlayerPrefs.SetFloat(Pref + "Size", _size);
            PlayerPrefs.SetFloat(Pref + "X", _position.x);
            PlayerPrefs.SetFloat(Pref + "Y", _position.y);
            PlayerPrefs.Save();
        }

        public void Draw()
        {
            if (!Visible || _unit <= 0f) return;
            if (_label == null)
                _label = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _label.fontSize = Mathf.Max(12, Mathf.RoundToInt(19f * _unit));
            GUI.Box(EditButton, _editing ? "Concluir HUD" : "Editar HUD", _label);
            if (_editing)
            {
                GUI.Box(SizeButton(false), "−", _label);
                GUI.Box(SizeButton(true), "+", _label);
                GUI.Box(new Rect(_safe.center.x - 150f * _unit, _safe.y + 130f * _unit, 300f * _unit, 64f * _unit),
                    $"Joystick fixo: {_size:P0}\nArraste a base para posicionar", _label);
            }
            else
            {
                GUI.Box(Button(0), "Acelerar", _label);
                GUI.Box(Button(1), "Frear / Ré", _label);
                GUI.Box(Button(0, 1), "Drift", _label);
                GUI.Box(Button(1, 1), "Pausa", _label);
            }
            EnsureCircle();
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.65f, 0.82f, 0.55f);
            GUI.DrawTexture(new Rect(_center.x - _radius, _center.y - _radius, _radius * 2f, _radius * 2f), _circle);
            Vector2 knob = _center + Vector2.ClampMagnitude(_axis, 1f) * _radius * 0.7f;
            float r = _radius * 0.36f;
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(knob.x - r, knob.y - r, r * 2f, r * 2f), _circle);
            GUI.color = previous;
        }

        private void EnsureCircle()
        {
            if (_circle != null) return;
            _circle = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            _circle.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    pixels[y * 64 + x] = new Color(1f, 1f, 1f,
                        Mathf.Clamp01(32f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f))));
            _circle.SetPixels(pixels);
            _circle.Apply();
        }

        public void Dispose()
        {
            if (_circle != null) Object.Destroy(_circle);
        }
    }
}
