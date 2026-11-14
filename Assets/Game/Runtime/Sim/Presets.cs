using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// The builder's ready-made designs (Presets menu). Milestone 32 adds ten more, from a tiny solid-fuel hop to a
    /// four-booster heavy lifter, several using the new parts.
    /// </summary>
    public static class Presets
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
