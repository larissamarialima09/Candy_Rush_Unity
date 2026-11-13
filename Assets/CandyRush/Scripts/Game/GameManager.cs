using System.Collections.Generic;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>
    /// O bootstrap do jogo — equivalente a main.ts + core/gameLoop.ts.
    ///
    /// Monta a cena inteira em código (não há .unity a mão: tudo aqui nasce de
    /// Resources/Shaders e das classes de Scripts/*), mantém o próprio
    /// acumulador de passo fixo (em vez do FixedUpdate do Unity) porque o resto
    /// do port — Kart, ChaseCamera, KartView — já espera um `alpha` de
    /// interpolação exatamente como o gameLoop.ts original fornecia.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        const int SeriesHeats = 2;
        public FuelRaceSettings fuelRaceSettings;
        public CandyPitStop pitStop;
        public CandyPitStop pitStopPrefab;
        FuelHUD fuelHud;

        sealed class Player
        {
            public int Index;
            public string Name;
            public Kart Kart;
            public KartView View;
            public Camera CameraComponent;
            public ChaseCamera Camera;
            public LapTracker LapTracker;
            public BarrierSystem Barriers;
            public KartEffects Effects;
            public PlayerInput Input;
            public float ReverseTime, ReverseDistance;
        }

        sealed class Rival
        {
            public Kart Kart;
            public KartView View;
            public RacerController Controller;
            public BotPitStrategy Strategy;
            public Competitor Competitor;
            public KartEffects Effects;
        }

        // --- Cena ---
        Light sun, rim;
        Light kartLamp;
        Vector3 sunOffsetUnity, rimOffsetUnity;
        CircuitPath circuitPath;
        TrackSurface trackSurface;
        CircuitScenery scenery;
        Audience audience;
        Coins coins;
        CandyObstacles candyObstacles;
        SpeedRamp speedRamp;
        JellyBlobs jellyBlobs;
        CandyParticles particles;
        SkidMarks skidMarks;
        CandyMusic music;
        RaceResultSound resultSound;

        readonly List<Player> players = new List<Player>();
        readonly List<Player> activePlayers = new List<Player>();
        readonly List<Rival> rivals = new List<Rival>();
        readonly List<Rival> activeRivals = new List<Rival>();
        RaceDirector director;
        RaceSeries raceSeries;
        GameState state;
        Transform playersRoot;
        Transform sceneRoot;
        /// <summary>
        /// Raiz com escala (-1,1,1): tudo que nasce dela usa os números do
        /// three.js sem tradução, e o espelho em X faz o resto. Câmera e luzes
        /// ficam DE FORA dela — essas, sim, convertem cada ponto com
        /// <see cref="ThreeSpace.ToUnity"/>, porque não têm "local" nenhum a
        /// espelhar: precisam de posição e direção corretas no mundo real.
        /// </summary>
        Transform worldRoot;

        GameUI ui;
        Viewport[] viewports = new Viewport[0];

        // --- Passo fixo ---
        float accumulator;
        float fps;
        float startSequence;
        readonly InputState gridInput = new InputState { Handbrake = true };

        readonly List<Kart> activeKartsScratch = new List<Kart>();
        readonly List<GameUI.HudEntry> hudEntries = new List<GameUI.HudEntry>();

        void Awake()
        {
            if(fuelRaceSettings==null) fuelRaceSettings=FuelRaceSettings.Load();
            // Restaura o pedal para instalações que receberam aceleração automática por padrão.
            // Depois desta migração, a escolha feita nas configurações continua sendo respeitada.
            if(!PlayerPrefs.HasKey("CandyRush.PedalControlsVersion"))
            {
                PlayerPrefs.SetInt("CandyRush.AutoAcceleration",0);
                PlayerPrefs.SetInt("CandyRush.PedalControlsVersion",1);
                PlayerPrefs.Save();
            }
            if(PlayerPrefs.HasKey("CandyRush.AutoAcceleration")) fuelRaceSettings.autoAcceleration=PlayerPrefs.GetInt("CandyRush.AutoAcceleration")==1;
            if(PlayerPrefs.HasKey("CandyRush.AutomaticDrift")) fuelRaceSettings.automaticDrift=PlayerPrefs.GetInt("CandyRush.AutomaticDrift")==1;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;

            worldRoot = new GameObject("world-mirror").transform;
            worldRoot.localScale = new Vector3(-1, 1, 1);

            BuildLighting();
            BuildTrackAndScenery();
            BuildActors();

            state = new GameState();
            director = new RaceDirector(circuitPath);
            raceSeries = new RaceSeries(SeriesHeats);

            music = gameObject.AddComponent<CandyMusic>();
            // O receptor fica separado do sintetizador, para o filtro da música não alterar os efeitos.
            var listener = new GameObject("GameAudioListener");
            listener.transform.SetParent(transform, false);
            listener.AddComponent<AudioListener>();
            var pickupAudio = new GameObject("CoinPickupAudio");
            pickupAudio.transform.SetParent(transform, false);
            var coinSound = pickupAudio.AddComponent<CoinPickupSound>();
            coins.Collected += () =>
            {
                if (!state.IsRacing) return;
                coinSound.PlayPickup();
                ShopProfile.Credit(1);
            };
            var resultAudio = new GameObject("RaceResultAudio");
            resultAudio.transform.SetParent(transform, false);
            resultSound = resultAudio.AddComponent<RaceResultSound>();

            ui = gameObject.AddComponent<GameUI>();
            ui.Init(state, OnStart, OnResume, OnRestart, OnMenu);
            ui.SetRaceSettings(fuelRaceSettings);
            ui.SetCircuit(circuitPath, director);
            fuelHud=gameObject.AddComponent<FuelHUD>();fuelHud.Initialize(state,pitStop);

            // Um piloto já existe no menu: é o kart que a câmera orbita na vitrine.
            SetPlayerCount(1);
            ResetRace();
            foreach (var p in players) p.Camera.Reset(p.Kart);
        }

        void BuildLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex.Linear(LightingConfig.HemisphereSky) * LightingConfig.HemisphereIntensity * LightingConfig.AmbientScale;
            RenderSettings.ambientGroundColor = Hex.Linear(LightingConfig.HemisphereGround) * LightingConfig.HemisphereIntensity * LightingConfig.AmbientScale;
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientSkyColor, RenderSettings.ambientGroundColor, 0.5f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex.Linear(0xffe6f2);
            RenderSettings.fogStartDistance = LightingConfig.FogNear;
            RenderSettings.fogEndDistance = LightingConfig.FogFar;

            var sunGo = new GameObject("sun");
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex.C(LightingConfig.SunColor);
            sun.intensity = LightingConfig.SunIntensity * LightingConfig.DirectScale;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = LightingConfig.SunShadowStrength;
            sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;

            var rimGo = new GameObject("rim");
            rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = Hex.C(LightingConfig.RimColor);
            rim.intensity = LightingConfig.RimIntensity * LightingConfig.DirectScale;
            rim.shadows = LightShadows.None;

            var lampGo = new GameObject("kart-lamp");
            kartLamp = lampGo.AddComponent<Light>();
            kartLamp.type = LightType.Point;
            kartLamp.color = Hex.C(LightingConfig.KartLampColor);
            kartLamp.intensity = LightingConfig.KartLampIntensity * LightingConfig.KartLampScale;
            kartLamp.range = LightingConfig.KartLampDistance;
            kartLamp.shadows = LightShadows.None;

            // O sol, a luz de contorno e a lanterna não pertencem à raiz
            // espelhada (não têm "local" — precisam de posição/direção certas
            // no mundo real), então cada uma converte o próprio deslocamento.
            sunOffsetUnity = ThreeSpace.ToUnity(LightOffset(LightingConfig.SunBearingRadians, LightingConfig.SunElevationDegrees, 240f));
            rimOffsetUnity = ThreeSpace.ToUnity(LightOffset(LightingConfig.RimBearingRadians, LightingConfig.RimElevationDegrees, 240f));

            // Céu: o próprio shader como skybox, desenhado no infinito. Uma esfera
            // não servia: o plano distante da câmera (2000) cortava uma esfera de
            // raio 3800 por inteiro, e o que aparecia era o skybox azul padrão.
            RenderSettings.skybox = new Material(Mats.SkyShader) { name = "Sky" };
            gameObject.AddComponent<CandyPostProcessing>();
        }

        static Vector3 LightOffset(float bearing, float elevationDegrees, float distance)
        {
            float elevation = elevationDegrees * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(elevation) * distance;
            return new Vector3(Mathf.Sin(bearing) * horizontal, Mathf.Sin(elevation) * distance, Mathf.Cos(bearing) * horizontal);
        }

        void BuildTrackAndScenery()
        {
            sceneRoot = SceneKit.Node("scene", worldRoot);

            circuitPath = new CircuitPath(CircuitConfig.Centerline, CircuitConfig.Samples, CircuitConfig.CurvatureWindow);
            trackSurface = new TrackSurface(circuitPath);

            Texture2D meadow = ProceduralTextures.Meadow();
            TerrainBuilder.BuildHorizon(sceneRoot, meadow);
            TerrainBuilder.Build(circuitPath, sceneRoot, meadow);

            Texture2D trackTex = ProceduralTextures.CandyTrack();
            Texture2D trackNormals = ProceduralTextures.CandyNormalMap();
            new CircuitSurface(circuitPath, sceneRoot, trackTex, trackNormals);

            scenery = new CircuitScenery(circuitPath, sceneRoot);
            new CandyProps(circuitPath, sceneRoot);
            new CandySigns(circuitPath, sceneRoot);
            audience = new Audience(scenery.Grandstand, sceneRoot);
            coins = new Coins(circuitPath, sceneRoot);

            candyObstacles = new CandyObstacles(circuitPath, sceneRoot);
            speedRamp = new SpeedRamp(circuitPath, sceneRoot);
            jellyBlobs = new JellyBlobs(circuitPath, sceneRoot);
            particles = new CandyParticles(sceneRoot);
            skidMarks = new SkidMarks(sceneRoot);
            if(pitStop==null)
            {
                if(pitStopPrefab!=null) pitStop=Instantiate(pitStopPrefab,sceneRoot,false);
                else pitStop=SceneKit.Node("PostoDoce",sceneRoot).gameObject.AddComponent<CandyPitStop>();
            }
            pitStop.trackTexture=trackTex;pitStop.trackNormals=trackNormals;
            pitStop.Configure(circuitPath,fuelRaceSettings);
        }

        void BuildActors()
        {
            playersRoot = new GameObject("players").transform;

            float steerSign = RacerController.MeasureSteerSign(trackSurface);
            for (int i = 0; i < RaceConfig.Rivals.Length; i++)
            {
                var profile = RaceConfig.Rivals[i];
                var kart = new Kart(fuelRaceSettings);
                rivals.Add(new Rival
                {
                    Kart = kart,
                    View = new KartView(kart, sceneRoot, KartPalette.Rival(profile.Sprite)),
                    Controller = new RacerController(circuitPath, profile, i * 1.7f, steerSign),
                    Strategy = new BotPitStrategy(i),
                    Effects = new KartEffects(kart,particles),
                });
            }
        }

        Player CreatePlayer(int index)
        {
            var kart = new Kart(fuelRaceSettings);
            var profile = CoopConfig.Players[Mathf.Min(index, CoopConfig.Players.Length - 1)];
            var view = index == 0
                ? new KartView(kart, sceneRoot, RacerProfile.Palette)
                : new KartView(kart, sceneRoot, KartPalette.Rival(profile.Sprite));

            var camGo = new GameObject($"camera-p{index + 1}");
            camGo.transform.SetParent(playersRoot, false);
            var cam = camGo.AddComponent<Camera>();
            CandyPostProcessing.ConfigureCamera(cam);
            cam.nearClipPlane = CameraConfig.Near;
            cam.farClipPlane = CameraConfig.Far;
            if (index > 0) cam.depth = 1;

            return new Player
            {
                Index = index,
                Name = index == 0 ? RacerProfile.Name : profile.Label,
                Kart = kart,
                View = view,
                CameraComponent = cam,
                Camera = new ChaseCamera(cam),
                LapTracker = new LapTracker(circuitPath),
                Barriers = new BarrierSystem(circuitPath),
                Effects = new KartEffects(kart, particles),
                Input = new PlayerInput(KeyMap.Solo),
            };
        }

        void ConfigureInputs()
        {
            bool coop = players.Count > 1;
            foreach (var p in players)
            {
                KeyMap keys = coop ? (p.Index == 0 ? KeyMap.CoopP1 : KeyMap.CoopP2) : KeyMap.Solo;
                int gamepadIndex = coop ? (p.Index == 0 ? 1 : 0) : 0;
                p.Input?.Dispose();
                p.Input = new PlayerInput(keys, gamepadIndex, touch: p.Index == 0,settings:fuelRaceSettings);
            }
        }

        void SetPlayerCount(int count)
        {
            while (players.Count < count) players.Add(CreatePlayer(players.Count));

            activePlayers.Clear();
            for (int i = 0; i < count; i++) activePlayers.Add(players[i]);

            foreach (var p in players)
            {
                bool active = p.Index < count;
                p.View.SetVisible(active);
                p.CameraComponent.gameObject.SetActive(active);
            }

            ConfigureInputs();
            ConfigureRabbitLineup(count);

            director.Competitors.Clear();
            for (int i = 0; i < count; i++)
                director.Add(players[i].Name, players[i].Kart, true, players[i].LapTracker);
            for (int i = 0; i < activeRivals.Count; i++)
                activeRivals[i].Competitor=director.Add(RaceConfig.Rivals[i].Name, activeRivals[i].Kart, false);
            pitStop.Bind(director.Competitors);

            LayoutViewports();
            raceSeries.Reset(director.Competitors);
            ui.SetPlayerCount(count);
        }

        void ConfigureRabbitLineup(int humanCount)
        {
            activeRivals.Clear();
            // Distribui as quatro aparências a partir da escolha do jogador.
            // No cooperativo, os humanos também recebem aparências distintas.
            for (int i = 0; i < humanCount; i++)
                players[i].View.SetPalette(KartPalette.Rival(RacerProfile.RabbitSprites[(RacerProfile.Rabbit + i) % 4]));
            for (int i = 0; i < rivals.Count; i++)
            {
                bool active = i < 4 - humanCount;
                rivals[i].View.SetVisible(active);
                if (active)
                {
                    rivals[i].View.SetPalette(KartPalette.Rival(RacerProfile.RabbitSprites[(RacerProfile.Rabbit + humanCount + i) % 4]));
                    activeRivals.Add(rivals[i]);
                }
            }
        }

        void LayoutViewports()
        {
            int count = Mathf.Max(1, activePlayers.Count);
            viewports = SplitScreen.ComputeViewports(count, Screen.width, Screen.height);

            for (int i = 0; i < count; i++)
            {
                var p = players[i];
                var v = viewports[i];
                // Unity conta a partir da base da tela; a UI (OnGUI) conta do topo.
                float nx = v.X / Screen.width, nw = v.Width / Screen.width;
                float nh = v.Height / Screen.height;
                float ny = 1f - (v.Y + v.Height) / Screen.height;
                p.CameraComponent.rect = new Rect(nx, ny, nw, nh);
                p.Camera.SetFovBonus(SplitScreen.FovBonusFor(count));
            }
        }

        void ResetRace()
        {
            startSequence = 3.8f;
            accumulator = 0;
            pitStop.ResetRace();
            foreach(var rival in rivals) rival.Strategy.Reset();
            if (resultSound != null) resultSound.ResetResult();
            for (int i = 0; i < players.Count; i++) PlaceOnGrid(players[i].Kart, i);
            for (int i = 0; i < activeRivals.Count; i++) PlaceOnGrid(activeRivals[i].Kart, activePlayers.Count + i);
            director.Reset();
            skidMarks.Clear();
            coins.Reset();
            foreach (var p in players) { p.LapTracker.Reset(); p.ReverseTime = p.ReverseDistance = 0; }
        }

        void PlaceOnGrid(Kart kart, int row)
        {
            int startIndex = circuitPath.IndexAtFraction(0);
            float sampleSpacing = circuitPath.TotalLength / circuitPath.Count;
            int spacingSamples = Mathf.RoundToInt(RaceConfig.GridRowSpacing / sampleSpacing);
            int index = ((startIndex - row * spacingSamples) % circuitPath.Count + circuitPath.Count * 4) % circuitPath.Count;
            float lateral = (row % 2 == 0 ? 1 : -1) * RaceConfig.GridLateralOffset;

            Vector3 position = circuitPath.PointAt(index, lateral, TrackConfig.RespawnHeight);
            PathSample sample = circuitPath.Samples[index];
            kart.RespawnAt(position, Mathf.Atan2(sample.Tangent.x, sample.Tangent.z));
            kart.Drift.Reset();
            kart.Fuel.ResetRace();
        }

        // --- Botões de UI ---

        void OnStart(int playerCount)
        {
            if (players.Count > 0)
            {
                players[0].Name = RacerProfile.Name;
            }
            SetPlayerCount(playerCount);
            ResetRace();
            foreach (var p in players) p.Camera.Reset(p.Kart);
            state.Set(GameStateName.Racing);
            music.Begin();
        }

        void OnResume() => state.Set(GameStateName.Racing);

        void OnMenu()
        {
            ShopProfile.Save();
            music.Stop();
            if (resultSound != null) resultSound.ResetResult();
            ui.HideFinish();
            state.Set(GameStateName.Menu);
        }

        void OnRestart()
        {
            ShopProfile.Save();
            ui.HideFinish();
            raceSeries.Reset(director.Competitors);
            ResetRace();
            foreach (var p in players) p.Camera.Reset(p.Kart);
            state.Set(GameStateName.Racing);
            music.Begin();
        }

        // --- Loop ---

        void Update()
        {
            float frameDt = Time.unscaledDeltaTime;
            if (frameDt > 0) fps += (1f / frameDt - fps) * 0.08f;
            if (frameDt > PhysicsConfig.MaxFrameTime) frameDt = PhysicsConfig.MaxFrameTime;

            accumulator += frameDt;
            float dt = PhysicsConfig.FixedTimeStep;
            int steps = 0;
            while (accumulator >= dt && steps < PhysicsConfig.MaxStepsPerFrame)
            {
                FixedStep(dt);
                accumulator -= dt;
                steps++;
            }
            if (steps == PhysicsConfig.MaxStepsPerFrame) accumulator = 0;
            // Pausado, o tempo do mundo não avança: nada na tela pode continuar se mexendo.
            if (state.Name == GameStateName.Paused) accumulator = 0;

            Render(accumulator / dt, frameDt);
        }

        List<Kart> ActiveKarts()
        {
            activeKartsScratch.Clear();
            foreach (var p in activePlayers) activeKartsScratch.Add(p.Kart);
            return activeKartsScratch;
        }

        void FixedStep(float dt)
        {
            foreach (var p in activePlayers) p.Input.Sample();

            bool pausePressed = false;
            foreach (var p in activePlayers) if (p.Input.State.PausePressed) pausePressed = true;
            if (pausePressed && !ui.FinishVisible) state.TogglePause();
            if (!state.Simulates) return;

            if (startSequence > 0)
            {
                bool waiting = startSequence > .8f;
                startSequence = Mathf.Max(0, startSequence - dt);
                if (waiting)
                {
                    // Assenta as rodas no chão, sem aceitar aceleração ou contar tempo de corrida.
                    foreach (var p in activePlayers)
                    {
                        p.Kart.Update(dt, gridInput, trackSurface);
                        p.Camera.Update(dt, p.Kart, false);
                    }
                    foreach (var r in activeRivals) r.Kart.Update(dt, gridInput, trackSurface);
                    return;
                }
            }

            foreach(var r in activeRivals)
            {
                var command=r.Controller.Update(dt,r.Kart);
                r.Strategy.Update(dt,r.Competitor,pitStop,command);
            }
            pitStop.Step(dt);
            foreach (var p in activePlayers) if(!p.Kart.PitControlled) p.Kart.Update(dt, p.Input.State, trackSurface);
            foreach (var r in activeRivals) if(!r.Kart.PitControlled) r.Kart.Update(dt,r.Controller.Command,trackSurface);

            candyObstacles.Update(dt);
            foreach (var p in activePlayers) if(!p.Kart.PitControlled) candyObstacles.Collide(p.Kart);
            foreach (var r in activeRivals) if(!r.Kart.PitControlled) candyObstacles.Collide(r.Kart);

            jellyBlobs.Update(dt);
            foreach (var p in activePlayers) if(!p.Kart.PitControlled) { speedRamp.Collide(p.Kart, dt); jellyBlobs.Collide(p.Kart, dt); }
            foreach (var r in activeRivals) if(!r.Kart.PitControlled) { speedRamp.Collide(r.Kart, dt); jellyBlobs.Collide(r.Kart, dt); }

            if (activePlayers.Count > 0)
            {
                foreach (var r in activeRivals) if(!r.Kart.PitControlled) activePlayers[0].Barriers.Update(dt, r.Kart);
            }
            foreach (var p in activePlayers) if(!p.Kart.PitControlled) p.Barriers.Update(dt, p.Kart);

            coins.Update(dt, ActiveKarts());

            foreach (var p in activePlayers) EnforceRaceDirection(p, dt);
            director.Update(dt);
            foreach (var p in activePlayers) p.Camera.Update(dt, p.Kart, p.Kart.Telemetry.OffTrack);

            if (director.PlayersFinished && !ui.FinishVisible)
            {
                var standings = raceSeries.Record(director.Competitors);
                ui.ShowFinish(director.Competitors, director.PlayerPosition, director.Winner, standings, raceSeries.Heat);
                resultSound.PlayForPosition(director.PlayerPosition);
                state.Set(GameStateName.Paused);
            }
        }

        void EnforceRaceDirection(Player player, float dt)
        {
            if(player.Kart.PitControlled || pitStop.IsInDriveArea(player.Kart)) {player.ReverseTime=player.ReverseDistance=0;return;}
            Vector3 position = player.Kart.Body.Position;
            int index = circuitPath.NearestSampleIndex(position.x, position.z);
            float speed = Vector3.Dot(player.Kart.Body.Velocity, circuitPath.Samples[index].Tangent);
            if (speed < -.35f)
            {
                player.ReverseTime += dt;
                player.ReverseDistance -= speed * dt;
            }
            else if (speed > .35f)
            {
                player.ReverseTime = player.ReverseDistance = 0;
            }
            // Tolera pequenas manobras e impactos; impede seguir pela pista ao contrário.
            if (player.ReverseTime < .8f || player.ReverseDistance < 4f) return;
            player.LapTracker.ReturnToStart(player.Kart, player.Index * RaceConfig.GridLateralOffset);
            player.Camera.Reset(player.Kart);
            player.ReverseTime = player.ReverseDistance = 0;
            ui.ShowWrongWay(player.Index);
        }

        void Render(float alpha, float frameDt)
        {
            ui.SetStartCountdown(startSequence);
            pitStop.SetPaused(!state.IsRacing);
            var active = activePlayers;
            if (active.Count == 0) return;

            if (state.Name == GameStateName.Menu)
            {
                active[0].Camera.Showcase(frameDt, active[0].Kart);
                candyObstacles.Update(frameDt);
                jellyBlobs.Update(frameDt);
                coins.Update(frameDt, ActiveKarts());
            }

            foreach (var p in active) { p.View.Update(alpha, state.Simulates ? frameDt : 0); p.Camera.ApplyToRender(alpha); }
            foreach (var r in activeRivals) r.View.Update(alpha, state.Simulates ? frameDt : 0);

            sun.transform.forward = -sunOffsetUnity.normalized;
            rim.transform.forward = -rimOffsetUnity.normalized;

            Vector3 lampPos = ThreeSpace.ToUnity(active[0].Kart.Body.ChassisPosition);
            lampPos.y += LightingConfig.KartLampOffsetY;
            kartLamp.transform.position = lampPos;

            if (state.IsRacing)
            {
                foreach (var p in active) p.Effects.Update(frameDt, p.Barriers.Contact);
                foreach(var r in activeRivals) r.Effects.Update(frameDt,null);
                skidMarks.Update(frameDt, ActiveKarts());
            }
            Transform camTransform = active[0].Camera.Camera.transform;
            float worldDt = state.Name == GameStateName.Paused ? 0 : frameDt;
            particles.Update(worldDt, camTransform.right, camTransform.up);
            audience.Update(worldDt, active[0].Kart.Body.Position, active[0].Kart.Telemetry.Speed);
            scenery.Castle.Update(worldDt);

            hudEntries.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                var p = active[i];
                hudEntries.Add(new GameUI.HudEntry
                {
                    Index = p.Index,
                    Name = p.Name,
                    Kart = p.Kart,
                    Tracker = p.LapTracker,
                    Viewport = i < viewports.Length ? viewports[i] : default,
                    CoinsCollected = p.Index < coins.CollectedByPlayer.Count ? coins.CollectedByPlayer[p.Index] : 0,
                    JustCollectedCoin = p.Index < coins.JustCollectedByPlayer.Count && coins.JustCollectedByPlayer[p.Index],
                });
            }
            ui.UpdateHud(hudEntries, coins, viewports, frameDt, fps);
            fuelHud.Display(hudEntries,frameDt);
        }

        void LateUpdate()
        {
            if (Screen.width > 0 && Screen.height > 0) LayoutViewports();
        }
        void OnApplicationPause(bool paused) { if (paused) ShopProfile.Save(); }
        void OnApplicationQuit() => ShopProfile.Save();
        void OnDestroy() { ShopProfile.Save(); foreach(var p in players) p.Input?.Dispose(); }
    }
}
