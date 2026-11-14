using System;

namespace Rocket
{
    /// <summary>What the first-flight guide looks at, filled in by the game each frame.</summary>
    public struct GuideState
    {
        public bool inFlight;      // a vessel is being flown (not the builder)
        public bool onPad;         // sitting where it was launched from, engines never lit
        public bool landed;        // on the ground (or the sea) now
        public bool destroyed;
        public double alt;         // above the surface, m
        public double maxAlt;      // highest this flight, m
        public double peAlt;       // periapsis above the surface, m (negative = it comes down)
        public double airTop;      // where the air ends, m
        public double orbitDv;     // delta-v the rocket still carries, m/s (vacuum)
    }

    /// <summary>
    /// The first-flight guide: a handful of steps from the builder to space (and orbit, if the rocket can), each one ticked
    /// off by what the player actually does rather than by clicking "next". A new game starts it; it is saved with the career.
    /// </summary>
    public static class Guide
    {
        public const int Launch = 0, Ignite = 1, Climb = 2, Space = 3, Orbit = 4, Home = 5, Orbiting = 6, Lost = 7, Closed = 99;
        public const double ClimbAlt = 8_000;
        /// <summary>Delta-v needed to get from the pad to a low orbit around Arden (with drag and gravity losses).</summary>
        public const double OrbitDv = 3_300;

        public static bool Showing(int step) => step >= Launch && step <= Lost;
        public static bool Finished(int step) => step == Home || step == Orbiting || step == Lost;

        /// <summary>The next step, given the current one and what is happening.</summary>
        public static int Advance(int step, GuideState s)
        {
            if (!Showing(step) || !s.inFlight) return step;
            // A fresh rocket on the pad after a failed or unfinished try: start again from lighting the engine.
            if (s.onPad && !s.destroyed && (step == Climb || step == Space || step == Orbit || step == Lost)) return Ignite;
            if (step == Launch) step = Ignite;
            if (step >= Ignite && step <= Orbit)
            {
                if (s.destroyed) return Lost;
                bool flew = s.maxAlt > 300;
                if (flew && s.landed) return Home;
            }
            switch (step)
            {
                case Ignite: return !s.onPad && s.alt > 30 ? Climb : step;
                case Climb: return s.alt > ClimbAlt ? Space : step;
                case Space: return s.alt > s.airTop ? Orbit : step;
                case Orbit: return s.peAlt > s.airTop ? Orbiting : step;
            }
            return step;
        }

        public static string Title(int step) => step switch
        {
            Launch => "First flight 1/5: launch",
            Ignite => "First flight 2/5: lift off",
            Climb => "First flight 3/5: climb",
            Space => "First flight 4/5: reach space",
            Orbit => "First flight 5/5: get into orbit",
            Home => "Welcome home!",
            Orbiting => "You're in orbit!",
            Lost => "That one didn't make it",
            _ => "",
        };

        /// <summary>The guide's advice for this step. Short enough for a small panel (about four lines).</summary>
        public static string Text(int step, GuideState s, bool career)
        {
            if (Words.Easy) return EasyText(step, s, career);
            return Words.Say(EngineerText(step, s, career));
        }

        /// <summary>The same steps in short words for young players (milestone 24).</summary>
        static string EasyText(int step, GuideState s, bool career)
        {
            string air = $"{s.airTop / 1000:0} km";
            switch (step)
            {
                case Launch:
                    return career ? "Your rocket is ready! Click <b>Missions</b> and pick a job. Then press <b>LAUNCH</b> (top right)."
                        : "Pick a rocket from <b>Presets</b>, or build your own. Then press <b>LAUNCH</b> (top right).";
                case Ignite: return "Press <b>SPACE</b> to start the engine! <b>Z</b> is full power and <b>X</b> stops it.";
                case Climb: return $"Press <b>T</b> to keep the rocket steady. Watch your <b>Height</b> go up (top left)! When a tank is empty, press <b>SPACE</b> again. Go up to {ClimbAlt / 1000:0} km.";
                case Space:
                    return s.orbitDv < OrbitDv
                        ? $"Space starts at {air}, where the air runs out. Keep going up! When the engine stops, press <b>SPACE</b> to drop the tank, then again to get the parachute ready. It opens by itself."
                        : $"Space starts at {air}. Higher than 10 km, tap <b>D</b> to tip the rocket over a little at a time. Or press <b>P</b> and let the autopilot fly!";
                case Orbit:
                    return s.orbitDv < 300 && s.peAlt < 0
                        ? "You made it to space! There's not enough fuel to stay up this time. Press <b>SPACE</b> to drop the tank and get the parachute ready."
                        : $"Now go sideways, really fast! Point at the yellow circle on the ball and run the engine until <b>Bottom of orbit</b> is higher than {air}. <b>M</b> shows the map.";
                case Home: return $"{(s.maxAlt > 300 ? $"You went up {Ui0(s.maxAlt)} and came back! " : "")}In the builder, <b>Missions</b> give you money, and science gets you new parts in <b>Research</b>.";
                case Orbiting: return "You're going round the planet! To come home, point at the crossed circle on the ball and run the engine until <b>Bottom of orbit</b> is about 30 km. Then press <b>SPACE</b> until the parachute is ready.";
                case Lost: return "Oops! Press <b>ESC</b> and pick <b>Revert to launch</b> to try again. Fins at the bottom keep a rocket flying straight.";
            }
            return "";
        }

        static string EngineerText(int step, GuideState s, bool career)
        {
            string air = $"{s.airTop / 1000:0} km";
            switch (step)
            {
                case Launch:
                    return career
                        ? "Your first rocket is ready. Open <b>Missions</b> and accept a contract (it pays when you do it), then press <b>LAUNCH</b> at the top right."
                        : "Pick a rocket from <b>Presets</b> or build one (click a part, then click the rocket), then press <b>LAUNCH</b> at the top right.";
                case Ignite:
                    return "Press <b>SPACE</b> to light the engine. <b>Z</b> is full throttle and <b>X</b> cuts it; <b>SHIFT</b>/<b>CTRL</b> fine-tune it.";
                case Climb:
                    return $"Press <b>T</b> so SAS holds the nose steady, and watch <b>Altitude</b> (top left). <b>SPACE</b> again drops an empty stage. Climb to {ClimbAlt / 1000:0} km.";
                case Space:
                    return s.orbitDv < OrbitDv
                        ? $"Space starts at {air}, where the air ends. This rocket can't reach orbit ({s.orbitDv:N0} of about {OrbitDv:N0} m/s), so just go up. When the engine stops, press <b>SPACE</b> to drop the tank and again to arm the parachute: it opens by itself below 280 m/s."
                        : $"Space starts at {air}. Past about 10 km, tip the nose east with <b>D</b> a little at a time, so you're picking up sideways speed. Or let the autopilot (<b>P</b>) fly.";
                case Orbit:
                    return s.orbitDv < 300 && s.peAlt < 0
                        ? "You're in space! Too little fuel for orbit this time: drop the empty stage and arm the parachute with <b>SPACE</b>. It opens by itself once you're slower than 280 m/s."
                        : $"Burn sideways: point at the <b>prograde</b> marker on the ball (the yellow circle) and burn until <b>Periapsis</b> is above {air}. <b>M</b> shows the map.";
                case Home:
                    return $"{(s.maxAlt > 300 ? $"You reached {Ui0(s.maxAlt)} and came back. " : "")}In the builder: <b>Missions</b> pay for flights, and science unlocks new parts in <b>Research</b>. Orbit takes about {OrbitDv:N0} m/s (see <b>Staging and delta-v</b>).";
                case Orbiting:
                    return "To come home, point <b>retrograde</b> (the crossed circle) and burn until periapsis is about 30 km, then <b>SPACE</b> until the parachute is armed (it opens by itself below 280 m/s). Or plan a trip to Tal: <b>M</b> for the map, then click your orbit.";
                case Lost:
                    return "Press <b>ESC</b> and choose <b>Revert to launch</b> to try again. In the builder, fins at the bottom keep a rocket straight, and a parachute brings the crew home.";
            }
            return "";
        }

        static string Ui0(double m) => m < 10_000 ? $"{m:0} m" : $"{m / 1000:0.0} km";
    }
}
