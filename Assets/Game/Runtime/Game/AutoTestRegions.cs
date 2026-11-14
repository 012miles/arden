using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        static Vector3d FindRegionSpot(CelestialBody b, string name, Func<Vector3d, bool> also = null)
        {
            var rng = new System.Random(11);
            for (int i = 0; i < 20000; i++)
            {
                var d = new Vector3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
                if (d.sqrMagnitude > 1 || d.sqrMagnitude < 1e-3) continue;
                d = d.normalized;
                if (Regions.At(b, d) == name && (also == null || also(d))) return d;
            }
            return Vector3d.up;
        }

        /// <summary>
        /// Milestone 10a: the region map over Arden, Tal and Vesk; a lander in one of Tal's great craters with the HUD and
        /// science panel naming the region; and mission control offering a region contract.
        /// </summary>
        IEnumerator RegionsTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var c = u.career;
            var home = root.home;
            var tal = root.moon;
            var vesk = root.bodies.First(b => b.name == "Vesk");

            var ship = Fly(VesselDesign.Stack("Mapper", "probe.spark", "tank.s1"), home, 120_000, 20, 30, u.ut, true);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.0f);
            f.mapView = true;
            f.SetMapLayer(false, true);
            f.SetMapView(home, (float)(home.radius * 3.0), 40f, 25f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("140_arden_regions");
            f.SetMapView(tal, (float)(tal.radius * 3.3), 20f, 20f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("141_tal_regions");
            f.SetMapView(vesk, (float)(vesk.radius * 3.3), 60f, 20f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("142_vesk_regions");
            f.SetMapLayer(false, false);
            f.mapView = false;

            // A crewed lander with a thermometer, down in one of Tal's great craters, sun up
            var spot = FindRegionSpot(tal, "Great Craters", d => Vector3d.Dot(tal.FixedToInertial(d, u.ut), Sun.DirectionAt(GameRoot.I.universe.ut)) > 0.4);
            var pilot = new CrewMember { name = "Rafe Okonkwo", role = CrewRole.Scientist, log = Crew.XpMask, state = 1 };
            c.roster.Add(pilot);
            var d0 = VesselDesign.Stack("Crater lab", "chute.s", "pod.wren", "tank.s2", "eng.wisp");
            DesignOps.AttachSurfaceAt(d0, d0.nodes.FindIndex(n => n.id == "tank.s2"), 0.95f, 45f, "leg.s", 4, false);
            DesignOps.AttachSurfaceAt(d0, d0.nodes.FindIndex(n => n.id == "tank.s2"), 0.5f, 0f, "sci.therm", 1, false);
            var lander = VesselSim.CreateLanded(d0, tal, spot, u.ut);
            foreach (var p in lander.parts) p.crew.Clear();
            lander.parts.First(p => p.def.id == "pod.wren").crew.Add(pilot.name);
            pilot.vesselId = lander.id;
            lander.throttle = 0;
            lander.hasLaunched = true;
            lander.RecomputeMass();
            u.Add(lander);
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(1.2f);
            f.SetCamera(150f, 14f, 24f);
            f.ShowSciencePanel(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("143_tal_crater_science");
            Log($"lander in {Regions.Under(lander, f.ut)}, radar {lander.RadarAltitude(f.ut):0.00} m");
            f.ShowSciencePanel(false);

            // Mission control offering region work
            c.Start(GameMode.Career);
            foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id);
            c.records |= Records.Launch | Records.Space | Records.Orbit | Records.TalFlyby | Records.TalOrbit | Records.TalLanding;
            c.RefreshOffers(u.ut);
            c.contracts.Add(c.RegionScienceOffer("Tal", Situation.Landed, "Great Craters"));
            c.contracts.Add(c.RegionScienceOffer("Arden", Situation.FlyingLow, "Ice Caps"));
            root.EnterHangar();
            root.hangar.ShowMissions(true);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("144_region_contracts");
            Log($"offers: {string.Join(", ", c.Offered.Select(k => k.title))}");
        }
    }
}
