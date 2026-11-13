using System;
using System.Collections;
using System.Reflection;
using CandyRush.Integrated;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class IntegratedFlowValidation
{
    const string Key = "CandyRush.Integration.Validation";
    static int frames;
    static bool failed;
    static IntegratedFlowValidation()
    {
        if (SessionState.GetBool(Key, false)) Attach();
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool(Key, true);
        Attach();
        EditorApplication.EnterPlaymode();
    }
    static void Attach()
    {
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
        };
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }
    static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            frames++;
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (frames == 90) ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("../menu-inicial-integrado.png"));
            if (frames < 120) return;
            Check(manager != null, "GameManager ausente");
            var state = (GameState)Field(manager, "state");
            var players = (IList)Field(manager, "activePlayers");
            var director = (RaceDirector)Field(manager, "director");
            if (frames == 120)
            {
                Check(state.Name == GameStateName.Menu, "Menu inicial ausente");
                var menu = UnityEngine.Object.FindFirstObjectByType<MenuCandyRush>();
                var uiMenu = UnityEngine.Object.FindFirstObjectByType<GameUI>();
                var front = (CandyFrontEnd)Field(uiMenu, "frontEnd");
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                menu.transform.Find("Palco/BotaoConfig").GetComponent<BotaoDoce>().OnPointerClick(pointer);
                Check(front.CurrentPage == CandyFrontEnd.Page.Rabbit, "Botao coelho nao abriu personagens");
                string originalName = RacerProfile.Name;
                int originalRabbit = RacerProfile.Rabbit;
                try
                {
                    RacerProfile.Save("Teste Candy", RacerProfile.Rabbit);
                    front.SelectRabbit(2);
                    front.ConfirmRabbit();
                    Check(RacerProfile.Rabbit == 2 && RacerProfile.Name == "Teste Candy", "Perfil nao foi salvo");
                    Check(front.CurrentPage == CandyFrontEnd.Page.Home, "Selecionar coelho nao voltou ao menu inicial");
                    menu.AtualizarPerfil();
                    menu.transform.Find("Palco/PlacaNome").GetComponent<BotaoDoce>().OnPointerClick(pointer);
                    Check(front.CurrentPage == CandyFrontEnd.Page.Home, "Editar nome saiu do menu inicial");
                    var nameField = menu.transform.Find("Palco/PlacaNome").GetComponent<UnityEngine.UI.InputField>();
                    Check(nameField != null && nameField.characterLimit == RacerProfile.MaxNameLength, "Campo de nome ausente ou sem limite");
                    nameField.text = "Nome Direto";
                    nameField.onEndEdit.Invoke(nameField.text);
                    Check(RacerProfile.Name == "Nome Direto" && RacerProfile.Rabbit == 2, "Edicao direta nao salvou nome ou alterou coelho");
                    nameField.text = "";
                    menu.SalvarNome();
                    Check(nameField.text == "Piloto Doce", "Nome vazio nao recebeu valor padrao");
                }
                finally { RacerProfile.Save(originalName, originalRabbit); menu.AtualizarPerfil(); }
                Debug.Log("MENU_PROFILE_OK: character button, selection, name edit, persistence");
                Check(UnityEngine.Object.FindObjectsByType<RabbitKartAnimation>(FindObjectsSortMode.None).Length >= 4, "Corredores ausentes");
                Call(manager, "OnStart", 1);
                Check(state.IsRacing && players.Count == 1, "Corrida solo falhou");
                TouchState.Throttle = true;
            }
            if (frames == 300)
            {
                TouchState.Throttle = false;
                Check(director.Elapsed > 0 && director.Competitors[0].Kart.Telemetry.Speed > .1f, "Kart nao acelerou");
                state.TogglePause();
                Check(!state.Simulates, "Pausa falhou");
                Call(manager, "OnResume");
                Check(state.IsRacing, "Retomar falhou");
                Call(manager, "OnStart", 2);
                Check(players.Count == 2, "Modo dupla falhou");
                var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                int split = 0;
                foreach (var camera in cameras) if (camera.enabled && camera.rect.height < 1) split++;
                Check(split == 2, "Tela dividida falhou");
                var ui = UnityEngine.Object.FindFirstObjectByType<GameUI>();
                ui.ShowFinish(director.Competitors, 1, director.Competitors[0], Array.Empty<SeriesStanding>(), 1);
                Check(ui.FinishVisible, "Resultado ausente");
                Call(manager, "OnRestart");
                Check(!ui.FinishVisible && state.IsRacing, "Reiniciar falhou");
                Call(manager, "OnMenu");
                Check(state.Name == GameStateName.Menu, "Voltar ao menu falhou");
                Check(!failed, "Erros de runtime registrados");
                Debug.Log("INTEGRATION_FLOW_OK: menu, solo, acceleration, pause, resume, split-screen, results, restart, menu");
                Finish(0);
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    static void Finish(int code)
    {
        TouchState.Throttle = false;
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
