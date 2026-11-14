// Engine exhaust (milestone 20): additive glowing plume on a unit cylinder (nozzle at y = 0, tip at y = -1).
// The vertex shader shapes it: tight with shock diamonds in thick air, ballooning and faint in vacuum.
// Vertex colour r: 0 = outer sheath, 1 = hot inner core.
Shader "Rocket/Plume"
{
    Properties
    {
        _CoreColor ("Core", Color) = (1, 0.9, 0.7, 1)
        _EdgeColor ("Edge", Color) = (1, 0.45, 0.12, 1)
        _Intensity ("Intensity", Float) = 4
        _Expand ("Vacuum expansion", Range(0, 1)) = 0
        _Diamonds ("Shock diamonds", Range(0, 1)) = 0.6
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+2" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Plume"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _EdgeColor;
                float _Intensity;
                float _Expand;
                float _Diamonds;
                float _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float t : TEXCOORD0; float core : TEXCOORD1; float3 nWS : TEXCOORD2; float3 pWS : TEXCOORD3; float ang : TEXCOORD4; };

            float Radius(float t, float core)
            {
                // Thick air squeezes the jet; vacuum lets it spread wide
                float sea = lerp(1.0, 0.45, t) * (1 + 0.08 * sin(t * 40));
                float vac = 1 + 2.6 * pow(t, 0.7);
                return lerp(sea, vac, _Expand) * (core > 0.5 ? 0.45 : 1);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float t = saturate(-v.positionOS.y);
                float2 xz = v.positionOS.xz;
                float r = Radius(t, v.color.r);
                float3 pos = float3(xz.x * r, v.positionOS.y * lerp(1, 1.25, _Expand) * (v.color.r > 0.5 ? 0.55 : 1), xz.y * r);
                o.pWS = TransformObjectToWorld(pos);
                o.positionHCS = TransformWorldToHClip(o.pWS);
                o.nWS = TransformObjectToWorldNormal(float3(xz.x, 0, xz.y));
                o.t = t;
                o.core = v.color.r;
                o.ang = atan2(xz.y, xz.x);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 v = normalize(GetCameraPositionWS() - i.pWS);
                // Looking through the middle of the jet you see more glowing gas than at its edges
                float thick = pow(saturate(abs(dot(normalize(i.nWS), v))), 1.5);
                float t = i.t;
                float along = pow(1 - t, i.core > 0.5 ? 2.2 : 1.4) * smoothstep(0, 0.04, t + 0.01);
                float flick = 0.85 + 0.15 * sin(_Time.y * 61 + _Seed * 7 + t * 23) * sin(_Time.y * 37 + i.ang * 3);
                float diamonds = 1 + _Diamonds * (1 - _Expand) * 1.6 * pow(saturate(cos(t * 6.283 * 5.5)), 8) * (1 - t);
                float3 c = lerp(_EdgeColor.rgb, _CoreColor.rgb, saturate(i.core * 0.7 + (1 - t) * 0.5 - _Expand * 0.3));
                float k = along * thick * flick * diamonds * _Intensity * (i.core > 0.5 ? 1.6 : 0.55) * lerp(1, 0.35, _Expand);
                return half4(c * k, 0);
            }
            ENDHLSL
        }
    }
}
