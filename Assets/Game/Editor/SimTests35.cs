using System;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 35 tests: flags per trip, parts that vary round their ratings, the lessons, swimming.</summary>
    public static partial class SimTests
    {
        static void RunMilestone35()
        {
            Run("Flags: an Ardling carries 3 each time they go out; the fourth is refused, with why", FlagLimitTest);
            Run("Parts vary: each one's limits are its rating x 0.93-1.13 (a 150 part gives way between about 140 and 170), kept in saves; Easy is half as tough again", ToughnessTest);
            Run("Lessons: orbit, landing on Tal and docking, each a run of steps that check the flight and end with a well done", LessonsTest);
            Run("The sea: an astronaut in the water swims slower than they walk and can't jump out", SwimTest);
        }

        static string FlagLimitTest()
        {
            var u = CareerUniverse();
            var tal = System4.First(b => b.name == "Tal");
            var eng = u.career.roster.First();
            var lander = u.Add(VesselSim.CreateLanded(Hopper(), tal, CelestialBody.DirectionFromLatLon(5, 10), 0));
            Seat(lander, eng);
            var eva = u.Add(lander.GoEva(eng.name, 0, out string why));
            Check(eva != null && eva.FlagsLeft == 3, "out with 3 flags: " + why);
            for (int i = 0; i < 3; i++) Check(eva.PlantFlag(1 + i, out why) != null, $"flag {i + 1}: {why}");
            Check(eva.FlagsLeft == 0 && eva.PlantFlag(5, out why) == null && why.Contains("No flags left"), "the fourth is refused: " + why);
            var back = VesselSim.FromSave(JsonUtility.FromJson<VesselSave>(JsonUtility.ToJson(eva.ToSave())), System4, 5);
            Check(back.FlagsLeft == 0, "flags used are kept in a save");
            return why;
        }

        static string ToughnessTest()
        {
            bool was = VesselSim.VaryToughness;
            var words = Words.Level;
            try
            {
                VesselSim.VaryToughness = true;
                Words.Level = WordLevel.Standard;
                float lo = 9, hi = 0;
                for (int k = 0; k < 40; k++)
                {
                    var v = VesselSim.CreateLanded(Hopper(), System4[0], CelestialBody.DirectionFromLatLon(5, 10), 0);
                    foreach (var p in v.parts) { lo = Mathf.Min(lo, p.Toughness); hi = Mathf.Max(hi, p.Toughness); }
                }
                Check(lo >= 0.93f - 1e-4f && hi <= 1.13f + 1e-4f && hi - lo > 0.12f, $"toughness from {lo:0.000} to {hi:0.000}");
                var one = VesselSim.CreateLanded(Hopper(), System4[0], CelestialBody.DirectionFromLatLon(5, 10), 0);
                var pod = one.parts.First(p => p.def.id == "pod.wren");
                Check(Mathf.Abs(pod.CrashLimit - pod.def.crashTolerance * pod.Toughness) < 1e-4f && Math.Abs(pod.TempLimit - pod.def.maxTemp * pod.Toughness) < 1e-3, "limits follow the toughness");
                var back = VesselSim.FromSave(JsonUtility.FromJson<VesselSave>(JsonUtility.ToJson(one.ToSave())), System4, 0);
                Check(Mathf.Abs(back.parts.First(p => p.def.id == "pod.wren").Toughness - pod.Toughness) < 1e-5f, "the toughness is kept in a save");
                float std = pod.Toughness;
                Words.Level = WordLevel.Easy;
                Check(Mathf.Abs(pod.Toughness - std * 1.5f) < 1e-4f, "Easy: half as tough again");
                VesselSim.VaryToughness = false;
                var fixedOne = VesselSim.CreateLanded(Hopper(), System4[0], CelestialBody.DirectionFromLatLon(5, 10), 0);
                Words.Level = WordLevel.Standard;
                Check(fixedOne.parts.All(p => Mathf.Abs(p.Toughness - 1f) < 1e-6f), "with variation off (tests), every part is exactly its rating");
                return $"{lo:0.000}-{hi:0.000}";
            }
            finally { VesselSim.VaryToughness = was; Words.Level = words; }
        }

        static string LessonsTest()
        {
            Check(Lessons.All.Length >= 3, "three lessons or more");
            foreach (var l in Lessons.All)
            {
                Check(!string.IsNullOrWhiteSpace(l.name) && !string.IsNullOrWhiteSpace(l.blurb) && l.steps.Length >= 4, $"{l.name}: a name, a blurb and steps");
                Check(l.steps.Last().done == null && l.steps.Take(l.steps.Length - 1).Count(s => s.done != null) >= 3, $"{l.name}: steps that check the flight, then a well done");
                Check(l.steps.All(s => !string.IsNullOrWhiteSpace(s.title) && s.text.Length > 20), $"{l.name}: every step explains itself");
            }
            Check(Lessons.All.Any(l => l.name.Contains("orbit")) && Lessons.All.Any(l => l.name.Contains("Tal")) && Lessons.All.Any(l => l.name.Contains("Dock")), "orbit, Tal and docking");
            return string.Join(", ", Lessons.All.Select(l => $"{l.name} ({l.steps.Length})"));
        }

        static string SwimTest()
        {
            var u = CareerUniverse();
            var home = System4[0];
            var sea = CelestialBody.DirectionFromLatLon(5, 30);
            Check(Terrain.IsWater(home, sea), "the test spot is sea");
            var eng = u.career.roster.First();
            var boat = u.Add(VesselSim.CreateLanded(Hopper(), home, sea, 0));
            Seat(boat, eng);
            Check(boat.InWater, "a ship down on the sea is in the water");
            var eva = u.Add(boat.GoEva(eng.name, 0, out string why));
            Check(eva != null && eva.landed && eva.InWater, "the astronaut's in the water: " + why);
            Vector3d a = home.InertialToFixed(eva.pos, 0);   // along the ground (the world turns under them)
            eva.inputPitch = 1;
            double t = 0;
            StepFor(eva, 5, ref t);
            double swum = (home.InertialToFixed(eva.pos, t) - a).magnitude;
            Check(swum < VesselSim.EvaWalkSpeed * 5 * 0.8 && swum > 1, $"swims {swum:0.0} m in 5 s (walking would be {VesselSim.EvaWalkSpeed * 5:0.0})");
            eva.inputPitch = 0;
            eva.evaJump = true;
            StepFor(eva, 0.5, ref t);
            Check(eva.landed, "no jumping out of the sea");
            return $"swam {swum:0.0} m in 5 s";
        }
    }
}
