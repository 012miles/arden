using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    public enum LandmarkKind { Stones, Probe, Spire, Geysers, Arch, Caldera, Balanced, LavaTubes, Crystals, Outpost, Wreck }   // the last two: milestone 34 outposts to scavenge

    /// <summary>A place worth finding on a world (milestone 27): where it is, how close counts as a visit, what it pays.</summary>
    public class Landmark
    {
        public string id, name, body, blurb;
        public LandmarkKind kind;
        public Vector3d dir;          // planet-fixed, unit
        public double reach = 500;    // landed this close (m) counts as a visit
        public double science, funds;
        public float yaw;             // which way it's turned about its up (deg)
        public int seed;              // for its model's shapes
    }

    /// <summary>
    /// Milestone 27 (captaincarl24's pick): places to discover. Each world has one or two: old stones on Arden's hills, a probe
    /// that crashed on Tal long ago, a lone rock needle, frost geysers on Vesk, a great stone arch and the Great Mound's
    /// caldera on Ochre, a boulder balanced on a pillar on Mote, lava caves on Pyre, a crater full of glowing crystals on
    /// Rime. Flying low over one (or mapping it with a survey scanner) puts a "?" on the map; landing next to it pays
    /// science and funds and counts toward the explorer badges.
    /// </summary>
    public static class Landmarks
    {
        // where each one starts its search (latitude, longitude), and how it settles: on the lowest ground nearby (a crater
        // floor), on the highest, or on dry land (Arden)
        enum Settle { AsIs, Low, High, Land, Flat }

        static readonly (string id, string name, string body, LandmarkKind kind, double lat, double lon, Settle settle, double reach, double sci, double funds, string blurb)[] Defs =
        {
            ("arden.stones", "The Old Stones", "Arden", LandmarkKind.Stones, 0.55, -0.25, Settle.Land, 400, 15, 8_000,
                "A ring of tall standing stones on a hilltop. Nobody knows who raised them."),
            ("tal.probe", "The Fallen Probe", "Tal", LandmarkKind.Probe, 12, 38, Settle.Low, 500, 60, 25_000,
                "A probe from the very first days of the space program, lost before anyone could see where it came down."),
            ("tal.needle", "The Needle", "Tal", LandmarkKind.Spire, -24, 151, Settle.AsIs, 600, 45, 18_000,
                "A single spire of rock 180 m tall, far too thin to be standing."),
            ("vesk.geysers", "Frost Geysers", "Vesk", LandmarkKind.Geysers, -68, 75, Settle.AsIs, 600, 80, 35_000,
                "Vents in the ice that throw plumes of frost high into space."),
            ("ochre.arch", "The Great Arch", "Ochre", LandmarkKind.Arch, 18, -62, Settle.AsIs, 800, 110, 60_000,
                "A natural stone arch 400 m across, carved by wind over millions of years."),
            ("ochre.caldera", "Great Mound Caldera", "Ochre", LandmarkKind.Caldera, 0, 0, Settle.AsIs, 3_000, 120, 60_000,
                "The summit crater of the Great Mound, the tallest volcano in the system."),
            ("mote.stone", "The Balanced Stone", "Mote", LandmarkKind.Balanced, 30, -100, Settle.Flat, 400, 70, 30_000,
                "A boulder the size of a house, balanced on a thin pillar of rock."),
            ("pyre.tubes", "Lava Caves", "Pyre", LandmarkKind.LavaTubes, -8, 120, Settle.Flat, 500, 100, 45_000,
                "Cave mouths in a hillside, with molten rock glowing deep inside."),
            ("rime.crystals", "Crystal Crater", "Rime", LandmarkKind.Crystals, 40, 30, Settle.Low, 600, 120, 55_000,
                "A crater floor bristling with giant blue crystals that glow in the dark."),
        };

        static readonly Dictionary<string, List<Landmark>> cache = new Dictionary<string, List<Landmark>>();

        /// <summary>The landmarks on a world (placed once per world, the same in every game).</summary>
        public static List<Landmark> On(CelestialBody b)
        {
            if (b == null) return new List<Landmark>();
            string key = b.name + "_" + b.seed;
            lock (cache)
            {
                if (cache.TryGetValue(key, out var list)) return list;
                list = new List<Landmark>();
                foreach (var d in Defs)
                {
                    if (d.body != b.name) continue;
                    Vector3d dir = d.kind == LandmarkKind.Caldera ? Vector3d.FromFloat(Terrain.GreatMound).normalized : CelestialBody.DirectionFromLatLon(d.lat, d.lon);
                    if (d.body == "Arden") dir = (LaunchSite.Direction + CelestialBody.NorthAt(LaunchSite.Direction) * (d.lat * Math.PI / 180) + CelestialBody.EastAt(LaunchSite.Direction) * (d.lon * Math.PI / 180)).normalized;
                    dir = SettleAt(b, dir, d.settle);
                    int hash = StableHash(d.id);
                    list.Add(new Landmark { id = d.id, name = d.name, body = d.body, kind = d.kind, dir = dir, reach = d.reach, science = d.sci, funds = d.funds, blurb = d.blurb,
                        yaw = (hash & 0xff) * 1.4f, seed = (hash >> 8) & 0x7fff });
                }
                cache[key] = list;
                return list;
            }
        }

        public static IEnumerable<Landmark> All(IEnumerable<CelestialBody> bodies) => bodies.SelectMany(On);

        /// <summary>A hash of a name that's the same on every machine and every run.</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s) { h ^= ch; h *= 16777619; }
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>The landmark's own frame (planet-fixed): y up out of the ground, turned by its yaw.</summary>
        public static UnityEngine.Quaternion Frame(Landmark l) =>
            UnityEngine.Quaternion.FromToRotation(UnityEngine.Vector3.up, l.dir.ToFloat()) * UnityEngine.Quaternion.Euler(0, l.yaw, 0);
        public static int Count => Defs.Length;

        static Vector3d SettleAt(CelestialBody b, Vector3d dir, Settle settle)
        {
            if (settle == Settle.AsIs) return dir;
            Vector3d e = CelestialBody.EastAt(dir), n = CelestialBody.NorthAt(dir);
            Vector3d best = dir;
            double bestScore = double.MaxValue;
            double reach = settle == Settle.Land ? 120_000 : 15_000;
            for (int r = 0; r <= 12; r++)
                for (int k = 0; k < (r == 0 ? 1 : 16); k++)
                {
                    double ang = k * Math.PI / 8, m = reach * r / 12.0;
                    Vector3d d = (dir + (e * Math.Cos(ang) + n * Math.Sin(ang)) * (m / b.radius)).normalized;
                    double h = Terrain.Height(b, d);
                    double score;
                    if (settle == Settle.Land)
                    {
                        if (Terrain.IsWater(b, d) || h < 60) continue;
                        // dry, a little high, not too far from the space centre, and well away from it
                        double fromSite = Vector3d.Angle(d, LaunchSite.Direction) * b.radius;
                        if (fromSite < 25_000) continue;
                        score = fromSite * 0.001 - h * 0.05;
                    }
                    else if (settle == Settle.Flat)
                    {
                        // the least slope over 60 m either way, a little high
                        Vector3d e1 = CelestialBody.EastAt(d), n1 = CelestialBody.NorthAt(d);
                        double s = 0;
                        foreach (var t in new[] { e1, n1, -e1, -n1 }) s = Math.Max(s, Math.Abs(Terrain.Height(b, (d + t * (60 / b.radius)).normalized) - h));
                        score = s * 10 - h * 0.002;
                    }
                    else score = settle == Settle.Low ? h : -h;
                    if (score < bestScore) { bestScore = score; best = d; }
                }
            return best;
        }

        public static double GroundDistance(CelestialBody b, Landmark l, Vector3d fixedDir) => Vector3d.Angle(fixedDir.normalized, l.dir) * b.radius;

        /// <summary>The nearest landmark on this world to a planet-fixed direction (null if it has none).</summary>
        public static Landmark Nearest(CelestialBody b, Vector3d fixedDir, out double metres, Func<Landmark, bool> filter = null)
        {
            metres = double.MaxValue;
            Landmark best = null;
            foreach (var l in On(b))
            {
                if (filter != null && !filter(l)) continue;
                double m = GroundDistance(b, l, fixedDir);
                if (m < metres) { metres = m; best = l; }
            }
            return best;
        }

        public const double SpotRange = 60_000, SpotAltitude = 80_000;

        /// <summary>
        /// Once a second: landmarks a survey scanner has mapped, or that the vessel being flown passes low over, are
        /// spotted; landing beside one is a visit (paid once, in career).
        /// </summary>
        public static void Update(Universe u, VesselSim v, double ut)
        {
            var c = u.career;
            if (v == null || v.destroyed || v.body == null || v.body.isStar || v.body.IsGasGiant) return;
            var b = v.body;
            Vector3d here = b.InertialToFixed(v.pos, ut).normalized;
            double alt = v.Altitude;
            foreach (var l in On(b))
            {
                double m = GroundDistance(b, l, here);
                double sight = Math.Min(SpotRange, Math.Max(15_000, alt * 1.2));     // further the higher you are
                if (!c.spotted.Contains(l.id) && ((alt < SpotAltitude && m < sight) || (u.survey != null && u.survey.IsScanned(b, l.dir))))
                {
                    c.spotted.Add(l.id);
                    c.news.Add($"Spotted something on {b.name}: a \"?\" on the map. Land near it to find out what it is.");
                }
                if (!c.visited.Contains(l.id) && v.landed && v.hasLaunched && m < l.reach)
                {
                    if (!c.spotted.Contains(l.id)) c.spotted.Add(l.id);
                    c.visited.Add(l.id);
                    if (c.IsCareer) { c.science += l.science; c.funds += l.funds; }
                    c.news.Add(c.IsCareer ? $"Discovered {l.name}! {l.blurb} +{l.science:0} science, +{l.funds:N0} funds" : $"Discovered {l.name}! {l.blurb}");
                    Achievements.Award(c, "explorer");
                    if (c.visited.Count >= 5) Achievements.Award(c, "globetrotter");
                }
            }
        }
    }
}
