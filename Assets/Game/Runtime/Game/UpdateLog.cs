using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 34: the update log. The title screen shows the newest update's highlights in a "What's new" card (marked
    /// NEW until it's been read); "Update log" lists every update, newest first.
    /// </summary>
    public static class UpdateLog
    {
        public struct Update
        {
            public int n;
            public string name, date;
            public string[] what;
        }

        static Update U(int n, string date, string name, params string[] what) => new Update { n = n, date = date, name = name, what = what };

        /// <summary>Every update, newest first.</summary>
        public static readonly Update[] All =
        {
            U(39, "Nov 15, 2026", "Out there",
                "Random places to find on every world, different in every game: supply caches, camps, crashed probes, depots... ten within driving distance of the space centre",
                "Things happen: lightning, dust devils, wind and heat waves where there's air; meteor showers and a strange shimmer on airless worlds; solar flares, micrometeoroids and blackouts in space",
                "Meteor showers leave meteorite chunks to pick up and sell",
                "Engines heat up as they run: past 85% they shut themselves down until they cool. Radiators cool engines now, and there's a coolant tank",
                "Parts can be damaged (weaker engines, leaking tanks): fix them with a repair kit, or an engineer on foot"),
            U(38, "Nov 15, 2026", "The survival kit",
                "Every Ardling has health, food and stamina: running uses stamina, on foot you get hungry, falls, heavy g and a burning cabin hurt",
                "Knocked out? A revive kit in your backpack gets you up; otherwise a teammate with one has 90 seconds",
                "Things lie about on the worlds: search an outpost or wreck and loot scatters round you. F picks up, R throws, Z crouches",
                "Your backpack (I, then 1-8 to use): food, medicine, fuel canisters and batteries for a ship in reach, repair kits, a scanner, a torch",
                "It keeps what's in it between flights; the supply store at the space centre sells kit, buys your finds and keeps a locker"),
            U(37, "Nov 14, 2026", "Realistic cabins",
                "Real lamps light the cabin: light pools under them, corners go darker, and the sun no longer shines through the walls",
                "Sunlight falls through the windows onto the floor and moves as the ship turns; close a blind and it's gone",
                "Textured surfaces: woven seat cloth, studded rubber floors, painted panels with seams and scratches, quilted pads, perforated grilles, brushed metal rails",
                "Night red really turns the lamps red; FLOOD brightens a lamp over the desk; the screens throw a blue glow",
                "The crew in the cabin are lit by the lamps too"),
            U(36, "Nov 14, 2026", "A cabin you could live in",
                "An overhead panel: 16 circuit breakers that really cut things, FLOOD / PANEL / VOLUME / TEMP knobs, guarded switches and LAMP TEST",
                "A keypad computer: type a program number and ENT (1 height, 2 speed, 3 fuel, 4 high point, 5 clock... and a few secrets)",
                "Real instruments: needle gauges for fuel, power, climb and g, and an attitude ball that turns with the ship",
                "MASTER ALARM flashes and beeps for new trouble until you press it; CAUTION stays lit while it lasts",
                "The cockpit shakes (not your view) under thrust and in reentry, the lights flicker, and the windows glow orange",
                "In space, loose things float about the cabin: push them around; with weight again they drop to the floor",
                "Glass and bolts in every window, fluttering vent streamers, the fans' hum, clicks and thuds for every switch",
                "Lived in: stowage bags, Velcro with a tablet, camera and torch, bungee cords with snacks, a photo from home, a mission patch, sticky notes",
                "Seats with bolsters, foot rests and a hand controller; a food warmer in habitats"),
            U(35, "Nov 14, 2026", "Lessons, crashes and the sea",
                "Tutorials: three new lessons (get into orbit, land on Tal, dock with a station) that set you up and talk you through it",
                "A game opens at the space centre: click a building to go in",
                "Easy mode helps you fly: tougher parts, stages fire when they run dry, parachutes and legs open by themselves, SAS starts on",
                "Easy autopilot: Fly to Tal (or Vesk) takes you from the pad to the moon and lands you; Land brings you down from any moon orbit",
                "Parts aren't all identical: each gives way somewhere around its rating",
                "Crashes break the rocket apart: pieces fly, tumble and come to rest, scorched, with smoke trails",
                "Splash down and bob on the waves; Ardlings swim (front crawl) and tread water",
                "Ardlings: new walking, running and jumping poses, a smaller helmet and nicer sticky notes; 3 flags per trip outside",
                "Green LAUNCH and Start buttons, red Abort",
                "Cabins you can use: look at something and press E. STAGE, ABORT, the switches, the throttle and the map all work; the screens are live",
                "Things to play with inside: the radio, blinds, night lights, a snack locker, a plant, a plushie, an exercise bike, chess, and lots more detail",
                "In multiplayer you see the others walking round the cabin with their names, and what anyone opens, shuts or switches, everyone sees",
                "Finish the story and the credits roll; the title screen just has a small Credits link in the corner now"),
            U(34, "Nov 14, 2026", "Expedition on the real game",
                "Expedition rebuilt: everyone builds one rocket with the real parts and rides in it as an Ardling, through the real solar system",
                "Six missions with a budget and goals, the six resources in the plan, supplies to survive on and a scored report with a leaderboard",
                "Outposts and wrecks on the moons and planets: land beside one and Scavenge it for fuel, supplies and science",
                "Walk inside your capsule or habitat (TAB) and look out of the windows; climb out after landing (ENTER), first or third person (C)",
                "Paint your rocket, stick on decals (even your own pictures), save your own presets, and dress your Ardling for multiplayer",
                "Definitions: rest the mouse on a game word (like \"radial in\") for a quick explanation, or open the Definitions page",
                "A new title screen and this update log; hide the interface with F1; multiplayer panels fold away",
                "Fixed: players who join a room now see engine flames and smoke; every new game no longer starts in the same storm"),
            U(32, "Nov 14, 2026", "An easier builder",
                "Fixed the rocket disappearing and many other builder bugs",
                "The interface fits any screen by itself, from a small laptop to an ultrawide",
                "A parts search, 22 ready-made rockets, 10 new parts and an interactive tutorial",
                "Backspace deletes, placing a part lets go of it, and Easy mode gives building tips",
                "Steadier multiplayer and better drag near the speed of sound"),
            U(31, "Nov 14, 2026", "The Space Apps check",
                "Compare two mission designs side by side",
                "See the real NASA missions your design is like",
                "Teams on the same network race on the same mission"),
            U(30, "Nov 13, 2026", "Multiplayer rocket rooms",
                "Build and fly one rocket with up to 4 players: builder, pilot, flight engineer and mission control",
                "Campaign rooms share the host's space program",
                "A new title menu: Singleplayer and Multiplayer, each Campaign or Sandbox"),
            U(29, "Nov 13, 2026", "Mission design and team rooms",
                "Plan a whole science mission on the real worlds, fly it day by day and get a report and a score",
                "Rooms for up to 4 players on their own devices, with a code to join"),
            U(28, "Nov 13, 2026", "Full graphics and the story",
                "Bloom, colour grading and brighter nights lit by Tal and Brume",
                "Story missions, flight replays and practice tools"),
            U(27, "Nov 13, 2026", "Places to discover",
                "Landmarks to find, engine dust, footprints and wheel tracks",
                "Photo mode (F2), weather, and a docking guide"),
            U(26, "Nov 13, 2026", "Countdown and booster recovery",
                "A launch countdown, boosters that fly home and land",
                "Mountains and craters on every world"),
            U(25, "Nov 13, 2026", "Launch pads and the space centre",
                "Several launch pads and a roll-out to the pad",
                "Click round the space centre to go anywhere"),
            U(24, "Nov 12, 2026", "Friendlier",
                "Easy, Standard and Engineer words",
                "Steadier reentry, new Ardlings, a calmer flight screen"),
            U(23, "Nov 12, 2026", "Polish for release",
                "Achievements, damage and chain explosions",
                "A real space centre with crowds, and the title screen on the pad"),
            U(22, "Nov 11, 2026", "Bases", "Build bases on other worlds"),
            U(21, "Nov 11, 2026", "Music and sound", "Music for every moment and recorded sounds"),
            U(20, "Nov 11, 2026", "The realistic look", "Realistic graphics, sharp worlds and ground photos"),
            U(19, "Nov 11, 2026", "Supplies", "Food, water and air for long voyages"),
            U(18, "Nov 10, 2026", "Saves and settings", "Save slots, a title screen and settings"),
            U(17, "Nov 10, 2026", "The flight computer", "Let the computer fly burns and landings for you"),
            U(16, "Nov 10, 2026", "A giant", "Brume, a gas giant with moons"),
            U(15, "Nov 10, 2026", "Time and planning", "An intercept planner and events"),
            U(14, "Nov 10, 2026", "Wanderers", "Rocks passing through Arden's space, and a grabber to catch them"),
            U(13, "Nov 10, 2026", "Polish and stability", "Fixes everywhere, and a first-flight guide"),
            U(12, "Nov 10, 2026", "Getting there in style", "Aerobraking and heat shields"),
            U(11, "Nov 9, 2026", "The sun and Ochre", "A real sun, and Ochre, the red desert planet"),
            U(10, "Nov 9, 2026", "Regions and rovers", "Science regions on each world, and rovers to drive"),
            U(9, "Nov 9, 2026", "A second moon", "Vesk, and quicksave"),
            U(8, "Nov 9, 2026", "Sky, ground and sound", "Real terrain, sky and light, sound, detailed parts"),
            U(7, "Nov 9, 2026", "Crew", "Crew roles and experience, and EVA"),
            U(6, "Nov 8, 2026", "Power and resources", "Power, comms, ore and fuel making"),
            U(5, "Nov 8, 2026", "Career", "Contracts, money, science and the space centre"),
            U(4, "Nov 8, 2026", "A persistent universe", "Many ships at once, rendezvous and docking"),
            U(3, "Nov 8, 2026", "Builder and spaceplanes", "A new builder, and planes"),
            U(2, "Nov 8, 2026", "Going to the moon", "Maneuvers, the map and Tal"),
            U(1, "Nov 8, 2026", "First flight", "Build a rocket and fly it to orbit"),
        };

        public static Update Latest => All[0];

        /// <summary>True until the newest update has been looked at.</summary>
        public static bool Unread
        {
            get { try { return !Universe.IsAutotest && PlayerPrefs.GetInt("seenUpdate", 0) < Latest.n; } catch { return false; } }
        }
        public static void MarkRead() { try { if (!Universe.IsAutotest) { PlayerPrefs.SetInt("seenUpdate", Latest.n); PlayerPrefs.Save(); } } catch { } }

        static GUIStyle bullet, head, small;
        static void Styles()
        {
            if (bullet != null) return;
            bullet = new GUIStyle(Ui.Small) { wordWrap = true, fontSize = 13 };
            bullet.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
            head = new GUIStyle(Ui.Header) { fontSize = 15 };
            small = new GUIStyle(Ui.Small) { fontSize = 11 };
        }

        /// <summary>The "What's new" card on the title screen. Returns true when "Read the update log" is pressed.</summary>
        public static bool DrawCard(Rect r, int maxLines)
        {
            Styles();
            var u = Latest;
            Ui.Box(r);
            if (Unread)
            {
                // a gold NEW tag that breathes gently
                float a = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
                Ui.Line(new Rect(r.x + 14, r.y + 13, 40, 20), new Color(1f, 0.78f, 0.25f, a));
                Ui.Text(new Rect(r.x + 14, r.y + 13, 40, 20), "<b><color=#1a1306>NEW</color></b>", new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
            }
            float hx = Unread ? r.x + 62 : r.x + 14;
            Ui.Text(new Rect(hx, r.y + 10, r.width - (hx - r.x) - 14, 24), $"WHAT'S NEW  <color=#8fa3b3><size=13>· update {u.n}</size></color>", head);
            Ui.Text(new Rect(r.x + 14, r.y + 34, r.width - 28, 22), $"<b>{u.name}</b>", Ui.Label);
            float y = r.y + 60;
            int shown = 0;
            foreach (var line in u.what)
            {
                if (shown >= maxLines) break;
                float h = bullet.CalcHeight(new GUIContent(line), r.width - 46);
                if (y + h > r.yMax - 44) break;
                Ui.Line(new Rect(r.x + 18, y + 7, 5, 5), Ui.Accent);
                Ui.Text(new Rect(r.x + 30, y, r.width - 46, h), line, bullet);
                y += h + 4;
                shown++;
            }
            return Ui.Press(new Rect(r.x + 14, r.yMax - 38, r.width - 28, 28), "Read the full update log  ▸", Ui.ButtonSmall);
        }

        static Vector2 scroll;

        /// <summary>The full update log. Returns false when Back is pressed.</summary>
        public static bool DrawPage(Rect r)
        {
            Styles();
            MarkRead();
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 16, r.y + 12, 300, 26), "Update log", Ui.Title);
            Ui.Text(new Rect(r.x + 170, r.y + 19, r.width - 300, 20), "Everything new in Arden Space Program, newest first.", Ui.Small);
            var view = new Rect(r.x + 8, r.y + 50, r.width - 16, r.height - 106);
            float tw = view.width - 60;
            float total = 0;
            foreach (var u in All) total += Height(u, tw);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 18, total));
            float y = 0;
            foreach (var u in All)
            {
                float h = Height(u, tw);
                if (y + h >= scroll.y && y <= scroll.y + view.height)
                {
                    Ui.Text(new Rect(10, y + 6, tw, 22), $"<b>Update {u.n}</b>  ·  {u.name}", u.n == Latest.n ? head : Ui.Label);
                    Ui.Text(new Rect(10, y + 6, view.width - 40, 22), u.date, new GUIStyle(small) { alignment = TextAnchor.MiddleRight });
                    float yy = y + 32;
                    foreach (var line in u.what)
                    {
                        float lh = bullet.CalcHeight(new GUIContent(line), tw - 20);
                        Ui.Line(new Rect(16, yy + 7, 4, 4), new Color(0.55f, 0.75f, 0.9f));
                        Ui.Text(new Rect(28, yy, tw - 20, lh), line, bullet);
                        yy += lh + 3;
                    }
                    Ui.Line(new Rect(10, y + h - 6, view.width - 40, 1), new Color(1, 1, 1, 0.08f));
                }
                y += h;
            }
            GUI.EndScrollView();
            return !Ui.Press(new Rect(r.x + 16, r.yMax - 48, 100, 34), "Back", Ui.Button);
        }

        static float Height(Update u, float tw)
        {
            float h = 32;
            foreach (var line in u.what) h += bullet.CalcHeight(new GUIContent(line), tw - 20) + 3;
            return h + 12;
        }
    }
}
