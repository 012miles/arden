using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 13 tests: the first-flight guide, and heat shields that hold a capsule shield-first.</summary>
    public static partial class SimTests
    {
        static void RunMilestone13()
        {
            Run("Guide: each step is ticked off by what the player does; a crash, a landing or a new rocket on the pad move it on", GuideSteps);
            Run("Guide: a new game starts it, it saves with the career, and older saves don't get it", GuideSave);
            Run("Guide: the starter rocket flown the way the guide says (engine, SAS, then SPACE to drop the tank and arm the parachute) gets home", GuideStarterFlight);
            Run("Guide: the autopilot's flight to orbit walks the guide through to \"in orbit\"", GuideOrbitFlight);
            Run("Heat shield: a capsule falling backwards turns round and flies shield first", ShieldTurnsAround);
        }

        static GuideState G(double alt, double maxAlt = -1, bool pad = false, bool landed = false, bool dead = false, double pe = -6e5, double dv = 4000) =>
            new GuideState { inFlight = true, onPad = pad, landed = landed, destroyed = dead, alt = alt, maxAlt = maxAlt < 0 ? alt : maxAlt, peAlt = pe, airTop = 70_000, orbitDv = dv };

        static string GuideSteps()
        {
            int s = Guide.Launch;
            Check(Guide.Advance(s, new GuideState()) == Guide.Launch, "moved on while still in the builder");
            s = Guide.Advance(s, G(0, pad: true, landed: true));
            Check(s == Guide.Ignite, $"on the pad: step {s}");
            s = Guide.Advance(s, G(5, pad: true, landed: true));
            Check(s == Guide.Ignite, "moved on before the engine was lit");
            s = Guide.Advance(s, G(120));
            Check(s == Guide.Climb, $"lifted off: step {s}");
            s = Guide.Advance(s, G(9_000));
            Check(s == Guide.Space, $"9 km: step {s}");
            s = Guide.Advance(s, G(71_000));
            Check(s == Guide.Orbit, $"above the air: step {s}");
            Check(Guide.Advance(s, G(90_000, pe: 40_000)) == Guide.Orbit, "periapsis in the air counted as orbit");
            s = Guide.Advance(s, G(90_000, pe: 75_000));
            Check(s == Guide.Orbiting, $"periapsis above the air: step {s}");
            Check(Guide.Advance(s, G(90_000, pe: 75_000)) == Guide.Orbiting, "the finished note should wait for Got it");

            // Down again without orbit, a crash, and a fresh rocket after the crash
            int h = Guide.Advance(Guide.Space, G(0, maxAlt: 40_000, landed: true));
            Check(h == Guide.Home, $"landed after a 40 km hop: step {h}");
            Check(Guide.Advance(Guide.Ignite, G(0, maxAlt: 0, landed: true)) == Guide.Ignite, "sitting on the pad counted as a landing");
            int lost = Guide.Advance(Guide.Climb, G(3_000, dead: true));
            Check(lost == Guide.Lost, $"crash: step {lost}");
            int again = Guide.Advance(lost, G(0, pad: true, landed: true));
            Check(again == Guide.Ignite, $"new rocket on the pad after a crash: step {again}");
            Check(Guide.Advance(Guide.Closed, G(100_000, pe: 90_000)) == Guide.Closed, "a closed guide came back");

            // The advice matches the rocket: no orbit talk for a rocket that can't get there
            string weak = Guide.Text(Guide.Space, G(20_000, dv: 1800), true), strong = Guide.Text(Guide.Space, G(20_000, dv: 4200), true);
            Check(weak.Contains("can't reach orbit") && weak.Contains("parachute"), "weak rocket advice: " + weak);
            Check(strong.Contains("east"), "orbital rocket advice: " + strong);
            Check(Guide.Text(Guide.Launch, default, true).Contains("Missions") && !Guide.Text(Guide.Launch, default, false).Contains("Missions"), "launch advice should mention Missions in career only");
            for (int i = Guide.Launch; i <= Guide.Lost; i++) Check(Guide.Title(i).Length > 0 && Guide.Text(i, G(1000), true).Length > 0, $"step {i} has no text");
            return "builder -> pad -> lift-off -> 8 km -> space -> orbit; landing, crash and retry branches; advice fits the rocket";
        }

        static string GuideSave()
        {
            var c = new Career();
            Check(c.guide == Guide.Closed, "a blank career shows the guide");
            c.Start(GameMode.Career);
            Check(c.guide == Guide.Launch, $"new career: guide step {c.guide}");
            c.guide = Guide.Space;
            var json = JsonUtility.ToJson(c.ToSave());
            var back = new Career();
            back.Load(JsonUtility.FromJson<CareerSave>(json));
            Check(back.guide == Guide.Space, $"loaded guide step {back.guide}");
            // An older save has no guide field at all
            string old = json.Replace($"\"guide\":{Guide.Space}", "").Replace(",}", "}").Replace(",,", ",");
            Check(!old.Contains("\"guide\""), "couldn't strip the field for the old-save case");
            var older = new Career();
            older.Load(JsonUtility.FromJson<CareerSave>(old));
            Check(older.guide == Guide.Closed, $"an older save got the guide (step {older.guide})");
            var sb = new Career();
            sb.Start(GameMode.Sandbox);
            Check(sb.guide == Guide.Launch, "new sandbox game should get the guide too");
            return "new career and sandbox start it; saved step loads back; older saves skip it";
        }

        static GuideState GuideFrom(VesselSim v, double ut, double maxAlt)
        {
            var o = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
            double dv = 0; foreach (var r in v.StageReports()) dv += r.dvVac;
            return new GuideState
            {
                inFlight = true, onPad = !v.hasLaunched, landed = v.landed, destroyed = v.destroyed, alt = v.Altitude, maxAlt = maxAlt,
                peAlt = o.Periapsis - v.body.radius, airTop = v.body.atmosphereHeight, orbitDv = dv,
            };
        }

        static string GuideStarterFlight()
        {
            var b = Body;
            var v = VesselSim.CreateOnPad(VesselDesign.FirstFlight(), b, 0);
            double ut = 0, maxAlt = 0, dv0 = 0;
            foreach (var r in v.StageReports()) dv0 += r.dvVac;
            Check(dv0 < Guide.OrbitDv, $"the starter rocket has {dv0:0} m/s, enough for orbit: the guide would send it the wrong way");
            int step = Guide.Advance(Guide.Launch, GuideFrom(v, ut, 0));
            var seen = new List<int> { step };
            v.throttle = 1;
            v.sas = SasMode.Stability;
            v.ActivateNextStage();                      // SPACE: light the engine
            bool dropped = false, chute = false;
            double chuteSpeed = 0;
            for (int i = 0; i < 400_000 && !v.destroyed && !(v.landed && maxAlt > 300); i++)
            {
                v.Step(0.02, ut); ut += 0.02;
                if (!v.landed) maxAlt = Math.Max(maxAlt, v.Altitude);
                double vs = Vector3d.Dot(v.vel, v.pos.normalized), speed = v.SurfaceRelVelocity.magnitude;
                if (!dropped && v.PotentialThrust() <= 0) { v.ActivateNextStage(); dropped = true; }   // SPACE: drop the empty tank
                else if (dropped && !chute) { v.ActivateNextStage(); chute = true; }                  // SPACE: arm the parachute
                if (chute && chuteSpeed == 0 && v.parts.Any(p => p.def.kind == PartKind.Parachute && p.chuteOpen > 0.05f)) chuteSpeed = speed;
                if (i % 25 == 0)
                {
                    int n = Guide.Advance(step, GuideFrom(v, ut, maxAlt));
                    if (n != step) { step = n; seen.Add(n); }
                }
            }
            step = Guide.Advance(step, GuideFrom(v, ut, maxAlt));
            if (seen.Last() != step) seen.Add(step);
            string stages = string.Join(" | ", VesselSim.CreateOnPad(VesselDesign.FirstFlight(), b, 0).stages.Select(st => $"d{st.decouple.Count} i{st.ignite.Count} c{st.chutes.Count}"));
            Check(!v.destroyed, $"destroyed: {v.destroyReason} (chute {(chuteSpeed > 0 ? $"opened at {chuteSpeed:0} m/s" : chute ? "armed, never opened" : "never armed")}; stages {stages}; max {maxAlt / 1000:0.0} km)");
            Check(v.landed, "never came down");
            Check(step == Guide.Home, $"guide ended at step {step} ({string.Join(" > ", seen.Select(Guide.Title))})");
            Check(seen.Contains(Guide.Climb), "the guide skipped the climb");
            return $"{dv0:0} m/s rocket, up to {maxAlt / 1000:0.0} km, chute opened at {chuteSpeed:0} m/s, landed at {v.SurfaceRelVelocity.magnitude:0.0} m/s; guide: {string.Join(" > ", seen.Select(Guide.Title))}";
        }

        static string GuideOrbitFlight()
        {
            var b = Body;
            var v = VesselSim.CreateOnPad(VesselDesign.Default(), b, 0);
            var ap = new FlightAutopilot();
            double ut = 0, maxAlt = 0;
            int step = Guide.Advance(Guide.Launch, GuideFrom(v, ut, 0));
            var seen = new List<int> { step };
            for (int i = 0; i < 200_000 && !v.destroyed && ap.phase != FlightAutopilot.Phase.Done; i++)
            {
                if (ap.Update(v, ut)) v.ActivateNextStage();
                v.Step(0.02, ut); ut += 0.02;
                if (!v.landed) maxAlt = Math.Max(maxAlt, v.Altitude);
                if (i % 25 == 0)
                {
                    int n = Guide.Advance(step, GuideFrom(v, ut, maxAlt));
                    if (n != step) { step = n; seen.Add(n); }
                }
            }
            int last = Guide.Advance(step, GuideFrom(v, ut, maxAlt));
            if (last != step) seen.Add(last);
            Check(!v.destroyed, "destroyed: " + v.destroyReason);
            Check(last == Guide.Orbiting, $"guide ended at step {last} ({string.Join(" > ", seen.Select(Guide.Title))})");
            var expect = new[] { Guide.Ignite, Guide.Climb, Guide.Space, Guide.Orbit, Guide.Orbiting };
            Check(seen.SequenceEqual(expect), $"steps went {string.Join(" > ", seen)}");
            return string.Join(" > ", seen.Select(Guide.Title));
        }

        static string ShieldTurnsAround()
        {
            // A capsule with a heat shield, dropped into the air at 2.2 km/s nose first (shield trailing). It should swing round.
            var home = System4[0];
            var d = D("cap", "chute.s", "pod.wren", "shield.s");
            double alt = 45_000;
            Vector3d up = new Vector3d(1, 0, 0), east = new Vector3d(0, 0, -1);
            Vector3d pos = up * (home.radius + alt), vel = home.SurfaceVelocity(pos) + east * 2200 - up * 150;
            Vector3d airV = vel - home.SurfaceVelocity(pos);
            // Part axis points from the shield (bottom) to the chute (top): flying nose first means the axis points along the flow.
            var rot = Quaternion.FromToRotation(Vector3.up, airV.normalized.ToFloat());
            var v = VesselSim.CreateFree(d, home, pos, vel, rot);
            v.sas = SasMode.Off;
            v.throttle = 0;
            double ut = 0, worstLate = 0;
            for (int i = 0; i < 6000 && !v.destroyed && !v.landed; i++)
            {
                v.Step(0.02, ut); ut += 0.02;
                if (ut > 60)
                {
                    Vector3d flow = v.SurfaceRelVelocity;
                    if (flow.magnitude > 50 && v.dynPressure > 500)
                    {
                        double ang = Vector3.Angle(v.Axis.ToFloat(), (-flow).normalized.ToFloat());
                        worstLate = Math.Max(worstLate, ang);
                    }
                }
            }
            Check(!v.destroyed, "capsule destroyed: " + v.destroyReason);
            Check(worstLate < 25, $"after a minute the shield still isn't leading: off by up to {worstLate:0} deg");
            return $"started backwards; after 60 s the shield leads within {worstLate:0} deg";
        }
    }
}
