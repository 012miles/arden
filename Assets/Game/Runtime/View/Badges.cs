using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 23: achievement badges, drawn in code like the Ardlings' sticky notes: a round, hexagonal, shield or
    /// star badge in the achievement's colour with a gold rim and a white symbol. Locked ones are grey with a "?".
    /// Also the pop-up when one is earned and the Badges screen.
    /// </summary>
    public static class Badges
    {
        const int N = 128;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Texture(AchievementDef a, bool earned)
        {
            string key = a.id + (earned ? "" : "?");
            if (cache.TryGetValue(key, out var t)) return t;
            t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Badge " + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color[N * N];
            Color fill = earned ? a.color : new Color(0.25f, 0.27f, 0.3f);
            Color rim = earned ? new Color(1f, 0.82f, 0.38f) : new Color(0.42f, 0.44f, 0.47f);
            Color ink = earned ? new Color(1f, 1f, 1f, 0.97f) : new Color(0.55f, 0.58f, 0.62f);
            float aa = 1.5f / N;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / N * 2 - 1, (y + 0.5f) / N * 2 - 1);
                    float d = Shape(a.shape, p);
                    if (d > aa) { px[y * N + x] = Color.clear; continue; }
                    // body: a soft top-to-bottom shading, then the rim, then the symbol
                    Color c = fill * (0.85f + 0.3f * (p.y * 0.5f + 0.5f));
                    c.a = 1;
                    float rimW = 0.09f;
                    if (d > -rimW) c = Color.Lerp(c, rim * (0.9f + 0.2f * (p.y * 0.5f + 0.5f)), Mathf.Clamp01((d + rimW) / (2 * aa)));
                    float s = earned ? Icon(a.icon, p / 0.62f) * 0.62f : Question(p / 0.62f) * 0.62f;
                    c = Color.Lerp(c, ink, Mathf.Clamp01((aa - s) / (2 * aa)) * ink.a);
                    c.a = Mathf.Clamp01((aa - d) / (2 * aa));
                    px[y * N + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true, false);
            cache[key] = t;
            return t;
        }

        // ---------------------------------------------------------------- shapes (signed distance, negative inside)

        static float Shape(int shape, Vector2 p)
        {
            switch (shape)
            {
                case 1: // hexagon
                {
                    Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
                    return Mathf.Max(q.x * 0.866f + q.y * 0.5f, q.y) - 0.86f;
                }
                case 2: // shield: flat top, pointed bottom
                {
                    float top = p.y - 0.85f;
                    float sides = Mathf.Abs(p.x) - 0.82f;
                    float point = (Mathf.Abs(p.x) * 0.9f - p.y * 0.55f) - 0.62f;   // the lower edges meet in a point
                    return Mathf.Max(Mathf.Max(top, sides), Mathf.Max(point, -p.y - 0.95f));
                }
                case 3: // five-pointed star with rounded inner corners, drawn fat so the symbol fits
                {
                    float ang = Mathf.Atan2(p.x, p.y), r = p.magnitude;
                    float k = Mathf.Cos(Mathf.PI / 5f) / Mathf.Cos((Mathf.Repeat(ang, 2 * Mathf.PI / 5f)) - Mathf.PI / 5f);
                    float star = Mathf.Lerp(0.72f, 0.95f, Mathf.Pow(Mathf.Clamp01((k - 0.809f) / 0.191f), 0.7f));
                    return Mathf.Min(r - star, r - 0.74f);
                }
                default: return p.magnitude - 0.92f;
            }
        }

        static float Seg(Vector2 p, Vector2 a, Vector2 b, float w)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t) - w;
        }
        static float Circle(Vector2 p, Vector2 c, float r) => Vector2.Distance(p, c) - r;
        static float Ring(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs(Vector2.Distance(p, c) - r) - w;
        static float Box(Vector2 p, Vector2 c, Vector2 h) { Vector2 q = new Vector2(Mathf.Abs(p.x - c.x) - h.x, Mathf.Abs(p.y - c.y) - h.y); return Mathf.Max(q.x, q.y); }
        static float U(params float[] d) { float m = 9; foreach (var x in d) m = Mathf.Min(m, x); return m; }

        static float Question(Vector2 p) => U(Ring(p, new Vector2(0, 0.25f), 0.28f, 0.08f) + (p.y < 0.1f && p.x < 0.05f ? 1 : 0), Seg(p, new Vector2(0.05f, 0.0f), new Vector2(0, -0.25f), 0.08f), Circle(p, new Vector2(0, -0.5f), 0.1f));

        /// <summary>The white symbol on a badge.</summary>
        static float Icon(int icon, Vector2 p)
        {
            switch (icon)
            {
                case 0: // rocket
                    return U(Seg(p, new Vector2(0, -0.45f), new Vector2(0, 0.45f), 0.17f), Circle(p, new Vector2(0, 0.45f), 0.17f),
                        Seg(p, new Vector2(-0.17f, -0.3f), new Vector2(-0.38f, -0.6f), 0.07f), Seg(p, new Vector2(0.17f, -0.3f), new Vector2(0.38f, -0.6f), 0.07f),
                        Seg(p, new Vector2(0, -0.62f), new Vector2(0, -0.8f), 0.07f));
                case 1: // arrow up over a line (altitude)
                    return U(Seg(p, new Vector2(0, -0.35f), new Vector2(0, 0.6f), 0.09f), Seg(p, new Vector2(0, 0.6f), new Vector2(-0.3f, 0.28f), 0.09f),
                        Seg(p, new Vector2(0, 0.6f), new Vector2(0.3f, 0.28f), 0.09f), Seg(p, new Vector2(-0.55f, -0.6f), new Vector2(0.55f, -0.6f), 0.07f));
                case 2: // planet with an orbit
                    return U(Circle(p, Vector2.zero, 0.32f), Ring(new Vector2(p.x, p.y * 2.6f), Vector2.zero, 0.68f, 0.09f) + (p.y > 0 && p.magnitude < 0.36f ? 1 : 0));
                case 3: // flag
                    return U(Seg(p, new Vector2(-0.35f, -0.7f), new Vector2(-0.35f, 0.65f), 0.07f), Box(p, new Vector2(0.05f, 0.38f), new Vector2(0.38f, 0.24f)));
                case 4: // footprint
                    return U(Circle(new Vector2(p.x * 1.4f, p.y), new Vector2(0, -0.15f), 0.42f), Circle(p, new Vector2(-0.22f, 0.45f), 0.1f), Circle(p, new Vector2(-0.02f, 0.55f), 0.1f), Circle(p, new Vector2(0.18f, 0.5f), 0.09f));
                case 5: // two rings joined (docking)
                    return U(Ring(p, new Vector2(-0.28f, 0), 0.3f, 0.08f), Ring(p, new Vector2(0.28f, 0), 0.3f, 0.08f));
                case 6: // speed streaks
                    return U(Seg(p, new Vector2(-0.6f, 0.3f), new Vector2(0.4f, 0.3f), 0.08f), Seg(p, new Vector2(-0.3f, 0), new Vector2(0.6f, 0), 0.08f), Seg(p, new Vector2(-0.6f, -0.3f), new Vector2(0.4f, -0.3f), 0.08f));
                case 7: // giant planet with rings
                    return U(Circle(p, Vector2.zero, 0.38f), Ring(new Vector2(p.x, (p.y - p.x * 0.3f) * 3f), Vector2.zero, 0.75f, 0.12f) + (p.y > -0.05f && p.magnitude < 0.42f ? 1 : 0));
                case 8: // sun
                {
                    float rays = 9;
                    for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); rays = Mathf.Min(rays, Seg(p, d * 0.45f, d * 0.7f, 0.07f)); }
                    return U(Circle(p, Vector2.zero, 0.3f), rays);
                }
                case 9: // wheel
                    return U(Ring(p, Vector2.zero, 0.45f, 0.11f), Circle(p, Vector2.zero, 0.12f), Seg(p, new Vector2(-0.4f, 0), new Vector2(0.4f, 0), 0.05f), Seg(p, new Vector2(0, -0.4f), new Vector2(0, 0.4f), 0.05f));
                case 10: // drop (fuel)
                    return U(Circle(p, new Vector2(0, -0.15f), 0.35f), Seg(p, new Vector2(0, 0.6f), new Vector2(-0.3f, -0.05f), 0.05f) - 0.0f, Seg(p, new Vector2(0, 0.6f), new Vector2(0.3f, -0.05f), 0.05f),
                        Box(p, new Vector2(0, 0.1f), new Vector2(0.22f, 0.25f)) + Mathf.Abs(p.x) * 0.6f - 0.05f);
                case 11: // rock
                    return U(Circle(new Vector2(p.x * 0.8f, p.y), new Vector2(0.02f, -0.02f), 0.45f) + 0.06f * Mathf.Sin(Mathf.Atan2(p.y, p.x) * 5));
                case 12: // dome base
                    return U(Circle(p, new Vector2(0, -0.2f), 0.42f) + (p.y < -0.2f ? 1 : 0), Box(p, new Vector2(0, -0.32f), new Vector2(0.62f, 0.12f)));
                case 13: // flask (research)
                    return U(Box(p, new Vector2(0, 0.42f), new Vector2(0.12f, 0.22f)), Circle(p, new Vector2(0, -0.2f), 0.38f), Seg(p, new Vector2(-0.2f, 0.64f), new Vector2(0.2f, 0.64f), 0.05f));
                case 14: // calendar
                    return U(Box(p, new Vector2(0, -0.05f), new Vector2(0.5f, 0.45f)) + (Box(p, new Vector2(0, -0.12f), new Vector2(0.38f, 0.28f)) < 0 ? 1 : 0), Seg(p, new Vector2(-0.25f, 0.4f), new Vector2(-0.25f, 0.6f), 0.06f), Seg(p, new Vector2(0.25f, 0.4f), new Vector2(0.25f, 0.6f), 0.06f), Box(p, new Vector2(-0.12f, -0.12f), new Vector2(0.1f, 0.08f)));
                case 15: // parachute
                    return U(Circle(p, new Vector2(0, 0.15f), 0.55f) + (p.y < 0.15f ? 1 : 0), Seg(p, new Vector2(-0.52f, 0.15f), new Vector2(0, -0.55f), 0.04f), Seg(p, new Vector2(0.52f, 0.15f), new Vector2(0, -0.55f), 0.04f), Seg(p, new Vector2(0, 0.15f), new Vector2(0, -0.55f), 0.04f), Circle(p, new Vector2(0, -0.6f), 0.1f));
                case 16: // crash burst
                {
                    float d = 9;
                    for (int k = 0; k < 10; k++) { float a = k * Mathf.PI / 5 + 0.2f, len = k % 2 == 0 ? 0.7f : 0.45f; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); d = Mathf.Min(d, Seg(p, Vector2.zero, dir * len, 0.09f)); }
                    return d;
                }
                case 18: // compass
                    return U(Ring(p, Vector2.zero, 0.55f, 0.08f), Seg(p, new Vector2(0, -0.35f), new Vector2(0, 0.42f), 0.1f) + Mathf.Abs(p.y) * 0.12f, Circle(p, Vector2.zero, 0.08f));
                case 19: // camera
                    return U(Box(p, new Vector2(0, -0.08f), new Vector2(0.6f, 0.36f)) + (Circle(p, new Vector2(0, -0.08f), 0.2f) < 0 ? 1 : 0), Box(p, new Vector2(-0.18f, 0.36f), new Vector2(0.16f, 0.1f)), Ring(p, new Vector2(0, -0.08f), 0.2f, 0.06f));
                case 20: // cloud with rain
                    return U(Circle(p, new Vector2(-0.2f, 0.12f), 0.24f), Circle(p, new Vector2(0.12f, 0.2f), 0.3f), Box(p, new Vector2(0, 0.02f), new Vector2(0.45f, 0.13f)),
                        Seg(p, new Vector2(-0.3f, -0.3f), new Vector2(-0.38f, -0.55f), 0.05f), Seg(p, new Vector2(0, -0.3f), new Vector2(-0.08f, -0.6f), 0.05f), Seg(p, new Vector2(0.3f, -0.3f), new Vector2(0.22f, -0.55f), 0.05f));
                case 21: // station: a truss with two panels and a module
                    return U(Seg(p, new Vector2(-0.6f, 0), new Vector2(0.6f, 0), 0.05f), Box(p, new Vector2(-0.5f, 0), new Vector2(0.12f, 0.4f)), Box(p, new Vector2(0.5f, 0), new Vector2(0.12f, 0.4f)), Box(p, Vector2.zero, new Vector2(0.18f, 0.16f)));
                default: // coin with a star-ish mark (millionaire)
                    return U(Ring(p, Vector2.zero, 0.5f, 0.08f), Seg(p, new Vector2(0, -0.3f), new Vector2(0, 0.3f), 0.08f), Seg(p, new Vector2(-0.2f, 0.18f), new Vector2(0.2f, 0.18f), 0.06f), Seg(p, new Vector2(-0.2f, -0.18f), new Vector2(0.2f, -0.18f), 0.06f));
            }
        }

        // ---------------------------------------------------------------- the pop-up and the Badges screen

        static string showing;
        static float showUntil;

        const float ToastTime = 6f, SlideTime = 0.45f;

        /// <summary>
        /// Over every screen (milestone 24): the newest badge slides in from the right edge near the bottom right, its
        /// badge pops in with a little bounce and a shine sweeps across, then after a few seconds it slides back out.
        /// </summary>
        public static void DrawToast(Career c, float W)
        {
            float now = Time.unscaledTime;
            if (Event.current.type == EventType.Layout && now > showUntil && c.badgeNews.Count > 0)
            {
                showing = c.badgeNews[0];
                c.badgeNews.RemoveAt(0);
                showUntil = now + ToastTime;
                GameAudio.I?.Confirm();
            }
            if (showing == null || now > showUntil) return;
            var a = Achievements.Get(showing);
            if (a == null) return;
            float age = ToastTime - (showUntil - now), left = showUntil - now;
            float k = age < SlideTime ? 1f - Mathf.Pow(1f - age / SlideTime, 3f)              // in: fast, easing to a stop
                : left < SlideTime ? Mathf.SmoothStep(0f, 1f, left / SlideTime) : 1f;     // out: eases off, then away
            const float w = 380, h = 96;
            var r = new Rect(W - w - 14 + (1f - k) * (w + 40), Ui.H - h - 150, w, h);
            GUI.Box(r, GUIContent.none, Ui.Panel);
            GUI.Box(r, GUIContent.none, Ui.Panel);                                         // twice: nearly solid
            Ui.Line(new Rect(r.x + 3, r.y + 8, 3, h - 16), new Color(1f, 0.78f, 0.25f));    // gold edge
            // the badge pops in: over-size, then settles
            float pop = Mathf.Clamp01((age - 0.15f) / 0.5f);
            float s = pop <= 0 ? 0 : 64f * (1f + 0.25f * Mathf.Sin(pop * Mathf.PI) * (1f - pop) * 2.2f) * Mathf.Min(1f, pop * 3f);
            if (s > 1) GUI.DrawTexture(new Rect(r.x + 16 + 32 - s / 2, r.y + h / 2 - s / 2, s, s), Texture(a, true));
            GUI.Label(new Rect(r.x + 92, r.y + 10, w - 100, 20), $"<color=#ffd060>{Words.T("ACHIEVEMENT UNLOCKED", "ACHIEVEMENT UNLOCKED", "BADGE EARNED!")}</color>", Ui.Small);
            GUI.Label(new Rect(r.x + 92, r.y + 28, w - 100, 26), a.name, Ui.Header);
            string reward = c.IsCareer && (a.funds > 0 || a.science > 0) ? $"  <color=#9fe0a0>+{a.funds:N0} funds, +{a.science:0} science</color>" : "";
            GUI.Label(new Rect(r.x + 92, r.y + 54, w - 100, 36), Words.Say(a.desc) + reward, Ui.Small);
            // a shine sweeping across once it has arrived
            float sweep = (age - SlideTime) / 0.9f;
            if (sweep > 0 && sweep < 1)
            {
                GUI.BeginGroup(r);
                for (int i = 0; i < 6; i++) Ui.Line(new Rect(-60 + sweep * (w + 120) + i * 5, 0, 5, h), new Color(1f, 1f, 1f, 0.05f + 0.035f * Mathf.Sin(i / 5f * Mathf.PI)));
                GUI.EndGroup();
            }
        }

        /// <summary>Every badge: earned ones in colour, the rest grey. Returns false when closed.</summary>
        public static bool DrawScreen(Rect r, Career c)
        {
            Ui.Box(r);
            GUI.Label(new Rect(r.x + 16, r.y + 12, 300, 26), "Badges", Ui.Header);
            GUI.Label(new Rect(r.x + 110, r.y + 15, 500, 22), $"{c.badges.Count} of {Achievements.All.Length} earned in this save", Ui.Small);
            bool open = !Ui.Press(new Rect(r.xMax - 40, r.y + 10, 28, 24), "x", Ui.ButtonSmall);
            int cols = 7;
            float cw = (r.width - 24) / cols, ch = Mathf.Min(132, (r.height - 56) / 4);
            for (int i = 0; i < Achievements.All.Length; i++)
            {
                var a = Achievements.All[i];
                bool got = c.badges.Contains(a.id);
                float x = r.x + 12 + (i % cols) * cw, y = r.y + 46 + (i / cols) * ch;
                float s = Mathf.Min(64, ch - 56);
                GUI.DrawTexture(new Rect(x + cw / 2 - s / 2, y, s, s), Texture(a, got));
                GUI.Label(new Rect(x + 2, y + s + 2, cw - 4, 18), got ? $"<b>{a.name}</b>" : a.name, Ui.Hint);
                GUI.Label(new Rect(x + 2, y + s + 19, cw - 4, 34), Words.Say(a.desc), Ui.Hint);
            }
            return open;
        }
    }
}
