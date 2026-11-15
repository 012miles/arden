using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>The My Ardling window with every hat (and a spread of suits, faces and note colours), then that look out on foot.</summary>
        IEnumerator ArdlingTour()
        {
            var root = GameRoot.I;
            root.ShowMenu();
            yield return new WaitForSecondsRealtime(1.5f);
            string was = MyArdling.Code;
            for (int i = 0; i < Ardlings.Hats; i++)
            {
                MyArdling.Code = Ardlings.Code(i % Ardlings.SuitCount, i % Ardlings.PaperCount, i % Ardlings.Faces, i);
                MyArdling.Show();
                yield return new WaitForSecondsRealtime(i == 0 ? 1.2f : 0.6f);
                yield return Shot($"790_ardling_hat_{i}_{Ardlings.HatNames[i].Replace(' ', '_')}");
                MyArdling.Open = false;
            }
            // out on foot, wearing it
            MyArdling.Code = Ardlings.Code(3, 2, 4, 6);
            var u = root.universe;
            var tal = root.bodies.First(b => b.name == "Tal");
            var lander = Crewed(VesselSim.CreateLanded(Presets.TalLander(), tal, CelestialBody.DirectionFromLatLon(1, 33), u.ut), u, "Iris Ardling");
            root.Fly(lander);
            yield return new WaitForSecondsRealtime(2f);
            var f = root.flight;
            f.GoEva(lander.parts.First(p => p.crew.Count > 0));
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("799_on_foot");
            // milestone 41: lost while inside the cabin: out of it, the pointer free, the death screen up
            Log($"boarded: {f.BoardNearest()}");
            yield return new WaitForSecondsRealtime(1.5f);
            var ship = f.active;
            f.EnterInterior();
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"inside before the crash: {f.inside}");
            ship.Destroy("Hit the ground at 41 m/s (Tal lander can take 12 m/s)");
            yield return new WaitForSecondsRealtime(2.6f);
            Log($"after the crash: inside={f.inside} cursor={Cursor.lockState} death screen={f.DeathScreenTitle}");
            yield return Shot("798_death_screen");
            // milestone 41: water: a capsule bobbing upright, and a tall rocket that's toppled over beside it
            var home = root.bodies.First(b => b.name == "Arden");
            var sea = CelestialBody.DirectionFromLatLon(5, 30);
            var tall = u.Add(VesselSim.CreateLanded(VesselDesign.Stack("Toppled", "pod.wren", "tank.s2", "tank.s2", "tank.s2", "eng.kestrel"), home, (sea + CelestialBody.EastAt(sea) * (14.0 / home.radius)).normalized, u.ut));
            var bob = Crewed(VesselSim.CreateLanded(VesselDesign.Stack("Bobber", "chute.s", "pod.wren", "shield.s"), home, sea, u.ut), u, "Otto Ardling");
            root.Fly(bob);
            yield return new WaitForSecondsRealtime(12f);
            Log($"floating: capsule {bob.submerged:P0} under, upright {Vector3d.Dot(bob.Axis, bob.pos.normalized):0.00}; tall rocket upright {Vector3d.Dot(tall.Axis, tall.pos.normalized):0.00}, in water {tall.InWater}");
            yield return Shot("797_floating");
            // milestone 41: the space centre: no labels, the building under the pointer glows with its name
            root.ShowMenu();
            root.menu.OpenCentre();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("796_centre");
            MenuMode.ForceHover = "Assembly building";
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("796_centre_hover");
            MenuMode.ForceHover = null;
            MyArdling.Code = was;
            Log("ARDLING TOUR DONE");
        }
    }
}
