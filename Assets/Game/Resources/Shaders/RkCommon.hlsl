// Shared lighting for Arden Space Program (milestone 20): single-scattering atmospheres, sun transmittance, the
// analytic environment parts reflect (sky, the world below, black space) and a small PBR model.
#ifndef RK_COMMON_INCLUDED
#define RK_COMMON_INCLUDED

#define RK_PI 3.14159265

// ---- Globals set every frame from C# (RenderEnv) ----
float4 _RkEnvPlanet;      // xyz: centre of the world below (world space), w: its radius (0 = none, e.g. the builder)
float4 _RkEnvGround;      // rgb: the lit world below as a parts reflection / bounce light
float4 _RkEnvZenith;      // rgb: sky straight up (black in space)
float4 _RkEnvHorizon;     // rgb: sky at the horizon
float4 _RkUp;             // xyz: local up at the camera
float4 _RkAmbSky;         // rgb: fill light from above
float4 _RkAmbGround;      // rgb: fill light from below
float4 _RkSunSpace;       // rgb: sunlight above the air (the scattering shaders redden it themselves)
float4 _RkFog;            // milestone 27 weather: rgb the colour of rain haze or blowing dust, a: density per metre (0 = clear)
float4 _RkShineDir;       // milestone 28: toward the big world lighting the night (planetshine), world space
float4 _RkShine;          // rgb: its light (0 = none)

// Light off a big world in the sky (diffuse only)
float3 RkShineLight(float3 n) { return _RkShine.rgb * saturate(dot(n, _RkShineDir.xyz)); }

// How much weather haze lies between the eye and a point this far away (0 = none, 1 = all haze)
float RkFogAmount(float dist) { return _RkFog.a > 0 ? 1 - exp(-dist * _RkFog.a) : 0; }
float3 RkApplyFog(float3 col, float dist) { return lerp(col, _RkFog.rgb, RkFogAmount(dist)); }

// ---- Small helpers ----
float RkHash3(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float RkNoise3(float3 x)
{
    float3 i = floor(x), f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(RkHash3(i + float3(0, 0, 0)), RkHash3(i + float3(1, 0, 0)), f.x),
                     lerp(RkHash3(i + float3(0, 1, 0)), RkHash3(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(RkHash3(i + float3(0, 0, 1)), RkHash3(i + float3(1, 0, 1)), f.x),
                     lerp(RkHash3(i + float3(0, 1, 1)), RkHash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

// Near and far hits of a ray with a sphere at the origin (x > y when it misses).
float2 RkRaySphere(float3 ro, float3 rd, float r)
{
    float b = dot(ro, rd);
    float c = dot(ro, ro) - r * r;
    float h = b * b - c;
    if (h < 0) return float2(1e30, -1e30);
    h = sqrt(h);
    return float2(-b - h, -b + h);
}

// ---- Atmosphere (per material: each world has its own air; define RK_ATMOSPHERE to use) ----
#if defined(RK_ATMOSPHERE)
// Expected in the including shader's UnityPerMaterial buffer:
//   float4 _PlanetCenter; float _PlanetRadius; float _AtmoRadius; float4 _BetaR; float _BetaM; float _HR; float _HM;
//   float _MieG; float _SunIntensity; float _AtmoOn;

// Schuler's approximation of the Chapman function: optical depth (in units of the scale height, density 1 at the
// ground) along a ray to infinity from height h (also in scale heights) above a sphere of radius X scale heights.
float RkChapman(float X, float h, float cosZ)
{
    float c = sqrt(X + h);
    if (cosZ >= 0) return c / (c * cosZ + 1) * exp(-h);
    float x0 = sqrt(saturate(1 - cosZ * cosZ)) * (X + h);
    float c0 = sqrt(x0);
    return 2 * c0 * exp(X - x0) - c / (1 - c * cosZ) * exp(-h);
}

// How much sunlight gets through the air to a point (planet-centred), and 0 in the planet's shadow.
float3 RkSunTransmittance(float3 p, float3 L)
{
    float r = length(p);
    float cosZ = dot(p, L) / r;
    float sinZ = sqrt(saturate(1 - cosZ * cosZ));
    // Behind the planet: soft edge a few km wide so the terminator doesn't alias
    float shadow = cosZ >= 0 ? 1 : smoothstep(-6000, 6000, r * sinZ - _PlanetRadius);
    if (_AtmoOn < 0.5) return shadow;
    if (shadow <= 0) return 0;
    float h = max(r - _PlanetRadius, 0);
    // (in the soft shadow edge the grazing ray dips at most a few km under the ground: the exponent stays small)
    float odR = _HR * RkChapman(_PlanetRadius / _HR, h / _HR, cosZ);
    float odM = _HM * RkChapman(_PlanetRadius / _HM, h / _HM, cosZ);
    return shadow * exp(-min(_BetaR.rgb * odR + _BetaM * 1.1 * odM, 60));
}

// Single scattering along a view ray (planet-centred origin ro, unit direction rd) up to distance tMax.
// Returns the light scattered toward the eye; 'trans' is what survives of whatever lies behind.
float3 RkScatter(float3 ro, float3 rd, float tMax, float3 L, int steps, out float3 trans)
{
    trans = 1;
    if (_AtmoOn < 0.5) return 0;
    float2 ta = RkRaySphere(ro, rd, _AtmoRadius);
    if (ta.x > ta.y || ta.y <= 0) return 0;
    float2 tp = RkRaySphere(ro, rd, _PlanetRadius);
    if (tp.x <= tp.y && tp.x > 0) tMax = min(tMax, tp.x);
    float t0 = max(ta.x, 0), t1 = min(ta.y, tMax);
    if (t1 <= t0) return 0;
    // From inside the air, put more samples near the eye where the air is densest
    bool inside = ta.x < 0;
    float odR = 0, odM = 0;
    float3 sumR = 0, sumM = 0;
    float prevT = t0;
    for (int i = 0; i < steps; i++)
    {
        float u = (i + 1.0) / steps;
        float t = t0 + (t1 - t0) * (inside ? u * u : u);
        float ds = t - prevT;
        float tm = (t + prevT) * 0.5;
        prevT = t;
        float3 p = ro + rd * tm;
        float h = length(p) - _PlanetRadius;
        float dR = exp(-max(h, -500) / _HR) * ds, dM = exp(-max(h, -500) / _HM) * ds;
        odR += dR * 0.5; odM += dM * 0.5;
        float3 tv = exp(-(_BetaR.rgb * odR + _BetaM * 1.1 * odM));
        float3 ts = RkSunTransmittance(p, L);
        sumR += dR * tv * ts;
        sumM += dM * tv * ts;
        odR += dR * 0.5; odM += dM * 0.5;
    }
    trans = exp(-(_BetaR.rgb * odR + _BetaM * 1.1 * odM));
    float mu = dot(rd, L);
    float pR = 3.0 / (16.0 * RK_PI) * (1 + mu * mu);
    float g = _MieG;
    float pM = 3.0 / (8.0 * RK_PI) * ((1 - g * g) * (1 + mu * mu)) / ((2 + g * g) * pow(max(1 + g * g - 2 * g * mu, 1e-4), 1.5));
    // A touch of light scattered more than once, so twilight and the horizon aren't pitch dark
    float3 multi = (sumR * _BetaR.rgb + sumM * _BetaM) * 0.006;
    return _SunIntensity * (sumR * _BetaR.rgb * pR + sumM * _BetaM * pM + multi);
}

#endif // RK_ATMOSPHERE

// ---- What shiny parts see: the analytic environment ----
float3 RkEnvironment(float3 posWS, float3 R, float rough)
{
    float3 up = _RkUp.xyz;
    float e = dot(R, up);
    float3 sky = lerp(_RkEnvHorizon.rgb, _RkEnvZenith.rgb, saturate(e * 1.6));
    if (_RkEnvPlanet.w > 0)
    {
        float3 toC = _RkEnvPlanet.xyz - posWS;
        float dist = length(toC);
        float sinA = saturate(_RkEnvPlanet.w / dist);
        float cosA = sqrt(1 - sinA * sinA);
        float w = 0.004 + rough * 0.35;
        float m = smoothstep(cosA - w, cosA + w, dot(R, toC / dist));
        return max(lerp(sky, _RkEnvGround.rgb, m), 0.012);
    }
    // The builder: a soft studio floor below
    return lerp(_RkEnvGround.rgb, sky, smoothstep(-0.25 - rough * 0.3, 0.1 + rough * 0.3, e));
}

float3 RkAmbient(float3 n)
{
    return lerp(_RkAmbGround.rgb, _RkAmbSky.rgb, saturate(dot(n, _RkUp.xyz) * 0.5 + 0.5));
}

// Karis' fit of the split-sum environment BRDF
float3 RkEnvBRDF(float3 f0, float rough, float nv)
{
    const float4 c0 = float4(-1, -0.0275, -0.572, 0.022);
    const float4 c1 = float4(1, 0.0425, 1.04, -0.04);
    float4 r = rough * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * nv)) * r.x + r.y;
    float2 ab = float2(-1.04, 1.04) * a004 + r.zw;
    return f0 * ab.x + ab.y;
}

// GGX specular + Lambert diffuse for one light (radiance 'lightCol' already includes shadow and attenuation).
float3 RkDirect(float3 diffCol, float3 f0, float rough, float3 n, float3 v, float3 l, float3 lightCol)
{
    float nl = saturate(dot(n, l));
    if (nl <= 0) return 0;
    float3 h = normalize(l + v);
    float nh = saturate(dot(n, h)), nv = max(dot(n, v), 1e-3), vh = saturate(dot(v, h));
    float a = max(rough * rough, 0.002);
    float a2 = a * a;
    float d = nh * nh * (a2 - 1) + 1;
    float D = a2 / (RK_PI * d * d);
    float k = a * 0.5;
    float vis = 0.25 / ((nl * (1 - k) + k) * (nv * (1 - k) + k));
    float3 F = f0 + (1 - f0) * pow(1 - vh, 5);
    return (diffCol * (1 - F) + D * vis * F * RK_PI) * lightCol * nl;
}

#endif
