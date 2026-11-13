using System.Collections.Generic;
using CandyRush.Integrated;
using UnityEngine;
using UnityEngine.UI;

public partial class MenuCandyRush
{
    readonly List<RectTransform> nuvens = new List<RectTransform>();
    readonly List<Vector2> posicoesNuvens = new List<Vector2>();
    void MontarNuvens()
    {
        var points = new[] { new Vector2(135, 110), new Vector2(1460, 90), new Vector2(280, 325), new Vector2(1390, 375), new Vector2(80, 500), new Vector2(1540, 530) };
        for (int i = 0; i < points.Length; i++)
        {
            var cloud = NovoRect("Nuvem_" + i, palco);
            cloud.anchorMin = cloud.anchorMax = PontoRef(points[i]);
            cloud.sizeDelta = new Vector2(190, 75);
            float size = i < 2 ? 1 : .75f;
            cloud.localScale = Vector3.one * size;
            for (int j = 0; j < 5; j++)
            {
                var puff = NovaImagem("Doce", cloud, SpriteDe(UiArt.Pill), Vector4.zero, false);
                puff.rectTransform.sizeDelta = new Vector2(85, 48 + (j == 2 ? 25 : 0));
                puff.rectTransform.anchoredPosition = new Vector2((j - 2) * 30, j == 2 ? 16 : j % 2 * 8);
                puff.color = new Color(1, .84f, .89f, .28f);
            }
            nuvens.Add(cloud);
            posicoesNuvens.Add(cloud.anchoredPosition);
        }
    }
    void AnimarNuvens(float time)
    {
        for (int i = 0; i < nuvens.Count; i++)
            nuvens[i].anchoredPosition = posicoesNuvens[i] + new Vector2(Mathf.Sin(time * .13f + i) * 30, Mathf.Sin(time * .35f + i * 2) * 5);
    }
}
