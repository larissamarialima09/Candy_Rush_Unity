using System;
using System.IO;
using System.Reflection;
using CandyRush.Integrated;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Teste curto da cena real; sai do Play sem fechar a Unity nem salvar a cena.</summary>
[InitializeOnLoad]
public static class FuelRaceSmokeCheck
{
    const string Request="Temp/CandyRushFuelSmoke.request",Status="Temp/CandyRushFuelSmoke.status.txt";
    const string Key="CandyRush.FuelSmoke",ErrorKey="CandyRush.FuelSmoke.Error",FramesKey="CandyRush.FuelSmoke.Frames";
    static FuelRaceSmokeCheck() {EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    static void Log(string message,string trace,LogType type)
    {
        if(!SessionState.GetBool(Key,false) || type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
        SessionState.SetString(ErrorKey,message+"\n"+trace);
    }
    static void Tick()
    {
        if(File.Exists(Request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(Request);
            if(SceneManager.GetActiveScene().path!="Assets/Scenes/Main.unity") {File.WriteAllText(Status,"SKIPPED: open Main scene.");return;}
            SessionState.SetBool(Key,true);SessionState.SetString(ErrorKey,"");SessionState.SetInt(FramesKey,0);
            File.WriteAllText(Status,"RUNNING");EditorApplication.EnterPlaymode();return;
        }
        if(!SessionState.GetBool(Key,false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        int frames=SessionState.GetInt(FramesKey,0);
        if(!EditorApplication.isPlaying)
        {
            if(frames<45 || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string error=SessionState.GetString(ErrorKey,"");
            File.WriteAllText(Status,string.IsNullOrEmpty(error)?"OK: Main, four racers, pit, Canvas, race start and 360 physics steps.":"FAILED: "+error);
            SessionState.SetBool(Key,false);return;
        }
        frames++;SessionState.SetInt(FramesKey,frames);
        try
        {
            if(frames==12)
            {
                var game=UnityEngine.Object.FindFirstObjectByType<GameManager>();
                if(game==null || game.pitStop==null || game.pitStop.pumps.Length==0) throw new Exception("Posto ausente na cena Main.");
                var racers=UnityEngine.Object.FindObjectsByType<KartRaceSystems>(FindObjectsSortMode.None);
                if(racers.Length!=4) throw new Exception("Esperados quatro pilotos ativos, encontrados "+racers.Length);
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(GameManager).GetMethod("OnStart",flags).Invoke(game,new object[]{1});
                var step=typeof(GameManager).GetMethod("FixedStep",flags);var args=new object[]{1f/60};
                for(int i=0;i<360;i++) step.Invoke(game,args);
                if(UnityEngine.Object.FindFirstObjectByType<FuelHUD>()==null) throw new Exception("HUD vertical ausente.");
                foreach(var racer in racers) if(racer.fuel.Current<0 || racer.fuel.Current>100) throw new Exception("Tanque fora do intervalo.");
            }
            if(frames==30) ScreenCapture.CaptureScreenshot(Path.GetFullPath("../fuel-pit-preview.png"));
            if(frames>=45) EditorApplication.isPlaying=false;
        }
        catch(Exception error)
        {SessionState.SetString(ErrorKey,error.ToString());SessionState.SetInt(FramesKey,45);EditorApplication.isPlaying=false;}
    }
}
