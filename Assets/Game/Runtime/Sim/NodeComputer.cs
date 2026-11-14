using System;
using System.Collections.Generic;

namespace Rocket
{
    /// <summary>
    /// The flight computer: keeps a queue of maneuver nodes (each planned from the path the one before leaves), and flies
    /// a node by itself: points along the burn, opens the throttle once lined up, eases off near the end and stops.
    /// </summary>
    public static class NodeComputer
    {
        /// <summary>What to do this moment for a node.</summary>
        public struct Step
        {
            public double throttle;
            public bool window;   // inside the burn (half the burn time either side of the node)
            public bool done;     // nothing left to burn
            public Vector3d dir;  // where to point
            public double remaining;
        }

        /// <summary>Plans each later node from the path the one before it leaves.</summary>
        public static void UpdateChain(ManeuverNode first, List<ManeuverNode> later)
        {
            var prev = first;
            foreach (var n in later)
            {
                if (prev != null && prev.valid && prev.planned.Count > 0) n.Update(prev.planned);
                else n.valid = false;
                prev = n;
            }
        }

        /// <summary>
        /// A speed-match node for the coming closest approach to a target on the current path: burn the difference in
        /// velocity there, starting half the burn early. Null if the path doesn't come within 'within' metres.
        /// </summary>
        public static ManeuverNode MatchAt(VesselSim v, List<TrajectorySegment> path, Orbit target, double ut, double within = 50_000)
        {
            if (path == null || path.Count == 0) return null;
            var seg = path[0];
            double span = seg.orbit.IsElliptic ? seg.orbit.Period : 4 * 3600;
            if (!Patcher.ClosestApproach(seg, v.body, target, ut, span, out double d, out double t, out _) || d > within) return null;
            seg.orbit.StateAt(t, out _, out var vel);
            target.StateAt(t, out _, out var vt);
            Vector3d dv = vt - vel;
            double half = ManeuverNode.BurnTime(v, dv.magnitude) / 2;
            double tn = t - (double.IsInfinity(half) ? 0 : Math.Min(half, 0.45 * (t - ut)));
            // Components in the frame at the node itself (half a burn earlier, the orbit's frame has turned a little)
            seg.orbit.StateAt(tn, out var rn, out var vn);
            Intercept.ToNode(rn, vn, dv, out double p, out double n, out double rad);
            var node = new ManeuverNode { ut = tn, prograde = p, normal = n, radial = rad };
            node.Update(path);
            return node;
        }

        /// <summary>
        /// Flies one node: 'started' says the burn has begun (so a node that's been burned off counts as done rather than
        /// waiting). Throttle only once the nose is within about 6 degrees of the burn; near the end the throttle drops so
        /// the last metres per second come off gently.
        /// </summary>
        public static Step Fly(VesselSim v, ManeuverNode node, double ut, bool started, Orbit matchOrbit = null)
        {
            var s = new Step();
            if (node == null || !node.valid) { s.done = true; return s; }
            double burn = ManeuverNode.BurnTime(v, node.DeltaV);
            s.window = !double.IsInfinity(burn) && ut > node.ut - burn / 2 - 0.5;
            var rem = node.Remaining(v, ut, s.window);
            // A speed match burns against the target's own velocity, so it ends truly alongside
            if (s.window && matchOrbit != null && v.body == node.body) { matchOrbit.StateAt(ut, out _, out var vt); rem = vt - v.vel; }
            s.remaining = rem.magnitude;
            s.dir = rem.magnitude > 1e-6 ? rem.normalized : node.dvWorld.normalized;
            // Done when what's left is tiny, or has swung mostly sideways (drift between two nearby craft does that): RCS work
            double along = Vector3d.Dot(rem.normalized, node.dvWorld.normalized);
            if (started && (rem.magnitude < 0.25 || along < 0.2 || (along < 0.7 && rem.magnitude < Math.Max(0.25, node.DeltaV * 0.004)))) { s.done = true; return s; }
            if (!s.window) return s;
            double accel = v.PotentialThrust() / Math.Max(v.mass, 1e-3);
            if (accel <= 0 || Vector3d.Dot(v.Axis, s.dir) < 0.995) return s;   // wait until pointed
            s.throttle = Math.Max(0.01, Math.Min(1, rem.magnitude / (accel * 1.5)));
            return s;
        }
    }
}
