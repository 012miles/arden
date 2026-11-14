using UnityEngine;

namespace Rocket
{
    /// <summary>Driving rovers: W/S drive, A/D steer, B brakes, while the wheels are on the ground.</summary>
    public partial class FlightMode
    {
        /// <summary>A rover with its wheels on (or within a couple of metres of) the ground: W/S drive it.</summary>
        bool Driving(VesselSim v) => v.HasDriveWheels && (v.landed || v.OnWheels || v.RadarAltitude(ut) < 2);

        /// <summary>Scripted driving for the autotest (x = steer, y = drive); null = keyboard.</summary>
        public Vector2? driveOverride;

        /// <summary>The rover line in the HUD: speed, distance driven here, and whether the motors have power.</summary>
        string RoverLine(VesselSim v)
        {
            if (!v.HasDriveWheels) return null;
            Vector3d up = v.pos.normalized, sv = v.SurfaceRelVelocity;
            double speed = (sv - up * Vector3d.Dot(sv, up)).magnitude;
            string dist = v.drivenOn == v.body.name && v.driven > 0 ? $", driven {Ui.Dist(v.driven)} on {v.body.name}" : "";
            string power = v.EcStored > 0.05 ? "" : "  <color=#ff8060>no power for the motors</color>";
            return $"Rover: {speed:0.0} m/s{dist}{(v.brakesOn ? "  (brakes on)" : "")}{power}";
        }
    }
}
