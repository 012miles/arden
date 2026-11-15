using System;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 34: an expedition in flight. The players ride as the crew; the host checks the goals every second
    /// (landed, walked, flag, orbit, outposts searched, home) and tells everyone; the panel shows the goals and how
    /// your Ardling is doing (supplies, air on foot, health); near an outpost or wreck anyone can Scavenge it. Ending the
    /// flight scores the expedition and puts it on the leaderboard. Outposts can be searched in any game, not only here.
    /// </summary>
    public partial class FlightMode
    {
        float exTick;

        /// <summary>The players take the seats (in order), instead of made-up crew: everyone rides in the team's rocket.</summary>
        public void SeatPlayers(System.Collections.Generic.IList<string> names)
        {
            if (active == null || names == null || names.Count == 0) return;
            int k = 0;
            foreach (var p in active.parts)
            {
                if (p.def.crew <= 0) continue;
                p.crew.Clear();
                for (int s = 0; s < p.def.crew && k < names.Count; s++) p.crew.Add(names[k++]);
            }
            if (views.TryGetValue(active, out var old)) { old.Destroy(); views[active] = VesselView.Create(active); }
        }

        /// <summary>The host, once a second: what the expedition has done, and the goals newly met.</summary>
        void ExpeditionTick(float dt)
        {
            var c = Coop.Current;
            if (c == null || !c.Expedition || !c.IsHost || active == null) return;
            exTick -= dt;
            if (exTick > 0) return;
            exTick = 1f;
            var st = c.room.State;
            var p = st.ex;
            var sc = Expedition.Scenario(st.exScenario);
            foreach (var v in U.vessels)
            {
                if (v.destroyed) continue;
                if (v.CrewCount > 0 || v.IsEva) p.flags |= v.careerFlags;
                if (v.IsEva && v.landed && !p.walked.Contains(v.body.name)) p.walked.Add(v.body.name);
                if (v.IsFlag && !p.flagged.Contains(v.body.name)) p.flagged.Add(v.body.name);
            }
            foreach (var id in U.career.scavenged)
            {
                var d = Outposts.Get(id);
                string key = d != null ? $"{d.body}:{id}" : id;
                if (!p.scavenged.Contains(key)) p.scavenged.Add(key);
            }
            int alive = U.vessels.Where(v => !v.destroyed).Sum(v => v.CrewCount);
            p.lost = Math.Max(0, p.crew - alive);
            // home: the crew back down on Arden after going to space
            var ship = active.IsEva ? null : active;
            p.home = ship != null && !ship.destroyed && ship.landed && ship.body.IsHomeWorld && ship.CrewCount > 0 && (p.flags & VFlags.Space) != 0;
            var news = Expedition.Update(sc, p);
            foreach (var g in news) Show($"✓ {g.text}  (+{g.points})", 5f, true);
            if (news.Count > 0) { audio?.Clunk(0.2f); c.room.Touch(); }
        }

        /// <summary>The expedition's goals and your Ardling, on the right (in place of the rocket room's goal box).</summary>
        void DrawExpeditionPanel(Rect r)
        {
            var c = Coop.Current;
            var st = c.room.State;
            var sc = Expedition.Scenario(st.exScenario);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 10, r.y + 4, r.width - 20, 20), $"<b>{sc.name}</b>  <color=#8a929c>{Expedition.DifficultyNames[Mathf.Clamp(st.exDifficulty, 0, 2)]}</color>", Ui.Small);
            float y = r.y + 24;
            var clip = new GUIStyle(Ui.Small) { wordWrap = false, clipping = TextClipping.Clip };
            foreach (var g in sc.goals)
            {
                bool ok = st.ex.done.Contains(g.id);
                Ui.Text(new Rect(r.x + 10, y, r.width - 20, 20), ok ? $"<color=#7dff8a>✓ {g.text}</color>" : $"<color=#c8d0d8>○ {g.text}</color>  <color=#8a929c>{g.points}</color>", clip);
                y += 20;
            }
            // your Ardling: supplies (food, water, air), air on foot, health
            string me = c.room.Me?.name;
            var mine = U.vessels.FirstOrDefault(v => !v.destroyed && v.parts.Any(q => q.crew.Contains(me)));
            y += 4;
            Ui.Line(new Rect(r.x + 10, y, r.width - 20, 1), new Color(1, 1, 1, 0.1f));
            y += 4;
            if (mine == null) { Ui.Text(new Rect(r.x + 10, y, r.width - 20, 18), "<color=#ff8070>Your Ardling was lost</color>", clip); return; }
            double days = mine.SupplyDaysLeft();
            float health = mine.starvingSince >= 0 ? Mathf.Clamp01(1f - (float)((ut - mine.starvingSince) / Supplies.Grace)) : 1f;
            Ui.Text(new Rect(r.x + 10, y, 90, 20), mine.IsEva ? "Suit air" : "Supplies", Ui.Small);
            // on foot: how full the suit is; aboard: the supplies against the trip
            double cap = mine.IsEva ? Math.Max(1e-6, mine.SuppliesCapacity / Math.Max(1, mine.CrewCount)) : Math.Max(1.0, sc.tripDays);
            float frac = double.IsInfinity(days) ? 1f : Mathf.Clamp01((float)(days / cap));
            Ui.Bar(new Rect(r.x + 100, y + 4, r.width - 190, 10), frac, frac < 0.25f ? new Color(1f, 0.4f, 0.3f) : new Color(0.5f, 0.85f, 1f));
            Ui.Text(new Rect(r.xMax - 84, y, 76, 20), Supplies.DaysText(days), clip);
            y += 20;
            Ui.Text(new Rect(r.x + 10, y, 90, 20), "Health", Ui.Small);
            Ui.Bar(new Rect(r.x + 100, y + 4, r.width - 190, 10), health, health < 0.5f ? new Color(1f, 0.35f, 0.25f) : new Color(0.45f, 0.9f, 0.5f));
            Ui.Text(new Rect(r.xMax - 84, y, 76, 20), mine.starvingSince >= 0 ? "starving!" : "fine", clip);
        }

        /// <summary>Height of the expedition panel.</summary>
        float ExpeditionPanelHeight => 24 + Expedition.Scenario(Coop.Current.room.State.exScenario).goals.Length * 20 + 8 + 40 + 6;

        // ---------------------------------------------------------------- outposts and wrecks (any game)

        /// <summary>A Scavenge button when the ship (or an astronaut) is down beside an outpost or wreck that hasn't been searched.</summary>
        void DrawScavenge(float W, float H)
        {
            if (active == null || active.destroyed || mapView) return;
            var l = Outposts.Near(active, ut, out double m);
            if (l == null || U.career.scavenged.Contains(l.id)) return;
            var r = new Rect(W / 2 - 190, H - 316, 380, 38);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 12, r.y + 9, 220, 20), $"⌂ <b>{l.name}</b>  {m:0} m", new GUIStyle(Ui.Small) { wordWrap = false, clipping = TextClipping.Clip });
            if (Ui.Press(new Rect(r.xMax - 126, r.y + 6, 118, 26), "Scavenge", Ui.ButtonSmallSel)) DoScavenge();
        }

        /// <summary>Searches the outpost or wreck nearby (on the host's game in a room). Fills the ship, or the one an astronaut walked from.</summary>
        public void DoScavenge()
        {
            if (Coop.Active && remote) { Coop.Current.room.Send(new NetMsg { t = "cmd", a = "scav" }); return; }
            var l = Outposts.Near(active, ut, out _);
            if (l == null) { Show("Nothing to search within 300 m.", 2.5f); return; }
            var ship = active;
            if (active.IsEva)
            {
                Vector3d here = active.pos;
                ship = U.vessels.Where(v => !v.destroyed && !v.IsEva && !v.IsFlag && v.body == active.body && v.landed && v.parts.Count > 0)
                    .OrderBy(v => (v.pos - here).magnitude).FirstOrDefault(v => (v.pos - here).magnitude < 600) ?? active;
            }
            bool fresh = !U.career.scavenged.Contains(l.id);
            string got = Outposts.Scavenge(l, ship, U.career);
            Show(got, 7f, true);
            audio?.Clunk(0.35f);
            if (fresh && U.career.scavenged.Contains(l.id)) { DropLoot(active.IsEva ? active : ship, l.id); Quest(QuestKind.Search, 1); }   // milestone 38: things to pick up (40: a quest)
        }

        // ---------------------------------------------------------------- the end

        /// <summary>Scores the expedition (host), saves it on the board and puts the report in the room for everyone.</summary>
        void FinishExpedition(Coop c)
        {
            var st = c.room.State;
            var sc = Expedition.Scenario(st.exScenario);
            ExpeditionTick(10f);   // one last look
            var team = string.Join(", ", st.players.Select(q => q.name));
            var rep = Expedition.Score(sc, st.ex, st.exDifficulty, team);
            st.exReport = JsonUtility.ToJson(rep);
            st.exRuns++;
            st.summary = $"{sc.name}: {rep.score} points {new string('★', rep.stars)}";
        }
    }
}
