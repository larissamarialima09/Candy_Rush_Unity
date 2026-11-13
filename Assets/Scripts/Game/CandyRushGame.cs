using CandyRush.Config;
using CandyRush.Core;
using CandyRush.UI;
using UnityEngine;

namespace CandyRush.Game
{
    /// <summary>
    /// Componente principal: coloque num GameObject da cena (a cena
    /// Assets/Scenes/CandyRush.unity já vem com ele).
    ///
    /// Roda o loop de PASSO FIXO com acumulador, como o gameLoop.ts da web:
    /// a física sempre avança em blocos de 1/60 s, e o visual interpola entre
    /// os dois últimos passos. Não usa o FixedUpdate da Unity para garantir o
    /// mesmo limite de passos por quadro e a mesma ordem de amostragem do input.
    /// </summary>
    public sealed class CandyRushGame : MonoBehaviour
    {
        [Tooltip("Câmera usada pelo jogo. Se vazio, usa a Main Camera ou cria uma.")]
        [SerializeField] private Camera gameCamera;

        [Tooltip("Liga o piloto automático (útil para ver a pista sem jogar).")]
        [SerializeField] private bool autopilot;

        private CandyRushWorld _world;
        private readonly KartInputSampler _input = new KartInputSampler();
        private Autopilot _autopilot;
        private RaceHud _hud;
        private TouchControls _touch;

        private float _accumulator;
        private bool _paused;

        public CandyRushWorld World => _world;
        public bool Paused => _paused;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null)
            {
                gameCamera = new GameObject("Main Camera").AddComponent<Camera>();
                gameCamera.tag = "MainCamera";
            }

            _world = new CandyRushWorld(gameCamera, transform);
            _autopilot = new Autopilot(_world.Path);
            _hud = new RaceHud();
            _touch = new TouchControls();
        }

        private void Update()
        {
            _touch.Update();
            float frameDt = Mathf.Min(Time.unscaledDeltaTime, PhysicsConfig.MaxFrameTime);
            _accumulator += frameDt;

            float dt = PhysicsConfig.FixedTimeStep;
            int steps = 0;
            while (_accumulator >= dt && steps < PhysicsConfig.MaxStepsPerFrame)
            {
                FixedStep(dt);
                _accumulator -= dt;
                steps++;
            }
            // Estourou o orçamento: descarta o resto (melhor câmera lenta do que travar).
            if (steps == PhysicsConfig.MaxStepsPerFrame) _accumulator = 0f;

            _world.Render(_accumulator / dt);
            if (!_paused) _world.Animate(frameDt);
        }

        /// <summary>Um passo fixo: uma amostra de input por passo, nunca por quadro.</summary>
        private void FixedStep(float dt)
        {
            KartInput input = _input.Sample();
            KartInput touch = _touch.Sample();
            input.Throttle = Mathf.Max(input.Throttle, touch.Throttle);
            input.Brake = Mathf.Max(input.Brake, touch.Brake);
            if (Mathf.Abs(touch.Steer) > Mathf.Abs(input.Steer)) input.Steer = touch.Steer;
            input.Handbrake |= touch.Handbrake;
            input.PausePressed |= touch.PausePressed;
            if (input.PausePressed) _paused = !_paused;
            if (_paused || _touch.Editing) return;

            if (autopilot)
            {
                KartInput auto = _autopilot.Drive(_world.Kart);
                auto.RespawnPressed = input.RespawnPressed;
                input = auto;
            }

            // R: devolve ao último checkpoint (ver Kart.RespawnHandler).
            if (input.RespawnPressed) _world.Laps.Rescue(_world.Kart);
            input.RespawnPressed = false;

            _world.Step(dt, input);
        }

        private void OnGUI()
        {
            if (_world != null) _hud.Draw(_world, _paused);
            _touch?.Draw();
        }

        private void OnDestroy() => _touch?.Dispose();
    }
}
