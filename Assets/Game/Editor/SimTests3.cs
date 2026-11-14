using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 3 tests: part trees, symmetry, side boosters, abort, legs, RCS.</summary>
    public static partial class SimTests
    {
        static void RunMilestone3()
        {
            Run("Symmetry: 4 side boosters sit 90 deg apart and keep the CoM on the axis", RadialSymmetry);
            Run("Mirror symmetry: a fin pair is reflected across the axis", MirrorSymmetry);
            Run("Re-root keeps every part exactly where it was", RerootGeometry);
            Run("Subassembly: remove and re-insert side boosters in symmetry", SubassemblyRoundTrip);
            Run("Side boosters: auto staging and delta-v", BoosterStaging);
            Run("Lopsided thrust spins the rocket; symmetric thrust doesn't", LopsidedThrust);
            Run("Booster rocket on autopilot: drops spent boosters and the tower, reaches orbit", BoosterAscent);
            Run("Abort at 2.5 km: escape tower pulls the capsule clear, it lands under its parachute", AbortTest);
            Run("Landing legs: touchdown at 9 m/s survives on legs, breaks the engine without", LegsLanding);
            Run("RCS translation: 5 s of thrust gives F/m*t and burns monopropellant", RcsTranslation);
            Run("Old saved rockets (stack format) still load", LegacyLoad);
        }

        static float AngleAround(Vector3 p) => Mathf.Atan2(p.z, p.x) * Mathf.Rad2Deg;

        static string RadialSymmetry()
        {
            var d = VesselDesign.Stack("core", "probe.spark", "tank.s4", "eng.kestrel");
            int tank = d.nodes.FindIndex(n => n.id == "tank.s4");
            var roots = DesignOps.AttachSurfaceAt(d, tank, 0.5f, 10f, "srb.cinder", 4, false);
            Check(roots.Count == 4, $"{roots.Count} copies");
            var ang = roots.Select(i => AngleAround(DesignOps.Center(d.nodes[i], PartCatalog.Get("srb.cinder")))).OrderBy(a => a).ToList();
            for (int k = 1; k < 4; k++) Check(Mathf.Abs(ang[k] - ang[k - 1] - 90f) < 0.01f, $"spacing {ang[k] - ang[k - 1]:F3}");
            Check(roots.Select(i => d.nodes[i].sym).Distinct().Count() == 1 && d.nodes[roots[0]].sym != 0, "copies don't share a symmetry group");
            Vector3 com = DesignOps.CenterOfMass(d);
            Check(new Vector2(com.x, com.z).magnitude < 1e-3f, $"CoM off axis by {new Vector2(com.x, com.z).magnitude:F4} m");
            float r = new Vector2(DesignOps.Center(d.nodes[roots[0]], PartCatalog.Get("srb.cinder")).x, DesignOps.Center(d.nodes[roots[0]], PartCatalog.Get("srb.cinder")).z).magnitude;
            Check(Mathf.Abs(r - 1.3f) < 0.01f, $"booster centre {r:F3} m from axis, expected 1.30 (touching)");
            return $"angles {string.Join(", ", ang.Select(a => a.ToString("0")))} deg, boosters {r:F2} m out, CoM offset {new Vector2(com.x, com.z).magnitude:E1} m";
        }

        static string MirrorSymmetry()
        {
            var d = VesselDesign.Stack("core", "probe.spark", "tank.s4", "eng.kestrel");
            int tank = d.nodes.FindIndex(n => n.id == "tank.s4");
            var roots = DesignOps.AttachSurfaceAt(d, tank, 0.9f, 30f, "fin.r", 1, true);
            Check(roots.Count == 2, $"{roots.Count} copies");
            var a = DesignOps.Center(d.nodes[roots[0]], PartCatalog.Get("fin.r"));
            var b = DesignOps.Center(d.nodes[roots[1]], PartCatalog.Get("fin.r"));
            // Mirror plane is the vessel's centre plane (x = 0): x flips, height and z stay.
            Check(Mathf.Abs(a.y - b.y) < 1e-4f && Mathf.Abs(a.x + b.x) < 1e-3f && Mathf.Abs(a.z - b.z) < 1e-3f, $"not mirrored: {a} vs {b}");
            return $"fins at {AngleAround(a):0} and {AngleAround(b):0} deg";
        }

        static string RerootGeometry()
        {
            var d = VesselDesign.Broadside();
            string Key(PartNode n) => $"{n.id}@{n.pos.x:F3},{n.pos.y:F3},{n.pos.z:F3}";
            var before = d.nodes.Select(Key).OrderBy(s => s).ToList();
            int srb = d.nodes.FindIndex(n => n.id == "srb.cinder");
            DesignOps.Reroot(d, srb);
            Check(d.nodes[0].id == "srb.cinder" && d.nodes[0].parent == -1, "SRB isn't the root");
            var after = d.nodes.Select(Key).OrderBy(s => s).ToList();
            Check(before.SequenceEqual(after), "parts moved");
            for (int i = 0; i < d.nodes.Count; i++)
            {
                int k = i, steps = 0;
                while (d.nodes[k].parent >= 0 && steps++ < 200) k = d.nodes[k].parent;
                Check(k == 0, $"part {i} doesn't lead to the root");
            }
            string invalid = d.Validate();
            int pod = d.nodes.FindIndex(n => n.id == "pod.wren");
            DesignOps.Reroot(d, pod);
            Check(d.Validate() == null, "design invalid after re-rooting back: " + d.Validate());
            return $"{d.nodes.Count} parts unchanged; rooted on a booster it says \"{invalid}\"";
        }

        static string SubassemblyRoundTrip()
        {
            var d = VesselDesign.Broadside();
            int n0 = d.nodes.Count, stages0 = d.Stages().Count;
            int dec = d.nodes.FindIndex(n => n.id == "dec.r");
            var group = DesignOps.SymmetryGroup(d, dec);
            var sub = DesignOps.Extract(d, dec);
            var kill = new HashSet<int>();
            foreach (int g in group) foreach (int k in DesignOps.Subtree(d, g)) kill.Add(k);
            DesignOps.Remove(d, kill);
            Check(d.nodes.Count == n0 - 6, $"removed {n0 - d.nodes.Count} parts, expected 6");
            // find the lower FT-4 again (indices changed)
            int newTank = -1;
            for (int i = 0; i < d.nodes.Count; i++) if (d.nodes[i].id == "tank.s4" && (newTank < 0 || d.nodes[i].pos.y < d.nodes[newTank].pos.y)) newTank = i;
            DesignOps.InsertSymmetric(d, sub, newTank, Attach.Surface, sub[0].pos, sub[0].rot, 2, false);
            Check(d.nodes.Count == n0, $"{d.nodes.Count} parts after re-insert, expected {n0}");
            Vector3 com = DesignOps.CenterOfMass(d);
            Check(new Vector2(com.x, com.z).magnitude < 1e-3f, "CoM off axis");
            Check(d.Stages().Count == stages0, "staging changed");
            return $"{n0} parts, {stages0} stages restored, CoM on axis";
        }

        static string BoosterStaging()
        {
            var d = VesselDesign.Broadside();
            Check(d.Validate() == null, "invalid: " + d.Validate());
            var st = d.Stages();
            var defs = d.Defs();
            Check(st.Count == 6, $"{st.Count} stages: {string.Join(" | ", st.Select(s => s.Describe(defs)))}");
            Check(st[0].ignite.Count == 3, "stage 1 should light the core and both boosters");
            Check(st[1].decouple.Count == 2 && st[1].ignite.Count == 0, "stage 2 should drop the two boosters");
            Check(st[2].jettison.Count == 1 && st[2].decouple.Count == 0, "stage 3 should jettison the escape tower");
            Check(st[3].decouple.Count == 1 && st[3].ignite.Count == 1, "stage 4 should separate the core and light the upper stage");
            var reps = Staging.AnalyzeDesign(d, 9.81);
            var starter = Staging.AnalyzeDesign(VesselDesign.Default(), 9.81);
            double dv = reps.Sum(r => r.dvVac), dvS = starter.Sum(r => r.dvVac);
            Check(dv > dvS + 300, $"boosters only add {dv - dvS:F0} m/s");
            return string.Join(" | ", st.Select(s => s.Describe(defs))) + $"\n      total {dv:F0} m/s vs starter {dvS:F0} m/s, liftoff TWR {reps[0].twrAsl:F2}";
        }

        static VesselDesign CoreWithBoosters(int count)
        {
            var d = VesselDesign.Stack("lopsided", "probe.spark", "tank.s4", "eng.kestrel");
            int tank = d.nodes.FindIndex(n => n.id == "tank.s4");
            var decs = DesignOps.AttachSurfaceAt(d, tank, 0.5f, 0f, "dec.r", count, false);
            foreach (int dec in decs) DesignOps.AttachToOuterFace(d, dec, "srb.cinder");
            return d;
        }

        static double SpinAfterIgnition(VesselDesign d)
        {
            var b = Body;
            double rr = b.radius + 200_000;
            var sim = VesselSim.CreateFree(d, b, new Vector3d(rr, 0, 0), new Vector3d(0, 0, -Math.Sqrt(b.mu / rr)), Quaternion.identity);
            sim.sas = SasMode.Off;
            sim.ActivateNextStage();
            double ut = 0;
            for (int i = 0; i < 100; i++) { sim.Step(0.02, ut); ut += 0.02; }
            return sim.angVel.magnitude;
        }

        static string LopsidedThrust()
        {
            double one = SpinAfterIgnition(CoreWithBoosters(1));
            double two = SpinAfterIgnition(CoreWithBoosters(2));
            Check(one > 0.05, $"one booster only spun it at {one:F4} rad/s");
            Check(two < 1e-3, $"two symmetric boosters spun it at {two:F5} rad/s");
            return $"after 2 s: one booster {one:F3} rad/s, two boosters {two:E1} rad/s";
        }

        static string BoosterAscent()
        {
            var b = Body;
            var sim = VesselSim.CreateOnPad(VesselDesign.Broadside(), b, 0);
            var ap = new FlightAutopilot();
            double ut = 0, boosterAlt = double.NaN, towerAlt = double.NaN;
            int steps = 0, pieces = 0;
            while (steps++ < 200_000 && !sim.destroyed && ap.phase != FlightAutopilot.Phase.Done)
            {
                if (ap.Update(sim, ut))
                {
                    int before = sim.nextStage;
                    var spawned = sim.ActivateNextStage();
                    pieces += spawned.Count;
                    if (before == 1) boosterAlt = sim.Altitude;
                    if (spawned.Exists(s => s.parts.Exists(p => p.def.kind == PartKind.EscapeTower))) towerAlt = sim.Altitude;
                }
                if (steps % 500 == 1)
                {
                    var oo = Orbit.FromState(sim.pos, sim.vel, b.mu, ut);
                    Vector3d upv = sim.pos.normalized;
                    double vs = Vector3d.Dot(sim.vel, upv);
                    Out.AppendLine($"      T={ut,6:0.0} {ap.phase,-11} alt={sim.Altitude / 1000,6:0.0}km vs={vs,5:0} hs={(sim.vel - upv * vs).magnitude,5:0} thr={sim.throttle:0.00} F={sim.thrustNow,4:0}kN stg={sim.nextStage} m={sim.mass:0.00}t q={sim.dynPressure / 1000:0.0}kPa ap={(oo.Apoapsis - b.radius) / 1000,6:0.0}");
                }
                sim.Step(0.02, ut);
                ut += 0.02;
            }
            Check(!sim.destroyed, "destroyed: " + sim.destroyReason);
            Check(ap.phase == FlightAutopilot.Phase.Done, $"autopilot stuck in {ap.phase}");
            Check(!double.IsNaN(boosterAlt), "boosters were never dropped");
            Check(!double.IsNaN(towerAlt), "escape tower was never jettisoned");
            var o = Orbit.FromState(sim.pos, sim.vel, b.mu, ut);
            Check(o.Periapsis - b.radius > b.atmosphereHeight, $"not in orbit: Pe {(o.Periapsis - b.radius) / 1000:F1} km");
            double left = sim.StageReports().Sum(r => r.dvVac);
            Check(left > 100, $"only {left:F0} m/s left in orbit");
            return $"boosters dropped at {boosterAlt / 1000:F1} km, tower at {towerAlt / 1000:F1} km, {pieces} pieces shed; orbit {(o.Apoapsis - b.radius) / 1000:F1} x {(o.Periapsis - b.radius) / 1000:F1} km with {left:F0} m/s left";
        }

        static string AbortTest()
        {
            var b = Body;
            var sim = VesselSim.CreateOnPad(VesselDesign.Broadside(), b, 0);
            var ap = new FlightAutopilot();
            double ut = 0;
            for (int i = 0; i < 20_000 && sim.Altitude < 2500 && !sim.destroyed; i++)
            {
                if (ap.Update(sim, ut)) sim.ActivateNextStage();
                sim.Step(0.02, ut); ut += 0.02;
            }
            Check(!sim.destroyed && sim.Altitude >= 2500, "never reached 2.5 km");
            sim.steerDirection = null;
            var rest = sim.Abort();
            Check(rest.Count == 1, $"abort split into {rest.Count + 1} pieces, expected 2");
            Check(sim.parts.Exists(p => p.def.kind == PartKind.Command) && sim.parts.Count == 3, $"capsule side has {sim.parts.Count} parts");
            var lower = rest[0];
            for (int i = 0; i < 200; i++) { sim.Step(0.02, ut); lower.Step(0.02, ut); ut += 0.02; }
            double sep = (sim.pos - lower.pos).magnitude;
            Check(sep > 60, $"only {sep:F0} m clear after 4 s");
            for (int i = 0; i < 100; i++) { sim.Step(0.02, ut); ut += 0.02; }
            while (sim.HasStagesLeft) sim.ActivateNextStage(); // jettison the tower, arm the parachute
            Check(!sim.parts.Exists(p => p.def.kind == PartKind.EscapeTower), "tower still attached");
            for (int i = 0; i < 100_000 && !sim.landed && !sim.destroyed; i++) { sim.Step(0.02, ut); ut += 0.02; }
            Check(sim.landed && !sim.destroyed, "capsule didn't land safely: " + sim.destroyReason);
            return $"{sep:F0} m clear of the rocket after 4 s; landed T+{ut:F0}s";
        }

        static VesselSim LanderAboveGround(bool legs, double sinkRate, bool overSea = false)
        {
            var d = VesselDesign.Stack("lander", "pod.wren", "tank.s2", "eng.wisp");
            if (legs)
            {
                int tank = d.nodes.FindIndex(n => n.id == "tank.s2");
                DesignOps.AttachSurfaceAt(d, tank, 0.85f, 45f, "leg.s", 4, false);
            }
            var b = Body;
            // open grass south-east of the pad (milestone 25: the old spot, 5 N 30 E, is sea, and splashdowns are gentler)
            Vector3d up = overSea ? CelestialBody.DirectionFromLatLon(5, 30) : LaunchSite.DirectionAt(b, 600, -700);
            var sim = VesselSim.CreateFree(d, b, up * (b.radius + 100), Vector3d.zero, Quaternion.FromToRotation(Vector3.up, up.ToFloat()));
            foreach (var p in sim.parts) if (p.def.kind == PartKind.Leg) { p.legsDeployed = true; p.legDeploy = 1f; }
            float low = float.MaxValue;
            foreach (var p in sim.parts) foreach (var c in sim.ContactPoints(p)) low = Mathf.Min(low, c.y);
            sim.pos = up * (b.radius + (sim.com.y - low) + 0.3);
            sim.vel = b.SurfaceVelocity(sim.pos) - up * sinkRate;
            sim.sas = SasMode.Stability;
            return sim;
        }

        static string LegsLanding()
        {
            string Land(VesselSim s)
            {
                double ut = 0;
                for (int i = 0; i < 2000 && !s.landed && !s.destroyed; i++) { s.Step(0.02, ut); ut += 0.02; }
                return s.destroyed ? "destroyed: " + s.destroyReason : s.landed ? "landed" : "still falling";
            }
            string withLegs = Land(LanderAboveGround(true, 9));
            string without = Land(LanderAboveGround(false, 9));
            string sea = Land(LanderAboveGround(false, 9, true));
            Check(withLegs == "landed", "with legs: " + withLegs);
            Check(without.StartsWith("destroyed"), "without legs: " + without);
            Check(Terrain.IsWater(Body, CelestialBody.DirectionFromLatLon(5, 30)) && sea == "landed", "splashdown without legs at 9 m/s: " + sea);
            return $"with legs: {withLegs}; without: {without}; in the sea: {sea}";
        }

        static string RcsTranslation()
        {
            var d = VesselDesign.Stack("rcs", "probe.spark", "mono.s");
            int tank = d.nodes.FindIndex(n => n.id == "mono.s");
            DesignOps.AttachSurfaceAt(d, tank, 0.5f, 0f, "rcs.b", 4, false);
            var b = Body;
            double rr = b.radius + 200_000;
            Vector3d r0 = new Vector3d(rr, 0, 0), v0 = new Vector3d(0, 0, -Math.Sqrt(b.mu / rr));
            var sim = VesselSim.CreateFree(d, b, r0, v0, Quaternion.identity);
            var refSim = VesselSim.CreateFree(d, b, r0, v0, Quaternion.identity);
            sim.sas = refSim.sas = SasMode.Off;
            sim.rcsOn = true;
            sim.inputTranslate = new Vector3(0, 1, 0);
            double mono0 = sim.MonoLeft, ut = 0;
            sim.RecomputeMass();
            double expectedA = 4.0 / sim.mass;
            for (int i = 0; i < 250; i++) { sim.Step(0.02, ut); refSim.Step(0.02, ut); ut += 0.02; }
            double dv = (sim.vel - refSim.vel).magnitude;
            double expect = expectedA * 5;
            double used = (mono0 - sim.MonoLeft) * 1000;
            Check(Math.Abs(dv - expect) / expect < 0.03, $"dv {dv:F3} m/s vs expected {expect:F3}");
            Check(used > 5 && used < 12, $"used {used:F1} kg of monopropellant");
            return $"dv {dv:F2} m/s (expected {expect:F2}), {used:F1} kg monopropellant used";
        }

        static string LegacyLoad()
        {
            string json = "{\"name\":\"Old\",\"parts\":[\"chute.s\",\"pod.wren\",\"dec.s\",\"tank.s2\",\"eng.kestrel\"]}";
            var d = JsonUtility.FromJson<VesselDesign>(json);
            d.Upgrade();
            Check(d.nodes.Count == 5, $"{d.nodes.Count} parts after upgrade");
            Check(d.nodes[0].id == "pod.wren", "capsule should be the root");
            Check(d.Validate() == null, "invalid: " + d.Validate());
            return $"converted to {d.nodes.Count} parts, {d.Stages().Count} stages";
        }
    }
}
