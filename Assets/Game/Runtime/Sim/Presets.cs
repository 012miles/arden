using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// The builder's ready-made designs (Presets menu). Milestone 32 adds ten more, from a tiny solid-fuel hop to a
    /// four-booster heavy lifter, several using the new parts.
    /// </summary>
    public static partial class Presets
    {
        public static List<(string label, VesselDesign design)> All() => new List<(string, VesselDesign)>
        {
            ("First flight", VesselDesign.FirstFlight()),
            ("Sky Hopper (tiny booster)", SkyHopper()),
            ("Science Hopper", ScienceHopper()),
            ("Starter rocket", VesselDesign.Default()),
            ("Abort test (escape tower)", AbortTest()),
            ("Side boosters", VesselDesign.Broadside()),
            ("Twin Booster", TwinBooster()),
            ("Orbiter (heat shield)", Orbiter()),
            ("Moon rocket", VesselDesign.Longshot()),
            ("Tal Lander (land and return)", TalLander()),
            ("Satellite launcher", ComsatLauncher()),
            ("Big Lift (heavy probe)", BigLift()),
            ("Quad Booster", QuadBooster()),
            ("Plane", VesselDesign.Kite()),
            ("Docking ferry", VesselDesign.Ferry()),
            ("Station (Outpost)", VesselDesign.Outpost()),
            ("Station Core (big)", StationCore()),
            ("Tal miner (Prospector)", VesselDesign.Prospector()),
            ("Rover (Tumbler)", VesselDesign.Rover()),
            ("Ochre probe (Wayfarer)", VesselDesign.Wayfarer()),
            ("Wanderer tug (Wrangler)", VesselDesign.Wrangler()),
            ("Brume probe (Long Haul)", VesselDesign.LongHaul()),
            // milestone 43 (captaincarl24: "add more presets for multiplayer like add like 10 more"): ships for a whole team, a
            // capsule and a four-seat habitat (five seats, room for everyone in a room of four)
            ("Team: Hopper (5 seats)", TeamHopper()),
            ("Team: Splashdown (5 seats)", TeamSplashdown()),
            ("Team: Twin Booster (5 seats)", TeamTwinBooster()),
            ("Team: Orbiter (5 seats)", TeamOrbiter()),
            ("Team: Quad Booster (5 seats)", TeamQuadBooster()),
            ("Team: Station (5 seats)", TeamStation()),
            ("Team: Big Lift (5 seats)", TeamBigLift()),
            ("Team: Tal Lander (5 seats, there and back)", TeamTalLander()),
            ("Team: Ochre Expedition (5 seats)", TeamOchre()),
            ("Team: Brume Voyager (5 seats)", TeamBrume()),
        }.Concat(Planes()).ToList();   // milestone 46: ten planes (PresetsPlanes)

        /// <summary>The ten team ships new in milestone 43 (for tests).</summary>
        public static readonly string[] TeamOnes =
        {
            "Team: Hopper (5 seats)", "Team: Splashdown (5 seats)", "Team: Twin Booster (5 seats)", "Team: Orbiter (5 seats)", "Team: Quad Booster (5 seats)",
            "Team: Station (5 seats)", "Team: Big Lift (5 seats)", "Team: Tal Lander (5 seats, there and back)", "Team: Ochre Expedition (5 seats)", "Team: Brume Voyager (5 seats)",
        };

        /// <summary>The ten presets new in milestone 32 (for tests).</summary>
        public static readonly string[] NewOnes =
        {
            "Sky Hopper (tiny booster)", "Science Hopper", "Abort test (escape tower)", "Twin Booster", "Orbiter (heat shield)",
            "Tal Lander (land and return)", "Satellite launcher", "Big Lift (heavy probe)", "Quad Booster", "Station Core (big)",
        };

        static int Find(VesselDesign d, string id, bool lowest = false)
        {
            int best = -1;
            for (int i = 0; i < d.nodes.Count; i++)
                if (d.nodes[i].id == id && (best < 0 || (lowest ? d.nodes[i].pos.y < d.nodes[best].pos.y : d.nodes[i].pos.y > d.nodes[best].pos.y))) best = i;
            return best;
        }

        // ---------------------------------------------------------------- milestone 43: team ships

        static VesselDesign Done(VesselDesign d) { DesignOps.ApplyDefaultGroups(d); return d; }

        static void Boosters(VesselDesign d, int onTank, float frac, int count, string srb)
        {
            var decs = DesignOps.AttachSurfaceAt(d, onTank, frac, count == 4 ? 45f : 0f, "dec.r", count, false);
            int srbSym = DesignOps.NextSym(d), noseSym = srbSym + 1;
            foreach (int dec in decs)
            {
                int b = DesignOps.AttachToOuterFace(d, dec, srb);
                d.nodes[b].sym = srbSym;
                d.nodes[DesignOps.AttachStack(d, b, true, "nose.s")].sym = noseSym;
            }
        }

        static void Power(VesselDesign d)
        {
            int battery = Find(d, "battery.s");
            if (battery < 0) return;
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 0f, "solar.s", 2, false);
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 90f, "antenna.s", 1, false);
        }

        /// <summary>A short hop for the whole team: up a few kilometres and down on the parachute.</summary>
        public static VesselDesign TeamHopper() => Done(VesselDesign.Stack("Team Hopper", "chute.s", "pod.wren", "hab.s", "dec.s", "tank.s4", "tank.s4", "fins.s", "eng.kestrel"));

        /// <summary>A bigger hop with a heat shield, out over the sea and back down for a splashdown.</summary>
        public static VesselDesign TeamSplashdown() => Done(VesselDesign.Stack("Team Splashdown", "chute.s", "pod.wren", "hab.s", "shield.s", "dec.s", "tank.s4", "tank.s4", "tank.s4", "fins.s", "eng.kestrel"));

        /// <summary>The team on the starter core with two small solid boosters.</summary>
        public static VesselDesign TeamTwinBooster()
        {
            var d = VesselDesign.Stack("Team Twin Booster", "chute.s", "pod.wren", "hab.s", "dec.s", "tank.s2", "eng.ember", "dec.s", "tank.s4", "tank.s3", "fins.s", "eng.kestrel");
            Boosters(d, Find(d, "tank.s3"), 0.5f, 2, "srb.flint");
            return Done(d);
        }

        /// <summary>Two stages to orbit for five, a heat shield to come home on.</summary>
        public static VesselDesign TeamOrbiter() => Done(VesselDesign.Stack("Team Orbiter", "chute.s", "pod.wren", "hab.s", "shield.s", "dec.s", "tank.s4", "tank.s4", "eng.swift",
            "adapt.ls", "dec.l", "tank.l16", "fins.l", "eng.heron"));

        /// <summary>A 2.6 m core with four solid boosters: lots of push off the pad with a full crew.</summary>
        public static VesselDesign TeamQuadBooster()
        {
            var d = VesselDesign.Stack("Team Quad Booster", "chute.s", "pod.wren", "hab.s", "shield.s", "dec.s", "tank.s4", "eng.ember", "adapt.ls",
                "dec.l", "tank.l8", "tank.l8", "fins.l", "eng.heron");
            Boosters(d, Find(d, "tank.l8", lowest: true), 0.4f, 4, "srb.cinder");
            return Done(d);
        }

        /// <summary>A crewed station for five: a docking port, power, and its own engine.</summary>
        public static VesselDesign TeamStation()
        {
            var d = VesselDesign.Stack("Team Station", "port.std", "pod.wren", "hab.s", "battery.s", "tank.s4", "eng.ember", "adapt.ls", "dec.l", "tank.l16", "fins.l", "eng.heron");
            Power(d);
            return Done(d);
        }

        /// <summary>The team on the big parts: a Condor upper stage on an FT-16L and an FT-8L.</summary>
        public static VesselDesign TeamBigLift() => Done(VesselDesign.Stack("Team Big Lift", "chute.s", "pod.wren", "hab.s", "shield.s", "adapt.ls", "tank.l8", "eng.condor",
            "dec.l", "tank.l16", "tank.l8", "fins.l", "eng.heron"));

        /// <summary>Five to Tal and back: a lander with legs on a big transfer stage and a Heron.</summary>
        public static VesselDesign TeamTalLander()
        {
            var d = VesselDesign.Stack("Team Tal Lander", "chute.s", "pod.wren", "hab.s", "shield.s", "tank.s4", "eng.swift",
                "dec.s", "tank.s4", "tank.s4", "eng.swift", "adapt.ls", "dec.l", "tank.l16", "tank.l16", "fins.l", "eng.heron");
            DesignOps.AttachSurfaceAt(d, Find(d, "tank.s4"), 0.95f, 45f, "leg.s", 4, false);
            return Done(d);
        }

        /// <summary>A long trip for five to the red planet: power for the months on the way, legs to land.</summary>
        public static VesselDesign TeamOchre()
        {
            var d = VesselDesign.Stack("Team Ochre Expedition", "chute.s", "pod.wren", "hab.s", "shield.s", "battery.s", "tank.s4", "eng.swift",
                "dec.s", "tank.s4", "tank.s4", "eng.swift", "adapt.ls", "dec.l", "tank.l16", "tank.l16", "fins.l", "eng.heron");
            DesignOps.AttachSurfaceAt(d, Find(d, "tank.s4"), 0.95f, 45f, "leg.s", 4, false);
            Power(d);
            return Done(d);
        }

        /// <summary>Five out to the giant's moons: a Condor stage with power, on a Heron with heavy solid boosters.</summary>
        public static VesselDesign TeamBrume()
        {
            var d = VesselDesign.Stack("Team Brume Voyager", "chute.s", "pod.wren", "hab.s", "shield.s", "battery.s", "adapt.ls", "tank.l8", "eng.condor",
                "dec.l", "tank.l16", "tank.l16", "fins.l", "eng.heron");
            Power(d);
            Boosters(d, Find(d, "tank.l16", lowest: true), 0.45f, 4, "srb.blaze");
            return Done(d);
        }

        /// <summary>A capsule on one small solid booster: up a few kilometres and down on the parachute. Starting parts only.</summary>
        public static VesselDesign SkyHopper()
        {
            var d = VesselDesign.Stack("Sky Hopper", "chute.s", "pod.wren", "dec.s", "srb.flint");
            DesignOps.AttachSurfaceAt(d, Find(d, "srb.flint"), 0.85f, 45f, "fin.r", 4, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>The first-flight rocket carrying all three early experiments: lots of science from one hop.</summary>
        public static VesselDesign ScienceHopper()
        {
            var d = VesselDesign.Stack("Science Hopper", "chute.s", "pod.wren", "dec.s", "tank.s2", "tank.s2", "fins.s", "eng.kestrel");
            int pod = Find(d, "pod.wren");
            DesignOps.AttachSurfaceAt(d, pod, 0.6f, 0f, "sci.therm", 1, false);
            DesignOps.AttachSurfaceAt(d, pod, 0.6f, 120f, "sci.baro", 1, false);
            DesignOps.AttachSurfaceAt(d, pod, 0.6f, 240f, "sci.bio", 1, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>An escape tower demonstration: press Backspace (abort) after liftoff and the tower pulls the capsule clear.</summary>
        public static VesselDesign AbortTest()
        {
            var d = VesselDesign.Stack("Abort Test", "les.s", "chute.s", "pod.wren", "dec.s", "tank.s2", "tank.s2", "fins.s", "eng.kestrel");
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>The starter core with two small solid boosters on the side.</summary>
        public static VesselDesign TwinBooster()
        {
            var d = VesselDesign.Stack("Twin Booster", "chute.s", "pod.wren", "dec.s", "tank.s2", "eng.ember", "dec.s", "tank.s4", "tank.s3", "fins.s", "eng.kestrel");
            int tank = Find(d, "tank.s3");
            var decs = DesignOps.AttachSurfaceAt(d, tank, 0.5f, 0f, "dec.r", 2, false);
            int srbSym = DesignOps.NextSym(d), noseSym = srbSym + 1;
            foreach (int dec in decs)
            {
                int srb = DesignOps.AttachToOuterFace(d, dec, "srb.flint");
                d.nodes[srb].sym = srbSym;
                d.nodes[DesignOps.AttachStack(d, srb, true, "nose.s")].sym = noseSym;
            }
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>A two-stage orbiter with a heat shield, so the capsule can come back from orbit and further.</summary>
        public static VesselDesign Orbiter()
        {
            var d = VesselDesign.Stack("Orbiter", "chute.s", "pod.wren", "shield.s", "dec.s", "tank.s4", "eng.ember",
                "dec.s", "tank.s4", "tank.s4", "tank.s3", "fins.s", "eng.kestrel");
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>
        /// A crewed lander for Tal on a three-stage rocket: the Heron lifts it, the Swift pushes it to Tal, and the lander
        /// (with legs and a Wisp) lands and flies home on its own fuel. The heat shield brings the capsule back through the air.
        /// </summary>
        public static VesselDesign TalLander()
        {
            var d = VesselDesign.Stack("Tal Lander", "chute.s", "pod.wren", "shield.s", "tank.s3", "eng.wisp",
                "dec.s", "tank.s4", "tank.s4", "eng.swift", "adapt.ls",
                "dec.l", "tank.l16", "tank.l8", "fins.l", "eng.heron");
            DesignOps.AttachSurfaceAt(d, Find(d, "tank.s3"), 0.95f, 45f, "leg.s", 4, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>A small satellite (probe core, battery, solar panels, antenna) inside a payload fairing on a two-stage rocket.</summary>
        public static VesselDesign ComsatLauncher()
        {
            var d = VesselDesign.Stack("Comsat Launcher", "probe.spark", "battery.s", "fairing.s", "tank.s2", "eng.ember",
                "dec.s", "tank.s4", "tank.s4", "fins.s", "eng.kestrel");
            int battery = Find(d, "battery.s");
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 0f, "solar.s", 2, false);
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 90f, "antenna.s", 1, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>A heavy probe on the new big parts: a Condor upper stage on an FT-16L and a Heron.</summary>
        public static VesselDesign BigLift()
        {
            var d = VesselDesign.Stack("Big Lift", "probe.vireo", "battery.s", "adapt.ls", "tank.l8", "eng.condor",
                "dec.l", "tank.l16", "fins.l", "eng.heron");
            int battery = Find(d, "battery.s");
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 0f, "solar.s", 2, false);
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 90f, "antenna.s", 1, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>A 2.6 m core with four solid boosters around it: lots of push off the pad.</summary>
        public static VesselDesign QuadBooster()
        {
            var d = VesselDesign.Stack("Quad Booster", "chute.s", "pod.wren", "shield.s", "dec.s", "tank.s4", "eng.ember", "adapt.ls",
                "dec.l", "tank.l8", "tank.l8", "fins.l", "eng.heron");
            int tank = Find(d, "tank.l8", lowest: true);
            var decs = DesignOps.AttachSurfaceAt(d, tank, 0.4f, 45f, "dec.r", 4, false);
            int srbSym = DesignOps.NextSym(d), noseSym = srbSym + 1;
            foreach (int dec in decs)
            {
                int srb = DesignOps.AttachToOuterFace(d, dec, "srb.cinder");
                d.nodes[srb].sym = srbSym;
                d.nodes[DesignOps.AttachStack(d, srb, true, "nose.s")].sym = noseSym;
            }
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>A bigger station: a standard docking port, a habitat for four, a Vireo core, solar power and its own engine.</summary>
        public static VesselDesign StationCore()
        {
            var d = VesselDesign.Stack("Station Core", "port.std", "hab.s", "probe.vireo", "battery.s", "tank.s4", "eng.ember",
                "dec.s", "tank.s4", "tank.s4", "tank.s4", "fins.s", "eng.kestrel");
            int battery = Find(d, "battery.s");
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 0f, "solar.s", 2, false);
            DesignOps.AttachSurfaceAt(d, Find(d, "hab.s"), 0.2f, 90f, "antenna.s", 1, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }
    }
}
