using System;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 23 tests: rebinding keys and the in-game guide.</summary>
    public static partial class SimTests
    {
        static void RunMilestone23()
        {
            Run("Controls: a game key moves to another key and back; shared keys are reported; reset restores everything", Rebinding);
            Run("How to play: every topic has a page, and the pages name the keys in use", GuidePages);
            Run("Achievements: earned once per save with funds and science (badge only in sandbox), saved, and checked from flight", AchievementRules);
            Run("Damage: hot parts char (and it's saved); too much air pressure crushes a capsule, a gentle flight doesn't", ChargingAndPressure);
            Run("Damage: a full tank explodes, scorches its neighbours and can set them off in a chain; the pod far away survives", ChainExplosions);
            Run("Space center: flat where the buildings are, rolling hills a few km out, and the ground the ship lands on matches what's drawn", SiteHills);
        }

        static string ChargingAndPressure()
        {
            var b = Body;
            Vector3d up = CelestialBody.DirectionFromLatLon(5, 60);
            Vector3d east = CelestialBody.EastAt(up);
            // Charring: a part held near its heat limit picks up soot that stays when it cools
            var v = VesselSim.CreateFree(D("hot", "pod.wren", "shield.s"), b, up * (b.radius + 20_000), b.SurfaceVelocity(up * (b.radius + 20_000)), Quaternion.FromToRotation(Vector3.up, up.ToFloat()));
            var pod = v.parts[0];
            double ut = 0;
            for (int i = 0; i < 100; i++) { pod.temp = pod.def.maxTemp - 30; v.Step(0.02, ut); ut += 0.02; }
            Check(pod.charred > 0.02f, $"pod near its limit for 2 s only charred {pod.charred:0.000}");
            float charred = pod.charred;
            foreach (var p in v.parts) p.temp = 300;                       // cooled right down: the soot stays
            for (int i = 0; i < 200; i++) { v.Step(0.02, ut); ut += 0.02; }
            Check(Math.Abs(pod.charred - charred) < 1e-4f, $"charring changed from {charred:0.000} to {pod.charred:0.000} once cool");
            var u = new Universe();
            u.Add(v);
            var u2 = new Universe();
            u2.LoadJson(u.ToJson(), System4);
            Check(u2.vessels.Count == 1 && Math.Abs(u2.vessels[0].parts[0].charred - charred) < 1e-3f, "charring not saved");

            // Pressure: a bare capsule slammed through thick air at 800 m/s (about 300 kPa; it takes 170)
            Vector3d p0 = up * (b.radius + 2000);
            var crushed = VesselSim.CreateFree(D("slam", "pod.wren"), b, p0, b.SurfaceVelocity(p0) + east * 800, Quaternion.LookRotation(east.ToFloat(), up.ToFloat()) * Quaternion.Euler(90, 0, 0));
            ut = 0;
            double qMax = 0;
            for (int i = 0; i < 100 && !crushed.destroyed; i++) { crushed.Step(0.02, ut); ut += 0.02; qMax = Math.Max(qMax, crushed.dynPressure); }
            Check(crushed.destroyed && crushed.destroyReason.Contains("airflow"), $"capsule at {qMax / 1000:0} kPa: {(crushed.destroyed ? crushed.destroyReason : "survived")}");
            // A gentle flight through the same air is fine
            var gentle = VesselSim.CreateFree(D("easy", "pod.wren"), b, p0, b.SurfaceVelocity(p0) + east * 150, Quaternion.LookRotation(east.ToFloat(), up.ToFloat()) * Quaternion.Euler(90, 0, 0));
            ut = 0;
            for (int i = 0; i < 100; i++) { gentle.Step(0.02, ut); ut += 0.02; }
            Check(!gentle.destroyed, "a gentle flight was crushed: " + gentle.destroyReason);
            // Every part has a sensible limit, and the pointy ones take more than tanks
            foreach (var d in PartCatalog.All) { float lim = VesselSim.PressureLimit(d); Check(lim >= 60 && lim <= 400, $"{d.id} limit {lim}"); }
            Check(VesselSim.PressureLimit(PartCatalog.Get("shield.s")) > VesselSim.PressureLimit(PartCatalog.Get("tank.s4")), "a heat shield should take more pressure than a tank");
            return $"pod charred {charred:0.00} in 2 s near its limit; crushed at {qMax / 1000:0} kPa: {crushed.destroyReason}";
        }

        static string ChainExplosions()
        {
            var b = Body;
            var design = VesselDesign.Default();
            int chains = 0, tanks = 0;
            string sample = "";
            for (int k = 0; k < design.nodes.Count; k++)
            {
                if (PartCatalog.Get(design.nodes[k].id).kind != PartKind.Tank) continue;
                tanks++;
                // In orbit (on the pad, a burst bottom tank drops the rest of the rocket onto the pad, which is fair)
                Vector3d r0 = new Vector3d(b.radius + 100_000, 0, 0);
                var v = VesselSim.CreateFree(design, b, r0, new Vector3d(0, 0, Math.Sqrt(b.mu / r0.magnitude)), Quaternion.identity);
                var tank = v.parts.Find(p => p.index == k);
                int n0 = v.parts.Count;
                var neighbours = v.parts.FindAll(p => p.index == tank.parent || p.parent == tank.index);
                double[] t0 = neighbours.Select(p => p.temp).ToArray();
                v.Explode(tank, "test");
                Check(v.explosions.Count == 1, $"tank {k}: {v.explosions.Count} explosions");
                Check(!v.parts.Contains(tank) && v.parts.Count < n0, $"tank {k} still there");
                for (int i = 0; i < neighbours.Count; i++)
                    Check(neighbours[i].temp > t0[i] + 300 && neighbours[i].charred >= 0.3f, $"tank {k}: neighbour {neighbours[i].def.id} not scorched ({neighbours[i].temp - t0[i]:0} K, char {neighbours[i].charred:0.00})");
                double ut = 0;
                for (int i = 0; i < 60 && !v.destroyed; i++) { v.Step(0.02, ut); ut += 0.02; }
                if (v.explosions.Count > 1) { chains++; sample = $"tank {k} set off {v.explosions.Count - 1} more"; }
                Check(v.destroyed || v.parts.Exists(p => p.def.kind == PartKind.Command), $"tank {k}: lost the pod without being destroyed");
                if (k > 4) Check(!v.destroyed, $"tank {k}, far from the pod, destroyed the whole ship: {v.destroyReason}");
            }
            Check(tanks >= 3, $"only {tanks} tanks in the default rocket");
            Check(chains >= 1, "no tank ever set off its neighbours");
            return $"{tanks} tanks tried, {chains} chain reactions ({sample})";
        }

        static string SiteHills()
        {
            var b = Body;
            double maxFlat = 0, maxHill = 0, worst = 0;
            for (int i = 0; i < 64; i++)
            {
                double ang = i * Math.PI * 2 / 64;
                for (double r = 0; r <= 3000; r += 250)
                {
                    var d = LaunchSite.DirectionAt(b, Math.Cos(ang) * r, Math.Sin(ang) * r);
                    maxFlat = Math.Max(maxFlat, Math.Abs(Terrain.Height(b, d)));
                }
                var far = LaunchSite.DirectionAt(b, Math.Cos(ang) * 13_000, Math.Sin(ang) * 13_000);
                maxHill = Math.Max(maxHill, Terrain.Height(b, far));
                for (double r = 3000; r <= 30_000; r += 1500)
                {
                    var d = LaunchSite.DirectionAt(b, Math.Cos(ang) * r, Math.Sin(ang) * r);
                    worst = Math.Max(worst, Math.Abs(LaunchSite.SurfaceHeight(b, d) - Terrain.Height(b, d)));
                }
            }
            Check(maxFlat < 1.0, $"the space center grounds rise {maxFlat:0.0} m");
            Check(maxHill > 30, $"the country 13 km out is only {maxHill:0} m high at most");
            Check(worst < 1e-6, $"landing ground and drawn ground differ by {worst:0.0} m");
            return $"grounds within {maxFlat:0.0} m, hills up to {maxHill:0} m at 13 km";
        }

        static string Rebinding()
        {
            try
            {
                Keybinds.ResetAll();
                Check(In.Name(K.Space) == "SPACE" && In.Name(K.LeftShift) == "SHIFT" && In.Name(K.Period) == ".", $"defaults {In.Name(K.Space)} {In.Name(K.LeftShift)} {In.Name(K.Period)}");
                Check(Keybinds.Clashes().Count == 0, "clashes with the default keys: " + string.Join("; ", Keybinds.Clashes()));
                Keybinds.Set(K.Space, "Enter");
                Check(In.Name(K.Space) == "ENTER", $"stage on {In.Name(K.Space)}");
                Keybinds.Set(K.X, "Z");
                Check(Keybinds.Clashes().Count == 1, $"{Keybinds.Clashes().Count} clashes after putting cut throttle on Z");
                Keybinds.Set(K.F1, "NotAKey");
                Check(In.Name(K.F1) == "F1", "a bad saved key should fall back to the default");
                Keybinds.ResetAll();
                Check(In.Name(K.Space) == "SPACE" && In.Name(K.X) == "X", "reset didn't restore the keys");
                return $"{Keybinds.Actions.Length} rebindable keys";
            }
            finally { Keybinds.ResetAll(); }
        }

        static string AchievementRules()
        {
            var u = new Universe();
            u.career.Start(GameMode.Career);
            var c = u.career;
            double f0 = c.funds, s0 = c.science;
            var strato = Achievements.Get("strato");
            Check(strato != null && strato.name == "Exited the Stratosphere", "no stratosphere achievement");
            Check(Achievements.Award(c, "strato") && !Achievements.Award(c, "strato"), "awarded twice (or not at all)");
            Check(Math.Abs(c.funds - f0 - strato.funds) < 1e-6 && Math.Abs(c.science - s0 - strato.science) < 1e-6, "reward not paid");
            Check(c.badgeNews.Count == 1 && c.news.Exists(n => n.Contains("Exited the Stratosphere")), "no pop-up or news");
            // Flying above 30 km on Arden earns it from the flight checks; the edge of space at 76 km
            var u2 = new Universe();
            u2.career.Start(GameMode.Career);
            var home = System4[0];
            var v = VesselSim.CreateFree(VesselDesign.Default(), home, new Vector3d(home.radius + 31_000, 0, 0), new Vector3d(0, 300, 0), Quaternion.identity);
            v.hasLaunched = true;
            u2.Add(v);
            Achievements.Check(u2, v, 0);
            Check(u2.career.badges.Contains("strato") && !u2.career.badges.Contains("space"), $"badges at 31 km: {string.Join(",", u2.career.badges)}");
            Check(u2.career.badges.Contains("liftoff"), "liftoff not earned in flight");
            // Saved with the career
            var u3 = new Universe();
            u3.LoadJson(u2.ToJson(), System4);
            Check(u3.career.badges.Contains("strato") && u3.career.badges.Count == u2.career.badges.Count, "badges not saved");
            // Sandbox: the badge, but no money
            var sb = new Universe();
            sb.career.Start(GameMode.Sandbox);
            double sf = sb.career.funds;
            Achievements.Award(sb.career, "orbit");
            Check(sb.career.badges.Contains("orbit") && sb.career.funds == sf, "sandbox paid out");
            Check(Achievements.All.Select(a => a.id).Distinct().Count() == Achievements.All.Length, "duplicate achievement ids");
            return $"{Achievements.All.Length} achievements; strato +{strato.funds:N0} funds +{strato.science:0} science";
        }

        static string GuidePages()
        {
            var t = typeof(HowToPlay);
            var page = t.GetMethod("Page", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            int chars = 0;
            for (int i = 0; i < HowToPlay.Topics.Length; i++)
            {
                string s = (string)page.Invoke(null, new object[] { i });
                Check(!string.IsNullOrWhiteSpace(s) && s.Length > 200, $"topic {HowToPlay.Topics[i]} is too short");
                chars += s.Length;
            }
            string controls = (string)page.Invoke(null, new object[] { HowToPlay.Topics.Length - 1 });
            Check(controls.Contains("SPACE") && controls.Contains("Stage"), "the controls page doesn't list the keys");
            return $"{HowToPlay.Topics.Length} topics, {chars} characters";
        }
    }
}
