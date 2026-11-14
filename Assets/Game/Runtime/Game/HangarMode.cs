using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// The 3D rocket builder. Pick a part from the list and it follows the mouse: it snaps to free stack nodes
    /// or sticks to the side of any part (with radial or mirror symmetry). Tools: Place, Offset, Rotate, Root.
    /// Right-click a part for paint, action groups and subassemblies. The right panel edits the staging.
    /// </summary>
    public class HangarMode
    {
        static readonly Color Bg = new Color(0.16f, 0.17f, 0.18f);
        static readonly int[] SymSteps = { 1, 2, 3, 4, 6, 8 };
        static readonly PartKind[] CategoryOrder =
        {
            PartKind.Command, PartKind.Parachute, PartKind.HeatShield, PartKind.EscapeTower, PartKind.NoseCone, PartKind.Tank, PartKind.Engine,
            PartKind.Solid, PartKind.Decoupler, PartKind.RadialDecoupler, PartKind.Adapter, PartKind.Fins, PartKind.Rcs, PartKind.MonoTank,
            PartKind.Leg, PartKind.Light, PartKind.Fairing, PartKind.Solar, PartKind.Antenna, PartKind.Wing, PartKind.ControlSurface, PartKind.Gear, PartKind.Frame, PartKind.Wheel,
            PartKind.DockingPort, PartKind.Grabber, PartKind.Habitat, PartKind.Battery, PartKind.Science,
            PartKind.Scanner, PartKind.Drill, PartKind.OreTank, PartKind.Converter, PartKind.Radiator,
        };
        enum Tool { Place, Offset, Rotate, Root }

        readonly GameRoot root;
        public VesselDesign design;
        /// <summary>Milestone 30, a rocket room: someone else is the builder; this player watches the rocket and comments on it.</summary>
        public bool spectate;

        /// <summary>The builder's latest rocket, from the room.</summary>
        public void SetDesignFromNet(VesselDesign d)
        {
            design = d;
            selected = -1;
            held = null;
            menuPart = -1;
            Rebuild();
        }
        VesselView view;
        GameObject env;
        Vector3 offset;
        float yaw = 30f, pitch = 12f, dist = 30f, targetY = 5f;
        Tool tool = Tool.Place;
        int symIndex;
        bool mirror, snap = true;

        List<PartNode> held;
        readonly List<VesselView> ghosts = new List<VesselView>();
        Quaternion heldExtra = Quaternion.identity;
        bool placeValid;
        int placeParent = -1;
        Attach placeAttach;
        Vector3 placePos;
        Quaternion placeRot = Quaternion.identity;

        int selected = -1, hover = -1, menuPart = -1;
        Vector2 menuPos, rightDownPos;
        readonly List<string> undo = new List<string>();

        Vector2 partsScroll, stageScroll;
        List<StageReport> reports = new List<StageReport>();
        List<StageAction> stageList = new List<StageAction>();
        string validation;
        List<string> warnings = new List<string>();
        bool showLoad;
        string[] saved = new string[0], subs = new string[0];
        string toast;
        float toastUntil;
        PartDef hoverDef;
        Rect partsRect;

        int Symmetry => SymSteps[symIndex];
        Camera Cam => root.nearCam;

        // Plane view lays the design down horizontally (nose forward, top up), like a spaceplane hangar.
        bool planeView, runwayLaunch, markers = true;
        int launchPad;   // which pad (LaunchSite.Pads) when not the runway (milestone 25)
        UnityEngine.Bounds viewBounds;
        Quaternion ViewRot => planeView ? Quaternion.LookRotation(Vector3.up, Vector3.forward) : Quaternion.identity;
        Vector3 ToWorld(Vector3 p) => offset + ViewRot * p;
        Vector3 ToDesign(Vector3 w) => Quaternion.Inverse(ViewRot) * (w - offset);

        public HangarMode(GameRoot root) { this.root = root; }

        // ---------------------------------------------------------------- lifecycle

        public void Enter(VesselDesign d)
        {
            design = d.Clone();
            selected = -1;
            held = null;
            hall = default;
            runwayLaunch = design.IsPlane;
            try { launchPad = Mathf.Clamp(PlayerPrefs.GetInt("launchPad", 0), 0, LaunchSite.Pads.Length - 1); } catch { launchPad = 0; }
            SetView(design.IsPlane);
            // Between flights: fail overdue contracts, top up the offers, and show what happened.
            var c = root.universe.career;
            c.CheckDeadlines(root.universe.ut);
            c.RefreshOffers(root.universe.ut);
            foreach (var n in c.news) newsQueue.Enqueue(n);
            c.news.Clear();
        }

        void SetView(bool plane)
        {
            planeView = plane;
            if (bay != null) foreach (var t in bay) if (t != null) t.SetActive(!plane);
            yaw = plane ? 60f : 30f;
            pitch = plane ? 22f : 12f;
            Rebuild();
            FitCamera();
        }

        public void Exit()
        {
            ClearGhosts();
            view?.Destroy();
            view = null;
            if (env != null) UnityEngine.Object.Destroy(env);
            env = null;
            bay = null;
            crowd = null;
            held = null;
        }

        /// <summary>Points the builder camera (for the screenshot tours).</summary>
        public void SetView(float yaw, float pitch, float dist, float targetY)
        {
            this.yaw = yaw; this.pitch = pitch; this.dist = dist; this.targetY = targetY;
        }

        void FitCamera()
        {
            if (planeView)
            {
                Vector3 s = viewBounds.size;
                dist = Mathf.Clamp(Mathf.Max(2f, Mathf.Max(s.x, s.z)) * 1.5f + 6f, 10f, 160f);
                targetY = Mathf.Max(0.5f, viewBounds.center.y);
                return;
            }
            Bounds(design.nodes, out float lo, out float hi);
            float h = Mathf.Max(2f, hi - lo);
            dist = Mathf.Clamp(h * 1.3f + 8f, 10f, Mathf.Max(12f, hall.half * 0.95f));
            targetY = h * 0.5f;
        }

        void BuildEnv()
        {
            if (env != null) UnityEngine.Object.Destroy(env);
            var b = BuildHangar(hall);
            env = new GameObject("Hangar");
            env.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Hangar");
            var mr = env.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            crowd = BuildCrowd(env.transform, hall);
            BuildOutside(env.transform, hall);
            bay = new[] { MakeBayTower(1), MakeBayTower(-1) };
            foreach (var t in bay) t.SetActive(!planeView);
        }

        Crowd crowd;
        // ---------------------------------------------------------------- the high bay (milestone 24)

        /// <summary>
        /// The hall is built to fit what's on the floor (milestone 24, captaincarl24: "the rocket is supposed to be as big as the
        /// hangar"): the work towers stand about as tall as the rocket, the roof a little above them, the floor about as wide
        /// as the hall is tall, and the door nearly the hall's height. A 20 m rocket gets an 84 m hall 38 m high; the biggest
        /// rockets get the old 340 m one. People, benches and crates stay their real size. It's rebuilt when the rocket grows
        /// or shrinks past a step.
        /// </summary>
        public struct Hall
        {
            public float half, top, doorHalf, doorTop, x0, x1, tz;
            public int levels;
            public bool Same(Hall o) => Mathf.Approximately(half, o.half) && Mathf.Approximately(top, o.top) && Mathf.Approximately(x0, o.x0) && levels == o.levels;
        }
        Hall hall;
        public Hall CurrentHall => hall;

        public static Hall HallFor(float height, float radius)
        {
            height = Mathf.Ceil(Mathf.Max(height, 8f) / 6f) * 6f;            // in steps, so it isn't rebuilt for every part
            radius = Mathf.Ceil(Mathf.Max(radius, 1f) / 1.5f) * 1.5f;
            var h = new Hall();
            h.levels = Mathf.Clamp(Mathf.CeilToInt((height - 2f) / 8f), 2, 15);
            h.top = Mathf.Min(170f, Mathf.Ceil(Mathf.Max(h.levels * 8f + 14f, height * 1.2f + 6f) / 2f) * 2f);
            h.x0 = Mathf.Max(7f, radius + 4.5f);
            h.x1 = h.x0 + Mathf.Clamp(height * 0.22f, 9f, 20f);
            h.tz = Mathf.Clamp(height * 0.18f, 6.5f, 18f);
            h.half = Mathf.Min(170f, Mathf.Max(Mathf.Max(h.top * 1.05f, h.x1 + 26f), 42f));
            h.doorHalf = Mathf.Clamp(Mathf.Max(radius + 6f, h.half * 0.24f), 9f, 42f);
            h.doorTop = h.top - Mathf.Clamp(h.top * 0.08f, 4f, 12f);
            return h;
        }

        GameObject[] bay;

        GameObject MakeBayTower(int side)
        {
            var go = new GameObject(side > 0 ? "Bay tower east" : "Bay tower west");
            go.transform.SetParent(env.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = BuildBayTower(side, hall).ToMesh("BayTower");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }

        /// <summary>A tower hides while it stands between the camera and the rocket, so building is never blocked.</summary>
        void UpdateBayVisibility(Vector3 camPos, Vector3 target)
        {
            if (bay == null) return;
            for (int i = 0; i < bay.Length; i++)
            {
                if (bay[i] == null) continue;
                float s = i == 0 ? 1f : -1f;
                float bin = hall.x0 - 0.5f, bout = hall.x1 + 5.5f, bz = hall.tz + 0.5f;
                float x0 = Mathf.Min(s * bin, s * bout), x1 = Mathf.Max(s * bin, s * bout);
                bool blocks = !planeView && SegmentHitsBox(new Vector2(camPos.x, camPos.z), new Vector2(target.x, target.z), new Vector2(x0, -bz), new Vector2(x1, bz));
                bool show = !planeView && !blocks;
                if (bay[i].activeSelf != show) bay[i].SetActive(show);
            }
        }

        static bool SegmentHitsBox(Vector2 a, Vector2 b, Vector2 lo, Vector2 hi)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            for (int k = 0; k < 2; k++)
            {
                float p = d[k], q0 = lo[k] - a[k], q1 = hi[k] - a[k];
                if (Mathf.Abs(p) < 1e-6f) { if (q0 > 0 || q1 < 0) return false; continue; }
                float u0 = q0 / p, u1 = q1 / p;
                if (u0 > u1) { float tmp = u0; u0 = u1; u1 = tmp; }
                t0 = Mathf.Max(t0, u0); t1 = Mathf.Min(t1, u1);
                if (t0 > t1) return false;
            }
            return true;
        }

        /// <summary>
        /// One side of the high bay: a 120 m steel tower beside the rocket (braced columns, a deck every 8 m), yellow work
        /// platforms reaching in toward the rocket with handrails and lamps underneath, lit work cabins, a zig-zag stair
        /// tower, coloured service pipes, cable trays, level markers, kit on the decks, and half the bridge across the top.
        /// side = +1 builds the east tower (x &gt; 0), -1 the west.
        /// </summary>
        static MeshBuilder BuildBayTower(int side, Hall hall)
        {
            var b = new MeshBuilder();
            float s = side;
            Color steel = new Color(0.4f, 0.42f, 0.45f), steelDark = new Color(0.28f, 0.3f, 0.33f), deck = new Color(0.5f, 0.51f, 0.53f);
            Color yellow = new Color(0.93f, 0.72f, 0.12f), white = new Color(0.86f, 0.87f, 0.88f), lamp = new Color(1f, 0.95f, 0.85f), warm = new Color(1f, 0.62f, 0.28f);
            Color dark = new Color(0.12f, 0.13f, 0.14f), blue = new Color(0.18f, 0.36f, 0.62f), green = new Color(0.2f, 0.5f, 0.3f), orange = new Color(0.92f, 0.45f, 0.12f);
            float X0 = hall.x0, X1 = hall.x1, Z = hall.tz;
            const float Lv = 8f;
            int levels = hall.levels;
            float topY = levels * Lv;
            float[] xs = { X0, (X0 + X1) * 0.5f, X1 };
            float[] zs = { -Z, -Z / 3f, Z / 3f, Z };
            var rng = new System.Random(side > 0 ? 31 : 47);
            Vector3 V(float x, float y, float z) => new Vector3(s * x, y, z);
            void Beam(Vector3 a, Vector3 c, float t, Color col)
            {
                Vector3 d = c - a;
                Vector3 dn = d.normalized;
                b.AddBox((a + c) * 0.5f, new Vector3(t, t, d.magnitude), col, Quaternion.LookRotation(dn, Mathf.Abs(dn.y) > 0.99f ? Vector3.forward : Vector3.up));
            }
            void Slab(Vector3 a, Vector3 c, float width, float thick, Color col)
            {
                Vector3 d = c - a;
                b.AddBox((a + c) * 0.5f, new Vector3(width, thick, d.magnitude), col, Quaternion.LookRotation(d.normalized, Vector3.up));
            }
            void Box(float x, float y, float z, Vector3 size, Color col) => b.AddBox(V(x, y, z), size, col, Quaternion.identity);

            // Columns on plinths, a beam grid at every level, X bracing on the three outer faces
            b.Mat = MeshBuilder.MatPaint;
            foreach (float x in xs) foreach (float z in zs)
            {
                Box(x, 0.3f, z, new Vector3(1.8f, 0.6f, 1.8f), new Color(0.55f, 0.55f, 0.53f));
                Beam(V(x, 0, z), V(x, topY + 4f, z), 0.9f, steel);
            }
            for (int k = 1; k <= levels; k++)
            {
                float y = k * Lv;
                foreach (float x in xs) Beam(V(x, y, -Z), V(x, y, Z), 0.55f, steel);
                foreach (float z in zs) Beam(V(X0, y, z), V(X1, y, z), 0.55f, steel);
                float y0 = y - Lv;
                for (int i = 0; i < zs.Length - 1; i++)
                {
                    Beam(V(X1, y0, zs[i]), V(X1, y, zs[i + 1]), 0.32f, steelDark);
                    Beam(V(X1, y0, zs[i + 1]), V(X1, y, zs[i]), 0.32f, steelDark);
                }
                for (int i = 0; i < xs.Length - 1; i++)
                    foreach (float z in new[] { -Z, Z })
                    {
                        Beam(V(xs[i], y0, z), V(xs[i + 1], y, z), 0.32f, steelDark);
                        Beam(V(xs[i + 1], y0, z), V(xs[i], y, z), 0.32f, steelDark);
                    }
            }

            // Decks: one at every level; every third reaches further in as a yellow work platform
            for (int k = 1; k < levels; k++)
            {
                float y = k * Lv;
                bool reach = k % 3 == 0;
                float inner = X0 - (reach ? 3.6f : 2.4f);
                b.Mat = MeshBuilder.MatMetal;
                Box((inner + X1) * 0.5f, y + 0.22f, 0, new Vector3(X1 - inner, 0.32f, 2 * Z - 0.4f), reach ? yellow : deck);
                b.Mat = MeshBuilder.MatPaint;
                Box(inner + 0.1f, y + 0.55f, 0, new Vector3(0.2f, 0.35f, 2 * Z - 0.4f), reach ? new Color(0.2f, 0.2f, 0.2f) : yellow);   // kick plate
                // handrails along the inner edge and the two ends
                b.Mat = MeshBuilder.MatMetal;
                for (float z = -Z + 0.6f; z <= Z - 0.5f; z += 1.8f) Box(inner + 0.15f, y + 0.95f, z, new Vector3(0.07f, 1.1f, 0.07f), yellow);
                Box(inner + 0.15f, y + 1.5f, 0, new Vector3(0.07f, 0.07f, 2 * Z - 0.8f), yellow);
                Box(inner + 0.15f, y + 1.0f, 0, new Vector3(0.05f, 0.05f, 2 * Z - 0.8f), yellow);
                foreach (float z in new[] { -Z + 0.3f, Z - 0.3f })
                {
                    Box((inner + X1) * 0.5f, y + 1.5f, z, new Vector3(X1 - inner, 0.07f, 0.07f), yellow);
                    for (float x = inner + 1.8f; x < X1; x += 1.8f) Box(x, y + 0.95f, z, new Vector3(0.07f, 1.1f, 0.07f), yellow);
                }
                // lamps under the deck
                b.Mat = MeshBuilder.MatLamp;
                foreach (float z in new[] { -Z * 0.66f, 0f, Z * 0.66f }) Box(inner + 1.6f, y - 0.05f, z, new Vector3(1.8f, 0.12f, 0.5f), lamp);
                Box(X0 + (X1 - X0) * 0.7f, y - 0.05f, 0, new Vector3(1.2f, 0.1f, 0.4f), lamp);
                // level marker on the inner column
                b.Mat = MeshBuilder.MatPaint;
                Box(X0 - 0.5f, y + 2.6f, -Z + 0.6f, new Vector3(0.05f, 1.0f, 1.4f), yellow);
                Box(X0 - 0.53f, y + 2.6f, -Z + 0.6f, new Vector3(0.05f, 0.3f, 0.9f), dark);
                // kit on the deck: cabinets, cases, a hose reel
                for (int j = 0; j < 4; j++)
                {
                    float x = X0 + 4f + (float)rng.NextDouble() * Mathf.Max(1f, X1 - X0 - 7f), z = -Z + 3f + (float)rng.NextDouble() * (2 * Z - 6f);
                    if (Mathf.Abs(z) < 1.5f) continue;
                    float hgt = 0.6f + (float)rng.NextDouble() * 1.4f;
                    Color c = new[] { steel, blue, orange, white, green }[rng.Next(5)];
                    Box(x, y + 0.38f + hgt * 0.5f, z, new Vector3(0.8f + (float)rng.NextDouble() * 1.2f, hgt, 0.6f + (float)rng.NextDouble() * 0.8f), c);
                }
            }

            // Work cabins with lit windows on some decks
            float cz = -Z + 3.6f;
            foreach (int k in side > 0 ? new[] { 1, 3, 7, 11 } : new[] { 2, 5, 9, 13 })
            {
                if (k >= levels) continue;
                float y = k * Lv + 0.38f;
                b.Mat = MeshBuilder.MatPaint;
                Box(X1 - 3.5f, y + 1.6f, cz, new Vector3(6.5f, 3.2f, 6f), k % 2 == 0 ? white : new Color(0.75f, 0.77f, 0.8f));
                Box(X1 - 3.5f, y + 3.3f, cz, new Vector3(6.9f, 0.25f, 6.4f), steelDark);
                b.Mat = MeshBuilder.MatLamp;
                for (int w = 0; w < 3; w++) Box(X1 - 6.8f, y + 1.9f, cz - 2f + w * 2f, new Vector3(0.06f, 1.2f, 1.5f), warm);
                Box(X1 - 3.5f, y + 1.9f, cz - 3.05f, new Vector3(1.2f, 1.2f, 0.06f), warm);
            }

            // Stair tower on the outside face: zig-zag flights with landings and rails
            float sx0 = X1 + 0.6f, sx1 = X1 + 5.4f, sxm = (sx0 + sx1) * 0.5f;
            b.Mat = MeshBuilder.MatPaint;
            foreach (float x in new[] { sx0, sx1 }) foreach (float z in new[] { -6.5f, 6.5f }) Beam(V(x, 0, z), V(x, topY, z), 0.35f, steel);
            for (int k = 0; k < levels; k++)
            {
                float y0 = k * Lv, ym = y0 + Lv * 0.5f, y1 = y0 + Lv;
                b.Mat = MeshBuilder.MatMetal;
                Slab(V(sxm - 1.2f, y0, -5f), V(sxm - 1.2f, ym, 5f), 2.0f, 0.22f, deck);
                Slab(V(sxm + 1.2f, ym, 5f), V(sxm + 1.2f, y1, -5f), 2.0f, 0.22f, deck);
                Box(sxm, ym, 5.9f, new Vector3(4.8f, 0.25f, 1.8f), deck);
                Box(sxm, y1, -5.9f, new Vector3(4.8f, 0.25f, 1.8f), deck);
                b.Mat = MeshBuilder.MatPaint;
                Beam(V(sx1, y0 + 1f, -5f), V(sx1, ym + 1f, 5f), 0.06f, yellow);
                Beam(V(sx0, ym + 1f, 5f), V(sx0, y1 + 1f, -5f), 0.06f, yellow);
                foreach (float z in new[] { -6.5f, 6.5f }) Beam(V(sx0, y1, z), V(sx1, y1, z), 0.3f, steel);
            }

            // Service pipes up the inner front column, cable trays along each level
            b.Mat = MeshBuilder.MatPaint;
            Color[] pipeCols = { blue, green, white, orange };
            for (int p = 0; p < 4; p++)
            {
                int s0 = b.v.Count;
                b.AddFrustum(topY, 0f, 0.14f + p * 0.02f, 0.14f + p * 0.02f, pipeCols[p], 10);
                b.Transform(s0, Quaternion.identity, V(X0 + 1.2f + p * 0.45f, 0, Z + 0.7f));   // up the back column, out of the camera's way
            }
            for (int k = 1; k < levels; k++)
            {
                b.AddBox(V(X1 - 0.8f, k * Lv - 0.6f, 0), new Vector3(0.5f, 0.12f, 2 * Z), yellow, Quaternion.identity);
                b.AddBox(V(X1 - 0.8f, k * Lv - 0.5f, 0), new Vector3(0.35f, 0.1f, 2 * Z), dark, Quaternion.identity);
            }

            // Half the bridge across the top, with a row of floodlights under it
            b.Mat = MeshBuilder.MatPaint;
            foreach (float z in new[] { -Z, Z })
            {
                Beam(V(0, topY + 4f, z), V(X1, topY + 4f, z), 1.2f, steel);
                Beam(V(0, topY + 1f, z), V(X1, topY + 1f, z), 0.6f, steel);
                for (float x = 1.5f; x < X1; x += 3.5f) Beam(V(x, topY + 1f, z), V(x + 3.5f, topY + 4f, z), 0.3f, steelDark);
            }
            for (float x = 1.5f; x < X1; x += 3.5f) Beam(V(x, topY + 4f, -Z), V(x, topY + 4f, Z), 0.4f, steelDark);
            b.Mat = MeshBuilder.MatLamp;
            for (float x = 3f; x < X1; x += 6f) foreach (float z in new[] { -Z * 0.55f, 0f, Z * 0.55f }) Box(x, topY + 0.3f, z, new Vector3(1.6f, 0.3f, 1.0f), lamp);

            // On the floor: hazard stripes along the tower's inner edge
            b.Mat = MeshBuilder.MatMatte;
            for (int k = 0; k < (int)Z; k++) Box(X0 - 2.8f, 0.008f, -Z + 1f + k * 2f, new Vector3(0.6f, 0.012f, 1f), k % 2 == 0 ? yellow : dark);
            return b;
        }



        /// <summary>Milestone 23: the assembly building at work: Ardlings on their rounds, forklifts with crates, a truck coming in, the crane.</summary>
        static Crowd BuildCrowd(Transform env, Hall hall)
        {
            var c = new Crowd(env, 11) { Role = Ardlings.RoleWork };
            Vector3 P(float x, float z) => new Vector3(x, 0, z);
            BuildProps(env, hall);
            bool Inside(Vector3[] pts) { foreach (var q in pts) if (Mathf.Abs(q.x) > hall.half - 3f || Mathf.Abs(q.z) > hall.half - 3f) return false; return true; }
            void W(Vector3[] pts, float speed = 1.3f) { if (Inside(pts)) c.AddWalker(pts, speed); }
            void V(string kind, Vector3[] pts, float speed) { if (Inside(pts)) c.AddVehicle(kind, pts, speed); }
            float fx = hall.x0 - 2.2f, fz = hall.tz - 1f, D = hall.half;
            // Close to the rocket: crews going round the work area and between the benches
            for (int k = 0; k < 4; k++) W(new[] { P(-20 - k, 24 + k), P(20 + k, 26), P(23, 33 + k), P(-23, 31) }, 1.1f + 0.1f * k);
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(fx, fz - k), P(fx, -fz + k), P(fx + 0.8f, -fz), P(fx + 0.8f, fz) }, 0.9f);   // at the foot of the towers
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(-fx, -fz + k), P(-fx, fz - k), P(-fx - 0.8f, fz), P(-fx - 0.8f, -fz) }, 1.0f);
            for (int k = 0; k < 2; k++) W(new[] { P(36, 22 + k), P(30, 52), P(44, 54 - k), P(48, 26) }, 1.2f);
            for (int k = 0; k < 2; k++) W(new[] { P(-38, 26 + k), P(-46, 36), P(-30, 64 - k), P(-26, 34) }, 1.3f);
            V("forklift", new[] { P(28, 40), P(-28, 40), P(-28, 46), P(28, 46) }, 1.8f);
            V("tug", new[] { P(40, -24), P(40, 60), P(46, 60), P(46, -24) }, 2.4f);
            for (int k = 0; k < 4; k++) W(new[] { P(-44 - k, -34), P(44, -36 - k), P(46 + k, 34), P(-46, 35 + k) });
            for (int k = 0; k < 3; k++) W(new[] { P(-12 + k * 2, -60), P(-12, -150), P(12, -150 + k * 3), P(12, -60) });
            for (int k = 0; k < 3; k++) W(new[] { P(70, 60 + k * 4), P(110, 60), P(110, 100 - k * 3), P(70, 100) });
            for (int k = 0; k < 2; k++) W(new[] { P(-90, -90 + k * 5), P(-60, -100), P(-70, -60), P(-100, -70) });
            V("forklift", new[] { P(-60, -20), P(-60, 40), P(-52, 40), P(-52, -20) }, 2.2f);
            V("forklift", new[] { P(62, 30), P(62, -40), P(54, -40), P(54, 30) }, 2.0f);
            V("forklift", new[] { P(-110, 80), P(-40, 80), P(-40, 88), P(-110, 88) }, 2.4f);
            // routes that scale with the hall, so a small hall is busy too: round the front and back of the work towers,
            // a forklift across the floor between the rocket and the door, people along the side walls
            float wx = hall.x1 + 3f, wz = hall.tz + 4f;
            for (int k = 0; k < 3; k++) W(new[] { P(-wx + k * 2, wz), P(wx, wz + k * 0.7f), P(wx - k, wz + 5f), P(-wx, wz + 4f) }, 1.1f + 0.1f * k);
            for (int k = 0; k < 3; k++) W(new[] { P(wx - k * 2, -wz), P(-wx, -wz - k * 0.7f), P(-wx + k, -wz - 5f), P(wx, -wz - 4f) }, 1.0f + 0.1f * k);
            V("forklift", new[] { P(-(D - 12), wz + 9), P(D - 12, wz + 9), P(D - 12, wz + 14), P(-(D - 12), wz + 14) }, 2.0f);
            V("tug", new[] { P(-(D - 14), -wz - 12), P(D - 14, -wz - 12), P(D - 14, -wz - 17), P(-(D - 14), -wz - 17) }, 2.4f);
            foreach (int sgn in new[] { -1, 1 })
                for (int k = 0; k < 2; k++) W(new[] { P(sgn * (D - 9), -(D - 12) + k * 6), P(sgn * (D - 9), D - 30), P(sgn * (D - 11), D - 30), P(sgn * (D - 11), -(D - 12)) }, 1.2f);
            float lane = Mathf.Min(8f, hall.doorHalf - 3f);
            c.AddVehicle("truck", new[] { P(-lane, D + 250), P(-lane, D - 22), P(lane, D - 22), P(lane, D + 250) }, 4.5f);       // in through the open door
            c.AddVehicle("car", new[] { P(-60, D + 80), P(60, D + 80), P(60, D + 88), P(-60, D + 88) }, 6f);
            for (int k = 0; k < 4; k++) c.AddWalker(new[] { P(-30 + k * 3, D + 12), P(30, D + 16 + k), P(36, D + 60), P(-34, D + 56) });
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(-lane + 2 + k * 2, D - 10), P(-lane + 2, D + 120), P(-lane + 4 + k, D + 120), P(-lane + 4, D - 10) });
            V("tug", new[] { P(90, 82), P(-90, 82), P(-90, 74), P(90, 74) }, 3f);
            V("car", new[] { P(120, -120), P(120, 120), P(128, 120), P(128, -120) }, 4f);
            float craneY = hall.top - 7.5f, drop = Mathf.Clamp(craneY - (hall.levels * 8f + 6f) - 2f, 1.5f, 15f);
            c.AddShuttle(Crowd.CraneMesh(hall.half * 2f - 2f, drop), new Vector3(0, craneY, -hall.half * 0.45f), new Vector3(0, craneY, hall.half * 0.45f), 2.5f);
            return c;
        }

        /// <summary>
        /// Milestone 23: the clutter of a working floor: workbenches with toolboxes, stacked crates and pallets, spare tank
        /// sections in their cradles, racks of parts along the walls, an access tower and safety cones.
        /// </summary>
        static void BuildProps(Transform env, Hall hall)
        {
            // where a prop may stand: inside the walls, clear of the work towers, the rocket's square and the way to the door
            bool Fits(float x, float z, float pad)
            {
                float lim = hall.half - pad, ax = Mathf.Abs(x), az = Mathf.Abs(z);
                if (ax > lim || az > lim) return false;
                if (ax > hall.x0 - 3.5f && ax < hall.x1 + 7f && az < hall.tz + 2f) return false;
                if (ax <= hall.x0 - 3.5f && az < hall.tz + 1f) return false;
                if (ax < hall.doorHalf + 4f && z > hall.half - 34f) return false;
                return true;
            }
            var b = new MeshBuilder();
            Color wood = new Color(0.55f, 0.4f, 0.24f), wood2 = new Color(0.47f, 0.34f, 0.2f), steel = new Color(0.55f, 0.57f, 0.6f), dark = new Color(0.16f, 0.17f, 0.19f);
            Color red = new Color(0.75f, 0.15f, 0.1f), yellow = new Color(0.95f, 0.72f, 0.1f), white = new Color(0.86f, 0.87f, 0.88f), orange = new Color(0.95f, 0.45f, 0.1f), blue = new Color(0.15f, 0.35f, 0.65f);
            var rng = new System.Random(5);
            float R(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            Quaternion Y(float deg) => Quaternion.Euler(0, deg, 0);
            void Bench(float x, float z, float turn)
            {
                if (!Fits(x, z, 3f)) return;
                b.Mat = MeshBuilder.MatMetal;
                var q = Y(turn);
                Vector3 at = new Vector3(x, 0, z);
                foreach (var lx in new[] { -1.4f, 1.4f }) foreach (var lz in new[] { -0.5f, 0.5f })
                    b.AddBox(at + q * new Vector3(lx, 0.45f, lz), new Vector3(0.08f, 0.9f, 0.08f), steel, q);
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(at + q * new Vector3(0, 0.93f, 0), new Vector3(3.1f, 0.06f, 1.2f), wood2, q);
                b.AddBox(at + q * new Vector3(0, 0.25f, 0), new Vector3(2.9f, 0.04f, 1.0f), steel, q);
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(at + q * new Vector3(-0.8f, 1.1f, 0.1f), new Vector3(0.6f, 0.28f, 0.3f), red, q);
                b.AddBox(at + q * new Vector3(0.7f, 1.0f, -0.2f), new Vector3(0.4f, 0.1f, 0.3f), blue, q);
                b.AddBox(at + q * new Vector3(1.9f, 0.55f, 0), new Vector3(0.6f, 1.1f, 0.5f), red, q);   // a tool chest beside it
            }
            void Crates(float x, float z, int n)
            {
                if (!Fits(x, z, 3f)) return;
                b.Mat = MeshBuilder.MatMatte;
                for (int i = 0; i < n; i++)
                {
                    float s = R(1.1f, 1.8f);
                    int layer = i < 4 ? 0 : i < 7 ? 1 : 2;
                    Vector3 at = new Vector3(x + (i % 2) * 2.0f + R(-0.15f, 0.15f) - layer * 0.4f, s * 0.5f + layer * 1.6f, z + (i / 2 % 2) * 2.0f + R(-0.15f, 0.15f));
                    var q = Y(R(-6, 6));
                    b.AddBox(at, new Vector3(s, s * 0.9f, s), i % 3 == 0 ? wood2 : wood, q);
                    b.AddBox(at + Vector3.up * (s * 0.45f + 0.005f), new Vector3(s * 1.02f, 0.01f, 0.08f), dark, q);
                }
            }
            void Pallet(float x, float z, float turn, Color load)
            {
                if (!Fits(x, z, 3f)) return;
                b.Mat = MeshBuilder.MatMatte;
                var q = Y(turn);
                Vector3 at = new Vector3(x, 0, z);
                b.AddBox(at + Vector3.up * 0.07f, new Vector3(1.2f, 0.14f, 1.0f), wood, q);
                b.Mat = MeshBuilder.MatPaint;
                for (int i = 0; i < 4; i++) b.AddBox(at + q * new Vector3((i % 2 - 0.5f) * 0.56f, 0.4f, (i / 2 - 0.5f) * 0.48f), new Vector3(0.52f, 0.5f, 0.44f), load, q);
            }
            void TankOnCradle(float x, float z, float turn, float r, float len, Color stripe)
            {
                if (!Fits(x, z, 3f)) return;
                var q = Y(turn);
                Vector3 at = new Vector3(x, 0, z);
                b.Mat = MeshBuilder.MatPaint;
                foreach (var e in new[] { -len * 0.35f, len * 0.35f })
                {
                    b.AddBox(at + q * new Vector3(e, 0.35f, 0), new Vector3(0.5f, 0.7f, r * 1.8f), yellow, q);
                }
                int s0 = b.v.Count;
                b.AddFrustum(len * 0.5f, -len * 0.5f, r, r, white, 24, capTop: true, capBottom: true);
                b.Transform(s0, q * Quaternion.Euler(0, 0, 90), at + Vector3.up * (r + 0.55f));
                s0 = b.v.Count;
                b.AddFrustum(0.3f, -0.3f, r * 1.01f, r * 1.01f, stripe, 24);
                b.Transform(s0, q * Quaternion.Euler(0, 0, 90), at + Vector3.up * (r + 0.55f));
            }
            void Rack(Vector3 at, float turn, float length)
            {
                var q = Y(turn);
                b.Mat = MeshBuilder.MatPaint;
                for (float x = -length * 0.5f; x <= length * 0.5f + 0.01f; x += 5f)
                    foreach (var dz in new[] { -1.2f, 1.2f })
                        b.AddBox(at + q * new Vector3(x, 4.5f, dz), new Vector3(0.18f, 9f, 0.18f), blue, q);
                for (int t = 0; t < 3; t++)
                {
                    float y = 0.4f + t * 3f;
                    b.AddBox(at + q * new Vector3(0, y, -1.2f), new Vector3(length, 0.2f, 0.12f), orange, q);
                    b.AddBox(at + q * new Vector3(0, y, 1.2f), new Vector3(length, 0.2f, 0.12f), orange, q);
                    b.Mat = MeshBuilder.MatMatte;
                    b.AddBox(at + q * new Vector3(0, y + 0.12f, 0), new Vector3(length, 0.05f, 2.4f), wood2, q);
                    b.Mat = MeshBuilder.MatPaint;
                    // what's on the shelf: crates, a tank section lying down, engine bells
                    for (float x = -length * 0.5f + 1.5f; x < length * 0.5f - 1f; x += R(2.2f, 3.6f))
                    {
                        float pick = R(0, 1);
                        Vector3 p = at + q * new Vector3(x, y + 0.15f, 0);
                        if (pick < 0.45f) { b.Mat = MeshBuilder.MatMatte; float s = R(0.9f, 1.6f); b.AddBox(p + Vector3.up * s * 0.45f, new Vector3(s, s * 0.9f, s), wood, q); b.Mat = MeshBuilder.MatPaint; }
                        else if (pick < 0.75f) { int s0 = b.v.Count; b.AddFrustum(0.9f, -0.9f, 0.8f, 0.8f, white, 16, capTop: true, capBottom: true); b.Transform(s0, q * Quaternion.Euler(0, 0, 90), p + Vector3.up * 0.8f); x += 1f; }
                        else { b.Mat = MeshBuilder.MatDarkMetal; int s0 = b.v.Count; b.AddFrustum(1.2f, 0f, 0.25f, 0.65f, dark, 14, capBottom: true); b.Transform(s0, Quaternion.identity, p); b.Mat = MeshBuilder.MatPaint; }
                    }
                }
            }
            void Cone(float x, float z)
            {
                if (!Fits(x, z, 3f)) return;
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(x, 0.02f, z), new Vector3(0.4f, 0.04f, 0.4f), dark, Quaternion.identity);
                int s0 = b.v.Count;
                b.AddFrustum(0.7f, 0.04f, 0.03f, 0.16f, orange, 10);
                b.Transform(s0, Quaternion.identity, new Vector3(x, 0, z));
            }
            void Tower(float x, float z, float h)
            {
                if (!Fits(x, z, 3f) || hall.half < 80f) return;   // (a small hall has no room for them beside the rocket)
                b.Mat = MeshBuilder.MatPaint;
                foreach (var dx in new[] { -1f, 1f }) foreach (var dz in new[] { -1f, 1f })
                    b.AddBox(new Vector3(x + dx, h * 0.5f, z + dz), new Vector3(0.15f, h, 0.15f), yellow, Quaternion.identity);
                for (float y = 2f; y < h; y += 2f)
                {
                    b.AddBox(new Vector3(x, y, z - 1f), new Vector3(2f, 0.08f, 0.08f), yellow, Quaternion.identity);
                    b.AddBox(new Vector3(x, y, z + 1f), new Vector3(2f, 0.08f, 0.08f), yellow, Quaternion.identity);
                    b.AddBox(new Vector3(x - 1f, y, z), new Vector3(0.08f, 0.08f, 2f), yellow, Quaternion.identity);
                }
                b.Mat = MeshBuilder.MatMetal;
                for (float y = 4f; y < h; y += 4f) b.AddBox(new Vector3(x - 0.6f, y, z), new Vector3(2.6f, 0.1f, 2.4f), steel, Quaternion.identity);
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(x, 0.1f, z), new Vector3(2.6f, 0.2f, 2.6f), dark, Quaternion.identity);
            }

            Bench(44, 10, 90); Bench(-46, 12, 90); Bench(20, 30, 0); Bench(-18, -24, 0); Bench(26, -26, 80);
            Crates(40, 48, 9); Crates(-46, 30, 7); Crates(32, -42, 6); Crates(-34, 66, 9); Crates(70, -20, 5); Crates(-80, -40, 8);
            for (int i = 0; i < 6; i++) Pallet(55 + (i % 3) * 1.6f, 10 + (i / 3) * 1.4f, R(-5, 5), i % 2 == 0 ? blue : new Color(0.3f, 0.5f, 0.3f));
            for (int i = 0; i < 4; i++) Pallet(-58 + (i % 2) * 1.6f, -8 + (i / 2) * 1.4f, R(-5, 5), red);
            TankOnCradle(36, 24, 15, 1.25f, 5.5f, orange); TankOnCradle(-40, 27, -10, 1.25f, 4f, blue); TankOnCradle(-60, 55, 90, 1.9f, 8f, orange); TankOnCradle(64, 70, 30, 0.65f, 3f, red);
            {
                // racks along the walls: either side of the door, and down both side walls
                float rl = Mathf.Min(100f, hall.half - hall.doorHalf - 14f), sl = Mathf.Min(140f, hall.half * 1.3f);
                if (rl > 10f) foreach (int sgn in new[] { -1, 1 }) Rack(new Vector3(sgn * (hall.doorHalf + 7f + rl * 0.5f), 0, hall.half - 4f), 0, rl);
                foreach (int sgn in new[] { -1, 1 }) Rack(new Vector3(sgn * (hall.half - 4f), 0, -hall.half * 0.12f), 90, sl);
            }
            Tower(4f, -24f, 12f); Tower(-6f, 26f, 10f);   // small mobile towers in the aisles (the high bay's towers stand beside the rocket)
            foreach (var p in new[] { new Vector2(9.5f, 12), new Vector2(-9.5f, 12), new Vector2(9.5f, -12), new Vector2(-9.5f, -12), new Vector2(14, -22), new Vector2(-14, -22) }) Cone(p.x, p.y);
            for (int i = 0; i < 12; i++) Cone(-30 + i * 5f, -Mathf.Min(70f, hall.half - 8f));

            var go = new GameObject("Props");
            go.transform.SetParent(env, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("HangarProps");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        /// <summary>
        /// Milestone 20: the assembly building. A polished concrete floor with painted work-area lines, ribbed steel walls
        /// with columns and a catwalk, roof trusses with rows of lamps, and the big door behind the rocket.
        /// Big enough (340 m across, 150 m high) that the builder camera stays inside even for the tallest rockets.
        /// </summary>
        static MeshBuilder BuildHangar(Hall hall)
        {
            var b = new MeshBuilder();
            float H = hall.half, Top = hall.top, DoorHalf = hall.doorHalf, DoorTop = hall.doorTop;   // the great door behind the rocket
            float walkY = Mathf.Min(30f, Top * 0.5f);
            Color concrete = new Color(0.5f, 0.5f, 0.49f), paint = new Color(0.95f, 0.75f, 0.15f), wall = new Color(0.8f, 0.82f, 0.84f);
            Color rib = new Color(0.62f, 0.65f, 0.68f), column = new Color(0.3f, 0.32f, 0.35f), dark = new Color(0.1f, 0.11f, 0.12f), lamp = new Color(1f, 0.96f, 0.88f);
            b.Mat = new Vector4(4, 0, 0.55f, 0);                     // concrete, polished a little
            b.AddBox(new Vector3(0, -0.1f, 0), new Vector3(H * 2, 0.2f, H * 2), concrete, Quaternion.identity);
            // Painted lines: the work square round the rocket and a grid of floor joints
            b.Mat = MeshBuilder.MatMatte;
            foreach (int sgn in new[] { -1, 1 })
            {
                float sq = Mathf.Max(4f, hall.x0 - 3f);   // the work square, inside the towers
                b.AddBox(new Vector3(sgn * sq, 0.005f, 0), new Vector3(0.25f, 0.01f, sq * 2 + 0.25f), paint, Quaternion.identity);
                b.AddBox(new Vector3(0, 0.005f, sgn * sq), new Vector3(sq * 2 + 0.25f, 0.01f, 0.25f), paint, Quaternion.identity);
            }
            Color joint = new Color(0.45f, 0.45f, 0.44f);
            for (int i = -16; i <= 16; i++)
            {
                if (i == 0 || Mathf.Abs(i * 10f) >= H) continue;
                b.AddBox(new Vector3(i * 10f, 0.003f, 0), new Vector3(0.04f, 0.006f, H * 2), joint, Quaternion.identity);
                b.AddBox(new Vector3(0, 0.003f, i * 10f), new Vector3(H * 2, 0.006f, 0.04f), joint, Quaternion.identity);
            }
            // Walls: steel sheet with vertical ribs, a blue-green column every 20 m, a catwalk 30 m up. The wall behind the
            // rocket (side 0, +z) has the great door in it, open (milestone 24): DoorHalf either side of the middle, DoorTop high.
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0, 90f * side, 0);
                bool door = side == 0;
                bool Open(float x, float y) => door && Mathf.Abs(x) < DoorHalf + 1.5f && y < DoorTop;
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);   // (milestone 24: light corrugated steel, not dark metal)
                if (!door) b.AddBox(q * new Vector3(0, Top * 0.5f, H + 0.5f), new Vector3(H * 2 + 2, Top, 1f), wall, q);
                else
                {
                    float sideW = H + 1 - DoorHalf;
                    foreach (int sgn in new[] { -1, 1 })
                        b.AddBox(q * new Vector3(sgn * (DoorHalf + sideW * 0.5f), Top * 0.5f, H + 0.5f), new Vector3(sideW, Top, 1f), wall, q);
                    b.AddBox(q * new Vector3(0, (DoorTop + Top) * 0.5f, H + 0.5f), new Vector3(DoorHalf * 2, Top - DoorTop, 1f), wall, q);
                }
                b.Mat = MeshBuilder.MatPaint;
                for (float x = -H; x <= H; x += 3f)
                {
                    if (Open(x, 0f)) { b.AddBox(q * new Vector3(x, (DoorTop + Top) * 0.5f, H - 0.12f), new Vector3(0.35f, Top - DoorTop, 0.25f), rib, q); continue; }
                    b.AddBox(q * new Vector3(x, Top * 0.5f, H - 0.12f), new Vector3(0.35f, Top, 0.25f), rib, q);
                }
                b.Mat = MeshBuilder.MatPaint;
                for (float x = -H + 10f; x <= H - 10f; x += 20f)
                    if (!Open(x, 0f)) b.AddBox(q * new Vector3(x, Top * 0.5f, H - 1.2f), new Vector3(1.6f, Top, 1.6f), column, q);
                b.Mat = MeshBuilder.MatMetal;
                foreach (int sgn in door ? new[] { -1, 1 } : new[] { 0 })
                {
                    float len = door ? H - DoorHalf - 2f : H * 2, cx = door ? sgn * (DoorHalf + 2f + len * 0.5f) : 0f;
                    b.AddBox(q * new Vector3(cx, walkY, H - 3f), new Vector3(len, 0.3f, 4f), new Color(0.6f, 0.62f, 0.65f), q);
                    b.AddBox(q * new Vector3(cx, walkY + 1.1f, H - 5f), new Vector3(len, 0.08f, 0.08f), paint, q);
                }
                for (float x = -H; x <= H; x += 2.5f)
                    if (!Open(x, 0f) && !(door && Mathf.Abs(x) < DoorHalf + 2f)) b.AddBox(q * new Vector3(x, walkY + 0.55f, H - 5f), new Vector3(0.06f, 1.1f, 0.06f), new Color(0.6f, 0.62f, 0.65f), q);
            }
            // The open door: a teal frame with the lift tracks, the door leaves raised and nested under the header (they lift
            // straight up on cables), the cable drums, hazard stripes and the threshold rail across the floor
            b.Mat = MeshBuilder.MatPaint;
            foreach (int sgn in new[] { -1, 1 })
            {
                b.AddBox(new Vector3(sgn * (DoorHalf + 1.2f), DoorTop * 0.5f, H - 0.4f), new Vector3(2.4f, DoorTop, 3.2f), column, Quaternion.identity);
                b.Mat = MeshBuilder.MatMetal;
                b.AddBox(new Vector3(sgn * (DoorHalf - 0.3f), DoorTop * 0.5f, H - 2.4f), new Vector3(0.6f, DoorTop, 0.8f), new Color(0.6f, 0.62f, 0.65f), Quaternion.identity);
                b.Mat = MeshBuilder.MatPaint;
                for (int k = 0; k < 10; k++)
                    b.AddBox(new Vector3(sgn * (DoorHalf + 1.2f), 1.2f + k * 2.4f, H - 2.05f), new Vector3(2.45f, 1.2f, 0.1f), k % 2 == 0 ? paint : dark, Quaternion.identity);
            }
            b.AddBox(new Vector3(0, DoorTop + 2f, H - 0.4f), new Vector3(DoorHalf * 2 + 4.8f, 4f, 3.2f), column, Quaternion.identity);
            Color leaf = new Color(0.76f, 0.78f, 0.8f), leafRib = new Color(0.62f, 0.64f, 0.67f);
            for (int k = 0; k < 5; k++)
            {
                // each leaf is a band of the door, lifted and parked one behind another; their bottom edges show under the header
                float bottom = DoorTop - 3f - k * 1.6f, z = H - 4.2f - k * 1.1f, lh = Mathf.Min(14f, Top - 0.6f - bottom);
                b.Mat = MeshBuilder.MatDarkMetal;
                b.AddBox(new Vector3(0, bottom + lh * 0.5f, z), new Vector3(DoorHalf * 2 - 1f, lh, 0.5f), leaf, Quaternion.identity);
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0, bottom + 0.3f, z - 0.3f), new Vector3(DoorHalf * 2 - 1f, 0.6f, 0.2f), k % 2 == 0 ? paint : column, Quaternion.identity);
                for (float x = -DoorHalf + 4f; x < DoorHalf - 2f; x += 6f)
                    b.AddBox(new Vector3(x, bottom + lh * 0.5f, z - 0.3f), new Vector3(0.3f, lh, 0.15f), leafRib, Quaternion.identity);
            }
            b.Mat = MeshBuilder.MatMetal;
            foreach (int sgn in new[] { -1, 1 })
            {
                int s0 = b.v.Count;
                b.AddFrustum(1.5f, -1.5f, 1.4f, 1.4f, new Color(0.3f, 0.32f, 0.35f), 16, capTop: true, capBottom: true);
                float drumY = Mathf.Min(DoorTop + 7f, Top - 2f);
                b.Transform(s0, Quaternion.Euler(90f, 0, 0), new Vector3(sgn * (DoorHalf - 3f), drumY, H - 4f));
                b.AddBox(new Vector3(sgn * (DoorHalf - 3f), (drumY + DoorTop - 10f) * 0.5f, H - 5.4f), new Vector3(0.12f, drumY - DoorTop + 10f, 0.12f), dark, Quaternion.identity);
            }
            b.Mat = MeshBuilder.MatMatte;
            for (int k = 0; k < (int)(DoorHalf * 2 / 2f); k++)
                b.AddBox(new Vector3(-DoorHalf + 1f + k * 2f, 0.006f, H - 1.6f), new Vector3(1f, 0.012f, 1.2f), k % 2 == 0 ? paint : dark, Quaternion.identity);
            b.Mat = MeshBuilder.MatMetal;
            b.AddBox(new Vector3(0, 0.03f, H + 0.2f), new Vector3(DoorHalf * 2, 0.06f, 0.5f), new Color(0.5f, 0.52f, 0.55f), Quaternion.identity);
            // The big door behind the rocket: tall panels in a frame, with a warning stripe at the bottom
            b.Mat = MeshBuilder.MatPaint;
            float pw = DoorHalf * 0.4f, dh = Mathf.Min(100f, Top * 0.66f);
            for (int k = -2; k <= 1; k++)
                b.AddBox(new Vector3(k * pw + pw * 0.5f, dh * 0.5f, -H + 0.4f), new Vector3(pw - 0.4f, dh, 0.4f), new Color(0.78f, 0.8f, 0.82f), Quaternion.identity);
            b.AddBox(new Vector3(0, dh + 1f, -H + 0.6f), new Vector3(pw * 4f + 2f, 2f, 0.8f), column, Quaternion.identity);
            b.AddBox(new Vector3(-pw * 2f - 1f, dh * 0.5f, -H + 0.6f), new Vector3(2f, dh + 2f, 0.8f), column, Quaternion.identity);
            b.AddBox(new Vector3(pw * 2f + 1f, dh * 0.5f, -H + 0.6f), new Vector3(2f, dh + 2f, 0.8f), column, Quaternion.identity);
            for (int k = 0; k < (int)(pw * 4f / 4f); k++)
                b.AddBox(new Vector3(-pw * 2f + 2f + k * 4f, 1.2f, -H + 0.65f), new Vector3(2f, 2.4f, 0.1f), k % 2 == 0 ? paint : dark, Quaternion.identity);
            // ---- Milestone 25: a working hall inside. A glass control room along the west wall with its lights on and a
            // balcony; the emblem above the far door; air ducts along both side walls; red fire points on the columns;
            // yellow walkway lanes round the floor; stairs up to the catwalk in two corners
            {
                Color glassC = new Color(0.12f, 0.2f, 0.26f), warm = new Color(1f, 0.82f, 0.55f), steelC = new Color(0.55f, 0.57f, 0.6f);
                float roomH = 4.2f, roomY = Mathf.Max(9.6f, walkY - roomH - 1.2f), roomLen = Mathf.Min(60f, H * 0.7f), roomZ = -H * 0.2f;
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);
                b.AddBox(new Vector3(-H + 4f, roomY + roomH * 0.5f, roomZ), new Vector3(8f, roomH + 0.6f, roomLen), new Color(0.66f, 0.66f, 0.64f), Quaternion.identity);
                b.Mat = MeshBuilder.MatGlass;
                b.AddBox(new Vector3(-H + 8.05f, roomY + roomH * 0.55f, roomZ), new Vector3(0.1f, roomH * 0.62f, roomLen - 2f), glassC, Quaternion.identity);
                b.Mat = MeshBuilder.MatLamp;
                for (float z = roomZ - roomLen * 0.5f + 3f; z < roomZ + roomLen * 0.5f - 2f; z += 4f)
                    b.AddBox(new Vector3(-H + 7.9f, roomY + roomH * 0.55f, z), new Vector3(0.05f, roomH * 0.5f, 3.2f), warm, Quaternion.identity);
                b.Mat = MeshBuilder.MatMetal;
                b.AddBox(new Vector3(-H + 9.5f, roomY - 0.1f, roomZ), new Vector3(3f, 0.25f, roomLen), steelC, Quaternion.identity);       // balcony
                b.AddBox(new Vector3(-H + 10.9f, roomY + 1.0f, roomZ), new Vector3(0.08f, 0.08f, roomLen), paint, Quaternion.identity);     // its rail
                for (float z = roomZ - roomLen * 0.5f; z <= roomZ + roomLen * 0.5f; z += 2.5f)
                    b.AddBox(new Vector3(-H + 10.9f, roomY + 0.5f, z), new Vector3(0.06f, 1f, 0.06f), steelC, Quaternion.identity);
                b.Mat = MeshBuilder.MatPaint;
                for (float z = roomZ - roomLen * 0.5f + 5f; z < roomZ + roomLen * 0.5f; z += 15f)
                    b.AddBox(new Vector3(-H + 10.5f, roomY * 0.5f, z), new Vector3(0.5f, roomY, 0.5f), column, Quaternion.identity);         // posts under it

                // the emblem on the far wall above the shut door: a dark panel, a white diamond, two stripes
                float ey = Mathf.Min(Top - 12f, Mathf.Min(100f, Top * 0.66f) + 12f), es = Mathf.Clamp(Top * 0.12f, 5f, 16f);
                if (ey + es < Top - 2f)
                {
                    b.AddBox(new Vector3(0, ey, -H + 1.0f), new Vector3(es * 3.2f, es * 1.6f, 0.3f), new Color(0.22f, 0.24f, 0.28f), Quaternion.identity);
                    b.AddBox(new Vector3(-es * 0.8f, ey, -H + 1.2f), new Vector3(es * 0.9f, es * 0.9f, 0.3f), Color.white, Quaternion.Euler(0, 0, 45));
                    foreach (int k in new[] { 0, 1 })
                        b.AddBox(new Vector3(es * 0.55f, ey + es * (0.22f - k * 0.44f), -H + 1.2f), new Vector3(es * 1.5f, es * 0.18f, 0.3f), k == 0 ? new Color(0.85f, 0.65f, 0.15f) : Color.white, Quaternion.identity);
                }

                // air ducts along the two side walls, under the catwalk, with hangers
                b.Mat = MeshBuilder.MatMetal;
                foreach (int sgn in new[] { -1, 1 })
                {
                    int s0 = b.v.Count;
                    b.AddFrustum(H * 0.9f, -H * 0.9f, 0.9f, 0.9f, new Color(0.7f, 0.72f, 0.74f), 16, capTop: true, capBottom: true);
                    b.Transform(s0, Quaternion.Euler(90f, 0, 0), new Vector3(sgn * (H - 2.2f), walkY + 3.5f, 0));
                    for (float z = -H * 0.9f + 4f; z < H * 0.9f; z += 6f)
                    {
                        b.AddBox(new Vector3(sgn * (H - 2.2f), walkY + 3.5f, z), new Vector3(2.0f, 2.0f, 0.12f), new Color(0.6f, 0.62f, 0.64f), Quaternion.identity);
                        b.AddBox(new Vector3(sgn * (H - 2.2f), walkY + 4.9f, z), new Vector3(0.06f, 1.2f, 0.06f), new Color(0.4f, 0.42f, 0.45f), Quaternion.identity);
                    }
                }

                // red fire points on the columns: a hose reel box and an extinguisher, with a sign
                for (int side = 1; side < 4; side++)
                {
                    Quaternion q = Quaternion.Euler(0, 90f * side, 0);
                    for (float x = -H + 30f; x <= H - 30f; x += 40f)
                    {
                        b.Mat = MeshBuilder.MatPaint;
                        b.AddBox(q * new Vector3(x, 1.4f, H - 2.2f), new Vector3(0.8f, 0.9f, 0.3f), new Color(0.75f, 0.12f, 0.1f), q);
                        b.AddBox(q * new Vector3(x, 2.4f, H - 2.1f), new Vector3(0.5f, 0.35f, 0.05f), new Color(0.95f, 0.95f, 0.93f), q);
                        int s0 = b.v.Count;
                        b.AddFrustum(0.65f, 0.05f, 0.09f, 0.09f, new Color(0.8f, 0.1f, 0.08f), 10, capTop: true);
                        b.Transform(s0, Quaternion.identity, q * new Vector3(x + 0.7f, 0, H - 2.4f));
                    }
                }

                // walkway lanes painted 4 m in from the walls
                b.Mat = MeshBuilder.MatMatte;
                Color lane = new Color(0.9f, 0.72f, 0.15f);
                foreach (int sgn in new[] { -1, 1 })
                {
                    b.AddBox(new Vector3(sgn * (H - 4f), 0.006f, 0), new Vector3(0.18f, 0.012f, H * 2 - 8f), lane, Quaternion.identity);
                    b.AddBox(new Vector3(0, 0.006f, sgn * (H - 4f)), new Vector3(H * 2 - 8f, 0.012f, 0.18f), lane, Quaternion.identity);
                    b.AddBox(new Vector3(sgn * (H - 6.5f), 0.006f, 0), new Vector3(0.18f, 0.012f, H * 2 - 13f), lane, Quaternion.identity);
                }

                // stairs up to the catwalk in the two far corners: zig-zag flights with landings
                b.Mat = MeshBuilder.MatMetal;
                foreach (int sgn in new[] { -1, 1 })
                {
                    float x0 = sgn * (H - 7f), z0 = -H + 6f;
                    int flights = Mathf.Max(2, Mathf.CeilToInt(walkY / 4f));
                    float fh = walkY / flights;
                    for (int k = 0; k < flights; k++)
                    {
                        float y0 = k * fh, y1 = y0 + fh, dir = k % 2 == 0 ? 1f : -1f;
                        Vector3 a0 = new Vector3(x0, y0, z0 + (dir > 0 ? 0f : 7f)), a1 = new Vector3(x0, y1, z0 + (dir > 0 ? 7f : 0f));
                        Vector3 dd = a1 - a0;
                        b.AddBox((a0 + a1) * 0.5f, new Vector3(1.4f, 0.18f, dd.magnitude), steelC, Quaternion.LookRotation(dd.normalized, Vector3.up));
                        b.AddBox(new Vector3(x0, y1, dir > 0 ? z0 + 8f : z0 - 1f), new Vector3(1.6f, 0.2f, 2f), steelC, Quaternion.identity);
                        b.Mat = MeshBuilder.MatPaint;
                        b.AddBox((a0 + a1) * 0.5f + new Vector3(sgn * 0.75f, 1f, 0), new Vector3(0.06f, 0.06f, dd.magnitude), paint, Quaternion.LookRotation(dd.normalized, Vector3.up));
                        b.Mat = MeshBuilder.MatMetal;
                    }
                }
                b.Mat = MeshBuilder.MatDarkMetal;
            }

            // Roof: dark, crossed by trusses carrying rows of lamps
            b.Mat = MeshBuilder.MatDarkMetal;
            b.AddBox(new Vector3(0, Top + 0.5f, 0), new Vector3(H * 2 + 2, 1f, H * 2 + 2), new Color(0.25f, 0.26f, 0.28f), Quaternion.identity);
            for (float z = -H + 20f; z <= H - 20f; z += 40f)
            {
                b.AddBox(new Vector3(0, Top - 3f, z), new Vector3(H * 2, 0.6f, 0.6f), new Color(0.4f, 0.42f, 0.45f), Quaternion.identity);
                b.AddBox(new Vector3(0, Top - 7f, z), new Vector3(H * 2, 0.6f, 0.6f), new Color(0.4f, 0.42f, 0.45f), Quaternion.identity);
                for (float x = -H; x <= H; x += 8f)
                    b.AddBox(new Vector3(x, Top - 5f, z), new Vector3(0.3f, 4f, 0.3f), new Color(0.4f, 0.42f, 0.45f), Quaternion.Euler(0, 0, 40f));
                b.Mat = MeshBuilder.MatLamp;
                for (float x = -H + 20f; x <= H - 20f; x += 20f)
                    b.AddBox(new Vector3(x, Top - 7.6f, z), new Vector3(3f, 0.25f, 1.2f), lamp, Quaternion.identity);
                b.Mat = MeshBuilder.MatDarkMetal;
            }
            return b;
        }

        /// <summary>
        /// Milestone 24 (captaincarl24: "make it look like a rocket hangar ... the vertical doors open so you can see the outside"):
        /// what you see through the open door. The space centre as it lies south of the assembly building: the apron outside
        /// the door, the two-lane rocket road out to the pad with its red service tower, floodlights and propellant spheres,
        /// mission control and the sheds to one side, headquarters to the other, lamps and trees, then grass down to the beach
        /// and the sea, under a sky. (x is east and +z is south here, the pad 467 m from the door, like on the real map.)
        /// </summary>
        static Material skyMat;

        static void BuildOutside(Transform env, Hall hall)
        {
            var b = new MeshBuilder();
            var rng = new System.Random(77);
            Color lawn = new Color(0.82f, 0.86f, 0.76f), concrete = new Color(0.62f, 0.62f, 0.6f), kerb = new Color(0.42f, 0.42f, 0.41f), pave = new Color(0.64f, 0.64f, 0.63f);
            Color sand = new Color(0.66f, 0.61f, 0.48f), sea = new Color(0.03f, 0.12f, 0.2f), white = new Color(0.86f, 0.87f, 0.88f), teal = new Color(0.3f, 0.31f, 0.33f);
            Color red = new Color(0.55f, 0.1f, 0.07f), steel = new Color(0.72f, 0.73f, 0.76f), dark = new Color(0.28f, 0.29f, 0.31f), yellow = new Color(0.9f, 0.7f, 0.12f), glass = new Color(0.1f, 0.16f, 0.22f);
            Color trunk = new Color(0.32f, 0.22f, 0.14f), leaf = new Color(0.12f, 0.24f, 0.09f), leaf2 = new Color(0.17f, 0.3f, 0.11f);
            const float Pad = 467f, DoorZ = 170f, Ground = 0.0f;
            Vector3 U = Vector3.up;
            Vector3 P(float x, float z, float y = 0) => new Vector3(x, y, z);
            void Flat(float x0, float z0, float x1, float z1, float y, Color c) => b.AddQuad(P(x0, z0, y), P(x1, z0, y), P(x1, z1, y), P(x0, z1, y), U, c);
            void Disc(float x, float z, float r, float y, Color c)
            {
                const int seg = 56;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                    b.AddTri(P(x, z, y), P(x + Mathf.Cos(a0) * r, z + Mathf.Sin(a0) * r, y), P(x + Mathf.Cos(a1) * r, z + Mathf.Sin(a1) * r, y), U, c);
                }
            }
            void Ring(float x, float z, float r0, float r1, float y, Color c)
            {
                const int seg = 56;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                    b.AddQuad(P(x + Mathf.Cos(a0) * r0, z + Mathf.Sin(a0) * r0, y), P(x + Mathf.Cos(a0) * r1, z + Mathf.Sin(a0) * r1, y),
                              P(x + Mathf.Cos(a1) * r1, z + Mathf.Sin(a1) * r1, y), P(x + Mathf.Cos(a1) * r0, z + Mathf.Sin(a1) * r0, y), U, c);
                }
            }
            void Box(float x, float z, float y, Vector3 size, Color c) => b.AddBox(P(x, z, y), size, c, Quaternion.identity);
            void Tree(float x, float z)
            {
                BodyView.Conifer(b, P(x, z), 7f + (float)rng.NextDouble() * 10f, rng);
            }
            void Lamp(float x, float z)
            {
                b.Mat = MeshBuilder.MatDarkMetal;
                Box(x, z, 4f, new Vector3(0.18f, 8f, 0.18f), dark);
                Box(x, z, 8.05f, new Vector3(1.0f, 0.14f, 0.35f), dark);
                b.Mat = MeshBuilder.MatLamp;
                Box(x, z, 7.95f, new Vector3(0.8f, 0.06f, 0.26f), new Color(1f, 0.93f, 0.78f));
            }

            // ---- The ground: lawn from the door to the dunes, the beach, and the sea beyond and to both sides
            b.Mat = MeshBuilder.MatLawn;
            for (float x = -2400; x < 2400; x += 200)
                for (float z = DoorZ; z < 1560; z += 200)                       // (from the door out: the hall has its own floor)
                    Flat(x, z, x + 200, Mathf.Min(z + 200, 1560), Ground, lawn);
            b.Mat = MeshBuilder.MatMatte;
            for (float x = -2400; x < 2400; x += 200)
            {
                b.AddQuad(P(x, 1560, Ground), P(x + 200, 1560, Ground), P(x + 200, 1640, Ground), P(x, 1640, Ground), U, Color.Lerp(sand, lawn * 0.4f, 0.35f));
                Flat(x, 1640, x + 200, 1800, Ground, sand);
                b.AddQuad(P(x, 1800, Ground), P(x + 200, 1800, Ground), P(x + 200, 1830, Ground - 0.25f), P(x, 1830, Ground - 0.25f), U, sand * 0.8f);
            }
            b.Mat = MeshBuilder.MatGlass;
            foreach (var (x0, z0, x1, z1) in new[] { (-12000f, 1820f, 12000f, 12000f), (-12000f, -12000f, -2400f, 1820f), (2400f, -12000f, 12000f, 1820f) })
                for (float x = x0; x < x1; x += 2000)
                    for (float z = z0; z < z1; z += 2000)
                        Flat(x, z, Mathf.Min(x + 2000, x1), Mathf.Min(z + 2000, z1), Ground - 0.3f, sea);
            b.Mat = MeshBuilder.MatMatte;
            foreach (int sgn in new[] { -1, 1 })   // beaches down the two sides
                for (float z = DoorZ; z < 1830; z += 200)
                    b.AddQuad(P(sgn * 2400, z, Ground + 0.01f), P(sgn * 2400, z + 200, Ground + 0.01f), P(sgn * 2560, z + 200, Ground - 0.25f), P(sgn * 2560, z, Ground - 0.25f), U, sand);

            // ---- The apron outside the door, the rocket road (two lanes, grass between) and the pad's round apron
            b.Mat = MeshBuilder.MatConcrete;
            Flat(-121, DoorZ, 121, 302, 0.04f, kerb);
            b.Mat = MeshBuilder.MatPaving;
            Flat(-120, DoorZ, 120, 300, 0.07f, pave);
            b.Mat = MeshBuilder.MatMatte;
            for (int k = 0; k < 20; k++) Box(-38f + k * 4f, DoorZ + 1.5f, 0.08f, new Vector3(2f, 0.02f, 1.2f), k % 2 == 0 ? yellow : dark);
            foreach (int lane in new[] { -1, 1 })
            {
                b.Mat = MeshBuilder.MatConcrete;
                Flat(lane * 9 - 6.7f, 299, lane * 9 + 6.7f, Pad - 92, 0.06f, kerb);
                b.Mat = MeshBuilder.MatPaving;
                Flat(lane * 9 - 5.5f, 299, lane * 9 + 5.5f, Pad - 92, 0.1f, pave);
            }
            b.Mat = MeshBuilder.MatConcrete;
            Disc(0, Pad, 97.2f, 0.05f, kerb);
            b.Mat = MeshBuilder.MatPaving;
            Disc(0, Pad, 96f, 0.09f, pave);
            b.Mat = MeshBuilder.MatConcrete;
            Disc(0, Pad, 34f, 0.11f, new Color(0.5f, 0.5f, 0.49f));
            b.Mat = MeshBuilder.MatMatte;
            Ring(0, Pad, 52f, 53.2f, 0.12f, yellow);
            for (float z = 310; z < Pad - 95; z += 26) foreach (int sgn in new[] { -1, 1 }) Lamp(sgn * 17f, z);

            // ---- The pad: the deck with its trench, the red service tower with walkways and swing arms, floodlights
            {
                Vector3 c = P(0, Pad);
                b.Mat = MeshBuilder.MatConcrete;
                b.AddBox(c + U * 1f, new Vector3(24f, 2f, 24f), concrete, Quaternion.identity);
                b.Mat = MeshBuilder.MatDarkMetal;
                b.AddBox(c + U * 2.03f + Vector3.forward * -6.5f, new Vector3(3.6f, 0.03f, 10f), new Color(0.12f, 0.12f, 0.12f), Quaternion.identity);
                b.Mat = MeshBuilder.MatPaint;
                const float tw = 2.4f, th = 34f;
                Vector3 tb = c + U * 2f + Vector3.right * -7f;
                for (int cx = -1; cx <= 1; cx += 2) for (int cz = -1; cz <= 1; cz += 2)
                    b.AddBox(tb + new Vector3(cx * tw * 0.5f, th * 0.5f, cz * tw * 0.5f), new Vector3(0.28f, th, 0.28f), red, Quaternion.identity);
                float diag = Mathf.Sqrt(tw * tw + 2.8f * 2.8f), ang = Mathf.Atan2(2.8f, tw) * Mathf.Rad2Deg;
                for (float y = 0; y <= th; y += 2.8f)
                    foreach (int sgn in new[] { -1, 1 })
                    {
                        b.AddBox(tb + new Vector3(0, y, sgn * tw * 0.5f), new Vector3(tw, 0.16f, 0.16f), red, Quaternion.identity);
                        b.AddBox(tb + new Vector3(sgn * tw * 0.5f, y, 0), new Vector3(0.16f, 0.16f, tw), red, Quaternion.identity);
                        if (y + 2.8f <= th + 0.01f)
                        {
                            b.AddBox(tb + new Vector3(0, y + 1.4f, sgn * tw * 0.5f), new Vector3(diag, 0.1f, 0.1f), red, Quaternion.Euler(0, 0, sgn * ang));
                            b.AddBox(tb + new Vector3(sgn * tw * 0.5f, y + 1.4f, 0), new Vector3(0.1f, 0.1f, diag), red, Quaternion.Euler(sgn * ang, 0, 0));
                        }
                    }
                b.Mat = MeshBuilder.MatMetal;
                for (int i = 0; i < 6; i++) b.AddBox(tb + U * (3f + i * 5.5f), new Vector3(2.9f, 0.18f, 2.9f), steel, Quaternion.identity);
                b.AddBox(tb + U * (th + 4.5f), new Vector3(0.12f, 9f, 0.12f), steel, Quaternion.identity);
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(tb + U * 24f + Vector3.right * 2.4f, new Vector3(4.6f, 0.5f, 1.0f), white, Quaternion.identity);
                b.AddBox(tb + U * 14f + Vector3.right * 1.8f, new Vector3(3.4f, 0.4f, 0.8f), white, Quaternion.identity);
                foreach (var cc in new[] { new Vector2(16, 16), new Vector2(16, -16), new Vector2(-16, 16), new Vector2(-16, -16) })
                {
                    Vector3 m = c + new Vector3(cc.x, 0, cc.y), toward = new Vector3(-cc.x, 0, -cc.y).normalized;
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddBox(m + U * 11f, new Vector3(0.35f, 22f, 0.35f), steel, Quaternion.identity);
                    b.AddBox(m + U * 22.2f, new Vector3(2.4f, 0.8f, 0.5f), dark, Quaternion.LookRotation(toward, U));
                    b.Mat = MeshBuilder.MatLamp;
                    b.AddBox(m + U * 22.2f + toward * 0.27f, new Vector3(2.1f, 0.55f, 0.05f), new Color(1f, 0.97f, 0.9f), Quaternion.LookRotation(toward, U));
                }
                // the propellant spheres and the water tower
                b.Mat = MeshBuilder.MatGloss;
                foreach (float x in new[] { 60f, 76f })
                {
                    b.AddSphere(P(x, Pad - 42, 9f), 6f, white, 28, 16);
                    b.Mat = MeshBuilder.MatMetal;
                    for (int k = 0; k < 6; k++) { Quaternion q = Quaternion.Euler(0, 60 * k, 0); b.AddBox(P(x, Pad - 42) + q * new Vector3(5f, 3.5f, 0), new Vector3(0.4f, 7f, 0.4f), steel, q); }
                    b.Mat = MeshBuilder.MatGloss;
                }
                b.AddSphere(P(36, Pad + 34, 26f), 4.5f, white, 24, 12);
                b.Mat = MeshBuilder.MatMetal;
                for (int k = 0; k < 4; k++) { Quaternion q = Quaternion.Euler(0, 45 + 90 * k, 0); b.AddBox(P(36, Pad + 34, 12f) + q * new Vector3(2.6f, 0, 0), new Vector3(0.45f, 24f, 0.45f), steel, q); }
            }

            // ---- Buildings round about: mission control and the sheds to the east, headquarters to the west, a fuel depot
            // and the maintenance shed by the apron, a few offices further off
            void Building(float x, float z, float w, float d, float h, float style, Color trim)
            {
                b.Mat = MeshBuilder.MatFacade(style);
                Box(x, z, h * 0.5f, new Vector3(w, h, d), new[] { new Color(0.8f, 0.74f, 0.64f), new Color(0.68f, 0.67f, 0.64f), new Color(0.62f, 0.4f, 0.32f), new Color(0.76f, 0.68f, 0.55f) }[Mathf.Abs((int)(x * 7 + z * 3)) % 4]);
                b.Mat = MeshBuilder.MatPaint;
                Box(x, z, h + 0.35f, new Vector3(w + 0.7f, 0.7f, d + 0.7f), trim);
            }
            b.Mat = MeshBuilder.MatPaving;
            Flat(130, 295, 210, 360, 0.07f, pave);
            Building(170, 327, 36, 22, 14, MeshBuilder.Punched, teal);
            Building(-181, 204, 62, 30, 20, MeshBuilder.Ribbon, teal);
            b.Mat = MeshBuilder.MatGlass;
            {
                int s0 = b.v.Count;
                b.AddFrustum(20f, 0f, 17f, 17f, glass, 28, capTop: true);
                b.Transform(s0, Quaternion.identity, P(-150, 204));
            }
            Building(-48, 307, 36, 18, 8, MeshBuilder.Cladding, teal);
            for (int i = 0; i < 3; i++) { b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding); b.AddBox(P(70 + i * 16, Pad + 70, 6f), new Vector3(10f, 12f, 10f), new Color(0.82f, 0.83f, 0.84f), Quaternion.Euler(0, 45, 0)); }
            Building(330, 380, 40, 26, 9, MeshBuilder.Cladding, teal);
            Building(-330, 520, 54, 22, 9, MeshBuilder.Punched, teal);
            Building(420, 620, 60, 30, 14, MeshBuilder.Ribbon, teal);
            Building(-420, 260, 70, 40, 12, MeshBuilder.Cladding, teal);
            b.Mat = MeshBuilder.MatGloss;
            for (int k = 0; k < 3; k++)
            {
                int s0 = b.v.Count;
                b.AddFrustum(14f, -14f, 2.6f, 2.6f, white, 20, capTop: true, capBottom: true);
                b.Transform(s0, Quaternion.Euler(90f, 0, 0) * Quaternion.Euler(0, 0, 0), P(96, 299 - k * 7, 3.7f));
            }

            // ---- Trees: an avenue along the apron, clumps out in the grass, palms near the beach
            b.Mat = MeshBuilder.MatMatte;
            for (float x = -220; x <= 220; x += 15) if (Mathf.Abs(x) > 130) { Tree(x, 190); Tree(x, 208); }
            for (int k = 0; k < 420; k++)
            {
                float x = -2300 + (float)rng.NextDouble() * 4600, z = 330 + (float)rng.NextDouble() * 1200;
                if (Mathf.Abs(x) < 140 && z < Pad + 130) continue;                  // the rocket road and the pad
                if (Mathf.PerlinNoise(x * 0.004f + 3f, z * 0.004f + 9f) < 0.52f) continue;
                if (x > 100 && x < 460 && z < 420) continue;
                Tree(x, z);
            }

            var go = new GameObject("Outside");
            go.transform.SetParent(env, false);
            go.transform.localPosition = new Vector3(0, 0, hall.half - DoorZ);   // (drawn for the door at z 170; the door is where the hall's wall is)
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("HangarOutside");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;

            // ---- The sky: a dome with a pale horizon and a deeper blue overhead, brightest toward the sun
            var sb = new MeshBuilder();
            const int segs = 48, rings = 16;
            const float R = 11500f;
            Vector3 sunDir = -GameRoot.SunDirection;
            Color horizon = new Color(0.72f, 0.82f, 0.92f), zenith = new Color(0.26f, 0.48f, 0.82f), glow = new Color(1f, 0.96f, 0.86f);
            Color SkyAt(Vector3 d)
            {
                float up = Mathf.Clamp01(d.y);
                Color c = Color.Lerp(horizon, zenith, Mathf.Pow(up, 0.55f));
                float toSun = Mathf.Max(0f, Vector3.Dot(d, sunDir));
                return Color.Lerp(c, glow, Mathf.Pow(toSun, 8f) * 0.6f);
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < segs; i++)
                {
                    float t0 = -0.08f + (j / (float)rings) * 1.6f, t1 = -0.08f + ((j + 1) / (float)rings) * 1.6f;
                    float a0 = i * Mathf.PI * 2 / segs, a1 = (i + 1) * Mathf.PI * 2 / segs;
                    Vector3 D(float t, float a) => new Vector3(Mathf.Cos(t) * Mathf.Cos(a), Mathf.Sin(t), Mathf.Cos(t) * Mathf.Sin(a));
                    Vector3 d00 = D(t0, a0), d10 = D(t0, a1), d11 = D(t1, a1), d01 = D(t1, a0);
                    int s0 = sb.v.Count;
                    foreach (var d in new[] { d00, d10, d11, d01 }) { sb.v.Add(d * R); sb.n.Add(-d); sb.c.Add(SkyAt(d)); }
                    sb.t.Add(s0); sb.t.Add(s0 + 2); sb.t.Add(s0 + 1);
                    sb.t.Add(s0); sb.t.Add(s0 + 3); sb.t.Add(s0 + 2);
                }
            if (skyMat == null)
            {
                skyMat = new Material(Mats.Unlit) { name = "HangarSky", renderQueue = 1000 };
                skyMat.SetFloat("_SrcBlend", (float)BlendMode.One);
                skyMat.SetFloat("_DstBlend", (float)BlendMode.Zero);
            }
            var sky = new GameObject("Sky");
            sky.transform.SetParent(env, false);
            sky.AddComponent<MeshFilter>().sharedMesh = sb.ToMesh("HangarSky");
            var smr = sky.AddComponent<MeshRenderer>();
            smr.sharedMaterial = skyMat;
            smr.shadowCastingMode = ShadowCastingMode.Off;
            smr.receiveShadows = false;
        }

        static void Bounds(IList<PartNode> nodes, out float lo, out float hi)
        {
            lo = 0; hi = 0;
            bool any = false;
            foreach (var n in nodes)
            {
                var d = PartCatalog.Get(n.id);
                if (d == null) continue;
                Vector3 c = DesignOps.Center(n, d);
                float h = d.Height * 0.5f;
                float a = Mathf.Min(c.y - h, n.pos.y, d.radial ? c.y : DesignOps.BottomNode(n, d).y);
                float b2 = Mathf.Max(c.y + h, n.pos.y);
                lo = any ? Mathf.Min(lo, a) : a;
                hi = any ? Mathf.Max(hi, b2) : b2;
                any = true;
            }
        }

        void Rebuild()
        {
            view?.Destroy();
            view = VesselView.FromNodes(design.nodes, "Design " + design.name, true);
            if (planeView)
            {
                // Lying down: sit the lowest point (usually the wheels) on the floor, centred over the origin.
                view.root.transform.SetPositionAndRotation(Vector3.zero, ViewRot);
                var b = WorldBounds(view.root);
                offset = new Vector3(-b.center.x, -b.min.y + 0.05f, -b.center.z);
            }
            else
            {
                Bounds(design.nodes, out float lo, out _);
                offset = new Vector3(0, -lo + 0.05f, 0);
            }
            view.root.transform.SetPositionAndRotation(offset, ViewRot);
            viewBounds = WorldBounds(view.root);
            // the hall fits the rocket (or the plane lying down)
            float fitH = planeView ? Mathf.Max(viewBounds.size.x, viewBounds.size.z) * 0.9f : viewBounds.size.y;
            float fitR = planeView ? 1f : Mathf.Max(viewBounds.extents.x, viewBounds.extents.z);
            var want = HallFor(fitH, fitR);
            if (env == null || !want.Same(hall)) { hall = want; BuildEnv(); }
            if (selected >= design.nodes.Count) selected = -1;
            if (selected >= 0) foreach (int g in DesignOps.SymmetryGroup(design, selected)) view.Tint(g, new Color(1f, 0.88f, 0.5f));
            validation = design.Validate();
            stageList = design.Stages();
            reports = Staging.AnalyzeDesign(design, root.home.SurfaceGravity);
            warnings = validation == null ? design.Warnings(root.home.SurfaceGravity) : new List<string>();
            string crewNote = root.universe.career.CrewNote(design);
            if (crewNote != null) warnings.Add(crewNote);
            subs = SubStore.List();
        }

        static UnityEngine.Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new UnityEngine.Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        void PushUndo()
        {
            undo.Add(JsonUtility.ToJson(design));
            if (undo.Count > 60) undo.RemoveAt(0);
        }

        void Undo()
        {
            if (undo.Count == 0) { Toast("Nothing to undo"); return; }
            design = JsonUtility.FromJson<VesselDesign>(undo[undo.Count - 1]);
            design.Upgrade();
            undo.RemoveAt(undo.Count - 1);
            selected = -1;
            Rebuild();
        }

        void Toast(string s) { toast = s; toastUntil = Time.time + 3f; }

        public void Notice(string s) => Toast(s);
        public void ShowVessels(bool on) => showVessels = on;
        public void ShowMissions(bool on) { showMissions = on; if (on) showTech = showCenter = false; }
        bool showStory;
        public void ShowStoryScreen(bool on) { showStory = on; }

        /// <summary>The story so far (milestone 28): every mission by chapter, done ones ticked, and the current briefing. False to close.</summary>
        bool DrawStoryScreen(Rect r)
        {
            Ui.Box(r);
            Ui.Box(r);
            Ui.Box(r);
            var c = root.universe.career;
            Story.Update(root.universe);
            GUI.Label(new Rect(r.x + 16, r.y + 10, 400, 26), "The story", Ui.Header);
            GUI.Label(new Rect(r.x + 16, r.y + 36, r.width - 140, 20), Story.Finished(c) ? "All done! The rest of space is yours." : $"Mission {c.storyStep + 1} of {Story.Missions.Length}. Mission control briefs each one when you fly.", Ui.Small);
            if (GUI.Button(new Rect(r.xMax - 96, r.y + 10, 80, 28), "Close", Ui.ButtonSmall)) return false;
            float colW = (r.width - 48) / 2, top = r.y + 66;
            float[] y = { top, top };
            for (int ch = 0; ch < Story.Chapters.Length; ch++)
            {
                int col = ch < 3 ? 0 : 1;
                float x = r.x + 16 + col * (colW + 16);
                GUI.Label(new Rect(x, y[col], colW, 24), $"Chapter {ch + 1}: {Story.Chapters[ch]}", Ui.Bold);
                y[col] += 26;
                for (int i = 0; i < Story.Missions.Length; i++)
                {
                    var m = Story.Missions[i];
                    if (m.chapter != ch) continue;
                    bool done = i < c.storyStep, now = i == c.storyStep;
                    string mark = done ? "<color=#7dff8a>✓</color>" : now ? "<color=#ffd56a>▶</color>" : "<color=#8a929c>○</color>";
                    string text = done ? $"<color=#c8d4e0>{m.title}</color>" : now ? $"<b>{m.title}</b>: {m.goal}" : $"<color=#a4acb6>{m.title}</color>";
                    GUI.Label(new Rect(x + 6, y[col], colW - 6, 22), $"{mark}  {text}", Ui.Small);
                    y[col] += 22;
                }
                y[col] += 8;
            }
            var cur = Story.Current(c);
            if (cur != null)
            {
                var br = new Rect(r.x + 16, r.yMax - 112, r.width - 32, 96);
                Ui.Box(br);
                GUI.Label(new Rect(br.x + 12, br.y + 8, br.width - 24, 20), $"<color=#9fc6e8>{cur.who}</color>", Ui.Small);
                GUI.Label(new Rect(br.x + 12, br.y + 28, br.width - 24, 44), Words.Say(cur.brief), new GUIStyle(Ui.Label) { wordWrap = true });
                GUI.Label(new Rect(br.x + 12, br.y + 70, br.width - 24, 20), $"<color=#ffd56a>Goal:</color> <b>{cur.goal}</b>" + (c.IsCareer ? $"   ·   pays {cur.funds:N0} funds, {cur.science:0} science" : ""), Ui.Small);
            }
            return true;
        }
        public void ShowTech(bool on) { showTech = on; if (on) showMissions = showCenter = false; }
        public void ShowCenter(bool on) { showCenter = on; if (on) showMissions = showTech = false; }
        public void ShowCrew(bool on) { showCrew = on; if (on) showPresets = false; Rebuild(); }
        /// <summary>From the space centre view (milestone 25): build a plane or a rocket, or pick the pad to launch from.</summary>
        public void PlaneView(bool on) { if (on != planeView) SetView(on); if (on && design.IsPlane) runwayLaunch = true; }
        public void SetLaunchPad(int pad)
        {
            runwayLaunch = false;
            launchPad = Mathf.Clamp(pad, 0, LaunchSite.Pads.Length - 1);
            try { PlayerPrefs.SetInt("launchPad", launchPad); } catch { }
            Toast($"Launching from {LaunchSite.Pads[launchPad].name.ToLowerInvariant()}");
        }

        // ---------------------------------------------------------------- vessels in flight

        bool showVessels, confirmNewGame;
        Vector2 vesselScroll;

        void DrawVessels(Rect r)
        {
            var u = root.universe;
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 12, r.y + 6, 240, 22), "Vessels in flight", Ui.Header);
            if (GUI.Button(new Rect(r.xMax - 34, r.y + 6, 24, 22), "x", Ui.ButtonSmall)) showVessels = false;
            GUI.Label(new Rect(r.x + 12, r.y + 28, r.width - 24, 20), $"Universe time {Ui.Time(u.ut)}", Ui.Small);
            var list = u.vessels.Where(v => !v.destroyed).OrderBy(v => v.isDebris).ThenBy(v => v.name).ToList();
            var view = new Rect(r.x + 6, r.y + 52, r.width - 12, r.height - 96);
            vesselScroll = GUI.BeginScrollView(view, vesselScroll, new Rect(0, 0, view.width - 18, Math.Max(view.height - 4, list.Count * 50)));
            float y = 0;
            if (list.Count == 0) GUI.Label(new Rect(6, 4, view.width - 30, 60), "Nothing out there yet. Vessels you leave in orbit (or landed somewhere) stay in the universe and show up here.", Ui.Small);
            foreach (var v in list)
            {
                GUI.Label(new Rect(6, y + 2, 230, 20), $"<b>{v.name}</b>{(v.isDebris ? "  <size=11>(debris)</size>" : "")}", Ui.Label);
                GUI.Label(new Rect(6, y + 22, 240, 20), v.Situation(u.ut), Ui.Small);
                bool home = v.landed && v.body == root.home;
                if (GUI.Button(new Rect(view.width - 166, y + 6, 76, 28), home ? "Recover" : "Delete", Ui.ButtonSmall))
                {
                    if (!home) u.career.OnVesselLost(v, false);
                    string msg = home ? u.career.Recover(v, u.ut, root.bodies) : $"{v.name} deleted";
                    u.Remove(v);
                    u.Save();
                    Toast(msg);
                    foreach (var n in u.career.news) newsQueue.Enqueue(n);
                    u.career.news.Clear();
                    GUI.EndScrollView();
                    return;
                }
                GUI.enabled = v.IsControllable;
                if (GUI.Button(new Rect(view.width - 84, y + 6, 62, 28), "Fly", Ui.Button)) { GUI.enabled = true; GUI.EndScrollView(); root.Fly(v); return; }
                GUI.enabled = true;
                y += 50;
            }
            GUI.EndScrollView();
            if (!confirmNewGame)
            {
                if (GUI.Button(new Rect(r.x + 12, r.yMax - 36, 150, 26), "Main menu", Ui.ButtonSmall)) { root.ShowMenu(); return; }
                if (list.Any(v => v.isDebris) && GUI.Button(new Rect(r.x + 170, r.yMax - 36, 150, 26), "Clear all debris", Ui.ButtonSmall))
                {
                    u.vessels.RemoveAll(v => v.isDebris);
                    u.Save();
                }
            }
            else
            {
                GUI.Label(new Rect(r.x + 12, r.yMax - 34, 170, 22), "<color=#ff9070>Delete every vessel?</color>", Ui.Label);
                if (GUI.Button(new Rect(r.x + 186, r.yMax - 36, 70, 26), "Yes", Ui.ButtonSmall)) { u.Reset(GameMode.Sandbox); u.career.chosen = false; confirmNewGame = false; showVessels = false; }
                if (GUI.Button(new Rect(r.x + 262, r.yMax - 36, 70, 26), "No", Ui.ButtonSmall)) confirmNewGame = false;
            }
        }

        // ---------------------------------------------------------------- held parts

        static List<PartNode> Normalize(List<PartNode> sub)
        {
            Quaternion q = Quaternion.Inverse(sub[0].rot);
            Vector3 o = sub[0].pos;
            foreach (var n in sub) { n.pos = q * (n.pos - o); n.rot = q * n.rot; }
            return sub;
        }

        void Hold(List<PartNode> sub)
        {
            held = Normalize(sub);
            heldExtra = Quaternion.identity;
            tool = Tool.Place;
            menuPart = -1;
            ClearGhosts();
        }

        void ClearGhosts()
        {
            foreach (var g in ghosts) g.Destroy();
            ghosts.Clear();
        }

        void DiscardHeld()
        {
            held = null;
            ClearGhosts();
        }

        void PickUp(int i)
        {
            PushUndo();
            if (i == 0)
            {
                var all = design.nodes.Select(n => n.Copy()).ToList();
                design.nodes.Clear();
                Hold(all);
            }
            else
            {
                var group = DesignOps.SymmetryGroup(design, i);
                var sub = DesignOps.Extract(design, i);
                var kill = new HashSet<int>();
                foreach (int g in group) foreach (int k in DesignOps.Subtree(design, g)) kill.Add(k);
                DesignOps.Remove(design, kill);
                int idx = Array.IndexOf(SymSteps, group.Count);
                if (group.Count > 1 && idx >= 0) symIndex = idx;
                Hold(sub);
            }
            design.manualStaging = false;
            selected = -1;
            Rebuild();
        }

        void Place()
        {
            PushUndo();
            bool surface = placeAttach == Attach.Surface;
            DesignOps.InsertSymmetric(design, held, placeParent, placeAttach, placePos, placeRot, surface ? Symmetry : 1, surface && mirror);
            if (design.manualStaging) { design.manualStaging = false; Toast("Staging reset to automatic"); }
            DesignOps.ApplyDefaultGroups(design);
            DiscardHeld();
            Rebuild();
        }

        void ComputePlacement()
        {
            var hdef = PartCatalog.Get(held[0].id);
            Vector2 mouse = In.MousePosition;
            Ray ray = Cam.ScreenPointToRay(mouse);
            placeValid = false;
            placeParent = -1;
            if (design.nodes.Count == 0)
            {
                placeValid = true;
                placeAttach = Attach.Root;
                placePos = Vector3.zero;
                placeRot = Quaternion.identity;
                return;
            }
            // 1) Snap to a free stack node near the mouse.
            float best = 30f * Ui.Scale;
            for (int i = 0; i < design.nodes.Count; i++)
            {
                var n = design.nodes[i];
                var d = PartCatalog.Get(n.id);
                if (d == null) continue;
                if (!hdef.radial && hdef.stackBottom && DesignOps.TopFree(design, i))
                {
                    Vector3 sp = Cam.WorldToScreenPoint(ToWorld(n.pos));
                    float dd = sp.z > 0 ? ((Vector2)sp - mouse).magnitude : 1e9f;
                    if (dd < best) { best = dd; placeValid = true; placeParent = i; placeAttach = Attach.Above; placeRot = n.rot; placePos = n.pos + DesignOps.Axis(n) * hdef.length; }
                }
                if (!hdef.radial && hdef.stackTop && DesignOps.BottomFree(design, i))
                {
                    Vector3 node = DesignOps.BottomNode(n, d);
                    Vector3 sp = Cam.WorldToScreenPoint(ToWorld(node));
                    float dd = sp.z > 0 ? ((Vector2)sp - mouse).magnitude : 1e9f;
                    if (dd < best) { best = dd; placeValid = true; placeParent = i; placeAttach = Attach.Below; placeRot = n.rot; placePos = node; }
                }
            }
            if (placeValid) return;
            // 2) Stick to the surface under the mouse.
            Physics.SyncTransforms();
            if (hdef.surfaceAttach && Physics.Raycast(ray, out var hit, 3000f) && view.colliderToIndex.TryGetValue(hit.collider, out int pi))
            {
                var pn = design.nodes[pi];
                var pd = PartCatalog.Get(pn.id);
                Vector3 p = ToDesign(hit.point), nrm = Quaternion.Inverse(ViewRot) * hit.normal;
                if (snap && !pd.radial)
                {
                    Vector3 local = Quaternion.Inverse(pn.rot) * (p - pn.pos);
                    float step = 360f / Mathf.Max(8, Symmetry * 4);
                    float ang = Mathf.Round(Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg / step) * step * Mathf.Deg2Rad;
                    float frac = Mathf.Clamp01(-local.y / Mathf.Max(0.01f, pd.length));
                    float r = Mathf.Lerp(pd.topDiameter, pd.bottomDiameter, frac) * 0.5f;
                    p = pn.pos + pn.rot * new Vector3(Mathf.Cos(ang) * r, local.y, Mathf.Sin(ang) * r);
                    nrm = pn.rot * new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                }
                DesignOps.SurfacePose(hdef, p, nrm, DesignOps.Axis(pn), heldExtra, out placePos, out placeRot);
                placeParent = pi;
                placeAttach = Attach.Surface;
                placeValid = true;
                return;
            }
            // 3) Floating: not attachable here.
            placePos = ToDesign(ray.GetPoint(Mathf.Max(6f, dist * 0.6f)));
            placeRot = heldExtra;
        }

        void UpdateGhosts()
        {
            var poses = placeValid && placeAttach == Attach.Surface && placeParent >= 0
                ? DesignOps.SymmetricPoses(design.nodes[placeParent], placePos, placeRot, PartCatalog.Get(design.nodes[placeParent].id).radial ? 1 : Symmetry, mirror)
                : new List<(Vector3, Quaternion)> { (placePos, placeRot) };
            while (ghosts.Count < poses.Count) ghosts.Add(VesselView.FromNodes(held, "Ghost", false));
            while (ghosts.Count > poses.Count) { ghosts[ghosts.Count - 1].Destroy(); ghosts.RemoveAt(ghosts.Count - 1); }
            for (int k = 0; k < ghosts.Count; k++)
            {
                ghosts[k].root.transform.SetPositionAndRotation(ToWorld(poses[k].Item1), ViewRot * poses[k].Item2);
                ghosts[k].TintAll(placeValid ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.4f, 0.35f), 0.5f);
            }
        }

        // ---------------------------------------------------------------- frame update

        public void Update()
        {
            var cam = root.farCam;
            root.nearCam.enabled = true;
            cam.cullingMask = 0;
            cam.backgroundColor = Bg;
            root.sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderEnv.UpdateStudio(root.sun);
            crowd?.Update(Time.unscaledDeltaTime);
            GameAudio.I?.Set(Mood.Builder, Ambience.Hangar);

            bool overUi = Ui.MouseOverUI(In.MousePosition);
            bool typing = GUIUtility.keyboardControl != 0;

            // Camera: right-drag orbit, scroll zoom, shift+scroll or middle-drag to move up/down.
            if (In.RightDown) rightDownPos = In.MousePosition;
            if (In.RightMouse)
            {
                var d = In.MouseDelta;
                yaw += d.x * 0.25f;
                pitch = Mathf.Clamp(pitch - d.y * 0.25f, -20f, 85f);
            }
            if (In.MiddleMouse) targetY = Mathf.Max(0, targetY - In.MouseDelta.y * dist * 0.002f);
            float sc = In.Scroll;
            if (Mathf.Abs(sc) > 0.01f && !overUi)
            {
                if (In.Held(K.LeftShift)) targetY = Mathf.Max(0, targetY + Mathf.Sign(sc) * Mathf.Max(0.5f, dist * 0.05f));
                else dist = Mathf.Clamp(dist * (sc > 0 ? 1f / 1.12f : 1.12f), 3f, 250f);
            }
            if (hall.half > 0) targetY = Mathf.Clamp(targetY, 0f, hall.top - 3f);   // what the camera looks at stays under the roof
            Vector3 target = new Vector3(0, targetY, 0);
            Quaternion q = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = q * new Vector3(0, 0, -1f);
            float useDist = dist;
            if (hall.half > 0)
            {
                // inside the hall: the camera comes in along its line of sight before it would pass through a wall or the
                // roof, so the view keeps its angle (scrolling further out does nothing)
                dist = Mathf.Min(dist, hall.half * 1.6f);
                float lim = hall.half - 2.5f;
                Vector3 lo = new Vector3(-lim, 1.2f, -lim), hi = new Vector3(lim, hall.top - 2f, lim);
                useDist = dist;
                for (int k = 0; k < 3; k++)
                {
                    if (Mathf.Abs(back[k]) < 1e-4f) continue;
                    float edge = back[k] > 0 ? hi[k] : lo[k];
                    float t = (edge - target[k]) / back[k];
                    if (t >= 0f) useDist = Mathf.Min(useDist, t);
                }
                useDist = Mathf.Max(useDist, 2f);
            }
            Vector3 camAt = target + back * useDist;
            cam.transform.position = camAt;
            cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
            UpdateBayVisibility(cam.transform.position, target);

            // Hover
            int newHover = -1;
            if (!overUi && held == null && view != null)
            {
                Physics.SyncTransforms();
                if (Physics.Raycast(Cam.ScreenPointToRay(In.MousePosition), out var hit, 3000f) && view.colliderToIndex.TryGetValue(hit.collider, out int hi)) newHover = hi;
            }
            if (newHover != hover)
            {
                if (hover >= 0 && hover < design.nodes.Count && !IsSelectedGroup(hover)) view.Tint(hover, null);
                hover = newHover;
                if (hover >= 0 && !IsSelectedGroup(hover)) view.Tint(hover, new Color(0.7f, 0.9f, 1f), 0.25f);
            }

            if (spectate)
            {
                // watching: clicking a part picks it to comment on (empty space: the whole rocket)
                if (In.LeftDown && !overUi && Coop.Current != null) Coop.Current.notePart = hover;
                return;
            }

            if (held != null)
            {
                ComputePlacement();
                UpdateGhosts();
            }

            if (!typing) HandleKeys();

            if (In.LeftDown)
            {
                Vector2 gui = new Vector2(In.MousePosition.x / Ui.Scale, (Screen.height - In.MousePosition.y) / Ui.Scale);
                if (held != null && partsRect.Contains(gui)) { DiscardHeld(); Toast("Part removed"); }
                else if (!overUi && menuPart < 0)
                {
                    if (held != null)
                    {
                        if (placeValid) Place();
                        else Toast("Move over a green node or the side of a part to attach.");
                    }
                    else if (hover >= 0) ClickPart(hover);
                    else selected = -1;
                }
            }
            if (In.RightUp && !overUi && held == null && (In.MousePosition - rightDownPos).magnitude < 5f && hover >= 0)
            {
                menuPart = hover;
                menuPos = new Vector2(In.MousePosition.x / Ui.Scale, (Screen.height - In.MousePosition.y) / Ui.Scale);
            }
        }

        bool IsSelectedGroup(int i) => selected >= 0 && selected < design.nodes.Count && DesignOps.SymmetryGroup(design, selected).Contains(i);

        void ClickPart(int i)
        {
            switch (tool)
            {
                case Tool.Place: PickUp(i); break;
                case Tool.Offset:
                case Tool.Rotate:
                    if (i == 0) { Toast("The root part can't be moved; pick another part."); break; }
                    selected = i;
                    Rebuild();
                    break;
                case Tool.Root:
                    PushUndo();
                    DesignOps.Reroot(design, i);
                    selected = -1;
                    Rebuild();
                    Toast($"{design.nodes[0].id} is now the root part");
                    tool = Tool.Place;
                    break;
            }
        }

        void HandleKeys()
        {
            bool ctrl = In.Held(K.LeftCtrl), shift = In.Held(K.LeftShift);
            if (ctrl && In.Down(K.Z)) { Undo(); return; }
            if (In.Down(K.X)) { symIndex = (symIndex + (shift ? SymSteps.Length - 1 : 1)) % SymSteps.Length; Toast($"Symmetry {Symmetry}x"); }
            if (In.Down(K.R)) { mirror = !mirror; Toast(mirror ? "Mirror symmetry" : "Radial symmetry"); }
            if (In.Down(K.C)) { snap = !snap; Toast(snap ? "Angle snap on" : "Angle snap off"); }
            if (In.Down(K.Escape)) { if (held != null) DiscardHeld(); else { selected = -1; menuPart = -1; Rebuild(); } }
            if (In.Down(K.Delete))
            {
                if (held != null) DiscardHeld();
                else if (selected > 0) { DeleteGroup(selected); selected = -1; }
                else if (hover > 0) DeleteGroup(hover);
            }

            float step = snap ? 15f : 5f;
            Quaternion r = Quaternion.identity;
            if (In.Down(K.W)) r = Quaternion.AngleAxis(step, Vector3.forward);
            if (In.Down(K.S)) r = Quaternion.AngleAxis(-step, Vector3.forward);
            if (In.Down(K.A)) r = Quaternion.AngleAxis(step, Vector3.up);
            if (In.Down(K.D)) r = Quaternion.AngleAxis(-step, Vector3.up);
            if (In.Down(K.Q)) r = Quaternion.AngleAxis(step, Vector3.right);
            if (In.Down(K.E)) r = Quaternion.AngleAxis(-step, Vector3.right);
            if (held != null) { heldExtra = heldExtra * r; return; }
            if (selected <= 0) return;
            if (tool == Tool.Rotate && r != Quaternion.identity) Nudge(Vector3.zero, r);
            if (tool == Tool.Offset)
            {
                float m = shift ? 0.25f : 0.05f;
                Vector3 o = Vector3.zero;
                if (In.Down(K.W)) o.y += m;
                if (In.Down(K.S)) o.y -= m;
                if (In.Down(K.A)) o.z += m;
                if (In.Down(K.D)) o.z -= m;
                if (In.Down(K.Q)) o.x += m;
                if (In.Down(K.E)) o.x -= m;
                if (o != Vector3.zero) Nudge(o, Quaternion.identity);
            }
        }

        void Nudge(Vector3 offLocal, Quaternion rotLocal)
        {
            PushUndo();
            foreach (int g in DesignOps.SymmetryGroup(design, selected)) DesignOps.TransformLocal(design, g, offLocal, rotLocal);
            Rebuild();
        }

        void DeleteGroup(int i)
        {
            if (i <= 0) return;
            PushUndo();
            var kill = new HashSet<int>();
            foreach (int g in DesignOps.SymmetryGroup(design, i)) foreach (int k in DesignOps.Subtree(design, g)) kill.Add(k);
            DesignOps.Remove(design, kill);
            design.manualStaging = false;
            Rebuild();
        }

        void MoveStage(int part, int delta)
        {
            PushUndo();
            Staging.Freeze(design);
            int s = design.nodes[part].stage + delta;
            if (s < 0)
            {
                foreach (var n in design.nodes) if (n.stage >= 0) n.stage++;
                s = 0;
            }
            design.nodes[part].stage = s;
            // drop empty stages
            int max = design.nodes.Max(n => n.stage);
            for (int st = max; st >= 0; st--)
                if (!design.nodes.Exists(n => n.stage == st))
                    foreach (var n in design.nodes) if (n.stage > st) n.stage--;
            Rebuild();
        }

        // ---------------------------------------------------------------- GUI

        public void OnGUI()
        {
            float W = Ui.W, H = Ui.H;
            if (Coop.Active && spectate) { DrawCoopWatch(W, H); return; }
            DrawBuilder(W, H);
            if (Coop.Active && !(showTech || showMissions || showCenter || showBadges || showStory)) DrawCoopBuilder(W, H);   // (not over a full-screen career page)
        }

        /// <summary>A rocket room, the builder's own screen: the team and their comments beside the rocket.</summary>
        void DrawCoopBuilder(float W, float H)
        {
            var c = Coop.Current;
            float x = W - 380 - 350;
            c.DrawTeam(new Rect(x, 96, 340, 30 + c.room.State.players.Count * 20 + 78), false);
            c.DrawBuildPanel(new Rect(x, 96 + 34 + c.room.State.players.Count * 20 + 82, 340, H - 430), design);
            c.DrawChat(new Rect(x, H - 160, 340, 80));
            if (!string.IsNullOrEmpty(c.room.State.summary)) GUI.Label(new Rect(300, 96, x - 310, 40), c.room.State.summary, new GUIStyle(Ui.Small) { wordWrap = true });
        }

        /// <summary>A rocket room, everyone but the builder: the rocket as it's built, its stages, comments, the goal, ready.</summary>
        void DrawCoopWatch(float W, float H)
        {
            var c = Coop.Current;
            Ui.Box(new Rect(0, 0, W, 52));
            GUI.Label(new Rect(14, 8, 600, 30), $"ROCKET ROOM  <size=14>· {c.Holder(Coop.Builder)} is building \"{design.name}\"</size>", Ui.Title);
            if (GUI.Button(new Rect(W - 150, 10, 136, 32), "Leave the room", Ui.Button)) { root.LeaveCoop(); return; }
            if (!c.Campaign) GUI.Label(new Rect(640, 16, W - 820, 22), "Click a part of the rocket to comment on it. Right-drag to look round, scroll to zoom.", Ui.Small);
            else GUI.Label(new Rect(380, H - 56, W - 780, 22), "Click a part of the rocket to comment on it. Right-drag to look round, scroll to zoom.", Ui.Small);
            float th = 30 + c.room.State.players.Count * 20 + 78;
            c.DrawTeam(new Rect(10, 60, 360, th), false);
            c.DrawBuildPanel(new Rect(10, 60 + th + 6, 360, H - 60 - th - 110), design);
            c.DrawChat(new Rect(10, H - 98, 360, 88));
            // the stages, readable but not clickable (an invisible button over them takes the clicks first)
            var sp = new Rect(W - 380, 60, 370, H - 70);
            GUI.Button(sp, GUIContent.none, GUIStyle.none);
            DrawStagePanel(sp);
            if (!string.IsNullOrEmpty(c.room.State.summary)) GUI.Label(new Rect(380, 60, W - 780, 40), c.room.State.summary, new GUIStyle(Ui.Small) { wordWrap = true });
            if (c.notePart >= 0 && c.notePart < design.nodes.Count) GUI.Label(new Rect(380, H - 34, W - 780, 22), $"Commenting on the {PartCatalog.Get(design.nodes[c.notePart].id)?.name}", Ui.Small);
            DrawCoopCareer(W, H);
        }

        /// <summary>A campaign room, everyone but the builder: the space program's money; the host also runs research, missions and facilities.</summary>
        void DrawCoopCareer(float W, float H)
        {
            var c = Coop.Current;
            var career = root.universe.career;
            if (!c.Campaign || !career.IsCareer) return;
            float x = W - 150 - 10;
            if (c.IsHost)
            {
                x -= 116;
                if (GUI.Button(new Rect(x, 10, 110, 32), "Facilities", showCenter ? Ui.RowSel : Ui.Button)) { showCenter = !showCenter; showTech = showMissions = false; }
                int offers = career.Offered.Count();
                x -= 116;
                if (GUI.Button(new Rect(x, 10, 110, 32), offers > 0 ? $"Missions ({offers})" : "Missions", showMissions ? Ui.RowSel : Ui.Button)) { showMissions = !showMissions; showTech = showCenter = false; }
                x -= 106;
                if (GUI.Button(new Rect(x, 10, 100, 32), Words.T("Tech tree", "Research", "New parts"), showTech ? Ui.RowSel : Ui.Button)) { showTech = !showTech; showMissions = showCenter = false; }
            }
            else showTech = showMissions = showCenter = false;
            GUI.Label(new Rect(x - 330, 16, 324, 22), $"<color=#ffd56a>Campaign</color>   Money <b>{career.funds:N0}</b>   Science <b>{career.science:0}</b>", Ui.Small);
            if (showTech || showMissions || showCenter)
            {
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.9f);
                Ui.Box(new Rect(0, 52, W, H - 52));
                Ui.Box(new Rect(0, 52, W, H - 52));
                GUI.color = old;
                if (showTech) DrawTechTree(new Rect(20, 58, W - 40, H - 66));
                if (showMissions) DrawMissions(new Rect(W / 2 - 520, 58, 1040, H - 66));
                if (showCenter) DrawSpaceCenter(new Rect(20, 58, W - 40, H - 66));
            }
            if (newsQueue.Count > 0 && Time.time >= toastUntil) Toast(newsQueue.Dequeue());
            if (!string.IsNullOrEmpty(toast) && Time.time < toastUntil)
                GUI.Label(new Rect(W / 2 - 400, 60, 800, Mathf.Max(30, Ui.BigCenter.CalcHeight(new GUIContent(Words.Say(toast)), 800))), Words.Say(toast), Ui.BigCenter);
        }

        void DrawBuilder(float W, float H)
        {

            // Top bar (milestone 24: tidier). Row 1: title, name, rocket/plane, money; file, undo, menu, launch site, LAUNCH.
            Ui.Box(new Rect(0, 0, W, 88));
            var career = root.universe.career;
            GUI.Label(new Rect(14, 8, 220, 30), planeView ? Words.T("PLANE BUILDER", "PLANE BUILDER", "BUILD A PLANE") : Words.T("ROCKET BUILDER", "ROCKET BUILDER", "BUILD A ROCKET"), Ui.Title);
            GUI.Label(new Rect(240, 14, 50, 24), "Name", Ui.Small);
            string newName = GUI.TextField(new Rect(282, 10, 200, 26), design.name, 40, Ui.Field);
            if (newName != design.name) design.name = newName;
            if (Event.current.type == EventType.MouseDown && !new Rect(282, 10, 200, 26).Contains(Event.current.mousePosition)) GUIUtility.keyboardControl = 0;
            if (GUI.Button(new Rect(494, 8, 76, 30), "Rocket", !planeView ? Ui.RowSel : Ui.Button)) SetView(false);
            if (GUI.Button(new Rect(572, 8, 66, 30), "Plane", planeView ? Ui.RowSel : Ui.Button)) SetView(true);
            if (career.IsCareer)
                GUI.Label(new Rect(654, 13, 330, 22), Words.T($"Funds <b>{career.funds:N0}</b>   Science <b>{career.science:0.0}</b>   Rep <b>{career.reputation:0}</b>",
                    $"Funds <b>{career.funds:N0}</b>   Science <b>{career.science:0.0}</b>   Fame <b>{career.reputation:0}</b>",
                    $"Money <b>{career.funds:N0}</b>   Science <b>{career.science:0}</b>   Stars <b>{career.reputation:0}</b>"), Ui.Label);
            else GUI.Label(new Rect(654, 15, 260, 22), Words.T("Sandbox: every part, no money", "Sandbox: every part, no money", "Free play: every part"), Ui.Small);

            string careerProblem = career.LaunchProblem(design, runwayLaunch);
            float bx = W - 10 - 150;
            GUI.enabled = validation == null && careerProblem == null && design.nodes.Count > 0 && (!Coop.Active || Coop.Current.AllReady);
            if (GUI.Button(new Rect(bx, 6, 150, 34), career.IsCareer ? $"LAUNCH  {career.LaunchCost(design):N0}" : "LAUNCH  >", Ui.Button))
            {
                if (Coop.Active) Coop.Current.Launch(design, runwayLaunch, launchPad);
                else root.Launch(design, runwayLaunch, launchPad);
                return;
            }
            GUI.enabled = true;
            bx -= 136;
            string fromLabel = runwayLaunch ? "From: Runway" : "From: " + LaunchSite.Pads[launchPad].name;
            if (!Coop.Active && GUI.Button(new Rect(bx - 40, 8, 170, 30), fromLabel, Ui.Button))
            {
                // the main pad, the two beach pads, the north pad, then the runway, and round again
                if (runwayLaunch) { runwayLaunch = false; launchPad = 0; }
                else if (launchPad < LaunchSite.Pads.Length - 1) launchPad++;
                else runwayLaunch = true;
                try { PlayerPrefs.SetInt("launchPad", launchPad); } catch { }
                Toast(runwayLaunch ? (design.IsPlane ? "Launching from the runway" : "Launching from the runway (this design has no landing gear)")
                                   : $"Launching from {LaunchSite.Pads[launchPad].name.ToLowerInvariant()}" + (launchPad > 0 ? " (rolled out from its own hall)" : ""));
            }
            bx -= 40;
            bx -= 70;
            if (GUI.Button(new Rect(bx, 8, 64, 30), Coop.Active ? "Leave" : "Menu", Ui.Button)) { if (Coop.Active) root.LeaveCoop(); else root.ShowMenu(); return; }
            bx -= 70;
            if (GUI.Button(new Rect(bx, 8, 64, 30), "Undo", Ui.Button)) Undo();
            bx -= 86;
            float fileX = bx;
            if (GUI.Button(new Rect(bx, 8, 80, 30), "File  v", showFile ? Ui.RowSel : Ui.Button)) { showFile = !showFile; showLoad = false; showVessels = false; }

            // Row 2: build tools, symmetry, snap, markers, presets, crew; career screens on the right
            float x = 14;
            foreach (Tool t in Enum.GetValues(typeof(Tool)))
            {
                if (GUI.Button(new Rect(x, 50, 70, 30), t.ToString(), tool == t ? Ui.RowSel : Ui.Button)) { tool = t; selected = -1; Rebuild(); }
                x += 74;
            }
            x += 12;
            if (GUI.Button(new Rect(x, 50, 96, 30), $"Symmetry {Symmetry}x", Ui.Button)) symIndex = (symIndex + 1) % SymSteps.Length;
            x += 100;
            if (GUI.Button(new Rect(x, 50, 74, 30), mirror ? "Mirror" : "Radial", Ui.Button)) mirror = !mirror;
            x += 78;
            if (GUI.Button(new Rect(x, 50, 84, 30), snap ? "Snap on" : "Snap off", snap ? Ui.RowSel : Ui.Button)) snap = !snap;
            x += 88;
            if (GUI.Button(new Rect(x, 50, 96, 30), markers ? "Markers on" : "Markers off", markers ? Ui.RowSel : Ui.Button)) markers = !markers;
            x += 112;
            if (GUI.Button(new Rect(x, 50, 96, 30), "Presets  v", showPresets ? Ui.RowSel : Ui.Button)) { showPresets = !showPresets; showFile = false; }
            float presetsX = x;
            x += 100;
            float crewX = x;
            bool seats = career.LaunchSeats(design) > 0;
            if (seats && GUI.Button(new Rect(x, 50, 76, 30), "Crew  v", showCrew ? Ui.RowSel : Ui.Button)) { showCrew = !showCrew; showPresets = false; }
            if (!seats) showCrew = false;
            float rx = W - 10;
            rx -= 90;
            if (GUI.Button(new Rect(rx, 50, 90, 30), $"Badges {career.badges.Count}", showBadges ? Ui.RowSel : Ui.Button)) { showBadges = !showBadges; showTech = showMissions = showCenter = false; }
            if (career.IsCareer && Coop.Active && !Coop.Current.IsHost) { showTech = showMissions = showCenter = false; }
            if (career.IsCareer && (!Coop.Active || Coop.Current.IsHost))      // a campaign room: research, missions and facilities are the host's to run
            {
                rx -= 126;
                if (GUI.Button(new Rect(rx, 50, 120, 30), "Facilities", showCenter ? Ui.RowSel : Ui.Button)) { showCenter = !showCenter; showTech = showMissions = false; showBadges = false; }
                int offers = career.Offered.Count();
                rx -= 116;
                if (GUI.Button(new Rect(rx, 50, 110, 30), offers > 0 ? $"Missions ({offers})" : "Missions", showMissions ? Ui.RowSel : Ui.Button)) { showMissions = !showMissions; showTech = false; showCenter = false; showBadges = false; }
                rx -= 106;
                if (GUI.Button(new Rect(rx, 50, 100, 30), Words.T("Tech tree", "Research", "New parts"), showTech ? Ui.RowSel : Ui.Button)) { showTech = !showTech; showMissions = false; showCenter = false; showBadges = false; }
            }
            rx -= 126;
            if (!Coop.Active && GUI.Button(new Rect(rx, 50, 120, 30), "Space Centre", Ui.Button)) { root.ShowCentre(); return; }   // the site from the air (milestone 25)
            if (FlightMode.StoryOn && !Coop.Active)
            {
                rx -= 86;
                if (GUI.Button(new Rect(rx, 50, 80, 30), "Story", showStory ? Ui.RowSel : Ui.Button)) { showStory = !showStory; showTech = showMissions = showCenter = showBadges = false; }
            }

            partsRect = new Rect(10, 96, 280, H - 106);
            DrawPartsPanel(partsRect);
            DrawStagePanel(new Rect(W - 380, 96, 370, H - 106));
            if (markers) DrawCenterMarkers();
            if (held != null) DrawNodeMarkers();
            if (menuPart >= 0 && menuPart < design.nodes.Count) DrawPartMenu();

            string blocker = validation ?? careerProblem;
            float guideBottom = H - 36;
            if (warnings.Count > 0 || blocker != null)
            {
                var lines = blocker != null ? new List<string> { blocker } : warnings;
                float wh = 34 + lines.Count * 19;
                var wr = new Rect(300, H - 36 - wh, W - 690, wh);
                guideBottom = wr.y - 6;
                Ui.Box(wr);
                GUI.Label(new Rect(wr.x + 10, wr.y + 5, 300, 20), blocker != null ? "Can't launch yet" : Words.T("Engineer's report", "Engineer's report", "Tips"), Ui.Header);
                for (int i = 0; i < lines.Count; i++)
                    GUI.Label(new Rect(wr.x + 10, wr.y + 28 + i * 19, wr.width - 20, 18), $"<color={(blocker != null ? "#ff8070" : "#ffc860")}>! {Words.Say(lines[i])}</color>", Ui.Small);
            }

            string hint = held != null
                ? "Click to attach  ·  W/S A/D Q/E rotate  ·  X symmetry  ·  R radial/mirror  ·  C snap  ·  drop on the parts list or ESC to discard"
                : tool == Tool.Place ? "Click a part to pick it up  ·  right-click a part for options  ·  right-drag rotate  ·  scroll zoom  ·  shift+scroll up/down  ·  Ctrl+Z undo"
                : tool == Tool.Offset ? "Click a part, then W/S up/down, A/D sideways, Q/E in/out (shift = bigger steps)"
                : tool == Tool.Rotate ? "Click a part, then W/S A/D Q/E to rotate it (C toggles 15 deg snap)"
                : "Click the part that should be the root (the part everything else hangs from)";
            GUI.Label(new Rect(300, H - 30, W - 690, 22), Words.Say(hint), Ui.Hint);
            if (career.chosen && Guide.Showing(career.guide)) DrawGuide(W, guideBottom);

            if (showFile) DrawFileMenu(new Rect(fileX - 60, 44, 200, 4 * 34 + 12));
            if (showLoad) DrawLoad(new Rect(fileX - 120, 44, 260, 300));
            if (showVessels) DrawVessels(new Rect(fileX - 320, 92, 430, 420));
            if (showPresets) DrawPresets(new Rect(presetsX, 84, 230, 12 * 32 + 12));
            if (showCrew) DrawCrew(crewX);
            if ((showTech || showMissions || showCenter) && career.IsCareer)
            {
                // Dim the builder behind the career screens.
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.9f);
                Ui.Box(new Rect(0, 88, W, H - 88));
                Ui.Box(new Rect(0, 88, W, H - 88));
                GUI.color = old;
            }
            if (showTech && career.IsCareer) DrawTechTree(new Rect(20, 92, W - 40, H - 100));
            if (showMissions && career.IsCareer) DrawMissions(new Rect(W / 2 - 520, 92, 1040, H - 100));
            if (showCenter && career.IsCareer) DrawSpaceCenter(new Rect(20, 92, W - 40, H - 100));
            if (showStory && FlightMode.StoryOn)
            {
                float sw = Mathf.Min(980, W - 60), sh = Mathf.Min(620, H - 110);
                if (!DrawStoryScreen(new Rect(W / 2 - sw / 2, 96, sw, sh))) showStory = false;
            }
            if (showBadges)
            {
                float bw = Mathf.Min(920, W - 60), bh = Mathf.Min(600, H - 110);
                if (!Badges.DrawScreen(new Rect(W / 2 - bw / 2, 96, bw, bh), career)) showBadges = false;
            }
            if (newsQueue.Count > 0 && Time.time >= toastUntil) Toast(newsQueue.Dequeue());
            if (!string.IsNullOrEmpty(toast) && Time.time < toastUntil)
                GUI.Label(new Rect(W / 2 - 400, 100, 800, Mathf.Max(30, Ui.BigCenter.CalcHeight(new GUIContent(Words.Say(toast)), 800))), Words.Say(toast), Ui.BigCenter);
            if (!career.chosen) DrawModeChoice(W, H);
        }

        // ---------------------------------------------------------------- presets, career screens

        bool showPresets, showTech, showMissions, showCenter, showCrew, showBadges, showFile;

        /// <summary>New, load, save and the vessels out there, in one menu (milestone 24).</summary>
        void DrawFileMenu(Rect r)
        {
            Ui.Box(r);
            float y = r.y + 6;
            foreach (var (label, act) in new (string, Action)[]
            {
                (Words.T("New design", "New design", "Start a new rocket"), () => { PushUndo(); design = new VesselDesign { name = "New Rocket" }; selected = -1; Rebuild(); }),
                (Words.T("Load a design...", "Load a design...", "Open a saved rocket"), () => { showLoad = true; saved = DesignStore.List(); }),
                (Words.T("Save this design", "Save this design", "Save this rocket"), () => { DesignStore.Save(design); Toast($"Saved \"{design.name}\""); }),
                ($"{Words.T("Vessels out there", "Vessels out there", "Ships out there")} ({root.universe.vessels.Count})", () => { showVessels = true; showLoad = false; }),
            })
            {
                if (Coop.Active && label.StartsWith(Words.T("Vessels out there", "Vessels out there", "Ships out there"))) continue;   // the room's ships are the host's
                if (GUI.Button(new Rect(r.x + 6, y, r.width - 12, 30), label, Ui.ButtonSmall)) { showFile = false; act(); }
                y += 34;
            }
        }
        public void ShowBadges(bool on) => showBadges = on;
        /// <summary>For the screenshot tour: open a career screen ("tech", "missions", "facilities") or close them (null).</summary>
        public void TourCareer(string which) { showTech = which == "tech"; showMissions = which == "missions"; showCenter = which == "facilities"; }

        /// <summary>Who flies: one row per seat, filled automatically (pilot first) unless you pick someone.</summary>
        void DrawCrew(float x)
        {
            var c = root.universe.career;
            int seats = c.LaunchSeats(design);
            while (c.crewPlan.Count < seats) c.crewPlan.Add("");
            var preview = c.PreviewCrew(design);
            var r = new Rect(x, 84, 420, 74 + seats * 26);
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 12, r.y + 6, 200, 22), "Crew", Ui.Header);
            GUI.Label(new Rect(r.x + 70, r.y + 9, r.width - 80, 20), "Pilots: SAS modes. Engineers: mining. Scientists: science.", Ui.Small);
            float y = r.y + 34;
            for (int s = 0; s < seats; s++)
            {
                var m = s < preview.Count ? preview[s] : null;
                string who = m == null ? "<color=#ff8070>nobody free</color>" : $"<b>{m.name}</b>  <color=#9fc6e8>{Crew.Tag(m)}</color>";
                GUI.Label(new Rect(r.x + 12, y + 1, 50, 20), $"Seat {s + 1}", Ui.Small);
                GUI.Label(new Rect(r.x + 62, y, r.width - 186, 20), who + (string.IsNullOrEmpty(c.crewPlan[s]) ? "  <size=11>(auto)</size>" : ""), Ui.Small);
                if (GUI.Button(new Rect(r.xMax - 120, y - 2, 56, 22), "Next", Ui.ButtonSmall)) CycleSeat(c, s);
                if (GUI.Button(new Rect(r.xMax - 60, y - 2, 50, 22), "Auto", Ui.ButtonSmall)) { c.crewPlan[s] = ""; Rebuild(); }
                y += 26;
            }
            GUI.Label(new Rect(r.x + 12, r.yMax - 26, r.width - 24, 20),
                c.IsCareer ? $"{c.Available.Count()} astronauts free. Hire more in the Space center." : "Sandbox hires fully trained astronauts as launches need them.", Ui.Small);
        }

        void CycleSeat(Career c, int s)
        {
            var taken = new HashSet<string>(c.crewPlan.Where((n, i) => i != s && !string.IsNullOrEmpty(n)));
            var options = c.Available.Where(m => !taken.Contains(m.name)).ToList();
            if (options.Count == 0) { Toast(c.IsCareer ? "Nobody else is free. Hire astronauts in the Space center." : "No one else yet: sandbox hires new astronauts at launch."); return; }
            int idx = options.FindIndex(m => m.name == c.crewPlan[s]);
            c.crewPlan[s] = options[(idx + 1) % options.Count].name;
            Rebuild();
        }
        static List<(string, VesselDesign)> presetCache;
        readonly Queue<string> newsQueue = new Queue<string>();
        Vector2 techScroll, missionScroll;

        void DrawPresets(Rect r)
        {
            Ui.Box(r);
            float y = r.y + 6;
            if (presetCache == null)
                presetCache = new List<(string, VesselDesign)>
                {
                    ("First flight", VesselDesign.FirstFlight()), ("Starter rocket", VesselDesign.Default()), ("Side boosters", VesselDesign.Broadside()), ("Moon rocket", VesselDesign.Longshot()),
                    ("Plane", VesselDesign.Kite()), ("Docking ferry", VesselDesign.Ferry()), ("Station (Outpost)", VesselDesign.Outpost()),
                    ("Tal miner (Prospector)", VesselDesign.Prospector()), ("Rover (Tumbler)", VesselDesign.Rover()),
                    ("Ochre probe (Wayfarer)", VesselDesign.Wayfarer()), ("Wanderer tug (Wrangler)", VesselDesign.Wrangler()), ("Brume probe (Long Haul)", VesselDesign.LongHaul()),
                };
            var career = root.universe.career;
            foreach (var (label, preset) in presetCache)
            {
                bool open = preset.nodes.All(n => career.IsUnlocked(n.id));
                string tag = career.IsCareer ? (open ? $"  {Prices.Of(preset):N0}" : "  (needs research)") : "";
                if (GUI.Button(new Rect(r.x + 6, y, r.width - 12, 28), label + tag, Ui.ButtonSmall))
                {
                    PushUndo();
                    design = preset.Clone();
                    selected = -1;
                    runwayLaunch = design.IsPlane || design.IsRover; // rovers get the runway to drive on
                    SetView(design.IsPlane);
                    showPresets = false;
                }
                y += 32;
            }
        }

        /// <summary>The first-flight guide in the builder: the next thing to do (launch), or how the last flight went.</summary>
        void DrawGuide(float W, float bottom)
        {
            var c = root.universe.career;
            string text = Guide.Text(c.guide, new GuideState { airTop = root.home.atmosphereHeight, maxAlt = root.flight != null ? root.flight.GuideMaxAlt : 0, orbitDv = Guide.OrbitDv }, c.IsCareer);
            float w = Mathf.Min(560, W - 710), tw = w - 24;
            float h = 32 + Ui.Small.CalcHeight(new GUIContent(text), tw) + 6;
            bool done = Guide.Finished(c.guide);
            if (done) h += 28;
            var r = new Rect(300 + (W - 690 - w) / 2, bottom - h, w, h);
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 12, r.y + 6, w - 50, 22), Guide.Title(c.guide), Ui.Header);
            if (GUI.Button(new Rect(r.xMax - 30, r.y + 6, 22, 20), "x", Ui.ButtonSmall) || (done && GUI.Button(new Rect(r.xMax - 110, r.yMax - 30, 100, 24), "Got it", Ui.ButtonSmall)))
            {
                if (!done) Toast("Guide closed: the ESC menu in flight brings it back");
                c.guide = Guide.Closed;
                root.universe.Save();
            }
            GUI.Label(new Rect(r.x + 12, r.y + 30, tw, h - 34), text, Ui.Small);
        }

        /// <summary>First start (or after "start over"): career or sandbox.</summary>
        void DrawModeChoice(float W, float H)
        {
            var u = root.universe;
            var r = new Rect(W / 2 - 320, H / 2 - 170, 640, 340);
            Ui.Box(new Rect(0, 0, W, H));
            Ui.Box(r);
            GUI.Label(new Rect(r.x, r.y + 16, r.width, 30), "How do you want to play?", Ui.BigCenter);
            if (GUI.Button(new Rect(r.x + 30, r.y + 70, 270, 50), "Career", Ui.Button)) { u.Reset(GameMode.Career); u.Save(); Enter(VesselDesign.FirstFlight()); Toast("Career started: open Missions to pick your first contracts"); }
            GUI.Label(new Rect(r.x + 30, r.y + 126, 270, 120), "Run a space program. Contracts pay for rockets, science unlocks new parts in a 9-tier tech tree, records and reputation open bigger missions. Starts a fresh universe.", Ui.Small);
            if (GUI.Button(new Rect(r.x + 340, r.y + 70, 270, 50), "Sandbox", Ui.Button)) { u.career.Start(GameMode.Sandbox); u.Save(); }
            GUI.Label(new Rect(r.x + 340, r.y + 126, 270, 120), $"Every part, no money, no contracts: build and fly anything.{(u.vessels.Count > 0 ? $" Keeps the {u.vessels.Count} vessel(s) already out there." : "")}", Ui.Small);
        }

        void DrawTechTree(Rect r)
        {
            var c = root.universe.career;
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 400, 24), "Research", Ui.Header);
            GUI.Label(new Rect(r.x + 120, r.y + 11, 600, 22), $"Science to spend: <b>{c.science:0.0}</b>. Each node needs one of the nodes before it.", Ui.Small);
            if (GUI.Button(new Rect(r.xMax - 34, r.y + 8, 24, 22), "x", Ui.ButtonSmall)) showTech = false;
            const int tiers = 9;
            var view = new Rect(r.x + 6, r.y + 38, r.width - 12, r.height - 44);
            // Nodes shrink to fit the tallest tier when there's room, and the tree scrolls when there isn't.
            int most = 1;
            for (int t = 0; t < tiers; t++) most = Mathf.Max(most, TechTree.Nodes.Count(n => n.tier == t));
            float nodeH = Mathf.Clamp((view.height - 30) / most - 8, 262, 300), contentH = 22 + most * (nodeH + 8);
            bool vScroll = contentH > view.height;
            float colW = Mathf.Max(150, (view.width - (vScroll ? 20 : 4)) / tiers);
            techScroll = GUI.BeginScrollView(view, techScroll, new Rect(0, 0, colW * tiers, vScroll ? contentH : view.height - 20));
            for (int t = 0; t < tiers; t++)
            {
                GUI.Label(new Rect(t * colW + 6, 0, colW - 12, 20), $"Tier {t}", Ui.Small);
                float y = 22;
                foreach (var n in TechTree.Nodes.Where(n => n.tier == t))
                {
                    bool have = c.unlocked.Contains(n.id), can = c.CanResearch(n);
                    bool reachable = n.requires.Length == 0 || n.requires.Any(q => c.unlocked.Contains(q));
                    var box = new Rect(t * colW + 4, y, colW - 8, nodeH);
                    var old = GUI.color;
                    GUI.color = have ? new Color(0.6f, 1f, 0.7f) : reachable ? Color.white : new Color(0.7f, 0.7f, 0.75f);
                    Ui.Box(box);
                    GUI.color = old;
                    GUI.Label(new Rect(box.x + 8, box.y + 6, box.width - 16, 20), $"<b>{n.name}</b>", Ui.Label);
                    GUI.Label(new Rect(box.x + 8, box.y + 26, box.width - 16, 62), Words.Say(n.description), Ui.Small);
                    string parts = string.Join("\n", n.parts.Select(p => PartCatalog.Get(p)?.name ?? p));
                    GUI.Label(new Rect(box.x + 8, box.y + 92, box.width - 16, 150), $"<color=#cfe6ff>{parts}</color>", Ui.Small);
                    if (have) GUI.Label(new Rect(box.x + 8, box.yMax - 30, box.width - 16, 22), "<color=#7dff8a>Researched</color>", Ui.Label);
                    else
                    {
                        if (!reachable) GUI.Label(new Rect(box.x + 8, box.yMax - 66, box.width - 16, 32), $"Needs {string.Join(" or ", n.requires.Select(q => TechTree.Get(q)?.name))}", Ui.Small);
                        GUI.enabled = can;
                        string lbl = can ? $"Research ({n.cost:0})" : $"{n.cost:0} science";
                        if (GUI.Button(new Rect(box.x + 6, box.yMax - 34, box.width - 12, 28), lbl, Ui.ButtonSmall) && c.Research(n)) { Rebuild(); root.universe.Save(); }
                        GUI.enabled = true;
                    }
                    y += nodeH + 8;
                }
            }
            GUI.EndScrollView();
            foreach (var n in c.news) newsQueue.Enqueue(n);
            c.news.Clear();
        }

        Vector2 rosterScroll;

        /// <summary>Facilities (upgrade), Administration strategies, and the astronaut corps (hire).</summary>
        void DrawSpaceCenter(Rect r)
        {
            var c = root.universe.career;
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 300, 24), "Facilities", Ui.Header);
            GUI.Label(new Rect(r.x + 150, r.y + 11, 700, 22), $"Funds {c.funds:N0}   Science {c.science:0.0}   Reputation {c.reputation:0}", Ui.Small);
            if (GUI.Button(new Rect(r.xMax - 34, r.y + 8, 24, 22), "x", Ui.ButtonSmall)) showCenter = false;
            float colW = (r.width - 48) / 3;
            float x0 = r.x + 12, top = r.y + 40;

            // Facilities
            GUI.Label(new Rect(x0, top, colW, 20), "FACILITIES", Ui.Small);
            float y = top + 22;
            foreach (var (f, name, _) in Facilities.List)
            {
                int lvl = c.Level(f);
                var box = new Rect(x0, y, colW, 74);
                Ui.Box(box);
                string pips = new string('#', lvl + 1) + new string('-', Facilities.MaxLevel - lvl);
                GUI.Label(new Rect(box.x + 10, box.y + 5, box.width - 20, 20), $"<b>{name}</b>  <size=11>level {lvl + 1}  [{pips}]</size>", Ui.Label);
                GUI.Label(new Rect(box.x + 10, box.y + 25, box.width - 20, 20), Facilities.Effect(f, lvl), Ui.Small);
                if (lvl < Facilities.MaxLevel)
                {
                    GUI.Label(new Rect(box.x + 10, box.y + 45, box.width - 140, 20), $"<color=#9fc6e8>Next: {Facilities.Effect(f, lvl + 1)}</color>", Ui.Small);
                    double cost = c.UpgradeCost(f);
                    GUI.enabled = c.funds >= cost;
                    if (GUI.Button(new Rect(box.xMax - 124, box.y + 40, 116, 26), $"Upgrade {cost:N0}", Ui.ButtonSmall) && c.Upgrade(f)) { Rebuild(); root.universe.Save(); }
                    GUI.enabled = true;
                }
                else GUI.Label(new Rect(box.x + 10, box.y + 45, box.width - 20, 20), "<color=#7dff8a>Fully upgraded</color>", Ui.Small);
                y += 78;
            }

            // Strategies
            float x1 = x0 + colW + 12;
            GUI.Label(new Rect(x1, top, colW, 20), $"ADMINISTRATION: STRATEGIES  {c.strategies.Count} / {c.StrategySlots}", Ui.Small);
            y = top + 22;
            foreach (var s in Career.Strategies)
            {
                bool on = c.HasStrategy(s.id);
                var box = new Rect(x1, y, colW, 92);
                var old = GUI.color;
                if (on) GUI.color = new Color(0.65f, 1f, 0.75f);
                Ui.Box(box);
                GUI.color = old;
                GUI.Label(new Rect(box.x + 10, box.y + 5, box.width - 20, 20), $"<b>{s.name}</b>{(on ? "  <color=#7dff8a>active</color>" : "")}", Ui.Label);
                GUI.Label(new Rect(box.x + 10, box.y + 25, box.width - 20, 36), Words.Say(s.description), Ui.Small);
                if (on) { if (GUI.Button(new Rect(box.x + 10, box.y + 62, 110, 24), "End it", Ui.ButtonSmall)) { c.Deactivate(s); root.universe.Save(); } }
                else
                {
                    GUI.enabled = c.CanActivate(s);
                    if (GUI.Button(new Rect(box.x + 10, box.y + 62, 160, 24), $"Start ({s.setupCost:N0})", Ui.ButtonSmall)) { c.Activate(s); Rebuild(); root.universe.Save(); }
                    GUI.enabled = true;
                    if (c.strategies.Count >= c.StrategySlots) GUI.Label(new Rect(box.x + 180, box.y + 66, box.width - 190, 20), "No free slot", Ui.Small);
                }
                y += 96;
            }

            // Astronauts
            float x2 = x1 + colW + 12;
            int living = c.LivingCrew;
            GUI.Label(new Rect(x2, top, colW, 20), $"ASTRONAUTS  {living}{(c.MaxRoster < int.MaxValue ? $" / {c.MaxRoster}" : "")}", Ui.Small);
            GUI.enabled = living < c.MaxRoster && c.funds >= c.HireCost;
            GUI.Label(new Rect(x2, top + 20, 130, 22), $"Hire ({c.HireCost:N0}):", Ui.Small);
            float hx = x2 + 120;
            foreach (var role in new[] { CrewRole.Pilot, CrewRole.Engineer, CrewRole.Scientist })
            {
                if (GUI.Button(new Rect(hx, top + 18, 80, 24), Crew.RoleName(role), Ui.ButtonSmall)) { Toast(c.Hire(role)); Rebuild(); root.universe.Save(); }
                hx += 84;
            }
            GUI.enabled = true;
            var view = new Rect(x2, top + 48, colW, r.yMax - top - 58);
            rosterScroll = GUI.BeginScrollView(view, rosterScroll, new Rect(0, 0, colW - 18, Math.Max(view.height - 4, c.roster.Count * 40)));
            float ry = 0;
            foreach (var m in c.roster.OrderBy(k => k.state).ThenBy(k => k.role))
            {
                var flying = m.state == 1 ? root.universe.Find(m.vesselId) : null;
                string st = m.state == 0 ? "<color=#7dff8a>available</color>" : m.state == 1 ? $"<color=#ffd060>aboard {flying?.name ?? "a vessel"}</color>" : "<color=#ff8070>lost</color>";
                int next = m.Level < Crew.MaxLevel ? Crew.Thresholds[m.Level] : 0;
                var face = Ardlings.LookFor(m.name);   // each Ardling's sticky note
                GUI.DrawTexture(new Rect(4, ry + 3, 32, 32), Ardlings.FaceTexture(face.face, face.paper));
                GUI.Label(new Rect(42, ry, colW - 62, 20), $"<b>{m.name}</b>  <color=#9fc6e8>{Crew.RoleName(m.role)}</color>  <size=11>level {m.Level} {Crew.Stars(m.Level)}</size>", Ui.Label);
                GUI.Label(new Rect(50, ry + 18, colW - 70, 20), $"{st}   <size=11>{m.flights} flight{(m.flights == 1 ? "" : "s")} · XP {m.Xp}{(next > 0 ? $"/{next}" : " (max)")}</size>", Ui.Small);
                ry += 40;
            }
            GUI.EndScrollView();
            foreach (var n in c.news) newsQueue.Enqueue(n);
            c.news.Clear();
        }

        void DrawMissions(Rect r)
        {
            var c = root.universe.career;
            double ut = root.universe.ut;
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 400, 24), "Mission control", Ui.Header);
            GUI.Label(new Rect(r.x + 170, r.y + 11, 600, 22), $"Funds {c.funds:N0}   Science {c.science:0.0}   Reputation {c.reputation:0} (better pay as it grows)", Ui.Small);
            if (GUI.Button(new Rect(r.xMax - 34, r.y + 8, 24, 22), "x", Ui.ButtonSmall)) showMissions = false;
            float colW = (r.width - 36) / 2;
            var left = new Rect(r.x + 12, r.y + 40, colW, r.height - 52);
            var right = new Rect(left.xMax + 12, r.y + 40, colW, r.height - 52);
            GUI.Label(new Rect(left.x, left.y, colW, 20), "OFFERED", Ui.Small);
            float y = left.y + 22;
            foreach (var k in c.Offered.ToList())
            {
                var box = new Rect(left.x, y, colW, 112);
                Ui.Box(box);
                GUI.Label(new Rect(box.x + 10, box.y + 6, box.width - 20, 20), $"<b>{Words.Say(k.title)}</b>", Ui.Label);
                GUI.Label(new Rect(box.x + 10, box.y + 26, box.width - 20, 36), Words.Say(k.description), Ui.Small);
                GUI.Label(new Rect(box.x + 10, box.y + 62, box.width - 20, 20), k.RewardText + $"   ·   {k.deadline / 86400:0} days", Ui.Small);
                GUI.enabled = c.Active.Count() < c.ActiveLimit;
                if (GUI.Button(new Rect(box.x + 10, box.y + 82, 90, 24), "Accept", Ui.ButtonSmall) && c.Accept(k, ut)) { root.universe.SpawnRescues(root.bodies); root.universe.Save(); }
                GUI.enabled = true;
                if (GUI.Button(new Rect(box.x + 106, box.y + 82, 90, 24), "Decline", Ui.ButtonSmall)) { c.Decline(k); c.RefreshOffers(ut); root.universe.Save(); }
                GUI.Label(new Rect(box.x + 206, box.y + 86, box.width - 216, 20), $"fail: -{k.penalty:N0}", Ui.Small);
                y += 118;
            }
            GUI.Label(new Rect(right.x, right.y, colW, 20), c.ActiveLimit < int.MaxValue ? $"ACTIVE  {c.Active.Count()} / {c.ActiveLimit}  (Mission Control level {c.Level(Facility.MissionControl) + 1})" : "ACTIVE", Ui.Small);
            y = right.y + 22;
            var act = c.Active.ToList();
            if (act.Count == 0) { GUI.Label(new Rect(right.x, y, colW, 20), "None yet: accept an offer on the left.", Ui.Small); y += 24; }
            foreach (var k in act)
            {
                GUI.Label(new Rect(right.x, y, colW, 20), $"<b>{Words.Say(k.title)}</b>  <size=11>due in {Ui.Time(Math.Max(0, k.deadline - ut))}</size>", Ui.Label);
                GUI.Label(new Rect(right.x + 10, y + 19, colW - 10, 34), Words.Say(k.description), Ui.Small);
                y += 54;
            }
            y += 8;
            GUI.Label(new Rect(right.x, y, colW, 20), "RECORDS", Ui.Small);
            y += 20;
            // Two columns, so the list (18 records with Vesk's) stays inside the panel even with several active contracts.
            int rows = (Records.List.Length + 1) / 2;
            float half = colW / 2;
            for (int i = 0; i < Records.List.Length; i++)
            {
                var rec = Records.List[i];
                float rx = right.x + (i < rows ? 0 : half), ry = y + (i % rows) * 18;
                if (ry > right.yMax - 40) continue;
                bool got = (c.records & rec.bit) != 0;
                GUI.Label(new Rect(rx, ry, half - 4, 20), got ? $"<color=#7dff8a>[x] {rec.name}</color>" : $"<color=#8a95a0>[ ] {rec.name} +{rec.funds / 1000:0}k</color>", Ui.Small);
            }
            int done = c.contracts.Count(k => k.state == ContractState.Completed), failed = c.contracts.Count(k => k.state == ContractState.Failed);
            GUI.Label(new Rect(right.x, right.yMax - 20, colW, 20), $"Completed {done}   Failed {failed}", Ui.Small);
        }

        void DrawNodeMarkers()
        {
            var hdef = PartCatalog.Get(held[0].id);
            if (hdef.radial) return;
            for (int i = 0; i < design.nodes.Count; i++)
            {
                var n = design.nodes[i];
                var d = PartCatalog.Get(n.id);
                if (hdef.stackBottom && DesignOps.TopFree(design, i)) Dot(ToWorld(n.pos));
                if (hdef.stackTop && DesignOps.BottomFree(design, i)) Dot(ToWorld(DesignOps.BottomNode(n, d)));
            }
        }

        /// <summary>Centre of mass, centre of lift (wings, control surfaces, fins) and centre of thrust.</summary>
        void DrawCenterMarkers()
        {
            if (design.nodes.Count == 0) return;
            Vector3 com = DesignOps.CenterOfMass(design);
            Vector3 lsum = Vector3.zero, tsum = Vector3.zero, tdir = Vector3.zero;
            float area = 0, thrust = 0;
            foreach (var n in design.nodes)
            {
                var d = PartCatalog.Get(n.id);
                if (d == null) continue;
                float a = d.LiftArea;
                if (a > 0) { lsum += DesignOps.Center(n, d) * a; area += a; }
                float t = d.IsJet ? d.jetThrust : d.thrustVac;
                if (t > 0) { tsum += DesignOps.BottomNode(n, d) * t; tdir += DesignOps.Axis(n) * t; thrust += t; }
            }
            var cMass = new Color(1f, 0.85f, 0.2f);
            var cLift = new Color(0.35f, 0.72f, 1f);
            var cThrust = new Color(1f, 0.45f, 0.9f);
            if (thrust > 0 && tdir.sqrMagnitude > 1e-6f)
            {
                Vector3 cot = tsum / thrust, dir = tdir.normalized;
                for (int k = 1; k <= 10; k++) Dot(ToWorld(cot + dir * (k * 0.3f)), cThrust, 4f);
                Marker(ToWorld(cot), cThrust, Words.T("CoT", "Thrust", "Push"));
            }
            Marker(ToWorld(com), cMass, Words.T("CoM", "Mass", "Weight"));
            string note = null;
            if (area > 0)
            {
                Vector3 col = lsum / area;
                Marker(ToWorld(col), cLift, Words.T("CoL", "Lift", "Wings"));
                float behind = com.y - col.y; // +Y is the nose, so positive = lift behind the mass
                if (Words.Easy) note = behind > 0.05f ? "The wings and fins are behind the weight: it will fly straight." : behind < -0.05f ? "The wings and fins are in front of the weight: it will flip over!" : "It might wobble.";
                else note = behind > 0.05f ? $"Centre of lift {behind:0.00} m behind the centre of mass: stable."
                     : behind < -0.05f ? $"Centre of lift {-behind:0.00} m ahead of the centre of mass: it will try to flip."
                     : "Centre of lift right on the centre of mass: twitchy.";
            }
            var r = new Rect(300, 96, 360, note != null ? 44 : 24);
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 8, r.y + 3, r.width, 20), Words.T("<color=#ffd933>o CoM</color>   <color=#59b8ff>o CoL</color>   <color=#ff73e6>o CoT</color>",
                "<color=#ffd933>o centre of mass</color>   <color=#59b8ff>o lift</color>   <color=#ff73e6>o thrust</color>", "<color=#ffd933>o weight</color>   <color=#59b8ff>o wings</color>   <color=#ff73e6>o push</color>"), Ui.Small);
            if (note != null) GUI.Label(new Rect(r.x + 8, r.y + 22, r.width - 10, 20), note, Ui.Small);
        }

        void Marker(Vector3 world, Color c, string label)
        {
            Vector3 sp = Cam.WorldToScreenPoint(world);
            if (sp.z <= 0) return;
            float gx = sp.x / Ui.Scale, gy = (Screen.height - sp.y) / Ui.Scale;
            Ui.Line(new Rect(gx - 8, gy - 8, 16, 16), new Color(0, 0, 0, 0.7f));
            Ui.Line(new Rect(gx - 6, gy - 6, 12, 12), c);
            Ui.Line(new Rect(gx - 2, gy - 2, 4, 4), new Color(0, 0, 0, 0.8f));
            GUI.Label(new Rect(gx + 10, gy - 10, 60, 20), $"<b>{label}</b>", Ui.Small);
        }

        void Dot(Vector3 world, Color c, float size)
        {
            Vector3 sp = Cam.WorldToScreenPoint(world);
            if (sp.z <= 0) return;
            float gx = sp.x / Ui.Scale, gy = (Screen.height - sp.y) / Ui.Scale;
            Ui.Line(new Rect(gx - size * 0.5f, gy - size * 0.5f, size, size), c);
        }

        void Dot(Vector3 world)
        {
            Vector3 sp = Cam.WorldToScreenPoint(world);
            if (sp.z <= 0) return;
            float gx = sp.x / Ui.Scale, gy = (Screen.height - sp.y) / Ui.Scale;
            Ui.Line(new Rect(gx - 5, gy - 5, 10, 10), new Color(0.4f, 1f, 0.5f, 0.9f));
        }

        void DrawPartsPanel(Rect r)
        {
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width, 24), "Parts", Ui.Header);
            float infoH = 110;
            var viewRect = new Rect(r.x + 4, r.y + 32, r.width - 8, r.height - 38 - infoH);
            float contentH = 0;
            var cats = new List<string>();
            var career = root.universe.career;
            int locked = 0;
            foreach (var kind in CategoryOrder)
                foreach (var def in PartCatalog.All.Where(p => p.kind == kind))
                {
                    if (!career.IsUnlocked(def.id)) { locked++; continue; }
                    if (!cats.Contains(def.Category)) { cats.Add(def.Category); contentH += 24; }
                    contentH += 50;
                }
            if (locked > 0) contentH += 40;
            contentH += 30 + subs.Length * 30;
            partsScroll = GUI.BeginScrollView(viewRect, partsScroll, new Rect(0, 0, viewRect.width - 18, contentH));
            float y = 0;
            hoverDef = null;
            var shown = new HashSet<string>();
            foreach (string cat in cats)
            {
                GUI.Label(new Rect(4, y + 2, 240, 20), cat.ToUpperInvariant(), Ui.Small);
                y += 24;
                foreach (var kind in CategoryOrder)
                    foreach (var def in PartCatalog.All.Where(p => p.kind == kind && p.Category == cat && career.IsUnlocked(p.id)))
                    {
                        if (!shown.Add(def.id)) continue;
                        var br = new Rect(2, y, viewRect.width - 22, 46);
                        if (GUI.Button(br, GUIContent.none, Ui.Button)) Hold(new List<PartNode> { new PartNode { id = def.id } });
                        GUI.Label(new Rect(br.x + 8, br.y + 2, br.width - 10, 20), def.name, Ui.Bold);
                        if (career.IsCareer) GUI.Label(new Rect(br.xMax - 64, br.y + 3, 60, 20), $"{Prices.Of(def):N0}", Ui.Small);
                        GUI.Label(new Rect(br.x + 8, br.y + 23, br.width - 14, 18), Words.PartLine(def), Ui.SmallLine);
                        if (br.Contains(Event.current.mousePosition)) hoverDef = def;
                        y += 50;
                    }
            }
            if (locked > 0)
            {
                GUI.Label(new Rect(4, y + 4, 240, 36), $"{locked} more parts to research in the Tech tree.", Ui.Small);
                y += 40;
            }
            GUI.Label(new Rect(4, y + 6, 240, 20), "SUBASSEMBLIES", Ui.Small);
            y += 28;
            if (subs.Length == 0) GUI.Label(new Rect(8, y, 240, 20), "Right-click a part > Save as subassembly", Ui.Small);
            foreach (var s in subs)
            {
                if (GUI.Button(new Rect(2, y, viewRect.width - 22, 26), s, Ui.Button))
                {
                    var nodes = SubStore.Load(s);
                    if (nodes != null && nodes.Count > 0) Hold(nodes);
                }
                y += 30;
            }
            GUI.EndScrollView();

            var info = new Rect(r.x + 10, r.yMax - infoH, r.width - 20, infoH - 6);
            var showDef = hoverDef ?? (held != null ? PartCatalog.Get(held[0].id) : null);
            if (showDef != null)
            {
                GUI.Label(new Rect(info.x, info.y, info.width, 20), showDef.name, Ui.Bold);
                GUI.Label(new Rect(info.x, info.y + 20, info.width, info.height - 20), Words.Easy ? Words.PartLine(showDef) : Words.Say(showDef.description) + "\n" + Words.PartLine(showDef), Ui.Small);
            }
            else GUI.Label(new Rect(info.x, info.y, info.width, info.height), Words.T("Click a part to pick it up, then click on your rocket to attach it. Green dots are free stack nodes.",
                "Click a part to pick it up, then click on your rocket to attach it. Green dots show where parts can stack.",
                "Click a part, then click on your rocket to stick it on. Green dots show where it can go."), Ui.Small);
        }

        void DrawStagePanel(Rect r)
        {
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 10, r.y + 6, 260, 24), Words.T("Staging and delta-v", "Stages and speed budget", "Steps"), Ui.Header);
            if (design.manualStaging && GUI.Button(new Rect(r.xMax - 110, r.y + 6, 100, 22), "Auto staging", Ui.ButtonSmall))
            {
                PushUndo();
                design.manualStaging = false;
                Rebuild();
            }
            var defs = design.Defs();
            float contentH = 0;
            foreach (var st in stageList) contentH += 44 + st.AllParts().Count() * 22;
            var viewRect = new Rect(r.x + 4, r.y + 34, r.width - 8, r.height - 120);
            stageScroll = GUI.BeginScrollView(viewRect, stageScroll, new Rect(0, 0, viewRect.width - 18, contentH));
            float y = 0;
            for (int s = 0; s < stageList.Count; s++)
            {
                var st = stageList[s];
                var rep = s < reports.Count ? reports[s] : null;
                string stats = rep == null || !rep.hasEngines ? "" : Words.T($"dv {rep.dvVac:0} m/s  TWR {rep.twrAsl:0.00}  {rep.burnTime:0}s",
                    $"{rep.dvVac:0} m/s  push/weight {rep.twrAsl:0.0}  {rep.burnTime:0}s", rep.twrAsl < 1 && s == 0 ? "<color=#ff9070>too heavy to lift off!</color>" : $"runs {rep.burnTime:0}s");
                GUI.Label(new Rect(4, y, 330, 20), $"<b>{Words.T("Stage", "Stage", "Step")} {s + 1}</b>  <size=12>{stats}</size>", Ui.Label);
                y += 22;
                foreach (int i in st.AllParts().ToList())
                {
                    if (i >= defs.Count || defs[i] == null) continue;
                    string verb = st.decouple.Contains(i) ? Words.T("Decouple", "Separate", "Split off") : st.ignite.Contains(i) ? Words.T("Ignite", "Start", "Start") : st.chutes.Contains(i) ? Words.T("Deploy", "Open", "Open") : Words.T("Jettison", "Drop", "Drop");
                    GUI.Label(new Rect(14, y, 250, 20), $"{verb} {defs[i].name}", Ui.Small);
                    if (GUI.Button(new Rect(viewRect.width - 76, y, 26, 20), "^", Ui.ButtonSmall)) { MoveStage(i, -1); GUI.EndScrollView(); return; }
                    if (GUI.Button(new Rect(viewRect.width - 48, y, 26, 20), "v", Ui.ButtonSmall)) { MoveStage(i, +1); GUI.EndScrollView(); return; }
                    y += 22;
                }
                y += 22;
            }
            GUI.EndScrollView();

            double total = reports.Sum(rp => rp.dvVac), totalAsl = reports.Sum(rp => rp.dvAsl);
            float mass = defs.Where(d => d != null).Sum(d => d.WetMass);
            Ui.Line(new Rect(r.x + 10, r.yMax - 82, r.width - 20, 1), new Color(1, 1, 1, 0.15f));
            GUI.Label(new Rect(r.x + 10, r.yMax - 76, r.width - 20, 20), Words.T($"Total dv  <b>{total:0} m/s</b> vacuum  ({totalAsl:0} at sea level)",
                $"Speed budget  <b>{total:0} m/s</b> in space  ({totalAsl:0} low down)", $"Rocket power  <b>{total:0}</b>  (orbit needs about 3,400)"), Ui.Label);
            int seats = defs.Where(d => d != null).Sum(d => d.crew);
            string supText = root.universe.career.suppliesOn && seats > 0 ? $"   Supplies <b>{Supplies.DaysText(Supplies.DaysFor(design))}</b> for {seats}" : "";
            GUI.Label(new Rect(r.x + 10, r.yMax - 56, r.width - 20, 20), Words.Easy ? $"Parts {design.nodes.Count}{(supText != "" ? $"   Food for <b>{Supplies.DaysText(Supplies.DaysFor(design))}</b>" : "")}"
                : $"Mass {mass:0.00} t   Parts {design.nodes.Count}   {(design.manualStaging ? "manual staging" : "auto staging")}{supText}", Ui.Small);
            GUI.Label(new Rect(r.x + 10, r.yMax - 36, r.width - 20, 34),
                Words.Easy && validation == null ? (design.IsRover ? "A rover: drive it from the runway." : design.IsPlane ? "A plane: take off from the runway." : total < 3300 ? "This might not have enough power to go round the planet." : "Enough power to go round the planet!") :
                Words.Say(validation) ?? Words.Say(design.IsRover ? "A rover: launch it from the runway to test drive, or carry it to Tal or Vesk on a lander." : design.IsPlane ? "Jets only breathe below ~25 km: their delta-v is for flying around, not for orbit." : total < 3300 ? "Orbit needs about 3,300-3,500 m/s; this may come up short." : "Orbit needs 3,300-3,500 m/s; Tal and back 5,500; to Ochre and land about 6,500."), Ui.Small);
        }

        void DrawPartMenu()
        {
            var n = design.nodes[menuPart];
            var def = PartCatalog.Get(n.id);
            var r = new Rect(Mathf.Min(menuPos.x + 10, Ui.W - 330), Mathf.Min(menuPos.y, Ui.H - 360), 320, 350);
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 10, r.y + 6, 260, 22), def.name, Ui.Header);
            if (GUI.Button(new Rect(r.xMax - 30, r.y + 6, 22, 22), "x", Ui.ButtonSmall)) { menuPart = -1; return; }
            var group = DesignOps.SymmetryGroup(design, menuPart);
            GUI.Label(new Rect(r.x + 10, r.y + 30, 300, 40), def.StatsLine + (group.Count > 1 ? $"\nIn a {group.Count}x symmetry group" : ""), Ui.Small);
            float y = r.y + 74;
            if (def.variants > 0)
            {
                GUI.Label(new Rect(r.x + 10, y + 3, 60, 20), "Paint", Ui.Small);
                if (GUI.Button(new Rect(r.x + 70, y, 140, 24), PartCatalog.PaintNames[n.variant % PartCatalog.PaintNames.Length] + "  >", Ui.Button))
                {
                    PushUndo();
                    int v = (n.variant + 1) % Mathf.Min(def.variants, PartCatalog.Paints.Length);
                    foreach (int g in group) design.nodes[g].variant = v;
                    Rebuild();
                }
                y += 32;
            }
            GUI.Label(new Rect(r.x + 10, y, 300, 20), "Action groups (keys 1-0, Backspace, G, U, B, Y)", Ui.Small);
            y += 22;
            for (int g = 0; g < Groups.Count; g++)
            {
                int col = g % 7, row = g / 7;
                var br = new Rect(r.x + 10 + col * 43, y + row * 30, 40, 26);
                bool on = (n.groups & Groups.Bit(g)) != 0;
                if (GUI.Button(br, g < 10 ? Groups.Name(g) : Groups.Name(g).Substring(0, Math.Min(5, Groups.Name(g).Length)), on ? Ui.RowSel : Ui.ButtonSmall))
                {
                    PushUndo();
                    foreach (int gi in group) design.nodes[gi].groups ^= Groups.Bit(g);
                    if (on) foreach (int gi in group) design.nodes[gi].groups &= ~Groups.Bit(g);
                    Rebuild();
                }
            }
            y += 30 * ((Groups.Count + 6) / 7) + 6;
            if (GUI.Button(new Rect(r.x + 10, y, 145, 28), "Save as subassembly", Ui.ButtonSmall))
            {
                var sub = DesignOps.Extract(design, menuPart);
                string name = $"{def.name} assembly {DateTime.Now:HHmmss}";
                SubStore.Save(name, sub);
                subs = SubStore.List();
                Toast($"Saved \"{name}\"");
            }
            if (GUI.Button(new Rect(r.x + 165, y, 145, 28), menuPart == 0 ? "Pick up all" : "Delete", Ui.ButtonSmall))
            {
                if (menuPart == 0) PickUp(0); else DeleteGroup(menuPart);
                menuPart = -1;
                return;
            }
            y += 34;
            if (GUI.Button(new Rect(r.x + 10, y, 300, 28), "Make this the root part", Ui.ButtonSmall))
            {
                PushUndo();
                DesignOps.Reroot(design, menuPart);
                menuPart = -1;
                Rebuild();
            }
        }

        void DrawLoad(Rect r)
        {
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width, 22), "Saved rockets", Ui.Header);
            float y = r.y + 34;
            if (saved.Length == 0) GUI.Label(new Rect(r.x + 10, y, r.width - 20, 40), "Nothing saved yet. Use Save to keep a design.", Ui.Small);
            foreach (var name in saved)
            {
                if (GUI.Button(new Rect(r.x + 10, y, r.width - 20, 26), name, Ui.Button))
                {
                    var d = DesignStore.Load(name);
                    if (d != null) { PushUndo(); design = d; selected = -1; runwayLaunch = design.IsPlane; SetView(design.IsPlane); }
                    showLoad = false;
                }
                y += 30;
                if (y > r.yMax - 30) break;
            }
        }
    }

    /// <summary>Saved part groups ("subassemblies") under the persistent data folder.</summary>
    public static class SubStore
    {
        [Serializable] class Sub { public string name; public List<PartNode> nodes = new List<PartNode>(); }

        static string Dir => Path.Combine(Application.persistentDataPath, "Subassemblies");

        static string Safe(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
        }

        public static void Save(string name, List<PartNode> nodes)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, Safe(name) + ".json"), JsonUtility.ToJson(new Sub { name = name, nodes = nodes }, true));
        }

        public static List<PartNode> Load(string name)
        {
            try
            {
                var s = JsonUtility.FromJson<Sub>(File.ReadAllText(Path.Combine(Dir, Safe(name) + ".json")));
                return s?.nodes;
            }
            catch (Exception e) { Debug.LogWarning("Couldn't load subassembly: " + e.Message); return null; }
        }

        public static string[] List() => Directory.Exists(Dir)
            ? Directory.GetFiles(Dir, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToArray()
            : new string[0];
    }
}
