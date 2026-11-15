using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    public enum PartKind
    {
        Command, Parachute, HeatShield, NoseCone, Tank, Engine, Solid, Decoupler, Adapter, Fins,
        RadialDecoupler, Rcs, MonoTank, Leg, Light, EscapeTower,
        Wing, ControlSurface, Gear, Fairing, FairingHalf,
        DockingPort, Habitat, Solar, Antenna, Science, Battery,
        Scanner, Drill, OreTank, Converter, Radiator,
        Eva, Flag,   // not buildable: an astronaut out on EVA, and a planted flag
        Wheel, Frame, // rovers: driven wheels and the rover frame
        Grabber,      // latches onto a wanderer
        Rock,         // not buildable: a wanderer (a small rocky body passing through)
    }

    /// <summary>
    /// Static data for one part type. Masses in tonnes, thrust in kN, lengths in metres.
    /// Stack parts: local origin at the top centre, body along local -Y, stack nodes at y=0 and y=-length.
    /// Radial parts: local origin at the attach point on the parent's skin, body along local +X (outward).
    /// </summary>
    public class PartDef
    {
        public string id, name, description;
        public PartKind kind;
        public float length = 1f, topDiameter = 1.3f, bottomDiameter = 1.3f;
        public bool stackTop = true, stackBottom = true, surfaceAttach = true, radial;
        public Vector3 radialSize;       // radial parts: (outward, height, width)
        public float dryMass, propellant, mono;
        public float coolant;   // milestone 39: litres of coolant a tank holds (for engines running hot)
        public float thrustVac, ispVac, ispAsl, gimbalDeg;
        public float torque;             // reaction wheel, kN*m
        public float finArea;            // m^2
        public float chuteCdA;           // m^2 fully open
        public float rcsThrust;          // kN per direction (RCS blocks)
        public float legLength;          // m, landing legs
        public float lesThrust, lesBurn; // escape tower motor
        public float crashTolerance = 8f, maxTemp = 1400f, ablator;
        public int crew;
        public int variants;             // number of paint schemes (0 = fixed)
        public Color color = new Color(0.86f, 0.87f, 0.88f);
        public Color accent = new Color(0.22f, 0.23f, 0.25f);
        public Color lightColor = new Color(1f, 0.95f, 0.8f);

        // Lifting surfaces (radial): radialSize = (span, root chord, thickness); the leading edge sweeps back by 'sweep'.
        public float wingArea, tipChord, sweep, incidence;
        public float liftSlope = 3.2f;   // normal force per radian of angle of attack: ~4-4.5 for wings, ~3 for stubby fins
        public bool controlSurface;
        public float maxDeflect = 20f;
        // Landing gear
        public float wheelRadius, strutLength;
        // Rover wheels: motor force at the tyre (kN), top speed (m/s) and the electric charge (EC/s) the motor draws at full power
        public float motorForce, driveSpeed, motorEc;
        // Air-breathing jets (thrustVac mirrors jetThrust so staging treats them as engines)
        public float jetThrust, jetMaxSpeed = 1300f;
        // Payload fairing: a shell of this height/radius above the base, jettisoned as two halves
        public float fairingHeight, fairingRadius, fairingMass;
        // Docking ports latch only onto ports of the same size (0 = small, 1 = standard)
        public int dockSize = -1;
        // Deployable fragile parts (solar panels, antennas) tear off above this dynamic pressure (kPa) while deployed
        public float maxQ;
        public float maxPressure;          // kPa the part takes in the airflow (0 = by kind, see VesselSim.PressureLimit)
        // How much load (kN) the joint to its parent takes before tearing; 0 = work it out from the size
        public float jointStrength;
        // Science instrument: the experiment it runs (see Science.Experiments)
        public string experiment;
        // Electricity (EC): storage, idle draw, solar output in full sun (deployed), output from a running engine
        public float ecCapacity, ecIdle, solarRate, alternator;
        // Comms: antenna power in metres (link range = sqrt(powerA * powerB)); relays pass other vessels' signals on
        public float commPower;
        public bool relay;
        // Resources: ore storage (t); drills dig drillRate t/s at 10% ore with a bit reaching drillReach m below the part;
        // converters turn convertRate t/s of ore into fuel or monopropellant. Machines draw machineEc EC/s and make heatOut kW
        // while running, and shed passiveCool kW by themselves; radiators shed coolRate kW (both at 300 K above ambient).
        public float oreCapacity, drillRate, drillReach, convertRate, machineEc, heatOut, passiveCool, coolRate;
        // Orbital survey scanner: widest swath (degrees either side of the ground track)
        public float scanHalfAngle;
        public bool noDeploy;            // a radiator strip that's always out
        // Probe cores: which SAS modes their computer can hold (see Crew.TierNeeded); crewed parts rely on a pilot
        public int sasTier;
        // Supplies (crew-days): how much a part holds; a greenhouse grows supplyGrow crew-days a day using supplyEc EC/s
        public float supplyCapacity, supplyGrow, supplyEc;
        public float labRate;              // research lab: science per day for each scientist aboard (others count 40%)

        public bool IsJet => jetThrust > 0;
        public bool IsDeployable => !noDeploy && (kind == PartKind.Solar || kind == PartKind.Antenna || kind == PartKind.Scanner || kind == PartKind.Drill || kind == PartKind.Radiator);
        public bool IsMachine => drillRate > 0 || convertRate > 0;
        /// <summary>How far this antenna reaches the ground station.</summary>
        public string CommRangeText { get { double r = Math.Sqrt(commPower * CommNet.GroundPower); return r >= 1e6 ? $"{r / 1e6:0.#} Mm" : $"{r / 1000:0} km"; } }

        /// <summary>Joint rating: stack joints scale with the area of the joint; side mounts are much weaker.</summary>
        public float JointStrength
        {
            get
            {
                if (jointStrength > 0) return jointStrength;
                if (radial) return 150f;
                float d = Mathf.Max(0.3f, Mathf.Max(topDiameter, bottomDiameter));
                return 1500f * (d / 1.3f) * (d / 1.3f);
            }
        }
        public float LiftArea => wingArea > 0 ? wingArea : (radial ? finArea : 0);
        public float WetMass => supplyCapacity * (float)Supplies.MassPerDay + dryMass + propellant + mono + fairingMass;
        public bool IsEngine => thrustVac > 0;
        public bool IsSolid => kind == PartKind.Solid;
        public bool IsDecoupler => kind == PartKind.Decoupler || kind == PartKind.RadialDecoupler;
        public float MaxDiameter => radial ? Mathf.Max(radialSize.x, radialSize.z) : Mathf.Max(topDiameter, bottomDiameter);
        public float Height => radial ? radialSize.y : length;
        public double MaxMassFlow => IsEngine ? thrustVac / (ispVac * VesselSim.G0) : 0;
        public float ThrustAsl => IsEngine ? thrustVac * ispAsl / ispVac : 0;
        public float RMid => (topDiameter + bottomDiameter) * 0.25f;

        /// <summary>Centre of the part in its own frame (for wings: the planform centroid).</summary>
        public Vector3 CenterLocal
        {
            get
            {
                if (!radial) return new Vector3(0, -length * 0.5f, 0);
                if (wingArea <= 0) return new Vector3(radialSize.x * 0.5f, 0, 0);
                float span = radialSize.x, cr = radialSize.y, ct = tipChord;
                float xc = span * (cr + 2 * ct) / (3 * (cr + ct));
                float midTip = cr * 0.5f - sweep - ct * 0.5f; // mid-chord at the tip (root mid-chord is 0)
                return new Vector3(xc, midTip * xc / span, 0);
            }
        }

        /// <summary>Where a stack part touches a surface when side-mounted (its -X side).</summary>
        public Vector3 SurfaceAttachLocal => radial ? Vector3.zero : new Vector3(-RMid, -length * 0.5f, 0);

        public string Category
        {
            get
            {
                if (supplyCapacity > 0 && crew == 0 && kind != PartKind.Eva) return "Docking and crew";   // supply crates and modules, the greenhouse
                switch (kind)
                {
                    case PartKind.Command: return "Command";
                    case PartKind.Parachute:
                    case PartKind.HeatShield:
                    case PartKind.EscapeTower: return "Recovery and safety";
                    case PartKind.Tank: return "Fuel tanks";
                    case PartKind.Engine: return "Engines";
                    case PartKind.Solid: return "Solid boosters";
                    case PartKind.Decoupler:
                    case PartKind.RadialDecoupler: return "Staging";
                    case PartKind.Adapter: return "Structure";
                    case PartKind.Rcs:
                    case PartKind.MonoTank: return "RCS";
                    case PartKind.Leg:
                    case PartKind.Light:
                    case PartKind.Fairing: return "Utility";
                    case PartKind.Wing:
                    case PartKind.ControlSurface: return "Wings and control";
                    case PartKind.Gear: return "Landing gear";
                    case PartKind.Wheel:
                    case PartKind.Frame: return "Rovers";
                    case PartKind.DockingPort:
                    case PartKind.Grabber:
                    case PartKind.Habitat: return "Docking and crew";
                    case PartKind.Solar:
                    case PartKind.Antenna: return "Utility";
                    case PartKind.Science: return "Science";
                    case PartKind.Battery: return "Power";
                    case PartKind.Scanner:
                    case PartKind.Drill:
                    case PartKind.OreTank:
                    case PartKind.Converter:
                    case PartKind.Radiator: return "Mining";
                    default: return "Aerodynamics";
                }
            }
        }

        public string StatsLine
        {
            get
            {
                string s = $"{WetMass:0.##} t";
                if (propellant > 0) s += $" · fuel {propellant:0.##} t";
                if (mono > 0) s += $" · mono {mono:0.##} t";
                if (IsJet) s += $" · jet {jetThrust:0} kN (needs air)";
                else if (IsEngine) s += $" · {thrustVac:0} kN · Isp {ispVac:0}/{ispAsl:0} s";
                if (wingArea > 0) s += $" · {(controlSurface ? "control " : "")}wing {wingArea:0.#} m²";
                if (motorForce > 0) s += $" · drive {motorForce:0.#} kN to {driveSpeed:0} m/s, {motorEc:0.#} EC/s";
                else if (wheelRadius > 0) s += " · wheels, brakes";
                if (fairingHeight > 0) s += $" · shell {fairingHeight:0.#} m";
                if (torque > 0) s += $" · torque {torque:0.#}";
                if (rcsThrust > 0) s += $" · RCS {rcsThrust:0.#} kN";
                if (chuteCdA > 0) s += " · parachute";
                if (ablator > 0) s += $" · ablator {ablator:0.##} t";
                if (crew > 0) s += $" · {crew} crew";
                if (kind == PartKind.Command && crew == 0) s += $" · SAS {Crew.TierModes(sasTier)}";
                if (dockSize >= 0) s += dockSize == 0 ? " · small port" : " · standard port";
                if (maxQ > 0) s += $" · fragile above {maxQ:0.#} kPa";
                if (ecCapacity > 0 && kind != PartKind.Command) s += $" · {ecCapacity:0} EC";
                if (solarRate > 0) s += $" · {solarRate:0.#} EC/s in sun";
                if (commPower > 0 && kind != PartKind.Command) s += $" · {(relay ? "relay " : "")}range {CommRangeText}";
                if (oreCapacity > 0) s += $" · ore {oreCapacity:0.##} t";
                if (drillRate > 0) s += $" · {drillRate * 3600:0.#} t/h at 10% ore";
                if (convertRate > 0) s += $" · {convertRate * 3600:0.#} t/h of ore";
                if (scanHalfAngle > 0) s += " · maps ore from orbit";
                if (machineEc > 0) s += $" · {machineEc:0.#} EC/s";
                if (heatOut > 0) s += $" · heat {heatOut:0} kW";
                if (coolRate > 0) s += $" · sheds {coolRate:0} kW";
                if (radial) s += " · side-mount";
                return s;
            }
        }
    }

    public static class PartCatalog
    {
        static readonly Color Hull = new Color(0.86f, 0.87f, 0.88f);
        static readonly Color Dark = new Color(0.22f, 0.23f, 0.25f);
        static readonly Color Mid = new Color(0.50f, 0.52f, 0.55f);
        static readonly Color Orange = new Color(0.95f, 0.48f, 0.14f);
        static readonly Color Amber = new Color(0.95f, 0.76f, 0.20f);
        static readonly Color Teal = new Color(0.12f, 0.55f, 0.60f);

        /// <summary>Paint colours (milestone 34: sixteen, and most parts take them). 0 is each part's own colour.</summary>
        public static readonly Color[] Paints =
        {
            new Color(0.86f, 0.87f, 0.88f), new Color(0.2f, 0.21f, 0.23f), new Color(0.92f, 0.46f, 0.14f), new Color(0.75f, 0.62f, 0.32f),
            new Color(0.78f, 0.16f, 0.15f), new Color(0.18f, 0.36f, 0.78f), new Color(0.42f, 0.7f, 0.92f), new Color(0.2f, 0.52f, 0.28f),
            new Color(0.58f, 0.8f, 0.24f), new Color(0.95f, 0.8f, 0.2f), new Color(0.48f, 0.28f, 0.68f), new Color(0.92f, 0.52f, 0.66f),
            new Color(0.12f, 0.55f, 0.58f), new Color(0.5f, 0.52f, 0.55f), new Color(0.12f, 0.17f, 0.32f), new Color(0.5f, 0.34f, 0.22f),
        };
        public static readonly string[] PaintNames = { "White", "Black", "Orange", "Foil", "Red", "Blue", "Sky blue", "Green", "Lime", "Yellow", "Purple", "Pink", "Teal", "Grey", "Navy", "Brown" };

        static readonly HashSet<PartKind> PaintKinds = new HashSet<PartKind>
        {
            PartKind.Command, PartKind.Tank, PartKind.MonoTank, PartKind.Decoupler, PartKind.RadialDecoupler, PartKind.NoseCone, PartKind.Fins,
            PartKind.Wing, PartKind.ControlSurface, PartKind.Fairing, PartKind.FairingHalf, PartKind.Habitat, PartKind.Solid, PartKind.OreTank,
            PartKind.Battery, PartKind.Frame, PartKind.Adapter,
        };

        /// <summary>Whether a part can be painted (milestone 34: most body parts, not just the ones with paint schemes).</summary>
        public static bool Paintable(PartDef d) => d != null && (d.variants > 0 || PaintKinds.Contains(d.kind));

        public static readonly List<PartDef> All = new List<PartDef>
        {
            new PartDef { id = "pod.wren", name = "Wren Capsule", kind = PartKind.Command, length = 1.25f, topDiameter = 0.6f, bottomDiameter = 1.3f,
                dryMass = 0.85f, torque = 6f, crashTolerance = 14f, crew = 1, maxTemp = 1600f, ecCapacity = 60f, commPower = 2e4f,
                color = new Color(0.93f, 0.93f, 0.91f), accent = new Color(0.32f, 0.24f, 0.20f),
                supplyCapacity = 5f, description = "One-seat capsule with a built-in reaction wheel. Survives touchdowns up to 14 m/s." },
            new PartDef { id = "probe.spark", name = "Spark Probe Core", kind = PartKind.Command, length = 0.45f,
                dryMass = 0.15f, torque = 1.5f, crashTolerance = 10f, maxTemp = 1300f, ecCapacity = 30f, ecIdle = 0.03f, commPower = 2e4f, sasTier = 2, color = Mid, accent = Amber,
                description = "Uncrewed guidance core. Needs electricity (30 EC built in, 0.03 EC/s) and a signal home to take new commands. SAS holds prograde, normal and radial; not target or maneuver." },
            new PartDef { id = "probe.vireo", name = "Vireo Guidance Core", kind = PartKind.Command, length = 0.5f,
                dryMass = 0.3f, torque = 3f, crashTolerance = 10f, maxTemp = 1300f, ecCapacity = 60f, ecIdle = 0.06f, commPower = 2e4f, sasTier = 4,
                color = new Color(0.3f, 0.32f, 0.36f), accent = Teal,
                description = "Advanced probe core: SAS holds every mode, target and maneuver included, without a pilot. Draws 0.06 EC/s." },
            new PartDef { id = "chute.s", name = "Main Parachute", kind = PartKind.Parachute, length = 0.35f, topDiameter = 0.35f, bottomDiameter = 0.6f,
                dryMass = 0.1f, chuteCdA = 500f, jointStrength = 3000f, crashTolerance = 10f, maxTemp = 1300f, color = Orange, accent = Hull,
                description = "Waits until you are below ~280 m/s, opens partly, then fully below 2 km. Goes on top of a capsule." },
            new PartDef { id = "shield.s", name = "Ablative Heat Shield", kind = PartKind.HeatShield, length = 0.18f, topDiameter = 1.3f, bottomDiameter = 1.36f,
                dryMass = 0.3f, ablator = 0.2f, crashTolerance = 12f, maxTemp = 3200f, surfaceAttach = false,
                color = new Color(0.36f, 0.26f, 0.2f), accent = new Color(0.62f, 0.6f, 0.56f),
                description = "Goes under a capsule. Burns away ablator to survive fast reentries, like coming home from Tal." },
            new PartDef { id = "les.s", name = "Escape Tower", kind = PartKind.EscapeTower, length = 3.2f, topDiameter = 0.12f, bottomDiameter = 0.8f,
                stackTop = false, surfaceAttach = false, dryMass = 0.9f, lesThrust = 170f, lesBurn = 3f, jointStrength = 1500f, maxTemp = 1800f, color = Orange, accent = Dark,
                description = "Sits on top of the parachute. ABORT (Backspace) fires it to yank the capsule clear. Jettison it once you're high up." },
            new PartDef { id = "nose.s", name = "Aero Nose Cone", kind = PartKind.NoseCone, length = 1.1f, topDiameter = 0f, bottomDiameter = 1.3f,
                stackTop = false, dryMass = 0.05f, maxTemp = 1800f, variants = 4, color = Hull, accent = Teal,
                description = "Cuts drag on the top of a stack or a side booster." },
            new PartDef { id = "nose.l", name = "Aero Nose Cone 2.6", kind = PartKind.NoseCone, length = 1.9f, topDiameter = 0f, bottomDiameter = 2.6f,
                stackTop = false, dryMass = 0.2f, maxTemp = 1800f, variants = 4, color = Hull, accent = Teal,
                description = "A big pointed top for 2.6 m rockets and boosters." },
            new PartDef { id = "tank.s1", name = "FT-1 Tank", kind = PartKind.Tank, length = 1.0f, variants = 4,
                dryMass = 0.07f, propellant = 0.5f, crashTolerance = 6f, color = Hull, accent = Dark, description = "Short 1.3 m propellant tank." },
            new PartDef { id = "tank.s2", name = "FT-2 Tank", kind = PartKind.Tank, length = 1.9f, variants = 4,
                dryMass = 0.13f, propellant = 1.0f, crashTolerance = 6f, color = Hull, accent = Dark, description = "Medium 1.3 m propellant tank." },
            new PartDef { id = "tank.s3", name = "FT-3 Tank", kind = PartKind.Tank, length = 2.8f, variants = 4,   // milestone 32
                dryMass = 0.19f, propellant = 1.5f, crashTolerance = 6f, color = Hull, accent = Dark, description = "1.3 m propellant tank, between the FT-2 and the FT-4." },
            new PartDef { id = "tank.s4", name = "FT-4 Tank", kind = PartKind.Tank, length = 3.7f, variants = 4,
                dryMass = 0.25f, propellant = 2.0f, crashTolerance = 6f, color = Hull, accent = Dark, description = "Long 1.3 m propellant tank." },
            new PartDef { id = "tank.l2", name = "FT-2L Tank", kind = PartKind.Tank, length = 1.0f, topDiameter = 2.6f, bottomDiameter = 2.6f, variants = 4,   // milestone 32
                dryMass = 0.25f, propellant = 2.0f, crashTolerance = 6f, color = Hull, accent = Teal, description = "Short 2.6 m propellant tank: for landers and upper stages." },
            new PartDef { id = "tank.l4", name = "FT-4L Tank", kind = PartKind.Tank, length = 1.9f, topDiameter = 2.6f, bottomDiameter = 2.6f, variants = 4,
                dryMass = 0.5f, propellant = 4.0f, crashTolerance = 6f, color = Hull, accent = Teal, description = "Medium 2.6 m propellant tank." },
            new PartDef { id = "tank.l8", name = "FT-8L Tank", kind = PartKind.Tank, length = 3.7f, topDiameter = 2.6f, bottomDiameter = 2.6f, variants = 4,
                dryMass = 1.0f, propellant = 8.0f, crashTolerance = 6f, color = Hull, accent = Teal, description = "Long 2.6 m propellant tank." },
            // Milestone 32: a very long one
            new PartDef { id = "tank.l16", name = "FT-16L Tank", kind = PartKind.Tank, length = 7.2f, topDiameter = 2.6f, bottomDiameter = 2.6f, variants = 4,
                dryMass = 2.0f, propellant = 16.0f, crashTolerance = 6f, color = Hull, accent = Teal, description = "Very long 2.6 m propellant tank for big first stages." },
            new PartDef { id = "eng.kestrel", name = "Kestrel Lifter", kind = PartKind.Engine, length = 1.3f,
                dryMass = 1.1f, thrustVac = 200f, ispVac = 300f, ispAsl = 265f, gimbalDeg = 3f, alternator = 1f, crashTolerance = 7f, maxTemp = 1900f, color = Mid, accent = Dark,
                description = "Workhorse first-stage engine. Good at sea level." },
            new PartDef { id = "eng.ember", name = "Ember Vacuum Engine", kind = PartKind.Engine, length = 1.0f,
                dryMass = 0.45f, thrustVac = 60f, ispVac = 345f, ispAsl = 110f, gimbalDeg = 4f, alternator = 1f, crashTolerance = 7f, maxTemp = 1900f, color = Mid, accent = Dark,
                description = "Very efficient in space, nearly useless at sea level." },
            new PartDef { id = "eng.wisp", name = "Wisp Lander Engine", kind = PartKind.Engine, length = 0.55f, topDiameter = 0.8f, bottomDiameter = 0.8f,
                dryMass = 0.2f, thrustVac = 30f, ispVac = 315f, ispAsl = 250f, gimbalDeg = 5f, alternator = 0.5f, crashTolerance = 7f, maxTemp = 1900f, color = Mid, accent = Dark,
                description = "Small, throttleable engine for landers." },
            new PartDef { id = "eng.heron", name = "Heron Booster", kind = PartKind.Engine, length = 2.0f, topDiameter = 2.6f, bottomDiameter = 2.6f,
                dryMass = 4.5f, thrustVac = 1000f, ispVac = 305f, ispAsl = 275f, gimbalDeg = 2f, alternator = 2f, crashTolerance = 7f, maxTemp = 1900f, color = Mid, accent = Dark,
                description = "Big 2.6 m booster engine for heavy rockets." },
            new PartDef { id = "eng.swift", name = "Swift Sustainer", kind = PartKind.Engine, length = 1.1f,
                dryMass = 0.7f, thrustVac = 120f, ispVac = 320f, ispAsl = 240f, gimbalDeg = 4f, alternator = 1f, crashTolerance = 7f, maxTemp = 1900f, color = Mid, accent = Dark,
                description = "A middle-sized 1.3 m engine that works both low down and in space: good for second stages." },
            new PartDef { id = "eng.condor", name = "Condor Vacuum Engine", kind = PartKind.Engine, length = 1.6f, topDiameter = 2.6f, bottomDiameter = 2.6f,
                dryMass = 1.8f, thrustVac = 400f, ispVac = 350f, ispAsl = 150f, gimbalDeg = 3f, alternator = 2f, crashTolerance = 7f, maxTemp = 1900f, color = Mid, accent = Dark,
                description = "Big 2.6 m engine for space: very efficient up there, weak at sea level." },
            new PartDef { id = "srb.cinder", name = "Cinder Solid Booster", kind = PartKind.Solid, length = 4.5f, variants = 4,
                dryMass = 0.8f, propellant = 3.2f, thrustVac = 160f, ispVac = 205f, ispAsl = 185f, crashTolerance = 7f, maxTemp = 1700f,
                color = new Color(0.90f, 0.90f, 0.86f), accent = new Color(0.75f, 0.16f, 0.12f),
                description = "Cheap, strong thrust. Can't be throttled or shut down once lit. Great as side boosters." },
            new PartDef { id = "srb.flint", name = "Flint Small Booster", kind = PartKind.Solid, length = 2.6f, variants = 4,
                dryMass = 0.4f, propellant = 1.5f, thrustVac = 85f, ispVac = 200f, ispAsl = 180f, crashTolerance = 7f, maxTemp = 1700f,
                color = new Color(0.90f, 0.90f, 0.86f), accent = new Color(0.75f, 0.16f, 0.12f),
                description = "A short solid booster: a quick push for small rockets, or a pair on the side." },
            new PartDef { id = "srb.blaze", name = "Blaze Heavy Booster", kind = PartKind.Solid, length = 8.0f, topDiameter = 2.6f, bottomDiameter = 2.6f, variants = 4,
                dryMass = 3.5f, propellant = 17f, thrustVac = 950f, ispVac = 215f, ispAsl = 195f, crashTolerance = 7f, maxTemp = 1700f,
                color = new Color(0.90f, 0.90f, 0.86f), accent = new Color(0.75f, 0.16f, 0.12f),
                description = "A huge 2.6 m solid booster: lifts heavy rockets off the pad, then falls away." },
            new PartDef { id = "dec.s", name = "Stack Decoupler", kind = PartKind.Decoupler, length = 0.2f, surfaceAttach = false,
                dryMass = 0.05f, color = Dark, accent = Amber,
                description = "Splits the rocket when staged; the part below falls away. Fuel can't flow through it." },
            new PartDef { id = "dec.l", name = "Stack Decoupler 2.6", kind = PartKind.Decoupler, length = 0.25f, topDiameter = 2.6f, bottomDiameter = 2.6f, surfaceAttach = false,
                dryMass = 0.15f, color = Dark, accent = Amber, description = "2.6 m decoupler." },
            new PartDef { id = "dec.r", name = "Radial Decoupler", kind = PartKind.RadialDecoupler, radial = true, radialSize = new Vector3(0.3f, 0.9f, 0.4f),
                stackTop = false, stackBottom = false, dryMass = 0.05f, jointStrength = 400f, color = Dark, accent = Amber,
                description = "Side-mount it, then hang a booster on its outer face. Pushes the booster away when staged." },
            new PartDef { id = "adapt.ls", name = "Adapter 1.3 / 2.6", kind = PartKind.Adapter, length = 0.9f, topDiameter = 1.3f, bottomDiameter = 2.6f, variants = 4,
                dryMass = 0.12f, color = Hull, accent = Dark, description = "Joins a 1.3 m part above to a 2.6 m part below." },
            new PartDef { id = "adapt.sl", name = "Adapter 2.6 / 1.3", kind = PartKind.Adapter, length = 0.9f, topDiameter = 2.6f, bottomDiameter = 1.3f, variants = 4,
                dryMass = 0.12f, color = Hull, accent = Dark, description = "Joins a 2.6 m part above to a 1.3 m part below (a narrow stage under a wide one)." },
            new PartDef { id = "fins.s", name = "Tail Fins", kind = PartKind.Fins, length = 0.8f, finArea = 2.4f, variants = 4,
                dryMass = 0.08f, color = Hull, accent = Orange, description = "Ring of four fins. Keeps the rocket pointed into the airflow." },
            new PartDef { id = "fins.l", name = "Tail Fins 2.6", kind = PartKind.Fins, length = 1.2f, topDiameter = 2.6f, bottomDiameter = 2.6f, finArea = 7f, variants = 4,
                dryMass = 0.25f, color = Hull, accent = Orange, description = "Bigger fin ring for 2.6 m rockets." },
            new PartDef { id = "fin.r", name = "Radial Fin", kind = PartKind.Fins, radial = true, radialSize = new Vector3(0.9f, 1.1f, 0.06f), finArea = 0.9f,
                stackTop = false, stackBottom = false, dryMass = 0.04f, variants = 4, color = Hull, accent = Orange,
                description = "A single fin. Place 3 or 4 with symmetry, low on the rocket." },
            new PartDef { id = "fin.l", name = "Big Radial Fin", kind = PartKind.Fins, radial = true, radialSize = new Vector3(1.4f, 1.8f, 0.08f), finArea = 2.0f,
                stackTop = false, stackBottom = false, dryMass = 0.09f, variants = 4, color = Hull, accent = Orange,
                description = "A big single fin for 2.6 m rockets. Place 3 or 4 with symmetry, low down." },
            new PartDef { id = "rcs.b", name = "RCS Thruster Block", kind = PartKind.Rcs, radial = true, radialSize = new Vector3(0.22f, 0.3f, 0.3f),
                stackTop = false, stackBottom = false, dryMass = 0.04f, rcsThrust = 1.0f, color = Mid, accent = Dark,
                description = "Monopropellant puffers for fine control. R toggles RCS; H/N, I/K, J/L translate." },
            new PartDef { id = "mono.s", name = "Monoprop Tank", kind = PartKind.MonoTank, length = 0.6f, variants = 4,
                dryMass = 0.1f, mono = 0.4f, color = Hull, accent = Teal, description = "Stack tank of monopropellant for RCS." },
            new PartDef { id = "mono.r", name = "Radial Monoprop Tank", kind = PartKind.MonoTank, radial = true, radialSize = new Vector3(0.45f, 0.9f, 0.45f),
                stackTop = false, stackBottom = false, dryMass = 0.04f, mono = 0.12f, color = Hull, accent = Teal, description = "Side-mounted monoprop tank." },
            // milestone 39: keeping engines cool (the radiators cool engines too now)
            new PartDef { id = "cool.s", name = "Coolant Tank", kind = PartKind.MonoTank, radial = true, radialSize = new Vector3(0.4f, 0.7f, 0.4f),
                stackTop = false, stackBottom = false, dryMass = 0.05f, coolant = 120f, color = new Color(0.2f, 0.55f, 0.85f), accent = Mid,
                description = "120 litres of coolant. An engine getting too hot draws on it before it has to shut down. Refill with a coolant canister." },
            new PartDef { id = "leg.s", name = "Landing Leg", kind = PartKind.Leg, radial = true, radialSize = new Vector3(0.25f, 1.1f, 0.22f),
                stackTop = false, stackBottom = false, dryMass = 0.05f, legLength = 1.7f, crashTolerance = 12f, color = Mid, accent = Dark,
                description = "G deploys the gear. Use 3 or 4 with symmetry near the bottom of a lander. Takes 12 m/s." },
            new PartDef { id = "light.s", name = "Spotlight", kind = PartKind.Light, radial = true, radialSize = new Vector3(0.18f, 0.18f, 0.18f),
                stackTop = false, stackBottom = false, dryMass = 0.01f, color = Mid, accent = Dark,
                description = "U toggles the lights. Handy on the night side." },
            new PartDef { id = "fairing.s", name = "Payload Fairing", kind = PartKind.Fairing, length = 0.3f,
                dryMass = 0.08f, fairingHeight = 4.6f, fairingRadius = 0.78f, fairingMass = 0.25f, color = Hull, accent = Dark, variants = 4,
                description = "Stack your payload on top: a 4.6 m shell protects it from drag and heat. Staging splits it into two halves." },

            // ----- Aircraft
            new PartDef { id = "cockpit.swift", name = "Swift Cockpit", kind = PartKind.Command, length = 2.0f, topDiameter = 0.2f, bottomDiameter = 1.3f,
                stackTop = false, dryMass = 1.0f, torque = 4f, crashTolerance = 12f, crew = 1, maxTemp = 1500f, ecCapacity = 40f, commPower = 2e4f,
                color = new Color(0.9f, 0.9f, 0.88f), accent = new Color(0.12f, 0.2f, 0.3f),
                supplyCapacity = 3f, description = "One-seat aircraft cockpit. Goes at the front of a fuselage." },
            new PartDef { id = "tail.cone", name = "Tail Cone", kind = PartKind.Adapter, length = 1.4f, topDiameter = 1.3f, bottomDiameter = 0.3f,
                stackBottom = false, dryMass = 0.08f, variants = 4, color = Hull, accent = Dark,
                description = "Streamlined tail for gliders and the back of a fuselage." },
            new PartDef { id = "eng.gale", name = "Gale Turbojet", kind = PartKind.Engine, length = 1.6f,
                dryMass = 1.0f, jetThrust = 120f, thrustVac = 120f, ispVac = 3500f, ispAsl = 3500f, alternator = 1.5f, crashTolerance = 7f, maxTemp = 1600f,
                color = Mid, accent = Dark,
                description = "Air-breathing jet: 120 kN, sips fuel, spools up over ~2 s. Strongest around 600-800 m/s; useless above ~25 km or in space." },
            new PartDef { id = "wing.delta", name = "Delta Wing", kind = PartKind.Wing, radial = true, radialSize = new Vector3(3.5f, 3.2f, 0.12f),
                tipChord = 0.6f, sweep = 2.6f, wingArea = 6.65f, incidence = 2f, liftSlope = 4.2f, jointStrength = 300f, stackTop = false, stackBottom = false,
                dryMass = 0.3f, crashTolerance = 12f, variants = 4, color = Hull, accent = Dark,
                description = "Big swept wing. Use mirror symmetry (R), mounted on the side of the fuselage behind the centre of mass." },
            new PartDef { id = "wing.std", name = "Straight Wing", kind = PartKind.Wing, radial = true, radialSize = new Vector3(3.0f, 1.6f, 0.1f),
                tipChord = 1.0f, sweep = 0.4f, wingArea = 3.9f, incidence = 2f, liftSlope = 4.4f, jointStrength = 200f, stackTop = false, stackBottom = false,
                dryMass = 0.18f, crashTolerance = 12f, variants = 4, color = Hull, accent = Dark,
                description = "Efficient straight wing for slow, gentle aircraft." },
            new PartDef { id = "elevon", name = "Elevon", kind = PartKind.ControlSurface, radial = true, radialSize = new Vector3(1.4f, 0.6f, 0.06f),
                tipChord = 0.5f, sweep = 0.1f, wingArea = 0.8f, liftSlope = 3.8f, jointStrength = 60f, controlSurface = true, maxDeflect = 20f, stackTop = false, stackBottom = false,
                dryMass = 0.05f, color = Hull, accent = Orange,
                description = "Moving surface. Works out pitch, roll and yaw by itself wherever you put it. Best far from the centre of mass." },
            new PartDef { id = "tail.fin", name = "Tail Fin", kind = PartKind.ControlSurface, radial = true, radialSize = new Vector3(1.6f, 1.4f, 0.08f),
                tipChord = 0.6f, sweep = 0.8f, wingArea = 1.6f, liftSlope = 3.8f, jointStrength = 80f, controlSurface = true, maxDeflect = 15f, stackTop = false, stackBottom = false,
                dryMass = 0.15f, color = Hull, accent = Teal,
                description = "All-moving vertical tail: keeps the nose straight and steers left/right. Mount it on top, at the back." },
            new PartDef { id = "gear.s", name = "Landing Gear", kind = PartKind.Gear, radial = true, radialSize = new Vector3(0.3f, 0.45f, 0.3f),
                wheelRadius = 0.32f, strutLength = 1.0f, stackTop = false, stackBottom = false, dryMass = 0.08f, crashTolerance = 9f,
                color = Mid, accent = Dark,
                description = "Retractable wheel (G). B toggles brakes; wheels ahead of the centre of mass steer with A/D. Put the main gear just behind the CoM." },

            // ----- Milestone 46 (captaincarl24: "create 10 more plane presets following like the space shuttle, commercial planes,
            // supersonic planes like the concorde and commercial like the boeing and airbus"): parts for big planes
            new PartDef { id = "cockpit.liner", name = "Airliner Cockpit", kind = PartKind.Command, length = 3.0f, topDiameter = 0.5f, bottomDiameter = 2.6f,
                stackTop = false, dryMass = 2.4f, torque = 14f, crashTolerance = 12f, crew = 2, maxTemp = 1500f, ecCapacity = 150f, commPower = 5e4f,
                color = new Color(0.94f, 0.94f, 0.92f), accent = new Color(0.12f, 0.18f, 0.26f),
                supplyCapacity = 8f, description = "Two-seat cockpit for big planes: pilot and co-pilot side by side. Goes at the front of a 2.6 m fuselage." },
            new PartDef { id = "cockpit.arrow", name = "Needle Nose Cockpit", kind = PartKind.Command, length = 4.2f, topDiameter = 0.15f, bottomDiameter = 2.6f,
                stackTop = false, dryMass = 2.6f, torque = 12f, crashTolerance = 10f, crew = 3, maxTemp = 2000f, ecCapacity = 150f, commPower = 5e4f,
                color = new Color(0.96f, 0.96f, 0.95f), accent = new Color(0.1f, 0.14f, 0.22f),
                supplyCapacity = 8f, description = "A long, pointed nose for flying faster than sound: three seats, and it takes the heat of Mach 2." },
            new PartDef { id = "cockpit.orbiter", name = "Orbiter Cockpit", kind = PartKind.Command, length = 3.2f, topDiameter = 0.9f, bottomDiameter = 2.6f,
                stackTop = false, dryMass = 3.4f, torque = 24f, crashTolerance = 14f, crew = 4, maxTemp = 2800f, ecCapacity = 400f, commPower = 2e5f,
                color = new Color(0.95f, 0.95f, 0.93f), accent = new Color(0.08f, 0.08f, 0.09f),
                supplyCapacity = 24f, description = "A spaceplane's cockpit for four, with heat tiles underneath: launch it like a rocket, glide home and land on a runway." },
            new PartDef { id = "cabin.liner", name = "Passenger Cabin", kind = PartKind.Habitat, length = 3.2f, topDiameter = 2.6f, bottomDiameter = 2.6f, variants = 4,
                crew = 8, dryMass = 2.2f, maxTemp = 1300f, ecCapacity = 60f, color = new Color(0.94f, 0.94f, 0.92f), accent = new Color(0.15f, 0.35f, 0.65f),
                supplyCapacity = 20f, description = "Eight seats in rows, with windows and overhead bins. Stack them behind an airliner cockpit." },
            new PartDef { id = "tail.l", name = "Tail Cone 2.6", kind = PartKind.Adapter, length = 3.2f, topDiameter = 2.6f, bottomDiameter = 0.5f,
                stackBottom = false, dryMass = 0.4f, variants = 4, color = Hull, accent = Dark,
                description = "Streamlined tail for a 2.6 m fuselage. Put the tail fin and elevons on it." },
            new PartDef { id = "eng.fan", name = "Breeze Turbofan", kind = PartKind.Engine, length = 2.4f, topDiameter = 1.5f, bottomDiameter = 1.3f,
                dryMass = 2.0f, jetThrust = 160f, thrustVac = 160f, ispVac = 7000f, ispAsl = 7000f, jetMaxSpeed = 520f, alternator = 3f, crashTolerance = 7f, maxTemp = 1500f,
                color = new Color(0.85f, 0.86f, 0.88f), accent = Dark,
                description = "Big, quiet and thrifty: 160 kN for airliners, best just under the speed of sound (useless past Mach 1.5). Mount it on the side of a fuel tank." },
            new PartDef { id = "eng.mistral", name = "Mistral Afterburner Jet", kind = PartKind.Engine, length = 2.6f, topDiameter = 1.3f, bottomDiameter = 1.1f,
                dryMass = 1.8f, jetThrust = 170f, thrustVac = 170f, ispVac = 2400f, ispAsl = 2400f, jetMaxSpeed = 1150f, alternator = 1.5f, crashTolerance = 7f, maxTemp = 2000f,
                color = Mid, accent = Dark,
                description = "A supersonic jet with an afterburner: 170 kN, strongest around Mach 2, and thirsty. For Concorde-style airliners and fast jets." },
            new PartDef { id = "wing.swept", name = "Swept Airliner Wing", kind = PartKind.Wing, radial = true, radialSize = new Vector3(9.0f, 4.2f, 0.22f),
                tipChord = 1.4f, sweep = 3.0f, wingArea = 25f, incidence = 2.5f, liftSlope = 4.6f, jointStrength = 1400f, stackTop = false, stackBottom = false,
                dryMass = 1.4f, crashTolerance = 12f, variants = 4, color = Hull, accent = Dark,
                description = "A long swept wing for airliners and cargo planes. Mirror it (R) on the fuselage sides, about at the centre of mass." },
            new PartDef { id = "wing.ogee", name = "Ogee Delta Wing", kind = PartKind.Wing, radial = true, radialSize = new Vector3(5.6f, 10f, 0.2f),
                tipChord = 1.0f, sweep = 8.0f, wingArea = 30f, incidence = 2f, liftSlope = 3.6f, jointStrength = 1600f, maxTemp = 2400f, stackTop = false, stackBottom = false,
                dryMass = 2.0f, crashTolerance = 12f, variants = 4, color = Hull, accent = Dark,
                description = "A huge, slender delta for supersonic airliners and spaceplanes: it lifts well at Mach 2 and takes reentry heat." },
            new PartDef { id = "elevon.l", name = "Big Elevon", kind = PartKind.ControlSurface, radial = true, radialSize = new Vector3(2.8f, 1.1f, 0.08f),
                tipChord = 0.8f, sweep = 0.3f, wingArea = 2.6f, liftSlope = 3.8f, jointStrength = 200f, controlSurface = true, maxDeflect = 20f, stackTop = false, stackBottom = false,
                dryMass = 0.15f, color = Hull, accent = Orange,
                description = "A bigger moving surface for heavy planes: pitch, roll and yaw. Put it at the back, far from the centre of mass." },
            new PartDef { id = "fin.tall", name = "Tall Tail Fin", kind = PartKind.ControlSurface, radial = true, radialSize = new Vector3(3.6f, 3.0f, 0.12f),
                tipChord = 1.2f, sweep = 1.8f, wingArea = 6.5f, liftSlope = 3.8f, jointStrength = 300f, controlSurface = true, maxDeflect = 15f, stackTop = false, stackBottom = false,
                dryMass = 0.35f, color = Hull, accent = Teal,
                description = "A big vertical tail for airliners: keeps the nose straight and steers left and right. Mount it on top at the back." },
            new PartDef { id = "gear.l", name = "Heavy Landing Gear", kind = PartKind.Gear, radial = true, radialSize = new Vector3(0.45f, 0.7f, 0.45f),
                wheelRadius = 0.55f, strutLength = 1.7f, stackTop = false, stackBottom = false, dryMass = 0.3f, crashTolerance = 10f,
                color = Mid, accent = Dark,
                description = "Big retractable wheels for heavy planes (G). B brakes; wheels ahead of the centre of mass steer." },

            // ----- Docking, crew and utility
            new PartDef { id = "port.sm", name = "Small Docking Port", kind = PartKind.DockingPort, length = 0.25f, topDiameter = 0.65f, bottomDiameter = 0.65f,
                dockSize = 0, dryMass = 0.03f, crashTolerance = 10f, color = Mid, accent = Amber,
                description = "Joins two vessels. Fly its free face to another small port, lined up, slower than 1.5 m/s. Fuel and crew can move across." },
            new PartDef { id = "port.std", name = "Docking Port", kind = PartKind.DockingPort, length = 0.3f,
                dockSize = 1, dryMass = 0.06f, crashTolerance = 10f, color = Mid, accent = Amber,
                description = "Standard 1.3 m docking port. Latches onto another standard port when they meet face to face, lined up, slower than 1.5 m/s." },
            new PartDef { id = "eng.lumen", name = "Lumen Drive", kind = PartKind.Engine, length = 2.4f, topDiameter = 1.3f, bottomDiameter = 1.3f,
                dryMass = 2.6f, thrustVac = 45f, ispVac = 880f, ispAsl = 160f, gimbalDeg = 2f, maxTemp = 2200f, alternator = 3f, color = Mid, accent = Teal,
                description = "A long, hot, very efficient engine for the far planets: 880 s in vacuum, nearly three times the Ember, but only 45 kN and hopeless in air (160 s at sea level). Makes 3 EC/s while it burns." },
            new PartDef { id = "dish.x", name = "Great Dish", kind = PartKind.Antenna, radial = true, radialSize = new Vector3(0.5f, 2.6f, 2.6f),
                stackTop = false, stackBottom = false, dryMass = 0.6f, commPower = 6e13f, relay = true, maxQ = 1.5f, ecIdle = 0.2f, color = new Color(0.9f, 0.9f, 0.88f), accent = Mid,
                description = "A folding 2.6 m dish (Y to unfold) that reaches the deep space stations from Brume, and relays other vessels' signals. Tears off in any real air." },
            new PartDef { id = "sup.s", name = "Supply Crate", kind = PartKind.Battery, radial = true, radialSize = new Vector3(0.35f, 0.9f, 0.6f),
                stackTop = false, stackBottom = false, dryMass = 0.08f, supplyCapacity = 30f, color = new Color(0.82f, 0.8f, 0.72f), accent = Teal,
                description = "Food, water and air for 30 crew-days (one crew member for a month), 0.15 t of it. Side-mounted." },
            new PartDef { id = "sup.l", name = "Supply Module", kind = PartKind.Habitat, length = 1.9f, variants = 4,
                dryMass = 0.3f, supplyCapacity = 150f, color = new Color(0.86f, 0.85f, 0.8f), accent = Teal,
                description = "150 crew-days of supplies (three crew for seven weeks), 0.75 t of them." },
            new PartDef { id = "garden.s", name = "Greenhouse", kind = PartKind.Habitat, length = 2.2f,
                dryMass = 1.4f, supplyCapacity = 10f, supplyGrow = 2f, supplyEc = 4f, color = new Color(0.8f, 0.9f, 0.82f), accent = new Color(0.25f, 0.6f, 0.3f),
                description = "Grows and recycles food, water and air: enough for two crew (2 crew-days a day) while it has 4 EC/s of power. Pair it with solar panels." },
            new PartDef { id = "grab.s", name = "Wanderer Grabber", kind = PartKind.Grabber, length = 0.9f, topDiameter = 1.3f, bottomDiameter = 1.3f,
                stackTop = false, dryMass = 0.45f, crashTolerance = 12f, jointStrength = 900f, experiment = "rock", ecCapacity = 20f, color = Mid, accent = Orange,
                description = "Three claws that bite into a wanderer: fly its open end into one slower than 1.5 m/s and it holds on. Takes rock samples (science) while it holds one. Release it like a docking port." },
            new PartDef { id = "base.hub", name = "Base Hub", kind = PartKind.Command, length = 2.0f, topDiameter = 3.2f, bottomDiameter = 3.2f,
                crew = 4, dryMass = 2.4f, torque = 2f, maxTemp = 1200f, crashTolerance = 10f, ecCapacity = 400f, ecIdle = 0.1f, supplyCapacity = 60f,
                color = Hull, accent = Teal,
                description = "The heart of a base: a squat, wide module for four with a big battery and 60 crew-days of supplies. Land it, then cable other modules to it on EVA." },
            new PartDef { id = "base.lab", name = "Research Lab", kind = PartKind.Habitat, length = 2.4f, topDiameter = 2.6f, bottomDiameter = 2.6f, variants = 4,
                crew = 2, dryMass = 2.0f, maxTemp = 1200f, ecCapacity = 100f, ecIdle = 0.5f, labRate = 2f, color = Hull, accent = Teal,
                description = "Two scientists study the world around them: about 2 science a day each (more on far worlds, a quarter more landed), sent home while the lab has power. Career only." },
            new PartDef { id = "base.tank", name = "Base Storage Tank", kind = PartKind.Tank, length = 3.0f, topDiameter = 2.6f, bottomDiameter = 2.6f, variants = 4,
                dryMass = 1.4f, propellant = 10f, oreCapacity = 4f, crashTolerance = 8f, color = new Color(0.82f, 0.8f, 0.74f), accent = Teal,
                description = "A base's depot: 10 t of propellant and 4 t of ore. Cabled to a base, it shares ore and pours its fuel into any landed ship plugged in." },
            new PartDef { id = "hab.s", name = "Habitat Module", kind = PartKind.Habitat, length = 2.4f, variants = 4,
                crew = 4, dryMass = 1.6f, maxTemp = 1200f, ecCapacity = 150f, color = Hull, accent = Teal,
                supplyCapacity = 40f, description = "Living space for four. Doesn't steer the ship by itself: it needs a probe core or a crewed command part." },
            new PartDef { id = "solar.s", name = "Solar Panel", kind = PartKind.Solar, radial = true, radialSize = new Vector3(0.12f, 0.7f, 0.35f),
                stackTop = false, stackBottom = false, dryMass = 0.02f, maxQ = 1.5f, solarRate = 1.2f, color = new Color(0.16f, 0.22f, 0.45f), accent = Mid,
                description = "Unfolds with Y (Deploy): up to 1.2 EC/s facing the sun, nothing in a planet's shadow. Fragile: deployed, it tears off above 1.5 kPa." },
            new PartDef { id = "antenna.s", name = "Antenna", kind = PartKind.Antenna, radial = true, radialSize = new Vector3(0.1f, 0.3f, 0.1f),
                stackTop = false, stackBottom = false, dryMass = 0.01f, maxQ = 4f, commPower = 2e6f, color = Mid, accent = Hull,
                description = "Extends with Y (Deploy): reaches the ground station from 20 Mm, far enough for Tal but not Vesk (take a relay dish). Snaps off above 4 kPa." },

            // ----- Science instruments
            new PartDef { id = "sci.therm", name = "Thermometer", kind = PartKind.Science, radial = true, radialSize = new Vector3(0.1f, 0.22f, 0.1f),
                stackTop = false, stackBottom = false, dryMass = 0.005f, experiment = "temp", color = Mid, accent = Orange,
                description = "Temperature scan. Works anywhere; full value when transmitted." },
            new PartDef { id = "sci.baro", name = "Barometer", kind = PartKind.Science, radial = true, radialSize = new Vector3(0.1f, 0.22f, 0.14f),
                stackTop = false, stackBottom = false, dryMass = 0.005f, experiment = "pressure", color = Mid, accent = Teal,
                description = "Pressure scan. Only where there's air (or on the ground of a world with air)." },
            new PartDef { id = "sci.bio", name = "Sample Canister", kind = PartKind.Science, radial = true, radialSize = new Vector3(0.32f, 0.55f, 0.32f),
                stackTop = false, stackBottom = false, dryMass = 0.05f, experiment = "samples", color = Hull, accent = Amber,
                description = "Collects samples anywhere. Worth a lot more brought home than transmitted (30%)." },
            new PartDef { id = "sci.ground", name = "Ground Probe", kind = PartKind.Science, radial = true, radialSize = new Vector3(0.25f, 0.5f, 0.25f),
                stackTop = false, stackBottom = false, dryMass = 0.06f, experiment = "ground", color = Mid, accent = Amber,
                description = "Drills into the surface: landed only. Transmits at 50%." },

            // ----- Power and comms
            new PartDef { id = "battery.r", name = "Battery Pack", kind = PartKind.Battery, radial = true, radialSize = new Vector3(0.1f, 0.32f, 0.26f),
                stackTop = false, stackBottom = false, dryMass = 0.01f, ecCapacity = 100f, color = Mid, accent = Amber,
                description = "Side-mounted battery: 100 EC. Probes need power to steer and to listen." },
            new PartDef { id = "battery.s", name = "Battery Bank", kind = PartKind.Battery, length = 0.25f,
                dryMass = 0.1f, ecCapacity = 600f, color = Dark, accent = Amber,
                description = "Stack battery bank: 600 EC." },
            // ----- Rovers
            new PartDef { id = "frame.r", name = "Rover Frame", kind = PartKind.Frame, length = 0.4f, topDiameter = 1.3f, bottomDiameter = 1.3f,
                dryMass = 0.22f, ecCapacity = 250f, torque = 1.5f, crashTolerance = 10f, color = Mid, accent = Dark,
                description = "A flat box chassis with a 250 EC battery and a small reaction wheel. Build it lying down (plane view), put wheels on both sides and a probe core or cockpit on top." },
            new PartDef { id = "wheel.s", name = "Rover Wheel", kind = PartKind.Wheel, radial = true, radialSize = new Vector3(0.22f, 0.62f, 0.62f),
                wheelRadius = 0.3f, strutLength = 0.3f, motorForce = 1.0f, driveSpeed = 10f, motorEc = 0.5f,
                stackTop = false, stackBottom = false, dryMass = 0.06f, crashTolerance = 8f, color = Dark, accent = Mid,
                description = "Electric wheel: W/S drive, A/D steer, B brakes. Mount them on the sides, axles pointing out; front and back wheels steer opposite ways." },
            new PartDef { id = "wheel.l", name = "Big Rover Wheel", kind = PartKind.Wheel, radial = true, radialSize = new Vector3(0.34f, 1.1f, 1.1f),
                wheelRadius = 0.55f, strutLength = 0.45f, motorForce = 3.5f, driveSpeed = 14f, motorEc = 1.4f,
                stackTop = false, stackBottom = false, dryMass = 0.18f, crashTolerance = 12f, color = Dark, accent = Mid,
                description = "Bigger, stronger electric wheel for heavy rovers and rough ground. Draws more charge." },
            new PartDef { id = "dish.d", name = "Deep Space Dish", kind = PartKind.Antenna, radial = true, radialSize = new Vector3(0.3f, 0.75f, 0.75f),
                stackTop = false, stackBottom = false, dryMass = 0.25f, maxQ = 1f, commPower = 8e12f, relay = true, color = Hull, accent = Mid,
                description = "Unfolds with Y. Reaches the ground from 40 Gm: Ochre and back from anywhere in its orbit, unless the sun is in the way. Passes other vessels' signals on too." },
            new PartDef { id = "relay.s", name = "Relay Dish", kind = PartKind.Antenna, radial = true, radialSize = new Vector3(0.14f, 0.36f, 0.3f),
                stackTop = false, stackBottom = false, dryMass = 0.08f, maxQ = 2f, commPower = 1e7f, relay = true, color = Hull, accent = Mid,
                description = "Unfolds with Y. Reaches the ground from 45 Mm, so probes at Vesk need one. Passes other vessels' signals on to the ground: put a few in high orbit so probes behind Arden, Tal or Vesk can still be flown." },
            new PartDef { id = "solar.l", name = "Solar Array", kind = PartKind.Solar, radial = true, radialSize = new Vector3(0.16f, 0.9f, 0.45f),
                stackTop = false, stackBottom = false, dryMass = 0.12f, maxQ = 1f, solarRate = 5f, color = new Color(0.14f, 0.2f, 0.42f), accent = Hull,
                description = "Big fold-out array (Y): up to 5 EC/s facing the sun. Enough to run a drill and a converter. Very fragile in air (1 kPa)." },

            // ----- Mining: survey, drills, ore, refining, cooling
            new PartDef { id = "scan.orbit", name = "Survey Scanner", kind = PartKind.Scanner, radial = true, radialSize = new Vector3(0.22f, 0.45f, 0.3f),
                stackTop = false, stackBottom = false, dryMass = 0.1f, maxQ = 2f, machineEc = 0.4f, scanHalfAngle = 12f, color = Hull, accent = Teal,
                description = "Unfold it (Y) in orbit to map ore under your ground track (0.4 EC/s). A polar orbit maps a whole world as it turns underneath. Open the ore map with O in map view." },
            new PartDef { id = "drill.s", name = "Ore Drill", kind = PartKind.Drill, radial = true, radialSize = new Vector3(0.42f, 1.0f, 0.42f),
                stackTop = false, stackBottom = false, dryMass = 0.25f, drillRate = 0.0015f, drillReach = 2.0f, machineEc = 1.5f, heatOut = 30f, passiveCool = 45f,
                crashTolerance = 9f, color = Amber, accent = Dark,
                description = "Mount it low and unfold it (Y) once landed: the bit reaches 2 m down. 5.4 t of ore an hour at 10% ore. Runs cool enough without radiators." },
            new PartDef { id = "drill.l", name = "Heavy Drill", kind = PartKind.Drill, radial = true, radialSize = new Vector3(0.66f, 1.5f, 0.66f),
                stackTop = false, stackBottom = false, dryMass = 0.9f, drillRate = 0.006f, drillReach = 2.6f, machineEc = 5f, heatOut = 140f, passiveCool = 50f,
                crashTolerance = 9f, color = Amber, accent = Dark,
                description = "Four times the small drill's output (21.6 t an hour at 10% ore), 5 EC/s. Makes 140 kW of heat: bring radiators." },
            new PartDef { id = "ore.r", name = "Ore Pod", kind = PartKind.OreTank, radial = true, radialSize = new Vector3(0.48f, 1.0f, 0.48f),
                stackTop = false, stackBottom = false, dryMass = 0.06f, oreCapacity = 0.4f, color = Hull, accent = new Color(0.55f, 0.36f, 0.2f),
                description = "Side-mounted ore hopper: 0.4 t. Launches empty." },
            new PartDef { id = "ore.s", name = "Ore Tank", kind = PartKind.OreTank, length = 1.5f, variants = 4,
                dryMass = 0.25f, oreCapacity = 1.5f, color = Hull, accent = new Color(0.55f, 0.36f, 0.2f),
                description = "1.3 m ore tank: 1.5 t. Launches empty; drills fill it." },
            new PartDef { id = "ore.l", name = "Ore Tank 2.6", kind = PartKind.OreTank, length = 2.0f, topDiameter = 2.6f, bottomDiameter = 2.6f, variants = 4,
                dryMass = 1.0f, oreCapacity = 8f, color = Hull, accent = new Color(0.55f, 0.36f, 0.2f),
                description = "2.6 m ore tank: 8 t." },
            new PartDef { id = "isru.s", name = "Ore Converter", kind = PartKind.Converter, length = 1.2f,
                dryMass = 1.2f, convertRate = 0.002f, machineEc = 6f, heatOut = 180f, passiveCool = 40f, maxTemp = 1600f, color = Mid, accent = Orange,
                description = "Refines ore into rocket fuel (0.8 t per t of ore) or monopropellant (0.6 t), 7.2 t of ore an hour, 6 EC/s. Makes 180 kW of heat: without radiators it slows down." },
            new PartDef { id = "rad.r", name = "Radiator Panel", kind = PartKind.Radiator, radial = true, radialSize = new Vector3(0.1f, 0.8f, 0.5f),
                stackTop = false, stackBottom = false, dryMass = 0.05f, maxQ = 2f, coolRate = 120f, color = new Color(0.9f, 0.9f, 0.88f), accent = Mid,
                description = "Unfolds (Y) to shed 120 kW from drills, converters and engines (a fifth of that folded). Tears off in thick air." },
            new PartDef { id = "rad.s", name = "Radiator Strip", kind = PartKind.Radiator, radial = true, radialSize = new Vector3(0.06f, 1.2f, 0.3f),
                stackTop = false, stackBottom = false, noDeploy = true, dryMass = 0.06f, coolRate = 40f, color = new Color(0.85f, 0.86f, 0.84f), accent = Dark,
                description = "Fixed radiator: sheds 40 kW, always working, survives reentry and launch. Cools engines too." },
        };

        /// <summary>An astronaut out of the hatch: walks, jumps, flies a small jetpack. Not in the builder.</summary>
        public static readonly PartDef EvaSuit = new PartDef
        {
            id = "eva.suit", name = "Astronaut", kind = PartKind.Eva, length = Ardlings.Height, topDiameter = 0.62f, bottomDiameter = 0.42f,
            stackTop = false, stackBottom = false, surfaceAttach = false, dryMass = 0.09f, mono = 0.006f, rcsThrust = 0.25f, torque = 0.15f,
            supplyCapacity = 0.5f, crew = 1, ecCapacity = 20f, crashTolerance = 9f, maxTemp = 900f,
            color = new Color(0.93f, 0.94f, 0.95f), accent = new Color(0.12f, 0.55f, 0.6f),
            description = "An astronaut on EVA, with a jetpack (6 kg of monopropellant).",
        };

        /// <summary>A flag planted by an astronaut. Not in the builder.</summary>
        public static readonly PartDef FlagPole = new PartDef
        {
            id = "flag.pole", name = "Flag", kind = PartKind.Flag, length = 2.4f, topDiameter = 0.07f, bottomDiameter = 0.07f,
            stackTop = false, stackBottom = false, surfaceAttach = false, dryMass = 0.01f, crashTolerance = 30f,
            color = new Color(0.12f, 0.55f, 0.6f), accent = new Color(0.95f, 0.48f, 0.14f),
        };

        /// <summary>One half of a jettisoned fairing (not buildable; used for the falling pieces).</summary>
        public static readonly PartDef FairingHalf = new PartDef
        {
            id = "__fairing.half", name = "Fairing half", kind = PartKind.FairingHalf, length = 4.6f, topDiameter = 1.56f, bottomDiameter = 1.56f,
            stackTop = false, stackBottom = false, surfaceAttach = false, dryMass = 0.125f, fairingHeight = 4.6f, fairingRadius = 0.78f, color = Hull,
        };

        /// <summary>Wanderers: small rocky bodies passing through, in three sizes. Not in the builder. A ball of radius length/2 under the top node.</summary>
        public static readonly PartDef[] Rocks =
        {
            new PartDef { id = "rock.a", name = "Wanderer (class A)", kind = PartKind.Rock, length = 3.2f, topDiameter = 3.2f, bottomDiameter = 3.2f,
                stackTop = false, stackBottom = false, surfaceAttach = false, dryMass = 18f, crashTolerance = 40f, maxTemp = 2400f, jointStrength = 4000f,
                color = new Color(0.47f, 0.43f, 0.39f), accent = new Color(0.33f, 0.30f, 0.28f), description = "A small rubble pile, 3 m across, 18 t." },
            new PartDef { id = "rock.b", name = "Wanderer (class B)", kind = PartKind.Rock, length = 4.8f, topDiameter = 4.8f, bottomDiameter = 4.8f,
                stackTop = false, stackBottom = false, surfaceAttach = false, dryMass = 60f, crashTolerance = 40f, maxTemp = 2400f, jointStrength = 6000f,
                color = new Color(0.52f, 0.46f, 0.40f), accent = new Color(0.36f, 0.31f, 0.27f), description = "A lump of rock and dust, 5 m across, 60 t." },
            new PartDef { id = "rock.c", name = "Wanderer (class C)", kind = PartKind.Rock, length = 7f, topDiameter = 7f, bottomDiameter = 7f,
                stackTop = false, stackBottom = false, surfaceAttach = false, dryMass = 170f, crashTolerance = 40f, maxTemp = 2400f, jointStrength = 9000f,
                color = new Color(0.42f, 0.40f, 0.38f), accent = new Color(0.30f, 0.29f, 0.28f), description = "A boulder 7 m across, 170 t: it takes a big tug to move." },
        };

        static Dictionary<string, PartDef> byId;

        public static PartDef Get(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, PartDef>();
                foreach (var d in All) byId[d.id] = d;
                byId[EvaSuit.id] = EvaSuit;
                byId[FlagPole.id] = FlagPole;
                foreach (var r in Rocks) byId[r.id] = r;
            }
            return id != null && byId.TryGetValue(id, out var def) ? def : null;
        }
    }
}
