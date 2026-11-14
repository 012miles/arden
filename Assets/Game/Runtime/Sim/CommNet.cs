using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Where the sunlight comes from: the sun (Helion), as Arden goes round it once a year. Arden's spin axis is tilted to
    /// its orbit, so the sun climbs higher in one half of the year and lower in the other.
    /// </summary>
    public static class Sun
    {
        /// <summary>The star of the system made last (null in hand-built test worlds, where the light comes from Fixed).</summary>
        public static CelestialBody Body;
        /// <summary>Where the sun is seen from Arden at time 0 (and always, without a star).</summary>
        public static readonly Vector3d Fixed = new Vector3d(0.75, -0.48, -0.45).normalized;

        /// <summary>From a point (home-planet frame) toward the sun.</summary>
        public static Vector3d DirectionFrom(Vector3d absPos, double ut) => Body == null ? Fixed : (Body.AbsolutePosition(ut) - absPos).normalized;

        /// <summary>From Arden toward the sun: good anywhere near Arden (across its whole sphere of influence it shifts under 0.4 deg).</summary>
        public static Vector3d DirectionAt(double ut) => DirectionFrom(Vector3d.zero, ut);

        /// <summary>True unless a planet or moon sits between this point and the sun.</summary>
        public static bool Lights(Vector3d absPos, IEnumerable<CelestialBody> bodies, double ut)
        {
            Vector3d dir = DirectionFrom(absPos, ut);
            foreach (var b in bodies)
            {
                if (b.isStar) continue;
                Vector3d c = b.AbsolutePosition(ut) - absPos;
                double t = Vector3d.Dot(c, dir);
                if (t > 0 && (c - dir * t).magnitude < b.radius) return false;
            }
            return true;
        }
    }

    /// <summary>A vessel's link home: signal strength (0 = none) and the hops it takes.</summary>
    public class CommLink
    {
        public double signal;
        public readonly List<Vector3d> points = new List<Vector3d>();   // absolute positions: vessel, relays..., ground
        public readonly List<double> strengths = new List<double>();
        public readonly List<string> via = new List<string>();
        public bool Connected => signal > 0;
        public string Describe => !Connected ? "No signal" : via.Count == 0 ? $"Signal {signal * 100:0}% (direct)" : $"Signal {signal * 100:0}% via {string.Join(", ", via)}";
    }

    /// <summary>
    /// The comms network: a ground station at the launch site and every powered antenna in the universe. Two antennas
    /// can talk up to sqrt(powerA x powerB) apart if nothing solid is in the way; relay dishes pass signals on.
    /// </summary>
    public static class CommNet
    {
        public const double GroundPower = 2e8;

        public static Vector3d GroundStation(CelestialBody home, double ut) =>
            home.AbsolutePosition(ut) + home.FixedToInertial(LaunchSite.Direction, ut) * (home.radius + 30);

        /// <summary>The two deep space stations, a third of the way round the equator either side of the launch site.</summary>
        public static IEnumerable<Vector3d> DeepSpaceStations(CelestialBody home, double ut)
        {
            foreach (double turn in new[] { 2 * Math.PI / 3, -2 * Math.PI / 3 })
            {
                Vector3d dir = Vector3d.RotateY(LaunchSite.Direction, turn);
                yield return home.AbsolutePosition(ut) + home.FixedToInertial(dir, ut) * (home.radius + 30);
            }
        }

        /// <summary>Strongest usable antenna (deployed, with power); 0 if it can't transmit at all.</summary>
        public static double PowerOf(VesselSim v)
        {
            if (v.destroyed || v.EcStored < 1e-3) return 0;
            double best = 0;
            foreach (var p in v.parts)
            {
                if (p.def.commPower <= 0) continue;
                if (p.def.IsDeployable && p.legDeploy < 0.9f) continue;
                best = Math.Max(best, p.def.commPower);
            }
            return best;
        }

        public static bool IsRelay(VesselSim v) =>
            !v.destroyed && v.EcStored > 1e-3 && v.parts.Exists(p => p.def.relay && p.legDeploy >= 0.9f);

        public static double Range(double pa, double pb) => Math.Sqrt(pa * pb);

        /// <summary>Link quality: full close in, fading smoothly to nothing at the edge of range.</summary>
        public static double Strength(double dist, double range)
        {
            if (range <= 0 || dist >= range) return 0;
            double s = 1 - dist / range;
            return Math.Min(1, s * s * (3 - 2 * s) * 1.6);
        }

        public static bool LineOfSight(Vector3d a, Vector3d b, IEnumerable<CelestialBody> bodies, double ut)
        {
            Vector3d ab = b - a;
            double len2 = Vector3d.Dot(ab, ab);
            foreach (var body in bodies)
            {
                Vector3d c = body.AbsolutePosition(ut);
                double t = len2 > 0 ? Math.Max(0, Math.Min(1, Vector3d.Dot(c - a, ab) / len2)) : 0;
                if ((a + ab * t - c).magnitude < body.radius - 1) return false;
            }
            return true;
        }

        /// <summary>Best path from a vessel to the ground station, through powered relays (strength multiplies per hop).</summary>
        public static CommLink Connect(VesselSim v, IEnumerable<VesselSim> vessels, IList<CelestialBody> bodies, double ut)
        {
            var link = new CommLink();
            double p0 = PowerOf(v);
            if (p0 <= 0) return link;
            // nodes: 0 = the vessel, 1..n = relays, n+1 = ground
            var pos = new List<Vector3d> { v.AbsolutePosition(ut) };
            var pow = new List<double> { p0 };
            var names = new List<string> { v.name };
            foreach (var r in vessels)
                if (r != v && IsRelay(r)) { pos.Add(r.AbsolutePosition(ut)); pow.Add(PowerOf(r)); names.Add(r.name); }
            int ground = pos.Count;
            pos.Add(GroundStation(bodies[0], ut));
            pow.Add(GroundPower);
            names.Add("ground");
            // The deep space stations: two more big dishes round Arden's equator, so its turning never cuts off a craft
            // far away. They only track craft beyond Arden's sphere of influence (close in, you still need relays).
            int deepFrom = pos.Count;
            foreach (var d in DeepSpaceStations(bodies[0], ut)) { pos.Add(d); pow.Add(GroundPower); names.Add("ground"); }
            int n = pos.Count;
            var home = bodies[0];
            bool IsGround(int i) => i == ground || i >= deepFrom;
            bool Far(Vector3d p) => (p - home.AbsolutePosition(ut)).magnitude > home.SoiRadius;
            var best = new double[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { best[i] = 0; prev[i] = -1; }
            best[0] = 1;
            for (int iter = 0; iter < n; iter++)
            {
                int u = -1;
                for (int i = 0; i < n; i++) if (!done[i] && best[i] > 0 && (u < 0 || best[i] > best[u])) u = i;
                if (u < 0 || IsGround(u)) break;
                done[u] = true;
                for (int w = 0; w < n; w++)
                {
                    if (done[w] || w == u) continue;
                    if (w >= deepFrom && !Far(pos[u])) continue; // deep space stations only track far-off craft
                    double s = Strength((pos[u] - pos[w]).magnitude, Range(pow[u], pow[w]));
                    if (s <= 0 || best[u] * s <= best[w] || !LineOfSight(pos[u], pos[w], bodies, ut)) continue;
                    best[w] = best[u] * s;
                    prev[w] = u;
                }
            }
            int reached = -1;
            for (int i = 0; i < n; i++) if (IsGround(i) && best[i] > 0 && (reached < 0 || best[i] > best[reached])) reached = i;
            if (reached < 0) return link;
            link.signal = best[reached];
            var chain = new List<int>();
            for (int k = reached; k >= 0; k = prev[k]) chain.Add(k);
            chain.Reverse();
            for (int i = 0; i < chain.Count; i++)
            {
                link.points.Add(pos[chain[i]]);
                if (i > 0) link.strengths.Add(Strength((pos[chain[i - 1]] - pos[chain[i]]).magnitude, Range(pow[chain[i - 1]], pow[chain[i]])));
                if (i > 0 && !IsGround(chain[i])) link.via.Add(names[chain[i]]);
            }
            return link;
        }
    }
}
