using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rocket
{
    /// <summary>
    /// Milestone 34 (captaincarl24: "theres a interior in those"): walk-in cabins for the crewed parts. TAB goes inside
    /// the capsule, cockpit or habitat you're riding in: walk round in first person (W/S/A/D, the mouse looks, CTRL runs)
    /// and look out of the windows at the real world going by. At the seats, E takes the controls (back to flying); at the
    /// hatch, E climbs out on EVA. The crew sit in their seats; in a room the other players walk round with you, each as
    /// the Ardling they made.
    ///
    /// How it works: the cabin is a room of its own (on its own layer, drawn by a camera over the world's cameras) and
    /// bigger than the part it's in. Its windows are holes: through them you see the world, drawn from inside the part
    /// looking the same way you are, so the view out is the real one.
    /// </summary>
    public partial class FlightMode
    {
        public const int InteriorLayer = 12;
        /// <summary>Inside a cabin (TAB).</summary>
        public bool inside;
        PartState insidePart;
        int insideIdx = -1;   // milestone 41: which part, by number (a room's copy of the ship is renewed every half second)
        GameObject room;
        Camera icam;
        Vector3 ipos;
        float iyaw, ipitch, ipSend;
        bool roomRound;
        float roomR, roomW, roomL;
        readonly List<Vector3> seats = new List<Vector3>();
        Vector3 hatchAt, panelAt;
        readonly Dictionary<string, GameObject> cabinFolk = new Dictionary<string, GameObject>();   // crew sitting, players walking
        static Material cabinMat, cabinGlow;
        const float Eye = 0.95f;   // an Ardling's eyes (they're 1.2 m tall)

        /// <summary>The crewed part this player rides in (their seat), or the first with crew.</summary>
        PartState CabinPart()
        {
            if (active == null || active.IsEva || active.destroyed) return null;
            string me = Coop.Active ? Coop.Current.room.Me?.name : null;
            var mine = me != null ? active.parts.FirstOrDefault(p => p.crew.Contains(me)) : null;
            return mine ?? active.parts.FirstOrDefault(p => p.def.crew > 0 && (p.def.kind == PartKind.Command || p.def.kind == PartKind.Habitat) && p.crew.Count > 0)
                ?? active.parts.FirstOrDefault(p => p.def.crew > 0 && p.crew.Count > 0);
        }

        public bool CanGoInside => !inside && !mapView && !photoMode && replay == null && CabinPart() != null;

        public void EnterInterior()
        {
            if (inside || mapView || photoMode || replay != null || rollout != null) return;
            var p = CabinPart();
            if (p == null)
            {
                Show(active == null || active.destroyed ? "There's nothing left to go inside." : active.IsEva ? "You're outside: walk up to the hatch and board (B) first."
                    : active.parts.Any(q => q.def.crew > 0) ? "Nobody's aboard: there's no one to go inside as." : "This ship has no cabin to go into: it needs a capsule, cockpit or habitat.", 3f);
                return;
            }
            insidePart = p;
            insideIdx = p.index;
            BuildRoom(p);
            inside = true;
            firstPerson = false;
            ipos = new Vector3(0, 0, roomRound ? -0.6f : -roomL * 0.25f);
            iyaw = 0; ipitch = 0;
            Show($"Inside the {p.def.name}. Look at anything and press E to use it: the switches, STAGE, the throttle, the radio, the blinds, the locker... Sit in a seat to fly; the hatch goes out. {In.Name(K.Tab)}: leave.", 6f);
        }

        /// <summary>For tours: which way you look in the cabin.</summary>
        public void TestCabinLook(float yaw, float pitch) { iyaw = yaw; ipitch = pitch; ipos = Vector3.zero; }

        public void LeaveInterior()
        {
            if (!inside) return;
            inside = false;
            SendPose(false);
            insidePart = null;
            insideIdx = -1;
            if (icam != null) icam.enabled = false;
            if (room != null) UnityEngine.Object.Destroy(room);
            room = null;
            cabinFolk.Clear();
            props.Clear(); screens.Clear(); lamps.Clear();
            ClearCabinKit();
            ResetCabinLight();
            SetWorldAo(true);
        }

        // ---------------------------------------------------------------- every frame

        /// <summary>Walking round the cabin, and E at a seat or the hatch. Replaces the ship's keys while inside.</summary>
        void HandleInteriorKeys(VesselSim v)
        {
            v.inputPitch = v.inputYaw = v.inputRoll = v.driveInput = 0;
            v.inputTranslate = Vector3.zero;
            ResolveInsidePart();
            if (insidePart == null || v.destroyed || v.IsEva || insidePart.crew.Count == 0 && !Coop.Active) { LeaveInterior(); return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            UpdateCabinProps(dt);   // milestone 35: the moving parts and the live screens
            if (!inside) return;
            if (mapView) return;    // (the map screen: M comes back)
            if (In.Down(K.Tab)) { LeaveInterior(); return; }
            if (In.Down(K.I)) TogglePack();   // milestone 38: eat, heal, refuel from the backpack
            UsePackKeys(MyArdlingName);
            if (!In.Held(K.LeftAlt) && !packOpen && Application.isFocused)
            {
                var d = In.MouseDelta;
                iyaw += d.x * 0.12f;
                ipitch = Mathf.Clamp(ipitch + d.y * 0.12f, -80f, 80f);
            }
            var fwd = Quaternion.Euler(0, iyaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, iyaw, 0) * Vector3.right;
            Vector3 move = fwd * ((In.Held(K.W) ? 1 : 0) - (In.Held(K.S) ? 1 : 0)) + right * ((In.Held(K.D) ? 1 : 0) - (In.Held(K.A) ? 1 : 0));
            if (move.sqrMagnitude > 1) move.Normalize();
            ipos += move * (In.Held(K.LeftCtrl) ? 2.6f : 1.5f) * dt;
            // the walls
            if (roomRound)
            {
                var flat = new Vector2(ipos.x, ipos.z);
                float lim = roomR - 0.4f;
                if (flat.magnitude > lim) { flat = flat.normalized * lim; ipos.x = flat.x; ipos.z = flat.y; }
            }
            else
            {
                ipos.x = Mathf.Clamp(ipos.x, -roomW / 2 + 0.4f, roomW / 2 - 0.4f);
                ipos.z = Mathf.Clamp(ipos.z, -roomL / 2 + 0.4f, roomL / 2 - 0.4f);
            }
            // milestone 35: look at something and press E (or click, or scroll the throttle)
            if (UseCabin() && !inside) return;
            ipSend -= dt;
            if (ipSend <= 0) { ipSend = 0.12f; SendPose(true); }
        }

        /// <summary>
        /// Milestone 41 (captaincarl24: "inside tab also doesnt work super buggy"): in a room, everyone but the host gets a new
        /// copy of the ship from the host every half second, with new parts, so the cabin's part is looked up again by its
        /// number. (It used to throw everyone but the host straight back out.)
        /// </summary>
        void ResolveInsidePart()
        {
            if (active == null) { insidePart = null; return; }
            if (insidePart != null && active.parts.Contains(insidePart)) return;
            insidePart = active.parts.FirstOrDefault(p => p.index == insideIdx);
        }

        // near something and facing it
        bool Facing(Vector3 at, float reach)
        {
            var d = new Vector2(at.x - ipos.x, at.z - ipos.z);
            if (d.magnitude > reach) return false;
            if (d.magnitude < 0.25f) return true;
            var f = Quaternion.Euler(0, iyaw, 0) * Vector3.forward;
            return Vector2.Dot(d.normalized, new Vector2(f.x, f.z)) > 0.5f;
        }
        bool NearSeat() => seats.Any(s => Facing(s, 1.1f)) || Facing(panelAt, 1.4f);
        bool NearHatch() => Facing(hatchAt, 1.2f);

        /// <summary>Both cameras: the cabin's from where you stand, the world's from inside the part looking the same way.</summary>
        void ApplyInteriorCamera(Camera world)
        {
            if (inside) ResolveInsidePart();
            if (inside && (insidePart == null || active == null || active.destroyed)) LeaveInterior();   // (crashed, or the cabin's gone)
            if (!inside || room == null || insidePart == null || mapView || photoMode)
            {
                if (icam != null && icam.enabled) icam.enabled = false;
                SetWorldAo(true);
                return;
            }
            SetWorldAo(false);   // milestone 35
            if (icam == null) BuildInteriorCamera(world);
            icam.enabled = true;
            var look = Quaternion.Euler(-ipitch, iyaw, 0);
            icam.transform.position = room.transform.position + ipos + Vector3.up * Eye;
            icam.transform.rotation = look;
            icam.fieldOfView = world.fieldOfView;
            Vector3 at = renderRot * (insidePart.Center - active.com);
            world.transform.position = at + renderRot * Vector3.up * 0.3f;
            world.transform.rotation = renderRot * look;
            UpdateCabinFolk();
        }

        void BuildInteriorCamera(Camera world)
        {
            var g = new GameObject("Cabin camera");
            icam = g.AddComponent<Camera>();
            icam.cullingMask = 1 << InteriorLayer;
            icam.nearClipPlane = 0.05f;
            icam.farClipPlane = 60f;
            icam.clearFlags = CameraClearFlags.Depth;
            var data = icam.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            data.renderShadows = false;
            data.renderPostProcessing = true;
            var baseData = world.GetUniversalAdditionalCameraData();
            if (!baseData.cameraStack.Contains(icam)) baseData.cameraStack.Add(icam);
        }

        /// <summary>The prompts while inside.</summary>
        void DrawInteriorHud(float W, float H)
        {
            if (!inside || mapView) return;
            // milestone 35: what the dot's on, and what E does to it
            string hint = null;
            if (lookedAt != null) { try { hint = lookedAt.prompt?.Invoke(); } catch { } }
            var st = new GUIStyle(Ui.BigCenter) { fontSize = 20 };
            if (lookedAt != null) Ui.Shadowed(new Rect(W / 2 - 300, H / 2 + 18, 600, 26), lookedAt.name.ToUpperInvariant(), new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            if (hint != null) Ui.Shadowed(new Rect(W / 2 - 400, H / 2 + 42, 800, 30), hint, st);
            Ui.Shadowed(new Rect(W / 2 - 500, H - 40, 1000, 22), $"Inside the {insidePart?.def.name}   ·   look at things: E or click uses them   ·   scroll on the throttle   ·   W/S/A/D walk   ·   {In.Name(K.Tab)}: leave the cabin", new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter });
            // the dot in the middle, for looking at things (bigger and yellow when it's on something)
            if (lookedAt != null) Ui.Line(new Rect(W / 2 - 4, H / 2 - 4, 8, 8), new Color(1f, 0.85f, 0.3f, 0.95f));
            else Ui.Line(new Rect(W / 2 - 2, H / 2 - 2, 4, 4), new Color(1, 1, 1, 0.7f));
            // what's been said (the flight's usual messages don't show in here)
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            {
                var c = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(messageUntil - Time.unscaledTime));
                float mh = Mathf.Max(60, Ui.BigCenter.CalcHeight(new GUIContent(Words.Say(message)), 800));
                Ui.Shadowed(new Rect(W / 2 - 400, 70, 800, mh), Words.Say(message), Ui.BigCenter);
                GUI.color = c;
            }
        }

        // ---------------------------------------------------------------- the others in the cabin

        void SendPose(bool on)
        {
            if (!Coop.Active || insidePart == null && on) return;
            var c = Coop.Current;
            string a = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}|{1:0.##}|{2:0.##}|{3:0}", insidePart?.index ?? -1, ipos.x, ipos.z, iyaw);
            c.room.Send(new NetMsg { t = "ip", a = a, on = on });
        }

        /// <summary>Crew in their seats; other players walking where they are (from their poses), each in their own look.</summary>
        void UpdateCabinFolk()
        {
            var want = new HashSet<string>();
            string me = Coop.Active ? Coop.Current.room.Me?.name : insidePart.crew.FirstOrDefault();
            // players walking in this cabin
            if (Coop.Active)
            {
                var c = Coop.Current;
                foreach (var kv in c.poses)
                {
                    var (a, on, t) = kv.Value;
                    if (!on || Time.unscaledTime - t > 2f || kv.Key == c.room.MyId) continue;
                    var f = a.Split('|');
                    if (f.Length != 4 || !int.TryParse(f[0], out int part) || part != insidePart.index) continue;
                    var pl = c.room.State.Player(kv.Key);
                    if (pl == null) continue;
                    float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
                    float.TryParse(f[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
                    float.TryParse(f[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float yaw);
                    string key = "walk:" + pl.name;
                    want.Add(key);
                    var go = Folk(key, MyArdling.LookOf(pl.look ?? pl.name));
                    go.transform.localPosition = Vector3.Lerp(go.transform.localPosition, new Vector3(x, 0, z), 0.3f);
                    go.transform.localRotation = Quaternion.Slerp(go.transform.localRotation, Quaternion.Euler(0, yaw, 0), 0.3f);
                    AnimateFolk(key, go, pl.name);   // milestone 35
                }
            }
            // the crew in their seats (anyone not walking round, and not you)
            for (int i = 0; i < insidePart.crew.Count && i < seats.Count; i++)
            {
                string who = insidePart.crew[i];
                if (who == me || want.Contains("walk:" + who)) continue;
                string key = "seat:" + who;
                want.Add(key);
                var go = Folk(key, Ardlings.LookFor(who));
                go.transform.localPosition = seats[i] + new Vector3(0, 0.12f, 0.1f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = new Vector3(1, 0.8f, 1);   // sitting down
            }
            foreach (var k in cabinFolk.Keys.ToList()) if (!want.Contains(k)) { UnityEngine.Object.Destroy(cabinFolk[k]); cabinFolk.Remove(k); }
        }

        GameObject Folk(string key, Ardlings.Look look)
        {
            if (cabinFolk.TryGetValue(key, out var go) && go != null) return go;
            go = MyArdling.Build(look, InteriorLayer);
            go.transform.SetParent(room.transform, false);
            IndoorLit(go);   // milestone 37
            cabinFolk[key] = go;
            return go;
        }

        // ---------------------------------------------------------------- the rooms

        void BuildRoom(PartState p)
        {
            if (room != null) UnityEngine.Object.Destroy(room);
            cabinFolk.Clear();
            seats.Clear();
            if (cabinMat == null)
            {
                cabinMat = new Material(Mats.Lit) { name = "Cabin" };
                cabinMat.SetFloat("_Ambient", 0.2f);      // lit from inside, whatever the sun's doing (milestone 35: lower, with the ambient occlusion off in here)
                cabinGlow = Mats.Additive;
            }
            var b = new MeshBuilder();
            var glow = new MeshBuilder();
            bool hab = p.def.kind == PartKind.Habitat || p.def.crew >= 3;
            if (hab) BuildHab(b, glow, p.def.crew); else BuildCapsule(b, glow, p.def.crew);
            room = new GameObject("Cabin " + p.def.name) { layer = InteriorLayer };
            room.transform.position = new Vector3(0, -30000f, 0);   // far from anything (and on its own layer)
            // milestone 37: lit by its own lamps (not the sun through the walls), corners darkening like a real room
            cabinMat.SetFloat("_SunScale", 0f);
            cabinMat.SetFloat("_CabinAO", 1f);
            cabinMat.SetVector("_RoomOrigin", room.transform.position);
            cabinMat.SetVector("_RoomSize", hab ? new Vector4(roomW / 2, 2.1f, 1, roomL / 2) : new Vector4(roomR * Mathf.Cos(Mathf.PI / 12f), 2.1f, 0, 0));
            var mesh = new GameObject("Walls") { layer = InteriorLayer };
            mesh.transform.SetParent(room.transform, false);
            mesh.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Cabin");
            var mr = mesh.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cabinMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            if (glow.v.Count > 0)
            {
                var g = new GameObject("Lights") { layer = InteriorLayer };
                g.transform.SetParent(room.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = glow.ToMesh("Cabin lights");
                var gr = g.AddComponent<MeshRenderer>();
                gr.sharedMaterial = cabinGlow;
                gr.shadowCastingMode = ShadowCastingMode.Off;
            }
            BuildCabinProps(hab, p.def.crew, 2.1f);   // milestone 35: the controls, the screens, the things to play with
        }

        static readonly Color Wall = new Color(0.78f, 0.79f, 0.77f), Floor = new Color(0.38f, 0.39f, 0.42f), Trim = new Color(0.2f, 0.22f, 0.25f),
            Seat = new Color(0.25f, 0.35f, 0.5f), ScreenGlow = new Color(0.25f, 0.7f, 1f);

        /// <summary>A round capsule cabin: windows ahead and to each side, seats facing the controls, the hatch behind.</summary>
        void BuildCapsule(MeshBuilder b, MeshBuilder glow, int crew)
        {
            roomRound = true;
            roomR = crew >= 2 ? 2.5f : 2.1f;
            float h = 2.1f;
            const int sides = 12;
            b.Mat = MatPanel;   // milestone 37: painted panels (the shader adds seams, orange peel, scratches)
            // floor and ceiling (round)
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 p0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * roomR, p1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * roomR;
                b.Mat = MatRubber;
                b.AddTri(Vector3.zero, p0, p1, Vector3.up, Floor);
                b.Mat = MatPanel;
                b.AddTri(Vector3.up * h, p1 + Vector3.up * h, p0 + Vector3.up * h, Vector3.down, Wall * 0.92f);
                bool window = i == 0 || i == 11 || i == 3 || i == 8;          // ahead (two panes), right, left
                bool hatch = i == 5 || i == 6;                                 // behind
                Vector3 n = -(p0 + p1).normalized;
                if (window) Pane(b, p0, p1, n, 0, 0.5f, 1.55f, h);
                else WallQuad(b, p0, p1, n, 0, h, hatch ? Trim * 1.4f : Wall);
                // a dark band at the floor, a trim line at the top
                WallQuad(b, p0 * 0.995f, p1 * 0.995f, n, 0, 0.18f, Trim);
            }
            // the controls under the front windows, with screens
            panelAt = new Vector3(0, 0, roomR - 0.75f);   // (the desk itself, with its screens and switches, is FlightCabin's)
            // seats facing ahead
            int seatCount = Mathf.Max(1, crew);
            for (int s = 0; s < seatCount; s++)
            {
                float x = (s - (seatCount - 1) / 2f) * 0.9f;
                var at = new Vector3(x, 0, roomR - 1.35f);
                seats.Add(at);
                b.Mat = MatFabric;
                b.AddBox(at + new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.1f, 0.5f), Seat, Quaternion.identity);
                b.AddBox(at + new Vector3(0, 0.55f, -0.25f), new Vector3(0.5f, 0.5f, 0.08f), Seat, Quaternion.Euler(-12, 0, 0));
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(at + new Vector3(0, 0.13f, 0), new Vector3(0.1f, 0.26f, 0.1f), Trim * 1.6f, Quaternion.identity);
            }
            // the hatch: a round door behind, with a wheel
            hatchAt = new Vector3(0, 0, -roomR + 0.5f);
            b.Mat = MeshBuilder.MatPaint;
            int s0 = b.v.Count;
            b.AddFrustum(0.06f, 0f, 0.55f, 0.55f, new Color(0.6f, 0.62f, 0.65f), 20, true, false);
            XfLocal(b, s0, new Vector3(0, 0.8f, -roomR + 0.14f), Quaternion.Euler(90, 0, 0));   // (its wheel turns: FlightCabin)
            // a light in the ceiling
            glow.AddBox(new Vector3(0, h - 0.02f, 0), new Vector3(1.2f, 0.02f, 1.2f), new Color(1f, 0.95f, 0.85f) * 0.5f, Quaternion.identity);
        }

        /// <summary>A long habitat: windows down both sides, bunks, a table, plants, the hatch at the far end.</summary>
        void BuildHab(MeshBuilder b, MeshBuilder glow, int crew)
        {
            roomRound = false;
            roomW = 3.2f; roomL = 7f;
            float h = 2.1f, hw = roomW / 2, hl = roomL / 2;
            b.Mat = MatRubber;
            b.AddQuad(new Vector3(-hw, 0, -hl), new Vector3(-hw, 0, hl), new Vector3(hw, 0, hl), new Vector3(hw, 0, -hl), Vector3.up, Floor);
            b.Mat = MatPanel;
            b.AddQuad(new Vector3(-hw, h, -hl), new Vector3(hw, h, -hl), new Vector3(hw, h, hl), new Vector3(-hw, h, hl), Vector3.down, Wall * 0.92f);
            // side walls in four bays, each with a window
            float bay = roomL / 4;
            for (int k = 0; k < 4; k++)
            {
                float z0 = -hl + k * bay, z1 = z0 + bay;
                Pane(b, new Vector3(-hw, 0, z0), new Vector3(-hw, 0, z1), Vector3.right, 0, 0.55f, 1.5f, h);
                Pane(b, new Vector3(hw, 0, z1), new Vector3(hw, 0, z0), Vector3.left, 0, 0.55f, 1.5f, h);
            }
            // the ends: controls ahead (with a window), the hatch behind
            Pane(b, new Vector3(hw, 0, hl), new Vector3(-hw, 0, hl), Vector3.back, 0, 0.6f, 1.55f, h);
            WallQuad(b, new Vector3(-hw, 0, -hl), new Vector3(hw, 0, -hl), Vector3.forward, 0, h, Trim * 1.4f);
            panelAt = new Vector3(0, 0, hl - 0.7f);
            int seatCount = Mathf.Max(1, crew);
            for (int s = 0; s < seatCount; s++)
            {
                var at = new Vector3((s % 2 == 0 ? -1 : 1) * 0.7f, 0, hl - 1.6f - (s / 2) * 1.1f);
                seats.Add(at);
                b.Mat = MatFabric;
                b.AddBox(at + new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.1f, 0.5f), Seat, Quaternion.identity);
                b.AddBox(at + new Vector3(0, 0.55f, -0.25f), new Vector3(0.5f, 0.5f, 0.08f), Seat, Quaternion.identity);
            }
            // bunks, a table, plants
            b.Mat = MatFabric;
            for (int k = 0; k < 2; k++)
            {
                b.AddBox(new Vector3(-hw + 0.4f, 0.3f + k * 0.8f, -hl + 1.3f), new Vector3(0.75f, 0.12f, 1.5f), new Color(0.35f, 0.45f, 0.6f), Quaternion.identity);
                b.AddBox(new Vector3(hw - 0.4f, 0.3f + k * 0.8f, -hl + 1.3f), new Vector3(0.75f, 0.12f, 1.5f), new Color(0.6f, 0.4f, 0.3f), Quaternion.identity);
            }
            b.Mat = MatPanel;
            b.AddBox(new Vector3(0, 0.55f, -0.4f), new Vector3(1f, 0.05f, 1f), new Color(0.85f, 0.85f, 0.82f), Quaternion.identity);
            b.Mat = MeshBuilder.MatMatte;
            for (int k = 0; k < 4; k++) b.AddSphere(new Vector3(hw - 0.35f, 0.4f, 0.6f + k * 0.45f), 0.2f, new Color(0.25f, 0.6f, 0.25f), 10, 6);
            hatchAt = new Vector3(0, 0, -hl + 0.5f);
            b.Mat = MeshBuilder.MatPaint;
            int s0 = b.v.Count;
            b.AddFrustum(0.05f, 0f, 0.6f, 0.6f, new Color(0.6f, 0.62f, 0.65f), 20, true, false);   // the door (its wheel: FlightCabin)
            XfLocal(b, s0, new Vector3(0, 0.8f, -hl + 0.0f), Quaternion.Euler(90, 0, 0));
            for (int k = 0; k < 3; k++) glow.AddBox(new Vector3(0, h - 0.02f, -hl + 1.5f + k * 2.5f), new Vector3(0.9f, 0.02f, 0.9f), new Color(1f, 0.95f, 0.85f) * 0.45f, Quaternion.identity);
        }

        /// <summary>A plain wall from p0 to p1 (floor positions), between two heights, facing n.</summary>
        static void WallQuad(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 n, float y0, float y1, Color c) =>
            b.AddQuad(p0 + Vector3.up * y0, p0 + Vector3.up * y1, p1 + Vector3.up * y1, p1 + Vector3.up * y0, n, c);

        /// <summary>A wall with a window: solid below and above, posts at the sides, open in the middle (the world shows through).</summary>
        static void Pane(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 n, float y0, float sill, float head, float h)
        {
            WallQuad(b, p0, p1, n, y0, sill, Wall);
            WallQuad(b, p0, p1, n, head, h, Wall);
            Vector3 a = Vector3.Lerp(p0, p1, 0.12f), c = Vector3.Lerp(p0, p1, 0.88f);
            WallQuad(b, p0, a, n, sill, head, Wall);
            WallQuad(b, c, p1, n, sill, head, Wall);
            // a frame round the opening
            Vector3 mid = Vector3.Lerp(p0, p1, 0.5f);
            float wdt = (c - a).magnitude;
            var q = Quaternion.LookRotation(-n, Vector3.up);
            b.AddBox(mid + Vector3.up * sill + n * 0.03f, new Vector3(wdt, 0.06f, 0.08f), Trim, q);
            b.AddBox(mid + Vector3.up * head + n * 0.03f, new Vector3(wdt, 0.06f, 0.08f), Trim, q);
        }

        static void XfLocal(MeshBuilder b, int from, Vector3 pos, Quaternion rot)
        {
            for (int i = from; i < b.v.Count; i++) { b.v[i] = pos + rot * b.v[i]; b.n[i] = rot * b.n[i]; }
        }
    }
}
