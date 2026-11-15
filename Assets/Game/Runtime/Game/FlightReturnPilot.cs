using System;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 46: "Fly home" on Easy (the trip autopilot used to go out to a moon only). From the ground on Tal or Vesk it
    /// climbs straight up, tips over toward the east and burns until the orbit's top is high enough, coasts up, and rounds the
    /// orbit off; from orbit it goes straight to planning. It then plans the burn home (ReturnPlanner), burns, warps out of
    /// the moon's gravity and down to the air, points the heat shield forward and leaves the rest to the parachutes.
    /// X hands control back at any point.
    /// </summary>
    public partial class FlightMode
    {
        /// <summary>Starts the trip home from a moon of the home world (landed on it or in orbit round it).</summary>
        bool correcting;   // the small burn on the way home that puts the lowest point where the parachutes want it

        public void StartReturn()
        {
            correcting = false;
            var v = active;
            if (v == null || v.body == null || v.body.parent != Home) return;
            tripTarget = Home; tripLand = true; braking = false;
            ClearNodes();
            bool ground = OnTheGround(v);
            trip = ground ? TripPhase.Ascend : TripPhase.ReturnPlan;
            Show($"Autopilot: flying home to {Home.name}{(ground ? $" (climbing off {v.body.name} first)" : "")}. X takes back control.", 4f);
        }

        /// <summary>
        /// Still on the ground, or not really in orbit: landed, settling onto its legs, or on a path whose low point is under
        /// the surface (the tour found a lander still settling after touchdown counted as flying, and planned a burn from the ground).
        /// </summary>
        bool OnTheGround(VesselSim v)
        {
            if (v.landed || v.settling) return true;
            var o = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
            return !o.IsElliptic ? false : o.Periapsis < v.body.radius + MoonParkingAlt(v.body) * 0.4;
        }

        /// <summary>The lit engines have run dry and there's another stage with fuel to light: stage (a second apart at most).</summary>
        void StageIfBurntOut(VesselSim v)
        {
            if (tripT < 1f) return;
            bool lit = v.parts.Exists(p => p.ignited && p.def.IsEngine && v.EngineHasFuel(p));
            if (!lit && v.PotentialThrust() > 0.01) { Stage(); tripT = 0; }
        }

        /// <summary>How high to put the orbit round a moon before heading home (clear of its mountains).</summary>
        static double MoonParkingAlt(CelestialBody moon) => 15_000 + moon.radius * 0.03;

        void ReturnTick(VesselSim v)
        {
            switch (trip)
            {
                case TripPhase.Ascend:
                {
                    // one continuous climb into orbit: straight up, tip over toward the east, and once the orbit's top is high
                    // enough, burn along the horizon (nose up or down to hold the height) until its low point is clear too
                    var o = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
                    double park = MoonParkingAlt(v.body), minPe = v.body.radius + park * 0.6;
                    if (!v.landed && !v.settling && v.hasLaunched && o.IsElliptic && o.Periapsis >= minPe)
                    {
                        v.throttle = 0;
                        v.steerDirection = null;
                        trip = TripPhase.ReturnPlan;
                        tripStatus = $"In orbit round {v.body.name}";
                        return;
                    }
                    StageIfBurntOut(v);
                    Vector3d up = v.pos.normalized, east = CelestialBody.EastAt(up);
                    double alt = v.RadarAltitude(ut), r = v.pos.magnitude, g = v.body.mu / (r * r);
                    double amax = v.PotentialThrust() / Math.Max(1e-3, v.mass);
                    // a moon's gravity is weak: an Arden booster at full power would fling the ship off it, so hold the push
                    // to a couple of times the moon's pull (and never let the path stop being an orbit)
                    double Throttle(double want) => amax > 1e-6 ? Math.Max(0.02, Math.Min(1, want / amax)) : 1;
                    if (!v.landed && !v.settling && v.hasLaunched && !o.IsElliptic)
                    {
                        v.steerDirection = -v.vel.normalized;
                        bool back = Vector3d.Angle(Vector3d.Rotate(v.rot, Vector3d.up), -v.vel.normalized) < 15;
                        v.throttle = back ? Throttle(Math.Max(3 * g, 4)) : 0;
                        tripStatus = "Too fast: slowing back into orbit";
                        return;
                    }
                    if (!o.IsElliptic || o.Apoapsis < v.body.radius + park * 0.8)
                    {
                        // straight up off the ground, then over to 45 degrees toward the east
                        double tilt = alt < 600 ? 0 : 45 * Math.PI / 180;
                        v.steerDirection = up * Math.Cos(tilt) + east * Math.Sin(tilt);
                        v.throttle = Throttle(2.5 * g);
                        tripStatus = $"Climbing off {v.body.name}: {alt / 1000:0.0} km";
                    }
                    else
                    {
                        Vector3d flat = v.vel - up * Vector3d.Dot(v.vel, up);
                        Vector3d along = flat.magnitude > 1 ? flat.normalized : east;
                        double vs = Vector3d.Dot(v.vel, up), height = r - v.body.radius;
                        double pitch = Math.Max(-25, Math.Min(35, (park - height) / 4000 * 15 - vs * 0.6)) * Math.PI / 180;
                        v.steerDirection = along * Math.Cos(pitch) + up * Math.Sin(pitch);
                        v.throttle = o.Apoapsis > v.body.radius + park * 4 ? 0 : Throttle(Math.Max(4 * g, 10));
                        tripStatus = $"Burning into orbit: low point {(o.Periapsis - v.body.radius) / 1000:0} km";
                    }
                    return;
                }
                case TripPhase.Circularize:
                    if (node != null || autoBurn) { tripStatus = node != null && node.ut > ut ? $"Rounding off the orbit in {Ui.Time(node.ut - ut)}" : "Rounding off the orbit"; return; }
                    trip = TripPhase.ReturnPlan;
                    return;
                case TripPhase.ReturnPlan:
                {
                    if (OnTheGround(v)) { trip = TripPhase.Ascend; return; }
                    if (!ReturnPlanner.Plan(v.body, v.pos, v.vel, ut, out double tb, out double dv, out double pe))
                    { StopTrip($"Couldn't find a way home from here. Get into a round orbit round {v.body.name} and try again."); return; }
                    AddNode(tb, dv, 0, 0);
                    StartAutoBurnEasy();
                    trip = TripPhase.ReturnBurn;
                    tripStatus = $"Burning for home ({dv:0} m/s), lowest point {pe / 1000:0} km over {Home.name}";
                    return;
                }
                case TripPhase.ReturnBurn:
                    if (autoBurn && v.throttle > 0.05) StageIfBurntOut(v);
                    if (node != null || autoBurn) { tripStatus = node != null && node.ut > ut ? $"Waiting for the burn home ({Ui.Time(node.ut - ut)})" : "Burning for home"; return; }
                    trip = TripPhase.ReturnCoast;
                    return;
                case TripPhase.ReturnCoast:
                {
                    if (trajectory.Count == 0) return;
                    var seg = trajectory[0];
                    if (v.body != Home)
                    {
                        if (seg.end != SegmentEnd.ExitSoi) { trip = TripPhase.ReturnPlan; tripStatus = "Fixing the course"; return; }
                        if (warpTargetUt == null && v.CanRailsWarp()) WarpTo(seg.tEnd + 30, $"leaving {v.body.name}");
                        tripStatus = $"Leaving {v.body.name}: {Ui.Time(seg.tEnd - ut)}";
                        return;
                    }
                    // home's gravity: first a small correction while far out (cheap there) so the lowest point is where the
                    // parachutes want it (the burn home lands within tens of km: the bug test's came in at 61 km, a grazing pass
                    // that can skip back out), then warp down to the top of the air, heat shield first
                    {
                        var oc = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
                        double want = ReturnPlanner.WantPeriapsis(Home), low = oc.Periapsis - Home.radius;
                        bool far = v.Altitude > Home.radius * 2;
                        bool off = !oc.IsElliptic || Math.Abs(low - want) > 12_000;   // (escaping counts: slow down)
                        if (far && off || correcting && far && (!oc.IsElliptic || Math.Abs(low - want) > 2_000))
                        {
                            if (!correcting) { correcting = true; warpTargetUt = null; SetWarpOne(); }
                            Vector3d dir = !oc.IsElliptic || low > want ? -v.vel.normalized : v.vel.normalized;
                            v.sas = SasMode.Stability;
                            v.steerDirection = dir;
                            bool aimed = Vector3d.Angle(Vector3d.Rotate(v.rot, Vector3d.up), dir) < 8;
                            double amax = v.PotentialThrust() / Math.Max(1e-3, v.mass);
                            v.throttle = aimed && amax > 1e-6 ? Math.Max(0.02, Math.Min(1, 1.5 / amax)) : 0;
                            StageIfBurntOut(v);
                            tripStatus = $"Fixing the way home: lowest point {low / 1000:0} km, aiming for {want / 1000:0} km";
                            return;
                        }
                        if (correcting) { correcting = false; v.throttle = 0; v.steerDirection = null; }
                    }
                    v.sas = SasMode.Retrograde;
                    if (v.Altitude > Home.atmosphereHeight + 2000)
                    {
                        // warp down toward the lowest point (it's in the air, so the path doesn't end in an impact: the bug test
                        // found it falling for real at 1x); the warp stops by itself at the top of the air
                        if (warpTargetUt == null && v.CanRailsWarp())
                        {
                            var oh = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
                            double tLow = seg.end == SegmentEnd.Impact ? seg.tEnd : oh.IsElliptic ? ut + oh.TimeToPeriapsis(ut) : seg.tEnd;
                            if (tLow - ut > 240) WarpTo(tLow - 60, "the air");
                        }
                        tripStatus = $"Falling home to {Home.name}: {v.Altitude / 1000:0} km";
                        return;
                    }
                    trip = TripPhase.Done;
                    Show("Back in the air over home: heat shield first. The parachutes will open when it's safe.", 5f);
                    return;
                }
            }
        }
    }
}
