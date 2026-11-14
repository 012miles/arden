using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 20b: sharp worlds from orbit. Each world's colours, ground relief and (for Arden) clouds are baked into
    /// cubemaps from the very same functions the terrain and the map use, on background threads after the game starts,
    /// home first. Until a world's maps are ready it keeps its vertex colours, so nothing waits.
    /// Cubemap texel directions follow the D3D/Unity face layout (rows from the top of each face).
    /// </summary>
    public static class PlanetBaker
    {
        public class Maps
        {
            public Cubemap color, normal, clouds;
        }

        class Job
        {
            public CelestialBody body;
            public int res;
            public byte[][] color = new byte[6][], normal = new byte[6][], clouds;
        }

        static readonly Dictionary<CelestialBody, Maps> done = new Dictionary<CelestialBody, Maps>();
        static readonly ConcurrentQueue<Job> finished = new ConcurrentQueue<Job>();
        static bool started;
        static volatile bool quitting;
        static string cacheDir;
        /// <summary>Bump when the terrain or colour functions change, so old cached maps are baked again.</summary>
        const int CacheVersion = 6;   // 4-5: the space centre's peninsula and soft shores (milestone 24)
        public static event Action<CelestialBody, Maps> Ready;

        public static Maps Get(CelestialBody b) => done.TryGetValue(b, out var m) ? m : null;
        public static int Baked => done.Count;

        /// <summary>Bakes every world (not the sun), the given ones first, two at a time on worker threads.</summary>
        public static void Start(IList<CelestialBody> bodies, params CelestialBody[] first)
        {
            if (started) return;
            started = true;
            Application.quitting += () => quitting = true;
            try { cacheDir = Path.Combine(Application.persistentDataPath, "planetmaps"); Directory.CreateDirectory(cacheDir); } catch { cacheDir = null; }
            var order = new List<CelestialBody>();
            foreach (var b in first) if (b != null && !b.isStar && !order.Contains(b)) order.Add(b);
            foreach (var b in bodies) if (!b.isStar && !order.Contains(b)) order.Add(b);
            var thread = new Thread(() =>
            {
                foreach (var b in order)
                {
                    if (quitting) return;
                    try
                    {
                        // Baked once, then kept in the game's data folder: the next start loads them in a moment
                        var job = LoadCached(b, ResFor(b));
                        if (job == null) { job = Bake(b, ResFor(b)); SaveCached(job); }
                        finished.Enqueue(job);
                    }
                    catch (Exception e) { if (!quitting) Debug.LogWarning($"Couldn't bake {b.name}: {e.Message}"); }
                }
            }) { IsBackground = true, Name = "PlanetBaker", Priority = System.Threading.ThreadPriority.BelowNormal };
            thread.Start();
        }

        static string CachePath(CelestialBody b, int res) => cacheDir == null ? null : Path.Combine(cacheDir, $"{b.name}_{b.seed}_{res}_v{CacheVersion}.bin.gz");

        static Job LoadCached(CelestialBody b, int res)
        {
            string path = CachePath(b, res);
            if (path == null || !File.Exists(path)) return null;
            try
            {
                using (var fs = File.OpenRead(path))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                using (var r = new BinaryReader(gz))
                {
                    if (r.ReadInt32() != res) return null;
                    bool clouds = r.ReadBoolean();
                    var job = new Job { body = b, res = res };
                    if (clouds) job.clouds = new byte[6][];
                    for (int f = 0; f < 6; f++) job.color[f] = r.ReadBytes(res * res * 4);
                    for (int f = 0; f < 6; f++) job.normal[f] = r.ReadBytes(res * res * 4);
                    if (clouds) for (int f = 0; f < 6; f++) job.clouds[f] = r.ReadBytes(res * res);
                    return job.normal[5].Length == res * res * 4 ? job : null;
                }
            }
            catch { return null; }
        }

        static void SaveCached(Job job)
        {
            string path = CachePath(job.body, job.res);
            if (path == null) return;
            try
            {
                using (var fs = File.Create(path))
                using (var gz = new GZipStream(fs, System.IO.Compression.CompressionLevel.Fastest))
                using (var w = new BinaryWriter(gz))
                {
                    w.Write(job.res);
                    w.Write(job.clouds != null);
                    for (int f = 0; f < 6; f++) w.Write(job.color[f]);
                    for (int f = 0; f < 6; f++) w.Write(job.normal[f]);
                    if (job.clouds != null) for (int f = 0; f < 6; f++) w.Write(job.clouds[f]);
                }
            }
            catch (Exception e) { Debug.LogWarning($"Couldn't keep the {job.body.name} maps: {e.Message}"); }
        }

        public static int ResFor(CelestialBody b) => b.style == BodyStyle.Earthlike || b.style == BodyStyle.Gas || b.radius > 150_000 ? 1024 : b.radius > 60_000 ? 512 : 256;   // milestone 26: sharper rocky worlds

        /// <summary>Main thread, every frame: turns finished bakes into textures (one per frame, so there's no hitch).</summary>
        public static void Update()
        {
            if (!finished.TryDequeue(out var job)) return;
            var maps = new Maps
            {
                color = Upload(job.color, job.res, GraphicsFormat.R8G8B8A8_SRGB, job.body.name + " colour"),
                normal = Upload(job.normal, job.res, GraphicsFormat.R8G8B8A8_UNorm, job.body.name + " relief"),
            };
            if (job.clouds != null)
            {
                var faces = new byte[6][];
                for (int f = 0; f < 6; f++) faces[f] = job.clouds[f] ?? new byte[job.res * job.res];
                maps.clouds = Upload(faces, job.res, GraphicsFormat.R8_UNorm, job.body.name + " clouds");
            }
            done[job.body] = maps;
            Ready?.Invoke(job.body, maps);
        }

        static Cubemap Upload(byte[][] faces, int res, GraphicsFormat fmt, string name)
        {
            var c = new Cubemap(res, fmt, TextureCreationFlags.MipChain) { name = name, filterMode = FilterMode.Trilinear, anisoLevel = 4, wrapMode = TextureWrapMode.Clamp };
            for (int f = 0; f < 6; f++) c.SetPixelData(faces[f], 0, (CubemapFace)f);
            c.Apply(true, true);   // mipmaps, then free the CPU copy
            return c;
        }

        /// <summary>Direction of texel (i, j) on cube face f (row 0 = top of the face), the D3D convention Unity uses.</summary>
        public static Vector3 Dir(int f, float u, float v)
        {
            switch (f)
            {
                case 0: return new Vector3(1, -v, -u).normalized;   // +X
                case 1: return new Vector3(-1, -v, u).normalized;   // -X
                case 2: return new Vector3(u, 1, v).normalized;     // +Y
                case 3: return new Vector3(u, -1, -v).normalized;   // -Y
                case 4: return new Vector3(u, -v, 1).normalized;    // +Z
                default: return new Vector3(-u, -v, -1).normalized; // -Z
            }
        }

        static Job Bake(CelestialBody b, int N)
        {
            var job = new Job { body = b, res = N };
            bool clouds = b.style == BodyStyle.Earthlike && b.HasAtmosphere;
            if (clouds) job.clouds = new byte[6][];
            bool relief = b.style != BodyStyle.Gas;
            double R = b.radius;
            float texel = 2f / N;
            var opts = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            for (int f = 0; f < 6; f++)
            {
                int face = f;
                var col = new byte[N * N * 4];
                var nrm = new byte[N * N * 4];
                var cl = clouds ? new byte[N * N] : null;
                // Heights on a grid one texel bigger all round, for the slopes at the edges
                int M = N + 2;
                var h = relief ? new float[M * M] : null;
                Parallel.For(0, M, opts, j =>
                {
                    for (int i = 0; i < M; i++)
                    {
                        float u = (i - 1 + 0.5f) * texel - 1f, v = (j - 1 + 0.5f) * texel - 1f;
                        Vector3 d = Dir(face, u, v);
                        if (relief) h[j * M + i] = (float)Terrain.Height(b, Vector3d.FromFloat(d));
                        if (i == 0 || j == 0 || i == M - 1 || j == M - 1) continue;
                        int k = ((j - 1) * N + (i - 1));
                        Color c = MeshFactory.BodyColor(b, d);
                        col[k * 4 + 0] = ToByte(Srgb(c.r));
                        col[k * 4 + 1] = ToByte(Srgb(c.g));
                        col[k * 4 + 2] = ToByte(Srgb(c.b));
                        col[k * 4 + 3] = ToByte(MeshFactory.WaterMask(b, d));
                        if (cl != null) cl[k] = ToByte(MeshFactory.CloudCover(b, d));
                    }
                });
                // Relief: the surface normal from the height grid (in planet-fixed axes)
                float step = (float)(R * texel);
                for (int j = 0; j < N; j++)
                    for (int i = 0; i < N; i++)
                    {
                        float u = (i + 0.5f) * texel - 1f, v = (j + 0.5f) * texel - 1f;
                        Vector3 d = Dir(face, u, v);
                        Vector3 n = d;
                        if (relief)
                        {
                            Vector3 du = Dir(face, u + texel, v) - Dir(face, u - texel, v);
                            Vector3 dv = Dir(face, u, v + texel) - Dir(face, u, v - texel);
                            float hu = h[(j + 1) * M + (i + 2)] - h[(j + 1) * M + i];
                            float hv = h[(j + 2) * M + (i + 1)] - h[j * M + (i + 1)];
                            // surface tangents: the move across the sphere plus the change in height
                            Vector3 tu = du * (float)R + d * hu, tv = dv * (float)R + d * hv;
                            n = Vector3.Cross(tv, tu).normalized;
                            if (Vector3.Dot(n, d) < 0) n = -n;
                        }
                        int k = j * N + i;
                        nrm[k * 4 + 0] = ToByte(n.x * 0.5f + 0.5f);
                        nrm[k * 4 + 1] = ToByte(n.y * 0.5f + 0.5f);
                        nrm[k * 4 + 2] = ToByte(n.z * 0.5f + 0.5f);
                        nrm[k * 4 + 3] = 255;
                    }
                job.color[f] = col;
                job.normal[f] = nrm;
                if (clouds) job.clouds[f] = cl;
            }
            return job;
        }

        // Linear to sRGB by hand (safe on worker threads), so dark seas keep their detail in 8 bits
        static float Srgb(float x)
        {
            x = Mathf.Clamp01(x);
            return x <= 0.0031308f ? 12.92f * x : 1.055f * (float)Math.Pow(x, 1.0 / 2.4) - 0.055f;
        }

        static byte ToByte(float x) => (byte)Mathf.Clamp(Mathf.RoundToInt(x * 255f), 0, 255);
    }
}
