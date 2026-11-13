Shader "CandyRush/ShellFur"
{
    Properties
    {
        _Color ("Base color", Color) = (1,1,1,1)
        _MainTex ("Color texture", 2D) = "white" {}
        _NoiseTex ("Strand noise (linear)", 2D) = "white" {}
        _FurMask ("Fur mask R: white = fur", 2D) = "white" {}
        _FurLength ("Fur length (local meters)", Range(0,0.08)) = 0.018
        _Density ("Density", Range(0.01,1)) = 0.65
        _NoiseScale ("Strands per UV", Range(1,128)) = 24
        _LayerCount ("Layers (mesh built by FurController)", Range(8,16)) = 12
        _FurDirection ("World gravity / grooming", Vector) = (0,-0.35,0,0)
        _RimColor ("Rim color", Color) = (1,1,1,1)
        _RimPower ("Rim power", Range(1,8)) = 3
        _RimStrength ("Rim strength", Range(0,0.25)) = 0.08
    }
    SubShader
    {
        // Cutout coverage avoids transparency sorting between shells and racers.
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Back ZWrite On
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #include "UnityCG.cginc"
        sampler2D _MainTex, _NoiseTex, _FurMask;
        float4 _FurMask_ST;
        fixed4 _Color, _RimColor;
        float _FurLength, _Density, _NoiseScale, _LayerCount;
        float4 _FurDirection;
        half _RimPower, _RimStrength;
        // Custom UV name prevents Surface Shader from generating _FurMask_ST a
        // second time. The vertex stage owns both the UV transform and sampling.
        struct Input { float2 uv_MainTex; float2 furMaskUV; float layer; float3 viewDir; };
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // UV3.x stores the one-based shell index, supplied by FurController.
            float layer = v.texcoord2.x / max(8.0, _LayerCount);
            float2 maskUV = TRANSFORM_TEX(v.texcoord.xy, _FurMask);
            float mask = tex2Dlod(_FurMask, float4(maskUV,0,0)).r;
            float3 directionOS = mul((float3x3)unity_WorldToObject, _FurDirection.xyz);
            v.vertex.xyz += _FurLength * mask * (v.normal * layer + directionOS * layer * layer);
            o.layer = layer;
            o.furMaskUV = maskUV;
        }
        void surf(Input i, inout SurfaceOutputStandard o)
        {
            float mask = tex2D(_FurMask, i.furMaskUV).r;
            float3 noise = tex2D(_NoiseTex, i.uv_MainTex * _NoiseScale).rgb;
            clip(mask - 0.01);
            // R defines coherent strands; G dithers the decreasing outer opacity.
            clip(noise.r - (1.0 - _Density) - i.layer * _Density * 0.72);
            clip(pow(saturate(1.0 - i.layer * 0.94), 0.65) * mask - noise.g);
            fixed3 color = tex2D(_MainTex, i.uv_MainTex).rgb * _Color.rgb;
            o.Albedo = color * lerp(0.72, 1.0, i.layer);
            o.Smoothness = 0.08;
            o.Metallic = 0;
            o.Occlusion = lerp(0.8, 1.0, i.layer);
            // A soft, coat-tinted edge; bounded intensity keeps fur from becoming
            // an HDR light source under bloom, including on older materials.
            o.Emission = color * saturate(_RimColor.rgb) * clamp(_RimStrength, 0, 0.25)
                * pow(1-saturate(dot(normalize(i.viewDir), o.Normal)), _RimPower);
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "CandyRush/FurLite"
}
