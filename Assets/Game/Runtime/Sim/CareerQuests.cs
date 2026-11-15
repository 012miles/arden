using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    /// <summary>
    /// Milestone 40: what the game remembers for stage three. The day's and week's quests (and which day and week they
    /// are), the worlds landed on for a land-on-worlds quest, and the places cleared of drones (they stay clear).
    /// </summary>
    public partial class Career
    {
        public readonly List<Quest> quests = new List<Quest>();
        public readonly List<string> cleared = new List<string>();
        public readonly List<string> questLanded = new List<string>();
        public int questDay = -1, questWeek = -1;

        /// <summary>New daily quests on a new day, new weekly ones in a new week (unfinished ones go; claimed or not).</summary>
        public bool RefreshQuests(DateTime utc)
        {
            int day = Quests.DayNumber(utc), week = Quests.WeekNumber(utc);
            bool changed = false;
            if (day != questDay)
            {
                quests.RemoveAll(q => !q.weekly);
                quests.InsertRange(0, Quests.Make(day, false));
                questDay = day;
                changed = true;
            }
            if (week != questWeek)
            {
                quests.RemoveAll(q => q.weekly);
                quests.AddRange(Quests.Make(week, true));
                questWeek = week;
                questLanded.Clear();
                changed = true;
            }
            return changed;
        }

        /// <summary>Counts something towards the quests; true if that finished one (to say so).</summary>
        public bool NoteQuest(QuestKind kind, int amount) => Quests.Note(quests, kind, amount);

        /// <summary>A landing on a world counts once per world (for the land-on-worlds quests).</summary>
        public bool NoteLanding(string body)
        {
            if (string.IsNullOrEmpty(body) || questLanded.Contains(body)) return false;
            questLanded.Add(body);
            return NoteQuest(QuestKind.Land, 1);
        }

        /// <summary>Takes a finished quest's reward: funds (in a career) and its thing (backpack, else locker).</summary>
        public string ClaimQuest(Quest q)
        {
            if (q == null || !q.Done || q.claimed) return null;
            q.claimed = true;
            if (IsCareer) funds += q.reward;
            string where = null;
            if (q.item != null && Items.Get(q.item) != null)
            {
                if (!BackpackFull) { backpack.Add(q.item); where = "your backpack"; }
                else if (!LockerFull) { locker.Add(q.item); where = "the locker"; }
            }
            var d = Items.Get(q.item);
            return (IsCareer ? $"+{q.reward:#,0} funds" : "Done") + (where != null ? $" and a {d.name.ToLowerInvariant()} in {where}" : "") + "!";
        }

        void SaveQuests(CareerSave s)
        {
            s.quests = quests.Select(q => new Quest { kind = q.kind, text = q.text, item = q.item, need = q.need, have = q.have, reward = q.reward, weekly = q.weekly, claimed = q.claimed }).ToList();
            s.cleared = cleared.ToList();
            s.questLanded = questLanded.ToList();
            s.questDay = questDay;
            s.questWeek = questWeek;
            s.questsVersion = 1;
        }

        void LoadQuests(CareerSave s)
        {
            quests.Clear(); cleared.Clear(); questLanded.Clear();
            questDay = questWeek = -1;
            if (s.questsVersion == 0) return;
            if (s.quests != null) quests.AddRange(s.quests.Where(q => q != null));
            if (s.cleared != null) cleared.AddRange(s.cleared);
            if (s.questLanded != null) questLanded.AddRange(s.questLanded);
            questDay = s.questDay;
            questWeek = s.questWeek;
        }

        void ResetQuests() { quests.Clear(); cleared.Clear(); questLanded.Clear(); questDay = questWeek = -1; }
    }
}
