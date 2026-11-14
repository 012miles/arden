using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 17: "Plan intercept" sets both burns; the flight computer (Auto-burn) warps to each, points and flies
        /// it; the tug ends up alongside the station.
        /// </summary>
        IEnumerator ComputerTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            u.ut = TimeWithSun(home, CelestialBody.DirectionFromLatLon(0, 30), 0.6, 0);
            double r1 = home.radius + 600_000, v1 = Math.Sqrt(home.mu / r1);
            // The tug with the Wrangler's antenna and solar panels, so the probe keeps its signal and power
            var td = VesselDesign.WranglerTug();
            int bat = td.nodes.FindIndex(x => x.id == "battery.s");
            DesignOps.AttachSurfaceAt(td, bat, 0.5f, 0f, "solar.s", 2, false);
            DesignOps.AttachSurfaceAt(td, bat, 0.5f, 90f, "antenna.s", 1, false);
            var tug = Fly(td, home, 300_000, 0, 30, u.ut, true);
            UnfoldAll(tug);
            var f = root.flight;
            Vector3d up = tug.pos.normalized, along = tug.vel.normalized, n = Vector3d.Cross(up, along).normalized;
            var tilt = Quaternion.AngleAxis(3f, up.ToFloat());
            Vector3d sp = Vector3d.Rotate(Quaternion.AngleAxis(120f, n.ToFloat()), up) * r1;
            Vector3d sv = Vector3d.Cross(n, sp.normalized).normalized * v1;
            sp = Vector3d.Rotate(tilt, sp); sv = Vector3d.Rotate(tilt, sv);
            var station = VesselSim.CreateFree(VesselDesign.Outpost("Outpost"), home, sp, sv, Quaternion.identity);
            station.throttle = 0; station.hasLaunched = true;
            u.Add(station);
            yield return new WaitForSecondsRealtime(0.8f);
            f.Stage();   // light the Kestrel
            f.active.throttle = 0;
            foreach (var p in f.active.parts) p.charge = p.def.ecCapacity;
            f.SetTarget(station);
            bool planned = f.PlanIntercept();
            yield return new WaitForSecondsRealtime(1.0f);
            Log($"planned: {planned}, burns {(f.node != null ? 1 : 0) + f.laterNodes.Count}: {f.node?.DeltaV:0} m/s, then {(f.laterNodes.Count > 0 ? f.laterNodes[0].DeltaV : 0):0} m/s");
            f.mapView = true;
            f.SetMapView(home, 5e6f, 20f, 65f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("240_two_burns_planned");
            f.mapView = false;
            f.ShowEvents(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("241_events_two_burns");
            f.ShowEvents(false);

            f.ToggleAutoBurn();
            Log($"auto-burn: {f.autoBurn}");
            // Wait for the first burn to start, and catch it
            yield return WaitFor(() => f.active.throttle > 0.5 || !f.autoBurn, 90, "the first burn");
            yield return new WaitForSecondsRealtime(1.5f);
            f.SetCamera(200f, 6f, 30f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("242_autoburn_firing");
            Log($"burning: throttle {f.active.throttle:0.00}, SAS {f.active.sas}");
            // Then the coast and the match
            yield return WaitFor(() => !f.autoBurn, 240, "both burns");
            yield return new WaitForSecondsRealtime(1.0f);
            double dist = Vector3d.Distance(f.active.pos, station.pos), rel = (f.active.vel - station.vel).magnitude;
            Log($"after the burns: {dist:0} m from the station at {rel:0.00} m/s; nodes left {(f.node != null ? 1 : 0) + f.laterNodes.Count}");
            if (f.targetVessel != station) f.SetTarget(station);   // (SetTarget on the current target clears it)
            f.LookAlong(station.pos - f.active.pos, 40f, 0f, 8f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("243_alongside");
        }
    }
}
