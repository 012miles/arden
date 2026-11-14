using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 29 tests: the mission design game (parts, orbits, the dashboard's checks, the simulation and its report) and team rooms over the network.</summary>
    public static partial class SimTests
    {
        static void RunMilestone29()
        {
            Run("Mission design: every kind of part, five missions and a free design, each mission's world has orbits, the ids are unique", MissionCatalogue);
            Run("Mission design: an example design for each mission flies and meets every goal", MissionExamples);
            Run("Mission design: the dashboard catches a rocket too small, over budget, no instruments, too many instruments, data that can't get home, an orbit that can't see the poles", MissionChecks);
            Run("Mission design: the report says why (no engine in low orbit, no fuel to stop at Tal, data lost), and a crippled design scores less", MissionReport);
            Run("Mission design: room codes and share codes come back as they went", MissionCodes);
            Run("Team room: a host and three players on the network share the four jobs, may only change their own part, see the same design, ask and answer, fly together and get the result", MissionRoom);
        }

        static string MissionCatalogue()
        {
            var sys = CelestialBody.CreateSystem();
            Check(MissionDesign.Parts.Select(p => p.id).Distinct().Count() == MissionDesign.Parts.Length, "two parts share an id");
            foreach (MCat c in Enum.GetValues(typeof(MCat))) Check(MissionDesign.In(c).Any(), $"no {c} parts");
            Check(MissionDesign.Scenarios.Count(s => !s.sandbox) == 5 && MissionDesign.Scenarios.Any(s => s.sandbox), "not five missions and a free design");
            foreach (var s in MissionDesign.Scenarios)
            {
                var b = sys.FirstOrDefault(x => x.name == s.body);
                Check(b != null, $"{s.name}: no world called {s.body}");
                Check(MissionDesign.OrbitsAt(b).Count() >= 4, $"{s.name}: too few orbits at {s.body}");
            }
            var arden = sys.First(x => x.IsHomeWorld);
            Check(MissionDesign.OrbitsAt(arden).Contains(MOrbit.Stationary) && !MissionDesign.OrbitsAt(sys.First(x => x.name == "Tal")).Contains(MOrbit.Stationary), "stationary orbits aren't Arden's alone");
            double stat = MissionDesign.AltitudeOf(arden, MOrbit.Stationary), period = MissionDesign.PeriodOf(arden, MOrbit.Stationary);
            Check(Math.Abs(period - arden.rotationPeriod) < 1, $"a stationary orbit goes round in {period / 3600:0.00} h, Arden turns in {arden.rotationPeriod / 3600:0.00} h");
            Check(MissionDesign.EclipseOf(arden, MOrbit.SunSync) == 0 && MissionDesign.EclipseOf(arden, MOrbit.Low) > 0.2, "shadow times are off");
            double ochreSun = MissionDesign.SolarFactor(sys.First(x => x.name == "Ochre"));
            Check(ochreSun > 0.35 && ochreSun < 0.45, $"sunlight at Ochre is {ochreSun:0.00} of Arden's");
            return $"{MissionDesign.Parts.Length} parts, {MissionDesign.Vehicles.Length} rockets; Arden's stationary orbit {stat / 1000:0} km up; sunlight at Ochre {ochreSun:0.00}";
        }

        static string MissionExamples()
        {
            var sys = CelestialBody.CreateSystem();
            var sb = new System.Text.StringBuilder();
            foreach (var s in MissionDesign.Scenarios.Where(s => !s.sandbox))
            {
                var plan = MissionDesign.Example(s.id);
                var sheet = MissionDesign.Evaluate(plan, sys);
                Check(sheet.CanLaunch, $"{s.name} example can't launch: {string.Join("; ", sheet.blockers)}");
                var r = MissionSim.Simulate(plan, sys, 1, true);
                Check(r.success && r.stars >= 1, $"{s.name} example: {r.headline}; " + string.Join(", ", r.reqs.Select(q => $"{q.name} {q.got:0.#}/{q.need:0.#}")) + "; " + string.Join(" | ", r.bad));
                sb.Append($"{s.name} {r.score} ({r.stars}*, {sheet.massKg:0} kg, {sheet.totalCostM:0} M); ");
            }
            return sb.ToString();
        }

        static string MissionChecks()
        {
            var sys = CelestialBody.CreateSystem();
            var p = MissionDesign.Example("ice");
            p.vehicle = "lv.sprite";
            Check(MissionDesign.Evaluate(p, sys).blockers.Any(b => b.Contains("Sprite")), "a Sprite to Vesk wasn't stopped");
            p = MissionDesign.Example("storm");
            p.vehicle = "lv.goliath";
            Check(MissionDesign.Evaluate(p, sys).blockers.Any(b => b.Contains("Over budget")), "a Goliath for Storm Watch wasn't over budget");
            p = MissionDesign.Example("storm");
            p.instruments.Clear();
            Check(MissionDesign.Evaluate(p, sys).blockers.Any(b => b.Contains("No instruments")), "no instruments wasn't stopped");
            p = MissionDesign.Example("storm");
            p.bus = "bus.cube";
            p.instruments = new List<string> { "ins.cam", "ins.mag", "ins.part" };
            Check(MissionDesign.Evaluate(p, sys).blockers.Any(b => b.Contains("room for 2")), "three instruments on a cube frame wasn't stopped");
            p = MissionDesign.Example("taltmap");
            p.antenna = "ant.low";
            var sh = MissionDesign.Evaluate(p, sys);
            Check(sh.warnings.Any(w => w.Contains("can be sent home")), "a low-gain antenna for a camera at Tal didn't warn about data");
            p = MissionDesign.Example("storm");
            p.orbit = MOrbit.Low;
            Check(MissionDesign.Evaluate(p, sys).warnings.Any(w => w.Contains("polar orbit")), "an equatorial orbit for world-wide weather didn't warn");
            return $"at Tal a low-gain antenna sends {sh.downPerDayGB:0.##} GB a day against {sh.dataPerDayGB:0} made";
        }

        static string MissionReport()
        {
            var sys = CelestialBody.CreateSystem();
            var p = MissionDesign.Example("storm");
            p.propulsion = "prop.none";
            p.orbit = MOrbit.Polar;
            var r = MissionSim.Simulate(p, sys, 2, true);
            Check(r.years < 1.6f && r.events.Any(e => e.Contains("dragged")), $"no engine in low orbit lasted {r.years:0.0} years: " + string.Join(" / ", r.events));
            p = MissionDesign.Example("taltmap");
            p.propulsion = "prop.none";
            r = MissionSim.Simulate(p, sys, 2, true);
            Check(!r.arrived && r.headline.Contains("Flew past"), "no engine at Tal didn't fly past: " + r.headline);
            p = MissionDesign.Example("taltmap");
            p.antenna = "ant.low";
            r = MissionSim.Simulate(p, sys, 2, true);
            Check(r.bad.Any(b => b.Contains("never made it home")), "lost data wasn't explained: " + string.Join(" | ", r.bad));
            var good = MissionSim.Simulate(MissionDesign.Example("taltmap"), sys, 2, true);
            Check(good.score > r.score + 50, $"the good design scored {good.score}, the crippled one {r.score}");
            return $"good {good.score}, low-gain {r.score} with {r.lostGB:0} GB lost";
        }

        static string MissionCodes()
        {
            foreach (var ip in new[] { "192.168.1.23", "10.0.0.5", "127.0.0.1", "172.20.14.200" })
                for (int k = 0; k < 10; k += 3)
                {
                    string code = NetRoom.CodeFor(System.Net.IPAddress.Parse(ip), k);
                    Check(NetRoom.Decode(code, out var back, out int port) && back.ToString() == ip && port == NetRoom.Port + k, $"{ip}+{k} -> {code} -> {back}:{port}");
                    Check(NetRoom.Decode(code.ToLowerInvariant().Replace("-", " "), out back, out _) && back.ToString() == ip, "codes aren't forgiving about case and spaces");
                }
            Check(NetRoom.Decode("192.168.0.9:47772", out var a, out int pt) && a.ToString() == "192.168.0.9" && pt == 47772, "a typed address doesn't work");
            Check(!NetRoom.Decode("HELLO", out _, out _), "a bad code was accepted");
            var plan = MissionDesign.Example("ochre");
            var share = MissionDesigns.ShareCode(plan);
            var back2 = MissionDesigns.FromShareCode(share);
            Check(back2 != null && JsonUtility.ToJson(back2) == JsonUtility.ToJson(plan), "a share code didn't bring the design back");
            return $"192.168.1.23 is {NetRoom.CodeFor(System.Net.IPAddress.Parse("192.168.1.23"), 0)}; a share code is {share.Length} characters";
        }

        static string MissionRoom()
        {
            var sys = CelestialBody.CreateSystem();
            var host = NetRoom.Host("Ana", "Test room", MissionDesign.Example("storm"));
            Check(host.Error == null, "couldn't open a room: " + host.Error);
            var clients = new List<NetRoom>();
            try
            {
                foreach (var n in new[] { "Ben", "Cy", "Di" }) clients.Add(NetRoom.Join(n, "127.0.0.1:" + host.ListenPort));
                bool Pump(Func<bool> done, double seconds)
                {
                    var t0 = DateTime.Now;
                    while ((DateTime.Now - t0).TotalSeconds < seconds)
                    {
                        host.Update(0.05f);
                        foreach (var c in clients) c.Update(0.05f);
                        if (done()) return true;
                        Thread.Sleep(15);
                    }
                    return false;
                }
                Check(Pump(() => host.State.players.Count == 4 && clients.All(c => c.Connected && c.MyId > 0 && c.State.players.Count == 4), 8), $"not everyone got in: host sees {host.State.players.Count}, " + string.Join(", ", clients.Select(c => $"{c.Connected}/{c.Error}")));
                Check(host.Me.roles == 1 && clients.All(c => c.Me.roles != 0 && (c.Me.roles & (c.Me.roles - 1)) == 0) && clients.Select(c => c.Me.roles).Distinct().Count() == 3,
                    "the four jobs weren't shared one each: " + string.Join(", ", host.State.players.Select(q => $"{q.name}:{q.roles}")));
                // who got which job (players can arrive in any order)
                var eng = clients.First(c => c.Me.Has(MRole.Engineer));
                var sci = clients.First(c => c.Me.Has(MRole.Scientist));
                var com = clients.First(c => c.Me.Has(MRole.Comms));
                clients = new List<NetRoom> { eng, sci, com };
                // the engineer can't change instruments; the scientist can
                clients[0].Pick("ins.mag");
                clients[1].Pick("ins.part");
                Check(Pump(() => host.State.plan.instruments.Contains("ins.part"), 4), "the science lead's pick didn't arrive");
                Check(!host.State.plan.instruments.Contains("ins.mag"), "the engineer could change the instruments");
                Check(Pump(() => clients.All(c => c.State.plan.instruments.Contains("ins.part")), 4), "not everyone saw the new instrument");
                // asking and answering
                clients[2].Ask(MRole.Engineer, "More power please");
                Check(Pump(() => clients[0].State.requests.Any(q => q.answer == 0), 4), "the engineer didn't get the request");
                clients[0].Answer(clients[0].State.requests.First().id, true);
                Check(Pump(() => host.State.requests.All(q => q.answer == 1), 4), "the answer didn't arrive");
                // everyone ready, fly
                clients[1].Pick("ins.part");     // back off again (and every player has now changed something? the comms player hasn't)
                clients[2].Pick("gnd.three");
                clients[0].Pick("bat.l");
                Check(Pump(() => host.State.players.All(q => q.id == 1 || q.changes > 0), 4), "changes weren't counted");
                foreach (var c in clients) c.Ready(true);
                Check(Pump(() => host.State.players.Where(q => q.id != 1).All(q => q.ready), 4), "ready marks didn't arrive");
                host.Pick("lv.sprite");          // the lead's choice (it doesn't reset what the others have done)
                host.Rocket("lv.sprite");
                var res = host.Fly(sys, true);
                Check(Pump(() => clients.All(c => c.State.phase == 1 && !string.IsNullOrEmpty(c.State.resultJson)), 4), "the result didn't reach everyone");
                var seen = JsonUtility.FromJson<MissionResult>(clients[2].State.resultJson);
                Check(seen.score == res.score && seen.team.Split(',').Length == 4, "players saw a different result");
                // someone leaves: their job goes to someone else
                clients[2].Stop();
                Check(Pump(() => host.State.players.Count == 3 && host.State.players.Any(q => q.Has(MRole.Comms)), 6), "the comms job wasn't handed on when its player left");
                return $"4 players, jobs {string.Join(" ", host.State.players.Select(q => q.name + ":" + q.roles))}; score {res.score} with +{res.teamBonus} teamwork";
            }
            finally
            {
                foreach (var c in clients) c.Stop();
                host.Stop();
            }
        }
    }
}
