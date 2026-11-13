using System.Collections.Generic;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>
    /// Aplica os itens equipados num modelo de coelho: o capacete vai para um
    /// HeadSocket (filho do osso "Head", então acompanha a cabeça e as orelhas),
    /// e as pinturas trocam a cor das peças do kart e das rodas.
    /// Usado na corrida e no palco de prévia da loja.
    /// </summary>
    public static class CosmeticRig
    {
        const string NomeDaCabeca = "Head", NomeDoSocket = "HeadSocket";

        public static void Aplicar(Transform modelo, IReadOnlyList<ShopItem> itens)
        {
            if (modelo == null || itens == null) return;
            foreach (var item in itens)
            {
                if (item == null) continue;
                switch (item.Categoria)
                {
                    case ShopCategoria.Capacete: Capacete(modelo, item); break;
                    case ShopCategoria.Kart: Pintar(modelo, "Kart_Body", item); break;
                    case ShopCategoria.Roda: Pintar(modelo, "Wheel_", item); break;
                }
            }
        }

        /// <summary>Encaixa o prefab no HeadSocket; itens nativos também têm um modelo procedural de reserva.</summary>
        static void Capacete(Transform modelo, ShopItem item)
        {
            Transform cabeca = Procurar(modelo, NomeDaCabeca);
            if (cabeca == null)
            {
                Debug.LogWarning("CosmeticRig: osso \"" + NomeDaCabeca + "\" não encontrado no coelho.", modelo);
                return;
            }

            // O GLB traz um capacete no próprio mesh da cabeça. Retira somente esse
            // casco ao usar outro capacete; olhos, focinho, orelhas e pelo continuam intactos.
            if (item.Id == "cupcake" || item.Id == "unicornio" || item.Id == "coroa")
                OcultarCascoOriginal(cabeca);

            Transform socket = cabeca.Find(NomeDoSocket);
            if (socket == null)
            {
                socket = new GameObject(NomeDoSocket).transform;
                socket.SetParent(cabeca, false);
            }

            // Tira o capacete anterior antes de pôr o novo.
            for (int i = socket.childCount - 1; i >= 0; i--)
            {
                socket.GetChild(i).gameObject.SetActive(false);
                VisualMaterialLibrary.Dispose(socket.GetChild(i).gameObject);
            }

            var asset = item.Asset;
            if (asset == null || asset.prefab == null)
            {
                ShopCosmeticModels.Criar(item.Id, socket);
                return;
            }

            var capacete = Object.Instantiate(asset.prefab, socket, false).transform;
            capacete.localPosition = asset.offsetPosition;
            capacete.localRotation = Quaternion.Euler(asset.offsetRotation);
            capacete.localScale = asset.offsetScale;
        }

        static void OcultarCascoOriginal(Transform cabeca)
        {
            if (cabeca.GetComponent<ShopCosmeticMeshOwner>() != null) return;
            var dono = cabeca.gameObject.AddComponent<ShopCosmeticMeshOwner>();
            foreach (var filtro in cabeca.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filtro.GetComponentInParent<ShopCosmeticMeshOwner>() != dono || filtro.transform.name == NomeDoSocket) continue;
                var renderer = filtro.GetComponent<Renderer>();
                if (renderer == null || filtro.sharedMesh == null || !filtro.sharedMesh.isReadable) continue;
                var slots = renderer.sharedMaterials;
                Mesh copia = null;
                for (int i = 0; i < slots.Length && i < filtro.sharedMesh.subMeshCount; i++)
                {
                    if (slots[i] == null) continue;
                    var limites = filtro.sharedMesh.GetSubMesh(i).bounds;
                    bool faixa = slots[i].name.StartsWith("White") && limites.min.y > .005f && limites.size.y > .18f;
                    if (!slots[i].name.Contains("_Main") && !faixa) continue;
                    if (copia == null) copia = Object.Instantiate(filtro.sharedMesh);
                    copia.SetTriangles(new int[0], i, false);
                }
                if (copia == null) continue;
                dono.Meshes.Add(copia);
                filtro.sharedMesh = copia;
            }
        }

        /// <summary>
        /// Pinta somente os slots da carroceria/aros, com MaterialPropertyBlock.
        /// Preserva os assets compartilhados e o acabamento atualizado pelo KartVisuals.
        /// </summary>
        static void Pintar(Transform modelo, string parteDoNome, ShopItem item)
        {
            if (!item.TemPintura) return;
            var propriedades = new MaterialPropertyBlock();
            foreach (var renderer in modelo.GetComponentsInChildren<Renderer>(true))
            {
                bool pertence = false;
                for (var p = renderer.transform; p != null && p != modelo.parent; p = p.parent)
                    if (p.name.Contains(parteDoNome)) { pertence = true; break; }
                if (!pertence) continue;
                var materiais = renderer.sharedMaterials;
                for (int i = 0; i < materiais.Length; i++)
                {
                    var material = materiais[i];
                    if (material == null) continue;
                    bool principal = material.name.Contains("_Main");
                    bool aro = parteDoNome == "Wheel_" && (material.name.StartsWith("Metal") || material.name.StartsWith("White"));
                    // Preserva pneus, banco, olhos e detalhes brancos do chassi.
                    if (!principal && !aro) continue;
                    propriedades.Clear();
                    renderer.GetPropertyBlock(propriedades, i);
                    if (material.HasProperty("baseColorFactor")) propriedades.SetColor("baseColorFactor", item.Cor);
                    if (material.HasProperty("_BaseColor")) propriedades.SetColor("_BaseColor", item.Cor);
                    if (material.HasProperty("_Color")) propriedades.SetColor("_Color", item.Cor);
                    renderer.SetPropertyBlock(propriedades, i);
                }
            }
        }

        static Transform Procurar(Transform raiz, string nome)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t;
            return null;
        }
    }

    public sealed class ShopCosmeticMeshOwner : MonoBehaviour
    {
        public readonly List<Mesh> Meshes = new List<Mesh>();
        void OnDestroy() { foreach (var mesh in Meshes) VisualMaterialLibrary.Dispose(mesh); }
    }
}
