using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 42 tests: Shiftlings, the Hollow, weapons.</summary>
    public static partial class SimTests
    {
        static void RunMilestone42()
        {
            Run("Shiftlings: pass as crew at a distance, turn when an Ardling comes close, chase on foot and claw, walk on the ground (no hovering)", ShiftlingTest);
            Run("The Hollow: phases by health; its slam hurts everyone near on a timer (faster when enraged); calls Shiftlings once hurt; tougher for a team; guards the wrecks", HollowTest);
            Run("Tutorial: the builder starts with nothing but the hangar and each step brings in one more thing; flight steps add the height, the navball, the top bar; the end shows it all", TutorialRevealTest);
            Run("Weapons: a kick without one; the best carried, or the one chosen if still carried; each with its reach, damage and pace; the drops only come from beating them", WeaponsTest);
        }

        static string ShiftlingTest()
        {
            double R = 200_000;
            Vector3d home = new Vector3d(R, 0, 0), side = new Vector3d(0, 0, 1);
            var d = new Drone { uid = 3, kind = DroneKind.Shiftling, body = "Tal", health = 45, state = DroneState.Idle };
            d.Pos = home; d.Home = home;
            var far = new List<(string, Vector3d, bool)> { ("Iris Ardling", home + side * 10, false) };
            for (int i = 0; i < 20; i++) Drones.Step(d, 0.1f, far, R);
            Check(d.state == DroneState.Idle && d.target == null, "10 m away it still passes as crew");
            Check(Math.Abs(d.Pos.magnitude - R) < 1e-6, $"it stands on the ground ({d.Pos.magnitude - R:0.00} m up)");
            Check((d.Pos - home).magnitude > 0.05 && (d.Pos - home).magnitude < 8, "it strolls about its place");
            var near = new List<(string, Vector3d, bool)> { ("Iris Ardling", d.Pos + side * 4, false) };
            Drones.Step(d, 0.1f, near, R);
            Check(d.state == DroneState.Alert && d.target == "Iris Ardling", "4 m away: it turns");
            for (int i = 0; i < 10; i++) Drones.Step(d, 0.1f, near, R);
            Check(d.state == DroneState.Chase || d.state == DroneState.Attack, "then comes at her");
            var close = new List<(string, Vector3d, bool)> { ("Iris Ardling", d.Pos + side * 1, false) };
            var hit = Drones.Step(d, 0.05f, close, R);
            Check(hit != null && Math.Abs(hit.Value.dmg - 8) < 1e-3, "a claw: 8");
            Check(Drones.Get(DroneKind.Shiftling).walker && Drones.Get(DroneKind.Lurker).walker && !Drones.Get(DroneKind.Scout).walker, "Shiftlings and Lurkers walk; drones hover");
            var crouched = new Drone { uid = 4, kind = DroneKind.Shiftling, body = "Tal", health = 45, state = DroneState.Idle };
            crouched.Pos = home; crouched.Home = home;
            Drones.Step(crouched, 0.1f, new List<(string, Vector3d, bool)> { ("Otto Ardling", home + side * 4, true) }, R);
            Check(crouched.state == DroneState.Idle, "crouching, you can get within 4 m without it turning");
            Check(!Drones.Damage(d, 30) && Drones.Damage(d, 30) && d.state == DroneState.Down, "two good hits knock one out");
            return "ok";
        }

        static string HollowTest()
        {
            double R = 200_000;
            Vector3d home = new Vector3d(R, 0, 0), side = new Vector3d(0, 0, 1);
            var def = Drones.Get(DroneKind.Hollow);
            Check(def.boss && def.walker && def.drop == "hollowheart", "the Hollow is a walking boss that drops its heart");
            var d = new Drone { uid = 9, kind = DroneKind.Hollow, body = "Tal", health = def.health, maxHealth = def.health, state = DroneState.Idle, slamT = 3, summonT = 6 };
            d.Pos = home; d.Home = home;
            var them = new List<(string, Vector3d, bool)> { ("Iris Ardling", home + side * 5, false), ("Otto Ardling", home + side * 6.5, false), ("Ana Ardling", home - side * 30, false) };
            var slam = new List<(string who, float dmg)>();
            bool summon = false;
            int slams = 0;
            for (double t = 0; t < 20; t += 0.1)
            {
                slam.Clear();
                Drones.Step(d, 0.1f, them, R, slam, ref summon);
                if (slam.Count > 0) { slams++; Check(slam.Any(s => s.who == "Iris Ardling") && !slam.Any(s => s.who == "Ana Ardling"), "the slam hits those near, not those far"); }
                them[0] = ("Iris Ardling", d.Pos + side * 5, false); them[1] = ("Otto Ardling", d.Pos + side * 6.5, false);
            }
            Check(d.phase == 0 && slams >= 3 && slams <= 5, $"fresh: a slam every 5 s ({slams} in 20 s)");
            Check(!summon, "fresh, it doesn't call Shiftlings");
            Drones.Damage(d, def.health * 0.5f);
            summon = false;
            for (int i = 0; i < 200 && !summon; i++) Drones.Step(d, 0.1f, them, R, slam, ref summon);
            Check(d.phase == 1 && summon, "hurt: it calls its Shiftlings");
            Drones.Damage(d, def.health * 0.3f);
            slams = 0;
            for (int i = 0; i < 200; i++) { slam.Clear(); Drones.Step(d, 0.1f, them, R, slam, ref summon); if (slam.Count > 0) slams++; them[0] = ("Iris Ardling", d.Pos + side * 5, false); }
            Check(d.phase == 2 && slams >= 5, $"nearly down: enraged, slams more often ({slams} in 20 s)");
            Check(Drones.BossHealth(1) == def.health && Drones.BossHealth(4) > def.health * 2, "tougher for a team of four");
            var lair = Drones.Guards("tal.lander", 3);
            Check(lair.Contains(DroneKind.Hollow) && lair.Count(k => k == DroneKind.Shiftling) == 2, "the Lost Lander: the Hollow and two Shiftlings");
            Check(!Drones.Guards("tal.camp", 3).Contains(DroneKind.Hollow) && Drones.Guards("tal.camp", 3).Contains(DroneKind.Shiftling), "Camp Ridge: no boss, but a Shiftling among its staff");
            Check(Drones.Damage(d, 99999) && d.state == DroneState.Down, "beaten");
            return $"{slams} slams in 20 s enraged";
        }

        static string TutorialRevealTest()
        {
            var S = Tutorial.Show.Parts; var St = Tutorial.Show.Stages; var T = Tutorial.Show.TopBar;
            Check(!Tutorial.TestShowsAt(0, S) && !Tutorial.TestShowsAt(0, St) && !Tutorial.TestShowsAt(0, T), "the first step: just the hangar");
            Check(Tutorial.TestShowsAt(1, S) && !Tutorial.TestShowsAt(1, St), "then the parts list, alone");
            Check(Tutorial.TestShowsAt(7, St) && !Tutorial.TestShowsAt(6, St), "the stages come in at their step");
            Check(Tutorial.TestShowsAt(8, T) && !Tutorial.TestShowsAt(7, T), "the top bar (Undo, LAUNCH) at its step");
            for (int i = 1; i < 10; i++)
                foreach (Tutorial.Show f in new[] { S, St, T })
                    Check(!Tutorial.TestShowsAt(i - 1, f) || Tutorial.TestShowsAt(i, f), $"nothing goes away again (step {i}, {f})");
            Check(!Tutorial.TestShowsAt(10, Tutorial.Show.Telemetry) && Tutorial.TestShowsAt(11, Tutorial.Show.Telemetry), "in flight: first just the LAUNCH button, then the height and speed");
            Check(Tutorial.TestShowsAt(12, Tutorial.Show.Navball) && !Tutorial.TestShowsAt(11, Tutorial.Show.Navball), "then the navball");
            Check(Tutorial.TestShowsAt(Tutorial.StepCount - 1, Tutorial.Show.Controls), "the last step shows everything");
            Tutorial.Stop(false);
            Check(Tutorial.BuilderShows(S) && Tutorial.FlightShows(Tutorial.Show.Controls), "with the tutorial off, everything shows");
            return $"{Tutorial.StepCount} steps";
        }

        static string WeaponsTest()
        {
            Check(Weapons.Held(new List<string>(), null).id == "kick", "nothing carried: a kick");
            var pack = new List<string> { "medkit", "wrench", "blaster" };
            Check(Weapons.Held(pack, null).id == "blaster", "the best carried by default: " + Weapons.Held(pack, null).id);
            Check(Weapons.Held(pack, "wrench").id == "wrench", "the one chosen");
            Check(Weapons.Held(pack, "boltrifle").id == "blaster", "one chosen but not carried: the best carried instead");
            foreach (var w in Weapons.All)
            {
                Check(w.damage > 0 && w.reach > 0 && w.every > 0, w.id + " has its numbers");
                if (w.id != "kick") Check(Items.Get(w.id) != null && Items.Get(w.id).price > 0, w.id + " is sold");
            }
            Check(Weapons.Get("boltrifle").reach > Weapons.Get("blaster").reach && Weapons.Get("blaster").reach > Weapons.Get("zapper").reach && !Weapons.Get("wrench").ranged, "reach: rifle > blaster > zapper, the wrench is melee");
            Check(Weapons.Rate(Weapons.Get("wrench")) > Weapons.Rate(Weapons.Kick) * 1.5, "a wrench beats a kick");
            for (int s = 0; s < 500; s++) foreach (var id in Items.Loot(s, 4)) Check(id != "hollowheart" && id != "shiftsample" && id != "dronecore", "drops never turn up as loot: " + id);
            Check(Items.Get("hollowheart").price > Items.Get("shiftsample").price, "a Hollow heart is worth more than a sample");
            return string.Join(", ", Weapons.All.Select(w => $"{w.id} {Weapons.Rate(w):0}/s"));
        }
    }
}
