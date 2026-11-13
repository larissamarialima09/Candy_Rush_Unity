using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RabbitKartAnimation : MonoBehaviour
{
    [Header("Animacao")]
    [Min(0.01f)] public float suavidade = 10f;
    [Min(0f)] public float molaOrelhas = 80f;
    [Min(0f)] public float amortecimentoOrelhas = 12f;
    [Min(0f)] public float respostaAceleracao = 0.8f;
    [Min(0f)] public float amplitudeQuique = 0.012f;
    private readonly string[] nomes = {
        "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR",
        "Steer_FL", "Steer_FR", "Ear_L", "Ear_R", "Head", "Arm_L", "Arm_R", "Rabbit"
    };
    private Transform[] partes;
    private Quaternion[] rotacoes;
    private Vector3 posicaoCorpo;
    private float velocidadeEntrada, volanteEntrada, velocidadeAnterior;
    private float volanteSuave, inclinacaoCabeca, fase, quique;
    private float orelha, velocidadeOrelha;
    private bool primeiraAmostra = true;

    private void Awake()
    {
        Inicializar();

    }

    private void Inicializar()
    {
        if (partes != null) return;
        partes = new Transform[nomes.Length];
        rotacoes = new Quaternion[nomes.Length];
        foreach (Transform filho in GetComponentsInChildren<Transform>(true))
        {
            int i = Array.IndexOf(nomes, filho.name);
            if (i < 0) continue;
            partes[i] = filho;
            rotacoes[i] = filho.localRotation;
        }
        if (partes[11] != null) posicaoCorpo = partes[11].localPosition;
    }

    // Velocidade em metros/segundo, com sinal; volante entre -1 e 1.
    // Rodas e suspensão são atualizadas diretamente pelo KartView.
    public void SetInput(float velocidade, float volante)
    {
        velocidadeEntrada = Finito(velocidade) ? velocidade : 0f;
        volanteEntrada = Finito(volante) ? Mathf.Clamp(volante, -1f, 1f) : 0f;
    }

    private static bool Finito(float valor) => !float.IsNaN(valor) && !float.IsInfinity(valor);

    public void Animate(float dt)
    {
        Inicializar();
        if (dt <= 0f) return;
        float velocidade = velocidadeEntrada;
        float aceleracao = primeiraAmostra ? 0f : (velocidade - velocidadeAnterior) / dt;
        primeiraAmostra = false;
        velocidadeAnterior = velocidade;
        float mistura = 1f - Mathf.Exp(-Mathf.Max(0.01f, suavidade) * dt);
        volanteSuave = Mathf.Lerp(volanteSuave, volanteEntrada, mistura);
        // Passos curtos mantem a mola estavel mesmo com quedas de frame.
        float restante = Mathf.Min(dt, 0.1f);
        float alvo = Mathf.Clamp(-aceleracao * respostaAceleracao, -25f, 25f);
        while (restante > 0f)
        {
            float passo = Mathf.Min(restante, 1f / 120f);
            velocidadeOrelha += ((alvo - orelha) * Mathf.Clamp(molaOrelhas, 0f, 500f)
                - velocidadeOrelha * Mathf.Clamp(amortecimentoOrelhas, 0f, 60f)) * passo;
            orelha = Mathf.Clamp(orelha + velocidadeOrelha * passo, -35f, 35f);
            restante -= passo;
        }
        Girar(6, orelha, 0f, 0f);
        Girar(7, orelha, 0f, 0f);
        inclinacaoCabeca = Mathf.Lerp(inclinacaoCabeca,
            -volanteSuave * Mathf.Clamp(velocidade / 8f, -1f, 1f) * 12f, mistura);
        Girar(8, 0f, 0f, inclinacaoCabeca);
        Girar(9, volanteSuave * 12f, 0f, -volanteSuave * 8f);
        Girar(10, -volanteSuave * 12f, 0f, -volanteSuave * 8f);
        fase = Mathf.Repeat(fase + Mathf.Abs(velocidade) * dt * 4f, Mathf.PI * 2f);
        quique = Mathf.Lerp(quique, amplitudeQuique * Mathf.Clamp01(Mathf.Abs(velocidade) / 3f), mistura);
        if (partes[11] != null)
            partes[11].localPosition = posicaoCorpo + Vector3.up * (Mathf.Sin(fase) * quique);
    }

    private void Girar(int indice, float x, float y, float z)
    {
        if (partes[indice] != null)
            partes[indice].localRotation = rotacoes[indice] * Quaternion.Euler(x, y, z);
    }

    private void OnDisable()
    {
        if (partes == null) return;
        for (int i = 0; i < partes.Length; i++)
            if (partes[i] != null) partes[i].localRotation = rotacoes[i];
        if (partes[11] != null) partes[11].localPosition = posicaoCorpo;

        orelha = velocidadeOrelha = volanteSuave = inclinacaoCabeca = fase = quique = 0f;
        primeiraAmostra = true;
    }
}
