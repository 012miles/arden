using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>
        /// Milestone 32: the builder bugs, checked in the running game (the root can't be picked up, a part you let go of
        /// goes back, choosing a new part puts the old one back, a placed part stays in your hand, Backspace, undo and
        /// redo); then screenshots of the new builder in Easy words, the search, the presets, the tutorial, the
        /// multiplayer screen and a rocket room's builder.
        /// </summary>
        IEnumerator Builder32Tour()
        {
            var root = GameRoot.I;
            yield return WaitPlanets();
            root.EnterHangar();
            yield return new WaitForSecondsRealtime(1.5f);
            var h = root.hangar;
            int pass = 0, fail = 0;
            void T(bool ok, string what) { Log((ok ? "PASS " : "FAIL ") + what); if (ok) pass++; else fail++; }

            h.TestPresetStarter();
            yield return null;
            string before = h.TestJson;
            int count = h.design.nodes.Count;
            // 1. the root
            h.TestPickUp(0);
            T(h.TestJson == before && h.TestHeldCount == 0, "clicking the root part leaves the rocket where it is (it used to lift the whole rocket)");
            // 2. a picked-up part goes back when you let go
            int tank = h.design.nodes.FindIndex(n => n.id == "tank.s4");
            h.TestPickUp(tank);
            T(h.TestHeldCount > 0 && h.design.nodes.Count < count, "a part picked up off the rocket is in your hand");
            h.TestCancel();
            T(h.TestJson == before && h.TestHeldCount == 0, "letting go (ESC / right-click) puts it back where it was");
            // 3. choosing another part while holding one puts the first one back
            h.TestPickUp(tank);
            h.TestHold("fin.r");
            T(h.design.nodes.Count == count, "choosing a new part from the list puts the picked-up one back (it used to vanish)");
            h.TestCancel();
            T(h.TestJson == before, "and letting go of the new part changes nothing");
            // 4. a new part stays in your hand
            h.TutorialClear();
            h.TestHold("pod.wren");
            h.TestPlace(0, true);
            T(h.design.nodes.Count == 1 && h.TestHeldCount == 0, "the first part goes down as the root and leaves your hand");
            h.TestHold("tank.s2");
            h.TestPlace(0, true);
            T(h.design.nodes.Count == 2 && h.TestHeldCount == 1 && h.HeldId == "tank.s2", "after placing a tank it's still in your hand");
            h.TestPlace(1, true);
            T(h.design.nodes.Count == 3 && h.HeldId == "tank.s2", "so a second one goes straight on");
            h.TestCancel();
            T(h.design.nodes.Count == 3 && h.TestHeldCount == 0, "right-click / ESC puts it down without changing the rocket");
            // 5. Backspace, undo, redo
            h.TestDelete(2);
            T(h.design.nodes.Count == 2, "Backspace deletes the part under the mouse");
            h.TestUndo();
            T(h.design.nodes.Count == 3, "undo brings it back");
            h.TestDelete(0);
            T(h.design.nodes.Count == 3, "Backspace on the root asks first");
            h.TestDelete(0);
            T(h.design.nodes.Count == 0, "a second Backspace clears the rocket");
            h.TestUndo();
            T(h.design.nodes.Count == 3, "undo brings the whole rocket back");
            h.TestRedo();
            T(h.design.nodes.Count == 0, "redo clears it again");
            h.TestUndo();
            T(h.design.nodes.Count == 3, "and undo once more");
            Log($"builder checks: {pass} passed, {fail} failed");

            // ---- pictures
            h.TestPresetStarter();
            h.SetView(28f, 10f, 26f, 6f);
            var was = Words.Level;
            Words.Level = WordLevel.Easy;
            h.TutorialSearch = "engine";
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("480_easy_builder_search");
            h.TutorialClear();
            h.TestHold("pod.wren"); h.TestPlace(0, true);
            h.TestHold("tank.s2"); h.TestPlace(0, true); h.TestCancel();
            h.TutorialSearch = "";
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("481_easy_checklist");
            Words.Level = WordLevel.Standard;
            h.TestPresetStarter();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("482_standard_builder");
            h.TestShowPresets(true);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("483_presets");
            h.TestShowPresets(false);
            h.TestShowFile(true);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("484_file_menu");
            h.TestShowFile(false);
            // the tutorial, at a few steps
            foreach (var (stepNo, name) in new[] { (0, "485_tutorial_welcome"), (1, "486_tutorial_parts"), (4, "487_tutorial_tank"), (9, "488_tutorial_launch") })
            {
                if (stepNo == 4) { h.TutorialClear(); h.TestHold("pod.wren"); h.TestPlace(0, true); }
                if (stepNo == 9) h.TestPresetStarter();
                Tutorial.TestGoto(stepNo);
                yield return new WaitForSecondsRealtime(0.9f);
                yield return Shot(name);
            }
            Tutorial.TestGoto(10);
            root.Launch(h.design, false, 0);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("489_tutorial_flight_space");
            Tutorial.TestGoto(11);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("490_tutorial_flight_height");
            Tutorial.Stop(false);
            Words.Level = was;
            // multiplayer: the one-screen lobby, and a rocket room's builder with the team panel
            root.ShowMission();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("491_multiplayer_lobby");
            var room = NetRoom.Host("captaincarl24", "Rocket club", new MissionPlan(), 1, false);
            Log($"rocket room: {room.Error ?? "open"}");
            if (room.Error == null)
            {
                root.EnterCoop(room);
                yield return new WaitForSecondsRealtime(2f);
                root.hangar.SetDesignFromNet(VesselDesign.Default());
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("492_rocket_room_builder");
                root.LeaveCoop();
                yield return new WaitForSecondsRealtime(1f);
            }
            var bad = Ui.LayoutProblems();
            Log($"layout problems on the last screen: {bad.Count}");
        }
    }
}
