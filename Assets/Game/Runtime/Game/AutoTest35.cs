using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 35 tour: the space centre a game opens at, the green LAUNCH, the tutorials list and the three lessons,
        /// a crash breaking apart, a splashdown bobbing on the sea and an Ardling swimming, and the new Ardling look.
        /// </summary>
        IEnumerator Tour35()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;

            root.ShowCentre();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("700_space_centre");
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.2f);
            Lessons.PickerOpen = true;
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("701_tutorials");
            Lessons.PickerOpen = false;

            // lesson 1 on the pad: the box, and the green LAUNCH
            Lessons.Begin(0);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("702_lesson_orbit");
            Log($"lesson: {Lessons.Current} step {Lessons.StepIndex}");
            // lesson 2: round Tal
            root.EnterHangar();
            Lessons.Begin(1);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("703_lesson_tal");
            root.flight.SetSas(SasMode.Retrograde);
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"lesson tal: step {Lessons.StepIndex} (pointing back should have moved it on)");
            // lesson 3: the station
            root.EnterHangar();
            Lessons.Begin(2);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("704_lesson_dock");
            root.EnterHangar();
            Lessons.Stop();

            // a crash: a rocket dropped onto the ground at speed, breaking apart
            var dir = (LaunchSite.Direction + CelestialBody.EastAt(LaunchSite.Direction) * (2500.0 / home.radius)).normalized;
            double ground = home.radius + LaunchSite.SurfaceHeight(home, dir);
            var faller = VesselSim.CreateFree(VesselDesign.Broadside(), home, home.FixedToInertial(dir, u.ut) * (ground + 60), Vector3d.zero, Quaternion.identity);
            faller.vel = home.SurfaceVelocity(faller.pos) - faller.pos.normalized * 70 + CelestialBody.EastAt(faller.pos.normalized) * 10;
            faller.rot = Quaternion.FromToRotation(Vector3.up, faller.pos.normalized.ToFloat()) * Quaternion.Euler(20, 0, 0);
            faller.hasLaunched = true;
            u.Add(faller);
            root.Fly(faller);
            root.flight.LookAlong(CelestialBody.EastAt(faller.pos.normalized), 70f, 0f, 12f);
            for (int i = 0; i < 60 && !faller.destroyed; i++) yield return new WaitForSecondsRealtime(0.05f);
            Ui.Hidden = true;   // (the end panel would cover the pieces)
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Shot("705_crash_blast");
            Log($"crash at once: {root.flight.ChunkInfo()}");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("706_crash_pieces");
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("707_crash_settled");
            Ui.Hidden = false;
            Log($"crash: destroyed={faller.destroyed} ({faller.destroyReason}); {root.flight.ChunkInfo()}");

            // the sea: a capsule bobbing, then an Ardling swimming
            var sea = CelestialBody.DirectionFromLatLon(5, 30);
            var cap = VesselSim.CreateLanded(VesselDesign.Stack("Splashdown", "chute.s", "pod.wren", "shield.s"), home, sea, u.ut);
            foreach (var p in cap.parts) p.crew.Clear();
            cap.parts.First(p => p.def.crew > 0).crew.Add("Iris Ardling");
            cap.hasLaunched = true;
            cap.career = u.career;
            u.Add(cap);
            root.Fly(cap);
            yield return new WaitForSecondsRealtime(2f);
            Log($"splashdown: in water={cap.InWater}");
            yield return Shot("708_splashdown");
            var swimmer = root.flight.GoEva(cap.parts.First(p => p.crew.Count > 0));
            yield return new WaitForSecondsRealtime(1f);
            root.flight.walkOverride = new Vector2(0, 1);
            yield return new WaitForSecondsRealtime(2.5f);
            if (swimmer != null) root.flight.LookAlong(CelestialBody.EastAt(swimmer.pos.normalized), 7f, 70f, 12f);   // from the side
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("709_swimming");
            root.flight.walkOverride = Vector2.zero;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("710_treading_water");
            root.flight.walkOverride = null;
            Log($"swimmer: in water={swimmer?.InWater}");
            root.EnterHangar();

            // the new look: a smaller helmet, a nicer note
            root.ShowMission();
            MyArdling.Show();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("711_ardling_look");
            MyArdling.Open = false;
            root.LeaveMission();
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}
