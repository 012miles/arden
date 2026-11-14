using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    public enum SasMode { Off, Stability, Prograde, Retrograde, Normal, AntiNormal, RadialOut, RadialIn, Maneuver, Target, AntiTarget, DockAlign }
    public enum RailsEvent { None, Atmosphere, SoiChange, NearSurface }

    /// <summary>Runtime state of one part inside a flying vessel. Positions are in the vessel frame (design frame).</summary>
    public class PartState
    {
        public PartDef def;
        public int index;          // design index (stage actions refer to this)
        public int parent;         // design index of the parent, -1 for the root
        public Attach attach;
        public Vector3 localPos;
        public Quaternion localRot = Quaternion.identity;
        public int groups, variant;
        public double propellant, mono, ablator;
        public double temp = 288;
        public bool ignited, chuteDeployed, chuteFailed, legsDeployed, lightOn, towerFired, fairingGone, enclosed;
        public float chuteOpen, legDeploy, thrustFrac, towerTime, deflect, spool, steer;
        public float spin;             // rover wheels: how far the tyre has rolled round (deg), for the view
        public float charred;          // 0..1: soot and scorching from heat (milestone 23), kept
        public float overPressure;     // seconds over its pressure limit
        public float rollSpeed;        // rover wheels: speed along the tyre last contact (m/s)
        public bool touching;          // rover wheels: on the ground last contact
        public int section;
        public bool topCovered, bottomCovered;
        public List<string> crew = new List<string>();  // names of the people aboard this part
        public int dockedTo = -1;      // design index of the port this port is latched to
        public string dockedName;      // name of the vessel that docked here (restored on undocking)
        public bool dockedDebris;
        public VesselSim disarmedWith; // just undocked from this vessel: won't latch onto it again until 2 m apart
        public List<ScienceData> science = new List<ScienceData>(); // results this part is holding
        public double charge;          // stored electricity (EC)
        public double supplies;        // food, water and air (crew-days)
        public double ore;             // ore held (t)
        public int convMode;           // converters: 0 off, 1 making fuel, 2 making monopropellant
        public float load;             // drills/converters/scanners: how hard it worked last step (0..1), for the HUD and effects
        public bool used;              // single-use instrument (sample canister) that needs a scientist to clean it
        public bool broken;            // landing leg that gave way on a hard landing: an engineer on EVA can fix it
        public Vector3 thrustLocal;    // this step's engine force on the part (kN, vessel frame)
        public Vector3 aeroArea;       // this step's aero "area vector" on the part (times q in kPa = kN)

        public Vector3 Axis => localRot * Vector3.up;
        public Vector3 Center => localPos + localRot * def.CenterLocal;
        public Vector3 Bottom => localPos + localRot * new Vector3(0, -def.length, 0);
        public float RTop => def.radial ? def.radialSize.z * 0.5f : def.topDiameter * 0.5f;
        public float RBottom => def.radial ? def.radialSize.z * 0.5f : def.bottomDiameter * 0.5f;
        public float HeatFraction => (float)(temp / def.maxTemp);
        public double Mass => def.dryMass + propellant + mono + ore + supplies * Supplies.MassPerDay + (fairingGone ? 0 : def.fairingMass);
        /// <summary>Wheel hub in the vessel frame (landing gear), following the deploy animation.</summary>
        public Vector3 WheelHub => localPos + localRot * Vector3.Lerp(new Vector3(0.15f, 0, 0), new Vector3(def.strutLength, 0, 0), legDeploy);
    }

    /// <summary>Launch pad on Arden's equator at longitude 0: a raised square the rocket stands on.</summary>
    public static class LaunchSite
    {
        public const double PadHeight = 1.0, PadHalfSize = 12.0;
        // Runway: an east-west strip 325 m south of the pad, its middle 740 m west of it (milestone 24: captaincarl24's map).
        // Planes start near its west end, facing east.
        public const double RunwayNorth = -325, RunwayEast = -740, RunwayLength = 2400, RunwayWidth = 40, RunwayHeight = 0.15, RunwayStartEast = RunwayEast - 1080;
        public static readonly Vector3d Direction = CelestialBody.DirectionFromLatLon(0, 0);

        /// <summary>
        /// The launch pads (milestone 25, captaincarl24's map): the main pad, two beside the beach out east and one to the north-west,
        /// each with the hall door its rockets roll out from (metres east and north of the main pad).
        /// </summary>
        public static readonly (string name, double e, double n, double fromE, double fromN)[] Pads =
        {
            ("Main pad", 0, 0, 0, 297), ("Beach pad 1", 2192, -206, 1292, 150), ("Beach pad 2", 2037, -428, 1292, 150), ("North pad", -1770, 1020, -1374, 1124),
        };

        /// <summary>The two north-south runways beside the main one (milestone 25: solid ground now, not just paint).</summary>
        public static readonly (double e0, double n0, double e1, double n1)[] SideRunways = { (-480, -560, -440, 1100), (-700, -650, -660, 1040) };

        /// <summary>The harbour (milestone 25): the landing barge's deck (3 m up) and the pier (3.5 m up) are solid, so a booster can
        /// come down on the barge.</summary>
        public static readonly (double e0, double n0, double e1, double n1, double top) Barge = (3294, 145, 3386, 179, 3.0), Pier = (2682, 117, 3420, 133, 3.5);

        public static bool OnBarge(CelestialBody body, Vector3d fixedDir)
        {
            if (!body.IsHomeWorld) return false;
            Vector3d dir = fixedDir.normalized;
            Vector3d off = (dir - Direction * Vector3d.Dot(dir, Direction)) * body.radius;
            double e = Vector3d.Dot(off, CelestialBody.EastAt(Direction)), n = Vector3d.Dot(off, CelestialBody.NorthAt(Direction));
            return e >= Barge.e0 && e <= Barge.e1 && n >= Barge.n0 && n <= Barge.n1;
        }

        /// <summary>Which pad's deck this spot is on (milestone 26), or -1.</summary>
        public static int PadAt(CelestialBody body, Vector3d fixedDir)
        {
            if (!body.IsHomeWorld) return -1;
            Vector3d dir = fixedDir.normalized;
            Vector3d off = (dir - Direction * Vector3d.Dot(dir, Direction)) * body.radius;
            double e = Vector3d.Dot(off, CelestialBody.EastAt(Direction)), n = Vector3d.Dot(off, CelestialBody.NorthAt(Direction));
            for (int i = 0; i < Pads.Length; i++) if (Math.Abs(e - Pads[i].e) <= PadHalfSize + 2 && Math.Abs(n - Pads[i].n) <= PadHalfSize + 2) return i;
            return -1;
        }

        public static Vector3d PadDirection(CelestialBody body, int pad) => pad <= 0 || pad >= Pads.Length ? Direction : DirectionAt(body, Pads[pad].e, Pads[pad].n);

        /// <summary>Ground height above the reference radius: the pad and runway near the space center, real terrain elsewhere.</summary>
        public static double SurfaceHeight(CelestialBody body, Vector3d fixedDir)
        {
            if (!body.IsHomeWorld) return Terrain.Height(body, fixedDir);
            Vector3d dir = fixedDir.normalized;
            double cos = Vector3d.Dot(dir, Direction);
            if (cos > 0.9999)
            {
                Vector3d off = (dir - Direction * cos) * body.radius;
                Vector3d east = CelestialBody.EastAt(Direction), north = CelestialBody.NorthAt(Direction);
                double e = Vector3d.Dot(off, east), n = Vector3d.Dot(off, north);
                if (Math.Abs(e) <= PadHalfSize && Math.Abs(n) <= PadHalfSize) return PadHeight;
                for (int i = 1; i < Pads.Length; i++) if (Math.Abs(e - Pads[i].e) <= PadHalfSize && Math.Abs(n - Pads[i].n) <= PadHalfSize) return PadHeight;
                if (Math.Abs(n - RunwayNorth) <= RunwayWidth / 2 && Math.Abs(e - RunwayEast) <= RunwayLength / 2) return RunwayHeight;
                foreach (var r in SideRunways) if (e >= r.e0 && e <= r.e1 && n >= r.n0 && n <= r.n1) return RunwayHeight;
                if (e >= Barge.e0 && e <= Barge.e1 && n >= Barge.n0 && n <= Barge.n1) return Barge.top;
                if (e >= Pier.e0 && e <= Pier.e1 && n >= Pier.n0 && n <= Pier.n1) return Pier.top;
                return Terrain.Height(body, dir); // flat round the space center, rolling hills further out (milestone 23)
            }
            return Terrain.Height(body, dir);
        }

        /// <summary>Planet-fixed unit direction of a spot given east/north offsets (m) from the pad.</summary>
        public static Vector3d DirectionAt(CelestialBody body, double eastM, double northM)
        {
            Vector3d east = CelestialBody.EastAt(Direction), north = CelestialBody.NorthAt(Direction);
            return (Direction + east * (eastM / body.radius) + north * (northM / body.radius)).normalized;
        }
    }

    /// <summary>Reentry heating constants (tuned by the reentry tests).</summary>
    public static class Thermal
    {
        // flux = HeatK * sqrt(rho) * (v^3 - V0^3), W/m^2. Tuned with SimTests' heat sweep: a bare capsule survives
        // coming back from low orbit, burns up coming back from Tal, and a heat shield gets it home from Tal.
        public static double HeatK = 0.0018;
        public const double V0 = 650;
        public const double SkinFraction = 0.08, MinSkinMass = 0.05, Cp = 900;
        public const double Emissivity = 0.85, Sigma = 5.670e-8, SpaceTemp = 250;
        public const double AblateStart = 700, AblateRate = 0.0012, AblateEnergy = 5e6;
    }

    /// <summary>
    /// Flight physics for one vessel: a rigid tree of parts. Position and velocity are relative to the body whose
    /// SOI it is in. Thrust, drag and heating are summed part by part in 3D, so side boosters, lopsided rockets,
    /// fins and parachutes all make their own forces and torques. Units: tonnes, kN, metres, seconds.
    /// </summary>
    public partial class VesselSim
    {
        public const double G0 = 9.80665;
        const float MaxManualRate = 0.9f, RateGain = 6f;
        const double RcsIsp = 240;

        public CelestialBody body;
        public string name;
        public bool isDebris;
        public List<PartState> parts = new List<PartState>();
        public List<StageAction> stages;
        public int nextStage;

        public Vector3d pos, vel;
        public Quaternion rot = Quaternion.identity;
        public Vector3 angVel;
        public Vector3d prevPos;
        public Quaternion prevRot = Quaternion.identity;

        public double throttle = 1.0;
        public float inputPitch, inputYaw, inputRoll;
        public float driveInput;                    // rover wheels: -1 (reverse) .. 1 (forward)
        public double driven;                       // metres driven on wheels on the body named drivenOn
        public string drivenOn = "";
        bool rolling;                               // on the wheels this step (ground handling)
        double motorRate;                           // EC/s the wheel motors are drawing
        public Vector3 inputTranslate;              // RCS: x = right, y = forward (nose), z = up (top side)
        public SasMode sas = SasMode.Stability;
        public bool rcsOn, gearDown, lightsOn, brakesOn;
        public Vector3d? steerDirection, maneuverDir, targetDir;

        public bool landed, destroyed, hasLaunched, disableRails;
        public string destroyReason = "";
        public string lastEvent;
        public Orbit rails;
        Vector3d landedFixedPos;
        Quaternion landedFixedRot;
        Quaternion sasHold;
        bool sasHoldValid;
        float sasStrain, sasCalm;   // seconds SAS has been losing its hold to the air / seconds the ship has been still
        Vector3 lastAeroTau;        // last step's aerodynamic torque (vessel frame, kN*m)

        public double mass;
        public Vector3 com;        // centre of mass in the vessel frame
        public Vector3 inertia;    // t*m^2 about local x (pitch), y (roll), z (yaw)
        /// <summary>A vessel this one just came apart from (staging, undocking, EVA): they don't collide until they're clear.</summary>
        public VesselSim ghostWith;
        float reactionTorque, rcsThrustTotal;
        Vector3 rcsTorqueCap;
        Vector3d axisCache;
        Quaternion rotCache;
        Vector3 omegaCache;
        Topology topo;
        readonly Dictionary<int, double> demandScratch = new Dictionary<int, double>();

        public double thrustNow, dynPressure, gForce, heatFlux;
        public int leadingPart = -1;
        public Vector3 rcsFiring;  // last RCS translation actually applied (for effects)

        public double Altitude => pos.magnitude - body.radius;
        public Vector3d Axis => Vector3d.Rotate(rot, Vector3d.up);
        public bool HasControl => commandable;
        public Vector3d SurfaceRelVelocity => vel - body.SurfaceVelocity(pos);
        /// <summary>Milestone 27: the wind where the vessel is (inertial, m/s), set each step; zero with the weather off.</summary>
        public Vector3d windInertial;
        /// <summary>Velocity through the air: over the ground, less the wind.</summary>
        public Vector3d AirRelVelocity => SurfaceRelVelocity - windInertial;
        public Vector3d AbsolutePosition(double ut) => body.AbsolutePosition(ut) + pos;
        public Vector3d AbsoluteVelocity(double ut) => body.AbsoluteVelocity(ut) + vel;
        public double MonoLeft { get { double m = 0; foreach (var p in parts) m += p.mono; return m; } }

        public float Length
        {
            get
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var p in parts) { lo = Mathf.Min(lo, p.Bottom.y, p.Center.y - p.def.Height * 0.5f); hi = Mathf.Max(hi, p.localPos.y, p.Center.y + p.def.Height * 0.5f); }
                return parts.Count > 0 ? hi - lo : 1f;
            }
        }

        public PartState Hottest
        {
            get
            {
                PartState h = null;
                foreach (var p in parts) if (h == null || p.HeatFraction > h.HeatFraction) h = p;
                return h;
            }
        }

        PartState Find(int designIndex)
        {
            foreach (var p in parts) if (p.index == designIndex) return p;
            return null;
        }

        // ---------------------------------------------------------------- creation

        static VesselSim Build(VesselDesign design, CelestialBody body)
        {
            design.Upgrade();
            var defs = design.Defs();
            var v = new VesselSim { body = body, name = design.name };
            for (int i = 0; i < defs.Count; i++)
            {
                var n = design.nodes[i];
                v.parts.Add(new PartState
                {
                    def = defs[i], index = i, parent = n.parent, attach = n.attach, localPos = n.pos, localRot = n.rot,
                    groups = n.groups, variant = n.variant, propellant = defs[i].propellant, mono = defs[i].mono, ablator = defs[i].ablator,
                });
                for (int k = 0; k < defs[i].crew; k++) v.parts[i].crew.Add(CrewNames.Next());
                v.parts[i].charge = defs[i].ecCapacity;
                v.parts[i].supplies = defs[i].supplyCapacity;
            }
            v.stages = design.Stages();
            v.RebuildTopology();
            v.RecomputeMass();
            return v;
        }

        public static VesselSim CreateOnPad(VesselDesign design, CelestialBody home, double ut, int pad = 0)
        {
            var v = Build(design, home);
            Vector3d up = home.IsHomeWorld ? LaunchSite.PadDirection(home, pad) : LaunchSite.Direction;
            Vector3d west = -CelestialBody.EastAt(up);
            float lowest = float.MaxValue;
            foreach (var p in v.parts) foreach (var c in v.ContactPoints(p)) lowest = Mathf.Min(lowest, c.y);
            v.landedFixedPos = up * (home.radius + LaunchSite.PadHeight + (v.com.y - lowest));
            // Top side (+Z, screen-up on the navball) faces west, so W ("nose down") tips the rocket east.
            v.landedFixedRot = Quaternion.LookRotation(west.ToFloat(), up.ToFloat());
            v.landed = true;
            v.SyncLanded(ut);
            v.prevPos = v.pos;
            v.prevRot = v.rot;
            return v;
        }

        /// <summary>A plane at the west end of the runway, facing east, gear down and brakes on.</summary>
        public static VesselSim CreateOnRunway(VesselDesign design, CelestialBody home, double ut)
        {
            var v = Build(design, home);
            foreach (var p in v.parts) if (p.def.kind == PartKind.Gear || p.def.kind == PartKind.Leg) { p.legsDeployed = true; p.legDeploy = 1f; }
            v.gearDown = true;
            v.brakesOn = true;
            Vector3d up = LaunchSite.DirectionAt(home, LaunchSite.RunwayStartEast, LaunchSite.RunwayNorth);
            Vector3d east = CelestialBody.EastAt(up);
            if (design.IsRover)
            {
                // A rover stands upright on its wheels, front (+Z) facing east down the runway, engines (if any) at zero.
                v.throttle = 0;
                float low = float.MaxValue;
                foreach (var p in v.parts) foreach (var c in v.ContactPoints(p)) low = Mathf.Min(low, c.y);
                v.landedFixedPos = up * (home.radius + LaunchSite.RunwayHeight + (v.com.y - low) + 0.02);
                v.landedFixedRot = Quaternion.LookRotation(east.ToFloat(), up.ToFloat());
                v.landed = true;
                v.SyncLanded(ut);
                v.prevPos = v.pos;
                v.prevRot = v.rot;
                return v;
            }
            float lowest = float.MaxValue; // along local -Z (the belly faces the ground)
            foreach (var p in v.parts) foreach (var c in v.ContactPoints(p)) lowest = Mathf.Min(lowest, c.z);
            v.landedFixedPos = up * (home.radius + LaunchSite.RunwayHeight + (v.com.z - lowest) + 0.02);
            // nose (+Y) east, top (+Z) up, right wing (+X) south
            v.landedFixedRot = Quaternion.LookRotation(up.ToFloat(), east.ToFloat());
            v.landed = true;
            v.SyncLanded(ut);
            v.prevPos = v.pos;
            v.prevRot = v.rot;
            return v;
        }

        /// <summary>Standing upright on a body's surface at a planet-fixed direction, legs down (mining demo, tests).</summary>
        public static VesselSim CreateLanded(VesselDesign design, CelestialBody body, Vector3d fixedDir, double ut)
        {
            var v = Build(design, body);
            foreach (var p in v.parts) if (p.def.kind == PartKind.Leg || p.def.kind == PartKind.Gear) { p.legsDeployed = true; p.legDeploy = 1f; }
            v.gearDown = true;
            if (design.IsRover) v.throttle = 0;
            Vector3d up = fixedDir.normalized;
            float lowest = float.MaxValue;
            foreach (var p in v.parts) foreach (var c in v.ContactPoints(p)) lowest = Mathf.Min(lowest, c.y);
            double ground = body.radius + LaunchSite.SurfaceHeight(body, up);
            v.landedFixedPos = up * (ground + (v.com.y - lowest));
            v.landedFixedRot = Quaternion.LookRotation((-CelestialBody.EastAt(up)).ToFloat(), up.ToFloat());
            v.landed = true;
            v.hasLaunched = true;
            v.SyncLanded(ut);
            v.prevPos = v.pos;
            v.prevRot = v.rot;
            return v;
        }

        public bool HasWheels { get { foreach (var p in parts) if (p.def.kind == PartKind.Gear || p.def.kind == PartKind.Wheel) return true; return false; } }
        public bool HasDriveWheels { get { foreach (var p in parts) if (p.def.kind == PartKind.Wheel && !p.broken) return true; return false; } }
        public bool OnWheels => rolling;

        public static VesselSim CreateFree(VesselDesign design, CelestialBody body, Vector3d pos, Vector3d vel, Quaternion rot)
        {
            var v = Build(design, body);
            v.pos = pos; v.vel = vel; v.rot = rot; v.hasLaunched = true;
            v.prevPos = pos; v.prevRot = rot;
            return v;
        }

        // ---------------------------------------------------------------- topology and mass properties

        void IndexArrays(out PartDef[] defs, out int[] parent, out Attach[] attach, out bool[] present)
        {
            int n = 0;
            foreach (var p in parts) n = Math.Max(n, p.index + 1);
            defs = new PartDef[n]; parent = new int[n]; attach = new Attach[n]; present = new bool[n];
            for (int i = 0; i < n; i++) parent[i] = -1;
            foreach (var p in parts) { defs[p.index] = p.def; parent[p.index] = p.parent; attach[p.index] = p.attach; present[p.index] = true; }
        }

        public void RebuildTopology()
        {
            IndexArrays(out var defs, out var parent, out var attach, out var present);
            topo = Topology.Of(defs, parent, attach, present);
            foreach (var p in parts)
            {
                p.section = topo.section[p.index];
                p.topCovered = topo.topCovered[p.index];
                p.bottomCovered = topo.bottomCovered[p.index];
                p.enclosed = false;
            }
            // Parts inside an intact fairing shell are shielded from the air.
            foreach (var f in parts)
            {
                if (f.def.kind != PartKind.Fairing || f.fairingGone) continue;
                Quaternion inv = Quaternion.Inverse(f.localRot);
                foreach (var p in parts)
                {
                    if (p == f) continue;
                    Vector3 l = inv * (p.Center - f.localPos);
                    if (l.y > 0 && l.y < f.def.fairingHeight && new Vector2(l.x, l.z).magnitude < f.def.fairingRadius + 0.05f) p.enclosed = true;
                }
            }
        }

        public void RecomputeMass()
        {
            double m = 0;
            Vector3 sum = Vector3.zero;
            float rw = 0;
            foreach (var p in parts)
            {
                double pm = p.Mass;
                m += pm;
                sum += p.Center * (float)pm;
            }
            mass = Math.Max(m, 0.01);
            com = parts.Count > 0 ? sum / (float)mass : Vector3.zero;
            Vector3 I = Vector3.zero;
            rcsThrustTotal = 0;
            rcsTorqueCap = Vector3.zero;
            foreach (var p in parts)
            {
                float pm = (float)p.Mass;
                Vector3 r = p.Center - com;
                float rad = Mathf.Max(0.05f, (p.RTop + p.RBottom) * 0.5f), L = p.def.Height;
                float own = pm * (3 * rad * rad + L * L) / 12f;
                I += new Vector3(pm * (r.y * r.y + r.z * r.z), pm * (r.x * r.x + r.z * r.z), pm * (r.x * r.x + r.y * r.y)) + Vector3.one * own;
                if (p.def.rcsThrust > 0)
                {
                    rcsThrustTotal += p.def.rcsThrust;
                    rcsTorqueCap += p.def.rcsThrust * new Vector3(new Vector2(r.y, r.z).magnitude, new Vector2(r.x, r.z).magnitude, new Vector2(r.x, r.y).magnitude);
                }
            }
            inertia = new Vector3(Mathf.Max(I.x, 0.05f), Mathf.Max(I.y, 0.02f), Mathf.Max(I.z, 0.05f));
            // Command: a crewed cockpit or capsule needs someone aboard, a probe core needs power; reaction wheels need power.
            double ec = 0;
            foreach (var p in parts) ec += p.charge;
            bool hasEc = ec > 1e-3;
            commandable = false;
            foreach (var p in parts)
            {
                if (p.def.kind != PartKind.Command && p.def.kind != PartKind.Eva) continue;
                bool ok = p.def.crew > 0 ? p.crew.Count > 0 : hasEc;
                if (!ok) continue;
                commandable = true;
                if (hasEc) rw += p.def.torque;
            }
            commandable &= !isDebris;
            reactionTorque = isDebris ? 0 : rw;
        }

        /// <summary>Points that can touch the ground, in the vessel frame.</summary>
        public IEnumerable<Vector3> ContactPoints(PartState p)
        {
            var d = p.def;
            if (d.kind == PartKind.Gear)
            {
                yield return p.WheelHub + p.localRot * new Vector3(d.wheelRadius, 0, 0);
                yield break;
            }
            if (d.kind == PartKind.Wheel)
            {
                // the tyre's rim, all round the axle
                Vector3 hub = DriveHub(p);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    yield return hub + p.localRot * new Vector3(0, Mathf.Cos(a) * d.wheelRadius, Mathf.Sin(a) * d.wheelRadius);
                }
                yield break;
            }
            if (d.kind == PartKind.Leg)
            {
                float k = p.legDeploy;
                yield return p.localPos + p.localRot * Vector3.Lerp(new Vector3(d.radialSize.x, -d.radialSize.y * 0.5f, 0),
                    new Vector3(d.legLength * 0.5f, -d.legLength, 0), k);
                yield break;
            }
            if (d.radial)
            {
                Vector3 s = d.radialSize;
                for (int i = 0; i < 8; i++)
                    yield return p.localPos + p.localRot * new Vector3((i & 1) * s.x, ((i >> 1) & 1) == 0 ? -s.y * 0.5f : s.y * 0.5f, ((i >> 2) & 1) == 0 ? -s.z * 0.5f : s.z * 0.5f);
                yield break;
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                yield return p.localPos + p.localRot * new Vector3(Mathf.Cos(a) * p.RTop, 0, Mathf.Sin(a) * p.RTop);
                yield return p.localPos + p.localRot * new Vector3(Mathf.Cos(a) * p.RBottom, -d.length, Mathf.Sin(a) * p.RBottom);
            }
        }

        // ---------------------------------------------------------------- engines and fuel

        double SectionLiquid(int section)
        {
            double s = 0;
            foreach (var p in parts) if (p.section == section && p.def.kind == PartKind.Tank) s += p.propellant;
            return s;
        }

        bool EngineHasFuel(PartState p) => p.def.IsSolid ? p.propellant > 1e-9 : SectionLiquid(p.section) > 1e-9;

        /// <summary>Total thrust force and torque about the CoM (vessel frame), plus gimbal authority.</summary>
        void EngineForces(double pressure, out Vector3 force, out Vector3 torque, out float gimbal, out double total)
        {
            force = torque = Vector3.zero;
            gimbal = 0;
            total = 0;
            foreach (var p in parts)
            {
                p.thrustFrac = 0;
                p.thrustLocal = Vector3.zero;
                if (p.def.kind == PartKind.EscapeTower && p.towerFired && p.towerTime < p.def.lesBurn)
                {
                    Vector3 fl = p.Axis * p.def.lesThrust;
                    force += fl; torque += Vector3.Cross(p.Bottom - com, fl); total += p.def.lesThrust;
                    p.thrustFrac = 1;
                    p.thrustLocal = fl;
                    continue;
                }
                if (!p.ignited || !p.def.IsEngine || !EngineHasFuel(p)) continue;
                double thr = p.def.IsSolid ? 1.0 : throttle;
                if (p.def.IsJet) thr = p.spool * JetFactor(p.def);
                if (thr <= 0) continue;
                double isp = p.def.ispVac + (p.def.ispAsl - p.def.ispVac) * Math.Min(1, pressure);
                double f = p.def.MaxMassFlow * thr * G0 * isp;
                Vector3 fv = p.Axis * (float)f;
                p.thrustLocal = fv;
                force += fv;
                torque += Vector3.Cross(p.Bottom - com, fv);
                total += f;
                p.thrustFrac = (float)thr;
                gimbal += (float)(f * Math.Sin(p.def.gimbalDeg * Mathf.Deg2Rad)) * (p.Bottom - com).magnitude;
            }
        }

        /// <summary>Jet thrust multiplier: needs air (falls with density), rises with speed, dies near its top speed.</summary>
        public double JetFactor(PartDef d)
        {
            double rho = body.Density(Altitude);
            if (rho <= 0) return 0;
            double x = Math.Min(1, SurfaceRelVelocity.magnitude / d.jetMaxSpeed);
            return Math.Pow(rho / body.seaLevelDensity, 0.8) * (1 + 1.2 * x) * (1 - x * x * x * x);
        }

        void SpoolJets(double dt)
        {
            foreach (var p in parts)
                if (p.def.IsJet) p.spool = Mathf.MoveTowards(p.spool, p.ignited ? (float)throttle : 0f, (float)dt * 0.5f);
        }

        public double PotentialThrust()
        {
            double t = 0;
            foreach (var p in parts) if (p.ignited && p.def.IsEngine && EngineHasFuel(p)) t += p.def.thrustVac;
            if (t > 0 || stages == null) return t;
            for (int s = nextStage; s < stages.Count; s++)
            {
                foreach (int i in stages[s].ignite) { var p = Find(i); if (p != null && EngineHasFuel(p)) t += p.def.thrustVac; }
                if (t > 0) break;
            }
            return t;
        }

        /// <summary>Full-throttle thrust (kN, at the current air pressure) of lit engines, split into solids and liquids.</summary>
        public void EngineThrustSplit(out double solid, out double liquid)
        {
            solid = liquid = 0;
            double pressure = body.PressureAtm(Altitude);
            foreach (var p in parts)
            {
                if (!p.ignited || !p.def.IsEngine || !EngineHasFuel(p)) continue;
                double f = p.def.MaxMassFlow * G0 * (p.def.ispVac + (p.def.ispAsl - p.def.ispVac) * Math.Min(1, pressure));
                if (p.def.IsSolid) solid += f; else liquid += f;
            }
        }

        public bool NextStageHasEngines()
        {
            if (stages == null || nextStage >= stages.Count) return false;
            foreach (int i in stages[nextStage].ignite) if (Find(i) != null) return true;
            return false;
        }

        /// <summary>True if the next stage only jettisons an escape tower that's still attached.</summary>
        public bool NextStageIsTowerJettison()
        {
            if (stages == null || nextStage >= stages.Count) return false;
            var st = stages[nextStage];
            return st.jettison.Count > 0 && st.decouple.Count == 0 && st.ignite.Count == 0 && st.jettison.Exists(i => Find(i) != null);
        }

        /// <summary>True if the next stage only drops parts whose engines are all burned out (spent side boosters).</summary>
        public bool NextStageDropsSpentBoosters()
        {
            if (stages == null || nextStage >= stages.Count) return false;
            var st = stages[nextStage];
            if (st.decouple.Count == 0 || st.ignite.Count > 0) return false;
            bool anyEngine = false;
            foreach (int d in st.decouple)
            {
                var dp = Find(d);
                if (dp == null || topo == null) continue;
                foreach (int k in topo.Subtree(d))
                {
                    var p = Find(k);
                    if (p == null || !p.def.IsEngine) continue;
                    anyEngine = true;
                    if (EngineHasFuel(p)) return false;
                }
            }
            return anyEngine;
        }

        public bool EnginesWouldBurn()
        {
            foreach (var p in parts)
            {
                if (p.def.kind == PartKind.EscapeTower && p.towerFired && p.towerTime < p.def.lesBurn) return true;
                if (p.ignited && p.def.IsEngine && (p.def.IsSolid || throttle > 0) && EngineHasFuel(p)) return true;
            }
            return false;
        }

        void ConsumePropellant(double dt)
        {
            demandScratch.Clear();
            foreach (var p in parts)
            {
                if (p.def.kind == PartKind.EscapeTower) { if (p.towerFired) p.towerTime += (float)dt; continue; }
                if (p.thrustFrac <= 0) continue;
                double flow = p.def.MaxMassFlow * p.thrustFrac * dt;
                if (p.def.IsSolid) p.propellant = Math.Max(0, p.propellant - flow);
                else { demandScratch.TryGetValue(p.section, out double d); demandScratch[p.section] = d + flow; }
            }
            foreach (var kv in demandScratch)
            {
                double pool = SectionLiquid(kv.Key);
                if (pool <= 0) continue;
                double frac = Math.Min(1, kv.Value / pool);
                foreach (var p in parts)
                    if (p.section == kv.Key && p.def.kind == PartKind.Tank) p.propellant = frac >= 1 ? 0 : p.propellant * (1 - frac);
            }
        }

        void UseMono(double tonnes)
        {
            double have = MonoLeft;
            if (have <= 0) return;
            double frac = Math.Min(1, tonnes / have);
            foreach (var p in parts) p.mono *= 1 - frac;
        }

        // ---------------------------------------------------------------- stepping

        public void Step(double dt, double ut)
        {
            if (destroyed) return;
            RecomputeMass();
            rotCache = rot;
            axisCache = Axis;
            omegaCache = Quaternion.Inverse(rot) * angVel;
            windInertial = landed ? Vector3d.zero : Weather.WindInertial(body, pos, ut);
            UpdateLegsAndLights(dt);
            SpoolJets(dt);
            UpdateSystems(dt, ut);
            double alt = Altitude;
            if (body.isStar && alt < SunBurnAltitude) { Destroy($"Flew too close to {body.name} and burned up"); return; }
            if (body.IsGasGiant && alt < GasGiantFloor) { Destroy($"Sank into {body.name}'s clouds: there's no surface down there"); return; }
            EngineForces(body.PressureAtm(alt), out Vector3 fLocal, out Vector3 thrustTorque, out float gimbal, out double thrust);

            // RCS translation
            rcsFiring = Vector3.zero;
            Vector3 rcsForce = Vector3.zero;
            if (rcsOn && rcsThrustTotal > 0 && MonoLeft > 0 && inputTranslate.sqrMagnitude > 0)
            {
                rcsFiring = Vector3.ClampMagnitude(inputTranslate, 1f);
                rcsForce = new Vector3(rcsFiring.x, rcsFiring.y, rcsFiring.z) * rcsThrustTotal;
                UseMono(rcsForce.magnitude / (RcsIsp * G0) * dt);
                fLocal += rcsForce;
            }
            thrustNow = thrust;

            rolling = false;
            motorRate = 0;
            if (landed)
            {
                if (IsEva) StepEvaGround(dt, ut, fLocal);
                else StepLanded(dt, ut, fLocal);
                if (!destroyed) UpdateDamage(dt, 0);   // chain reactions carry on on the ground too
                return;
            }

            // Rolling on wheels: spring-damper contact physics instead of the "landed" lock.
            if (WheelsDown && alt < Terrain.MaxHeight(body) + 1500 && RadarAltitude(ut) < 4)
            {
                StepGround(dt, ut, fLocal, thrustTorque, gimbal);
                if (!destroyed) UpdateDamage(dt, dynPressure / 1000.0);
                return;
            }
            if (!hasLaunched && RadarAltitude(ut) > 3) hasLaunched = true;

            Vector3d v0 = vel;
            // On rails only with some sideways motion: a straight up-or-down fall makes a degenerate orbit the exact maths can't follow.
            bool coast = !disableRails && fLocal.sqrMagnitude <= 0 && alt > body.atmosphereHeight && Vector3d.Cross(pos, vel).magnitude > pos.magnitude * 1.0;
            if (coast)
            {
                if (rails == null) rails = Orbit.FromState(pos, vel, body.mu, ut);
                rails.StateAt(ut + dt, out pos, out vel);
                dynPressure = 0;
                heatFlux = 0;
                leadingPart = -1;
                CoolInSpace(dt);
                UpdateDamage(dt, 0);
                if (destroyed) return;
            }
            else
            {
                rails = null;
                Vector3d thrustAcc = Vector3d.Rotate(rot, Vector3d.FromFloat(fLocal)) / mass;
                IntegrateRK4(dt, thrustAcc);
                ConsumePropellant(dt);
                double s = SurfaceRelVelocity.magnitude;
                dynPressure = 0.5 * body.Density(Altitude) * s * s;
                UpdateHeat(dt);
                if (destroyed) return;
                UpdateDamage(dt, dynPressure / 1000.0);
                if (destroyed) return;
            }
            gForce = ((vel - v0) / dt - body.Gravity(pos)).magnitude / G0;

            Vector3 wOld = Quaternion.Inverse(rot) * angVel;
            IntegrateRotation(dt, gimbal, thrustTorque);
            if (!coast) CheckStructure(dt, wOld);
            UpdateChutes(dt);
            CheckGround(ut + dt);
            if (!destroyed && !landed) CheckSoi(ut + dt);
        }

        void UpdateLegsAndLights(double dt)
        {
            foreach (var p in parts)
            {
                if (p.def.kind == PartKind.Leg || p.def.kind == PartKind.Gear || p.def.IsDeployable)
                    p.legDeploy = Mathf.MoveTowards(p.legDeploy, p.legsDeployed && !p.broken ? 1f : 0f, (float)dt * (p.def.IsDeployable ? 0.4f : 0.8f));
            }
        }

        public bool WheelsDown { get { foreach (var p in parts) if (IsRolling(p)) return true; return false; } }
        static bool IsRolling(PartState p) => (p.def.kind == PartKind.Gear && p.legDeploy > 0.9f) || (p.def.kind == PartKind.Wheel && !p.broken);
        /// <summary>Rover wheel hub in the vessel frame: just outside the part it is mounted on, axle pointing out.</summary>
        static Vector3 DriveHub(PartState p) => p.localPos + p.localRot * new Vector3(p.def.strutLength, 0, 0);

        /// <summary>
        /// Ground handling for wheeled vessels: wheels are spring-dampers with rolling friction, brakes and nose-wheel
        /// steering; any other part touching down is a stiff contact that breaks if it hits too fast. Runs at 250 Hz.
        /// </summary>
        void StepGround(double dt, double ut, Vector3 fLocal, Vector3 thrustTorque, float gimbal)
        {
            rails = null;
            rolling = true;
            const int n = 5;
            double h = dt / n, ecUse = 0;
            for (int k = 0; k < n && !destroyed; k++)
            {
                double t = ut + h * k;
                ContactForces(t, out Vector3d fContact, out Vector3 tContact);
                if (destroyed) return;
                ecUse += motorRate * h;
                Vector3d vrel = SurfaceRelVelocity;
                double s = vrel.magnitude, rho = body.Density(Altitude);
                Vector3d aero = Vector3d.zero;
                if (rho > 0 && s > 0.05)
                {
                    Vector3 u = Quaternion.Inverse(rot) * (vrel / s).ToFloat();
                    Vector3 cda = AeroCdA(u, (float)s, Quaternion.Inverse(rot) * angVel, out _);
                    aero = Vector3d.Rotate(rot, Vector3d.FromFloat(cda)) * (0.5 * rho * s * s / 1000.0);
                }
                dynPressure = 0.5 * rho * s * s;
                Vector3d force = Vector3d.Rotate(rot, Vector3d.FromFloat(fLocal)) + aero + fContact; // kN
                vel = vel + (body.Gravity(pos) + force / mass) * h;
                pos = pos + vel * h;
                IntegrateRotation(h, gimbal, thrustTorque + tContact);
            }
            ConsumePropellant(dt);
            if (ecUse > 0) AddCharge(-ecUse);
            // Rover bookkeeping: tyres roll round for the view, and distance driven counts while any wheel is down.
            if (HasDriveWheels)
            {
                Vector3d up = pos.normalized, sv = SurfaceRelVelocity;
                double ground = (sv - up * Vector3d.Dot(sv, up)).magnitude;
                bool down = false;
                foreach (var p in parts)
                {
                    if (p.def.kind != PartKind.Wheel) continue;
                    p.spin = (p.spin + (float)(p.rollSpeed / p.def.wheelRadius * dt * Mathf.Rad2Deg)) % 360f;
                    down |= p.touching;
                }
                if (down && ground > 0.05)
                {
                    if (drivenOn != body.name) { drivenOn = body.name; driven = 0; }
                    driven += ground * dt;
                }
            }
            gForce = 1;
            heatFlux = 0;
            leadingPart = -1;
            UpdateChutes(dt);
            // Park: with the brakes on, no thrust and nearly stopped, lock to the ground so time warp works.
            Vector3 relSpin = angVel - body.AngularVelocityVector.ToFloat();
            if (brakesOn && fLocal.sqrMagnitude == 0 && driveInput == 0 && SurfaceRelVelocity.magnitude < 0.15 && relSpin.magnitude < 0.02f)
            {
                landedFixedPos = body.InertialToFixed(pos, ut + dt);
                landedFixedRot = Quaternion.Inverse(body.RotationAt(ut + dt)) * rot;
                landed = true;
                SyncLanded(ut + dt);
            }
        }

        /// <summary>
        /// Which way a rover drives: the vessel's nose (+Y) when it lies flat like a plane, otherwise its +Z side (a rover
        /// standing upright on its frame, probe core on top). Whichever is more level wins. World frame, on the ground plane.
        /// </summary>
        Vector3d DriveForward(Vector3d up)
        {
            Vector3d y = Vector3d.Rotate(rot, new Vector3d(0, 1, 0)), z = Vector3d.Rotate(rot, new Vector3d(0, 0, 1));
            Vector3d yh = y - up * Vector3d.Dot(y, up), zh = z - up * Vector3d.Dot(z, up);
            Vector3d d = yh.sqrMagnitude >= zh.sqrMagnitude ? yh : zh;
            return d.sqrMagnitude > 1e-6 ? d.normalized : CelestialBody.EastAt(up);
        }

        /// <summary>The ground's real slope under a point: the terrain normal from heights 0.5 m either side.</summary>
        Vector3d GroundNormal(Vector3d upInertial, double ut)
        {
            Vector3d f = body.InertialToFixed(upInertial, ut);
            Vector3d e = CelestialBody.EastAt(f), nn = CelestialBody.NorthAt(f);
            double d = 0.5, a = d / body.radius;
            double he = LaunchSite.SurfaceHeight(body, (f + e * a).normalized) - LaunchSite.SurfaceHeight(body, (f - e * a).normalized);
            double hn = LaunchSite.SurfaceHeight(body, (f + nn * a).normalized) - LaunchSite.SurfaceHeight(body, (f - nn * a).normalized);
            Vector3d nf = (f - e * (he / (2 * d)) - nn * (hn / (2 * d))).normalized;
            return body.FixedToInertial(nf, ut);
        }

        void ContactForces(double ut, out Vector3d fWorld, out Vector3 tLocal)
        {
            fWorld = Vector3d.zero;
            tLocal = Vector3.zero;
            motorRate = 0;
            Quaternion inv = Quaternion.Inverse(rot);
            int wheels = 0;
            foreach (var p in parts) if (IsRolling(p)) wheels++;
            double g = body.SurfaceGravity;
            double wheelK = mass * g / Math.Max(1, wheels) / 0.05, wheelC = 2 * 0.8 * Math.Sqrt(wheelK * mass / Math.Max(1, wheels));
            double rigidK = mass * g / 0.03, rigidC = 2 * 0.8 * Math.Sqrt(rigidK * mass);
            double speed = SurfaceRelVelocity.magnitude;
            float steerMax = 25f * (1f - Mathf.Min((float)speed / 60f, 0.85f));
            float roverSteer = 30f / (1f + (float)speed / 2f); // rovers: sharp at a crawl, gentle at speed
            Vector3d up = pos.normalized, driveFwd = HasDriveWheels ? DriveForward(up) : up;
            bool powered = EcStored > 0.05;
            foreach (var p in parts)
            {
                if (p.def.kind == PartKind.Gear && p.legDeploy > 0.9f)
                {
                    p.steer = p.WheelHub.y > com.y ? -inputYaw * steerMax : 0f;
                    Contact(p, p.WheelHub, p.def.wheelRadius, 1, ut, ref fWorld, ref tLocal, inv, wheelK, wheelC, driveFwd, 0, false);
                }
                else if (p.def.kind == PartKind.Wheel && !p.broken)
                {
                    Vector3 hub = DriveHub(p);
                    // front wheels (ahead of the centre of mass) steer one way, back wheels the other
                    double ahead = Vector3d.Dot(Vector3d.Rotate(rot, Vector3d.FromFloat(hub - com)), driveFwd);
                    p.steer = (float)(inputYaw * roverSteer * Math.Sign(ahead));
                    Contact(p, hub, p.def.wheelRadius, 2, ut, ref fWorld, ref tLocal, inv, wheelK, wheelC, driveFwd, roverSteer, powered);
                }
                else
                {
                    foreach (var c in ContactPoints(p))
                    {
                        Contact(p, c, 0, 0, ut, ref fWorld, ref tLocal, inv, rigidK, rigidC, driveFwd, 0, false);
                        if (destroyed) return;
                    }
                }
                if (destroyed) return;
            }
        }

        /// <summary>
        /// One contact with the ground. kind 0 = a rigid point, 1 = a landing-gear wheel (rolls along the nose),
        /// 2 = a rover wheel (rolls across its axle, pushes with its motor, follows the real slope).
        /// </summary>
        void Contact(PartState p, Vector3 local, double radius, int kind, double ut, ref Vector3d fWorld, ref Vector3 tLocal, Quaternion inv,
            double k, double c, Vector3d driveFwd, float steerMax, bool powered)
        {
            bool wheel = kind > 0;
            if (kind == 2) { p.touching = false; p.rollSpeed = 0; }
            Vector3d rW = Vector3d.Rotate(rot, Vector3d.FromFloat(local - com));
            Vector3d P = pos + rW;
            Vector3d radial = P.normalized;
            double ground = body.radius + LaunchSite.SurfaceHeight(body, body.InertialToFixed(radial, ut));
            double pen = radius - (P.magnitude - ground);
            if (pen <= 0) return;
            Vector3d nrm = kind == 2 ? GroundNormal(radial, ut) : radial;
            Vector3d vP = vel + Vector3d.Cross(Vector3d.FromFloat(angVel), rW) - body.SurfaceVelocity(P);
            double vn = Vector3d.Dot(vP, nrm);
            double impact = wheel ? -vn : vP.magnitude;
            float tol = p.def.crashTolerance * (!wheel && Terrain.IsWater(body, body.InertialToFixed(radial, ut)) ? Terrain.WaterCushion : 1f);
            if (impact > tol)
            {
                Destroy(kind == 2 ? $"{p.def.name} broke hitting the ground at {-vn:F1} m/s (it takes {p.def.crashTolerance:F0})"
                      : wheel ? $"Landing gear collapsed at {-vn:F1} m/s sink rate (it takes {p.def.crashTolerance:F0})"
                              : $"{p.def.name} hit the ground at {impact:F0} m/s (it takes {p.def.crashTolerance:F0})");
                return;
            }
            double fn = Math.Max(0, k * pen - c * vn);
            Vector3d fwd;
            if (kind == 2)
            {
                // Rolls across its axle, signed to match the rover's forward; steering turns it toward the right for D.
                Vector3d axle = Vector3d.Rotate(rot, Vector3d.FromFloat(p.localRot * Vector3.right));
                fwd = Vector3d.Cross(axle, nrm);
                if (fwd.sqrMagnitude < 1e-6) fwd = driveFwd;
                fwd = fwd.normalized;
                if (Vector3d.Dot(fwd, driveFwd) < 0) fwd = -fwd;
                Vector3d right = Vector3d.Cross(nrm, fwd);
                double a = p.steer * Mathf.Deg2Rad;
                fwd = (fwd * Math.Cos(a) + right * Math.Sin(a)).normalized;
            }
            else
            {
                Vector3 fwdL = wheel ? Quaternion.AngleAxis(p.steer, Vector3.forward) * Vector3.up : Vector3.up;
                fwd = Vector3d.Rotate(rot, Vector3d.FromFloat(fwdL));
            }
            fwd = (fwd - nrm * Vector3d.Dot(fwd, nrm)).normalized;
            Vector3d side = Vector3d.Cross(nrm, fwd);
            Vector3d vt = vP - nrm * vn;
            double vl = Vector3d.Dot(vt, fwd), vs = Vector3d.Dot(vt, side);
            double muL = wheel ? (brakesOn ? 0.7 : kind == 2 ? 0.03 : 0.02) : 0.5, muS = wheel ? 0.9 : 0.5;
            Vector3d f = nrm * fn + fwd * (-muL * fn * Math.Max(-1, Math.Min(1, vl / 0.3))) + side * (-muS * fn * Math.Max(-1, Math.Min(1, vs / 0.3)));
            if (kind == 2)
            {
                p.touching = true;
                p.rollSpeed = (float)vl;
                if (powered && driveInput != 0 && !brakesOn)
                {
                    // The motor pushes toward the wanted speed, up to its force and the tyre's grip.
                    double target = driveInput * p.def.driveSpeed;
                    double push = p.def.motorForce * Math.Abs(driveInput) * Math.Max(-1, Math.Min(1, target - vl));
                    push = Math.Max(-0.85 * fn, Math.Min(0.85 * fn, push));
                    f = f + fwd * push;
                    motorRate += p.def.motorEc * Math.Abs(push) / p.def.motorForce;
                }
            }
            fWorld = fWorld + f;
            tLocal += Vector3.Cross(local - com, inv * f.ToFloat());
        }

        void IntegrateRK4(double dt, Vector3d thrustAcc)
        {
            Vector3d r0 = pos, v0 = vel;
            Vector3d a1 = Accel(r0, v0, thrustAcc);
            Vector3d r2 = r0 + v0 * (dt / 2), v2 = v0 + a1 * (dt / 2);
            Vector3d a2 = Accel(r2, v2, thrustAcc);
            Vector3d r3 = r0 + v2 * (dt / 2), v3 = v0 + a2 * (dt / 2);
            Vector3d a3 = Accel(r3, v3, thrustAcc);
            Vector3d r4 = r0 + v3 * dt, v4 = v0 + a3 * dt;
            Vector3d a4 = Accel(r4, v4, thrustAcc);
            pos = r0 + (v0 + 2 * v2 + 2 * v3 + v4) * (dt / 6);
            vel = v0 + (a1 + 2 * a2 + 2 * a3 + a4) * (dt / 6);
        }

        Vector3d Accel(Vector3d r, Vector3d v, Vector3d thrustAcc)
        {
            Vector3d a = body.Gravity(r) + thrustAcc;
            double rho = body.Density(r.magnitude - body.radius);
            if (rho > 0)
            {
                Vector3d vrel = v - body.SurfaceVelocity(r) - windInertial;
                double s = vrel.magnitude;
                if (s > 1e-4)
                {
                    Vector3 u = (Quaternion.Inverse(rotCache) * (vrel / s).ToFloat());
                    Vector3 cda = AeroCdA(u, (float)s, omegaCache, out _);
                    double q = 0.5 * rho * s * s; // Pa
                    a = a + Vector3d.Rotate(rotCache, Vector3d.FromFloat(cda)) * (q / (mass * 1000.0));
                }
            }
            return a;
        }

        /// <summary>
        /// Aerodynamic "area vector" summed over parts for a unit flow direction u (vessel velocity relative to air,
        /// vessel frame). Multiply by dynamic pressure for force (N). Also returns the torque vector per unit q about the CoM.
        /// </summary>
        /// <summary>Plate normal of a lifting surface (incidence and control deflection applied about its span axis).</summary>
        public Vector3 SurfaceNormal(PartState p, float extraDeflect = 0f)
        {
            float inc = p.def.incidence * (Vector3.Dot(p.localRot * Vector3.forward, Vector3.forward) >= 0 ? 1f : -1f);
            return p.localRot * (Quaternion.AngleAxis(inc + p.deflect + extraDeflect, Vector3.right) * Vector3.forward);
        }

        /// <summary>Lift + drag "area vector" of one lifting surface for local flow direction 'up' (multiply by q).</summary>
        static Vector3 LiftForce(float area, float slope, Vector3 up, Vector3 nrm)
        {
            float s = Vector3.Dot(up, nrm);
            float c = Mathf.Sqrt(Mathf.Max(0, 1 - s * s));
            // Plate normal force: 'slope' per radian near zero, peaking at 45 deg, plus parasitic drag.
            return -nrm * (area * slope * s * c) - up * (area * (0.02f + 0.1f * s * s));
        }

        /// <summary>
        /// Aerodynamic "area vector" summed over parts for a unit flow direction u (vessel velocity relative to air,
        /// vessel frame) at 'speed'. Each part sees its own airflow including the vessel's rotation, which damps
        /// pitch and yaw naturally. Multiply by dynamic pressure for force (N); also returns torque per unit q.
        /// </summary>
        /// <summary>Drag area (m^2, drag coefficient times area) flying nose first and tail first, averaged: for forecasts.</summary>
        public double DragArea()
        {
            double a = AeroCdA(Vector3.up, 200f, Vector3.zero, out _, false).magnitude;
            double b = AeroCdA(Vector3.down, 200f, Vector3.zero, out _, false).magnitude;
            return (a + b) / 2;
        }

        /// <summary>
        /// Drag areas for an aerobraking pass: 'least' flying as you'd hold it (SAS on retrograde: the base, or heat shield,
        /// into the air) and 'most' side on, as when it tumbles.
        /// </summary>
        public void DragRange(out double least, out double most)
        {
            double tail = AeroCdA(Vector3.down, 200f, Vector3.zero, out _, false).magnitude;
            double side = Math.Max(AeroCdA(Vector3.right, 200f, Vector3.zero, out _, false).magnitude, AeroCdA(Vector3.forward, 200f, Vector3.zero, out _, false).magnitude);
            least = tail;
            most = Math.Max(side, tail);
        }

        /// <summary>
        /// Milestone 32: the speed of sound here (m/s). About 340 at Arden's sea level, falling to 295 by 11 km (the air
        /// gets colder) and creeping back up higher; thinner, colder air elsewhere.
        /// </summary>
        public double SoundSpeed()
        {
            if (body == null || !body.HasAtmosphere) return 300;
            double alt = Math.Max(0, Altitude);
            if (body.IsHomeWorld) return alt < 11000 ? 340 - 45 * alt / 11000 : alt < 20000 ? 295 : Math.Min(330, 295 + (alt - 20000) * 0.0011);
            return 240;
        }

        /// <summary>
        /// Milestone 32: how much harder the air pushes near and past the speed of sound (the transonic drag rise that
        /// makes "max Q" the roughest part of a launch): 1 below Mach 0.8, about 1.45 just past Mach 1, easing back to 1 by
        /// Mach 3. (Reentry and aerocapture, far faster than that, are as they always were.) Lift isn't changed, only drag.
        /// </summary>
        public static float MachDrag(float mach)
        {
            if (!(mach >= 0.8f) || mach >= 3f) return 1f;
            if (mach < 1.05f) { float t = (mach - 0.8f) / 0.25f; return 1f + 0.45f * t * t * (3f - 2f * t); }
            float k = (mach - 1.05f) / 1.95f;
            return 1.45f - 0.45f * k * k * (3f - 2f * k);
        }

        /// <summary>The Mach number now (speed through the air over the speed of sound), for the flight display.</summary>
        public double Mach => body != null && body.HasAtmosphere ? AirRelVelocity.magnitude / SoundSpeed() : 0;

        Vector3 AeroCdA(Vector3 u, float speed, Vector3 omegaL, out Vector3 torque, bool transonic = true)
        {
            Vector3 total = Vector3.zero;
            torque = Vector3.zero;
            foreach (var p in parts) p.aeroArea = Vector3.zero;
            Vector3 vRel = u * speed;
            float invS2 = speed > 0.1f ? 1f / (speed * speed) : 0f;
            float machK = transonic ? MachDrag(speed / (float)SoundSpeed()) : 1f;   // (forecasts use the plain drag)
            foreach (var p in parts)
            {
                if (p.enclosed) continue;
                var d = p.def;
                Vector3 r = p.Center - com;
                Vector3 vp = vRel + Vector3.Cross(omegaL, r); // this part's velocity through the air (vessel motion + rotation)
                float sp = vp.magnitude;
                if (sp < 1e-3f) continue;
                Vector3 uf = vp / sp;
                float w = speed > 0.1f ? sp * sp * invS2 : 1f;
                Vector3 f;
                if (d.radial)
                {
                    float form = d.radialSize.y * d.radialSize.z * (d.LiftArea > 0 ? 0.15f : 0.6f);
                    f = -uf * form * machK;
                    if (d.LiftArea > 0) f += LiftForce(d.LiftArea, d.liftSlope, uf, SurfaceNormal(p));
                }
                else if (d.kind == PartKind.Fairing && !p.fairingGone)
                {
                    float c = Vector3.Dot(p.Axis, uf), R = d.fairingRadius, H = d.fairingHeight;
                    float area = (c > 0 ? Mathf.PI * R * R * 0.3f * c * c : 0) + H * 2 * R * 0.8f * (1 - c * c);
                    f = -uf * area * machK;
                    r = p.localPos + p.localRot * new Vector3(0, H * 0.5f, 0) - com;
                }
                else
                {
                    float c = Vector3.Dot(p.Axis, uf);
                    float area = 0, shieldFace = 0;
                    if (c > 0 && !p.topCovered) area += Mathf.PI * Mathf.Max(p.RTop, 0.05f) * Mathf.Max(p.RTop, 0.05f) * FaceCd(d, true) * c * c;
                    if (c < 0 && !p.bottomCovered)
                    {
                        float bottom = Mathf.PI * p.RBottom * p.RBottom * FaceCd(d, false) * c * c;
                        area += bottom;
                        // A capsule's own blunt base works like a heat shield's (below): it flies base first
                        if (d.kind == PartKind.HeatShield || (d.kind == PartKind.Command && d.stackTop && p.RBottom > 1.6f * p.RTop)) shieldFace = bottom;
                    }
                    float side = d.length * (d.topDiameter + d.bottomDiameter) * 0.5f * 0.8f + d.finArea * 1.2f;
                    area += side * (1 - c * c);
                    f = -uf * area * machK;
                    // A blunt heat shield's pressure acts through its centre of curvature, well behind the shield face, so a
                    // capsule flies shield first by itself (as real ones do) instead of tipping over like an arrow flown backwards.
                    if (shieldFace > 0 && area > 0)
                    {
                        Vector3 rFace = p.Center + p.Axis * (2f * p.RBottom) - com;
                        r = (rFace * shieldFace + r * (area - shieldFace)) / area; // both forces lie along the flow
                    }
                    // Side on (or apex first), air spilling round the blunt base still turns it back into the flow: a
                    // righting couple that's strongest side on, so a capsule never hangs sideways (no extra drag)
                    if (!p.bottomCovered && c < 0.98f && (d.kind == PartKind.HeatShield || (d.kind == PartKind.Command && d.stackTop && p.RBottom > 1.6f * p.RTop)))
                    {
                        float face = Mathf.PI * p.RBottom * p.RBottom * FaceCd(d, false);
                        torque += Vector3.Cross(p.Axis * (2f * p.RBottom), -uf * (face * 0.3f * (1 - c * c) * w));
                    }
                }
                f *= w;
                p.aeroArea = f;
                if (p.chuteDeployed && !p.chuteFailed && p.chuteOpen > 0)
                {
                    Vector3 fc = -u * d.chuteCdA * p.chuteOpen;
                    total += fc;
                    torque += Vector3.Cross(p.localPos - com, fc);
                    p.aeroArea += fc;
                }
                total += f;
                torque += Vector3.Cross(r, f);
            }
            return total;
        }

        /// <summary>Airflow seen by the vessel: unit direction (vessel frame), speed, and dynamic pressure in kPa.</summary>
        double FlowLocal(out Vector3 u, out float speed)
        {
            Vector3d vrel = AirRelVelocity;
            double s = vrel.magnitude;
            speed = (float)s;
            u = s > 1e-3 ? Quaternion.Inverse(rot) * (vrel / s).ToFloat() : Vector3.up;
            return 0.5 * body.Density(Altitude) * s * s / 1000.0;
        }

        static float FaceCd(PartDef d, bool top)
        {
            if (top)
            {
                switch (d.kind)
                {
                    case PartKind.NoseCone:
                    case PartKind.EscapeTower: return 0.25f;
                    case PartKind.Command: return 0.45f;
                    case PartKind.Parachute: return 0.5f;
                    case PartKind.Engine: return 0.9f;
                    default: return 0.8f;
                }
            }
            switch (d.kind)
            {
                case PartKind.Command:
                case PartKind.HeatShield: return 1.1f;
                case PartKind.Engine:
                case PartKind.Solid: return 0.7f;
                default: return 0.85f;
            }
        }

        // ---------------------------------------------------------------- heat

        static double SkinCapacity(PartState p) => Math.Max(Thermal.MinSkinMass, Thermal.SkinFraction * p.Mass) * 1000 * Thermal.Cp;

        static double SurfaceArea(PartState p)
        {
            if (p.def.radial) { var s = p.def.radialSize; return 2 * (s.x * s.y + s.y * s.z + s.x * s.z); }
            return Math.PI * (p.RTop + p.RBottom) * Math.Max(0.1, p.def.length) + Math.PI * (p.RTop * p.RTop + p.RBottom * p.RBottom);
        }

        void UpdateHeat(double dt)
        {
            double rho = body.Density(Altitude);
            Vector3d vrel = SurfaceRelVelocity;
            double s = vrel.magnitude;
            double flux = rho > 0 && s > Thermal.V0 ? Thermal.HeatK * Math.Sqrt(rho) * (s * s * s - Thermal.V0 * Thermal.V0 * Thermal.V0) : 0;
            heatFlux = flux;
            Vector3 u = s > 1 ? Quaternion.Inverse(rot) * (vrel / s).ToFloat() : Vector3.up;
            double airTemp = 288 - Math.Min(60, Altitude / 1000 * 3);
            double best = 0;
            leadingPart = -1;

            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                double qIn = 0;
                if (flux > 0 && !p.enclosed)
                {
                    float c = p.def.radial ? 0 : Vector3.Dot(p.Axis, u);
                    double exposure = 0.03 + 0.12 * (1 - Math.Abs(c));
                    double r = Math.Max(p.RTop, p.RBottom);
                    if (!p.def.radial && c > 0 && !p.topCovered && c > exposure) { exposure = c; r = Math.Max(p.RTop, 0.3); }
                    if (!p.def.radial && c < 0 && !p.bottomCovered && -c > exposure) { exposure = -c; r = p.RBottom; }
                    qIn = flux * exposure * Math.PI * r * r;
                    if (qIn > best) { best = qIn; leadingPart = i; }
                }
                double area = SurfaceArea(p);
                double qRad = Thermal.Sigma * Thermal.Emissivity * area * (Math.Pow(p.temp, 4) - Math.Pow(Thermal.SpaceTemp, 4));
                double hConv = rho > 0 ? 6 * Math.Sqrt(rho * Math.Max(s, 1)) : 0;
                double qConv = hConv * area * (p.temp - airTemp);
                double qAbl = 0;
                if (p.ablator > 0 && p.temp > Thermal.AblateStart)
                {
                    double rate = Thermal.AblateRate * (p.temp - Thermal.AblateStart) / 1000.0;
                    double used = Math.Min(p.ablator, rate * dt);
                    p.ablator -= used;
                    qAbl = used * 1000 * Thermal.AblateEnergy / dt;
                    if (p.ablator <= 0) lastEvent = $"{p.def.name}: ablator used up!";
                }
                p.temp += (qIn - qRad - qConv - qAbl) * dt / SkinCapacity(p);
                if (p.temp < 150) p.temp = 150;
            }

            // Burn-up: a part with nothing hanging off it is lost; losing a command part or a structural part ends the flight.
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                if (i >= parts.Count) continue;   // an explosion took several parts at once
                var p = parts[i];
                if (p.temp <= p.def.maxTemp) continue;
                if (HoldsFuel(p)) { Explode(p, $"{p.def.name} overheated and exploded ({p.temp:0} K, limit {p.def.maxTemp:0} K)"); if (destroyed) return; continue; }
                bool leaf = topo == null || topo.children[p.index].Count == 0;
                if (p.def.kind == PartKind.Command || !leaf)
                {
                    Destroy($"{p.def.name} burned up on reentry ({p.temp:0} K, limit {p.def.maxTemp:0} K)");
                    return;
                }
                lastEvent = $"{p.def.name} burned up!";
                parts.RemoveAt(i);
                RebuildTopology();
                RecomputeMass();
            }
        }

        void CoolInSpace(double dt)
        {
            foreach (var p in parts)
            {
                double qRad = Thermal.Sigma * Thermal.Emissivity * SurfaceArea(p) * (Math.Pow(p.temp, 4) - Math.Pow(Thermal.SpaceTemp, 4));
                p.temp = Math.Max(Thermal.SpaceTemp, p.temp - qRad * dt / SkinCapacity(p));
            }
        }

        // ---------------------------------------------------------------- rotation

        void IntegrateRotation(double dt, float gimbal, Vector3 thrustTorque)
        {
            float fdt = (float)dt;
            Quaternion inv = Quaternion.Inverse(rot);
            Vector3 w = inv * angVel;
            Vector3 tau = ControlTorque(w, gimbal, fdt) + thrustTorque;
            float damp = 0;
            lastAeroTau = Vector3.zero;
            if (dynPressure > 1) { lastAeroTau = AeroTorque(out damp); tau += lastAeroTau; }

            w.x += tau.x / inertia.x * fdt;
            w.y += tau.y / inertia.y * fdt;
            w.z += tau.z / inertia.z * fdt;
            if (damp > 0)
            {
                w.x /= 1f + damp / inertia.x * fdt;
                w.z /= 1f + damp / inertia.z * fdt;
            }
            angVel = rot * w;
            float ang = angVel.magnitude * fdt;
            if (ang > 1e-9f) rot = Quaternion.AngleAxis(ang * Mathf.Rad2Deg, angVel / angVel.magnitude) * rot;
            rot = Quaternion.Normalize(rot);
        }

        /// <summary>Aerodynamic torque in the vessel frame (kN*m), from per-part forces at their centres.</summary>
        Vector3 AeroTorque(out float damp)
        {
            damp = 0;
            double qk = FlowLocal(out Vector3 u, out float s);
            if (s < 0.5f) return Vector3.zero;
            AeroCdA(u, s, Quaternion.Inverse(rot) * angVel, out Vector3 t);
            float len = Mathf.Max(1f, Length);
            float side = 0, chute = 0;
            foreach (var p in parts)
            {
                side += p.def.radial ? p.def.finArea : p.def.length * p.def.MaxDiameter * 0.8f + p.def.finArea;
                if (p.chuteDeployed && !p.chuteFailed) chute += p.def.chuteCdA * p.chuteOpen;
            }
            damp = (float)(qk * (side + chute * 0.3f) * len * len * 0.02);
            return t * (float)qk;
        }

        Vector3 SurfaceNormalAt(PartState p, float deflectDeg)
        {
            float inc = p.def.incidence * (Vector3.Dot(p.localRot * Vector3.forward, Vector3.forward) >= 0 ? 1f : -1f);
            return p.localRot * (Quaternion.AngleAxis(inc + deflectDeg, Vector3.right) * Vector3.forward);
        }

        readonly List<(PartState p, Vector3 e)> surfScratch = new List<(PartState, Vector3)>();

        Vector3 ControlTorque(Vector3 w, float gimbal, float dt)
        {
            wheelUse = 0;
            if (!commandable)
            {
                // Nobody (and nothing powered) at the controls: surfaces centre, no steering at all.
                foreach (var p in parts) if (p.def.controlSurface) p.deflect = Mathf.MoveTowards(p.deflect, 0, 60f * dt);
                sasHoldValid = false;
                return Vector3.zero;
            }
            float rw = reactionTorque;
            bool rcs = rcsOn && MonoLeft > 0 && !isDebris;
            Vector3 rcsCap = rcs ? rcsTorqueCap : Vector3.zero;
            Vector3 tmaxBase = new Vector3(rw + gimbal + rcsCap.x, rw + gimbal * 0.05f + rcsCap.y, rw + gimbal + rcsCap.z);

            // Control surfaces: measure each one's torque per degree of deflection right now, so any placement
            // (mirrored, upside down, on the tail) moves the right way without setup.
            surfScratch.Clear();
            Vector3 aeroCap = Vector3.zero;
            if (!isDebris)
            {
                double qk = FlowLocal(out Vector3 u, out float speed);
                if (qk > 0.005 && speed > 1f)
                {
                    foreach (var p in parts)
                    {
                        if (!p.def.controlSurface || p.enclosed) continue;
                        Vector3 r = p.Center - com;
                        Vector3 vp = u * speed + Vector3.Cross(w, r);
                        float sp = vp.magnitude;
                        if (sp < 1f) continue;
                        Vector3 uf = vp / sp;
                        float k = (float)qk * sp * sp / (speed * speed);
                        Vector3 df = (LiftForce(p.def.LiftArea, p.def.liftSlope, uf, SurfaceNormalAt(p, 1f)) - LiftForce(p.def.LiftArea, p.def.liftSlope, uf, SurfaceNormalAt(p, -1f))) * 0.5f;
                        Vector3 e = Vector3.Cross(r, df) * k; // kN*m per degree
                        surfScratch.Add((p, e));
                        aeroCap += new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z)) * p.def.maxDeflect;
                    }
                }
            }
            Vector3 tmax = tmaxBase + aeroCap;
            if (tmax.x <= 1e-4f && tmax.y <= 1e-4f)
            {
                sasHoldValid = false;
                foreach (var p in parts) if (p.def.controlSurface) p.deflect = Mathf.MoveTowards(p.deflect, 0, 60f * dt);
                return Vector3.zero;
            }
            // W = nose down, D = nose right, Q = roll left (aircraft conventions).
            Vector3 cmd = new Vector3(-inputPitch, -inputRoll, -inputYaw);
            bool manual = cmd.sqrMagnitude > 1e-4f;

            bool steer = false;
            Vector3 autoRate = Vector3.zero;
            if (steerDirection.HasValue) { autoRate = RateTowardDirection(steerDirection.Value); steer = true; }
            else if (sas == SasMode.Stability)
            {
                steer = true;
                if (manual || (rolling && driveInput != 0)) sasHoldValid = false; // a rover driving over bumps goes where the ground takes it
                else if (!sasHoldValid)
                {
                    // Take a new hold once the ship is still; in the air, still for a whole second, so a swing's
                    // turning point (where it pauses for an instant) isn't mistaken for settled
                    sasCalm = w.magnitude < 0.03f ? sasCalm + dt : 0f;
                    if (sasCalm >= (dynPressure > 300 ? 1f : 0f)) { sasHold = rot; sasHoldValid = true; sasStrain = 0; sasCalm = 0; }
                }
                else
                {
                    // In the air, a hold the controls can't keep is let go: the ship settles where the air puts it
                    // (a capsule shield first) and SAS damps the swing and takes a new hold there, instead of
                    // fighting the air all the way down and lurching from side to side
                    // An unpowered ship with no control surfaces (a capsule, a probe, a spent stage) also lets go as soon
                    // as the air pushes hard, so it turns gently while the air is still thin
                    bool ballistic = thrustNow <= 0 && surfScratch.Count == 0;
                    float push = new Vector2(lastAeroTau.x, lastAeroTau.z).magnitude;
                    bool losing = Quaternion.Angle(sasHold, rot) > 10f || (ballistic && push > 0.05f * Mathf.Max(0.01f, Mathf.Min(tmaxBase.x, tmaxBase.z)));
                    if (dynPressure > 300 && losing) sasStrain += dt; else sasStrain = 0;
                    if (sasStrain > 0.6f) { sasHoldValid = false; sasStrain = 0; sasCalm = 0; }
                    else autoRate = RateTowardRotation(sasHold);
                }
            }
            else if (sas != SasMode.Off)
            {
                steer = true;
                var d = SasDirection();
                if (d.HasValue) autoRate = RateTowardDirection(d.Value);
            }

            Vector3 tau = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                float target;
                if (Mathf.Abs(cmd[i]) > 1e-3f) target = cmd[i] * MaxManualRate;
                else if (steer) target = autoRate[i];
                else continue;
                tau[i] = Mathf.Clamp(inertia[i] * (target - w[i]) * RateGain, -tmax[i], tmax[i]);
            }
            // Wheels, gimbal and RCS first; control surfaces make up the rest (least squares per axis).
            Vector3 baseTau = new Vector3(Mathf.Clamp(tau.x, -tmaxBase.x, tmaxBase.x), Mathf.Clamp(tau.y, -tmaxBase.y, tmaxBase.y), Mathf.Clamp(tau.z, -tmaxBase.z, tmaxBase.z));
            Vector3 rem = tau - baseTau;
            Vector3 e2 = Vector3.zero;
            foreach (var s in surfScratch) e2 += Vector3.Scale(s.e, s.e);
            foreach (var p in parts) if (p.def.controlSurface && !surfScratch.Exists(s => s.p == p)) p.deflect = Mathf.MoveTowards(p.deflect, 0, 60f * dt);
            foreach (var (p, e) in surfScratch)
            {
                float d = 0;
                for (int a = 0; a < 3; a++) if (e2[a] > 1e-9f) d += rem[a] * e[a] / e2[a];
                d = Mathf.Clamp(d, -p.def.maxDeflect, p.def.maxDeflect);
                p.deflect = Mathf.MoveTowards(p.deflect, d, 120f * dt);
            }
            if (rcs)
            {
                // RCS supplies whatever the reaction wheels and gimbal can't.
                Vector3 extra = new Vector3(Mathf.Max(0, Mathf.Abs(baseTau.x) - rw - gimbal), Mathf.Max(0, Mathf.Abs(baseTau.y) - rw), Mathf.Max(0, Mathf.Abs(baseTau.z) - rw - gimbal));
                float lever = Mathf.Max(0.5f, rcsTorqueCap.magnitude / Mathf.Max(0.01f, rcsThrustTotal));
                UseMono(extra.magnitude / lever / (RcsIsp * G0) * dt);
            }
            wheelUse = Mathf.Min(Mathf.Abs(baseTau.x), rw) + Mathf.Min(Mathf.Abs(baseTau.y), rw) + Mathf.Min(Mathf.Abs(baseTau.z), rw);
            return baseTau;
        }

        Vector3 RateTowardRotation(Quaternion target)
        {
            Quaternion err = target * Quaternion.Inverse(rot);
            err.ToAngleAxis(out float angDeg, out Vector3 axis);
            if (angDeg > 180f) angDeg -= 360f;
            if (Mathf.Abs(angDeg) < 1e-3f || float.IsNaN(axis.x) || float.IsInfinity(axis.x)) return Vector3.zero;
            Vector3 errLocal = Quaternion.Inverse(rot) * (axis * (angDeg * Mathf.Deg2Rad));
            Vector3 rate = errLocal * 1.6f;
            if (rate.magnitude > 0.7f) rate = rate.normalized * 0.7f;
            return rate;
        }

        Vector3 RateTowardDirection(Vector3d dirD)
        {
            Vector3 nose = rot * Vector3.up;
            Vector3 dir = dirD.ToFloat().normalized;
            if (dir.sqrMagnitude < 0.5f) return Vector3.zero;
            Vector3 rate = RateTowardRotation(Quaternion.FromToRotation(nose, dir) * rot);
            rate.y = 0;
            return rate;
        }

        public Vector3d? SasDirection() => DirectionFor(sas);

        public Vector3d? DirectionFor(SasMode mode)
        {
            switch (mode)
            {
                case SasMode.Maneuver: return maneuverDir;
                case SasMode.DockAlign: return dockAlignDir;
                case SasMode.Target: return targetDir;
                case SasMode.AntiTarget: return targetDir.HasValue ? -targetDir.Value : (Vector3d?)null;
            }
            Vector3d v = relVelOverride ?? (Altitude < 30_000 && body.HasAtmosphere ? SurfaceRelVelocity : vel);
            if (v.magnitude < 0.5) return null;
            Vector3d pro = v.normalized, rhat = pos.normalized;
            Vector3d normal = Vector3d.Cross(pos, vel).normalized;
            Vector3d radial = (rhat - pro * Vector3d.Dot(rhat, pro)).normalized;
            switch (mode)
            {
                case SasMode.Prograde: return pro;
                case SasMode.Retrograde: return -pro;
                case SasMode.Normal: return normal;
                case SasMode.AntiNormal: return -normal;
                case SasMode.RadialOut: return radial;
                case SasMode.RadialIn: return -radial;
                default: return null;
            }
        }

        // ---------------------------------------------------------------- ground, landing, parachutes, SOI

        void StepLanded(double dt, double ut, Vector3 fLocal)
        {
            ConsumePropellant(dt);
            SyncLanded(ut + dt);
            sasHoldValid = false;
            dynPressure = 0;
            heatFlux = 0;
            gForce = 1;
            CoolInSpace(dt * 0.2);
            if (HasWheels && (fLocal.sqrMagnitude > 0 || !brakesOn || driveInput != 0))
            {
                landed = false; // release the parking lock and roll on the wheels
                rails = null;
                return;
            }
            if (fLocal.sqrMagnitude > 0)
            {
                Vector3d fWorld = Vector3d.Rotate(rot, Vector3d.FromFloat(fLocal));
                double upAcc = Vector3d.Dot(fWorld, pos.normalized) / mass;
                if (upAcc > body.SurfaceGravity + 0.05)
                {
                    landed = false;
                    hasLaunched = true;
                    rails = null;
                }
            }
            UpdateChutes(dt);
        }

        /// <summary>Puts a landed vessel's lowest point back on the ground (saves from before the terrain had height).</summary>
        public void SettleOnGround(double ut)
        {
            if (!landed) return;
            SyncLanded(ut);
            double minH = LowestPoint(ut, out var low);
            if (low == null || Math.Abs(minH) < 0.05) return;
            landedFixedPos = landedFixedPos + landedFixedPos.normalized * (-minH);
            SyncLanded(ut);
        }

        void SyncLanded(double ut)
        {
            pos = body.FixedToInertial(landedFixedPos, ut);
            vel = body.SurfaceVelocity(pos);
            rot = body.RotationAt(ut) * landedFixedRot;
            angVel = body.AngularVelocityVector.ToFloat();
        }

        double LowestPoint(double ut, out PartState low)
        {
            Vector3d up = pos.normalized;
            double ground = body.radius + LaunchSite.SurfaceHeight(body, body.InertialToFixed(up, ut));
            double minH = double.MaxValue;
            low = null;
            foreach (var p in parts)
                foreach (var c in ContactPoints(p))
                {
                    double h = (pos + Vector3d.Rotate(rot, Vector3d.FromFloat(c - com))).magnitude - ground;
                    if (h < minH) { minH = h; low = p; }
                }
            return minH;
        }

        public double RadarAltitude(double ut) => LowestPoint(ut, out _);

        void CheckGround(double ut)
        {
            double minH = LowestPoint(ut, out var low);
            if (low == null || minH >= 0) return;
            // Lifting off: ignore millimetre-scale wobble in the terrain height (it's evaluated in single precision).
            if (minH > -0.05 && Vector3d.Dot(SurfaceRelVelocity, pos.normalized) > 0.005) return;
            double impact = SurfaceRelVelocity.magnitude;
            float tolerance = low.def.kind == PartKind.Leg && low.legDeploy < 0.9f ? 4f : low.def.crashTolerance;
            bool water = Terrain.IsWater(body, body.InertialToFixed(pos, ut));
            if (water) tolerance *= Terrain.WaterCushion;   // the sea gives (milestone 25)
            if (impact > tolerance)
            {
                // Deployed landing legs give way (up to 60% over their rating), soaking up the hit: the vessel settles
                // on whatever is lowest now, and an engineer on EVA can fix the legs.
                if (low.def.kind == PartKind.Leg && low.legDeploy >= 0.9f && impact <= tolerance * 1.6)
                {
                    Vector3d upD = pos.normalized;
                    double FootHeight(PartState p) { double h = double.MaxValue; foreach (var c in ContactPoints(p)) h = Math.Min(h, Vector3d.Dot(Vector3d.Rotate(rot, Vector3d.FromFloat(c - com)), upD)); return h; }
                    double lowH = FootHeight(low);
                    var hit = parts.FindAll(p => p.def.kind == PartKind.Leg && p.legDeploy >= 0.9f && FootHeight(p) - lowH <= 0.3);
                    foreach (var p in hit) { p.broken = true; p.legsDeployed = false; p.legDeploy = 0; }
                    int n = hit.Count;
                    lastEvent = $"{n} landing leg{(n == 1 ? "" : "s")} gave way at {impact:F0} m/s (rated {tolerance:F0}). An engineer on EVA can fix {(n == 1 ? "it" : "them")}.";
                    minH = LowestPoint(ut, out _);
                }
                else
                {
                    Destroy(water ? $"Hit the sea at {impact:F0} m/s ({low.def.name} can take {tolerance:F0} m/s in water)" : $"Hit the ground at {impact:F0} m/s ({low.def.name} can take {tolerance:F0} m/s)");
                    return;
                }
            }
            pos = pos + pos.normalized * (-minH);
            landedFixedPos = body.InertialToFixed(pos, ut);
            landedFixedRot = Quaternion.Inverse(body.RotationAt(ut)) * rot;
            landed = true;
            rails = null;
            SyncLanded(ut);
        }

        public bool CheckSoi(double ut)
        {
            if (body.parent != null && pos.magnitude > body.SoiRadius)
            {
                pos = pos + body.PositionAt(ut);
                vel = vel + body.VelocityAt(ut);
                body = body.parent;
            }
            else
            {
                CelestialBody into = null;
                foreach (var c in body.children)
                    if ((pos - c.PositionAt(ut)).magnitude < c.SoiRadius) { into = c; break; }
                if (into == null) return false;
                pos = pos - into.PositionAt(ut);
                vel = vel - into.VelocityAt(ut);
                body = into;
            }
            rails = null;
            prevPos = pos;
            prevRot = rot;
            lastEvent = $"Entering {body.name}'s sphere of influence";
            return true;
        }

        void UpdateChutes(double dt)
        {
            double alt = Altitude, rho = body.Density(alt), speed = SurfaceRelVelocity.magnitude;
            foreach (var p in parts)
            {
                if (p.def.kind != PartKind.Parachute || !p.chuteDeployed || p.chuteFailed) continue;
                if (p.chuteOpen > 0.02f && speed > 380 && rho > 1e-4)
                {
                    p.chuteFailed = true;
                    p.chuteOpen = 0;
                    lastEvent = "Parachute ripped off!";
                    continue;
                }
                float target = p.chuteOpen;
                if (landed || rho < 1e-4) target = 0;
                else if (speed < 280) target = alt < 2000 ? 1f : 0.12f;
                float rate = target > p.chuteOpen ? 0.6f : 2f;
                p.chuteOpen = Mathf.MoveTowards(p.chuteOpen, target, (float)dt * rate);
            }
        }

        public void Destroy(string why)
        {
            destroyed = true;
            destroyReason = why;
            thrustNow = 0;
            foreach (var p in parts) p.thrustFrac = 0;
        }

        // ---------------------------------------------------------------- staging, action groups, abort

        public bool HasStagesLeft => stages != null && nextStage < stages.Count;

        public List<VesselSim> ActivateNextStage()
        {
            var spawned = new List<VesselSim>();
            if (!HasStagesLeft) return spawned;
            var st = stages[nextStage++];
            foreach (int d in st.decouple) { var piece = Decouple(d); if (piece != null) spawned.Add(piece); }
            foreach (int i in st.ignite) { var p = Find(i); if (p != null) p.ignited = true; }
            foreach (int i in st.chutes) { var p = Find(i); if (p != null) p.chuteDeployed = true; }
            foreach (int i in st.jettison) spawned.AddRange(Jettison(i));
            rails = null;
            return spawned;
        }

        /// <summary>Fires an action group. Returns any pieces that broke away.</summary>
        public List<VesselSim> ToggleGroup(int group)
        {
            var spawned = new List<VesselSim>();
            int bit = Groups.Bit(group);
            if (group == Groups.Gear) gearDown = !gearDown;
            if (group == Groups.Lights) lightsOn = !lightsOn;
            if (group == Groups.Brakes) brakesOn = !brakesOn;
            foreach (var p in parts.ToArray())
            {
                if ((p.groups & bit) == 0 || Find(p.index) == null) continue;
                switch (p.def.kind)
                {
                    case PartKind.Leg:
                    case PartKind.Gear: if (!p.broken) p.legsDeployed = group == Groups.Gear ? gearDown : !p.legsDeployed; break;
                    case PartKind.Fairing: spawned.AddRange(Jettison(p.index)); break;
                    case PartKind.Solar:
                    case PartKind.Antenna:
                    case PartKind.Scanner:
                    case PartKind.Drill:
                    case PartKind.Radiator: if (p.def.IsDeployable) p.legsDeployed = !p.legsDeployed; break;
                    case PartKind.Light: p.lightOn = group == Groups.Lights ? lightsOn : !p.lightOn; break;
                    case PartKind.Parachute: p.chuteDeployed = true; break;
                    case PartKind.EscapeTower: if (!p.towerFired) { p.towerFired = true; p.towerTime = 0; } break;
                    case PartKind.Decoupler:
                    case PartKind.RadialDecoupler: { var piece = Decouple(p.index); if (piece != null) spawned.Add(piece); break; }
                    default:
                        if (p.def.IsEngine && !p.def.IsSolid) p.ignited = !p.ignited;
                        else if (p.def.IsSolid) p.ignited = true;
                        break;
                }
            }
            rails = null;
            return spawned;
        }

        /// <summary>Abort: cut liquid engines, then fire the Abort group (escape tower and capsule decoupler by default).</summary>
        public List<VesselSim> Abort()
        {
            throttle = 0;
            foreach (var p in parts) if (p.def.IsEngine && !p.def.IsSolid) p.ignited = false;
            var spawned = ToggleGroup(Groups.Abort);
            lastEvent = "ABORT!";
            return spawned;
        }

        /// <summary>Jettisons an escape tower (flies off on its motor) or a fairing (splits into two halves).</summary>
        List<VesselSim> Jettison(int index)
        {
            var res = new List<VesselSim>();
            var p = Find(index);
            if (p == null) return res;
            if (p.def.kind == PartKind.EscapeTower) { var t = JettisonTower(index); if (t != null) res.Add(t); return res; }
            if (p.def.kind != PartKind.Fairing || p.fairingGone) return res;
            p.fairingGone = true;
            RebuildTopology();
            RecomputeMass();
            for (int k = 0; k < 2; k++)
            {
                Quaternion half = p.localRot * Quaternion.Euler(0, 180f * k, 0);
                var hp = new PartState
                {
                    def = PartCatalog.FairingHalf, index = 0, parent = -1, attach = Attach.Root,
                    localPos = p.localPos + p.localRot * new Vector3(0, p.def.fairingHeight, 0), localRot = half,
                };
                var piece = new VesselSim { body = body, name = name + " (fairing)", isDebris = true, parts = new List<PartState> { hp }, rot = rot, sas = SasMode.Off, throttle = 0, hasLaunched = true, ghostWith = this };
                piece.RebuildTopology();
                piece.RecomputeMass();
                Vector3 outLocal = half * Vector3.right;
                piece.pos = pos + Vector3d.Rotate(rot, Vector3d.FromFloat(piece.com - com));
                piece.vel = vel + Vector3d.Rotate(rot, Vector3d.FromFloat(outLocal * 4f));
                piece.angVel = angVel + rot * (Vector3.Cross(p.Axis, outLocal) * 0.6f);
                piece.prevPos = piece.pos;
                piece.prevRot = piece.rot;
                res.Add(piece);
            }
            lastEvent = "Fairing jettisoned";
            return res;
        }

        VesselSim JettisonTower(int index)
        {
            var p = Find(index);
            if (p == null) return null;
            var piece = Decouple(index, 4.0);
            if (piece != null)
                foreach (var q in piece.parts) if (q.def.kind == PartKind.EscapeTower) { q.towerFired = true; q.towerTime = 0; }
            return piece;
        }

        /// <summary>Detaches a part and everything below it in the tree as a new (uncontrolled) vessel.</summary>
        VesselSim Decouple(int designIndex, double kick = 1.2)
        {
            var dp = Find(designIndex);
            if (dp == null || topo == null || dp.parent < 0 || Find(dp.parent) == null) return null;
            var idx = new HashSet<int>(topo.Subtree(designIndex));
            var lower = parts.FindAll(p => idx.Contains(p.index));
            parts.RemoveAll(p => idx.Contains(p.index));
            foreach (var p in lower) if (p.def.kind == PartKind.Parachute && !p.chuteDeployed) p.groups = 0;

            Vector3 oldCom = com;
            var debris = new VesselSim
            {
                body = body, name = name + " (debris)", isDebris = true, parts = lower,
                rot = rot, angVel = angVel, sas = SasMode.Off, throttle = throttle, hasLaunched = true, ghostWith = this,
            };
            RebuildTopology(); RecomputeMass();
            debris.RebuildTopology(); debris.RecomputeMass();
            // Push apart along the decoupler's axis (radial decouplers push sideways).
            Vector3 pushLocal = dp.def.kind == PartKind.RadialDecoupler ? dp.localRot * Vector3.right : -(dp.localRot * Vector3.up);
            if (dp.def.kind == PartKind.EscapeTower) pushLocal = dp.localRot * Vector3.up;
            Vector3d push = Vector3d.Rotate(rot, Vector3d.FromFloat(pushLocal)).normalized;
            double total = mass + debris.mass;
            debris.pos = pos + Vector3d.Rotate(rot, Vector3d.FromFloat(debris.com - oldCom));
            debris.vel = vel + push * (kick * mass / total);
            pos = pos + Vector3d.Rotate(rot, Vector3d.FromFloat(com - oldCom));
            vel = vel - push * (kick * debris.mass / total);
            debris.prevPos = debris.pos;
            debris.prevRot = debris.rot;
            if (landed) { debris.landed = false; landed = false; }
            rails = null;
            sasHoldValid = false;
            return debris;
        }

        // ---------------------------------------------------------------- time warp

        public bool CanRailsWarp() => !destroyed && !EnginesWouldBurn() && (landed || Altitude > body.atmosphereHeight + (body.HasAtmosphere ? 0 : 5000));

        public RailsEvent RailsAdvance(double dt, double ut, out double advanced)
        {
            advanced = dt;
            if (landed) { SyncLanded(ut + dt); return RailsEvent.None; }
            if (rails == null) rails = Orbit.FromState(pos, vel, body.mu, ut);
            CoolInSpace(Math.Min(dt, 600));
            double limit = body.radius + RailsFloor;
            double tStop = ut + dt;
            RailsEvent ev = RailsEvent.None;
            Patcher.FindEvent(body, rails, ut, ut + dt, out double tSoi, out var kind, out _);
            if ((kind == SegmentEnd.ExitSoi || kind == SegmentEnd.EnterSoi) && tSoi < tStop) { tStop = tSoi + 1e-3; ev = RailsEvent.SoiChange; }
            if (rails.Periapsis < limit)
            {
                int n = (int)Math.Min(4000, Math.Max(8, (tStop - ut) / 5));
                double tPrev = ut;
                for (int i = 1; i <= n; i++)
                {
                    double t = ut + (tStop - ut) * i / n;
                    if (rails.RadiusAt(t) < limit)
                    {
                        double lo = tPrev, hi = t;
                        for (int k = 0; k < 50; k++) { double mid = (lo + hi) / 2; if (rails.RadiusAt(mid) < limit) hi = mid; else lo = mid; }
                        tStop = lo;
                        ev = body.HasAtmosphere ? RailsEvent.Atmosphere : RailsEvent.NearSurface;
                        break;
                    }
                    tPrev = t;
                }
            }
            rails.StateAt(tStop, out pos, out vel);
            advanced = tStop - ut;
            if (ev == RailsEvent.SoiChange && !CheckSoi(tStop)) ev = RailsEvent.None;
            return ev;
        }

        // ---------------------------------------------------------------- readouts

        public List<StageReport> StageReports()
        {
            if (stages == null) return new List<StageReport>();
            IndexArrays(out var defs, out var parent, out var attach, out var present);
            int n = defs.Length;
            var prop = new double[n]; var mono = new double[n]; var ign = new bool[n];
            foreach (var p in parts) { prop[p.index] = p.propellant; mono[p.index] = p.mono; ign[p.index] = p.ignited; }
            return Staging.Analyze(defs, parent, attach, prop, mono, present, ign, stages, Math.Max(0, nextStage - 1), body.SurfaceGravity);
        }

        public float ActiveFuelFraction()
        {
            double have = 0, full = 0;
            var seen = new HashSet<int>();
            foreach (var p in parts)
            {
                if (!p.ignited || !p.def.IsEngine) continue;
                if (p.def.IsSolid) { have += p.propellant; full += p.def.propellant; continue; }
                if (!seen.Add(p.section)) continue;
                foreach (var t in parts) if (t.section == p.section && t.def.kind == PartKind.Tank) { have += t.propellant; full += t.def.propellant; }
            }
            return full > 0 ? (float)(have / full) : 0f;
        }
    }
}
