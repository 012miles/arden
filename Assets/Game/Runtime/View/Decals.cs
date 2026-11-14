using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 34 (captaincarl24: "add the option to paint your ship and add custom decals for both multiplayer and
    /// singleplayer"): stickers on parts. Built-in ones (stars, stripes, numbers, Ardling faces...) are drawn here; your
    /// own pictures (PNG or JPG in the Decals folder) are shrunk to 128 pixels and stored in the rocket's design, so
    /// they go with it into saves and to the other players in a room. A decal is kept on its part as one string:
    /// "id|x|y|z|nx|ny|nz|size|spin" (where on the part, which way it faces, how big, how it's turned).
    /// </summary>
    public static class Decals
    {
        public const int Px = 128;

        public static readonly string[] BuiltIn =
        {
            "star", "ring", "logo", "stripes", "checker", "bolt", "heart", "arrow", "flame", "moon", "planet", "wings",
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            "face0", "face1", "face2", "face3", "face4", "face5", "face6", "face7", "face8", "face9",
        };

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Shader shader;

        public static string FolderPath => Path.Combine(Application.persistentDataPath, "Decals");
        static string CachePath => Path.Combine(Application.persistentDataPath, "DecalCache");

        // ---------------------------------------------------------------- the decal string

        public struct Spec
        {
            public string id;
            public Vector3 pos, normal;
            public float size, spin;
        }

        public static string Format(Spec s) => string.Join("|", s.id,
            F(s.pos.x), F(s.pos.y), F(s.pos.z), F(s.normal.x), F(s.normal.y), F(s.normal.z), F(s.size), F(s.spin));
        static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        public static bool Parse(string decal, out Spec s)
        {
            s = default;
            if (string.IsNullOrEmpty(decal)) return false;
            var p = decal.Split('|');
            if (p.Length != 9) return false;
            var f = new float[8];
            for (int i = 0; i < 8; i++)
                if (!float.TryParse(p[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f[i])) return false;
            s = new Spec { id = p[0], pos = new Vector3(f[0], f[1], f[2]), normal = new Vector3(f[3], f[4], f[5]), size = Mathf.Clamp(f[6], 0.1f, 4f), spin = f[7] };
            if (s.normal.sqrMagnitude < 1e-6f) s.normal = Vector3.forward;
            return true;
        }

        // ---------------------------------------------------------------- your own pictures

        /// <summary>The pictures in the Decals folder (made if it isn't there).</summary>
        public static string[] UserFiles()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                return Directory.GetFiles(FolderPath).Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f).ToArray();
            }
            catch { return new string[0]; }
        }

        /// <summary>Makes a picture file into a decal for this rocket: shrunk, stored in the design. Returns its id, or null.</summary>
        public static string AddImage(VesselDesign d, string file)
        {
            try
            {
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!src.LoadImage(File.ReadAllBytes(file))) return null;
                var small = Shrink(src);
                UnityEngine.Object.Destroy(src);
                byte[] png = small.EncodeToPNG();
                string id = "img:" + Hash(png);
                if (d.images == null) d.images = new List<DecalImage>();
                if (!d.images.Any(i => i.id == id)) d.images.Add(new DecalImage { id = id, png = Convert.ToBase64String(png) });
                Keep(id, png, small);
                return id;
            }
            catch (Exception e) { Debug.LogWarning("Couldn't read that picture: " + e.Message); return null; }
        }

        /// <summary>Learns the pictures a design carries (from a save, or another player), so its decals can be drawn.</summary>
        public static void Learn(VesselDesign d)
        {
            if (d?.images == null) return;
            foreach (var img in d.images)
            {
                if (img == null || string.IsNullOrEmpty(img.id) || textures.ContainsKey(img.id)) continue;
                try
                {
                    byte[] png = Convert.FromBase64String(img.png);
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = img.id };
                    if (t.LoadImage(png)) Keep(img.id, png, t);
                }
                catch { }
            }
        }

        /// <summary>Drops pictures the rocket no longer uses (so a design doesn't carry old ones round).</summary>
        public static void Tidy(VesselDesign d)
        {
            if (d?.images == null || d.images.Count == 0) return;
            var used = new HashSet<string>(d.nodes.Where(n => !string.IsNullOrEmpty(n.decal)).Select(n => n.decal.Split('|')[0]));
            d.images.RemoveAll(i => !used.Contains(i.id));
        }

        static void Keep(string id, byte[] png, Texture2D t)
        {
            t.wrapMode = TextureWrapMode.Clamp;
            textures[id] = t;
            try
            {
                Directory.CreateDirectory(CachePath);
                string f = Path.Combine(CachePath, id.Substring(4) + ".png");
                if (!File.Exists(f)) File.WriteAllBytes(f, png);
            }
            catch { }
        }

        static Texture2D Shrink(Texture2D src)
        {
            float k = Mathf.Min(1f, (float)Px / Mathf.Max(src.width, src.height));
            int w = Mathf.Max(1, Mathf.RoundToInt(src.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(src.height * k));
            var rt = RenderTexture.GetTemporary(Px, Px, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            // centred in a square, keeping its shape
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Px, Px, 0);
            Graphics.DrawTexture(new Rect((Px - w) / 2f, (Px - h) / 2f, w, h), src);
            GL.PopMatrix();
            var t = new Texture2D(Px, Px, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            t.ReadPixels(new Rect(0, 0, Px, Px), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        static string Hash(byte[] data)
        {
            ulong h = 1469598103934665603UL;
            foreach (byte b in data) { h ^= b; h *= 1099511628211UL; }
            return h.ToString("x16");
        }

        // ---------------------------------------------------------------- textures and materials

        /// <summary>A decal's picture: built in, or one of the player's (from the design, or the cache on this computer).</summary>
        public static Texture2D Texture(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (textures.TryGetValue(id, out var t)) return t;
            if (id.StartsWith("img:"))
            {
                try
                {
                    string f = Path.Combine(CachePath, id.Substring(4) + ".png");
                    if (File.Exists(f))
                    {
                        t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = id };
                        if (t.LoadImage(File.ReadAllBytes(f))) { textures[id] = t; return t; }
                    }
                }
                catch { }
                return null;   // someone else's picture we haven't been sent
            }
            if (id.StartsWith("face") && int.TryParse(id.Substring(4), out int face))
                return textures[id] = Ardlings.FaceTexture(Mathf.Clamp(face, 0, Ardlings.Faces - 1), Ardlings.PaperColor(0));
            t = Draw(id);
            if (t != null) textures[id] = t;
            return t;
        }

        public static Material Material(string id)
        {
            if (materials.TryGetValue(id, out var m) && m != null) return m;
            var t = Texture(id);
            if (t == null) return null;
            if (shader == null) shader = Resources.Load<Shader>("Shaders/RocketDecal");
            m = new Material(shader) { name = "Decal " + id };
            m.SetTexture("_MainTex", t);
            materials[id] = m;
            return m;
        }

        static Mesh quad;

        /// <summary>Puts a decal on a part's GameObject (in the part's own frame). Returns it, or null if there's nothing to draw.</summary>
        public static GameObject Attach(Transform part, string decal, int layer)
        {
            if (part == null || !Parse(decal, out var s)) return null;
            var mat = Material(s.id);
            if (mat == null) return null;
            if (quad == null)
            {
                quad = new Mesh { name = "Decal" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
                quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quad.RecalculateBounds();
            }
            var go = new GameObject("Decal") { layer = layer };
            go.transform.SetParent(part, false);
            Vector3 n = s.normal.normalized;
            // upright along the part where it can be (the picture's top toward the part's top), then turned by 'spin'
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.forward, n);
            go.transform.localPosition = s.pos + n * 0.012f;
            go.transform.localRotation = Quaternion.AngleAxis(s.spin, n) * Quaternion.LookRotation(-n, up.normalized);
            go.transform.localScale = Vector3.one * s.size;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ---------------------------------------------------------------- the built-in pictures

        static Texture2D Draw(string id)
        {
            var px = new Color[Px * Px];
            Color ink = new Color(0.08f, 0.08f, 0.1f);
            switch (id)
            {
                case "star": Poly(px, Star(5, 60, 25), new Color(1f, 0.84f, 0.2f), ink); break;
                case "ring": Ring(px, 58, 40, new Color(0.85f, 0.18f, 0.16f)); Ring(px, 34, 22, Color.white); break;
                case "logo":
                    Disc(px, 60, new Color(0.15f, 0.35f, 0.75f));
                    Poly(px, new[] { new Vector2(64, 18), new Vector2(104, 64), new Vector2(64, 110), new Vector2(24, 64) }, Color.white, null);
                    break;
                case "stripes":
                    for (int y = 0; y < Px; y++) for (int x = 0; x < Px; x++)
                        if (x > 8 && x < 120 && y > 30 && y < 98) px[y * Px + x] = ((x + y) / 16) % 2 == 0 ? new Color(0.98f, 0.8f, 0.1f) : ink;
                    break;
                case "checker":
                    for (int y = 8; y < 120; y++) for (int x = 8; x < 120; x++) px[y * Px + x] = ((x - 8) / 28 + (y - 8) / 28) % 2 == 0 ? Color.white : ink;
                    break;
                case "bolt": Poly(px, new[] { new Vector2(74, 6), new Vector2(30, 70), new Vector2(60, 70), new Vector2(48, 122), new Vector2(98, 52), new Vector2(68, 52) }, new Color(1f, 0.85f, 0.15f), ink); break;
                case "heart":
                    for (int y = 0; y < Px; y++) for (int x = 0; x < Px; x++)
                    {
                        float u = (x - 64f) / 52f, v = (y - 58f) / 52f;
                        float a = u * u + v * v - 1; if (a * a * a - u * u * v * v * v < 0) px[y * Px + x] = new Color(0.9f, 0.15f, 0.25f);
                    }
                    break;
                case "arrow": Poly(px, new[] { new Vector2(64, 120), new Vector2(112, 70), new Vector2(82, 70), new Vector2(82, 8), new Vector2(46, 8), new Vector2(46, 70), new Vector2(16, 70) }, Color.white, ink); break;
                case "flame":
                    Poly(px, new[] { new Vector2(64, 124), new Vector2(96, 70), new Vector2(92, 30), new Vector2(64, 6), new Vector2(36, 30), new Vector2(32, 70) }, new Color(1f, 0.45f, 0.1f), null);
                    Poly(px, new[] { new Vector2(64, 96), new Vector2(80, 64), new Vector2(76, 36), new Vector2(64, 22), new Vector2(52, 36), new Vector2(48, 64) }, new Color(1f, 0.85f, 0.3f), null);
                    break;
                case "moon":
                    Disc(px, 54, new Color(0.9f, 0.9f, 0.86f));
                    for (int y = 0; y < Px; y++) for (int x = 0; x < Px; x++) { float dx = x - 86, dy = y - 74; if (dx * dx + dy * dy < 46 * 46) px[y * Px + x] = new Color(0, 0, 0, 0); }
                    break;
                case "planet":
                    Disc(px, 40, new Color(0.9f, 0.6f, 0.3f));
                    for (int y = 0; y < Px; y++) for (int x = 0; x < Px; x++)
                    {
                        float u = (x - 64f) / 60f, v = (y - 64f) / 18f, rr = u * u + v * v;
                        bool front = y < 64 || (x - 64f) * (x - 64f) + (y - 64f) * (y - 64f) > 40 * 40;
                        if (rr > 0.72f && rr < 1f && front) px[y * Px + x] = new Color(0.95f, 0.85f, 0.6f);
                    }
                    break;
                case "wings":
                    Poly(px, new[] { new Vector2(64, 70), new Vector2(6, 92), new Vector2(18, 74), new Vector2(4, 66), new Vector2(64, 50) }, Color.white, ink);
                    Poly(px, new[] { new Vector2(64, 70), new Vector2(122, 92), new Vector2(110, 74), new Vector2(124, 66), new Vector2(64, 50) }, Color.white, ink);
                    Disc(px, 16, new Color(0.85f, 0.2f, 0.18f));
                    break;
                default:
                    if (id.Length == 1 && char.IsDigit(id[0])) Digit(px, id[0] - '0');
                    else return null;
                    break;
            }
            var t = new Texture2D(Px, Px, TextureFormat.RGBA32, true) { name = "Decal " + id, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static Vector2[] Star(int points, float outer, float inner)
        {
            var v = new Vector2[points * 2];
            for (int i = 0; i < v.Length; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI / points;
                float r = i % 2 == 0 ? outer : inner;
                v[i] = new Vector2(64 + Mathf.Cos(a) * r, 64 + Mathf.Sin(a) * r);
            }
            return v;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }

        /// <summary>A filled shape with a dark outline (null: none).</summary>
        static void Poly(Color[] px, Vector2[] poly, Color fill, Color? outline)
        {
            for (int y = 0; y < Px; y++)
                for (int x = 0; x < Px; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (Inside(poly, p)) { px[y * Px + x] = fill; continue; }
                    if (outline.HasValue)
                    {
                        bool near = false;
                        for (int k = 0; k < 8 && !near; k++) { float a = k * Mathf.PI / 4; near = Inside(poly, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4f); }
                        if (near) px[y * Px + x] = outline.Value;
                    }
                }
        }

        static void Disc(Color[] px, float r, Color c)
        {
            for (int y = 0; y < Px; y++) for (int x = 0; x < Px; x++) { float dx = x + 0.5f - 64, dy = y + 0.5f - 64; if (dx * dx + dy * dy < r * r) px[y * Px + x] = c; }
        }

        static void Ring(Color[] px, float outer, float inner, Color c)
        {
            for (int y = 0; y < Px; y++) for (int x = 0; x < Px; x++) { float dx = x + 0.5f - 64, dy = y + 0.5f - 64, d = dx * dx + dy * dy; if (d < outer * outer && d >= inner * inner) px[y * Px + x] = c; }
        }

        // a 5x7 figure, each cell 14 px, white with a dark rim
        static readonly string[] Digits =
        {
            "01110100011001110101110011000101110", "00100011000010000100001000010001110", "01110100010000100010001000100011111",
            "11111000100010000010000011000101110", "00010001100101010010111110001000010", "11111100001111000001000011000101110",
            "00110010001000011110100011000101110", "11111000010001000100010000100001000", "01110100011000101110100011000101110",
            "01110100011000101111000010001001100",
        };

        static void Digit(Color[] px, int d)
        {
            string g = Digits[Mathf.Clamp(d, 0, 9)];
            const int cell = 14, ox = (Px - 5 * cell) / 2, oy = (Px - 7 * cell) / 2;
            bool On(int cx, int cy) => cx >= 0 && cx < 5 && cy >= 0 && cy < 7 && g[cy * 5 + cx] == '1';
            for (int y = 0; y < Px; y++)
                for (int x = 0; x < Px; x++)
                {
                    int gx = (x - ox) / cell, gy = 6 - (y - oy) / cell;   // the figure's top row at the top of the picture
                    if (x < ox || y < oy) { gx = -1; }
                    if (On(gx, gy)) { px[y * Px + x] = Color.white; continue; }
                    int rx0 = (x - ox + 4) / cell, rx1 = (x - ox - 4) / cell, ry0 = 6 - (y - oy + 4) / cell, ry1 = 6 - (y - oy - 4) / cell;
                    bool rim = x >= ox - 4 && y >= oy - 4 && (On(rx0, gy) || On(rx1, gy) || On(gx, ry0) || On(gx, ry1));
                    if (rim) px[y * Px + x] = new Color(0.08f, 0.08f, 0.1f);
                }
        }
    }
}
