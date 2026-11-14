using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 7b: a lander on Tal with two legs broken by a hard landing and its parachute used; the engineer climbs
        /// out, walks, plants a flag, fixes the legs, repacks the chute and takes a surface sample, then climbs back in;
        /// and a pilot flying the jetpack beside a capsule in orbit.
        /// </summary>
        IEnumerator EvaTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var c = u.career;
            var tal = root.moon;
            CrewMember Hire(string name, CrewRole role) { var m = new CrewMember { name = name, role = role, log = Crew.XpMask, state = 1 }; c.roster.Add(m); return m; }
            var pilot = Hire("Juno Adair", CrewRole.Pilot);
            var eng = Hire("Tobin Reyes", CrewRole.Engineer);
            var sci = Hire("Mira Okafor", CrewRole.Scientist);

            var d = VesselDesign.Stack("Ibis lander", "chute.s", "pod.wren", "hab.s", "tank.s2", "eng.wisp");
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s2"), 0.95f, 45f, "leg.s", 4, false);
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "hab.s"), 0.4f, 0f, "solar.s", 2, false);
            // A sunlit spot with the sun at a slant
            Vector3d spot = Vector3d.up;
            double best = 9;
            for (int la = -60; la <= 60; la += 3)
                for (int lo = -180; lo < 180; lo += 3)
                {
                    var dir = CelestialBody.DirectionFromLatLon(la, lo);
                    double s = Math.Abs(Vector3d.Dot(dir, Sun.DirectionAt(GameRoot.I.universe.ut)) - 0.7);
                    if (s < best) { best = s; spot = dir; }
                }
            var lander = VesselSim.CreateLanded(d, tal, spot, u.ut);
            foreach (var p in lander.parts) { p.crew.Clear(); if (p.def.kind == PartKind.Solar) { p.legsDeployed = true; p.legDeploy = 1; } }
            lander.parts.First(p => p.def.id == "pod.wren").crew.Add(pilot.name);
            var hab = lander.parts.First(p => p.def.id == "hab.s");
            hab.crew.Add(eng.name);
            hab.crew.Add(sci.name);
            int broke = 0;
            foreach (var p in lander.parts) if (p.def.kind == PartKind.Leg && broke < 2) { p.broken = true; p.legsDeployed = false; p.legDeploy = 0; broke++; }
            lander.parts.First(p => p.def.kind == PartKind.Parachute).chuteDeployed = true;
            lander.nextStage = lander.stages.Count;
            lander.throttle = 0;
            u.Add(lander);
            pilot.vesselId = eng.vesselId = sci.vesselId = lander.id;
            lander.RecomputeMass();
            root.Fly(lander);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            f.SetCamera(160f, 12f, 22f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("90_lander_crew");

            var eva = f.GoEva(hab);
            yield return new WaitForSecondsRealtime(0.6f);
            Log($"EVA: {eva?.name} landed={eva?.landed}; lander crew {lander.CrewCount}");
            if (eva == null) yield break;
            // Walk out a few steps, plant a flag, turn round and walk back to the lander
            f.walkOverride = new Vector2(0, 1);
            yield return new WaitForSecondsRealtime(1.5f);
            f.walkOverride = null;
            var flag = f.PlantFlag();
            yield return new WaitForSecondsRealtime(0.3f);
            f.LookAlong(lander.pos - eva.pos, 7f, -35f, 10f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("91_flag");
            f.walkOverride = new Vector2(1, 0);
            yield return new WaitForSecondsRealtime(1.8f);
            f.walkOverride = new Vector2(0, 1);
            yield return new WaitForSecondsRealtime(1.3f);
            f.walkOverride = null;
            f.LookAlong(lander.pos - eva.pos, 7f, 25f, 12f);
            yield return new WaitForSecondsRealtime(0.5f);
            var jobs = eva.WorkInReach(u.vessels);
            Log($"walked back; jobs in reach: {string.Join(", ", jobs.Select(j => j.p.def.name))}; can board: {eva.BoardingPart(lander) != null}");
            yield return Shot("92_eva_jobs");

            foreach (var (vv, p) in eva.WorkInReach(u.vessels).ToList()) { if (p.broken) f.RepairPart(vv, p); else f.RepackChute(vv, p); }
            Lab.Run(c, eva, eva.parts[0], Science.Get("surface"), f.ut);
            f.ShowSciencePanel(true);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("93_repairs_science");
            f.ShowSciencePanel(false);
            Log($"after repairs: broken {lander.parts.Count(p => p.broken)}, chute packed={!lander.parts.First(p => p.def.kind == PartKind.Parachute).chuteDeployed}, suit holds {eva.parts[0].science.Count} result(s)");
            bool boarded = f.BoardNearest();
            yield return new WaitForSecondsRealtime(0.5f);
            Log($"boarded={boarded}; lander crew {lander.CrewCount}, science aboard {lander.parts.Sum(p => p.science.Count)}; flag '{flag?.name}'");

            // A pilot on a jetpack beside a capsule in orbit of Arden
            var home = root.home;
            var cd = VesselDesign.Stack("Wren orbiter", "chute.s", "pod.wren", "tank.s2", "eng.ember");
            double r = home.radius + 140_000, sp = Math.Sqrt(home.mu / r);
            Vector3d odir = CelestialBody.DirectionFromLatLon(0, 30), east = CelestialBody.EastAt(odir);
            var cap = VesselSim.CreateFree(cd, home, odir * r, east * sp, Quaternion.LookRotation(east.ToFloat(), odir.ToFloat()));
            var pilot2 = Hire("Ines Varga", CrewRole.Pilot);
            foreach (var p in cap.parts) p.crew.Clear();
            cap.parts.First(p => p.def.id == "pod.wren").crew.Add(pilot2.name);
            cap.throttle = 0;
            cap.hasLaunched = true;
            u.Add(cap);
            pilot2.vesselId = cap.id;
            cap.RecomputeMass();
            f.SwitchTo(cap);
            yield return new WaitForSecondsRealtime(0.5f);
            var spacewalker = f.GoEva(cap.parts.First(p => p.def.id == "pod.wren"));
            f.translateOverride = new Vector3(0.6f, 0.3f, 0f);
            yield return new WaitForSecondsRealtime(1.6f);
            f.translateOverride = Vector3.zero;
            yield return new WaitForSecondsRealtime(0.3f);
            f.translateOverride = null;
            f.LookAlong(cap.pos - spacewalker.pos, 9f, 30f, -10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("94_jetpack");
            Log($"spacewalk: {Vector3d.Distance(spacewalker.pos, cap.pos):0.0} m from the capsule at {(spacewalker.vel - cap.vel).magnitude:0.00} m/s, jetpack {spacewalker.MonoLeft * 1000:0.0} kg left");
        }
    }
}
