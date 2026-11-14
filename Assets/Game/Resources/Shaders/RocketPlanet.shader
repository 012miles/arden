// Ground of a world (milestones 20a/20b): both the whole-planet sphere (far camera) and the close-up patch (near camera).
// Sunlight is reddened by the air on its way down, airless dust scatters light the way regolith does, seas glint,
// and distant ground fades into the sky colour through the same single-scattering air as the sky shell.
// From orbit the sphere uses baked colour/relief cubemaps (PlanetBaker); up close the patch gets photo textures of the
// world's kinds of ground (grass, rock, sand, snow, gravel...) at three sizes that fade in as you get closer.
// Vertex colour = ground colour; UV0 = (open water, ground kind A, B, C). Object space = planet-fixed axes.
Shader "Rocket/Planet"
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
        _SkyColor ("Sky fill", Color) = (0.36, 0.58, 0.9, 1)
        _Regolith ("Dusty airless ground", Range(0, 1)) = 0
        _AlbedoScale ("Albedo scale", Float) = 1
        _Ambient ("Fill", Range(0, 1)) = 0.035
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Range(0, 1)) = 0
        _PatchCenterPF ("Patch centre, planet-fixed (w = 1 on the patch)", Vector) = (0, 0, 0, 0)
        _PatchOffset ("Patch centre mod 1024 m", Vector) = (0, 0, 0, 0)
        _Layers ("Ground layers: base, A, B, C", Vector) = (0, 2, 3, 7)
        _LayerRock ("Rock layer", Float) = 1
        _Detail ("Detail strength", Float) = 1
        _HasMaps ("Baked maps ready", Float) = 0
        _PatchHalf ("Patch half size (m)", Float) = 3000
        _MapBlend ("Shade the patch with the baked relief", Float) = 0
        _CloudsOn ("Clouds", Float) = 0
        _CloudPaint ("Clouds painted on the patch from above", Float) = 0
        _TerrainA ("Ground photos", 2DArray) = "" {}
        _TerrainN ("Ground normals", 2DArray) = "" {}
        _ColorMap ("Baked colour", Cube) = "white" {}
        _NormalMap ("Baked relief", Cube) = "white" {}
        _CloudMap ("Baked clouds", Cube) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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
            float4 _SkyColor;
            float _Regolith;
            float _AlbedoScale;
            float _Ambient;
            float4 _Tint;
            float _Emission;
            float4 _PatchCenterPF;
            float4 _PatchOffset;
            float4 _Layers;
            float _LayerRock;
            float _Detail;
            float _HasMaps;
            float _CloudsOn;
            float _CloudPaint;
            float _PatchHalf;
            float _MapBlend;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define RK_ATMOSPHERE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "RkCommon.hlsl"

            TEXTURE2D_ARRAY(_TerrainA); SAMPLER(sampler_TerrainA);
            TEXTURE2D_ARRAY(_TerrainN);
            TEXTURECUBE(_ColorMap); SAMPLER(sampler_ColorMap);
            TEXTURECUBE(_NormalMap);
            TEXTURECUBE(_CloudMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; float4 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : COLOR;
                float4 ground : TEXCOORD2;     // water, A, B, C
                float3 posOS : TEXCOORD3;
                float3 normalOS : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.ground = v.uv;
                o.posOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                return o;
            }

            // One ground photo layer at one size: triplanar in planet-fixed axes (positions offset so the pattern stays
            // put when the patch is rebuilt), returning colour relative to its average (~1) and a bent normal.
            void Layer(float3 p, float3 dir, float3 w3, float layer, float size, inout float3 rel, inout float3 nAdd, float weight)
            {
                float3 c = 0, n = 0;
                [branch] if (w3.x > 0.02)
                {
                    float2 uv = p.zy / size;
                    c += SAMPLE_TEXTURE2D_ARRAY(_TerrainA, sampler_TerrainA, uv, layer).rgb * w3.x;
                    float3 t = SAMPLE_TEXTURE2D_ARRAY(_TerrainN, sampler_TerrainA, uv, layer).xyz * 2 - 1;
                    n += float3(0, t.y, t.x) * sign(dir.x) * w3.x;
                }
                [branch] if (w3.y > 0.02)
                {
                    float2 uv = p.xz / size;
                    c += SAMPLE_TEXTURE2D_ARRAY(_TerrainA, sampler_TerrainA, uv, layer).rgb * w3.y;
                    float3 t = SAMPLE_TEXTURE2D_ARRAY(_TerrainN, sampler_TerrainA, uv, layer).xyz * 2 - 1;
                    n += float3(t.x, 0, t.y) * w3.y;
                }
                [branch] if (w3.z > 0.02)
                {
                    float2 uv = p.xy / size;
                    c += SAMPLE_TEXTURE2D_ARRAY(_TerrainA, sampler_TerrainA, uv, layer).rgb * w3.z;
                    float3 t = SAMPLE_TEXTURE2D_ARRAY(_TerrainN, sampler_TerrainA, uv, layer).xyz * 2 - 1;
                    n += float3(t.x, t.y, 0) * sign(dir.z) * w3.z;
                }
                float wsum = max(w3.x * (w3.x > 0.02) + w3.y * (w3.y > 0.02) + w3.z * (w3.z > 0.02), 1e-3);
                rel += (c / wsum * 2 - 1) * weight;      // stored around 0.5
                nAdd += n / wsum * weight;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 camWS = GetCameraPositionWS();
                float3 p = i.positionWS - _PlanetCenter.xyz;
                float3 up = normalize(p);
                float3 toCam = camWS - i.positionWS;
                float dist = length(toCam);
                float3 v = toCam / dist;
                bool patch = _PatchCenterPF.w > 0.5;
                float3 dirPF = normalize(i.posOS + _PatchCenterPF.xyz);
                float3 nGeoOS = normalize(i.normalOS);
                float3 nOS = nGeoOS;
                float3 albedo = i.color.rgb;
                float water = i.ground.x;

                // From orbit: the baked maps are sharper than the sphere's vertices
                [branch] if (!patch && _HasMaps > 0.5)
                {
                    float4 cm = SAMPLE_TEXTURECUBE(_ColorMap, sampler_ColorMap, dirPF);
                    albedo = cm.rgb;
                    water = cm.a;
                    float3 nm = SAMPLE_TEXTURECUBE(_NormalMap, sampler_ColorMap, dirPF).xyz * 2 - 1;
                    nOS = normalize(nm);
                }
                // Milestone 25: toward its edge the close-up ground fades into the baked colour the far planet is drawn with,
                // so there's no square seam where one meets the other
                float patchEdge = 0;
                [branch] if (patch && _HasMaps > 0.5)
                {
                    patchEdge = smoothstep(0.74, 0.97, length(i.posOS) / max(_PatchHalf, 1.0));
                    float mapK = max(patchEdge, _MapBlend);
                    if (mapK > 0.001)
                    {
                        float4 cm = SAMPLE_TEXTURECUBE(_ColorMap, sampler_ColorMap, dirPF);
                        albedo = lerp(albedo, cm.rgb, mapK);
                        water = lerp(water, cm.a, mapK);
                    }
                    // Milestone 26: high up, crater rims and ridges come from the baked relief (finer than the vertices then)
                    if (_MapBlend > 0.001)
                    {
                        float3 nm = SAMPLE_TEXTURECUBE(_NormalMap, sampler_ColorMap, dirPF).xyz * 2 - 1;
                        nOS = normalize(lerp(nOS, normalize(nm), _MapBlend));
                    }
                }
                albedo *= _Tint.rgb * _AlbedoScale;

                // Up close: photo textures of the ground, blended by kind, at three sizes fading in with distance
                [branch] if (patch && _Detail > 0 && water < 0.5)
                {
                    float f1 = 1 - smoothstep(40, 260, dist), f2 = 1 - smoothstep(500, 3500, dist), f3 = 1 - smoothstep(4000, 30000, dist);
                    if (f3 > 0.001)
                    {
                        float3 pT = i.posOS + _PatchOffset.xyz;
                        float3 w3 = pow(abs(dirPF), 6);
                        w3 /= dot(w3, 1);
                        float rock = smoothstep(0.08, 0.24, 1 - dot(nGeoOS, dirPF));
                        float4 lw = float4(0, i.ground.y, i.ground.z, i.ground.w);
                        lw.x = saturate(1 - lw.y - lw.z - lw.w);
                        lw /= max(dot(lw, 1), 1e-3);
                        lw *= 1 - rock;
                        float3 rel = 0, nAdd = 0;
                        float layers[5] = { _Layers.x, _Layers.y, _Layers.z, _Layers.w, _LayerRock };
                        float wts[5] = { lw.x, lw.y, lw.z, lw.w, rock };
                        [loop] for (int k = 0; k < 5; k++)
                        {
                            float wk = wts[k];
                            if (wk < 0.02) continue;
                            if (f1 > 0.001) Layer(pT, dirPF, w3, layers[k], 1.7, rel, nAdd, wk * f1 * 0.55);
                            if (f2 > 0.001) Layer(pT, dirPF, w3, layers[k], 13.6, rel, nAdd, wk * f2 * 0.45);
                            Layer(pT, dirPF, w3, layers[k], 108.8, rel, nAdd, wk * f3 * 0.4);
                        }
                        albedo *= saturate(1 + rel * 0.9 * _Detail);
                        nOS = normalize(nOS + nAdd * 0.9 * _Detail);
                    }
                }
                float3 n = TransformObjectToWorldNormal(nOS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);
                float3 L = sun.direction;
                float3 sunTop = _RkSunSpace.rgb * RkSunTransmittance(p, L);
                float3 sunAtGround = sunTop * sun.shadowAttenuation;
                float3 Lpf = TransformWorldToObjectDir(L);
                [branch] if (_CloudsOn > 0.5 && _HasMaps > 0.5)
                {
                    // Cloud shadows: the cover where the sunbeam crosses the cloud layer (5 km up)
                    float3 s = normalize(dirPF + Lpf * (5000.0 / _PlanetRadius) / max(dot(dirPF, Lpf), 0.15));
                    sunAtGround *= 1 - 0.6 * SAMPLE_TEXTURECUBE(_CloudMap, sampler_ColorMap, s).r;
                }
                float nl = saturate(dot(n, L)), nv = saturate(dot(n, v));
                // Dusty airless ground (Lommel-Seeliger) looks evenly bright right up to the terminator
                float ls = 2 * nl / max(nl + nv, 1e-3) * 0.5;
                float diffuse = lerp(nl, ls, _Regolith * 0.6);
                float daylight = smoothstep(-0.12, 0.35, dot(up, L));
                float3 skyFill = _AtmoOn > 0.5 ? _RkSunSpace.rgb * _SkyColor.rgb * 0.22 * daylight : 0;
                float3 col = albedo * (sunAtGround * diffuse + skyFill * saturate(dot(n, up) * 0.5 + 0.5) + _Ambient * 0.02 + RkShineLight(n));

                if (water > 0.5)
                {
                    // Open sea: a sun glint and the sky's reflection, stronger at grazing angles. Up close (milestone 25) the
                    // surface ripples: a few moving wave trains tilt the water's normal, so the glint sparkles and moves.
                    float3 wn = up;
                    [branch] if (patch)
                    {
                        float3 pw = i.posOS + _PatchOffset.xyz;
                        float t = _Time.y;
                        float3 w = float3(sin(pw.x * 0.23 + pw.z * 0.11 + t * 1.3) + 0.6 * sin(pw.x * 0.71 - pw.y * 0.37 + t * 2.1),
                                          sin(pw.y * 0.19 + pw.x * 0.13 - t * 1.1) + 0.6 * sin(pw.y * 0.63 + pw.z * 0.41 + t * 2.4),
                                          sin(pw.z * 0.21 - pw.y * 0.17 + t * 1.6) + 0.6 * sin(pw.z * 0.77 + pw.x * 0.29 - t * 1.9));
                        w -= up * dot(w, up);
                        float near = 1 - smoothstep(300, 2500, dist);
                        wn = normalize(up + w * 0.06 * near);
                    }
                    col += RkDirect(0, 0.02, 0.16, wn, v, L, sunAtGround);
                    float fres = 0.02 + 0.98 * pow(1 - saturate(dot(wn, v)), 5);
                    col += _SkyColor.rgb * _RkSunSpace.rgb * 0.35 * daylight * fres;
                }

            #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = v;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionHCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS);
                    col += albedo * l.color * (l.distanceAttenuation * saturate(dot(n, l.direction)));
                LIGHT_LOOP_END
            #endif
                col += albedo * _Emission;

                // Seen from above the cloud layer, the close-up ground shows the clouds too (the cloud shell is drawn
                // behind the near camera's patch)
                [branch] if (patch && _CloudPaint > 0.001 && _HasMaps > 0.5)
                {
                    float cover = SAMPLE_TEXTURECUBE(_CloudMap, sampler_ColorMap, dirPF).r * 0.92;
                    float3 cloudCol = 0.95 * (sunTop * saturate(dot(up, L) * 0.6 + 0.4) + skyFill * 0.8);
                    col = lerp(col, cloudCol, cover * _CloudPaint);
                }

                // The air between the ground and the eye
                float3 trans;
                float3 ins = RkScatter(camWS - _PlanetCenter.xyz, -v, dist, L, 10, trans);
                col = col * trans + ins;
                col = RkApplyFog(col, dist);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; o.positionHCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
