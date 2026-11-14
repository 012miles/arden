using System;
using UnityEngine;

namespace Rocket
{
    /// <summary>The transfer planner in the map: target another planet while orbiting yours, then "Plan transfer".</summary>
    public partial class FlightMode
    {
        /// <summary>True when the target is another planet round the same sun as the planet you're orbiting.</summary>
        bool PlanetTarget => target != null && active != null && target.IsPlanet && target.parent != null && active.body.IsPlanet
                             && target != active.body && target.parent == active.body.parent;

        /// <summary>On the way: orbiting the sun with another planet targeted.</summary>
        bool CruiseTarget => target != null && active != null && active.body.isStar && target.parent == active.body;

        /// <summary>The map panel's button for the planner: plan the trip, or fine-tune the arrival on the way.</summary>
        string TransferButton => PlanetTarget ? "Plan transfer" : CruiseTarget ? "Fine-tune arrival" : VesselTarget ? (CanMatch ? "Match speed" : "Plan intercept") : null;

        void TransferButtonPressed()
        {
            if (PlanetTarget) PlanTransfer();
            else if (CruiseTarget) PlanCorrection();
            else if (VesselTarget) { if (CanMatch) PlanMatch(); else PlanIntercept(); }
        }

        /// <summary>A vessel (or wanderer) target flying around the same body as us.</summary>
        bool VesselTarget => targetVessel != null && !targetVessel.landed && !active.landed && targetVessel.body == active.body && trajectory.Count > 0;

        /// <summary>Close enough at the coming closest approach (with no node set) to plan the burn that matches its speed there.</summary>
        bool CanMatch => (node == null || !node.valid) && !double.IsNaN(closestDist) && closestDist < 10_000 && closestTime > ut + 5 && closestRel > 0.5;

        /// <summary>The cheapest single burn to meet the target, as a maneuver node; the speed match comes when you're nearly there.</summary>
        public bool PlanIntercept()
        {
            if (!VesselTarget) return false;
            var tOrb = Orbit.FromState(targetVessel.pos, targetVessel.vel, targetVessel.body.mu, ut);
            var me = Orbit.FromState(active.pos, active.vel, active.body.mu, ut);
            double myP = me.IsElliptic ? me.Period : 3600;
            double window = tOrb.IsElliptic ? Math.Max(myP * 2, Math.Min(Patcher.SynodicPeriod(me, tOrb), myP * 6))
                                            : Math.Max(600, Math.Min(TimeToLeave(tOrb, active.body.SoiRadius) - ut - 3600, 2 * 86400));
            double meet = targetVessel.IsRock && !tOrb.IsElliptic ? Intercept.WandererMeetLimit(tOrb) : double.PositiveInfinity;
            if (!Intercept.Plan(active.body, active.pos, active.vel, ut, tOrb, window, out var p, meet))
            {
                Show($"No way to reach {targetVessel.name} from this orbit without going through the air.", 4f);
                return false;
            }
            me.StateAt(p.burnUt, out var r, out var v);
            Intercept.ToNode(r, v, p.dv1, out double pro, out double nor, out double rad);
            laterNodes.Clear();
            AddNode(p.burnUt, pro, nor, rad);
            RefreshTrajectory();
            // The speed match, queued: from the path the first burn leaves, half its burn before the meeting
            var seg = node.valid ? node.planned.Find(s => p.arriveUt >= s.tStart && p.arriveUt <= s.tEnd) : null;
            if (seg != null)
            {
                double half = ManeuverNode.BurnTime(active, p.dv2.magnitude) / 2;
                double tn = p.arriveUt - (double.IsInfinity(half) ? 0 : Math.Min(half, 0.45 * (p.arriveUt - p.burnUt)));
                seg.orbit.StateAt(tn, out var ra, out var va);   // components in the frame at the node itself
                Intercept.ToNode(ra, va, p.dv2, out double p2, out double n2, out double r2);
                AddLaterNode(tn, p2, n2, r2);
                laterNodes[laterNodes.Count - 1].matchTarget = targetVessel;
            }
            closestTimer = 0;
            Show($"Intercept: {p.dv1.magnitude:0} m/s in {Ui.Time(p.burnUt - ut)}, then {p.dv2.magnitude:0} m/s to match speed (both set)", 6f);
            return true;
        }

        /// <summary>A node at the coming closest approach that cancels the speed difference to the target.</summary>
        public bool PlanMatch()
        {
            if (!VesselTarget || double.IsNaN(closestDist)) return false;
            var seg = trajectory.Find(s => closestTime >= s.tStart && closestTime <= s.tEnd) ?? trajectory[0];
            seg.orbit.StateAt(closestTime, out _, out var v);
            var tOrb = Orbit.FromState(targetVessel.pos, targetVessel.vel, targetVessel.body.mu, ut);
            tOrb.StateAt(closestTime, out _, out var vt);
            double half = ManeuverNode.BurnTime(active, (vt - v).magnitude) / 2;
            double tn = closestTime - (double.IsInfinity(half) ? 0 : Math.Min(half, 0.45 * (closestTime - ut)));
            seg.orbit.StateAt(tn, out var rn, out var vn);   // components in the frame at the node itself
            Intercept.ToNode(rn, vn, vt - v, out double pro, out double nor, out double rad);
            AddNode(tn, pro, nor, rad);
            node.matchTarget = targetVessel;
            RefreshTrajectory();
            Show($"Match speed: {(vt - v).magnitude:0} m/s at {Ui.Dist(closestDist)} from {targetVessel.name}", 5f);
            return true;
        }

        /// <summary>One line for the map panel: where the target is now, where it should be to leave, and when that comes round.</summary>
        string TransferLine()
        {
            if (CruiseTarget)
            {
                var enc = trajectory.Find(s => s.body == target);
                return enc != null ? $"{target.name}: pass {Ui.Dist(enc.orbit.Periapsis - target.radius)} up, in {Ui.Time(enc.tStart - ut)}"
                     : !double.IsNaN(closestDist) ? $"{target.name}: closest {Ui.Dist(closestDist)}, no encounter" : $"On the way to {target.name}";
            }
            if (VesselTarget)
                return CanMatch ? $"{targetVessel.name}: closest {Ui.Dist(closestDist)} at {closestRel:0} m/s. Match speed there."
                     : node != null && node.valid && !double.IsNaN(plannedClosestDist) ? $"After the burn: closest {Ui.Dist(plannedClosestDist)} in {Ui.Time(plannedClosestTime - ut)}"
                     : $"Meet {targetVessel.name}: one burn to get there, one to match its speed";
            if (!PlanetTarget) return null;
            double now = TransferPlanner.Phase(active.body, target, ut) * 180 / Math.PI;
            double want = TransferPlanner.RequiredPhase(active.body, target) * 180 / Math.PI;
            double win = TransferPlanner.NextWindow(active.body, target, ut);
            return $"{target.name} {now:0} deg ahead, leave at {want:0}: window in {Ui.Time(win - ut)}";
        }

        /// <summary>Puts a maneuver node on the best escape burn toward the targeted planet, at its next window.</summary>
        public bool PlanTransfer()
        {
            if (!PlanetTarget) { Show("Target another planet first (click its name in the map)."); return false; }
            if (!TransferPlanner.Plan(active.body, active.pos, active.vel, ut, target, out double burnUt, out double dv, out double normal, out double closest, out _))
            {
                Show($"Can't plan a transfer from here: get into a low, round orbit of {active.body.name} first.");
                return false;
            }
            RefreshTrajectory();
            AddNode(burnUt, dv, normal);
            RefreshTrajectory();
            Show(closest <= 0
                ? $"Transfer to {target.name} planned: {dv:0} m/s in {Ui.Time(burnUt - ut)}. Fine-tune the arrival on the way."
                : $"Closest this gets is {Ui.Dist(closest)} from {target.name}: fine-tune it on the way.", 7f);
            return true;
        }

        /// <summary>On the way, to a planet with air: a correction that dips the arrival into the air just deep enough to capture.</summary>
        public bool PlanAerocapture()
        {
            if (!CruiseTarget || !target.HasAtmosphere) return false;
            var enc = trajectory.Find(s => s.body == target);
            double vinf = enc != null && !enc.orbit.IsElliptic ? System.Math.Sqrt(-target.mu / enc.orbit.a)
                        : System.Math.Abs(TransferPlanner.ExcessSpeed(target, target.home != null ? target.home : target)); // rough until there's an encounter
            active.DragRange(out double least, out double most);
            double alt = TransferPlanner.AerocaptureAltitude(target, vinf, active.mass, least, most);
            if (double.IsNaN(alt))
            {
                Show($"No pass through {target.name}'s air would capture this vessel at {vinf:0} m/s: it needs more drag (a heat shield) or a capture burn.", 7f);
                return false;
            }
            return PlanCorrection(alt, $"aerocapture: {Ui.Dist(alt)} up in its air");
        }

        /// <summary>On the way: a small correction burn that brings the arrival down to just above the target's air.</summary>
        public bool PlanCorrection() => PlanCorrection(target.atmosphereHeight + 30_000, null);

        bool PlanCorrection(double wantAlt, string why)
        {
            if (!CruiseTarget) return false;
            if (!TransferPlanner.PlanCorrection(active.body, active.pos, active.vel, ut, target, wantAlt, out double burnUt, out double p, out double n, out double r, out double alt))
            {
                Show($"{target.name} isn't anywhere near this path: plan the trip again from orbit.");
                return false;
            }
            RefreshTrajectory();
            AddNode(burnUt, p, n, r);
            RefreshTrajectory();
            Show(double.IsNaN(alt) ? $"Best correction: {Math.Sqrt(p * p + n * n + r * r):0.0} m/s in {Ui.Time(burnUt - ut)} (still no encounter)."
                : $"Correction: {Math.Sqrt(p * p + n * n + r * r):0.0} m/s in {Ui.Time(burnUt - ut)}, arriving {Ui.Dist(alt)} above {target.name}{(why != null ? $" ({why})" : "")}.", 7f);
            return true;
        }
    }
}
