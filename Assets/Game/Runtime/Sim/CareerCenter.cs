using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public enum Facility { Pad, Runway, Assembly, MissionControl, Research, Astronauts, Admin }

    /// <summary>The space center's buildings: three levels each, with what every level allows and what upgrading costs.</summary>
    public static class Facilities
    {
        public const int MaxLevel = 2;

        public static readonly (Facility f, string name, double[] upgradeCost)[] List =
        {
            (Facility.Pad, "Launch Pad", new[] { 60_000.0, 320_000 }),
            (Facility.Runway, "Runway", new[] { 50_000.0, 280_000 }),
            (Facility.Assembly, "Assembly Building", new[] { 55_000.0, 300_000 }),
            (Facility.MissionControl, "Mission Control", new[] { 45_000.0, 250_000 }),
            (Facility.Research, "Research Lab", new[] { 70_000.0, 380_000 }),
            (Facility.Astronauts, "Astronaut Complex", new[] { 35_000.0, 200_000 }),
            (Facility.Admin, "Administration", new[] { 40_000.0, 220_000 }),
        };

        public static readonly double[] PadMass = { 20, 150, double.PositiveInfinity };   // t at launch
        public static readonly double[] RunwayMass = { 12, 60, double.PositiveInfinity };
        public static readonly int[] PartLimit = { 30, 120, int.MaxValue };
        public static readonly int[] ActiveContracts = { 3, 7, int.MaxValue };
        public static readonly int[] Offers = { 4, 6, 8 };
        public static readonly int[] ResearchTier = { 3, 6, 8 };
        public static readonly int[] RosterSize = { 5, 12, int.MaxValue };
        public static readonly int[] StrategySlots = { 1, 2, 4 };

        static string Lim(double v, string unit) => double.IsInfinity(v) || v >= int.MaxValue ? "no limit" : $"{v:N0}{unit}";

        public static string Effect(Facility f, int lvl) => f switch
        {
            Facility.Pad => $"Launch mass {Lim(PadMass[lvl], " t")}",
            Facility.Runway => $"Takeoff mass {Lim(RunwayMass[lvl], " t")}",
            Facility.Assembly => $"Parts per vessel {Lim(PartLimit[lvl], "")}",
            Facility.MissionControl => $"Active contracts {Lim(ActiveContracts[lvl], "")}, {Offers[lvl]} offers",
            Facility.Research => $"Research up to tier {ResearchTier[lvl]}",
            Facility.Astronauts => $"Roster {Lim(RosterSize[lvl], "")} astronauts",
            _ => $"{StrategySlots[lvl]} strateg{(StrategySlots[lvl] == 1 ? "y" : "ies")} at once",
        };
    }

    public class StrategyDef
    {
        public string id, name, description;
        public double setupCost;
    }

    public enum CrewRole { Pilot, Engineer, Scientist }

    /// <summary>A member of the astronaut corps.</summary>
    [Serializable]
    public class CrewMember
    {
        public string name;
        public int state;     // 0 available, 1 flying, 2 lost
        public int vesselId;  // where they are while flying
        public int flights;
        public CrewRole role;
        public int log;       // VFlags of everything they've done and come home from (experience)

        public int Xp => Crew.XpOf(log);
        public int Level => Crew.LevelOf(Crew.XpOf(log));
    }

    /// <summary>
    /// Crew roles and experience. Everyone gains experience by coming home from new places: space, orbit, Tal, Tal orbit,
    /// Tal's surface (1 to 5 points, levels at 1, 3, 6, 10 and 15). Roles:
    /// pilots hold more SAS modes as they level up; engineers make drills and converters produce more;
    /// scientists send science home more efficiently and can clean out used sample canisters.
    /// </summary>
    public static class Crew
    {
        public static readonly (int flag, int xp, string what)[] Log =
        {
            (VFlags.Space, 1, "space"), (VFlags.OrbitHome, 2, "orbit"), (VFlags.SoiTal, 3, "Tal"), (VFlags.OrbitTal, 4, "Tal orbit"), (VFlags.LandedTal, 5, "Tal's surface"),
            (VFlags.SoiVesk, 4, "Vesk"), (VFlags.OrbitVesk, 5, "Vesk orbit"), (VFlags.LandedVesk, 6, "Vesk's surface"),
            (VFlags.SoiSun, 6, "orbit of Helion"),
            (VFlags.SoiOchre, 7, "Ochre"), (VFlags.OrbitOchre, 8, "Ochre orbit"), (VFlags.LandedOchre, 9, "Ochre's surface"), (VFlags.LandedMote, 8, "Mote's surface"),
            (VFlags.SoiBrume, 9, "Brume"), (VFlags.OrbitBrume, 10, "Brume orbit"), (VFlags.LandedRime, 12, "Rime's surface"), (VFlags.LandedPyre, 12, "Pyre's surface"),
        };
        public static readonly int[] Thresholds = { 1, 3, 6, 10, 15 };
        public const int MaxLevel = 5;
        public static int XpMask { get { int m = 0; foreach (var e in Log) m |= e.flag; return m; } }

        public static int XpOf(int log) { int x = 0; foreach (var e in Log) if ((log & e.flag) != 0) x += e.xp; return x; }
        public static int LevelOf(int xp) { int l = 0; foreach (int t in Thresholds) if (xp >= t) l++; return l; }

        public static string RoleName(CrewRole r) => r == CrewRole.Pilot ? "pilot" : r == CrewRole.Engineer ? "engineer" : "scientist";
        public static string Stars(int level) => level <= 0 ? "" : new string('*', level);
        public static string Tag(CrewMember m) => m == null ? "" : $"{RoleName(m.role)}{(m.Level > 0 ? " " + Stars(m.Level) : "")}";

        /// <summary>SAS modes in tiers: 0 hold, 1 prograde/retrograde, 2 normal/radial, 3 target and port alignment, 4 maneuver.</summary>
        public static int TierNeeded(SasMode m) => m switch
        {
            SasMode.Off or SasMode.Stability => 0,
            SasMode.Prograde or SasMode.Retrograde => 1,
            SasMode.Normal or SasMode.AntiNormal or SasMode.RadialOut or SasMode.RadialIn => 2,
            SasMode.Target or SasMode.AntiTarget or SasMode.DockAlign => 3,
            _ => 4,
        };
        public static int PilotTier(int level) => Math.Min(4, level + 1);
        public static string TierModes(int tier) => tier switch
        {
            <= 0 => "hold only", 1 => "hold, prograde/retrograde", 2 => "up to normal/radial", 3 => "up to target", _ => "every mode",
        };
        /// <summary>Extra drill and converter output from the best engineer aboard (level 0: +20%, +10% a level).</summary>
        public static double MiningBonus(int level) => level < 0 ? 0 : 0.2 + 0.1 * level;
        /// <summary>Extra share of a result sent home from the best scientist aboard (level 0: +10%, +5% a level).</summary>
        public static double TransmitBonus(int level) => level < 0 ? 0 : 0.10 + 0.05 * level;
        public static CrewRole RoleFor(string name) { int h = 0; foreach (char ch in name ?? "") h = h * 31 + ch; return (CrewRole)(((h % 3) + 3) % 3); }
    }

    /// <summary>Space center management: facilities, strategies, the astronaut roster, rescues and bases.</summary>
    public partial class Career
    {
        public readonly int[] facility = new int[7];
        public readonly HashSet<string> strategies = new HashSet<string>();
        public readonly List<CrewMember> roster = new List<CrewMember>();

        public static readonly List<StrategyDef> Strategies = new List<StrategyDef>
        {
            new StrategyDef { id = "outsourced", name = "Outsourced research", setupCost = 15_000,
                description = "A quarter of every contract's money goes to outside labs instead: 1 science per 1,000 funds." },
            new StrategyDef { id = "patents", name = "Patent licensing", setupCost = 15_000,
                description = "Sell a quarter of contract science as patents: 1,000 funds per science." },
            new StrategyDef { id = "pr", name = "Public relations", setupCost = 10_000,
                description = "Spend a fifth of contract money on publicity: 1 reputation per 1,500 funds." },
            new StrategyDef { id = "bulk", name = "Bulk purchasing", setupCost = 25_000,
                description = "Rockets cost 20% less to launch, but the cheaper look costs you 20% of reputation gains." },
            new StrategyDef { id = "student", name = "Student research", setupCost = 10_000,
                description = "Turn a quarter of reputation gains into science, 1 for 1, through university programs." },
        };

        public int Level(Facility f) => facility[(int)f];
        public bool HasStrategy(string strategyId) => strategies.Contains(strategyId);

        public double MaxLaunchMass(bool runway) => (runway ? Facilities.RunwayMass : Facilities.PadMass)[Level(runway ? Facility.Runway : Facility.Pad)];
        public int MaxParts => Facilities.PartLimit[Level(Facility.Assembly)];
        public int ActiveLimit => IsCareer ? Facilities.ActiveContracts[Level(Facility.MissionControl)] : int.MaxValue;
        public int OffersMax => Facilities.Offers[Level(Facility.MissionControl)];
        public int MaxResearchTier => Facilities.ResearchTier[Level(Facility.Research)];
        public int MaxRoster => Facilities.RosterSize[Level(Facility.Astronauts)];
        public int StrategySlots => Facilities.StrategySlots[Level(Facility.Admin)];

        void StartCenter()
        {
            for (int i = 0; i < facility.Length; i++) facility[i] = IsCareer ? 0 : Facilities.MaxLevel;
            strategies.Clear();
            roster.Clear();
            crewPlan.Clear();
            // Two pilots, an engineer and a scientist to start a career; sandbox hires fully trained crew as launches need them.
            if (IsCareer) foreach (var r in new[] { CrewRole.Pilot, CrewRole.Engineer, CrewRole.Scientist, CrewRole.Pilot }) roster.Add(new CrewMember { name = NewCrewName(), role = r });
        }

        void LoadCenter(CareerSave s)
        {
            for (int i = 0; i < facility.Length; i++) facility[i] = s.facilities != null && i < s.facilities.Count ? s.facilities[i] : (IsCareer ? 0 : Facilities.MaxLevel);
            strategies.Clear();
            if (s.strategies != null) foreach (var x in s.strategies) strategies.Add(x);
            roster.Clear();
            crewPlan.Clear();
            if (s.roster != null) roster.AddRange(s.roster);
            // Saved before crew had roles: the first four become pilot, engineer, scientist, pilot; the rest by name.
            if (s.version < 2)
                for (int i = 0; i < roster.Count; i++)
                    roster[i].role = i < 4 ? new[] { CrewRole.Pilot, CrewRole.Engineer, CrewRole.Scientist, CrewRole.Pilot }[i] : Crew.RoleFor(roster[i].name);
        }

        // ---------------------------------------------------------------- facilities

        public double UpgradeCost(Facility f)
        {
            int lvl = Level(f);
            return lvl >= Facilities.MaxLevel ? double.PositiveInfinity : Facilities.List[(int)f].upgradeCost[lvl];
        }

        public bool Upgrade(Facility f)
        {
            double cost = UpgradeCost(f);
            if (!IsCareer || double.IsInfinity(cost) || funds < cost) return false;
            funds -= cost;
            facility[(int)f]++;
            news.Add($"{Facilities.List[(int)f].name} upgraded to level {Level(f) + 1}: {Facilities.Effect(f, Level(f))}");
            if (f == Facility.MissionControl) RefreshOffers(0);
            return true;
        }

        public static double LaunchMass(VesselDesign d)
        {
            double m = 0;
            foreach (var n in d.nodes) { var def = PartCatalog.Get(n.id); if (def != null) m += def.WetMass; }
            return m;
        }

        /// <summary>Pad/runway mass and part-count limits for the current facility levels, or null.</summary>
        string FacilityProblem(VesselDesign d, bool runway)
        {
            double mass = LaunchMass(d), maxMass = MaxLaunchMass(runway);
            if (mass > maxMass) return $"{mass:0.0} t is too heavy for the {(runway ? "runway" : "launch pad")} ({maxMass:0} t). Upgrade it in the Space center.";
            if (d.nodes.Count > MaxParts) return $"{d.nodes.Count} parts: the Assembly Building handles {MaxParts}. Upgrade it in the Space center.";
            return null;
        }

        public double LaunchCost(VesselDesign d) => Prices.Of(d) * (HasStrategy("bulk") ? 0.8 : 1.0);

        // ---------------------------------------------------------------- strategies

        public bool CanActivate(StrategyDef s) => IsCareer && !HasStrategy(s.id) && strategies.Count < StrategySlots && funds >= s.setupCost;

        public bool Activate(StrategyDef s)
        {
            if (!CanActivate(s)) return false;
            funds -= s.setupCost;
            strategies.Add(s.id);
            news.Add($"Strategy started: {s.name}");
            return true;
        }

        public void Deactivate(StrategyDef s) { if (strategies.Remove(s.id)) news.Add($"Strategy ended: {s.name}"); }

        /// <summary>Pays out a reward, passing it through the active strategies. Returns what was actually paid.</summary>
        public (double funds, double science, double rep) Pay(double f, double s, double r)
        {
            if (HasStrategy("outsourced")) { double moved = f * 0.25; f -= moved; s += moved / 1000; }
            if (HasStrategy("pr")) { double moved = f * 0.20; f -= moved; r += moved / 1500; }
            if (HasStrategy("patents")) { double moved = s * 0.25; s -= moved; f += moved * 1000; }
            if (HasStrategy("bulk")) r *= 0.8;
            if (HasStrategy("student")) { double moved = r * 0.25; r -= moved; s += moved; }
            funds += f;
            science += s;
            reputation += r;
            return (f, s, r);
        }

        // ---------------------------------------------------------------- astronauts

        static readonly System.Random nameRng = new System.Random();

        string NewCrewName()
        {
            bool Free(string n) => !roster.Exists(c => c.name == n) && !contracts.Exists(k => k.crewName == n);
            for (int i = 0; i < 300; i++)
            {
                string n = CrewNames.Next();
                if (Free(n)) return n;
            }
            for (int k = 0; k < 200; k++)
            {
                string n = CrewNames.Numbered(k / 20);
                if (Free(n)) return n;
            }
            return CrewNames.Next() + " " + nameRng.Next(11, 99);
        }

        public IEnumerable<CrewMember> Available => roster.Where(c => c.state == 0);
        public int LivingCrew => roster.Count(c => c.state != 2);
        public double HireCost => 8_000 + 4_000 * roster.Count(c => c.state != 2);

        public string Hire(CrewRole role = CrewRole.Pilot)
        {
            if (!IsCareer) return "Sandbox hires fully trained astronauts by itself whenever a launch needs them.";
            if (LivingCrew >= MaxRoster) return $"The Astronaut Complex holds {MaxRoster}. Upgrade it to hire more.";
            double cost = HireCost;
            if (funds < cost) return $"Hiring costs {cost:N0}; you have {funds:N0}.";
            funds -= cost;
            var m = new CrewMember { name = NewCrewName(), role = role };
            roster.Add(m);
            return $"Hired {m.name} ({Crew.RoleName(role)}) for {cost:N0}.";
        }

        /// <summary>
        /// The astronaut with this name. Crew the roster doesn't know (from saves made before roles) count as level-0
        /// pilots in a career, and as fully trained pilots in sandbox.
        /// </summary>
        public CrewMember CrewInfo(string name) =>
            roster.Find(c => c.name == name) ?? new CrewMember { name = name, role = CrewRole.Pilot, log = IsCareer ? 0 : Crew.XpMask };

        /// <summary>Who the builder has picked for each seat, in seat order (empty = choose automatically). Not saved.</summary>
        public readonly List<string> crewPlan = new List<string>();

        /// <summary>Seat 1 wants a pilot, then an engineer, a scientist, a pilot...</summary>
        public static CrewRole SeatRole(int seat) => (CrewRole)(seat % 3);

        /// <summary>
        /// Who would fill this many seats: the builder's picks first, then someone of the seat's preferred role, then anyone
        /// free. In sandbox, missing astronauts are hired (fully trained) when 'hire' is set.
        /// </summary>
        public List<CrewMember> PlanCrew(int seats, bool hire)
        {
            var picked = new List<CrewMember>();
            var free = Available.ToList();
            for (int s = 0; s < seats; s++)
            {
                CrewMember m = null;
                if (s < crewPlan.Count && !string.IsNullOrEmpty(crewPlan[s])) m = free.Find(c => c.name == crewPlan[s]);
                if (m == null)
                {
                    var want = SeatRole(s);
                    m = free.Find(c => c.role == want && !crewPlan.Contains(c.name)) ?? free.Find(c => !crewPlan.Contains(c.name));
                }
                if (m == null && !IsCareer && hire)
                {
                    m = new CrewMember { name = NewCrewName(), role = SeatRole(s), log = Crew.XpMask };
                    roster.Add(m);
                }
                if (m == null) break;
                free.Remove(m);
                picked.Add(m);
            }
            return picked;
        }

        /// <summary>Seats that get crew at launch: capsules and cockpits in a career (habitats launch empty), every seat in sandbox.</summary>
        public int LaunchSeats(VesselDesign d) => Seats(d, IsCareer);

        /// <summary>The crew a design would launch with, for the builder (sandbox fills gaps with new, fully trained astronauts).</summary>
        public List<CrewMember> PreviewCrew(VesselDesign d)
        {
            int seats = LaunchSeats(d);
            var list = PlanCrew(seats, false);
            if (!IsCareer) for (int s = list.Count; s < seats; s++) list.Add(new CrewMember { name = "(new astronaut)", role = SeatRole(s), log = Crew.XpMask });
            return list;
        }

        /// <summary>Crew seats in the command parts (filled first) and habitats of a design.</summary>
        public static int Seats(VesselDesign d, bool commandOnly)
        {
            int n = 0;
            foreach (var node in d.nodes)
            {
                var def = PartCatalog.Get(node.id);
                if (def == null || def.crew <= 0) continue;
                if (!commandOnly || def.kind == PartKind.Command) n += def.crew;
            }
            return n;
        }

        /// <summary>Builder note when there aren't enough astronauts for the capsules, or null.</summary>
        public string CrewNote(VesselDesign d)
        {
            int seats = Seats(d, true);
            if (seats == 0) return null;
            if (IsCareer)
            {
                int have = Available.Count();
                if (have == 0) return "No astronauts available: the capsule will fly empty (it can't steer without crew). Hire in the Space center.";
                if (have < seats) return $"Only {have} astronaut{(have == 1 ? "" : "s")} for {seats} capsule seats.";
            }
            var crew = PreviewCrew(d);
            bool probe = d.nodes.Any(n => (PartCatalog.Get(n.id)?.sasTier ?? 0) > 0);
            if (!probe && !crew.Any(c => c.role == CrewRole.Pilot))
                return "No pilot aboard: SAS can only hold attitude. Pick a pilot (Crew button) or add a probe core.";
            return null;
        }

        /// <summary>At launch: seats astronauts (the builder's picks, then pilots first). Returns how many flew.</summary>
        public int AssignCrew(VesselSim v)
        {
            foreach (var p in v.parts) p.crew.Clear();
            int seats = 0;
            foreach (var p in v.parts) if (p.def.crew > 0 && (!IsCareer || p.def.kind == PartKind.Command)) seats += p.def.crew;
            var crew = PlanCrew(seats, true);
            int k = 0;
            // Capsules and cockpits first; in sandbox, habitats too (in a career they launch empty and crew move in later).
            foreach (bool command in new[] { true, false })
                foreach (var p in v.parts)
                {
                    if (p.def.crew <= 0 || (p.def.kind == PartKind.Command) != command || (!command && IsCareer)) continue;
                    while (p.crew.Count < p.def.crew && k < crew.Count)
                    {
                        var m = crew[k++];
                        m.state = 1;
                        m.vesselId = v.id;
                        p.crew.Add(m.name);
                    }
                }
            v.RecomputeMass();
            return k;
        }

        /// <summary>
        /// Recovered: everyone aboard is back at the space center with what they've done added to their log (experience).
        /// Rescued strangers join the corps.
        /// </summary>
        string ReturnCrew(VesselSim v)
        {
            var names = v.parts.SelectMany(p => p.crew).ToList();
            if (names.Count == 0) return "";
            var rescued = new List<string>();
            var promoted = new List<string>();
            foreach (var n in names)
            {
                var m = roster.Find(c => c.name == n);
                if (m == null) { m = new CrewMember { name = n, role = Crew.RoleFor(n), log = IsCareer ? 0 : Crew.XpMask }; roster.Add(m); if (IsCareer) rescued.Add(n); }
                int before = m.Level;
                m.log |= v.careerFlags & Crew.XpMask;
                m.state = 0;
                m.vesselId = 0;
                m.flights++;
                if (m.Level > before) promoted.Add($"{m.name} is now a level {m.Level} {Crew.RoleName(m.role)}");
            }
            if (!IsCareer) return "";
            foreach (var p in promoted) news.Add(p + "!");
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && x.type == "rescue").ToList())
                if (names.Contains(c.crewName)) Complete(c);
            return rescued.Count > 0 ? $". Rescued {string.Join(", ", rescued)}, who joins your astronauts" : $". Crew home: {string.Join(", ", names)}";
        }

        /// <summary>A vessel with people aboard was destroyed (or deleted): they're lost.</summary>
        public void OnVesselLost(VesselSim v, bool penalty = true)
        {
            foreach (var n in v.parts.SelectMany(p => p.crew))
            {
                var m = roster.Find(c => c.name == n);
                if (m != null && m.state != 2)
                {
                    m.state = 2;
                    if (!IsCareer) continue;
                    if (penalty) { reputation = Math.Max(0, reputation - 5); news.Add($"{n} was lost. -5 reputation"); }
                    else news.Add($"{n} was left aboard {v.name} and is gone.");
                }
            }
        }

        // ---------------------------------------------------------------- rescue and base contracts

        void AddCenterCandidates(List<Contract> list, System.Random rng)
        {
            bool orbit = (records & Records.Orbit) != 0, talLand = (records & Records.TalLanding) != 0;
            if (orbit && unlocked.Contains("docking") && OpenCount("rescue") < 1)
            {
                string who = NewCrewName();
                double alt = 90_000 + 10_000 * rng.Next(0, 12);
                var c = New("rescue", $"Rescue {who}", $"{who} is stranded in a {alt / 1000:0} km orbit of Arden in a capsule with a small docking port. Dock with it, bring {who.Split(' ')[0]} aboard and recover them on Arden.", 60_000, 200, 15);
                c.body = "Arden"; c.crewName = who; c.altitude = alt; list.Add(c);
            }
            // Milestone 27: space stations, put together in orbit by docking (offered once you've docked)
            if (orbit && (records & Records.Docking) != 0 && OpenCount("station") < 1)   // after your first docking
            {
                foreach (var (body, rec, seats, pieces, pay, sci) in new[] { ("Arden", Records.Orbit, 3, 2, 120_000.0, 120.0), ("Tal", Records.TalOrbit, 4, 3, 240_000.0, 300.0), ("Ochre", Records.OchreOrbit, 4, 3, 520_000.0, 700.0) })
                {
                    if ((records & rec) == 0 || contracts.Any(k => k.type == "station" && k.body == body && k.state != ContractState.Failed)) continue;
                    var c = New("station", $"Space station at {body}", $"Build a station in orbit of {body} by docking ships together: at least {pieces} ships made into one, {seats} crew seats, a free docking port for visitors, an antenna and solar panels.", pay, 400, 25, sci);
                    c.body = body; c.seats = seats; c.amount = pieces; list.Add(c);
                    break;
                }
            }
            if (unlocked.Contains("electrics") && OpenCount("base") < 2)
            {
                if (!contracts.Any(k => k.type == "base" && k.body == "Arden" && k.state != ContractState.Failed && k.state != ContractState.Completed))
                {
                    double lat = Math.Round(rng.NextDouble() * 40 - 20, 1), lon = Math.Round(25 + rng.NextDouble() * 120, 1);
                    var c = New("base", $"Outpost at {lat:0.0}, {lon:0.0}", $"Land an outpost on Arden within 10 km of latitude {lat:0.0}, longitude {lon:0.0}: at least 2 crew seats, an antenna and a solar panel.", 70_000, 150, 12);
                    c.body = "Arden"; c.lat = lat; c.lon = lon; c.radiusKm = 10; c.seats = 2; list.Add(c);
                }
                if (talLand && !Has("base", "Tal"))
                {
                    var c = New("base", "Base on Tal", "Land a base anywhere on Tal: at least 4 crew seats, an antenna and a solar panel.", 150_000, 300, 25);
                    c.body = "Tal"; c.seats = 4; list.Add(c);
                }
                // Milestone 22: research bases of several modules cabled together
                if (unlocked.Contains("outposts"))
                {
                    foreach (var (body, rec, seats, pay, sci) in new[] { ("Tal", Records.TalLanding, 4, 260_000.0, 500.0), ("Vesk", Records.VeskLanding, 4, 380_000.0, 700.0), ("Ochre", Records.OchreLanding, 6, 600_000.0, 1000.0) })
                    {
                        if ((records & rec) == 0 || contracts.Any(k => k.type == "base" && k.body == body && k.partId == "base.lab" && k.state != ContractState.Failed)) continue;
                        var c = New("base", $"Research base on {body}", $"Build a research base on {body}: at least 3 modules cabled together, {seats} crew seats, a Research Lab, an antenna and a solar panel.", pay, 500, 30, sci);
                        c.body = body; c.seats = seats; c.amount = 3; c.partId = "base.lab"; list.Add(c);
                    }
                }
            }
        }

        /// <summary>Checks base contracts against landed vessels (once a second or so).</summary>
        public void CheckBases(IEnumerable<VesselSim> vessels, double ut, Universe u = null)
        {
            if (!IsCareer) return;
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && x.type == "base").ToList())
                foreach (var v in vessels)
                {
                    if (v.destroyed || !v.landed || v.body.name != c.body) continue;
                    // A base is the vessel and every module cabled to it (milestone 22)
                    var g = u != null ? Bases.GroupOf(u, v) : new List<VesselSim> { v };
                    var all = g.SelectMany(x => x.parts).ToList();
                    int seats = all.Sum(p => p.def.crew);
                    if (seats < c.seats || !all.Exists(p => p.def.kind == PartKind.Antenna) || !all.Exists(p => p.def.kind == PartKind.Solar)) continue;
                    if (c.amount > 0 && g.Count < c.amount) continue;                                 // modules needed
                    if (!string.IsNullOrEmpty(c.partId) && !all.Exists(p => p.def.id == c.partId)) continue;
                    if (c.radiusKm > 0)
                    {
                        Vector3d here = v.body.InertialToFixed(v.pos, ut).normalized;
                        double km = Vector3d.Angle(here, CelestialBody.DirectionFromLatLon(c.lat, c.lon)) * v.body.radius / 1000;
                        if (km > c.radiusKm) continue;
                    }
                    Complete(c);
                    break;
                }
        }

        /// <summary>Checks station contracts against vessels in orbit (once a second or so; milestone 27).</summary>
        public void CheckStations(IEnumerable<VesselSim> vessels, double ut)
        {
            if (!IsCareer) return;
            foreach (var c in contracts.Where(x => x.state == ContractState.Active && x.type == "station").ToList())
                foreach (var v in vessels)
                {
                    if (v.destroyed || v.landed || v.isDebris || v.body.name != c.body) continue;
                    if (Stations.Meets(v, ut, c.seats, (int)c.amount)) { Complete(c); break; }
                }
        }

        /// <summary>Accepted rescue contracts that still need their stranded capsule put into orbit.</summary>
        public IEnumerable<Contract> PendingRescues => contracts.Where(x => x.state == ContractState.Active && x.type == "rescue" && x.vesselId == 0);
    }
}
