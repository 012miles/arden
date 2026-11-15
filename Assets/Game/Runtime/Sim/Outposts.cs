using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    /// <summary>
    /// Milestone 34 (the Expedition's "outposts and wrecks on planets and moons to scavenge fuel, food and parts"): old
    /// outposts and crashed ships on the moons and planets. Land within 300 m of one (or walk up to it) and Scavenge: its
    /// fuel tops up your tanks, its stores your supplies, and its logs give science. Each can be searched once per game.
    /// They're in every game, not just expeditions, and show on the map as a ⌂.
    /// </summary>
    public static class Outposts
    {
        public class Def
        {
            public string id, name, body, blurb;
            public LandmarkKind kind;
            public double lat, lon, fuel, mono, supplies, science;
        }

        public const double Reach = 300;

        public static readonly Def[] Defs =
        {
            new Def { id = "tal.camp", name = "Camp Ridge", body = "Tal", kind = LandmarkKind.Outpost, lat = 3, lon = -18, fuel = 9, mono = 0.6, supplies = 40, science = 40,
                blurb = "Tal's first outpost, left when the money ran out. The tanks were never emptied." },
            new Def { id = "tal.lander", name = "The Lost Lander", body = "Tal", kind = LandmarkKind.Wreck, lat = -6, lon = 64, fuel = 2.5, mono = 0.2, supplies = 6, science = 30,
                blurb = "A lander that came down too fast. Its upper tank survived." },
            new Def { id = "vesk.station", name = "Ice Station Vesk", body = "Vesk", kind = LandmarkKind.Outpost, lat = 12, lon = 18, fuel = 8, mono = 0.6, supplies = 40, science = 60,
                blurb = "A research hut on the ice, still with fuel made from the ice itself." },
            new Def { id = "ochre.harbor", name = "Dust Harbor", body = "Ochre", kind = LandmarkKind.Outpost, lat = 6, lon = 40, fuel = 14, mono = 1, supplies = 120, science = 90,
                blurb = "A supply depot left for a crew that never came: fuel, food and air for months." },
            new Def { id = "ochre.wreck", name = "Pathfinder Wreck", body = "Ochre", kind = LandmarkKind.Wreck, lat = -12, lon = -100, fuel = 3, mono = 0.3, supplies = 10, science = 70,
                blurb = "An early Ochre lander, half buried in red dust." },
            new Def { id = "mote.depot", name = "Mote Depot", body = "Mote", kind = LandmarkKind.Outpost, lat = 2, lon = 8, fuel = 16, mono = 1, supplies = 150, science = 110,
                blurb = "The farthest outpost ever built, waiting for anyone brave enough to reach it." },
            new Def { id = "rime.wreck", name = "Rime Wreck", body = "Rime", kind = LandmarkKind.Wreck, lat = 22, lon = 80, fuel = 4, mono = 0.3, supplies = 12, science = 90,
                blurb = "A probe that slid off a crystal ridge." },
        };

        static readonly Dictionary<string, List<Landmark>> cache = new Dictionary<string, List<Landmark>>();

        // ---- milestone 39: random places along the way, different in every game (the game's seed picks them)
        static int gameSeed = 1;
        static readonly Dictionary<string, Def> randomDefs = new Dictionary<string, Def>();

        /// <summary>Which game's random places to use (its weather seed); a new seed forgets the old places.</summary>
        public static int GameSeed
        {
            get => gameSeed;
            set { if (value == gameSeed) return; gameSeed = value; lock (cache) { cache.Clear(); randomDefs.Clear(); } }
        }

        static readonly (string name, LandmarkKind kind, string blurb)[] SiteKinds =
        {
            ("Supply cache", LandmarkKind.Outpost, "Crates left for whoever came next. Some are still sealed."),
            ("Abandoned camp", LandmarkKind.Outpost, "Tents, a stove and a note that just says \"back soon\"."),
            ("Crashed probe", LandmarkKind.Wreck, "An old probe that didn't land so much as arrive."),
            ("Research hut", LandmarkKind.Outpost, "Instruments still ticking away, and a fridge nobody emptied."),
            ("Fuel depot", LandmarkKind.Outpost, "A tank farm, mostly empty. Mostly."),
            ("Relay mast", LandmarkKind.Wreck, "A fallen relay mast with a toolbox chained to it."),
            ("Survey rover", LandmarkKind.Wreck, "A rover that ran out of battery mid-survey."),
            ("Cargo pod", LandmarkKind.Wreck, "A cargo pod that missed its drop zone by a long way."),
        };

        /// <summary>
        /// The random places on a world: 10 round the space centre on the home world (4 to 60 km out, for rover trips),
        /// 8 anywhere on other worlds with ground (none on the sun or the gas giant). Smaller finds than the big outposts:
        /// a little fuel and food, some science, and things to pick up.
        /// </summary>
        public static List<Def> RandomSites(CelestialBody b)
        {
            var list = new List<Def>();
            if (b == null || b.isStar || b.IsGasGiant) return list;
            var rng = new System.Random(gameSeed * 7919 + Landmarks.StableHash(b.name));
            bool home = b.IsHomeWorld;
            int n = home ? 10 : 8;
            for (int i = 0; i < n; i++)
            {
                var k = SiteKinds[rng.Next(SiteKinds.Length)];
                Vector3d dir = default;
                for (int tries = 0; tries < 12; tries++)
                {
                    if (home)
                    {
                        Vector3d site = LaunchSite.Direction, e = CelestialBody.EastAt(site), nn = CelestialBody.NorthAt(site);
                        double a = rng.NextDouble() * Math.PI * 2, km = 4 + rng.NextDouble() * 56;
                        dir = (site + (e * Math.Cos(a) + nn * Math.Sin(a)) * (km * 1000 / b.radius)).normalized;
                    }
                    else dir = CelestialBody.DirectionFromLatLon(rng.NextDouble() * 100 - 50, rng.NextDouble() * 360 - 180);
                    if (!Terrain.IsWater(b, dir)) break;
                }
                double lat = Math.Asin(Math.Max(-1, Math.Min(1, dir.y))) * 180 / Math.PI, lon = Math.Atan2(-dir.z, dir.x) * 180 / Math.PI;
                var d = new Def
                {
                    id = $"site.{b.name.ToLowerInvariant()}.{i}", name = $"{k.name} {(char)('A' + rng.Next(26))}-{1 + rng.Next(9)}", body = b.name, kind = k.kind,
                    lat = lat, lon = lon, fuel = Math.Round(0.4 + rng.NextDouble() * 2.6, 1), mono = Math.Round(rng.NextDouble() * 0.15, 2),
                    supplies = Math.Round(2 + rng.NextDouble() * 12), science = Math.Round(5 + rng.NextDouble() * 20), blurb = k.blurb,
                };
                list.Add(d);
            }
            return list;
        }

        public static Def Get(string id)
        {
            var d = Defs.FirstOrDefault(x => x.id == id);
            if (d != null || id == null || !id.StartsWith("site.")) return d;
            lock (cache) { if (randomDefs.TryGetValue(id, out d)) return d; }
            return null;
        }

        /// <summary>The outposts and wrecks on a world, as places (drawn like the landmarks).</summary>
        public static List<Landmark> On(CelestialBody b)
        {
            if (b == null) return new List<Landmark>();
            lock (cache)
            {
                if (cache.TryGetValue(b.name, out var list)) return list;
                list = new List<Landmark>();
                foreach (var d in Defs.Concat(RandomSites(b)))   // milestone 39: and the random places
                {
                    if (d.body != b.name) continue;
                    if (d.id.StartsWith("site.")) randomDefs[d.id] = d;
                    int hash = Landmarks.StableHash(d.id);
                    list.Add(new Landmark { id = d.id, name = d.name, body = d.body, kind = d.kind, dir = CelestialBody.DirectionFromLatLon(d.lat, d.lon), reach = Reach,
                        blurb = d.blurb, yaw = (hash & 0xff) * 1.4f, seed = (hash >> 8) & 0x7fff });
                }
                cache[b.name] = list;
                return list;
            }
        }

        /// <summary>The nearest one a vessel could search (same world, on the ground, within reach), or null.</summary>
        public static Landmark Near(VesselSim v, double ut, out double metres)
        {
            metres = double.MaxValue;
            if (v == null || v.destroyed || !v.landed) return null;
            Landmark best = null;
            Vector3d here = v.body.InertialToFixed(v.pos, ut).normalized;
            foreach (var l in On(v.body))
            {
                double m = Vector3d.Angle(here, l.dir) * v.body.radius;
                if (m < metres) { metres = m; best = l; }
            }
            return metres <= Reach ? best : null;
        }

        /// <summary>
        /// Searches an outpost: fuel into the tanks (not boosters), monopropellant, supplies, and science. Returns what was
        /// found, or why not. The ship to fill is the one the astronaut walked from, if they're on foot.
        /// </summary>
        public static string Scavenge(Landmark l, VesselSim ship, Career c)
        {
            var d = Get(l?.id);
            if (d == null || ship == null) return "Nothing to search here.";
            if (c != null && c.scavenged.Contains(d.id)) return $"{d.name} has already been searched.";
            double fuel = d.fuel, mono = d.mono;
            double gotFuel = 0, gotMono = 0;
            foreach (var p in ship.parts)
            {
                if (fuel > 0 && p.def.propellant > 0 && !p.def.IsSolid)
                {
                    double add = Math.Min(fuel, p.def.propellant - p.propellant);
                    if (add > 0) { p.propellant += add; fuel -= add; gotFuel += add; }
                }
                if (mono > 0 && p.def.mono > 0)
                {
                    double add = Math.Min(mono, p.def.mono - p.mono);
                    if (add > 0) { p.mono += add; mono -= add; gotMono += add; }
                }
            }
            double gotSupplies = ship.CrewCount > 0 ? ship.AddSupplies(d.supplies) : 0;
            ship.RecomputeMass();
            if (c != null)
            {
                c.scavenged.Add(d.id);
                c.science += d.science;
            }
            var bits = new List<string>();
            if (gotFuel > 0.01) bits.Add($"{gotFuel:0.0} t of fuel");
            if (gotMono > 0.01) bits.Add($"{gotMono * 1000:0} kg of thruster fuel");
            if (gotSupplies > 0.5) bits.Add($"{gotSupplies:0} days of supplies");
            bits.Add($"{d.science:0} science from its logs");
            return $"Searched {d.name}: {string.Join(", ", bits)}.";
        }
    }
}
