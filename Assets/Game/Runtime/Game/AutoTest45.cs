using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>Milestone 45 (-autotest45): a capsule coming down under its new parachute, from two sides.</summary>
        IEnumerator M45Tour()
        {
            var root = GameRoot.I;
            yield return WaitPlanets();
            var u = root.universe;
            var home = root.bodies.First(b => b.name == "Arden");
            Vector3d up = LaunchSite.DirectionAt(home, 3000, 2000);
            Vector3d pos = up * (home.radius + 1500);
            var cap = VesselSim.CreateFree(VesselDesign.Stack("Chute test", "chute.s", "pod.wren", "shield.s"), home, pos, home.SurfaceVelocity(pos) - up * 8, Quaternion.FromToRotation(Vector3.up, up.ToFloat()));
            cap = Crewed(cap, u, "Iris Ardling");
            root.Fly(cap);
            yield return new WaitForSecondsRealtime(1f);
            var f = root.flight;
            foreach (var p in cap.parts.Where(p => p.def.kind == PartKind.Parachute)) { p.chuteDeployed = true; p.chuteOpen = 1f; }
            yield return new WaitForSecondsRealtime(2f);
            f.SetCamera(28f, 12f, 30f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("850_parachute_side");
            f.SetCamera(200f, -20f, 26f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("851_parachute_below");
            Log($"chute: {cap.parts.First(p => p.def.kind == PartKind.Parachute).chuteOpen:0.00} open, falling {cap.SurfaceRelVelocity.magnitude:0.0} m/s, destroyed {cap.destroyed}");
            Log("M45 DONE");
        }
    }
}
