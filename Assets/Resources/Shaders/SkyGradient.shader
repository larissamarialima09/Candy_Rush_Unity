// Céu em degradê para o Skybox: azul-claro no alto, rosa no horizonte.
// A cor do horizonte é a mesma da névoa, para o chão distante sumir no céu.
Shader "CandyRush/SkyGradient"
{
    Properties
    {
        _TopColor ("Topo", Color) = (0.72, 0.86, 1, 1)
        _HorizonColor ("Horizonte", Color) = (1, 0.9, 0.96, 1)
        _BottomColor ("Embaixo", Color) = (1, 0.9, 0.96, 1)
        _Exponent ("Curva do degradê", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            fixed4 _BottomColor;
            float _Exponent;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float h = normalize(i.dir).y;
                if (h >= 0) return lerp(_HorizonColor, _TopColor, pow(saturate(h), _Exponent));
                return lerp(_HorizonColor, _BottomColor, saturate(-h * 4));
            }
            ENDCG
        }
    }
}
