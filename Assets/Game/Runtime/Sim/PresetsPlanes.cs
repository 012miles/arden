using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 46 (captaincarl24: "create 10 more plane presets following like the space shuttle, commercial planes,
    /// supersonic planes, like the concorde and commercial like the boeing and airbus"). Original designs in the style of
    /// real aircraft, built from the new big-plane parts. Each is balanced the same way as the Kite: the wings' lift centred a
    /// little behind the centre of mass, the main wheels just behind it, the nose wheel well forward, the tail surfaces far back.
    /// Every runway plane is tested to take off inside the runway and climb; the shuttle goes up from the pad like a rocket.
    /// </summary>
    public static partial class Presets
    {
        /// <summary>The ten planes new in milestone 46 (for tests), and which of them launch upright from the pad.</summary>
        public static readonly string[] PlaneOnes =
        {
            "Plane: Twin airliner (737 / A320 style)", "Plane: Jumbo jet (747 style)", "Plane: Wide-body twin (A350 / 787 style)",
            "Plane: Regional jet (tail engines)", "Plane: Cargo lifter (four engines)", "Plane: Supersonic airliner (Concorde style)",
            "Plane: Quiet supersonic jet (X-59 style)", "Plane: Rocket plane (X-15 style)", "Plane: Business jet", "Plane: Space shuttle (pad launch, runway landing)",
        };

        public static IEnumerable<(string label, VesselDesign design)> Planes()
        {
            yield return (PlaneOnes[0], TwinAirliner());
            yield return (PlaneOnes[1], JumboJet());
            yield return (PlaneOnes[2], WideBody());
            yield return (PlaneOnes[3], RegionalJet());
            yield return (PlaneOnes[4], CargoLifter());
            yield return (PlaneOnes[5], SupersonicAirliner());
            yield return (PlaneOnes[6], QuietSupersonic());
            yield return (PlaneOnes[7], RocketPlane());
            yield return (PlaneOnes[8], BusinessJet());
            yield return (PlaneOnes[9], SpaceShuttle());
        }

        // ---------------------------------------------------------------- the planes

        // (each takes where its wings' lift and its main wheels sit behind the centre of mass, tuned by a takeoff sweep; the
        // engines' thrust lines run level with the centre of mass, so full power doesn't lift or drop the nose on the runway)
        public static VesselDesign TwinAirliner(float lift = 0.3f, float gear = 0.35f) => Airliner("Twin Airliner", new[] { "cockpit.liner", "cabin.liner", "cabin.liner", "tank.l4", "tank.l4", "tail.l" },
            d => Engines(d, 4, 0.6f, "eng.fan", 0f), lift, gear);

        public static VesselDesign JumboJet(float lift = 0.3f, float gear = 0.35f) => Airliner("Jumbo Jet", new[] { "cockpit.liner", "cabin.liner", "cabin.liner", "cabin.liner", "tank.l8", "tank.l4", "tail.l" },
            d => { Engines(d, 5, 0.5f, "eng.fan", 22f); Engines(d, 5, 0.5f, "eng.fan", -22f); }, lift, gear);

        public static VesselDesign WideBody(float lift = 0.3f, float gear = 0.35f) => Airliner("Wide-body Twin", new[] { "cockpit.liner", "cabin.liner", "cabin.liner", "cabin.liner", "tank.l8", "tail.l" },
            d => Engines(d, 4, 0.8f, "eng.fan", 0f), lift, gear);

        public static VesselDesign RegionalJet(float lift = 0.3f, float gear = 0.35f) => Airliner("Regional Jet", new[] { "cockpit.liner", "cabin.liner", "cabin.liner", "tank.l4", "tail.l" },
            d => Engines(d, 3, 0.75f, "eng.fan", 0f), lift, gear);

        public static VesselDesign CargoLifter(float lift = 0.3f, float gear = 0.35f) => Airliner("Cargo Lifter", new[] { "cockpit.liner", "tank.l8", "tank.l8", "tank.l4", "tail.l" },
            d => { Engines(d, 3, 0.5f, "eng.fan", 22f); Engines(d, 3, 0.5f, "eng.fan", -22f); }, lift, gear);

        public static VesselDesign SupersonicAirliner(float lift = 0.3f, float gear = 0.35f)
        {
            var d = VesselDesign.Stack("Supersonic Airliner", "cockpit.arrow", "cabin.liner", "cabin.liner", "tank.l4", "tank.l4", "tail.l");
            Engines(d, 4, 0.6f, "eng.mistral", 22f);
            Engines(d, 4, 0.6f, "eng.mistral", -22f);
            return Finish(d, "wing.ogee", lift, "elevon.l", "fin.tall", "gear.l", gear, -80f);
        }

        public static VesselDesign QuietSupersonic(float lift = -0.1f, float gear = 0.35f)
        {
            var d = VesselDesign.Stack("Quiet Supersonic", "cockpit.swift", "tank.s2", "tank.s2", "tank.s2", "eng.mistral");
            return Finish(d, "wing.delta", lift, "elevon", "tail.fin", "gear.s", gear, -70f);
        }

        public static VesselDesign RocketPlane(float lift = 0.1f, float gear = 0.35f)
        {
            var d = VesselDesign.Stack("Rocket Plane", "cockpit.swift", "tank.s2", "tank.s2", "eng.swift");
            return Finish(d, "wing.delta", lift, "elevon", "tail.fin", "gear.s", gear, -70f);
        }

        public static VesselDesign BusinessJet(float lift = 0.1f, float gear = 0.35f)
        {
            var d = VesselDesign.Stack("Business Jet", "cockpit.swift", "hab.s", "tank.s2", "tail.cone");
            Engines(d, 2, 0.6f, "eng.gale", 0f);
            return Finish(d, "wing.delta", lift, "elevon", "tail.fin", "gear.s", gear, -70f);
        }

        /// <summary>Launched from the pad like a rocket (two big boosters on the sides), glides home and lands on the runway.</summary>
        public static VesselDesign SpaceShuttle(float lift = 0.3f, float gear = 0.6f)
        {
            var d = VesselDesign.Stack("Space Shuttle", "cockpit.orbiter", "tank.l4", "tank.l4", "tank.l4", "eng.heron");
            Boosters(d, 2, 0.5f, 2, "srb.blaze");
            return Finish(d, "wing.ogee", lift, "elevon.l", "fin.tall", "gear.l", gear, -80f);
        }

        /// <summary>For the takeoff sweep: each runway plane's builder by its wing and wheel positions.</summary>
        public static readonly (string name, System.Func<float, float, VesselDesign> make)[] PlaneBuilders =
        {
            ("Twin Airliner", (a, g) => TwinAirliner(a, g)), ("Jumbo Jet", (a, g) => JumboJet(a, g)), ("Wide-body Twin", (a, g) => WideBody(a, g)),
            ("Regional Jet", (a, g) => RegionalJet(a, g)), ("Cargo Lifter", (a, g) => CargoLifter(a, g)), ("Supersonic Airliner", (a, g) => SupersonicAirliner(a, g)),
            ("Quiet Supersonic", (a, g) => QuietSupersonic(a, g)), ("Rocket Plane", (a, g) => RocketPlane(a, g)), ("Business Jet", (a, g) => BusinessJet(a, g)),
        };

        // ---------------------------------------------------------------- building them

        static VesselDesign Airliner(string name, string[] stack, System.Action<VesselDesign> engines, float liftBehind, float gearBehind)
        {
            var d = VesselDesign.Stack(name, stack);
            engines(d);
            return Finish(d, "wing.swept", liftBehind, "elevon.l", "fin.tall", "gear.l", gearBehind, -70f);
        }

        /// <summary>A mirrored pair of engines on the sides of stack part 'onPart', 'frac' of the way down it, at an angle round it.</summary>
        static void Engines(VesselDesign d, int onPart, float frac, string id, float angle) => DesignOps.AttachSurfaceAt(d, MainStack(d)[onPart], frac, angle, id, 1, true);

        /// <summary>Tail surfaces at the back, wings with their lift a little behind the centre of mass, then the wheels.</summary>
        static VesselDesign Finish(VesselDesign d, string wing, float liftBehind, string elevon, string fin, string gear, float gearBehind, float mainAngle)
        {
            var stack = MainStack(d);
            int tail = stack[stack.Count - 1];
            DesignOps.AttachSurfaceAt(d, tail, 0.45f, 90f, fin, 1, false);
            DesignOps.AttachSurfaceAt(d, tail, 0.55f, 0f, elevon, 1, true);
            var com = DesignOps.CenterOfMass(d);
            AtY(d, com.y - liftBehind - PartCatalog.Get(wing).CenterLocal.y, 0f, wing, true);
            com = DesignOps.CenterOfMass(d);
            AtY(d, com.y - gearBehind, mainAngle, gear, true);
            var nose = d.nodes[stack[0]];
            AtY(d, nose.pos.y - PartCatalog.Get(nose.id).length * 0.85f, -90f, gear, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>The fuselage: stack parts on the middle line, nose first.</summary>
        static List<int> MainStack(VesselDesign d)
        {
            var res = new List<int>();
            for (int i = 0; i < d.nodes.Count; i++)
            {
                var n = d.nodes[i];
                var def = PartCatalog.Get(n.id);
                if (def == null || def.radial || n.attach == Attach.Surface || Mathf.Abs(n.pos.x) > 0.01f || Mathf.Abs(n.pos.z) > 0.01f) continue;
                res.Add(i);
            }
            res.Sort((a, b) => d.nodes[b].pos.y.CompareTo(d.nodes[a].pos.y));
            return res;
        }

        /// <summary>Side-mounts a part (with its mirror) on whichever fuselage part spans height y.</summary>
        static void AtY(VesselDesign d, float y, float angle, string id, bool mirror)
        {
            var stack = MainStack(d);
            int best = stack[0];
            float frac = 0;
            foreach (int i in stack)
            {
                var n = d.nodes[i];
                float len = PartCatalog.Get(n.id).length, top = n.pos.y, bot = top - len;
                best = i;
                if (y >= bot) { frac = Mathf.Clamp01((top - y) / len); break; }
                frac = 1f;
            }
            DesignOps.AttachSurfaceAt(d, best, frac, angle, id, 1, mirror);
        }
    }
}
