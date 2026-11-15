using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 41 (captaincarl24: "add some better water physics"): the sea is water now, not a floor. A ship that comes
    /// down on it floats on the part of it that's under water: each part pushes water aside by its size (sealed cabins and
    /// tanks hold air and push more, engines are solid metal and push less), so a capsule sits half in the water while a
    /// lone engine sinks. The buoyancy pushes up where the wet parts are, not at the centre of mass, so a wide capsule
    /// rights itself and a tall rocket topples over onto its side. A splashdown plunges in and bobs back up; a fast one
    /// skims and slows in the water's drag; engines push a ship along the surface (and fly it off), rover wheels paddle,
    /// the yaw keys steer. Something heavier than the water it can push aside sinks (the crew have time to swim out), and
    /// a ship that drifts onto a beach stops there.
    /// </summary>
    public partial class VesselSim
    {
        public const double WaterDensity = 1.025;   // tonnes per cubic metre
        /// <summary>Floating: how the ship moves on the water, in the world's turning frame (m/s and rad/s).</summary>
        public Vector3d floatVel;
        public Vector3 floatSpin;
        /// <summary>For the view: the last splash's speed (the view clears it), and how much of the ship is under water (0..1).</summary>
        public float splash, submerged;
        /// <summary>Where it sits on the world while down (body-fixed), for tests and tours.</summary>
        public Vector3d LandedFixedPos => landedFixedPos;
        bool sinkWarned;

        /// <summary>How much water a part can push aside (cubic metres).</summary>
        public static double Displacement(PartDef d)
        {
            double v;
            if (d.radial) v = d.radialSize.x * d.radialSize.y * d.radialSize.z;
            else
            {
                double r1 = d.topDiameter * 0.5, r2 = d.bottomDiameter * 0.5;
                v = Math.PI / 3.0 * d.length * (r1 * r1 + r1 * r2 + r2 * r2);
            }
            double k;
            switch (d.kind)
            {
                case PartKind.Command: case PartKind.Habitat: k = 2.4; break;                    // sealed, full of air
                case PartKind.Tank: case PartKind.MonoTank: case PartKind.OreTank: k = 2.0; break;
                case PartKind.Wing: case PartKind.ControlSurface: k = 1.6; break;                // hollow
                case PartKind.Engine: k = 0.5; break;                                            // solid metal
                case PartKind.Solid: k = 0.8; break;
                case PartKind.HeatShield: k = 1.0; break;
                default: k = 1.2; break;
            }
            return Math.Max(0.002, v * k);
        }

        /// <summary>The most this ship could float (tonnes of water it can push aside), against its mass.</summary>
        public double FloatCapacity { get { double v = 0; foreach (var p in parts) v += Displacement(p.def); return v * WaterDensity; } }

        // the points the water acts on: a few slices along each part, each split round its axis so a tilt dips one side deeper
        struct WetPoint { public Vector3 at; public float len, half; public Vector3 axis; public double vol; }
        readonly List<WetPoint> wetPoints = new List<WetPoint>();
        int wetFor = -1;
        double wetVolume, wetWidth;

        void BuildWetPoints()
        {
            int sig = parts.Count * 7919 + (int)(com.x * 100) + (int)(com.y * 1000) + (int)(com.z * 100);
            if (sig == wetFor && wetPoints.Count > 0) return;
            wetFor = sig;
            wetPoints.Clear();
            wetVolume = 0; wetWidth = 0.5;
            const int S = 4;
            foreach (var p in parts)
            {
                var d = p.def;
                double vol = Displacement(d);
                wetVolume += vol;
                float h = Mathf.Max(0.05f, d.Height), w = Mathf.Max(0.1f, d.MaxDiameter);
                wetWidth = Math.Max(wetWidth, w);
                Vector3 axis = p.Axis.normalized;
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized, v2 = Vector3.Cross(axis, u);
                // capsules and heat shields are built to float upright (weight low, wide at the base): their water acts further
                // out, which is what rights them; everything else as its shape says
                bool upright = d.kind == PartKind.Command || d.kind == PartKind.HeatShield || d.kind == PartKind.Habitat;
                float off = w * (upright ? 0.75f : 0.4f);
                for (int k = 0; k < S; k++)
                {
                    Vector3 c = p.Center + axis * (-h * 0.5f + h * (k + 0.5f) / S) - com;
                    foreach (var side in new[] { u, -u, v2, -v2 })
                        wetPoints.Add(new WetPoint { at = c + side * off, len = h / S, half = w * 0.3f, axis = axis, vol = vol / (S * 4) });
                }
            }
        }

        /// <summary>The water's push on the ship as it lies (tonnes of water pushed aside, and where, from the centre of mass, world frame).</summary>
        double Underwater(Vector3d up, out Vector3d centre)
        {
            double under = 0, seaR = body.radius;
            Vector3d sum = Vector3d.zero;
            var R = landedFixedRot;
            foreach (var wp in wetPoints)
            {
                Vector3d rel = Vector3d.Rotate(R, Vector3d.FromFloat(wp.at));
                double hgt = (landedFixedPos + rel).magnitude - seaR;
                double cosA = Math.Abs(Vector3d.Dot(Vector3d.Rotate(R, Vector3d.FromFloat(wp.axis)), up));
                double ext = Math.Max(0.04, wp.len * cosA + 2 * wp.half * Math.Sqrt(Math.Max(0, 1 - cosA * cosA)));
                double f = 0.5 - hgt / ext;
                if (f <= 0) continue;
                if (f > 1) f = 1;
                under += wp.vol * f;
                sum += rel * (wp.vol * f);
            }
            centre = under > 0 ? sum / under : Vector3d.zero;
            return under;
        }

        /// <summary>Coming down on the sea: in it goes, keeping its speed and spin (the water takes it from here).</summary>
        void EnterWater(double ut)
        {
            Vector3d up = pos.normalized;
            Vector3d sv = SurfaceRelVelocity;
            splash = (float)Math.Abs(Vector3d.Dot(sv, up)) + 0.3f * (float)(sv - up * Vector3d.Dot(sv, up)).magnitude;
            landedFixedPos = body.InertialToFixed(pos, ut);
            landedFixedRot = Quaternion.Inverse(body.RotationAt(ut)) * rot;
            floatVel = body.InertialToFixed(sv, ut);
            floatSpin = Quaternion.Inverse(body.RotationAt(ut)) * (angVel - body.AngularVelocityVector.ToFloat());
            landed = true;
            rails = null;
            sinkWarned = false;
            SyncLanded(ut);
            vel += body.FixedToInertial(floatVel, ut);
            angVel += body.RotationAt(ut) * floatSpin;
        }

        /// <summary>Floating, every step: buoyancy, weight, engines, drag, the turn buoyancy gives, sinking and beaching.</summary>
        void StepFloat(double dt, double ut, Vector3 fLocal)
        {
            ConsumePropellant(dt);
            sasHoldValid = false;
            dynPressure = 0;
            heatFlux = 0;
            gForce = 1;
            CoolInSpace(dt);   // (the sea cools things down)
            UpdateChutes(dt);
            double g = body.SurfaceGravity;
            Vector3d upNow = landedFixedPos.normalized;
            Vector3d push = Vector3d.Rotate(landedFixedRot, Vector3d.FromFloat(fLocal));   // kN, world's frame
            // enough thrust upward lifts it out (as from the ground)
            if (Vector3d.Dot(push, upNow) / mass > g + 0.05) { LeaveWater(ut); return; }
            // rover wheels paddle (slowly)
            if (HasWheels && Math.Abs(driveInput) > 0.01f)
            {
                Vector3d upI = body.FixedToInertial(upNow, ut);
                push += body.InertialToFixed(DriveForward(upI), ut) * (driveInput * mass * 0.9);
            }
            BuildWetPoints();
            double L = Math.Max(0.5, Length), r = wetWidth * 0.5;
            double inertia = mass * (L * L / 12.0 + r * r / 4.0) + mass * 0.05;
            bool winged = parts.Exists(p => p.def.kind == PartKind.Wing);
            int n = Math.Max(1, (int)Math.Ceiling(dt / 0.02));
            double h = dt / n;
            for (int s = 0; s < n; s++)
            {
                Vector3d up = landedFixedPos.normalized;
                double under = Underwater(up, out var cob);
                submerged = (float)Math.Min(1, under / Math.Max(1e-6, wetVolume));
                double fb = WaterDensity * g * under;                                      // kN
                Vector3d a = (up * (fb - mass * g) + push) / mass;
                floatVel += a * h;
                // the water's drag, worked out up-and-down and sideways apart: bobbing is damped hard (the whole bottom pushes
                // on the water, and makes waves), gliding along much less (only the wet front pushes)
                double wet = under > 0 ? Math.Pow(under, 2.0 / 3.0) : 0;
                double vUp = Vector3d.Dot(floatVel, up);
                Vector3d vSide = floatVel - up * vUp;
                double kqUp = 0.5 * WaterDensity * wet / mass, klUp = 1.2 * submerged;            // (1/m, 1/s)
                double kqSide = 0.18 * WaterDensity * wet / mass, klSide = 0.25 * submerged;
                vUp /= 1 + h * (klUp + kqUp * Math.Abs(vUp));
                vSide = vSide / (1 + h * (klSide + kqSide * vSide.magnitude));
                floatVel = up * vUp + vSide;
                // the turn: buoyancy pushing up off the centre of mass, a little nudge from the waves, the yaw keys
                Vector3d alpha = Vector3d.zero;
                if (under > 0) alpha = Vector3d.Cross(cob, up * fb) / inertia;
                Vector3d east = CelestialBody.EastAt(up), north = CelestialBody.NorthAt(up);
                double t = ut + s * h + id * 3.1;
                alpha += (east * Math.Sin(t * 0.83) + north * Math.Cos(t * 0.61)) * (0.04 * submerged);
                if (HasControl && Math.Abs(inputYaw) > 0.01f) alpha += up * (inputYaw * 0.35 * submerged);
                floatSpin += (alpha * h).ToFloat();
                floatSpin /= (float)(1 + h * (0.5 + 2.0 * submerged));
                float ang = floatSpin.magnitude * (float)h;
                if (ang > 1e-7f) landedFixedRot = Quaternion.AngleAxis(ang * Mathf.Rad2Deg, floatSpin.normalized) * landedFixedRot;
                landedFixedPos += floatVel * h;
                // bounced clear of the water and still going up: flying again (it'll splash back down)
                if (under <= 0 && Vector3d.Dot(floatVel, up) > 0.5) { LeaveWater(ut + (s + 1) * h); return; }
            }
            Vector3d upEnd = landedFixedPos.normalized;
            Vector3d horiz = floatVel - upEnd * Vector3d.Dot(floatVel, upEnd);
            // a plane fast enough on the water: off it goes (its wings take it from here, or it settles back)
            if (winged && horiz.magnitude > 35) { LeaveWater(ut + dt); return; }
            // drifted onto a beach: it stops there
            if (!Terrain.IsWater(body, landedFixedPos))
            {
                floatVel = Vector3d.zero; floatSpin = Vector3.zero; submerged = 0;
                SyncLanded(ut + dt);
                SettleOnGround(ut + dt);
                lastEvent = $"{name} washed up on the shore.";
                return;
            }
            // heavier than the water it can push aside: under it goes
            double depth = body.radius - landedFixedPos.magnitude;
            if (submerged > 0.97f && Vector3d.Dot(floatVel, upEnd) < -0.05 && !sinkWarned)
            {
                sinkWarned = true;
                bool crew = parts.Exists(p => p.crew.Count > 0);
                lastEvent = crew ? $"{name} is sinking! Get the crew out (EVA) and swim for it." : $"{name} is sinking.";
            }
            if (depth > Math.Max(25, 1.5 * L)) { Destroy($"{name} sank: it's heavier than the water it can push aside"); return; }
            SyncLanded(ut + dt);
            vel += body.FixedToInertial(floatVel, ut + dt);
            angVel += body.RotationAt(ut + dt) * floatSpin;
        }

        void LeaveWater(double ut)
        {
            SyncLanded(ut);
            vel += body.FixedToInertial(floatVel, ut);
            angVel += body.RotationAt(ut) * floatSpin;
            landed = false;
            hasLaunched = true;
            rails = null;
            floatVel = Vector3d.zero;
            floatSpin = Vector3.zero;
            submerged = 0;
        }

        /// <summary>Over open sea right now (where the wheels can't roll and a touchdown is a splashdown).</summary>
        public bool OverWater(double ut) => Terrain.IsWater(body, body.InertialToFixed(pos, ut));
    }
}
