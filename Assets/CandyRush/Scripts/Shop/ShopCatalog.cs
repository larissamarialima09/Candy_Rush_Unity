using System.Collections.Generic;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Categorias da loja. A ordem segue as abas, da esquerda para a direita.</summary>
    public enum ShopCategoria { Capacete, Kart, Roda, Efeito }

    /// <summary>Item da loja. Preço em moedas doces.</summary>
    public sealed class ShopItem
    {
        public readonly string Id;
        public readonly string Nome;
        public readonly int Preco;
        public readonly bool Novo;
        public readonly bool Bloqueado;
        /// <summary>Já vem na posse do piloto (o capacete inicial).</summary>
        public readonly bool Inicial;
        /// <summary>Texto mostrado no card e no botão quando o item está bloqueado.</summary>
        public readonly string Requisito;
        public readonly ShopCategoria Categoria;
        /// <summary>Cor de pintura (KARTS e RODAS). Alfa 0 = o item não pinta nada.</summary>
        public readonly Color Cor;
        /// <summary>Preenchido quando o item vem de um CosmeticItem (ScriptableObject).</summary>
        public CosmeticItem Asset;

        public ShopItem(string id, string nome, int preco, ShopCategoria categoria, bool novo = false,
            bool bloqueado = false, bool inicial = false, string requisito = null, Color cor = default)
        {
            Id = id;
            Nome = nome;
            Preco = preco;
            Categoria = categoria;
            Novo = novo;
            Bloqueado = bloqueado;
            Inicial = inicial;
            Requisito = requisito;
            Cor = cor;
        }

        public bool TemPintura => Cor.a > 0f;
        /// <summary>Ícone 2D escolhido no ScriptableObject; null usa o ícone desenhado pela loja.</summary>
        public Sprite Icone => Asset != null ? Asset.icone : null;
    }

    /// <summary>
    /// Abas e itens da loja. Os itens de código ficam aqui; CosmeticItem criados em
    /// Resources/Candy/Shop são somados às suas categorias.
    /// </summary>
    public static class ShopCatalog
    {
        public static readonly string[] Abas = { "CAPACETES", "KARTS", "RODAS", "EFEITOS" };
        public const int AbaCapacetes = 0;

        /// <summary>
        /// Mostra as abas sem itens com "EM BREVE". Desligado, a aba some.
        /// A loja é montada em código (não há Inspector), por isso o ajuste fica aqui.
        /// </summary>
        public static bool MostrarAbasVazias = false;

        public const string CapaceteInicial = "cupcake";

        static readonly ShopItem[][] codigo =
        {
            new[]
            {
                new ShopItem("cupcake", "Capacete Cupcake", 300, ShopCategoria.Capacete, inicial: true),
                new ShopItem("unicornio", "Capacete Unicórnio", 500, ShopCategoria.Capacete, novo: true),
                new ShopItem("rosquinha", "Óculos Rosquinha", 200, ShopCategoria.Capacete),
                new ShopItem("coroa", "Capacete Coroa de Bala", 800, ShopCategoria.Capacete),
                new ShopItem("cachecol", "Cachecol de Alcaçuz", 150, ShopCategoria.Capacete),
                new ShopItem("misterioso", "Capacete Misterioso", 1000, ShopCategoria.Capacete, bloqueado: true,
                    requisito: "Desbloqueia no capítulo 3"),
            },
            new[]
            {
                new ShopItem("pintura_morango", "Pintura Morango", 250, ShopCategoria.Kart, cor: Rgb(0xff5fa8)),
                new ShopItem("pintura_menta", "Pintura Menta", 250, ShopCategoria.Kart, cor: Rgb(0x6fe0b8)),
                new ShopItem("pintura_dourada", "Pintura Dourada", 600, ShopCategoria.Kart, novo: true, cor: Rgb(0xffd45c)),
            },
            new[]
            {
                new ShopItem("aro_azul", "Aros Azuis", 150, ShopCategoria.Roda, cor: Rgb(0x66d9ff)),
                new ShopItem("aro_roxo", "Aros Roxos", 300, ShopCategoria.Roda, cor: Rgb(0xb388ff)),
                new ShopItem("aro_dourado", "Aros Dourados", 400, ShopCategoria.Roda, novo: true, cor: Rgb(0xffd45c)),
            },
            new ShopItem[0],
        };

        static readonly ShopItem[][] cache = new ShopItem[Abas.Length][];

        static Color Rgb(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

        /// <summary>Itens de uma aba: os de código mais os CosmeticItem da mesma categoria.</summary>
        public static ShopItem[] Itens(int aba)
        {
            if (aba < 0 || aba >= Abas.Length) return new ShopItem[0];
            if (cache[aba] != null) return cache[aba];
            var lista = new List<ShopItem>(codigo[aba]);
            foreach (var asset in Resources.LoadAll<CosmeticItem>("Candy/Shop"))
            {
                if ((int)asset.categoria != aba || string.IsNullOrWhiteSpace(asset.id) || asset.preco < 0) continue;
                int existente = lista.FindIndex(item => item.Id == asset.id);
                if (existente >= 0) lista[existente] = asset.ParaItem();
                else lista.Add(asset.ParaItem());
            }
            return cache[aba] = lista.ToArray();
        }

        public static void Reload()
        {
            for (int i = 0; i < cache.Length; i++) cache[i] = null;
        }

        /// <summary>Abas que a loja mostra: as que têm itens, ou todas se MostrarAbasVazias estiver ligado.</summary>
        public static List<int> AbasVisiveis()
        {
            var visiveis = new List<int>();
            for (int i = 0; i < Abas.Length; i++)
                if (MostrarAbasVazias || Itens(i).Length > 0) visiveis.Add(i);
            return visiveis;
        }

        /// <summary>Item pelo id em qualquer aba; null se não existir.</summary>
        public static ShopItem Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int aba = 0; aba < Abas.Length; aba++)
                foreach (var item in Itens(aba)) if (item.Id == id) return item;
            return null;
        }
    }
}
