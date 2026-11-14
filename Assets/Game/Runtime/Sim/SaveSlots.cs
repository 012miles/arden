using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>What the load screen shows about a saved game.</summary>
    public class SlotInfo
    {
        public string id, name;
        public GameMode mode;
        public bool chosen;
        public double ut, funds, science;
        public int vessels;
        public DateTime saved;
    }

    [Serializable]
    class SlotMeta { public string name; public string created; }

    /// <summary>
    /// Several saved games side by side: each is a folder holding its universe and its quicksave. The old single save
    /// (universe.json from before slots) becomes "Game 1". Designs stay shared between games.
    /// </summary>
    public static class SaveSlots
    {
        /// <summary>The game's name. Unity names the data folder after it, so the old name's folder is copied over once.</summary>
        public const string ProductName = "Arden Space Program", OldProductName = "Rocket Game (prototype)";

        /// <summary>
        /// The game was renamed (Rocket Game -> Arden Space Program), and Unity keeps each game's files in a folder named
        /// after it. The first time the new name runs, everything in the old folder (saves, designs, quicksaves) is copied
        /// across; the old folder is left as it was, as a backup. Returns how many files were copied.
        /// </summary>
        public static int CopyFromOldFolder(string newDir, string oldDir)
        {
            if (string.IsNullOrEmpty(newDir) || string.IsNullOrEmpty(oldDir) || !Directory.Exists(oldDir)) return 0;
            if (Path.GetFullPath(newDir).TrimEnd('\\', '/') == Path.GetFullPath(oldDir).TrimEnd('\\', '/')) return 0;
            // Only into a folder that has nothing of its own yet
            if (Directory.Exists(newDir) && (Directory.Exists(Path.Combine(newDir, "saves")) || Directory.Exists(Path.Combine(newDir, "Designs")) || File.Exists(Path.Combine(newDir, "universe.json")))) return 0;
            int n = 0;
            foreach (var f in Directory.GetFiles(oldDir, "*", SearchOption.AllDirectories))
            {
                string rel = f.Substring(oldDir.Length).TrimStart('\\', '/');
                if (rel.StartsWith("Unity") || rel.EndsWith(".log") || rel.StartsWith("autotest")) continue;   // Unity's caches, logs and test runs stay behind
                string to = Path.Combine(newDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                if (!File.Exists(to)) { File.Copy(f, to); n++; }
            }
            return n;
        }

        /// <summary>
        /// Folders an older version of the game may have used, best first. Unity names the data folder
        /// company/product, and both have changed: the game was renamed, and the company name became the maker's
        /// GitHub username (2026-11-14). So: this game's folder under any other company, then the old game name
        /// under this company, then the old game name under any other company. Only folders that hold saves or
        /// designs count. CopyFromOldFolder only fills an empty folder, so the first one that copies wins.
        /// </summary>
        public static List<string> OldFolders(string dataDir)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(dataDir)) return found;
            string company = Path.GetDirectoryName(Path.GetFullPath(dataDir).TrimEnd('\\', '/'));
            string root = string.IsNullOrEmpty(company) ? null : Path.GetDirectoryName(company);
            var others = new List<string>();
            try { if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) others = Directory.GetDirectories(root).Where(d => !SameFolder(d, company)).OrderBy(d => d).ToList(); }
            catch { }
            var tries = others.Select(d => Path.Combine(d, ProductName)).ToList();
            if (!string.IsNullOrEmpty(company)) tries.Add(Path.Combine(company, OldProductName));
            tries.AddRange(others.Select(d => Path.Combine(d, OldProductName)));
            foreach (var t in tries)
            {
                try
                {
                    if (Directory.Exists(t) && (Directory.Exists(Path.Combine(t, "saves")) || Directory.Exists(Path.Combine(t, "Designs")) || File.Exists(Path.Combine(t, "universe.json"))))
                        found.Add(t);
                }
                catch { }
            }
            return found;
        }

        static bool SameFolder(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

        /// <summary>Copies the best older folder into the game's data folder, once (nothing happens if it already has saves).</summary>
        public static int CopyFromOlderVersions(string dataDir, out string from)
        {
            from = null;
            foreach (var old in OldFolders(dataDir))
            {
                int n = CopyFromOldFolder(dataDir, old);
                if (n > 0) { from = old; return n; }
            }
            return 0;
        }

        /// <summary>Where the saves folder lives (the game's data folder; tests point it elsewhere).</summary>
        public static string Root;
        /// <summary>The game being played (a folder name), or null before one is picked.</summary>
        public static string Current;

        static string Dir => Path.Combine(Root ?? ".", Universe.IsAutotest ? "autotest_saves" : "saves");
        public static string SlotDir(string id) => Path.Combine(Dir, id);
        public static string UniversePath(string id) => Path.Combine(SlotDir(id), "universe.json");
        public static string QuickPath(string id) => Path.Combine(SlotDir(id), "quicksave.json");
        static string MetaPath(string id) => Path.Combine(SlotDir(id), "slot.json");
        static string LastPath => Path.Combine(Dir, "last.txt");

        /// <summary>Starts a new, empty game folder and returns its id.</summary>
        public static string Create(string name)
        {
            Directory.CreateDirectory(Dir);
            int n = 1;
            while (Directory.Exists(SlotDir($"game{n}"))) n++;
            string id = $"game{n}";
            Directory.CreateDirectory(SlotDir(id));
            File.WriteAllText(MetaPath(id), JsonUtility.ToJson(new SlotMeta { name = string.IsNullOrWhiteSpace(name) ? $"Game {n}" : name.Trim(), created = DateTime.Now.ToString("o") }));
            return id;
        }

        public static string NameOf(string id)
        {
            try { var m = JsonUtility.FromJson<SlotMeta>(File.ReadAllText(MetaPath(id))); if (!string.IsNullOrEmpty(m?.name)) return m.name; } catch { }
            return id;
        }

        public static void Rename(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !Directory.Exists(SlotDir(id))) return;
            File.WriteAllText(MetaPath(id), JsonUtility.ToJson(new SlotMeta { name = name.Trim(), created = DateTime.Now.ToString("o") }));
        }

        /// <summary>Every saved game, most recently played first.</summary>
        public static List<SlotInfo> List()
        {
            var list = new List<SlotInfo>();
            if (!Directory.Exists(Dir)) return list;
            foreach (var d in Directory.GetDirectories(Dir))
            {
                string id = Path.GetFileName(d);
                var info = new SlotInfo { id = id, name = NameOf(id), saved = Directory.GetLastWriteTime(d) };
                string u = UniversePath(id);
                if (File.Exists(u))
                {
                    info.saved = File.GetLastWriteTime(u);
                    try
                    {
                        var s = JsonUtility.FromJson<UniverseSave>(File.ReadAllText(u));
                        if (s != null)
                        {
                            info.ut = s.ut;
                            info.vessels = s.vessels?.Count(v => v != null) ?? 0;
                            if (s.career != null) { info.mode = s.career.mode; info.chosen = s.career.chosen; info.funds = s.career.funds; info.science = s.career.science; }
                        }
                    }
                    catch { }
                }
                list.Add(info);
            }
            return list.OrderByDescending(i => i.saved).ToList();
        }

        /// <summary>Deletes a saved game for good.</summary>
        public static void Delete(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Contains("..")) return;
            if (Directory.Exists(SlotDir(id))) Directory.Delete(SlotDir(id), true);
            if (Last == id) Last = null;
            if (Current == id) Current = null;
        }

        /// <summary>The game played last (for Continue), if it's still there.</summary>
        public static string Last
        {
            get { try { string id = File.Exists(LastPath) ? File.ReadAllText(LastPath).Trim() : null; return id != null && Directory.Exists(SlotDir(id)) ? id : null; } catch { return null; } }
            set { try { Directory.CreateDirectory(Dir); if (value == null) File.Delete(LastPath); else File.WriteAllText(LastPath, value); } catch { } }
        }

        /// <summary>
        /// The save from before slots existed (universe.json and quicksave.json next to the saves folder) moves into a
        /// game called "Game 1". Returns its id, or null if there was nothing to move.
        /// </summary>
        public static string Migrate(string oldUniverse, string oldQuick)
        {
            if (oldUniverse == null || !File.Exists(oldUniverse)) return null;
            if (List().Count > 0) return null;
            string id = Create("Game 1");
            File.Move(oldUniverse, UniversePath(id));
            if (oldQuick != null && File.Exists(oldQuick)) File.Move(oldQuick, QuickPath(id));
            Last = id;
            return id;
        }
    }
}
