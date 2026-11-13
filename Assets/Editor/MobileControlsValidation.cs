using System;
using CandyRush.Integrated;
using UnityEngine;

public static class MobileControlsValidation
{
    public static void Run()
    {
        foreach (var area in new[] {
            new Rect(0, 0, 1280, 720), new Rect(80, 0, 2212, 1080),
            new Rect(96, 0, 2880, 1360), new Rect(48, 0, 1480, 672),
            new Rect(0, 0, 1600, 1200), new Rect(0, 0, 854, 480) })
        {
            var layout = MobileControlLayout.Create(area);
            var controls = new[] { layout.Joystick, layout.Brake, layout.Throttle, layout.Drift, layout.Turbo, layout.Meter, layout.Pause };
            for (int i = 0; i < controls.Length; i++)
            {
                var rect = controls[i];
                Check(rect.xMin >= area.xMin && rect.yMin >= area.yMin && rect.xMax <= area.xMax && rect.yMax <= area.yMax, "Controle fora da área segura");
                for (int j = i + 1; j < controls.Length; j++)
                    Check(!rect.Overlaps(controls[j]), "Controles sobrepostos");
            }
            Check(layout.Drift.height + .01f >= 64 * layout.Scale && layout.Turbo.height + .01f >= 64 * layout.Scale, "Ação pequena demais para toque");
        }
        Check(MobileControlLayout.ShapeStick(new Vector2(.05f, 0)) == Vector2.zero, "Toque acidental virou comando");
        Check(MobileControlLayout.ShapeStick(new Vector2(.1f, 0)) == Vector2.zero, "Joystick não voltou ao centro");
        Vector2 diagonal = MobileControlLayout.ShapeStick(new Vector2(-.5f, -.5f));
        Check(diagonal.x < 0 && diagonal.y < 0 && Mathf.Abs(diagonal.x - diagonal.y) < 1e-4f, "Diagonal foi perdida ou deslocada");
        Check(MobileControlLayout.ShapeStick(Vector2.up * 1.5f) == Vector2.up, "Força acima do curso não ficou no máximo");
        float weak = MobileControlLayout.ShapeStick(Vector2.right * .3f).magnitude;
        float strong = MobileControlLayout.ShapeStick(Vector2.right * .6f).magnitude;
        Check(weak > 0 && weak < strong && strong < 1, "Força do joystick não é proporcional");
        for (int degrees = 0; degrees < 360; degrees++)
        {
            float radians = degrees * Mathf.Deg2Rad;
            Vector2 raw = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * .7f;
            Vector2 result = MobileControlLayout.ShapeStick(raw);
            Check(result.magnitude <= 1 + 1e-4f, "Joystick passou do máximo");
            // Produto vetorial entre as direções: estável perto de 0°, ao contrário de Vector2.Angle.
            Vector2 a = result.normalized, b = raw.normalized;
            Check(Mathf.Abs(a.x * b.y - a.y * b.x) < 1e-4f && Vector2.Dot(a, b) > 0, "Direção do joystick foi desviada em " + degrees + "°");
        }
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Check(font != null, "Fonte interna ausente");
        foreach (char glyph in "ACELERAR FREAR DRIFT CONFIGURAÇÕES MÚSICA VOLTAR ON OFF")
            if (!char.IsWhiteSpace(glyph)) Check(font.HasCharacter(glyph), "Caractere ausente na fonte: " + glyph);
        CheckPointerCapture();
        CheckMusicToggle();
        CheckCoinAudio();
        CheckResultAudio();
        Debug.Log("MOBILE_CONTROLS_VALIDATION_PASSED: áreas seguras, drift e joystick");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void CheckPointerCapture()
    {
        var host = new GameObject("Validação de gestos");
        var ui = host.AddComponent<GameUI>();
        var settings=ScriptableObject.CreateInstance<FuelRaceSettings>();
        settings.autoAcceleration=true;
        ui.SetRaceSettings(settings);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(GameUI);
        var method = type.GetMethod("ReadControlPointer", flags);
        var throttle = (Rect)type.GetProperty("ThrottleRect", flags).GetValue(ui);
        var joystick = (Rect)type.GetProperty("JoystickRect", flags).GetValue(ui);
        try
        {
            method.Invoke(ui, new object[] { 10, throttle.center, false, true });
            Check(TouchState.Acceleration == 1, "Acelerador não respondeu");
            TouchState.Acceleration = 0;
            method.Invoke(ui, new object[] { 10, throttle.center + Vector2.up * throttle.height, false, false });
            Check(TouchState.Acceleration == 1, "Botão perdeu o dedo ao escorregar");
            // Solta o acelerador e zera o acelerador, para medir só o joystick.
            ((System.Collections.IDictionary)type.GetField("heldButtons", flags).GetValue(ui)).Remove(10);
            TouchState.Acceleration = 0;
            TouchState.Brake = false;
            // Diagonais só viram: os pedais são independentes do joystick.
            method.Invoke(ui, new object[] { 11, joystick.center + new Vector2(1, -1) * joystick.width * .1f, false, true });
            Check(TouchState.Steer < 0 && TouchState.Steer > -1, "Diagonal não virou à direita");
            Check(TouchState.Acceleration == 0 && !TouchState.Brake, "Joystick acionou um pedal");
            float steer = TouchState.Steer;
            method.Invoke(ui, new object[] { 11, joystick.center + new Vector2(1, 1) * joystick.width * .1f, false, false });
            Check(Mathf.Approximately(TouchState.Steer, steer), "Movimento vertical alterou a direção");
            Check(TouchState.Acceleration == 0 && !TouchState.Brake, "Joystick para baixo acionou um pedal");
            // Três dedos independentes: virar, acelerar e segurar drift no mesmo quadro.
            var drift=(Rect)type.GetProperty("DriftRect",flags).GetValue(ui);
            method.Invoke(ui,new object[]{12,throttle.center,false,true});
            method.Invoke(ui,new object[]{13,drift.center,false,true});
            Check(TouchState.Acceleration==1 && TouchState.DriftHeld && TouchState.Steer!=0,"Multitoque perdeu pedal, direção ou drift");
            type.GetMethod("OnApplicationFocus", flags).Invoke(ui, new object[] { false });
            Check(TouchState.Steer == 0 && TouchState.Acceleration == 0 && !TouchState.DriftHeld, "Comando preso após perder foco");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(settings); }
    }

    static void CheckMusicToggle()
    {
        const string key = "CandyRush.MusicEnabled";
        bool hadPreference = PlayerPrefs.HasKey(key);
        bool original = PlayerPrefs.GetInt(key, 1) != 0;
        var host = new GameObject("Validação de música");
        var music = host.AddComponent<CandyMusic>();
        music.Initialize();
        var source = host.GetComponent<AudioSource>();
        try
        {
            Check(host.GetComponent<AudioSource>() != null, "Fonte de música ausente");
            Check(music.Clip != null && music.Clip.samples > 0, "Candy Clouds não foi importada");
            Check(source.clip == music.Clip && source.loop && !source.playOnAwake && source.spatialBlend == 0,
                "Música da corrida configurada incorretamente");
            CandyMusic.SetMusicEnabled(false);
            Check(source.mute, "OFF não silenciou a música");
            Check(PlayerPrefs.GetInt(key, 1) == 0, "Preferência OFF não foi salva");
            CandyMusic.SetMusicEnabled(true);
            Check(CandyMusic.MusicEnabled && PlayerPrefs.GetInt(key, 0) == 1, "ON não reativou a música");
            Check(!source.mute, "ON não reativou a faixa");
        }
        finally
        {
            CandyMusic.SetMusicEnabled(original);
            if (!hadPreference) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    static void CheckCoinAudio()
    {
        var host = new GameObject("Validação do som da moeda");
        try
        {
            var pickup = host.AddComponent<CoinPickupSound>();
            pickup.Initialize();
            var source = host.GetComponent<AudioSource>();
            Check(pickup.Clip != null && pickup.Clip.samples > 0, "MP3 da moeda não foi importado");
            Check(!source.loop && !source.playOnAwake && source.spatialBlend == 0, "Efeito da moeda configurado incorretamente");
            Check(host.GetComponent<CandyMusic>() == null && host.GetComponent<AudioListener>() == null,
                "Efeito compartilha filtros da música");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    static void CheckResultAudio()
    {
        var host = new GameObject("Validação dos sons de resultado");
        try
        {
            var result = host.AddComponent<RaceResultSound>();
            result.Initialize();
            var victory = result.ClipForPosition(1);
            var defeat = result.ClipForPosition(4);
            Check(victory != null && victory.samples > 0, "Som de vitória ausente");
            Check(defeat != null && defeat.samples > 0 && defeat != victory, "Som de game over ausente");
            Check(result.ClipForPosition(0) == null, "Resultado indefinido acionou som");
            for (int position = 1; position <= 10; position++)
                Check(result.ClipForPosition(position) == (position <= 3 ? victory : defeat), "Som errado na posição " + position);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
