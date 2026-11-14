// Clouds and rings (milestone 20): a vertex-coloured, alpha-blended shell lit by sunlight that has come through the
// air (golden at sunset, gone on the night side), with a soft sky fill and the same distance haze as the ground.
Shader "Rocket/Cloud"
{
    Properties
    {
        _Opacity ("Opacity", Range(0, 1)) = 1
        _PlanetCenter ("Planet centre (world)", Vector) = (0, 0, 0, 0)
        _PlanetRadius ("Radius", Float) = 700000
        _AtmoRadius ("Air top radius", Float) = 775000
        _BetaR ("Rayleigh", Vector) = (0.0000058, 0.0000135, 0.0000331, 0)
        _BetaM ("Mie", Float) = 0.000007
        _HR ("Rayleigh height", Float) = 5800
        _HM ("Mie height", Float) = 1200
        _MieG ("Mie g", Float) = 0.76
        _SunIntensity ("Sun in air", Float) = 28
        _AtmoOn ("Has air", Float) = 0
        _SkyColor ("Sky fill", Color) = (0.36, 0.58, 0.9, 1)
        _HasMaps ("Baked clouds ready", Float) = 0
        _CloudMap ("Baked clouds", Cube) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Cloud"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Opacity;
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
                float4 _SkyColor;
                float _HasMaps;
            CBUFFER_END
            TEXTURECUBE(_CloudMap); SAMPLER(sampler_CloudMap);
            #define RK_ATMOSPHERE
            #include "RkCommon.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 color : COLOR; float3 posOS : TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.posOS = v.positionOS.xyz;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                Light sun = GetMainLight();
                float3 n = normalize(i.normalWS);
                float3 p = i.positionWS - _PlanetCenter.xyz;
                float3 cam = GetCameraPositionWS();
                float3 toCam = cam - i.positionWS;
                float dist = length(toCam);
                // Thin cloud lets light through: lit from either side, softly
                float wrap = saturate(dot(n, sun.direction) * 0.6 + 0.4);
                float3 sunHere = _RkSunSpace.rgb * RkSunTransmittance(p + n * 50, sun.direction);
                float day = smoothstep(-0.15, 0.3, dot(normalize(p), sun.direction));
                float3 col = i.color.rgb * (sunHere * wrap + _RkSunSpace.rgb * _SkyColor.rgb * 0.18 * day);
                float3 trans;
                float3 ins = RkScatter(cam - _PlanetCenter.xyz, -toCam / dist, dist, sun.direction, 8, trans);
                col = col * trans + ins;
                // Baked cover (sharper than the shell's vertices) once it's ready
                float cover = _HasMaps > 0.5 ? SAMPLE_TEXTURECUBE(_CloudMap, sampler_CloudMap, normalize(i.posOS)).r * 0.92 : i.color.a;
                col = RkApplyFog(col, dist);
                return half4(col, cover * _Opacity);
            }
            ENDHLSL
        }
    }
}
