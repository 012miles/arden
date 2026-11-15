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
    }
}
