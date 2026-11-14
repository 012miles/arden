using System;
using System.Collections.Generic;

namespace Rocket
{
    /// <summary>What the people aboard can do: pilots' SAS modes, engineers' mining bonus, scientists' science bonus.</summary>
    public partial class VesselSim
    {
        /// <summary>Set by the universe: the roster that knows each astronaut's role and experience.</summary>
        public Career career;

        /// <summary>Role and experience of someone aboard (without a roster, everyone counts as a level-0 pilot).</summary>
        public CrewMember CrewInfo(string name) => career != null ? career.CrewInfo(name) : new CrewMember { name = name, role = CrewRole.Pilot };

        /// <summary>Highest level among the crew of a role, or -1 if nobody aboard has it.</summary>
        public int BestLevel(CrewRole role)
        {
            int best = -1;
            foreach (var p in parts)
                foreach (var n in p.crew)
                {
                    var m = CrewInfo(n);
                    if (m.role == role && m.Level > best) best = m.Level;
                }
            return best;
        }

        /// <summary>
        /// Which SAS modes the vessel can hold (Crew.TierNeeded): any working command part holds attitude; a powered probe
        /// core adds what its computer can do; a pilot adds more with each level. -1 without control.
        /// </summary>
        public int SasTier
        {
            get
            {
                if (!HasControl) return -1;
                int t = 0;
                bool ec = EcStored > 1e-3;
                foreach (var p in parts) if (p.def.kind == PartKind.Command && p.def.crew == 0 && ec) t = Math.Max(t, p.def.sasTier);
                int pilot = BestLevel(CrewRole.Pilot);
                if (pilot >= 0) t = Math.Max(t, Crew.PilotTier(pilot));
                return t;
            }
        }

        public bool CanHold(SasMode m) => Crew.TierNeeded(m) <= SasTier;

        /// <summary>Who gives the vessel its best SAS: "pilot Ava Roe, level 2" or "Vireo Guidance Core".</summary>
        public string SasSource
        {
            get
            {
                string who = null;
                int best = -1;
                bool ec = EcStored > 1e-3;
                foreach (var p in parts)
                {
                    if (p.def.kind == PartKind.Command && p.def.crew == 0 && ec && p.def.sasTier > best) { best = p.def.sasTier; who = p.def.name; }
                    foreach (var n in p.crew)
                    {
                        var m = CrewInfo(n);
                        if (m.role == CrewRole.Pilot && Crew.PilotTier(m.Level) > best) { best = Crew.PilotTier(m.Level); who = $"{m.name}, level {m.Level} pilot"; }
                    }
                }
                return who ?? "the command part (no pilot)";
            }
        }

        /// <summary>Everyone aboard with their role and level, for the HUD.</summary>
        public List<CrewMember> CrewAboard()
        {
            var list = new List<CrewMember>();
            foreach (var p in parts) foreach (var n in p.crew) list.Add(CrewInfo(n));
            return list;
        }
    }
}
