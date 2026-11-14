using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 19 tests: supplies for long voyages.</summary>
    public static partial class SimTests
    {
        static void RunMilestone19()
        {
            Run("Supplies: the crew eat a crew-day each a day in space, nothing at home on Arden, nothing with supplies off", SuppliesUse);
            Run("Supplies: out of supplies, the crew die two days later (lost from the roster); resupplied in time, they live", SuppliesRunOut);
            Run("Supplies: a powered greenhouse feeds two; an astronaut's suit takes half a day and gives back the rest", SuppliesGreenhouseAndEva);
            Run("Supplies: saved and loaded; on for new careers, off for sandbox and older saves; the builder's estimate", SuppliesSaveAndEstimate);
        }

        /// <summary>A crewed capsule in a 200 km orbit of Arden, in a universe with supplies on (or off), crewed by a roster member.</summary>
        static (Universe u, VesselSim v) SupplyShip(VesselDesign d, bool on = true, bool landedHome = false)
        {
            var u = new Universe();
            u.career.Start(GameMode.Career);
            u.career.suppliesOn = on;
            var home = Home14;
            VesselSim v = landedHome
                ? VesselSim.CreateLanded(d, home, CelestialBody.DirectionFromLatLon(5, 60), 0)
                : VesselSim.CreateFree(d, home, new Vector3d(home.radius + 200_000, 0, 0), new Vector3d(0, 0, -Math.Sqrt(home.mu / (home.radius + 200_000))), Quaternion.identity);
            v.hasLaunched = true; v.throttle = 0;
            u.Add(v);
            var pod = v.parts.First(p => p.crew.Count > 0);
            var m = u.career.roster.First(c => c.state == 0);
            pod.crew[0] = m.name; m.state = 1;
            return (u, v);
        }

        static readonly Dictionary<VesselSim, double> clocks = new Dictionary<VesselSim, double>();

        /// <summary>Moves a ship on (on rails, systems running) by 'seconds', keeping its own game clock between calls.</summary>
        static void Advance(VesselSim v, double seconds, double step = 3600)
        {
            clocks.TryGetValue(v, out double ut);
            double end = ut + seconds;
            while (ut < end - 1e-6) { double dt = Math.Min(step, end - ut); v.BackgroundAdvance(dt, ut, false); ut += dt; }
            clocks[v] = ut;
        }

        static string SuppliesUse()
        {
            var (u, v) = SupplyShip(D("cap", "chute.s", "pod.wren"));
            double s0 = v.SuppliesStored;
            Check(Math.Abs(s0 - 5) < 1e-6, $"a capsule starts with {s0} crew-days");
            Check(Math.Abs(v.SupplyDaysLeft() - 5) < 1e-6, $"days left {v.SupplyDaysLeft()}");
            Advance(v, 2 * Supplies.Day);
            Check(Math.Abs(v.SuppliesStored - 3) < 0.01, $"after two days in orbit: {v.SuppliesStored:0.00} crew-days (expected 3)");
            double m0 = VesselSim.CreateFree(D("cap", "chute.s", "pod.wren"), Home14, new Vector3d(Home14.radius + 200_000, 0, 0), Vector3d.zero, Quaternion.identity).mass;
            Check(Math.Abs((m0 - v.mass) - 2 * Supplies.MassPerDay) < 1e-4, $"eaten supplies didn't come off the mass ({m0 - v.mass:0.0000} t)");
            var (u2, off) = SupplyShip(D("cap", "chute.s", "pod.wren"), on: false);
            Advance(off, 2 * Supplies.Day);
            Check(Math.Abs(off.SuppliesStored - 5) < 1e-6 && double.IsInfinity(off.SupplyDaysLeft()), "supplies were used with them switched off");
            var (u3, home) = SupplyShip(D("cap", "chute.s", "pod.wren"), landedHome: true);
            Advance(home, 2 * Supplies.Day);
            Check(Math.Abs(home.SuppliesStored - 5) < 1e-6, "the crew ate at home on Arden");
            return $"5 crew-days in a capsule; two days in orbit leave 3 (and 10 kg lighter); nothing used at home or with supplies off";
        }

        static string SuppliesRunOut()
        {
            var (u, v) = SupplyShip(D("cap", "chute.s", "pod.wren"));
            string who = v.parts.SelectMany(p => p.crew).First();
            foreach (var p in v.parts) p.supplies = Math.Min(p.supplies, 0.5);
            Advance(v, 0.6 * Supplies.Day);
            Check(v.starvingSince >= 0 && v.SuppliesStored < 1e-6, $"not starving after the supplies ran out ({v.SuppliesStored:0.00} left)");
            Advance(v, 1.5 * Supplies.Day);
            Check(v.CrewCount == 1, "the crew died before the two days were up");
            Advance(v, 1.0 * Supplies.Day);
            Check(v.CrewCount == 0, "the crew are still alive three days after the supplies ran out");
            var m = u.career.roster.Find(c => c.name == who);
            Check(m != null && m.state == 2, $"{who} isn't marked lost in the roster");
            Check(u.career.news.Any(n => n.Contains(who) && n.Contains("died")), "no news of the death: " + string.Join(" | ", u.career.news));
            Check(!v.destroyed, "the ship itself was destroyed");

            // Resupplied in time
            var (u2, w) = SupplyShip(D("cap", "chute.s", "pod.wren"));
            foreach (var p in w.parts) p.supplies = 0;
            Advance(w, 1.5 * Supplies.Day);
            Check(w.starvingSince >= 0 && w.CrewCount == 1, "the second ship wasn't starving yet alive");
            w.AddSupplies(3);
            Advance(w, 2.0 * Supplies.Day);
            Check(w.CrewCount == 1 && w.starvingSince < 0, "resupplied in time, the crew still died");
            return $"{who} died 2 days after the supplies ran out (roster: lost, news posted); a crew resupplied 1.5 days in lived";
        }

        static string SuppliesGreenhouseAndEva()
        {
            var d = D("gh", "chute.s", "pod.wren", "garden.s", "battery.s");
            var (u, v) = SupplyShip(d);
            foreach (var p in v.parts) p.supplies = 0.5 * p.def.supplyCapacity;
            double s0 = v.SuppliesStored;
            // An hour in 10-second steps, power kept up (as solar panels would)
            for (int i = 0; i < 360; i++) { foreach (var p in v.parts) p.charge = p.def.ecCapacity; v.BackgroundAdvance(10, i * 10, false); }
            double grown = v.SuppliesStored - s0;
            Check(v.greenhouseOn && Math.Abs(grown - 1.0 / 24) < 0.005, $"one crew with a powered greenhouse gained {grown * 24:0.00} crew-days a day (expected +1)");
            // Without power it's just a store
            foreach (var p in v.parts) p.charge = 0;
            double s1 = v.SuppliesStored;
            Advance(v, 3600, 600);
            Check(!v.greenhouseOn && Math.Abs((s1 - v.SuppliesStored) - 1.0 / 24) < 0.005, $"an unpowered greenhouse still grew food ({(s1 - v.SuppliesStored) * 24:0.00} a day used)");

            // EVA: the suit takes half a day from the ship and gives back what's left
            var (u2, cap) = SupplyShip(D("cap", "chute.s", "pod.wren"));
            string who = cap.parts.SelectMany(p => p.crew).First();
            double before = cap.SuppliesStored;
            var eva = cap.GoEva(who, 0, out string why);
            Check(eva != null, "couldn't go on EVA: " + why);
            u2.Add(eva);
            Check(Math.Abs(eva.SuppliesStored - 0.5) < 1e-6 && Math.Abs(cap.SuppliesStored - (before - 0.5)) < 1e-6, $"suit {eva.SuppliesStored:0.00}, ship {cap.SuppliesStored:0.00}");
            bool back = eva.Board(cap, out string msg);
            Check(back && Math.Abs(cap.SuppliesStored - before) < 1e-6, $"boarding: {msg}, ship back to {cap.SuppliesStored:0.00} of {before:0.00}");
            return $"greenhouse with power: +1 crew-day a day for one crew; without power: -1; a suit carried 0.5 days and gave them back";
        }

        static string SuppliesSaveAndEstimate()
        {
            var (u, v) = SupplyShip(D("cap", "chute.s", "pod.wren"));
            foreach (var p in v.parts) p.supplies = 0;
            Advance(v, 0.5 * Supplies.Day);
            double since = v.starvingSince;
            var back = new Universe();
            back.LoadJson(u.ToJson(), System4);
            var v2 = back.vessels.First();
            Check(back.career.suppliesOn && Math.Abs(v2.starvingSince - since) < 1, $"after loading: supplies on {back.career.suppliesOn}, starving since {v2.starvingSince:0} (was {since:0})");
            var (u3, w) = SupplyShip(D("cap", "chute.s", "pod.wren"));
            Advance(w, 1 * Supplies.Day);
            var w2 = VesselSim.FromSave(w.ToSave(), System4, 0);
            Check(Math.Abs(w2.SuppliesStored - w.SuppliesStored) < 1e-6, "supplies changed on save/load");
            var c = new Career(); c.Start(GameMode.Career);
            var sb = new Career(); sb.Start(GameMode.Sandbox);
            Check(c.suppliesOn && !sb.suppliesOn, "new careers should have supplies on, sandbox off");
            var old = JsonUtility.ToJson(c.ToSave()).Replace("\"suppliesOn\":true", "\"suppliesOn_gone\":true");
            var oc = new Career(); oc.Load(JsonUtility.FromJson<CareerSave>(old));
            Check(!oc.suppliesOn, "an older save (no supplies setting) came back with supplies on");
            // The builder's estimate
            var crate = D("c", "chute.s", "pod.wren", "tank.s2");
            DesignOps.AttachSurfaceAt(crate, crate.nodes.FindIndex(n => n.id == "tank.s2"), 0.5f, 0, "sup.s", 1, false);
            double d1 = Supplies.DaysFor(crate), d2 = Supplies.DaysFor(D("h", "probe.spark", "hab.s", "garden.s"));
            Check(Math.Abs(d1 - 35) < 1e-6, $"capsule + crate: {d1} days (expected 35)");
            Check(Math.Abs(d2 - 25) < 1e-6, $"habitat for four + greenhouse: {d2} days (expected (40+10)/(4-2) = 25)");
            Check(double.IsInfinity(Supplies.DaysFor(D("p", "probe.spark", "tank.s1"))), "a probe has a supply limit");
            return $"starvation clock and supplies survive a save; careers on, sandbox and old saves off; capsule + crate {d1} days, habitat + greenhouse {d2}";
        }
    }
}
