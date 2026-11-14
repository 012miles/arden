using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 14: a Wrangler tug in orbit drifts its grabber into a class B wanderer and latches on, pushes it with
        /// the engine, takes a rock sample; then a wanderer passing through Arden's space in the map, the vessel list, and
        /// the Wrangler in the builder.
        /// </summary>
        IEnumerator WanderersTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var home = root.home;
            u.ut = TimeWithSun(home, CelestialBody.DirectionFromLatLon(0, 30), 0.6, 0);
            // A natural one, coming in from the edge of Arden's space
            var passing = Wanderers.Spawn(home, u.ut, new System.Random(5), 1, 1);
            u.Add(passing);
            u.wanderersSeen = 1;

            var tug = Fly(VesselDesign.WranglerTug(), home, 300_000, 0, 30, u.ut, true);
            var f = root.flight;
            tug.sas = SasMode.Stability;
            yield return new WaitForSecondsRealtime(0.6f);
            // A class B rock a few metres beyond the grabber, which closes on it at half a metre a second
            var grab = tug.parts.Find(p => p.def.kind == PartKind.Grabber);
            Vector3d up = tug.Axis;
            Vector3d face = tug.pos + Vector3d.Rotate(tug.rot, Vector3d.FromFloat(grab.localPos - tug.com));
            var rockDef = PartCatalog.Rocks[1];
            float R = rockDef.length * 0.5f;
            var rockStart = face + up * (R + 4.0);
            var rock = VesselSim.CreateFree(VesselDesign.Stack("Wanderer B-NT2", rockDef.id), home, rockStart - Vector3d.FromFloat(new Vector3(0, 0, 0)), tug.vel,
                Quaternion.Euler(20, 40, 10));
            // Put its ball's centre where we want it (its origin is the top of the ball)
            rock.pos += rockStart - (rock.pos + Vector3d.Rotate(rock.rot, Vector3d.FromFloat(rock.parts[0].Center - rock.com)));
            rock.name = "Wanderer B-NT2";
            rock.sas = SasMode.Off;
            rock.throttle = 0;
            u.Add(rock);
            tug.vel += up * 0.5;
            tug.rails = null;   // it was coasting on a cached orbit
            f.SetTarget(rock);
            f.SetCamera(115f, 8f, 26f);
            yield return new WaitForSecondsRealtime(2.0f);
            yield return Shot("210_tug_approach");
            Vector3d rockNow = rock.pos + Vector3d.Rotate(rock.rot, Vector3d.FromFloat(rock.parts[0].Center - rock.com));
            Log($"approach: gap {((rockNow - (tug.pos + Vector3d.Rotate(tug.rot, Vector3d.FromFloat(grab.localPos - tug.com)))).magnitude - R):0.0} m, closing {Vector3d.Dot(tug.vel - rock.vel, up):0.00} m/s, tug {tug.mass:0.0} t");

            yield return WaitFor(() => f.active.parts.Exists(p => p.def.kind == PartKind.Rock), 20, "the grab");
            bool got = f.active.parts.Exists(p => p.def.kind == PartKind.Rock);
            Log($"grabbed: {got}, mass now {f.active.mass:0.0} t, {f.active.parts.Count} parts, rock still separate: {u.vessels.Contains(rock)}");
            yield return new WaitForSecondsRealtime(1.0f);
            f.SetCamera(140f, 12f, 30f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("211_grabbed");

            // Push: light the Ember at a third throttle
            f.Stage();
            f.active.throttle = 0.35;
            yield return new WaitForSecondsRealtime(1.5f);
            f.SetCamera(200f, 6f, 34f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("212_pushing");
            f.active.throttle = 0;

            // A rock sample
            var g = f.active.parts.Find(p => p.def.kind == PartKind.Grabber);
            if (g != null) Log("sample: " + f.RunExperiment(g, Science.Get("rock")));
            f.ShowSciencePanel(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("213_rock_sample");
            f.ShowSciencePanel(false);

            // The passing wanderer's path through Arden's space
            f.SetTarget(passing);
            f.mapView = true;
            f.SetMapView(home, 1.2e8f, 20f, 60f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("214_wanderer_path");
            Log($"passing wanderer: {passing.name}, {passing.Situation(u.ut)}");
            f.mapView = false;
            f.ShowVesselList(true);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("215_vessel_list");
            f.ShowVesselList(false);

            root.currentDesign = VesselDesign.Wrangler();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("216_wrangler_builder");
        }
    }
}
