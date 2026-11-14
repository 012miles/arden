// Exhaust smoke and steam (milestone 20): soft billowing particle puffs, glowing near the flame, lit by the sun and
// the sky fill like everything else. Particle colour: rgb tint (above 1 = glowing), a = opacity.
Shader "Rocket/Smoke"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+3" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Smoke"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "RkCommon.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 pWS : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.pWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.pWS);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 q = i.uv * 2 - 1;
                float r = length(q);
                // A lumpy round puff: noise eats into the edge
                float n = RkNoise3(float3(q * 2.3, i.pWS.x * 0.07 + i.pWS.z * 0.05)) * 0.6 + RkNoise3(float3(q * 5.1, i.pWS.y * 0.09)) * 0.4;
                float a = saturate((1 - r) * 1.8 - (n - 0.5) * 0.9) * i.color.a;
                if (a <= 0.003) discard;
                Light sun = GetMainLight();
                // Top of the puff faces the sun more than the bottom: cheap volume shading from the disc position
                float3 fakeN = normalize(float3(q.x, q.y, 0.6));
                float3 toSun = sun.direction;
                float lit = 0.55 + 0.45 * saturate(dot(fakeN, normalize(mul((float3x3)UNITY_MATRIX_V, toSun))));
                float3 tint = i.color.rgb;
                float3 glow = max(tint - 1, 0);
                float3 base = min(tint, 1);
                float3 col = base * (sun.color * lit * 0.8 + _RkAmbSky.rgb * 0.9 + _RkAmbGround.rgb * 0.5) + glow * 3;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
