using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 11b tests: Ochre and Mote, transfer windows, the deep space dish.</summary>
    public static partial class SimTests
    {
        static void RunMilestone11b()
        {
            Run("Ochre: a dusty planet beyond Arden in the same plane, thin air, its moon Mote inside its sphere of influence; regions match the ground", OchreWorld);
            Run("Transfer windows: the phase angle comes round to the right one at the window; the trip takes about half the transfer orbit", TransferWindows);
            Run("Transfer planner: from low Arden orbit, the planned burn reaches Ochre's sphere of influence when flown", TransferToOchre);
            Run("Ochre's thin air: a capsule under its parachute lands safely, just faster than at home", OchreParachute);
            Run("Ochre career: contracts open after escaping Arden, records and crew experience, return contracts, the deep space dish reaches it", OchreCareer);
            Run("Deep space stations: a dish near Ochre keeps its link home all through an Arden day; close to Arden you still need relays", OchreSignal);
        }

        static string OchreSignal()
        {
            var home = System4[0]; var o = Ochre;
            var none = new List<VesselSim>();
            VesselSim Near(bool dish, double t)
            {
                var d = D("probe", "probe.spark", "tank.s1");
                if (dish) DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s1"), 0.5f, 0f, "dish.d", 1, false);
                // 2,000 km off Ochre, on the side facing Arden (so Ochre isn't in the way)
                Vector3d toHome = (home.AbsolutePosition(t) - o.AbsolutePosition(t)).normalized;
                var v = VesselSim.CreateFree(d, o, toHome * (o.radius + 2_000_000), Vector3d.zero, Quaternion.identity);
                foreach (var p in v.parts) if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1; }
                return v;
            }
            int linked = 0, deep = 0;
            for (int h = 0; h < 12; h++)
            {
                double t = 3_000_000 + h * 1800; // every half hour through an Arden day
                var l = CommNet.Connect(Near(true, t), none, System4, t);
                if (l.Connected) linked++;
                if (l.Connected && (l.points[l.points.Count - 1] - CommNet.GroundStation(home, t)).magnitude > 1000) deep++;
                Check(!CommNet.Connect(Near(false, t), none, System4, t).Connected, "a probe at Ochre reached home without the dish");
            }
            Check(linked == 12, $"the dish near Ochre had a link only {linked} of 12 half-hours");
            Check(deep > 0, "the deep space stations never carried the signal");
            return $"linked all {linked} half-hours of an Arden day, {deep} of them through a deep space station";
        }

        static CelestialBody Ochre => System4.First(b => b.name == "Ochre");
        static CelestialBody Mote => System4.First(b => b.name == "Mote");

        static string OchreWorld()
        {
            var home = System4[0]; var sun = Helion; var o = Ochre; var m = Mote;
            Check(o.parent == sun && o.IsPlanet && m.parent == o, "Ochre doesn't go round the sun with Mote round it");
            double tilt = Vector3d.Angle(Vector3d.Cross(o.orbitZ, o.orbitX), Vector3d.Cross(home.orbitZ, home.orbitX)) * 180 / Math.PI;
            Check(tilt < 0.01, $"Ochre's orbit is tilted {tilt:0.0} deg to Arden's");
            Check(o.orbitRadius > home.orbitRadius * 1.3, "Ochre isn't further out than Arden");
            double g = o.SurfaceGravity, soi = o.SoiRadius;
            Check(g > 2 && g < 4.5 && o.HasAtmosphere && o.seaLevelDensity < 0.5, $"g {g:0.00}, air {o.seaLevelDensity:0.00} kg/m3");
            Check(m.orbitRadius + m.SoiRadius < soi, "Mote pokes out of Ochre's sphere of influence");
            var by = RegionSamples(o, 12000, 515);
            var names = Regions.Names(o);
            for (int i = 0; i < names.Length; i++)
            {
                double share = by.TryGetValue(i, out var l) ? l.Count / 12000.0 : 0;
                Check(share > 0.004 && share < 0.75, $"{names[i]} covers {share * 100:0.0}%");
            }
            Check(MeanHeight(o, by[2]) < MeanHeight(o, by[0]) - 300, "Ochre's canyons aren't below its plains");
            double worst = 0;
            foreach (var d in Directions(400, 516))
            {
                var side = Vector3d.Cross(d, new Vector3d(0, 1, 0));
                if (side.sqrMagnitude < 0.01) continue;
                worst = Math.Max(worst, Math.Abs(Terrain.Height(o, (d + side.normalized * (10 / o.radius)).normalized) - Terrain.Height(o, d)));
            }
            Check(worst < 9, $"the ground jumps {worst:0.0} m over 10 m");
            Check(Regions.Names(m).Length == 3, "Mote has no regions");
            return $"Ochre {o.orbitRadius / 1e9:0.0} Gm out, g {g:0.00}, air {o.atmosphereHeight / 1000:0} km, SOI {soi / 1e6:0} Mm; Mote {m.orbitRadius / 1000:0} km out; year {o.OrbitPeriod / 86400:0} d";
        }

        static string TransferWindows()
        {
            var home = System4[0]; var o = Ochre;
            double tt = TransferPlanner.TransferTime(home, o), want = TransferPlanner.RequiredPhase(home, o);
            double win = TransferPlanner.NextWindow(home, o, 0);
            double at = TransferPlanner.Phase(home, o, win);
            double err = Math.Abs(at - want) * 180 / Math.PI;
            Check(err < 0.5, $"at the window the phase is {at * 180 / Math.PI:0.0} deg, wanted {want * 180 / Math.PI:0.0}");
            double synodic = 2 * Math.PI / Math.Abs(o.OrbitalAngularVelocity - home.OrbitalAngularVelocity);
            Check(win >= 0 && win <= synodic, $"window in {win / 86400:0} d, but they line up every {synodic / 86400:0} d");
            double win2 = TransferPlanner.NextWindow(home, o, win + 86400);
            Check(Math.Abs(win2 - win - synodic) < 86400 * 2, $"the next window after that is {(win2 - win) / 86400:0} d later (synodic {synodic / 86400:0})");
            // A transfer leaving now would arrive where Ochre will be
            double dvEsc = TransferPlanner.EjectionDv(home, o, home.radius + 100_000);
            Check(dvEsc > 900 && dvEsc < 2000, $"escape burn {dvEsc:0} m/s from 100 km");
            return $"leave with Ochre {want * 180 / Math.PI:0} deg ahead; first window day {win / 86400:0}, then every {synodic / 86400:0} days; {tt / 86400:0}-day trip; {dvEsc:0} m/s from low orbit";
        }

        static string TransferToOchre()
        {
            var home = System4[0]; var o = Ochre;
            double r = home.radius + 100_000;
            var v = InOrbit(D("probe", "probe.spark"), home, r, 0.4);
            v.sas = SasMode.Off;
            double ut = 0;
            var t0 = DateTime.Now;
            Check(TransferPlanner.Plan(home, v.pos, v.vel, ut, o, out double burnUt, out double dv, out double normal, out double closest, out double window), "no plan");
            double secs = (DateTime.Now - t0).TotalSeconds, planPe;
            string scanText = "";
            Check(closest < o.SoiRadius * 10, $"the plan only gets within {closest / 1e6:0.0} Mm (Ochre's SOI is {o.SoiRadius / 1e6:0} Mm)");
            // What the plan itself expects at arrival
            v.RailsAdvance(0, ut, out _);
            {
                var park = Orbit.FromState(v.pos, v.vel, home.mu, ut);
                park.StateAt(burnUt, out var pr, out var pv);
                var segs = Patcher.Predict(home, pr, pv + pv.normalized * dv + Vector3d.Cross(pr, pv).normalized * normal, burnUt, 3);
                var arrSeg = segs.Find(s => s.body == o);
                planPe = arrSeg != null ? arrSeg.orbit.Periapsis - o.radius : double.NaN;
                // how arrival responds to the burn size and the sideways part (debugging the search)
                var scan = new List<string>();
                foreach (double dd in new[] { -2, -0.5, -0.1, 0.1, 0.5, 2 })
                    foreach (double nn in new[] { 0.0 })
                    {
                        var ss = Patcher.Predict(home, pr, pv + pv.normalized * (dv + dd) + Vector3d.Cross(pr, pv).normalized * (normal + nn), burnUt, 3);
                        var a = ss.Find(s => s.body == o);
                        scan.Add($"dv{dd:+0.0;-0.0}: {(a != null ? ((a.orbit.Periapsis - o.radius) / 1000).ToString("0") : "miss")}");
                    }
                foreach (double nn in new[] { -5.0, -1, 1, 5 })
                {
                    var ss = Patcher.Predict(home, pr, pv + pv.normalized * dv + Vector3d.Cross(pr, pv).normalized * (normal + nn), burnUt, 3);
                    var a = ss.Find(s => s.body == o);
                    scan.Add($"n{nn:+0;-0}: {(a != null ? ((a.orbit.Periapsis - o.radius) / 1000).ToString("0") : "miss")}");
                }
                scanText = string.Join(", ", scan);
            }
            // Fly it: coast to the burn, kick, then rails through every sphere of influence
            v.RailsAdvance(burnUt - ut, ut, out double used);
            ut += used;
            Vector3d nrm = Vector3d.Cross(v.pos, v.vel).normalized;
            v.vel = v.vel + v.vel.normalized * dv + nrm * normal;
            v.rails = null;
            double start = ut;
            string fix = "no correction";
            bool corrected = false;
            for (int i = 0; i < 4000 && v.body != o && !v.destroyed; i++)
            {
                v.RailsAdvance(6 * 3600, ut, out used);
                ut += used;
                v.CheckSoi(ut);
                if (!corrected && v.body.isStar)
                {
                    // Out of Arden's pull: plan the mid-course correction, coast to it and make it
                    corrected = true;
                    Check(TransferPlanner.PlanCorrection(v.body, v.pos, v.vel, ut, o, out double cUt, out double cp, out double cn, out double cr, out double cAlt), "no correction plan");
                    v.RailsAdvance(cUt - ut, ut, out used);
                    ut += used;
                    Vector3d pro = v.vel.normalized, nr = Vector3d.Cross(v.pos, v.vel).normalized, ra = Vector3d.Cross(nr, pro);
                    v.vel = v.vel + pro * cp + nr * cn + ra * cr;
                    v.rails = null;
                    fix = $"correction {Math.Sqrt(cp * cp + cn * cn + cr * cr):0.0} m/s on day {(cUt - start) / 86400:0} (plan: arrive {cAlt / 1000:0} km up)";
                }
                if (ut - start > 400 * 86400) break;
            }
            Check(v.body == o, $"never reached Ochre: around {v.body.name} after {(ut - start) / 86400:0} d");
            var arr = Orbit.FromState(v.pos, v.vel, o.mu, ut);
            double peAlt = arr.Periapsis - o.radius;
            Check(peAlt > o.atmosphereHeight * 0.5 && peAlt < 400_000, $"arrived with periapsis {peAlt / 1000:0} km, the plan said {planPe / 1000:0} km (aimed for about {(o.atmosphereHeight + 30_000) / 1000:0} km); {scanText}");
            return $"planned in {secs:0.0}s: {dv:0} m/s (+{normal:0} sideways) on day {burnUt / 86400:0.0} (window day {window / 86400:0.0}); {fix}; reached Ochre after {(ut - start) / 86400:0} d, periapsis {(arr.Periapsis - o.radius) / 1000:0} km";
        }

        static string OchreParachute()
        {
            var o = Ochre;
            var dir = Directions(1, 517).First();
            double ground = Terrain.Height(o, dir), alt = ground + 6000;
            var v = VesselSim.CreateFree(D("cap", "chute.s", "pod.wren"), o, dir * (o.radius + alt), o.SurfaceVelocity(dir * (o.radius + alt)) - dir * 60, Quaternion.LookRotation(Vector3.forward, dir.ToFloat()));
            v.ActivateNextStage(); // open the chute
            double t = 0, maxV = 0;
            for (int i = 0; i < 60_000 && !v.landed && !v.destroyed; i++)
            {
                v.Step(0.02, t); t += 0.02;
                if (v.RadarAltitude(t) < 50) maxV = Math.Max(maxV, v.SurfaceRelVelocity.magnitude);
            }
            Check(v.landed && !v.destroyed, $"didn't land under the chute: {v.destroyReason}");
            Check(maxV > 4 && maxV < 13, $"came down at {maxV:0.0} m/s");
            return $"down from 6 km in {t:0} s, touching down at {maxV:0.0} m/s";
        }

        static string OchreCareer()
        {
            var u = CareerUniverse();
            var c = u.career;
            foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id);
            List<Contract> Offers() => (List<Contract>)typeof(Career).GetMethod("Candidates", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { 0.0 });
            Check(!Offers().Any(k => k.body == "Ochre"), "Ochre offered before leaving Arden");
            c.records |= Records.Escape;
            Check(Offers().Any(k => k.type == "flyby" && k.body == "Ochre"), "no trip to Ochre after escaping");
            double f0 = c.funds;
            c.OnFlags(VFlags.SoiOchre | VFlags.OrbitOchre | VFlags.LandedOchre | VFlags.LandedMote);
            Check((c.records & Records.OchreLanding) != 0 && (c.records & Records.MoteLanding) != 0 && c.funds > f0 + 500_000, $"Ochre records paid {c.funds - f0:N0}");
            var later = Offers();
            foreach (var (type, body) in new[] { ("orbit", "Ochre"), ("land", "Ochre"), ("land", "Mote"), ("return", "Ochre") })
                Check(later.Any(k => k.type == type && k.body == body), $"no {type} {body} contract");
            var ret = new Contract { id = 980, type = "return", body = "Ochre", situation = "land", state = ContractState.Active, deadline = 1e12, reward = 1000 };
            c.contracts.Add(ret);
            var cap = u.Add(InOrbit(D("cap", "chute.s", "pod.wren"), System4[0], System4[0].radius + 100_000, 0));
            cap.careerFlags = VFlags.Launched | VFlags.SoiSun | VFlags.SoiOchre | VFlags.LandedOchre;
            c.Recover(cap, 0, System4);
            Check(ret.state == ContractState.Completed && (c.records & Records.OchreReturn) != 0, "coming home from Ochre didn't complete its return");
            Check(Crew.XpOf(VFlags.LandedOchre) == 9, "no crew experience for Ochre");
            var dish = PartCatalog.Get("dish.d");
            double range = Math.Sqrt(dish.commPower * CommNet.GroundPower);
            double farthest = Ochre.orbitRadius + System4[0].orbitRadius;
            Check(TechTree.NodeOf("dish.d") != null && range > farthest, $"the dish reaches {range / 1e9:0} Gm; Ochre can be {farthest / 1e9:0} Gm away");
            return $"Ochre records paid {c.funds - f0:N0}+; dish range {range / 1e9:0} Gm (Ochre at most {farthest / 1e9:0} Gm away)";
        }
    }
}
