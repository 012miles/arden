using System;
using System.Collections.Generic;

namespace Rocket
{
    /// <summary>
    /// Lambert's problem: the Kepler orbit that goes from r1 to r2 in a given time. Universal-variable form with a
    /// bisection on z (zero revolutions), which is slow but never fails to converge.
    /// </summary>
    public static class Lambert
    {
        static double C(double z) => z > 1e-8 ? (1 - Math.Cos(Math.Sqrt(z))) / z : z < -1e-8 ? (Math.Cosh(Math.Sqrt(-z)) - 1) / -z : 0.5 - z / 24;
        static double S(double z)
        {
            if (z > 1e-8) { double s = Math.Sqrt(z); return (s - Math.Sin(s)) / (s * s * s); }
            if (z < -1e-8) { double s = Math.Sqrt(-z); return (Math.Sinh(s) - s) / (s * s * s); }
            return 1.0 / 6 - z / 120;
        }

        /// <summary>
        /// Velocities at r1 (leaving) and r2 (arriving) for a flight time 'tof', going round the same way as the
        /// reference angular momentum 'normal' (so a transfer never runs backwards against the orbit it starts from).
        /// </summary>
        public static bool Solve(Vector3d r1, Vector3d r2, double tof, double mu, Vector3d normal, out Vector3d v1, out Vector3d v2)
        {
            v1 = v2 = Vector3d.zero;
            double R1 = r1.magnitude, R2 = r2.magnitude;
            if (tof <= 0 || R1 < 1 || R2 < 1) return false;
            double cosd = Math.Max(-1, Math.Min(1, Vector3d.Dot(r1, r2) / (R1 * R2)));
            double dnu = Math.Acos(cosd);
            if (Vector3d.Dot(Vector3d.Cross(r1, r2), normal) < 0) dnu = 2 * Math.PI - dnu;
            double A = Math.Sin(dnu) * Math.Sqrt(R1 * R2 / (1 - Math.Cos(dnu)));
            if (Math.Abs(A) < 1e-6 || double.IsNaN(A)) return false;
            double sqmu = Math.Sqrt(mu);
            double Y(double z) => R1 + R2 + A * (z * S(z) - 1) / Math.Sqrt(C(z));
            double lo = -600, hi = 4 * Math.PI * Math.PI - 1e-7;
            for (int i = 0; i < 200; i++)
            {
                double mid = (lo + hi) / 2, y = Y(mid);
                if (y < 0) { lo = mid; continue; }
                double x = Math.Sqrt(y / C(mid));
                double t = (x * x * x * S(mid) + A * Math.Sqrt(y)) / sqmu;
                if (t < tof) lo = mid; else hi = mid;
            }
            double z0 = (lo + hi) / 2, y0 = Y(z0);
            if (y0 <= 0 || double.IsNaN(y0)) return false;
            double f = 1 - y0 / R1, g = A * Math.Sqrt(y0 / mu), gdot = 1 - y0 / R2;
            if (Math.Abs(g) < 1e-9) return false;
            v1 = (r2 - r1 * f) / g;
            v2 = (r2 * gdot - r1) / g;
            // The bisection can stall against the zero-revolution limit: check the answer flies where it should.
            var o = Orbit.FromState(r1, v1, mu, 0);
            o.StateAt(tof, out var rEnd, out _);
            return (rEnd - r2).magnitude < Math.Max(50, R2 * 1e-4);
        }
    }

    /// <summary>A planned intercept: one burn to get there, then one at arrival to match the target's speed.</summary>
    public struct InterceptPlan
    {
        public double burnUt, arriveUt;
        public Vector3d dv1, dv2;        // inertial burn vectors
        public double Total => dv1.magnitude + dv2.magnitude;
        public double lowest;            // the transfer's lowest point above the surface (m)
    }

    public static class Intercept
    {
        /// <summary>
        /// The cheapest intercept of a target on a Kepler path around the same body: searches burn times over 'window'
        /// seconds and flight times between a tenth and one and a half of my orbit (or up to the target's own pass),
        /// keeps transfers that stay above the air, then fine-tunes. Total cost = the burn there plus matching speed.
        /// maxArrive limits where the meeting may be (a wanderer is worth catching near its closest pass, where capture is cheap).
        /// </summary>
        public static bool Plan(CelestialBody body, Vector3d pos, Vector3d vel, double ut, Orbit target, double window, out InterceptPlan plan,
            double maxArrive = double.PositiveInfinity)
        {
            plan = default;
            var me = Orbit.FromState(pos, vel, body.mu, ut);
            Vector3d normal = Vector3d.Cross(pos, vel).normalized;
            double period = me.IsElliptic ? me.Period : 3600;
            double floor = body.radius + body.SafeAltitude + 10_000;
            double tofMin = period * 0.1, tofMax = period * 1.5;
            if (!target.IsElliptic) tofMax = Math.Max(tofMax, Math.Min(window, 3 * 86400));

            double Cost(double tb, double tof, out InterceptPlan p)
            {
                p = default;
                me.StateAt(tb, out var r1, out var vm);
                target.StateAt(tb + tof, out var r2, out var vt);
                if (r2.magnitude > Math.Min(body.SoiRadius * 0.98, maxArrive)) return double.PositiveInfinity;
                if (!Lambert.Solve(r1, r2, tof, body.mu, normal, out var v1, out var v2)) return double.PositiveInfinity;
                // Reject transfers that dip into the air or the ground on the way.
                var tr = Orbit.FromState(r1, v1, body.mu, tb);
                double low = Math.Min(r1.magnitude, r2.magnitude);
                bool passesPe = Vector3d.Dot(r1, v1) < 0 && Vector3d.Dot(r2, v2) > 0;
                if (passesPe) low = Math.Min(low, tr.Periapsis);
                if (low < floor) return double.PositiveInfinity;
                p = new InterceptPlan { burnUt = tb, arriveUt = tb + tof, dv1 = v1 - vm, dv2 = vt - v2, lowest = low - body.radius };
                return p.Total;
            }

            double best = double.PositiveInfinity, bTb = 0, bTof = 0;
            const int nb = 72, nt = 48;
            for (int i = 0; i <= nb; i++)
            {
                double tb = ut + 60 + window * i / nb;
                for (int j = 0; j <= nt; j++)
                {
                    double tof = tofMin * Math.Pow(tofMax / tofMin, (double)j / nt);
                    double c = Cost(tb, tof, out _);
                    if (c < best) { best = c; bTb = tb; bTof = tof; }
                }
            }
            if (double.IsInfinity(best)) return false;
            double sb = window / nb, st = bTof * 0.08;
            for (int round = 0; round < 8; round++)
            {
                bool improved = true;
                for (int guard = 0; improved && guard < 60; guard++)
                {
                    improved = false;
                    foreach (var (db, dt) in new[] { (sb, 0.0), (-sb, 0.0), (0.0, st), (0.0, -st), (sb, -st), (-sb, st) })
                    {
                        double tb = bTb + db, tof = bTof + dt;
                        if (tb < ut + 30 || tof < 60) continue;
                        double c = Cost(tb, tof, out _);
                        if (c < best - 1e-6) { best = c; bTb = tb; bTof = tof; improved = true; }
                    }
                }
                sb /= 3; st /= 3;
            }
            Cost(bTb, bTof, out plan);
            return true;
        }

        /// <summary>Where a wanderer is worth meeting: within twice its closest-pass distance (or 2,000 km beyond it).</summary>
        public static double WandererMeetLimit(Orbit o) => Math.Max(o.Periapsis * 2, o.Periapsis + 2_000_000);

        /// <summary>A burn vector as maneuver node components (prograde, normal, radial) at the given state.</summary>
        public static void ToNode(Vector3d r, Vector3d v, Vector3d dv, out double prograde, out double normal, out double radial)
        {
            ManeuverNode.Frame(r, v, out var pro, out var nor, out var rad);
            prograde = Vector3d.Dot(dv, pro);
            normal = Vector3d.Dot(dv, nor);
            radial = Vector3d.Dot(dv, rad);
        }
    }

    /// <summary>Something coming up that the player may want to warp to.</summary>
    public struct FlightEvent
    {
        public string title;
        public double ut;
        public int kind;     // 0 node, 1 apsis, 2 SOI, 3 target, 4 wanderer, 5 window
        public FlightEvent(string title, double ut, int kind) { this.title = title; this.ut = ut; this.kind = kind; }
    }

    public static class FlightEvents
    {
        /// <summary>
        /// The upcoming events for a vessel, soonest first: the maneuver node, the next apoapsis and periapsis, sphere
        /// of influence changes along the path, the closest approach to a target, each wanderer's closest pass, and the
        /// next transfer window to the other planets.
        /// </summary>
        public static List<FlightEvent> Build(VesselSim v, List<TrajectorySegment> path, ManeuverNode node, string targetName, double targetUt, double targetDist,
            IEnumerable<VesselSim> others, IList<CelestialBody> bodies, double ut, IList<ManeuverNode> later = null)
        {
            var list = new List<FlightEvent>();
            string D(double m) => m >= 1e9 ? $"{m / 1e9:0.00} Gm" : m >= 1e7 ? $"{m / 1e6:0} Mm" : m >= 1e4 ? $"{m / 1000:0} km" : $"{m:0} m";
            if (node != null && node.valid) list.Add(new FlightEvent($"Maneuver ({node.DeltaV:0} m/s)", node.ut, 0));
            if (later != null) foreach (var n in later) if (n.valid) list.Add(new FlightEvent($"Next burn ({n.DeltaV:0} m/s)", n.ut, 6));
            if (path != null && path.Count > 0 && !v.landed)
            {
                var o = path[0].orbit;
                var b = path[0].body;
                if (o.IsElliptic && o.Apoapsis < b.SoiRadius)
                {
                    double ta = ut + o.TimeToApoapsis(ut), tp = ut + o.TimeToPeriapsis(ut);
                    if (ta < path[0].tEnd || path[0].end == SegmentEnd.None) list.Add(new FlightEvent($"Apoapsis {D(o.Apoapsis - b.radius)}", ta, 1));
                    if (o.Periapsis > b.radius && (tp < path[0].tEnd || path[0].end == SegmentEnd.None)) list.Add(new FlightEvent($"Periapsis {D(o.Periapsis - b.radius)}", tp, 1));
                }
                else if (!o.IsElliptic)
                {
                    double tp = o.TimeToPeriapsis(ut);
                    if (!double.IsNaN(tp) && tp > 0 && o.Periapsis > b.radius) list.Add(new FlightEvent($"Closest to {b.name}: {D(o.Periapsis - b.radius)}", ut + tp, 1));
                }
                for (int i = 0; i < path.Count; i++)
                {
                    var s = path[i];
                    if (s.end == SegmentEnd.EnterSoi && i + 1 < path.Count) list.Add(new FlightEvent($"Enter {path[i + 1].body.name}'s sphere of influence", s.tEnd, 2));
                    else if (s.end == SegmentEnd.ExitSoi) list.Add(new FlightEvent($"Leave {s.body.name}'s sphere of influence", s.tEnd, 2));
                    else if (s.end == SegmentEnd.Impact) list.Add(new FlightEvent(s.body.HasAtmosphere ? $"Into {s.body.name}'s air" : $"Hits {s.body.name}", s.tEnd, 2));
                }
            }
            if (targetName != null && !double.IsNaN(targetUt) && targetUt > ut) list.Add(new FlightEvent($"Closest to {targetName}: {D(targetDist)}", targetUt, 3));
            if (others != null)
                foreach (var w in others)
                {
                    if (!w.IsRock || w.destroyed || w.landed) continue;
                    var o = Orbit.FromState(w.pos, w.vel, w.body.mu, ut);
                    double tp = o.TimeToPeriapsis(ut);
                    if (!double.IsNaN(tp) && tp > 0 && (!o.IsElliptic || tp < o.Period)) list.Add(new FlightEvent($"{w.name} passes {w.body.name} at {D(o.Periapsis - w.body.radius)}", ut + tp, 4));
                }
            if (bodies != null && v.body.IsHomeWorld)
                foreach (var p in bodies)
                    if (p.IsPlanet && !p.IsHomeWorld && !p.isStar)
                    {
                        double t = TransferPlanner.NextWindow(v.body, p, ut);
                        if (!double.IsNaN(t) && t > ut) list.Add(new FlightEvent($"Transfer window to {p.name}", t, 5));
                    }
            if (v.UsesSupplies)
            {
                if (v.starvingSince >= 0) list.Add(new FlightEvent("The crew die without supplies", v.starvingSince + Supplies.Grace, 7));
                else { double d = v.SupplyDaysLeft(); if (!double.IsInfinity(d)) list.Add(new FlightEvent($"Supplies run out ({v.CrewCount} crew)", ut + d * Supplies.Day, 7)); }
            }
            list.RemoveAll(e => e.ut <= ut + 1 || double.IsNaN(e.ut));
            list.Sort((a, b) => a.ut.CompareTo(b.ut));
            return list;
        }

        /// <summary>How long before an event a warp should stop: the node's half burn plus a margin, a minute otherwise.</summary>
        public static double Lead(FlightEvent e, double halfBurn) => e.kind == 0 ? halfBurn + 20 : e.kind == 1 ? 15 : e.kind == 6 ? 120 : 60;
    }
}
