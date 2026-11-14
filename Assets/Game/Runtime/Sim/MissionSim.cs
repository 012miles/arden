using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    [Serializable]
    public class MReq
    {
        public string name, unit;
        public float need, got;
        public bool ok;
    }

    /// <summary>How a mission went: what it achieved, the score, what happened when, and why (it goes over the network as it is).</summary>
    [Serializable]
    public class MissionResult
    {
        public string scenario, headline, team;
        public bool launched, arrived, success;
        public int score, stars, teamBonus, seed;
        public float science, coverage, relayShare, dataGB, lostGB, years, goalYears, costM, budgetM, massKg;
        public List<MReq> reqs = new List<MReq>();
        public List<string> events = new List<string>();     // "day|text"
        public List<string> good = new List<string>(), bad = new List<string>();
        public List<float> tDays = new List<float>(), duty = new List<float>(), stored = new List<float>(), sent = new List<float>(), sci = new List<float>(), cov = new List<float>();
    }

    /// <summary>
    /// Milestone 29: flies a mission design day by day. Launch (rockets fail now and then), the trip and slowing into
    /// orbit, then every day: power in sunlight and through the night (instruments take turns when it's short), data made
    /// against data sent home (the rest is lost when the computer is full), science (blurry from too high or with rough
    /// pointing, worth more when it's what the goal needs and when more people use it), map coverage, solar panels
    /// wearing, and a few surprises. Then the goals are checked, a score worked out, and the report says why.
    /// </summary>
    public static class MissionSim
    {
        public static MissionResult Simulate(MissionPlan plan, IList<CelestialBody> bodies, int seed, bool easy = false)
        {
            var sheet = MissionDesign.Evaluate(plan, bodies, easy);
            var sc = sheet.scenario;
            var b = sheet.body;
            var r = new MissionResult { scenario = sc.id, seed = seed, goalYears = sc.lifeYears, costM = sheet.totalCostM, budgetM = sc.budget, massKg = sheet.massKg };
            var rng = new Random(seed);
            var lv = MissionDesign.Vehicle(plan.vehicle);
            var ins = plan.instruments.Select(MissionDesign.Part).Where(p => p != null && p.cat == MCat.Instrument).ToList();
            var pw = MissionDesign.Part(plan.power);
            var bat = MissionDesign.Part(plan.battery);
            var users = MissionDesign.Part(plan.users) ?? MissionDesign.Part("usr.team");
            var o = MissionDesign.OrbitsAt(b).Contains(plan.orbit) ? plan.orbit : MOrbit.Low;
            bool alive = true;
            int deathDay = 0;
            bool hasEngine = (MissionDesign.Part(plan.propulsion)?.fuelKg ?? 0) > 0;
            void Event(int day, string text) => r.events.Add($"{day}|{text}");

            // ---- launch
            r.launched = true;
            if (!sheet.CanLaunch) { r.launched = false; r.headline = "Not launched: " + sheet.blockers[0]; Finish(r, sheet, sc, ins, easy); return r; }
            if (!easy && rng.NextDouble() > lv.reliability)
            {
                Event(0, $"The {lv.name} failed on the way up. The spacecraft was lost.");
                r.headline = "Lost at launch";
                r.bad.Add($"The rocket failed. A {lv.name} works {lv.reliability * 100:0} times in 100: bad luck this time. Fly again, or pay for a more reliable rocket.");
                Finish(r, sheet, sc, ins, easy);
                return r;
            }
            Event(0, $"Launched on a {lv.name}.");
            // ---- the trip
            MissionDesign.DeltaV(b, o, out double insert, out double perYear);
            int cruise = b.IsHomeWorld ? 0 : b.parent != null && b.parent.IsHomeWorld ? 4 : 220;
            if (sheet.dvHave < insert)
            {
                Event(cruise, $"Reached {b.name} but couldn't slow down enough to stay (needed {insert:0} m/s, had {sheet.dvHave:0}). It flew past.");
                r.headline = $"Flew past {b.name}";
                r.bad.Add($"Not enough fuel to get into orbit: slowing down at {b.name} takes {insert:0} m/s and the spacecraft only had {sheet.dvHave:0} m/s. A bigger engine and tanks (heavier: check the rocket can still lift it).");
                Finish(r, sheet, sc, ins, easy);
                return r;
            }
            if (cruise > 0) Event(cruise, $"Into a {MissionDesign.OrbitNames[(int)o].ToLower()} round {b.name}, {sheet.altitudeKm:0} km up.");
            else Event(0, $"In a {MissionDesign.OrbitNames[(int)o].ToLower()}, {sheet.altitudeKm:0} km up, once round every {sheet.periodMin:0} minutes.");
            r.arrived = true;

            // ---- every day in orbit
            int goalDays = (int)(sc.lifeYears * 365);
            int lifeDays = (int)(sheet.lifeYearsEstimate * 365);
            int days = Math.Max(1, goalDays);
            double fuel = sheet.dvHave - insert;
            double store = 0, sent = 0, lost = 0, science = 0, cover = 0, relay = 0, relayDays = 0;
            float solarWear = 1f, batWear = 1f;
            var dead = new HashSet<string>();
            int safeUntil = -1;
            double R = b.radius / 1000.0, alt = sheet.altitudeKm;
            double groundTrackKmDay = 2 * Math.PI * R * (86400 / (sheet.periodMin * 60));
            double surface = 4 * Math.PI * R * R;
            double sampleEvery = Math.Max(1, days / 100.0), nextSample = 0;
            float baseLoad = sheet.loadW - ins.Sum(i => i.power);
            float sumDuty = 0; int dutyDays = 0;
            for (int d = 0; d < days && alive; d++)
            {
                double year = d / 365.0;
                // wear and surprises
                if (d > 0 && d % 365 == 0) { solarWear *= 0.975f; batWear *= 0.97f; }
                double p = (easy ? 0.5 : 1.0) / 365.0;
                if (rng.NextDouble() < 0.35 * p) { safeUntil = d + 6; Event(d, "A solar storm: the spacecraft went into safe mode for 6 days to protect itself."); }
                if (ins.Count > dead.Count && rng.NextDouble() < 0.10 * p)
                {
                    var hit = ins.Where(i => !dead.Contains(i.id)).ElementAt(rng.Next(ins.Count - dead.Count));
                    dead.Add(hit.id);
                    Event(d, $"A tiny meteoroid hit the {hit.name}. It stopped working.");
                }
                if (rng.NextDouble() < 0.45 * p) { store = 0; Event(d, "The computer restarted and lost the data it was holding."); }
                if (fuel > 0 && rng.NextDouble() < 0.03 * p) { fuel *= 0.6; Event(d, "A small fuel leak: 40% of the fuel left is gone."); }
                // keeping the orbit up
                fuel -= perYear / 365.0;
                if (d > lifeDays || (hasEngine && fuel < 0))
                {
                    string why = sheet.solarFactor < 0.7f && MissionDesign.Part(plan.thermal)?.heaterW <= 0 ? "The cold got into the electronics and the spacecraft stopped working."
                        : MissionDesign.Part(plan.propulsion)?.fuelKg <= 0 && b.IsHomeWorld && o != MOrbit.Medium && o != MOrbit.High ? (o == MOrbit.Stationary ? "With no engine it drifted off station and out of use." : "The thin upper air dragged the orbit down; it burned up in Arden's air.")
                        : fuel <= 0 ? "Out of fuel: it couldn't keep its orbit any more." : "It reached the end of its design life and wore out.";
                    Event(d, why);
                    alive = false;
                    deathDay = d;
                    break;
                }
                // power: what's left for instruments in sunlight, and whether the battery gets them through the night
                float sun = (float)((pw != null ? pw.solarW * Math.Max(1, Math.Min(4, pw.rtgW > 0 ? 1 : plan.wings)) * sheet.solarFactor : 0) * solarWear + (pw?.rtgW ?? 0));
                float instW = ins.Where(i => !dead.Contains(i.id)).Sum(i => i.power);
                float dutySun = instW > 0 ? Clamp01((sun - baseLoad) / instW) : 1f;
                bool nightOk = sheet.nightNeedWh <= (bat?.batteryWh ?? 0) * 0.6f * batWear + 1e-3f;
                bool safe = d <= safeUntil;
                float ecl = sheet.eclipse;
                // data and science
                double gen = 0;
                var dutyOf = new Dictionary<string, float>();
                foreach (var i in ins)
                {
                    if (dead.Contains(i.id) || safe) { dutyOf[i.id] = 0; continue; }
                    float lit = 1 - ecl;
                    float du = dutySun * (i.night ? (lit + (nightOk ? ecl : 0)) : lit * 0.85f);
                    if (MissionDesign.HasTag(i, "weather") && i.id == "ins.sounder" && !b.HasAtmosphere) du = 0;
                    dutyOf[i.id] = du;
                    gen += i.dataGB * du;
                }
                float dayDuty = ins.Count > 0 ? ins.Average(i => dutyOf.TryGetValue(i.id, out var v) ? v : 0) : 0;
                sumDuty += dayDuty; dutyDays++;
                double down = safe ? 0 : sheet.downPerDayGB;
                double available = store + gen;
                double delivered = Math.Min(available, down);
                double keep = available - delivered;
                double overflow = Math.Max(0, keep - sheet.storageGB);
                store = keep - overflow;
                sent += delivered;
                lost += overflow;
                double share = gen > 1e-9 ? Math.Min(1, delivered / gen) : 1;
                foreach (var i in ins)
                {
                    float du = dutyOf[i.id];
                    if (du <= 0 || i.science <= 0) continue;
                    double q = du * (i.dataGB > 0 ? share : 1);
                    if (i.needPointing > 0) q *= Math.Min(1, Math.Sqrt(i.needPointing / Math.Max(1e-4, PointingOf(plan, b))));
                    if (MissionDesign.HasTag(i, "map") || i.id == "ins.cam" || i.id == "ins.therm") q *= sheet.resolution;
                    q *= MissionDesign.Wanted(sc, i) ? 1.0 : 0.3;
                    science += i.science * q * users.scienceMult * Math.Max(1, b.scienceMult);
                    // coverage, for the instruments the goal needs
                    if (MissionDesign.Wanted(sc, i) && i.swath > 0)
                    {
                        double swathKm = Math.Min(2 * R, i.swath * alt);
                        double rate = swathKm * groundTrackKmDay / surface * q;
                        cover += (sheet.maxCoverage - cover) * (1 - Math.Exp(-rate));
                    }
                }
                // a relay: in view of the far side and of home
                if (ins.Any(i => i.id == "ins.relay" && !dead.Contains(i.id)) && !safe)
                {
                    double view = o == MOrbit.High ? 0.62 : o == MOrbit.Medium ? 0.45 : o == MOrbit.Polar ? 0.3 : 0.22;
                    double link = Math.Min(1, sheet.rateMbps / 0.2);
                    relay += view * dutyOf["ins.relay"] / Math.Max(1e-3, (1 - ecl) + ecl) * link;
                    relayDays++;
                }
                if (d >= nextSample || d == days - 1)
                {
                    nextSample += sampleEvery;
                    r.tDays.Add(d); r.duty.Add(dayDuty); r.stored.Add((float)store); r.sent.Add((float)sent); r.sci.Add((float)science); r.cov.Add((float)cover);
                }
            }
            int lived = alive ? days : deathDay;
            r.years = lived / 365f;
            r.science = (float)science;
            r.coverage = (float)cover;
            r.dataGB = (float)sent;
            r.lostGB = (float)lost;
            r.relayShare = relayDays > 0 ? (float)(relay / Math.Max(1, days)) : 0;
            if (alive) Event(days, $"Goal reached: {sc.lifeYears:0.#} years of work done." + (sheet.lifeYearsEstimate > sc.lifeYears * 1.2f ? " It's still healthy: an extended mission begins." : ""));

            // ---- why it went the way it did
            float avgDuty = dutyDays > 0 ? sumDuty / dutyDays : 0;
            if (sheet.sunW < sheet.loadW) r.bad.Add($"Power was short ({sheet.sunW:0} W made, {sheet.loadW:0} W wanted), so instruments ran only {avgDuty * 100:0}% of the time. Bigger or more solar wings would have meant more science.");
            if (sheet.nightNeedWh > sheet.batteryUsableWh) r.bad.Add($"The battery couldn't get through the night side ({sheet.nightNeedWh:0} Wh needed, {sheet.batteryUsableWh:0} Wh safe to use): instruments switched off in every eclipse.");
            if (lost > sent * 0.1) r.bad.Add($"{lost:0} GB of data never made it home ({sent:0} GB did): the antenna and ground stations could send only {sheet.downPerDayGB:0.##} GB a day. A dish or more ground stations would bring more home.");
            else if (sent > 0) r.good.Add($"Almost all the data made it home ({sent:0} GB): the comms plan matched the instruments.");
            if (sheet.pointingQuality < 0.95f) r.bad.Add("Pointing was too rough for some instruments: their pictures were blurry, worth less science.");
            if (sheet.resolution < 0.6f && ins.Any(i => MissionDesign.HasTag(i, "map"))) r.bad.Add($"From {sheet.altitudeKm:0} km the pictures had only {sheet.resolution * 100:0}% of the detail they'd have from low orbit.");
            if (sc.coverageNeeded > 0 && sheet.maxCoverage < sc.coverageNeeded) r.bad.Add($"The orbit could never see more than {sheet.maxCoverage * 100:0}% of {b.name}. A polar orbit passes over everywhere.");
            else if (sc.coverageNeeded > 0 && cover >= sc.coverageNeeded) r.good.Add($"Coverage reached {cover * 100:0}%: the orbit and instruments saw what the goal needed.");
            if (avgDuty > 0.8f) r.good.Add($"Plenty of power: instruments worked {avgDuty * 100:0}% of the time they could.");
            if (sc.budget > 0 && sheet.totalCostM < sc.budget * 0.8f) r.good.Add($"Under budget by {sc.budget - sheet.totalCostM:0} M.");
            if (!alive && r.years < sc.lifeYears) r.bad.Add($"It stopped after {r.years:0.#} years of the {sc.lifeYears:0.#} planned. " + (r.events.Count > 0 ? r.events[r.events.Count - 1].Split('|')[1] : ""));
            else if (alive) r.good.Add($"It lasted the whole {sc.lifeYears:0.#} years.");
            if (dead.Count > 0) r.bad.Add($"A meteoroid knocked out {string.Join(", ", ins.Where(i => dead.Contains(i.id)).Select(i => i.name))}: having more than one instrument for the goal is insurance.");
            Finish(r, sheet, sc, ins, easy);
            return r;
        }

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        static float PointingOf(MissionPlan plan, CelestialBody b)
        {
            var att = MissionDesign.Part(plan.attitude) ?? MissionDesign.Part("att.rw");
            return att.pointing * (att.id == "att.mag" && !b.IsHomeWorld ? 2.5f : 1f);
        }

        /// <summary>The goals checked, the score (out of 1000) and stars, and the headline.</summary>
        static void Finish(MissionResult r, MSheet sheet, MScenario sc, List<MPart> ins, bool easy)
        {
            r.reqs.Clear();
            if (sc.coverageNeeded > 0) r.reqs.Add(new MReq { name = $"Coverage ({string.Join(", ", sc.tags)})", unit = "%", need = sc.coverageNeeded * 100, got = r.coverage * 100 });
            if (sc.scienceNeeded > 0) r.reqs.Add(new MReq { name = "Science", unit = "points", need = sc.scienceNeeded, got = r.science });
            if (sc.dataNeededGB > 0) r.reqs.Add(new MReq { name = "Data home", unit = "GB", need = sc.dataNeededGB, got = r.dataGB });
            if (sc.relayNeeded > 0) r.reqs.Add(new MReq { name = "Relay time", unit = "%", need = sc.relayNeeded * 100, got = r.relayShare * 100 });
            r.reqs.Add(new MReq { name = "Lifetime", unit = "years", need = sc.lifeYears, got = r.years });
            foreach (var q in r.reqs) q.ok = q.got >= q.need - 1e-3f;
            int met = r.reqs.Count(q => q.ok);
            r.success = r.arrived && met == r.reqs.Count;
            // score: goals 500, science 250, money 100, data efficiency 50, lasting longer 50, plus any team bonus
            double goals = r.reqs.Count == 0 ? 0 : r.reqs.Average(q => q.ok ? 1.0 : Math.Min(0.8, q.got / Math.Max(1e-6, q.need)));
            double sci = sc.relayNeeded > 0 && sc.scienceNeeded <= 0
                ? Math.Min(1, r.relayShare / (sc.relayNeeded * 1.3))                      // a relay's "science" is the time it keeps landers in touch
                : Math.Min(1, r.science / Math.Max(1, sc.scienceNeeded > 0 ? sc.scienceNeeded * 1.5 : 600));
            double money = sc.budget > 0 ? Math.Max(0, Math.Min(1, (sc.budget - sheet.totalCostM) / (sc.budget * 0.5))) : 0.5;
            double eff = r.dataGB + r.lostGB > 0 ? r.dataGB / (r.dataGB + r.lostGB) : (r.arrived ? 1 : 0);
            double extra = sheet.lifeYearsEstimate > sc.lifeYears ? Math.Min(1, (sheet.lifeYearsEstimate - sc.lifeYears) / Math.Max(0.5, sc.lifeYears)) : 0;
            double s = !r.launched ? 0 : 500 * goals + 250 * sci + 100 * money + 50 * eff + 50 * extra;
            if (!r.arrived) s *= 0.25;
            r.score = (int)Math.Round(Math.Max(0, Math.Min(1000, s))) + r.teamBonus;
            r.stars = !r.success ? 0 : r.score >= 850 ? 3 : r.score >= 700 ? 2 : 1;
            if (r.headline == null) r.headline = r.success ? (r.stars == 3 ? "Outstanding mission!" : r.stars == 2 ? "Mission accomplished, and done well" : "Mission accomplished")
                : r.arrived ? $"Partly successful: {met} of {r.reqs.Count} goals met" : r.headline;
            if (r.good.Count == 0 && r.success) r.good.Add("Every goal met.");
        }
    }
}
