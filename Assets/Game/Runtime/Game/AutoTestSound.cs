using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 21: every sound loads; the builder plays its calm music over the hangar hum; the title screen its theme
        /// (settings and credits pages shot); the pad is quiet but for the breeze; liftoff starts the launch piece; and in
        /// orbit, after a silence, a space track.
        /// </summary>
        IEnumerator SoundTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var ga = GameAudio.I;
            string[] sfx = { "engine_low", "engine_vac", "ignition", "wind", "rcs", "rcs_puff", "explosion1", "explosion2", "clank", "clink", "thud", "hangar_amb", "crickets", "breeze", "ui_click", "ui_drop", "ui_confirm", "ui_error" };
            string[] music = { "title_space", "builder_august", "builder_exploration", "builder_home", "launch_arclight", "space_outthere", "space_temple", "space_firstlight", "space_beyond", "space_drifting", "space_atmosphere" };
            int ok = sfx.Count(n => ga.Clip(n) != null) + music.Count(n => ga.Clip(n, true) != null);
            float musicLen = music.Sum(n => ga.Clip(n, true)?.length ?? 0) / 60f;
            Log($"sound check: {ok}/{sfx.Length + music.Length} clips load, {musicLen:0} min of music");

            root.EnterHangar();
            yield return new WaitForSecondsRealtime(4f);
            Log($"builder: mood {ga.CurrentMood}, playing '{ga.Playing}'");

            root.ShowMenu();
            yield return new WaitForSecondsRealtime(3.5f);
            Log($"title: mood {ga.CurrentMood}, playing '{ga.Playing}'");
            root.menu.ShowPage("settings");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("300_settings_music");
            root.menu.ShowPage("credits");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("301_credits");
            root.menu.ShowPage("main");
            root.EnterHangar();   // leave the title screen the way a player would
            yield return new WaitForSecondsRealtime(0.5f);

            u.ut = TimeWithSun(root.home, LaunchSite.Direction, 0.5, 0);
            root.Launch(VesselDesign.Broadside());
            var f = root.flight;
            yield return new WaitForSecondsRealtime(2.5f);
            Log($"pad: mood {ga.CurrentMood}");
            f.Stage();
            yield return new WaitForSecondsRealtime(4f);
            Log($"liftoff: mood {ga.CurrentMood}, playing '{ga.Playing}' at {f.active.Altitude:0} m");
            f.SetCamera(200f, 4f, 60f);
            yield return Shot("302_liftoff_with_music");

            var orbiter = Fly(VesselDesign.Stack("Lookout", "probe.spark", "tank.s1", "eng.ember"), root.home, 200_000, 0, 60, u.ut, true);
            yield return new WaitForSecondsRealtime(2f);
            Log($"orbit: mood {ga.CurrentMood} (space music after a silence)");
            yield return new WaitForSecondsRealtime(30f);
            Log($"orbit later: mood {ga.CurrentMood}, playing '{ga.Playing}'");
        }
    }
}
