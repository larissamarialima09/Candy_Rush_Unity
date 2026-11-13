using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>
    /// Moedas (uma carteira só) e, por coelho, os itens comprados e equipados da loja.
    /// Tudo fica no PlayerPrefs, como o perfil do piloto (RacerProfile).
    /// </summary>
    public static class ShopProfile
    {
        const string MoedasKey = "CandyRush.ShopCoins", PosseKey = "CandyRush.ShopOwned", EquipadoKey = "CandyRush.ShopEquipped";
        public const int MoedasIniciais = 1250;
        public static event Action Changed;

#if UNITY_EDITOR
        static string validationPrefix = "";
        static HashSet<string> validationKeys;
        /// <summary>Carteira temporária do teste; nunca escreve nas chaves do jogador.</summary>
        public static IDisposable BeginValidationProfile() => new ValidationProfile();
        sealed class ValidationProfile : IDisposable
        {
            readonly string previousPrefix = validationPrefix;
            readonly HashSet<string> previousKeys = validationKeys;
            public ValidationProfile()
            {
                validationPrefix = "CandyRush.Validation." + Guid.NewGuid().ToString("N") + ".";
                validationKeys = new HashSet<string>();
            }
            public void Dispose()
            {
                foreach (string key in validationKeys) PlayerPrefs.DeleteKey(key);
                validationPrefix = previousPrefix; validationKeys = previousKeys;
                PlayerPrefs.Save(); Changed?.Invoke();
            }
        }
#endif

        static string Key(string key)
        {
#if UNITY_EDITOR
            if (validationPrefix.Length > 0) { key = validationPrefix + key; validationKeys.Add(key); }
#endif
            return key;
        }

        public static int Coins => PlayerPrefs.GetInt(Key(MoedasKey), MoedasIniciais);

        // Posse e equipamento são por coelho: cada personagem tem o seu guarda-roupa.
        static string ChavePosse(int coelho) => Key(PosseKey + "." + coelho);
        static string ChaveEquipado(int coelho, ShopCategoria categoria) => Key(EquipadoKey + "." + categoria + "." + coelho);

        public static bool Owns(ShopItem item, int coelho) => item != null && (item.Inicial || OwnedIds(coelho).Contains(item.Id));
        public static bool Owns(ShopItem item) => Owns(item, RacerProfile.Rabbit);

        /// <summary>Compra o item para o coelho se estiver livre, não bloqueado e houver moedas suficientes.</summary>
        public static bool Buy(ShopItem item, int coelho)
        {
            if (item == null || item.Preco < 0 || Owns(item, coelho) || item.Bloqueado || Coins < item.Preco) return false;
            var ids = OwnedIds(coelho);
            ids.Add(item.Id);
            PlayerPrefs.SetInt(Key(MoedasKey), Coins - item.Preco);
            PlayerPrefs.SetString(ChavePosse(coelho), string.Join(",", ids));
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static bool Buy(ShopItem item) => Buy(item, RacerProfile.Rabbit);

        /// <summary>Desconta moedas para outra compra (ex.: liberar um coelho). Falha sem saldo.</summary>
        public static bool Pay(int valor)
        {
            if (valor < 0 || Coins < valor) return false;
            PlayerPrefs.SetInt(Key(MoedasKey), Coins - valor);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Moedas realmente coletadas na corrida entram na mesma carteira da loja.</summary>
        public static void Credit(int valor)
        {
            if (valor <= 0) return;
            PlayerPrefs.SetInt(Key(MoedasKey), (int)Math.Min(int.MaxValue, (long)Coins + valor));
            Changed?.Invoke();
        }

        public static void Save() => PlayerPrefs.Save();

        public static bool Equip(ShopItem item, int coelho)
        {
            if (!Owns(item, coelho)) return false;
            PlayerPrefs.SetString(ChaveEquipado(coelho, item.Categoria), item.Id);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static bool Equip(ShopItem item) => Equip(item, RacerProfile.Rabbit);

        /// <summary>Item equipado da categoria, ou null. Sem escolha salva, o capacete é o inicial.</summary>
        public static ShopItem Equipado(ShopCategoria categoria, int coelho)
        {
            string padrao = categoria == ShopCategoria.Capacete ? ShopCatalog.CapaceteInicial : "";
            return ShopCatalog.Find(PlayerPrefs.GetString(ChaveEquipado(coelho, categoria), padrao));
        }

        public static bool EstaEquipado(ShopItem item, int coelho) => Equipado(item.Categoria, coelho)?.Id == item.Id;

        /// <summary>Um item de cada categoria que estiver equipada no coelho.</summary>
        public static ShopItem[] EquipadosEm(int coelho)
        {
            var lista = new List<ShopItem>();
            foreach (ShopCategoria categoria in Enum.GetValues(typeof(ShopCategoria)))
            {
                var item = Equipado(categoria, coelho);
                if (item != null) lista.Add(item);
            }
            return lista.ToArray();
        }

        static List<string> OwnedIds(int coelho)
        {
            var ids = new List<string>();
            foreach (var id in PlayerPrefs.GetString(ChavePosse(coelho), "").Split(','))
                if (id.Length > 0) ids.Add(id);
            return ids;
        }
    }
}
