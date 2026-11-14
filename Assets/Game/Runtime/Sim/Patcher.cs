using System;
using System.Collections.Generic;

namespace Rocket
{
    public enum SegmentEnd { None, ExitSoi, EnterSoi, Impact }

    /// <summary>One piece of a predicted path: a Kepler orbit around one body between two times.</summary>
    public class TrajectorySegment
    {
        public CelestialBody body;
        public Orbit orbit;
        public double tStart, tEnd;
        public SegmentEnd end;
        public CelestialBody next; // body after the SOI change
    }

    /// <summary>Patched-conic trajectory prediction: follows a path through SOI changes.</summary>
    public static class Patcher
    {
        /// <summary>Predicts up to maxSegments pieces starting from a state around 'body'.</summary>
        public static List<TrajectorySegment> Predict(CelestialBody body, Vector3d pos, Vector3d vel, double ut, int maxSegments = 3)
        {
            var segs = new List<TrajectorySegment>();
            for (int k = 0; k < maxSegments; k++)
            {
                var o = Orbit.FromState(pos, vel, body.mu, ut);
                double cap = body.isStar ? 3 * 365 * 86400.0 : 60 * 86400.0; // round the sun: show the whole orbit (or years of an escape)
                double horizon = o.IsElliptic ? Math.Min(o.Period, cap) : body.isStar ? cap : 120 * 86400.0;
                var seg = new TrajectorySegment { body = body, orbit = o, tStart = ut, tEnd = ut + horizon };
                FindEvent(body, o, ut, ut + horizon, out double tEv, out var kind, out var next);
                if (kind != SegmentEnd.None)
                {
                    seg.tEnd = tEv;
                    seg.end = kind;
                    seg.next = next;
                }
                segs.Add(seg);
                if (kind != SegmentEnd.ExitSoi && kind != SegmentEnd.EnterSoi) break;
                o.StateAt(tEv, out var r, out var v);
                if (kind == SegmentEnd.ExitSoi)
                {
                    pos = r + body.PositionAt(tEv);
                    vel = v + body.VelocityAt(tEv);
                }
                else
                {
                    pos = r - next.PositionAt(tEv);
                    vel = v - next.VelocityAt(tEv);
                }
                body = next;
                ut = tEv;
            }
            return segs;
        }

        /// <summary>First SOI exit, SOI entry or surface impact between t0 and t1.</summary>
        public static void FindEvent(CelestialBody body, Orbit o, double t0, double t1,
            out double tEvent, out SegmentEnd kind, out CelestialBody next)
        {
            tEvent = double.PositiveInfinity;
            kind = SegmentEnd.None;
            next = null;

            // Leaving the parent's SOI.
            if (body.parent != null && (!o.IsElliptic || o.Apoapsis > body.SoiRadius))
            {
                double soi = body.SoiRadius;
                if (FirstCrossing(t => o.RadiusAt(t) > soi, t0, t1, ExitStep(o), out double te) && te < tEvent)
                {
                    tEvent = te; kind = SegmentEnd.ExitSoi; next = body.parent;
                }
            }

            // Entering a moon's SOI. Step size is set so the path can't jump over the sphere.
            foreach (var c in body.children)
            {
                double soi = c.SoiRadius;
                double vmax = o.MaxSpeed + c.orbitRadius * c.OrbitalAngularVelocity;
                double tLimit = Math.Min(t1, tEvent);
                double t = t0;
                o.StateAt(t, out var r0, out _);
                if ((r0 - c.PositionAt(t)).magnitude < soi) continue; // already inside: handled elsewhere
                double tPrev = t;
                int guard = 0;
                while (t < tLimit && guard++ < 20000)
                {
                    o.StateAt(t, out var r, out _);
                    double d = (r - c.PositionAt(t)).magnitude;
                    if (d < soi)
                    {
                        double lo = tPrev, hi = t;
                        for (int i = 0; i < 60; i++)
                        {
                            double mid = (lo + hi) / 2;
                            o.StateAt(mid, out var rm, out _);
                            if ((rm - c.PositionAt(mid)).magnitude < soi) hi = mid; else lo = mid;
                        }
                        if (hi < tEvent) { tEvent = hi; kind = SegmentEnd.EnterSoi; next = c; }
                        break;
                    }
                    tPrev = t;
                    t += Math.Max(1.0, (d - soi) / vmax * 0.9);
                }
            }

            // Hitting the surface.
            if (o.Periapsis < body.radius)
            {
                if (FirstCrossing(t => o.RadiusAt(t) < body.radius, t0, Math.Min(t1, tEvent), ExitStep(o), out double ti) && ti < tEvent)
                {
                    tEvent = ti; kind = SegmentEnd.Impact; next = null;
                }
            }
        }

        static double ExitStep(Orbit o) => o.IsElliptic ? Math.Max(1, o.Period / 720) : 60;

        static bool FirstCrossing(Func<double, bool> inside, double t0, double t1, double step, out double t)
        {
            t = double.PositiveInfinity;
            if (inside(t0)) { t = t0; return true; }
            double prev = t0;
            int n = (int)Math.Min(20000, Math.Ceiling((t1 - t0) / step));
            for (int i = 1; i <= n; i++)
            {
                double cur = Math.Min(t1, t0 + i * step);
                if (inside(cur))
                {
                    double lo = prev, hi = cur;
                    for (int k = 0; k < 60; k++)
                    {
                        double mid = (lo + hi) / 2;
                        if (inside(mid)) hi = mid; else lo = mid;
                    }
                    t = hi;
                    return true;
                }
                prev = cur;
            }
            return false;
        }

        /// <summary>Closest distance to a body along one segment (used when there's no encounter yet).</summary>
        public static void ClosestApproach(TrajectorySegment seg, CelestialBody target, out double dist, out double time)
        {
            dist = double.PositiveInfinity;
            time = seg.tStart;
            if (target.parent != seg.body) return;
            const int n = 400;
            double best = double.PositiveInfinity, bestT = seg.tStart;
            for (int i = 0; i <= n; i++)
            {
                double t = seg.tStart + (seg.tEnd - seg.tStart) * i / n;
                seg.orbit.StateAt(t, out var r, out _);
                double d = (r - target.PositionAt(t)).magnitude;
                if (d < best) { best = d; bestT = t; }
            }
            double h = (seg.tEnd - seg.tStart) / n;
            double lo = Math.Max(seg.tStart, bestT - h), hi = Math.Min(seg.tEnd, bestT + h);
            for (int k = 0; k < 40; k++)
            {
                double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
                seg.orbit.StateAt(m1, out var r1, out _);
                seg.orbit.StateAt(m2, out var r2, out _);
                if ((r1 - target.PositionAt(m1)).magnitude < (r2 - target.PositionAt(m2)).magnitude) hi = m2; else lo = m1;
            }
            time = (lo + hi) / 2;
            seg.orbit.StateAt(time, out var rb, out _);
            dist = (rb - target.PositionAt(time)).magnitude;
        }

        /// <summary>
        /// Closest approach between a path segment and another vessel's Kepler orbit around the same body, searched
        /// from tFrom over 'span' seconds (past the segment's end too, if the segment simply closes on itself).
        /// Returns false if they aren't around the same body.
        /// </summary>
        public static bool ClosestApproach(TrajectorySegment seg, CelestialBody targetBody, Orbit target, double tFrom, double span,
            out double dist, out double time, out double relSpeed)
        {
            dist = double.PositiveInfinity; time = tFrom; relSpeed = 0;
            if (targetBody != seg.body || target == null) return false;
            double t0 = Math.Max(seg.tStart, tFrom);
            double t1 = seg.end == SegmentEnd.None && seg.orbit.IsElliptic ? t0 + span : Math.Min(seg.tEnd, t0 + span);
            if (t1 <= t0) return false;
            double D(double t)
            {
                seg.orbit.StateAt(t, out var a, out _);
                target.StateAt(t, out var b, out _);
                return (a - b).magnitude;
            }
            const int n = 720;
            double h = (t1 - t0) / n, best = double.PositiveInfinity, bestT = t0;
            for (int i = 0; i <= n; i++)
            {
                double t = t0 + h * i, d = D(t);
                if (d < best) { best = d; bestT = t; }
            }
            double lo = Math.Max(t0, bestT - h), hi = Math.Min(t1, bestT + h);
            for (int k = 0; k < 60; k++)
            {
                double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
                if (D(m1) < D(m2)) hi = m2; else lo = m1;
            }
            time = (lo + hi) / 2;
            seg.orbit.StateAt(time, out var ra, out var va);
            target.StateAt(time, out var rb, out var vb);
            dist = (ra - rb).magnitude;
            relSpeed = (va - vb).magnitude;
            return true;
        }

        /// <summary>How often two orbits line up the same way again (capped at 30 of the shorter period).</summary>
        public static double SynodicPeriod(Orbit a, Orbit b)
        {
            double inv = Math.Abs(1 / a.Period - 1 / b.Period);
            double cap = 30 * Math.Min(a.Period, b.Period);
            return inv < 1 / cap ? cap : 1 / inv;
        }

        /// <summary>
        /// Plans a one-burn rendezvous with a vessel orbiting the same body: picks the burn time (within maxWait) whose
        /// Hohmann-style transfer arrives where the target will be, then fine-tunes time and prograde dv to minimise
        /// the closest approach. Works for raising or lowering. Returns false if either orbit isn't closed.
        /// </summary>
        public static bool FindRendezvous(CelestialBody body, Vector3d pos, Vector3d vel, double ut, Orbit target, double maxWait,
            out double burnUt, out double dv, out double closest, out double tClosest)
        {
            burnUt = ut + 30; dv = 0; closest = double.PositiveInfinity; tClosest = ut;
            var o = Orbit.FromState(pos, vel, body.mu, ut);
            if (!o.IsElliptic || !target.IsElliptic) return false;
            const int n = 2400;
            double bestAng = double.PositiveInfinity;
            for (int i = 0; i <= n; i++)
            {
                double t = ut + 30 + maxWait * i / n;
                o.StateAt(t, out var r, out _);
                double aT = (r.magnitude + target.a) / 2;
                double tTr = Math.PI * Math.Sqrt(aT * aT * aT / body.mu);
                target.StateAt(t + tTr, out var rt, out _);
                double ang = Vector3d.Angle(-r, rt);
                if (ang < bestAng) { bestAng = ang; burnUt = t; }
            }
            o.StateAt(burnUt, out var rb, out var vb);
            double a2 = (rb.magnitude + target.a) / 2;
            dv = Math.Sqrt(body.mu * (2 / rb.magnitude - 1 / a2)) - vb.magnitude;

            double Score(double tb, double d, out double tc)
            {
                o.StateAt(tb, out var r, out var v);
                var oo = Orbit.FromState(r, v + v.normalized * d, body.mu, tb);
                tc = tb;
                if (!oo.IsElliptic) return double.PositiveInfinity;
                var seg = new TrajectorySegment { body = body, orbit = oo, tStart = tb, tEnd = tb + oo.Period, end = SegmentEnd.None };
                ClosestApproach(seg, body, target, tb, oo.Period * 0.75, out double dist, out tc, out _);
                return dist;
            }
            double best = Score(burnUt, dv, out tClosest);
            double tStep = maxWait / n * 2, dStep = 1.0;
            for (int round = 0; round < 6; round++)
            {
                bool improved = true;
                for (int guard = 0; improved && guard < 40; guard++)
                {
                    improved = false;
                    foreach (var (dt, dd) in new[] { (tStep, 0.0), (-tStep, 0.0), (0.0, dStep), (0.0, -dStep) })
                    {
                        if (burnUt + dt < ut + 5) continue;
                        double s = Score(burnUt + dt, dv + dd, out double tc);
                        if (s < best) { best = s; burnUt += dt; dv += dd; tClosest = tc; improved = true; }
                    }
                }
                tStep /= 3;
                dStep /= 3;
            }
            closest = best;
            return !double.IsInfinity(best);
        }

        /// <summary>
        /// Searches burn times over the next orbit for a prograde transfer burn that reaches a moon with
        /// a periapsis near 'wantPe'. Used by tests and the in-game demo. Returns false if none found.
        /// </summary>
        public static bool FindTransfer(CelestialBody body, Vector3d pos, Vector3d vel, double ut, CelestialBody moon,
            double wantPe, out double burnUt, out double dv, out double gotPe)
        {
            burnUt = ut; dv = 0; gotPe = double.NaN;
            var o = Orbit.FromState(pos, vel, body.mu, ut);
            if (!o.IsElliptic) return false;
            double rTarget = moon.orbitRadius;
            double hohmann = Math.Sqrt(body.mu / o.a) * (Math.Sqrt(2 * rTarget / (o.a + rTarget)) - 1);
            double bestScore = double.PositiveInfinity;
            for (int i = 0; i < 180; i++)
            {
                double t = ut + 60 + o.Period * i / 180.0;
                o.StateAt(t, out var r, out var v);
                for (double d = hohmann - 30; d <= hohmann + 60; d += 3)
                {
                    var segs = Predict(body, r, v + v.normalized * d, t, 2);
                    if (segs.Count < 2 || segs[0].end != SegmentEnd.EnterSoi || segs[1].body != moon) continue;
                    double pe = segs[1].orbit.Periapsis - moon.radius;
                    double score = Math.Abs(pe - wantPe) + d * 2;
                    if (pe > 5000 && score < bestScore) { bestScore = score; burnUt = t; dv = d; gotPe = pe; }
                }
            }
            return !double.IsInfinity(bestScore);
        }
    }
}
