using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 38 (captaincarl24: "add the rest of all the systems in the doc"), stage one, the survival kit. Every Ardling
    /// has health, hunger and stamina. On foot the stomach slowly empties (aboard, the crew eat from the ship's supplies);
    /// running uses stamina; falls, heavy g, a burning cabin and an empty stomach hurt. At no health an Ardling is knocked
    /// out: someone with a revive kit has a minute and a half to get them up, or they're lost.
    /// </summary>
    [Serializable]
    public class Vitals
    {
        public string name;
        public float health = Survival.Max, hunger = Survival.Max, stamina = Survival.Max;
        public double downedAt = -1;   // game time they were knocked out (-1: they're fine)
        public bool tired;             // ran out of stamina: no running until it's back to StaminaRestart
        public bool Down => downedAt >= 0;
    }

    public static class Survival
    {
        public const float Max = 100;
        public const double HungerHours = 18;            // a full stomach lasts this long on foot (game hours)
        public const float SprintDrain = 13f, StaminaRegen = 8f, StaminaRestart = 30f;   // per second (real time)
        public const double DownSeconds = 90;            // knocked out this long, then it's too late
        public const float FallSafe = 5.5f, FallDamage = 12f;     // landing faster than this (m/s) hurts, per m/s over
        public const float GSafe = 8f, GDamage = 6f;     // g felt above this hurts, per g per second
        public const float HeatDamage = 7f;              // per second in a cabin that's burning up
        public const float StarveDamage = 5f;            // per game hour on an empty stomach
        public const float RegenPerHour = 3f;            // healing per game hour while fed (more resting in a bunk)
        public const float CrouchSpeed = 0.5f;           // walking speed crouched

        public static float Eat(Vitals v, float amount) { float was = v.hunger; v.hunger = Mathf.Min(Max, v.hunger + amount); return v.hunger - was; }
        public static float Heal(Vitals v, float amount) { if (v.Down) return 0; float was = v.health; v.health = Mathf.Min(Max, v.health + amount); return v.health - was; }

        /// <summary>Takes health; true if that knocked them out.</summary>
        public static bool Hurt(Vitals v, float amount, double now)
        {
            if (v.Down || amount <= 0) return false;
            v.health -= amount;
            if (v.health > 0) return false;
            v.health = 0;
            v.downedAt = now;
            return true;
        }

        public static void Revive(Vitals v, float health = 35f) { if (!v.Down) return; v.downedAt = -1; v.health = Mathf.Max(health, 1f); v.stamina = Mathf.Min(v.stamina, 20f); }

        /// <summary>Knocked out too long: lost.</summary>
        public static bool TooLate(Vitals v, double now) => v.Down && now - v.downedAt > DownSeconds;

        /// <summary>
        /// Game time passing: on foot the stomach empties; aboard a ship that has food (or with supplies switched off) the
        /// crew eat and it fills again. Empty hurts; fed heals slowly (faster resting).
        /// </summary>
        public static void Tick(Vitals v, double hours, bool aboardFed, bool resting = false)
        {
            if (hours <= 0 || v.Down) return;
            if (aboardFed) v.hunger = Mathf.Min(Max, v.hunger + (float)(hours * Max / 2.0));
            else v.hunger = Mathf.Max(0, v.hunger - (float)(hours * Max / HungerHours));
            if (v.hunger <= 0) v.health = Mathf.Max(0.5f, v.health - (float)(hours * StarveDamage));   // (starving alone never quite kills: the supplies countdown does)
            else if (v.hunger > 40) v.health = Mathf.Min(Max, v.health + (float)(hours * RegenPerHour * (resting ? 4 : 1)));
        }

        /// <summary>Real time passing on foot: running drains stamina, anything else brings it back. True if they may run.</summary>
        public static bool Stamina(Vitals v, float dt, bool wantRun)
        {
            bool run = wantRun && !v.tired && v.stamina > 0;
            if (run) { v.stamina = Mathf.Max(0, v.stamina - SprintDrain * dt); if (v.stamina <= 0) v.tired = true; }
            else v.stamina = Mathf.Min(Max, v.stamina + StaminaRegen * dt * (v.hunger < 20 ? 0.5f : 1f));
            if (v.tired && v.stamina >= StaminaRestart) v.tired = false;
            return run;
        }

        /// <summary>Hurt by landing from a jump or fall this fast (m/s straight down).</summary>
        public static float FallHurt(float speed) => speed > FallSafe ? (speed - FallSafe) * FallDamage : 0;
    }

    // ---------------------------------------------------------------- items

    public enum ItemKind { Food, Medical, Supply, Tool, Valuable, Upgrade }

    public class ItemDef
    {
        public string id, name, about;
        public ItemKind kind;
        public float hunger, heal, stamina;   // food and medicine
        public double fuel, mono, ec, supplies, air, coolant;   // tonnes / EC / crew-days / suit hours / litres it adds
        public int price;                     // in the store (funds); sells for half (valuables: their worth)
        public int rarity;                    // 0 common, 1 uncommon, 2 rare, 3 very rare (how often they turn up)
        public Color color = Color.white;
        public string shape = "box";          // box, can, pouch, kit, rock, bar, gem, tube
        public int Sell => kind == ItemKind.Valuable ? price : Math.Max(1, price / 2);
    }

    /// <summary>A thing lying on (or flying over) a world: where it is in the world's own turning frame, and how it's moving.</summary>
    [Serializable]
    public class WorldItem
    {
        public int uid;
        public string id, body;
        public double x, y, z;      // body-fixed position (m)
        public double vx, vy, vz;   // body-fixed velocity while flying
        public float yaw;
        public bool flying;
        public Vector3d Pos { get => new Vector3d(x, y, z); set { x = value.x; y = value.y; z = value.z; } }
        public Vector3d Vel { get => new Vector3d(vx, vy, vz); set { vx = value.x; vy = value.y; vz = value.z; } }
        public ItemDef Def => Items.Get(id);
    }

    /// <summary>A player's backpack in a room (the host keeps everyone's), and what they're holding.</summary>
    [Serializable]
    public class NetPack
    {
        public string name, carried;
        public int size = 4;
        public List<string> items = new List<string>();
    }

    public static class Items
    {
        static Color C(float r, float g, float b) => new Color(r, g, b);

        /// <summary>Our own things to find, buy, carry and use (milestone 38).</summary>
        public static readonly ItemDef[] All =
        {
            // food: how much of an empty stomach each fills
            new ItemDef { id = "ration", name = "Ration bar", kind = ItemKind.Food, hunger = 22, price = 40, rarity = 0, color = C(0.85f, 0.7f, 0.35f), shape = "bar", about = "Dense, dry and filling." },
            new ItemDef { id = "noodles", name = "Space noodles", kind = ItemKind.Food, hunger = 35, price = 60, rarity = 0, color = C(0.95f, 0.85f, 0.4f), shape = "pouch", about = "Just add water. Slurp carefully." },
            new ItemDef { id = "crackers", name = "Tal-cheese crackers", kind = ItemKind.Food, hunger = 14, price = 25, rarity = 0, color = C(0.95f, 0.75f, 0.3f), shape = "box", about = "Crunchy. Tal has no cheese; the name is a joke." },
            new ItemDef { id = "berries", name = "Dried ochre-berries", kind = ItemKind.Food, hunger = 9, stamina = 25, price = 30, rarity = 1, color = C(0.85f, 0.3f, 0.2f), shape = "pouch", about = "Sweet, and a burst of energy." },
            new ItemDef { id = "stew", name = "Freeze-dried stew", kind = ItemKind.Food, hunger = 48, price = 90, rarity = 1, color = C(0.6f, 0.35f, 0.2f), shape = "pouch", about = "A proper meal." },
            new ItemDef { id = "jerky", name = "Vesk jerky", kind = ItemKind.Food, hunger = 26, price = 45, rarity = 0, color = C(0.5f, 0.25f, 0.15f), shape = "bar", about = "Chewy. Very chewy." },
            new ItemDef { id = "fruitpouch", name = "Fruit pouch", kind = ItemKind.Food, hunger = 12, stamina = 10, price = 25, rarity = 0, color = C(0.95f, 0.5f, 0.6f), shape = "pouch", about = "Squeeze and enjoy." },
            new ItemDef { id = "chococube", name = "Choco cube", kind = ItemKind.Food, hunger = 7, stamina = 40, price = 35, rarity = 1, color = C(0.35f, 0.2f, 0.12f), shape = "box", about = "Small, rich, and a big burst of energy." },
            new ItemDef { id = "soup", name = "Hot soup tin", kind = ItemKind.Food, hunger = 40, price = 70, rarity = 1, color = C(0.8f, 0.2f, 0.15f), shape = "can", about = "Heats itself when you pull the tab." },
            new ItemDef { id = "feast", name = "Celebration cake", kind = ItemKind.Food, hunger = 70, stamina = 30, price = 200, rarity = 3, color = C(1f, 0.85f, 0.9f), shape = "box", about = "For very special landings." },
            // medicine
            new ItemDef { id = "bandage", name = "Bandage", kind = ItemKind.Medical, heal = 15, price = 50, rarity = 0, color = C(0.95f, 0.95f, 0.92f), shape = "bar", about = "For scrapes and bumps." },
            new ItemDef { id = "medkit", name = "Med kit", kind = ItemKind.Medical, heal = 55, price = 150, rarity = 1, color = C(0.95f, 0.95f, 0.95f), shape = "kit", about = "Patches up most things." },
            new ItemDef { id = "revive", name = "Revive kit", kind = ItemKind.Medical, heal = 0, price = 400, rarity = 2, color = C(0.95f, 0.25f, 0.2f), shape = "kit", about = "Gets a knocked-out Ardling back on their feet. Used by itself if it's you." },
            // supplies for the ship and suit
            new ItemDef { id = "fuelcan", name = "Fuel canister", kind = ItemKind.Supply, fuel = 0.25, price = 120, rarity = 0, color = C(0.85f, 0.15f, 0.1f), shape = "can", about = "250 kg of rocket fuel. Pour it into a ship within reach." },
            new ItemDef { id = "monocan", name = "Thruster fuel can", kind = ItemKind.Supply, mono = 0.04, price = 90, rarity = 1, color = C(0.95f, 0.75f, 0.1f), shape = "can", about = "40 kg of thruster (RCS) fuel." },
            new ItemDef { id = "battery", name = "Battery pack", kind = ItemKind.Supply, ec = 600, price = 100, rarity = 1, color = C(0.2f, 0.5f, 0.9f), shape = "box", about = "600 EC of electricity for a ship." },
            new ItemDef { id = "supplycrate", name = "Supply crate", kind = ItemKind.Supply, supplies = 3, price = 140, rarity = 0, color = C(0.55f, 0.6f, 0.4f), shape = "box", about = "Three crew-days of food, water and air for a ship." },
            new ItemDef { id = "coolantcan", name = "Coolant canister", kind = ItemKind.Supply, coolant = 40, price = 110, rarity = 1, color = C(0.2f, 0.55f, 0.85f), shape = "can", about = "40 litres of engine coolant, for a ship with a coolant tank." },
            new ItemDef { id = "airtank", name = "Air tank", kind = ItemKind.Supply, air = 6, price = 80, rarity = 0, color = C(0.2f, 0.65f, 0.3f), shape = "tube", about = "Refills your suit's air." },
            // tools
            new ItemDef { id = "repairkit", name = "Repair kit", kind = ItemKind.Tool, price = 180, rarity = 1, color = C(0.9f, 0.55f, 0.1f), shape = "kit", about = "Fixes a broken part within reach. Anyone can use it, not just engineers." },
            new ItemDef { id = "scanner", name = "Scanner", kind = ItemKind.Tool, price = 160, rarity = 1, color = C(0.3f, 0.8f, 0.7f), shape = "box", about = "Shows what's lying about, and outposts, for a minute." },
            new ItemDef { id = "torch", name = "Torch", kind = ItemKind.Tool, price = 60, rarity = 0, color = C(0.95f, 0.8f, 0.2f), shape = "tube", about = "A light for dark places. Use it again to switch it off." },
            new ItemDef { id = "flagpack", name = "Spare flag", kind = ItemKind.Tool, price = 70, rarity = 1, color = C(0.9f, 0.9f, 0.95f), shape = "tube", about = "One more flag to plant." },
            // things worth money
            new ItemDef { id = "meteorite", name = "Meteorite chunk", kind = ItemKind.Valuable, price = 180, rarity = 1, color = C(0.3f, 0.28f, 0.27f), shape = "rock", about = "Fell from space a long time ago. Collectors love them." },
            new ItemDef { id = "probepart", name = "Old probe part", kind = ItemKind.Valuable, price = 120, rarity = 0, color = C(0.75f, 0.7f, 0.4f), shape = "box", about = "From one of the first probes. A museum will pay for it." },
            new ItemDef { id = "geode", name = "Crystal geode", kind = ItemKind.Valuable, price = 420, rarity = 2, color = C(0.6f, 0.4f, 0.85f), shape = "gem", about = "Sparkling crystals inside a dull rock." },
            new ItemDef { id = "patch", name = "Lost mission patch", kind = ItemKind.Valuable, price = 90, rarity = 0, color = C(0.2f, 0.35f, 0.75f), shape = "bar", about = "From a mission nobody remembers." },
            new ItemDef { id = "coin", name = "Ancient Ardling coin", kind = ItemKind.Valuable, price = 900, rarity = 3, color = C(0.95f, 0.8f, 0.3f), shape = "gem", about = "Nobody knows how it got here." },
            // upgrades
            new ItemDef { id = "strap", name = "Backpack straps", kind = ItemKind.Upgrade, price = 500, rarity = 2, color = C(0.25f, 0.3f, 0.4f), shape = "bar", about = "Your backpack holds 6 things instead of 4." },
            new ItemDef { id = "bigpack", name = "Big backpack", kind = ItemKind.Upgrade, price = 1400, rarity = 3, color = C(0.3f, 0.45f, 0.3f), shape = "box", about = "Your backpack holds 8 things." },
        };

        public static ItemDef Get(string id) => All.FirstOrDefault(d => d.id == id);
        public static readonly int[] BackpackSizes = { 4, 6, 8 };
        public const int LockerSmall = 12, LockerBig = 24, LockerUpgradePrice = 1500;
        public const double PickupReach = 2.5, UseReach = 5.0;

        // weights for how often each rarity turns up
        static readonly float[] RarityWeight = { 60, 28, 10, 2 };

        /// <summary>What a search turns up: 2-5 things, picked by rarity (the same each time for the same seed).</summary>
        public static List<string> Loot(int seed, int count = -1)
        {
            var rng = new System.Random(seed);
            if (count < 0) count = 2 + rng.Next(4);
            var pool = All.Where(d => d.kind != ItemKind.Upgrade || rng.NextDouble() < 0.3).ToList();
            var outList = new List<string>();
            for (int i = 0; i < count; i++)
            {
                double total = pool.Sum(d => RarityWeight[d.rarity]);
                double r = rng.NextDouble() * total;
                foreach (var d in pool) { r -= RarityWeight[d.rarity]; if (r <= 0) { outList.Add(d.id); break; } }
                if (outList.Count <= i) outList.Add(pool[0].id);
            }
            return outList;
        }

        /// <summary>
        /// Uses an item (not tools that need the world: those are the flight's job). Food and medicine go to the Ardling;
        /// ship supplies into the nearest ship in reach (or their own). Returns what happened, or null if it can't be used here.
        /// </summary>
        public static string UseOn(ItemDef d, Vitals who, VesselSim ship, VesselSim eva)
        {
            switch (d.kind)
            {
                case ItemKind.Food:
                {
                    float got = Survival.Eat(who, d.hunger);
                    if (d.stamina > 0) who.stamina = Mathf.Min(Survival.Max, who.stamina + d.stamina);
                    return got < 0.5f && d.stamina <= 0 ? null : $"{who.name.Split(' ')[0]} eats the {d.name.ToLowerInvariant()}." + (d.stamina > 0 ? " Energy!" : "");
                }
                case ItemKind.Medical:
                    if (d.id == "revive") return null;   // (only on someone knocked out)
                    if (who.health >= Survival.Max - 0.5f) return null;
                    Survival.Heal(who, d.heal);
                    return $"{who.name.Split(' ')[0]} uses the {d.name.ToLowerInvariant()}. Health {who.health:0}.";
                case ItemKind.Supply:
                {
                    if (d.air > 0)
                    {
                        if (eva == null) return null;
                        double add = eva.AddSupplies(d.air / 24.0);
                        return add > 1e-4 ? "Fresh air in the suit." : null;
                    }
                    if (ship == null) return null;
                    var bits = new List<string>();
                    double fuel = d.fuel, mono = d.mono;
                    foreach (var p in ship.parts)
                    {
                        if (fuel > 0 && p.def.propellant > 0 && !p.def.IsSolid) { double a = Math.Min(fuel, p.def.propellant - p.propellant); if (a > 0) { p.propellant += a; fuel -= a; } }
                        if (mono > 0 && p.def.mono > 0) { double a = Math.Min(mono, p.def.mono - p.mono); if (a > 0) { p.mono += a; mono -= a; } }
                    }
                    if (d.fuel > 0 && fuel < d.fuel - 1e-4) bits.Add($"{(d.fuel - fuel) * 1000:0} kg of fuel");
                    if (d.mono > 0 && mono < d.mono - 1e-4) bits.Add($"{(d.mono - mono) * 1000:0} kg of thruster fuel");
                    if (d.ec > 0) { double before = ship.EcStored; ship.AddCharge(d.ec); if (ship.EcStored > before + 0.5) bits.Add($"{ship.EcStored - before:0} EC"); }
                    if (d.supplies > 0 && ship.SuppliesCapacity > 0) { double a = ship.AddSupplies(d.supplies); if (a > 0.01) bits.Add($"{a:0.0} crew-days of supplies"); }
                    if (d.coolant > 0) { double a = ship.AddCoolant(d.coolant); if (a > 0.5) bits.Add($"{a:0} L of coolant"); }   // milestone 39
                    if (bits.Count == 0) return null;
                    ship.RecomputeMass();
                    return $"Into {ship.name}: {string.Join(", ", bits)}.";
                }
                case ItemKind.Upgrade: return null;   // (the backpack takes these)
                default: return null;
            }
        }
    }
}
