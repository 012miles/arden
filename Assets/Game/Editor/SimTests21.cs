using System;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 21 tests: the soundtrack and sounds are all there.</summary>
    public static partial class SimTests
    {
        static void RunMilestone21()
        {
            Run("Sound: every music track and sound effect loads, music streams, effects are short enough to keep in memory", SoundAssets);
        }

        static string SoundAssets()
        {
            string[] sfx = { "engine_low", "engine_vac", "ignition", "wind", "rcs", "rcs_puff", "explosion1", "explosion2", "clank", "clink", "thud", "hangar_amb", "crickets", "breeze", "ui_click", "ui_drop", "ui_confirm", "ui_error" };
            string[] music = { "title_space", "builder_august", "builder_exploration", "builder_home", "launch_arclight", "space_outthere", "space_temple", "space_firstlight", "space_beyond", "space_drifting", "space_atmosphere" };
            float minutes = 0;
            foreach (var n in music)
            {
                var c = Resources.Load<AudioClip>("Audio/Music/" + n);
                Check(c != null, "missing music " + n);
                Check(c.loadType == AudioClipLoadType.Streaming, $"{n} doesn't stream");
                Check(c.length > 60, $"{n} is only {c.length:0} s");
                minutes += c.length / 60f;
            }
            foreach (var n in sfx)
            {
                var c = Resources.Load<AudioClip>("Audio/Sfx/" + n);
                Check(c != null, "missing sound " + n);
                Check(c.length < 30, $"{n} is {c.length:0} s long");
            }
            return $"{music.Length} tracks ({minutes:0} min), {sfx.Length} effects";
        }
    }
}
