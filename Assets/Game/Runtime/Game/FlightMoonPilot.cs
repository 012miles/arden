using System;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 35 (captaincarl24: "for easy mode, make it so that it can autopilot to the moon and autoland"): on Easy,
    /// the autopilot can take you all the way. "Fly to Tal" (or Vesk): launch to orbit if you're on the pad, plan the
    /// trip (the burn is picked so you arrive about 30 km above the moon), warp out, slow into a low orbit round it,
    /// then land. "Land" on its own works from any orbit round a moon: burn to fall, warp down, then a landing burn that
    /// slows you to a gentle touchdown, legs down. Round a world with air, "Come home" drops into the air and the
    /// parachutes do the rest. X (or Stop) hands control back at any point.
    /// </summary>
    public partial class FlightMode
    {
        public enum TripPhase { Off, Launch, Plan, Transfer, Coast, Capture, Deorbit, Fall, Brake, Done, Ascend, Circularize, ReturnPlan, ReturnBurn, ReturnCoast }   // (milestone 46: the way home, FlightReturnPilot)
        public TripPhase trip = TripPhase.Off;
        CelestialBody tripTarget;
        bool tripLand, braking;
        float tripT;
        public string tripStatus = "";

        /// <summary>Starts a whole trip to a moon of the world you're at (and lands, unless 'land' is false).</summary>
        public void StartTrip(CelestialBody moon, bool land = true)
        {
            if (active == null || moon == null) return;
            tripTarget = moon; tripLand = land; braking = false;
            ClearNodes();
            if (!active.hasLaunched || (active.landed && active.body == Home)) { trip = TripPhase.Launch; tripStatus = "Launching to orbit"; }
            else if (active.body == moon) trip = land ? TripPhase.Deorbit : TripPhase.Done;
            else trip = TripPhase.Plan;
            Show($"Autopilot: off to {moon.name}{(land ? ", and down onto it" : "")}. X takes back control.", 4f);
        }

        /// <summary>Lands from orbit round where you are (a moon), or drops into the air to come home (a world with air).</summary>
        public void StartLanding()
        {
            if (active == null || active.landed) return;
            tripTarget = active.body; tripLand = true; braking = false;
            ClearNodes();
            trip = TripPhase.Deorbit;
            Show(active.body.HasAtmosphere ? "Autopilot: coming down through the air (the parachutes will open)." : $"Autopilot: landing on {active.body.name}.", 3.5f);
        }

        public void StopTrip(string why = null)
        {
            if (trip == TripPhase.Off) return;
            trip = TripPhase.Off;
            autoBurn = false;
            if (autopilotOn) StopAutopilot();
            if (active != null) { active.throttle = 0; active.steerDirection = null; }
            warpTargetUt = null;
            if (why != null) Show(why, 4f);
        }

        /// <summary>Every frame while a trip's on: one step of whatever phase it's in.</summary>
        void TripTick(float dt)
        {
            if (trip == TripPhase.Off || active == null) return;
            var v = active;
            if (v.destroyed) { StopTrip(); return; }
            if (In.Down(K.X)) { StopTrip("Autopilot off. You have control."); return; }
            tripT += dt;
            switch (trip)
            {
                case TripPhase.Launch:
                    if (!v.hasLaunched)
                    {
                        // milestone 46 (the bug test found the Tal Lander in the sea): light the first stage once, then wait for
                        // liftoff. It used to stage every frame until the rocket counted as launched, and since landings settle
                        // (milestone 41) a rocket sits on the pad a moment after lighting up: it dropped its booster on the pad
                        // and flew on the second stage alone (and cut the countdown short).
                        if (countdown >= 0f) { tripStatus = "Counting down"; return; }
                        if (v.nextStage == 0) { if (!TryCountdown()) Stage(); }
                        tripStatus = "Lifting off";
                        return;
                    }
                    if (!autopilotOn && v.Altitude < v.body.atmosphereHeight) { EnableAutopilot(); return; }
                    if (!autopilotOn) { trip = tripTarget == v.body ? TripPhase.Deorbit : TripPhase.Plan; }
                    tripStatus = "Climbing to orbit";
                    return;
                case TripPhase.Plan:
                    if (v.landed) { StopTrip("Can't plan a trip from the ground."); return; }
                    if (!PlanMoonTransfer(tripTarget)) { StopTrip($"Couldn't find a way to {tripTarget.name} from here. Is the rocket in orbit round {tripTarget.parent?.name}?"); return; }
                    StartAutoBurnEasy();
                    trip = TripPhase.Transfer;
                    tripStatus = $"Burning for {tripTarget.name}";
                    return;
                case TripPhase.Transfer:
                    if (node != null || autoBurn) { tripStatus = node != null && node.ut > ut ? $"Waiting for the burn to {tripTarget.name} ({Ui.Time(node.ut - ut)})" : $"Burning for {tripTarget.name}"; return; }
                    trip = TripPhase.Coast;
                    return;
                case TripPhase.Coast:
                    if (v.body == tripTarget) { trip = TripPhase.Capture; return; }
                    if (trajectory.Count > 0 && trajectory[0].end == SegmentEnd.EnterSoi && trajectory[0].next == tripTarget)
                    {
                        if (warpTargetUt == null && v.CanRailsWarp()) WarpTo(trajectory[0].tEnd + 30, $"{tripTarget.name}'s gravity");
                        tripStatus = $"Coasting to {tripTarget.name}: {Ui.Time(trajectory[0].tEnd - ut)}";
                    }
                    else { trip = TripPhase.Plan; tripStatus = "Fixing the course"; }
                    return;
                case TripPhase.Capture:
                {
                    var o = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
                    if (node == null && !autoBurn)
                    {
                        if (o.IsElliptic && o.Apoapsis - v.body.radius < 200_000) { trip = tripLand ? TripPhase.Deorbit : TripPhase.Done; return; }
                        double tpe = ut + Math.Max(60, o.TimeToPeriapsis(ut));
                        o.StateAt(tpe, out var rp, out var vp);
                        double circ = Math.Sqrt(v.body.mu / rp.magnitude);
                        AddNode(tpe, circ - vp.magnitude, 0, 0);
                        StartAutoBurnEasy();
                    }
                    tripStatus = $"Slowing into orbit round {tripTarget.name}";
                    return;
                }
                case TripPhase.Deorbit:
                {
                    var o = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
                    double peAlt = o.Periapsis - v.body.radius;
                    double want = v.body.HasAtmosphere ? v.body.atmosphereHeight * 0.4 : -v.body.radius * 0.02;
                    v.steerDirection = -(v.vel.normalized);
                    bool pointing = Vector3d.Angle(Vector3d.Rotate(v.rot, Vector3d.up), -v.vel.normalized) < 10;
                    if (peAlt > want) { v.throttle = pointing ? 1 : 0; tripStatus = "Burning to come down"; return; }
                    v.throttle = 0;
                    v.steerDirection = null;
                    if (v.body.HasAtmosphere) { trip = TripPhase.Done; Show("On the way down: the parachutes will open when it's safe.", 4f); return; }
                    trip = TripPhase.Fall;
                    return;
                }
                case TripPhase.Fall:
                {
                    // start braking while there's still room to stop (all the speed counts, sideways too)
                    double alt = v.RadarAltitude(ut);
                    if (TimeToBrake(v) || trajectory.Count == 0) { trip = TripPhase.Brake; warpTargetUt = null; SetWarpOne(); return; }
                    if (trajectory[0].end == SegmentEnd.Impact && warpTargetUt == null && v.CanRailsWarp() && trajectory[0].tEnd - ut > 400)
                    {
                        // warp only part of the way, then look again (the stopping distance changes as we fall)
                        double t = Math.Min(trajectory[0].tEnd - 300, ut + Math.Max(30, (trajectory[0].tEnd - ut) * 0.5));
                        if (t > ut + 20) WarpTo(t, "the landing burn");
                    }
                    tripStatus = $"Falling towards {v.body.name}: {alt / 1000:0.0} km";
                    v.steerDirection = -(v.SurfaceRelVelocity.normalized);
                    return;
                }
                case TripPhase.Brake:
                    if (v.landed || v.settling) { v.throttle = 0; v.steerDirection = null; trip = TripPhase.Done; Show($"Landed on {v.body.name}! Press ENTER to climb out.", 5f); return; }
                    LandingBurn(v);
                    return;
                case TripPhase.Ascend: case TripPhase.Circularize: case TripPhase.ReturnPlan: case TripPhase.ReturnBurn: case TripPhase.ReturnCoast:
                    ReturnTick(v);   // milestone 46: flying home
                    return;
                case TripPhase.Done:
                    trip = TripPhase.Off;
                    tripStatus = "";
                    return;
            }
        }

        /// <summary>
        /// Time to brake now? When the seconds left before hitting the ground (falling as we are) come down to the seconds
        /// a full burn needs to take off all the speed, sideways too, with a little to spare.
        /// </summary>
        bool TimeToBrake(VesselSim v)
        {
            double r = v.pos.magnitude, g = v.body.mu / (r * r);
            double a = v.PotentialThrust() / Math.Max(1e-3, v.mass);
            Vector3d sv = v.SurfaceRelVelocity;
            double alt = Math.Max(0, v.RadarAltitude(ut) - 5), vs = Vector3d.Dot(sv, v.pos.normalized);
            double tImpact = (vs + Math.Sqrt(vs * vs + 2 * g * alt)) / g;                 // falling from here with nothing done
            double tStop = sv.magnitude / Math.Max(0.1, a * 0.85 - g * 0.5);             // a full burn to a stop
            return tImpact <= tStop * 1.15 + 6;
        }

        /// <summary>
        /// The landing burn: point against the way you're moving (straight up once nearly stopped) and hold a speed that
        /// falls as the ground comes up: fast high up, a couple of metres a second at the bottom.
        /// </summary>
        void LandingBurn(VesselSim v)
        {
            double r = v.pos.magnitude, g = v.body.mu / (r * r);
            double amax = v.PotentialThrust() / Math.Max(1e-3, v.mass);
            Vector3d up = v.pos.normalized, sv = v.SurfaceRelVelocity;
            double alt = v.RadarAltitude(ut), speed = sv.magnitude, vs = Vector3d.Dot(sv, up);
            if (amax < g * 1.05) { StopTrip($"The engine's too weak to land here (it needs to push more than {v.body.name} pulls)."); return; }
            if (!v.gearDown) FireGroup(Groups.Gear);
            double room = Math.Max(0, alt - 3);
            double safe = Math.Sqrt(2 * (amax * 0.75 - g) * room);                     // the speed we could still stop from
            double target = Math.Max(1.6, Math.Min(safe * 0.85, 400));
            if (!braking && !TimeToBrake(v)) { v.throttle = 0; v.steerDirection = -sv.normalized; tripStatus = $"Coasting down: {alt:0} m"; return; }
            braking = true;
            v.steerDirection = speed > 3 && Vector3d.Dot(sv.normalized, up) < 0.95 ? -sv.normalized : up;
            double over = speed - target;                                              // how much too fast
            double aWant = g * Math.Max(0.2, -vs / Math.Max(speed, 0.1)) + over * 1.6;
            double face = Math.Max(0.2, Vector3d.Dot(Vector3d.Rotate(v.rot, Vector3d.up), v.steerDirection.Value));
            v.throttle = Math.Max(0, Math.Min(1, aWant / amax / face));
            tripStatus = $"Landing burn: {alt:0} m, {speed:0.0} m/s";
        }

        /// <summary>Hands the planned burns to the flight computer (on Easy it doesn't ask about SAS skills).</summary>
        void StartAutoBurnEasy()
        {
            if (node == null) return;
            autoBurn = true;
            active.sas = SasMode.Maneuver;
        }

        /// <summary>
        /// The burn from a round orbit to a moon: when the moon will be across the way at the far end (a half-orbit
        /// transfer), then fine-tuned by trying nearby times and sizes until the path arrives 20-60 km above it.
        /// </summary>
        public bool PlanMoonTransfer(CelestialBody moon)
        {
            var v = active;
            if (v == null || moon == null || moon.parent != v.body) return false;
            var o = Orbit.FromState(v.pos, v.vel, v.body.mu, ut);
            if (!o.IsElliptic) return false;
            double mu = v.body.mu, r1 = o.a, r2 = moon.orbitRadius;
            double at = (r1 + r2) / 2, ttr = Math.PI * Math.Sqrt(at * at * at / mu);
            double dv0 = Math.Sqrt(mu / r1) * (Math.Sqrt(2 * r2 / (r1 + r2)) - 1);
            Vector3d MoonAt(double t) => moon.AbsolutePosition(t) - v.body.AbsolutePosition(t);
            // when to go: the moon where we'll arrive
            double synodic = Math.Abs(1 / (1 / o.Period - 1 / (2 * Math.PI * Math.Sqrt(r2 * r2 * r2 / mu))));
            double span = Math.Min(Math.Max(synodic, o.Period), 40 * o.Period), best = double.MaxValue, tb = ut + 120;
            for (double t = ut + 120; t < ut + 120 + span; t += o.Period / 120)
            {
                o.StateAt(t, out var r, out _);
                double miss = Vector3d.Angle(-r.normalized, MoonAt(t + ttr).normalized);
                if (miss < best) { best = miss; tb = t; }
            }
            // fine-tune: the closest pass between 20 and 60 km up, not into the ground
            double want = 30_000, bestScore = double.MaxValue, bestT = tb, bestDv = dv0;
            for (double dt = -400; dt <= 400; dt += 40)
                for (double k = 0.985; k <= 1.035; k += 0.0025)
                {
                    double t = tb + dt;
                    if (t < ut + 60) continue;
                    o.StateAt(t, out var r, out var vel);
                    var path = Patcher.Predict(v.body, r, vel + vel.normalized * dv0 * k, t, 2);
                    if (path.Count < 2 || path[0].end != SegmentEnd.EnterSoi || path[0].next != moon) continue;
                    double pe = path[1].orbit.Periapsis - moon.radius;
                    double score = Math.Abs(pe - want) + (pe < 8_000 ? 1e6 : 0) + Math.Abs(k - 1) * 2e4;
                    if (score < bestScore) { bestScore = score; bestT = t; bestDv = dv0 * k; }
                }
            if (bestScore == double.MaxValue) return false;
            AddNode(bestT, bestDv, 0, 0);
            return node != null;
        }

        /// <summary>The Easy autopilot's buttons (above the navball) and what it's doing.</summary>
        void DrawTripPanel(float W, float H)
        {
            if (!Words.Easy || active == null || active.IsEva || active.destroyed || mapView || Coop.Active || replay != null) return;
            var r = new Rect(W / 2 - 250, H - 372, 500, 40);
            if (trip != TripPhase.Off)
            {
                Ui.Box(r);
                Ui.Text(new Rect(r.x + 12, r.y + 10, 360, 22), $"<color=#7dff8a>Autopilot</color>  {tripStatus}", new GUIStyle(Ui.Small) { wordWrap = false, clipping = TextClipping.Clip });
                if (Ui.Press(new Rect(r.xMax - 96, r.y + 7, 88, 26), "Stop (X)", Ui.ButtonStopSmall)) StopTrip("Autopilot off. You have control.");
                return;
            }
            var home = Home;
            bool canGoHome = active.body != home && active.body.parent == home && active.hasLaunched;   // milestone 46: "Fly home" from a moon
            if (active.landed && active.hasLaunched && active.body != Home && !canGoHome) return;
            var moons = root.bodies.Where(b => b.parent == (active.body.parent == home ? home : active.body) && !b.isStar && b.parent != null && b != active.body).ToList();
            if (active.body != home) moons.Clear();
            bool canLand = !active.landed && active.hasLaunched && active.body != null && (!active.body.HasAtmosphere || active.body == home)
                && Orbit.FromState(active.pos, active.vel, active.body.mu, ut).IsElliptic;
            if (active.landed || active.settling) canLand = false;   // (settling onto its legs: it has landed)
            if (moons.Count == 0 && !canLand && !canGoHome) return;
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 12, r.y + 10, 90, 22), "<color=#9fc6e8>Autopilot</color>", Ui.Small);
            float x = r.x + 92;
            foreach (var m in moons.Take(2))
            {
                if (Ui.Press(new Rect(x, r.y + 7, 118, 26), $"Fly to {m.name}", Ui.ButtonGoSmall)) StartTrip(m);
                x += 124;
            }
            if (canLand && Ui.Press(new Rect(x, r.y + 7, 118, 26), active.body.HasAtmosphere ? "Come home" : "Land", Ui.ButtonGoSmall)) StartLanding();
            if (canLand) x += 124;
            if (canGoHome && Ui.Press(new Rect(x, r.y + 7, 118, 26), $"Fly home", Ui.ButtonGoSmall)) StartReturn();
        }
    }
}
