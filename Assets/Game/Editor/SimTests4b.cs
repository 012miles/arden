using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 4b tests: docking, undocking, crew, and structural failure.</summary>
    public static partial class SimTests
    {
        static void RunMilestone4b()
        {
            Run("Docking: lined-up ports at 0.3 m/s latch; mass, momentum, crew and fuel lines join up", DockingMerge);
            Run("Docking refuses: 40 deg out of line, 3 m/s, or mismatched port sizes", DockingRefusals);
            Run("Undocking gives back both vessels (names, parts, crew) and pushes them apart gently", UndockSplit);
            Run("Docked vessels save and load, and still undock afterwards", DockedSaveLoad);
            Run("Crew: seats fill at launch, transfers move people, an empty capsule can't steer", CrewTransfer);
            Run("Structure: an overloaded joint tears, a strong one holds", JointOverload);
            Run("Structure: a wing pulled at 20 deg and 300 m/s tears off; at 3 deg it holds", WingOverload);
            Run("Fragile parts: a deployed solar panel tears off at 3 kPa, a folded one doesn't", FragilePanels);
        }

        /// <summary>Two ferries nose to nose in the same orbit, 'gap' metres between port faces, A closing at 'speed'.</summary>
        static void NoseToNose(out VesselSim a, out VesselSim b, double gap, double speed, float misalignDeg = 0, string portB = null)
        {
            var home = System4[0];
            double r = home.radius + 100_000, v = Math.Sqrt(home.mu / r);
            var pos = new Vector3d(r, 0, 0);
            var vel = new Vector3d(0, 0, -v);
            a = VesselSim.CreateFree(VesselDesign.FerryCraft("Alpha"), home, pos, vel + new Vector3d(0, 0, -speed),
                Quaternion.FromToRotation(Vector3.up, new Vector3(0, 0, -1)));
            var bd = VesselDesign.FerryCraft("Bravo");
            if (portB != null) bd.nodes[bd.nodes.FindIndex(n => n.id == "port.sm")].id = portB;
            Quaternion rb = Quaternion.AngleAxis(misalignDeg, Vector3.right) * Quaternion.FromToRotation(Vector3.up, new Vector3(0, 0, 1));
            b = VesselSim.CreateFree(bd, home, pos + new Vector3d(0, 0, -20), vel, rb);
            a.PortFaceWorld(a.FreePorts().First(), out var fa, out _);
            b.PortFaceWorld(b.FreePorts().First(), out var fb, out _);
            Vector3d want = fa + new Vector3d(0, 0, -gap);
            b.pos = b.pos + (want - fb);
            b.prevPos = b.pos;
            a.rcsOn = b.rcsOn = false;
        }

        /// <summary>Steps both until A docks with B (or time runs out). Returns the time it took, or NaN.</summary>
        static double FlyUntilDocked(VesselSim a, VesselSim b, double maxT, out double pBefore, out double mBefore, out string lastNote)
        {
            double t = 0;
            pBefore = 0; mBefore = 0; lastNote = null;
            for (int i = 0; t < maxT; i++)
            {
                a.Step(0.02, t); b.Step(0.02, t); t += 0.02;
                Vector3d p = a.vel * a.mass + b.vel * b.mass;
                pBefore = p.magnitude; mBefore = a.mass + b.mass;
                if (a.TryDock(b, out string note)) return t;
                if (note != null) lastNote = note;
            }
            return double.NaN;
        }

        static string DockingMerge()
        {
            NoseToNose(out var a, out var b, 1.5, 0.3);
            int partsA = a.parts.Count, partsB = b.parts.Count, crew = a.CrewCount + b.CrewCount;
            Vector3d pTotal0 = a.vel * a.mass + b.vel * b.mass;
            double t = FlyUntilDocked(a, b, 20, out double pB, out double mB, out _);
            Check(!double.IsNaN(t), "never latched");
            Check(a.parts.Count == partsA + partsB && b.parts.Count == 0, $"{a.parts.Count} parts after docking, expected {partsA + partsB}");
            Check(Math.Abs(a.mass - mB) < 1e-6, $"mass {a.mass:F4} t, was {mB:F4} t");
            double pAfter = (a.vel * a.mass).magnitude;
            Check(Math.Abs(pAfter - pB) / pB < 1e-6, $"momentum changed by {(pAfter - pB) / pB:E2}");
            Check(a.CrewCount == crew, $"{a.CrewCount} crew aboard, expected {crew}");
            var ports = a.DockedPorts().ToList();
            Check(ports.Count == 2 && ports.All(p => a.FindPart(p.dockedTo) != null), "the two ports aren't linked to each other");
            var tanks = a.parts.Where(p => p.def.kind == PartKind.Tank).ToList();
            Check(tanks.Select(p => p.section).Distinct().Count() == 1, "fuel doesn't flow across the docking port");
            Check(ports.Any(p => p.dockedName == "Bravo"), "Bravo's name wasn't kept for undocking");
            Check(a.HasControl, "the docked vessel lost control");
            return $"latched after {t:F1} s; {a.parts.Count} parts, {a.mass:F2} t, {a.CrewCount} crew, one fuel line";
        }

        static string DockingRefusals()
        {
            NoseToNose(out var a, out var b, 1.5, 0.3, 40f);
            double t = FlyUntilDocked(a, b, 10, out _, out _, out string n1);
            Check(double.IsNaN(t), "docked while 40 deg out of line");
            Check(n1 != null && n1.Contains("out of line"), $"no 'out of line' hint (got: {n1 ?? "nothing"})");
            NoseToNose(out a, out b, 1.5, 3.0);
            t = FlyUntilDocked(a, b, 3, out _, out _, out string n2);
            Check(double.IsNaN(t), "docked at 3 m/s");
            Check(n2 != null && n2.Contains("too fast"), $"no 'too fast' hint (got: {n2 ?? "nothing"})");
            NoseToNose(out a, out b, 1.5, 0.3, 0, "port.std");
            t = FlyUntilDocked(a, b, 10, out _, out _, out _);
            Check(double.IsNaN(t), "a small port latched onto a standard one");
            return $"refused: \"{n1}\"; \"{n2}\"; small vs standard: no latch";
        }

        static string UndockSplit()
        {
            NoseToNose(out var a, out var b, 1.0, 0.3);
            double massA = a.mass, massB = b.mass;
            int partsA = a.parts.Count, partsB = b.parts.Count, crewA = a.CrewCount, crewB = b.CrewCount;
            Check(!double.IsNaN(FlyUntilDocked(a, b, 20, out _, out _, out _)), "never latched");
            Vector3d p0 = a.vel * a.mass;
            var port = a.DockedPorts().First();
            var piece = a.Undock(port);
            Check(piece != null, "undock returned nothing");
            Check(piece.name == "Bravo" && !piece.isDebris, $"piece is \"{piece.name}\" (debris={piece.isDebris})");
            Check(a.parts.Count == partsA && piece.parts.Count == partsB, $"split {a.parts.Count}/{piece.parts.Count}, expected {partsA}/{partsB}");
            Check(a.CrewCount == crewA && piece.CrewCount == crewB, "crew went to the wrong side");
            Check(Math.Abs(a.mass - massA) < 1e-6 && Math.Abs(piece.mass - massB) < 1e-6, "masses don't match the originals");
            Vector3d p1 = a.vel * a.mass + piece.vel * piece.mass;
            Check((p1 - p0).magnitude < 1e-6, $"momentum changed by {(p1 - p0).magnitude:E2}");
            double sep = (piece.vel - a.vel).magnitude;
            Check(Math.Abs(sep - 0.3) < 0.01, $"separation speed {sep:F3} m/s");
            Check(a.HasControl && piece.HasControl, "a side lost control");
            Check(!a.DockedPorts().Any() && a.FreePorts().Any() && piece.FreePorts().Any(), "ports didn't come free");
            // The ports are still touching: they must not latch again until they've drifted apart.
            Check(!a.TryDock(piece, out _), "re-latched straight after undocking");
            Vector3d away = (piece.pos - a.pos).normalized;
            piece.pos = piece.pos + away * 3;
            Check(!a.TryDock(piece, out _), "latched from 3 m away");
            piece.pos = piece.pos - away * 3;
            piece.vel = a.vel;
            piece.angVel = a.angVel;
            Check(a.TryDock(piece, out _), "couldn't dock again after drifting apart and coming back");
            return $"split into {a.name} + {piece.name}, separating at {sep:F2} m/s; no instant re-latch; docks again after drifting 3 m apart and back";
        }

        static string DockedSaveLoad()
        {
            NoseToNose(out var a, out var b, 1.0, 0.3);
            Check(!double.IsNaN(FlyUntilDocked(a, b, 20, out _, out _, out _)), "never latched");
            var u = new Universe();
            u.Add(a);
            var u2 = new Universe();
            u2.LoadJson(u.ToJson(), System4);
            var c = u2.vessels[0];
            Check(c.parts.Count == a.parts.Count && c.CrewCount == a.CrewCount, "parts or crew lost in the save");
            var port = c.DockedPorts().FirstOrDefault();
            Check(port != null, "docking link lost in the save");
            var piece = c.Undock(port);
            Check(piece != null && piece.name == "Bravo", "couldn't undock after loading");
            return $"saved {a.parts.Count} parts with {a.CrewCount} crew; after loading it undocks into {c.name} + {piece.name}";
        }

        static string CrewTransfer()
        {
            var home = System4[0];
            var d = D("crewtest", "pod.wren", "hab.s", "tank.s1");
            var s = InOrbit(d, home, home.radius + 100_000, 0);
            var pod = s.parts.First(p => p.def.id == "pod.wren");
            var hab = s.parts.First(p => p.def.id == "hab.s");
            Check(pod.crew.Count == pod.def.crew && hab.crew.Count == hab.def.crew, $"seats not filled: pod {pod.crew.Count}, hab {hab.crew.Count}");
            Check(s.HasControl, "a crewed capsule should steer");
            string full = s.TransferCrew(pod, hab);
            Check(full.Contains("full"), $"moved into a full habitat: {full}");
            s.TransferCrew(hab, pod); // pod is full too (1 seat)
            string who = pod.crew[0];
            hab.crew.RemoveAt(0); // make room
            string msg = s.TransferCrew(pod, hab);
            Check(pod.crew.Count == 0 && hab.crew.Contains(who), $"transfer failed: {msg}");
            Check(!s.HasControl, "an empty capsule (no probe core) shouldn't steer");
            s.TransferCrew(hab, pod);
            Check(s.HasControl, "control didn't come back with a pilot aboard");
            return $"{who} moved to the habitat and back; names like \"{hab.crew[0]}\"";
        }

        static string JointOverload()
        {
            var home = System4[0];
            VesselSim Make(string port)
            {
                var v = InOrbit(D("strain", "probe.spark", "tank.s4", "tank.s4", port, "tank.s1", "eng.heron"), home, home.radius + 300_000, 0);
                v.ActivateNextStage();
                return v;
            }
            var weak = Make("port.sm");
            double t = 0;
            for (int i = 0; i < 50 && weak.pendingSpawns.Count == 0; i++) { weak.Step(0.02, t); t += 0.02; }
            Check(weak.pendingSpawns.Count == 1, "a 1000 kN push through a small docking port didn't tear it");
            string why = weak.lastEvent;
            var strong = Make("port.std");
            t = 0;
            for (int i = 0; i < 50; i++) { strong.Step(0.02, t); t += 0.02; }
            Check(strong.pendingSpawns.Count == 0, $"a standard port tore: {strong.lastEvent}");
            return why;
        }

        static string WingOverload()
        {
            var b = Body;
            var hard = KiteFree(b, 1000, 300, 20f, out _, out _);
            double t = 0;
            for (int i = 0; i < 10 && hard.pendingSpawns.Count == 0; i++) { hard.Step(0.02, t); t += 0.02; }
            Check(hard.pendingSpawns.Count > 0, "a wing at 20 deg and 300 m/s held on");
            string why = hard.lastEvent;
            Check(why.Contains("Wing"), $"something other than a wing tore: {why}");
            var easy = KiteFree(b, 1000, 300, 3f, out _, out _);
            t = 0;
            for (int i = 0; i < 50; i++) { easy.Step(0.02, t); t += 0.02; }
            Check(easy.pendingSpawns.Count == 0, $"tore at 3 deg: {easy.lastEvent}");
            return why;
        }

        static string FragilePanels()
        {
            var home = System4[0];
            VesselSim Sat(bool deployed)
            {
                var d = D("sat", "probe.spark", "tank.s1");
                DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "tank.s1"), 0.5f, 0f, "solar.s", 2, false);
                double alt = 10_000, rho = home.Density(alt), v = Math.Sqrt(2 * 3000 / rho);
                Vector3d up = new Vector3d(1, 0, 0), pos = up * (home.radius + alt);
                var s = VesselSim.CreateFree(d, home, pos, home.SurfaceVelocity(pos) + new Vector3d(0, 0, -v), Quaternion.FromToRotation(Vector3.up, new Vector3(0, 0, -1)));
                s.sas = SasMode.Off;
                if (deployed) foreach (var p in s.parts) if (p.def.IsDeployable) { p.legsDeployed = true; p.legDeploy = 1f; }
                return s;
            }
            var open = Sat(true);
            double t = 0;
            int torn = 0;
            for (int i = 0; i < 10; i++) { open.Step(0.02, t); t += 0.02; torn += open.pendingSpawns.Count; open.pendingSpawns.Clear(); }
            Check(torn == 2, $"{torn} panels tore off, expected both");
            var shut = Sat(false);
            t = 0;
            for (int i = 0; i < 10; i++) { shut.Step(0.02, t); t += 0.02; }
            Check(shut.pendingSpawns.Count == 0, "folded panels tore off");
            return $"deployed: both torn ({open.lastEvent}); folded: intact";
        }
    }
}
