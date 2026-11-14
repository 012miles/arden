using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 7a tests: crew roles, experience, and what pilots, engineers and scientists do.</summary>
    public static partial class SimTests
    {
        static void RunMilestone7a()
        {
            Run("Experience: coming home from space, orbit, Tal, Tal orbit and Tal's surface earns levels 1 to 5", CrewExperience);
            Run("Seating: pilot first, then engineer and scientist; builder picks win; sandbox hires trained crew", CrewSeating);
            Run("Pilots: SAS modes grow with the pilot's level; probe cores hold their own set; scientists only hold", PilotSas);
            Run("Engineers: more drill and converter output for the same power", EngineerMining);
            Run("Scientists: send more of a result home and clean used sample canisters", ScientistScience);
            Run("Roles, experience and used canisters survive save/load; old careers get roles", CrewSave);
        }

        static Universe CareerUniverse()
        {
            var u = new Universe();
            u.career.Start(GameMode.Career);
            return u;
        }

        static CrewMember Member(Career c, CrewRole role, int log = 0)
        {
            var m = new CrewMember { name = $"Test {role} {c.roster.Count}", role = role, log = log };
            c.roster.Add(m);
            return m;
        }

        static void Seat(VesselSim v, params CrewMember[] crew)
        {
            foreach (var p in v.parts) p.crew.Clear();
            int k = 0;
            foreach (var p in v.parts) while (p.def.crew > p.crew.Count && k < crew.Length) { crew[k].state = 1; p.crew.Add(crew[k++].name); }
            v.RecomputeMass();
        }

        static string CrewExperience()
        {
            int[] steps = { VFlags.Space, VFlags.OrbitHome, VFlags.SoiTal, VFlags.OrbitTal, VFlags.LandedTal };
            int log = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                log |= steps[i];
                Check(Crew.LevelOf(Crew.XpOf(log)) == i + 1, $"after {i + 1} new places the level is {Crew.LevelOf(Crew.XpOf(log))}");
            }
            Check(Crew.LevelOf(Crew.XpOf(VFlags.Launched | VFlags.Docked)) == 0, "launching and docking gave experience");

            var u = CareerUniverse();
            var c = u.career;
            var pilot = c.roster.First(m => m.role == CrewRole.Pilot);
            var v = u.Add(InOrbit(D("cap", "chute.s", "pod.wren"), System4[0], System4[0].radius + 100_000, 0));
            Seat(v, pilot);
            v.careerFlags = VFlags.Launched | VFlags.Space | VFlags.OrbitHome;
            c.news.Clear();
            c.Recover(v, 0, System4);
            Check(pilot.state == 0 && pilot.flights == 1 && pilot.Level == 2 && pilot.Xp == 3, $"after an orbital flight: level {pilot.Level}, xp {pilot.Xp}, flights {pilot.flights}");
            Check(c.news.Any(n => n.Contains("level 2 pilot")), "no news of the promotion: " + string.Join(" | ", c.news));
            // Coming home from the same places again doesn't add more
            var v2 = u.Add(InOrbit(D("cap", "chute.s", "pod.wren"), System4[0], System4[0].radius + 100_000, 0));
            Seat(v2, pilot);
            v2.careerFlags = VFlags.Space;
            c.Recover(v2, 0, System4);
            Check(pilot.Level == 2 && pilot.flights == 2, "a repeat flight changed the level");
            return $"levels at {string.Join(", ", Crew.Thresholds)} XP; orbital flight took a pilot to level {pilot.Level}";
        }

        static string CrewSeating()
        {
            var u = CareerUniverse();
            var c = u.career;
            Check(c.roster.Select(m => m.role).SequenceEqual(new[] { CrewRole.Pilot, CrewRole.Engineer, CrewRole.Scientist, CrewRole.Pilot }), "starting roster isn't pilot, engineer, scientist, pilot");
            var d = D("three", "pod.wren", "pod.wren", "pod.wren");
            var v = u.Add(VesselSim.CreateOnPad(d, System4[0], 0));
            Check(c.AssignCrew(v) == 3, "three seats not filled");
            var roles = v.CrewAboard().Select(m => m.role).ToList();
            Check(roles.SequenceEqual(new[] { CrewRole.Pilot, CrewRole.Engineer, CrewRole.Scientist }), "seated " + string.Join(", ", roles));
            c.Recover(v, 0, System4);

            // The builder's pick goes where it's asked
            var sci = c.roster.First(m => m.role == CrewRole.Scientist);
            c.crewPlan.Clear();
            c.crewPlan.Add(sci.name);
            var w = u.Add(VesselSim.CreateOnPad(D("one", "chute.s", "pod.wren"), System4[0], 0));
            c.AssignCrew(w);
            Check(w.CrewAboard()[0].name == sci.name, "the builder's pick didn't fly");
            Check(c.CrewNote(D("one", "chute.s", "pod.wren")) == null, "note while a pilot is free");
            c.Recover(w, 0, System4);
            c.crewPlan.Clear();

            // No pilot free: the builder says so (unless a probe core is aboard)
            foreach (var m in c.roster) if (m.role == CrewRole.Pilot) m.state = 2;
            Check((c.CrewNote(D("one", "chute.s", "pod.wren")) ?? "").Contains("No pilot"), "no 'no pilot' note");
            Check(c.CrewNote(D("one", "chute.s", "pod.wren", "probe.spark")) == null, "'no pilot' note with a probe core aboard");

            // Hiring picks the role
            c.funds = 1e6;
            c.Hire(CrewRole.Engineer);
            Check(c.roster.Last().role == CrewRole.Engineer && c.roster.Last().Level == 0, "hired engineer has the wrong role or level");

            // Sandbox: launches are crewed by fully trained astronauts, habitats included
            var s = new Universe();
            s.career.Start(GameMode.Sandbox);
            var sv = s.Add(VesselSim.CreateOnPad(VesselDesign.Outpost(), System4[0], 0));
            int flew = s.career.AssignCrew(sv);
            Check(flew == 4 && sv.CrewAboard().All(m => m.Level == Crew.MaxLevel), $"sandbox outpost: {flew} aboard, levels {string.Join(",", sv.CrewAboard().Select(m => m.Level))}");
            Check(sv.CrewAboard()[0].role == CrewRole.Pilot, "sandbox's first seat isn't a pilot");
            return $"career seats pilot/engineer/scientist; builder pick flies; sandbox outpost launched with {flew} level-5 astronauts";
        }

        static string PilotSas()
        {
            var u = CareerUniverse();
            var c = u.career;
            var home = System4[0];
            VesselSim Capsule(params CrewMember[] crew) { var v = u.Add(InOrbit(D("cap", "chute.s", "pod.wren", "hab.s"), home, home.radius + 100_000, 0)); Seat(v, crew); return v; }
            var sciOnly = Capsule(Member(c, CrewRole.Scientist, VFlags.Space | VFlags.OrbitHome | VFlags.SoiTal));
            Check(sciOnly.SasTier == 0 && sciOnly.CanHold(SasMode.Stability) && !sciOnly.CanHold(SasMode.Prograde), $"scientist-only capsule tier {sciOnly.SasTier}");
            var rookie = Capsule(Member(c, CrewRole.Pilot));
            Check(rookie.SasTier == 1 && rookie.CanHold(SasMode.Retrograde) && !rookie.CanHold(SasMode.Normal), $"level-0 pilot tier {rookie.SasTier}");
            var vet = Capsule(Member(c, CrewRole.Pilot, VFlags.Space | VFlags.OrbitHome | VFlags.SoiTal));
            Check(vet.SasTier == 4 && vet.CanHold(SasMode.Maneuver), $"level-3 pilot tier {vet.SasTier}");
            var lvl2 = Capsule(Member(c, CrewRole.Pilot, VFlags.Space | VFlags.SoiTal));
            Check(lvl2.SasTier == 3 && lvl2.CanHold(SasMode.Target) && !lvl2.CanHold(SasMode.Maneuver), $"level-2 pilot tier {lvl2.SasTier}");
            // The pilot moving into the habitat still counts
            var spark = u.Add(InOrbit(D("probe", "probe.spark", "tank.s1"), home, home.radius + 100_000, 0));
            Check(spark.SasTier == 2 && !spark.CanHold(SasMode.Target), $"Spark tier {spark.SasTier}");
            foreach (var p in spark.parts) p.charge = 0;
            spark.RecomputeMass();
            Check(spark.SasTier == -1, "a flat probe still had SAS");
            var vireo = u.Add(InOrbit(D("probe", "probe.vireo", "tank.s1"), home, home.radius + 100_000, 0));
            Check(vireo.SasTier == 4, $"Vireo tier {vireo.SasTier}");
            Check(TechTree.NodeOf("probe.vireo") != null && Prices.Of(PartCatalog.Get("probe.vireo")) > 1000, "Vireo not researchable or priced");
            // Crew the roster doesn't know (old saves): rookie pilots in a career, veterans in sandbox
            var old = Capsule();
            old.parts.First(p => p.def.id == "pod.wren").crew.Add("Unknown Person");
            old.RecomputeMass();
            Check(old.SasTier == 1, $"unknown crew in a career: tier {old.SasTier}");
            var s = new Universe();
            s.career.Start(GameMode.Sandbox);
            var sold = s.Add(InOrbit(D("cap", "chute.s", "pod.wren"), home, home.radius + 100_000, 0));
            Check(sold.SasTier == 4, $"unknown crew in sandbox: tier {sold.SasTier}");
            return "scientist: hold; pilot L0 pro/retro, L2 target, L3 everything; Spark normal/radial, Vireo everything";
        }

        static string EngineerMining()
        {
            var home = System4[0];
            double Run(CrewMember who, out double ec)
            {
                var u = CareerUniverse();
                if (who != null) u.career.roster.Add(who);
                var v = u.Add(InOrbit(D("refinery", "pod.wren", "battery.s", "isru.s", "ore.s", "tank.s4"), home, home.radius + 300_000, 0));
                Seat(v, who != null ? new[] { who } : new CrewMember[0]);
                Overcharge(v);
                foreach (var p in v.parts) { if (p.def.oreCapacity > 0) p.ore = 1.0; if (p.def.kind == PartKind.Tank) p.propellant = 0; }
                v.SetConverter(v.parts.First(p => p.def.id == "isru.s"), 1);
                double t = 0;
                for (int i = 0; i < 10; i++) { v.BackgroundAdvance(10, t, false); t += 10; }
                ec = v.miningEc;
                return 1.0 - v.OreStored;
            }
            double none = Run(null, out double ecNone);
            double rookie = Run(new CrewMember { name = "Eng A", role = CrewRole.Engineer }, out double ecRookie);
            double vet = Run(new CrewMember { name = "Eng B", role = CrewRole.Engineer, log = VFlags.Space | VFlags.SoiTal }, out _);
            double pilot = Run(new CrewMember { name = "Pil C", role = CrewRole.Pilot, log = Crew.XpMask }, out _);
            Check(Math.Abs(none - 0.2) < 0.005, $"no engineer: {none:0.000} t of ore in 100 s (expected 0.2)");
            Check(Math.Abs(rookie / none - 1.2) < 0.01 && Math.Abs(vet / none - 1.4) < 0.01, $"engineers: L0 x{rookie / none:0.00}, L2 x{vet / none:0.00}");
            Check(Math.Abs(pilot - none) < 1e-9, "a pilot sped up the converter");
            Check(Math.Abs(ecRookie - ecNone) < 1e-9, "the engineer changed the power draw");
            return $"converter 100 s: {none:0.000} t alone, {rookie:0.000} t with a new engineer, {vet:0.000} t with a level-2 one";
        }

        static string ScientistScience()
        {
            var u = CareerUniverse();
            var c = u.career;
            var home = System4[0];
            var d = D("lab", "chute.s", "pod.wren", "tank.s1");
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s1"), 0.5f, 0f, "sci.bio", 1, false);
            var v = u.Add(InOrbit(d, home, home.radius + 100_000, 0));
            var pilot = Member(c, CrewRole.Pilot);
            Seat(v, pilot);
            var can = v.parts.First(p => p.def.id == "sci.bio");
            var exp = Science.Get("samples");
            Lab.Run(c, v, can, exp, 0);
            var data = Lab.Held(can, exp);
            Check(data != null && Math.Abs(Lab.TransmitFactor(v, data) - 0.3f) < 1e-5f, "without a scientist, samples send at 30%");
            Lab.Discard(can, data);
            Check(can.used && Lab.Run(c, v, can, exp, 0).Contains("used"), "a discarded canister ran again");
            Check(Lab.Reset(v, can).Contains("scientist") && can.used, "cleaned without a scientist");
            var sci = Member(c, CrewRole.Scientist, VFlags.Space | VFlags.OrbitHome); // level 2
            Seat(v, sci);
            Check(!Lab.Reset(v, can).Contains("Only") && !can.used, "the scientist couldn't clean it");
            Lab.Run(c, v, can, exp, 0);
            data = Lab.Held(can, exp);
            float f = Lab.TransmitFactor(v, data);
            Check(Math.Abs(f - (0.3f + 0.10f + 0.05f * 2)) < 1e-5f, $"level-2 scientist sends samples at {f:P0}");
            v.connected = true;
            string msg = Lab.Transmit(c, v, can, data, System4);
            Check(msg.StartsWith("Sent") && can.used, $"transmit: '{msg}', used={can.used}");
            // Instruments that aren't single-use stay ready
            Check(Science.Get("temp").singleUse == false, "the thermometer became single-use");
            return $"samples send at 30% alone, {f * 100:0}% with a level-2 scientist; used canisters need a scientist to clean";
        }

        static string CrewSave()
        {
            var u = CareerUniverse();
            var c = u.career;
            c.roster[1].log = VFlags.Space | VFlags.OrbitHome;
            var d = D("lab", "chute.s", "pod.wren", "tank.s1");
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s1"), 0.5f, 0f, "sci.bio", 1, false);
            var v = u.Add(InOrbit(d, System4[0], System4[0].radius + 100_000, 0));
            v.parts.First(p => p.def.id == "sci.bio").used = true;
            string json = u.ToJson();
            var u2 = new Universe();
            u2.LoadJson(json, System4);
            Check(u2.career.roster.Select(m => m.role).SequenceEqual(c.roster.Select(m => m.role)) && u2.career.roster[1].Level == 2, "roles or experience changed in the save");
            Check(u2.vessels[0].parts.First(p => p.def.id == "sci.bio").used && u2.vessels[0].career == u2.career, "used flag or roster link lost");
            // A career saved before roles: all pilots in the file, roles handed out on load
            string old = json.Replace("\"version\":2", "\"version\":1");
            foreach (var r in new[] { "\"role\":1", "\"role\":2" }) old = old.Replace(r, "\"role\":0");
            var u3 = new Universe();
            u3.LoadJson(old, System4);
            Check(u3.career.roster.Take(4).Select(m => m.role).SequenceEqual(new[] { CrewRole.Pilot, CrewRole.Engineer, CrewRole.Scientist, CrewRole.Pilot }), "old career didn't get roles");
            return "roles, levels, used canisters and the roster link round-trip; pre-role careers get pilot/engineer/scientist/pilot";
        }
    }
}
