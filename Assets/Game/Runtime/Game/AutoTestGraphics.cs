using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 20: the new look. The builder; the pad by day; liftoff with exhaust and shadows; climbing at 12 km;
        /// sunset on the pad; Arden from orbit by day and at the day-night line; Tal's dusty ground; Ochre's thin
        /// butterscotch air from orbit; Brume; the map.
        /// </summary>
        IEnumerator GraphicsTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;

            // Wait for the worlds' sharp maps (baked in the background since the game started)
            float t0 = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - t0 < 120f) yield return null;
            Log($"maps baked: {PlanetBaker.Baked} worlds after {Time.realtimeSinceStartup:0.0} s");

            var d = VesselDesign.Broadside();   // boosters, so the launch has thick smoke
            root.currentDesign = d;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("280_builder");

            // The pad by day
            u.ut = TimeWithSun(home, LaunchSite.Direction, 0.55, 0);
            root.Launch(d);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            f.SetCamera(215f, 6f, 34f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("281_pad_day");

            // Liftoff
            f.Stage();
            yield return new WaitForSecondsRealtime(2.6f);
            f.SetCamera(200f, -2f, 46f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("282_liftoff");
            int puffs = 0, emitters = 0;
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                emitters++; puffs += ps.particleCount;
                var arr = new ParticleSystem.Particle[ps.particleCount];
                int n = ps.GetParticles(arr);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (n > 0)
                {
                    Vector3 w = Smoke.Anchor.TransformPoint(arr[n / 2].position);
                    Log($"smoke: puff {n / 2} local {arr[n / 2].position} world {w} size {arr[n / 2].GetCurrentSize(ps):0.0} colour {arr[n / 2].GetCurrentColor(ps)}; emitter at {ps.transform.position}; visible {r.isVisible} bounds {r.bounds}; mat {r.sharedMaterial?.shader?.name}");
                }
            }
            Log($"liftoff: altitude {f.active.Altitude:0} m, exposure {RenderEnv.Exposure:0.00}, smoke emitters {emitters} with {puffs} puffs");

            yield return new WaitForSecondsRealtime(1.5f);
            f.SetCamera(190f, 30f, 120f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("282b_liftoff_smoke");

            // Climbing through 12 km, looking across the horizon
            double t2 = TimeWithSun(home, CelestialBody.DirectionFromLatLon(5, 30), 0.5, u.ut);
            u.ut = t2;
            var climber = Fly(VesselDesign.Default(), home, 12_000, 5, 30, u.ut, false, 500);
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3d up = climber.pos.normalized;
            Vector3d sunNow = Sun.DirectionAt(u.ut);
            Vector3d sunFlat = sunNow - up * Vector3d.Dot(sunNow, up);
            f.LookAlong(-sunFlat.normalized, 45f, 35f, 4f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("283_climb_12km");

            // Low over Arden's hills and mountains: the close-up ground photos
            double tl = TimeWithSun(home, CelestialBody.DirectionFromLatLon(20, 60), 0.6, u.ut);
            u.ut = tl;
            var low = Fly(VesselDesign.Default(), home, 2600, 20, 60, u.ut, false, 160);
            yield return new WaitForSecondsRealtime(1.5f);
            up = low.pos.normalized;
            sunNow = Sun.DirectionAt(u.ut);
            sunFlat = sunNow - up * Vector3d.Dot(sunNow, up);
            f.LookAlong(-sunFlat.normalized, 30f, 50f, -8f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("283b_low_hills");

            // Sunset at the pad
            u.ut = TimeWithSun(home, LaunchSite.Direction, 0.03, u.ut);
            root.Launch(d);
            yield return new WaitForSecondsRealtime(1.5f);
            up = f.active.pos.normalized;
            sunNow = Sun.DirectionAt(u.ut);
            sunFlat = sunNow - up * Vector3d.Dot(sunNow, up);
            f.LookAlong(sunFlat.normalized, 60f, 25f, 4f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("284_pad_sunset");
            Log($"sunset: sun light {root.sun.color}, exposure {RenderEnv.Exposure:0.00}");

            // Orbit by day, looking down past the craft at Arden and its limb
            double t3 = TimeWithSun(home, CelestialBody.DirectionFromLatLon(0, 60), 0.75, u.ut);
            u.ut = t3;
            var orbiter = Fly(VesselDesign.Stack("Lookout", "probe.spark", "tank.s1", "eng.ember"), home, 180_000, 0, 60, u.ut, true);
            yield return new WaitForSecondsRealtime(1.2f);
            f.SetCamera(30f, 22f, 9f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("285_orbit_day");

            // The day-night line, looking along the limb toward the sun
            double t4 = TimeWithSun(home, CelestialBody.DirectionFromLatLon(0, 90), 0.04, u.ut);
            u.ut = t4;
            orbiter = Fly(VesselDesign.Stack("Lookout", "probe.spark", "tank.s1", "eng.ember"), home, 220_000, 0, 90, u.ut, true);
            yield return new WaitForSecondsRealtime(1.2f);
            up = orbiter.pos.normalized;
            sunNow = Sun.DirectionAt(u.ut);
            sunFlat = sunNow - up * Vector3d.Dot(sunNow, up);
            f.LookAlong(sunFlat.normalized, 14f, 25f, 30f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("286_orbit_terminator");

            // Tal's dusty ground by day
            var tal = root.moon;
            Vector3d day = Sun.DirectionAt(u.ut);
            Vector3d spot = (tal.InertialToFixed(day, u.ut) + new Vector3d(0.6, 0.2, 0)).normalized;
            var ld = VesselDesign.Stack("Lander", "chute.s", "pod.wren", "tank.s2", "eng.wisp");
            DesignOps.AttachSurfaceAt(ld, ld.nodes.FindIndex(n => n.id == "tank.s2"), 0.95f, 45f, "leg.s", 4, false);
            var lander = VesselSim.CreateLanded(ld, tal, spot, u.ut);
            lander.throttle = 0;
            u.Add(lander);
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(1.4f);
            f.SetCamera(140f, 12f, 14f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("287_tal_surface");

            // An Ardling steps out
            var walker = f.GoEva(lander.parts.First(p => p.def.id == "pod.wren"));
            yield return new WaitForSecondsRealtime(1.5f);
            if (walker != null)
            {
                Vector3 facing = walker.rot * Vector3.forward;
                f.LookAlong(Vector3d.FromFloat(-facing), 3.0f, 20f, 4f);   // from in front, a little to one side
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("287b_ardling");
                f.walkOverride = new Vector2(0, 1);      // a few steps, to see the walk
                yield return new WaitForSecondsRealtime(0.9f);
                facing = walker.rot * Vector3.forward;
                Vector3 side = Vector3.Cross(walker.pos.normalized.ToFloat(), facing);
                f.LookAlong(Vector3d.FromFloat(-side), 3.2f, 0f, 4f);
                yield return new WaitForSecondsRealtime(0.25f);
                yield return Shot("287c_ardling_walk");
                f.walkOverride = null;
                Log($"ardling: {walker.parts[0].crew.FirstOrDefault()} look {Ardlings.LookFor(walker.parts[0].crew.FirstOrDefault()).face}");
            }
            else Log("no ardling: GoEva failed");

            // Ochre from low orbit
            var ochre = root.bodies.First(b => b.name == "Ochre");
            var oc = Fly(VesselDesign.Stack("Scout", "probe.spark", "tank.s1"), ochre, 90_000, 10, 0, u.ut, true);
            yield return new WaitForSecondsRealtime(1.2f);
            up = oc.pos.normalized;
            sunNow = Sun.DirectionAt(u.ut);
            sunFlat = sunNow - up * Vector3d.Dot(sunNow, up);
            f.LookAlong(sunFlat.normalized, 12f, 60f, 20f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("288_ochre_orbit");

            // Ochre's ground, landed in the dunes by day
            Vector3d oday = Sun.DirectionFrom(ochre.AbsolutePosition(u.ut), u.ut);   // the sun as seen from Ochre
            Vector3d ospot = (ochre.InertialToFixed(oday, u.ut) + new Vector3d(0.3, 0.25, 0.2)).normalized;
            var olander = VesselSim.CreateLanded(ld, ochre, ospot, u.ut);
            olander.throttle = 0;
            u.Add(olander);
            root.Fly(olander);
            yield return new WaitForSecondsRealtime(1.4f);
            f.SetCamera(120f, 8f, 16f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("288b_ochre_surface");

            // Brume from high orbit
            var brume = root.bodies.First(b => b.name == "Brume");
            var bc = Fly(VesselDesign.Stack("Scout", "probe.spark", "tank.s1"), brume, 2_500_000, 15, 0, u.ut, true);
            yield return new WaitForSecondsRealtime(1.2f);
            up = bc.pos.normalized;
            f.LookAlong(-up, 30f, 40f, 0f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("289_brume");

            // The map
            root.Fly(orbiter);
            yield return new WaitForSecondsRealtime(1.0f);
            f.mapView = true;
            f.SetMapView(home, (float)(home.radius * 3.6), -15f, -10f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("290_map");
            f.mapView = false;
            Log("graphics tour done");
        }
    }
}
