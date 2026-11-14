using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 23: the in-game guide, a page per topic, shown from the title screen and the pause menu. Key names come
    /// from the current bindings, so the guide stays right after rebinding.
    /// </summary>
    public static class HowToPlay
    {
        static string Kb(K k) => $"<b>{In.Name(k)}</b>";

        public static readonly string[] Topics = { "Basics", "Building", "To orbit", "Map and planning", "Docking and EVA", "Science and career", "Supplies and bases", "Multiplayer", "Controls" };

        static readonly string[] EasyTopics = { "Basics", "Building", "Going up", "The map", "Ships and walks", "Science and jobs", "Food and bases", "Playing together", "Keys" };

        /// <summary>The playing-together page (milestone 34: expeditions on the real game, rocket rooms, your Ardling, cabins).</summary>
        const int Together = 7;

        public static string TopicName(int i) => Words.Easy ? EasyTopics[i] : Topics[i];

        /// <summary>The page for a topic, in the player's words (milestone 24). The keys page is the same for everyone.</summary>
        static string Page(int i)
        {
            if (i >= Topics.Length - 1) return EngineerPage(i);
            if (i == Together) return TogetherPage();
            return Words.Engineer ? EngineerPage(i) : Words.Standard ? StandardPage(i) : EasyPage(i);
        }

        static string StandardPage(int i)
        {
            switch (i)
            {
                case 0: return
                    "Arden Space Program is about building rockets and flying them with real physics: gravity, air and orbits work like the real thing.\n\n" +
                    "You start at the space centre on Arden. Build a rocket in the assembly building, launch it, and try to get into orbit " +
                    "(going round Arden without falling back down). Then visit the moon Tal, the icy moon Vesk, the desert planet Ochre and finally the giant, Brume.\n\n" +
                    "Your crew are Ardlings: small astronauts in big helmets, each with a sticky-note face. On long trips they need food, water and air, " +
                    "and in career mode they get better with experience.\n\n" +
                    "Career mode has money, science, contracts and new parts to research. Sandbox gives you every part for free.\n\n" +
                    "The game saves when you go back to the builder. In flight, " + Kb(K.F5) + " quicksaves and " + Kb(K.F9) + " loads it.";
                case 1: return
                    "Pick a part from the list on the left, then click on your rocket to attach it. Green dots show where parts can stack; " +
                    "side parts (fins, solar panels, legs) stick onto the surface. Symmetry puts on several copies at once, evenly around the rocket.\n\n" +
                    "A part stays in your hand after you stick it on, so you can add more; right-click or ESC puts it down. Type in the search box to find a part. " +
                    "Backspace deletes the part under the mouse. Right-drag to turn the view and scroll to zoom. Right-click a part for its options. " +
                    "Ctrl+Z (Cmd+Z on a Mac) undoes. File > Play the tutorial shows you round step by step.\n\n" +
                    "The panel on the right shows your stages and each one's speed budget: how much its engines can change your speed. " +
                    "Getting into orbit takes about 3,400 m/s; going to Tal and back about 5,500.\n\n" +
                    "Keep the centre of mass (yellow) ahead of the centre of lift (blue) so the rocket flies nose first, like an arrow. " +
                    "The engines must push harder than the rocket weighs: a push-to-weight of 1.2 or more gets you off the pad.";
                case 2: return
                    "Press " + Kb(K.Space) + " to start the first stage. Fly straight up to about 1 km, then tip a little toward the east (" + Kb(K.D) + ") " +
                    "and keep tipping slowly, so you're flying almost sideways by 40-50 km.\n\n" +
                    "Watch your highest point (top left). When it passes 75 km (above the air), cut the engine (" + Kb(K.X) + ") and coast up. " +
                    "Near the highest point, point forward along your path (the yellow marker on the ball) and burn again until your lowest point is above 75 km too. " +
                    "Now you're in orbit: going sideways so fast that you keep falling around the planet instead of into it.\n\n" +
                    "The ball shows which way you're pointing. " + Kb(K.T) + " turns on SAS, which holds your direction or points you forward, backward and so on.\n\n" +
                    "To come home: point backward and burn until your lowest point is down in the air (about 30 km), drop everything but the capsule, " +
                    "and the parachute opens by itself low down.";
                case 3: return
                    "Press " + Kb(K.M) + " for the map. Your orbit is the blue line. Click it to add a planned burn, then drag the handles to shape it: " +
                    "the new path shows straight away. The ball then points the way, and a timer says when to start.\n\n" +
                    "The flight computer can fly planned burns for you, and the meeting planner finds a path to a target.\n\n" +
                    "Time warp (" + Kb(K.Period) + " faster, " + Kb(K.Comma) + " slower, " + Kb(K.Slash) + " stop) makes long trips quick. " +
                    "Click the clock to see what's coming up: burns, close passes, sunrise.\n\n" +
                    "Planets line up best at certain times. To reach another planet, wait for its best time to go: the map's transfer panel shows when.";
                case 4: return
                    "Docking: get close to the other ship at almost the same speed, point your docking port at its port, and creep in slower than 1.5 m/s. " +
                    "RCS (" + Kb(K.R) + ") with " + Kb(K.H) + "/" + Kb(K.N) + "/" + Kb(K.I) + "/" + Kb(K.K) + "/" + Kb(K.J) + "/" + Kb(K.L) + " moves you without turning. " +
                    "The docking panel shows the distance and how fast you're closing, and SAS can line up the ports for you.\n\n" +
                    "Spacewalks: press EVA in the crew panel. On the ground, " + Kb(K.W) + "/" + Kb(K.S) + " walk and " + Kb(K.A) + "/" + Kb(K.D) + " turn; " +
                    Kb(K.Space) + " jumps, and in space the jetpack flies you around. Plant flags (" + Kb(K.G) + "), take samples, fix broken legs (engineers) " +
                    "and climb back in (" + Kb(K.B) + ").";
                case 5: return
                    "Science: run experiments in new places: on the pad, flying low and high, in space, and landed on each world and its different regions. " +
                    "Bring the results home for full value, or send them with an antenna for part of it.\n\n" +
                    "Career: science pays for new parts in the research tree; contracts pay money and fame. Records (first orbit, first landing...) pay bonuses. " +
                    "Hire Ardlings at the astronaut complex: pilots unlock more SAS modes, engineers fix things and mine faster, scientists earn more science.\n\n" +
                    "Ore: drills dig ore on moons and planets, and converters turn it into fuel, so you can fill up far from home.";
                default: return
                    "Supplies: each Ardling uses a day's food, water and air for every day away from home. The crew panel shows how long they'll last. " +
                    "Supply crates and modules carry more; a greenhouse grows food for two. If supplies run out, the crew have two days left.\n\n" +
                    "Bases: land modules close together (within 60 m). On a spacewalk, take a cable from one module and plug it into another. " +
                    "Cabled modules share power, supplies and ore, and the base's tanks can refuel any ship you plug in.\n\n" +
                    "The Base Hub is a home for four, and the Research Lab earns science every day while scientists work inside. " +
                    "Research-base contracts ask for three cabled modules with a lab.";
            }
        }

        static string EasyPage(int i)
        {
            switch (i)
            {
                case 0: return
                    "In this game you build rockets and fly them into space!\n\n" +
                    "You start at the space centre on Arden. Build a rocket, launch it, and fly up high. Can you go all the way round the planet? " +
                    "Then you can visit the moon Tal, the ice moon Vesk, the red planet Ochre and the giant planet Brume.\n\n" +
                    "Your crew are Ardlings. They wear big helmets with a sticky note on the front. Keep them safe and bring them home!\n\n" +
                    "The game saves by itself when you go back to the builder.";
                case 1: return
                    "Click a part on the left. Then click on your rocket to stick it on. Green dots show where parts can go.\n\n" +
                    "A good rocket has a capsule for the crew at the top, a parachute on the capsule, fuel tanks, an engine at the bottom, " +
                    "and fins at the bottom so it flies straight.\n\n" +
                    "The Steps panel on the right shows what happens each time you press " + Kb(K.Space) + ".\n\n" +
                    "To look around, hold the right mouse button and move the mouse. Scroll to zoom in and out.";
                case 2: return
                    "Press " + Kb(K.Space) + " to start the engine. Up you go!\n\n" +
                    "Press " + Kb(K.T) + " so the rocket stays steady. When a tank is empty, press " + Kb(K.Space) + " to drop it.\n\n" +
                    "To go round the planet, you have to go sideways really, really fast. Higher than 10 km, tap " + Kb(K.D) + " to tip the rocket over a bit at a time. " +
                    "Or press " + Kb(K.P) + " and the autopilot flies for you!\n\n" +
                    "To come home, press " + Kb(K.Space) + " until the parachute is ready. It opens by itself and you float down.";
                case 3: return
                    "Press " + Kb(K.M) + " to see the map. It shows your path around the planet and where the moons are.\n\n" +
                    "Click the clock at the top to see what happens next.\n\n" +
                    "To make time go faster, press " + Kb(K.Period) + ". Press " + Kb(K.Comma) + " to slow down, and " + Kb(K.Slash) + " to go back to normal. " +
                    "Space trips can take a long time!";
                case 4: return
                    "Two ships can join together in space! Fly very close and very slowly, and point the round docking parts at each other.\n\n" +
                    "Your Ardlings can go outside! Press EVA in the crew panel. Walk with " + Kb(K.W) + " and " + Kb(K.S) + ", turn with " + Kb(K.A) + " and " + Kb(K.D) +
                    ", and jump with " + Kb(K.Space) + ". In space, a jetpack flies them around.\n\n" +
                    "Press " + Kb(K.G) + " to plant a flag, and " + Kb(K.B) + " to climb back inside.";
                case 5: return
                    "Science: take your rocket to new places and do experiments. Bring the results home to earn science.\n\n" +
                    "In career mode, science buys new parts, and missions give you money. Pick a mission, do it, and get paid!\n\n" +
                    "You can hire more Ardlings too. Pilots are good at flying, engineers can fix things, and scientists find more science.";
                default: return
                    "Ardlings need food, water and air on long trips. The crew panel shows how many days they have left. Bring supply crates for long trips!\n\n" +
                    "You can build a base on another world: land some parts close together. Then an Ardling can walk outside and plug in cables to join them up.\n\n" +
                    "A base shares power and food, and it can fill up your ship with fuel.";
            }
        }

        static string TogetherPage()
        {
            if (Words.Easy) return
                "Play with up to four friends, each on their own computer on the same Wi-Fi. Pick Multiplayer, make a room or join one. " +
                "Your friends can find your room in their list, or type its code. Press My Ardling to pick your colour, face and hat!\n\n" +
                "Expedition: everyone builds one rocket together, then you all ride in it. Pick a trip, like landing on Tal. Stay under the money, " +
                "bring enough food, and land next to old outposts to grab fuel. At the end you get points and stars.\n\n" +
                "Rocket room: one friend builds, the others say what they think. Then one flies, one drops the stages, and one plans where to go.\n\n" +
                "Press TAB to walk inside your capsule, and ENTER to climb out after landing.";
            if (Words.Engineer) return
                "Multiplayer: up to four players on one local network, each on their own device. Title > Multiplayer: host an Expedition or a Rocket room, " +
                "or join from the list or by room code (it encodes the host's address). My Ardling sets the look the others see.\n\n" +
                "Expedition: all players edit the one design (last change wins; changes wait while you hold a part). The host or mission control picks the mission " +
                "and difficulty; the plan shows budget, mass and TWR, delta-v against the trip, supplies against trip time, power, instruments, antennas and seats. " +
                "Every player needs a seat. The host simulates the flight and streams it; goals are checked once a second; outposts and wrecks can be scavenged " +
                "within 300 m. Ending the expedition scores it out of 1000 with notes and a leaderboard.\n\n" +
                "Rocket rooms: builder, pilot, engineer and mission control. The builder edits while the others comment on parts.";
            return
                "Play with up to four people, each on their own computer on the same Wi-Fi. On the title screen pick Multiplayer, then host an Expedition " +
                "or a Rocket room, or join one from the list or with its room code. My Ardling picks how you look to everyone (suit colour, visor note, hat).\n\n" +
                "Expedition: everyone builds one rocket together, and every player rides in it as an Ardling. Pick a mission and difficulty, keep under the budget, " +
                "and watch the six resources in the plan (mass, power, budget, data, comms, time). Fly through the real solar system, survive on your supplies, " +
                "land beside outposts and wrecks to scavenge fuel and food, walk inside the cabin (TAB) and climb out on the surface (ENTER). " +
                "The report at the end says what went well and what to change, and the best runs go on the leaderboard.\n\n" +
                "Rocket rooms: one player builds while the others comment on parts. Then the pilot flies, the engineer stages and runs the systems, and mission control plans the route.";
        }

        static string EngineerPage(int i)
        {
            switch (i)
            {
                case 0: return
                    "Arden Space Program is about building rockets and flying them with real orbital physics.\n\n" +
                    "You start at the space centre on Arden. Build a rocket in the assembly building, launch it, and try to reach orbit, " +
                    "then Arden's moon Tal, the icy moon Vesk, the desert planet Ochre and finally the giant, Brume.\n\n" +
                    "Your crew are Ardlings: small folk in big helmets, each with their own sticky-note face. Look after them: " +
                    "on long trips they need supplies, and in career mode they gain experience.\n\n" +
                    "Career mode has money, science, contracts and a tech tree. Sandbox gives you every part for free.\n\n" +
                    "The game saves whenever you go back to the builder, and you can quicksave and quickload in flight (" + Kb(K.F5) + " / " + Kb(K.F9) + ").";
                case 1: return
                    "Pick parts from the list on the left and click on your rocket to attach them. Green dots are free stack nodes; " +
                    "side parts (fins, solar panels, legs) go on the surface. Symmetry places several at once.\n\n" +
                    "A part stays in your hand after you place it (right-click or ESC puts it down); a part picked up off the rocket goes back if you let go. " +
                    "Search the parts list by name or kind. Backspace (or Delete) removes the part under the mouse. Right-drag to turn the view, scroll to zoom. " +
                    "Right-click a part for its options. Ctrl+Z (Cmd+Z on a Mac) undoes, Ctrl+Shift+Z redoes. File > Play the tutorial for a guided tour.\n\n" +
                    "The panel on the right shows your stages and how much delta-v (change of speed) each one has. " +
                    "Getting to orbit takes about 3,300-3,500 m/s; to Tal and back about 5,500.\n\n" +
                    "Keep the centre of mass (yellow) above the centre of lift (blue) so the rocket flies nose first. " +
                    "A thrust-to-weight ratio (TWR) above 1.2 at launch gets you off the pad.";
                case 2: return
                    "Press " + Kb(K.Space) + " to light the first stage. Fly straight up until about 1 km, then lean a little east and keep " +
                    "leaning gradually so you are flying sideways by 40-50 km.\n\n" +
                    "Watch the apoapsis (highest point). When it passes 75 km (above the air), cut the engine (" + Kb(K.X) + ") and coast up. " +
                    "Near apoapsis, point prograde (along your motion) and burn again until the periapsis is above 75 km too: you're in orbit.\n\n" +
                    "The navball shows your heading. T toggles SAS, which holds your direction or points prograde, retrograde and so on.\n\n" +
                    "To come home: burn retrograde to drop the periapsis into the air, separate the capsule, and open the parachute low down.";
                case 3: return
                    "Press " + Kb(K.M) + " for the map. Your orbit is drawn in blue; click on it to add a maneuver node, then drag its handles " +
                    "to plan a burn. The navball then points the way, and the timer shows when to start.\n\n" +
                    "The flight computer can fly planned burns for you, and the intercept planner finds a transfer to a target.\n\n" +
                    "Time warp (" + Kb(K.Period) + " faster, " + Kb(K.Comma) + " slower, " + Kb(K.Slash) + " stop) makes long trips quick. " +
                    "The events list (click the clock) shows what's coming: burns, encounters, sunrise.\n\n" +
                    "To reach another planet, wait for its transfer window: the map's transfer panel shows when it opens.";
                case 4: return
                    "Docking: get close to a target with matching speed, point your docking port at its port and creep in at less than 1.5 m/s. " +
                    "RCS (" + Kb(K.R) + ") with " + Kb(K.H) + "/" + Kb(K.N) + "/" + Kb(K.I) + "/" + Kb(K.K) + "/" + Kb(K.J) + "/" + Kb(K.L) + " moves you without turning. " +
                    "The docking panel shows the distance and closing speed, and SAS can align the ports.\n\n" +
                    "Spacewalks: press EVA in the crew panel. On the ground, " + Kb(K.W) + "/" + Kb(K.S) + " walk and " + Kb(K.A) + "/" + Kb(K.D) + " turn; " +
                    Kb(K.Space) + " jumps and the jetpack flies you in space. Plant flags (" + Kb(K.G) + "), take samples, fix broken legs (engineers) " +
                    "and climb back in (" + Kb(K.B) + ").";
                case 5: return
                    "Science: run experiments in different places (on the pad, flying low and high, in space, landed on each world and its regions). " +
                    "Bring results home for full value or transmit them with an antenna for part of it.\n\n" +
                    "Career: science buys new parts in the tech tree; contracts pay money and reputation. Records (first orbit, first landing...) " +
                    "pay bonuses. Hire Ardlings in the astronaut complex: pilots unlock SAS modes, engineers fix things and mine better, " +
                    "scientists earn more science.\n\n" +
                    "Ore: drills dig ore on moons and planets; converters turn it into fuel, so you can refuel far from home.";
                case 6: return
                    "Supplies: each Ardling eats a crew-day of supplies a day away from home. The crew panel shows how long they'll last. " +
                    "Supply crates and modules carry more; a greenhouse grows food for two. If supplies run out, the crew have two days.\n\n" +
                    "Bases: land modules close together (within 60 m). On a spacewalk, take a cable from one module and plug it into another. " +
                    "Cabled modules share power, supplies and ore, and the base's tanks refuel any ship you plug in.\n\n" +
                    "The Base Hub is a home for four; the Research Lab earns science every day while scientists work in it. " +
                    "Research-base contracts ask for three cabled modules with a lab.";
                default:
                    var sb = new System.Text.StringBuilder();
                    sb.Append("Change any key in Settings > Controls (title screen).\n\n");
                    int n = 0;
                    foreach (var a in Keybinds.Actions)
                    {
                        sb.Append($"<b>{In.Name(a.key),-6}</b> {a.what}");
                        sb.Append(++n % 2 == 0 ? "\n" : "      ");
                    }
                    sb.Append("\n<b>1-0</b> action groups   <b>ESC</b> pause menu");
                    return sb.ToString();
            }
        }

        /// <summary>Draws the guide in a box: topics down the left, the page on the right. Returns false when closed.</summary>
        public static bool Draw(Rect r, ref int topic)
        {
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 16, r.y + 12, 300, 26), "How to play", Ui.Header);
            bool open = !Ui.Press(new Rect(r.xMax - 40, r.y + 10, 28, 24), "x", Ui.ButtonSmall);
            Ui.Text(new Rect(r.xMax - 380, r.y + 14, 60, 22), "Words", Ui.Small);
            Ui.WordsPicker(r.xMax - 330, r.y + 8, 92);
            float ty = r.y + 48;
            for (int i = 0; i < Topics.Length; i++)
            {
                if (Ui.Press(new Rect(r.x + 14, ty, 170, 30), TopicName(i), topic == i ? Ui.ButtonSmallSel : Ui.ButtonSmall)) topic = i;
                ty += 34;
            }
            var page = new Rect(r.x + 200, r.y + 48, (r.width - 216) * 0.93f, r.height - 64);
            Ui.Text(page, Page(topic), Ui.Small);
            return open;
        }
    }
}
