using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 38, the survival kit in flight. Each Ardling's health, hunger and stamina tick along (on foot the stomach
    /// empties; aboard, the crew eat from the ship); falls, heavy g and a burning cabin hurt; running uses stamina; Z
    /// crouches. Knocked out, an Ardling has 90 seconds: a revive kit (yours is used by itself) or a teammate with one gets
    /// them up, or they're lost. Things lie about on the worlds (searching an outpost or wreck turns some up): on foot
    /// F picks one up or puts it down, R throws it, I opens the backpack (keep it there: it stays between flights), and
    /// 1-8 use what's in it: food, medicine, fuel and batteries for a ship in reach, a repair kit, the scanner, a torch.
    /// In a room the host keeps everyone's: players' actions go to the host, and the host sends back how they're doing.
    /// </summary>
    public partial class FlightMode
    {
        double survivalUt = -1;
        float survivalSync, fallVs;
        bool fallWasLanded = true;
        int fallFor = -1;
        public bool packOpen;
        float scanUntil;
        bool torchOn;
        Light torchLight;
        readonly Dictionary<string, string> carried = new Dictionary<string, string>();   // who → what they're holding (single player / the host)
        readonly Dictionary<int, GameObject> itemViews = new Dictionary<int, GameObject>();
        GameObject carriedView;
        string carriedViewId;
        static readonly Dictionary<string, Mesh> itemMeshes = new Dictionary<string, Mesh>();

        // ---------------------------------------------------------------- whose, and where

        /// <summary>The Ardling this player is: their own in a room; on foot the one walking; inside, the cabin's first.</summary>
        string MyArdlingName
        {
            get
            {
                if (Coop.Active) return Coop.Current.room.Me?.name;
                if (active == null) return null;
                if (active.IsEva) return active.EvaName;
                if (inside && insidePart != null && insidePart.crew.Count > 0) return insidePart.crew[0];
                return active.parts.SelectMany(p => p.crew).FirstOrDefault();
            }
        }

        bool SurvivalHere => active != null && !(Coop.Active && remote);   // this device runs the survival (single player, the host)

        Vitals VitalsFor(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Coop.Active && remote) return Coop.Current.room.State.vitals.Find(v => v.name == name);
            return U.career.VitalsOf(name);
        }

        NetPack RoomPack(string who)
        {
            var st = Coop.Current.room.State;
            var p = st.packs.Find(x => x.name == who);
            if (p == null && !remote) { p = new NetPack { name = who, items = new List<string> { "ration", "bandage" } }; st.packs.Add(p); }
            return p;
        }

        /// <summary>A player's backpack (the career's in single player; the host's copy in a room).</summary>
        List<string> PackOf(string who, out int size)
        {
            if (Coop.Active) { var p = RoomPack(who); size = p?.size ?? 4; return p?.items ?? new List<string>(); }
            size = U.career.backpackSize;
            return U.career.backpack;
        }

        string CarriedBy(string who)
        {
            if (string.IsNullOrEmpty(who)) return null;
            if (Coop.Active) return RoomPack(who)?.carried;
            return carried.TryGetValue(who, out var id) ? id : null;
        }

        void SetCarried(string who, string id)
        {
            if (Coop.Active) { var p = RoomPack(who); if (p != null) p.carried = id; Coop.Current.room.Touch(); return; }
            if (id == null) carried.Remove(who); else carried[who] = id;
        }

        bool PackAdd(string who, string id, out string msg)
        {
            if (!Coop.Active) return U.career.AddToBackpack(id, out msg);
            var p = RoomPack(who);
            var d = Items.Get(id);
            if (p == null || d == null) { msg = "Can't carry that."; return false; }
            if (d.kind == ItemKind.Upgrade)
            {
                int size = id == "bigpack" ? 8 : 6;
                if (p.size >= size) { msg = $"Your backpack already holds {p.size}."; return false; }
                p.size = size; msg = $"{d.name}: your backpack now holds {size} things."; Coop.Current.room.Touch(); return true;
            }
            if (p.items.Count >= p.size) { msg = $"Your backpack is full ({p.size} things)."; return false; }
            p.items.Add(id); msg = $"{d.name} in your backpack."; Coop.Current.room.Touch(); return true;
        }

        VesselSim EvaOf(string who) => U.vessels.FirstOrDefault(v => !v.destroyed && v.IsEva && v.EvaName == who);
        VesselSim ShipOf(string who) => U.vessels.FirstOrDefault(v => !v.destroyed && !v.IsEva && v.parts.Any(p => p.crew.Contains(who)));

        /// <summary>The ship a walker can reach (for fuel and batteries), or the one they're in.</summary>
        VesselSim ShipInReach(VesselSim eva, string who)
        {
            if (eva == null) return ShipOf(who);
            Vector3d here = eva.pos;
            return U.vessels.Where(v => !v.destroyed && !v.IsEva && !v.IsFlag && v.body == eva.body && v.parts.Count > 0)
                .OrderBy(v => (v.pos - here).magnitude).FirstOrDefault(v => (v.pos - here).magnitude < Items.UseReach + v.Length * 0.6);
        }

        Vector3d FixedPos(VesselSim v) => v.body.InertialToFixed(v.pos, ut);
        Vector3d FixedFacing(VesselSim v) => v.body.InertialToFixed(Vector3d.Rotate(v.rot, new Vector3d(0, 0, 1)), ut).normalized;

        void TellPlayer(string who, string text, float secs = 3f)
        {
            if (!Coop.Active || who == Coop.Current.room.Me?.name) { Show(text, secs); return; }
            Coop.Current.room.BroadcastRaw(new NetMsg { t = "say", a = text });
        }

        // ---------------------------------------------------------------- every frame

        /// <summary>Health, hunger and stamina over time; falls, g and heat; knocked-out Ardlings running out of time.</summary>
        void SurvivalTick()
        {
            if (!SurvivalHere || U == null) return;
            double now = ut;
            if (survivalUt < 0 || now < survivalUt || now - survivalUt > 86400 * 30) survivalUt = now;
            double dtGame = now - survivalUt;
            survivalUt = now;
            double hours = dtGame / 3600.0;
            bool easy = Words.Easy;
            foreach (var v in U.vessels.ToList())
            {
                if (v.destroyed) continue;
                bool fed = !v.UsesSupplies || v.SuppliesStored > 1e-6;
                bool live = v == active && !railsWarp && dtGame > 0 && dtGame < 1.0;
                foreach (var p in v.parts)
                {
                    foreach (var name in p.crew.ToList())
                    {
                        var vit = U.career.VitalsOf(name);
                        if (!v.hasLaunched && !v.IsEva) { vit.health = vit.hunger = vit.stamina = Survival.Max; vit.downedAt = -1; vit.tired = false; continue; }   // fresh on the pad
                        Survival.Tick(vit, hours, !v.IsEva && fed, p.def.kind == PartKind.Habitat);
                        if (live)
                        {
                            float gSafe = easy ? 12f : Survival.GSafe;
                            if (v.gForce > gSafe) HurtArdling(vit, (float)(v.gForce - gSafe) * Survival.GDamage * (float)dtGame, v, "the g-force");
                            if (p.HeatFraction > 0.92f) HurtArdling(vit, Survival.HeatDamage * (float)dtGame * (easy ? 0.5f : 1f), v, "the heat");
                        }
                        if (vit.Down && Survival.TooLate(vit, now)) LoseArdling(name, v, p);
                    }
                }
            }
            // falling on foot: landing hard hurts
            if (active.IsEva)
            {
                if (fallFor != active.id) { fallFor = active.id; fallWasLanded = active.landed; fallVs = 0; }
                float vs = (float)Vector3d.Dot(active.SurfaceRelVelocity, active.pos.normalized);
                if (!fallWasLanded && active.landed && !railsWarp)
                {
                    float hurt = Survival.FallHurt(-fallVs) * (easy ? 0.5f : 1f);
                    if (hurt > 0.5f && active.EvaName != null) HurtArdling(U.career.VitalsOf(active.EvaName), hurt, active, "the fall");
                }
                if (!active.landed) fallVs = vs;
                fallWasLanded = active.landed;
            }
            UpdateWorldItems(Mathf.Min((float)dtGame, 0.1f));
            // a room: everyone's numbers, now and then
            if (Coop.Active && Coop.Current.IsHost)
            {
                survivalSync -= Time.unscaledDeltaTime;
                if (survivalSync <= 0)
                {
                    survivalSync = 0.5f;
                    var st = Coop.Current.room.State;
                    st.vitals = st.players.Select(q => U.career.VitalsOf(q.name)).Where(x => x != null)
                        .Select(x => new Vitals { name = x.name, health = Mathf.Round(x.health), hunger = Mathf.Round(x.hunger), stamina = Mathf.Round(x.stamina), downedAt = x.downedAt, tired = x.tired }).ToList();
                    st.items = U.items.Select(w => new WorldItem { uid = w.uid, id = w.id, body = w.body, x = w.x, y = w.y, z = w.z, yaw = w.yaw, flying = w.flying }).ToList();
                    Coop.Current.room.Touch();
                }
            }
        }

        void HurtArdling(Vitals vit, float amount, VesselSim v, string what)
        {
            if (vit == null || vit.Down) return;
            if (!Survival.Hurt(vit, amount, ut)) return;
            // knocked out: their own revive kit gets them up straight away
            var pack = PackOf(vit.name, out _);
            int k = pack.IndexOf("revive");
            if (k >= 0)
            {
                pack.RemoveAt(k);
                Survival.Revive(vit);
                TellPlayer(vit.name, $"{vit.name} was knocked out by {what}, and their revive kit got them back up. (Health {vit.health:0}.)", 5f);
                return;
            }
            Show($"{vit.name} is knocked out by {what}! Someone with a revive kit has {Survival.DownSeconds:0} seconds to reach them.", 6f, true);
            audio?.Clunk(0.4f);
        }

        void LoseArdling(string name, VesselSim v, PartState p)
        {
            var vit = U.career.VitalsOf(name);
            vit.downedAt = -1;
            vit.health = Survival.Max;
            U.career.LoseCrew(name, v.name, "knocked out and not revived in time");
            if (v.IsEva)
            {
                v.destroyed = true;
                v.destroyReason = $"{name} was knocked out and nobody reached them in time.";
            }
            else { p.crew.Remove(name); v.RecomputeMass(); }
            Show($"{name} didn't make it: nobody revived them in time.", 6f, true);
        }

        // ---------------------------------------------------------------- the keys (on foot, and in the cabin)

        /// <summary>F, R, Z, I and 1-8 on foot; after the walking keys. True if this Ardling is knocked out (no walking).</summary>
        bool HandleSurvivalKeys(VesselSim v)
        {
            string me = MyArdlingName;
            var vit = VitalsFor(me);
            if (vit != null && vit.Down)
            {
                v.inputPitch = v.inputYaw = v.inputRoll = 0; v.evaStrafe = 0; v.evaJump = false; v.inputTranslate = Vector3.zero; v.evaRun = false;
                return true;
            }
            if (In.Down(K.Z) && v.landed) { v.evaCrouch = !v.evaCrouch; Show(v.evaCrouch ? "Crouching (slower, quieter). Z: stand up." : "Standing.", 1.5f); }
            if (In.Down(K.I)) TogglePack();
            UsePackKeys(me);
            if (!v.landed) return false;
            string holding = CarriedBy(me);
            if (In.Down(K.F))
            {
                var down = DownedNear(v, me);
                if (down != null) ItemOp(me, "revive", down);
                else if (holding != null) ItemOp(me, "put", "");
                else { var w = NearestItem(v, out _); if (w != null) ItemOp(me, "pick", w.uid.ToString(CultureInfo.InvariantCulture)); }
            }
            if (In.Down(K.R) && holding != null) ItemOp(me, "throw", "");
            return false;
        }

        void UsePackKeys(string me)
        {
            K[] keys = { K.Alpha1, K.Alpha2, K.Alpha3, K.Alpha4, K.Alpha5, K.Alpha6, K.Alpha7, K.Alpha8 };
            for (int i = 0; i < keys.Length; i++) if (In.Down(keys[i])) ItemOp(me, "use", i.ToString(CultureInfo.InvariantCulture));
        }

        void TogglePack() { packOpen = !packOpen; }

        /// <summary>The nearest thing lying within reach of this walker (from the room's list on a player's device).</summary>
        WorldItem NearestItem(VesselSim v, out double dist)
        {
            dist = double.MaxValue;
            if (v == null || !v.IsEva) return null;
            var list = Coop.Active && remote ? Coop.Current.room.State.items : U.items;
            Vector3d here = FixedPos(v);
            WorldItem best = null;
            foreach (var w in list)
            {
                if (w.body != v.body.name || w.flying) continue;
                double d = (w.Pos - here).magnitude;
                if (d < dist) { dist = d; best = w; }
            }
            return dist <= Items.PickupReach ? best : null;
        }

        /// <summary>A knocked-out Ardling on foot near this one (to revive), if this player has a revive kit.</summary>
        string DownedNear(VesselSim v, string me)
        {
            if (!PackOf(me, out _).Contains("revive")) return null;
            foreach (var o in U.vessels)
            {
                if (o == v || o.destroyed || !o.IsEva || o.body != v.body || o.EvaName == null) continue;
                var ov = VitalsFor(o.EvaName);
                if (ov != null && ov.Down && (o.pos - v.pos).magnitude < 3.0) return o.EvaName;
            }
            return null;
        }

        // ---------------------------------------------------------------- doing things (here, or on the host for a player)

        public void ItemOp(string who, string op, string arg)
        {
            if (string.IsNullOrEmpty(who)) return;
            if (Coop.Active && remote) { Coop.Current.room.Send(new NetMsg { t = "cmd", a = "item", b = op + "|" + arg }); return; }
            DoItemOp(who, op, arg);
        }

        void DoItemOp(string who, string op, string arg)
        {
            var eva = EvaOf(who);
            var pack = PackOf(who, out int size);
            int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n);
            switch (op)
            {
                case "pick":
                {
                    if (eva == null || !eva.landed) return;
                    if (CarriedBy(who) != null) { TellPlayer(who, "Your hands are full: put it down (F) or away (I) first.", 2f); return; }
                    var w = U.items.Find(x => x.uid == n);
                    if (w == null || w.body != eva.body.name || (w.Pos - FixedPos(eva)).magnitude > Items.PickupReach + 1.5) return;
                    U.items.Remove(w);
                    SetCarried(who, w.id);
                    TellPlayer(who, $"Picked up: {w.Def.name}. F puts it down, R throws it, I puts it in your backpack. ({w.Def.about})", 4f);
                    return;
                }
                case "put":
                case "throw":
                {
                    string id = CarriedBy(who);
                    if (id == null || eva == null || !eva.landed) return;
                    var b = eva.body;
                    Vector3d at = FixedPos(eva), up = at.normalized, fwd = FixedFacing(eva);
                    fwd = (fwd - up * Vector3d.Dot(fwd, up)).normalized;
                    var w = U.AddItem(id, b, at + fwd * 0.6 + up * (op == "throw" ? 0.9 : 0.1), UnityEngine.Random.Range(0f, 360f));
                    if (op == "throw") { w.flying = true; w.Vel = fwd * 7.0 + up * 3.5; }
                    else SettleItem(w, b);
                    SetCarried(who, null);
                    if (op == "throw") TellPlayer(who, $"Threw the {w.Def.name.ToLowerInvariant()}!", 1.5f);
                    return;
                }
                case "store":
                {
                    string id = CarriedBy(who);
                    if (id == null) return;
                    if (PackAdd(who, id, out string msg)) SetCarried(who, null);
                    TellPlayer(who, msg, 2.5f);
                    return;
                }
                case "drop":
                {
                    if (eva == null || !eva.landed || n < 0 || n >= pack.Count) { TellPlayer(who, "Drop things on the ground, on foot.", 2f); return; }
                    string id = pack[n];
                    pack.RemoveAt(n);
                    Vector3d at = FixedPos(eva), up = at.normalized, fwd = FixedFacing(eva);
                    fwd = (fwd - up * Vector3d.Dot(fwd, up)).normalized;
                    SettleItem(U.AddItem(id, eva.body, at + fwd * 0.5, UnityEngine.Random.Range(0f, 360f)), eva.body);
                    if (Coop.Active) Coop.Current.room.Touch();
                    return;
                }
                case "use": UseFromPack(who, pack, n, eva); return;
                case "revive":
                {
                    int k = pack.IndexOf("revive");
                    var tv = U.career.VitalsOf(arg);
                    if (k < 0 || tv == null || !tv.Down) return;
                    var teva = EvaOf(arg);
                    bool near = eva != null && teva != null && (teva.pos - eva.pos).magnitude < 3.5 || ShipOf(who) != null && ShipOf(who) == ShipOf(arg);
                    if (!near) return;
                    pack.RemoveAt(k);
                    Survival.Revive(tv);
                    Show($"{who} revived {arg}! (Health {tv.health:0}.)", 4f, true);
                    if (Coop.Active) { Coop.Current.room.BroadcastRaw(new NetMsg { t = "say", a = $"{who} revived {arg}!" }); Coop.Current.room.Touch(); }
                    return;
                }
            }
        }

        void UseFromPack(string who, List<string> pack, int n, VesselSim eva)
        {
            if (n < 0 || n >= pack.Count) return;
            var d = Items.Get(pack[n]);
            var vit = U.career.VitalsOf(who);
            if (d == null || vit == null) return;
            string msg = null;
            bool keep = false;
            switch (d.id)
            {
                case "revive":
                {
                    // someone knocked out nearby, or aboard with you
                    string target = eva != null ? DownedNearAll(eva) : ShipOf(who)?.parts.SelectMany(p => p.crew).FirstOrDefault(c => c != who && U.career.VitalsOf(c).Down);
                    if (target != null) { DoItemOp(who, "revive", target); return; }
                    msg = "Nobody here needs reviving. (If you're knocked out, a revive kit in your backpack is used by itself.)";
                    keep = true;
                    break;
                }
                case "repairkit":
                {
                    if (eva == null) { msg = "Repairs are done outside: go out (ENTER) and stand by the broken part."; keep = true; break; }
                    var broken = eva.WorkInReach(U.vessels).Where(t => t.p.broken || t.p.damage > 0.01f).OrderByDescending(t => t.p.damage + (t.p.broken ? 1 : 0)).FirstOrDefault();
                    if (broken.p == null) { msg = "Nothing broken or damaged within reach."; keep = true; break; }
                    broken.v.FixPart(broken.p);
                    msg = $"{who} fixed the {broken.p.def.name} with the repair kit.";
                    break;
                }
                case "scanner": scanUntil = Time.unscaledTime + 60f; msg = "Scanning: what's lying about nearby shows for a minute."; break;
                case "torch": torchOn = !torchOn; keep = true; msg = torchOn ? "Torch on." : "Torch off."; break;
                case "flagpack":
                    if (eva == null || eva.parts[0].flagsUsed <= 0) { msg = "You've got all your flags already."; keep = true; break; }
                    eva.parts[0].flagsUsed--;
                    msg = $"One more flag ({eva.FlagsLeft} left).";
                    break;
                default:
                    if (d.kind == ItemKind.Upgrade)
                    {
                        pack.RemoveAt(n);
                        if (!PackAdd(who, d.id, out msg)) { pack.Insert(n, d.id); keep = true; }
                        else { TellPlayer(who, msg); return; }
                        break;
                    }
                    if (d.kind == ItemKind.Valuable) { msg = $"{d.name}: worth {d.price} at the store back home. ({d.about})"; keep = true; break; }
                    msg = Items.UseOn(d, vit, ShipInReach(eva, who), eva);
                    if (msg == null)
                    {
                        keep = true;
                        msg = d.kind == ItemKind.Food ? "Not hungry right now." : d.kind == ItemKind.Medical ? "No need: health is full." :
                              d.kind == ItemKind.Supply ? (d.air > 0 ? "Use that on foot, to refill your suit." : "No ship in reach with room for it.") : $"Can't use the {d.name.ToLowerInvariant()} here.";
                    }
                    break;
            }
            if (!keep) pack.RemoveAt(n);
            if (Coop.Active) Coop.Current.room.Touch();
            if (msg != null) TellPlayer(who, msg, 3f);
        }

        string DownedNearAll(VesselSim eva)
        {
            foreach (var o in U.vessels)
            {
                if (o == eva || o.destroyed || !o.IsEva || o.body != eva.body || o.EvaName == null) continue;
                if (U.career.VitalsOf(o.EvaName).Down && (o.pos - eva.pos).magnitude < 3.5) return o.EvaName;
            }
            return null;
        }

        /// <summary>The host carries out a player's item command.</summary>
        void RunItemCommand(int whoId, NetMsg m)
        {
            var p = Coop.Current.room.State.Player(whoId);
            if (p == null || string.IsNullOrEmpty(m.b)) return;
            var f = m.b.Split(new[] { '|' }, 2);
            DoItemOp(p.name, f[0], f.Length > 1 ? f[1] : "");
        }

        // ---------------------------------------------------------------- things on the ground

        void SettleItem(WorldItem w, CelestialBody b)
        {
            var dir = w.Pos.normalized;
            w.Pos = dir * (b.radius + LaunchSite.SurfaceHeight(b, dir) + 0.06);
            w.flying = false;
            w.Vel = Vector3d.zero;
        }

        /// <summary>Thrown things fly and land (in the world's turning frame, near enough for a throw).</summary>
        void UpdateWorldItems(float dt)
        {
            if (dt <= 0) return;
            foreach (var w in U.items)
            {
                if (!w.flying) continue;
                var b = root.bodies.FirstOrDefault(x => x.name == w.body);
                if (b == null) { w.flying = false; continue; }
                Vector3d p = w.Pos, up = p.normalized;
                double g = b.mu / p.sqrMagnitude;
                w.Vel = w.Vel - up * (g * dt);
                p += w.Vel * dt;
                double ground = b.radius + LaunchSite.SurfaceHeight(b, p.normalized) + 0.06;
                if (p.magnitude <= ground)
                {
                    up = p.normalized;
                    p = up * ground;
                    Vector3d vv = w.Vel;
                    double vn = Vector3d.Dot(vv, up);
                    Vector3d vt = vv - up * vn;
                    if (Math.Abs(vn) > 2.5) w.Vel = vt * 0.45 - up * (vn * 0.3);   // a little bounce
                    else { w.flying = false; w.Vel = Vector3d.zero; }
                }
                w.Pos = p;
            }
        }

        /// <summary>Things near the ship, drawn where they lie (and what this player is holding, in front of them).</summary>
        void UpdateItemViews()
        {
            if (active == null) return;
            var list = Coop.Active && remote ? Coop.Current.room.State.items : U.items;
            var b = active.body;
            Vector3d here = FixedPos(active);
            var want = new HashSet<int>();
            foreach (var w in list)
            {
                if (w.body != b.name || (w.Pos - here).sqrMagnitude > 900 * 900) continue;
                want.Add(w.uid);
                if (!itemViews.TryGetValue(w.uid, out var go) || go == null) { go = BuildItemView(w.Def); itemViews[w.uid] = go; }
                Vector3d rel = b.FixedToInertial(w.Pos, renderUt);
                go.transform.position = (b.AbsolutePosition(renderUt) + rel - originAbs).ToFloat();
                Vector3 up = rel.normalized.ToFloat();
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0, w.yaw + (w.flying ? Time.time * 400f : 0), 0);
                var ring = go.transform.Find("Glow");
                if (ring != null) ring.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.time * 3f + w.uid));
            }
            foreach (var k in itemViews.Keys.ToList()) if (!want.Contains(k)) { if (itemViews[k] != null) UnityEngine.Object.Destroy(itemViews[k]); itemViews.Remove(k); }
            // in your hands
            string holding = active.IsEva ? CarriedBy(MyArdlingName) : null;
            if (holding != carriedViewId)
            {
                if (carriedView != null) UnityEngine.Object.Destroy(carriedView);
                carriedView = holding != null ? BuildItemView(Items.Get(holding), false) : null;
                carriedViewId = holding;
            }
            if (carriedView != null)
            {
                carriedView.transform.position = renderRot * new Vector3(0.12f, 0.12f, 0.42f) + renderRot * Vector3.up * (active.evaCrouch ? -0.12f : 0f);
                carriedView.transform.rotation = renderRot * Quaternion.Euler(0, 20, 0);
            }
            // the torch
            if (torchOn && active.IsEva)
            {
                if (torchLight == null)
                {
                    var tg = new GameObject("Torch");
                    torchLight = tg.AddComponent<Light>();
                    torchLight.type = LightType.Spot; torchLight.range = 40f; torchLight.spotAngle = 55f; torchLight.intensity = 4f; torchLight.color = new Color(1f, 0.95f, 0.85f);
                    torchLight.shadows = LightShadows.None;
                }
                torchLight.enabled = true;
                torchLight.transform.position = renderRot * new Vector3(0, 0.45f, 0.25f);
                torchLight.transform.rotation = renderRot * Quaternion.Euler(12, 0, 0);
            }
            else if (torchLight != null) torchLight.enabled = false;
        }

        GameObject BuildItemView(ItemDef d, bool glow = true)
        {
            var go = new GameObject("Item " + (d?.name ?? "?"));
            if (d == null) return go;
            string key = d.shape + "|" + d.color;
            if (!itemMeshes.TryGetValue(key, out var mesh) || mesh == null)
            {
                var b = new MeshBuilder();
                Color c = d.color, dark = d.color * 0.55f;
                switch (d.shape)
                {
                    case "can": b.Mat = MeshBuilder.MatGloss; b.AddFrustum(0.32f, 0f, 0.1f, 0.1f, c, 16, true, true); b.Mat = MeshBuilder.MatPaint; b.AddBox(new Vector3(0, 0.35f, 0.04f), new Vector3(0.04f, 0.06f, 0.04f), new Color(0.2f, 0.2f, 0.2f), Quaternion.identity); b.AddBox(new Vector3(0, 0.36f, -0.03f), new Vector3(0.12f, 0.02f, 0.03f), new Color(0.15f, 0.15f, 0.15f), Quaternion.identity); break;
                    case "pouch": b.Mat = MeshBuilder.MatGloss; b.AddBox(new Vector3(0, 0.1f, 0), new Vector3(0.16f, 0.2f, 0.04f), new Color(0.88f, 0.88f, 0.9f), Quaternion.identity); b.Mat = MeshBuilder.MatMatte; b.AddBox(new Vector3(0, 0.1f, -0.022f), new Vector3(0.12f, 0.08f, 0.004f), c, Quaternion.identity); break;
                    case "kit": b.Mat = MeshBuilder.MatPaint; b.AddBox(new Vector3(0, 0.08f, 0), new Vector3(0.3f, 0.16f, 0.2f), c, Quaternion.identity); b.AddBox(new Vector3(0, 0.08f, -0.101f), new Vector3(0.1f, 0.03f, 0.004f), d.id == "repairkit" ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.85f, 0.1f, 0.1f), Quaternion.identity); if (d.id != "repairkit") b.AddBox(new Vector3(0, 0.08f, -0.101f), new Vector3(0.03f, 0.1f, 0.004f), new Color(0.85f, 0.1f, 0.1f), Quaternion.identity); b.AddBox(new Vector3(0, 0.17f, 0), new Vector3(0.12f, 0.02f, 0.03f), dark, Quaternion.identity); break;
                    case "rock": b.Mat = MeshBuilder.MatMatte; b.AddSphere(new Vector3(0, 0.09f, 0), 0.1f, c, 7, 5); b.AddSphere(new Vector3(0.05f, 0.07f, 0.03f), 0.07f, c * 0.9f, 6, 4); break;
                    case "gem": b.Mat = MeshBuilder.MatGloss; b.AddSphere(new Vector3(0, 0.08f, 0), 0.08f, dark, 7, 5); b.AddFrustum(0.2f, 0.08f, 0f, 0.05f, c, 6, false, false); break;
                    case "bar": b.Mat = MeshBuilder.MatPaint; b.AddBox(new Vector3(0, 0.025f, 0), new Vector3(0.22f, 0.05f, 0.08f), c, Quaternion.identity); b.AddBox(new Vector3(0, 0.051f, 0), new Vector3(0.12f, 0.003f, 0.06f), Color.white, Quaternion.identity); break;
                    case "tube": b.Mat = MeshBuilder.MatPaint; { int s0 = b.v.Count; b.AddFrustum(0.36f, 0f, 0.05f, 0.05f, c, 12, true, true); XfLocal(b, s0, new Vector3(-0.18f, 0.05f, 0), Quaternion.Euler(0, 0, -90)); } break;
                    default: b.Mat = MeshBuilder.MatPaint; b.AddBox(new Vector3(0, 0.1f, 0), new Vector3(0.24f, 0.2f, 0.18f), c, Quaternion.identity); b.AddBox(new Vector3(0, 0.1f, -0.091f), new Vector3(0.16f, 0.1f, 0.004f), Color.white, Quaternion.identity); break;
                }
                mesh = b.ToMesh("Item " + d.shape);
                itemMeshes[key] = mesh;
            }
            var body = new GameObject("Shape");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = body.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            if (glow)
            {
                // a soft ring on the ground, so loot catches the eye
                var gb = new MeshBuilder();
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI * 2 / 16;
                    gb.AddBox(new Vector3(Mathf.Cos(a) * 0.32f, 0.01f, Mathf.Sin(a) * 0.32f), new Vector3(0.11f, 0.004f, 0.03f), new Color(1f, 0.85f, 0.35f, 0.6f), Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90, 0));
                }
                var g = new GameObject("Glow");
                g.transform.SetParent(go.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = gb.ToMesh("Item glow");
                var gr = g.AddComponent<MeshRenderer>();
                gr.sharedMaterial = Mats.Additive;
                gr.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        void ClearItemViews()
        {
            foreach (var go in itemViews.Values) if (go != null) UnityEngine.Object.Destroy(go);
            itemViews.Clear();
            if (carriedView != null) UnityEngine.Object.Destroy(carriedView);
            carriedView = null; carriedViewId = null;
            if (torchLight != null) UnityEngine.Object.Destroy(torchLight.gameObject);
            torchLight = null; torchOn = false;
            carried.Clear();
            packOpen = false;
            survivalUt = -1;
        }

        /// <summary>Searching an outpost or wreck leaves things lying about nearby, to pick up.</summary>
        void DropLoot(VesselSim near, string seedKey)
        {
            if (near == null || near.body == null) return;
            int seed = (seedKey ?? "x").Aggregate(17, (h, ch) => h * 31 + ch) ^ U.nextItemUid * 7919;
            var loot = Items.Loot(seed);
            var rng = new System.Random(seed);
            Vector3d at = FixedPos(near), up = at.normalized;
            Vector3d e = Vector3d.Cross(new Vector3d(0, 1, 0), up);
            if (e.magnitude < 1e-6) e = new Vector3d(1, 0, 0);
            e = e.normalized;
            Vector3d nn = Vector3d.Cross(up, e).normalized;
            double r0 = near.IsEva ? 2.5 : 6 + near.Length * 0.5;
            foreach (var id in loot)
            {
                double a = rng.NextDouble() * Math.PI * 2, r = r0 + rng.NextDouble() * 4;
                var w = U.AddItem(id, near.body, at + (e * Math.Cos(a) + nn * Math.Sin(a)) * r, (float)(rng.NextDouble() * 360));
                SettleItem(w, near.body);
            }
            Show($"{loot.Count} things lie scattered on the ground nearby: walk over and pick them up (F).", 5f, true);
        }

        // ---------------------------------------------------------------- what you see

        /// <summary>Health, food and stamina, bottom left, on foot and in the cabin; prompts for what F/R do.</summary>
        void DrawSurvivalHud(float W, float H)
        {
            if (active == null || active.destroyed) return;
            string me = MyArdlingName;
            var vit = VitalsFor(me);
            bool show = active.IsEva || inside;
            float boxY = inside ? H - 120 : H - 278;   // (on foot: above the throttle and fuel panel)
            if (vit != null && show)
            {
                var r = new Rect(12, boxY, 250, 92);
                Ui.Box(r);
                Ui.Text(new Rect(r.x + 10, r.y + 4, r.width - 20, 20), $"<b>{me}</b>" + (vit.Down ? "  <color=#ff7060>KNOCKED OUT</color>" : active.IsEva && active.evaCrouch ? "  <color=#9aa4ae>crouching</color>" : ""), Ui.Small);
                void Bar(float y, string label, float v, Color c)
                {
                    Ui.Text(new Rect(r.x + 10, y, 70, 18), label, Ui.Small);
                    Ui.Bar(new Rect(r.x + 80, y + 5, r.width - 130, 9), Mathf.Clamp01(v / Survival.Max), c);
                    Ui.Text(new Rect(r.xMax - 46, y, 40, 18), $"{v:0}", Ui.Small);
                }
                Bar(r.y + 26, "Health", vit.health, vit.health < 30 ? new Color(1f, 0.35f, 0.25f) : new Color(0.45f, 0.9f, 0.5f));
                Bar(r.y + 46, "Food", vit.hunger, vit.hunger < 20 ? new Color(1f, 0.55f, 0.2f) : new Color(0.95f, 0.8f, 0.35f));
                Bar(r.y + 66, "Stamina", vit.stamina, vit.tired ? new Color(0.6f, 0.6f, 0.65f) : new Color(0.4f, 0.75f, 1f));
                if (vit.Down)
                {
                    double left = Survival.DownSeconds - (ut - vit.downedAt);
                    Ui.Shadowed(new Rect(W / 2 - 400, H * 0.3f, 800, 40), $"{me} is knocked out: {Math.Max(0, left):0} s for a teammate with a revive kit", new GUIStyle(Ui.BigCenter) { fontSize = 22 });
                }
            }
            // what F and R do right now
            if (active.IsEva && active.landed && vit != null && !vit.Down)
            {
                string holding = CarriedBy(me);
                string hint = null;
                var down = DownedNear(active, me);
                if (down != null) hint = $"F: revive {down} (uses your revive kit)";
                else if (holding != null) hint = $"Holding: {Items.Get(holding)?.name}   ·   F: put down   ·   R: throw   ·   I: backpack";
                else { var w = NearestItem(active, out double d); if (w != null) hint = $"F: pick up {w.Def?.name}"; }
                if (hint != null) Ui.Shadowed(new Rect(W / 2 - 400, H * 0.66f, 800, 26), hint, new GUIStyle(Ui.BigCenter) { fontSize = 18 });
            }
            if (Time.unscaledTime < scanUntil) DrawScanner(W, boxY);
            if (packOpen) DrawPack(W, H, me);
        }

        void DrawScanner(float W, float boxY)
        {
            var list = Coop.Active && remote ? Coop.Current.room.State.items : U.items;
            Vector3d here = FixedPos(active), up = here.normalized;
            Vector3d north = (new Vector3d(0, 1, 0) - up * up.y).normalized, east = Vector3d.Cross(up, north).normalized;   // (body's spin axis is +Y)
            string Dir(Vector3d d)
            {
                double a = Math.Atan2(Vector3d.Dot(d, east), Vector3d.Dot(d, north)) * 180 / Math.PI;
                string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                return names[(int)Math.Round(((a % 360) + 360) % 360 / 45) % 8];
            }
            var near = list.Where(w => w.body == active.body.name).Select(w => (w, d: (w.Pos - here).magnitude)).Where(t => t.d < 3000).OrderBy(t => t.d).Take(6).ToList();
            float sh = 30 + near.Count * 20 + (near.Count == 0 ? 20 : 0);
            var r = new Rect(12, boxY - 6 - sh, 250, sh);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 10, r.y + 4, r.width - 20, 20), $"<b>Scanner</b>  <color=#8a929c>{Mathf.CeilToInt(scanUntil - Time.unscaledTime)} s</color>", Ui.Small);
            float y = r.y + 24;
            if (near.Count == 0) Ui.Text(new Rect(r.x + 10, y, r.width - 20, 20), "Nothing within 3 km.", Ui.Small);
            foreach (var (w, d) in near) { Ui.Text(new Rect(r.x + 10, y, r.width - 20, 20), $"{w.Def?.name}  <color=#8a929c>{d:0} m {Dir(w.Pos - here)}</color>", Ui.Small); y += 20; }
        }

        /// <summary>The backpack (I): use, drop, put away what you're holding.</summary>
        void DrawPack(float W, float H, string me)
        {
            var pack = Coop.Active && remote ? Coop.Current.room.State.packs.Find(p => p.name == me)?.items ?? new List<string>() : PackOf(me, out _);
            int size = Coop.Active ? (Coop.Current.room.State.packs.Find(p => p.name == me)?.size ?? 4) : U.career.backpackSize;
            string holding = CarriedBy(me);
            float h = 70 + size * 34 + (holding != null ? 34 : 0);
            var r = new Rect(W / 2 - 230, H / 2 - h / 2, 460, h);
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 14, r.y + 8, 300, 24), $"<b>Backpack</b>  <color=#8a929c>{pack.Count}/{size}</color>", Ui.Bold);
            if (Ui.Press(new Rect(r.xMax - 92, r.y + 8, 80, 26), $"Close ({In.Name(K.I)})", Ui.ButtonSmall)) packOpen = false;
            float y = r.y + 40;
            if (holding != null)
            {
                Ui.Text(new Rect(r.x + 14, y + 4, 260, 22), $"In your hands: <b>{Items.Get(holding)?.name}</b>", Ui.Small);
                if (Ui.Press(new Rect(r.xMax - 128, y, 116, 28), "Put it away", Ui.ButtonSmall)) ItemOp(me, "store", "");
                y += 34;
            }
            for (int i = 0; i < size; i++)
            {
                var row = new Rect(r.x + 10, y, r.width - 20, 30);
                Ui.Line(new Rect(row.x, row.yMax + 2, row.width, 1), new Color(1, 1, 1, 0.06f));
                if (i < pack.Count)
                {
                    var d = Items.Get(pack[i]);
                    Ui.Text(new Rect(row.x + 4, row.y + 5, 250, 22), $"<color=#8a929c>{i + 1}</color>  {d?.name}  <color=#8a929c>{ItemStat(d)}</color>", new GUIStyle(Ui.Small) { wordWrap = false, clipping = TextClipping.Clip });
                    if (Ui.Press(new Rect(row.xMax - 168, row.y + 1, 80, 26), "Use", Ui.ButtonSmall)) ItemOp(me, "use", i.ToString(CultureInfo.InvariantCulture));
                    if (active.IsEva && active.landed && Ui.Press(new Rect(row.xMax - 84, row.y + 1, 80, 26), "Drop", Ui.ButtonSmall)) ItemOp(me, "drop", i.ToString(CultureInfo.InvariantCulture));
                }
                else Ui.Text(new Rect(row.x + 4, row.y + 5, 250, 22), $"<color=#5a626c>{i + 1}  empty</color>", Ui.Small);
                y += 34;
            }
            Ui.Text(new Rect(r.x + 14, r.yMax - 24, r.width - 28, 20), $"<color=#8a929c>1-{size}: use   ·   F picks things up on foot   ·   kept after you land</color>", Ui.Small);
        }

        static string ItemStat(ItemDef d)
        {
            if (d == null) return "";
            switch (d.kind)
            {
                case ItemKind.Food: return $"+{d.hunger:0} food" + (d.stamina > 0 ? $", +{d.stamina:0} stamina" : "");
                case ItemKind.Medical: return d.id == "revive" ? "revives" : $"+{d.heal:0} health";
                case ItemKind.Supply: return d.fuel > 0 ? $"{d.fuel * 1000:0} kg fuel" : d.mono > 0 ? $"{d.mono * 1000:0} kg thruster fuel" : d.ec > 0 ? $"{d.ec:0} EC" : d.supplies > 0 ? $"{d.supplies:0} crew-days" : d.coolant > 0 ? $"{d.coolant:0} L coolant" : "suit air";
                case ItemKind.Valuable: return $"worth {d.price}";
                case ItemKind.Upgrade: return "backpack upgrade";
                default: return d.kind.ToString().ToLowerInvariant();
            }
        }

        // ---------------------------------------------------------------- for tours

        public Vitals TestVitals(string name) => U.career.VitalsOf(name);
        public void TestHurt(string name, float amount) => HurtArdling(U.career.VitalsOf(name), amount, active, "a test");
        public void TestDropLoot() => DropLoot(active, "test" + U.nextItemUid);
        public string TestCarried => CarriedBy(MyArdlingName);
        public int TestItemsNear => U.items.Count(w => active != null && w.body == active.body.name && (w.Pos - FixedPos(active)).magnitude < 40);
        public void TestPickNearest() { var w = U.items.Where(x => active != null && x.body == active.body.name).OrderBy(x => (x.Pos - FixedPos(active)).magnitude).FirstOrDefault(); if (w != null) { var e = FixedPos(active); w.Pos = e + (w.Pos - e).normalized * 1.0; DoItemOp(MyArdlingName, "pick", w.uid.ToString(CultureInfo.InvariantCulture)); } }
    }
}
