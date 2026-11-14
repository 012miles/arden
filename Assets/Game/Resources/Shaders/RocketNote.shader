// The sticky note on an Ardling's visor (milestone 20): matte paper with a marker face, lit like everything else.
Shader "Rocket/Note"
{
    Properties
    {
        _MainTex ("Face", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "RkCommon.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 nWS : TEXCOORD0; float3 pWS : TEXCOORD1; float2 uv : TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.pWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.pWS);
                o.nWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.nWS) * (front ? 1 : -1);
                float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                if (!front) albedo = float3(0.9, 0.88, 0.8) * albedo.r;   // the back is plain paper
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.pWS));
                float3 col = albedo * (sun.color * sun.shadowAttenuation * saturate(dot(n, sun.direction)) * 0.95 + RkAmbient(n));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
