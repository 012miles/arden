using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>
    /// Headless physics tests. Run with:
    ///   Unity.exe -batchmode -projectPath . -executeMethod Rocket.EditorTools.SimTests.RunAll
    /// Results go to TestResults/sim_tests.txt; exit code 0 = all passed.
    /// </summary>
    public static partial class SimTests
    {
        static readonly StringBuilder Out = new StringBuilder();
        static int failures;
        static CelestialBody Body => CelestialBody.CreateHome();

        [MenuItem("Rocket/Run Sim Tests")]
        public static void RunAll()
        {
            Out.Clear();
            failures = 0;
            Weather.On = false;      // milestone 27: the weather tests turn it on themselves
            VesselSim.VaryToughness = false;   // milestone 35: parts exactly at their ratings, so results repeat
            VesselSim.EngineHeat = false;      // milestone 39: engines making heat changes older numbers (its own tests switch it on)
            // -only3b runs just the spaceplane tests (quick iteration).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only3b") >= 0) { RunMilestone3b(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only4") >= 0) { RunMilestone4a(); RunMilestone4b(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only5") >= 0) { RunMilestone5a(); RunMilestone5b(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only6") >= 0) { RunMilestone6a(); RunMilestone6b(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only7") >= 0) { RunMilestone7a(); RunMilestone7b(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only8") >= 0) { RunMilestone8(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only9") >= 0) { RunMilestone9(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only10") >= 0) { RunMilestone10(); RunMilestone10b(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only11") >= 0) { RunMilestone11(); RunMilestone11b(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only12") >= 0) { RunMilestone12(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only13") >= 0) { RunMilestone13(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only14") >= 0) { RunMilestone14(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only15") >= 0) { RunMilestone15(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only16") >= 0) { RunMilestone16(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only17") >= 0) { RunMilestone17(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only18") >= 0) { RunMilestone18(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-onlyFixes") >= 0) { RunFixes(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only19") >= 0) { RunMilestone19(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only20") >= 0) { RunMilestone20(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only21") >= 0) { RunMilestone21(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only22") >= 0) { RunMilestone22(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only23") >= 0) { RunMilestone23(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only24") >= 0) { RunMilestone24(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only27") >= 0) { RunMilestone27(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only28") >= 0) { RunMilestone28(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only29") >= 0) { RunMilestone29(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only30") >= 0) { RunMilestone30(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only31") >= 0) { RunMilestone31(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only32") >= 0) { RunMilestone32(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only34") >= 0) { RunMilestone34(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only35") >= 0) { RunMilestone35(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only38") >= 0) { RunMilestone38(); Finish(); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-only39") >= 0) { RunMilestone39(); Finish(); return; }
            Run("Orbit: state at epoch round-trips (ellipse + hyperbola)", StateAtEpoch);
            Run("Orbit: one full period returns to the start", OrbitPeriodRoundTrip);
            Run("Orbit: circular orbit reports Ap = Pe", CircularApPe);
            Run("Flight integrator (RK4) matches the exact orbit", Rk4MatchesKepler);
            Run("Starter rocket: stages and delta-v match hand calculation", DesignDeltaV);
            Run("Rocket sits on the pad and turns with the planet", PadHold);
            Run("Decoupling conserves mass and momentum", DecoupleConservation);
            Run("Capsule without parachute crashes", CrashWithoutChute);
            Run("Full mission: autopilot to orbit, deorbit, reentry, parachute landing", FullMission);
            Run("Autopilot reaches orbit while coasting under 1000x time warp", WarpedAscent);
            Run("Moon: SOI size and orbital motion are consistent", MoonGeometry);
            Run("SOI crossing keeps absolute position and velocity continuous", SoiContinuity);
            Run("Moon transfer: planned burn gives a Tal encounter that the flight reproduces", MoonTransfer);
            Run("Maneuver node: guided finite burn matches the planned orbit", ManeuverBurn);
            Run("Fast reentry (moon return): no shield burns up, heat shield survives", FastReentry);
            Run("Moon rocket preset has the delta-v for a Tal round trip", LongshotDeltaV);
            RunMilestone3();
            RunMilestone3b();
            RunMilestone4a();
            RunMilestone4b();
            RunMilestone5a();
            RunMilestone5b();
            RunMilestone6a();
            RunMilestone6b();
            RunMilestone7a();
            RunMilestone7b();
            RunMilestone8();
            RunMilestone9();
            RunMilestone10();
            RunMilestone10b();
            RunMilestone11();
            RunMilestone11b();
            RunMilestone12();
            RunMilestone13();
            RunMilestone14();
            RunMilestone15();
            RunMilestone16();
            RunMilestone17();
            RunMilestone18();
            RunFixes();
            RunMilestone19();
            RunMilestone20();
            RunMilestone21();
            RunMilestone22();
            RunMilestone23();
            RunMilestone24();
            RunMilestone27();
            RunMilestone28();
            RunMilestone29();
            RunMilestone30();
            RunMilestone31();
            RunMilestone32();
            RunMilestone34();
            RunMilestone35();
            RunMilestone38();
            RunMilestone39();
            if (Environment.GetCommandLineArgs().Length > 0 && Array.IndexOf(Environment.GetCommandLineArgs(), "-heatSweep") >= 0) HeatSweep();
            Finish();
        }

        static void Finish()
        {
            string summary = failures == 0 ? "ALL TESTS PASSED" : $"{failures} TEST(S) FAILED";
            Out.AppendLine(summary);
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/sim_tests.txt", Out.ToString());
            Debug.Log("SIMTESTS\n" + Out);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void Run(string name, Func<string> test)
        {
            var t0 = DateTime.Now;
            try
            {
                string detail = test();
                Out.AppendLine($"PASS  {name}  ({(DateTime.Now - t0).TotalSeconds:0.0}s)\n      {detail}");
            }
            catch (Exception e)
            {
                failures++;
                Out.AppendLine($"FAIL  {name}\n      {e.Message}");
                if (e.GetType() != typeof(Exception) && e.StackTrace != null)      // a crash, not a failed check: say where
                    foreach (var line in System.Linq.Enumerable.Take(e.StackTrace.Split('\n'), 4)) Out.AppendLine("        " + line.Trim());
            }
        }

        static void Check(bool ok, string msg) { if (!ok) throw new Exception(msg); }

        static VesselDesign D(string name, params string[] parts) => VesselDesign.Stack(name, parts);

        // ------------------------------------------------------------------ orbits

        static string StateAtEpoch()
        {
            var b = Body;
            var r = new Vector3d(b.radius + 120_000, 30_000, -50_000);
            double vc = Math.Sqrt(b.mu / r.magnitude);
            var cases = new[] { new Vector3d(300, 150, vc * 1.05), new Vector3d(500, -200, vc * 1.9) };
            double worst = 0;
            foreach (var v in cases)
            {
                var o = Orbit.FromState(r, v, b.mu, 100);
                o.StateAt(100, out var r2, out var v2);
                worst = Math.Max(worst, (r2 - r).magnitude);
                Check((r2 - r).magnitude < 0.01 && (v2 - v).magnitude < 1e-5, $"mismatch e={o.e:F3}");
            }
            return $"worst position error {worst:E2} m";
        }

        static string OrbitPeriodRoundTrip()
        {
            var b = Body;
            var r = new Vector3d(b.radius + 100_000, 0, 0);
            double vc = Math.Sqrt(b.mu / r.magnitude);
            var o = Orbit.FromState(r, new Vector3d(0, vc * 0.25, -vc * 1.08), b.mu, 0);
            o.StateAt(o.Period, out var r2, out _);
            double err = (r2 - r).magnitude;
            Check(err < 1.0, $"position error after one period {err:F3} m");
            return $"period {o.Period:F0} s, e={o.e:F3}, error {err:E2} m";
        }

        static string CircularApPe()
        {
            var b = Body;
            double rr = b.radius + 100_000;
            var o = Orbit.FromState(new Vector3d(0, 0, rr), new Vector3d(Math.Sqrt(b.mu / rr), 0, 0), b.mu, 0);
            double ap = o.Apoapsis - b.radius, pe = o.Periapsis - b.radius;
            Check(Math.Abs(ap - 100_000) < 1 && Math.Abs(pe - 100_000) < 1, $"Ap {ap:F1} Pe {pe:F1}");
            return $"Ap {ap:F2} m, Pe {pe:F2} m, period {o.Period / 60:F1} min";
        }

        static string Rk4MatchesKepler()
        {
            var b = Body;
            double rr = b.radius + 200_000;
            var r = new Vector3d(rr, 0, 0);
            var v = new Vector3d(0, 300, -Math.Sqrt(b.mu / rr) * 1.03);
            var sim = VesselSim.CreateFree(D("probe", "probe.spark"), b, r, v, Quaternion.identity);
            sim.disableRails = true;
            sim.sas = SasMode.Off;
            double ut = 0;
            for (int i = 0; i < 30_000; i++) { sim.Step(0.02, ut); ut += 0.02; }
            Orbit.FromState(r, v, b.mu, 0).StateAt(ut, out var rk, out _);
            double err = (sim.pos - rk).magnitude;
            Check(err < 5, $"drift {err:F2} m after {ut:F0} s");
            return $"drift {err:F3} m after {ut:F0} s of integration";
        }

        // ------------------------------------------------------------------ rockets

        static string DesignDeltaV()
        {
            var d = VesselDesign.Default();
            var defs = d.Defs();
            var stages = d.Stages();
            Check(stages.Count == 4, $"expected 4 stages, got {stages.Count}");
            var reps = Staging.AnalyzeDesign(d, 9.81);
            double total = 0; foreach (var p in defs) total += p.WetMass;
            double expect1 = 300 * VesselSim.G0 * Math.Log(total / (total - 5.0));
            double upper = 0.1 + 0.85 + 5 * Supplies.MassPerDay + 0.05 + 1.13 + 0.45;   // the capsule's 5 crew-days of supplies weigh 25 kg
            double expect2 = 345 * VesselSim.G0 * Math.Log(upper / (upper - 1.0));
            Check(Math.Abs(reps[0].dvVac - expect1) < 0.5, $"stage 1 dv {reps[0].dvVac:F1} vs {expect1:F1}");
            Check(Math.Abs(reps[1].dvVac - expect2) < 0.5, $"stage 2 dv {reps[1].dvVac:F1} vs {expect2:F1}");
            return $"stage 1 {reps[0].dvVac:F0} m/s (TWR {reps[0].twrAsl:F2}), stage 2 {reps[1].dvVac:F0} m/s, liftoff {total:F2} t";
        }

        static string LongshotDeltaV()
        {
            var d = VesselDesign.Longshot();
            Check(d.Validate() == null, "invalid: " + d.Validate());
            var reps = Staging.AnalyzeDesign(d, 9.81);
            double total = 0; foreach (var r in reps) total += r.dvVac;
            var warn = d.Warnings(9.81);
            Check(total > 5500, $"only {total:F0} m/s");
            Check(reps[0].twrAsl > 1.3, $"liftoff TWR {reps[0].twrAsl:F2}");
            return $"{total:F0} m/s vacuum over {reps.Count} stages, liftoff TWR {reps[0].twrAsl:F2}, warnings: {(warn.Count == 0 ? "none" : string.Join("; ", warn))}";
        }

        static string PadHold()
        {
            var b = Body;
            var sim = VesselSim.CreateOnPad(VesselDesign.Default(), b, 0);
            double r0 = sim.pos.magnitude, ut = 0;
            for (int i = 0; i < 500; i++) { sim.Step(0.02, ut); ut += 0.02; }
            Check(sim.landed && !sim.destroyed, "rocket left the pad without engines");
            Check(Math.Abs(sim.pos.magnitude - r0) < 1e-3, "rocket height changed on the pad");
            double moved = (sim.pos - VesselSim.CreateOnPad(VesselDesign.Default(), b, 0).pos).magnitude;
            Check(Math.Abs(moved - b.AngularVelocity * r0 * ut) < 0.5, $"moved {moved:F2} m");
            return $"held for {ut:F0} s, carried {moved:F0} m east by the planet's spin";
        }

        static string DecoupleConservation()
        {
            var b = Body;
            var sim = VesselSim.CreateFree(VesselDesign.Default(), b, new Vector3d(b.radius + 100_000, 0, 0), new Vector3d(0, 0, -2300),
                Quaternion.LookRotation(Vector3.forward, Vector3.right));
            sim.RecomputeMass();
            double m0 = sim.mass;
            Vector3d p0 = sim.vel * m0;
            sim.ActivateNextStage();
            var spawned = sim.ActivateNextStage();
            Check(spawned.Count == 1, "expected one piece to break away");
            var deb = spawned[0];
            Check(sim.parts.Count == 5 && deb.parts.Count == 6, $"split {sim.parts.Count}/{deb.parts.Count}");
            Check(Math.Abs(sim.mass + deb.mass - m0) < 1e-9, "mass changed");
            double dp = (sim.vel * sim.mass + deb.vel * deb.mass - p0).magnitude;
            Check(dp < 1e-6, $"momentum changed by {dp:E2}");
            return $"upper {sim.mass:F2} t + lower {deb.mass:F2} t, momentum error {dp:E1}";
        }

        static string CrashWithoutChute()
        {
            var b = Body;
            Vector3d up = CelestialBody.DirectionFromLatLon(10, 40);
            var sim = VesselSim.CreateFree(D("pod", "pod.wren"), b, up * (b.radius + 2000), b.SurfaceVelocity(up * (b.radius + 2000)),
                Quaternion.FromToRotation(Vector3.up, up.ToFloat()));
            double ut = 0;
            for (int i = 0; i < 20_000 && !sim.destroyed && !sim.landed; i++) { sim.Step(0.02, ut); ut += 0.02; }
            Check(sim.destroyed, sim.landed ? "capsule landed safely without a parachute" : "capsule never hit the ground");
            return sim.destroyReason;
        }

        static string FullMission()
        {
            var b = Body;
            var sim = VesselSim.CreateOnPad(VesselDesign.Default(), b, 0);
            var ap = new FlightAutopilot();
            double ut = 0, maxQ = 0, maxG = 0, ascentPeak = 0;
            var log = new StringBuilder();
            int steps = 0;
            while (steps++ < 200_000 && !sim.destroyed && ap.phase != FlightAutopilot.Phase.Done)
            {
                if (ap.Update(sim, ut)) { sim.ActivateNextStage(); log.Append($" stage{sim.nextStage}@{ut:F0}s/{sim.Altitude / 1000:F1}km"); }
                sim.Step(0.02, ut);
                ut += 0.02;
                maxQ = Math.Max(maxQ, sim.dynPressure);
                maxG = Math.Max(maxG, sim.gForce);
                foreach (var p in sim.parts) ascentPeak = Math.Max(ascentPeak, p.temp);
            }
            Check(!sim.destroyed, "destroyed during ascent: " + sim.destroyReason);
            Check(ap.phase == FlightAutopilot.Phase.Done, $"autopilot stuck in {ap.phase}");
            var o = Orbit.FromState(sim.pos, sim.vel, b.mu, ut);
            double apAlt = o.Apoapsis - b.radius, peAlt = o.Periapsis - b.radius;
            Check(peAlt > b.atmosphereHeight, $"not in orbit: Ap {apAlt / 1000:F1} km, Pe {peAlt / 1000:F1} km");
            double dvLeft = 0; foreach (var r in sim.StageReports()) dvLeft += r.dvVac;
            string ascent = $"orbit {apAlt / 1000:F1} x {peAlt / 1000:F1} km at T+{ut:F0}s, max Q {maxQ / 1000:F1} kPa, max {maxG:F1} g, hottest part {ascentPeak:F0} K, {dvLeft:F0} m/s left;{log}";

            sim.steerDirection = null;
            sim.sas = SasMode.Retrograde;
            sim.throttle = 0;
            for (int i = 0; i < 1500; i++) { sim.Step(0.02, ut); ut += 0.02; }
            sim.throttle = 1;
            int guard = 0;
            while (guard++ < 30_000)
            {
                var oo = Orbit.FromState(sim.pos, sim.vel, b.mu, ut);
                if (oo.Periapsis - b.radius < 25_000 || sim.PotentialThrust() <= 0) break;
                sim.Step(0.02, ut); ut += 0.02;
            }
            sim.throttle = 0;
            sim.ActivateNextStage();
            sim.ActivateNextStage();
            Check(sim.parts.Count == 2, $"expected capsule + chute, have {sim.parts.Count} parts");

            double maxQr = 0, touchdown = 0, peakPod = 0;
            guard = 0;
            while (guard++ < 400_000 && !sim.destroyed && !sim.landed)
            {
                double before = sim.SurfaceRelVelocity.magnitude;
                sim.Step(0.02, ut); ut += 0.02;
                maxQr = Math.Max(maxQr, sim.dynPressure);
                foreach (var p in sim.parts) if (p.def.kind == PartKind.Command) peakPod = Math.Max(peakPod, p.temp);
                touchdown = before;
            }
            Check(!sim.destroyed, "capsule destroyed: " + sim.destroyReason);
            Check(sim.landed, $"capsule never landed (alt {sim.Altitude:F0} m)");
            return ascent + $"\n      LEO reentry: capsule peaked at {peakPod:F0} K (limit 1600), landed at {touchdown:F1} m/s, max Q {maxQr / 1000:F1} kPa";
        }

        static string WarpedAscent()
        {
            var b = Body;
            var sim = VesselSim.CreateOnPad(VesselDesign.Default(), b, 0);
            var ap = new FlightAutopilot();
            double ut = 0;
            int jumps = 0;
            for (int guard = 0; guard < 300_000 && !sim.destroyed && ap.phase != FlightAutopilot.Phase.Done; guard++)
            {
                double untilBurn = ap.TimeUntilBurn(sim, ut);
                if (sim.CanRailsWarp() && untilBurn > 0.02)
                {
                    sim.RailsAdvance(Math.Min(16.0, Math.Max(0.02, untilBurn)), ut, out double used);
                    ut += used;
                    jumps++;
                    if (ap.Update(sim, ut)) sim.ActivateNextStage();
                    continue;
                }
                if (ap.Update(sim, ut)) sim.ActivateNextStage();
                sim.Step(0.02, ut);
                ut += 0.02;
            }
            Check(!sim.destroyed, "destroyed: " + sim.destroyReason);
            Check(ap.phase == FlightAutopilot.Phase.Done, $"autopilot stuck in {ap.phase}");
            var o = Orbit.FromState(sim.pos, sim.vel, b.mu, ut);
            Check(o.Periapsis - b.radius > b.atmosphereHeight, $"not in orbit: Pe {(o.Periapsis - b.radius) / 1000:F1} km");
            return $"orbit {(o.Apoapsis - b.radius) / 1000:F1} x {(o.Periapsis - b.radius) / 1000:F1} km after {jumps} warp jumps";
        }

        // ------------------------------------------------------------------ moon and patched conics

        static string MoonGeometry()
        {
            var sys = CelestialBody.CreateSystem();
            var moon = sys[1];
            double soi = moon.SoiRadius;
            Check(soi > 1_500_000 && soi < 3_000_000, $"SOI {soi / 1000:F0} km looks wrong");
            double t = 5000, h = 1;
            var num = (moon.PositionAt(t + h) - moon.PositionAt(t - h)) / (2 * h);
            double err = (num - moon.VelocityAt(t)).magnitude;
            Check(err < 1e-3, $"velocity mismatch {err:E2}");
            return $"Tal: SOI {soi / 1000:F0} km, orbit period {moon.OrbitPeriod / 3600:F1} h, orbital speed {moon.VelocityAt(0).magnitude:F0} m/s, surface g {moon.SurfaceGravity:F2}";
        }

        static string SoiContinuity()
        {
            var sys = CelestialBody.CreateSystem();
            var home = sys[0]; var moon = sys[1];
            double t0 = 1000;
            Vector3d mp = moon.PositionAt(t0), mv = moon.VelocityAt(t0);
            Vector3d start = mp + new Vector3d(moon.SoiRadius + 2000, 0, 0);
            var sim = VesselSim.CreateFree(D("probe", "probe.spark"), home, start, mv + new Vector3d(-800, 0, 0), Quaternion.identity);
            sim.sas = SasMode.Off;
            double ut = t0;
            Vector3d absBefore = Vector3d.zero, velBefore = Vector3d.zero;
            for (int i = 0; i < 10_000 && sim.body == home; i++)
            {
                absBefore = sim.AbsolutePosition(ut); velBefore = sim.AbsoluteVelocity(ut);
                sim.Step(0.02, ut); ut += 0.02;
            }
            Check(sim.body == moon, "never entered Tal's SOI");
            double dPos = (sim.AbsolutePosition(ut) - absBefore).magnitude, dVel = (sim.AbsoluteVelocity(ut) - velBefore).magnitude;
            Check(dPos < 50 && dVel < 0.5, $"jump at SOI change: {dPos:F1} m, {dVel:F3} m/s");
            return $"switched to Tal at {sim.pos.magnitude / 1000:F0} km from its centre; step change {dPos:F2} m (one step of motion), {dVel:F4} m/s";
        }

        static string MoonTransfer()
        {
            var sys = CelestialBody.CreateSystem();
            var home = sys[0]; var moon = sys[1];
            double rr = home.radius + 80_000, vc = Math.Sqrt(home.mu / rr);
            Vector3d r = new Vector3d(rr, 0, 0), v = new Vector3d(0, 0, -vc); // eastward, equatorial
            double ut = 0;
            var t0 = DateTime.Now;
            Check(Patcher.FindTransfer(home, r, v, ut, moon, 50_000, out double burnUt, out double dv, out double predPe), "no transfer found");
            double searchSec = (DateTime.Now - t0).TotalSeconds;

            var sim = VesselSim.CreateFree(D("probe", "probe.spark"), home, r, v, Quaternion.identity);
            sim.sas = SasMode.Off;
            sim.RailsAdvance(burnUt - ut, ut, out double used);
            ut += used;
            sim.vel = sim.vel + sim.vel.normalized * dv; // impulsive burn
            sim.rails = null;
            var plan = Patcher.Predict(home, sim.pos, sim.vel, ut, 3);
            Check(plan.Count >= 2 && plan[1].body == moon, "plan has no Tal encounter");

            double minAlt = double.PositiveInfinity, entered = double.NaN, left = double.NaN;
            for (int i = 0; i < 200_000; i++)
            {
                var ev = sim.RailsAdvance(300, ut, out used);
                ut += used;
                if (sim.body == moon)
                {
                    if (double.IsNaN(entered)) entered = ut;
                    minAlt = Math.Min(minAlt, sim.Altitude);
                    // sample finely near periapsis
                    var o = Orbit.FromState(sim.pos, sim.vel, moon.mu, ut);
                    minAlt = Math.Min(minAlt, o.Periapsis - moon.radius);
                }
                else if (!double.IsNaN(entered)) { left = ut; break; }
                if (ev == RailsEvent.NearSurface) break;
            }
            Check(!double.IsNaN(entered), "never reached Tal's SOI");
            Check(Math.Abs(minAlt - predPe) < 2000, $"periapsis {minAlt / 1000:F1} km vs predicted {predPe / 1000:F1} km");
            return $"burn {dv:F0} m/s (search {searchSec:F1}s), entered Tal SOI after {(entered - burnUt) / 3600:F1} h, " +
                   $"periapsis {minAlt / 1000:F1} km (predicted {predPe / 1000:F1}), " + (double.IsNaN(left) ? "impact/stayed" : $"left SOI {(left - entered) / 3600:F1} h later");
        }

        static string ManeuverBurn()
        {
            var b = Body;
            double rr = b.radius + 100_000, vc = Math.Sqrt(b.mu / rr);
            var sim = VesselSim.CreateFree(D("transfer", "chute.s", "pod.wren", "dec.s", "tank.s4", "eng.ember"), b,
                new Vector3d(rr, 0, 0), new Vector3d(0, 0, -vc), Quaternion.LookRotation(Vector3.right, Vector3.up));
            sim.ActivateNextStage(); // light the Ember
            sim.throttle = 0;
            double ut = 0;
            var traj = Patcher.Predict(b, sim.pos, sim.vel, ut);
            var node = new ManeuverNode { ut = 300, prograde = 300 };
            node.Update(traj);
            double plannedAp = node.planned[0].orbit.Apoapsis - b.radius;
            sim.sas = SasMode.Maneuver;
            bool started = false;
            for (int i = 0; i < 40_000; i++)
            {
                double burn = ManeuverNode.BurnTime(sim, node.DeltaV);
                bool window = ut > node.ut - burn / 2;
                var rem = node.Remaining(sim, ut, window);
                sim.maneuverDir = rem.normalized;
                if (window)
                {
                    started = true;
                    if (rem.magnitude < 0.4 || Vector3d.Dot(rem, node.dvWorld) < 0) { sim.throttle = 0; break; }
                    // only burn when pointed close to the target direction
                    sim.throttle = Vector3d.Dot(sim.Axis, rem.normalized) > 0.98 ? Math.Min(1, rem.magnitude / 20 + 0.05) : 0;
                }
                sim.Step(0.02, ut); ut += 0.02;
            }
            Check(started, "burn never started");
            var o = Orbit.FromState(sim.pos, sim.vel, b.mu, ut);
            double ap = o.Apoapsis - b.radius;
            Check(Math.Abs(ap - plannedAp) / plannedAp < 0.02, $"Ap {ap / 1000:F1} km vs planned {plannedAp / 1000:F1} km");
            return $"planned Ap {plannedAp / 1000:F1} km, flew {ap / 1000:F1} km (error {Math.Abs(ap - plannedAp) / plannedAp * 100:F2}%)";
        }

        // ------------------------------------------------------------------ heat

        /// <summary>Reentry from an orbit with the given apoapsis, aimed at a 30 km periapsis, starting at the top of the air.</summary>
        static (bool destroyed, bool landed, double peak, double ablatorLeft, string why) Reentry(VesselDesign d, double apAlt)
        {
            var b = Body;
            double rr = b.radius + 76_000, ra = b.radius + apAlt, rp = b.radius + 30_000;
            double a = (ra + rp) / 2, e = (ra - rp) / (ra + rp);
            double speed = Math.Sqrt(b.mu * (2 / rr - 1 / a));
            double h = Math.Sqrt(b.mu * a * (1 - e * e));
            double cosG = Math.Min(1, h / (rr * speed)), sinG = Math.Sqrt(1 - cosG * cosG);
            Vector3d pos = new Vector3d(rr, 0, 0);
            Vector3d vel = new Vector3d(-speed * sinG, 0, -speed * cosG); // inbound
            var sim = VesselSim.CreateFree(d, b, pos, vel, Quaternion.LookRotation(Vector3.up, Vector3.forward));
            sim.sas = SasMode.Retrograde;
            while (sim.HasStagesLeft) sim.ActivateNextStage(); // arm the parachute
            double ut = 0, peak = 0;
            for (int i = 0; i < 400_000 && !sim.destroyed && !sim.landed; i++)
            {
                sim.Step(0.02, ut); ut += 0.02;
                foreach (var p in sim.parts) if (p.def.kind == PartKind.Command) peak = Math.Max(peak, p.temp);
            }
            double abl = 0; foreach (var p in sim.parts) abl += p.ablator;
            return (sim.destroyed, sim.landed, peak, abl, sim.destroyReason);
        }

        static void HeatSweep()
        {
            double keep = Thermal.HeatK;
            Out.AppendLine("HEAT SWEEP (peak capsule K / outcome):");
            foreach (double k in new[] { 0.0008, 0.0012, 0.0016, 0.002, 0.0025, 0.003, 0.004 })
            {
                Thermal.HeatK = k;
                var leo = Reentry(D("leo", "chute.s", "pod.wren"), 80_000);
                var bare = Reentry(D("bare", "chute.s", "pod.wren"), 12_000_000);
                var sh = Reentry(D("shield", "chute.s", "pod.wren", "shield.s"), 12_000_000);
                string O(bool d, bool l) => d ? "BURNED" : (l ? "landed" : "?");
                Out.AppendLine($"  K={k:0.0000}  LEO bare {leo.peak,5:0} {O(leo.destroyed, leo.landed)} | moon bare {bare.peak,5:0} {O(bare.destroyed, bare.landed)} | moon shield {sh.peak,5:0} {O(sh.destroyed, sh.landed)} abl left {sh.ablatorLeft * 1000:0} kg");
            }
            Thermal.HeatK = keep;
        }

        static string FastReentry()
        {
            const double moonReturn = 3150; // m/s at the top of the atmosphere coming back from Tal
            var bare = Reentry(D("bare", "chute.s", "pod.wren"), 12_000_000);
            var shielded = Reentry(D("shield", "chute.s", "pod.wren", "shield.s"), 12_000_000);
            var leo = Reentry(D("leo", "chute.s", "pod.wren"), 80_000);
            Check(leo.landed && !leo.destroyed, $"bare capsule should survive an orbital-speed reentry: {leo.why}");
            Check(bare.destroyed, $"bare capsule survived a moon-return reentry (peak {bare.peak:F0} K) - heating too weak");
            Check(shielded.landed && !shielded.destroyed, $"heat shield capsule failed: {shielded.why}");
            return $"orbital speed, no shield: peak {leo.peak:F0} K, landed. Moon return, no shield: {bare.why}. " +
                   $"Moon return with shield: capsule peak {shielded.peak:F0} K, {shielded.ablatorLeft * 1000:F0} kg ablator left, landed";
        }
    }
}
