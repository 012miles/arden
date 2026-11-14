using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>IMGUI styling and helpers. Everything is laid out in a virtual space ~900 px tall.</summary>
    public static class Ui
    {
        public static float Scale = 1f, W = 1600f, H = 900f;
        public const float MinWidth = 1480f, MinHeight = 900f;
        public static GUIStyle Panel, Label, Bold, Small, SmallLine, Header, Title, Hero, Button, ButtonSmall, ButtonSel, ButtonSmallSel, Row, RowSel, RowSelCenter, Center, BigCenter, Hint, Field;
        public static readonly Color Accent = new Color(0.35f, 0.8f, 1f);
        static Texture2D texPanel, texRowSel, texWhite;
        static List<Rect> rects = new List<Rect>(), rectsBuilding = new List<Rect>();
        // Everything drawn last frame, in order: panels (true) and buttons (false), for the layout check
        static List<(Rect r, bool panel)> drawn = new List<(Rect, bool)>(), drawnBuilding = new List<(Rect, bool)>();
        static bool init;

        /// <summary>
        /// How big the interface is (milestone 32: it fits any computer by itself). Every screen is laid out in a virtual
        /// space at least MinWidth x MinHeight, scaled to fill the window as far as it can without running out of room, so
        /// nothing overlaps on any screen shape: a 13" or 15" laptop, a 16:9 monitor, an ultrawide, a small window.
        /// The size setting can only make it smaller (more room); a bigger interface would have to overlap.
        /// </summary>
        public static float ComputeScale(int screenW, int screenH, float setting)
        {
            float fit = Mathf.Min(screenH / MinHeight, screenW / MinWidth);   // the biggest it can be without overlapping
            return Mathf.Clamp(fit * Mathf.Clamp(setting, 0.6f, 1f), 0.3f, 4f);
        }

        public static void Begin()
        {
            if (!init) Init();
            Scale = ComputeScale(Screen.width, Screen.height, Settings.UiScale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
            W = Screen.width / Scale;
            H = Screen.height / Scale;
            var e = Event.current;
            if (e.type == EventType.Repaint)
            {
                var tmp = rects; rects = rectsBuilding; rectsBuilding = tmp;
                rectsBuilding.Clear();
                var tmp2 = drawn; drawn = drawnBuilding; drawnBuilding = tmp2;
                drawnBuilding.Clear();
            }
            // Clicking anywhere leaves a text box (clicking another text box goes straight into that one, as it's drawn later)
            if (e.type == EventType.MouseDown) GUIUtility.keyboardControl = 0;
        }

        static bool TopLevel => GUIUtility.GUIToScreenPoint(Vector2.zero).sqrMagnitude < 0.01f;

        static void Note(Rect r, GUIStyle s)
        {
            if (Event.current.type == EventType.Repaint && s != GUIStyle.none && TopLevel) drawnBuilding.Add((r, false));
        }

        /// <summary>GUI.Button, noted for the layout check (buttons that land on other buttons).</summary>
        public static bool Press(Rect r, string text, GUIStyle s) { Note(r, s); return GUI.Button(r, text, s); }
        public static bool Press(Rect r, GUIContent c, GUIStyle s) { Note(r, s); return GUI.Button(r, c, s); }
        public static bool Press(Rect r, string text) { Note(r, Button); return GUI.Button(r, text, Button ?? GUI.skin.button); }
        public static bool Press(Rect r, GUIContent c) { Note(r, Button); return GUI.Button(r, c, Button ?? GUI.skin.button); }

        /// <summary>True if the mouse (screen pixels, bottom-left origin) is over any panel drawn last frame.</summary>
        public static bool MouseOverUI(Vector2 px)
        {
            var p = new Vector2(px.x / Scale, (Screen.height - px.y) / Scale);
            foreach (var r in rects) if (r.Contains(p)) return true;
            return false;
        }

        /// <summary>True if the rect (GUI space) overlaps any panel drawn last frame.</summary>
        public static bool OverPanel(Rect r)
        {
            foreach (var p in rects) if (p.Overlaps(r)) return true;
            return false;
        }

        /// <summary>Autotest check: panels drawn last frame that run off the screen or overlap another panel.</summary>
        public static List<string> LayoutProblems()
        {
            var bad = new List<string>();
            string R(Rect r) => $"[{r.x:0},{r.y:0} {r.width:0}x{r.height:0}]";
            // A box over most of the screen is a dimmer behind a modal screen: whatever was drawn before it is meant to be covered.
            bool Backdrop(Rect r) => r.width * r.height >= W * H * 0.8f;
            var live = new List<Rect>();
            foreach (var a in rects)
            {
                if (Backdrop(a)) { live.Clear(); continue; }
                if (a.x < -1 || a.y < -1 || a.xMax > W + 1 || a.yMax > H + 1) bad.Add($"off screen {R(a)} in {W:0}x{H:0}");
                foreach (var b in live)
                {
                    if (Contains(b, a) || Contains(a, b)) continue;   // a card inside its panel
                    var o = Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
                    if (o.width > 2 && o.height > 2) bad.Add($"overlap {R(b)} & {R(a)} by {o.width:0}x{o.height:0}");
                }
                live.Add(a);
            }
            // Buttons on top of other buttons (a toolbar too full for the window), unless a panel drawn after the first
            // one covers the spot (a menu that drops down over them).
            for (int i = 0; i < drawn.Count; i++)
            {
                if (drawn[i].panel) continue;
                var a = drawn[i].r;
                if (a.x < -1 || a.y < -1 || a.xMax > W + 1 || a.yMax > H + 1) bad.Add($"button off screen {R(a)} in {W:0}x{H:0}");
                for (int j = i + 1; j < drawn.Count; j++)
                {
                    if (drawn[j].panel) continue;
                    var b = drawn[j].r;
                    var o = Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
                    if (o.width <= 2 || o.height <= 2) continue;
                    bool covered = false;
                    for (int k = i + 1; k < j && !covered; k++) if (drawn[k].panel && Contains(drawn[k].r, o)) covered = true;
                    if (!covered) bad.Add($"buttons overlap {R(a)} & {R(b)} by {o.width:0}x{o.height:0}");
                }
            }
            return bad;
        }

        static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 && inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;

        public static void Box(Rect r)
        {
            // Boxes inside a scroll view are in its own coordinates; the scroll view's panel already covers them.
            if (Event.current.type == EventType.Repaint && TopLevel) { rectsBuilding.Add(r); drawnBuilding.Add((r, true)); }
            GUI.Box(r, GUIContent.none, Panel);
        }

        public static void Bar(Rect r, float frac, Color fill)
        {
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.12f);
            GUI.DrawTexture(r, texWhite);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), texWhite);
            GUI.color = old;
        }

        public static void Line(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, texWhite);
            GUI.color = old;
        }

        public static string Dist(double m)
        {
            double a = Math.Abs(m);
            if (double.IsInfinity(m) || double.IsNaN(m)) return "--";
            if (a < 10_000) return $"{m:0} m";
            if (a < 10_000_000) return $"{m / 1000:0.0} km";
            if (a < 1_000_000_000) return $"{m / 1_000_000:0.00} Mm";
            return $"{m / 1e9:0.00} Gm";
        }

        public static string Time(double s)
        {
            if (double.IsNaN(s) || double.IsInfinity(s)) return "--";
            if (s < 0) return "-" + Time(-s);
            long t = (long)Math.Round(s);
            long d = t / 86400, h = (t / 3600) % 24, m = (t / 60) % 60, sec = t % 60;
            if (d > 0) return $"{d}d {h}h {m:00}m";
            if (h > 0) return $"{h}h {m:00}m {sec:00}s";
            if (m > 0) return $"{m}m {sec:00}s";
            return $"{sec}s";
        }

        /// <summary>Fonts (milestone 23): Barlow for text, Exo 2 for headings (both SIL Open Font License).</summary>
        public static Font Body, BodyMedium, BodyBold, Display, DisplayBold;

        static void Init()
        {
            init = true;
            Body = Resources.Load<Font>("Fonts/Barlow-Regular");
            BodyMedium = Resources.Load<Font>("Fonts/Barlow-Medium");
            BodyBold = Resources.Load<Font>("Fonts/Barlow-SemiBold");
            Display = Resources.Load<Font>("Fonts/Exo2-SemiBold");
            DisplayBold = Resources.Load<Font>("Fonts/Exo2-Bold");
            // Rounded panels and buttons, drawn once into small textures and nine-sliced
            texPanel = Rounded(32, 9, new Color(0.035f, 0.05f, 0.075f, 0.86f), new Color(0.32f, 0.52f, 0.62f, 0.28f));
            texRowSel = Rounded(24, 6, new Color(0.16f, 0.43f, 0.6f, 0.88f), new Color(0.45f, 0.75f, 0.9f, 0.5f));
            texWhite = Tex(Color.white);
            var texBtn = Rounded(24, 6, new Color(0.12f, 0.16f, 0.21f, 0.94f), new Color(0.34f, 0.44f, 0.52f, 0.55f));
            var texBtnHover = Rounded(24, 6, new Color(0.17f, 0.25f, 0.32f, 0.97f), new Color(0.45f, 0.65f, 0.78f, 0.7f));
            var texBtnDown = Rounded(24, 6, new Color(0.18f, 0.46f, 0.62f, 1f), new Color(0.55f, 0.82f, 0.95f, 0.8f));
            var texField = Rounded(24, 5, new Color(0.02f, 0.03f, 0.05f, 0.92f), new Color(0.35f, 0.5f, 0.6f, 0.6f));
            var text = new Color(0.9f, 0.93f, 0.96f);
            Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8), border = new RectOffset(10, 10, 10, 10) };
            Panel.normal.background = texPanel;
            Label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = false, font = BodyMedium, clipping = TextClipping.Overflow };
            Label.normal.textColor = text;
            Bold = new GUIStyle(Label) { font = BodyBold };
            Small = new GUIStyle(Label) { fontSize = 13, wordWrap = true, font = Body };
            Small.normal.textColor = new Color(0.7f, 0.76f, 0.82f);
            SmallLine = new GUIStyle(Small) { wordWrap = false, clipping = TextClipping.Clip };   // one line, cut at the edge (part cards)
            Header = new GUIStyle(Label) { fontSize = 17, font = Display };
            Header.normal.textColor = Accent;
            Title = new GUIStyle(Label) { fontSize = 23, font = DisplayBold };
            Hero = new GUIStyle(Label) { fontSize = 52, font = DisplayBold };
            Button = Btn(15, BodyMedium, texBtn, texBtnHover, texBtnDown, text);
            ButtonSmall = Btn(13, BodyMedium, texBtn, texBtnHover, texBtnDown, text);
            ButtonSmall.padding = new RectOffset(4, 4, 2, 2);
            // A button that's switched on (the tool in use, the chosen option): the same shape and centred text as any
            // button, in the highlight colour. (Before milestone 32 these used the list-row style, which put the text in
            // the top-left corner.)
            ButtonSel = Btn(15, BodyMedium, texRowSel, texRowSel, texBtnDown, Color.white);
            ButtonSmallSel = Btn(13, BodyMedium, texRowSel, texRowSel, texBtnDown, Color.white);
            ButtonSmallSel.padding = new RectOffset(4, 4, 2, 2);
            Row = new GUIStyle(Label) { padding = new RectOffset(6, 6, 3, 3) };
            RowSel = new GUIStyle(Row) { border = new RectOffset(7, 7, 7, 7) };
            RowSel.normal.background = texRowSel;
            RowSel.hover.background = texRowSel;
            RowSel.normal.textColor = RowSel.hover.textColor = Color.white;
            RowSelCenter = new GUIStyle(RowSel) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            Center = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            BigCenter = new GUIStyle(Center) { fontSize = 25, font = DisplayBold };
            Hint = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter };
            Field = new GUIStyle(GUI.skin.textField) { fontSize = 15, font = BodyMedium, border = new RectOffset(6, 6, 6, 6), padding = new RectOffset(6, 6, 3, 3) };
            Field.normal.background = Field.focused.background = Field.hover.background = texField;
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = text;
        }

        /// <summary>Three buttons choosing how the game talks (Easy / Standard / Engineer). Returns the height used.</summary>
        public static float WordsPicker(float x, float y, float bw)
        {
            for (int i = 0; i < 3; i++)
                if (Press(new Rect(x + i * (bw + 4), y, bw, 28), Words.Names[i], (int)Words.Level == i ? ButtonSmallSel : ButtonSmall)) Words.Level = (WordLevel)i;
            return 28;
        }

        static GUIStyle Btn(int size, Font f, Texture2D normal, Texture2D hover, Texture2D down, Color text)
        {
            // Text in the middle of the button, with even padding all round (the fonts sit a pixel low, so one less at the top)
            var b = new GUIStyle(GUI.skin.button) { fontSize = size, font = f, border = new RectOffset(7, 7, 7, 7), alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 2, 4), wordWrap = false, clipping = TextClipping.Clip };
            b.normal.background = b.focused.background = normal;
            b.hover.background = hover;
            b.active.background = b.onActive.background = down;
            b.onNormal.background = b.onHover.background = down;
            b.normal.textColor = b.focused.textColor = b.hover.textColor = b.active.textColor = text;
            b.onNormal.textColor = b.onHover.textColor = Color.white;
            return b;
        }

        /// <summary>A rounded rectangle (anti-aliased) with a thin border, for nine-slicing.</summary>
        static Texture2D Rounded(int size, int radius, Color fill, Color border)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float r = radius, h = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // distance outside a rounded box (negative inside)
                    float qx = Mathf.Abs(x + 0.5f - h) - (h - r), qy = Mathf.Abs(y + 0.5f - h) - (h - r);
                    float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
                    float inside = Mathf.Clamp01(0.5f - d);
                    float edge = Mathf.Clamp01(1.2f - Mathf.Abs(d + 0.9f));
                    Color c = Color.Lerp(fill, new Color(border.r, border.g, border.b, Mathf.Max(fill.a, border.a)), edge * border.a);
                    c.a *= inside;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
