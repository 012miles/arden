using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 16 tests: Brume, its moons Rime and Pyre, and the parts for the long trip.</summary>
    public static partial class SimTests
    {
        static void RunMilestone16()
        {
            Run("Brume: a ringed giant round the sun past Ochre, with Rime and Pyre inside its sphere of influence", BrumeSystem);
            Run("Brume has no surface: a probe sinking into its clouds is lost, but a skim through the top of its air captures", BrumeNoSurface);
            Run("Rime and Pyre: landable, Pyre's volcanoes and lava fields, every region found", BrumeMoons);
            Run("Getting there: a transfer window within months, and the planner reaches Brume", TransferToBrume);
            Run("The Great Dish calls home from Brume; the deep space dish can't", BrumeSignal);
            Run("Career: Brume contracts after Ochre, records and crew experience", BrumeCareer);
            Run("Long Haul: reaches orbit with the delta-v for Brume and back to a moon's orbit", LongHaulPreset);
        }

        static CelestialBody Brume => System4.First(b => b.name == "Brume");
        static CelestialBody Rime => System4.First(b => b.name == "Rime");
        static CelestialBody Pyre => System4.First(b => b.name == "Pyre");

        static string BrumeSystem()
        {
            var home = System4[0];
            Check(System4.Count == 9 && System4[6] == Brume && System4[7] == Rime && System4[8] == Pyre, "the system list isn't [.., Brume, Rime, Pyre]");
            Check(Brume.parent == Helion && Brume.IsPlanet && Brume.IsGasGiant, "Brume isn't a gas giant going round the sun");
            Check(Rime.parent == Brume && Pyre.parent == Brume && !Rime.IsPlanet, "the moons don't go round Brume");
            Check(Brume.orbitRadius > Ochre.orbitRadius * 2, "Brume isn't well past Ochre");
            double year = Brume.OrbitPeriod / 86400;
            Check(year > 450 && year < 700, $"Brume's year is {year:0} days");
            double soi = Brume.SoiRadius;
            Check(Pyre.orbitRadius + Pyre.SoiRadius < Rime.orbitRadius - Rime.SoiRadius, "the moons' spheres overlap");
            Check(Rime.orbitRadius + Rime.SoiRadius < soi * 0.1, "Rime is far out in Brume's sphere");
            Check(Pyre.orbitRadius - Pyre.SoiRadius > Brume.ringOuter, "Pyre's sphere cuts into the rings");
            Check(Brume.ringInner > Brume.radius + Brume.atmosphereHeight, "the rings sit in the air");
            double g = Brume.SurfaceGravity / 9.81;
            Check(g > 0.7 && g < 1.0, $"cloud-top gravity {g:0.00} g");
            return $"year {year:0} days, SOI {soi / 1e6:0} Mm, {g:0.00} g; rings {Brume.ringInner / 1000:0}-{Brume.ringOuter / 1000:0} km; Pyre {Pyre.orbitRadius / 1000:0} km (SOI {Pyre.SoiRadius / 1000:0} km), Rime {Rime.orbitRadius / 1000:0} km (SOI {Rime.SoiRadius / 1000:0} km)";
        }

        static string BrumeNoSurface()
        {
            var b = Brume;
            // Straight down from 30 km with a parachute open: it never lands
            Vector3d up = new Vector3d(1, 0, 0);
            var cap = VesselSim.CreateFree(D("cap", "chute.s", "pod.wren", "shield.s"), b, up * (b.radius + 30_000), b.SurfaceVelocity(up * (b.radius + 30_000)), Quaternion.FromToRotation(Vector3.up, up.ToFloat()));
            cap.throttle = 0;
            foreach (var p in cap.parts) if (p.def.kind == PartKind.Parachute) p.chuteDeployed = true;
            double ut = 0;
            for (int i = 0; i < 200_000 && !cap.destroyed && !cap.landed; i++) { cap.Step(0.02, ut); ut += 0.02; }
            Check(cap.destroyed && !cap.landed, cap.landed ? "landed on a gas giant" : "never came down");
            Check(cap.destroyReason.Contains("no surface"), "reason: " + cap.destroyReason);
            // Arriving from Arden (about 2 km/s left over): somewhere in the upper air there's a pass that slows it into orbit
            var probe = VesselSim.CreateFree(D("probe", "probe.spark", "tank.s2", "shield.s"), b, new Vector3d(b.radius * 3, 0, 0), Vector3d.zero, Quaternion.identity);
            var lines = new List<string>();
            double lo = double.NaN, hi = double.NaN;
            for (double alt = 100_000; alt <= 215_000; alt += 5_000)
            {
                Inbound(b, alt, 2000, out var r, out var v, out double t0);
                var f = AeroPass.Predict(b, r, v, t0, probe.mass, probe.DragArea());
                if (f.outcome == AeroOutcome.Captured) { if (double.IsNaN(lo)) lo = alt; hi = alt; }
                if (alt % 20_000 == 0) lines.Add($"{alt / 1000:0} km: {f.outcome}");
                if (f.outcome == AeroOutcome.Lands) Check(f.Describe().Contains("clouds"), "the forecast talks of landing on a gas giant: " + f.Describe());
            }
            Check(!double.IsNaN(lo), "no pass captures: " + string.Join(", ", lines));
            return $"sank: {cap.destroyReason}; aerocapture corridor {lo / 1000:0}-{hi / 1000:0} km ({string.Join(", ", lines)})";
        }

        static string BrumeMoons()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var m in new[] { Rime, Pyre })
            {
                var dir = CelestialBody.DirectionFromLatLon(10, 30);
                var lander = VesselSim.CreateLanded(Hopper(), m, dir, 0);
                double ut = 0;
                for (int i = 0; i < 500; i++) { lander.Step(0.02, ut); ut += 0.02; }
                Check(lander.landed && !lander.destroyed, $"couldn't stand on {m.name}: {lander.destroyReason}");
                var names = Regions.Names(m);
                var seen = new HashSet<string>();
                double hiH = 0;
                for (int i = 0; i < 20000; i++)
                {
                    double lat = Math.Asin(2 * ((i * 0.618034) % 1) - 1) * 180 / Math.PI, lon = (i * 137.508) % 360;
                    var d = CelestialBody.DirectionFromLatLon(lat, lon);
                    seen.Add(Regions.At(m, d));
                    hiH = Math.Max(hiH, Terrain.Height(m, d));
                }
                Check(seen.Count == names.Length, $"{m.name}: found {seen.Count} of {names.Length} regions ({string.Join(", ", seen)})");
                Check(hiH <= Terrain.MaxHeight(m), $"{m.name}: ground at {hiH:0} m, over its max {Terrain.MaxHeight(m):0}");
                sb.Append($"{m.name}: stood fine, regions {string.Join("/", names)}, highest {hiH:0} m; ");
            }
            var brumeRegions = new HashSet<string>();
            for (int i = 0; i < 400; i++) brumeRegions.Add(Regions.At(Brume, CelestialBody.DirectionFromLatLon(-90 + i * 0.45, i * 7)));
            Check(brumeRegions.Count == 4, "Brume's bands: " + string.Join(", ", brumeRegions));
            return sb.ToString() + "Brume's four cloud bands";
        }

        static string TransferToBrume()
        {
            var home = System4[0]; var b = Brume;
            double win = TransferPlanner.NextWindow(home, b, 0);
            Check(win > 0 && win < 150 * 86400, $"first window on day {win / 86400:0}");
            double dvEsc = TransferPlanner.EjectionDv(home, b, home.radius + 100_000);
            Check(dvEsc > 1500 && dvEsc < 2800, $"escape burn {dvEsc:0} m/s from 100 km");
            double tt = TransferPlanner.TransferTime(home, b) / 86400;
            // Plan from a parking orbit a day before the window
            var v = InOrbit(D("probe", "probe.spark"), home, home.radius + 100_000, 0.4);
            double ut = win - 86400;
            v.RailsAdvance(ut, 0, out _);
            bool ok = TransferPlanner.Plan(home, v.pos, v.vel, ut, b, out double burnUt, out double dv, out double normal, out double closest, out double window);
            Check(ok, "no plan");
            Check(closest < b.SoiRadius, $"the plan only gets within {closest / 1e6:0} Mm (SOI {b.SoiRadius / 1e6:0} Mm)");
            return $"window day {win / 86400:0}, {tt:0}-day trip, {dvEsc:0} m/s from low orbit; planned {dv:0} m/s on day {burnUt / 86400:0.0}, passing {closest / 1e6:0} Mm from Brume";
        }

        static string BrumeSignal()
        {
            var home = System4[0]; var b = Brume;
            var none = new List<VesselSim>();
            VesselSim Near(string dish, double t)
            {
                var d = D("probe", "probe.spark", "tank.s1");
                DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s1"), 0.5f, 0f, dish, 1, false);
                Vector3d toHome = (home.AbsolutePosition(t) - b.AbsolutePosition(t)).normalized;
                var v = VesselSim.CreateFree(d, b, toHome * (b.radius + 20_000_000), Vector3d.zero, Quaternion.identity);
                foreach (var p in v.parts) if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1; }
                return v;
            }
            // Brume at its farthest from Arden: half a synodic period after the closest approach, roughly; try a spread of days
            int great = 0, small = 0, n = 0;
            double worst = 0;
            for (int day = 0; day < 600; day += 25)
            {
                double t = day * 86400.0 + 3600;
                double dist = (home.AbsolutePosition(t) - b.AbsolutePosition(t)).magnitude;
                // Skip days when the sun sits between them
                Vector3d a = home.RootPosition(t), c = b.RootPosition(t);
                if (Vector3d.Angle(a, c) > 170 * Math.PI / 180) continue;
                n++;
                worst = Math.Max(worst, dist);
                if (CommNet.Connect(Near("dish.x", t), none, System4, t).Connected) great++;
                if (CommNet.Connect(Near("dish.d", t), none, System4, t).Connected) small++;
            }
            Check(great == n, $"the Great Dish had a link {great} of {n} days");
            Check(small < n, "the small deep space dish reached home from Brume every time");
            return $"Great Dish linked {great}/{n} sample days (up to {worst / 1e9:0.0} Gm), the deep space dish {small}/{n}";
        }

        static string BrumeCareer()
        {
            var c = new Career();
            c.Start(GameMode.Career);
            c.RefreshOffers(0);
            Check(!c.contracts.Any(k => k.body == "Brume"), "Brume contracts at the start");
            c.records |= Records.OchreFlyby;
            bool flyby = false;
            for (int i = 0; i < 40 && !flyby; i++)
            {
                c.contracts.RemoveAll(k => k.state == ContractState.Offered);
                c.seed = 300 + i;
                c.RefreshOffers(0);
                flyby = c.contracts.Any(k => k.type == "flyby" && k.body == "Brume");
            }
            Check(flyby, "no Fly to Brume contract after the Ochre flyby");
            c.OnFlags(VFlags.SoiBrume | VFlags.OrbitBrume | VFlags.LandedPyre);
            Check((c.records & Records.BrumeFlyby) != 0 && (c.records & Records.BrumeOrbit) != 0 && (c.records & Records.BrumeMoonLanding) != 0, "Brume records not set");
            Check(VFlags.LandedOn("Rime") == VFlags.LandedRime && VFlags.LandedOn("Pyre") == VFlags.LandedPyre, "return contracts can't tell the moons");
            int xp = Crew.XpOf(VFlags.SoiBrume | VFlags.OrbitBrume | VFlags.LandedRime);
            Check(xp >= 30, $"a Brume tour is worth only {xp} XP");
            Check(TechTree.NodeOf("eng.lumen")?.id == "far" && TechTree.NodeOf("dish.x")?.id == "far", "the new parts aren't in Far Reaches");
            return $"Fly to Brume offered after Ochre; records set; Brume orbit + Rime landing = {xp} XP";
        }

        static string LongHaulPreset()
        {
            var b = Body;
            var v = VesselSim.CreateOnPad(VesselDesign.LongHaul(), b, 0);
            var ap = new FlightAutopilot();
            double ut = 0;
            for (int i = 0; i < 200_000 && !v.destroyed && ap.phase != FlightAutopilot.Phase.Done; i++)
            {
                if (ap.Update(v, ut)) v.ActivateNextStage();
                v.Step(0.02, ut); ut += 0.02;
            }
            Check(!v.destroyed, "destroyed on the way up: " + v.destroyReason);
            var o = Orbit.FromState(v.pos, v.vel, b.mu, ut);
            Check(o.Periapsis - b.radius > b.atmosphereHeight, $"not in orbit: {(o.Apoapsis - b.radius) / 1000:0} x {(o.Periapsis - b.radius) / 1000:0} km");
            double dv = 0; foreach (var r in v.StageReports()) dv += r.dvVac;
            // Escape to Brume (~2,100 m/s) + capture (~400, or less by aerobraking) + a moon's orbit (~1,500)
            Check(dv > 4000, $"only {dv:0} m/s left in orbit");
            Check(v.parts.Exists(p => p.def.id == "dish.x") && v.parts.Exists(p => p.def.id == "eng.lumen"), "lost its dish or engine");
            return $"orbit {(o.Apoapsis - b.radius) / 1000:0} x {(o.Periapsis - b.radius) / 1000:0} km with {dv:0} m/s left";
        }
    }
}
