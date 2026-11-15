using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 43 tests: team presets, what the crew feel, sandbox rooms without a budget.</summary>
    public static partial class SimTests
    {
        static void RunMilestone43()
        {
            Run("Team presets: ten ships for a whole room, each valid, with seats for four or more, and enough push to leave the pad", TeamPresetsTest);
            Run("What the crew feel: 1 g up out of the floor on the pad, weightless coasting, out of the ceiling upside down, pressed down under thrust", FeltTest);
            Run("Sandbox rooms: no budget (nothing over budget, the report doesn't count money); campaign rooms keep it", SandboxBudgetTest);
        }

        static string TeamPresetsTest()
        {
            var all = Presets.All();
            var lines = new List<string>();
            foreach (var name in Presets.TeamOnes)
            {
                var found = all.FirstOrDefault(p => p.label == name);
                Check(found.design != null, "missing team preset " + name);
                var d = found.design;
                Check(d.Validate() == null, $"{name}: {d.Validate()}");
                Check(DesignOps.TreeProblem(d) == null, $"{name}: {DesignOps.TreeProblem(d)}");
                int seats = d.Defs().Sum(p => p.crew);
                Check(seats >= 4, $"{name}: {seats} seats");
                var reps = Staging.AnalyzeDesign(d, 9.81);
                var first = reps.FirstOrDefault(r => r.hasEngines);
                Check(first != null && first.twrAsl >= 1.15, $"{name}: liftoff push/weight {first?.twrAsl:0.00}");
                lines.Add($"{name.Substring(6).Split(' ')[0]} {seats}s {reps.Sum(r => r.dvVac):0}m/s {first?.twrAsl:0.0}x");
            }
            return string.Join(", ", lines);
        }

        static string FeltTest()
        {
            var b = Body;
            Vector3d up = LaunchSite.DirectionAt(b, 600, -700);
            var pad = VesselSim.CreateLanded(VesselDesign.Stack("Felt", "chute.s", "pod.wren", "tank.s2", "eng.kestrel"), b, up, 0);
            double t = 0;
            for (int i = 0; i < 100; i++) { pad.Step(0.02, t); t += 0.02; }
            Check(pad.feltLocal.y > 8.5f && Mathf.Abs(pad.feltLocal.x) < 1f && Mathf.Abs(pad.feltLocal.z) < 1f, $"standing on the pad: up out of the floor at 1 g ({pad.feltLocal})");
            // upside down on the ground: the pull's out of the ceiling
            var flipped = VesselSim.CreateLanded(VesselDesign.Stack("Flipped", "pod.wren"), b, up, 0);
            flipped.TestFlipLanded(t);
            for (int i = 0; i < 100; i++) { flipped.Step(0.02, t); t += 0.02; }
            Check(flipped.feltLocal.y < -8f, $"upside down: the floor's above you ({flipped.feltLocal})");
            // coasting high above: weightless
            var high = VesselSim.CreateFree(VesselDesign.Stack("Coaster", "pod.wren"), b, up * (b.radius + 200_000), b.SurfaceVelocity(up * (b.radius + 200_000)), Quaternion.FromToRotation(Vector3.up, up.ToFloat()));
            for (int i = 0; i < 100; i++) { high.Step(0.02, t); t += 0.02; }
            Check(high.feltLocal.magnitude < 0.5f, $"coasting: weightless ({high.feltLocal.magnitude:0.00} m/s²)");
            // under thrust: pressed down harder
            var burn = VesselSim.CreateFree(VesselDesign.Stack("Burner", "pod.wren", "tank.s2", "eng.kestrel"), b, up * (b.radius + 200_000), b.SurfaceVelocity(up * (b.radius + 200_000)), Quaternion.FromToRotation(Vector3.up, up.ToFloat()));
            burn.throttle = 1;
            foreach (var p in burn.parts) if (p.def.IsEngine) p.ignited = true;
            for (int i = 0; i < 100; i++) { burn.Step(0.02, t); t += 0.02; }
            Check(burn.feltLocal.y > 15f, $"under thrust: pressed into the floor ({burn.feltLocal.y / 9.81f:0.0} g)");
            return $"pad {pad.feltLocal.y / 9.81f:0.00} g, flipped {flipped.feltLocal.y / 9.81f:0.00} g, coasting {high.feltLocal.magnitude:0.00}, thrust {burn.feltLocal.y / 9.81f:0.0} g";
        }

        static string SandboxBudgetTest()
        {
            var sc = Expedition.Scenario("tal");
            var huge = Presets.TeamBrume();
            var camp = Expedition.Evaluate(huge, sc, 1, 4);
            var sand = Expedition.Evaluate(huge, sc, 1, 4, true);
            Check(camp.cost > camp.budget && camp.blockers.Any(x => x.StartsWith("Over budget")), $"a campaign room: a {camp.cost:N0} ship is over the {camp.budget:N0} budget");
            Check(sand.budget == 0 && !sand.blockers.Any(x => x.StartsWith("Over budget")), "a sandbox room: no budget, nothing over it");
            return $"campaign budget {camp.budget:N0}, sandbox none (ship {sand.cost:N0})";
        }
    }
}
