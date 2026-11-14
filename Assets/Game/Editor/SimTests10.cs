using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 10a tests: named regions and science per region.</summary>
    public static partial class SimTests
    {
        static void RunMilestone10()
        {
            Run("Regions: every region of Arden, Tal and Vesk covers a fair share; they match the ground (seas at sea level, craters low, ridges high)", RegionMaps);
            Run("Regional science: the same experiment in two regions is two results; launch site and space results aren't regional; old results still count", RegionalScience);
            Run("Region contracts: offered for worlds you've landed on, completed only by science from that region; saved with the career", RegionContracts);
        }

        static Dictionary<int, List<Vector3d>> RegionSamples(CelestialBody b, int n, int seed)
        {
            var by = new Dictionary<int, List<Vector3d>>();
            foreach (var d in Directions(n, seed))
            {
                int i = Regions.IndexAt(b, d);
                if (!by.TryGetValue(i, out var l)) by[i] = l = new List<Vector3d>();
                l.Add(d);
            }
            return by;
        }

        static double MeanHeight(CelestialBody b, IEnumerable<Vector3d> dirs) => dirs.Average(d => Terrain.Height(b, d));

        static string RegionMaps()
        {
            var notes = new List<string>();
            foreach (var b in System4.Where(x => !x.isStar))
            {
                const int n = 12000;
                var by = RegionSamples(b, n, 101 + b.seed);
                var names = Regions.Names(b);
                var shares = new List<string>();
                for (int i = 0; i < names.Length; i++)
                {
                    double share = by.TryGetValue(i, out var l) ? l.Count / (double)n : 0;
                    // (an icy moon can be mostly plain: Rime's frost plains come out at ~72%)
                    Check(share >= 0.004 && share <= 0.75, $"{b.name} {names[i]} covers {share * 100:0.0}%");
                    shares.Add($"{names[i]} {share * 100:0}%");
                }
                notes.Add($"{b.name}: {string.Join(", ", shares)}");
                if (b.name == "Arden")
                {
                    string pad = Regions.At(b, LaunchSite.Direction);
                    Check(pad == "Grasslands" || pad == "Forests", $"the space center is in {pad}");
                    foreach (int sea in new[] { 0, 1 }) Check(by[sea].All(d => Terrain.Height(b, d) < 0.01), $"{names[sea]} isn't at sea level");
                    Check(MeanHeight(b, by[6]) > MeanHeight(b, by[5]) + 300 && MeanHeight(b, by[5]) > MeanHeight(b, by[3]), "mountains, highlands and grasslands aren't in height order");
                }
                if (b.name == "Tal")
                    Check(MeanHeight(b, by[3]) < MeanHeight(b, by[0]) - 300, $"Tal's great craters ({MeanHeight(b, by[3]):0} m) aren't below its highlands ({MeanHeight(b, by[0]):0} m)");
                if (b.name == "Vesk")
                    Check(MeanHeight(b, by[1]) > MeanHeight(b, by[0]) && MeanHeight(b, by[3]) < MeanHeight(b, by[0]), "Vesk's ridges aren't higher than its plains, or its craters lower");
            }
            return string.Join("; ", notes);
        }

        static Vector3d FindRegion(CelestialBody b, string name, int seed)
        {
            foreach (var d in Directions(5000, seed)) if (Regions.At(b, d) == name) return d;
            throw new Exception($"no {name} found on {b.name}");
        }

        static string RegionalScience()
        {
            var u = CareerUniverse();
            var c = u.career;
            var temp = Science.Get("temp");
            var dark = u.Add(VesselSim.CreateLanded(Hopper(), Tal, FindRegion(Tal, "Dark Plains", 5), 0));
            var high = u.Add(VesselSim.CreateLanded(Hopper(), Tal, FindRegion(Tal, "Bright Highlands", 6), 0));
            Check(Regions.Under(dark, 0) == "Dark Plains" && Regions.Under(high, 0) == "Bright Highlands", "landers aren't in the regions they were put in");
            float full = c.Remaining(Science.SubjectId("temp", Tal, Situation.Landed, "Dark Plains"), temp.baseValue * Science.Multiplier(Tal, Situation.Landed));
            string m1 = Lab.Run(c, dark, dark.parts[0], temp, 0);
            var d1 = Lab.Held(dark.parts[0], temp);
            Check(d1 != null && d1.subject == "temp@Tal:Landed:Dark Plains" && d1.title.Contains("Dark Plains"), $"result from the dark plains: {d1?.subject} / {d1?.title}");
            float got = c.Credit(d1, 1f, System4);
            Check(Math.Abs(got - full) < 0.01f, $"credited {got:0.0} of {full:0.0}");
            // The highlands are a separate result, still worth the full amount
            Check(Math.Abs(Lab.Preview(c, high, temp, 0) - full) < 0.01f, "the highlands lost value from a dark-plains result");
            Check(Lab.Preview(c, dark, temp, 0) < 0.01f, "the dark plains are still worth something after a full result");
            // Old saves' results (no region) still parse and count
            Career.ParseSubject("temp@Tal:Landed", System4, out var b0, out var s0);
            Career.ParseSubject("temp@Tal:Landed:Dark Plains", System4, out var b1, out var s1);
            Check(b0 == Tal && b1 == Tal && s0 == Situation.Landed && s1 == Situation.Landed, "subjects don't parse");
            // Not regional: the launch site, flying high, space
            Check(Science.SubjectId("temp", Tal, Situation.SpaceLow, "Dark Plains") == "temp@Tal:SpaceLow", "space results took a region");
            Check(Science.RegionOf(InOrbit(D("p", "probe.spark"), Tal, Tal.radius + 30_000, 0), Situation.SpaceLow, 0) == null, "space has a region");
            return $"{d1.title}: +{got:0.0}; the highlands still worth {Lab.Preview(c, high, temp, 0):0.0}";
        }

        static string RegionContracts()
        {
            var u = CareerUniverse();
            var c = u.career;
            foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id);
            c.records |= Records.Space | Records.Orbit | Records.TalFlyby | Records.TalOrbit | Records.TalLanding;
            c.contracts.Clear(); // starting offers may already fill the two open science slots
            var cand = (List<Contract>)typeof(Career).GetMethod("Candidates", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { 0.0 });
            var talRegion = cand.FirstOrDefault(k => k.type == "science" && k.body == "Tal" && !string.IsNullOrEmpty(k.region));
            Check(talRegion != null && Regions.Names("Tal").Contains(talRegion.region) && talRegion.title.Contains(talRegion.region),
                "no Tal region contract on offer: " + string.Join(" | ", cand.Select(k => $"{k.type}/{k.body}/{k.region}: {k.title}")));
            Check(cand.Any(k => k.type == "science" && k.body == "Arden" && k.situation == "FlyingLow" && !string.IsNullOrEmpty(k.region)), "no Arden low-flying region contract");

            var want = new Contract { id = 960, type = "science", body = "Tal", situation = "Landed", region = "Great Craters", state = ContractState.Active, deadline = 1e12, reward = 1000 };
            var anyTal = new Contract { id = 961, type = "science", body = "Tal", situation = "Landed", state = ContractState.Active, deadline = 1e12, reward = 1000 };
            c.contracts.Add(want); c.contracts.Add(anyTal);
            c.Credit(new ScienceData { experiment = "temp", subject = "temp@Tal:Landed:Dark Plains", transmit = 1 }, 1f, System4);
            Check(want.state == ContractState.Active && anyTal.state == ContractState.Completed, "science from the wrong region completed the crater contract (or none completed the plain one)");
            c.Credit(new ScienceData { experiment = "temp", subject = "temp@Tal:Landed:Great Craters", transmit = 1 }, 1f, System4);
            Check(want.state == ContractState.Completed, "science from the great craters didn't complete its contract");

            var open = new Contract { id = 962, type = "science", body = "Vesk", situation = "Landed", region = "Blue Ridges", state = ContractState.Active, deadline = 1e12, reward = 1000 };
            c.contracts.Add(open);
            var u2 = new Universe();
            u2.LoadJson(u.ToJson(), System4);
            var back = u2.career.contracts.Find(k => k.id == 962);
            Check(back != null && back.region == "Blue Ridges", "the region didn't survive save/load");
            return $"offered \"{talRegion.title}\"; the crater contract waited for crater science";
        }
    }
}
