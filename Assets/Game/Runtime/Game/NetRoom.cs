using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Rocket
{
    [Serializable]
    public class NetPlayer
    {
        public int id;
        public string name;
        public int roles;          // bit per MRole
        public bool ready;
        public int changes;
        public string look;        // milestone 34: their Ardling ("suit,paper,face,hat")
        public bool Has(MRole r) => (roles & (1 << (int)r)) != 0;
    }

    /// <summary>A comment on the rocket (milestone 30): on one part, or the whole rocket (part -1); the builder marks it done or not.</summary>
    [Serializable]
    public class CoopNote
    {
        public int id, from, part = -1, state;   // state 0 open, 1 done, 2 no
        public string text;
    }

    [Serializable]
    public class NetRequest
    {
        public int id, from, toRole, answer;   // answer 0 open, 1 yes, 2 no
        public string text;
    }

    /// <summary>Everything a mission team shares: who's in, the design, the chat, requests between roles, the last result.</summary>
    [Serializable]
    public class RoomState
    {
        public int proto = NetRoom.Proto;
        public string room, code;
        public int phase;          // 0 designing, 1 flying, 2 the report
        public int runs, nextReq = 1, nextPlayer = 2, stateId;
        public List<NetPlayer> players = new List<NetPlayer>();
        public MissionPlan plan = new MissionPlan();
        public List<string> chat = new List<string>();
        public List<NetRequest> requests = new List<NetRequest>();
        public string resultJson;
        // ---- milestone 30: a rocket room (build and fly together)
        public int activity;                     // 0 mission design, 1 rocket
        public bool campaign;                    // a campaign (career, missions unlock) or a sandbox
        public bool pvp;                         // milestone 43: players' weapons hurt each other (the host switches it)
        public int unlocked = 99;                // mission design campaign: how many missions are open
        public string careerJson;                // rocket campaign: the host's career (funds, parts, contracts), for everyone's builder
        public string designJson, goal = "space", summary;
        // ---- milestone 34: an expedition (activity 2): the plan, what's been done, the last report; the game's weather
        public string exScenario = "tal";
        public int exDifficulty = 1, exRuns, weatherSeed;
        public ExProgress ex = new ExProgress();
        public string exReport;
        // ---- milestone 38: survival in a room (the host keeps everyone's and sends it round): each Ardling's health,
        // hunger and stamina, each player's backpack and what they're holding, and the things lying about
        public List<Vitals> vitals = new List<Vitals>();
        public List<NetPack> packs = new List<NetPack>();
        public List<WorldItem> items = new List<WorldItem>();
        public string hazard;   // milestone 39: what's going on out there right now (the host's game runs it)
        public List<Drone> drones = new List<Drone>();   // milestone 40: the rogue drones nearby (the host's game runs them)
        /// <summary>A room that builds and flies a rocket: a rocket room (1) or an expedition (2).</summary>
        public bool Rocket => activity == 1 || activity == 2;
        // ---- milestone 31: the room's best flight on each mission ("scenario|score|stars"), announced so other rooms can race it
        public List<string> bests = new List<string>();
        public void BestFor(string scenario, out int score, out int stars)
        {
            score = 0; stars = 0;
            foreach (var e in bests)
            {
                var f = e.Split('|');
                if (f.Length == 3 && f[0] == scenario) { int.TryParse(f[1], out score); int.TryParse(f[2], out stars); return; }
            }
        }
        public void RecordBest(string scenario, int score, int stars)
        {
            BestFor(scenario, out int was, out _);
            if (score <= was && was > 0) return;
            bests.RemoveAll(e => e.StartsWith(scenario + "|"));
            bests.Add($"{scenario}|{score}|{stars}");
        }
        public List<CoopNote> notes = new List<CoopNote>();
        public int nextNote = 1, pad, coopPhase, flights;   // coopPhase 0 building, 1 flying
        public bool runway, goalDone;
        public NetPlayer Player(int id) => players.FirstOrDefault(p => p.id == id);
    }

    [Serializable]
    public class NetMsg
    {
        public string t, a, b, name, json;
        public int n, ver;
        public bool on;
    }

    /// <summary>A room seen on the local network.</summary>
    public class RoomInfo
    {
        public string name, code, host;
        public int port, players, activity;
        public bool campaign;
        public float seen;
        public string scenario;                  // milestone 31: the mission it's on, and its best score there
        public int score, stars;
    }

    /// <summary>
    /// Milestone 29 (captaincarl24: "create a room and other people join your room ... 4 people playing on their own device"):
    /// a mission team over the network. One player creates a room: their game listens for up to three others and
    /// announces the room on the local network (so it shows up in everyone's list), with a room code that also works
    /// typed in. The host's game holds the shared design: each player sends the changes their role is allowed to make,
    /// the host checks and applies them and sends the whole room to everyone. The mission is flown on the host and the
    /// result sent round, so every screen shows the same thing. Playing alone uses the same room, with no network.
    /// </summary>
    public class NetRoom
    {
        public const int Proto = 1, Port = 47770, DiscoveryPort = 47769, MaxPlayers = 4;
        public static NetRoom Current;

        public bool IsHost, Solo;
        public int MyId = 1;
        public RoomState State = new RoomState();
        public string Error, Status;
        public bool Connected;
        public int ListenPort;
        public readonly List<RoomInfo> Found = new List<RoomInfo>();
        public int StateVersion;                 // goes up when a new state arrives (screens redraw)
        /// <summary>Rocket rooms (milestone 30): the host hears launches, the pilot's controls and other jobs' commands; players hear the flight.</summary>
        public Action<int, NetMsg> OnHostCommand;
        public Action<NetMsg> OnClientMessage;
        /// <summary>Milestone 34: someone's place in a cabin (the host passes each on to everyone).</summary>
        public Action<int, string, bool> OnPose;
        public Action<int, string> OnCabin;   // milestone 35: someone used something in a cabin
        public string RoleName(int r) => State.Rocket ? Coop.RoleNames[Mathf.Clamp(r, 0, 3)] : MissionDesign.RoleNames[Mathf.Clamp(r, 0, 3)];

        TcpListener listener;
        Thread acceptThread;
        readonly List<Conn> conns = new List<Conn>();
        readonly ConcurrentQueue<(Conn c, string line)> inbox = new ConcurrentQueue<(Conn, string)>();
        Conn hostConn;
        UdpClient beacon, finder;
        float beaconTimer, sendTimer;
        bool dirty;
        volatile bool stopping;

        /// <summary>
        /// One connection. Milestone 32: messages go into a queue that its own thread writes out, so a slow connection
        /// (a busy Wi-Fi, a laptop going to sleep) can no longer freeze the host's whole game while it waits. When there's
        /// nothing to send for a moment the thread sends a heartbeat, so both ends notice quickly if the other one's gone.
        /// </summary>
        class Conn
        {
            public TcpClient tcp;
            public StreamWriter w;
            public int id;
            public bool hello;
            public long lastHeard = DateTime.UtcNow.Ticks;
            readonly BlockingCollection<string> outbox = new BlockingCollection<string>(new ConcurrentQueue<string>());
            Thread writer;
            volatile bool closed;

            public void Start()
            {
                writer = new Thread(WriteLoop) { IsBackground = true, Name = "Room writer" };
                writer.Start();
            }

            /// <summary>Queues a line. Flight frames are dropped if the line's backed up (the next one's on its way).</summary>
            public void Send(string line, bool droppable = false)
            {
                if (closed) return;
                if (droppable && outbox.Count > 30) return;
                if (outbox.Count > 2000) { Close(); return; }   // hopelessly behind: let it go
                try { outbox.Add(line); } catch { }
            }

            void WriteLoop()
            {
                try
                {
                    while (!closed)
                    {
                        if (outbox.TryTake(out string line, 1500))
                        {
                            bool closeNow = false;
                            do
                            {
                                if (line == CloseMark) { closeNow = true; break; }
                                w.WriteLine(line);
                            } while (outbox.TryTake(out line));   // everything waiting, then one flush
                            w.Flush();
                            if (closeNow) { Thread.Sleep(200); Close(); return; }
                        }
                        else { w.WriteLine(Heartbeat); w.Flush(); }
                    }
                }
                catch { Close(); }
            }

            public void Close()
            {
                closed = true;
                try { tcp?.Close(); } catch { }
            }

            /// <summary>Sends what's queued (a "sorry, the room's full"), then hangs up.</summary>
            public void CloseAfterSending() { try { outbox.Add(CloseMark); } catch { Close(); } }
        }

        const string CloseMark = "__close__";

        const string Heartbeat = "{\"t\":\"hb\"}";
        /// <summary>Nothing at all from the other end for this long: the connection's gone (Wi-Fi dropped, laptop asleep).</summary>
        public const double SilenceLimit = 15.0;

        public NetPlayer Me => State.Player(MyId);
        public bool Can(MRole r) => Me != null && Me.Has(r);
        public int PlayerCount => State.players.Count;

        // ---------------------------------------------------------------- starting

        public static NetRoom StartSolo(string name, MissionPlan plan)
        {
            var r = new NetRoom { IsHost = true, Solo = true, Connected = true };
            r.State.room = "Solo"; r.State.plan = plan ?? new MissionPlan();
            r.State.players.Add(new NetPlayer { id = 1, name = Clean(name), roles = 15, look = MyArdling.Code });
            Current = r;
            return r;
        }

        public static NetRoom Host(string name, string roomName, MissionPlan plan, int activity = 0, bool campaign = false)
        {
            var r = new NetRoom { IsHost = true, Connected = true };
            r.State.activity = activity;
            r.State.campaign = campaign;
            r.State.room = string.IsNullOrWhiteSpace(roomName) ? $"{Clean(name)}'s room" : roomName.Trim();
            r.State.plan = plan ?? new MissionPlan();
            r.State.players.Add(new NetPlayer { id = 1, name = Clean(name), roles = 15, look = MyArdling.Code });
            for (int p = Port; p < Port + 10 && r.listener == null; p++)
            {
                try { var l = new TcpListener(IPAddress.Any, p); l.Start(MaxPlayers); r.listener = l; r.ListenPort = p; }
                catch { }
            }
            if (r.listener == null) { r.Error = "Couldn't open a room: the network port is busy."; r.Connected = false; return r; }
            r.State.code = CodeFor(LocalAddress(), r.ListenPort - Port);
            r.acceptThread = new Thread(r.AcceptLoop) { IsBackground = true, Name = "Room accept" };
            r.acceptThread.Start();
            try { r.beacon = new UdpClient { EnableBroadcast = true }; } catch { }
            r.Status = "Room open";
            Current = r;
            return r;
        }

        public static NetRoom Join(string name, string codeOrAddress)
        {
            var r = new NetRoom { IsHost = false, MyId = 0 };
            if (!Decode(codeOrAddress, out var ip, out int port)) { r.Error = "That room code doesn't look right."; return r; }
            r.Status = $"Joining {codeOrAddress}...";
            string who = Clean(name);
            var t = new Thread(() =>
            {
                try
                {
                    var tcp = new TcpClient();
                    var ar = tcp.BeginConnect(ip, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(5000) || !tcp.Connected) { r.inbox.Enqueue((null, "__fail|Couldn't reach that room. Are you on the same network?")); return; }
                    tcp.EndConnect(ar);
                    tcp.NoDelay = true;
                    var c = new Conn { tcp = tcp, w = new StreamWriter(tcp.GetStream(), new UTF8Encoding(false)) { AutoFlush = false } };
                    r.hostConn = c;
                    c.Start();
                    c.Send(JsonUtility.ToJson(new NetMsg { t = "hello", name = who, ver = Proto, a = MyArdling.Code }));
                    r.ReadLoop(c);
                }
                catch (Exception e) { r.inbox.Enqueue((null, "__fail|Couldn't join: " + e.Message)); }
            }) { IsBackground = true, Name = "Room join" };
            t.Start();
            Current = r;
            return r;
        }

        public void Stop()
        {
            stopping = true;
            try { listener?.Stop(); } catch { }
            lock (conns) foreach (var c in conns) c.Close();
            hostConn?.Close();
            try { beacon?.Close(); } catch { }
            try { finder?.Close(); } catch { }
            if (Current == this) Current = null;
        }

        static string Clean(string n)
        {
            n = (n ?? "").Trim();
            if (n.Length == 0) n = "Player";
            return n.Length > 16 ? n.Substring(0, 16) : n;
        }

        // ---------------------------------------------------------------- threads

        void AcceptLoop()
        {
            while (!stopping)
            {
                try
                {
                    var tcp = listener.AcceptTcpClient();
                    tcp.NoDelay = true;
                    var c = new Conn { tcp = tcp, w = new StreamWriter(tcp.GetStream(), new UTF8Encoding(false)) };
                    lock (conns) conns.Add(c);
                    c.Start();
                    new Thread(() => ReadLoop(c)) { IsBackground = true, Name = "Room reader" }.Start();
                }
                catch { if (stopping) return; Thread.Sleep(50); }
            }
        }

        void ReadLoop(Conn c)
        {
            try
            {
                var rd = new StreamReader(c.tcp.GetStream(), Encoding.UTF8);
                string line;
                while (!stopping && (line = rd.ReadLine()) != null)
                {
                    c.lastHeard = DateTime.UtcNow.Ticks;
                    if (line == Heartbeat) continue;   // just "still here"
                    inbox.Enqueue((c, line));
                }
            }
            catch { }
            inbox.Enqueue((c, "__bye"));
        }

        // ---------------------------------------------------------------- every frame

        public void Update(float dt)
        {
            while (inbox.TryDequeue(out var m))
            {
                if (m.line.StartsWith("__fail|")) { Error = m.line.Substring(7); Connected = false; continue; }
                if (m.line == "__bye")
                {
                    if (IsHost) Leave(m.c);
                    else if (!stopping) { Error = "The host closed the room (or the connection dropped)."; Connected = false; }
                    continue;
                }
                NetMsg msg;
                try { msg = JsonUtility.FromJson<NetMsg>(m.line); } catch { continue; }
                if (msg == null || msg.t == null) continue;
                if (IsHost) HostReceive(m.c, msg);
                else ClientReceive(msg);
            }
            if (IsHost && !Solo)
            {
                sendTimer -= dt;
                if (dirty && sendTimer <= 0) { Broadcast(); sendTimer = 0.08f; }
                beaconTimer -= dt;
                if (beaconTimer <= 0 && beacon != null)
                {
                    beaconTimer = 1f;
                    var data = Encoding.UTF8.GetBytes(BeaconText());
                    try { beacon.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort)); } catch { }
                    // also each network's own broadcast address (some Wi-Fi, and Macs with several networks, don't pass 255.255.255.255 on)
                    broadcastTargets ??= DirectedBroadcasts();
                    foreach (var b in broadcastTargets) try { beacon.Send(data, data.Length, new IPEndPoint(b, DiscoveryPort)); } catch { }
                    try { beacon.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, DiscoveryPort)); } catch { }
                }
                // a player we haven't heard from at all for a while has gone (their heartbeat stopped)
                long now = DateTime.UtcNow.Ticks;
                List<Conn> silent = null;
                lock (conns) foreach (var c in conns) if (c.hello && (now - c.lastHeard) / 1e7 > SilenceLimit) (silent ??= new List<Conn>()).Add(c);
                if (silent != null) foreach (var c in silent) Leave(c);
            }
            else if (!IsHost && Connected && hostConn != null && (DateTime.UtcNow.Ticks - hostConn.lastHeard) / 1e7 > SilenceLimit)
            {
                Error = "Lost the connection to the room (nothing heard from the host for a while).";
                Connected = false;
                stopping = true;
                hostConn.Close();
            }
            if (Solo && dirty) { dirty = false; State.stateId++; StateVersion++; RegisterLooks(); }
        }

        // ---------------------------------------------------------------- the host

        void HostReceive(Conn c, NetMsg msg)
        {
            if (msg.t == "hello")
            {
                if (msg.ver != Proto) { c.Send(JsonUtility.ToJson(new NetMsg { t = "refuse", a = "That room is on a different version of the game: update both games to the same release." })); c.CloseAfterSending(); return; }
                if (State.players.Count >= MaxPlayers) { c.Send(JsonUtility.ToJson(new NetMsg { t = "refuse", a = "That room is full (4 players)." })); c.CloseAfterSending(); return; }
                c.id = State.nextPlayer++;
                c.hello = true;
                var joiner = new NetPlayer { id = c.id, name = UniqueName(Clean(msg.name)), look = Ardlings.Parse(msg.a, out _, out _, out _, out _) ? msg.a : null };
                State.players.Add(joiner);
                GiveJobsTo(joiner);
                Say($"{Clean(msg.name)} joined the team.");
                c.Send(JsonUtility.ToJson(new NetMsg { t = "welcome", n = c.id }));
                MarkDirty();
                return;
            }
            if (!c.hello) return;
            Apply(c.id, msg);
        }

        void Leave(Conn c)
        {
            lock (conns) conns.Remove(c);
            c.Close();
            var p = State.Player(c.id);
            if (p == null) return;
            State.players.Remove(p);
            HandOnJobs(p.roles);
            Say($"{p.name} left.");
            MarkDirty();
        }

        /// <summary>Two players with the same name get told apart ("Sam" and "Sam 2").</summary>
        string UniqueName(string n)
        {
            if (!State.players.Any(p => p.name == n)) return n;
            for (int k = 2; k < 10; k++) { string m = $"{(n.Length > 13 ? n.Substring(0, 13) : n)} {k}"; if (!State.players.Any(p => p.name == m)) return m; }
            return n;
        }

        /// <summary>
        /// Milestone 32: someone new joins. Everyone keeps the jobs they have (they used to be dealt out again from scratch,
        /// undoing whatever people had picked); the newcomer takes jobs from whoever has the most, until the jobs are shared
        /// out as evenly as they can be.
        /// </summary>
        public void GiveJobsTo(NetPlayer joiner)
        {
            int want = Math.Max(1, 4 / Math.Max(1, State.players.Count));
            for (int guard = 0; guard < 4 && CountJobs(joiner) < want; guard++)
            {
                var rich = State.players.Where(q => q != joiner && CountJobs(q) > 1).OrderByDescending(CountJobs).ThenByDescending(q => q.id).FirstOrDefault();
                if (rich == null) break;
                int give = -1;
                for (int r = 3; r >= 0; r--) if ((rich.roles & (1 << r)) != 0) { give = r; break; }   // the last job on their list
                rich.roles &= ~(1 << give);
                joiner.roles |= 1 << give;
            }
            if (joiner.roles == 0 && State.players.Count > 0) joiner.roles = 0;   // a fifth wheel can still watch and chat
        }

        /// <summary>Someone left: their jobs go to whoever has the fewest (the host first if it's a tie).</summary>
        public void HandOnJobs(int roles)
        {
            for (int r = 0; r < 4; r++)
            {
                if ((roles & (1 << r)) == 0) continue;
                if (State.players.Any(q => q.Has((MRole)r))) continue;
                var poor = State.players.OrderBy(CountJobs).ThenBy(q => q.id).FirstOrDefault();
                if (poor != null) poor.roles |= 1 << r;
            }
        }

        /// <summary>Milestone 41: only the builder changes or launches the rocket (in an expedition too); with nobody holding the
        /// job (they just left), anyone may, until it's handed on.</summary>
        bool BuilderOk(NetPlayer p) => p != null && (p.Has(MRole.Lead) || !State.players.Any(q => q.Has(MRole.Lead)));
        static int CountJobs(NetPlayer p) { int n = 0; for (int r = 0; r < 4; r++) if ((p.roles & (1 << r)) != 0) n++; return n; }

        /// <summary>Shares the four roles out among whoever's here: the host first (with the extras when there are fewer than four).</summary>
        public void ShareRoles()
        {
            var ps = State.players;
            int n = ps.Count;
            foreach (var p in ps) p.roles = 0;
            if (n == 0) return;
            int[][] plan = n == 1 ? new[] { new[] { 0, 1, 2, 3 } }
                : n == 2 ? new[] { new[] { 0, 1 }, new[] { 2, 3 } }
                : n == 3 ? new[] { new[] { 0 }, new[] { 1 }, new[] { 2, 3 } }
                : new[] { new[] { 0 }, new[] { 1 }, new[] { 2 }, new[] { 3 } };
            for (int i = 0; i < n && i < plan.Length; i++) foreach (int r in plan[i]) ps[i].roles |= 1 << r;
        }

        void Say(string text)
        {
            State.chat.Add(text);
            while (State.chat.Count > 30) State.chat.RemoveAt(0);
        }

        void MarkDirty() { dirty = true; }

        /// <summary>Milestone 34: everyone in the room wears the Ardling they made.</summary>
        void RegisterLooks() { foreach (var p in State.players) MyArdling.Register(p.name, p.look); }

        /// <summary>Tells the room about a new look (from the My Ardling menu).</summary>
        public void SendLook(string code) => Send(new NetMsg { t = "look", a = code });

        void Broadcast()
        {
            dirty = false;
            State.stateId++;
            StateVersion++;
            RegisterLooks();
            string json = JsonUtility.ToJson(new NetMsg { t = "state", json = JsonUtility.ToJson(State) });
            lock (conns) foreach (var c in conns) if (c.hello) c.Send(json);
        }

        /// <summary>A change from a player (or from the host's own screen): only what their role may change.</summary>
        public void Apply(int who, NetMsg m)
        {
            var p = State.Player(who);
            if (p == null) return;
            var plan = State.plan;
            bool changed = false;
            bool Role(MRole r) => p.Has(r);
            switch (m.t)
            {
                case "look":
                    if (Ardlings.Parse(m.a, out _, out _, out _, out _)) { p.look = m.a; MarkDirty(); }
                    break;
                case "pick":
                {
                    var part = MissionDesign.Part(m.b);
                    if (part == null || !Role(MissionDesign.RoleFor(part.cat)) || State.phase != 0) break;
                    switch (part.cat)
                    {
                        case MCat.Bus: plan.bus = part.id; break;
                        case MCat.Power: plan.power = part.id; break;
                        case MCat.Battery: plan.battery = part.id; break;
                        case MCat.Attitude: plan.attitude = part.id; break;
                        case MCat.Computer: plan.computer = part.id; break;
                        case MCat.Antenna: plan.antenna = part.id; break;
                        case MCat.Propulsion: plan.propulsion = part.id; break;
                        case MCat.Thermal: plan.thermal = part.id; break;
                        case MCat.Ground: plan.ground = part.id; break;
                        case MCat.Users: plan.users = part.id; break;
                        case MCat.Instrument:
                            if (plan.instruments.Contains(part.id)) plan.instruments.Remove(part.id);
                            else if (plan.instruments.Count < 7) plan.instruments.Add(part.id);
                            break;
                    }
                    changed = true;
                    break;
                }
                case "wings": if (Role(MRole.Engineer) && State.phase == 0) { plan.wings = Mathf.Clamp(m.n, 1, 4); changed = true; } break;
                case "orbit": if (Role(MRole.Lead) && State.phase == 0) { plan.orbit = (MOrbit)Mathf.Clamp(m.n, 0, 5); changed = true; } break;
                case "vehicle": if (Role(MRole.Lead) && State.phase == 0 && MissionDesign.Vehicles.Any(v => v.id == m.b)) { plan.vehicle = m.b; changed = true; } break;
                case "scenario":
                    if (State.campaign && !MissionDesign.OpenInCampaign(m.b, State.unlocked)) break;     // not unlocked yet
                    if (Role(MRole.Lead) && State.phase == 0 && MissionDesign.Scenarios.Any(s => s.id == m.b))
                    {
                        plan.scenario = m.b;
                        var sc = MissionDesign.Scenario(m.b);
                        if (!sc.sandbox) plan.body = sc.body;
                        changed = true;
                    }
                    break;
                case "sandbox":
                    if (Role(MRole.Lead) && State.phase == 0)
                    {
                        if (!string.IsNullOrEmpty(m.a)) plan.body = m.a;
                        if (!string.IsNullOrEmpty(m.b)) plan.sandboxTag = m.b;
                        if (m.n > 0) plan.sandboxYears = m.n;
                        changed = true;
                    }
                    break;
                case "plan":
                    // loading a saved or shared design: the lead's call
                    if (Role(MRole.Lead) && State.phase == 0 && !string.IsNullOrEmpty(m.json))
                    {
                        try { var np = JsonUtility.FromJson<MissionPlan>(m.json); if (np != null) { State.plan = np; changed = true; } } catch { }
                    }
                    break;
                case "take":
                {
                    int r = Mathf.Clamp(m.n, 0, 3);
                    if (p.Has((MRole)r)) return;
                    var had = State.players.FirstOrDefault(q => q != p && q.Has((MRole)r));
                    foreach (var q in State.players) q.roles &= ~(1 << r);
                    p.roles |= 1 << r;
                    // a swap: whoever had the job gets one of the taker's other jobs if they'd be left with none
                    if (had != null && had.roles == 0)
                        for (int k = 0; k < 4; k++)
                            if (k != r && (p.roles & (1 << k)) != 0) { p.roles &= ~(1 << k); had.roles |= 1 << k; break; }
                    Say($"{p.name} is now the {RoleName(r).ToLower()}.");
                    MarkDirty();
                    return;
                }
                case "ready": p.ready = m.on; MarkDirty(); return;
                case "chat":
                    if (!string.IsNullOrWhiteSpace(m.a)) { Say($"{p.name}: {m.a.Trim().Substring(0, Math.Min(120, m.a.Trim().Length))}"); MarkDirty(); }
                    return;
                case "ask":
                    if (!string.IsNullOrWhiteSpace(m.a))
                    {
                        State.requests.Add(new NetRequest { id = State.nextReq++, from = who, toRole = Mathf.Clamp(m.n, 0, 3), text = m.a.Trim() });
                        while (State.requests.Count > 12) State.requests.RemoveAt(0);
                        Say($"{p.name} asks the {RoleName(m.n).ToLower()}: {m.a.Trim()}");
                        MarkDirty();
                    }
                    return;
                // ---- rocket rooms (milestone 30)
                case "design":
                    // (milestone 41: only the builder, in an expedition too)
                    if (State.Rocket && BuilderOk(p) && State.coopPhase == 0 && !string.IsNullOrEmpty(m.json) && m.json != State.designJson) { State.designJson = m.json; p.changes++; MarkDirty(); }
                    return;
                case "note":
                    if (State.Rocket && !string.IsNullOrWhiteSpace(m.a))
                    {
                        string text = m.a.Trim();
                        if (text.Length > 140) text = text.Substring(0, 140);
                        State.notes.Add(new CoopNote { id = State.nextNote++, from = who, part = m.n, text = text });
                        while (State.notes.Count > 30) State.notes.RemoveAt(0);
                        p.changes++;
                        MarkDirty();
                    }
                    return;
                case "noteState":
                {
                    var note = State.notes.FirstOrDefault(x => x.id == m.n);
                    if (note != null && Role(MRole.Lead)) { note.state = m.on ? 1 : 2; MarkDirty(); }
                    return;
                }
                case "goal": if (State.Rocket && Role((MRole)Coop.Control) && State.coopPhase == 0 && Coop.Goals.Any(g => g.id == m.b)) { State.goal = m.b; State.goalDone = false; MarkDirty(); } return;
                case "pad": if (State.Rocket && Role((MRole)Coop.Control) && State.coopPhase == 0) { State.runway = m.n < 0; State.pad = Math.Max(0, m.n); MarkDirty(); } return;
                case "launch": if (State.Rocket && BuilderOk(p) && State.coopPhase == 0) OnHostCommand?.Invoke(who, m); return;
                case "exsc": if (State.activity == 2 && State.coopPhase == 0 && (Role((MRole)Coop.Control) || who == MyId) && Expedition.Scenarios.Any(x => x.id == m.b)) { State.exScenario = m.b; MarkDirty(); } return;
                case "exdiff": if (State.activity == 2 && State.coopPhase == 0 && (Role((MRole)Coop.Control) || who == MyId)) { State.exDifficulty = Mathf.Clamp(m.n, 0, 2); MarkDirty(); } return;
                case "in": if (State.Rocket && Role((MRole)Coop.Pilot)) OnHostCommand?.Invoke(who, m); return;
                // ---- expeditions (milestone 33): poses, actions and the pilot's controls go to the expedition on the host
                case "cmd": if (State.Rocket) OnHostCommand?.Invoke(who, m); return;
                case "wk": if (State.Rocket) OnHostCommand?.Invoke(who, m); return;   // milestone 43: a player's walking keys
                case "ip": if (State.Rocket) { OnPose?.Invoke(who, m.a, m.on); BroadcastRaw(new NetMsg { t = "ipr", n = who, a = m.a, on = m.on }); } return;
                case "cs": if (State.Rocket && !string.IsNullOrEmpty(m.a) && m.a.Length < 80) { OnCabin?.Invoke(who, m.a); BroadcastRaw(new NetMsg { t = "csr", n = who, a = m.a }); } return;
                case "answer":
                {
                    var q = State.requests.FirstOrDefault(x => x.id == m.n);
                    if (q != null && q.answer == 0 && p.Has((MRole)q.toRole))
                    {
                        q.answer = m.on ? 1 : 2;
                        Say($"{p.name} {(m.on ? "agreed" : "said no")}: {q.text}");
                        MarkDirty();
                    }
                    return;
                }
            }
            if (changed)
            {
                p.changes++;
                foreach (var q in State.players) q.ready = false;     // a changed design needs everyone's OK again
                MarkDirty();
            }
        }

        /// <summary>The host flies the mission and sends the result to everyone (a bonus for a team that worked together).</summary>
        public MissionResult Fly(IList<CelestialBody> bodies, bool easy)
        {
            if (!IsHost) return null;
            int seed = State.runs * 7919 + State.plan.instruments.Count * 31 + (int)(DateTime.Now.Ticks % 100000);
            var res = MissionSim.Simulate(State.plan, bodies, seed, easy);
            if (State.players.Count > 1 && res.launched)
            {
                int bonus = 0;
                if (State.players.All(q => q.changes > 0)) bonus += 40;                        // everyone had a hand in it
                if (State.requests.Count > 0 && State.requests.All(q => q.answer != 0)) bonus += 30;  // every request answered
                if (State.requests.Any(q => q.answer == 1)) bonus += 30;                       // and someone gave ground
                res.teamBonus = bonus;
                res.score += bonus;
            }
            res.team = string.Join(", ", State.players.Select(q => q.name));
            if (res.launched) State.RecordBest(State.plan.scenario, res.score, res.stars);
            State.resultJson = JsonUtility.ToJson(res);
            State.phase = 1;
            State.runs++;
            MarkDirty();
            return res;
        }

        /// <summary>The host sends something to every player straight away (the flight, many times a second).</summary>
        public void BroadcastRaw(NetMsg m)
        {
            if (!IsHost || Solo) return;
            string json = JsonUtility.ToJson(m);
            bool droppable = m.t == "fs";   // flight frames: if a player's connection is backed up, skip to the newest
            lock (conns) foreach (var c in conns) if (c.hello) c.Send(json, droppable);
        }

        List<IPAddress> broadcastTargets;

        /// <summary>The broadcast address of each local IPv4 network (e.g. 192.168.1.255).</summary>
        static List<IPAddress> DirectedBroadcasts()
        {
            var list = new List<IPAddress>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                        var a = ua.Address.GetAddressBytes();
                        var mk = ua.IPv4Mask.GetAddressBytes();
                        if (mk.Length != 4 || (mk[0] == 0 && mk[1] == 0)) continue;
                        var b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~mk[i]);
                        var ip = new IPAddress(b);
                        if (!list.Any(x => x.Equals(ip))) list.Add(ip);
                    }
                }
            }
            catch { }
            return list;
        }

        public void Touch() => MarkDirty();

        public void SetPhase(int phase)
        {
            if (!IsHost) return;
            State.phase = phase;
            if (phase == 0) foreach (var q in State.players) q.ready = false;
            MarkDirty();
        }

        // ---------------------------------------------------------------- the clients

        void ClientReceive(NetMsg m)
        {
            switch (m.t)
            {
                case "welcome": MyId = m.n; Connected = true; Status = "Joined"; break;
                case "refuse": Error = m.a; Connected = false; break;
                case "state":
                    try
                    {
                        var s = JsonUtility.FromJson<RoomState>(m.json);
                        if (s != null) { State = s; StateVersion++; RegisterLooks(); }
                    }
                    catch { }
                    break;
                default: OnClientMessage?.Invoke(m); break;
            }
        }

        /// <summary>Sends a change (or applies it straight away, for the host).</summary>
        public void Send(NetMsg m)
        {
            if (IsHost) { Apply(MyId, m); if (Solo) { dirty = true; } return; }
            hostConn?.Send(JsonUtility.ToJson(m));
        }

        public void Pick(string partId) => Send(new NetMsg { t = "pick", b = partId });
        public void Wings(int n) => Send(new NetMsg { t = "wings", n = n });
        public void Orbit(MOrbit o) => Send(new NetMsg { t = "orbit", n = (int)o });
        public void Rocket(string id) => Send(new NetMsg { t = "vehicle", b = id });
        public void Scenario(string id) => Send(new NetMsg { t = "scenario", b = id });
        public void Sandbox(string body, string tag, int years) => Send(new NetMsg { t = "sandbox", a = body, b = tag, n = years });
        public void LoadPlan(MissionPlan p) => Send(new NetMsg { t = "plan", json = JsonUtility.ToJson(p) });
        public void TakeRole(MRole r) => Send(new NetMsg { t = "take", n = (int)r });
        public void Ready(bool on) => Send(new NetMsg { t = "ready", on = on });
        public void Chat(string text) => Send(new NetMsg { t = "chat", a = text });
        public void Ask(MRole to, string text) => Send(new NetMsg { t = "ask", n = (int)to, a = text });
        public void Answer(int id, bool yes) => Send(new NetMsg { t = "answer", n = id, on = yes });

        // ---------------------------------------------------------------- finding rooms

        /// <summary>Listens for rooms announced on this network (call each frame while the join page is open).</summary>
        public void Listen()
        {
            if (finder == null)
            {
                try
                {
                    finder = new UdpClient();
                    finder.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    finder.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                }
                catch { finder = null; return; }
            }
            try
            {
                while (finder.Available > 0)
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    var data = finder.Receive(ref ep);
                    string text = Encoding.UTF8.GetString(data);
                    var heard = ParseBeacon(text);
                    if (heard == null) continue;
                    var info = Found.FirstOrDefault(x => x.code == heard.code);
                    if (info == null) Found.Add(info = heard);
                    else ParseBeacon(text, info);
                    info.host = ep.Address.ToString();
                    info.seen = Time.unscaledTime;
                }
            }
            catch { }
            Found.RemoveAll(x => Time.unscaledTime - x.seen > 7f);   // (4 s made rooms flicker on a busy Wi-Fi)
        }

        /// <summary>What the host announces each second: the room, its code, players, port, kind, campaign, and (milestone 31) its mission and best score there.</summary>
        public string BeaconText()
        {
            State.BestFor(State.plan.scenario, out int best, out int stars);
            return $"ARDENMD|{Proto}|{(State.room ?? "").Replace("|", " ")}|{State.code}|{State.players.Count}|{ListenPort}|{State.activity}|{(State.campaign ? 1 : 0)}|{State.plan.scenario}|{best}|{stars}";
        }

        /// <summary>Reads an announcement (null if it isn't one); older games send fewer fields.</summary>
        public static RoomInfo ParseBeacon(string text, RoomInfo into = null)
        {
            var f = (text ?? "").Split('|');
            if (f.Length < 6 || f[0] != "ARDENMD" || f[1] != Proto.ToString()) return null;
            var info = into ?? new RoomInfo();
            info.code = f[3];
            info.name = f[2];
            info.players = int.TryParse(f[4], out int n) ? n : 0;
            info.port = int.TryParse(f[5], out int pt) ? pt : Port;
            info.activity = f.Length > 6 && int.TryParse(f[6], out int act) ? act : 0;
            info.campaign = f.Length > 7 && f[7] == "1";
            info.scenario = f.Length > 8 ? f[8] : null;
            info.score = f.Length > 9 && int.TryParse(f[9], out int sc) ? sc : 0;
            info.stars = f.Length > 10 && int.TryParse(f[10], out int st) ? st : 0;
            return info;
        }

        /// <summary>Other mission design rooms on this network on the same mission (not free design), best first: the teams to beat.</summary>
        public IEnumerable<RoomInfo> Rivals()
        {
            string id = State.plan?.scenario;
            if (string.IsNullOrEmpty(id) || MissionDesign.Scenario(id).sandbox) return Enumerable.Empty<RoomInfo>();
            return Found.Where(f => f.activity == 0 && f.code != State.code && f.scenario == id).OrderByDescending(f => f.score).ToList();
        }

        // ---------------------------------------------------------------- room codes

        const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        /// <summary>This computer's address on the local network (the first that isn't loopback, preferring ones with a gateway).</summary>
        public static IPAddress LocalAddress()
        {
            try
            {
                IPAddress best = null;
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var props = ni.GetIPProperties();
                    bool gateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                    foreach (var ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ua.Address)) continue;
                        if (gateway) return ua.Address;
                        best ??= ua.Address;
                    }
                }
                if (best != null) return best;
            }
            catch { }
            return IPAddress.Loopback;
        }

        /// <summary>An address and port (0-9 past the usual one) as a code like "K7QM-2X4P".</summary>
        public static string CodeFor(IPAddress ip, int portOffset)
        {
            var b = ip.GetAddressBytes();
            ulong v = ((ulong)b[0] << 28) | ((ulong)b[1] << 20) | ((ulong)b[2] << 12) | ((ulong)b[3] << 4) | (ulong)(portOffset & 15);
            v ^= 0x5A5A5A5A5UL & 0xFFFFFFFFFUL;          // so neighbouring addresses don't look alike
            var sb = new StringBuilder();
            for (int i = 7; i >= 0; i--) { sb.Append(Alphabet[(int)((v >> (i * 5)) & 31)]); if (i == 4) sb.Append('-'); }
            return sb.ToString();
        }

        /// <summary>A room code (or a plain address like 192.168.1.20) back into an address and port.</summary>
        public static bool Decode(string code, out IPAddress ip, out int port)
        {
            ip = IPAddress.Loopback; port = Port;
            if (string.IsNullOrWhiteSpace(code)) return false;
            code = code.Trim();
            if (code.Contains("."))
            {
                var parts = code.Split(':');
                if (!IPAddress.TryParse(parts[0], out ip)) return false;
                if (parts.Length > 1 && int.TryParse(parts[1], out int p)) port = p;
                return true;
            }
            string s = code.ToUpperInvariant().Replace("-", "").Replace(" ", "").Replace("O", "0").Replace("I", "1");
            if (s.Length != 8) return false;
            ulong v = 0;
            foreach (char ch in s)
            {
                int k = Alphabet.IndexOf(ch);
                if (k < 0) return false;
                v = (v << 5) | (ulong)k;
            }
            v ^= 0x5A5A5A5A5UL & 0xFFFFFFFFFUL;
            byte[] bytes = { (byte)(v >> 28), (byte)(v >> 20), (byte)(v >> 12), (byte)(v >> 4) };
            ip = new IPAddress(bytes);
            port = Port + (int)(v & 15);
            return true;
        }
    }
}
