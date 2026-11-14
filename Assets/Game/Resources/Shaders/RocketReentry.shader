// Reentry plasma (milestone 23): additive glowing gas around a vessel coming in hot. A shock layer cups the leading
// face (y > 0) and a long sheath streams back along -y with flowing streaks, white-yellow where it's hottest, through
// orange to a red-violet fringe. Mesh: unit radius around +y; vertex colour r = 0 shock cap, 1 outer sheath, 2 inner core.
Shader "Rocket/Reentry"
{
    Properties
    {
        _Strength ("Strength (0..1)", Float) = 0.5
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+2" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Reentry"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RkCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 pOS : TEXCOORD0; float3 nWS : TEXCOORD1; float3 pWS : TEXCOORD2; float layer : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = v.positionOS.xyz;
                // The sheath billows out with strength and wavers
                if (v.color.r > 0.5)
                {
                    float t = saturate(-p.y);
                    float wave = 1 + 0.12 * sin(t * 18 - _Time.y * 9 + _Seed) * t + 0.25 * _Strength * sin(3.1416 * saturate(t * 1.4));
                    p.xz *= wave;
                }
                o.pOS = p;
                o.pWS = TransformObjectToWorld(p);
                o.positionHCS = TransformWorldToHClip(o.pWS);
                o.nWS = TransformObjectToWorldNormal(v.normalOS);
                o.layer = v.color.r;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 v = normalize(GetCameraPositionWS() - i.pWS);
                float facing = saturate(abs(dot(normalize(i.nWS), v)));
                float t = saturate(-i.pOS.y);                          // 0 at the leading face, 1 at the tail
                float ang = atan2(i.pOS.z, i.pOS.x);
                // Streaks: noise stretched along the flow, scrolling back fast
                float flow = _Time.y * 6.0;
                float streak = RkNoise3(float3(ang * 2.6 + _Seed, i.pOS.y * 1.6 + flow, _Seed)) * 0.65 + RkNoise3(float3(ang * 7.0, i.pOS.y * 4.0 + flow * 1.7, 3.0 + _Seed)) * 0.35;
                streak = smoothstep(0.25, 0.85, streak);
                float flick = 0.85 + 0.15 * sin(_Time.y * 47 + _Seed * 5 + ang * 3);
                float3 hot = float3(1.0, 0.92, 0.72), mid = float3(1.0, 0.45, 0.12), cool = float3(0.75, 0.18, 0.35);
                float3 col = t < 0.35 ? lerp(hot, mid, t / 0.35) : lerp(mid, cool, (t - 0.35) / 0.65);
                float k;
                if (i.layer < 0.5)
                {
                    // Shock cap: brightest straight ahead of the shield, a glowing rim at its edge
                    float rim = pow(1 - facing, 2);
                    k = (0.22 * facing + 1.3 * rim) * (0.8 + 0.4 * streak);     // see-through in the middle, a hot glowing edge
                    col = lerp(float3(1.0, 0.62, 0.3), float3(1.0, 0.88, 0.62), rim);
                }
                else if (i.layer < 1.5)
                {
                    // Outer sheath: thin at the edges, fading down the tail, broken into streaks
                    k = pow(facing, 1.4) * pow(1 - t, 1.6) * (0.35 + 0.9 * streak) * 1.4;
                }
                else
                {
                    // Inner core: a hotter, shorter tongue
                    k = pow(facing, 2) * pow(1 - t, 3) * (0.6 + 0.6 * streak) * 2.6;
                    col = lerp(hot, mid, t);
                }
                float s = saturate(_Strength);
                return half4(col * k * flick * s * (0.5 + 2.5 * s), 0);
            }
            ENDHLSL
        }
    }
}
