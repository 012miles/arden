using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>Milestone 23: the new interface look on the title screen, the How to play guide, Controls, the builder and flight.</summary>
        IEnumerator PolishTour()
        {
            var root = GameRoot.I;
            root.universe.career.Start(GameMode.Career);   // so the badges show their rewards
            root.universe.career.guide = Guide.Closed;
            root.ShowMenu();
            float tb = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - tb < 120f) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("330_title");
            yield return new WaitForSecondsRealtime(20f);
            yield return Shot("330b_title_later");
            root.menu.ShowPage("help");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("331_how_to_play");
            root.menu.ShowPage("controls");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("332_controls");
            // A rebind shows up everywhere: put staging on Enter for a moment
            Keybinds.Set(K.Space, "Enter");
            Log($"rebind: stage key now {In.Name(K.Space)}; clashes {Keybinds.Clashes().Count}");
            root.menu.ShowPage("main");
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("333_builder");
            // The assembly building at work: a wide look round the floor
            root.hangar.SetView(35f, 14f, 120f, 18f);
            yield return new WaitForSecondsRealtime(3.0f);
            yield return Shot("339_hangar_busy");
            root.hangar.SetView(200f, 8f, 70f, 6f);
            yield return new WaitForSecondsRealtime(2.0f);
            yield return Shot("339b_hangar_floor");
            root.Launch(VesselDesign.Default());
            yield return new WaitForSecondsRealtime(2.0f);
            yield return Shot("334_flight_hud");
            // The space center around the pad: from behind the rocket, then from up high
            root.flight.SetCamera(180f, 16f, 150f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("340_space_center");
            root.flight.SetCamera(160f, 3f, 120f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("340b_space_center_low");
            Keybinds.ResetAll();
            Log($"reset: stage key {In.Name(K.Space)}");
            root.flight.ShowHowTo(6);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("335_guide_in_flight");
            root.flight.ShowHowTo(-1);

            // Reentry: a capsule coming in shield-first, then one tumbling sideways (a wider fireball)
            var u = root.universe;
            var home = root.home;
            foreach (bool sideways in new[] { false, true })
            {
                Vector3d dir = home.FixedToInertial(CelestialBody.DirectionFromLatLon(-5, 80), u.ut);
                Vector3d east = CelestialBody.EastAt(dir);
                Vector3d pos = dir * (home.radius + 38_000);
                Vector3d vel = home.SurfaceVelocity(pos) + east * 2300 - dir * 260;
                Vector3 flow = vel.normalized.ToFloat();
                Quaternion rot = sideways ? Quaternion.LookRotation(-flow, dir.ToFloat()) * Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, 90) : Quaternion.LookRotation(dir.ToFloat(), -flow);
                var cap = VesselSim.CreateFree(VesselDesign.Stack(sideways ? "Tumbler" : "Homecoming", "chute.s", "pod.wren", "shield.s"), home, pos, vel, rot);
                cap.throttle = 0; cap.hasLaunched = true; cap.sas = SasMode.Stability;
                u.Add(cap);
                root.Fly(cap);
                yield return new WaitForSecondsRealtime(2.0f);
                root.flight.SetCamera(sideways ? 60f : 100f, 8f, 26f);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(sideways ? "338b_reentry_sideways" : "338_reentry");
                Log($"reentry {(sideways ? "sideways" : "shield first")}: heat flux {cap.heatFlux / 1000:0} kW/m2 at {cap.Altitude / 1000:0.0} km, {cap.SurfaceRelVelocity.magnitude:0} m/s");
            }

            // An aerial look over the space center and the hills around it
            {
                Vector3d over = home.FixedToInertial(LaunchSite.DirectionAt(home, -150, -900), u.ut);
                Vector3d p0 = over * (home.radius + 900);
                var spot = VesselSim.CreateFree(VesselDesign.Stack("Spotter", "pod.wren"), home, p0, home.SurfaceVelocity(p0), Quaternion.LookRotation(over.ToFloat()));
                spot.throttle = 0; spot.hasLaunched = true;
                u.Add(spot);
                root.Fly(spot);
                root.flight.SetCamera(200f, 38f, 60f);
                yield return new WaitForSecondsRealtime(2.0f);
                yield return Shot("341_space_center_air");
                root.flight.SetCamera(20f, 8f, 90f);
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Shot("341b_hills");
            }

            // Scorched metal: a capsule back from a hot reentry, standing on the pad
            root.Launch(VesselDesign.Stack("Scorched", "chute.s", "pod.wren", "shield.s"));
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var p in root.flight.active.parts) p.charred = p.def.kind == PartKind.HeatShield ? 1f : 0.75f;
            root.flight.SetCamera(150f, 10f, 9f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("342_charred");

            // A tank bursts on the pad, and the fire spreads to its neighbours
            root.Launch(VesselDesign.Default());
            yield return new WaitForSecondsRealtime(1.5f);
            var boomer = root.flight.active;
            root.flight.SetCamera(200f, 10f, 70f);
            var tank = boomer.parts.Find(p => p.index == 7) ?? boomer.parts.Find(p => p.def.kind == PartKind.Tank);
            int before = boomer.parts.Count;
            if (tank != null) boomer.Explode(tank, "a test");
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Shot("343_explosion");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("343b_chain");
            Log($"explosion: {before} parts before, {(boomer.destroyed ? "vessel destroyed" : boomer.parts.Count + " left")}");

            // An achievement pops up; then the badges screen in the builder
            Achievements.Award(root.universe.career, "strato");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("336_achievement_popup");
            foreach (var id in new[] { "liftoff", "sound", "space", "orbit", "eva", "moonwalk", "flag", "dock", "crash" }) Achievements.Award(root.universe.career, id);
            root.universe.career.badgeNews.Clear();
            root.EnterHangar();
            root.hangar.ShowBadges(true);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("337_badges");
            Log($"badges: {root.universe.career.badges.Count}");
        }
    }
}
