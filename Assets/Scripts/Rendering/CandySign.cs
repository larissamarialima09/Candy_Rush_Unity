using UnityEngine;

namespace CandyRush.Rendering
{
    /// <summary>
    /// Letreiro de bala (o createSignTexture da web): placa rosa com moldura
    /// creme, granulado em cima e embaixo e o texto em branco com contorno.
    ///
    /// Na web o texto era pintado num canvas; aqui a placa é uma textura e o
    /// texto é 3D (TextMesh) por cima, com o contorno feito de oito cópias.
    /// A placa fica virada para -Z local (lida por quem olha na direção +Z).
    /// </summary>
    public static class CandySign
    {
        /// <param name="width">Largura da placa; a altura é width / 4, como na web.</param>
        public static Transform Build(Transform parent, string text, float width, Vector3 position, Color background, float glow = 0.3f)
        {
            float height = width / 4f;
            Transform root = SceneryBuilder.Group(parent, "Letreiro " + text, position, Quaternion.identity);

            Material board = CandyMaterials.Textured(ProceduralTextures.SignBackground(background), 0.35f);
            CandyMaterials.SetGlow(board, background, glow);
            SceneryBuilder.Part(root, "Placa", ProceduralMeshes.Primitive(PrimitiveType.Quad), board,
                Vector3.zero, Quaternion.identity, new Vector3(width, height, 1f), false);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return root;
            Material material = CandyMaterials.Text(font);
            // A fonte é dinâmica: quando a Unity refaz o atlas de letras, a textura muda.
            Font.textureRebuilt += rebuilt =>
            {
                if (rebuilt == font && material != null) material.mainTexture = font.material.mainTexture;
            };

            // Tamanho do texto proporcional à placa, e encolhido se o texto for comprido.
            float characterSize = height * 0.042f * Mathf.Min(1f, 11f / Mathf.Max(1, text.Length));
            Transform textRoot = SceneryBuilder.Group(root, "Texto", new Vector3(0f, 0f, -0.03f), Quaternion.identity);
            float outline = height * 0.025f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                AddText(textRoot, text, font, material, characterSize,
                    new Vector3(Mathf.Cos(angle) * outline, Mathf.Sin(angle) * outline, 0.01f), ProceduralTextures.Hex(0xb8245f));
            }
            AddText(textRoot, text, font, material, characterSize, Vector3.zero, Color.white);
            return root;
        }

        private static void AddText(Transform parent, string content, Font font, Material material, float characterSize, Vector3 offset, Color color)
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            var text = go.AddComponent<TextMesh>();
            text.text = content;
            text.font = font;
            text.fontSize = 120;
            text.fontStyle = FontStyle.Bold;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
