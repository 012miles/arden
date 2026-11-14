using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>A probe with the Great Dish already unfolded, so it has a signal home from Brume.</summary>
        static VesselDesign WithGreatDish(string name)
        {
            var d = VesselDesign.Stack(name, "probe.spark", "tank.s1");
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s1"), 0.5f, 0f, "dish.x", 1, false);
            return d;
        }

        static void UnfoldAll(VesselSim v) { foreach (var p in v.parts) if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1; } }

        /// <summary>
        /// Milestone 16: Brume and its rings from a high, tilted orbit; the Brume system in the map; a lander in Pyre's
        /// volcano country; one on Rime with Brume in the sky; an aerobraking forecast at Brume; the whole solar system;
        /// and the Long Haul probe in the builder.
        /// </summary>
        IEnumerator GiantTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var brume = root.bodies.First(b => b.name == "Brume");
            var rime = root.bodies.First(b => b.name == "Rime");
            var pyre = root.bodies.First(b => b.name == "Pyre");
            var sun = root.bodies.First(b => b.isStar);

            // Brume from 12,000 km up, 20 degrees above its ring plane, on the sunlit side
            var spot = CelestialBody.DirectionFromLatLon(20, 40);
            u.ut = TimeWithSun(brume, spot, 0.7, u.ut);
            var orb = Fly(WithGreatDish("Brume watcher"), brume, 12_000_000, 20, 40, u.ut, true);
            UnfoldAll(orb);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3d toPlanet = -orb.pos.normalized;
            f.LookAlong(toPlanet, 30f, 0f, 3f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("230_brume_and_rings");
            Log($"watcher: {orb.Situation(f.ut)}, signal {f.LinkText}");

            f.mapView = true;
            f.SetMapView(brume, 5e7f, 20f, 35f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("231_brume_system_map");
            f.mapView = false;

            // Pyre: a lander among the volcanoes, in sunlight
            Func<Vector3d, CelestialBody, bool> lit = (d, b) => Vector3d.Dot(b.FixedToInertial(d, u.ut), Sun.DirectionFrom(b.AbsolutePosition(u.ut), u.ut)) > 0.4;
            var pspot = FindRegionSpot(pyre, "Volcanoes", d => lit(d, pyre));
            var ld = VesselDesign.Stack("Ember Hopper", "probe.spark", "battery.s", "tank.s2", "eng.wisp");
            DesignOps.AttachSurfaceAt(ld, ld.nodes.FindIndex(n => n.id == "tank.s2"), 0.95f, 45f, "leg.s", 4, false);
            DesignOps.AttachSurfaceAt(ld, ld.nodes.FindIndex(n => n.id == "battery.s"), 0.5f, 0f, "dish.x", 1, false);
            var pl = VesselSim.CreateLanded(ld, pyre, pspot, u.ut);
            pl.throttle = 0; pl.hasLaunched = true;
            u.Add(pl);
            root.Fly(pl);
            f = root.flight;
            UnfoldAll(pl);
            yield return new WaitForSecondsRealtime(1.2f);
            f.SetCamera(160f, 10f, 22f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("232_pyre_volcanoes");
            Log($"Pyre lander in {Regions.Under(pl, f.ut)}, {pl.Situation(f.ut)}, ground {Terrain.Height(pyre, pspot):0} m");

            var lspot = FindRegionSpot(pyre, "Lava Fields", d => lit(d, pyre));
            var pl2 = VesselSim.CreateLanded(ld, pyre, lspot, u.ut);
            pl2.throttle = 0; pl2.hasLaunched = true;
            u.Add(pl2);
            root.Fly(pl2);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            f.SetCamera(210f, 6f, 26f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("233_pyre_lava_fields");
            Log($"Pyre lava spot colour {MeshFactory.BodyColor(pyre, lspot.ToFloat())}, with detail {MeshFactory.BodyColor(pyre, lspot.ToFloat(), (float)(pyre.radius / 1800))}, lava {Terrain.LavaFields(pyre, lspot.ToFloat()):0.00}, style {pyre.style}");

            // Rime: on the face that looks at Brume, so the giant hangs in the sky
            Vector3d faceFixed = rime.InertialToFixed((brume.AbsolutePosition(u.ut) - rime.AbsolutePosition(u.ut)).normalized, u.ut);
            // 74 degrees round from the point under Brume: the giant stands about 16 degrees above the horizon
            Vector3d side = Vector3d.Cross(faceFixed, new Vector3d(0, 1, 0)).normalized;
            var rspot = (faceFixed.normalized * 0.276 + side * 0.961).normalized;
            var rl = VesselSim.CreateLanded(ld, rime, rspot, u.ut);
            rl.throttle = 0; rl.hasLaunched = true;
            u.Add(rl);
            root.Fly(rl);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3d upR = rl.pos.normalized;
            Vector3d toBrume = (brume.AbsolutePosition(u.ut) - rime.AbsolutePosition(u.ut)).normalized;
            Vector3d flat = toBrume - upR * Vector3d.Dot(toBrume, upR);
            f.LookAlong(flat.normalized, 24f, 0f, 6f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("234_rime_brume_rising");
            Log($"Rime lander in {Regions.Under(rl, f.ut)}, Brume {Math.Asin(Math.Max(-1, Math.Min(1, Vector3d.Dot(toBrume, upR)))) * 180 / Math.PI:0} deg up");

            // Arriving at Brume: the aerobraking forecast for a pass 205 km up (a light probe brakes hard: higher than a heavy one)
            {
                double rp = brume.radius + 205_000, vinf = 2000, vp = Math.Sqrt(vinf * vinf + 2 * brume.mu / rp);
                var o = Orbit.FromState(new Vector3d(rp, 0, 0), new Vector3d(0, 0, -vp), brume.mu, 0);
                o.StateAt(-6 * 3600, out var r0, out var v0);
                var probe = VesselSim.CreateFree(WithGreatDish("Brume arrival"), brume, r0, v0, Quaternion.LookRotation(v0.normalized.ToFloat()));
                probe.throttle = 0; probe.hasLaunched = true;
                u.Add(probe);
                root.Fly(probe);
                f = root.flight;
                UnfoldAll(probe);
                yield return new WaitForSecondsRealtime(1.5f);
                f.mapView = true;
                f.SetMapView(brume, 2.5e7f, 30f, 70f);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("235_brume_aerocapture");
                Log("forecast: " + (f.AeroForecastText ?? "none"));
                f.mapView = false;
            }

            // The whole system
            f.mapView = true;
            f.SetMapView(sun, 7e10f, 10f, 70f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("236_solar_system");
            f.mapView = false;

            root.currentDesign = VesselDesign.LongHaul();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("237_long_haul_builder");
        }
    }
}
