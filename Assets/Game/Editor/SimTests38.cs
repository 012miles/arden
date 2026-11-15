using System;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 38 tests: health, hunger and stamina; knocked out and revived; the backpack, items and loot; saves.</summary>
    public static partial class SimTests
    {
        static void RunMilestone38()
        {
            Run("Survival: eating fills, medicine heals, damage knocks out at zero, a revive gets them up, and 90 s is too late", VitalsTest);
            Run("Survival over time: on foot the stomach empties in about 18 h; aboard with food it fills; empty hurts, fed heals", HungerTest);
            Run("Stamina: running drains it, resting brings it back; run dry and there's no running until it's back to 30", StaminaTest);
            Run("Falls: landing under 5.5 m/s is safe, faster hurts 12 per m/s over", FallTest);
            Run("Backpack: 4 places, straps make 6 and the big backpack 8; full is refused, with why", BackpackTest);
            Run("Items: food, medicine and a fuel canister used on an Ardling and a ship do what they say", ItemUseTest);
            Run("Loot: 2-5 things, the same for the same seed, common things far more often than very rare ones", LootTest);
            Run("Saves keep each Ardling's numbers, the backpack, the locker and things lying on the worlds; old saves get the starting kit", SurvivalSaveTest);
        }

        static string VitalsTest()
        {
            var v = new Vitals { name = "Iris Ardling", hunger = 40, health = 60 };
            Check(Math.Abs(Survival.Eat(v, 35) - 35) < 1e-3 && Math.Abs(v.hunger - 75) < 1e-3, "noodles: 40 -> 75");
            Survival.Eat(v, 50);
            Check(Math.Abs(v.hunger - 100) < 1e-3, "can't eat past full");
            Survival.Heal(v, 55);
            Check(Math.Abs(v.health - 100) < 1e-3, "a med kit heals to full, no further");
            Check(!Survival.Hurt(v, 40, 10) && Math.Abs(v.health - 60) < 1e-3, "40 damage: still up");
            Check(Survival.Hurt(v, 70, 12) && v.Down && v.health == 0 && Math.Abs(v.downedAt - 12) < 1e-9, "70 more: knocked out at 12 s");
            Check(!Survival.Hurt(v, 10, 13) && Survival.Heal(v, 50) == 0, "knocked out: no more damage, no healing");
            Check(!Survival.TooLate(v, 12 + 89) && Survival.TooLate(v, 12 + 91), "lost after 90 s");
            Survival.Revive(v);
            Check(!v.Down && Math.Abs(v.health - 35) < 1e-3 && v.stamina <= 20, "revived: up at 35 health, worn out");
            return $"health {v.health:0}";
        }

        static string HungerTest()
        {
            var v = new Vitals { name = "Otto Ardling" };
            Survival.Tick(v, 9, false);
            Check(Math.Abs(v.hunger - 50) < 0.5f, $"9 h on foot: half a stomach ({v.hunger:0.0})");
            Survival.Tick(v, 1, true);
            Check(Math.Abs(v.hunger - 100) < 0.5f, "an hour aboard a ship with food: full again");
            v.hunger = 0; v.health = 80;
            Survival.Tick(v, 2, false);
            Check(Math.Abs(v.health - 70) < 0.5f, $"2 h starving: -10 health ({v.health:0.0})");
            v.hunger = 90;
            Survival.Tick(v, 2, false);
            Check(v.health > 75.5f && v.health < 76.5f, $"2 h fed: +6 health ({v.health:0.0})");
            float h0 = v.health;
            Survival.Tick(v, 1, true, resting: true);
            Check(Math.Abs(v.health - h0 - 12) < 0.5f, "resting in a habitat heals four times faster");
            return $"health {v.health:0}";
        }

        static string StaminaTest()
        {
            var v = new Vitals { name = "Iris Ardling" };
            bool ran = Survival.Stamina(v, 1, true);
            Check(ran && Math.Abs(v.stamina - 87) < 0.01f, "a second's running: -13");
            for (int i = 0; i < 7; i++) Survival.Stamina(v, 1, true);   // (87 - 7 x 13 runs it dry)
            Check(v.stamina == 0 && v.tired, "run dry: tired");
            Check(!Survival.Stamina(v, 1, true) && Math.Abs(v.stamina - 8) < 0.01f, "tired: no running, it comes back (+8/s)");
            for (int i = 0; i < 3; i++) Survival.Stamina(v, 1, false);
            Check(v.tired == false && v.stamina >= Survival.StaminaRestart, $"back to 30: running again ({v.stamina:0})");
            v.hunger = 10; float s0 = v.stamina;
            Survival.Stamina(v, 1, false);
            Check(Math.Abs(v.stamina - s0 - 4) < 0.01f, "hungry: stamina comes back at half speed");
            return $"stamina {v.stamina:0}";
        }

        static string FallTest()
        {
            Check(Survival.FallHurt(5) == 0 && Survival.FallHurt(5.5f) == 0, "a normal jump's landing is safe");
            Check(Math.Abs(Survival.FallHurt(10) - 54) < 0.01f, "10 m/s: 54 damage");
            Check(Survival.FallHurt(15) > 100, "15 m/s knocks anyone out");
            return "ok";
        }

        static string BackpackTest()
        {
            var u = CareerUniverse();
            var c = u.career;
            c.Start(GameMode.Career);
            Check(c.backpack.SequenceEqual(new[] { "ration", "bandage" }) && c.backpackSize == 4, "a new game: a ration bar and a bandage, 4 places");
            Check(c.AddToBackpack("medkit", out _) && c.AddToBackpack("fuelcan", out _), "two more fit");
            Check(!c.AddToBackpack("soup", out string why) && why.Contains("full"), "the fifth is refused: " + why);
            Check(c.AddToBackpack("strap", out why) && c.backpackSize == 6 && c.backpack.Count == 4, "straps are worn at once: 6 places");
            Check(c.AddToBackpack("soup", out _), "now the soup fits");
            Check(c.AddToBackpack("bigpack", out _) && c.backpackSize == 8 && !c.AddToBackpack("strap", out why), "the big backpack: 8; straps no use now: " + why);
            Check(!c.AddToBackpack("nonsense", out _), "unknown things are refused");
            return $"{c.backpack.Count}/{c.backpackSize}";
        }

        static string ItemUseTest()
        {
            var v = new Vitals { name = "Iris Ardling", hunger = 30, health = 40, stamina = 10 };
            string r = Items.UseOn(Items.Get("stew"), v, null, null);
            Check(r != null && Math.Abs(v.hunger - 78) < 1e-3, "stew: +48 food");
            Items.UseOn(Items.Get("chococube"), v, null, null);
            Check(Math.Abs(v.stamina - 50) < 1e-3, "a choco cube: +40 stamina");
            Check(Items.UseOn(Items.Get("medkit"), v, null, null) != null && Math.Abs(v.health - 95) < 1e-3, "med kit: +55");
            v.health = 100;
            Check(Items.UseOn(Items.Get("bandage"), v, null, null) == null, "full health: the bandage isn't used up");
            var ship = VesselSim.CreateLanded(Hopper(), System4[0], CelestialBody.DirectionFromLatLon(5, 10), 0);
            foreach (var p in ship.parts) if (p.def.propellant > 0 && !p.def.IsSolid) p.propellant = 0;
            double before = ship.parts.Sum(p => p.propellant);
            r = Items.UseOn(Items.Get("fuelcan"), v, ship, null);
            double after = ship.parts.Sum(p => p.propellant);
            Check(r != null && Math.Abs(after - before - 0.25) < 1e-6, $"a fuel canister: +250 kg into the ship ({r})");
            foreach (var p in ship.parts) if (p.def.propellant > 0 && !p.def.IsSolid) p.propellant = p.def.propellant;
            Check(Items.UseOn(Items.Get("fuelcan"), v, ship, null) == null, "full tanks: the canister's kept");
            Check(Items.UseOn(Items.Get("fuelcan"), v, null, null) == null, "no ship in reach: kept");
            return r;
        }

        static string LootTest()
        {
            var a = Items.Loot(1234);
            var b = Items.Loot(1234);
            Check(a.Count >= 2 && a.Count <= 5 && a.SequenceEqual(b), "2-5 things, the same each time for one seed: " + string.Join(", ", a));
            int common = 0, veryRare = 0;
            for (int s = 0; s < 400; s++)
                foreach (var id in Items.Loot(s, 5)) { var d = Items.Get(id); Check(d != null, "a real item"); if (d.rarity == 0) common++; if (d.rarity == 3) veryRare++; }
            Check(common > veryRare * 8 && veryRare > 0, $"common {common}, very rare {veryRare}");
            return $"common {common}, very rare {veryRare}";
        }

        static string SurvivalSaveTest()
        {
            var u = CareerUniverse();
            var c = u.career;
            var vit = c.VitalsOf("Iris Ardling");
            vit.health = 42; vit.hunger = 13; vit.stamina = 7;
            c.backpack.Clear(); c.backpack.Add("geode"); c.backpack.Add("medkit");
            c.locker.Add("fuelcan"); c.backpackSize = 6; c.lockerSize = 24;
            var tal = System4.First(b => b.name == "Tal");
            u.AddItem("coin", tal, CelestialBody.DirectionFromLatLon(3, 4) * (tal.radius + 10), 45);
            var back = new Universe();
            back.LoadJson(u.ToJson(), System4);
            var bv = back.career.VitalsOf("Iris Ardling");
            Check(Math.Abs(bv.health - 42) < 1e-3 && Math.Abs(bv.hunger - 13) < 1e-3 && Math.Abs(bv.stamina - 7) < 1e-3, "the numbers are kept");
            Check(back.career.backpack.SequenceEqual(new[] { "geode", "medkit" }) && back.career.locker.SequenceEqual(new[] { "fuelcan" }), "the backpack and locker are kept");
            Check(back.career.backpackSize == 6 && back.career.lockerSize == 24, "their sizes too");
            var w = back.items.SingleOrDefault();
            Check(w != null && w.id == "coin" && w.body == "Tal" && Math.Abs(w.Pos.magnitude - (tal.radius + 10)) < 1e-3 && back.nextItemUid > w.uid, "the coin lying on Tal is kept");
            // a save from before milestone 38 (no survival fields): the starting kit
            var old = JsonUtility.FromJson<CareerSave>(JsonUtility.ToJson(c.ToSave()));
            old.survivalVersion = 0; old.backpack = null; old.vitals = null;
            var oc = new Career();
            oc.Load(old);
            Check(oc.backpack.SequenceEqual(new[] { "ration", "bandage" }), "an older save starts with the kit");
            return "kept";
        }
    }
}
