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
        public float roll;    // milestone 41: the host's roll-out to the pad, its time + 1 (0: not rolling out)
        public int rollPad;
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
    public partial class Coop
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
        public static bool Active => Current != null && Current.room != null && Current.room.Connected && Current.room.State.Rocket;
        /// <summary>Milestone 34: an expedition (the builder builds, the players are the crew, a mission with a budget and a report).</summary>
        public bool Expedition => room.State.activity == 2;
        /// <summary>Whether this player builds: whoever holds the Builder job, in a rocket room and an expedition alike
        /// (milestone 41: everyone used to build in an expedition, which made the jobs meaningless). Nobody holding it (they
        /// just left): anyone can, until the job is handed on.</summary>
        public bool CanBuild => Has(Builder) || NobodyBuilds;
        public bool NobodyBuilds => room.State.players.Count > 0 && !room.State.players.Any(p => p.Has((MRole)Builder));
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
            room.OnPose = (who, a, on) => poses[who] = (a, on, Time.unscaledTime);
            room.OnCabin = CabinEvent;   // milestone 35
        }

        /// <summary>Milestone 34: where each player is in a cabin ("part|x|z|yaw"), whether they're inside, and when it was heard.</summary>
        public readonly Dictionary<int, (string a, bool on, float t)> poses = new Dictionary<int, (string, bool, float)>();
        // milestone 35: the cabins' shared things (blinds, lockers, lights, the plant...): what just happened, and how each was left
        public readonly Queue<(int who, string a)> cabinEvents = new Queue<(int, string)>();
        public readonly Dictionary<string, float> cabinState = new Dictionary<string, float>();
        public void CabinEvent(int who, string a)
        {
            if (string.IsNullOrEmpty(a)) return;
            cabinEvents.Enqueue((who, a));
            while (cabinEvents.Count > 40) cabinEvents.Dequeue();
            var f = a.Split('|');
            if (f.Length == 3 && float.TryParse(f[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) cabinState[f[0] + "|" + f[1]] = v;
        }

        public bool Has(int role) => room.Me != null && room.Me.Has((MRole)role);
        public static int RoleFor(CAct a) => a == CAct.Build || a == CAct.Science ? Builder : a == CAct.Steer ? Pilot : a == CAct.Navigate ? Control : Engineer;
        public bool CanDo(CAct a) => Has(RoleFor(a)) || (a == CAct.Build && NobodyBuilds);
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
                case "ipr": if (m.n != room.MyId) poses[m.n] = (m.a, m.on, Time.unscaledTime); break;
                case "csr": if (m.n != room.MyId) CabinEvent(m.n, m.a); break;
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
            if (!room.Connected)
            {
                if (room.Error != null)
                {
                    // back to the multiplayer screen, saying why (milestone 32: it used to drop you on the title screen without a word)
                    var e = room.Error;
                    root.LeaveCoop();
                    root.ShowMission();
                    root.mission.Notice(e);
                    Debug.Log("Room closed: " + e);
                }
                return;
            }
            if (!room.State.Rocket) return;
            ShareCareer();
            // milestone 34: everyone sees the host's weather; an expedition report arriving goes on this computer's board too
            if (IsHost) { if (room.State.weatherSeed != Weather.GameSeed) { room.State.weatherSeed = Weather.GameSeed; room.Touch(); } }
            else if (room.State.weatherSeed != 0) Weather.GameSeed = room.State.weatherSeed;
            if (Expedition && room.State.exRuns != seenReports) { if (seenReports >= 0 && !string.IsNullOrEmpty(room.State.exReport)) { try { var rep = JsonUtility.FromJson<ExReport>(room.State.exReport); Rocket.Expedition.AddToBoard(Application.persistentDataPath, rep); showReport = true; } catch { } } seenReports = room.State.exRuns; }
            var hangar = root.hangar;
            // the host: a launch asked for while building
            if (IsHost && !root.inFlight)
            {
                while (commands.Count > 0)
                {
                    var (who, m) = commands.Dequeue();
                    if (m.t != "launch" || Phase != 0) continue;
                    VesselDesign d = null;
                    try { d = JsonUtility.FromJson<VesselDesign>(room.State.designJson); d.Upgrade(); Decals.Learn(d); } catch { }
                    if (d == null || d.nodes.Count == 0) continue;
                    var st = room.State;
                    string problem = Campaign ? root.universe.career.LaunchProblem(d, st.runway) : null;
                    if (problem == null && Expedition) { var dash = Rocket.Expedition.Evaluate(d, Rocket.Expedition.Scenario(st.exScenario), st.exDifficulty, st.players.Count); if (!dash.CanLaunch) problem = dash.blockers[0]; }
                    if (problem != null) { room.Chat($"Can't launch yet: {problem}"); continue; }
                    st.coopPhase = 1; st.flights++; st.goalDone = false; st.summary = null;
                    maxAltitude = 0;
                    if (Expedition)
                    {
                        // a fresh expedition: the budget, the crew, nothing done yet
                        var sc = Rocket.Expedition.Scenario(st.exScenario);
                        st.ex = new ExProgress { budget = Rocket.Expedition.Budget(sc, st.exDifficulty), cost = Prices.Of(d), crew = st.players.Count,
                            dvHad = Staging.AnalyzeDesign(d, 9.81).Sum(r => r.dvVac) };
                        root.universe.career.scavenged.Clear();
                    }
                    room.Touch();
                    root.Launch(d, st.runway, Mathf.Clamp(st.pad, 0, LaunchSite.Pads.Length - 1));
                    root.flight.SeatPlayers(st.players.Select(q => q.name).ToList());   // the players are the crew (milestone 41: in a rocket room too, so they wear the looks they made)
                    root.flight.Notice(Expedition ? $"Off on the expedition! Everyone's aboard. {MyJobs(true)}" : $"Flying together! {MyJobs(true)}");
                    break;
                }
            }
            bool building = !root.inFlight && !root.inMenu && !root.inMission;
            if (building)
            {
                bool builder = CanBuild;
                if (hangar.spectate == builder) hangar.spectate = !builder;
                if (builder && Expedition)
                {
                    // milestone 34: everyone builds the same rocket. A change made here goes to the room; someone else's
                    // change comes in when there's nothing unsent here and nothing in this player's hand.
                    designTimer -= Time.unscaledDeltaTime;
                    if (designTimer <= 0 && hangar.design != null)
                    {
                        designTimer = 0.25f;
                        string j = JsonUtility.ToJson(hangar.design);
                        string roomJ = room.State.designJson;
                        if (j != lastAppliedDesign && j != roomJ) { lastSentDesign = j; lastAppliedDesign = j; room.Send(new NetMsg { t = "design", json = j }); }
                        else if (!string.IsNullOrEmpty(roomJ) && roomJ != lastAppliedDesign && !hangar.Holding)
                        {
                            lastAppliedDesign = roomJ;
                            try { var d = JsonUtility.FromJson<VesselDesign>(roomJ); d.Upgrade(); hangar.SetDesignFromNet(d); } catch { }
                        }
                    }
                }
                else if (builder)
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
            try { var d = JsonUtility.FromJson<VesselDesign>(room.State.designJson); d.Upgrade(); Decals.Learn(d); root.currentDesign = d; } catch { }
        }

        // ---------------------------------------------------------------- building: launching

        /// <summary>The builder's LAUNCH: the host launches; a player builder asks the host to.</summary>
        public void Launch(VesselDesign d, bool runway, int pad)
        {
            if (!CanBuild) return;
            room.Send(new NetMsg { t = "design", json = JsonUtility.ToJson(d) });
            room.Send(new NetMsg { t = "launch", on = runway, n = pad });
        }

        public bool AllReady => room.State.players.Where(p => !p.Has((MRole)Builder) && p.id != room.MyId).All(p => p.ready);
        public string NotReady => string.Join(", ", room.State.players.Where(p => !p.Has((MRole)Builder) && p.id != room.MyId && !p.ready).Select(p => p.name));

        /// <summary>Why the team's rocket can't go on this expedition yet (over budget, not enough seats), or null.</summary>
        public string LaunchProblem(VesselDesign d)
        {
            if (!Expedition) return null;
            var dash = Rocket.Expedition.Evaluate(d, Rocket.Expedition.Scenario(room.State.exScenario), room.State.exDifficulty, room.State.players.Count);
            return dash.CanLaunch ? null : dash.blockers[0];
        }

        public bool showReport;
        int seenReports = -1;

        // ---------------------------------------------------------------- the panels

        static readonly Color[] RoleColor = { new Color(0.95f, 0.6f, 0.25f), new Color(0.4f, 0.65f, 1f), new Color(0.5f, 0.9f, 0.5f), new Color(0.85f, 0.55f, 0.95f) };
        string noteText = "", chatText = "";
        public int notePart = -1;
        Vector2 noteScroll;

        /// <summary>
        /// Milestone 34: the team, comments and chat panels folded away into one slim bar (they used to cover half the
        /// screen). Kept between rooms.
        /// </summary>
        public static bool Collapsed
        {
            get { try { return PlayerPrefs.GetInt("coopCollapsed", 0) == 1; } catch { return false; } }
            set { try { PlayerPrefs.SetInt("coopCollapsed", value ? 1 : 0); PlayerPrefs.Save(); } catch { } }
        }

        /// <summary>The folded-away bar: the room, how many are here, the latest chat line, and a button to open it all again.</summary>
        public void DrawCollapsedBar(Rect r)
        {
            Ui.Box(r);
            var chat = room.State.chat;
            string last = chat.Count > 0 ? chat[chat.Count - 1] : "no messages yet";
            var clip = new GUIStyle(Ui.Small) { wordWrap = false, clipping = TextClipping.Clip };
            Ui.Text(new Rect(r.x + 10, r.y + (r.height - 20) / 2, r.width - 150, 20), $"<b>{room.State.room}</b> · {room.State.players.Count} here · <color=#9aa4ad>{last}</color>", clip);
            if (Ui.Press(new Rect(r.xMax - 132, r.y + (r.height - 26) / 2, 124, 26), "Team & chat ▸", Ui.ButtonSmall)) Collapsed = false;
        }

        /// <summary>How tall the team panel is (it grows with the players, and with the job buttons when they're open).</summary>
        public float TeamHeight(bool flying) => 30 + room.State.players.Count * 20 + 48 + (!flying && room.State.players.Count > 1 ? (showJobs ? 30 : 26) : 0);
        bool showJobs;

        /// <summary>
        /// The team, each player's jobs and whether they're ready (top right in the builder and in flight). Milestone 32:
        /// the four job buttons only show after "Change jobs", so there's less to look at.
        /// </summary>
        public void DrawTeam(Rect r, bool flying)
        {
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 10, r.y + 6, r.width - 50, 22), $"<b>{room.State.room}</b>  ·  code <color=#ffd56a>{room.State.code}</color>", Ui.Small);
            if (Ui.Press(new Rect(r.xMax - 34, r.y + 5, 26, 22), "–", Ui.ButtonSmall)) Collapsed = true;   // fold the panels away
            float y = r.y + 28;
            var clip = new GUIStyle(Ui.Small) { wordWrap = false, clipping = TextClipping.Clip };
            foreach (var p in room.State.players)
            {
                string jobs = string.Join(", ", Enumerable.Range(0, 4).Where(k => p.Has((MRole)k)).Select(k => RoleNames[k]));
                // milestone 34: each player's Ardling note and suit colour
                var lk = MyArdling.LookOf(p.look ?? p.name);
                GUI.DrawTexture(new Rect(r.x + 10, y + 1, 18, 18), Ardlings.FaceTexture(lk.face, lk.paper));
                Ui.Line(new Rect(r.x + 10, y + 18, 18, 2), lk.suit);
                Ui.Text(new Rect(r.x + 32, y, r.width - 42, 20), $"{(p.ready && !flying ? "<color=#7dff8a>✓</color> " : "")}<b>{p.name}</b>{(p.id == room.MyId ? " (you)" : "")}: {jobs}", clip);
                for (int k = 0; k < 4; k++) if (p.Has((MRole)k)) Ui.Line(new Rect(r.x + 4, y + 4, 3, 12), RoleColor[k]);
                y += 20;
            }
            // one job: what it involves; several (a small team): just their names, so it fits
            int jobCount = Enumerable.Range(0, 4).Count(Has);
            string jobText = jobCount <= 1 ? $"<color=#9fc6e8>Your job:</color> {MyJobs(flying)}" : $"<color=#9fc6e8>Your jobs:</color> {string.Join(", ", Enumerable.Range(0, 4).Where(Has).Select(k => RoleNames[k]))}";
            Ui.Text(new Rect(r.x + 10, y + 2, r.width - 20, 44), jobText, new GUIStyle(Ui.Small) { wordWrap = true });
            if (!flying && room.State.players.Count > 1)
            {
                if (!showJobs)
                {
                    if (Ui.Press(new Rect(r.x + 10, r.yMax - 26, 120, 22), "Change jobs", Ui.ButtonSmall)) showJobs = true;
                }
                else
                {
                    // take a job (whoever had it gets one of yours, so nobody's left with nothing to do)
                    float bw = (r.width - 20 - 30) / 4f;
                    for (int k = 0; k < 4; k++)
                    {
                        bool mine = Has(k);
                        var br = new Rect(r.x + 10 + k * bw, r.yMax - 28, bw - 4, 24);
                        if (Ui.Press(br, RoleNames[k].Split(' ')[0], mine ? Ui.ButtonSmallSel : Ui.ButtonSmall) && !mine) { room.TakeRole((MRole)k); showJobs = false; }
                        Ui.Line(new Rect(br.x, br.yMax - 2, br.width, 2), RoleColor[k]);
                    }
                    if (Ui.Press(new Rect(r.xMax - 36, r.yMax - 28, 26, 24), "×", Ui.ButtonSmall)) showJobs = false;
                }
            }
        }

        static readonly string[] QuickNotesShort = { "Add more fuel", "It needs a parachute", "Add fins", "Looks great!" };

        /// <summary>
        /// Building: comments on the rocket, the goal and launch site, ready. The builder ticks comments off. Milestone 32:
        /// fewer quick comments, and the goal and launch site are plain words for everyone but mission control.
        /// </summary>
        public void DrawBuildPanel(Rect r, VesselDesign d)
        {
            Ui.Box(r); Ui.Box(r);
            bool builder = Has(Builder);
            Ui.Text(new Rect(r.x + 10, r.y + 6, r.width - 20, 24), builder ? "The team's comments" : "Comment on the rocket", Ui.Header);
            float y = r.y + 34;
            // the goal and where from (mission control picks)
            var goal = Goal(room.State.goal);
            bool control = Has(Control);
            string from = room.State.runway ? "Runway" : LaunchSite.Pads[Mathf.Clamp(room.State.pad, 0, LaunchSite.Pads.Length - 1)].name;
            if (control)
            {
                Ui.Text(new Rect(r.x + 10, y + 4, 60, 20), "Goal", Ui.Small);
                if (Ui.Press(new Rect(r.x + 60, y, r.width - 70, 26), goal.name + "  ▸", Ui.ButtonSmall))
                {
                    int gi = Array.FindIndex(Goals, g => g.id == goal.id);
                    room.Send(new NetMsg { t = "goal", b = Goals[(gi + 1) % Goals.Length].id });
                }
                y += 30;
                Ui.Text(new Rect(r.x + 10, y + 4, 60, 20), "From", Ui.Small);
                if (Ui.Press(new Rect(r.x + 60, y, r.width - 70, 26), from + "  ▸", Ui.ButtonSmall))
                {
                    int next = room.State.runway ? 0 : room.State.pad + 1;
                    room.Send(new NetMsg { t = "pad", n = next >= LaunchSite.Pads.Length ? -1 : next });
                }
                y += 34;
            }
            else
            {
                Ui.Text(new Rect(r.x + 10, y, r.width - 20, 40), $"Goal: <b>{goal.name}</b>   ·   from the <b>{from.ToLowerInvariant()}</b>\n<color=#8a929c>(mission control picks: {Holder(Control)})</color>", new GUIStyle(Ui.Small) { wordWrap = true });
                y += 44;
            }
            // the comments
            var notes = room.State.notes;
            float bottomH = builder ? 46 : 120;
            var listRect = new Rect(r.x + 6, y, r.width - 12, Mathf.Max(30, r.yMax - y - bottomH));
            var st = new GUIStyle(Ui.Small) { wordWrap = true };
            float total = 0;
            foreach (var n in notes) total += st.CalcHeight(new GUIContent(NoteLine(n, d)), listRect.width - 90) + 8;
            noteScroll = GUI.BeginScrollView(listRect, noteScroll, new Rect(0, 0, listRect.width - 18, Mathf.Max(total, 10)));
            float ny = 0;
            if (notes.Count == 0) Ui.Text(new Rect(4, 0, listRect.width - 24, 40), builder ? "No comments yet: the others can click a part of your rocket to comment on it." : "No comments yet. Click a part of the rocket, then write what you think.", st);
            foreach (var n in notes.AsEnumerable().Reverse())
            {
                string line = NoteLine(n, d);
                float h = st.CalcHeight(new GUIContent(line), listRect.width - 90);
                Ui.Text(new Rect(4, ny, listRect.width - 94, h), line, st);
                if (builder && n.state == 0)
                {
                    if (Ui.Press(new Rect(listRect.width - 86, ny, 34, 22), "✓", Ui.ButtonSmall)) room.Send(new NetMsg { t = "noteState", n = n.id, on = true });
                    if (Ui.Press(new Rect(listRect.width - 48, ny, 30, 22), "✗", Ui.ButtonSmall)) room.Send(new NetMsg { t = "noteState", n = n.id, on = false });
                }
                ny += h + 8;
            }
            GUI.EndScrollView();
            if (!builder)
            {
                float by = r.yMax - bottomH + 4;
                string onWhat = notePart >= 0 && d != null && notePart < d.nodes.Count ? $"On the <b>{PartCatalog.Get(d.nodes[notePart].id)?.name ?? "part"}</b>:" : "On the whole rocket (click a part first to pick one):";
                Ui.Text(new Rect(r.x + 10, by, r.width - 20, 20), onWhat, new GUIStyle(Ui.Small) { wordWrap = false, clipping = TextClipping.Clip });
                noteText = GUI.TextField(new Rect(r.x + 10, by + 20, r.width - 90, 26), noteText ?? "", 140, Ui.Field);
                bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && !string.IsNullOrWhiteSpace(noteText);
                if ((Ui.Press(new Rect(r.xMax - 74, by + 20, 64, 26), "Send", Ui.ButtonSmall) || enter) && !string.IsNullOrWhiteSpace(noteText))
                {
                    room.Send(new NetMsg { t = "note", n = notePart, a = noteText });
                    noteText = "";
                    GUIUtility.keyboardControl = 0;   // back to the game (the keys work again)
                }
                float qx = r.x + 10, qy = by + 50;
                foreach (var q in QuickNotesShort)
                {
                    float w = Ui.Small.CalcSize(new GUIContent(q)).x + 16;
                    if (qx + w > r.xMax - 10) break;
                    if (Ui.Press(new Rect(qx, qy, w, 22), q, Ui.ButtonSmall)) room.Send(new NetMsg { t = "note", n = notePart, a = q });
                    qx += w + 4;
                }
                var me = room.Me;
                if (me != null && Ui.Press(new Rect(r.x + 10, r.yMax - 36, r.width - 20, 30), me.ready ? "Ready to launch ✓  (click to undo)" : "I'm ready to launch", me.ready ? Ui.ButtonGo : Ui.Button)) room.Ready(!me.ready);
            }
            else Ui.Text(new Rect(r.x + 10, r.yMax - 40, r.width - 20, 36), room.State.players.Count <= 1 ? "<color=#7dff8a>Playing alone: LAUNCH when you're ready.</color>" : AllReady ? "<color=#7dff8a>Everyone's ready: LAUNCH when you are.</color>" : $"Waiting for: {NotReady}", new GUIStyle(Ui.Small) { wordWrap = true });
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
            for (int i = Math.Max(0, chat.Count - 3); i < chat.Count; i++) { Ui.Text(new Rect(r.x + 8, y, r.width - 16, 18), chat[i], clip); y += 18; }
            chatText = GUI.TextField(new Rect(r.x + 8, r.yMax - 30, r.width - 80, 24), chatText ?? "", 120, Ui.Field);
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && !string.IsNullOrWhiteSpace(chatText);
            if ((Ui.Press(new Rect(r.xMax - 66, r.yMax - 30, 58, 24), "Send", Ui.ButtonSmall) || enter) && !string.IsNullOrWhiteSpace(chatText))
            {
                room.Chat(chatText);
                chatText = "";
                GUIUtility.keyboardControl = 0;   // sent: the game's keys work again (milestone 32: in flight they used to stage and steer while you typed)
            }
        }
    }
}
