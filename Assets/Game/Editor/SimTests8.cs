using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 8b tests: terrain with real height.</summary>
    public static partial class SimTests
    {
        static void RunMilestone8()
        {
            Run("Terrain: Arden's seas flat, mountains up to ~3.5 km, the space center flat; Tal hilly with craters; all smooth", TerrainShape);
            Run("Terrain: landings touch down on the real ground, high on a mountain or low in a crater", TerrainLanding);
            Run("Terrain: astronauts walk up and down slopes on the ground; rockets lift off uneven ground", TerrainWalkAndLiftOff);
            Run("Terrain: a vessel saved landed at the old flat height loads standing on the ground", TerrainOldSaves);
            Run("Sound: every clip is made, loud enough but never clipping; loops join without a click; one-shots die away", SoundClips);
        }

        static string SoundClips()
        {
            double Rms(float[] s, int from, int to) { double a = 0; for (int i = from; i < to; i++) a += s[i] * s[i]; return Math.Sqrt(a / Math.Max(1, to - from)); }
            var loops = new (string name, float[] s)[] { ("rumble", SoundSynth.Rumble()), ("jet", SoundSynth.Jet()), ("wind", SoundSynth.Wind()), ("hiss", SoundSynth.Hiss()) };
            var shots = new (string name, float[] s)[] { ("clunk", SoundSynth.Clunk()), ("clank", SoundSynth.Clank()), ("boom", SoundSynth.Boom()), ("whoosh", SoundSynth.Whoosh()) };
            var notes = new List<string>();
            foreach (var (name, s) in loops.Concat(shots))
            {
                float peak = s.Max(x => Math.Abs(x));
                Check(s.Length > SoundSynth.Rate / 2 && peak > 0.3f && peak <= 1.0001f, $"{name}: {s.Length} samples, peak {peak:0.00}");
            }
            foreach (var (name, s) in loops)
            {
                // the jump from the last sample back to the first is no bigger than ordinary sample-to-sample steps
                double step = 0;
                for (int i = 1; i < s.Length; i++) step = Math.Max(step, Math.Abs(s[i] - s[i - 1]));
                double seam = Math.Abs(s[0] - s[s.Length - 1]);
                Check(seam <= step * 1.05, $"{name}: loop seam {seam:0.000} vs largest step {step:0.000}");
                notes.Add($"{name} {s.Length / (float)SoundSynth.Rate:0.0}s");
            }
            foreach (var (name, s) in shots)
            {
                int n = s.Length;
                double loudest = 0;
                for (int w = 0; w < 10; w++) loudest = Math.Max(loudest, Rms(s, n * w / 10, n * (w + 1) / 10));
                double tail = Rms(s, n * 19 / 20, n);
                Check(tail < loudest * 0.3, $"{name}: tail {tail:0.000} vs loudest {loudest:0.000}");
            }
            return "loops " + string.Join(", ", notes) + "; 4 one-shots fade out";
        }

        static IEnumerable<Vector3d> Directions(int n, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                Vector3d d;
                do d = new Vector3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1); while (d.sqrMagnitude > 1 || d.sqrMagnitude < 1e-3);
                yield return d.normalized;
            }
        }

        /// <summary>The highest (or lowest) spot found among random directions on a body.</summary>
        static Vector3d Extreme(CelestialBody b, bool high, out double h)
        {
            Vector3d best = Vector3d.up;
            h = high ? double.MinValue : double.MaxValue;
            foreach (var d in Directions(3000, 3))
            {
                double x = Terrain.Height(b, d);
                if (high ? x > h : x < h) { h = x; best = d; }
            }
            return best;
        }

        static string TerrainShape()
        {
            var home = System4[0];
            double aMax = 0, tMin = double.MaxValue, tMax = double.MinValue;
            int sea = 0, seaFlat = 0, n = 0;
            foreach (var d in Directions(3000, 1))
            {
                n++;
                double ha = Terrain.Height(home, d), ht = Terrain.Height(Tal, d);
                aMax = Math.Max(aMax, ha);
                tMin = Math.Min(tMin, ht);
                tMax = Math.Max(tMax, ht);
                float h0 = Terrain.ArdenField(home, d.ToFloat());
                if (h0 < 0) { sea++; if (ha == 0) seaFlat++; }
            }
            Check(sea > 300 && seaFlat == sea, $"{sea - seaFlat} of {sea} sea samples weren't at sea level");
            Check(aMax > 1500 && aMax <= Terrain.MaxHeight(home), $"Arden's highest sample {aMax:0} m");
            Check(tMin > -400 && tMax < Terrain.MaxHeight(Tal) && tMax - tMin > 1500, $"Tal ranges {tMin:0} to {tMax:0} m");
            // The space center is flat ground (hills begin about 3.5 km out since milestone 23)
            foreach (var (e, nn) in new[] { (0.0, 0.0), (500.0, 300.0), (-2000.0, 1500.0), (2500.0, -1500.0) })
                Check(Math.Abs(LaunchSite.SurfaceHeight(home, LaunchSite.DirectionAt(home, e, nn)) - (Math.Abs(e) <= 12 && Math.Abs(nn) <= 12 ? LaunchSite.PadHeight : 0)) < 1e-9, $"ground {e},{nn} m from the pad isn't flat");
            // Smooth at walking scale: one metre apart never differs by more than a slope of 1:1
            double steepest = 0;
            foreach (var d in Directions(400, 2))
            {
                Vector3d t = Vector3d.Cross(d, new Vector3d(0, 1, 0)).normalized;
                double h1 = Terrain.Height(Tal, d), h2 = Terrain.Height(Tal, (d + t * (1.0 / Tal.radius)).normalized);
                steepest = Math.Max(steepest, Math.Abs(h2 - h1));
            }
            Check(steepest < 1.0, $"a 1 m step on Tal changed height by {steepest:0.00} m");
            // Craters: plenty of local dips on Tal
            int dips = 0;
            foreach (var d in Directions(600, 4))
            {
                Vector3d t1 = Vector3d.Cross(d, new Vector3d(0, 1, 0)).normalized, t2 = Vector3d.Cross(d, t1);
                double step = 1500 / Tal.radius, h = Terrain.Height(Tal, d);
                if (new[] { t1, -t1, t2, -t2 }.All(t => Terrain.Height(Tal, (d + t * step).normalized) > h + 5)) dips++;
            }
            Check(dips > 6, $"only {dips} crater-like dips found on Tal");
            return $"Arden up to {aMax:0} m, seas and the space center flat; Tal {tMin:0} to {tMax:0} m with {dips} dips in 600 samples; max 1 m slope {steepest:0.00}";
        }

        static string TerrainLanding()
        {
            var home = System4[0];
            string Drop(CelestialBody b, Vector3d dir, out double ground)
            {
                ground = Terrain.Height(b, dir);
                var probe = VesselSim.CreateLanded(Hopper(), b, dir, 0);
                foreach (var p in probe.parts) if (p.def.kind == PartKind.Leg) { p.legsDeployed = true; p.legDeploy = 1; }
                var v = VesselSim.CreateFree(Hopper(), b, probe.pos + dir * 3, probe.vel - dir * 2, probe.rot); // touches down at ~8 m/s on Arden
                foreach (var p in v.parts) if (p.def.kind == PartKind.Leg) { p.legsDeployed = true; p.legDeploy = 1; }
                v.disableRails = true;
                double t = 0;
                for (int i = 0; i < 1500 && !v.landed && !v.destroyed; i++) { v.Step(0.01, t); t += 0.01; }
                Check(v.landed && !v.destroyed, $"didn't land on {b.name} at {ground:0} m: {v.destroyReason}");
                double radar = v.RadarAltitude(t);
                Check(Math.Abs(radar) < 0.1, $"landed {radar:0.00} m off the ground at {ground:0} m");
                return $"{ground:0} m";
            }
            var peak = Extreme(home, true, out _);
            string a = Drop(home, peak, out double hp);
            Check(hp > 1500, $"the mountain was only {hp:0} m");
            var pit = Extreme(Tal, false, out _);
            string c = Drop(Tal, pit, out _);
            return $"touched down on an Arden peak at {a} and in a Tal low at {c}";
        }

        static string TerrainWalkAndLiftOff()
        {
            var u = CareerUniverse();
            var eng = u.career.roster.First(m => m.role == CrewRole.Engineer);
            // A slope on Tal: the steepest of a few hundred spots
            Vector3d best = Vector3d.up;
            double grade = 0;
            foreach (var d in Directions(500, 9))
            {
                Vector3d tan = Vector3d.Cross(d, new Vector3d(0, 1, 0)).normalized;
                double g = Math.Abs(Terrain.Height(Tal, (d + tan * (20 / Tal.radius)).normalized) - Terrain.Height(Tal, d)) / 20;
                if (g > grade) { grade = g; best = d; }
            }
            var v = u.Add(VesselSim.CreateLanded(Hopper(), Tal, best, 0));
            Seat(v, eng);
            var eva = u.Add(v.GoEva(eng.name, 0, out string why) ?? throw new Exception(why));
            double t0 = 0, h0 = eva.Altitude;
            double minR = double.MaxValue, maxR = double.MinValue;
            eva.inputPitch = 1;
            for (int i = 0; i < 400; i++)
            {
                eva.Step(0.02, t0); t0 += 0.02;
                if (i % 40 == 0) { eva.inputYaw = i % 80 == 0 ? 0.6f : -0.6f; }
                double r = eva.RadarAltitude(t0);
                minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
            }
            double climbed = eva.Altitude - h0;
            Check(eva.landed && minR > -0.2 && maxR < 0.2, $"walking: landed={eva.landed}, radar {minR:0.00}..{maxR:0.00} m");
            Check(Math.Abs(climbed) > 0.2, $"walked 12 m over a {grade * 100:0}% slope but height changed only {climbed:0.00} m");

            // A rocket standing on Tal's uneven ground lifts off cleanly
            var r0 = VesselSim.CreateLanded(D("riser", "probe.spark", "tank.s2", "eng.wisp"), Tal, best, 0);
            r0.ActivateNextStage();
            r0.throttle = 1;
            double t = 0;
            for (int i = 0; i < 150; i++) { r0.Step(0.02, t); t += 0.02; }
            Check(!r0.landed && !r0.destroyed && r0.RadarAltitude(t) > 2, $"lift-off: landed={r0.landed}, radar {r0.RadarAltitude(t):0.0} m");
            return $"walked over a {grade * 100:0}% slope ({climbed:+0.0;-0.0} m) staying on the ground; a rocket climbed {r0.RadarAltitude(t):0} m off Tal in 3 s";
        }

        static string TerrainOldSaves()
        {
            var u = new Universe();
            var peak = Extreme(Tal, true, out double h);
            var v = u.Add(VesselSim.CreateLanded(Hopper(), Tal, peak, 0));
            string json = u.ToJson();
            // Pretend it was saved before terrain had height: landed at the bare radius
            var s = JsonUtility.FromJson<VesselSave>(JsonUtility.ToJson(v.ToSave()));
            s.landedFixedPos = s.landedFixedPos.normalized * (Tal.radius + 3);
            var loaded = VesselSim.FromSave(s, System4, 0);
            Check(Math.Abs(loaded.RadarAltitude(0)) < 0.1, $"old save loaded {loaded.RadarAltitude(0):0.0} m off the ground (ground at {h:0} m)");
            var u2 = new Universe();
            u2.LoadJson(json, System4);
            Check(Vector3d.Distance(u2.vessels[0].pos, v.pos) < 0.05, "a normal save moved when loaded");
            return $"an old save on a {h:0} m Tal peak came back standing on it";
        }
    }
}
