using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    /// <summary>
    /// Milestone 38: what the game remembers about survival. Each Ardling's health, hunger and stamina; your backpack
    /// (4 things, 6 with straps, 8 with the big backpack), which keeps what's in it from one flight to the next; and the
    /// locker at the space centre (12, or 24 once it's extended), where the store puts what doesn't fit.
    /// </summary>
    public partial class Career
    {
        public readonly List<Vitals> vitals = new List<Vitals>();
        public readonly List<string> backpack = new List<string>();
        public readonly List<string> locker = new List<string>();
        public int backpackSize = 4, lockerSize = Items.LockerSmall;

        /// <summary>An Ardling's health, hunger and stamina (made fresh the first time they're asked about).</summary>
        public Vitals VitalsOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var v = vitals.Find(x => x.name == name);
            if (v == null) { v = new Vitals { name = name }; vitals.Add(v); }
            return v;
        }

        public bool BackpackFull => backpack.Count >= backpackSize;
        public bool LockerFull => locker.Count >= lockerSize;

        /// <summary>Into the backpack if there's room. Straps and the big backpack are worn straight away instead.</summary>
        public bool AddToBackpack(string id, out string msg)
        {
            var d = Items.Get(id);
            if (d == null) { msg = "That's not something you can carry."; return false; }
            if (d.kind == ItemKind.Upgrade)
            {
                int size = id == "bigpack" ? 8 : 6;
                if (backpackSize >= size) { msg = $"Your backpack already holds {backpackSize}."; return false; }
                backpackSize = size;
                msg = $"{d.name}: your backpack now holds {size} things.";
                return true;
            }
            if (BackpackFull) { msg = $"Your backpack is full ({backpackSize} things)."; return false; }
            backpack.Add(id);
            msg = $"{d.name} in your backpack.";
            return true;
        }

        /// <summary>A new game's backpack: something to eat and a bandage.</summary>
        void ResetSurvival()
        {
            vitals.Clear();
            backpack.Clear();
            locker.Clear();
            backpack.Add("ration");
            backpack.Add("bandage");
            backpackSize = 4;
            lockerSize = Items.LockerSmall;
        }

        void SaveSurvival(CareerSave s)
        {
            s.vitals = vitals.Select(v => new Vitals { name = v.name, health = v.health, hunger = v.hunger, stamina = v.stamina, downedAt = v.downedAt, tired = v.tired }).ToList();
            s.backpack = backpack.ToList();
            s.locker = locker.ToList();
            s.backpackSize = backpackSize;
            s.lockerSize = lockerSize;
            s.survivalVersion = 1;
        }

        void LoadSurvival(CareerSave s)
        {
            vitals.Clear();
            backpack.Clear();
            locker.Clear();
            if (s.survivalVersion == 0) { ResetSurvival(); return; }   // a save from before milestone 38: start with the kit
            if (s.vitals != null) vitals.AddRange(s.vitals.Where(v => v != null && !string.IsNullOrEmpty(v.name)));
            if (s.backpack != null) backpack.AddRange(s.backpack.Where(id => Items.Get(id) != null));
            if (s.locker != null) locker.AddRange(s.locker.Where(id => Items.Get(id) != null));
            backpackSize = Items.BackpackSizes.Contains(s.backpackSize) ? s.backpackSize : 4;
            lockerSize = s.lockerSize >= Items.LockerBig ? Items.LockerBig : Items.LockerSmall;
        }
    }
}
