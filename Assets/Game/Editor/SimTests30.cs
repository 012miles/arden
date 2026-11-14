using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 30 tests: rocket rooms (build together, fly together) over the network.</summary>
    public static partial class SimTests
    {
        static void RunMilestone30()
        {
            Run("Rocket room: four players get the four jobs; only the builder changes the rocket and launches; anyone comments and the builder ticks it off; mission control picks the goal", RocketRoomBuilding);
            Run("Rocket room: only the pilot's controls are taken; jobs are checked for every command; the flight reaches the players and plays back smoothly; goals are judged", RocketRoomFlying);
            Run("Campaign rocket room: the room says it's a campaign; the host's career (money, research, contracts) reaches every player; the guide stays closed; the host's save is named after the room", CampaignRoomCareer);
            Run("Campaign mission design: missions open in order (one done opens the next); a locked mission can't be picked; free design is sandbox only", CampaignMissionLocks);
        }

        static string CampaignRoomCareer()
        {
            var host = NetRoom.Host("Ana", "Moon club", new MissionPlan(), 1, true);
            Check(host.Error == null, "couldn't open a campaign room: " + host.Error);
            var client = NetRoom.Join("Ben", "127.0.0.1:" + host.ListenPort);
            try
            {
                bool Pump(Func<bool> done, double seconds)
                {
                    var t0 = DateTime.Now;
                    while ((DateTime.Now - t0).TotalSeconds < seconds)
                    {
                        host.Update(0.05f); client.Update(0.05f);
                        if (done()) return true;
                        Thread.Sleep(15);
                    }
                    return false;
                }
                Check(Pump(() => client.Connected && client.State.players.Count == 2, 8), "the player didn't get in");
                Check(client.State.campaign && client.State.activity == 1, "the player doesn't know it's a campaign rocket room");
                var watcher = new Coop(null, client);
                // the host's career, a few flights in
                var u = new Universe();
                u.Reset(GameMode.Career);
                var c = u.career;
                c.funds = 41_234; c.science = 17; c.reputation = 9;
                var node = TechTree.Nodes.First(n => n.id != "start" && n.parts.Length > 0);
                c.unlocked.Add(node.id);
                int contracts = c.contracts.Count;
                string json = Coop.CareerJson(c);
                host.BroadcastRaw(new NetMsg { t = "career", json = json });
                Check(Pump(() => watcher.careerJson != null, 4), "the career didn't reach the player");
                var mine = new Universe();
                mine.Reset(GameMode.Career);
                Check(!mine.career.IsUnlocked(node.parts[0]), "test setup: the part is open already");
                Check(Coop.ApplyCareer(mine.career, watcher.careerJson), "the career couldn't be read");
                Check(mine.career.IsCareer && Math.Abs(mine.career.funds - 41_234) < 0.5 && Math.Abs(mine.career.science - 17) < 0.01, $"money or science is off: {mine.career.funds}, {mine.career.science}");
                Check(mine.career.IsUnlocked(node.parts[0]), "the research didn't come across");
                Check(mine.career.contracts.Count == contracts, "the contracts didn't come across");
                Check(mine.career.guide == Guide.Closed, "the first-flight guide opened in a room");
                Check(!Coop.ApplyCareer(mine.career, "not a career") && !Coop.ApplyCareer(mine.career, ""), "junk was taken as a career");
                Check(GameRoot.CoopSlotName("Moon club") == "Multiplayer: Moon club", "the save's name");
                return $"{json.Length / 1024f:0.0} KB career, {contracts} contracts, research {node.id}";
            }
            finally { client.Stop(); host.Stop(); }
        }

        static string CampaignMissionLocks()
        {
            var order = MissionDesign.CampaignOrder;
            Check(order.Length >= 3 && order.All(s => !s.sandbox), "the campaign's missions");
            Check(MissionDesign.UnlockedFrom(id => 0) == 1, "more than the first mission open at the start");
            Check(MissionDesign.UnlockedFrom(id => id == order[0].id ? 2 : 0) == 2, "doing the first didn't open the second");
            Check(MissionDesign.UnlockedFrom(id => id == order[1].id ? 3 : 0) == 1, "the second done out of order opened things");
            Check(MissionDesign.UnlockedFrom(id => 3) == order.Length, "everything done should open everything");
            Check(MissionDesign.OpenInCampaign(order[0].id, 1) && !MissionDesign.OpenInCampaign(order[1].id, 1) && MissionDesign.OpenInCampaign(order[1].id, 2), "open/locked is wrong");
            Check(!MissionDesign.OpenInCampaign("sandbox", 99), "free design is open in a campaign");
            var plan = new MissionPlan { scenario = order[0].id };
            var host = NetRoom.Host("Ana", "Mission club", plan, 0, true);
            Check(host.Error == null, "couldn't open the room: " + host.Error);
            try
            {
                host.State.unlocked = 1;
                host.Scenario(order[1].id);
                host.Update(0.05f);
                Check(host.State.plan.scenario == order[0].id, "a locked mission could be picked");
                host.State.unlocked = 2;
                host.Scenario(order[1].id);
                host.Update(0.05f);
                Check(host.State.plan.scenario == order[1].id, "an open mission couldn't be picked");
                host.Scenario("sandbox");
                host.Update(0.05f);
                Check(host.State.plan.scenario == order[1].id, "free design could be picked in a campaign");
                return $"{order.Length} missions: " + string.Join(" > ", order.Select(s => s.name));
            }
            finally { host.Stop(); }
        }

        static (NetRoom host, List<NetRoom> clients, Func<Func<bool>, double, bool> pump) RocketRoom()
        {
            var host = NetRoom.Host("Ana", "Rocket test", new MissionPlan(), 1);
            Check(host.Error == null, "couldn't open a rocket room: " + host.Error);
            var clients = new List<NetRoom>();
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
            Check(Pump(() => host.State.players.Count == 4 && clients.All(c => c.Connected && c.MyId > 0 && c.State.players.Count == 4 && c.State.activity == 1), 8), "not everyone got into the rocket room");
            return (host, clients, Pump);
        }

        static string RocketRoomBuilding()
        {
            var (host, clients, Pump) = RocketRoom();
            try
            {
                var hostCoop = new Coop(null, host);
                Check(host.Me.Has((MRole)Coop.Builder) && host.Me.roles == 1, "the host isn't the builder");
                var pilot = clients.First(c => c.Me.Has((MRole)Coop.Pilot));
                var eng = clients.First(c => c.Me.Has((MRole)Coop.Engineer));
                var ctl = clients.First(c => c.Me.Has((MRole)Coop.Control));
                // the builder's rocket reaches everyone; nobody else can change it
                string design = JsonUtility.ToJson(VesselDesign.Default());
                host.Send(new NetMsg { t = "design", json = design });
                Check(Pump(() => clients.All(c => c.State.designJson == design), 4), "the builder's rocket didn't reach everyone");
                pilot.Send(new NetMsg { t = "design", json = JsonUtility.ToJson(VesselDesign.Stack("Hack", "pod.wren")) });
                Pump(() => false, 0.4);
                Check(host.State.designJson == design, "the pilot could change the rocket");
                // comments: on a part, ticked off by the builder
                eng.Send(new NetMsg { t = "note", n = 2, a = "It needs a parachute" });
                Check(Pump(() => host.State.notes.Count == 1 && clients.All(c => c.State.notes.Count == 1), 4), "the comment didn't reach everyone");
                Check(host.State.notes[0].part == 2 && host.State.notes[0].text.Contains("parachute"), "the comment lost its part or words");
                pilot.Send(new NetMsg { t = "noteState", n = host.State.notes[0].id, on = true });
                Pump(() => false, 0.3);
                Check(host.State.notes[0].state == 0, "someone other than the builder ticked a comment off");
                host.Send(new NetMsg { t = "noteState", n = host.State.notes[0].id, on = true });
                Check(Pump(() => eng.State.notes[0].state == 1, 4), "the builder's tick didn't arrive");
                // the goal: mission control's
                pilot.Send(new NetMsg { t = "goal", b = "orbit" });
                Pump(() => false, 0.3);
                Check(host.State.goal == "space", "the pilot changed the goal");
                ctl.Send(new NetMsg { t = "goal", b = "orbit" });
                ctl.Send(new NetMsg { t = "pad", n = 2 });
                Check(Pump(() => host.State.goal == "orbit" && host.State.pad == 2, 4), "mission control's goal and pad didn't arrive");
                // launching: the builder only
                pilot.Send(new NetMsg { t = "launch" });
                Pump(() => false, 0.3);
                Check(!hostCoop.commands.Any(q => q.m.t == "launch"), "the pilot launched");
                host.Send(new NetMsg { t = "launch" });
                Check(hostCoop.commands.Any(q => q.m.t == "launch"), "the builder couldn't launch");
                return $"jobs {string.Join(" ", host.State.players.Select(q => q.name + ":" + q.roles))}; goal {host.State.goal}, pad {host.State.pad}";
            }
            finally { foreach (var c in clients) c.Stop(); host.Stop(); }
        }

        static string RocketRoomFlying()
        {
            var (host, clients, Pump) = RocketRoom();
            try
            {
                var hostCoop = new Coop(null, host);
                var pilot = clients.First(c => c.Me.Has((MRole)Coop.Pilot));
                var eng = clients.First(c => c.Me.Has((MRole)Coop.Engineer));
                var watcher = new Coop(null, eng);
                // the pilot's controls are taken, the engineer's aren't
                eng.Send(new NetMsg { t = "in", json = JsonUtility.ToJson(new PilotInput { p = 1 }) });
                pilot.Send(new NetMsg { t = "in", json = JsonUtility.ToJson(new PilotInput { p = 0.5f, thr = 1 }) });
                Check(Pump(() => hostCoop.commands.Count(q => q.m.t == "in") >= 1, 4), "the pilot's controls didn't arrive");
                Pump(() => false, 0.3);
                Check(hostCoop.commands.Count(q => q.m.t == "in") == 1, "the engineer's steering was taken too");
                // commands go to the host, which checks the sender's job
                eng.Send(new NetMsg { t = "cmd", a = "stage" });
                pilot.Send(new NetMsg { t = "cmd", a = "stage" });
                Check(Pump(() => hostCoop.commands.Count(q => q.m.t == "cmd") == 2, 4), "commands didn't arrive");
                var cmds = hostCoop.commands.Where(q => q.m.t == "cmd").ToList();
                Check(hostCoop.SenderCan(cmds.First(q => q.who == eng.MyId).who, CAct.Stage) && !hostCoop.SenderCan(cmds.First(q => q.who == pilot.MyId).who, CAct.Stage), "staging isn't the engineer's job alone");
                Check(hostCoop.SenderCan(pilot.MyId, CAct.Steer) && !hostCoop.SenderCan(eng.MyId, CAct.Steer), "steering isn't the pilot's job alone");
                // the flight, streamed: smooth between frames
                for (int i = 0; i < 6; i++)
                {
                    var f = new FlightFrame { ut = 100 + i * 0.05, px = 700_000 + i * 10, py = 0, pz = 0, qw = 1, vessel = 7, body = 0 };
                    host.BroadcastRaw(new NetMsg { t = "fs", json = JsonUtility.ToJson(f) });
                    Pump(() => false, 0.05);
                }
                Check(Pump(() => watcher.frames.Count >= 5, 4), $"only {watcher.frames.Count} flight frames arrived");
                Check(watcher.Sample(out var ff, out var pos, out _, out _, out double sut) && pos.x >= 700_000 && pos.x <= 700_050 && sut >= 100 && sut <= 100.25, $"the played-back flight is off: {pos.x:0}, {sut:0.00}");
                host.BroadcastRaw(new NetMsg { t = "snap", n = 3, json = JsonUtility.ToJson(VesselSim.CreateOnPad(VesselDesign.Default(), CelestialBody.CreateHome(), 0).ToSave()) });
                Check(Pump(() => watcher.snapId == 3 && watcher.snapJson != null, 4), "the ship's copy didn't arrive");
                Check(JsonUtility.FromJson<VesselSave>(watcher.snapJson) != null, "the ship's copy couldn't be read");
                // goals
                var v = VesselSim.CreateOnPad(VesselDesign.Default(), CelestialBody.CreateHome(), 0);
                Check(Coop.GoalMet("hop", v, 12_000, 0) && !Coop.GoalMet("hop", v, 8_000, 0), "the 10 km goal is judged wrong");
                v.careerFlags |= VFlags.SoiTal;
                Check(Coop.GoalMet("tal", v, 0, 0) && !Coop.GoalMet("talland", v, 0, 0), "the Tal goals are judged wrong");
                return $"{watcher.frames.Count} frames, played back at x {pos.x - 700_000:0} m into the run";
            }
            finally { foreach (var c in clients) c.Stop(); host.Stop(); }
        }
    }
}
