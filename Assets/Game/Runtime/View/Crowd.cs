using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 23: life around the space center and in the assembly building. Ardling workers walk their rounds
    /// (pausing now and then for a chat), and cars, trucks and forklifts drive their routes. Everything moves along
    /// loops in its parent's local space, so it follows the site or the hangar wherever the world is drawn.
    /// </summary>
    public class Crowd
    {
        class Agent
        {
            public Transform t;
            public Transform[] limbs;      // walkers: arms and legs
            public Vector3[] path;
            public float[] cum;            // distance along the loop at each corner
            public float s, speed, pause, phase, height;
            public bool walker;
            public Transform spin;         // a vehicle's turning part (forklift mast, crane hook...)
            public Transform body;         // walkers: bobs a little with each step (milestone 25)
        }

        readonly Transform root;
        readonly List<Agent> agents = new List<Agent>();
        readonly System.Random rng;

        public Crowd(Transform parent, int seed)
        {
            root = new GameObject("Crowd").transform;
            root.SetParent(parent, false);
            rng = new System.Random(seed);
        }

        public int Count => agents.Count;

        /// <summary>What new walkers wear for work (Ardlings.Role*); -1 picks a mix (milestone 25).</summary>
        public int Role = -1;

        Agent Add(Transform t, Vector3[] loop, float speed, bool walker)
        {
            var a = new Agent { t = t, path = loop, speed = speed, walker = walker, s = (float)rng.NextDouble() * 1000f, phase = (float)rng.NextDouble() * 6f };
            a.cum = new float[loop.Length + 1];
            for (int i = 0; i < loop.Length; i++) a.cum[i + 1] = a.cum[i] + Vector3.Distance(loop[i], loop[(i + 1) % loop.Length]);
            agents.Add(a);
            Place(a, 0);
            return a;
        }

        /// <summary>An Ardling worker on a loop (their look comes from a made-up name, so the crowd is varied).</summary>
        public void AddWalker(Vector3[] loop, float speed = 1.3f)
        {
            string name = CrewNames.Next() + " " + rng.Next(1000);
            var look = Ardlings.LookFor(name);
            int role = Role >= 0 ? Role : new[] { Ardlings.RoleNone, Ardlings.RoleWork, Ardlings.RoleOffice, Ardlings.RoleOffice }[rng.Next(4)];
            int variant = rng.Next(4);
            var go = new GameObject("Worker");
            go.transform.SetParent(root, false);
            // a mix of heights; some of the visitors are children
            float size = role == Ardlings.RoleVisitor && rng.NextDouble() < 0.3 ? 0.74f + (float)rng.NextDouble() * 0.08f : 0.92f + (float)rng.NextDouble() * 0.16f;
            go.transform.localScale = Vector3.one * size;
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0, Ardlings.Height, 0);   // the model hangs down from the helmet top
            var near = new List<Renderer>();
            var far = new List<Renderer>();
            MeshRenderer Piece(Transform parent, Mesh m, List<Renderer> into)
            {
                var g = new GameObject(m.name);
                g.transform.SetParent(parent, false);
                g.AddComponent<MeshFilter>().sharedMesh = m;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = Mats.Lit;
                r.shadowCastingMode = ShadowCastingMode.On;
                into.Add(r);
                return r;
            }
            Piece(body.transform, Ardlings.Body(look, false), near);
            Piece(body.transform, Ardlings.LiteBody(look), far);
            if (role != Ardlings.RoleNone) { var gm = Ardlings.Gear(role, variant); Piece(body.transform, gm, near); Piece(body.transform, gm, far); }
            var note = Ardlings.AddNote(body.transform, look).GetComponent<Renderer>();
            near.Add(note); far.Add(note);
            Mesh[] lm = { Ardlings.Arm(look, -1), Ardlings.Arm(look, 1), Ardlings.Leg(look, -1), Ardlings.Leg(look, 1) };
            Mesh[] ll = { Ardlings.LiteArm(look, -1), Ardlings.LiteArm(look, 1), Ardlings.LiteLeg(look, -1), Ardlings.LiteLeg(look, 1) };
            Vector3[] piv = { Ardlings.ShoulderL, Ardlings.ShoulderR, Ardlings.HipL, Ardlings.HipR };
            var limbs = new Transform[4];
            for (int k = 0; k < 4; k++)
            {
                var lg = new GameObject("Limb");
                lg.transform.SetParent(body.transform, false);
                lg.transform.localPosition = piv[k];
                Piece(lg.transform, lm[k], near);
                Piece(lg.transform, ll[k], far);
                if (k == 1) { var hm = Ardlings.HandItem(role, variant); if (hm != null) Piece(lg.transform, hm, near); }
                limbs[k] = lg.transform;
            }
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.1f, near.ToArray()), new LOD(0.005f, far.ToArray()) });
            lod.RecalculateBounds();
            var a = Add(go.transform, loop, speed * (0.85f + 0.3f * (float)rng.NextDouble()) * Mathf.Lerp(0.8f, 1f, size), true);
            a.limbs = limbs;
            a.body = body.transform;
        }

        /// <summary>A vehicle on a loop: "car", "van", "bus", "truck", "tanker", "plane", "forklift" or "tug".</summary>
        public void AddVehicle(string kind, Vector3[] loop, float speed)
        {
            var go = new GameObject("Vehicle " + kind);
            go.transform.SetParent(root, false);
            Color paint = new[] { new Color(0.85f, 0.85f, 0.83f), new Color(0.12f, 0.45f, 0.52f), new Color(0.9f, 0.6f, 0.15f), new Color(0.7f, 0.15f, 0.12f), new Color(0.2f, 0.25f, 0.35f) }[rng.Next(5)];
            if (kind == "forklift") paint = new Color(0.95f, 0.72f, 0.1f);
            if (kind == "bus" || kind == "plane") paint = new Color(0.9f, 0.91f, 0.92f);
            go.AddComponent<MeshFilter>().sharedMesh = VehicleMesh(kind, paint);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.On;
            // far away a vehicle is a speck: let it go (milestone 24, there are many more of them now)
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(kind == "plane" || kind == "bus" ? 0.0012f : 0.0018f, new Renderer[] { mr }) });
            lod.RecalculateBounds();
            Add(go.transform, loop, speed * (0.9f + 0.2f * (float)rng.NextDouble()), false);
        }

        /// <summary>A big slow machine (a crawler carrying a rocket) going back and forth along a road, facing 'along'.</summary>
        public void AddCrawler(Mesh mesh, Vector3 a, Vector3 b, float speed)
        {
            var go = new GameObject("Crawler");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.On;
            var ag = Add(go.transform, new[] { a, b }, speed, false);
            ag.height = -1;   // keeps its heading both ways
            Vector3 d = b - a; d.y = 0;
            go.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        /// <summary>Something that slides back and forth along a straight rail (the hangar's overhead crane).</summary>
        public void AddShuttle(Mesh mesh, Vector3 a, Vector3 b, float speed)
        {
            var go = new GameObject("Shuttle");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var ag = Add(go.transform, new[] { a, b }, speed, false);
            ag.height = -1;   // keeps its own heading
        }

        public void SetVisible(bool v) { if (root.gameObject.activeSelf != v) root.gameObject.SetActive(v); }

        public void Update(float dt)
        {
            if (!root.gameObject.activeInHierarchy) return;
            dt = Mathf.Min(dt, 0.1f);
            foreach (var a in agents)
            {
                if (a.pause > 0) { a.pause -= dt; Animate(a, 0, dt); continue; }
                a.s += a.speed * dt;
                // Walkers stop for a few seconds now and then
                if (a.walker && rng.NextDouble() < dt * 0.04) a.pause = 1.5f + (float)rng.NextDouble() * 4f;
                Place(a, dt);
                Animate(a, 1, dt);
            }
        }

        void Place(Agent a, float dt)
        {
            float total = a.cum[a.path.Length];
            float s = Mathf.Repeat(a.s, total);
            int i = 0;
            while (i < a.path.Length - 1 && a.cum[i + 1] < s) i++;
            Vector3 p0 = a.path[i], p1 = a.path[(i + 1) % a.path.Length];
            float seg = Mathf.Max(1e-3f, a.cum[i + 1] - a.cum[i]);
            Vector3 pos = Vector3.Lerp(p0, p1, (s - a.cum[i]) / seg);
            a.t.localPosition = pos;
            if (a.height >= 0)
            {
                Vector3 dir = p1 - p0;
                dir.y = 0;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    var want = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    a.t.localRotation = dt <= 0 ? want : Quaternion.RotateTowards(a.t.localRotation, want, dt * (a.walker ? 240f : 90f));
                }
            }
        }

        void Animate(Agent a, float moving, float dt)
        {
            if (!a.walker || a.limbs == null) return;
            a.phase += dt * 8f * moving;
            float swing = Mathf.Sin(a.phase) * 30f * moving;
            float idle = Mathf.Sin(Time.time * 1.6f + a.cum[1]) * 3f;
            if (a.body != null)
            {
                // a little bounce with each step and a lean into the walk; standing, a slow sway
                a.body.localPosition = new Vector3(0, Ardlings.Height + Mathf.Abs(Mathf.Sin(a.phase)) * 0.025f * moving, 0);
                a.body.localRotation = Quaternion.Euler(4f * moving, 0, Mathf.Sin(a.phase) * 2.5f * moving + idle * 0.4f * (1 - moving));
            }
            for (int k = 0; k < 4; k++)
            {
                int side = k % 2 == 0 ? -1 : 1;
                a.limbs[k].localRotation = k < 2 ? Quaternion.Euler(swing * side * 0.8f + idle, 0, side * 5f) : Quaternion.Euler(-swing * side, 0, 0);
            }
        }

        // ---------------------------------------------------------------- vehicles

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// <summary>Simple vehicles facing +Z, wheels on the ground at y = 0.</summary>
        public static Mesh VehicleMesh(string kind, Color paint)
        {
            string key = kind + paint;
            if (meshes.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            Color glass = new Color(0.08f, 0.12f, 0.18f), tyre = new Color(0.08f, 0.08f, 0.09f), metal = new Color(0.75f, 0.76f, 0.78f), crate = new Color(0.62f, 0.45f, 0.25f);
            void Wheels(float x, float z1, float z2, float r)
            {
                b.Mat = MeshBuilder.MatRubber;
                foreach (float z in new[] { z1, z2 })
                    foreach (int sx in new[] { -1, 1 })
                    {
                        int s0 = b.v.Count;
                        b.AddFrustum(0.12f, -0.12f, r, r, tyre, 14, capTop: true, capBottom: true);
                        b.Transform(s0, Quaternion.Euler(0, 0, 90f), new Vector3(sx * x, r, z));
                    }
                b.Mat = MeshBuilder.MatPaint;
            }
            switch (kind)
            {
                case "truck":
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddBox(new Vector3(0, 1.45f, 2.4f), new Vector3(2.3f, 1.9f, 1.9f), paint, Quaternion.identity);       // cab
                    b.Mat = MeshBuilder.MatGlass;
                    b.AddBox(new Vector3(0, 1.85f, 3.36f), new Vector3(2.0f, 0.8f, 0.05f), glass, Quaternion.identity);
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddBox(new Vector3(0, 0.95f, -1.0f), new Vector3(2.4f, 0.25f, 5.2f), metal, Quaternion.identity);    // flatbed
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(new Vector3(0, 1.75f, -1.2f), new Vector3(2.0f, 1.4f, 3.0f), crate, Quaternion.identity);    // load
                    b.AddFrustum(1.0f, -1.0f, 0.55f, 0.55f, new Color(0.9f, 0.9f, 0.88f), 14, capTop: true, capBottom: true);
                    b.Transform(b.v.Count - 62, Quaternion.Euler(90f, 0, 0), new Vector3(0, 3.0f, -1.2f));               // a tank part on top
                    Wheels(1.1f, 2.4f, -2.6f, 0.5f);
                    break;
                case "forklift":
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddBox(new Vector3(0, 0.75f, -0.2f), new Vector3(1.2f, 0.9f, 1.9f), paint, Quaternion.identity);
                    b.Mat = MeshBuilder.MatMetal;
                    foreach (int sx in new[] { -1, 1 }) b.AddBox(new Vector3(sx * 0.5f, 2.0f, -0.6f), new Vector3(0.08f, 1.6f, 0.08f), metal, Quaternion.identity);   // cage
                    b.AddBox(new Vector3(0, 2.8f, -0.4f), new Vector3(1.1f, 0.06f, 1.2f), metal, Quaternion.identity);
                    foreach (int sx in new[] { -1, 1 }) b.AddBox(new Vector3(sx * 0.35f, 1.4f, 0.85f), new Vector3(0.1f, 2.4f, 0.1f), metal, Quaternion.identity);  // mast
                    foreach (int sx in new[] { -1, 1 }) b.AddBox(new Vector3(sx * 0.3f, 0.45f, 1.45f), new Vector3(0.12f, 0.06f, 1.2f), metal, Quaternion.identity); // forks
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(new Vector3(0, 0.95f, 1.45f), new Vector3(1.1f, 0.9f, 1.1f), crate, Quaternion.identity);   // a crate of parts
                    Wheels(0.55f, 0.5f, -0.8f, 0.32f);
                    break;
                case "tug":
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddBox(new Vector3(0, 0.7f, 0), new Vector3(2.0f, 0.8f, 3.2f), paint, Quaternion.identity);
                    b.AddBox(new Vector3(0, 1.4f, -0.6f), new Vector3(1.6f, 0.7f, 1.2f), paint, Quaternion.identity);
                    b.Mat = MeshBuilder.MatGlass;
                    b.AddBox(new Vector3(0, 1.45f, 0.01f), new Vector3(1.5f, 0.5f, 0.05f), glass, Quaternion.identity);
                    Wheels(0.95f, 1.0f, -1.0f, 0.42f);
                    break;
                case "carLow":   // parked cars (hundreds of them in the space centre's car parks): boxes only
                case "vanLow":
                case "busLow":
                {
                    float L = kind == "busLow" ? 11.5f : kind == "vanLow" ? 5.0f : 4.0f, H = kind == "busLow" ? 2.8f : kind == "vanLow" ? 1.9f : 0.6f, W = kind == "busLow" ? 2.5f : 1.8f;
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddBox(new Vector3(0, 0.35f + H * 0.5f, 0), new Vector3(W, H, L), paint, Quaternion.identity);
                    if (kind == "carLow") b.AddBox(new Vector3(0, 1.2f, -0.3f), new Vector3(1.6f, 0.55f, 2.1f), paint, Quaternion.identity);
                    b.Mat = MeshBuilder.MatGlass;
                    float gy = kind == "carLow" ? 1.2f : 0.35f + H * 0.72f;
                    b.AddBox(new Vector3(0, gy, kind == "carLow" ? -0.3f : 0.1f), new Vector3(kind == "carLow" ? 1.64f : W + 0.04f, kind == "carLow" ? 0.4f : H * 0.3f, kind == "carLow" ? 2.0f : L * 0.92f), glass, Quaternion.identity);
                    b.Mat = MeshBuilder.MatRubber;
                    foreach (float z in new[] { L * 0.32f, -L * 0.32f }) b.AddBox(new Vector3(0, 0.33f, z), new Vector3(W + 0.06f, 0.62f, 0.62f), tyre, Quaternion.identity);
                    break;
                }
                case "van":
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddBox(new Vector3(0, 1.25f, -0.3f), new Vector3(2.0f, 1.9f, 4.2f), paint, Quaternion.identity);
                    b.AddBox(new Vector3(0, 0.85f, 2.2f), new Vector3(2.0f, 1.1f, 0.9f), paint, Quaternion.identity);
                    b.Mat = MeshBuilder.MatGlass;
                    b.AddBox(new Vector3(0, 1.75f, 1.82f), new Vector3(1.8f, 0.7f, 0.05f), glass, Quaternion.Euler(-20f, 0, 0));
                    foreach (int sx in new[] { -1, 1 }) b.AddBox(new Vector3(sx * 1.01f, 1.7f, 1.2f), new Vector3(0.04f, 0.6f, 1.0f), glass, Quaternion.identity);
                    b.Mat = MeshBuilder.MatPaint;
                    foreach (int sx in new[] { -1, 1 }) b.AddBox(new Vector3(sx * 1.02f, 1.3f, -0.6f), new Vector3(0.03f, 0.35f, 3.0f), new Color(0.12f, 0.45f, 0.52f), Quaternion.identity);
                    Wheels(0.9f, 1.6f, -1.6f, 0.38f);
                    break;
                case "bus":
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddBox(new Vector3(0, 1.75f, 0), new Vector3(2.5f, 2.8f, 11.5f), paint, Quaternion.identity);
                    b.Mat = MeshBuilder.MatGlass;
                    foreach (int sx in new[] { -1, 1 }) b.AddBox(new Vector3(sx * 1.26f, 2.25f, 0.2f), new Vector3(0.04f, 1.0f, 10.2f), glass, Quaternion.identity);
                    b.AddBox(new Vector3(0, 2.0f, 5.76f), new Vector3(2.3f, 1.5f, 0.05f), glass, Quaternion.identity);
                    b.Mat = MeshBuilder.MatPaint;
                    foreach (int sx in new[] { -1, 1 }) b.AddBox(new Vector3(sx * 1.27f, 1.1f, 0), new Vector3(0.03f, 0.3f, 11.4f), new Color(0.12f, 0.45f, 0.52f), Quaternion.identity);
                    Wheels(1.15f, 3.8f, -3.6f, 0.5f);
                    break;
                case "tanker":
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddBox(new Vector3(0, 1.45f, 3.4f), new Vector3(2.3f, 1.9f, 1.9f), paint, Quaternion.identity);
                    b.Mat = MeshBuilder.MatGlass;
                    b.AddBox(new Vector3(0, 1.85f, 4.36f), new Vector3(2.0f, 0.8f, 0.05f), glass, Quaternion.identity);
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddBox(new Vector3(0, 0.9f, -1.0f), new Vector3(2.0f, 0.25f, 7.6f), metal, Quaternion.identity);
                    b.Mat = MeshBuilder.MatGloss;
                    int tk = b.v.Count;
                    b.AddFrustum(3.6f, -3.6f, 1.15f, 1.15f, new Color(0.88f, 0.89f, 0.9f), 18, capTop: true, capBottom: true);
                    b.Transform(tk, Quaternion.Euler(90f, 0, 0), new Vector3(0, 2.15f, -1.0f));
                    Wheels(1.1f, 3.4f, -3.4f, 0.5f);
                    break;
                case "plane":
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddFrustum(5f, -5f, 0.9f, 0.9f, paint, 18, capTop: true, capBottom: true);
                    b.AddSphere(new Vector3(0, 5f, 0), 0.9f, paint, 18, 6, Mathf.PI * 0.5f);
                    b.Transform(0, Quaternion.Euler(90f, 0, 0), new Vector3(0, 1.6f, 0));
                    b.AddBox(new Vector3(0, 1.8f, 0.3f), new Vector3(12f, 0.15f, 1.6f), paint, Quaternion.identity);
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(new Vector3(0, 3.0f, -4.6f), new Vector3(0.15f, 2.4f, 1.6f), new Color(0.12f, 0.45f, 0.52f), Quaternion.identity);
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddBox(new Vector3(0, 1.9f, -4.6f), new Vector3(4f, 0.12f, 1.2f), paint, Quaternion.identity);
                    b.Mat = MeshBuilder.MatGlass;
                    b.AddBox(new Vector3(0, 2.15f, 4.3f), new Vector3(1.0f, 0.35f, 1.2f), glass, Quaternion.identity);
                    b.Mat = MeshBuilder.MatMetal;
                    foreach (var (x, z) in new[] { (0f, 3.6f), (-1.6f, 0f), (1.6f, 0f) }) b.AddBox(new Vector3(x, 0.55f, z), new Vector3(0.1f, 1.1f, 0.1f), metal, Quaternion.identity);
                    Wheels(1.6f, 0.05f, 0.0f, 0.3f);
                    break;
                default: // car
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddBox(new Vector3(0, 0.65f, 0), new Vector3(1.8f, 0.6f, 4.0f), paint, Quaternion.identity);
                    b.AddBox(new Vector3(0, 1.2f, -0.3f), new Vector3(1.6f, 0.55f, 2.1f), paint, Quaternion.identity);
                    b.Mat = MeshBuilder.MatGlass;
                    b.AddBox(new Vector3(0, 1.2f, 0.76f), new Vector3(1.5f, 0.45f, 0.05f), glass, Quaternion.Euler(-25f, 0, 0));
                    b.AddBox(new Vector3(0, 1.2f, -1.36f), new Vector3(1.5f, 0.45f, 0.05f), glass, Quaternion.Euler(25f, 0, 0));
                    foreach (int sx in new[] { -1, 1 }) b.AddBox(new Vector3(sx * 0.81f, 1.2f, -0.3f), new Vector3(0.04f, 0.42f, 1.8f), glass, Quaternion.identity);
                    b.Mat = MeshBuilder.MatLamp;
                    foreach (int sx in new[] { -1, 1 }) b.AddBox(new Vector3(sx * 0.6f, 0.7f, 2.01f), new Vector3(0.3f, 0.12f, 0.03f), new Color(1f, 0.95f, 0.8f), Quaternion.identity);
                    Wheels(0.85f, 1.3f, -1.3f, 0.34f);
                    break;
            }
            m = b.ToMesh("Vehicle " + kind);
            meshes[key] = m;
            return m;
        }

        /// <summary>The hangar's overhead crane: a beam across the bay with a trolley and a hook.</summary>
        public static Mesh CraneMesh(float span, float drop = 15f)
        {
            var b = new MeshBuilder();
            Color yellow = new Color(0.95f, 0.72f, 0.1f), dark = new Color(0.2f, 0.21f, 0.23f);
            b.Mat = MeshBuilder.MatPaint;
            b.AddBox(Vector3.zero, new Vector3(span, 1.6f, 1.4f), yellow, Quaternion.identity);
            b.AddBox(new Vector3(0, -1.2f, 0), new Vector3(2.4f, 0.8f, 2.0f), yellow, Quaternion.identity);
            b.Mat = MeshBuilder.MatMetal;
            b.AddBox(new Vector3(0, -1.5f - drop * 0.5f, 0), new Vector3(0.08f, drop, 0.08f), dark, Quaternion.identity);
            b.AddBox(new Vector3(0, -1.6f - drop, 0), new Vector3(0.9f, 0.4f, 0.5f), dark, Quaternion.identity);
            return b.ToMesh("Crane");
        }
    }
}
