using CandyRush.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CandyRush.Core
{
    /// <summary>
    /// Lê teclado e controle e devolve um KartInput (convertido de src/core/input.ts).
    ///
    /// Dispositivos são combinados pegando o MAIOR valor de cada eixo, não
    /// somando: assim segurar o acelerador no teclado e no gatilho ao mesmo
    /// tempo não dá vantagem, e dá para trocar de dispositivo no meio da corrida.
    ///
    /// Chamado uma vez por PASSO DE FÍSICA, antes de simular.
    /// </summary>
    public sealed class KartInputSampler
    {
        private bool _respawnLatched;
        private bool _pauseLatched;

        public KartInput Sample()
        {
            var input = new KartInput();
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            // --- Teclado: WASD ou setas ---
            float keyThrottle = 0f, keyBrake = 0f, keySteer = 0f;
            bool keyHandbrake = false, keyRespawn = false, keyPause = false;
            if (keyboard != null)
            {
                bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
                bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
                keySteer = (right ? 1f : 0f) - (left ? 1f : 0f);
                keyThrottle = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f;
                keyBrake = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f;
                keyHandbrake = keyboard.spaceKey.isPressed;
                keyRespawn = keyboard.rKey.isPressed;
                keyPause = keyboard.escapeKey.isPressed || keyboard.pKey.isPressed;
            }

            // --- Controle ---
            float padThrottle = 0f, padBrake = 0f, padSteer = 0f;
            bool padHandbrake = false, padRespawn = false, padPause = false;
            if (gamepad != null)
            {
                padSteer = ShapeAxis(gamepad.leftStick.x.ReadValue());
                padThrottle = Mathf.Max(Trigger(gamepad.rightTrigger.ReadValue()), gamepad.buttonSouth.isPressed ? 1f : 0f);
                padBrake = Mathf.Max(Trigger(gamepad.leftTrigger.ReadValue()), gamepad.buttonEast.isPressed ? 1f : 0f);
                padHandbrake = gamepad.rightShoulder.isPressed || gamepad.buttonWest.isPressed;
                padRespawn = gamepad.buttonNorth.isPressed;
                padPause = gamepad.startButton.isPressed;
            }

            input.Throttle = Mathf.Max(keyThrottle, padThrottle);
            input.Brake = Mathf.Max(keyBrake, padBrake);
            input.Steer = LargestMagnitude(keySteer, padSteer);
            input.Handbrake = keyHandbrake || padHandbrake;

            bool respawnDown = keyRespawn || padRespawn;
            input.RespawnPressed = respawnDown && !_respawnLatched;
            _respawnLatched = respawnDown;

            bool pauseDown = keyPause || padPause;
            input.PausePressed = pauseDown && !_pauseLatched;
            _pauseLatched = pauseDown;

            return input;
        }

        /// <summary>Zona morta e curva no analógico: mais precisão perto do centro.</summary>
        private static float ShapeAxis(float value)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude <= ControlsConfig.Gamepad.Deadzone) return 0f;
            float rescaled = (magnitude - ControlsConfig.Gamepad.Deadzone) / (1f - ControlsConfig.Gamepad.Deadzone);
            float shaped = Mathf.Pow(Mathf.Clamp01(rescaled), ControlsConfig.Gamepad.SteerCurve);
            return Mathf.Sign(value) * shaped;
        }

        private static float Trigger(float value) => value >= ControlsConfig.Gamepad.TriggerThreshold ? value : 0f;

        /// <summary>Entre dois comandos de direção vale o mais forte, preservando o sinal.</summary>
        private static float LargestMagnitude(float a, float b)
        {
            if (Mathf.Approximately(Mathf.Abs(a), Mathf.Abs(b))) return Mathf.Approximately(a + b, 0f) ? 0f : a;
            return Mathf.Abs(a) > Mathf.Abs(b) ? a : b;
        }
    }
}
