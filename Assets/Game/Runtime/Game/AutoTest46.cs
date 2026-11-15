using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 46 (-autotest46): the new planes in the builder and on the runway, then "Fly home" all the way from the
        /// ground on Tal: the climb, rounding off the orbit, the burn home, leaving Tal and falling back into Arden's air.
        /// </summary>
        IEnumerator M46Tour()
        {
            var root = GameRoot.I;
            yield return WaitPlanets();
            var was = Words.Level;
            Words.Level = WordLevel.Easy;
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.5f);
            var h = root.hangar;
            h.SetDesignFromNet(Presets.JumboJet());
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("860_builder_jumbo");
            h.SetDesignFromNet(Presets.SpaceShuttle());
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("861_builder_shuttle");
            var u = root.universe;
            var home = root.bodies.First(b => b.name == "Arden");
            foreach (var (name, d) in new[] { ("jumbo", Presets.JumboJet()), ("supersonic", Presets.SupersonicAirliner()), ("twin", Presets.TwinAirliner()) })
            {
                var plane = Crewed(VesselSim.CreateOnRunway(d, home, u.ut), u, "Iris Ardling");
                plane.hasLaunched = false;   // (parked, waiting for LAUNCH, like a player's: Crewed marks it launched)
                plane.throttle = 0;
                root.Fly(plane);
                yield return new WaitForSecondsRealtime(2f);
                root.flight.SetCamera(140f, 10f, 70f);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot($"862_runway_{name}");
                Log($"{name}: on the runway, landed {plane.landed}, destroyed {plane.destroyed}, moving {plane.SurfaceRelVelocity.magnitude:0.0} m/s, {plane.parts.Sum(p => p.crew.Count)} aboard of {plane.parts.Sum(p => p.def.crew)} seats");
                u.Remove(plane);   // (the next one goes on the same spot)
            }
            // fly home from the ground on Tal
            var tal = root.bodies.First(b => b.name == "Tal");
            var lander = Crewed(VesselSim.CreateLanded(Presets.TalLander(), tal, CelestialBody.DirectionFromLatLon(1, 33), u.ut), u, "Iris Ardling");
            lander.hasLaunched = true;
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(2f);
            var f = root.flight;
            f.StartReturn();
            float t0 = Time.realtimeSinceStartup, lastLog = -99;
            var lastPhase = f.trip;
            Log($"trip: {f.trip}");
            while (Time.realtimeSinceStartup - t0 < 900 && f.trip != FlightMode.TripPhase.Off && f.active != null && !f.active.destroyed)
            {
                if (f.trip != lastPhase || Time.realtimeSinceStartup - lastLog > 15)
                {
                    Log($"{f.trip}: {f.tripStatus} ({f.active.body.name}, {f.active.Altitude / 1000:0.0} km)");
                    lastPhase = f.trip; lastLog = Time.realtimeSinceStartup;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            var a = f.active;
            Log($"home trip ended: {f.trip}; at {a?.body.name}, {a?.Altitude / 1000:0.0} km, destroyed {a?.destroyed}");
            f.SetCamera(160f, 8f, 30f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("864_back_home");
            Words.Level = was;
            Log("M46 DONE");
        }
    }
}
