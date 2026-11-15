using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    /// <summary>
    /// Milestone 44 (captaincarl24: "incorporate the nasa datasets into the game ... use the datasets for the main game"): the
    /// NASA Space Apps "Space Mission Design Game" resources, brought into the main game's builder as a Mission Review of the
    /// rocket on the stand. Each part comes from one resource:
    /// - NASA's "Design and Develop Science Missions": a mission has four segments. Launch (can the rocket get there?), Space
    ///   (the spacecraft: someone or something to fly it, power, steering, instruments), Ground (an antenna to talk to mission
    ///   control and send data home) and User (who uses what it brings back).
    /// - NASA's Mission Design Lab: a design is checked discipline by discipline: propulsion, power, thermal, attitude control,
    ///   communications, and the end of the mission (how it gets down again).
    /// - JPL's Concept Maturity Levels: how far along the design is, CML 1 (an idea) to CML 4 (a point design, everything
    ///   checked), the levels where the paper says the big decisions are made.
    /// - The telescope learning game: the real NASA missions a design is like.
    /// </summary>
    public static class MissionReview
    {
        public class Line { public string area; public bool ok; public string text; }

        public class Result
        {
            public readonly List<Line> segments = new List<Line>(), disciplines = new List<Line>();
            public int cml;
            public string cmlName, cmlNext;
            public readonly List<string> realMissions = new List<string>();
            public double dv, twr;
        }

        public static readonly string[] CmlNames = { "", "CML 1: an idea", "CML 2: a first concept", "CML 3: it could work", "CML 4: a point design" };

        public static Result Review(VesselDesign d)
        {
            var r = new Result();
            if (d == null || d.nodes.Count == 0) { r.cml = 0; r.cmlName = "No design yet"; r.cmlNext = "Add a capsule or probe core to start."; return r; }
            var defs = d.Defs().Where(x => x != null).ToList();
            var reps = Staging.AnalyzeDesign(d, 9.81);
            r.dv = reps.Sum(s => s.dvVac);
            var first = reps.FirstOrDefault(s => s.hasEngines);
            r.twr = first?.twrAsl ?? 0;
            bool plane = d.IsPlane, rover = d.IsRover;
            bool command = defs.Any(x => x.kind == PartKind.Command);
            int seats = defs.Sum(x => x.crew);
            bool engine = defs.Any(x => x.IsEngine);
            double battery = defs.Sum(x => x.ecCapacity);
            bool solar = defs.Any(x => x.kind == PartKind.Solar);
            bool antenna = defs.Any(x => x.kind == PartKind.Antenna || x.commPower > 0);
            bool bigAntenna = defs.Any(x => x.kind == PartKind.Antenna);
            int science = defs.Count(x => x.kind == PartKind.Science || x.kind == PartKind.Scanner);
            bool chute = defs.Any(x => x.kind == PartKind.Parachute), shield = defs.Any(x => x.kind == PartKind.HeatShield);
            bool legs = defs.Any(x => x.kind == PartKind.Leg), gear = defs.Any(x => x.kind == PartKind.Gear), wheels = defs.Any(x => x.kind == PartKind.Wheel);
            double torque = defs.Sum(x => x.torque);
            bool rcs = defs.Any(x => x.kind == PartKind.Rcs);
            bool habitat = defs.Any(x => x.kind == PartKind.Habitat), dock = defs.Any(x => x.kind == PartKind.DockingPort);
            bool rocket = defs.Any(x => x.IsEngine && !x.IsJet);   // (jets show a big speed budget, but only in the air)
            bool orbit = rocket && r.dv >= 3300, tal = rocket && r.dv >= 5500;

            // ---- the four segments
            string goes = plane ? "flies on its wings" : rover ? "drives on the ground" : tal ? $"{r.dv:N0} m/s: enough for Tal and back" : orbit ? $"{r.dv:N0} m/s: enough for orbit" : $"{r.dv:N0} m/s: a hop (orbit needs about 3,300)";
            bool launchOk = plane || rover || (engine && r.twr >= 1.15);
            r.segments.Add(new Line { area = "Launch", ok = launchOk, text = !engine && !rover ? "No engine: it can't leave the ground." : !plane && !rover && r.twr < 1.15 ? $"Lift-off push is only {r.twr:0.00}x its weight: it needs 1.2x or more to get off the pad." : $"It {(plane || rover ? goes : $"lifts off at {r.twr:0.0}x its weight, {goes}")}." });
            bool spaceOk = command && (battery > 0 || solar) && (torque > 0 || rcs);
            r.segments.Add(new Line { area = "Space", ok = spaceOk, text = !command ? "Nothing to fly it: add a capsule, cockpit or probe core." : battery <= 0 && !solar ? "No power: add a battery or solar panels." : torque <= 0 && !rcs ? "Nothing to steer it in space: add a capsule or core with reaction wheels, or RCS." : $"{(seats > 0 ? $"{seats} seat{(seats > 1 ? "s" : "")}" : "Uncrewed")}, {battery:N0} EC of power{(solar ? " plus solar" : "")}, steering by {(torque > 0 ? "reaction wheels" : "RCS")}." });
            r.segments.Add(new Line { area = "Ground", ok = antenna, text = !antenna ? "No way to talk home: add an antenna so mission control can hear it and science can be sent." : bigAntenna ? "An antenna for mission control and sending science home." : "Only the core's small radio: fine near home, add an antenna to go further." });
            r.segments.Add(new Line { area = "User", ok = science > 0 || seats > 0, text = science > 0 ? $"{science} instrument{(science > 1 ? "s" : "")} making data for scientists back home." : seats > 0 ? "The crew are the users: they fly it and bring back what they see. Add an instrument for science." : "Nothing on board makes data or carries anyone: who is this mission for?" });

            // ---- the Mission Design Lab's disciplines
            r.disciplines.Add(new Line { area = "Propulsion", ok = engine || rover, text = engine ? $"{reps.Count(s => s.hasEngines)} stage{(reps.Count(s => s.hasEngines) == 1 ? "" : "s")} with engines, {r.dv:N0} m/s in all." : rover ? "Driven by its wheels." : "No engines." });
            r.disciplines.Add(new Line { area = "Power", ok = battery > 0 && (solar || r.dv < 3300 || plane || seats > 0), text = battery <= 0 ? "No battery." : !solar && orbit && seats == 0 ? "Batteries only: a long trip without solar panels will go flat." : $"{battery:N0} EC stored{(solar ? ", recharged by the sun" : "")}." });
            bool needsShield = seats > 0 && orbit && !plane;
            r.disciplines.Add(new Line { area = "Thermal", ok = !needsShield || shield, text = needsShield && !shield ? "Coming home from orbit is fast and hot: add a heat shield under the capsule." : shield ? "A heat shield for coming home." : "No hard reentry planned." });
            r.disciplines.Add(new Line { area = "Attitude control", ok = torque > 0 || rcs, text = torque > 0 ? $"Reaction wheels ({torque:0} kN·m){(rcs ? " and RCS" : "")}." : rcs ? "RCS only: steering uses up monopropellant." : "Nothing to point it." });
            r.disciplines.Add(new Line { area = "Communications", ok = antenna, text = antenna ? "It can talk to the ground." : "No radio." });
            bool recovers = chute || legs || gear || wheels || seats == 0;
            r.disciplines.Add(new Line { area = "End of mission", ok = recovers, text = seats > 0 && !chute && !legs && !gear ? "The crew have no way down: add a parachute (or landing legs or gear)." : chute ? "Comes down on its parachute." : gear ? "Lands on its wheels on a runway." : legs ? "Lands on its legs." : "Uncrewed: it can stay out there when its job's done." });

            // ---- concept maturity (JPL's CMLs 1-4)
            bool concept = command && (engine || rover);
            bool feasible = concept && launchOk;
            bool point = feasible && r.segments.All(l => l.ok) && r.disciplines.All(l => l.ok);
            r.cml = point ? 4 : feasible ? 3 : concept ? 2 : 1;
            r.cmlName = CmlNames[r.cml];
            r.cmlNext = r.cml == 1 ? "Next: something to fly it and an engine (CML 2)." : r.cml == 2 ? "Next: make sure it can launch (CML 3)." : r.cml == 3 ? "Next: fix every red line below (CML 4)." : "Every segment and discipline checks out. Fly it to prove it!";

            // ---- real missions like this (after the telescope game)
            if (plane && orbit) r.realMissions.Add("NASA's Space Shuttle (1981-2011): launched like a rocket, landed on a runway like a plane.");
            else if (plane && engine && defs.Any(x => x.IsEngine && !x.IsJet)) r.realMissions.Add("NASA's X-15 (1959-1968): a rocket plane that flew to the edge of space.");
            else if (plane) r.realMissions.Add("NASA's research planes, like the X-59 quiet supersonic jet, test new wings and engines before airliners use them.");
            if (rover) r.realMissions.Add("NASA's Curiosity (2012) and Perseverance (2021) Mars rovers, and the Apollo Lunar Roving Vehicle (1971-72).");
            if (seats > 0 && shield && chute && !plane) r.realMissions.Add(tal ? "Apollo (1968-72) and Artemis's Orion: capsules that went to the Moon and splashed down under parachutes." : "Mercury, Gemini and Orion: capsules that come home behind a heat shield and land on parachutes.");
            if (legs && tal) r.realMissions.Add("The Apollo Lunar Module: landed on its legs on the Moon and flew the crew back up.");
            if (habitat && dock) r.realMissions.Add("The International Space Station: modules joined by docking ports, crewed since 2000.");
            if (seats == 0 && solar && bigAntenna) r.realMissions.Add(science > 0 ? "NASA's Lunar Reconnaissance Orbiter (2009): solar power, a big antenna and cameras mapping the Moon." : "Communications satellites: solar panels and antennas, relaying signals round the world.");
            if (seats == 0 && reps.Count > 0 && reps[0].startMass < 2) r.realMissions.Add("NASA's MarCO CubeSats (2018): tiny spacecraft that relayed InSight's Mars landing.");
            if (r.realMissions.Count == 0) r.realMissions.Add(orbit ? "Early rockets like NASA's Vanguard and Explorer 1 (1958) proved a small rocket can reach orbit." : "Sounding rockets: NASA still flies short hops like this to test instruments high in the air.");
            return r;
        }
    }
}
