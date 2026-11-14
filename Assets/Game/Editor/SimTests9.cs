using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 9 tests: Vesk, the second moon, and quicksave.</summary>
    public static partial class SimTests
    {
        static void RunMilestone9()
        {
            Run("Vesk: a small icy moon 26,000 km out, its sphere of influence well clear of Tal's, tidally locked, smooth ground", VeskGeometry);
            Run("Vesk transfer: a planned burn from low Arden orbit reaches Vesk and the flight matches the plan", VeskTransfer);
            Run("Vesk landing: a lander touches down on the ice and stays put; there's ore to mine", VeskLanding);
            Run("Vesk career: records, contracts, 1.6x science, return contracts per moon and crew experience", VeskCareer);
            Run("Quicksave: loading brings back every vessel, the career, the time and which vessel you flew", QuickSaveRoundTrip);
        }

        static CelestialBody Vesk => System4[2];

        static string VeskGeometry()
        {
            var home = System4[0]; var tal = System4[1]; var vesk = Vesk;
            Check(System4.Count >= 3 && vesk.name == "Vesk" && vesk.parent == home && home.children.Contains(vesk), "Vesk isn't Arden's second moon");
            double soi = vesk.SoiRadius;
            Check(soi > 1_000_000 && soi < 3_000_000, $"SOI {soi / 1000:F0} km");
            double gap = (vesk.orbitRadius - soi) - (tal.orbitRadius + tal.SoiRadius);
            Check(gap > 5_000_000, $"only {gap / 1000:F0} km between the two spheres of influence");
            Check(Math.Abs(vesk.rotationPeriod - vesk.OrbitPeriod) < 1, "not tidally locked");
            double g = vesk.SurfaceGravity;
            Check(g > 0.5 && g < 1.0, $"surface g {g:F2}");
            Check(Science.Multiplier(vesk, Situation.Landed) > Science.Multiplier(tal, Situation.Landed) * 1.5, "Vesk science isn't worth more than Tal's");

            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var d in Directions(2000, 91)) { double h = Terrain.Height(vesk, d); lo = Math.Min(lo, h); hi = Math.Max(hi, h); }
            Check(lo > -1000 && hi < Terrain.MaxHeight(vesk) && hi - lo > 400, $"ground from {lo:0} to {hi:0} m");
            double worst = 0;
            foreach (var d in Directions(400, 92))
            {
                var side = Vector3d.Cross(d, new Vector3d(0, 1, 0));
                if (side.sqrMagnitude < 0.01) continue;
                double dh = Math.Abs(Terrain.Height(vesk, (d + side.normalized * (10 / vesk.radius)).normalized) - Terrain.Height(vesk, d));
                worst = Math.Max(worst, dh);
            }
            Check(worst < 10, $"the ground jumps {worst:0.0} m over 10 m");
            return $"SOI {soi / 1000:F0} km ({gap / 1000:F0} km clear of Tal's), orbit {vesk.OrbitPeriod / 3600:F0} h, g {g:F2}, ground {lo:0} to {hi:0} m, steepest {worst:0.0} m per 10 m";
        }

        static string VeskTransfer()
        {
            var home = System4[0]; var vesk = Vesk;
            double rr = home.radius + 100_000, vc = Math.Sqrt(home.mu / rr);
            Vector3d r = new Vector3d(rr, 0, 0), v = new Vector3d(0, 0, -vc);
            double ut = 0;
            Check(Patcher.FindTransfer(home, r, v, ut, vesk, 40_000, out double burnUt, out double dv, out double predPe), "no transfer to Vesk found");
            var sim = VesselSim.CreateFree(D("probe", "probe.spark"), home, r, v, Quaternion.identity);
            sim.sas = SasMode.Off;
            sim.RailsAdvance(burnUt - ut, ut, out double used);
            ut += used;
            sim.vel = sim.vel + sim.vel.normalized * dv;
            sim.rails = null;
            var plan = Patcher.Predict(home, sim.pos, sim.vel, ut, 3);
            Check(plan.Count >= 2 && plan[1].body == vesk, "the plan has no Vesk encounter");

            double minAlt = double.PositiveInfinity, entered = double.NaN;
            for (int i = 0; i < 100_000; i++)
            {
                var ev = sim.RailsAdvance(600, ut, out used);
                ut += used;
                if (sim.body == vesk)
                {
                    if (double.IsNaN(entered)) entered = ut;
                    var o = Orbit.FromState(sim.pos, sim.vel, vesk.mu, ut);
                    minAlt = Math.Min(minAlt, o.Periapsis - vesk.radius);
                    break;
                }
                if (sim.body != home || ev == RailsEvent.NearSurface) break;
            }
            Check(!double.IsNaN(entered), $"never reached Vesk (ended around {sim.body.name})");
            Check(Math.Abs(minAlt - predPe) < 3000, $"periapsis {minAlt / 1000:F1} km vs predicted {predPe / 1000:F1} km");
            return $"burn {dv:F0} m/s from 100 km, Vesk reached after {(entered - burnUt) / 3600:F1} h, periapsis {minAlt / 1000:F1} km (predicted {predPe / 1000:F1})";
        }

        static string VeskLanding()
        {
            var vesk = Vesk;
            var dir = Directions(1, 93).First();
            double ground = Terrain.Height(vesk, dir);
            var probe = VesselSim.CreateLanded(Hopper(), vesk, dir, 0);
            var v = VesselSim.CreateFree(Hopper(), vesk, probe.pos + dir * 3, probe.vel - dir * 2, probe.rot);
            foreach (var p in v.parts) if (p.def.kind == PartKind.Leg) { p.legsDeployed = true; p.legDeploy = 1; }
            v.disableRails = true;
            double t = 0;
            for (int i = 0; i < 3000 && !v.landed && !v.destroyed; i++) { v.Step(0.01, t); t += 0.01; }
            Check(v.landed && !v.destroyed, $"didn't land on Vesk at {ground:0} m: {v.destroyReason}");
            Check(Math.Abs(v.RadarAltitude(t)) < 0.1, $"landed {v.RadarAltitude(t):0.00} m off the ground");
            var at = vesk.InertialToFixed(v.pos, t);
            for (int i = 0; i < 2000; i++) { v.Step(0.01, t); t += 0.01; }
            double drift = (vesk.InertialToFixed(v.pos, t) - at).magnitude;
            Check(v.landed && drift < 0.5, $"slid {drift:0.00} m in 20 s");
            double best = 0;
            foreach (var d in Directions(400, 94)) best = Math.Max(best, Ore.Concentration(vesk, d));
            Check(best > 0.05, $"richest ore on Vesk only {best * 100:0.0}%");
            return $"down on the ice at {ground:0} m, {drift:0.000} m of creep in 20 s; ore up to {best * 100:0}%";
        }

        static string VeskCareer()
        {
            var u = CareerUniverse();
            var c = u.career;
            foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id);
            var candidates = typeof(Career).GetMethod("Candidates", BindingFlags.NonPublic | BindingFlags.Instance);
            List<Contract> Offers() => (List<Contract>)candidates.Invoke(c, new object[] { 0.0 });

            c.records |= Records.Space | Records.Orbit | Records.TalFlyby;
            Check(!Offers().Any(k => k.body == "Vesk"), "Vesk offered before Tal's been orbited");
            c.records |= Records.TalOrbit;
            var first = Offers();
            Check(first.Any(k => k.type == "flyby" && k.body == "Vesk") && !first.Any(k => k.type == "land" && k.body == "Vesk"), "after orbiting Tal: no Vesk flyby, or a landing too soon");

            double f0 = c.funds;
            c.OnFlags(VFlags.SoiVesk | VFlags.OrbitVesk | VFlags.LandedVesk);
            Check((c.records & (Records.VeskFlyby | Records.VeskOrbit | Records.VeskLanding)) == (Records.VeskFlyby | Records.VeskOrbit | Records.VeskLanding), "Vesk records not set");
            Check(c.funds > f0 + 100_000, $"Vesk records paid only {c.funds - f0:N0}");
            var later = Offers();
            foreach (var type in new[] { "orbit", "land", "return", "survey", "mine" })
                Check(later.Any(k => k.type == type && k.body == "Vesk"), $"no Vesk {type} contract on offer");

            // A return contract only completes for the moon it names
            var vReturn = new Contract { id = 950, type = "return", body = "Vesk", situation = "land", state = ContractState.Active, deadline = 1e12, reward = 1000 };
            var tReturn = new Contract { id = 951, type = "return", body = "Tal", situation = "land", state = ContractState.Active, deadline = 1e12, reward = 1000 };
            c.contracts.Add(vReturn); c.contracts.Add(tReturn);
            var fromTal = u.Add(InOrbit(D("cap", "chute.s", "pod.wren"), System4[0], System4[0].radius + 100_000, 0));
            fromTal.careerFlags = VFlags.Launched | VFlags.LandedTal;
            c.Recover(fromTal, 0, System4);
            Check(tReturn.state == ContractState.Completed && vReturn.state == ContractState.Active, "a Tal landing completed the Vesk return (or not the Tal one)");
            var pilot = c.roster.First(m => m.role == CrewRole.Pilot);
            var fromVesk = u.Add(InOrbit(D("cap", "chute.s", "pod.wren"), System4[0], System4[0].radius + 100_000, 1));
            Seat(fromVesk, pilot);
            fromVesk.careerFlags = VFlags.Launched | VFlags.Space | VFlags.OrbitHome | VFlags.SoiVesk | VFlags.OrbitVesk | VFlags.LandedVesk;
            c.Recover(fromVesk, 0, System4);
            Check(vReturn.state == ContractState.Completed && (c.records & Records.VeskReturn) != 0, "coming home from Vesk didn't complete its return");
            Check(pilot.Level == 5, $"a pilot back from Vesk's surface is level {pilot.Level} (xp {pilot.Xp})");
            return $"Vesk opens after Tal orbit; 4 records paid {c.funds - f0:N0}+; landed science x{Science.Multiplier(Vesk, Situation.Landed):0.0} vs Tal x{Science.Multiplier(System4[1], Situation.Landed):0.0}; pilot level {pilot.Level} ({pilot.Xp} XP)";
        }

        static string QuickSaveRoundTrip()
        {
            var u = CareerUniverse();
            var c = u.career;
            u.ut = 5000;
            var orbiter = u.Add(InOrbit(D("Vesk relay", "probe.spark", "tank.s2"), Vesk, Vesk.radius + 30_000, 1.0));
            var lander = u.Add(VesselSim.CreateLanded(Hopper(), Vesk, Directions(1, 77).First(), u.ut));
            lander.hasLaunched = true;
            u.Add(InOrbit(D("cap", "chute.s", "pod.wren"), System4[0], System4[0].radius + 100_000, 0));
            c.records |= Records.VeskLanding;
            double funds = c.funds;
            Vector3d orbiterPos = orbiter.pos;
            string qs = u.ToQuickSave(lander);

            // What happens after the save: a vessel gone, the lander wrecked, money spent, time passed
            u.Remove(orbiter);
            lander.destroyed = true;
            c.funds -= 12_345;
            c.records &= ~Records.VeskLanding;
            u.ut = 9000;

            var back = u.LoadQuickSave(qs, System4);
            Check(back != null && back.id == lander.id && back.body == Vesk && back.landed && !back.destroyed, "didn't come back to the lander, standing on Vesk");
            Check(u.vessels.Count == 3, $"{u.vessels.Count} vessels after loading");
            var o2 = u.Find(orbiter.id);
            Check(o2 != null && o2.body == Vesk && (o2.pos - orbiterPos).magnitude < 0.01, "the Vesk relay didn't come back where it was");
            Check(Math.Abs(u.ut - 5000) < 1e-6 && Math.Abs(c.funds - funds) < 0.01 && (c.records & Records.VeskLanding) != 0, "time, funds or records not restored");
            Check(Math.Abs(back.RadarAltitude(u.ut)) < 0.2, $"the lander loaded {back.RadarAltitude(u.ut):0.00} m off the ground");
            Check(u.LoadQuickSave("not a save", System4) == null && u.LoadQuickSave("{}", System4) == null && u.vessels.Count == 3, "a broken quicksave changed the universe");
            return $"{qs.Length / 1024f:0.0} KB; 3 vessels, funds, records and time back; flying {back.name} again";
        }
    }
}
