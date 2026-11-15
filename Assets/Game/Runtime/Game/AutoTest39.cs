using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 39: a random place beside a lander on Tal, searched; then each hazard where it can happen: lightning,
        /// a dust devil and a heat wave on Arden; meteors, the shimmer and a solar flare on Tal; micrometeoroids and a
        /// blackout in orbit. Logs what each did (health, damage, things lying about) and takes pictures.
        /// </summary>
        IEnumerator HazardsTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            var tal = root.bodies.First(b => b.name == "Tal");

            // a random place on Tal, with a lander beside it
            var site = Outposts.On(tal).First(l => l.id.StartsWith("site."));
            var e = CelestialBody.EastAt(site.dir);
            var lander = Crewed(VesselSim.CreateLanded(Presets.TalLander(), tal, (site.dir + e * (40.0 / tal.radius)).normalized, u.ut), u, "Iris Ardling");
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(2f);
            var f = root.flight;
            f.LookAlong(tal.FixedToInertial(site.dir, u.ut) * tal.radius - lander.pos, 60f, 0f, 14f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("770_random_site");
            int before = u.items.Count;
            f.DoScavenge();
            yield return new WaitForSecondsRealtime(0.8f);
            Log($"random site {site.name}: searched {u.career.scavenged.Contains(site.id)}, things dropped {u.items.Count - before}");

            // Tal: meteors, the shimmer, a solar flare (someone outside)
            var eva = f.GoEva(lander.parts.First(p => p.crew.Count > 0));
            yield return new WaitForSecondsRealtime(1.5f);
            var vit = f.TestVitals("Iris Ardling");
            int items0 = u.items.Count;
            f.TestHazard(HazardKind.MeteorShower);
            yield return new WaitForSecondsRealtime(12f);
            yield return Shot("771_meteor_shower");
            yield return new WaitForSecondsRealtime(20f);
            Log($"meteor shower: Iris {Describe(vit)}, lander damage {lander.parts.Max(p => p.damage):0.00}, meteorite chunks {u.items.Count(w => w.id == "meteorite")}");
            f.TestEndHazard();
            Survival.Revive(vit); vit.health = 100;
            f.TestDropLoot();
            yield return new WaitForSecondsRealtime(0.5f);
            f.TestHazard(HazardKind.Shimmer);
            yield return new WaitForSecondsRealtime(5f);
            yield return Shot("772_shimmer");
            Log($"shimmer: things in the air {u.items.Count(w => w.flying)}, Iris on her feet {eva.landed}");
            f.TestEndHazard();
            yield return new WaitForSecondsRealtime(3f);
            f.TestHazard(HazardKind.SolarFlare);
            yield return new WaitForSecondsRealtime(6f);
            yield return Shot("773_solar_flare");
            Log($"solar flare (outside): Iris {Describe(vit)}");
            f.TestEndHazard();
            Survival.Revive(vit); vit.health = 100;

            // Arden: lightning, a dust devil, a heat wave
            var dir = (LaunchSite.Direction + CelestialBody.EastAt(LaunchSite.Direction) * (3500.0 / home.radius)).normalized;
            var hopper = Crewed(VesselSim.CreateLanded(Presets.TalLander(), home, dir, u.ut), u, "Otto Ardling");
            root.Fly(hopper);
            yield return new WaitForSecondsRealtime(2f);
            f = root.flight;
            var eva2 = f.GoEva(hopper.parts.First(p => p.crew.Count > 0));
            yield return new WaitForSecondsRealtime(1.5f);
            var otto = f.TestVitals("Otto Ardling");
            f.TestHazard(HazardKind.Lightning);
            for (int i = 0; i < 6; i++) { yield return new WaitForSecondsRealtime(1.3f); yield return Shot($"774_lightning_{i}"); }
            yield return new WaitForSecondsRealtime(10f);
            Log($"thunderstorm: Otto {Describe(otto)}, ship damage {hopper.parts.Max(p => p.damage):0.00}");
            f.TestEndHazard();
            Survival.Revive(otto); otto.health = 100;
            f.TestDropLoot();
            f.TestHazard(HazardKind.DustDevil);
            yield return new WaitForSecondsRealtime(14f);
            yield return Shot("775_dust_devil");
            yield return new WaitForSecondsRealtime(10f);
            Log($"dust devil: Otto {Describe(otto)}, things in the air {u.items.Count(w => w.flying)}");
            f.TestEndHazard();
            otto.hunger = 100;
            f.TestHazard(HazardKind.HeatWave);
            yield return new WaitForSecondsRealtime(8f);
            yield return Shot("776_heat_wave");
            Log($"heat wave: Otto's food {otto.hunger:0} after 8 s, engines x{VesselSim.HeatBoost}");
            f.TestEndHazard();

            // orbit: micrometeoroids, a blackout
            double r = home.radius + 150000;
            var odir = CelestialBody.DirectionFromLatLon(0, 30);
            var opos = home.FixedToInertial(odir, u.ut) * r;
            var orb = Crewed(VesselSim.CreateFree(VesselDesign.Stack("Orbit hazard test", "chute.s", "pod.wren", "shield.s"), home, opos,
                CelestialBody.EastAt(opos.normalized) * Math.Sqrt(home.mu / r), Quaternion.FromToRotation(Vector3.up, opos.normalized.ToFloat())), u, "Iris Ardling");
            root.Fly(orb);
            yield return new WaitForSecondsRealtime(2f);
            f = root.flight;
            f.TestHazard(HazardKind.Micrometeoroids);
            yield return new WaitForSecondsRealtime(22f);
            yield return Shot("777_micrometeoroids");
            Log($"micrometeoroids: worst part damage {orb.parts.Max(p => p.damage):0.00}");
            f.TestEndHazard();
            f.TestHazard(HazardKind.Blackout);
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"blackout: connected {orb.connected}");
            yield return Shot("778_blackout");
            f.TestEndHazard();
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"after the blackout: connected {orb.connected}");
            Log("HAZARDS TOUR DONE");
        }

        static VesselSim Crewed(VesselSim v, Universe u, string who)
        {
            foreach (var p in v.parts) p.crew.Clear();
            v.parts.First(p => p.def.crew > 0).crew.Add(who);
            v.hasLaunched = true;
            v.career = u.career;
            u.Add(v);
            return v;
        }
    }
}
