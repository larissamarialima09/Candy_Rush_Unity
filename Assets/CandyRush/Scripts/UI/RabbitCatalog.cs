using System.Collections.Generic;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>
    /// Coelhos da tela ESCOLHA SEU COELHO: preço e quais já são do jogador.
    /// A compra usa as mesmas moedas da loja (ShopProfile).
    /// </summary>
    public static class RabbitCatalog
    {
        const string OwnedKey = "CandyRush.RabbitsOwned";

        /// <summary>Mesma ordem de RacerProfile.RabbitNames. Preço 0 = livre desde o início.</summary>
        public static readonly int[] Precos = { 0, 0, 800, 2000 };

        /// <summary>Coelho do banner OFERTA DO DIA.</summary>
        public const int Oferta = 3;

        public static bool Possui(int indice) => Precos[indice] == 0 || Possuidos().Contains(indice);

        /// <summary>Compra o coelho com as moedas. Falha se já for do jogador ou sem saldo.</summary>
        public static bool Comprar(int indice)
        {
            if (Possui(indice) || !ShopProfile.Pay(Precos[indice])) return false;
            var indices = Possuidos();
            indices.Add(indice);
            var texto = new List<string>();
            foreach (var i in indices) texto.Add(i.ToString());
            PlayerPrefs.SetString(OwnedKey, string.Join(",", texto));
            PlayerPrefs.Save();
            return true;
        }

        static List<int> Possuidos()
        {
            var indices = new List<int>();
            foreach (var parte in PlayerPrefs.GetString(OwnedKey, "").Split(','))
                if (int.TryParse(parte, out int indice)) indices.Add(indice);
            return indices;
        }
    }
}
