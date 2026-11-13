Shader "CandyRush/FurLite"
{
    Properties
    {
        _Color ("Base color", Color) = (1,1,1,1)
        _MainTex ("Fur color texture", 2D) = "white" {}
        [Normal] _BumpMap ("Fur normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = 0.5
        _SmoothMask ("Smoothness mask R (inner ears / nose)", 2D) = "black" {}
        _Glossiness ("Fur smoothness", Range(0,1)) = 0.12
        _DetailSmoothness ("Masked smoothness", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows addshadow
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex, _BumpMap, _SmoothMask;
        fixed4 _Color;
        half _BumpScale, _Glossiness, _DetailSmoothness;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float2 uv_SmoothMask; };
        void surf(Input i, inout SurfaceOutputStandard o)
        {
            o.Albedo = tex2D(_MainTex, i.uv_MainTex).rgb * _Color.rgb;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, i.uv_BumpMap), _BumpScale);
            o.Smoothness = lerp(_Glossiness, _DetailSmoothness, tex2D(_SmoothMask, i.uv_SmoothMask).r);
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Standard"
}
