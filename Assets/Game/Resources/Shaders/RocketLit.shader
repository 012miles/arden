// Parts, buildings and the pad (milestone 20): physically based lighting on vertex-coloured procedural meshes.
// Per-vertex material in UV0: x = material id, y = metallic, z = smoothness, w = unused.
//   0 paint, 1 gold foil, 2 bright metal, 3 dark metal, 4 concrete, 5 asphalt (1-5 use the downloaded texture layers),
//   6 glass, 7 lamp (glows); milestone 37, cabin surfaces drawn here: 11 woven cloth, 12 studded rubber floor,
//   13 painted panel (orange peel, seams, scratches), 14 quilting, 15 perforated metal, 16 hook-and-loop.
// Sun with soft shadows, extra lights (engine glow, spotlights), sky/ground fill and reflections of the world below.
Shader "Rocket/Lit"
{
    Properties
    {
        _Ambient ("Fill light (0.24 = normal)", Range(0, 1)) = 0.24
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Range(0, 1)) = 0
        _Char ("Charring", Range(0, 1)) = 0
        _DetailA ("Detail albedo+roughness", 2DArray) = "" {}
        _DetailN ("Detail normals", 2DArray) = "" {}
        _SunScale ("Sun and sky (0 indoors)", Range(0, 1)) = 1
        _CabinAO ("Room corners darken (cabins)", Range(0, 1)) = 0
        _RoomOrigin ("Room origin (world)", Vector) = (0, 0, 0, 0)
        _RoomSize ("Room: radius or half width, height, box?, half length", Vector) = (2, 2.1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _Ambient;
            float4 _Tint;
            float _Emission;
            float _Char;
            float _SunScale;
            float _CabinAO;
            float4 _RoomOrigin;
            float4 _RoomSize;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "RkCommon.hlsl"

            TEXTURE2D_ARRAY(_DetailA); SAMPLER(sampler_DetailA);
            TEXTURE2D_ARRAY(_DetailN);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; float4 mat : TEXCOORD0; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : COLOR;
                float4 mat : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                float3 normalOS : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.mat = v.mat;
                o.positionOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                return o;
            }

            // Triplanar sample of one texture layer in object space; returns albedo+roughness, and a world normal.
            void Triplanar(float3 p, float3 nOS, float layer, float scale, out float4 alb, out float3 nWS)
            {
                float3 w = pow(abs(nOS), 4);
                w /= (w.x + w.y + w.z);
                float2 ux = p.zy / scale, uy = p.xz / scale, uz = p.xy / scale;
                alb = SAMPLE_TEXTURE2D_ARRAY(_DetailA, sampler_DetailA, ux, layer) * w.x
                    + SAMPLE_TEXTURE2D_ARRAY(_DetailA, sampler_DetailA, uy, layer) * w.y
                    + SAMPLE_TEXTURE2D_ARRAY(_DetailA, sampler_DetailA, uz, layer) * w.z;
                // Whiteout blend of the three tangent-space normals into object space
                float3 tx = SAMPLE_TEXTURE2D_ARRAY(_DetailN, sampler_DetailA, ux, layer).xyz * 2 - 1;
                float3 ty = SAMPLE_TEXTURE2D_ARRAY(_DetailN, sampler_DetailA, uy, layer).xyz * 2 - 1;
                float3 tz = SAMPLE_TEXTURE2D_ARRAY(_DetailN, sampler_DetailA, uz, layer).xyz * 2 - 1;
                float3 nx = float3(tx.xy + nOS.zy, abs(tx.z) * nOS.x);
                float3 ny = float3(ty.xy + nOS.xz, abs(ty.z) * nOS.y);
                float3 nz = float3(tz.xy + nOS.xy, abs(tz.z) * nOS.z);
                float3 n = normalize(nx.zyx * w.x + ny.xzy * w.y + nz.xyz * w.z);
                nWS = TransformObjectToWorldNormal(n);
            }

            // Planar sample on a wall (u along it, v up it) or a floor, with the texture's normal turned into the world.
            void WallSample(float2 uv, float layer, float3 tOS, float3 nOS, out float4 alb, out float3 nWS)
            {
                alb = SAMPLE_TEXTURE2D_ARRAY(_DetailA, sampler_DetailA, uv, layer);
                float3 tn = SAMPLE_TEXTURE2D_ARRAY(_DetailN, sampler_DetailA, uv, layer).xyz * 2 - 1;
                nWS = TransformObjectToWorldNormal(normalize(tOS * tn.x + float3(0, 1, 0) * tn.y + nOS * tn.z));
            }
            void FloorSample(float2 uv, float layer, out float4 alb, out float3 nWS)
            {
                alb = SAMPLE_TEXTURE2D_ARRAY(_DetailA, sampler_DetailA, uv, layer);
                float3 tn = SAMPLE_TEXTURE2D_ARRAY(_DetailN, sampler_DetailA, uv, layer).xyz * 2 - 1;
                nWS = TransformObjectToWorldNormal(normalize(float3(tn.x, tn.z, tn.y)));
            }

            // ---- Milestone 37: cabin surfaces drawn in the shader (nothing to download), with real relief ----
            // The plane a surface mostly faces: uv in metres along it, and its two directions (object space).
            void CabinPlane(float3 p, float3 nO, out float2 uv, out float3 t, out float3 b)
            {
                float3 a = abs(nO);
                if (a.y >= a.x && a.y >= a.z) { uv = p.xz; t = float3(1, 0, 0); b = float3(0, 0, 1); }
                else if (a.x >= a.z) { uv = p.zy; t = float3(0, 0, 1); b = float3(0, 1, 0); }
                else { uv = p.xy; t = float3(1, 0, 0); b = float3(0, 1, 0); }
            }

            // Height (0..1) of each pattern
            float CabinHeight(int id, float2 uv)
            {
                if (id == 11)   // woven cloth: threads over and under, 3 mm
                {
                    float2 q = uv / 0.003;
                    float wx = sin(q.x * RK_PI) * 0.5 + 0.5, wy = sin(q.y * RK_PI) * 0.5 + 0.5;
                    float pick = step(0.5, frac((floor(q.x) + floor(q.y)) * 0.5));
                    return lerp(wx, wy, pick);
                }
                if (id == 12)   // rubber floor: round studs every 4 cm
                {
                    float2 c = frac(uv / 0.04) - 0.5;
                    return 1 - smoothstep(0.17, 0.25, length(c));
                }
                if (id == 13)   // painted panel: orange peel, with panel seams every 60 x 50 cm
                {
                    float peel = RkNoise3(float3(uv / 0.005, 1.7));
                    float2 d = abs(frac(uv / float2(0.6, 0.5) + 0.5) - 0.5) * float2(0.6, 0.5);
                    float seam = smoothstep(0.001, 0.0035, min(d.x, d.y));
                    return peel * 0.12 + seam * 0.88;
                }
                if (id == 14)   // quilting: puffed diamonds, 9 cm
                {
                    float2 r = float2(uv.x + uv.y, uv.x - uv.y) * 0.7071 / 0.09;
                    float2 c = abs(frac(r) - 0.5);
                    return sqrt(saturate(1 - pow(max(c.x, c.y) * 2, 4)));
                }
                if (id == 15)   // perforated metal: 4 mm holes, staggered
                {
                    float2 q = uv / 0.009;
                    q.x += step(0.5, frac(q.y * 0.5)) * 0.5;
                    return smoothstep(0.19, 0.25, length(frac(q) - 0.5));
                }
                if (id == 16) return RkNoise3(float3(uv / 0.0015, 3.1));   // hook-and-loop fuzz
                return 0.5;
            }

            void CabinSurface(int id, float3 p, float3 nO, inout float3 albedo, inout float smooth, inout float3 nWS)
            {
                float2 uv; float3 t, b;
                CabinPlane(p, nO, uv, t, b);
                float cell = id == 11 ? 0.003 : id == 12 ? 0.04 : id == 13 ? 0.01 : id == 14 ? 0.09 : id == 15 ? 0.009 : 0.003;
                float e = cell * 0.08;
                float h = CabinHeight(id, uv);
                float hx = CabinHeight(id, uv + float2(e, 0)), hy = CabinHeight(id, uv + float2(0, e));
                // fade the finest patterns out with distance (no shimmering), keep the colour
                float fw = length(fwidth(uv));
                float fade = saturate(2.0 - fw / cell * 2.5);
                float bump = (id == 11 ? 0.0005 : id == 12 ? 0.003 : id == 13 ? 0.0005 : id == 14 ? 0.01 : id == 15 ? 0.0012 : 0.0003) * fade;
                float2 g = float2(hx - h, hy - h) / e * bump;
                float3 nObj = normalize(nO - t * g.x - b * g.y);
                nWS = normalize(lerp(nWS, TransformObjectToWorldNormal(nObj), fade));
                float hv = lerp(0.5, h, fade);
                float mott = RkNoise3(p * 3.1) * 0.6 + RkNoise3(p * 13.0) * 0.4;   // stains, wear, dye
                if (id == 11) { albedo *= lerp(0.8, 1.0, hv) * (0.94 + 0.1 * mott); smooth = 0.06; }
                else if (id == 12)
                {
                    float scuff = smoothstep(0.62, 0.8, RkNoise3(p * 1.7));
                    albedo *= lerp(0.72, 1.05, hv) * (0.9 + 0.15 * mott);
                    albedo = lerp(albedo, albedo * 1.35 + 0.02, scuff * 0.35);
                    smooth = lerp(0.12, 0.32, hv);
                }
                else if (id == 13)
                {
                    float2 d = abs(frac(uv / float2(0.6, 0.5) + 0.5) - 0.5) * float2(0.6, 0.5);
                    float seam = smoothstep(0.001, 0.0035, min(d.x, d.y));
                    float sc = RkNoise3(float3(uv.x * 260.0, uv.y * 9.0, 5.0)) * RkNoise3(float3(uv.x * 9.0, uv.y * 230.0, 2.0));
                    float scratch = smoothstep(0.6, 0.66, sc) * fade;   // rare, faint ones
                    albedo *= lerp(0.45, 1.0, seam) * (0.95 + 0.08 * mott);
                    albedo += scratch * 0.025;
                    smooth = saturate(smooth + (mott - 0.5) * 0.15 - scratch * 0.2);
                }
                else if (id == 14) { albedo *= lerp(0.74, 1.0, hv) * (0.95 + 0.07 * mott); smooth = 0.08; }
                else if (id == 15) { albedo *= lerp(0.06, 1.0, hv); smooth = 0.38; }
                else if (id == 16) { albedo *= 0.82 + 0.28 * hv; smooth = 0.02; }
            }

            // Corners of a cabin darken a little, as they do in real rooms (no screen-space noise)
            float CabinOcclusion(float3 posWS, float3 nWS)
            {
                float3 q = posWS - _RoomOrigin.xyz;
                float floorAO = lerp(0.5, 1, smoothstep(0.0, 0.5, q.y));
                float ceilAO = lerp(0.72, 1, smoothstep(_RoomSize.y, _RoomSize.y - 0.4, q.y));
                float toWall = _RoomSize.z > 0.5 ? min(_RoomSize.x - abs(q.x), _RoomSize.w - abs(q.z)) : _RoomSize.x - length(q.xz);
                float wallAO = lerp(0.6, 1, smoothstep(0.0, 0.45, toWall));
                float ao = lerp(floorAO * ceilAO, wallAO * lerp(1, floorAO, 0.3), saturate(abs(nWS.y)));
                return lerp(1, ao, _CabinAO);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 albedo = i.color.rgb * _Tint.rgb;
                int id = (int)round(i.mat.x);
                float metallic = i.mat.y;
                float smooth = i.mat.z;
                if (id == 0 && metallic == 0 && smooth == 0) smooth = 0.5;   // meshes without material data: plain paint
                float3 emissive = 0;

                if (id >= 1 && id <= 5)
                {
                    // Downloaded material, tinted by the vertex colour (white = as photographed)
                    float scale = id == 1 ? 0.45 : id == 2 ? 0.9 : id == 3 ? 0.8 : 3.0;
                    float4 a; float3 nd;
                    Triplanar(i.positionOS, normalize(i.normalOS), id - 1, scale, a, nd);
                    albedo *= a.rgb;
                    smooth = saturate(1 - a.a * (1.15 - smooth));
                    n = normalize(lerp(n, nd, id == 1 ? 0.9 : 0.6));
                }
                else if (id == 0)
                {
                    // Paint: faint mottling in sheen and colour so big panels don't look like plastic
                    float m = RkNoise3(i.positionOS * 2.7) * 0.6 + RkNoise3(i.positionOS * 11.0) * 0.4;
                    smooth = saturate(smooth + (m - 0.5) * 0.18);
                    albedo *= 0.96 + 0.08 * m;
                }
                else if (id == 7) emissive = albedo * 6;
                else if (id >= 11 && id <= 16) CabinSurface(id, i.positionOS, normalize(i.normalOS), albedo, smooth, n);   // milestone 37

                // Milestone 24: buildings and grounds drawn in the shader, so plain boxes read as real buildings
                float glassAmt = 0;
                float3 glassEmit = 0;
                if (id == 8)
                {
                    // Facades from photographs (ambientCG, CC0) at real scale: style in mat.w picks office grid, concrete
                    // frame, glass curtain wall or metal cladding; roofs are gravel
                    float style = i.mat.w;
                    float3 p = i.positionOS;
                    float3 nO = normalize(i.normalOS);
                    float h = p.y;
                    if (nO.y > 0.6)
                    {
                        float4 a; float3 nd;
                        FloorSample(p.xz / 3.0, 11, a, nd);
                        float m = RkNoise3(p * 0.08) * 0.6 + RkNoise3(p * 0.7) * 0.4;
                        albedo = a.rgb * (0.8 + 0.25 * m);
                        smooth = saturate(1 - a.a);
                        n = normalize(lerp(n, nd, 0.7));
                    }
                    else if (abs(nO.y) < 0.4)
                    {
                        float layer = style < 0.17 ? 5 : style < 0.5 ? 6 : style < 0.83 ? 7 : 8;
                        float tile = style < 0.17 ? 28.0 : style < 0.5 ? 21.0 : style < 0.83 ? 20.0 : 3.0;
                        float3 t3 = normalize(float3(-nO.z, 0, nO.x));
                        float u = dot(p, t3);
                        float4 a; float3 nd;
                        WallSample(float2(u / tile, h / tile), layer, t3, nO, a, nd);
                        albedo *= a.rgb * (layer == 8 ? 1.12 : 1.2);
                        smooth = saturate(1 - a.a);
                        n = normalize(lerp(n, nd, layer == 8 ? 0.95 : 0.6));
                        if (layer != 8) glassAmt = saturate((0.45 - a.a) * 3.5);                     // the glass in the photo
                        albedo *= lerp(float3(0.72, 0.74, 0.78), float3(1, 1, 1), saturate(h / 6.0));  // darker at the foot
                        albedo *= 1 - 0.07 * RkNoise3(float3(u * 1.3, h * 0.1, 2.0));                  // grime streaks
                        float3 upWS = normalize(TransformObjectToWorldDir(float3(0, 1, 0)));
                        float night = saturate((0.12 - dot(upWS, _MainLightPosition.xyz)) * 5);
                        float2 cells = style < 0.17 ? float2(14, 8) : style < 0.5 ? float2(6, 6) : float2(16, 10);
                        float r = RkHash3(float3(floor(u / tile * cells.x), floor(h / tile * cells.y), layer));
                        // fewer, softer lit windows, fading with distance so far-off blocks don't read as glowing slabs (milestone 26)
                        float camD = length(GetCameraPositionWS() - i.positionWS);
                        glassEmit = float3(1.0, 0.76, 0.42) * step(0.72, r) * glassAmt * night * 1.25 * lerp(1.0, 0.22, smoothstep(450, 2200, camD));
                    }
                }
                else if (id == 9)
                {
                    // Paving slabs (photo, CC0), greyed a little, with broad patches so the repeat doesn't show
                    float3 p = i.positionOS;
                    float4 a; float3 nd;
                    FloorSample(p.xz / 4.0, 9, a, nd);
                    float3 t = lerp(dot(a.rgb, float3(0.3, 0.59, 0.11)).xxx, a.rgb, 0.4);
                    float m = RkNoise3(p * 0.035) * 0.5 + RkNoise3(p * 0.22) * 0.3 + RkNoise3(p * 1.7) * 0.2;
                    albedo *= t * (0.82 + 0.32 * m);
                    smooth = saturate(1 - a.a);
                    n = normalize(lerp(n, nd, 0.6));
                }
                else if (id == 10)
                {
                    // Lawn (photo, CC0) with mowing stripes and broad patches
                    float3 p = i.positionOS;
                    float4 a; float3 nd;
                    FloorSample(p.xz / 3.0, 10, a, nd);
                    float stripe = smoothstep(0.4, 0.6, frac(p.x / 6.0)) * (1 - smoothstep(0.9, 1.0, frac(p.x / 6.0)));
                    float m = RkNoise3(p * 0.05) * 0.6 + RkNoise3(p * 0.45) * 0.4;
                    albedo *= a.rgb * (0.88 + 0.14 * stripe) * (0.84 + 0.32 * m);
                    smooth = 0.06;
                    n = normalize(lerp(n, nd, 0.5));
                }

                // Heat damage (milestone 23): soot and scorch streaks running back along the part, paint gone dull
                if (_Char > 0.001)
                {
                    float streak = RkNoise3(i.positionOS * float3(5.0, 0.6, 5.0)) * 0.7 + RkNoise3(i.positionOS * float3(17.0, 2.0, 17.0)) * 0.3;
                    float soot = saturate(_Char * 1.7 - (1 - streak) * 0.9);
                    float3 scorch = lerp(float3(0.32, 0.2, 0.12), float3(0.035, 0.03, 0.028), saturate(_Char * 1.3));
                    albedo = lerp(albedo, scorch, soot);
                    smooth *= 1 - 0.8 * soot;
                    metallic *= 1 - 0.7 * soot;
                }
                float rough = saturate(1 - smooth);
                rough = max(rough, 0.04);
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                float3 f0 = lerp(0.04, albedo, metallic);
                float3 diff = albedo * (1 - metallic);
                if (id == 6) { diff *= 0.15; f0 = 0.05; rough = 0.04; }
                if (glassAmt > 0) { diff *= lerp(1, 0.55, glassAmt); f0 = lerp(f0, 0.06, glassAmt); rough = lerp(rough, 0.05, glassAmt); emissive += glassEmit; }

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);
                float3 col = RkDirect(diff, f0, rough, n, v, sun.direction, sun.color * sun.shadowAttenuation) * _SunScale;   // (indoors: the cabin's own lamps)
                col += diff * RkShineLight(n) * _SunScale;

            #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = v;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionHCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS);
                    col += RkDirect(diff, f0, max(rough, 0.25), n, v, l.direction, l.color * l.distanceAttenuation);
                LIGHT_LOOP_END
            #endif

                // Fill light and reflections of the surroundings
                float ao = 1;
            #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor aof = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionHCS));
                ao = aof.indirectAmbientOcclusion;
            #endif
                float fill = _Ambient / 0.24;
                float nv = saturate(dot(n, v));
                float3 R = reflect(-v, n);
                float3 env = RkEnvironment(i.positionWS, R, rough);
                float3 amb = RkAmbient(n);
                if (_SunScale < 0.999)
                {
                    // indoors: a soft, even fill from the cabin itself (in space there's no sky to light it)
                    amb = lerp(float3(0.3, 0.3, 0.31) * (0.8 + 0.2 * saturate(n.y * 0.5 + 0.5)), amb, _SunScale);
                    env = lerp(float3(0.2, 0.2, 0.21), env, _SunScale);
                }
                if (_CabinAO > 0) ao *= CabinOcclusion(i.positionWS, n);
                col += (diff * amb * fill + env * RkEnvBRDF(f0, rough, nv) * min(fill, 1.5)) * ao;
                if (_CabinAO > 0) col *= lerp(1, ao, 0.5);   // the lamps' light gathers less in the corners too
                col += albedo * _Emission * 1.5 + emissive;
                col = RkApplyFog(col, length(GetCameraPositionWS() - i.positionWS));
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - p);
            #else
                float3 ld = _LightDirection;
            #endif
                float4 c = TransformWorldToHClip(ApplyShadowBias(p, n, ld));
            #if UNITY_REVERSED_Z
                c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
            #else
                c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionHCS = c;
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
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
