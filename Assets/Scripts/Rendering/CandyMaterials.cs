using UnityEngine;
using UnityEngine.Rendering;

namespace CandyRush.Rendering
{
    /// <summary>
    /// Fábrica de materiais do jogo. Tudo usa o shader "CandyRush/Candy"
    /// (Assets/Resources/Shaders), que fica dentro de Resources para entrar
    /// no build mesmo sendo carregado só por código.
    /// </summary>
    public static class CandyMaterials
    {
        private const string CandyShaderName = "CandyRush/Candy";
        private const string SkyShaderName = "CandyRush/SkyGradient";

        private static Shader _candy;

        private static Shader CandyShader
        {
            get
            {
                if (_candy == null) _candy = Shader.Find(CandyShaderName);
                if (_candy == null) Debug.LogError($"[CandyMaterials] Shader '{CandyShaderName}' não encontrado.");
                return _candy;
            }
        }

        /// <summary>
        /// Material de bala: brilhante e sem metal ("glossy candy" do guia de estilo).
        /// roughness é o valor da versão web (0 = espelho, 1 = fosco).
        /// </summary>
        public static Material Candy(Color color, float roughness = 0.22f)
        {
            var material = new Material(CandyShader) { name = "Candy" };
            material.SetColor("_Color", color);
            material.SetFloat("_Glossiness", Mathf.Clamp01(1f - roughness) * 0.8f);
            return material;
        }

        /// <summary>Material que brilha sozinho (faróis, lanternas, escapamento).</summary>
        public static Material Glow(Color color, float intensity)
        {
            Material material = Candy(color, 0.3f);
            SetGlow(material, color, intensity);
            return material;
        }

        public static void SetGlow(Material material, Color color, float intensity)
        {
            material.SetColor("_EmissionColor", color * (intensity * 0.35f));
        }

        /// <summary>Material para fitas da pista: cor do vértice, dois lados e deslocamento de profundidade.</summary>
        public static Material Ribbon(Texture2D texture, float roughness, float offset)
        {
            Material material = Candy(Color.white, roughness);
            material.SetFloat("_UseVertexColor", 1f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_OffsetFactor", offset);
            material.SetFloat("_OffsetUnits", offset);
            if (texture != null) material.SetTexture("_MainTex", texture);
            return material;
        }

        /// <summary>Material do céu em degradê, para RenderSettings.skybox.</summary>
        public static Material Sky(Color top, Color horizon)
        {
            Shader shader = Shader.Find(SkyShaderName);
            if (shader == null)
            {
                Debug.LogError($"[CandyMaterials] Shader '{SkyShaderName}' não encontrado.");
                return null;
            }
            var material = new Material(shader) { name = "Sky" };
            material.SetColor("_TopColor", top);
            material.SetColor("_HorizonColor", horizon);
            material.SetColor("_BottomColor", horizon);
            return material;
        }

        /// <summary>Material de bala com textura (listras, espiral, xadrez...).</summary>
        public static Material Textured(Texture2D texture, float roughness = 0.2f, Vector2? tiling = null, Color? tint = null)
        {
            Material material = Candy(tint ?? Color.white, roughness);
            material.SetTexture("_MainTex", texture);
            if (tiling.HasValue) material.SetTextureScale("_MainTex", tiling.Value);
            return material;
        }

        /// <summary>Desenha os dois lados da face (bandeiras, estrelas, placas finas).</summary>
        public static Material DoubleSided(Material material)
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
            return material;
        }

        /// <summary>
        /// Liga o "revestimento de bala" (candyCoat.ts da web): leve degradê pela
        /// altura e brilho na borda. Usado no donut.
        /// </summary>
        public static Material Coat(Material material, Color top, Color bottom, float rimPower, float strength = 0.18f)
        {
            material.SetColor("_CoatTop", top);
            material.SetColor("_CoatBottom", bottom);
            material.SetFloat("_CoatStrength", strength);
            material.SetFloat("_RimPower", rimPower);
            return material;
        }

        /// <summary>Material para texto 3D (TextMesh) que respeita a profundidade e a névoa.</summary>
        public static Material Text(Font font)
        {
            Shader shader = Shader.Find("CandyRush/Text3D");
            if (shader == null)
            {
                Debug.LogError("[CandyMaterials] Shader 'CandyRush/Text3D' não encontrado.");
                return font.material;
            }
            return new Material(shader) { name = "Text", mainTexture = font.material.mainTexture };
        }

        /// <summary>Cor de vértice no espaço de cor do projeto (cores de vértice não são convertidas sozinhas).</summary>
        public static Color VertexColor(Color color) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
    }
}
