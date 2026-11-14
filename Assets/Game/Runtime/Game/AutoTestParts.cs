using System;
using System.Collections;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>Milestone 8d: the more detailed parts, in the builder and on the pad in daylight.</summary>
        IEnumerator PartsTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            root.currentDesign = VesselDesign.Longshot();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("120_builder_moon_rocket");
            root.currentDesign = VesselDesign.Broadside();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("121_builder_boosters");
            root.currentDesign = VesselDesign.Lander("Probe lander");
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("122_builder_lander");

            u.ut = TimeWithSun(root.home, LaunchSite.Direction, 0.45, 0);
            root.Launch(VesselDesign.Broadside());
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            f.SetCamera(215f, 4f, 16f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("123_pad_closeup");
            Log("parts tour done");
        }
    }
}
