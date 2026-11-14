using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>Milestone 30: this game hosts a rocket room and builds; three other copies join as pilot, engineer and mission control.</summary>
        IEnumerator CoopHostTour()
        {
            var root = GameRoot.I;
            yield return WaitPlanets();
            bool camp = Array.IndexOf(Environment.GetCommandLineArgs(), "-coopCampaign") >= 0;
            var room = NetRoom.Host("captaincarl24", "Rocket club", new MissionPlan(), 1, camp);
            Log($"rocket room: {room.Error ?? "open"}, code {room.State.code}, {(camp ? "campaign" : "sandbox")}");
            root.EnterCoop(room);
            if (camp) Log($"campaign save: '{SaveSlots.NameOf(SaveSlots.Current)}', career {root.universe.career.IsCareer}, money {root.universe.career.funds:N0}");
            yield return WaitFor(() => room.State.players.Count == 4, 90f, "three players");
            Log($"players: {string.Join(", ", room.State.players.Select(p => p.name + ":" + p.roles))}");
            // build: start from the starter rocket
            root.hangar.SetDesignFromNet(camp ? VesselDesign.FirstFlight() : VesselDesign.Default());
            yield return WaitFor(() => room.State.notes.Count >= 3, 60f, "the team's comments");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("450_builder_with_comments");
            foreach (var n in room.State.notes.ToList()) room.Send(new NetMsg { t = "noteState", n = n.id, on = n.text != "It needs a parachute" || true });
            yield return WaitFor(() => Coop.Current.AllReady, 60f, "everyone ready");
            yield return new WaitForSecondsRealtime(1f);
            Coop.Current.Launch(root.hangar.design, false, 0);
            yield return WaitFor(() => root.inFlight, 20f, "the launch");
            Log($"launched: goal {room.State.goal}");
            yield return new WaitForSecondsRealtime(14f);
            root.flight.SetCamera(160f, 8f, 60f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("451_host_flight");
            Log($"host flight: altitude {root.flight.active.Altitude:0} m, stage {root.flight.active.nextStage}, throttle {root.flight.active.throttle:0.00}, sas {root.flight.active.sas}, goal done {room.State.goalDone}");
            yield return WaitFor(() => !root.inFlight, 60f, "mission control to end the flight");
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("452_host_after");
            Log($"after: {room.State.summary}");
            if (camp)
            {
                Log($"campaign after: money {root.universe.career.funds:N0}, science {root.universe.career.science:0.0}, saves: " + string.Join(" | ", SaveSlots.List().Select(s => $"{s.name} ({s.mode})")));
                root.hangar.TourCareer("missions");
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("456_host_campaign_missions");
                root.hangar.TourCareer(null);
            }
            yield return new WaitForSecondsRealtime(4f);
            root.LeaveCoop();
        }

        /// <summary>Milestone 30: the new title menu (Singleplayer / Multiplayer) and the multiplayer steps.</summary>
        IEnumerator Menus30Tour()
        {
            var root = GameRoot.I;
            yield return WaitPlanets();
            root.ShowMenu();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("460_title");
            root.menu.ShowPage("single");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("461_singleplayer");
            root.menu.ShowPage("new");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("462_new_campaign");
            root.ShowMission();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("463_multiplayer");
            root.mission.TourMenu("make", true, 1);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("464_multiplayer_campaign");
            root.mission.TourMenu("make", false, 0);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("465_multiplayer_sandbox");
            root.mission.TourMenu("join", true, 1);
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("466_join");
            root.mission.TourHostCampaign("captaincarl24", "Mission club");
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"mission campaign: {NetRoom.Current?.State.unlocked} of {MissionDesign.CampaignOrder.Length} open, scenario {NetRoom.Current?.State.plan.scenario}");
            yield return Shot("467_mission_campaign");
            NetRoom.Current?.Stop();
            root.LeaveMission();
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>Milestone 30: joins a rocket room (-mdName, -mdCode) and does whichever job it gets.</summary>
        IEnumerator CoopJoinTour()
        {
            var root = GameRoot.I;
            string name = Arg("-mdName") ?? "Player";
            string code = Arg("-mdCode") ?? "127.0.0.1:47770";
            yield return WaitPlanets();
            root.ShowMission();
            yield return new WaitForSecondsRealtime(1f);
            for (int attempt = 0; attempt < 20 && !Coop.Active; attempt++)
            {
                root.mission.TourJoin(name, code);
                float t0 = Time.realtimeSinceStartup;
                while (!Coop.Active && NetRoom.Current != null && NetRoom.Current.Error == null && Time.realtimeSinceStartup - t0 < 6f) yield return null;
                if (!Coop.Active) yield return new WaitForSecondsRealtime(1.5f);
            }
            Log($"{name}: in the rocket room {Coop.Active}");
            if (!Coop.Active) yield break;
            var c = Coop.Current;
            var room = c.room;
            yield return WaitFor(() => room.State.players.Count == 4 && !string.IsNullOrEmpty(room.State.designJson), 90f, "the team and the rocket");
            yield return new WaitForSecondsRealtime(2f + room.MyId % 3);
            bool pilot = c.Has(Coop.Pilot), eng = c.Has(Coop.Engineer), ctl = c.Has(Coop.Control);
            Log($"{name}: jobs {room.Me.roles}" + (room.State.campaign ? $"; campaign, the host's money on my screen {root.universe.career.funds:N0}, career {root.universe.career.IsCareer}" : ""));
            if (pilot) { c.notePart = 3; room.Send(new NetMsg { t = "note", n = 3, a = "Add fins at the bottom" }); }
            if (eng) room.Send(new NetMsg { t = "note", n = -1, a = "It needs a parachute" });
            if (ctl) { room.Send(new NetMsg { t = "goal", b = "hop" }); room.Send(new NetMsg { t = "note", n = -1, a = "Let's aim for 10 km first" }); room.Chat("Goal: fly higher than 10 km!"); }
            yield return new WaitForSecondsRealtime(3f);
            room.Ready(true);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot($"453_{name.ToLower()}_build");
            yield return WaitFor(() => root.inFlight, 60f, "the launch");
            var f = root.flight;
            yield return new WaitForSecondsRealtime(2.5f);
            if (pilot) { f.active.throttle = 1; f.SetSas(SasMode.Stability); }
            if (eng) { yield return new WaitForSecondsRealtime(1f); f.Stage(); }
            yield return new WaitForSecondsRealtime(9f);
            f.SetCamera(150f, 10f, 70f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot($"454_{name.ToLower()}_flight");
            Log($"{name}: sees altitude {f.active.Altitude:0} m, throttle {f.active.throttle:0.00}, sas {f.active.sas}, frames {c.frames.Count}");
            if (ctl)
            {
                yield return new WaitForSecondsRealtime(8f);
                f.CoopEndFlight();
            }
            yield return WaitFor(() => !root.inFlight, 60f, "back to building");
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot($"455_{name.ToLower()}_after");
            Log($"{name}: back to building; {room.State.summary}" + (room.State.campaign ? $" (money now {root.universe.career.funds:N0})" : ""));
            yield return new WaitForSecondsRealtime(3f);
            root.LeaveCoop();
        }
    }
}
