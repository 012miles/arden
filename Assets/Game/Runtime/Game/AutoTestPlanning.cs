using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 15: a tug in a low orbit targets a station in a higher, tilted one; plans an intercept; the events list;
        /// warps to the burn, makes it, then plans the speed match at the closest approach. Then an intercept with a wanderer.
        /// </summary>
        IEnumerator PlanningTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            u.ut = TimeWithSun(home, CelestialBody.DirectionFromLatLon(0, 30), 0.6, 0);
            // A station 600 km up, tilted 3 degrees, 120 degrees ahead of the tug
            double r1 = home.radius + 600_000, v1 = Math.Sqrt(home.mu / r1);
            var tug = Fly(VesselDesign.WranglerTug(), home, 300_000, 0, 30, u.ut, true);
            var f = root.flight;
            Vector3d up = tug.pos.normalized, along = tug.vel.normalized, n = Vector3d.Cross(up, along).normalized;
            var tilt = Quaternion.AngleAxis(3f, up.ToFloat());
            Vector3d sp = Vector3d.Rotate(Quaternion.AngleAxis(120f, n.ToFloat()), up) * r1;   // 120 degrees ahead
            Vector3d sv = Vector3d.Cross(n, sp.normalized).normalized * v1;                      // going the same way
            sp = Vector3d.Rotate(tilt, sp); sv = Vector3d.Rotate(tilt, sv);
            var station = VesselSim.CreateFree(VesselDesign.Outpost("Outpost"), home, sp, sv, Quaternion.identity);
            station.throttle = 0; station.hasLaunched = true;
            u.Add(station);
            var rock = Wanderers.Spawn(home, u.ut, new System.Random(11), 1, 0);
            u.Add(rock);
            yield return new WaitForSecondsRealtime(0.8f);
            f.SetTarget(station);
            f.mapView = true;
            f.SetMapView(home, 5e6f, 20f, 65f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("220_map_target");

            bool planned = f.PlanIntercept();
            yield return new WaitForSecondsRealtime(1.2f);
            Log($"intercept planned: {planned}, node {f.node?.DeltaV:0} m/s in {Ui.Time((f.node?.ut ?? u.ut) - u.ut)}");
            yield return Shot("221_intercept_plan");

            f.mapView = false;
            f.ShowEvents(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("222_events");
            f.ShowEvents(false);

            // Warp to the burn and make it in one go (the autotest has no pilot)
            if (f.node != null && f.node.valid)
            {
                double at = f.node.ut;
                f.WarpTo(at - 0.5, "the intercept burn");
                yield return WaitFor(() => !f.WarpingToTarget, 40, "the warp to the burn");
                Log($"warped: {at - u.ut:0.0} s before the node");
                yield return new WaitForSecondsRealtime(0.3f);
                f.active.vel += f.node.dvWorld;
                f.active.rails = null;
                f.DeleteNode();
                f.RefreshTrajectory();
            }
            f.mapView = true;
            f.SetMapView(home, 5e6f, 20f, 65f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("223_on_the_way");
            bool matched = f.PlanMatch();
            yield return new WaitForSecondsRealtime(1.0f);
            Log($"match planned: {matched}, node {f.node?.DeltaV:0} m/s in {Ui.Time((f.node?.ut ?? u.ut) - u.ut)}");
            yield return Shot("224_match_speed");

            // A wanderer: meet it near its closest pass
            f.DeleteNode();
            f.SetTarget(rock);
            f.SetMapView(home, 1.0e8f, 20f, 60f);
            yield return new WaitForSecondsRealtime(0.6f);
            bool wPlan = f.PlanIntercept();
            yield return new WaitForSecondsRealtime(1.2f);
            Log($"wanderer intercept: {wPlan}, node {f.node?.DeltaV:0} m/s in {Ui.Time((f.node?.ut ?? u.ut) - u.ut)}");
            f.SetMapView(home, 1.2e7f, 20f, 60f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("225_wanderer_intercept");
            f.mapView = false;
            f.ShowEvents(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("226_events_wanderer");
        }
    }
}
