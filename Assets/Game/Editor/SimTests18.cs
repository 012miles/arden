using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 18 tests: save slots.</summary>
    public static partial class SimTests
    {
        static void RunMilestone18()
        {
            Run("Save slots: several games side by side, listed with their mode and progress, deleted on request", SaveSlotsBasics);
            Run("Save slots: the old single save becomes 'Game 1', with its quicksave", SaveSlotsMigrate);
        }

        static string TempRoot()
        {
            string d = Path.Combine(Path.GetTempPath(), "rocket_slots_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(d);
            return d;
        }

        static string SaveSlotsBasics()
        {
            string oldRoot = SaveSlots.Root, oldCur = SaveSlots.Current;
            SaveSlots.Root = TempRoot();
            try
            {
                Check(SaveSlots.List().Count == 0, "a fresh folder has saves in it");
                string a = SaveSlots.Create("Moon program"), b = SaveSlots.Create("");
                Check(a != b && SaveSlots.NameOf(a) == "Moon program" && SaveSlots.NameOf(b) == "Game 2", $"names {SaveSlots.NameOf(a)} / {SaveSlots.NameOf(b)}");
                // Write a career universe into A and a sandbox one into B
                var ua = new Universe(); ua.career.Start(GameMode.Career); ua.career.funds = 123_456; ua.ut = 86400 * 4.5;
                ua.Add(VesselSim.CreateFree(D("probe", "probe.spark"), System4[0], new Vector3d(System4[0].radius + 200_000, 0, 0), new Vector3d(0, 0, -2300), Quaternion.identity));
                File.WriteAllText(SaveSlots.UniversePath(a), ua.ToJson());
                var ub = new Universe(); ub.career.Start(GameMode.Sandbox);
                File.WriteAllText(SaveSlots.UniversePath(b), ub.ToJson());
                System.Threading.Thread.Sleep(20);
                File.SetLastWriteTime(SaveSlots.UniversePath(a), DateTime.Now.AddMinutes(1));   // A was played last
                var list = SaveSlots.List();
                Check(list.Count == 2 && list[0].id == a, "not listed newest first");
                var ia = list.First(s => s.id == a);
                Check(ia.mode == GameMode.Career && ia.chosen && Math.Abs(ia.funds - 123_456) < 1 && ia.vessels == 1 && Math.Abs(ia.ut - 86400 * 4.5) < 1, $"slot A reads {ia.mode} {ia.funds} funds {ia.vessels} vessels day {ia.ut / 86400}");
                Check(list.First(s => s.id == b).mode == GameMode.Sandbox, "slot B isn't sandbox");
                // The universe loads back from the current slot
                SaveSlots.Current = a;
                Check(Universe.SavePath == SaveSlots.UniversePath(a), "the save path doesn't follow the current slot");
                var back = new Universe();
                back.LoadJson(File.ReadAllText(Universe.SavePath), System4);
                Check(back.vessels.Count == 1 && Math.Abs(back.career.funds - 123_456) < 1, "slot A didn't load back");
                // Last played, rename, delete
                SaveSlots.Last = a;
                Check(SaveSlots.Last == a, "last played not remembered");
                SaveSlots.Rename(b, "Sandbox fun");
                Check(SaveSlots.NameOf(b) == "Sandbox fun", "rename failed");
                SaveSlots.Delete(a);
                Check(!Directory.Exists(SaveSlots.SlotDir(a)) && SaveSlots.Last == null && SaveSlots.Current == null, "delete left something behind");
                Check(SaveSlots.List().Count == 1, "the other game went too");
                SaveSlots.Delete("../evil");
                Check(SaveSlots.List().Count == 1, "a path outside the saves folder was touched");
                return $"two games, newest first, career/sandbox details read back, rename and delete work";
            }
            finally { SaveSlots.Root = oldRoot; SaveSlots.Current = oldCur; }
        }

        static string SaveSlotsMigrate()
        {
            string oldRoot = SaveSlots.Root, oldCur = SaveSlots.Current;
            SaveSlots.Root = TempRoot();
            try
            {
                string uni = Path.Combine(SaveSlots.Root, "universe.json"), quick = Path.Combine(SaveSlots.Root, "quicksave.json");
                var u = new Universe(); u.career.Start(GameMode.Career); u.career.funds = 77_000;
                File.WriteAllText(uni, u.ToJson());
                File.WriteAllText(quick, "{\"activeId\":1}");
                string id = SaveSlots.Migrate(uni, quick);
                Check(id != null && SaveSlots.NameOf(id) == "Game 1", "the old save didn't become Game 1");
                Check(!File.Exists(uni) && File.Exists(SaveSlots.UniversePath(id)) && File.Exists(SaveSlots.QuickPath(id)), "files not moved");
                Check(SaveSlots.Last == id, "Continue wouldn't pick it");
                Check(Math.Abs(SaveSlots.List()[0].funds - 77_000) < 1, "the career didn't survive the move");
                // A second run finds nothing to move
                File.WriteAllText(uni, u.ToJson());
                Check(SaveSlots.Migrate(uni, quick) == null && SaveSlots.List().Count == 1, "migrated twice");
                return "universe.json and quicksave.json moved into Game 1; only once";
            }
            finally { SaveSlots.Root = oldRoot; SaveSlots.Current = oldCur; }
        }
    }
}
