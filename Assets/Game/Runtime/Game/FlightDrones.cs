using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 40, rogue drones in flight (the rules are in Sim/Drones.cs). Come within 800 m of a place nobody's cleared
    /// and its guards wake: 1-3 drones at a random place, a warden and two scouts at the big outposts. They notice
    /// Ardlings on foot, chase and zap them. Fight back: with a zapper in your backpack, left click zaps the drone you're
    /// facing (18 m); without one, a kick (close up); or throw things at them (R). A drone knocked out drops a drone core
    /// (worth money) and sometimes more; once all of a place's guards are down it stays clear. The host runs them in a room.
    /// </summary>
    public partial class FlightMode
    {
        readonly List<Drone> drones = new List<Drone>();
        readonly HashSet<string> placesAwake = new HashSet<string>();
        readonly Dictionary<int, GameObject> droneViews = new Dictionary<int, GameObject>();
        readonly List<(GameObject go, float until)> zapFx = new List<(GameObject, float)>();
        readonly HashSet<int> wardensCalled = new HashSet<int>();
        int nextDroneUid = 1;
        float droneWake, attackCool;
        static Mesh droneMesh, mimicMesh;

        // ---------------------------------------------------------------- waking, stepping, clearing

        void DronesTick(float dt)
        {
            UpdateZapFx();
            if (!SurvivalHere || paused || active == null || active.destroyed || replay != null || Lessons.Active) return;
            if (railsWarp) return;
            var b = active.body;
            Vector3d here = FixedPos(active);
            droneWake -= dt;
            if (droneWake <= 0)
            {
                droneWake = 1f;
                foreach (var l in Outposts.On(b))
                {
                    if (U.career.cleared.Contains(l.id) || placesAwake.Contains(l.id)) continue;
                    Vector3d centre = l.dir * (b.radius + LaunchSite.SurfaceHeight(b, l.dir));
                    if ((centre - here).magnitude > Drones.Wake) continue;
                    WakeGuards(l, b, centre);
                }
                // far away again: they go back to sleep (and come back fresh)
                foreach (var d in drones.Where(x => x.body == b.name && (x.Home - here).magnitude > 2500).ToList()) { drones.Remove(d); placesAwake.Remove(d.home); }
                foreach (var d in drones.Where(x => x.body != b.name).ToList()) { drones.Remove(d); placesAwake.Remove(d.home); }
            }
            var targets = U.vessels.Where(v => !v.destroyed && v.IsEva && v.body == b && v.EvaName != null && !(U.career.VitalsOf(v.EvaName)?.Down ?? true))
                .Select(v => (v.EvaName, FixedPos(v), v.evaCrouch)).ToList();
            bool easy = Words.Easy;
            foreach (var d in drones.ToList())
            {
                if (d.body != b.name) continue;
                var dir = d.Pos.normalized;
                double ground = b.radius + LaunchSite.SurfaceHeight(b, dir);
                var was = d.state;
                var hit = Drones.Step(d, dt, targets, ground);
                if (d.kind == DroneKind.Warden && was != DroneState.Chase && d.state == DroneState.Chase && wardensCalled.Add(d.uid))
                {
                    SpawnDrone(DroneKind.Scout, b, d.home, d.Pos + RandomFlat(d.Pos, 4), d.Home);
                    Show("The warden calls for help: another scout joins in!", 3f, true);
                }
                if (hit != null)
                {
                    var (who, dmg, drain) = hit.Value;
                    var tv = U.career.VitalsOf(who);
                    var te = EvaOf(who);
                    if (dmg >= 1f) Zap(b, d.Pos, te != null ? FixedPos(te) + FixedPos(te).normalized * 0.5 : d.Pos, d.Def.color);
                    if (drain > 0 && tv != null) tv.stamina = Mathf.Max(0, tv.stamina - drain);
                    HurtArdling(tv, dmg * (easy ? 0.5f : 1f), te, $"a {d.Def.name.ToLowerInvariant()}");
                }
            }
        }

        void WakeGuards(Landmark l, CelestialBody b, Vector3d centre)
        {
            placesAwake.Add(l.id);
            foreach (var k in Drones.Guards(l.id, U.WeatherSeed))
                SpawnDrone(k, b, l.id, centre + RandomFlat(centre, UnityEngine.Random.Range(6f, 16f)), centre);
            if (Words.Easy && drones.Count(d => d.home == l.id) > 1) drones.Remove(drones.Last(d => d.home == l.id));   // (Easy: one fewer)
        }

        Vector3d RandomFlat(Vector3d at, double r)
        {
            Vector3d up = at.normalized, e = Vector3d.Cross(new Vector3d(0, 1, 0), up);
            if (e.magnitude < 1e-6) e = new Vector3d(1, 0, 0);
            e = e.normalized;
            Vector3d n = Vector3d.Cross(up, e).normalized;
            double a = UnityEngine.Random.value * Math.PI * 2;
            return (e * Math.Cos(a) + n * Math.Sin(a)) * r;
        }

        Drone SpawnDrone(DroneKind k, CelestialBody b, string home, Vector3d at, Vector3d homePos)
        {
            var d = new Drone { uid = nextDroneUid++, kind = k, body = b.name, home = home, health = Drones.Get(k).health, state = DroneState.Idle };
            d.Pos = at; d.Home = at - (at - homePos) * 0.2;
            drones.Add(d);
            return d;
        }

        /// <summary>A drone knocked out: it drops a drone core (and maybe more); the last guard down clears the place.</summary>
        void DroneDown(Drone d, string by)
        {
            var b = root.bodies.FirstOrDefault(x => x.name == d.body);
            if (b == null) return;
            var dir = d.Pos.normalized;
            SettleItem(U.AddItem("dronecore", b, d.Pos, UnityEngine.Random.Range(0f, 360f)), b);
            if (UnityEngine.Random.value < 0.4f) SettleItem(U.AddItem(Items.Loot(d.uid * 31 + U.nextItemUid, 1)[0], b, d.Pos + RandomFlat(d.Pos, 1.2), 0), b);
            SpawnBoom(b, b.FixedToInertial(d.Pos, ut), 1.5f);
            Show($"{by} knocked out the {d.Def.name.ToLowerInvariant()}! It dropped its core.", 3f, true);
            Quest(QuestKind.Drones, 1);
            string home = d.home;
            drones.Remove(d);
            if (!drones.Any(x => x.home == home && x.state != DroneState.Down) && !U.career.cleared.Contains(home))
            {
                U.career.cleared.Add(home);
                Show($"The place is clear of drones.", 3f, true);
            }
        }

        // ---------------------------------------------------------------- fighting back

        /// <summary>Left click on foot: the zapper (in your backpack) at the drone you're facing, or a kick up close.</summary>
        void HandleAttack(VesselSim v, string me)
        {
            attackCool -= Time.unscaledDeltaTime;
            if (!v.landed || packOpen || !In.LeftDown || In.Held(K.LeftAlt) || attackCool > 0 || Ui.MouseOverUI(In.MousePosition)) return;
            if (CarriedBy(me) != null) return;   // (hands full: R throws)
            var target = AimedDrone(v, out bool zapper);
            if (target == null) return;
            attackCool = zapper ? 0.8f : 0.6f;
            ItemOp(me, "attack", target.uid.ToString(CultureInfo.InvariantCulture));
        }

        Drone AimedDrone(VesselSim v, out bool zapper)
        {
            string me = MyArdlingName;
            zapper = PackOf(me, out _).Contains("zapper") || Coop.Active && remote && (Coop.Current.room.State.packs.Find(p => p.name == me)?.items.Contains("zapper") ?? false);
            var list = Coop.Active && remote ? Coop.Current.room.State.drones : drones;
            Vector3d here = FixedPos(v), up = here.normalized;
            Vector3d fwd = FirstPersonView ? v.body.InertialToFixed(Vector3d.FromFloat(root.farCam.transform.forward), ut) : FixedFacing(v);   // (first person: where you're looking)
            fwd = (fwd - up * Vector3d.Dot(fwd, up)).normalized;
            double reach = zapper ? 18 : 2.6, cosMax = Math.Cos((zapper ? 25 : 60) * Math.PI / 180);
            Drone best = null;
            double bestD = double.MaxValue;
            foreach (var d in list)
            {
                if (d.body != v.body.name || d.state == DroneState.Down) continue;
                Vector3d to = d.Pos - here;
                double dist = to.magnitude;
                if (dist > reach || dist < 1e-3) continue;
                Vector3d flat = (to - up * Vector3d.Dot(to, up)).normalized;
                if (Vector3d.Dot(flat, fwd) < cosMax && dist > 1.5) continue;
                if (dist < bestD) { bestD = dist; best = d; }
            }
            return best;
        }

        /// <summary>The host (or this game) hits a drone for a player.</summary>
        void DoAttack(string who, int uid)
        {
            var eva = EvaOf(who);
            var d = drones.Find(x => x.uid == uid);
            if (eva == null || d == null) return;
            bool zapper = PackOf(who, out _).Contains("zapper");
            double dist = (d.Pos - FixedPos(eva)).magnitude;
            if (dist > (zapper ? 20 : 3.2)) return;
            var b = eva.body;
            if (zapper) Zap(b, FixedPos(eva) + FixedPos(eva).normalized * 0.8, d.Pos, new Color(0.5f, 0.9f, 1f));
            else audio?.Clunk(0.4f);
            if (d.state == DroneState.Idle || d.target == null) { d.target = who; d.state = DroneState.Chase; }
            if (Drones.Damage(d, zapper ? 20 : 6)) DroneDown(d, who);
        }

        /// <summary>Things thrown at a drone hurt it.</summary>
        void ThrownHits(WorldItem w)
        {
            foreach (var d in drones)
            {
                if (d.body != w.body || d.state == DroneState.Down) continue;
                if ((d.Pos - w.Pos).magnitude > 1.3) continue;
                w.Vel = w.Vel * -0.2;
                if (d.target == null) { d.target = MyArdlingName; d.state = DroneState.Chase; }
                if (Drones.Damage(d, 15)) { DroneDown(d, MyArdlingName ?? "Someone"); }
                else Show($"Hit the {d.Def.name.ToLowerInvariant()}! ({d.health:0} left)", 1.5f);
                return;
            }
        }

        // ---------------------------------------------------------------- the look

        void Zap(CelestialBody b, Vector3d from, Vector3d to, Color c)
        {
            var go = new GameObject("Zap");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Mats.Additive;
            lr.widthMultiplier = 0.08f;
            lr.positionCount = 8;
            Vector3 a = RenderPoint(b, from), z = RenderPoint(b, to);
            for (int i = 0; i < 8; i++)
            {
                float f = i / 7f;
                Vector3 jit = i == 0 || i == 7 ? Vector3.zero : UnityEngine.Random.insideUnitSphere * 0.25f;
                lr.SetPosition(i, Vector3.Lerp(a, z, f) + jit);
            }
            lr.startColor = c; lr.endColor = Color.white;
            zapFx.Add((go, Time.unscaledTime + 0.15f));
        }

        void UpdateZapFx()
        {
            for (int i = zapFx.Count - 1; i >= 0; i--) { if (Time.unscaledTime < zapFx[i].until) continue; if (zapFx[i].go != null) UnityEngine.Object.Destroy(zapFx[i].go); zapFx.RemoveAt(i); }
        }

        void UpdateDroneViews()
        {
            if (active == null) return;
            var list = Coop.Active && remote ? Coop.Current.room.State.drones : drones;
            var b = active.body;
            var want = new HashSet<int>();
            foreach (var d in list)
            {
                if (d.body != b.name) continue;
                want.Add(d.uid);
                if (!droneViews.TryGetValue(d.uid, out var go) || go == null) { go = BuildDroneView(d.Def); droneViews[d.uid] = go; }
                bool asleepMimic = d.kind == DroneKind.Mimic && d.state == DroneState.Idle;
                Vector3d up = d.Pos.normalized;
                Vector3d p = d.Pos + (asleepMimic ? -up * (Drones.Hover + 0.2) : up * (0.15 * Math.Sin(Time.time * 2.3f + d.uid)));
                go.transform.position = RenderPoint(b, p);
                Vector3 upR = b.FixedToInertial(up, renderUt).ToFloat();
                Vector3 look = Vector3.ProjectOnPlane(renderRot * Vector3.forward, upR);
                var te = d.target != null ? EvaOf(d.target) : null;
                if (te != null) look = Vector3.ProjectOnPlane(RenderPoint(b, FixedPos(te)) - go.transform.position, upR);
                if (look.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(look, upR);
                var rotor = go.transform.Find("Rotor");
                if (rotor != null) { rotor.gameObject.SetActive(!asleepMimic); rotor.localRotation = Quaternion.Euler(0, Time.time * 900f, 0); }
                var eye = go.transform.Find("Eye");
                if (eye != null) eye.gameObject.SetActive(!asleepMimic);
            }
            foreach (var k in droneViews.Keys.ToList()) if (!want.Contains(k)) { if (droneViews[k] != null) UnityEngine.Object.Destroy(droneViews[k]); droneViews.Remove(k); }
        }

        GameObject BuildDroneView(DroneDef def)
        {
            var go = new GameObject(def.name);
            float s = def.size;
            var body = new MeshBuilder();
            if (def.kind == DroneKind.Mimic)
            {
                body.Mat = MeshBuilder.MatPaint;
                body.AddBox(new Vector3(0, 0.12f, 0), new Vector3(0.24f, 0.2f, 0.18f) * (s / 0.25f), new Color(0.75f, 0.6f, 0.4f), Quaternion.identity);
                body.AddBox(new Vector3(0, 0.12f, -0.091f * (s / 0.25f)), new Vector3(0.16f, 0.1f, 0.004f) * (s / 0.25f), Color.white, Quaternion.identity);
            }
            else
            {
                body.Mat = MeshBuilder.MatPaint;
                body.AddSphere(Vector3.zero, s * 0.5f, def.color, 14, 9);
                body.Mat = MeshBuilder.MatMatte;
                body.AddBox(new Vector3(0, -s * 0.35f, 0), new Vector3(s * 0.7f, s * 0.15f, s * 0.7f), def.color * 0.5f, Quaternion.identity);
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI / 2 + Mathf.PI / 4;
                    body.AddBox(new Vector3(Mathf.Cos(a) * s * 0.55f, s * 0.1f, Mathf.Sin(a) * s * 0.55f), new Vector3(s * 0.5f, s * 0.06f, s * 0.08f), new Color(0.2f, 0.2f, 0.22f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
                }
                if (def.kind == DroneKind.Warden) body.AddFrustum(s * 0.9f, s * 0.4f, 0.02f, s * 0.12f, new Color(0.9f, 0.85f, 0.95f), 10, true, false);   // an antenna
            }
            var bg = new GameObject("Body");
            bg.transform.SetParent(go.transform, false);
            bg.AddComponent<MeshFilter>().sharedMesh = body.ToMesh(def.name);
            bg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            if (def.kind != DroneKind.Mimic || true)
            {
                var rb = new MeshBuilder();
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI / 2 + Mathf.PI / 4;
                    rb.AddBox(new Vector3(Mathf.Cos(a) * s * 0.8f, s * 0.16f, Mathf.Sin(a) * s * 0.8f), new Vector3(s * 0.55f, 0.01f, s * 0.07f), new Color(0.15f, 0.15f, 0.15f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
                }
                var rg = new GameObject("Rotor");
                rg.transform.SetParent(go.transform, false);
                rg.AddComponent<MeshFilter>().sharedMesh = rb.ToMesh("Rotor");
                rg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                var eb = new MeshBuilder();
                eb.AddSphere(new Vector3(0, 0, s * 0.45f), s * 0.13f, new Color(1f, 0.25f, 0.2f) * 2f, 8, 6);
                var eg = new GameObject("Eye");
                eg.transform.SetParent(go.transform, false);
                eg.AddComponent<MeshFilter>().sharedMesh = eb.ToMesh("Eye");
                var er = eg.AddComponent<MeshRenderer>();
                er.sharedMaterial = Mats.Additive;
                er.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        /// <summary>Health bars over drones that are after someone, and what left click does.</summary>
        void DrawDroneHud(float W, float H)
        {
            if (active == null || mapView) return;
            var list = Coop.Active && remote ? Coop.Current.room.State.drones : drones;
            var cam = root.farCam;
            foreach (var d in list)
            {
                if (d.body != active.body.name || d.state == DroneState.Idle || d.state == DroneState.Down) continue;
                if (!droneViews.TryGetValue(d.uid, out var go) || go == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(go.transform.position + (renderRot * Vector3.up) * (d.Def.size + 0.4f));
                if (sp.z <= 0) continue;
                float x = sp.x / Ui.Scale, y = (Screen.height - sp.y) / Ui.Scale;
                var r = new Rect(x - 40, y - 16, 80, 14);
                Ui.Text(new Rect(x - 80, y - 34, 160, 18), $"<color=#ff9080>{d.Def.name}</color>", new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter });
                Ui.Bar(new Rect(r.x, r.y + 3, r.width, 7), d.health / d.Def.health, new Color(1f, 0.35f, 0.3f));
            }
            if (active.IsEva && active.landed && CarriedBy(MyArdlingName) == null)
            {
                var t = AimedDrone(active, out bool zapper);
                if (t != null) Ui.Shadowed(new Rect(W / 2 - 300, H * 0.7f, 600, 24), zapper ? $"Click: zap the {t.Def.name.ToLowerInvariant()}" : $"Click: kick the {t.Def.name.ToLowerInvariant()} (a zapper from the store works from further away)", new GUIStyle(Ui.BigCenter) { fontSize = 17 });
            }
        }

        void ClearDrones()
        {
            drones.Clear();
            placesAwake.Clear();
            wardensCalled.Clear();
            foreach (var go in droneViews.Values) if (go != null) UnityEngine.Object.Destroy(go);
            droneViews.Clear();
            foreach (var (go, _) in zapFx) if (go != null) UnityEngine.Object.Destroy(go);
            zapFx.Clear();
        }

        // ---------------------------------------------------------------- for tours

        public int TestDroneCount => drones.Count;
        public Drone TestNearestDrone() => drones.OrderBy(d => (d.Pos - FixedPos(active)).magnitude).FirstOrDefault();
        public void TestAttack(int uid) => DoAttack(MyArdlingName, uid);
        public void TestSpawnDrone(DroneKind k, float metres) { var at = FixedPos(active); var p = at + RandomFlat(at, metres); SpawnDrone(k, active.body, "test", p, p); }
    }
}
