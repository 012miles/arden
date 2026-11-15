using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 41 tests: the fixes (roles in an expedition, the roll-out in the stream, the death screen's tips, the update log).</summary>
    public static partial class SimTests
    {
        static void RunMilestone41()
        {
            Run("Expedition roles: only the builder builds and launches; the others watch, comment and say ready", ExpeditionRolesTest);
            Run("Rooms: the host's roll-out to the pad goes out with each frame (and nothing when there isn't one)", RolloutFrameTest);
            Run("Death screen: a tip that fits each way of being lost", DeathTipTest);
            Run("Update log: the first version, then only the latest updates", UpdateLogTrimTest);
            Run("Water: a capsule floats upright, part under; a lone engine sinks; a tall rocket tips onto its side; a drift slows in the water", FloatTest);
            Run("Water: a splashdown plunges in and floats (no crash), engines push a floating ship along, and a ship lifts off the water", SplashTest);
            Run("Landing: a lander that touches down tilted on one leg settles onto all its legs before it parks (it isn't frozen at the angle it touched)", SettleTest);
        }

        static Vector3d SeaSpot => CelestialBody.DirectionFromLatLon(5, 30);

        static string FloatTest()
        {
            var home = System4[0];
            Check(Terrain.IsWater(home, SeaSpot), "the test spot is sea");
            double t = 0;
            var cap = VesselSim.CreateLanded(VesselDesign.Stack("Bobber", "chute.s", "pod.wren", "shield.s"), home, SeaSpot, 0);
            Check(cap.FloatCapacity > cap.mass * 1.3, $"a capsule can float ({cap.FloatCapacity:0.00} t of water vs {cap.mass:0.00} t)");
            StepFor(cap, 30, ref t);
            double up = Vector3d.Dot(cap.Axis, cap.pos.normalized), alt = cap.Altitude;
            Check(!cap.destroyed && cap.landed && cap.InWater, "the capsule's afloat: " + cap.destroyReason);
            Check(cap.submerged > 0.15f && cap.submerged < 0.9f, $"part of it under water: {cap.submerged:P0}");
            Check(up > 0.85, $"floating upright: {Math.Acos(Math.Min(1, up)) * 180 / Math.PI:0} deg over");
            Check(Math.Abs(alt) < 1.5, $"riding at the waterline: {alt:0.00} m");
            string capsule = $"capsule {cap.submerged:P0} under, {Math.Acos(Math.Min(1, up)) * 180 / Math.PI:0} deg";

            var anchor = VesselSim.CreateLanded(VesselDesign.Stack("Anchor", "eng.kestrel", "eng.kestrel", "eng.kestrel"), home, SeaSpot, 0);
            Check(anchor.FloatCapacity < anchor.mass, "three engines can't float");
            t = 0;
            StepFor(anchor, 120, ref t);
            Check(anchor.destroyed && anchor.destroyReason.Contains("sank"), "the engines sank: " + (anchor.destroyed ? anchor.destroyReason : $"still at {anchor.Altitude:0.0} m"));

            var tall = VesselSim.CreateLanded(VesselDesign.Stack("Tall", "pod.wren", "tank.s2", "tank.s2", "tank.s2", "eng.kestrel"), home, SeaSpot, 0);
            t = 0;
            StepFor(tall, 60, ref t);
            double tallUp = Math.Abs(Vector3d.Dot(tall.Axis, tall.pos.normalized));
            Check(!tall.destroyed && tall.InWater, "the tall rocket's afloat: " + tall.destroyReason);
            Check(tallUp < 0.6, $"the tall rocket toppled onto its side: {Math.Acos(Math.Min(1, tallUp)) * 180 / Math.PI:0} deg from upright");

            var drift = VesselSim.CreateLanded(VesselDesign.Stack("Drifter", "chute.s", "pod.wren", "shield.s"), home, SeaSpot, 0);
            t = 0;
            StepFor(drift, 5, ref t);
            Vector3d a = drift.LandedFixedPos;
            drift.floatVel += CelestialBody.EastAt(a.normalized) * 8;
            StepFor(drift, 20, ref t);
            double moved = (drift.LandedFixedPos - a).magnitude, left = drift.floatVel.magnitude;
            Check(moved > 10 && left < 2.5, $"a push carries it {moved:0} m and the water slows it to {left:0.0} m/s");
            return $"{capsule}; engines sank; tall rocket {Math.Acos(Math.Min(1, tallUp)) * 180 / Math.PI:0} deg over; drifted {moved:0} m";
        }

        static string SplashTest()
        {
            var home = System4[0];
            var d = VesselDesign.Stack("Splasher", "chute.s", "pod.wren", "shield.s");
            Vector3d up = SeaSpot;
            var cap = VesselSim.CreateFree(d, home, up * (home.radius + 25), Vector3d.zero, Quaternion.FromToRotation(Vector3.up, up.ToFloat()));
            cap.vel = home.SurfaceVelocity(cap.pos) - up * 9;
            double t = 0, lowest = 0;
            bool wasIn = false;
            for (int i = 0; i < 1500 && !cap.destroyed; i++)
            {
                cap.Step(0.02, t); t += 0.02;
                if (cap.landed) { wasIn = true; lowest = Math.Min(lowest, cap.Altitude); }
            }
            Check(!cap.destroyed && wasIn && cap.InWater, "a 9 m/s splashdown floats: " + cap.destroyReason);
            Check(cap.splash > 3, $"it made a splash ({cap.splash:0.0})");
            Check(lowest < -0.2 && Math.Abs(cap.Altitude) < 1.5, $"it plunged in ({lowest:0.00} m) and came back up ({cap.Altitude:0.00} m)");

            // a floating rocket lying on its side with its engine lit is pushed along the surface
            var boat = VesselSim.CreateLanded(VesselDesign.Stack("Boat", "pod.wren", "tank.s2", "eng.ember"), home, SeaSpot, 0);
            t = 0;
            StepFor(boat, 40, ref t);   // (it topples over first)
            Vector3d a = boat.LandedFixedPos;
            boat.throttle = 0.35;
            foreach (var p in boat.parts) if (p.def.IsEngine) p.ignited = true;
            StepFor(boat, 10, ref t);
            double moved = (boat.LandedFixedPos - a).magnitude;
            Check(!boat.destroyed && (moved > 5 || !boat.landed), $"its engine moved it {moved:0} m (landed {boat.landed})");

            // enough thrust lifts a ship off the water
            var hop = VesselSim.CreateLanded(VesselDesign.Stack("Hop", "pod.wren", "tank.s2", "eng.kestrel"), home, SeaSpot, 0);
            t = 0;
            StepFor(hop, 2, ref t);
            hop.throttle = 1;
            foreach (var p in hop.parts) if (p.def.IsEngine) p.ignited = true;
            StepFor(hop, 3, ref t);
            Check(!hop.destroyed && !hop.landed && hop.Altitude > 3, $"full throttle lifts it off the water ({hop.Altitude:0.0} m)");
            return $"plunged to {lowest:0.00} m; the engine pushed it {moved:0} m";
        }

        static string SettleTest()
        {
            var b = Body;
            var sim = LanderAboveGround(true, 2);
            sim.sas = SasMode.Stability;   // (SAS mustn't hold it up on one leg)
            Vector3d up = sim.pos.normalized;
            sim.rot = Quaternion.AngleAxis(20f, CelestialBody.EastAt(up).ToFloat()) * sim.rot;   // touching down tilted
            sim.pos += up * 1.2;
            double t = 0;
            bool touched = false;
            double tiltAtTouch = -1;
            for (int i = 0; i < 3000 && !sim.landed && !sim.destroyed; i++)
            {
                sim.Step(0.02, t); t += 0.02;
                if (!touched && sim.settling) { touched = true; tiltAtTouch = Math.Acos(Math.Min(1, Vector3d.Dot(sim.Axis, sim.pos.normalized))) * 180 / Math.PI; }
            }
            double tilt = Math.Acos(Math.Min(1, Vector3d.Dot(sim.Axis, sim.pos.normalized))) * 180 / Math.PI;
            Check(!sim.destroyed, "the lander broke: " + sim.destroyReason);
            Check(touched, "it never touched down to settle");
            Check(sim.landed, "it never parked");
            Check(tiltAtTouch > 12, $"test setup: it touched down tilted ({tiltAtTouch:0} deg)");
            Check(tilt < 6, $"it settled onto its legs: {tilt:0.0} deg from upright (touched at {tiltAtTouch:0})");
            return $"touched at {tiltAtTouch:0} deg, parked at {tilt:0.0} deg after {t:0.0} s";
        }

        static string ExpeditionRolesTest()
        {
            var host = NetRoom.Host("Ana", "Trip test", new MissionPlan(), 2);
            Check(host.Error == null, "couldn't open an expedition room: " + host.Error);
            var clients = new List<NetRoom>();
            foreach (var n in new[] { "Ben", "Cy" }) clients.Add(NetRoom.Join(n, "127.0.0.1:" + host.ListenPort));
            bool Pump(Func<bool> done, double seconds)
            {
                var t0 = DateTime.Now;
                while ((DateTime.Now - t0).TotalSeconds < seconds)
                {
                    host.Update(0.05f);
                    foreach (var c in clients) c.Update(0.05f);
                    if (done()) return true;
                    Thread.Sleep(15);
                }
                return false;
            }
            try
            {
                Check(Pump(() => host.State.players.Count == 3 && clients.All(c => c.Connected && c.MyId > 0 && c.State.players.Count == 3 && c.State.activity == 2), 8), "not everyone got into the expedition room");
                var builderRoom = new[] { host }.Concat(clients).First(r => r.Me.Has((MRole)Coop.Builder));
                var other = clients.First(r => r != builderRoom && !r.Me.Has((MRole)Coop.Builder));
                var otherCoop = new Coop(null, other);
                var builderCoop = new Coop(null, builderRoom);
                Check(builderCoop.Expedition && otherCoop.Expedition, "the room isn't an expedition");
                Check(builderCoop.CanBuild && builderCoop.CanDo(CAct.Build), "the builder can't build");
                Check(!otherCoop.CanBuild && !otherCoop.CanDo(CAct.Build), $"{other.Me.name} isn't the builder but can build");
                string design = JsonUtility.ToJson(VesselDesign.Default());
                builderRoom.Send(new NetMsg { t = "design", json = design });
                Check(Pump(() => host.State.designJson == design && clients.All(c => c.State.designJson == design), 4), "the builder's rocket didn't reach everyone");
                other.Send(new NetMsg { t = "design", json = JsonUtility.ToJson(VesselDesign.Stack("Hack", "pod.wren")) });
                Pump(() => false, 0.4);
                Check(host.State.designJson == design, "someone who isn't the builder changed the rocket");
                int launches = 0;
                host.OnHostCommand = (who, m) => { if (m.t == "launch") launches++; };
                other.Send(new NetMsg { t = "launch", n = 0 });
                Pump(() => false, 0.4);
                Check(launches == 0, "someone who isn't the builder launched");
                builderRoom.Send(new NetMsg { t = "launch", n = 0 });
                Check(Pump(() => launches == 1, 3), "the builder's launch didn't get through");
                return $"builder {builderRoom.Me.name}; {other.Me.name} watches";
            }
            finally { foreach (var c in clients) c.Stop(); host.Stop(); }
        }

        static string RolloutFrameTest()
        {
            var f = new FlightFrame { ut = 10, roll = 7.5f, rollPad = 2 };
            var back = JsonUtility.FromJson<FlightFrame>(JsonUtility.ToJson(f));
            Check(Math.Abs(back.roll - 7.5f) < 1e-4 && back.rollPad == 2, "the roll-out didn't survive the trip");
            var none = JsonUtility.FromJson<FlightFrame>(JsonUtility.ToJson(new FlightFrame { ut = 10 }));
            Check(none.roll == 0, "a frame without a roll-out says there is one");
            var old = JsonUtility.FromJson<FlightFrame>("{\"ut\":5}");   // from an older version
            Check(old.roll == 0, "an older frame reads as a roll-out");
            return "ok";
        }

        static string DeathTipTest()
        {
            Check(FlightMode.DeathTip("Hit the ground at 40 m/s (Wren pod can take 12 m/s)").Contains("6 m/s"), "a hard landing's tip");
            Check(FlightMode.DeathTip("Hit the sea at 40 m/s (Wren pod can take 26 m/s in water)").Contains("parachutes"), "a splashdown's tip");
            Check(FlightMode.DeathTip("Heat shield burned up on reentry (2900 K, rated 2600 K)").Contains("heat shield"), "a reentry's tip");
            Check(FlightMode.DeathTip("Wren pod was crushed by the airflow (90 kPa, it takes 60 kPa)").Contains("Throttle back"), "too much air's tip");
            Check(FlightMode.DeathTip("Iris Ardling was knocked out and nobody reached them in time.").Contains("revive kit"), "a knock-out's tip");
            Check(!string.IsNullOrEmpty(FlightMode.DeathTip(null)) && !string.IsNullOrEmpty(FlightMode.DeathTip("Something odd")), "always some tip");
            return "ok";
        }

        static string UpdateLogTrimTest()
        {
            var all = UpdateLog.All;
            Check(all.Last().n == 1 && all.First().n == UpdateLog.Latest.n && all.First().n >= 41, "the log should run from the newest update down to the first version");
            Check(!all.Any(u => u.n > 1 && u.n < 31), "older updates are still listed: " + string.Join(",", all.Where(u => u.n > 1 && u.n < 31).Select(u => u.n)));
            return string.Join(", ", all.Select(u => u.n));
        }
    }
}
