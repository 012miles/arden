using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 43, two copies of the game (-autotestWalkHost, then -autotestWalkJoin -mdName Mia -mdCode 127.0.0.1:47770):
        /// a room on the pad in a five-seat team ship. The player climbs out as their own Ardling and walks; the host climbs
        /// out too; the ship stays the ship on both screens; the player climbs back in.
        /// </summary>
        IEnumerator WalkHostTour()
        {
            var root = GameRoot.I;
            yield return WaitPlanets();
            var room = NetRoom.Host("captaincarl24", "Walk club", new MissionPlan(), 1, false);
            Log($"room: {room.Error ?? "open"}");
            root.EnterCoop(room);
            yield return WaitFor(() => room.State.players.Count == 2, 90f, "the other player");
            root.hangar.SetDesignFromNet(Presets.TeamHopper());
            yield return new WaitForSecondsRealtime(3f);
            yield return WaitFor(() => Coop.Current.AllReady, 60f, "everyone ready");
            Coop.Current.Launch(root.hangar.design, false, 0);
            yield return WaitFor(() => root.inFlight, 20f, "the launch");
            var f = root.flight;
            string mia = room.State.players.First(p => p.id != room.MyId).name;
            Log($"crew aboard: {string.Join(", ", f.active.parts.SelectMany(p => p.crew))}");
            yield return WaitFor(() => f.active != null && U(f).vessels.Any(v => v.IsEva && v.EvaName == mia), 40f, $"{mia} to climb out");
            Log($"host: {mia} is out: {U(f).vessels.Any(v => v.IsEva && v.EvaName == mia)}; walkers {f.TestWalkers}; ship {f.TestShipName}");
            var miaEva = U(f).vessels.First(v => v.IsEva && v.EvaName == mia);
            var shipV = f.active;
            double startGap = (miaEva.pos - shipV.pos).magnitude;
            yield return new WaitForSecondsRealtime(3f);
            Log($"host: {mia} walked away: {startGap:0.0} m from the ship, then {(miaEva.pos - shipV.pos).magnitude:0.0} m");
            // the host climbs out too: the ship must stay the ship for everyone
            f.GoEva(f.active.parts.First(p => p.crew.Contains("captaincarl24")));
            yield return new WaitForSecondsRealtime(2f);
            Log($"host out: active is me {f.active.EvaName == "captaincarl24"}; the room's ship is {f.TestShipName}; walkers {f.TestWalkers}");
            f.SetCamera(200f, 15f, 12f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("820_host_walking");
            yield return WaitFor(() => !U(f).vessels.Any(v => v.IsEva && v.EvaName == mia), 40f, $"{mia} to climb back in");
            Log($"host: {mia} is back in: {f.active != null && U(f).vessels.Any(v => !v.IsEva && v.parts.Any(p => p.crew.Contains(mia)))}");
            yield return new WaitForSecondsRealtime(3f);
            Log("WALK HOST DONE");
            root.LeaveCoop();
        }

        static Universe U(FlightMode f) => GameRoot.I.universe;

        IEnumerator WalkJoinTour()
        {
            var root = GameRoot.I;
            string name = Arg("-mdName") ?? "Mia";
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
            Log($"{name}: in the room {Coop.Active}");
            if (!Coop.Active) yield break;
            var c = Coop.Current;
            yield return WaitFor(() => !string.IsNullOrEmpty(c.room.State.designJson), 60f, "the rocket");
            yield return new WaitForSecondsRealtime(2f);
            c.room.Ready(true);
            yield return WaitFor(() => root.inFlight, 60f, "the launch");
            var f = root.flight;
            yield return new WaitForSecondsRealtime(3f);
            Log($"{name}: aboard {f.active?.name}, crew {string.Join(", ", f.active.parts.SelectMany(p => p.crew))}");
            // climb out (asks the host)
            f.GoEva(f.active.parts.First(p => p.crew.Count > 0));
            yield return WaitFor(() => f.active != null && f.active.IsEva && f.active.EvaName == name, 20f, "my own Ardling");
            Log($"{name}: I'm out as {f.active?.EvaName}; walkers here {f.TestWalkers}; the ship here {f.TestShipName}");
            // walk forward a little
            f.walkOverride = new Vector2(0, 1);
            yield return new WaitForSecondsRealtime(2f);
            f.walkOverride = null;
            yield return new WaitForSecondsRealtime(1f);
            f.SetCamera(160f, 12f, 10f);
            yield return Shot("821_player_walking");
            // the host climbs out: I must still be my own Ardling, and the ship still the ship
            yield return new WaitForSecondsRealtime(4f);
            Log($"{name}: after the host climbed out: I'm {f.active?.EvaName}; walkers {f.TestWalkers}; the ship {f.TestShipName}");
            yield return Shot("822_both_out");
            // back to the hatch (walking backward the way she came) and in
            f.walkOverride = new Vector2(0, -1);
            yield return new WaitForSecondsRealtime(2f);
            f.walkOverride = null;
            for (int i = 0; i < 20 && f.active != null && f.active.IsEva; i++)
            {
                f.BoardNearest();
                yield return new WaitForSecondsRealtime(1f);
            }
            Log($"{name}: back aboard {f.active != null && !f.active.IsEva} ({f.active?.name})");
            yield return new WaitForSecondsRealtime(4f);
            Log("WALK JOIN DONE");
        }
    }
}
