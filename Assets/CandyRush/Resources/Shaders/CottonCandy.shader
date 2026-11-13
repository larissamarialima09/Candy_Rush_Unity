// Algodão-doce (fx/cottonCandy.ts): três camadas do mesmo ruído fbm —
// contorno (vértice), felpudo (normal) e mechas/rodamoinho (cor) — mais brilho
// de borda e cintilas de açúcar.
//
// É um shader de vértice/fragmento próprio, e não surface shader, porque o
// felpudo mexe na normal em espaço de MUNDO, e o surface shader só aceita
// normal em espaço tangente.
Shader "CandyRush/CottonCandy"
{
    Properties
    {
        _Base ("Base", Color) = (1, 0.75, 0.86, 1)
        _Swirl ("Swirl", Color) = (0.77, 0.93, 1, 1)
        _Rim ("Rim", Color) = (1, 0.88, 0.94, 1)
        _PuffFrequency ("Puff Frequency", Float) = 0.14
        _FiberFrequency ("Fiber Frequency", Float) = 1.1
        _Relief ("Relief", Float) = 0.85
        _Fuzz ("Fuzz", Float) = 0.85
        _RimStrength ("Rim Strength", Float) = 0.5
        _EmissionStrength ("Emission", Float) = 0.06
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    float ccHash(float3 p)
    {
        p = frac(p * 0.3183099 + float3(0.71, 0.113, 0.419));
        p *= 17.0;
        return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
    }

    float ccNoise(float3 x)
    {
        float3 i = floor(x);
        float3 f = frac(x);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(
            lerp(
                lerp(ccHash(i + float3(0, 0, 0)), ccHash(i + float3(1, 0, 0)), f.x),
                lerp(ccHash(i + float3(0, 1, 0)), ccHash(i + float3(1, 1, 0)), f.x),
                f.y),
            lerp(
                lerp(ccHash(i + float3(0, 0, 1)), ccHash(i + float3(1, 0, 1)), f.x),
                lerp(ccHash(i + float3(0, 1, 1)), ccHash(i + float3(1, 1, 1)), f.x),
                f.y),
            f.z);
    }

    float ccFbm(float3 p)
    {
        float sum = 0.0;
        float amp = 0.5;
        for (int i = 0; i < 4; i++)
        {
            sum += amp * ccNoise(p);
            p *= 2.03;
            amp *= 0.5;
        }
        return sum;
    }

    float _PuffFrequency;
    float _Relief;

    // O empurrão usa a posição de OBJETO como semente, como no original.
    float4 ccDisplace(float4 vertex, float3 normal)
    {
        float lift = (ccFbm(vertex.xyz * _PuffFrequency) - 0.5) * _Relief;
        vertex.xyz += normal * lift;
        return vertex;
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            float4 _Base;
            float4 _Swirl;
            float4 _Rim;
            float _FiberFrequency;
            float _Fuzz;
            float _RimStrength;
            float _EmissionStrength;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                SHADOW_COORDS(2)
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                v.vertex = ccDisplace(v.vertex, v.normal);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);

                // Felpudo: a inclinação do ruído vira desvio da normal.
                float3 p = i.worldPos * _FiberFrequency;
                float center = ccFbm(p);
                float3 gradient = float3(
                    ccFbm(p + float3(0.35, 0, 0)) - center,
                    ccFbm(p + float3(0, 0.35, 0)) - center,
                    ccFbm(p + float3(0, 0, 0.35)) - center);
                normal = normalize(normal - gradient * (_Fuzz * 6.0));

                // Rodamoinho entre as duas cores e mechas puxadas na vertical.
                float swirl = ccFbm(i.worldPos * (_PuffFrequency * 0.85));
                float3 tint = lerp(_Base.rgb, _Swirl.rgb, smoothstep(0.34, 0.70, swirl));
                float3 fiberCoord = i.worldPos * _FiberFrequency;
                fiberCoord.y *= 0.22;
                float fiber = ccFbm(fiberCoord);
                tint *= 0.88 + 0.30 * smoothstep(0.30, 0.82, fiber);

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float nl = max(0.0, dot(normal, lightDir));
                float shadow = SHADOW_ATTENUATION(i);
                float3 ambient = ShadeSH9(float4(normal, 1.0));
                float3 color = tint * (ambient + _LightColor0.rgb * nl * shadow);
                color += _Base.rgb * _EmissionStrength;

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float rim = pow(1.0 - saturate(dot(normalize(i.worldNormal), viewDir)), 2.2);
                color += rim * _Rim.rgb * _RimStrength;

                float sparkle = ccNoise(i.worldPos * 19.0);
                color += smoothstep(0.88, 0.99, sparkle) * float3(1.0, 0.97, 0.99) * 0.28;

                fixed4 result = fixed4(color, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                v.vertex = ccDisplace(v.vertex, v.normal);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
