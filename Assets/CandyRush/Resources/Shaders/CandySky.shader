// O céu (skyShader.ts): degradê do horizonte ao topo, brilho rosado no
// horizonte, lua e estrelinhas. Usado como RenderSettings.skybox: a direção
// vem da posição do vértice do cubo do skybox, no mundo do Unity. O three.js
// espelha o eixo X, então o X é invertido aqui para a lua e as estrelas caírem
// exatamente onde caíam no navegador.
Shader "CandyRush/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.94, 0.51, 0.71, 1)
        _MidColor ("Mid", Color) = (0.98, 0.62, 0.70, 1)
        _HorizonColor ("Horizon", Color) = (1, 0.80, 0.54, 1)
        _MoonColor ("Moon", Color) = (1, 1, 1, 1)
        _CandyGlow ("Candy Glow", Color) = (1, 0.9, 0.6, 1)
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

            float3 _TopColor;
            float3 _MidColor;
            float3 _HorizonColor;
            float3 _MoonColor;
            float3 _CandyGlow;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.dir = vertex.xyz;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float starField(float3 dir)
            {
                float2 uv = float2(
                    atan2(dir.z, dir.x) * 0.15915494 + 0.5,
                    asin(clamp(dir.y, -1.0, 1.0)) * 0.31830989 + 0.5);
                float2 grid = floor(uv * float2(280.0, 120.0));
                float rnd = hash(grid);
                float star = smoothstep(0.986, 1.0, rnd);
                float twinkle = 0.45 + 0.55 * hash(grid + 17.0);
                return star * twinkle * smoothstep(-0.05, 0.45, dir.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(float3(-i.dir.x, i.dir.y, i.dir.z));
                float height = saturate(dir.y);

                float3 sky = lerp(_HorizonColor, _MidColor, smoothstep(0.04, 0.46, height));
                sky = lerp(sky, _TopColor, smoothstep(0.42, 0.95, height));

                float horizonGlow = exp(-pow(height * 6.5, 2.0));
                sky += _CandyGlow * horizonGlow * 0.025;

                float3 moonDir = normalize(float3(-0.38, 0.52, -0.72));
                float moonDisk = smoothstep(0.9986, 0.9995, dot(dir, moonDir));
                float moonHalo = pow(max(dot(dir, moonDir), 0.0), 42.0);
                sky += _MoonColor * moonDisk * 0.55;
                sky += _MoonColor * moonHalo * 0.035;

                float stars = starField(dir);
                sky += float3(1.0, 0.64, 0.86) * stars * 0.06;

                float vignette = smoothstep(-0.25, 0.85, dir.y);
                sky *= lerp(1.0, 0.98, vignette);

                // As propriedades Color já chegam no espaço de cor do projeto.
                // Evita uma segunda conversão que escureceria os tons pastel.
                // Alfa 0 marca "não passar pelo tone mapping" (ver ToneMap.shader).
                return fixed4(sky, 0.0);
            }
            ENDCG
        }
    }
}
