using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    public static class Mats
    {
        public static Material Lit, Planet, Highlight, Unlit, Additive, Atmosphere, Cloud, Stars, Plume, Sun, Reentry;

        public static void Init()
        {
            if (Lit != null) return;
            var lit = Resources.Load<Shader>("Shaders/RocketLit");
            var unlit = Resources.Load<Shader>("Shaders/RocketUnlit");
            if (lit == null || unlit == null) Debug.LogError("Rocket shaders missing from Resources/Shaders");
            Lit = new Material(lit) { name = "RocketLit" };
            Lit.SetFloat("_Ambient", 0.24f);
            // Downloaded material photos (ambientCG, CC0): foil, bright metal, dark metal, concrete, asphalt
            var detailA = Resources.Load<Texture2DArray>("Textures/PartDetailA");
            var detailN = Resources.Load<Texture2DArray>("Textures/PartDetailN");
            if (detailA == null || detailN == null) Debug.LogError("Part detail textures missing from Resources/Textures");
            Lit.SetTexture("_DetailA", detailA);
            Lit.SetTexture("_DetailN", detailN);
            var planetShader = Resources.Load<Shader>("Shaders/RocketPlanet");
            if (planetShader == null) Debug.LogError("Planet shader missing from Resources/Shaders");
            Planet = new Material(planetShader != null ? planetShader : lit) { name = "RocketPlanet" };
            Planet.SetFloat("_Ambient", 0.035f);
            Highlight = new Material(Lit) { name = "RocketHighlight" };
            Highlight.SetFloat("_Ambient", 0.35f);
            Highlight.SetColor("_Tint", new Color(1f, 0.88f, 0.5f));
            Highlight.SetFloat("_Emission", 0.25f);
            Unlit = new Material(unlit) { name = "RocketUnlit" };
            Unlit.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            Unlit.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            Additive = new Material(unlit) { name = "RocketAdditive" };
            Additive.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            Additive.SetFloat("_DstBlend", (float)BlendMode.One);
            var starShader = Resources.Load<Shader>("Shaders/RocketStars");
            Stars = new Material(starShader != null ? starShader : unlit) { name = "RocketStars" };
            var starMap = Resources.Load<Cubemap>("Sky/StarMap");
            if (starMap == null) Debug.LogError("Star map missing from Resources/Sky");
            Stars.SetTexture("_Stars", starMap);
            Sun = new Material(Additive) { name = "RocketSun", renderQueue = 2450 };
            var plume = Resources.Load<Shader>("Shaders/RocketPlume");
            if (plume == null) Debug.LogError("Plume shader missing from Resources/Shaders");
            Plume = new Material(plume != null ? plume : unlit) { name = "RocketPlume" };
            var reentry = Resources.Load<Shader>("Shaders/RocketReentry");
            Reentry = new Material(reentry != null ? reentry : unlit) { name = "RocketReentry" };
            var atm = Resources.Load<Shader>("Shaders/RocketAtmosphere");
            var cloud = Resources.Load<Shader>("Shaders/RocketCloud");
            if (atm == null || cloud == null) Debug.LogError("Atmosphere/cloud shaders missing from Resources/Shaders");
            Atmosphere = new Material(atm != null ? atm : unlit) { name = "RocketAtmosphere" };
            Atmosphere.SetFloat("_Power", 1.7f);
            Atmosphere.SetFloat("_Strength", 2.2f);
            Cloud = new Material(cloud != null ? cloud : unlit) { name = "RocketCloud" };
        }
    }

    /// <summary>Accumulates vertex-coloured geometry. Front faces wind clockwise (Unity convention).</summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Color> c = new List<Color>();
        public readonly List<int> t = new List<int>();
        readonly List<Vector4> m = new List<Vector4>();
        Vector4 mat = MatPaint;

        // Per-vertex material (UV0): id, metallic, smoothness. See Rocket/Lit.
        public static readonly Vector4 MatPaint = new Vector4(0, 0, 0.5f, 0);
        public static readonly Vector4 MatGloss = new Vector4(0, 0, 0.72f, 0);
        public static readonly Vector4 MatMatte = new Vector4(0, 0, 0.22f, 0);
        public static readonly Vector4 MatFoil = new Vector4(1, 1, 0.42f, 0);
        public static readonly Vector4 MatMetal = new Vector4(2, 1, 0.62f, 0);
        public static readonly Vector4 MatDarkMetal = new Vector4(3, 0.85f, 0.4f, 0);
        public static readonly Vector4 MatConcrete = new Vector4(4, 0, 0.3f, 0);
        public static readonly Vector4 MatAsphalt = new Vector4(5, 0, 0.25f, 0);
        public static readonly Vector4 MatGlass = new Vector4(6, 0, 0.95f, 0);
        public static readonly Vector4 MatLamp = new Vector4(7, 0, 0.6f, 0);
        public static readonly Vector4 MatRubber = new Vector4(0, 0, 0.12f, 0);
        // Milestone 24: buildings and grounds drawn in the shader (see Rocket/Lit)
        public static readonly Vector4 MatPaving = new Vector4(9, 0, 0.22f, 0);
        public static readonly Vector4 MatLawn = new Vector4(10, 0, 0.08f, 0);
        public const float Ribbon = 0f, Punched = 0.33f, Curtain = 0.66f, Cladding = 1f;
        /// <summary>A facade with windows and floors drawn on it: Ribbon, Punched, Curtain or Cladding.</summary>
        public static Vector4 MatFacade(float style) => new Vector4(8, 0, 0.32f, style);

        /// <summary>The material for vertices added from now on.</summary>
        public Vector4 Mat
        {
            get => mat;
            set { FlushMat(); mat = value; }
        }

        void FlushMat() { while (m.Count < v.Count) m.Add(mat); }

        /// <summary>Material data for vertices just copied in from another mesh (pads with the current material if short).</summary>
        public void AddRawUv(List<Vector4> uv)
        {
            int start = v.Count - uv.Count;
            while (m.Count < start) m.Add(mat);
            foreach (var x in uv) m.Add(x);
        }

        /// <summary>Cone section around +Y from yTop (radius rTop) down to yBottom (radius rBottom).</summary>
        /// <summary>One triangle, wound so it faces along 'normal'.</summary>
        public void AddTri(Vector3 a, Vector3 b, Vector3 c2, Vector3 normal, Color col)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c2 - a), normal) < 0) { var t2 = b; b = c2; c2 = t2; }
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c2);
            n.Add(normal); n.Add(normal); n.Add(normal);
            c.Add(col); c.Add(col); c.Add(col);
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c2, Vector3 d, Vector3 normal, Color col)
        {
            AddTri(a, b, c2, normal, col);
            AddTri(a, c2, d, normal, col);
        }

        public void AddFrustum(float yTop, float yBottom, float rTop, float rBottom, Color col, int seg = 24,
            bool capTop = false, bool capBottom = false, bool doubleSided = false, float angFrom = 0f, float angTo = Mathf.PI * 2f)
        {
            float h = Mathf.Max(yTop - yBottom, 1e-4f);
            float slope = (rBottom - rTop) / h;
            int start = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = angFrom + i * (angTo - angFrom) / seg, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                Vector3 nrm = new Vector3(ca, slope, sa).normalized;
                v.Add(new Vector3(ca * rTop, yTop, sa * rTop)); n.Add(nrm); c.Add(col);
                v.Add(new Vector3(ca * rBottom, yBottom, sa * rBottom)); n.Add(nrm); c.Add(col);
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = start + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                t.Add(a0); t.Add(a1); t.Add(b0);
                t.Add(a1); t.Add(b1); t.Add(b0);
            }
            if (doubleSided)
            {
                int s2 = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = angFrom + i * (angTo - angFrom) / seg, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    Vector3 nrm = -new Vector3(ca, slope, sa).normalized;
                    Color dim = col * 0.6f; dim.a = col.a;
                    v.Add(new Vector3(ca * rTop, yTop, sa * rTop)); n.Add(nrm); c.Add(dim);
                    v.Add(new Vector3(ca * rBottom, yBottom, sa * rBottom)); n.Add(nrm); c.Add(dim);
                }
                for (int i = 0; i < seg; i++)
                {
                    int a0 = s2 + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                    t.Add(a0); t.Add(b0); t.Add(a1);
                    t.Add(a1); t.Add(b0); t.Add(b1);
                }
            }
            if (capTop && rTop > 1e-4f) AddDisc(yTop, rTop, col, seg, true);
            if (capBottom && rBottom > 1e-4f) AddDisc(yBottom, rBottom, col, seg, false);
        }

        public void AddDisc(float y, float r, Color col, int seg, bool facingUp)
        {
            int center = v.Count;
            Vector3 nrm = facingUp ? Vector3.up : Vector3.down;
            v.Add(new Vector3(0, y, 0)); n.Add(nrm); c.Add(col);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); n.Add(nrm); c.Add(col);
            }
            for (int i = 0; i < seg; i++)
            {
                int p0 = center + 1 + i, p1 = p0 + 1;
                if (facingUp) { t.Add(center); t.Add(p1); t.Add(p0); }
                else { t.Add(center); t.Add(p0); t.Add(p1); }
            }
        }

        /// <summary>Axis-aligned box (before rotation) centred at 'center'.</summary>
        public void AddBox(Vector3 center, Vector3 size, Color col, Quaternion rotation)
        {
            Vector3 hs = size * 0.5f;
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3[] ups = { Vector3.up, Vector3.up, Vector3.forward, Vector3.forward, Vector3.up, Vector3.up };
            for (int f = 0; f < 6; f++)
            {
                Vector3 N = normals[f], W = ups[f], Rt = Vector3.Cross(N, W);
                Vector3 nOff = Vector3.Scale(N, hs), wOff = Vector3.Scale(W, hs), rOff = Vector3.Scale(Rt, hs);
                nOff = N * Mathf.Abs(Vector3.Dot(nOff, N));
                wOff = W * Mathf.Abs(Vector3.Dot(wOff, W));
                rOff = Rt * Mathf.Abs(Vector3.Dot(rOff, Rt));
                Vector3 c0 = nOff;
                int s = v.Count;
                Vector3[] q = { c0 - rOff + wOff, c0 + rOff + wOff, c0 - rOff - wOff, c0 + rOff - wOff };
                foreach (var p in q) { v.Add(center + rotation * p); n.Add(rotation * N); c.Add(col); }
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
                t.Add(s + 1); t.Add(s + 3); t.Add(s + 2);
            }
        }

        /// <summary>UV sphere (or the top part of one when maxTheta &lt; PI).</summary>
        public void AddSphere(Vector3 center, float r, Color col, int seg = 24, int rings = 12, float maxTheta = Mathf.PI, bool doubleSided = false)
        {
            int start = v.Count;
            for (int i = 0; i <= rings; i++)
            {
                float th = maxTheta * i / rings, st = Mathf.Sin(th), ct = Mathf.Cos(th);
                for (int j = 0; j <= seg; j++)
                {
                    float ph = j * Mathf.PI * 2f / seg;
                    Vector3 d = new Vector3(st * Mathf.Cos(ph), ct, st * Mathf.Sin(ph));
                    v.Add(center + d * r); n.Add(d); c.Add(col);
                }
            }
            int row = seg + 1;
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a0 = start + i * row + j, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    t.Add(a0); t.Add(a1); t.Add(b0);
                    t.Add(a1); t.Add(b1); t.Add(b0);
                    if (doubleSided)
                    {
                        t.Add(a0); t.Add(b0); t.Add(a1);
                        t.Add(a1); t.Add(b0); t.Add(b1);
                    }
                }
        }

        /// <summary>Rotates then moves every vertex added since 'start'.</summary>
        public void Transform(int start, Quaternion r, Vector3 t)
        {
            for (int i = start; i < v.Count; i++) { v[i] = r * v[i] + t; n[i] = r * n[i]; }
        }

        public Mesh ToMesh(string name = "mesh")
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            FlushMat();
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetColors(c);
            mesh.SetUVs(0, this.m);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    public static class Noise
    {
        public static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                int h = seed * 1013 + x * 73856093 ^ y * 19349663 ^ z * 83492791;
                h = (h << 13) ^ h;
                int r = (h * (h * h * 15731 + 789221) + 1376312589) & 0x7fffffff;
                return r / 2147483647f;
            }
        }

        static float Smooth(float t) => t * t * (3 - 2 * t);

        public static float Value(Vector3 p, int seed)
        {
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
            float u = Smooth(p.x - xi), w = Smooth(p.y - yi), s = Smooth(p.z - zi);
            float x00 = Mathf.Lerp(Hash(xi, yi, zi, seed), Hash(xi + 1, yi, zi, seed), u);
            float x10 = Mathf.Lerp(Hash(xi, yi + 1, zi, seed), Hash(xi + 1, yi + 1, zi, seed), u);
            float x01 = Mathf.Lerp(Hash(xi, yi, zi + 1, seed), Hash(xi + 1, yi, zi + 1, seed), u);
            float x11 = Mathf.Lerp(Hash(xi, yi + 1, zi + 1, seed), Hash(xi + 1, yi + 1, zi + 1, seed), u);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, w), Mathf.Lerp(x01, x11, w), s);
        }

        public static float Fbm(Vector3 p, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int k = 0; k < octaves; k++)
            {
                sum += amp * Value(p, seed + k * 31);
                norm += amp;
                p *= 2.03f;
                amp *= 0.5f;
            }
            return sum / norm;
        }
    }

    public static class MeshFactory
    {
        static readonly Dictionary<string, Mesh> partCache = new Dictionary<string, Mesh>();
        static Mesh flame, canopy, explosion;

        // Arden's ground, at realistic brightness (forest reflects ~8% of sunlight, open sea ~5%)
        static readonly Color DeepSea = new Color(0.012f, 0.035f, 0.085f), Shallow = new Color(0.03f, 0.13f, 0.19f);
        static readonly Color Beach = new Color(0.44f, 0.4f, 0.3f), Grass = new Color(0.21f, 0.28f, 0.12f);
        static readonly Color Forest = new Color(0.075f, 0.14f, 0.06f), Highland = new Color(0.33f, 0.29f, 0.22f);
        static readonly Color Rock = new Color(0.44f, 0.42f, 0.40f), Snow = new Color(0.86f, 0.89f, 0.93f);

        /// <summary>
        /// Kinds of ground for the close-up photo textures (milestone 20b), as weights (A, B, C); the world's base ground
        /// takes the rest, and the shader adds bare rock on steep slopes. What A, B and C are depends on the world
        /// (see BodyView.Layers): Arden sand / snow / dry highland dirt, Tal rocky highlands, Vesk and Rime dust / snow,
        /// Ochre dunes / polar ice / canyon gravel, Pyre rough ash.
        /// </summary>
        public static Vector3 GroundWeights(CelestialBody body, Vector3 d, float field = float.NaN)
        {
            int seed = body.seed;
            switch (body.style)
            {
                case BodyStyle.Earthlike:
                {
                    float h = float.IsNaN(field) ? Terrain.ArdenField(body, d) : field;
                    float sand = h < 0.03f ? 1f : Mathf.InverseLerp(0.06f, 0.03f, h);
                    float lat = Mathf.Abs(d.y) + (Noise.Fbm(d * 6f, 3, seed + 9) - 0.5f) * 0.12f;
                    float snow = Mathf.InverseLerp(0.8f, 0.9f, lat);
                    float high = Mathf.InverseLerp(0.22f, 0.42f, h) * (1f - snow);
                    return new Vector3(sand * (1f - snow), snow, high);
                }
                case BodyStyle.Barren:
                {
                    float maria = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.47f, 0.40f, Noise.Fbm(d * 1.3f, 3, seed + 5)));
                    return new Vector3((1f - maria) * 0.65f, 0, 0);
                }
                case BodyStyle.Icy:
                {
                    float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.56f, 0.7f, Noise.Fbm(d * 2.2f, 3, seed + 7)));
                    float frost = Mathf.InverseLerp(0.45f, 0.62f, Noise.Fbm(d * 4f, 3, seed + 21));
                    return new Vector3(patch * 0.8f, frost * (1f - patch), 0);
                }
                case BodyStyle.Desert:
                {
                    float dunes = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.62f, Noise.Fbm(d * 3f, 3, seed + 8)));
                    float lat = Mathf.Abs(d.y) + (Noise.Fbm(d * 5f, 3, seed + 9) - 0.5f) * 0.1f;
                    float ice = Mathf.InverseLerp(0.86f, 0.92f, lat);
                    float canyon = Mathf.Clamp01(Terrain.CanyonDepth(body, d) * 1.4f);
                    return new Vector3(dunes * (1f - ice) * (1f - canyon), ice, canyon * (1f - ice));
                }
                case BodyStyle.Volcanic:
                    return new Vector3(1f - Terrain.LavaFields(body, d), 0, 0);
                default:
                    return Vector3.zero;
            }
        }

        /// <summary>1 over open water (Arden's seas, not frozen near the poles), else 0: the planet shader makes it glint.</summary>
        public static float WaterMask(CelestialBody body, Vector3 d, float field = float.NaN)
        {
            if (body.style != BodyStyle.Earthlike) return 0f;
            float h = float.IsNaN(field) ? Terrain.ArdenField(body, d) : field;
            if (h >= 0f) return 0f;
            float lat = Mathf.Abs(d.y) + (Noise.Fbm(d * 6f, 3, body.seed + 9) - 0.5f) * 0.12f;
            return lat > 0.85f ? 0f : Mathf.InverseLerp(0f, -0.008f, h);
        }

        /// <summary>Surface colour of a body at a body-fixed direction. 'detail' adds fine variation for close-up ground.</summary>
        public static Color BodyColor(CelestialBody body, Vector3 d, float detail = 0f, float field = float.NaN)
        {
            if (body.style == BodyStyle.Barren) return BarrenColor(d, body.seed, detail);
            if (body.style == BodyStyle.Icy) return IcyColor(d, body.seed, detail);
            if (body.style == BodyStyle.Star) { float g = 0.9f + (Noise.Fbm(d * 6f, 3, body.seed) - 0.5f) * 0.25f; return new Color(1f, 0.9f * g, 0.62f * g, 1f); }
            if (body.style == BodyStyle.Desert) return DesertColor(body, d, detail);
            if (body.style == BodyStyle.Gas) return GasColor(body, d, detail);
            if (body.style == BodyStyle.Volcanic) return VolcanicColor(body, d, detail);
            return PlanetColor(body, d, detail, field);
        }

        /// <summary>Brume: cream and tan belts, a rusty storm band, sea-green temperate zones and pale polar haze, all streaked by wind.</summary>
        public static Color GasColor(CelestialBody body, Vector3 d, float detail)
        {
            int seed = body.seed;
            float lat = Regions.BrumeLatitude(body, d);
            Color cream = new Color(0.93f, 0.86f, 0.68f), tan = new Color(0.78f, 0.62f, 0.42f), rust = new Color(0.72f, 0.38f, 0.24f);
            Color sea = new Color(0.52f, 0.66f, 0.6f), haze = new Color(0.78f, 0.82f, 0.86f);
            // Fine stripes inside each band: wind shear stretches the noise east-west.
            float stripe = Mathf.Sin((d.y + (Noise.Fbm(new Vector3(d.x * 2f, d.y * 18f, d.z * 2f), 4, seed + 3) - 0.5f) * 0.06f) * 70f) * 0.5f + 0.5f;
            Color c;
            if (lat < 0.18f) c = Color.Lerp(cream, tan, stripe * 0.5f);
            else if (lat < 0.45f) c = Color.Lerp(rust, tan, stripe * 0.6f);
            else if (lat < 0.8f) c = Color.Lerp(sea, cream, stripe * 0.35f);
            else c = Color.Lerp(haze, sea, (1f - Mathf.InverseLerp(0.8f, 1f, lat)) * 0.4f);
            // A great oval storm in the southern storm band
            Vector3 spot = new Vector3(0.85f, -0.3f, 0.43f).normalized;
            float sd = Vector3.Distance(new Vector3(d.x, d.y * 2.2f, d.z), new Vector3(spot.x, spot.y * 2.2f, spot.z));
            c = Color.Lerp(c, new Color(0.82f, 0.32f, 0.2f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.08f, sd)) * 0.85f);
            if (detail > 0f) c *= 1f + (Noise.Fbm(d * detail, 2, seed + 7) - 0.5f) * 0.12f;
            c.a = 1f;
            return c;
        }

        /// <summary>Pyre: black basalt lava fields with glowing cracks, grey ash plains, red-brown volcano flanks and bright calderas.</summary>
        public static Color VolcanicColor(CelestialBody body, Vector3 d, float detail)
        {
            int seed = body.seed;
            Color basalt = new Color(0.12f, 0.1f, 0.1f), ash = new Color(0.5f, 0.48f, 0.46f), flank = new Color(0.42f, 0.24f, 0.16f), glow = new Color(1f, 0.45f, 0.08f);
            float lava = Terrain.LavaFields(body, d);
            Color c = Color.Lerp(ash, basalt, lava);
            double v = Terrain.VolcanoField(d, 3.2f, 2800, seed + 3, out float caldera);
            c = Color.Lerp(c, flank, Mathf.Clamp01((float)v / 1500f) * 0.8f * (1f - 0.7f * lava));   // fresh lava covers the lower flanks
            float crack = 1f - Mathf.Abs(2f * Noise.Fbm(d * 11f, 3, seed + 8) - 1f);
            c = Color.Lerp(c, glow, Mathf.Pow(crack, 60f) * lava * 0.85f);                            // thin glowing cracks in the lava crust
            c = Color.Lerp(c, glow, caldera * 0.9f);
            if (detail > 0f) c *= 1f + (Noise.Fbm(d * detail, 3, seed + 7) - 0.5f) * 0.3f;
            c.a = 1f;
            return c;
        }

        /// <summary>A flat ring round a gas giant's equator: dusty bands with gaps, see-through, in planet-fixed coordinates.</summary>
        public static Mesh RingMesh(CelestialBody body)
        {
            var b = new MeshBuilder();
            const int seg = 160, bands = 40;
            float r0 = (float)body.ringInner, r1 = (float)body.ringOuter;
            for (int k = 0; k < bands; k++)
            {
                float a = r0 + (r1 - r0) * k / bands, c2 = r0 + (r1 - r0) * (k + 1) / bands, mid = (k + 0.5f) / bands;
                float dens = 0.35f + 0.65f * Noise.Fbm(new Vector3(mid * 9f, 0.3f, 0.7f), 3, body.seed + 31);
                if (mid > 0.62f && mid < 0.67f) dens = 0.04f;               // the big gap
                dens *= Mathf.SmoothStep(0f, 1f, mid * 6f) * Mathf.SmoothStep(0f, 1f, (1f - mid) * 5f);
                Color col = Color.Lerp(new Color(0.8f, 0.72f, 0.6f), new Color(0.62f, 0.56f, 0.5f), Noise.Fbm(new Vector3(mid * 23f, 0.1f, 0.2f), 2, body.seed + 33));
                col.a = Mathf.Clamp01(dens) * 0.75f;
                for (int i = 0; i < seg; i++)
                {
                    float t0 = 2 * Mathf.PI * i / seg, t1 = 2 * Mathf.PI * (i + 1) / seg;
                    Vector3 p00 = new Vector3(Mathf.Cos(t0) * a, 0, Mathf.Sin(t0) * a), p01 = new Vector3(Mathf.Cos(t1) * a, 0, Mathf.Sin(t1) * a);
                    Vector3 p10 = new Vector3(Mathf.Cos(t0) * c2, 0, Mathf.Sin(t0) * c2), p11 = new Vector3(Mathf.Cos(t1) * c2, 0, Mathf.Sin(t1) * c2);
                    b.AddQuad(p00, p10, p11, p01, Vector3.up, col);
                }
            }
            return b.ToMesh("Rings");
        }

        /// <summary>Ochre: rust plains, pale sandy dune seas, dark basalt canyon floors, white polar ice.</summary>
        public static Color DesertColor(CelestialBody body, Vector3 d, float detail)
        {
            int seed = body.seed;
            Color rust = new Color(0.66f, 0.34f, 0.18f), sand = new Color(0.86f, 0.62f, 0.38f), basalt = new Color(0.3f, 0.19f, 0.14f), ice = new Color(0.92f, 0.9f, 0.88f);
            float high = Noise.Fbm(d * 2.0f, 5, seed + 1);
            Color c = Color.Lerp(rust, new Color(0.55f, 0.3f, 0.2f), Mathf.InverseLerp(0.5f, 0.7f, high)); // highlands darker
            float dunes = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.62f, Noise.Fbm(d * 3f, 3, seed + 8)));
            c = Color.Lerp(c, sand, dunes * 0.85f);
            c = Color.Lerp(c, basalt, Mathf.Clamp01(Terrain.CanyonDepth(body, d) * 1.4f));
            float lat = Mathf.Abs(d.y) + (Noise.Fbm(d * 5f, 3, seed + 9) - 0.5f) * 0.1f;
            c = Color.Lerp(c, ice, Mathf.InverseLerp(0.86f, 0.92f, lat));
            if (detail > 0f) c *= 1f + (Noise.Fbm(d * detail, 3, seed + 7) - 0.5f) * 0.3f;
            c.a = 1f;
            return c;
        }

        static Color BarrenColor(Vector3 d, int seed, float detail)
        {
            float n = Noise.Fbm(d * 2.5f, 5, seed);
            float maria = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.47f, 0.40f, Noise.Fbm(d * 1.3f, 3, seed + 5)));
            float g = Mathf.Lerp(0.6f, 0.33f, maria) + (n - 0.5f) * 0.2f;
            float cr = Noise.Fbm(d * 16f, 2, seed + 11);
            if (cr > 0.67f) g += 0.09f;
            else if (cr > 0.63f) g -= 0.05f;
            if (detail > 0f) g *= 1f + (Noise.Fbm(d * detail, 3, seed + 7) - 0.5f) * 0.4f;
            return new Color(g, g * 0.97f, g * 0.93f, 1f);
        }

        /// <summary>Vesk's ice: bright blue-white plains, darker bluish cracks (the same lines that stand up as ridges), tan dust patches.</summary>
        static Color IcyColor(Vector3 d, int seed, float detail)
        {
            Color ice = new Color(0.84f, 0.9f, 0.97f), crackCol = new Color(0.42f, 0.52f, 0.66f), dust = new Color(0.72f, 0.66f, 0.58f);
            float crack = 1f - Mathf.Abs(2f * Noise.Fbm(d * 9f, 3, seed + 2) - 1f);
            Color c = Color.Lerp(ice, crackCol, Mathf.Pow(crack, 8f) * 0.85f);
            float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.56f, 0.7f, Noise.Fbm(d * 2.2f, 3, seed + 7)));
            c = Color.Lerp(c, dust, patch * 0.45f);
            float n = (Noise.Fbm(d * 30f, 2, seed + 11) - 0.5f) * 0.08f;
            if (detail > 0f) n += (Noise.Fbm(d * detail, 3, seed + 13) - 0.5f) * 0.1f;
            c = c * (1f + n);
            c.a = 1f;
            return c;
        }

        public static Color PlanetColor(CelestialBody body, Vector3 d, float detail = 0f, float field = float.NaN)
        {
            int seed = body.seed;
            float h = float.IsNaN(field) ? Terrain.ArdenField(body, d) : field;
            Color col;
            // (milestone 24: soft edges at the waterline and where the sand meets the grass, instead of hard steps)
            if (h < 0f) col = Color.Lerp(Color.Lerp(DeepSea, Shallow, Mathf.InverseLerp(-0.5f, 0f, h)), Beach * 0.8f, Mathf.InverseLerp(-0.006f, 0f, h));
            else if (h < 0.2f) col = Color.Lerp(Beach, Color.Lerp(Grass, Forest, Noise.Fbm(d * 9f, 3, seed + 3)), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.018f, 0.05f, h)));
            else if (h < 0.38f) col = Color.Lerp(Forest, Highland, Mathf.InverseLerp(0.2f, 0.38f, h));
            else col = Color.Lerp(Highland, Rock, Mathf.InverseLerp(0.38f, 0.65f, h));
            float lat = Mathf.Abs(d.y) + (Noise.Fbm(d * 6f, 3, seed + 9) - 0.5f) * 0.12f;
            col = Color.Lerp(col, Snow, Mathf.InverseLerp(0.8f, 0.9f, lat));
            if (detail > 0f && h >= 0f) col *= 1f + (Noise.Fbm(d * detail, 3, seed + 7) - 0.5f) * 0.35f;
            col.a = 1f;
            return col;
        }

        /// <summary>Whole planet as a cube-sphere, in planet-fixed coordinates.</summary>
        public static Mesh PlanetMesh(CelestialBody body, int res)
        {
            var b = new MeshBuilder();
            var uv = new List<Vector4>();
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up, Vector3.down };
            Vector3[] ups = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward, Vector3.forward };
            float R = (float)body.radius;
            for (int f = 0; f < 6; f++)
            {
                Vector3 N = normals[f], W = ups[f], Rt = Vector3.Cross(N, W);
                int start = b.v.Count;
                for (int j = 0; j <= res; j++)
                    for (int i = 0; i <= res; i++)
                    {
                        Vector3 p = N + Rt * (2f * i / res - 1f) + W * (2f * j / res - 1f);
                        Vector3 d = p.normalized;
                        // normals from heights about one grid cell apart, so they match the mesh rather than finer detail
                        float fld = body.style == BodyStyle.Earthlike ? Terrain.ArdenField(body, d) : float.NaN;
                        b.v.Add(d * (R + (float)Terrain.Height(body, Vector3d.FromFloat(d), fld))); b.n.Add(TerrainNormal(body, d, 1.2f * Mathf.PI / 2f / res)); b.c.Add(BodyColor(body, d, 0f, fld));
                        Vector3 gw = GroundWeights(body, d, fld);
                        uv.Add(new Vector4(WaterMask(body, d, fld), gw.x, gw.y, gw.z));
                    }
                int row = res + 1;
                for (int j = 0; j < res; j++)
                    for (int i = 0; i < res; i++)
                    {
                        int bl = start + j * row + i, br = bl + 1, tl = bl + row, tr = tl + 1;
                        b.t.Add(tl); b.t.Add(tr); b.t.Add(bl);
                        b.t.Add(tr); b.t.Add(br); b.t.Add(bl);
                    }
            }
            var mesh = b.ToMesh("Planet");
            mesh.SetUVs(0, uv);
            return mesh;
        }

        public const float CloudAltitude = 5000f;

        /// <summary>Surface normal of the terrain at a direction, from heights a small angle (radians) either side.</summary>
        public static Vector3 TerrainNormal(CelestialBody body, Vector3 d, float step)
        {
            Vector3 t1 = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized, t2 = Vector3.Cross(d, t1);
            double R = body.radius;
            Vector3d P(Vector3 dir) { var dd = Vector3d.FromFloat(dir.normalized); return dd * (R + Terrain.Height(body, dd)); }
            // central differences: symmetric, so no bias toward one side
            Vector3d dx = P(d + t1 * step) - P(d - t1 * step), dy = P(d + t2 * step) - P(d - t2 * step);
            Vector3 n = Vector3d.Cross(dx, dy).normalized.ToFloat();
            return Vector3.Dot(n, d) < 0 ? -n : n;
        }

        /// <summary>Cloud cover (0..1) over a planet-fixed direction: patchy weather, thinning toward the poles' edges.</summary>
        public static float CloudCover(CelestialBody body, Vector3 d)
        {
            float n = Noise.Fbm(d * 3.4f, 5, body.seed + 123);
            float wisp = Noise.Fbm(d * 11f, 3, body.seed + 321);
            float c = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.66f, n + (wisp - 0.5f) * 0.12f));
            return c * 0.92f;
        }

        /// <summary>A cube-sphere shell around a body (planet-fixed coordinates), coloured per direction.</summary>
        public static Mesh Shell(float radius, int res, System.Func<Vector3, Color> color, string name)
        {
            var b = new MeshBuilder();
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up, Vector3.down };
            Vector3[] ups = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward, Vector3.forward };
            for (int f = 0; f < 6; f++)
            {
                Vector3 N = normals[f], W = ups[f], Rt = Vector3.Cross(N, W);
                int start = b.v.Count;
                for (int j = 0; j <= res; j++)
                    for (int i = 0; i <= res; i++)
                    {
                        Vector3 d = (N + Rt * (2f * i / res - 1f) + W * (2f * j / res - 1f)).normalized;
                        b.v.Add(d * radius); b.n.Add(d); b.c.Add(color(d));
                    }
                int row = res + 1;
                for (int j = 0; j < res; j++)
                    for (int i = 0; i < res; i++)
                    {
                        int bl = start + j * row + i, br = bl + 1, tl = bl + row, tr = tl + 1;
                        b.t.Add(tl); b.t.Add(tr); b.t.Add(bl);
                        b.t.Add(tr); b.t.Add(br); b.t.Add(bl);
                    }
            }
            return b.ToMesh(name);
        }

        /// <summary>Stars on a unit sphere: small quads facing the centre, brighter ones bigger, a few tinted.</summary>
        public static Mesh StarField(int count, int seed)
        {
            var rng = new System.Random(seed);
            var b = new MeshBuilder();
            for (int k = 0; k < count; k++)
            {
                Vector3 d;
                do d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1); while (d.sqrMagnitude > 1 || d.sqrMagnitude < 0.01f);
                d.Normalize();
                float m = Mathf.Pow((float)rng.NextDouble(), 3f);           // most stars faint, a few bright
                float size = 0.00045f + 0.0011f * m;
                float t = (float)rng.NextDouble();
                Color c = t < 0.15f ? new Color(0.75f, 0.85f, 1f) : t > 0.88f ? new Color(1f, 0.85f, 0.65f) : Color.white;
                c.a = 0.35f + 0.65f * m;
                Vector3 a1 = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized * size, a2 = Vector3.Cross(d, a1).normalized * size;
                b.AddQuad(d - a1 - a2, d + a1 - a2, d + a1 + a2, d - a1 + a2, -d, c);
            }
            return b.ToMesh("Stars");
        }

        /// <summary>The sun: a bright core fading into a wide glow, as a fan in the XY plane facing -Z (unit radius).</summary>
        public static Mesh SunDisc()
        {
            var b = new MeshBuilder();
            float[] radii = { 0f, 0.07f, 0.12f, 0.3f, 1f };
            Color[] cols = { new Color(1f, 1f, 0.98f, 1f), new Color(1f, 0.99f, 0.94f, 1f), new Color(1f, 0.96f, 0.88f, 0.5f), new Color(1f, 0.93f, 0.8f, 0.1f), new Color(1f, 0.9f, 0.75f, 0f) };
            const int seg = 40;
            for (int r = 0; r + 1 < radii.Length; r++)
                for (int s = 0; s < seg; s++)
                {
                    float a0 = s * Mathf.PI * 2 / seg, a1 = (s + 1) * Mathf.PI * 2 / seg;
                    Vector3 p00 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * radii[r], p01 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * radii[r];
                    Vector3 p10 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * radii[r + 1], p11 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * radii[r + 1];
                    int i0 = b.v.Count;
                    b.v.Add(p00); b.v.Add(p01); b.v.Add(p11); b.v.Add(p10);
                    for (int q = 0; q < 4; q++) b.n.Add(Vector3.back);
                    b.c.Add(cols[r]); b.c.Add(cols[r]); b.c.Add(cols[r + 1]); b.c.Add(cols[r + 1]);
                    b.t.Add(i0); b.t.Add(i0 + 1); b.t.Add(i0 + 2);
                    b.t.Add(i0); b.t.Add(i0 + 2); b.t.Add(i0 + 3);
                }
            return b.ToMesh("Sun");
        }

        /// <summary>
        /// High-detail curved ground patch around a planet-fixed direction. Vertices are relative to
        /// the patch centre so they stay small (good float precision near the vessel). 'clouds' (0..1) paints the
        /// cloud cover onto it as seen from above, so flying over the patch doesn't punch a hole in the clouds.
        /// </summary>
        public static void BuildPatch(Mesh mesh, CelestialBody body, Vector3d centerDir, double half, int res, float clouds = 0f)
        {
            Vector3d c = centerDir.normalized;
            Vector3d e1 = Vector3d.Cross(Vector3d.up, c);
            if (e1.sqrMagnitude < 1e-8) e1 = Vector3d.Cross(Vector3d.right, c);
            e1 = e1.normalized;
            Vector3d e2 = Vector3d.Cross(e1, c).normalized;
            double R = body.radius;
            Vector3d c0 = c * R;
            int row = res + 1;
            var verts = new Vector3[row * row];
            var norms = new Vector3[row * row];
            var cols = new Color[row * row];
            var uvs = new Vector4[row * row];
            float detail = (float)(R / 1800.0);
            for (int j = 0; j <= res; j++)
                for (int i = 0; i <= res; i++)
                {
                    double x = -half + 2 * half * i / res, y = -half + 2 * half * j / res;
                    Vector3d dir = (c0 + e1 * x + e2 * y).normalized;
                    int k = j * row + i;
                    Vector3 df = dir.ToFloat();
                    float fld = body.style == BodyStyle.Earthlike ? Terrain.ArdenField(body, df) : float.NaN;   // worked out once
                    verts[k] = (dir * (R + Terrain.Height(body, dir, fld)) - c0).ToFloat(); // the pad and runway are their own meshes
                    norms[k] = df;
                    cols[k] = BodyColor(body, df, detail, fld);
                    Vector3 gw = GroundWeights(body, df, fld);
                    uvs[k] = new Vector4(WaterMask(body, df, fld), gw.x, gw.y, gw.z);
                    if (clouds > 0.001f) cols[k] = Color.Lerp(cols[k], new Color(0.93f, 0.94f, 0.96f), CloudCover(body, dir.ToFloat()) * clouds);
                }
            var tris = new int[res * res * 6];
            int ti = 0;
            for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    int bl = j * row + i, br = bl + 1, tl = bl + row, tr = tl + 1;
                    tris[ti++] = tl; tris[ti++] = tr; tris[ti++] = bl;
                    tris[ti++] = tr; tris[ti++] = br; tris[ti++] = bl;
                }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.colors = cols;
            mesh.SetUVs(0, uvs);
            mesh.triangles = tris;
            mesh.RecalculateNormals(); // slopes catch the light
            mesh.RecalculateBounds();
        }

        /// <summary>Swept, tapered plate in the local XY plane (X = span, Y = chord), thickness along Z.</summary>
        static void AddWingPlate(MeshBuilder b, PartDef d, Color main, Color edge)
        {
            float span = d.radialSize.x, cr = d.radialSize.y, ct = d.tipChord, th = d.radialSize.z * 0.5f;
            Vector3 rl = new Vector3(0, cr * 0.5f, 0), rt = new Vector3(0, -cr * 0.5f, 0);
            Vector3 tl = new Vector3(span, cr * 0.5f - d.sweep, 0), tt = new Vector3(span, cr * 0.5f - d.sweep - ct, 0);
            Vector3 up = Vector3.forward * th, dn = -up;
            b.AddQuad(rl + up, tl + up, tt + up, rt + up, Vector3.forward, main);
            b.AddQuad(rl + dn, tl + dn, tt + dn, rt + dn, Vector3.back, main);
            Vector3 le = Vector3.Cross(tl - rl, Vector3.forward).normalized; if (le.y < 0) le = -le;
            b.AddQuad(rl + up, tl + up, tl + dn, rl + dn, le, main);
            Vector3 te = -Vector3.Cross(tt - rt, Vector3.forward).normalized; if (te.y > 0) te = -te;
            b.AddQuad(rt + up, tt + up, tt + dn, rt + dn, te, main);
            b.AddQuad(tl + up, tt + up, tt + dn, tl + dn, Vector3.right, edge);
            b.AddQuad(rl + up, rt + up, rt + dn, rl + dn, Vector3.left, main);
        }

        /// <summary>Landing-gear strut: unit length along -Y.</summary>
        public static Mesh GearStrut()
        {
            if (partCache.TryGetValue("__gearstrut", out var m)) return m;
            var b = new MeshBuilder();
            b.Mat = MeshBuilder.MatMetal;
            b.AddFrustum(0, -1f, 0.06f, 0.05f, new Color(0.85f, 0.86f, 0.88f), 10, capBottom: true);
            m = b.ToMesh("GearStrut");
            partCache["__gearstrut"] = m;
            return m;
        }

        public static Mesh Wheel(float radius)
        {
            string key = "__wheel" + radius.ToString("0.00");
            if (partCache.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            b.Mat = MeshBuilder.MatRubber;
            b.AddFrustum(0.12f, -0.12f, radius, radius, new Color(0.12f, 0.12f, 0.13f), 20, capTop: true, capBottom: true);
            b.Mat = MeshBuilder.MatMetal;
            b.AddFrustum(0.13f, -0.13f, radius * 0.5f, radius * 0.5f, new Color(0.8f, 0.82f, 0.85f), 12, capTop: true, capBottom: true);
            b.Transform(0, Quaternion.Euler(90f, 0, 0), Vector3.zero); // axle along Z
            m = b.ToMesh("Wheel");
            partCache[key] = m;
            return m;
        }

        /// <summary>A rover tyre with its axle along X: dark tread blocks (so you can see it turn), a light hub and spokes.</summary>
        public static Mesh RoverTyre(float radius, float width)
        {
            string key = "__tyre" + radius.ToString("0.00") + width.ToString("0.00");
            if (partCache.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            Color rubber = new Color(0.11f, 0.11f, 0.12f), tread = new Color(0.2f, 0.2f, 0.21f), hub = new Color(0.72f, 0.74f, 0.78f);
            b.Mat = MeshBuilder.MatRubber;
            b.AddFrustum(width * 0.5f, -width * 0.5f, radius * 0.92f, radius * 0.92f, rubber, 24, capTop: true, capBottom: true);
            for (int k = 0; k < 12; k++)
            {
                Quaternion q = Quaternion.Euler(0, 30f * k, 0);
                b.AddBox(q * new Vector3(radius * 0.94f, 0, 0), new Vector3(radius * 0.12f, width * 0.96f, radius * 0.2f), tread, q);
            }
            b.AddFrustum(width * 0.52f, -width * 0.52f, radius * 0.45f, radius * 0.45f, hub, 14, capTop: true, capBottom: true);
            for (int k = 0; k < 3; k++)
            {
                Quaternion q = Quaternion.Euler(0, 60f * k, 0);
                b.AddBox(new Vector3(0, width * 0.53f, 0), new Vector3(radius * 1.3f, 0.02f, radius * 0.1f), hub, q);
                b.AddBox(new Vector3(0, -width * 0.53f, 0), new Vector3(radius * 1.3f, 0.02f, radius * 0.1f), hub, q);
            }
            b.Transform(0, Quaternion.Euler(0, 0, -90f), Vector3.zero); // axle from Y to X
            m = b.ToMesh("RoverTyre");
            partCache[key] = m;
            return m;
        }

        /// <summary>Fairing shell rising from the base: cylinder then an ogive nose.</summary>
        public static Mesh FairingShell(PartDef d, int variant)
        {
            string key = "__shell" + d.id + variant;
            if (partCache.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            Color col = d.variants > 0 && variant > 0 ? PartCatalog.Paints[variant % PartCatalog.Paints.Length] : d.color;
            AddShell(b, d.fairingHeight, d.fairingRadius, col, 0f, Mathf.PI * 2f, false, 0f);
            m = b.ToMesh("FairingShell");
            partCache[key] = m;
            return m;
        }

        /// <summary>Shell profile from y = y0 up to y0 + H (or down from the tip when 'fromTip').</summary>
        static void AddShell(MeshBuilder b, float H, float R, Color col, float a0, float a1, bool fromTip, float y0)
        {
            float cyl = H * 0.62f;
            var ys = new List<float> { 0, cyl };
            var rs = new List<float> { R, R };
            for (int i = 1; i <= 6; i++)
            {
                float k = i / 6f;
                ys.Add(cyl + (H - cyl) * k);
                rs.Add(Mathf.Max(0.04f, R * Mathf.Sqrt(1 - k * k)));
            }
            for (int i = 0; i < ys.Count - 1; i++)
            {
                float yb = ys[i], yt = ys[i + 1];
                if (fromTip) { yb -= H; yt -= H; }
                b.AddFrustum(yt + y0, yb + y0, rs[i + 1], rs[i], col, 24, doubleSided: true, angFrom: a0, angTo: a1);
            }
        }

        /// <summary>Leg strut (unit length along -Y) with a foot pad; scaled and aimed at runtime.</summary>
        public static Mesh LegStrut()
        {
            if (partCache.TryGetValue("__strut", out var m)) return m;
            var b = new MeshBuilder();
            b.Mat = MeshBuilder.MatMetal;
            b.AddFrustum(0, -1f, 0.05f, 0.05f, new Color(0.85f, 0.86f, 0.88f), 10);
            int s = b.v.Count;
            b.AddFrustum(0.02f, -0.06f, 0.18f, 0.2f, new Color(0.3f, 0.31f, 0.33f), 16, capTop: true, capBottom: true);
            b.Transform(s, Quaternion.identity, new Vector3(0, -1f, 0));
            m = b.ToMesh("LegStrut");
            partCache["__strut"] = m;
            return m;
        }

        public static Mesh PartMesh(PartDef d, int variant = 0)
        {
            string key = variant > 0 && d.variants > 0 ? d.id + "#" + variant : d.id;
            if (partCache.TryGetValue(key, out var cached)) return cached;
            if (variant > 0 && d.variants > 0)
            {
                Color keep = d.color;
                d.color = PartCatalog.Paints[variant % PartCatalog.Paints.Length];
                partCache.Remove(d.id);
                var painted = PartMesh(d, 0);
                partCache.Remove(d.id);
                d.color = keep;
                partCache[key] = painted;
                return painted;
            }
            var b = new MeshBuilder();
            float L = d.length, rt = d.topDiameter * 0.5f, rb = d.bottomDiameter * 0.5f;
            Color dark = new Color(0.16f, 0.16f, 0.18f);
            Vector3 rs = d.radialSize;
            switch (d.kind)
            {
                case PartKind.Wing:
                    AddWingPlate(b, d, d.color, d.accent);
                    break;
                case PartKind.ControlSurface:
                    AddWingPlate(b, d, d.accent, d.color);
                    break;
                case PartKind.Gear:
                    b.AddBox(new Vector3(rs.x * 0.4f, 0, 0), new Vector3(rs.x * 0.8f, rs.y, rs.z), d.color, Quaternion.identity);
                    break;
                case PartKind.Wheel:
                    // Mounting plate, suspension arm and motor housing; the tyre is its own spinning mesh (RoverTyre).
                    b.AddBox(new Vector3(0.03f, 0, 0), new Vector3(0.06f, d.wheelRadius * 0.9f, d.wheelRadius * 0.9f), d.accent, Quaternion.identity);
                    b.AddBox(new Vector3(d.strutLength * 0.35f, 0, 0), new Vector3(d.strutLength * 0.7f, d.wheelRadius * 0.28f, d.wheelRadius * 0.28f), d.color, Quaternion.identity);
                    {
                        int start = b.v.Count; // motor housing: a short drum along the axle (X), just inside the tyre
                        b.AddFrustum(d.strutLength - rs.x * 0.5f, d.strutLength * 0.55f, d.wheelRadius * 0.32f, d.wheelRadius * 0.32f, d.accent, 14, capTop: true, capBottom: true);
                        b.Transform(start, Quaternion.Euler(0, 0, -90f), Vector3.zero);
                    }
                    break;
                case PartKind.Frame:
                    // Flat box chassis (the stack runs through its thin middle), with side rails and a dark deck.
                    b.AddBox(new Vector3(0, -L * 0.5f, 0), new Vector3(rt * 2f, L, 2.0f), d.color, Quaternion.identity);
                    b.AddBox(new Vector3(0, -L * 0.15f, 0), new Vector3(rt * 2f - 0.1f, 0.03f, 1.9f), dark, Quaternion.identity);
                    foreach (float sx in new[] { -1f, 1f })
                        b.AddBox(new Vector3(sx * (rt + 0.02f), -L * 0.5f, 0), new Vector3(0.06f, L * 0.6f, 2.02f), d.accent, Quaternion.identity);
                    b.AddBox(new Vector3(0, -L * 0.5f, 1.01f), new Vector3(rt * 1.4f, L * 0.4f, 0.04f), d.accent, Quaternion.identity); // front bumper
                    break;
                case PartKind.Fairing:
                    b.AddFrustum(0, -L, rt, rb, d.accent, 32, capTop: true, capBottom: true);
                    b.AddFrustum(0.02f, -0.06f, d.fairingRadius, d.fairingRadius, d.color, 32, capBottom: true);
                    break;
                case PartKind.FairingHalf:
                    AddShell(b, d.fairingHeight, d.fairingRadius, d.color, -Mathf.PI / 2f, Mathf.PI / 2f, true, 0f);
                    break;
                case PartKind.DockingPort:
                    // Body, with a yellow latch ring and a dark face plate at each end (either end can dock).
                    b.AddFrustum(-0.04f, -L + 0.04f, rt * 0.86f, rb * 0.86f, d.color, 28);
                    b.AddFrustum(0, -0.05f, rt, rt, d.accent, 28, capTop: true);
                    b.AddFrustum(-L + 0.05f, -L, rb, rb, d.accent, 28, capBottom: true);
                    b.Mat = MeshBuilder.MatDarkMetal;
                    b.AddFrustum(0.006f, 0.0f, rt * 0.55f, rt * 0.55f, dark * 3f, 20, capTop: true);
                    b.AddFrustum(-L, -L - 0.006f, rb * 0.55f, rb * 0.55f, dark * 3f, 20, capBottom: true);
                    b.Mat = MeshBuilder.MatPaint;
                    for (int k = 0; k < 3; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 120 * k, 0);
                        b.AddBox(q * new Vector3(rt * 0.72f, 0.03f, 0), new Vector3(0.08f, 0.06f, 0.12f), d.accent, q);
                    }
                    break;
                case PartKind.Grabber:
                    // A squat body with a collar, an open ring at the top and three claws reaching past it.
                    b.AddFrustum(-0.05f, -L, rt * 0.8f, rb, d.color, 28, capBottom: true);
                    b.AddFrustum(-L * 0.55f, -L * 0.75f, rb * 1.03f, rb * 1.03f, d.accent, 28);
                    b.AddFrustum(0, -0.08f, rt * 0.92f, rt * 0.92f, dark, 28, capTop: true);
                    for (int k = 0; k < 3; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 120 * k + 60, 0);
                        Quaternion lean = q * Quaternion.Euler(0, 0, -24);
                        b.AddBox(q * new Vector3(rt * 0.82f, 0.12f, 0), new Vector3(0.12f, 0.55f, 0.18f), d.accent, lean);
                        Quaternion tipLean = q * Quaternion.Euler(0, 0, 30);
                        b.AddBox(q * new Vector3(rt * 0.95f, 0.46f, 0), new Vector3(0.1f, 0.3f, 0.14f), dark, tipLean);
                    }
                    break;
                case PartKind.Rock:
                    AddRock(b, L * 0.5f, d);
                    break;
                case PartKind.Habitat when d.id == "base.lab":
                {
                    // Research Lab: a drum with a long window band, instrument racks outside and a small dish
                    Color glass = new Color(0.1f, 0.16f, 0.24f), dk = new Color(0.32f, 0.33f, 0.35f);
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddFrustum(0, -L, rt, rb, d.color, 36, capTop: true, capBottom: true);
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddFrustum(-0.03f, -0.16f, rt * 1.015f, rt * 1.015f, d.accent, 36);
                    b.AddFrustum(-L + 0.16f, -L + 0.03f, rb * 1.015f, rb * 1.015f, d.accent, 36);
                    b.Mat = MeshBuilder.MatGlass;
                    b.AddFrustum(-L * 0.36f, -L * 0.52f, rt * 1.006f, rt * 1.006f, glass, 36, angFrom: 0.3f, angTo: Mathf.PI * 1.7f);
                    b.Mat = MeshBuilder.MatDarkMetal;
                    for (int k = 0; k < 3; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 200f + 40f * k, 0);
                        b.AddBox(q * new Vector3(0, -L * 0.72f, rt * 1.02f), new Vector3(0.36f, 0.5f, 0.12f), dk * 1.6f, q);
                    }
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddBox(new Vector3(rt * 0.55f, 0.35f, 0), new Vector3(0.06f, 0.7f, 0.06f), new Color(0.82f, 0.83f, 0.85f), Quaternion.identity);
                    {
                        int ds = b.v.Count;
                        b.Mat = MeshBuilder.MatPaint;
                        b.AddSphere(Vector3.zero, 0.42f, Color.white, 20, 5, Mathf.PI * 0.35f, doubleSided: true);
                        for (int i = ds; i < b.v.Count; i++) b.v[i] = new Vector3(b.v[i].x, -b.v[i].y + 0.42f * Mathf.Cos(Mathf.PI * 0.35f), b.v[i].z);
                        b.Transform(ds, Quaternion.Euler(-30f, 0, 25f), new Vector3(rt * 0.55f, 0.72f, 0));
                    }
                    b.Mat = MeshBuilder.MatLamp;
                    b.AddBox(new Vector3(0, -L * 0.25f, -rt * 1.0f), new Vector3(0.05f, 0.05f, 0.03f), new Color(0.4f, 0.8f, 1f), Quaternion.identity);
                    b.Mat = MeshBuilder.MatPaint;
                    break;
                }
                case PartKind.Habitat:
                    b.AddFrustum(0, -L, rt, rb, d.color, 32, capTop: true, capBottom: true);
                    b.AddFrustum(-0.03f, -0.16f, rt * 1.015f, rt * 1.015f, d.accent, 32);
                    b.AddFrustum(-L + 0.16f, -L + 0.03f, rb * 1.015f, rb * 1.015f, d.accent, 32);
                    for (int k = 0; k < 6; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 60 * k, 0);
                        b.Mat = MeshBuilder.MatGlass;
                        b.AddBox(q * new Vector3(0, -L * 0.5f, rt * 1.0f), new Vector3(0.22f, 0.3f, 0.04f), new Color(0.1f, 0.18f, 0.28f), q);
                        b.Mat = MeshBuilder.MatPaint;
                    }
                    break;
                case PartKind.Solar:
                    b.AddBox(new Vector3(rs.x * 0.5f, 0, 0), rs, d.accent, Quaternion.identity);
                    b.AddBox(new Vector3(rs.x * 0.75f, 0, 0), new Vector3(rs.x * 0.5f, rs.y * 0.9f, rs.z * 0.9f), d.color, Quaternion.identity);
                    break;
                case PartKind.Antenna:
                    b.AddBox(new Vector3(rs.x * 0.5f, 0, 0), rs, d.color, Quaternion.identity);
                    break;
                case PartKind.Battery when d.radial:
                    b.AddBox(new Vector3(rs.x * 0.5f, 0, 0), rs, d.color, Quaternion.identity);
                    b.AddBox(new Vector3(rs.x * 0.55f, rs.y * 0.3f, 0), new Vector3(rs.x * 1.02f, rs.y * 0.12f, rs.z * 1.02f), d.accent, Quaternion.identity);
                    break;
                case PartKind.Battery:
                    b.AddFrustum(0, -L, rt, rb, d.color, 32, capTop: true, capBottom: true);
                    b.AddFrustum(-L * 0.3f, -L * 0.45f, rt * 1.015f, rt * 1.015f, d.accent, 32);
                    b.AddFrustum(-L * 0.55f, -L * 0.7f, rt * 1.015f, rt * 1.015f, d.accent, 32);
                    break;
                case PartKind.Science:
                    b.AddBox(new Vector3(rs.x * 0.5f, 0, 0), rs, d.color, Quaternion.identity);
                    b.AddBox(new Vector3(rs.x * 0.75f, rs.y * 0.3f, 0), new Vector3(rs.x * 0.55f, rs.y * 0.25f, rs.z * 1.05f), d.accent, Quaternion.identity);
                    break;
                case PartKind.Eva:
                    return Ardlings.Body(Ardlings.LookFor("Ardling"));
                case PartKind.Flag:
                {
                    // Pole along -Y from the top; the banner flies toward +X. An original emblem: a white diamond on a teal field with an orange band.
                    b.AddFrustum(0, -L, rt, rt, new Color(0.8f, 0.8f, 0.82f), 10, capTop: true, capBottom: true);
                    b.AddSphere(new Vector3(0, 0.03f, 0), 0.05f, new Color(0.9f, 0.85f, 0.5f), 10, 6);
                    b.AddBox(new Vector3(0.5f, -0.33f, 0), new Vector3(0.9f, 0.56f, 0.015f), d.color, Quaternion.identity);
                    b.AddBox(new Vector3(0.5f, -0.48f, 0), new Vector3(0.9f, 0.1f, 0.02f), d.accent, Quaternion.identity);
                    b.AddBox(new Vector3(0.5f, -0.28f, 0), new Vector3(0.2f, 0.2f, 0.025f), Color.white, Quaternion.Euler(0, 0, 45));
                    b.AddFrustum(-L + 0.05f, -L, 0.18f, 0.2f, new Color(0.5f, 0.5f, 0.52f), 12, capTop: true);
                    break;
                }
                case PartKind.Scanner:
                    b.AddBox(new Vector3(rs.x * 0.5f, 0, 0), rs, d.color, Quaternion.identity);
                    b.AddBox(new Vector3(rs.x * 0.55f, -rs.y * 0.3f, 0), new Vector3(rs.x * 1.04f, rs.y * 0.18f, rs.z * 1.04f), d.accent, Quaternion.identity);
                    b.AddBox(new Vector3(rs.x * 1.0f, rs.y * 0.15f, 0), new Vector3(0.04f, rs.y * 0.3f, rs.z * 0.6f), dark, Quaternion.identity);
                    break;
                case PartKind.Drill:
                {
                    // Gearbox housing with hazard stripes; the bit itself is the part that folds down (Deployable).
                    b.AddBox(new Vector3(rs.x * 0.5f, rs.y * 0.1f, 0), new Vector3(rs.x, rs.y * 0.8f, rs.z), d.color, Quaternion.identity);
                    for (int k = 0; k < 3; k++)
                        b.AddBox(new Vector3(rs.x * 0.5f, rs.y * (0.35f - k * 0.22f), 0), new Vector3(rs.x * 1.02f, rs.y * 0.07f, rs.z * 1.02f), d.accent, Quaternion.identity);
                    int s = b.v.Count;
                    b.AddFrustum(0, -rs.y * 0.2f, rs.x * 0.32f, rs.x * 0.26f, new Color(0.35f, 0.35f, 0.37f), 16, capBottom: true);
                    b.Transform(s, Quaternion.identity, new Vector3(rs.x * 0.5f, -rs.y * 0.3f, 0));
                    break;
                }
                case PartKind.OreTank when d.radial:
                {
                    int s = b.v.Count;
                    b.AddFrustum(rs.y * 0.5f, -rs.y * 0.5f, rs.x * 0.45f, rs.x * 0.45f, d.color, 20, capTop: true, capBottom: true);
                    b.AddFrustum(rs.y * 0.3f, rs.y * 0.18f, rs.x * 0.47f, rs.x * 0.47f, d.accent, 20);
                    b.AddFrustum(-rs.y * 0.18f, -rs.y * 0.3f, rs.x * 0.47f, rs.x * 0.47f, d.accent, 20);
                    b.Transform(s, Quaternion.identity, new Vector3(rs.x * 0.5f, 0, 0));
                    break;
                }
                case PartKind.OreTank:
                    b.AddFrustum(0, -L, rt, rb, d.color, 32, capTop: true, capBottom: true);
                    b.AddFrustum(-0.02f, -0.14f, rt * 1.015f, rt * 1.015f, d.accent, 32);
                    b.AddFrustum(-L + 0.14f, -L + 0.02f, rb * 1.015f, rb * 1.015f, d.accent, 32);
                    for (int k = 0; k < 4; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 90 * k, 0);
                        b.AddBox(q * new Vector3(0, -L * 0.5f, rt), new Vector3(rt * 0.5f, L * 0.45f, 0.05f), d.accent * 0.8f + Color.black * 0.2f, q);
                    }
                    break;
                case PartKind.Converter:
                    b.AddFrustum(0, -L, rt * 0.92f, rb * 0.92f, d.color, 28, capTop: true, capBottom: true);
                    b.AddFrustum(0, -0.12f, rt, rt, d.accent, 28, capTop: true);
                    b.AddFrustum(-L + 0.12f, -L, rb, rb, d.accent, 28, capBottom: true);
                    for (int k = 0; k < 6; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 60 * k + 30, 0);
                        b.AddBox(q * new Vector3(0, -L * 0.5f, rt * 0.95f), new Vector3(0.07f, L * 0.8f, 0.07f), new Color(0.72f, 0.7f, 0.66f), q);
                        if (k % 2 == 0) b.AddBox(q * new Vector3(0, -L * 0.5f, rt * 0.9f), new Vector3(0.3f, 0.28f, 0.12f), dark, q);
                    }
                    break;
                case PartKind.Radiator:
                    b.AddBox(new Vector3(rs.x * 0.5f, 0, 0), rs, d.noDeploy ? d.color : d.accent, Quaternion.identity);
                    if (d.noDeploy)
                        for (int k = 0; k < 6; k++)
                            b.AddBox(new Vector3(rs.x * 0.9f, -rs.y * 0.42f + k * rs.y * 0.168f, 0), new Vector3(rs.x * 0.5f, 0.025f, rs.z * 1.02f), d.accent, Quaternion.identity);
                    break;
                case PartKind.Command when d.id == "base.hub":
                {
                    // Base Hub: a squat drum under a low dome, a ring of windows, a hatch, a docking collar on top and four feet
                    float r = rt;
                    Color metal = new Color(0.82f, 0.83f, 0.85f), glass = new Color(0.1f, 0.16f, 0.24f);
                    b.Mat = MeshBuilder.MatGloss;
                    int ds = b.v.Count;
                    b.AddSphere(Vector3.zero, 1f, d.color, 36, 10, Mathf.PI * 0.5f);
                    for (int i = ds; i < b.v.Count; i++) { b.v[i] = Vector3.Scale(b.v[i], new Vector3(r, 0.5f, r)) + new Vector3(0, -0.5f, 0); b.n[i] = new Vector3(b.n[i].x / r, b.n[i].y / 0.5f, b.n[i].z / r).normalized; }
                    b.AddFrustum(-0.5f, -L + 0.32f, r, r, d.color, 36);
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddFrustum(-L + 0.32f, -L, r * 1.03f, r * 1.05f, d.accent, 36, capBottom: true);
                    b.AddFrustum(-0.56f, -0.66f, r * 1.006f, r * 1.006f, d.accent, 36);
                    b.Mat = MeshBuilder.MatGlass;
                    for (int k = 0; k < 8; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 45f * k + 22.5f, 0);
                        b.AddBox(q * new Vector3(0, -1.0f, r * 0.995f), new Vector3(0.42f, 0.3f, 0.05f), glass, q);
                    }
                    b.Mat = MeshBuilder.MatDarkMetal;
                    b.AddBox(new Vector3(r * 0.99f, -1.18f, 0), new Vector3(0.06f, 0.95f, 0.75f), new Color(0.5f, 0.51f, 0.53f), Quaternion.identity);  // hatch
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddFrustum(0.08f, -0.02f, 0.62f, 0.66f, metal, 28, capTop: true);                                                          // docking collar
                    for (int k = 0; k < 4; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 90f * k + 45f, 0);
                        b.AddBox(q * new Vector3(0, -L + 0.2f, r * 1.12f), new Vector3(0.14f, 0.7f, 0.14f), metal, q * Quaternion.Euler(-25f, 0, 0));
                        int fs = b.v.Count;
                        b.AddFrustum(-L + 0.06f, -L, 0.3f, 0.32f, metal, 14, capTop: true, capBottom: true);
                        b.Transform(fs, Quaternion.identity, q * new Vector3(0, 0, r * 1.3f));
                    }
                    b.Mat = MeshBuilder.MatLamp;
                    b.AddSphere(new Vector3(0, -0.02f, 0) + Quaternion.Euler(0, 20f, 0) * new Vector3(0, 0, r * 0.7f), 0.06f, new Color(1f, 0.8f, 0.3f), 8, 5);
                    b.Mat = MeshBuilder.MatPaint;
                    break;
                }
                case PartKind.Command when !d.stackTop:
                {
                    const int segs = 7;
                    for (int i = 0; i < segs; i++)
                    {
                        float t0 = (float)i / segs, t1 = (float)(i + 1) / segs;
                        float r0 = Mathf.Lerp(rt, rb, Mathf.Sqrt(t0)), r1 = Mathf.Lerp(rt, rb, Mathf.Sqrt(t1));
                        b.AddFrustum(-L * t0, -L * t1, r0, r1, d.color, 28, capTop: i == 0, capBottom: i == segs - 1);
                    }
                    int s = b.v.Count;
                    b.AddSphere(Vector3.zero, 1f, d.accent, 16, 6, Mathf.PI * 0.5f);
                    b.Transform(s, Quaternion.Euler(90f, 0, 0), Vector3.zero);
                    for (int i = s; i < b.v.Count; i++) b.v[i] = Vector3.Scale(b.v[i], new Vector3(0.38f, 0.75f, 0.3f)) + new Vector3(0, -L * 0.62f, rb * 0.72f);
                    break;
                }
                case PartKind.Engine when d.IsJet:
                    b.AddFrustum(0, -L * 0.75f, rt, rb * 0.92f, d.color, 28, capTop: true);
                    b.AddFrustum(-L * 0.15f, -L * 0.25f, rt * 1.02f, rt * 1.02f, d.accent, 28);
                    b.Mat = MeshBuilder.MatDarkMetal;
                    b.AddFrustum(-L * 0.75f, -L, rb * 0.92f, rb * 0.62f, new Color(0.55f, 0.54f, 0.55f), 28, capBottom: true);
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddFrustum(-L + 0.02f, -L - 0.01f, rb * 0.5f, rb * 0.5f, new Color(0.08f, 0.08f, 0.08f), 20, capBottom: true);
                    break;
                case PartKind.RadialDecoupler:
                    b.AddBox(new Vector3(rs.x * 0.5f, 0, 0), rs, d.color, Quaternion.identity);
                    b.AddBox(new Vector3(rs.x * 0.55f, 0, 0), new Vector3(rs.x * 0.2f, rs.y * 1.02f, rs.z * 1.02f), d.accent, Quaternion.identity);
                    break;
                case PartKind.Rcs:
                    b.AddBox(new Vector3(rs.x * 0.35f, 0, 0), new Vector3(rs.x * 0.7f, rs.y * 0.7f, rs.z * 0.7f), d.color, Quaternion.identity);
                    foreach (var dir in new[] { Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                        b.AddBox(new Vector3(rs.x * 0.6f, 0, 0) + dir * rs.y * 0.42f, new Vector3(0.07f, 0.07f, 0.07f) + new Vector3(0, Mathf.Abs(dir.y), Mathf.Abs(dir.z)) * 0.08f, d.accent, Quaternion.identity);
                    break;
                case PartKind.MonoTank when d.radial:
                {
                    int s = b.v.Count;
                    b.AddFrustum(rs.y * 0.5f, -rs.y * 0.5f, rs.x * 0.45f, rs.x * 0.45f, d.color, 20, capTop: true, capBottom: true);
                    b.AddFrustum(0.06f, -0.06f, rs.x * 0.47f, rs.x * 0.47f, d.accent, 20);
                    b.Transform(s, Quaternion.identity, new Vector3(rs.x * 0.5f, 0, 0));
                    break;
                }
                case PartKind.MonoTank:
                    b.AddFrustum(0, -L, rt, rb, d.color, 32, capTop: true, capBottom: true);
                    b.AddFrustum(-L * 0.4f, -L * 0.6f, rt * 1.015f, rb * 1.015f, d.accent, 32);
                    break;
                case PartKind.Leg:
                    b.AddBox(new Vector3(rs.x * 0.5f, rs.y * 0.15f, 0), new Vector3(rs.x, rs.y * 0.7f, rs.z), d.color, Quaternion.identity);
                    b.AddBox(new Vector3(rs.x * 0.8f, rs.y * 0.35f, 0), new Vector3(rs.x * 0.4f, 0.12f, rs.z * 1.3f), d.accent, Quaternion.identity);
                    break;
                case PartKind.Light:
                    b.AddBox(new Vector3(rs.x * 0.4f, 0, 0), new Vector3(rs.x * 0.8f, rs.y, rs.z), d.color, Quaternion.identity);
                    b.AddBox(new Vector3(rs.x * 0.85f, 0, 0), new Vector3(rs.x * 0.15f, rs.y * 0.8f, rs.z * 0.8f), new Color(1f, 0.95f, 0.75f), Quaternion.identity);
                    break;
                case PartKind.EscapeTower:
                    b.AddFrustum(0, -0.7f, 0.04f, 0.12f, d.color, 16, capTop: true);
                    b.AddFrustum(-0.7f, -1.9f, 0.16f, 0.16f, d.color, 20, capBottom: true);
                    for (int k = 0; k < 4; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 45 + 90 * k, 0);
                        b.AddBox(q * new Vector3(0.2f, -1.9f, 0), new Vector3(0.14f, 0.12f, 0.08f), d.accent, q * Quaternion.Euler(0, 0, -35));
                        b.AddBox(q * new Vector3(0.22f, -2.55f, 0), new Vector3(0.05f, 1.3f, 0.05f), d.accent, q * Quaternion.Euler(0, 0, 14));
                    }
                    b.AddFrustum(-L + 0.12f, -L, rb, rb, d.accent, 24, capTop: true, capBottom: true);
                    break;
                case PartKind.Fins when d.radial:
                {
                    int s = b.v.Count;
                    b.AddBox(new Vector3(rs.x * 0.5f, -rs.y * 0.1f, 0), new Vector3(rs.x, rs.y * 0.8f, rs.z), d.color, Quaternion.identity);
                    b.AddBox(new Vector3(rs.x * 0.3f, rs.y * 0.3f, 0), new Vector3(rs.x * 0.6f, rs.y * 0.4f, rs.z), d.color, Quaternion.Euler(0, 0, -20));
                    b.AddBox(new Vector3(rs.x * 0.96f, -rs.y * 0.1f, 0), new Vector3(rs.x * 0.08f, rs.y * 0.8f, rs.z * 1.2f), d.accent, Quaternion.identity);
                    break;
                }
                case PartKind.Command when d.crew == 0:
                {
                    // Probe core: a squat drum with an equipment band, vents, a status light and stub antennas.
                    Color band = Color.Lerp(d.color, Color.black, 0.45f), metal = new Color(0.62f, 0.63f, 0.65f);
                    b.AddFrustum(0, -L, rt * 0.97f, rb * 0.97f, d.color, 28, capTop: true, capBottom: true);
                    b.Mat = MeshBuilder.MatFoil;
                    b.AddFrustum(-L * 0.28f, -L * 0.72f, rt * 0.985f, rb * 0.985f, Color.white, 28);
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddFrustum(0, -0.035f, rt, rt, d.accent, 28);
                    b.AddFrustum(-L + 0.035f, -L, rb, rb, d.accent, 28);
                    for (int k = 0; k < 8; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 45f * k + 22.5f, 0);
                        b.AddBox(q * new Vector3(0, -L * 0.5f, rt * 0.99f), new Vector3(0.11f, L * 0.26f, 0.025f), Color.Lerp(band, Color.black, 0.4f), q);
                    }
                    b.Mat = MeshBuilder.MatLamp;
                    b.AddBox(new Vector3(rt * 0.99f, -L * 0.5f, 0), new Vector3(0.03f, 0.06f, 0.06f), new Color(0.4f, 1f, 0.55f), Quaternion.identity);
                    b.Mat = MeshBuilder.MatMetal;
                    foreach (int side in new[] { -1, 1 })
                        b.AddBox(new Vector3(side * rt * 0.55f, 0.13f, 0), new Vector3(0.025f, 0.26f, 0.025f), metal, Quaternion.identity);
                    b.Mat = MeshBuilder.MatPaint;
                    if (d.sasTier >= 4) b.AddSphere(new Vector3(0, 0, 0), rt * 0.3f, d.accent, 16, 6, Mathf.PI * 0.5f);
                    break;
                }
                case PartKind.Command:
                {
                    // Capsule: shell, heat-shield ring, top collar, framed window, hatch, RCS ports and panel seams.
                    float shellBottom = Mathf.Lerp(rt, rb, (L - 0.12f) / L);
                    b.AddFrustum(0, -L + 0.12f, rt, shellBottom, d.color, 32, capTop: true);
                    b.Mat = MeshBuilder.MatMatte;
                    b.AddFrustum(-L + 0.12f, -L, shellBottom, rb, d.accent, 32, capBottom: true);
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddFrustum(0.06f, -0.04f, rt * 0.86f, rt * 0.92f, new Color(0.8f, 0.81f, 0.83f), 24, capTop: true);
                    b.Mat = MeshBuilder.MatGloss;
                    Color seam = Color.Lerp(d.color, Color.black, 0.22f);
                    foreach (float f in new[] { 0.3f, 0.62f })
                    {
                        float y = -L * f, rr = Mathf.Lerp(rt, rb, f) * 1.004f;
                        b.AddFrustum(y + 0.01f, y - 0.01f, rr, rr, seam, 32);
                    }
                    float tilt = Mathf.Atan2(rb - rt, L) * Mathf.Rad2Deg;
                    float wy = -L * 0.42f, wr = Mathf.Lerp(rt, rb, 0.42f);
                    b.Mat = MeshBuilder.MatDarkMetal;
                    b.AddBox(new Vector3(0, wy, wr * 0.995f), new Vector3(0.36f, 0.27f, 0.035f), new Color(0.6f, 0.61f, 0.64f), Quaternion.Euler(-tilt, 0, 0));
                    b.Mat = MeshBuilder.MatGlass;
                    b.AddBox(new Vector3(0, wy, wr * 1.01f), new Vector3(0.28f, 0.2f, 0.04f), new Color(0.1f, 0.18f, 0.28f), Quaternion.Euler(-tilt, 0, 0));
                    b.Mat = MeshBuilder.MatGloss;
                    // hatch on the left side
                    float hy = -L * 0.55f, hr = Mathf.Lerp(rt, rb, 0.55f);
                    Quaternion hq = Quaternion.Euler(0, -90f, 0) * Quaternion.Euler(-tilt, 0, 0);
                    b.AddBox(Quaternion.Euler(0, -90f, 0) * new Vector3(0, hy, hr * 1.003f), new Vector3(0.42f, 0.5f, 0.02f), seam, hq);
                    b.AddBox(Quaternion.Euler(0, -90f, 0) * new Vector3(0.12f, hy, hr * 1.012f), new Vector3(0.06f, 0.1f, 0.03f), new Color(0.5f, 0.5f, 0.52f), hq);
                    // RCS ports, each with a little cluster of nozzles (milestone 25)
                    for (int k = 0; k < 4; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 45f + 90f * k, 0);
                        float py = -L * 0.18f, pr = Mathf.Lerp(rt, rb, 0.18f);
                        b.AddBox(q * new Vector3(0, py, pr * 1.006f), new Vector3(0.12f, 0.12f, 0.025f), new Color(0.3f, 0.3f, 0.32f), q * Quaternion.Euler(-tilt, 0, 0));
                        b.Mat = MeshBuilder.MatDarkMetal;
                        foreach (var (nx, ny) in new[] { (-0.03f, 0f), (0.03f, 0f), (0f, 0.03f) })
                        {
                            int s0 = b.v.Count;
                            b.AddFrustum(0.035f, 0f, 0.008f, 0.016f, new Color(0.18f, 0.18f, 0.19f), 8);
                            b.Transform(s0, q * Quaternion.Euler(90f, 0, 0), q * new Vector3(nx, py + ny, pr * 1.02f));
                        }
                        b.Mat = MeshBuilder.MatGloss;
                    }
                    if (d.crew > 1)
                        foreach (int side in new[] { -1, 1 })
                        {
                            Quaternion q = Quaternion.Euler(0, 38f * side, 0);
                            b.Mat = MeshBuilder.MatGlass;
                            b.AddBox(q * new Vector3(0, wy + 0.06f, wr * 1.008f), new Vector3(0.13f, 0.13f, 0.03f), new Color(0.1f, 0.18f, 0.28f), q * Quaternion.Euler(-tilt, 0, 0));
                            b.Mat = MeshBuilder.MatGloss;
                        }
                    b.Mat = MeshBuilder.MatMetal;
                    Bar(b, new Vector3(rt * 0.5f, 0.02f, -rt * 0.3f), new Vector3(rt * 0.5f, 0.32f, -rt * 0.3f), 0.02f, new Color(0.8f, 0.81f, 0.83f));
                    b.Mat = MeshBuilder.MatPaint;
                    break;
                }
                case PartKind.HeatShield:
                    b.AddFrustum(0, -L * 0.4f, rt, rt, d.accent, 32, capTop: true);
                    b.AddFrustum(-L * 0.4f, -L, rb, rb * 0.92f, d.color, 32, capBottom: true);
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddFrustum(-L * 0.36f, -L * 0.46f, rb * 1.012f, rb * 1.012f, new Color(0.55f, 0.56f, 0.58f), 32);
                    BoltRing(b, -L * 0.41f, rb * 1.025f, rb > 1f ? 32 : 16, 0.024f, new Color(0.75f, 0.76f, 0.78f));
                    b.Mat = MeshBuilder.MatPaint;
                    break;
                case PartKind.Parachute:
                    b.AddFrustum(0, -L, rt, rb, d.color, 24, capTop: true, capBottom: true);
                    b.AddFrustum(-L * 0.7f, -L, rb * 1.02f, rb * 1.02f, d.accent, 24);
                    b.Mat = MeshBuilder.MatMatte;
                    for (int k = 0; k < 4; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 90f * k + 45f, 0);
                        float rm = Mathf.Lerp(rt, rb, 0.35f) * 1.02f;
                        b.AddBox(q * new Vector3(0, -L * 0.35f, rm), new Vector3(0.05f, L * 0.7f, 0.012f), new Color(0.85f, 0.75f, 0.2f), q * Quaternion.Euler(-Mathf.Atan2(rb - rt, L) * Mathf.Rad2Deg, 0, 0));
                    }
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddFrustum(0.02f, -0.04f, rt * 1.03f, rt * 1.03f, new Color(0.7f, 0.71f, 0.73f), 24);
                    b.Mat = MeshBuilder.MatPaint;
                    break;
                case PartKind.NoseCone:
                {
                    const int segs = 7;
                    for (int i = 0; i < segs; i++)
                    {
                        float t0 = (float)i / segs, t1 = (float)(i + 1) / segs;
                        float r0 = rb * Mathf.Sqrt(1 - (1 - t0) * (1 - t0)), r1 = rb * Mathf.Sqrt(1 - (1 - t1) * (1 - t1));
                        b.AddFrustum(-L * t0, -L * t1, Mathf.Max(r0, 0.02f), r1, i == segs - 1 ? d.accent : d.color, 24, capTop: i == 0);
                    }
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddFrustum(0.12f, -0.01f, 0.004f, 0.03f, new Color(0.75f, 0.76f, 0.78f), 10);   // a little tip
                    b.Mat = MeshBuilder.MatPaint;
                    break;
                }
                case PartKind.Tank:
                {
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddFrustum(0, -L, rt, rb, d.color, 32, capTop: true, capBottom: true);
                    b.AddFrustum(-0.02f, -0.12f, rt * 1.015f, rt * 1.015f, d.accent, 32);
                    b.AddFrustum(-L + 0.12f, -L + 0.02f, rb * 1.015f, rb * 1.015f, d.accent, 32);
                    if (L > 2f) b.AddFrustum(-L * 0.5f + 0.04f, -L * 0.5f - 0.04f, rt * 1.01f, rt * 1.01f, d.accent, 32);
                    // Panel seams, stringers and a feed line down one side.
                    Color seam = Color.Lerp(d.color, Color.black, 0.2f), stringer = Color.Lerp(d.color, Color.black, 0.1f);
                    int panels = Mathf.Max(2, Mathf.RoundToInt(L / 0.75f));
                    for (int i = 1; i < panels; i++)
                    {
                        float y = -L * i / panels;
                        if (Mathf.Abs(y + L * 0.5f) < 0.06f && L > 2f) continue;
                        b.AddFrustum(y + 0.008f, y - 0.008f, rt * 1.004f, rt * 1.004f, seam, 32);
                    }
                    int strings = rt > 1f ? 16 : 8;
                    for (int k = 0; k < strings; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 360f / strings * (k + 0.5f), 0);
                        b.AddBox(q * new Vector3(0, -L * 0.5f, rt * 1.002f), new Vector3(0.03f, L - 0.26f, 0.018f), stringer, q);
                    }
                    if (L > 1.5f)
                    {
                        Quaternion q = Quaternion.Euler(0, 90f, 0);
                        b.Mat = MeshBuilder.MatMetal;
                        b.AddBox(q * new Vector3(0, -L * 0.5f, rt + 0.045f), new Vector3(0.075f, L - 0.18f, 0.075f), new Color(0.85f, 0.86f, 0.88f), q);
                        for (float yy = -0.3f; yy > -L + 0.25f; yy -= 0.6f) b.AddBox(q * new Vector3(0, yy, rt + 0.03f), new Vector3(0.12f, 0.035f, 0.07f), new Color(0.55f, 0.56f, 0.58f), q);
                    }
                    // milestone 25: bolt rings on the end bands, a cable raceway with clamps, a vent valve near the top
                    b.Mat = MeshBuilder.MatMetal;
                    Color bolt = new Color(0.78f, 0.79f, 0.81f);
                    BoltRing(b, -0.07f, rt * 1.022f, rt > 1f ? 36 : 18, 0.022f, bolt);
                    BoltRing(b, -L + 0.07f, rb * 1.022f, rb > 1f ? 36 : 18, 0.022f, bolt);
                    if (L > 1f)
                    {
                        Quaternion q = Quaternion.Euler(0, -90f, 0);
                        Color race = Color.Lerp(d.color, Color.black, 0.3f);
                        b.Mat = MeshBuilder.MatPaint;
                        b.AddBox(q * new Vector3(0, -L * 0.5f, rt + 0.028f), new Vector3(0.13f, L - 0.3f, 0.05f), race, q);
                        for (float yy = -0.4f; yy > -L + 0.3f; yy -= 0.5f) b.AddBox(q * new Vector3(0, yy, rt + 0.04f), new Vector3(0.16f, 0.04f, 0.06f), Color.Lerp(race, Color.black, 0.3f), q);
                    }
                    b.Mat = MeshBuilder.MatMetal;
                    {
                        Quaternion q = Quaternion.Euler(0, 200f, 0);
                        b.AddBox(q * new Vector3(0, -0.2f, rt * 1.01f), new Vector3(0.09f, 0.09f, 0.07f), new Color(0.6f, 0.61f, 0.63f), q);
                        b.AddBox(q * new Vector3(0, -0.2f, rt * 1.01f + 0.05f), new Vector3(0.05f, 0.05f, 0.05f), new Color(0.75f, 0.2f, 0.15f), q);
                    }
                    b.Mat = MeshBuilder.MatPaint;
                    break;
                }
                case PartKind.Engine:
                {
                    // Thrust plate, turbopumps, combustion chamber and a curved bell with cooling bands.
                    float r = rt;
                    Color metal = new Color(0.8f, 0.8f, 0.82f), bell = new Color(0.30f, 0.29f, 0.30f), copper = new Color(1f, 0.72f, 0.52f);
                    b.AddFrustum(0, -0.06f, r, r * 0.98f, d.accent, 28, capTop: true, capBottom: true);
                    b.AddFrustum(-0.06f, -L * 0.3f, r * 0.62f, r * 0.5f, d.accent, 24, capBottom: true);
                    b.Mat = MeshBuilder.MatMetal;
                    for (int k = 0; k < 3; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 120f * k + 30f, 0);
                        b.AddBox(q * new Vector3(0, -L * 0.18f, r * 0.68f), new Vector3(r * 0.32f, L * 0.2f, r * 0.3f), metal, q);
                        b.AddBox(q * new Vector3(0, -L * 0.33f, r * 0.42f), new Vector3(0.05f, L * 0.14f, 0.05f), new Color(0.6f, 0.6f, 0.62f), q * Quaternion.Euler(-25f, 0, 0));
                    }
                    b.AddFrustum(-L * 0.3f, -L * 0.42f, r * 0.34f, r * 0.27f, dark, 20, capTop: true);
                    AddBell(b, -L * 0.42f, -L, r * 0.25f, r * 0.85f, bell);
                    for (int k = 1; k <= 3; k++)
                    {
                        float t = k / 4f, y = Mathf.Lerp(-L * 0.42f, -L, t), rr = Mathf.Lerp(r * 0.25f, r * 0.85f, Mathf.Pow(t, 0.6f)) * 1.012f;
                        b.AddFrustum(y + 0.012f, y - 0.012f, rr, rr, metal, 28);
                    }
                    b.AddFrustum(-L + 0.05f, -L, r * 0.84f, r * 0.86f, copper, 28);
                    // milestone 25: two gimbal actuators, propellant lines from the pumps to the chamber, bolts on the plate
                    Color act = new Color(0.55f, 0.56f, 0.58f);
                    foreach (float ang in new[] { 75f, 165f })
                    {
                        Quaternion q = Quaternion.Euler(0, ang, 0);
                        Vector3 top = q * new Vector3(0, -0.07f, r * 0.58f), bot = q * new Vector3(0, -L * 0.48f, r * 0.31f);
                        Bar(b, top, bot, 0.045f, metal);
                        Bar(b, top + (bot - top) * 0.08f, top + (bot - top) * 0.5f, 0.085f, act);
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 120f * k + 30f, 0);
                        Vector3 pump = q * new Vector3(0, -L * 0.27f, r * 0.62f), elbow = q * new Vector3(0, -L * 0.34f, r * 0.48f), chamber = q * new Vector3(0, -L * 0.36f, r * 0.33f);
                        Bar(b, pump, elbow, 0.055f, new Color(0.7f, 0.7f, 0.72f));
                        Bar(b, elbow, chamber, 0.055f, new Color(0.7f, 0.7f, 0.72f));
                    }
                    BoltRing(b, -0.03f, r * 0.995f, r > 1f ? 30 : 16, 0.03f, new Color(0.7f, 0.71f, 0.73f));
                    b.Mat = MeshBuilder.MatPaint;
                    break;
                }
                case PartKind.Solid:
                {
                    b.AddFrustum(0, -L + 0.45f, rt, rt, d.color, 32, capTop: true, capBottom: true);
                    b.AddFrustum(-0.25f, -0.55f, rt * 1.01f, rt * 1.01f, d.accent, 32);
                    b.AddFrustum(-L * 0.5f + 0.15f, -L * 0.5f - 0.15f, rt * 1.01f, rt * 1.01f, d.accent, 32);
                    Color seam = Color.Lerp(d.color, Color.black, 0.25f);
                    foreach (float f in new[] { 0.3f, 0.7f })
                    {
                        float y = -(L - 0.45f) * f;
                        b.AddFrustum(y + 0.05f, y - 0.05f, rt * 1.012f, rt * 1.012f, seam, 32);
                        b.Mat = MeshBuilder.MatMetal;
                        BoltRing(b, y, rt * 1.03f, rt > 1f ? 32 : 16, 0.028f, new Color(0.72f, 0.73f, 0.75f));
                        b.Mat = MeshBuilder.MatPaint;
                    }
                    b.Mat = MeshBuilder.MatMetal;
                    foreach (float f in new[] { 0.12f, 0.82f })   // attach lugs on one side
                        b.AddBox(new Vector3(0, -L * f, rt + 0.06f), new Vector3(0.22f, 0.16f, 0.14f), new Color(0.6f, 0.61f, 0.63f), Quaternion.identity);
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddFrustum(-L + 0.45f, -L + 0.3f, rt * 0.9f, rt * 0.4f, dark, 24, capTop: true);
                    AddBell(b, -L + 0.3f, -L, rt * 0.3f, rt * 0.62f, dark, 5);
                    break;
                }
                case PartKind.Decoupler:
                {
                    b.AddFrustum(0, -L, rt, rb, d.color, 32, capTop: true, capBottom: true);
                    b.AddFrustum(-L * 0.35f, -L * 0.65f, rt * 1.02f, rb * 1.02f, d.accent, 32);
                    int bolts = rt > 1f ? 24 : 12;
                    for (int k = 0; k < bolts; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 360f / bolts * k, 0);
                        b.Mat = MeshBuilder.MatMetal;
                        b.AddBox(q * new Vector3(0, -L * 0.5f, rt * 1.03f), new Vector3(0.035f, 0.035f, 0.025f), new Color(0.9f, 0.9f, 0.92f), q);
                    }
                    b.Mat = MeshBuilder.MatPaint;
                    break;
                }
                case PartKind.Adapter:
                    b.AddFrustum(0, -L, rt, rb, d.color, 32, capTop: true, capBottom: true);
                    b.AddFrustum(-L + 0.1f, -L, rb * 1.01f, rb * 1.01f, d.accent, 32);
                    break;
                case PartKind.Fins:
                    b.AddFrustum(0, -L, rt, rb, d.color, 32, capTop: true, capBottom: true);
                    for (int k = 0; k < 4; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 45 + 90 * k, 0);
                        b.AddBox(q * new Vector3(rt + 0.36f, -L * 0.55f, 0), Vector3.zero + new Vector3(0.72f, L * 0.9f, 0.05f), d.accent, q);
                        b.Mat = MeshBuilder.MatMetal;
                        b.AddBox(q * new Vector3(rt + 0.36f, -L * 0.1f + 0.01f, 0), new Vector3(0.72f, 0.04f, 0.065f), new Color(0.72f, 0.73f, 0.75f), q);   // leading edge
                        b.Mat = MeshBuilder.MatPaint;
                        b.AddBox(q * new Vector3(rt + 0.03f, -L * 0.55f, 0), new Vector3(0.08f, L * 0.9f, 0.14f), Color.Lerp(d.accent, Color.black, 0.25f), q); // root fillet
                    }
                    break;
                default:
                    b.AddFrustum(0, -L, rt, rb, d.color, 24, true, true);
                    break;
            }
            var m = b.ToMesh(d.id);
            partCache[d.id] = m;
            return m;
        }

        /// <summary>A square bar (pipe, strut, actuator) of thickness t from a to c.</summary>
        static void Bar(MeshBuilder b, Vector3 a, Vector3 c, float t, Color col)
        {
            Vector3 d = c - a;
            if (d.sqrMagnitude < 1e-8f) return;
            b.AddBox((a + c) * 0.5f, new Vector3(t, d.magnitude, t), col, Quaternion.FromToRotation(Vector3.up, d.normalized));
        }

        /// <summary>A ring of bolt heads round the part at height y, radius r.</summary>
        static void BoltRing(MeshBuilder b, float y, float r, int count, float size, Color col)
        {
            for (int k = 0; k < count; k++)
            {
                Quaternion q = Quaternion.Euler(0, 360f / count * (k + 0.5f), 0);
                b.AddBox(q * new Vector3(0, y, r), new Vector3(size, size, size * 0.7f), col, q);
            }
        }

        /// <summary>A lumpy, flat-shaded ball of rock of about this radius, centred one radius below the top node.</summary>
        static void AddRock(MeshBuilder b, float R, PartDef d)
        {
            int seed = 7;
            foreach (char ch in d.id) seed = seed * 31 + ch;
            var rng = new System.Random(seed & 0x7fffffff);
            var bumps = new List<(Vector3 dir, float size, float width)>();
            for (int i = 0; i < 14; i++)
            {
                var dir = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
                bumps.Add((dir, ((float)rng.NextDouble() - 0.45f) * 0.35f, 2f + (float)rng.NextDouble() * 5f));
            }
            Vector3 stretch = new Vector3(1f + (float)rng.NextDouble() * 0.25f, 0.85f + (float)rng.NextDouble() * 0.15f, 0.9f + (float)rng.NextDouble() * 0.2f);
            Vector3 Surf(float lat, float lon)
            {
                var u = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                float r = 1f;
                foreach (var (dir, size, width) in bumps) r += size * Mathf.Exp(-width * (1f - Vector3.Dot(u, dir)));
                return Vector3.Scale(u, stretch) * R * r + new Vector3(0, -R, 0);
            }
            const int nLat = 12, nLon = 20;
            Vector3 c0 = new Vector3(0, -R, 0);
            for (int i = 0; i < nLat; i++)
            {
                float la0 = -Mathf.PI / 2 + Mathf.PI * i / nLat, la1 = -Mathf.PI / 2 + Mathf.PI * (i + 1) / nLat;
                for (int j = 0; j < nLon; j++)
                {
                    float lo0 = 2 * Mathf.PI * j / nLon, lo1 = 2 * Mathf.PI * (j + 1) / nLon;
                    Vector3 p00 = Surf(la0, lo0), p01 = Surf(la0, lo1), p10 = Surf(la1, lo0), p11 = Surf(la1, lo1);
                    Color col = Color.Lerp(d.color, d.accent, (float)rng.NextDouble() * 0.7f);
                    Vector3 n1 = Vector3.Cross(p10 - p00, p11 - p00).normalized;
                    if (Vector3.Dot(n1, (p00 + p10 + p11) / 3f - c0) < 0) n1 = -n1;
                    if (i < nLat - 1) b.AddTri(p00, p10, p11, n1, col);
                    Vector3 n2 = Vector3.Cross(p11 - p00, p01 - p00).normalized;
                    if (Vector3.Dot(n2, (p00 + p11 + p01) / 3f - c0) < 0) n2 = -n2;
                    if (i > 0) b.AddTri(p00, p11, p01, n2, col);   // the bottom row's second triangle would be a sliver at the pole
                }
            }
        }

        static void AddBell(MeshBuilder b, float yTop, float yBottom, float rThroat, float rExit, Color col, int sections = 7)
        {
            var keep = b.Mat;
            b.Mat = MeshBuilder.MatDarkMetal;
            for (int i = 0; i < sections; i++)
            {
                float t0 = (float)i / sections, t1 = (float)(i + 1) / sections;
                float r0 = Mathf.Lerp(rThroat, rExit, Mathf.Pow(t0, 0.6f)), r1 = Mathf.Lerp(rThroat, rExit, Mathf.Pow(t1, 0.6f));
                b.AddFrustum(Mathf.Lerp(yTop, yBottom, t0), Mathf.Lerp(yTop, yBottom, t1), r0, r1, col * 1.9f, 28, doubleSided: true);
            }
            b.Mat = keep;
        }

        /// <summary>The part of a solar panel or antenna that unfolds outward (+X), starting at its housing.</summary>
        public static Mesh Deployable(PartDef d)
        {
            string key = "deploy:" + d.id;
            if (partCache.TryGetValue(key, out var cached)) return cached;
            var b = new MeshBuilder();
            if (d.kind == PartKind.Solar)
            {
                bool big = d.solarRate > 2;
                float len = big ? 4.6f : 2.4f, h = big ? 1.5f : 0.9f;
                int cells = big ? 6 : 4;
                b.Mat = new Vector4(6, 0, 0.88f, 0);
                b.AddBox(new Vector3(len * 0.5f, 0, 0), new Vector3(len, h, 0.03f), d.color * 2.2f, Quaternion.identity);
                b.Mat = MeshBuilder.MatMetal;
                for (int k = 1; k < cells; k++) b.AddBox(new Vector3(len * k / cells, 0, 0), new Vector3(0.025f, h * 1.02f, 0.04f), d.accent, Quaternion.identity);
                b.AddBox(new Vector3(len * 0.5f, 0, 0), new Vector3(len, 0.03f, 0.045f), d.accent, Quaternion.identity);
            }
            else if (d.kind == PartKind.Drill)
            {
                // The bit hangs down from the housing (local -Y); the view stretches it along Y as it deploys.
                float r = d.radialSize.x * 0.18f, reach = d.drillReach;
                Color steel = new Color(0.85f, 0.86f, 0.88f), dk = new Color(0.4f, 0.4f, 0.42f);
                b.Mat = MeshBuilder.MatMetal;
                b.AddFrustum(0, -reach * 0.85f, r, r, steel, 12);
                b.AddFrustum(-reach * 0.85f, -reach, r, 0.02f, dk, 12);
                for (int k = 0; k < 8; k++)
                    b.AddBox(new Vector3(0, -reach * (0.08f + k * 0.1f), 0), new Vector3(r * 2.6f, 0.035f, r * 0.7f), dk, Quaternion.Euler(0, k * 45f, 0));
            }
            else if (d.kind == PartKind.Radiator)
            {
                float len = 1.9f, h = d.radialSize.y * 0.95f;
                b.AddBox(new Vector3(len * 0.5f, 0, 0), new Vector3(len, h, 0.025f), d.color, Quaternion.identity);
                for (int k = 0; k < 7; k++) b.AddBox(new Vector3(len * 0.5f, -h * 0.42f + k * h * 0.14f, 0), new Vector3(len, 0.02f, 0.05f), d.accent, Quaternion.identity);
                b.AddBox(new Vector3(0.05f, 0, 0), new Vector3(0.1f, h * 0.3f, 0.06f), d.accent, Quaternion.identity);
            }
            else if (d.kind == PartKind.Scanner)
            {
                b.AddBox(new Vector3(0.4f, 0, 0), new Vector3(0.8f, 0.04f, 0.04f), d.accent, Quaternion.identity);
                b.AddBox(new Vector3(0.85f, 0, 0), new Vector3(0.14f, 0.24f, 0.32f), d.color, Quaternion.identity);
                b.AddBox(new Vector3(0.93f, 0, 0), new Vector3(0.03f, 0.2f, 0.26f), new Color(0.12f, 0.2f, 0.32f), Quaternion.identity);
            }
            else if (d.relay)
            {
                b.AddBox(new Vector3(0.3f, 0, 0), new Vector3(0.6f, 0.05f, 0.05f), d.accent, Quaternion.identity);
                int s = b.v.Count;
                b.AddFrustum(0.14f, 0f, 0.6f, 0.07f, d.color, 24, doubleSided: true);
                b.Transform(s, Quaternion.Euler(0, 0, -90f), new Vector3(0.6f, 0, 0));
                b.AddBox(new Vector3(0.95f, 0, 0), new Vector3(0.04f, 0.04f, 0.04f), d.accent, Quaternion.identity);
            }
            else
            {
                b.AddBox(new Vector3(0.6f, 0, 0), new Vector3(1.2f, 0.03f, 0.03f), d.color, Quaternion.identity);
                b.AddBox(new Vector3(1.2f, 0, 0), new Vector3(0.06f, 0.06f, 0.06f), d.accent, Quaternion.identity);
            }
            var m = b.ToMesh(key);
            partCache[key] = m;
            return m;
        }

        /// <summary>Unit flame: wide at the nozzle (y=0), tapering to y=-1.</summary>
        public static Mesh Flame()
        {
            if (flame != null) return flame;
            // A unit cylinder in rings (the plume shader shapes it), outer sheath (r = 0) and hot core (r = 1)
            var b = new MeshBuilder();
            const int rings = 18;
            for (int layer = 0; layer < 2; layer++)
            {
                Color col = new Color(layer, 0, 0, 1);
                for (int i = 0; i < rings; i++)
                    b.AddFrustum(-(float)i / rings, -(float)(i + 1) / rings, 1f, 1f, col, 24);
            }
            flame = b.ToMesh("Flame");
            return flame;
        }

        static Mesh starSphere;

        /// <summary>Unit sphere for the star map (seen from inside).</summary>
        public static Mesh StarSphere()
        {
            if (starSphere != null) return starSphere;
            var b = new MeshBuilder();
            b.AddSphere(Vector3.zero, 1f, Color.white, 64, 32);
            starSphere = b.ToMesh("StarSphere");
            return starSphere;
        }

        /// <summary>Unit parachute canopy: dome over y = 0..1.</summary>
        public static Mesh Canopy()
        {
            if (canopy != null) return canopy;
            var b = new MeshBuilder();
            b.AddSphere(Vector3.zero, 1f, new Color(0.95f, 0.5f, 0.2f), 20, 8, Mathf.PI * 0.42f, doubleSided: true);
            canopy = b.ToMesh("Canopy");
            return canopy;
        }

        static Mesh plasma, navballMesh;

        /// <summary>Reentry glow: wide at the leading face (y=0), streaming back to y=-1.</summary>
        public static Mesh Plasma()
        {
            if (plasma != null) return plasma;
            // Milestone 23 (Rocket/Reentry): a shock cap cupping the leading face (r = 0), an outer sheath streaming back
            // (r = 1) and a hotter inner core (r = 2), all finely divided so the shader can ripple them
            var b = new MeshBuilder();
            int s0 = b.v.Count;
            b.AddSphere(Vector3.zero, 1f, new Color(0, 0, 0, 1), 40, 10, Mathf.PI * 0.42f);
            for (int i = s0; i < b.v.Count; i++) { var q = b.v[i]; b.v[i] = new Vector3(q.x * 1.12f, q.y * 0.35f - 0.02f, q.z * 1.12f); }
            const int rings = 24;
            foreach (var (layer, r0, r1, len) in new[] { (1f, 1.12f, 0.45f, 1f), (2f, 0.85f, 0.2f, 0.6f) })
                for (int i = 0; i < rings; i++)
                {
                    float t0 = (float)i / rings, t1 = (float)(i + 1) / rings;
                    float a0 = Mathf.Lerp(r0, r1, Mathf.Pow(t0, 0.8f)), a1 = Mathf.Lerp(r0, r1, Mathf.Pow(t1, 0.8f));
                    b.AddFrustum(-t0 * len, -t1 * len, a0, a1, new Color(layer, 0, 0, 1), 32);
                }
            plasma = b.ToMesh("Plasma");
            return plasma;
        }

        /// <summary>Unit UV sphere for the navball. uv = (phi / 2pi, 1 - theta / pi).</summary>
        public static Mesh NavballMesh()
        {
            if (navballMesh != null) return navballMesh;
            const int seg = 64, rings = 32;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float th = Mathf.PI * i / rings;
                for (int j = 0; j <= seg; j++)
                {
                    float ph = 2f * Mathf.PI * j / seg;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v.Add(d); n.Add(d); uv.Add(new Vector2((float)j / seg, 1f - (float)i / rings));
                }
            }
            int row = seg + 1;
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a0 = i * row + j, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    t.Add(a0); t.Add(a1); t.Add(b0);
                    t.Add(a1); t.Add(b1); t.Add(b0);
                }
            navballMesh = new Mesh { name = "Navball" };
            navballMesh.SetVertices(v); navballMesh.SetNormals(n); navballMesh.SetUVs(0, uv); navballMesh.SetTriangles(t, 0);
            navballMesh.RecalculateBounds();
            return navballMesh;
        }

        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            {'0', new[]{"111","101","101","101","111"}}, {'1', new[]{"010","110","010","010","111"}},
            {'2', new[]{"111","001","111","100","111"}}, {'3', new[]{"111","001","111","001","111"}},
            {'4', new[]{"101","101","111","001","001"}}, {'5', new[]{"111","100","111","001","111"}},
            {'6', new[]{"111","100","111","101","111"}}, {'7', new[]{"111","001","001","001","001"}},
            {'8', new[]{"111","101","111","101","111"}}, {'9', new[]{"111","101","111","001","111"}},
            {'N', new[]{"101","111","111","101","101"}}, {'E', new[]{"111","100","111","100","111"}},
            {'S', new[]{"111","100","111","001","111"}}, {'W', new[]{"101","101","111","111","101"}},
        };

        /// <summary>
        /// Navball skin: sky above the horizon, ground below, a pitch ladder every 10 deg, heading lines every 30 deg,
        /// and labels. Ball-local axes: +X east, +Y up, +Z south.
        /// </summary>
        public static Texture2D NavballTexture()
        {
            const int W = 1024, H = 512;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[W * H];
            var labels = new List<(string s, float h, float p)>();
            for (int k = 0; k < 12; k++)
            {
                int hdg = k * 30;
                string s = hdg == 0 ? "N" : hdg == 90 ? "E" : hdg == 180 ? "S" : hdg == 270 ? "W" : (hdg / 10).ToString();
                labels.Add((s, hdg, 6f));
                labels.Add((s, hdg, -6f));
            }
            foreach (int p in new[] { -60, -30, 30, 60 })
                foreach (int h in new[] { 45, 135, 225, 315 })
                    labels.Add((Mathf.Abs(p).ToString(), h, p));
            const float cell = 1.5f;
            Color sky = new Color(0.22f, 0.5f, 0.82f), skyHi = new Color(0.12f, 0.3f, 0.6f);
            Color ground = new Color(0.56f, 0.36f, 0.2f), groundLo = new Color(0.36f, 0.22f, 0.12f);
            for (int j = 0; j < H; j++)
            {
                float th = (1f - (j + 0.5f) / H) * Mathf.PI;
                for (int i = 0; i < W; i++)
                {
                    float ph = (i + 0.5f) / W * 2f * Mathf.PI;
                    float x = Mathf.Sin(th) * Mathf.Cos(ph), y = Mathf.Cos(th), z = Mathf.Sin(th) * Mathf.Sin(ph);
                    float pitch = Mathf.Asin(Mathf.Clamp(y, -1f, 1f)) * Mathf.Rad2Deg;
                    float hdg = Mathf.Atan2(x, -z) * Mathf.Rad2Deg;
                    if (hdg < 0) hdg += 360f;
                    Color c = pitch >= 0 ? Color.Lerp(sky, skyHi, pitch / 90f) : Color.Lerp(ground, groundLo, -pitch / 90f);
                    float cosP = Mathf.Max(0.05f, Mathf.Cos(pitch * Mathf.Deg2Rad));
                    if (Mathf.Abs(pitch) < 0.7f) c = Color.white;
                    else if (Mathf.Abs(pitch) < 85f)
                    {
                        float pr = Mathf.Abs(pitch - Mathf.Round(pitch / 10f) * 10f);
                        float hr = Mathf.Abs(Mathf.DeltaAngle(hdg, Mathf.Round(hdg / 30f) * 30f)) * cosP;
                        float halfW = Mathf.Abs(Mathf.Round(pitch / 10f) * 10f) % 30 == 0 ? 0.35f : 0.2f;
                        if (pr < halfW && Mathf.Abs(Mathf.DeltaAngle(hdg, Mathf.Round(hdg / 90f) * 90f)) * cosP < 14f) c = Color.Lerp(c, Color.white, 0.75f);
                        if (hr < 0.25f) c = Color.Lerp(c, Color.white, 0.45f);
                    }
                    foreach (var lb in labels)
                    {
                        float dy = (lb.p - pitch) / cell + 2.5f;
                        if (dy < 0 || dy >= 5) continue;
                        float gw = lb.s.Length * 4 - 1;
                        float dx = Mathf.DeltaAngle(lb.h, hdg) * cosP / cell + gw / 2f;
                        if (dx < 0 || dx >= gw) continue;
                        int ci = (int)(dx / 4), col = (int)dx % 4, rowI = (int)dy;
                        if (col < 3 && Glyphs.TryGetValue(lb.s[ci], out var g) && g[rowI][col] == '1') c = Color.white;
                    }
                    px[j * W + i] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        public enum MarkerKind { Prograde, Retrograde, Normal, AntiNormal, RadialOut, RadialIn, Maneuver, Target, AntiTarget }

        public static Color MarkerColor(MarkerKind k)
        {
            switch (k)
            {
                case MarkerKind.Prograde: case MarkerKind.Retrograde: return new Color(1f, 0.85f, 0.15f);
                case MarkerKind.Normal: case MarkerKind.AntiNormal: return new Color(0.95f, 0.35f, 0.95f);
                case MarkerKind.RadialOut: case MarkerKind.RadialIn: return new Color(0.35f, 0.9f, 1f);
                case MarkerKind.Maneuver: return new Color(0.3f, 0.55f, 1f);
                default: return new Color(0.8f, 0.45f, 1f);
            }
        }

        /// <summary>32x32 navball marker icon (white shapes; tinted at draw time).</summary>
        public static Texture2D MarkerTexture(MarkerKind k)
        {
            const int S = 48;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int j = 0; j < S; j++)
                for (int i = 0; i < S; i++)
                {
                    float x = (i + 0.5f) / S * 2f - 1f, y = (j + 0.5f) / S * 2f - 1f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    bool ring = Mathf.Abs(r - 0.5f) < 0.09f;
                    bool dot = r < 0.12f;
                    bool cross = (Mathf.Abs(x - y) < 0.1f || Mathf.Abs(x + y) < 0.1f) && r < 0.42f;
                    bool on = false;
                    switch (k)
                    {
                        case MarkerKind.Prograde:
                            on = ring || dot || (Mathf.Abs(x) < 0.07f && y > 0.58f && y < 0.95f) || (Mathf.Abs(y) < 0.07f && Mathf.Abs(x) > 0.58f && Mathf.Abs(x) < 0.95f);
                            break;
                        case MarkerKind.Retrograde:
                            on = ring || cross || (Mathf.Abs(x) < 0.07f && y < -0.58f && y > -0.95f);
                            break;
                        case MarkerKind.Normal:
                        case MarkerKind.AntiNormal:
                        {
                            float d1 = TriDist(x, k == MarkerKind.Normal ? y : -y);
                            on = Mathf.Abs(d1) < 0.08f || (k == MarkerKind.Normal ? dot : cross && r < 0.25f);
                            break;
                        }
                        case MarkerKind.RadialOut:
                            on = ring || dot || ((Mathf.Abs(x - y) < 0.09f || Mathf.Abs(x + y) < 0.09f) && r > 0.6f && r < 0.95f);
                            break;
                        case MarkerKind.RadialIn:
                            on = ring || ((Mathf.Abs(x - y) < 0.09f || Mathf.Abs(x + y) < 0.09f) && r < 0.42f && r > 0.12f);
                            break;
                        case MarkerKind.Maneuver:
                            on = Mathf.Abs(r - 0.62f) < 0.1f || (Mathf.Abs(x) < 0.09f && Mathf.Abs(y) < 0.9f) || (Mathf.Abs(y) < 0.09f && Mathf.Abs(x) < 0.9f);
                            break;
                        case MarkerKind.Target:
                        case MarkerKind.AntiTarget:
                        {
                            float sq = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                            on = Mathf.Abs(sq - 0.55f) < 0.08f || (k == MarkerKind.Target ? dot : cross);
                            break;
                        }
                    }
                    px[j * S + i] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static float TriDist(float x, float y)
        {
            // Signed distance-ish to an upward triangle outline of size ~0.6.
            float a = y + 0.35f;                         // bottom edge
            float b = -0.866f * x - 0.5f * y + 0.35f;   // left edge
            float c = 0.866f * x - 0.5f * y + 0.35f;    // right edge
            return Mathf.Min(a, Mathf.Min(b, c));
        }

        /// <summary>Dark bezel around the navball that also hides the square corners of its render.</summary>
        public static Texture2D BezelTexture()
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int j = 0; j < S; j++)
                for (int i = 0; i < S; i++)
                {
                    float x = (i + 0.5f) / S * 2f - 1f, y = (j + 0.5f) / S * 2f - 1f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    Color c;
                    if (r > 1f) c = new Color(0, 0, 0, 0);
                    else if (r > 0.9f) c = new Color(0.16f, 0.17f, 0.19f, 1f);
                    else if (r > 0.88f) c = new Color(0.45f, 0.47f, 0.5f, 1f);
                    else c = new Color(0, 0, 0, 0);
                    px[j * S + i] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static Mesh Explosion()
        {
            if (explosion != null) return explosion;
            var b = new MeshBuilder();
            b.AddSphere(Vector3.zero, 1f, new Color(1f, 0.6f, 0.2f, 0.8f), 20, 12);
            b.AddSphere(Vector3.zero, 0.6f, new Color(1f, 0.95f, 0.7f, 0.9f), 16, 8);
            explosion = b.ToMesh("Explosion");
            return explosion;
        }
    }
}
