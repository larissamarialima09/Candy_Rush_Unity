// Material "bala" do Candy Rush: brilhante, sem metal, com névoa e sombras.
// Usado por tudo que é desenhado por código (pista, terreno, barreiras, kart).
//
// _UseVertexColor = 1 multiplica pela cor do vértice (zebra, largada, escape).
// _Cull = 0 desenha os dois lados (as fitas da pista, como o DoubleSide da web).
// _OffsetFactor/_OffsetUnits evitam briga de profundidade entre camadas da pista.
// _CoatStrength > 0 liga o "revestimento de bala" (candyCoat.ts da web): um leve
// degradê pela altura do mundo (_CoatBottom → _CoatTop) e um brilho na borda.
Shader "CandyRush/Candy"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _MainTex ("Textura", 2D) = "white" {}
        _Glossiness ("Brilho", Range(0,1)) = 0.6
        _EmissionColor ("Emissão", Color) = (0,0,0,1)
        _UseVertexColor ("Usar cor do vértice", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _OffsetFactor ("Offset Factor", Float) = 0
        _OffsetUnits ("Offset Units", Float) = 0
        _CoatTop ("Revestimento: topo", Color) = (1,1,1,1)
        _CoatBottom ("Revestimento: base", Color) = (1,1,1,1)
        _CoatStrength ("Revestimento: força", Float) = 0
        _RimPower ("Revestimento: brilho da borda", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull [_Cull]
        Offset [_OffsetFactor], [_OffsetUnits]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        fixed4 _EmissionColor;
        float _UseVertexColor;
        fixed4 _CoatTop;
        fixed4 _CoatBottom;
        float _CoatStrength;
        float _RimPower;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
            float3 worldPos;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 vertexColor = lerp(fixed4(1,1,1,1), IN.color, _UseVertexColor);
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color * vertexColor;
            o.Albedo = c.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb;

            if (_CoatStrength > 0)
            {
                float height = saturate((IN.worldPos.y + 1.5) / 12.0);
                fixed3 tint = lerp(_CoatBottom.rgb, _CoatTop.rgb, height);
                o.Albedo = lerp(o.Albedo, o.Albedo * tint, _CoatStrength);
                float rim = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), 2.0);
                o.Emission += rim * _CoatTop.rgb * _RimPower;
            }
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
