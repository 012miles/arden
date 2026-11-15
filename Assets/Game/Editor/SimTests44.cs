using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 44 tests: the Mission Review (NASA Space Apps resources), and a plane's cabin frame.</summary>
    public static partial class SimTests
    {
        static void RunMilestone44()
        {
            Run("Mission Review: the four segments, the disciplines, maturity levels and real missions follow the design", ReviewTest);
            Run("Plane cabins: on the runway the floor's down; a plane standing on its tail lays you on your back", PlaneCabinTest);
        }

        static string ReviewTest()
        {
            var orbiter = MissionReview.Review(Presets.Orbiter());
            Check(orbiter.segments.Count == 4 && orbiter.disciplines.Count == 6, $"lines: {orbiter.segments.Count} segments, {orbiter.disciplines.Count} disciplines");
            Check(orbiter.cml >= 3, $"the Orbiter preset: {orbiter.cmlName} ({string.Join(", ", orbiter.segments.Concat(orbiter.disciplines).Where(l => !l.ok).Select(l => l.area + ": " + l.text))})");
            Check(orbiter.segments[0].ok && orbiter.dv > 3300, $"the Orbiter launches ({orbiter.dv:0} m/s, {orbiter.twr:0.00}x)");
            // a capsule going to orbit with no heat shield: the thermal line goes red, and it can't be a point design
            var noShield = VesselDesign.Stack("No shield", "chute.s", "pod.wren", "tank.l4", "tank.l4", "eng.heron");
            var ns = MissionReview.Review(noShield);
            var thermal = ns.disciplines.First(l => l.area == "Thermal");
            Check(!thermal.ok || ns.dv < 3300, $"no heat shield ({ns.dv:0} m/s): thermal says {thermal.text}");
            Check(ns.cml < 4 || ns.dv < 3300, "no heat shield can't be CML 4");
            // just a probe core: CML 1, and it can't launch
            var core = MissionReview.Review(VesselDesign.Stack("Core", "probe.spark"));
            Check(core.cml <= 1 || !core.segments[0].ok, $"a lone probe core: {core.cmlName}");
            Check(!core.segments[0].ok, "a lone probe core launches?");
            // real missions: a plane is a research plane, a crewed lander for Tal is Apollo
            var plane = MissionReview.Review(VesselDesign.Kite());
            Check(plane.realMissions.Any(m => m.Contains("plane")) && !plane.realMissions.Any(m => m.Contains("Shuttle")), $"the plane is like: {plane.realMissions.FirstOrDefault()}");
            var lander = MissionReview.Review(Presets.TalLander());
            Check(lander.realMissions.Count > 0, "the Tal Lander is like nothing");
            Check(MissionReview.Review(new VesselDesign()).cml == 0, "an empty design");
            return $"Orbiter {orbiter.cmlName}; no shield {ns.cmlName}; core {core.cmlName}; plane like \"{plane.realMissions[0].Substring(0, 24)}...\"; Tal Lander like \"{lander.realMissions[0].Substring(0, 24)}...\"";
        }

        static string PlaneCabinTest()
        {
            var b = Body;
            var frame = Quaternion.LookRotation(Vector3.up, Vector3.forward);   // a plane's cabin in the ship (FlightCockpit): forward = the nose, up = the top
            var s = VesselSim.CreateOnRunway(VesselDesign.Kite(), b, 0);
            double ut = 0;
            for (int i = 0; i < 150; i++) { s.Step(0.02, ut); ut += 0.02; }
            Vector3 felt = Quaternion.Inverse(frame) * s.feltLocal;
            Check(felt.y > 8.5f && Mathf.Abs(felt.z) < 1.5f, $"on the runway the floor should be down ({felt})");
            // the same plane standing on its tail (a shuttle on the pad): the pull is to the back wall
            var up = VesselSim.CreateLanded(VesselDesign.Kite(), b, LaunchSite.DirectionAt(b, 600, -700), 0);
            for (int i = 0; i < 150; i++) { up.Step(0.02, ut); ut += 0.02; }
            Vector3 f2 = Quaternion.Inverse(frame) * up.feltLocal;
            Check(f2.z > 8f, $"standing on its tail: lying back against the rear wall ({f2})");
            return $"runway {felt.y / 9.81f:0.00} g down, on its tail {f2.z / 9.81f:0.00} g back";
        }
    }
}
