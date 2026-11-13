using System;
using System.Collections.Generic;
using System.IO;
using CandyRush.Integrated;
using UnityEditor;
using UnityEngine;

/// <summary>Testes do tanque e do posto na instância existente, sem abrir outro Editor.</summary>
[InitializeOnLoad]
public static class FuelGameplayValidation
{
    const string Request="Temp/CandyRushFuel.request",Status="Temp/CandyRushFuel.status.txt";
    static FuelGameplayValidation() {EditorApplication.delayCall+=EnsureSettings;EditorApplication.update+=Poll;}
    [MenuItem("CandyRush/Fuel/Create settings")]
    public static void EnsureSettings()
    {
        const string path="Assets/CandyRush/Resources/Candy/FuelRaceSettings.asset";
        if(File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var settings=ScriptableObject.CreateInstance<FuelRaceSettings>();
        AssetDatabase.CreateAsset(settings,path);AssetDatabase.SaveAssets();
    }
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(EditorApplication.isPlayingOrWillChangePlaymode) {EditorApplication.isPlaying=false;return;}
        File.Delete(Request);
        try {Run();File.WriteAllText(Status,"OK: fuel, drift, turbo, ramp, queue, one stop, reset, bot deadline and responsive controls.");}
        catch(Exception error) {File.WriteAllText(Status,"FAILED: "+error);Debug.LogException(error);}
    }
    static void Check(bool ok,string message) {if(!ok) throw new InvalidOperationException(message);}
    static void Near(float a,float b,string message) {Check(Mathf.Abs(a-b)<.015f,message+": "+a+" != "+b);}
    [MenuItem("CandyRush/Fuel/Validate gameplay")]
    public static void Run()
    {
        EnsureSettings();
        var tuning=ScriptableObject.CreateInstance<FuelRaceSettings>();
        var world=new GameObject("Validação combustível");world.transform.localScale=new Vector3(-1,1,1);
        try
        {
            var kart=new Kart(tuning);var fuel=kart.Fuel;
            int lows=0,empties=0,refills=0,ends=0;fuel.OnFuelLow+=()=>lows++;fuel.OnFuelEmpty+=()=>empties++;fuel.OnRefueled+=()=>refills++;
            kart.Drift.OnDriftEnd+=(level,time)=>ends++;
            fuel.Step(10,20);Near(fuel.Current,99,"Consumo por distância");fuel.ResetRace();
            kart.Drift.Update(.4f,10,15,true,true,.7f);Near(fuel.Current,100,"Drift cobrou antes da soltura");
            kart.Drift.Update(.01f,10,15,true,false,.7f);Near(fuel.Current,85,"Drift curto");Near(kart.Turbo.Charge,.1f,"Carga do drift curto");
            fuel.ResetRace();kart.Drift.Reset();
            for(int i=0;i<140;i++) kart.Drift.Update(1f/60,10,15,true,true,.7f);
            Check(kart.Drift.Level==2,"Drift longo não subiu de nível");
            kart.Drift.Update(.01f,10,15,true,false,.7f);Near(fuel.Current,80,"Drift longo");Near(kart.Turbo.Charge,.2f,"Carga do drift longo");
            Check(ends==2,"Drift cobrou mais de uma vez");
            fuel.ResetRace();kart.Drift.Reset();kart.Turbo.FillCharge(1);
            Check(kart.Turbo.TryActivate(),"Turbo cheio não ativou");Near(fuel.Current,70,"Custo turbo");
            fuel.ResetRace();kart.Drift.Reset();fuel.TrySpend(90,"teste");kart.Turbo.FillCharge(1);
            Check(!kart.Turbo.TryActivate() && kart.Turbo.Ready,"Turbo sem combustível perdeu carga");
            kart.Drift.Update(.1f,10,15,true,true,.8f);Check(!kart.Drift.Drifting,"Drift começou com 10%");
            fuel.TrySpend(10,"teste");fuel.Step(1,10);Check(fuel.IsEmpty && lows==1 && empties==1,"Eventos de baixo/vazio repetidos");
            fuel.BeginRefuel();fuel.SetRefuelProgress(1);Check(fuel.CompleteRefuel(),"Reabastecimento falhou");
            fuel.BeginRefuel();Check(!fuel.CompleteRefuel() && refills==1,"Abastecimento duplicado");fuel.ResetRace();kart.Drift.Reset();
            // O piso usa o amostrador próprio; não depende de colisores PhysX.
            var path=new CircuitPath(CircuitConfig.Centerline,CircuitConfig.Samples,CircuitConfig.CurvatureWindow);
            var ground=new TrackSurface(path);
            int trackIndex=path.IndexAtFraction(.1f);var sample=path.Samples[trackIndex];
            kart.RespawnAt(path.PointAt(trackIndex,0,.7f),Mathf.Atan2(sample.Tangent.x,sample.Tangent.z));
            fuel.TrySpend(100,"teste");var input=new InputState{Throttle=1,DriftHeld=true,TurboPressed=true,Steer=.4f};
            for(int i=0;i<360;i++) kart.Update(1f/60,input,ground);
            Check(kart.Telemetry.Speed>0,"Kart vazio ficou imóvel");
            Vector3 flat=kart.Body.Velocity;flat.y=0;
            Check(flat.magnitude<=KartConfig.Engine.PowerCutoffSpeed*tuning.emptySpeedFactor+.1f,"Teto de 30% não aplicado");
            Check(!kart.Drift.Drifting && !kart.Turbo.IsBoosting,"Ação ativa com tanque vazio");
            fuel.ResetRace();kart.Drift.Reset();
            var ramp=new SpeedRamp(path,world.transform);int rampIndex=path.IndexAtFraction(SpeedRampConfig.At);
            kart.RespawnAt(path.PointAt(rampIndex,SpeedRampConfig.Lateral,.7f),0);ramp.Collide(kart,3);
            Check(kart.Turbo.Ready,"Rampa não carregou turbo");
            // Quatro karts disputam uma bomba, com ordem e parada única verificadas.
            tuning.pumpCount=1;var pitGo=SceneKit.Node("Posto de teste",world.transform);
            var pit=pitGo.gameObject.AddComponent<CandyPitStop>();pit.Configure(path,tuning);
            var racers=new List<Competitor>();var completed=new List<int>();
            for(int i=0;i<4;i++)
            {
                int id=i;var k=new Kart(tuning);var tracker=new LapTracker(path);tracker.Lap=Mathf.Max(0,RaceConfig.Laps-2);
                k.RespawnAt(pit.EntryPoint-sample.Tangent*(i*3+2),0);
                k.Fuel.TrySpend(65,"teste");k.Fuel.OnRefueled+=()=>completed.Add(id);
                racers.Add(new Competitor{Kart=k,Tracker=tracker,IsPlayer=false});
            }
            pit.Bind(racers);
            foreach(var c in racers) Check(pit.TryEnter(c.Kart),"Entrada do posto recusada");
            for(int step=0;step<6000;step++) pit.Step(1f/30);
            Check(completed.Count==4,"Fila não terminou: "+completed.Count);
            foreach(var c in racers)
            {Check(c.Kart.Fuel.HasRefueled && !c.Kart.PitControlled,"Kart preso no posto");Near(c.Kart.Fuel.Current,100,"Tanque não completou");Check(!pit.TryEnter(c.Kart),"Segunda parada aceita");}
            for(int i=0;i<4;i++) Check(completed[i]==i,"Fila ultrapassada");
            pit.ResetRace();foreach(var c in racers) c.Kart.Fuel.ResetRace();
            for(int i=0;i<3;i++)
            {
                racers[i].Tracker.Lap=Mathf.Max(0,RaceConfig.Laps-2);
                var strategy=new BotPitStrategy(i);strategy.Update(.1f,racers[i],pit,new InputState());
                Check(pit.IsRequested(racers[i].Kart),"Bot ignorou limite da penúltima volta");
            }
            CheckLayouts();
            MobileControlsValidation.CheckPointerCapture();
            Debug.Log("FUEL_GAMEPLAY_VALIDATION_OK");
        }
        finally {UnityEngine.Object.DestroyImmediate(world);UnityEngine.Object.DestroyImmediate(tuning);}
    }
    static void CheckLayouts()
    {
        foreach(var size in new[]{new Vector2(960,540),new Vector2(1920,1080),new Vector2(1280,720),new Vector2(2400,1080)})
        {
            var area=new Rect(20,10,size.x-40,size.y-20);var layout=MobileControlLayout.Create(area);
            var controls=new[]{layout.Joystick,layout.Brake,layout.Throttle,layout.Drift,layout.Turbo,layout.Meter};
            for(int i=0;i<controls.Length;i++)
            {
                Check(controls[i].xMin>=area.xMin && controls[i].yMin>=area.yMin && controls[i].xMax<=area.xMax && controls[i].yMax<=area.yMax,"Controle fora da área segura");
                for(int j=i+1;j<controls.Length;j++) Check(!controls[i].Overlaps(controls[j]),"Controles sobrepostos");
            }
            Check(!layout.Drift.Overlaps(layout.Turbo),"Drift e turbo sobrepostos");
            Check(area.Contains(layout.Turbo.min) && area.Contains(layout.Drift.max),"Botão fora da área segura");
            Check(!layout.Joystick.Overlaps(layout.Drift),"Multitoque sem área independente");
        }
    }
}
