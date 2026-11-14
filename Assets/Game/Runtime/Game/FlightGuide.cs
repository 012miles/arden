using UnityEngine;

namespace Rocket
{
    /// <summary>The first-flight guide in flight: works out the step from what the vessel is doing and shows its advice.</summary>
    public partial class FlightMode
    {
        VesselSim guideVessel;
        double guideMaxAlt;
        /// <summary>Highest point of the last flight, for the builder's "welcome home" note.</summary>
        public double GuideMaxAlt => guideMaxAlt;

        public GuideState GuideNow()
        {
            var v = active;
            if (v != guideVessel) { guideVessel = v; guideMaxAlt = 0; }
            var s = new GuideState { inFlight = v != null };
            if (v == null) return s;
            double alt = v.Altitude;
            if (!v.landed) guideMaxAlt = System.Math.Max(guideMaxAlt, alt);
            double dv = 0;
            if (reports != null) foreach (var r in reports) dv += r.dvVac;
            Orbit orbit = trajectory.Count > 0 ? trajectory[0].orbit : null;
            s.onPad = !v.hasLaunched;
            s.landed = v.landed || (v.WheelsDown && v.RadarAltitude(ut) < 0.3 && v.SurfaceRelVelocity.magnitude < 1);
            s.destroyed = v.destroyed;
            s.alt = alt;
            s.maxAlt = guideMaxAlt;
            s.peAlt = orbit != null ? orbit.Periapsis - v.body.radius : -v.body.radius;
            s.airTop = v.body.HasAtmosphere ? v.body.atmosphereHeight : v.body.SafeAltitude;
            s.orbitDv = dv;
            return s;
        }

        void UpdateGuide()
        {
            var c = U.career;
            if (!Guide.Showing(c.guide) || active == null || active.IsEva) return;
            int next = Guide.Advance(c.guide, GuideNow());
            if (next != c.guide)
            {
                c.guide = next;
                U.Save();
            }
        }

        /// <summary>The guide's panel, top right under the stages. Returns its bottom edge (or y when hidden).</summary>
        float DrawGuide(float W, float y)
        {
            var c = U.career;
            if (!Guide.Showing(c.guide) || active == null || active.IsEva || Tutorial.Active) return y;   // (the tutorial's box says it instead)
            var s = GuideNow();
            string text = Guide.Text(c.guide, s, c.IsCareer);
            var r = new Rect(W - 390, y, 380, 30 + Ui.Small.CalcHeight(new GUIContent(text), 336) + 12);
            bool done = Guide.Finished(c.guide);
            if (done) r.height += 30;
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 12, r.y + 6, r.width - 50, 22), Guide.Title(c.guide), Ui.Header);
            if (Ui.Press(new Rect(r.xMax - 30, r.y + 6, 22, 20), "x", Ui.ButtonSmall)) CloseGuide();
            Ui.Text(new Rect(r.x + 12, r.y + 30, 336, r.height - 34), text, Ui.Small);
            if (done && Ui.Press(new Rect(r.xMax - 110, r.yMax - 32, 100, 24), "Got it", Ui.ButtonSmall)) CloseGuide();
            return r.yMax;
        }

        void CloseGuide()
        {
            bool finished = Guide.Finished(U.career.guide);
            U.career.guide = Guide.Closed;
            U.Save();
            if (!finished) Show("Guide closed: the ESC menu brings it back", 4f);
        }
    }
}
