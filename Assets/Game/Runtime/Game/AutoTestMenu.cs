using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 18: the title screen on a first run, the new-game page, a career started from it, back to the title
        /// with Continue, a second (sandbox) game, the load list, and settings.
        /// </summary>
        IEnumerator MenuTour()
        {
            var root = GameRoot.I;
            // Start from no saves at all (the autotest's own folder)
            string dir = Path.Combine(Application.persistentDataPath, "autotest_saves");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            root.menu.Enter();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("250_title_first_run");

            root.menu.ShowPage("new");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("251_new_game");

            root.NewGame("Moon program", GameMode.Career, true);
            yield return new WaitForSecondsRealtime(1.2f);
            Log($"new career: slot {SaveSlots.Current} '{SaveSlots.NameOf(SaveSlots.Current)}', mode {root.universe.career.mode}, design {root.currentDesign.name}, saved {File.Exists(Universe.SavePath)}");
            yield return Shot("252_builder_with_menu_button");
            string career = SaveSlots.Current;

            root.ShowMenu();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("253_title_continue");

            root.NewGame("Sandbox fun", GameMode.Sandbox, false);
            yield return new WaitForSecondsRealtime(0.8f);
            root.ShowMenu();
            root.menu.ShowPage("load");
            yield return new WaitForSecondsRealtime(0.8f);
            Log("saves: " + string.Join(" | ", SaveSlots.List().Select(s => $"{s.name} ({s.mode}, {s.vessels} vessels)")));
            yield return Shot("254_load_list");

            root.menu.ShowPage("settings");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("255_settings");

            root.OpenSlot(career);
            yield return new WaitForSecondsRealtime(1.0f);
            Log($"reopened '{SaveSlots.NameOf(SaveSlots.Current)}': mode {root.universe.career.mode}, funds {root.universe.career.funds:N0}");
            yield return Shot("256_reopened_career");
        }
    }
}
