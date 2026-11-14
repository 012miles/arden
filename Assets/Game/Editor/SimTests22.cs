using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 22 tests: bases of landed modules joined by ground cables.</summary>
    public static partial class SimTests
    {
        static void RunMilestone22()
        {
            Run("Bases: cables join landed modules up to 60 m apart (not flying ones, not further); they save and load", BaseCables);
            Run("Bases: linked modules share power, supplies and ore; the base's tanks fill a plugged-in ship, never the reverse", BaseSharing);
            Run("Bases: a cable comes loose when a module lifts off or is pulled out of reach", BaseCableBreaks);
            Run("Bases: crew walk between linked modules; a crewed research lab earns science every day (career)", BaseCrewAndLab);
            Run("Crew names: simple first names, and everyone is an Ardling; a big roster still gets different names", CrewNaming);
            Run("Contracts: a research base counts every cabled module (3 modules, 4 seats, a lab, an antenna, solar power)", BaseContract);
        }

        static CelestialBody Tal22 => System4.First(b => b.name == "Tal");

        /// <summary>A landed module on Tal, 'east' metres east of a spot near the equator.</summary>
        static VesselSim Module(Universe u, string name, double east, params string[] parts)
        {
            var tal = Tal22;
            double lon = 30 + east / (tal.radius * Math.PI / 180);
            var d = VesselDesign.Stack(name, parts);
            var v = VesselSim.CreateLanded(d, tal, CelestialBody.DirectionFromLatLon(5, lon), 0);
            v.hasLaunched = true; v.throttle = 0;
            u.Add(v);
            return v;
        }

        static string BaseCables()
        {
            var u = new Universe();
            var hub = Module(u, "Hub", 0, "base.hub");
            var lab = Module(u, "Lab", 35, "base.lab", "base.tank");
            var far = Module(u, "Far", 120, "base.tank");
            Check(Bases.Distance(hub, lab) > 30 && Bases.Distance(hub, lab) < 40, $"hub-lab {Bases.Distance(hub, lab):0} m");
            Check(Bases.Connect(u, hub, lab) == null, "couldn't connect hub and lab");
            Check(Bases.Connect(u, hub, lab) != null, "connected the same pair twice");
            Check(Bases.Connect(u, lab, far) != null, $"connected {Bases.Distance(lab, far):0} m apart");
            var flying = Module(u, "Hopper", 20, "base.tank");
            flying.landed = false;
            Check(Bases.Connect(u, hub, flying) != null, "connected a module that isn't landed");
            Check(Bases.GroupOf(u, lab).Count == 2 && Bases.Groups(u).Count == 1, "groups");
            var u2 = new Universe();
            u2.LoadJson(u.ToJson(), System4);
            Check(u2.links.Count == 1 && Bases.GroupOf(u2, u2.Find(hub.id)).Count == 2, "links didn't survive saving");
            return $"linked at {Bases.Distance(hub, lab):0} m; refused at {Bases.Distance(lab, far):0} m";
        }

        static string BaseSharing()
        {
            var u = new Universe();
            var hub = Module(u, "Hub", 0, "base.hub");
            var depot = Module(u, "Depot", 25, "base.tank");
            var ship = Module(u, "Lander", -25, "pod.wren", "tank.s2", "eng.wisp");
            foreach (var p in hub.parts) { p.charge = p.def.ecCapacity; p.supplies = p.def.supplyCapacity; }
            foreach (var p in depot.parts) { p.charge = 0; p.ore = p.def.oreCapacity; p.propellant = p.def.propellant; }
            foreach (var p in ship.parts) if (p.def.kind == PartKind.Tank) p.propellant = 0.1;
            double ecBefore = hub.EcStored + depot.EcStored + ship.EcStored;
            double shipCap = ship.parts.Sum(p => (double)p.def.propellant), depotFuel = depot.parts.Sum(p => p.propellant);
            Bases.Connect(u, hub, depot);
            Bases.Connect(u, hub, ship);
            Bases.Share(u);
            double ecAfter = hub.EcStored + depot.EcStored + ship.EcStored;
            Check(Math.Abs(ecAfter - ecBefore) < 1e-6, $"power not conserved {ecBefore:0.0} -> {ecAfter:0.0}");
            double fh = hub.parts.Sum(p => p.charge) / hub.parts.Sum(p => p.def.ecCapacity), fd = depot.parts.Sum(p => p.charge) / Math.Max(1e-9, depot.parts.Sum(p => p.def.ecCapacity));
            Check(depot.parts.Sum(p => p.def.ecCapacity) <= 0 || Math.Abs(fh - fd) < 1e-6, "power not evened out");
            double shipFuel = ship.parts.Sum(p => p.propellant), left = depot.parts.Sum(p => p.propellant);
            Check(Math.Abs(shipFuel - shipCap) < 1e-6, $"ship holds {shipFuel:0.00} of {shipCap:0.00} t");
            Check(Math.Abs(left + shipFuel - depotFuel - 0.1) < 1e-6, "fuel not conserved");
            // The ship burns some; sharing again tops it up from the depot, but an empty depot never takes fuel back
            foreach (var p in depot.parts) p.propellant = 0;
            Bases.Share(u);
            Check(Math.Abs(ship.parts.Sum(p => p.propellant) - shipCap) < 1e-6, "an empty depot drained the ship");
            return $"ship filled to {shipCap:0.0} t from the depot ({left:0.0} t left there)";
        }

        static string BaseCableBreaks()
        {
            var u = new Universe();
            var a = Module(u, "A", 0, "base.hub");
            var b = Module(u, "B", 30, "base.tank");
            var c = Module(u, "C", -30, "base.tank");
            Bases.Connect(u, a, b);
            Bases.Connect(u, a, c);
            b.landed = false;
            var notes = Bases.Validate(u);
            Check(u.links.Count == 1 && notes.Count == 1 && notes[0].Contains("came loose"), $"after lift-off: {u.links.Count} links, notes {string.Join("; ", notes)}");
            // C ends up 80 m away (rolled off, say)
            var spot = Module(u, "Spot", -80, "base.tank");
            c.pos = spot.pos;
            u.Remove(spot);
            Bases.Validate(u);
            Check(u.links.Count == 0, $"a cable stretched to {Bases.Distance(a, c):0} m held");
            return "loose after lift-off and at 80 m";
        }

        static string BaseCrewAndLab()
        {
            var u = new Universe();
            u.career.Start(GameMode.Career);
            var hub = Module(u, "Hub", 0, "base.hub");
            var lab = Module(u, "Lab", 30, "base.lab", "base.tank");
            var sci = u.career.roster.First(m => m.role == CrewRole.Scientist);
            foreach (var p in hub.parts.Concat(lab.parts)) p.crew.Clear();   // nobody aboard but our scientist
            hub.parts[0].crew.Add(sci.name);
            sci.state = 1; sci.vesselId = hub.id;
            Check(Bases.MoveCrew(u, hub, lab).Contains("not connected") || Bases.MoveCrew(u, hub, lab).Contains("aren't"), "moved crew without a cable");
            Bases.Connect(u, hub, lab);
            string msg = Bases.MoveCrew(u, hub, lab);
            Check(lab.parts.Any(p => p.crew.Contains(sci.name)) && sci.vesselId == lab.id, "crew didn't move: " + msg);
            foreach (var p in lab.parts) p.charge = p.def.ecCapacity;
            foreach (var p in hub.parts) p.charge = p.def.ecCapacity;
            double before = u.career.science;
            for (int i = 0; i < 144; i++) { foreach (var p in lab.parts) p.charge = p.def.ecCapacity; Advance(lab, 600, 600); }   // kept powered
            double gained = u.career.science - before;
            double expect = 2.0 * 1.0 * Tal22.scienceMult * 1.25;
            Check(Math.Abs(gained - expect) < expect * 0.05, $"one day of research gave {gained:0.00}, expected about {expect:0.00}");
            Check(lab.labRate > 0, "lab rate not shown");
            return $"{sci.name} moved over; a day's research {gained:0.0} science";
        }

        static string CrewNaming()
        {
            var u = new Universe();
            u.career.Start(GameMode.Career);
            var c = u.career;
            foreach (var m in c.roster) Check(m.name.EndsWith(" " + CrewNames.Surname), $"starting crew {m.name}");
            int hired = 0;
            for (int i = 0; i < 60; i++)
            {
                c.funds = 1e9;
                if (!c.Hire().StartsWith("Hired")) break;
                hired++;
            }
            Check(hired > 0, "couldn't hire anyone");
            Check(c.roster.Select(m => m.name).Distinct().Count() == c.roster.Count, "two astronauts share a name");
            Check(c.roster.All(m => m.name.Contains(" " + CrewNames.Surname)), "someone isn't an Ardling");
            for (int i = 0; i < 200; i++) Check(CrewNames.Next().EndsWith(" Ardling"), "name format");
            Check(!CrewNames.FirstNames.Contains("Jeb") && !CrewNames.FirstNames.Contains("Bill"), "KSP's famous names");
            return $"{CrewNames.Count} first names; roster of {c.roster.Count}, e.g. {string.Join(", ", c.roster.Take(4).Select(m => m.name))}";
        }

        static string BaseContract()
        {
            var u = new Universe();
            u.career.Start(GameMode.Career);
            var c = u.career;
            var k = new Contract { id = 999, type = "base", title = "Research base on Tal", body = "Tal", seats = 4, amount = 3, partId = "base.lab", state = ContractState.Active, reward = 1000 };
            c.contracts.Add(k);
            var hub = Module(u, "Hub", 0, "base.hub");
            var lab = Module(u, "Lab", 30, "base.lab");
            // A power module: a tank with a solar array and an antenna on its sides
            var pd = VesselDesign.Stack("Power", "probe.spark", "tank.s2");
            int ti = pd.nodes.FindIndex(n => n.id == "tank.s2");
            DesignOps.AttachSurfaceAt(pd, ti, 0.5f, 0f, "solar.l", 1, false);
            DesignOps.AttachSurfaceAt(pd, ti, 0.5f, 180f, "antenna.s", 1, false);
            var tal = Tal22;
            var power = VesselSim.CreateLanded(pd, tal, CelestialBody.DirectionFromLatLon(5, 30 - 30 / (tal.radius * Math.PI / 180)), 0);
            power.hasLaunched = true;
            u.Add(power);
            Check(power.parts.Exists(p => p.def.kind == PartKind.Solar) && power.parts.Exists(p => p.def.kind == PartKind.Antenna), "power module missing parts");
            c.CheckBases(u.vessels, 0, u);
            Check(k.state == ContractState.Active, "completed before the modules were cabled");
            Bases.Connect(u, hub, lab);
            c.CheckBases(u.vessels, 0, u);
            Check(k.state == ContractState.Active, "completed with only two modules (no solar or antenna)");
            Bases.Connect(u, hub, power);
            c.CheckBases(u.vessels, 0, u);
            Check(k.state == ContractState.Completed, $"not completed with 3 cabled modules ({k.state})");
            return "completed once the third module was cabled in";
        }
    }
}
