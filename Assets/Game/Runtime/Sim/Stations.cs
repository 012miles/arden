using System;
using System.Linq;

namespace Rocket
{
    /// <summary>
    /// Milestone 27: space stations (ships docked together in orbit) and the docking guide's advice.
    /// </summary>
    public static class Stations
    {
        /// <summary>How many ships went into this one: one, plus one for each pair of latched docking ports.</summary>
        public static int DockedPieces(VesselSim v)
        {
            int latched = v.parts.Count(p => p.def.kind == PartKind.DockingPort && p.dockedTo >= 0);
            return 1 + latched / 2;
        }

        /// <summary>A station for a contract: in a safe closed orbit, enough ships docked, seats, a free port, an antenna and solar power.</summary>
        public static bool Meets(VesselSim v, double ut, int seats, int pieces)
        {
            if (v.destroyed || v.landed) return false;
            var o = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
            if (!o.IsElliptic || o.Periapsis - v.body.radius < v.body.SafeAltitude) return false;
            if (DockedPieces(v) < pieces) return false;
            if (v.parts.Sum(p => p.def.crew) < seats) return false;
            if (!v.FreePorts().Any()) return false;
            return v.parts.Exists(p => p.def.kind == PartKind.Antenna) && v.parts.Exists(p => p.def.kind == PartKind.Solar);
        }

        /// <summary>
        /// The docking guide's one line of advice from how the ports line up: distance (m), sideways miss (m), angle
        /// between the faces (deg), closing speed (m/s, + toward).
        /// </summary>
        public static string DockHint(double dist, double lateral, double angle, double closing)
        {
            double good = GoodSpeed(dist);
            if (angle > VesselSim.DockMaxAngle) return "Turn to face the port (Align ports)";
            if (dist < 30 && lateral > Math.Max(0.4, dist * 0.25)) return closing > 0.3 ? "Stop and line up sideways first" : "Slide sideways onto the line (H/N I/K J/L)";
            if (closing < -0.05) return "Drifting away: push toward it";
            if (closing > good * 1.6 + 0.1) return "Too fast: slow down";
            if (closing < good * 0.4 && dist > 3) return "Move in closer";
            return dist < 3 ? "Steady... nearly there" : "Good: keep coming";
        }

        /// <summary>A comfortable closing speed at this distance: a tenth of it, at most 5 m/s, slowing to 0.3 m/s at the end.</summary>
        public static double GoodSpeed(double dist) => Math.Max(0.3, Math.Min(5, dist * 0.1));
    }
}
