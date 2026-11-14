using System;
using System.IO;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Quicksave (F5) and quickload (F9): the whole universe (every vessel, the career, the survey) plus which
    /// vessel you were flying, in one file next to the main save. Loading puts everything back exactly as it was.
    /// </summary>
    public partial class FlightMode
    {
        public static string QuickPath => SaveSlots.Current != null ? SaveSlots.QuickPath(SaveSlots.Current)
            : Path.Combine(Application.persistentDataPath, Universe.IsAutotest ? "autotest_quicksave.json" : "quicksave.json");
        public static bool HasQuicksave => File.Exists(QuickPath);
        public bool Paused { get => paused; set => paused = value; }

        public bool QuickSave()
        {
            if (active == null || active.destroyed) { Show("Nothing to quicksave."); return false; }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(QuickPath));
                File.WriteAllText(QuickPath, U.ToQuickSave(active));
                U.Save(); // the main save matches it too
                Show($"Quicksaved {active.name}. F9 loads it.");
                return true;
            }
            catch (Exception e) { Show("Couldn't quicksave: " + e.Message); return false; }
        }

        public bool QuickLoad()
        {
            string json = null;
            try { if (HasQuicksave) json = File.ReadAllText(QuickPath); }
            catch (Exception e) { Debug.LogWarning("Couldn't read the quicksave: " + e.Message); }
            if (string.IsNullOrEmpty(json)) { Show("No quicksave yet: F5 saves one."); return false; }

            int wasId = active != null ? active.id : -1;
            var wasDesign = design;
            ClearScene();
            active = null;
            var v = U.LoadQuickSave(json, root.bodies);
            if (v == null) { root.EnterHangar(); return false; }
            BeginExisting(v);
            if (v.id == wasId) design = wasDesign; // same flight: "Revert to launch" still works
            Show($"Quickloaded: {v.name}, {v.Situation(ut)}", 5f);
            return true;
        }
    }
}
