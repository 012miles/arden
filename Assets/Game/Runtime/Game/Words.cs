using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// How the game talks (milestone 24, captaincarl24's idea). Engineer: the proper terms, as the game always spoke.
    /// Standard: plain words with the numbers, for a high schooler. Easy: short, simple words for young children.
    /// </summary>
    public enum WordLevel { Easy = 0, Standard = 1, Engineer = 2 }

    public static class Words
    {
        public static readonly string[] Names = { "Easy", "Standard", "Engineer" };
        public static readonly string[] Blurbs =
        {
            "Short, simple words for young players, and fewer numbers.",
            "Plain words with the numbers. A good place to start.",
            "The proper terms: apoapsis, delta-v, TWR, Isp, kelvin.",
        };

        static WordLevel? level;
        public static WordLevel Level
        {
            get
            {
                if (!level.HasValue)
                {
                    int i = Array.IndexOf(Environment.GetCommandLineArgs(), "-words");
                    var args = Environment.GetCommandLineArgs();
                    level = i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int a) ? (WordLevel)Mathf.Clamp(a, 0, 2)
                        : Universe.IsAutotest ? WordLevel.Standard : (WordLevel)Mathf.Clamp(PlayerPrefs.GetInt("words", 1), 0, 2);
                }
                return level.Value;
            }
            set
            {
                if (level == value) return;
                level = value;
                cache.Clear();
                if (!Universe.IsAutotest) { PlayerPrefs.SetInt("words", (int)value); PlayerPrefs.Save(); }
            }
        }

        public static bool Easy => Level == WordLevel.Easy;
        public static bool Standard => Level == WordLevel.Standard;
        public static bool Engineer => Level == WordLevel.Engineer;

        /// <summary>The same thing said three ways.</summary>
        public static string T(string engineer, string standard, string easy) =>
            Level == WordLevel.Engineer ? engineer : Level == WordLevel.Standard ? standard : easy;

        /// <summary>A temperature: kelvin for engineers, Celsius for everyone else.</summary>
        public static string Temp(double kelvin) => Engineer ? $"{kelvin:0} K" : $"{kelvin - 273.15:0} °C";

        /// <summary>A part's one-line summary: the full stats for engineers, plain words for standard, what it does for young players.</summary>
        public static string PartLine(PartDef d)
        {
            if (Engineer) return d.StatsLine;
            if (Standard) return Say(d.StatsLine);
            switch (d.kind)
            {
                case PartKind.Command: return d.crew > 0 ? $"Room for {d.crew} Ardling{(d.crew > 1 ? "s" : "")}" : "Flies the ship with nobody inside";
                case PartKind.Tank: return "Holds fuel for the engines";
                case PartKind.MonoTank: return "Fuel for the puffers";
                case PartKind.Engine: return d.IsJet ? "Jet engine: only works in air" : "Pushes the rocket";
                case PartKind.Solid: return "Booster: very strong, can't be stopped";
                case PartKind.Decoupler: case PartKind.RadialDecoupler: return "Splits the rocket in two";
                case PartKind.Parachute: return "Slows you down for landing";
                case PartKind.HeatShield: return "Keeps you safe coming home";
                case PartKind.NoseCone: return "A pointy top that slips through the air";
                case PartKind.Fins: return "Keeps the rocket flying straight";
                case PartKind.Wing: return "Wing: lifts a plane";
                case PartKind.ControlSurface: return "Moving wing for steering";
                case PartKind.Gear: case PartKind.Wheel: return "Wheels";
                case PartKind.Leg: return "Legs for landing";
                case PartKind.Rcs: return "Puffers: little pushes to turn and move";
                case PartKind.Light: return "A light";
                case PartKind.EscapeTower: return "Pulls the crew to safety if something goes wrong";
                case PartKind.Fairing: case PartKind.FairingHalf: return "A shell that covers things on top";
                case PartKind.DockingPort: return "Joins two ships together";
                case PartKind.Habitat: return "A room to live in";
                case PartKind.Solar: return "Makes power from sunlight";
                case PartKind.Antenna: return "Talks to home";
                case PartKind.Science: return "Does science";
                case PartKind.Battery: return "Stores power";
                case PartKind.Scanner: return "Finds ore from space";
                case PartKind.Drill: return "Digs up ore";
                case PartKind.OreTank: return "Holds ore";
                case PartKind.Converter: return "Turns ore into fuel";
                case PartKind.Radiator: return "Keeps machines cool";
                case PartKind.Frame: return "A frame for a rover";
                case PartKind.Grabber: return "Grabs space rocks";
                default: return d.Category;
            }
        }

        // ---------------------------------------------------------------- rewriting sentences

        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        // Longer phrases first. Each: the engineer's term (a regex), the standard words, the easy words.
        static readonly (string term, string std, string easy)[] Terms =
        {
            ("maneuver nodes?", "planned burn", "plan"),
            ("dynamic pressure|dyn\\. pressure", "air push", "air push"),
            ("delta-v|Δv", "speed budget", "rocket power"),
            ("thrust-to-weight( ratio)?|\\bTWR\\b", "push vs weight", "push"),
            ("specific impulse|\\bIsp\\b", "fuel efficiency", "fuel saving"),
            ("apoapsis|apoapses", "highest point", "top of the orbit"),
            ("periapsis|periapses", "lowest point", "bottom of the orbit"),
            ("sphere of influence|\\bSOI\\b", "gravity zone", "gravity bubble"),
            ("aerocapture", "air capture", "catching an orbit with the air"),
            ("aerobraking|aerobrake", "air braking", "slowing down in the air"),
            ("retrograde", "backward", "backward"),
            ("prograde", "forward", "forward"),
            ("radial out", "outward", "away from the planet"),
            ("radial in", "inward", "toward the planet"),
            ("anti-normal|antinormal", "orbit-down", "down"),
            ("inclination", "orbit tilt", "tilt"),
            ("eccentricity", "orbit stretch", "stretch"),
            ("circularize|circularise", "round out the orbit", "make the orbit round"),
            ("sub-orbital|suborbital", "not in orbit yet", "falling back"),
            ("trajectory", "path", "path"),
            ("rendezvous", "meet-up", "meeting"),
            ("monopropellant|monoprop|mono", "thruster fuel", "puffer fuel"),
            ("propellant", "fuel", "fuel"),
            ("electric charge|\\bEC\\b", "power", "power"),
            ("ablator", "shield coating", "heat shield"),
            ("~ablative", "heat-soaking", "heat"),
            ("angle of attack|\\bAoA\\b", "nose angle", "nose tilt"),
            ("sideslip", "side angle", "side tilt"),
            ("gimbal", "engine steering", "engine steering"),
            ("torque", "turning strength", "turning power"),
            ("~decoupler", "separator", "splitter"),
            ("~decouple", "separate", "split off"),
            ("ascent", "climb", "climb"),
            ("descent", "way down", "way down"),
            ("velocity", "speed", "speed"),
            ("telemetry", "flight info", "flight info"),
            ("transfer window", "best time to go", "best time to go"),
            ("insertion", "arrival", "arrival"),
            ("atmosphere", "atmosphere", "air"),
            ("~vacuum", "vacuum", "space"),
            ("altitude", "altitude", "height"),
            ("reentry|re-entry", "reentry", "coming home through the air"),
            ("ISRU", "fuel maker", "fuel maker"),
            ("destroyed", "destroyed", "broken"),
            ("deploy(ed|s)?", "deploy", "open"),
            ("hyperbolic", "escaping", "flying away"),
        };

        static List<(Regex re, string std, string easy)> compiled;
        static readonly Regex Kelvin = new Regex(@"(-?\d+(?:\.\d+)?)\s?K\b", RegexOptions.Compiled);
        static readonly Regex Details = new Regex(@"\s*\((?=[^()]*\d)[^()]*\)", RegexOptions.Compiled);   // "(259 kPa, it takes 170 kPa)"

        /// <summary>Rewrites a sentence for the current level (unchanged for engineers).</summary>
        public static string Say(string s)
        {
            if (string.IsNullOrEmpty(s) || Engineer) return s;
            if (cache.TryGetValue(s, out var done)) return done;
            if (compiled == null)
            {
                compiled = new List<(Regex, string, string)>();
                foreach (var (term, std, easy) in Terms)
                {
                    // a leading ~ means lower case only: the word also turns up in part names, which keep their names
                    bool lower = term.StartsWith("~");
                    string t = lower ? term.Substring(1) : term;
                    compiled.Add((new Regex(t.StartsWith("\\b") ? t : $"\\b({t})", (lower ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.Compiled), std, easy));
                }
            }
            string r = s;
            bool easyMode = Easy;
            if (easyMode) r = Details.Replace(r, "");
            r = Kelvin.Replace(r, m => $"{double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) - 273.15:0} °C");
            foreach (var (re, std, easy) in compiled)
            {
                string to = easyMode ? easy : std;
                r = re.Replace(r, m => char.IsUpper(m.Value[0]) && !(m.Value.Length > 1 && m.Value.ToUpperInvariant() == m.Value) ? char.ToUpper(to[0]) + to.Substring(1) : to);
            }
            if (cache.Count > 400) cache.Clear();
            cache[s] = r;
            return r;
        }
    }
}
