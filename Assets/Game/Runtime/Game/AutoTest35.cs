using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 35: the Easy autopilot flies a Tal Lander from the pad all the way to Tal and lands it. Logs each phase
        /// and takes a picture at each one; fails if it crashes or runs out of time.
        /// </summary>
        IEnumerator TripTour()
        {
            var root = GameRoot.I;
            var was = Words.Level;
            Words.Level = WordLevel.Easy;
            root.Launch(Presets.TalLander());
            yield return new WaitForSecondsRealtime(2f);
            var f = root.flight;
            var tal = root.bodies.First(b => b.name == "Tal");
            f.StartTrip(tal);
            var last = (FlightMode.TripPhase)(-1);
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < 780f)
            {
                var v = f.active;
                if (v == null || v.destroyed) { Log($"TRIP FAIL: destroyed in {last}: {v?.destroyReason}"); break; }
                if (f.trip != last)
                {
                    Log($"trip: {last} -> {f.trip} at {Time.unscaledTime - start:0} s; {v.body.name}, alt {v.Altitude / 1000:0.0} km, status \"{f.tripStatus}\"");
                    last = f.trip;
                    yield return Shot($"720_trip_{(int)last}_{last}");
                }
                if (f.trip == FlightMode.TripPhase.Off) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(3f);   // (milestone 46: landings settle onto their legs for a moment first)
            var lv = f.active;
            bool landed = lv != null && !lv.destroyed && (lv.landed || lv.settling) && lv.body == tal;
            Log(landed ? $"TRIP PASS: landed on Tal after {Time.unscaledTime - start:0} s, {lv.parts.Sum(p => p.propellant):0.0} t of fuel left" : $"TRIP FAIL: ended in {last} on {lv?.body.name}, landed={lv?.landed}, status \"{f.tripStatus}\"");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("729_trip_end");
            Words.Level = was;
        }

        /// <summary>
        /// Milestone 35: the interactive cabin. A capsule on the pad: the desk (live screens, warning lamps, switches, STAGE and
        /// ABORT), then the things to play with (the radio, the snack locker, the blinds, night lights, the plushie, the plant),
        /// round to the hatch, and STAGE from the desk to launch; then a habitat on the ground (the bike, chess, the bunks) and
        /// out through its hatch.
        /// </summary>
        IEnumerator CabinTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            root.Launch(Presets.Orbiter());
            yield return new WaitForSecondsRealtime(2.5f);
            var f = root.flight;
            if (!f.CanGoInside)
            {
                var pod = f.active.parts.FirstOrDefault(p => p.def.crew > 0);
                if (pod != null && pod.crew.Count == 0) pod.crew.Add("Iris Ardling");
            }
            f.EnterInterior();
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"capsule: inside={f.inside}; {f.CabinInfo()}");
            Log($"looking at: {f.TestLookAt("map", 0.8f)}");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("730_cabin_desk");
            Log($"looking at: {f.TestLookAt("stage", 0.55f)}");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("731_cabin_stage_button");
            Log($"looking at: {f.TestLookAt("sas", 0.55f)}");
            var sasWas = f.active.sas;
            f.TestUse("sas");
            yield return new WaitForSecondsRealtime(0.8f);
            Log($"SAS switch: {sasWas} -> {f.active.sas}");
            yield return Shot("732_cabin_switches");
            f.TestUse("lights");
            f.TestUse("gear");
            yield return new WaitForSecondsRealtime(0.5f);
            Log($"lights {f.active.lightsOn}, gear {f.active.gearDown}");
            Log($"looking at: {f.TestLookAt("throttle", 0.7f)}");
            f.TestUse("throttle");
            yield return new WaitForSecondsRealtime(0.8f);
            Log($"throttle lever: {f.active.throttle:0.00}");
            yield return Shot("733_cabin_throttle");
            f.TestUse("throttle");
            Log($"looking at: {f.TestLookAt("radio", 0.7f)}");
            f.TestUse("radio");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("734_cabin_radio");
            Log($"looking at: {f.TestLookAt("locker", 1.1f)}");
            f.TestUse("locker");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("735_cabin_locker");
            f.TestUse("blind*");
            f.TestUse("cabinlights");
            f.TestUse("cabinlights");
            yield return new WaitForSecondsRealtime(1.5f);
            f.TestCabinLook(0f, -12f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("736_cabin_night_blinds");
            Log($"night: {f.CabinInfo()}");
            f.TestUse("cabinlights");
            f.TestUse("blind*");
            Log($"looking at: {f.TestLookAt("plushie", 1.0f)}");
            f.TestUse("plushie");
            for (int i = 0; i < 3; i++) f.TestUse("plant");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("737_cabin_plushie");
            f.TestCabinLook(180f, -6f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("738_cabin_back");
            f.TestCabinLook(-125f, -6f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("739_cabin_left");
            f.TestCabinLook(110f, -6f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("740_cabin_right");
            Log($"looking at: {f.TestLookAt("abort", 0.55f)}");
            f.TestUse("abort");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("741_cabin_abort_cover");
            Log($"after one press of ABORT: destroyed={f.active.destroyed}, stage {f.active.nextStage} (it needs a second press)");
            // milestone 36: the overhead panel (lamp test, a breaker, the keypad, a knob) and the gauge strip
            Log($"looking at: {f.TestLookAt("lamptest", 0.7f)}");
            f.TestUse("lamptest");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("750_overhead_lamp_test");
            f.TestUse("brk_fans");
            f.TestUse("key4"); f.TestUse("key2"); f.TestUse("keyENT");
            f.TestUse("knob_flood"); f.TestUse("knob_flood");
            yield return new WaitForSecondsRealtime(2.8f);
            Log($"looking at: {f.TestLookAt("key5", 0.7f)}");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("751_overhead_panel");
            Log($"panel: {f.CabinInfo()}");
            f.TestUse("brk_fans");
            f.TestUse("knob_flood"); f.TestUse("knob_flood"); f.TestUse("knob_flood");   // (round to 0.3 again)
            Log($"looking at: {f.TestLookAt("masteralarm", 0.9f)}");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("752_gauge_strip");
            f.TestCabinLook(-100f, -15f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("753_stowage_and_velcro");
            f.TestCabinLook(-45f, -5f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("754_bungee_and_patch");
            f.TestCabinLook(0f, -12f);
            // STAGE from the desk: away we go, the screens running
            f.TestLookAt("stage", 0.6f);
            f.TestUse("stage");
            yield return new WaitForSecondsRealtime(16f);
            f.TestCabinLook(0f, 5f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("742_cabin_liftoff");
            Log($"after STAGE: launched={f.active.hasLaunched}, alt {f.active.Altitude:0} m; {f.CabinInfo()}");
            f.LeaveInterior();

            // milestone 36: weightless in orbit (the loose things float), then reentry (the windows glow, the cabin shakes)
            var podDesign = VesselDesign.Stack("Cabin orbit test", "chute.s", "pod.wren", "shield.s");
            double r = home.radius + 120000;
            var odir = CelestialBody.DirectionFromLatLon(0, 10);
            var opos = home.FixedToInertial(odir, u.ut) * r;
            var orbiter = VesselSim.CreateFree(podDesign, home, opos, CelestialBody.EastAt(opos.normalized) * Math.Sqrt(home.mu / r), Quaternion.FromToRotation(Vector3.up, opos.normalized.ToFloat()));
            foreach (var p in orbiter.parts) p.crew.Clear();
            orbiter.parts.First(p => p.def.crew > 0).crew.Add("Iris Ardling");
            orbiter.hasLaunched = true; orbiter.career = u.career;
            u.Add(orbiter);
            root.Fly(orbiter);
            yield return new WaitForSecondsRealtime(1.5f);
            f = root.flight;
            f.EnterInterior();
            yield return new WaitForSecondsRealtime(5f);
            f.TestCabinLook(20f, 8f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("755_zero_g_floating");
            Log($"in orbit: g {orbiter.gForce:0.00}; {f.CabinInfo()}");
            Log($"looking at: {f.TestLookAt("float0", 0.8f)}");
            f.TestUse("float0");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("756_pushed_the_pen");
            f.LeaveInterior();
            double rr = home.radius + 42000;
            var rdir = CelestialBody.DirectionFromLatLon(0, 40);
            var rpos = home.FixedToInertial(rdir, u.ut) * rr;
            var rvel = CelestialBody.EastAt(rpos.normalized) * 3200 - rpos.normalized * 120;
            var faller2 = VesselSim.CreateFree(podDesign, home, rpos, rvel, Quaternion.FromToRotation(Vector3.up, (-rvel.normalized).ToFloat()));
            foreach (var p in faller2.parts) p.crew.Clear();
            faller2.parts.First(p => p.def.crew > 0).crew.Add("Iris Ardling");
            faller2.hasLaunched = true; faller2.career = u.career;
            u.Add(faller2);
            root.Fly(faller2);
            yield return new WaitForSecondsRealtime(0.5f);
            f = root.flight;
            f.EnterInterior();
            yield return new WaitForSecondsRealtime(1.5f);
            f.TestCabinLook(0f, 0f);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Shot("757_reentry_glow");
            Log($"reentry: heat flux {faller2.heatFlux / 1000:0} kW/m2, destroyed {faller2.destroyed}; {f.CabinInfo()}");
            f.LeaveInterior();

            // a habitat on the ground
            var dir = (LaunchSite.Direction + CelestialBody.EastAt(LaunchSite.Direction) * (3000.0 / home.radius)).normalized;
            var hab = VesselSim.CreateLanded(VesselDesign.Stack("Hab test", "hab.s"), home, dir, u.ut);
            foreach (var p in hab.parts) p.crew.Clear();
            var habPart = hab.parts.First(p => p.def.crew > 0);
            habPart.crew.Add("Iris Ardling"); habPart.crew.Add("Otto Ardling");
            hab.hasLaunched = true;
            hab.career = u.career;
            u.Add(hab);
            root.Fly(hab);
            yield return new WaitForSecondsRealtime(2f);
            f = root.flight;
            f.EnterInterior();
            yield return new WaitForSecondsRealtime(1.2f);
            Log($"habitat: inside={f.inside}; {f.CabinInfo()}");
            f.TestCabinLook(0f, -8f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("743_hab_front");
            Log($"looking at: {f.TestLookAt("bike", 1.1f)}");
            f.TestUse("bike");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("744_hab_bike");
            Log($"looking at: {f.TestLookAt("chess", 0.9f)}");
            f.TestUse("chess"); f.TestUse("chess");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("745_hab_chess");
            f.TestCabinLook(180f, -8f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("746_hab_back");
            Log($"looking at: {f.TestLookAt("hatch", 0.8f)}");
            f.TestUse("hatch");
            yield return new WaitForSecondsRealtime(2f);
            Log($"through the hatch: inside={f.inside}, on EVA={f.active?.IsEva}");
            yield return Shot("747_out_of_the_hatch");
            Log("CABIN TOUR DONE");
        }

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

            // the title screen without its Credits button, and the credits rolling at the end of the story
            root.ShowMenu();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("712_title");
            EndCredits.Start(true);
            yield return new WaitForSecondsRealtime(9f);
            yield return Shot("713_end_credits");
            EndCredits.Active = false;

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
