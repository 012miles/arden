using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>When (s from 0) the launch site has the sun at this height (dot of up with the sun direction).</summary>
        static double TimeWithSun(CelestialBody home, Vector3d fixedDir, double sunDot, double from)
        {
            double best = from, bd = 9;
            for (double t = from; t < from + home.rotationPeriod; t += 60)
            {
                double d = Math.Abs(Vector3d.Dot(home.FixedToInertial(fixedDir, t), Sun.DirectionFrom(home.AbsolutePosition(t), t)) - sunDot);
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        VesselSim Fly(VesselDesign d, CelestialBody b, double alt, double lat, double lon, double ut, bool orbitSpeed, double speed = 900)
        {
            var root = GameRoot.I;
            Vector3d dirFixed = CelestialBody.DirectionFromLatLon(lat, lon);
            Vector3d dir = b.FixedToInertial(dirFixed, ut);
            Vector3d east = CelestialBody.EastAt(dir);
            double r = b.radius + alt;
            Vector3d vel = orbitSpeed ? east * Math.Sqrt(b.mu / r) : b.SurfaceVelocity(dir * r) + east * speed + dir * (speed * 0.13);
            var v = VesselSim.CreateFree(d, b, dir * r, vel, Quaternion.LookRotation(east.ToFloat(), dir.ToFloat()));
            v.throttle = 0;
            v.hasLaunched = true;
            root.universe.Add(v);
            root.Fly(v);
            return v;
        }

        /// <summary>
        /// Milestone 8a: a night launch lit by its own engines; climbing through 24 km in daylight above the clouds;
        /// Arden from orbit at the day-night line with the atmosphere's glow, clouds, stars and the sun; the map; and
        /// a lander's spotlights on Tal's night side.
        /// </summary>
        IEnumerator LooksTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;

            u.ut = TimeWithSun(home, LaunchSite.Direction, -0.45, 0);
            root.Launch(VesselDesign.Default());
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            f.Stage();
            yield return new WaitForSecondsRealtime(1.3f);
            f.SetCamera(200f, 4f, 38f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("100_night_launch");
            Log($"night launch at ut {u.ut:0}: sun height {Vector3d.Dot(home.FixedToInertial(LaunchSite.Direction, u.ut), Sun.DirectionAt(GameRoot.I.universe.ut)):0.00}, altitude {f.active.Altitude:0} m");

            // Daylight, 24 km up over land with clouds below
            double t2 = TimeWithSun(home, CelestialBody.DirectionFromLatLon(8, 40), 0.6, u.ut);
            u.ut = t2;
            var climber = Fly(VesselDesign.Default(), home, 24_000, 8, 40, u.ut, false);
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3d up = climber.pos.normalized;
            Vector3d sunFlat = Sun.DirectionAt(GameRoot.I.universe.ut) - up * Vector3d.Dot(Sun.DirectionAt(GameRoot.I.universe.ut), up);
            f.LookAlong(-sunFlat.normalized, 40f, 30f, 6f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("101_climb_24km");

            // Orbit at the day-night line, looking along the limb toward the sun
            double t3 = TimeWithSun(home, CelestialBody.DirectionFromLatLon(0, 90), 0.05, u.ut);
            u.ut = t3;
            var orbiter = Fly(VesselDesign.Stack("Lookout", "probe.spark", "tank.s1"), home, 260_000, 0, 90, u.ut, true);
            yield return new WaitForSecondsRealtime(1.2f);
            up = orbiter.pos.normalized;
            sunFlat = Sun.DirectionAt(GameRoot.I.universe.ut) - up * Vector3d.Dot(Sun.DirectionAt(GameRoot.I.universe.ut), up);
            f.LookAlong(sunFlat.normalized, 18f, 25f, 40f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("102_orbit_limb");

            f.mapView = true;
            f.SetMapView(home, (float)(home.radius * 3.6), -15f, -10f); // mostly the day side, with the night line showing
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("103_map_glow");
            f.mapView = false;

            // A lander on Tal's night side with its spotlights on
            var tal = root.moon;
            Vector3d night = -Sun.DirectionAt(GameRoot.I.universe.ut);
            Vector3d spot = (tal.InertialToFixed(night, u.ut) + new Vector3d(0, 0.35, 0)).normalized;
            var ld = VesselDesign.Stack("Night lander", "chute.s", "pod.wren", "tank.s2", "eng.wisp");
            int tank = ld.nodes.FindIndex(n => n.id == "tank.s2");
            DesignOps.AttachSurfaceAt(ld, tank, 0.95f, 45f, "leg.s", 4, false);
            DesignOps.AttachSurfaceAt(ld, ld.nodes.FindIndex(n => n.id == "pod.wren"), 0.75f, 20f, "light.s", 3, false);
            var lander = VesselSim.CreateLanded(ld, tal, spot, u.ut);
            lander.throttle = 0;
            u.Add(lander);
            foreach (var p in lander.parts) if (p.def.kind == PartKind.Light) p.lightOn = true;
            lander.lightsOn = true;
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(1.2f);
            f.SetCamera(150f, 18f, 16f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("104_night_lights");
            Log($"night lander sunlit={lander.InSunlight(f.ut)}; lights {lander.parts.Count(p => p.lightOn)}");
        }
    }
}
