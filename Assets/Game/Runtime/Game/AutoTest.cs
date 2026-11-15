using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Run the game with "-autotest [-autotestOut dir]": opens the builder, launches the starter rocket
    /// on autopilot, takes screenshots along the way, writes a telemetry log, then quits.
    /// </summary>
    public partial class AutoTest : MonoBehaviour
    {
        string outDir;
        readonly StringBuilder log = new StringBuilder();
        float start;

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        void Log(string s)
        {
            string line = $"[{Time.realtimeSinceStartup - start,6:0.0}s] {s}";
            log.AppendLine(line);
            Debug.Log("AUTOTEST " + line);
        }

        IEnumerator Shot(string name)
        {
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
            yield return null;
            yield return null;
            Log("screenshot " + name);
        }

        IEnumerator WaitFor(Func<bool> cond, float timeout, string what)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond())
            {
                if (Time.realtimeSinceStartup - t0 > timeout) { Log("TIMEOUT waiting for " + what); yield break; }
                yield return null;
            }
        }

        IEnumerator Start()
        {
            start = Time.realtimeSinceStartup;
            outDir = Arg("-autotestOut") ?? Path.Combine(Application.persistentDataPath, "autotest");
            Directory.CreateDirectory(outDir);
            var root = GameRoot.I;
            Log($"screen {Screen.width}x{Screen.height}");
            Weather.On = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestExplore") >= 0;   // milestone 27: weather only in its own tour
            yield return new WaitForSecondsRealtime(2f);

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestParts") >= 0)
            {
                yield return PartsTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestCoopHost") >= 0)
            {
                yield return CoopHostTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestMatch31") >= 0)
            {
                yield return Match31Tour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            foreach (var (flag, tour) in new (string, Func<IEnumerator>)[] { ("-autotestExpedition", ExpeditionTour), ("-autotest35", Tour35), ("-autotestTrip", TripTour), ("-autotestCabin", CabinTour) })
            {
                if (Array.IndexOf(Environment.GetCommandLineArgs(), flag) < 0) continue;
                yield return tour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestBuilder32") >= 0)
            {
                yield return Builder32Tour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestRival") >= 0)
            {
                yield return RivalTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestMenus30") >= 0)
            {
                yield return Menus30Tour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestCoopJoin") >= 0)
            {
                yield return CoopJoinTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestMission") >= 0)
            {
                yield return MissionTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestMissionHost") >= 0)
            {
                yield return MissionHostTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestMissionJoin") >= 0)
            {
                yield return MissionJoinTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestReplay") >= 0)
            {
                yield return ReplayTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestExplore") >= 0)
            {
                yield return ExploreTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestScenery") >= 0)
            {
                yield return SceneryTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestCountdown") >= 0)
            {
                yield return CountdownTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestRollout") >= 0)
            {
                yield return RolloutTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestHangar") >= 0)
            {
                yield return HangarTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestCampus") >= 0)
            {
                yield return CampusTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestWords") >= 0)
            {
                yield return WordsTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestPerf") >= 0)
            {
                yield return PerfTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestPolish") >= 0)
            {
                yield return PolishTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestCrewPhoto") >= 0)
            {
                yield return CrewPhotoTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestBases") >= 0)
            {
                yield return BasesTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestSound") >= 0)
            {
                yield return SoundTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestGraphics") >= 0)
            {
                yield return GraphicsTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestSupplies") >= 0)
            {
                yield return SuppliesTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestFixes") >= 0)
            {
                yield return FixesTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestMenu") >= 0)
            {
                yield return MenuTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestComputer") >= 0)
            {
                yield return ComputerTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestGiant") >= 0)
            {
                yield return GiantTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestPlanning") >= 0)
            {
                yield return PlanningTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestWanderers") >= 0)
            {
                yield return WanderersTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestSizes") >= 0)
            {
                yield return SizesTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestAero") >= 0)
            {
                yield return AeroTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestOchre") >= 0)
            {
                yield return OchreTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestSun") >= 0)
            {
                yield return SunTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestRovers") >= 0)
            {
                yield return RoversTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestRegions") >= 0)
            {
                yield return RegionsTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestVesk") >= 0)
            {
                yield return VeskTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestTerrain") >= 0)
            {
                yield return TerrainTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestLooks") >= 0)
            {
                yield return LooksTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestEva") >= 0)
            {
                yield return EvaTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestCrew") >= 0)
            {
                yield return CrewTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestMining") >= 0)
            {
                yield return MiningTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestComms") >= 0)
            {
                yield return CommsTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestCenter") >= 0)
            {
                yield return SpaceCenterTour();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestCareer") >= 0)
            {
                yield return CareerFlight();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestDocking") >= 0)
            {
                yield return DockingFlight();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestRendezvous") >= 0)
            {
                yield return RendezvousFlight();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotestPlane") >= 0)
            {
                yield return PlaneFlight();
                File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            root.currentDesign = VesselDesign.Broadside();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("01_builder");

            root.Launch(VesselDesign.Broadside());
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("02_pad");

            f.EnableAutopilot();
            StartCoroutine(Telemetry());
            yield return WaitFor(() => f.active.Altitude > 400 || f.active.destroyed, 30, "liftoff");
            f.SetCamera(200f, 4f, 45f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("03_liftoff");

            bool done = false;
            StartCoroutine(WarpDriver(() => done));
            yield return WaitFor(() => f.active.nextStage >= 2 || f.active.destroyed, 90, "booster separation");
            f.SetCamera(115f, 2f, 38f);
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Shot("03b_boosters");
            Log($"boosters dropped at {f.active.Altitude / 1000:0.0} km, {f.LoadedCount} pieces nearby");
            yield return WaitFor(() => f.active.Altitude > 16_000 || f.active.destroyed, 150, "16 km");
            f.SetCamera(130f, 10f, 70f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("04_ascent");
            Log($"16 km: speed {f.active.SurfaceRelVelocity.magnitude:0} m/s, stage {f.active.nextStage}");

            yield return WaitFor(() => f.active.Altitude > 76_000 || f.active.destroyed, 240, "space");
            f.SetCamera(240f, -15f, 40f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("05_space");

            yield return WaitFor(() => !f.autopilotOn || f.active.destroyed, 300, "orbit");
            done = true;
            f.RequestWarp(1);
            var v = f.active;
            var o = Orbit.FromState(v.pos, v.vel, v.body.mu, f.ut);
            Log($"RESULT destroyed={v.destroyed} phase={f.autopilot.phase} ap={(o.Apoapsis - v.body.radius) / 1000:0.0}km pe={(o.Periapsis - v.body.radius) / 1000:0.0}km t={f.ut:0}s");
            double dvLeft = 0;
            foreach (var r in v.StageReports()) dvLeft += r.dvVac;
            Log($"dv left {dvLeft:0} m/s, stages used {v.nextStage}");

            f.SetCamera(160f, 15f, 30f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("06_orbit");
            f.mapView = true;
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("07_map");

            // Plan (not fly) a transfer to the moon, to show the maneuver planner and patched conics.
            float tPlan = Time.realtimeSinceStartup;
            bool found = Patcher.FindTransfer(v.body, v.pos, v.vel, f.ut, root.moon, 60_000, out double burnUt, out double tdv, out double tpe);
            Log($"transfer search {(found ? "found" : "FAILED")}: burn in {burnUt - f.ut:0} s, dv {tdv:0} m/s, Tal periapsis {tpe / 1000:0} km ({Time.realtimeSinceStartup - tPlan:0.0}s)");
            if (found)
            {
                f.RefreshTrajectory();
                f.AddNode(burnUt, tdv);
                f.SetTarget(root.moon);
                f.RefreshTrajectory();
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("08_transfer_plan");
                f.SetMapView(root.home, 3.4e7f, 0f, 70f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot("09_transfer_far");
            }

            yield return PlaneFlight();

            File.WriteAllText(Path.Combine(outDir, "autotest_log.txt"), log.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        /// <summary>
        /// Milestone 4: a station is already in orbit (persistent universe); launch, reach orbit, target it, plan and fly a
        /// one-burn rendezvous, match speed and close in, switch to the station, then check the builder's vessel list.
        /// </summary>
        IEnumerator RendezvousFlight()
        {
            var root = GameRoot.I;
            var home = root.home;
            var u = root.universe;
            var stDesign = VesselDesign.Stack("Outpost", "probe.spark", "tank.s4", "eng.ember");
            double r = home.radius + 100_000, sp = Math.Sqrt(home.mu / r), ph = 1.2;
            var stPos = new Vector3d(r * Math.Cos(ph), 0, -r * Math.Sin(ph));
            var stVel = new Vector3d(-sp * Math.Sin(ph), 0, -sp * Math.Cos(ph));
            var station = VesselSim.CreateFree(stDesign, home, stPos, stVel, Quaternion.LookRotation(stVel.normalized.ToFloat(), stPos.normalized.ToFloat()));
            station.hasLaunched = true;
            station.throttle = 0;
            u.Add(station);

            root.currentDesign = VesselDesign.Broadside();
            root.EnterHangar();
            root.hangar.ShowVessels(true);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("20_builder_vessels");
            root.hangar.ShowVessels(false);

            root.Launch(VesselDesign.Broadside());
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            f.EnableAutopilot();
            StartCoroutine(Telemetry());
            bool done = false;
            StartCoroutine(WarpDriver(() => done));
            yield return WaitFor(() => !f.autopilotOn || f.active.destroyed, 400, "orbit");
            done = true;
            f.RequestWarp(1);
            var v = f.active;
            var oc = Orbit.FromState(v.pos, v.vel, home.mu, f.ut);
            Log($"orbit {(oc.Apoapsis - home.radius) / 1000:0.0} x {(oc.Periapsis - home.radius) / 1000:0.0} km; station: {station.Situation(f.ut)}, {u.vessels.Count} vessels in the universe");

            f.SetTarget(station);
            f.mapView = true;
            f.SetMapView(home, (float)(home.radius * 3.2), 20f, 55f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("21_orbit_target");

            var ot = Orbit.FromState(station.pos, station.vel, home.mu, f.ut);
            oc = Orbit.FromState(v.pos, v.vel, home.mu, f.ut);
            bool ok = Patcher.FindRendezvous(home, v.pos, v.vel, f.ut, ot, Patcher.SynodicPeriod(oc, ot), out double burnUt, out double dv, out double closest, out double tc);
            Log($"rendezvous plan {(ok ? "found" : "FAILED")}: burn {dv:0.0} m/s in {burnUt - f.ut:0} s, closest {closest:0} m");
            if (!ok) yield break;
            f.RefreshTrajectory();
            f.AddNode(burnUt, dv);
            f.RefreshTrajectory();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("22_rendezvous_plan");

            // Fly the burn: warp most of the way, point at the node, burn when the timer says so (it cuts itself off).
            f.mapView = false;
            f.SetSas(SasMode.Maneuver);
            double burnTime = ManeuverNode.BurnTime(v, dv);
            double start = burnUt - burnTime / 2;
            while (f.node != null && f.ut < start - 30) { f.RequestWarp((int)Math.Min(100000, Math.Max(1, (start - 30 - f.ut) / 3))); yield return null; }
            f.RequestWarp(1);
            yield return WaitFor(() => f.node == null || f.ut >= start, 60, "burn start");
            v.throttle = 1;
            yield return WaitFor(() => f.node == null || v.destroyed, 60, "burn end");
            v.throttle = 0;
            Log($"transfer burn done, {v.Situation(f.ut)}");

            // Coast to just before the closest approach.
            var oc2 = Orbit.FromState(v.pos, v.vel, home.mu, f.ut);
            var seg = new TrajectorySegment { body = home, orbit = oc2, tStart = f.ut, tEnd = f.ut + oc2.Period, end = SegmentEnd.None };
            Patcher.ClosestApproach(seg, home, Orbit.FromState(station.pos, station.vel, home.mu, f.ut), f.ut, oc2.Period, out double d2, out double tc2, out double rel2);
            Log($"after the burn: closest {d2:0} m in {tc2 - f.ut:0} s at {rel2:0.0} m/s");
            v.sas = SasMode.Stability;
            while (f.ut < tc2 - 45) { f.RequestWarp((int)Math.Min(1000, Math.Max(1, (tc2 - 45 - f.ut) / 3))); yield return null; }
            f.RequestWarp(1);

            // Match speed and close in: steer to cancel the difference between our relative velocity and a gentle approach.
            float t0 = Time.realtimeSinceStartup;
            double dist = 0, relSpeed = 0;
            while (Time.realtimeSinceStartup - t0 < 100 && !v.destroyed)
            {
                Vector3d relP = station.AbsolutePosition(f.ut) - v.AbsolutePosition(f.ut);
                Vector3d relV = v.AbsoluteVelocity(f.ut) - station.AbsoluteVelocity(f.ut);
                dist = relP.magnitude;
                relSpeed = relV.magnitude;
                if (dist < 220 && relSpeed < 0.8) break;
                Vector3d want = relP.normalized * Math.Min(15, Math.Max(0.5, (dist - 120) / 30));
                Vector3d err = want - relV;
                v.steerDirection = err.normalized;
                double align = Vector3d.Dot(v.Axis, err.normalized);
                double acc = v.PotentialThrust() / Math.Max(0.1, v.mass);
                v.throttle = align > 0.99 && err.magnitude > 0.3 ? Math.Min(1, err.magnitude / Math.Max(0.1, acc) * 1.5) : 0;
                f.RequestWarp(v.throttle == 0 && dist > 1500 && err.magnitude < 1 ? 10 : 1);
                yield return null;
            }
            v.throttle = 0;
            v.steerDirection = null;
            f.RequestWarp(1);
            Log($"close in: {dist:0} m from the station at {relSpeed:0.00} m/s, {v.Situation(f.ut)}");
            f.LookAlong(station.AbsolutePosition(f.ut) - v.AbsolutePosition(f.ut), 26f, 24f, 10f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("23_closing_in");

            var chaser = v;
            f.SwitchTo(station);
            yield return new WaitForSecondsRealtime(0.5f);
            f.LookAlong(chaser.AbsolutePosition(f.ut) - station.AbsolutePosition(f.ut), 20f, -24f, 10f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("24_switched");
            Log($"switched: flying {f.active.name}, target {f.TargetName}, {f.LoadedCount} vessel(s) loaded nearby");

            root.EnterHangar();
            root.hangar.ShowVessels(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("25_builder_after");
            Log($"universe saved with {u.vessels.Count} vessels: {string.Join(", ", u.vessels.ConvertAll(x => x.name))}");
        }

        /// <summary>
        /// Milestone 5: a new career. Take the first contracts, fly the First Flight straight up, run science high up,
        /// come down under the parachute, recover it, and spend the science in the tech tree.
        /// </summary>
        IEnumerator CareerFlight()
        {
            var root = GameRoot.I;
            var c = root.universe.career;
            root.currentDesign = VesselDesign.FirstFlight();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("40_career_builder");
            Log($"career: funds {c.funds:N0}, science {c.science:0.0}; first flight costs {Prices.Of(root.currentDesign):N0}; offers: {string.Join(" | ", c.Offered.Select(k => k.title))}");

            root.hangar.ShowMissions(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("41_missions");
            foreach (var k in c.Offered.ToList()) c.Accept(k, root.universe.ut);
            Log($"accepted {c.Active.Count()} contracts, funds now {c.funds:N0}");
            root.hangar.ShowTech(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("42_tech_tree");
            root.hangar.ShowTech(false);

            root.Launch(root.currentDesign);
            var f = root.flight;
            var v = f.active;
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"launched: funds {c.funds:N0}");
            v.sas = SasMode.Stability;
            f.Stage();
            bool done = false;
            StartCoroutine(PhysicsWarp(() => done));
            bool ranHigh = false, staged = false;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 240 && !v.destroyed && !(v.landed && v.hasLaunched && staged))
            {
                double vs = Vector3d.Dot(v.vel - root.home.SurfaceVelocity(v.pos), v.pos.normalized);
                if (!ranHigh && v.Altitude > 19_000)
                {
                    ranHigh = true;
                    foreach (var (p, e) in Lab.On(v)) f.RunExperiment(p, e);
                    f.SetCamera(160f, 8f, 18f);
                    yield return new WaitForSecondsRealtime(0.3f);
                    yield return OpenScienceShot(f);
                    Log($"science at {v.Altitude / 1000:0.0} km: {string.Join("; ", v.parts.SelectMany(p => p.science).Select(d => d.title))}");
                }
                if (!staged && vs < -50 && v.Altitude < 45_000 && v.thrustNow <= 0)
                {
                    staged = true;
                    f.Stage(); // drop the tank and engine
                    yield return new WaitForSecondsRealtime(0.3f);
                    f.Stage(); // arm the parachute
                    v = f.active;
                    Log($"staged at {v.Altitude / 1000:0.0} km, falling at {-vs:0} m/s");
                }
                yield return null;
            }
            done = true;
            f.RequestWarp(1);
            Log($"down: landed={v.landed} destroyed={v.destroyed} {v.destroyReason}; {v.Situation(f.ut)}; contracts done so far {c.contracts.Count(k => k.state == ContractState.Completed)}");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("44_landed");
            if (!f.CanRecover) yield break;
            f.Recover();
            yield return new WaitForSecondsRealtime(0.5f);
            Log($"after recovery: funds {c.funds:N0}, science {c.science:0.0}, rep {c.reputation:0}, records {c.records}, completed {string.Join(", ", c.contracts.Where(k => k.state == ContractState.Completed).Select(k => k.title))}");
            root.hangar.ShowMissions(true);
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("45_after_recovery");
            bool researched = c.Research(TechTree.Get("staging"));
            root.hangar.ShowTech(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("46_research");
            Log($"researched Staging: {researched}; science left {c.science:0.0}");
        }

        /// <summary>
        /// Milestone 5b: the space center screen; hire, upgrade and start a strategy; a rocket too heavy for the pad;
        /// a rescue contract putting a stranded capsule in orbit; and a crewed launch seating an astronaut from the roster.
        /// </summary>
        IEnumerator SpaceCenterTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var c = u.career;
            root.currentDesign = VesselDesign.FirstFlight();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(0.8f);
            root.hangar.ShowCenter(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("50_space_center");
            root.hangar.ShowCenter(false);

            // A rocket too heavy for the level-1 pad (research unlocked for the demo)
            foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id);
            c.funds += 150_000; // a few contracts' worth, so the tour can spend
            root.currentDesign = VesselDesign.Longshot();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("52_too_heavy");
            Log($"Longshot {Career.LaunchMass(root.currentDesign):0} t: {c.LaunchProblem(root.currentDesign)}");

            Log($"hire: {c.Hire()}");
            Log($"upgrade pad: {c.Upgrade(Facility.Pad)}, mission control: {c.Upgrade(Facility.MissionControl)}");
            Log($"strategy: {c.Activate(Career.Strategies.First(s => s.id == "pr"))}");
            Log($"Longshot after the pad upgrade: {c.LaunchProblem(root.currentDesign) ?? "ready to launch"}");
            root.hangar.ShowCenter(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("51_after_upgrades");
            root.hangar.ShowCenter(false);

            c.SetRecord(Records.Orbit);
            var k = new Contract { id = 999, type = "rescue", title = "Rescue Juno Marsh", description = "Juno Marsh is stranded in a 110 km orbit of Arden in a capsule with a small docking port. Dock with it, bring Juno aboard and recover them on Arden.",
                body = "Arden", crewName = "Juno Marsh", altitude = 110_000, reward = 60_000, advance = 12_000, penalty = 30_000, repReward = 15, state = ContractState.Offered, deadline = 200 * 86400 };
            c.contracts.Add(k);
            c.Accept(k, u.ut);
            u.SpawnRescues(root.bodies);
            var stranded = u.Find(k.vesselId);
            Log($"rescue: {stranded?.name} {stranded?.Situation(u.ut)} with {string.Join(", ", stranded?.parts.SelectMany(p => p.crew) ?? new string[0])}");
            root.hangar.ShowMissions(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("53_rescue_contract");
            root.hangar.ShowMissions(false);

            root.Launch(VesselDesign.FirstFlight());
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("54_crewed_launch");
            var v = root.flight.active;
            Log($"launched with crew: {string.Join(", ", v.parts.SelectMany(p => p.crew))}; roster: {string.Join(", ", c.roster.Select(m => $"{m.name} ({(m.state == 0 ? "available" : m.state == 1 ? "flying" : "lost")})"))}");
            root.flight.ShowVesselList(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("55_vessels");
        }

        /// <summary>
        /// Milestone 6a: a bare probe's warnings in the builder; a probe behind Arden talking to the ground through a relay
        /// (map links, HUD power and signal); then the relay loses power and the probe can't stage.
        /// </summary>
        IEnumerator CommsTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            root.currentDesign = VesselDesign.Stack("Bare probe", "probe.spark", "tank.s1", "eng.ember");
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("60_probe_warnings");

            VesselSim Orbiting(VesselDesign d, double alt, double lon)
            {
                double r = home.radius + alt;
                Vector3d dir = CelestialBody.DirectionFromLatLon(0, lon);
                Vector3d east = CelestialBody.EastAt(dir);
                var v = VesselSim.CreateFree(d, home, dir * r, east * Math.Sqrt(home.mu / r), Quaternion.LookRotation(dir.ToFloat(), east.ToFloat()));
                foreach (var p in v.parts) if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1; }
                v.throttle = 0;
                v.hasLaunched = true;
                return u.Add(v);
            }
            var rd = VesselDesign.Stack("Relay-1", "probe.spark", "battery.s", "tank.s1");
            int rt = rd.nodes.FindIndex(n => n.id == "tank.s1");
            DesignOps.AttachSurfaceAt(rd, rt, 0.4f, 0f, "solar.s", 2, false);
            DesignOps.AttachSurfaceAt(rd, rt, 0.6f, 90f, "relay.s", 1, false);
            var relay = Orbiting(rd, 2_500_000, 75);
            var sd = VesselDesign.Stack("Scout", "probe.spark", "battery.s", "tank.s1", "eng.ember");
            int st = sd.nodes.FindIndex(n => n.id == "tank.s1");
            DesignOps.AttachSurfaceAt(sd, st, 0.4f, 0f, "solar.s", 2, false);
            DesignOps.AttachSurfaceAt(sd, st, 0.6f, 90f, "antenna.s", 1, false);
            var scout = Orbiting(sd, 150_000, 150);

            root.Fly(scout);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"scout: {f.commLink?.Describe}; power {scout.EcStored:0}/{scout.EcCapacity:0} EC, {scout.ecRate:+0.00;-0.00} EC/s, sunlit={scout.InSunlight(f.ut)}");
            f.mapView = true;
            f.SetMapView(home, (float)(home.radius * 9.5), 0f, 80f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("61_map_relay");
            f.mapView = false;
            f.SetCamera(200f, 15f, 14f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("62_hud_signal");

            foreach (var p in relay.parts) if (p.def.relay) { p.legsDeployed = false; p.legDeploy = 0; } // the relay folds its dish
            yield return new WaitForSecondsRealtime(1.2f);
            f.Stage();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("63_no_signal");
            Log($"relay dish folded: {f.commLink?.Describe}; partial control={scout.PartialControl}; staging blocked={scout.nextStage == 0}");
        }

        /// <summary>
        /// Milestone 6b: a mining lander without radiators in the builder; Tal's ore map after a polar survey satellite has
        /// worked for ten hours; the Prospector lander drilling and refining on a rich, sunlit, mapped spot; an hour of warp.
        /// </summary>
        IEnumerator MiningTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var tal = root.moon;
            var hot = VesselDesign.Lander("Hot miner");
            DesignOps.Remove(hot, Enumerable.Range(0, hot.nodes.Count).Where(i => hot.nodes[i].id == "rad.r").ToList());
            root.currentDesign = hot;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("70_miner_warnings");

            // A survey satellite in a polar orbit of Tal maps it for ten hours (run straight through the sim), and stays there.
            double r = tal.radius + 60_000, sp = Math.Sqrt(tal.mu / r);
            var sd = VesselDesign.Stack("Surveyor", "probe.spark", "battery.s", "tank.s1");
            int st = sd.nodes.FindIndex(n => n.id == "tank.s1");
            DesignOps.AttachSurfaceAt(sd, st, 0.4f, 0f, "solar.s", 2, false);
            DesignOps.AttachSurfaceAt(sd, st, 0.6f, 90f, "scan.orbit", 1, false);
            var sat = VesselSim.CreateFree(sd, tal, new Vector3d(r, 0, 0), new Vector3d(0, sp, 0), Quaternion.identity);
            foreach (var p in sat.parts) if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1; }
            sat.throttle = 0;
            u.Add(sat);
            double t = u.ut;
            for (int i = 0; i < 60; i++) { foreach (var p in sat.parts) p.charge = p.def.ecCapacity; sat.BackgroundAdvance(600, t, false); t += 600; }
            u.ut = t;
            Log($"surveyor: {sat.scanNote}; Tal {u.survey.Coverage(tal) * 100:0}% mapped after 10 h");

            // The lander goes down on the richest mapped spot with the sun well up.
            double best = -1;
            Vector3d spot = Vector3d.up;
            for (int la = -70; la <= 70; la += 2)
                for (int lo = -180; lo < 180; lo += 2)
                {
                    var d = CelestialBody.DirectionFromLatLon(la, lo);
                    if (!u.survey.IsScanned(tal, d) || Vector3d.Dot(tal.FixedToInertial(d, u.ut), Sun.DirectionAt(GameRoot.I.universe.ut)) < 0.55) continue;
                    double c = Ore.Concentration(tal, d);
                    if (c > best) { best = c; spot = d; }
                }
            var lander = VesselSim.CreateLanded(VesselDesign.Lander("Prospector"), tal, spot, u.ut);
            foreach (var p in lander.parts)
            {
                if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1; }
                if (p.def.kind == PartKind.Tank) p.propellant = p.def.propellant * 0.05;
            }
            lander.SetConverter(lander.parts.First(p => p.def.convertRate > 0), 1);
            lander.throttle = 0;
            u.Add(lander);
            root.Fly(lander);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            OreSurvey.LatLon(spot, out double lat, out double lon);
            Log($"lander at {lat:0}, {lon:0} ({best * 100:0.0}% ore), sunlit={lander.InSunlight(f.ut)}, solar {lander.SolarOutput(f.ut):0.0} EC/s");

            f.mapView = true;
            f.showOreMap = true;
            Vector3d sIn = tal.FixedToInertial(spot, f.ut); // look straight down at the landing site
            f.SetMapView(tal, (float)(tal.radius * 4.6), (float)(Math.Atan2(-sIn.x, -sIn.z) * 180 / Math.PI), (float)Math.Max(-80, Math.Min(80, Math.Asin(sIn.y) * 180 / Math.PI)));
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("71_ore_map_tal");

            f.mapView = false;
            f.showOreMap = false;
            f.ShowMiningPanel(true);
            f.SetCamera(30f, 10f, 17f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("72_drilling");
            var conv = lander.parts.First(p => p.def.convertRate > 0);
            lander.convNotes.TryGetValue(conv.index, out string cn);
            Log($"drills: {lander.drillNote}, {lander.oreRate * 3600:0.0} t/h; converter: {cn}; core {lander.coreTemp:0} K; mining {lander.miningEc:0.0} EC/s");

            double fuel0 = lander.parts.Sum(p => p.propellant), ut0 = f.ut;
            f.RequestWarp(100);
            yield return WaitFor(() => f.ut >= ut0 + 900, 30f, "15 minutes of warp");
            f.RequestWarp(1);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("73_after_warp");
            lander.convNotes.TryGetValue(conv.index, out cn);
            Log($"after {(f.ut - ut0) / 3600:0.0} h: fuel +{lander.parts.Sum(p => p.propellant) - fuel0:0.00} t, ore {lander.OreStored:0.00} t; core {lander.coreTemp:0} K at {lander.heatEff * 100:0}% speed; converter: {cn}");
        }

        IEnumerator OpenScienceShot(FlightMode f)
        {
            f.ShowSciencePanel(true);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("43_science");
            f.ShowSciencePanel(false);
        }

        /// <summary>4x physics warp until told to stop (rails warp when the flight allows it).</summary>
        IEnumerator PhysicsWarp(Func<bool> stop)
        {
            var f = GameRoot.I.flight;
            while (!stop())
            {
                if (f.active != null && !f.active.destroyed) f.RequestWarp(f.active.CanRailsWarp() && !f.active.landed ? 50 : 4);
                yield return null;
            }
        }

        static float Clamp1(float x, float dead = 0.04f) => Mathf.Abs(x) < dead ? 0f : Mathf.Clamp(x, -1f, 1f);

        /// <summary>
        /// Milestone 4b: a ferry 40 m behind a station in the same orbit lines its port up with SAS "dock align",
        /// creeps in with RCS and latches; one crew member moves into the station's habitat (and back); then it undocks.
        /// </summary>
        IEnumerator DockingFlight()
        {
            var root = GameRoot.I;
            var home = root.home;
            var u = root.universe;
            double r = home.radius + 100_000, sp = Math.Sqrt(home.mu / r), ph = 0.5;
            Vector3d P(double a) => new Vector3d(r * Math.Cos(a), 0, -r * Math.Sin(a));
            Vector3d V(double a) => new Vector3d(-sp * Math.Sin(a), 0, -sp * Math.Cos(a));

            var stPos = P(ph);
            var stVel = V(ph);
            var station = VesselSim.CreateFree(VesselDesign.Outpost(), home, stPos, stVel,
                Quaternion.LookRotation(stPos.normalized.ToFloat(), (-stVel).normalized.ToFloat())); // port faces back along the orbit
            station.throttle = 0;
            foreach (var p in station.parts)
            {
                if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1f; }
                if (p.def.kind == PartKind.Habitat) p.crew.Clear(); // waiting for its first crew
            }
            station.RecomputeMass();
            u.Add(station);
            double back = 40.0 / r;
            var fPos = P(ph - back);
            var fVel = V(ph - back);
            var ferry = VesselSim.CreateFree(VesselDesign.FerryCraft(), home, fPos, fVel, Quaternion.LookRotation(fPos.normalized.ToFloat(), fVel.normalized.ToFloat()));
            ferry.throttle = 0;
            ferry.rcsOn = true;
            u.Add(ferry);

            root.Fly(ferry);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(0.8f);
            f.SetTarget(station);
            yield return null;
            f.SetSas(SasMode.DockAlign);
            f.LookAlong(station.AbsolutePosition(f.ut) - ferry.AbsolutePosition(f.ut), 12f, 30f, 14f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("30_approach");
            Log($"approach: {f.dockDist:0.0} m, off-axis {f.dockLateral:0.00} m, angle {f.dockAngle:0.0} deg, ports {f.dockMine?.def.name} -> {f.dockTheirs?.def.name}");

            float t0 = Time.realtimeSinceStartup;
            bool finalShot = false;
            while (Time.realtimeSinceStartup - t0 < 150 && u.vessels.Contains(station) && !f.active.destroyed)
            {
                var v = f.active;
                if (f.dockMine == null || f.dockTheirs == null) { f.translateOverride = Vector3.zero; yield return null; continue; }
                v.PortFaceWorld(f.dockMine, out var pm, out _);
                station.PortFaceWorld(f.dockTheirs, out var pt, out var nt);
                Vector3d d = pm - pt;
                double s = Vector3d.Dot(d, nt);
                Vector3d lat = d - nt * s;
                Vector3d relV = v.vel - station.vel;
                bool lined = f.dockAngle < 4 && lat.magnitude < 0.25;
                double approach = lined ? Math.Min(0.7, Math.Max(0.2, s * 0.05)) : (s > 6 ? Math.Min(0.5, (s - 5) * 0.05) : 0);
                Vector3d fix = -lat * 0.3;
                if (fix.magnitude > 0.5) fix = fix.normalized * 0.5;
                Vector3d err = (-nt * approach + fix) - relV;
                Vector3 local = Quaternion.Inverse(v.rot) * err.ToFloat();
                f.translateOverride = new Vector3(Clamp1(local.x * 5), Clamp1(local.y * 5), Clamp1(local.z * 5));
                if (!finalShot && s < 5)
                {
                    finalShot = true;
                    f.LookAlong(station.AbsolutePosition(f.ut) - v.AbsolutePosition(f.ut), 9f, 70f, 8f);
                    yield return Shot("31_final_approach");
                    Log($"final approach: {f.dockDist:0.00} m, off-axis {f.dockLateral:0.00} m, angle {f.dockAngle:0.0} deg, closing {f.dockClosing:0.00} m/s, mono left {v.MonoLeft * 1000:0} kg");
                }
                yield return null;
            }
            f.translateOverride = null;
            bool docked = !u.vessels.Contains(station);
            var ship = f.active;
            Log($"docked={docked} after {Time.realtimeSinceStartup - t0:0} s; {ship.name}: {ship.parts.Count} parts, {ship.mass:0.00} t, crew {ship.CrewCount}, control={ship.HasControl}");
            if (!docked) yield break;
            yield return new WaitForSecondsRealtime(1.2f);
            f.SetCamera(200f, 18f, 22f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("32_docked");

            var pod = ship.parts.Find(p => p.def.id == "pod.wren");
            var hab = ship.parts.Find(p => p.def.kind == PartKind.Habitat);
            string moved = f.TransferCrew(pod, hab);
            Log($"crew transfer: {moved} control={ship.HasControl} (the station's probe core still flies it)");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("33_crew_transfer");
            f.TransferCrew(hab, pod);

            var port = ship.parts.Find(p => p.def.kind == PartKind.DockingPort && p.dockedTo >= 0);
            var piece = f.Undock(port);
            Log($"undocked: {ship.name} ({ship.parts.Count} parts, crew {ship.CrewCount}) and {piece?.name} ({piece?.parts.Count} parts, crew {piece?.CrewCount})");
            f.translateOverride = new Vector3(0, -1, 0);
            yield return new WaitForSecondsRealtime(3f);
            f.translateOverride = Vector3.zero;
            yield return new WaitForSecondsRealtime(1.5f);
            f.translateOverride = null;
            f.SetCamera(200f, 18f, 30f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("34_undocked");
            Log($"after backing off: {Vector3d.Distance(ship.pos, piece.pos):0.0} m apart");

            root.EnterHangar();
            root.hangar.ShowVessels(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("35_builder");
            Log($"universe: {string.Join(", ", u.vessels.ConvertAll(x => $"{x.name} ({x.CrewCount} crew)"))}");
        }

        /// <summary>The Kite plane: plane builder, runway, takeoff roll, liftoff, gear up and a climb.</summary>
        IEnumerator PlaneFlight()
        {
            var root = GameRoot.I;
            root.currentDesign = VesselDesign.Kite();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("10_plane_builder");

            root.Launch(VesselDesign.Kite(), true);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("11_runway");

            var v = f.active;
            v.throttle = 1;
            f.Stage();
            f.FireGroup(Groups.Brakes);
            bool fly = true;
            StartCoroutine(PlanePilot(() => fly));
            yield return WaitFor(() => v.SurfaceRelVelocity.magnitude > 55 || v.destroyed, 30, "takeoff roll");
            f.SetCamera(215f, 6f, 24f);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Shot("12_takeoff_roll");
            Log($"takeoff roll: {v.SurfaceRelVelocity.magnitude:0} m/s, thrust {v.thrustNow:0} kN");

            yield return WaitFor(() => v.RadarAltitude(f.ut) > 12 || v.destroyed, 30, "liftoff");
            f.SetCamera(250f, 2f, 28f);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Shot("13_liftoff");
            Log($"liftoff: {v.SurfaceRelVelocity.magnitude:0} m/s, destroyed={v.destroyed} {v.destroyReason}");

            yield return WaitFor(() => v.Altitude > 600 || v.destroyed, 40, "climb");
            f.FireGroup(Groups.Gear);
            yield return WaitFor(() => v.Altitude > 1500 || v.destroyed, 40, "1.5 km");
            f.SetCamera(300f, 12f, 22f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("14_flying");
            Log($"flying: alt {v.Altitude:0} m, {v.SurfaceRelVelocity.magnitude:0} m/s, gear {(v.gearDown ? "down" : "up")}, destroyed={v.destroyed} {v.destroyReason}");
            fly = false;
        }

        /// <summary>Holds the runway heading; rotates to 12 deg nose-up from 70 m/s.</summary>
        IEnumerator PlanePilot(Func<bool> on)
        {
            var f = GameRoot.I.flight;
            var v = f.active;
            Vector3d headingFixed = v.body.InertialToFixed(Vector3d.FromFloat(v.rot * Vector3.up), f.ut);
            while (on() && f.active == v && !v.destroyed)
            {
                Vector3d up = v.pos.normalized, fwd = v.body.FixedToInertial(headingFixed, f.ut);
                fwd = (fwd - up * Vector3d.Dot(fwd, up)).normalized;
                double pitch = (v.SurfaceRelVelocity.magnitude < 70 ? 0 : 12) * Math.PI / 180;
                v.steerDirection = fwd * Math.Cos(pitch) + up * Math.Sin(pitch);
                yield return null;
            }
            if (f.active == v) v.steerDirection = null;
        }

        /// <summary>Logs the flight every 5 s of mission time, and on every autopilot phase change.</summary>
        IEnumerator Telemetry()
        {
            var f = GameRoot.I.flight;
            double nextT = 0;
            var lastPhase = (FlightAutopilot.Phase)(-1);
            while (f.active != null && !f.active.destroyed && (f.autopilotOn || f.ut < 5))
            {
                var v = f.active;
                if (f.ut >= nextT || f.autopilot.phase != lastPhase)
                {
                    var o = Orbit.FromState(v.pos, v.vel, v.body.mu, f.ut);
                    Vector3d up = v.pos.normalized;
                    double vs = Vector3d.Dot(v.vel, up);
                    double hs = (v.vel - up * vs).magnitude;
                    Log($"T={f.ut,6:0.0} {f.autopilot.phase,-11} alt={v.Altitude / 1000,6:0.0}km vs={vs,5:0} hs={hs,5:0} thr={v.throttle:0.00} F={v.thrustNow,4:0}kN stg={v.nextStage} fuel={v.ActiveFuelFraction():0.00} ap={(o.Apoapsis - v.body.radius) / 1000,6:0.0} warp={f.warp}{(f.railsWarp ? "R" : "")}");
                    lastPhase = f.autopilot.phase;
                    while (nextT <= f.ut) nextT += 5;
                }
                yield return null;
            }
        }

        /// <summary>Speeds the flight up: 4x physics warp while burning, rails warp while coasting.</summary>
        IEnumerator WarpDriver(Func<bool> stop)
        {
            var f = GameRoot.I.flight;
            while (!stop())
            {
                if (f.active != null && !f.active.destroyed)
                    f.RequestWarp(f.active.CanRailsWarp() ? 1000 : 4);
                yield return null;
            }
        }
    }
}
