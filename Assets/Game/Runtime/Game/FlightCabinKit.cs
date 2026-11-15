using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 36 (captaincarl24: "more detailed bro, search online to see how other popular games did it and do it"). What the
    /// research turned up, and what this file does with it:
    /// - Cockpits are built from a kit of props used again and again (the way KSP's interior builders use prop packs, and Star
    ///   Citizen mixes the same parts per ship): toggle switches, guarded switches, circuit breakers, knobs, backlit buttons,
    ///   needle gauges and lamps, placed in panels. Every one of them can be pressed.
    /// - The flight's numbers live on real instruments in the cabin (like EA's Squadrons: no floating HUD): needle gauges for
    ///   fuel, power, climb, g and heat, and an attitude ball that turns with the ship.
    /// - Layered light (as flight sims split panel, flood and dome lights): a FLOOD knob, a PANEL knob for the backlit labels,
    ///   and a LAMP TEST button that lights every lamp. A MASTER ALARM flashes and beeps for new trouble until you press it.
    /// - Shake the cockpit, not the camera (Squadrons again: shaking the view makes people sick): under thrust, in thick air
    ///   and in reentry the loose things rattle, cables sway and the lights flicker; the view stays steady. Reentry glows
    ///   orange in the windows.
    /// - Real spacecraft are crowded and lived in (the ISS and Apollo cabins): cloth stowage bags with straps and tags,
    ///   Velcro strips holding a tablet, a camera and a torch, bungee cords with food and drink pouches, a family photo, a
    ///   mission patch, cable bundles with zip ties, quilted wall pads, placards and sticky notes; vent streamers that
    ///   flutter in the airflow; and in weightlessness the loose things float about (push them!), falling to the floor
    ///   when there's weight again.
    /// </summary>
    public partial class FlightMode
    {
        class Gauge { public Transform needle; public Func<float> value; public float shown; }
        class Floater { public Transform t; public Vector3 vel, spin; public float r, restH; public Quaternion restTilt; public Prop prop; }
        class Rattler { public Transform t; public Vector3 basePos; public Quaternion baseRot; public float amount, seed; }

        readonly List<Gauge> gauges = new List<Gauge>();
        readonly List<Floater> floaters = new List<Floater>();
        readonly List<Rattler> rattlers = new List<Rattler>();
        readonly List<(Transform t, float seed, Quaternion rest)> streamers = new List<(Transform, float, Quaternion)>();
        readonly List<(TextMesh tm, Color c)> kitLabels = new List<(TextMesh, Color)>();
        readonly List<(Mesh mesh, Color c)> plasma = new List<(Mesh, Color)>();
        readonly List<(GameObject go, Func<bool> on)> kitLights = new List<(GameObject, Func<bool>)>();
        readonly Dictionary<string, Prop> propById = new Dictionary<string, Prop>();
        readonly HashSet<string> alarmAcked = new HashSet<string>();
        Transform miniBall;
        AudioSource cabinAudio, cabinHum;
        TextMesh keypadScreen;
        string keypadText = "", keypadShow = "";
        float lampTestUntil, alarmBeepT, vibration, lastAmbient = -1;
        bool alarmFlashing;
        static Material miniBallMat;

        // milestone 37: cabin surfaces drawn by RocketLit (ids 11-16), and the photographed bright metal (id 2) shaded as paint
        static readonly Vector4 MatFabric = new Vector4(11, 0, 0.1f, 0), MatRubber = new Vector4(12, 0, 0.2f, 0), MatPanel = new Vector4(13, 0, 0.42f, 0),
            MatQuilt = new Vector4(14, 0, 0.1f, 0), MatPerf = new Vector4(15, 0, 0.35f, 0), MatVelcro = new Vector4(16, 0, 0.03f, 0), MatBrushed = new Vector4(2, 0, 0.55f, 0);
        readonly List<(Light l, float i, Color c)> cabinLamps = new List<(Light, float, Color)>();
        readonly List<(Mesh mesh, Vector3 mid, Vector3 n, Vector3 t, float w, float hh, Prop blind)> sunPatches = new List<(Mesh, Vector3, Vector3, Vector3, float, float, Prop)>();
        readonly List<Prop> windowBlinds = new List<Prop>();

        /// <summary>Lit by the cabin's lamps, not the sun through the walls (the crew, the plushie).</summary>
        static void IndoorLit(GameObject go)
        {
            var pb = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.sharedMaterial != Mats.Lit) continue;
                r.GetPropertyBlock(pb);
                pb.SetFloat("_SunScale", 0.08f);
                pb.SetFloat("_Ambient", 0.24f);
                r.SetPropertyBlock(pb);
            }
        }

        /// <summary>
        /// Real lamps (milestone 37): point lights that only light the cabin, so light pools under them and fades into the
        /// corners. The ceiling lamp(s) follow the light switch, the flood lamp the FLOOD knob, and the screens glow blue.
        /// </summary>
        void BuildCabinLamps(bool hab, float h, float front)
        {
            cabinLamps.Clear();
            void AddLamp(string name, Vector3 at, Color c, float intensity, float range)
            {
                var go = new GameObject(name) { layer = InteriorLayer };
                go.transform.SetParent(room.transform, false);
                go.transform.localPosition = at;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
                l.cullingMask = 1 << InteriorLayer; l.renderMode = LightRenderMode.ForcePixel; l.shadows = LightShadows.None;
                cabinLamps.Add((l, intensity, c));
            }
            Color warm = new Color(1f, 0.92f, 0.8f);
            if (hab) foreach (float z in new[] { -roomL / 2 + 1.5f, -roomL / 2 + 4f, -roomL / 2 + 6.5f }) AddLamp("Ceiling lamp", new Vector3(0, h - 0.15f, z), warm, 2.6f, 4.5f);
            else AddLamp("Ceiling lamp", new Vector3(0, h - 0.2f, -0.1f), warm, 3.0f, 5f);
            AddLamp("Flood lamp", new Vector3(0, h - 0.35f, front - 0.95f), new Color(1f, 0.96f, 0.9f), 1.3f, 2.8f);
            AddLamp("Screen glow", new Vector3(0, 0.78f, front - 0.5f), new Color(0.45f, 0.75f, 1f), 0.4f, 1.3f);
        }

        void UpdateCabinLamps()
        {
            float flood = BreakerOff("flood") ? 0 : KitValue("knob_flood", 0.3f);
            bool flickering = cabinLights != null && cabinLightMode != 1 && !cabinLights.activeSelf;
            foreach (var (l, i0, c0) in cabinLamps)
            {
                if (l == null) continue;
                float k = 1;
                Color c = c0;
                if (l.name == "Flood lamp") k = flood / 0.3f * (cabinLightMode == 1 ? 0.35f : 1f);
                else if (l.name == "Screen glow") k = BreakerOff("panel") ? 0.25f : 1f;
                else
                {
                    k = cabinLightMode == 0 ? 1f : cabinLightMode == 1 ? 0f : 0.5f;
                    if (cabinLightMode == 2) c = new Color(1f, 0.2f, 0.14f);
                    if (flickering) k *= 0.12f;
                }
                if (LampTest) k = Mathf.Max(k, 1f);
                l.intensity = i0 * k;
                l.color = c;
            }
        }

        /// <summary>Sunlight through each window: a bright patch on the floor that moves as the ship turns (milestone 37).</summary>
        void BuildSunPatches(float h)
        {
            sunPatches.Clear();
            var wins = Windows(h).ToList();
            for (int i = 0; i < wins.Count; i++)
            {
                var (c, n, w, sill, head) = wins[i];
                var go = new GameObject("Sun patch") { layer = InteriorLayer };
                go.transform.SetParent(room.transform, false);
                var mesh = new Mesh { name = "Sun patch" };
                mesh.MarkDynamic();
                mesh.vertices = new Vector3[4];
                mesh.colors = new Color[4];
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = cabinGlow;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                sunPatches.Add((mesh, c + Vector3.up * (sill + head) / 2, n, new Vector3(n.z, 0, -n.x), w, head - sill, i < windowBlinds.Count ? windowBlinds[i] : null));
            }
        }

        void UpdateSunPatches()
        {
            var sun = root.sun;
            float sunI = sun != null && sun.enabled && sun.gameObject.activeInHierarchy ? sun.intensity * Mathf.Max(sun.color.r, Mathf.Max(sun.color.g, sun.color.b)) : 0;
            Vector3 s = sun != null ? Quaternion.Inverse(renderRot) * sun.transform.forward : Vector3.down;   // the way the light travels, in the cabin
            var verts = new Vector3[4];
            var cols = new Color[4];
            foreach (var sp in sunPatches)
            {
                float facing = Vector3.Dot(s, sp.n);
                bool lit = sunI > 0.05f && s.y < -0.08f && facing > 0.05f && (sp.blind == null || sp.blind.state < 0.5f);
                float a = lit ? Mathf.Clamp01(sunI / 2.5f) * 0.3f * Mathf.Clamp01(facing * 2.5f) : 0;
                if (a > 0.003f)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 corner = sp.mid + sp.t * (sp.w / 2) * (k == 0 || k == 3 ? -1 : 1) + Vector3.up * (sp.hh / 2) * (k < 2 ? -1 : 1);
                        Vector3 hit = corner + s * ((corner.y - 0.007f) / -s.y);
                        if (roomRound) { var f = new Vector2(hit.x, hit.z); float lim = roomR * 0.95f; if (f.magnitude > lim) { f = f.normalized * lim; hit.x = f.x; hit.z = f.y; } }
                        else { hit.x = Mathf.Clamp(hit.x, -roomW / 2 + 0.02f, roomW / 2 - 0.02f); hit.z = Mathf.Clamp(hit.z, -roomL / 2 + 0.02f, roomL / 2 - 0.02f); }
                        verts[k] = hit;
                        cols[k] = new Color(1f, 0.9f, 0.72f, a);
                    }
                }
                else for (int k = 0; k < 4; k++) { verts[k] = sp.mid; cols[k] = Color.clear; }
                sp.mesh.vertices = verts;
                sp.mesh.colors = cols;
                sp.mesh.RecalculateBounds();
            }
        }

        // ---------------------------------------------------------------- the kit

        float KitValue(string id, float dflt) => propById.TryGetValue(id, out var p) ? p.state : dflt;
        bool BreakerOff(string id) => KitValue("brk_" + id, 0) > 0.5f;
        bool LampTest => Time.unscaledTime < lampTestUntil;
        Vector3 RoomPoint(Transform t, Vector3 local) => room.transform.InverseTransformPoint(t.TransformPoint(local));

        Transform PanelRoot(string name, Vector3 at, Quaternion rot, Transform parent = null)
        {
            var go = new GameObject(name) { layer = InteriorLayer };
            go.transform.SetParent(parent ?? room.transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rot;
            return go.transform;
        }

        GameObject PieceMat(string name, Vector3 at, Quaternion rot, Action<MeshBuilder> build, Material mat, Transform parent = null)
        {
            var go = Piece(name, at, rot, build, false, parent);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        TextMesh KitLabel(Transform p, Vector2 at, string text, float h, Color col, TextAnchor anchor = TextAnchor.MiddleCenter, float z = -0.002f)
        {
            var tm = Label(new Vector3(at.x, at.y, z), Quaternion.identity, text, h, col, anchor, p);
            kitLabels.Add((tm, col));
            return tm;
        }

        void KitSound(string clip, float vol = 0.5f)
        {
            if (cabinAudio == null || GameAudio.I == null) return;
            var c = GameAudio.I.Clip(clip);
            if (c != null) cabinAudio.PlayOneShot(c, vol * Mathf.Lerp(0.2f, 1.6f, KitValue("knob_volume", 0.6f)));
        }

        Prop KitProp(string id, string name, Transform p, Vector2 at, float size, Func<string> prompt, Action use, bool shared = false, string sound = "clink")
        {
            var pr = AddProp(id, name, RoomPoint(p, new Vector3(at.x, at.y, -0.03f)), prompt, use, size, shared);
            pr.sound = sound;
            propById[id] = pr;
            return pr;
        }

        /// <summary>A toggle switch (its lever flips up and down), optionally under a hinged guard.</summary>
        Prop KitToggle(Transform p, Vector2 at, string id, string label, Func<bool> on, Action act, bool guard = false)
        {
            Piece("Toggle base", new Vector3(at.x, at.y, 0), Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(new Vector3(0, 0, -0.003f), new Vector3(0.034f, 0.05f, 0.006f), C(0.12f, 0.12f, 0.13f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0, -0.008f), new Vector3(0.016f, 0.016f, 0.006f), C(0.55f, 0.56f, 0.58f), Quaternion.identity);
            }, false, p);
            var lever = Piece("Toggle", new Vector3(at.x, at.y, -0.01f), Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0, 0, -0.018f), new Vector3(0.007f, 0.007f, 0.036f), C(0.85f, 0.86f, 0.88f), Quaternion.identity);
                b.AddSphere(new Vector3(0, 0, -0.037f), 0.0065f, C(0.9f, 0.9f, 0.92f), 8, 5);
            }, false, p);
            if (guard)
                Piece("Guard", new Vector3(at.x, at.y, 0), Quaternion.identity, b =>
                {
                    b.Mat = MeshBuilder.MatPaint;
                    foreach (int s in new[] { -1, 1 }) b.AddBox(new Vector3(s * 0.022f, 0, -0.022f), new Vector3(0.004f, 0.05f, 0.044f), C(0.85f, 0.15f, 0.12f), Quaternion.identity);
                    b.AddBox(new Vector3(0, 0.026f, -0.022f), new Vector3(0.048f, 0.004f, 0.044f), C(0.85f, 0.15f, 0.12f), Quaternion.identity);
                }, false, p);
            KitLabel(p, at + new Vector2(0, -0.04f), label, 0.012f, C(0.92f, 0.92f, 0.94f));
            var pr = KitProp(id, label, p, at, 0.03f, () => $"E: {label} {(on() ? "off" : "on")}", act);
            pr.moving = lever.transform;
            pr.animate = (pp, dt) => { pp.shown = Mathf.MoveTowards(pp.shown, on() ? 1f : 0f, dt * 10f); pp.moving.localRotation = Quaternion.Euler(Mathf.Lerp(28f, -28f, pp.shown), 0, 0); };
            return pr;
        }

        /// <summary>A circuit breaker: push it in for power, pull it out (a white collar shows) to cut it.</summary>
        Prop KitBreaker(Transform p, Vector2 at, string key, string label, Action<bool> changed = null)
        {
            string id = "brk_" + key;
            Piece("Breaker collar", new Vector3(at.x, at.y, 0), Quaternion.Euler(-90, 0, 0), b =>
            {
                b.Mat = MeshBuilder.MatMatte;
                b.AddFrustum(0.004f, 0f, 0.012f, 0.012f, C(0.2f, 0.2f, 0.22f), 12, true, false);
            }, false, p);
            var knob = Piece("Breaker", new Vector3(at.x, at.y, -0.004f), Quaternion.Euler(-90, 0, 0), b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddFrustum(0.006f, -0.006f, 0.0065f, 0.0065f, C(0.95f, 0.95f, 0.95f), 10, false, false);   // the white collar (hidden when pushed in)
                b.AddFrustum(0.016f, 0.006f, 0.0075f, 0.0075f, C(0.08f, 0.08f, 0.09f), 10, true, false);
            }, false, p);
            KitLabel(p, at + new Vector2(0, 0.026f), label, 0.0095f, C(0.9f, 0.9f, 0.92f));
            Prop pr = null;
            pr = KitProp(id, label + " breaker", p, at, 0.022f, () => pr.state > 0.5f ? $"E: push in the {label} breaker (power back)" : $"E: pull the {label} breaker (power off)", null, true, "thud");
            pr.use = () => { pr.state = pr.state > 0.5f ? 0 : 1; SendCabin(pr); changed?.Invoke(pr.state < 0.5f); };
            pr.moving = knob.transform;
            pr.animate = (pp, dt) => { pp.shown = Mathf.MoveTowards(pp.shown, pp.state, dt * 12f); pp.moving.localPosition = new Vector3(at.x, at.y, -0.004f - 0.009f * pp.shown); };
            return pr;
        }

        /// <summary>A rotary knob with a pointer: scroll to turn it, E steps it round.</summary>
        Prop KitKnob(Transform p, Vector2 at, string key, string label, float start, Func<float, string> readout)
        {
            string id = "knob_" + key;
            Piece("Knob skirt", new Vector3(at.x, at.y, 0), Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatMatte;
                for (int k = 0; k <= 6; k++)
                {
                    float a = Mathf.Lerp(135, -135, k / 6f);
                    b.AddBox(Quaternion.Euler(0, 0, a) * new Vector3(0, 0.032f, 0) + new Vector3(0, 0, -0.002f), new Vector3(0.003f, 0.008f, 0.002f), C(0.9f, 0.9f, 0.92f), Quaternion.Euler(0, 0, a));
                }
            }, false, p);
            var knob = Piece("Knob", new Vector3(at.x, at.y, 0), Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                int s0 = b.v.Count;
                b.AddFrustum(0.018f, 0f, 0.019f, 0.022f, C(0.1f, 0.1f, 0.11f), 16, true, false);
                XfLocal(b, s0, Vector3.zero, Quaternion.Euler(-90, 0, 0));
                b.AddBox(new Vector3(0, 0.011f, -0.0185f), new Vector3(0.004f, 0.016f, 0.002f), C(0.95f, 0.95f, 0.95f), Quaternion.identity);
            }, false, p);
            KitLabel(p, at + new Vector2(0, -0.046f), label, 0.011f, C(0.92f, 0.92f, 0.94f));
            Prop pr = null;
            pr = KitProp(id, label + " knob", p, at, 0.03f, () => $"Scroll to turn {label}  ·  E: step it  (now {readout(pr.state)})", null, true, "ui_click");
            pr.state = start; pr.shown = start;
            pr.use = () => { pr.state = pr.state >= 0.99f ? 0 : Mathf.Min(1, pr.state + 0.25f); SendCabin(pr); };
            pr.scroll = sc => { pr.state = Mathf.Clamp01(pr.state + (sc > 0 ? 0.05f : -0.05f)); SendCabin(pr); KitSound("ui_click", 0.15f); };
            pr.moving = knob.transform;
            pr.animate = (pp, dt) => { pp.shown = Mathf.MoveTowards(pp.shown, pp.state, dt * 4f); pp.moving.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(135, -135, pp.shown)); };
            return pr;
        }

        /// <summary>A square push button with its label on it, lit from inside when it's on.</summary>
        Prop KitButton(Transform p, Vector2 at, Vector2 size, string id, string label, Color lightCol, Func<bool> lit, Action press, string sound = "ui_click")
        {
            Piece("Button", new Vector3(at.x, at.y, 0), Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(new Vector3(0, 0, -0.004f), new Vector3(size.x + 0.008f, size.y + 0.008f, 0.008f), C(0.1f, 0.1f, 0.11f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0, -0.01f), new Vector3(size.x, size.y, 0.008f), lightCol * 0.35f, Quaternion.identity);
            }, false, p);
            var glow = Piece("Button light", new Vector3(at.x, at.y, -0.0145f), Quaternion.identity, b => b.AddBox(Vector3.zero, new Vector3(size.x * 0.96f, size.y * 0.96f, 0.001f), lightCol * 1.4f, Quaternion.identity), true, p);
            kitLights.Add((glow, lit));
            var tm = Label(new Vector3(at.x, at.y, -0.0155f), Quaternion.identity, label, Mathf.Min(0.012f, size.y * 0.32f), C(0.06f, 0.06f, 0.07f), TextAnchor.MiddleCenter, p);
            return KitProp(id, label.Replace('\n', ' '), p, at, Mathf.Max(size.x, size.y) * 0.6f, () => $"E: press {label.Replace('\n', ' ')}", press, false, sound);
        }

        /// <summary>A small indicator light.</summary>
        void KitLight(Transform p, Vector2 at, float size, Color col, Func<bool> on)
        {
            Piece("Light base", new Vector3(at.x, at.y, 0), Quaternion.identity, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(new Vector3(0, 0, -0.003f), new Vector3(size * 1.5f, size * 1.5f, 0.006f), C(0.08f, 0.08f, 0.09f), Quaternion.identity); b.AddBox(new Vector3(0, 0, -0.007f), new Vector3(size, size, 0.004f), col * 0.25f, Quaternion.identity); }, false, p);
            var g = Piece("Light", new Vector3(at.x, at.y, -0.0095f), Quaternion.identity, b => b.AddBox(Vector3.zero, new Vector3(size, size, 0.001f), col * 1.5f, Quaternion.identity), true, p);
            kitLights.Add((g, on));
        }

        /// <summary>A round needle gauge: bezel, ticks (red at the end that's trouble), a needle that follows the value.</summary>
        void KitGauge(Transform p, Vector2 at, float r, string label, Func<float> value, bool redHigh, bool redLow = false)
        {
            Piece("Gauge", new Vector3(at.x, at.y, 0), Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                int s0 = b.v.Count;
                b.AddFrustum(0.017f, 0f, r + 0.008f, r + 0.01f, C(0.55f, 0.56f, 0.58f), 28, false, false, true);   // the bezel: a rim standing round the face
                XfLocal(b, s0, Vector3.zero, Quaternion.Euler(-90, 0, 0));
                b.Mat = MeshBuilder.MatMatte;
                s0 = b.v.Count;
                b.AddFrustum(0.012f, 0f, r + 0.008f, r + 0.008f, C(0.05f, 0.05f, 0.06f), 28, true, false);   // the face (one disc: nothing to flicker against)
                XfLocal(b, s0, Vector3.zero, Quaternion.Euler(-90, 0, 0));
                for (int k = 0; k <= 10; k++)
                {
                    float a = Mathf.Lerp(135, -135, k / 10f);
                    bool red = redHigh && k >= 8 || redLow && k <= 2;
                    float len = k % 5 == 0 ? 0.012f : 0.007f;
                    b.AddBox(Quaternion.Euler(0, 0, a) * new Vector3(0, r * 0.84f, 0) + new Vector3(0, 0, -0.0135f), new Vector3(0.0025f, len, 0.001f), red ? C(0.95f, 0.2f, 0.15f) : C(0.92f, 0.92f, 0.9f), Quaternion.Euler(0, 0, a));
                }
            }, false, p);
            var pivot = PanelRoot("Needle", new Vector3(at.x, at.y, -0.0145f), Quaternion.identity, p);
            Piece("Needle bar", Vector3.zero, Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0, r * 0.38f, 0), new Vector3(0.003f, r * 0.85f, 0.001f), C(1f, 0.6f, 0.15f), Quaternion.identity);
                b.AddSphere(Vector3.zero, 0.005f, C(0.2f, 0.2f, 0.22f), 8, 5);
            }, false, pivot);
            KitLabel(p, at + new Vector2(0, -r * 0.45f), label, 0.009f, C(0.85f, 0.85f, 0.87f), TextAnchor.MiddleCenter, -0.014f);
            gauges.Add(new Gauge { needle = pivot, value = value });
        }

        void Screw(MeshBuilder b, Vector3 at) => b.AddSphere(at, 0.005f, C(0.6f, 0.6f, 0.62f), 6, 4);

        /// <summary>A sticky note (Ardlings love them).</summary>
        GameObject StickyNote(Vector3 at, Quaternion rot, string text, Color paper, Transform parent = null)
        {
            var go = Piece("Sticky note", at, rot, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(Vector3.zero, new Vector3(0.075f, 0.075f, 0.002f), paper, Quaternion.identity); b.AddBox(new Vector3(0, 0.03f, -0.0012f), new Vector3(0.075f, 0.015f, 0.0005f), paper * 0.92f, Quaternion.identity); }, false, parent);
            Label(new Vector3(0, 0.004f, -0.0018f), Quaternion.identity, text, 0.0095f, C(0.12f, 0.15f, 0.45f), TextAnchor.MiddleCenter, go.transform);
            Rattle(go.transform, 0.6f);
            return go;
        }

        /// <summary>A small printed placard.</summary>
        void Placard(Vector3 at, Quaternion rot, string text, Color bg, Color fg, float w = 0.16f, float h = 0.04f, Transform parent = null)
        {
            var go = Piece("Placard", at, rot, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(Vector3.zero, new Vector3(w, h, 0.002f), bg, Quaternion.identity); }, false, parent);
            Label(new Vector3(0, 0, -0.0015f), Quaternion.identity, text, h * 0.42f, fg, TextAnchor.MiddleCenter, go.transform);
        }

        /// <summary>A cable hanging from a to b with a sag, zip-tied every so often.</summary>
        GameObject Cable(Vector3 a, Vector3 b, float sag, float r, Color col, int seg = 12)
        {
            var go = Piece("Cable", Vector3.zero, Quaternion.identity, m =>
            {
                m.Mat = MeshBuilder.MatMatte;
                Vector3 P(float t) => Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1 - t);
                for (int i = 0; i < seg; i++)
                {
                    Vector3 p0 = P(i / (float)seg), p1 = P((i + 1) / (float)seg), d = p1 - p0;
                    if (d.sqrMagnitude < 1e-6f) continue;
                    var q = Quaternion.LookRotation(d.normalized);
                    m.AddBox((p0 + p1) / 2, new Vector3(r, r, d.magnitude + r * 0.5f), col, q);
                    if (i % 4 == 2) m.AddBox(p0, new Vector3(r * 1.5f, r * 1.5f, 0.008f), C(0.95f, 0.95f, 0.93f), q);   // a zip tie
                }
            });
            Rattle(go.transform, 0.8f);
            return go;
        }

        void Rattle(Transform t, float amount) => rattlers.Add(new Rattler { t = t, basePos = t.localPosition, baseRot = t.localRotation, amount = amount, seed = UnityEngine.Random.value * 100f });

        // a flat spot on a wall: the capsule's facet i, or a habitat wall gap
        struct Spot
        {
            public Vector3 o, t, n;
            public Vector3 At(float u, float y, float off) => o + t * u + Vector3.up * y + n * off;
            public Quaternion Face => Quaternion.LookRotation(-n, Vector3.up);   // forward into the wall (text reads from the room)
        }

        Spot Facet(int i)
        {
            float a = (i + 0.5f) * Mathf.PI * 2 / 12f;
            var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            return new Spot { o = dir * roomR * Mathf.Cos(Mathf.PI / 12f), n = -dir, t = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a)) };
        }

        // ---------------------------------------------------------------- building it all

        void BuildCabinKit(bool hab, float h, float front)
        {
            gauges.Clear(); floaters.Clear(); rattlers.Clear(); streamers.Clear(); kitLabels.Clear(); plasma.Clear(); kitLights.Clear();
            propById.Clear(); alarmAcked.Clear(); keypadText = ""; keypadShow = ""; lampTestUntil = 0; alarmFlashing = false; lastAmbient = -1; miniBall = null;
            // sounds: one-shots and the fans' hum
            cabinAudio = room.AddComponent<AudioSource>();
            cabinAudio.spatialBlend = 0; cabinAudio.playOnAwake = false;
            var humClip = GameAudio.I != null ? GameAudio.I.Clip("hangar_amb") : null;
            if (humClip != null)
            {
                var hg = new GameObject("Hum");
                hg.transform.SetParent(room.transform, false);
                cabinHum = hg.AddComponent<AudioSource>();
                cabinHum.clip = humClip; cabinHum.loop = true; cabinHum.spatialBlend = 0; cabinHum.volume = 0; cabinHum.pitch = 0.7f;
                cabinHum.Play();
            }
            BuildCabinLamps(hab, h, front);   // milestone 37
            BuildSunPatches(h);
            BuildOverheadPanel(hab, h);
            BuildGaugeStrip(front);
            BuildWindowsGlass(hab, h);
            BuildVents(hab, h);
            BuildSeatExtras();
            BuildClutter(hab, h, front);
            BuildFloaters(hab, h);
            // the existing loose things rattle too
            foreach (var n in new[] { "Clipboard", "Handset", "Harness" })
                foreach (Transform t in room.transform) if (t.name == n) Rattle(t, n == "Harness" ? 0.3f : 0.7f);
            propById.Clear();
            foreach (var p in props) propById[p.id] = p;
        }

        // the overhead panel: breakers, knobs, guarded switches, LAMP TEST, the keypad computer, the cabin temperature
        void BuildOverheadPanel(bool hab, float h)
        {
            Vector3 at = hab ? new Vector3(0, h - 0.06f, roomL / 2 - 1.25f) : new Vector3(0, h - 0.06f, roomR - 1.05f);
            var p = PanelRoot("Overhead panel", at, Quaternion.Euler(-90, 0, 0));   // face down; its "up" is toward the back
            Piece("Overhead plate", Vector3.zero, Quaternion.identity, b =>
            {
                b.Mat = MatPanel;
                b.AddBox(new Vector3(0, 0, 0.015f), new Vector3(1.14f, 0.64f, 0.03f), C(0.2f, 0.21f, 0.23f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0, -0.001f), new Vector3(1.1f, 0.6f, 0.002f), C(0.27f, 0.29f, 0.31f), Quaternion.identity);
                // section frames and dividers
                b.AddBox(new Vector3(0.0f, 0, -0.004f), new Vector3(0.006f, 0.58f, 0.006f), C(0.15f, 0.15f, 0.17f), Quaternion.identity);
                b.AddBox(new Vector3(-0.28f, 0.05f, -0.004f), new Vector3(0.54f, 0.006f, 0.006f), C(0.15f, 0.15f, 0.17f), Quaternion.identity);
                b.AddBox(new Vector3(-0.28f, -0.16f, -0.004f), new Vector3(0.54f, 0.006f, 0.006f), C(0.15f, 0.15f, 0.17f), Quaternion.identity);
                b.AddBox(new Vector3(0.28f, -0.16f, -0.004f), new Vector3(0.54f, 0.006f, 0.006f), C(0.15f, 0.15f, 0.17f), Quaternion.identity);
                foreach (var c in new[] { new Vector3(-0.53f, 0.28f), new Vector3(0.53f, 0.28f), new Vector3(-0.53f, -0.28f), new Vector3(0.53f, -0.28f), new Vector3(0, 0.28f), new Vector3(0, -0.28f) })
                    Screw(b, c + new Vector3(0, 0, -0.004f));
                // standoffs up to the ceiling
                foreach (var c in new[] { new Vector3(-0.45f, 0.22f), new Vector3(0.45f, 0.22f), new Vector3(-0.45f, -0.22f), new Vector3(0.45f, -0.22f) })
                    b.AddBox(c + new Vector3(0, 0, 0.045f), new Vector3(0.03f, 0.03f, 0.06f), C(0.5f, 0.5f, 0.52f), Quaternion.identity);
            }, false, p);
            Color title = C(0.95f, 0.8f, 0.35f);
            KitLabel(p, new Vector2(-0.28f, 0.275f), "CIRCUIT BREAKERS", 0.013f, title);
            KitLabel(p, new Vector2(-0.28f, 0.03f), "CABIN", 0.013f, title);
            KitLabel(p, new Vector2(-0.28f, -0.18f), "SYSTEMS", 0.013f, title);
            KitLabel(p, new Vector2(0.28f, 0.275f), "NAV COMPUTER", 0.013f, title);
            // 16 breakers; some cut real things
            string[,] names =
            {
                { "fans", "FANS" }, { "panel", "PANEL LTS" }, { "flood", "FLOOD" }, { "radio", "RADIO" }, { "nav", "NAV" }, { "pumpa", "PUMP A" }, { "pumpb", "PUMP B" }, { "heater", "HEATER" },
                { "o2", "O2 FLOW" }, { "co2", "CO2 SCRUB" }, { "water", "WATER" }, { "science", "SCIENCE" }, { "camera", "CAMERA" }, { "toaster", "TOASTER" }, { "coffee", "COFFEE" }, { "spare", "SPARE" },
            };
            string[] flavour = { "", "", "", "", "", "Pump A whirs down.", "Pump B goes quiet.", "The heater clicks off. Brr.", "The air goes very still...", "The CO2 scrubber stops. Don't leave it off for long!",
                "The water pump stops.", "The science rack powers down.", "The camera turns off.", "The toaster goes cold. A tragedy.", "No more coffee. The crew look worried.", "Nothing happens. It's a spare." };
            for (int i = 0; i < 16; i++)
            {
                int k = i;
                var v2 = new Vector2(-0.5f + (i % 8) * 0.064f, i < 8 ? 0.21f : 0.12f);
                KitBreaker(p, v2, names[i, 0], names[i, 1], on => { if (!on && flavour[k] != "") Show(flavour[k], 2.5f); if (!on && k == 3) Show("The radio goes dead.", 2f); if (!on && k == 4) Show("The attitude ball stops.", 2f); });
            }
            // the cabin knobs
            KitKnob(p, new Vector2(-0.46f, -0.05f), "flood", "FLOOD", 0.3f, v => $"{v * 100:0}%");
            KitKnob(p, new Vector2(-0.33f, -0.05f), "panel", "PANEL", 0.7f, v => $"{v * 100:0}%");
            KitKnob(p, new Vector2(-0.20f, -0.05f), "volume", "VOLUME", 0.6f, v => $"{v * 100:0}%");
            KitKnob(p, new Vector2(-0.07f, -0.05f), "temp", "TEMP", 0.5f, v => $"{16 + v * 12:0}°C");
            // guarded switches and LAMP TEST
            bool purge = false, dump = false, boost = false;
            KitToggle(p, new Vector2(-0.46f, -0.23f), "o2purge", "O2 PURGE", () => purge, () => { purge = !purge; if (purge) { PuffAt(RoomPoint(p, new Vector3(-0.46f, -0.23f, -0.05f))); Show("Psssht! Fresh oxygen floods the cabin.", 2.5f); } }, true);
            KitToggle(p, new Vector2(-0.33f, -0.23f), "waterdump", "WATER DUMP", () => dump, () => { dump = !dump; if (dump) Show("Waste water sprays out into space: a cloud of sparkling ice crystals outside.", 3.5f); }, true);
            KitToggle(p, new Vector2(-0.20f, -0.23f), "fanboost", "FAN BOOST", () => boost, () => { boost = !boost; propById["fanboost"].state = boost ? 1 : 0; Show(boost ? "The fans spin up: whooosh." : "The fans settle down.", 2f); }, false);
            KitButton(p, new Vector2(-0.07f, -0.23f), new Vector2(0.07f, 0.045f), "lamptest", "LAMP\nTEST", C(1f, 0.85f, 0.4f), () => LampTest, () => { lampTestUntil = Time.unscaledTime + 2.5f; Show("Lamp test: every light on the ship should be lit.", 2.5f); });
            // the keypad computer
            Piece("Keypad screen", new Vector3(0.28f, 0.205f, 0), Quaternion.identity, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(new Vector3(0, 0, -0.004f), new Vector3(0.42f, 0.085f, 0.008f), C(0.06f, 0.06f, 0.07f), Quaternion.identity); }, false, p);
            Piece("Keypad glow", new Vector3(0.28f, 0.205f, -0.0085f), Quaternion.identity, b => b.AddBox(Vector3.zero, new Vector3(0.4f, 0.07f, 0.001f), C(0.05f, 0.18f, 0.08f), Quaternion.identity), true, p);
            keypadScreen = Label(new Vector3(0.09f, 0.205f, -0.0095f), Quaternion.identity, "READY", 0.028f, C(0.4f, 1f, 0.5f), TextAnchor.MiddleLeft, p);
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "ENT" };
            for (int i = 0; i < 12; i++)
            {
                string key = keys[i];
                var v2 = new Vector2(0.16f + (i % 3) * 0.12f, 0.115f - (i / 3) * 0.07f);
                KitButton(p, v2, new Vector2(0.09f, 0.05f), "key" + key, key, C(0.6f, 0.75f, 1f), () => LampTest, () => KeypadPress(key), "ui_click");
            }
            KitLabel(p, new Vector2(0.28f, -0.2f), "PROGRAMS  1 HEIGHT   2 SPEED   3 FUEL   4 HIGH POINT   5 CLOCK", 0.0085f, C(0.75f, 0.78f, 0.8f));
            // the cabin temperature readout
            Piece("Temp screen", new Vector3(0.28f, -0.25f, 0), Quaternion.identity, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(new Vector3(0, 0, -0.003f), new Vector3(0.2f, 0.04f, 0.006f), C(0.06f, 0.06f, 0.07f), Quaternion.identity); }, false, p);
            var temp = Label(new Vector3(0.28f, -0.25f, -0.0065f), Quaternion.identity, "", 0.018f, C(1f, 0.6f, 0.3f), TextAnchor.MiddleCenter, p);
            screens.Add((temp, () => BreakerOff("heater") ? "CABIN --°C" : $"CABIN {16 + KitValue("knob_temp", 0.5f) * 12:0}°C"));
            StickyNote(new Vector3(0.475f, -0.235f, -0.004f), Quaternion.Euler(0, 0, 6), "the COFFEE\nbreaker stays\nIN. -crew", C(1f, 0.92f, 0.35f), p);
            // the bundle of cables from the panel to the walls
            Vector3 c0 = RoomPoint(p, new Vector3(-0.55f, 0.28f, 0.02f)), c1 = RoomPoint(p, new Vector3(0.55f, 0.28f, 0.02f));
            Vector3 w0, w1;
            if (hab) { w0 = new Vector3(-roomW / 2 + 0.03f, h - 0.15f, at.z - 0.25f); w1 = new Vector3(roomW / 2 - 0.03f, h - 0.15f, at.z - 0.25f); }
            else { w0 = Facet(10).At(0.3f, h - 0.25f, 0.03f); w1 = Facet(1).At(-0.3f, h - 0.25f, 0.03f); }
            Color[] cc = { C(0.12f, 0.12f, 0.13f), C(0.8f, 0.15f, 0.12f), C(0.2f, 0.4f, 0.8f) };
            for (int k = 0; k < 3; k++)
            {
                Vector3 off = new Vector3(0, 0, (k - 1) * 0.025f);
                Cable(c0 + off, w0 + off, 0.12f + k * 0.03f, 0.014f, cc[k]);
                Cable(c1 + off, w1 + off, 0.1f + k * 0.03f, 0.014f, cc[(k + 1) % 3]);
            }
        }

        void KeypadPress(string key)
        {
            if (BreakerOff("nav")) { keypadShow = "NO POWER"; return; }
            if (key == "CLR") { keypadText = ""; keypadShow = "READY"; return; }
            if (key != "ENT") { if (keypadText.Length < 8) keypadText += key; keypadShow = keypadText; return; }
            var v = active;
            string res;
            switch (keypadText)
            {
                case "1": res = $"P1 {Ui.Dist(v.Altitude).ToUpperInvariant()}"; break;
                case "2": res = $"P2 {v.SurfaceRelVelocity.magnitude:0} M/S"; break;
                case "3": res = $"P3 FUEL {v.parts.Sum(x => x.propellant):0.0} T"; break;
                case "4":
                {
                    var o = trajectory.Count > 0 ? trajectory[0].orbit : null;
                    res = o == null || v.landed ? "P4 LANDED" : o.IsElliptic ? $"P4 {Ui.Dist(o.Apoapsis - v.body.radius).ToUpperInvariant()}" : "P4 ESCAPE";
                    break;
                }
                case "5": { long s = v.hasLaunched ? (long)Math.Max(0, ut - v.launchUt) : 0; res = $"P5 {s / 3600:00}:{(s / 60) % 60:00}:{s % 60:00}"; break; }
                case "7": res = "HI ARDLING!"; break;
                case "42": res = "THE ANSWER"; break;
                case "1234": res = "NICE TRY"; break;
                case "0": res = "ALL SYSTEMS GO"; break;
                default: res = keypadText.Length == 0 ? "READY" : "PROG ?? " + keypadText; break;
            }
            keypadText = "";
            keypadShow = res;
            KitSound("ui_confirm", 0.4f);
        }

        // the instrument strip on top of the desk: gauges, the attitude ball, MASTER ALARM, CAUTION
        void BuildGaugeStrip(float front)
        {
            var p = PanelRoot("Gauge strip", new Vector3(0, 0.88f, front - 0.16f), Quaternion.Euler(15, 0, 0));
            Piece("Strip plate", Vector3.zero, Quaternion.identity, b =>
            {
                b.Mat = MatPanel;
                b.AddBox(new Vector3(0, 0, 0.02f), new Vector3(1.46f, 0.22f, 0.04f), C(0.16f, 0.17f, 0.19f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.115f, -0.03f), new Vector3(1.46f, 0.012f, 0.1f), C(0.1f, 0.1f, 0.11f), Quaternion.identity);   // a glare shield lip
                foreach (var c in new[] { new Vector3(-0.7f, 0.09f), new Vector3(0.7f, 0.09f), new Vector3(-0.7f, -0.09f), new Vector3(0.7f, -0.09f) }) Screw(b, c + new Vector3(0, 0, -0.002f));
            }, false, p);
            var v = active;
            KitGauge(p, new Vector2(-0.47f, 0.005f), 0.068f, "FUEL", () => FuelForGauge(), false, true);
            KitGauge(p, new Vector2(-0.27f, 0.005f), 0.068f, "POWER", () => active.EcCapacity > 0 ? (float)(active.EcStored / active.EcCapacity) : 0, false, true);
            KitGauge(p, new Vector2(0.27f, 0.005f), 0.068f, "CLIMB", () => 0.5f + 0.5f * (float)Math.Tanh(Vector3d.Dot(active.SurfaceRelVelocity, active.pos.normalized) / 150.0), false);
            KitGauge(p, new Vector2(0.47f, 0.005f), 0.068f, "G", () => Mathf.Clamp01((float)active.gForce / 6f), true);
            // the attitude ball: the game's navball, turning with the ship
            Piece("Ball bezel", Vector3.zero, Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                int s0 = b.v.Count;
                b.AddFrustum(0.02f, 0f, 0.088f, 0.095f, C(0.3f, 0.3f, 0.32f), 32, false, false, true);
                XfLocal(b, s0, Vector3.zero, Quaternion.Euler(-90, 0, 0));
            }, false, p);
            var ballGo = new GameObject("Attitude ball") { layer = InteriorLayer };
            ballGo.transform.SetParent(p, false);
            ballGo.transform.localPosition = new Vector3(0, 0, 0.01f);
            ballGo.transform.localScale = Vector3.one * 0.08f;
            ballGo.AddComponent<MeshFilter>().sharedMesh = MeshFactory.NavballMesh();
            if (miniBallMat == null) miniBallMat = new Material(Resources.Load<Shader>("Shaders/RocketNavball")) { mainTexture = MeshFactory.NavballTexture() };
            var br = ballGo.AddComponent<MeshRenderer>();
            br.sharedMaterial = miniBallMat;
            br.shadowCastingMode = ShadowCastingMode.Off;
            miniBall = ballGo.transform;
            Piece("Ball marker", new Vector3(0, 0, -0.075f), Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(-0.03f, 0, 0), new Vector3(0.04f, 0.006f, 0.002f), C(1f, 0.75f, 0.1f), Quaternion.identity);
                b.AddBox(new Vector3(0.03f, 0, 0), new Vector3(0.04f, 0.006f, 0.002f), C(1f, 0.75f, 0.1f), Quaternion.identity);
                b.AddBox(new Vector3(0, -0.006f, 0), new Vector3(0.006f, 0.012f, 0.002f), C(1f, 0.75f, 0.1f), Quaternion.identity);
            }, false, p);
            KitLabel(p, new Vector2(0, -0.1f), "ATTITUDE", 0.009f, C(0.85f, 0.85f, 0.87f), TextAnchor.MiddleCenter, -0.003f);
            // MASTER ALARM and CAUTION at the ends
            KitButton(p, new Vector2(-0.64f, 0.005f), new Vector2(0.1f, 0.075f), "masteralarm", "MASTER\nALARM", C(1f, 0.15f, 0.1f), () => alarmFlashing && Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.3f || LampTest, AckAlarm, "ui_confirm");
            KitLight(p, new Vector2(0.64f, 0.02f), 0.06f, C(1f, 0.75f, 0.1f), () => AlarmConditions().Any() || LampTest);
            KitLabel(p, new Vector2(0.64f, -0.035f), "CAUTION", 0.011f, C(1f, 0.8f, 0.3f), TextAnchor.MiddleCenter, -0.003f);
        }

        float FuelForGauge()
        {
            var v = active;
            float f = v.ActiveFuelFraction();
            if (f <= 0 && !v.hasLaunched) { double have = v.parts.Sum(x => x.propellant), full = v.parts.Sum(x => (double)x.def.propellant); f = full > 0 ? (float)(have / full) : 0; }
            return f;
        }

        // ---- the master alarm: new trouble flashes and beeps until someone presses it

        IEnumerable<string> AlarmConditions()
        {
            var v = active;
            if (v == null) yield break;
            if (v.hasLaunched && v.parts.Any(p => p.ignited && p.def.IsEngine) && v.ActiveFuelFraction() < 0.1f) yield return "LOW FUEL";   // (only with an engine running)
            if (v.Hottest != null && v.Hottest.HeatFraction > 0.8f) yield return "HOT";
            if (v.UsesSupplies && v.SupplyDaysLeft() < 1) yield return "FOOD LOW";
            if (v.EcCapacity > 0 && v.EcStored / v.EcCapacity < 0.08) yield return "POWER LOW";
            if (BreakerOff("co2")) yield return "CO2";
        }

        void AckAlarm()
        {
            foreach (var c in AlarmConditions()) alarmAcked.Add(c);
            alarmFlashing = false;
            Show("Master alarm off. (The CAUTION light stays on while there's trouble.)", 2.5f);
        }

        void UpdateAlarm(float dt)
        {
            var now = AlarmConditions().ToList();
            alarmAcked.RemoveWhere(c => !now.Contains(c));   // trouble that's gone can alarm again
            bool fresh = now.Any(c => !alarmAcked.Contains(c));
            if (fresh && !alarmFlashing) Show($"MASTER ALARM: {string.Join(", ", now.Where(c => !alarmAcked.Contains(c)))}", 3f, true);
            alarmFlashing = fresh;
            if (alarmFlashing) { alarmBeepT -= dt; if (alarmBeepT <= 0) { alarmBeepT = 1.1f; KitSound("ui_error", 0.35f); } }
        }

        // ---- glass in the windows (it was a hole), bolts round the frames, and reentry's glow

        void BuildWindowsGlass(bool hab, float h)
        {
            var list = Windows(h).ToList();
            if (hab) list.Add((new Vector3(0, 0, roomL / 2), Vector3.back, roomW * 0.76f, 0.6f, 1.55f));
            foreach (var (c, n, w, sill, head) in list)
            {
                var q = Quaternion.LookRotation(-n, Vector3.up);
                float hh = head - sill;
                Vector3 mid = c + Vector3.up * (sill + head) / 2;
                PieceMat("Glass", mid + n * 0.012f, q, b =>
                {
                    b.AddBox(Vector3.zero, new Vector3(w, hh, 0.002f), new Color(0.75f, 0.85f, 0.95f, 0.035f), Quaternion.identity);
                    b.AddBox(new Vector3(-w * 0.18f, hh * 0.1f, -0.002f), new Vector3(w * 0.08f, hh * 0.9f, 0.001f), new Color(1f, 1f, 1f, 0.022f), Quaternion.Euler(0, 0, -28));   // a faint glint
                    b.AddBox(new Vector3(-w * 0.05f, hh * 0.05f, -0.002f), new Vector3(w * 0.03f, hh * 0.9f, 0.001f), new Color(1f, 1f, 1f, 0.016f), Quaternion.Euler(0, 0, -28));
                }, Mats.Unlit);
                var pl = Piece("Plasma", mid + n * 0.016f, q, b => b.AddBox(Vector3.zero, new Vector3(w, hh, 0.002f), new Color(1f, 0.45f, 0.15f, 0f), Quaternion.identity), true);
                plasma.Add((pl.GetComponent<MeshFilter>().sharedMesh, new Color(1f, 0.45f, 0.15f)));
                // a thick inner frame with bolts all round
                Piece("Window bolts", mid, q, b =>
                {
                    b.Mat = MatBrushed;
                    Color fr = C(0.7f, 0.72f, 0.75f);
                    foreach (int s in new[] { -1, 1 }) b.AddBox(new Vector3(s * (w / 2 + 0.02f), 0, -0.035f), new Vector3(0.04f, hh + 0.08f, 0.05f), fr, Quaternion.identity);
                    for (int k = 0; k < 6; k++)
                    {
                        float x = Mathf.Lerp(-w / 2, w / 2, (k + 0.5f) / 6f);
                        b.AddSphere(new Vector3(x, hh / 2 + 0.04f, -0.075f), 0.008f, C(0.75f, 0.75f, 0.78f), 6, 4);
                        b.AddSphere(new Vector3(x, -hh / 2 - 0.04f, -0.075f), 0.008f, C(0.75f, 0.75f, 0.78f), 6, 4);
                    }
                    for (int k = 0; k < 4; k++)
                        foreach (int s in new[] { -1, 1 }) b.AddSphere(new Vector3(s * (w / 2 + 0.02f), Mathf.Lerp(-hh / 2, hh / 2, (k + 0.5f) / 4f), -0.062f), 0.008f, C(0.75f, 0.75f, 0.78f), 6, 4);
                });
            }
        }

        // ---- vents with streamers that flutter in the air flow

        void BuildVents(bool hab, float h)
        {
            var spots = new List<(Vector3 top, Vector3 outward, Vector3 along)>();
            if (!hab)
                foreach (int i in new[] { 2, 9 })
                {
                    var s = Facet(i);
                    for (int k = -1; k <= 1; k++) spots.Add((s.At(k * 0.09f, 1.65f, 0.05f), s.n, s.t));
                }
            else
                foreach (float x in new[] { -0.8f, 0.8f })
                {
                    Piece("Ceiling vent", new Vector3(x, h - 0.01f, 0.3f), Quaternion.identity, b =>
                    {
                        b.Mat = MatPerf;
                        b.AddBox(Vector3.zero, new Vector3(0.36f, 0.02f, 0.2f), C(0.25f, 0.26f, 0.28f), Quaternion.identity);
                        for (int k = 0; k < 6; k++) b.AddBox(new Vector3(-0.15f + k * 0.06f, -0.012f, 0), new Vector3(0.012f, 0.008f, 0.18f), C(0.12f, 0.12f, 0.13f), Quaternion.identity);
                    });
                    for (int k = -1; k <= 1; k++) spots.Add((new Vector3(x + k * 0.1f, h - 0.03f, 0.3f), Vector3.right * Mathf.Sign(x), Vector3.forward));
                }
            Color[] ribbon = { C(1f, 0.3f, 0.25f), C(1f, 0.85f, 0.2f), C(0.3f, 0.7f, 1f) };
            int n = 0;
            foreach (var (top, outward, along) in spots)
            {
                var pivot = PanelRoot("Streamer", top, Quaternion.LookRotation(outward, Vector3.up));
                Piece("Ribbon", Vector3.zero, Quaternion.identity, b => b.AddBox(new Vector3(0, -0.08f, 0), new Vector3(0.018f, 0.16f, 0.002f), ribbon[n % 3], Quaternion.identity), false, pivot);
                streamers.Add((pivot, n * 1.7f, pivot.localRotation));
                n++;
            }
        }

        // ---- seats get bolsters, armrests, foot rests and a hand controller you can wiggle

        void BuildSeatExtras()
        {
            for (int i = 0; i < seats.Count; i++)
            {
                var s = seats[i];
                Piece("Seat sides", s, Quaternion.identity, b =>
                {
                    b.Mat = MatFabric;
                    Color pad = C(0.22f, 0.32f, 0.46f), frame = C(0.55f, 0.56f, 0.58f);
                    foreach (int side in new[] { -1, 1 })
                    {
                        b.AddBox(new Vector3(side * 0.27f, 0.42f, -0.02f), new Vector3(0.06f, 0.16f, 0.46f), pad, Quaternion.identity);       // bolster
                        b.AddBox(new Vector3(side * 0.3f, 0.52f, 0.05f), new Vector3(0.07f, 0.035f, 0.34f), C(0.15f, 0.15f, 0.17f), Quaternion.identity);   // armrest
                        b.AddBox(new Vector3(side * 0.22f, 0.13f, 0.4f), new Vector3(0.03f, 0.26f, 0.03f), frame, Quaternion.Euler(-20, 0, 0)); // foot rest struts
                    }
                    b.AddBox(new Vector3(0, 0.08f, 0.46f), new Vector3(0.42f, 0.025f, 0.2f), frame, Quaternion.Euler(-25, 0, 0));             // foot pan
                    b.AddBox(new Vector3(0, 0.1f, 0.47f), new Vector3(0.36f, 0.01f, 0.14f), C(0.12f, 0.12f, 0.12f), Quaternion.Euler(-25, 0, 0)); // grip tape
                    for (int k = 0; k < 4; k++) b.AddBox(new Vector3(0, 0.36f, -0.2f + k * 0.12f), new Vector3(0.46f, 0.004f, 0.01f), pad * 0.8f, Quaternion.identity);   // seat stitching
                });
                // the hand controller on the right armrest
                var pivot = PanelRoot("Hand controller", s + new Vector3(0.3f, 0.54f, 0.16f), Quaternion.identity);
                Piece("Controller base", Vector3.zero, Quaternion.identity, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(new Vector3(0, 0.01f, 0), new Vector3(0.07f, 0.02f, 0.07f), C(0.1f, 0.1f, 0.11f), Quaternion.identity); b.AddSphere(new Vector3(0, 0.02f, 0), 0.025f, C(0.08f, 0.08f, 0.08f), 10, 5, Mathf.PI / 2); }, false, pivot);
                var stick = Piece("Stick", new Vector3(0, 0.02f, 0), Quaternion.identity, b =>
                {
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(new Vector3(0, 0.04f, 0), new Vector3(0.012f, 0.08f, 0.012f), C(0.6f, 0.6f, 0.62f), Quaternion.identity);
                    b.AddBox(new Vector3(0, 0.1f, 0.005f), new Vector3(0.032f, 0.06f, 0.04f), C(0.12f, 0.12f, 0.13f), Quaternion.Euler(-10, 0, 0));
                    b.AddBox(new Vector3(0, 0.133f, 0.012f), new Vector3(0.014f, 0.008f, 0.014f), C(0.9f, 0.15f, 0.1f), Quaternion.identity);
                }, false, pivot);
                var pr = AddProp("stick" + i, "Hand controller", s + new Vector3(0.3f, 0.66f, 0.16f), () => "E: wiggle the hand controller", null, 0.06f);
                pr.sound = "clink";
                pr.moving = stick.transform;
                pr.use = () => { pr.vel = Time.unscaledTime; };
                pr.animate = (pp, dt) =>
                {
                    float t = Time.unscaledTime - pp.vel;
                    float a = t < 1.2f ? Mathf.Sin(t * 18f) * 18f * (1.2f - t) : 0;
                    pp.moving.localRotation = Quaternion.Euler(a, 0, a * 0.6f);
                };
            }
        }

        // ---- the lived-in things

        void BuildClutter(bool hab, float h, float front)
        {
            string crew = insidePart != null && insidePart.crew.Count > 0 ? insidePart.crew[0].Split(' ')[0] : "Iris";
            Spot strip, bungee, bags;
            if (hab)
            {
                float hw = roomW / 2;
                strip = new Spot { o = new Vector3(-hw, 0, 0), n = Vector3.right, t = Vector3.forward };
                bungee = new Spot { o = new Vector3(hw, 0, 0), n = Vector3.left, t = Vector3.back };
                bags = new Spot { o = new Vector3(-hw, 0, -0.9f), n = Vector3.right, t = Vector3.forward };
            }
            else { strip = Facet(9); bungee = Facet(10); bags = Facet(9); }
            float stripW = hab ? 0.36f : 0.8f;
            // a Velcro strip with a tablet, a camera and a torch stuck to it
            bool vertical = hab;
            Piece("Velcro", strip.At(0, hab ? 1.15f : 1.3f, 0.005f), strip.Face, b => { b.Mat = MatVelcro; b.AddBox(Vector3.zero, vertical ? new Vector3(0.05f, 0.9f, 0.006f) : new Vector3(stripW, 0.05f, 0.006f), C(0.18f, 0.18f, 0.2f), Quaternion.identity); });
            Vector3 Item(float k) => vertical ? strip.At(0, 0.8f + k * 0.35f, 0.02f) : strip.At(-0.3f + k * 0.3f, 1.3f, 0.02f);
            var tablet = Piece("Tablet", Item(0), strip.Face, b => { b.Mat = MeshBuilder.MatPaint; b.AddBox(Vector3.zero, new Vector3(0.2f, 0.14f, 0.012f), C(0.12f, 0.12f, 0.13f), Quaternion.identity); });
            var tg = Piece("Tablet screen", Item(0) + strip.n * 0.0075f, strip.Face, b => b.AddBox(Vector3.zero, new Vector3(0.18f, 0.12f, 0.001f), C(0.1f, 0.2f, 0.35f), Quaternion.identity), true);
            var ttm = Label(Item(0) + strip.n * 0.009f, strip.Face, "", 0.012f, C(0.8f, 0.9f, 1f));
            screens.Add((ttm, () => $"CHECKLIST\n{(active.hasLaunched ? "[x]" : "[ ]")} Launch\n{(active.landed && active.hasLaunched ? "[x]" : "[ ]")} Land\n[ ] Have fun"));
            Rattle(tablet.transform, 0.5f); Rattle(tg.transform, 0.5f); Rattle(ttm.transform, 0.5f);
            var cam = Piece("Camera", Item(1), strip.Face, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(Vector3.zero, new Vector3(0.12f, 0.08f, 0.06f), C(0.15f, 0.15f, 0.16f), Quaternion.identity);
                int s0 = b.v.Count;
                b.AddFrustum(0.06f, 0f, 0.025f, 0.03f, C(0.08f, 0.08f, 0.08f), 14, true, false);
                XfLocal(b, s0, new Vector3(0.01f, 0, -0.03f), Quaternion.Euler(-90, 0, 0));
                b.AddBox(new Vector3(-0.04f, 0.045f, 0), new Vector3(0.025f, 0.012f, 0.025f), C(0.85f, 0.15f, 0.12f), Quaternion.identity);
            });
            var torch = Piece("Torch", Item(2), strip.Face * Quaternion.Euler(0, 0, 90), b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                int s0 = b.v.Count;
                b.AddFrustum(0.16f, 0f, 0.018f, 0.018f, C(0.95f, 0.75f, 0.15f), 10, true, true);
                b.AddFrustum(0.2f, 0.16f, 0.026f, 0.018f, C(0.2f, 0.2f, 0.22f), 10, true, false);
                XfLocal(b, s0, new Vector3(0, -0.1f, -0.025f), Quaternion.identity);
            });
            Rattle(cam.transform, 0.6f); Rattle(torch.transform, 0.6f);
            AddProp("camera", "Camera", Item(1) + strip.n * 0.05f, () => "E: take a photo", () => { Show("Click! A photo for the folks at home.", 2f); lampTestUntil = Time.unscaledTime + 0.12f; }, 0.07f).sound = "clink";
            AddProp("torch", "Torch", Item(2) + strip.n * 0.05f, () => "E: click the torch on and off", () => Show("Click. (It's daytime somewhere.)", 1.5f), 0.07f).sound = "clink";
            // stowage bags: soft, strapped, tagged
            string[] tags = { "FOOD B3", "SPARE SOCKS", "TOOLS", "SCIENCE KIT" };
            for (int k = 0; k < (hab ? 3 : 2); k++)
            {
                Vector3 at = hab ? new Vector3(-roomW / 2 + 0.2f, 0, -1.2f + k * 0.45f) : bags.At(-0.25f + k * 0.48f, 0f, 0.17f);
                var face = hab ? Quaternion.Euler(0, -90, 0) : bags.Face;
                float bw = 0.4f, bh = 0.32f, bd = 0.3f;
                var bag = Piece("Stowage bag", at, face, b =>
                {
                    b.Mat = MatFabric;
                    Color cloth = C(0.88f, 0.86f, 0.8f);
                    b.AddBox(new Vector3(0, bh / 2, 0), new Vector3(bw, bh - 0.04f, bd), cloth, Quaternion.identity);
                    b.AddBox(new Vector3(0, bh / 2, 0), new Vector3(bw - 0.04f, bh, bd - 0.04f), cloth * 0.97f, Quaternion.identity);   // rounded look
                    foreach (float x in new[] { -bw * 0.28f, bw * 0.28f }) b.AddBox(new Vector3(x, bh / 2, 0), new Vector3(0.025f, bh + 0.006f, bd + 0.006f), C(0.3f, 0.32f, 0.36f), Quaternion.identity);   // straps
                    b.AddBox(new Vector3(0, bh * 0.75f, -bd / 2 - 0.004f), new Vector3(0.14f, 0.05f, 0.004f), C(0.95f, 0.95f, 0.95f), Quaternion.identity);   // the tag
                    b.AddBox(new Vector3(0, bh + 0.005f, 0), new Vector3(0.12f, 0.012f, 0.03f), C(0.3f, 0.32f, 0.36f), Quaternion.identity);   // a handle
                });
                Label(new Vector3(0, bh * 0.75f, -bd / 2 - 0.007f), Quaternion.identity, tags[k], 0.016f, C(0.15f, 0.15f, 0.2f), TextAnchor.MiddleCenter, bag.transform);
                Rattle(bag.transform, 0.25f);
            }
            if (!hab)
            {
                // stacked on the floor of the round cabin, against the wall
                var top = Piece("Stowage bag", bags.At(0.0f, 0.32f, 0.15f), bags.Face, b =>
                {
                    b.Mat = MatFabric;
                    b.AddBox(new Vector3(0, 0.12f, 0), new Vector3(0.5f, 0.22f, 0.26f), C(0.85f, 0.83f, 0.76f), Quaternion.identity);
                    b.AddBox(new Vector3(0, 0.12f, 0), new Vector3(0.025f, 0.226f, 0.266f), C(0.3f, 0.32f, 0.36f), Quaternion.identity);
                });
                Label(new Vector3(0.12f, 0.12f, -0.135f), Quaternion.identity, "CLOTHES - " + crew.ToUpperInvariant(), 0.016f, C(0.15f, 0.15f, 0.2f), TextAnchor.MiddleCenter, top.transform);
                Rattle(top.transform, 0.3f);
            }
            // bungee cords with pouches tucked behind them, a photo, a mission patch
            float by = hab ? 1.1f : 1.15f, bw2 = hab ? 0.38f : 0.9f;
            foreach (float dy in new[] { 0f, 0.3f })
            {
                Piece("Bungee", bungee.At(0, by + dy, 0.03f), bungee.Face, b =>
                {
                    b.Mat = MatFabric;
                    b.AddBox(Vector3.zero, new Vector3(bw2, 0.008f, 0.008f), C(0.15f, 0.3f, 0.75f), Quaternion.identity);
                    foreach (int s in new[] { -1, 1 }) b.AddBox(new Vector3(s * bw2 / 2, 0, 0.012f), new Vector3(0.02f, 0.02f, 0.024f), C(0.7f, 0.7f, 0.72f), Quaternion.identity);
                });
            }
            float u0 = hab ? -0.09f : -0.3f, du = hab ? 0.18f : 0.3f;
            var pouch = Piece("Food pouch", bungee.At(u0, by + 0.12f, 0.025f), bungee.Face * Quaternion.Euler(0, 0, 8), b =>
            {
                b.Mat = MeshBuilder.MatGloss;
                b.AddBox(Vector3.zero, new Vector3(0.11f, 0.15f, 0.015f), C(0.85f, 0.85f, 0.87f), Quaternion.identity);
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(new Vector3(0, 0.01f, -0.009f), new Vector3(0.08f, 0.05f, 0.002f), C(0.9f, 0.45f, 0.15f), Quaternion.identity);
            });
            Label(bungee.At(u0, by + 0.13f, 0.035f), bungee.Face * Quaternion.Euler(0, 0, 8), "MAC &\nCHEESE", 0.011f, C(0.15f, 0.1f, 0.05f));
            var drink = Piece("Drink bag", bungee.At(u0 + du, by + 0.12f, 0.025f), bungee.Face * Quaternion.Euler(0, 0, -6), b =>
            {
                b.Mat = MeshBuilder.MatGloss;
                b.AddBox(Vector3.zero, new Vector3(0.09f, 0.13f, 0.02f), C(0.95f, 0.6f, 0.15f) * 0.9f, Quaternion.identity);
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0.025f, 0.1f, 0), new Vector3(0.006f, 0.09f, 0.006f), C(0.95f, 0.95f, 0.95f), Quaternion.Euler(0, 0, -12));   // the straw
            });
            AddProp("pouch", "Food pouch", bungee.At(u0, by + 0.12f, 0.06f), () => "E: have a bite", () => Show("Mac and cheese from a pouch. Delicious, somehow.", 2f), 0.08f);
            AddProp("drink", "Drink bag", bungee.At(u0 + du, by + 0.12f, 0.06f), () => "E: have a sip", () => Show("Slurp. Orange drink, room temperature.", 2f), 0.07f);
            Rattle(pouch.transform, 0.5f); Rattle(drink.transform, 0.5f);
            if (!hab)
            {
                // a family photo and the mission patch
                var photo = Piece("Photo", bungee.At(u0 + du * 2, by + 0.14f, 0.022f), bungee.Face * Quaternion.Euler(0, 0, -4), b =>
                {
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(Vector3.zero, new Vector3(0.14f, 0.11f, 0.008f), C(0.95f, 0.95f, 0.93f), Quaternion.identity);
                    b.Mat = MeshBuilder.MatMatte;
                    b.AddBox(new Vector3(0, 0.012f, -0.005f), new Vector3(0.12f, 0.06f, 0.002f), C(0.45f, 0.7f, 0.95f), Quaternion.identity);   // sky
                    b.AddBox(new Vector3(0, -0.028f, -0.005f), new Vector3(0.12f, 0.025f, 0.002f), C(0.35f, 0.7f, 0.3f), Quaternion.identity);  // grass
                    b.AddSphere(new Vector3(0.04f, 0.03f, -0.006f), 0.01f, C(1f, 0.9f, 0.3f), 8, 5);                                          // sun
                    foreach (float x in new[] { -0.035f, -0.01f, 0.015f })
                    {
                        b.AddSphere(new Vector3(x, -0.002f, -0.007f), 0.009f, C(0.95f, 0.85f, 0.4f), 8, 5);                                   // three Ardlings
                        b.AddBox(new Vector3(x, -0.018f, -0.007f), new Vector3(0.012f, 0.018f, 0.002f), C(0.95f, 0.95f, 0.95f), Quaternion.identity);
                    }
                });
                Rattle(photo.transform, 0.4f);
                AddProp("photo", "Photo", bungee.At(u0 + du * 2, by + 0.14f, 0.05f), () => "A photo from home", () => Show("Everyone back home, waving. You wave back.", 2.5f), 0.07f);
            }
            Spot patchSpot = hab ? bungee : Facet(10);
            float patchY = hab ? 1.75f : 1.72f;
            Piece("Mission patch", patchSpot.At(hab ? 0 : -0.25f, patchY, 0.006f), patchSpot.Face, b =>
            {
                b.Mat = MeshBuilder.MatMatte;
                int s0 = b.v.Count;
                b.AddFrustum(0.004f, 0f, 0.075f, 0.075f, C(0.85f, 0.75f, 0.2f), 24, true, false);
                b.AddFrustum(0.006f, 0.004f, 0.066f, 0.066f, C(0.12f, 0.2f, 0.45f), 24, true, false);
                b.AddFrustum(0.007f, 0.006f, 0.03f, 0.03f, C(0.3f, 0.55f, 0.95f), 16, true, false);   // the world
                XfLocal(b, s0, Vector3.zero, Quaternion.Euler(-90, 0, 0));
            });
            Label(patchSpot.At(hab ? 0 : -0.25f, patchY + 0.045f, 0.014f), patchSpot.Face, "ARDEN", 0.016f, C(1f, 0.9f, 0.4f));
            Label(patchSpot.At(hab ? 0 : -0.25f, patchY - 0.048f, 0.014f), patchSpot.Face, crew.ToUpperInvariant(), 0.012f, C(1f, 0.9f, 0.4f));
            // quilted pads on the bare walls of the round cabin
            if (!hab)
                foreach (int i in new[] { 4, 10 })
                {
                    var s = Facet(i);
                    Piece("Wall pad", s.At(0, 0.48f, 0.012f), s.Face, b =>
                    {
                        b.Mat = MatQuilt;   // (the shader puffs and stitches it)
                        Color cloth = C(0.86f, 0.85f, 0.8f);
                        b.AddBox(Vector3.zero, new Vector3(0.84f, 0.5f, 0.03f), cloth, Quaternion.identity);
                        b.Mat = MatFabric;
                        b.AddBox(new Vector3(0, 0.25f, -0.006f), new Vector3(0.86f, 0.02f, 0.024f), C(0.3f, 0.32f, 0.36f), Quaternion.identity);
                    });
                }
            // placards and notes
            if (!hab)
            {
                Placard(Facet(2).At(0.2f, 0.9f, 0.006f), Facet(2).Face, "FIRE EXT. ↓", C(0.85f, 0.12f, 0.1f), Color.white, 0.14f, 0.035f);
                Placard(Facet(9).At(0, 0.98f, 0.006f), Facet(9).Face, "STOWAGE A1-A3", C(0.95f, 0.95f, 0.93f), C(0.1f, 0.1f, 0.12f), 0.2f, 0.035f);
            }
            Placard(hatchAt + new Vector3(0, 1.45f, roomRound ? -0.3f : -0.47f), Quaternion.Euler(0, 180, 0), "PULL TO OPEN — CHECK HELMET", C(0.95f, 0.8f, 0.15f), C(0.1f, 0.1f, 0.1f), 0.36f, 0.045f);
            float deskW = hab ? 2.0f : 1.6f;
            StickyNote(new Vector3(-deskW / 2 + 0.15f, 0.457f, front - 0.43f), Quaternion.Euler(90, -8, 0), "NOT THIS\nONE!!", C(1f, 0.55f, 0.6f));
            StickyNote(new Vector3(deskW / 2 - 0.2f, 0.457f, front - 0.42f), Quaternion.Euler(90, 10, 0), "GO GO\nGO!", C(0.6f, 1f, 0.6f));
            StickyNote(hatchAt + new Vector3(0.32f, 1.1f, roomRound ? -0.29f : -0.44f), Quaternion.Euler(0, 180, -5), "helmet??", C(1f, 0.92f, 0.35f));
            var door = room.transform.Find("Locker door");
            if (door != null) StickyNote(new Vector3(0.2f, 1.05f, -0.014f), Quaternion.Euler(0, 0, -7), $"snacks are\nfor sharing\n- {crew}", C(0.55f, 0.85f, 1f), door);
            if (hab) BuildGalley(h);
        }

        // the habitat's galley: a food warmer above the locker that you can use
        void BuildGalley(float h)
        {
            Vector3 at = new Vector3(roomW / 2 - 0.55f, 1.56f, -roomL / 2 + 0.2f);
            var face = Quaternion.Euler(0, 180, 0);
            Piece("Food warmer", at, face, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0, 0.15f, 0), new Vector3(0.5f, 0.3f, 0.3f), C(0.82f, 0.83f, 0.85f), Quaternion.identity);
                b.AddBox(new Vector3(-0.06f, 0.15f, -0.152f), new Vector3(0.32f, 0.22f, 0.004f), C(0.1f, 0.1f, 0.12f), Quaternion.identity);
                b.AddBox(new Vector3(0.19f, 0.15f, -0.152f), new Vector3(0.08f, 0.22f, 0.004f), C(0.25f, 0.26f, 0.28f), Quaternion.identity);
            });
            var lit = Piece("Warmer light", at + face * new Vector3(-0.06f, 0.15f, -0.155f), face, b => b.AddBox(Vector3.zero, new Vector3(0.3f, 0.2f, 0.001f), C(1f, 0.6f, 0.2f), Quaternion.identity), true);
            lit.SetActive(false);
            var readout = Label(at + face * new Vector3(0.19f, 0.2f, -0.156f), face, "--", 0.02f, C(0.4f, 1f, 0.5f));
            float doneAt = 0;
            var pr = AddProp("warmer", "Food warmer", at + face * new Vector3(0, 0.15f, -0.2f), () => doneAt > Time.unscaledTime ? "Warming..." : "E: warm up a meal", null, 0.25f);
            pr.sound = "ui_click";
            pr.use = () => { if (doneAt > Time.unscaledTime) return; doneAt = Time.unscaledTime + 4f; Show("Warming up a meal... (4 seconds)", 2f); };
            pr.moving = lit.transform;
            pr.animate = (pp, dt) =>
            {
                bool on = doneAt > Time.unscaledTime;
                if (lit.activeSelf != on) lit.SetActive(on);
                readout.text = on ? $"0:0{Mathf.CeilToInt(doneAt - Time.unscaledTime)}" : "--";
                if (pp.state > 0.5f && !on) { Show("DING! Hot stew is ready. It smells amazing.", 3f); KitSound("clink", 0.8f); }
                pp.state = on ? 1 : 0;
            };
        }

        // ---- loose things that float in weightlessness (and fall when there's weight)

        void BuildFloaters(bool hab, float h)
        {
            (string name, Color col, Vector3 size)[] kinds =
            {
                ("Pen", C(0.15f, 0.3f, 0.85f), new Vector3(0.012f, 0.012f, 0.14f)),
                ("Food pouch", C(0.85f, 0.85f, 0.87f), new Vector3(0.1f, 0.14f, 0.015f)),
                ("Sticky note", C(1f, 0.92f, 0.35f), new Vector3(0.075f, 0.075f, 0.002f)),
                ("Sock", C(0.85f, 0.3f, 0.3f), new Vector3(0.06f, 0.16f, 0.03f)),
                ("Bolt", C(0.7f, 0.7f, 0.72f), new Vector3(0.015f, 0.015f, 0.05f)),
            };
            for (int i = 0; i < kinds.Length; i++)
            {
                var (name, col, size) = kinds[i];
                float a = i * 1.3f;
                Vector3 start = hab ? new Vector3(Mathf.Sin(a) * 0.8f, 1.1f + i * 0.12f, -0.2f + Mathf.Cos(a) * 1.2f) : new Vector3(Mathf.Sin(a) * 0.7f, 1.1f + i * 0.12f, Mathf.Cos(a) * 0.7f - 0.2f);
                var go = Piece("Floating " + name, start, Quaternion.Euler(i * 40, i * 70, i * 20), b =>
                {
                    b.Mat = name == "Bolt" ? MeshBuilder.MatPaint : MeshBuilder.MatMatte;
                    b.AddBox(Vector3.zero, size, col, Quaternion.identity);
                    if (name == "Pen") b.AddBox(new Vector3(0, 0, 0.065f), new Vector3(0.014f, 0.014f, 0.02f), C(0.9f, 0.9f, 0.92f), Quaternion.identity);
                    if (name == "Sock") b.AddBox(new Vector3(0, -0.07f, 0.03f), new Vector3(0.06f, 0.04f, 0.07f), col, Quaternion.identity);
                });
                bool longOne = size.z >= size.x && size.z >= size.y;   // a pen or a bolt lies along the floor; flat things lie flat
                var f = new Floater { t = go.transform, r = Mathf.Max(size.x, size.y, size.z) * 0.5f, restH = (longOne ? Mathf.Max(size.x, size.y) : size.z) * 0.5f + 0.002f,
                    restTilt = longOne ? Quaternion.identity : Quaternion.Euler(90, 0, 0), vel = new Vector3(Mathf.Sin(a * 3), Mathf.Cos(a * 2), Mathf.Sin(a)) * 0.04f, spin = new Vector3(i * 9 + 5, i * 13 + 7, i * 5 + 3) };
                Prop pr = null;
                pr = AddProp("float" + i, name, start, () => $"E: give the {name.ToLowerInvariant()} a push", () =>
                {
                    Vector3 eye = ipos + Vector3.up * Eye;
                    f.vel += (f.t.localPosition - eye).normalized * 1.1f + Vector3.up * 0.4f;
                    f.spin += UnityEngine.Random.insideUnitSphere * 200f;
                }, 0.08f);
                pr.sound = "ui_drop";
                f.prop = pr;
                floaters.Add(f);
            }
        }

        // ---------------------------------------------------------------- every frame

        void UpdateCabinKit(float dt)
        {
            var v = active;
            if (v == null || room == null) return;
            float g = Mathf.Max(0, (float)v.gForce);
            bool weight = v.landed || g > 0.05f;
            // vibration: thrust, thick air, reentry (the cockpit shakes, the camera doesn't)
            float push = v.thrustNow > 1 ? Mathf.Clamp01((float)(v.thrustNow / Math.Max(1e-3, v.mass) / 35.0)) : 0;
            float aero = Mathf.Clamp01((float)(v.dynPressure / 25000.0));
            float heat = Mathf.Clamp01((float)(v.heatFlux / 5e5));
            float want = v.landed ? 0 : Mathf.Clamp01(push * 0.55f + aero * 0.8f + heat * 0.6f);
            vibration = Mathf.MoveTowards(vibration, want, dt * 2f);
            float tt = Time.time;
            foreach (var r in rattlers)
            {
                if (r.t == null) continue;
                float a = vibration * r.amount;
                if (a < 0.001f) { r.t.localPosition = r.basePos; r.t.localRotation = r.baseRot; continue; }
                Vector3 j = new Vector3(Mathf.PerlinNoise(tt * 23f, r.seed) - 0.5f, Mathf.PerlinNoise(r.seed, tt * 27f) - 0.5f, Mathf.PerlinNoise(tt * 19f + r.seed, 3.1f) - 0.5f);
                r.t.localPosition = r.basePos + j * 0.012f * a;
                r.t.localRotation = r.baseRot * Quaternion.Euler(j * 4f * a);
            }
            // the lights flicker in hard shaking or reentry
            if (cabinLights != null && cabinLightMode != 1)
            {
                bool flick = (vibration > 0.6f || heat > 0.3f) && Mathf.PerlinNoise(tt * 9f, 7.7f) < 0.18f + heat * 0.15f;
                if (cabinLights.activeSelf == flick) cabinLights.SetActive(!flick);
            }
            // reentry glows in the windows
            float glowA = heat > 0.02f ? Mathf.Clamp01(heat * 1.6f) * (0.75f + 0.25f * Mathf.PerlinNoise(tt * 6f, 1.3f)) : 0;
            foreach (var (mesh, c) in plasma)
            {
                var cols = mesh.colors;
                if (cols.Length == 0 || Mathf.Abs(cols[0].a - glowA * 0.85f) < 0.01f) continue;
                for (int i = 0; i < cols.Length; i++) cols[i] = new Color(c.r, c.g, c.b, glowA * 0.85f);
                mesh.colors = cols;
            }
            // gauges (they shake a little too) and the attitude ball
            foreach (var gg in gauges)
            {
                float val = 0;
                try { val = Mathf.Clamp01(gg.value()); } catch { }
                if (LampTest) val = 1;
                gg.shown = Mathf.Lerp(gg.shown, val, dt * 4f) + (Mathf.PerlinNoise(tt * 30f, gg.shown * 10f) - 0.5f) * 0.02f * vibration;
                gg.needle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(135, -135, gg.shown));
            }
            if (miniBall != null && root.navball != null && !BreakerOff("nav"))
            {
                root.navball.Orient(v);
                miniBall.localRotation = root.navball.BallRotation;
            }
            // backlit labels (PANEL knob, PANEL LTS breaker) and the flood light (FLOOD knob and breaker)
            float panelK = BreakerOff("panel") ? 0.12f : Mathf.Lerp(0.35f, 1.25f, KitValue("knob_panel", 0.7f));
            foreach (var (tm, c) in kitLabels) if (tm != null) tm.color = new Color(c.r * panelK, c.g * panelK, c.b * panelK, c.a);
            ApplyCabinAmbient();
            UpdateCabinLamps();   // milestone 37
            UpdateSunPatches();
            foreach (var (go, on) in kitLights) { bool o = false; try { o = on() && !BreakerOff("panel") || LampTest; } catch { } if (go.activeSelf != o) go.SetActive(o); }
            if (keypadScreen != null) keypadScreen.text = BreakerOff("nav") ? "" : keypadShow == "" ? "READY" : keypadShow;
            // the fans: streamers flutter, the hum
            bool fans = !BreakerOff("fans");
            float boost = KitValue("fanboost", 0) > 0.5f ? 1.6f : 1f;
            foreach (var (t, seed, rest) in streamers)
            {
                if (t == null) continue;
                float lift = fans ? (30f + 25f * Mathf.PerlinNoise(tt * 3f * boost, seed)) * Mathf.Min(boost, 1.3f) : 0;
                float swirl = fans ? (Mathf.PerlinNoise(seed, tt * 5f * boost) - 0.5f) * 30f : 0;
                if (!weight) lift = Mathf.Max(lift, 60f);   // nothing pulls them down in space
                t.localRotation = rest * Quaternion.Euler(-lift, 0, swirl);   // blown out from the vent
            }
            if (cabinHum != null)
            {
                float vol = Mathf.Lerp(0.2f, 1.6f, KitValue("knob_volume", 0.6f));
                cabinHum.volume = fans ? 0.06f * vol * boost : 0;
                cabinHum.pitch = fans ? 0.6f * boost : 0.6f;
            }
            UpdateAlarm(dt);
            UpdateFloaters(dt, weight, g);
        }

        void UpdateFloaters(float dt, bool weight, float g)
        {
            float h = 2.1f;
            float dtc = Mathf.Min(dt, 0.05f);
            foreach (var f in floaters)
            {
                if (f.t == null) continue;
                Vector3 p = f.t.localPosition;
                if (weight)
                {
                    f.vel += Vector3.down * 9.81f * Mathf.Clamp(g, 0.3f, 4f) * dtc;
                    if (p.y <= f.r + 0.005f && vibration > 0.15f) f.vel += new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0f, 1.5f), UnityEngine.Random.Range(-1f, 1f)) * vibration * 0.4f * dtc * 10f;
                }
                else
                {
                    f.vel *= 1f - 0.03f * dtc;   // the air slows them a little
                    f.vel += new Vector3(Mathf.PerlinNoise(Time.time * 0.3f, f.r * 50f) - 0.5f, Mathf.PerlinNoise(f.r * 70f, Time.time * 0.3f) - 0.5f, 0) * 0.02f * dtc;   // the fans' breeze
                }
                p += f.vel * dtc;
                // the walls, floor and ceiling
                float floor = weight ? f.restH : f.r;
                if (p.y < floor) { p.y = floor; f.vel.y = Mathf.Abs(f.vel.y) * (weight ? 0.25f : 0.7f); if (weight) { f.vel.x *= 0.8f; f.vel.z *= 0.8f; f.spin *= 0.7f; } }
                if (p.y > h - f.r) { p.y = h - f.r; f.vel.y = -Mathf.Abs(f.vel.y) * 0.7f; }
                if (roomRound)
                {
                    var flat = new Vector2(p.x, p.z);
                    float lim = roomR - 0.35f;
                    if (flat.magnitude > lim) { var nrm = flat.normalized; flat = nrm * lim; p.x = flat.x; p.z = flat.y; var vv = new Vector2(f.vel.x, f.vel.z); vv -= nrm * 1.7f * Vector2.Dot(vv, nrm); f.vel.x = vv.x; f.vel.z = vv.y; }
                }
                else
                {
                    float lx = roomW / 2 - 0.15f, lz = roomL / 2 - 0.15f;
                    if (Mathf.Abs(p.x) > lx) { p.x = Mathf.Sign(p.x) * lx; f.vel.x = -f.vel.x * 0.7f; }
                    if (Mathf.Abs(p.z) > lz) { p.z = Mathf.Sign(p.z) * lz; f.vel.z = -f.vel.z * 0.7f; }
                }
                f.t.localPosition = p;
                bool resting = weight && p.y <= f.r && f.vel.y < 0.3f;
                if (resting)
                {
                    f.spin = Vector3.MoveTowards(f.spin, Vector3.zero, 600f * dtc);
                    var yaw = Quaternion.Euler(0, Quaternion.LookRotation(Vector3.ProjectOnPlane(f.t.localRotation * Vector3.forward, Vector3.up).normalized + Vector3.forward * 1e-4f).eulerAngles.y, 0);
                    f.t.localRotation = Quaternion.Slerp(f.t.localRotation, yaw * f.restTilt, dtc * 6f);
                }
                else f.t.localRotation *= Quaternion.Euler(f.spin * dtc);
                if (f.prop != null) f.prop.at = p;
            }
        }

        /// <summary>The cabin's fill light: the light mode, plus the FLOOD knob (and its breaker).</summary>
        void ApplyCabinAmbient()
        {
            if (cabinMat == null) return;
            float baseA = cabinLightMode == 0 ? 0.2f : cabinLightMode == 1 ? 0.06f : 0.1f;   // (milestone 37: the lamps do most of it now)
            float flood = BreakerOff("flood") ? 0 : KitValue("knob_flood", 0.3f);
            float a = Mathf.Max(0.03f, baseA + (flood - 0.3f) * 0.08f);
            if (Mathf.Abs(a - lastAmbient) < 0.002f) return;
            lastAmbient = a;
            cabinMat.SetFloat("_Ambient", a);
        }

        void ClearCabinKit()
        {
            gauges.Clear(); floaters.Clear(); rattlers.Clear(); streamers.Clear(); kitLabels.Clear(); plasma.Clear(); kitLights.Clear(); propById.Clear();
            miniBall = null; cabinAudio = null; cabinHum = null; keypadScreen = null; vibration = 0;
            cabinLamps.Clear(); sunPatches.Clear(); windowBlinds.Clear();
        }
    }
}
