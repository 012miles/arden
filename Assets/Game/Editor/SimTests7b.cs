using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 7b tests: EVA, walking, the jetpack, EVA science, flags, broken legs and repacked parachutes.</summary>
    public static partial class SimTests
    {
        static void RunMilestone7b()
        {
            Run("EVA: out of the hatch next to it, own vessel with control; back in through any free seat", EvaOutAndBack);
            Run("EVA walking: W walks along the ground, A/D turn, a jump comes back down safely", EvaWalking);
            Run("Jetpack: pushes about 2.6 m/s^2 on monopropellant; lifts off Tal but not Arden", EvaJetpack);
            Run("EVA science: EVA report anywhere, surface sample on the ground, results go aboard when boarding", EvaScience);
            Run("Flags: planted a step ahead, stay put as their own marker and survive save/load", EvaFlags);
            Run("Legs: a hard landing breaks them instead of the vessel; only an engineer on EVA can fix them", LegsBreakAndRepair);
            Run("Parachutes: anyone on EVA can repack a used one; it opens on the next stage", ChuteRepack);
        }

        static VesselDesign Hopper()
        {
            var d = D("Hopper", "chute.s", "pod.wren", "hab.s", "tank.s2", "eng.wisp");
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s2"), 0.95f, 45f, "leg.s", 4, false);
            return d;
        }

        static VesselSim LandedHopper(Universe u, CelestialBody b, params CrewMember[] crew)
        {
            var v = u.Add(VesselSim.CreateLanded(Hopper(), b, CelestialBody.DirectionFromLatLon(5, 10), 0));
            Seat(v, crew);
            return v;
        }

        static void StepFor(VesselSim v, double seconds, ref double t)
        {
            int n = (int)Math.Round(seconds / 0.02);
            for (int i = 0; i < n && !v.destroyed; i++) { v.Step(0.02, t); t += 0.02; }
        }

        static string EvaOutAndBack()
        {
            var u = CareerUniverse();
            var home = System4[0];
            var pilot = u.career.roster.First(m => m.role == CrewRole.Pilot);
            var v = u.Add(InOrbit(Hopper(), home, home.radius + 150_000, 0));
            Seat(v, pilot);
            var pod = v.parts.First(p => p.def.id == "pod.wren");
            var eva = v.GoEva(pilot.name, 0, out string why);
            Check(eva != null, "no EVA: " + why);
            u.Add(eva);
            Check(eva.IsEva && eva.HasControl && eva.EvaName == pilot.name && pod.crew.Count == 0, "astronaut not set up right");
            double d = v.DistanceToPart(eva, eva.parts[0]);
            double rel = (eva.vel - v.vel).magnitude;
            Check(d > 0.5 && d < 4, $"astronaut appeared {d:0.0} m from the vessel");
            Check(rel < 0.5, $"astronaut drifting at {rel:0.00} m/s");
            Check(!v.HasControl, "the empty capsule kept control");
            Check(eva.BoardingPart(v) != null, "can't reach the hatch it came out of");
            Check(eva.Board(v, out string msg) && pod.crew.Contains(pilot.name) && eva.parts[0].crew.Count == 0, "boarding failed: " + msg);
            Check(v.HasControl, "no control after climbing back in");
            // Can't open the hatch flying fast through air
            var plane = InOrbit(Hopper(), home, home.radius + 20_000, 0);
            Seat(plane, pilot);
            Check(plane.EvaProblem(pilot.name)?.Contains("fast") == true, "EVA allowed at orbital speed in the air");
            return $"out {d:0.0} m from the hatch at {rel:0.00} m/s; back in through the capsule";
        }

        static string EvaWalking()
        {
            var u = CareerUniverse();
            var tal = Tal;
            var eng = u.career.roster.First(m => m.role == CrewRole.Engineer);
            var v = LandedHopper(u, tal, eng);
            var eva = u.Add(v.GoEva(eng.name, 0, out string why) ?? throw new Exception(why));
            Check(eva.landed, "an EVA from a landed vessel should stand on the ground");
            double t = 0;
            Vector3d p0 = tal.InertialToFixed(eva.pos, 0); // the ground turns with Tal: measure on the surface
            eva.inputPitch = 1;
            StepFor(eva, 5, ref t);
            eva.inputPitch = 0;
            double walked = Vector3d.Distance(tal.InertialToFixed(eva.pos, t), p0);
            Check(eva.landed && Math.Abs(walked - VesselSim.EvaWalkSpeed * 5) < 0.6, $"walked {walked:0.0} m in 5 s (landed={eva.landed})");
            Check(Math.Abs(eva.RadarAltitude(t)) < 0.1, $"floating {eva.RadarAltitude(t):0.00} m above the ground");
            Vector3 f0 = eva.rot * Vector3.forward;
            eva.inputYaw = 1;
            StepFor(eva, 0.9, ref t);
            eva.inputYaw = 0;
            float turned = Vector3.Angle(f0, eva.rot * Vector3.forward);
            Check(turned > 80 && turned < 100, $"turned {turned:0} deg in 0.9 s");
            eva.evaJump = true;
            double top = 0;
            bool left = false;
            for (int i = 0; i < 500 && !eva.destroyed; i++)
            {
                eva.Step(0.02, t); t += 0.02;
                if (!eva.landed) left = true;
                top = Math.Max(top, eva.RadarAltitude(t));
                if (left && eva.landed) break;
            }
            Check(left && eva.landed && !eva.destroyed, $"jump: left={left}, landed={eva.landed}, destroyed={eva.destroyReason}");
            double expect = VesselSim.EvaJumpSpeed * VesselSim.EvaJumpSpeed / (2 * tal.SurfaceGravity);
            Check(Math.Abs(top - expect) < 0.3, $"jumped {top:0.00} m (expected {expect:0.00})");
            return $"walked {walked:0.0} m in 5 s, turned {turned:0} deg, jumped {top:0.0} m on Tal";
        }

        static string EvaJetpack()
        {
            var u = CareerUniverse();
            var home = System4[0];
            var pilot = u.career.roster.First(m => m.role == CrewRole.Pilot);
            var v = u.Add(InOrbit(Hopper(), home, home.radius + 150_000, 0));
            Seat(v, pilot);
            var eva = u.Add(v.GoEva(pilot.name, 0, out _));
            eva.sas = SasMode.Off;
            Vector3d rel0 = eva.vel - v.vel;
            double m0 = eva.MonoLeft, t = 0;
            eva.inputTranslate = new Vector3(0, 0, 1);
            for (int i = 0; i < 200; i++) { eva.Step(0.02, t); v.Step(0.02, t); t += 0.02; } // gravity pulls both alike
            eva.inputTranslate = Vector3.zero;
            double dvJet = (eva.vel - v.vel - rel0).magnitude, a = 0.25 / (0.09 + 0.006);
            double used = m0 - eva.MonoLeft;
            Check(used > 0.0003 && used < 0.0006, $"4 s of jetpack used {used * 1000:0.00} kg");
            Check(Math.Abs(dvJet - a * 4) < a * 4 * 0.1, $"jetpack changed speed by {dvJet:0.0} m/s (expected about {a * 4:0.0})");

            // Lift-off: works on Tal, not on Arden
            var eng = u.career.roster.First(m => m.role == CrewRole.Engineer);
            var lander = LandedHopper(u, Tal, eng);
            var tEva = u.Add(lander.GoEva(eng.name, 0, out _));
            tEva.inputTranslate = new Vector3(0, 1, 0);
            double t3 = 0;
            StepFor(tEva, 0.5, ref t3);
            Check(!tEva.landed && tEva.RadarAltitude(t3) > 0.05,
                $"the jetpack didn't lift off Tal: landed={tEva.landed}, radar {tEva.RadarAltitude(t3):0.000} m, climb {Vector3d.Dot(tEva.SurfaceRelVelocity, tEva.pos.normalized):0.000} m/s, mono {tEva.MonoLeft * 1000:0.0} kg, ground {LaunchSite.SurfaceHeight(Tal, Tal.InertialToFixed(tEva.pos, t3)):0} m");
            var sci = u.career.roster.First(m => m.role == CrewRole.Scientist);
            var ard = u.Add(VesselSim.CreateLanded(Hopper(), home, CelestialBody.DirectionFromLatLon(10, 40), 0));
            Seat(ard, sci);
            var aEva = u.Add(ard.GoEva(sci.name, 0, out _));
            aEva.inputTranslate = new Vector3(0, 1, 0);
            double t4 = 0;
            StepFor(aEva, 0.5, ref t4);
            Check(aEva.landed, "the jetpack lifted off Arden");
            return $"jetpack {dvJet:0.0} m/s in 4 s on {used * 1000:0.00} kg; lifts off Tal, not Arden";
        }

        static string EvaScience()
        {
            var u = CareerUniverse();
            var c = u.career;
            var sci = c.roster.First(m => m.role == CrewRole.Scientist);
            var v = LandedHopper(u, Tal, sci);
            var eva = u.Add(v.GoEva(sci.name, 0, out _));
            var list = Lab.On(eva);
            Check(list.Any(x => x.exp.id == "eva") && list.Any(x => x.exp.id == "surface"), "EVA experiments missing");
            var suit = eva.parts[0];
            Check(Lab.Run(c, eva, suit, Science.Get("surface"), 0).StartsWith("Surface sample"), "no surface sample on the ground");
            Check(Lab.Run(c, eva, suit, Science.Get("eva"), 0).StartsWith("EVA report"), "no EVA report");
            Check(suit.science.Count == 2, "results not held by the suit");
            Check(eva.Board(v, out _), "couldn't board");
            Check(v.parts.Sum(p => p.science.Count) == 2 && suit.science.Count == 0, "science didn't come aboard");
            float worth = Lab.Preview(c, v, Science.Get("surface"), 0);
            return $"surface sample and EVA report taken on Tal and carried aboard (surface sample worth {Science.Get("surface").baseValue * Science.Multiplier(Tal, Situation.Landed):0} science)";
        }

        static string EvaFlags()
        {
            var u = CareerUniverse();
            var pilot = u.career.roster.First(m => m.role == CrewRole.Pilot);
            var v = LandedHopper(u, Tal, pilot);
            var eva = u.Add(v.GoEva(pilot.name, 0, out _));
            var flag = eva.PlantFlag(0, out string why);
            Check(flag != null, "no flag: " + why);
            u.Add(flag);
            double d = Vector3d.Distance(flag.pos, eva.pos);
            Check(flag.landed && flag.IsFlag && !flag.IsControllable && d > 0.5 && d < 3, $"flag {d:0.0} m away, landed={flag.landed}");
            var u2 = new Universe();
            u2.LoadJson(u.ToJson(), System4);
            var f2 = u2.vessels.FirstOrDefault(x => x.IsFlag);
            var e2 = u2.vessels.FirstOrDefault(x => x.IsEva);
            Check(f2 != null && f2.landed && f2.name == flag.name, "flag lost in the save");
            Check(e2 != null && e2.EvaName == pilot.name, "astronaut lost in the save");
            // Not in the air
            var orbit = u.Add(InOrbit(Hopper(), System4[0], System4[0].radius + 150_000, 0));
            Seat(orbit, u.career.roster.First(m => m.role == CrewRole.Engineer));
            var fly = u.Add(orbit.GoEva(orbit.parts.First(p => p.crew.Count > 0).crew[0], 0, out _));
            Check(fly.PlantFlag(0, out string w2) == null && w2.Contains("ground"), "planted a flag in orbit");
            return $"\"{flag.name}\" planted {d:0.0} m ahead; flag and astronaut survive the save";
        }

        static string LegsBreakAndRepair()
        {
            var u = CareerUniverse();
            VesselSim Drop(double speed)
            {
                var b = Tal;
                Vector3d up = CelestialBody.DirectionFromLatLon(-5, 30);
                var probe = VesselSim.CreateLanded(Hopper(), b, up, 0);
                var v = VesselSim.CreateFree(Hopper(), b, probe.pos + up * 3, probe.vel - up * speed, probe.rot);
                foreach (var p in v.parts) if (p.def.kind == PartKind.Leg) { p.legsDeployed = true; p.legDeploy = 1; }
                v.disableRails = true;
                double t = 0;
                for (int i = 0; i < 3000 && !v.landed && !v.destroyed; i++) { v.Step(0.01, t); t += 0.01; }   // (milestone 41: a soft landing settles for a moment before it parks)
                return v;
            }
            var hard = Drop(14);
            int broke = hard.parts.Count(p => p.broken);
            Check(!hard.destroyed && hard.landed && broke == 4, $"14 m/s: destroyed={hard.destroyed} ({hard.destroyReason}), landed={hard.landed}, broken legs {broke}");
            Check(hard.lastEvent != null && hard.lastEvent.Contains("gave way"), "no message about the legs");
            var crash = Drop(25);
            Check(crash.destroyed, "25 m/s onto legs rated 12 survived");
            var soft = Drop(5);
            Check(!soft.parts.Any(p => p.broken), "5 m/s broke legs");
            Check(soft.landed && !soft.destroyed, $"5 m/s didn't settle and park: landed={soft.landed} settling={soft.settling} {soft.destroyReason}");

            // Broken legs won't deploy; a pilot can't fix them, an engineer can
            hard.ToggleGroup(Groups.Gear);
            hard.Step(0.5, 0);
            Check(hard.parts.Where(p => p.def.kind == PartKind.Leg).All(p => p.legDeploy < 0.01f), "a broken leg deployed");
            u.Add(hard);
            var pilot = u.career.roster.First(m => m.role == CrewRole.Pilot);
            var eng = u.career.roster.First(m => m.role == CrewRole.Engineer);
            Seat(hard, pilot, eng);
            var pEva = u.Add(hard.GoEva(pilot.name, 0, out string w1) ?? throw new Exception(w1));
            var jobs = pEva.WorkInReach(u.vessels);
            Check(jobs.Count(j => j.p.broken) == 4, $"{jobs.Count(j => j.p.broken)} broken legs in reach of the astronaut");
            var leg = jobs.First(j => j.p.broken).p;
            Check(pEva.Repair(hard, leg).Contains("Only an engineer") && leg.broken, "a pilot fixed a leg");
            var eEva = u.Add(hard.GoEva(eng.name, 0, out string w2) ?? throw new Exception(w2));
            foreach (var (vv, p) in eEva.WorkInReach(u.vessels).Where(j => j.p.broken).ToList()) eEva.Repair(vv, p);
            Check(!hard.parts.Any(p => p.broken), "the engineer didn't fix every leg");
            for (int i = 0; i < 100; i++) hard.Step(0.02, i * 0.02); // the gear switch is down, so they come back down
            Check(hard.parts.Where(p => p.def.kind == PartKind.Leg).All(p => p.legDeploy > 0.99f), "fixed legs didn't deploy");
            return $"14 m/s broke {broke} legs (vessel fine), 25 m/s destroyed it; the engineer fixed them, the pilot couldn't";
        }

        static string ChuteRepack()
        {
            var u = CareerUniverse();
            var sci = u.career.roster.First(m => m.role == CrewRole.Scientist);
            var v = LandedHopper(u, System4[0], sci);
            var chute = v.parts.First(p => p.def.kind == PartKind.Parachute);
            chute.chuteDeployed = true;
            v.nextStage = v.stages.Count;
            var eva = u.Add(v.GoEva(sci.name, 0, out _));
            var jobs = eva.WorkInReach(u.vessels);
            Check(jobs.Any(j => j.p == chute), "the used parachute isn't within reach from the ground");
            string msg = eva.Repack(v, chute);
            Check(!chute.chuteDeployed && v.HasStagesLeft, $"repack: '{msg}', deployed={chute.chuteDeployed}, stages left={v.HasStagesLeft}");
            v.ActivateNextStage();
            Check(chute.chuteDeployed, "the repacked chute didn't open on the next stage");
            return "a scientist repacked the chute from the ground; the next stage opened it";
        }
    }
}
