using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rocket
{
    /// <summary>
    /// Milestone 34 (captaincarl24: "for multiplayer make it so each player can customize their ardling like the among us
    /// type menu"): your own Ardling. Pick a suit colour, the face drawn on your visor note, the note's colour and a hat,
    /// with your Ardling turning in a preview. It's kept on this computer, travels with you into every room, and the
    /// others see it in the team list and on your Ardling.
    /// </summary>
    public static class MyArdling
    {
        public const int PreviewLayer = 11;

        /// <summary>Your look as "suit,paper,face,hat" (or made from your name until you pick one).</summary>
        public static string Code
        {
            get
            {
                string c = null;
                try { c = PlayerPrefs.GetString("myArdling", ""); } catch { }
                if (Ardlings.Parse(c, out _, out _, out _, out _)) return c;
                var (s, p, f, h) = Ardlings.ChoicesFor(PlayerName);
                return Ardlings.Code(s, p, f, h);
            }
            set { try { PlayerPrefs.SetString("myArdling", value ?? ""); PlayerPrefs.Save(); } catch { } }
        }

        static string PlayerName { get { try { return PlayerPrefs.GetString("mdName", "Player"); } catch { return "Player"; } } }

        public static Ardlings.Look LookOf(string code) =>
            Ardlings.Parse(code, out int s, out int p, out int f, out int h) ? Ardlings.Make(s, p, f, h) : Ardlings.LookFor(code);

        /// <summary>Everyone in a room wears their own look (call when the room's player list changes).</summary>
        public static void Register(string name, string code)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (Ardlings.Parse(code, out int s, out int p, out int f, out int h)) Ardlings.Custom[name] = Ardlings.Make(s, p, f, h);
        }

        // ---------------------------------------------------------------- the editor

        /// <summary>The editor is open (drawn over whatever screen opened it).</summary>
        public static bool Open;
        public static event Action<string> Changed;   // a room passes the new look on
        static int suit, paper, face, hat;
        static bool loaded;
        static float spin;

        static void Load()
        {
            Ardlings.Parse(Code, out suit, out paper, out face, out hat);
            loaded = true;
        }

        /// <summary>Opens the editor, starting from the look you have.</summary>
        public static void Show() { Load(); Open = true; }

        /// <summary>The editor as a window in the middle of the screen. Done saves; Esc or Cancel closes without saving.</summary>
        public static void DrawWindow()
        {
            if (!Open) { HidePreview(); return; }
            if (!loaded) Load();
            float W = Ui.W, H = Ui.H;
            var dim = new Rect(0, 0, W, H);
            Ui.Dim(dim, 0.55f);
            var r = new Rect(W / 2 - 420, Mathf.Max(20, H / 2 - 290), 840, Mathf.Min(580, H - 40));
            Ui.ShieldAround(r, W, H);   // milestone 41: (a whole-screen shield here swallowed every click in the window)
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 18, r.y + 12, 300, 30), "My Ardling", Ui.Title);
            Ui.Text(new Rect(r.x + 170, r.y + 19, r.width - 200, 20), "How you look to everyone in a room. Kept on this computer.", Ui.Small);

            // the preview, turning slowly
            var pv = new Rect(r.x + 18, r.y + 54, 300, r.height - 120);
            Ui.Line(pv, new Color(0.03f, 0.05f, 0.08f, 1f));
            var look = Ardlings.Make(suit, paper, face, hat);
            var tex = Preview(look);
            if (tex != null) GUI.DrawTexture(pv, tex, ScaleMode.ScaleAndCrop);
            Ui.Text(new Rect(pv.x, pv.yMax - 26, pv.width, 22), $"<b>{PlayerName}</b>", new GUIStyle(Ui.Label) { alignment = TextAnchor.MiddleCenter });

            // the choices
            float x = r.x + 340, y = r.y + 56, w = r.xMax - 18 - x;
            Ui.Text(new Rect(x, y, 200, 22), "Suit colour", Ui.Header);
            y += 26;
            int perRow = 6;
            float sw = (w - (perRow - 1) * 6) / perRow;
            for (int i = 0; i < Ardlings.SuitCount; i++)
            {
                var sr = new Rect(x + (i % perRow) * (sw + 6), y + (i / perRow) * 40, sw, 34);
                if (Ui.Press(sr, GUIContent.none, i == suit ? Ui.ButtonSmallSel : Ui.ButtonSmall)) suit = i;
                Ui.Line(new Rect(sr.x + 5, sr.y + 5, sr.width - 10, sr.height - 10), Ardlings.SuitColor(i));
            }
            y += ((Ardlings.SuitCount + perRow - 1) / perRow) * 40 + 8;
            Ui.Text(new Rect(x, y, 200, 22), "Visor note", Ui.Header);
            y += 26;
            int fr = 5;
            float fw = (w - (fr - 1) * 6) / fr;
            for (int i = 0; i < Ardlings.Faces; i++)
            {
                var b = new Rect(x + (i % fr) * (fw + 6), y + (i / fr) * 58, fw, 52);
                if (Ui.Press(b, GUIContent.none, i == face ? Ui.ButtonSmallSel : Ui.ButtonSmall)) face = i;
                GUI.DrawTexture(new Rect(b.center.x - 20, b.y + 4, 40, 40), Ardlings.FaceTexture(i, Ardlings.PaperColor(paper)));
            }
            y += ((Ardlings.Faces + fr - 1) / fr) * 58 + 4;
            Ui.Text(new Rect(x, y + 4, 110, 22), "Note colour", Ui.Label);
            for (int i = 0; i < Ardlings.PaperCount; i++)
            {
                var b = new Rect(x + 110 + i * 46, y, 40, 30);
                if (Ui.Press(b, GUIContent.none, i == paper ? Ui.ButtonSmallSel : Ui.ButtonSmall)) paper = i;
                Ui.Line(new Rect(b.x + 5, b.y + 5, b.width - 10, b.height - 10), Ardlings.PaperColor(i));
            }
            y += 40;
            Ui.Text(new Rect(x, y, 200, 22), "Hat", Ui.Header);
            y += 26;
            int hr = 3;
            float hw = (w - (hr - 1) * 6) / hr;
            for (int i = 0; i < Ardlings.Hats; i++)
            {
                var b = new Rect(x + (i % hr) * (hw + 6), y + (i / hr) * 32, hw, 28);
                string n = Ardlings.HatNames[i];
                if (Ui.Press(b, char.ToUpper(n[0]) + n.Substring(1), i == hat ? Ui.ButtonSmallSel : Ui.ButtonSmall)) hat = i;
            }

            // buttons
            if (Ui.Press(new Rect(r.x + 18, r.yMax - 52, 140, 36), "Random", Ui.Button))
            {
                var rng = new System.Random();
                suit = rng.Next(Ardlings.SuitCount); paper = rng.Next(Ardlings.PaperCount); face = rng.Next(Ardlings.Faces); hat = rng.Next(Ardlings.Hats);
            }
            bool cancel = Ui.Press(new Rect(r.xMax - 280, r.yMax - 52, 120, 36), "Cancel", Ui.Button) || In.EscapeDown;
            if (Ui.Press(new Rect(r.xMax - 150, r.yMax - 52, 132, 36), "Done", Ui.ButtonGo))
            {
                Code = Ardlings.Code(suit, paper, face, hat);
                Register(PlayerName, Code);
                Changed?.Invoke(Code);
                Open = false;
                HidePreview();
                return;
            }
            if (cancel) { Open = false; loaded = false; HidePreview(); }
        }

        // ---------------------------------------------------------------- the turning preview

        static GameObject rig, model;
        static Camera cam;
        static RenderTexture rt;
        static string builtFor;

        static Texture Preview(Ardlings.Look look)
        {
            if (rig == null)
            {
                rig = new GameObject("Ardling preview") { hideFlags = HideFlags.DontSave };
                rig.transform.position = new Vector3(0, -20000f, 0);   // nowhere near anything (and on its own layer)
                rt = new RenderTexture(512, 768, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "ArdlingPreviewRT" };
                var cg = new GameObject("Preview camera");
                cg.transform.SetParent(rig.transform, false);
                cg.transform.localPosition = new Vector3(0, 0.7f, 3.2f);
                cg.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.02f, -1f));
                cam = cg.AddComponent<Camera>();
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 20f;
                cam.cullingMask = 1 << PreviewLayer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.09f, 0.13f, 1f);
                cam.targetTexture = rt;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Base;
                data.renderPostProcessing = false;   // (no exposure: the colours as they are)
                data.renderShadows = false;
                // milestone 41: its own studio lights (a warm key from the front, a cool rim behind) instead of the sun and a
                // doubled fill, which washed the colours out to pastel
                Lamp("Key light", new Vector3(1.3f, 1.9f, 2.4f), new Color(1f, 0.95f, 0.88f), 9f);
                Lamp("Rim light", new Vector3(-1.6f, 1.6f, -1.8f), new Color(0.7f, 0.82f, 1f), 5f);
                Lamp("Fill light", new Vector3(-1.8f, 0.4f, 2.2f), new Color(0.85f, 0.9f, 1f), 2.5f);
            }
            if (!rig.activeSelf) FlightMode.SetWorldAo(false);   // milestone 41: the world's ambient occlusion grained the preview
            rig.SetActive(true);
            string key = $"{look.suit}|{look.paper}|{look.face}|{look.hat}";
            if (model == null || builtFor != key)
            {
                if (model != null) UnityEngine.Object.Destroy(model);
                model = Build(look);
                model.transform.SetParent(rig.transform, false);
                builtFor = key;
            }
            spin += Time.unscaledDeltaTime * 40f;
            model.transform.localRotation = Quaternion.Euler(0, 35f * Mathf.Sin(spin * Mathf.Deg2Rad), 0);   // facing the camera (the model faces +Z), turning side to side
            return rt;
        }

        static void HidePreview()
        {
            if (rig == null || !rig.activeSelf) return;
            rig.SetActive(false);
            var f = GameRoot.I != null ? GameRoot.I.flight : null;
            FlightMode.SetWorldAo(!(f != null && f.inside));   // (back on, unless the cabin wants it off)
        }

        static void Lamp(string name, Vector3 at, Color c, float intensity)
        {
            var g = new GameObject(name) { layer = PreviewLayer };
            g.transform.SetParent(rig.transform, false);
            g.transform.localPosition = at;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = 9f;
            l.shadows = LightShadows.None;
            l.cullingMask = 1 << PreviewLayer;
        }

        /// <summary>A whole Ardling standing at the origin (feet on y = 0), on the preview layer.</summary>
        public static GameObject Build(Ardlings.Look look, int layer = PreviewLayer)
        {
            var go = new GameObject("Ardling");
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0, Ardlings.Height, 0);
            void Piece(Transform parent, Mesh m)
            {
                var g = new GameObject(m.name) { layer = layer };
                g.transform.SetParent(parent, false);
                g.AddComponent<MeshFilter>().sharedMesh = m;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = Mats.Lit;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // the preview: no sun (its own lamps light it) and a soft fill, so the colours stay true whatever the sun's doing
                if (layer == PreviewLayer) { var pb = new MaterialPropertyBlock(); pb.SetFloat("_Ambient", 0.3f); pb.SetFloat("_SunScale", 0f); r.SetPropertyBlock(pb); }
            }
            Piece(body.transform, Ardlings.Body(look, false));
            var note = Ardlings.AddNote(body.transform, look);
            note.layer = layer;
            // (milestone 35: out of the game's light the note's paper would glare white: dimmed to match)
            var mpb = new MaterialPropertyBlock();
            mpb.SetFloat("_Bright", layer == PreviewLayer ? 0.95f : 0.75f);
            mpb.SetFloat("_Flat", layer == PreviewLayer ? 1f : 0.7f);
            note.GetComponent<Renderer>().SetPropertyBlock(mpb);
            Mesh[] lm = { Ardlings.Arm(look, -1), Ardlings.Arm(look, 1), Ardlings.Leg(look, -1), Ardlings.Leg(look, 1) };
            Vector3[] piv = { Ardlings.ShoulderL, Ardlings.ShoulderR, Ardlings.HipL, Ardlings.HipR };
            for (int k = 0; k < 4; k++)
            {
                var lg = new GameObject("Limb") { layer = layer };
                lg.transform.SetParent(body.transform, false);
                lg.transform.localPosition = piv[k];
                if (k == 0) lg.transform.localRotation = Quaternion.Euler(0, 0, -12f);   // a little wave
                Piece(lg.transform, lm[k]);
            }
            go.layer = layer;
            body.layer = layer;
            return go;
        }
    }
}
