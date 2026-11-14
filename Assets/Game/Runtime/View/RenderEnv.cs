using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rocket
{
    /// <summary>
    /// Milestone 20 lighting: the post-processing stack (filmic tone mapping, bloom, exposure), each world's air for the
    /// scattering shaders, and the per-frame globals that tell parts what they're lit by and what they reflect
    /// (the sky, the world below, black space). Also small CPU copies of the shader maths, so the sun's colour and the
    /// sky colours match what the shaders draw.
    /// </summary>
    public static class RenderEnv
    {
        public const float SunLight = 2.1f;      // directional light intensity above the air
        public const float SunInAir = 28f;       // the scattering shaders' sun strength (same units)
        public static readonly Color SunColor = new Color(1f, 0.97f, 0.93f);

        static readonly int EnvPlanet = Shader.PropertyToID("_RkEnvPlanet"), EnvGround = Shader.PropertyToID("_RkEnvGround");
        static readonly int EnvZenith = Shader.PropertyToID("_RkEnvZenith"), EnvHorizon = Shader.PropertyToID("_RkEnvHorizon");
        static readonly int Up = Shader.PropertyToID("_RkUp"), AmbSky = Shader.PropertyToID("_RkAmbSky"), AmbGround = Shader.PropertyToID("_RkAmbGround");
        static readonly int SunSpace = Shader.PropertyToID("_RkSunSpace");
        public static readonly int PlanetCenter = Shader.PropertyToID("_PlanetCenter");

        static Volume volume;
        static ColorAdjustments colorAdj;
        static Bloom bloom;
        static Vignette vignette;
        static Tonemapping tonemap;
        static float exposure, photoBright;
        public static float Exposure => exposure;

        /// <summary>A global volume with the look of the game (created once).</summary>
        public static void Init()
        {
            if (volume != null) return;
            var go = new GameObject("PostFX");
            UnityEngine.Object.DontDestroyOnLoad(go);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            tonemap = tone;
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.3f);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.62f);
            bloom.clamp.Override(2.5f);           // milestone 28: flames and the sun are far over 1; without a cap they washed half the screen white
            bloom.highQualityFiltering.Override(true);
            colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0f);
            colorAdj.contrast.Override(8f);
            colorAdj.saturation.Override(6f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.18f);
            vig.smoothness.Override(0.45f);
            vignette = vig;
            volume.sharedProfile = profile;
            Shader.SetGlobalVector(SunSpace, (Vector4)(SunColor * SunLight));
            ApplyQuality(Settings.Graphics);
        }

        /// <summary>Graphics quality: Low drops anti-aliasing, shortens shadows and renders a little smaller; High is the full look.</summary>
        /// <summary>The graphics setting in use (0 Low, 1 Medium, 2 High).</summary>
        public static int Quality = 2;

        public static void ApplyQuality(int level)
        {
            Quality = level;
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null)
            {
                asset.msaaSampleCount = level >= 2 ? 4 : level == 1 ? 2 : 1;
                asset.shadowDistance = level >= 2 ? 250f : level == 1 ? 150f : 70f;
                asset.renderScale = level == 0 ? 0.7f : 1f;
                asset.mainLightShadowmapResolution = level >= 2 ? 4096 : level == 1 ? 2048 : 1024;
                asset.shadowCascadeCount = level >= 2 ? 4 : level == 1 ? 2 : 1;
            }
            if (bloom != null) bloom.highQualityFiltering.Override(level >= 2);
            QualitySettings.lodBias = level >= 2 ? 1.5f : level == 1 ? 1f : 0.6f;
        }

        /// <summary>Post-exposure in stops, eased toward the target so day/night changes feel like eyes adjusting.</summary>
        public static void SetExposure(float target, bool instant = false)
        {
            exposure = instant ? target : Mathf.MoveTowards(exposure, target, Time.unscaledDeltaTime * 1.6f);
            if (colorAdj != null) colorAdj.postExposure.Override(exposure + photoBright);
        }

        public static void SetBloom(float intensity) { if (bloom != null) bloom.intensity.Override(intensity); }

        /// <summary>
        /// Photo mode's looks (milestone 27): 0 normal, 1 vivid, 2 warm, 3 cool, 4 black and white, 5 old photo,
        /// 6 dramatic, 7 dreamy; plus brightness in stops. 0, 0 puts the game's own look back.
        /// </summary>
        public static void SetPhotoLook(int filter, float bright, bool photo = true)
        {
            photoBright = photo ? bright : 0f;
            if (colorAdj == null) return;
            if (!photo) filter = 0;
            float contrast = 8f, sat = 6f, vig = 0.18f, bl = 0.4f;
            Color tint = Color.white;
            switch (filter)
            {
                case 1: contrast = 18f; sat = 38f; break;
                case 2: contrast = 10f; sat = 12f; tint = new Color(1f, 0.9f, 0.76f); break;
                case 3: contrast = 8f; sat = -5f; tint = new Color(0.8f, 0.9f, 1.05f); break;
                case 4: contrast = 22f; sat = -100f; break;
                case 5: contrast = -4f; sat = -72f; tint = new Color(1f, 0.85f, 0.62f); vig = 0.48f; break;
                case 6: contrast = 42f; sat = -12f; vig = 0.42f; break;
                case 7: contrast = -16f; sat = 18f; vig = 0.26f; bl = 1.7f; break;
            }
            colorAdj.contrast.Override(contrast);
            colorAdj.saturation.Override(sat);
            colorAdj.colorFilter.Override(tint);
            colorAdj.postExposure.Override(exposure + photoBright);
            if (vignette != null) vignette.intensity.Override(vig);
            if (bloom != null) bloom.intensity.Override(bl);
        }

        // ---------------------------------------------------------------- each world's air

        public struct Air
        {
            public bool on;
            public Vector3 betaR;        // Rayleigh scattering per metre at the ground (r, g, b)
            public float betaM, hr, hm, g, top;
        }

        public static Air AirOf(CelestialBody b)
        {
            var a = new Air { on = b.HasAtmosphere, hr = (float)Math.Max(b.scaleHeight, 1), hm = 1200f, g = 0.8f, betaM = 7e-6f, top = (float)(b.radius + b.atmosphereHeight) };
            if (!a.on) { a.top = (float)b.radius; return a; }
            float dens = (float)(b.seaLevelDensity / CelestialBody.StandardDensity);
            switch (b.style)
            {
                case BodyStyle.Desert:
                    // Thin dusty air: fine red dust scatters reds most, so the day sky is butterscotch
                    a.betaR = new Vector3(19e-6f, 12e-6f, 6.5e-6f) * Mathf.Sqrt(dens) * 1.6f;
                    a.betaM = 14e-6f; a.hm = 2500f; a.g = 0.65f;
                    break;
                case BodyStyle.Gas:
                    a.betaR = new Vector3(2.6e-6f, 3.6e-6f, 5.6e-6f);
                    a.betaM = 3e-6f; a.hm = 9000f; a.g = 0.7f;
                    break;
                default:
                    // Arden: Earth-like air; its column is a little shorter than Earth's, so a bit richer per metre
                    a.betaR = new Vector3(5.8e-6f, 13.5e-6f, 33.1e-6f) * 1.3f * dens;
                    break;
            }
            return a;
        }

        public static void ApplyAir(Material m, CelestialBody b)
        {
            var a = AirOf(b);
            m.SetFloat("_PlanetRadius", (float)b.radius);
            m.SetFloat("_AtmoRadius", a.top);
            m.SetVector("_BetaR", new Vector4(a.betaR.x, a.betaR.y, a.betaR.z, 0));
            m.SetFloat("_BetaM", a.betaM);
            m.SetFloat("_HR", a.hr);
            m.SetFloat("_HM", a.hm);
            m.SetFloat("_MieG", a.g);
            m.SetFloat("_SunIntensity", SunInAir);
            m.SetFloat("_AtmoOn", a.on ? 1f : 0f);
            if (m.HasProperty("_SkyColor")) m.SetColor("_SkyColor", b.sky);
            if (m.HasProperty("_AlbedoScale")) m.SetFloat("_AlbedoScale", AlbedoScale(b));
        }

        /// <summary>
        /// How much of the sunlight a world's ground really reflects compared with its map colours: airless dust is
        /// dark (Tal is about as dark as worn asphalt), ice and fresh sand brighter.
        /// </summary>
        public static float AlbedoScale(CelestialBody b)
        {
            switch (b.style)
            {
                case BodyStyle.Barren: return 0.42f;
                case BodyStyle.Icy: return 0.72f;
                case BodyStyle.Desert: return 0.55f;
                case BodyStyle.Volcanic: return 0.55f;
                case BodyStyle.Gas: return 0.62f;
                default: return 1f;
            }
        }

        // ---------------------------------------------------------------- CPU copies of the shader maths

        static double Chapman(double X, double h, double cosZ)
        {
            double c = Math.Sqrt(X + h);
            if (cosZ >= 0) return c / (c * cosZ + 1) * Math.Exp(-h);
            double x0 = Math.Sqrt(Math.Max(0, 1 - cosZ * cosZ)) * (X + h);
            double c0 = Math.Sqrt(x0);
            return 2 * c0 * Math.Exp(X - x0) - c / (1 - c * cosZ) * Math.Exp(-h);
        }

        /// <summary>Fraction of sunlight (r, g, b) reaching a planet-centred point: reddened by air, 0 behind the planet.</summary>
        public static Vector3 SunTransmittance(CelestialBody b, Vector3d p, Vector3 L)
        {
            double r = p.magnitude;
            if (r < 1) return Vector3.one;
            double cosZ = Vector3d.Dot(p, Vector3d.FromFloat(L)) / r;
            double sinZ = Math.Sqrt(Math.Max(0, 1 - cosZ * cosZ));
            float shadow = cosZ >= 0 ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-6000f, 6000f, (float)(r * sinZ - b.radius)));
            var a = AirOf(b);
            if (!a.on || shadow <= 0) return Vector3.one * shadow;
            double h = Math.Max(r - b.radius, 0);
            double odR = a.hr * Chapman(b.radius / a.hr, h / a.hr, cosZ);
            double odM = a.hm * Chapman(b.radius / a.hm, h / a.hm, cosZ);
            float T(float beta) => (float)Math.Exp(-Math.Min(beta * odR + a.betaM * 1.1 * odM, 60));
            return new Vector3(T(a.betaR.x), T(a.betaR.y), T(a.betaR.z)) * shadow;
        }

        /// <summary>The sky's colour seen from a planet-centred point looking along 'dir' (single scattering, like the sky shader).</summary>
        public static Color SkyColor(CelestialBody b, Vector3d ro, Vector3 dir, Vector3 L, int steps = 16)
        {
            var a = AirOf(b);
            if (!a.on) return Color.black;
            Vector3d rd = Vector3d.FromFloat(dir.normalized);
            double bb = Vector3d.Dot(ro, rd), c = ro.sqrMagnitude - (double)a.top * a.top, disc = bb * bb - c;
            if (disc < 0) return Color.black;
            double sq = Math.Sqrt(disc), t0 = Math.Max(-bb - sq, 0), t1 = -bb + sq;
            if (t1 <= 0) return Color.black;
            double cp = ro.sqrMagnitude - b.radius * b.radius, dp = bb * bb - cp;
            if (dp >= 0) { double tp = -bb - Math.Sqrt(dp); if (tp > 0) t1 = Math.Min(t1, tp); }
            if (t1 <= t0) return Color.black;
            bool inside = ro.magnitude < a.top;
            double odR = 0, odM = 0, prev = t0;
            Vector3 sumR = Vector3.zero, sumM = Vector3.zero;
            for (int i = 0; i < steps; i++)
            {
                double u = (i + 1.0) / steps, t = t0 + (t1 - t0) * (inside ? u * u : u), ds = t - prev, tm = (t + prev) * 0.5;
                prev = t;
                Vector3d p = ro + rd * tm;
                double h = p.magnitude - b.radius;
                double dR = Math.Exp(-Math.Max(h, -500) / a.hr) * ds, dM = Math.Exp(-Math.Max(h, -500) / a.hm) * ds;
                odR += dR * 0.5; odM += dM * 0.5;
                Vector3 tv = new Vector3((float)Math.Exp(-(a.betaR.x * odR + a.betaM * 1.1 * odM)), (float)Math.Exp(-(a.betaR.y * odR + a.betaM * 1.1 * odM)), (float)Math.Exp(-(a.betaR.z * odR + a.betaM * 1.1 * odM)));
                Vector3 ts = SunTransmittance(b, p, L);
                Vector3 w = Vector3.Scale(tv, ts);
                sumR += w * (float)dR;
                sumM += w * (float)dM;
                odR += dR * 0.5; odM += dM * 0.5;
            }
            float mu = Vector3.Dot(dir.normalized, L);
            float pR = 3f / (16f * Mathf.PI) * (1 + mu * mu);
            float g = a.g;
            float pM = 3f / (8f * Mathf.PI) * ((1 - g * g) * (1 + mu * mu)) / ((2 + g * g) * Mathf.Pow(Mathf.Max(1 + g * g - 2 * g * mu, 1e-4f), 1.5f));
            Vector3 multi = (Vector3.Scale(sumR, a.betaR) + sumM * a.betaM) * 0.006f;
            Vector3 col = SunInAir * (Vector3.Scale(sumR, a.betaR) * pR + sumM * (a.betaM * pM) + multi);
            return new Color(col.x, col.y, col.z, 1f);
        }

        static readonly System.Collections.Generic.Dictionary<CelestialBody, Color> albedoCache = new System.Collections.Generic.Dictionary<CelestialBody, Color>();

        /// <summary>A world's average ground colour (for what parts reflect and the light bounced up from below).</summary>
        public static Color AverageAlbedo(CelestialBody b)
        {
            if (albedoCache.TryGetValue(b, out var c)) return c;
            var rng = new System.Random(b.seed);
            Color sum = Color.black;
            const int n = 300;
            for (int i = 0; i < n; i++)
            {
                Vector3 d;
                do d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1); while (d.sqrMagnitude > 1 || d.sqrMagnitude < 0.01f);
                sum += MeshFactory.BodyColor(b, d.normalized);
            }
            c = sum / n * AlbedoScale(b);
            if (b.style == BodyStyle.Earthlike) c = Color.Lerp(c, new Color(0.9f, 0.92f, 0.95f), 0.3f); // clouds
            c.a = 1f;
            albedoCache[b] = c;
            return c;
        }

        // ---------------------------------------------------------------- per-frame setups

        /// <summary>
        /// Flight and the title screen: light the scene for a camera near 'body'. 'camRel' is the camera relative to the
        /// body's centre (inertial axes, metres) and 'centreWS' the body's centre in render space.
        /// </summary>
        public static void UpdateWorld(CelestialBody body, Vector3d camRel, Vector3 centreWS, Vector3 sunDir, Light sun, bool sunlit)
        {
            Shader.SetGlobalVector(SunSpace, (Vector4)(SunColor * SunLight));
            Vector3 up = camRel.normalized.ToFloat();
            double r = camRel.magnitude, alt = r - body.radius;
            Vector3 T = sunlit ? SunTransmittance(body, camRel, sunDir) : Vector3.zero;
            if (sun != null)
            {
                sun.color = new Color(SunColor.r * T.x, SunColor.g * T.y, SunColor.b * T.z);
                sun.intensity = SunLight;
                sun.shadows = T.magnitude > 0.02f ? LightShadows.Soft : LightShadows.None;
            }

            // Sky colours straight up and at the horizon toward the sun's side
            Color zenith = Color.black, horizon = Color.black;
            if (body.HasAtmosphere && alt < body.atmosphereHeight * 1.5)
            {
                Vector3 side = Vector3.ProjectOnPlane(sunDir, up);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(up, Vector3.right);
                side.Normalize();
                zenith = SkyColor(body, camRel, up, sunDir, 12);
                horizon = SkyColor(body, camRel, (side + up * 0.06f).normalized, sunDir, 12);
                Color back = SkyColor(body, camRel, (-side + up * 0.06f).normalized, sunDir, 12);
                horizon = Color.Lerp(horizon, back, 0.5f);
            }

            // The world below, as seen from here: its colour lit by the sun, a crescent or full disc from far away
            Color alb = AverageAlbedo(body);
            float cosSun = Vector3.Dot(up, sunDir);
            float far01 = Mathf.Clamp01((float)(alt / (body.radius * 0.6)));
            float lit = Mathf.Lerp(Mathf.Clamp01(cosSun + 0.08f), 0.5f + 0.5f * cosSun, far01);
            Vector3 Tg = SunTransmittance(body, Vector3d.FromFloat(up) * (body.radius + 10), sunDir);
            Color ground = alb * SunLight * lit;
            ground = new Color(ground.r * Mathf.Lerp(Tg.x, 1, far01), ground.g * Mathf.Lerp(Tg.y, 1, far01), ground.b * Mathf.Lerp(Tg.z, 1, far01));
            if (body.HasAtmosphere) ground += body.sky * (0.25f * lit * SunLight * 0.3f);
            if (body.isStar) ground = new Color(4f, 3.6f, 2.8f);

            float sinA = (float)Math.Min(1, body.radius / Math.Max(r, body.radius));
            float coverage = 1f - Mathf.Sqrt(1f - sinA * sinA);           // how much of the view below the world fills
            Shader.SetGlobalVector(EnvPlanet, new Vector4(centreWS.x, centreWS.y, centreWS.z, body.isStar ? 0 : (float)body.radius));
            Shader.SetGlobalVector(EnvGround, ground);
            Shader.SetGlobalVector(EnvZenith, zenith);
            Shader.SetGlobalVector(EnvHorizon, horizon);
            Shader.SetGlobalVector(Up, up);
            Color starlight = new Color(0.02f, 0.022f, 0.028f);
            Shader.SetGlobalVector(AmbSky, (zenith + horizon) * 0.5f + starlight);
            Shader.SetGlobalVector(AmbGround, ground * Mathf.Lerp(1f, coverage, far01) * 0.6f + starlight);

            // Eyes adjust: brighter at night or in a world's shadow
            float sunLum = (T.x + T.y + T.z) / 3f;
            SetExposure(sunLum < 0.08f ? 1.4f : sunLum < 0.3f ? 0.6f : 0f);
        }

        static readonly int ShineDirId = Shader.PropertyToID("_RkShineDir"), ShineId = Shader.PropertyToID("_RkShine");

        /// <summary>
        /// Milestone 28: light reflected off a big world in the sky (Arden lighting Tal's night side like a huge moon,
        /// Tal lighting Arden's nights, Brume its moons). The brightest of the world you're at's parent and moons:
        /// how much of it is sunlit as seen from here, how big it looks, how pale it is. Turned up a lot from real life
        /// so nights are playable; at most a sixth of sunlight.
        /// </summary>
        public static void UpdateShine(CelestialBody here, Vector3d eyeAbs, double ut, System.Collections.Generic.IEnumerable<CelestialBody> bodies)
        {
            Color best = Color.black;
            Vector3 dir = Vector3.up;
            float bestK = 0f;
            if (here != null && bodies != null)
                foreach (var c in bodies)
                {
                    if (c == null || c == here || c.isStar) continue;
                    if (c != here.parent && c.parent != here) continue;
                    Vector3d d = c.AbsolutePosition(ut) - eyeAbs;
                    double dist = d.magnitude;
                    if (dist < c.radius * 1.01) continue;
                    Vector3d toEye = -d / dist;
                    Vector3d sun = Sun.DirectionFrom(c.AbsolutePosition(ut), ut);
                    float phase = (float)(1 + Vector3d.Dot(sun, toEye)) * 0.5f;
                    float size = (float)(c.radius / dist);
                    Color alb = AverageAlbedo(c);
                    float lum = alb.r * 0.3f + alb.g * 0.59f + alb.b * 0.11f;
                    float k = Mathf.Min(0.16f, lum * phase * size * size * 400f);
                    if (k > bestK)
                    {
                        bestK = k;
                        dir = (d / dist).ToFloat();
                        float m = Mathf.Max(1e-3f, Mathf.Max(alb.r, Mathf.Max(alb.g, alb.b)));
                        best = new Color(alb.r / m, alb.g / m, alb.b / m) * (k * SunLight);
                    }
                }
            Shader.SetGlobalVector(ShineDirId, dir);
            Shader.SetGlobalVector(ShineId, best);
        }

        public static float ShineStrength => Shader.GetGlobalVector(ShineId).magnitude / SunLight;

        /// <summary>The builder: a soft studio (grey floor, pale ceiling) and a key light.</summary>
        public static void UpdateStudio(Light sun)
        {
            Shader.SetGlobalVector(SunSpace, (Vector4)(SunColor * SunLight));
            if (sun != null)
            {
                sun.color = SunColor;
                sun.intensity = 2.2f;
                sun.shadows = LightShadows.Soft;
            }
            Shader.SetGlobalVector(EnvPlanet, Vector4.zero);
            Shader.SetGlobalVector(EnvGround, new Color(0.16f, 0.165f, 0.17f));
            Shader.SetGlobalVector(EnvZenith, new Color(0.62f, 0.66f, 0.72f));
            Shader.SetGlobalVector(EnvHorizon, new Color(0.42f, 0.44f, 0.47f));
            Shader.SetGlobalVector(Up, Vector3.up);
            Shader.SetGlobalVector(AmbSky, new Color(0.42f, 0.45f, 0.5f));
            Shader.SetGlobalVector(AmbGround, new Color(0.17f, 0.17f, 0.18f));
            Shader.SetGlobalVector(ShineId, Vector4.zero);
            SetExposure(0f, true);
        }
    }
}
