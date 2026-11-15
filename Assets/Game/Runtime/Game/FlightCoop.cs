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
        bool rolloutSkippedHere;     // milestone 41: this player skipped the host's roll-out
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
                else if (m.t == "wk") HostWalkInput(who, m);   // milestone 43
                // a command can end the flight (mission control's "end"): stop here, the ship's being taken apart
                if (!root.inFlight || active?.parts == null) return;
            }
            if (active?.parts == null) return;
            // the flight, to everyone (milestone 43: the room's ship, even while the host's out walking; and every walker)
            var ship = CoopShip;
            if (ship?.parts == null) return;
            coopSendT -= dt; coopSnapT -= dt;
            long sig = FlightRecording.SignatureOf(ship);
            if (sig != coopSig || coopSnapT <= 0)
            {
                coopSig = sig; coopSnapT = 0.5f; coopSnap++;
                string snap = null;
                try { snap = JsonUtility.ToJson(ship.ToSave()); } catch (Exception e) { Debug.LogWarning("Couldn't send the flight: " + e.Message); }
                if (snap != null) c.room.BroadcastRaw(new NetMsg { t = "snap", n = coopSnap, json = snap });
            }
            var walkers = WalkersNow();
            HostSendWalkerSaves(c, walkers, dt);
            if (coopSendT <= 0) { coopSendT = 0.05f; var fr = FrameNow(); fr.walk = walkers; c.room.BroadcastRaw(new NetMsg { t = "fs", json = JsonUtility.ToJson(fr) }); }
            // the goal
            if (ship.hasLaunched) c.maxAltitude = Math.Max(c.maxAltitude, ship.Altitude);
            ExpeditionTick(dt);   // milestone 34
            coopGoalT -= dt;
            if (coopGoalT <= 0)
            {
                coopGoalT = 1f;
                var st = c.room.State;
                if (!c.Expedition && !st.goalDone && st.goal != "free" && Coop.GoalMet(st.goal, ship, c.maxAltitude, ut))
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
            var v = CoopShip ?? active;   // (milestone 43: the room's ship)
            var f = new FlightFrame
            {
                ut = ut, px = v.pos.x, py = v.pos.y, pz = v.pos.z, vx = v.vel.x, vy = v.vel.y, vz = v.vel.z,
                qx = v.rot.x, qy = v.rot.y, qz = v.rot.z, qw = v.rot.w, wx = v.angVel.x, wy = v.angVel.y, wz = v.angVel.z,
                throttle = (float)v.throttle, sas = (int)v.sas, rcs = v.rcsOn, landed = v.landed, destroyed = v.destroyed,
                body = root.bodies.IndexOf(v.body), vessel = v.id, snap = coopSnap, warp = warp, rails = railsWarp,
                target = target != null ? target.name : null,
                roll = rollout != null ? rollout.t + 1f : 0f, rollPad = rollout != null ? rollout.pad : 0,   // milestone 41
                gx = v.feltLocal.x, gy = v.feltLocal.y, gz = v.feltLocal.z,                                  // milestone 43
            };
            if (node != null) { f.hasNode = true; f.nodeUt = node.ut; f.nodePro = (float)node.prograde; f.nodeNor = (float)node.normal; f.nodeRad = (float)node.radial; }
            return f;
        }

        void RunCoopCommand(Coop c, int who, NetMsg m)
        {
            string name = m.a ?? "";
            CAct act = name == "stage" || name == "abort" ? CAct.Stage
                : name == "group" || name == "rcs" || name == "scav" ? CAct.Systems
                : name == "sas" || name == "sastoggle" || name == "autopilot" ? CAct.Steer
                : name == "run" || name == "send" ? CAct.Science
                : CAct.Navigate;
            bool endByHost = name == "end";
            bool anyone = name == "scav" && c.Expedition || name == "item" || name == "eva" || name == "board";   // in an expedition anyone can search an outpost; anyone uses their own things, climbs out and back in
            if (!c.SenderCan(who, act) && !endByHost && !anyone) return;
            var p = c.room.State.Player(who);
            var shipNow = CoopShip;
            var wasActive = active;
            bool onShip = shipNow != null && shipNow != active && name != "item" && name != "eva" && name != "board";
            Internal(() =>
            {
                if (onShip) active = shipNow;   // milestone 43: the host's out walking: the job's done on the ship
                try
                {
                switch (name)
                {
                    case "eva": HostGoEva(p?.name); break;      // milestone 43: a player climbs out
                    case "board": HostBoard(p?.name); break;    // and back in
                    case "stage": if (!TryCountdown()) Stage(); break;
                    case "abort": DoAbort(); break;
                    case "group": FireGroup(m.n); break;
                    case "rcs": ToggleRcs(); break;
                    case "scav": DoScavenge(); break;
                    case "item": RunItemCommand(who, m); break;   // milestone 38
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
                }
                finally { if (onShip && active == shipNow) active = wasActive; }
            });
        }

        /// <summary>The flight's over: everyone back to building (the host returns to the hangar; the players follow).</summary>
        public void CoopEndFlight()
        {
            if (!Coop.Active) return;
            var c = Coop.Current;
            if (!c.IsHost) { c.room.Send(new NetMsg { t = "cmd", a = "end" }); return; }
            var st = c.room.State;
            if (c.Expedition && active != null) FinishExpedition(c);   // milestone 34: the report and the leaderboard
            else if (active != null)
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
            rolloutSkippedHere = false;
            ClearWalkers();   // milestone 43
            active = VesselSim.FromSave(save, root.bodies, ut);
            shipLocal = active;
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
            if (shipLocal == null) shipLocal = active;
            if (c.snapId != c.snapUsed && c.snapJson != null)
            {
                c.snapUsed = c.snapId;
                try { ApplySnap(JsonUtility.FromJson<VesselSave>(c.snapJson)); } catch { }
            }
            if (c.walkSnapJson != null) { ClientWalkerSaves(c.walkSnapJson); c.walkSnapJson = null; }   // milestone 43
            var sh = shipLocal;
            if (c.Sample(out var f, out var pos, out var vel, out var rot, out double sut))
            {
                ut = sut; prevUt = ut;
                var b = root.bodies[Mathf.Clamp(f.body, 0, root.bodies.Count - 1)];
                if (sh.body != b) sh.body = b;
                sh.pos = sh.prevPos = pos;
                sh.vel = vel;
                sh.rot = sh.prevRot = rot;
                sh.angVel = new Vector3(f.wx, f.wy, f.wz);
                if (!c.CanDo(CAct.Steer)) sh.throttle = f.throttle;
                sh.sas = (SasMode)f.sas;
                sh.rcsOn = f.rcs;
                sh.landed = f.landed;
                ClientWalkers(f.walk);   // milestone 43: every player's Ardling (this player's own is what they control)
                warp = f.warp; railsWarp = f.rails;
                target = string.IsNullOrEmpty(f.target) ? null : root.bodies.FirstOrDefault(x => x.name == f.target);
                if (active == sh) FollowHostRollout(f);   // milestone 41: everyone sees the roll-out to the pad, not just the host
                if (f.gx != 0 || f.gy != 0 || f.gz != 0) sh.feltLocal = new Vector3(f.gx, f.gy, f.gz);   // milestone 43: the cabin feels what the host's ship does
                if (f.destroyed && !sh.destroyed)
                {
                    sh.destroyed = true;
                    sh.destroyReason = "The rocket was destroyed.";
                    SpawnBoom(sh.body, sh.pos, Mathf.Clamp(sh.Length * 2.5f, 8f, 40f));
                    Shatter(sh, Mathf.Clamp((float)sh.SurfaceRelVelocity.magnitude / 40f, 0.6f, 2f));   // milestone 35
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
            // how it looks here: flames, smoke, legs, chutes, reentry (the host's game runs the real thing)
            shipLocal?.VisualStep(dt);
            ClientSendWalk(dt);   // milestone 43: out walking: this player's keys, to the host
            // this player's steering, to the host (not while they're out walking)
            if (c.CanDo(CAct.Steer) && active == shipLocal)
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

        /// <summary>
        /// Milestone 41 (captaincarl24: "when the host has the moving rocket to pad animation other people dont"): the host's
        /// roll-out comes in with each frame, and this device rolls its own crawler out alongside, kept to the host's time.
        /// SPACE skips it here only (the host's carries on).
        /// </summary>
        void FollowHostRollout(FlightFrame f)
        {
            if (f.roll > 0f && !rolloutSkippedHere && !active.hasLaunched && !active.destroyed)
            {
                if (rollout == null && active.body == Home) StartRollout(Mathf.Clamp(f.rollPad, 0, LaunchSite.Pads.Length - 1));
                if (rollout == null) return;
                float want = f.roll - 1f;
                if (Mathf.Abs(rollout.t - want) > 0.4f)
                {
                    if (rollout.t < LegA && want >= LegA) { camYaw = RoadBearing() + 75f; camPitch = 7f; }   // (the second leg's camera)
                    rollout.t = Mathf.Min(want, LegA + LegB - 0.05f);
                }
            }
            else if (f.roll <= 0f && rollout != null) EndRollout();
        }

        void AddNodeLocal(double t, double p, double n, double r)
        {
            Internal(() => AddNode(t, p, n, r));
        }

        /// <summary>A new copy of the ship from the host: the same model if its parts haven't changed, a new one if they have.</summary>
        void ApplySnap(VesselSave save)
        {
            var old = shipLocal ?? active;
            var v = VesselSim.FromSave(save, root.bodies, ut);
            v.pos = v.prevPos = old.pos; v.rot = v.prevRot = old.rot; v.vel = old.vel; v.throttle = old.throttle; v.feltLocal = old.feltLocal;
            bool same = v.id == old.id && v.parts.Count == old.parts.Count && v.parts.Select(p => p.index).SequenceEqual(old.parts.Select(p => p.index));
            views.TryGetValue(old, out var oldView);
            views.Remove(old);
            U.Remove(old);
            if (same && oldView != null) views[v] = oldView;
            else { oldView?.Destroy(); views[v] = VesselView.Create(v); }
            if (old.destroyed) { v.destroyed = true; views[v].SetVisible(false); }
            U.Add(v);
            if (active == old) active = v;   // (milestone 43: while this player's out walking, they stay on their Ardling)
            shipLocal = v;
        }

        // ---------------------------------------------------------------- the panels

        void DrawCoopFlight(float W, float H)
        {
            var c = Coop.Current;
            float tw = 330;
            if (Coop.Collapsed)
            {
                // folded away: one bar with the goal and the latest chat (milestone 34)
                var st0 = c.room.State;
                var br = new Rect(W - 430, H - 300, 420, 40);
                c.DrawCollapsedBar(br);
                Ui.Text(new Rect(br.x, br.y - 22, br.width, 20), $"Goal: <b>{Coop.Goal(st0.goal).name}</b> {(st0.goalDone ? "<color=#7dff8a>✓ done!</color>" : "")}", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight });
                return;
            }
            var tr = new Rect(W - tw - 10, H * 0.5f + 40, tw, 30 + c.room.State.players.Count * 20 + 78);
            if (dockMine != null) tr.y = H - 470;     // out of the docking guide's way
            c.DrawTeam(tr, true);
            var st = c.room.State;
            if (c.Expedition)
            {
                var er = new Rect(tr.x, tr.yMax + 4, tw, ExpeditionPanelHeight);
                if (er.yMax + 30 > H - 330) er.y = tr.y - er.height - 34;
                DrawExpeditionPanel(er);
                if ((c.Has(Coop.Control) || c.IsHost) && Ui.Press(new Rect(er.xMax - 150, er.yMax + 4, 150, 24), "End expedition", Ui.ButtonStopSmall)) CoopEndFlight();
                c.DrawChat(new Rect(10, H - 330, 420, 88));
                return;
            }
            var gr = new Rect(tr.x, tr.yMax + 4, tw, 50);
            Ui.Box(gr);
            Ui.Text(new Rect(gr.x + 10, gr.y + 4, tw - 20, 20), $"Goal: <b>{Coop.Goal(st.goal).name}</b> {(st.goalDone ? "<color=#7dff8a>✓ done!</color>" : "")}", Ui.Small);
            if (c.Has(Coop.Control) || c.IsHost)
            {
                if (Ui.Press(new Rect(gr.x + 10, gr.y + 24, 150, 22), "End flight for all", Ui.ButtonStopSmall)) CoopEndFlight();
                if (c.Has(Coop.Control) && Ui.Press(new Rect(gr.x + 168, gr.y + 24, 150, 22), node == null ? "Add maneuver" : "Delete maneuver", Ui.ButtonSmall))
                {
                    if (node == null) AddNodeAtNextApsis(); else DeleteNode();
                }
            }
            c.DrawChat(new Rect(10, H - 330, 420, 88));
        }

        /// <summary>The pause menu in a rocket room: resume, end the flight (host or mission control), leave the room.</summary>
        void DrawCoopPause(float W, float H, float atY = -1)
        {
            var c = Coop.Current;
            float ph = active.destroyed ? 266 : 272;   // (milestone 43: + PvP)
            var r = new Rect(W / 2 - 190, atY >= 0 ? Mathf.Min(atY, H - 10 - ph) : H / 2 - 130, 380, ph);
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x, r.y + 12, r.width, 30), active.destroyed ? "The rocket was lost" : "Paused (the flight goes on)", Ui.BigCenter);
            float y = r.y + 56;
            if (!active.destroyed && Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), "Resume", Ui.Button)) paused = false;
            if (active.destroyed) y -= 0; else y += 36;
            bool canEnd = c.IsHost || c.Has(Coop.Control);
            GUI.enabled = canEnd;
            if (Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), canEnd ? "Back to building (everyone)" : "The host or mission control ends the flight", Ui.Button)) { paused = false; CoopEndFlight(); return; }
            GUI.enabled = true;
            y += 36;
            if (Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), $"Leave the room (main menu, {In.Name(K.F10)})", Ui.Button)) { paused = false; root.LeaveCoop(); return; }
            y += 36;
            float hw = (r.width - 88) / 2;
            if (Ui.Press(new Rect(r.x + 40, y, hw, 30), $"Hide UI ({In.Name(K.F1)})", Ui.Button)) { paused = false; Ui.ToggleHidden(); return; }
            if (Ui.Press(new Rect(r.x + 48 + hw, y, hw, 30), "Definitions", Ui.Button)) Glossary.Open = true;
            // milestone 43: PvP (the host decides)
            y += 36;
            var st = c.room.State;
            GUI.enabled = c.IsHost;
            if (Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), st.pvp ? "PvP: ON (weapons hurt players)" : (c.IsHost ? "PvP: off (switch on)" : "PvP: off (the host decides)"), st.pvp ? Ui.ButtonStopSmall : Ui.Button))
            { st.pvp = !st.pvp; c.room.Touch(); CoopSay(st.pvp ? "PvP is on: players' weapons hurt each other." : "PvP is off."); }
            GUI.enabled = true;
        }
    }
}
