using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 22: a base on Tal. A hub with solar power and an antenna, a research lab, a storage depot and a lander
        /// stand on the ground; an astronaut takes a cable from the hub; the modules are cabled together; the Base panel;
        /// the lander topped up from the depot; the base parts in the builder.
        /// </summary>
        IEnumerator BasesTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            u.career.Start(GameMode.Career);
            u.career.guide = Guide.Closed;
            var tal = root.moon;
            Vector3d sun = Sun.DirectionAt(u.ut);
            Vector3d centre = (tal.InertialToFixed(sun, u.ut) + new Vector3d(0.25, 0.3, 0.1)).normalized;
            Vector3d east = Vector3d.Cross(new Vector3d(0, 1, 0), centre).normalized, north = Vector3d.Cross(centre, east).normalized;
            VesselSim Place(VesselDesign d, double e, double n)
            {
                var dir = (centre * tal.radius + east * e + north * n).normalized;
                var v = VesselSim.CreateLanded(d, tal, dir, u.ut);
                v.throttle = 0; v.hasLaunched = true;
                u.Add(v);
                return v;
            }
            var hd = VesselDesign.Stack("Tal Base", "base.hub");
            DesignOps.AttachSurfaceAt(hd, 0, 0.55f, 90f, "solar.l", 2, false);
            DesignOps.AttachSurfaceAt(hd, 0, 0.3f, 0f, "antenna.s", 1, false);
            var hub = Place(hd, 0, 0);
            var lab = Place(VesselDesign.Stack("Lab One", "base.lab"), 24, 4);
            var depot = Place(VesselDesign.Stack("Depot", "base.tank"), -22, 6);
            var ld = VesselDesign.Stack("Lander", "chute.s", "pod.wren", "tank.s2", "eng.wisp");
            DesignOps.AttachSurfaceAt(ld, ld.nodes.FindIndex(n => n.id == "tank.s2"), 0.95f, 45f, "leg.s", 4, false);
            var ship = Place(ld, 6, -26);
            foreach (var p in ship.parts) if (p.def.kind == PartKind.Tank) p.propellant *= 0.2;
            foreach (var p in depot.parts) p.propellant = p.def.propellant;
            root.Fly(hub);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            f.SetCamera(150f, 38f, 75f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("310_base_modules");

            // An astronaut takes a cable from the hub and walks off with it
            var walker = f.GoEva(hub.parts.First(p => p.crew.Count > 0));
            yield return new WaitForSecondsRealtime(1.2f);
            if (walker != null)
            {
                f.TakeCableFrom(hub);
                f.walkOverride = new Vector2(0, 1);
                yield return new WaitForSecondsRealtime(4f);
                f.walkOverride = null;
                f.SetCamera(200f, 18f, 9f);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("311_carrying_cable");
                Log($"cable: carrying {f.CarryingCable}, {Bases.Distance(walker, hub):0.0} m from the hub");
            }
            else Log("no walker: GoEva failed");

            // Cable the base together (as the astronaut would) and look at it from the hub
            double shipBefore = ship.parts.Sum(p => p.propellant);
            foreach (var m in new[] { lab, depot, ship })
            {
                string why = Bases.Connect(u, hub, m);
                Log($"connect {m.name}: {why ?? "ok"} ({Bases.Distance(hub, m):0} m)");
            }
            var hubNow = u.Find(hub.id);
            root.Fly(hubNow);
            yield return new WaitForSecondsRealtime(1.5f);
            var s = Bases.Summarize(Bases.GroupOf(u, hubNow));
            Log($"base: {s.modules} modules, crew {s.crew}/{s.seats}, power {s.ec:0}/{s.ecCap:0}, fuel {s.fuel:0.0}/{s.fuelCap:0.0} t, lab {s.labPerDay:0.0}/day; lander fuel {shipBefore:0.00} -> {ship.parts.Sum(p => p.propellant):0.00} t");
            f.SetCamera(120f, 30f, 55f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("312_base_panel");

            // The base parts in the builder
            var bd = VesselDesign.Stack("Outpost Core", "base.hub", "dec.l", "tank.l4", "eng.kestrel");
            root.currentDesign = bd;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("313_builder_hub");
            Log("bases tour done");
        }
    }
}
