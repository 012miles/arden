using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 5b tests: facilities, strategies, the astronaut roster, rescue and base contracts.</summary>
    public static partial class SimTests
    {
        static void RunMilestone5b()
        {
            Run("Facilities: pad mass, part count, research tier and contract limits; upgrades cost money and lift them", CareerFacilities);
            Run("Strategies: setup cost, slot limit, and the trades they make on rewards and launch prices", CareerStrategies);
            Run("Astronauts: roster seats capsules at launch, comes home on recovery, can be lost; hiring costs and is capped", CareerRoster);
            Run("Rescue: accepting puts a stranded capsule in orbit; recovering its pilot completes it and they join the corps", CareerRescue);
            Run("Bases: a landed vessel with seats, antenna and solar panel at the right spot completes the contract", CareerBase);
            Run("Space center state (facilities, strategies, roster) saves and loads", CareerCenterSave);
        }

        static void UnlockAll(Career c) { foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id); }

        static string CareerFacilities()
        {
            var c = NewCareer();
            UnlockAll(c);
            c.funds = 10_000_000;
            var heavy = VesselDesign.Longshot();
            double mass = Career.LaunchMass(heavy);
            string p = c.LaunchProblem(heavy);
            Check(p != null && p.Contains("too heavy"), $"a {mass:0} t rocket wasn't stopped by the level-1 pad: {p}");
            Check(c.LaunchProblem(VesselDesign.FirstFlight()) == null, "the first flight was blocked");
            var many = VesselDesign.Default();
            while (many.nodes.Count <= 30) many.nodes.Add(new PartNode { id = "light.s", parent = 0, attach = Attach.Surface });
            Check((c.LaunchProblem(many) ?? "").Contains("parts"), "31 parts weren't blocked by the Assembly Building");
            var t4 = TechTree.Nodes.First(n => n.tier == 4);
            var fresh = NewCareer();
            fresh.science = 10_000;
            foreach (var n in TechTree.Nodes.Where(n => n.tier < 4)) fresh.unlocked.Add(n.id);
            Check(!fresh.CanResearch(t4), "researched tier 4 with a level-1 lab");
            fresh.funds = 1_000_000;
            Check(fresh.Upgrade(Facility.Research) && fresh.CanResearch(t4), "lab upgrade didn't open tier 4");
            double before = c.funds;
            Check(c.Upgrade(Facility.Pad) && c.Upgrade(Facility.Pad) && !c.Upgrade(Facility.Pad), "pad didn't upgrade exactly twice");
            Check(Math.Abs(before - c.funds - (60_000 + 320_000)) < 1e-6, "upgrades charged the wrong amount");
            Check(c.LaunchProblem(heavy) == null, $"still blocked after upgrading the pad: {c.LaunchProblem(heavy)}");
            // Mission Control: three active contracts at level 1
            var mc = NewCareer();
            for (int i = 0; i < 5; i++) mc.contracts.Add(new Contract { id = 900 + i, type = "test", title = $"t{i}", state = ContractState.Offered, deadline = 1e6 });
            int ok = 0;
            foreach (var k in mc.contracts.Where(k => k.state == ContractState.Offered).ToList()) if (mc.Accept(k, 0)) ok++;
            Check(ok == 3, $"accepted {ok} contracts with a level-1 Mission Control (limit 3)");
            return $"Longshot {mass:0} t blocked at 20 t, flies after two pad upgrades (380,000); 31 parts blocked; tier 4 needs a lab upgrade; 3 contracts at once";
        }

        static string CareerStrategies()
        {
            var c = NewCareer();
            c.funds = 100_000;
            var outsourced = Career.Strategies.First(s => s.id == "outsourced");
            var patents = Career.Strategies.First(s => s.id == "patents");
            Check(c.Activate(outsourced) && Math.Abs(c.funds - 85_000) < 1e-6, "setup cost wasn't charged");
            Check(!c.Activate(patents), "a second strategy started with one Administration slot");
            double f0 = c.funds, s0 = c.science;
            var (pf, ps, pr) = c.Pay(10_000, 0, 2);
            Check(Math.Abs(pf - 7_500) < 1e-6 && Math.Abs(ps - 2.5) < 1e-6, $"outsourced paid {pf:N0} funds and {ps:0.0} science");
            c.Deactivate(outsourced);
            c.facility[(int)Facility.Admin] = 1;
            Check(c.Activate(patents), "patents didn't start");
            (pf, ps, pr) = c.Pay(0, 4, 0);
            Check(Math.Abs(pf - 1_000) < 1e-6 && Math.Abs(ps - 3) < 1e-6, $"patents turned 4 science into {pf:N0} funds + {ps:0.0} science");
            var bulk = Career.Strategies.First(s => s.id == "bulk");
            var d = VesselDesign.FirstFlight();
            double full = c.LaunchCost(d);
            Check(c.Activate(bulk) && Math.Abs(c.LaunchCost(d) - 0.8 * full) < 1e-6, "bulk purchasing didn't cut the launch price by 20%");
            return $"outsourced: 10,000 -> 7,500 + 2.5 science; patents: 4 science -> 3 + 1,000 funds; bulk: launch {full:N0} -> {c.LaunchCost(d):N0}";
        }

        static string CareerRoster()
        {
            var home = System4[0];
            var c = NewCareer();
            Check(c.roster.Count == 4 && c.Available.Count() == 4, $"started with {c.roster.Count} astronauts");
            var v = VesselSim.CreateOnPad(VesselDesign.FirstFlight(), home, 0);
            v.id = 7;
            int flew = c.AssignCrew(v);
            string pilot = v.parts.SelectMany(p => p.crew).FirstOrDefault();
            Check(flew == 1 && pilot != null && c.roster.Find(m => m.name == pilot).state == 1, "the capsule wasn't crewed from the roster");
            Check(c.Available.Count() == 3, "the pilot is still listed as available");
            c.Recover(v, 0, System4);
            var m0 = c.roster.Find(m => m.name == pilot);
            Check(m0.state == 0 && m0.flights == 1, "the pilot didn't come home with a flight logged");
            var w = VesselSim.CreateOnPad(VesselDesign.FirstFlight(), home, 0);
            c.AssignCrew(w);
            string lostName = w.parts.SelectMany(p => p.crew).First();
            c.reputation = 20;
            c.OnVesselLost(w);
            Check(c.roster.Find(m => m.name == lostName).state == 2 && Math.Abs(c.reputation - 15) < 1e-6, "losing a crewed vessel didn't lose the pilot and 5 reputation");
            c.funds = 1_000_000;
            double cost1 = c.HireCost;
            string h1 = c.Hire();
            Check(h1.StartsWith("Hired") && c.LivingCrew == 4, $"first hire: {h1} ({c.LivingCrew} living)");
            Check(c.HireCost > cost1, "hiring didn't get more expensive");
            while (c.Hire().StartsWith("Hired")) { }
            Check(c.Hire().Contains("holds"), "no message about the Astronaut Complex cap");
            Check(c.LivingCrew == c.MaxRoster, $"{c.LivingCrew} living astronauts with a cap of {c.MaxRoster}");
            var empty = NewCareer();
            foreach (var m in empty.roster) m.state = 2;
            Check(empty.CrewNote(VesselDesign.FirstFlight()) != null, "no warning when nobody can fly");
            return $"pilot {pilot} flew and came home; {lostName} lost (-5 rep); first hire {cost1:N0}; roster capped at {c.MaxRoster}";
        }

        static string CareerRescue()
        {
            var home = System4[0];
            var c = NewCareer();
            var k = new Contract { id = 77, type = "rescue", title = "Rescue Test Pilot", body = "Arden", crewName = "Test Pilot", altitude = 120_000, reward = 60_000, advance = 12_000, repReward = 15, state = ContractState.Offered, deadline = 1e7 };
            c.contracts.Add(k);
            Check(c.Accept(k, 0) && c.PendingRescues.Contains(k), "rescue didn't wait to be spawned");
            var u = new Universe();
            u.career.Load(c.ToSave());
            var uk = u.career.contracts.First(x => x.id == 77);
            u.SpawnRescues(System4);
            var stranded = u.Find(uk.vesselId);
            Check(stranded != null && stranded.CrewCount == 1 && stranded.parts.Any(p => p.crew.Contains("Test Pilot")), "no stranded capsule with the pilot aboard");
            Check(stranded.FreePorts().Any(), "the stranded capsule has no free docking port");
            Check(Math.Abs(stranded.Altitude - 120_000) < 10 && stranded.Situation(0).StartsWith("Orbiting"), $"capsule at {stranded.Altitude / 1000:0} km: {stranded.Situation(0)}");
            u.SpawnRescues(System4);
            Check(u.vessels.Count == 1, "spawned the capsule twice");
            // Pretend we docked, moved the pilot into our ship and flew home.
            var ship = VesselSim.CreateOnPad(VesselDesign.FerryCraft(), home, 0);
            ship.parts.First(p => p.def.id == "pod.wren").crew.Clear();
            ship.parts.First(p => p.def.id == "pod.wren").crew.Add("Test Pilot");
            double f0 = u.career.funds;
            string msg = u.career.Recover(ship, 0, System4);
            Check(uk.state == ContractState.Completed, $"recovering the pilot didn't complete the rescue: {msg}");
            Check(u.career.roster.Exists(m => m.name == "Test Pilot" && m.state == 0), "the rescued pilot didn't join the corps");
            return $"stranded capsule at {stranded.Altitude / 1000:0} km with a docking port; recovered: {msg}";
        }

        static string CareerBase()
        {
            var home = System4[0];
            var c = NewCareer();
            var k = new Contract { id = 88, type = "base", title = "Outpost", body = "Arden", lat = 10, lon = 50, radiusKm = 10, seats = 2, reward = 70_000, repReward = 12, state = ContractState.Active, deadline = 1e9 };
            c.contracts.Add(k);
            VesselSim BaseAt(double lat, double lon, bool solar)
            {
                var d = D("base", "pod.wren", "hab.s", "tank.s1");
                int t = d.nodes.FindIndex(n => n.id == "tank.s1");
                DesignOps.AttachSurfaceAt(d, t, 0.5f, 0f, "antenna.s", 1, false);
                if (solar) DesignOps.AttachSurfaceAt(d, t, 0.5f, 90f, "solar.s", 1, false);
                Vector3d dir = home.FixedToInertial(CelestialBody.DirectionFromLatLon(lat, lon), 0);
                var v = VesselSim.CreateFree(d, home, dir * (home.radius + 2), Vector3d.zero, Quaternion.FromToRotation(Vector3.up, dir.ToFloat()));
                v.landed = true;
                return v;
            }
            c.CheckBases(new[] { BaseAt(10, 50, false) }, 0);
            Check(k.state == ContractState.Active, "completed without a solar panel");
            c.CheckBases(new[] { BaseAt(10, 51, true) }, 0);
            Check(k.state == ContractState.Active, "completed 110 km from the spot");
            c.CheckBases(new[] { BaseAt(10.03, 50.02, true) }, 0);
            Check(k.state == ContractState.Completed, "the right base at the right spot didn't complete it");
            return "needs seats + antenna + solar panel, within 10 km of 10.0, 50.0";
        }

        static string CareerCenterSave()
        {
            var c = NewCareer();
            c.funds = 1_000_000;
            c.Upgrade(Facility.Pad); c.Upgrade(Facility.Admin); c.Upgrade(Facility.Admin);
            c.Activate(Career.Strategies[0]); c.Activate(Career.Strategies[2]);
            c.Hire();
            c.roster[0].state = 2;
            var u = new Universe();
            u.career.Load(c.ToSave());
            var u2 = new Universe();
            u2.LoadJson(u.ToJson(), System4);
            var k = u2.career;
            Check(k.Level(Facility.Pad) == 1 && k.Level(Facility.Admin) == 2, "facility levels lost");
            Check(k.strategies.SetEquals(c.strategies), "strategies lost");
            Check(k.roster.Count == c.roster.Count && k.roster[0].state == 2 && k.roster.Select(m => m.name).SequenceEqual(c.roster.Select(m => m.name)), "roster lost");
            var sb = new Career();
            sb.Start(GameMode.Sandbox);
            Check(sb.Level(Facility.Pad) == Facilities.MaxLevel && sb.LaunchProblem(VesselDesign.Longshot()) == null, "sandbox facilities aren't maxed");
            return $"pad L{k.Level(Facility.Pad) + 1}, admin L{k.Level(Facility.Admin) + 1}, {k.strategies.Count} strategies, {k.roster.Count} astronauts survived the save";
        }
    }
}
