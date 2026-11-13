// O ACESFilmicToneMapping do three.js, com a exposição do original.
//
// O céu escreve alfa 0 e passa direto: no three ele tinha `toneMapped: false`.
Shader "Hidden/CandyRush/ToneMap"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Exposure ("Exposure", Float) = 1.05
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Exposure;

            float3 RRTAndODTFit(float3 v)
            {
                float3 a = v * (v + 0.0245786) - 0.000090537;
                float3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
                return a / b;
            }

            float3 ACESFilmic(float3 color)
            {
                color *= _Exposure / 0.6;
                // As matrizes do three estão em colunas (mat3 do GLSL).
                float3 c0 = float3(0.59719, 0.07600, 0.02840);
                float3 c1 = float3(0.35458, 0.90834, 0.13383);
                float3 c2 = float3(0.04823, 0.01566, 0.83777);
                color = c0 * color.x + c1 * color.y + c2 * color.z;
                color = RRTAndODTFit(color);
                float3 o0 = float3(1.60475, -0.10208, -0.00327);
                float3 o1 = float3(-0.53108, 1.10813, -0.07276);
                float3 o2 = float3(-0.07367, -0.00605, 1.07602);
                color = o0 * color.x + o1 * color.y + o2 * color.z;
                return saturate(color);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                if (c.a < 0.5) return float4(saturate(c.rgb), 1);
                return float4(ACESFilmic(c.rgb), 1);
            }
            ENDCG
        }
    }
}
