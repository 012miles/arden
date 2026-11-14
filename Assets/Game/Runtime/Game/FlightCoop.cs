using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    [Serializable]
    public class PilotInput { public float p, y, r, tx, ty, tz, thr, drive; }

    /// <summary>
    /// Milestone 30 in flight: a rocket room. On the host the flight runs as usual and is sent to everyone; the other
    /// players' controls and commands arrive and are carried out (after checking it's their job). On a player's device
    /// the flight isn't simulated: the ship is placed where the host says, and the player's own job (steering, staging,
    /// navigation, science) goes to the host.
    /// </summary>
    public partial class FlightMode
    {
        public bool remote;          // this device shows the host's flight
        bool coopExec;               // carrying out a command (or the game's own move): no job check
        float coopSendT, coopSnapT, coopInT, coopGoalT, nodeEditUntil;
        long coopSig;
        int coopSnap;
        double lastNodeUt;
        double lastNodeP, lastNodeN, lastNodeR;

        /// <summary>
        /// In a rocket room, before an action: true if it's handled (not this player's job, or sent to the host to do),
        /// false to go ahead here (single player, or the host doing their own job, or a command being carried out).
        /// </summary>
        bool CoopBlock(CAct act, string name, int n = 0, string b = null)
        {
            if (!Coop.Active || coopExec) return false;
            var c = Coop.Current;
            int role = Coop.RoleFor(act);
            if (!c.CanDo(act)) { Show($"That's the {Coop.RoleNames[role].ToLower()}'s job ({c.Holder(role)}).", 2.5f); return true; }
            if (remote) { c.room.Send(new NetMsg { t = "cmd", a = name, n = n, b = b }); return true; }
            return false;
        }

        void Internal(Action a)
        {
            bool was = coopExec;
            coopExec = true;
            try { a(); } finally { coopExec = was; }
        }

        // ---------------------------------------------------------------- the host

        void CoopHostTick(float dt)
        {
            if (!Coop.Active || !Coop.Current.IsHost || active == null) return;
            var c = Coop.Current;
            while (c.commands.Count > 0)
            {
                var (who, m) = c.commands.Dequeue();
                if (m.t == "in")
                {
                    try
                    {
                        var pi = JsonUtility.FromJson<PilotInput>(m.json);
                        c.inPitch = pi.p; c.inYaw = pi.y; c.inRoll = pi.r; c.inTranslate = new Vector3(pi.tx, pi.ty, pi.tz); c.inThrottle = pi.thr; c.inDrive = pi.drive; c.inTime = Time.unscaledTime;
                    }
                    catch { }
                }
                else if (m.t == "cmd") RunCoopCommand(c, who, m);
            }
            // the flight, to everyone
            coopSendT -= dt; coopSnapT -= dt;
            long sig = FlightRecording.SignatureOf(active);
            if (sig != coopSig || coopSnapT <= 0)
            {
                coopSig = sig; coopSnapT = 0.5f; coopSnap++;
                c.room.BroadcastRaw(new NetMsg { t = "snap", n = coopSnap, json = JsonUtility.ToJson(active.ToSave()) });
            }
            if (coopSendT <= 0) { coopSendT = 0.05f; c.room.BroadcastRaw(new NetMsg { t = "fs", json = JsonUtility.ToJson(FrameNow()) }); }
            // the goal
            if (active.hasLaunched) c.maxAltitude = Math.Max(c.maxAltitude, active.Altitude);
            coopGoalT -= dt;
            if (coopGoalT <= 0)
            {
                coopGoalT = 1f;
                var st = c.room.State;
                if (!st.goalDone && st.goal != "free" && Coop.GoalMet(st.goal, active, c.maxAltitude, ut))
                {
                    st.goalDone = true;
                    st.summary = $"Goal done: {Coop.Goal(st.goal).name}!";
                    c.room.Touch();
                    Show(st.summary, 5f, true);
                    audio?.Clunk(0.2f);
                }
            }
        }

        FlightFrame FrameNow()
        {
            var v = active;
            var f = new FlightFrame
            {
                ut = ut, px = v.pos.x, py = v.pos.y, pz = v.pos.z, vx = v.vel.x, vy = v.vel.y, vz = v.vel.z,
                qx = v.rot.x, qy = v.rot.y, qz = v.rot.z, qw = v.rot.w, wx = v.angVel.x, wy = v.angVel.y, wz = v.angVel.z,
                throttle = (float)v.throttle, sas = (int)v.sas, rcs = v.rcsOn, landed = v.landed, destroyed = v.destroyed,
                body = root.bodies.IndexOf(v.body), vessel = v.id, snap = coopSnap, warp = warp, rails = railsWarp,
                target = target != null ? target.name : null,
            };
            if (node != null) { f.hasNode = true; f.nodeUt = node.ut; f.nodePro = (float)node.prograde; f.nodeNor = (float)node.normal; f.nodeRad = (float)node.radial; }
            return f;
        }

        void RunCoopCommand(Coop c, int who, NetMsg m)
        {
            string name = m.a ?? "";
            CAct act = name == "stage" || name == "abort" ? CAct.Stage
                : name == "group" || name == "rcs" ? CAct.Systems
                : name == "sas" || name == "sastoggle" || name == "autopilot" ? CAct.Steer
                : name == "run" || name == "send" ? CAct.Science
                : CAct.Navigate;
            bool endByHost = name == "end";
            if (!c.SenderCan(who, act) && !endByHost) return;
            var p = c.room.State.Player(who);
            Internal(() =>
            {
                switch (name)
                {
                    case "stage": if (!TryCountdown()) Stage(); break;
                    case "abort": DoAbort(); break;
                    case "group": FireGroup(m.n); break;
                    case "rcs": ToggleRcs(); break;
                    case "sastoggle": ToggleSas(); break;
                    case "sas": SetSas((SasMode)m.n); break;
                    case "autopilot": if (autopilotOn) StopAutopilot(); else EnableAutopilot(); break;
                    case "warp": if (m.n > 0) ChangeWarp(1); else if (m.n < 0) ChangeWarp(-1); else { SetWarpOne(); warpTargetUt = null; } break;
                    case "node": if (double.TryParse(m.b, NumberStyles.Float, CultureInfo.InvariantCulture, out double t)) AddNode(t); break;
                    case "nodeset":
                    {
                        var f = (m.b ?? "").Split(';');
                        if (f.Length == 4 && double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double nu)
                            && double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double np)
                            && double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double nn)
                            && double.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double nr))
                        {
                            if (node == null) AddNode(nu, np, nn, nr);
                            else { node.ut = Math.Max(ut + 5, nu); node.prograde = np; node.normal = nn; node.radial = nr; node.Update(trajectory); }
                        }
                        break;
                    }
                    case "nodedel": if (node != null) DeleteNode(); break;
                    case "target":
                    {
                        var b = root.bodies.FirstOrDefault(x => x.name == m.b);
                        if (b != null) SetTarget(b); else if (target != null) SetTarget(target);
                        break;
                    }
                    case "run":
                    {
                        var part = active.FindPart(m.n);
                        var e = Science.Get(m.b);
                        if (part != null && e != null) RunExperiment(part, e);
                        break;
                    }
                    case "send":
                    {
                        var part = active.FindPart(m.n);
                        int k = int.TryParse(m.b, out int kk) ? kk : -1;
                        if (part != null && k >= 0 && k < part.science.Count) TransmitData(part, part.science[k]);
                        break;
                    }
                    case "end":
                        if (who == c.room.MyId || (p != null && p.Has((MRole)Coop.Control))) CoopEndFlight();
                        break;
                }
            });
        }

        /// <summary>The flight's over: everyone back to building (the host returns to the hangar; the players follow).</summary>
        public void CoopEndFlight()
        {
            if (!Coop.Active) return;
            var c = Coop.Current;
            if (!c.IsHost) { c.room.Send(new NetMsg { t = "cmd", a = "end" }); return; }
            var st = c.room.State;
            if (active != null)
                st.summary = $"Last flight: {(active.destroyed ? "lost" : active.landed ? "landed" : "still flying")}, highest {Ui.Dist(c.maxAltitude)}. " + (st.goal == "free" ? "" : st.goalDone ? $"Goal done: {Coop.Goal(st.goal).name}!" : $"Goal not reached: {Coop.Goal(st.goal).name}.");
            if (c.Campaign && CanRecover)
            {
                string got = U.career.Recover(active, ut, root.bodies);
                U.Remove(active);
                if (!string.IsNullOrEmpty(got)) st.summary += " " + got;
            }
            st.coopPhase = 0;
            foreach (var q in st.players) q.ready = false;
            c.room.Touch();
            root.EnterHangar();
        }

        /// <summary>Messages on the host's screen go to everyone's.</summary>
        void CoopSay(string s)
        {
            if (Coop.Active && Coop.Current.IsHost && root.inFlight) Coop.Current.room.BroadcastRaw(new NetMsg { t = "say", a = s });
        }

        // ---------------------------------------------------------------- a player's device

        /// <summary>Joins the host's flight: the ship from the host's copy, placed where the host says from now on.</summary>
        public void BeginRemote(VesselSave save)
        {
            ClearScene();
            design = null;
            remote = true;
            active = VesselSim.FromSave(save, root.bodies, ut);
            U.Add(active);
            StartFlight();
            camYaw = 180f;
            camPitch = 6f;
            camDist = Mathf.Clamp(active.Length * 1.5f + 10f, 15f, 150f);
            Show($"Flying together: {Coop.Current.MyJobs(true)}", 6f);
        }

        void CoopClientTick(float dt)
        {
            var c = Coop.Current;
            if (c == null) return;
            // a new copy of the ship: staging, chutes, fuel used
            if (c.snapId != c.snapUsed && c.snapJson != null)
            {
                c.snapUsed = c.snapId;
                try { ApplySnap(JsonUtility.FromJson<VesselSave>(c.snapJson)); } catch { }
            }
            if (c.Sample(out var f, out var pos, out var vel, out var rot, out double sut))
            {
                ut = sut; prevUt = ut;
                var b = root.bodies[Mathf.Clamp(f.body, 0, root.bodies.Count - 1)];
                if (active.body != b) active.body = b;
                active.pos = active.prevPos = pos;
                active.vel = vel;
                active.rot = active.prevRot = rot;
                active.angVel = new Vector3(f.wx, f.wy, f.wz);
                if (!c.CanDo(CAct.Steer)) active.throttle = f.throttle;
                active.sas = (SasMode)f.sas;
                active.rcsOn = f.rcs;
                active.landed = f.landed;
                warp = f.warp; railsWarp = f.rails;
                target = string.IsNullOrEmpty(f.target) ? null : root.bodies.FirstOrDefault(x => x.name == f.target);
                if (f.destroyed && !active.destroyed)
                {
                    active.destroyed = true;
                    active.destroyReason = "The rocket was destroyed.";
                    SpawnBoom(active.body, active.pos, Mathf.Clamp(active.Length * 2.5f, 8f, 40f));
                    if (views.TryGetValue(active, out var dv)) dv.SetVisible(false);
                }
                // the route: the host's maneuver node, unless mission control (this player) is changing it right now
                if (Time.unscaledTime > nodeEditUntil)
                {
                    if (f.hasNode)
                    {
                        if (node == null) AddNodeLocal(f.nodeUt, f.nodePro, f.nodeNor, f.nodeRad);
                        else { node.ut = f.nodeUt; node.prograde = f.nodePro; node.normal = f.nodeNor; node.radial = f.nodeRad; }
                        lastNodeUt = node.ut; lastNodeP = node.prograde; lastNodeN = node.normal; lastNodeR = node.radial;
                    }
                    else if (node != null) node = null;
                }
            }
            while (c.sayings.Count > 0) Show(c.sayings.Dequeue(), 3f);
            // this player's steering, to the host
            if (c.CanDo(CAct.Steer))
            {
                coopInT -= dt;
                if (coopInT <= 0)
                {
                    coopInT = 0.05f;
                    var pi = new PilotInput { p = active.inputPitch, y = active.inputYaw, r = active.inputRoll, tx = active.inputTranslate.x, ty = active.inputTranslate.y, tz = active.inputTranslate.z, thr = (float)active.throttle, drive = active.driveInput };
                    c.room.Send(new NetMsg { t = "in", json = JsonUtility.ToJson(pi) });
                }
            }
            // mission control changed the node here: send it
            if (node != null && c.CanDo(CAct.Navigate) && (Math.Abs(node.ut - lastNodeUt) > 0.01 || Math.Abs(node.prograde - lastNodeP) > 1e-3 || Math.Abs(node.normal - lastNodeN) > 1e-3 || Math.Abs(node.radial - lastNodeR) > 1e-3))
            {
                lastNodeUt = node.ut; lastNodeP = node.prograde; lastNodeN = node.normal; lastNodeR = node.radial;
                nodeEditUntil = Time.unscaledTime + 1.2f;
                c.room.Send(new NetMsg { t = "cmd", a = "nodeset", b = string.Join(";", new[] { node.ut, node.prograde, node.normal, node.radial }.Select(x => x.ToString("R", CultureInfo.InvariantCulture))) });
            }
        }

        void AddNodeLocal(double t, double p, double n, double r)
        {
            Internal(() => AddNode(t, p, n, r));
        }

        /// <summary>A new copy of the ship from the host: the same model if its parts haven't changed, a new one if they have.</summary>
        void ApplySnap(VesselSave save)
        {
            var v = VesselSim.FromSave(save, root.bodies, ut);
            v.pos = v.prevPos = active.pos; v.rot = v.prevRot = active.rot; v.vel = active.vel; v.throttle = active.throttle;
            bool same = v.id == active.id && v.parts.Count == active.parts.Count && v.parts.Select(p => p.index).SequenceEqual(active.parts.Select(p => p.index));
            views.TryGetValue(active, out var oldView);
            views.Remove(active);
            U.Remove(active);
            if (same && oldView != null) views[v] = oldView;
            else { oldView?.Destroy(); views[v] = VesselView.Create(v); }
            if (active.destroyed) { v.destroyed = true; views[v].SetVisible(false); }
            U.Add(v);
            active = v;
        }

        // ---------------------------------------------------------------- the panels

        void DrawCoopFlight(float W, float H)
        {
            var c = Coop.Current;
            float tw = 330;
            var tr = new Rect(W - tw - 10, H * 0.5f + 40, tw, 30 + c.room.State.players.Count * 20 + 78);
            if (dockMine != null) tr.y = H - 470;     // out of the docking guide's way
            c.DrawTeam(tr, true);
            var st = c.room.State;
            var gr = new Rect(tr.x, tr.yMax + 4, tw, 50);
            Ui.Box(gr);
            GUI.Label(new Rect(gr.x + 10, gr.y + 4, tw - 20, 20), $"Goal: <b>{Coop.Goal(st.goal).name}</b> {(st.goalDone ? "<color=#7dff8a>✓ done!</color>" : "")}", Ui.Small);
            if (c.Has(Coop.Control) || c.IsHost)
            {
                if (GUI.Button(new Rect(gr.x + 10, gr.y + 24, 150, 22), "End flight for all", Ui.ButtonSmall)) CoopEndFlight();
                if (c.Has(Coop.Control) && GUI.Button(new Rect(gr.x + 168, gr.y + 24, 150, 22), node == null ? "Add maneuver" : "Delete maneuver", Ui.ButtonSmall))
                {
                    if (node == null) AddNodeAtNextApsis(); else DeleteNode();
                }
            }
            c.DrawChat(new Rect(10, H - 330, 420, 88));
        }

        /// <summary>The pause menu in a rocket room: resume, end the flight (host or mission control), leave the room.</summary>
        void DrawCoopPause(float W, float H)
        {
            var c = Coop.Current;
            var r = new Rect(W / 2 - 190, H / 2 - 130, 380, active.destroyed ? 230 : 200);
            Ui.Box(r); Ui.Box(r);
            GUI.Label(new Rect(r.x, r.y + 12, r.width, 30), active.destroyed ? "The rocket was lost" : "Paused (the flight goes on)", Ui.BigCenter);
            float y = r.y + 56;
            if (!active.destroyed && GUI.Button(new Rect(r.x + 40, y, r.width - 80, 30), "Resume", Ui.Button)) paused = false;
            if (active.destroyed) y -= 0; else y += 36;
            bool canEnd = c.IsHost || c.Has(Coop.Control);
            GUI.enabled = canEnd;
            if (GUI.Button(new Rect(r.x + 40, y, r.width - 80, 30), canEnd ? "Back to building (everyone)" : "The host or mission control ends the flight", Ui.Button)) { paused = false; CoopEndFlight(); return; }
            GUI.enabled = true;
            y += 36;
            if (GUI.Button(new Rect(r.x + 40, y, r.width - 80, 30), "Leave the room", Ui.Button)) { paused = false; root.LeaveCoop(); return; }
        }
    }
}
