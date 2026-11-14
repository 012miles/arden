using System;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 24 tests: steady reentries, wording levels, the new Ardlings, captaincarl24's map.</summary>
    public static partial class SimTests
    {
        static void RunMilestone24()
        {
            Run("Reentry: capsules turn shield first and stay steady with SAS off, on hold or on retrograde, from any start; nothing lurches", SteadyReentry);
            Run("Words: three levels; plain words for standard, short ones for young players, engineers' terms untouched; guide, pages and parts in each", WordLevels);
            Run("Ardlings: the new suit builds for every look, stands 1.2 m tall and stays a sensible size", ArdlingModel);
            Run("Terrain: mountains and hills on the worlds, lots of craters on the airless ones, Ochre's giant volcano, all under each world's highest-ground limit", WorldTerrain);
            Run("Four launch pads: each one's deck is solid at pad height, a rocket put on any pad stands on it and stays put; the side runways are solid too", LaunchPads);
            Run("Builder hall fits the rocket: a small rocket nearly fills a small hall, a big one gets a big hall; towers reach the top, the door lets it out", HallFits);
            Run("captaincarl24's map: the space centre on a peninsula (sea west, south and east, mainland north), every pad, runway and building on dry land, the close-up ground quick to build", Peninsula);
        }

        static string WorldTerrain()
        {
            var sys = CelestialBody.CreateSystem();
            var sb = new System.Text.StringBuilder();
            var rng = new System.Random(3);
            foreach (var name in new[] { "Tal", "Vesk", "Ochre", "Pyre", "Rime", "Mote" })
            {
                var b = sys.First(x => x.name == name);
                double lo = double.MaxValue, hi = double.MinValue, bumpiest = 0;
                for (int i = 0; i < 4000; i++)
                {
                    var d = new Vector3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1).normalized;
                    double h = Terrain.Height(b, d);
                    lo = System.Math.Min(lo, h); hi = System.Math.Max(hi, h);
                    // how rough the ground is over 300 m: craters and hills make it uneven up close
                    var t = Vector3d.Cross(d, new Vector3d(0, 1, 0)).normalized;
                    double h2 = Terrain.Height(b, (d + t * (300.0 / b.radius)).normalized);
                    bumpiest = System.Math.Max(bumpiest, System.Math.Abs(h2 - h));
                }
                Check(hi < Terrain.MaxHeight(b), $"{name}: highest sample {hi:0} m is over its limit {Terrain.MaxHeight(b):0}");
                Check(hi - lo > 1500, $"{name}: only {hi - lo:0} m from lowest to highest");
                Check(bumpiest > 20, $"{name}: the ground is too smooth up close ({bumpiest:0.0} m over 300 m at most)");
                sb.Append($"{name} {lo:0}..{hi:0} m; ");
            }
            var ochre = sys.First(x => x.name == "Ochre");
            double top = Terrain.Height(ochre, Vector3d.FromFloat(Terrain.GreatMound) + new Vector3d(0, 0.0003, 0)), foot = Terrain.Height(ochre, Vector3d.FromFloat(Terrain.GreatMound));
            double flank = Terrain.Height(ochre, (Vector3d.FromFloat(Terrain.GreatMound) + Vector3d.Cross(Vector3d.FromFloat(Terrain.GreatMound), new Vector3d(0, 1, 0)).normalized * (12_000.0 / ochre.radius)).normalized);
            Check(flank > 3000, $"Ochre's volcano is only {flank:0} m up its flank");
            return sb.ToString() + $"the Great Mound's flank {flank:0} m";
        }

        static string LaunchPads()
        {
            var b = Body;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < LaunchSite.Pads.Length; i++)
            {
                var p = LaunchSite.Pads[i];
                foreach (var (de, dn) in new[] { (0.0, 0.0), (10.0, 10.0), (-10.0, 5.0) })
                    Check(System.Math.Abs(LaunchSite.SurfaceHeight(b, LaunchSite.DirectionAt(b, p.e + de, p.n + dn)) - LaunchSite.PadHeight) < 1e-9, $"{p.name} deck at ({de}, {dn}) isn't at pad height");
                Check(System.Math.Abs(LaunchSite.SurfaceHeight(b, LaunchSite.DirectionAt(b, p.e + 30, p.n))) < 1e-9, $"{p.name}: the ground 30 m off isn't flat");
                var v = VesselSim.CreateOnPad(VesselDesign.Default(), b, 0, i);
                double off = Vector3d.Angle(b.InertialToFixed(v.pos, 0), LaunchSite.PadDirection(b, i)) * b.radius;
                Check(v.landed && off < 1.0, $"{p.name}: rocket {off:0.0} m from the middle, landed {v.landed}");
                Vector3d start = v.pos;
                for (int k = 0; k < 300; k++) v.Step(0.02, k * 0.02);
                double drift = (v.pos - start).magnitude;
                double spin = b.radius * b.AngularVelocity * 6;   // the ground turns under it: allow for that
                Check(v.landed && !v.destroyed && drift < spin + 1.0, $"{p.name}: after 6 s the rocket moved {drift:0.0} m (landed {v.landed})");
                sb.Append($"{p.name} ok; ");
            }
            foreach (var r in LaunchSite.SideRunways)
                Check(System.Math.Abs(LaunchSite.SurfaceHeight(b, LaunchSite.DirectionAt(b, (r.e0 + r.e1) / 2, (r.n0 + r.n1) / 2)) - LaunchSite.RunwayHeight) < 1e-9, "a side runway isn't solid");
            var bg = LaunchSite.Barge;
            var bd = LaunchSite.DirectionAt(b, (bg.e0 + bg.e1) / 2, (bg.n0 + bg.n1) / 2);
            Check(System.Math.Abs(LaunchSite.SurfaceHeight(b, bd) - bg.top) < 1e-9 && LaunchSite.OnBarge(b, bd), "the barge's deck isn't solid");
            Check(!LaunchSite.OnBarge(b, LaunchSite.Direction), "the pad counts as the barge");
            return sb.ToString() + "side runways and the barge solid";
        }

        static string HallFits()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (h, r) in new[] { (12f, 1.25f), (22f, 1.25f), (45f, 2.5f), (80f, 4f), (120f, 6f) })
            {
                var hall = HangarMode.HallFor(h, r);
                Check(hall.top >= h + 6f && hall.top <= Mathf.Max(40f, h * 1.75f), $"{h} m rocket in a {hall.top} m high hall");
                Check(hall.doorTop > h + 1f, $"{h} m rocket, door {hall.doorTop} m high");
                Check(hall.levels * 8f >= h - 10f && hall.levels * 8f + 4f < hall.top, $"{h} m rocket, towers {hall.levels * 8} m in a {hall.top} m hall");
                Check(hall.x0 > r + 3f && hall.x1 + 12f < hall.half && hall.doorHalf > r + 4f, $"{h} m rocket: towers {hall.x0}-{hall.x1}, half {hall.half}, door half {hall.doorHalf}");
                sb.Append($"{h} m: {hall.half * 2:0} x {hall.top:0} m hall; ");
            }
            var small = HangarMode.HallFor(22f, 1.25f);
            Check(small.top < 50f && small.half < 60f, $"a 22 m rocket gets a {small.half * 2} x {small.top} m hall (too big)");
            return sb.ToString();
        }

        static string Peninsula()
        {
            var b = Body;
            float F(double e, double n) => Terrain.ArdenField(b, LaunchSite.DirectionAt(b, e, n).ToFloat());
            // dry land (past the beach) under everything that's built
            var built = new (string what, double e, double n)[]
            {
                ("the pad", 0, 0), ("the assembly building", 0, 340), ("headquarters", -180, 263), ("runway west end", LaunchSite.RunwayEast - 1200, LaunchSite.RunwayNorth),
                ("runway east end", LaunchSite.RunwayEast + 1200, LaunchSite.RunwayNorth), ("runway 1 south", -460, -560), ("runway 2 south", -680, -650),
                ("runway 1 north", -460, 1100), ("the hangar", -300, 460), ("the tracking station", -200, 1100), ("research", -1228, 190),
                ("east pad A", 2192, -206), ("east pad B", 2037, -428), ("the orange tanks", 2345, -268), ("north-west pad", -1770, 1020), ("the helipads", 336, 64),
            };
            foreach (var (what, e, n) in built) Check(F(e, n) > 0.026f, $"{what} at ({e:0}, {n:0}) isn't on dry land (field {F(e, n):0.000})");
            // sea all round but the north
            foreach (var (e, n) in new[] { (0.0, -2200.0), (3600.0, 0.0), (-3300.0, 0.0), (-2600.0, -1200.0), (2600.0, -2000.0) })
                Check(F(e, n) < 0f, $"({e:0}, {n:0}) should be sea (field {F(e, n):0.000})");
            foreach (var (e, n) in new[] { (0.0, 3000.0), (0.0, 8000.0), (-1500.0, 2500.0), (1500.0, 2500.0) })
                Check(F(e, n) > 0.05f, $"({e:0}, {n:0}) should be the mainland (field {F(e, n):0.000})");
            // a beach between them: walking south from the pad, sand comes before the sea
            bool sand = false;
            for (double n = 0; n > -2500; n -= 10)
            {
                float f = F(0, n);
                if (f >= 0f && f < 0.025f) sand = true;
                if (f < 0f) break;
            }
            Check(sand, "no beach south of the pad");
            // the sea is flat at sea level and so is the space centre
            Check(Math.Abs(Terrain.Height(b, LaunchSite.DirectionAt(b, 0, -2600))) < 1e-9, "the sea isn't at sea level");
            // the close-up ground at the space centre (built once while standing there)
            var mesh = new Mesh();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            MeshFactory.BuildPatch(mesh, b, LaunchSite.Direction, 4300, 150);
            double ms150 = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            MeshFactory.BuildPatch(mesh, b, LaunchSite.Direction, 4300, 116);
            double ms116 = sw.Elapsed.TotalMilliseconds;
            UnityEngine.Object.DestroyImmediate(mesh);
            return $"{built.Length} spots on dry land, sea round three sides, beach south of the pad; close-up ground {ms150:0} ms at 150 cells, {ms116:0} ms at 116";
        }

        static string WordLevels()
        {
            var was = Words.Level;
            try
            {
                string msg = "Wren Capsule was crushed by the airflow (259 kPa, it takes 170 kPa) at apoapsis; heat 1473 K, delta-v 900 m/s";
                Words.Level = WordLevel.Engineer;
                Check(Words.Say(msg) == msg, "engineers' words changed");
                Words.Level = WordLevel.Standard;
                string std = Words.Say(msg);
                Check(std.Contains("highest point") && std.Contains("1200 °C") && std.Contains("speed budget") && std.Contains("259 kPa"), $"standard: {std}");
                Words.Level = WordLevel.Easy;
                string easy = Words.Say(msg);
                Check(!easy.Contains("kPa") && easy.Contains("top of the orbit") && easy.Contains("rocket power"), $"easy: {easy}");
                Check(Words.Say("Apoapsis") == "Top of the orbit", "capital letters not kept");
                foreach (var name in new[] { "Stack Decoupler", "Ember Vacuum Engine", "Ablative Heat Shield" })
                    Check(Words.Say($"{name} hit the ground") == $"{name} hit the ground", $"a part name changed: {Words.Say(name)}");
                var page = typeof(HowToPlay).GetMethod("Page", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var texts = new System.Collections.Generic.Dictionary<WordLevel, string>();
                foreach (WordLevel l in Enum.GetValues(typeof(WordLevel)))
                {
                    Words.Level = l;
                    string all = "";
                    for (int i = 0; i < HowToPlay.Topics.Length; i++)
                    {
                        string t = (string)page.Invoke(null, new object[] { i });
                        Check(t.Length > 200, $"{l} page {HowToPlay.TopicName(i)} is too short");
                        all += t;
                    }
                    texts[l] = all;
                    for (int step = 0; step < 12; step++) Guide.Text(step, new GuideState { airTop = 70_000, orbitDv = 3500 }, true);
                    foreach (var d in PartCatalog.All) Check(!string.IsNullOrEmpty(Words.PartLine(d)), $"{l}: no line for {d.name}");
                    Check(!string.IsNullOrEmpty(Guide.Text(Guide.Climb, new GuideState { airTop = 70_000 }, true)), $"{l}: no guide text");
                }
                Check(texts[WordLevel.Easy] != texts[WordLevel.Standard] && texts[WordLevel.Standard] != texts[WordLevel.Engineer], "the levels read the same");
                Words.Level = WordLevel.Easy;
                string kid = Guide.Text(Guide.Orbit, new GuideState { airTop = 70_000, orbitDv = 3500, peAlt = 10_000 }, true);
                Check(!kid.Contains("periapsis") && !kid.Contains("prograde"), $"easy guide still has big words: {kid}");
                return $"standard: \"{std}\"; easy: \"{easy}\"; page lengths {texts[WordLevel.Easy].Length}/{texts[WordLevel.Standard].Length}/{texts[WordLevel.Engineer].Length} characters";
            }
            finally { Words.Level = was; }
        }

        static string ArdlingModel()
        {
            int most = 0;
            float tallest = 0, lowest = 0;
            for (int hat = 0; hat < Ardlings.Hats; hat++)
            {
                var look = Ardlings.LookFor("Test " + hat);
                look.hat = hat;
                var m = Ardlings.Body(look, true);
                most = Math.Max(most, m.vertexCount);
                tallest = Math.Max(tallest, m.bounds.max.y);
                lowest = Math.Min(lowest, m.bounds.min.y);
                Check(m.vertexCount > 3000, $"hat {hat}: only {m.vertexCount} vertices");
                var arm = Ardlings.Arm(look, -1);
                var leg = Ardlings.Leg(look, 1);
                Check(arm.vertexCount > 300 && leg.vertexCount > 300, "limbs missing");
                Check(Math.Abs(arm.bounds.center.x) < 0.2f, $"the arm isn't built round its shoulder ({arm.bounds.center})");
            }
            Check(lowest < -1.17f && lowest > -1.23f, $"feet at {lowest:0.000} m (want -1.2)");
            Check(most < 40000, $"{most} vertices is too heavy for a crowd");
            return $"up to {most} vertices with arms and legs, feet at {lowest:0.000} m";
        }

        struct DownResult { public double thick, spin, peakQ; public bool ok; public string text; }

        /// <summary>
        /// Flies a craft down from a 100 x 30 km orbit and reports its worst angle off the airflow once the air is thick
        /// (over 2 kPa, after 15 s to settle), and its fastest spin anywhere in the air.
        /// </summary>
        static DownResult FlyDown(string label, VesselDesign d, SasMode sas, float startTiltDeg, CelestialBody b = null, double peAlt = 30_000)
        {
            b = b ?? Body;
            double ra = b.radius + 100_000, rp = b.radius + peAlt, a = (ra + rp) / 2;
            double va = Math.Sqrt(b.mu * (2 / ra - 1 / a));
            var r = new Vector3d(ra, 0, 0);
            var v = new Vector3d(0, 0, -va);
            Vector3 back = (-v).normalized.ToFloat();
            Quaternion rot = Quaternion.AngleAxis(startTiltDeg, Vector3.up) * Quaternion.LookRotation(Vector3.right, back);
            var s = VesselSim.CreateFree(d, b, r, v, rot);
            s.throttle = 0;
            s.sas = sas;
            double ut = 0, thick = 0, spin = 0, peakQ = 0, tThick = -1;
            for (int i = 0; i < 400_000 && !s.destroyed && !s.landed; i++)
            {
                s.Step(0.02, ut); ut += 0.02;
                if (s.Altitude < 3000) break;
                double q = s.dynPressure;
                peakQ = Math.Max(peakQ, q);
                if (q < 200) continue;
                spin = Math.Max(spin, s.angVel.magnitude * Mathf.Rad2Deg);
                if (q < 2000) continue;
                if (tThick < 0) tThick = ut;
                if (ut - tThick < 15) continue;
                Vector3d nose = Vector3d.Rotate(s.rot, new Vector3d(0, 1, 0));
                thick = Math.Max(thick, Vector3d.Angle(nose, -s.SurfaceRelVelocity) * 180 / Math.PI);
            }
            return new DownResult
            {
                thick = thick, spin = spin, peakQ = peakQ, ok = !s.destroyed,
                text = $"{label} {thick:0}°/{spin:0}°/s" + (s.destroyed ? " DESTROYED " + s.destroyReason : ""),
            };
        }

        static string SteadyReentry()
        {
            var cap = D("cap", "chute.s", "pod.wren", "shield.s");
            var bare = D("bare", "chute.s", "pod.wren");                       // the capsule's own blunt base, no shield
            var probe = D("pr", "chute.s", "probe.spark", "battery.s", "shield.s");
            var runs = new[]
            {
                FlyDown("shield retro 25", cap, SasMode.Retrograde, 25), FlyDown("shield retro 150", cap, SasMode.Retrograde, 150),
                FlyDown("shield off 25", cap, SasMode.Off, 25), FlyDown("shield off 90", cap, SasMode.Off, 90), FlyDown("shield off 150", cap, SasMode.Off, 150),
                FlyDown("shield hold 25", cap, SasMode.Stability, 25), FlyDown("shield hold 150", cap, SasMode.Stability, 150),
                FlyDown("bare off 90", bare, SasMode.Off, 90), FlyDown("bare hold 25", bare, SasMode.Stability, 25), FlyDown("bare retro 25", bare, SasMode.Retrograde, 25),
                FlyDown("probe hold 25", probe, SasMode.Stability, 25), FlyDown("probe retro 150", probe, SasMode.Retrograde, 150),
                FlyDown("Ochre retro 25", cap, SasMode.Retrograde, 25, Ochre, 40_000),
            };
            foreach (var x in runs)
            {
                Check(x.ok, x.text);
                Check(x.thick < 10, $"swung {x.thick:0}° off the airflow in thick air: {x.text}");
                Check(x.spin < 35, $"lurched at {x.spin:0}°/s: {x.text}");
            }
            return string.Join(", ", runs.Select(x => x.text)) + " (worst angle in thick air / fastest spin)";
        }
    }
}
