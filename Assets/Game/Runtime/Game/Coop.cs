using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public enum CAct { Build, Steer, Stage, Systems, Navigate, Science }

    public class CoopGoal { public string id, name; }

    /// <summary>The flight as the host sends it to the other players, twenty times a second.</summary>
    [Serializable]
    public class FlightFrame
    {
        public double ut, px, py, pz, vx, vy, vz, nodeUt;
        public float qx, qy, qz, qw, wx, wy, wz, throttle, nodePro, nodeNor, nodeRad;
        public int body, sas, vessel, snap, warp;
        public bool rcs, landed, destroyed, hasNode, rails;
        public string target;
    }

    /// <summary>
    /// Milestone 30 (captaincarl24: "the whole game is supposed to be based off this ... all 4 people have different roles, one
    /// builds the rocket while the other 3 comment on it and suggest changes, one drives the rocket, one does stuff"):
    /// a rocket room. Up to four players, each on their own device, build and fly one rocket together.
    ///
    /// Building: the builder builds it in the hangar while everyone else watches the same rocket appear, clicks parts to
    /// comment on them and suggests changes (the builder ticks them off); mission control picks the goal and where to
    /// launch from; everyone says ready and the builder launches.
    /// Flying: the host's game flies the rocket and sends it to everyone many times a second. The pilot steers and works
    /// the throttle; the flight engineer stages and runs the systems (gear, lights, chutes, RCS, abort); mission control
    /// plans the route (maneuver nodes, targets, time warp) and watches the goal; the builder runs the science and the
    /// radio. Each player's game only lets them do their own job, and passes it to the host.
    /// </summary>
    public class Coop
    {
        public const int Builder = 0, Pilot = 1, Engineer = 2, Control = 3;
        public static readonly string[] RoleNames = { "Builder", "Pilot", "Flight engineer", "Mission control" };
        public static readonly string[] BuildJobs =
        {
            "you build the rocket; the team's comments appear on the right",
            "look the rocket over: click a part to comment, or suggest a change",
            "check the stages, fuel and speed budget: click a part to comment",
            "pick the goal and where to launch from, and suggest changes",
        };
        public static readonly string[] FlyJobs =
        {
            "science and the radio: run experiments and send results home (Science button)",
            "fly it: W/S A/D Q/E steer, SHIFT/CTRL throttle, Z full, X cut, T SAS",
            "staging and systems: SPACE stage, BACKSPACE abort, G gear, U lights, B brakes, Y chutes, R RCS",
            "the route: map (M), maneuver nodes, targets, time warp (. and ,), the goal",
        };
        public static readonly CoopGoal[] Goals =
        {
            new CoopGoal { id = "hop", name = "Fly higher than 10 km" },
            new CoopGoal { id = "space", name = "Reach space (above 70 km)" },
            new CoopGoal { id = "orbit", name = "Get into orbit" },
            new CoopGoal { id = "tal", name = "Fly to Tal" },
            new CoopGoal { id = "talland", name = "Land on Tal" },
            new CoopGoal { id = "talback", name = "Land on Tal and come home" },
            new CoopGoal { id = "free", name = "Free flight (no goal)" },
        };
        public static readonly string[] QuickNotes = { "Add more fuel", "It needs a parachute", "Add fins at the bottom", "Too heavy: lose a stage", "Add landing legs", "Add an antenna", "Looks great!" };

        public static Coop Current;
        public readonly NetRoom room;
        readonly GameRoot root;
        public static bool Active => Current != null && Current.room != null && Current.room.Connected && Current.room.State.activity == 1;
        public bool IsHost => room.IsHost;
        /// <summary>A campaign room: the host's saved space program (money, research, contracts), shared with everyone.</summary>
        public bool Campaign => room.State.campaign;
        public int Phase => room.State.coopPhase;

        public Coop(GameRoot root, NetRoom room)
        {
            this.root = root;
            this.room = room;
            room.OnHostCommand = (who, m) => commands.Enqueue((who, m));
            room.OnClientMessage = OnStream;
        }

        public bool Has(int role) => room.Me != null && room.Me.Has((MRole)role);
        public static int RoleFor(CAct a) => a == CAct.Build || a == CAct.Science ? Builder : a == CAct.Steer ? Pilot : a == CAct.Navigate ? Control : Engineer;
        public bool CanDo(CAct a) => Has(RoleFor(a));
        public bool SenderCan(int who, CAct a) { var p = room.State.Player(who); return p != null && p.Has((MRole)RoleFor(a)); }
        public string Holder(int role) => string.Join(", ", room.State.players.Where(p => p.Has((MRole)role)).Select(p => p.name).DefaultIfEmpty("nobody"));
        public string MyJobs(bool flying) => string.Join("; ", Enumerable.Range(0, 4).Where(Has).Select(r => $"{RoleNames[r]}: {(flying ? FlyJobs[r] : BuildJobs[r])}"));
        public static CoopGoal Goal(string id) => Goals.FirstOrDefault(g => g.id == id) ?? Goals[1];

        // ---------------------------------------------------------------- the host's side

        /// <summary>Commands from the other players, for the host's flight to carry out (the host checks each sender's job).</summary>
        public readonly Queue<(int who, NetMsg m)> commands = new Queue<(int, NetMsg)>();
        public float inPitch, inYaw, inRoll, inThrottle = -1f, inDrive, inTime = -10f;
        public Vector3 inTranslate;
        public double maxAltitude;

        /// <summary>The goal reached? (checked by the host during the flight, once a second)</summary>
        public static bool GoalMet(string goal, VesselSim v, double maxAlt, double ut)
        {
            if (v == null) return false;
            int f = v.careerFlags;
            switch (goal)
            {
                case "hop": return maxAlt > 10_000;
                case "space": return maxAlt > 70_000 || (f & VFlags.Space) != 0;
                case "orbit": return (f & VFlags.OrbitHome) != 0 || (!v.landed && v.body.IsHomeWorld && v.Situation(ut).StartsWith("Orbiting"));
                case "tal": return (f & VFlags.SoiTal) != 0;
                case "talland": return (f & VFlags.LandedTal) != 0;
                case "talback": return (f & VFlags.LandedTal) != 0 && v.landed && v.body.IsHomeWorld;
                default: return false;
            }
        }

        // ---------------------------------------------------------------- a player's side: the flight coming in

        public readonly List<(float at, FlightFrame f)> frames = new List<(float, FlightFrame)>();
        public string snapJson;
        public int snapId = -1, snapUsed = -1;
        public readonly Queue<string> sayings = new Queue<string>();
        public bool boom;

        void OnStream(NetMsg m)
        {
            switch (m.t)
            {
                case "fs":
                    try
                    {
                        var f = JsonUtility.FromJson<FlightFrame>(m.json);
                        frames.Add((Time.unscaledTime, f));
                        while (frames.Count > 40) frames.RemoveAt(0);
                    }
                    catch { }
                    break;
                case "snap": snapJson = m.json; snapId = m.n; break;
                case "say": if (!string.IsNullOrEmpty(m.a)) { sayings.Enqueue(m.a); while (sayings.Count > 6) sayings.Dequeue(); } break;
                case "boom": boom = true; break;
                case "career": careerJson = m.json; careerNew = true; break;
            }
        }

        // ---------------------------------------------------------------- a campaign: the host's career on every screen

        public string careerJson;
        public bool careerNew;
        string lastCareerSent;
        string lastCareerPlayers;
        float careerTimer;

        /// <summary>A career as text, to send.</summary>
        public static string CareerJson(Career c) => JsonUtility.ToJson(c.ToSave());

        /// <summary>The host's career arriving on a player's device (the first-flight guide stays closed: it's for playing alone).</summary>
        public static bool ApplyCareer(Career c, string json)
        {
            if (c == null || string.IsNullOrEmpty(json)) return false;
            CareerSave s;
            try { s = JsonUtility.FromJson<CareerSave>(json); } catch { return false; }
            if (s == null || s.version == 0) return false;
            c.Load(s);
            c.guide = Guide.Closed;
            return true;
        }

        /// <summary>The host sends its career when it changes (and when someone joins); players take it in.</summary>
        void ShareCareer()
        {
            if (!Campaign || root == null) return;
            if (IsHost)
            {
                careerTimer -= Time.unscaledDeltaTime;
                if (careerTimer > 0) return;
                careerTimer = 1f;
                string j = CareerJson(root.universe.career);
                string who = string.Join(",", room.State.players.Select(p => p.id));   // someone new gets it too
                if (j == lastCareerSent && who == lastCareerPlayers) return;
                lastCareerSent = j;
                lastCareerPlayers = who;
                room.BroadcastRaw(new NetMsg { t = "career", json = j });
            }
            else if (careerNew)
            {
                careerNew = false;
                ApplyCareer(root.universe.career, careerJson);
            }
        }

        /// <summary>The flight as it was a tenth of a second ago, smoothly between the frames that came in (so a late frame doesn't jolt it).</summary>
        public bool Sample(out FlightFrame f, out Vector3d pos, out Vector3d vel, out Quaternion rot, out double ut)
        {
            f = null; pos = vel = Vector3d.zero; rot = Quaternion.identity; ut = 0;
            if (frames.Count == 0) return false;
            float at = Time.unscaledTime - 0.1f;
            int i = frames.Count - 1;
            while (i > 0 && frames[i].at > at) i--;
            var a = frames[i];
            var b = i + 1 < frames.Count ? frames[i + 1] : a;
            float k = b.at > a.at ? Mathf.Clamp01((at - a.at) / (b.at - a.at)) : 1f;
            if (a.f.vessel != b.f.vessel || a.f.body != b.f.body) { a = b; k = 1; }
            f = b.f;
            pos = Vector3d.Lerp(new Vector3d(a.f.px, a.f.py, a.f.pz), new Vector3d(b.f.px, b.f.py, b.f.pz), k);
            vel = Vector3d.Lerp(new Vector3d(a.f.vx, a.f.vy, a.f.vz), new Vector3d(b.f.vx, b.f.vy, b.f.vz), k);
            rot = Quaternion.Slerp(new Quaternion(a.f.qx, a.f.qy, a.f.qz, a.f.qw), new Quaternion(b.f.qx, b.f.qy, b.f.qz, b.f.qw), k);
            ut = a.f.ut + (b.f.ut - a.f.ut) * k;
            return true;
        }

        // ---------------------------------------------------------------- every frame (both sides)

        float designTimer;
        string lastSentDesign, lastAppliedDesign;
        int lastPhase = -1;

        public void Update()
        {
            room.Update(Time.unscaledDeltaTime);
            if (!room.Connected) { if (room.Error != null) { var e = room.Error; root.LeaveCoop(); root.menu.ShowPage("main"); Debug.Log("Room closed: " + e); } return; }
            if (room.State.activity != 1) return;
            ShareCareer();
            var hangar = root.hangar;
            // the host: a launch asked for while building
            if (IsHost && !root.inFlight)
            {
                while (commands.Count > 0)
                {
                    var (who, m) = commands.Dequeue();
                    if (m.t != "launch" || Phase != 0) continue;
                    VesselDesign d = null;
                    try { d = JsonUtility.FromJson<VesselDesign>(room.State.designJson); d.Upgrade(); } catch { }
                    if (d == null || d.nodes.Count == 0) continue;
                    var st = room.State;
                    string problem = Campaign ? root.universe.career.LaunchProblem(d, st.runway) : null;
                    if (problem != null) { room.Chat($"Can't launch yet: {problem}"); continue; }
                    st.coopPhase = 1; st.flights++; st.goalDone = false; st.summary = null;
                    maxAltitude = 0;
                    room.Touch();
                    root.Launch(d, st.runway, Mathf.Clamp(st.pad, 0, LaunchSite.Pads.Length - 1));
                    root.flight.Notice($"Flying together! {MyJobs(true)}");
                    break;
                }
            }
            bool building = !root.inFlight && !root.inMenu && !root.inMission;
            if (building)
            {
                bool builder = Has(Builder);
                if (hangar.spectate == builder) hangar.spectate = !builder;
                if (builder)
                {
                    designTimer -= Time.unscaledDeltaTime;
                    if (designTimer <= 0 && hangar.design != null)
                    {
                        designTimer = 0.3f;
                        string j = JsonUtility.ToJson(hangar.design);
                        if (j != lastSentDesign) { lastSentDesign = j; lastAppliedDesign = j; room.Send(new NetMsg { t = "design", json = j }); }
                    }
                }
                else if (!string.IsNullOrEmpty(room.State.designJson) && room.State.designJson != lastAppliedDesign)
                {
                    lastAppliedDesign = room.State.designJson;
                    try { var d = JsonUtility.FromJson<VesselDesign>(lastAppliedDesign); d.Upgrade(); hangar.SetDesignFromNet(d); } catch { }
                }
            }
            // players follow the host between building and flying
            if (!IsHost)
            {
                if (Phase == 1 && !root.inFlight && snapJson != null)
                {
                    try { root.BeginRemoteFlight(JsonUtility.FromJson<VesselSave>(snapJson)); snapUsed = snapId; } catch (Exception ex) { Debug.LogWarning("Couldn't join the flight: " + ex.Message); }
                }
                else if (Phase == 0 && root.inFlight) { frames.Clear(); snapJson = null; UseRoomDesign(); root.EnterHangar(); }
            }
            if (Phase != lastPhase) { lastPhase = Phase; if (Phase == 0) { lastAppliedDesign = null; } }
        }

        /// <summary>Back to building: start from the team's rocket (not whatever this device last had).</summary>
        public void UseRoomDesign()
        {
            if (string.IsNullOrEmpty(room.State.designJson)) return;
            try { var d = JsonUtility.FromJson<VesselDesign>(room.State.designJson); d.Upgrade(); root.currentDesign = d; } catch { }
        }

        // ---------------------------------------------------------------- building: launching

        /// <summary>The builder's LAUNCH: the host launches; a player builder asks the host to.</summary>
        public void Launch(VesselDesign d, bool runway, int pad)
        {
            if (!Has(Builder)) return;
            room.Send(new NetMsg { t = "design", json = JsonUtility.ToJson(d) });
            room.Send(new NetMsg { t = "launch", on = runway, n = pad });
        }

        public bool AllReady => room.State.players.Where(p => !p.Has((MRole)Builder)).All(p => p.ready);
        public string NotReady => string.Join(", ", room.State.players.Where(p => !p.Has((MRole)Builder) && !p.ready).Select(p => p.name));

        // ---------------------------------------------------------------- the panels

        static readonly Color[] RoleColor = { new Color(0.95f, 0.6f, 0.25f), new Color(0.4f, 0.65f, 1f), new Color(0.5f, 0.9f, 0.5f), new Color(0.85f, 0.55f, 0.95f) };
        string noteText = "", chatText = "";
        public int notePart = -1;
        Vector2 noteScroll;

        /// <summary>The team, each player's jobs and whether they're ready (top right in the builder and in flight).</summary>
        public void DrawTeam(Rect r, bool flying)
        {
            Ui.Box(r); Ui.Box(r);
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 22), $"<b>{room.State.room}</b>  ·  code <color=#ffd56a>{room.State.code}</color>", Ui.Small);
            float y = r.y + 28;
            foreach (var p in room.State.players)
            {
                string jobs = string.Join(", ", Enumerable.Range(0, 4).Where(k => p.Has((MRole)k)).Select(k => RoleNames[k]));
                GUI.Label(new Rect(r.x + 10, y, r.width - 20, 20), $"{(p.ready && !flying ? "<color=#7dff8a>✓</color> " : "")}<b>{p.name}</b>{(p.id == room.MyId ? " (you)" : "")}: {jobs}", Ui.Small);
                for (int k = 0; k < 4; k++) if (p.Has((MRole)k)) Ui.Line(new Rect(r.x + 4, y + 4, 3, 12), RoleColor[k]);
                y += 20;
            }
            GUI.Label(new Rect(r.x + 10, y + 2, r.width - 20, 44), $"<color=#9fc6e8>Your job:</color> {MyJobs(flying)}", new GUIStyle(Ui.Small) { wordWrap = true });
            if (!flying && room.State.players.Count > 1)
            {
                // swap jobs while building
                float bw = (r.width - 20) / 4f;
                for (int k = 0; k < 4; k++)
                {
                    bool mine = Has(k);
                    var br = new Rect(r.x + 10 + k * bw, r.yMax - 26, bw - 4, 22);
                    if (GUI.Button(br, RoleNames[k].Split(' ')[0], mine ? Ui.RowSel : Ui.ButtonSmall) && !mine) room.TakeRole((MRole)k);
                    Ui.Line(new Rect(br.x, br.yMax - 2, br.width, 2), RoleColor[k]);
                }
            }
        }

        /// <summary>Building: comments on the rocket, suggestions, the goal and launch site, ready. Builder ticks comments off.</summary>
        public void DrawBuildPanel(Rect r, VesselDesign d)
        {
            Ui.Box(r); Ui.Box(r);
            bool builder = Has(Builder);
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 24), builder ? "The team's comments" : "Comment on the rocket", Ui.Header);
            float y = r.y + 34;
            // the goal and where from (mission control picks)
            var goal = Goal(room.State.goal);
            bool control = Has(Control);
            GUI.Label(new Rect(r.x + 10, y + 4, 60, 20), "Goal", Ui.Small);
            GUI.enabled = control;
            if (GUI.Button(new Rect(r.x + 60, y, r.width - 70, 26), goal.name + (control ? "  ▸" : ""), Ui.ButtonSmall))
            {
                int gi = Array.FindIndex(Goals, g => g.id == goal.id);
                room.Send(new NetMsg { t = "goal", b = Goals[(gi + 1) % Goals.Length].id });
            }
            y += 30;
            GUI.Label(new Rect(r.x + 10, y + 4, 60, 20), "From", Ui.Small);
            string from = room.State.runway ? "Runway" : LaunchSite.Pads[Mathf.Clamp(room.State.pad, 0, LaunchSite.Pads.Length - 1)].name;
            if (GUI.Button(new Rect(r.x + 60, y, r.width - 70, 26), from + (control ? "  ▸" : ""), Ui.ButtonSmall))
            {
                int next = room.State.runway ? 0 : room.State.pad + 1;
                room.Send(new NetMsg { t = "pad", n = next >= LaunchSite.Pads.Length ? -1 : next });
            }
            GUI.enabled = true;
            if (!control) GUI.Label(new Rect(r.x + 10, y + 28, r.width - 20, 18), $"<color=#8a929c>(mission control's choice: {Holder(Control)})</color>", Ui.Small);
            y += 50;
            // the comments
            var notes = room.State.notes;
            var listRect = new Rect(r.x + 6, y, r.width - 12, r.yMax - y - (builder ? 46 : 150));
            var st = new GUIStyle(Ui.Small) { wordWrap = true };
            float total = 0;
            foreach (var n in notes) total += st.CalcHeight(new GUIContent(NoteLine(n, d)), listRect.width - 90) + 8;
            noteScroll = GUI.BeginScrollView(listRect, noteScroll, new Rect(0, 0, listRect.width - 18, Mathf.Max(total, 10)));
            float ny = 0;
            if (notes.Count == 0) GUI.Label(new Rect(4, 0, listRect.width - 24, 40), builder ? "No comments yet: the others can click a part of your rocket to comment on it." : "No comments yet. Click a part of the rocket, then write what you think.", st);
            foreach (var n in notes.AsEnumerable().Reverse())
            {
                string line = NoteLine(n, d);
                float h = st.CalcHeight(new GUIContent(line), listRect.width - 90);
                GUI.Label(new Rect(4, ny, listRect.width - 94, h), line, st);
                if (builder && n.state == 0)
                {
                    if (GUI.Button(new Rect(listRect.width - 86, ny, 34, 22), "✓", Ui.ButtonSmall)) room.Send(new NetMsg { t = "noteState", n = n.id, on = true });
                    if (GUI.Button(new Rect(listRect.width - 48, ny, 30, 22), "✗", Ui.ButtonSmall)) room.Send(new NetMsg { t = "noteState", n = n.id, on = false });
                }
                ny += h + 8;
            }
            GUI.EndScrollView();
            if (!builder)
            {
                float by = r.yMax - 146;
                string onWhat = notePart >= 0 && d != null && notePart < d.nodes.Count ? $"On the <b>{PartCatalog.Get(d.nodes[notePart].id)?.name ?? "part"}</b> (click empty space for the whole rocket):" : "On the whole rocket (or click a part first):";
                GUI.Label(new Rect(r.x + 10, by, r.width - 20, 20), onWhat, Ui.Small);
                noteText = GUI.TextField(new Rect(r.x + 10, by + 22, r.width - 90, 28), noteText ?? "", 140, Ui.Field);
                bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && !string.IsNullOrWhiteSpace(noteText);
                if ((GUI.Button(new Rect(r.xMax - 74, by + 22, 64, 28), "Send", Ui.ButtonSmall) || enter) && !string.IsNullOrWhiteSpace(noteText)) { room.Send(new NetMsg { t = "note", n = notePart, a = noteText }); noteText = ""; }
                float qx = r.x + 10, qy = by + 56;
                foreach (var q in QuickNotes)
                {
                    float w = Ui.Small.CalcSize(new GUIContent(q)).x + 16;
                    if (qx + w > r.xMax - 10) { qx = r.x + 10; qy += 26; }
                    if (GUI.Button(new Rect(qx, qy, w, 22), q, Ui.ButtonSmall)) room.Send(new NetMsg { t = "note", n = notePart, a = q });
                    qx += w + 4;
                }
                var me = room.Me;
                if (me != null && GUI.Button(new Rect(r.x + 10, r.yMax - 38, r.width - 20, 30), me.ready ? "Ready to launch ✓ (click to undo)" : "Ready to launch", me.ready ? Ui.RowSel : Ui.Button)) room.Ready(!me.ready);
            }
            else GUI.Label(new Rect(r.x + 10, r.yMax - 40, r.width - 20, 36), AllReady ? "<color=#7dff8a>Everyone's ready: LAUNCH when you are.</color>" : $"Waiting for: {NotReady}", new GUIStyle(Ui.Small) { wordWrap = true });
        }

        string NoteLine(CoopNote n, VesselDesign d)
        {
            var p = room.State.Player(n.from);
            string part = n.part >= 0 && d != null && n.part < d.nodes.Count ? $" <color=#9fc6e8>[{PartCatalog.Get(d.nodes[n.part].id)?.name ?? "part"}]</color>" : "";
            string mark = n.state == 1 ? "<color=#7dff8a>✓ done</color> " : n.state == 2 ? "<color=#8a929c>✗ </color>" : "";
            return $"{mark}<b>{p?.name ?? "?"}</b>{part}: {n.text}";
        }

        /// <summary>Chat (bottom left), in the builder and in flight.</summary>
        public void DrawChat(Rect r)
        {
            Ui.Box(r);
            var chat = room.State.chat;
            float y = r.y + 4;
            var clip = new GUIStyle(Ui.Small) { clipping = TextClipping.Clip, wordWrap = false };
            for (int i = Math.Max(0, chat.Count - 3); i < chat.Count; i++) { GUI.Label(new Rect(r.x + 8, y, r.width - 16, 18), chat[i], clip); y += 18; }
            chatText = GUI.TextField(new Rect(r.x + 8, r.yMax - 30, r.width - 80, 24), chatText ?? "", 120, Ui.Field);
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && !string.IsNullOrWhiteSpace(chatText);
            if ((GUI.Button(new Rect(r.xMax - 66, r.yMax - 30, 58, 24), "Send", Ui.ButtonSmall) || enter) && !string.IsNullOrWhiteSpace(chatText)) { room.Chat(chatText); chatText = ""; }
        }
    }
}
