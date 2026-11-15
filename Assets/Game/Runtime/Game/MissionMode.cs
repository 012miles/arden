using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 29: the Mission Design game (title screen, "Mission Design"). Alone, or as a team of up to four, each on
    /// their own device: one creates a room, the others join it from the list or with its code. Each player has a role
    /// (mission lead, spacecraft engineer, science lead, comms and ground) and chooses their part of the mission, while
    /// everyone watches the same dashboard of mass, power, budget, data, communications, fuel and lifetime, with the
    /// reasons behind every warning. Players can ask each other for things. When everyone's ready the mission is flown as
    /// a fast simulation, and the team gets a score and a report on why it went the way it did.
    /// </summary>
    public class MissionMode
    {
        readonly GameRoot root;
        readonly MissionView view;
        enum Page { Hub, Make, Join, Room, Board, Designs }
        Page page = Page.Hub;
        string playerName, roomName = "", joinCode = "", chatText = "", askText = "", designName = "", pasteCode = "", note;
        bool hubCampaign = true;
        int hubKind = 2;                     // 2 expedition (milestone 33), 1 rocket room
        float noteUntil;
        int tab, askRole = 1, lastStateVersion = -1;
        float flyStart = -1f;
        MissionResult result;
        string resultSig;
        MSheet sheet;
        string sheetSig;
        Vector2 leftScroll, warnScroll;
        string hover;
        public const float FlyLength = 22f;

        public MissionMode(GameRoot root) { this.root = root; view = new MissionView(root); }

        NetRoom Room => NetRoom.Current;
        bool Easy => Words.Level == WordLevel.Easy;

        public void Enter()
        {
            playerName = PlayerPrefs.GetString("mdName", "Player");
            page = Page.Hub;
            view.Enter();
        }

        public void Leave()
        {
            Room?.Stop();
            view.Leave();
            PlayerPrefs.SetString("mdName", playerName ?? "Player");
        }

        /// <summary>Off to a rocket room: the picture goes, the room stays open.</summary>
        public void LeaveKeepRoom()
        {
            view.Leave();
            PlayerPrefs.SetString("mdName", playerName ?? "Player");
            page = Page.Hub;
        }

        void Toast(string text) { note = text; noteUntil = Time.unscaledTime + 4f; }

        public void Update()
        {
            var r = Room;
            r?.Update(Time.unscaledDeltaTime);
            if (r != null && r.Connected && r.State.Rocket) { root.EnterCoop(r); return; }   // a rocket room or an expedition (milestone 34: built on the rocket room)
            bool lobby = page == Page.Hub || page == Page.Make || page == Page.Join;
            if (r != null && lobby && !r.IsHost) r.Listen();
            if (r != null && !r.IsHost && r.Connected && lobby) page = Page.Room;
            if (r != null && r.Connected && !r.Solo && page != Page.Join && r.State.activity == 0) r.Listen();   // other teams on the same mission
            if (r != null && !r.Connected && r.Error != null && page == Page.Room && !r.IsHost) { page = Page.Join; Notice(r.Error); }
            var plan = r != null && r.Connected ? r.State.plan : new MissionPlan();
            string sig = JsonUtility.ToJson(plan);
            if (sig != sheetSig || sheet == null) { sheetSig = sig; sheet = MissionDesign.Evaluate(plan, root.bodies, Easy); }
            // the result arrives (or changes): start the fly-through
            if (r != null && r.State.resultJson != resultSig)
            {
                resultSig = r.State.resultJson;
                result = string.IsNullOrEmpty(resultSig) ? null : JsonUtility.FromJson<MissionResult>(resultSig);
                if (result != null && r.State.phase == 1) { flyStart = Time.unscaledTime; }
            }
            if (r != null && r.State.phase == 1 && flyStart < 0) flyStart = Time.unscaledTime;
            if (r != null && r.State.phase != 1) flyStart = -1f;
            float fly = r != null && r.State.phase == 1 && flyStart >= 0 ? Mathf.Clamp01((Time.unscaledTime - flyStart) / FlyLength) : -1f;
            if (r != null && r.IsHost && r.State.phase == 1 && fly >= 1f)
            {
                r.SetPhase(2);
                if (result != null) MissionScores.Add(result, MissionDesign.ScenarioOf(r.State.plan).name);
                if (r.State.campaign)
                {
                    int was = r.State.unlocked;
                    r.State.unlocked = MissionDesign.UnlockedFrom(id => MissionScores.Best(id)?.stars ?? 0);
                    if (r.State.unlocked > was && r.State.unlocked <= MissionDesign.CampaignOrder.Length) { r.Chat($"New mission unlocked: {MissionDesign.CampaignOrder[r.State.unlocked - 1].name}!"); }
                    r.Touch();
                }
            }
            if (r != null && !r.IsHost && r.State.phase == 2 && result != null && r.StateVersion != lastStateVersion && r.State.runs > 0)
            {
                lastStateVersion = r.StateVersion;
                MissionScores.AddOnce(result, MissionDesign.ScenarioOf(r.State.plan).name);
            }
            view.Update(plan, sheet, fly);
        }

        // ---------------------------------------------------------------- screens

        public void OnGUI()
        {
            float W = Ui.W, H = Ui.H;
            var r = Room;
            if (page == Page.Hub || page == Page.Make || page == Page.Join) DrawLobby(W, H);
            else if (page == Page.Board) DrawBoard(W, H);
            else if (page == Page.Designs) DrawDesigns(W, H);
            else if (r != null && r.Connected)
            {
                if (r.State.phase == 0) DrawDesign(W, H);
                else if (r.State.phase == 1) DrawFlying(W, H);
                else DrawReport(W, H);
            }
            else DrawLobby(W, H);
            if (note != null && Time.unscaledTime < noteUntil)
            {
                var nr = new Rect(W * 0.5f - 300, H * 0.5f - 22, 600, 44);
                Ui.Box(nr); Ui.Box(nr);
                Ui.Text(nr, note, Ui.Center);
            }
        }

        /// <summary>A message on the multiplayer screen (why a room closed, say).</summary>
        public void Notice(string text) { note = text; noteUntil = Time.unscaledTime + 7f; }

        /// <summary>
        /// Milestone 32 (captaincarl24: multiplayer had "too much buttons"): one screen instead of three. Your name once;
        /// host a game on the left (campaign or sandbox, a rocket room or a mission design room); the games on your network
        /// on the right, each with Join, and a box for a code. It listens for games the whole time it's open.
        /// </summary>
        void DrawLobby(float W, float H)
        {
            if (Room == null) NetRoom.Current = new NetRoom { IsHost = false };   // something to listen with
            float pw = Mathf.Min(1020, W - 80), x0 = Mathf.Max(40, (W - pw) * 0.5f), y0 = Mathf.Max(70, H * 0.1f);
            var r = new Rect(x0, y0, pw, Mathf.Min(560, H - y0 - 40));
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 24, r.y + 14, 500, 40), "MULTIPLAYER", Ui.Title);
            Ui.Text(new Rect(r.x + 24, r.y + 50, r.width - 48, 22), "Up to four players, each on their own computer, on the same Wi-Fi.", Ui.Small);
            Ui.Text(new Rect(r.x + 24, r.y + 86, 100, 24), "Your name", Ui.Label);
            playerName = GUI.TextField(new Rect(r.x + 124, r.y + 82, 260, 32), playerName ?? "", 16, Ui.Field);
            // milestone 34: your own Ardling (how the others see you)
            var myLook = MyArdling.LookOf(MyArdling.Code);
            if (Ui.Press(new Rect(r.x + 396, r.y + 82, 170, 32), "    My Ardling...", Ui.Button)) { PlayerPrefs.SetString("mdName", playerName ?? "Player"); MyArdling.Show(); }
            GUI.DrawTexture(new Rect(r.x + 404, r.y + 86, 24, 24), Ardlings.FaceTexture(myLook.face, myLook.paper));
            Ui.Line(new Rect(r.x + 404, r.y + 111, 24, 3), myLook.suit);
            var wrap = new GUIStyle(Ui.Small) { wordWrap = true };

            // ---- host
            float colW = (r.width - 72) * 0.45f;
            var hostR = new Rect(r.x + 24, r.y + 132, colW, r.height - 200);
            Ui.Box(hostR);
            Ui.Text(new Rect(hostR.x + 14, hostR.y + 10, hostR.width - 28, 26), "Host a game", Ui.Header);
            float y = hostR.y + 46;
            float half = (hostR.width - 34) / 2;
            if (Ui.Press(new Rect(hostR.x + 14, y, half, 34), "Campaign", hubCampaign ? Ui.ButtonSel : Ui.Button)) hubCampaign = true;
            if (Ui.Press(new Rect(hostR.x + 20 + half, y, half, 34), "Sandbox", !hubCampaign ? Ui.ButtonSel : Ui.Button)) hubCampaign = false;
            y += 40;
            Ui.Text(new Rect(hostR.x + 14, y, hostR.width - 28, 40), hubCampaign ? "A space program you build up together: money, research, contracts. Kept on your computer for next time." : "Every part, no money: build and fly anything.", wrap);
            y += 46;
            // milestone 33: a rocket room or an expedition (the separate mission design game is gone: planning is part of an expedition now)
            if (hubKind != 1 && hubKind != 2) hubKind = 2;
            if (Ui.Press(new Rect(hostR.x + 14, y, half, 34), "Expedition", hubKind == 2 ? Ui.ButtonSel : Ui.Button)) hubKind = 2;
            if (Ui.Press(new Rect(hostR.x + 20 + half, y, half, 34), "Rocket room", hubKind == 1 ? Ui.ButtonSel : Ui.Button)) hubKind = 1;
            y += 40;
            Ui.Text(new Rect(hostR.x + 14, y, hostR.width - 28, 54), hubKind == 1 ? "Build a rocket and fly it together: a builder, a pilot, a flight engineer and mission control."
                : "The builder builds one rocket with the real parts and everyone rides in it as an Ardling: a mission with a budget, the real solar system, supplies to survive on, outposts to scavenge, and a scored report. Works on your own too.", wrap);
            y += 58;
            if (Ui.Press(new Rect(hostR.x + 14, hostR.yMax - 58, hostR.width - 28, 44), "Host", Ui.Button))
            {
                PlayerPrefs.SetString("mdName", playerName ?? "Player");
                Room?.Stop();
                var plan = new MissionPlan();
                if (hubCampaign) plan.scenario = MissionDesign.CampaignOrder[0].id;
                var room = NetRoom.Host(playerName, roomName, plan, hubKind, hubCampaign);
                if (room.Error != null) Toast(room.Error);
                else { root.EnterCoop(room); return; }
            }

            // ---- join
            var joinR = new Rect(hostR.xMax + 24, hostR.y, r.xMax - 24 - hostR.xMax - 24, hostR.height);
            Ui.Box(joinR);
            Ui.Text(new Rect(joinR.x + 14, joinR.y + 10, joinR.width - 28, 26), "Join a game", Ui.Header);
            var cur = Room;
            var found = cur != null && !cur.IsHost ? cur.Found : new List<RoomInfo>();
            y = joinR.y + 46;
            if (found.Count == 0)
            {
                Ui.Text(new Rect(joinR.x + 14, y, joinR.width - 28, 44), "Looking for games on your network...\nWhen a friend hosts one, it shows up here.", wrap);
                y += 52;
            }
            var clip = new GUIStyle(Ui.Small) { wordWrap = false, clipping = TextClipping.Clip };
            foreach (var f in found.OrderBy(f => f.name).Take(5))
            {
                var row = new Rect(joinR.x + 10, y, joinR.width - 20, 44);
                Ui.Line(row, new Color(1, 1, 1, 0.05f));
                Ui.Text(new Rect(row.x + 8, row.y + 3, row.width - 110, 20), $"<b>{f.name}</b>", clip);
                Ui.Text(new Rect(row.x + 8, row.y + 22, row.width - 110, 18), $"{(f.activity == 1 ? "Rocket room" : f.activity == 2 ? "Expedition" : "Mission design")} · {(f.campaign ? "campaign" : "sandbox")} · {f.players}/4 players", clip);
                GUI.enabled = f.players < 4;
                if (Ui.Press(new Rect(row.xMax - 94, row.y + 6, 86, 32), f.players < 4 ? "Join" : "Full", Ui.Button)) { PlayerPrefs.SetString("mdName", playerName ?? "Player"); DoJoin(f.host + ":" + f.port); }
                GUI.enabled = true;
                y += 50;
            }
            float cy = joinR.yMax - 96;
            Ui.Text(new Rect(joinR.x + 14, cy + 6, 120, 24), "Have a code?", Ui.Small);
            joinCode = GUI.TextField(new Rect(joinR.x + 110, cy, joinR.width - 214, 32), joinCode ?? "", 21, Ui.Field);
            if (Ui.Press(new Rect(joinR.xMax - 98, cy, 84, 32), "Join", Ui.Button) && !string.IsNullOrWhiteSpace(joinCode)) { PlayerPrefs.SetString("mdName", playerName ?? "Player"); DoJoin(joinCode); }
            string status = cur != null && !cur.IsHost ? (cur.Error != null ? $"<color=#ff9080>{cur.Error}</color>" : cur.Status) : null;
            Ui.Text(new Rect(joinR.x + 14, cy + 38, joinR.width - 28, 40), status ?? "The code is on the host's screen. Some school networks block games; a phone hotspot works.", wrap);

            if (Ui.Press(new Rect(r.x + 24, r.yMax - 56, 120, 40), "Back", Ui.Button)) { Room?.Stop(); NetRoom.Current = null; root.LeaveMission(); return; }
            if (Ui.Press(new Rect(r.x + 152, r.yMax - 56, 140, 40), "Leaderboard", Ui.Button)) { page = Page.Board; }
        }
        void DoJoin(string code)
        {
            var old = Room;
            var found = old?.Found.ToList();
            old?.Stop();
            var nr = NetRoom.Join(playerName, code);
            if (found != null) nr.Found.AddRange(found);
            if (nr.Error != null) Toast(nr.Error);
        }

        // ---------------------------------------------------------------- designing

        static readonly string[] Tabs = { "Mission", "Craft", "Science", "Orbit", "Comms" };
        static readonly MRole[] TabRole = { MRole.Lead, MRole.Engineer, MRole.Scientist, MRole.Lead, MRole.Comms };
        static readonly Color[] RoleColor = { new Color(0.95f, 0.6f, 0.25f), new Color(0.4f, 0.65f, 1f), new Color(0.5f, 0.9f, 0.5f), new Color(0.85f, 0.55f, 0.95f) };

        void DrawTopBar(float W)
        {
            var r = Room;
            var bar = new Rect(10, 8, W - 20, 52);
            Ui.Box(bar);
            var sc = MissionDesign.ScenarioOf(r.State.plan);
            Ui.Text(new Rect(bar.x + 14, bar.y + 4, 420, 24), $"MISSION DESIGN  <size=13>· {sc.name}</size>", Ui.Header);
            Ui.Text(new Rect(bar.x + 14, bar.y + 28, 520, 20), r.Solo ? "On your own: you do every job." : $"Room <b>{r.State.room}</b>   ·   code <b><color=#ffd56a>{r.State.code}</color></b>", Ui.Small);
            float x = bar.x + 560;
            foreach (var p in r.State.players)
            {
                var pr = new Rect(x, bar.y + 8, 196, 36);
                Ui.Line(pr, p.id == r.MyId ? new Color(1, 1, 1, 0.12f) : new Color(1, 1, 1, 0.05f));
                Ui.Text(new Rect(pr.x + 8, pr.y + 1, 150, 18), $"{(p.ready ? "<color=#7dff8a>✓</color> " : "")}<b>{p.name}</b>{(p.id == 1 ? " (host)" : "")}", Ui.Small);
                for (int k = 0; k < 4; k++)
                    if (p.Has((MRole)k)) Ui.Line(new Rect(pr.x + 8 + k * 20, pr.y + 22, 16, 8), RoleColor[k]);
                x += 202;
            }
            if (Ui.Press(new Rect(bar.xMax - 100, bar.y + 10, 88, 32), "Leave", Ui.ButtonSmall)) { Room?.Stop(); NetRoom.Current = null; page = Page.Hub; result = null; resultSig = null; }
        }

        void DrawDesign(float W, float H)
        {
            var r = Room;
            DrawTopBar(W);
            // ---- left: the choices, by tab
            var left = new Rect(10, 68, 430, H - 78 - 150);
            Ui.Box(left); Ui.Box(left);
            float tx = left.x + 6;
            for (int i = 0; i < Tabs.Length; i++)
            {
                bool mine = r.Can(TabRole[i]);
                float tw = 80;
                var trc = new Rect(tx, left.y + 6, tw, 30);
                if (Ui.Press(trc, Tabs[i], tab == i ? Ui.ButtonSmallSel : Ui.ButtonSmall)) tab = i;
                Ui.Line(new Rect(trc.x + 2, trc.yMax - 3, trc.width - 4, 3), mine ? RoleColor[(int)TabRole[i]] : new Color(1, 1, 1, 0.1f));
                tx += tw + 4;
            }
            var role = TabRole[tab];
            bool can = r.Can(role);
            Ui.Text(new Rect(left.x + 10, left.y + 40, left.width - 20, 20), can ? $"<color=#{ColorUtility.ToHtmlStringRGB(RoleColor[(int)role])}>Your job</color> as {MissionDesign.RoleNames[(int)role].ToLower()}: {MissionDesign.RoleJobs[(int)role]}"
                : $"The {MissionDesign.RoleNames[(int)role].ToLower()}'s choice ({Holder(role)}). Ask them below.", new GUIStyle(Ui.Small) { wordWrap = true });
            var inner = new Rect(left.x + 6, left.y + 80, left.width - 12, left.height - 86 - 64);
            hover = null;
            var content = new Rect(0, 0, inner.width - 18, ContentHeight(tab));
            leftScroll = GUI.BeginScrollView(inner, leftScroll, content);
            DrawChoices(tab, content.width, can);
            GUI.EndScrollView();
            // what the hovered (or a picked) choice is
            string blurb = hover;
            var br = new Rect(left.x + 8, left.yMax - 64, left.width - 16, 58);
            Ui.Line(br, new Color(0, 0, 0, 0.25f));
            Ui.Text(new Rect(br.x + 6, br.y + 4, br.width - 12, br.height - 8), blurb ?? "Point at a choice to read what it does.", new GUIStyle(Ui.Small) { wordWrap = true });

            // ---- right: the dashboard
            DrawDashboard(new Rect(W - 400, 68, 390, H - 78 - 150));
            // ---- bottom: team talk and the go button
            DrawTeamBar(new Rect(10, H - 146, W - 20, 136));
        }

        string Holder(MRole role) => string.Join(", ", Room.State.players.Where(p => p.Has(role)).Select(p => p.name).DefaultIfEmpty("nobody"));

        float ContentHeight(int t) => t == 0 ? 120 + MissionDesign.Scenarios.Length * 64 + 200 + 330
            : t == 1 ? 7 * 34 + MissionDesign.Parts.Count(p => p.cat <= MCat.Thermal && p.cat != MCat.Instrument) * 30 + 60
            : t == 2 ? 2 * 34 + (MissionDesign.In(MCat.Instrument).Count() + MissionDesign.In(MCat.Users).Count()) * 30 + 40
            : t == 3 ? 2 * 34 + 6 * 58 + MissionDesign.Vehicles.Length * 46 + 40
            : 2 * 34 + (MissionDesign.In(MCat.Antenna).Count() + MissionDesign.In(MCat.Ground).Count()) * 30 + 140;

        void Section(ref float y, float w, string title)
        {
            Ui.Text(new Rect(4, y, w, 24), title, Ui.Bold);
            y += 28;
        }

        bool Choice(ref float y, float w, string label, string detail, bool selected, bool can, string blurb, float h = 28)
        {
            var rc = new Rect(4, y, w - 8, h);
            bool clicked = false;
            GUI.enabled = can;
            if (Ui.Press(rc, GUIContent.none, selected ? Ui.RowSel : Ui.Row)) clicked = true;
            GUI.enabled = true;
            Ui.Text(new Rect(rc.x + 8, rc.y + 4, rc.width * 0.42f, 20), (selected ? "● <b>" : "○ ") + label + (selected ? "</b>" : ""), selected ? PickedStyle : Ui.Small);
            Ui.Text(new Rect(rc.x + rc.width * 0.36f, rc.y + 5, rc.width * 0.64f - 8, 20), detail, selected ? PickedDetail : DetailStyle);
            if (rc.Contains(Event.current.mousePosition)) hover = blurb;
            y += h + 2;
            return clicked;
        }

        static GUIStyle pickedStyle, pickedDetail, detailStyle;
        static GUIStyle PickedStyle => pickedStyle ??= new GUIStyle(Ui.Small) { normal = { textColor = Color.white }, clipping = TextClipping.Clip, wordWrap = false };
        static GUIStyle PickedDetail => pickedDetail ??= new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight, normal = { textColor = new Color(0.92f, 0.96f, 1f) }, fontSize = Math.Max(9, Ui.Small.fontSize - 1), clipping = TextClipping.Clip, wordWrap = false };
        static GUIStyle DetailStyle => detailStyle ??= new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight, fontSize = Math.Max(9, Ui.Small.fontSize - 1), clipping = TextClipping.Clip, wordWrap = false };

        static string PartLine(MPart p)
        {
            var bits = new List<string>();
            if (p.mass > 0) bits.Add($"{p.mass:0.#} kg");
            if (p.power > 0) bits.Add($"{p.power:0} W");
            if (p.solarW > 0) bits.Add($"+{p.solarW:0} W");
            if (p.rtgW > 0) bits.Add($"+{p.rtgW:0} W");
            if (p.batteryWh > 0) bits.Add($"{p.batteryWh:0} Wh");
            if (p.pointing > 0) bits.Add($"{p.pointing:0.##} deg");
            if (p.storageGB > 0) bits.Add($"{p.storageGB:0} GB");
            if (p.cat == MCat.Antenna) bits.Add($"max {p.maxMbps:0} Mbps");
            if (p.fuelKg > 0) bits.Add($"{p.fuelKg:0} kg fuel");
            if (p.heaterW > 0) bits.Add($"{p.heaterW:0} W heat");
            if (p.capacityKg > 0) bits.Add($"holds {p.capacityKg:0} kg, {p.slots:0} instr.");
            if (p.dataGB > 0) bits.Add($"{p.dataGB:0.##} GB/day");
            if (p.cat == MCat.Ground) bits.Add($"{p.opsPerYear:0} M/yr");
            if (p.cost > 0) bits.Add($"{p.cost:0.#} M");
            return string.Join(" · ", bits);
        }

        void DrawChoices(int t, float w, bool can)
        {
            var r = Room;
            var plan = r.State.plan;
            float y = 4;
            switch (t)
            {
                case 0:
                {
                    Section(ref y, w, "What is the mission for?");
                    bool camp = r.State.campaign;
                    if (camp) { Ui.Text(new Rect(4, y - 4, w - 8, 20), $"<color=#ffd56a>Campaign</color>: {Math.Min(r.State.unlocked, MissionDesign.CampaignOrder.Length)} of {MissionDesign.CampaignOrder.Length} missions open; 1 star opens the next.", Ui.Small); y += 22; }
                    foreach (var s in MissionDesign.Scenarios)
                    {
                        if (camp && s.sandbox) continue;
                        bool open = !camp || MissionDesign.OpenInCampaign(s.id, r.State.unlocked);
                        string stars = s.sandbox ? "free" : new string('★', s.difficulty + 1);
                        var best = MissionScores.Best(s.id);
                        string done = best != null && best.stars > 0 ? $"  <color=#ffd56a>{new string('★', best.stars)}</color>" : "";
                        if (Choice(ref y, w, s.name + done, open ? $"{stars}  ·  {MissionDesign.Scenario(s.id).body}" + (s.budget > 0 ? $"  ·  {s.budget:0} M" : "") : "locked", plan.scenario == s.id, can && open, open ? s.brief : "Finish the mission before this one to unlock it.", 28)) r.Scenario(s.id);
                        Ui.Text(new Rect(16, y - 2, w - 24, 30), $"<color=#9fb2c4>{s.goal}</color>", Ui.Small);
                        y += 30;
                    }
                    var sc = MissionDesign.ScenarioOf(plan);
                    if (sc.sandbox)
                    {
                        Section(ref y, w, "Free design: where, and what to study");
                        float x = 4;
                        foreach (var b in root.bodies.Where(b => !b.isStar && !b.IsGasGiant))
                        {
                            GUI.enabled = can;
                            if (Ui.Press(new Rect(x, y, 66, 26), b.name, plan.body == b.name ? Ui.ButtonSmallSel : Ui.ButtonSmall)) r.Sandbox(b.name, null, 0);
                            GUI.enabled = true;
                            x += 70; if (x > w - 70) { x = 4; y += 30; }
                        }
                        y += 32; x = 4;
                        foreach (var tag in new[] { "map", "weather", "ice", "minerals", "volcano", "space", "relay" })
                        {
                            GUI.enabled = can;
                            if (Ui.Press(new Rect(x, y, 66, 26), tag, plan.sandboxTag == tag ? Ui.ButtonSmallSel : Ui.ButtonSmall)) r.Sandbox(null, tag, 0);
                            GUI.enabled = true;
                            x += 70; if (x > w - 70) { x = 4; y += 30; }
                        }
                        y += 34;
                    }
                    if (!sc.sandbox && can)
                    {
                        if (Ui.Press(new Rect(4, y, w - 8, 28), "Start from an example design (then improve it!)", Ui.ButtonSmall)) r.LoadPlan(MissionDesign.Example(plan.scenario));
                        y += 36;
                    }
                    Section(ref y, w, "What counts as success");
                    foreach (var line in Requirements(sc)) { Ui.Text(new Rect(12, y, w - 16, 20), "• " + line, Ui.Small); y += 20; }
                    y += 8;
                    var wst = new GUIStyle(Ui.Small) { wordWrap = true };
                    string real = MissionDesign.RealMissions(plan);
                    if (real.Length > 0)
                    {
                        Section(ref y, w, "Real missions like this");
                        float rh = wst.CalcHeight(new GUIContent(real), w - 20);
                        Ui.Text(new Rect(10, y, w - 20, rh), $"<color=#c8d6e4>{real}</color>", wst);
                        y += rh + 10;
                    }
                    if (!r.Solo && !sc.sandbox)
                    {
                        Section(ref y, w, "Other teams on this mission");
                        y = DrawRivals(10, y, w - 20, wst);
                    }
                    break;
                }
                case 1:
                {
                    foreach (var (cat, title) in new[] { (MCat.Bus, "Frame"), (MCat.Power, "Power"), (MCat.Battery, "Battery"), (MCat.Attitude, "Pointing"), (MCat.Computer, "Computer"), (MCat.Propulsion, "Engine"), (MCat.Thermal, "Keeping warm") })
                    {
                        Section(ref y, w, title);
                        if (cat == MCat.Power)
                        {
                            Ui.Text(new Rect(w - 210, y - 26, 80, 22), "Wings:", Ui.Small);
                            for (int k = 1; k <= 4; k++)
                            {
                                GUI.enabled = can;
                                if (Ui.Press(new Rect(w - 160 + (k - 1) * 38, y - 28, 34, 24), k.ToString(), plan.wings == k ? Ui.ButtonSmallSel : Ui.ButtonSmall)) r.Wings(k);
                                GUI.enabled = true;
                            }
                        }
                        foreach (var p in MissionDesign.In(cat))
                        {
                            if (p.advanced && Easy) continue;
                            string cur = cat == MCat.Bus ? plan.bus : cat == MCat.Power ? plan.power : cat == MCat.Battery ? plan.battery : cat == MCat.Attitude ? plan.attitude : cat == MCat.Computer ? plan.computer : cat == MCat.Propulsion ? plan.propulsion : plan.thermal;
                            if (Choice(ref y, w, p.name, PartLine(p), cur == p.id, can, p.blurb)) r.Pick(p.id);
                        }
                    }
                    break;
                }
                case 2:
                {
                    var bus = MissionDesign.Part(plan.bus);
                    Section(ref y, w, $"Instruments ({plan.instruments.Count} of {bus?.slots ?? 4:0} places)");
                    var sc = MissionDesign.ScenarioOf(plan);
                    foreach (var p in MissionDesign.In(MCat.Instrument))
                    {
                        if (p.advanced && Easy) continue;
                        bool useful = MissionDesign.Wanted(sc, p);
                        if (Choice(ref y, w, (useful ? "<color=#ffd56a>" : "") + p.name + (useful ? "</color>" : ""), PartLine(p), plan.instruments.Contains(p.id), can, p.blurb + (useful ? " (Good for this goal.)" : ""))) r.Pick(p.id);
                    }
                    Section(ref y, w, "Who uses the data");
                    foreach (var p in MissionDesign.In(MCat.Users))
                        if (Choice(ref y, w, p.name, $"x{p.scienceMult:0.#} science · {p.cost:0} M + {p.opsPerYear:0} M/yr", plan.users == p.id, can, p.blurb)) r.Pick(p.id);
                    break;
                }
                case 3:
                {
                    var b = sheet.body;
                    Section(ref y, w, $"Orbit round {b.name}");
                    foreach (var o in MissionDesign.OrbitsAt(b))
                    {
                        double alt = MissionDesign.AltitudeOf(b, o) / 1000, ecl = MissionDesign.EclipseOf(b, o), cov = MissionDesign.MaxCoverageOf(b, o);
                        if (Choice(ref y, w, MissionDesign.OrbitNames[(int)o], $"{alt:0} km · sees {cov * 100:0}% · {ecl * 100:0}% shadow", plan.orbit == o, r.Can(MRole.Lead), MissionDesign.OrbitBlurb(o), 28)) r.Orbit(o);
                        Ui.Text(new Rect(16, y - 2, w - 24, 26), $"<color=#9fb2c4>detail {MissionDesign.ResolutionOf(b, o) * 100:0}% · once round in {MissionDesign.PeriodOf(b, o) / 60:0} min · in touch {MissionDesign.ContactHours(b, o, MissionDesign.Part(plan.ground)):0.#} h/day</color>", Ui.Small);
                        y += 26;
                    }
                    Section(ref y, w, "Rocket");
                    foreach (var v in MissionDesign.Vehicles)
                    {
                        float capacity = v.CapacityFor(sheet.dest);
                        if (Choice(ref y, w, v.name, $"{(capacity > 0 ? $"{capacity:N0} kg" : "can't reach")} to {MissionDesign.DestText(sheet.dest, b)} · {v.cost:0} M · works {v.reliability * 100:0}%", plan.vehicle == v.id, r.Can(MRole.Lead), v.blurb, 28)) r.Rocket(v.id);
                        y += 14;
                    }
                    break;
                }
                case 4:
                {
                    Section(ref y, w, "Antenna");
                    foreach (var p in MissionDesign.In(MCat.Antenna))
                        if (!(p.advanced && Easy) && Choice(ref y, w, p.name, PartLine(p), plan.antenna == p.id, can, p.blurb)) r.Pick(p.id);
                    Section(ref y, w, "Ground stations");
                    foreach (var p in MissionDesign.In(MCat.Ground))
                        if (!(p.advanced && Easy) && Choice(ref y, w, p.name, PartLine(p), plan.ground == p.id, can, p.blurb)) r.Pick(p.id);
                    y += 10;
                    Ui.Text(new Rect(8, y, w - 12, 130), $"Data falls off with distance: twice as far, a quarter as fast. From {sheet.body.name} the signal travels {MissionDesign.Dist(sheet.commDistanceKm)}. Right now: {sheet.rateMbps:0.###} Mbps for {sheet.contactHours:0.#} hours a day = {sheet.downPerDayGB:0.##} GB a day home, against {sheet.dataPerDayGB:0.#} GB a day made.", new GUIStyle(Ui.Small) { wordWrap = true });
                    break;
                }
            }
        }

        static IEnumerable<string> Requirements(MScenario sc)
        {
            if (sc.coverageNeeded > 0) yield return $"See {sc.coverageNeeded * 100:0}% of {sc.body} with {string.Join("/", sc.tags)} instruments";
            if (sc.scienceNeeded > 0) yield return $"At least {sc.scienceNeeded:0} science points";
            if (sc.dataNeededGB > 0) yield return $"At least {sc.dataNeededGB:0} GB of data sent home";
            if (sc.relayNeeded > 0) yield return $"Relay contact {sc.relayNeeded * 100:0}% of the time";
            yield return $"Keep working for {sc.lifeYears:0.#} years";
            yield return sc.budget > 0 ? $"Build and run it for under {sc.budget:0} M" : "No budget limit";
        }

        void DrawDashboard(Rect r)
        {
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 12, r.y + 8, r.width - 24, 24), "Mission dashboard", Ui.Header);
            // concept maturity (after NASA's Concept Maturity Levels)
            Ui.Text(new Rect(r.x + 12, r.y + 34, 200, 20), $"Design maturity: level {sheet.maturity} of 6", Ui.Small);
            for (int k = 0; k < 6; k++) Ui.Line(new Rect(r.x + 200 + k * 30, r.y + 38, 26, 10), k < sheet.maturity ? new Color(0.45f, 0.85f, 1f) : new Color(1, 1, 1, 0.1f));
            float y = r.y + 58;
            foreach (var g in sheet.gauges)
            {
                Color c = g.state == 0 ? new Color(0.4f, 0.9f, 0.5f) : g.state == 1 ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.42f, 0.38f);
                Ui.Text(new Rect(r.x + 12, y, 80, 20), g.name, Ui.Small);
                bool lifeGauge = g.name == "Life" || g.name == "Contact";
                string val = g.name == "Life" ? $"needs {g.used:0.#}, lasts {g.have:0.#} {g.unit}" : g.name == "Contact" ? $"{g.used:0.#} {g.unit}" : $"{Short(g.used)} / {Short(g.have)} {g.unit}";
                Ui.Text(new Rect(r.x + 150, y, r.width - 162, 20), val, new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight });
                float frac = lifeGauge ? (g.name == "Life" ? Mathf.Clamp01(g.have / Mathf.Max(0.01f, g.used)) : g.used / 24f) : Mathf.Clamp01(g.used / Mathf.Max(1e-6f, g.have));
                Ui.Bar(new Rect(r.x + 12, y + 20, r.width - 24, 6), frac, c);
                var hr = new Rect(r.x + 8, y, r.width - 16, 30);
                if (hr.Contains(Event.current.mousePosition)) hover = g.text;
                y += 32;
            }
            y += 4;
            Ui.Text(new Rect(r.x + 12, y, r.width - 24, 20), sheet.blockers.Count > 0 ? "<color=#ff8070><b>Can't launch yet</b></color>" : sheet.warnings.Count > 0 ? "<color=#ffc860><b>It'll fly, but...</b></color>" : "<color=#7dff8a><b>Looks good!</b></color>", Ui.Small);
            y += 22;
            var wr = new Rect(r.x + 8, y, r.width - 16, r.yMax - y - 8);
            var lines = sheet.blockers.Select(b => "<color=#ff9080>✗ " + b + "</color>").Concat(sheet.warnings.Select(w => "<color=#ffd080>! " + w + "</color>")).ToList();
            if (lines.Count == 0) lines.Add($"Every gauge is in the green. {sheet.massKg:0} kg, {sheet.totalCostM:0} M, about {sheet.lifeYearsEstimate:0.#} years of life. Fly it!");
            var st = new GUIStyle(Ui.Small) { wordWrap = true };
            float total = lines.Sum(l => st.CalcHeight(new GUIContent(l), wr.width - 24) + 6);
            warnScroll = GUI.BeginScrollView(wr, warnScroll, new Rect(0, 0, wr.width - 18, total));
            float ly = 0;
            foreach (var l in lines) { float h = st.CalcHeight(new GUIContent(l), wr.width - 24); Ui.Text(new Rect(0, ly, wr.width - 22, h), l, st); ly += h + 6; }
            GUI.EndScrollView();
        }

        static string Short(float v) => v >= 10000 ? $"{v / 1000:0.#}k" : v >= 100 ? $"{v:0}" : v >= 1 ? $"{v:0.#}" : $"{v:0.###}";

        void DrawTeamBar(Rect r)
        {
            var room = Room;
            Ui.Box(r); Ui.Box(r);
            // chat
            var chat = room.State.chat;
            float cy = r.y + 6;
            float chatW = Mathf.Min(r.width * 0.36f, 560) - 20;
            var clip = new GUIStyle(Ui.Small) { clipping = TextClipping.Clip, wordWrap = false };
            for (int i = Math.Max(0, chat.Count - 4); i < chat.Count; i++) { Ui.Text(new Rect(r.x + 12, cy, chatW, 20), chat[i], clip); cy += 20; }
            if (!room.Solo)
            {
                chatText = GUI.TextField(new Rect(r.x + 12, r.yMax - 40, chatW - 90, 30), chatText ?? "", 120, Ui.Field);
                bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && !string.IsNullOrWhiteSpace(chatText);
                if (Ui.Press(new Rect(r.x + chatW - 72, r.yMax - 40, 80, 30), "Send", Ui.ButtonSmall) || enter) { room.Chat(chatText); chatText = ""; }
            }
            else Ui.Text(new Rect(r.x + 12, r.yMax - 34, chatW, 22), "Tip: point at any gauge or choice to see why it matters.", Ui.Small);
            // requests: open ones for my roles, with yes/no; ask someone
            float x = r.x + Mathf.Min(r.width * 0.36f, 560);
            float ry = r.y + 6;
            if (!room.Solo)
            {
                foreach (var q in room.State.requests.Where(q => q.answer == 0).Reverse().Take(2))
                {
                    var from = room.State.Player(q.from);
                    Ui.Text(new Rect(x, ry + 3, 330, 20), $"<b>{from?.name ?? "?"}</b> → {MissionDesign.RoleNames[q.toRole].ToLower()}: {q.text}", Ui.Small);
                    if (room.Can((MRole)q.toRole))
                    {
                        if (Ui.Press(new Rect(x + 334, ry, 50, 24), "Yes", Ui.ButtonSmall)) room.Answer(q.id, true);
                        if (Ui.Press(new Rect(x + 388, ry, 50, 24), "No", Ui.ButtonSmall)) room.Answer(q.id, false);
                    }
                    ry += 28;
                }
                ry = Mathf.Max(ry, r.y + 64);
                Ui.Text(new Rect(x, ry + 4, 40, 20), "Ask", Ui.Small);
                for (int k = 0; k < 4; k++)
                    if (Ui.Press(new Rect(x + 34 + k * 46, ry, 42, 26), new[] { "Lead", "Eng", "Sci", "Com" }[k], askRole == k ? Ui.ButtonSmallSel : Ui.ButtonSmall)) askRole = k;
                askText = GUI.TextField(new Rect(x + 222, ry, 170, 26), askText ?? "", 80, Ui.Field);
                if (Ui.Press(new Rect(x + 396, ry, 50, 26), "Ask", Ui.ButtonSmall) && !string.IsNullOrWhiteSpace(askText)) { room.Ask((MRole)askRole, askText); askText = ""; }
                // quick asks
                float qx = x;
                foreach (var quick in new[] { (1, "more power"), (0, "a bigger rocket"), (3, "a faster antenna"), (2, "a lighter instrument") })
                {
                    if (Ui.Press(new Rect(qx, ry + 32, 110, 22), quick.Item2, Ui.ButtonSmall)) room.Ask((MRole)quick.Item1, "Can we have " + quick.Item2 + "?");
                    qx += 114;
                }
                // take a role
                float rx = r.xMax - 440;
                Ui.Text(new Rect(rx, r.y + 6, 200, 20), "Take a job:", Ui.Small);
                for (int k = 0; k < 4; k++)
                {
                    bool mine = room.Can((MRole)k);
                    var br = new Rect(rx + (k % 2) * 104, r.y + 28 + (k / 2) * 30, 100, 26);
                    if (Ui.Press(br, MissionDesign.RoleNames[k].Split(' ')[0] + (k == 1 ? " eng." : ""), mine ? Ui.ButtonSmallSel : Ui.ButtonSmall) && !mine) room.TakeRole((MRole)k);
                    Ui.Line(new Rect(br.x, br.yMax - 3, br.width, 3), RoleColor[k]);
                }
            }
            // ready / fly
            var go = new Rect(r.xMax - 210, r.y + 10, 196, 50);
            var me = room.Me;
            if (room.IsHost)
            {
                int ready = room.State.players.Count(p => p.ready || p.id == room.MyId);
                bool all = room.Solo || ready == room.State.players.Count;
                GUI.enabled = sheet.CanLaunch && all;
                if (Ui.Press(go, "Fly the mission", Ui.Button))
                {
                    if (me != null && !me.ready) room.Ready(true);
                    room.Fly(root.bodies, Easy);
                }
                GUI.enabled = true;
                Ui.Text(new Rect(go.x, go.yMax + 4, go.width, 40), !sheet.CanLaunch ? "<color=#ff9080>Fix the red problems first</color>" : room.Solo ? "Ready when you are" : all ? "Everyone's ready" : $"{ready} of {room.State.players.Count} ready", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperCenter, wordWrap = true });
            }
            else if (me != null)
            {
                if (Ui.Press(go, me.ready ? "Ready ✓ (click to undo)" : "I'm ready", me.ready ? Ui.ButtonSel : Ui.Button)) room.Ready(!me.ready);
                Ui.Text(new Rect(go.x, go.yMax + 4, go.width, 40), "The host flies it when everyone's ready.", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperCenter, wordWrap = true });
            }
            if (Ui.Press(new Rect(r.xMax - 210, r.yMax - 34, 96, 26), "Designs", Ui.ButtonSmall)) page = Page.Designs;
            if (Ui.Press(new Rect(r.xMax - 110, r.yMax - 34, 96, 26), "Scores", Ui.ButtonSmall)) page = Page.Board;
        }

        // ---------------------------------------------------------------- flying it

        void DrawFlying(float W, float H)
        {
            DrawTopBar(W);
            var res = result;
            if (res == null) { Ui.Text(new Rect(W * 0.5f - 200, H * 0.5f, 400, 30), "Flying...", Ui.BigCenter); return; }
            float k = flyStart >= 0 ? Mathf.Clamp01((Time.unscaledTime - flyStart) / FlyLength) : 0;
            float lastDay = res.tDays.Count > 0 ? res.tDays[res.tDays.Count - 1] : 0;
            float day = k * Math.Max(lastDay, 1);
            int idx = 0;
            while (idx < res.tDays.Count - 1 && res.tDays[idx + 1] <= day) idx++;
            var bar = new Rect(W * 0.5f - 360, 72, 720, 64);
            Ui.Box(bar); Ui.Box(bar);
            Ui.Text(new Rect(bar.x + 14, bar.y + 6, 500, 24), !res.launched ? "Not launched" : $"Day {day:0} of {Math.Max(lastDay, 0):0}  ·  {MissionDesign.ScenarioOf(Room.State.plan).name}", Ui.Bold);
            Ui.Bar(new Rect(bar.x + 14, bar.y + 36, bar.width - 28, 10), k, new Color(0.45f, 0.75f, 1f));
            // live dashboard
            var dr = new Rect(W - 330, 150, 320, 230);
            Ui.Box(dr); Ui.Box(dr);
            Ui.Text(new Rect(dr.x + 12, dr.y + 8, 300, 24), "Mission so far", Ui.Header);
            if (res.tDays.Count > 0)
            {
                float y = dr.y + 40;
                void Row(string name, string val) { Ui.Text(new Rect(dr.x + 12, y, 150, 20), name, Ui.Small); Ui.Text(new Rect(dr.x + 150, y, 158, 20), val, new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight }); y += 24; }
                Row("Instruments working", $"{res.duty[idx] * 100:0}%");
                Row("Data waiting", $"{res.stored[idx]:0.#} GB");
                Row("Data home", $"{res.sent[idx]:0} GB");
                Row("Science", $"{res.sci[idx]:0}");
                Row("Coverage", $"{res.cov[idx] * 100:0}%");
            }
            // events so far
            var er = new Rect(10, 150, 520, 300);
            Ui.Box(er); Ui.Box(er);
            Ui.Text(new Rect(er.x + 12, er.y + 8, 400, 24), "Mission log", Ui.Header);
            float ey = er.y + 38;
            foreach (var e in res.events)
            {
                var f = e.Split('|');
                if (f.Length < 2 || !float.TryParse(f[0], out float d) || d > day + 0.5f) continue;
                Ui.Text(new Rect(er.x + 12, ey, er.width - 24, 40), $"<color=#9fc6e8>Day {d:0}</color>  {f[1]}", new GUIStyle(Ui.Small) { wordWrap = true });
                ey += 38;
                if (ey > er.yMax - 30) break;
            }
            if (Room.IsHost && Ui.Press(new Rect(W * 0.5f - 70, bar.yMax + 8, 140, 32), "Skip to the end", Ui.ButtonSmall)) flyStart = Time.unscaledTime - FlyLength;
        }

        // ---------------------------------------------------------------- the report

        void DrawReport(float W, float H)
        {
            DrawTopBar(W);
            var res = result;
            if (res == null) return;
            var r = new Rect(W * 0.5f - 560, 68, 1120, H - 78);
            Ui.Box(r); Ui.Box(r); Ui.Box(r);
            string stars = res.stars > 0 ? new string('★', res.stars) + new string('☆', 3 - res.stars) : "";
            Ui.Text(new Rect(r.x + 20, r.y + 12, 700, 34), res.headline, Ui.Title);
            Ui.Text(new Rect(r.xMax - 340, r.y + 12, 320, 34), $"<color=#ffd56a>{stars}</color>  {res.score} points", new GUIStyle(Ui.Title) { alignment = TextAnchor.UpperRight });
            if (res.teamBonus > 0) Ui.Text(new Rect(r.xMax - 340, r.y + 46, 320, 20), $"(including +{res.teamBonus} for teamwork)", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight });
            // goals
            float y = r.y + 64;
            Ui.Text(new Rect(r.x + 20, y, 400, 22), "Goals", Ui.Bold);
            y += 24;
            foreach (var q in res.reqs)
            {
                Ui.Text(new Rect(r.x + 24, y, 520, 20), $"{(q.ok ? "<color=#7dff8a>✓</color>" : "<color=#ff8070>✗</color>")} {q.name}: {q.got:0.#} {q.unit} (needed {q.need:0.#})", Ui.Small);
                y += 21;
            }
            y += 8;
            var st = new GUIStyle(Ui.Small) { wordWrap = true };
            Ui.Text(new Rect(r.x + 20, y, 520, 22), "What went well", Ui.Bold);
            y += 24;
            foreach (var g in res.good) { float h = st.CalcHeight(new GUIContent(g), 510); Ui.Text(new Rect(r.x + 24, y, 510, h), "<color=#9fe8a8>+</color> " + g, st); y += h + 4; }
            y += 6;
            Ui.Text(new Rect(r.x + 20, y, 520, 22), "What to change next time", Ui.Bold);
            y += 24;
            if (res.bad.Count == 0) { Ui.Text(new Rect(r.x + 24, y, 510, 20), "Nothing much: try for a higher score with less money, or a harder mission.", st); y += 24; }
            foreach (var b in res.bad) { float h = st.CalcHeight(new GUIContent(b), 510); Ui.Text(new Rect(r.x + 24, y, 510, h), "<color=#ffb070>!</color> " + b, st); y += h + 4; }
            // charts
            float cx = r.x + 580, cw = r.width - 600;
            Chart(new Rect(cx, r.y + 64, cw, 120), "Instruments working (%)", res.duty, 1f, new Color(0.45f, 0.9f, 0.5f), 100);
            Chart(new Rect(cx, r.y + 200, cw, 120), "Data home (GB)", res.sent, 0, new Color(0.45f, 0.75f, 1f), 1);
            Chart(new Rect(cx, r.y + 336, cw, 120), "Science", res.sci, 0, new Color(1f, 0.8f, 0.35f), 1);
            // summary and the best before
            float sy = r.y + 470;
            Ui.Text(new Rect(cx, sy, cw, 20), $"{res.massKg:0} kg  ·  {res.costM:0} M" + (res.budgetM > 0 ? $" of {res.budgetM:0} M" : "") + $"  ·  {res.years:0.#} of {res.goalYears:0.#} years  ·  {res.dataGB:0} GB home, {res.lostGB:0} GB lost", Ui.Small);
            var best = MissionScores.Best(res.scenario);
            if (best != null) Ui.Text(new Rect(cx, sy + 22, cw, 20), best.score > res.score ? $"Your best on this mission: {best.score} points ({best.team})." : "A new best score for this mission!", Ui.Small);
            float ry = sy + 52;
            var wst = new GUIStyle(Ui.Small) { wordWrap = true };
            string real = MissionDesign.RealMissions(Room.State.plan);
            if (real.Length > 0)
            {
                Ui.Text(new Rect(cx, ry, cw, 22), "Real missions like this", Ui.Bold);
                ry += 24;
                float rh = wst.CalcHeight(new GUIContent(real), cw);
                Ui.Text(new Rect(cx, ry, cw, rh), $"<color=#c8d6e4>{real}</color>", wst);
                ry += rh + 10;
            }
            if (!Room.Solo && !MissionDesign.ScenarioOf(Room.State.plan).sandbox && ry < r.yMax - 110)
            {
                Ui.Text(new Rect(cx, ry, cw, 22), "Other teams on this mission", Ui.Bold);
                DrawRivals(cx, ry + 24, cw, wst);
            }
            // what next
            var room = Room;
            float by = r.yMax - 52;
            if (room.IsHost)
            {
                if (Ui.Press(new Rect(r.x + 20, by, 230, 40), "Change the design", Ui.Button)) room.SetPhase(0);
                if (Ui.Press(new Rect(r.x + 258, by, 200, 40), "Fly it again", Ui.Button)) room.Fly(root.bodies, Easy);
            }
            else Ui.Text(new Rect(r.x + 20, by + 10, 440, 22), "The host can change the design or fly again.", Ui.Small);
            if (Ui.Press(new Rect(r.xMax - 360, by, 110, 40), "Scores", Ui.Button)) page = Page.Board;
            if (Ui.Press(new Rect(r.xMax - 244, by, 110, 40), "Designs", Ui.Button)) page = Page.Designs;
            if (Ui.Press(new Rect(r.xMax - 128, by, 110, 40), "Leave", Ui.Button)) { room.Stop(); NetRoom.Current = null; page = Page.Hub; result = null; resultSig = null; }
        }

        static void Chart(Rect r, string title, List<float> vals, float maxFixed, Color c, float mult)
        {
            Ui.Line(r, new Color(0, 0, 0, 0.25f));
            Ui.Text(new Rect(r.x + 6, r.y + 2, r.width - 12, 18), title, Ui.Small);
            if (vals == null || vals.Count < 2) return;
            float max = maxFixed > 0 ? maxFixed : Mathf.Max(1e-3f, vals.Max());
            var plot = new Rect(r.x + 6, r.y + 22, r.width - 12, r.height - 28);
            for (int i = 0; i < vals.Count; i++)
            {
                float x = plot.x + plot.width * i / (vals.Count - 1);
                float h = plot.height * Mathf.Clamp01(vals[i] / max);
                Ui.Line(new Rect(x, plot.yMax - h, Mathf.Max(1.5f, plot.width / vals.Count), h), new Color(c.r, c.g, c.b, 0.55f));
            }
            Ui.Text(new Rect(r.xMax - 120, r.y + 2, 114, 18), $"max {max * mult:0.#}", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight });
        }

        /// <summary>Other rooms on this network flying the same mission, with their best scores against ours (milestone 31).</summary>
        float DrawRivals(float x, float y, float w, GUIStyle wst)
        {
            var room = Room;
            room.State.BestFor(room.State.plan.scenario, out int ours, out _);
            var rivals = room.Rivals().ToList();
            if (rivals.Count == 0)
            {
                string none = "None on this network right now. When another room picks this mission, its best score shows here: the higher score wins.";
                float h = wst.CalcHeight(new GUIContent(none), w);
                Ui.Text(new Rect(x, y, w, h), $"<color=#9fb2c4>{none}</color>", wst);
                return y + h + 6;
            }
            foreach (var f in rivals.Take(4))
            {
                string score = f.score > 0 ? $"best <b>{f.score}</b> <color=#ffd56a>{new string('★', f.stars)}</color>" : "no flight yet";
                string vs = f.score <= 0 || ours <= 0 ? "" : ours > f.score ? "  <color=#7dff8a>(you're ahead)</color>" : ours < f.score ? $"  <color=#ffb070>({f.score - ours} to beat them)</color>" : "  (a tie)";
                Ui.Text(new Rect(x, y, w, 20), $"• <b>{f.name}</b> ({f.players} player{(f.players == 1 ? "" : "s")}): {score}{vs}", Ui.Small);
                y += 20;
            }
            Ui.Text(new Rect(x, y, w, 20), ours > 0 ? $"Your team's best: <b>{ours}</b>" : "Your team hasn't flown this one yet.", Ui.Small);
            return y + 24;
        }

        // ---------------------------------------------------------------- scores and designs

        void DrawBoard(float W, float H)
        {
            var r = new Rect(W * 0.5f - 420, 80, 840, H - 140);
            Ui.Box(r); Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 20, r.y + 14, 600, 30), "Leaderboard", Ui.Header);
            Ui.Text(new Rect(r.x + 20, r.y + 44, 600, 20), "The best expeditions flown on this computer, by route.", Ui.Small);
            float y = r.y + 74;
            foreach (var sc in Expedition.Scenarios)
            {
                Ui.Text(new Rect(r.x + 20, y, 760, 22), $"{sc.name}  <size=12><color=#8a929c>{sc.blurb}</color></size>", new GUIStyle(Ui.Bold) { wordWrap = false, clipping = TextClipping.Clip });
                y += 24;
                var top = Expedition.Top(Application.persistentDataPath, sc.id).Take(3).ToList();
                if (top.Count == 0) { Ui.Text(new Rect(r.x + 34, y, 600, 20), "<color=#8a929c>No expeditions yet</color>", Ui.Small); y += 22; }
                int n = 1;
                foreach (var e in top)
                {
                    Ui.Text(new Rect(r.x + 34, y, 760, 20), $"{n++}.  <b>{e.score}</b> points  {new string('★', e.stars)}   ·   {Expedition.DifficultyNames[Mathf.Clamp(e.difficulty, 0, 2)]}   ·   {e.team}   ·   {e.date}", Ui.Small);
                    y += 21;
                }
                y += 6;
            }
            if (Ui.Press(new Rect(r.x + 20, r.yMax - 52, 120, 38), "Back", Ui.Button)) page = Room != null && Room.Connected ? Page.Room : Page.Hub;
        }

        readonly List<string> compare = new List<string>();     // milestone 31: two designs side by side ("" = the one on the table now)
        const string NowKey = "";

        void ToggleCompare(string key)
        {
            if (compare.Remove(key)) return;
            compare.Add(key);
            while (compare.Count > 2) compare.RemoveAt(0);
        }

        MissionPlan ComparePlan(string key)
        {
            if (key == NowKey) return Room != null && Room.Connected ? Room.State.plan : null;
            var d = MissionDesigns.All().FirstOrDefault(x => x.name == key);
            return d == null ? null : JsonUtility.FromJson<MissionPlan>(d.json);
        }

        static GUIStyle clipSmall;
        static GUIStyle ClipSmall => clipSmall ??= new GUIStyle(Ui.Small) { clipping = TextClipping.Clip, wordWrap = false };

        void DrawDesigns(float W, float H)
        {
            float pw = Mathf.Min(1180, W - 40);
            var r = new Rect(W * 0.5f - pw / 2, 70, pw, H - 110);
            Ui.Box(r); Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 20, r.y + 14, 600, 30), "Designs", Ui.Header);
            var room = Room;
            bool inRoom = room != null && room.Connected;
            bool lead = inRoom && room.Can(MRole.Lead) && room.State.phase == 0;
            float lw = pw * 0.5f - 30;
            float y = r.y + 56;
            if (inRoom)
            {
                Ui.Text(new Rect(r.x + 20, y + 6, 120, 22), "Save this one as", Ui.Small);
                designName = GUI.TextField(new Rect(r.x + 140, y, lw - 240, 30), designName ?? "", 32, Ui.Field);
                if (Ui.Press(new Rect(r.x + lw - 90, y, 100, 30), "Save", Ui.ButtonSmall)) { MissionDesigns.Save(string.IsNullOrWhiteSpace(designName) ? MissionDesign.ScenarioOf(room.State.plan).name : designName, room.State.plan); Toast("Saved."); }
                y += 40;
                Ui.Text(new Rect(r.x + 20, y + 6, 120, 22), "Paste a code", Ui.Small);
                pasteCode = GUI.TextField(new Rect(r.x + 140, y, lw - 240, 30), pasteCode ?? "", 4000, Ui.Field);
                GUI.enabled = lead;
                if (Ui.Press(new Rect(r.x + lw - 90, y, 100, 30), "Load it", Ui.ButtonSmall))
                {
                    var p = MissionDesigns.FromShareCode(pasteCode);
                    if (p != null) { room.LoadPlan(p); Toast("Loaded."); page = Page.Room; } else Toast("That code doesn't work.");
                }
                GUI.enabled = true;
                y += 40;
                if (Ui.Press(new Rect(r.x + 140, y, 230, 30), "Copy a share code", Ui.ButtonSmall)) { GUIUtility.systemCopyBuffer = MissionDesigns.ShareCode(room.State.plan); Toast("Share code copied: paste it to a friend."); }
                y += 46;
            }
            Ui.Text(new Rect(r.x + 20, y, lw, 22), "Tick two to compare them", Ui.Bold);
            y += 28;
            float bottom = r.yMax - 100;
            if (inRoom) DesignRow(ref y, r.x + 20, lw, NowKey, "This design (now)", room.State.plan, false, lead);
            var list = MissionDesigns.All();
            if (list.Count == 0) { Ui.Text(new Rect(r.x + 58, y, lw - 40, 20), "Nothing saved on this computer yet.", Ui.Small); y += 24; }
            foreach (var d in list)
            {
                if (y + 40 > bottom) { Ui.Text(new Rect(r.x + 58, y, lw - 40, 20), $"...and {list.Count - list.IndexOf(d)} more", Ui.Small); break; }
                DesignRow(ref y, r.x + 20, lw, d.name, d.name, JsonUtility.FromJson<MissionPlan>(d.json), true, lead);
            }
            if (!lead && inRoom) Ui.Text(new Rect(r.x + 20, r.yMax - 92, lw, 20), "Only the mission lead can load a design, while designing.", Ui.Small);
            DrawCompare(new Rect(r.x + pw * 0.5f, r.y + 56, pw * 0.5f - 20, r.height - 120));
            if (Ui.Press(new Rect(r.x + 20, r.yMax - 52, 120, 38), "Back", Ui.Button)) page = inRoom ? Page.Room : Page.Hub;
        }

        void DesignRow(ref float y, float x, float w, string key, string name, MissionPlan p, bool saved, bool lead)
        {
            if (p == null) return;
            var sh = MissionDesign.Evaluate(p, root.bodies, Easy);
            bool on = compare.Contains(key);
            if (Ui.Press(new Rect(x, y + 4, 30, 30), on ? "✓" : "", on ? Ui.ButtonSmallSel : Ui.ButtonSmall)) ToggleCompare(key);
            float tw = w - 38 - (saved ? 196 : 0);
            Ui.Text(new Rect(x + 38, y + 1, tw, 22), $"<b>{name}</b>  ·  {MissionDesign.ScenarioOf(p).name}", ClipSmall);
            Ui.Text(new Rect(x + 38, y + 19, tw, 22), $"<color=#9fb2c4>{sh.massKg:0} kg, {sh.totalCostM:0} M, {p.instruments.Count} instruments{(sh.CanLaunch ? "" : ", can't launch yet")}</color>", ClipSmall);
            if (saved)
            {
                GUI.enabled = lead;
                if (Ui.Press(new Rect(x + w - 192, y + 5, 92, 28), "Load", Ui.ButtonSmall)) { Room.LoadPlan(p); page = Page.Room; }
                GUI.enabled = true;
                if (Ui.Press(new Rect(x + w - 94, y + 5, 92, 28), "Delete", Ui.ButtonSmall)) { MissionDesigns.Delete(key); compare.Remove(key); }
            }
            y += 42;
        }

        /// <summary>Two designs side by side, the better value of each line in green (a trade study).</summary>
        void DrawCompare(Rect r)
        {
            Ui.Line(r, new Color(0, 0, 0, 0.22f));
            Ui.Text(new Rect(r.x + 14, r.y + 8, r.width - 28, 24), "Compare two designs", Ui.Bold);
            var wrap = new GUIStyle(Ui.Small) { wordWrap = true };
            var picked = compare.Select(k => (key: k, plan: ComparePlan(k))).Where(t => t.plan != null).ToList();
            if (picked.Count < 2)
            {
                Ui.Text(new Rect(r.x + 14, r.y + 38, r.width - 28, 140), "Tick two designs on the left (this design and a saved one, or two saved ones) to see them side by side.\n\nMission teams call this a trade study: there's rarely one perfect design, just different trade-offs between mass, power, money, data and time.", wrap);
                return;
            }
            var a = MissionDesign.Evaluate(picked[0].plan, root.bodies, Easy);
            var b = MissionDesign.Evaluate(picked[1].plan, root.bodies, Easy);
            string na = picked[0].key == NowKey ? "This design" : picked[0].key, nb = picked[1].key == NowKey ? "This design" : picked[1].key;
            var rows = MissionDesign.Compare(a, b);
            float c1 = r.x + 14, c2 = r.x + r.width * 0.33f, c3 = r.x + r.width * 0.665f, cw = r.width * 0.32f;
            float y = r.y + 40;
            Ui.Text(new Rect(c2, y, cw, 22), $"<b>{na}</b>", ClipSmall);
            Ui.Text(new Rect(c3, y, cw, 22), $"<b>{nb}</b>", ClipSmall);
            y += 26;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (i % 2 == 0) Ui.Line(new Rect(r.x + 6, y - 2, r.width - 12, 22), new Color(1, 1, 1, 0.04f));
                Ui.Text(new Rect(c1, y, c2 - c1 - 6, 22), row.name, ClipSmall);
                Ui.Text(new Rect(c2, y, cw, 22), row.winner == 1 ? $"<color=#7dff8a>{row.a}</color>" : row.a, ClipSmall);
                Ui.Text(new Rect(c3, y, cw, 22), row.winner == 2 ? $"<color=#7dff8a>{row.b}</color>" : row.b, ClipSmall);
                y += 22;
            }
            y += 10;
            string verdict = MissionDesign.CompareVerdict(rows, a, b, na, nb);
            Ui.Text(new Rect(c1, y, r.width - 28, wrap.CalcHeight(new GUIContent(verdict), r.width - 28)), verdict, wrap);
        }


        // ---------------------------------------------------------------- for the screenshot tour

        public void TourSolo(string name, MissionPlan plan) { playerName = name; NetRoom.StartSolo(name, plan); page = Page.Room; }
        public void TourHost(string name, string room, MissionPlan plan) { playerName = name; var r = NetRoom.Host(name, room, plan); page = r.Error == null ? Page.Room : Page.Hub; }
        public void TourJoin(string name, string code) { playerName = name; NetRoom.Current = new NetRoom(); DoJoin(code); page = Page.Join; }
        public void TourMenu(string p, bool campaign, int kind)
        {
            hubCampaign = campaign; hubKind = kind;
            if (p == "join") { Room?.Stop(); NetRoom.Current = new NetRoom { IsHost = false }; }
            page = p == "make" ? Page.Make : p == "join" ? Page.Join : Page.Hub;
        }
        public void TourHostCampaign(string name, string room)
        {
            playerName = name;
            Room?.Stop();
            var r = NetRoom.Host(name, room, new MissionPlan { scenario = MissionDesign.CampaignOrder[0].id }, 0, true);
            r.State.unlocked = MissionDesign.UnlockedFrom(id => MissionScores.Best(id)?.stars ?? 0);
            page = r.Error == null ? Page.Room : Page.Hub; tab = 0;
        }
        public void TourTab(int t) { tab = Mathf.Clamp(t, 0, 4); }
        public void TourScroll(float y) { leftScroll.y = y; }
        public void TourCompare(string a, string b) { compare.Clear(); compare.Add(a); compare.Add(b); page = Page.Designs; }
        public void TourPage(string p) { page = p == "board" ? Page.Board : p == "designs" ? Page.Designs : Page.Room; }
        public void TourSkip() { flyStart = Time.unscaledTime - FlyLength; }
    }

    // ---------------------------------------------------------------- the leaderboard and saved designs

    [Serializable] public class MScore { public string scenario, scenarioName, team, date; public int score, stars; }
    [Serializable] class MScoreList { public List<MScore> list = new List<MScore>(); }
    [Serializable] public class MSaved { public string name, json; }
    [Serializable] class MSavedList { public List<MSaved> list = new List<MSaved>(); }

    /// <summary>The best missions flown on this computer (a small file next to the saves).</summary>
    public static class MissionScores
    {
        static string PathOf => Path.Combine(Application.persistentDataPath, Universe.IsAutotest ? "autotest_mission_scores.json" : "mission_scores.json");
        static MScoreList cache;
        static string lastAdded;

        static MScoreList Load()
        {
            if (cache != null) return cache;
            try { cache = File.Exists(PathOf) ? JsonUtility.FromJson<MScoreList>(File.ReadAllText(PathOf)) : new MScoreList(); } catch { cache = new MScoreList(); }
            return cache ??= new MScoreList();
        }

        public static void Add(MissionResult r, string scenarioName)
        {
            if (r == null || !r.launched) return;
            var l = Load();
            l.list.Add(new MScore { scenario = r.scenario, scenarioName = scenarioName, team = r.team ?? "", score = r.score, stars = r.stars, date = DateTime.Now.ToString("MMM d, HH:mm") });
            l.list = l.list.OrderByDescending(x => x.score).Take(200).ToList();
            lastAdded = $"{r.scenario}|{r.seed}|{r.score}";
            try { File.WriteAllText(PathOf, JsonUtility.ToJson(l)); } catch { }
        }

        /// <summary>Clients add a result once, when it arrives.</summary>
        public static void AddOnce(MissionResult r, string scenarioName)
        {
            if (r == null || lastAdded == $"{r.scenario}|{r.seed}|{r.score}") return;
            Add(r, scenarioName);
        }

        public static List<MScore> Top(string scenario, int n) => Load().list.Where(x => x.scenario == scenario).OrderByDescending(x => x.score).Take(n).ToList();
        public static MScore Best(string scenario) => Top(scenario, 1).FirstOrDefault();
    }

    /// <summary>Mission designs kept on this computer, and share codes (a design packed into text) to send to friends.</summary>
    public static class MissionDesigns
    {
        static string PathOf => Path.Combine(Application.persistentDataPath, "mission_designs.json");

        static MSavedList Load()
        {
            try { return File.Exists(PathOf) ? JsonUtility.FromJson<MSavedList>(File.ReadAllText(PathOf)) ?? new MSavedList() : new MSavedList(); } catch { return new MSavedList(); }
        }

        public static List<MSaved> All() => Load().list;

        public static void Save(string name, MissionPlan plan)
        {
            var l = Load();
            l.list.RemoveAll(x => x.name == name);
            l.list.Insert(0, new MSaved { name = name, json = JsonUtility.ToJson(plan) });
            try { File.WriteAllText(PathOf, JsonUtility.ToJson(l)); } catch { }
        }

        public static void Delete(string name)
        {
            var l = Load();
            l.list.RemoveAll(x => x.name == name);
            try { File.WriteAllText(PathOf, JsonUtility.ToJson(l)); } catch { }
        }

        public static string ShareCode(MissionPlan plan)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(plan));
            using var ms = new MemoryStream();
            using (var gz = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) gz.Write(bytes, 0, bytes.Length);
            return "AMD1:" + Convert.ToBase64String(ms.ToArray());
        }

        public static MissionPlan FromShareCode(string code)
        {
            try
            {
                code = (code ?? "").Trim();
                if (!code.StartsWith("AMD1:")) return null;
                var data = Convert.FromBase64String(code.Substring(5));
                using var ms = new MemoryStream(data);
                using var gz = new DeflateStream(ms, CompressionMode.Decompress);
                using var rd = new StreamReader(gz, Encoding.UTF8);
                var p = JsonUtility.FromJson<MissionPlan>(rd.ReadToEnd());
                return p != null && p.instruments != null ? p : null;
            }
            catch { return null; }
        }
    }
}
