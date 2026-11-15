using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 42 (captaincarl24: "add new weapons like guns and melee weapons"): what an Ardling on foot can fight with.
    /// Melee: a kick (always), the wrench and the shock baton (it saps stamina too). Ranged: the zapper (from milestone 40),
    /// the blaster (quick bolts) and the bolt rifle (slow, hard and far). Carry one in the backpack and press its number to
    /// hold it; left click uses whatever's held (the best one if nothing is chosen). Bought at the supply store or traders,
    /// and now and then out of the mystery machine.
    /// </summary>
    public class WeaponDef
    {
        public string id, name, verb;
        public bool ranged;
        public float damage, reach, every, cone, drain;
        public Color color;
    }

    public static class Weapons
    {
        static Color C(float r, float g, float b) => new Color(r, g, b);

        public static readonly WeaponDef Kick = new WeaponDef { id = "kick", name = "Kick", verb = "kick", ranged = false, damage = 6, reach = 2.6f, every = 0.6f, cone = 60, color = Color.white };

        public static readonly WeaponDef[] All =
        {
            Kick,
            new WeaponDef { id = "wrench", name = "Wrench", verb = "whack", ranged = false, damage = 13, reach = 2.7f, every = 0.55f, cone = 60, color = C(0.75f, 0.75f, 0.8f) },
            new WeaponDef { id = "baton", name = "Shock baton", verb = "shock", ranged = false, damage = 18, reach = 2.9f, every = 0.8f, cone = 55, drain = 20, color = C(0.4f, 0.8f, 1f) },
            new WeaponDef { id = "zapper", name = "Zapper", verb = "zap", ranged = true, damage = 20, reach = 18, every = 0.8f, cone = 25, color = C(0.5f, 0.9f, 1f) },
            new WeaponDef { id = "blaster", name = "Blaster", verb = "shoot", ranged = true, damage = 11, reach = 30, every = 0.3f, cone = 14, color = C(1f, 0.55f, 0.25f) },
            new WeaponDef { id = "boltrifle", name = "Bolt rifle", verb = "shoot", ranged = true, damage = 42, reach = 60, every = 1.6f, cone = 7, color = C(0.75f, 1f, 0.45f) },
        };

        public static WeaponDef Get(string id) => All.FirstOrDefault(w => w.id == id);
        public static bool IsWeapon(string id) => id != null && id != "kick" && All.Any(w => w.id == id);

        /// <summary>Damage a second, the measure of "best".</summary>
        public static float Rate(WeaponDef w) => w.damage / Mathf.Max(0.1f, w.every);

        /// <summary>What this walker fights with: the one they chose, if they still carry it; else the best they carry; else a kick.</summary>
        public static WeaponDef Held(IList<string> pack, string chosen)
        {
            if (chosen != null && pack != null && pack.Contains(chosen) && Get(chosen) != null) return Get(chosen);
            WeaponDef best = Kick;
            if (pack != null) foreach (var id in pack) { var w = Get(id); if (w != null && Rate(w) > Rate(best)) best = w; }
            return best;
        }
    }
}
