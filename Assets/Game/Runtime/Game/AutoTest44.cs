using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 44 (-autotest44): the builder's Mission Review (the NASA Space Apps resources) on two designs, then a
        /// plane's new cockpit on the runway, looking ahead through the windscreen and back at the door.
        /// </summary>
        IEnumerator M44Tour()
        {
            var root = GameRoot.I;
            yield return WaitPlanets();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.5f);
            var h = root.hangar;
            foreach (var (name, d) in new[] { ("orbiter", Presets.Orbiter()), ("tallander", Presets.TalLander()), ("plane", VesselDesign.Kite()) })
            {
                h.SetDesignFromNet(d);
                h.TestShowReview(true);
                yield return new WaitForSecondsRealtime(1.2f);
                var rv = h.TestReview;
                Log($"review {name}: {rv.cmlName}; red: {string.Join(", ", rv.segments.Concat(rv.disciplines).Where(l => !l.ok).Select(l => l.area))}; like: {rv.realMissions.FirstOrDefault()}");
                yield return Shot($"840_review_{name}");
            }
            h.TestShowReview(false);
            // the plane's cockpit, on the runway
            var u = root.universe;
            var home = root.bodies.First(b => b.name == "Arden");
            var plane = Crewed(VesselSim.CreateOnRunway(VesselDesign.Kite(), home, u.ut), u, "Iris Ardling");
            root.Fly(plane);
            yield return new WaitForSecondsRealtime(2f);
            var f = root.flight;
            f.EnterInterior();
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"cockpit: inside={f.inside} plane={f.TestPlaneRoom} {f.TestCabinShape}; moving {f.TestCabinMove}; flip {f.TestFlip:0.00}");
            f.TestCabinLook(0, -8);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("841_cockpit_ahead");
            f.TestCabinLook(180, 0);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("842_cockpit_back");
            f.TestCabinLook(-70, 5);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("843_cockpit_side");
            Log($"after looking round: {f.TestCabinMove}; flip {f.TestFlip:0.00}");
            f.LeaveInterior();
            Log("M44 DONE");
        }
    }
}
