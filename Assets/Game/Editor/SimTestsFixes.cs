using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Bug-fix tests: ships collide, side-mounted docking ports.</summary>
    public static partial class SimTests
    {
        static void RunFixes()
        {
            Run("Collisions: two ships drifting together bounce apart (momentum kept), instead of passing through", CollideBounce);
            Run("Collisions: a hard hit wrecks the ships, as on the ground", CollideCrash);
            Run("Collisions: docking ports still latch, a stage separation isn't a crash, the grabber still bites", CollideExceptions);
            Run("Side ports: a docking port on a ship's side faces outward, and another ship docks to it from the side", SidePortDocking);
            Run("Rename: the old Rocket Game folder's saves and designs are copied into Arden Space Program's, once", RenameCopiesSaves);
        }

        static string RenameCopiesSaves()
        {
            string root = TempRoot(), oldDir = System.IO.Path.Combine(root, SaveSlots.OldProductName), newDir = System.IO.Path.Combine(root, SaveSlots.ProductName);
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(oldDir, "saves", "game1"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(oldDir, "Designs"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(oldDir, "saves", "game1", "universe.json"), "{}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(oldDir, "Designs", "Rocket.json"), "{}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(oldDir, "Player.log"), "log");
            int n = SaveSlots.CopyFromOldFolder(newDir, oldDir);
            Check(n == 2, $"copied {n} files (expected the save and the design, not the log)");
            Check(System.IO.File.Exists(System.IO.Path.Combine(newDir, "saves", "game1", "universe.json")) && System.IO.File.Exists(System.IO.Path.Combine(newDir, "Designs", "Rocket.json")), "files missing in the new folder");
            Check(System.IO.File.Exists(System.IO.Path.Combine(oldDir, "saves", "game1", "universe.json")), "the old folder wasn't kept as a backup");
            Check(SaveSlots.CopyFromOldFolder(newDir, oldDir) == 0, "copied a second time over the new game's own files");
            return "save + design copied, log left behind, old folder kept, only once";
        }

        /// <summary>Two vessels in a 300 km orbit, 'gap' metres apart along the track, closing at 'closing' m/s.</summary>
        static (VesselSim a, VesselSim b) PairInOrbit(VesselDesign da, VesselDesign db, double gap, double closing, Quaternion ra, Quaternion rb)
        {
            var home = Home14;
            double r = home.radius + 300_000, sp = Math.Sqrt(home.mu / r);
            var pos = new Vector3d(r, 0, 0);
            var vel = new Vector3d(0, 0, -sp);
            var along = new Vector3d(0, 0, -1);
            var a = VesselSim.CreateFree(da, home, pos, vel + along * (closing / 2), ra);
            var b = VesselSim.CreateFree(db, home, pos + along * gap, vel - along * (closing / 2), rb);
            a.throttle = b.throttle = 0; a.sas = b.sas = SasMode.Off;
            return (a, b);
        }

        static string CollideBounce()
        {
            var side = Quaternion.LookRotation(Vector3.up, Vector3.right);   // lying across the track, so they meet side to side
            var (a, b) = PairInOrbit(D("a", "probe.spark", "tank.s2"), D("b", "probe.spark", "tank.s2"), 3.0, 2.0, side, side);
            double m = a.mass + b.mass;
            Vector3d p0 = a.vel * a.mass + b.vel * b.mass;
            double rel0 = Vector3d.Dot(a.vel - b.vel, new Vector3d(0, 0, -1));
            double ut = 0, worst = 0;
            int contacts = 0;
            for (int i = 0; i < 400; i++)
            {
                a.Step(0.02, ut); b.Step(0.02, ut); ut += 0.02;
                if (Collisions.Resolve(a, b, out _)) contacts++;
                worst = Math.Max(worst, Collisions.Overlapping(a, b) ? 1 : 0);
            }
            Vector3d p1 = a.vel * a.mass + b.vel * b.mass;
            double rel1 = Vector3d.Dot(a.vel - b.vel, new Vector3d(0, 0, -1));
            Check(contacts > 0, "they never touched (passed through?)");
            Check(!a.destroyed && !b.destroyed, "a gentle bump wrecked them");
            Check(rel1 < 0, $"still closing at {rel1:0.00} m/s after the bump");
            // Gravity acts on both equally over 8 s; compare the change in momentum with the bump alone
            Check(Math.Abs(-rel1 / rel0 - Collisions.Bounce) < 0.15, $"bounced back at {-rel1:0.00} m/s from {rel0:0.00} (expected about {Collisions.Bounce * rel0:0.00})");
            Check(Collisions.Overlapping(a, b) == false || (a.pos - b.pos).magnitude > 1, "still inside each other at the end");
            return $"met at {rel0:0.0} m/s, bounced back at {-rel1:0.00} m/s after {contacts} contact steps; nobody hurt";
        }

        static string CollideCrash()
        {
            var side = Quaternion.LookRotation(Vector3.up, Vector3.right);
            var (a, b) = PairInOrbit(D("a", "probe.spark", "tank.s2"), D("b", "probe.spark", "tank.s2"), 5.0, 40.0, side, side);
            double ut = 0;
            string crash = null;
            for (int i = 0; i < 100 && crash == null; i++) { a.Step(0.02, ut); b.Step(0.02, ut); ut += 0.02; Collisions.Resolve(a, b, out crash); }
            Check(crash != null, "a 40 m/s head-on hit didn't wreck anything");
            Check((a.destroyed && a.destroyReason.Contains("Crashed into")) || (b.destroyed && b.destroyReason.Contains("Crashed into")), "wrong reason: " + a.destroyReason + " / " + b.destroyReason);
            return $"{crash} wrecked: {(a.destroyed ? a.destroyReason : b.destroyReason)}";
        }

        static string CollideExceptions()
        {
            // Docking: two ferries nose to nose at 0.6 m/s
            var axisA = Quaternion.FromToRotation(Vector3.up, Vector3.back);     // a's nose points along the track (-z is 'back' = (0,0,-1))
            var axisB = Quaternion.FromToRotation(Vector3.up, Vector3.forward);  // b faces back at a
            var fa = VesselDesign.FerryCraft("A"); var fb = VesselDesign.FerryCraft("B");
            var (a, b) = PairInOrbit(fa, fb, 0, 0.6, axisA, axisB);
            // Put b's port face 1.5 m in front of a's
            var pa = a.parts.Find(p => p.def.kind == PartKind.DockingPort); var pb = b.parts.Find(p => p.def.kind == PartKind.DockingPort);
            a.PortFaceWorld(pa, out var wa, out var na);
            b.PortFaceWorld(pb, out var wb, out _);
            b.pos += (wa + na * 1.5) - wb;
            double ut = 0;
            bool docked = false;
            for (int i = 0; i < 600 && !docked; i++)
            {
                a.Step(0.02, ut); b.Step(0.02, ut); ut += 0.02;
                Collisions.Resolve(a, b, out _);
                docked = a.TryDock(b, out _);
            }
            Check(docked, "the ports never latched (did the collision push them apart?)");

            // Staging: the spent stage starts touching the ship, and nothing gets shoved
            var ship = VesselSim.CreateFree(D("s", "probe.spark", "tank.s1", "eng.ember", "dec.s", "tank.s2", "eng.kestrel"), Home14,
                new Vector3d(Home14.radius + 300_000, 0, 0), new Vector3d(0, 0, -2300), Quaternion.identity);
            ship.ActivateNextStage();   // kestrel
            var pieces = ship.ActivateNextStage();   // decouple
            var debris = pieces.FirstOrDefault();
            Check(debris != null && debris.ghostWith == ship, "the dropped stage isn't marked as just separated");
            Vector3d v0 = ship.vel, d0 = debris.vel;
            bool hit = Collisions.Resolve(ship, debris, out string crash);
            Check(!hit && crash == null && (ship.vel - v0).magnitude < 1e-9 && (debris.vel - d0).magnitude < 1e-9, "separating stages bumped each other");
            for (int i = 0; i < 200; i++) { ship.Step(0.02, i * 0.02); debris.Step(0.02, i * 0.02); Collisions.Resolve(ship, debris, out _); }
            Check(debris.ghostWith == null, "the separation mark never cleared once they were apart");

            // The grabber still bites
            var (tug, rock) = GrabSetup(0, 1.2, 0.5);
            bool got = false;
            for (int i = 0; i < 400 && !got; i++) { tug.Step(0.02, i * 0.02); rock.Step(0.02, i * 0.02); Collisions.Resolve(tug, rock, out _); got = tug.TryGrab(rock, out _); }
            Check(got, "the grabber couldn't latch with collisions on");
            return "ferries docked nose to nose; a stage dropped cleanly and stopped being 'just separated' once clear; the grabber latched";
        }

        static string SidePortDocking()
        {
            // A station with a small port on the side of its tank, and a ferry with a nose port
            var sd = VesselDesign.Stack("Station", "probe.spark", "tank.s4", "eng.ember");
            int tank = sd.nodes.FindIndex(n => n.id == "tank.s4");
            DesignOps.AttachSurfaceAt(sd, tank, 0.5f, 0f, "port.sm", 1, false);
            var station = VesselSim.CreateFree(sd, Home14, new Vector3d(Home14.radius + 300_000, 0, 0), new Vector3d(0, 0, -2300), Quaternion.identity);
            var sp = station.parts.Find(p => p.def.kind == PartKind.DockingPort);
            Check(station.PortFaceWorld(sp, out var face, out var nrm), "the side port has no free face");
            double sideways = Math.Abs(Vector3d.Dot(nrm, station.Axis));
            Vector3d outward = face - station.pos; outward -= station.Axis * Vector3d.Dot(outward, station.Axis);
            Check(sideways < 0.05, $"the side port faces {Math.Acos(Math.Min(1, sideways)) * 180 / Math.PI:0} deg from the ship's axis, not straight out");
            Check(Vector3d.Dot(nrm, outward.normalized) > 0.95, "the side port faces into the hull");
            // A ferry comes in along the port's normal, nose first
            var ferry = VesselSim.CreateFree(VesselDesign.FerryCraft("Ferry"), Home14, face + nrm * 10, station.vel - nrm * 0.5,
                Quaternion.FromToRotation(Vector3.up, (-nrm).ToFloat()));
            var fp = ferry.parts.Find(p => p.def.kind == PartKind.DockingPort);
            ferry.PortFaceWorld(fp, out var ff, out _);
            ferry.pos += (face + nrm * 2.0) - ff;
            ferry.sas = SasMode.Off; ferry.throttle = 0; station.throttle = 0;
            double ut = 0;
            bool docked = false;
            string note = null;
            for (int i = 0; i < 800 && !docked; i++)
            {
                ferry.Step(0.02, ut); station.Step(0.02, ut); ut += 0.02;
                Collisions.Resolve(ferry, station, out _);
                docked = ferry.TryDock(station, out note) || docked;
            }
            Check(docked, "the ferry couldn't dock to the side port: " + note);
            return $"side port faces straight out ({Math.Asin(Math.Min(1, sideways)) * 180 / Math.PI:0.0} deg off square); a ferry docked to it from the side";
        }
    }
}
