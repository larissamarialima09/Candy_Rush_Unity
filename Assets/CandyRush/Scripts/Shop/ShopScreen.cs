using System.Collections.Generic;
using System.Globalization;
using CandyRush.Integrated;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Loja doce (Unity UI), aberta pelo botão LOJA do menu inicial.
///
/// Montada em código, como o MenuCandyRush, com coordenadas da arte de
/// referência (1672 x 941, origem no canto superior esquerdo).
/// Abas: CAPACETES, KARTS, RODAS e EFEITOS. Abas sem itens somem (ver ShopCatalog).
///
/// O coelho aparece no palco 3D (ShopPreviewStage): gira sozinho, gira com o dedo
/// e mostra o item testado antes da compra. COMPRAR e EQUIPAR gravam em ShopProfile.
/// VOLTAR ou Esc fecham a loja. O banner OFERTA DO DIA abre a escolha de coelho.
/// </summary>
public class ShopScreen : MonoBehaviour
{
    static readonly Vector2 Ref = new Vector2(1672f, 941f);
    static readonly Color Tinta = Hex.C(0x42216e);
    static readonly Color Lilas = Hex.C(0x8b3caa);
    static readonly CultureInfo Pt = new CultureInfo("pt-BR");

    // Painel da grade: cartões de 310 x 214 em 3 colunas, com 22 de espaço.
    const float PainelX = 610, PainelY = 246, PainelW = 1030, PainelH = 506;
    const float CartaoW = 310, CartaoH = 214, Folga = 22;

    /// <summary>Banner OFERTA DO DIA: recebe o índice do coelho em oferta. A loja já está fechada.</summary>
    public UnityEvent<int> aoOferta = new UnityEvent<int>();
    public UnityEvent aoCorrer = new UnityEvent();

    sealed class Cartao
    {
        public ShopItem Item;
        public Button Botao;
        public Image Moldura, Fundo, Icone;
        public GameObject Novo, Equipado, Cadeado, Preco, Posse, Requisito;
    }

    readonly List<Cartao> cartoes = new List<Cartao>();
    readonly List<Image> abas = new List<Image>();
    readonly List<Text> textosAbas = new List<Text>();
    List<int> abasVisiveis;
    Sprite abaAtiva, abaInativa, pilulaComprar, pilulaComprarOff, pilulaPequena, pilulaPequenaOff;
    Sprite pilulaTestar, pilulaEquipar, pilulaCinza230, pilulaRequisito;
    RectTransform palco, painel;
    Text moedas, vazio, rotuloComprar, rotuloEquipar, rotuloTestar, rotuloOferta, nomePrevia, estadoPrevia;
    static Sprite superficie;
    Image imagemComprar, imagemEquipar;
    GameObject moedaComprar;
    BotaoDoce bComprar, bEquipar, bTestar;
    ShopPreviewStage palcoDePrevia;
    string montagemAtual;
    GameObject carteira;
    Text saldoCarteira;

    int aba = ShopCatalog.AbaCapacetes;
    ShopItem selecionado;
    readonly Dictionary<ShopCategoria, ShopItem> provados = new Dictionary<ShopCategoria, ShopItem>();

    void Awake()
    {
        UiArt.Build();
        palcoDePrevia = new ShopPreviewStage();
        Montar();
    }

    void OnEnable()
    {
        GarantirEntrada();
        transform.SetAsLastSibling();
        ShopProfile.Changed += Atualizar;
        aba = abasVisiveis.Contains(ShopCatalog.AbaCapacetes) ? ShopCatalog.AbaCapacetes : abasVisiveis[0];
        provados.Clear();
        selecionado = PadraoDaAba(aba);
        // Ligar o palco antes de montar o coelho: a medição do kart usa os renderers ativos.
        palcoDePrevia.Ativar(true);
        montagemAtual = null;
        CriarGrade();
        Atualizar();
    }

    void OnDisable()
    {
        ShopProfile.Changed -= Atualizar;
        if (carteira != null) carteira.SetActive(false);
        if (palcoDePrevia != null) palcoDePrevia.Ativar(false);
    }

    void OnDestroy()
    {
        if (palcoDePrevia != null) palcoDePrevia.Dispose();
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Voltar();
#else
        if (Input.GetKeyDown(KeyCode.Escape)) Voltar();
#endif
        palcoDePrevia.Atualizar(Time.unscaledDeltaTime);
    }

    // ============================ Montagem ============================

    void Montar()
    {
        var raiz = (RectTransform)transform;
        Esticar(raiz);
        // A loja recebe eventos acima do menu e de qualquer véu da sua animação de entrada.
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        gameObject.AddComponent<GraphicRaycaster>();
        var grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.ignoreParentGroups = true;
        grupo.interactable = grupo.blocksRaycasts = true;

        // Fundo: o mesmo pôr do sol do menu, com véu rosa para dar cor de pista.
        var fundo = NovaImagem("Fundo", raiz, Fundo(), true);
        Esticar(fundo.rectTransform);
        var fit = fundo.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = Ref.x / Ref.y;
        var veu = NovaImagem("Veu", raiz, null, false);
        Esticar(veu.rectTransform);
        veu.color = new Color(.19f, .10f, .29f, .58f);

        // O palco preenche a tela sem distorcer, como no menu.
        palco = NovoRect("Palco", raiz);
        palco.sizeDelta = Ref;
        var palcoFit = palco.gameObject.AddComponent<AspectRatioFitter>();
        palcoFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        palcoFit.aspectRatio = Ref.x / Ref.y;

        abasVisiveis = ShopCatalog.AbasVisiveis();
        PrepararSprites();
        MontarCabecalho();
        MontarAbas();
        MontarPainel();
        MontarPreview();
        MontarRodape();
        MontarCarteira();
    }

    Sprite Fundo()
    {
        var tex = Resources.Load<Texture2D>("Candy/Menu/SunsetBackdrop") ?? Resources.Load<Texture2D>("Candy/Menu/Backdrop");
        return tex != null ? ShopArt.ParaSprite(tex) : ShopArt.ParaSprite(UiArt.MenuSky);
    }

    void PrepararSprites()
    {
        abaAtiva = ShopArt.ParaSprite(UiArt.MenuPill(230, 74, "#ff80b8", "#f50079"));
        abaInativa = ShopArt.ParaSprite(UiArt.MenuPill(230, 74, "#f3ecff", "#dcc6fa"));
        pilulaComprar = ShopArt.ParaSprite(UiArt.MenuPill(520, 100, "#ff80b8", "#f50079"));
        pilulaComprarOff = ShopArt.ParaSprite(UiArt.MenuPill(520, 100, "#d7cfe0", "#b7abc9"));
        pilulaEquipar = ShopArt.ParaSprite(UiArt.MenuPill(230, 100, "#ff80b8", "#f50079"));
        pilulaTestar = ShopArt.ParaSprite(UiArt.MenuPill(230, 100, "#bd8aeb", "#8133b0"));
        pilulaCinza230 = ShopArt.ParaSprite(UiArt.MenuPill(230, 100, "#d7cfe0", "#b7abc9"));
        pilulaPequena = ShopArt.ParaSprite(UiArt.MenuPill(170, 32, "#ff80b8", "#f50079"));
        pilulaPequenaOff = ShopArt.ParaSprite(UiArt.MenuPill(170, 32, "#e9e1f2", "#c7b5dd"));
        pilulaRequisito = ShopArt.ParaSprite(UiArt.MenuPill(282, 32, "#e9e1f2", "#c7b5dd"));
    }

    void MontarCabecalho()
    {
        var voltar = Pilula("Voltar", palco, 40, 36, 230, 84, ShopArt.ParaSprite(UiArt.MenuPill(230, 84, "#bd8aeb", "#8133b0")), Fechar);
        Imagem(Caixa("Seta", voltar.transform, 22, 20, 44, 44), ShopArt.ParaSprite(UiArt.BackIcon));
        Rotulo(Caixa("Texto", voltar.transform, 70, 0, 150, 84), "VOLTAR", 36, Color.white);

        Rotulo(Caixa("Colecao", palco, 576, 24, 520, 28), "CANDY RUSH  /  COLEÇÃO DOCE", 21, Hex.C(0xffdfaa));
        Rotulo(Caixa("Titulo", palco, 516, 55, 640, 82), "LOJA DOCE", 68, Color.white);
        Rotulo(Caixa("Subtitulo", palco, 516, 136, 640, 32), "Um toque de açúcar. Um estilo só seu.", 23, Hex.C(0xffe4f1));

        var caixaMoedas = Imagem(Caixa("Moedas", palco, 1330, 36, 310, 92), ShopArt.ParaSprite(UiArt.MenuPill(310, 92, "#fffaf5", "#ffe0eb")));
        Imagem(Caixa("Moeda", caixaMoedas.transform, 12, 8, 76, 76), ShopArt.ParaSprite(UiArt.CoinDot));
        moedas = Rotulo(Caixa("Valor", caixaMoedas.transform, 92, 14, 150, 64), "", 48, Tinta, TextAnchor.MiddleLeft);

        var mais = Pilula("Mais", palco, 1560, 47, 70, 70, ShopArt.ParaSprite(UiArt.MenuPill(70, 70, "#ff80b8", "#f50079")),
            AbrirCarteira);
        Rotulo(Filho(mais.transform), "+", 52, Color.white);
    }

    void MontarCarteira()
    {
        var bloqueio = NovaImagem("Carteira", (RectTransform)transform, null, true);
        Esticar(bloqueio.rectTransform);
        bloqueio.color = new Color(.10f, .03f, .18f, .88f);
        carteira = bloqueio.gameObject;
        var centro = NovoRect("Conteudo", bloqueio.transform);
        centro.sizeDelta = new Vector2(780, 430);
        Superficie("Painel", centro, 0, 0, 780, 430, Hex.C(0xfff2f9));
        Rotulo(Caixa("Titulo", centro, 40, 28, 700, 62), "SUAS MOEDAS DOCES", 42, Tinta);
        saldoCarteira = Rotulo(Caixa("Saldo", centro, 40, 100, 700, 65), "", 42, Lilas);
        Rotulo(Caixa("Como ganhar", centro, 65, 177, 650, 90),
            "Colete moedas na pista para comprar acessórios, pinturas e aros. Cada moeda vai para sua carteira!", 28, Tinta);
        var correr = Pilula("Correr", centro, 70, 302, 410, 82, pilulaComprar, () =>
        {
            Fechar();
            aoCorrer.Invoke();
        });
        Rotulo(Filho(correr.transform), "CORRER E GANHAR", 30, Color.white);
        var fechar = Pilula("Fechar", centro, 500, 302, 210, 82, pilulaTestar, () => carteira.SetActive(false));
        Rotulo(Filho(fechar.transform), "VOLTAR", 30, Color.white);
        carteira.SetActive(false);
    }

    void AbrirCarteira()
    {
        MenuClickSound.Play();
        saldoCarteira.text = ShopProfile.Coins.ToString("N0", Pt) + " MOEDAS";
        carteira.SetActive(true);
        carteira.transform.SetAsLastSibling();
    }

    void Voltar()
    {
        if (carteira.activeSelf) carteira.SetActive(false);
        else Fechar();
    }

    static void GarantirEntrada()
    {
        var eventos = EventSystem.current;
        if (eventos == null) eventos = new GameObject("EventSystem").AddComponent<EventSystem>();
        foreach (var modulo in eventos.GetComponents<BaseInputModule>())
            if (modulo.isActiveAndEnabled) return;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var entrada = eventos.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        entrada.AssignDefaultActions();
#else
        eventos.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }

    void MontarAbas()
    {
        int n = abasVisiveis.Count;
        const float largura = 230, folga = 22, y = 178, altura = 54;
        float x0 = PainelX + (PainelW - (n * largura + (n - 1) * folga)) / 2f;
        for (int i = 0; i < n; i++)
        {
            int categoria = abasVisiveis[i];
            var rt = Caixa("Aba " + ShopCatalog.Abas[categoria], palco, x0 + i * (largura + folga), y, largura, altura);
            var img = Imagem(rt, abaInativa, true);
            var botao = rt.gameObject.AddComponent<Button>();
            botao.targetGraphic = img;
            botao.transition = Selectable.Transition.None;
            botao.onClick.AddListener(() => TrocarAba(categoria));
            abas.Add(img);
            textosAbas.Add(Rotulo(Filho(rt), ShopCatalog.Abas[categoria], 25, Lilas));
        }
    }

    void MontarPainel()
    {
        var img = Superficie("Painel", palco, PainelX, PainelY, PainelW, PainelH, Hex.C(0xf6edf8));
        painel = img.rectTransform;

        vazio = Rotulo(Caixa("Vazio", painel, 0, 0, PainelW, PainelH), "EM BREVE", 60, Lilas);
        vazio.gameObject.SetActive(false);
    }

    void MontarPreview()
    {
        Superficie("Vitrine", palco, 40, 194, 520, 558, new Color(.20f,.12f,.30f,.92f));
        Rotulo(Caixa("Seu piloto", palco, 68, 212, 464, 30), "SEU PILOTO", 21, Hex.C(0xffd58e), TextAnchor.MiddleLeft);
        // Prévia 3D: o palco desenha numa RenderTexture e este RawImage mostra o resultado.
        // Arrastar o dedo sobre ele gira o coelho.
        var previa = Caixa("Previa", palco, 56, 248, 488, 388).gameObject.AddComponent<RawImage>();
        previa.texture = palcoDePrevia.Textura;
        previa.raycastTarget = true;
        previa.gameObject.AddComponent<ArrastoPrevia>().alvo = palcoDePrevia;
        nomePrevia = Rotulo(Caixa("Item selecionado", palco, 64, 636, 472, 40), "", 28, Color.white);
        estadoPrevia = Rotulo(Caixa("Estado da previa", palco, 64, 678, 472, 28), "", 19, Hex.C(0xffd58e));
        Rotulo(Caixa("Arraste", palco, 64, 714, 472, 24), "ARRASTE PARA GIRAR  ·  PRÉVIA 3D", 16, Hex.C(0xd4b9e3));
    }

    void MontarRodape()
    {
        const float y = 776, altura = 100;
        bTestar = Pilula("Testar", palco, 60, y, 230, altura, pilulaTestar, Testar);
        rotuloTestar = Rotulo(Filho(bTestar.transform), "TESTAR", 36, Color.white);

        bEquipar = Pilula("Equipar", palco, 310, y, 230, altura, pilulaEquipar, Equipar);
        imagemEquipar = bEquipar.GetComponent<Image>();
        rotuloEquipar = Rotulo(Caixa("Texto", bEquipar.transform, 16, 0, 198, altura), "", 36, Color.white);

        bComprar = Pilula("Comprar", palco, 610, y, 520, altura, pilulaComprar, Comprar);
        imagemComprar = bComprar.GetComponent<Image>();
        moedaComprar = Imagem(Caixa("Moeda", bComprar.transform, 110, 18, 64, 64), ShopArt.ParaSprite(UiArt.CoinDot)).gameObject;
        rotuloComprar = Rotulo(Caixa("Texto", bComprar.transform, 190, 0, 300, altura), "", 40, Color.white, TextAnchor.MiddleLeft);

        // Banner OFERTA DO DIA: toca para abrir a escolha de coelho com a oferta em foco.
        var oferta = Pilula("Oferta", palco, 1160, y, 480, altura, ShopArt.ParaSprite(UiArt.MenuPill(480, 100, "#b889dc", "#8b3caa")), Oferta);
        Imagem(Caixa("Moeda", oferta.transform, 24, 18, 64, 64), ShopArt.ParaSprite(UiArt.CoinDot));
        Rotulo(Caixa("Titulo", oferta.transform, 104, 10, 360, 36), "OFERTA DO DIA:", 24, Hex.C(0xffe066), TextAnchor.MiddleLeft);
        rotuloOferta = Rotulo(Caixa("Texto", oferta.transform, 104, 46, 360, 44), "", 30, Color.white, TextAnchor.MiddleLeft);
        rotuloOferta.text = "coelho " + RacerProfile.RabbitNames[RabbitCatalog.Oferta] + " por "
            + RabbitCatalog.Precos[RabbitCatalog.Oferta].ToString("N0", Pt);
        Rotulo(Caixa("Dica", palco, 610, 890, 1030, 28), "ESCOLHA UM ITEM  ·  EXPERIMENTE NO SEU KART  ·  CORRA COM SEU ESTILO", 18, Hex.C(0xffe4f1));
    }

    // ============================ Grade ============================

    /// <summary>Recria os cartões da aba atual. Chamado ao abrir a loja e ao trocar de aba.</summary>
    void CriarGrade()
    {
        foreach (var c in cartoes)
        {
            c.Botao.gameObject.SetActive(false);
            c.Moldura.transform.SetParent(null);
            c.Botao.transform.SetParent(null);
            VisualMaterialLibrary.Dispose(c.Moldura.gameObject);
            VisualMaterialLibrary.Dispose(c.Botao.gameObject);
        }
        cartoes.Clear();

        var itens = ShopCatalog.Itens(aba);
        vazio.gameObject.SetActive(itens.Length == 0);
        for (int i = 0; i < itens.Length; i++) cartoes.Add(NovoCartao(itens[i], i));
    }

    Cartao NovoCartao(ShopItem item, int indice)
    {
        float x = 28 + (indice % 3) * (CartaoW + Folga), y = 22 + (indice / 3) * (CartaoH + Folga);
        var c = new Cartao { Item = item };

        c.Moldura = Imagem(Caixa("Moldura", painel, x - 8, y - 8, CartaoW + 16, CartaoH + 16), ShopArt.ParaSprite(ShopArt.Moldura, new Vector4(46, 46, 46, 46)));
        c.Moldura.type = Image.Type.Sliced;
        var rt = Caixa("Cartao " + item.Id, painel, x, y, CartaoW, CartaoH);
        c.Fundo = Imagem(rt, SuperficieSprite(), true);
        c.Fundo.type = Image.Type.Sliced;
        c.Botao = rt.gameObject.AddComponent<Button>();
        c.Botao.targetGraphic = c.Fundo;
        c.Botao.transition = Selectable.Transition.None;
        c.Botao.onClick.AddListener(() => Selecionar(item));

        // Capacetes usam o desenho 2D; pinturas e aros usam a amostra de cor. Ícone do asset vence.
        Sprite icone = item.Icone;
        if (icone == null)
        {
            bool amostra = item.Categoria == ShopCategoria.Kart || item.Categoria == ShopCategoria.Roda;
            icone = ShopArt.ParaSprite(amostra ? ShopArt.Amostra(item) : ShopArt.Icone(item.Id));
        }
        var halo = Superficie("Fundo do ícone", rt, 79, 12, 152, 120, Hex.C(0xf6edf8));
        c.Icone = Imagem(Caixa("Icone", rt, 67, 6, 176, 132), icone);
        c.Icone.preserveAspect = true;
        Rotulo(Caixa("Nome", rt, 12, 138, CartaoW - 24, 34), item.Nome.ToUpperInvariant(), 22, Tinta);

        // À VENDA: preço em moedas.
        var preco = Imagem(Caixa("Preco", rt, 70, 176, 170, 32), pilulaPequena);
        Imagem(Caixa("Moeda", preco.transform, 6, 2, 28, 28), ShopArt.ParaSprite(UiArt.CoinDot));
        Rotulo(Caixa("Valor", preco.transform, 38, 0, 124, 32), item.Preco.ToString("N0", Pt), 22, Color.white, TextAnchor.MiddleLeft);
        c.Preco = preco.gameObject;

        // BLOQUEADO: o requisito no lugar do preço.
        var requisito = Imagem(Caixa("Requisito", rt, 14, 176, 282, 32), pilulaRequisito);
        Rotulo(Filho(requisito.transform), item.Requisito ?? "BLOQUEADO", 18, Lilas);
        c.Requisito = requisito.gameObject;

        // COMPRADO: check e sem preço.
        var posse = Imagem(Caixa("Posse", rt, 70, 176, 170, 32), pilulaPequenaOff);
        Imagem(Caixa("Check", posse.transform, 6, 2, 28, 28), ShopArt.ParaSprite(ShopArt.Check));
        Rotulo(Caixa("Texto", posse.transform, 38, 0, 128, 32), "COMPRADO", 20, Lilas, TextAnchor.MiddleLeft);
        c.Posse = posse.gameObject;

        var novo = Imagem(Caixa("Novo", rt, 8, 8, 110, 38), ShopArt.ParaSprite(UiArt.MenuPill(110, 38, "#ff80b8", "#f50079")));
        Rotulo(Filho(novo.transform), "NOVO!", 20, Color.white);
        c.Novo = novo.gameObject;

        // EQUIPADO: etiqueta verde.
        var equipado = Imagem(Caixa("Equipado", rt, 8, 8, 160, 38), ShopArt.ParaSprite(UiArt.MenuPill(160, 38, "#b8f5c8", "#3ec47a")));
        Imagem(Caixa("Check", equipado.transform, 8, 5, 28, 28), ShopArt.ParaSprite(ShopArt.Check));
        Rotulo(Caixa("Texto", equipado.transform, 38, 0, 118, 38), "EQUIPADO", 19, Hex.C(0x1d6b3c));
        c.Equipado = equipado.gameObject;

        var cadeado = Imagem(Caixa("Cadeado", rt, 262, 8, 40, 48), ShopArt.ParaSprite(ShopArt.Cadeado));
        c.Cadeado = cadeado.gameObject;

        return c;
    }

    void AtualizarCartao(Cartao c)
    {
        var item = c.Item;
        bool posse = ShopProfile.Owns(item);
        bool equipado = ShopProfile.EstaEquipado(item, RacerProfile.Rabbit);
        bool trancado = item.Bloqueado && !posse;

        c.Moldura.enabled = item == selecionado;
        Color tom = trancado ? Hex.C(0xe0d9e8) : item == selecionado ? Hex.C(0xfff2f9) : Color.white;
        c.Fundo.color = tom;
        c.Icone.color = trancado ? new Color(.5f, .5f, .56f) : Color.white;
        c.Cadeado.SetActive(trancado);
        c.Requisito.SetActive(trancado);
        c.Novo.SetActive(item.Novo && !posse);
        c.Equipado.SetActive(equipado);
        c.Preco.SetActive(!posse && !trancado);
        c.Posse.SetActive(posse && !equipado);
    }

    // ============================ Estado ============================

    void Atualizar()
    {
        moedas.text = ShopProfile.Coins.ToString("N0", Pt);
        nomePrevia.text = selecionado != null ? selecionado.Nome : "Seu kart doce";
        estadoPrevia.text = selecionado == null ? "ESCOLHA UM ITEM"
            : selecionado.Bloqueado && !ShopProfile.Owns(selecionado) ? selecionado.Requisito
            : provados.Count > 0 ? "EXPERIMENTANDO " + provados.Count + (provados.Count == 1 ? " ITEM" : " ITENS") + " · SEM GASTAR MOEDAS"
            : ShopProfile.EstaEquipado(selecionado, RacerProfile.Rabbit) ? "EQUIPADO NO SEU PILOTO"
            : "TOQUE EM TESTAR PARA EXPERIMENTAR";
        for (int i = 0; i < abas.Count; i++)
        {
            bool ativa = abasVisiveis[i] == aba;
            abas[i].sprite = ativa ? abaAtiva : abaInativa;
            textosAbas[i].color = ativa ? Color.white : Lilas;
        }
        foreach (var c in cartoes) AtualizarCartao(c);
        AtualizarBotoes();
        AtualizarPrevia();
        if (saldoCarteira != null) saldoCarteira.text = ShopProfile.Coins.ToString("N0", Pt) + " MOEDAS";
    }

    /// <summary>Cada botão do rodapé diz o motivo quando está desligado.</summary>
    void AtualizarBotoes()
    {
        var item = selecionado;
        if (item == null)
        {
            Estado(bComprar, imagemComprar, rotuloComprar, false, "EM BREVE", pilulaComprar, pilulaComprarOff);
            moedaComprar.SetActive(false);
            Estado(bEquipar, imagemEquipar, rotuloEquipar, false, "EM BREVE", pilulaEquipar, pilulaCinza230);
            Estado(bTestar, bTestar.GetComponent<Image>(), rotuloTestar, false, "EM BREVE", pilulaTestar, pilulaCinza230);
            return;
        }

        bool posse = ShopProfile.Owns(item);
        bool equipado = ShopProfile.EstaEquipado(item, RacerProfile.Rabbit);

        // COMPRAR
        string rotulo; bool pode;
        if (posse) { rotulo = "COMPRADO"; pode = false; }
        else if (item.Bloqueado) { rotulo = item.Requisito ?? "BLOQUEADO"; pode = false; }
        else if (ShopProfile.Coins < item.Preco) { rotulo = "FALTAM " + (item.Preco - ShopProfile.Coins).ToString("N0", Pt) + " MOEDAS"; pode = false; }
        else { rotulo = "COMPRAR (" + item.Preco.ToString("N0", Pt) + ")"; pode = true; }
        Estado(bComprar, imagemComprar, rotuloComprar, pode, rotulo, pilulaComprar, pilulaComprarOff);
        moedaComprar.SetActive(pode);

        // EQUIPAR
        string rotuloEq; bool podeEq;
        if (equipado) { rotuloEq = "EQUIPADO"; podeEq = false; }
        else if (!posse) { rotuloEq = "COMPRE PARA EQUIPAR"; podeEq = false; }
        else { rotuloEq = "EQUIPAR"; podeEq = true; }
        Estado(bEquipar, imagemEquipar, rotuloEquipar, podeEq, rotuloEq, pilulaEquipar, pilulaCinza230);

        // TESTAR: vale para item livre, mesmo sem comprar.
        bool podeTestar = !item.Bloqueado && (!equipado || provados.ContainsKey(item.Categoria));
        string teste = item.Bloqueado ? "BLOQUEADO" : EmTeste(item) ? "RESTAURAR" : podeTestar ? "TESTAR" : "EM USO";
        Estado(bTestar, bTestar.GetComponent<Image>(), rotuloTestar, podeTestar, teste, pilulaTestar, pilulaCinza230);
    }

    void Estado(BotaoDoce b, Image img, Text texto, bool ativo, string rotulo, Sprite on, Sprite off)
    {
        b.enabled = ativo;
        b.GetComponent<Button>().interactable = ativo;
        img.sprite = ativo ? on : off;
        texto.text = rotulo;
        if (!ativo) b.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// O palco mostra os itens equipados do coelho. O item testado troca o da sua
    /// categoria. Só remonta o coelho quando a lista de itens muda.
    /// </summary>
    void AtualizarPrevia()
    {
        int coelho = RacerProfile.Rabbit;
        var lista = new List<ShopItem>(ShopProfile.EquipadosEm(coelho));
        foreach (var provado in provados.Values)
        {
            lista.RemoveAll(i => i.Categoria == provado.Categoria);
            lista.Add(provado);
        }
        string chave = coelho + ":" + string.Join(",", lista.ConvertAll(i => i.Id));
        if (chave == montagemAtual) return;
        montagemAtual = chave;
        palcoDePrevia.Montar(coelho, lista);
    }

    // ============================ Ações ============================

    /// <summary>Item equipado da aba; sem escolha, o primeiro da aba. Null se a aba não tem itens.</summary>
    ShopItem PadraoDaAba(int categoria)
    {
        var equipado = ShopProfile.Equipado((ShopCategoria)categoria, RacerProfile.Rabbit);
        if (equipado != null) return equipado;
        var itens = ShopCatalog.Itens(categoria);
        return itens.Length > 0 ? itens[0] : null;
    }

    void TrocarAba(int nova)
    {
        if (nova == aba) return;
        MenuClickSound.Play();
        aba = nova;
        selecionado = PadraoDaAba(aba);
        CriarGrade();
        Atualizar();
    }

    void Selecionar(ShopItem item)
    {
        MenuClickSound.Play();
        selecionado = item;
        Atualizar();
    }

    void Testar()
    {
        if (selecionado == null || selecionado.Bloqueado) return;
        MenuClickSound.Play();
        if (EmTeste(selecionado) || ShopProfile.EstaEquipado(selecionado, RacerProfile.Rabbit))
            provados.Remove(selecionado.Categoria);
        else provados[selecionado.Categoria] = selecionado;
        Atualizar();
    }

    void Equipar()
    {
        if (selecionado == null || !ShopProfile.Equip(selecionado)) return;
        MenuClickSound.PlayStart();
        provados.Remove(selecionado.Categoria);
        Atualizar();
    }

    void Comprar()
    {
        if (selecionado == null || !ShopProfile.Buy(selecionado)) return;
        MenuClickSound.PlayStart();
        provados[selecionado.Categoria] = selecionado;
        Atualizar();
    }

    bool EmTeste(ShopItem item) => provados.TryGetValue(item.Categoria, out var teste) && teste.Id == item.Id;

    void Oferta()
    {
        Fechar();
        aoOferta.Invoke(RabbitCatalog.Oferta);
    }

    void Fechar()
    {
        MenuClickSound.Play();
        gameObject.SetActive(false);
    }

    // ============================ Utilitários ============================

    // Uma única textura branca recortada permite painéis de cor sólida,
    // sem multiplicar o rosa da arte antiga em todas as superfícies.
    static Sprite SuperficieSprite()
    {
        if (superficie != null) return superficie;
        var c = new Canvas2D(96, 96);
        c.Fill(Color.white); c.FillRoundRect(0, 0, 96, 96, 22);
        superficie = ShopArt.ParaSprite(c.ToTexture("loja-superficie", false, false, false), new Vector4(24, 24, 24, 24));
        return superficie;
    }

    static Image Superficie(string nome, Transform pai, float x, float y, float w, float h, Color cor)
    {
        var sombra = Imagem(Caixa(nome + " sombra", pai, x, y + 8, w, h), SuperficieSprite());
        sombra.type = Image.Type.Sliced; sombra.color = new Color(.12f,.04f,.20f,.22f);
        var img = Imagem(Caixa(nome, pai, x, y, w, h), SuperficieSprite());
        img.type = Image.Type.Sliced; img.color = cor;
        return img;
    }

    /// <summary>Caixa em pixels da arte: centro ancorado no canto superior esquerdo do pai.</summary>
    static RectTransform Caixa(string nome, Transform pai, float x, float y, float w, float h)
    {
        var rt = NovoRect(nome, pai);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = Vector2.one * .5f;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x + w / 2f, -(y + h / 2f));
        return rt;
    }

    static RectTransform Filho(Transform pai)
    {
        var rt = NovoRect("Texto", pai);
        Esticar(rt);
        return rt;
    }

    /// <summary>Botão "de bala" com fundo em pílula e um clique que dispara `acao`.</summary>
    BotaoDoce Pilula(string nome, Transform pai, float x, float y, float w, float h, Sprite fundo, UnityAction acao)
    {
        var rt = Caixa(nome, pai, x, y, w, h);
        Imagem(rt, fundo, true);
        var b = rt.gameObject.AddComponent<BotaoDoce>();
        b.receberCliques = false;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rt.GetComponent<Image>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(acao);
        return b;
    }

    static Text Rotulo(RectTransform rt, string valor, int tamanho, Color cor, TextAnchor alinhamento = TextAnchor.MiddleCenter)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = UiArt.Font;
        t.text = valor;
        t.fontSize = tamanho;
        t.fontStyle = FontStyle.Bold;
        t.alignment = alinhamento;
        t.color = cor;
        t.raycastTarget = false;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 14;
        t.resizeTextMaxSize = tamanho;
        return t;
    }

    static Image Imagem(RectTransform rt, Sprite s, bool clicavel = false)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = s;
        img.raycastTarget = clicavel;
        return img;
    }

    static Image NovaImagem(string nome, RectTransform pai, Sprite s, bool clicavel)
    {
        return Imagem(NovoRect(nome, pai), s, clicavel);
    }

    static RectTransform NovoRect(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        return rt;
    }

    static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>Arrastar o dedo sobre a prévia gira o coelho no palco.</summary>
    public sealed class ArrastoPrevia : MonoBehaviour, IDragHandler
    {
        public ShopPreviewStage alvo;

        public void OnDrag(PointerEventData dados)
        {
            if (alvo != null) alvo.Girar(dados.delta.x);
        }
    }
}
