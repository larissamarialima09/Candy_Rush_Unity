using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>
    /// Item de loja definido no Inspector. Crie em Assets/Resources/Candy/Shop
    /// (botão direito > Create > Candy Rush > Item de loja). A loja junta esses
    /// itens aos da categoria, e o capacete encaixa no osso "Head" do coelho.
    /// </summary>
    [CreateAssetMenu(fileName = "NovoItemLoja", menuName = "Candy Rush/Item de loja")]
    public sealed class CosmeticItem : ScriptableObject
    {
        [Tooltip("Identificador salvo no PlayerPrefs. Não mude depois de publicar.")]
        public string id;
        public string nome;
        public ShopCategoria categoria;
        public int preco = 300;
        public bool novo;
        public bool inicial;
        public bool bloqueado;
        [Tooltip("Texto do bloqueio, ex.: \"Desbloqueia no capítulo 3\".")]
        public string requisito;

        [Tooltip("Ícone 2D do card. Vazio: a loja usa o ícone desenhado.")]
        public Sprite icone;

        [Tooltip("KARTS e RODAS: cor aplicada às peças do kart ou às rodas.")]
        public Color cor = Color.white;

        [Header("Capacete (CAPACETE)")]
        [Tooltip("Modelo 3D do capacete. Vai como filho do HeadSocket.")]
        public GameObject prefab;
        [Tooltip("Posição local no HeadSocket. Ajuste até as orelhas ficarem visíveis.")]
        public Vector3 offsetPosition;
        [Tooltip("Rotação local em graus.")]
        public Vector3 offsetRotation;
        [Tooltip("Escala local. O coelho já vem com escala 1,2 no kart.")]
        public Vector3 offsetScale = Vector3.one;

        /// <summary>Converte o asset no item usado pela loja.</summary>
        public ShopItem ParaItem() =>
            new ShopItem(id, nome, preco, categoria, novo, bloqueado, inicial, requisito, cor) { Asset = this };
    }
}
