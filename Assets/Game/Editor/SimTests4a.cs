using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 4a tests: the persistent universe, background (on-rails) motion, closest approach, rendezvous.</summary>
    public static partial class SimTests
    {
        static void RunMilestone4a()
        {
            Run("Universe: save and load keeps every vessel exactly (parts, fuel, staging, orbit, landing spot)", UniverseRoundTrip);
            Run("Unloaded vessels coast on exact orbits and change SOI on their own", BackgroundRails);
            Run("Unloaded: landed vessels ride the surface; hitting air under warp loses the vessel", BackgroundEdges);
            Run("Closest approach between two vessels' orbits matches brute force", ClosestApproachVessels);
            Run("Rendezvous: a planned one-burn transfer meets the target, and flying it agrees", RendezvousPlan);
            Run("Universe ids stay unique as stages break off", UniverseIds);
            Run("A maneuver node several orbits ahead stays valid", NodeLapsAhead);
        }

        static string NodeLapsAhead()
        {
            var home = System4[0];
            var s = InOrbit(D("probe", "probe.spark"), home, home.radius + 90_000, 0);
            var traj = Patcher.Predict(home, s.pos, s.vel, 0, 3);
            var o = traj[0].orbit;
            var n = new ManeuverNode { ut = 3.5 * o.Period, prograde = 20 };
            n.Update(traj);
            Check(n.valid, "a node 3.5 orbits ahead was dropped");
            o.StateAt(n.ut, out var r, out _);
            Check((n.pos - r).magnitude < 1e-3, "the node isn't on the orbit");
            return $"node at T+{Ui.Time(n.ut)} is valid; planned apoapsis {(n.planned[0].orbit.Apoapsis - home.radius) / 1000:F1} km";
        }

        static readonly List<CelestialBody> System4 = CelestialBody.CreateSystem();

        /// <summary>A vessel in an equatorial orbit of radius r, 'phase' radians ahead of +X (orbits head toward -Z, eastward).</summary>
        static VesselSim InOrbit(VesselDesign d, CelestialBody b, double r, double phase, double speedScale = 1)
        {
            double v = Math.Sqrt(b.mu / r) * speedScale;
            var pos = new Vector3d(r * Math.Cos(phase), 0, -r * Math.Sin(phase));
            var vel = new Vector3d(-v * Math.Sin(phase), 0, -v * Math.Cos(phase));
            return VesselSim.CreateFree(d, b, pos, vel, Quaternion.LookRotation(Vector3.forward, Vector3.right));
        }

        static string UniverseRoundTrip()
        {
            var home = System4[0];
            var u = new Universe { ut = 1234.5 };
            var pad = u.Add(VesselSim.CreateOnPad(VesselDesign.Default(), home, u.ut));
            var orb = u.Add(InOrbit(VesselDesign.Broadside(), home, home.radius + 90_000, 0.7));
            orb.ActivateNextStage();
            foreach (var piece in orb.ActivateNextStage()) u.Add(piece);
            var tank = orb.parts.First(p => p.def.kind == PartKind.Tank);
            tank.propellant *= 0.37;
            orb.RecomputeMass();
            orb.gearDown = true; orb.sas = SasMode.Prograde;
            var plane = u.Add(VesselSim.CreateOnRunway(VesselDesign.Kite(), home, u.ut));

            string json = u.ToJson();
            var u2 = new Universe();
            u2.LoadJson(json, System4);
            Check(u2.vessels.Count == u.vessels.Count, $"{u2.vessels.Count} vessels came back, {u.vessels.Count} were saved");
            Check(Math.Abs(u2.ut - u.ut) < 1e-9, "universe time changed");
            foreach (var a in u.vessels)
            {
                var b = u2.Find(a.id);
                Check(b != null, $"{a.name} (id {a.id}) is missing");
                Check(b.name == a.name && b.body == a.body && b.landed == a.landed && b.isDebris == a.isDebris, $"{a.name}: identity/situation changed");
                Check(b.parts.Count == a.parts.Count && b.nextStage == a.nextStage, $"{a.name}: {b.parts.Count} parts / stage {b.nextStage}, was {a.parts.Count} / {a.nextStage}");
                for (int i = 0; i < a.parts.Count; i++)
                {
                    var pa = a.parts[i]; var pb = b.parts[i];
                    Check(pa.def == pb.def && Math.Abs(pa.propellant - pb.propellant) < 1e-9 && pa.ignited == pb.ignited && pa.index == pb.index,
                        $"{a.name}: part {i} ({pa.def.id}) changed");
                }
                Check((a.pos - b.pos).magnitude < 1e-6 && (a.vel - b.vel).magnitude < 1e-9, $"{a.name}: state moved {(a.pos - b.pos).magnitude:E2} m");
                Check(Math.Abs(a.mass - b.mass) < 1e-9, $"{a.name}: mass {b.mass:F4} vs {a.mass:F4}");
                Check(b.sas == a.sas && b.gearDown == a.gearDown, $"{a.name}: switches changed");
            }
            // Both copies keep moving identically.
            var b2 = u2.Find(orb.id);
            orb.BackgroundAdvance(1000, u.ut, false);
            b2.BackgroundAdvance(1000, u.ut, false);
            var p2 = u2.Find(pad.id);
            pad.BackgroundAdvance(1000, u.ut, false);
            p2.BackgroundAdvance(1000, u.ut, false);
            Check((orb.pos - b2.pos).magnitude < 1e-3, "orbits diverged after loading");
            Check((pad.pos - p2.pos).magnitude < 1e-3, "landed vessel moved differently after loading");
            return $"{u.vessels.Count} vessels ({u.vessels.Count(v => v.isDebris)} debris), {json.Length / 1024.0:F1} KB of JSON, identical after 1000 s";
        }

        static string BackgroundRails()
        {
            var home = System4[0];
            var probe = InOrbit(D("probe", "probe.spark", "tank.s1", "eng.ember"), home, home.radius + 150_000, 0.3, 1.05);
            var o = Orbit.FromState(probe.pos, probe.vel, home.mu, 0);
            Vector3d start = probe.pos;
            double t = 0;
            while (t < o.Period - 1e-9) { double dt = Math.Min(7.3, o.Period - t); probe.BackgroundAdvance(dt, t, false); t += dt; }
            double err = (probe.pos - start).magnitude;
            Check(err < 0.5, $"one orbit later it's {err:F3} m from where it started");
            Check(!probe.awake && !probe.destroyed, "an orbit above the air shouldn't wake or lose it");

            // Moon transfer while unloaded: it should end up in Tal's SOI by itself.
            var moon = System4[1];
            var leo = InOrbit(D("transfer", "probe.spark", "tank.s1", "eng.ember"), home, home.radius + 100_000, 0);
            Check(Patcher.FindTransfer(home, leo.pos, leo.vel, 0, moon, 60_000, out double burnUt, out double dv, out _), "no transfer found");
            t = 0;
            while (t < burnUt) { double dt = Math.Min(50, burnUt - t); leo.BackgroundAdvance(dt, t, false); t += dt; }
            leo.vel = leo.vel + leo.vel.normalized * dv;
            leo.rails = null;
            double tEnter = double.NaN;
            for (int i = 0; i < 40_000 && leo.body == home; i++) { leo.BackgroundAdvance(30, t, true); t += 30; }
            if (leo.body == moon) tEnter = t;
            Check(!double.IsNaN(tEnter), $"never reached Tal (still around {leo.body.name}, destroyed={leo.destroyed} {leo.destroyReason})");
            return $"orbit closure error {err:F4} m; unloaded transfer entered Tal's SOI {Ui.Time(tEnter - burnUt)} after the burn";
        }

        static string BackgroundEdges()
        {
            var home = System4[0];
            var pad = VesselSim.CreateOnPad(VesselDesign.Default(), home, 0);
            Vector3d fixed0 = home.InertialToFixed(pad.pos, 0);
            for (int i = 0; i < 36; i++) pad.BackgroundAdvance(100, i * 100, true);
            double moved = (home.InertialToFixed(pad.pos, 3600) - fixed0).magnitude;
            Check(moved < 1e-3 && pad.landed && !pad.destroyed, $"landed vessel drifted {moved:F4} m on the ground");

            // Periapsis inside the air: under warp it's lost; at normal speed it wakes up for full physics instead.
            VesselSim Dipper() => InOrbit(D("dipper", "probe.spark", "tank.s1"), home, home.radius + 90_000, 0, 0.96);
            var a = Dipper();
            double t = 0;
            for (int i = 0; i < 400 && !a.destroyed; i++) { a.BackgroundAdvance(20, t, true); t += 20; }
            Check(a.destroyed, "a vessel dipping into the air under warp wasn't lost");
            var b = Dipper();
            t = 0;
            for (int i = 0; i < 400 && !b.awake; i++) { b.BackgroundAdvance(20, t, false); t += 20; }
            Check(b.awake && !b.destroyed, "without warp it should wake up for physics, not vanish");
            Check(Math.Abs(b.Altitude - home.atmosphereHeight) < 50, $"woke up at {b.Altitude:F0} m, the air starts at {home.atmosphereHeight:F0} m");
            return $"landed drift {moved:E1} m over an hour; warp: \"{a.destroyReason}\"; no warp: woke at {b.Altitude / 1000:F1} km";
        }

        static string ClosestApproachVessels()
        {
            var home = System4[0];
            var chaser = InOrbit(D("chaser", "probe.spark"), home, home.radius + 80_000, 0);
            var target = InOrbit(D("target", "probe.spark"), home, home.radius + 95_000, 0.9, 1.02);
            var oc = Orbit.FromState(chaser.pos, chaser.vel, home.mu, 0);
            var ot = Orbit.FromState(target.pos, target.vel, home.mu, 0);
            var seg = new TrajectorySegment { body = home, orbit = oc, tStart = 0, tEnd = oc.Period, end = SegmentEnd.None };
            double span = 2.5 * Math.Max(oc.Period, ot.Period);
            Check(Patcher.ClosestApproach(seg, home, ot, 0, span, out double d, out double tc, out double rel), "closest approach refused");
            double best = double.PositiveInfinity, bestT = 0;
            for (double t = 0; t <= span; t += 0.25)
            {
                oc.StateAt(t, out var a, out _);
                ot.StateAt(t, out var b, out _);
                double dd = (a - b).magnitude;
                if (dd < best) { best = dd; bestT = t; }
            }
            Check(Math.Abs(d - best) < Math.Max(20, best * 0.002), $"found {d:F0} m at {tc:F0} s, brute force {best:F0} m at {bestT:F0} s");
            return $"closest {d / 1000:F2} km at T+{Ui.Time(tc)} (brute force {best / 1000:F2} km at T+{Ui.Time(bestT)}), relative speed {rel:F1} m/s";
        }

        static string RendezvousPlan()
        {
            var home = System4[0];
            var chaser = InOrbit(D("chaser", "probe.spark"), home, home.radius + 80_000, 0);
            var target = InOrbit(D("station", "probe.spark"), home, home.radius + 120_000, 2.0);
            var ot = Orbit.FromState(target.pos, target.vel, home.mu, 0);
            var oc = Orbit.FromState(chaser.pos, chaser.vel, home.mu, 0);
            double wait = Patcher.SynodicPeriod(oc, ot); // the transfer window comes round once per synodic period
            Check(Patcher.FindRendezvous(home, chaser.pos, chaser.vel, 0, ot, wait, out double burnUt, out double dv, out double closest, out double tc),
                "no rendezvous found");
            Check(closest < 2000, $"best plan only gets within {closest:F0} m");
            // Fly it: coast to the burn on rails, burn prograde (impulse), coast to the closest approach.
            double t = 0;
            while (t < burnUt - 1e-9) { double dt = Math.Min(20, burnUt - t); chaser.BackgroundAdvance(dt, t, false); target.BackgroundAdvance(dt, t, false); t += dt; }
            chaser.vel = chaser.vel + chaser.vel.normalized * dv;
            chaser.rails = null;
            double flown = double.PositiveInfinity, rel = 0;
            while (t < tc + 300)
            {
                chaser.BackgroundAdvance(1, t, false); target.BackgroundAdvance(1, t, false); t += 1;
                double d = (chaser.pos - target.pos).magnitude;
                if (d < flown) { flown = d; rel = (chaser.vel - target.vel).magnitude; }
            }
            Check(Math.Abs(flown - closest) < 0.1 * closest + 150, $"flew to {flown:F0} m, the plan said {closest:F0} m");
            double hohmann = Math.Sqrt(home.mu / oc.a) * (Math.Sqrt(2 * ot.a / (oc.a + ot.a)) - 1);
            Check(Math.Abs(dv - hohmann) < 15, $"burn {dv:F1} m/s vs Hohmann {hohmann:F1} m/s");
            return $"burn {dv:F1} m/s (Hohmann {hohmann:F1}) in {Ui.Time(burnUt)}; planned closest {closest:F0} m, flown {flown:F0} m at {rel:F0} m/s relative";
        }

        static string UniverseIds()
        {
            var home = System4[0];
            var u = new Universe();
            var a = u.Add(VesselSim.CreateOnPad(VesselDesign.Broadside(), home, 0));
            var b = u.Add(VesselSim.CreateOnPad(VesselDesign.Default(), home, 0));
            for (int k = 0; k < 6 && a.HasStagesLeft; k++) foreach (var piece in a.ActivateNextStage()) u.Add(piece);
            var ids = u.vessels.Select(v => v.id).ToList();
            Check(ids.Distinct().Count() == ids.Count, "duplicate ids");
            Check(u.vessels.Count >= 4, $"only {u.vessels.Count} vessels after staging");
            var json = u.ToJson();
            var u2 = new Universe();
            u2.LoadJson(json, System4);
            var c = u2.Add(VesselSim.CreateOnPad(VesselDesign.Default(), home, 0));
            Check(!u2.vessels.Any(v => v != c && v.id == c.id), "a new vessel reused an id after loading");
            return $"{u.vessels.Count} vessels, ids {string.Join(",", ids)}; next id after reload {c.id}";
        }
    }
}
