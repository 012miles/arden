using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 19: the builder showing how long a ship's supplies last; a capsule in orbit running low (warning, the
        /// events list), running out (the two-day countdown), and the crew lost when time runs on.
        /// </summary>
        IEnumerator SuppliesTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            u.career.suppliesOn = true;

            // The builder: a capsule with a supply crate
            var d = VesselDesign.Stack("Long Hop", "chute.s", "pod.wren", "dec.s", "tank.s2", "eng.ember", "dec.s", "tank.s4", "tank.s4", "tank.s2", "fins.s", "eng.kestrel");
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s2"), 0.5f, 0f, "sup.s", 2, false);
            DesignOps.ApplyDefaultGroups(d);
            root.currentDesign = d;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("270_builder_supplies");
            Log($"builder: {Supplies.DaysText(Supplies.DaysFor(d))} of supplies for {d.nodes.Sum(n => PartCatalog.Get(n.id)?.crew ?? 0)}");

            // In orbit with less than a day left
            u.ut = TimeWithSun(home, CelestialBody.DirectionFromLatLon(0, 30), 0.6, 0);
            var cap = Fly(VesselDesign.Stack("Wren 7", "chute.s", "pod.wren", "tank.s1", "eng.ember"), home, 200_000, 0, 30, u.ut, true);
            var f = root.flight;
            foreach (var p in cap.parts) p.supplies = Math.Min(p.supplies, 0.8);
            yield return new WaitForSecondsRealtime(1.5f);
            f.SetCamera(160f, 12f, 14f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("271_low_supplies");
            f.ShowEvents(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("272_events_supplies");
            f.ShowEvents(false);
            Log($"low: {Supplies.DaysText(cap.SupplyDaysLeft())} left");

            // Run out: the countdown starts
            foreach (var p in cap.parts) p.supplies = 0;
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("273_out_of_supplies");
            Log($"out: starving since {cap.starvingSince:0}, crew {cap.CrewCount}");

            // Let two days go by
            string who = cap.parts.SelectMany(p => p.crew).FirstOrDefault();
            f.WarpTo(u.ut + Supplies.Grace + 3600, "two days later");
            yield return WaitFor(() => !f.WarpingToTarget, 30, "the warp");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("274_crew_lost");
            Log($"after the warp: crew {cap.CrewCount}, {who} lost: {u.career.roster.Find(m => m.name == who)?.state == 2}");
        }
    }
}
