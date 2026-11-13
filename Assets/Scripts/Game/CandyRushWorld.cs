using CandyRush.CameraRig;
using CandyRush.Config;
using CandyRush.Core;
using CandyRush.Rendering;
using CandyRush.Track;
using CandyRush.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace CandyRush.Game
{
    /// <summary>
    /// O mundo do jogo: monta pista, kart, câmera e luzes, e avança a
    /// simulação um passo fixo por vez. Não é MonoBehaviour de propósito:
    /// o CandyRushGame (jogo) e as ferramentas de teste do Editor usam a
    /// mesma classe.
    ///
    /// Ordem de um passo (igual ao main.ts da web): kart → obstáculos, rampa
    /// e geleias → barreiras → moedas → checkpoints/voltas → câmera.
    /// </summary>
    public sealed class CandyRushWorld
    {
        public readonly CircuitPath Path;
        public readonly TrackSurface Surface;
        public readonly BarrierSystem Barriers;
        public readonly LapTracker Laps;
        public readonly Kart Kart;
        public readonly KartView KartView;
        public readonly ChaseCamera Camera;

        // Elementos da pista (Etapa 2).
        public readonly SpeedRamp Ramp;
        public readonly JellyBlobs Jellies;
        public readonly CandyObstacles Obstacles;
        public readonly Coins Coins;
        public readonly CandyCastle Castle;

        public readonly Transform Root;

        public CandyRushWorld(Camera camera, Transform parent = null)
        {
            Root = new GameObject("Candy Rush (mundo)").transform;
            if (parent != null) Root.SetParent(parent, false);

            Path = new CircuitPath(CircuitConfig.Centerline(), CircuitConfig.Samples, CircuitConfig.CurvatureWindow);
            Surface = new TrackSurface(Path);
            Barriers = new BarrierSystem(Path);
            Laps = new LapTracker(Path);

            Kart = new Kart();
            // R, queda ou capotagem: volta ao último checkpoint cruzado.
            Kart.RespawnHandler = kart => Laps.Rescue(kart);

            SetupAtmosphere(camera);
            SetupLights();
            TrackView.Build(Path, Root);
            StartGate.Build(Path, Root);
            DonutTunnel.Build(Path, Root);
            Castle = new CandyCastle(Path, Root);
            Ramp = new SpeedRamp(Path, Root);
            Jellies = new JellyBlobs(Path, Root);
            Obstacles = new CandyObstacles(Path, Root);
            Coins = new Coins(Path, Root);
            KartView = new KartView(Kart, Root);

            Camera = new ChaseCamera(camera);
            ResetRace();
        }

        /// <summary>Recoloca o kart no grid e zera voltas e cronômetro.</summary>
        public void ResetRace()
        {
            Laps.Reset();
            Coins.Reset();
            PlaceOnGrid(Kart, 0);
            Camera.Reset(Kart);
            KartView.Update(1f);
            Camera.ApplyToRender(1f);
        }

        /// <summary>Um passo FIXO de simulação.</summary>
        public void Step(float dt, KartInput input)
        {
            Kart.Update(dt, input, Surface);

            // Obstáculos, rampa e geleia são REGRA DE JOGO: andam no passo fixo.
            Obstacles.Update(dt);
            Obstacles.Collide(Kart);
            Jellies.Update(dt);
            Ramp.Collide(Kart);
            Jellies.Collide(Kart, dt);

            // Barreiras depois da integração: corrigem posição e velocidade, como uma resolução de contato.
            Barriers.Update(dt, Kart);
            Coins.Update(dt, Kart);
            Laps.Update(dt, Kart);
            Camera.Update(dt, Kart, Kart.Telemetry.OffTrack);
        }

        /// <summary>Animações só visuais (letreiros), com o tempo do quadro e não o passo fixo.</summary>
        public void Animate(float frameDt)
        {
            Castle.Animate(frameDt);
        }

        /// <summary>Aplica o estado interpolado ao visual.</summary>
        public void Render(float alpha)
        {
            KartView.Update(alpha);
            Camera.ApplyToRender(alpha);
        }

        /// <summary>
        /// Coloca um kart numa fileira do grid (fileira 0 = na linha de largada).
        /// Fileiras pares à esquerda, ímpares à direita, como na web.
        /// </summary>
        public void PlaceOnGrid(Kart kart, int row)
        {
            int startIndex = Path.IndexAtFraction(0f);
            int spacingSamples = Mathf.RoundToInt(RaceConfig.Grid.RowSpacing / (Path.TotalLength / Path.Count));
            int index = (startIndex - row * spacingSamples + Path.Count * 4) % Path.Count;
            float lateral = (row % 2 == 0 ? 1f : -1f) * RaceConfig.Grid.LateralOffset;

            Vector3 position = Path.PointAt(index, lateral, TrackConfig.OffTrack.RespawnHeight);
            kart.RespawnAt(position, Path.HeadingAt(index));
        }

        /// <summary>Céu, névoa e luz ambiente em tons de bala.</summary>
        private static void SetupAtmosphere(Camera camera)
        {
            Material sky = CandyMaterials.Sky(CircuitConfig.Hex(0xb9dcff), LightingConfig.Horizon);
            RenderSettings.skybox = sky;
            camera.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = LightingConfig.Horizon;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = LightingConfig.Horizon;
            RenderSettings.fogStartDistance = LightingConfig.Fog.Near;
            RenderSettings.fogEndDistance = LightingConfig.Fog.Far;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = LightingConfig.Ambient.Sky;
            RenderSettings.ambientEquatorColor = LightingConfig.Ambient.Equator;
            RenderSettings.ambientGroundColor = LightingConfig.Ambient.Ground;
        }

        /// <summary>Sol com sombras e uma luz de contorno azulada, nas direções da versão web.</summary>
        private void SetupLights()
        {
            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.transform.SetParent(Root, false);
            sun.type = LightType.Directional;
            sun.color = LightingConfig.Sun.Color;
            sun.intensity = LightingConfig.Sun.Intensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.shadowNormalBias = 0.4f;
            sun.transform.rotation = Quaternion.LookRotation(-LightingConfig.DirectionFrom(LightingConfig.Sun.BearingDegrees, LightingConfig.Sun.ElevationDegrees));
            RenderSettings.sun = sun;

            var rim = new GameObject("Contorno").AddComponent<Light>();
            rim.transform.SetParent(Root, false);
            rim.type = LightType.Directional;
            rim.color = LightingConfig.Rim.Color;
            rim.intensity = LightingConfig.Rim.Intensity;
            rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.LookRotation(-LightingConfig.DirectionFrom(LightingConfig.Rim.BearingDegrees, LightingConfig.Rim.ElevationDegrees));

            // Sombras só perto da câmera: é onde elas importam, e mantém o custo baixo no iPhone.
            QualitySettings.shadowDistance = 60f;
        }
    }
}
