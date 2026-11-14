using System;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 3b tests: wings, control surfaces, wheels, jets, runway and fairings.</summary>
    public static partial class SimTests
    {
        static void RunMilestone3b()
        {
            Run("Wings: lift grows with angle of attack and matches the flat-plate formula", WingLift);
            Run("Mirrored wings and level flight: no roll or yaw by themselves", MirroredNoRoll);
            Run("Control surfaces add authority with the right sign (W nose down, D nose right, Q roll left)", ControlSigns);
            Run("Jet: thrust factor vs speed and altitude, spool-up to full thrust", JetBehaviour);
            Run("Plane sits on its wheels on the runway, brakes on and off", RunwaySit);
            Run("Plane takes off within the runway and climbs", PlaneTakeoff);
            Run("Plane touches down on its wheels and brakes to a stop", PlaneLanding);
            Run("Fairing: shields the payload, jettisons as two halves, sheds its shell mass", FairingJettison);
        }

        /// <summary>The Kite preset in free flight: velocity horizontal (east), nose pitched up by aoa.</summary>
        static VesselSim KiteFree(CelestialBody b, double alt, double speed, float aoaDeg, out Vector3d up, out Vector3d east)
        {
            var r0 = VesselSim.CreateOnRunway(VesselDesign.Kite(), b, 0);
            up = r0.pos.normalized;
            Vector3 nose = r0.rot * Vector3.up;
            east = Vector3d.FromFloat(nose);
            float a = aoaDeg * Mathf.Deg2Rad;
            Vector3 pitched = nose * Mathf.Cos(a) + up.ToFloat() * Mathf.Sin(a);
            Quaternion rot = Quaternion.FromToRotation(nose, pitched) * r0.rot;
            Vector3d pos = up * (b.radius + alt);
            var s = VesselSim.CreateFree(VesselDesign.Kite(), b, pos, b.SurfaceVelocity(pos) + east * speed, rot);
            s.sas = SasMode.Off;
            s.throttle = 0;
            return s;
        }

        static void DeployGear(VesselSim s)
        {
            foreach (var p in s.parts) if (p.def.kind == PartKind.Gear) { p.legsDeployed = true; p.legDeploy = 1f; }
            s.gearDown = true;
        }

        static string WingLift()
        {
            var b = Body;
            double speed = 120, alt = 1000;
            double q = 0.5 * b.Density(alt) * speed * speed / 1000.0; // kPa
            string detail = "";
            double prev = double.NegativeInfinity;
            foreach (float aoa in new[] { 0f, 3f, 6f })
            {
                var s = KiteFree(b, alt, speed, aoa, out var up, out _);
                Vector3d v0 = s.vel;
                const double dt = 0.005;
                s.Step(dt, 0);
                double lift = Vector3d.Dot((s.vel - v0) / dt - b.Gravity(s.pos), up) * s.mass; // kN
                double pred = 0;
                foreach (var p in s.parts)
                {
                    if (p.def.LiftArea <= 0) continue;
                    Vector3 n = p.localRot * Vector3.forward;
                    if (Mathf.Abs(n.z) < 0.5f) continue; // vertical fin: no lift in level flight
                    double ae = (aoa + p.def.incidence) * Mathf.Deg2Rad;
                    pred += q * p.def.LiftArea * p.def.liftSlope * Math.Sin(ae) * Math.Cos(ae) * Math.Cos(ae);
                }
                Check(lift > prev, $"lift didn't grow: {lift:F1} kN at {aoa} deg after {prev:F1}");
                Check(Math.Abs(lift - pred) < 0.3 * Math.Abs(pred) + 2, $"lift {lift:F1} kN at {aoa} deg, flat-plate estimate {pred:F1} kN");
                detail += $"{aoa:0} deg: {lift:F1} kN (estimate {pred:F1})  ";
                prev = lift;
            }
            var w = KiteFree(b, alt, speed, 0, out _, out _);
            return detail + $"| weight {w.mass * b.SurfaceGravity:F1} kN";
        }

        static string MirroredNoRoll()
        {
            var b = Body;
            var s = KiteFree(b, 1000, 120, 4f, out _, out _);
            double ut = 0;
            for (int i = 0; i < 25; i++) { s.Step(0.02, ut); ut += 0.02; }
            Vector3 w = Quaternion.Inverse(s.rot) * s.angVel;
            Check(Mathf.Abs(w.y) < 0.005f, $"rolling at {w.y:F4} rad/s");
            Check(Mathf.Abs(w.z) < 0.005f, $"yawing at {w.z:F4} rad/s");
            Check(Mathf.Abs(s.com.x) < 1e-3f, $"centre of mass {s.com.x:F4} m off the centreline");
            return $"after 0.5 s: pitch {w.x:F3}, roll {w.y:F5}, yaw {w.z:F5} rad/s";
        }

        static Vector3 SpinUp(CelestialBody b, double alt, int axis, float input, out VesselSim sim)
        {
            var s = KiteFree(b, alt, 150, 2f, out _, out _);
            if (axis == 0) s.inputPitch = input;
            if (axis == 1) s.inputRoll = input;
            if (axis == 2) s.inputYaw = input;
            double ut = 0;
            for (int i = 0; i < 15; i++) { s.Step(0.02, ut); ut += 0.02; }
            sim = s;
            return Quaternion.Inverse(s.rot) * s.angVel;
        }

        static string ControlSigns()
        {
            var b = Body;
            string[] names = { "pitch", "roll", "yaw" };
            string detail = "";
            for (int a = 0; a < 3; a++)
            {
                float dAir = SpinUp(b, 1000, a, 1, out var sIn)[a] - SpinUp(b, 1000, a, 0, out var sOff)[a];
                float dVac = SpinUp(b, 100_000, a, 1, out _)[a] - SpinUp(b, 100_000, a, 0, out _)[a];
                Check(Mathf.Sign(dAir) == Mathf.Sign(dVac), $"{names[a]}: control surfaces fight the reaction wheel ({dAir:F3} vs {dVac:F3})");
                Check(Mathf.Abs(dAir) > 2f * Mathf.Abs(dVac), $"{names[a]}: surfaces add little ({dAir:F3} vs wheel only {dVac:F3})");
                Vector3d up = sIn.pos.normalized;
                if (a == 0)
                {
                    double nIn = Vector3d.Dot(Vector3d.FromFloat(sIn.rot * Vector3.up), up), nOff = Vector3d.Dot(Vector3d.FromFloat(sOff.rot * Vector3.up), up);
                    Check(nIn < nOff, "W didn't push the nose down");
                }
                if (a == 1)
                {
                    double rIn = Vector3d.Dot(Vector3d.FromFloat(sIn.rot * Vector3.right), up), rOff = Vector3d.Dot(Vector3d.FromFloat(sOff.rot * Vector3.right), up);
                    Check(rIn > rOff, "Q didn't roll left (right wing should rise)");
                }
                if (a == 2)
                {
                    Vector3d right = Vector3d.FromFloat(sOff.rot * Vector3.right);
                    double yIn = Vector3d.Dot(Vector3d.FromFloat(sIn.rot * Vector3.up), right), yOff = Vector3d.Dot(Vector3d.FromFloat(sOff.rot * Vector3.up), right);
                    Check(yIn > yOff, "D didn't turn the nose right");
                }
                detail += $"{names[a]} {dAir / dVac:F0}x  ";
            }
            return "authority vs reaction wheel alone at 150 m/s: " + detail;
        }

        static string JetBehaviour()
        {
            var b = Body;
            var pad = VesselSim.CreateOnRunway(VesselDesign.Kite(), b, 0);
            var jet = pad.parts.First(p => p.def.IsJet).def;
            double f0 = pad.JetFactor(jet);
            double fMid = KiteFree(b, 500, jet.jetMaxSpeed * 0.5, 0, out _, out _).JetFactor(jet);
            double fTop = KiteFree(b, 500, jet.jetMaxSpeed, 0, out _, out _).JetFactor(jet);
            double fHigh = KiteFree(b, 30_000, jet.jetMaxSpeed * 0.5, 0, out _, out _).JetFactor(jet);
            double fVac = KiteFree(b, 100_000, 300, 0, out _, out _).JetFactor(jet);
            Check(Math.Abs(f0 - 1) < 0.02, $"static sea-level factor {f0:F3}");
            Check(fMid > 1.3, $"half top speed factor {fMid:F2}");
            Check(fTop < 0.01, $"top speed factor {fTop:F3}");
            Check(fHigh < 0.15, $"30 km factor {fHigh:F3}");
            Check(fVac == 0, $"vacuum factor {fVac:F3}");

            pad.ActivateNextStage();
            double ut = 0, t1 = 0;
            for (int i = 0; i < 150; i++) { pad.Step(0.02, ut); ut += 0.02; if (i == 49) t1 = pad.thrustNow; }
            Check(t1 > 30 && t1 < 80, $"thrust after 1 s of spool-up {t1:F0} kN");
            Check(pad.thrustNow > 110 && pad.thrustNow < 145, $"thrust after 3 s {pad.thrustNow:F0} kN");
            return $"factor static {f0:F2}, half speed {fMid:F2}, top speed {fTop:F2}, 30 km {fHigh:F3}; thrust 1 s {t1:F0} kN, 3 s {pad.thrustNow:F0} kN";
        }

        static string RunwaySit()
        {
            var b = Body;
            var s = VesselSim.CreateOnRunway(VesselDesign.Kite(), b, 0);
            double ut = 0;
            for (int i = 0; i < 250; i++) { s.Step(0.02, ut); ut += 0.02; }
            Check(s.landed && !s.destroyed, "didn't stay parked with the brakes on");
            double h0 = s.Altitude;
            Vector3d f0 = b.InertialToFixed(s.pos, ut);
            s.brakesOn = false;
            for (int i = 0; i < 500; i++) { s.Step(0.02, ut); ut += 0.02; }
            Check(!s.destroyed, s.destroyReason);
            double drift = (b.InertialToFixed(s.pos, ut) - f0).magnitude;
            double noseUp = Math.Asin(Vector3d.Dot(Vector3d.FromFloat(s.rot * Vector3.up), s.pos.normalized)) * 180 / Math.PI;
            double wing = Math.Asin(Vector3d.Dot(Vector3d.FromFloat(s.rot * Vector3.right), s.pos.normalized)) * 180 / Math.PI;
            Check(Math.Abs(s.Altitude - h0) < 0.15, $"height changed {s.Altitude - h0:F3} m on the wheels");
            Check(s.SurfaceRelVelocity.magnitude < 0.5, $"still moving {s.SurfaceRelVelocity.magnitude:F2} m/s");
            Check(drift < 2, $"rolled {drift:F2} m");
            Check(Math.Abs(noseUp) < 3 && Math.Abs(wing) < 1, $"attitude nose {noseUp:F1} deg, wing {wing:F1} deg");
            return $"10 s on the wheels: settled {s.Altitude - h0:F3} m, drift {drift:F2} m, nose {noseUp:F2} deg, wings {wing:F2} deg";
        }

        static string PlaneTakeoff()
        {
            var b = Body;
            var s = VesselSim.CreateOnRunway(VesselDesign.Kite(), b, 0);
            s.ActivateNextStage();
            s.brakesOn = false;
            s.throttle = 1;
            Vector3d f0 = b.InertialToFixed(s.pos, 0);
            Vector3d eastFixed = b.InertialToFixed(Vector3d.FromFloat(s.rot * Vector3.up), 0);
            double ut = 0, roll = -1, vLift = 0, tLift = 0, maxAlt = 0;
            for (int i = 0; i < 3000 && !s.destroyed; i++)
            {
                Vector3d up = s.pos.normalized, east = b.FixedToInertial(eastFixed, ut);
                east = (east - up * Vector3d.Dot(east, up)).normalized;
                double speed = s.SurfaceRelVelocity.magnitude;
                double pitch = (speed < 70 ? 0 : 10) * Math.PI / 180;
                s.steerDirection = east * Math.Cos(pitch) + up * Math.Sin(pitch);
                s.Step(0.02, ut);
                ut += 0.02;
                double radar = s.RadarAltitude(ut);
                if (roll < 0 && radar > 5) { roll = (b.InertialToFixed(s.pos, ut) - f0).magnitude; vLift = speed; tLift = ut; }
                maxAlt = Math.Max(maxAlt, s.Altitude);
            }
            Check(!s.destroyed, s.destroyReason);
            Check(roll > 0, "never left the ground");
            Check(roll < 2000, $"needed {roll:F0} m of runway");
            Check(s.Altitude > 300, $"only {s.Altitude:F0} m up after 60 s");
            return $"airborne after {tLift:F1} s and {roll:F0} m at {vLift:F0} m/s; {s.Altitude:F0} m up at 60 s, {s.SurfaceRelVelocity.magnitude:F0} m/s";
        }

        static string PlaneLanding()
        {
            var b = Body;
            var r0 = VesselSim.CreateOnRunway(VesselDesign.Kite(), b, 0);
            Vector3d up = r0.pos.normalized, nose = Vector3d.FromFloat(r0.rot * Vector3.up);
            // Final approach: 4 m up, 112 m/s, sinking 1 m/s, engine off, holding the nose 6 deg up until the wheels touch.
            Vector3d pos = r0.pos + up * 4.0;
            float flare = 6f * Mathf.Deg2Rad;
            Quaternion rot = Quaternion.FromToRotation(nose.ToFloat(), nose.ToFloat() * Mathf.Cos(flare) + up.ToFloat() * Mathf.Sin(flare)) * r0.rot;
            var s = VesselSim.CreateFree(VesselDesign.Kite(), b, pos, b.SurfaceVelocity(pos) + nose * 112 - up * 1.0, rot);
            DeployGear(s);
            s.throttle = 0;
            Vector3d f0 = b.InertialToFixed(pos, 0);
            double ut = 0, maxSink = 0, tTouch = 0;
            bool touched = false;
            for (int i = 0; i < 6000 && !s.destroyed && !s.landed; i++)
            {
                Vector3d u = s.pos.normalized, v = s.SurfaceRelVelocity;
                Vector3d horiz = v - u * Vector3d.Dot(v, u);
                double pitch = touched ? 0 : flare;
                s.steerDirection = horiz.magnitude > 5 ? (Vector3d?)(horiz.normalized * Math.Cos(pitch) + u * Math.Sin(pitch)) : null;
                s.Step(0.02, ut);
                ut += 0.02;
                double radar = s.RadarAltitude(ut);
                if (!touched) maxSink = Math.Max(maxSink, -Vector3d.Dot(s.SurfaceRelVelocity, s.pos.normalized));
                if (!touched && radar < 0.05) { touched = true; tTouch = ut; }
                if (touched && ut > tTouch + 1.5) s.brakesOn = true;
            }
            Check(maxSink < 4, $"hit the runway sinking {maxSink:F1} m/s");
            Check(!s.destroyed, s.destroyReason);
            Check(touched, "never touched down");
            Check(s.landed, $"still rolling at {s.SurfaceRelVelocity.magnitude:F1} m/s after {ut:F0} s");
            double dist = (b.InertialToFixed(s.pos, ut) - f0).magnitude;
            Check(dist < 2000, $"needed {dist:F0} m to stop (the runway is {LaunchSite.RunwayLength:F0} m)");
            return $"touchdown sink rate {maxSink:F1} m/s; stopped and parked after {dist:F0} m in {ut:F0} s";
        }

        static string FairingJettison()
        {
            var b = Body;
            var d = D("fairing", "probe.spark", "fairing.s", "tank.s2", "eng.ember");
            Vector3d up = CelestialBody.DirectionFromLatLon(0, 0);
            var s = VesselSim.CreateFree(d, b, up * (b.radius + 20_000), b.SurfaceVelocity(up * (b.radius + 20_000)) + up * 300,
                Quaternion.FromToRotation(Vector3.up, up.ToFloat()));
            var probe = s.parts.First(p => p.def.id == "probe.spark");
            Check(probe.enclosed, "the probe isn't inside the fairing");
            double m0 = s.mass;
            System.Collections.Generic.List<VesselSim> halves = null;
            for (int k = 0; k < 6 && s.HasStagesLeft; k++)
            {
                var spawned = s.ActivateNextStage();
                if (spawned.Exists(v => v.parts.Exists(p => p.def.kind == PartKind.FairingHalf))) { halves = spawned; break; }
            }
            Check(halves != null, "no stage jettisoned the fairing");
            Check(halves.Count == 2, $"{halves.Count} pieces instead of 2 halves");
            Check(Math.Abs(m0 - s.mass - 0.25) < 1e-3, $"vessel lost {m0 - s.mass:F3} t, the shell is 0.25 t");
            Check(Math.Abs(halves.Sum(h => h.mass) - 0.25) < 1e-3, $"halves weigh {halves.Sum(h => h.mass):F3} t");
            Check(!probe.enclosed, "the probe is still shielded after the jettison");
            Vector3d sep = halves[0].vel - halves[1].vel;
            Check(sep.magnitude > 4, $"halves separate at only {sep.magnitude:F1} m/s");
            return $"shell {m0 - s.mass:F2} t split into 2 halves separating at {sep.magnitude:F1} m/s";
        }
    }
}
