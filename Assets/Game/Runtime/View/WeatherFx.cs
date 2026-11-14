using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 27: what the weather looks like where the camera is. Rain on Arden (streaks round the camera, grey haze,
    /// dim light), dust storms on Ochre (an orange-brown haze you can't see far through, dust flying past) and, on calm
    /// days there, dust devils wandering over the desert. Up above the weather it all fades out.
    /// </summary>
    public static class WeatherFx
    {
        static ParticleSystem rain, blowing;
        static Material dustMat;
        static readonly int FogId = Shader.PropertyToID("_RkFog");

        class Devil { public ParticleSystem ps; public Vector3d dir; public float life, age; }
        static readonly List<Devil> devils = new List<Devil>();
        static float devilTimer;
        static System.Random rng = new System.Random(27);

        /// <summary>How much of the weather is around the camera: all of it low down, none above the clouds or dust.</summary>
        public static float Below(CelestialBody b, double alt) =>
            b.style == BodyStyle.Earthlike ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2500f, 6000f, (float)alt))
                                           : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4000f, 12000f, (float)alt));

        /// <summary>
        /// Every frame in flight (after the light is set): haze, dimmer sun, rain or blowing dust, dust devils.
        /// eyeRel: the camera, body-relative inertial; up: local up (render space); airVel: the vessel's velocity over the
        /// ground (render space), so rain streaks past a moving ship.
        /// </summary>
        public static void Update(CelestialBody b, Vector3d eyeRel, double ut, Vector3 camPos, Vector3 up, Vector3 groundVel, Light sun,
            Vector3d originAbs, double renderUt, float dt, bool show)
        {
            WeatherNow w = default;
            float k = 0f;
            if (show && Weather.On && Weather.Has(b))
            {
                double alt = eyeRel.magnitude - b.radius;
                k = Below(b, alt);
                if (k > 0.001f) w = Weather.At(b, b.InertialToFixed(eyeRel, ut).normalized, ut);
            }
            float sunUp = Vector3.Dot(up, GameRoot.SunNow);
            float day = Mathf.Clamp(sunUp * 1.3f + 0.12f, 0.02f, 1f);

            // haze and light
            float density = 0f;
            Color fog = Color.black;
            if (b != null && b.style == BodyStyle.Earthlike)
            {
                density = k * (w.rain / 2200f + w.cloud / 45000f);
                fog = new Color(0.5f, 0.53f, 0.58f) * (RenderEnv.SunLight * 0.42f * day);
                if (sun != null) sun.intensity *= 1f - k * Mathf.Max(0.78f * w.rain, 0.55f * w.cloud);
            }
            else if (b != null && b.style == BodyStyle.Desert)
            {
                density = k * w.dust / 650f;
                fog = new Color(0.62f, 0.41f, 0.26f) * (RenderEnv.SunLight * 0.45f * day);
                if (sun != null) sun.intensity *= 1f - k * 0.8f * w.dust;
            }
            Shader.SetGlobalVector(FogId, new Vector4(fog.r, fog.g, fog.b, density));

            // rain streaks round the camera
            float rainRate = k * w.rain * 6500f;
            if (rainRate > 1f && rain == null) rain = MakeRain();
            if (rain != null)
            {
                var em = rain.emission; em.rateOverTime = rainRate;
                if (rainRate > 1f)
                {
                    rain.transform.SetPositionAndRotation(camPos + up * 22f, Quaternion.FromToRotation(Vector3.up, up));
                    Vector3 wind = (b.FixedToInertial(w.windFixed, ut)).ToFloat();
                    var vol = rain.velocityOverLifetime;
                    Vector3 v = Vector3.ClampMagnitude(-up * 13f + wind - groundVel, 160f);
                    vol.x = v.x; vol.y = v.y; vol.z = v.z;
                }
            }

            // blowing dust (Ochre's storms)
            float dustRate = k * w.dust * 2600f;
            if (dustRate > 1f && blowing == null) blowing = MakeBlowing();
            if (blowing != null)
            {
                var em = blowing.emission; em.rateOverTime = dustRate;
                if (dustRate > 1f)
                {
                    blowing.transform.SetPositionAndRotation(camPos, Quaternion.FromToRotation(Vector3.up, up));
                    Vector3 wind = (b.FixedToInertial(w.windFixed, ut)).ToFloat();
                    var vol = blowing.velocityOverLifetime;
                    Vector3 v = Vector3.ClampMagnitude(wind * 1.2f + up * 0.8f - groundVel, 160f);
                    vol.x = v.x; vol.y = v.y; vol.z = v.z;
                }
            }

            UpdateDevils(b, eyeRel, ut, originAbs, renderUt, dt, show && Weather.On && b != null && b.style == BodyStyle.Desert && w.dust < 0.3f && sunUp > 0.1f && k > 0.9f);
        }

        /// <summary>Clears every effect (leaving the flight, or in the builder).</summary>
        public static void Stop()
        {
            Shader.SetGlobalVector(FogId, Vector4.zero);
            if (rain != null) { var em = rain.emission; em.rateOverTime = 0f; rain.Clear(); }
            if (blowing != null) { var em = blowing.emission; em.rateOverTime = 0f; blowing.Clear(); }
            foreach (var d in devils) if (d.ps != null) Object.Destroy(d.ps.gameObject);
            devils.Clear();
        }

        static ParticleSystem MakeRain()
        {
            var go = new GameObject("Rain");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 2.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startColor = new Color(0.75f, 0.8f, 0.9f, 0.32f);
            main.maxParticles = 9000;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(80f, 1f, 80f);
            var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.World;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Additive;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.045f;
            r.lengthScale = 1f;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static ParticleSystem MakeBlowing()
        {
            if (dustMat == null) dustMat = new Material(Resources.Load<Shader>("Shaders/RocketSmoke")) { name = "Blowing dust" };
            var go = new GameObject("Blowing dust");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.78f, 0.56f, 0.38f, 0.28f);
            main.maxParticles = 6000;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(120f, 40f, 120f);
            var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.World;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dustMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.maxParticleSize = 2f;
            ps.Play();
            return ps;
        }

        // ---------------------------------------------------------------- dust devils

        static void UpdateDevils(CelestialBody b, Vector3d eyeRel, double ut, Vector3d originAbs, double renderUt, float dt, bool wanted)
        {
            for (int i = devils.Count - 1; i >= 0; i--)
            {
                var d = devils[i];
                d.age += dt;
                bool over = !wanted || d.age > d.life || b == null;
                if (d.ps == null) { devils.RemoveAt(i); continue; }
                var em = d.ps.emission;
                em.rateOverTime = over ? 0f : 110f;
                if (over && d.age > d.life + 9f || (over && !wanted && d.ps.particleCount == 0)) { Object.Destroy(d.ps.gameObject); devils.RemoveAt(i); continue; }
                if (b == null) continue;
                // drift with the wind, stay on the ground
                var w = Weather.At(b, d.dir, ut);
                d.dir = (d.dir + w.windFixed * (0.5 * dt / b.radius)).normalized;
                double h = Terrain.Height(b, d.dir);
                Vector3d abs = b.AbsolutePosition(renderUt) + b.FixedToInertial(d.dir * (b.radius + h), renderUt);
                Vector3 up = b.FixedToInertial(d.dir, renderUt).ToFloat();
                d.ps.transform.SetPositionAndRotation((abs - originAbs).ToFloat(), Quaternion.LookRotation(up));
            }
            if (!wanted || b == null) return;
            devilTimer -= dt;
            if (devils.Count >= 3 || devilTimer > 0) return;
            devilTimer = 6f + (float)rng.NextDouble() * 10f;
            Vector3d here = b.InertialToFixed(eyeRel, ut).normalized;
            Vector3d e = CelestialBody.EastAt(here), n = CelestialBody.NorthAt(here);
            double a = rng.NextDouble() * System.Math.PI * 2, m = 160 + rng.NextDouble() * 650;
            var dev = new Devil { dir = (here + (e * System.Math.Cos(a) + n * System.Math.Sin(a)) * (m / b.radius)).normalized, life = 40f + (float)rng.NextDouble() * 50f };
            dev.ps = MakeDevil();
            devils.Add(dev);
        }

        static ParticleSystem MakeDevil()
        {
            if (dustMat == null) dustMat = new Material(Resources.Load<Shader>("Shaders/RocketSmoke")) { name = "Blowing dust" };
            var go = new GameObject("Dust devil");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.duration = 5f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;     // the whole column moves with its foot (and spins round its own up)
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.7f, 0.54f, 0.4f, 0.5f);
            main.maxParticles = 1200;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 4f; shape.radius = 1.6f;
            var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.Local;
            vol.x = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Constant(0f, 1f, 0f)); vol.y = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Constant(0f, 1f, 0f));   // (all three in the same mode)
            vol.z = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 4f), new Keyframe(0.3f, 14f), new Keyframe(1, 18f)));
            vol.orbitalZ = 2.2f; vol.radial = 0.45f;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 2.2f)));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0.7f, 0.12f), new GradientAlphaKey(0.35f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dustMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.maxParticleSize = 6f;
            ps.Play();
            return ps;
        }

        /// <summary>For the screenshot tour: dust devils right now.</summary>
        public static int DevilCount => devils.Count;
        public static void SpawnDevilsSoon() => devilTimer = 0f;
    }
}
