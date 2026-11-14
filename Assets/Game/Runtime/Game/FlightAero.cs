using UnityEngine;

namespace Rocket
{
    /// <summary>The aerobraking forecast: what the next pass through a planet's air on your path would do.</summary>
    public partial class FlightMode
    {
        AeroForecast aero;
        float aeroTimer;

        /// <summary>Re-forecasts twice a second: the first piece of the path that dips into an atmosphere from above it.</summary>
        void UpdateAeroForecast(float dt)
        {
            aeroTimer -= dt;
            if (aeroTimer > 0) return;
            aeroTimer = 0.5f;
            aero = null;
            if (active == null || active.landed || active.destroyed) return;
            foreach (var s in trajectory)
            {
                var b = s.body;
                if (!b.HasAtmosphere || s.orbit.Periapsis > b.radius + b.atmosphereHeight) continue;
                Vector3d pos, vel;
                double t;
                if (s == trajectory[0]) { pos = active.pos; vel = active.vel; t = ut; }
                else { t = s.tStart; s.orbit.StateAt(t, out pos, out vel); }
                if (b == active.body && active.Altitude < b.atmosphereHeight) break; // already flying in it
                active.DragRange(out double held, out _); // as flown holding retrograde (what the planner assumes too)
                var f = AeroPass.Predict(b, pos, vel, t, active.mass, held);
                if (f.outcome != AeroOutcome.None) aero = f;
                break;
            }
        }

        /// <summary>For the autotest log: the forecast in words, and the active vessel's comms link.</summary>
        public string AeroForecastText => AeroLine();
        public string LinkText => commLink?.Describe;

        /// <summary>One line for the map panel, or null.</summary>
        string AeroLine() => aero?.Describe() is string d ? d + $" in {Ui.Time(aero.tEntry - ut)}" : null;
    }
}
