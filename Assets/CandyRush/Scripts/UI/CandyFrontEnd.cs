using System;
using System.Globalization;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Interactive menu over separate artwork; no text/buttons baked into backgrounds.</summary>
    public sealed class CandyFrontEnd
    {
        public enum Page { Home, Rabbit }
        public Page CurrentPage { get; private set; }
        public int SelectedRabbit { get; private set; }
        /// <summary>Desligado pelo GameUI quando o logo animado (Canvas) assume o lugar do logo estático.</summary>
        public bool ShowStaticLogo = true;
        static readonly CultureInfo Pt = new CultureInfo("pt-BR");
        readonly Action<int> start;
        readonly Texture2D background, logo, rabbits, lollipop;
        GUIStyle pink, purple, small, title, caption, card, homePlay, homeSettings, homePlate;
        GUIStyle selectionArrow, selectionContinue;
        public CandyFrontEnd(Action<int> start)
        {
            this.start = start;
            background = Resources.Load<Texture2D>("Candy/Menu/SunsetBackdrop") ?? Resources.Load<Texture2D>("Candy/Menu/Backdrop");
            logo = Resources.Load<Texture2D>("Candy/Menu/Logo");
            rabbits = Resources.Load<Texture2D>("Candy/Menu/Rabbits");
            lollipop = Resources.Load<Texture2D>("Candy/Menu/Lollipop");
            SelectedRabbit = RacerProfile.Rabbit;
        }
        public void OpenSettings()
        {
            MenuClickSound.Play();
            SelectedRabbit = RacerProfile.Rabbit;
            CurrentPage = Page.Rabbit;
        }
        public void SelectRabbit(int index)
        {
            MenuClickSound.Play();
            SelectedRabbit = (index % 4 + 4) % 4;
        }
        /// <summary>Banner OFERTA DO DIA: abre a escolha de coelho com o indicado já em foco.</summary>
        public void OpenSelection(int index)
        {
            MenuClickSound.Play();
            SelectedRabbit = (index % 4 + 4) % 4;
            CurrentPage = Page.Rabbit;
        }
        /// <summary>Salva o coelho escolhido e volta ao menu inicial. Coelho bloqueado não é escolhido.</summary>
        public void ConfirmRabbit()
        {
            if (!RabbitCatalog.Possui(SelectedRabbit)) return;
            MenuClickSound.Play();
            RacerProfile.Save(RacerProfile.Name, SelectedRabbit);
            CurrentPage = Page.Home;
        }
        /// <summary>Compra o coelho focado com as moedas. Fica em SELECIONAR quando dá certo.</summary>
        void Comprar()
        {
            if (RabbitCatalog.Comprar(SelectedRabbit)) MenuClickSound.PlayStart();
            else MenuClickSound.Play();
        }
        public void Back()
        {
            MenuClickSound.Play();
            CurrentPage = Page.Home;
        }
        public void Play(bool playSound = true)
        {
            if (playSound) MenuClickSound.PlayStart();
            start(1);
        }
        void Styles()
        {
            if (pink != null) return;
            pink = new GUIStyle(GUI.skin.button) { font = UiArt.Font, fontSize = 32, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, border = new RectOffset(60, 60, 60, 60) };
            pink.normal.background = UiArt.FrontPink;
            pink.hover.background = UiArt.FrontHover;
            pink.active.background = UiArt.FrontPurple;
            pink.focused.background = UiArt.FrontHover;
            pink.normal.textColor = pink.hover.textColor = pink.active.textColor = pink.focused.textColor = Color.white;
            purple = new GUIStyle(pink) { fontSize = 23 };
            purple.normal.background = UiArt.FrontPurple;
            small = HomeButton(112, 60, 19, "#bd8aeb", "#8133b0");
            selectionArrow = HomeButton(64, 72, 32, "#ff80b8", "#f50079");
            selectionContinue = HomeButton(340, 76, 32, "#ff80b8", "#f50079");
            title = new GUIStyle { font = UiArt.Font, fontSize = 31, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, normal = { textColor = Hex.C(0x42216e) } };
            caption = new GUIStyle(title) { fontSize = 20, wordWrap = true };
            card = new GUIStyle { normal = { background = UiArt.FrontPanel }, border = new RectOffset(30, 30, 30, 30) };
            homePlay = HomeButton(400, 104, 36, "#ff80b8", "#f50079");
            homeSettings = HomeButton(320, 72, 22, "#bd8aeb", "#8133b0");
            homePlate = new GUIStyle { normal = { background = UiArt.MenuPill(340, 48, "#fffaf5", "#ffe0eb") } };
        }
        GUIStyle HomeButton(int width, int height, int fontSize, string top, string bottom)
        {
            var style = new GUIStyle(pink) { fontSize = fontSize, border = new RectOffset() };
            style.normal.background = UiArt.MenuPill(width, height, top, bottom);
            style.hover.background = style.focused.background = UiArt.MenuPill(width, height, "#ffd2ec", bottom);
            style.active.background = UiArt.MenuPill(width, height, bottom, top);
            return style;
        }
        void Panel(Rect r) => GUI.Box(r, GUIContent.none, card);
        /// <summary>Pílula com ícone à esquerda e texto, para JOGAR/CONFIGURAÇÕES.</summary>
        bool IconButton(Rect r, Texture2D icon, string label, GUIStyle pillStyle)
        {
            bool clicked = GUI.Button(r, GUIContent.none, pillStyle);
            float iconSize = r.height * 0.46f;
            float iconX = r.x + r.height * 0.32f;
            GUI.DrawTexture(new Rect(iconX, r.y + (r.height - iconSize) / 2, iconSize, iconSize), icon);
            var textStyle = new GUIStyle { font = pillStyle.font, fontSize = pillStyle.fontSize, fontStyle = pillStyle.fontStyle,
                alignment = TextAnchor.MiddleCenter, normal = { textColor = pillStyle.normal.textColor } };
            float textX = iconX + iconSize + 10;
            GUI.Label(new Rect(textX, r.y, r.xMax - textX - 14, r.height), label, textStyle);
            return clicked;
        }
        void Rabbit(Rect r, int index)
        {
            if (rabbits == null) return;
            float aspect = rabbits.width / 4f / rabbits.height;
            float w = Mathf.Min(r.width, r.height * aspect), h = w / aspect;
            GUI.DrawTextureWithTexCoords(new Rect(r.center.x - w / 2, r.center.y - h / 2, w, h), rabbits,
                new Rect(index * .25f, 0, .25f, 1));
        }
        /// <summary>Pirulito girando, 24 quadros num atlas 6x4 gerado por BakeFrontEndArt.</summary>
        void Lollipop(Rect r, float offset)
        {
            if (lollipop == null) return;
            int frame = Mathf.FloorToInt((Time.unscaledTime + offset) * 12) % 24;
            GUI.DrawTextureWithTexCoords(r, lollipop, new Rect((frame % 6) / 6f, (frame / 6) / 4f, 1f / 6, .25f));
        }
        public void Draw()
        {
            Styles();
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), background != null ? background : UiArt.MenuSky, ScaleMode.ScaleAndCrop);
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            float scale = Mathf.Min(safe.width / 1280f, safe.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x + (safe.width - 1280 * scale) / 2,
                Screen.height - safe.yMax + (safe.height - 720 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            Lollipop(new Rect(1130, 490, 120, 168), 0);
            if (CurrentPage == Page.Home) DrawHome();
            else
            {
                if (GUI.Button(new Rect(28, 28, 112, 60), "VOLTAR", small)) Back();
                DrawSelection();
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && CurrentPage != Page.Home)
            { Back(); Event.current.Use(); }
            GUI.matrix = previous;
        }
        void DrawHome()
        {
            if (ShowStaticLogo && logo != null) GUI.DrawTexture(new Rect(360, 5, 580, 355), logo, ScaleMode.ScaleToFit);
            Rabbit(new Rect(30, 278, 340, 400), RacerProfile.Rabbit);
            if (IconButton(new Rect(440, 354, 400, 104), UiArt.PlayIcon, "JOGAR", homePlay)) Play();
            if (IconButton(new Rect(480, 478, 320, 72), UiArt.GearIcon, "CONFIGURAÇÕES", homeSettings)) OpenSettings();
            GUI.Box(new Rect(470, 580, 340, 48), GUIContent.none, homePlate);
            string plate = RacerProfile.Name + "  ·  " + RacerProfile.RabbitNames[RacerProfile.Rabbit];
            GUI.DrawTexture(new Rect(480, 587, 28, 28), UiArt.FlagIcon);
            GUI.Label(new Rect(512, 582, 256, 38), plate, caption);
            GUI.DrawTexture(new Rect(772, 587, 28, 28), UiArt.FlagIcon);
            Lollipop(new Rect(1040, 105, 90, 126), 1);
        }
        void Header(string text)
        {
            var rect = new Rect(235, 40, 810, 80);
            GUI.DrawTexture(new Rect(rect.x, rect.y + 5, rect.width, rect.height), UiArt.White,
                ScaleMode.StretchToFill, true, 0, new Color(.3f, .1f, .4f, .25f), 0, rect.height / 2);
            GUI.DrawTexture(rect, UiArt.White, ScaleMode.StretchToFill, true, 0,
                new Color(1f, .97f, .99f), 0, rect.height / 2);
            GUI.Label(new Rect(255, 50, 770, 60), text, title);
        }
        void DrawSelection()
        {
            Header("ESCOLHA SEU COELHO");
            const float cardW = 220, gap = 26;
            float total = 4 * cardW + 3 * gap;
            float startX = (1280 - total) / 2;
            const float centerY = 360;
            for (int i = 0; i < 4; i++)
            {
                bool selected = i == SelectedRabbit;
                bool possui = RabbitCatalog.Possui(i);
                float h = selected ? 350 : 320;
                Rect r = new Rect(startX + i * (cardW + gap), centerY - h / 2, cardW, h);
                if (selected)
                {
                    var color = GUI.color;
                    GUI.color = new Color(1, .35f, .73f, .8f + .2f * Mathf.Sin(Time.unscaledTime * 3));
                    Panel(new Rect(r.x - 7, r.y - 7, r.width + 14, r.height + 14));
                    GUI.color = color;
                }
                if (GUI.Button(r, GUIContent.none, card)) SelectRabbit(i);
                // Bloqueado: silhueta escura, sem revelar o coelho.
                var tint = GUI.color;
                if (!possui) GUI.color = new Color(.12f, .06f, .18f, .92f);
                Rabbit(new Rect(r.x + 10, r.y + 9, r.width - 20, r.height - 62), i);
                GUI.color = tint;
                if (!possui) DrawLock(r, i);
                GUI.Label(new Rect(r.x + 5, r.yMax - 48, r.width - 10, 36), RacerProfile.RabbitNames[i], caption);
            }
            if (GUI.Button(new Rect(startX - 84, centerY - 36, 64, 72), "‹", selectionArrow)) SelectRabbit(SelectedRabbit - 1);
            if (GUI.Button(new Rect(startX + total + 20, centerY - 36, 64, 72), "›", selectionArrow)) SelectRabbit(SelectedRabbit + 1);
            DrawBuyBar();
            DrawCoins();
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            { ConfirmRabbit(); Event.current.Use(); }
        }

        /// <summary>Cadeado no centro da silhueta e o preço em moedas embaixo do nome.</summary>
        void DrawLock(Rect card, int indice)
        {
            GUI.DrawTexture(new Rect(card.center.x - 34, card.center.y - 60, 68, 80), ShopArt.Cadeado);
            var pill = new Rect(card.center.x - 80, card.yMax - 92, 160, 40);
            GUI.DrawTexture(new Rect(pill.x + 8, pill.y + 6, 28, 28), UiArt.CoinDot);
            GUI.Label(new Rect(pill.x + 40, pill.y, 116, 40), RabbitCatalog.Precos[indice].ToString("N0", Pt), caption);
        }

        /// <summary>
        /// Botão do coelho focado. Sempre explica o que falta: SELECIONAR, COMPRAR (preço)
        /// ou FALTAM X MOEDAS (desligado).
        /// </summary>
        void DrawBuyBar()
        {
            var barra = new Rect(430, 600, 420, 76);
            if (RabbitCatalog.Possui(SelectedRabbit))
            {
                if (GUI.Button(barra, "SELECIONAR", selectionContinue)) ConfirmRabbit();
                return;
            }
            int preco = RabbitCatalog.Precos[SelectedRabbit];
            if (ShopProfile.Coins >= preco)
            {
                if (GUI.Button(barra, "COMPRAR (" + preco.ToString("N0", Pt) + ")", selectionContinue)) Comprar();
                return;
            }
            var cor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, .55f);
            GUI.enabled = false;
            GUI.Button(barra, "FALTAM " + (preco - ShopProfile.Coins).ToString("N0", Pt) + " MOEDAS", selectionContinue);
            GUI.enabled = true;
            GUI.color = cor;
        }

        /// <summary>Contador de moedas no canto superior direito, com a moeda de pirulito.</summary>
        void DrawCoins()
        {
            var caixa = new Rect(1060, 46, 190, 60);
            GUI.DrawTexture(new Rect(caixa.x + 6, caixa.y + 16, 28, 28), UiArt.CoinDot);
            GUI.Label(new Rect(caixa.x + 40, caixa.y + 6, 144, 46), ShopProfile.Coins.ToString("N0", Pt), caption);
        }
    }
}
