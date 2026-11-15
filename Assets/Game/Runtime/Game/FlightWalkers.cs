using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>A player's walking keys, to the host (milestone 43).</summary>
    [Serializable] public class WalkInput { public float p, y, s, r, tx, ty, tz, look; public bool run, crouch; public int jumps; }

    /// <summary>Where one walker is, from the host, twenty times a second (milestone 43). Body-fixed position and turn.</summary>
    [Serializable] public class WalkerState { public string n; public int b; public double x, y, z; public float qx, qy, qz, qw, p, s, look; public bool l, run, crouch; }

    /// <summary>Full copies of the walkers, so a player's game can make them (milestone 43).</summary>
    [Serializable] public class WalkerSaves { public List<VesselSave> v = new List<VesselSave>(); }

    /// <summary>
    /// Milestone 43 (captaincarl24: "make it so you can also spacewalk in multiplayer", "when host leaves i control the host
    /// like flight engineer stuff super buggy"): in a room the ship stays the ship and every player walks and spacewalks as
    /// their own Ardling.
    ///
    /// The host's game is still the one that runs everything. When a player climbs out (ENTER, or EVA on the crew panel),
    /// their game asks the host, which puts that player's Ardling out of the hatch. From then on their game sends their
    /// walking keys to the host twenty times a second; the host moves their Ardling with the rest of the flight and sends
    /// every walker's place back with the ship. Their camera and keys follow their own Ardling; B at a hatch climbs back in.
    /// The ship the room flies is the one they launched, whoever's walking about: the pilot's steering, the flight engineer's
    /// staging and mission control's plans all go to the ship, even while the host is out on a spacewalk (they used to
    /// steer the host's Ardling instead).
    /// </summary>
    public partial class FlightMode
    {
        // ---------------------------------------------------------------- the host

        int coopShipId = -1;
        readonly Dictionary<string, (WalkInput inp, float t, int jumps)> walkIn = new Dictionary<string, (WalkInput, float, int)>();
        float walkSnapT;
        string walkSig;

        /// <summary>The room's ship (on the host): what the host flies, or the ship they climbed out of while they're walking.</summary>
        VesselSim CoopShip
        {
            get
            {
                if (active == null) return null;
                if (!active.IsEva) { coopShipId = active.id; return active; }
                return U.vessels.Find(v => v.id == coopShipId && !v.destroyed && !v.IsEva) ?? active;
            }
        }

        /// <summary>A player's walking keys arrive.</summary>
        void HostWalkInput(int who, NetMsg m)
        {
            var p = Coop.Current.room.State.Player(who);
            if (p == null) return;
            try
            {
                var wi = JsonUtility.FromJson<WalkInput>(m.json);
                walkIn.TryGetValue(p.name, out var old);
                walkIn[p.name] = (wi, Time.unscaledTime, old.inp == null ? wi.jumps : old.jumps);
            }
            catch { }
        }

        /// <summary>Every frame on the host, before the physics: each player's Ardling gets their keys; the ship gets the pilot's while the host walks.</summary>
        void HostDriveWalkers()
        {
            if (!Coop.Active || remote || !Coop.Current.IsHost || active == null) return;
            var c = Coop.Current;
            string me = c.room.Me?.name;
            foreach (var name in walkIn.Keys.ToList())
            {
                if (name == me) continue;
                var eva = EvaOf(name);
                if (eva == null || eva == active) continue;
                var (wi, t, seen) = walkIn[name];
                bool fresh = Time.unscaledTime - t < 1.5f;
                eva.inputPitch = fresh ? wi.p : 0; eva.inputYaw = fresh ? wi.y : 0; eva.evaStrafe = fresh ? wi.s : 0; eva.inputRoll = fresh ? wi.r : 0;
                eva.inputTranslate = fresh ? new Vector3(wi.tx, wi.ty, wi.tz) : Vector3.zero;
                eva.evaRun = fresh && wi.run; eva.evaCrouch = wi.crouch; eva.evaLookYaw = wi.look; eva.rcsOn = true;
                if (wi.jumps != seen) { eva.evaJump = true; walkIn[name] = (wi, t, wi.jumps); }
            }
            // the host is out walking: the ship still answers to the pilot (another player), and holds still otherwise
            if (active.IsEva)
            {
                var ship = CoopShip;
                if (ship != null && ship != active && !ship.destroyed)
                {
                    bool fresh = Time.unscaledTime - c.inTime < 1.5f && !c.CanDo(CAct.Steer);
                    ship.inputPitch = fresh ? c.inPitch : 0; ship.inputYaw = fresh ? c.inYaw : 0; ship.inputRoll = fresh ? c.inRoll : 0;
                    ship.inputTranslate = fresh ? c.inTranslate : Vector3.zero; ship.driveInput = fresh ? c.inDrive : 0;
                    if (fresh && c.inThrottle >= 0) ship.throttle = c.inThrottle;
                }
            }
        }

        /// <summary>A player climbs out of their seat (on the host).</summary>
        void HostGoEva(string who)
        {
            var ship = CoopShip;
            if (ship == null || ship.IsEva || string.IsNullOrEmpty(who) || EvaOf(who) != null) return;
            var part = ship.parts.FirstOrDefault(p => p.crew.Contains(who));
            if (part == null) { TellPlayer(who, "You don't have a seat aboard this ship, so you can't climb out of it.", 3f); return; }
            var eva = ship.GoEva(who, ut, out string why);
            if (eva == null) { TellPlayer(who, why, 3f); return; }
            U.Add(eva);
            views[eva] = VesselView.Create(eva);
            var m = U.career.roster.Find(x => x.name == who);
            if (m != null) m.vesselId = eva.id;
            TellPlayer(who, eva.landed ? $"{who} is out. W/S walk, A/D turn, CTRL run, SPACE jump, B climbs back in."
                                       : $"{who} is out on a spacewalk. W/S A/D and SHIFT/CTRL fly the jetpack, B climbs back in.", 5f);
            audio?.Clunk(0.3f);
        }

        /// <summary>A player's Ardling climbs back into the nearest hatch with a free seat (on the host).</summary>
        void HostBoard(string who)
        {
            var eva = EvaOf(who);
            if (eva == null || eva == active) return;
            VesselSim best = null;
            double bd = double.MaxValue;
            foreach (var u in U.vessels)
            {
                var p = eva.BoardingPart(u);
                if (p == null) continue;
                double d = eva.DistanceToPart(u, p);
                if (d < bd) { bd = d; best = u; }
            }
            if (best == null) { TellPlayer(who, "No hatch with a free seat within 3 m.", 2.5f); return; }
            if (!eva.Board(best, out string msg)) { TellPlayer(who, msg, 2.5f); return; }
            var m = U.career.roster.Find(x => x.name == who);
            if (m != null) m.vesselId = best.id;
            U.Remove(eva);
            RemoveView(eva);
            loaded.Remove(eva);
            if (targetVessel == eva) targetVessel = null;
            walkIn.Remove(who);
            TellPlayer(who, msg, 3f);
        }

        /// <summary>Every walker near the ship (on the host), for the frames.</summary>
        List<WalkerState> WalkersNow()
        {
            var list = new List<WalkerState>();
            var ship = CoopShip;
            if (ship == null) return list;
            foreach (var v in U.vessels)
            {
                if (v.destroyed || !v.IsEva || v.EvaName == null || v.body != ship.body) continue;
                if (Vector3d.Distance(v.pos, ship.pos) > 5000) continue;
                var fx = v.body.InertialToFixed(v.pos, ut);
                var q = Quaternion.Inverse(v.body.RotationAt(ut)) * v.rot;
                list.Add(new WalkerState { n = v.EvaName, b = root.bodies.IndexOf(v.body), x = fx.x, y = fx.y, z = fx.z, qx = q.x, qy = q.y, qz = q.z, qw = q.w,
                    p = v.inputPitch, s = v.evaStrafe, look = v.evaLookYaw, l = v.landed, run = v.evaRun, crouch = v.evaCrouch });
            }
            return list;
        }

        /// <summary>Full copies of the walkers when who's out changes (and every two seconds), so players' games can make them.</summary>
        void HostSendWalkerSaves(Coop c, List<WalkerState> now, float dt)
        {
            string sig = string.Join(",", now.Select(w => w.n));
            walkSnapT -= dt;
            if (sig == walkSig && walkSnapT > 0) return;
            walkSig = sig;
            walkSnapT = 2f;
            var ws = new WalkerSaves();
            foreach (var w in now) { var v = EvaOf(w.n); if (v == null) continue; try { ws.v.Add(v.ToSave()); } catch { } }
            c.room.BroadcastRaw(new NetMsg { t = "wsnap", json = JsonUtility.ToJson(ws) });
        }

        // ---------------------------------------------------------------- a player's device

        VesselSim shipLocal;   // the ship, on a player's device (what they control while aboard)
        readonly Dictionary<string, VesselSim> walkersLocal = new Dictionary<string, VesselSim>();
        float walkSendT;
        int myJumps;

        /// <summary>The room's ship as this device knows it.</summary>
        VesselSim ShipHere => remote ? (shipLocal ?? active) : CoopShip;

        /// <summary>The walkers' full copies arrive: any new one is made here.</summary>
        void ClientWalkerSaves(string json)
        {
            WalkerSaves ws;
            try { ws = JsonUtility.FromJson<WalkerSaves>(json); } catch { return; }
            if (ws?.v == null) return;
            foreach (var s in ws.v)
            {
                VesselSim sim;
                try { sim = VesselSim.FromSave(s, root.bodies, ut); } catch { continue; }
                string n = sim.EvaName;
                if (n == null) continue;
                if (walkersLocal.TryGetValue(n, out var have) && U.vessels.Contains(have)) continue;
                U.Add(sim);
                views[sim] = VesselView.Create(sim);
                walkersLocal[n] = sim;
            }
        }

        /// <summary>Each frame from the host: where every walker is; gone ones go; this player's own is what they control.</summary>
        void ClientWalkers(List<WalkerState> list)
        {
            var seen = new HashSet<string>();
            if (list != null)
                foreach (var w in list)
                {
                    if (w == null || w.n == null || !walkersLocal.TryGetValue(w.n, out var v)) continue;   // (its copy comes in a moment)
                    seen.Add(w.n);
                    var b = root.bodies[Mathf.Clamp(w.b, 0, root.bodies.Count - 1)];
                    if (v.body != b) v.body = b;
                    v.pos = v.prevPos = b.FixedToInertial(new Vector3d(w.x, w.y, w.z), ut);
                    v.rot = v.prevRot = b.RotationAt(ut) * new Quaternion(w.qx, w.qy, w.qz, w.qw);
                    v.vel = b.SurfaceVelocity(v.pos);
                    v.landed = w.l;
                    if (v != active) { v.inputPitch = w.p; v.evaStrafe = w.s; v.evaRun = w.run; v.evaCrouch = w.crouch; v.evaLookYaw = w.look; }
                }
            foreach (var n in walkersLocal.Keys.ToList())
            {
                if (seen.Contains(n)) continue;
                var v = walkersLocal[n];
                walkersLocal.Remove(n);
                if (v == active) BackToShipHere();
                U.Remove(v);
                RemoveView(v);
            }
            string me = MyArdlingName;
            if (me != null && walkersLocal.TryGetValue(me, out var mine) && active != mine)
            {
                if (!active.IsEva) shipLocal = active;
                active = mine;
                camDist = 6f; camPitch = 12f;
                if (inside) LeaveInterior();
            }
        }

        void BackToShipHere()
        {
            if (shipLocal == null) return;
            active = shipLocal;
            camDist = Mathf.Clamp(active.Length * 1.5f + 10f, 15f, 150f);
        }

        /// <summary>This player's walking keys, to the host (twenty times a second while out).</summary>
        void ClientSendWalk(float dt)
        {
            var c = Coop.Current;
            string me = MyArdlingName;
            if (c == null || active == null || !active.IsEva || active.EvaName != me) return;
            if (active.evaJump) { myJumps++; active.evaJump = false; }
            walkSendT -= dt;
            if (walkSendT > 0) return;
            walkSendT = 0.05f;
            var wi = new WalkInput { p = active.inputPitch, y = active.inputYaw, s = active.evaStrafe, r = active.inputRoll, tx = active.inputTranslate.x, ty = active.inputTranslate.y,
                tz = active.inputTranslate.z, look = active.evaLookYaw, run = active.evaRun, crouch = active.evaCrouch, jumps = myJumps };
            c.room.Send(new NetMsg { t = "wk", json = JsonUtility.ToJson(wi) });
        }

        void ClearWalkers()
        {
            walkIn.Clear();
            foreach (var v in walkersLocal.Values) { U.Remove(v); RemoveView(v); }
            walkersLocal.Clear();
            shipLocal = null;
            coopShipId = -1;
            walkSig = null;
        }

        // ---------------------------------------------------------------- for tours

        public int TestWalkers => remote ? walkersLocal.Count : WalkersNow().Count;
        public string TestShipName => ShipHere?.name;
    }
}
