// The night sky (milestone 20): NASA's Milky Way star map on a huge sphere around the camera, drawn first so
// everything else covers it. Turning the sphere turns the sky; _Exposure fades it in daylight.
// Star map: NASA/Goddard Space Flight Center Scientific Visualization Studio; Gaia DR2: ESA/Gaia/DPAC.
Shader "Rocket/Stars"
{
    Properties
    {
        _Stars ("Star map", Cube) = "black" {}
        _Exposure ("Exposure", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Stars"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURECUBE(_Stars); SAMPLER(sampler_Stars);
            CBUFFER_START(UnityPerMaterial)
                float _Exposure;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 dir : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 c = SAMPLE_TEXTURECUBE(_Stars, sampler_Stars, normalize(i.dir)).rgb;
                return half4(c * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
