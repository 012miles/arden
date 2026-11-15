using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public enum GameMode { Sandbox, Career }

    // ======================================================================== tech tree

    public class TechNode
    {
        public string id, name, description;
        public int tier;
        public float cost;               // science
        public string[] parts, requires;
    }

    /// <summary>Nine tiers of research. Each node costs science and unlocks parts; it needs at least one of its 'requires'.</summary>
    public static class TechTree
    {
        public static readonly List<TechNode> Nodes = new List<TechNode>
        {
            new TechNode { id = "start", name = "First Steps", tier = 0, cost = 0, requires = new string[0],
                description = "A capsule, a parachute, a decoupler, tanks, one engine and a thermometer.",
                parts = new[] { "pod.wren", "chute.s", "dec.s", "tank.s1", "tank.s2", "eng.kestrel", "srb.cinder", "srb.flint", "fins.s", "nose.s", "sci.therm" } },
            new TechNode { id = "staging", name = "Staging", tier = 1, cost = 5, requires = new[] { "start" },
                description = "A vacuum engine and bigger tanks: real two-stage rockets. Plus a barometer.",
                parts = new[] { "eng.ember", "tank.s3", "tank.s4", "sci.baro" } },
            new TechNode { id = "survival", name = "Survivability", tier = 1, cost = 5, requires = new[] { "start" },
                description = "Heat shield, escape tower, lights, and a supply crate for longer trips.",
                parts = new[] { "shield.s", "les.s", "light.s", "sup.s" } },
            new TechNode { id = "rocketry", name = "General Rocketry", tier = 2, cost = 15, requires = new[] { "staging" },
                description = "Side boosters, the Swift sustainer and the 2.6 m size.",
                parts = new[] { "dec.r", "adapt.ls", "adapt.sl", "tank.l2", "tank.l4", "fin.r", "eng.swift", "nose.l" } },
            new TechNode { id = "control", name = "Flight Control", tier = 2, cost = 15, requires = new[] { "staging", "survival" },
                description = "Probe cores, antennas, battery packs and sample canisters.",
                parts = new[] { "probe.spark", "antenna.s", "battery.r", "sci.bio" } },
            new TechNode { id = "heavy", name = "Heavy Rocketry", tier = 3, cost = 40, requires = new[] { "rocketry" },
                description = "The Heron and Condor engines, the long 2.6 m tanks and the Blaze booster.",
                parts = new[] { "eng.heron", "eng.condor", "tank.l8", "tank.l16", "dec.l", "fins.l", "fin.l", "srb.blaze" } },
            new TechNode { id = "aviation", name = "Aviation", tier = 3, cost = 40, requires = new[] { "control" },
                description = "Cockpit, straight wings, control surfaces and landing gear.",
                parts = new[] { "cockpit.swift", "wing.std", "elevon", "tail.fin", "gear.s", "tail.cone" } },
            new TechNode { id = "finecontrol", name = "Fine Control", tier = 4, cost = 80, requires = new[] { "control" },
                description = "RCS thrusters, monopropellant, payload fairings and the Vireo core (every SAS mode).",
                parts = new[] { "rcs.b", "mono.s", "mono.r", "fairing.s", "probe.vireo" } },
            new TechNode { id = "landing", name = "Landing", tier = 5, cost = 100, requires = new[] { "rocketry", "control" },
                description = "Landing legs, the Wisp lander engine and a ground probe.",
                parts = new[] { "leg.s", "eng.wisp", "sci.ground" } },
            new TechNode { id = "jets", name = "Jet Engines", tier = 5, cost = 150, requires = new[] { "aviation" },
                description = "The Gale turbojet and the big delta wing.",
                parts = new[] { "eng.gale", "wing.delta" } },
            new TechNode { id = "electrics", name = "Electrics", tier = 4, cost = 70, requires = new[] { "control" },
                description = "Solar panels and battery banks: power for long missions.",
                parts = new[] { "solar.s", "battery.s" } },
            new TechNode { id = "relays", name = "Relay Networks", tier = 6, cost = 220, requires = new[] { "electrics" },
                description = "Relay dishes: pass signals around Arden and Tal.",
                parts = new[] { "relay.s" } },
            new TechNode { id = "mining", name = "Ore Mining", tier = 6, cost = 200, requires = new[] { "electrics", "landing" },
                description = "Survey scanners to map ore from orbit, drills, ore tanks and a radiator strip.",
                parts = new[] { "scan.orbit", "drill.s", "ore.r", "ore.s", "rad.s", "cool.s" } },   // (milestone 39: the coolant tank)
            new TechNode { id = "refining", name = "Ore Refining", tier = 7, cost = 380, requires = new[] { "mining" },
                description = "Turn ore into fuel where you land: the converter, radiator panels, big solar arrays and heavy drills.",
                parts = new[] { "isru.s", "rad.r", "solar.l", "drill.l", "ore.l" } },
            new TechNode { id = "deepspace", name = "Deep Space", tier = 8, cost = 600, requires = new[] { "relays" },
                description = "The deep space dish: a signal home from the other planets.",
                parts = new[] { "dish.d" } },
            new TechNode { id = "rovers", name = "Rovers", tier = 6, cost = 180, requires = new[] { "electrics", "landing" },
                description = "Electric wheels in two sizes and a rover frame with a battery: drive across Tal and Vesk.",
                parts = new[] { "wheel.s", "wheel.l", "frame.r" } },
            new TechNode { id = "lifesupport", name = "Life Support", tier = 5, cost = 120, requires = new[] { "electrics" },
                description = "For long voyages: the supply module (150 crew-days) and a greenhouse that grows food for two.",
                parts = new[] { "sup.l", "garden.s" } },
            new TechNode { id = "outposts", name = "Outposts", tier = 6, cost = 260, requires = new[] { "lifesupport" },
                description = "Build bases: the Base Hub, a Research Lab and a storage tank. Astronauts cable landed modules together on EVA.",
                parts = new[] { "base.hub", "base.lab", "base.tank" } },
            new TechNode { id = "far", name = "Far Reaches", tier = 8, cost = 800, requires = new[] { "deepspace" },
                description = "For the giant: the Lumen Drive (880 s) and the Great Dish, which calls home from Brume.",
                parts = new[] { "eng.lumen", "dish.x" } },
            new TechNode { id = "wanderers", name = "Wanderers", tier = 8, cost = 450, requires = new[] { "docking" },
                description = "The grabber: latch onto a passing wanderer, push it into orbit, and take rock samples.",
                parts = new[] { "grab.s" } },
            new TechNode { id = "docking", name = "Docking", tier = 7, cost = 300, requires = new[] { "finecontrol" },
                description = "Docking ports in both sizes.",
                parts = new[] { "port.sm", "port.std" } },
            new TechNode { id = "habitation", name = "Space Habitation", tier = 8, cost = 500, requires = new[] { "docking", "relays" },
                description = "A habitat module for four.",
                parts = new[] { "hab.s" } },
        };

        public static TechNode Get(string id) => Nodes.Find(n => n.id == id);
        public static TechNode NodeOf(string partId) => Nodes.Find(n => Array.IndexOf(n.parts, partId) >= 0);
    }

    // ======================================================================== prices

    public static class Prices
    {
        public const float FuelPerT = 150f, MonoPerT = 600f, SolidPerT = 60f;

        static readonly Dictionary<string, float> Cost = new Dictionary<string, float>
        {
            { "pod.wren", 600 }, { "probe.spark", 450 }, { "chute.s", 400 }, { "shield.s", 300 }, { "les.s", 550 }, { "nose.s", 120 },
            { "tank.s1", 200 }, { "tank.s2", 350 }, { "tank.s4", 650 }, { "tank.l4", 1300 }, { "tank.l8", 2500 },
            { "eng.kestrel", 1100 }, { "eng.ember", 950 }, { "eng.wisp", 700 }, { "eng.heron", 5200 }, { "srb.cinder", 450 },
            { "dec.s", 300 }, { "dec.l", 700 }, { "dec.r", 350 }, { "adapt.ls", 400 }, { "fins.s", 250 }, { "fins.l", 600 }, { "fin.r", 120 },
            { "rcs.b", 480 }, { "mono.s", 500 }, { "mono.r", 300 }, { "leg.s", 440 }, { "light.s", 100 }, { "fairing.s", 600 },
            { "cockpit.swift", 1500 }, { "tail.cone", 300 }, { "eng.gale", 3500 }, { "wing.delta", 900 }, { "wing.std", 600 },
            { "elevon", 400 }, { "tail.fin", 500 }, { "gear.s", 400 }, { "port.sm", 800 }, { "port.std", 1400 }, { "hab.s", 4000 },
            { "solar.s", 750 }, { "antenna.s", 450 }, { "battery.r", 150 }, { "battery.s", 550 }, { "relay.s", 1800 }, { "sci.therm", 300 }, { "sci.baro", 450 }, { "sci.bio", 900 }, { "sci.ground", 1300 },
            { "solar.l", 2800 }, { "scan.orbit", 2500 }, { "drill.s", 2200 }, { "drill.l", 6000 }, { "ore.r", 300 }, { "ore.s", 800 }, { "ore.l", 3000 },
            { "isru.s", 7000 }, { "rad.r", 900 }, { "rad.s", 500 }, { "cool.s", 400 }, { "probe.vireo", 1600 },
            { "wheel.s", 600 }, { "wheel.l", 1500 }, { "frame.r", 900 }, { "dish.d", 6000 },
            { "tank.s3", 500 }, { "tank.l2", 700 }, { "tank.l16", 4600 }, { "eng.swift", 1500 }, { "eng.condor", 6500 },
            { "srb.flint", 250 }, { "srb.blaze", 2400 }, { "nose.l", 300 }, { "adapt.sl", 400 }, { "fin.l", 220 },
            { "grab.s", 4000 }, { "sup.s", 400 }, { "sup.l", 2500 }, { "garden.s", 6000 }, { "base.hub", 9000 }, { "base.lab", 12000 }, { "base.tank", 3500 }, { "eng.lumen", 18000 }, { "dish.x", 12000 },
        };

        static float FuelValue(PartDef d, double propellant, double mono) =>
            (float)(propellant * (d.IsSolid ? SolidPerT : FuelPerT) + mono * MonoPerT);

        /// <summary>Price of a part with full tanks.</summary>
        public static float Of(PartDef d) => d == null ? 0 : (Cost.TryGetValue(d.id, out float c) ? c : 200f) + FuelValue(d, d.propellant, d.mono);

        /// <summary>What the part is worth now: the dry part plus whatever fuel is left.</summary>
        public static float ValueOf(PartState p) =>
            (Cost.TryGetValue(p.def.id, out float c) ? c : 0f) + FuelValue(p.def, p.propellant, p.mono);

        public static float Of(VesselDesign d) { float s = 0; foreach (var n in d.nodes) s += Of(PartCatalog.Get(n.id)); return s; }

        /// <summary>Share of a recovered vessel's value you get back: 98% at the launch site, falling to 20% a quarter of the way round the world.</summary>
        public static double RecoveryFactor(double distanceM, double bodyRadius)
        {
            double quarter = Math.PI * bodyRadius / 2;
            return Math.Max(0.2, 0.98 - 0.78 * Math.Min(1, distanceM / quarter));
        }
    }

    // ======================================================================== science

    public enum Situation { LaunchSite, Landed, FlyingLow, FlyingHigh, SpaceLow, SpaceHigh }

    public class ExperimentDef
    {
        public string id, name, verb;
        public float baseValue, transmit;
        public Situation[] situations;
        public bool needsAir;
        public bool singleUse;   // once its result is sent or thrown away, a scientist has to clean it out before it works again
    }

    /// <summary>A science result waiting to be sent home or recovered.</summary>
    [Serializable]
    public class ScienceData
    {
        public string experiment, subject, title;
        public float transmit;
    }

    [Serializable]
    public class SubjectSave { public string id; public float collected, cap; }

    public static class Science
    {
        static readonly Situation[] All = { Situation.LaunchSite, Situation.Landed, Situation.FlyingLow, Situation.FlyingHigh, Situation.SpaceLow, Situation.SpaceHigh };
        static readonly Situation[] Air = { Situation.LaunchSite, Situation.Landed, Situation.FlyingLow, Situation.FlyingHigh };

        public static readonly List<ExperimentDef> Experiments = new List<ExperimentDef>
        {
            new ExperimentDef { id = "crew", name = "Crew report", verb = "Crew report", baseValue = 5, transmit = 1f, situations = All },
            new ExperimentDef { id = "temp", name = "Temperature scan", verb = "Temperature scan", baseValue = 6, transmit = 1f, situations = All },
            new ExperimentDef { id = "pressure", name = "Pressure scan", verb = "Pressure scan", baseValue = 9, transmit = 1f, situations = Air, needsAir = true },
            new ExperimentDef { id = "samples", name = "Sample collection", verb = "Samples", baseValue = 15, transmit = 0.3f, situations = All, singleUse = true },
            new ExperimentDef { id = "ground", name = "Ground probe", verb = "Ground probe", baseValue = 25, transmit = 0.5f, situations = new[] { Situation.LaunchSite, Situation.Landed } },
            new ExperimentDef { id = "eva", name = "EVA report", verb = "EVA report", baseValue = 8, transmit = 1f, situations = All },
            new ExperimentDef { id = "surface", name = "Surface sample", verb = "Surface sample", baseValue = 30, transmit = 0.25f, situations = new[] { Situation.LaunchSite, Situation.Landed } },
            new ExperimentDef { id = "rock", name = "Rock sample", verb = "Rock sample", baseValue = 40, transmit = 0.3f, situations = All },
        };

        public static ExperimentDef Get(string id) => Experiments.Find(e => e.id == id);

        public static string Describe(Situation s) => s switch
        {
            Situation.LaunchSite => "at the launch site",
            Situation.Landed => "on the surface",
            Situation.FlyingLow => "flying low",
            Situation.FlyingHigh => "flying high",
            Situation.SpaceLow => "in space near",
            _ => "in space high over",
        };

        /// <summary>Where the vessel is, for science and contracts.</summary>
        public static Situation SituationOf(VesselSim v, double ut)
        {
            var b = v.body;
            if (v.landed || v.RadarAltitude(ut) < 2)
            {
                if (b.IsHomeWorld)
                {
                    Vector3d fixedDir = b.InertialToFixed(v.pos, ut).normalized;
                    double dPad = Vector3d.Angle(fixedDir, LaunchSite.Direction) * b.radius;
                    double dRwy = Vector3d.Angle(fixedDir, LaunchSite.DirectionAt(b, LaunchSite.RunwayEast, LaunchSite.RunwayNorth)) * b.radius;
                    if (Math.Min(dPad, dRwy) < 2500) return Situation.LaunchSite;
                }
                return Situation.Landed;
            }
            double alt = v.Altitude;
            if (b.HasAtmosphere && alt < b.atmosphereHeight) return alt < 18_000 ? Situation.FlyingLow : Situation.FlyingHigh;
            double split = b.HasAtmosphere ? 250_000 : 60_000;
            return alt < split ? Situation.SpaceLow : Situation.SpaceHigh;
        }

        public static float Multiplier(CelestialBody b, Situation s)
        {
            if (b.IsHomeWorld)
                return s switch { Situation.LaunchSite => 0.3f, Situation.Landed => 0.4f, Situation.FlyingLow => 0.7f, Situation.FlyingHigh => 0.9f, Situation.SpaceLow => 1f, _ => 1.5f };
            return (s switch { Situation.LaunchSite or Situation.Landed => 4f, Situation.SpaceLow => 3f, _ => 2f }) * b.scienceMult;
        }

        /// <summary>Landed and low-flying results count separately in each region of a world.</summary>
        public static bool Regional(Situation s) => s == Situation.Landed || s == Situation.FlyingLow;

        public static string SubjectId(string exp, CelestialBody b, Situation s, string region = null) =>
            Regional(s) && !string.IsNullOrEmpty(region) ? $"{exp}@{b.name}:{s}:{region}" : $"{exp}@{b.name}:{s}";

        public static string SubjectTitle(ExperimentDef e, CelestialBody b, Situation s, string region = null)
        {
            bool r = Regional(s) && !string.IsNullOrEmpty(region);
            return s == Situation.LaunchSite ? $"{e.verb} at {b.name}'s launch site"
                : s == Situation.Landed ? (r ? $"{e.verb} from {b.name}'s {region}" : $"{e.verb} from {b.name}'s surface")
                : s == Situation.FlyingLow || s == Situation.FlyingHigh ? (r ? $"{e.verb} {Describe(s)} over {b.name}'s {region}" : $"{e.verb} {Describe(s)} over {b.name}")
                : $"{e.verb} {Describe(s)} {b.name}";
        }

        /// <summary>The region a result is from, or null for one that isn't regional.</summary>
        public static string RegionOf(VesselSim v, Situation s, double ut) => Regional(s) ? Regions.Under(v, ut) : null;

        public static bool Allowed(ExperimentDef e, CelestialBody b, Situation s) =>
            Array.IndexOf(e.situations, s) >= 0 && (!e.needsAir || b.HasAtmosphere);
    }

    /// <summary>Running, sending and keeping experiments aboard a vessel.</summary>
    public static class Lab
    {
        /// <summary>Every experiment the vessel can run: science parts, plus a crew report from each crewed command part.</summary>
        public static List<(PartState part, ExperimentDef exp)> On(VesselSim v)
        {
            var list = new List<(PartState, ExperimentDef)>();
            foreach (var p in v.parts)
            {
                if (!string.IsNullOrEmpty(p.def.experiment)) { var e = Science.Get(p.def.experiment); if (e != null) list.Add((p, e)); }
                else if (p.def.kind == PartKind.Command && p.def.crew > 0 && p.crew.Count > 0) list.Add((p, Science.Get("crew")));
                else if (p.def.kind == PartKind.Eva && p.crew.Count > 0) { list.Add((p, Science.Get("eva"))); list.Add((p, Science.Get("surface"))); }
            }
            return list;
        }

        public static ScienceData Held(PartState p, ExperimentDef e) => p.science.Find(d => d.experiment == e.id);

        /// <summary>Sending needs a link home (any working antenna, including a capsule's or probe's own) and 10 EC.</summary>
        public static bool CanTransmit(VesselSim v) => v.connected && v.EcStored >= VesselSim.TransmitEc;

        /// <summary>The wanderer a grabber is holding (for rock samples), or null.</summary>
        public static string RockFor(VesselSim v, PartState p)
        {
            var r = p != null ? v.HeldRock(p) : null;
            return r?.dockedName;
        }

        /// <summary>Subject id and title: a rock sample is its own subject for each wanderer.</summary>
        static (string id, string title) Subject(VesselSim v, PartState p, ExperimentDef e, Situation sit, double ut)
        {
            if (e.id == "rock") { string rock = RockFor(v, p) ?? "?"; return ($"rock@{v.body.name}:{sit}:{rock}", $"Rock sample from {rock}"); }
            string region = Science.RegionOf(v, sit, ut);
            return (Science.SubjectId(e.id, v.body, sit, region), Science.SubjectTitle(e, v.body, sit, region));
        }

        /// <summary>What running it here would be worth (recovered), or -1 if it can't run here.</summary>
        public static float Preview(Career c, VesselSim v, ExperimentDef e, double ut, PartState p = null)
        {
            var sit = Science.SituationOf(v, ut);
            if (!Science.Allowed(e, v.body, sit)) return -1;
            if (e.id == "rock" && RockFor(v, p) == null) return -1;   // the grabber has to be holding a wanderer
            float cap = e.baseValue * Science.Multiplier(v.body, sit);
            return c.Remaining(Subject(v, p, e, sit, ut).id, cap);
        }

        public static string Run(Career c, VesselSim v, PartState p, ExperimentDef e, double ut)
        {
            if (Held(p, e) != null) return $"The {p.def.name} already holds a result. Send it or discard it first.";
            if (p.used) return $"The {p.def.name} has been used. A scientist aboard can clean it out (Reset).";
            var sit = Science.SituationOf(v, ut);
            if (!Science.Allowed(e, v.body, sit)) return $"{e.name}: nothing to measure {Science.Describe(sit)} {v.body.name}.";
            if (e.id == "rock" && RockFor(v, p) == null) return "The grabber needs to be holding a wanderer to take a sample.";
            var (id, title) = Subject(v, p, e, sit, ut);
            var d = new ScienceData { experiment = e.id, subject = id, title = title, transmit = e.transmit };
            p.science.Add(d);
            float worth = Preview(c, v, e, ut, p);
            return $"{d.title}: worth {worth:0.0} science brought home" + (e.transmit < 1 ? $" ({worth * e.transmit:0.0} if sent)" : "");
        }

        public static string Transmit(Career c, VesselSim v, PartState p, ScienceData d, IList<CelestialBody> bodies)
        {
            if (!v.connected) return "No signal: you need a link home to send science (antenna range, or a relay).";
            if (v.EcStored < VesselSim.TransmitEc) return $"Not enough power to transmit (needs {VesselSim.TransmitEc:0} EC).";
            v.AddCharge(-VesselSim.TransmitEc);
            float factor = TransmitFactor(v, d);
            float gain = c.Credit(d, factor, bodies);
            p.science.Remove(d);
            if (Science.Get(d.experiment)?.singleUse == true) p.used = true;
            return $"Sent: {d.title}. +{gain:0.0} science" + (factor > d.transmit + 1e-4f ? $" ({factor * 100:0}% with the scientist's help)" : "");
        }

        /// <summary>Share of a result's value that sending it gets: the experiment's rate, plus the best scientist's bonus.</summary>
        public static float TransmitFactor(VesselSim v, ScienceData d) => d.transmit >= 1 ? 1 : Math.Min(1f, d.transmit + (float)Crew.TransmitBonus(v.BestLevel(CrewRole.Scientist)));

        /// <summary>Throws a result away (a single-use instrument then needs cleaning).</summary>
        public static void Discard(PartState p, ScienceData d)
        {
            p.science.Remove(d);
            if (Science.Get(d.experiment)?.singleUse == true) p.used = true;
        }

        /// <summary>A scientist cleans out a used sample canister so it can collect again.</summary>
        public static string Reset(VesselSim v, PartState p)
        {
            if (!p.used) return $"The {p.def.name} is ready.";
            if (v.BestLevel(CrewRole.Scientist) < 0) return $"Only a scientist can clean out the {p.def.name}. Bring one along.";
            p.used = false;
            return $"The {p.def.name} is clean and ready to collect again.";
        }
    }

    // ======================================================================== contracts and records

    public enum ContractState { Offered, Active, Completed, Failed }

    [Serializable]
    public class Contract
    {
        public int id;
        public string type, title, description;
        public string body, partId, situation;
        public string region;            // science: a named region of the body (empty = anywhere)
        public double altitude, ap, pe;
        public string crewName;          // rescue: who is stranded
        public string target;            // capture: the wanderer's name
        public int vesselId, seats;      // rescue: the stranded vessel (0 = not spawned yet); base: crew seats needed
        public double lat, lon, radiusKm; // base: where (radius 0 = anywhere on the body)
        public double amount;            // survey: share of the surface to map; mine: tonnes of ore to have aboard
        public double advance, reward, penalty, scienceReward, repReward;
        public double deadline, acceptedUt;
        public ContractState state;

        public string RewardText => $"{advance:N0} up front, {reward:N0} on completion" + (scienceReward > 0 ? $", +{scienceReward:0} science" : "") + $", +{repReward:0} rep";
    }

    /// <summary>One-time firsts, as bits.</summary>
    public static class Records
    {
        public const int Launch = 1, Space = 2, Orbit = 4, CrewedOrbit = 8, TalFlyby = 16, TalOrbit = 32, TalLanding = 64, TalReturn = 128, Docking = 256,
            OreMined = 512, OreRefined = 1024, FirstEva = 2048, TalFlag = 4096,
            VeskFlyby = 8192, VeskOrbit = 16384, VeskLanding = 32768, VeskReturn = 65536, VeskFlag = 131072, RoverTal = 262144, RoverVesk = 524288, Escape = 1048576,
            OchreFlyby = 1 << 21, OchreOrbit = 1 << 22, OchreLanding = 1 << 23, OchreReturn = 1 << 24, MoteLanding = 1 << 25,
            FirstGrab = 1 << 26, FirstCapture = 1 << 27,
            BrumeFlyby = 1 << 28, BrumeOrbit = 1 << 29, BrumeMoonLanding = 1 << 30;
        public static readonly (int bit, string name, float funds, float rep)[] List =
        {
            (Launch, "First launch", 2_000, 1), (Space, "First time in space", 8_000, 3), (Orbit, "First orbit", 15_000, 5),
            (CrewedOrbit, "First crewed orbit", 10_000, 5), (TalFlyby, "First trip to Tal", 20_000, 6), (TalOrbit, "First orbit of Tal", 25_000, 6),
            (TalLanding, "First landing on Tal", 40_000, 10), (TalReturn, "First return from Tal", 50_000, 12), (Docking, "First docking", 20_000, 6),
            (OreMined, "First ore mined", 15_000, 4), (OreRefined, "First fuel made from ore", 20_000, 5),
            (FirstEva, "First EVA", 6_000, 3), (TalFlag, "First flag on Tal", 25_000, 8),
            (VeskFlyby, "First trip to Vesk", 35_000, 8), (VeskOrbit, "First orbit of Vesk", 40_000, 8),
            (VeskLanding, "First landing on Vesk", 60_000, 12), (VeskReturn, "First return from Vesk", 75_000, 14),
            (VeskFlag, "First flag on Vesk", 30_000, 9),
            (RoverTal, "First drive on Tal", 30_000, 8), (RoverVesk, "First drive on Vesk", 40_000, 9),
            (Escape, "First escape from Arden", 60_000, 12),
            (OchreFlyby, "First trip to Ochre", 120_000, 15), (OchreOrbit, "First orbit of Ochre", 140_000, 15),
            (OchreLanding, "First landing on Ochre", 220_000, 22), (OchreReturn, "First return from Ochre", 300_000, 28),
            (MoteLanding, "First landing on Mote", 160_000, 16),
            (FirstGrab, "First wanderer grabbed", 40_000, 10), (FirstCapture, "First wanderer put in orbit", 90_000, 16),
            (BrumeFlyby, "First trip to Brume", 260_000, 24), (BrumeOrbit, "First orbit of Brume", 300_000, 26),
            (BrumeMoonLanding, "First landing on a moon of Brume", 450_000, 34),
        };
    }

    /// <summary>Progress bits a vessel earns as it flies (kept in the save; used by records and contracts).</summary>
    public static class VFlags
    {
        public const int Launched = 1, Space = 2, OrbitHome = 4, SoiTal = 8, OrbitTal = 16, LandedTal = 32, Docked = 64, CrewedOrbit = 128,
            Mined = 256, Refined = 512, SoiVesk = 1024, OrbitVesk = 2048, LandedVesk = 4096, DroveTal = 8192, DroveVesk = 16384, SoiSun = 32768,
            SoiOchre = 1 << 16, OrbitOchre = 1 << 17, LandedOchre = 1 << 18, SoiMote = 1 << 19, LandedMote = 1 << 20,
            SoiBrume = 1 << 21, OrbitBrume = 1 << 22, LandedRime = 1 << 23, LandedPyre = 1 << 24, SoiRime = 1 << 25, SoiPyre = 1 << 26;

        /// <summary>The "landed there" flag for a world (for return contracts), or 0.</summary>
        public static int LandedOn(string body) => body == "Rime" ? LandedRime : body == "Pyre" ? LandedPyre : body == "Tal" ? LandedTal : body == "Vesk" ? LandedVesk : body == "Ochre" ? LandedOchre : body == "Mote" ? LandedMote : 0;
    }

    [Serializable]
    public class CareerSave
    {
        public int version;
        public GameMode mode;
        public bool chosen;
        public double funds, science, reputation;
        public List<string> unlocked = new List<string>();
        public List<SubjectSave> subjects = new List<SubjectSave>();
        public List<Contract> contracts = new List<Contract>();
        public int records, nextContractId = 1, seed = 1234;
        public List<int> facilities = new List<int>();
        public List<string> strategies = new List<string>();
        public List<CrewMember> roster = new List<CrewMember>();
        public int guide = Guide.Closed;   // older saves: their players are past the first flight
        public bool suppliesOn;            // older saves: off
        public List<string> badges = new List<string>();   // achievements earned in this save (milestone 23)
        public List<string> landmarksSpotted = new List<string>(), landmarksVisited = new List<string>();   // milestone 27
        public int storyStep;                  // milestone 28: the story mission in progress
        public bool storyStarted, storyBriefed;
        public List<string> scavenged = new List<string>();   // milestone 34: outposts and wrecks searched
        public bool creditsSeen;               // milestone 35: the credits rolled after the story
        public int presetSlots;                // milestone 34: slots open for your own presets (older saves: 0, read as 1)
        // milestone 38: survival (older saves: survivalVersion 0, given the starting kit)
        public int survivalVersion;
        public List<Vitals> vitals = new List<Vitals>();
        public List<string> backpack = new List<string>(), locker = new List<string>();
        public int backpackSize, lockerSize;
    }

    /// <summary>
    /// Career: funds, science and reputation, the tech tree, science subjects, contracts and records.
    /// Plain C# (no Unity objects), saved inside the universe file. Sandbox mode unlocks everything and charges nothing.
    /// </summary>
    public partial class Career
    {
        public const double StartFunds = 25_000;
        /// <summary>Achievements earned in this save, and ones earned but not yet shown in a pop-up.</summary>
        public readonly List<string> badges = new List<string>(), badgeNews = new List<string>();
        /// <summary>Landmarks seen (a "?" on the map) and found (landed beside), by id (milestone 27).</summary>
        public readonly List<string> spotted = new List<string>(), visited = new List<string>();
        /// <summary>Outposts and wrecks searched already (milestone 34; each once per game).</summary>
        public readonly List<string> scavenged = new List<string>();
        /// <summary>The story (milestone 28): which mission, whether a save's already-done steps were skipped, whether this one's been briefed.</summary>
        public int storyStep;
        public bool storyStarted, storyBriefed;
        /// <summary>Mission control's words for missions just done (shown in flight, not saved).</summary>
        public readonly List<(string who, string text)> storyNews = new List<(string, string)>();
        public const int OfferCount = 4;

        public GameMode mode = GameMode.Sandbox;
        public bool chosen;
        public double funds = StartFunds, science, reputation;
        /// <summary>How many slots for the player's own presets this campaign has opened (milestone 34; the first is free).</summary>
        public int presetSlots = 1;
        /// <summary>The credits have rolled at the end of the story in this game (milestone 35).</summary>
        public bool creditsSeen;
        public readonly HashSet<string> unlocked = new HashSet<string> { "start" };
        public readonly Dictionary<string, SubjectSave> subjects = new Dictionary<string, SubjectSave>();
        public readonly List<Contract> contracts = new List<Contract>();
        public int records, nextContractId = 1, seed = 1234;
        /// <summary>The first-flight guide's step (Guide.Closed once done or dismissed).</summary>
        public int guide = Guide.Closed;
        /// <summary>Crew need supplies on long trips (and die when they run out). A choice made when the game starts.</summary>
        public bool suppliesOn;

        /// <summary>A crew member dies (out of supplies, say): lost for good, with the news.</summary>
        public void LoseCrew(string who, string vesselName, string why)
        {
            var m = roster.Find(c => c.name == who);
            if (m != null && m.state != 2) m.state = 2;
            if (IsCareer) reputation = Math.Max(0, reputation - 5);
            news.Add($"{who} died aboard {vesselName}: {why}." + (IsCareer ? " -5 reputation" : ""));
        }

        /// <summary>Wanderers in Arden's space right now (free or held), kept up to date by the universe; for capture offers.</summary>
        public readonly List<string> liveWanderers = new List<string>();
        /// <summary>Things that happened (contract done, record set, science gained), for the game to show.</summary>
        public readonly List<string> news = new List<string>();

        public bool IsCareer => mode == GameMode.Career;

        public void Start(GameMode m)
        {
            mode = m;
            chosen = true;
            funds = StartFunds;
            science = reputation = 0;
            presetSlots = 1;
            creditsSeen = false;
            scavenged.Clear();
            ResetSurvival();   // milestone 38
            unlocked.Clear();
            unlocked.Add("start");
            subjects.Clear();
            contracts.Clear();
            records = 0;
            nextContractId = 1;
            seed = Environment.TickCount & 0xffff;
            news.Clear();
            guide = Guide.Launch;
            suppliesOn = m == GameMode.Career;
            StartCenter();
            if (IsCareer) RefreshOffers(0);
        }

        // ---------------------------------------------------------------- parts and tech

        public bool IsUnlocked(string partId)
        {
            if (!IsCareer) return true;
            var n = TechTree.NodeOf(partId);
            return n == null || unlocked.Contains(n.id);
        }

        public bool CanResearch(TechNode n) => IsCareer && !unlocked.Contains(n.id) && science >= n.cost - 1e-6 && n.tier <= MaxResearchTier &&
                                              (n.requires.Length == 0 || n.requires.Any(r => unlocked.Contains(r)));

        public bool Research(TechNode n)
        {
            if (!CanResearch(n)) return false;
            science -= n.cost;
            unlocked.Add(n.id);
            news.Add($"Researched {n.name}: {string.Join(", ", n.parts.Select(p => PartCatalog.Get(p)?.name ?? p))}");
            return true;
        }

        /// <summary>Why a design can't be launched in career mode (locked parts, not enough money), or null.</summary>
        public string LaunchProblem(VesselDesign d, bool runway = false)
        {
            if (!IsCareer) return null;
            var locked = d.nodes.Select(n => n.id).Distinct().Where(id => !IsUnlocked(id)).ToList();
            if (locked.Count > 0) return $"Not researched yet: {string.Join(", ", locked.Select(id => PartCatalog.Get(id)?.name ?? id))}";
            string limits = FacilityProblem(d, runway);
            if (limits != null) return limits;
            double cost = LaunchCost(d);
            if (funds < cost) return $"It costs {cost:N0}; you have {funds:N0}";
            return null;
        }

        public void ChargeLaunch(VesselDesign d)
        {
            if (!IsCareer) return;
            funds -= LaunchCost(d);
        }

        // ---------------------------------------------------------------- science

        SubjectSave Subject(string id, float cap)
        {
            if (!subjects.TryGetValue(id, out var s)) subjects[id] = s = new SubjectSave { id = id, collected = 0, cap = cap };
            return s;
        }

        /// <summary>Science still to be had from a subject (the first result gives the most; repeats give what's left).</summary>
        public float Remaining(string subjectId, float cap)
        {
            var s = subjects.TryGetValue(subjectId, out var x) ? x : null;
            return s == null ? cap : Math.Max(0, s.cap - s.collected);
        }

        public static float CapOf(ScienceData d, IList<CelestialBody> bodies)
        {
            var e = Science.Get(d.experiment);
            ParseSubject(d.subject, bodies, out var b, out var sit);
            return e == null || b == null ? 0 : e.baseValue * Science.Multiplier(b, sit);
        }

        public static void ParseSubject(string subject, IList<CelestialBody> bodies, out CelestialBody b, out Situation s)
        {
            b = null; s = Situation.Landed;
            // "exp@Body:Situation" or, for regional results, "exp@Body:Situation:Region"
            int at = subject.IndexOf('@');
            if (at < 0) return;
            var parts = subject.Substring(at + 1).Split(':');
            if (parts.Length < 2) return;
            foreach (var c in bodies) if (c.name == parts[0]) b = c;
            Enum.TryParse(parts[1], out s);
        }

        /// <summary>Credits a result (fully when recovered, partly when transmitted). Returns the science gained.</summary>
        public float Credit(ScienceData d, float factor, IList<CelestialBody> bodies)
        {
            float cap = CapOf(d, bodies);
            var s = Subject(d.subject, cap);
            float gain = Math.Max(0, (s.cap - s.collected) * factor);
            s.collected += gain;
            if (IsCareer) science += gain;
            OnScienceCredited(d.subject, gain);
            return gain;
        }

        // ---------------------------------------------------------------- recovery

        /// <summary>Recovering a vessel on the home world: refunds part value by distance from the launch site and credits its science.</summary>
        public string Recover(VesselSim v, double ut, IList<CelestialBody> bodies)
        {
            var home = bodies[0];
            double dist = 0;
            if (v.body == home)
            {
                Vector3d fixedDir = home.InertialToFixed(v.pos, ut).normalized;
                dist = Vector3d.Angle(fixedDir, LaunchSite.Direction) * home.radius;
            }
            double factor = Prices.RecoveryFactor(dist, home.radius);
            double value = 0;
            foreach (var p in v.parts) value += Prices.ValueOf(p);
            double refund = value * factor;
            float sci = 0;
            foreach (var p in v.parts)
            {
                foreach (var d in p.science) sci += Credit(d, 1f, bodies);
                p.science.Clear();
            }
            if ((v.careerFlags & (VFlags.OrbitTal | VFlags.LandedTal | VFlags.SoiTal)) != 0) SetRecord(Records.TalReturn);
            if ((v.careerFlags & (VFlags.OrbitVesk | VFlags.LandedVesk | VFlags.SoiVesk)) != 0) SetRecord(Records.VeskReturn);
            if ((v.careerFlags & (VFlags.SoiOchre | VFlags.OrbitOchre | VFlags.LandedOchre | VFlags.SoiMote | VFlags.LandedMote)) != 0) SetRecord(Records.OchreReturn);
            OnRecovered(v);
            string crewNote = ReturnCrew(v);
            if (!IsCareer) return $"{v.name} recovered.";
            funds += refund;
            return $"{v.name} recovered: +{refund:N0} funds ({factor * 100:0}% of {value:N0})" + (sci > 0 ? $", +{sci:0.0} science" : "") + crewNote;
        }

        // ---------------------------------------------------------------- records

        public void SetRecord(int bit)
        {
            if ((records & bit) != 0) return;
            records |= bit;
            foreach (var r in Records.List)
                if (r.bit == bit)
                {
                    if (IsCareer) { var (f, s, rp) = Pay(r.funds, 0, r.rep); news.Add($"Record: {r.name}! +{f:N0} funds, +{rp:0.0} rep"); }
                    else news.Add($"Record: {r.name}!");
                }
        }

        /// <summary>Feeds in what a vessel has achieved (from VesselSim.careerFlags) to set records.</summary>
        public void OnFlags(int flags)
        {
            if ((flags & VFlags.Launched) != 0) SetRecord(Records.Launch);
            if ((flags & VFlags.Space) != 0) SetRecord(Records.Space);
            if ((flags & VFlags.OrbitHome) != 0) SetRecord(Records.Orbit);
            if ((flags & VFlags.CrewedOrbit) != 0) SetRecord(Records.CrewedOrbit);
            if ((flags & VFlags.SoiTal) != 0) SetRecord(Records.TalFlyby);
            if ((flags & VFlags.OrbitTal) != 0) SetRecord(Records.TalOrbit);
            if ((flags & VFlags.LandedTal) != 0) SetRecord(Records.TalLanding);
            if ((flags & VFlags.SoiVesk) != 0) SetRecord(Records.VeskFlyby);
            if ((flags & VFlags.OrbitVesk) != 0) SetRecord(Records.VeskOrbit);
            if ((flags & VFlags.LandedVesk) != 0) SetRecord(Records.VeskLanding);
            if ((flags & VFlags.Docked) != 0) SetRecord(Records.Docking);
            if ((flags & VFlags.Mined) != 0) SetRecord(Records.OreMined);
            if ((flags & VFlags.Refined) != 0) SetRecord(Records.OreRefined);
            if ((flags & VFlags.DroveTal) != 0) SetRecord(Records.RoverTal);
            if ((flags & VFlags.DroveVesk) != 0) SetRecord(Records.RoverVesk);
            if ((flags & VFlags.SoiSun) != 0) SetRecord(Records.Escape);
            if ((flags & VFlags.SoiOchre) != 0) SetRecord(Records.OchreFlyby);
            if ((flags & VFlags.OrbitOchre) != 0) SetRecord(Records.OchreOrbit);
            if ((flags & VFlags.LandedOchre) != 0) SetRecord(Records.OchreLanding);
            if ((flags & VFlags.LandedMote) != 0) SetRecord(Records.MoteLanding);
            if ((flags & VFlags.SoiBrume) != 0) SetRecord(Records.BrumeFlyby);
            if ((flags & VFlags.OrbitBrume) != 0) SetRecord(Records.BrumeOrbit);
            if ((flags & (VFlags.LandedRime | VFlags.LandedPyre)) != 0) SetRecord(Records.BrumeMoonLanding);
        }

        // ---------------------------------------------------------------- contracts

        System.Random Rng() => new System.Random(seed + nextContractId * 7919);

        public IEnumerable<Contract> Offered => contracts.Where(c => c.state == ContractState.Offered);
        public IEnumerable<Contract> Active => contracts.Where(c => c.state == ContractState.Active);

        bool Has(string type, string body = null) => contracts.Any(c => c.type == type && (body == null || c.body == body) && c.state != ContractState.Failed);
        bool HasOpen(string type) => contracts.Any(c => c.type == type && (c.state == ContractState.Offered || c.state == ContractState.Active));
        int OpenCount(string type) => contracts.Count(c => c.type == type && (c.state == ContractState.Offered || c.state == ContractState.Active));

        /// <summary>An offered or active contract asking for the same thing already exists.</summary>
        bool OpenSame(Contract k) => contracts.Any(o => (o.state == ContractState.Offered || o.state == ContractState.Active) &&
            o.type == k.type && o.body == k.body && o.situation == k.situation && o.partId == k.partId && (o.region ?? "") == (k.region ?? ""));

        Contract New(string type, string title, string desc, double reward, double days, double rep, double sci = 0)
        {
            double scale = 1 + Math.Min(0.5, reputation / 200.0); // better pay as your reputation grows
            return new Contract
            {
                id = nextContractId++, type = type, title = title, description = desc, state = ContractState.Offered,
                reward = Math.Round(reward * scale / 100) * 100, advance = Math.Round(reward * scale * 0.2 / 100) * 100,
                penalty = Math.Round(reward * scale * 0.5 / 100) * 100, repReward = rep, scienceReward = sci, deadline = days * 86400,
            };
        }

        /// <summary>A science contract for one named region: landed there, or flying low over it.</summary>
        public Contract RegionScienceOffer(string body, Situation s, string region)
        {
            bool far = body != "Arden";
            string where = s == Situation.FlyingLow ? $"flying low over {body}'s {region}" : $"on {body}'s {region}";
            var c = New("science", $"Science {where}", $"Bring home (or transmit) any science gathered {where}. The map's region view (O) shows where it is.",
                far ? 45_000 : 10_000, far ? 250 : 90, far ? 8 : 3, far ? 14 : 5);
            c.body = body; c.situation = s.ToString(); c.region = region;
            return c;
        }

        /// <summary>Candidate contracts for where the space program is now.</summary>
        List<Contract> Candidates(double ut)
        {
            var list = new List<Contract>();
            var rng = Rng();
            bool space = (records & Records.Space) != 0, orbit = (records & Records.Orbit) != 0, tal = (records & Records.TalFlyby) != 0;
            bool talOrbit = (records & Records.TalOrbit) != 0, talLand = (records & Records.TalLanding) != 0;
            if (!space)
            {
                if (!Has("altitude")) { var c = New("altitude", "Reach 20 km", "Fly any vessel above 20 km over Arden.", 6_000, 30, 2); c.body = "Arden"; c.altitude = 20_000; list.Add(c); }
                if (!Has("space")) { var c = New("space", "Reach space", "Get any vessel above Arden's air (75 km).", 14_000, 40, 4); c.body = "Arden"; list.Add(c); }
            }
            if (!Has("orbit", "Arden") && (space || !HasOpen("space"))) { var c = New("orbit", "Orbit Arden", "Put a vessel into a stable orbit around Arden.", 26_000, 60, 6); c.body = "Arden"; list.Add(c); }
            if (orbit)
            {
                if (!Has("flyby", "Tal")) { var c = New("flyby", "Fly to Tal", "Enter Tal's sphere of influence.", 40_000, 120, 8); c.body = "Tal"; list.Add(c); }
                if (!HasOpen("satellite"))
                {
                    double pe = 90_000 + 10_000 * rng.Next(0, 12), ap = pe + 20_000 * rng.Next(0, 10);
                    var c = New("satellite", $"Satellite: {ap / 1000:0} x {pe / 1000:0} km", $"Put an uncrewed satellite (probe core and antenna) into an orbit of Arden with apoapsis {ap / 1000:0} km and periapsis {pe / 1000:0} km (within 10%).", 30_000 + ap / 20, 90, 6);
                    c.body = "Arden"; c.ap = ap; c.pe = pe; list.Add(c);
                }
                if (!HasOpen("dock") && !Has("dock")) { var c = New("dock", "Dock in orbit", "Dock two vessels together in orbit around Arden.", 45_000, 120, 10); c.body = "Arden"; list.Add(c); }
            }
            if (tal && !Has("orbit", "Tal")) { var c = New("orbit", "Orbit Tal", "Put a vessel into a stable orbit around Tal.", 55_000, 150, 10); c.body = "Tal"; list.Add(c); }
            if ((tal || talOrbit) && !Has("land", "Tal")) { var c = New("land", "Land on Tal", "Touch down safely on Tal.", 90_000, 200, 15); c.body = "Tal"; list.Add(c); }
            if (talLand && !Has("return", "Tal")) { var c = New("return", "Back from Tal's surface", "Land on Tal, then bring that vessel home and recover it.", 140_000, 250, 20); c.body = "Tal"; c.situation = "land"; list.Add(c); }
            // Vesk, the far icy moon, opens up once Tal's been orbited.
            bool vesk = (records & Records.VeskFlyby) != 0, veskLand = (records & Records.VeskLanding) != 0;
            if (talOrbit && !Has("flyby", "Vesk")) { var c = New("flyby", "Fly to Vesk", "Enter the sphere of influence of Vesk, the icy moon 26,000 km out.", 70_000, 250, 12); c.body = "Vesk"; list.Add(c); }
            if (vesk && !Has("orbit", "Vesk")) { var c = New("orbit", "Orbit Vesk", "Put a vessel into a stable orbit around Vesk.", 85_000, 250, 14); c.body = "Vesk"; list.Add(c); }
            if (vesk && !Has("land", "Vesk")) { var c = New("land", "Land on Vesk", "Touch down safely on Vesk's ice.", 130_000, 300, 20); c.body = "Vesk"; list.Add(c); }
            if (veskLand && !Has("return", "Vesk")) { var c = New("return", "Back from Vesk's surface", "Land on Vesk, then bring that vessel home and recover it.", 200_000, 350, 26); c.body = "Vesk"; c.situation = "land"; list.Add(c); }
            // Ochre, the next planet out, once you have got out of Arden's pull (or reached Vesk); Mote once you've been to Ochre.
            bool ochre = (records & Records.OchreFlyby) != 0, ochreLand = (records & Records.OchreLanding) != 0;
            if (((records & Records.Escape) != 0 || (records & Records.VeskOrbit) != 0) && !Has("flyby", "Ochre")) { var c = New("flyby", "Fly to Ochre", "Cross to Ochre, the dusty planet beyond Arden, and enter its sphere of influence. Use the transfer planner in the map (target Ochre).", 160_000, 600, 18); c.body = "Ochre"; list.Add(c); }
            if (ochre && !Has("orbit", "Ochre")) { var c = New("orbit", "Orbit Ochre", "Put a vessel into a stable orbit around Ochre.", 190_000, 600, 20); c.body = "Ochre"; list.Add(c); }
            if (ochre && !Has("land", "Ochre")) { var c = New("land", "Land on Ochre", "Touch down safely on Ochre. Its air is thin: parachutes help, but bring engines too.", 280_000, 700, 26); c.body = "Ochre"; list.Add(c); }
            if (ochre && !Has("land", "Mote")) { var c = New("land", "Land on Mote", "Touch down on Mote, Ochre's little moon.", 200_000, 700, 20); c.body = "Mote"; list.Add(c); }
            // Brume, the ringed giant, once you've been as far as Ochre (or have the Far Reaches parts).
            bool brume = (records & Records.BrumeFlyby) != 0;
            if ((ochre || unlocked.Contains("far")) && !Has("flyby", "Brume")) { var c = New("flyby", "Fly to Brume", "Cross to Brume, the ringed giant three and a half times farther out than Arden, and enter its sphere of influence. The trip takes about five months; the transfer planner (target Brume) finds the window.", 320_000, 900, 26); c.body = "Brume"; list.Add(c); }
            if (brume && !Has("orbit", "Brume")) { var c = New("orbit", "Orbit Brume", "Put a vessel into a stable orbit around Brume. Its deep air makes aerobraking easy, but there's no surface below the clouds.", 360_000, 900, 28); c.body = "Brume"; list.Add(c); }
            if (brume && !Has("land", "Rime")) { var c = New("land", "Land on Rime", "Touch down on Rime, Brume's icy outer moon (1 g: bring a real lander).", 480_000, 1000, 34); c.body = "Rime"; list.Add(c); }
            if (brume && !Has("land", "Pyre")) { var c = New("land", "Land on Pyre", "Touch down on Pyre, Brume's volcanic inner moon. Mind the volcanoes: the lava fields are flat.", 520_000, 1000, 36); c.body = "Pyre"; list.Add(c); }
            if (ochreLand && !Has("return", "Ochre")) { var c = New("return", "Back from Ochre's surface", "Land on Ochre, then bring that vessel home and recover it.", 420_000, 900, 34); c.body = "Ochre"; c.situation = "land"; list.Add(c); }
            // Rovers: once the wheels are researched, drive one around (distance counts on one world at a time).
            if (unlocked.Contains("rovers"))
            {
                if (!Has("rover", "Arden")) { var c = New("rover", "Test drive", "Drive a rover 1 km on Arden (launch it from the runway).", 15_000, 60, 3); c.body = "Arden"; c.amount = 1000; list.Add(c); }
                if (talLand && !Has("rover", "Tal")) { var c = New("rover", "Drive on Tal", "Land a rover on Tal and drive it 2 km.", 110_000, 300, 14); c.body = "Tal"; c.amount = 2000; list.Add(c); }
                if (veskLand && !Has("rover", "Vesk")) { var c = New("rover", "Drive on Vesk", "Land a rover on Vesk and drive it 3 km across the ice.", 160_000, 350, 18); c.body = "Vesk"; c.amount = 3000; list.Add(c); }
            }
            // Wanderers: once the grabber's researched, grab one, then push a named one into orbit.
            if (unlocked.Contains("wanderers"))
            {
                if (!Has("grab")) { var c = New("grab", "Grab a wanderer", "Fly a grabber into any wanderer passing through Arden's space and latch on (slower than 1.5 m/s). Target one from the vessel list (V).", 60_000, 120, 10); c.body = "Arden"; list.Add(c); }
                if (!HasOpen("capture"))
                    foreach (var w in liveWanderers)
                    {
                        int cls = Wanderers.ClassOf(w);
                        if (cls < 0 || contracts.Any(k => k.type == "capture" && k.target == w)) continue;
                        var c = New("capture", $"Capture {w}", $"Grab {w} and push it into a closed orbit around Arden (periapsis above the air, apoapsis under 40,000 km). It's class {"ABC"[cls]}: {PartCatalog.Rocks[cls].dryMass:0} t. If it leaves Arden's space first, the contract fails.",
                            new[] { 110_000, 170_000, 280_000 }[cls], 30, 14 + 4 * cls, 20 + 8 * cls);
                        c.body = "Arden"; c.target = w; list.Add(c);
                        break;
                    }
            }
            // Close flybys: skim a world low without landing (one open at a time)
            if (!HasOpen("skim"))
            {
                var skims = new List<(string body, double alt, double pay, string how)>();
                if (tal && !Has("skim", "Tal")) skims.Add(("Tal", 15_000, 50_000, "Skim Tal below 15 km without landing: a flyby, not an orbit."));
                if (vesk && !Has("skim", "Vesk")) skims.Add(("Vesk", 10_000, 80_000, "Pass Vesk below 10 km without landing."));
                if (ochre && !Has("skim", "Mote")) skims.Add(("Mote", 5_000, 150_000, "Pass within 5 km of Mote's surface without landing."));
                if ((records & Records.BrumeFlyby) != 0 && !Has("skim", "Pyre")) skims.Add(("Pyre", 40_000, 300_000, "Pass Pyre below 40 km: close enough to photograph its calderas."));
                if (skims.Count > 0)
                {
                    var (body, alt, pay, how) = skims[rng.Next(skims.Count)];
                    var c = New("skim", $"Close pass: {body}", how, pay, 200, 8);
                    c.body = body; c.altitude = alt; list.Add(c);
                }
            }
            // Science and part tests: up to two of each open at once, in different places, so there's always work.
            if (OpenCount("science") < 2)
            {
                var places = new List<(string body, Situation s)> { ("Arden", Situation.LaunchSite), ("Arden", Situation.FlyingLow), ("Arden", Situation.FlyingHigh) };
                if (space) places.Add(("Arden", Situation.SpaceLow));
                if (orbit) places.Add(("Arden", Situation.SpaceHigh));
                if (tal) { places.Add(("Tal", Situation.SpaceLow)); places.Add(("Tal", Situation.SpaceHigh)); }
                if (talLand) places.Add(("Tal", Situation.Landed));
                if (vesk) { places.Add(("Vesk", Situation.SpaceLow)); places.Add(("Vesk", Situation.SpaceHigh)); }
                if (veskLand) places.Add(("Vesk", Situation.Landed));
                if (ochre) { places.Add(("Ochre", Situation.SpaceLow)); places.Add(("Ochre", Situation.SpaceHigh)); places.Add(("Ochre", Situation.FlyingHigh)); }
                if (ochreLand) places.Add(("Ochre", Situation.Landed));
                if (brume) { places.Add(("Brume", Situation.SpaceLow)); places.Add(("Brume", Situation.SpaceHigh)); places.Add(("Brume", Situation.FlyingHigh)); }
                foreach (var pl in places)
                {
                    bool far = pl.body != "Arden", pad = pl.s == Situation.LaunchSite;
                    string where = pad ? "at the launch site" : pl.s == Situation.Landed ? $"on {pl.body}'s surface"
                        : $"{Science.Describe(pl.s)} {(pl.s <= Situation.FlyingHigh ? "over " : "")}{pl.body}";
                    var c = New("science", $"Science {where}", $"Bring home (or transmit) any science gathered {where}.",
                        far ? 30_000 : pad ? 3_000 : 8_000, far ? 200 : 60, far ? 6 : pad ? 1 : 2, far ? 10 : pad ? 1 : 4);
                    c.body = pl.body; c.situation = pl.s.ToString(); list.Add(c);
                }
                // A named region to visit: flying low over Arden, or landed on any world you've already landed on.
                var regional = new List<(string body, Situation s)> { ("Arden", Situation.FlyingLow) };
                if (space) regional.Add(("Arden", Situation.Landed));
                if (talLand) regional.Add(("Tal", Situation.Landed));
                if (veskLand) regional.Add(("Vesk", Situation.Landed));
                if (ochreLand) regional.Add(("Ochre", Situation.Landed));
                foreach (var pl in regional)
                {
                    var names = Regions.Names(pl.body);
                    list.Add(RegionScienceOffer(pl.body, pl.s, names[rng.Next(names.Length)]));
                }
            }
            if (OpenCount("test") < 2)
            {
                var tests = new List<(string part, string body, Situation s, double pay)>
                {
                    ("eng.kestrel", "Arden", Situation.FlyingLow, 3_000), ("eng.kestrel", "Arden", Situation.FlyingHigh, 5_000),
                    ("srb.cinder", "Arden", Situation.FlyingLow, 3_000),
                };
                if (unlocked.Contains("staging")) { tests.Add(("eng.ember", "Arden", Situation.FlyingHigh, 7_000)); tests.Add(("eng.ember", "Arden", Situation.SpaceLow, 9_000)); }
                if (unlocked.Contains("heavy")) tests.Add(("eng.heron", "Arden", Situation.FlyingLow, 12_000));
                if (unlocked.Contains("landing") && tal) tests.Add(("eng.wisp", "Tal", Situation.SpaceLow, 25_000));
                if (unlocked.Contains("jets")) tests.Add(("eng.gale", "Arden", Situation.FlyingHigh, 12_000));
                foreach (var t in tests)
                {
                    var def = PartCatalog.Get(t.part);
                    var c = New("test", $"Test the {def.name} {Science.Describe(t.s)}", $"Fire the {def.name} while {Science.Describe(t.s)} {(t.s <= Situation.FlyingHigh ? "over " : "")}{t.body}.", t.pay, 60, 3);
                    c.partId = t.part; c.body = t.body; c.situation = t.s.ToString(); list.Add(c);
                }
            }
            // Mining: map a world's ore from orbit, then dig some up.
            if (unlocked.Contains("mining"))
            {
                if (orbit && !Has("survey", "Arden"))
                {
                    var c = New("survey", "Map Arden's ore", "Map at least half of Arden's surface with a survey scanner. A polar orbit sees it all as Arden turns underneath.", 35_000, 120, 6);
                    c.body = "Arden"; c.amount = 0.5; list.Add(c);
                }
                if (tal && !Has("survey", "Tal"))
                {
                    var c = New("survey", "Map Tal's ore", "Map at least 60% of Tal's surface with a survey scanner in orbit around Tal.", 60_000, 200, 8);
                    c.body = "Tal"; c.amount = 0.6; list.Add(c);
                }
                if (space && !Has("mine", "Arden"))
                {
                    var c = New("mine", "Dig ore on Arden", "Land a vessel with a drill and an ore tank anywhere on Arden's dry land and have 1 t of ore aboard.", 30_000, 120, 5);
                    c.body = "Arden"; c.amount = 1; list.Add(c);
                }
                if (vesk && !Has("survey", "Vesk"))
                {
                    var c = New("survey", "Map Vesk's ice", "Map at least 60% of Vesk's surface with a survey scanner in orbit around Vesk.", 90_000, 300, 10);
                    c.body = "Vesk"; c.amount = 0.6; list.Add(c);
                }
                if (veskLand && !Has("mine", "Vesk"))
                {
                    var c = New("mine", "Mine Vesk's ice", "Land on Vesk with a drill and an ore tank, and have 1.5 t of ore aboard.", 120_000, 350, 14);
                    c.body = "Vesk"; c.amount = 1.5; list.Add(c);
                }
                if (talLand && !Has("mine", "Tal"))
                {
                    var c = New("mine", "Mine ore on Tal", "Land on Tal with a drill and an ore tank, and have 1.5 t of ore aboard.", 85_000, 250, 12);
                    c.body = "Tal"; c.amount = 1.5; list.Add(c);
                }
            }
            AddCenterCandidates(list, rng);
            return list;
        }

        /// <summary>Survey contracts: done when enough of the world has been mapped.</summary>
        public void CheckSurvey(OreSurvey survey, IList<CelestialBody> bodies)
        {
            if (!IsCareer || survey == null) return;
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && x.type == "survey").ToList())
                foreach (var b in bodies)
                    if (b.name == c.body && survey.Coverage(b) >= c.amount - 1e-9) Complete(c);
        }

        /// <summary>Tops the offer list back up (in the builder, between flights).</summary>
        public void RefreshOffers(double ut)
        {
            if (!IsCareer) return;
            int guard = 0;
            while (Offered.Count() < OffersMax && guard++ < 12)
            {
                int saveId = nextContractId;
                var cand = Candidates(ut).Where(c => !OpenSame(c)).ToList();
                nextContractId = saveId; // only the one we keep uses up an id
                if (cand.Count == 0) break;
                // Story contracts (altitude, space, orbit, Tal...) first; repeatable science and tests fill the rest.
                var story = cand.Where(c => c.type != "science" && c.type != "test").ToList();
                var pool = story.Count > 0 && Rng().Next(3) > 0 ? story : cand;
                var pick = pool[Rng().Next(pool.Count)];
                pick.id = nextContractId++;
                contracts.Add(pick);
                // keep at most two science and two tests offered or active
                if ((pick.type == "science" || pick.type == "test") && OpenCount(pick.type) > 2) contracts.Remove(pick);
            }
        }

        public bool Accept(Contract c, double ut)
        {
            if (c.state != ContractState.Offered || Active.Count() >= ActiveLimit) return false;
            c.state = ContractState.Active;
            c.acceptedUt = ut;
            c.deadline = ut + c.deadline;
            if (IsCareer) funds += c.advance;
            return true;
        }

        public void Decline(Contract c)
        {
            if (c.state == ContractState.Offered) contracts.Remove(c);
        }

        void Complete(Contract c)
        {
            c.state = ContractState.Completed;
            var (f, s, r) = Pay(c.reward, c.scienceReward, c.repReward);
            news.Add($"Contract complete: {c.title}! +{f:N0} funds" + (s > 0.05 ? $", +{s:0.0} science" : "") + $", +{r:0.0} rep");
        }

        void Fail(Contract c, string why)
        {
            c.state = ContractState.Failed;
            funds -= c.penalty;
            reputation = Math.Max(0, reputation - c.repReward * 0.5);
            news.Add($"Contract failed: {c.title} ({why}). -{c.penalty:N0} funds");
        }

        /// <summary>Checks active contracts against a vessel that's flying (the active one, every frame).</summary>
        public void CheckVessel(VesselSim v, double ut)
        {
            if (!IsCareer || v == null || v.destroyed) return;
            var sit = Science.SituationOf(v, ut);
            foreach (var c in contracts.Where(x => x.state == ContractState.Active).ToList())
            {
                bool here = c.body == null || v.body.name == c.body;
                switch (c.type)
                {
                    case "altitude": if (here && !v.landed && v.Altitude >= c.altitude) Complete(c); break;
                    case "space": if (here && v.body.HasAtmosphere && v.Altitude > v.body.atmosphereHeight) Complete(c); break;
                    case "orbit": if (here && v.Situation(ut).StartsWith("Orbiting")) Complete(c); break;
                    case "flyby": if (here) Complete(c); break;
                    case "land": if (here && v.landed && v.hasLaunched) Complete(c); break;
                    case "rover": if (here && v.drivenOn == c.body && v.driven >= c.amount) Complete(c); break;
                    case "skim": if (here && !v.landed && v.hasLaunched && v.Altitude < c.altitude) Complete(c); break;
                    case "mine": if (here && v.landed && v.OreStored >= c.amount - 1e-6) Complete(c); break;
                    case "test":
                        if (here && sit.ToString() == c.situation && v.parts.Exists(p => p.def.id == c.partId && p.thrustFrac > 0.05f)) Complete(c);
                        break;
                }
            }
        }

        /// <summary>Checks satellite contracts against every vessel in the universe (once a second or so).</summary>
        public void CheckSatellites(IEnumerable<VesselSim> vessels, double ut)
        {
            if (!IsCareer) return;
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && x.type == "satellite").ToList())
                foreach (var v in vessels)
                {
                    if (v.destroyed || v.isDebris || v.landed || v.body.name != c.body) continue;
                    bool probe = v.parts.Exists(p => p.def.kind == PartKind.Command && p.def.crew == 0);
                    bool antenna = v.parts.Exists(p => p.def.kind == PartKind.Antenna);
                    if (!probe || !antenna || v.CrewCount > 0) continue;
                    var o = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
                    if (!o.IsElliptic) continue;
                    double ap = o.Apoapsis - v.body.radius, pe = o.Periapsis - v.body.radius;
                    if (Math.Abs(ap - c.ap) <= 0.1 * c.ap && Math.Abs(pe - c.pe) <= 0.1 * c.pe) { Complete(c); break; }
                }
        }

        public void CheckDeadlines(double ut)
        {
            if (!IsCareer) return;
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && ut > x.deadline).ToList()) Fail(c, "out of time");
        }

        /// <summary>A grabber has just latched onto a wanderer.</summary>
        public void OnGrabbed(VesselSim v, string rock)
        {
            SetRecord(Records.FirstGrab);
            if (!IsCareer) return;
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && x.type == "grab").ToList())
                if (c.target == null || c.target == rock) Complete(c);
        }

        /// <summary>A wanderer counts as captured in a closed orbit of the home world: periapsis above the air, apoapsis under 40,000 km.</summary>
        public static bool Captured(VesselSim v, double ut)
        {
            if (v.destroyed || v.landed || !v.body.IsHomeWorld) return false;
            var o = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
            return o.IsElliptic && o.Periapsis - v.body.radius > v.body.SafeAltitude && o.Apoapsis - v.body.radius < 40_000_000;
        }

        /// <summary>Capture contracts and the capture record, against every vessel (once a second). Fails one whose wanderer is gone.</summary>
        public void CheckWanderers(IEnumerable<VesselSim> vessels, double ut)
        {
            var all = vessels.ToList();
            foreach (var v in all)
                if (v.parts.Exists(p => p.def.kind == PartKind.Rock) && Captured(v, ut)) SetRecord(Records.FirstCapture);
            if (!IsCareer) return;
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && x.type == "capture").ToList())
            {
                var holder = all.Find(v => !v.destroyed && v.HeldRockNames().Contains(c.target));
                if (holder == null) Fail(c, $"{c.target} is gone");
                else if (Captured(holder, ut)) Complete(c);
            }
        }

        public void OnDocked(VesselSim v)
        {
            OnFlags(VFlags.Docked);
            if (!IsCareer) return;
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && x.type == "dock").ToList())
                if (v.body.name == c.body && !v.landed) Complete(c);
        }

        void OnScienceCredited(string subject, float gain)
        {
            if (!IsCareer || gain <= 0) return;
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && x.type == "science").ToList())
                if (string.IsNullOrEmpty(c.region) ? subject.Contains($"@{c.body}:{c.situation}") : subject.EndsWith($"@{c.body}:{c.situation}:{c.region}")) Complete(c);
        }

        void OnRecovered(VesselSim v)
        {
            if (!IsCareer) return;
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && x.type == "return").ToList())
                if ((v.careerFlags & VFlags.LandedOn(c.body)) != 0) Complete(c);
        }

        // ---------------------------------------------------------------- save

        public CareerSave ToSave()
        {
            var save = new CareerSave
            {
            version = 2, mode = mode, chosen = chosen, funds = funds, science = science, reputation = reputation,
            unlocked = unlocked.ToList(), subjects = subjects.Values.ToList(), contracts = contracts.ToList(),
            records = records, nextContractId = nextContractId, seed = seed,
            facilities = facility.ToList(), strategies = strategies.ToList(), roster = roster.ToList(), guide = guide, suppliesOn = suppliesOn,
            badges = badges.ToList(),
            landmarksSpotted = spotted.ToList(), landmarksVisited = visited.ToList(),
            storyStep = storyStep, storyStarted = storyStarted, storyBriefed = storyBriefed,
            presetSlots = presetSlots, scavenged = scavenged.ToList(), creditsSeen = creditsSeen,
            };
            SaveSurvival(save);   // milestone 38
            return save;
        }

        public void Load(CareerSave s)
        {
            if (s == null || s.version == 0) { mode = GameMode.Sandbox; chosen = false; ResetSurvival(); return; } // saved before careers existed
            mode = s.mode; chosen = s.chosen; funds = s.funds; science = s.science; reputation = s.reputation;
            presetSlots = Math.Max(1, s.presetSlots);
            creditsSeen = s.creditsSeen;
            scavenged.Clear();
            if (s.scavenged != null) scavenged.AddRange(s.scavenged);
            unlocked.Clear(); foreach (var u in s.unlocked) unlocked.Add(u); unlocked.Add("start");
            subjects.Clear(); foreach (var x in s.subjects) subjects[x.id] = x;
            contracts.Clear(); contracts.AddRange(s.contracts);
            records = s.records; nextContractId = Math.Max(1, s.nextContractId); seed = s.seed;
            guide = s.guide;
            suppliesOn = s.suppliesOn;
            badges.Clear();
            if (s.badges != null) badges.AddRange(s.badges);
            spotted.Clear(); visited.Clear();
            if (s.landmarksSpotted != null) spotted.AddRange(s.landmarksSpotted);
            if (s.landmarksVisited != null) visited.AddRange(s.landmarksVisited);
            storyStep = s.storyStep; storyStarted = s.storyStarted; storyBriefed = s.storyBriefed;
            storyNews.Clear();
            badgeNews.Clear();
            LoadCenter(s);
            LoadSurvival(s);   // milestone 38
        }
    }
}
