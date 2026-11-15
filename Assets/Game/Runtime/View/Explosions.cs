using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 42 (captaincarl24: "add a better explosion effect, instead of a simple circle"): an explosion made the way
    /// the engine flames were: layers. A flash of real light that lights up everything near; a shockwave ring racing out;
    /// a fireball that keeps erupting for a moment, rolling and churning as it cools from white through orange to soot;
    /// burning debris flung out on smoke trails and falling back; glowing embers drifting down; a thick smoke column that
    /// rises and spreads (only in air); and near the ground a ring of dust. Everything stays put on the world as it turns.
    /// </summary>
    public static class Explosions
    {
        static Material fireMat, smokeMat;
        static Mesh ringMesh;

        public static void Blast(Vector3 at, Vector3 up, float size, bool inAir, bool nearGround)
        {
            if (fireMat == null) fireMat = new Material(Resources.Load<Shader>("Shaders/RocketSpark")) { name = "Blast fire" };
            if (smokeMat == null) smokeMat = new Material(Resources.Load<Shader>("Shaders/RocketSmoke")) { name = "Blast smoke" };
            size = Mathf.Max(1f, size);
            var go = new GameObject("Blast");   // (not parented: the particles keep to the world through their simulation space)
            go.transform.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up));
            Object.Destroy(go, 16f);

            ParticleSystem Make(string name, Material m, ParticleSystemRenderMode mode, float radius)
            {
                var c = new GameObject(name);
                c.transform.SetParent(go.transform, false);
                var ps = c.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = false; main.playOnAwake = false; main.duration = 1.2f;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = Smoke.Anchor;
                var em = ps.emission; em.rateOverTime = 0f;
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
                shape.position = new Vector3(0, radius * 1.2f, 0);   // (a little off the ground, so the billboards don't cut flat against it)
                var r = c.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = m; r.renderMode = mode; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                r.maxParticleSize = 5f;
                return ps;
            }
            Gradient G(Color c0, Color c1, Color c2, Color c3, float a0, float a1, float a2)
            {
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 0.18f), new GradientColorKey(c2, 0.5f), new GradientColorKey(c3, 1f) },
                          new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(a1, 0.45f), new GradientAlphaKey(a2, 1f) });
                return g;
            }

            // the fireball: three eruptions a moment apart, churning as they swell and cool
            var fire = Make("Fireball", fireMat, ParticleSystemRenderMode.Billboard, size * 0.3f);
            {
                var main = fire.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 2.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size * 1.15f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = Color.white;
                main.gravityModifier = inAir ? -0.12f : 0f;
                var rot = fire.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
                var drag = fire.limitVelocityOverLifetime; drag.enabled = true; drag.limit = size * 0.4f; drag.dampen = 0.25f;
                var noise = fire.noise; noise.enabled = true; noise.strength = size * 0.35f; noise.frequency = 0.8f; noise.scrollSpeed = 0.6f;
                var col = fire.colorOverLifetime; col.enabled = true;
                col.color = G(new Color(1f, 0.98f, 0.9f), new Color(1f, 0.78f, 0.3f), new Color(0.95f, 0.35f, 0.08f), new Color(0.22f, 0.08f, 0.05f), 1f, 0.85f, 0f);
                var sol = fire.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.35f), new Keyframe(0.25f, 1.25f), new Keyframe(1, 1.9f)));
                var em = fire.emission;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)36), new ParticleSystem.Burst(0.12f, (short)18), new ParticleSystem.Burst(0.3f, (short)12) });
                fire.Play();
            }
            // burning debris on smoke trails
            var bits = Make("Debris", fireMat, ParticleSystemRenderMode.Billboard, size * 0.2f);
            {
                var main = bits.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(size * 2f, size * 6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.5f, 0.2f));
                main.gravityModifier = 1f;
                var tr = bits.trails; tr.enabled = true; tr.ratio = 0.7f; tr.lifetime = new ParticleSystem.MinMaxCurve(0.35f); tr.dieWithParticles = false;
                tr.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0f)));
                tr.colorOverTrail = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.28f, 0.27f, 0.7f), new Color(0.5f, 0.5f, 0.5f, 0f));
                var r = bits.GetComponent<ParticleSystemRenderer>(); r.trailMaterial = smokeMat;
                var col = bits.colorOverLifetime; col.enabled = true;
                col.color = G(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.55f, 0.15f), new Color(0.7f, 0.2f, 0.05f), new Color(0.2f, 0.1f, 0.08f), 1f, 1f, 0f);
                bits.Emit(Mathf.Clamp((int)(size * 9), 12, 60));
            }
            // embers drifting down
            var embers = Make("Embers", fireMat, ParticleSystemRenderMode.Billboard, size * 0.5f);
            {
                var main = embers.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 2.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.45f, 0.15f));
                main.gravityModifier = inAir ? 0.25f : 0.6f;
                var noise = embers.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 1.2f;
                var col = embers.colorOverLifetime; col.enabled = true;
                col.color = G(Color.white, new Color(1f, 0.7f, 0.3f), new Color(1f, 0.4f, 0.1f), new Color(0.4f, 0.1f, 0.05f), 1f, 0.9f, 0f);
                embers.Emit(Mathf.Clamp((int)(size * 14), 20, 110));
            }
            // the smoke column (in air): dark at first, greying as it rises and spreads
            if (inAir)
            {
                var smoke = Make("Smoke", smokeMat, ParticleSystemRenderMode.Billboard, size * 0.4f);
                var main = smoke.main;
                main.duration = 2f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 12f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(size * 0.2f, size * 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.2f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = Color.white;
                main.gravityModifier = -0.05f;
                var vel = smoke.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local; vel.y = new ParticleSystem.MinMaxCurve(size * 0.15f, size * 0.35f);
                var drag = smoke.limitVelocityOverLifetime; drag.enabled = true; drag.limit = 2f; drag.dampen = 0.12f;
                var col = smoke.colorOverLifetime; col.enabled = true;
                col.color = G(new Color(0.24f, 0.22f, 0.2f), new Color(0.34f, 0.33f, 0.32f), new Color(0.46f, 0.45f, 0.44f), new Color(0.62f, 0.62f, 0.62f), 0.85f, 0.6f, 0f);
                var sol = smoke.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.2f, 1.4f), new Keyframe(1, 3.8f)));
                var noise = smoke.noise; noise.enabled = true; noise.strength = 1.4f; noise.frequency = 0.18f; noise.scrollSpeed = 0.12f;
                smoke.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.Distance;
                var em = smoke.emission;
                em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, (short)18), new ParticleSystem.Burst(0.4f, (short)10), new ParticleSystem.Burst(0.9f, (short)8) });
                smoke.Play();
            }
            // a flash of real light
            var lg = new GameObject("Flash");
            lg.transform.SetParent(go.transform, false);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.78f, 0.5f);
            l.range = size * 14f;
            l.intensity = 8f;
            l.shadows = LightShadows.None;
            lg.AddComponent<FxFade>().light = l;
            lg.GetComponent<FxFade>().dur = 0.5f;
            // the shockwave: a pale ring racing out and fading
            ringMesh ??= Ring();
            var rg = new GameObject("Shockwave");
            rg.transform.SetParent(go.transform, false);
            rg.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            var rr = rg.AddComponent<MeshRenderer>();
            var rm = new Material(Mats.Additive) { name = "Shockwave" };
            rm.SetColor("_Color", new Color(1f, 0.9f, 0.75f, 0.4f));
            rr.sharedMaterial = rm;
            rr.shadowCastingMode = ShadowCastingMode.Off;
            var rf = rg.AddComponent<FxFade>();
            rf.mat = rm; rf.dur = 0.55f; rf.scale0 = size * 0.4f; rf.scale1 = size * 4f; rf.alpha0 = 0.4f;
            rg.transform.localScale = Vector3.one * rf.scale0;
            // dust thrown up off the ground
            if (nearGround) Smoke.Dust(at, up, size * 1.4f);
        }

        /// <summary>A flat ring, inner radius 0.8, outer 1 (unit size: scaled as it races out), white with the edges fading.</summary>
        static Mesh Ring()
        {
            const int n = 48;
            var v = new Vector3[n * 3]; var c = new Color[n * 3]; var t = new int[n * 12];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v[i * 3] = d * 0.78f; v[i * 3 + 1] = d * 0.9f; v[i * 3 + 2] = d;
                c[i * 3] = new Color(1, 1, 1, 0); c[i * 3 + 1] = Color.white; c[i * 3 + 2] = new Color(1, 1, 1, 0);
                int j = (i + 1) % n, k = i * 12;
                int a0 = i * 3, b0 = j * 3;
                t[k] = a0; t[k + 1] = b0; t[k + 2] = a0 + 1; t[k + 3] = b0; t[k + 4] = b0 + 1; t[k + 5] = a0 + 1;
                t[k + 6] = a0 + 1; t[k + 7] = b0 + 1; t[k + 8] = a0 + 2; t[k + 9] = b0 + 1; t[k + 10] = b0 + 2; t[k + 11] = a0 + 2;
            }
            // (both sides)
            var t2 = new int[t.Length * 2];
            for (int i = 0; i < t.Length; i += 3) { t2[i] = t[i]; t2[i + 1] = t[i + 1]; t2[i + 2] = t[i + 2]; t2[t.Length + i] = t[i]; t2[t.Length + i + 1] = t[i + 2]; t2[t.Length + i + 2] = t[i + 1]; }
            var m = new Mesh { name = "Shockwave ring", vertices = v, colors = c, triangles = t2 };
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>Fades an effect out and removes it: a light dims, a ring grows and fades.</summary>
    public class FxFade : MonoBehaviour
    {
        public float dur = 0.5f, scale0 = 1f, scale1 = 1f, alpha0 = 1f;
        public Light light;
        public Material mat;
        float t, i0 = -1f;

        void Update()
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / Mathf.Max(0.01f, dur));
            if (light != null) { if (i0 < 0) i0 = light.intensity; light.intensity = i0 * (1f - f) * (1f - f); }
            if (mat != null)
            {
                transform.localScale = Vector3.one * Mathf.Lerp(scale0, scale1, 1f - (1f - f) * (1f - f));
                var c = mat.GetColor("_Color"); c.a = alpha0 * (1f - f); mat.SetColor("_Color", c);
            }
            if (t >= dur) { if (mat != null) Destroy(mat); Destroy(gameObject); }
        }
    }
}
