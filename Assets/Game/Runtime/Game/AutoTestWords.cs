using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class FlightMode
    {
        /// <summary>For the screenshot tours: open or close the pause menu.</summary>
        public void SetPaused(bool on) => paused = on;
    }

    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 24: the tidier interface in each wording level (title settings, new game, builder, flight, the guide,
        /// the pause menu), the new Ardlings up close, and a capsule settling shield first under SAS hold.
        /// </summary>
        IEnumerator WordsTour()
        {
            var root = GameRoot.I;
            var was = Words.Level;
            root.universe.career.Start(GameMode.Career);
            root.universe.career.guide = Guide.Climb;
            root.ShowMenu();
            float tb = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - tb < 120f) yield return null;
            Words.Level = WordLevel.Standard;
            root.menu.ShowPage("settings");
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("350_settings_words");
            root.menu.ShowPage("new");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("351_new_game_words");
            root.menu.ShowPage("main");
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("352_builder_standard");
            Words.Level = WordLevel.Easy;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("352b_builder_easy");
            Words.Level = WordLevel.Engineer;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("352c_builder_engineer");

            // Flight: the same moment in all three
            Words.Level = WordLevel.Standard;
            root.Launch(VesselDesign.Default());
            yield return new WaitForSecondsRealtime(1.0f);
            var v = root.flight.active;
            v.ActivateNextStage();
            v.throttle = 1;
            v.sas = SasMode.Stability;
            yield return new WaitForSecondsRealtime(12f);
            foreach (var (l, n) in new[] { (WordLevel.Standard, "353_flight_standard"), (WordLevel.Easy, "353b_flight_easy"), (WordLevel.Engineer, "353c_flight_engineer") })
            {
                Words.Level = l;
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot(n);
            }
            Log($"flight: {v.Altitude / 1000:0.0} km, {v.vel.magnitude:0} m/s");
            Words.Level = WordLevel.Easy;
            root.flight.ShowHowTo(2);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("354_guide_easy");
            Words.Level = WordLevel.Standard;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("354b_guide_standard");
            root.flight.ShowHowTo(-1);
            root.flight.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("355_pause_words");
            root.flight.SetPaused(false);

            // A capsule held by SAS at a bad angle as it meets the air: it lets go and settles shield first
            var u = root.universe;
            var home = root.home;
            Vector3d dir = home.FixedToInertial(CelestialBody.DirectionFromLatLon(-5, 80), u.ut);
            Vector3d east = CelestialBody.EastAt(dir);
            Vector3d pos = dir * (home.radius + 45_000);
            Vector3d vel = home.SurfaceVelocity(pos) + east * 2250 - dir * 180;
            Vector3 flow = vel.normalized.ToFloat();
            var cap = VesselSim.CreateFree(VesselDesign.Stack("Homecoming", "chute.s", "pod.wren", "shield.s"), home, pos, vel,
                Quaternion.LookRotation(Vector3.Cross(flow, dir.ToFloat()), -flow) * Quaternion.Euler(70, 0, 0));
            cap.throttle = 0; cap.hasLaunched = true; cap.sas = SasMode.Stability;
            u.Add(cap);
            root.Fly(cap);
            root.flight.SetCamera(100f, 8f, 26f);
            double worst = 0, late = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 25f && !cap.destroyed)
            {
                yield return null;
                double tilt = Vector3d.Angle(Vector3d.Rotate(cap.rot, new Vector3d(0, 1, 0)), -cap.SurfaceRelVelocity) * 180 / System.Math.PI;
                worst = System.Math.Max(worst, tilt);
                if (Time.realtimeSinceStartup - t0 > 15f) late = System.Math.Max(late, tilt);
            }
            yield return Shot("356_capsule_settled");
            Log($"capsule: started up to {worst:0} deg off the airflow, last 10 s within {late:0} deg, at {cap.Altitude / 1000:0.0} km");

            Words.Level = was;
        }

        /// <summary>Puts a craft hanging still above a spot on a world (planet-fixed direction), for scenery shots.</summary>
        VesselSim Hover(CelestialBody b, Vector3d fixedDir, double alt)
        {
            var root = GameRoot.I;
            double ut = root.universe.ut;
            Vector3d dir = b.FixedToInertial(fixedDir, ut);
            Vector3d pos = dir * (b.radius + alt);
            var v = VesselSim.CreateFree(VesselDesign.Stack("Scout", "probe.spark", "tank.s1", "eng.ember"), b, pos, b.SurfaceVelocity(pos),
                Quaternion.LookRotation(CelestialBody.EastAt(dir).ToFloat(), dir.ToFloat()));
            v.throttle = 0;
            v.hasLaunched = true;
            root.universe.Add(v);
            root.Fly(v);
            return v;
        }

        /// <summary>The highest of many spots on a world where the sun stands between lo and hi (dot with straight up).</summary>
        static Vector3d HighestLit(CelestialBody b, Vector3d sunFixed, double lo, double hi, int seed)
        {
            var rng = new System.Random(seed);
            Vector3d best = sunFixed;
            double bh = double.MinValue;
            for (int i = 0; i < 20000; i++)
            {
                var d = new Vector3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
                if (d.magnitude > 1 || d.magnitude < 0.1) continue;
                d = d.normalized;
                double s = Vector3d.Dot(d, sunFixed);
                if (s < lo || s > hi) continue;
                double h = Terrain.Height(b, d);
                if (h > bh) { bh = h; best = d; }
            }
            return best;
        }

        /// <summary>A spot some way from a point, off to the side of the sun (so the view across is lit from the side).</summary>
        static Vector3d SideOf(CelestialBody b, Vector3d at, Vector3d sunFixed, double metres)
        {
            Vector3d side = Vector3d.Cross(at, sunFixed).normalized;
            return (at - side * (metres / b.radius)).normalized;
        }

        /// <summary>Milestone 26: the new ground on other worlds: Tal's mountain ranges and craters, Ochre's Great Mound.</summary>
        IEnumerator SceneryTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
            float tb = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - tb < 300f) yield return null;
            Log($"planet maps ready after {Time.realtimeSinceStartup - tb:0.0} s ({PlanetBaker.Baked} worlds)");
            root.Launch(VesselDesign.Default(), false, 0);
            yield return new WaitForSecondsRealtime(1f);
            var f = root.flight;
            FlightMode.HideHud = true;

            // Tal: high over the crater fields, low sun so every rim throws a shadow
            var tal = root.moon;
            Vector3d sunT = tal.InertialToFixed(Sun.DirectionFrom(tal.AbsolutePosition(u.ut), u.ut), u.ut);
            Vector3d peak = HighestLit(tal, sunT, 0.18, 0.45, 5);
            Log($"Tal: highest lit spot {Terrain.Height(tal, peak):0} m");
            Vector3d over = SideOf(tal, peak, sunT, 40_000);
            var v = Hover(tal, over, 30_000);
            yield return new WaitForSecondsRealtime(1.5f);
            f.LookAlong(tal.FixedToInertial(peak, u.ut) * (tal.radius + Terrain.Height(tal, peak)) - v.pos, 30f, 0f, 30f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("397_tal_craters");

            // Tal: low across a mountain range toward its highest peak
            Vector3d near = SideOf(tal, peak, sunT, 26_000);
            v = Hover(tal, near, Terrain.Height(tal, near) + 2200);
            yield return new WaitForSecondsRealtime(1.5f);
            f.LookAlong(tal.FixedToInertial(peak, u.ut) * (tal.radius + Terrain.Height(tal, peak)) - v.pos, 24f, 14f, 4f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("398_tal_mountains");

            // Ochre: the Great Mound, a shield volcano 6 km high and 70 km across, from 80 km off
            var ochre = root.bodies.First(b => b.name == "Ochre");
            Vector3d sunO = ochre.InertialToFixed(Sun.DirectionFrom(ochre.AbsolutePosition(u.ut), u.ut), u.ut);
            Vector3d mound = Vector3d.FromFloat(Terrain.GreatMound);
            double sunUp = Vector3d.Dot(mound, sunO);
            Log($"Ochre: sun over the Great Mound {sunUp:0.00}");
            if (sunUp < 0.2)
            {
                // wait for Ochre to turn the mound into the morning sun
                for (int i = 0; i < 400 && Vector3d.Dot(mound, ochre.InertialToFixed(Sun.DirectionFrom(ochre.AbsolutePosition(u.ut), u.ut), u.ut)) < 0.35; i++) u.ut += 120;
                sunO = ochre.InertialToFixed(Sun.DirectionFrom(ochre.AbsolutePosition(u.ut), u.ut), u.ut);
                Log($"Ochre: waited to sun {Vector3d.Dot(mound, sunO):0.00}");
            }
            Vector3d far = SideOf(ochre, mound, sunO, 85_000);
            v = Hover(ochre, far, Terrain.Height(ochre, far) + 7000);
            yield return new WaitForSecondsRealtime(1.5f);
            f.LookAlong(ochre.FixedToInertial(mound, u.ut) * (ochre.radius + 3000) - v.pos, 24f, 12f, 3f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("399_ochre_great_mound");

            // and from above, looking down into its summit caldera
            Vector3d above = SideOf(ochre, mound, sunO, 30_000);
            v = Hover(ochre, above, 26_000);
            yield return new WaitForSecondsRealtime(1.5f);
            f.LookAlong(ochre.FixedToInertial(mound, u.ut) * (ochre.radius + 5500) - v.pos, 24f, 0f, 20f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("399b_ochre_caldera");

            FlightMode.HideHud = false;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
        }

        /// <summary>Milestone 26: the countdown (arms pulling away, steam, liftoff at zero), then the landing guide over the harbour.</summary>
        IEnumerator CountdownTour()
        {
            var root = GameRoot.I;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
            float tb = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - tb < 300f) yield return null;
            FlightMode.ForceCountdown = true;
            root.Launch(VesselDesign.Default(), false, 0);
            yield return new WaitForSecondsRealtime(1.5f);
            root.flight.SetCamera(150f, 10f, 70f);
            bool started = root.flight.StartCountdownNow();
            Log($"countdown started: {started}");
            yield return new WaitForSecondsRealtime(3.5f);
            yield return Shot("393_countdown_t6");
            yield return new WaitForSecondsRealtime(3.5f);
            yield return Shot("394_countdown_steam");
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("395_liftoff");
            Log($"after the countdown: launched {root.flight.active.hasLaunched}, altitude {root.flight.active.Altitude:0} m");
            FlightMode.ForceCountdown = false;
            root.flight.DropOverHarbour(2500, 900);
            root.flight.SetCamera(200f, 20f, 60f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("396_landing_guide");
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
        }

        /// <summary>Milestone 25: launching from the beach pad rolls the rocket out on a crawler, then it stands on that pad.</summary>
        IEnumerator RolloutTour()
        {
            var root = GameRoot.I;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
            float tb = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - tb < 300f) yield return null;
            FlightMode.ForceRollout = true;
            foreach (var (pad, tag) in new[] { (1, "beach"), (0, "main"), (3, "north") })
            {
                root.Launch(VesselDesign.Longshot(), false, pad);
                yield return new WaitForSecondsRealtime(2.6f);
                yield return Shot($"390_rollout_{tag}_leaving");
                yield return new WaitForSecondsRealtime(6.5f);
                yield return Shot($"391_rollout_{tag}_arriving");
                yield return new WaitForSecondsRealtime(7f);
                yield return Shot($"392_on_{tag}_pad");
                var v = root.flight.active;
                double h = v.Altitude;
                Log($"{tag}: {v.name} {(v.landed ? "landed" : "NOT LANDED")} at {h:0.0} m, {Vector3d.Angle(root.home.InertialToFixed(v.pos, root.universe.ut), LaunchSite.PadDirection(root.home, pad)) * root.home.radius:0} m from the pad's middle");
                root.EnterHangar();
                yield return new WaitForSecondsRealtime(1f);
            }
            FlightMode.ForceRollout = false;
        }

        /// <summary>The assembly building with its great door open (milestone 24): the default view, wider, and out of the door.</summary>
        IEnumerator HangarTour()
        {
            var root = GameRoot.I;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("380_builder");
            var views = new (string name, float yaw, float pitch, float dist, float y)[]
            {
                ("381_door_behind_rocket", 0f, 4f, 100f, 22f), ("382_door_wide", 12f, 2f, 155f, 45f), ("383_door_low", -8f, -6f, 60f, 10f),
                ("384_from_the_side", 70f, 6f, 150f, 40f), ("385_busy_hall", 35f, 14f, 120f, 18f),
            };
            foreach (var (name, yaw, pitch, dist, y) in views)
            {
                root.hangar.SetView(yaw, pitch, dist, y);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot(name);
            }
            var small = root.hangar.CurrentHall;
            Log($"hall for {root.currentDesign.name}: {small.half * 2:0} m across, {small.top:0} m high, door {small.doorHalf * 2:0} x {small.doorTop:0} m, towers {small.levels * 8} m");
            // a bigger rocket gets a bigger hall
            var was = root.currentDesign;
            root.currentDesign = VesselDesign.Longshot();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("386_big_rocket");
            var big = root.hangar.CurrentHall;
            Log($"hall for {root.currentDesign.name}: {big.half * 2:0} m across, {big.top:0} m high, door {big.doorHalf * 2:0} x {big.doorTop:0} m, towers {big.levels * 8} m");
            root.hangar.SetView(8f, 3f, 200f, 30f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("387_big_rocket_door");
            root.currentDesign = was;
            // milestone 25: the space centre from the air, its buildings labelled and clickable
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
            root.ShowCentre();
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("388_space_centre");
            Log($"space centre view: centre {root.menu.centre}, in menu {root.inMenu}");
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
        }

        /// <summary>The space center's buildings close up, with the menu hidden (the title screen's camera, moved about).</summary>
        IEnumerator CampusTour()
        {
            var root = GameRoot.I;
            root.ShowMenu();
            float tb = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - tb < 300f) yield return null;   // a fresh bake takes a while
            yield return new WaitForSecondsRealtime(2f);
            root.menu.hideGui = true;
            var shots = new (string name, double[] at)[]
            {
                ("359_peninsula", new double[] { 0, -2000, 2300, 0, 0, 0 }),
                ("359b_peninsula_high", new double[] { 0, -3600, 3600, 0, 250, 0 }),
                ("360_campus_overview", new double[] { 400, -300, 380, -10, 300, 0 }),
                ("360b_campus_west", new double[] { -620, -260, 430, 40, 320, 0 }),
                ("360c_campus_north", new double[] { 120, 980, 420, 20, 200, 0 }),
                ("361_assembly_building", new double[] { 120, 175, 14, 10, 340, 32 }),
                ("362_headquarters", new double[] { -112, 176, 22, -178, 252, 6 }),
                ("362b_hq_plaza", new double[] { -146, 204, 2.4, -178, 248, 9 }),
                ("363_astronaut_complex", new double[] { 205, 255, 9, 150, 320, 7 }),
                ("364_research", new double[] { -1290, 30, 55, -1222, 200, 4 }),
                ("364b_skybridge", new double[] { -1238, 194, 4, -1212, 240, 9 }),
                ("365_airport", new double[] { -200, 300, 60, -330, 520, 12 }),
                ("365b_hangar", new double[] { -430, 455, 7, -320, 460, 14 }),
                ("365c_runways", new double[] { -300, -700, 140, -560, 200, 0 }),
                ("366_pad", new double[] { -42, -58, 9, 0, 15, 18 }),
                ("366b_east_pads", new double[] { 1850, -560, 140, 2120, -300, 15 }),
                ("366c_northwest_pad", new double[] { -1560, 860, 90, -1700, 1040, 20 }),
                ("367_helipads", new double[] { 300, -10, 25, 338, 66, 3 }),
                ("368_tracking_station", new double[] { -122, 1010, 42, -200, 1110, 8 }),
                ("369_rocket_road", new double[] { 26, 70, 14, 0, 330, 24 }),
                ("370_town", new double[] { 120, 470, 60, 380, 680, 0 }),
                ("370b_town_street", new double[] { 240, 548, 6, 520, 562, 3 }),
                ("371_visitor_centre", new double[] { 560, 1060, 40, 660, 1170, 18 }),
                ("372_harbour", new double[] { 2950, 30, 45, 3320, 150, 8 }),
                ("373_industry_solar", new double[] { 520, -360, 120, 850, 120, 0 }),
                ("374_lighthouse", new double[] { 1240, -1490, 22, 1300, -1415, 18 }),
                ("375_test_stand", new double[] { -1100, 440, 30, -1180, 520, 20 }),
                ("376_east_crawlers", new double[] { 1500, 300, 120, 1800, -100, 0 }),
            };
            foreach (var (name, at) in shots)
            {
                root.menu.shot = at;
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot(name);
            }
            // the same place at night (milestone 25: the floodlights, lamps and lit windows)
            root.menu.timeShift = root.home.rotationPeriod * 0.5;
            foreach (var (name, at) in new (string, double[])[] { ("377_night_campus", new double[] { 400, -300, 380, -10, 300, 0 }), ("377b_night_pad", new double[] { -60, -80, 25, 0, 20, 15 }), ("377c_night_town", new double[] { 120, 470, 60, 380, 680, 0 }) })
            {
                root.menu.shot = at;
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot(name);
            }
            root.menu.timeShift = 0;
            root.menu.shot = null;
            root.menu.hideGui = false;
        }
    }
}
