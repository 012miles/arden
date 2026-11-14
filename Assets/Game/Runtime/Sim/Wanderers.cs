using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Wanderers: small rocky bodies that pass through Arden's space on open (hyperbolic) paths. Each is a one-part vessel
    /// (a Rock part) that comes in from near the edge of Arden's sphere of influence, swings past and leaves again,
    /// unless someone grabs it and pushes it into an orbit.
    /// </summary>
    public static class Wanderers
    {
        public const int MaxAtOnce = 2;
        /// <summary>Time between new arrivals (s): two days.</summary>
        public const double Interval = 2 * 86400;
        static readonly string Letters = "BCDFGHJKLMNPRSTVXZ";

        /// <summary>The size class (0 = A, 1 = B, 2 = C) a wanderer's name carries ("Wanderer B-KR3"), or -1.</summary>
        public static int ClassOf(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Wanderer ") || name.Length < 10) return -1;
            return "ABC".IndexOf(name[9]);
        }

        /// <summary>
        /// A new wanderer on its way in: it starts at 85% of the sphere of influence, with its closest pass between 150 km above
        /// the air and about 6,000 km up, arriving at 150-600 m/s, tilted no more than 25 degrees from the equator.
        /// </summary>
        public static VesselSim Spawn(CelestialBody home, double ut, System.Random rng, int number, int cls = -1)
        {
            if (cls < 0) { double k = rng.NextDouble(); cls = k < 0.5 ? 0 : k < 0.85 ? 1 : 2; }
            var def = PartCatalog.Rocks[Mathf.Clamp(cls, 0, PartCatalog.Rocks.Length - 1)];
            string name = $"Wanderer {"ABC"[cls]}-{Letters[rng.Next(Letters.Length)]}{Letters[rng.Next(Letters.Length)]}{number}";
            double rp = home.radius + home.SafeAltitude + 150_000 + rng.NextDouble() * 6_000_000;
            double vinf = 150 + rng.NextDouble() * 450;
            double inc = (rng.NextDouble() * 2 - 1) * 25, node = rng.NextDouble() * 360, argp = rng.NextDouble() * 360;
            Path(home, rp, vinf, inc, node, argp, out var pos, out var vel, out double toPeri);
            var d = VesselDesign.Stack(name, def.id);
            var rot = Quaternion.Euler((float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 360));
            var s = VesselSim.CreateFree(d, home, pos, vel, rot);
            s.name = name;
            s.hasLaunched = true;
            s.throttle = 0;
            s.sas = SasMode.Off;
            s.launchUt = ut;
            return s;
        }

        /// <summary>
        /// The state of an inbound pass at 85% of the sphere of influence, for a closest pass at radius rp with arrival speed
        /// vinf. Angles in degrees: inclination, the ascending node's longitude, and the periapsis angle from the node.
        /// toPeri is the time until the closest pass.
        /// </summary>
        public static void Path(CelestialBody home, double rp, double vinf, double inc, double node, double argp, out Vector3d pos, out Vector3d vel, out double toPeri)
        {
            double mu = home.mu, vp = Math.Sqrt(vinf * vinf + 2 * mu / rp), a = argp * Math.PI / 180;
            // In the equatorial plane (x-z, north = +y) prograde motion runs from +x toward -z.
            var p = new Vector3d(Math.Cos(a), 0, -Math.Sin(a));
            var q = new Vector3d(-Math.Sin(a), 0, -Math.Cos(a));
            double n = node * Math.PI / 180;
            var tilt = Quaternion.AngleAxis((float)inc, new Vector3((float)Math.Cos(n), 0, (float)-Math.Sin(n)));
            p = Vector3d.Rotate(tilt, p);
            q = Vector3d.Rotate(tilt, q);
            var o = Orbit.FromState(p * rp, q * vp, mu, 0);
            double start = Math.Min(home.SoiRadius * 0.85, 2e8), lo = -60 * 86400.0, hi = 0;
            for (int i = 0; i < 90; i++) { double mid = (lo + hi) / 2; if (o.RadiusAt(mid) > start) lo = mid; else hi = mid; }
            o.StateAt(hi, out pos, out vel);
            toPeri = -hi;
        }
    }

    /// <summary>Grabbing a wanderer: the grabber's claws latch onto the rock (it then flies as part of this vessel).</summary>
    public partial class VesselSim
    {
        public const float GrabRange = 0.8f, GrabMaxSpeed = 1.5f, GrabMaxAngle = 30f;

        public bool IsRock => parts.Count == 1 && parts[0].def.kind == PartKind.Rock;

        /// <summary>The rock a grabber holds, or null.</summary>
        public PartState HeldRock(PartState grabber)
        {
            if (grabber == null || grabber.def.kind != PartKind.Grabber || grabber.dockedTo < 0) return null;
            var r = Find(grabber.dockedTo);
            return r != null && r.def.kind == PartKind.Rock ? r : null;
        }

        /// <summary>Names of the wanderers this vessel holds.</summary>
        public IEnumerable<string> HeldRockNames()
        {
            foreach (var p in parts) if (p.def.kind == PartKind.Rock) yield return IsRock ? name : p.dockedName;
        }

        /// <summary>A grabber's open end (vessel frame), when it isn't holding anything.</summary>
        bool GrabFace(PartState p, out Vector3 pos, out Vector3 nrm)
        {
            pos = nrm = Vector3.zero;
            if (p.def.kind != PartKind.Grabber || p.dockedTo >= 0 || p.topCovered) return false;
            pos = p.localPos;
            nrm = p.Axis;
            return true;
        }

        /// <summary>World position of a wanderer's centre and its radius.</summary>
        void RockBall(out Vector3d center, out float radius)
        {
            var rp = parts[0];
            center = pos + Vector3d.Rotate(rot, Vector3d.FromFloat(rp.Center - com));
            radius = rp.def.length * 0.5f;
        }

        /// <summary>
        /// Latches a free grabber onto a wanderer it has just flown into: within 0.8 m of the surface, pointing at its
        /// centre within 30 degrees, slower than 1.5 m/s. The rock then becomes part of this vessel. 'note' explains a near miss.
        /// </summary>
        public bool TryGrab(VesselSim rock, out string note)
        {
            note = null;
            if (rock == null || rock == this || !rock.IsRock || rock.body != body || destroyed || rock.destroyed || landed || rock.landed) return false;
            rock.RockBall(out var center, out float R);
            if ((center - pos).magnitude > 400) return false;
            foreach (var pa in parts)
            {
                if (!GrabFace(pa, out var la, out var na)) continue;
                Vector3d wa = pos + Vector3d.Rotate(rot, Vector3d.FromFloat(la - com));
                Vector3d toC = center - wa;
                double gap = toC.magnitude - R;
                // Claws that just let go re-arm once they're 2 m clear of the rock.
                if (pa.disarmedWith == rock) { if (gap > 2.0) pa.disarmedWith = null; else continue; }
                if (gap > GrabRange) continue;
                float ang = Vector3.Angle(rot * na, toC.normalized.ToFloat());
                Vector3 dirL = Quaternion.Inverse(rock.rot) * (-toC).normalized.ToFloat();   // from the rock's centre toward the claws
                var rp = rock.parts[0];
                Vector3 lb = rp.Center + dirL * R;
                double rel = (PointVelocity(la) - rock.PointVelocity(lb)).magnitude;
                if (ang > GrabMaxAngle) { note = $"The grabber touched {rock.name} {ang:0} deg off square (needs under {GrabMaxAngle:0})"; continue; }
                if (rel > GrabMaxSpeed) { note = $"The grabber hit {rock.name} at {rel:0.0} m/s: too fast to hold on (under {GrabMaxSpeed:0.0} m/s)"; continue; }
                string rockName = rock.name;
                Merge(rock, pa, rp, la, na, lb, dirL);
                lastEvent = $"Grabbed {rockName}!";
                return true;
            }
            return false;
        }
    }
}
