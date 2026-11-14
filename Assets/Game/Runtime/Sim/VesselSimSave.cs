using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>One part's saved state.</summary>
    [Serializable]
    public class PartSave
    {
        public string id;
        public int index, parent;
        public Attach attach;
        public Vector3 localPos;
        public Quaternion localRot;
        public int groups, variant;
        public string decal;
        public int flagsUsed;
        public float toughness;
        public double propellant, mono, ablator, temp;
        public bool ignited, chuteDeployed, chuteFailed, legsDeployed, lightOn, towerFired, fairingGone;
        public float chuteOpen, legDeploy, towerTime, spool;
        public List<string> crew = new List<string>();
        public int dockedTo = -1;
        public string dockedName;
        public bool dockedDebris;
        public List<ScienceData> science = new List<ScienceData>();
        public double charge, ore, supplies;
        public float charred;
        public int convMode;
        public bool used, broken;
    }

    /// <summary>A vessel's saved state: parts, staging, orbit (or landing spot) and switches.</summary>
    [Serializable]
    public class VesselSave
    {
        public int id;
        public string name, body;
        public bool isDebris, landed, hasLaunched, awake;
        public double launchUt;
        public Vector3d pos, vel, landedFixedPos;
        public Quaternion rot, landedFixedRot;
        public Vector3 angVel;
        public int nextStage;
        public double throttle;
        public SasMode sas;
        public bool rcsOn, gearDown, lightsOn, brakesOn;
        public int saveVersion;   // 0 = saved before crew were tracked (fill the seats on load)
        public int careerFlags;
        public double coreTemp;   // drills/converters (0 in old saves = cold)
        public double driven;     // rovers: metres driven on drivenOn
        public string drivenOn;
        public double starvingSince = -1;
        public List<StageAction> stages = new List<StageAction>();
        public List<PartSave> parts = new List<PartSave>();
    }

    /// <summary>Persistence and background (unloaded) motion.</summary>
    public partial class VesselSim
    {
        public int id;            // unique in the universe; 0 = not added yet
        public double launchUt;   // universe time it left the pad (for mission time)
        public bool awake;        // unloaded but still needs full physics (in the air, or skimming an airless surface)
        public int careerFlags;   // VFlags: what this vessel has achieved (space, orbit, Tal...)
        /// <summary>When set, prograde/retrograde (navball and SAS) use this velocity instead (target-relative mode).</summary>
        public Vector3d? relVelOverride;

        public bool IsControllable => parts.Exists(p => p.def.kind == PartKind.Command || p.def.kind == PartKind.Eva);

        public VesselSave ToSave()
        {
            var s = new VesselSave
            {
                id = id, name = name, body = body.name, isDebris = isDebris, landed = landed, hasLaunched = hasLaunched, awake = awake,
                launchUt = launchUt, pos = pos, vel = vel, landedFixedPos = landedFixedPos, rot = rot, landedFixedRot = landedFixedRot,
                angVel = angVel, nextStage = nextStage, throttle = throttle, sas = sas,
                rcsOn = rcsOn, gearDown = gearDown, lightsOn = lightsOn, brakesOn = brakesOn, saveVersion = 3, careerFlags = careerFlags,
                coreTemp = coreTemp, driven = driven, drivenOn = drivenOn, starvingSince = starvingSince, stages = stages ?? new List<StageAction>(),
            };
            foreach (var p in parts)
                s.parts.Add(new PartSave
                {
                    id = p.def.id, index = p.index, parent = p.parent, attach = p.attach, localPos = p.localPos, localRot = p.localRot,
                    groups = p.groups, variant = p.variant, decal = p.decal, flagsUsed = p.flagsUsed, toughness = p.toughness, propellant = p.propellant, mono = p.mono, ablator = p.ablator, temp = p.temp,
                    ignited = p.ignited, chuteDeployed = p.chuteDeployed, chuteFailed = p.chuteFailed, legsDeployed = p.legsDeployed,
                    lightOn = p.lightOn, towerFired = p.towerFired, fairingGone = p.fairingGone,
                    chuteOpen = p.chuteOpen, legDeploy = p.legDeploy, towerTime = p.towerTime, spool = p.spool,
                    crew = new List<string>(p.crew), dockedTo = p.dockedTo, dockedName = p.dockedName, dockedDebris = p.dockedDebris,
                    science = new List<ScienceData>(p.science), charge = p.charge, ore = p.ore, supplies = p.supplies, charred = p.charred, convMode = p.convMode, used = p.used, broken = p.broken,
                });
            return s;
        }

        /// <summary>Rebuilds a vessel from its save; null if its body or any part no longer exists.</summary>
        public static VesselSim FromSave(VesselSave s, IList<CelestialBody> bodies, double ut)
        {
            CelestialBody b = null;
            foreach (var c in bodies) if (c.name == s.body) b = c;
            if (b == null || s.parts == null || s.parts.Count == 0) return null;
            var v = new VesselSim
            {
                id = s.id, name = s.name, body = b, isDebris = s.isDebris, landed = s.landed, hasLaunched = s.hasLaunched, awake = s.awake,
                launchUt = s.launchUt, pos = s.pos, vel = s.vel, landedFixedPos = s.landedFixedPos, rot = s.rot, landedFixedRot = s.landedFixedRot,
                angVel = s.angVel, nextStage = s.nextStage, throttle = s.throttle, sas = s.sas,
                rcsOn = s.rcsOn, gearDown = s.gearDown, lightsOn = s.lightsOn, brakesOn = s.brakesOn, careerFlags = s.careerFlags, starvingSince = s.starvingSince,
                coreTemp = s.coreTemp > 1 ? s.coreTemp : Ore.AmbientTemp, driven = s.driven, drivenOn = s.drivenOn ?? "", stages = s.stages ?? new List<StageAction>(),
            };
            foreach (var ps in s.parts)
            {
                var def = ps.id == PartCatalog.FairingHalf.id ? PartCatalog.FairingHalf : PartCatalog.Get(ps.id);
                if (def == null) return null;
                v.parts.Add(new PartState
                {
                    def = def, index = ps.index, parent = ps.parent, attach = ps.attach, localPos = ps.localPos, localRot = ps.localRot,
                    groups = ps.groups, variant = ps.variant, decal = ps.decal, flagsUsed = ps.flagsUsed, toughness = ps.toughness, propellant = ps.propellant, mono = ps.mono, ablator = ps.ablator, temp = ps.temp,
                    ignited = ps.ignited, chuteDeployed = ps.chuteDeployed, chuteFailed = ps.chuteFailed, legsDeployed = ps.legsDeployed,
                    lightOn = ps.lightOn, towerFired = ps.towerFired, fairingGone = ps.fairingGone,
                    chuteOpen = ps.chuteOpen, legDeploy = ps.legDeploy, towerTime = ps.towerTime, spool = ps.spool,
                    crew = ps.crew != null ? new List<string>(ps.crew) : new List<string>(),
                    dockedTo = ps.dockedTo, dockedName = ps.dockedName, dockedDebris = ps.dockedDebris,
                    science = ps.science != null ? new List<ScienceData>(ps.science) : new List<ScienceData>(),
                });
                var np = v.parts[v.parts.Count - 1];
                np.charge = s.saveVersion < 3 ? def.ecCapacity : Math.Min(ps.charge, def.ecCapacity);
                np.supplies = Math.Min(Math.Max(0, ps.supplies), def.supplyCapacity);
                np.charred = Mathf.Clamp01(ps.charred);
                np.ore = Math.Max(0, Math.Min(ps.ore, def.oreCapacity));
                np.convMode = def.convertRate > 0 ? Math.Max(0, Math.Min(2, ps.convMode)) : 0;
                np.used = ps.used;
                np.broken = ps.broken;
                if (s.saveVersion < 2) { np.dockedTo = -1; for (int k = 0; k < def.crew; k++) np.crew.Add(CrewNames.Next()); }
            }
            v.RebuildTopology();
            v.RecomputeMass();
            if (v.landed) v.SettleOnGround(ut);
            v.prevPos = v.pos;
            v.prevRot = v.rot;
            return v;
        }

        /// <summary>Altitude below which a vessel can't coast on rails: the top of the air, or 5 km on airless worlds.</summary>
        public double RailsFloor => body.isStar ? SunBurnAltitude : body.HasAtmosphere ? body.atmosphereHeight : 5000;
        /// <summary>Closer than this to the sun's surface, anything burns up.</summary>
        public const double SunBurnAltitude = 20_000_000;
        /// <summary>How deep into a gas giant's cloud tops a vessel can go before the pressure crushes it (m).</summary>
        public const double GasGiantFloor = 2_000;

        /// <summary>
        /// Moves an unloaded vessel along its orbit (exact Kepler, through SOI changes); landed vessels ride the surface.
        /// Attitude is frozen while on rails. If it reaches air (or skims an airless surface) it is lost when warping,
        /// otherwise it's marked awake so the flight steps it with full physics.
        /// </summary>
        public void BackgroundAdvance(double dt, double ut, bool warping)
        {
            if (destroyed) return;
            if (landed) SyncLanded(ut + dt);
            else
            {
                double t = ut, end = ut + dt;
                for (int guard = 0; guard < 6 && t < end - 1e-9; guard++)
                {
                    var ev = RailsAdvance(end - t, t, out double adv);
                    t += adv;
                    if (ev == RailsEvent.Atmosphere || ev == RailsEvent.NearSurface)
                    {
                        if (warping) Destroy(body.isStar ? $"{name} burned up close to {body.name}" : body.HasAtmosphere ? $"{name} was lost in {body.name}'s atmosphere" : $"{name} crashed into {body.name}");
                        else awake = true;
                        break;
                    }
                }
                lastEvent = null;
                angVel = Vector3.zero;
            }
            wheelUse = 0;
            UpdateSystems(dt, ut);
            RecomputeMass();
            prevPos = pos;
            prevRot = rot;
        }

        /// <summary>Plain-language situation: "Orbiting Arden, 80 x 78 km", "Landed on Tal"...</summary>
        public string Situation(double ut)
        {
            if (destroyed) return "Destroyed";
            if (landed) return $"Landed on {body.name}";
            if (body.HasAtmosphere && Altitude < body.atmosphereHeight) return $"Flying over {body.name}";
            var o = Orbit.FromState(pos, vel, body.mu, ut);
            string km(double r) => (r - body.radius) >= 1e9 ? $"{(r - body.radius) / 1e9:0.00} Gm" : (r - body.radius) >= 1e6 ? $"{(r - body.radius) / 1e6:0.00} Mm" : $"{(r - body.radius) / 1000:0} km";
            if (!o.IsElliptic || o.Apoapsis > body.SoiRadius)
            {
                if (!IsRock) return $"Escaping {body.name}";
                bool coming = Vector3d.Dot(pos, vel) < 0;
                return coming ? $"Passing {body.name}: closest {km(o.Periapsis)}" : $"Leaving {body.name}'s space";
            }
            if (o.Periapsis - body.radius < body.SafeAltitude) return $"Sub-orbital over {body.name}";
            return $"Orbiting {body.name}, {km(o.Apoapsis)} x {km(o.Periapsis)}";
        }
    }
}
