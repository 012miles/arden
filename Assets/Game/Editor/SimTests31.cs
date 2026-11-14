using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 31 tests: the Space Apps check (real missions, comparing designs, rooms racing on the same mission, the guide).</summary>
    public static partial class SimTests
    {
        static void RunMilestone31()
        {
            Run("Real missions: every mission and every free-design subject names real missions like it; a CubeSat frame and a nuclear battery add their own", RealMissionsText);
            Run("Comparing designs: a line for each resource, the better value marked, the cheaper design wins on cost, a design that can't launch loses on problems, same design = no winner", CompareDesigns);
            Run("Racing rooms: a room keeps its best score per mission and announces it with the mission; announcements round-trip, older ones still read; rivals are other rooms on the same mission only", RoomRivals);
            Run("Guide: a page for playing together and mission design in every word level, before the keys page", GuideTogether);
            Run("New company name: the first launch copies the saves from this game's folder under the old company name (not the older prototype's), skips test runs, and never copies twice", NewCompanyFolder);
        }

        static string NewCompanyFolder()
        {
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arden_company_test_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                string Make(string rel, string text = "{}")
                {
                    string f = System.IO.Path.Combine(root, rel);
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f));
                    System.IO.File.WriteAllText(f, text);
                    return f;
                }
                Make("OldCompany/" + SaveSlots.ProductName + "/saves/game1/universe.json", "new game");
                Make("OldCompany/" + SaveSlots.ProductName + "/Designs/Lander.json");
                Make("OldCompany/" + SaveSlots.ProductName + "/autotest_saves/x/universe.json");
                Make("OldCompany/" + SaveSlots.OldProductName + "/Designs/Prototype.json");
                Make("SomeOtherGame/Stuff/save.dat");
                string now = System.IO.Path.Combine(root, "captaincarl24", SaveSlots.ProductName);
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(now, "planetmaps"));   // the map cache is made before the copy runs

                var olds = SaveSlots.OldFolders(now);
                Check(olds.Count == 2, $"expected 2 older folders, found {olds.Count}: {string.Join(", ", olds)}");
                Check(olds[0].EndsWith(SaveSlots.ProductName), "this game's folder under the old company should come first");

                int n = SaveSlots.CopyFromOlderVersions(now, out string from);
                Check(n == 2, $"copied {n} files, expected the save and the design");
                Check(from != null && from.Contains("OldCompany") && from.EndsWith(SaveSlots.ProductName), $"copied from {from}");
                Check(System.IO.File.ReadAllText(System.IO.Path.Combine(now, "saves", "game1", "universe.json")) == "new game", "the save didn't arrive");
                Check(System.IO.File.Exists(System.IO.Path.Combine(now, "Designs", "Lander.json")), "the design didn't arrive");
                Check(!System.IO.Directory.Exists(System.IO.Path.Combine(now, "autotest_saves")), "test runs were copied");
                Check(!System.IO.File.Exists(System.IO.Path.Combine(now, "Designs", "Prototype.json")), "the older prototype's designs were mixed in");
                Check(System.IO.File.Exists(System.IO.Path.Combine(root, "OldCompany", SaveSlots.ProductName, "saves", "game1", "universe.json")), "the old folder should stay as a backup");

                int again = SaveSlots.CopyFromOlderVersions(now, out _);
                Check(again == 0, $"the second launch copied {again} more files");
                Check(SaveSlots.OldFolders(null).Count == 0 && SaveSlots.CopyFromOlderVersions("", out _) == 0, "no folder should copy nothing");
                return $"{n} files copied once, from {System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(from))}";
            }
            finally { try { System.IO.Directory.Delete(root, true); } catch { } }
        }

        static string RealMissionsText()
        {
            foreach (var s in MissionDesign.Scenarios.Where(x => !x.sandbox))
            {
                string t = MissionDesign.RealMissions(MissionDesign.Example(s.id));
                Check(t.Length > 80, $"{s.name}: no real missions ({t})");
            }
            foreach (var tag in new[] { "map", "weather", "ice", "minerals", "volcano", "space", "relay" })
            {
                var p = new MissionPlan { scenario = "sandbox", sandboxTag = tag };
                Check(MissionDesign.RealMissions(p).Length > 60, $"free design '{tag}': no real missions");
            }
            var cube = MissionDesign.Example("storm"); cube.bus = "bus.cube";
            Check(MissionDesign.RealMissions(cube).Contains("MarCO"), "a CubeSat frame doesn't mention a real CubeSat mission");
            var rtg = MissionDesign.Example("ochre"); rtg.power = "rtg";
            Check(MissionDesign.RealMissions(rtg).Contains("Voyager"), "a nuclear battery doesn't mention real RTG missions");
            return $"{MissionDesign.Scenarios.Length - 1} missions + 7 subjects";
        }

        static string CompareDesigns()
        {
            var bodies = CelestialBody.CreateSystem();
            var lean = MissionDesign.Example("storm");
            var rich = lean.Clone(); rich.users = "usr.open"; rich.instruments.Add("ins.therm"); rich.wings = 4;
            var a = MissionDesign.Evaluate(lean, bodies); var b = MissionDesign.Evaluate(rich, bodies);
            var rows = MissionDesign.Compare(a, b);
            Check(rows.Count >= 12, $"only {rows.Count} lines");
            foreach (var n in new[] { "Cost", "Spare power", "Data home", "Lifetime", "Science a day", "Problems" })
                Check(rows.Any(r => r.name == n), $"no '{n}' line");
            Check(rows.First(r => r.name == "Cost").winner == 1, "the cheaper design doesn't win on cost");
            Check(rows.First(r => r.name == "Science a day").winner == 2, "the design with more instruments and users doesn't win on science");
            var broken = lean.Clone(); broken.vehicle = "lv.sprite"; broken.bus = "bus.big";
            var c = MissionDesign.Evaluate(broken, bodies);
            Check(!c.CanLaunch, "the broken design can launch");
            var rows2 = MissionDesign.Compare(a, c);
            Check(rows2.First(r => r.name == "Problems").winner == 1, "the design that can't launch doesn't lose on problems");
            Check(MissionDesign.CompareVerdict(rows2, a, c, "A", "C").Contains("can't launch"), "the verdict doesn't say which can't launch");
            var same = MissionDesign.Compare(a, MissionDesign.Evaluate(lean.Clone(), bodies));
            Check(same.All(r => r.winner == 0), "the same design has winners");
            return $"{rows.Count} lines; lean wins {rows.Count(r => r.winner == 1)}, rich wins {rows.Count(r => r.winner == 2)}";
        }

        static string RoomRivals()
        {
            var st = new RoomState();
            st.RecordBest("storm", 500, 1);
            st.RecordBest("storm", 720, 2);
            st.RecordBest("storm", 610, 1);
            st.RecordBest("taltmap", 300, 0);
            st.BestFor("storm", out int s1, out int k1);
            st.BestFor("taltmap", out int s2, out _);
            st.BestFor("ice", out int s3, out _);
            Check(s1 == 720 && k1 == 2, $"storm best {s1}/{k1}, wanted 720/2");
            Check(s2 == 300 && s3 == 0, $"taltmap {s2}, ice {s3}");
            var back = JsonUtility.FromJson<RoomState>(JsonUtility.ToJson(st));
            back.BestFor("storm", out int s4, out _);
            Check(s4 == 720, "the best scores don't survive the network");

            var host = new NetRoom { IsHost = true, ListenPort = 47771 };
            host.State.room = "Comets|club"; host.State.code = "ABCD2345"; host.State.activity = 0; host.State.plan = new MissionPlan { scenario = "storm" };
            host.State.players.Add(new NetPlayer { id = 1, name = "Ana", roles = 15 });
            host.State.RecordBest("storm", 812, 2);
            var info = NetRoom.ParseBeacon(host.BeaconText());
            Check(info != null, "the announcement doesn't parse");
            Check(info.name == "Comets club" && info.code == "ABCD2345" && info.port == 47771 && info.players == 1 && info.activity == 0, $"announcement fields wrong: {info.name} {info.code} {info.port}");
            Check(info.scenario == "storm" && info.score == 812 && info.stars == 2, $"mission/score wrong: {info.scenario} {info.score} {info.stars}");
            var old = NetRoom.ParseBeacon("ARDENMD|1|Old room|ZZZZ2222|2|47770|0|1");
            Check(old != null && old.scenario == null && old.score == 0 && old.campaign, "an older game's announcement doesn't read");
            Check(NetRoom.ParseBeacon("hello|1|x") == null, "junk reads as a room");

            var me = new NetRoom { IsHost = true };
            me.State.code = "MINE2345"; me.State.plan = new MissionPlan { scenario = "storm" };
            me.Found.Add(new RoomInfo { name = "Comets", code = "AAAA2222", activity = 0, scenario = "storm", score = 812 });
            me.Found.Add(new RoomInfo { name = "Rockets", code = "BBBB2222", activity = 0, scenario = "storm", score = 900 });
            me.Found.Add(new RoomInfo { name = "Mappers", code = "CCCC2222", activity = 0, scenario = "taltmap", score = 999 });
            me.Found.Add(new RoomInfo { name = "Builders", code = "DDDD2222", activity = 1, scenario = "storm", score = 0 });
            me.Found.Add(new RoomInfo { name = "Me", code = "MINE2345", activity = 0, scenario = "storm", score = 100 });
            var rivals = me.Rivals().ToList();
            Check(rivals.Count == 2 && rivals[0].name == "Rockets" && rivals[1].name == "Comets", "rivals: " + string.Join(", ", rivals.Select(r => r.name)));
            me.State.plan.scenario = "sandbox";
            Check(!me.Rivals().Any(), "free design has rivals");
            return "best per mission kept; announcement round-trips; 2 rivals of 5 rooms";
        }

        static string GuideTogether()
        {
            int i = Array.IndexOf(HowToPlay.Topics, "Multiplayer");
            Check(i >= 0 && i == HowToPlay.Topics.Length - 2, $"the multiplayer page is at {i}");
            var page = typeof(HowToPlay).GetMethod("Page", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var was = Words.Level;
            try
            {
                foreach (WordLevel l in Enum.GetValues(typeof(WordLevel)))
                {
                    Words.Level = l;
                    string t = (string)page.Invoke(null, new object[] { i });
                    Check(t.Contains("room") && t.Length > 400, $"{l}: the page is too short or doesn't mention rooms");
                    Check(t.ToLower().Contains("mission"), $"{l}: the page doesn't explain mission design");
                }
            }
            finally { Words.Level = was; }
            return $"{HowToPlay.Topics.Length} topics";
        }
    }
}
