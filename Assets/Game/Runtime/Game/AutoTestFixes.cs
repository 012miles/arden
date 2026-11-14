using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Bug fixes: a ferry drifts into the side of a station and bounces off (ships used to pass through each other);
        /// a station with a docking port on its side, and a ferry docking to it there; the side port in the builder.
        /// </summary>
        IEnumerator FixesTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            u.ut = TimeWithSun(home, CelestialBody.DirectionFromLatLon(0, 30), 0.6, 0);

            // 1) A bump: the ferry drifts sideways into the station's tank at 1 m/s
            var ferry = Fly(VesselDesign.FerryCraft("Ferry"), home, 300_000, 0, 30, u.ut, true);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(0.6f);
            Vector3d up = ferry.pos.normalized, along = ferry.vel.normalized, side = Vector3d.Cross(up, along).normalized;
            var station = VesselSim.CreateFree(VesselDesign.Outpost("Outpost"), home, ferry.pos + side * 7, ferry.vel, ferry.rot);
            station.throttle = 0; station.hasLaunched = true; station.sas = SasMode.Stability;
            u.Add(station);
            ferry.vel += side * 1.0;
            ferry.rails = null;
            f.SetTarget(station);
            f.SetCamera(100f, 25f, 26f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("260_bump_closing");
            double closing0 = Vector3d.Dot(ferry.vel - station.vel, side);
            yield return WaitFor(() => Vector3d.Dot(f.active.vel - station.vel, side) < -0.01, 15, "the bounce");
            double after = Vector3d.Dot(f.active.vel - station.vel, side);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("261_bounced_off");
            Log($"bump: closing at {closing0:0.00} m/s, then moving apart at {-after:0.00} m/s; ferry destroyed {f.active.destroyed}, station destroyed {station.destroyed}");

            // 2) A docking port on the side of a tank, and a ferry docking to it
            var sd = VesselDesign.Stack("Side dock", "probe.spark", "tank.s4", "eng.ember");
            DesignOps.AttachSurfaceAt(sd, sd.nodes.FindIndex(n => n.id == "tank.s4"), 0.5f, 0f, "port.sm", 1, false);
            var ferry2 = Fly(VesselDesign.FerryCraft("Ferry 2"), home, 300_000, 0, 60, u.ut, true);
            f = root.flight;
            yield return new WaitForSecondsRealtime(0.6f);
            var dock = VesselSim.CreateFree(sd, home, ferry2.pos + ferry2.vel.normalized * 30, ferry2.vel, ferry2.rot);
            dock.throttle = 0; dock.hasLaunched = true; dock.sas = SasMode.Stability;
            u.Add(dock);
            var sp = dock.parts.Find(p => p.def.kind == PartKind.DockingPort);
            dock.PortFaceWorld(sp, out var face, out var nrm);
            // Line the ferry's nose port up 3 m off the side port, closing at 0.4 m/s
            ferry2.rot = Quaternion.FromToRotation(Vector3.up, (-nrm).ToFloat());
            var fp = ferry2.parts.Find(p => p.def.kind == PartKind.DockingPort);
            ferry2.PortFaceWorld(fp, out var ff, out _);
            ferry2.pos += (face + nrm * 3.0) - ff;
            ferry2.vel = dock.vel - nrm * 0.4;
            ferry2.angVel = Vector3.zero;
            ferry2.rails = null;
            f.SetTarget(dock);
            ferry2.sas = SasMode.DockAlign;   // "Align ports", as a player would (plain hold would swing back to the old attitude)
            f.LookAlong(-nrm + up * 0.3, 24f, 30f, 10f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("262_side_port_approach");
            yield return WaitFor(() => !u.vessels.Contains(dock), 20, "the side docking");
            yield return new WaitForSecondsRealtime(1.0f);
            f.SetCamera(160f, 20f, 26f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("263_side_port_docked");
            Log($"side docking: docked {!u.vessels.Contains(dock)}, {f.active.name} now {f.active.parts.Count} parts");

            // 3) The side port in the builder: it stands out from the tank
            root.currentDesign = sd;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("264_builder_side_port");
        }
    }
}
