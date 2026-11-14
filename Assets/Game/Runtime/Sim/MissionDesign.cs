using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    /// <summary>Who decides what in a mission team (milestone 29). Alone, you're all four.</summary>
    public enum MRole { Lead = 0, Engineer = 1, Scientist = 2, Comms = 3 }

    public enum MCat { Bus, Power, Battery, Attitude, Computer, Antenna, Propulsion, Thermal, Instrument, Ground, Users }

    /// <summary>One thing you can put in a mission: a spacecraft part, an instrument, a ground network or a science team.</summary>
    public class MPart
    {
        public string id, name, blurb;
        public MCat cat;
        public bool advanced;
        public float mass, power, cost;                  // kg, W used, M (millions of funds)
        public float capacityKg, slots, idleW, lifeYears; // bus: what it carries, instrument places, its own needs, design life
        public float solarW, rtgW, batteryWh;            // power parts (solar at Arden's distance from the sun)
        public float pointing;                           // attitude: how finely it can point (deg)
        public float storageGB;                          // computer
        public float gain, maxMbps, needPointing;        // antennas (and instruments' own pointing need)
        public float fuelKg, isp;                        // propulsion
        public float heaterW;                            // thermal
        public float dataGB, science, swath;             // instruments: per day while working; swath as a share of the altitude
        public string[] tags = new string[0];
        public bool night;                               // instrument works on the night side too (radar, magnetometer)
        public float hoursPerStation, groundGain, opsPerYear;   // ground networks
        public float scienceMult = 1f;                   // users
        public float Max => cat == MCat.Bus ? capacityKg : 0;
    }

    public enum MOrbit { Low, Polar, SunSync, Medium, High, Stationary }

    /// <summary>A rocket to launch on: how much it lifts to each kind of destination, its price and how often it works.</summary>
    public class MVehicle
    {
        public string id, name, blurb;
        public float leo, high, moon, planet;            // kg it can carry there
        public float cost, reliability;
        public float CapacityFor(MDest d) => d == MDest.Leo ? leo : d == MDest.High ? high : d == MDest.Moon ? moon : planet;
    }

    public enum MDest { Leo, High, Moon, Planet }

    /// <summary>What the mission is for: where, which kinds of science, and what counts as success.</summary>
    public class MScenario
    {
        public string id, name, body, brief, goal;
        public string[] tags;                            // the science that counts most
        public float coverageNeeded, scienceNeeded, lifeYears, dataNeededGB, relayNeeded;
        public float budget;                             // M (0 = no limit)
        public int difficulty;                           // 0 easy .. 2 hard
        public bool sandbox;
    }

    /// <summary>The team's design, as plain choices (it goes over the network and into saves as it is).</summary>
    [Serializable]
    public class MissionPlan
    {
        public string scenario = "storm";
        public string body = "Arden";                    // sandbox: where
        public string sandboxTag = "map";
        public float sandboxYears = 2;
        public string bus = "bus.box", power = "sol.l", battery = "bat.s", attitude = "att.rw", computer = "cpu.fast", antenna = "ant.low",
            propulsion = "prop.none", thermal = "th.blanket", ground = "gnd.one", users = "usr.team", vehicle = "lv.kestrel";
        public int wings = 2;
        public MOrbit orbit = MOrbit.Low;
        public List<string> instruments = new List<string>();

        public MissionPlan Clone() => UnityEngine.JsonUtility.FromJson<MissionPlan>(UnityEngine.JsonUtility.ToJson(this));
    }

    /// <summary>One gauge on the dashboard: how much is used against how much there is, and why it matters.</summary>
    public struct MGauge
    {
        public string name, unit, text;
        public float used, have;
        public int state;                                // 0 fine, 1 tight, 2 a problem
    }

    /// <summary>Everything worked out from a design before it flies: the gauges, problems that stop a launch, warnings.</summary>
    public class MSheet
    {
        public MScenario scenario;
        public CelestialBody body;
        public float massKg, payloadKg, costM, opsPerYearM, totalCostM, devMonths;
        public float sunW, nightW, loadW, nightNeedWh, batteryUsableWh;
        public float dataPerDayGB, downPerDayGB, rateMbps, contactHours, storageGB;
        public float dvNeed, dvHave, launchCapacity;
        public float eclipse, maxCoverage, resolution, pointingQuality, solarFactor, commDistanceKm, altitudeKm, periodMin;
        public float lifeYearsEstimate;
        public float sciencePerDay;                                   // science points a day at best (milestone 31, for comparing)
        public MDest dest;
        public readonly List<MGauge> gauges = new List<MGauge>();
        public readonly List<string> blockers = new List<string>();   // can't launch until fixed
        public readonly List<string> warnings = new List<string>();
        public int maturity;                                          // concept maturity, 1..6 (after NASA's CMLs)
        public bool CanLaunch => blockers.Count == 0;
    }

    /// <summary>
    /// Milestone 29 (captaincarl24, for the NASA Space Apps "Space Mission Design Game" challenge): design a whole space mission.
    /// Pick what it's for, build a spacecraft from parts (a frame, power, batteries, pointing, a computer, an antenna,
    /// fuel, heaters), choose instruments, an orbit, a rocket, ground stations and who uses the data; watch the gauges
    /// (mass, power, budget, data, communications, time, speed budget) and the warnings; then fly it as a fast
    /// simulation that gives a score and a report on why it went the way it did. The same worlds as the main game,
    /// their real sizes, distances and sunlight.
    /// </summary>
    public static class MissionDesign
    {
        // ---------------------------------------------------------------- the catalogue

        public static readonly MPart[] Parts =
        {
            // frames: what they carry, how many instruments fit, what they use themselves, how long they're built to last
            new MPart { id = "bus.cube", cat = MCat.Bus, name = "Cube frame", mass = 12, cost = 4, capacityKg = 30, slots = 2, idleW = 6, lifeYears = 2,
                blurb = "Tiny and cheap, like a CubeSat. Room for two small instruments; built to last about 2 years." },
            new MPart { id = "bus.box", cat = MCat.Bus, name = "Box frame", mass = 180, cost = 30, capacityKg = 260, slots = 4, idleW = 40, lifeYears = 5,
                blurb = "A medium spacecraft. Four instrument places; built for about 5 years." },
            new MPart { id = "bus.big", cat = MCat.Bus, name = "Big frame", mass = 700, cost = 85, capacityKg = 1300, slots = 7, idleW = 120, lifeYears = 10, advanced = true,
                blurb = "A flagship-size spacecraft. Seven instrument places, built for 10 years, heavy and expensive." },
            // power
            new MPart { id = "sol.s", cat = MCat.Power, name = "Small solar wings", mass = 4, cost = 1.5f, solarW = 60,
                blurb = "Power from sunlight: 60 W a wing near Arden, less further from the sun, nothing in a world's shadow." },
            new MPart { id = "sol.l", cat = MCat.Power, name = "Large solar wings", mass = 15, cost = 5, solarW = 300,
                blurb = "300 W a wing near Arden. Two wings is usual; out at Ochre sunlight is only 40% as strong." },
            new MPart { id = "sol.xl", cat = MCat.Power, name = "Huge solar wings", mass = 45, cost = 12, solarW = 1100, advanced = true,
                blurb = "1,100 W a wing near Arden. For power-hungry radars far from the sun." },
            new MPart { id = "rtg", cat = MCat.Power, name = "Nuclear battery (RTG)", mass = 55, cost = 60, rtgW = 300, advanced = true,
                blurb = "Makes 300 W day and night, however far from the sun. Heavy and very expensive. (The wings setting is ignored.)" },
            // batteries: for the night side of each orbit
            new MPart { id = "bat.s", cat = MCat.Battery, name = "Small battery", mass = 4, cost = 1, batteryWh = 150,
                blurb = "Keeps things running in a world's shadow. Only 60% of it should be used each orbit, or it wears out." },
            new MPart { id = "bat.l", cat = MCat.Battery, name = "Large battery", mass = 20, cost = 4, batteryWh = 900,
                blurb = "Six times the small one, for long nights in low orbits." },
            new MPart { id = "bat.xl", cat = MCat.Battery, name = "Huge battery", mass = 60, cost = 10, batteryWh = 3000, advanced = true,
                blurb = "For big spacecraft with long eclipses." },
            // pointing
            new MPart { id = "att.mag", cat = MCat.Attitude, name = "Magnetic torquers", mass = 3, cost = 1, power = 5, pointing = 2f,
                blurb = "Push against Arden's magnetic field: cheap and light, but rough (2 deg), and weaker away from Arden (5 deg)." },
            new MPart { id = "att.rw", cat = MCat.Attitude, name = "Reaction wheels", mass = 25, cost = 8, power = 30, pointing = 0.1f,
                blurb = "Spinning wheels turn the spacecraft precisely (0.1 deg). Needed for dishes and sharp cameras." },
            new MPart { id = "att.star", cat = MCat.Attitude, name = "Star tracker + wheels", mass = 40, cost = 18, power = 45, pointing = 0.01f, advanced = true,
                blurb = "Navigates by the stars: very precise (0.01 deg), for the sharpest pictures and deep-space dishes." },
            // computers: where data waits until it can be sent
            new MPart { id = "cpu.basic", cat = MCat.Computer, name = "Basic computer", mass = 2, cost = 1, power = 8, storageGB = 8,
                blurb = "Runs the spacecraft and stores 8 GB of data until it can be sent home." },
            new MPart { id = "cpu.fast", cat = MCat.Computer, name = "Fast computer", mass = 6, cost = 5, power = 20, storageGB = 64,
                blurb = "Stores 64 GB: room for a day of pictures between ground station passes." },
            new MPart { id = "cpu.rec", cat = MCat.Computer, name = "Big recorder", mass = 15, cost = 9, power = 30, storageGB = 512, advanced = true,
                blurb = "Stores 512 GB, for instruments that make lots of data and short contact times." },
            // antennas: how fast data gets home (it falls off with the square of the distance)
            new MPart { id = "ant.low", cat = MCat.Antenna, name = "Low-gain antenna", mass = 2, cost = 1, power = 10, gain = 0.00005f, maxMbps = 2,
                blurb = "Sends in all directions, so no pointing needed, but slowly: fine near Arden, a trickle from Tal, useless further out." },
            new MPart { id = "ant.dish", cat = MCat.Antenna, name = "High-gain dish", mass = 18, cost = 7, power = 40, gain = 0.003f, maxMbps = 40, needPointing = 1f,
                blurb = "A dish that must point at home (1 deg). About 60 times faster than a low-gain antenna at the same distance." },
            new MPart { id = "ant.deep", cat = MCat.Antenna, name = "Deep-space dish", mass = 45, cost = 20, power = 80, gain = 0.05f, maxMbps = 100, needPointing = 0.1f, advanced = true,
                blurb = "A big dish for the planets. Needs fine pointing (0.1 deg) and the Deep Space Network at home." },
            // propulsion: slowing into orbit at another world, and keeping the orbit up
            new MPart { id = "prop.none", cat = MCat.Propulsion, name = "No engine", blurb = "Light and cheap, but it can't slow into orbit round another world or fight air drag." },
            new MPart { id = "prop.s", cat = MCat.Propulsion, name = "Small thruster", mass = 8, cost = 3, fuelKg = 25, isp = 220,
                blurb = "A little fuel for keeping a low orbit up for years." },
            new MPart { id = "prop.m", cat = MCat.Propulsion, name = "Main engine", mass = 30, cost = 10, fuelKg = 120, isp = 310,
                blurb = "Enough to slow into orbit round a moon, or a small spacecraft round a planet." },
            new MPart { id = "prop.l", cat = MCat.Propulsion, name = "Big engine and tanks", mass = 80, cost = 20, fuelKg = 900, isp = 320, advanced = true,
                blurb = "For heavy spacecraft going into orbit round Ochre or Brume's moons." },
            // keeping warm
            new MPart { id = "th.blanket", cat = MCat.Thermal, name = "Blankets and paint", mass = 2, cost = 0.5f,
                blurb = "Enough to keep warm as close to the sun as Arden and its moons." },
            new MPart { id = "th.heat", cat = MCat.Thermal, name = "Heaters", mass = 6, cost = 2, heaterW = 60,
                blurb = "Needed further out (Ochre, Brume): uses 60 W, but cold electronics fail without it." },
            // instruments
            new MPart { id = "ins.cam", cat = MCat.Instrument, name = "Wide camera", mass = 8, cost = 3, power = 15, dataGB = 6, science = 1.0f, needPointing = 1f, swath = 1.0f, tags = new[] { "map", "weather" },
                blurb = "Pictures of a wide strip of ground. Needs daylight. Good for maps and clouds." },
            new MPart { id = "ins.sharp", cat = MCat.Instrument, name = "Sharp camera", mass = 35, cost = 15, power = 40, dataGB = 40, science = 3.0f, needPointing = 0.1f, swath = 0.1f, tags = new[] { "map" }, advanced = true,
                blurb = "Very detailed pictures of a narrow strip: lots of science and lots of data. Needs fine pointing." },
            new MPart { id = "ins.sounder", cat = MCat.Instrument, name = "Weather sounder", mass = 25, cost = 9, power = 30, dataGB = 3, science = 1.5f, needPointing = 1f, swath = 0.8f, tags = new[] { "weather" }, night = true,
                blurb = "Measures temperature and water in the air at every height. Only useful at worlds with air." },
            new MPart { id = "ins.alt", cat = MCat.Instrument, name = "Radar altimeter", mass = 30, cost = 10, power = 60, dataGB = 1, science = 1.2f, needPointing = 2f, swath = 0.02f, tags = new[] { "map" }, night = true,
                blurb = "Bounces radio off the ground to measure its height: maps mountains and craters, day or night." },
            new MPart { id = "ins.ice", cat = MCat.Instrument, name = "Ice radar", mass = 45, cost = 18, power = 120, dataGB = 4, science = 3.5f, needPointing = 2f, swath = 0.05f, tags = new[] { "ice" }, night = true, advanced = true,
                blurb = "Sees ice hidden under the surface. Power hungry." },
            new MPart { id = "ins.spec", cat = MCat.Instrument, name = "Spectrometer", mass = 20, cost = 12, power = 25, dataGB = 8, science = 2.2f, needPointing = 0.5f, swath = 0.3f, tags = new[] { "ice", "minerals", "weather" },
                blurb = "Splits light into colours to tell what things are made of: ice, rock, gases." },
            new MPart { id = "ins.therm", cat = MCat.Instrument, name = "Thermal camera", mass = 15, cost = 7, power = 20, dataGB = 5, science = 1.4f, needPointing = 0.5f, swath = 0.6f, tags = new[] { "weather", "volcano", "ice" }, night = true,
                blurb = "Sees heat: storms, volcanoes and lava, day or night." },
            new MPart { id = "ins.mag", cat = MCat.Instrument, name = "Magnetometer", mass = 3, cost = 2, power = 4, dataGB = 0.05f, science = 0.4f, tags = new[] { "space" }, night = true,
                blurb = "Measures magnetic fields. Tiny and cheap, no pointing needed." },
            new MPart { id = "ins.part", cat = MCat.Instrument, name = "Particle counter", mass = 4, cost = 2, power = 5, dataGB = 0.1f, science = 0.4f, tags = new[] { "space" }, night = true,
                blurb = "Counts radiation and dust. Tiny and cheap." },
            new MPart { id = "ins.relay", cat = MCat.Instrument, name = "Relay radio", mass = 15, cost = 6, power = 35, tags = new[] { "relay" }, night = true,
                blurb = "Passes messages between landers and home. Makes no science itself." },
            // ground segment
            new MPart { id = "gnd.one", cat = MCat.Ground, name = "One ground station", hoursPerStation = 1, groundGain = 1, opsPerYear = 5,
                blurb = "One dish at the space centre. Each pass over it is a chance to send data home." },
            new MPart { id = "gnd.three", cat = MCat.Ground, name = "Three ground stations", hoursPerStation = 3, groundGain = 1, opsPerYear = 12,
                blurb = "Stations round the world: three times as many passes." },
            new MPart { id = "gnd.dsn", cat = MCat.Ground, name = "Deep Space Network", hoursPerStation = 3, groundGain = 10000, opsPerYear = 25, advanced = true,
                blurb = "Huge dishes round the world that hear faint signals from the planets (10,000 times better)." },
            // user segment
            new MPart { id = "usr.team", cat = MCat.Users, name = "Small science team", cost = 2, opsPerYear = 2,
                blurb = "A few scientists study the data." },
            new MPart { id = "usr.big", cat = MCat.Users, name = "Big science team", cost = 6, opsPerYear = 8, scienceMult = 1.3f,
                blurb = "More scientists find more in the same data (science x1.3)." },
            new MPart { id = "usr.open", cat = MCat.Users, name = "Open data for everyone", cost = 10, opsPerYear = 10, scienceMult = 1.5f,
                blurb = "Data put online for every scientist and school: the most science (x1.5), the most cost." },
        };

        public static readonly MVehicle[] Vehicles =
        {
            new MVehicle { id = "lv.sprite", name = "Sprite", leo = 400, high = 120, moon = 0, planet = 0, cost = 25, reliability = 0.94f,
                blurb = "A small rocket for little spacecraft in low orbit. Cheap, fails a little more often." },
            new MVehicle { id = "lv.kestrel", name = "Kestrel", leo = 2200, high = 900, moon = 520, planet = 260, cost = 70, reliability = 0.97f,
                blurb = "The workhorse: medium spacecraft to any orbit of Arden, small ones to the moons and planets." },
            new MVehicle { id = "lv.broadside", name = "Broadside", leo = 8000, high = 3500, moon = 2200, planet = 1300, cost = 160, reliability = 0.98f,
                blurb = "A heavy rocket for big spacecraft and the planets." },
            new MVehicle { id = "lv.goliath", name = "Goliath", leo = 25000, high = 11000, moon = 7000, planet = 4200, cost = 380, reliability = 0.96f,
                blurb = "Super-heavy. Lifts anything, costs a fortune, and it's new, so a little less proven." },
        };

        public static readonly MScenario[] Scenarios =
        {
            new MScenario { id = "storm", name = "Storm Watch", body = "Arden", difficulty = 0, tags = new[] { "weather" }, coverageNeeded = 0.9f, scienceNeeded = 500, lifeYears = 2, budget = 180,
                goal = "Watch the weather over all of Arden for 2 years",
                brief = "Farmers and sailors want warning of storms. See the weather everywhere on Arden, poles included, at least once a day, for 2 years." },
            new MScenario { id = "taltmap", name = "Map Tal", body = "Tal", difficulty = 1, tags = new[] { "map" }, coverageNeeded = 0.9f, scienceNeeded = 700, lifeYears = 1, budget = 260,
                goal = "Map 90% of Tal's surface",
                brief = "Before anyone lands again, we need a full map of Tal: every crater, every mountain, near side and far side." },
            new MScenario { id = "relay", name = "Talk to the Far Side", body = "Tal", difficulty = 1, tags = new[] { "relay" }, relayNeeded = 0.45f, lifeYears = 5, budget = 200,
                goal = "Relay messages from Tal's far side for 5 years",
                brief = "Landers on Tal's far side can't see Arden. Put a relay where it can see both, as much of the time as possible." },
            new MScenario { id = "ice", name = "Ice Hunter", body = "Vesk", difficulty = 2, tags = new[] { "ice" }, coverageNeeded = 0.7f, scienceNeeded = 900, dataNeededGB = 300, lifeYears = 2, budget = 420,
                goal = "Find Vesk's ice: 70% coverage, 300 GB home",
                brief = "Vesk's ice could be fuel for future ships. Find where it is under the surface and send home at least 300 GB of data." },
            new MScenario { id = "ochre", name = "Red Planet Orbiter", body = "Ochre", difficulty = 2, tags = new[] { "minerals", "weather", "map" }, coverageNeeded = 0.8f, scienceNeeded = 1200, dataNeededGB = 150, lifeYears = 3, budget = 650,
                goal = "Study Ochre from orbit for 3 years",
                brief = "Ochre is far: sunlight is weak, signals are faint, and getting into orbit takes a lot of fuel. Study its rocks and weather." },
            new MScenario { id = "sandbox", name = "Free design", body = "Arden", difficulty = 0, tags = new[] { "map" }, budget = 0, lifeYears = 2, sandbox = true,
                goal = "Your own mission",
                brief = "No budget limit: pick a world and what to study, and try anything." },
        };

        public static MPart Part(string id) => Parts.FirstOrDefault(p => p.id == id);

        /// <summary>The campaign's missions, in order (no free design).</summary>
        public static MScenario[] CampaignOrder => Scenarios.Where(s => !s.sandbox).ToArray();

        /// <summary>Is a mission open in a campaign with this many unlocked?</summary>
        public static bool OpenInCampaign(string id, int unlocked)
        {
            int i = Array.FindIndex(CampaignOrder, s => s.id == id);
            return i >= 0 && i < Math.Max(1, unlocked);
        }

        /// <summary>How many campaign missions are open: the first, and each one after a mission done (one star or more).</summary>
        public static int UnlockedFrom(Func<string, int> bestStars)
        {
            int n = 1;
            foreach (var s in CampaignOrder) { if (bestStars(s.id) >= 1) n++; else break; }
            return Math.Min(n, CampaignOrder.Length);
        }

        /// <summary>A design that does each mission (for "start from an example", and the tests).</summary>
        public static MissionPlan Example(string scenario)
        {
            var p = new MissionPlan { scenario = scenario, body = Scenario(scenario).body };
            switch (scenario)
            {
                case "storm":
                    p.bus = "bus.box"; p.power = "sol.l"; p.wings = 2; p.battery = "bat.l"; p.attitude = "att.rw"; p.computer = "cpu.fast"; p.antenna = "ant.dish";
                    p.propulsion = "prop.s"; p.thermal = "th.blanket"; p.ground = "gnd.three"; p.users = "usr.team"; p.vehicle = "lv.sprite"; p.orbit = MOrbit.SunSync;
                    p.instruments = new List<string> { "ins.sounder", "ins.cam" };
                    break;
                case "taltmap":
                    p.bus = "bus.box"; p.power = "sol.l"; p.wings = 2; p.battery = "bat.s"; p.attitude = "att.rw"; p.computer = "cpu.fast"; p.antenna = "ant.dish";
                    p.propulsion = "prop.m"; p.thermal = "th.blanket"; p.ground = "gnd.one"; p.users = "usr.team"; p.vehicle = "lv.kestrel"; p.orbit = MOrbit.Polar;
                    p.instruments = new List<string> { "ins.cam", "ins.sharp", "ins.alt" };
                    break;
                case "relay":
                    p.bus = "bus.box"; p.power = "sol.l"; p.wings = 1; p.battery = "bat.s"; p.attitude = "att.rw"; p.computer = "cpu.basic"; p.antenna = "ant.dish";
                    p.propulsion = "prop.m"; p.thermal = "th.blanket"; p.ground = "gnd.one"; p.users = "usr.team"; p.vehicle = "lv.kestrel"; p.orbit = MOrbit.High;
                    p.instruments = new List<string> { "ins.relay" };
                    break;
                case "ice":
                    p.bus = "bus.box"; p.power = "sol.l"; p.wings = 2; p.battery = "bat.l"; p.attitude = "att.rw"; p.computer = "cpu.fast"; p.antenna = "ant.dish";
                    p.propulsion = "prop.m"; p.thermal = "th.blanket"; p.ground = "gnd.three"; p.users = "usr.team"; p.vehicle = "lv.kestrel"; p.orbit = MOrbit.Polar;
                    p.instruments = new List<string> { "ins.ice", "ins.spec", "ins.therm" };
                    break;
                case "ochre":
                    p.bus = "bus.box"; p.power = "sol.l"; p.wings = 3; p.battery = "bat.s"; p.attitude = "att.rw"; p.computer = "cpu.fast"; p.antenna = "ant.deep";
                    p.propulsion = "prop.l"; p.thermal = "th.heat"; p.ground = "gnd.dsn"; p.users = "usr.team"; p.vehicle = "lv.goliath"; p.orbit = MOrbit.Polar;
                    p.instruments = new List<string> { "ins.cam", "ins.spec" };
                    break;
                default:
                    p.instruments = new List<string> { "ins.cam" };
                    break;
            }
            return p;
        }
        public static MVehicle Vehicle(string id) => Vehicles.FirstOrDefault(v => v.id == id) ?? Vehicles[1];
        public static MScenario Scenario(string id) => Scenarios.FirstOrDefault(s => s.id == id) ?? Scenarios[0];
        public static IEnumerable<MPart> In(MCat c) => Parts.Where(p => p.cat == c);

        /// <summary>Which role chooses each kind of thing.</summary>
        public static MRole RoleFor(MCat c) => c == MCat.Instrument || c == MCat.Users ? MRole.Scientist
            : c == MCat.Antenna || c == MCat.Ground ? MRole.Comms
            : MRole.Engineer;

        public static readonly string[] RoleNames = { "Mission lead", "Spacecraft engineer", "Science lead", "Comms and ground" };
        public static readonly string[] RoleJobs =
        {
            "picks the goal, the orbit and the rocket, and says go",
            "builds the spacecraft: frame, power, batteries, pointing, computer, engine, heaters",
            "picks the instruments and who uses the data",
            "picks the antenna and the ground stations that bring the data home",
        };

        public static MScenario ScenarioOf(MissionPlan p)
        {
            var s = Scenario(p.scenario);
            if (!s.sandbox) return s;
            return new MScenario { id = s.id, name = s.name, body = p.body, sandbox = true, budget = 0, tags = new[] { p.sandboxTag }, lifeYears = p.sandboxYears,
                coverageNeeded = p.sandboxTag == "relay" || p.sandboxTag == "space" ? 0 : 0.6f, relayNeeded = p.sandboxTag == "relay" ? 0.4f : 0, scienceNeeded = 300,
                goal = $"Study {p.body}: {p.sandboxTag}", brief = s.brief };
        }

        public static readonly string[] OrbitNames = { "Low orbit", "Polar orbit", "Sun-synchronous", "Medium orbit", "High orbit", "Stationary orbit" };

        public static string OrbitBlurb(MOrbit o) => o switch
        {
            MOrbit.Low => "Close to the ground over the equator: sharp views, quick to reach, but it never passes over the poles and spends a lot of each orbit in shadow.",
            MOrbit.Polar => "Close, and passing over both poles: as the world turns underneath, it sees everywhere. Shadow on every orbit.",
            MOrbit.SunSync => "A polar orbit tilted so it always rides the line between day and night: always in sunlight (no batteries needed for night), sees everywhere. Arden only.",
            MOrbit.Medium => "Higher up: sees a wider area at once and spends less time in shadow, but in less detail.",
            MOrbit.High => "Far out: almost never in shadow and can see a whole side of the world at once, but the detail is poor.",
            MOrbit.Stationary => "Goes round once a day, exactly as fast as Arden turns, so it hangs over one spot: always in touch with the space centre, but sees only one side. Arden only.",
            _ => ""
        };

        /// <summary>Orbits that make sense at this world (stationary and sun-synchronous are Arden's).</summary>
        public static IEnumerable<MOrbit> OrbitsAt(CelestialBody b)
        {
            foreach (MOrbit o in Enum.GetValues(typeof(MOrbit)))
            {
                if ((o == MOrbit.SunSync || o == MOrbit.Stationary) && !b.IsHomeWorld) continue;
                yield return o;
            }
        }

        // ---------------------------------------------------------------- the geometry of each orbit

        public static double AltitudeOf(CelestialBody b, MOrbit o)
        {
            double low = b.IsHomeWorld ? 320_000 : Math.Max(b.atmosphereHeight + 60_000, b.radius * 0.1 + Terrain.MaxHeight(b));
            if (b.HasAtmosphere && !b.IsHomeWorld) low = Math.Max(low, b.atmosphereHeight * 1.5);
            switch (o)
            {
                case MOrbit.Medium: return b.radius * 1.2;
                case MOrbit.High: return b.radius * 5;
                case MOrbit.Stationary:
                    double T = Math.Abs(b.rotationPeriod) > 1 ? Math.Abs(b.rotationPeriod) : 86400;
                    return Math.Pow(b.mu * T * T / (4 * Math.PI * Math.PI), 1.0 / 3) - b.radius;
                default: return low;
            }
        }

        public static MDest DestinationOf(CelestialBody b, MOrbit o)
        {
            if (b.IsHomeWorld) return o == MOrbit.Low || o == MOrbit.Polar || o == MOrbit.SunSync ? MDest.Leo : MDest.High;
            return b.parent != null && b.parent.IsHomeWorld ? MDest.Moon : MDest.Planet;
        }

        /// <summary>Speed the spacecraft's own engine must find to slow into orbit there (m/s), and to keep it up each year.</summary>
        public static void DeltaV(CelestialBody b, MOrbit o, out double insert, out double perYear)
        {
            insert = 0; perYear = 5;
            if (b.IsHomeWorld)
            {
                if (o == MOrbit.Stationary) perYear = 50;                 // keeping on station
                else if (o == MOrbit.Low || o == MOrbit.Polar || o == MOrbit.SunSync) perYear = 30;   // the thin upper air
                return;
            }
            double r = b.radius + AltitudeOf(b, o);
            double vCirc = Math.Sqrt(b.mu / r), vEsc = Math.Sqrt(2 * b.mu / r);
            double arrive = b.parent != null && b.parent.IsHomeWorld ? 700 : b.parent != null && !b.parent.isStar ? 3500 : 2600;   // speed left over on arrival
            double vIn = Math.Sqrt(arrive * arrive + vEsc * vEsc);
            insert = vIn - vCirc;
        }

        /// <summary>Share of each orbit spent in the world's shadow.</summary>
        public static double EclipseOf(CelestialBody b, MOrbit o)
        {
            if (o == MOrbit.SunSync) return 0;
            double r = b.radius + AltitudeOf(b, o);
            double f = Math.Asin(Math.Min(1, b.radius / r)) / Math.PI;
            return o == MOrbit.Stationary ? 0.02 : f;
        }

        /// <summary>The most of the surface this orbit can ever see.</summary>
        public static double MaxCoverageOf(CelestialBody b, MOrbit o)
        {
            double r = b.radius + AltitudeOf(b, o);
            double view = Math.Acos(Math.Min(1, b.radius / r));            // half-angle of the ground in view
            switch (o)
            {
                case MOrbit.Polar: case MOrbit.SunSync: return 1;
                case MOrbit.Stationary: return (1 - b.radius / r) * 0.5;
                default:
                    double latMax = Math.Min(Math.PI / 2, view * 0.75);       // equatorial: only the middle band
                    return Math.Sin(latMax);
            }
        }

        public static double PeriodOf(CelestialBody b, MOrbit o)
        {
            double r = b.radius + AltitudeOf(b, o);
            return 2 * Math.PI * Math.Sqrt(r * r * r / b.mu);
        }

        /// <summary>How much detail pictures have from this height (1 from a low orbit).</summary>
        public static double ResolutionOf(CelestialBody b, MOrbit o) => Math.Max(0.15, Math.Min(1, Math.Sqrt(AltitudeOf(b, MOrbit.Low) / AltitudeOf(b, o))));

        /// <summary>Sunlight there compared with at Arden.</summary>
        public static double SolarFactor(CelestialBody b)
        {
            CelestialBody planet = b;
            while (planet.parent != null && !planet.parent.isStar) planet = planet.parent;
            double arden = 13.6e9;
            return Math.Pow(arden / Math.Max(1e9, planet.orbitRadius), 2);
        }

        /// <summary>How far the radio signal goes home (km).</summary>
        public static double CommDistanceKm(CelestialBody b, MOrbit o)
        {
            if (b.IsHomeWorld) return o == MOrbit.Stationary ? AltitudeOf(b, o) / 1000 : o == MOrbit.High || o == MOrbit.Medium ? AltitudeOf(b, o) / 1000 * 1.2 : 1500;
            if (b.parent != null && b.parent.IsHomeWorld) return b.orbitRadius / 1000;
            CelestialBody planet = b;
            while (planet.parent != null && !planet.parent.isStar) planet = planet.parent;
            double a = 13.6e9, p = planet.orbitRadius;
            return Math.Sqrt(a * a + p * p) / 1000;
        }

        /// <summary>Hours a day in touch with home, with this many ground stations.</summary>
        public static double ContactHours(CelestialBody b, MOrbit o, MPart ground)
        {
            double perStation;
            if (b.IsHomeWorld)
                perStation = o == MOrbit.Stationary ? 24 : o == MOrbit.High ? 10 : o == MOrbit.Medium ? 5 : o == MOrbit.Low ? 0.6 : 0.9;
            else
                perStation = 9 * (1 - EclipseOf(b, o));                // when home is above the horizon, and the world isn't in the way
            return Math.Min(24, perStation * ground.hoursPerStation);
        }

        public static bool HasTag(MPart p, string tag) => p.tags != null && p.tags.Contains(tag);
        public static bool Wanted(MScenario s, MPart p) => p.tags != null && p.tags.Any(t => s.tags.Contains(t));

        // ---------------------------------------------------------------- working it all out

        public static MSheet Evaluate(MissionPlan plan, IList<CelestialBody> bodies, bool easy = false)
        {
            var s = new MSheet();
            var sc = ScenarioOf(plan);
            s.scenario = sc;
            var b = bodies.FirstOrDefault(x => x.name == sc.body) ?? bodies[0];
            s.body = b;
            var bus = Part(plan.bus) ?? Part("bus.box");
            var pw = Part(plan.power) ?? Part("sol.l");
            var bat = Part(plan.battery) ?? Part("bat.s");
            var att = Part(plan.attitude) ?? Part("att.rw");
            var cpu = Part(plan.computer) ?? Part("cpu.basic");
            var ant = Part(plan.antenna) ?? Part("ant.low");
            var prop = Part(plan.propulsion) ?? Part("prop.none");
            var th = Part(plan.thermal) ?? Part("th.blanket");
            var gnd = Part(plan.ground) ?? Part("gnd.one");
            var usr = Part(plan.users) ?? Part("usr.team");
            var lv = Vehicle(plan.vehicle);
            var ins = plan.instruments.Select(Part).Where(p => p != null && p.cat == MCat.Instrument).ToList();
            int wings = pw.rtgW > 0 ? 1 : Math.Max(1, Math.Min(4, plan.wings));
            var o = OrbitsAt(b).Contains(plan.orbit) ? plan.orbit : MOrbit.Low;

            // mass
            float dry = bus.mass + pw.mass * wings + bat.mass + att.mass + cpu.mass + ant.mass + prop.mass + th.mass + ins.Sum(i => i.mass);
            s.payloadKg = dry - bus.mass;
            s.massKg = dry + prop.fuelKg;
            // money
            s.costM = bus.cost + pw.cost * wings + bat.cost + att.cost + cpu.cost + ant.cost + prop.cost + th.cost + ins.Sum(i => i.cost) + usr.cost + lv.cost;
            s.opsPerYearM = gnd.opsPerYear + usr.opsPerYear;
            s.totalCostM = s.costM + s.opsPerYearM * sc.lifeYears;
            s.devMonths = 18 + ins.Count * 3 + (bus.id == "bus.big" ? 12 : bus.id == "bus.cube" ? -8 : 0) + ins.Count(i => i.advanced) * 4 + (pw.rtgW > 0 ? 8 : 0);
            // orbit
            s.altitudeKm = (float)(AltitudeOf(b, o) / 1000);
            s.periodMin = (float)(PeriodOf(b, o) / 60);
            s.eclipse = (float)EclipseOf(b, o);
            s.maxCoverage = (float)MaxCoverageOf(b, o);
            s.resolution = (float)ResolutionOf(b, o);
            s.solarFactor = (float)SolarFactor(b);
            s.commDistanceKm = (float)CommDistanceKm(b, o);
            s.dest = DestinationOf(b, o);
            s.launchCapacity = lv.CapacityFor(s.dest);
            // pointing (rougher with magnetic torquers away from Arden)
            float pointing = att.pointing * (att.id == "att.mag" && !b.IsHomeWorld ? 2.5f : 1f);
            float worst = 1f;
            foreach (var i in ins.Where(i => i.needPointing > 0)) worst = Math.Min(worst, (float)Math.Min(1, Math.Sqrt(i.needPointing / pointing)));
            s.pointingQuality = worst;
            // power
            float hot = th.heaterW * (s.solarFactor < 0.7f ? 1f : 0f);
            float instW = ins.Sum(i => i.power);
            s.loadW = bus.idleW + att.power + cpu.power + ant.power * 0.5f + hot + instW;
            float solar = pw.solarW * wings * s.solarFactor;
            s.sunW = solar + pw.rtgW;
            s.nightW = pw.rtgW;
            double nightHours = PeriodOf(b, o) / 3600 * s.eclipse;
            float nightLoad = bus.idleW + att.power + cpu.power + hot + ins.Where(i => i.night).Sum(i => i.power);
            s.nightNeedWh = (float)Math.Max(0, (nightLoad - s.nightW) * nightHours);
            s.batteryUsableWh = bat.batteryWh * 0.6f;
            // data
            s.dataPerDayGB = ins.Sum(i => i.dataGB);
            s.contactHours = (float)ContactHours(b, o, gnd);
            double dGm = s.commDistanceKm / 1e6;
            float antGood = ant.needPointing > 0 ? (float)Math.Min(1, Math.Sqrt(ant.needPointing / pointing)) : 1f;
            s.rateMbps = (float)Math.Min(ant.maxMbps, ant.gain * gnd.groundGain / Math.Max(1e-9, dGm * dGm)) * antGood;
            s.downPerDayGB = s.rateMbps * s.contactHours * 3600f / 8000f;
            s.storageGB = cpu.storageGB;
            // fuel
            DeltaV(b, o, out double insert, out double perYear);
            s.dvNeed = (float)(insert + perYear * sc.lifeYears);
            s.dvHave = prop.fuelKg > 0 ? (float)(prop.isp * 9.81 * Math.Log(s.massKg / Math.Max(1, s.massKg - prop.fuelKg))) : 0;
            // how long it'll last
            float life = bus.lifeYears;
            if (s.dvHave < insert) life = 0;
            else if (perYear > 0) life = (float)Math.Min(life, (s.dvHave - insert) / perYear);
            if (prop.fuelKg <= 0 && b.IsHomeWorld && (o == MOrbit.Low || o == MOrbit.Polar || o == MOrbit.SunSync)) life = Math.Min(life, 1.4f);   // drag brings it down
            if (prop.fuelKg <= 0 && b.IsHomeWorld && o == MOrbit.Stationary) life = Math.Min(life, 3f);                                        // drifts off station
            if (s.solarFactor < 0.7f && th.heaterW <= 0) life = Math.Min(life, 0.4f);
            s.lifeYearsEstimate = life;
            s.sciencePerDay = ins.Sum(i => i.science * (Wanted(sc, i) ? 1f : 0.3f)) * usr.scienceMult;

            // ---- problems that stop a launch
            if (s.massKg > s.launchCapacity) s.blockers.Add(s.launchCapacity <= 0
                ? $"The {lv.name} can't reach {b.name} at all. Pick a bigger rocket."
                : $"Too heavy for the {lv.name}: {s.massKg:0} kg, but it can only carry {s.launchCapacity:0} kg to {DestText(s.dest, b)}.");
            if (s.payloadKg > bus.capacityKg) s.blockers.Add($"The {bus.name} can only carry {bus.capacityKg:0} kg of parts; you've put {s.payloadKg:0} kg on it.");
            if (ins.Count > bus.slots) s.blockers.Add($"The {bus.name} has room for {bus.slots:0} instruments; you've picked {ins.Count}.");
            if (ins.Count == 0) s.blockers.Add("No instruments: pick at least one (the science lead's job).");
            if (sc.budget > 0 && s.totalCostM > sc.budget) s.blockers.Add($"Over budget: {s.totalCostM:0} M to build and run for {sc.lifeYears:0.#} years, but the budget is {sc.budget:0} M.");
            if (pw.rtgW <= 0 && s.solarFactor < 0.02f) s.blockers.Add("Too far from the sun for solar power: it needs a nuclear battery.");

            // ---- warnings: it'll fly, but not well
            if (s.sunW < s.loadW) s.warnings.Add($"Not enough power: it needs {s.loadW:0} W in sunlight but makes only {s.sunW:0} W, so instruments will have to take turns. More or bigger wings, or fewer instruments.");
            if (s.nightNeedWh > s.batteryUsableWh) s.warnings.Add($"The battery is too small for the night side: each orbit needs {s.nightNeedWh:0} Wh but only {s.batteryUsableWh:0} Wh is safe to use. Instruments will switch off in the dark.");
            if (s.dataPerDayGB > s.downPerDayGB * 1.05f) s.warnings.Add($"Instruments make {s.dataPerDayGB:0.#} GB a day but only {s.downPerDayGB:0.##} GB can be sent home ({s.rateMbps:0.###} Mbps for {s.contactHours:0.#} h a day). The rest is lost. A dish, more ground stations, or fewer instruments.");
            float between = s.contactHours > 0 ? (24 - s.contactHours) / 24f * s.dataPerDayGB : s.dataPerDayGB;
            if (s.storageGB < between && s.dataPerDayGB <= s.downPerDayGB) s.warnings.Add($"The computer can store {s.storageGB:0} GB but {between:0} GB piles up between passes. A computer with more storage.");
            if (s.pointingQuality < 0.95f) s.warnings.Add($"{att.name} point to about {pointing:0.##} deg: too rough for some of your instruments or antenna (blurry pictures, weaker signal).");
            if (s.resolution < 0.6f && ins.Any(i => HasTag(i, "map"))) s.warnings.Add($"From {s.altitudeKm:0} km up, pictures have only {s.resolution * 100:0}% of the detail of a low orbit.");
            if (sc.coverageNeeded > 0 && s.maxCoverage < sc.coverageNeeded) s.warnings.Add($"A {OrbitNames[(int)o].ToLower()} can only ever see {s.maxCoverage * 100:0}% of {b.name}; the goal needs {sc.coverageNeeded * 100:0}%. A polar orbit sees everything.");
            if (s.dvHave < insert) s.warnings.Add($"Not enough fuel to slow into orbit at {b.name}: it needs {insert:0} m/s, the spacecraft has {s.dvHave:0} m/s. It'll fly straight past.");
            else if (life < sc.lifeYears) s.warnings.Add($"It'll likely last about {life:0.#} years, less than the {sc.lifeYears:0.#} the goal needs ({LifeReason(prop, th, b, o, s, bus)}).");
            foreach (var i in ins.Where(i => !Wanted(sc, i) && !HasTag(i, "relay"))) s.warnings.Add($"The {i.name} doesn't help with this goal much ({string.Join(", ", sc.tags)}).");
            if (ins.Any(i => HasTag(i, "weather") && i.id == "ins.sounder") && !b.HasAtmosphere) s.warnings.Add($"{b.name} has no air: the weather sounder has nothing to measure.");

            // ---- the gauges
            s.gauges.Add(G("Mass", "kg", s.massKg, Math.Max(1, s.launchCapacity), $"The spacecraft against what the {lv.name} can lift to {DestText(s.dest, b)}."));
            s.gauges.Add(G("Power", "W", s.loadW, Math.Max(1, s.sunW), "What it uses in sunlight against what it makes."));
            s.gauges.Add(G("Night", "Wh", s.nightNeedWh, Math.Max(1, s.batteryUsableWh), $"Battery needed for {s.eclipse * 100:0}% of each orbit in shadow."));
            if (sc.budget > 0) s.gauges.Add(G("Budget", "M", s.totalCostM, sc.budget, $"Building {s.costM:0} M + running {s.opsPerYearM:0} M a year for {sc.lifeYears:0.#} years."));
            else s.gauges.Add(new MGauge { name = "Budget", unit = "M", used = s.totalCostM, have = s.totalCostM, state = 0, text = "Free design: no limit." });
            s.gauges.Add(G("Data", "GB/day", s.dataPerDayGB, Math.Max(0.001f, s.downPerDayGB), "What the instruments make against what can be sent home."));
            s.gauges.Add(new MGauge { name = "Contact", unit = "h/day", used = s.contactHours, have = 24, state = s.contactHours < 1 ? 1 : 0, text = $"In touch with home {s.contactHours:0.#} h a day, {s.rateMbps:0.###} Mbps over {Dist(s.commDistanceKm)}." });
            s.gauges.Add(G("Fuel", "m/s", s.dvNeed, Math.Max(1, s.dvHave), "Speed changes needed (into orbit, keeping it up) against what the fuel gives."));
            s.gauges.Add(new MGauge { name = "Life", unit = "years", used = sc.lifeYears, have = Math.Max(0.01f, life), state = life >= sc.lifeYears ? 0 : life >= sc.lifeYears * 0.7f ? 1 : 2, text = $"Goal {sc.lifeYears:0.#} years; this design should last about {life:0.#}. {s.devMonths:0} months to build." });

            // ---- how far along the design is (after NASA's concept maturity levels)
            s.maturity = 1 + (ins.Count > 0 ? 1 : 0) + (s.blockers.Count == 0 ? 1 : 0) + (s.warnings.Count <= 2 ? 1 : 0) + (s.warnings.Count == 0 ? 1 : 0) + (s.blockers.Count == 0 && s.warnings.Count == 0 && life >= sc.lifeYears ? 1 : 0);
            return s;
        }

        // ---------------------------------------------------------------- milestone 31: the Space Apps check

        /// <summary>
        /// Real missions that did something like this (after the challenge's telescope game, which shows players the real
        /// missions their design resembles). Facts only: names, years, orbits and instruments.
        /// </summary>
        public static string RealMissions(MissionPlan plan)
        {
            var sc = ScenarioOf(plan);
            string key = sc.sandbox ? plan.sandboxTag : sc.id;
            string s = key switch
            {
                "storm" or "weather" => "NOAA-20 and NOAA-21 (weather satellites built with NASA) fly a sun-synchronous polar orbit with weather sounders and a wide camera, seeing the whole Earth twice a day. GOES weather satellites watch from a stationary orbit instead, always over the same side.",
                "taltmap" or "map" => "NASA's Lunar Reconnaissance Orbiter (2009) maps the Moon from a low polar orbit with a sharp narrow camera, a wide camera and a laser altimeter that measured the height of the whole surface.",
                "relay" => "China's Queqiao relay (2018) hangs beyond the Moon so the Chang'e 4 lander on the far side could talk to Earth. At Mars, NASA's Odyssey and Mars Reconnaissance Orbiter relay the rovers' messages home.",
                "ice" => "NASA's Lunar Prospector (1998) found signs of ice at the Moon's poles, and the radar on Mars Reconnaissance Orbiter maps ice buried under Mars. Europa Clipper (launched 2024) carries an ice-penetrating radar to look under Europa's ice.",
                "ochre" or "minerals" => "NASA's Mars Reconnaissance Orbiter (2005) has studied Mars from a low polar orbit with a very sharp camera (HiRISE), a mineral spectrometer (CRISM) and a weather sounder, sending it all home through a 3-metre dish and the Deep Space Network.",
                "volcano" => "NASA's Juno (launched 2011) has watched the volcanoes of Jupiter's moon Io with its infrared camera.",
                "space" => "NASA's twin Van Allen Probes (2012 to 2019) measured the radiation belts around Earth with magnetometers and particle counters, like the small instruments here.",
                _ => "",
            };
            if (plan.bus == "bus.cube") s += " A tiny spacecraft like yours: NASA's MarCO (2018) were two briefcase-size CubeSats that relayed news of InSight's Mars landing as it happened.";
            if (plan.power == "rtg") s += " A nuclear battery like yours powers Voyager 1 and 2 and the Curiosity and Perseverance rovers, and powered Cassini.";
            return s.Trim();
        }

        /// <summary>One line of a side-by-side comparison: the two values and which is better (0 neither, 1 the first, 2 the second).</summary>
        public struct MCompareRow { public string name, a, b; public int winner; }

        /// <summary>Two designs side by side, line by line (a trade study): which carries more margin, costs less, lasts longer...</summary>
        public static List<MCompareRow> Compare(MSheet a, MSheet b)
        {
            var rows = new List<MCompareRow>();
            void Row(string name, string ta, string tb, double va, double vb, bool higherBetter)
            {
                int w = 0;
                double tol = 1e-3 * Math.Max(1, Math.Max(Math.Abs(va), Math.Abs(vb)));
                if (Math.Abs(va - vb) > tol) w = (va > vb) == higherBetter ? 1 : 2;
                rows.Add(new MCompareRow { name = name, a = ta, b = tb, winner = w });
            }
            static float DataShare(MSheet s) => s.dataPerDayGB <= 1e-6f ? 1f : Math.Min(1f, s.downPerDayGB / s.dataPerDayGB);
            rows.Add(new MCompareRow { name = "Mission", a = a.scenario.name, b = b.scenario.name });
            Row("Rocket spare", $"{a.launchCapacity - a.massKg:+0;-0} kg", $"{b.launchCapacity - b.massKg:+0;-0} kg", a.launchCapacity - a.massKg, b.launchCapacity - b.massKg, true);
            Row("Cost", $"{a.totalCostM:0} M", $"{b.totalCostM:0} M", a.totalCostM, b.totalCostM, false);
            Row("Spare power", $"{a.sunW - a.loadW:+0;-0} W", $"{b.sunW - b.loadW:+0;-0} W", a.sunW - a.loadW, b.sunW - b.loadW, true);
            Row("Night battery", $"{a.batteryUsableWh - a.nightNeedWh:+0;-0} Wh", $"{b.batteryUsableWh - b.nightNeedWh:+0;-0} Wh", a.batteryUsableWh - a.nightNeedWh, b.batteryUsableWh - b.nightNeedWh, true);
            Row("Data home", $"{DataShare(a) * 100:0}% of {a.dataPerDayGB:0.#} GB", $"{DataShare(b) * 100:0}% of {b.dataPerDayGB:0.#} GB", DataShare(a) * 1000 + Math.Min(a.downPerDayGB, a.dataPerDayGB), DataShare(b) * 1000 + Math.Min(b.downPerDayGB, b.dataPerDayGB), true);
            Row("In touch", $"{a.contactHours:0.#} h/day", $"{b.contactHours:0.#} h/day", a.contactHours, b.contactHours, true);
            Row("Spare fuel", $"{a.dvHave - a.dvNeed:+0;-0} m/s", $"{b.dvHave - b.dvNeed:+0;-0} m/s", a.dvHave - a.dvNeed, b.dvHave - b.dvNeed, true);
            Row("Lifetime", $"{a.lifeYearsEstimate:0.#} years", $"{b.lifeYearsEstimate:0.#} years", a.lifeYearsEstimate, b.lifeYearsEstimate, true);
            Row("Science a day", $"{a.sciencePerDay:0.#}", $"{b.sciencePerDay:0.#}", a.sciencePerDay, b.sciencePerDay, true);
            Row("Build time", $"{a.devMonths:0} months", $"{b.devMonths:0} months", a.devMonths, b.devMonths, false);
            Row("Maturity", $"level {a.maturity}", $"level {b.maturity}", a.maturity, b.maturity, true);
            static string Problems(MSheet s) => (s.blockers.Count > 0 ? $"{s.blockers.Count} to fix first, " : "") + (s.warnings.Count == 0 ? (s.blockers.Count > 0 ? "no warnings" : "none") : s.warnings.Count == 1 ? "1 warning" : $"{s.warnings.Count} warnings");
            Row("Problems", Problems(a), Problems(b), a.blockers.Count * 10 + a.warnings.Count, b.blockers.Count * 10 + b.warnings.Count, false);
            return rows;
        }

        /// <summary>What the comparison says, in a sentence or two.</summary>
        public static string CompareVerdict(List<MCompareRow> rows, MSheet a, MSheet b, string na, string nb)
        {
            int wa = rows.Count(r => r.winner == 1), wb = rows.Count(r => r.winner == 2);
            if (wa == 0 && wb == 0) return "These two are the same design.";
            string s = $"{na} is better on {wa} lines, {nb} on {wb}. ";
            if (a.scenario.id != b.scenario.id) s += "They're for different missions, so it isn't quite a fair race. ";
            if (a.blockers.Count > 0 || b.blockers.Count > 0) s += (a.blockers.Count > 0 && b.blockers.Count > 0 ? "Neither" : a.blockers.Count > 0 ? na : nb) + (a.blockers.Count > 0 && b.blockers.Count > 0 ? " can launch yet. " : " can't launch yet. ");
            return s + "There's rarely one perfect design: pick what matters most for the goal.";
        }

        static string LifeReason(MPart prop, MPart th, CelestialBody b, MOrbit o, MSheet s, MPart bus)
        {
            if (s.solarFactor < 0.7f && th.heaterW <= 0) return "it's too cold out there without heaters";
            if (prop.fuelKg <= 0 && b.IsHomeWorld && o != MOrbit.Medium && o != MOrbit.High) return o == MOrbit.Stationary ? "with no engine it drifts off station" : "with no engine, the thin upper air slowly drags it down";
            if (s.dvHave < s.dvNeed) return "it runs out of fuel keeping its orbit";
            return $"the {bus.name} is built for {bus.lifeYears:0} years";
        }

        static MGauge G(string name, string unit, float used, float have, string text)
        {
            float k = used / Math.Max(1e-6f, have);
            return new MGauge { name = name, unit = unit, used = used, have = have, text = text, state = k <= 0.85f ? 0 : k <= 1.0001f ? 1 : 2 };
        }

        public static string DestText(MDest d, CelestialBody b) => d == MDest.Leo ? "low Arden orbit" : d == MDest.High ? "high Arden orbit" : $"{b.name}";
        public static string Dist(double km) => km >= 1e6 ? $"{km / 1e6:0.#} million km" : km >= 10_000 ? $"{km / 1000:0} thousand km" : $"{km:0} km";
    }
}
