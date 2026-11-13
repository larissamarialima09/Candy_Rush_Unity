using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Roda a cena principal em modo de jogo por alguns quadros dentro do próprio
/// editor, em lote, só para pegar exceções de runtime (referência nula,
/// index fora da faixa) que a compilação não detecta. Sem GPU de verdade em
/// -nographics a única coisa que pode falhar são os shaders — o resto (a
/// montagem da cena inteira em Awake) já é sinal suficiente de que o
/// bootstrap não está quebrado.
/// </summary>
[InitializeOnLoad]
public static class PlayModeSmokeTest
{
    static int framesLeft;
    const string RunningKey = "CandyRush.SmokeTest.Running";

    static PlayModeSmokeTest()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        framesLeft = 30;
        EditorApplication.update += Tick;
    }

    [MenuItem("CandyRush/Smoke Test Play Mode")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        framesLeft = 30;
        SessionState.SetBool(RunningKey, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        framesLeft--;
        if (framesLeft <= 0)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(RunningKey, false);
            if (UnityEngine.Object.FindObjectsByType<RabbitKartAnimation>(FindObjectsSortMode.None).Length == 0)
            {
                Debug.LogError("SMOKE_TEST_FAILED: nenhum personagem 3D ativo na cena.");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("SMOKE_TEST_OK");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }
}
