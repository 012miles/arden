using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 40: at Camp Ridge on Tal, the trader's panel; the warden and scouts waking; zapping a scout down (it
        /// drops its core) and kicking another; then the quest board and the mystery machine at the space centre.
        /// </summary>
        IEnumerator DronesTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var tal = root.bodies.First(b => b.name == "Tal");
            var camp = Outposts.On(tal).First(l => l.id == "tal.camp");
            var e = CelestialBody.EastAt(camp.dir);
            var lander = Crewed(VesselSim.CreateLanded(Presets.TalLander(), tal, (camp.dir + e * (6.0 / tal.radius)).normalized, u.ut), u, "Iris Ardling");
            u.career.backpack.Clear();
            u.career.backpack.AddRange(new[] { "geode", "medkit", "zapper" });
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(2f);
            var f = root.flight;
            var eva = f.GoEva(lander.parts.First(p => p.crew.Count > 0));
            yield return new WaitForSecondsRealtime(2.5f);
            f.tradeOpen = true;
            yield return new WaitForSecondsRealtime(0.6f);
            Log($"trade open: {f.tradeOpen}");
            yield return Shot("780_trader");
            f.tradeOpen = false;
            Log($"drones awake at the camp: {f.TestDroneCount}");
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("781_warden_and_scouts");
            var vit = f.TestVitals("Iris Ardling");
            Log($"after 3 s near the warden: Iris {Describe(vit)}");

            // a scout of our own to zap down
            f.TestSpawnDrone(DroneKind.Scout, 8f);
            yield return new WaitForSecondsRealtime(1f);
            var d = f.TestNearestDrone();
            int coresBefore = u.items.Count(w => w.id == "dronecore");
            for (int i = 0; i < 4 && d != null && f.TestDroneCount > 0 && d.state != DroneState.Down; i++) { f.TestAttack(d.uid); yield return new WaitForSecondsRealtime(0.5f); if (i == 0) yield return Shot("782_zap"); }
            yield return new WaitForSecondsRealtime(1f);
            Log($"zapped the nearest drone ({d?.kind}): down {d?.state == DroneState.Down || d?.health <= 0}, cores on the ground {u.items.Count(w => w.id == "dronecore") - coresBefore}");
            yield return Shot("783_core_dropped");
            // no zapper: a kick
            u.career.backpack.Remove("zapper");
            f.TestSpawnDrone(DroneKind.Dart, 1.5f);
            yield return new WaitForSecondsRealtime(0.3f);
            var dart = f.TestNearestDrone();
            float hp0 = dart?.health ?? 0;
            if (dart != null) f.TestAttack(dart.uid);
            Log($"kicked a {dart?.kind}: health {hp0:0} -> {dart?.health:0}");
            Survival.Revive(vit); vit.health = 100;

            // the quest board and the mystery machine
            root.ShowCentre();
            yield return new WaitForSecondsRealtime(2f);
            StorePanel.OpenAt(3);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("784_quest_board");
            Log($"quests: {string.Join(" / ", u.career.quests.Select(q => $"{q.text} {q.have}/{q.need}"))}");
            StorePanel.OpenAt(4);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("785_mystery_machine");
            StorePanel.Open = false;
            Log("DRONES TOUR DONE");
        }
    }
}
