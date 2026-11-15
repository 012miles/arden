using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 44 (captaincarl24: "for the plane cockpit make a new model for it cuz like it uses the same cockpit as the
    /// rocket which im standing on floor even though im horizontal", "make it so that when your cockpit is upside down your on
    /// the roof unless the g forces are holding you down"). A plane's cabins are built along the plane: the nose ahead and the
    /// belly as the floor (a rocket's are built across it, with the engines below). The pull you feel (VesselSim.feltLocal) is
    /// turned into that frame. So on the runway and in level flight you stand on the floor. Rolled upside down you drop onto
    /// the roof and walk about up there (the view turns over). Pulling through a loop, the g-force holds you on the floor. A
    /// shuttle standing on the pad lays you on your back against the rear wall.
    /// - The cockpit: an instrument panel and a raked windscreen ahead with its frame, side windows, a curved roof, seats facing
    ///   the nose (one in the middle, or two side by side with more rows behind for a bigger crew), rudder pedals, and the thrust
    ///   levers between the seats.
    /// - The passenger cabin: rows of seats either side of an aisle, small windows with shades, overhead bins, a door at the back.
    /// </summary>
    public partial class FlightMode
    {
        bool planeRoom, airlinerRoom;
        Quaternion cabinRot = Quaternion.identity;   // the cabin's frame in the ship's (a plane's: cabin forward = the nose, cabin up = the top)
        float iflip;                                 // 0 standing on the floor, 1 standing on the roof (upside down)
        bool onCeiling;
        const float PlaneWallTop = 1.45f, PlaneSill = 0.95f, CockpitRake = 0.9f, LinerWallTop = 1.55f, LinerHead = 1.37f;

        Quaternion CabinToWorld => renderRot * cabinRot;
        Vector3 EyeAt => ipos + Vector3.up * Mathf.Lerp(Eye, BodyH + 0.05f - Eye, iflip);

        /// <summary>For tours and tests: a plane's cabin, and how far the view has turned over (0 floor, 1 roof).</summary>
        public bool TestPlaneRoom => planeRoom;
        public float TestFlip => iflip;
        public string TestCabinShape => $"{(planeRoom ? airlinerRoom ? "airliner" : "cockpit" : roomRound ? "capsule" : "habitat")} {roomW:0.0} x {roomL:0.0}, {seats.Count} seats";

        /// <summary>Is this ship a plane (its cabins built along it)? Landing gear says so.</summary>
        static bool BuiltAsPlane(VesselSim v) => v != null && v.parts.Exists(q => q.def.kind == PartKind.Gear);

        // ---------------------------------------------------------------- the cockpit

        void BuildCockpit(MeshBuilder b, MeshBuilder glow, int crew)
        {
            roomRound = false;
            int rows = crew <= 2 ? 1 : 1 + (crew - 1) / 2;
            roomW = 2.8f;
            roomL = 3.4f + (rows - 1) * 1.1f;
            roomR = roomL / 2;
            float h = RoomH, hw = roomW / 2, hl = roomL / 2;
            var prof = RoofProfile(PlaneWallTop);
            float Zf(float y) => hl - CockpitRake * Mathf.Clamp01((y - PlaneSill) / (h - PlaneSill));
            // the floor
            b.Mat = MatRubber;
            b.AddQuad(new Vector3(-hw, 0, -hl), new Vector3(-hw, 0, hl), new Vector3(hw, 0, hl), new Vector3(hw, 0, -hl), Vector3.up, Floor);
            // under the windscreen: the front wall behind the instrument panel
            b.Mat = MatPanel;
            WallQuad(b, new Vector3(hw, 0, hl), new Vector3(-hw, 0, hl), Vector3.back, 0, PlaneSill, Trim * 1.3f);
            // the sides: solid below the sill, side windows at the front, solid behind them
            float zw0 = hl - 1.45f;
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 n = new Vector3(-s, 0, 0);
                Vector3 back = new Vector3(s * hw, 0, -hl), front = new Vector3(s * hw, 0, hl), w0 = new Vector3(s * hw, 0, zw0);
                WallQuad(b, back, front, n, 0, PlaneSill, Wall);
                WallQuad(b, back, w0, n, PlaneSill, PlaneWallTop, Wall);
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(new Vector3(s * (hw - 0.02f), PlaneSill, (zw0 + Zf(PlaneSill)) / 2), new Vector3(0.05f, 0.05f, Zf(PlaneSill) - zw0), Trim, Quaternion.identity);
                b.AddBox(new Vector3(s * (hw - 0.02f), PlaneWallTop, (zw0 + Zf(PlaneWallTop)) / 2), new Vector3(0.05f, 0.05f, Zf(PlaneWallTop) - zw0), Trim, Quaternion.identity);
                b.AddBox(new Vector3(s * (hw - 0.02f), (PlaneSill + PlaneWallTop) / 2, zw0), new Vector3(0.05f, PlaneWallTop - PlaneSill, 0.07f), Trim, Quaternion.identity);
                b.AddBox(new Vector3(s * (hw - 0.02f), (PlaneSill + PlaneWallTop) / 2, zw0 + 0.62f), new Vector3(0.04f, PlaneWallTop - PlaneSill, 0.05f), Trim, Quaternion.identity);
                b.Mat = MatPanel;
            }
            // the curved roof, running forward to the top of the windscreen
            PlaneRoof(b, prof, -hl, Zf, Wall * 0.92f);
            // the windscreen's frame: posts from the sill up to the roof, the sill itself, and the top edge
            b.Mat = MeshBuilder.MatMatte;
            foreach (int k in new[] { 0, 2, 4, 6, 8 })
                Beam(b, new Vector3(prof[k].x, PlaneSill, hl), new Vector3(prof[k].x, prof[k].y, Zf(prof[k].y)), k == 4 ? 0.07f : 0.05f, Trim);
            Beam(b, new Vector3(-hw, PlaneSill, hl - 0.02f), new Vector3(hw, PlaneSill, hl - 0.02f), 0.07f, Trim);
            for (int k = 0; k + 1 < prof.Count; k++)
                Beam(b, new Vector3(prof[k].x, prof[k].y, Zf(prof[k].y)), new Vector3(prof[k + 1].x, prof[k + 1].y, Zf(prof[k + 1].y)), 0.05f, Trim);
            // the back wall, with a small round door on the left (the locker's on the right)
            b.Mat = MatPanel;
            PlaneEndWall(b, prof, -hl, Vector3.forward, PlaneWallTop, Wall);
            hatchAt = new Vector3(-0.45f, 0, -hl + 0.5f);
            b.Mat = MeshBuilder.MatPaint;
            int s0 = b.v.Count;
            b.AddFrustum(0.05f, 0f, 0.45f, 0.45f, new Color(0.6f, 0.62f, 0.65f), 20, true, false);   // (its wheel turns: FlightCabin)
            XfLocal(b, s0, new Vector3(hatchAt.x, 0.8f, -hl + 0.01f), Quaternion.Euler(90, 0, 0));
            // seats facing the nose: one in the middle for a one-seater, else two side by side, rows behind for more
            panelAt = new Vector3(0, 0, hl - 0.7f);
            int seatCount = Mathf.Max(1, crew);
            for (int i = 0; i < seatCount; i++)
            {
                int row = crew <= 2 ? 0 : i / 2;
                float x = crew == 1 ? 0 : (i % 2 == 0 ? -0.62f : 0.62f);
                var at = new Vector3(x, 0, hl - 1.35f - row * 1.1f);
                seats.Add(at);
                b.Mat = MatFabric;
                b.AddBox(at + new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.1f, 0.5f), Seat, Quaternion.identity);
                b.AddBox(at + new Vector3(0, 0.62f, -0.25f), new Vector3(0.5f, 0.62f, 0.08f), Seat, Quaternion.Euler(-10, 0, 0));
                b.AddBox(at + new Vector3(0, 1.0f, -0.32f), new Vector3(0.3f, 0.16f, 0.08f), Seat * 0.8f, Quaternion.Euler(-10, 0, 0));   // headrest
                foreach (int s in new[] { -1, 1 }) b.AddBox(at + new Vector3(s * 0.27f, 0.45f, 0), new Vector3(0.05f, 0.05f, 0.4f), Trim * 1.4f, Quaternion.identity);   // armrests
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(at + new Vector3(0, 0.13f, 0), new Vector3(0.12f, 0.26f, 0.12f), Trim * 1.6f, Quaternion.identity);
                if (row == 0)
                    foreach (int s in new[] { -1, 1 })   // rudder pedals
                        b.AddBox(at + new Vector3(s * 0.13f, 0.1f, 0.82f), new Vector3(0.1f, 0.18f, 0.04f), Trim * 1.8f, Quaternion.Euler(-30, 0, 0));
            }
            // a light in the roof
            glow.AddBox(new Vector3(0, h - 0.03f, -0.2f), new Vector3(0.8f, 0.02f, 0.5f), new Color(1f, 0.95f, 0.85f) * 0.45f, Quaternion.identity);
        }

        // ---------------------------------------------------------------- the passenger cabin

        void BuildAirliner(MeshBuilder b, MeshBuilder glow, int crew)
        {
            roomRound = false;
            int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, crew) / 4f));
            roomW = 3.4f;
            roomL = Mathf.Max(6.0f, 4.8f + rows * 0.95f);
            roomR = roomL / 2;
            float h = RoomH, hw = roomW / 2, hl = roomL / 2;
            var prof = RoofProfile(LinerWallTop);
            // carpet, with a lighter strip down the aisle
            b.Mat = MatFabric;
            b.AddQuad(new Vector3(-hw, 0, -hl), new Vector3(-hw, 0, hl), new Vector3(hw, 0, hl), new Vector3(hw, 0, -hl), Vector3.up, new Color(0.28f, 0.32f, 0.42f));
            b.AddQuad(new Vector3(-0.32f, 0.004f, -hl), new Vector3(-0.32f, 0.004f, hl), new Vector3(0.32f, 0.004f, hl), new Vector3(0.32f, 0.004f, -hl), Vector3.up, new Color(0.4f, 0.44f, 0.52f));
            // the sides: a row of small windows
            b.Mat = MatPanel;
            var wins = PlaneWindows();
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 n = new Vector3(-s, 0, 0);
                Vector3 back = new Vector3(s * hw, 0, -hl), front = new Vector3(s * hw, 0, hl);
                WallQuad(b, back, front, n, 0, PlaneSill, Wall);
                WallQuad(b, back, front, n, LinerHead, LinerWallTop, Wall);
                float z = -hl;
                foreach (var w in wins)
                {
                    if (Mathf.Sign(w.c.x) != s) continue;
                    float z0 = w.c.z - w.w / 2, z1 = w.c.z + w.w / 2;
                    WallQuad(b, new Vector3(s * hw, 0, z), new Vector3(s * hw, 0, z0), n, PlaneSill, LinerHead, Wall);
                    b.Mat = MeshBuilder.MatMatte;
                    b.AddBox(new Vector3(s * (hw - 0.015f), (PlaneSill + LinerHead) / 2, w.c.z), new Vector3(0.03f, LinerHead - PlaneSill + 0.06f, w.w + 0.06f), Trim * 1.6f, Quaternion.identity);
                    b.Mat = MatPanel;
                    z = z1;
                }
                WallQuad(b, new Vector3(s * hw, 0, z), front, n, PlaneSill, LinerHead, Wall);
            }
            // the window surrounds are frames only: cut their middles back out (drawn as a frame of four bars instead)
            // (the box above would fill the window, so it's thin bars instead)
            PlaneRoof(b, prof, -hl, _ => hl, Wall * 0.94f);
            PlaneEndWall(b, prof, hl, Vector3.back, LinerWallTop, Wall * 0.95f);    // the front bulkhead (the cockpit's beyond it)
            PlaneEndWall(b, prof, -hl, Vector3.forward, LinerWallTop, Wall);       // the back, with the door
            hatchAt = new Vector3(0, 0, -hl + 0.5f);
            b.Mat = MeshBuilder.MatPaint;
            int s0 = b.v.Count;
            b.AddFrustum(0.05f, 0f, 0.55f, 0.55f, new Color(0.6f, 0.62f, 0.65f), 20, true, false);
            XfLocal(b, s0, new Vector3(0, 0.8f, -hl + 0.01f), Quaternion.Euler(90, 0, 0));
            // rows of seats, two either side of the aisle, facing the nose
            panelAt = new Vector3(0, 0, hl - 0.7f);
            int seatCount = Mathf.Max(1, crew);
            float[] xs = { -0.62f, 0.62f, -1.12f, 1.12f };
            for (int i = 0; i < seatCount; i++)
            {
                var at = new Vector3(xs[i % 4], 0, hl - 1.9f - (i / 4) * 0.95f);
                seats.Add(at);
                b.Mat = MatFabric;
                Color c = new Color(0.22f, 0.3f, 0.55f);
                b.AddBox(at + new Vector3(0, 0.3f, 0), new Vector3(0.48f, 0.1f, 0.46f), c, Quaternion.identity);
                b.AddBox(at + new Vector3(0, 0.66f, -0.24f), new Vector3(0.48f, 0.7f, 0.08f), c, Quaternion.Euler(-8, 0, 0));
                b.AddBox(at + new Vector3(0, 0.98f, -0.29f), new Vector3(0.48f, 0.08f, 0.09f), new Color(0.9f, 0.9f, 0.88f), Quaternion.Euler(-8, 0, 0));   // the headrest cover
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(at + new Vector3(0, 0.13f, 0), new Vector3(0.36f, 0.26f, 0.1f), Trim * 1.5f, Quaternion.identity);
                b.AddBox(at + new Vector3(0, 0.62f, -0.29f), new Vector3(0.34f, 0.24f, 0.02f), Trim * 1.2f, Quaternion.Euler(-8, 0, 0));   // the tray table on the back
            }
            for (int k = 0; k < 3; k++) glow.AddBox(new Vector3(0, h - 0.02f, -hl + 1.2f + k * (roomL - 2.4f) / 2f), new Vector3(0.5f, 0.02f, 1.2f), new Color(1f, 0.95f, 0.85f) * 0.4f, Quaternion.identity);
        }

        /// <summary>A plane's windows (for the walls' openings, the glass and the shades): the cockpit's side windows, the cabin's rows.</summary>
        List<(Vector3 c, Vector3 n, float w, float sill, float head)> PlaneWindows()
        {
            var res = new List<(Vector3, Vector3, float, float, float)>();
            float hw = roomW / 2, hl = roomL / 2;
            if (!airlinerRoom)
            {
                foreach (int s in new[] { -1, 1 })
                {
                    float z0 = hl - 1.45f, z1 = hl - 0.45f;
                    res.Add((new Vector3(s * hw, 0, (z0 + z1) / 2), new Vector3(-s, 0, 0), z1 - z0, PlaneSill, PlaneWallTop));
                }
                return res;
            }
            for (float z = -hl + 1.4f; z < hl - 0.7f; z += 0.95f)
                foreach (int s in new[] { -1, 1 })
                    res.Add((new Vector3(s * hw, 0, z), new Vector3(-s, 0, 0), 0.34f, PlaneSill, LinerHead));
            return res;
        }

        /// <summary>The roof's cross-section: from the top of the left wall, arching over the middle, to the top of the right wall.</summary>
        List<Vector2> RoofProfile(float wallTop)
        {
            const int n = 8;
            var res = new List<Vector2>();
            float hw = roomW / 2;
            for (int k = 0; k <= n; k++) { float a = Mathf.PI * (1f - k / (float)n); res.Add(new Vector2(hw * Mathf.Cos(a), wallTop + (RoomH - wallTop) * Mathf.Sin(a))); }
            return res;
        }

        /// <summary>The curved roof from the back wall forward (to where the windscreen meets it, height by height).</summary>
        static void PlaneRoof(MeshBuilder b, List<Vector2> prof, float zBack, System.Func<float, float> zFront, Color c)
        {
            float wallTop = prof[0].y;
            for (int k = 0; k + 1 < prof.Count; k++)
            {
                Vector2 a = prof[k], e = prof[k + 1], mid = (a + e) / 2;
                Vector3 n = new Vector3(-mid.x, -(mid.y - wallTop) - 0.05f, 0).normalized;
                b.AddQuad(new Vector3(a.x, a.y, zBack), new Vector3(e.x, e.y, zBack), new Vector3(e.x, e.y, zFront(e.y)), new Vector3(a.x, a.y, zFront(a.y)), n, c);
            }
        }

        /// <summary>An end wall shaped to the roof: square below the wall tops, a fan under the arch.</summary>
        void PlaneEndWall(MeshBuilder b, List<Vector2> prof, float z, Vector3 n, float wallTop, Color c)
        {
            float hw = roomW / 2;
            b.AddQuad(new Vector3(-hw, 0, z), new Vector3(-hw, wallTop, z), new Vector3(hw, wallTop, z), new Vector3(hw, 0, z), n, c);
            Vector3 centre = new Vector3(0, wallTop, z);
            for (int k = 0; k + 1 < prof.Count; k++)
                b.AddTri(centre, new Vector3(prof[k].x, prof[k].y, z), new Vector3(prof[k + 1].x, prof[k + 1].y, z), n, c);
        }

        /// <summary>A square bar from a to e.</summary>
        static void Beam(MeshBuilder b, Vector3 a, Vector3 e, float t, Color c)
        {
            Vector3 d = e - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Vector3 dn = d.normalized;
            b.AddBox((a + e) / 2, new Vector3(t, t, d.magnitude), c, Quaternion.LookRotation(dn, Mathf.Abs(dn.y) > 0.95f ? Vector3.forward : Vector3.up));
        }

        // ---------------------------------------------------------------- the things only planes have

        /// <summary>The windscreen's glass (and its reentry glow), the thrust levers, the passenger cabin's bins and signs.</summary>
        void BuildPlaneExtras(float h)
        {
            float hw = roomW / 2, hl = roomL / 2;
            if (!airlinerRoom)
            {
                var prof = RoofProfile(PlaneWallTop);
                float Zf(float y) => hl - CockpitRake * Mathf.Clamp01((y - PlaneSill) / (h - PlaneSill));
                void Panes(MeshBuilder b, Color c)
                {
                    for (int k = 0; k + 1 < prof.Count; k++)
                        b.AddQuad(new Vector3(prof[k].x, PlaneSill, hl - 0.01f), new Vector3(prof[k + 1].x, PlaneSill, hl - 0.01f),
                            new Vector3(prof[k + 1].x, prof[k + 1].y, Zf(prof[k + 1].y) - 0.01f), new Vector3(prof[k].x, prof[k].y, Zf(prof[k].y) - 0.01f), new Vector3(0, -0.6f, -0.8f), c);
                }
                PieceMat("Windscreen", Vector3.zero, Quaternion.identity, b => Panes(b, new Color(0.75f, 0.85f, 0.95f, 0.035f)), Mats.Unlit);
                var pl = Piece("Plasma", Vector3.zero, Quaternion.identity, b => Panes(b, new Color(1f, 0.45f, 0.15f, 0f)), true);
                plasma.Add((pl.GetComponent<MeshFilter>().sharedMesh, new Color(1f, 0.45f, 0.15f)));
                // a dark coaming along the bottom of the windscreen (no glare off the panel)
                Piece("Coaming", new Vector3(0, PlaneSill + 0.03f, hl - 0.1f), Quaternion.identity, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(Vector3.zero, new Vector3(roomW - 0.12f, 0.05f, 0.2f), C(0.08f, 0.08f, 0.09f), Quaternion.identity); });
                // the thrust levers between the two front seats (one-seaters have them on the left)
                bool pair = seats.Count >= 2;
                Vector3 ped = pair ? new Vector3(0, 0, hl - 1.15f) : new Vector3(-0.45f, 0, hl - 1.05f);
                var levers = Piece("Thrust levers", ped, Quaternion.identity, b =>
                {
                    b.Mat = MatPanel;
                    b.AddBox(new Vector3(0, pair ? 0.36f : 0.6f, 0), new Vector3(pair ? 0.34f : 0.18f, pair ? 0.72f : 0.08f, pair ? 0.75f : 0.32f), C(0.17f, 0.18f, 0.2f), Quaternion.identity);
                    b.Mat = MeshBuilder.MatGloss;
                    int n = pair ? 4 : 1;
                    for (int k = 0; k < n; k++)
                    {
                        float x = (k - (n - 1) / 2f) * 0.07f;
                        b.AddBox(new Vector3(x, (pair ? 0.72f : 0.64f) + 0.09f, 0.05f), new Vector3(0.02f, 0.18f, 0.02f), C(0.7f, 0.72f, 0.75f), Quaternion.Euler(20, 0, 0));
                        b.AddBox(new Vector3(x, (pair ? 0.72f : 0.64f) + 0.18f, 0.08f), new Vector3(0.05f, 0.035f, 0.035f), C(0.15f, 0.15f, 0.15f), Quaternion.identity);
                    }
                });
                AddProp("levers", "Thrust levers", ped + new Vector3(0, pair ? 0.9f : 0.82f, 0.06f), () => $"Thrust levers: throttle {active?.throttle * 100:0}%. Scroll on the throttle by the panel (or sit down and use SHIFT / CTRL)", null, 0.12f);
            }
            else
            {
                // overhead bins down both sides, with lids, and reading lights under them
                foreach (int s in new[] { -1, 1 })
                {
                    Piece("Bins", new Vector3(s * (hw - 0.34f), 1.78f, 0), Quaternion.identity, b =>
                    {
                        b.Mat = MatPanel;
                        b.AddBox(Vector3.zero, new Vector3(0.62f, 0.36f, roomL - 1.6f), C(0.9f, 0.9f, 0.88f), Quaternion.identity);
                        for (float z = -roomL / 2 + 1.0f; z < roomL / 2 - 0.9f; z += 0.95f)
                            b.AddBox(new Vector3(-s * 0.312f, -0.02f, z), new Vector3(0.006f, 0.3f, 0.012f), C(0.6f, 0.6f, 0.6f), Quaternion.identity);
                        b.AddBox(new Vector3(-s * 0.31f, -0.19f, 0), new Vector3(0.04f, 0.02f, roomL - 1.6f), C(0.3f, 0.32f, 0.35f), Quaternion.identity);
                    });
                }
                Piece("Seat belt sign", new Vector3(0, 1.9f, hl - 0.02f), Quaternion.Euler(0, 180, 0), b => { b.Mat = MatPanel; b.AddBox(Vector3.zero, new Vector3(0.5f, 0.16f, 0.03f), C(0.12f, 0.12f, 0.14f), Quaternion.identity); });
                Label(new Vector3(0, 1.9f, hl - 0.04f), Quaternion.Euler(0, 180, 0), "FASTEN SEAT BELT", 0.03f, C(1f, 0.8f, 0.3f));
            }
        }

        /// <summary>Planes: anything the cabin kit put past the walls (it's laid out for a wider habitat) is brought back in.</summary>
        void KeepPlanePropsInside()
        {
            if (!planeRoom || room == null) return;
            float lx = roomW / 2 - 0.04f, lz = roomL / 2 - 0.04f;
            foreach (Transform t in room.transform)
            {
                var p = t.localPosition;
                if (Mathf.Abs(p.x) > lx) p.x = Mathf.Sign(p.x) * lx;
                if (Mathf.Abs(p.z) > lz) p.z = Mathf.Sign(p.z) * lz;
                t.localPosition = p;
            }
            foreach (var p in props)
            {
                if (Mathf.Abs(p.at.x) > lx) p.at.x = Mathf.Sign(p.at.x) * lx;
                if (Mathf.Abs(p.at.z) > lz) p.at.z = Mathf.Sign(p.at.z) * lz;
            }
        }
    }
}
