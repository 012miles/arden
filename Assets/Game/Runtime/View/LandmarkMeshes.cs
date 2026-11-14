using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 27: the models of the places to discover, built from simple shapes in a local frame (y up, the origin on
    /// the ground). Each has a solid part (Rocket/Lit) and, for vents, plumes, crystals and lava, a glowing part (additive).
    /// </summary>
    public static class LandmarkMeshes
    {
        public static GameObject Create(Landmark l, CelestialBody body)
        {
            var solid = new MeshBuilder();
            var glow = new MeshBuilder();
            Color ground = MeshFactory.BodyColor(body, l.dir.ToFloat());
            int seed = l.seed;
            // how high the ground is round the landmark, in its own frame (so rocks and stones sit on a slope)
            Quaternion q = Landmarks.Frame(l);
            double h0 = Terrain.Height(body, l.dir), R = body.radius;
            G = (x, z) =>
            {
                Vector3d p = l.dir * R + Vector3d.FromFloat(q * new Vector3(x, 0, z));
                return (float)(Terrain.Height(body, p.normalized) - h0 - (p.magnitude - R));
            };
            switch (l.kind)
            {
                case LandmarkKind.Stones: Stones(solid, seed); break;
                case LandmarkKind.Probe: Probe(solid, ground, seed); break;
                case LandmarkKind.Spire: Spire(solid, ground, seed); break;
                case LandmarkKind.Geysers: Geysers(solid, glow, seed); break;
                case LandmarkKind.Arch: Arch(solid, seed); break;
                case LandmarkKind.Caldera: Caldera(solid, glow, seed); break;
                case LandmarkKind.Balanced: Balanced(solid, ground, seed); break;
                case LandmarkKind.LavaTubes: LavaTubes(solid, glow, seed); break;
                case LandmarkKind.Crystals: Crystals(solid, glow, seed); break;
            }
            var go = new GameObject("Landmark " + l.name);
            go.AddComponent<MeshFilter>().sharedMesh = solid.ToMesh(l.id);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            if (glow.v.Count > 0)
            {
                var g = new GameObject("Glow");
                g.transform.SetParent(go.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = glow.ToMesh(l.id + " glow");
                var gr = g.AddComponent<MeshRenderer>();
                gr.sharedMaterial = Mats.Additive;
                gr.shadowCastingMode = ShadowCastingMode.Off;
                gr.receiveShadows = false;
            }
            return go;
        }

        // ---------------------------------------------------------------- helpers

        static float H(int seed, int i) => Noise.Hash(seed, i, 17, 911);

        /// <summary>The ground's height at (x, z) in the landmark's frame (set for each landmark as it's built).</summary>
        static System.Func<float, float, float> G = (x, z) => 0f;

        /// <summary>Moves, turns and stretches the vertices added since 'from'.</summary>
        static void Xf(MeshBuilder b, int from, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            Vector3 inv = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            for (int i = from; i < b.v.Count; i++)
            {
                b.v[i] = pos + rot * Vector3.Scale(b.v[i], scale);
                b.n[i] = (rot * Vector3.Scale(b.n[i], inv)).normalized;
            }
        }

        static void Xf(MeshBuilder b, int from, Vector3 pos, Quaternion rot) => Xf(b, from, pos, rot, Vector3.one);

        /// <summary>A lumpy boulder.</summary>
        static void Boulder(MeshBuilder b, Vector3 at, float size, Color col, int seed, float squash = 0.7f)
        {
            b.Mat = MeshBuilder.MatConcrete;
            int s0 = b.v.Count;
            b.AddSphere(Vector3.zero, 1f, col, 9, 6);
            Quaternion rot = Quaternion.Euler(H(seed, 1) * 30 - 15, H(seed, 2) * 360, H(seed, 3) * 30 - 15);
            for (int i = s0; i < b.v.Count; i++)
            {
                Vector3 d = b.v[i];
                float bump = 0.78f + 0.44f * Mathf.PerlinNoise(d.x * 1.6f + seed * 0.37f, d.z * 1.6f + d.y * 1.2f + seed * 0.11f);
                b.v[i] = at + rot * (new Vector3(d.x, d.y * squash, d.z) * bump * size);
                b.n[i] = rot * new Vector3(d.x, d.y / squash, d.z).normalized;
                float shade = Mathf.Lerp(0.62f, 1.05f, d.y * 0.5f + 0.5f);
                b.c[i] = new Color(col.r * shade, col.g * shade, col.b * shade, 1f);
            }
        }

        static void Rubble(MeshBuilder b, int n, float r0, float r1, float s0, float s1, Color col, int seed)
        {
            for (int i = 0; i < n; i++)
            {
                float a = H(seed, i * 5) * Mathf.PI * 2, r = Mathf.Lerp(r0, r1, H(seed, i * 5 + 1));
                float s = Mathf.Lerp(s0, s1, Mathf.Pow(H(seed, i * 5 + 2), 2f));
                float tone = 0.8f + 0.3f * H(seed, i * 5 + 3);
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                Boulder(b, new Vector3(x, s * 0.25f + G(x, z), z), s, col * tone, seed + i * 31);
            }
        }

        static Color Tone(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        // ---------------------------------------------------------------- the places

        /// <summary>Arden: a ring of standing stones with lintels on a hilltop, a horseshoe of taller ones inside.</summary>
        static void Stones(MeshBuilder b, int seed)
        {
            b.Mat = MeshBuilder.MatConcrete;
            var stone = new Color(0.55f, 0.54f, 0.5f);
            const int ring = 14;
            float[] tops = new float[ring];
            for (int k = 0; k < ring; k++)
            {
                float a = k * Mathf.PI * 2 / ring, h = 5.2f + 1.4f * H(seed, k);
                tops[k] = h - 1f;
                float gy = G(Mathf.Cos(a) * 18f, Mathf.Sin(a) * 18f);
                tops[k] += gy;
                if (k == 9) { b.AddBox(new Vector3(Mathf.Cos(a) * 18f, 0.4f + gy, Mathf.Sin(a) * 18f), new Vector3(1.9f, 0.9f, h), Tone(stone, 0.9f), Quaternion.Euler(0, -a * Mathf.Rad2Deg + 20, 0)); continue; }   // one has fallen
                b.AddBox(new Vector3(Mathf.Cos(a) * 18f, h * 0.5f - 1f + gy, Mathf.Sin(a) * 18f), new Vector3(1.9f, h, 1.1f), Tone(stone, 0.85f + 0.25f * H(seed, k + 40)),
                    Quaternion.Euler(H(seed, k + 60) * 4 - 2, -a * Mathf.Rad2Deg + 90, H(seed, k + 80) * 4 - 2));
            }
            for (int k = 0; k < ring; k += 2)
            {
                if (k == 8 || k + 1 == 9) continue;
                float a0 = k * Mathf.PI * 2 / ring, a1 = (k + 1) * Mathf.PI * 2 / ring;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 18f, 0, Mathf.Sin(a0) * 18f), p1 = new Vector3(Mathf.Cos(a1) * 18f, 0, Mathf.Sin(a1) * 18f);
                Vector3 mid = (p0 + p1) * 0.5f + Vector3.up * (Mathf.Min(tops[k], tops[k + 1]) + 0.45f);
                b.AddBox(mid, new Vector3((p1 - p0).magnitude + 1.6f, 0.9f, 1.2f), Tone(stone, 0.8f), Quaternion.LookRotation(p1 - p0) * Quaternion.Euler(0, 90, 0));
            }
            for (int k = 0; k < 5; k++)
            {
                float a = Mathf.PI * (0.15f + 0.175f * k), h = 6.5f + 1.2f * H(seed, k + 100);
                b.AddBox(new Vector3(Mathf.Cos(a) * 9f, h * 0.5f - 1f + G(Mathf.Cos(a) * 9f, Mathf.Sin(a) * 9f), Mathf.Sin(a) * 9f), new Vector3(2.4f, h, 1.3f), Tone(stone, 0.9f), Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90, 0));
            }
            b.AddBox(new Vector3(0, 0.25f, 0), new Vector3(3.6f, 0.7f, 1.6f), Tone(stone, 0.75f), Quaternion.Euler(0, 20, 0));
        }

        /// <summary>Tal: an old probe half buried where it hit, a bent dish, one solar wing still on, one torn off, bits strewn about.</summary>
        static void Probe(MeshBuilder b, Color ground, int seed)
        {
            var gold = new Color(0.82f, 0.62f, 0.26f);
            var metal = new Color(0.62f, 0.63f, 0.65f);
            var panel = new Color(0.08f, 0.11f, 0.22f);
            // the furrow it dug coming in
            b.Mat = MeshBuilder.MatMatte;
            b.AddBox(new Vector3(-11f, 0.04f, 0), new Vector3(22f, 0.12f, 2.8f), Tone(ground, 0.62f), Quaternion.Euler(0, 4, 0));
            b.AddBox(new Vector3(-11f, 0.25f, 1.9f), new Vector3(20f, 0.5f, 0.9f), Tone(ground, 0.85f), Quaternion.Euler(0, 4, 0));
            b.AddBox(new Vector3(-11f, 0.25f, -1.9f), new Vector3(20f, 0.5f, 0.9f), Tone(ground, 0.85f), Quaternion.Euler(0, 4, 0));
            // the body: an eight-sided drum in gold foil, nose down in the ground
            b.Mat = MeshBuilder.MatFoil;
            int s = b.v.Count;
            b.AddFrustum(2.4f, -2.4f, 1.3f, 1.3f, gold, 8, true, true);
            Xf(b, s, new Vector3(0.4f, 0.9f, 0), Quaternion.Euler(8, 0, 58));
            b.Mat = MeshBuilder.MatMetal;
            s = b.v.Count;
            b.AddFrustum(0.5f, -0.5f, 0.5f, 0.7f, metal, 10, true, false);          // engine bell sticking up
            Xf(b, s, new Vector3(-1.7f, 2.4f, 0.2f), Quaternion.Euler(8, 0, 58));
            // the dish, bent back
            s = b.v.Count;
            b.AddFrustum(0.55f, 0f, 2.3f, 0.25f, metal, 18, false, false, true);
            Xf(b, s, new Vector3(2.4f, 2.2f, 1.4f), Quaternion.Euler(35, 30, -20));
            b.AddBox(new Vector3(1.6f, 1.9f, 0.9f), new Vector3(0.12f, 1.8f, 0.12f), metal, Quaternion.Euler(30, 0, -40));
            // a solar wing still attached, one torn off and lying flat
            b.Mat = MeshBuilder.MatGloss;
            b.AddBox(new Vector3(-0.9f, 2.6f, -2.8f), new Vector3(1.4f, 0.06f, 4.2f), panel, Quaternion.Euler(-15, 0, 50));
            b.AddBox(new Vector3(8.5f, 0.06f, -5f), new Vector3(1.4f, 0.06f, 4.2f), panel, Quaternion.Euler(3, 38, 2));
            // bits everywhere
            for (int i = 0; i < 18; i++)
            {
                float a = H(seed, i) * Mathf.PI * 2, r = 3f + 22f * H(seed, i + 30);
                float sz = 0.15f + 0.6f * H(seed, i + 60);
                b.Mat = i % 3 == 0 ? MeshBuilder.MatFoil : MeshBuilder.MatDarkMetal;
                b.AddBox(new Vector3(Mathf.Cos(a) * r, sz * 0.3f + G(Mathf.Cos(a) * r, Mathf.Sin(a) * r), Mathf.Sin(a) * r), new Vector3(sz, sz * 0.4f, sz * 1.6f), i % 3 == 0 ? gold : metal * 0.7f,
                    Quaternion.Euler(H(seed, i + 90) * 60, H(seed, i + 120) * 360, 0));
            }
        }

        /// <summary>Tal: a single leaning needle of rock 180 m tall, boulders round its foot.</summary>
        static void Spire(MeshBuilder b, Color ground, int seed)
        {
            b.Mat = MeshBuilder.MatConcrete;
            var rock = Tone(ground, 0.78f);
            float y = -30f, lean = 0f;
            float[] hs = { 50, 30, 24, 20, 18, 16, 14, 12, 10, 8 };
            float r = 30f;
            for (int k = 0; k < hs.Length; k++)
            {
                float rTop = Mathf.Max(4.5f, r * (0.8f + 0.1f * H(seed, k)));
                int s = b.v.Count;
                b.AddFrustum(hs[k], 0, rTop, r, Tone(rock, 0.9f + 0.2f * H(seed, k + 20)), 7, k == hs.Length - 1, false);
                float leanNext = lean + 1.2f + 1.6f * H(seed, k + 40);
                Xf(b, s, new Vector3(lean, y, lean * 0.4f), Quaternion.Euler(0, H(seed, k + 60) * 60, 0));
                y += hs[k];
                lean = leanNext;
                r = rTop;
            }
            Rubble(b, 14, 26f, 60f, 1.5f, 6f, rock, seed);
        }

        /// <summary>Vesk: a line of ice vents along a blue crack, each throwing a tall plume of frost.</summary>
        static void Geysers(MeshBuilder b, MeshBuilder glow, int seed)
        {
            var ice = new Color(0.84f, 0.9f, 0.96f);
            b.Mat = MeshBuilder.MatMatte;
            b.AddBox(new Vector3(0, 0.06f, 0), new Vector3(230f, 0.25f, 4f), new Color(0.32f, 0.44f, 0.6f), Quaternion.Euler(0, 12, 0));
            b.Mat = MeshBuilder.MatGloss;
            for (int k = 0; k < 5; k++)
            {
                float x = (k - 2) * 46f + 8f * H(seed, k);
                Vector3 at = Quaternion.Euler(0, 12, 0) * new Vector3(x, 0, 0);
                at.y = G(at.x, at.z);
                int s = b.v.Count;
                b.AddFrustum(6f, -4f, 2.2f, 10f, ice, 12, false, false);
                Xf(b, s, at, Quaternion.identity);
                s = b.v.Count;
                b.AddDisc(5.9f, 2.2f, new Color(0.2f, 0.3f, 0.42f), 12, true);
                Xf(b, s, at, Quaternion.identity);
                float big = 0.7f + 0.6f * H(seed, k + 10);
                foreach (var (top, bot, rt, rb, a) in new[] { (70f, 5f, 9f, 1.8f, 0.26f), (190f, 50f, 24f, 8f, 0.12f), (360f, 140f, 46f, 20f, 0.05f) })
                {
                    int g = glow.v.Count;
                    glow.AddFrustum(top * big, bot * big, rt * big, rb * big, new Color(0.8f, 0.9f, 1f, a), 14, false, false, true);
                    Xf(glow, g, at, Quaternion.identity);
                }
            }
        }

        /// <summary>Ochre: a natural arch 400 m across and 250 m high, banded red rock, massive feet.</summary>
        static void Arch(MeshBuilder b, int seed)
        {
            b.Mat = MeshBuilder.MatConcrete;
            Color[] bands = { new Color(0.62f, 0.34f, 0.2f), new Color(0.71f, 0.43f, 0.26f), new Color(0.55f, 0.29f, 0.17f), new Color(0.66f, 0.38f, 0.22f) };
            const int n = 30;
            for (int i = 0; i < n; i++)
            {
                float t0 = Mathf.PI * i / n, t1 = Mathf.PI * (i + 1) / n, tm = (t0 + t1) * 0.5f;
                Vector3 p0 = new Vector3(Mathf.Cos(t0) * 200f, Mathf.Sin(t0) * 250f, 0), p1 = new Vector3(Mathf.Cos(t1) * 200f, Mathf.Sin(t1) * 250f, 0);
                float top = Mathf.Sin(tm);
                float thick = Mathf.Lerp(60f, 34f, top), depth = Mathf.Lerp(80f, 48f, top);
                Vector3 d = p1 - p0;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                b.AddBox((p0 + p1) * 0.5f, new Vector3(d.magnitude + 6f, thick, depth), bands[i % bands.Length], Quaternion.Euler(0, 0, ang));
                // a lumpy outer layer so it isn't a perfect curve
                if (i % 2 == 0) b.AddBox((p0 + p1) * 0.5f + Quaternion.Euler(0, 0, ang) * new Vector3(0, thick * 0.45f, (H(seed, i) - 0.5f) * 20f),
                    new Vector3(d.magnitude * 1.2f, thick * 0.35f, depth * 0.7f), bands[(i + 1) % bands.Length], Quaternion.Euler(0, H(seed, i + 9) * 10 - 5, ang));
            }
            foreach (float x in new[] { -200f, 200f })
            {
                int s = b.v.Count;
                b.AddFrustum(50f, -60f, 46f, 64f, bands[2], 9, false, false);
                Xf(b, s, new Vector3(x, Mathf.Min(0f, G(x, 0)), 0), Quaternion.Euler(0, H(seed, (int)x) * 40, 0));
            }
            Rubble(b, 16, 210f, 300f, 4f, 16f, bands[0], seed);
        }

        /// <summary>Ochre's caldera floor: steaming vents, dark lava boulders.</summary>
        static void Caldera(MeshBuilder b, MeshBuilder glow, int seed)
        {
            var dark = new Color(0.2f, 0.13f, 0.1f);
            b.Mat = MeshBuilder.MatConcrete;
            for (int k = 0; k < 4; k++)
            {
                float a = k * 1.7f + H(seed, k), r = 40f + 120f * H(seed, k + 4);
                Vector3 at = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                at.y = G(at.x, at.z);
                int s = b.v.Count;
                b.AddFrustum(5f, -4f, 2f, 9f, new Color(0.66f, 0.55f, 0.32f), 10, false, false);
                Xf(b, s, at, Quaternion.identity);
                int g = glow.v.Count;
                glow.AddFrustum(90f, 4f, 22f, 2f, new Color(1f, 0.92f, 0.75f, 0.1f), 12, false, false, true);
                Xf(glow, g, at, Quaternion.identity);
            }
            Rubble(b, 22, 10f, 260f, 1.5f, 7f, dark, seed);
        }

        /// <summary>Mote: a boulder the size of a house balanced on a thin pillar.</summary>
        static void Balanced(MeshBuilder b, Color ground, int seed)
        {
            b.Mat = MeshBuilder.MatConcrete;
            var rock = Tone(ground, 0.8f);
            int s = b.v.Count;
            b.AddFrustum(24f, -6f, 2.2f, 4.8f, rock, 9, false, false);
            Xf(b, s, Vector3.zero, Quaternion.Euler(2, 0, 3));
            Boulder(b, new Vector3(1.2f, 31f, 0.4f), 9f, Tone(rock, 1.05f), seed + 5, 0.75f);
            Rubble(b, 10, 8f, 30f, 0.8f, 3.5f, rock, seed);
        }

        /// <summary>Pyre: a dark basalt mound with three cave mouths, molten rock glowing deep inside.</summary>
        static void LavaTubes(MeshBuilder b, MeshBuilder glow, int seed)
        {
            var basalt = new Color(0.13f, 0.12f, 0.12f);
            b.Mat = MeshBuilder.MatConcrete;
            int s = b.v.Count;
            b.AddSphere(Vector3.zero, 1f, basalt, 18, 9);
            for (int i = s; i < b.v.Count; i++)
            {
                Vector3 d = b.v[i];
                float bump = 0.9f + 0.2f * Mathf.PerlinNoise(d.x * 2.1f + 3.1f, d.z * 2.1f + d.y * 1.7f);
                b.v[i] = new Vector3(d.x * 95f, d.y * 40f - 10f, d.z * 75f) * bump;
                b.n[i] = new Vector3(d.x / 95f, d.y / 40f, d.z / 75f).normalized;
            }
            for (int k = -1; k <= 1; k++)
            {
                float yaw = k * 34f;
                Quaternion q = Quaternion.Euler(0, yaw, 0);
                Vector3 mouth = q * new Vector3(0, 7f, 70f);
                // the tunnel: a dark tube going in (its axis along the hill's radius), open at the mouth
                b.Mat = MeshBuilder.MatMatte;
                int t = b.v.Count;
                b.AddFrustum(4f, -40f, 8f, 6f, new Color(0.02f, 0.015f, 0.015f), 14, false, false, true);
                Xf(b, t, mouth, q * Quaternion.Euler(90, 0, 0));
                // lava at the far end, lit from within
                b.Mat = MeshBuilder.MatLamp;
                t = b.v.Count;
                b.AddDisc(0, 5.6f, new Color(0.85f, 0.25f, 0.04f), 14, true);
                Xf(b, t, mouth + q * new Vector3(0, -2f, -34f), q * Quaternion.Euler(80, 0, 0));
                int g = glow.v.Count;
                glow.AddFrustum(2f, -30f, 11f, 6f, new Color(1f, 0.38f, 0.08f, 0.18f), 14, false, false, true);
                Xf(glow, g, mouth, q * Quaternion.Euler(90, 0, 0));
                // a trickle of cooled lava out of the mouth
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(mouth + q * new Vector3(0, -6.5f, 14f), new Vector3(7f, 1.2f, 30f), new Color(0.07f, 0.05f, 0.05f), q * Quaternion.Euler(16, 0, 0));
            }
            Rubble(b, 14, 70f, 130f, 2f, 7f, basalt, seed);
        }

        /// <summary>Rime: a crater floor bristling with giant six-sided crystals that glow blue.</summary>
        static void Crystals(MeshBuilder b, MeshBuilder glow, int seed)
        {
            for (int i = 0; i < 36; i++)
            {
                float a = H(seed, i) * Mathf.PI * 2, r = 75f * Mathf.Sqrt(H(seed, i + 50));
                float h = 6f + 34f * Mathf.Pow(H(seed, i + 100), 1.8f), rad = 0.16f * h + 0.6f;
                Vector3 at = new Vector3(Mathf.Cos(a) * r, -2f, Mathf.Sin(a) * r);
                at.y += G(at.x, at.z);
                Quaternion q = Quaternion.Euler(H(seed, i + 150) * 50 - 25, H(seed, i + 200) * 360, H(seed, i + 250) * 50 - 25);
                var col = Color.Lerp(new Color(0.05f, 0.17f, 0.32f), new Color(0.12f, 0.08f, 0.3f), H(seed, i + 300) * 0.5f);
                b.Mat = MeshBuilder.MatLamp;
                int s = b.v.Count;
                b.AddFrustum(h * 0.8f, 0f, rad, rad * 1.15f, col, 6, false, false);
                b.AddFrustum(h, h * 0.8f, 0f, rad, col * 1.3f, 6, false, false);
                Xf(b, s, at, q);
            }
            Rubble(b, 12, 80f, 110f, 1.5f, 5f, new Color(0.6f, 0.66f, 0.72f), seed);
        }
    }
}
