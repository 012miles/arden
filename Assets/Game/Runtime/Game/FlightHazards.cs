using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 39, random hazards in flight (the rules are in Sim/Hazards.cs). Every few minutes (not while time-warping,
    /// in a lesson or a replay) something happens where you are, announced on screen: lightning strikes near you (it hurts
    /// Ardlings and damages parts); a dust devil crosses, flinging loose things and Ardlings; wind blows things about and
    /// tires walkers; a heat wave makes walkers hungry and engines hot; meteors fall (leaving meteorite chunks to pick up);
    /// a shimmer lifts loose things; a solar flare hurts anyone outside; micrometeoroids ping off the hull; the signal
    /// drops. The host's game runs them in a room, and the messages go to everyone.
    /// </summary>
    public partial class FlightMode
    {
        HazardDef hazard;
        float hazardT, hazardNextIn = -1, hazardWarnLeft, hazardTick, hazardHurtT;
        int hazardCount;
        Vector3d hazardAt, hazardDir;   // body-fixed: where it's centred (dust devil, shimmer) and which way it goes
        ParticleSystem hazardDust;
        GameObject hazardGlow;
        Light hazardLight;
        readonly List<(float t, Vector3d at, bool meteor)> hazardPending = new List<(float, Vector3d, bool)>();
        readonly List<(GameObject go, float until)> hazardFx = new List<(GameObject, float)>();
        static Material hazardSmokeMat;

        public string HazardName => hazard?.name;
        bool HazardBlackout => hazard != null && hazard.kind == HazardKind.Blackout;

        // ---------------------------------------------------------------- the clock

        void HazardsTick(float dt)
        {
            UpdateHazardFx();
            if (!SurvivalHere || paused || active == null || active.destroyed || replay != null || Lessons.Active || rollout != null || Universe.IsAutotest && !hazardsInTests) return;
            if (railsWarp || warp > 4) return;   // (nothing happens while you warp past it)
            bool easy = Words.Easy;
            if (hazard == null)
            {
                if (hazardNextIn < 0) hazardNextIn = Hazards.FirstGap * (easy ? 1.5f : 1f) * UnityEngine.Random.Range(0.7f, 1.3f);
                if (hazardWarnLeft > 0)
                {
                    hazardWarnLeft -= dt;
                    if (hazardWarnLeft <= 0) BeginHazard(pendingHazard);
                    return;
                }
                hazardNextIn -= dt;
                if (hazardNextIn > 0) return;
                var h = PickHazard();
                if (h == null) { hazardNextIn = 60; return; }
                if (h.warnSecs > 0) { pendingHazard = h; hazardWarnLeft = h.warnSecs; Show("Warning: " + h.warn, h.warnSecs, true); audio?.Clunk(0.2f); return; }
                BeginHazard(h);
                return;
            }
            hazardT += dt;
            RunHazard(dt, easy);
            if (hazardT >= hazard.duration) EndHazard();
        }

        HazardDef pendingHazard;
        bool hazardsInTests;

        /// <summary>What could happen right here, picked by weight.</summary>
        HazardDef PickHazard()
        {
            var v = active;
            var b = v.body;
            double ground = v.landed ? 0 : Math.Max(0, v.Altitude - LaunchSite.SurfaceHeight(b, v.pos.normalized));
            bool surface = v.landed || ground < 2000 && !b.isStar && !b.IsGasGiant;
            bool space = !v.landed && v.Altitude > Math.Max(b.atmosphereHeight, 20000) && !surface;
            bool air = b.atmosphereHeight > 0 && b.seaLevelDensity > 0.005;
            bool thick = b.seaLevelDensity > 0.3;
            return Hazards.Pick(Environment.TickCount ^ (hazardCount * 7919), thick, air, surface, space);
        }

        void BeginHazard(HazardDef h)
        {
            hazard = h;
            hazardT = 0;
            hazardTick = 0;
            hazardHurtT = 0;
            hazardCount++;
            hazardPending.Clear();
            Vector3d here = FixedPos(active), up = here.normalized;
            Vector3d e = Vector3d.Cross(new Vector3d(0, 1, 0), up);
            if (e.magnitude < 1e-6) e = new Vector3d(1, 0, 0);
            e = e.normalized;
            Vector3d n = Vector3d.Cross(up, e).normalized;
            double a = UnityEngine.Random.value * Math.PI * 2;
            Vector3d side = e * Math.Cos(a) + n * Math.Sin(a);
            switch (h.kind)
            {
                case HazardKind.DustDevil:
                    // starts 140 m off and crosses close by
                    hazardAt = here + side * 140 + Vector3d.Cross(up, side).normalized * UnityEngine.Random.Range(-15f, 15f);
                    hazardDir = -side;
                    hazardDust = MakeDust(new Color(0.62f, 0.5f, 0.36f, 0.55f), true);
                    break;
                case HazardKind.Gust:
                    hazardDir = side;
                    hazardDust = MakeDust(new Color(0.7f, 0.62f, 0.5f, 0.25f), false);
                    hazardDust.transform.rotation = Quaternion.LookRotation(active.body.FixedToInertial(side, renderUt).ToFloat(), renderRot * Vector3.up);   // (blowing the wind's way)
                    break;
                case HazardKind.MeteorShower:
                    for (int i = 0; i < 15; i++)
                    {
                        double aa = UnityEngine.Random.value * Math.PI * 2, r = UnityEngine.Random.value < 0.2f ? UnityEngine.Random.Range(4f, 25f) : UnityEngine.Random.Range(30f, 250f);
                        hazardPending.Add((UnityEngine.Random.Range(1f, h.duration - 3f), here + (e * Math.Cos(aa) + n * Math.Sin(aa)) * r, true));
                    }
                    break;
                case HazardKind.Shimmer:
                {
                    hazardAt = here + side * UnityEngine.Random.Range(12f, 35f);
                    var b = new MeshBuilder();
                    b.AddSphere(Vector3.zero, 1.2f, new Color(0.55f, 0.8f, 1f, 0.35f), 16, 10, Mathf.PI, true);
                    b.AddSphere(Vector3.zero, 0.5f, new Color(0.85f, 0.95f, 1f, 0.8f), 12, 8);
                    hazardGlow = new GameObject("Shimmer");
                    hazardGlow.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Shimmer");
                    var mr = hazardGlow.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Mats.Additive;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    hazardLight = hazardGlow.AddComponent<Light>();
                    hazardLight.type = LightType.Point; hazardLight.range = 40; hazardLight.intensity = 3; hazardLight.color = new Color(0.6f, 0.85f, 1f);
                    break;
                }
                case HazardKind.HeatWave: VesselSim.HeatBoost = 1.5f; break;
            }
            Show(h.start, 6f, true);
            audio?.Clunk(0.25f);
            if (Coop.Active && Coop.Current.IsHost) { Coop.Current.room.State.hazard = h.name; Coop.Current.room.Touch(); }
        }

        void EndHazard()
        {
            if (hazard == null) return;
            if (!string.IsNullOrEmpty(hazard.end)) Show(hazard.end, 3f);
            if (hazardT >= hazard.duration - 0.5f && active != null && !active.destroyed) Quest(QuestKind.Survive, 1);   // milestone 40
            if (hazard.kind == HazardKind.HeatWave) VesselSim.HeatBoost = 1f;
            hazard = null;
            hazardWarnLeft = 0;
            hazardPending.Clear();
            if (Coop.Active && Coop.Current.IsHost) { Coop.Current.room.State.hazard = null; Coop.Current.room.Touch(); }
            if (hazardDust != null) { var em = hazardDust.emission; em.enabled = false; UnityEngine.Object.Destroy(hazardDust.gameObject, 4f); hazardDust = null; }
            if (hazardGlow != null) UnityEngine.Object.Destroy(hazardGlow);
            hazardGlow = null; hazardLight = null;
            hazardNextIn = UnityEngine.Random.Range(Hazards.MinGap, Hazards.MaxGap) * (Words.Easy ? 1.5f : 1f);
        }

        // ---------------------------------------------------------------- what each one does

        void RunHazard(float dt, bool easy)
        {
            float hurtK = easy ? 0.5f : 1f;
            var b = active.body;
            Vector3d here = FixedPos(active);
            hazardTick -= dt;
            hazardHurtT -= dt;
            switch (hazard.kind)
            {
                case HazardKind.Lightning:
                    if (hazardTick <= 0)
                    {
                        hazardTick = UnityEngine.Random.Range(2.5f, 5f);
                        Vector3d up = here.normalized, e = Vector3d.Cross(new Vector3d(0, 1, 0), up).normalized, n = Vector3d.Cross(up, e).normalized;
                        double a = UnityEngine.Random.value * Math.PI * 2, r = UnityEngine.Random.value < 0.25f ? UnityEngine.Random.Range(3f, 15f) : UnityEngine.Random.Range(25f, 130f);
                        Strike(here + (e * Math.Cos(a) + n * Math.Sin(a)) * r, hurtK);
                    }
                    break;
                case HazardKind.DustDevil:
                {
                    Vector3d up = hazardAt.normalized;
                    hazardAt = hazardAt + (hazardDir - up * Vector3d.Dot(hazardDir, up)).normalized * (7.0 * dt);
                    var dir = hazardAt.normalized;
                    hazardAt = dir * (b.radius + LaunchSite.SurfaceHeight(b, dir));
                    if (hazardDust != null) hazardDust.transform.position = RenderPoint(b, hazardAt);
                    foreach (var w in U.items.Where(x => x.body == b.name && (x.Pos - hazardAt).magnitude < 12).ToList())
                    {
                        Vector3d off = w.Pos - hazardAt, wu = w.Pos.normalized;
                        Vector3d swirl = Vector3d.Cross(wu, off).normalized;
                        w.flying = true;
                        w.Vel = swirl * 6 + wu * 4;
                    }
                    if (hazardHurtT <= 0)
                    {
                        hazardHurtT = 1f;
                        foreach (var e in U.vessels.Where(v => !v.destroyed && v.IsEva && v.body == b && v.landed).ToList())
                        {
                            Vector3d ep = FixedPos(e);
                            double d = (ep - hazardAt).magnitude;
                            if (d > 10) continue;
                            Vector3d swirl = Vector3d.Cross(ep.normalized, ep - hazardAt).normalized;
                            e.Fling(b.FixedToInertial(swirl * 5 + ep.normalized * 3, ut));
                            if (e.EvaName != null) HurtArdling(U.career.VitalsOf(e.EvaName), 8 * hurtK, e, "the dust devil");
                        }
                    }
                    break;
                }
                case HazardKind.Gust:
                    if (hazardDust != null) hazardDust.transform.position = RenderPoint(b, here + here.normalized * 3) - (Vector3)(b.FixedToInertial(hazardDir, renderUt).ToFloat() * 20f);
                    if (hazardTick <= 0)
                    {
                        hazardTick = 1.5f;
                        foreach (var w in U.items.Where(x => x.body == b.name && !x.flying && (x.Pos - here).magnitude < 60).ToList())
                            if (UnityEngine.Random.value < 0.35f) { w.flying = true; w.Vel = hazardDir * UnityEngine.Random.Range(2f, 4f) + w.Pos.normalized * 1.5; }
                    }
                    foreach (var e in U.vessels.Where(v => !v.destroyed && v.IsEva && v.body == b && v.landed && v.EvaName != null))
                    {
                        var vit = U.career.VitalsOf(e.EvaName);
                        if (e.inputPitch != 0 || e.evaStrafe != 0) vit.stamina = Mathf.Max(0, vit.stamina - 4f * dt);
                    }
                    break;
                case HazardKind.HeatWave:
                    foreach (var e in U.vessels.Where(v => !v.destroyed && v.IsEva && v.EvaName != null))
                    {
                        var vit = U.career.VitalsOf(e.EvaName);
                        vit.hunger = Mathf.Max(0, vit.hunger - 0.5f * dt);
                    }
                    break;
                case HazardKind.MeteorShower:
                    for (int i = hazardPending.Count - 1; i >= 0; i--)
                    {
                        if (hazardPending[i].t > hazardT) continue;
                        var at = hazardPending[i].at;
                        hazardPending.RemoveAt(i);
                        Meteor(at, hurtK);
                    }
                    break;
                case HazardKind.Shimmer:
                {
                    var dir = hazardAt.normalized;
                    Vector3d c = dir * (b.radius + LaunchSite.SurfaceHeight(b, dir) + 3 + Math.Sin(hazardT * 2) * 0.5);
                    if (hazardGlow != null)
                    {
                        hazardGlow.transform.position = RenderPoint(b, c);
                        hazardGlow.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(hazardT * 5f));
                        if (hazardLight != null) hazardLight.intensity = 2.5f + Mathf.Sin(hazardT * 7f);
                    }
                    if (hazardT < hazard.duration - 3)
                    {
                        foreach (var w in U.items.Where(x => x.body == b.name && (x.Pos - c).magnitude < 25))
                        {
                            w.flying = true;
                            w.Vel = w.Pos.normalized * 0.8 + (c - w.Pos).normalized * 0.2;
                        }
                        foreach (var e in U.vessels.Where(v => !v.destroyed && v.IsEva && v.body == b).ToList())
                        {
                            if ((FixedPos(e) - c).magnitude > 12) continue;
                            if (e.landed) e.Fling(b.FixedToInertial(FixedPos(e).normalized * 1.2, ut));
                        }
                    }
                    break;
                }
                case HazardKind.SolarFlare:
                    foreach (var e in U.vessels.Where(v => !v.destroyed && v.IsEva && v.EvaName != null).ToList())
                        HurtArdling(U.career.VitalsOf(e.EvaName), 2.2f * hurtK * dt, e, "the solar flare");
                    break;
                case HazardKind.Micrometeoroids:
                    if (hazardTick <= 0)
                    {
                        hazardTick = UnityEngine.Random.Range(5f, 10f);
                        if (active.IsEva) { if (active.EvaName != null) HurtArdling(U.career.VitalsOf(active.EvaName), 15 * hurtK, active, "a micrometeoroid"); }
                        else if (active.parts.Count > 0)
                        {
                            var p = active.parts[UnityEngine.Random.Range(0, active.parts.Count)];
                            active.DamagePart(p, UnityEngine.Random.Range(0.08f, 0.25f) * (easy ? 0.5f : 1f), "a micrometeoroid");
                            Show(active.lastEvent, 3f);
                            audio?.Clunk(0.5f);
                        }
                    }
                    break;
            }
        }

        /// <summary>A lightning strike: a bolt, a flash, thunder a moment later; it hurts walkers and damages ships nearby.</summary>
        void Strike(Vector3d at, float hurtK)
        {
            var b = active.body;
            var dir = at.normalized;
            at = dir * (b.radius + LaunchSite.SurfaceHeight(b, dir));
            Vector3 ground = RenderPoint(b, at), up = b.FixedToInertial(dir, renderUt).ToFloat().normalized;
            var go = new GameObject("Lightning");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Mats.Additive;
            lr.widthMultiplier = 0.9f;
            lr.positionCount = 12;
            Vector3 side = Vector3.Cross(up, Vector3.right).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(up, Vector3.forward).normalized;
            Vector3 side2 = Vector3.Cross(up, side);
            for (int i = 0; i < 12; i++)
            {
                float f = i / 11f;
                Vector3 jag = i == 0 || i == 11 ? Vector3.zero : (side * UnityEngine.Random.Range(-12f, 12f) + side2 * UnityEngine.Random.Range(-12f, 12f)) * (1 - f * 0.6f);
                lr.SetPosition(i, ground + up * (900f * (1 - f)) + jag);
            }
            lr.startColor = new Color(0.8f, 0.9f, 1f, 1f); lr.endColor = new Color(1f, 1f, 1f, 1f);
            var lg = new GameObject("Flash");
            lg.transform.position = ground + up * 30f;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 400; l.intensity = 6; l.color = new Color(0.85f, 0.9f, 1f);
            hazardFx.Add((go, Time.unscaledTime + 0.18f));
            hazardFx.Add((lg, Time.unscaledTime + 0.12f));
            double dist = (at - FixedPos(active)).magnitude;
            audio?.Clunk(Mathf.Clamp01(1.2f - (float)dist / 200f));
            // walkers nearby
            foreach (var e in U.vessels.Where(v => !v.destroyed && v.IsEva && v.body == b && v.EvaName != null).ToList())
            {
                double d = (FixedPos(e) - at).magnitude;
                if (d < 10) HurtArdling(U.career.VitalsOf(e.EvaName), (float)(15 + 45 * (1 - d / 10)) * hurtK, e, "lightning");
            }
            // ships nearby
            foreach (var s in U.vessels.Where(v => !v.destroyed && !v.IsEva && !v.IsFlag && v.body == b && v.landed && v.parts.Count > 0).ToList())
            {
                double d = (FixedPos(s) - at).magnitude;
                if (d > 8 + s.Length * 0.5) continue;
                var p = s.parts[UnityEngine.Random.Range(0, s.parts.Count)];
                s.DamagePart(p, UnityEngine.Random.Range(0.25f, 0.6f) * hurtK, "lightning");
                p.temp += 300;
                Show($"Lightning hit {s.name}! {s.lastEvent}", 4f, true);
            }
        }

        /// <summary>A meteor: a streak down, an impact; it hurts walkers and damages ships nearby; some leave a chunk to pick up.</summary>
        void Meteor(Vector3d at, float hurtK)
        {
            var b = active.body;
            var dir = at.normalized;
            at = dir * (b.radius + LaunchSite.SurfaceHeight(b, dir));
            Vector3 ground = RenderPoint(b, at), up = b.FixedToInertial(dir, renderUt).ToFloat().normalized;
            var go = new GameObject("Meteor");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Mats.Additive;
            lr.widthMultiplier = 1.4f;
            lr.positionCount = 2;
            Vector3 slant = Vector3.Cross(up, Vector3.right).normalized * 120f;
            lr.SetPosition(0, ground + up * 160f + slant * 0.4f);
            lr.SetPosition(1, ground + up * 2f);
            lr.startColor = new Color(1f, 0.6f, 0.2f, 0f); lr.endColor = new Color(1f, 0.85f, 0.5f, 1f);
            hazardFx.Add((go, Time.unscaledTime + 0.25f));
            SpawnBoom(b, b.FixedToInertial(at, ut), 3.5f);
            foreach (var e in U.vessels.Where(v => !v.destroyed && v.IsEva && v.body == b && v.EvaName != null).ToList())
            {
                double d = (FixedPos(e) - at).magnitude;
                if (d < 12) HurtArdling(U.career.VitalsOf(e.EvaName), (float)(10 + 55 * (1 - d / 12)) * hurtK, e, "a meteorite");
            }
            foreach (var s in U.vessels.Where(v => !v.destroyed && !v.IsEva && !v.IsFlag && v.body == b && v.parts.Count > 0).ToList())
            {
                double d = (FixedPos(s) - at).magnitude;
                if (d > 15) continue;
                var p = s.parts[UnityEngine.Random.Range(0, s.parts.Count)];
                s.DamagePart(p, UnityEngine.Random.Range(0.4f, 0.9f) * hurtK, "a meteorite");
                Show($"A meteorite hit {s.name}! {s.lastEvent}", 4f, true);
            }
            if (UnityEngine.Random.value < 0.35f) SettleItem(U.AddItem("meteorite", b, at + dir * 0.3, UnityEngine.Random.Range(0f, 360f)), b);
        }

        // ---------------------------------------------------------------- the look

        Vector3 RenderPoint(CelestialBody b, Vector3d fixedPos) => (b.AbsolutePosition(renderUt) + b.FixedToInertial(fixedPos, renderUt) - originAbs).ToFloat();

        ParticleSystem MakeDust(Color c, bool column)
        {
            var go = new GameObject(column ? "Dust devil" : "Wind dust");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.duration = 5; main.startLifetime = column ? 3f : 2.5f; main.startSpeed = column ? 2f : 9f;
            main.startSize = column ? new ParticleSystem.MinMaxCurve(2f, 5f) : new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startColor = c; main.maxParticles = 600; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = column ? 90 : 40;
            var sh = ps.shape;
            sh.shapeType = column ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Box;
            if (column) { sh.angle = 8; sh.radius = 2.5f; }
            else sh.scale = new Vector3(60, 6, 4);
            var vel = ps.velocityOverLifetime;
            vel.enabled = column;
            if (column) { vel.orbitalY = 2.5f; vel.y = 6f; vel.space = ParticleSystemSimulationSpace.Local; }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(c.a, 0.2f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (hazardSmokeMat == null) hazardSmokeMat = new Material(Resources.Load<Shader>("Shaders/RocketSmoke")) { name = "Hazard dust" };
            r.sharedMaterial = hazardSmokeMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            // point the column up, out of the ground
            go.transform.rotation = Quaternion.FromToRotation(Vector3.forward, renderRot * Vector3.up);
            ps.Play();
            return ps;
        }

        void UpdateHazardFx()
        {
            for (int i = hazardFx.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < hazardFx[i].until) continue;
                if (hazardFx[i].go != null) UnityEngine.Object.Destroy(hazardFx[i].go);
                hazardFx.RemoveAt(i);
            }
        }

        /// <summary>A banner while one's going on, and a tint for heat waves and solar flares.</summary>
        void DrawHazardHud(float W, float H)
        {
            string name = HazardName;
            float left = hazard != null ? hazard.duration - hazardT : 0;
            if (Coop.Active && remote) { name = Coop.Current.room.State.hazard; left = 0; }
            if (string.IsNullOrEmpty(name)) return;
            var kind = Hazards.All.FirstOrDefault(h => h.name == name)?.kind ?? HazardKind.None;
            if (kind == HazardKind.HeatWave || kind == HazardKind.SolarFlare)
            {
                float a = kind == HazardKind.SolarFlare ? 0.1f + 0.05f * Mathf.Sin(Time.time * 4f) : 0.07f;
                var was = GUI.color;
                GUI.color = kind == HazardKind.SolarFlare ? new Color(1f, 0.85f, 0.6f, a) : new Color(1f, 0.55f, 0.2f, a);
                GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                GUI.color = was;
            }
            var r = new Rect(W / 2 - 150, 72, 300, 26);
            Ui.Box(r);
            Ui.Text(r, $"<color=#ffb050><b>!</b></color>  <b>{name}</b>" + (left > 0 ? $"  <color=#8a929c>{left:0} s</color>" : ""), new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter });
        }

        void ClearHazards()
        {
            if (hazard != null && hazard.kind == HazardKind.HeatWave) VesselSim.HeatBoost = 1f;
            hazard = null;
            pendingHazard = null;
            hazardWarnLeft = 0;
            hazardNextIn = -1;
            hazardPending.Clear();
            if (hazardDust != null) UnityEngine.Object.Destroy(hazardDust.gameObject);
            hazardDust = null;
            if (hazardGlow != null) UnityEngine.Object.Destroy(hazardGlow);
            hazardGlow = null;
            foreach (var (go, _) in hazardFx) if (go != null) UnityEngine.Object.Destroy(go);
            hazardFx.Clear();
        }

        // ---------------------------------------------------------------- for tours

        public void TestHazard(HazardKind k) { hazardsInTests = true; var h = Hazards.Get(k); if (h == null) return; if (hazard != null) EndHazard(); BeginHazard(h); }
        public void TestEndHazard() { EndHazard(); hazardsInTests = false; }
    }
}
