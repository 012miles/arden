using System;
using System.IO;
using UnityEngine;

namespace Rocket
{
    /// <summary>One of the player's own presets: a rocket saved into a slot, and when.</summary>
    [Serializable]
    public class PresetSlotFile
    {
        public string saved;
        public VesselDesign design;
    }

    /// <summary>
    /// Milestone 34 (captaincarl24: "save your rocket and load it as your own custom presets ... they get one preset and
    /// can unlock more using their money"): up to 12 slots for your own rockets, beside the ready-made ones. The first
    /// slot is free; in a campaign each further slot is bought with funds (5,000 for the second, 2,500 more for each one
    /// after). A sandbox has every slot open. The rockets in the slots are kept on this computer and shared between
    /// games, like saved designs; which slots are open belongs to each campaign.
    /// </summary>
    public static class PresetStore
    {
        public const int MaxSlots = 12;

        static string Dir => Path.Combine(Application.persistentDataPath, "MyPresets");
        static PresetSlotFile[] cache;

        /// <summary>What slot <paramref name="slot"/> (1 to 12) costs to open. The first is free.</summary>
        public static double SlotPrice(int slot) => slot <= 1 ? 0 : 2500.0 * slot;

        /// <summary>How many slots this game has open.</summary>
        public static int OpenSlots(Career c) => c == null || !c.IsCareer ? MaxSlots : Mathf.Clamp(c.presetSlots, 1, MaxSlots);

        /// <summary>Buys the next slot. False (and why) if it's not the next one, there's no money, or it's a sandbox.</summary>
        public static bool BuyNext(Career c, out string why)
        {
            why = null;
            if (c == null || !c.IsCareer) { why = "Every slot is open in a sandbox."; return false; }
            int next = OpenSlots(c) + 1;
            if (next > MaxSlots) { why = "All 12 slots are open."; return false; }
            double price = SlotPrice(next);
            if (c.funds < price) { why = $"Slot {next} costs {price:N0} funds: you have {c.funds:N0}."; return false; }
            c.funds -= price;
            c.presetSlots = next;
            return true;
        }

        static string PathOf(int slot) => Path.Combine(Dir, $"slot{slot}.json");

        /// <summary>The rocket in a slot, or null if it's empty.</summary>
        public static PresetSlotFile Get(int slot)
        {
            if (slot < 1 || slot > MaxSlots) return null;
            if (cache == null)
            {
                cache = new PresetSlotFile[MaxSlots + 1];
                for (int i = 1; i <= MaxSlots; i++)
                {
                    try
                    {
                        if (!File.Exists(PathOf(i))) continue;
                        var f = JsonUtility.FromJson<PresetSlotFile>(File.ReadAllText(PathOf(i)));
                        if (f?.design?.nodes != null && f.design.nodes.Count > 0) cache[i] = f;
                    }
                    catch (Exception e) { Debug.LogWarning($"Couldn't read preset slot {i}: {e.Message}"); }
                }
            }
            return cache[slot];
        }

        /// <summary>Saves a rocket into a slot (over whatever was there).</summary>
        public static void Put(int slot, VesselDesign d)
        {
            if (slot < 1 || slot > MaxSlots || d == null) return;
            var f = new PresetSlotFile { saved = DateTime.Now.ToString("MMM d, HH:mm"), design = d.Clone() };
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(PathOf(slot), JsonUtility.ToJson(f, true));
            }
            catch (Exception e) { Debug.LogWarning($"Couldn't save preset slot {slot}: {e.Message}"); }
            Get(1);
            cache[slot] = f;
        }

        /// <summary>Empties a slot.</summary>
        public static void Clear(int slot)
        {
            if (slot < 1 || slot > MaxSlots) return;
            try { if (File.Exists(PathOf(slot))) File.Delete(PathOf(slot)); } catch { }
            Get(1);
            cache[slot] = null;
        }

        /// <summary>Forgets what was read, so the slots are read again (tests).</summary>
        public static void Reload() => cache = null;
    }
}
