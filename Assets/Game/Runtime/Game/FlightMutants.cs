using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 42 in flight (the rules are in Sim/Drones.cs and Sim/Weapons.cs): how Shiftlings, Lurkers and the Hollow
    /// look and move, the Hollow's slams and its health bar, and the weapons an Ardling fights with.
    ///
    /// A Shiftling passing as crew is an ordinary Ardling (in a room, sometimes in a teammate's look) that strolls about
    /// and now and then twitches. When it turns, the suit goes dark, the note falls away and the visor splits on a glow,
    /// with spines along the shoulders; it runs, arms out. A Lurker runs on all fours. The Hollow is a giant one, darker,
    /// with more spines and glowing cracks; its slam throws up a ring of dust.
    /// </summary>
    public partial class FlightMode
    {
        readonly Dictionary<string, string> heldBy = new Dictionary<string, string>();   // the weapon each walker chose
        readonly Dictionary<string, float> struckAt = new Dictionary<string, float>();    // when each last struck (for the swing)

        /// <summary>Hooks the Ardling views up to what the game knows (knocked out, carrying, holding, striking).</summary>
        void HookArdlingViews()
        {
            VesselView.ArdlingDown = n => VitalsFor(n)?.Down ?? false;
            VesselView.ArdlingCarrying = n => CarriedBy(n) != null;
            VesselView.ArdlingHolds = n => { var w = HeldWeapon(n); return w.id == "kick" ? null : w.id; };
            VesselView.ArdlingStruck = n => struckAt.TryGetValue(n ?? "", out float t0) ? Time.time - t0 : 99f;
        }
        readonly Dictionary<int, (Vector3 at, float phase, float speed, float twitch)> walkAnim = new Dictionary<int, (Vector3, float, float, float)>();
        readonly Dictionary<int, int> slamsSeen = new Dictionary<int, int>();
        static readonly Color MutantTint = new Color(0.32f, 0.12f, 0.42f);

        /// <summary>What this walker fights with now.</summary>
        WeaponDef HeldWeapon(string who)
        {
            heldBy.TryGetValue(who ?? "", out string chosen);
            List<string> pack = PackOf(who, out _);
            if (Coop.Active && remote) pack = Coop.Current.room.State.packs.Find(p => p.name == who)?.items ?? pack;
            return Weapons.Held(pack, chosen);
        }

        // ---------------------------------------------------------------- the look of the walkers

        GameObject BuildWalkerView(Drone d)
        {
            var def = d.Def;
            var go = new GameObject(def.name);
            Ardlings.Look disguise = MyArdling.LookOf(!string.IsNullOrEmpty(d.look) ? d.look : Ardlings.Code(d.uid % Ardlings.SuitCount, d.uid % Ardlings.PaperCount, d.uid % Ardlings.Faces, 0));
            if (d.kind == DroneKind.Shiftling)
            {
                var plain = MyArdling.Build(disguise, 0);
                plain.name = "Disguise";
                plain.transform.SetParent(go.transform, false);
            }
            var m = BuildMutant(disguise, def.kind);
            m.name = "Mutant";
            m.transform.SetParent(go.transform, false);
            float scale = def.kind == DroneKind.Hollow ? 3.6f : def.kind == DroneKind.Lurker ? 0.85f : 1f;
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>An Ardling turned: dark suit, no note, a split glowing visor, spines on the shoulders and back.</summary>
        static GameObject BuildMutant(Ardlings.Look look, DroneKind kind)
        {
            var go = MyArdling.Build(look, 0);
            var pb = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name.Contains("Note")) { r.gameObject.SetActive(false); continue; }
                r.GetPropertyBlock(pb);
                pb.SetColor("_Tint", kind == DroneKind.Hollow ? MutantTint * 0.6f : MutantTint);
                r.SetPropertyBlock(pb);
            }
            var body = go.transform.Find("Body");
            float h = Ardlings.Height;
            // spines: dark cones along the shoulders and down the back
            var sp = new MeshBuilder { Mat = MeshBuilder.MatGloss };
            int n = kind == DroneKind.Hollow ? 9 : 5;
            Color spine = new Color(0.12f, 0.04f, 0.16f);
            for (int i = 0; i < n; i++)
            {
                float f = n == 1 ? 0.5f : i / (float)(n - 1);
                int s0 = sp.v.Count;
                sp.AddFrustum(0.16f + 0.08f * Mathf.Sin(f * Mathf.PI), 0f, 0f, 0.045f, spine, 6, false, true);   // (a cone, its tip up)
                Vector3 at = new Vector3(Mathf.Lerp(-0.2f, 0.2f, f), -0.62f + 0.08f * Mathf.Sin(f * Mathf.PI), -0.12f - 0.05f * Mathf.Sin(f * Mathf.PI));
                var q = Quaternion.Euler(-35f, 0, Mathf.Lerp(40f, -40f, f));
                for (int k = s0; k < sp.v.Count; k++) sp.v[k] = at + q * sp.v[k];
            }
            var sg = new GameObject("Spines");
            sg.transform.SetParent(body, false);
            sg.AddComponent<MeshFilter>().sharedMesh = sp.ToMesh("Spines");
            sg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            // the visor split open on a glow: a jagged mouth and two eyes (drawn additive, so they shine in the dark)
            var gl = new MeshBuilder();
            Color glow = kind == DroneKind.Hollow ? new Color(1f, 0.2f, 0.9f) * 2.2f : new Color(0.95f, 0.25f, 1f) * 2f;
            float vz = 0.215f, vy = -0.3f;
            gl.AddSphere(new Vector3(-0.07f, vy + 0.05f, vz), 0.032f, glow, 8, 6);
            gl.AddSphere(new Vector3(0.07f, vy + 0.05f, vz), 0.032f, glow, 8, 6);
            for (int i = 0; i < 6; i++)
            {
                float x = -0.09f + i * 0.036f;
                gl.AddBox(new Vector3(x, vy - 0.05f + (i % 2 == 0 ? 0.012f : -0.012f), vz), new Vector3(0.03f, 0.012f, 0.01f), glow, Quaternion.Euler(0, 0, i % 2 == 0 ? 25f : -25f));
            }
            if (kind == DroneKind.Hollow)
                for (int i = 0; i < 4; i++) gl.AddBox(new Vector3(-0.12f + i * 0.08f, -0.75f - 0.05f * i, 0.17f), new Vector3(0.012f, 0.18f, 0.01f), glow * 0.6f, Quaternion.Euler(0, 0, 20f - 13f * i));   // cracks down the chest
            var gg = new GameObject("Glow");
            gg.transform.SetParent(body, false);
            gg.AddComponent<MeshFilter>().sharedMesh = gl.ToMesh("Mutant glow");
            var gr = gg.AddComponent<MeshRenderer>();
            gr.sharedMaterial = Mats.Additive;
            gr.shadowCastingMode = ShadowCastingMode.Off;
            _ = h;
            return go;
        }

        /// <summary>Each frame: where a walker stands, which way it faces, its stride, its disguise (and its twitch), its turn.</summary>
        void UpdateWalkerView(Drone d, GameObject go, CelestialBody b)
        {
            var def = d.Def;
            Vector3d up = d.Pos.normalized;
            Vector3 at = RenderPoint(b, d.Pos);
            Vector3 upR = b.FixedToInertial(up, renderUt).ToFloat();
            walkAnim.TryGetValue(d.uid, out var wa);
            float dt = Mathf.Max(1e-3f, Time.deltaTime);
            Vector3 step = Vector3.ProjectOnPlane(at - wa.at, upR);
            float speed = wa.at == Vector3.zero ? 0f : Mathf.Min(step.magnitude / dt, 20f);
            wa.speed = Mathf.Lerp(wa.speed, speed, Mathf.Clamp01(dt * 8f));
            wa.phase += dt * (3f + wa.speed * 2.2f) / Mathf.Max(0.5f, go.transform.localScale.x * 0.6f);
            wa.at = at;
            go.transform.position = at;
            // facing: its target, else the way it's walking
            Vector3 look = step.sqrMagnitude > 1e-6f ? step : go.transform.forward;
            var te = d.target != null ? EvaOf(d.target) : null;
            if (te != null && d.state != DroneState.Idle) look = Vector3.ProjectOnPlane(RenderPoint(b, FixedPos(te)) - at, upR);
            look = Vector3.ProjectOnPlane(look, upR);
            if (look.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.Slerp(go.transform.rotation, Quaternion.LookRotation(look, upR), Mathf.Clamp01(dt * 8f));
            // passing as crew, or turned (a Shiftling turning: the suit flickers between the two for a moment)
            bool disguised = d.kind == DroneKind.Shiftling && d.state == DroneState.Idle;
            if (d.kind == DroneKind.Shiftling && d.state == DroneState.Alert) disguised = Mathf.Sin(Time.time * 40f) > 0.2f;
            var plain = go.transform.Find("Disguise");
            var mutant = go.transform.Find("Mutant");
            if (plain != null && plain.gameObject.activeSelf != disguised) plain.gameObject.SetActive(disguised);
            if (mutant != null && mutant.gameObject.activeSelf == disguised) mutant.gameObject.SetActive(!disguised);
            var shown = disguised ? plain : mutant;
            if (shown == null) return;
            // a disguised one twitches now and then (the only tell)
            wa.twitch -= dt;
            if (wa.twitch < -6f - (d.uid % 5)) wa.twitch = 0.18f;
            float jerk = disguised && wa.twitch > 0 ? 18f * Mathf.Sin(wa.twitch * 60f) : 0f;
            walkAnim[d.uid] = wa;
            var body = shown.Find("Body");
            float moving = Mathf.Clamp01(wa.speed / 1.5f);
            bool crawl = d.kind == DroneKind.Lurker && !disguised;
            bool hunting = !disguised && d.state != DroneState.Idle;
            if (body != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(wa.phase)) * 0.05f * moving;
                float lean = crawl ? 68f : hunting ? 14f * moving : 4f * moving;
                body.localPosition = new Vector3(0, Ardlings.Height * (crawl ? 0.55f : 1f) + bob, 0);
                body.localRotation = Quaternion.Euler(lean, jerk, (disguised ? 0f : 3f * Mathf.Sin(Time.time * 7f + d.uid)));
                int k = 0;
                foreach (Transform limb in body)
                {
                    if (limb.name != "Limb") continue;
                    int side = k % 2 == 0 ? -1 : 1;
                    bool arm = k < 2;
                    float swing = Mathf.Sin(wa.phase) * (crawl ? 50f : 38f) * moving;
                    Quaternion q;
                    if (crawl) q = arm ? Quaternion.Euler(-80f + swing * side, 0, side * 8f) : Quaternion.Euler(-30f - swing * side, 0, side * 6f);
                    else if (hunting) q = arm ? Quaternion.Euler(-70f + swing * side * 0.4f, 0, side * 22f) : Quaternion.Euler(-swing * side, 0, 0);   // arms out, reaching
                    else q = arm ? Quaternion.Euler(swing * side * 0.8f, 0, side * 5f) : Quaternion.Euler(-swing * side, 0, 0);
                    limb.localRotation = Quaternion.Slerp(limb.localRotation, q, Mathf.Clamp01(dt * 14f));
                    k++;
                }
            }
            // the Hollow's slam: a ring of dust and a thud (once per slam, here and on everyone's game)
            if (def.boss)
            {
                slamsSeen.TryGetValue(d.uid, out int seen);
                if (d.slams != seen)
                {
                    slamsSeen[d.uid] = d.slams;
                    if (seen != 0 || d.slams == 1) BossSlamFx(at, upR);
                }
            }
        }

        void BossSlamFx(Vector3 at, Vector3 up)
        {
            Smoke.Dust(at, up, Drones.SlamRadius);
            audio?.Boom(30);
            if (active != null && active.IsEva) Show("The Hollow slams the ground!", 1.5f);
        }

        /// <summary>The boss's health bar across the top while it fights (for everyone near).</summary>
        void DrawBossBar(float W)
        {
            if (active == null) return;
            var list = Coop.Active && remote ? Coop.Current.room.State.drones : drones;
            Vector3d here = FixedPos(active);
            foreach (var d in list)
            {
                if (!d.Def.boss || d.body != active.body.name || d.state == DroneState.Idle || d.state == DroneState.Down) continue;
                if ((d.Pos - here).magnitude > 400) continue;
                var r = new Rect(W / 2 - 300, 126, 600, 46);   // (under the messages)
                Ui.Box(r); Ui.Box(r);
                string phase = d.phase >= 2 ? "  <color=#ff6a6a>ENRAGED</color>" : d.phase == 1 ? "  <color=#d8a0ff>calling its Shiftlings</color>" : "";
                Ui.Text(new Rect(r.x + 12, r.y + 4, r.width - 24, 20), $"<b>{d.Def.name.ToUpperInvariant()}</b>{phase}", new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter });
                Ui.Bar(new Rect(r.x + 14, r.y + 27, r.width - 28, 10), d.health / Math.Max(1f, Drones.MaxHealth(d)), new Color(0.75f, 0.25f, 0.95f));
                return;
            }
        }

        // ---------------------------------------------------------------- weapons

        /// <summary>A weapon's shot or swing as it looks (the zapper's crackle, a blaster bolt, the rifle's beam, a swing's whoosh).</summary>
        void WeaponFx(WeaponDef w, CelestialBody b, Vector3d from, Vector3d to)
        {
            if (!w.ranged) { audio?.Clunk(0.35f); return; }
            if (w.id == "zapper") { Zap(b, from, to, w.color); return; }
            var go = new GameObject(w.name + " shot");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Mats.Additive;
            lr.widthMultiplier = w.id == "boltrifle" ? 0.14f : 0.07f;
            lr.positionCount = 2;
            Vector3 a = RenderPoint(b, from), z = RenderPoint(b, to);
            if (w.id == "blaster") a = Vector3.Lerp(a, z, 0.55f);   // a short bolt, near the end of its flight
            lr.SetPosition(0, a); lr.SetPosition(1, z);
            lr.startColor = w.color; lr.endColor = Color.white;
            zapFx.Add((go, Time.unscaledTime + (w.id == "boltrifle" ? 0.16f : 0.08f)));
            audio?.Clunk(w.id == "boltrifle" ? 0.6f : 0.25f);
        }

        /// <summary>Firing at nothing in particular: the shot goes the way you face (only how it looks).</summary>
        void FireAtNothing(VesselSim v, WeaponDef w)
        {
            if (!w.ranged) { audio?.Clunk(0.2f); return; }
            Vector3d here = FixedPos(v), up = here.normalized;
            Vector3d fwd = FirstPersonView ? v.body.InertialToFixed(Vector3d.FromFloat(root.farCam.transform.forward), ut) : FixedFacing(v);
            WeaponFx(w, v.body, here + up * 0.8, here + up * 0.8 + fwd.normalized * w.reach);
        }

        // ---------------------------------------------------------------- for tours

        public void TestSpawn(DroneKind k, float metres, string look = null)
        {
            var at = FixedPos(active);
            var p = at + RandomFlat(at, metres);
            var d = SpawnDrone(k, active.body, "test", p, p);
            if (look != null) d.look = look;
        }
        public Drone TestBoss() => drones.FirstOrDefault(d => d.Def.boss);
        public void TestHold(string weapon) => heldBy[MyArdlingName ?? ""] = weapon;
        public string TestHeld() => HeldWeapon(MyArdlingName).id;
        public void TestClearDrones() => ClearDrones();
        public System.Collections.Generic.List<Drone> TestDrones() => drones.ToList();
        public Vector3d FixedPosOf(VesselSim v) => FixedPos(v);
    }
}
