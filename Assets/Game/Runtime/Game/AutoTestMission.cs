using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        IEnumerator WaitPlanets()
        {
            float tb = Time.realtimeSinceStartup;
            while (PlanetBaker.Baked < 8 && Time.realtimeSinceStartup - tb < 300f) yield return null;
        }

        /// <summary>Milestone 29, alone: the mission design hub, each job's choices, the mission flown, the report, the leaderboard.</summary>
        IEnumerator MissionTour()
        {
            var root = GameRoot.I;
            yield return WaitPlanets();
            root.ShowMenu();
            yield return new WaitForSecondsRealtime(1f);
            root.ShowMission();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("430_mission_hub");
            var plan = MissionDesign.Example("taltmap");
            plan.instruments.Remove("ins.sharp");
            plan.antenna = "ant.low";            // a design with a problem to show
            root.mission.TourSolo("captaincarl24", plan);
            yield return new WaitForSecondsRealtime(1.5f);
            string[] names = { "431_design_mission", "432_design_spacecraft", "433_design_science", "434_design_orbit", "435_design_comms" };
            for (int t = 0; t < 5; t++)
            {
                root.mission.TourTab(t);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot(names[t]);
            }
            var r = NetRoom.Current;
            r.LoadPlan(MissionDesign.Example("taltmap"));       // fixed: back to the good design
            yield return new WaitForSecondsRealtime(0.6f);
            root.mission.TourTab(3);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("435b_design_fixed");
            var res = r.Fly(root.bodies, true);
            Log($"solo mission: {res.headline}, {res.score} points, {res.stars} stars");
            yield return new WaitForSecondsRealtime(MissionMode.FlyLength * 0.55f);
            yield return Shot("436_mission_flying");
            yield return WaitFor(() => r.State.phase == 2, 30f, "the report");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("437_mission_report");
            root.mission.TourPage("board");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("438_leaderboard");
            r.Stop();
            root.LeaveMission();
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>Milestone 29, as a team: this game hosts a room, waits for three players (other copies of the game), flies when they're ready.</summary>
        IEnumerator MissionHostTour()
        {
            var root = GameRoot.I;
            yield return WaitPlanets();
            root.ShowMission();
            yield return new WaitForSecondsRealtime(1f);
            var plan = MissionDesign.Example("storm");
            plan.instruments = new System.Collections.Generic.List<string> { "ins.cam" };
            root.mission.TourHost("captaincarl24", "Space club", plan);
            var r = NetRoom.Current;
            Log($"room open: {r.Error ?? "ok"}, code {r.State.code}, port {r.ListenPort}");
            yield return WaitFor(() => r.State.players.Count == 4, 90f, "three players to join");
            Log($"players: {string.Join(", ", r.State.players.Select(p => p.name + ":" + p.roles))}");
            yield return new WaitForSecondsRealtime(4f);
            r.Orbit(MOrbit.SunSync);
            r.Ask(MRole.Engineer, "Can we have a bigger battery?");
            yield return WaitFor(() => r.State.players.Where(p => p.id != 1).All(p => p.ready), 60f, "everyone ready");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("440_host_design");
            var res = r.Fly(root.bodies, true);
            Log($"team mission: {res.headline}, {res.score} points (+{res.teamBonus} teamwork), design: {string.Join(", ", r.State.plan.instruments)}, battery {r.State.plan.battery}, ground {r.State.plan.ground}");
            yield return new WaitForSecondsRealtime(MissionMode.FlyLength * 0.5f);
            yield return Shot("441_host_flying");
            yield return WaitFor(() => r.State.phase == 2, 30f, "the report");
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("442_host_report");
            yield return new WaitForSecondsRealtime(3f);
            r.Stop();
        }

        /// <summary>Milestone 31 (the Space Apps check): Singleplayer without mission design, the new guide page, real missions, two designs compared, the report.</summary>
        IEnumerator Match31Tour()
        {
            var root = GameRoot.I;
            yield return WaitPlanets();
            root.ShowMenu();
            yield return new WaitForSecondsRealtime(1.5f);
            root.menu.ShowPage("single");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("470_singleplayer");
            root.menu.TourHelp(7);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("471_help_multiplayer");
            root.menu.ShowPage("main");
            root.ShowMission();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("472_multiplayer_hub");
            root.mission.TourMenu("make", false, 0);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("473_make_mission_room");
            var plan = MissionDesign.Example("ochre");
            root.mission.TourHost("captaincarl24", "Solo lab", plan);
            var r = NetRoom.Current;
            yield return new WaitForSecondsRealtime(1.5f);
            root.mission.TourTab(0);
            root.mission.TourScroll(99999f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("474_mission_real");
            var big = plan.Clone();
            big.instruments.Add("ins.therm");
            big.wings = 4;
            big.users = "usr.big";
            MissionDesigns.Save("Ochre lean", plan);
            MissionDesigns.Save("Ochre big", big);
            root.mission.TourCompare("Ochre lean", "Ochre big");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("475_compare");
            var rows = MissionDesign.Compare(MissionDesign.Evaluate(plan, root.bodies), MissionDesign.Evaluate(big, root.bodies));
            Log("compare: " + string.Join("; ", rows.Select(x => $"{x.name} {x.a} | {x.b} -> {x.winner}")));
            root.mission.TourPage("room");
            var res = r.Fly(root.bodies, true);
            Log($"solo room mission: {res.headline}, {res.score} points");
            root.mission.TourSkip();
            yield return WaitFor(() => r.State.phase == 2, 30f, "the report");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("476_report_real");
            MissionDesigns.Delete("Ochre lean");
            MissionDesigns.Delete("Ochre big");
            r.Stop();
            root.LeaveMission();
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>Milestone 31: two rooms race on the same mission. Run two copies (-mdName Comets, -mdName Rockets): each hosts, flies, and must hear the other's score.</summary>
        IEnumerator RivalTour()
        {
            var root = GameRoot.I;
            string name = Arg("-mdName") ?? "Comets";
            yield return WaitPlanets();
            root.ShowMission();
            yield return new WaitForSecondsRealtime(1f);
            var plan = MissionDesign.Example("storm");
            if (name != "Comets") { plan.instruments = new System.Collections.Generic.List<string> { "ins.cam" }; plan.ground = "gnd.one"; }
            root.mission.TourHost(name, name, plan);
            var r = NetRoom.Current;
            Log($"{name}: room {r.State.code}, port {r.ListenPort}, error {r.Error ?? "none"}");
            yield return new WaitForSecondsRealtime(name == "Comets" ? 4f : 8f);
            var res = r.Fly(root.bodies, true);
            Log($"{name}: flew it, {res.score} points, {res.stars} stars");
            root.mission.TourSkip();
            yield return WaitFor(() => r.State.phase == 2, 30f, "the report");
            yield return WaitFor(() => r.Rivals().Any(f => f.score > 0), 45f, "the other team's score");
            yield return new WaitForSecondsRealtime(1f);
            Log($"{name}: rivals heard: " + string.Join(", ", r.Rivals().Select(f => $"{f.name} {f.score} ({f.stars} stars, {f.scenario})")));
            yield return Shot($"477_{name.ToLower()}_report");
            r.SetPhase(0);
            root.mission.TourPage("room");
            root.mission.TourTab(0);
            root.mission.TourScroll(99999f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot($"478_{name.ToLower()}_rivals");
            yield return new WaitForSecondsRealtime(6f);
            r.Stop();
            root.LeaveMission();
        }

        /// <summary>Milestone 29, as a team: this game joins a room (-mdName, -mdCode), does its job, says it's ready, watches the flight and the report.</summary>
        IEnumerator MissionJoinTour()
        {
            var root = GameRoot.I;
            string name = Arg("-mdName") ?? "Player";
            string code = Arg("-mdCode") ?? "127.0.0.1:47770";
            yield return WaitPlanets();
            root.ShowMission();
            yield return new WaitForSecondsRealtime(1f);
            NetRoom r = null;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                root.mission.TourJoin(name, code);
                r = NetRoom.Current;
                float t0 = Time.realtimeSinceStartup;
                while (!r.Connected && r.Error == null && Time.realtimeSinceStartup - t0 < 6f) yield return null;
                if (r.Connected) break;
                yield return new WaitForSecondsRealtime(1.5f);
            }
            Log($"{name}: joined {r.Connected}, error {r.Error}");
            if (!r.Connected) yield break;
            yield return WaitFor(() => r.State.players.Count == 4, 90f, "the whole team");
            yield return new WaitForSecondsRealtime(2f + (r.MyId % 3));
            var me = r.Me;
            root.mission.TourTab(me.Has(MRole.Engineer) ? 1 : me.Has(MRole.Scientist) ? 2 : me.Has(MRole.Comms) ? 4 : 0);
            if (me.Has(MRole.Engineer)) { r.Pick("bat.l"); r.Wings(2); }
            if (me.Has(MRole.Scientist)) { r.Pick("ins.sounder"); r.Chat("Added the weather sounder!"); }
            if (me.Has(MRole.Comms)) { r.Pick("ant.dish"); r.Pick("gnd.three"); }
            Log($"{name}: roles {me.roles}, did my job");
            yield return new WaitForSecondsRealtime(5f);
            // answer anything asked of me
            foreach (var q in r.State.requests.Where(q => q.answer == 0 && r.Can((MRole)q.toRole)).ToList()) r.Answer(q.id, true);
            yield return new WaitForSecondsRealtime(1.5f);
            r.Ready(true);
            yield return new WaitForSecondsRealtime(1.5f);
            if (!r.Me.ready) { r.Ready(true); yield return new WaitForSecondsRealtime(1f); }
            yield return Shot($"443_{name.ToLower()}_design");
            yield return WaitFor(() => r.State.phase == 1, 60f, "the flight");
            yield return new WaitForSecondsRealtime(MissionMode.FlyLength * 0.4f);
            yield return Shot($"444_{name.ToLower()}_flying");
            yield return WaitFor(() => r.State.phase == 2, 40f, "the report");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot($"445_{name.ToLower()}_report");
            Log($"{name}: result seen {!string.IsNullOrEmpty(r.State.resultJson)}");
            r.Stop();
        }
    }
}
