using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>One achievement: what it's called, how it's earned, its reward and how its badge looks.</summary>
    public class AchievementDef
    {
        public string id, name, desc;
        public double funds, science;
        public Color color;
        public int shape;      // 0 round, 1 hexagon, 2 shield, 3 star
        public int icon;       // see Badges.DrawIcon
    }

    /// <summary>
    /// Milestone 23 (captaincarl24's idea): achievements. Each one earns a badge kept in that save, and in career extra funds
    /// and science. Most are checked every second from the vessel being flown and the save's records; a few (a crash)
    /// are reported as they happen.
    /// </summary>
    public static class Achievements
    {
        static AchievementDef A(string id, string name, string desc, double funds, double sci, Color c, int shape, int icon) =>
            new AchievementDef { id = id, name = name, desc = desc, funds = funds, science = sci, color = c, shape = shape, icon = icon };

        static readonly Color Blue = new Color(0.2f, 0.5f, 0.9f), Teal = new Color(0.1f, 0.6f, 0.62f), Gold = new Color(0.9f, 0.68f, 0.2f),
            Red = new Color(0.82f, 0.25f, 0.2f), Purple = new Color(0.52f, 0.32f, 0.78f), Green = new Color(0.3f, 0.68f, 0.32f),
            Orange = new Color(0.95f, 0.52f, 0.15f), Slate = new Color(0.4f, 0.48f, 0.58f), Ice = new Color(0.55f, 0.8f, 0.95f);

        public static readonly AchievementDef[] All =
        {
            A("liftoff", "Liftoff!", "Leave the pad.", 2_000, 2, Orange, 0, 0),
            A("sound", "Sound Barrier", "Beat 340 m/s in Arden's air.", 4_000, 4, Blue, 0, 6),
            A("strato", "Exited the Stratosphere", "Climb above 30 km on Arden.", 6_000, 6, Blue, 1, 1),
            A("space", "Edge of Space", "Climb above 75 km on Arden.", 10_000, 10, Purple, 1, 1),
            A("orbit", "Round and Round", "Get into orbit.", 15_000, 15, Teal, 0, 2),
            A("hyper", "Hypersonic", "Over 1,700 m/s while in the air.", 8_000, 8, Red, 3, 6),
            A("night", "Night Owl", "Launch in the dark.", 3_000, 3, Slate, 0, 0),
            A("eva", "Stepping Out", "Your first spacewalk.", 6_000, 6, Teal, 2, 4),
            A("moonwalk", "Moonwalker", "Walk on Tal.", 20_000, 20, Slate, 2, 4),
            A("flag", "Planted It", "A flag on another world.", 12_000, 12, Red, 2, 3),
            A("dock", "Docking Pro", "Dock two ships.", 15_000, 15, Teal, 1, 5),
            A("tal", "Tal Touchdown", "Land on Tal.", 25_000, 25, Slate, 3, 2),
            A("talback", "There and Back", "Come home from Tal.", 30_000, 30, Gold, 3, 0),
            A("vesk", "Ice Skater", "Land on Vesk.", 35_000, 35, Ice, 3, 2),
            A("ochre", "Red Sands", "Land on Ochre.", 60_000, 50, Orange, 3, 2),
            A("mote", "Tiny World", "Land on Mote.", 40_000, 35, Slate, 1, 2),
            A("brume", "Giant Leap", "Orbit Brume, the giant.", 90_000, 80, Gold, 3, 7),
            A("escape", "Escape Velocity", "Leave Arden's gravity.", 30_000, 25, Purple, 0, 6),
            A("sun", "Sunbather", "Halfway from Arden to the sun.", 40_000, 40, Orange, 3, 8),
            A("rover", "Road Trip", "Drive on another world.", 15_000, 15, Green, 1, 9),
            A("fuel", "Fuel Factory", "Make fuel from mined ore.", 20_000, 20, Green, 1, 10),
            A("capture", "Rock Wrangler", "Put a wanderer in orbit.", 40_000, 40, Slate, 2, 11),
            A("base", "Base Builder", "Cable two modules into a base.", 25_000, 25, Teal, 1, 12),
            A("lab", "Research Station", "A crewed lab at a base.", 35_000, 40, Purple, 2, 13),
            A("longhaul", "Thirty Days Out", "A crew away for 30 days.", 30_000, 30, Gold, 2, 14),
            A("chute", "Soft Landing", "Land under a parachute.", 4_000, 4, Orange, 0, 15),
            A("barge", "Sea Landing", "Land on the barge in the harbour.", 25_000, 25, Blue, 2, 2),
            A("crash", "Lithobraking", "Hit the ground at 80 m/s. Oops.", 1_000, 1, Red, 0, 16),
            A("rich", "Millionaire", "Have a million funds.", 0, 50, Gold, 3, 17),
            // Milestone 27
            A("explorer", "Explorer", "Find one of the hidden places on a world.", 20_000, 25, Green, 2, 18),
            A("globetrotter", "Globetrotter", "Find five hidden places.", 80_000, 90, Gold, 3, 18),
            A("photo", "Say Cheese", "Take a picture in photo mode.", 2_000, 2, Blue, 0, 19),
            A("storm", "Storm Chaser", "Fly through rain or a dust storm.", 8_000, 8, Slate, 1, 20),
            A("station", "Space Station", "Dock three ships into one in orbit.", 45_000, 45, Teal, 3, 21),
        };

        public static AchievementDef Get(string id) => All.FirstOrDefault(a => a.id == id);

        /// <summary>Awards an achievement once per save: a badge, and in career its funds and science. Returns true if new.</summary>
        public static bool Award(Career c, string id)
        {
            var a = Get(id);
            if (a == null || c.badges.Contains(id)) return false;
            c.badges.Add(id);
            if (c.IsCareer) { c.funds += a.funds; c.science += a.science; }
            c.badgeNews.Add(id);
            c.news.Add(c.IsCareer ? $"Achievement: {a.name}! +{a.funds:N0} funds, +{a.science:0} science" : $"Achievement: {a.name}!");
            return true;
        }

        /// <summary>Once a second: what the vessel being flown (and the save as a whole) has done.</summary>
        public static void Check(Universe u, VesselSim v, double ut)
        {
            var c = u.career;
            int r = c.records;
            void Rec(int bit, string id) { if ((r & bit) != 0) Award(c, id); }
            Rec(Records.Launch, "liftoff");
            Rec(Records.Orbit, "orbit");
            Rec(Records.FirstEva, "eva");
            Rec(Records.Docking, "dock");
            Rec(Records.TalLanding, "tal");
            Rec(Records.TalReturn, "talback");
            Rec(Records.VeskLanding, "vesk");
            Rec(Records.OchreLanding, "ochre");
            Rec(Records.MoteLanding, "mote");
            Rec(Records.BrumeOrbit, "brume");
            Rec(Records.Escape, "escape");
            Rec(Records.RoverTal | Records.RoverVesk, "rover");
            Rec(Records.OreRefined, "fuel");
            Rec(Records.FirstCapture, "capture");
            if ((r & (Records.TalFlag | Records.VeskFlag)) != 0 || u.vessels.Any(x => !x.destroyed && x.body != null && !x.body.IsHomeWorld && x.parts.Exists(p => p.def.kind == PartKind.Flag))) Award(c, "flag");
            var groups = Bases.Groups(u);
            if (groups.Count > 0) Award(c, "base");
            if (groups.Any(g => Bases.Summarize(g).labPerDay > 0)) Award(c, "lab");
            if (c.IsCareer && c.funds >= 1_000_000) Award(c, "rich");
            if (v == null || v.destroyed || v.body == null) return;

            var b = v.body;
            bool home = b.IsHomeWorld;
            double alt = v.Altitude;
            bool inAir = b.HasAtmosphere && alt < b.atmosphereHeight;
            double speed = v.SurfaceRelVelocity.magnitude;
            if (home && v.hasLaunched && !v.landed && alt > 100) Award(c, "liftoff");
            if (home && inAir && speed > 340 && !v.landed) Award(c, "sound");
            if (home && alt > 30_000 && v.hasLaunched) Award(c, "strato");
            if (home && alt > b.atmosphereHeight && v.hasLaunched) Award(c, "space");
            if (inAir && speed > 1700 && b.PressureAtm(alt) > 0.001) Award(c, "hyper");
            if (home && v.hasLaunched && !v.landed && alt < 3000 && ut - v.launchUt < 120)
            {
                Vector3d up = v.pos.normalized, sun = Sun.DirectionFrom(v.AbsolutePosition(ut), ut);
                if (Vector3d.Dot(up, sun) < -0.1) Award(c, "night");
            }
            if (v.IsEva && v.landed && b.name == "Tal") Award(c, "moonwalk");
            if (b.isStar && v.pos.magnitude < SunbatherDistance(u)) Award(c, "sun");
            if (v.CrewCount > 0 && v.hasLaunched && ut - v.launchUt > 30 * Supplies.Day && !(v.landed && home)) Award(c, "longhaul");
            if (v.landed && b.HasAtmosphere && v.parts.Exists(p => p.chuteDeployed && !p.chuteFailed && p.chuteOpen > 0.5f)) Award(c, "chute");
            if (home && v.landed && v.hasLaunched && LaunchSite.OnBarge(b, b.InertialToFixed(v.pos, ut))) Award(c, "barge");
            if (Weather.On && v.hasLaunched && !v.landed && Weather.Has(b) && alt < 6000)
            {
                var w = Weather.At(b, b.InertialToFixed(v.pos, ut), ut);
                if (w.rain > 0.4f || w.dust > 0.4f) Award(c, "storm");
            }
            if (!v.landed && v.Situation(ut).StartsWith("Orbiting") && Stations.DockedPieces(v) >= 3) Award(c, "station");
        }

        static double SunbatherDistance(Universe u)
        {
            var home = u.vessels.Select(x => x.body).FirstOrDefault(x => x != null && x.IsHomeWorld);
            return home != null ? home.orbitRadius * 0.5 : 2e10;
        }

        /// <summary>A vessel was destroyed: was it a spectacular crash?</summary>
        public static void OnDestroyed(Universe u, VesselSim v, double impactSpeed)
        {
            if (v == null || v.body == null) return;
            if (impactSpeed > 80 && v.Altitude < Terrain.MaxHeight(v.body) + 500) Award(u.career, "crash");
        }
    }
}
