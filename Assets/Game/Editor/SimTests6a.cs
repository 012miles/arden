using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 6a tests: electricity and the comms network.</summary>
    public static partial class SimTests
    {
        static void RunMilestone6a()
        {
            Run("Power: a probe drains its battery at its idle rate; empty, it can't steer", PowerDrain);
            Run("Power: a deployed panel facing the sun charges; in shadow or folded it doesn't", PowerSolar);
            Run("Power: reaction wheels draw extra while steering; engines charge while firing", PowerWheelsAlternator);
            Run("Comms: range from both antennas, line of sight blocked by Arden, relays reach the far side", CommsLinks);
            Run("Comms: a probe without signal has partial control; crew always have full control", CommsPartialControl);
            Run("Power and charge survive save/load; old saves come back charged", PowerSave);
        }

        static VesselSim Sat(params string[] extra)
        {
            var home = System4[0];
            var ids = new List<string> { "probe.spark", "tank.s1" };
            var d = D("sat", ids.ToArray());
            int t = d.nodes.FindIndex(n => n.id == "tank.s1");
            float ang = 0;
            foreach (var e in extra) { DesignOps.AttachSurfaceAt(d, t, 0.5f, ang, e, 1, false); ang += 90; }
            var s = InOrbit(d, home, home.radius + 300_000, 0);
            s.sas = SasMode.Off;
            return s;
        }

        static string PowerDrain()
        {
            var s = Sat();
            double e0 = s.EcStored;
            Check(Math.Abs(e0 - 30) < 1e-6, $"probe starts with {e0} EC");
            double t = 0;
            for (int i = 0; i < 10; i++) { s.BackgroundAdvance(10, t, false); t += 10; }
            Check(Math.Abs(s.EcStored - (30 - 0.03 * 100)) < 1e-6, $"after 100 s: {s.EcStored:F3} EC, expected 27");
            Check(s.HasControl, "lost control with power left");
            for (int i = 0; i < 120; i++) { s.BackgroundAdvance(10, t, false); t += 10; }
            Check(s.EcStored < 1e-6 && !s.HasControl, $"after the battery ran out: {s.EcStored:F3} EC, control={s.HasControl}");
            // and without control, input does nothing
            s.inputPitch = 1;
            for (int i = 0; i < 50; i++) { s.Step(0.02, t); t += 0.02; }
            Check(s.angVel.magnitude < 1e-6, $"a dead probe turned ({s.angVel.magnitude:E2} rad/s)");
            return $"30 EC -> 27 EC in 100 s; dead after {30 / 0.03:0} s; no steering without power";
        }

        static string PowerSolar()
        {
            var home = System4[0];
            VesselSim Lit(bool sunSide, bool deployed)
            {
                var s = Sat("solar.s", "battery.r");
                var panel = s.parts.First(p => p.def.id == "solar.s");
                if (deployed) { panel.legsDeployed = true; panel.legDeploy = 1; }
                Vector3d dir = sunSide ? Sun.DirectionAt(0) : -Sun.DirectionAt(0);
                double r = home.radius + 300_000;
                // a circular orbit through that point, heading east
                Vector3d east = Vector3d.Cross(new Vector3d(0, 1, 0), dir).normalized;
                s.pos = dir * r;
                s.vel = east * Math.Sqrt(home.mu / r);
                s.rails = null;
                Vector3 n0 = panel.localRot * Vector3.forward;
                s.rot = Quaternion.FromToRotation(n0, Sun.DirectionAt(0).ToFloat());
                foreach (var p in s.parts) p.charge = p.def.ecCapacity * 0.5;
                return s;
            }
            var sunny = Lit(true, true);
            double out1 = sunny.SolarOutput(0);
            Check(sunny.InSunlight(0) && Math.Abs(out1 - 1.2) < 0.01, $"panel facing the sun makes {out1:F3} EC/s (expected 1.2)");
            double e0 = sunny.EcStored, t = 0;
            for (int i = 0; i < 6; i++) { sunny.BackgroundAdvance(5, t, false); t += 5; }
            Check(sunny.EcStored > e0 + 30 * (1.2 - 0.03) * 0.9, $"charged only {sunny.EcStored - e0:F2} EC in 30 s");
            var shade = Lit(false, true);
            Check(!shade.InSunlight(0) && shade.SolarOutput(0) == 0, "made power in Arden's shadow");
            var folded = Lit(true, false);
            Check(folded.SolarOutput(0) == 0, "a folded panel made power");
            return $"sunlit {out1:0.00} EC/s, +{sunny.EcStored - e0:0.0} EC in 30 s; shadow and folded: 0";
        }

        static string PowerWheelsAlternator()
        {
            var a = Sat();
            var b = Sat();
            double t = 0;
            for (int i = 0; i < 250; i++)
            {
                b.inputPitch = (i / 25) % 2 == 0 ? 1 : -1; // rock back and forth so the wheels keep working
                a.Step(0.02, t); b.Step(0.02, t); t += 0.02;
            }
            double idle = 30 - a.EcStored, steering = 30 - b.EcStored;
            Check(steering > idle * 1.5, $"steering used {steering:F3} EC vs idle {idle:F3}");
            // An engine firing charges the batteries
            var home = System4[0];
            var r = InOrbit(D("burn", "probe.spark", "tank.s2", "eng.ember"), home, home.radius + 300_000, 0);
            foreach (var p in r.parts) p.charge = Math.Min(5, p.def.ecCapacity);
            r.ActivateNextStage();
            r.throttle = 1;
            t = 0;
            for (int i = 0; i < 250; i++) { r.Step(0.02, t); t += 0.02; }
            Check(r.EcStored > 5 + 4, $"5 s of burning only brought power to {r.EcStored:F2} EC");
            return $"5 s idle {idle:F3} EC, steering {steering:F3} EC; Ember burn +{r.EcStored - 5:F1} EC";
        }

        static VesselSim AtLon(string[] ids, double lon, double altitude, bool deploy)
        {
            var home = System4[0];
            var d = D("c", ids[0], "tank.s1");
            for (int i = 1; i < ids.Length; i++) DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s1"), 0.5f, 90f * i, ids[i], 1, false);
            Vector3d dir = CelestialBody.DirectionFromLatLon(0, lon);
            var v = VesselSim.CreateFree(d, home, dir * (home.radius + altitude), Vector3d.zero, Quaternion.identity);
            if (deploy) foreach (var p in v.parts) if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1; }
            v.name = string.Join("+", ids);
            return v;
        }

        static string CommsLinks()
        {
            var none = new List<VesselSim>();
            var overhead = AtLon(new[] { "probe.spark" }, 0, 100_000, false);
            var l1 = CommNet.Connect(overhead, none, System4, 0);
            Check(l1.Connected && l1.via.Count == 0, "a probe 100 km above the station had no signal");
            var high = AtLon(new[] { "probe.spark" }, 0, 5_000_000, false);
            Check(!CommNet.Connect(high, none, System4, 0).Connected, "the built-in antenna reached 5,000 km (range is 2,000 km)");
            var highAnt = AtLon(new[] { "probe.spark", "antenna.s" }, 0, 5_000_000, true);
            Check(CommNet.Connect(highAnt, none, System4, 0).Connected, "a deployed antenna didn't reach 5,000 km");
            var foldedAnt = AtLon(new[] { "probe.spark", "antenna.s" }, 0, 5_000_000, false);
            Check(!CommNet.Connect(foldedAnt, none, System4, 0).Connected, "a folded antenna worked");
            var farSide = AtLon(new[] { "probe.spark", "antenna.s" }, 150, 100_000, true);
            Check(!CommNet.Connect(farSide, none, System4, 0).Connected, "signal went through Arden");
            var relay = AtLon(new[] { "probe.spark", "relay.s" }, 75, 2_500_000, true);
            var l2 = CommNet.Connect(farSide, new List<VesselSim> { relay }, System4, 0);
            Check(l2.Connected && l2.via.Count == 1 && l2.via[0] == relay.name, $"the relay didn't carry the signal ({l2.Describe})");
            Check(l2.signal > 0.05 && l2.signal < 1, $"relayed signal {l2.signal:F2}");
            var deadRelay = AtLon(new[] { "probe.spark", "relay.s" }, 75, 2_500_000, true);
            foreach (var p in deadRelay.parts) p.charge = 0;
            Check(!CommNet.Connect(farSide, new List<VesselSim> { deadRelay }, System4, 0).Connected, "a relay with no power passed the signal on");
            return $"overhead {l1.signal * 100:0}%; 5,000 km needs an antenna; far side blocked; via relay: {l2.Describe}";
        }

        static string CommsPartialControl()
        {
            var probe = Sat();
            probe.connected = false;
            Check(probe.PartialControl, "a probe without signal had full control");
            probe.connected = true;
            Check(!probe.PartialControl, "a connected probe had partial control");
            var home = System4[0];
            var crewed = InOrbit(D("crew", "pod.wren", "tank.s1"), home, home.radius + 300_000, 0);
            crewed.connected = false;
            Check(!crewed.PartialControl && crewed.HasControl, "crew lost control without a signal");
            return "probe: partial control without signal; crewed capsule: full control";
        }

        static string PowerSave()
        {
            var s = Sat("battery.r");
            Check(Math.Abs(s.EcCapacity - 130) < 1e-6, $"capacity {s.EcCapacity}, expected 130 (probe 30 + pack 100)");
            s.AddCharge(-77);
            var u = new Universe();
            u.Add(s);
            var u2 = new Universe();
            u2.LoadJson(u.ToJson(), System4);
            Check(Math.Abs(u2.vessels[0].EcStored - s.EcStored) < 1e-6, "charge changed in the save");
            string old = u.ToJson().Replace("\"saveVersion\":3", "\"saveVersion\":2");
            var u3 = new Universe();
            u3.LoadJson(old, System4);
            Check(Math.Abs(u3.vessels[0].EcStored - 130) < 1e-6, $"an old save loaded with {u3.vessels[0].EcStored} EC");
            return $"{s.EcStored:0} of 130 EC saved and loaded; old saves start full";
        }
    }
}
