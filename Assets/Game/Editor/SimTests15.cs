using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 15 tests: Lambert's problem, intercept planning, and the events list.</summary>
    public static partial class SimTests
    {
        static void RunMilestone15()
        {
            Run("Lambert: recovers the velocities of known orbits (ellipses, a hyperbola, the long way round)", LambertKnown);
            Run("Intercept: from a low orbit to a higher, tilted one; flown with the two burns it meets and matches speed", InterceptOrbit);
            Run("Intercept: catching a wanderer on its pass, staying above the air", InterceptWanderer);
            Run("Events list: node, apsides, target, wanderer pass and transfer windows, soonest first", EventsList);
            Run("Events list: a moon transfer shows the sphere-of-influence change", EventsSoi);
        }

        static string LambertKnown()
        {
            var home = Home14;
            double mu = home.mu, worst = 0;
            var cases = new List<(Vector3d r, Vector3d v, double tof, string what)>
            {
                (new Vector3d(home.radius + 300_000, 0, 0), new Vector3d(0, 0, -Math.Sqrt(mu / (home.radius + 300_000))), 1200, "circular, a third of a lap"),
                (new Vector3d(home.radius + 200_000, 0, 0), new Vector3d(0, 300, -Math.Sqrt(mu / (home.radius + 200_000)) * 1.15), 2600, "tilted ellipse"),
                (new Vector3d(home.radius + 500_000, 0, 0), new Vector3d(0, 0, -Math.Sqrt(2 * mu / (home.radius + 500_000)) * 1.2), 4000, "hyperbola"),
                (new Vector3d(home.radius + 300_000, 0, 0), new Vector3d(0, 0, -Math.Sqrt(mu / (home.radius + 300_000)) * 1.05), 2200, "two thirds of a lap (long way)"),
            };
            foreach (var (r, v, tof, what) in cases)
            {
                var o = Orbit.FromState(r, v, mu, 0);
                o.StateAt(tof, out var r2, out var v2);
                bool ok = Lambert.Solve(r, r2, tof, mu, Vector3d.Cross(r, v), out var a1, out var a2);
                Check(ok, $"{what}: no solution");
                double e1 = (a1 - v).magnitude, e2 = (a2 - v2).magnitude;
                Check(e1 < 0.05 && e2 < 0.05, $"{what}: velocity off by {e1:0.000} / {e2:0.000} m/s");
                worst = Math.Max(worst, Math.Max(e1, e2));
            }
            return $"4 cases, worst velocity error {worst * 1000:0.0} mm/s";
        }

        /// <summary>Flies an intercept plan with instant burns: returns the miss distance and the speed left over after matching.</summary>
        static (double miss, double rel) FlyPlan(CelestialBody b, Vector3d pos, Vector3d vel, double ut, Orbit target, InterceptPlan p)
        {
            var me = Orbit.FromState(pos, vel, b.mu, ut);
            me.StateAt(p.burnUt, out var r1, out var v1);
            var tr = Orbit.FromState(r1, v1 + p.dv1, b.mu, p.burnUt);
            tr.StateAt(p.arriveUt, out var ra, out var va);
            target.StateAt(p.arriveUt, out var rt, out var vt);
            return ((ra - rt).magnitude, (va + p.dv2 - vt).magnitude);
        }

        static string InterceptOrbit()
        {
            var home = Home14;
            double r0 = home.radius + 300_000, v0 = Math.Sqrt(home.mu / r0);
            var pos = new Vector3d(r0, 0, 0);
            var vel = new Vector3d(0, 0, -v0);
            // Target: a 600 km circle tilted 3 degrees, 120 degrees ahead
            double r1 = home.radius + 600_000, v1 = Math.Sqrt(home.mu / r1), ph = 120 * Math.PI / 180, inc = 3 * Math.PI / 180;
            var tp = new Vector3d(r1 * Math.Cos(ph), 0, -r1 * Math.Sin(ph));
            var tv = new Vector3d(-v1 * Math.Sin(ph), 0, -v1 * Math.Cos(ph));
            var tilt = Quaternion.AngleAxis((float)(inc * 180 / Math.PI), Vector3.right);
            tp = Vector3d.Rotate(tilt, tp); tv = Vector3d.Rotate(tilt, tv);
            var target = Orbit.FromState(tp, tv, home.mu, 0);
            var me = Orbit.FromState(pos, vel, home.mu, 0);
            bool ok = Intercept.Plan(home, pos, vel, 0, target, Math.Max(me.Period * 2, Patcher.SynodicPeriod(me, target)), out var p);   // as the game searches
            Check(ok, "no plan");
            var (miss, rel) = FlyPlan(home, pos, vel, 0, target, p);
            Check(miss < 2000, $"missed by {miss:0} m");
            Check(rel < 0.5, $"{rel:0.00} m/s left after matching");
            // A Hohmann transfer alone is ~270 m/s here (both burns); the 3 degree tilt adds 100-200 m/s depending on where
            // the burns fall relative to the line where the two orbits cross.
            Check(p.Total < 500, $"{p.Total:0} m/s in all");
            Check(p.lowest > home.SafeAltitude, $"dips to {p.lowest / 1000:0} km");
            return $"burn {p.dv1.magnitude:0} m/s in {Ui.Time(p.burnUt)}, arrive {Ui.Time(p.arriveUt - p.burnUt)} later, match {p.dv2.magnitude:0} m/s ({p.Total:0} in all); miss {miss:0} m, {rel * 100:0} cm/s left";
        }

        static string InterceptWanderer()
        {
            var home = Home14;
            double r0 = home.radius + 300_000, v0 = Math.Sqrt(home.mu / r0);
            var pos = new Vector3d(r0, 0, 0);
            var vel = new Vector3d(0, 0, -v0);
            Wanderers.Path(home, home.radius + 900_000, 300, 4, 10, 70, out var wp, out var wv, out double toPeri);
            // Start the clock half a day before its closest pass
            var w0 = Orbit.FromState(wp, wv, home.mu, 0);
            double ut = toPeri - 43_200;
            var target = Orbit.FromState(wp, wv, home.mu, 0);
            double limit = Intercept.WandererMeetLimit(target);
            bool ok = Intercept.Plan(home, pos, vel, ut, target, 43_200 + 6 * 3600, out var p, limit);
            // (my orbit's state at 'ut' is the one given: the plan treats pos/vel as now)
            Check(ok, "no plan to catch the wanderer");
            var (miss, rel) = FlyPlan(home, pos, vel, ut, target, p);
            Check(miss < 3000, $"missed by {miss:0} m");
            Check(rel < 0.5, $"{rel:0.00} m/s left after matching");
            Check(p.lowest > home.SafeAltitude, $"dips to {p.lowest / 1000:0} km");
            target.StateAt(p.arriveUt, out var ra, out _);
            Check(p.Total < 3900, $"{p.Total:0} m/s: more than the Wrangler tug carries");
            Check(ra.magnitude <= limit + 1, $"met it {(ra.magnitude - home.radius) / 1000:0} km up, beyond the {(limit - home.radius) / 1000:0} km limit");
            return $"{p.dv1.magnitude:0} + {p.dv2.magnitude:0} = {p.Total:0} m/s, meeting it {(ra.magnitude - home.radius) / 1000:0} km up, {Ui.Time(p.arriveUt - toPeri)} from its closest pass; miss {miss:0} m";
        }

        static string EventsList()
        {
            var home = Home14;
            double r0 = home.radius + 300_000, v0 = Math.Sqrt(home.mu / r0);
            var v = VesselSim.CreateFree(VesselDesign.WranglerTug(), home, new Vector3d(r0, 0, 0), new Vector3d(0, 0, -v0 * 1.05), Quaternion.identity);
            var path = Patcher.Predict(home, v.pos, v.vel, 0, 3);
            var node = new ManeuverNode { ut = 900, prograde = 50 };
            node.Update(path);
            Check(node.valid, "the node didn't take");
            var rock = Wanderers.Spawn(home, 0, new System.Random(3), 1);
            var list = FlightEvents.Build(v, path, node, "Outpost", 2500, 1234, new[] { v, rock }, System4, 0);
            string Names() => string.Join(" | ", list.Select(e => $"{e.title} @{e.ut:0}"));
            Check(list.Count >= 5, "too few events: " + Names());
            for (int i = 1; i < list.Count; i++) Check(list[i].ut >= list[i - 1].ut, "not soonest first: " + Names());
            Check(list.Exists(e => e.kind == 0 && Math.Abs(e.ut - 900) < 1), "no node: " + Names());
            Check(list.Exists(e => e.title.StartsWith("Apoapsis")) && list.Exists(e => e.title.StartsWith("Periapsis")), "no apsides: " + Names());
            Check(list.Exists(e => e.kind == 3 && e.title.Contains("Outpost")), "no target approach: " + Names());
            Check(list.Exists(e => e.kind == 4 && e.title.Contains(rock.name)), "no wanderer pass: " + Names());
            Check(list.Exists(e => e.kind == 5 && e.title.Contains("Ochre")), "no transfer window: " + Names());
            Check(list.TrueForAll(e => e.ut > 1), "an event in the past");
            Check(FlightEvents.Lead(list.Find(e => e.kind == 0), 40) == 60 && FlightEvents.Lead(list.Find(e => e.kind == 3), 0) == 60, "warp leads");
            return Names();
        }

        static string EventsSoi()
        {
            var home = Home14;
            var tal = System4[1];
            double r0 = home.radius + 100_000, v0 = Math.Sqrt(home.mu / r0);
            var pos = new Vector3d(r0, 0, 0);
            var vel = new Vector3d(0, 0, -v0);
            bool found = Patcher.FindTransfer(home, pos, vel, 0, tal, 60_000, out double burnUt, out double dv, out _);
            Check(found, "no Tal transfer");
            var me = Orbit.FromState(pos, vel, home.mu, 0);
            me.StateAt(burnUt, out var rb, out var vb);
            var path = Patcher.Predict(home, rb, vb + vb.normalized * dv, burnUt, 3);
            var v = VesselSim.CreateFree(VesselDesign.WranglerTug(), home, rb, vb + vb.normalized * dv, Quaternion.identity);
            var list = FlightEvents.Build(v, path, null, null, double.NaN, 0, new VesselSim[0], System4, burnUt);
            var soi = list.Find(e => e.kind == 2 && e.title.Contains("Tal"));
            Check(soi.title != null, "no SOI event: " + string.Join(" | ", list.Select(e => e.title)));
            Check(Math.Abs(soi.ut - path[0].tEnd) < 1, "SOI event at the wrong time");
            return $"{soi.title} in {Ui.Time(soi.ut - burnUt)}; {list.Count} events in all";
        }
    }
}
