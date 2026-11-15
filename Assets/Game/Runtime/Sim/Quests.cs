using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    /// <summary>
    /// Milestone 40: daily and weekly quests (the quest board at the space centre). Three new ones each day and two bigger
    /// ones each week (real days and weeks, the same for everyone on that day), each paying funds and something for the
    /// backpack. Progress counts up as you play: walking, searching places, eating, picking things up, downing drones,
    /// planting flags, landing, selling, driving and riding out hazards.
    /// </summary>
    public enum QuestKind { Walk, Search, Eat, PickUp, Drones, Flag, Land, Sell, Drive, Survive }

    [Serializable]
    public class Quest
    {
        public QuestKind kind;
        public string text, item;
        public int need, have, reward;
        public bool weekly, claimed;
        public bool Done => have >= need;
    }

    public static class Quests
    {
        static readonly (QuestKind kind, string text, int lo, int hi, int perUnit)[] Kinds =
        {
            (QuestKind.Walk, "Walk {0} m on foot", 300, 1500, 1),
            (QuestKind.Search, "Search {0} places (outposts, wrecks, caches...)", 1, 3, 400),
            (QuestKind.Eat, "Eat {0} things", 2, 6, 60),
            (QuestKind.PickUp, "Pick up {0} things", 3, 12, 50),
            (QuestKind.Drones, "Knock out {0} rogue drones", 1, 6, 250),
            (QuestKind.Flag, "Plant {0} flags", 1, 3, 300),
            (QuestKind.Land, "Land on {0} different worlds", 1, 2, 900),
            (QuestKind.Sell, "Sell {0} worth of finds", 200, 1500, 1),
            (QuestKind.Drive, "Drive a rover {0} km", 1, 8, 250),
            (QuestKind.Survive, "Ride out {0} hazards (storms, meteors, flares...)", 1, 3, 350),
        };

        static readonly string[] RewardItems = { "medkit", "revive", "fuelcan", "repairkit", "stew", "scanner", "battery", "coolantcan", "chococube", "geode" };

        public static int DayNumber(DateTime utc) => (int)(utc.Date - new DateTime(2026, 1, 1)).TotalDays;
        public static int WeekNumber(DateTime utc) => (int)Math.Floor(DayNumber(utc) / 7.0);

        /// <summary>The day's three (or the week's two, bigger) quests.</summary>
        public static List<Quest> Make(int number, bool weekly)
        {
            var rng = new Random(number * (weekly ? 7919 : 104729) + (weekly ? 13 : 7));
            var list = new List<Quest>();
            var kinds = Kinds.OrderBy(_ => rng.Next()).Take(weekly ? 2 : 3).ToList();
            foreach (var k in kinds)
            {
                int need = k.lo + rng.Next(k.hi - k.lo + 1);
                if (k.kind == QuestKind.Walk || k.kind == QuestKind.Sell) need = need / 50 * 50;
                if (weekly) need = (int)Math.Ceiling(need * 3.0);
                if (k.kind == QuestKind.Land) need = Math.Min(need, weekly ? 3 : 1);
                int reward = Math.Max(100, need * k.perUnit / (k.perUnit == 1 ? 2 : 1)) * (weekly ? 2 : 1);
                list.Add(new Quest { kind = k.kind, text = string.Format(k.text, need), need = need, reward = reward / 10 * 10, weekly = weekly, item = RewardItems[rng.Next(RewardItems.Length)] });
            }
            return list;
        }

        /// <summary>Counts something done towards every quest of that kind (not past what it needs).</summary>
        public static bool Note(IEnumerable<Quest> quests, QuestKind kind, int amount)
        {
            bool finished = false;
            foreach (var q in quests)
            {
                if (q.kind != kind || q.claimed || q.Done || amount <= 0) continue;
                q.have = Math.Min(q.need, q.have + amount);
                if (q.Done) finished = true;
            }
            return finished;
        }
    }
}
