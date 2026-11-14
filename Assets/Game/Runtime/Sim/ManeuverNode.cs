using System;
using System.Collections.Generic;

namespace Rocket
{
    /// <summary>
    /// A planned burn: a time on the current path and a delta-v in the orbital frame
    /// (prograde / normal / radial-out). Predicts the path that results.
    /// </summary>
    public class ManeuverNode
    {
        public double ut;
        public double prograde, normal, radial; // m/s
        /// <summary>A speed-match burn: re-aimed at this target from the real path once the burns before it are done.</summary>
        public VesselSim matchTarget;

        // Filled in by Update()
        public CelestialBody body;
        public Vector3d pos, vel;      // state at the node, before the burn (relative to body)
        public Vector3d dvWorld;       // burn vector, inertial
        public List<TrajectorySegment> planned = new List<TrajectorySegment>();
        public bool valid;

        public double DeltaV => Math.Sqrt(prograde * prograde + normal * normal + radial * radial);

        public static void Frame(Vector3d r, Vector3d v, out Vector3d pro, out Vector3d nor, out Vector3d rad)
        {
            pro = v.normalized;
            nor = Vector3d.Cross(r, v).normalized;
            rad = (r.normalized - pro * Vector3d.Dot(r.normalized, pro)).normalized;
        }

        /// <summary>Recomputes the node state and the planned trajectory from the current predicted path.</summary>
        public void Update(List<TrajectorySegment> current)
        {
            valid = false;
            if (current == null) return;
            foreach (var s in current)
            {
                // A closed orbit with no events repeats, so a node can sit any number of laps ahead.
                bool repeats = s.end == SegmentEnd.None && s.orbit.IsElliptic;
                if (ut < s.tStart - 1e-6 || (ut > s.tEnd + 1e-6 && !repeats)) continue;
                body = s.body;
                s.orbit.StateAt(ut, out pos, out vel);
                Frame(pos, vel, out var p, out var n, out var r);
                dvWorld = p * prograde + n * normal + r * radial;
                planned = Patcher.Predict(body, pos, vel + dvWorld, ut, 3);
                valid = true;
                return;
            }
        }

        /// <summary>
        /// Delta-v still to burn: the velocity the planned orbit has right now minus the vessel's velocity.
        /// Before the burn window this is just the planned burn.
        /// </summary>
        public Vector3d Remaining(VesselSim v, double now, bool burning)
        {
            if (!valid || planned.Count == 0) return Vector3d.zero;
            if (!burning || v.body != body) return dvWorld;
            planned[0].orbit.StateAt(now, out _, out var vp);
            return vp - v.vel;
        }

        public static double BurnTime(VesselSim v, double dv)
        {
            double thrust = v.PotentialThrust();
            return thrust > 0 ? dv / (thrust / v.mass) : double.PositiveInfinity;
        }
    }
}
