using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 9: Vesk from 40 km; a lander with an astronaut and flag on Vesk's ice; the transfer from low Arden
        /// orbit planned in the map; the whole system zoomed out; quicksave and quickload; and two views of Tal's map
        /// globe from different sides (to see whether its dark line turns with Tal or stays with the view).
        /// </summary>
        IEnumerator VeskTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var c = u.career;
            var home = root.home;
            var tal = root.moon;
            var vesk = root.bodies.First(b => b.name == "Vesk");

            // Vesk from 40 km, low sun across the ice ridges
            var spot = HighSpot(vesk, 3000, out double vh);
            OreSurvey.LatLon(spot, out double la, out double lo);
            u.ut = TimeWithSun(vesk, spot, 0.3, 0);
            var probe = Fly(VesselDesign.Stack("Vesk survey probe", "probe.spark", "tank.s1"), vesk, 40_000, la, lo + 3, u.ut, true);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            Vector3d up = probe.pos.normalized;
            Vector3d sunFlat = Sun.DirectionAt(GameRoot.I.universe.ut) - up * Vector3d.Dot(Sun.DirectionAt(GameRoot.I.universe.ut), up);
            f.LookAlong(-sunFlat.normalized, 20f, 0f, 50f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("130_vesk_from_40km");
            Log($"Vesk probe at {probe.Altitude / 1000:0.0} km: {probe.Situation(f.ut)}");

            // A crewed lander on the ice, an astronaut out with a flag
            var pilot = new CrewMember { name = "Ilse Varga", role = CrewRole.Pilot, log = Crew.XpMask, state = 1 };
            c.roster.Add(pilot);
            var d = VesselDesign.Stack("Skua lander", "chute.s", "pod.wren", "tank.s2", "eng.wisp");
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s2"), 0.95f, 45f, "leg.s", 4, false);
            var lspot = HighSpot(vesk, 400, out double lh);
            u.ut = TimeWithSun(vesk, lspot, 0.45, u.ut);
            var lander = VesselSim.CreateLanded(d, vesk, lspot, u.ut);
            foreach (var p in lander.parts) p.crew.Clear();
            var pod = lander.parts.First(p => p.def.id == "pod.wren");
            pod.crew.Add(pilot.name);
            pilot.vesselId = lander.id;
            lander.throttle = 0;
            lander.hasLaunched = true;
            lander.RecomputeMass();
            u.Add(lander);
            pilot.vesselId = lander.id;
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(1.2f);
            f.SetCamera(160f, 12f, 22f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("131a_vesk_lander");
            var eva = f.GoEva(pod);
            yield return new WaitForSecondsRealtime(0.5f);
            if (eva != null)
            {
                f.walkOverride = new Vector2(0, 1);
                yield return new WaitForSecondsRealtime(1.6f);
                f.walkOverride = null;
                f.PlantFlag();
                yield return new WaitForSecondsRealtime(0.4f);
                up = eva.pos.normalized;
                sunFlat = Sun.DirectionAt(GameRoot.I.universe.ut) - up * Vector3d.Dot(Sun.DirectionAt(GameRoot.I.universe.ut), up);
                Vector3d toLander = lander.pos - eva.pos;
                f.LookAlong(toLander - up * Vector3d.Dot(toLander, up), 10f, -40f, 14f);
                yield return new WaitForSecondsRealtime(0.6f);
            }
            yield return Shot("131_vesk_flag");
            f.SetCamera(200f, -50f, 9f); // asks for a view from under the ice: the camera should stay above it
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("131b_low_camera");
            Log($"low camera: asked for -50 deg, got {f.CamPitch:0} deg");
            Log($"Vesk lander at {lh:0} m, radar {lander.RadarAltitude(f.ut):0.00} m; EVA {(eva != null ? eva.name + " landed=" + eva.landed : "failed")}; records {u.career.records}");

            // The transfer: from 100 km over Arden, planned in the map
            var ship = Fly(VesselDesign.Stack("Vesk transfer stage", "probe.spark", "tank.s4", "eng.ember"), home, 100_000, 0, 30, u.ut, true);
            yield return new WaitForSecondsRealtime(1.0f);
            float t0 = Time.realtimeSinceStartup;
            bool found = Patcher.FindTransfer(home, ship.pos, ship.vel, f.ut, vesk, 50_000, out double burnUt, out double dv, out double pe);
            Log($"Vesk transfer {(found ? "found" : "FAILED")}: burn in {burnUt - f.ut:0} s, {dv:0} m/s, Vesk periapsis {pe / 1000:0} km ({Time.realtimeSinceStartup - t0:0.0}s)");
            if (found)
            {
                f.RefreshTrajectory();
                f.AddNode(burnUt, dv);
                f.SetTarget(vesk);
                f.RefreshTrajectory();
                f.mapView = true;
                f.SetMapView(home, 6.5e7f, 0f, 75f);
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Shot("132_vesk_transfer_plan");
            }
            f.mapView = true;
            f.SetMapView(home, 1.0e8f, 30f, 35f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("133_system_map");
            f.mapView = false;

            // Quicksave, change things, quickload
            yield return new WaitForSecondsRealtime(0.4f);
            f.QuickSave();
            int savedCount = u.vessels.Count;
            double savedUt = f.ut;
            string savedName = f.active.name;
            u.Remove(probe);
            u.ut += 3600;
            bool loaded = f.QuickLoad();
            yield return new WaitForSecondsRealtime(1.0f);
            Log($"quickload {(loaded ? "ok" : "FAILED")}: {u.vessels.Count}/{savedCount} vessels, ut {f.ut:0} vs {savedUt:0}, flying {f.active?.name} (saved {savedName})");
            f.Paused = true;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("134_pause_quicksave");
            f.Paused = false;

            // Tal's globe in the map from two sides
            f.mapView = true;
            f.SetMapView(tal, (float)(tal.radius * 3.3), 20f, 10f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("135_tal_map_a");
            f.SetMapView(tal, (float)(tal.radius * 3.3), 110f, 10f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("136_tal_map_b");
            f.SetMapView(vesk, (float)(vesk.radius * 3.3), 60f, 15f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("137_vesk_map");
            f.mapView = false;

            // Mission control in a career that has reached Vesk: Vesk contracts, three active, and the records list
            c.Start(GameMode.Career);
            foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id);
            c.records |= Records.Launch | Records.Space | Records.Orbit | Records.TalFlyby | Records.TalOrbit | Records.TalLanding | Records.VeskFlyby;
            c.RefreshOffers(u.ut);
            foreach (var k in c.Offered.Take(3).ToList()) c.Accept(k, u.ut);
            c.RefreshOffers(u.ut);
            root.EnterHangar();
            root.hangar.ShowMissions(true);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("138_mission_control");
            Log($"career: {c.Offered.Count()} offered ({string.Join(", ", c.Offered.Select(k => k.title))}), {c.Active.Count()} active");
        }
    }
}
