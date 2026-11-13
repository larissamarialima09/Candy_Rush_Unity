// Variante transparente do material padrão: o vidro da máquina de doces e os
// potes. Não escreve profundidade, igual ao `depthWrite: false` do original —
// sem isso o globo apagaria as balas de dentro conforme a câmera gira.
Shader "CandyRush/LitTransparent"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.5)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.95
        _Metallic ("Metallic", Range(0,1)) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _CoatTop ("Coat Top", Color) = (1,1,1,1)
        _CoatBottom ("Coat Bottom", Color) = (1,1,1,1)
        _CoatRim ("Coat Rim", Float) = 0
        _CoatMix ("Coat Mix", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 300
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert finalcolor:coatFinal
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        half4 _EmissionColor;
        fixed4 _CoatTop;
        fixed4 _CoatBottom;
        half _CoatRim;
        half _CoatMix;

        struct Input
        {
            float2 uv_MainTex;
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
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = _Color.a * tex.a;
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
                color.rgb += rim * _CoatTop.rgb * _CoatRim * color.a;
                #endif
            }
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
