using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 5a tests: career money, tech tree, science, contracts and records.</summary>
    public static partial class SimTests
    {
        static void RunMilestone5a()
        {
            Run("Career: tech tree gates parts; research needs science and a researched node before it", CareerTech);
            Run("Career: launching costs the rocket's price; recovery refunds by distance and fuel left", CareerMoney);
            Run("Science: situations, values, repeats give less, transmitting gives part, air-only experiments", CareerScience);
            Run("Contracts: offers fit progress; accepting pays the advance; completion pays the reward", CareerContracts);
            Run("Contracts: satellite orbits are checked across the universe; overdue contracts fail", CareerSatelliteDeadline);
            Run("Records pay once; sandbox unlocks everything and charges nothing; the career saves and loads", CareerRecordsSave);
        }

        /// <summary>A new career with a fixed contract seed, so the offers are the same every run.</summary>
        static Career NewCareer()
        {
            var c = new Career();
            c.Start(GameMode.Career);
            c.contracts.Clear();
            c.seed = 4242;
            c.nextContractId = 1;
            c.RefreshOffers(0);
            return c;
        }

        static string CareerTech()
        {
            var c = NewCareer();
            Check(c.IsUnlocked("pod.wren") && c.IsUnlocked("eng.kestrel") && c.IsUnlocked("sci.therm"), "starting parts locked");
            Check(!c.IsUnlocked("eng.ember") && !c.IsUnlocked("port.sm"), "advanced parts unlocked at the start");
            Check(c.LaunchProblem(VesselDesign.FirstFlight()) == null, $"can't launch the first flight: {c.LaunchProblem(VesselDesign.FirstFlight())}");
            string p = c.LaunchProblem(VesselDesign.Default());
            Check(p != null && p.Contains("Not researched"), $"the starter rocket (needs Staging) wasn't blocked: {p}");
            var staging = TechTree.Get("staging");
            Check(!c.Research(staging), "researched with no science");
            c.science = 10;
            Check(c.Research(staging) && Math.Abs(c.science - 5) < 1e-6, "research didn't take 5 science");
            Check(c.IsUnlocked("eng.ember"), "Staging didn't unlock the Ember");
            c.science = 1000;
            Check(!c.Research(TechTree.Get("heavy")), "researched Heavy Rocketry without General Rocketry");
            Check(c.Research(TechTree.Get("rocketry")) && c.Research(TechTree.Get("heavy")), "couldn't research along the chain");
            int tiers = TechTree.Nodes.Select(n => n.tier).Distinct().Count();
            var orphan = PartCatalog.All.Where(d => TechTree.NodeOf(d.id) == null).Select(d => d.id).ToList();
            Check(tiers == 9, $"{tiers} tiers, expected 9");
            Check(orphan.Count == 0, $"parts not in the tech tree: {string.Join(", ", orphan)}");
            return $"{TechTree.Nodes.Count} nodes over {tiers} tiers, every part placed; first flight costs {Prices.Of(VesselDesign.FirstFlight()):N0}";
        }

        static string CareerMoney()
        {
            var home = System4[0];
            var c = NewCareer();
            var d = VesselDesign.FirstFlight();
            double cost = Prices.Of(d), f0 = c.funds;
            c.ChargeLaunch(d);
            Check(Math.Abs(f0 - c.funds - cost) < 1e-6, "launch didn't charge the price");
            var pad = VesselSim.CreateOnPad(d, home, 0);
            double before = c.funds;
            c.Recover(pad, 0, System4);
            double refundPad = c.funds - before;
            Check(Math.Abs(refundPad - 0.98 * cost) < 1, $"refund at the pad {refundPad:N0}, expected 98% of {cost:N0}");
            // Landed a quarter of the way round the world, tanks empty
            var far = VesselSim.CreateOnPad(d, home, 0);
            Vector3d farDir = home.FixedToInertial(CelestialBody.DirectionFromLatLon(0, 90), 0);
            far.pos = farDir * home.radius;
            foreach (var p in far.parts) p.propellant = 0;
            before = c.funds;
            c.Recover(far, 0, System4);
            double refundFar = c.funds - before;
            double dry = far.parts.Sum(p => Prices.ValueOf(p));
            Check(Math.Abs(refundFar - 0.2 * dry) < 1, $"far refund {refundFar:N0}, expected 20% of the dry value {dry:N0}");
            return $"rocket {cost:N0}; recovered at the pad {refundPad:N0}; far away with empty tanks {refundFar:N0}";
        }

        static string CareerScience()
        {
            var home = System4[0];
            var moon = System4[1];
            var c = NewCareer();
            var probe = D("sci", "probe.spark", "tank.s1");
            VesselSim At(double alt) { var v = InOrbit(probe, home, home.radius + alt, 0); return v; }
            var pad = VesselSim.CreateOnPad(probe, home, 0);
            var expect = new (VesselSim v, Situation s)[] { (pad, Situation.LaunchSite), (At(10_000), Situation.FlyingLow), (At(40_000), Situation.FlyingHigh), (At(100_000), Situation.SpaceLow), (At(400_000), Situation.SpaceHigh) };
            foreach (var (v, s) in expect) Check(Science.SituationOf(v, 0) == s, $"expected {s}, got {Science.SituationOf(v, 0)} at {v.Altitude / 1000:0} km");
            var talLanded = InOrbit(probe, moon, moon.radius + 0.5, 0);
            talLanded.landed = true;
            Check(Science.SituationOf(talLanded, 0) == Situation.Landed, "not landed on Tal");

            var therm = Science.Get("temp");
            var orbit = At(100_000);
            orbit.parts[0].science.Clear();
            var part = orbit.parts[0];
            Lab.Run(c, orbit, part, therm, 0);
            var d1 = Lab.Held(part, therm);
            Check(d1 != null && d1.subject == "temp@Arden:SpaceLow", $"result subject {d1?.subject}");
            float g1 = c.Credit(d1, 1f, System4);
            Check(Math.Abs(g1 - 6f) < 1e-4, $"first temperature scan in low space gave {g1}, expected 6");
            float g2 = c.Credit(d1, 1f, System4);
            Check(g2 < 1e-4, $"the same scan again gave {g2}");
            var samples = new ScienceData { experiment = "samples", subject = "samples@Tal:SpaceLow", transmit = 0.3f };
            float sent = c.Credit(samples, samples.transmit, System4), brought = c.Credit(samples, 1f, System4);
            Check(Math.Abs(sent - 15 * 3 * 0.3f) < 1e-3 && Math.Abs(brought - 15 * 3 * 0.7f) < 1e-3, $"samples sent {sent:F2} then recovered {brought:F2}");
            var baro = Science.Get("pressure");
            Check(!Science.Allowed(baro, home, Situation.SpaceLow) && Science.Allowed(baro, home, Situation.FlyingHigh), "barometer air rules wrong on Arden");
            Check(!Science.Allowed(baro, moon, Situation.Landed), "barometer worked on airless Tal");
            orbit.connected = false;
            string noRadio = Lab.Transmit(c, orbit, part, d1, System4);
            Check(noRadio.Contains("No signal"), $"transmitted without a link home: {noRadio}");
            return $"situations ok; temperature scan in low space {g1:0.0} sci then {g2:0.0}; Tal samples sent {sent:0.0} + recovered {brought:0.0}; total science {c.science:0.0}";
        }

        static string CareerContracts()
        {
            var home = System4[0];
            var c = NewCareer();
            var offered = c.Offered.ToList();
            Check(offered.Count == Career.OfferCount, $"{offered.Count} offers, expected {Career.OfferCount}");
            Check(offered.All(k => k.type != "flyby" && k.type != "land" && k.type != "dock"), "offered Tal or docking before reaching space");
            var alt = c.contracts.FirstOrDefault(k => k.type == "altitude") ?? c.contracts.FirstOrDefault(k => k.type == "space");
            if (alt == null)
            {
                alt = new Contract { id = 400, type = "altitude", title = "Reach 20 km", body = "Arden", altitude = 20_000, reward = 6_000, advance = 1_200, state = ContractState.Offered, deadline = 30 * 86400 };
                c.contracts.Add(alt);
            }
            double f0 = c.funds;
            c.Accept(alt, 0);
            Check(Math.Abs(c.funds - f0 - alt.advance) < 1e-6 && alt.state == ContractState.Active, "accepting didn't pay the advance");
            var high = InOrbit(D("hop", "probe.spark", "tank.s1"), home, home.radius + 90_000, 0, 0.5);
            double f1 = c.funds;
            c.CheckVessel(high, 0);
            Check(alt.state == ContractState.Completed && Math.Abs(c.funds - f1 - alt.reward) < 1e-6, $"{alt.title} didn't complete at 90 km");
            // Orbit contract
            var orb = c.contracts.FirstOrDefault(k => k.type == "orbit" && k.state == ContractState.Offered);
            if (orb == null)
            {
                orb = new Contract { id = 500, type = "orbit", title = "Orbit Arden", body = "Arden", reward = 26_000, advance = 5_000, state = ContractState.Offered, deadline = 60 * 86400 };
                c.contracts.Add(orb);
            }
            c.Accept(orb, 0);
            c.CheckVessel(InOrbit(D("o", "probe.spark"), home, home.radius + 100_000, 0), 0);
            Check(orb.state == ContractState.Completed, "orbit contract didn't complete in a 100 km orbit");
            c.SetRecord(Records.Space); c.SetRecord(Records.Orbit);
            foreach (var k in c.Offered.ToList()) c.Decline(k);
            c.RefreshOffers(0);
            Check(c.Offered.Any(k => k.type == "flyby" || k.type == "satellite" || k.type == "dock"), "no bigger contracts after reaching orbit");
            // With everything open accepted, there are still offers to pick from (science and tests in new places).
            foreach (var k in c.Offered.ToList()) c.Accept(k, 0);
            c.RefreshOffers(0);
            Check(c.Offered.Any(), "nothing left to offer while contracts are active");
            return $"first offers: {string.Join(", ", offered.Select(k => k.title))}; after orbit: {string.Join(", ", c.Offered.Select(k => k.title))}";
        }

        static string CareerSatelliteDeadline()
        {
            var home = System4[0];
            var c = NewCareer();
            var sat = new Contract { id = 99, type = "satellite", title = "Satellite test", body = "Arden", ap = 150_000, pe = 120_000, reward = 30_000, penalty = 15_000, repReward = 5, state = ContractState.Active, deadline = 1e9 };
            c.contracts.Add(sat);
            double r1 = home.radius + 120_000, r2 = home.radius + 150_000, a = (r1 + r2) / 2;
            double vPe = Math.Sqrt(home.mu * (2 / r1 - 1 / a)) / Math.Sqrt(home.mu / r1);
            var crewed = InOrbit(D("crewed", "pod.wren", "antenna.s"), home, r1, 0, vPe);
            var noAntenna = InOrbit(D("dumb", "probe.spark", "tank.s1"), home, r1, 0, vPe);
            c.CheckSatellites(new[] { crewed, noAntenna }, 0);
            Check(sat.state == ContractState.Active, "completed with a crewed ship or one without an antenna");
            var good = InOrbit(D("sat", "probe.spark", "antenna.s"), home, r1, 0, vPe);
            double f0 = c.funds;
            c.CheckSatellites(new[] { crewed, noAntenna, good }, 0);
            Check(sat.state == ContractState.Completed && Math.Abs(c.funds - f0 - 30_000) < 1e-6, "the right satellite didn't complete the contract");
            var late = new Contract { id = 100, type = "orbit", title = "Late", body = "Tal", reward = 50_000, penalty = 25_000, repReward = 10, state = ContractState.Active, deadline = 1000 };
            c.contracts.Add(late);
            f0 = c.funds;
            c.CheckDeadlines(500);
            Check(late.state == ContractState.Active, "failed before its deadline");
            c.CheckDeadlines(2000);
            Check(late.state == ContractState.Failed && Math.Abs(f0 - c.funds - 25_000) < 1e-6, "overdue contract didn't fail with its penalty");
            return "satellite needs probe + antenna + no crew + the right orbit; overdue contract failed with a 25,000 penalty";
        }

        static string CareerRecordsSave()
        {
            var c = NewCareer();
            double f0 = c.funds;
            c.OnFlags(VFlags.Launched | VFlags.Space);
            c.OnFlags(VFlags.Launched | VFlags.Space);
            Check(Math.Abs(c.funds - f0 - 10_000) < 1e-6, $"records paid {c.funds - f0:N0}, expected 10,000 once");
            var sb = new Career();
            sb.Start(GameMode.Sandbox);
            Check(sb.IsUnlocked("hab.s") && sb.LaunchProblem(VesselDesign.Longshot()) == null, "sandbox locked something");
            double sf = sb.funds;
            sb.ChargeLaunch(VesselDesign.Longshot());
            Check(sb.funds == sf, "sandbox charged for a launch");

            c.science = 12.5;
            c.Research(TechTree.Get("staging"));
            c.Accept(c.Offered.First(), 0);
            c.Credit(new ScienceData { experiment = "temp", subject = "temp@Arden:FlyingLow", transmit = 1 }, 1, System4);
            var u = new Universe();
            u.career.Load(c.ToSave());
            var u2 = new Universe();
            u2.LoadJson(u.ToJson(), System4);
            var k = u2.career;
            Check(k.IsCareer && Math.Abs(k.funds - c.funds) < 1e-6 && Math.Abs(k.science - c.science) < 1e-6 && k.reputation == c.reputation, "money/science/rep changed");
            Check(k.IsUnlocked("eng.ember") && k.records == c.records, "research or records lost");
            Check(k.contracts.Count == c.contracts.Count && k.Active.Count() == c.Active.Count(), "contracts lost");
            Check(Math.Abs(k.Remaining("temp@Arden:FlyingLow", 4.2f)) < 1e-4, "science already collected was forgotten");
            return $"records +{10_000:N0} once; sandbox free; career saved with {k.contracts.Count} contracts, {k.unlocked.Count} nodes, {k.subjects.Count} science subjects";
        }
    }
}
