using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Trips between planets. A transfer orbit (half an ellipse round the sun from one planet's orbit to the other's) only
    /// meets the other planet if it's the right angle ahead (or behind) when you leave: the "phase angle". This works out
    /// that angle, when it next comes round (the window), the escape burn from a parking orbit, and then searches burn
    /// times and sizes against the real patched-conic path for the one that gets closest to the target.
    /// </summary>
    public static class TransferPlanner
    {
        /// <summary>How far round the sun 'to' is ahead of 'from' (radians, -pi..pi), measured along their direction of travel.</summary>
        public static double Phase(CelestialBody from, CelestialBody to, double ut)
        {
            Vector3d a = from.PositionAt(ut), b = to.PositionAt(ut);
            Vector3d n = Vector3d.Cross(from.orbitZ, from.orbitX); // the way the planets go round
            return Math.Atan2(Vector3d.Dot(Vector3d.Cross(a, b), n), Vector3d.Dot(a, b));
        }

        /// <summary>Time to fly the transfer half-ellipse (s).</summary>
        public static double TransferTime(CelestialBody from, CelestialBody to)
        {
            double r1 = from.orbitRadius, r2 = to.orbitRadius, mu = from.parent.mu;
            return Math.PI * Math.Sqrt(Math.Pow(r1 + r2, 3) / (8 * mu));
        }

        /// <summary>The phase angle to leave at, so the target arrives where the transfer ends (radians, -pi..pi).</summary>
        public static double RequiredPhase(CelestialBody from, CelestialBody to)
        {
            double p = Math.PI - to.OrbitalAngularVelocity * TransferTime(from, to);
            return Wrap(p);
        }

        static double Wrap(double a) { a %= 2 * Math.PI; if (a > Math.PI) a -= 2 * Math.PI; if (a < -Math.PI) a += 2 * Math.PI; return a; }

        /// <summary>When the phase angle next comes round to the right one (the window), at or after 'ut'.</summary>
        public static double NextWindow(CelestialBody from, CelestialBody to, double ut)
        {
            double rate = to.OrbitalAngularVelocity - from.OrbitalAngularVelocity; // the phase angle changes this fast
            double now = Phase(from, to, ut), want = RequiredPhase(from, to);
            double gap = rate < 0 ? (now - want) : (want - now);
            gap %= 2 * Math.PI;
            if (gap < 0) gap += 2 * Math.PI;
            return ut + gap / Math.Abs(rate);
        }

        /// <summary>Speed left over after escaping, for the transfer (m/s; the sign says faster or slower than the planet).</summary>
        public static double ExcessSpeed(CelestialBody from, CelestialBody to)
        {
            double r1 = from.orbitRadius, r2 = to.orbitRadius, mu = from.parent.mu;
            return Math.Sqrt(mu / r1) * (Math.Sqrt(2 * r2 / (r1 + r2)) - 1);
        }

        /// <summary>The prograde burn from a circular parking orbit of radius r that leaves with the transfer's excess speed.</summary>
        public static double EjectionDv(CelestialBody from, CelestialBody to, double r)
        {
            double vinf = ExcessSpeed(from, to), mu = from.mu;
            return Math.Sqrt(vinf * vinf + 2 * mu / r) - Math.Sqrt(mu / r);
        }

        /// <summary>
        /// Plans the trip from a vessel orbiting 'home' to planet 'target': near the next window, the burn time and prograde
        /// size whose predicted path passes closest to the target (an encounter if 'closest' is inside its sphere of
        /// influence). Returns false if no escape toward it turned up.
        /// </summary>
        public static bool Plan(CelestialBody home, Vector3d pos, Vector3d vel, double ut, CelestialBody target,
            out double burnUt, out double dv, out double closest, out double window) =>
            Plan(home, pos, vel, ut, target, out burnUt, out dv, out _, out closest, out window);

        /// <summary>As above, with the burn's sideways (normal) part too: parking orbits over Arden's equator are tilted to the planets' orbits.</summary>
        public static bool Plan(CelestialBody home, Vector3d pos, Vector3d vel, double ut, CelestialBody target,
            out double burnUt, out double dv, out double normal, out double closest, out double window)
        {
            burnUt = ut; dv = 0; normal = 0; closest = double.PositiveInfinity;
            window = NextWindow(home, target, ut);
            var park = Orbit.FromState(pos, vel, home.mu, ut);
            if (!park.IsElliptic || park.Apoapsis > home.SoiRadius * 0.5) return false;
            double period = park.Period, synodic = 2 * Math.PI / Math.Abs(target.OrbitalAngularVelocity - home.OrbitalAngularVelocity);
            // A window that's only just gone is still good (the phase hardly moves in a day or two).
            double prev = window - synodic;
            if (prev > ut - period && window - ut > 2 * 86400) window = Math.Max(ut + 60, prev);
            double dv0 = EjectionDv(home, target, park.a);
            double best = double.PositiveInfinity, bestT = window, bestDv = dv0, bestN = 0;
            // On arrival, aim to pass this high (just above the air): low enough to capture cheaply or aerobrake.
            double wantAlt = target.atmosphereHeight + 30_000, soi = target.SoiRadius;

            // Score: a miss scores its closest distance (>= 0); an encounter scores below zero, lower the nearer its
            // periapsis is to the height we want.
            double Try(double t, double d, double n = 0)
            {
                if (t < ut + 30) return double.PositiveInfinity;
                park.StateAt(t, out var r, out var v);
                Vector3d nrm = Vector3d.Cross(r, v).normalized;
                var segs = Patcher.Predict(home, r, v + v.normalized * d + nrm * n, t, 3);
                double dist = double.PositiveInfinity;
                foreach (var s in segs)
                {
                    if (s.body == target)
                    {
                        double peAlt = s.orbit.Periapsis - target.radius;
                        return Math.Min(Math.Abs(peAlt - wantAlt), soi) - soi;
                    }
                    if (s.body.isStar)
                    {
                        Patcher.ClosestApproach(s, target, out double cd, out _);
                        dist = Math.Min(dist, cd);
                    }
                }
                return dist;
            }

            // Coarse: burn times round one parking orbit near the window, at the worked-out size
            for (int i = 0; i < 48; i++)
            {
                double t = window - period / 2 + period * i / 48;
                double d = Try(t, dv0);
                if (d < best) { best = d; bestT = t; bestDv = dv0; }
            }
            if (double.IsInfinity(best)) return false;
            // Fine: nudge time and size around the best, then the sideways part, then all three ever finer. Arrival is
            // very sensitive (1 m/s at departure moves it thousands of km), so the last rounds work in fractions of a m/s.
            double stepT = period / 48, stepDv = 30, stepN = 0;
            void Refine(int rounds)
            {
                for (int round = 0; round < rounds; round++)
                {
                    double ct = bestT, cd = bestDv, cn = bestN;
                    int nk = stepN > 0 ? 1 : 0, bi = 0, bj = 0, bk = 0;
                    for (int i = -3; i <= 3; i++)
                        for (int j = -3; j <= 3; j++)
                            for (int k = -nk; k <= nk; k++)
                            {
                                if (i == 0 && j == 0 && k == 0) continue;
                                double t = ct + i * stepT / 3, d = cd + j * stepDv, n = cn + k * stepN;
                                double score = Try(t, d, n);
                                if (score < best) { best = score; bestT = t; bestDv = d; bestN = n; bi = i; bj = j; bk = k; }
                            }
                    // Best on the edge of the grid: the answer lies further out, so keep the step; otherwise look closer.
                    bool edge = Math.Abs(bi) == 3 || Math.Abs(bj) == 3 || (nk > 0 && Math.Abs(bk) == nk);
                    if (!edge) { stepT /= 3; stepDv /= 3; stepN /= 3; }
                }
            }
            Refine(2);
            for (double n = -400; n <= 400; n += 50)
            {
                double score = Try(bestT, bestDv, n);
                if (score < best) { best = score; bestN = n; }
            }
            stepT = period / 96; stepDv = 10; stepN = 25;
            Refine(6);
            // Last, fine sweeps one setting at a time: arrival height swings through zero over a fraction of a m/s.
            void Sweep(Func<double, double> score, Action<double> keep, double span, double step)
            {
                double bestOff = 0;
                for (double off = -span; off <= span + 1e-9; off += step)
                {
                    double s = score(off);
                    if (s < best) { best = s; bestOff = off; }
                }
                keep(bestOff);
            }
            if (best < 0)
            {
                // The burn size slides the arrival point along one line past the planet: put it as close as that line
                // allows. Getting closer still is a job for a small correction halfway (PlanCorrection): from there a
                // m/s moves the arrival point thousands of km, where at departure it moves it only tens.
                double t0 = bestT, d0 = bestDv, n0 = bestN;
                Sweep(off => Try(t0, d0 + off, n0), off => bestDv = d0 + off, 1.0, 0.01);
                d0 = bestDv;
                Sweep(off => Try(t0, d0 + off, n0), off => bestDv = d0 + off, 0.01, 0.0002);
            }
            burnUt = bestT; dv = bestDv; normal = bestN;
            closest = best <= 0 ? 0 : best;
            return true;
        }

        /// <summary>
        /// A course correction on the way (orbiting the sun): a small burn about 40% of the way to the target that puts
        /// the arrival periapsis just above its air (or turns a near miss into an encounter). Burn in the vessel's own
        /// frame: prograde, normal, radial (m/s). Returns false if the target isn't anywhere near the path.
        /// </summary>
        /// <summary>
        /// The highest arrival periapsis (m above the surface) at which a pass through the target's air would capture a
        /// vessel of this mass and drag arriving at 'vinf' (m/s), into an orbit no bigger than a quarter of its sphere of
        /// influence. NaN if no height works (too fast, or too little drag).
        /// </summary>
        public static double AerocaptureAltitude(CelestialBody target, double vinf, double massT, double cdA) =>
            AerocaptureAltitude(target, vinf, massT, cdA, cdA);

        /// <summary>
        /// As above, safe whichever way the vessel flies through: the pass has to capture it with its least drag (flying
        /// cleanly end on) and still leave it in orbit with its most (side on, if it tumbles). NaN if no height does both.
        /// </summary>
        public static double AerocaptureAltitude(CelestialBody target, double vinf, double massT, double cdLeast, double cdMost)
        {
            if (!target.HasAtmosphere) return double.NaN;
            double top = target.radius + target.atmosphereHeight;
            AeroForecast Pass(double alt, double cdA)
            {
                double rp = target.radius + alt, vp = Math.Sqrt(vinf * vinf + 2 * target.mu / rp);
                // Arriving east-about over the equator (with the air's turn), periapsis at time 0
                var hyp = Orbit.FromState(new Vector3d(rp, 0, 0), new Vector3d(0, 0, -vp), target.mu, 0);
                double lo = -86400 * 3, hi = 0;
                for (int i = 0; i < 80; i++) { double mid = (lo + hi) / 2; if (hyp.RadiusAt(mid) > top) lo = mid; else hi = mid; }
                hyp.StateAt(lo, out var r, out var v);
                return AeroPass.Predict(target, r, v, lo, massT, cdA);
            }
            // The safe band: heights where it captures flying cleanly AND still stays up if it tumbles. Aim for the middle,
            // so neither more nor less drag than expected tips it out of the band.
            double bandTop = double.NaN, bandBottom = double.NaN;
            for (double alt = target.atmosphereHeight * 0.95; alt > target.atmosphereHeight * 0.05; alt -= target.atmosphereHeight * 0.005)
            {
                var clean = Pass(alt, cdLeast * 0.8); // margin: a wobbling craft loses drag (and escaping is worse than coming in deep)
                bool cleanOk = clean.outcome == AeroOutcome.Captured && clean.ap < target.SoiRadius * 0.25;
                if (clean.outcome == AeroOutcome.Impact || clean.outcome == AeroOutcome.Lands) break; // too deep even flying cleanly
                if (!cleanOk) { if (!double.IsNaN(bandTop)) break; continue; }                     // not captured yet: go deeper
                var rough = Pass(alt, cdMost);
                if (rough.outcome != AeroOutcome.Captured) break;                                    // tumbling would bring it down
                if (double.IsNaN(bandTop)) bandTop = alt;
                bandBottom = alt;
            }
            return double.IsNaN(bandTop) ? double.NaN : (bandTop + bandBottom) / 2;
        }

        public static bool PlanCorrection(CelestialBody sun, Vector3d pos, Vector3d vel, double ut, CelestialBody target,
            out double burnUt, out double prograde, out double normal, out double radial, out double arrivalAlt) =>
            PlanCorrection(sun, pos, vel, ut, target, target.atmosphereHeight + 30_000, out burnUt, out prograde, out normal, out radial, out arrivalAlt);

        /// <summary>As above, aiming the arrival periapsis at 'wantAlt' (m above the surface): e.g. an aerocapture height.</summary>
        public static bool PlanCorrection(CelestialBody sun, Vector3d pos, Vector3d vel, double ut, CelestialBody target, double wantAlt,
            out double burnUt, out double prograde, out double normal, out double radial, out double arrivalAlt)
        {
            burnUt = ut; prograde = normal = radial = 0; arrivalAlt = double.NaN;
            if (!sun.isStar || target.parent != sun) return false;
            var segs = Patcher.Predict(sun, pos, vel, ut, 2);
            if (segs.Count == 0) return false;
            double tArrive;
            if (segs.Count > 1 && segs[1].body == target) tArrive = segs[0].tEnd;
            else
            {
                Patcher.ClosestApproach(segs[0], target, out double cd, out double ct);
                if (double.IsInfinity(cd) || cd > target.SoiRadius * 40) return false;
                tArrive = ct;
            }
            burnUt = ut + Math.Max(120, (tArrive - ut) * 0.4);
            var path = Orbit.FromState(pos, vel, sun.mu, ut);
            path.StateAt(burnUt, out var r, out var v);
            Vector3d pro = v.normalized, nrm = Vector3d.Cross(r, v).normalized, rad = Vector3d.Cross(nrm, pro);
            double soi = target.SoiRadius, tb = burnUt;
            double Score(double p, double n, double q)
            {
                var ss = Patcher.Predict(sun, r, v + pro * p + nrm * n + rad * q, tb, 2);
                if (ss.Count > 1 && ss[1].body == target) return Math.Min(Math.Abs(ss[1].orbit.Periapsis - target.radius - wantAlt), soi) - soi;
                Patcher.ClosestApproach(ss[0], target, out double d, out _);
                return d;
            }
            double best = Score(0, 0, 0), bp = 0, bn = 0, bq = 0;
            // Sweep each direction in turn, coarse then ever finer
            foreach (double span in new[] { 30.0, 3.0, 0.3, 0.03 })
                for (int pass = 0; pass < 2; pass++)
                    for (int axis = 0; axis < 3; axis++)
                    {
                        double c0 = axis == 0 ? bp : axis == 1 ? bn : bq, keep = c0;
                        for (int i = -20; i <= 20; i++)
                        {
                            double x = c0 + span * i / 20;
                            double s = axis == 0 ? Score(x, bn, bq) : axis == 1 ? Score(bp, x, bq) : Score(bp, bn, x);
                            if (s < best) { best = s; keep = x; }
                        }
                        if (axis == 0) bp = keep; else if (axis == 1) bn = keep; else bq = keep;
                    }
            prograde = bp; normal = bn; radial = bq;
            var fin = Patcher.Predict(sun, r, v + pro * bp + nrm * bn + rad * bq, burnUt, 2);
            if (fin.Count > 1 && fin[1].body == target) arrivalAlt = fin[1].orbit.Periapsis - target.radius;
            return true;
        }
    }
}
