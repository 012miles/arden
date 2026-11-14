using System;
using UnityEngine;

namespace Rocket
{
    public partial class FlightMode
    {
        /// <summary>
        /// Milestone 21: which music and ambience a flight wants. Silence on the pad (just the space centre's breeze or
        /// crickets), the launch piece from liftoff until space (or four minutes), quiet space music everywhere else.
        /// </summary>
        void UpdateMusic()
        {
            var ga = GameAudio.I;
            if (ga == null) return;
            if (active == null || active.destroyed) { ga.Set(Mood.Space, Ambience.None); return; }
            var b = active.body;
            bool home = b.IsHomeWorld;
            Mood m;
            if (home && active.landed && !active.hasLaunched) m = Mood.Silent;
            else if (home && active.hasLaunched && !active.landed && ut - active.launchUt < 240 && active.Altitude < b.atmosphereHeight) m = Mood.Launch;
            else if (ga.CurrentMood == Mood.Launch && home && !active.landed && active.Altitude < b.atmosphereHeight) m = Mood.Launch;   // let it finish
            else m = Mood.Space;
            // The space centre's sounds when you're on the ground near it
            var amb = Ambience.None;
            if (home && active.Altitude < 2500)
            {
                Vector3d fixedDir = b.InertialToFixed(active.pos, ut).normalized;
                double km = Vector3d.Angle(fixedDir, LaunchSite.Direction) * Math.PI / 180 * b.radius / 1000;
                if (km < 6) amb = Vector3.Dot(active.pos.normalized.ToFloat(), GameRoot.SunNow) > -0.05f ? Ambience.SiteDay : Ambience.SiteNight;
            }
            ga.Set(m, amb, mapView ? 0.8f : 1f);
        }
    }
}
