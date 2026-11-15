using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 38: the survival kit. An Ardling out on Tal: the health/food/stamina box; loot scattered round them,
        /// picked up, carried and thrown; the backpack; eating; knocked out with a revive kit (back up at once) and without
        /// (the countdown); the cabin's box; then the supply store at the space centre.
        /// </summary>
        IEnumerator SurvivalTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var tal = root.bodies.First(b => b.name == "Tal");
            var lander = VesselSim.CreateLanded(Presets.TalLander(), tal, CelestialBody.DirectionFromLatLon(2, 25), u.ut);
            foreach (var p in lander.parts) p.crew.Clear();
            var seat = lander.parts.First(p => p.def.crew > 0);
            seat.crew.Add("Iris Ardling");
            lander.hasLaunched = true;
            lander.career = u.career;
            u.Add(lander);
            u.career.backpack.Clear();
            u.career.backpack.AddRange(new[] { "ration", "medkit", "revive", "fuelcan" });
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(2f);
            var f = root.flight;
            var eva = f.GoEva(seat);
            yield return new WaitForSecondsRealtime(2f);
            Log($"on foot: {eva?.EvaName}, vitals {Describe(f.TestVitals("Iris Ardling"))}");
            yield return Shot("760_on_foot_vitals");

            // loot round them
            f.TestDropLoot();
            yield return new WaitForSecondsRealtime(1f);
            Log($"loot nearby: {f.TestItemsNear}");
            yield return Shot("761_loot_scattered");
            f.TestPickNearest();
            yield return new WaitForSecondsRealtime(0.8f);
            Log($"holding: {f.TestCarried}");
            yield return Shot("762_holding");
            f.ItemOp("Iris Ardling", "throw", "");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("763_thrown");
            yield return new WaitForSecondsRealtime(2f);
            Log($"after the throw: holding {f.TestCarried ?? "nothing"}, things nearby {f.TestItemsNear}");

            // the backpack, and eating
            var vit = f.TestVitals("Iris Ardling");
            vit.hunger = 30;
            f.packOpen = true;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("764_backpack");
            f.ItemOp("Iris Ardling", "use", "0");
            yield return new WaitForSecondsRealtime(0.4f);
            f.packOpen = false;
            Log($"ate the ration bar: food {vit.hunger:0}, backpack {string.Join(",", u.career.backpack)}");

            // knocked out: the revive kit gets them up; then again without one
            f.TestHurt("Iris Ardling", 250);
            yield return new WaitForSecondsRealtime(0.5f);
            Log($"knocked out with a kit: {Describe(vit)}, backpack {string.Join(",", u.career.backpack)}");
            f.TestHurt("Iris Ardling", 250);
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"knocked out without one: {Describe(vit)}");
            yield return Shot("765_knocked_out");
            Survival.Revive(vit);
            yield return new WaitForSecondsRealtime(0.5f);

            // crouched, first person
            eva.evaCrouch = true;
            f.firstPerson = true;
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("766_crouched_first_person");
            f.firstPerson = false;
            eva.evaCrouch = false;

            // back in, the cabin's box
            f.BoardNearest();
            yield return new WaitForSecondsRealtime(1.5f);
            f.EnterInterior();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("767_cabin_vitals");
            f.LeaveInterior();

            // the store
            root.ShowCentre();
            yield return new WaitForSecondsRealtime(2f);
            StorePanel.Open = true;
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("768_supply_store");
            StorePanel.Open = false;
            Log("SURVIVAL TOUR DONE");
        }

        static string Describe(Vitals v) => v == null ? "none" : $"health {v.health:0}, food {v.hunger:0}, stamina {v.stamina:0}{(v.Down ? ", KNOCKED OUT" : "")}";
    }
}
