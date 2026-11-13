// Material padrão do jogo: o MeshStandardMaterial do three.js, com cor por
// vértice, mapa de cor, mapa de normais, emissão e a "cobertura de bala"
// (fx/candyCoat.ts): degradê pela altura no mundo e brilho de borda.
Shader "CandyRush/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMapAmount ("Emission uses albedo map", Range(0,1)) = 0
        _CoatTop ("Coat Top", Color) = (1,1,1,1)
        _CoatBottom ("Coat Bottom", Color) = (1,1,1,1)
        _CoatRim ("Coat Rim", Float) = 0
        _CoatMix ("Coat Mix", Float) = 0
        _OffsetFactor ("Offset Factor", Float) = 0
        _OffsetUnits ("Offset Units", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Offset [_OffsetFactor], [_OffsetUnits]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert finalcolor:coatFinal
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _BumpScale;
        half _Glossiness;
        half _Metallic;
        half4 _EmissionColor;
        half _EmissionMapAmount;
        fixed4 _CoatTop;
        fixed4 _CoatBottom;
        half _CoatRim;
        half _CoatMix;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float4 vcolor;
            float3 wNormal;
            float3 worldPos;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcolor = v.color;
            o.wNormal = UnityObjectToWorldNormal(v.normal);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = tex.rgb * _Color.rgb * IN.vcolor.rgb;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BumpMap), _BumpScale);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb * lerp(fixed3(1,1,1), tex.rgb, _EmissionMapAmount);
            o.Alpha = 1;
        }

        void coatFinal(Input IN, SurfaceOutputStandard o, inout fixed4 color)
        {
            if (_CoatMix > 0)
            {
                float h = saturate((IN.worldPos.y + 1.5) / 12.0);
                float3 tint = lerp(_CoatBottom.rgb, _CoatTop.rgb, h);
                color.rgb = lerp(color.rgb, color.rgb * tint, _CoatMix);
                #ifndef UNITY_PASS_FORWARDADD
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.worldPos);
                float rim = pow(1.0 - max(0.0, dot(normalize(IN.wNormal), viewDir)), 2.0);
                color.rgb += rim * _CoatTop.rgb * _CoatRim;
                #endif
            }
        }
        ENDCG
    }
    FallBack "Diffuse"
}
