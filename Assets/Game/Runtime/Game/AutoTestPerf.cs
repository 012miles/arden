using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 24: how fast the busiest scenes draw at each graphics setting, with vsync off. Logs the average and
        /// worst-5% frame times, the CPU and GPU time when the driver reports them, draw calls and triangles.
        /// </summary>
        IEnumerator PerfTour()
        {
            var root = GameRoot.I;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            root.universe.career.Start(GameMode.Career);
            root.universe.career.guide = Guide.Closed;
            Log($"hardware: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB), {SystemInfo.processorType} x{SystemInfo.processorCount}, {SystemInfo.systemMemorySize} MB RAM, {Screen.width}x{Screen.height}");
            root.ShowMenu();
            float tb = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - tb < 120f) yield return null;

            var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            var timings = new FrameTiming[1];

            var scenes = new List<(string name, Func<IEnumerator> setup)>
            {
                ("title screen", () => { root.ShowMenu(); return null; }),
                ("builder (busy hall)", () => { root.EnterHangar(); root.hangar.SetView(35f, 14f, 120f, 18f); return null; }),
                ("on the pad", () => { root.Launch(VesselDesign.Default()); return null; }),
                ("space centre", () => { root.flight.SetCamera(180f, 16f, 150f); return null; }),
                ("low over the hills", () => FlyAt(root, 3_000, 250)),
                ("orbit", () => FlyAt(root, 100_000, 0)),
                ("map", () => { root.flight.SetMapView(root.home, 4_000_000f); return null; }),
            };
            int was = RenderEnv.Quality;
            var summary = new List<string>();
            foreach (int q in new[] { 2, 1, 0 })
            {
                RenderEnv.ApplyQuality(q);
                foreach (var (name, setup) in scenes)
                {
                    if (name == "map") root.flight.mapView = true; else if (root.flight != null) root.flight.mapView = false;
                    var e = setup();
                    if (e != null) yield return e;
                    yield return new WaitForSecondsRealtime(2.5f);
                    var dts = new List<float>();
                    double cpu = 0, gpu = 0; int nTiming = 0;
                    long dc = 0, bt = 0, tr = 0, sp = 0; int n = 0;
                    float t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 3f)
                    {
                        yield return null;
                        dts.Add(Time.unscaledDeltaTime * 1000f);
                        FrameTimingManager.CaptureFrameTimings();
                        if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0 && timings[0].gpuFrameTime < 500) { cpu += timings[0].cpuFrameTime; gpu += timings[0].gpuFrameTime; nTiming++; }
                        if (draws.Valid) { dc += draws.LastValue; bt += batches.LastValue; tr += tris.LastValue; sp += setPass.LastValue; n++; }
                    }
                    dts.Sort();
                    float avg = dts.Average(), worst = dts[Mathf.Min(dts.Count - 1, (int)(dts.Count * 0.95f))];
                    string gpuText = nTiming > 0 ? $"  cpu {cpu / nTiming:0.0} ms  gpu {gpu / nTiming:0.0} ms" : "";
                    string drawText = n > 0 ? $"  draws {dc / n}  batches {bt / n}  setpass {sp / n}  tris {tr / n / 1000}k" : "";
                    Log($"perf {new[] { "Low", "Medium", "High" }[q]} {name}: {1000f / avg:0} fps (avg {avg:0.0} ms, worst 5% {worst:0.0} ms){gpuText}{drawText}");
                    if (q == 0) summary.Add($"{name} {1000f / avg:0}");
                }
            }
            root.flight.mapView = false;
            RenderEnv.ApplyQuality(was);
            Log("perf summary (Low): " + string.Join(", ", summary));
            Log($"memory: {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB used, {UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576} MB reserved, graphics driver {UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576} MB");
            draws.Dispose(); batches.Dispose(); tris.Dispose(); setPass.Dispose();
        }

        /// <summary>A probe flying east at this height and speed, so the camera sees the ground going by (or the world from orbit).</summary>
        IEnumerator FlyAt(GameRoot root, double alt, double speed)
        {
            var home = root.home;
            var u = root.universe;
            Vector3d dir = home.FixedToInertial(CelestialBody.DirectionFromLatLon(-0.05, 0.6), u.ut);
            Vector3d east = CelestialBody.EastAt(dir);
            Vector3d pos = dir * (home.radius + alt);
            Vector3d vel = alt > 70_000 ? east * Math.Sqrt(home.mu / pos.magnitude) : home.SurfaceVelocity(pos) + east * speed;
            var v = VesselSim.CreateFree(VesselDesign.Stack(alt > 70_000 ? "Orbiter" : "Skimmer", "probe.spark", "battery.s", "tank.s1"), home, pos, vel, Quaternion.LookRotation(dir.ToFloat(), -east.ToFloat()));
            v.throttle = 0; v.hasLaunched = true; v.sas = SasMode.Stability;
            u.Add(v);
            root.Fly(v);
            yield return new WaitForSecondsRealtime(1.0f);
            root.flight.SetCamera(200f, 12f, 30f);
        }
    }
}
