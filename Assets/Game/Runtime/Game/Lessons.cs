using System;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 35 (captaincarl24: "add more tutorials like how to get into orbit, how to land on the moon, how to dock
    /// with another space station"): guided lessons. Each one sets you up where it starts (on the pad with an orbiter;
    /// in orbit round Tal in a lander; behind a station in a ferry), then a box talks you through it step by step and
    /// waits until you've done each one. Crash and you can try again. Lesson ships are called "Lesson: ..." and don't stay
    /// in your save. Opened from Tutorials in the builder's File menu.
    /// </summary>
    public static class Lessons
    {
        public class Step
        {
            public string title, text;
            public Func<FlightMode, bool> done;   // null: press Next
        }

        public class Lesson
        {
            public string name, blurb;
            public Step[] steps;
        }

        static Step S(string title, string text, Func<FlightMode, bool> done = null) => new Step { title = title, text = text, done = done };

        static double Alt(FlightMode f) => f.active.Altitude;
        static Orbit O(FlightMode f) => f.trajectory.Count > 0 ? f.trajectory[0].orbit : null;
        static double Apo(FlightMode f) { var o = O(f); return o == null ? 0 : o.IsElliptic ? o.Apoapsis - f.active.body.radius : 1e12; }
        static double Peri(FlightMode f) { var o = O(f); return o == null ? -1e9 : o.Periapsis - f.active.body.radius; }
        static double Up(FlightMode f) => Vector3d.Dot(f.active.SurfaceRelVelocity, f.active.pos.normalized);
        static float Tilt(FlightMode f) => Vector3.Angle(f.active.rot * Vector3.up, f.active.pos.normalized.ToFloat());

        public static readonly Lesson[] All =
        {
            new Lesson { name = "Get into orbit", blurb = "From the pad to going round Arden: the gravity turn, the highest point, and the burn that rounds it out.",
                steps = new[]
                {
                    S("Lift off", "Press the green LAUNCH (or SPACE) to light the engine. T turns SAS on, so the rocket holds steady.", f => f.active.hasLaunched && Alt(f) > 300),
                    S("The gravity turn", "Above 1 km, start leaning the nose east (towards 90 on the navball) with the steering keys. Aim for about 10 degrees off straight up by 3 km.", f => Alt(f) > 3000 && Tilt(f) > 7f),
                    S("Keep turning", "Let the rocket follow its path: the Fwd button by the navball (prograde) does it for you. By 12 km the nose should be about halfway down.", f => Alt(f) > 12000 && Tilt(f) > 25f),
                    S("Raise the highest point", "Watch Highest point in the top-left box. When it reaches about 80 km, cut the engine with X.", f => Apo(f) > 75000 && f.active.throttle < 0.02),
                    S("Coast to the top", "Coast up to the highest point. Press M to see it on the map; time warp (.) gets you there faster, and / stops it.", f => Alt(f) > 60000 && Up(f) < 60 || Peri(f) > 70000),
                    S("Make it round", "Point forward (the Fwd button) and burn full (Z). Watch Lowest point climb: when it's above 70 km, cut the engine (X).", f => Peri(f) > 70000 && f.active.throttle < 0.02),
                    S("You're in orbit!", "You're falling round Arden, fast enough sideways to keep missing the ground. No fuel needed to stay up here. Well done!"),
                } },
            new Lesson { name = "Land on Tal", blurb = "From orbit round Tal to standing on it: slow down, fall, burn again, and touch down gently.",
                steps = new[]
                {
                    S("In orbit round Tal", "Your lander is going round Tal, 25 km up. Tal has no air, so no parachutes: the engine does all the slowing."),
                    S("Point backwards", "Press the Back button by the navball (retrograde) to point against the way you're moving.", f => f.active.sas == SasMode.Retrograde),
                    S("Slow down", "Burn (Z, or SHIFT for gently) until Lowest point drops below the ground: your path now meets Tal. Then cut the engine (X).", f => Peri(f) < 0 && f.active.throttle < 0.02),
                    S("Legs down", "Press G to put the landing legs down.", f => f.active.gearDown),
                    S("The landing burn", "Keep pointing Back. As you get low, burn to slow down: keep your Surface speed under your height in metres divided by 10, and below 5 m/s at the very end.", f => f.active.landed),
                    S("You landed on Tal!", "Press ENTER to climb out and walk round (C changes the view), or G to plant a flag. Well done!"),
                } },
            new Lesson { name = "Dock with a station", blurb = "Meet a station in orbit, line up with its docking port, and drift in slowly until the ports latch.",
                steps = new[]
                {
                    S("Your target", "The station ahead is your target: the purple marks on the navball point at it. Press T for SAS and R for RCS (little puffers).", f => f.active.sas != SasMode.Off && f.active.rcsOn),
                    S("Line up", "Press Dock by the navball (or Tgt) so your docking port points at theirs.", f => f.active.sas == SasMode.DockAlign || f.active.sas == SasMode.Target),
                    S("Drift in", "H pushes you forward and N back; I and K move you up and down, J and L sideways. Close in at about 1 m/s.", f => f.dockDist < 15),
                    S("Slow and steady", "Slow down to under 0.3 m/s and keep lined up with the guide on screen. The ports latch when they touch.", f => f.active.parts.Any(p => p.dockedTo >= 0)),
                    S("Docked!", "You've joined the station. Crew can move between the two, and you can undock from the crew panel. Well done!"),
                } },
        };

        public static int Current = -1, StepIndex;
        public static bool Active => Current >= 0;
        static VesselSim lessonShip;

        /// <summary>Starts a lesson: sets the scene and flies it.</summary>
        public static void Begin(int which)
        {
            var root = GameRoot.I;
            if (root == null || which < 0 || which >= All.Length) return;
            Current = which;
            StepIndex = 0;
            Tutorial.Stop(false);
            var u = root.universe;
            var home = root.home;
            switch (which)
            {
                case 0:
                {
                    var d = Presets.Orbiter();
                    d.name = "Lesson: Orbiter";
                    root.LaunchLesson(d);
                    lessonShip = root.flight.active;
                    break;
                }
                case 1:
                {
                    var tal = root.bodies.First(b => b.name == "Tal");
                    var d = VesselDesign.Stack("Lesson: Tal lander", "chute.s", "pod.wren", "shield.s", "tank.s3", "eng.wisp");
                    DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s3"), 0.95f, 45f, "leg.s", 4, false);
                    DesignOps.ApplyDefaultGroups(d);
                    double r = tal.radius + 25_000, sp = Math.Sqrt(tal.mu / r);
                    var pos = new Vector3d(r, 0, 0);
                    var vel = new Vector3d(0, 0, -sp);
                    var v = VesselSim.CreateFree(d, tal, pos, vel, Quaternion.LookRotation(pos.normalized.ToFloat(), vel.normalized.ToFloat()));
                    foreach (var p in v.parts) if (p.def.IsEngine) p.ignited = true;
                    v.nextStage = v.stages.Count;   // engine already lit, nothing left to stage by accident
                    v.throttle = 0;
                    v.hasLaunched = true;
                    v.career = u.career;
                    u.Add(v);
                    lessonShip = v;
                    root.Fly(v);
                    root.flight.SetSas(SasMode.Stability);
                    break;
                }
                case 2:
                {
                    double r = home.radius + 100_000, sp = Math.Sqrt(home.mu / r), ph = 0.5;
                    Vector3d P(double a) => new Vector3d(r * Math.Cos(a), 0, -r * Math.Sin(a));
                    Vector3d V(double a) => new Vector3d(-sp * Math.Sin(a), 0, -sp * Math.Cos(a));
                    var st = VesselSim.CreateFree(VesselDesign.Outpost("Lesson: Station"), home, P(ph), V(ph), Quaternion.LookRotation(P(ph).normalized.ToFloat(), (-V(ph)).normalized.ToFloat()));
                    st.throttle = 0;
                    foreach (var p in st.parts) { if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1f; } if (p.def.kind == PartKind.Habitat) p.crew.Clear(); }
                    st.RecomputeMass();
                    u.Add(st);
                    double back = 70.0 / r;
                    var ferry = VesselSim.CreateFree(VesselDesign.FerryCraft("Lesson: Ferry"), home, P(ph - back), V(ph - back), Quaternion.LookRotation(P(ph - back).normalized.ToFloat(), V(ph - back).normalized.ToFloat()));
                    ferry.throttle = 0;
                    ferry.hasLaunched = true;
                    ferry.career = u.career;
                    u.Add(ferry);
                    lessonShip = ferry;
                    root.Fly(ferry);
                    root.flight.SetTarget(st);
                    break;
                }
            }
        }

        public static void Stop() { Current = -1; lessonShip = null; }

        /// <summary>The lesson's box, in flight. Moves on when a step's done; offers a retry after a crash.</summary>
        public static void Draw(FlightMode f)
        {
            if (!Active || f?.active == null) return;
            var lesson = All[Current];
            if (StepIndex >= lesson.steps.Length) { Stop(); return; }
            var s = lesson.steps[StepIndex];
            bool crashed = f.active.destroyed;
            if (!crashed && s.done != null && s.done(f)) { StepIndex++; GameAudio.I?.Click(); return; }
            float W = Ui.W, bw = 420, tw = bw - 28;
            string text = crashed ? "That didn't go to plan, and that's how everyone learns. Try the lesson again from the start." : Words.Say(s.text);
            float th = Ui.Small.CalcHeight(new GUIContent(text), tw);
            var box = new Rect(10, 306, bw, 40 + th + 48);   // left, under the flight panels (clear of the messages in the middle)
            Ui.Box(box); Ui.Box(box);
            Ui.Text(new Rect(box.x + 14, box.y + 10, bw - 120, 24), crashed ? "Crashed!" : s.title, Ui.Header);
            Ui.Text(new Rect(box.xMax - 110, box.y + 12, 96, 20), $"{lesson.name}  {StepIndex + 1}/{lesson.steps.Length}", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight, fontSize = 11 });
            Ui.Text(new Rect(box.x + 14, box.y + 38, tw, th), text, Ui.Small);
            float by = box.yMax - 38;
            if (Ui.Press(new Rect(box.x + 14, by, 110, 28), "Stop lesson", Ui.ButtonSmall)) { Stop(); return; }
            if (crashed)
            {
                if (Ui.Press(new Rect(box.xMax - 124, by, 110, 28), "Try again", Ui.ButtonGoSmall)) { int which = Current; GameRoot.I.EnterHangar(); Begin(which); }
                return;
            }
            bool last = StepIndex == lesson.steps.Length - 1;
            if (s.done == null && Ui.Press(new Rect(box.xMax - 110, by, 96, 28), last ? "Finish" : "Next  ▸", Ui.ButtonGoSmall)) { StepIndex++; if (last) Stop(); }
            else if (s.done != null) Ui.Text(new Rect(box.xMax - 160, by + 4, 146, 22), "<color=#ffd56a>your turn!</color>", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight });
        }

        // ---------------------------------------------------------------- the list (builder: File > Tutorials)

        public static bool PickerOpen;

        public static void DrawPicker()
        {
            if (!PickerOpen) return;
            float W = Ui.W, H = Ui.H;
            Ui.Dim(new Rect(0, 0, W, H), 0.5f);
            Ui.Press(new Rect(0, 0, W, H), GUIContent.none, GUIStyle.none);
            var r = new Rect(W / 2 - 300, H / 2 - 230, 600, 460);
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 18, r.y + 14, 400, 28), "Tutorials", Ui.Title);
            if (Ui.Press(new Rect(r.xMax - 44, r.y + 14, 30, 28), "×", Ui.ButtonSmall) || In.EscapeDown) { PickerOpen = false; return; }
            float y = r.y + 58;
            var wrap = new GUIStyle(Ui.Small) { wordWrap = true };
            void Row(string name, string blurb, Action go)
            {
                Ui.Line(new Rect(r.x + 14, y, r.width - 28, 82), new Color(1, 1, 1, 0.04f));
                Ui.Text(new Rect(r.x + 26, y + 8, 380, 24), name, Ui.Bold);
                Ui.Text(new Rect(r.x + 26, y + 32, r.width - 190, 44), blurb, wrap);
                if (Ui.Press(new Rect(r.xMax - 140, y + 24, 112, 34), "Start  ▸", Ui.ButtonGo)) { PickerOpen = false; go(); }
                y += 90;
            }
            Row("Build and fly your first rocket", "The first tutorial: put a rocket together part by part, launch it, steer and read the map.", Tutorial.Start);
            for (int i = 0; i < All.Length; i++) { int k = i; Row(All[i].name, All[i].blurb, () => Begin(k)); }
        }
    }
}
