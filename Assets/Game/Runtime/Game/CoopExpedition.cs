using System;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 34: the expedition's plan in the builder (beside the team): the objective first, then the six resources
    /// (mass, power, budget, data, comms, time) for the rocket on the table, what stops it and what's risky, and the
    /// team getting ready. After a flight, the report.
    /// </summary>
    public partial class Coop
    {
        ExDashboard dash;
        float dashTimer;
        string dashKey;

        ExDashboard Dashboard(VesselDesign d)
        {
            var st = room.State;
            dashTimer -= Time.unscaledDeltaTime;
            string key = $"{st.exScenario}|{st.exDifficulty}|{st.players.Count}|{d?.nodes.Count}";
            if (dash == null || dashTimer <= 0 || key != dashKey)
            {
                dashTimer = 0.5f;
                dashKey = key;
                dash = Rocket.Expedition.Evaluate(d, Rocket.Expedition.Scenario(st.exScenario), st.exDifficulty, st.players.Count);
            }
            return dash;
        }

        /// <summary>The plan: which expedition and how hard (the host or mission control picks), its goals, the six resources, ready.</summary>
        public void DrawExpeditionPlan(Rect r, VesselDesign d)
        {
            var st = room.State;
            var sc = Rocket.Expedition.Scenario(st.exScenario);
            bool picks = IsHost || Has(Control);
            Ui.Box(r); Ui.Box(r);
            float x = r.x + 10, w = r.width - 20, y = r.y + 6;
            var wrap = new GUIStyle(Ui.Small) { wordWrap = true };
            var clip = new GUIStyle(Ui.Small) { wordWrap = false, clipping = TextClipping.Clip };
            Ui.Text(new Rect(x, y, w, 22), "Expedition", Ui.Header);
            y += 24;
            GUI.enabled = picks;
            if (Ui.Press(new Rect(x, y, w, 26), sc.name + "  ▸", Ui.ButtonSmall))
            {
                int i = Array.FindIndex(Rocket.Expedition.Scenarios, s => s.id == sc.id);
                room.Send(new NetMsg { t = "exsc", b = Rocket.Expedition.Scenarios[(i + 1) % Rocket.Expedition.Scenarios.Length].id });
            }
            y += 30;
            float bw = (w - 8) / 3;
            for (int k = 0; k < 3; k++)
                if (Ui.Press(new Rect(x + k * (bw + 4), y, bw, 24), Rocket.Expedition.DifficultyNames[k], st.exDifficulty == k ? Ui.ButtonSmallSel : Ui.ButtonSmall)) room.Send(new NetMsg { t = "exdiff", n = k });
            GUI.enabled = true;
            y += 28;
            float bh = wrap.CalcHeight(new GUIContent(sc.blurb), w);
            Ui.Text(new Rect(x, y, w, bh), sc.blurb, wrap);
            y += bh + 4;
            foreach (var g in sc.goals) { Ui.Text(new Rect(x, y, w, 20), $"○ {g.text}  <color=#8a929c>{g.points}</color>", clip); y += 19; }
            y += 6;
            // the six resources
            var x6 = Dashboard(d);
            Ui.Line(new Rect(x, y, w, 1), new Color(1, 1, 1, 0.12f));
            y += 4;
            void Row(string name, string value, float frac, bool bad, bool warn)
            {
                Ui.Text(new Rect(x, y, 70, 20), name, clip);
                Ui.Bar(new Rect(x + 72, y + 6, 70, 8), frac, bad ? new Color(1f, 0.35f, 0.3f) : warn ? new Color(1f, 0.75f, 0.3f) : new Color(0.45f, 0.85f, 0.55f));
                Ui.Text(new Rect(x + 148, y, w - 148, 20), value, clip);
                y += 20;
            }
            Row("Budget", $"{x6.cost:N0} of {x6.budget:N0}", x6.budget > 0 ? (float)(x6.cost / x6.budget) : 0, x6.cost > x6.budget, x6.cost > x6.budget * 0.9);
            Row("Mass", $"{x6.mass:0.0} t, push {x6.twr:0.0}x", Mathf.Clamp01((float)x6.twr / 2f), x6.twr > 0 && x6.twr < 1.05, x6.twr < 1.25);
            Row("Speed", $"{x6.dv:N0} of ~{sc.dvNeed:N0} m/s", Mathf.Clamp01((float)(x6.dv / sc.dvNeed)), false, x6.dv < sc.dvNeed * 0.85);
            Row("Time", double.IsInfinity(x6.days) ? $"supplies: no limit (trip ~{sc.tripDays:0} d)" : $"supplies {x6.days:0.#} d, trip ~{sc.tripDays:0} d", double.IsInfinity(x6.days) ? 1 : Mathf.Clamp01((float)(x6.days / sc.tripDays)), false, !double.IsInfinity(x6.days) && x6.days < sc.tripDays);
            Row("Power", x6.powerMake > 0 ? $"makes {x6.powerMake:0.#}/s, stores {x6.powerStore:0}" : $"stores {x6.powerStore:0}, makes none", x6.powerMake > x6.powerUse ? 1 : 0.3f, false, x6.powerMake <= x6.powerUse && sc.tripDays > 3);
            Row("Data", x6.instruments > 0 ? $"{x6.instruments} instrument{(x6.instruments == 1 ? "" : "s")}" : "no instruments", Mathf.Clamp01(x6.instruments / 3f), false, x6.instruments == 0);
            Row("Comms", x6.antennas > 0 ? $"{x6.antennas} antenna{(x6.antennas == 1 ? "" : "s")}" : "capsule radio only", x6.antennas > 0 ? 1 : 0.4f, false, x6.antennas == 0 && sc.tripDays > 10);
            Row("Seats", $"{x6.seats} for {x6.players} player{(x6.players == 1 ? "" : "s")}", Mathf.Clamp01(x6.players > 0 ? (float)x6.seats / x6.players : 1), x6.seats < x6.players, false);
            y += 4;
            // what stops it, what's risky
            foreach (var b in x6.blockers.Take(2)) { float h = wrap.CalcHeight(new GUIContent(b), w); Ui.Text(new Rect(x, y, w, h), $"<color=#ff8070>■ {b}</color>", wrap); y += h + 2; }
            foreach (var wn in x6.warnings.Take(Math.Max(0, 3 - x6.blockers.Count)))
            {
                if (y > r.yMax - 90) break;
                float h = wrap.CalcHeight(new GUIContent(wn), w);
                Ui.Text(new Rect(x, y, w, h), $"<color=#ffc860>▲ {wn}</color>", wrap);
                y += h + 2;
            }
            if (y < r.yMax - 78) Ui.Text(new Rect(x, r.yMax - 76, w, 34), $"<color=#8a929c>Like {sc.real}</color>", wrap);
            // ready
            var me = room.Me;
            if (me != null && Ui.Press(new Rect(x, r.yMax - 36, w, 30), me.ready ? "Ready ✓  (click to undo)" : "I'm ready", me.ready ? Ui.ButtonGo : Ui.Button)) room.Ready(!me.ready);
        }

        /// <summary>Height the plan wants (it fills the space between the team and the chat).</summary>
        public const float PlanMinHeight = 470;

        public bool ReportShowing => showReport && !string.IsNullOrEmpty(room.State.exReport);

        /// <summary>The report after an expedition: the score and stars, each line, what went well and what to change.</summary>
        public void DrawReport(float W, float H)
        {
            if (!showReport || string.IsNullOrEmpty(room.State.exReport)) return;
            ExReport rep;
            try { rep = JsonUtility.FromJson<ExReport>(room.State.exReport); } catch { showReport = false; return; }
            var sc = Rocket.Expedition.Scenario(rep.scenario);
            Ui.Dim(new Rect(0, 0, W, H), 0.5f);
            var r = new Rect(W / 2 - 360, Mathf.Max(20, H / 2 - 320), 720, Mathf.Min(640, H - 40));
            Ui.ShieldAround(r, W, H);   // milestone 41: (a whole-screen shield here swallowed Close)
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 20, r.y + 14, 500, 30), "Expedition report", Ui.Title);
            Ui.Text(new Rect(r.x + 20, r.y + 46, r.width - 40, 22), $"<b>{sc.name}</b>  ·  {Rocket.Expedition.DifficultyNames[Mathf.Clamp(rep.difficulty, 0, 2)]}  ·  {rep.team}  ·  {rep.date}", Ui.Small);
            Ui.Text(new Rect(r.xMax - 260, r.y + 12, 240, 40), $"<size=34><b>{rep.score}</b></size> <color=#8a929c>/ 1000</color>", new GUIStyle(Ui.Label) { alignment = TextAnchor.UpperRight });
            Ui.Text(new Rect(r.xMax - 260, r.y + 50, 240, 30), $"<size=24><color=#ffd060>{new string('★', rep.stars)}</color><color=#3a4048>{new string('★', 3 - rep.stars)}</color></size>", new GUIStyle(Ui.Label) { alignment = TextAnchor.UpperRight });
            float y = r.y + 86;
            var wrap = new GUIStyle(Ui.Small) { wordWrap = true };
            foreach (var l in rep.lines) { Ui.Text(new Rect(r.x + 24, y, r.width - 48, 18), l, Ui.Small); y += 19; }
            y += 8;
            Ui.Text(new Rect(r.x + 20, y, 300, 22), "What went well", Ui.Header);
            y += 24;
            foreach (var g in rep.good.Take(5)) { float h = wrap.CalcHeight(new GUIContent(g), r.width - 60); Ui.Text(new Rect(r.x + 30, y, r.width - 60, h), $"<color=#7dff8a>✓</color> {g}", wrap); y += h + 2; }
            if (rep.good.Count == 0) { Ui.Text(new Rect(r.x + 30, y, r.width - 60, 18), "Nothing yet: every expedition teaches something.", Ui.Small); y += 20; }
            y += 6;
            Ui.Text(new Rect(r.x + 20, y, 300, 22), "What to change", Ui.Header);
            y += 24;
            foreach (var c in rep.change.Take(5))
            {
                float h = wrap.CalcHeight(new GUIContent(c), r.width - 60);
                if (y + h > r.yMax - 60) break;
                Ui.Text(new Rect(r.x + 30, y, r.width - 60, h), $"<color=#ffc860>▲</color> {c}", wrap);
                y += h + 2;
            }
            var top = Rocket.Expedition.Top(Application.persistentDataPath, rep.scenario);
            int place = top.FindIndex(t => t.score == rep.score && t.date == rep.date && t.team == rep.team) + 1;
            Ui.Text(new Rect(r.x + 20, r.yMax - 44, 420, 30), place > 0 ? $"#{place} on this computer's leaderboard for {sc.name}" : "Not in the top ten this time", Ui.Small);
            if (Ui.Press(new Rect(r.xMax - 140, r.yMax - 50, 120, 36), "Close", Ui.Button) || In.EscapeDown) showReport = false;
        }
    }
}
