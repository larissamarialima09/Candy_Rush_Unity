using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class OriginalDesignValidation
{
    const string ResumeKey = "CandyRush.OriginalDesignValidation";
    static OriginalDesignValidation()
    {
        if (SessionState.GetBool(ResumeKey, false)) EditorApplication.delayCall += Run;
    }

    [MenuItem("CandyRush/Personagens/Conferir design original restaurado")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(ResumeKey, true);
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += Run;
            return;
        }
        SessionState.SetBool(ResumeKey, false);
        try
        {
            RabbitKartValidation.Run();
            FurAppearanceValidation.Run();
            File.WriteAllText("../original-design-validation.txt", "OK: quatro modelos originais, cores originais, materiais, animacoes, rodas, pausa e renderizacao.\n");
            Debug.Log("ORIGINAL_DESIGN_VALIDATION_OK");
        }
        catch (Exception error)
        {
            File.WriteAllText("../original-design-validation.txt", "FAILED: " + error);
            Debug.LogException(error);
        }
    }
}
