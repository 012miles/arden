using System;

namespace Rocket
{
    /// <summary>Milestone 22: research labs turn crew time into science (career only).</summary>
    public partial class VesselSim
    {
        /// <summary>Science per day the labs aboard are making right now (for the HUD).</summary>
        public double labRate;

        void UpdateLab(double dt)
        {
            labRate = 0;
            if (career == null || !career.IsCareer || destroyed) return;
            bool power = EcStored > 1;
            foreach (var p in parts)
            {
                if (p.def.labRate <= 0 || p.crew.Count == 0 || !power) continue;
                double people = 0;
                foreach (var n in p.crew)
                {
                    var m = CrewInfo(n);
                    people += m.role == CrewRole.Scientist ? 1 + 0.25 * Math.Max(0, m.Level) : 0.4;
                }
                double perDay = p.def.labRate * people * body.scienceMult * (landed ? 1.25 : 1.0) * (body.IsHomeWorld ? 0.25 : 1.0);
                labRate += perDay;
            }
            if (labRate > 0) career.science += labRate * dt / Supplies.Day;
        }
    }
}
