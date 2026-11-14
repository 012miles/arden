using System;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Real ground height (m above a world's reference radius) at a planet-fixed direction: Arden's continents rise
    /// into hills and mountains up to ~3.5 km (seas stay flat at 0, the land round the launch site is flat); Tal has
    /// rolling highlands, low dark plains and craters with raised rims. Physics, walking, drilling and the meshes all
    /// use this, so what you see is what you land on.
    /// </summary>
    public static class Terrain
    {
        /// <summary>The same, with Arden's land/sea field already worked out for this point (ArdenField), to save doing it twice.</summary>
        public static double Height(CelestialBody b, Vector3d fixedDir, float ardenField)
        {
            if (b.style != BodyStyle.Earthlike || float.IsNaN(ardenField)) return Height(b, fixedDir);
            return ArdenHeight(b, fixedDir.normalized.ToFloat(), ardenField);
        }

        public static double Height(CelestialBody b, Vector3d fixedDir)
        {
            Vector3 d = fixedDir.normalized.ToFloat();
            return b.style == BodyStyle.Star || b.style == BodyStyle.Gas ? 0 : b.style == BodyStyle.Earthlike ? ArdenHeight(b, d) : b.style == BodyStyle.Icy ? VeskHeight(b, d)
                : b.style == BodyStyle.Desert ? DesertHeight(b, d) : b.style == BodyStyle.Volcanic ? VolcanicHeight(b, d) : TalHeight(b, d);
        }

        /// <summary>Pyre: low, smooth lava fields, rougher ash plains, and broad volcanoes with summit calderas.</summary>
        static double VolcanicHeight(CelestialBody b, Vector3 d)
        {
            double h = 900 + (Noise.Fbm(d * 2.3f, 5, b.seed + 1) - 0.5) * 1500;
            float lava = LavaFields(b, d);
            h = h * (1 - lava) + 250 * lava;                                  // the lava has flooded the low ground flat
            h += Volcanoes(d, 3.2f, 2800, b.seed + 3);
            h += Craters(d, 24f, 110, b.seed + 4) * (1 - lava);
            h += (Noise.Fbm(d * 150f, 2, b.seed + 5) - 0.5) * 40 * (1 - lava * 0.8);
            h += (Noise.Fbm(d * 50f, 3, b.seed + 7) - 0.5) * 320 * (1 - lava);                    // milestone 26: hills
            return h;
        }

        /// <summary>How much a spot on Pyre is smooth lava field (0..1): shared with its colours and regions.</summary>
        public static float LavaFields(CelestialBody b, Vector3 d) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.47f, 0.40f, Noise.Fbm(d * 1.6f, 4, b.seed + 6)));

        /// <summary>Broad shield volcanoes scattered through 3D cells: a gentle cone with a sunken caldera at the top.</summary>
        public static double Volcanoes(Vector3 d, float freq, double height, int seed) => VolcanoField(d, freq, height, seed, out _);

        /// <summary>The volcano height, and how close to a summit caldera the spot is (1 = in the middle of one).</summary>
        public static double VolcanoField(Vector3 d, float freq, double height, int seed, out float caldera)
        {
            caldera = 0;
            Vector3 p = d * freq;
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
            double total = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int cx = xi + dx, cy = yi + dy, cz = zi + dz;
                        if (Noise.Hash(cx, cy, cz, seed) > 0.3f) continue;
                        var c = new Vector3(cx + Noise.Hash(cx, cy, cz, seed + 1), cy + Noise.Hash(cx, cy, cz, seed + 2), cz + Noise.Hash(cx, cy, cz, seed + 3));
                        if (c.sqrMagnitude < 1e-6f) continue;
                        c = c.normalized * freq;   // on the surface, so every volcano has its summit (and caldera) on the ground
                        float rad = 0.35f + 0.4f * Noise.Hash(cx, cy, cz, seed + 4);
                        float dist = (p - c).magnitude / rad;
                        if (dist >= 1f) continue;
                        double k = height * (0.5 + 0.5 * Noise.Hash(cx, cy, cz, seed + 5));
                        double cone = k * Math.Pow(1 - dist, 1.6);
                        if (dist < 0.22f) { cone -= k * 0.3 * (1 - dist / 0.22); caldera = Mathf.Max(caldera, 1f - dist / 0.22f); }
                        total = Math.Max(total, cone);
                    }
            return total;
        }

        /// <summary>Ochre: rolling rusty plains and highlands cut by deep winding canyons, craters, and fine dune ripples.</summary>
        /// <summary>Where Ochre's giant volcano stands (milestone 26).</summary>
        public static readonly Vector3 GreatMound = new Vector3(0.42f, 0.31f, -0.85f).normalized;

        static double DesertHeight(CelestialBody b, Vector3 d)
        {
            double h = 1400 + (Noise.Fbm(d * 2.0f, 5, b.seed + 1) - 0.5) * 2600;
            double canyon = 1 - Math.Abs(2 * Noise.Fbm(d * 3.5f, 4, b.seed + 2) - 1);   // 1 along the canyon lines
            h -= Math.Pow(canyon, 12) * 1800;
            h += Craters(d, 6f, 650, b.seed + 3) + Craters(d, 22f, 140, b.seed + 4);
            h += Craters(d, 80f, 45, b.seed + 6);                                            // milestone 26: more craters
            h += (Noise.Fbm(d * 260f, 2, b.seed + 5) - 0.5) * 30;                          // dune ripples
            // Milestone 26: ridged mountain ranges, rolling hills, and the Great Mound: a shield volcano some 6 km high
            // and 70 km across with a summit caldera
            double ridge = 1 - Math.Abs(2 * Noise.Fbm(d * 6f, 4, b.seed + 31) - 1);
            float ranges = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.65f, Noise.Fbm(d * 1.5f, 2, b.seed + 33)));
            h += ridge * ridge * ridge * 2200 * ranges * (1 - Math.Pow(canyon, 6));
            h += (Noise.Fbm(d * 45f, 3, b.seed + 35) - 0.5) * 380;
            float mound = Vector3.Angle(d, GreatMound) * Mathf.Deg2Rad * (float)b.radius / 35_000f;   // 0 at the top, 1 at the foot
            if (mound < 1f)
            {
                double shield = 6000 * Math.Pow(1 - mound, 1.6);
                if (mound < 0.09f) shield -= 900 * (1 - mound / 0.09f);                            // the caldera
                h += shield;
            }
            return h;
        }

        /// <summary>How much a spot on Ochre lies in a canyon (0..1): shared with its colours and regions.</summary>
        public static float CanyonDepth(CelestialBody b, Vector3 d) => Mathf.Pow(1f - Mathf.Abs(2f * Noise.Fbm(d * 3.5f, 4, b.seed + 2) - 1f), 12f);

        /// <summary>Highest the ground gets on a world (for overlays and safe heights).</summary>
        public static double MaxHeight(CelestialBody b) => b.style == BodyStyle.Star || b.style == BodyStyle.Gas ? 0 : b.style == BodyStyle.Earthlike ? 3800 : b.style == BodyStyle.Icy ? 3800 : b.style == BodyStyle.Desert ? 9500 : b.style == BodyStyle.Volcanic ? 4600 : 6200;

        /// <summary>Vesk: smooth ice plains crossed by raised crack ridges, a few soft craters.</summary>
        static double VeskHeight(CelestialBody b, Vector3 d)
        {
            double h = 700 + (Noise.Fbm(d * 2.6f, 4, b.seed + 1) - 0.5) * 900;
            double crack = 1 - Math.Abs(2 * Noise.Fbm(d * 9f, 3, b.seed + 2) - 1); // ridges along the noise's zero lines
            h += Math.Pow(crack, 6) * 220;
            h += Craters(d, 7f, 380, b.seed + 3) + Craters(d, 26f, 70, b.seed + 4);
            h += Craters(d, 90f, 30, b.seed + 6) + Craters(d, 300f, 10, b.seed + 7);                 // milestone 26: more, smaller craters
            h += (Noise.Fbm(d * 120f, 2, b.seed + 5) - 0.5) * 24;
            // Milestone 26: ridged ice mountains in places, and gentle hills
            double ridge = 1 - Math.Abs(2 * Noise.Fbm(d * 5f, 4, b.seed + 21) - 1);
            float ranges = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.65f, Noise.Fbm(d * 1.8f, 2, b.seed + 23)));
            h += ridge * ridge * ridge * 1600 * ranges;
            h += (Noise.Fbm(d * 40f, 3, b.seed + 25) - 0.5) * 260;
            return h;
        }

        /// <summary>
        /// Arden's land/sea field (below 0 sea, 0 to 0.025 beach, then grass, forest, highland, rock as it rises). Near
        /// the space centre it's the peninsula from captaincarl24's map (milestone 24): within 2 degrees of the pad the
        /// peninsula's field replaces the planet's own, fading back to it by 4 degrees. The ground's colours and photo
        /// textures, the glint on the water, the hills, regions and ore all read this, so they agree on the coast.
        /// </summary>
        public static float ArdenField(CelestialBody b, Vector3 d)
        {
            float h = (Noise.Fbm(d * 1.8f, 6, b.seed) - 0.5f) * 2.4f;
            if (!b.IsHomeWorld) return h;
            float padAngle = Vector3.Angle(d, LaunchSite.Direction.ToFloat());
            if (padAngle >= 4f) return h;
            return Mathf.Lerp(SiteField(b, d), h, Mathf.Clamp01((padAngle - 2f) / 2f));
        }

        /// <summary>
        /// The space centre's peninsula, traced from captaincarl24's map: the waterline as (east, north) metres from the pad,
        /// round the land: down the east coast (with a cape to the south-east), along the south coast, up the west coast
        /// (with a small bay near the top), then far round the mainland in the north. (East is on the left when you look
        /// north in the game, so this is his picture flipped east-west: it looks right from the sea.)
        /// </summary>
        static readonly float[] Shore =
        {
            9000, 1700,   4342, 1286,   3672, 1036,   3509, 632,   3151, 265,   2847, -87,   2625, -424,   2410, -741,
            2170, -1037,  1940, -1313,  1700, -1620,  1300, -1760,  900, -1600,  56, -1327,  -771, -1093,  -1687, -785,
            -2111, -566,  -2446, -315,  -2592, 0,   -2679, 362,   -2644, 735,   -2472, 1118,  -2269, 1518,  -2265, 1734,
            -2703, 1991,  -3204, 2257,  -3633, 2524,  -9000, 4500,  -9000, 40000,  9000, 40000,
        };

        /// <summary>
        /// The land/sea field round the space centre: grass well inland, a beach about 250 m wide at the shore (whose line
        /// wanders by a hundred metres or so: coves and points), and the sea deepening offshore.
        /// </summary>
        static float SiteField(CelestialBody b, Vector3 d)
        {
            Vector3 east = CelestialBody.EastAt(LaunchSite.Direction).ToFloat(), north = CelestialBody.NorthAt(LaunchSite.Direction).ToFloat();
            float R = (float)b.radius;
            float e = Vector3.Dot(d, east) * R, n = Vector3.Dot(d, north) * R;          // metres east and north of the pad
            // signed distance to the waterline: + on land
            bool inside = false;
            float best = float.MaxValue;
            int count = Shore.Length / 2;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float xi = Shore[i * 2], yi = Shore[i * 2 + 1], xj = Shore[j * 2], yj = Shore[j * 2 + 1];
                if ((yi > n) != (yj > n) && e < (xj - xi) * (n - yi) / (yj - yi) + xi) inside = !inside;
                float dx = xj - xi, dy = yj - yi, len2 = dx * dx + dy * dy;
                float t = len2 > 0f ? Mathf.Clamp01(((e - xi) * dx + (n - yi) * dy) / len2) : 0f;
                float px = xi + dx * t - e, py = yi + dy * t - n;
                best = Mathf.Min(best, px * px + py * py);
            }
            float inland = Mathf.Sqrt(best) * (inside ? 1f : -1f);
            inland += (Noise.Fbm(d * 1500f, 3, b.seed + 71) - 0.5f) * 300f;               // coves and points
            if (inland < 0f) return -0.32f * Mathf.SmoothStep(0f, 1f, -inland / 900f);
            if (inland < 250f) return 0.025f * inland / 250f;
            return Mathf.Lerp(0.025f, 0.12f, Mathf.SmoothStep(0f, 1f, (inland - 250f) / 150f));
        }

        /// <summary>
        /// Open water under this spot (milestone 25): Arden's seas (not the frozen ones near the poles, not the barge, pier or
        /// pads). A splashdown is gentler than hitting land.
        /// </summary>
        public static bool IsWater(CelestialBody b, Vector3d fixedDir)
        {
            if (b.style != BodyStyle.Earthlike) return false;
            Vector3 d = fixedDir.normalized.ToFloat();
            if (ArdenField(b, d) > -0.002f) return false;
            if (b.IsHomeWorld && LaunchSite.SurfaceHeight(b, fixedDir) > 0.5) return false;
            float lat = Mathf.Abs(d.y) + (Noise.Fbm(d * 6f, 3, b.seed + 9) - 0.5f) * 0.12f;
            return lat <= 0.85f;
        }

        /// <summary>How much harder a part can hit water than land before it breaks.</summary>
        public const float WaterCushion = 2.2f;

        static double ArdenHeight(CelestialBody b, Vector3 d, float field = float.NaN)
        {
            float h = float.IsNaN(field) ? ArdenField(b, d) : field;     // the same field the planet is painted with
            double land = h - 0.025;                                     // seas and beaches stay at sea level
            float padAngle = Vector3.Angle(d, LaunchSite.Direction.ToFloat());
            // Milestone 23: the space center sits in rolling green country, flat only where the pad, runway and
            // buildings are (about 3.5 km round the pad), instead of a 50 km plain
            double near = 0;
            if (padAngle < 4f)
            {
                float ring = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1.1f, padAngle)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, 4f, padAngle)));
                near = ring * (110 + (Noise.Fbm(d * 600f, 3, b.seed + 61) - 0.5) * 420 + (Noise.Fbm(d * 2600f, 2, b.seed + 63) - 0.5) * 40);
                near *= Mathf.Clamp01((h - 0.025f) / 0.05f);                                     // no hills out of the sea
            }
            if (land <= 0) return Math.Max(0, near);
            double height = Math.Pow(land, 1.5) * 4800;
            height += (Noise.Fbm(d * 140f, 3, b.seed + 41) - 0.5) * 170 * Math.Min(1, land * 10); // hills, fading at the coast
            // Smaller hills and knolls a few km across, and gentle bumps (milestone 23)
            height += ((Noise.Fbm(d * 700f, 3, b.seed + 45) - 0.5) * 90 + (Noise.Fbm(d * 3200f, 2, b.seed + 47) - 0.5) * 14) * Math.Min(1, land * 6);
            // Ridged ranges on high ground: sharp crests and valleys tens of km apart.
            double ridge = 1 - Math.Abs(2 * Noise.Fbm(d * 22f, 4, b.seed + 51) - 1);
            height += ridge * ridge * 1300 * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.45f, (float)land));
            if (padAngle < 4f) height = height * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((padAngle - 2f) / 2f)) + near;   // the big ranges still stay back
            else height += near;
            return Math.Max(0, height);
        }

        static double TalHeight(CelestialBody b, Vector3 d)
        {
            double hills = (Noise.Fbm(d * 2.2f, 5, b.seed + 1) - 0.5) * 2400;
            float maria = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.47f, 0.40f, Noise.Fbm(d * 1.3f, 3, b.seed + 5))); // the dark plains
            double h = 1200 + hills * (1 - 0.7 * maria) - 700 * maria;
            h += Craters(d, 5f, 900, b.seed + 7) + Craters(d, 17f, 240, b.seed + 13) + Craters(d, 55f, 110, b.seed + 19) + Craters(d, 170f, 30, b.seed + 29);
            h += Craters(d, 480f, 9, b.seed + 37);                                                  // milestone 26: lots of small craters
            h += (Noise.Fbm(d * 160f, 3, b.seed + 23) - 0.5) * 50;
            // Milestone 26: ranges of mountains across parts of the highlands (sharp ridged crests), and rolling hills
            double ridge = 1 - Math.Abs(2 * Noise.Fbm(d * 7f, 4, b.seed + 31) - 1);
            float ranges = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.62f, Noise.Fbm(d * 1.6f, 2, b.seed + 33)));
            double sizeK = Math.Sqrt(Math.Min(1.0, b.radius / 200_000.0));                         // smaller worlds, smaller mountains
            h += ridge * ridge * ridge * 2600 * sizeK * (1 - maria) * ranges;
            h += (Noise.Fbm(d * 60f, 3, b.seed + 35) - 0.5) * 420 * sizeK * (1 - 0.6 * maria);
            return h;
        }

        /// <summary>Bowl-shaped craters with raised rims, scattered through 3D cells around the sphere.</summary>
        public static double Craters(Vector3 d, float freq, double depth, int seed)
        {
            Vector3 p = d * freq;
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
            double total = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int cx = xi + dx, cy = yi + dy, cz = zi + dz;
                        if (Noise.Hash(cx, cy, cz, seed) > 0.55f) continue;
                        var c = new Vector3(cx + Noise.Hash(cx, cy, cz, seed + 1), cy + Noise.Hash(cx, cy, cz, seed + 2), cz + Noise.Hash(cx, cy, cz, seed + 3));
                        float rad = 0.16f + 0.3f * Noise.Hash(cx, cy, cz, seed + 4);
                        float x = (p - c).magnitude / rad;
                        if (x > 1.7f) continue;
                        double dep = depth * (0.6 + 0.8 * Noise.Hash(cx, cy, cz, seed + 5)) * (rad / 0.46f);
                        total += (x < 1 ? dep * (x * x - 1) : 0) + 0.28 * dep * Math.Exp(-((x - 1) / 0.22) * ((x - 1) / 0.22));
                    }
            return total;
        }
    }
}
