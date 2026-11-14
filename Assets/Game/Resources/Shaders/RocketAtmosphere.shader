// The sky (milestone 20): a shell at the top of a world's air, drawn from the inside faces so it works from the ground
// and from space. Each pixel integrates single Rayleigh + Mie scattering along the view ray (sunsets, the blue limb,
// the planet's shadow on its own air) and dims whatever lies behind (stars, the sun disc) by the air's transmittance.
// Ground pixels are covered by the planet itself, which applies the same air in its own shader.
Shader "Rocket/Atmosphere"
{
    Properties
    {
        _PlanetCenter ("Planet centre (world)", Vector) = (0, 0, 0, 0)
        _PlanetRadius ("Radius", Float) = 700000
        _AtmoRadius ("Air top radius", Float) = 775000
        _BetaR ("Rayleigh", Vector) = (0.0000058, 0.0000135, 0.0000331, 0)
        _BetaM ("Mie", Float) = 0.000007
        _HR ("Rayleigh height", Float) = 5800
        _HM ("Mie height", Float) = 1200
        _MieG ("Mie g", Float) = 0.76
        _SunIntensity ("Sun in air", Float) = 28
        _AtmoOn ("Has air", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Atmosphere"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _PlanetCenter;
                float _PlanetRadius;
                float _AtmoRadius;
                float4 _BetaR;
                float _BetaM;
                float _HR;
                float _HM;
                float _MieG;
                float _SunIntensity;
                float _AtmoOn;
            CBUFFER_END
            #define RK_ATMOSPHERE
            #include "RkCommon.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 cam = GetCameraPositionWS();
                float3 rd = normalize(i.positionWS - cam);
                Light sun = GetMainLight();
                float3 trans;
                float3 ins = RkScatter(cam - _PlanetCenter.xyz, rd, 1e12, sun.direction, 16, trans);
                float a = saturate(1 - dot(trans, 1.0 / 3.0));
                // weather haze hides the sky (premultiplied: blend the colour and the cover toward it)
                float fog = RkFogAmount(length(i.positionWS - cam));
                return half4(lerp(ins, _RkFog.rgb, fog), lerp(a, 1, fog));
            }
            ENDHLSL
        }
    }
}
