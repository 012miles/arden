using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 41 (captaincarl24: "when landing on other planets, why does the ship get anchored like i landed like on a 45
    /// degree on 1 legg and it anchored not realistic"): a touchdown used to park the ship right where it first touched, at
    /// whatever angle. Now it settles first on the same contact physics rovers roll on: every leg foot and every part
    /// touching the ground pushes back and grips, gravity pulls round the feet that are down, so a ship that lands on one leg
    /// rocks down onto the others, or (too far over, or too tall on a slope) topples onto its side. It parks once it has
    /// been still for a moment, so time warp works as before.
    /// </summary>
    public partial class VesselSim
    {
        /// <summary>Touched down and settling on the ground's contact physics (not parked yet).</summary>
        public bool settling;
        double settleStill, settleTime;

        /// <summary>
        /// Milestone 43 (captaincarl24: "if the ships falling upside down we're pushed to the back"; zero gravity in space): what
        /// the crew feel, in the ship's own frame (m/s²): the push of everything but gravity (engines, air, the ground or the
        /// water holding it up). Standing on the pad it points up out of the floor at 1 g; coasting it's nothing (weightless);
        /// upside down on the ground it points out of the ceiling. The cabin turns it into which way is down.
        /// </summary>
        public Vector3 feltLocal = new Vector3(0, 9.81f, 0);

        /// <summary>For tests: turns a ship sitting on the ground upside down where it stands.</summary>
        public void TestFlipLanded(double ut) { landedFixedRot = landedFixedRot * Quaternion.AngleAxis(180f, Vector3.right); SyncLanded(ut); }

        public void Step(double dt, double ut)
        {
            Vector3d v0 = vel;
            StepCore(dt, ut);
            if (dt <= 0 || body == null) return;
            Vector3d push = landed ? -body.Gravity(pos) : (vel - v0) / dt - body.Gravity(pos);
            Vector3 local = Quaternion.Inverse(rot) * push.ToFloat();
            if (local.magnitude > 60f) local = local.normalized * 60f;   // (a crash's jolt: kept to 6 g)
            feltLocal = Vector3.Lerp(feltLocal, local, Mathf.Clamp01((float)dt * 8f));
        }
    }
}
