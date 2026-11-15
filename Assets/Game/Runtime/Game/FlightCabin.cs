using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rocket
{
    /// <summary>
    /// Milestone 35 (captaincarl24: "make the inside really detailed and interactive, super interactive, really really super
    /// detailed"): the cabins come alive. Look at something (the dot in the middle) and press E (or click):
    /// - the controls work: STAGE, ABORT (under a cover: press twice), SAS, RCS, lights and gear switches, the throttle lever
    ///   (scroll on it, or E for full / cut), the map screen; in a room each still needs that player's job;
    /// - the screens are live: height, speed, climb; the orbit; fuel, stage and speed budget; signal, crew and supplies; the
    ///   mission clock; warning lights for low fuel, heat, SAS, RCS, gear and lights;
    /// - things to play with: the radio (quick calls to the team, or mission control), the cabin lights (bright, dim, night
    ///   red), a blind on every window, the snack locker, a plant to water, a hanging Ardling plushie to push (it floats in
    ///   space), a fire extinguisher; in habitats a bunk to nap in, an exercise bike, and a game of chess on the table;
    /// - and it's all much more detailed: rivets, seams, pipes, cables, vents, handrails, oxygen tanks, a first-aid box,
    ///   floor plates, harnesses, labels everywhere.
    /// In a room you see the other players walking round with their names over their heads, and the blinds, locker, lights,
    /// plant and plushie are shared: what one player does, everyone sees.
    /// </summary>
    public partial class FlightMode
    {
        class Prop
        {
            public string id, name;
            public Vector3 at;                 // room-local point you look at
            public float size = 0.18f, reach = 1.6f;
            public Func<string> prompt;
            public Action use;                 // E or click
            public Action<float> scroll;       // the mouse wheel while looking at it
            public bool shared;                // its state goes to the others in a room
            public float state, shown, vel;    // what it's set to, what's showing (animated toward it), a swing speed
            public Transform moving;
            public Action<Prop, float> animate;
            public string sound;               // milestone 36: what it sounds like (clink, thud, a click...)
        }

        readonly List<Prop> props = new List<Prop>();
        readonly List<(TextMesh tm, Func<string> text)> screens = new List<(TextMesh, Func<string>)>();
        readonly List<(GameObject go, Func<bool> on)> lamps = new List<(GameObject, Func<bool>)>();
        Prop lookedAt;
        float screenT, abortArmedUntil, plantGrow;
        int cabinLightMode, radioCall, chessMoves;
        GameObject cabinLights;
        static Material puffMat;
        Font cabinFont;
        readonly Dictionary<string, (Vector3 last, float phase)> folkWalk = new Dictionary<string, (Vector3, float)>();

        static readonly string[] RadioCalls = { "All good up here!", "Ready for the next stage!", "Look out of the window!", "Anyone hungry?", "Hold on tight!", "This view is amazing!" };
        static readonly string[] ControlReplies = { "Copy that, flight. Looking good from down here.", "Roger. Keep it steady.", "Mission control here: we see you. Nice flying.", "Copy. The whole town's watching!" };

        // ---------------------------------------------------------------- building

        static Color C(float r, float g, float b) => new Color(r, g, b);

        GameObject Piece(string name, Vector3 at, Quaternion rot, Action<MeshBuilder> build, bool glowing = false, Transform parent = null)
        {
            var b = new MeshBuilder();
            build(b);
            var go = new GameObject(name) { layer = InteriorLayer };
            go.transform.SetParent(parent ?? room.transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rot;
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = glowing ? cabinGlow : cabinMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        TextMesh Label(Vector3 at, Quaternion rot, string text, float height, Color col, TextAnchor anchor = TextAnchor.MiddleCenter, Transform parent = null)
        {
            if (cabinFont == null) cabinFont = Ui.BodyBold != null ? Ui.BodyBold : Resources.Load<Font>("Fonts/Barlow-SemiBold");
            var go = new GameObject("Label " + text) { layer = InteriorLayer };
            go.transform.SetParent(parent ?? room.transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rot;
            var tm = go.AddComponent<TextMesh>();
            tm.font = cabinFont;
            tm.fontSize = 64;
            tm.characterSize = height / 6.4f;   // a line about 'height' metres tall (a line is fontSize x size / 10)
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center;
            tm.color = col;
            tm.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = CabinTextMaterial();
            return tm;
        }

        GameObject Lamp(Vector3 at, Quaternion rot, Color col, Func<bool> on, float size = 0.035f)
        {
            Piece("Lamp base", at, rot, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(Vector3.zero, new Vector3(size * 1.5f, size * 1.5f, 0.012f), C(0.08f, 0.08f, 0.09f), Quaternion.identity); });
            var lit = Piece("Lamp", at + rot * new Vector3(0, 0, -0.008f), rot, b => b.AddBox(Vector3.zero, new Vector3(size, size, 0.01f), col * 1.6f, Quaternion.identity), true);
            lamps.Add((lit, on));
            return lit;
        }

        static Material cabinTextMat;

        /// <summary>
        /// Milestone 36: the cabin's text material. The font's own (Unity's text shader) takes its depth test from the
        /// interface and drew labels through walls; Rocket/Text always tests depth. It follows the font's texture when the
        /// font adds letters.
        /// </summary>
        Material CabinTextMaterial()
        {
            if (cabinTextMat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/RocketText");
                if (sh == null) return cabinFont.material;
                cabinTextMat = new Material(sh) { name = "Cabin text", mainTexture = cabinFont.material.mainTexture };
                var font = cabinFont;
                Font.textureRebuilt += f => { if (f == font && cabinTextMat != null) cabinTextMat.mainTexture = f.material.mainTexture; };
            }
            return cabinTextMat;
        }

        Prop AddProp(string id, string name, Vector3 at, Func<string> prompt, Action use, float size = 0.18f, bool shared = false)
        {
            var p = new Prop { id = id, name = name, at = at, prompt = prompt, use = use, size = size, shared = shared };
            props.Add(p);
            return p;
        }

        /// <summary>Everything you can use, the live screens and the extra detail, for the cabin just built.</summary>
        void BuildCabinProps(bool hab, int crew, float h)
        {
            props.Clear(); screens.Clear(); lamps.Clear(); folkWalk.Clear(); windowBlinds.Clear();
            lookedAt = null; plantGrow = 0; chessMoves = 0; cabinLightMode = 0; abortArmedUntil = 0;
            bool habOnly = hab && !planeRoom;
            hab = hab || planeRoom;   // milestone 44: a plane's cockpit and passenger cabin are long boxes, laid out like a habitat
            float front = hab ? roomL / 2 : roomR - 0.12f;
            float side = hab ? roomW / 2 - 0.35f : 0.98f;
            float consoleZ = hab ? roomL / 2 - 0.95f : roomR - 1.9f;
            BuildDashboard(front, hab ? Mathf.Min(2.0f, roomW - 0.4f) : 1.6f);
            BuildConsoles(side, consoleZ);
            BuildSeatsAndHatch(hab, h);
            BuildCabinLife(hab, h, front);
            if (habOnly) BuildHabExtras(h);
            BuildCabinKit(hab, h, front);   // milestone 36: the overhead panel, gauges, glass, vents, seats, clutter, floating things
            BuildMoreProps(hab, h);         // milestone 42: snacks, a drink maker, a desk toy, a guitar, a photo, a poster, a mirror, a toolbox, a game
            if (planeRoom) { BuildPlaneExtras(h); KeepPlanePropsInside(); }   // milestone 44: the windscreen, thrust levers, bins
            BuildCabinCollision(hab, front);   // milestone 43: solid furniture, handrails for weightlessness
            ResetCabinLight();
            // in a room: how the others left this cabin (the blinds, the locker, the lights, the plant, the chess)
            if (Coop.Active && insidePart != null)
            {
                foreach (var p in props)
                    if (p.shared && p.id != "plushie" && p.id != "bike" && Coop.Current.cabinState.TryGetValue(insidePart.index + "|" + p.id, out float s))
                    { p.state = s; p.shown = s; ApplyCabinState(p); }
                Coop.Current.cabinEvents.Clear();
            }
        }

        static ScriptableRendererFeature aoFeature;
        static bool aoLooked;

        /// <summary>
        /// The world's ambient occlusion off inside (its radius is made for the outside: in a 2 m cabin it filled every
        /// corner with dark grain), back on outside.
        /// </summary>
        internal static void SetWorldAo(bool on)
        {
            if (!aoLooked)
            {
                aoLooked = true;
                try
                {
                    var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                    var renderer = urp != null ? urp.scriptableRenderer : null;
                    var prop = typeof(ScriptableRenderer).GetProperty("rendererFeatures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var list = renderer != null ? prop?.GetValue(renderer) as List<ScriptableRendererFeature> : null;
                    aoFeature = list?.FirstOrDefault(f => f != null && f.GetType().Name.Contains("AmbientOcclusion"));
                }
                catch (Exception e) { Debug.LogWarning("Couldn't find the ambient occlusion: " + e.Message); }
            }
            if (aoFeature != null && aoFeature.isActive != on) aoFeature.SetActive(on);
        }

        /// <summary>The cabin's light back to normal (its material is shared by every cabin).</summary>
        void ResetCabinLight()
        {
            if (cabinMat == null) return;
            cabinMat.SetFloat("_Ambient", 0.2f);
            cabinMat.SetColor("_Tint", Color.white);
        }

        // the desk under the front windows: screens, lamps, switches, STAGE and ABORT
        void BuildDashboard(float front, float width)
        {
            float z = front - 0.42f;
            Piece("Desk", Vector3.zero, Quaternion.identity, b =>
            {
                b.Mat = MatPanel;
                b.AddBox(new Vector3(0, 0.22f, z), new Vector3(width, 0.44f, 0.52f), C(0.32f, 0.34f, 0.37f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.445f, z), new Vector3(width + 0.04f, 0.02f, 0.56f), C(0.18f, 0.19f, 0.21f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.2f, z - 0.265f), new Vector3(width - 0.1f, 0.3f, 0.01f), C(0.26f, 0.28f, 0.3f), Quaternion.identity);
                for (int i = 0; i < 6; i++) b.AddBox(new Vector3(-width / 2 + 0.2f + i * (width - 0.4f) / 5f, 0.12f, z - 0.27f), new Vector3(0.12f, 0.02f, 0.01f), C(0.12f, 0.12f, 0.14f), Quaternion.identity);   // vents
            });
            // the slanted board with three screens
            var boardRot = Quaternion.Euler(35, 0, 0);
            Vector3 boardC = new Vector3(0, 0.62f, front - 0.27f), n = boardRot * Vector3.back;
            Piece("Board", boardC, boardRot, b => { b.Mat = MatPanel; b.AddBox(Vector3.zero, new Vector3(width - 0.12f, 0.36f, 0.04f), C(0.16f, 0.17f, 0.19f), Quaternion.identity); });
            float sw = (width - 0.3f) / 3f;
            string[] titles = { "FLIGHT", "ORBIT", "FUEL" };
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = boardC + boardRot * new Vector3((k - 1) * (sw + 0.03f), 0.01f, 0) + n * 0.022f;
                Piece("Screen bezel", c, boardRot, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(Vector3.zero, new Vector3(sw, 0.27f, 0.006f), C(0.05f, 0.05f, 0.06f), Quaternion.identity); });
                Piece("Screen", c + n * 0.004f, boardRot, b => b.AddBox(Vector3.zero, new Vector3(sw - 0.02f, 0.25f, 0.002f), C(0.03f, 0.1f, 0.16f) * 0.5f, Quaternion.identity), true);
                Label(c + n * 0.008f + boardRot * new Vector3(-sw / 2 + 0.02f, 0.115f, 0), boardRot, titles[k], 0.022f, C(0.45f, 0.8f, 1f), TextAnchor.UpperLeft);
                var tm = Label(c + n * 0.008f + boardRot * new Vector3(-sw / 2 + 0.02f, 0.082f, 0), boardRot, "", 0.026f, C(0.75f, 0.95f, 1f), TextAnchor.UpperLeft);
                int which = k;
                screens.Add((tm, () => ScreenText(which)));
            }
            AddProp("map", "Map screen", boardC + boardRot * new Vector3(0, 0.01f, 0), () => "E: open the map", () => { mapView = true; Show($"The map ({In.Name(K.M)} to come back inside).", 2.5f); }, 0.16f);
            // warning lamps along the top of the board
            (string, Color, Func<bool>)[] warn =
            {
                ("FUEL", C(1f, 0.6f, 0.1f), () => active != null && active.ActiveFuelFraction() < 0.15 && active.hasLaunched),
                ("HEAT", C(1f, 0.2f, 0.15f), () => active?.Hottest != null && active.Hottest.HeatFraction > 0.7f),
                ("SAS", C(0.3f, 1f, 0.4f), () => active != null && active.sas != SasMode.Off),
                ("RCS", C(0.3f, 0.8f, 1f), () => active != null && active.rcsOn),
                ("GEAR", C(0.3f, 1f, 0.4f), () => active != null && active.gearDown),
                ("LIGHTS", C(1f, 0.95f, 0.6f), () => active != null && active.lightsOn),
            };
            for (int i = 0; i < warn.Length; i++)
            {
                var (name, col, on) = warn[i];
                Vector3 at = boardC + boardRot * new Vector3(-0.42f + i * 0.17f, 0.165f, 0) + n * 0.03f;
                Lamp(at, boardRot, col, on, 0.03f);
                Label(at + boardRot * new Vector3(0, -0.032f, 0) + n * 0.002f, boardRot, name, 0.016f, C(0.8f, 0.82f, 0.85f));
            }
            // the switch row on the desk: SAS, RCS, LIGHTS, GEAR
            (string id, string label, Func<bool> on, Action act)[] sw4 =
            {
                ("sas", "SAS", () => active.sas != SasMode.Off, ToggleSas),
                ("rcs", "RCS", () => active.rcsOn, ToggleRcs),
                ("lights", "LIGHTS", () => active.lightsOn, () => FireGroup(Groups.Lights)),
                ("gear", "GEAR", () => active.gearDown, () => FireGroup(Groups.Gear)),
            };
            for (int i = 0; i < sw4.Length; i++)
            {
                var (id, label, on, act) = sw4[i];
                Vector3 at = new Vector3(-width / 2 + 0.4f + i * 0.18f, 0.47f, front - 0.6f);
                Piece("Switch plate", at, Quaternion.identity, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(new Vector3(0, -0.012f, 0), new Vector3(0.09f, 0.012f, 0.07f), C(0.1f, 0.1f, 0.11f), Quaternion.identity); });
                var lever = Piece("Switch", at, Quaternion.identity, b => { b.Mat = MeshBuilder.MatPaint; b.AddBox(new Vector3(0, 0.03f, 0), new Vector3(0.016f, 0.06f, 0.016f), C(0.85f, 0.86f, 0.88f), Quaternion.identity); b.AddSphere(new Vector3(0, 0.062f, 0), 0.013f, C(0.9f, 0.9f, 0.92f), 8, 5); });
                Label(at + new Vector3(0, -0.004f, -0.055f), Quaternion.Euler(90, 0, 0), label, 0.018f, C(0.9f, 0.9f, 0.92f));
                var p = AddProp(id, label, at + new Vector3(0, 0.04f, 0), () => $"E: {label} {(on() ? "off" : "on")}  (now {(on() ? "on" : "off")})", act, 0.09f);
                p.moving = lever.transform;
                p.animate = (pp, dt) => { pp.shown = Mathf.MoveTowards(pp.shown, on() ? 1f : 0f, dt * 8f); pp.moving.localRotation = Quaternion.Euler(Mathf.Lerp(-28f, 28f, pp.shown), 0, 0); };
            }
            // STAGE: a big red button on a yellow-and-black base
            Vector3 st = new Vector3(width / 2 - 0.18f, 0.455f, front - 0.55f);
            Piece("Stage base", st, Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddFrustum(0.02f, 0f, 0.075f, 0.08f, C(0.95f, 0.8f, 0.1f), 20, true, false);
                for (int k = 0; k < 6; k++) b.AddBox(new Vector3(Mathf.Cos(k * 1.047f) * 0.07f, 0.021f, Mathf.Sin(k * 1.047f) * 0.07f), new Vector3(0.03f, 0.002f, 0.012f), C(0.08f, 0.08f, 0.08f), Quaternion.Euler(0, -k * 60f, 0));
            });
            var stageBtn = Piece("Stage button", st + new Vector3(0, 0.02f, 0), Quaternion.identity, b => { b.Mat = MeshBuilder.MatGloss; b.AddFrustum(0.035f, 0f, 0.045f, 0.05f, C(0.85f, 0.1f, 0.08f), 18, true, false); });
            Label(st + new Vector3(0, -0.004f, -0.11f), Quaternion.Euler(90, 0, 0), "STAGE", 0.024f, C(1f, 0.85f, 0.2f));
            var sp = AddProp("stage", "STAGE", st + new Vector3(0, 0.04f, 0), () => active.nextStage < active.stages.Count ? $"E: STAGE  (next: stage {active.nextStage + 1})" : "STAGE: no stages left", () => { if (!TryCountdown()) Stage(); }, 0.1f);
            sp.moving = stageBtn.transform;
            sp.animate = (pp, dt) => { pp.shown = Mathf.MoveTowards(pp.shown, 0, dt * 3f); pp.moving.localPosition = st + new Vector3(0, 0.02f - 0.012f * pp.shown, 0); };
            sp.use = () => { sp.shown = 1; if (!TryCountdown()) Stage(); };
            // ABORT: under a clear cover, press twice
            Vector3 ab = new Vector3(-width / 2 + 0.15f, 0.455f, front - 0.55f);
            Piece("Abort", ab, Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0, 0.01f, 0), new Vector3(0.12f, 0.02f, 0.12f), C(0.12f, 0.12f, 0.13f), Quaternion.identity);
                b.AddFrustum(0.04f, 0.02f, 0.035f, 0.038f, C(0.9f, 0.12f, 0.1f), 16, true, false);
            });
            // (a faint glassy glow, so the red button shows through; hinged at the back)
            var cover = Piece("Abort cover", ab + new Vector3(0, 0.02f, 0.06f), Quaternion.identity, b => b.AddBox(new Vector3(0, 0.035f, -0.06f), new Vector3(0.11f, 0.07f, 0.11f), C(0.12f, 0.16f, 0.2f), Quaternion.identity), true);
            Label(ab + new Vector3(0, -0.004f, -0.11f), Quaternion.Euler(90, 0, 0), "ABORT", 0.024f, C(1f, 0.4f, 0.35f));
            var ap = AddProp("abort", "ABORT", ab + new Vector3(0, 0.04f, 0), () => Time.unscaledTime < abortArmedUntil ? "E again: ABORT! (the crew get pulled clear)" : "E: lift the cover (ABORT)", null, 0.1f);
            ap.use = () =>
            {
                if (Time.unscaledTime < abortArmedUntil) { abortArmedUntil = 0; DoAbort(); return; }
                abortArmedUntil = Time.unscaledTime + 2.5f;
                Show("Cover up. Press E again to abort.", 2f);
            };
            ap.moving = cover.transform;
            ap.animate = (pp, dt) => { pp.shown = Mathf.MoveTowards(pp.shown, Time.unscaledTime < abortArmedUntil ? 1f : 0f, dt * 6f); pp.moving.localRotation = Quaternion.Euler(100f * pp.shown, 0, 0); };   // (up and back)
            // a checklist on a clipboard
            Piece("Clipboard", new Vector3(width / 2 - 0.42f, 0.455f, front - 0.6f), Quaternion.Euler(0, 12, 0), b =>
            {
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(Vector3.zero, new Vector3(0.14f, 0.008f, 0.19f), C(0.55f, 0.4f, 0.25f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.006f, -0.005f), new Vector3(0.12f, 0.004f, 0.16f), C(0.95f, 0.95f, 0.92f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.01f, 0.085f), new Vector3(0.06f, 0.01f, 0.02f), C(0.6f, 0.62f, 0.65f), Quaternion.identity);
            });
            Label(new Vector3(width / 2 - 0.42f, 0.468f, front - 0.58f), Quaternion.Euler(90, 12, 0), "CHECKLIST\n[x] Strap in\n[x] SAS on\n[ ] Look outside", 0.011f, C(0.15f, 0.15f, 0.2f), TextAnchor.UpperCenter);
        }

        string ScreenText(int which)
        {
            var v = active;
            if (v == null) return "";
            switch (which)
            {
                case 0:
                {
                    double vs = Vector3d.Dot(v.SurfaceRelVelocity, v.pos.normalized);
                    return $"{v.body.name}\nHEIGHT {Ui.Dist(v.Altitude)}\nSPEED {v.SurfaceRelVelocity.magnitude:0} m/s\nCLIMB {vs:+0;-0} m/s\nG {v.gForce:0.0}";
                }
                case 1:
                {
                    if (v.landed) return $"LANDED\n{v.body.name}\n{(v.InWater ? "on the sea" : "on the ground")}";
                    var o = trajectory.Count > 0 ? trajectory[0].orbit : null;
                    if (o == null) return "--";
                    string ap = o.IsElliptic ? Ui.Dist(o.Apoapsis - v.body.radius) : "escape";
                    double tilt = Math.Acos(Math.Max(-1, Math.Min(1, o.H.y))) * 180 / Math.PI;   // the orbit's axis against the world's spin axis (+Y)
                    double lo = o.Periapsis - v.body.radius;
                    return $"HIGH {ap}\nLOW {(lo < 0 ? "under the ground" : Ui.Dist(lo))}\nTILT {tilt:0} deg\n{(node != null ? $"BURN IN {Ui.Time(node.ut - ut)}" : "")}";
                }
                default:
                {
                    float fuel = v.ActiveFuelFraction();
                    if (fuel <= 0 && !v.hasLaunched) { double have = v.parts.Sum(p => p.propellant), full = v.parts.Sum(p => (double)p.def.propellant); fuel = full > 0 ? (float)(have / full) : 0; }
                    return $"FUEL {fuel * 100:0}%\nSTAGE {Math.Min(v.nextStage + 1, v.stages.Count)}/{v.stages.Count}\nTHROTTLE {v.throttle * 100:0}%\nSIGNAL {(v.connected ? "OK" : "LOST")}\nFOOD {Supplies.DaysText(v.SupplyDaysLeft())}";
                }
            }
        }

        // the side consoles: the throttle on the right, the radio on the left
        void BuildConsoles(float side, float z)
        {
            foreach (int s in new[] { -1, 1 })
                Piece("Console", new Vector3(s * side, 0, z), Quaternion.identity, b =>
                {
                    b.Mat = MatPanel;
                    b.AddBox(new Vector3(0, 0.3f, 0), new Vector3(0.24f, 0.6f, 0.42f), C(0.3f, 0.32f, 0.35f), Quaternion.identity);
                    b.AddBox(new Vector3(0, 0.605f, 0), new Vector3(0.26f, 0.015f, 0.44f), C(0.16f, 0.17f, 0.19f), Quaternion.identity);
                    for (int k = 0; k < 3; k++) b.AddBox(new Vector3(-s * 0.121f, 0.15f + k * 0.12f, 0), new Vector3(0.004f, 0.08f, 0.3f), C(0.2f, 0.21f, 0.23f), Quaternion.identity);
                });
            // throttle lever
            Vector3 tz = new Vector3(side, 0.62f, z);
            var lever = Piece("Throttle", tz, Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0, 0.08f, 0), new Vector3(0.02f, 0.16f, 0.02f), C(0.7f, 0.72f, 0.75f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.17f, 0), new Vector3(0.09f, 0.035f, 0.04f), C(0.15f, 0.15f, 0.17f), Quaternion.identity);
            });
            Piece("Throttle slot", tz, Quaternion.identity, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(new Vector3(0, 0.002f, 0), new Vector3(0.04f, 0.004f, 0.3f), C(0.05f, 0.05f, 0.06f), Quaternion.identity); });
            Label(tz + new Vector3(0, 0.005f, -0.19f), Quaternion.Euler(90, 0, 0), "THROTTLE", 0.02f, C(0.9f, 0.9f, 0.92f));
            var tp = AddProp("throttle", "Throttle", tz + new Vector3(0, 0.12f, 0), () => $"E: throttle {(active.throttle > 0.05 ? "cut" : "full")}  ·  scroll: set it  (now {active.throttle * 100:0}%)", () => SetThrottleFromCabin(active.throttle > 0.05 ? 0 : 1), 0.13f);
            tp.scroll = sc => SetThrottleFromCabin(Mathf.Clamp01((float)active.throttle + (sc > 0 ? 0.1f : -0.1f)));
            tp.moving = lever.transform;
            tp.animate = (pp, dt) => { pp.shown = Mathf.MoveTowards(pp.shown, (float)active.throttle, dt * 3f); pp.moving.localRotation = Quaternion.Euler(Mathf.Lerp(-35f, 35f, pp.shown), 0, 0); };
            // radio: a little screen with the last messages, a handset
            Vector3 rz = new Vector3(-side, 0.62f, z);
            var rot = Quaternion.Euler(35, 0, 0);
            Piece("Radio", rz + new Vector3(0, 0.08f, 0.05f), rot, b =>
            {
                b.Mat = MatPanel;
                b.AddBox(Vector3.zero, new Vector3(0.22f, 0.16f, 0.05f), C(0.2f, 0.22f, 0.2f), Quaternion.identity);
                b.Mat = MatPerf;
                b.AddBox(new Vector3(0.07f, -0.015f, -0.026f), new Vector3(0.06f, 0.09f, 0.003f), C(0.6f, 0.62f, 0.64f), Quaternion.identity);   // speaker grille
            });
            Piece("Radio screen", rz + new Vector3(0, 0.08f, 0.05f) + rot * new Vector3(-0.03f, 0.01f, -0.027f), rot, b => b.AddBox(Vector3.zero, new Vector3(0.13f, 0.1f, 0.002f), C(0.1f, 0.25f, 0.1f) * 0.6f, Quaternion.identity), true);
            var rtm = Label(rz + new Vector3(0, 0.08f, 0.05f) + rot * new Vector3(-0.09f, 0.055f, -0.03f), rot, "", 0.009f, C(0.6f, 1f, 0.6f), TextAnchor.UpperLeft);
            screens.Add((rtm, RadioText));
            Piece("Handset", rz + new Vector3(0.06f, 0.0f, -0.12f), Quaternion.Euler(0, 20, 0), b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(Vector3.zero, new Vector3(0.04f, 0.03f, 0.14f), C(0.12f, 0.12f, 0.13f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.02f, 0.055f), new Vector3(0.045f, 0.02f, 0.035f), C(0.1f, 0.1f, 0.11f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.02f, -0.055f), new Vector3(0.045f, 0.02f, 0.035f), C(0.1f, 0.1f, 0.11f), Quaternion.identity);
            });
            Label(rz + new Vector3(0, 0.01f, -0.19f), Quaternion.Euler(90, 0, 0), "RADIO", 0.02f, C(0.9f, 0.9f, 0.92f));
            AddProp("radio", "Radio", rz + new Vector3(0, 0.1f, 0), () => $"E: radio: \"{RadioCalls[radioCall % RadioCalls.Length]}\"", RadioCall, 0.15f);
        }

        string RadioText()
        {
            if (BreakerOff("radio")) return "";
            if (Coop.Active)
            {
                var chat = Coop.Current.room.State.chat;
                return string.Join("\n", chat.Skip(Math.Max(0, chat.Count - 5)).Select(l => l.Length > 26 ? l.Substring(0, 26) + "…" : l));
            }
            return "MISSION CONTROL\nchannel 1  ·  clear\n" + (radioCall > 0 ? ControlReplies[(radioCall - 1) % ControlReplies.Length].Substring(0, Math.Min(26, ControlReplies[(radioCall - 1) % ControlReplies.Length].Length)) + "…" : "press E to call");
        }

        void RadioCall()
        {
            if (BreakerOff("radio")) { Show("The radio's dead: its breaker is pulled (the overhead panel).", 2.5f); return; }
            string line = RadioCalls[radioCall % RadioCalls.Length];
            radioCall++;
            GameAudio.I?.Click();
            if (Coop.Active) { Coop.Current.room.Chat(line); return; }
            Show($"You: \"{line}\"\nMission control: \"{ControlReplies[(radioCall - 1) % ControlReplies.Length]}\"", 4f);
        }

        void SetThrottleFromCabin(float t)
        {
            if (Coop.Active && !Coop.Current.CanDo(CAct.Steer)) { Show($"That's the pilot's job ({Coop.Current.Holder(Coop.Pilot)}).", 2.5f); return; }
            if (t > active.throttle && BlockedNoSignal("throttle up")) return;
            active.throttle = t;   // (a pilot on another device: their throttle goes to the host with their steering)
        }

        // seats with harnesses (sit to fly), the hatch with its wheel (open to go out)
        void BuildSeatsAndHatch(bool hab, float h)
        {
            for (int i = 0; i < seats.Count; i++)
            {
                var s = seats[i];
                Piece("Harness", s, Quaternion.identity, b =>
                {
                    b.Mat = MatFabric;
                    b.AddBox(new Vector3(-0.12f, 0.55f, -0.2f), new Vector3(0.05f, 0.45f, 0.01f), C(0.15f, 0.15f, 0.17f), Quaternion.Euler(-12, 0, 0));
                    b.AddBox(new Vector3(0.12f, 0.55f, -0.2f), new Vector3(0.05f, 0.45f, 0.01f), C(0.15f, 0.15f, 0.17f), Quaternion.Euler(-12, 0, 0));
                    b.AddBox(new Vector3(0, 0.4f, -0.17f), new Vector3(0.06f, 0.06f, 0.02f), C(0.7f, 0.72f, 0.75f), Quaternion.identity);
                    b.AddBox(new Vector3(0, 0.82f, -0.29f), new Vector3(0.3f, 0.12f, 0.06f), C(0.25f, 0.35f, 0.5f) * 0.8f, Quaternion.Euler(-12, 0, 0));   // headrest
                });
                AddProp("seat" + i, "Seat", s + new Vector3(0, 0.45f, 0), () => "E: sit down and fly", () => { LeaveInterior(); Show("At the controls.", 2f); }, 0.3f);
            }
            // the hatch wheel turns as it opens
            Vector3 hw = hatchAt + (roomRound ? new Vector3(0, 0.8f, -0.28f) : new Vector3(0, 0.8f, -0.44f));
            var wheel = Piece("Hatch wheel", hw, Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4;
                    b.AddBox(new Vector3(Mathf.Cos(a) * 0.17f, Mathf.Sin(a) * 0.17f, 0), new Vector3(0.07f, 0.03f, 0.03f), C(0.9f, 0.6f, 0.15f), Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90));
                }
                for (int k = 0; k < 4; k++) b.AddBox(Vector3.zero, new Vector3(0.34f, 0.02f, 0.02f), C(0.85f, 0.55f, 0.12f), Quaternion.Euler(0, 0, k * 45));
                b.AddSphere(Vector3.zero, 0.035f, C(0.3f, 0.3f, 0.32f), 10, 6);
            });
            Label(hw + new Vector3(0, 0.42f, 0.02f), Quaternion.Euler(0, 180, 0), "HATCH", 0.035f, C(0.95f, 0.75f, 0.2f));
            var hp = AddProp("hatch", "Hatch", hw, () => "E: open the hatch and go out", null, 0.3f);
            hp.moving = wheel.transform;
            hp.use = () => { hp.state = 1; hp.vel = Time.unscaledTime; };
            hp.animate = (pp, dt) =>
            {
                if (pp.state < 1) return;
                pp.shown += dt * 540f;
                pp.moving.localRotation = Quaternion.Euler(0, 0, pp.shown);
                if (Time.unscaledTime - pp.vel > 0.7f) { pp.state = 0; var part = insidePart; LeaveInterior(); GoEva(part); }
            };
        }

        // lights, blinds, the locker, the plant, the plushie, the extinguisher, oxygen, first aid, the clock, pipes and rivets
        void BuildCabinLife(bool hab, float h, float front)
        {
            float hl = hab ? roomL / 2 : 0, hw = hab ? roomW / 2 : 0;
            Vector3 OnWall(float angleDeg, float y, float inset)
            {
                // a point on the round wall (capsule) at an angle from ahead, a little in from it
                float a = angleDeg * Mathf.Deg2Rad;
                float r = roomR * Mathf.Cos(Mathf.PI / 12f) - inset;   // the flat walls sit inside the circle
                return new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
            }
            Quaternion FaceIn(Vector3 at) => Quaternion.LookRotation(new Vector3(at.x, 0, at.z).normalized, Vector3.up);   // its -Z toward the middle
            // cabin lights: bright, dim, night red
            cabinLights = room.transform.Find("Lights")?.gameObject;
            Vector3 ls = roomRound ? OnWall(135, 0.95f, 0.03f) : new Vector3(-0.8f, 0.95f, -hl + 0.03f);
            Quaternion lr = roomRound ? FaceIn(ls) : Quaternion.Euler(0, 180, 0);
            Piece("Light switch", ls, lr, b => { b.Mat = MeshBuilder.MatPaint; b.AddBox(Vector3.zero, new Vector3(0.09f, 0.13f, 0.015f), C(0.9f, 0.9f, 0.88f), Quaternion.identity); b.AddBox(new Vector3(0, 0, -0.012f), new Vector3(0.025f, 0.05f, 0.015f), C(0.3f, 0.3f, 0.32f), Quaternion.identity); });
            Label(ls + Vector3.up * 0.1f + lr * new Vector3(0, 0, -0.01f), lr, "CABIN LIGHTS", 0.016f, C(0.2f, 0.2f, 0.25f));
            var lp = AddProp("cabinlights", "Cabin lights", ls, () => $"E: cabin lights {(new[] { "dim", "night red", "bright" })[cabinLightMode]}", null, 0.12f, true);
            lp.use = () => { lp.state = (cabinLightMode + 1) % 3; ApplyCabinState(lp); SendCabin(lp); };
            // a blind on each window
            foreach (var (c, n, wdt, sill, head) in Windows(h))
            {
                var q = Quaternion.LookRotation(-n, Vector3.up);
                var blind = Piece("Blind", c + n * 0.085f + Vector3.up * head, q, b => { b.Mat = MeshBuilder.MatMatte; b.AddBox(new Vector3(0, -0.5f, 0), new Vector3(wdt, 1f, 0.012f), C(0.82f, 0.8f, 0.72f), Quaternion.identity); for (int k = 0; k < 10; k++) b.AddBox(new Vector3(0, -0.05f - k * 0.1f, -0.007f), new Vector3(wdt, 0.006f, 0.004f), C(0.7f, 0.68f, 0.6f), Quaternion.identity); });
                float full = head - sill;
                blind.transform.localScale = new Vector3(1, 0.02f, 1);
                var bp = AddProp("blind" + props.Count, "Blind", c + Vector3.up * ((sill + head) / 2), null, null, 0.4f, true);
                bp.reach = 2.4f;
                windowBlinds.Add(bp);
                bp.prompt = () => bp.state > 0.5f ? "E: open the blind" : "E: close the blind";
                bp.use = () => { bp.state = bp.state > 0.5f ? 0 : 1; SendCabin(bp); };
                bp.moving = blind.transform;
                bp.animate = (pp, dt) => { pp.shown = Mathf.MoveTowards(pp.shown, pp.state, dt * 1.5f); pp.moving.localScale = new Vector3(1, Mathf.Max(0.02f, pp.shown * full), 1); };
            }
            // the snack locker: a door that opens, snacks inside
            Vector3 lk = roomRound ? OnWall(225, 0, 0.25f) : new Vector3(hw - 0.55f, 0, -hl + 0.25f);
            Quaternion lq = roomRound ? FaceIn(lk) : Quaternion.Euler(0, 180, 0);   // (its forward into the wall)
            Piece("Locker", lk, lq, b =>
            {
                // an open-fronted box: back, sides, top and bottom (the door covers the front)
                b.Mat = MatPanel;
                Color shell = C(0.85f, 0.48f, 0.15f);
                b.AddBox(new Vector3(0, 0.75f, 0.24f), new Vector3(0.6f, 1.5f, 0.02f), C(0.3f, 0.22f, 0.15f), Quaternion.identity);
                b.AddBox(new Vector3(-0.29f, 0.75f, 0.1f), new Vector3(0.02f, 1.5f, 0.3f), shell, Quaternion.identity);
                b.AddBox(new Vector3(0.29f, 0.75f, 0.1f), new Vector3(0.02f, 1.5f, 0.3f), shell, Quaternion.identity);
                b.AddBox(new Vector3(0, 1.49f, 0.1f), new Vector3(0.6f, 0.02f, 0.3f), shell, Quaternion.identity);
                b.AddBox(new Vector3(0, 0.01f, 0.1f), new Vector3(0.6f, 0.02f, 0.3f), shell, Quaternion.identity);
                b.Mat = MeshBuilder.MatMatte;
                for (int s = 0; s < 3; s++) b.AddBox(new Vector3(0, 0.4f + s * 0.4f, 0.1f), new Vector3(0.56f, 0.015f, 0.26f), C(0.75f, 0.75f, 0.75f), Quaternion.identity);
                Color[] packs = { C(0.9f, 0.2f, 0.2f), C(0.2f, 0.6f, 0.9f), C(0.95f, 0.85f, 0.2f), C(0.3f, 0.8f, 0.4f) };
                for (int s = 0; s < 3; s++) for (int k = 0; k < 4; k++) b.AddBox(new Vector3(-0.19f + k * 0.125f, 0.47f + s * 0.4f, 0.1f), new Vector3(0.1f, 0.12f, 0.08f), packs[(s + k) % 4], Quaternion.Euler(0, (k - 1.5f) * 6, 0));
                b.AddBox(new Vector3(0, 0.2f, 0.1f), new Vector3(0.4f, 0.3f, 0.2f), C(0.3f, 0.5f, 0.75f), Quaternion.identity);   // a water box on the floor
            });
            var door = Piece("Locker door", lk + lq * new Vector3(-0.29f, 0, -0.06f), lq, b =>
            {
                b.Mat = MatPanel;
                b.AddBox(new Vector3(0.29f, 0.75f, 0), new Vector3(0.58f, 1.48f, 0.02f), C(0.9f, 0.52f, 0.18f), Quaternion.identity);
                b.AddBox(new Vector3(0.52f, 0.8f, -0.02f), new Vector3(0.03f, 0.14f, 0.02f), C(0.3f, 0.3f, 0.32f), Quaternion.identity);
                for (int k = 0; k < 4; k++) b.AddBox(new Vector3(0.29f, 1.3f - k * 0.025f, -0.012f), new Vector3(0.3f, 0.008f, 0.004f), C(0.6f, 0.35f, 0.1f), Quaternion.identity);
            });
            Label(new Vector3(0.29f, 1.32f, -0.015f), Quaternion.identity, "SNACKS", 0.04f, C(0.25f, 0.12f, 0.05f), TextAnchor.MiddleCenter, door.transform);
            var dp = AddProp("locker", "Locker", lk + lq * new Vector3(0, 0.8f, -0.08f), null, null, 0.35f, true);
            dp.prompt = () => dp.state > 0.5f ? "E: grab a snack  ·  E twice quickly: close it" : "E: open the snack locker";
            dp.use = () =>
            {
                if (dp.state < 0.5f || Time.unscaledTime - dp.vel < 0.6f) { dp.state = dp.state > 0.5f ? 0 : 1; SendCabin(dp); }
                else Show(new[] { "Mmm, Tal-cheese crackers.", "Freeze-dried noodles. Still crunchy.", "A space biscuit. It tastes like space.", "Dried Ochre-berries! The good ones." }[UnityEngine.Random.Range(0, 4)], 2.5f);
                dp.vel = Time.unscaledTime;
            };
            dp.moving = door.transform;
            dp.animate = (pp, dt) => { pp.shown = Mathf.MoveTowards(pp.shown, pp.state, dt * 3f); pp.moving.localRotation = lq * Quaternion.Euler(0, 105f * Mathf.SmoothStep(0, 1, pp.shown), 0); };
            // a plant to water
            Vector3 pl = roomRound ? OnWall(52, 0.45f, 0.3f) : new Vector3(roomW / 2 - 0.3f, 0.45f, 0.2f);
            Piece("Plant stand", new Vector3(pl.x, 0, pl.z), Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0, 0.44f, 0), new Vector3(0.22f, 0.02f, 0.22f), C(0.55f, 0.4f, 0.28f), Quaternion.identity);
                for (int k = 0; k < 4; k++) b.AddBox(new Vector3((k % 2 == 0 ? -1 : 1) * 0.09f, 0.215f, (k < 2 ? -1 : 1) * 0.09f), new Vector3(0.02f, 0.43f, 0.02f), C(0.45f, 0.32f, 0.22f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.12f, 0), new Vector3(0.2f, 0.015f, 0.2f), C(0.5f, 0.36f, 0.25f), Quaternion.identity);
            });
            Piece("Pot", pl, Quaternion.identity, b => { b.Mat = MeshBuilder.MatPaint; b.AddFrustum(0.12f, 0f, 0.07f, 0.05f, C(0.75f, 0.4f, 0.25f), 14, false, true); b.AddFrustum(0.115f, 0.1f, 0.066f, 0.066f, C(0.3f, 0.2f, 0.12f), 14, true, false); });
            var leaves = Piece("Plant", pl + Vector3.up * 0.11f, Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatMatte;
                for (int k = 0; k < 7; k++)
                {
                    float a = k * 2.4f;
                    b.AddSphere(new Vector3(Mathf.Cos(a) * 0.04f, 0.05f + k * 0.022f, Mathf.Sin(a) * 0.04f), 0.035f - k * 0.002f, C(0.2f, 0.55f + k * 0.03f, 0.2f), 8, 5);
                }
                b.AddBox(new Vector3(0, 0.08f, 0), new Vector3(0.01f, 0.16f, 0.01f), C(0.2f, 0.4f, 0.15f), Quaternion.identity);
            });
            var pp2 = AddProp("plant", "Plant", pl + Vector3.up * 0.15f, () => plantGrow >= 0.95f ? "The plant's as big as it gets. It looks happy." : "E: water the plant", null, 0.14f, true);
            pp2.use = () => { pp2.state = Mathf.Min(1f, plantGrow + 0.12f); ApplyCabinState(pp2); SendCabin(pp2); Show("You water the plant. It perks up.", 2f); };
            pp2.moving = leaves.transform;
            pp2.animate = (pp, dt) => { float s = 1f + plantGrow * 0.8f; pp.moving.localScale = Vector3.Lerp(pp.moving.localScale, new Vector3(s, s, s), dt * 2f); };
            // the plushie: a little Ardling on a string (it floats when nothing's pulling)
            Vector3 ph = roomRound ? new Vector3(0.45f, h - 0.02f, 0.2f) : new Vector3(0.4f, h - 0.02f, 1.6f);
            var pivot = new GameObject("Plushie") { layer = InteriorLayer };
            pivot.transform.SetParent(room.transform, false);
            pivot.transform.localPosition = ph;
            Piece("String", Vector3.zero, Quaternion.identity, b => b.AddBox(new Vector3(0, -0.2f, 0), new Vector3(0.004f, 0.4f, 0.004f), C(0.9f, 0.9f, 0.9f), Quaternion.identity), false, pivot.transform);
            var toy = MyArdling.Build(Ardlings.LookFor("Plushie Ardling"), InteriorLayer);
            toy.transform.SetParent(pivot.transform, false);
            toy.transform.localScale = Vector3.one * 0.16f;
            IndoorLit(toy);
            toy.transform.localPosition = new Vector3(0, -0.4f - Ardlings.Height * 0.16f, 0);
            var tp = AddProp("plushie", "Plushie", ph + Vector3.down * 0.48f, () => "E: give the plushie a push", null, 0.12f, true);
            tp.moving = pivot.transform;
            tp.use = () => { tp.vel += UnityEngine.Random.Range(150f, 260f) * (UnityEngine.Random.value < 0.5f ? -1 : 1); SendCabin(tp, tp.vel); };
            tp.animate = (pp, dt) =>
            {
                // a pendulum under the ship's gravity (none in space: it just drifts)
                bool weight = active != null && (active.landed || active.gForce > 0.05);
                float g = weight ? 9f * Mathf.Clamp((float)active.gForce, 0.3f, 3f) : 0.15f;
                pp.vel += -g * Mathf.Sin(pp.shown * Mathf.Deg2Rad) * 40f * dt;
                pp.vel *= weight ? 1f - 0.4f * dt : 1f - 0.05f * dt;
                pp.shown += pp.vel * dt;
                if (!weight && Mathf.Abs(pp.vel) < 3f) pp.vel += Mathf.Sin(Time.time * 0.7f) * 2f * dt;
                pp.moving.localRotation = Quaternion.Euler(pp.shown, Time.time * (weight ? 0 : 6f), pp.shown * 0.3f);
            };
            // the fire extinguisher
            Vector3 fx = roomRound ? OnWall(80, 0.35f, 0.08f) : new Vector3(roomW / 2 - 0.12f, 0.35f, hl - 0.15f);
            Piece("Extinguisher", fx, Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatGloss;
                b.AddFrustum(0.36f, 0f, 0.055f, 0.055f, C(0.85f, 0.08f, 0.06f), 14, true, true);
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0, 0.39f, 0), new Vector3(0.05f, 0.05f, 0.05f), C(0.2f, 0.2f, 0.22f), Quaternion.identity);
                b.AddBox(new Vector3(0.03f, 0.42f, 0), new Vector3(0.09f, 0.015f, 0.02f), C(0.25f, 0.25f, 0.27f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.18f, 0.056f), new Vector3(0.07f, 0.1f, 0.004f), C(0.95f, 0.95f, 0.9f), Quaternion.identity);
            });
            AddProp("extinguisher", "Fire extinguisher", fx + Vector3.up * 0.25f, () => "E: test the fire extinguisher", () => { Show("Fwoosh! (The cabin's fine. It was a test.)", 2f); PuffAt(fx + Vector3.up * 0.42f); }, 0.12f);
            // oxygen tanks, a first-aid box, the clock, the vents, the pipes, the rivets
            BuildCabinDetail(hab, h, OnWall, FaceIn);
        }

        IEnumerable<(Vector3 c, Vector3 n, float w, float sill, float head)> Windows(float h)
        {
            if (planeRoom) { foreach (var w in PlaneWindows()) yield return w; yield break; }   // milestone 44
            if (roomRound)
            {
                const int sides = 12;
                foreach (int i in new[] { 0, 11, 3, 8 })
                {
                    float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                    Vector3 p0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * roomR, p1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * roomR;
                    Vector3 n = -(p0 + p1).normalized;
                    yield return (Vector3.Lerp(p0, p1, 0.5f), n, (p1 - p0).magnitude * 0.76f, 0.5f, 1.55f);
                }
            }
            else
            {
                float hw = roomW / 2, hl = roomL / 2, bay = roomL / 4;
                for (int k = 0; k < 4; k++)
                {
                    float z = -hl + (k + 0.5f) * bay;
                    yield return (new Vector3(-hw, 0, z), Vector3.right, bay * 0.76f, 0.55f, 1.5f);
                    yield return (new Vector3(hw, 0, z), Vector3.left, bay * 0.76f, 0.55f, 1.5f);
                }
            }
        }

        void BuildCabinDetail(bool hab, float h, Func<float, float, float, Vector3> onWall, Func<Vector3, Quaternion> faceIn)
        {
            var b = new MeshBuilder();
            var glow = new MeshBuilder();
            if (roomRound)
            {
                const int sides = 12;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                    Vector3 p0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * roomR, p1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * roomR;
                    Vector3 n = -(p0 + p1).normalized;
                    // a seam at each corner with rivets up it
                    b.Mat = MeshBuilder.MatMatte;
                    b.AddBox(p0 * 0.985f + Vector3.up * h / 2, new Vector3(0.03f, h, 0.03f), C(0.6f, 0.61f, 0.6f), Quaternion.LookRotation(n));
                    b.Mat = MeshBuilder.MatPaint;
                    for (float y = 0.25f; y < h - 0.1f; y += 0.22f) b.AddSphere(p0 * 0.975f + Vector3.up * y + Vector3.Cross(Vector3.up, n) * 0.025f, 0.009f, C(0.7f, 0.71f, 0.72f), 6, 4);
                    // pipes round the top: yellow gas, blue water
                    Vector3 m = Vector3.Lerp(p0, p1, 0.5f) * 0.96f;
                    var along = Quaternion.LookRotation((p1 - p0).normalized);
                    b.AddBox(m + Vector3.up * (h - 0.1f), new Vector3(0.04f, 0.04f, (p1 - p0).magnitude), C(0.9f, 0.75f, 0.2f), along);
                    b.AddBox(m * 0.985f + Vector3.up * (h - 0.17f), new Vector3(0.035f, 0.035f, (p1 - p0).magnitude), C(0.25f, 0.5f, 0.85f), along);
                    // handrails on the solid walls, cables down two of them
                    bool window = i == 0 || i == 11 || i == 3 || i == 8, hatchSide = i == 5 || i == 6;
                    if (!window && !hatchSide && i != 7)   // (7: the snack locker)
                    {
                        b.Mat = MatBrushed;
                        b.AddBox(m * 0.97f + Vector3.up * 1.2f, new Vector3(0.025f, 0.025f, (p1 - p0).magnitude * 0.7f), C(0.85f, 0.86f, 0.88f), along);
                        b.Mat = MeshBuilder.MatPaint;
                        for (int s = -1; s <= 1; s += 2) b.AddBox(m * 0.985f + Vector3.up * 1.2f + (p1 - p0).normalized * s * (p1 - p0).magnitude * 0.33f, new Vector3(0.02f, 0.02f, 0.06f), C(0.6f, 0.6f, 0.62f), Quaternion.LookRotation(n));
                    }
                    if (i == 1)
                        for (int c = 0; c < 3; c++) b.AddBox(m * 0.975f + Vector3.up * (h / 2) + (p1 - p0).normalized * (c - 1) * 0.04f, new Vector3(0.018f, h - 0.3f, 0.018f), c == 1 ? C(0.8f, 0.15f, 0.12f) : C(0.1f, 0.1f, 0.11f), Quaternion.LookRotation(n));
                    if (i == 2 || i == 9)
                    {
                        // a vent with slats
                        var q = Quaternion.LookRotation(-n);
                        b.Mat = MatPerf;
                        b.AddBox(m * 0.985f + Vector3.up * 1.75f, new Vector3(0.35f, 0.2f, 0.02f), C(0.55f, 0.56f, 0.58f), q);
                        b.Mat = MatBrushed;
                        for (int s = 0; s < 5; s++) b.AddBox(m * 0.975f + Vector3.up * (1.67f + s * 0.04f), new Vector3(0.32f, 0.008f, 0.015f), C(0.12f, 0.12f, 0.13f), q);
                    }
                }
                // floor plates: a ring and spokes
                b.Mat = MatBrushed;
                for (int k = 0; k < 12; k++)
                {
                    float a = (k + 0.5f) * Mathf.PI / 6;
                    b.AddBox(new Vector3(Mathf.Sin(a) * roomR * 0.5f, 0.004f, Mathf.Cos(a) * roomR * 0.5f), new Vector3(0.02f, 0.006f, roomR * 0.9f), C(0.38f, 0.4f, 0.43f), Quaternion.Euler(0, a * Mathf.Rad2Deg, 0));
                }
                int ringN = 32;
                for (int k = 0; k < ringN; k++)
                {
                    float a = k * Mathf.PI * 2 / ringN;
                    b.AddBox(new Vector3(Mathf.Sin(a) * roomR * 0.45f, 0.005f, Mathf.Cos(a) * roomR * 0.45f), new Vector3(roomR * 0.09f, 0.006f, 0.02f), C(0.38f, 0.4f, 0.43f), Quaternion.Euler(0, a * Mathf.Rad2Deg, 0));
                }
                // ceiling light ring (glowing)
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4;
                    glow.AddBox(new Vector3(Mathf.Sin(a) * roomR * 0.62f, h - 0.03f, Mathf.Cos(a) * roomR * 0.62f), new Vector3(0.18f, 0.015f, 0.08f), C(1f, 0.95f, 0.85f) * 0.35f, Quaternion.Euler(0, a * Mathf.Rad2Deg, 0));
                }
            }
            else
            {
                float hw = roomW / 2, hl = roomL / 2;
                b.Mat = MeshBuilder.MatPaint;
                for (int k = 0; k <= 4; k++)
                {
                    float z = -hl + k * (roomL / 4f);
                    foreach (int s in new[] { -1, 1 })
                    {
                        b.AddBox(new Vector3(s * (hw - 0.015f), h / 2, z), new Vector3(0.03f, h, 0.05f), C(0.6f, 0.61f, 0.6f), Quaternion.identity);
                        for (float y = 0.25f; y < h - 0.1f; y += 0.22f) b.AddSphere(new Vector3(s * (hw - 0.032f), y, z + 0.04f), 0.009f, C(0.7f, 0.71f, 0.72f), 6, 4);
                    }
                    b.AddBox(new Vector3(0, h - 0.015f, z), new Vector3(roomW, 0.03f, 0.06f), C(0.6f, 0.61f, 0.6f), Quaternion.identity);
                }
                foreach (int s in new[] { -1, 1 })
                {
                    b.AddBox(new Vector3(s * (hw - 0.08f), h - 0.1f, 0), new Vector3(0.04f, 0.04f, roomL), C(0.9f, 0.75f, 0.2f), Quaternion.identity);
                    b.AddBox(new Vector3(s * (hw - 0.14f), h - 0.1f, 0), new Vector3(0.035f, 0.035f, roomL), C(0.25f, 0.5f, 0.85f), Quaternion.identity);
                    b.Mat = MatBrushed;
                    b.AddBox(new Vector3(s * (hw - 0.04f), 1.62f, 0), new Vector3(0.025f, 0.025f, roomL - 0.6f), C(0.85f, 0.86f, 0.88f), Quaternion.identity);
                    b.Mat = MeshBuilder.MatPaint;
                }
                b.Mat = MatBrushed;
                for (float z = -hl + 0.25f; z < hl; z += 0.5f) b.AddBox(new Vector3(0, 0.004f, z), new Vector3(roomW * 0.9f, 0.006f, 0.02f), C(0.38f, 0.4f, 0.43f), Quaternion.identity);
            }
            // oxygen tanks by the hatch, a first-aid box, a fire blanket
            Vector3 o2 = roomRound ? onWall(150, 0, 0.15f) : new Vector3(-1.2f, 0, -roomL / 2 + 0.2f);
            b.Mat = MeshBuilder.MatGloss;
            for (int k = 0; k < 2; k++)
            {
                int s0 = b.v.Count;
                b.AddFrustum(0.9f, 0f, 0.08f, 0.08f, C(0.2f, 0.65f, 0.3f), 14, true, true);
                for (int i = s0; i < b.v.Count; i++) b.v[i] += o2 + new Vector3(k * 0.18f - 0.09f, 0, 0) * (roomRound ? 0.8f : 1f);
                b.AddSphere(o2 + new Vector3(k * 0.18f - 0.09f, 0.9f, 0), 0.08f, C(0.2f, 0.65f, 0.3f), 12, 6);
            }
            b.Mat = MeshBuilder.MatPaint;
            Vector3 fa = roomRound ? onWall(70, 1.45f, 0.04f) : new Vector3(-1.3f, 1.75f, roomL / 2 - 0.04f);
            var fq = roomRound ? faceIn(fa) : Quaternion.identity;   // (forward into the wall)
            b.AddBox(fa, new Vector3(0.28f, 0.22f, 0.07f), C(0.95f, 0.95f, 0.93f), fq);
            b.AddBox(fa + fq * new Vector3(0, 0, -0.037f), new Vector3(0.12f, 0.035f, 0.004f), C(0.85f, 0.1f, 0.1f), fq);
            b.AddBox(fa + fq * new Vector3(0, 0, -0.037f), new Vector3(0.035f, 0.12f, 0.004f), C(0.85f, 0.1f, 0.1f), fq);
            var mesh = new GameObject("Detail") { layer = InteriorLayer };
            mesh.transform.SetParent(room.transform, false);
            mesh.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Cabin detail");
            var mr = mesh.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cabinMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            if (glow.v.Count > 0)
            {
                var g = new GameObject("Light ring") { layer = InteriorLayer };
                g.transform.SetParent(cabinLights != null ? cabinLights.transform : room.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = glow.ToMesh("Light ring");
                var gr = g.AddComponent<MeshRenderer>();
                gr.sharedMaterial = cabinGlow;
                gr.shadowCastingMode = ShadowCastingMode.Off;
            }
            // labels and the mission clock
            Label(o2 + Vector3.up * 1.05f + (roomRound ? faceIn(o2) * new Vector3(0, 0, -0.1f) : new Vector3(0, 0, 0.12f)), roomRound ? faceIn(o2) : Quaternion.Euler(0, 180, 0), "O2", 0.05f, C(0.2f, 0.65f, 0.3f));
            Label(fa + fq * new Vector3(0, 0.15f, -0.04f), fq, "FIRST AID", 0.022f, C(0.85f, 0.1f, 0.1f));
            Vector3 ck = roomRound ? onWall(180, 1.72f, 0.03f) : new Vector3(0, 1.72f, -roomL / 2 + 0.03f);
            var cq = roomRound ? faceIn(ck) : Quaternion.Euler(0, 180, 0);
            Piece("Clock", ck, cq, bb => { bb.Mat = MeshBuilder.MatMatte; bb.AddBox(Vector3.zero, new Vector3(0.42f, 0.12f, 0.02f), C(0.05f, 0.05f, 0.06f), Quaternion.identity); });
            var clock = Label(ck + cq * new Vector3(0, 0, -0.012f), cq, "", 0.06f, C(1f, 0.35f, 0.25f));
            screens.Add((clock, () => { if (!active.hasLaunched) return "T- --:--:--"; long s = (long)Math.Max(0, ut - active.launchUt); return $"T+ {s / 86400}:{(s / 3600) % 24:00}:{(s / 60) % 60:00}:{s % 60:00}"; }));
        }

        // habitats: a bunk to nap in, an exercise bike, chess on the table
        void BuildHabExtras(float h)
        {
            float hw = roomW / 2, hl = roomL / 2;
            Piece("Bedding", Vector3.zero, Quaternion.identity, b =>
            {
                Color[] blankets = { C(0.85f, 0.3f, 0.25f), C(0.3f, 0.55f, 0.85f), C(0.95f, 0.75f, 0.25f), C(0.35f, 0.7f, 0.4f) };
                int n = 0;
                foreach (int s in new[] { -1, 1 })
                {
                    float x = s * (hw - 0.4f), z = -hl + 1.3f;
                    b.Mat = MeshBuilder.MatPaint;
                    foreach (float dz in new[] { -0.74f, 0.74f })
                        foreach (float dx in new[] { -0.36f, 0.36f })
                            b.AddBox(new Vector3(x + dx, 0.62f, z + dz), new Vector3(0.04f, 1.24f, 0.04f), C(0.6f, 0.62f, 0.65f), Quaternion.identity);   // posts
                    for (int k = 0; k < 2; k++)
                    {
                        float y = 0.36f + k * 0.8f;
                        b.Mat = MatFabric;
                        b.AddBox(new Vector3(x, y + 0.03f, z), new Vector3(0.7f, 0.07f, 1.44f), C(0.92f, 0.92f, 0.9f), Quaternion.identity);                 // mattress
                        b.AddBox(new Vector3(x, y + 0.1f, z - 0.55f), new Vector3(0.42f, 0.08f, 0.24f), C(0.97f, 0.97f, 0.97f), Quaternion.Euler(0, 4 * s, 0)); // pillow
                        b.AddBox(new Vector3(x, y + 0.08f, z + 0.2f), new Vector3(0.72f, 0.04f, 0.95f), blankets[n++ % 4], Quaternion.identity);             // blanket
                        b.AddBox(new Vector3(x, y + 0.11f, z - 0.25f), new Vector3(0.72f, 0.03f, 0.14f), C(0.97f, 0.97f, 0.95f), Quaternion.identity);         // its folded top
                    }
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(new Vector3(x - s * 0.37f, 0.85f, z), new Vector3(0.02f, 0.3f, 1.2f), C(0.25f, 0.4f, 0.6f), Quaternion.identity);   // a rail on the top bunk
                }
            });
            AddProp("bunk", "Bunk", new Vector3(-hw + 0.4f, 0.4f, -hl + 1.3f), () => "E: have a nap", () => { Show("Zzz... You wake up feeling great.", 3f); PuffAt(new Vector3(-hw + 0.4f, 0.6f, -hl + 1.3f), true); }, 0.4f);
            // the bike: pedals that turn while you ride
            Vector3 bk = new Vector3(hw - 0.55f, 0, -0.4f);
            Piece("Bike", bk, Quaternion.Euler(0, 90, 0), b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0, 0.05f, 0), new Vector3(0.3f, 0.04f, 0.7f), C(0.2f, 0.2f, 0.22f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.35f, -0.2f), new Vector3(0.05f, 0.6f, 0.05f), C(0.85f, 0.2f, 0.15f), Quaternion.Euler(-15, 0, 0));
                b.AddBox(new Vector3(0, 0.66f, -0.27f), new Vector3(0.2f, 0.05f, 0.25f), C(0.1f, 0.1f, 0.1f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.55f, 0.25f), new Vector3(0.05f, 0.5f, 0.05f), C(0.85f, 0.2f, 0.15f), Quaternion.Euler(15, 0, 0));
                b.AddBox(new Vector3(0, 0.8f, 0.3f), new Vector3(0.4f, 0.03f, 0.03f), C(0.2f, 0.2f, 0.22f), Quaternion.identity);
                b.AddFrustum(0.05f, -0.05f, 0.18f, 0.18f, C(0.3f, 0.3f, 0.32f), 18, true, true);
            });
            var crank = Piece("Pedals", bk + new Vector3(0, 0.25f, 0), Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(new Vector3(0, 0, 0), new Vector3(0.02f, 0.24f, 0.02f), C(0.7f, 0.7f, 0.72f), Quaternion.identity);
                b.AddBox(new Vector3(0.06f, 0.12f, 0), new Vector3(0.1f, 0.02f, 0.05f), C(0.15f, 0.15f, 0.15f), Quaternion.identity);
                b.AddBox(new Vector3(-0.06f, -0.12f, 0), new Vector3(0.1f, 0.02f, 0.05f), C(0.15f, 0.15f, 0.15f), Quaternion.identity);
            });
            var bp = AddProp("bike", "Exercise bike", bk + new Vector3(0, 0.5f, 0), () => "E: ride the exercise bike (keeps an Ardling's legs strong)", null, 0.35f, true);
            bp.moving = crank.transform;
            bp.use = () => { bp.state = Time.unscaledTime + 5f; SendCabin(bp, 5f); Show("Pedal, pedal, pedal...", 2f); };
            bp.animate = (pp, dt) => { if (Time.unscaledTime < pp.state) pp.shown += dt * 360f; pp.moving.localRotation = Quaternion.Euler(0, 90, 0) * Quaternion.Euler(pp.shown, 0, 0); };
            // chess on the table
            Vector3 tb = new Vector3(0, 0.58f, -0.4f);
            Piece("Board", tb, Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                for (int x = 0; x < 8; x++) for (int z = 0; z < 8; z++)
                    b.AddBox(new Vector3(-0.21f + x * 0.06f, 0, -0.21f + z * 0.06f), new Vector3(0.06f, 0.006f, 0.06f), (x + z) % 2 == 0 ? C(0.92f, 0.9f, 0.85f) : C(0.25f, 0.2f, 0.18f), Quaternion.identity);
            });
            var pieces = new List<Transform>();
            for (int k = 0; k < 8; k++)
            {
                bool white = k < 4;
                var pc = Piece("Chess piece", tb + new Vector3(-0.21f + (k % 4) * 0.12f + (white ? 0 : 0.06f), 0.003f, white ? -0.21f : 0.21f), Quaternion.identity, b =>
                {
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddFrustum(0.05f, 0f, 0.012f, 0.02f, white ? C(0.95f, 0.93f, 0.88f) : C(0.12f, 0.1f, 0.1f), 10, true, true);
                    b.AddSphere(new Vector3(0, 0.058f, 0), 0.014f, white ? C(0.95f, 0.93f, 0.88f) : C(0.12f, 0.1f, 0.1f), 8, 5);
                });
                pieces.Add(pc.transform);
            }
            var cp = AddProp("chess", "Chess", tb + Vector3.up * 0.05f, () => $"E: make a chess move  (move {chessMoves + 1})", null, 0.22f, true);
            cp.reach = 1.3f;
            cp.use = () => { cp.state = chessMoves + 1; ApplyCabinState(cp); SendCabin(cp); };
            cp.animate = (pp, dt) =>
            {
                for (int k = 0; k < pieces.Count; k++)
                {
                    // each move nudges one piece to a new square (the same way on every screen)
                    var rng = new System.Random(k * 31 + chessMoves * 7);
                    int moved = chessMoves == 0 ? -1 : (chessMoves * 3 + 1) % pieces.Count;
                    if (k != moved) continue;
                    var target = tb + new Vector3(-0.21f + rng.Next(8) * 0.06f, 0.003f, -0.21f + rng.Next(8) * 0.06f);
                    pieces[k].localPosition = Vector3.Lerp(pieces[k].localPosition, target, dt * 4f);
                }
            };
        }

        // ---------------------------------------------------------------- using things

        /// <summary>What the dot in the middle is on (the nearest thing along your look, within reach).</summary>
        Prop LookTarget()
        {
            Vector3 eye = EyeAt, dir = Quaternion.Euler(-ipitch, iyaw, 0) * Vector3.forward;
            Prop best = null;
            float bestT = float.MaxValue;
            foreach (var p in props)
            {
                Vector3 to = p.at - eye;
                float t = Vector3.Dot(to, dir);
                if (t <= 0.05f || t > p.reach) continue;
                float off = (to - dir * t).magnitude;
                if (off > p.size) continue;
                if (t < bestT) { bestT = t; best = p; }
            }
            return best;
        }

        /// <summary>E, a click or the wheel on what you're looking at. True if it used up the key.</summary>
        bool UseCabin()
        {
            lookedAt = LookTarget();
            if (lookedAt == null) return false;
            if (lookedAt.scroll != null && Mathf.Abs(In.Scroll) > 0.01f) lookedAt.scroll(In.Scroll);
            // (a click only while the mouse is looking round: with ALT held it's for the panels)
            if (In.Down(K.E) || In.LeftDown && !In.Held(K.LeftAlt) && !In.Typing && !packOpen) { lookedAt.use?.Invoke(); KitSound(lookedAt.sound ?? "ui_click"); return true; }
            return false;
        }

        /// <summary>Every frame inside: the moving parts, the lamps, the screens, the lights.</summary>
        void UpdateCabinProps(float dt)
        {
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                if (p.animate != null && (p.moving != null || p.id == "chess")) p.animate(p, dt);
                if (!inside) return;   // (the hatch opened: out we go)
            }
            foreach (var (go, on) in lamps) { bool o = false; try { o = on() || LampTest; } catch { } if (go.activeSelf != o) go.SetActive(o); }
            screenT -= dt;
            if (screenT <= 0)
            {
                screenT = 0.25f;
                foreach (var (tm, text) in screens) { try { tm.text = text(); } catch { } }
            }
            // the low-fuel lamp blinks
            foreach (var (go, on) in lamps) if (go.activeSelf && go.name == "Lamp") go.transform.localScale = Vector3.one * (0.85f + 0.15f * Mathf.Sin(Time.time * 6f));
            UpdateCabinKit(dt);   // milestone 36
            ConsumeCabinEvents();
        }

        void ApplyCabinState(Prop p)
        {
            switch (p.id)
            {
                case "cabinlights":
                    cabinLightMode = Mathf.RoundToInt(p.state) % 3;
                    lastAmbient = -1;
                    ApplyCabinAmbient();
                    if (cabinMat != null) cabinMat.SetColor("_Tint", cabinLightMode == 2 ? C(1f, 0.6f, 0.55f) : Color.white);
                    if (cabinLights != null) cabinLights.SetActive(cabinLightMode != 1);
                    break;
                case "plant": plantGrow = Mathf.Clamp01(p.state); break;
                case "chess": chessMoves = Mathf.RoundToInt(p.state); break;
            }
        }

        void PuffAt(Vector3 roomLocal, bool zzz = false)
        {
            if (room == null) return;
            var go = new GameObject("Puff") { layer = InteriorLayer };
            go.transform.SetParent(room.transform, false);
            go.transform.localPosition = roomLocal;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.duration = 1f; main.startLifetime = zzz ? 2f : 1.2f; main.startSpeed = zzz ? 0.2f : 1.4f;
            main.startSize = zzz ? 0.08f : 0.15f; main.startColor = zzz ? new Color(0.7f, 0.85f, 1f, 0.9f) : new Color(0.95f, 0.95f, 0.95f, 0.7f);
            main.gravityModifier = zzz ? -0.05f : 0.05f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = zzz ? 10 : 25; shape.radius = 0.02f;
            go.transform.localRotation = zzz ? Quaternion.Euler(-90, 0, 0) : Quaternion.LookRotation(new Vector3(-roomLocal.x, 0, -roomLocal.z).normalized + Vector3.up * 0.2f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (puffMat == null) puffMat = new Material(Resources.Load<Shader>("Shaders/RocketSmoke")) { name = "Cabin puff" };
            r.sharedMaterial = puffMat;
            r.gameObject.layer = InteriorLayer;
            ps.Emit(zzz ? 6 : 60);
            UnityEngine.Object.Destroy(go, 3f);
        }

        // ---------------------------------------------------------------- sharing with the room

        void SendCabin(Prop p, float value = float.NaN)
        {
            if (!p.shared || !Coop.Active || insidePart == null) return;
            float v = float.IsNaN(value) ? p.state : value;
            Coop.Current.cabinState[insidePart.index + "|" + p.id] = v;   // (for when you come back in)
            Coop.Current.room.Send(new NetMsg { t = "cs", a = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2:0.###}", insidePart.index, p.id, v) });
        }

        void ConsumeCabinEvents()
        {
            if (!Coop.Active || insidePart == null) return;
            var q = Coop.Current.cabinEvents;
            while (q.Count > 0)
            {
                var (who, a) = q.Dequeue();
                if (who == Coop.Current.room.MyId) continue;
                var f = a.Split('|');
                if (f.Length != 3 || !int.TryParse(f[0], out int part) || part != insidePart.index) continue;
                if (!float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) continue;
                var p = props.FirstOrDefault(x => x.id == f[1]);
                if (p == null) continue;
                if (p.id == "plushie") p.vel = v;
                else if (p.id == "bike") p.state = Time.unscaledTime + v;
                else { p.state = v; ApplyCabinState(p); }
            }
        }

        /// <summary>The other players in the cabin walk (arms and legs swinging as they move) with their names over their heads.</summary>
        void AnimateFolk(string key, GameObject go, string name)
        {
            var body = go.transform.Find("Body");
            if (body == null) return;
            folkWalk.TryGetValue(key, out var st);
            float moved = (go.transform.localPosition - st.last).magnitude;
            st.phase += moved * 9f;
            st.last = go.transform.localPosition;
            folkWalk[key] = st;
            float swing = Mathf.Sin(st.phase) * Mathf.Clamp01(moved * 40f) * 32f;
            int k = 0;
            foreach (Transform limb in body)
            {
                if (limb.name != "Limb") continue;
                int side = k % 2 == 0 ? -1 : 1;
                limb.localRotation = Quaternion.Euler(k < 2 ? swing * side : -swing * side, 0, k < 2 ? side * 6f : 0);
                k++;
            }
            if (name == null) return;
            var tag = go.transform.Find("Name tag");
            if (tag == null)
            {
                var tm = Label(new Vector3(0, Ardlings.Height + 0.25f, 0), Quaternion.identity, name, 0.07f, Color.white, TextAnchor.MiddleCenter, go.transform);
                tm.gameObject.name = "Name tag";
                tag = tm.transform;
            }
            if (icam != null) tag.rotation = Quaternion.LookRotation(tag.position - icam.transform.position);
        }

        // ---------------------------------------------------------------- for tours

        Prop FindProp(string id) => props.FirstOrDefault(x => x.id == id) ?? props.FirstOrDefault(x => x.id.StartsWith(id));

        /// <summary>For tours: walk up to one of the cabin's things and look at it. Returns what the dot's on.</summary>
        public string TestLookAt(string id, float back = 0.9f)
        {
            var p = FindProp(id);
            if (p == null) return "none";
            Vector3 d = p.at - EyeAt, flat = new Vector3(d.x, 0, d.z);
            if (flat.magnitude > back) ipos += flat.normalized * (flat.magnitude - back);
            d = p.at - EyeAt;
            iyaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            ipitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            lookedAt = LookTarget();
            return lookedAt?.id ?? "nothing";
        }

        /// <summary>For tours: use one of the cabin's things (as if you'd pressed E on it); "blind*" uses every blind.</summary>
        public bool TestUse(string id)
        {
            if (id.EndsWith("*"))
            {
                var all = props.Where(x => x.id.StartsWith(id.TrimEnd('*'))).ToList();
                foreach (var x in all) x.use?.Invoke();
                return all.Count > 0;
            }
            var p = FindProp(id);
            if (p?.use == null) return false;
            p.use();
            return true;
        }

        /// <summary>For tours: what's in the cabin and how it's set.</summary>
        public string CabinInfo()
        {
            string s0 = screens.Count > 0 && screens[0].tm != null ? screens[0].tm.text.Replace('\n', ' ') : "";
            return $"props {props.Count} ({string.Join(",", props.Select(p => p.id))}), screens {screens.Count}, lamps lit {lamps.Count(l => l.go.activeSelf)}/{lamps.Count}, " +
                   $"lights {cabinLightMode}, plant {plantGrow:0.00}, chess {chessMoves}, blinds shut {props.Count(p => p.id.StartsWith("blind") && p.state > 0.5f)}, " +
                   $"locker {(FindProp("locker")?.state > 0.5f ? "open" : "shut")}, screen 1: \"{s0}\"; " +
                   $"gauges {gauges.Count}, floating {floaters.Count(f => f.t != null && f.t.localPosition.y > f.restH + 0.05f)}/{floaters.Count}, " +
                   $"vibration {vibration:0.00}, alarm {(alarmFlashing ? "FLASHING" : "quiet")}, keypad \"{keypadShow}\", fans {(BreakerOff("fans") ? "off" : "on")}, " +
                   $"flood {KitValue("knob_flood", 0.3f):0.00}, ambient {lastAmbient:0.00}";
        }
    }
}
