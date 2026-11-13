using System.Collections.Generic;
using CandyRush.Rendering;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Modelos leves de doces, nas medidas do Head dos quatro GLBs do jogo.</summary>
    public static class ShopCosmeticModels
    {
        static readonly Dictionary<int, Material> materiais = new Dictionary<int, Material>();
        static Mesh esfera;

        public static Transform Criar(string id, Transform pai)
        {
            if (id != "cupcake" && id != "unicornio" && id != "rosquinha" && id != "coroa" && id != "cachecol") return null;
            var raiz = new GameObject("Acessorio " + id).transform;
            raiz.SetParent(pai, false);
            switch (id)
            {
                case "cupcake": Cupcake(raiz); break;
                case "unicornio": Unicornio(raiz); break;
                case "rosquinha": Rosquinha(raiz); break;
                case "coroa": Coroa(raiz); break;
                case "cachecol": Cachecol(raiz); break;
            }
            return raiz;
        }

        static Material Material(int cor)
        {
            if (!materiais.TryGetValue(cor, out var material) || material == null)
            {
                material = Mats.Candy(cor, .28f).Material;
                material.name = "Doce " + cor.ToString("x6");
                if (material.HasProperty("_UseFog")) material.SetFloat("_UseFog", 0);
                materiais[cor] = material;
            }
            return material;
        }

        static Transform Peca(Transform pai, string nome, Mesh mesh, int cor, Vector3 pos, Vector3 escala, Vector3 rotacao = default)
        {
            var go = new GameObject(nome, typeof(MeshFilter), typeof(MeshRenderer));
            var t = go.transform;
            t.SetParent(pai, false);
            t.localPosition = pos; t.localScale = escala; t.localRotation = Quaternion.Euler(rotacao);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = Material(cor);
            return t;
        }

        static void Bola(Transform pai, string nome, int cor, Vector3 pos, Vector3 escala)
        {
            if (esfera == null) esfera = Geo.Sphere(1, 20, 12).ToMesh("Doce esfera");
            Peca(pai, nome, esfera, cor, pos, escala);
        }

        static void Anel(Transform pai, string nome, int cor, float raio, float tubo, Vector3 pos, Vector3 rotacao = default)
            => Peca(pai, nome, ProceduralMeshes.Torus(raio, tubo, 8, 32), cor, pos, Vector3.one, rotacao);

        static void Cupcake(Transform pai)
        {
            Peca(pai, "Forma rosa", ProceduralMeshes.Cylinder(.32f, .29f, .13f, 32), 0xec4f9b,
                new Vector3(0, .20f, 0), Vector3.one);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2 / 24;
                Bola(pai, "Gomo da forma", 0xff8ac6, new Vector3(Mathf.Cos(a) * .304f, .20f, Mathf.Sin(a) * .304f), new Vector3(.016f, .063f, .016f));
            }
            Bola(pai, "Glace", 0xffe8f4, new Vector3(0, .30f, 0), new Vector3(.325f, .13f, .30f));
            Bola(pai, "Glace superior", 0xffc9e4, new Vector3(0, .38f, .08f), new Vector3(.18f, .085f, .15f));
            Bola(pai, "Cereja", 0xf32b75, new Vector3(0, .47f, .10f), Vector3.one * .065f);
            int[] cores = { 0xffd55a, 0x9ae9d3, 0xc69bff, 0xff649d };
            for (int i = 0; i < 12; i++)
            {
                float a = i * 2.39996f;
                Peca(pai, "Confeito", ProceduralMeshes.RoundedBox(new Vector3(.048f, .015f, .016f), .006f, 2), cores[i % 4],
                    new Vector3(Mathf.Cos(a) * .235f, .355f, Mathf.Sin(a) * .19f), Vector3.one, new Vector3(0, i * 37, 0));
            }
        }

        static void Unicornio(Transform pai)
        {
            Bola(pai, "Casco de bala", 0xc39aef, new Vector3(0, .18f, -.01f), new Vector3(.32f, .16f, .29f));
            Anel(pai, "Borda", 0xf6dcff, .295f, .025f, new Vector3(0, .17f, 0), new Vector3(90, 0, 0));
            Peca(pai, "Chifre dourado", ProceduralMeshes.Cone(.075f, .30f, 24), 0xffd45c,
                new Vector3(0, .40f, .18f), Vector3.one);
            for (int i = 0; i < 4; i++)
                Anel(pai, "Espiral do chifre", 0xfff0b3, .065f - i * .014f, .008f,
                    new Vector3(0, .30f + i * .053f, .18f), new Vector3(90, 0, 0));
            for (int i = 0; i < 5; i++)
                Bola(pai, "Crina", i % 2 == 0 ? 0xff8ac6 : 0x8de8df, new Vector3(0, .33f - i * .018f, .06f - i * .08f), new Vector3(.06f, .055f, .06f));
        }

        static void Rosquinha(Transform pai)
        {
            foreach (int lado in new[] { -1, 1 })
            {
                var pos = new Vector3(lado * .126f, -.005f, .255f);
                Anel(pai, "Rosquinha", 0xe7ae6c, .093f, .027f, pos);
                Anel(pai, "Cobertura rosa", 0xff80b8, .093f, .022f, pos + Vector3.forward * .023f);
                for (int i = 0; i < 7; i++)
                {
                    float a = i * Mathf.PI * 2 / 7;
                    Bola(pai, "Confeito dos oculos", i % 2 == 0 ? 0xffffff : 0xffdf73,
                        pos + new Vector3(Mathf.Cos(a) * .094f, Mathf.Sin(a) * .094f, .042f), Vector3.one * .009f);
                }
                Peca(pai, "Haste", ProceduralMeshes.RoundedBox(new Vector3(.023f, .026f, .26f), .01f, 2), 0xff80b8,
                    new Vector3(lado * .248f, 0, .12f), Vector3.one);
            }
            Peca(pai, "Ponte dos oculos", ProceduralMeshes.RoundedBox(new Vector3(.065f, .024f, .025f), .01f, 2), 0xff80b8,
                new Vector3(0, 0, .28f), Vector3.one);
        }

        static void Coroa(Transform pai)
        {
            Peca(pai, "Aro da coroa", ProceduralMeshes.Cylinder(.306f, .30f, .09f, 32), 0xffc741,
                new Vector3(0, .23f, 0), Vector3.one);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7;
                var pos = new Vector3(Mathf.Cos(a) * .28f, .36f, Mathf.Sin(a) * .28f);
                Peca(pai, "Ponta da coroa", ProceduralMeshes.Cone(.075f, .23f, 4), 0xffdc65, pos, Vector3.one);
                Bola(pai, "Bala na ponta", i % 2 == 0 ? 0xff66ae : 0x8de8df, pos + Vector3.up * .12f, Vector3.one * .027f);
            }
            Bola(pai, "Joia", 0xee519e, new Vector3(0, .24f, .30f), new Vector3(.045f, .035f, .02f));
        }

        static void Cachecol(Transform pai)
        {
            Anel(pai, "Gola de alcacuz", 0x44213d, .175f, .065f, new Vector3(0, -.22f, 0), new Vector3(90, 0, 0));
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(-.14f, -.30f - i * .036f, .18f + i * .012f);
                Peca(pai, "Listra do cachecol", ProceduralMeshes.RoundedBox(new Vector3(.11f, .041f, .045f), .012f, 2),
                    i % 2 == 0 ? 0xff73b6 : 0x44213d, p, Vector3.one, new Vector3(-15, 0, -12));
            }
        }
    }
}
