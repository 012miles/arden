using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 27: what ships leave on the ground. Boot prints where Ardlings walk and tyre tracks where rovers drive
    /// (kept in the world's frame, so they stay where they were made; the oldest go once there are too many), and dust
    /// blown out from under a rocket firing close to the ground: straight out and gone on airless worlds, billowing on
    /// worlds with air, a white spray over water.
    /// </summary>
    public static class GroundFx
    {
        // ---------------------------------------------------------------- prints and tracks

        struct Mark
        {
            public Vector3d dir;        // planet-fixed, unit
            public double h;            // height of the drawn ground there (m)
            public int ver;             // which build of the close-up ground that height is from
            public Vector3 f, u;        // planet-fixed forward and up
            public float len, wid;
            public Color col;
            public bool oval;
        }

        public const int MaxMarks = 2400;
        static readonly Mark[] marks = new Mark[MaxMarks];
        static int count, head;
        static string body;
        static double radius;
        static GameObject go;
        static Mesh mesh;
        static Vector3d origin;
        static bool dirty;
        static float rebuildAt;

        public static int Count => count;

        public static void Clear()
        {
            count = head = 0;
            dirty = true;
        }

        /// <summary>Adds a print or a piece of track: where (planet-fixed direction and the drawn ground's height), which way, how big.</summary>
        public static void Add(CelestialBody b, Vector3d dirFixed, double h, int ver, Vector3d fwdFixed, float len, float wid, Color col, bool oval)
        {
            Vector3d d = dirFixed.normalized;
            Vector3d p = d * (b.radius + h);
            if (body != b.name || count == 0 || (p - origin).magnitude > 20_000) { Clear(); body = b.name; radius = b.radius; origin = p; }
            marks[head] = new Mark { dir = d, h = h, ver = ver, f = fwdFixed.normalized.ToFloat(), u = d.ToFloat(), len = len, wid = wid, col = col, oval = oval };
            head = (head + 1) % MaxMarks;
            if (count < MaxMarks) count++;
            dirty = true;
        }

        /// <summary>
        /// Every frame: keeps the marks where they belong as the world turns, moves them onto the ground when the close-up
        /// ground is rebuilt (a few hundred a frame), and redraws them now and then.
        /// </summary>
        public static void Sync(CelestialBody active, double renderUt, Vector3d originAbs, System.Func<Vector3d, double> drawnHeight, int ver)
        {
            bool show = count > 0 && active != null && active.name == body;
            if (go == null)
            {
                if (!show) return;
                go = new GameObject("Ground marks");
                mesh = new Mesh { name = "Ground marks", indexFormat = IndexFormat.UInt32 };
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.Lit;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = true;
            }
            if (go.activeSelf != show) go.SetActive(show);
            if (!show) return;
            if (drawnHeight != null)
                for (int k = 0, done = 0; k < count && done < 250; k++)
                {
                    if (marks[k].ver == ver) continue;
                    marks[k].h = drawnHeight(marks[k].dir);
                    marks[k].ver = ver;
                    done++;
                    dirty = true;
                }
            go.transform.SetPositionAndRotation((active.AbsolutePosition(renderUt) + active.FixedToInertial(origin, renderUt) - originAbs).ToFloat(), active.RotationAt(renderUt));
            if (dirty && Time.unscaledTime >= rebuildAt) { Rebuild(); rebuildAt = Time.unscaledTime + 0.2f; }
        }

        static void Rebuild()
        {
            dirty = false;
            var b = new MeshBuilder { Mat = MeshBuilder.MatMatte };
            for (int k = 0; k < count; k++)
            {
                var m = marks[k];
                Vector3 c = (m.dir * (radius + m.h) - origin).ToFloat() + m.u * 0.04f;
                Vector3 side = Vector3.Cross(m.u, m.f).normalized * (m.wid * 0.5f);
                Vector3 fwd = m.f * (m.len * 0.5f);
                if (!m.oval) { b.AddQuad(c - fwd - side, c + fwd - side, c + fwd + side, c - fwd + side, m.u, m.col); continue; }
                // a boot: a rounded toe, a narrower heel
                const int seg = 8;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                    Vector3 p0 = c + fwd * Mathf.Cos(a0) + side * Mathf.Sin(a0) * (Mathf.Cos(a0) > 0 ? 1f : 0.8f);
                    Vector3 p1 = c + fwd * Mathf.Cos(a1) + side * Mathf.Sin(a1) * (Mathf.Cos(a1) > 0 ? 1f : 0.8f);
                    b.AddTri(c, p0, p1, m.u, m.col);
                }
            }
            var built = b.ToMesh("Ground marks");
            mesh.Clear();
            mesh.SetVertices(built.vertices);
            mesh.SetNormals(built.normals);
            mesh.SetColors(built.colors);
            var uv = new List<Vector4>(); built.GetUVs(0, uv);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(built.triangles, 0);
            mesh.RecalculateBounds();
            Object.Destroy(built);
        }

        /// <summary>The colour a mark leaves on this ground: darker where it's pressed in (lighter on dark volcanic rock).</summary>
        public static Color MarkColor(CelestialBody b, Vector3d fixedDir)
        {
            Color g = MeshFactory.BodyColor(b, fixedDir.ToFloat());
            float lum = g.r * 0.3f + g.g * 0.59f + g.b * 0.11f;
            float k = lum < 0.12f ? 1.5f : 0.62f;
            return new Color(g.r * k, g.g * k, g.b * k, 1f);
        }

        // ---------------------------------------------------------------- engine dust

        static ParticleSystem dust;
        static Material dustMat;
        static bool dustAir;

        /// <summary>
        /// Every frame: dust blown out from under a rocket close to the ground (strength 0..1 from the thrust and the
        /// height), where it hits (render space), which way is up, the ground's colour; strength 0 stops it.
        /// </summary>
        public static void Dust(float strength, Vector3 at, Vector3 up, Color col, bool air, float size)
        {
            if (dust == null)
            {
                if (strength <= 0.01f) return;
                if (dustMat == null) dustMat = new Material(Resources.Load<Shader>("Shaders/RocketSmoke")) { name = "Dust" };
                var g = new GameObject("Engine dust");
                dust = g.AddComponent<ParticleSystem>();
                dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = dust.main;
                main.loop = true; main.playOnAwake = false; main.duration = 5f;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = Smoke.Anchor;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.maxParticles = 1500;
                var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radiusThickness = 0f; shape.arc = 360f; shape.rotation = new Vector3(-90f, 0, 0);
                var lift = dust.velocityOverLifetime; lift.enabled = true; lift.space = ParticleSystemSimulationSpace.Local;
                lift.x = 0f; lift.y = 0f; lift.z = 0f;
                var colt = dust.colorOverLifetime; colt.enabled = true;
                var gr = new Gradient();
                gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                           new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0.45f, 0.4f), new GradientAlphaKey(0f, 1f) });
                colt.color = gr;
                var r = g.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = dustMat; r.renderMode = ParticleSystemRenderMode.Billboard; r.sortMode = ParticleSystemSortMode.Distance;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.maxParticleSize = 3f;
                dustAir = !air;   // forces the settings below
                dust.Play();
            }
            if (air != dustAir)
            {
                dustAir = air;
                var main = dust.main;
                var drag = dust.limitVelocityOverLifetime;
                var sol = dust.sizeOverLifetime; sol.enabled = true;
                if (air)
                {
                    // billows: slows in the air, swells, lingers
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
                    main.gravityModifier = -0.01f;
                    drag.enabled = true; drag.limit = 3f; drag.dampen = 0.12f;
                    sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(0.4f, 1.3f), new Keyframe(1, 2.4f)));
                }
                else
                {
                    // no air: every grain flies straight out and is gone
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                    main.gravityModifier = 0f;
                    drag.enabled = false;
                    sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 1.6f)));
                }
            }
            var em = dust.emission;
            if (strength <= 0.01f) { em.rateOverTime = 0f; return; }
            dust.transform.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, up));
            var m2 = dust.main;
            float s = Mathf.Clamp(size, 1f, 8f);
            m2.startSpeed = air ? new ParticleSystem.MinMaxCurve(6f * s * 0.6f, 14f * s * 0.6f) : new ParticleSystem.MinMaxCurve(18f, 40f);
            m2.startSize = air ? new ParticleSystem.MinMaxCurve(1.2f * s, 2.6f * s) : new ParticleSystem.MinMaxCurve(0.4f * s, 1.1f * s);
            m2.startColor = new Color(col.r, col.g, col.b, 1f);
            var sh = dust.shape; sh.radius = s * 0.6f;
            var lv = dust.velocityOverLifetime; lv.x = 0f; lv.z = 0f; lv.y = air ? new ParticleSystem.MinMaxCurve(0.5f, 2.5f) : new ParticleSystem.MinMaxCurve(0.2f, 1.6f);
            em.rateOverTime = strength * (air ? 110f : 220f);
        }

        /// <summary>Removes the dust (leaving a flight).</summary>
        public static void StopDust()
        {
            if (dust != null) { var em = dust.emission; em.rateOverTime = 0f; dust.Clear(); }
        }
    }
}
