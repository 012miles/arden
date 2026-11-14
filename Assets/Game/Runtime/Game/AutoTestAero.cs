using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        static VesselSim Relay(string name, CelestialBody b, double alt, double lonDeg, double ut)
        {
            var d = VesselDesign.Stack(name, "probe.spark", "battery.s");
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "battery.s"), 0.5f, 0f, "relay.s", 1, false);
            DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "battery.s"), 0.5f, 180f, "solar.s", 1, false);
            double r = b.radius + alt, th = lonDeg * Math.PI / 180, sp = Math.Sqrt(b.mu / r);
            var v = VesselSim.CreateFree(d, b, new Vector3d(r * Math.Cos(th), 0, -r * Math.Sin(th)), new Vector3d(-sp * Math.Sin(th), 0, -sp * Math.Cos(th)), Quaternion.identity);
            foreach (var p in v.parts) if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1; }
            v.hasLaunched = true;
            v.throttle = 0;
            return v;
        }

        /// <summary>
        /// Milestone 12: the aerobraking forecast on an approach to Ochre; a heat-shielded probe in the middle of the pass;
        /// the comms map with a relay ring round Arden; the comms map zoomed out to a dish at Ochre; the Wayfarer.
        /// </summary>
        IEnumerator AeroTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            var ochre = root.bodies.First(b => b.name == "Ochre");
            var sun = root.bodies.First(b => b.isStar);

            // Coming in to Ochre on a hyperbola whose periapsis is in the air: the forecast
            var d = VesselDesign.Stack("Ember probe", "chute.s", "probe.spark", "battery.s", "shield.s");
            double vinf = 2600, alt = 23_000, rp = ochre.radius + alt, vp = Math.Sqrt(vinf * vinf + 2 * ochre.mu / rp);
            var hyp = Orbit.FromState(new Vector3d(rp, 0, 0), new Vector3d(0, 0, -vp), ochre.mu, u.ut + 3 * 3600);
            hyp.StateAt(u.ut, out var r0, out var v0);
            var probe = VesselSim.CreateFree(d, ochre, r0, v0, Quaternion.LookRotation(Vector3.forward, (-v0).normalized.ToFloat()));
            probe.hasLaunched = true; probe.throttle = 0; probe.sas = SasMode.Retrograde;
            u.Add(probe);
            root.Fly(probe);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.5f);
            f.mapView = true;
            f.SetMapView(ochre, (float)(ochre.radius * 8), 20f, 50f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("180_aerobrake_forecast");
            Log($"forecast: {f.AeroForecastText}");
            f.mapView = false;

            // Into the pass: jump to just before periapsis and watch it glow
            u.ut = hyp.epoch - 25;
            hyp.StateAt(u.ut, out r0, out v0);
            probe.pos = r0; probe.vel = v0; probe.rails = null;
            probe.rot = Quaternion.LookRotation(Vector3.forward, (-v0).normalized.ToFloat());
            yield return new WaitForSecondsRealtime(1.0f);
            f.SetCamera(130f, 10f, 14f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("181_aerobrake_pass");
            Log($"in the pass: {probe.Altitude / 1000:0.0} km, {probe.SurfaceRelVelocity.magnitude:0} m/s, {probe.dynPressure / 1000:0.0} kPa, destroyed {probe.destroyed}");

            // A relay ring round Arden and a probe on the far side, in the comms map
            for (int i = 0; i < 3; i++) u.Add(Relay($"Relay {i + 1}", home, 3_000_000, i * 120, u.ut));
            var far = Relay("Far-side probe", home, 200_000, 180, u.ut);
            u.Add(far);
            root.Fly(far);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            f.mapView = true;
            f.SetMapLayer(false, false, true);
            f.SetMapView(home, (float)(home.radius * 14), 30f, 70f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("182_comms_map");
            Log($"far-side probe signal: {(f.LinkText ?? "-")}");

            // Zoomed right out: a dish probe near Ochre, linked home through the deep space stations
            Vector3d toHome = (home.AbsolutePosition(u.ut) - ochre.AbsolutePosition(u.ut)).normalized;
            double rr = ochre.radius + 2_000_000;
            var dishProbe = VesselSim.CreateFree(WithDish("Ochre relay"), ochre, toHome * rr, Vector3d.Cross(new Vector3d(0, 1, 0), toHome).normalized * Math.Sqrt(ochre.mu / rr), Quaternion.identity);
            dishProbe.throttle = 0;
            UnfoldDish(dishProbe);
            dishProbe.hasLaunched = true;
            u.Add(dishProbe);
            root.Fly(dishProbe);
            f = root.flight;
            yield return new WaitForSecondsRealtime(1.2f);
            f.mapView = true;
            f.SetMapLayer(false, false, true);
            f.SetMapView(sun, 6.0e10f, 10f, 70f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("183_comms_deep_space");
            Log($"dish probe at Ochre: {(f.LinkText ?? "-")}");
            f.SetMapLayer(false, false);
            f.mapView = false;

            // The Wayfarer in the builder
            root.currentDesign = VesselDesign.Wayfarer();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("184_wayfarer");
        }
    }
}
