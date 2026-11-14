using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 34 (captaincarl24: "lets say i didnt know what radial in was ... when you hover over the text it will show a
    /// quick definition"): every game word explained. Rest the mouse on a word in any label or button for a moment (2 s by
    /// default, set in Settings) and a small box says what it means. The Definitions page lists them all with a search, from
    /// the title screen, the builder's menu and the pause menu.
    /// </summary>
    public static class Glossary
    {
        public class Entry
        {
            public string term, group, what;
            public string[] words;   // what it's called on screen at each word level (and short forms), matched as whole words
        }

        static Entry E(string group, string term, string what, params string[] words) =>
            new Entry { group = group, term = term, what = what, words = words.Length > 0 ? words : new[] { term } };

        public static readonly string[] Groups = { "Flying", "Orbits", "Building and parts", "Worlds and crew", "Space program", "Playing together" };

        public static readonly Entry[] All =
        {
            // ---------------------------------------------------------------- flying
            E("Flying", "Prograde", "The direction you're moving. Burning prograde speeds you up and raises the far side of your orbit.", "prograde"),
            E("Flying", "Retrograde", "Backwards: the opposite of the way you're moving. Burning retrograde slows you down; it's how you lower an orbit or come down to land.", "retrograde"),
            E("Flying", "Radial in", "Pointing toward the middle of the world you're going round, at right angles to the way you're moving. Burning radial in swings your orbit round (moves its high and low points) without making it much bigger or smaller.", "radial in", "radial-in"),
            E("Flying", "Radial out", "Pointing away from the world you're going round, at right angles to the way you're moving. Like radial in, it swings your orbit round rather than resizing it.", "radial out", "radial-out"),
            E("Flying", "Normal and anti-normal", "Straight up or down out of the flat plane of your orbit. Burning normal or anti-normal tilts your orbit: it's how you change inclination.", "anti-normal", "antinormal", "orbit-up", "orbit-down", "orbit normal"),
            E("Flying", "Throttle", "How hard the engines push, from 0 to 100%. Solid boosters ignore it: once lit they burn flat out.", "throttle"),
            E("Flying", "Thrust", "The push an engine makes, in kilonewtons (kN). More thrust for the same mass means you speed up faster.", "thrust"),
            E("Flying", "Staging", "A rocket is built in stages. Each one fires, burns out and is dropped, so you don't carry empty tanks. Space fires the next stage.", "staging", "stage", "stages"),
            E("Flying", "SAS", "Stability assist: the ship holds its direction by itself, or turns to point prograde, retrograde, at a target and so on.", "SAS"),
            E("Flying", "RCS", "Reaction control system: little thrusters (\"puffers\") that turn and nudge the ship, burning monopropellant. Handy for docking.", "RCS", "puffers", "puffer"),
            E("Flying", "Gimbal", "An engine that can tilt a little to steer the rocket.", "gimbal", "engine steering"),
            E("Flying", "Torque", "How strongly something can turn the ship. Capsules and probe cores have reaction wheels that turn it with power instead of fuel.", "torque", "turning strength"),
            E("Flying", "Angle of attack", "The angle between where the nose points and the way the air is coming from. Too much on a fast rocket and it can flip over.", "angle of attack", "AoA", "nose angle"),
            E("Flying", "Sideslip", "Sliding sideways through the air, with the nose pointed off to one side of the way you're going.", "sideslip", "side angle"),
            E("Flying", "Drag", "The air pushing back against you. It grows with the square of your speed, and it's strongest low down where the air is thick.", "drag"),
            E("Flying", "Dynamic pressure", "How hard the air is pushing on the rocket. Thick air and high speed both raise it; rockets ease off near its peak (\"max Q\") so they don't break up.", "dynamic pressure", "dyn. pressure", "air push", "max Q"),
            E("Flying", "Mach", "Your speed compared to the speed of sound in the air around you. Mach 1 is the speed of sound; drag jumps as you get close to it (the sound barrier).", "mach"),
            E("Flying", "Lift", "The upward push wings make by moving through the air.", "lift"),
            E("Flying", "G-force", "How heavy you feel. 1 g is standing still on Arden; hard burns and turns push it higher, and too many g's hurts the crew.", "g-force", "g force", "g-load"),
            E("Flying", "Time warp", "Speeding up the clock to skip long coasts. Fast warp isn't allowed in thick air or while the engines are burning.", "time warp", "warp"),
            E("Flying", "Abort", "The emergency button: the escape tower pulls the capsule away from a rocket that's going wrong.", "abort"),
            E("Flying", "Reentry", "Coming back down into a world's air from space, very fast. The air squeezed in front of you gets extremely hot, so point the heat shield forward.", "reentry", "re-entry"),
            E("Flying", "Aerobraking", "Dipping into a world's air to slow down a bit on each pass, instead of burning fuel.", "aerobraking", "aerobrake", "air braking"),
            E("Flying", "Aerocapture", "One deep pass through a world's air that slows you enough to be caught in orbit, without a big braking burn.", "aerocapture", "air capture"),
            E("Flying", "Altitude", "Your height above sea level. The ground can be higher or lower than sea level, so close to the ground watch the height above the ground too.", "altitude"),
            E("Flying", "Atmosphere", "The layer of air round a world: thick near the ground, thinning out as you climb, until there's none at all.", "atmosphere"),
            E("Flying", "Vacuum", "Space with no air at all. Some engines work much better in vacuum, and parachutes and wings do nothing there.", "vacuum"),

            // ---------------------------------------------------------------- orbits
            E("Orbits", "Orbit", "Going round a world so fast sideways that you keep falling but always miss the ground. No fuel is needed to stay in orbit, as long as it's above the air.", "orbit", "orbits", "orbiting"),
            E("Orbits", "Apoapsis", "The highest point of your orbit. Burning prograde at the lowest point raises it.", "apoapsis", "apoapses", "highest point", "top of the orbit", "Ap"),
            E("Orbits", "Periapsis", "The lowest point of your orbit. If it's inside the air or under the ground, you're coming down.", "periapsis", "periapses", "lowest point", "bottom of the orbit", "Pe"),
            E("Orbits", "Inclination", "How tilted your orbit is compared to the world's equator, in degrees. 0° goes round the equator; 90° passes over the poles.", "inclination", "orbit tilt"),
            E("Orbits", "Eccentricity", "How stretched an orbit is. 0 is a perfect circle, close to 1 is a long thin oval, and 1 or more means you're escaping.", "eccentricity", "orbit stretch"),
            E("Orbits", "Circularize", "Making your orbit round, usually by burning prograde at the highest point until the lowest point comes up to match.", "circularize", "circularise", "round out the orbit", "make the orbit round"),
            E("Orbits", "Sub-orbital", "Up and back down: your path meets the ground (or the air) before it goes all the way round.", "sub-orbital", "suborbital", "not in orbit yet"),
            E("Orbits", "Escape", "Going fast enough to leave a world's pull for good: the orbit opens up and never comes back round.", "hyperbolic", "escaping", "escape velocity", "escape"),
            E("Orbits", "Sphere of influence", "The region round a world where its gravity is the one that matters. Cross the edge and your orbit switches to the next world.", "sphere of influence", "SOI", "gravity zone", "gravity bubble"),
            E("Orbits", "Trajectory", "The path your ship will follow if nothing changes.", "trajectory"),
            E("Orbits", "Maneuver", "A planned burn. Put it on your orbit, drag its handles, and see where it takes you before you fire the engines.", "maneuver node", "maneuver nodes", "maneuver", "maneuvers", "planned burn"),
            E("Orbits", "Burn", "Firing the engines to change your speed or your orbit.", "burn", "burns", "burning"),
            E("Orbits", "Delta-v", "How much your ship can change its speed with the fuel it has, in metres per second. Every trip costs some; the builder adds up what each stage has.", "delta-v", "Δv", "speed budget"),
            E("Orbits", "TWR", "Thrust-to-weight ratio: the engines' push divided by the ship's weight. Above 1 a rocket can lift off; about 1.3 to 2 is good for a launch.", "TWR", "thrust-to-weight", "thrust-to-weight ratio", "push vs weight"),
            E("Orbits", "Isp", "Specific impulse: how well an engine uses its fuel, in seconds. Higher Isp gets more delta-v from the same fuel. Space engines have high Isp in vacuum but are weak in thick air.", "Isp", "specific impulse", "fuel efficiency"),
            E("Orbits", "Gravity turn", "Tipping the rocket over gently after launch so gravity bends the path sideways into orbit. It saves a lot of fuel compared to going straight up.", "gravity turn"),
            E("Orbits", "Rendezvous", "Getting your ship to the same place at the same time as another one, at the same speed, so they end up side by side.", "rendezvous", "meet-up"),
            E("Orbits", "Intercept", "A path that reaches another ship or world at the right moment to meet it.", "intercept", "encounter"),
            E("Orbits", "Docking", "Joining two ships with docking ports, so crew and fuel can move between them.", "docking", "docked", "dock"),
            E("Orbits", "Transfer window", "When the worlds are lined up so a trip costs the least fuel. Miss it and you wait for the next one, or pay much more.", "transfer window", "best time to go", "launch window"),
            E("Orbits", "Transfer orbit", "The cheapest way between two round orbits: one burn stretches your orbit out to the other one, and a second burn rounds it out when you get there.", "transfer orbit", "hohmann", "transfer"),
            E("Orbits", "Orbit insertion", "The burn on arrival that slows you enough to be caught in orbit round the new world.", "orbit insertion", "insertion"),

            // ---------------------------------------------------------------- building and parts
            E("Building and parts", "Decoupler", "Joins two stages and pushes them apart when it fires, so the empty one drops away.", "decoupler", "decouplers", "decouple", "separator", "splitter"),
            E("Building and parts", "Radial decoupler", "Holds a booster on the side of a rocket, and lets go of it when it fires.", "radial decoupler"),
            E("Building and parts", "Fairing", "A smooth shell over the top of a rocket that protects what's inside from the air. It splits open and falls away once you're high up.", "fairing", "fairings"),
            E("Building and parts", "Nose cone", "A pointed top that cuts the drag.", "nose cone"),
            E("Building and parts", "Fins", "Small wings at the bottom of a rocket that keep it pointing straight, like the feathers on an arrow.", "fins", "fin"),
            E("Building and parts", "Solid booster", "A rocket full of solid fuel: strong and cheap, but once it's lit you can't throttle it or turn it off.", "solid rocket booster", "solid booster", "SRB", "booster", "boosters"),
            E("Building and parts", "Oxidizer", "What rocket fuel burns with. There's no air in space, so rockets carry their own oxidizer.", "oxidizer", "oxidiser", "liquid fuel"),
            E("Building and parts", "Monopropellant", "A fuel that burns on its own without oxidizer. RCS puffers use it.", "monopropellant", "monoprop", "thruster fuel", "puffer fuel"),
            E("Building and parts", "Propellant", "Whatever an engine burns: fuel and oxidizer, or monopropellant.", "propellant"),
            E("Building and parts", "Electric charge", "Power stored in batteries, made by solar panels or a nuclear battery. Probes, antennas, science and reaction wheels use it.", "electric charge", "EC"),
            E("Building and parts", "Solar panel", "Makes power from sunlight. Nothing in a world's shadow, and less the farther you are from the sun.", "solar panel", "solar panels", "solar"),
            E("Building and parts", "Antenna", "Sends science and messages home. It needs a clear line to a ground station (or a relay) and enough power.", "antenna", "antennas", "relay"),
            E("Building and parts", "Probe core", "A computer that flies the ship with nobody inside. It needs power and a signal home.", "probe core", "probe"),
            E("Building and parts", "Reaction wheel", "Spinning wheels inside a part that turn the ship using power instead of fuel.", "reaction wheel", "reaction wheels"),
            E("Building and parts", "Heat shield", "A thick shield that soaks up the heat of coming down through the air. Its coating (ablator) slowly burns away; point it forward on the way down.", "heat shield", "ablator", "shield coating"),
            E("Building and parts", "Escape tower", "A small rocket on top of the capsule that pulls the crew to safety if the launch goes wrong.", "escape tower", "launch escape"),
            E("Building and parts", "Parachute", "Slows you down to land. It only opens safely when you're slow enough and low enough in the air.", "parachute", "parachutes", "chute", "chutes"),
            E("Building and parts", "Jet engine", "Breathes air, so it only works inside an atmosphere, but uses far less fuel than a rocket engine.", "jet engine", "jet engines", "jet"),
            E("Building and parts", "Fuel maker", "Turns ore dug up on another world into fuel, so a ship can fill up far from home.", "ISRU", "fuel maker", "converter"),
            E("Building and parts", "Ore", "Rock with useful stuff in it. A drill digs it up and a fuel maker turns it into fuel.", "ore"),
            E("Building and parts", "Radiator", "Gets rid of heat from hard-working machines like the fuel maker.", "radiator", "radiators"),
            E("Building and parts", "Symmetry", "Placing copies of a part evenly round the rocket at once: 2, 3, 4, 6 or 8 of them.", "symmetry"),
            E("Building and parts", "Center of mass", "The rocket's balance point. Keep it ahead of the center of lift so the rocket flies straight.", "center of mass", "centre of mass", "CoM"),
            E("Building and parts", "Center of lift", "Where the air's push on the rocket acts. Behind the center of mass the rocket flies straight; in front of it, the rocket wants to flip.", "center of lift", "centre of lift", "CoL", "center of pressure"),
            E("Building and parts", "Sub-assembly", "A saved group of parts you can drop into any rocket.", "sub-assembly", "sub-assemblies", "subassembly"),
            E("Building and parts", "Preset", "A ready-made rocket you can load into the builder and change. You can save your own rockets as presets too.", "preset", "presets"),
            E("Building and parts", "Payload", "What the rocket carries up: the part that does the job, like a satellite, a lander or the crew.", "payload"),
            E("Building and parts", "Wet and dry mass", "A ship's mass full of fuel (wet) and empty (dry). The difference is what the engines can burn.", "wet mass", "dry mass"),

            // ---------------------------------------------------------------- worlds and crew
            E("Worlds and crew", "Arden", "Your home world, where the space centre is.", "Arden"),
            E("Worlds and crew", "Tal", "Arden's big moon: grey, cratered and airless. Most people's first trip.", "Tal"),
            E("Worlds and crew", "Vesk", "Arden's small second moon: a ball of ice with very weak gravity, farther out than Tal.", "Vesk"),
            E("Worlds and crew", "Ochre", "The dusty orange desert planet next out from Arden. Its air is thin: parachutes help, but not much.", "Ochre"),
            E("Worlds and crew", "Brume", "The banded gas giant far out from the sun, with its own moons. There's no ground to land on.", "Brume"),
            E("Worlds and crew", "Wanderers", "Small rocks that pass through Arden's space on open paths and leave again, unless a ship with a grabber catches one.", "wanderer", "wanderers"),
            E("Worlds and crew", "Ardling", "Your crew: the people of Arden. Pilots, engineers and scientists each have their own skills.", "Ardling", "Ardlings"),
            E("Worlds and crew", "EVA", "Extra-vehicular activity: an Ardling climbing out to move round outside the ship.", "EVA", "spacewalk"),
            E("Worlds and crew", "Supplies", "Food, water and air for the crew on long trips. If they run out, the crew are in trouble.", "supplies"),
            E("Worlds and crew", "Gravity", "The pull of a world. Big, dense worlds pull harder: Tal's gravity is much weaker than Arden's.", "gravity"),

            // ---------------------------------------------------------------- space program
            E("Space program", "Funds", "Your space program's money in a campaign. Contracts pay funds; parts, launches and upgrades cost them.", "funds", "money"),
            E("Space program", "Science", "Points from experiments in new places. Spend them in the tech tree to unlock new parts.", "science"),
            E("Space program", "Reputation", "How well known your program is. More reputation brings bigger contracts.", "reputation"),
            E("Space program", "Contract", "A job from mission control: do it and get paid.", "contract", "contracts"),
            E("Space program", "Tech tree", "The research map: spend science to unlock new parts.", "tech tree", "research"),
            E("Space program", "Recovery", "Bringing a ship or booster back home to get some of its cost back.", "recovery", "recover", "recovered"),
            E("Space program", "Campaign", "The career game: run a space program with money, science and contracts, and unlock parts as you go.", "campaign", "career"),
            E("Space program", "Sandbox", "Every part, no money: build and fly anything.", "sandbox"),

            // ---------------------------------------------------------------- playing together
            E("Playing together", "Host", "The player whose game runs the room. Everyone else's game follows the host's.", "host"),
            E("Playing together", "Room code", "The code (like 38RL-DF8P) friends type to join your room.", "room code"),
            E("Playing together", "Mission control", "A job in a rocket room: picks the goal, plans the burns, runs time warp and ends the flight.", "mission control"),
            E("Playing together", "Flight engineer", "A job in a rocket room: stages, aborts and runs the gear, lights, brakes, parachutes and RCS.", "flight engineer"),
            E("Playing together", "Pilot", "A job in a rocket room: steers, works the throttle and SAS.", "pilot"),
            E("Playing together", "Concept maturity", "How far along a mission design is, after NASA's Concept Maturity Levels: from a rough idea to a design ready to build.", "maturity", "CML"),
        };

        // ---------------------------------------------------------------- finding the words

        static Regex finder;
        static Dictionary<string, Entry> byWord;

        static void Build()
        {
            byWord = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in All) foreach (var w in e.words) if (!byWord.ContainsKey(w)) byWord[w] = e;
            // longest first, so "radial decoupler" wins over "decoupler" and "transfer window" over "transfer"
            var alts = byWord.Keys.OrderByDescending(w => w.Length).Select(w => Regex.Escape(w));
            finder = new Regex($@"(?<![\w-])({string.Join("|", alts)})(?![\w-])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);

        /// <summary>The definitions a piece of text mentions, in order (for tests and the page's search).</summary>
        public static List<Entry> Find(string text)
        {
            if (finder == null) Build();
            var list = new List<Entry>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (Match m in finder.Matches(Tags.Replace(text, "")))
            {
                var e = Lookup(m.Value);
                if (e != null && !list.Contains(e)) list.Add(e);
            }
            return list;
        }

        static Entry Lookup(string word)
        {
            if (byWord.TryGetValue(word, out var e))
            {
                // short capitals only count in capitals ("Ap", "Pe", "EC", "SAS"; not "ap" inside a sentence, "ec" or "sas")
                bool caps = e.words.Any(w => w.Length <= 4 && string.Equals(w, word, StringComparison.OrdinalIgnoreCase) && w.Any(char.IsUpper));
                if (caps && !e.words.Any(w => w == word)) return null;
                return e;
            }
            return null;
        }

        // ---------------------------------------------------------------- hovering

        static Entry candidate, hovered;
        static float hoverSince;

        /// <summary>
        /// Called for every label and button as it's drawn: if the mouse is over one of its words that has a definition,
        /// that word is the one being hovered this frame. Cheap unless the mouse is over the text.
        /// </summary>
        public static void Scan(Rect r, string text, GUIStyle style)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.Repaint || string.IsNullOrEmpty(text) || Settings.DefsDelay <= 0 || page) return;
            var m = e.mousePosition;
            if (!r.Contains(m)) return;
            if (finder == null) Build();
            string plain = Tags.Replace(text, "");
            var matches = finder.Matches(plain);
            if (matches.Count == 0) return;
            // which letter the mouse is over (rich text tags stripped, so it's near enough)
            int at = -1;
            try
            {
                var st = new GUIStyle(style ?? GUI.skin.label) { richText = false };
                at = st.GetCursorStringIndex(r, new GUIContent(plain), m);
            }
            catch { }
            Entry best = null;
            foreach (Match mt in matches)
            {
                var en = Lookup(mt.Value);
                if (en == null) continue;
                if (at < 0) { if (matches.Count == 1) best = en; break; }
                if (at >= mt.Index - 1 && at <= mt.Index + mt.Length + 1) { best = en; break; }
            }
            if (best != null) candidate = best;
        }

        /// <summary>Draws the definition box once the mouse has rested on a word long enough. Call last thing in OnGUI.</summary>
        public static void DrawTip()
        {
            var ev = Event.current;
            if (ev.type == EventType.Repaint)
            {
                if (candidate != hovered) { hovered = candidate; hoverSince = Time.unscaledTime; }
                candidate = null;
            }
            if (hovered == null || Settings.DefsDelay <= 0 || Time.unscaledTime - hoverSince < Settings.DefsDelay) return;
            var e = hovered;
            var head = new GUIStyle(Ui.Bold) { fontSize = 14 };
            head.normal.textColor = Ui.Accent;
            var body = new GUIStyle(Ui.Small) { wordWrap = true };
            body.normal.textColor = new Color(0.86f, 0.9f, 0.94f);
            float w = 330;
            string also = AlsoCalled(e);
            string text = e.what + (also != null ? $"\n<color=#8a96a2>Also called: {also}</color>" : "");
            float h = body.CalcHeight(new GUIContent(text), w - 20) + 34;
            var mouse = new Vector2(In.MousePosition.x / Ui.Scale, (Screen.height - In.MousePosition.y) / Ui.Scale);
            float x = Mathf.Clamp(mouse.x + 16, 6, Ui.W - w - 6), y = mouse.y + 22;
            if (y + h > Ui.H - 6) y = mouse.y - h - 10;
            var r = new Rect(x, Mathf.Max(6, y), w, h);
            GUI.Box(r, GUIContent.none, Ui.Panel);
            GUI.Box(r, GUIContent.none, Ui.Panel);   // twice: darker, so it reads over anything
            GUI.Label(new Rect(r.x + 10, r.y + 6, w - 20, 22), e.term, head);
            GUI.Label(new Rect(r.x + 10, r.y + 28, w - 20, h - 32), text, body);
        }

        static string AlsoCalled(Entry e)
        {
            // other names, leaving out forms of the same word ("burning" for burn, "stages" for staging)
            string root = e.term.ToLowerInvariant();
            root = root.Substring(0, Math.Min(4, root.Length));
            var others = e.words.Where(w => !w.ToLowerInvariant().StartsWith(root) && !(w.EndsWith("s") && e.words.Contains(w.Substring(0, w.Length - 1))))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
            return others.Count > 0 ? string.Join(", ", others) : null;
        }

        // ---------------------------------------------------------------- the Definitions page

        /// <summary>Open as a window over the game (from the builder or the pause menu).</summary>
        public static bool Open;
        static bool page;   // the page is being drawn: its own words don't pop up boxes
        static string search = "";
        static Vector2 scroll;
        static int group = -1;

        /// <summary>The Definitions window over the game, when open. Esc or Close shuts it.</summary>
        public static void DrawWindow()
        {
            if (!Open) return;
            float W = Ui.W, H = Ui.H;
            var dim = new Rect(0, 0, W, H);
            Ui.Dim(dim, 0.5f);
            Ui.Press(dim, GUIContent.none, GUIStyle.none);   // eats clicks behind the window
            var r = new Rect(W / 2 - 470, Mathf.Max(30, H / 2 - 330), 940, Mathf.Min(660, H - 60));
            if (!DrawPage(r) || In.EscapeDown) Open = false;
        }

        /// <summary>The searchable list of every definition. Returns false when Close is pressed.</summary>
        public static bool DrawPage(Rect r)
        {
            page = true;
            Ui.Box(r); Ui.Box(r);
            GUI.Label(new Rect(r.x + 16, r.y + 12, 300, 26), "Definitions", Ui.Title);
            GUI.Label(new Rect(r.x + 180, r.y + 18, r.width - 400, 20), $"Rest the mouse on a word anywhere in the game for {(Settings.DefsDelay <= 0 ? "a moment (switched off in Settings)" : $"{Settings.DefsDelay:0} s")} to see what it means.", Ui.Small);
            bool keep = !Ui.Press(new Rect(r.xMax - 106, r.y + 12, 90, 30), "Close", Ui.Button);
            // search and groups
            GUI.Label(new Rect(r.x + 16, r.y + 56, 60, 24), "Search", Ui.Label);
            GUI.SetNextControlName("defsSearch");
            search = GUI.TextField(new Rect(r.x + 80, r.y + 52, 260, 28), search ?? "", 40, Ui.Field);
            float gx = r.x + 356;
            for (int i = -1; i < Groups.Length; i++)
            {
                string name = i < 0 ? "All" : Groups[i];
                float w = Ui.ButtonSmall.CalcSize(new GUIContent(name)).x + 18;
                if (gx + w > r.xMax - 12) break;
                if (Ui.Press(new Rect(gx, r.y + 54, w, 26), name, group == i ? Ui.ButtonSmallSel : Ui.ButtonSmall)) { group = i; scroll = Vector2.zero; }
                gx += w + 4;
            }
            // the list
            string q = (search ?? "").Trim();
            var list = All.Where(e => (group < 0 || e.group == Groups[group]) &&
                (q.Length == 0 || e.term.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || e.words.Any(w => w.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) || q.Length > 3 && e.what.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(e => q.Length > 0 && !e.term.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 1 : 0).ThenBy(e => Array.IndexOf(Groups, e.group)).ThenBy(e => e.term).ToList();
            var view = new Rect(r.x + 10, r.y + 92, r.width - 20, r.height - 104);
            var body = new GUIStyle(Ui.Small) { wordWrap = true };
            var head = new GUIStyle(Ui.Bold);
            head.normal.textColor = new Color(0.62f, 0.86f, 1f);
            float tw = view.width - 230 - 24, total = 0;
            var heights = new List<float>();
            foreach (var e in list) { string t = Body(e); float h = Mathf.Max(36, body.CalcHeight(new GUIContent(t), tw)) + 12; heights.Add(h); total += h; }
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 18, Mathf.Max(total, 10)));
            float y = 0;
            if (list.Count == 0) GUI.Label(new Rect(8, 4, view.width - 30, 24), $"No word matches \"{q}\".", Ui.Small);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (y + heights[i] >= scroll.y && y <= scroll.y + view.height)
                {
                    if (i % 2 == 0) Ui.Line(new Rect(0, y, view.width - 18, heights[i]), new Color(1, 1, 1, 0.03f));
                    GUI.Label(new Rect(8, y + 5, 220, 22), e.term, head);
                    GUI.Label(new Rect(8, y + 25, 220, 18), e.group, new GUIStyle(Ui.Small) { fontSize = 11 });
                    GUI.Label(new Rect(236, y + 6, tw, heights[i] - 8), Body(e), body);
                }
                y += heights[i];
            }
            GUI.EndScrollView();
            page = false;
            return keep;
        }

        static string Body(Entry e)
        {
            string also = AlsoCalled(e);
            return e.what + (also != null ? $"  <color=#8a96a2>(Also called: {also})</color>" : "");
        }
    }
}
