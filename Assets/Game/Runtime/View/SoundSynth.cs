using System;

namespace Rocket
{
    /// <summary>
    /// Makes every sound in the game from scratch (no audio files): engine rumble, jet whine, wind, the RCS hiss,
    /// staging clunks, docking clanks, explosions and parachute whooshes. Loops are cross-faded so they repeat
    /// without a click. Plain C# (float arrays), so it can be checked without an audio device.
    /// </summary>
    public static class SoundSynth
    {
        public const int Rate = 44100;

        static float[] Make(float seconds) => new float[(int)(seconds * Rate)];

        /// <summary>Blends the last part of a loop into its start so it repeats seamlessly, then trims that part off.</summary>
        static float[] MakeLoop(float[] s, float fadeSeconds)
        {
            int f = (int)(fadeSeconds * Rate), n = s.Length - f;
            var o = new float[n];
            Array.Copy(s, o, n);
            for (int i = 0; i < f; i++)
            {
                float t = (float)i / f;
                o[i] = s[i] * t + s[n + i] * (1 - t);
            }
            return o;
        }

        static void Normalize(float[] s, float peak)
        {
            float m = 1e-6f;
            foreach (var x in s) m = Math.Max(m, Math.Abs(x));
            for (int i = 0; i < s.Length; i++) s[i] *= peak / m;
        }

        /// <summary>Deep rocket roar: brown noise with a throbbing low end.</summary>
        public static float[] Rumble(int seed = 1)
        {
            var r = new Random(seed);
            var s = Make(3.2f);
            double brown = 0, lp = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double w = r.NextDouble() * 2 - 1;
                brown = (brown + 0.03 * w) / 1.03;
                lp += (w - lp) * 0.08;                       // a little grit on top
                double t = (double)i / Rate;
                double throb = 1 + 0.25 * Math.Sin(2 * Math.PI * 7.3 * t) * Math.Sin(2 * Math.PI * 0.9 * t);
                s[i] = (float)((brown * 6 + lp * 0.35) * throb);
            }
            Normalize(s, 0.9f);
            return MakeLoop(s, 0.2f);
        }

        /// <summary>Turbojet whine: a few tones over a band of noise.</summary>
        public static float[] Jet(int seed = 2)
        {
            var r = new Random(seed);
            var s = Make(2.2f);
            double lp = 0, hp = 0, prev = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double t = (double)i / Rate, w = r.NextDouble() * 2 - 1;
                lp += (w - lp) * 0.3;
                hp = 0.97 * (hp + lp - prev); prev = lp;
                double tones = Math.Sin(2 * Math.PI * 1170 * t) * 0.25 + Math.Sin(2 * Math.PI * 2343 * t) * 0.1 + Math.Sin(2 * Math.PI * 587 * t) * 0.15;
                s[i] = (float)(tones + hp * 0.8);
            }
            Normalize(s, 0.7f);
            return MakeLoop(s, 0.15f);
        }

        /// <summary>Rushing air: pink-ish noise with slow gusts.</summary>
        public static float[] Wind(int seed = 3)
        {
            var r = new Random(seed);
            var s = Make(4.5f);
            double b0 = 0, b1 = 0, b2 = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double w = r.NextDouble() * 2 - 1, t = (double)i / Rate;
                b0 = 0.997 * b0 + w * 0.029591; b1 = 0.985 * b1 + w * 0.032534; b2 = 0.95 * b2 + w * 0.048056;
                double gust = 0.75 + 0.25 * Math.Sin(2 * Math.PI * 0.43 * t + Math.Sin(2 * Math.PI * 0.17 * t) * 2);
                s[i] = (float)((b0 + b1 + b2) * gust);
            }
            Normalize(s, 0.75f);
            return MakeLoop(s, 0.4f);
        }

        /// <summary>Gas hiss for RCS thrusters and the jetpack.</summary>
        public static float[] Hiss(int seed = 4)
        {
            var r = new Random(seed);
            var s = Make(1.6f);
            double prev = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double w = r.NextDouble() * 2 - 1;
                s[i] = (float)(w - prev * 0.6);
                prev = w;
            }
            Normalize(s, 0.5f);
            return MakeLoop(s, 0.1f);
        }

        static float Env(double t, double attack, double decay) => (float)((t < attack ? t / attack : 1) * Math.Exp(-Math.Max(0, t - attack) / decay));

        /// <summary>Staging: a heavy thump with a sharp click.</summary>
        public static float[] Clunk(int seed = 5)
        {
            var r = new Random(seed);
            var s = Make(0.6f);
            for (int i = 0; i < s.Length; i++)
            {
                double t = (double)i / Rate;
                double thump = Math.Sin(2 * Math.PI * (75 - 30 * t) * t) * Env(t, 0.004, 0.09);
                double click = (r.NextDouble() * 2 - 1) * Env(t, 0.0005, 0.012);
                s[i] = (float)(thump + click * 0.6);
            }
            Normalize(s, 0.9f);
            return s;
        }

        /// <summary>Docking latch: a ringing metallic clank.</summary>
        public static float[] Clank(int seed = 6)
        {
            var r = new Random(seed);
            var s = Make(1.1f);
            double[] f = { 410, 1135, 2207, 3390 }, a = { 1, 0.6, 0.35, 0.2 }, d = { 0.35, 0.22, 0.14, 0.08 };
            for (int i = 0; i < s.Length; i++)
            {
                double t = (double)i / Rate, x = 0;
                for (int k = 0; k < f.Length; k++) x += a[k] * Math.Sin(2 * Math.PI * f[k] * t) * Env(t, 0.001, d[k]);
                x += (r.NextDouble() * 2 - 1) * Env(t, 0.0005, 0.01) * 0.8;
                s[i] = (float)x;
            }
            Normalize(s, 0.8f);
            return s;
        }

        /// <summary>Explosion: a crack, then a long rumbling roll.</summary>
        public static float[] Boom(int seed = 7)
        {
            var r = new Random(seed);
            var s = Make(3.0f);
            double brown = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double t = (double)i / Rate, w = r.NextDouble() * 2 - 1;
                brown = (brown + 0.05 * w) / 1.05;
                double low = Math.Sin(2 * Math.PI * (55 - 25 * t / 3) * t) * Env(t, 0.01, 0.6);
                s[i] = (float)(brown * 8 * Env(t, 0.003, 0.7) + w * Env(t, 0.001, 0.05) * 0.7 + low * 0.6);
            }
            Normalize(s, 1f);
            return s;
        }

        /// <summary>Parachute opening: a rising, then fading whoosh.</summary>
        public static float[] Whoosh(int seed = 8)
        {
            var r = new Random(seed);
            var s = Make(1.4f);
            double lp = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double t = (double)i / Rate, w = r.NextDouble() * 2 - 1;
                lp += (w - lp) * (0.05 + 0.25 * Math.Min(1, t / 0.4));
                double env = Math.Sin(Math.PI * Math.Min(1, t / 1.4)) * (t < 0.25 ? t / 0.25 : 1);
                s[i] = (float)(lp * env);
            }
            Normalize(s, 0.7f);
            return s;
        }
    }
}
