using System.Collections.Generic;
using CandyRush.Integrated;
using UnityEditor;
using UnityEngine;

public static class ShopCosmeticAssets
{
    const string Pasta = "Assets/Resources/Candy/Shop";

    [MenuItem("CandyRush/Loja/Gerar prefabs dos doces")]
    public static void Bake()
    {
        System.IO.Directory.CreateDirectory(Pasta + "/Models");
        System.IO.Directory.CreateDirectory("Assets/CandyRush/Art/Shop");
        AssetDatabase.Refresh();
        var meshes = new Dictionary<Mesh, Mesh>();
        var materials = new Dictionary<Material, Material>();
        var itens = new List<ShopItem>(ShopCatalog.Itens(0));
        foreach (var item in itens)
        {
            var holder = new GameObject("Bake doces");
            try
            {
                var modelo = ShopCosmeticModels.Criar(item.Id, holder.transform);
                if (modelo == null) continue;
                modelo.name = item.Id;
                foreach (var filtro in modelo.GetComponentsInChildren<MeshFilter>())
                {
                    var source = filtro.sharedMesh;
                    if (!meshes.TryGetValue(source, out var salvo))
                    {
                        salvo = Salvar(Object.Instantiate(source), "Assets/CandyRush/Art/Shop/Mesh_" + meshes.Count + ".asset");
                        meshes.Add(source, salvo);
                    }
                    filtro.sharedMesh = salvo;
                }
                foreach (var renderer in modelo.GetComponentsInChildren<Renderer>())
                {
                    var source = renderer.sharedMaterial;
                    if (!materials.TryGetValue(source, out var salvo))
                    {
                        salvo = Salvar(Object.Instantiate(source), "Assets/CandyRush/Art/Shop/" + source.name + ".mat");
                        materials.Add(source, salvo);
                    }
                    renderer.sharedMaterial = salvo;
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(modelo.gameObject, Pasta + "/Models/" + item.Id + ".prefab");
                string path = Pasta + "/" + item.Id + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<CosmeticItem>(path);
                if (asset == null) { asset = ScriptableObject.CreateInstance<CosmeticItem>(); AssetDatabase.CreateAsset(asset, path); }
                asset.id = item.Id; asset.nome = item.Nome; asset.preco = item.Preco;
                asset.categoria = item.Categoria; asset.novo = item.Novo; asset.inicial = item.Inicial;
                asset.bloqueado = item.Bloqueado; asset.requisito = item.Requisito;
                asset.prefab = prefab; asset.cor = Color.clear; asset.offsetScale = Vector3.one;
                EditorUtility.SetDirty(asset);
            }
            finally { Object.DestroyImmediate(holder); }
        }
        AssetDatabase.SaveAssets();
        ShopCatalog.Reload();
    }

    static T Salvar<T>(T copia, string path) where T : Object
    {
        copia.name = System.IO.Path.GetFileNameWithoutExtension(path);
        var existente = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existente == null) { AssetDatabase.CreateAsset(copia, path); return copia; }
        EditorUtility.CopySerialized(copia, existente);
        Object.DestroyImmediate(copia);
        EditorUtility.SetDirty(existente);
        return existente;
    }
}
