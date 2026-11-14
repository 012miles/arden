using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 6b tests: ore, survey scanning, drills, converters, machine heat and radiators.</summary>
    public static partial class SimTests
    {
        static void RunMilestone6b()
        {
            Run("Ore: deposits are patchy, none under Arden's seas, richer on Tal", OreMapShape);
            Run("Survey: a polar orbit maps all of Tal as it turns; equatorial only a band; folded or too high maps nothing", SurveyScanning);
            Run("Drills: landed, unfolded and reaching the ground they fill the ore tanks at rate x ore %; otherwise nothing", DrillingRates);
            Run("Converters: ore into fuel (80%) or monopropellant (60%), anywhere; stop when out of ore or tanks are full", Refining);
            Run("Heat: a converter alone runs hot and slow; a radiator keeps it at full speed; warp settles to the same", HeatAndRadiators);
            Run("Power: machines share what the batteries can give, in small steps or one big warp step", MiningPower);
            Run("Mining state and the survey map survive save/load; old saves load empty and cold", MiningSave);
            Run("Career: ore records, survey and mining contracts; Prospector preset can reach Tal", MiningCareer);
        }

        static CelestialBody Tal => System4[1];

        /// <summary>Gives the vessel effectively unlimited electricity (for tests about something else).</summary>
        static void Overcharge(VesselSim v) { foreach (var p in v.parts) if (p.def.ecCapacity > 0) p.charge = 1e7; }

        static void Unfold(VesselSim v, PartKind kind) { foreach (var p in v.parts) if (p.def.kind == kind) { p.legsDeployed = true; p.legDeploy = 1; } }

        /// <summary>The richest sunlit spot on a body at ut = 0 (planet-fixed = inertial then), on a 2-degree grid.</summary>
        static Vector3d RichSpot(CelestialBody b, out double conc)
        {
            conc = -1;
            var best = Vector3d.up;
            for (int la = -76; la <= 76; la += 2)
                for (int lo = -180; lo < 180; lo += 2)
                {
                    var d = CelestialBody.DirectionFromLatLon(la, lo);
                    if (Vector3d.Dot(d, Sun.DirectionAt(0)) < 0.35) continue;
                    double c = Ore.Concentration(b, d);
                    if (c > conc) { conc = c; best = d; }
                }
            return best;
        }

        static string OreMapShape()
        {
            var rng = new System.Random(7);
            var home = System4[0];
            int n = 4000, talOre = 0, ardenOre = 0, ardenSea = 0, seaOre = 0;
            double talMax = 0, ardenMax = 0;
            for (int i = 0; i < n; i++)
            {
                var d = new Vector3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
                if (d.sqrMagnitude < 1e-4 || d.sqrMagnitude > 1) { i--; continue; }
                d = d.normalized;
                double ct = Ore.Concentration(Tal, d), ca = Ore.Concentration(home, d);
                if (ct > 0) talOre++;
                if (ca > 0) ardenOre++;
                talMax = Math.Max(talMax, ct);
                ardenMax = Math.Max(ardenMax, ca);
                float h = (Noise.Fbm(d.ToFloat() * 1.8f, 6, home.seed) - 0.5f) * 2.4f;
                if (h < 0) { ardenSea++; if (ca > 0) seaOre++; }
                Check(Ore.Concentration(Tal, d) == ct, "the ore map isn't repeatable");
            }
            double tf = (double)talOre / n, af = (double)ardenOre / n;
            Check(tf > 0.2 && tf < 0.7, $"ore covers {tf * 100:0}% of Tal (expected patchy: 20-70%)");
            Check(af > 0.05 && af < 0.5, $"ore covers {af * 100:0}% of Arden (expected 5-50%)");
            Check(seaOre == 0 && ardenSea > n / 10, $"{seaOre} of {ardenSea} sea samples had ore");
            Check(talMax > 0.1 && talMax < 0.3, $"Tal's richest sample {talMax:P1}");
            Check(ardenMax > 0.02 && ardenMax <= 0.1 + 1e-9, $"Arden's richest sample {ardenMax:P1}");
            Check(talMax > ardenMax, "Arden is richer than Tal");
            return $"Tal: ore on {tf * 100:0}%, up to {talMax * 100:0.0}%; Arden: {af * 100:0}% (none at sea), up to {ardenMax * 100:0.0}%";
        }

        static VesselSim Scanner(Vector3d pos, Vector3d vel, bool unfold, OreSurvey s)
        {
            var d = D("mapper", "probe.spark", "battery.s", "tank.s1");
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(x => x.id == "tank.s1"), 0.5f, 0f, "scan.orbit", 1, false);
            var v = VesselSim.CreateFree(d, Tal, pos, vel, Quaternion.identity);
            v.survey = s;
            if (unfold) Unfold(v, PartKind.Scanner);
            Overcharge(v);
            return v;
        }

        static double RunScan(VesselSim v, double hours)
        {
            double t = 0;
            while (t < hours * 3600 - 1e-6) { v.BackgroundAdvance(600, t, false); t += 600; }
            Check(!v.destroyed, v.destroyReason);
            return v.survey.Coverage(Tal);
        }

        static string SurveyScanning()
        {
            double r = Tal.radius + 50_000, sp = Math.Sqrt(Tal.mu / r);
            var polarS = new OreSurvey();
            var polar = Scanner(new Vector3d(r, 0, 0), new Vector3d(0, sp, 0), true, polarS);
            double c3 = RunScan(polar, 3);
            Check(c3 > 0.2 && c3 < 0.5, $"3 h of polar orbit mapped {c3 * 100:0}% (expected about a third)");
            double c20 = c3;
            double t = 3 * 3600;
            while (t < 20 * 3600) { polar.BackgroundAdvance(600, t, false); t += 600; }
            c20 = polarS.Coverage(Tal);
            Check(c20 > 0.95, $"20 h of polar orbit mapped only {c20 * 100:0}%");
            Check(polar.scanNote.StartsWith("Mapping"), $"scanner note: {polar.scanNote}");

            var eqS = new OreSurvey();
            double ce = RunScan(Scanner(new Vector3d(r, 0, 0), new Vector3d(0, 0, -sp), true, eqS), 20);
            Check(ce > 0.1 && ce < 0.32, $"an equatorial orbit mapped {ce * 100:0}% (expected a band, ~20%)");

            var foldS = new OreSurvey();
            var folded = Scanner(new Vector3d(r, 0, 0), new Vector3d(0, sp, 0), false, foldS);
            Check(RunScan(folded, 3) == 0 && folded.scanNote.Contains("Unfold"), "a folded scanner mapped ground");

            double rHigh = Tal.radius + 700_000;
            var highS = new OreSurvey();
            var high = Scanner(new Vector3d(rHigh, 0, 0), new Vector3d(0, Math.Sqrt(Tal.mu / rHigh), 0), true, highS);
            Check(RunScan(high, 3) == 0 && high.scanNote.Contains("Too high"), $"mapped from 700 km ({high.scanNote})");

            // A spot inside a mapped band reads as mapped, the survey has a version that moves on
            Check(polarS.IsScanned(Tal, CelestialBody.DirectionFromLatLon(10, 20)) && polarS.version > 0, "mapped spot not marked");
            return $"polar: {c3 * 100:0}% after 3 h, {c20 * 100:0}% after 20 h; equatorial {ce * 100:0}%; folded / too high: 0";
        }

        static string DrillingRates()
        {
            var spot = RichSpot(Tal, out double c);
            Check(c > 0.06, $"richest sunlit spot only {c:P1}");
            var v = VesselSim.CreateLanded(VesselDesign.Lander(), Tal, spot, 0);
            Overcharge(v);
            var drills = v.parts.FindAll(p => p.def.id == "drill.s");
            Check(drills.Count == 2, "lander should have two drills");
            foreach (var d in drills) Check(v.DrillTipHeight(d, 0) < 0.5, $"drill bit ends {v.DrillTipHeight(d, 0):0.00} m above the ground");

            // Folded: nothing
            v.BackgroundAdvance(10, 0, false);
            Check(v.OreStored == 0 && v.drillNote.Contains("Unfold"), $"folded drills dug ore ({v.drillNote})");

            Unfold(v, PartKind.Drill);
            double t = 10;
            for (int i = 0; i < 12; i++) { v.BackgroundAdvance(10, t, false); t += 10; }
            double expected = 2 * 0.0015 * (c / Ore.ReferenceConc) * 120;
            Check(Math.Abs(v.OreStored - expected) < expected * 0.02, $"dug {v.OreStored:0.000} t in 120 s, expected {expected:0.000}");
            Check(v.drillNote == "Drilling" && v.coreTemp < Ore.HotStart, $"drill note '{v.drillNote}', core {v.coreTemp:0} K");
            Check((v.careerFlags & VFlags.Mined) != 0, "mining didn't set the Mined flag");

            // Full tanks: stops, and stops drawing power
            foreach (var p in v.parts) p.ore = p.def.oreCapacity;
            v.BackgroundAdvance(10, t, false);
            Check(v.oreRate == 0 && v.miningEc == 0 && v.drillNote.Contains("full"), $"full tanks: rate {v.oreRate}, EC {v.miningEc}, '{v.drillNote}'");

            // In orbit: nothing
            var o = InOrbit(VesselDesign.Lander(), Tal, Tal.radius + 30_000, 0);
            Unfold(o, PartKind.Drill);
            Overcharge(o);
            o.BackgroundAdvance(10, 0, false);
            Check(o.OreStored == 0 && o.drillNote.Contains("Land"), $"drilled in orbit ({o.drillNote})");

            // A drill mounted too high can't reach
            var hd = VesselDesign.Lander("high");
            DesignOps.Remove(hd, Enumerable.Range(0, hd.nodes.Count).Where(i => hd.nodes[i].id == "drill.s").ToList());
            DesignOps.AttachSurfaceAt(hd, hd.nodes.FindIndex(n => n.id == "isru.s"), 0.5f, 45f, "drill.s", 1, false);
            var h = VesselSim.CreateLanded(hd, Tal, spot, 0);
            Unfold(h, PartKind.Drill);
            Overcharge(h);
            h.BackgroundAdvance(10, 0, false);
            Check(h.OreStored == 0 && h.drillNote.Contains("reach"), $"high drill: '{h.drillNote}'");
            return $"{c * 100:0.0}% ore: 2 drills dug {expected:0.000} t in 120 s ({expected * 30:0.0} t/h); folded, in orbit, too high, full: nothing";
        }

        static string Refining()
        {
            var home = System4[0];
            var v = InOrbit(D("refinery", "probe.spark", "battery.s", "isru.s", "ore.s", "tank.s4"), home, home.radius + 300_000, 0);
            Overcharge(v);
            foreach (var p in v.parts) { if (p.def.oreCapacity > 0) p.ore = 1.0; if (p.def.kind == PartKind.Tank) p.propellant = 0.5; }
            var conv = v.parts.First(p => p.def.id == "isru.s");
            v.BackgroundAdvance(10, 0, false);
            Check(Math.Abs(v.OreStored - 1.0) < 1e-12 && v.convNotes[conv.index] == "Off", "a converter that's off refined ore");
            v.SetConverter(conv, 1);
            double t = 10, fuel0 = v.parts.Sum(p => p.propellant);
            for (int i = 0; i < 30; i++) { v.BackgroundAdvance(10, t, false); t += 10; }
            double used = 1.0 - v.OreStored, made = v.parts.Sum(p => p.propellant) - fuel0;
            Check(Math.Abs(used - 0.002 * 300) < 0.002 * 300 * 0.03, $"used {used:0.000} t of ore in 300 s (expected 0.6)");
            Check(Math.Abs(made - used * Ore.FuelYield) < 1e-6, $"made {made:0.0000} t of fuel from {used:0.0000} t of ore");
            Check((v.careerFlags & VFlags.Refined) != 0, "refining didn't set the Refined flag");
            for (int i = 0; i < 40; i++) { v.BackgroundAdvance(10, t, false); t += 10; }
            Check(v.OreStored < 1e-9 && v.convNotes[conv.index].Contains("ore"), $"out of ore: {v.OreStored:0.000} t left, '{v.convNotes[conv.index]}'");

            // Monopropellant, and full tanks
            var m = InOrbit(D("monofab", "probe.spark", "battery.s", "isru.s", "ore.s", "mono.s"), home, home.radius + 300_000, 0);
            Overcharge(m);
            foreach (var p in m.parts) { if (p.def.oreCapacity > 0) p.ore = 1.0; if (p.def.mono > 0) p.mono = 0; }
            var mc = m.parts.First(p => p.def.id == "isru.s");
            m.SetConverter(mc, 2);
            t = 0;
            for (int i = 0; i < 10; i++) { m.BackgroundAdvance(10, t, false); t += 10; }
            double mono = m.MonoLeft, oreUsed = 1.0 - m.OreStored;
            Check(Math.Abs(mono - oreUsed * Ore.MonoYield) < 1e-6 && oreUsed > 0.19, $"mono {mono:0.0000} t from {oreUsed:0.0000} t of ore");
            foreach (var p in m.parts) if (p.def.mono > 0) p.mono = p.def.mono;
            double before = m.OreStored;
            m.BackgroundAdvance(10, t, false);
            Check(m.OreStored == before && m.convNotes[mc.index].Contains("full"), $"full mono tanks: '{m.convNotes[mc.index]}'");
            return $"0.6 t of ore -> {made:0.00} t of fuel in 5 min; {oreUsed:0.00} t -> {mono:0.000} t mono; stops when empty or full";
        }

        static VesselSim Plant(int radiators, bool unfold)
        {
            var home = System4[0];
            var d = D("plant", "probe.spark", "battery.s", "isru.s", "ore.l", "ore.l", "tank.l8", "tank.l8");
            if (radiators > 0) DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "isru.s"), 0.5f, 0f, "rad.r", radiators, false);
            var v = InOrbit(d, home, home.radius + 300_000, 0);
            Overcharge(v);
            foreach (var p in v.parts) { if (p.def.oreCapacity > 0) p.ore = p.def.oreCapacity; if (p.def.kind == PartKind.Tank) p.propellant = 0; }
            if (unfold) Unfold(v, PartKind.Radiator);
            v.SetConverter(v.parts.First(p => p.def.id == "isru.s"), 1);
            return v;
        }

        static double Expect(double qKw, double shedKw)
        {
            double k = shedKw * 1000 / Ore.NominalDelta, lo = Ore.AmbientTemp, hi = 3000;
            for (int i = 0; i < 60; i++) { double mid = (lo + hi) / 2; if (mid - Ore.AmbientTemp - qKw * 1000 * Ore.HeatEfficiency(mid) / k > 0) hi = mid; else lo = mid; }
            return lo;
        }

        static string HeatAndRadiators()
        {
            double Run(VesselSim v, double hours, double step) { double t = 0; while (t < hours * 3600 - 1e-6) { v.BackgroundAdvance(step, t, false); t += step; } return v.coreTemp; }

            var a = Plant(0, false);
            double tA = Run(a, 4, 60), eA = Expect(180, 40);
            Check(Math.Abs(tA - eA) < 5 && a.heatEff < 0.5, $"no radiators: core {tA:0} K (expected {eA:0}), speed {a.heatEff:P0}");
            double oreA = 16 - a.OreStored;

            var b = Plant(1, true);
            double tB = Run(b, 2, 60), eB = Expect(180, 160);
            Check(Math.Abs(tB - eB) < 3 && b.heatEff > 0.999, $"one radiator: core {tB:0} K (expected {eB:0}), speed {b.heatEff:P0}");

            var c = Plant(1, false);
            double tC = Run(c, 2.5, 60), eC = Expect(180, 40 + 120 * Ore.FoldedRadiator);
            Check(Math.Abs(tC - eC) < 5 && c.heatEff > a.heatEff && c.heatEff < 0.9, $"folded radiator: core {tC:0} K (expected {eC:0}), speed {c.heatEff:P0}");

            // One 4-hour warp step settles straight to the same temperature
            var w = Plant(0, false);
            w.BackgroundAdvance(4 * 3600, 0, false);
            double oreW = 16 - w.OreStored;
            Check(Math.Abs(w.coreTemp - eA) < 2, $"warp step: core {w.coreTemp:0} K vs {eA:0}");
            Check(Math.Abs(oreW - oreA) < oreA * 0.25, $"warp step refined {oreW:0.0} t vs {oreA:0.0} t in small steps");

            // The engineer's report catches it
            var noRad = D("x", "probe.spark", "battery.s", "isru.s", "ore.s", "tank.s4");
            Check(noRad.Warnings(9.81).Any(s => s.Contains("radiators")), "no heat warning for a bare converter");
            var withRad = D("y", "probe.spark", "battery.s", "isru.s", "ore.s", "tank.s4");
            DesignOps.AttachSurfaceAt(withRad, withRad.nodes.FindIndex(n => n.id == "isru.s"), 0.5f, 0f, "rad.r", 2, false);
            Check(!withRad.Warnings(9.81).Any(s => s.Contains("radiators")), "heat warning even with two radiators");
            return $"core: alone {tA:0} K at {a.heatEff * 100:0}% speed; folded radiator {tC:0} K; radiator out {tB:0} K, full speed; 4 h warp step {w.coreTemp:0} K";
        }

        static string MiningPower()
        {
            var home = System4[0];
            VesselSim Make()
            {
                var v = InOrbit(D("lowpower", "probe.spark", "battery.s", "isru.s", "ore.s", "tank.s4"), home, home.radius + 300_000, 0);
                foreach (var p in v.parts) { if (p.def.oreCapacity > 0) p.ore = 1.0; if (p.def.kind == PartKind.Tank) p.propellant = 0; }
                v.SetConverter(v.parts.First(p => p.def.id == "isru.s"), 1);
                return v;
            }
            var s = Make();
            double t = 0;
            for (int i = 0; i < 200; i++) { s.BackgroundAdvance(1, t, false); t += 1; }
            double usedSmall = 1.0 - s.OreStored, expect = 0.002 * (630 - 0.03 * 200) / 6.0;
            Check(Math.Abs(usedSmall - expect) < expect * 0.05, $"630 EC ran the converter for {usedSmall / 0.002:0} s of 200 (expected ~{expect / 0.002:0})");
            Check(s.EcStored >= 0 && s.EcStored < 1, $"battery left at {s.EcStored:0.00} EC");
            var b = Make();
            b.BackgroundAdvance(200, 0, false);
            double usedBig = 1.0 - b.OreStored;
            Check(Math.Abs(usedBig - expect) < expect * 0.05, $"one 200 s step refined {usedBig:0.000} t vs {expect:0.000}");
            Check(b.convNotes.Values.First().Contains("power"), $"note: {b.convNotes.Values.First()}");
            return $"630 EC: {usedSmall:0.000} t refined in 1 s steps, {usedBig:0.000} t in one step (expected {expect:0.000}); never below 0 EC";
        }

        static string MiningSave()
        {
            var spot = RichSpot(Tal, out _);
            var u = new Universe();
            var v = u.Add(VesselSim.CreateLanded(VesselDesign.Lander(), Tal, spot, 0));
            foreach (var p in v.parts) if (p.def.oreCapacity > 0) p.ore = 0.7;
            v.SetConverter(v.parts.First(p => p.def.id == "isru.s"), 2);
            v.coreTemp = 555;
            u.survey.Mark(Tal, spot, 10);
            double cov = u.survey.Coverage(Tal);
            Check(v.survey == u.survey, "Universe.Add didn't hand the vessel the survey");
            string json = u.ToJson();
            var u2 = new Universe();
            u2.LoadJson(json, System4);
            var v2 = u2.vessels[0];
            Check(Math.Abs(v2.OreStored - 0.7) < 1e-9 && v2.parts.First(p => p.def.id == "isru.s").convMode == 2 && Math.Abs(v2.coreTemp - 555) < 1e-9, "ore, converter mode or core heat changed in the save");
            Check(Math.Abs(u2.survey.Coverage(Tal) - cov) < 1e-12 && u2.survey.IsScanned(Tal, spot) && v2.survey == u2.survey, "survey map changed in the save");
            string old = json.Replace("\"ore\":0.7", "\"ore\":0").Replace("\"coreTemp\":555.0", "\"coreTemp\":0.0");
            var u3 = new Universe();
            u3.LoadJson(old, System4);
            Check(u3.vessels[0].OreStored == 0 && u3.vessels[0].coreTemp == Ore.AmbientTemp, $"old save: ore {u3.vessels[0].OreStored}, core {u3.vessels[0].coreTemp}");
            u2.Reset(GameMode.Sandbox);
            Check(u2.survey.Coverage(Tal) == 0, "starting over kept the map");
            return $"ore 0.7 t, converter on mono, core 555 K and {cov * 100:0.0}% of Tal mapped all round-trip";
        }

        static string MiningCareer()
        {
            var c = new Career();
            c.Start(GameMode.Career);
            foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id);
            c.records |= Records.Space | Records.Orbit | Records.TalFlyby | Records.TalLanding;
            // Both mining nodes exist and every new part is in the tree
            foreach (var id in new[] { "scan.orbit", "drill.s", "drill.l", "ore.r", "ore.s", "ore.l", "isru.s", "rad.r", "rad.s", "solar.l" })
                Check(TechTree.NodeOf(id) != null && Prices.Of(PartCatalog.Get(id)) > 250, $"{id} isn't in the tech tree or has no price");
            // Offers
            var cand = (List<Contract>)typeof(Career).GetMethod("Candidates", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { 0.0 });
            Check(cand.Any(k => k.type == "survey" && k.body == "Tal") && cand.Any(k => k.type == "mine" && k.body == "Tal"), "no survey/mine contracts on offer");
            // Records
            double f0 = c.funds;
            c.OnFlags(VFlags.Mined | VFlags.Refined);
            Check((c.records & Records.OreMined) != 0 && (c.records & Records.OreRefined) != 0 && c.funds > f0, "ore records didn't pay");
            // Survey contract
            var sc = new Contract { id = 900, type = "survey", body = "Tal", amount = 0.6, state = ContractState.Active, deadline = 1e12, reward = 1000 };
            var mc = new Contract { id = 901, type = "mine", body = "Tal", amount = 1.5, state = ContractState.Active, deadline = 1e12, reward = 1000 };
            c.contracts.Add(sc); c.contracts.Add(mc);
            var s = new OreSurvey();
            for (int lo = -180; lo < 180; lo += 10) s.Mark(Tal, CelestialBody.DirectionFromLatLon(0, lo), 12);
            c.CheckSurvey(s, System4);
            Check(sc.state == ContractState.Active, $"survey done at {s.Coverage(Tal) * 100:0}%");
            for (int la = -85; la <= 85; la += 10) for (int lo = -180; lo < 180; lo += 10) s.Mark(Tal, CelestialBody.DirectionFromLatLon(la, lo), 12);
            c.CheckSurvey(s, System4);
            Check(sc.state == ContractState.Completed, $"survey not done at {s.Coverage(Tal) * 100:0}%");
            // Mining contract
            var v = VesselSim.CreateLanded(VesselDesign.Lander(), Tal, RichSpot(Tal, out _), 0);
            foreach (var p in v.parts) if (p.def.oreCapacity > 0) p.ore = 1.0;
            c.CheckVessel(v, 0);
            Check(mc.state == ContractState.Active, "mine contract done with 1 t");
            foreach (var p in v.parts) if (p.def.oreCapacity > 0) p.ore = p.def.oreCapacity;
            c.CheckVessel(v, 0);
            Check(mc.state == ContractState.Completed, $"mine contract not done with {v.OreStored:0.0} t");

            // The Prospector preset: enough delta-v for Tal, lifts off, no mining warnings
            var pd = VesselDesign.Prospector();
            var reps = Staging.AnalyzeDesign(pd, 9.81);
            double dv = reps.Sum(r => r.dvVac);
            var first = reps.Find(r => r.hasEngines);
            var warn = pd.Warnings(9.81);
            Check(first != null && first.twrAsl >= 1.25, $"Prospector liftoff TWR {first?.twrAsl:0.00}");
            Check(dv > 5600, $"Prospector has only {dv:0} m/s (orbit ~3,400 + Tal transfer ~860 + capture and landing ~800)");
            Check(!warn.Any(x => x.Contains("radiators") || x.Contains("EC/s") || x.Contains("ore tank")), "Prospector warnings: " + string.Join(" | ", warn));
            return $"ore records pay; Tal survey at 60%, mine contract at {mc.amount:0.0} t; Prospector {dv:0} m/s, liftoff TWR {first.twrAsl:0.00}";
        }
    }
}
