using System;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 32 (captaincarl24: "make a more interactive tutorial instead of reading a manual ... like a box and like a
    /// text saying this is ..."): the first time you start a game, a box points at each part of the screen in turn,
    /// says what it is, and waits for you to do it: put down a capsule, a tank, an engine and a parachute, look at the
    /// stages, launch; then in flight light the engine, read the height and speed, steer, open the map. Everything
    /// around the thing it's pointing at is dimmed. It can be skipped at any time and played again from How to play.
    /// </summary>
    public static class Tutorial
    {
        enum Wait { Next, Command, Tank, Engine, Chute, Launch, Lit, Climb, MapOpen, MapClosed }
        enum Spot { None, Parts, Search, Rocket, Stages, Undo, Launch, Telemetry, Bottom }

        struct Step
        {
            public string title, text, easy, search;
            public Spot spot;
            public Wait wait;
            public bool flight;
        }

        static readonly Step[] Steps =
        {
            new Step { title = "Welcome to the rocket builder!", spot = Spot.None, wait = Wait.Next,
                text = "This quick tutorial shows you how to build a rocket and fly it, one step at a time. Skip it whenever you like.",
                easy = "Let's build a rocket and fly it together, one step at a time. You can skip this whenever you like." },
            new Step { title = "The parts list", spot = Spot.Parts, wait = Wait.Next,
                text = "Everything you can build with is here. Point at a part to read what it does.",
                easy = "These are all the parts you can use. Point at one to see what it does." },
            new Step { title = "Search", spot = Spot.Search, wait = Wait.Next,
                text = "Looking for something? Type here to find it: try \"engine\" or \"fin\".",
                easy = "Type here to find a part, like \"engine\" or \"fin\"." },
            new Step { title = "Start at the top: a capsule", spot = Spot.Parts, wait = Wait.Command, search = "command",
                text = "Click the Wren Capsule, then click in the middle of the hangar to put it down. It's the brain of the rocket, and holds the crew.",
                easy = "Click the Wren Capsule, then click in the middle of the hangar to put it down. It steers the rocket and carries the crew." },
            new Step { title = "Now a fuel tank", spot = Spot.Rocket, wait = Wait.Tank, search = "tank",
                text = "Click a tank in the list (the FT-2 is a good size), then click the green dot under the capsule.",
                easy = "Click a fuel tank in the list, then click the green dot under the capsule to stick it on." },
            new Step { title = "Then an engine", spot = Spot.Rocket, wait = Wait.Engine, search = "kestrel",
                text = "Click the Kestrel Lifter and put it under the tank. Tip: a part stays in your hand after you place it, so you can add more. Right-click to put it down.",
                easy = "Click the Kestrel Lifter and stick it under the tank. The part stays in your hand for more: right-click to let go." },
            new Step { title = "A parachute on top", spot = Spot.Rocket, wait = Wait.Chute, search = "parachute",
                text = "Put the Main Parachute on the very top of the capsule, so it lands softly when the flight's over.",
                easy = "Stick the parachute on the very top of the capsule, so it lands softly." },
            new Step { title = "The stages", spot = Spot.Stages, wait = Wait.Next,
                text = "These are your stages: what happens each time you press SPACE in flight. The numbers show how much push and fuel each one has.",
                easy = "These are the steps of your flight: each press of SPACE does the next one." },
            new Step { title = "Oops? Undo", spot = Spot.Undo, wait = Wait.Next,
                text = "Undo puts back your last change (Ctrl+Z, or Cmd+Z on a Mac). Backspace deletes the part under the mouse.",
                easy = "Undo takes back your last change (Ctrl+Z, or Cmd+Z on a Mac). Backspace deletes the part under the mouse." },
            new Step { title = "Ready to fly!", spot = Spot.Launch, wait = Wait.Launch,
                text = "Press LAUNCH to take your rocket to the pad.",
                easy = "Press LAUNCH to take your rocket to the launch pad." },
            // ---- in flight
            new Step { title = "Light the engine", spot = Spot.Bottom, wait = Wait.Lit, flight = true,
                text = "Press SPACE to start the engine and lift off!",
                easy = "Press SPACE to start the engine and take off!" },
            new Step { title = "How high, how fast", spot = Spot.Telemetry, wait = Wait.Next, flight = true,
                text = "This box shows your altitude and how fast you're climbing. Space starts at 70 km.",
                easy = "This shows how high you are and how fast you're going up. Space starts at 70 km." },
            new Step { title = "Steering", spot = Spot.None, wait = Wait.Climb, flight = true,
                text = "A and D lean the rocket left and right, W and S tip it forward and back. Keep it pointing up for now: watch it climb past 3 km.",
                easy = "A and D lean left and right, W and S tip forward and back. Keep it pointing up and watch it climb past 3 km." },
            new Step { title = "The map", spot = Spot.None, wait = Wait.MapOpen, flight = true,
                text = "Press M to see the map: the line is the path your rocket will follow.",
                easy = "Press M to see the map: the line shows where your rocket is going." },
            new Step { title = "Back to the rocket", spot = Spot.None, wait = Wait.MapClosed, flight = true,
                text = "Press M again to come back to your rocket.",
                easy = "Press M again to come back to your rocket." },
            new Step { title = "You've got it!", spot = Spot.None, wait = Wait.Next, flight = true,
                text = "When the engine runs out, the capsule falls back: the parachute opens by itself low down (or press SPACE). How to play, in the pause menu (ESC), explains everything else. Have fun!",
                easy = "When the fuel runs out the capsule falls back, and the parachute opens near the ground. Press ESC for How to play if you get stuck. Have fun!" },
        };

        static int step = -1;
        static bool offered;
        static float stepStart;

        public static bool Active => step >= 0 && step < Steps.Length;
        public static int StepIndex => step;
        public static int StepCount => Steps.Length;
        public static string StepTitle => Active ? Steps[step].title : null;

        /// <summary>Finished or skipped once (saved): it doesn't start by itself again.</summary>
        public static bool Done
        {
            get { try { return PlayerPrefs.GetInt("tutorialDone", 0) == 1; } catch { return true; } }
            set { if (Universe.IsAutotest) return; try { PlayerPrefs.SetInt("tutorialDone", value ? 1 : 0); PlayerPrefs.Save(); } catch { } }
        }

        /// <summary>Starts from the beginning (How to play's "Play the tutorial", or a first game).</summary>
        public static void Start() { step = 0; stepStart = Time.unscaledTime; offered = true; mapSeen = false; }
        public static void Stop(bool finished) { step = -1; Done = true; }
        /// <summary>For the screenshot tour: straight to a step.</summary>
        public static void TestGoto(int i) { step = Mathf.Clamp(i, 0, Steps.Length - 1); stepStart = Time.unscaledTime; offered = true; }

        static VesselDesign lastDesign;
        /// <summary>The builder calls this when a part goes on (or a preset's chosen).</summary>
        public static void Placed(VesselDesign d) { lastDesign = d; }

        static void Advance()
        {
            step++;
            stepStart = Time.unscaledTime;
            if (step >= Steps.Length) Stop(true);
        }

        static bool Has(VesselDesign d, Func<PartDef, bool> f) => d != null && d.nodes.Any(n => { var p = PartCatalog.Get(n.id); return p != null && f(p); });

        // ---------------------------------------------------------------- the builder

        static string searchSet;   // what the tutorial typed into the parts search, to clear it again afterwards

        public static void DrawBuilder(HangarMode h)
        {
            if (!Active && !offered && !Done && !Universe.IsAutotest) Start();   // the first game: it starts by itself
            if (searchSet != null && (!Active || Steps[step].search == null))
            {
                if (h.TutorialSearch == searchSet) h.TutorialSearch = "";
                searchSet = null;
            }
            if (!Active) return;
            var s = Steps[step];
            if (s.flight) return;   // waiting for the launch
            var d = h.design;
            // A rocket already in the hangar at the first building step: build your own from scratch, or fly this one.
            bool choosing = step == 3 && d.nodes.Count > 0;
            if (!choosing)
                switch (s.wait)
                {
                    case Wait.Command: if (Has(d, p => p.kind == PartKind.Command)) { Advance(); return; } break;
                    case Wait.Tank: if (Has(d, p => p.kind == PartKind.Tank || p.IsSolid)) { Advance(); return; } break;
                    case Wait.Engine: if (Has(d, p => p.IsEngine && !p.IsJet)) { Advance(); return; } break;
                    case Wait.Chute: if (Has(d, p => p.kind == PartKind.Parachute)) { Advance(); return; } break;
                }
            if (s.search != null && !choosing && h.TutorialSearch != s.search && Time.unscaledTime - stepStart < 0.2f) { h.TutorialSearch = s.search; searchSet = s.search; }
            Rect target = choosing ? h.RocketScreenRect() : s.spot == Spot.Parts ? h.PartsPanelRect : s.spot == Spot.Search ? h.SearchRect : s.spot == Spot.Stages ? h.StagePanelRect
                : s.spot == Spot.Undo ? h.UndoRect : s.spot == Spot.Launch ? h.LaunchRect : s.spot == Spot.Rocket ? h.RocketScreenRect() : default;
            var shown = s;
            if (choosing)
            {
                shown.title = "Build your own?";
                shown.text = "There's a rocket in the hangar already. Press Start fresh to build your own, one part at a time, or Next to fly this one.";
                shown.easy = "There's a rocket here already. Press Start fresh to build your own, or Next to fly this one.";
            }
            // adding a part: the list stays lit as well as the rocket (you pick the part there first)
            Rect lit = s.spot == Spot.Rocket && !choosing ? h.PartsPanelRect : default;
            int r = Draw(shown, target, s.wait == Wait.Next || choosing, choosing ? "Start fresh" : null, lit);
            if (r == 1 && choosing) { step = 7; stepStart = Time.unscaledTime; }   // on to the stages
            else if (r == 1) Advance();
            else if (r == 2) { h.TutorialClear(); h.TutorialSearch = "command"; stepStart = Time.unscaledTime; }
        }

        /// <summary>The launch button was pressed during the tutorial: on to the flight steps.</summary>
        public static void Launched()
        {
            if (!Active) return;
            if (!Steps[step].flight) { step = Array.FindIndex(Steps, x => x.flight); stepStart = Time.unscaledTime; }
        }

        // ---------------------------------------------------------------- the flight

        static bool mapSeen;

        public static void DrawFlight(FlightMode f)
        {
            if (!Active || !Steps[step].flight || f?.active == null || Coop.Active) return;
            var s = Steps[step];
            var v = f.active;
            switch (s.wait)
            {
                case Wait.Lit: if (v.hasLaunched || v.Altitude > 300 || v.throttle > 0.05 && v.thrustNow > 1) { Advance(); return; } break;
                case Wait.Climb: if (v.Altitude > 3000 || v.destroyed) { Advance(); return; } break;
                case Wait.MapOpen: if (f.mapView) { mapSeen = true; Advance(); return; } break;
                case Wait.MapClosed: if (!f.mapView) { Advance(); return; } break;
            }
            Rect target = s.spot == Spot.Telemetry ? f.TelemetryRect : default;
            int r = Draw(s, target, s.wait == Wait.Next || (s.wait == Wait.Climb && Time.unscaledTime - stepStart > 25f), null);
            if (r == 1) Advance();
        }

        // ---------------------------------------------------------------- drawing

        static readonly Color Dim = new Color(0f, 0.01f, 0.03f, 0.55f);

        /// <summary>Dims around the target, rings it, and shows the box. Returns 1 for Next, 2 for the extra button, 0 otherwise.</summary>
        static int Draw(Step s, Rect target, bool canNext, string extra, Rect alsoLit = default)
        {
            float W = Ui.W, H = Ui.H;
            bool spot = target.width > 2 && target.height > 2;
            if (spot)
            {
                target = Rect.MinMaxRect(Mathf.Max(0, target.xMin - 6), Mathf.Max(0, target.yMin - 6), Mathf.Min(W, target.xMax + 6), Mathf.Min(H, target.yMax + 6));
                // dim everything but the target (and anything else that should stay lit); it all stays clickable
                var hole = target;
                if (alsoLit.width > 2) hole = Rect.MinMaxRect(Mathf.Min(hole.xMin, alsoLit.xMin - 4), Mathf.Min(hole.yMin, alsoLit.yMin - 4), Mathf.Max(hole.xMax, alsoLit.xMax + 4), Mathf.Max(hole.yMax, alsoLit.yMax + 4));
                Ui.Line(new Rect(0, 0, W, hole.yMin), Dim);
                Ui.Line(new Rect(0, hole.yMax, W, H - hole.yMax), Dim);
                Ui.Line(new Rect(0, hole.yMin, hole.xMin, hole.height), Dim);
                Ui.Line(new Rect(hole.xMax, hole.yMin, W - hole.xMax, hole.height), Dim);
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                var ring = new Color(1f, 0.82f, 0.3f, pulse);
                Ui.Line(new Rect(target.xMin, target.yMin, target.width, 3), ring);
                Ui.Line(new Rect(target.xMin, target.yMax - 3, target.width, 3), ring);
                Ui.Line(new Rect(target.xMin, target.yMin, 3, target.height), ring);
                Ui.Line(new Rect(target.xMax - 3, target.yMin, 3, target.height), ring);
            }
            string text = Words.Easy ? s.easy ?? s.text : s.text;
            float bw = 400, tw = bw - 28;
            float th = Ui.Small.CalcHeight(new GUIContent(text), tw);
            float bh = 40 + th + 50;
            Rect box;
            if (!spot) box = new Rect(W * 0.5f - bw * 0.5f, s.spot == Spot.Bottom ? H - bh - 150 : H * 0.28f, bw, bh);
            else if (target.xMax + 16 + bw < W) box = new Rect(target.xMax + 16, Mathf.Clamp(target.center.y - bh * 0.5f, 10, H - bh - 10), bw, bh);
            else if (target.xMin - 16 - bw > 0) box = new Rect(target.xMin - 16 - bw, Mathf.Clamp(target.center.y - bh * 0.5f, 10, H - bh - 10), bw, bh);
            else box = new Rect(Mathf.Clamp(target.center.x - bw * 0.5f, 10, W - bw - 10), target.yMax + 16 + bh < H ? target.yMax + 16 : Mathf.Max(10, target.yMin - 16 - bh), bw, bh);
            Ui.Box(box); Ui.Box(box);
            Ui.Text(new Rect(box.x + 14, box.y + 10, bw - 90, 24), s.title, Ui.Header);
            Ui.Text(new Rect(box.xMax - 70, box.y + 12, 58, 20), $"{step + 1} / {Steps.Length}", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight });
            Ui.Text(new Rect(box.x + 14, box.y + 38, tw, th), text, Ui.Small);
            int result = 0;
            float by = box.yMax - 40;
            if (Ui.Press(new Rect(box.x + 14, by, 116, 28), "Skip tutorial", Ui.ButtonSmall)) { Stop(false); return 0; }
            if (extra != null && Ui.Press(new Rect(box.x + 138, by, 110, 28), extra, Ui.ButtonSmall)) result = 2;
            if (canNext && Ui.Press(new Rect(box.xMax - 110, by, 96, 28), step == Steps.Length - 1 ? "Finish" : "Next  ▸", Ui.ButtonSel)) result = 1;
            else if (!canNext) Ui.Text(new Rect(box.xMax - 160, by + 4, 146, 22), "<color=#ffd56a>your turn!</color>", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight });
            return result;
        }
    }
}
