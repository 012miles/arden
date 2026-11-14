using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>Milestone 28: the story's mission control, a launch replayed from the movie cameras, practice tools, a night lit by Arden.</summary>
        IEnumerator ReplayTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
            float tb = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - tb < 300f) yield return null;

            // 1) the story: a briefing on the pad, then the word from mission control once it's off
            FlightMode.ForceStory = true;
            u.career.storyStep = 0; u.career.storyStarted = true; u.career.storyBriefed = false;
            u.career.records &= ~Records.Launch;
            u.ut = TimeWithSun(home, LaunchSite.Direction, 0.55, u.ut);
            root.Launch(VesselDesign.Default(), false, 0);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(2.5f);
            f.SetCamera(160f, 8f, 60f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("420_story_briefing");
            f.Stage();
            active().sas = SasMode.Stability;
            yield return new WaitForSecondsRealtime(3.5f);
            yield return Shot("421_story_done");
            Log($"story: mission {u.career.storyStep} ({Story.Current(u.career)?.title})");
            // climb a while so there's something to replay
            yield return new WaitForSecondsRealtime(18f);
            Log($"recorded {f.recorder.Count} samples, {f.recorder.snaps.Count} ship copies, altitude {f.active.Altitude:0} m");

            // 2) the replay: the tower camera at liftoff, a flyby, the chase camera
            f.StartReplay();
            var r = f.replay;
            Log($"replay started: {r != null}");
            if (r != null)
            {
                r.playing = false;
                r.cam = FlightMode.ReplayCam.Tower;
                r.ut = f.recorder.StartUt + 4;
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Shot("422_replay_tower");
                r.cam = FlightMode.ReplayCam.Flyby;
                r.flybySet = false;
                r.ut = f.recorder.StartUt + 10;
                r.playing = true; r.speed = 0.5f;
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot("423_replay_flyby");
                r.cam = FlightMode.ReplayCam.Chase;
                r.speed = 1f;
                f.SetCamera(140f, 10f, 45f);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("424_replay_chase");
                f.EndReplay();
            }

            // 3) the builder's story screen
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
            root.hangar.ShowStoryScreen(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("425_story_screen");
            root.hangar.ShowStoryScreen(false);
            FlightMode.ForceStory = false;

            // 4) practice tools (sandbox): the panel, then off to orbit Tal, then beside the Fallen Probe
            var oldMode = u.career.mode;
            u.career.mode = GameMode.Sandbox;
            root.Launch(LegLander("Practice", false), false, 0);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            f.showPractice = true;
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("426_practice_tools");
            f.showPractice = false;
            var tal = root.moon;
            f.PracticeOrbit(tal, 40_000);
            f.SetCamera(30f, 25f, 20f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("427_practice_tal_orbit");
            Log($"practice orbit: {f.active.body.name}, {f.active.Situation(f.ut)}, altitude {f.active.Altitude:0} m");
            var probe = Landmarks.On(tal).First(l => l.kind == LandmarkKind.Probe);
            f.PracticeHover(tal, (probe.dir + CelestialBody.EastAt(probe.dir) * (300 / tal.radius)).normalized, 900, "the Fallen Probe");
            f.SetCamera(160f, -5f, 30f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("428_practice_hover");
            Log($"practice hover: {f.active.body.name}, altitude {f.active.RadarAltitude(f.ut):0} m above the ground");
            f.PracticeTimeOfDay(3);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return WaitFor(() => !f.IsWarpingToTarget, 30f, "warp to night");
            Log($"after warping to night: sun height {Vector3d.Dot(f.active.pos.normalized, Sun.DirectionFrom(f.active.AbsolutePosition(f.ut), f.ut)):0.00}");
            u.career.mode = oldMode;

            // 5) a night on Tal lit by a full Arden overhead (Tal between the sun and Arden)
            double full = u.ut, best = 2;
            for (double t = u.ut; t < u.ut + 40 * 86400; t += 1200)
            {
                Vector3d toA = (home.AbsolutePosition(t) - tal.AbsolutePosition(t)).normalized;
                double s = Vector3d.Dot(Sun.DirectionFrom(tal.AbsolutePosition(t), t), toA);
                if (s < best) { best = s; full = t; }
            }
            u.ut = full;
            Vector3d toArden = (home.AbsolutePosition(full) - tal.AbsolutePosition(full)).normalized;
            Vector3d subArden = tal.InertialToFixed(toArden, full).normalized;
            Vector3d spot = (subArden + CelestialBody.EastAt(subArden) * 2.6).normalized;     // Arden low in the sky, so it's in the picture
            var night = VesselSim.CreateLanded(LegLander("Night", true), tal, spot, u.ut);
            night.hasLaunched = true; u.Add(night); root.Fly(night);
            f = root.flight;
            yield return new WaitForSecondsRealtime(2f);
            f.LookAlong(toArden, 22f, 18f, 0f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("429_tal_night_ardenlight");
            Log($"night on Tal: Arden shine {RenderEnv.ShineStrength:0.000} of sunlight, sun height {Vector3d.Dot(night.pos.normalized, Sun.DirectionFrom(night.AbsolutePosition(u.ut), u.ut)):0.00}, exposure {RenderEnv.Exposure:0.00}");
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);

            VesselSim active() => root.flight.active;
        }
    }
}
