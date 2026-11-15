using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 46 tests: the ten plane presets (valid, take off, climb; the shuttle from the pad) and the fly-home autopilot's plan.</summary>
    public static partial class SimTests
    {
        static void RunMilestone46()
        {
            Run("Plane presets: ten planes, each valid, with landing gear, seats and wings", PlanePresetsValid);
            Run("Plane presets park: every runway plane sits still on the runway with its brakes on", PlanesParked);
            Run("Plane presets take off: every runway plane lifts off inside the runway and climbs", PlanePresetsTakeoff);
            Run("Space shuttle preset: launches from the pad on its boosters and climbs", ShuttleClimbs);
            Run("Trip autopilot: the Tal Lander preset reaches orbit on the launch autopilot (the bug test found it in the sea)", TalLanderToOrbit);
            Run("Fly home: from a low orbit round Tal, the planned burn leaves Tal and dips into Arden's air for the parachutes", ReturnPlanTest);
        }

        static string PlanePresetsValid()
        {
            var all = Presets.All();
            var lines = new List<string>();
            foreach (var name in Presets.PlaneOnes)
            {
                var found = all.FirstOrDefault(p => p.label == name);
                Check(found.design != null, "missing " + name);
                var d = found.design;
                Check(d.Validate() == null, $"{name}: {d.Validate()}");
                Check(DesignOps.TreeProblem(d) == null, $"{name}: {DesignOps.TreeProblem(d)}");
                Check(d.IsPlane, $"{name} has no landing gear");
                Check(d.Defs().Any(x => x.kind == PartKind.Wing), $"{name} has no wings");
                int seats = d.Defs().Sum(x => x.crew);
                Check(seats >= 1, $"{name}: no seats");
                bool upright = d.LaunchesUpright;
                Check(upright == name.Contains("shuttle"), $"{name}: launches upright {upright}");
                lines.Add($"{d.name} {seats}s {Career.LaunchMass(d):0}t");
            }
            return string.Join(", ", lines);
        }

        /// <summary>The Kite test's takeoff, for any plane: full throttle, brakes off, nose up 10° past the rotation speed.</summary>
        static (bool ok, string line) Takeoff(VesselDesign design, double rotate = 70)
        {
            var b = Body;
            var s = VesselSim.CreateOnRunway(design, b, 0);
            s.ActivateNextStage();
            s.brakesOn = false;
            s.throttle = 1;
            Vector3d f0 = b.InertialToFixed(s.pos, 0);
            Vector3d eastFixed = b.InertialToFixed(Vector3d.FromFloat(s.rot * Vector3.up), 0);
            double ut = 0, roll = -1, vLift = 0, maxAlt = 0, worstBank = 0;
            for (int i = 0; i < 4500 && !s.destroyed; i++)
            {
                Vector3d up = s.pos.normalized, east = b.FixedToInertial(eastFixed, ut);
                east = (east - up * Vector3d.Dot(east, up)).normalized;
                double speed = s.SurfaceRelVelocity.magnitude;
                double pitch = (speed < rotate ? 0 : 10) * Math.PI / 180;
                s.steerDirection = east * Math.Cos(pitch) + up * Math.Sin(pitch);
                s.Step(0.02, ut);
                ut += 0.02;
                double radar = s.RadarAltitude(ut);
                if (roll < 0 && radar > 5) { roll = (b.InertialToFixed(s.pos, ut) - f0).magnitude; vLift = speed; }
                maxAlt = Math.Max(maxAlt, s.Altitude);
                if (roll > 0) worstBank = Math.Max(worstBank, Math.Abs(Math.Asin(Math.Max(-1, Math.Min(1, Vector3d.Dot(Vector3d.FromFloat(s.rot * Vector3.right), up)))) * 180 / Math.PI));
            }
            bool ok = !s.destroyed && roll > 0 && roll < 2000 && s.Altitude > 300;
            string line = $"{design.name}: {(s.destroyed ? "DESTROYED " + s.destroyReason : roll < 0 ? "never lifted off" : $"off after {roll:0} m at {vLift:0} m/s")}, {s.Altitude:0} m at 90 s, {s.SurfaceRelVelocity.magnitude:0} m/s, bank {worstBank:0} deg";
            return (ok, line);
        }

        static string PlanesParked()
        {
            var b = Body;
            var lines = new List<string>();
            foreach (var (name, d) in Presets.Planes())
            {
                if (d.LaunchesUpright) continue;
                var s = VesselSim.CreateOnRunway(d, b, 0);
                double ut = 0, h0 = s.Altitude;
                for (int i = 0; i < 400; i++) { s.Step(0.02, ut); ut += 0.02; }
                double speed = s.SurfaceRelVelocity.magnitude;
                lines.Add($"{d.name} {speed:0.00} m/s {s.Altitude - h0:+0.00;-0.00} m{(s.destroyed ? " DESTROYED " + s.destroyReason : "")}");
                Check(!s.destroyed && speed < 0.5, string.Join(", ", lines));
            }
            return string.Join(", ", lines);
        }

        static string PlanePresetsTakeoff()
        {
            var lines = new List<string>();
            bool all = true;
            foreach (var (name, d) in Presets.Planes())
            {
                if (d.LaunchesUpright) continue;
                var (ok, line) = Takeoff(d);
                lines.Add((ok ? "ok " : "BAD ") + line);
                all &= ok;
            }
            Check(all, string.Join(" | ", lines));
            return string.Join(" | ", lines);
        }

        /// <summary>-sweep46: for each runway plane, the wing and wheel positions that take off soonest and climb (prints the best).</summary>
        public static void Sweep46()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (name, make) in Presets.PlaneBuilders)
            {
                string best = null; double bestScore = double.MaxValue; var good = new List<string>();
                foreach (float lift in new[] { -0.3f, -0.1f, 0.1f, 0.3f, 0.5f })
                    foreach (float gear in new[] { 0.15f, 0.35f, 0.6f, 0.9f })
                    {
                        var (ok, line) = Takeoff(make(lift, gear));
                        // score: needs to be ok; shorter roll better
                        int at = line.IndexOf("off after ");
                        double roll = at >= 0 && double.TryParse(line.Substring(at + 10).Split(' ')[0], out double r) ? r : 99999;
                        if (ok) good.Add($"{lift:0.0}/{gear:0.00}:{roll:0}");
                        if (ok && roll < bestScore) { bestScore = roll; best = $"lift {lift:0.0}, gear {gear:0.00}: {line}"; }
                    }
                sb.AppendLine($"{name}: BEST {best ?? "none"} | ok: {string.Join(" ", good)}");
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../TestResults/sweep46.txt"), sb.ToString());
        }

        static string ReturnPlanTest()
        {
            var sys = CelestialBody.CreateSystem();
            var tal = sys.First(b => b.name == "Tal");
            var arden = tal.parent;
            double r = tal.radius + 20_000, vc = Math.Sqrt(tal.mu / r);
            var notes = new List<string>();
            foreach (double phase in new[] { 0.0, 2.1, 4.2 })
            {
                Vector3d pos = new Vector3d(Math.Cos(phase), 0, Math.Sin(phase)) * r, vel = new Vector3d(-Math.Sin(phase), 0, Math.Cos(phase)) * vc;
                double ut = 1000 + phase * 500;
                Check(ReturnPlanner.Plan(tal, pos, vel, ut, out double tb, out double dv, out double pe), $"no plan from phase {phase}");
                // check it by flying the predicted path from the burn
                var o = Orbit.FromState(pos, vel, tal.mu, ut);
                o.StateAt(tb, out var rb, out var vb);
                var path = Patcher.Predict(tal, rb, vb + vb.normalized * dv, tb, 2);
                Check(path.Count >= 2 && path[0].end == SegmentEnd.ExitSoi && path[0].next == arden, "the path doesn't leave Tal for Arden");
                double low = path[1].orbit.Periapsis - arden.radius;
                Check(low > arden.atmosphereHeight * 0.2 && low < arden.atmosphereHeight * 0.8, $"lowest point {low / 1000:0} km (air to {arden.atmosphereHeight / 1000:0} km)");
                Check(dv > 0 && dv < 1500, $"a {dv:0} m/s burn");
                Check(tb >= ut + 30 && tb < ut + o.Period + 120, $"the burn's {tb - ut:0} s away (orbit {o.Period:0} s)");
                notes.Add($"{dv:0} m/s in {tb - ut:0} s, down to {low / 1000:0} km");
            }
            // a lopsided orbit like the climb off Tal leaves (low point 10 km, high point 80 km): still a modest burn
            {
                double rp = tal.radius + 10_000, ra = tal.radius + 80_000, sma = (rp + ra) / 2;
                double vp = Math.Sqrt(tal.mu * (2 / rp - 1 / sma));
                Check(ReturnPlanner.Plan(tal, new Vector3d(rp, 0, 0), new Vector3d(0, 0, vp), 5000, out double tb, out double dv, out double pe), "no plan from a lopsided orbit");
                Check(dv < 900, $"a {dv:0} m/s burn from the lopsided orbit (the bug test's 1,120 m/s flung it toward the sun)");
                Check(pe > arden.atmosphereHeight * 0.15 && pe < arden.atmosphereHeight * 0.8, $"lopsided: lowest point {pe / 1000:0} km");
                notes.Add($"lopsided {dv:0} m/s, down to {pe / 1000:0} km");
            }
            return string.Join("; ", notes);
        }

        static string TalLanderToOrbit()
        {
            var b = Body;
            var sim = VesselSim.CreateOnPad(Presets.TalLander(), b, 0);
            var ap = new FlightAutopilot();
            double ut = 0;
            var log = new System.Text.StringBuilder();
            int steps = 0;
            while (steps++ < 200_000 && !sim.destroyed && ap.phase != FlightAutopilot.Phase.Done)
            {
                if (ap.Update(sim, ut)) { sim.ActivateNextStage(); log.Append($" stage{sim.nextStage}@{ut:F0}s/{sim.Altitude / 1000:F1}km"); }
                sim.Step(0.02, ut);
                ut += 0.02;
                if (steps % 1500 == 0) log.Append($" [{ut:F0}s {sim.Altitude / 1000:F1}km {sim.SurfaceRelVelocity.magnitude:F0}m/s thr{sim.throttle:F2} {ap.phase}]");
            }
            var o = Orbit.FromState(sim.pos, sim.vel, b.mu, ut);
            string res = $"{ap.phase}, Ap {(o.Apoapsis - b.radius) / 1000:F1} km, Pe {(o.Periapsis - b.radius) / 1000:F1} km at {ut:F0}s;{log}";
            Check(!sim.destroyed, sim.destroyReason + " | " + res);
            Check(ap.phase == FlightAutopilot.Phase.Done && o.Periapsis - b.radius > b.atmosphereHeight, res);
            return res;
        }

        static string ShuttleClimbs()
        {
            var b = Body;
            var d = Presets.SpaceShuttle();
            Vector3d up0 = LaunchSite.DirectionAt(b, 0, 0);
            var s = VesselSim.CreateLanded(d, b, up0, 0);
            s.throttle = 1;
            s.ActivateNextStage();
            double ut = 0;
            for (int i = 0; i < 3000 && !s.destroyed; i++)
            {
                s.steerDirection = s.pos.normalized;
                s.Step(0.02, ut);
                ut += 0.02;
            }
            Check(!s.destroyed, s.destroyReason);
            Check(s.Altitude > 8000, $"only {s.Altitude:0} m up after 60 s");
            return $"{s.Altitude / 1000:0.0} km up after 60 s at {s.SurfaceRelVelocity.magnitude:0} m/s";
        }
    }
}
