using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 13: every main screen at several window shapes (4:3, 5:4, 16:9, 16:10, ultrawide). Logs any panel that
        /// runs off the screen or lands on another panel, and takes a screenshot of each screen at each size.
        /// </summary>
        IEnumerator SizesTour()
        {
            var root = GameRoot.I;
            // milestone 32: MacBook shapes too (13" and 15" Airs are about 1.54:1, older MacBooks 16:10, Surface-style 3:2)
            var sizes = new[] { (1024, 768), (1280, 1024), (1280, 720), (1440, 932), (1680, 1050), (1500, 1000), (1920, 1200), (2560, 1080) };
            int problems = 0;

            IEnumerator Check(string name, bool shot)
            {
                yield return new WaitForSecondsRealtime(0.35f);
                var bad = Ui.LayoutProblems();
                foreach (var b in bad) Log($"LAYOUT {name}: {b}");
                problems += bad.Count;
                if (shot) yield return Shot(name);
            }

            foreach (var (w, h) in sizes)
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(0.8f);
                Log($"--- {Screen.width}x{Screen.height}: ui scale {Ui.Scale:0.00}, layout {Ui.W:0}x{Ui.H:0}");
                string tag = $"{w}x{h}";
                bool shoot = w == 1024 || w == 2560;   // screenshots at the two extremes keep the set small

                root.EnterHangar();
                var hg = root.hangar;
                yield return Check($"200_{tag}_builder", true);
                if (w == 1440)
                {
                    // the same window in Easy and Engineer words (different labels, different widths)
                    var was = Words.Level;
                    Words.Level = WordLevel.Easy;
                    yield return Check($"200_{tag}_builder_easy", true);
                    Words.Level = WordLevel.Engineer;
                    yield return Check($"200_{tag}_builder_engineer", false);
                    Words.Level = was;
                    root.ShowMission();
                    yield return Check($"200_{tag}_lobby", true);
                    root.LeaveMission();
                    root.EnterHangar();
                    hg = root.hangar;
                }
                hg.ShowMissions(true);
                yield return Check($"201_{tag}_missions", shoot);
                hg.ShowTech(true);
                yield return Check($"202_{tag}_research", shoot);
                hg.ShowCenter(true);
                yield return Check($"203_{tag}_center", shoot);
                hg.ShowCenter(false);
                hg.ShowVessels(true);
                yield return Check($"204_{tag}_vessels", shoot);
                hg.ShowVessels(false);

                root.Launch(VesselDesign.Broadside());
                yield return new WaitForSecondsRealtime(1.0f);
                var f = root.flight;
                yield return Check($"205_{tag}_pad", true);
                f.ShowSciencePanel(true);
                yield return Check($"206_{tag}_science", shoot);
                f.ShowSciencePanel(false);
                f.ShowVesselList(true);
                yield return Check($"207_{tag}_vessel_list", shoot);
                f.ShowVesselList(false);
                f.mapView = true;
                yield return Check($"208_{tag}_map", true);
                f.mapView = false;
                f.Paused = true;
                yield return Check($"209_{tag}_paused", shoot);
                f.Paused = false;
            }
            Log($"layout problems found: {problems}");
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}
