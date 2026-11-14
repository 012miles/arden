using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 10b: the Tumbler rover in the builder; driving and turning on the runway; driving across Tal's dark
        /// plains and Vesk's ice with the sun low; and mission control offering drive contracts.
        /// </summary>
        IEnumerator RoversTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var c = u.career;
            var tal = root.moon;
            var vesk = root.bodies.First(b => b.name == "Vesk");

            root.currentDesign = VesselDesign.Rover();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("150_rover_builder");

            // On the runway: off with the brakes, full ahead, then a turn
            u.ut = TimeWithSun(root.home, LaunchSite.Direction, 0.6, 0);
            root.Launch(VesselDesign.Rover(), true);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(1.0f);
            var v = f.active;
            v.brakesOn = false;
            f.driveOverride = new Vector2(0, 1);
            yield return new WaitForSecondsRealtime(5f);
            f.SetCamera(200f, 12f, 11f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("151_rover_runway");
            Log($"runway: {RoverSpeed(v):0.0} m/s, driven {v.driven:0} m, EC {v.EcStored:0}");
            f.driveOverride = new Vector2(1, 1);
            yield return new WaitForSecondsRealtime(1.5f);
            f.SetCamera(140f, 35f, 14f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("152_rover_turning");
            Log($"turning: {RoverSpeed(v):0.0} m/s, upright {Vector3d.Dot(Vector3d.Rotate(v.rot, new Vector3d(0, 1, 0)), v.pos.normalized):0.00}");
            f.driveOverride = null;

            // Tal: the dark plains, low sun
            yield return DriveOn(tal, "Dark Plains", "153_rover_tal", 0.3);
            // Vesk: the ice, low sun
            yield return DriveOn(vesk, "Ice Plains", "154_rover_vesk", 0.3);

            // Mission control with drive contracts on offer
            c.Start(GameMode.Career);
            foreach (var n in TechTree.Nodes) c.unlocked.Add(n.id);
            c.records |= Records.Launch | Records.Space | Records.Orbit | Records.TalFlyby | Records.TalOrbit | Records.TalLanding;
            c.contracts.Clear();
            c.RefreshOffers(u.ut);
            root.EnterHangar();
            root.hangar.ShowMissions(true);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("155_rover_contracts");
            Log($"offers: {string.Join(", ", c.Offered.Select(k => k.title))}");
        }

        static double RoverSpeed(VesselSim v)
        {
            Vector3d up = v.pos.normalized, s = v.SurfaceRelVelocity;
            return (s - up * Vector3d.Dot(s, up)).magnitude;
        }

        IEnumerator DriveOn(CelestialBody b, string region, string shot, double sunHeight)
        {
            var root = GameRoot.I;
            var u = root.universe;
            var f = root.flight;
            var spot = FindRegionSpot(b, region, d => Math.Abs(Terrain.Height(b, d) - Terrain.Height(b, (d + CelestialBody.EastAt(d) * (80 / b.radius)).normalized)) < 4);
            u.ut = TimeWithSun(b, spot, sunHeight, u.ut);
            var rover = VesselSim.CreateLanded(VesselDesign.Rover(), b, spot, u.ut);
            rover.name = $"Tumbler ({b.name})";
            u.Add(rover);
            root.Fly(rover);
            yield return new WaitForSecondsRealtime(1.0f);
            rover.brakesOn = false;
            f.driveOverride = new Vector2(0, 1);
            yield return new WaitForSecondsRealtime(7f);
            f.driveOverride = new Vector2(0.4f, 1);
            yield return new WaitForSecondsRealtime(1f);
            Vector3d up = rover.pos.normalized;
            Vector3d sunFlat = Sun.DirectionAt(GameRoot.I.universe.ut) - up * Vector3d.Dot(Sun.DirectionAt(GameRoot.I.universe.ut), up);
            f.LookAlong(sunFlat.normalized, 12f, 150f, 14f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(shot);
            Log($"{b.name}: {RoverSpeed(rover):0.0} m/s, driven {rover.driven:0} m on {rover.drivenOn}, region {Regions.Under(rover, f.ut)}, destroyed {rover.destroyed}");
            f.driveOverride = null;
        }
    }
}
