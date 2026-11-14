using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        static Vector3d HighSpot(CelestialBody b, int samples, out double h)
        {
            var rng = new System.Random(5);
            Vector3d best = Vector3d.up;
            h = double.MinValue;
            for (int i = 0; i < samples; i++)
            {
                var d = new Vector3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
                if (d.sqrMagnitude > 1 || d.sqrMagnitude < 1e-3) continue;
                d = d.normalized;
                double x = Terrain.Height(b, d);
                if (x > h) { h = x; best = d; }
            }
            return best;
        }

        /// <summary>
        /// Milestone 8b: flying low past Arden's highest mountains with the sun low; a lander on Tal's cratered ground;
        /// Tal's craters from 30 km; and Tal in the map with its relief.
        /// </summary>
        IEnumerator TerrainTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            var tal = root.moon;
            var f = root.flight;

            var peak = HighSpot(home, 4000, out double ph);
            OreSurvey.LatLon(peak, out double plat, out double plon);
            u.ut = TimeWithSun(home, peak, 0.35, 0);
            // a few km to one side of the peak, at 3.5 km, heading toward it
            var v = Fly(VesselDesign.Stack("Glider probe", "probe.spark", "tank.s1"), home, ph + 1500, plat, plon - 0.25, u.ut, false, 120);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3d toPeak = home.FixedToInertial(peak, f.ut) * (home.radius + ph) - v.pos;
            Vector3d upv = v.pos.normalized;
            Vector3d flat = toPeak - upv * Vector3d.Dot(toPeak, upv);
            f.LookAlong(flat.normalized, 40f, 0f, 9f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("110_arden_mountains");
            Log($"Arden peak {ph:0} m at {plat:0.0}, {plon:0.0}; flying at {v.Altitude:0} m");

            // Tal: a lander on high cratered ground, low sun
            var tspot = HighSpot(tal, 3000, out double th);
            u.ut = TimeWithSun(tal, tspot, 0.3, f.ut);
            var ld = VesselDesign.Stack("Crater lander", "chute.s", "pod.wren", "tank.s2", "eng.wisp");
            DesignOps.AttachSurfaceAt(ld, ld.nodes.FindIndex(n => n.id == "tank.s2"), 0.95f, 45f, "leg.s", 4, false);
            var lander = VesselSim.CreateLanded(ld, tal, tspot, u.ut);
            lander.throttle = 0;
            u.Add(lander);
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(1.5f);
            Vector3d up = lander.pos.normalized;
            Vector3d sunFlat = Sun.DirectionAt(GameRoot.I.universe.ut) - up * Vector3d.Dot(Sun.DirectionAt(GameRoot.I.universe.ut), up);
            f.LookAlong(sunFlat.normalized, 140f, 140f, 22f); // pull back and look down across the land around it
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("111_tal_lander");
            Log($"Tal lander at {th:0} m, radar {lander.RadarAltitude(f.ut):0.00} m");

            // Tal from 30 km, looking down across the craters
            OreSurvey.LatLon(tspot, out double tlat, out double tlon);
            var low = Fly(VesselDesign.Stack("Skimmer", "probe.spark", "tank.s1"), tal, 30_000, tlat, tlon + 1.5, u.ut, true);
            yield return new WaitForSecondsRealtime(1.5f);
            up = low.pos.normalized;
            sunFlat = Sun.DirectionAt(GameRoot.I.universe.ut) - up * Vector3d.Dot(Sun.DirectionAt(GameRoot.I.universe.ut), up);
            f.LookAlong(-sunFlat.normalized, 20f, 0f, 50f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("112_tal_from_30km");

            f.mapView = true;
            Vector3d sIn = Sun.DirectionAt(GameRoot.I.universe.ut);
            f.SetMapView(tal, (float)(tal.radius * 3.3), (float)(Math.Atan2(-sIn.x, -sIn.z) * 180 / Math.PI) + 55f, 10f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("113_tal_map");
            f.mapView = false;
        }
    }
}
