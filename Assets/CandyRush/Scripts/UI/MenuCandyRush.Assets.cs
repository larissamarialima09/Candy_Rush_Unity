using System.Collections.Generic;
using CandyRush.Integrated;
using UnityEngine;
using UnityEngine.UI;

// Adapta o menu fornecido aos assets e ao perfil já existentes no jogo.
public partial class MenuCandyRush
{
    readonly List<Sprite> spritesCriados = new List<Sprite>();
    readonly List<Texture2D> texturasCriadas = new List<Texture2D>();
    RectTransform fundoRt;
    Text nomePiloto;
    InputField campoNome;
    Sprite[] coelhos;

    Sprite SpriteDe(Texture2D texture, bool owned = false)
    {
        if (texture == null) return null;
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
        spritesCriados.Add(sprite);
        if (owned) texturasCriadas.Add(texture);
        return sprite;
    }

    void PrepararAssets()
    {
        UiArt.Build();
        if (fundo == null) fundo = SpriteDe(Resources.Load<Texture2D>("Candy/Menu/SunsetBackdrop") ?? Resources.Load<Texture2D>("Candy/Menu/Backdrop"));
        if (logo == null) logo = SpriteDe(Resources.Load<Texture2D>("Candy/Menu/LogoTransparent") ?? Resources.Load<Texture2D>("Candy/Menu/Logo"));
        if (botaoJogar == null) botaoJogar = SpriteDe(UiArt.MenuPill(526, 147, "#ff80b8", "#f50079"), true);
        if (botaoConfig == null) botaoConfig = SpriteDe(UiArt.MenuPill(428, 97, "#bd8aeb", "#8133b0"), true);
        if (botaoNome == null) botaoNome = SpriteDe(UiArt.MenuPill(456, 66, "#fffaf5", "#ffe0eb"), true);
        if (botaoLoja == null) botaoLoja = SpriteDe(UiArt.MenuPill(210, 68, "#ff80b8", "#f50079"), true);
        var atlas = Resources.Load<Texture2D>("Candy/Menu/Rabbits");
        coelhos = new Sprite[4];
        if (atlas != null) for (int i = 0; i < 4; i++)
        {
            coelhos[i] = Sprite.Create(atlas, new Rect(i * atlas.width / 4f, 0, atlas.width / 4f, atlas.height), Vector2.one * .5f);
            spritesCriados.Add(coelhos[i]);
        }
        if (coelhoKart == null) coelhoKart = coelhos[RacerProfile.Rabbit];
        if (fumaca == null) fumaca = SpriteDe(UiArt.Pill);
        if (brilho == null) brilho = SpriteDe(UiArt.CoinDot);
    }

    Text Texto(Transform parent, string value, int size, Color color)
    {
        var rt = NovoRect("Texto", parent);
        Esticar(rt);
        rt.offsetMin = new Vector2(64, 8);
        rt.offsetMax = new Vector2(-24, -8);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = UiArt.Font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 16;
        text.resizeTextMaxSize = size;
        return text;
    }

    void Icone(Transform parent, Texture2D texture, float size)
    {
        var rt = NovoRect("Icone", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0, .5f);
        rt.anchoredPosition = new Vector2(size, 0);
        rt.sizeDelta = Vector2.one * size;
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = SpriteDe(texture);
        image.raycastTarget = false;
    }

    void CompletarInterface()
    {
        logoRt.GetComponent<Image>().preserveAspect = true;
        kartRt.GetComponent<Image>().preserveAspect = true;
        // Só exibe a camada giratória quando há recortes compatíveis fornecidos.
        logoRt.Find("PirulitoMascara").gameObject.SetActive(pirulitoGiro != null && pirulitoMascara != null);
        Texto(bJogar.transform, "JOGAR", 48, Color.white);
        var coelhoLabel = Texto(bConfig.transform, "Coelho", 30, Color.white);
        coelhoLabel.rectTransform.offsetMin = new Vector2(18, 4);
        coelhoLabel.rectTransform.offsetMax = new Vector2(-18, -4);
        Icone(bJogar.transform, UiArt.PlayIcon, 58);
        var lojaLabel = Texto(bLoja.transform, "LOJA", 34, Color.white);
        lojaLabel.rectTransform.offsetMin = new Vector2(18, 4);
        lojaLabel.rectTransform.offsetMax = new Vector2(-18, -4);
        nomePiloto = Texto(bNome.transform, "", 26, new Color(.3f, .12f, .43f));
        nomePiloto.rectTransform.offsetMin = new Vector2(18, 5);
        nomePiloto.rectTransform.offsetMax = new Vector2(-58, -5);
        nomePiloto.alignment = TextAnchor.MiddleLeft;
        nomePiloto.resizeTextForBestFit = false;
        campoNome = bNome.gameObject.AddComponent<InputField>();
        bNome.gameObject.AddComponent<MenuNameSelectSound>();
        campoNome.targetGraphic = bNome.GetComponent<Image>();
        campoNome.textComponent = nomePiloto;
        campoNome.characterLimit = RacerProfile.MaxNameLength;
        campoNome.lineType = InputField.LineType.SingleLine;
        campoNome.transition = Selectable.Transition.None;
        campoNome.text = RacerProfile.Name;
        campoNome.onValidateInput = (text, index, character) =>
            char.IsLetterOrDigit(character) || character == ' ' || character == '-' || character == '_'
                ? character : '\0';
        campoNome.onEndEdit.AddListener(_ => SalvarNome());
        var lapis = Texto(bNome.transform, "✎", 32, new Color(.3f, .12f, .43f));
        lapis.rectTransform.anchorMin = new Vector2(.82f, 0);
        lapis.rectTransform.offsetMin = lapis.rectTransform.offsetMax = Vector2.zero;
    }

    public void AtualizarPerfil()
    {
        if (campoNome != null && !campoNome.isFocused) campoNome.SetTextWithoutNotify(RacerProfile.Name);
        if (kartRt != null) kartRt.GetComponent<Image>().sprite = coelhos[RacerProfile.Rabbit];
    }

    public void EditarNome()
    {
        if (saindo || campoNome == null || campoNome.isFocused) return;
        campoNome.Select();
        campoNome.ActivateInputField();
    }

    public void SalvarNome()
    {
        if (campoNome == null) return;
        RacerProfile.Save(campoNome.text, RacerProfile.Rabbit);
        campoNome.SetTextWithoutNotify(RacerProfile.Name);
    }

    void OnDestroy()
    {
        foreach (var sprite in spritesCriados) if (sprite != null) Destroy(sprite);
        foreach (var texture in texturasCriadas) if (texture != null) Destroy(texture);
    }
}
