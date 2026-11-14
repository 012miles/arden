using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 35 (captaincarl24: "make easy mode even more simpler, not the text but mechanics as well"): on Easy the
    /// game lends a hand. Parts are half as tough again (see PartState.Toughness). A stage that runs dry with engines
    /// next fires the next one by itself; parachutes open when it's safe; landing legs come down near the ground; and
    /// SAS starts switched on. Each helper says what it did.
    /// </summary>
    public partial class FlightMode
    {
        float easyT;
        bool easyChutesSaid, easyLegsSaid;

        void EasyHelpers(VesselSim v, float dt)
        {
            if (!Words.Easy || v == null || v.destroyed || v.IsEva || remote || replay != null || paused) return;
            if (Coop.Active && !Coop.Current.IsHost) return;
            easyT -= dt;
            if (easyT > 0) return;
            easyT = 0.25f;
            if (!v.hasLaunched) { easyChutesSaid = easyLegsSaid = false; return; }
            // 1) a stage that's run dry, with engines in the next one: fire it (never the parachutes stage)
            if (v.throttle > 0.05 && v.nextStage < v.stages.Count && v.thrustNow < 1e-3 && v.ActiveFuelFraction() <= 0.001)
            {
                var st = v.stages[v.nextStage];
                if (st.ignite.Count > 0 && st.chutes.Count == 0)
                {
                    Stage();
                    Show("Easy: that stage ran out of fuel, so the next one fired by itself.", 3f);
                    return;
                }
            }
            double alt = v.RadarAltitude(ut);
            double down = -Vector3d.Dot(v.SurfaceRelVelocity, v.pos.normalized);
            // 2) parachutes once they won't tear (slow enough, in the air, on the way down)
            if (v.body.HasAtmosphere && down > 3 && alt < 5000 && v.SurfaceRelVelocity.magnitude < 250 && v.Altitude < v.body.atmosphereHeight * 0.6)
            {
                bool any = false;
                foreach (var p in v.parts)
                    if (p.def.kind == PartKind.Parachute && !p.chuteDeployed && !p.chuteFailed) { p.chuteDeployed = true; any = true; }
                if (any && !easyChutesSaid) { easyChutesSaid = true; Show("Easy: parachutes open.", 2.5f); audio?.Clunk(0.4f); }
            }
            // 3) legs down near the ground
            if (!v.gearDown && down > 0.5 && alt < 250 && v.parts.Any(p => (p.def.kind == PartKind.Leg || p.def.kind == PartKind.Gear) && !p.broken))
            {
                FireGroup(Groups.Gear);
                if (!easyLegsSaid) { easyLegsSaid = true; Show("Easy: legs down for landing.", 2.5f); }
            }
        }
    }
}
