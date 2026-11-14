using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 12 tests: the aerobraking forecast, aerocapture, the Wayfarer.</summary>
    public static partial class SimTests
    {
        static void RunMilestone12()
        {
            Run("Aero forecast: skimming the air lowers the orbit, a deep pass comes down, a high one misses; Ochre arrivals too shallow escape", AeroForecasts);
            Run("Aerocapture: the planner refuses a tumble-prone lander and finds a pass for a heat-shielded probe; a full-physics pass matches the forecast", Aerocapture);
            Run("Wayfarer: the Ochre probe preset has the delta-v for the trip, a dish, legs and a parachute", WayfarerPreset);
        }

        /// <summary>A state at the top of a body's air, inbound on a hyperbola (or ellipse) with this periapsis height and arrival speed.</summary>
        static void Inbound(CelestialBody b, double peAlt, double vinf, out Vector3d r, out Vector3d v, out double t)
        {
            double rp = b.radius + peAlt, vp = Math.Sqrt(vinf * vinf + 2 * b.mu / rp), top = b.radius + b.atmosphereHeight;
            var o = Orbit.FromState(new Vector3d(rp, 0, 0), new Vector3d(0, 0, -vp), b.mu, 0);
            double lo = -3 * 86400, hi = 0;
            for (int i = 0; i < 80; i++) { double mid = (lo + hi) / 2; if (o.RadiusAt(mid) > top) lo = mid; else hi = mid; }
            o.StateAt(lo, out r, out v);
            t = lo;
        }

        static string AeroForecasts()
        {
            var home = System4[0];
            var cap = VesselSim.CreateFree(D("cap", "chute.s", "pod.wren", "shield.s"), home, new Vector3d(home.radius + 100_000, 0, 0), Vector3d.zero, Quaternion.identity);
            double m = cap.mass, cdA = cap.DragArea();
            Check(cdA > 0.3 && cdA < 5, $"capsule drag area {cdA:0.00} m2");
            // From a 100 km circular orbit, lowered periapsis at three heights
            AeroForecast From(double peAlt)
            {
                double ra = home.radius + 100_000, rp = home.radius + peAlt, a = (ra + rp) / 2;
                double va = Math.Sqrt(home.mu * (2 / ra - 1 / a));
                return AeroPass.Predict(home, new Vector3d(ra, 0, 0), new Vector3d(0, 0, -va), 0, m, cdA);
            }
            var high = From(80_000); var skim = From(68_000); var deep = From(25_000);
            Check(high.outcome == AeroOutcome.None, $"a periapsis above the air still met it ({high.outcome})");
            Check(skim.outcome == AeroOutcome.Captured && skim.ap < 100_000, $"skimming at 68 km: {skim.outcome}, apoapsis {skim.ap / 1000:0} km");
            Check(deep.outcome == AeroOutcome.Lands || deep.outcome == AeroOutcome.Impact, $"a 25 km periapsis didn't come down ({deep.outcome})");
            Check(deep.peakG > 1 && deep.peakFlux > 0, $"no deceleration or heat on the way down ({deep.peakG:0.0} g)");
            // Ochre: arriving at 2.6 km/s, a pass near the top of its thin air isn't enough
            var o = Ochre;
            Inbound(o, 45_000, 2600, out var r, out var v, out double t);
            var shallow = AeroPass.Predict(o, r, v, t, 1.2, 1.5);
            Check(shallow.outcome == AeroOutcome.Escapes, $"a 45 km pass at Ochre: {shallow.outcome}");
            return $"capsule {m:0.00} t, drag area {cdA:0.00} m2: 68 km skim leaves {skim.ap / 1000:0} x {skim.pe / 1000:0} km; 25 km pass {deep.outcome} at {deep.peakG:0.0} g; Ochre 45 km pass still escapes at {shallow.excess:0} m/s";
        }

        static string Aerocapture()
        {
            var o = Ochre;
            double vinf = 2600;
            // A long, unstable probe lander (the Wayfarer's) could fly end on or tumble side on: far too uncertain to trust
            var full = VesselSim.CreateFree(VesselDesign.Wayfarer(), o, new Vector3d(o.radius + 500_000, 0, 0), Vector3d.zero, Quaternion.identity);
            while (full.parts.Exists(p => p.def.id == "tank.l8" || p.def.id == "tank.s4")) { var dropped = full.ActivateNextStage(); if (dropped == null || full.nextStage >= full.stages.Count) break; }
            full.DragRange(out double wl, out double wm);
            Check(double.IsNaN(TransferPlanner.AerocaptureAltitude(o, vinf, full.mass, wl, wm)), "the planner trusted a tumbling-prone lander to aerocapture");
            // A blunt capsule behind a heat shield flies shield first: the planner finds its pass
            var d = D("aeroprobe", "chute.s", "probe.spark", "battery.s", "shield.s"); // a probe core keeps SAS working with nobody aboard
            var probe = VesselSim.CreateFree(d, o, new Vector3d(o.radius + 500_000, 0, 0), Vector3d.zero, Quaternion.identity);
            double m = probe.mass;
            probe.DragRange(out double least, out double most);
            double alt = TransferPlanner.AerocaptureAltitude(o, vinf, m, least, most);
            Check(!double.IsNaN(alt), $"no aerocapture height for a {m:0.00} t probe with {least:0.00}-{most:0.00} m2 at {vinf:0} m/s");
            Inbound(o, alt, vinf, out var r, out var v, out double t);
            var f = AeroPass.Predict(o, r, v, t, m, least);
            Check(f.outcome == AeroOutcome.Captured, $"the forecast at the planned height says {f.outcome}");
            // Fly the pass with full physics, nose pointing retrograde (heat shield into the air)
            var fly = VesselSim.CreateFree(d, o, r, v, Quaternion.LookRotation(Vector3.forward, (-v).normalized.ToFloat()));
            fly.throttle = 0;
            fly.sas = SasMode.Retrograde;
            double tt = t, peakQ = 0, minAlt = double.MaxValue, v0 = fly.vel.magnitude, maxTilt = 0;
            for (int i = 0; i < 200_000 && !fly.destroyed; i++)
            {
                fly.Step(0.05, tt); tt += 0.05;
                peakQ = Math.Max(peakQ, fly.dynPressure);
                minAlt = Math.Min(minAlt, fly.Altitude);
                if (fly.dynPressure > 100) maxTilt = Math.Max(maxTilt, Vector3d.Angle(Vector3d.Rotate(fly.rot, new Vector3d(0, 1, 0)), -fly.SurfaceRelVelocity) * 180 / Math.PI);
                if (fly.landed) break;
                if (fly.Altitude > o.atmosphereHeight && Vector3d.Dot(fly.pos, fly.vel) > 0) break;
            }
            Check(!fly.destroyed, $"the probe didn't survive the pass: {fly.destroyReason}");
            var after = Orbit.FromState(fly.pos, fly.vel, o.mu, tt);
            double realExit = fly.vel.magnitude, err = Math.Abs(realExit - f.exitSpeed) / f.exitSpeed;
            Check(!fly.landed && realExit < v0 * 0.6, $"the real pass only slowed {v0:0} -> {realExit:0} m/s");
            Check(after.IsElliptic && after.Apoapsis < o.SoiRadius, $"the real pass didn't capture: exit {realExit:0} m/s (escape at the top of the air is {Math.Sqrt(2 * o.mu / (o.radius + o.atmosphereHeight)):0}), tilting up to {maxTilt:0} deg");
            Check(maxTilt < 20, $"the heat shield should hold the probe shield-first, but it tipped {maxTilt:0} deg");
            Check(err < 0.15, $"forecast exit {f.exitSpeed:0} m/s, flown {realExit:0} m/s ({err * 100:0}% off); aimed {alt / 1000:0.0} km, lowest {minAlt / 1000:0.0} km, peak q {peakQ:0} Pa, speed {v0:0} -> {fly.vel.magnitude:0} m/s, worst tilt {maxTilt:0} deg; forecast clean {f.ap / 1000:0} km ap (least {least:0.00}, most {most:0.00} m2)");
            return $"lander refused ({wl:0.00}-{wm:0.00} m2); heat-shielded probe {m:0.00} t, {least:0.00}-{most:0.00} m2: pass at {alt / 1000:0} km, forecast {f.ap / 1000:0} x {f.pe / 1000:0} km, exits at {f.exitSpeed:0} m/s; flown (tilting up to {maxTilt:0} deg) exits at {realExit:0} m/s, {(after.IsElliptic && after.Apoapsis < o.SoiRadius ? $"captured {(after.Apoapsis - o.radius) / 1000:0} x {(after.Periapsis - o.radius) / 1000:0} km" : "just escaping")}; peak {peakQ / 1000:0.0} kPa";
        }

        static string WayfarerPreset()
        {
            var d = VesselDesign.Wayfarer();
            Check(d.Validate() == null, "invalid: " + d.Validate());
            var reps = Staging.AnalyzeDesign(d, 9.81);
            double total = 0; foreach (var r in reps) total += r.dvVac;
            Check(total > 5600, $"only {total:F0} m/s (orbit 3,400 + escape 1,250 + capture and landing)");
            Check(reps[0].twrAsl > 1.2, $"liftoff TWR {reps[0].twrAsl:F2}");
            foreach (var id in new[] { "dish.d", "leg.s", "chute.s", "solar.s" }) Check(d.nodes.Exists(n => n.id == id), $"no {id}");
            Check(!d.IsPlane && !d.IsRover, "the Wayfarer looks like a plane or a rover");
            return $"{total:F0} m/s vacuum over {reps.Count} stages, liftoff TWR {reps[0].twrAsl:F2}";
        }
    }
}
