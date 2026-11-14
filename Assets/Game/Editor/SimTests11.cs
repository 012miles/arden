using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 11a tests: the sun as a real body.</summary>
    public static partial class SimTests
    {
        static void RunMilestone11()
        {
            Run("Sun: Arden goes round Helion once a year; its sphere of influence holds both moons; the sun starts where it always was", SunSystem);
            Run("Seasons: Arden's tilt takes the sun 29 deg north and south of the equator over the year; night and eclipses still work", SunSeasons);
            Run("Escape: a fast burn leaves Arden's pull for an orbit round Helion, the plan and the flight agree, nothing jumps", SunEscape);
            Run("Sun career: escaping counts as a record and crew experience; flying into Helion burns you up", SunCareer);
        }

        static CelestialBody Helion => System4[3];

        static string SunSystem()
        {
            var home = System4[0]; var sun = Helion;
            Check(sun.isStar && home.parent == sun && home.isHome && sun.children.Contains(home), "Arden doesn't go round a star");
            Check(home.AbsolutePosition(12345).magnitude < 1e-6, "home isn't at the origin of absolute positions");
            double soi = home.SoiRadius, year = home.OrbitPeriod;
            Check(soi > 60e6 && soi < 100e6, $"Arden's sphere of influence {soi / 1e6:0} Mm");
            Check(Tal.orbitRadius + Tal.SoiRadius < soi && Vesk.orbitRadius + Vesk.SoiRadius < soi, "a moon pokes out of Arden's sphere of influence");
            Check(year / home.rotationPeriod > 250 && year / home.rotationPeriod < 450, $"a year is {year / home.rotationPeriod:0} Arden days");
            double start = Vector3d.Angle(Sun.DirectionAt(0), Sun.Fixed);
            Check(start < 1e-6, $"the sun starts {start * 180 / Math.PI:0.0000} deg away from where it used to be");
            // Moons keep their old positions relative to Arden, to the millimetre
            double t = 7_654_321;
            double moonErr = (Tal.AbsolutePosition(t) - Tal.PositionAt(t)).magnitude;
            Check(moonErr < 1e-3, $"Tal moved {moonErr:0.000} m");
            // A quarter year on, the sun has swung round by a quarter turn
            double quarter = Vector3d.Angle(Sun.DirectionAt(0), Sun.DirectionAt(year / 4));
            Check(Math.Abs(quarter - Math.PI / 2) < 0.01, $"a quarter year moved the sun {quarter * 180 / Math.PI:0.0} deg");
            double v = home.VelocityAt(0).magnitude;
            return $"Helion {sun.radius / 1e6:0} Mm across the middle; Arden 13.6 Gm out at {v / 1000:0.0} km/s, a {year / 86400:0}-day year ({year / home.rotationPeriod:0} Arden days); SOI {soi / 1e6:0} Mm";
        }

        static string SunSeasons()
        {
            var home = System4[0];
            double year = home.OrbitPeriod, hi = -1, lo = 1;
            for (int i = 0; i < 48; i++)
            {
                double y = Sun.DirectionAt(year * i / 48).y; // the sun's height above Arden's equator (sin of declination)
                hi = Math.Max(hi, y); lo = Math.Min(lo, y);
            }
            double north = Math.Asin(hi) * 180 / Math.PI, south = Math.Asin(lo) * 180 / Math.PI;
            Check(Math.Abs(north - 28.8) < 1 && Math.Abs(south + 28.8) < 1, $"the sun ranges {south:0.0} to {north:0.0} deg");
            // Day and night: a satellite on the sunward side is lit, one straight behind Arden isn't, at any time of year
            foreach (double t in new[] { 0.0, year * 0.37 })
            {
                Vector3d s = Sun.DirectionAt(t);
                var day = InOrbit(D("sat", "probe.spark"), home, home.radius + 300_000, 0);
                day.pos = s * (home.radius + 300_000);
                var night = InOrbit(D("sat", "probe.spark"), home, home.radius + 300_000, 0);
                night.pos = -s * (home.radius + 300_000);
                Check(day.InSunlight(t) && !night.InSunlight(t), $"at {t / 86400:0} d: day side lit {day.InSunlight(t)}, night side lit {night.InSunlight(t)}");
            }
            return $"sun from {south:0.0} to {north:0.0} deg over the {year / 86400:0}-day year; day and night sides right in two seasons";
        }

        static string SunEscape()
        {
            var home = System4[0]; var sun = Helion;
            double r = home.radius + 100_000, vesc = Math.Sqrt(2 * home.mu / r);
            var v = InOrbit(D("probe", "probe.spark"), home, r, 0, (vesc + 300) / Math.Sqrt(home.mu / r));
            v.sas = SasMode.Off;
            double ut = 0;
            var plan = Patcher.Predict(home, v.pos, v.vel, ut, 3);
            Check(plan.Count >= 2 && plan[0].end == SegmentEnd.ExitSoi && plan[1].body == sun, $"the plan doesn't leave Arden for Helion ({plan.Count} pieces, {plan[0].end})");
            double tExit = plan[0].tEnd, dPos = double.NaN, dVel = double.NaN;
            for (int i = 0; i < 400 && v.body == home; i++)
            {
                Vector3d p0 = v.pos, v0 = v.vel;
                double t0 = ut;
                v.RailsAdvance(3600, ut, out double used);
                ut += used;
                v.CheckSoi(ut);
                if (v.body != home)
                {
                    // Where Arden's own orbit says it should be (home sits at the origin of absolute positions)
                    Orbit.FromState(p0, v0, home.mu, t0).StateAt(ut, out var pe, out var ve);
                    dPos = (v.AbsolutePosition(ut) - pe).magnitude;
                    dVel = (v.AbsoluteVelocity(ut) - ve).magnitude;
                }
            }
            Check(v.body == sun, $"never left Arden (still around {v.body.name} after {ut / 86400:0.0} d)");
            Check(Math.Abs(ut - tExit) < 120, $"left at {ut / 3600:0.0} h, planned {tExit / 3600:0.0} h");
            Check(dPos < 1 && dVel < 0.01, $"the switch to Helion jumped {dPos:0.000} m, {dVel:0.0000} m/s");
            var o = Orbit.FromState(v.pos, v.vel, sun.mu, ut);
            double aArden = home.orbitRadius;
            Check(o.IsElliptic && Math.Abs(o.a - aArden) < aArden * 0.3, // 1.5 km/s spare either way of Arden's 11.5 km/s
                 $"orbit round Helion a = {o.a / 1e9:0.00} Gm (Arden's is {aArden / 1e9:0.00})");
            return $"escape burn {vesc + 300:0} m/s at 100 km; out of Arden's pull after {ut / 3600:0} h; now round Helion, {o.Apoapsis / 1e9:0.00} x {o.Periapsis / 1e9:0.00} Gm";
        }

        static string SunCareer()
        {
            var u = CareerUniverse();
            var c = u.career;
            double f0 = c.funds;
            c.OnFlags(VFlags.SoiSun);
            Check((c.records & Records.Escape) != 0 && c.funds > f0, "escaping Arden isn't a record");
            Check(Crew.XpOf(VFlags.SoiSun) == 6, "no crew experience for going round the sun");
            var sun = Helion;
            var v = VesselSim.CreateFree(D("probe", "probe.spark"), sun, new Vector3d(sun.radius + VesselSim.SunBurnAltitude * 0.5, 0, 0), new Vector3d(0, 0, -30_000), Quaternion.identity);
            v.Step(0.02, 0);
            Check(v.destroyed && v.destroyReason.Contains("burned"), $"a probe {VesselSim.SunBurnAltitude * 0.5 / 1e6:0} Mm from Helion survived: {v.destroyReason}");
            return $"record paid {c.funds - f0:N0}; a probe {VesselSim.SunBurnAltitude * 0.5 / 1e6:0} Mm above Helion: \"{v.destroyReason}\"";
        }
    }
}
