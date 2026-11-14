using System;
using UnityEngine;

namespace Rocket
{
    public enum AeroOutcome { None, Captured, Escapes, Lands, Impact }

    /// <summary>What a pass through a planet's air would do (see AeroPass.Predict).</summary>
    public class AeroForecast
    {
        public AeroOutcome outcome;
        public CelestialBody body;
        public double ap, pe;            // after leaving the air (m above the surface); captured only
        public double excess;            // still escaping: speed left at infinity (m/s)
        public double impactSpeed;       // hits the ground: how fast (m/s)
        public double peakG, peakFlux;   // worst deceleration (g) and heating (W/m^2) on the way through
        public double tEntry, tExit;     // when it meets the air and (if it does) leaves it
        public double exitSpeed;         // speed (inertial, m/s) as it climbs back out of the air

        public string Describe()
        {
            string worst = $" (peak {peakG:0.0} g)";
            switch (outcome)
            {
                case AeroOutcome.Captured: return $"Aerobrake at {body.name}: leaves the air in a {Ui.Dist(ap)} x {Ui.Dist(pe)} orbit{worst}";
                case AeroOutcome.Escapes: return $"Aerobrake at {body.name}: not enough, still escaping at {excess:0} m/s{worst}";
                case AeroOutcome.Lands: return body.IsGasGiant ? $"Too deep: sinks into {body.name}'s clouds{worst}" : $"Into {body.name}'s air for good: slows to a landing{worst}";
                case AeroOutcome.Impact: return body.IsGasGiant ? $"Too deep: sinks into {body.name}'s clouds{worst}" : $"Too deep: hits {body.name} at {impactSpeed:0} m/s{worst}";
                default: return null;
            }
        }
    }

    /// <summary>
    /// Forecasts a pass through a planet's atmosphere: coasts the orbit to where it meets the air, then steps through
    /// the air with drag (the air turns with the planet) until the vessel climbs back out, slows into it for good, or
    /// hits the ground. Point mass with a fixed drag area, so it's a guide, not a promise: it assumes the craft is held
    /// steady with its base into the air, and one that wobbles (as small probes do) brakes noticeably less.
    /// </summary>
    public static class AeroPass
    {
        public static AeroForecast Predict(CelestialBody b, Vector3d pos, Vector3d vel, double ut, double massT, double cdA)
        {
            var f = new AeroForecast { body = b };
            if (!b.HasAtmosphere || massT <= 0 || cdA <= 0) return f;
            double top = b.radius + b.atmosphereHeight;
            var o = Orbit.FromState(pos, vel, b.mu, ut);
            if (o.Periapsis > top) return f; // never meets the air
            // Coast (exactly, on rails) to the top of the air if not in it already
            double t = ut;
            if (pos.magnitude > top)
            {
                double tPe = ut + o.TimeToPeriapsis(ut);
                if (double.IsNaN(tPe) || tPe < ut) return f; // heading away
                double lo = ut, hi = tPe;
                for (int i = 0; i < 80; i++) { double mid = (lo + hi) / 2; if (o.RadiusAt(mid) > top) lo = mid; else hi = mid; }
                t = hi;
                o.StateAt(t, out pos, out vel);
            }
            f.tEntry = t;
            Vector3d omega = b.AngularVelocityVector;
            double massKg = massT * 1000;
            Vector3d Acc(Vector3d r, Vector3d v, out double q, out double flux)
            {
                double alt = r.magnitude - b.radius, rho = b.Density(alt);
                Vector3d vr = v - Vector3d.Cross(omega, r);
                double s = vr.magnitude;
                q = 0.5 * rho * s * s;
                flux = rho > 0 && s > Thermal.V0 ? Thermal.HeatK * Math.Sqrt(rho) * (s * s * s - Thermal.V0 * Thermal.V0 * Thermal.V0) : 0;
                Vector3d drag = s > 1e-3 ? vr * (-q * cdA / (massKg * s)) : Vector3d.zero;
                return b.Gravity(r) + drag;
            }
            double g0 = 9.80665;
            for (int i = 0; i < 40_000; i++)
            {
                double alt = pos.magnitude - b.radius;
                double rho = b.Density(Math.Max(0, alt));
                double dt = rho > b.seaLevelDensity * 1e-3 ? 0.25 : 1.0;
                // RK4
                Vector3d a1 = Acc(pos, vel, out double q, out double fl);
                f.peakG = Math.Max(f.peakG, (a1 - b.Gravity(pos)).magnitude / g0);
                f.peakFlux = Math.Max(f.peakFlux, fl);
                Vector3d a2 = Acc(pos + vel * (dt / 2), vel + a1 * (dt / 2), out _, out _);
                Vector3d a3 = Acc(pos + (vel + a1 * (dt / 2)) * (dt / 2), vel + a2 * (dt / 2), out _, out _);
                Vector3d a4 = Acc(pos + (vel + a2 * (dt / 2)) * dt, vel + a3 * dt, out _, out _);
                pos = pos + (vel + (vel + a1 * (dt / 2)) * 2 + (vel + a2 * (dt / 2)) * 2 + (vel + a3 * dt)) * (dt / 6);
                vel = vel + (a1 + a2 * 2 + a3 * 2 + a4) * (dt / 6);
                t += dt;
                alt = pos.magnitude - b.radius;
                double ground = Terrain.Height(b, b.InertialToFixed(pos, t).normalized);
                Vector3d vrel = vel - Vector3d.Cross(omega, pos);
                if (alt <= ground)
                {
                    double sp = vrel.magnitude;
                    f.outcome = sp < 20 ? AeroOutcome.Lands : AeroOutcome.Impact; // a parachute's job from here
                    f.impactSpeed = sp;
                    return f;
                }
                // Slowed right down deep in the air: it'll come down (a parachute or engines from here)
                if (alt < b.atmosphereHeight * 0.6 && vrel.magnitude < 400 && Vector3d.Dot(vel, pos) < 0) { f.outcome = AeroOutcome.Lands; return f; }
                if (alt > b.atmosphereHeight && Vector3d.Dot(vel, pos) > 0)
                {
                    f.tExit = t;
                    f.exitSpeed = vel.magnitude;
                    var after = Orbit.FromState(pos, vel, b.mu, t);
                    if (after.IsElliptic && after.Apoapsis < b.SoiRadius)
                    {
                        f.outcome = AeroOutcome.Captured;
                        f.ap = after.Apoapsis - b.radius;
                        f.pe = after.Periapsis - b.radius;
                    }
                    else
                    {
                        f.outcome = AeroOutcome.Escapes;
                        double e = vel.sqrMagnitude / 2 - b.mu / pos.magnitude;
                        f.excess = e > 0 ? Math.Sqrt(2 * e) : 0;
                    }
                    return f;
                }
            }
            f.outcome = AeroOutcome.Lands;
            return f;
        }
    }
}
