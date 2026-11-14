using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 17 tests: queued maneuver nodes, the flight computer flying them, close-flyby contracts.</summary>
    public static partial class SimTests
    {
        static void RunMilestone17()
        {
            Run("Queued nodes: the next burn is planned from the path the first one leaves", NodeChain);
            Run("Flight computer: flies a planned intercept with real (finite) burns, both of them, and arrives alongside", ComputerIntercept);
            Run("Flight computer: waits until pointed before burning, then stops on the dot", ComputerPointing);
            Run("Close-pass contracts: offered after a flyby, done by skimming low without landing", SkimContracts);
        }

        static string NodeChain()
        {
            var home = Home14;
            double r0 = home.radius + 300_000, v0 = Math.Sqrt(home.mu / r0);
            var path = Patcher.Predict(home, new Vector3d(r0, 0, 0), new Vector3d(0, 0, -v0), 0, 3);
            var n1 = new ManeuverNode { ut = 600, prograde = 120 };
            n1.Update(path);
            var later = new List<ManeuverNode> { new ManeuverNode { ut = 600 + 2000, prograde = 80 } };
            NodeComputer.UpdateChain(n1, later);
            var n2 = later[0];
            Check(n1.valid && n2.valid, "nodes not valid");
            n1.planned[0].orbit.StateAt(n2.ut, out var expect, out _);
            Check((n2.pos - expect).magnitude < 1, $"the second node sits {(n2.pos - expect).magnitude:0} m off the first one's path");
            var o2 = n2.planned[0].orbit;
            Check(o2.Apoapsis > n1.planned[0].orbit.Apoapsis, "the second prograde burn didn't raise the orbit further");
            NodeComputer.UpdateChain(null, later);
            Check(!later[0].valid, "a queued node stayed valid with nothing before it");
            var list = FlightEvents.Build(VesselSim.CreateFree(VesselDesign.WranglerTug(), home, new Vector3d(r0, 0, 0), new Vector3d(0, 0, -v0), Quaternion.identity),
                path, n1, null, double.NaN, 0, null, null, 0, new List<ManeuverNode> { n2 });
            NodeComputer.UpdateChain(n1, new List<ManeuverNode> { n2 });
            list = FlightEvents.Build(VesselSim.CreateFree(VesselDesign.WranglerTug(), home, new Vector3d(r0, 0, 0), new Vector3d(0, 0, -v0), Quaternion.identity),
                path, n1, null, double.NaN, 0, null, null, 0, new List<ManeuverNode> { n2 });
            Check(list.Exists(e => e.kind == 6), "the events list doesn't show the queued burn");
            return $"second burn on the first's path; apoapsis {(n1.planned[0].orbit.Apoapsis - home.radius) / 1000:0} -> {(o2.Apoapsis - home.radius) / 1000:0} km; listed as 'Next burn'";
        }

        /// <summary>Steps a vessel through a queue of nodes with the flight computer until 'until'. Returns the burns made.</summary>
        static int FlyNodes(VesselSim v, List<ManeuverNode> nodes, ref double ut, double until, System.Text.StringBuilder log, Orbit matchOrbit = null)
        {
            int done = 0;
            bool started = false;
            var cur = nodes.Count > 0 ? nodes[0] : null;
            int idx = 0;
            v.sas = SasMode.Maneuver;
            while (ut < until && !v.destroyed)
            {
                if (cur != null)
                {
                    var st = NodeComputer.Fly(v, cur, ut, started, idx > 0 ? matchOrbit : null);
                    v.maneuverDir = st.dir;
                    v.throttle = st.throttle;
                    if (st.throttle > 0) started = true;
                    if (st.done)
                    {
                        v.throttle = 0;
                        log?.Append($" burn{idx + 1} done at {ut:0}s ({st.remaining:0.00} m/s left);");
                        done++;
                        idx++;
                        started = false;
                        cur = idx < nodes.Count ? nodes[idx] : null;
                        if (cur != null)
                        {
                            // re-plan the next one from where we really are (a speed match is re-aimed at the target, as the game does)
                            var now = Patcher.Predict(v.body, v.pos, v.vel, ut, 3);
                            var m = matchOrbit != null ? NodeComputer.MatchAt(v, now, matchOrbit, ut) : null;
                            if (m != null) { nodes[idx] = m; cur = m; } else cur.Update(now);
                        }
                        if (cur == null) { v.sas = SasMode.Stability; break; }   // all burns made: stop here, so the result is measured right away
                    }
                }
                v.Step(0.05, ut); ut += 0.05;
                if ((int)(ut / 0.05) % 2000 == 0) foreach (var pp in v.parts) pp.charge = pp.def.ecCapacity;   // the test tug has no solar panels
            }
            return done;
        }

        static string ComputerIntercept()
        {
            var home = Home14;
            double r0 = home.radius + 300_000, v0 = Math.Sqrt(home.mu / r0);
            var tug = VesselSim.CreateFree(VesselDesign.WranglerTug(), home, new Vector3d(r0, 0, 0), new Vector3d(0, 0, -v0), Quaternion.LookRotation(Vector3.right, Vector3.up));
            tug.ActivateNextStage();
            tug.throttle = 0;
            // The target: 600 km up, tilted 3 degrees, 120 degrees ahead
            double r1 = home.radius + 600_000, v1 = Math.Sqrt(home.mu / r1), ph = 120 * Math.PI / 180;
            var tp = new Vector3d(r1 * Math.Cos(ph), 0, -r1 * Math.Sin(ph));
            var tv = new Vector3d(-v1 * Math.Sin(ph), 0, -v1 * Math.Cos(ph));
            var tilt = Quaternion.AngleAxis(3f, Vector3.right);
            var target = Orbit.FromState(Vector3d.Rotate(tilt, tp), Vector3d.Rotate(tilt, tv), home.mu, 0);
            var me = Orbit.FromState(tug.pos, tug.vel, home.mu, 0);
            Check(Intercept.Plan(home, tug.pos, tug.vel, 0, target, Math.Max(me.Period * 2, Patcher.SynodicPeriod(me, target)), out var p), "no plan");
            // The two nodes, as the game sets them
            var path = Patcher.Predict(home, tug.pos, tug.vel, 0, 3);
            me.StateAt(p.burnUt, out var rb, out var vb);
            Intercept.ToNode(rb, vb, p.dv1, out double a1, out double b1, out double c1);
            var n1 = new ManeuverNode { ut = p.burnUt, prograde = a1, normal = b1, radial = c1 };
            n1.Update(path);
            var seg = n1.planned.Find(s => p.arriveUt >= s.tStart && p.arriveUt <= s.tEnd);
            double half2 = ManeuverNode.BurnTime(tug, p.dv2.magnitude) / 2;
            seg.orbit.StateAt(p.arriveUt - half2, out var ra, out var va);
            Intercept.ToNode(ra, va, p.dv2, out double a2, out double b2, out double c2);
            var n2 = new ManeuverNode { ut = p.arriveUt - half2, prograde = a2, normal = b2, radial = c2 };
            NodeComputer.UpdateChain(n1, new List<ManeuverNode> { n2 });
            var log = new System.Text.StringBuilder();
            double ut = 0;
            int burns = FlyNodes(tug, new List<ManeuverNode> { n1, n2 }, ref ut, p.arriveUt + 120, log, target);
            Check(!tug.destroyed, "destroyed: " + tug.destroyReason);
            Check(burns == 2, $"made {burns} of 2 burns;{log}");
            // Where it is and how it's moving relative to the target the moment the match burn ends
            target.StateAt(ut, out var rt, out var vtNow);
            double best = (tug.pos - rt).magnitude, rel = (tug.vel - vtNow).magnitude;
            // (the first burn's small finite-burn error leaves a few km to close with RCS; the speed is what the match fixes)
            Check(best < 5000, $"{best:0} m from the target at {rel:0.0} m/s;{log}");
            Check(rel < 1, $"{rel:0.00} m/s relative after the match;{log}");
            return $"planned {p.dv1.magnitude:0} + {p.dv2.magnitude:0} m/s; flew both with finite burns: {best:0} m from the target at {rel:0.00} m/s afterwards;{log}";
        }

        static string ComputerPointing()
        {
            var home = Home14;
            double r0 = home.radius + 300_000, v0 = Math.Sqrt(home.mu / r0);
            // Pointing the wrong way to start with: the computer must turn round before it burns
            var tug = VesselSim.CreateFree(VesselDesign.WranglerTug(), home, new Vector3d(r0, 0, 0), new Vector3d(0, 0, -v0), Quaternion.LookRotation(Vector3.forward, Vector3.up));
            tug.ActivateNextStage();
            tug.throttle = 0;
            var path = Patcher.Predict(home, tug.pos, tug.vel, 0, 3);
            var node = new ManeuverNode { ut = 120, prograde = 200 };
            node.Update(path);
            double plannedAp = node.planned[0].orbit.Apoapsis;
            double ut = 0, firstBurnAngle = -1;
            bool started = false;
            tug.sas = SasMode.Maneuver;
            for (int i = 0; i < 20_000; i++)
            {
                var st = NodeComputer.Fly(tug, node, ut, started);
                tug.maneuverDir = st.dir;
                tug.throttle = st.throttle;
                if (st.throttle > 0 && !started) { started = true; firstBurnAngle = Vector3.Angle(tug.Axis.ToFloat(), st.dir.ToFloat()); }
                if (st.done) { tug.throttle = 0; break; }
                tug.Step(0.02, ut); ut += 0.02;
            }
            Check(started, "never burned");
            Check(firstBurnAngle >= 0 && firstBurnAngle < 6, $"started burning {firstBurnAngle:0} deg off the burn direction");
            var o = Orbit.FromState(tug.pos, tug.vel, home.mu, ut);
            double err = Math.Abs(o.Apoapsis - plannedAp);
            Check(err < 3000, $"apoapsis {(o.Apoapsis - home.radius) / 1000:0.0} km, planned {(plannedAp - home.radius) / 1000:0.0} km");
            return $"turned round, first burned {firstBurnAngle:0.0} deg off; apoapsis {(o.Apoapsis - home.radius) / 1000:0.0} km vs planned {(plannedAp - home.radius) / 1000:0.0}";
        }

        static string SkimContracts()
        {
            var c = new Career();
            c.Start(GameMode.Career);
            c.records |= Records.Space | Records.Orbit | Records.TalFlyby;
            Contract skim = null;
            for (int i = 0; i < 40 && skim == null; i++)
            {
                c.contracts.RemoveAll(k => k.state == ContractState.Offered);
                c.seed = 500 + i;
                c.RefreshOffers(0);
                skim = c.contracts.FirstOrDefault(k => k.type == "skim" && k.state == ContractState.Offered);
            }
            Check(skim != null && skim.body == "Tal", $"no Tal close-pass offer ({skim?.body})");
            skim.state = ContractState.Active;
            var tal = System4[1];
            var high = VesselSim.CreateFree(D("p", "probe.spark"), tal, new Vector3d(tal.radius + 40_000, 0, 0), new Vector3d(0, 0, -800), Quaternion.identity);
            c.CheckVessel(high, 0);
            Check(skim.state == ContractState.Active, "completed 40 km up");
            var landed = VesselSim.CreateLanded(Hopper(), tal, CelestialBody.DirectionFromLatLon(0, 10), 0);
            landed.hasLaunched = true;
            c.CheckVessel(landed, 0);
            Check(skim.state == ContractState.Active, "completed by a landed vessel");
            var low = VesselSim.CreateFree(D("p", "probe.spark"), tal, new Vector3d(tal.radius + 12_000, 0, 0), new Vector3d(0, 0, -900), Quaternion.identity);
            c.CheckVessel(low, 0);
            Check(skim.state == ContractState.Completed, $"skimming at 12 km: {skim.state}");
            return $"{skim.title}: done at 12 km, not at 40 km or landed";
        }
    }
}
