using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 42: the busy title screen and building picker (a launch from another pad), a Shiftling passing as crew and
        /// then turned, a Lurker, the Hollow with its health bar and a slam, and an Ardling holding and firing a blaster.
        /// </summary>
        IEnumerator MutantsTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            // the title screen, busy
            root.ShowMenu();
            yield return new WaitForSecondsRealtime(1.5f);
            root.menu.TestLaunchNow();
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("800_title_liftoff");
            yield return new WaitForSecondsRealtime(7f);
            Log($"title: launches in the air {root.menu.AmbientLaunches}");
            yield return Shot("801_title_climbing");
            root.menu.OpenCentre();
            root.menu.TestLaunchNow();
            yield return new WaitForSecondsRealtime(9f);
            yield return Shot("802_centre_launch");

            // out on Tal, away from the outposts
            var tal = root.bodies.First(b => b.name == "Tal");
            var lander = Crewed(VesselSim.CreateLanded(Presets.TalLander(), tal, CelestialBody.DirectionFromLatLon(1, 33), u.ut), u, "Iris Ardling");
            u.career.backpack.Clear();
            u.career.backpack.AddRange(new[] { "medkit", "blaster", "wrench" });
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(2f);
            var f = root.flight;
            f.GoEva(lander.parts.First(p => p.crew.Count > 0));
            yield return new WaitForSecondsRealtime(1.5f);
            // a Shiftling passing as crew, 9 m off
            f.TestSpawn(DroneKind.Shiftling, 9f, Ardlings.Code(3, 1, 2, 4));
            yield return new WaitForSecondsRealtime(2f);
            var s = f.TestNearestDrone();
            Log($"shiftling: {s?.state} at {(s != null ? (s.Pos - f.FixedPosOf(f.active)).magnitude : -1):0.0} m");
            yield return Shot("803_shiftling_disguised");
            // closer: it turns
            if (s != null) { s.Pos = f.FixedPosOf(f.active) + (s.Pos - f.FixedPosOf(f.active)).normalized * 3.5; s.Home = s.Pos; }
            yield return new WaitForSecondsRealtime(1.6f);
            Log($"shiftling close: {s?.state}");
            yield return Shot("804_shiftling_turned");
            f.TestAttack(s.uid); f.TestAttack(s.uid); f.TestAttack(s.uid); f.TestAttack(s.uid); f.TestAttack(s.uid);
            yield return new WaitForSecondsRealtime(1f);
            // a Lurker
            f.TestSpawn(DroneKind.Lurker, 7f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("805_lurker");
            foreach (var d in f.TestDrones().Where(x => x.kind == DroneKind.Lurker).ToList()) { f.TestAttack(d.uid); f.TestAttack(d.uid); f.TestAttack(d.uid); }
            yield return new WaitForSecondsRealtime(1f);
            // the Hollow
            f.TestSpawn(DroneKind.Hollow, 22f);
            yield return new WaitForSecondsRealtime(4f);
            var boss = f.TestBoss();
            Log($"hollow: {boss?.state} health {boss?.health:0}/{(boss != null ? Drones.MaxHealth(boss) : 0):0}");
            yield return Shot("806_hollow");
            yield return new WaitForSecondsRealtime(4f);
            Log($"hollow slams {boss?.slams}");
            yield return Shot("807_hollow_slam");
            // the blaster in hand, firing at it
            f.TestHold("blaster");
            yield return new WaitForSecondsRealtime(1f);
            if (boss != null) f.TestAttack(boss.uid);
            yield return null;
            yield return Shot("808_blaster");
            Log($"held: {f.TestHeld()}");
            f.TestClearDrones();   // (the Hollow goes, so the rest of the tour is calm)
            // a drone blown up: the new blast (a flash of light, a shockwave, the fireball, debris on smoke trails, embers)
            f.TestSpawnDrone(DroneKind.Scout, 9f);
            yield return new WaitForSecondsRealtime(0.5f);
            var scout = f.TestDrones().FirstOrDefault(x => x.kind == DroneKind.Scout);
            if (scout != null)
            {
                // in view: between her and the lander
                var me = f.FixedPosOf(f.active);
                var mid = me + (f.FixedPosOf(lander) - me) * 0.45;
                scout.Pos = mid.normalized * (mid.magnitude + 1.6); scout.Home = scout.Pos;
            }
            if (scout != null) { f.TestAttack(scout.uid); f.TestAttack(scout.uid); f.TestAttack(scout.uid); }
            yield return new WaitForSecondsRealtime(0.12f);
            {
                var blastGo = GameObject.Find("Blast");
                var blast = blastGo != null ? blastGo.transform : null;
                var cam = root.farCam;
                if (blast == null) Log("blast: none under the anchor");
                else
                {
                    string counts = string.Join(", ", blast.GetComponentsInChildren<ParticleSystem>(true).Select(ps => $"{ps.name} {ps.particleCount}"));
                    var vp = cam.WorldToViewportPoint(blast.position);
                    Log($"blast: at {blast.position} (camera {cam.transform.position}, viewport {vp}), active {blast.gameObject.activeInHierarchy}, layer {blast.gameObject.layer}, camera mask {cam.cullingMask}, near mask {root.nearCam.cullingMask}; {counts}");
                }
            }
            yield return Shot("809_blast_flash");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Shot("809_blast_fire");
            // the new things in the cabin
            f.BoardNearest();
            yield return new WaitForSecondsRealtime(1.2f);
            f.EnterInterior();
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var (yaw, pitch, name) in new[] { (30f, 8f, "snacks"), (120f, 8f, "drink"), (295f, 5f, "guitar_poster"), (200f, -12f, "photo_toolbox"), (0f, -25f, "desk_toys") })
            {
                f.TestCabinLook(yaw, pitch);
                yield return new WaitForSecondsRealtime(0.7f);
                yield return Shot($"810_cabin_{name}");
            }
            f.LeaveInterior();
            Log("MUTANTS TOUR DONE");
        }
    }
}
