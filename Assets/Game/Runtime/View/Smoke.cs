using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 20: exhaust smoke in air. Each engine gets a particle emitter at its nozzle; the puffs live in the
    /// planet's turning frame (the Anchor, moved every frame because the world is drawn around the vessel), so they
    /// stay where they were left as the rocket climbs away. Thick white for solid boosters, thinner for liquid engines,
    /// none in vacuum.
    /// </summary>
    public static class Smoke
    {
        static Transform anchor;
        static Material mat;

        /// <summary>The frame smoke is left behind in: the active world's centre and rotation, in render space.</summary>
        public static Transform Anchor
        {
            get
            {
                if (anchor == null) anchor = new GameObject("SmokeAnchor").transform;
                return anchor;
            }
        }

        public static void SetAnchor(Vector3 bodyCentre, Quaternion bodyRot) => Anchor.SetPositionAndRotation(bodyCentre, bodyRot);

        static Transform ground;

        /// <summary>The ground under the vessel (a plane facing up): exhaust hitting it billows out sideways instead of sinking in.</summary>
        public static Transform Ground
        {
            get
            {
                if (ground == null) ground = new GameObject("SmokeGround").transform;
                return ground;
            }
        }

        public static void SetGround(Vector3 point, Vector3 up) => Ground.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.up, up));

        public static bool Makes(PartDef def) => def.IsEngine && !def.IsJet && def.ispVac < 600 && def.ispAsl >= def.ispVac * 0.6f;

        public static ParticleSystem Create(Transform parent, PartDef def)
        {
            if (mat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/RocketSmoke");
                mat = new Material(sh) { name = "Smoke" };
            }
            bool solid = def.kind == PartKind.Solid;
            float size = Mathf.Max(def.bottomDiameter, 0.6f);
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, -def.length - size * 0.6f, 0);
            go.transform.localRotation = Quaternion.Euler(90f, 0, 0);          // emit along the part's -Y (down the exhaust)
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(solid ? 9f : 5f, solid ? 14f : 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(solid ? 12f : 8f, solid ? 20f : 14f);
            main.startSize = solid ? new ParticleSystem.MinMaxCurve(size * 3f, size * 5f) : new ParticleSystem.MinMaxCurve(size * 2f, size * 3.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = solid ? new Color(0.93f, 0.91f, 0.89f, 0.8f) : new Color(0.9f, 0.9f, 0.92f, 0.45f);
            main.maxParticles = solid ? 900 : 500;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;   // its bounds live in the far-away planet frame
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.customSimulationSpace = Anchor;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = size * 0.35f;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 2.5f;
            drag.dampen = 0.06f;
            var hit = ps.collision;
            hit.enabled = true;
            hit.type = ParticleSystemCollisionType.Planes;
            hit.SetPlane(0, Ground);
            hit.bounce = 0.05f;
            hit.dampen = 0.35f;
            hit.lifetimeLoss = 0f;
            hit.radiusScale = 0.3f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.1f, 1.6f), new Keyframe(1, 6f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.8f, 0.55f), 0f), new GradientColorKey(Color.white, 0.04f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.04f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = solid ? 2.2f : 1.4f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.2f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 4f;
            return ps;
        }

        static Material sparkMat;

        /// <summary>Reentry sparks and embers (milestone 23): glowing bits streaming back off the hot side.</summary>
        public static ParticleSystem CreateSparks(Transform parent)
        {
            if (sparkMat == null) sparkMat = new Material(Resources.Load<Shader>("Shaders/RocketSpark")) { name = "Sparks" };
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.45f, 0.15f));
            main.maxParticles = 600;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;   // left behind in the air, like the smoke
            main.customSimulationSpace = Anchor;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.5f), new GradientColorKey(new Color(0.8f, 0.15f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.3f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = sparkMat;
            r.renderMode = ParticleSystemRenderMode.Stretch;               // streaks along their motion
            r.velocityScale = 0.08f;
            r.lengthScale = 1.5f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>
        /// An explosion (milestone 23): a rolling fireball, a burst of glowing debris that arcs down, and in air a cloud
        /// of dark smoke that rises and spreads. Fire and forget at a render-space point; it cleans itself up.
        /// </summary>
        public static void Burst(Vector3 at, float size, bool inAir)
        {
            if (sparkMat == null) sparkMat = new Material(Resources.Load<Shader>("Shaders/RocketSpark")) { name = "Sparks" };
            if (mat == null) mat = new Material(Resources.Load<Shader>("Shaders/RocketSmoke")) { name = "Smoke" };
            var go = new GameObject("Burst");
            go.transform.position = at;
            ParticleSystem Make(string name, Material m, ParticleSystemRenderMode mode)
            {
                var c = new GameObject(name);
                c.transform.SetParent(go.transform, false);
                var ps = c.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = false; main.playOnAwake = false; main.duration = 1f;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = Anchor;
                var em = ps.emission; em.rateOverTime = 0f;
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = size * 0.25f;
                var r = c.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = m; r.renderMode = mode; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                r.maxParticleSize = 4f;
                return ps;
            }
            Gradient G(Color c0, Color c1, Color c2, float a0, float a1, float a2)
            {
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 0.35f), new GradientColorKey(c2, 1f) },
                          new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(a1, 0.35f), new GradientAlphaKey(a2, 1f) });
                return g;
            }

            // Fireball: big glowing puffs that swell and cool from white-yellow through orange to a dull red
            var fire = Make("Fire", sparkMat, ParticleSystemRenderMode.Billboard);
            {
                var main = fire.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 1.1f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = Color.white;
                main.gravityModifier = inAir ? -0.08f : 0f;
                var drag = fire.limitVelocityOverLifetime; drag.enabled = true; drag.limit = size * 0.5f; drag.dampen = 0.2f;
                var col = fire.colorOverLifetime; col.enabled = true;
                col.color = G(new Color(1f, 0.95f, 0.75f), new Color(1f, 0.5f, 0.12f), new Color(0.45f, 0.08f, 0.04f), 1f, 0.9f, 0f);
                var sol = fire.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.3f, 1.3f), new Keyframe(1, 1.7f)));
                fire.Emit(36);
            }
            // Debris: hot fragments flung out in streaks, falling back down
            var bits = Make("Debris", sparkMat, ParticleSystemRenderMode.Stretch);
            {
                var main = bits.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(size * 1.5f, size * 5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.45f, 0.15f));
                main.gravityModifier = 1f;
                var col = bits.colorOverLifetime; col.enabled = true;
                col.color = G(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.4f, 0.1f), new Color(0.6f, 0.1f, 0.05f), 1f, 0.9f, 0f);
                var r = bits.GetComponent<ParticleSystemRenderer>(); r.velocityScale = 0.06f; r.lengthScale = 1.2f;
                bits.Emit(70);
            }
            // Smoke: a dark rolling cloud that rises, spreads and greys as it thins
            if (inAir)
            {
                var smoke = Make("Smoke", mat, ParticleSystemRenderMode.Billboard);
                var main = smoke.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(size * 0.3f, size * 1.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.4f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = Color.white;
                main.gravityModifier = -0.03f;
                var drag = smoke.limitVelocityOverLifetime; drag.enabled = true; drag.limit = 1.5f; drag.dampen = 0.1f;
                var col = smoke.colorOverLifetime; col.enabled = true;
                col.color = G(new Color(0.16f, 0.14f, 0.13f), new Color(0.24f, 0.23f, 0.22f), new Color(0.5f, 0.5f, 0.5f), 0.95f, 0.8f, 0f);
                var sol = smoke.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.2f, 1.4f), new Keyframe(1, 3.5f)));
                var noise = smoke.noise; noise.enabled = true; noise.strength = 1.2f; noise.frequency = 0.2f; noise.scrollSpeed = 0.15f;
                var hit = smoke.collision; hit.enabled = true; hit.type = ParticleSystemCollisionType.Planes; hit.SetPlane(0, Ground); hit.bounce = 0.05f; hit.dampen = 0.4f; hit.lifetimeLoss = 0f; hit.radiusScale = 0.3f;
                smoke.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.Distance;
                smoke.Emit(28);
            }
            Object.Destroy(go, 12f);
        }

        /// <summary>
        /// Milestone 26: the pad's water deluge before liftoff: white steam and spray billowing out from the deck for a while,
        /// rolling along the ground and rising. Fire and forget at a render-space point ('up' is the local vertical).
        /// </summary>
        public static GameObject Steam(Vector3 at, Vector3 up, float size, float seconds)
        {
            if (mat == null) mat = new Material(Resources.Load<Shader>("Shaders/RocketSmoke")) { name = "Smoke" };
            var go = new GameObject("Steam");
            go.transform.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, up));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = seconds;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.customSimulationSpace = Anchor;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(size * 0.4f, size * 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.35f, size * 0.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.95f, 0.96f, 0.98f, 1f);
            main.gravityModifier = -0.02f;
            main.maxParticles = 600;
            var em = ps.emission; em.rateOverTime = new ParticleSystem.MinMaxCurve(55f, new AnimationCurve(new Keyframe(0, 0.12f), new Keyframe(0.3f, 0.75f), new Keyframe(1, 1f)));
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 80f; shape.radius = size * 0.4f;
            shape.rotation = new Vector3(-90f, 0, 0);
            var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.limit = size * 0.25f; drag.dampen = 0.15f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.92f, 0.93f, 0.95f), 0.4f), new GradientColorKey(new Color(0.85f, 0.86f, 0.88f), 1f) },
                      new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.3f, 1.4f), new Keyframe(1, 3f)));
            var noise = ps.noise; noise.enabled = true; noise.strength = 1.4f; noise.frequency = 0.15f; noise.scrollSpeed = 0.2f;
            var hit = ps.collision; hit.enabled = true; hit.type = ParticleSystemCollisionType.Planes; hit.SetPlane(0, Ground); hit.bounce = 0.02f; hit.dampen = 0.5f; hit.lifetimeLoss = 0f; hit.radiusScale = 0.3f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard; r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.maxParticleSize = 4f;
            ps.Play();
            Object.Destroy(go, seconds + 9f);
            return go;
        }

        public static void DriveSparks(ParticleSystem ps, float strength)
        {
            var em = ps.emission;
            em.rateOverTime = strength > 0.15f ? 220f * strength * strength : 0f;
            if (strength > 0.15f && !ps.isPlaying) ps.Play();
            else if (strength <= 0.15f && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>Per frame: puffs per second from throttle and air pressure (none above the thick air).</summary>
        public static void Drive(ParticleSystem ps, PartDef def, float thrustFrac, float pressure)
        {
            float air = Mathf.Clamp01(pressure * 1.6f);
            float rate = (def.kind == PartKind.Solid ? 90f : 36f) * Mathf.Clamp01(thrustFrac) * air;
            var em = ps.emission;
            em.rateOverTime = rate;
            if (rate > 0.5f && !ps.isPlaying) ps.Play();
            else if (rate <= 0.5f && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
