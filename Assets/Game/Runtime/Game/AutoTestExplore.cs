using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        static VesselDesign LegLander(string name, bool chute)
        {
            var ld = chute ? VesselDesign.Stack(name, "chute.s", "pod.wren", "tank.s2", "eng.wisp") : VesselDesign.Stack(name, "pod.wren", "tank.s2", "eng.wisp");
            DesignOps.AttachSurfaceAt(ld, ld.nodes.FindIndex(n => n.id == "tank.s2"), 0.95f, 45f, "leg.s", 4, false);
            return ld;
        }

        /// <summary>A time when the sun stands at this height over a spot on any world.</summary>
        static double SunOver(CelestialBody b, Vector3d fixedDir, double sunDot, double from)
        {
            double best = from, bd = 9, period = Math.Abs(b.rotationPeriod) > 1 ? Math.Abs(b.rotationPeriod) : 86400;
            double step = Math.Max(60, period / 2000);
            for (double t = from; t < from + period; t += step)
            {
                double d = Math.Abs(Vector3d.Dot(b.FixedToInertial(fixedDir, t), Sun.DirectionFrom(b.AbsolutePosition(t), t)) - sunDot);
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        /// <summary>Stands a lander some way from a landmark on the sun's side and looks past it at the landmark.</summary>
        IEnumerator AtLandmark(string id, double metres, float camDist, float yawOff, float pitch, double sunDot, string shot, bool front = false)
        {
            var root = GameRoot.I;
            var u = root.universe;
            var f = root.flight;
            var l = root.bodies.SelectMany(Landmarks.On).First(x => x.id == id);
            var b = root.bodies.First(x => x.name == l.body);
            u.ut = SunOver(b, l.dir, sunDot, u.ut);
            Vector3d sunF = b.InertialToFixed(Sun.DirectionFrom(b.AbsolutePosition(u.ut), u.ut), u.ut);
            Vector3d side = sunF - l.dir * Vector3d.Dot(sunF, l.dir);
            if (side.sqrMagnitude < 1e-8) side = CelestialBody.EastAt(l.dir);
            side = side.normalized;
            // a little off the sun line, so the light comes across the scene
            Vector3d across = Vector3d.Cross(l.dir, side).normalized;
            Vector3d toward = (side * 0.85 + across * 0.5).normalized;
            if (front) toward = Vector3d.FromFloat(Landmarks.Frame(l) * Vector3.forward).normalized;   // in front of it (the caves' mouths)
            Vector3d spot = (l.dir + toward * (metres / b.radius)).normalized;
            var lander = VesselSim.CreateLanded(LegLander("Explorer", true), b, spot, u.ut);
            lander.throttle = 0;
            lander.hasLaunched = true;
            u.Add(lander);
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(1.6f);
            double h = Terrain.Height(b, l.dir);
            Vector3d target = b.FixedToInertial(l.dir * (b.radius + h + Math.Min(60, metres * 0.04)), u.ut);
            f.LookAlong(target - lander.pos, camDist, yawOff, pitch);
            yield return new WaitForSecondsRealtime(1.4f);
            yield return Shot(shot);
            Log($"{l.name}: {Landmarks.GroundDistance(b, l, b.InertialToFixed(lander.pos, u.ut)):0} m away, found {u.career.visited.Contains(l.id)}");
        }

        /// <summary>Milestone 27: places to discover, weather, engine dust, prints and tracks, photo mode, the docking guide, the map's "?".</summary>
        IEnumerator ExploreTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
            float tb = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - tb < 300f) yield return null;
            root.Launch(VesselDesign.Default(), false, 0);
            yield return new WaitForSecondsRealtime(1f);
            var f = root.flight;
            Weather.On = false;

            // 1) the places
            yield return AtLandmark("tal.probe", 45, 26f, 25f, 14f, 0.45, "400_fallen_probe");
            FlightMode.HideHud = true;
            yield return AtLandmark("ochre.arch", 1300, 40f, 18f, 6f, 0.4, "401_great_arch");
            yield return AtLandmark("rime.crystals", 150, 30f, 20f, 9f, 0.35, "402_crystal_crater");
            yield return AtLandmark("rime.crystals", 150, 30f, 20f, 9f, -0.3, "402b_crystals_at_night");
            yield return AtLandmark("vesk.geysers", 650, 34f, 15f, 10f, 0.35, "403_frost_geysers");
            yield return AtLandmark("pyre.tubes", 230, 30f, 12f, 8f, 0.4, "404_lava_caves", true);
            yield return AtLandmark("arden.stones", 60, 24f, 20f, 12f, 0.5, "405_old_stones");
            yield return AtLandmark("mote.stone", 70, 24f, 22f, 10f, 0.45, "406_balanced_stone");
            yield return AtLandmark("tal.needle", 650, 34f, 18f, 6f, 0.35, "407_the_needle");
            FlightMode.HideHud = false;

            // 2) weather
            Weather.On = true;
            var home = root.home;
            Vector3d east = CelestialBody.EastAt(LaunchSite.Direction);
            Weather.Override = new WeatherNow { rain = 1f, cloud = 1f, windSpeed = 18, windFixed = east * 18 };
            u.ut = TimeWithSun(home, LaunchSite.Direction, 0.5, u.ut);
            root.Launch(VesselDesign.Default(), false, 0);
            yield return new WaitForSecondsRealtime(1.5f);
            f = root.flight;
            FlightMode.ForceCountdown = true;
            bool held = f.StartCountdownNow();
            Log($"wind 18 m/s: countdown asked, held {held}");
            f.SetCamera(150f, 6f, 70f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("408_rain_at_the_pad");
            FlightMode.ForceCountdown = false;

            var ochre = root.bodies.First(b => b.name == "Ochre");
            Vector3d os = CelestialBody.DirectionFromLatLon(-12, 70);
            u.ut = SunOver(ochre, os, 0.55, u.ut);
            Weather.Override = new WeatherNow { dust = 1f, windSpeed = 38, windFixed = CelestialBody.EastAt(os) * 38 };
            var ol = VesselSim.CreateLanded(LegLander("Duster", true), ochre, os, u.ut);
            ol.throttle = 0; ol.hasLaunched = true; u.Add(ol); root.Fly(ol);
            yield return new WaitForSecondsRealtime(2f);
            f.SetCamera(130f, 8f, 26f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("409_ochre_dust_storm");

            Weather.Override = new WeatherNow { dust = 0f, windSpeed = 7, windFixed = CelestialBody.EastAt(os) * 7 };
            for (int i = 0; i < 3; i++) { WeatherFx.SpawnDevilsSoon(); yield return new WaitForSecondsRealtime(0.4f); }
            yield return new WaitForSecondsRealtime(7f);
            f.SetCamera(120f, 4f, 70f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("410_dust_devils");
            Log($"dust devils: {WeatherFx.DevilCount}");
            Weather.Override = null;
            Weather.On = false;

            // 3) engine dust: a hopper firing just off the ground on Tal, then on Ochre
            var tal = root.moon;
            foreach (var (b, dir, shot) in new[] { (tal, CelestialBody.DirectionFromLatLon(4, 30), "411_engine_dust_tal"), (ochre, CelestialBody.DirectionFromLatLon(-15, 75), "411b_engine_dust_ochre") })
            {
                u.ut = SunOver(b, dir, 0.5, u.ut);
                var hop = VesselSim.CreateLanded(LegLander("Hopper", false), b, dir, u.ut);
                hop.hasLaunched = true; u.Add(hop); root.Fly(hop);
                yield return new WaitForSecondsRealtime(1.2f);
                f.Stage();
                hop.throttle = (float)Math.Min(1, 0.9 * hop.mass * b.mu / hop.pos.sqrMagnitude / Math.Max(1, hop.parts.Where(p => p.def.IsEngine).Sum(p => p.def.thrustVac)));
                f.SetCamera(140f, 10f, 34f);
                yield return new WaitForSecondsRealtime(2.2f);
                yield return Shot(shot);
                Log($"{shot}: thrust {hop.thrustNow:0.0}, radar {hop.RadarAltitude(u.ut):0.0} m");
                hop.throttle = 0;
            }

            // 4) boot prints: an Ardling walks a loop on Tal
            Vector3d ws = CelestialBody.DirectionFromLatLon(6, 34);
            u.ut = SunOver(tal, ws, 0.35, u.ut);
            var pl = VesselSim.CreateLanded(LegLander("Walker", true), tal, ws, u.ut);
            pl.hasLaunched = true; u.Add(pl); root.Fly(pl);
            yield return new WaitForSecondsRealtime(1.4f);
            var walker = f.GoEva(pl.parts.First(p => p.def.id == "pod.wren"));
            yield return new WaitForSecondsRealtime(1.2f);
            if (walker != null)
            {
                f.walkOverride = new Vector2(0.25f, 1);
                yield return new WaitForSecondsRealtime(9f);
                f.walkOverride = null;
                f.SetCamera(200f, 50f, 9f);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("412_boot_prints");
                Log($"prints: {GroundFx.Count} marks");
            }

            // 5) tyre tracks: a rover drives on Tal
            Vector3d rs = CelestialBody.DirectionFromLatLon(8, 36);
            var rover = VesselSim.CreateLanded(VesselDesign.Rover(), tal, rs, u.ut);
            rover.hasLaunched = true; u.Add(rover); root.Fly(rover);
            yield return new WaitForSecondsRealtime(1f);
            rover.brakesOn = false;
            f.driveOverride = new Vector2(0.15f, 1);
            yield return new WaitForSecondsRealtime(8f);
            f.driveOverride = null;
            rover.brakesOn = true;
            f.SetCamera(200f, 35f, 16f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("413_rover_tracks");
            Log($"tracks: {GroundFx.Count} marks, rover driven {rover.driven:0} m");

            // 6) photo mode
            f.TogglePhoto();
            f.SetPhoto(5, 0f, 50f, 0f);
            f.SetCamera(160f, 12f, 14f);
            yield return new WaitForSecondsRealtime(1.2f);
            var ca = UnityEngine.Rendering.VolumeManager.instance.stack.GetComponent<UnityEngine.Rendering.Universal.ColorAdjustments>();
            Log($"photo look in the post stack: saturation {ca.saturation.value:0}, contrast {ca.contrast.value:0}, filter {ca.colorFilter.value}");
            yield return Shot("414_photo_mode");
            f.TogglePhoto();

            // 7) the docking guide: a ferry 25 m off a side port, a little off the line and turned, closing a bit fast
            u.ut = TimeWithSun(home, CelestialBody.DirectionFromLatLon(0, 60), 0.6, u.ut);
            var sd = VesselDesign.Stack("Side dock", "probe.spark", "tank.s4", "eng.ember");
            DesignOps.AttachSurfaceAt(sd, sd.nodes.FindIndex(n => n.id == "tank.s4"), 0.5f, 0f, "port.sm", 1, false);
            var ferry = Fly(VesselDesign.FerryCraft("Ferry"), home, 300_000, 0, 60, u.ut, true);
            f = root.flight;
            yield return new WaitForSecondsRealtime(0.6f);
            var dock = VesselSim.CreateFree(sd, home, ferry.pos + ferry.vel.normalized * 40, ferry.vel, ferry.rot);
            dock.throttle = 0; dock.hasLaunched = true; dock.sas = SasMode.Stability;
            u.Add(dock);
            var sp = dock.parts.Find(p => p.def.kind == PartKind.DockingPort);
            dock.PortFaceWorld(sp, out var face, out var nrm);
            Vector3d upD = ferry.pos.normalized;
            Vector3d sideD = Vector3d.Cross(nrm, upD).normalized;
            ferry.rot = Quaternion.AngleAxis(9f, upD.ToFloat()) * Quaternion.FromToRotation(Vector3.up, (-nrm).ToFloat());
            var fp = ferry.parts.Find(p => p.def.kind == PartKind.DockingPort);
            ferry.PortFaceWorld(fp, out var ff, out _);
            ferry.pos += (face + nrm * 25.0 + sideD * 2.2) - ff;
            ferry.vel = dock.vel - nrm * 3.2;
            ferry.angVel = Vector3.zero;
            ferry.rails = null;
            f.SetTarget(dock);
            f.LookAlong(-nrm + upD * 0.3, 24f, 25f, 10f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("415_docking_guide");
            Log($"docking guide: {f.dockDist:0.0} m, off line {f.dockLateral:0.00} m, {f.dockAngle:0} deg, closing {f.dockClosing:0.00} m/s: \"{f.dockHint}\"");

            // 8) the map: spotted places on Tal as "?", a found one by name
            foreach (var l in Landmarks.On(tal)) if (!u.career.spotted.Contains(l.id)) u.career.spotted.Add(l.id);
            var tl = VesselSim.CreateFree(VesselDesign.Stack("Mapper", "probe.spark", "tank.s1", "eng.ember"), tal, CelestialBody.DirectionFromLatLon(0, 90) * (tal.radius + 60_000), Vector3d.zero, Quaternion.identity);
            u.Add(tl); root.Fly(tl);
            yield return new WaitForSecondsRealtime(1f);
            f.mapView = true;
            var probe = Landmarks.On(tal).First(x => x.kind == LandmarkKind.Probe);
            Vector3d look = tal.FixedToInertial(probe.dir, u.ut);
            f.SetMapView(tal, (float)(tal.radius * 3.0), Mathf.Atan2((float)look.x, (float)look.z) * Mathf.Rad2Deg + 180f, 25f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("416_map_landmarks");
            f.mapView = false;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
        }
    }
}
