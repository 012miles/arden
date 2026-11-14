using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 11b: the transfer planner on a probe in low Arden orbit, with the planned path to Ochre; Ochre from
        /// orbit; a lander on Ochre under its butterscotch sky; Mote up close; and Ochre's regions in the map.
        /// </summary>
        /// <summary>A small probe with the deep space dish already unfolded, so it has a signal home from Ochre.</summary>
        static VesselDesign WithDish(string name)
        {
            var d = VesselDesign.Stack(name, "probe.spark", "tank.s1");
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s1"), 0.5f, 0f, "dish.d", 1, false);
            return d;
        }

        static void UnfoldDish(VesselSim v) { foreach (var p in v.parts) if (p.def.id == "dish.d") { p.legsDeployed = true; p.legDeploy = 1; } }

        IEnumerator OchreTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            var ochre = root.bodies.First(b => b.name == "Ochre");
            var mote = root.bodies.First(b => b.name == "Mote");
            var sun = root.bodies.First(b => b.isStar);

            // The planner: target Ochre from low Arden orbit
            var ship = Fly(VesselDesign.Stack("Pathfinder", "probe.spark", "tank.s4", "eng.ember"), home, 100_000, 0, 30, u.ut, true);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.0f);
            f.SetTarget(ochre);
            f.mapView = true;
            float t0 = Time.realtimeSinceStartup;
            bool planned = f.PlanTransfer();
            Log($"transfer {(planned ? "planned" : "FAILED")} in {Time.realtimeSinceStartup - t0:0.0}s; node {(f.node != null ? $"{f.node.prograde:0} m/s at day {(f.node.ut - u.ut) / 86400:0.0}" : "none")}");
            f.SetMapView(sun, 3.4e10f, 10f, 70f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("170_transfer_planner");
            f.mapView = false;
            f.DeleteNode();

            // Ochre from 200 km, sunlit
            var spot = CelestialBody.DirectionFromLatLon(10, 40);
            u.ut = TimeWithSun(ochre, spot, 0.6, u.ut);
            var orb = Fly(WithDish("Ochre orbiter"), ochre, 200_000, 10, 20, u.ut, true);
            UnfoldDish(orb);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3d up = orb.pos.normalized;
            Vector3d sunHere = Sun.DirectionFrom(ochre.AbsolutePosition(u.ut), u.ut);
            Vector3d sunFlat = sunHere - up * Vector3d.Dot(sunHere, up);
            f.LookAlong(-sunFlat.normalized, 30f, 0f, 40f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("171_ochre_from_orbit");

            // A lander in a canyon country, low sun, looking along the ground under the sky
            var ground = FindRegionSpot(ochre, "Canyons", d => Vector3d.Dot(ochre.FixedToInertial(d, u.ut), Sun.DirectionFrom(ochre.AbsolutePosition(u.ut), u.ut)) > 0.35);
            var d0 = VesselDesign.Stack("Dust lander", "chute.s", "pod.wren", "tank.s2", "eng.wisp");
            DesignOps.AttachSurfaceAt(d0, d0.nodes.FindIndex(n => n.id == "tank.s2"), 0.95f, 45f, "leg.s", 4, false);
            var lander = VesselSim.CreateLanded(d0, ochre, ground, u.ut);
            lander.throttle = 0;
            lander.hasLaunched = true;
            foreach (var p in lander.parts) p.crew.Clear();
            u.Add(lander);
            root.Fly(lander);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            f.SetCamera(200f, 8f, 26f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("172_ochre_lander");
            Log($"Ochre lander in {Regions.Under(lander, f.ut)}, radar {lander.RadarAltitude(f.ut):0.00} m, {lander.Situation(f.ut)}");

            // Mote from 15 km
            var low = Fly(WithDish("Mote skimmer"), mote, 15_000, 0, 0, u.ut, true);
            UnfoldDish(low);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            up = low.pos.normalized;
            // Along Mote's horizon toward Ochre, hanging in the sky
            Vector3d toOchre = ochre.AbsolutePosition(u.ut) - low.AbsolutePosition(u.ut);
            f.LookAlong(toOchre.normalized, 25f, 0f, -4f); // straight at Ochre, past the probe
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("173_mote");

            // Ochre's regions in the map
            f.mapView = true;
            f.SetMapLayer(false, true);
            f.SetMapView(ochre, (float)(ochre.radius * 3.4), 30f, 20f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("174_ochre_regions");
            f.SetMapLayer(false, false);
            f.mapView = false;
        }
    }
}
