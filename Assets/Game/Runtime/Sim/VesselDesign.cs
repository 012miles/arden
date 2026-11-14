using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public enum Attach { Root = 0, Above = 1, Below = 2, Surface = 3 }

    /// <summary>Action groups: bits 0-9 are custom groups 1-10; the rest are the standard toggles.</summary>
    public static class Groups
    {
        public const int Abort = 10, Gear = 11, Lights = 12, Brakes = 13, Deploy = 14;
        public static int Bit(int g) => 1 << g;
        public static string Name(int g) => g < 10 ? $"{(g + 1) % 10}" : g == Abort ? "Abort" : g == Gear ? "Gear" : g == Lights ? "Lights" : g == Brakes ? "Brakes" : "Deploy";
        public const int Count = 15;
    }

    /// <summary>One placed part in a design. Positions and rotations are in the vessel frame (nose = +Y).</summary>
    [Serializable]
    public class PartNode
    {
        public string id;
        public int parent = -1;
        public Attach attach;
        public Vector3 pos;
        public Quaternion rot = Quaternion.identity;
        public int sym;           // symmetry group id (0 = none)
        public int variant;       // paint scheme
        public int stage = -1;    // manual staging: activation stage (0-based); -1 = not staged
        public int groups;        // action-group bitmask

        public PartNode Copy() => (PartNode)MemberwiseClone();
    }

    /// <summary>A rocket as built in the builder: a tree of parts. nodes[0] is the root.</summary>
    [Serializable]
    public class VesselDesign
    {
        public string name = "Untitled Rocket";
        public List<PartNode> nodes = new List<PartNode>();
        public bool manualStaging;
        public List<string> parts = new List<string>(); // legacy (v1) vertical stack; converted by Upgrade()

        public VesselDesign Clone()
        {
            var d = JsonUtility.FromJson<VesselDesign>(JsonUtility.ToJson(this));
            d.Upgrade();
            return d;
        }

        public void Upgrade()
        {
            if ((nodes == null || nodes.Count == 0) && parts != null && parts.Count > 0)
                nodes = Stack(name, parts.ToArray()).nodes;
            if (nodes == null) nodes = new List<PartNode>();
            parts = new List<string>();
        }

        public List<PartDef> Defs() => nodes.Select(n => PartCatalog.Get(n.id)).ToList();
        public int Count => nodes.Count;

        /// <summary>Builds a straight stack from the top down (used by presets and tests).</summary>
        public static VesselDesign Stack(string name, params string[] ids)
        {
            var d = new VesselDesign { name = name };
            float y = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                var def = PartCatalog.Get(ids[i]);
                d.nodes.Add(new PartNode { id = ids[i], parent = i - 1, attach = i == 0 ? Attach.Root : Attach.Below, pos = new Vector3(0, y, 0) });
                y -= def != null ? def.length : 1f;
            }
            // The command part is the root, so everything else (escape tower, decouplers) can separate from it.
            int cmd = d.nodes.FindIndex(n => PartCatalog.Get(n.id)?.kind == PartKind.Command);
            if (cmd > 0) DesignOps.Reroot(d, cmd);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        public Topology Topo() => Topology.Of(Defs(), nodes.Select(n => n.parent).ToArray(), nodes.Select(n => n.attach).ToArray(), null);

        public List<StageAction> Stages() => Staging.Build(this);

        /// <summary>Returns null when the design can fly, otherwise a reason it can't.</summary>
        public string Validate()
        {
            if (nodes.Count == 0) return "Pick a command part (capsule or probe core) to start.";
            var defs = Defs();
            for (int i = 0; i < defs.Count; i++)
                if (defs[i] == null) return $"Unknown part '{nodes[i].id}'.";
            if (nodes[0].parent != -1) return "Broken design: the first part must be the root.";
            if (!defs.Exists(d => d.kind == PartKind.Command)) return "Needs a command part (capsule or probe core).";
            var topo = Topo();
            bool cmdInRoot = false;
            for (int i = 0; i < defs.Count; i++)
                if (defs[i].kind == PartKind.Command && topo.section[i] == topo.section[0]) cmdInRoot = true;
            if (!cmdInRoot) return "A command part must stay with the root part (no decoupler between them).";
            return null;
        }

        /// <summary>Pre-flight checks (the engineer's report).</summary>
        public List<string> Warnings(double gravity)
        {
            var w = new List<string>();
            var defs = Defs();
            if (defs.Count == 0 || defs.Contains(null)) return w;
            var topo = Topo();
            var reports = Staging.AnalyzeDesign(this, gravity);
            bool crewed = defs.Exists(d => d.crew > 0);
            bool plane = IsPlane; // lands on wheels and flies on wings: capsule and liftoff-TWR checks don't apply
            bool jetOnly = defs.Exists(d => d.IsJet) && !defs.Exists(d => d.IsEngine && !d.IsJet);
            if (crewed && !plane && !defs.Exists(d => d.kind == PartKind.Parachute)) w.Add("No parachute: the crew can't land safely.");
            double total = reports.Sum(r => r.dvVac);
            if (crewed && !plane && !jetOnly && total > 5000 && !defs.Exists(d => d.kind == PartKind.HeatShield))
                w.Add("No heat shield: coming home from Tal will burn up the capsule.");
            var first = reports.Find(r => r.hasEngines);
            if (!plane && first != null && first.twrAsl < 1.0) w.Add($"Liftoff TWR {first.twrAsl:0.00} is below 1: it won't leave the pad.");
            else if (!plane && first != null && first.twrAsl < 1.25) w.Add($"Liftoff TWR {first.twrAsl:0.00} is low: expect a slow, lossy climb.");
            if (plane)
            {
                Vector3 cm = DesignOps.CenterOfMass(this);
                float lift = 0, area = 0;
                bool mainBehind = false;
                foreach (var n in nodes)
                {
                    var d = PartCatalog.Get(n.id);
                    if (d == null) continue;
                    if (d.LiftArea > 0 && Mathf.Abs((n.rot * Vector3.forward).z) > 0.5f) { lift += DesignOps.Center(n, d).y * d.LiftArea; area += d.LiftArea; }
                    if (d.kind == PartKind.Gear && n.pos.y < cm.y - 0.05f) mainBehind = true;
                }
                if (area <= 0) w.Add("Landing gear but no wings: it will never leave the runway.");
                else if (lift / area > cm.y) w.Add($"Centre of lift is {lift / area - cm.y:0.00} m ahead of the centre of mass: the plane will flip. Move the wings back.");
                if (!mainBehind) w.Add("No wheels behind the centre of mass: the plane will sit on its tail.");
            }
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].kind != PartKind.Engine) continue;
                bool tank = false;
                for (int j = 0; j < defs.Count; j++)
                    if (topo.section[j] == topo.section[i] && defs[j].kind == PartKind.Tank) tank = true;
                if (!tank) w.Add($"{defs[i].name} has no fuel tank in its stage (decouplers block fuel).");
            }
            Vector3 com = DesignOps.CenterOfMass(this);
            if (new Vector2(com.x, com.z).magnitude > 0.15f) w.Add($"Centre of mass is {new Vector2(com.x, com.z).magnitude:0.00} m off the centre line: use symmetry.");
            float off = DesignOps.ThrustOffset(this);
            if (off > 0.12f) w.Add($"Liftoff thrust is {off:0.00} m off-centre: the rocket will try to spin.");
            if (defs.Exists(d => d.kind == PartKind.Rcs) && !defs.Exists(d => d.mono > 0)) w.Add("RCS thrusters but no monopropellant.");
            if (defs.Exists(d => d.kind == PartKind.EscapeTower) && !defs.Exists(d => d.IsDecoupler)) w.Add("Escape tower with no decoupler to separate the capsule.");
            if (defs.Count > 60) w.Add($"{defs.Count} parts: check staging carefully.");
            // Uncrewed craft live on electricity and a signal home.
            bool probe = defs.Exists(d => d.kind == PartKind.Command && d.crew == 0), people = defs.Exists(d => d.kind == PartKind.Command && d.crew > 0);
            if (probe && !people)
            {
                float cap = defs.Sum(d => d.ecCapacity), idle = defs.Sum(d => d.ecIdle);
                if (idle > 0 && !defs.Exists(d => d.solarRate > 0))
                    w.Add($"No solar panels: {cap:0} EC keeps the probe alive about {cap / idle / 60:0} minutes (steering uses more).");
                if (!defs.Exists(d => d.commPower > 2e4f))
                    w.Add($"Only the probe core's own antenna: the signal reaches about {Math.Sqrt(2e4 * CommNet.GroundPower) / 1000:0} km from the ground station.");
            }
            // Mining kit: somewhere to put the ore, enough power, and a way to shed the heat.
            var machines = defs.Where(d => d.IsMachine).ToList();
            if (machines.Count > 0)
            {
                if (!defs.Exists(d => d.oreCapacity > 0)) w.Add("Drills and converters need an ore tank or ore pod to work from.");
                float heat = machines.Sum(d => d.heatOut), shed = machines.Sum(d => d.passiveCool) + defs.Sum(d => d.coolRate);
                double tEq = Ore.AmbientTemp + heat / Math.Max(1e-3, shed) * Ore.NominalDelta;
                if (tEq > Ore.HotStart) w.Add($"Drills and converters make {heat:0} kW of heat but shed only {shed:0} kW: they'll slow down running hot. Add radiators.");
                float ecUse = machines.Sum(d => d.machineEc), sun = defs.Sum(d => d.solarRate);
                if (sun < ecUse) w.Add($"Mining needs {ecUse:0.#} EC/s but the solar panels give at most {sun:0.#} EC/s in full sun.");
            }
            return w;
        }

        /// <summary>
        /// Tal mining lander on a two-stage lifter: survey scanner, drills, an ore tank and a converter, radiators and
        /// solar arrays. Map Tal from orbit, land somewhere rich, and refuel from the ground.
        /// </summary>
        public static VesselDesign Prospector() => LanderKit(Stack("Prospector", "probe.spark", "battery.s", "isru.s", "ore.s", "tank.s4", "eng.ember",
            "dec.s", "tank.s4", "tank.s4", "tank.s4", "eng.kestrel", "adapt.ls", "dec.l", "tank.l8", "tank.l8", "tank.l8", "tank.l8", "fins.l", "eng.heron"));

        /// <summary>The Prospector's lander on its own (tests and the mining demo).</summary>
        /// <summary>
        /// A four-wheeled rover standing on its frame: probe core on top, big wheels on the frame's sides, an antenna, two
        /// solar panels and a headlight on the front. Drives toward its +Z side.
        /// </summary>
        public static VesselDesign Rover()
        {
            var d = Stack("Tumbler", "probe.spark", "frame.r");
            int frame = d.nodes.FindIndex(n => n.id == "frame.r"), probe = d.nodes.FindIndex(n => n.id == "probe.spark");
            var f = d.nodes[frame];
            float half = PartCatalog.Get("frame.r").length * 0.5f;
            foreach (var (x, z) in new[] { (1f, 0.65f), (1f, -0.65f), (-1f, 0.65f), (-1f, -0.65f) })
                DesignOps.AttachSurface(d, frame, f.pos + f.rot * new Vector3(x * 0.65f, -half, z), f.rot * new Vector3(x, 0, 0), "wheel.l", 1, false, Quaternion.identity);
            DesignOps.AttachSurfaceAt(d, probe, 0.5f, 90f, "antenna.s", 1, false);
            DesignOps.AttachSurfaceAt(d, probe, 0.5f, 0f, "solar.s", 2, false);
            DesignOps.AttachSurface(d, frame, f.pos + f.rot * new Vector3(0, -half, 1.0f), f.rot * Vector3.forward, "light.s", 1, false, Quaternion.identity);
            return d;
        }

        /// <summary>
        /// An Ochre trip: a probe lander (deep space dish, solar panels, a parachute for the thin air, legs and its own
        /// engine) on a transfer stage, on top of the moon rocket's heavy lower stages. Escape at the window, fine-tune or
        /// aerocapture on the way, then land.
        /// </summary>
        public static VesselDesign Wayfarer()
        {
            var d = Stack("Wayfarer",
                "chute.s", "probe.spark", "battery.s", "tank.s2", "eng.wisp",
                "dec.s", "tank.s4", "tank.s4", "eng.ember",
                "dec.s", "tank.s4", "tank.s4", "eng.kestrel", "adapt.ls",
                "dec.l", "tank.l8", "tank.l8", "fins.l", "eng.heron");
            int lander = d.nodes.FindIndex(n => n.id == "tank.s2"), probe = d.nodes.FindIndex(n => n.id == "battery.s");
            DesignOps.AttachSurfaceAt(d, lander, 0.95f, 45f, "leg.s", 4, false);
            DesignOps.AttachSurfaceAt(d, probe, 0.5f, 0f, "dish.d", 1, false);
            DesignOps.AttachSurfaceAt(d, probe, 0.5f, 90f, "solar.s", 2, false);
            return d;
        }

        /// <summary>
        /// Wanderer tug: a grabber on a probe core, RCS, solar power and an antenna, with 12 t of fuel in 2.6 m tanks and a
        /// Kestrel (enough thrust to shove a 170 t rock), on a four-tank Heron core. Catches a wanderer near its closest pass
        /// and pushes it into orbit.
        /// </summary>
        public static VesselDesign Wrangler()
        {
            var d = Stack("Wrangler",
                "grab.s", "probe.vireo", "battery.s", "mono.s", "adapt.ls", "tank.l8", "tank.l4", "eng.kestrel", "adapt.ls",
                "dec.l", "tank.l8", "tank.l8", "tank.l8", "tank.l8", "fins.l", "eng.heron");
            int mono = d.nodes.FindIndex(n => n.id == "mono.s"), battery = d.nodes.FindIndex(n => n.id == "battery.s");
            DesignOps.AttachSurfaceAt(d, mono, 0.5f, 45f, "rcs.b", 4, false);
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 0f, "solar.s", 2, false);
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 90f, "antenna.s", 1, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>
        /// Brume probe: a Vireo core, battery, solar panels and the Great Dish on two FT-4s and a Lumen Drive (about 5,700 m/s
        /// on its own), on a Kestrel stage and a three-tank Heron core. Enough to reach Brume, capture, and visit a moon's orbit.
        /// </summary>
        public static VesselDesign LongHaul()
        {
            var d = Stack("Long Haul",
                "probe.vireo", "battery.s", "tank.s4", "tank.s4", "eng.lumen",
                "dec.s", "tank.s4", "tank.s4", "eng.kestrel", "adapt.ls",
                "dec.l", "tank.l8", "tank.l8", "tank.l8", "fins.l", "eng.heron");
            int battery = d.nodes.FindIndex(n => n.id == "battery.s");
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 0f, "dish.x", 1, false);
            DesignOps.AttachSurfaceAt(d, battery, 0.5f, 90f, "solar.s", 2, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>Just the tug, for tests and orbital setups.</summary>
        public static VesselDesign WranglerTug(string name = "Wrangler")
        {
            var d = Stack(name, "grab.s", "probe.vireo", "battery.s", "mono.s", "adapt.ls", "tank.l8", "tank.l4", "eng.kestrel");
            int mono = d.nodes.FindIndex(n => n.id == "mono.s");
            DesignOps.AttachSurfaceAt(d, mono, 0.5f, 45f, "rcs.b", 4, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        public static VesselDesign Lander(string name = "Prospector") => LanderKit(Stack(name, "probe.spark", "battery.s", "isru.s", "ore.s", "tank.s4", "eng.ember"));

        /// <summary>Legs, drills, radiators, solar arrays and a scanner on the topmost tank, ore tank and converter.</summary>
        static VesselDesign LanderKit(VesselDesign d)
        {
            int Top(string id) { int best = -1; for (int i = 0; i < d.nodes.Count; i++) if (d.nodes[i].id == id && (best < 0 || d.nodes[i].pos.y > d.nodes[best].pos.y)) best = i; return best; }
            int tank = Top("tank.s4"), conv = Top("isru.s"), ore = Top("ore.s");
            DesignOps.AttachSurfaceAt(d, tank, 0.95f, 45f, "leg.s", 4, false);
            DesignOps.AttachSurfaceAt(d, tank, 0.8f, 0f, "drill.s", 2, false);
            DesignOps.AttachSurfaceAt(d, ore, 0.5f, 90f, "rad.r", 2, false);
            DesignOps.AttachSurfaceAt(d, ore, 0.5f, 0f, "scan.orbit", 1, false);
            DesignOps.AttachSurfaceAt(d, ore, 0.5f, 180f, "antenna.s", 1, false); // a probe needs a signal home to take off again
            DesignOps.AttachSurfaceAt(d, conv, 0.5f, 45f, "solar.l", 4, false); // four arms at right angles: power whatever the sun's direction
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        public static VesselDesign Default() => Stack("Pathfinder I",
            "chute.s", "pod.wren",
            "dec.s", "tank.s2", "eng.ember",
            "dec.s", "tank.s4", "tank.s4", "tank.s2", "fins.s", "eng.kestrel");

        public static VesselDesign Longshot() => Stack("Longshot",
            "chute.s", "pod.wren", "shield.s",
            "dec.s", "tank.s4", "eng.ember",
            "dec.s", "tank.s4", "tank.s4", "eng.kestrel", "adapt.ls",
            "dec.l", "tank.l8", "tank.l8", "fins.l", "eng.heron");

        /// <summary>Light jet plane: cockpit, two tanks, a turbojet, delta wings, elevons, a tail fin and tricycle gear.</summary>
        public static VesselDesign Kite()
        {
            var d = Stack("Kite", "cockpit.swift", "tank.s2", "tank.s2", "eng.gale");
            int cockpit = d.nodes.FindIndex(n => n.id == "cockpit.swift");
            int jet = d.nodes.FindIndex(n => n.id == "eng.gale");
            int rear = -1, front = -1; // the two tanks
            for (int i = 0; i < d.nodes.Count; i++)
                if (d.nodes[i].id == "tank.s2" && (rear < 0 || d.nodes[i].pos.y < d.nodes[rear].pos.y)) rear = i;
            for (int i = 0; i < d.nodes.Count; i++) if (d.nodes[i].id == "tank.s2" && i != rear) front = i;
            // Balance (tuned by a takeoff sweep): centre of lift ~0.25 m behind the centre of mass, main gear ~0.35 m
            // behind it, so the elevons can lift the nose at rotation speed and the plane rests slightly nose-up.
            DesignOps.AttachSurfaceAt(d, front, 0.75f, 0f, "wing.delta", 1, true);
            DesignOps.AttachSurfaceAt(d, jet, 0.5f, 0f, "elevon", 1, true);
            DesignOps.AttachSurfaceAt(d, jet, 0.45f, 90f, "tail.fin", 1, false);
            DesignOps.AttachSurfaceAt(d, cockpit, 0.9f, -90f, "gear.s", 1, false);
            DesignOps.AttachSurfaceAt(d, rear, 0.22f, -70f, "gear.s", 1, true);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        public bool IsPlane => nodes.Exists(n => PartCatalog.Get(n.id)?.kind == PartKind.Gear);
        public bool IsRover => !IsPlane && nodes.Exists(n => PartCatalog.Get(n.id)?.kind == PartKind.Wheel);

        /// <summary>Career starter (tier-0 parts only): one stage, a thermometer, and a capsule that comes down on its parachute.</summary>
        public static VesselDesign FirstFlight()
        {
            var d = Stack("First Flight", "chute.s", "pod.wren", "dec.s", "tank.s2", "tank.s2", "fins.s", "eng.kestrel");
            int pod = d.nodes.FindIndex(n => n.id == "pod.wren");
            DesignOps.AttachSurfaceAt(d, pod, 0.6f, 0f, "sci.therm", 1, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>Crew ferry: a capsule with a docking port on its nose and RCS for docking, on a two-stage lifter.</summary>
        public static VesselDesign Ferry()
        {
            var d = Stack("Ferry", "port.sm", "chute.s", "pod.wren", "mono.s", "tank.s2", "eng.ember",
                "dec.s", "tank.s4", "tank.s4", "tank.s2", "fins.s", "eng.kestrel");
            int mono = d.nodes.FindIndex(n => n.id == "mono.s");
            DesignOps.AttachSurfaceAt(d, mono, 0.5f, 45f, "rcs.b", 4, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>The spacecraft part of the ferry only (for tests and orbital setups).</summary>
        public static VesselDesign FerryCraft(string name = "Ferry")
        {
            var d = Stack(name, "port.sm", "pod.wren", "mono.s", "tank.s1", "eng.ember");
            int mono = d.nodes.FindIndex(n => n.id == "mono.s");
            DesignOps.AttachSurfaceAt(d, mono, 0.5f, 45f, "rcs.b", 4, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>A small station: docking port, habitat for four, probe core, fuel and solar panels.</summary>
        public static VesselDesign Outpost(string name = "Outpost")
        {
            var d = Stack(name, "port.sm", "hab.s", "probe.spark", "tank.s4", "eng.ember");
            int tank = d.nodes.FindIndex(n => n.id == "tank.s4");
            DesignOps.AttachSurfaceAt(d, tank, 0.35f, 0f, "solar.s", 2, false);
            int hab = d.nodes.FindIndex(n => n.id == "hab.s");
            DesignOps.AttachSurfaceAt(d, hab, 0.2f, 90f, "antenna.s", 1, false);
            DesignOps.ApplyDefaultGroups(d);
            return d;
        }

        /// <summary>Starter core with an escape tower and two side boosters on radial decouplers.</summary>
        public static VesselDesign Broadside()
        {
            var d = Stack("Broadside",
                "les.s", "chute.s", "pod.wren",
                "dec.s", "tank.s2", "eng.ember",
                "dec.s", "tank.s4", "tank.s4", "tank.s2", "fins.s", "eng.kestrel");
            int tank = -1; // the lower FT-4 on the core
            for (int i = 0; i < d.nodes.Count; i++)
                if (d.nodes[i].id == "tank.s4" && (tank < 0 || d.nodes[i].pos.y < d.nodes[tank].pos.y)) tank = i;
            var decs = DesignOps.AttachSurfaceAt(d, tank, 0.55f, 0f, "dec.r", 2, false);
            int srbSym = DesignOps.NextSym(d), noseSym = srbSym + 1;
            foreach (int dec in decs)
            {
                int srb = DesignOps.AttachToOuterFace(d, dec, "srb.cinder");
                d.nodes[srb].sym = srbSym;
                d.nodes[DesignOps.AttachStack(d, srb, true, "nose.s")].sym = noseSym;
            }
            return d;
        }
    }

    /// <summary>Connectivity derived from a design or a flying vessel: children, fuel sections and staging depth.</summary>
    public class Topology
    {
        public int n;
        public int[] parent;
        public List<int>[] children;
        public int[] section;        // fuel section per part (decouplers start a new one)
        public List<int> sectionDepth = new List<int>();  // serial (stacked) staging depth per section
        public List<bool> sectionBooster = new List<bool>();
        public bool[] topCovered, bottomCovered;

        public static Topology Of(IList<PartDef> defs, int[] parent, Attach[] attach, bool[] present)
        {
            int n = defs.Count;
            var t = new Topology { n = n, parent = parent, children = new List<int>[n], section = new int[n], topCovered = new bool[n], bottomCovered = new bool[n] };
            for (int i = 0; i < n; i++) { t.children[i] = new List<int>(); t.section[i] = -1; }
            bool P(int i) => i >= 0 && i < n && defs[i] != null && (present == null || present[i]);
            for (int i = 0; i < n; i++)
            {
                if (!P(i) || !P(parent[i])) continue;
                t.children[parent[i]].Add(i);
                if (attach[i] == Attach.Below) { t.topCovered[i] = true; t.bottomCovered[parent[i]] = true; }
                if (attach[i] == Attach.Above) { t.bottomCovered[i] = true; t.topCovered[parent[i]] = true; }
            }
            var stack = new Stack<int>();
            for (int r = 0; r < n; r++)
            {
                if (!P(r) || P(parent[r])) continue; // roots only
                t.sectionDepth.Add(0); t.sectionBooster.Add(false);
                t.section[r] = t.sectionDepth.Count - 1;
                stack.Push(r);
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    foreach (int c in t.children[p])
                    {
                        if (defs[c].IsDecoupler)
                        {
                            bool booster = defs[c].kind == PartKind.RadialDecoupler;
                            t.sectionDepth.Add(t.sectionDepth[t.section[p]] + (booster ? 0 : 1));
                            t.sectionBooster.Add(booster);
                            t.section[c] = t.sectionDepth.Count - 1;
                        }
                        else t.section[c] = t.section[p];
                        stack.Push(c);
                    }
                }
            }
            return t;
        }

        /// <summary>Part i and everything attached below it in the tree.</summary>
        public List<int> Subtree(int i)
        {
            var res = new List<int>();
            var st = new Stack<int>();
            st.Push(i);
            while (st.Count > 0)
            {
                int p = st.Pop();
                res.Add(p);
                foreach (int c in children[p]) st.Push(c);
            }
            return res;
        }
    }

    /// <summary>What one press of the stage key does (part indices are design indices).</summary>
    [Serializable]
    public class StageAction
    {
        public List<int> decouple = new List<int>();
        public List<int> ignite = new List<int>();
        public List<int> chutes = new List<int>();
        public List<int> jettison = new List<int>();   // escape towers
        public bool Empty => decouple.Count + ignite.Count + chutes.Count + jettison.Count == 0;

        public IEnumerable<int> AllParts() => decouple.Concat(ignite).Concat(chutes).Concat(jettison);

        public string Describe(IList<PartDef> defs)
        {
            var bits = new List<string>();
            if (decouple.Count > 0) bits.Add(decouple.Count > 1 ? $"Decouple x{decouple.Count}" : "Decouple");
            foreach (var g in ignite.Where(i => i < defs.Count && defs[i] != null).GroupBy(i => defs[i].name))
                bits.Add(g.Count() > 1 ? $"Ignite {g.Key} x{g.Count()}" : $"Ignite {g.Key}");
            foreach (var g in jettison.Where(i => i < defs.Count && defs[i] != null).GroupBy(i => defs[i].kind == PartKind.Fairing ? "fairing" : "tower")) bits.Add(g.Count() > 1 ? $"Jettison {g.Key} x{g.Count()}" : $"Jettison {g.Key}");
            if (chutes.Count > 0) bits.Add(chutes.Count > 1 ? $"Parachutes x{chutes.Count}" : "Deploy parachute");
            return bits.Count > 0 ? string.Join(" + ", bits) : "(nothing)";
        }
    }

    public class StageReport
    {
        public int number;
        public string label;
        public double dvVac, dvAsl, twrVac, twrAsl, burnTime, startMass;
        public bool hasEngines;
    }

    public static class Staging
    {
        static bool Activatable(PartDef d) => d.IsEngine || d.IsDecoupler || d.kind == PartKind.Parachute || d.kind == PartKind.EscapeTower || d.kind == PartKind.Fairing;

        /// <summary>
        /// Auto staging: the deepest stacked stage lights first (side boosters light with the stage they hang on),
        /// boosters drop in their own stage, each stacked decoupler fires together with the engines above it,
        /// the escape tower goes with the first stage separation, parachutes last.
        /// </summary>
        public static List<StageAction> Build(VesselDesign design)
        {
            var defs = design.Defs();
            if (defs.Count == 0 || defs.Contains(null)) return new List<StageAction>();
            if (design.manualStaging) return BuildManual(design, defs);
            var topo = design.Topo();
            var stages = new List<StageAction>();
            int maxDepth = 0;
            for (int i = 0; i < defs.Count; i++) maxDepth = Math.Max(maxDepth, topo.sectionDepth[topo.section[i]]);
            var towers = Enumerable.Range(0, defs.Count).Where(i => defs[i].kind == PartKind.EscapeTower || defs[i].kind == PartKind.Fairing).ToList();
            bool towerDone = towers.Count == 0;
            for (int d = maxDepth; d >= 0; d--)
            {
                var st = new StageAction();
                for (int i = 0; i < defs.Count; i++)
                {
                    int sd = topo.sectionDepth[topo.section[i]];
                    if (defs[i].kind == PartKind.Decoupler && sd == d + 1) st.decouple.Add(i);
                    if (defs[i].IsEngine && sd == d) st.ignite.Add(i);
                }
                if (!st.Empty) stages.Add(st);
                var boosters = new StageAction();
                for (int i = 0; i < defs.Count; i++)
                    if (defs[i].kind == PartKind.RadialDecoupler && topo.sectionDepth[topo.section[i]] == d) boosters.decouple.Add(i);
                if (!boosters.Empty) stages.Add(boosters);
                // The escape tower gets its own stage once the first stage (and its boosters) is done.
                if (!towerDone && stages.Count > 0) { stages.Add(new StageAction { jettison = towers }); towerDone = true; }
            }
            if (!towerDone) stages.Add(new StageAction { jettison = towers });
            var chutes = Enumerable.Range(0, defs.Count).Where(i => defs[i].kind == PartKind.Parachute).ToList();
            if (chutes.Count > 0) stages.Add(new StageAction { chutes = chutes });
            return stages;
        }

        static List<StageAction> BuildManual(VesselDesign design, List<PartDef> defs)
        {
            int max = -1;
            foreach (var n in design.nodes) max = Math.Max(max, n.stage);
            var stages = new List<StageAction>();
            for (int s = 0; s <= max; s++)
            {
                var st = new StageAction();
                for (int i = 0; i < defs.Count; i++)
                {
                    if (design.nodes[i].stage != s || !Activatable(defs[i])) continue;
                    if (defs[i].IsDecoupler) st.decouple.Add(i);
                    else if (defs[i].IsEngine) st.ignite.Add(i);
                    else if (defs[i].kind == PartKind.Parachute) st.chutes.Add(i);
                    else if (defs[i].kind == PartKind.EscapeTower || defs[i].kind == PartKind.Fairing) st.jettison.Add(i);
                }
                stages.Add(st);
            }
            return stages;
        }

        /// <summary>Switches a design to manual staging, keeping the current auto stages.</summary>
        public static void Freeze(VesselDesign design)
        {
            if (design.manualStaging) return;
            var stages = Build(design);
            foreach (var n in design.nodes) n.stage = -1;
            for (int s = 0; s < stages.Count; s++)
                foreach (int i in stages[s].AllParts()) design.nodes[i].stage = s;
            design.manualStaging = true;
        }

        public static List<StageReport> AnalyzeDesign(VesselDesign design, double gravity)
        {
            var defs = design.Defs();
            if (defs.Count == 0 || defs.Contains(null)) return new List<StageReport>();
            int n = defs.Count;
            var prop = new double[n]; var mono = new double[n]; var present = new bool[n];
            for (int i = 0; i < n; i++) { prop[i] = defs[i].propellant; mono[i] = defs[i].mono; present[i] = true; }
            return Analyze(defs, design.nodes.Select(x => x.parent).ToArray(), design.nodes.Select(x => x.attach).ToArray(),
                prop, mono, present, new bool[n], Build(design), 0, gravity);
        }

        /// <summary>
        /// Delta-v per stage. Burns are split at every moment a fuel source runs dry and each phase uses the exact
        /// rocket equation, so mixed solid + liquid stages and side boosters come out right.
        /// </summary>
        public static List<StageReport> Analyze(IList<PartDef> defs, int[] parent, Attach[] attach, double[] propellant, double[] mono,
            bool[] present, bool[] ignited, List<StageAction> stages, int firstStage, double gravity)
        {
            int n = defs.Count;
            var reports = new List<StageReport>();
            var pres = (bool[])present.Clone();
            var prop = (double[])propellant.Clone();
            var ign = (bool[])ignited.Clone();
            var topo = Topology.Of(defs, parent, attach, pres);
            var section = topo.section;

            var shellGone = new bool[n];
            double Mass()
            {
                double m = 0;
                for (int i = 0; i < n; i++)
                    if (pres[i] && defs[i] != null) m += defs[i].dryMass + prop[i] + mono[i] + defs[i].supplyCapacity * Supplies.MassPerDay + (shellGone[i] ? 0 : defs[i].fairingMass);
                return m;
            }
            double Liquid(int sec)
            {
                double s = 0;
                for (int i = 0; i < n; i++) if (pres[i] && defs[i] != null && defs[i].kind == PartKind.Tank && section[i] == sec) s += prop[i];
                return s;
            }

            for (int s = firstStage; s < stages.Count; s++)
            {
                var st = stages[s];
                foreach (int d in st.decouple.Concat(st.jettison))
                {
                    if (d >= n || !pres[d]) continue;
                    if (defs[d].kind == PartKind.Fairing) { shellGone[d] = true; continue; } // only the shell goes
                    foreach (int k in topo.Subtree(d)) pres[k] = false;
                }
                foreach (int i in st.ignite) if (i < n && pres[i]) ign[i] = true;

                // A stage burns until the fuel in whatever the next stage drops is gone (e.g. side boosters run dry);
                // the remaining engines keep burning into the next stage.
                var dropNext = new HashSet<int>();
                if (s + 1 < stages.Count)
                    foreach (int d in stages[s + 1].decouple)
                        if (d < n && pres[d]) foreach (int k in topo.Subtree(d)) if (pres[k]) dropNext.Add(k);
                double DropFuel()
                {
                    double f = 0;
                    foreach (int k in dropNext)
                        if (defs[k] != null && (defs[k].kind == PartKind.Tank || defs[k].IsSolid)) f += prop[k];
                    return f;
                }
                bool stopOnDrop = DropFuel() > 1e-9;

                var rep = new StageReport { number = s + 1, label = st.Describe(defs), startMass = Mass() };
                bool first = true;
                for (int guard = 0; guard < 64; guard++)
                {
                    if (stopOnDrop && DropFuel() <= 1e-9) break;
                    double mass = Mass();
                    double fVac = 0, fAsl = 0, mdot = 0;
                    var demand = new Dictionary<int, double>();
                    for (int i = 0; i < n; i++)
                    {
                        if (!pres[i] || !ign[i] || defs[i] == null || !defs[i].IsEngine) continue;
                        bool fuel = defs[i].IsSolid ? prop[i] > 1e-9 : Liquid(section[i]) > 1e-9;
                        if (!fuel) continue;
                        double md = defs[i].MaxMassFlow;
                        mdot += md; fVac += defs[i].thrustVac; fAsl += defs[i].ThrustAsl;
                        if (!defs[i].IsSolid) { demand.TryGetValue(section[i], out double dd); demand[section[i]] = dd + md; }
                    }
                    if (first)
                    {
                        rep.twrVac = fVac / (mass * gravity);
                        rep.twrAsl = fAsl / (mass * gravity);
                        rep.hasEngines = fVac > 0;
                        first = false;
                    }
                    if (mdot <= 0) break;
                    double tMin = double.PositiveInfinity;
                    foreach (var kv in demand) tMin = Math.Min(tMin, Liquid(kv.Key) / kv.Value);
                    for (int i = 0; i < n; i++)
                        if (pres[i] && ign[i] && defs[i] != null && defs[i].IsSolid && prop[i] > 1e-9) tMin = Math.Min(tMin, prop[i] / defs[i].MaxMassFlow);
                    if (double.IsInfinity(tMin) || tMin <= 0) break;
                    double m1 = mass - mdot * tMin;
                    double ln = Math.Log(mass / m1);
                    rep.dvVac += fVac / mdot * ln;
                    rep.dvAsl += fAsl / mdot * ln;
                    rep.burnTime += tMin;
                    foreach (var kv in demand)
                    {
                        double pool = Liquid(kv.Key);
                        double frac = Math.Min(1, kv.Value * tMin / pool);
                        for (int i = 0; i < n; i++)
                            if (pres[i] && defs[i] != null && defs[i].kind == PartKind.Tank && section[i] == kv.Key)
                                prop[i] = frac >= 1 - 1e-12 ? 0 : prop[i] * (1 - frac);
                    }
                    for (int i = 0; i < n; i++)
                        if (pres[i] && ign[i] && defs[i] != null && defs[i].IsSolid && prop[i] > 1e-9)
                        {
                            prop[i] -= defs[i].MaxMassFlow * tMin;
                            if (prop[i] < 1e-9) prop[i] = 0;
                        }
                }
                reports.Add(rep);
            }
            return reports;
        }
    }
}
