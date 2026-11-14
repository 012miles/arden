using System;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 28 (captaincarl24's pick): practice tools, for sandbox games (pause menu, "Practice tools"). Refill the ship;
    /// put it in orbit round any world; drop it a few km above any world, or beside one of the hidden places, to practise
    /// landing; jump the clock to sunrise, noon, sunset or night where it is.
    /// </summary>
    public partial class FlightMode
    {
        public bool showPractice;
        int practiceBody = 1, practiceSpot;
        float practiceAlt = 100f;

        public bool PracticeAllowed => !U.career.IsCareer;

        CelestialBody[] PracticeWorlds => root.bodies.Where(b => !b.isStar).ToArray();

        /// <summary>Fills every tank, battery and supply store, regrows ablator, mends broken legs.</summary>
        public void PracticeRefill()
        {
            foreach (var p in active.parts)
            {
                p.propellant = p.def.propellant;
                p.mono = p.def.mono;
                p.ablator = p.def.ablator;
                p.charge = p.def.ecCapacity;
                p.supplies = p.def.supplyCapacity;
                p.broken = false;
                p.temp = 288;
            }
            active.starvingSince = -1;
            active.RecomputeMass();
            Show("Refilled: fuel, monopropellant, power, supplies; legs mended.", 3f);
        }

        /// <summary>Moves the ship somewhere new (body-relative, inertial), still and steady.</summary>
        void PracticeMove(CelestialBody b, Vector3d pos, Vector3d vel)
        {
            var v = active;
            v.body = b;
            v.landed = false;
            v.hasLaunched = true;
            v.pos = v.prevPos = pos;
            v.vel = vel;
            v.angVel = Vector3.zero;
            v.rails = null;
            v.throttle = 0;
            Vector3d up = pos.normalized;
            Vector3d along = vel.sqrMagnitude > 1 ? vel.normalized : CelestialBody.EastAt(up);
            v.rot = v.prevRot = Quaternion.FromToRotation(Vector3.up, up.ToFloat());
            SetWarpOne();
            warpTargetUt = null;
            node = null;
            laterNodes.Clear();
            autopilotOn = false;
            target = null; targetVessel = null;
            for (int i = loaded.Count - 1; i >= 0; i--) if (loaded[i] != v) { RemoveView(loaded[i]); loaded.RemoveAt(i); }
            mapFocus = b;
            trajTimer = 0;
            GroundFx.Clear();
        }

        public void PracticeOrbit(CelestialBody b, double altitude)
        {
            double r = b.radius + Math.Max(altitude, b.HasAtmosphere ? b.atmosphereHeight + 5000 : Terrain.MaxHeight(b) + 3000);
            Vector3d dir = b.FixedToInertial(CelestialBody.DirectionFromLatLon(0, 0), ut);
            Vector3d east = CelestialBody.EastAt(dir);
            PracticeMove(b, dir * r, east * Math.Sqrt(b.mu / r));
            Show($"In a {Ui.Dist(r - b.radius)} orbit round {b.name}.", 3f);
        }

        /// <summary>Hanging still (moving with the ground) some way above a spot on a world, ready to land.</summary>
        public void PracticeHover(CelestialBody b, Vector3d fixedDir, double height, string where)
        {
            Vector3d dir = fixedDir.normalized;
            double h = Math.Max(0, LaunchSite.SurfaceHeight(b, dir)) + height;
            Vector3d pos = b.FixedToInertial(dir, ut) * (b.radius + h);
            PracticeMove(b, pos, b.SurfaceVelocity(pos));
            Show($"{Ui.Dist(height)} above {where}. Land gently!", 4f);
        }

        /// <summary>Jumps the clock (with the time warp) to the next sunrise (0), noon (1), sunset (2) or night (3) where the ship is.</summary>
        public void PracticeTimeOfDay(int kind)
        {
            var b = active.body;
            if (b.isStar) return;
            Vector3d fixedDir = b.InertialToFixed(active.pos, ut).normalized;
            double period = Math.Abs(b.rotationPeriod) > 1 ? Math.Abs(b.rotationPeriod) : 86400;
            double step = Math.Max(30, period / 1500), best = ut, bestScore = double.MaxValue, prev = double.NaN;
            for (double t = ut + step; t < ut + period + step; t += step)
            {
                double s = Vector3d.Dot(b.FixedToInertial(fixedDir, t), Sun.DirectionFrom(b.AbsolutePosition(t), t));
                bool rising = !double.IsNaN(prev) && s > prev;
                double score = kind == 0 ? (rising ? Math.Abs(s - 0.06) : 9) : kind == 1 ? -s : kind == 2 ? (!rising && !double.IsNaN(prev) ? Math.Abs(s - 0.06) : 9) : Math.Abs(s + 0.7);
                if (score < bestScore) { bestScore = score; best = t; }
                prev = s;
            }
            string label = new[] { "sunrise", "noon", "sunset", "night" }[kind];
            if (active.CanRailsWarp())
            {
                warpTargetUt = best;
                warpLabel = label;
                Show($"Warping to {label}...", 2f);
                return;
            }
            // flying: jump the clock, keeping the ship where it is over the ground and moving the same way
            var v = active;
            Vector3d pf = b.InertialToFixed(v.pos, ut), vf = b.InertialToFixed(v.vel - b.SurfaceVelocity(v.pos), ut);
            ut = best;
            prevUt = ut;
            v.pos = v.prevPos = b.FixedToInertial(pf, ut);
            v.vel = b.SurfaceVelocity(v.pos) + b.FixedToInertial(vf, ut);
            v.rails = null;
            trajTimer = 0;
            Show($"Jumped to {label}.", 3f);
        }

        void DrawPractice(float W, float H)
        {
            var r = new Rect(W * 0.5f - 270, H * 0.5f - 210, 540, 380);
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 16, r.y + 10, 300, 26), "Practice tools", Ui.Header);
            GUI.Label(new Rect(r.x + 16, r.y + 36, r.width - 32, 20), "Sandbox only: move your ship anywhere and try again.", Ui.Small);
            float y = r.y + 66;
            var worlds = PracticeWorlds;
            practiceBody = Mathf.Clamp(practiceBody, 0, worlds.Length - 1);
            var wb = worlds[practiceBody];
            if (GUI.Button(new Rect(r.x + 16, y, 200, 30), "Refill everything", Ui.Button)) PracticeRefill();
            y += 44;
            GUI.Label(new Rect(r.x + 16, y + 4, 60, 22), "World", Ui.Label);
            if (GUI.Button(new Rect(r.x + 80, y, 30, 28), "<", Ui.ButtonSmall)) practiceBody = (practiceBody + worlds.Length - 1) % worlds.Length;
            GUI.Label(new Rect(r.x + 112, y + 3, 120, 22), wb.name, Ui.Center);
            if (GUI.Button(new Rect(r.x + 234, y, 30, 28), ">", Ui.ButtonSmall)) practiceBody = (practiceBody + 1) % worlds.Length;
            y += 40;
            GUI.Label(new Rect(r.x + 16, y + 4, 120, 22), "Orbit at", Ui.Label);
            if (GUI.Button(new Rect(r.x + 110, y, 30, 28), "-", Ui.ButtonSmall)) practiceAlt = Mathf.Max(10f, practiceAlt - (practiceAlt > 200 ? 100 : 10));
            GUI.Label(new Rect(r.x + 142, y + 3, 90, 22), $"{practiceAlt:0} km", Ui.Center);
            if (GUI.Button(new Rect(r.x + 234, y, 30, 28), "+", Ui.ButtonSmall)) practiceAlt = Mathf.Min(5000f, practiceAlt + (practiceAlt >= 200 ? 100 : 10));
            if (GUI.Button(new Rect(r.x + 280, y, 240, 28), $"Put me in orbit round {wb.name}", Ui.ButtonSmall)) { PracticeOrbit(wb, practiceAlt * 1000); showPractice = false; return; }
            y += 40;
            bool canLand = !wb.IsGasGiant;
            GUI.enabled = canLand;
            if (GUI.Button(new Rect(r.x + 16, y, 504, 28), canLand ? $"Drop me 3 km above {wb.name} by day, to practise landing" : $"{wb.name} has no ground to land on", Ui.ButtonSmall))
            {
                Vector3d sun = wb.InertialToFixed(Sun.DirectionFrom(wb.AbsolutePosition(ut), ut), ut);
                Vector3d spot = (sun + CelestialBody.EastAt(sun) * 0.35).normalized;
                if (wb.IsHomeWorld) spot = LaunchSite.DirectionAt(wb, 600, -700);
                PracticeHover(wb, spot, 3000, wb.name);
                showPractice = false; GUI.enabled = true; return;
            }
            GUI.enabled = true;
            y += 40;
            var spots = Landmarks.All(root.bodies).ToList();
            practiceSpot = Mathf.Clamp(practiceSpot, 0, spots.Count - 1);
            var ls = spots[practiceSpot];
            GUI.Label(new Rect(r.x + 16, y + 4, 90, 22), "Place", Ui.Label);
            if (GUI.Button(new Rect(r.x + 80, y, 30, 28), "<", Ui.ButtonSmall)) practiceSpot = (practiceSpot + spots.Count - 1) % spots.Count;
            bool known = U.career.visited.Contains(ls.id);
            GUI.Label(new Rect(r.x + 112, y + 3, 200, 22), known ? ls.name : $"? on {ls.body}", Ui.Center);
            if (GUI.Button(new Rect(r.x + 314, y, 30, 28), ">", Ui.ButtonSmall)) practiceSpot = (practiceSpot + 1) % spots.Count;
            if (GUI.Button(new Rect(r.x + 356, y, 164, 28), "Drop me next to it", Ui.ButtonSmall))
            {
                var lb = root.bodies.First(b => b.name == ls.body);
                Vector3d near = (ls.dir + CelestialBody.EastAt(ls.dir) * (Math.Min(400, ls.reach * 0.6) / lb.radius)).normalized;
                PracticeHover(lb, near, 900, known ? ls.name : $"something on {ls.body}");
                showPractice = false; return;
            }
            y += 44;
            GUI.Label(new Rect(r.x + 16, y + 4, 120, 22), "Time of day", Ui.Label);
            string[] tods = { "Sunrise", "Noon", "Sunset", "Night" };
            for (int i = 0; i < 4; i++)
                if (GUI.Button(new Rect(r.x + 130 + i * 98, y, 92, 28), tods[i], Ui.ButtonSmall)) { PracticeTimeOfDay(i); showPractice = false; return; }
            y += 44;
            if (GUI.Button(new Rect(r.xMax - 116, r.yMax - 44, 100, 30), "Close", Ui.Button)) showPractice = false;
        }
    }
}
