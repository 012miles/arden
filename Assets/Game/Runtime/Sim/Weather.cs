using System;
using UnityEngine;

namespace Rocket
{
    /// <summary>The weather at one place and time: wind (planet-fixed, along the ground), cloud, rain, dust.</summary>
    public struct WeatherNow
    {
        public double windSpeed;      // m/s at the ground
        public Vector3d windFixed;    // planet-fixed, horizontal, m/s (blowing toward)
        public float cloud, rain, dust;

        public bool Calm => windSpeed < 0.5 && cloud < 0.05f && rain < 0.05f && dust < 0.05f;

        /// <summary>"Rain, wind 14 m/s from the west".</summary>
        public string Describe(Vector3d upFixed)
        {
            string sky = dust > 0.6f ? "Dust storm" : dust > 0.2f ? "Blowing dust" : rain > 0.6f ? "Heavy rain" : rain > 0.15f ? "Rain" : cloud > 0.6f ? "Overcast" : cloud > 0.25f ? "Cloudy" : "Clear";
            return windSpeed < 1 ? $"{sky}, still air" : $"{sky}, wind {windSpeed:0} m/s from the {FromWhere(upFixed)}";
        }

        string FromWhere(Vector3d upFixed)
        {
            if (windSpeed < 1e-3) return "nowhere";
            Vector3d e = CelestialBody.EastAt(upFixed), n = CelestialBody.NorthAt(upFixed);
            // where it comes FROM: the opposite of where it blows
            double a = Math.Atan2(-Vector3d.Dot(windFixed, e), -Vector3d.Dot(windFixed, n)) * 180 / Math.PI;
            string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return names[(int)Math.Round(((a + 360) % 360) / 45) % 8];
        }
    }

    /// <summary>
    /// Milestone 27: weather on the worlds with air. Arden has drifting weather systems: clear skies, cloud, rain and
    /// stronger wind in storms. Ochre has dust storms that roll across the desert (its thin air pushes much less, but
    /// you can't see far). Wind pushes ships in the lower air, more with height up to a few km, fading out by 25 km;
    /// the countdown holds when it's over 15 m/s at the pad. It all follows time and place, so it's the same in every
    /// game and needs no saving. Off in the tests unless a test turns it on.
    /// Milestone 34: each game shifts it by its own seed (GameSeed), so new games don't all start in the same storm.
    /// </summary>
    public static class Weather
    {
        public static bool On = true;
        /// <summary>The game's own weather (milestone 34: set by its universe; 0 = the fixed weather the tests use).</summary>
        public static int GameSeed;
        /// <summary>For the screenshot tours: the weather everywhere, whatever the noise says.</summary>
        public static WeatherNow? Override;

        public const double LaunchWindLimit = 15, WindTop = 25_000;
        const double Hours = 3600;

        public static bool Has(CelestialBody b) => b != null && b.HasAtmosphere && (b.style == BodyStyle.Earthlike || b.style == BodyStyle.Desert);

        public static WeatherNow At(CelestialBody b, Vector3d fixedDir, double ut)
        {
            if (Override.HasValue) return Override.Value;
            var w = new WeatherNow();
            if (!Has(b)) return w;
            Vector3d dd = fixedDir.normalized;
            Vector3 d = dd.ToFloat();
            // weather systems drift across the world and change over hours
            float t = (float)((ut / Hours / 9.0) % 4096.0);
            int gs = GameSeed;
            Vector3 shift = gs == 0 ? Vector3.zero : new Vector3((gs % 977) * 0.731f, (gs % 613) * 0.417f, (gs % 389) * 0.529f);   // this game's weather
            Vector3 drift = new Vector3(t * 0.37f, t * 0.21f, -t * 0.29f) + shift;
            float storm = Noise.Fbm(d * 2.2f + drift, 3, b.seed + 701 + gs);
            float gust = Noise.Fbm(d * 5f + drift * 1.7f, 2, b.seed + 703 + gs);
            float turn = Noise.Fbm(d * 1.3f + drift * 0.5f, 2, b.seed + 705 + gs);
            if (b.style == BodyStyle.Earthlike)
            {
                // (milestone 34: storms a little rarer: mostly clear or cloudy, rain now and then)
                w.cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(gs == 0 ? 0.44f : 0.48f, gs == 0 ? 0.6f : 0.63f, storm));
                w.rain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(gs == 0 ? 0.56f : 0.6f, gs == 0 ? 0.68f : 0.7f, storm));
                w.windSpeed = 2 + 10 * Mathf.Clamp01((gust - 0.3f) / 0.4f) + 14 * w.rain;
            }
            else
            {
                w.dust = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.7f, storm));
                w.windSpeed = 5 + 12 * Mathf.Clamp01((gust - 0.3f) / 0.4f) + 30 * w.dust;
            }
            double ang = turn * Math.PI * 5;
            Vector3d e = CelestialBody.EastAt(dd), n = CelestialBody.NorthAt(dd);
            w.windFixed = (e * Math.Cos(ang) + n * Math.Sin(ang)) * w.windSpeed;
            return w;
        }

        /// <summary>How much of the ground wind there is at a height: a bit less at the ground, more up high, none above 25 km.</summary>
        public static double WindScale(double alt)
        {
            if (alt >= WindTop) return 0;
            double k = Math.Min(2.2, 0.8 + Math.Max(0, alt) / 4000);
            if (alt > 12_000) k *= 1 - (alt - 12_000) / (WindTop - 12_000);
            return k;
        }

        /// <summary>The wind (inertial frame, m/s, on top of the air turning with the world) at a vessel's position.</summary>
        public static Vector3d WindInertial(CelestialBody b, Vector3d posInertial, double ut)
        {
            if (!On || !Has(b)) return Vector3d.zero;
            double alt = posInertial.magnitude - b.radius;
            double k = WindScale(alt);
            if (k <= 0) return Vector3d.zero;
            var w = At(b, b.InertialToFixed(posInertial, ut), ut);
            return b.FixedToInertial(w.windFixed * k, ut);
        }
    }
}
