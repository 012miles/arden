using System;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 28 tests: the story, flight recordings for the replay, planetshine, landmark sighting range.</summary>
    public static partial class SimTests
    {
        static void RunMilestone28()
        {
            Run("Story: twenty missions in five chapters, done in order with mission control's words and pay; an old save skips ahead quietly; kept in the save", StoryMissions);
            Run("Replay recording: a sample every tenth of a second, smooth in between, a new copy of the ship when it changes, long flights thinned to the limit", ReplayRecording);
            Run("Planetshine: full Arden lights Tal's night side, a thin crescent hardly at all, nothing far from any world", Planetshine);
            Run("Hidden places: not spotted from the launch pad 53 km away; spotted flying over them", LandmarkSighting);
        }

        static string StoryMissions()
        {
            Check(Story.Missions.Length == 20, $"{Story.Missions.Length} story missions");
            Check(Story.Missions.Select(m => m.chapter).Distinct().Count() == Story.Chapters.Length, "missions don't cover every chapter");
            for (int i = 1; i < Story.Missions.Length; i++) Check(Story.Missions[i].chapter >= Story.Missions[i - 1].chapter, "chapters out of order");
            Check(Story.Missions.All(m => m.check != null && !string.IsNullOrEmpty(m.brief) && !string.IsNullOrEmpty(m.done) && !string.IsNullOrEmpty(m.goal)), "a mission is missing its words or check");
            // a new career: starts at the first mission, briefs it, completes it when the rocket flies
            var u = new Universe();
            u.career.Start(GameMode.Career);
            Story.Update(u);
            Check(u.career.storyStep == 0 && u.career.storyStarted, $"a new career starts at mission {u.career.storyStep}");
            double funds = u.career.funds;
            u.career.records |= Records.Launch;
            Story.Update(u);
            Check(u.career.storyStep == 1 && u.career.storyNews.Count == 1, "launching didn't finish the first mission");
            Check(Math.Abs(u.career.funds - funds - Story.Missions[0].funds) < 1e-6, "the first mission didn't pay");
            // one at a time, so each gets its message
            u.career.records |= Records.Space | Records.Orbit;
            Story.Update(u);
            Check(u.career.storyStep == 2, "two missions finished in one go");
            Story.Update(u);
            Check(u.career.storyStep == 3, "the third mission didn't follow");
            // an old save that's already been to Tal: skips ahead without a pile of messages
            var old = new Universe();
            old.career.Start(GameMode.Career);
            old.career.records |= Records.Launch | Records.Space | Records.Orbit | Records.CrewedOrbit | Records.TalFlyby | Records.TalOrbit;
            Story.Update(old);
            Check(old.career.storyStep == 6 && old.career.storyNews.Count == 0, $"an old save skipped to {old.career.storyStep} with {old.career.storyNews.Count} messages");
            // saved
            var u2 = new Universe();
            u2.career.Load(u.career.ToSave());
            Check(u2.career.storyStep == 3 && u2.career.storyStarted, "the story's place wasn't saved");
            return $"{Story.Missions.Length} missions; an old save starts at \"{Story.Missions[old.career.storyStep].title}\"";
        }

        static string ReplayRecording()
        {
            var sys = CelestialBody.CreateSystem();
            var b = sys[0];
            var d = VesselDesign.Stack("Drop", "chute.s", "pod.wren");
            Vector3d up = CelestialBody.DirectionFromLatLon(5, 5);
            Vector3d pos = up * (b.radius + 2000);
            var v = VesselSim.CreateFree(d, b, pos, b.SurfaceVelocity(pos), Quaternion.identity);
            v.hasLaunched = true;
            var rec = new FlightRecording();
            double t = 0;
            for (int i = 0; i < 300; i++)
            {
                if (i == 150) foreach (var p in v.parts) if (p.def.kind == PartKind.Parachute) p.chuteDeployed = true;
                v.Step(0.02, t); t += 0.02;
                rec.Sample(v, t, sys);
            }
            Check(rec.Count >= 55 && rec.Count <= 62, $"{rec.Count} samples in 6 s (expected about 60)");
            Check(rec.snaps.Count == 2, $"{rec.snaps.Count} copies of the ship (expected 2: before and after the chute)");
            var a = rec.frames[10]; var c = rec.frames[11];
            var mid = rec.At((a.ut + c.ut) / 2);
            Check((mid.pos - (a.pos + c.pos) * 0.5).magnitude < 0.01, "halfway between two samples isn't halfway");
            Check((rec.VelocityAt(a.ut) - (c.pos - a.pos) / (c.ut - a.ut)).magnitude < 1e-6, "velocity from the samples is off");
            // a long flight thins out but keeps its ends
            var longRec = new FlightRecording();
            for (int i = 0; i < FlightRecording.MaxFrames * 3; i++) longRec.Sample(v, i * 0.1, sys, true);
            Check(longRec.Count <= FlightRecording.MaxFrames && longRec.Count > FlightRecording.MaxFrames / 3, $"{longRec.Count} samples kept of {FlightRecording.MaxFrames * 3}");
            Check(longRec.StartUt == 0 && Math.Abs(longRec.EndUt - (FlightRecording.MaxFrames * 3 - 1) * 0.1) < 1e-6, "thinning lost the start or the end");
            return $"{rec.Count} samples, {rec.snaps.Count} ship copies; a long flight kept {longRec.Count}";
        }

        static string Planetshine()
        {
            var sys = CelestialBody.CreateSystem();
            var arden = sys.First(x => x.IsHomeWorld);
            var tal = sys.First(x => x.name == "Tal");
            // find when Tal is between the sun and Arden (full Arden from Tal) and when it's on the far side (new Arden)
            double full = 0, fullDot = 2, thin = 0, thinDot = -2;
            for (double t = 0; t < 40 * 86400; t += 1800)
            {
                Vector3d toArden = (arden.AbsolutePosition(t) - tal.AbsolutePosition(t)).normalized;
                double s = Vector3d.Dot(Sun.DirectionFrom(tal.AbsolutePosition(t), t), toArden);
                if (s < fullDot) { fullDot = s; full = t; }
                if (s > thinDot) { thinDot = s; thin = t; }
            }
            float Shine(double t)
            {
                Vector3d toArden = (arden.AbsolutePosition(t) - tal.AbsolutePosition(t)).normalized;
                Vector3d eye = tal.AbsolutePosition(t) + toArden * (tal.radius + 10);
                RenderEnv.UpdateShine(tal, eye, t, sys);
                return RenderEnv.ShineStrength;
            }
            float sFull = Shine(full), sThin = Shine(thin);
            Check(sFull > 0.05f, $"full Arden only lights Tal at {sFull:0.000} of sunlight");
            Check(sThin < sFull * 0.2f, $"a thin Arden still lights Tal at {sThin:0.000} (full {sFull:0.000})");
            RenderEnv.UpdateShine(arden, new Vector3d(1e12, 0, 0), 0, sys);
            float sFar = RenderEnv.ShineStrength;
            Check(sFar < 1e-4f, "planetshine far from any world");
            RenderEnv.UpdateShine(null, Vector3d.zero, 0, sys);
            return $"full Arden {sFull:0.000}, crescent {sThin:0.000} of sunlight";
        }

        static string LandmarkSighting()
        {
            var u = new Universe();
            u.career.Start(GameMode.Career);
            var b = Body;
            var stones = Landmarks.On(b).First(l => l.kind == LandmarkKind.Stones);
            var rocket = VesselSim.CreateOnPad(VesselDesign.Default(), b, 0, 0);
            rocket.hasLaunched = true;
            Landmarks.Update(u, rocket, 0);
            Check(!u.career.spotted.Contains(stones.id), "the Old Stones were spotted from the pad");
            var plane = VesselSim.CreateFree(VesselDesign.Default(), b, stones.dir * (b.radius + 8000), Vector3d.zero, Quaternion.identity);
            plane.hasLaunched = true;
            Landmarks.Update(u, plane, 0);
            Check(u.career.spotted.Contains(stones.id), "not spotted flying 8 km over them");
            return "pad: not seen; overhead: seen";
        }
    }
}
