using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 11a: the whole Helion system in the map; an escape burn planned from low Arden orbit and the path
        /// it leaves on round the sun; Helion up close; and Arden from orbit at another time of year.
        /// </summary>
        IEnumerator SunTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            var sun = root.bodies.First(b => b.isStar);

            var ship = Fly(VesselDesign.Stack("Wanderer", "probe.spark", "tank.s4", "eng.ember"), home, 100_000, 0, 30, u.ut, true);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.0f);

            // Plan an escape: prograde, enough to leave with about 2 km/s to spare
            double r = ship.pos.magnitude, vc = Math.Sqrt(home.mu / r), vesc = Math.Sqrt(2 * home.mu / r);
            f.RefreshTrajectory();
            f.AddNode(f.ut + 2400, vesc - vc + 600);
            f.RefreshTrajectory();
            f.mapView = true;
            f.SetMapView(home, 2.4e8f, 20f, 60f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("160_escape_plan");
            f.SetMapView(sun, 3.4e10f, 0f, 70f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("161_sun_system");

            // Fly it: the burn as an instant kick, then warp out of Arden's pull
            ship.RailsAdvance(2400, u.ut, out double coast);
            u.ut += coast;
            ship.vel = ship.vel + ship.vel.normalized * (vesc - vc + 600);
            ship.rails = null;
            f.DeleteNode();
            double t0 = f.ut;
            for (int i = 0; i < 300 && ship.body == home; i++)
            {
                ship.RailsAdvance(1800, u.ut, out double used);
                u.ut += used;
                ship.CheckSoi(u.ut);
            }
            Log($"escape: left Arden after {(u.ut - t0) / 3600:0.0} h, now {ship.Situation(u.ut)}");
            root.Fly(ship);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.0f);
            f.mapView = true;
            f.SetMapView(sun, 3.4e10f, 0f, 70f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("162_round_the_sun");
            f.SetMapView(sun, (float)(sun.radius * 3.4), 30f, 15f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("163_helion");
            f.mapView = false;

            // Arden from orbit half a year on: the sun now lights it from the other side of the sky
            u.ut += home.OrbitPeriod / 2;
            var watcher = Fly(VesselDesign.Stack("Season watcher", "probe.spark", "tank.s1"), home, 400_000, 10, 60, u.ut, true);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3d down = -watcher.pos.normalized;
            f.LookAlong(down, 40f, 0f, -25f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("164_half_a_year_on");
            Log($"half a year on: sun at {Math.Asin(Sun.DirectionAt(u.ut).y) * 180 / Math.PI:0.0} deg (was {Math.Asin(Sun.Fixed.y) * 180 / Math.PI:0.0})");
        }
    }
}
