using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 7a (career): the builder's crew picker with a scientist in the only seat (no-pilot warning); the
        /// astronaut roster with roles, levels and hiring by role; a rookie pilot who can't hold maneuver; the science panel
        /// with a scientist's transmit bonus and a used sample canister they can clean.
        /// </summary>
        IEnumerator CrewTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var c = u.career;
            var home = root.home;
            c.funds = 400_000;
            c.science = 500;
            foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id);
            var pilot = c.roster[0];
            var eng = c.roster[1];
            var sci = c.roster[2];
            var rookie = c.roster[3];
            pilot.log = VFlags.Space | VFlags.OrbitHome | VFlags.SoiTal | VFlags.OrbitTal;
            eng.log = VFlags.Space | VFlags.OrbitHome;
            sci.log = VFlags.Space | VFlags.OrbitHome;
            pilot.flights = 4; eng.flights = 2; sci.flights = 2;

            c.crewPlan.Clear();
            c.crewPlan.Add(sci.name);
            root.currentDesign = VesselDesign.FirstFlight();
            root.EnterHangar();
            root.hangar.ShowCrew(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("80_builder_crew");
            root.hangar.ShowCrew(false);
            c.crewPlan.Clear();
            c.Hire(CrewRole.Engineer);
            root.hangar.ShowCenter(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("81_roster");
            root.hangar.ShowCenter(false);

            // A rookie pilot and a level-2 scientist in orbit, with two sample canisters
            var d = VesselDesign.Stack("Ibis", "chute.s", "pod.wren", "hab.s", "tank.s2", "eng.ember");
            int hab = d.nodes.FindIndex(n => n.id == "hab.s");
            DesignOps.AttachSurfaceAt(d, hab, 0.5f, 0f, "sci.bio", 2, false);
            DesignOps.AttachSurfaceAt(d, hab, 0.3f, 90f, "antenna.s", 1, false);
            double r = home.radius + 120_000, sp = Math.Sqrt(home.mu / r);
            Vector3d dir = CelestialBody.DirectionFromLatLon(0, 20), east = CelestialBody.EastAt(dir);
            var v = VesselSim.CreateFree(d, home, dir * r, east * sp, Quaternion.LookRotation(east.ToFloat(), dir.ToFloat()));
            foreach (var p in v.parts) { p.crew.Clear(); if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1; } }
            v.parts.First(p => p.def.id == "pod.wren").crew.Add(rookie.name);
            v.parts.First(p => p.def.id == "hab.s").crew.Add(sci.name);
            v.throttle = 0;
            v.hasLaunched = true;
            u.Add(v);
            rookie.state = sci.state = 1;
            rookie.vesselId = sci.vesselId = v.id;
            v.RecomputeMass();
            root.Fly(v);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            f.RefreshTrajectory();
            f.AddNode(f.ut + 600, 50);
            f.SetSas(SasMode.Maneuver);
            f.SetCamera(200f, 12f, 16f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("82_rookie_sas");
            Log($"SAS tier {v.SasTier} from {v.SasSource}; maneuver allowed={v.CanHold(SasMode.Maneuver)}; sas now {v.sas}");

            var cans = v.parts.Where(p => p.def.id == "sci.bio").ToList();
            var exp = Science.Get("samples");
            Lab.Run(c, v, cans[0], exp, f.ut);
            Lab.Discard(cans[0], Lab.Held(cans[0], exp));
            Lab.Run(c, v, cans[1], exp, f.ut);
            f.ShowSciencePanel(true);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("83_scientist_science");
            Log($"canister 1 used={cans[0].used}; canister 2 sends at {Lab.TransmitFactor(v, Lab.Held(cans[1], exp)) * 100:0}% with {sci.name} (level {sci.Level} scientist)");
        }
    }
}
