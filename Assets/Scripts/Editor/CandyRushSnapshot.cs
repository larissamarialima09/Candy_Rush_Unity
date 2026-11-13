using System.IO;
using CandyRush.Config;
using CandyRush.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyRush.EditorTools
{
    /// <summary>
    /// Tira fotos do jogo sem apertar Play: monta o mundo numa cena
    /// temporária, deixa o piloto automático dirigir alguns segundos e
    /// renderiza a câmera para PNGs em Temp/CandyRushSnapshots/.
    ///
    /// Menu Candy Rush > Tirar fotos, ou em linha de comando:
    ///   Unity -batchmode -quit -projectPath . -executeMethod CandyRush.EditorTools.CandyRushSnapshot.Run
    /// </summary>
    public static class CandyRushSnapshot
    {
        private const int Width = 1280;
        private const int Height = 720;

        [MenuItem("Candy Rush/Tirar fotos")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Snapshot Camera").AddComponent<Camera>();
            var world = new CandyRushWorld(camera);
            var autopilot = new Autopilot(world.Path);

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "CandyRushSnapshots");
            Directory.CreateDirectory(folder);

            Save(camera, Path.Combine(folder, "0_largada.png"));

            float[] moments = { 4f, 9f, 16f, 24f };
            float time = 0f;
            int shot = 1;
            foreach (float moment in moments)
            {
                while (time < moment)
                {
                    world.Step(PhysicsConfig.FixedTimeStep, autopilot.Drive(world.Kart));
                    time += PhysicsConfig.FixedTimeStep;
                }
                world.Render(1f);
                Save(camera, Path.Combine(folder, $"{shot}_{moment:0}s.png"));
                shot++;
            }

            Debug.Log($"[Candy Rush] Fotos salvas em {folder}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Save(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.aspect = (float)Width / Height;
            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }
}
