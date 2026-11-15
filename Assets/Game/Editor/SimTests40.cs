using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 40 tests: rogue drones, quests, traders, the mystery machine.</summary>
    public static partial class SimTests
    {
        static void RunMilestone40()
        {
            Run("Drones: notice an Ardling in sight (crouching halves it), chase, zap on a cooldown, give up when led too far from home, go home, and drop when hit enough", DroneTest);
            Run("Guards: 1-3 drones at a random place, a warden and two scouts at the big outposts, the same each game", GuardsTest);
            Run("Quests: three a day and two bigger a week, the same for everyone that day; progress counts up and stops at the goal; claiming pays once; kept in saves", QuestsTest);
            Run("Traders and the mystery machine: six things a day per trader, a little dearer than home; valuables sell for more; rare things come out of the machine far more often than loot", TradeTest);
        }

        static string DroneTest()
        {
            double R = 200_000;
            Vector3d home = new Vector3d(R, 0, 0);
            var d = new Drone { uid = 1, kind = DroneKind.Scout, body = "Tal", health = 30, state = DroneState.Idle };
            d.Pos = home; d.Home = home;
            Vector3d side = new Vector3d(0, 0, 1);
            var far = new List<(string, Vector3d, bool)> { ("Iris Ardling", home + side * 25, true) };
            Drones.Step(d, 0.1f, far, R);
            Check(d.state == DroneState.Idle && d.target == null, "crouched 25 m away: unseen (sight 32, halved)");
            var near = new List<(string, Vector3d, bool)> { ("Iris Ardling", home + side * 25, false) };
            Drones.Step(d, 0.1f, near, R);
            Check(d.state == DroneState.Alert && d.target == "Iris Ardling", "standing: noticed");
            for (int i = 0; i < 16; i++) Drones.Step(d, 0.1f, near, R);
            Check(d.state == DroneState.Chase, "then (after a moment to react) gives chase");
            double before = (d.Pos - (home + side * 25)).magnitude;
            for (int i = 0; i < 10; i++) Drones.Step(d, 0.1f, near, R);
            Check((d.Pos - (home + side * 25)).magnitude < before - 2, "closing in at its speed");
            // right beside her: zaps, then waits for its cooldown
            var close = new List<(string, Vector3d, bool)> { ("Iris Ardling", d.Pos + side * 1, false) };
            var hit = Drones.Step(d, 0.05f, close, R);
            Check(hit != null && hit.Value.who == "Iris Ardling" && Math.Abs(hit.Value.dmg - 5) < 1e-3, "zap: 5");
            Check(Drones.Step(d, 0.05f, close, R) == null, "then a cooldown");
            // led more than 70 m from home: gives up and goes home
            var lured = new List<(string, Vector3d, bool)> { ("Iris Ardling", home + side * 90, false) };
            for (int i = 0; i < 40 && d.state != DroneState.GiveUp; i++) Drones.Step(d, 0.1f, lured, R);
            Check(d.state == DroneState.GiveUp && d.target == null, "too far from home: gives up");
            var none = new List<(string, Vector3d, bool)>();
            for (int i = 0; i < 400 && d.state != DroneState.Idle; i++) Drones.Step(d, 0.1f, none, R);
            Check(d.state == DroneState.Idle && (d.Pos - home).magnitude < 3, "and floats home");
            Check(Math.Abs(d.Pos.magnitude - (R + Drones.Hover + d.Def.size * 0.5)) < 1e-6, "hovering above the ground");
            Check(!Drones.Damage(d, 20) && Drones.Damage(d, 20) && d.state == DroneState.Down, "two zaps knock a scout out");
            Check(Drones.Step(d, 0.1f, near, R) == null, "a downed drone does nothing");
            var leak = new Drone { kind = DroneKind.Leaker, health = 60, state = DroneState.Idle };
            leak.Pos = home; leak.Home = home;
            var by = new List<(string, Vector3d, bool)> { ("Otto Ardling", home + side * 2, true) };
            var aura = Drones.Step(leak, 1f, by, R);
            Check(aura != null && aura.Value.dmg > 0, "a leaker hurts anyone close, even unnoticed");
            return "ok";
        }

        static string GuardsTest()
        {
            var big = Drones.Guards("tal.camp", 7);
            Check(big[0] == DroneKind.Warden && big.Count(k => k == DroneKind.Scout) == 2 && big.Count(k => k == DroneKind.Shiftling) == 1 && big.Count == 4, "a big outpost: a warden and two scouts (and since milestone 42 a Shiftling among them)");
            for (int s = 0; s < 30; s++)
            {
                var g = Drones.Guards("site.tal." + (s % 8), s);
                Check(g.Count >= 1 && g.Count <= 3 && !g.Contains(DroneKind.Warden), "a random place: 1-3, no warden");
                Check(g.SequenceEqual(Drones.Guards("site.tal." + (s % 8), s)), "the same each time");
            }
            return string.Join(",", Drones.Guards("site.tal.3", 5));
        }

        static string QuestsTest()
        {
            var day = Quests.Make(320, false);
            Check(day.Count == 3 && day.Select(q => q.kind).Distinct().Count() == 3 && day.All(q => !q.weekly && q.need > 0 && q.reward >= 100), "three daily quests: " + string.Join(" / ", day.Select(q => q.text)));
            Check(Quests.Make(320, false).Select(q => q.text).SequenceEqual(day.Select(q => q.text)), "the same for everyone that day");
            var week = Quests.Make(45, true);
            Check(week.Count == 2 && week.All(q => q.weekly), "two weekly quests: " + string.Join(" / ", week.Select(q => q.text)));
            var u = CareerUniverse();
            var c = u.career;
            c.Start(GameMode.Career);
            var t0 = new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);
            Check(c.RefreshQuests(t0) && c.quests.Count == 5, "a new game gets 3 + 2");
            Check(!c.RefreshQuests(t0.AddHours(5)), "the same day: no change");
            var q0 = c.quests[0];
            bool done = false;
            for (int i = 0; i < q0.need + 5; i++) done |= c.NoteQuest(q0.kind, 1);
            Check(q0.Done && q0.have == q0.need && done, "counted up to the goal and no further");
            double f = c.funds;
            string got = c.ClaimQuest(q0);
            Check(got != null && Math.Abs(c.funds - f - q0.reward) < 1e-6 && q0.claimed && c.ClaimQuest(q0) == null, "claimed once: " + got);
            Check(c.backpack.Contains(q0.item) || c.locker.Contains(q0.item), "the reward's thing is in the backpack or locker");
            Check(c.NoteLanding("Tal") && !c.NoteLanding("Tal") || !c.quests.Any(q => q.kind == QuestKind.Land), "a world counts once for landing quests");
            c.cleared.Add("site.tal.2");
            var back = new Universe();
            back.LoadJson(u.ToJson(), System4);
            Check(back.career.quests.Count == 5 && back.career.quests[0].claimed && back.career.cleared.Contains("site.tal.2") && back.career.questDay == c.questDay, "quests and cleared places are kept");
            Check(c.RefreshQuests(t0.AddDays(1)) && c.quests.Count(q => !q.weekly) == 3 && !c.quests.Any(q => !q.weekly && q.claimed), "a new day: three new daily quests");
            return string.Join(" / ", day.Select(q => q.text));
        }

        static string TradeTest()
        {
            var s1 = FlightMode.TraderStock("tal.camp", 320);
            Check(s1.Count == 6 && s1.Distinct().Count() == 6 && s1.SequenceEqual(FlightMode.TraderStock("tal.camp", 320)), "six things, the same all day");
            Check(!s1.SequenceEqual(FlightMode.TraderStock("tal.camp", 321)) || !s1.SequenceEqual(FlightMode.TraderStock("tal.camp", 322)), "a new stock on other days");
            Check(s1.All(id => Items.Get(id).kind != ItemKind.Valuable), "traders don't sell valuables");
            var med = Items.Get("medkit");
            Check(FlightMode.TraderPrice(med) > med.price && FlightMode.TraderBuys(med) < med.price, $"dearer to buy ({FlightMode.TraderPrice(med)}), less to sell ({FlightMode.TraderBuys(med)})");
            var geo = Items.Get("geode");
            Check(FlightMode.TraderBuys(geo) > geo.Sell, $"valuables fetch more out here ({FlightMode.TraderBuys(geo)} vs {geo.Sell})");
            int rare = 0, n = 2000;
            for (int s = 0; s < n; s++) if (Items.Get(Items.Mystery(s)).rarity >= 2) rare++;
            int lootRare = 0;
            for (int s = 0; s < n / 5; s++) foreach (var id in Items.Loot(s, 5)) if (Items.Get(id).rarity >= 2) lootRare++;
            Check(rare > lootRare * 1.5, $"the machine: {rare * 100 / n}% rare or better, loot {lootRare * 100 / n}%");
            Check(Items.Get("zapper") != null && Items.Get("dronecore")?.kind == ItemKind.Valuable, "the zapper and drone cores exist");
            return $"{rare * 100 / n}% rare from the machine";
        }
    }
}
