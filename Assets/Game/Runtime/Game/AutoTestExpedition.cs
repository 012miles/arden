using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 34: the Expedition on the real game. Hosts an expedition room on its own, builds a Tal lander, checks
        /// the plan and the six resources, launches with the player aboard, sets the ship down beside Camp Ridge on Tal,
        /// scavenges it, walks out in first and third person, then ends the expedition and reads the report.
        /// </summary>
        IEnumerator ExpeditionTour()
        {
            var root = GameRoot.I;
            PlayerPrefs.SetString("mdName", "Mia");
            var room = NetRoom.Host("Mia", "Tal trip", null, 2, false);
            root.EnterCoop(room);
            yield return new WaitForSecondsRealtime(1.5f);
            var c = Coop.Current;
            var hg = root.hangar;
            Log($"expedition room: expedition={c?.Expedition} can build={c?.CanBuild} watching={hg.spectate}");
            yield return Shot("600_expedition_plan_empty");

            var lander = Presets.All().First(p => p.label.StartsWith("Tal Lander")).design.Clone();
            hg.SetDesignFromNet(lander);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("601_expedition_plan_lander");
            var dash = Expedition.Evaluate(hg.design, Expedition.Scenario("tal"), 1, 1);
            Log($"dashboard: cost {dash.cost:N0}/{dash.budget:N0}, dv {dash.dv:N0}, twr {dash.twr:0.00}, seats {dash.seats}, days {dash.days:0.#}; blockers: {string.Join(" | ", dash.blockers)}; warnings: {string.Join(" | ", dash.warnings)}");

            c.Launch(hg.design, false, 0);
            yield return new WaitForSecondsRealtime(3f);
            var f = root.flight;
            var u = root.universe;
            var flying = u.vessels.FirstOrDefault(v => v.CrewCount > 0);
            Log($"launched: in flight={root.inFlight}, crew {(flying != null ? string.Join(", ", flying.parts.SelectMany(p => p.crew)) : "none")}, budget {room.State.ex.budget:N0}, cost {room.State.ex.cost:N0}");
            yield return Shot("602_expedition_flight");

            // set the ship down beside Camp Ridge on Tal
            var tal = root.bodies.First(b => b.name == "Tal");
            var camp = Outposts.On(tal).First(l => l.id == "tal.camp");
            Vector3d dir = (camp.dir + CelestialBody.EastAt(camp.dir) * (70.0 / tal.radius)).normalized;
            var ship = VesselSim.CreateLanded(hg.design, tal, dir, u.ut);
            foreach (var p in ship.parts) p.crew.Clear();
            ship.parts.First(p => p.def.crew > 0).crew.Add("Mia");
            ship.hasLaunched = true;
            ship.careerFlags |= VFlags.Space;
            foreach (var p in ship.parts) if (p.def.propellant > 0 && !p.def.IsSolid) p.propellant *= 0.2;   // most of the fuel used getting there
            ship.career = u.career;
            ship.RecomputeMass();
            u.Add(ship);
            if (flying != null) u.Remove(flying);
            root.Fly(ship);
            yield return new WaitForSecondsRealtime(2.5f);
            var near = Outposts.Near(ship, u.ut, out double m);
            Log($"beside the outpost: {(near != null ? near.name : "none")} at {m:0} m");
            f.LookAlong(tal.FixedToInertial(camp.dir, u.ut) * tal.radius - ship.pos, 70f, 0f, 18f);   // the outpost beyond the ship
            yield return Shot("603_expedition_outpost");
            double fuelBefore = ship.parts.Sum(p => p.propellant);
            f.DoScavenge();
            yield return new WaitForSecondsRealtime(1f);
            Log($"scavenged: fuel {fuelBefore:0.00} -> {ship.parts.Sum(p => p.propellant):0.00} t, searched {string.Join(",", u.career.scavenged)}");

            // inside the cabin: the windows look out at the outpost; then round to the seats and the hatch
            f.EnterInterior();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("603b_expedition_cabin_front");
            f.TestCabinLook(180f, -10f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("603c_expedition_cabin_back");
            Log($"cabin: inside={f.inside}");
            f.LeaveInterior();
            yield return new WaitForSecondsRealtime(0.5f);

            // walk out: first person, then third
            var eva = f.GoEva(ship.parts.First(p => p.crew.Count > 0));
            yield return new WaitForSecondsRealtime(2f);
            f.firstPerson = true;
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("604_expedition_first_person");
            f.firstPerson = false;
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("605_expedition_third_person");
            f.PlantFlag();
            yield return new WaitForSecondsRealtime(2.5f);
            Log($"progress: done {string.Join(", ", room.State.ex.done)}; walked {string.Join(",", room.State.ex.walked)}; flags {string.Join(",", room.State.ex.flagged)}");

            f.CoopEndFlight();
            yield return new WaitForSecondsRealtime(2f);
            c.showReport = true;
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("606_expedition_report");
            Log($"report: {room.State.summary}");
            root.LeaveCoop();
            yield return new WaitForSecondsRealtime(1f);
        }
    }
}
