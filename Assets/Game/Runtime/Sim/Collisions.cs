using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Vessels bump into each other. Every part is a capsule (stack parts, along their axis) or a ball (side-mounted
    /// parts); where two vessels' shapes overlap they're pushed apart, and a rigid-body impulse at the contact point
    /// sends them off with a little bounce, spinning them as well. Hit harder than a part can take and that vessel is
    /// wrecked, as on the ground. Open docking ports and an open grabber don't collide, so they can still latch, and
    /// pieces that have just come apart (staging, undocking, EVA) ignore each other until they're clear.
    /// </summary>
    public static class Collisions
    {
        public const double Bounce = 0.2;

        struct Shape { public Vector3d a, b; public double r; public PartState part; }

        static readonly List<Shape> shapesA = new List<Shape>(), shapesB = new List<Shape>();

        /// <summary>Whether a part takes part in collisions (open ports and an open grabber don't: they're for latching).</summary>
        static bool Collides(PartState p)
        {
            var k = p.def.kind;
            if (k == PartKind.Flag || k == PartKind.FairingHalf) return false;
            if (k == PartKind.DockingPort && p.dockedTo < 0) return false;
            if (k == PartKind.Grabber && p.dockedTo < 0) return false;
            return true;
        }

        static Vector3d World(VesselSim v, Vector3 local) => v.pos + Vector3d.Rotate(v.rot, Vector3d.FromFloat(local - v.com));

        static void Shapes(VesselSim v, List<Shape> into)
        {
            into.Clear();
            foreach (var p in v.parts)
            {
                if (!Collides(p)) continue;
                var d = p.def;
                if (d.radial)
                {
                    float rr = Mathf.Clamp(Mathf.Max(d.radialSize.x, Mathf.Max(d.radialSize.y, d.radialSize.z)) * 0.5f, 0.08f, 2f);
                    var c = World(v, p.Center);
                    into.Add(new Shape { a = c, b = c, r = rr, part = p });
                    continue;
                }
                float r = Mathf.Max(0.05f, Mathf.Max(p.RTop, p.RBottom));
                Vector3 top = p.localPos, bottom = p.Bottom, axis = bottom - top;
                float len = axis.magnitude;
                if (len > 2 * r)
                {
                    // Round ends that reach just to the flat faces
                    Vector3 dir = axis / len;
                    into.Add(new Shape { a = World(v, top + dir * r), b = World(v, bottom - dir * r), r = r, part = p });
                }
                else
                {
                    var c = World(v, p.Center);
                    into.Add(new Shape { a = c, b = c, r = r, part = p });
                }
            }
        }

        /// <summary>How far from its centre of mass a vessel reaches (for a quick "could they touch" check).</summary>
        public static double Reach(VesselSim v)
        {
            double best = 0;
            foreach (var p in v.parts)
            {
                float ext = p.def.radial ? Mathf.Max(p.def.radialSize.x, Mathf.Max(p.def.radialSize.y, p.def.radialSize.z)) : p.def.length + Mathf.Max(p.RTop, p.RBottom);
                best = Math.Max(best, (p.Center - v.com).magnitude + ext);
            }
            return best;
        }

        /// <summary>Closest points between segments p1-q1 and p2-q2.</summary>
        static void Closest(Vector3d p1, Vector3d q1, Vector3d p2, Vector3d q2, out Vector3d c1, out Vector3d c2)
        {
            Vector3d d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            double a = Vector3d.Dot(d1, d1), e = Vector3d.Dot(d2, d2), f = Vector3d.Dot(d2, r);
            double s, t;
            if (a <= 1e-12 && e <= 1e-12) { c1 = p1; c2 = p2; return; }
            if (a <= 1e-12) { s = 0; t = Math.Max(0, Math.Min(1, f / e)); }
            else
            {
                double c = Vector3d.Dot(d1, r);
                if (e <= 1e-12) { t = 0; s = Math.Max(0, Math.Min(1, -c / a)); }
                else
                {
                    double b = Vector3d.Dot(d1, d2), den = a * e - b * b;
                    s = den > 1e-12 ? Math.Max(0, Math.Min(1, (b * f - c * e) / den)) : 0;
                    t = (b * s + f) / e;
                    if (t < 0) { t = 0; s = Math.Max(0, Math.Min(1, -c / a)); }
                    else if (t > 1) { t = 1; s = Math.Max(0, Math.Min(1, (b - c) / a)); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }

        /// <summary>Whether two vessels are still overlapping anywhere (used to tell when separated pieces are clear).</summary>
        public static bool Overlapping(VesselSim a, VesselSim b)
        {
            if ((a.pos - b.pos).magnitude > Reach(a) + Reach(b)) return false;
            Shapes(a, shapesA); Shapes(b, shapesB);
            foreach (var sa in shapesA)
                foreach (var sb in shapesB)
                {
                    Closest(sa.a, sa.b, sb.a, sb.b, out var ca, out var cb);
                    if ((ca - cb).magnitude < sa.r + sb.r + 0.05) return true;
                }
            return false;
        }

        /// <summary>
        /// Pushes two vessels apart where they overlap and bounces them off each other. Returns true if they touched;
        /// 'crash' names a vessel that was wrecked by the hit (null if none).
        /// </summary>
        public static bool Resolve(VesselSim a, VesselSim b, out string crash)
        {
            crash = null;
            if (a == b || a.destroyed || b.destroyed || a.body != b.body || (a.landed && b.landed)) return false;
            // Pieces that just came apart ignore each other until they're clear
            if (a.ghostWith == b || b.ghostWith == a)
            {
                if (!Overlapping(a, b)) { if (a.ghostWith == b) a.ghostWith = null; if (b.ghostWith == a) b.ghostWith = null; }
                return false;
            }
            if ((a.pos - b.pos).magnitude > Reach(a) + Reach(b)) return false;
            Shapes(a, shapesA); Shapes(b, shapesB);
            // The deepest overlap
            double deepest = 0;
            Vector3d n = Vector3d.zero, contact = Vector3d.zero;
            PartState pa = null, pb = null;
            foreach (var sa in shapesA)
                foreach (var sb in shapesB)
                {
                    Closest(sa.a, sa.b, sb.a, sb.b, out var ca, out var cb);
                    Vector3d dlt = ca - cb;
                    double dist = dlt.magnitude, depth = sa.r + sb.r - dist;
                    if (depth <= deepest) continue;
                    deepest = depth;
                    n = dist > 1e-6 ? dlt / dist : (a.pos - b.pos).normalized;
                    contact = cb + n * sb.r;
                    pa = sa.part; pb = sb.part;
                }
            if (pa == null) return false;

            double ima = a.landed ? 0 : 1 / Math.Max(a.mass, 1e-3), imb = b.landed ? 0 : 1 / Math.Max(b.mass, 1e-3);
            if (ima + imb <= 0) return false;
            // Push apart (most of the overlap, shared by mass)
            double share = deepest * 0.8 / (ima + imb);
            if (ima > 0) a.pos += n * (share * ima);
            if (imb > 0) b.pos -= n * (share * imb);

            Vector3 rA = (contact - a.pos).ToFloat(), rB = (contact - b.pos).ToFloat(), nf = n.ToFloat();
            Vector3d vA = a.vel + Vector3d.FromFloat(Vector3.Cross(a.angVel, rA));
            Vector3d vB = b.vel + Vector3d.FromFloat(Vector3.Cross(b.angVel, rB));
            double vn = Vector3d.Dot(vA - vB, n);
            if (vn < 0)
            {
                Vector3 IinvA(Vector3 x) => a.landed ? Vector3.zero : InvInertia(a, x);
                Vector3 IinvB(Vector3 x) => b.landed ? Vector3.zero : InvInertia(b, x);
                double k = ima + imb + Vector3.Dot(nf, Vector3.Cross(IinvA(Vector3.Cross(rA, nf)), rA)) + Vector3.Dot(nf, Vector3.Cross(IinvB(Vector3.Cross(rB, nf)), rB));
                double j = -(1 + Bounce) * vn / Math.Max(k, 1e-9);
                if (ima > 0) { a.vel += n * (j * ima); a.angVel += IinvA(Vector3.Cross(rA, nf * (float)j)); }
                if (imb > 0) { b.vel -= n * (j * imb); b.angVel -= IinvB(Vector3.Cross(rB, nf * (float)j)); }
                double speed = -vn;
                if (speed > pa.def.crashTolerance && !a.landed) { a.Destroy($"Crashed into {b.name} at {speed:0} m/s ({pa.def.name} can take {pa.def.crashTolerance:0} m/s)"); crash = a.name; }
                if (speed > pb.def.crashTolerance && !b.landed) { b.Destroy($"Crashed into {a.name} at {speed:0} m/s ({pb.def.name} can take {pb.def.crashTolerance:0} m/s)"); crash = crash == null ? b.name : crash + " and " + b.name; }
                if (crash == null && speed > 0.3) a.lastEvent = $"Bumped into {b.name} at {speed:0.0} m/s";
            }
            a.rails = null; b.rails = null;
            return true;
        }

        /// <summary>The inverse inertia applied to a world-space vector (the inertia is diagonal in the vessel's frame).</summary>
        static Vector3 InvInertia(VesselSim v, Vector3 world)
        {
            Vector3 l = Quaternion.Inverse(v.rot) * world;
            l = new Vector3(l.x / v.inertia.x, l.y / v.inertia.y, l.z / v.inertia.z);
            return v.rot * l;
        }
    }
}
