using System.IO;
using CandyRush.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyRush.EditorTools
{
    /// <summary>
    /// Prepara o projeto: cria a cena principal com o CandyRushGame, coloca-a
    /// no Build Profile e ajusta as opções de Player (nome, iOS em paisagem).
    ///
    /// Roda sozinho na primeira vez que o projeto é aberto (se a cena não
    /// existir) e também pelo menu Candy Rush > Preparar projeto.
    /// </summary>
    [InitializeOnLoad]
    public static class CandyRushSetup
    {
        public const string ScenePath = "Assets/Scenes/CandyRush.unity";

        static CandyRushSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !Application.isBatchMode) Run();
            };
        }

        [MenuItem("Candy Rush/Preparar projeto")]
        public static void Run()
        {
            CreateScene();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[Candy Rush] Projeto preparado. Abra Assets/Scenes/CandyRush.unity e aperte Play.");
        }

        private static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            new GameObject("Candy Rush").AddComponent<CandyRushGame>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Candy Rush";
            PlayerSettings.productName = "Candy Rush";
            // Troque pelo seu identificador (ex.: com.seunome.candyrush) antes de publicar.
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.candyrush.game");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.candyrush.game");

            // Jogo de corrida: paisagem, girando entre os dois lados.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Mac: janela redimensionável em 16:9 por padrão.
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
        }
    }
}
