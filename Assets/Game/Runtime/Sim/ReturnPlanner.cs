using System;

namespace Rocket
{
    /// <summary>
    /// Milestone 46 (captaincarl24 asked how to autopilot "to the moon and back"): the burn home from an orbit round a moon.
    /// Tries burn times round one orbit and burn sizes from just enough to escape upward, predicting each path with the patched
    /// conics (Patcher), and keeps the smallest burn that leaves the moon and drops into the planet's air at about the right
    /// height for the parachutes (its lowest point near 45% of the way up the air). A correction on the way home trims the rest.
    /// </summary>
    public static class ReturnPlanner
    {
        public static bool Plan(CelestialBody moon, Vector3d pos, Vector3d vel, double ut, out double tBurn, out double dv, out double peAlt)
        {
            tBurn = ut; dv = 0; peAlt = double.NaN;
            var home = moon?.parent;
            if (home == null || home.isStar) return false;
            var o = Orbit.FromState(pos, vel, moon.mu, ut);
            if (!o.IsElliptic) return false;
            double want = WantPeriapsis(home);
            double vc = Math.Sqrt(moon.mu / o.a), dvEsc = vc * (Math.Sqrt(2) - 1);
            double best = double.MaxValue, bt = ut, bd = 0, bp = double.NaN;
            void Try(double t, double d)
            {
                o.StateAt(t, out var r, out var v);
                var path = Patcher.Predict(moon, r, v + v.normalized * d, t, 2);
                if (path.Count < 2 || path[0].end != SegmentEnd.ExitSoi || path[0].next != home || path[1].orbit == null) return;
                double pe = path[1].orbit.Periapsis - home.radius;
                // the smallest burn that dips into the right band of air (1 km off the ideal height counts as 1 m/s); a path
                // outside the band only if there's nothing better. (Scoring mostly on height let it pick a 1,120 m/s burn that
                // was so touchy its small aiming errors flung the ship past Arden toward the sun: the bug test.)
                bool band = pe > want * 0.35 && pe < want * 1.6;
                double score = band ? d + Math.Abs(pe - want) / 1000 : 1e6 + Math.Abs(pe - want);
                if (score < best) { best = score; bt = t; bd = d; bp = pe; }
            }
            double period = o.Period;
            for (int i = 0; i < 36; i++)
                for (int k = 0; k < 28; k++)
                    Try(ut + 60 + period * i / 36.0, dvEsc * 0.98 + k * 35);
            if (best == double.MaxValue) return false;
            double t0 = bt, d0 = bd;
            for (int i = -6; i <= 6; i++)
                for (int k = -10; k <= 10; k++)
                {
                    double t = t0 + i * period / 36.0 / 6, d = d0 + k * 3.5;
                    if (t > ut + 30 && d > 0) Try(t, d);
                }
            tBurn = bt; dv = bd; peAlt = bp;
            return true;
        }

        /// <summary>How low the path home should dip: into the air for the parachutes (an airless world: just above the ground).</summary>
        public static double WantPeriapsis(CelestialBody home) => home.HasAtmosphere ? home.atmosphereHeight * 0.45 : home.radius * 0.03;
    }
}
