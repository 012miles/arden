using System;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 34 tests: definitions, the update log, your own preset slots.</summary>
    public static partial class SimTests
    {
        static void RunMilestone34()
        {
            Run("Definitions: game words are found in sentences (longest first, whole words, short capitals only in capitals) and every one is explained", GlossaryTest);
            Run("Update log: newest first, no number twice, every update says what's new", UpdateLogTest);
            Run("Own presets: the first slot is free, each further one costs more, buying takes the funds and is saved with the campaign; a sandbox has every slot", PresetSlotsTest);
            Run("Paint and decals: 16 colours on most body parts, a decal string reads back the same, and paint and decals ride from the design into flight and through a save", PaintDecalTest);
            Run("Expedition: the budget and the seats stop a launch, the six resources are worked out, goals are met from what was done, the report scores out of 1000 with notes, and the board keeps the best", ExpeditionRulesTest);
            Run("Outposts: on the moons and planets, found within 300 m of a landed ship, searching tops up the tanks (not boosters) and supplies once, and is saved", OutpostTest);
            Run("Weather: each game has its own (new games don't all start in the same storm), the tests keep the fixed weather", WeatherSeedTest);
        }

        static string ExpeditionRulesTest()
        {
            var sc = Expedition.Scenario("tal");
            var lander = Presets.All().First(p => p.label.StartsWith("Tal Lander")).design;
            var dash = Expedition.Evaluate(lander, sc, 1, 1);
            Check(dash.seats >= 1 && dash.dv > 1000 && dash.cost > 0 && dash.mass > 1, $"dashboard: seats {dash.seats}, dv {dash.dv:0}, cost {dash.cost:0}, mass {dash.mass:0.0}");
            Check(Expedition.Evaluate(lander, sc, 1, dash.seats + 1).blockers.Any(b => b.Contains("seats")), "more players than seats stops it");
            var pricey = Expedition.Evaluate(lander, new ExScenario { id = "x", name = "x", budget = 100, dvNeed = 1, tripDays = 1, goals = new ExGoal[0] }, 1, 1);
            Check(pricey.blockers.Any(b => b.StartsWith("Over budget")), "over budget stops it");
            Check(Expedition.Budget(sc, 0) > Expedition.Budget(sc, 1) && Expedition.Budget(sc, 2) < Expedition.Budget(sc, 1), "easy has more money, hard less");
            var p = new ExProgress { crew = 2, budget = 45000, cost = 30000, dvHad = 6000 };
            p.flags |= VFlags.LandedTal;
            p.walked.Add("Tal");
            var news = Expedition.Update(sc, p);
            Check(news.Count == 2 && p.done.Contains("land:Tal") && p.done.Contains("walk:Tal"), "landing and walking on Tal are met: " + string.Join(",", p.done));
            var half = Expedition.Score(sc, p, 1, "Mia");
            Check(half.score > 300 && half.score < 700 && half.change.Count >= 2, $"half done: {half.score}, {half.change.Count} notes");
            p.flagged.Add("Tal"); p.home = true;
            Expedition.Update(sc, p);
            var full = Expedition.Score(sc, p, 1, "Mia");
            Check(full.score >= 900 && full.stars == 3, $"everything done, under budget, all home: {full.score} {full.stars} stars");
            var none = Expedition.Score(sc, new ExProgress { crew = 1, budget = 45000, cost = 44000 }, 1, "Mia");
            Check(none.score < 100 && none.stars == 0 && none.change.Any(c => c.Contains("speed budget") || c.Contains("Didn't land")), $"nothing done: {none.score}");
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rk_board_" + Guid.NewGuid().ToString("N"));
            Expedition.AddToBoard(dir, half);
            int place = Expedition.AddToBoard(dir, full);
            Check(place == 1 && Expedition.Top(dir, "tal").Count == 2 && Expedition.Top(dir, "tal")[0].score == full.score, "the best run tops the board");
            try { System.IO.Directory.Delete(dir, true); } catch { }
            return $"half {half.score}, full {full.score}";
        }

        static string OutpostTest()
        {
            var tal = System4.First(b => b.name == "Tal");
            Check(Outposts.On(tal).Count >= 2 && Outposts.On(tal).Any(l => l.kind == LandmarkKind.Outpost) && Outposts.On(tal).Any(l => l.kind == LandmarkKind.Wreck), "Tal has an outpost and a wreck");
            var camp = Outposts.On(tal).First(l => l.id == "tal.camp");
            var u = CareerUniverse();
            var ship = u.Add(VesselSim.CreateLanded(VesselDesign.Stack("Lander", "pod.wren", "tank.s2", "eng.ember"), tal, (camp.dir + CelestialBody.EastAt(camp.dir) * (100.0 / tal.radius)).normalized, 0));
            ship.career = u.career;
            Check(Outposts.Near(ship, 0, out double m)?.id == camp.id && m < 150, $"found beside the ship ({m:0} m)");   // (by id: a new game's seed rebuilds the list)
            var far = VesselSim.CreateLanded(VesselDesign.Stack("Far", "pod.wren"), tal, (camp.dir + CelestialBody.EastAt(camp.dir) * (2000.0 / tal.radius)).normalized, 0);
            Check(Outposts.Near(far, 0, out _) == null, "not from 2 km away");
            foreach (var p in ship.parts) p.propellant = 0;
            string got = Outposts.Scavenge(camp, ship, u.career);
            double fuel = ship.parts.Sum(p => p.propellant);
            Check(fuel > 0.5 && u.career.scavenged.Contains("tal.camp") && got.Contains("fuel"), $"searching fills the tanks: {fuel:0.00} t ({got})");
            Check(Outposts.Scavenge(camp, ship, u.career).Contains("already"), "only once");
            var back = new Career();
            back.Load(JsonUtility.FromJson<CareerSave>(JsonUtility.ToJson(u.career.ToSave())));
            Check(back.scavenged.Contains("tal.camp"), "searched outposts are saved");
            return got;
        }

        static string WeatherSeedTest()
        {
            var arden = System4[0];
            int was = Weather.GameSeed;
            bool wasOn = Weather.On;
            Weather.On = true;
            try
            {
                var pad = LaunchSite.Direction;
                var seen = new System.Collections.Generic.HashSet<string>();
                int rainy = 0, games = 40;
                for (int g = 1; g <= games; g++)
                {
                    Weather.GameSeed = g * 7919;
                    var w = Weather.At(arden, pad, 1.9e6);
                    seen.Add(w.rain > 0.15f ? "rain" : w.cloud > 0.25f ? "cloud" : "clear");
                    if (w.rain > 0.15f) rainy++;
                }
                Check(seen.Count >= 2, "different games start with different weather: " + string.Join(",", seen));
                Check(rainy < games / 2, $"storms are the exception, not the rule ({rainy} of {games} games start in rain)");
                return $"{rainy}/{games} rainy starts, kinds: {string.Join(",", seen)}";
            }
            finally { Weather.GameSeed = was; Weather.On = wasOn; }
        }

        static string PaintDecalTest()
        {
            Check(PartCatalog.Paints.Length == 16 && PartCatalog.PaintNames.Length == 16, "16 colours, each named");
            Check(PartCatalog.Paintable(PartCatalog.Get("pod.wren")) && PartCatalog.Paintable(PartCatalog.Get("tank.s2")) && !PartCatalog.Paintable(PartCatalog.Get("chute.s")), "capsules and tanks take paint, parachutes don't");
            var spec = new Decals.Spec { id = "star", pos = new Vector3(0.1f, -0.25f, 0.6f), normal = new Vector3(0, 0, 1), size = 0.8f, spin = 30f };
            string code = Decals.Format(spec);
            Check(Decals.Parse(code, out var back) && back.id == "star" && (back.pos - spec.pos).magnitude < 0.01f && Mathf.Abs(back.spin - 30f) < 0.01f && Mathf.Abs(back.size - 0.8f) < 0.01f, "the decal reads back: " + code);
            Check(!Decals.Parse("star|1|2", out _) && !Decals.Parse(null, out _), "a broken decal string is refused");
            var d = VesselDesign.Default();
            d.nodes[0].variant = 7;
            d.nodes[0].decal = code;
            var v = VesselSim.CreateLanded(d, System4[0], CelestialBody.DirectionFromLatLon(5, 10), 0);
            Check(v.parts[0].variant == 7 && v.parts[0].decal == code, "paint and decal reach the flying rocket");
            var saved = JsonUtility.FromJson<VesselSave>(JsonUtility.ToJson(v.ToSave()));
            Check(saved.parts[0].decal == code && saved.parts[0].variant == 7, "and are kept in a save");
            var clone = d.Clone();
            clone.images.Add(new DecalImage { id = "img:0123", png = "AAAA" });
            Check(JsonUtility.FromJson<VesselDesign>(JsonUtility.ToJson(clone)).images.Count == 1, "a design carries its own pictures");
            return code;
        }

        static string GlossaryTest()
        {
            var f = Glossary.Find("Burn radial in at the apoapsis, then check your delta-v");
            Check(f.Any(e => e.term == "Radial in") && f.Any(e => e.term == "Apoapsis") && f.Any(e => e.term == "Delta-v") && f.Any(e => e.term == "Burn"), "radial in, apoapsis, delta-v and burn: " + string.Join(", ", f.Select(e => e.term)));
            Check(Glossary.Find("Fire the radial decoupler").First().term == "Radial decoupler", "\"radial decoupler\" should win over \"decoupler\"");
            Check(Glossary.Find("Ap 85 km  Pe 72 km").Count == 2, "Ap and Pe in capitals are found");
            Check(Glossary.Find("a map of the cap").Count == 0, "\"ap\" inside other words, or in lower case, is not a word to explain");
            Check(Glossary.Find("<b>Highest point</b> 12 km").Any(e => e.term == "Apoapsis"), "the Standard words (\"highest point\") lead to the same definition, through rich-text tags");
            Check(Glossary.Find("Tal").Any(e => e.term == "Tal") && Glossary.Find("total").Count == 0, "Tal the moon, not \"tal\" inside \"total\"");
            foreach (var e in Glossary.All)
            {
                Check(!string.IsNullOrWhiteSpace(e.what) && e.what.Length > 20, $"{e.term} needs a real definition");
                Check(Glossary.Groups.Contains(e.group), $"{e.term} is in an unknown group {e.group}");
            }
            Check(Glossary.All.Select(e => e.term).Distinct().Count() == Glossary.All.Length, "no word is defined twice");
            return $"{Glossary.All.Length} definitions";
        }

        static string UpdateLogTest()
        {
            var all = UpdateLog.All;
            for (int i = 1; i < all.Length; i++) Check(all[i].n < all[i - 1].n, $"update {all[i].n} is out of order");
            Check(all.All(u => u.what != null && u.what.Length > 0 && !string.IsNullOrWhiteSpace(u.name)), "every update has a name and says what's new");
            Check(UpdateLog.Latest.n == all.Max(u => u.n), "the latest is the newest");
            return $"{all.Length} updates, latest {UpdateLog.Latest.n}";
        }

        static string PresetSlotsTest()
        {
            Check(PresetStore.SlotPrice(1) == 0 && PresetStore.SlotPrice(2) == 5000 && PresetStore.SlotPrice(3) == 7500 && PresetStore.SlotPrice(12) == 30000, "slot prices 0, 5,000, 7,500 ... 30,000");
            var sandbox = new Career();
            sandbox.Start(GameMode.Sandbox);
            Check(PresetStore.OpenSlots(sandbox) == PresetStore.MaxSlots && !PresetStore.BuyNext(sandbox, out _), "a sandbox has every slot and buys none");
            var c = new Career();
            c.Start(GameMode.Career);
            Check(PresetStore.OpenSlots(c) == 1, "a new campaign has one slot");
            double before = c.funds;
            Check(PresetStore.BuyNext(c, out var why) && PresetStore.OpenSlots(c) == 2 && Math.Abs(c.funds - (before - 5000)) < 0.01, "buying slot 2 costs 5,000: " + why);
            c.funds = 100;
            Check(!PresetStore.BuyNext(c, out why) && why.Contains("7,500") && PresetStore.OpenSlots(c) == 2, "no money, no slot: " + why);
            var back = new Career();
            back.Load(JsonUtility.FromJson<CareerSave>(JsonUtility.ToJson(c.ToSave())));
            Check(back.presetSlots == 2, "the open slots are saved with the campaign");
            var old = c.ToSave(); old.presetSlots = 0;   // a save from before milestone 34
            back.Load(old);
            Check(PresetStore.OpenSlots(back) == 1, "an older save has the one free slot");
            return "ok";
        }
    }
}
