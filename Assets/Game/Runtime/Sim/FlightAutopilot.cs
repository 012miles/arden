using System;

namespace Rocket
{
    /// <summary>
    /// Simple launch-to-orbit autopilot: vertical climb, gravity turn to the east, coast to apoapsis,
    /// circularize. Used by the automated tests and as an in-game demo (P key).
    /// </summary>
    public class FlightAutopilot
    {
        public enum Phase { Launch, Ascent, Coast, Circularize, Done }

        public Phase phase = Phase.Launch;
        public double targetAltitude = 80_000;
        public double turnStart = 1_000, turnEnd = 50_000;
        public string status = "";
        double lastStageTime = -10;
        double liftoffTwr = double.NaN;

        static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);

        static Vector3d Horizontal(Vector3d v, Vector3d up)
        {
            var h = v - up * Vector3d.Dot(v, up);
            return h.sqrMagnitude > 1e-6 ? h.normalized : CelestialBody.EastAt(up);
        }

        /// <summary>Seconds of burning needed to circularize at apoapsis, at full throttle.</summary>
        public static double CircularizeBurnTime(VesselSim v, Orbit o)
        {
            if (!o.IsElliptic) return 0;
            double rAp = o.Apoapsis;
            double vAp = Math.Sqrt(Math.Max(0, v.body.mu * (2 / rAp - 1 / o.a)));
            double dv = Math.Max(0, Math.Sqrt(v.body.mu / rAp) - vAp);
            double thrust = v.PotentialThrust();
            if (thrust <= 0) return 0;
            return dv / (thrust / v.mass);
        }

        /// <summary>How long until the circularization burn should start (infinite if not coasting).</summary>
        public double TimeUntilBurn(VesselSim v, double ut)
        {
            if (phase == Phase.Done) return double.PositiveInfinity;
            if (phase != Phase.Coast) return 0; // launching, ascending or circularizing: burning now
            var o = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
            if (!o.IsElliptic) return double.PositiveInfinity;
            return o.TimeToApoapsis(ut) - (CircularizeBurnTime(v, o) * 0.5 + 2);
        }

        /// <summary>Steers and throttles the vessel. Returns true when it wants the next stage now.</summary>
        public bool Update(VesselSim v, double ut)
        {
            var body = v.body;
            double R = body.radius, alt = v.Altitude;
            Vector3d up = v.pos.normalized;
            Vector3d east = CelestialBody.EastAt(up);
            var orbit = Orbit.FromState(v.pos, v.vel, body.mu, ut);
            double apAlt = orbit.Apoapsis - R, peAlt = orbit.Periapsis - R;
            bool canStage = ut - lastStageTime > 1.0;
            bool stage = false;

            switch (phase)
            {
                case Phase.Launch:
                    v.throttle = 1;
                    v.steerDirection = up;
                    if (v.nextStage == 0 && canStage) stage = true;
                    if (!v.landed) phase = Phase.Ascent;
                    status = "Liftoff";
                    break;

                case Phase.Ascent:
                {
                    double frac = Clamp01((alt - turnStart) / (turnEnd - turnStart));
                    double pitch = Math.Max(2, 90 - 90 * Math.Sqrt(frac)); // profile, degrees above the horizon
                    // Real gravity turn: a small kick right after liftoff, then stay close to the airflow.
                    // The allowed angle of attack shrinks as dynamic pressure builds, so fast rockets can't get stuck vertical.
                    Vector3d srf = v.SurfaceRelVelocity;
                    double speed = srf.magnitude;
                    if (double.IsNaN(liftoffTwr))
                    {
                        v.EngineThrustSplit(out double s0, out double l0);
                        liftoffTwr = (s0 + l0) / (v.mass * body.SurfaceGravity);
                    }
                    // Punchier rockets need a bigger kick: they outrun gravity's bending of the path.
                    double kick = Math.Max(5, Math.Min(20, 5 * Math.Pow(liftoffTwr / 1.9, 2)));
                    pitch = Math.Min(pitch, 90 - kick * Clamp01((speed - 30) / 50));
                    double q = v.dynPressure / 1000;
                    if (speed >= 50 && q > 25)
                    {
                        double pro = Math.Asin(Math.Max(-1, Math.Min(1, Vector3d.Dot(srf / speed, up)))) * 180 / Math.PI;
                        pitch = Math.Max(pro - 8, Math.Min(pro + 8, pitch));
                    }
                    pitch = Math.Max(2, pitch);
                    double pr = pitch * Math.PI / 180;
                    v.steerDirection = up * Math.Sin(pr) + east * Math.Cos(pr);
                    // Limit acceleration to ~2.2 g in the thick lower air to cut drag losses (solids can't throttle).
                    double g = body.SurfaceGravity;
                    v.EngineThrustSplit(out double solid, out double liquid);
                    double limit = alt < 25_000 ? 2.2 * g * v.mass : double.PositiveInfinity;
                    double thr = liquid > 0 ? Math.Max(0.35, Math.Min(1, (limit - solid) / liquid)) : 1;
                    v.throttle = apAlt > targetAltitude - 4000 ? Math.Min(thr, 0.35) : thr;
                    if (apAlt >= targetAltitude)
                    {
                        v.throttle = 0;
                        phase = Phase.Coast;
                    }
                    status = $"Gravity turn, pitch {pitch:F0} deg";
                    break;
                }

                case Phase.Coast:
                {
                    v.steerDirection = Horizontal(v.vel, up);
                    v.throttle = alt < body.atmosphereHeight && apAlt < targetAltitude - 600 ? 0.25 : 0;
                    double tAp = orbit.TimeToApoapsis(ut);
                    if (alt > body.atmosphereHeight && tAp < CircularizeBurnTime(v, orbit) * 0.5 + 2)
                        phase = Phase.Circularize;
                    status = $"Coasting to apoapsis, {tAp:F0} s";
                    break;
                }

                case Phase.Circularize:
                {
                    // Burn horizontally, tilting down while still climbing and up if already falling,
                    // so a late start doesn't just push the far side of the orbit higher.
                    double vs = Vector3d.Dot(v.vel, up);
                    double tilt = Math.Max(-0.25, Math.Min(0.6, -vs / 120.0));
                    v.steerDirection = Horizontal(v.vel, up) * Math.Cos(tilt) + up * Math.Sin(tilt);
                    double ecc = apAlt - peAlt;
                    v.throttle = ecc < 15_000 ? 0.4 : 1;
                    bool safe = peAlt > body.atmosphereHeight + 2000;
                    bool done = peAlt > targetAltitude - 2500
                                || (safe && ecc < 2500)
                                || (safe && apAlt > targetAltitude * 2.5)
                                || (v.PotentialThrust() <= 0 && !v.NextStageHasEngines());
                    if (done)
                    {
                        v.throttle = 0;
                        phase = Phase.Done;
                    }
                    status = $"Circularizing, periapsis {peAlt / 1000:F1} km";
                    break;
                }

                case Phase.Done:
                    v.throttle = 0;
                    v.steerDirection = null;
                    status = "In orbit";
                    break;
            }

            bool burning = phase == Phase.Ascent || phase == Phase.Circularize || (phase == Phase.Coast && v.throttle > 0);
            if (burning && canStage && v.throttle > 0 && v.thrustNow <= 0 && !v.landed && v.NextStageHasEngines())
                stage = true;
            if (burning && canStage && !v.landed && v.NextStageDropsSpentBoosters())
                stage = true; // side boosters have burned out: drop the dead weight
            if (canStage && !v.landed && alt > 25_000 && v.NextStageIsTowerJettison())
                stage = true; // past the worst of the climb: lose the escape tower's 0.9 t
            if (stage) lastStageTime = ut;
            return stage;
        }
    }
}
