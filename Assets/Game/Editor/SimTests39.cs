using System;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 39 tests: random places, hazards, engine heat with radiators and coolant, part damage and repairs.</summary>
    public static partial class SimTests
    {
        static void RunMilestone39()
        {
            Run("Random places: 10 round the space centre (4-60 km, dry land), 8 on other worlds with ground, none on the sun or gas giant; each game has its own", SitesTest);
            Run("Hazards: lightning in thick air, dust and heat wherever there's air, meteors and the shimmer on airless ground, flares, micrometeoroids and blackouts in space", HazardsTest);
            Run("Engine heat: a burn heats the engine; past 85% it shuts itself down and fires again below 60%; radiators keep it cooler; coolant is drawn on", EngineHeatTest);
            Run("Damage: a damaged engine pushes less and runs hotter, damaged tanks leak, a broken leg can be fixed, and it's all kept in saves", DamageTest);
        }

        static string SitesTest()
        {
            int was = Outposts.GameSeed;
            try
            {
                var home = System4[0];
                var tal = System4.First(b => b.name == "Tal");
                Outposts.GameSeed = 4242;
                var a = Outposts.RandomSites(home);
                var a2 = Outposts.RandomSites(home);
                Check(a.Count == 10 && a.Select(d => d.id).SequenceEqual(a2.Select(d => d.id)) && a.Select(d => d.name).SequenceEqual(a2.Select(d => d.name)), "10 on Arden, the same for the same game");
                foreach (var d in a)
                {
                    var dir = CelestialBody.DirectionFromLatLon(d.lat, d.lon);
                    double km = Vector3d.Angle(dir, LaunchSite.Direction) * home.radius / 1000;
                    Check(km > 3.5 && km < 61, $"{d.name}: {km:0} km from the centre");
                }
                Check(Outposts.RandomSites(tal).Count == 8, "8 on Tal");
                Check(Outposts.RandomSites(System4.First(b => b.IsGasGiant)).Count == 0 && Outposts.RandomSites(System4.First(b => b.isStar)).Count == 0, "none on the gas giant or the sun");
                var listed = Outposts.On(tal);
                var site = listed.First(l => l.id.StartsWith("site."));
                Check(Outposts.Get(site.id)?.name == site.name, "a random place can be looked up (to search it)");
                Outposts.GameSeed = 99;
                var b2 = Outposts.RandomSites(home);
                Check(!b2.Select(d => d.name).SequenceEqual(a.Select(d => d.name)), "another game has other places");
                return string.Join(", ", a.Take(3).Select(d => d.name));
            }
            finally { Outposts.GameSeed = was; }
        }

        static string HazardsTest()
        {
            var arden = Hazards.Eligible(thick: true, air: true, surface: true, space: false).Select(h => h.kind).ToList();
            Check(arden.Contains(HazardKind.Lightning) && arden.Contains(HazardKind.DustDevil) && arden.Contains(HazardKind.HeatWave) && !arden.Contains(HazardKind.MeteorShower) && !arden.Contains(HazardKind.Micrometeoroids),
                "on Arden's ground: " + string.Join(",", arden));
            var ochre = Hazards.Eligible(thick: false, air: true, surface: true, space: false).Select(h => h.kind).ToList();
            Check(!ochre.Contains(HazardKind.Lightning) && ochre.Contains(HazardKind.DustDevil), "thin air: dust but no lightning");
            var tal = Hazards.Eligible(thick: false, air: false, surface: true, space: false).Select(h => h.kind).ToList();
            Check(tal.Contains(HazardKind.MeteorShower) && tal.Contains(HazardKind.Shimmer) && tal.Contains(HazardKind.SolarFlare) && !tal.Contains(HazardKind.DustDevil), "on Tal: " + string.Join(",", tal));
            var space = Hazards.Eligible(thick: false, air: false, surface: false, space: true).Select(h => h.kind).ToList();
            Check(space.Contains(HazardKind.Micrometeoroids) && space.Contains(HazardKind.Blackout) && space.Contains(HazardKind.SolarFlare) && !space.Contains(HazardKind.Lightning), "in space: " + string.Join(",", space));
            Check(Hazards.Eligible(false, false, false, false).Count == 0, "nothing in flight low in the air");
            for (int s = 0; s < 50; s++)
            {
                var h = Hazards.Pick(s, false, false, true, false);
                Check(h != null && tal.Contains(h.kind), "picks only what can happen there");
                Check(Hazards.Pick(s, false, false, true, false) == h, "the same for the same seed");
            }
            return string.Join(",", tal);
        }

        static VesselSim HotBurner(bool radiators)
        {
            var d = VesselDesign.Stack("Heat test", "pod.wren", "tank.s4", "tank.s4", "eng.kestrel");
            if (radiators) DesignOps.AttachSurfaceAt(d, 1, 0.5f, 0f, "rad.s", 2, false);
            var b = System4[0];
            double r = b.radius + 300_000;
            var v = VesselSim.CreateFree(d, b, new Vector3d(r, 0, 0), new Vector3d(0, 0, -Math.Sqrt(b.mu / r)), Quaternion.identity);
            v.ActivateNextStage();
            v.throttle = 1;
            return v;
        }

        static PartState EngineOf(VesselSim v) => v.parts.First(p => p.def.IsEngine);

        static string EngineHeatTest()
        {
            bool was = VesselSim.EngineHeat;
            try
            {
                VesselSim.EngineHeat = true;
                var v = HotBurner(false);
                var eng = EngineOf(v);
                double t0 = eng.temp, ut = 0;
                for (int i = 0; i < 1500; i++) { v.Step(0.02, ut); ut += 0.02; }
                double warm = eng.temp;
                Check(warm > t0 + 100 && !eng.hot, $"30 s at full power: {t0:0} -> {warm:0} K (limit {eng.TempLimit:0})");
                for (int i = 0; i < 9000; i++) { v.Step(0.02, ut); ut += 0.02; }
                Check(!eng.hot && eng.temp < eng.TempLimit * 0.6, $"a healthy engine settles (3 more minutes: {eng.temp / eng.TempLimit * 100:0}% of its limit), never overheating");
                var hw = HotBurner(false);
                var hwe = EngineOf(hw);
                VesselSim.HeatBoost = 1.5f;
                double hut = 0;
                for (int i = 0; i < 12000; i++) { hw.Step(0.02, hut); hut += 0.02; }
                VesselSim.HeatBoost = 1f;
                Check(!hwe.hot, $"nor in a heat wave ({hwe.temp / hwe.TempLimit * 100:0}%)");
                var dmg = HotBurner(false);
                var de = EngineOf(dmg);
                dmg.DamagePart(de, 0.8f, "a test");
                double dut = 0;
                int steps = 0;
                double peak = 0;
                while (!de.hot && steps++ < 60000)
                {
                    foreach (var p in dmg.parts) if (p.def.propellant > 0 && !p.def.IsSolid) p.propellant = p.def.propellant;   // (keep it burning)
                    dmg.Step(0.02, dut); dut += 0.02;
                    peak = Math.Max(peak, de.temp / de.TempLimit);
                }
                Check(de.hot, $"a badly damaged engine overheats ({steps * 0.02:0} s at full power, peak {peak * 100:0}% of its limit)");
                var cool = HotBurner(true);
                var ce = EngineOf(cool);
                ut = 0;
                for (int i = 0; i < 1500; i++) { cool.Step(0.02, ut); ut += 0.02; }
                Check(ce.temp < warm - 20, $"with radiators: {ce.temp:0} K instead of {warm:0} K");
                // pushed past 85%: shuts down, no thrust
                eng.temp = eng.TempLimit * 0.86;
                v.Step(0.02, ut); ut += 0.02;
                Check(eng.hot, "past 85%: shut down");
                v.Step(0.02, ut); ut += 0.02;
                Check(v.thrustNow < 1e-6, "a hot engine gives no thrust");
                v.throttle = 0;
                int guard = 0;
                while (eng.hot && guard++ < 200_000) { v.Step(0.1, ut); ut += 0.1; }
                Check(!eng.hot && eng.temp < eng.TempLimit * 0.6 + 1, $"cooled below 60% after {guard * 0.1:0} s: fires again");
                // coolant: an engine past 70% draws on a coolant tank
                var d = VesselDesign.Stack("Coolant test", "pod.wren", "tank.s4", "eng.kestrel");
                DesignOps.AttachSurfaceAt(d, 1, 0.5f, 0f, "cool.s", 1, false);
                var b = System4[0];
                double r = b.radius + 300_000;
                var c = VesselSim.CreateFree(d, b, new Vector3d(r, 0, 0), new Vector3d(0, 0, -Math.Sqrt(b.mu / r)), Quaternion.identity);
                Check(Math.Abs(c.CoolantLeft - 120) < 1e-6, "a coolant tank starts full (120 L)");
                var cEng = EngineOf(c);
                cEng.temp = cEng.TempLimit * 0.8;
                c.Step(0.05, 0);
                Check(c.CoolantLeft < 120 && cEng.temp < cEng.TempLimit * 0.8, $"coolant drawn ({120 - c.CoolantLeft:0.00} L) and the engine cooler");
                Check(c.AddCoolant(500) > 0 && Math.Abs(c.CoolantLeft - 120) < 1e-6, "a canister tops it back up, no further");
                return $"burn {warm:0} K, with radiators {ce.temp:0} K";
            }
            finally { VesselSim.EngineHeat = was; }
        }

        static string DamageTest()
        {
            bool was = VesselSim.EngineHeat;
            try
            {
                VesselSim.EngineHeat = false;
                var v = HotBurner(false);
                double ut = 0;
                v.Step(0.02, ut);
                double full = v.thrustNow;
                var eng = EngineOf(v);
                v.DamagePart(eng, 1f, "a test");
                v.Step(0.02, ut);
                Check(Math.Abs(v.thrustNow - full * 0.4) < full * 0.02, $"a wrecked engine gives 40% ({v.thrustNow:0} of {full:0} kN)");
                VesselSim.EngineHeat = true;
                Check(v.EngineHeatW(eng) > 0 && Math.Abs(v.EngineHeatW(eng) / (eng.def.thrustVac * eng.thrustFrac * VesselSim.EngineHeatPerKn) - 6) < 1e-6, "and makes six times the heat");
                VesselSim.EngineHeat = false;
                var tank = v.parts.First(p => p.def.propellant > 0 && !p.def.IsSolid);
                v.throttle = 0;
                v.DamagePart(tank, 1f, "a test");
                double before = tank.propellant;
                for (int i = 0; i < 500; i++) { v.Step(0.02, ut); ut += 0.02; }
                Check(tank.propellant < before - tank.def.propellant * 0.0007 * 10 * 0.9, $"a wrecked tank leaks ({(before - tank.propellant) * 1000:0} kg in 10 s)");
                Check(tank.charred > 0.5f, "and looks scorched");
                var back = VesselSim.FromSave(JsonUtility.FromJson<VesselSave>(JsonUtility.ToJson(v.ToSave())), System4, ut);
                Check(Math.Abs(back.parts.First(p => p.def.IsEngine).damage - 1f) < 1e-6, "damage is kept in a save");
                Check(v.FixPart(eng) && eng.damage == 0 && !v.FixPart(eng), "fixed (and nothing more to fix)");
                var lander = VesselSim.CreateLanded(Hopper(), System4[0], CelestialBody.DirectionFromLatLon(5, 10), 0);
                var leg = lander.parts.FirstOrDefault(p => p.def.kind == PartKind.Leg);
                if (leg != null)
                {
                    lander.DamagePart(leg, 1f, "a meteorite");
                    Check(leg.broken, "a wrecked leg breaks");
                    Check(lander.FixPart(leg) && !leg.broken && leg.damage == 0, "and is fixed");
                }
                return $"thrust {full:0} -> 40%";
            }
            finally { VesselSim.EngineHeat = was; }
        }
    }
}
