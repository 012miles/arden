using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// EVA: astronauts step out of the hatch as their own little vessel. On the ground they walk (W/S), turn (A/D) and
    /// jump; anywhere they can fly a small jetpack. They take EVA reports and surface samples, plant flags, fix broken
    /// landing legs (engineers) and repack used parachutes, then climb back into any hatch with a free seat.
    /// </summary>
    public partial class VesselSim
    {
        public const double EvaWalkSpeed = 1.6, EvaRunFactor = 2.4, EvaJumpSpeed = 3.0, BoardRange = 3.0, WorkRange = 4.5; // reach includes climbing the lander's side
        public const float EvaTurnRate = 100f;
        /// <summary>
        /// Milestone 34 (walking in first and third person): a sideways step (-1 left, 1 right), running, and a turn in
        /// degrees from the mouse (used up by the next step).
        /// </summary>
        public float evaStrafe, evaLookYaw;
        public bool evaRun;

        public bool IsEva => parts.Count == 1 && parts[0].def.kind == PartKind.Eva;
        /// <summary>Milestone 35 (captaincarl24: "why do Ardlings have infinite flags, give them a limit of like 3"): flags per trip outside.</summary>
        public const int FlagsCarried = 3;
        public int FlagsLeft => IsEva ? Math.Max(0, FlagsCarried - parts[0].flagsUsed) : 0;
        /// <summary>Milestone 35: down on the sea (a splashdown bobs; an astronaut swims).</summary>
        public bool InWater => landed && Terrain.IsWater(body, landedFixedPos);
        public const double EvaSwimSpeed = 0.9;
        public bool IsFlag => parts.Exists(p => p.def.kind == PartKind.Flag);
        public bool evaJump;
        public string EvaName => IsEva && parts[0].crew.Count > 0 ? parts[0].crew[0] : null;

        /// <summary>Walking on the ground: W/S along the way the astronaut faces, A/D to turn, a jump or the jetpack to leave the ground.</summary>
        void StepEvaGround(double dt, double ut, Vector3 fLocal)
        {
            dynPressure = 0;
            heatFlux = 0;
            gForce = 1;
            sasHoldValid = false;
            CoolInSpace(dt * 0.2);
            if (fLocal.sqrMagnitude > 0)
            {
                Vector3d fWorld = Vector3d.Rotate(rot, Vector3d.FromFloat(fLocal));
                if (Vector3d.Dot(fWorld, pos.normalized) / mass > body.SurfaceGravity * 1.02) { landed = false; hasLaunched = true; rails = null; return; }
            }
            if (evaJump && Terrain.IsWater(body, landedFixedPos)) evaJump = false;   // milestone 35: no jumping out of the sea
            if (evaJump)
            {
                evaJump = false;
                landed = false;
                hasLaunched = true;
                rails = null;
                vel = vel + pos.normalized * EvaJumpSpeed;
                return;
            }
            Vector3d up = landedFixedPos.normalized;
            Quaternion q = landedFixedRot;
            float turn = inputYaw * EvaTurnRate * (float)dt + evaLookYaw;
            evaLookYaw = 0;
            if (Mathf.Abs(turn) > 1e-4f) q = Quaternion.AngleAxis(turn, up.ToFloat()) * q;
            var move = new Vector2(evaStrafe, inputPitch);
            if (move.sqrMagnitude > 1f) move.Normalize();
            if (move.sqrMagnitude > 1e-4f)
            {
                Vector3d fwd = Vector3d.FromFloat(q * Vector3.forward), right = Vector3d.FromFloat(q * Vector3.right);
                fwd = (fwd - up * Vector3d.Dot(fwd, up)).normalized;
                right = (right - up * Vector3d.Dot(right, up)).normalized;
                bool swim = Terrain.IsWater(body, up);
                double speed = swim ? EvaSwimSpeed * (evaRun ? 1.6 : 1.0) : EvaWalkSpeed * (evaRun ? EvaRunFactor : 1.0);   // (milestone 35: slower swimming)
                double stand = landedFixedPos.magnitude - (body.radius + LaunchSite.SurfaceHeight(body, up));
                Vector3d nu = (landedFixedPos + (fwd * move.y + right * move.x) * (speed * dt)).normalized;
                landedFixedPos = nu * (body.radius + LaunchSite.SurfaceHeight(body, nu) + stand);
                q = Quaternion.FromToRotation(q * Vector3.up, nu.ToFloat()) * q;
            }
            landedFixedRot = q;
            SyncLanded(ut + dt);
        }

        /// <summary>Which way is out of a crewed part's hatch (vessel frame): the side that's most level with the ground when landed.</summary>
        Vector3 HatchDir(PartState p)
        {
            Vector3[] sides = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            if (!landed) return p.localRot * Vector3.forward;
            Vector3 up = (Quaternion.Inverse(rot) * pos.normalized.ToFloat()).normalized;
            Vector3 best = p.localRot * Vector3.forward;
            float bd = float.MaxValue;
            foreach (var s in sides) { Vector3 d = p.localRot * s; float t = Mathf.Abs(Vector3.Dot(d, up)); if (t < bd - 0.05f) { bd = t; best = d; } }
            return best;
        }

        /// <summary>Why someone can't step out right now, or null.</summary>
        public string EvaProblem(string crewName)
        {
            if (destroyed) return "The vessel is gone.";
            if (IsEva) return "Already on EVA.";
            if (!parts.Exists(p => p.crew.Contains(crewName))) return $"{crewName} isn't aboard.";
            if (!landed && body.HasAtmosphere && Altitude < body.atmosphereHeight && SurfaceRelVelocity.magnitude > 3)
                return "Too fast to open the hatch in the air: land first, or get to space.";
            if (dynPressure > 500) return "Too much air rushing past to open the hatch.";
            return null;
        }

        /// <summary>Sends someone out of the hatch. Returns the astronaut (a new vessel for the universe), or null with the reason.</summary>
        public VesselSim GoEva(string crewName, double ut, out string why)
        {
            why = EvaProblem(crewName);
            if (why != null) return null;
            var p = parts.Find(q => q.crew.Contains(crewName));
            var design = VesselDesign.Stack(crewName, PartCatalog.EvaSuit.id);
            Vector3 outL = HatchDir(p).normalized;
            float r = Mathf.Max(p.RTop, p.RBottom);
            Vector3d hatch = pos + Vector3d.Rotate(rot, Vector3d.FromFloat(p.Center - com + outL * (r + (landed ? 1.2f : 0.7f))));
            VesselSim eva;
            if (landed)
            {
                eva = CreateLanded(design, body, body.InertialToFixed(hatch, ut).normalized, ut);
                // face away from the hatch
                Vector3d up = eva.landedFixedPos.normalized;
                Vector3d outFixed = body.InertialToFixed(Vector3d.Rotate(rot, Vector3d.FromFloat(outL)), ut);
                outFixed = (outFixed - up * Vector3d.Dot(outFixed, up)).normalized;
                if (outFixed.sqrMagnitude > 0.5) eva.landedFixedRot = Quaternion.LookRotation(outFixed.ToFloat(), up.ToFloat());
                eva.SyncLanded(ut);
            }
            else eva = CreateFree(design, body, hatch, vel + Vector3d.Rotate(rot, Vector3d.FromFloat(outL)) * 0.2, rot);
            var suit = eva.parts[0];
            suit.crew.Clear();
            suit.crew.Add(crewName);
            if (career != null && career.suppliesOn)
            {
                double take = Math.Min(suit.def.supplyCapacity, SuppliesStored);
                TakeSupplies(take);
                suit.supplies = take;
            }
            eva.name = crewName;
            eva.rcsOn = true;
            eva.sas = SasMode.Stability;
            eva.throttle = 0;
            eva.hasLaunched = true;
            eva.launchUt = launchUt;
            eva.careerFlags = careerFlags; // the astronaut's experience comes along
            eva.career = career;
            eva.survey = survey;
            eva.prevPos = eva.pos;
            eva.prevRot = eva.rot;
            p.crew.Remove(crewName);
            RecomputeMass();
            eva.RecomputeMass();
            eva.ghostWith = this;
            return eva;
        }

        /// <summary>World-relative distance (same body) from this vessel's centre of mass to a part of another vessel.</summary>
        public double DistanceToPart(VesselSim other, PartState p) =>
            other.body != body ? double.MaxValue : (other.pos + Vector3d.Rotate(other.rot, Vector3d.FromFloat(p.Center - other.com)) - pos).magnitude;

        /// <summary>
        /// How far an astronaut has to reach to a part. Standing on the ground beside a landed vessel, only the distance
        /// across the ground counts: they climb its side to get at the hatch or the parachute on top.
        /// </summary>
        public double ReachTo(VesselSim other, PartState p)
        {
            if (other.body != body) return double.MaxValue;
            Vector3d d = other.pos + Vector3d.Rotate(other.rot, Vector3d.FromFloat(p.Center - other.com)) - pos;
            if (landed && other.landed) { Vector3d up = pos.normalized; d = d - up * Vector3d.Dot(d, up); }
            return d.magnitude - Math.Max(p.RTop, p.RBottom);
        }

        /// <summary>For an astronaut: the nearest part of 'target' with a free seat within reach of its hull, or null.</summary>
        public PartState BoardingPart(VesselSim target)
        {
            if (!IsEva || target == null || target == this || target.destroyed || target.body != body || target.IsEva) return null;
            PartState best = null;
            double bd = double.MaxValue;
            foreach (var p in target.parts)
            {
                if (p.def.crew <= 0 || p.crew.Count >= p.def.crew) continue;
                double d = ReachTo(target, p) - (landed && target.landed ? 0 : Math.Max(p.def.length, 0.5) * 0.5);
                if (d < bd) { bd = d; best = p; }
            }
            return bd <= BoardRange ? best : null;
        }

        /// <summary>The astronaut climbs into 'target', taking any results they hold. This vessel is then empty and should be removed.</summary>
        public bool Board(VesselSim target, out string msg)
        {
            var p = BoardingPart(target);
            if (p == null) { msg = "Get within 3 m of a hatch with a free seat."; return false; }
            var suit = parts[0];
            string who = EvaName;
            p.crew.Add(who);
            target.AddSupplies(suit.supplies);
            suit.supplies = 0;
            p.science.AddRange(suit.science);
            suit.science.Clear();
            suit.crew.Clear();
            target.careerFlags |= careerFlags & Crew.XpMask;
            target.RecomputeMass();
            RecomputeMass();
            msg = $"{who} climbed into the {p.def.name} of {target.name}.";
            return true;
        }

        /// <summary>A landed astronaut plants a flag a step ahead. Returns the flag (a new vessel), or null with the reason.</summary>
        public VesselSim PlantFlag(double ut, out string why)
        {
            why = !IsEva ? "Only an astronaut on EVA can plant a flag." : !landed ? "Stand on the ground to plant a flag."
                : FlagsLeft <= 0 ? $"No flags left: an Ardling carries {FlagsCarried} each time they go out. Climb back in for more." : null;
            if (why != null) return null;
            parts[0].flagsUsed++;
            Vector3d up = landedFixedPos.normalized;
            Vector3d fwd = Vector3d.FromFloat(landedFixedRot * Vector3.forward);
            fwd = (fwd - up * Vector3d.Dot(fwd, up)).normalized;
            Vector3d spot = (landedFixedPos + fwd * 1.3).normalized;
            var flag = CreateLanded(VesselDesign.Stack($"{body.name} flag", PartCatalog.FlagPole.id), body, spot, ut);
            flag.name = $"Flag: {EvaName} on {body.name}";
            flag.landedFixedRot = Quaternion.LookRotation(fwd.ToFloat(), up.ToFloat());
            flag.SyncLanded(ut);
            flag.throttle = 0;
            flag.sas = SasMode.Off;
            return flag;
        }

        /// <summary>Broken legs and used parachutes on nearby vessels that this astronaut can reach.</summary>
        public List<(VesselSim v, PartState p)> WorkInReach(IEnumerable<VesselSim> vessels)
        {
            var list = new List<(VesselSim, PartState)>();
            if (!IsEva) return list;
            foreach (var v in vessels)
            {
                if (v == this || v.destroyed || v.body != body || v.IsEva) continue;
                foreach (var p in v.parts)
                {
                    bool job = p.broken || (p.def.kind == PartKind.Parachute && (p.chuteDeployed || p.chuteFailed));
                    if (job && ReachTo(v, p) <= WorkRange + (landed && v.landed ? 0 : Math.Max(p.def.Height, 0.5) * 0.5)) list.Add((v, p));
                }
            }
            return list;
        }

        /// <summary>An engineer on EVA fixes a broken landing leg (it comes back folded: deploy it with G).</summary>
        public string Repair(VesselSim v, PartState p)
        {
            if (!IsEva) return "Only an astronaut on EVA can make repairs.";
            if (!p.broken) return $"The {p.def.name} isn't broken.";
            var m = CrewInfo(EvaName);
            if (m.role != CrewRole.Engineer) return $"Only an engineer can fix the {p.def.name}: {EvaName} is a {Crew.RoleName(m.role)}.";
            p.broken = false;
            p.legsDeployed = v.gearDown; // back to wherever the gear switch is
            v.RecomputeMass();
            return $"{EvaName} fixed the {p.def.name}." + (v.gearDown ? " It's coming back down." : " Deploy it with G.");
        }

        /// <summary>Anyone on EVA can repack a used parachute; it opens again on the vessel's next stage.</summary>
        public string Repack(VesselSim v, PartState p)
        {
            if (!IsEva) return "Only an astronaut on EVA can repack a parachute.";
            if (p.def.kind != PartKind.Parachute) return $"The {p.def.name} isn't a parachute.";
            if (!p.chuteDeployed && !p.chuteFailed) return "That parachute is already packed.";
            p.chuteDeployed = false;
            p.chuteFailed = false;
            p.chuteOpen = 0;
            if (v.stages == null) v.stages = new List<StageAction>();
            v.stages.Insert(Math.Min(v.nextStage, v.stages.Count), new StageAction { chutes = new List<int> { p.index } });
            return $"{EvaName} repacked the {p.def.name}: it opens on {v.name}'s next stage (SPACE).";
        }
    }
}
