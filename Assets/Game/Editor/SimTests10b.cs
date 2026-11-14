using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 10b tests: rovers.</summary>
    public static partial class SimTests
    {
        static void RunMilestone10b()
        {
            Run("Rover: drives down the runway at its wheels' top speed, stays upright, uses electric charge", RoverDrives);
            Run("Rover: D steers right, A left; brakes stop it and park it; with no charge the motors do nothing", RoverSteerBrakePower);
            Run("Rover on Tal: drives 300 m over real ground, counts the distance, completes a drive contract, survives save/load", RoverOnTal);
        }

        static double Ground(VesselSim v) { Vector3d up = v.pos.normalized, s = v.SurfaceRelVelocity; return (s - up * Vector3d.Dot(s, up)).magnitude; }
        static double Upright(VesselSim v) => Vector3d.Dot(Vector3d.Rotate(v.rot, new Vector3d(0, 1, 0)), v.pos.normalized);
        static void Drive(VesselSim v, ref double t, double seconds)
        {
            int n = (int)(seconds / 0.02);
            for (int i = 0; i < n && !v.destroyed; i++) { v.Step(0.02, t); t += 0.02; }
        }

        /// <summary>Where each tyre's bottom and the lowest other contact point are, above the ground (debugging).</summary>
        static string RoverDebug(VesselSim v, double t)
        {
            var b = v.body;
            double Above(Vector3 local)
            {
                Vector3d P = v.pos + Vector3d.Rotate(v.rot, Vector3d.FromFloat(local - v.com));
                return P.magnitude - (b.radius + LaunchSite.SurfaceHeight(b, b.InertialToFixed(P.normalized, t)));
            }
            var tyres = v.parts.Where(p => p.def.kind == PartKind.Wheel)
                .Select(p => (Above(p.localPos + p.localRot * new Vector3(p.def.strutLength, 0, 0)) - p.def.wheelRadius).ToString("0.00"));
            var rigid = v.parts.Where(p => p.def.kind != PartKind.Wheel).SelectMany(p => v.ContactPoints(p).Select(c => (p.def.id, h: Above(c)))).OrderBy(x => x.h).First();
            return $"tyre bottoms {string.Join("/", tyres)} m; lowest other point {rigid.h:0.00} m on {rigid.id}";
        }

        static string RoverDrives()
        {
            var home = System4[0];
            var d = VesselDesign.Rover();
            Check(d.IsRover && !d.IsPlane, "the Tumbler isn't a rover");
            foreach (var id in new[] { "wheel.s", "wheel.l", "frame.r" })
                Check(TechTree.NodeOf(id) != null && Prices.Of(PartCatalog.Get(id)) > 300, $"{id} has no tech node or price");
            var v = VesselSim.CreateOnRunway(d, home, 0);
            double t = 0;
            Drive(v, ref t, 1);
            Check(v.landed && !v.destroyed && Upright(v) > 0.99, $"the rover doesn't stand on the runway (upright {Upright(v):0.00})");
            Vector3d start = home.InertialToFixed(v.pos, t), east = CelestialBody.EastAt(start.normalized);
            double ec0 = v.EcStored;
            v.brakesOn = false;
            v.driveInput = 1;
            double top = 0;
            Drive(v, ref t, 25);
            top = Ground(v);
            Check(!v.destroyed, v.destroyReason);
            Vector3d moved = home.InertialToFixed(v.pos, t) - start;
            double along = Vector3d.Dot(moved, east);
            Check(top > 11 && top < 15, $"top speed {top:0.0} m/s (the big wheels make 14); landed {v.landed}, on wheels {v.OnWheels}, radar {v.RadarAltitude(t):0.00}, " +
                $"EC {v.EcStored:0}, touching {v.parts.Count(p => p.def.kind == PartKind.Wheel && p.touching)}, roll {string.Join("/", v.parts.Where(p => p.def.kind == PartKind.Wheel).Select(p => p.rollSpeed.ToString("0.0")))}, " +
                $"brakes {v.brakesOn}, drive {v.driveInput}, moved {moved.magnitude:0.0} m; " + RoverDebug(v, t));
            Check(along > 200 && along > 0.95 * moved.magnitude, $"went {along:0} m east of {moved.magnitude:0} m");
            Check(Upright(v) > 0.97, $"leaning: upright {Upright(v):0.00}");
            Check(v.EcStored < ec0 - 2, $"used {ec0 - v.EcStored:0.0} EC");
            Check(Math.Abs(v.driven - along) < along * 0.1 && v.drivenOn == "Arden", $"counted {v.driven:0} m on {v.drivenOn}");
            return $"{top:0.0} m/s after 25 s, {along:0} m down the runway, {ec0 - v.EcStored:0} EC used, driven {v.driven:0} m";
        }

        static string RoverSteerBrakePower()
        {
            var home = System4[0];
            string Turn(float yaw, out double turned)
            {
                var v = VesselSim.CreateOnRunway(VesselDesign.Rover(), home, 0);
                double t = 0;
                v.brakesOn = false;
                v.driveInput = 1;
                Drive(v, ref t, 6);
                Vector3d up = v.pos.normalized, s0 = v.SurfaceRelVelocity;
                Vector3d f0 = (s0 - up * Vector3d.Dot(s0, up)).normalized, right = Vector3d.Cross(up, f0);
                v.inputYaw = yaw;
                Drive(v, ref t, 3);
                up = v.pos.normalized;
                Vector3d s1 = v.SurfaceRelVelocity, f1 = (s1 - up * Vector3d.Dot(s1, up)).normalized;
                turned = Math.Atan2(Vector3d.Dot(f1, right), Vector3d.Dot(f1, f0)) * 180 / Math.PI;
                Check(!v.destroyed && Upright(v) > 0.9, $"rolled over turning ({Upright(v):0.00}) {v.destroyReason}");
                return $"{turned:+0;-0} deg";
            }
            Turn(1, out double right1);
            Turn(-1, out double left1);
            Check(right1 > 15 && left1 < -15, $"D turned {right1:0} deg, A turned {left1:0} deg (right is +)");

            // Brakes stop it and park it
            var b = VesselSim.CreateOnRunway(VesselDesign.Rover(), home, 0);
            double tb = 0;
            b.brakesOn = false; b.driveInput = 1;
            Drive(b, ref tb, 8);
            double before = Ground(b);
            b.driveInput = 0; b.brakesOn = true;
            Drive(b, ref tb, 12);
            Check(b.landed && Ground(b) < 0.2, $"from {before:0.0} m/s the brakes left it at {Ground(b):0.00} m/s, parked {b.landed}");

            // No charge: no drive
            var p = VesselSim.CreateOnRunway(VesselDesign.Rover(), home, 0);
            foreach (var part in p.parts) part.charge = 0;
            double tp = 0;
            p.brakesOn = false; p.driveInput = 1;
            Drive(p, ref tp, 5);
            Check(Ground(p) < 0.3, $"a flat rover still drove at {Ground(p):0.0} m/s");
            return $"D {right1:+0} deg, A {left1:0} deg in 3 s at speed; braked from {before:0.0} m/s and parked; no charge, no drive";
        }

        static string RoverOnTal()
        {
            // A smooth patch of the dark plains
            Vector3d spot = Vector3d.up;
            double best = double.MaxValue;
            foreach (var dir in Directions(3000, 404))
            {
                if (Regions.At(Tal, dir) != "Dark Plains") continue;
                Vector3d e = CelestialBody.EastAt(dir);
                double h0 = Terrain.Height(Tal, dir), worst = 0;
                for (int k = 1; k <= 6; k++) worst = Math.Max(worst, Math.Abs(Terrain.Height(Tal, (dir + e * (k * 60 / Tal.radius)).normalized) - h0));
                if (worst < best) { best = worst; spot = dir; }
            }
            var u = CareerUniverse();
            var v = u.Add(VesselSim.CreateLanded(VesselDesign.Rover(), Tal, spot, 0));
            double t = 0;
            Drive(v, ref t, 1);
            v.driveInput = 1;
            Drive(v, ref t, 45);
            Check(!v.destroyed, v.destroyReason);
            Check(v.drivenOn == "Tal" && v.driven > 300, $"driven {v.driven:0} m on {v.drivenOn}");
            Check(Upright(v) > 0.9 && Math.Abs(v.RadarAltitude(t)) < 1.5, $"upright {Upright(v):0.00}, radar {v.RadarAltitude(t):0.0} m");
            double speed = Ground(v);

            var c = u.career;
            var job = new Contract { id = 970, type = "rover", body = "Tal", amount = 300, state = ContractState.Active, deadline = 1e12, reward = 1000 };
            var far = new Contract { id = 971, type = "rover", body = "Vesk", amount = 10, state = ContractState.Active, deadline = 1e12, reward = 1000 };
            c.contracts.Add(job); c.contracts.Add(far);
            c.CheckVessel(v, t);
            Check(job.state == ContractState.Completed && far.state == ContractState.Active, "the Tal drive contract didn't complete (or the Vesk one did)");
            c.records |= Records.TalLanding;
            foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id);
            var cand = (List<Contract>)typeof(Career).GetMethod("Candidates", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { 0.0 });
            Check(cand.Any(k => k.type == "rover" && k.body == "Arden"), "no test drive on offer");

            var u2 = new Universe();
            u2.LoadJson(u.ToJson(), System4);
            var back = u2.Find(v.id);
            Check(back != null && back.drivenOn == "Tal" && Math.Abs(back.driven - v.driven) < 0.01 && back.HasDriveWheels, "distance or wheels lost in save/load");
            return $"{v.driven:0} m on Tal's dark plains in 45 s ({speed:0.0} m/s), drive contract done, saved and loaded";
        }
    }
}
