using System;
using System.Linq;

namespace Rocket
{
    /// <summary>One step of the story: who at mission control asks for it, what to do, what they say when it's done.</summary>
    public class StoryMission
    {
        public int chapter;
        public string title, goal, who, brief, done;
        public double funds, science;
        public Func<Universe, bool> check;
    }

    /// <summary>
    /// Milestone 28 (captaincarl24's pick): the story. Twenty missions in five chapters, from the first hop to the moons of the
    /// giant, one at a time. Mission control (Rosa the flight director, Bram the chief engineer, Dr Tilly the scientist)
    /// briefs each one and says something when it's done. Works in sandbox too (the pay is only in career). A save
    /// that's already done some of it skips ahead quietly the first time.
    /// </summary>
    public static class Story
    {
        public const string Rosa = "Rosa Ardling, flight director", Bram = "Bram Ardling, chief engineer", Tilly = "Dr Tilly Ardling, science";
        public static readonly string[] Chapters = { "First Steps", "To Tal", "Building Up", "Ice and Dust", "The Giant" };

        static bool Rec(Universe u, int bit) => (u.career.records & bit) != 0;

        public static readonly StoryMission[] Missions =
        {
            // Chapter 1: First Steps
            M(0, "Leave the pad", "Launch a rocket", Rosa,
                "Welcome to the space program! Everyone's out on the lawn. Let's start simple: build anything with an engine and get it off the pad.",
                "It flew! Half the town heard that. Well done.", 2_000, 2, u => Rec(u, Records.Launch)),
            M(0, "Edge of space", "Climb above 70 km", Bram,
                "The air thins out at 70 km. Above that you're in space. More fuel, a second stage if you can. Up, up, up.",
                "That's space! The sky goes black up there, doesn't it?", 8_000, 6, u => Rec(u, Records.Space)),
            M(0, "Round and round", "Get into orbit", Rosa,
                "Going up isn't enough: you have to go sideways fast enough to keep falling round Arden. Tip over east as you climb.",
                "Orbit! You'll never run out of 'down' again.", 15_000, 12, u => Rec(u, Records.Orbit)),
            M(0, "Someone aboard", "Orbit with crew", Tilly,
                "Now with an Ardling aboard. They've been practising in the pool for weeks. Bring them home safe, with a parachute.",
                "They say the view was worth every minute of the training.", 12_000, 12, u => Rec(u, Records.CrewedOrbit)),
            // Chapter 2: To Tal
            M(1, "Fly to Tal", "Reach Tal's sphere of influence", Rosa,
                "See Tal up there? Plan a burn from orbit (click your orbit in the map) so your path meets it.",
                "We've got pictures of Tal's far side! Nobody's seen that before.", 20_000, 20, u => Rec(u, Records.TalFlyby)),
            M(1, "Orbit Tal", "Get into orbit round Tal", Tilly,
                "Slow down near Tal and it'll catch you. Burn retrograde at your closest point.",
                "Orbiting another world. My instruments are very happy.", 25_000, 25, u => Rec(u, Records.TalOrbit)),
            M(1, "Land on Tal", "Touch down on Tal", Bram,
                "No air, so no parachutes. Landing legs, a good engine, and come down slowly. Under 5 m/s at the end.",
                "Touchdown! The dust is settling. The whole town is cheering.", 40_000, 40, u => Rec(u, Records.TalLanding)),
            M(1, "Come home", "Return from Tal", Rosa,
                "Now the hard part: get back. Keep enough fuel to lift off Tal and a heat shield for the trip through Arden's air.",
                "Welcome home! That was a proper voyage.", 50_000, 50, u => Rec(u, Records.TalReturn)),
            // Chapter 3: Building Up
            M(2, "Dock two ships", "Join two ships with docking ports", Bram,
                "Target a ship with a docking port, line up with the docking guide, and come in slowly. Under 1.5 m/s.",
                "Click! Two ships are one. That's how we'll build big things up there.", 20_000, 20, u => Rec(u, Records.Docking)),
            M(2, "Plant a flag", "A flag on Tal", Tilly,
                "Take an Ardling to Tal, walk out (EVA), and plant a flag. For the scrapbook.",
                "It's on the front page of the paper.", 25_000, 25, u => Rec(u, Records.TalFlag)),
            M(2, "Something strange", "Find a hidden place", Tilly,
                "Pilots keep seeing odd shapes down on the worlds. Fly low over the ground and look for the '?' on the map, then land beside it.",
                "Fascinating! I'll be writing about this for years.", 30_000, 40, u => u.career.visited.Count >= 1),
            M(2, "Space station", "Dock three ships into one in orbit", Rosa,
                "A home in orbit: send up a core with spare docking ports, then dock two more ships to it.",
                "Our first space station! The crew say it needs curtains.", 50_000, 50, u => u.career.badges.Contains("station")),
            // Chapter 4: Ice and Dust
            M(3, "The icy moon", "Land on Vesk", Tilly,
                "Vesk is the small icy moon further out. Its ice could be fuel one day. Go and see.",
                "Ice everywhere, just like we hoped.", 60_000, 60, u => Rec(u, Records.VeskLanding)),
            M(3, "Leave home", "Escape Arden's gravity", Bram,
                "Burn hard enough and Arden lets you go. You'll be going round the sun on your own.",
                "You're free of Arden! Next stop: the planets.", 60_000, 50, u => Rec(u, Records.Escape)),
            M(3, "The red planet", "Fly to Ochre", Rosa,
                "Ochre's the dusty orange planet. Use the transfer planner, and leave when the window opens.",
                "Ochre's filling the screen. Look at those canyons!", 120_000, 100, u => Rec(u, Records.OchreFlyby)),
            M(3, "Red sands", "Land on Ochre", Bram,
                "Thin air: parachutes help a little, engines do the rest. Watch out for dust storms.",
                "Down on Ochre! The dust's a lovely colour.", 220_000, 160, u => Rec(u, Records.OchreLanding)),
            M(3, "The long way home", "Return from Ochre", Rosa,
                "Bring them back. It's a long trip, so check the supplies.",
                "Home from another planet. You've made history.", 300_000, 200, u => Rec(u, Records.OchreReturn)),
            // Chapter 5: The Giant
            M(4, "The giant", "Orbit Brume", Tilly,
                "Brume, the banded giant. You can't land on it (no ground) but you can orbit it, and its moons are waiting.",
                "Those storms are bigger than Arden. Incredible.", 300_000, 260, u => Rec(u, Records.BrumeOrbit)),
            M(4, "A giant's moon", "Land on one of Brume's moons", Bram,
                "Rime is frozen, Pyre is full of lava. Pick one and land.",
                "Two planets away and still in one piece. Good work.", 450_000, 340, u => Rec(u, Records.BrumeMoonLanding)),
            M(4, "Explorer", "Find five hidden places", Tilly,
                "There are more strange places out there. Find five, and the story's yours.",
                "That's everything we know of. Thank you, from all of us at mission control. The rest of space is yours to explore.", 200_000, 300, u => u.career.visited.Count >= 5),
        };

        static StoryMission M(int ch, string title, string goal, string who, string brief, string done, double funds, double sci, Func<Universe, bool> check) =>
            new StoryMission { chapter = ch, title = title, goal = goal, who = who, brief = brief, done = done, funds = funds, science = sci, check = check };

        /// <summary>The mission in progress (null when the story's finished).</summary>
        public static StoryMission Current(Career c) => c.storyStep >= 0 && c.storyStep < Missions.Length ? Missions[c.storyStep] : null;
        public static bool Finished(Career c) => c.storyStep >= Missions.Length;

        /// <summary>
        /// Once a second: if the mission in progress is done, pay for it and move on (one a second, so each gets its
        /// message). The first time in a save, skip what's already done quietly.
        /// </summary>
        public static void Update(Universe u)
        {
            var c = u.career;
            if (!c.storyStarted)
            {
                c.storyStarted = true;
                while (c.storyStep < Missions.Length && Missions[c.storyStep].check(u)) c.storyStep++;
                c.storyBriefed = false;
                return;
            }
            var m = Current(c);
            if (m == null || !m.check(u)) return;
            if (c.IsCareer) { c.funds += m.funds; c.science += m.science; }
            c.storyNews.Add((m.who, m.done + (c.IsCareer ? $"  (+{m.funds:N0} funds, +{m.science:0} science)" : "")));
            c.storyStep++;
            c.storyBriefed = false;
        }
    }
}
