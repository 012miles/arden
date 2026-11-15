using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>One thing an expedition sets out to do, and what it's worth.</summary>
    public class ExGoal
    {
        public string id, text;
        public int points;
    }

    /// <summary>An expedition: where to, what to do there, the budget, how long it takes and the real mission it's like.</summary>
    public class ExScenario
    {
        public string id, name, blurb, real;
        public double budget, dvNeed, tripDays;
        public ExGoal[] goals;
    }

    /// <summary>What an expedition has done so far (kept in the room, checked by the host every second of the flight).</summary>
    [Serializable]
    public class ExProgress
    {
        public int flags;                                     // VFlags reached by the team's ship (and its astronauts)
        public List<string> walked = new List<string>();      // worlds an Ardling stood on
        public List<string> flagged = new List<string>();     // worlds with the team's flag
        public List<string> scavenged = new List<string>();   // outposts and wrecks searched ("body:id")
        public List<string> done = new List<string>();        // goals met
        public bool home;                                     // back on Arden with the crew aboard
        public int crew, lost;                                // Ardlings aboard at launch, and lost on the way
        public double cost, budget, dvHad, days;
    }

    /// <summary>The six resources (mass, power, budget, data, comms, time) and the problems in a design, for the builder.</summary>
    public class ExDashboard
    {
        public double mass, twr, dv, cost, budget, powerStore, powerMake, powerUse, days, tripDays;
        public int instruments, seats, players, antennas;
        public readonly List<string> blockers = new List<string>(), warnings = new List<string>();
        public bool CanLaunch => blockers.Count == 0;
    }

    /// <summary>The report after an expedition: the score, stars, each line of the score and notes on what to change.</summary>
    [Serializable]
    public class ExReport
    {
        public string scenario, team, date;
        public int score, stars, difficulty;
        public List<string> lines = new List<string>(), good = new List<string>(), change = new List<string>();
    }

    /// <summary>
    /// Milestone 34 (captaincarl24: "we're building a rocket and flying it like the rocket builder in singleplayer ... it's
    /// also supposed to be very large"): Expedition, redone on the real game. The team builds one rocket together in the
    /// real builder with the real parts, every player rides in it as an Ardling, and it flies through the real, full-size
    /// solar system. Each expedition has an objective first (the NASA Space Apps challenge): a budget, goals worth points,
    /// the six resources to manage, supplies to survive on, outposts to scavenge, and a scored report with a leaderboard.
    /// Plain C# (no Unity objects except the saved board).
    /// </summary>
    public static class Expedition
    {
        static ExGoal G(string id, string text, int points) => new ExGoal { id = id, text = text, points = points };

        public static readonly ExScenario[] Scenarios =
        {
            new ExScenario { id = "tal", name = "First steps on Tal", budget = 45_000, dvNeed = 5_500, tripDays = 2,
                blurb = "Land on Tal, walk out, plant the team's flag and bring everyone home.", real = "Apollo 11 (1969): the first people on the Moon.",
                goals = new[] { G("land:Tal", "Land on Tal", 300), G("walk:Tal", "Walk on Tal (ENTER after landing)", 200), G("flag:Tal", "Plant a flag on Tal (G on foot)", 100), G("home", "Bring the crew home to Arden", 300) } },
            new ExScenario { id = "outpost", name = "The lost outpost", budget = 32_000, dvNeed = 3_600, tripDays = 2,
                blurb = "Camp Ridge on Tal went quiet years ago. Land beside it, scavenge its fuel to get home, and bring everyone back.", real = "NASA's Artemis plans: a base camp near the Moon's south pole.",
                goals = new[] { G("land:Tal", "Land on Tal", 200), G("scav:Tal", "Scavenge an outpost or wreck on Tal", 300), G("walk:Tal", "Walk on Tal", 100), G("home", "Bring the crew home to Arden", 300) } },
            new ExScenario { id = "vesk", name = "Ice of Vesk", budget = 55_000, dvNeed = 6_200, tripDays = 4,
                blurb = "Arden's little ice moon: land, walk on the ice, plant a flag, come home.", real = "Europa Clipper (2024) and the plans to land on Jupiter's icy moon.",
                goals = new[] { G("land:Vesk", "Land on Vesk", 300), G("walk:Vesk", "Walk on Vesk", 200), G("flag:Vesk", "Plant a flag on Vesk", 100), G("home", "Bring the crew home to Arden", 300) } },
            new ExScenario { id = "ochre", name = "The red planet", budget = 110_000, dvNeed = 7_500, tripDays = 70,
                blurb = "Cross to Ochre, land, find Dust Harbor's supplies and come home. Months away: take supplies, or a greenhouse.", real = "Perseverance (2021) and Mars Sample Return.",
                goals = new[] { G("orbit:Ochre", "Get into orbit round Ochre", 150), G("land:Ochre", "Land on Ochre", 250), G("walk:Ochre", "Walk on Ochre", 150), G("scav:Ochre", "Scavenge on Ochre", 150), G("home", "Bring the crew home to Arden", 200) } },
            new ExScenario { id = "brume", name = "The giant's moons", budget = 160_000, dvNeed = 9_000, tripDays = 500,
                blurb = "Out to Brume, the gas giant: orbit it, land on its moon Mote, and make it back. The longest trip there is.", real = "Galileo, Juno and JUICE at Jupiter.",
                goals = new[] { G("orbit:Brume", "Get into orbit round Brume", 250), G("land:Mote", "Land on Mote", 300), G("walk:Mote", "Walk on Mote", 150), G("home", "Bring the crew home to Arden", 300) } },
            new ExScenario { id = "free", name = "Free expedition", budget = 100_000, dvNeed = 6_000, tripDays = 10,
                blurb = "Go anywhere. Points for every world you land on, every outpost you search and coming home.", real = "Voyager (1977): the grand tour of the outer planets.",
                goals = new[] { G("land:Tal", "Land on Tal", 120), G("land:Vesk", "Land on Vesk", 150), G("land:Ochre", "Land on Ochre", 200), G("land:Mote", "Land on Mote", 200), G("scav", "Scavenge an outpost or wreck", 130), G("home", "Bring the crew home to Arden", 200) } },
        };

        public static readonly string[] DifficultyNames = { "Easy", "Standard", "Hard" };
        public static readonly string[] DifficultyBlurbs =
        {
            "Half as much again to spend, and the crew can manage on less.",
            "The budget as planned.",
            "A tight budget: every part counts.",
        };

        public static ExScenario Scenario(string id) => Scenarios.FirstOrDefault(s => s.id == id) ?? Scenarios[0];
        public static double Budget(ExScenario s, int difficulty) => s.budget * (difficulty == 0 ? 1.5 : difficulty == 2 ? 0.8 : 1.0);

        // ---------------------------------------------------------------- the six resources

        /// <summary>Works out a design's dashboard for an expedition: what it costs, carries, makes and lasts, and what stops it.</summary>
        /// <remarks>Milestone 43 (captaincarl24: "sandbox for multiplayer has a budget?? fix that"): a sandbox room has no budget (0).</remarks>
        public static ExDashboard Evaluate(VesselDesign d, ExScenario s, int difficulty, int players, bool sandbox = false)
        {
            var x = new ExDashboard { budget = sandbox ? 0 : Budget(s, difficulty), players = players, tripDays = s.tripDays };
            if (d == null || d.nodes.Count == 0) { x.blockers.Add("Build a rocket first"); return x; }
            var defs = d.Defs();
            if (defs.Contains(null)) { x.blockers.Add("The rocket has a part this game doesn't know"); return x; }
            x.cost = Prices.Of(d);
            foreach (var def in defs)
            {
                x.seats += def.crew;
                x.powerStore += def.ecCapacity;
                x.powerMake += def.solarRate + def.alternator;
                x.powerUse += def.ecIdle;
                if (!string.IsNullOrEmpty(def.experiment)) x.instruments++;
                if (def.kind == PartKind.Antenna) x.antennas++;
            }
            var stages = Staging.AnalyzeDesign(d, 9.81);
            x.dv = stages.Sum(r => r.dvVac);
            x.mass = stages.Count > 0 ? stages[0].startMass : defs.Sum(p => p.dryMass + p.propellant + p.mono);
            x.twr = stages.Count > 0 ? stages.First(r => r.hasEngines || r == stages.Last()).twrAsl : 0;
            x.days = Supplies.DaysFor(d, Math.Max(1, players));
            // what stops a launch
            if (x.budget > 0 && x.cost > x.budget) x.blockers.Add($"Over budget: it costs {x.cost:N0}, the budget is {x.budget:N0}");
            if (x.seats < players) x.blockers.Add($"Everyone rides along: {players} players need {players} seats (it has {x.seats})");
            // what's risky
            if (x.dv < s.dvNeed * 0.85) x.warnings.Add($"Speed budget {x.dv:N0} m/s: the trip needs about {s.dvNeed:N0} (outposts can top you up)");
            if (x.twr > 0 && x.twr < 1.15) x.warnings.Add($"Push vs weight at lift-off is {x.twr:0.00}: it may not leave the pad (aim for 1.3+)");
            if (!double.IsInfinity(x.days) && x.days < s.tripDays) x.warnings.Add($"Supplies last {x.days:0.#} days; the trip takes about {s.tripDays:0} (add supply crates or a greenhouse)");
            if (x.powerMake <= x.powerUse && s.tripDays > 3) x.warnings.Add("Nothing makes power: add solar panels for a long trip");
            if (x.antennas == 0 && s.tripDays > 10) x.warnings.Add("No antenna: far from home you'll lose touch");
            if (x.instruments == 0) x.warnings.Add("No science instruments: you'll bring nothing back to study");
            if (!d.nodes.Any(n => PartCatalog.Get(n.id)?.kind == PartKind.Parachute)) x.warnings.Add("No parachute to land back on Arden");
            return x;
        }

        // ---------------------------------------------------------------- goals

        /// <summary>Whether a goal is met by what the expedition has done.</summary>
        public static bool Met(string goal, ExProgress p)
        {
            var parts = goal.Split(':');
            string kind = parts[0], body = parts.Length > 1 ? parts[1] : null;
            switch (kind)
            {
                case "land": return (p.flags & VFlags.LandedOn(body)) != 0 || p.walked.Contains(body);
                case "walk": return p.walked.Contains(body);
                case "flag": return p.flagged.Contains(body);
                case "orbit": return (p.flags & OrbitFlag(body)) != 0;
                case "scav": return body == null ? p.scavenged.Count > 0 : p.scavenged.Any(x => x.StartsWith(body + ":"));
                case "home": return p.home;
                default: return false;
            }
        }

        static int OrbitFlag(string body) => body == "Tal" ? VFlags.OrbitTal : body == "Vesk" ? VFlags.OrbitVesk : body == "Ochre" ? VFlags.OrbitOchre : body == "Brume" ? VFlags.OrbitBrume : body == "Arden" ? VFlags.OrbitHome : 0;

        /// <summary>Goals newly met (adds them to the progress), for the host to announce.</summary>
        public static List<ExGoal> Update(ExScenario s, ExProgress p)
        {
            var news = new List<ExGoal>();
            foreach (var g in s.goals)
                if (!p.done.Contains(g.id) && Met(g.id, p)) { p.done.Add(g.id); news.Add(g); }
            return news;
        }

        // ---------------------------------------------------------------- the report

        /// <summary>Scores an expedition out of 1000: the goals (800), money left (100) and everyone alive (100), with notes.</summary>
        public static ExReport Score(ExScenario s, ExProgress p, int difficulty, string team)
        {
            var r = new ExReport { scenario = s.id, team = team, difficulty = difficulty, date = DateTime.Now.ToString("MMM d, yyyy") };
            int maxGoals = s.goals.Sum(g => g.points);
            double goalPts = 0;
            foreach (var g in s.goals)
            {
                bool ok = p.done.Contains(g.id) || Met(g.id, p);
                int pts = ok ? (int)Math.Round(800.0 * g.points / maxGoals) : 0;
                goalPts += pts;
                r.lines.Add($"{(ok ? "✓" : "✗")} {g.text}: {pts}");
                if (ok) r.good.Add(g.text);
                else r.change.Add(Hint(g, s, p));
            }
            double money = p.budget > 0 ? Math.Max(0, Math.Min(1, 1 - p.cost / p.budget)) : 1;   // (sandbox: no budget to keep to)
            int moneyPts = (int)Math.Round(100 * money * Math.Min(1, goalPts / 400.0));   // saving money only counts if you got somewhere
            r.lines.Add(p.budget > 0 ? $"Money left over ({money * 100:0}% of the budget): {moneyPts}" : $"Sandbox (no budget): {moneyPts}");
            if (p.budget > 0 && money > 0.25 && goalPts >= 400) r.good.Add($"Came in well under budget ({p.cost:N0} of {p.budget:N0})");
            int crewPts = p.crew > 0 && p.lost == 0 && p.home ? 100 : p.crew > 0 && p.lost == 0 ? 50 : 0;
            r.lines.Add(p.lost > 0 ? $"Ardlings lost: {p.lost}: 0" : $"Everyone alive{(p.home ? " and home" : "")}: {crewPts}");
            if (p.lost > 0) r.change.Add($"{p.lost} Ardling{(p.lost == 1 ? " was" : "s were")} lost. Take more supplies, a heat shield and parachutes, and land slowly.");
            r.score = (int)Math.Round(goalPts) + moneyPts + crewPts;
            if (difficulty == 2) { r.score = (int)Math.Round(r.score * 1.15); r.lines.Add("Hard: +15%"); }
            else if (difficulty == 0) { r.score = (int)Math.Round(r.score * 0.85); r.lines.Add("Easy: -15%"); }
            r.score = Math.Min(1000, r.score);
            r.stars = r.score >= 850 ? 3 : r.score >= 600 ? 2 : r.score >= 350 ? 1 : 0;
            if (r.change.Count == 0) r.change.Add("A perfect run. Try it on Hard, or with a cheaper rocket.");
            return r;
        }

        static string Hint(ExGoal g, ExScenario s, ExProgress p)
        {
            var parts = g.id.Split(':');
            string body = parts.Length > 1 ? parts[1] : null;
            switch (parts[0])
            {
                case "land": return p.dvHad < s.dvNeed ? $"Didn't land on {body}: the rocket had {p.dvHad:N0} m/s of speed budget and the trip needs about {s.dvNeed:N0}. Bigger tanks, a lighter lander, or refuel at an outpost." : $"Didn't land on {body}: plan the trip in the map (M) with a maneuver, and land with legs, slowly.";
                case "walk": return $"Nobody walked on {body}: after landing press ENTER to climb out (C switches first and third person).";
                case "flag": return $"No flag on {body}: on foot, press G to plant one.";
                case "orbit": return $"Never in orbit round {body}: burn retrograde at the closest point to be caught.";
                case "scav": return "No outpost searched: look for the ⌂ marks on the map, land within 300 m and press Scavenge.";
                case "home": return "The crew didn't make it home: keep fuel for the way back, and bring a heat shield and parachutes for Arden.";
                default: return g.text;
            }
        }

        // ---------------------------------------------------------------- the leaderboard (on this computer)

        [Serializable] class BoardFile { public List<ExReport> runs = new List<ExReport>(); }

        static string BoardPath(string dir) => Path.Combine(dir, "expedition_board.json");

        public static List<ExReport> Board(string dir)
        {
            try { var f = File.Exists(BoardPath(dir)) ? JsonUtility.FromJson<BoardFile>(File.ReadAllText(BoardPath(dir))) : null; return f?.runs ?? new List<ExReport>(); }
            catch { return new List<ExReport>(); }
        }

        /// <summary>Adds a run to the board (the ten best for each expedition are kept). Returns its place (1 = best).</summary>
        public static int AddToBoard(string dir, ExReport r)
        {
            var runs = Board(dir);
            runs.Add(r);
            var keep = runs.GroupBy(x => x.scenario).SelectMany(gr => gr.OrderByDescending(x => x.score).Take(10)).ToList();
            try { Directory.CreateDirectory(dir); File.WriteAllText(BoardPath(dir), JsonUtility.ToJson(new BoardFile { runs = keep }, true)); } catch { }
            return keep.Where(x => x.scenario == r.scenario).OrderByDescending(x => x.score).ToList().IndexOf(r) + 1;
        }

        public static List<ExReport> Top(string dir, string scenario) => Board(dir).Where(x => x.scenario == scenario).OrderByDescending(x => x.score).Take(10).ToList();
    }
}
