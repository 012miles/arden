using System;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Named regions of each world, worked out from the same noise that paints and shapes it, so a region is what you
    /// see: Arden's oceans, shores, grasslands, forests, highlands, mountains and ice caps; Tal's bright highlands, low
    /// hills, dark plains and great craters; Vesk's ice plains, blue ridges, dust fields and ice craters. Science taken
    /// landed or flying low counts separately in each region.
    /// </summary>
    public static class Regions
    {
        static readonly string[] Arden = { "Deep Ocean", "Shallows", "Shores", "Grasslands", "Forests", "Highlands", "Mountains", "Ice Caps" };
        static readonly string[] Tal = { "Bright Highlands", "Low Hills", "Dark Plains", "Great Craters" };
        static readonly string[] Vesk = { "Ice Plains", "Blue Ridges", "Dust Fields", "Ice Craters" };
        static readonly string[] Ochre = { "Rust Plains", "Dune Seas", "Canyons", "Highlands", "Polar Ice" };
        static readonly string[] Mote = { "Ochre Face", "Far Face", "Rim" };
        static readonly string[] Brume = { "Equatorial Belt", "Storm Bands", "Temperate Zones", "Polar Haze" };
        static readonly string[] Rime = { "Frost Plains", "Rift Ridges", "Dust Fields", "Frost Craters" };
        static readonly string[] Pyre = { "Lava Fields", "Ash Plains", "Volcanoes", "Calderas" };
        static readonly string[] None = { "Surface" };
        static readonly Color[] BrumeColors = { new Color(0.95f, 0.85f, 0.6f), new Color(0.85f, 0.4f, 0.25f), new Color(0.55f, 0.75f, 0.7f), new Color(0.8f, 0.85f, 0.95f) };
        static readonly Color[] PyreColors = { new Color(0.15f, 0.12f, 0.12f), new Color(0.65f, 0.62f, 0.6f), new Color(0.6f, 0.3f, 0.2f), new Color(1f, 0.5f, 0.1f) };

        static readonly Color[] ArdenColors =
        {
            new Color(0.08f, 0.12f, 0.55f), new Color(0.2f, 0.85f, 0.95f), new Color(1f, 0.92f, 0.3f), new Color(0.6f, 1f, 0.25f),
            new Color(0.05f, 0.45f, 0.15f), new Color(1f, 0.55f, 0.15f), new Color(0.75f, 0.3f, 0.9f), new Color(0.97f, 0.98f, 1f),
        };
        static readonly Color[] TalColors = { new Color(0.95f, 0.85f, 0.45f), new Color(0.55f, 0.85f, 0.5f), new Color(0.4f, 0.45f, 0.95f), new Color(0.95f, 0.4f, 0.35f) };
        static readonly Color[] OchreColors = { new Color(0.85f, 0.4f, 0.2f), new Color(1f, 0.85f, 0.45f), new Color(0.45f, 0.2f, 0.65f), new Color(0.55f, 0.75f, 0.3f), new Color(0.95f, 0.97f, 1f) };
        static readonly Color[] MoteColors = { new Color(1f, 0.6f, 0.3f), new Color(0.4f, 0.55f, 0.95f), new Color(0.75f, 0.75f, 0.75f) };
        static readonly Color[] VeskColors = { new Color(0.75f, 0.9f, 1f), new Color(0.25f, 0.45f, 0.95f), new Color(0.9f, 0.7f, 0.4f), new Color(0.95f, 0.45f, 0.6f) };

        public static string[] Names(string body) => body == "Arden" ? Arden : body == "Tal" ? Tal : body == "Vesk" ? Vesk : body == "Ochre" ? Ochre : body == "Mote" ? Mote
            : body == "Brume" ? Brume : body == "Rime" ? Rime : body == "Pyre" ? Pyre : None;
        public static string[] Names(CelestialBody b) => Names(b.name);

        public static Color ColorOf(CelestialBody b, int i)
        {
            var t = b.name == "Arden" ? ArdenColors : b.name == "Tal" ? TalColors : b.name == "Vesk" ? VeskColors : b.name == "Ochre" ? OchreColors : b.name == "Mote" ? MoteColors
                : b.name == "Brume" ? BrumeColors : b.name == "Rime" ? VeskColors : b.name == "Pyre" ? PyreColors : null;
            return t != null && i >= 0 && i < t.Length ? t[i] : Color.gray;
        }

        /// <summary>Region index at a planet-fixed direction.</summary>
        public static int IndexAt(CelestialBody b, Vector3d fixedDir)
        {
            Vector3 d = fixedDir.normalized.ToFloat();
            switch (b.name)
            {
                case "Arden": return ArdenAt(b, d);
                case "Tal": return TalAt(b, d);
                case "Vesk": return VeskAt(b, d);
                case "Ochre": return OchreAt(b, d);
                case "Mote": return MoteAt(b, d);
                case "Brume": return BrumeAt(b, d);
                case "Rime": return VeskAt(b, d);   // the same kind of ice world as Vesk, so the same rules
                case "Pyre": return PyreAt(b, d);
                default: return 0;
            }
        }

        public static string At(CelestialBody b, Vector3d fixedDir) => Names(b)[IndexAt(b, fixedDir)];

        /// <summary>The region under a vessel.</summary>
        public static string Under(VesselSim v, double ut) => At(v.body, v.body.InertialToFixed(v.pos, ut));

        // Matches MeshFactory.PlanetColor: same height field, same bands, same snow line.
        static int ArdenAt(CelestialBody b, Vector3 d)
        {
            int seed = b.seed;
            float h = Terrain.ArdenField(b, d);
            float lat = Mathf.Abs(d.y) + (Noise.Fbm(d * 6f, 3, seed + 9) - 0.5f) * 0.12f;
            if (lat > 0.85f) return 7;
            if (h < -0.22f) return 0;
            if (h < 0f) return 1;
            if (h < 0.025f) return 2;
            if (h < 0.2f) return Noise.Fbm(d * 9f, 3, seed + 3) > 0.5f ? 4 : 3;
            if (h < 0.38f) return 5;
            return 6;
        }

        // Matches Terrain.TalHeight and MeshFactory.BarrenColor: crater floors of the biggest craters, the dark maria, then
        // high or low ground.
        static int TalAt(CelestialBody b, Vector3 d)
        {
            if (Terrain.Craters(d, 5f, 900, b.seed + 7) < -280) return 3;
            float maria = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.47f, 0.40f, Noise.Fbm(d * 1.3f, 3, b.seed + 5)));
            if (maria > 0.5f) return 2;
            double hills = Noise.Fbm(d * 2.2f, 5, b.seed + 1) - 0.5;
            return hills > 0 ? 0 : 1;
        }

        // Matches Terrain.DesertHeight and MeshFactory.DesertColor.
        static int OchreAt(CelestialBody b, Vector3 d)
        {
            int seed = b.seed;
            float lat = Mathf.Abs(d.y) + (Noise.Fbm(d * 5f, 3, seed + 9) - 0.5f) * 0.1f;
            if (lat > 0.89f) return 4;
            if (Terrain.CanyonDepth(b, d) > 0.35f) return 2;
            float dunes = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.62f, Noise.Fbm(d * 3f, 3, seed + 8)));
            if (dunes > 0.5f) return 1;
            if (Noise.Fbm(d * 2.0f, 5, seed + 1) > 0.58f) return 3;
            return 0;
        }

        /// <summary>Brume's cloud bands by latitude, with the same wobble its colours use.</summary>
        public static float BrumeLatitude(CelestialBody b, Vector3 d) => Mathf.Abs(d.y + (Noise.Fbm(d * 4f, 3, b.seed + 2) - 0.5f) * 0.08f);

        static int BrumeAt(CelestialBody b, Vector3 d)
        {
            float lat = BrumeLatitude(b, d);
            return lat < 0.18f ? 0 : lat < 0.45f ? 1 : lat < 0.8f ? 2 : 3;
        }

        // Matches Terrain.VolcanicHeight and MeshFactory.VolcanicColor.
        static int PyreAt(CelestialBody b, Vector3 d)
        {
            Terrain.VolcanoField(d, 3.2f, 2800, b.seed + 3, out float caldera);
            if (caldera > 0.05f) return 3;
            if (Terrain.Volcanoes(d, 3.2f, 2800, b.seed + 3) > 500) return 2;
            return Terrain.LavaFields(b, d) > 0.5f ? 0 : 1;
        }

        // Mote always keeps one face to Ochre (it is tidally locked), so its regions are the near face, the far face and the rim between.
        static int MoteAt(CelestialBody b, Vector3 d)
        {
            Vector3 face = b.parent == null ? Vector3.right : b.InertialToFixed(-b.PositionAt(0).normalized, 0).ToFloat();
            float k = Vector3.Dot(d, face);
            return k > 0.35f ? 0 : k < -0.35f ? 1 : 2;
        }

        // Matches Terrain.VeskHeight and MeshFactory.IcyColor.
        static int VeskAt(CelestialBody b, Vector3 d)
        {
            if (Terrain.Craters(d, 7f, 380, b.seed + 3) < -110) return 3;
            float crack = 1f - Mathf.Abs(2f * Noise.Fbm(d * 9f, 3, b.seed + 2) - 1f);
            if (crack > 0.95f) return 1;
            float dust = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.56f, 0.7f, Noise.Fbm(d * 2.2f, 3, b.seed + 7)));
            if (dust > 0.5f) return 2;
            return 0;
        }
    }
}
