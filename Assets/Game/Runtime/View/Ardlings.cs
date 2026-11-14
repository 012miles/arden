using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// The crew (milestone 20): Ardlings, small folk in big round helmets whose visors are blacked out. Nobody has
    /// seen what's inside; each one sticks a hand-drawn sticky note on the visor instead. Everyone has their own suit
    /// colour, note face and (sometimes) a hat, picked from their name so they always look the same.
    /// Body: top of the helmet at y = 0, feet at y = -Height, facing +Z, backpack toward -Z.
    /// </summary>
    public static class Ardlings
    {
        public const float Height = 1.2f;
        const float HelmetR = 0.31f;
        static readonly Vector3 HelmetC = new Vector3(0, -HelmetR, 0);

        public struct Look
        {
            public Color suit, trim, paper;
            public int face, hat;
            public float noteTilt;
        }

        static readonly Color[] Suits =
        {
            new Color(0.93f, 0.55f, 0.18f), new Color(0.13f, 0.55f, 0.58f), new Color(0.78f, 0.2f, 0.18f), new Color(0.2f, 0.38f, 0.75f),
            new Color(0.5f, 0.3f, 0.68f), new Color(0.92f, 0.78f, 0.22f), new Color(0.3f, 0.6f, 0.3f), new Color(0.9f, 0.9f, 0.88f),
            new Color(0.9f, 0.5f, 0.62f), new Color(0.16f, 0.2f, 0.32f), new Color(0.55f, 0.4f, 0.28f), new Color(0.35f, 0.75f, 0.85f),
        };
        static readonly Color[] Papers =
        {
            new Color(1f, 0.93f, 0.38f), new Color(1f, 0.66f, 0.78f), new Color(0.6f, 0.83f, 1f), new Color(0.66f, 0.95f, 0.58f), new Color(1f, 0.76f, 0.4f),
        };
        public const int Faces = 10, Hats = 9;
        public static readonly string[] FaceNames = { "smile", "grin", "happy", "wink", "surprised", "determined", "sleepy", "cheeky", "cool", "worried" };
        public static readonly string[] HatNames = { "none", "beanie", "party hat", "propeller cap", "antenna", "wide-brim hat", "crown", "flower", "top hat" };

        static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s ?? "") { h ^= c; h *= 16777619; }
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h;
        }

        /// <summary>Everyone's look comes from their name, so it's the same every flight.</summary>
        public static Look LookFor(string name)
        {
            uint h = Hash(name);
            var l = new Look
            {
                suit = Suits[h % (uint)Suits.Length],
                paper = Papers[(h >> 5) % (uint)Papers.Length],
                face = (int)((h >> 9) % Faces),
                hat = (h >> 14) % 10 < 4 ? 0 : 1 + (int)((h >> 18) % (Hats - 1)),
                noteTilt = ((h >> 22) % 21) - 10f,
            };
            l.trim = Color.Lerp(l.suit, Color.black, 0.35f);
            if (l.suit.grayscale > 0.8f) l.trim = new Color(0.15f, 0.5f, 0.55f);
            return l;
        }

        // ---------------------------------------------------------------- the body

        static readonly Dictionary<string, Mesh> bodies = new Dictionary<string, Mesh>();

        /// <summary>Where the arms and legs swing from (body frame), for the walk animation.</summary>
        public static readonly Vector3 ShoulderL = new Vector3(-0.19f, -0.7f, 0), ShoulderR = new Vector3(0.19f, -0.7f, 0);
        public static readonly Vector3 HipL = new Vector3(-0.09f, -0.95f, 0), HipR = new Vector3(0.09f, -0.95f, 0);

        static readonly Dictionary<string, Mesh> limbMeshes = new Dictionary<string, Mesh>();

        /// <summary>One arm (side -1 left, +1 right) built around its shoulder, so it can swing.</summary>
        public static Mesh Arm(Look look, int side)
        {
            string key = $"arm{side}|{look.suit}|{look.trim}|{look.paper}|{lite}";
            if (limbMeshes.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            AddArm(b, look, side, -(side < 0 ? ShoulderL : ShoulderR));
            m = b.ToMesh("Ardling arm");
            limbMeshes[key] = m;
            return m;
        }

        /// <summary>One leg and boot built around its hip.</summary>
        public static Mesh Leg(Look look, int side)
        {
            string key = $"leg{side}|{look.suit}|{look.trim}|{lite}";
            if (limbMeshes.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            AddLeg(b, look, side, -(side < 0 ? HipL : HipR));
            m = b.ToMesh("Ardling leg");
            limbMeshes[key] = m;
            return m;
        }

        static Color Shade(Color c, float k) { var r = Color.Lerp(Color.black, c, k); r.a = 1f; return r; }

        // Distant Ardlings (milestone 24): the same suit in far fewer triangles, without the small details
        static bool lite;
        static int S(int n) => lite ? Mathf.Max(6, n / 3) : n;

        /// <summary>The far-away versions of the body and limbs, for crowds (about a tenth of the triangles).</summary>
        public static Mesh LiteBody(Look look) { lite = true; try { return Body(look, false); } finally { lite = false; } }
        public static Mesh LiteArm(Look look, int side) { lite = true; try { return Arm(look, side); } finally { lite = false; } }
        public static Mesh LiteLeg(Look look, int side) { lite = true; try { return Leg(look, side); } finally { lite = false; } }

        static void AddArm(MeshBuilder b, Look look, int side, Vector3 off)
        {
            float sd = side;
            Vector3 shoulder = new Vector3(0.19f * sd, -0.7f, 0) + off;
            Vector3 elbow = new Vector3(0.238f * sd, -0.822f, -0.014f) + off;
            Vector3 wrist = new Vector3(0.262f * sd, -0.922f, 0.032f) + off;
            Vector3 down = (wrist - elbow).normalized;
            Color shade = Shade(look.suit, 0.8f);
            b.Mat = MeshBuilder.MatMatte;
            // A padded shoulder, the upper arm, folds at the elbow, the forearm
            RoundBox(b, shoulder + new Vector3(0.014f * sd, 0.01f, 0), new Vector3(0.082f, 0.072f, 0.084f), 2.3f, look.suit, Quaternion.identity, 0f, 18, 10);
            Limb(b, shoulder, elbow, 0.064f, 0.056f, look.suit);
            if (!lite) Bellows(b, elbow, ((elbow - shoulder).normalized + down).normalized, 0.058f, 3, 0.017f, shade);
            Limb(b, elbow, wrist, 0.056f, 0.049f, look.suit);
            b.Mat = MeshBuilder.MatPaint;
            if (lite) { }
            else if (side < 0)
            {
                // Mission patch on the left arm: a card in the note's colour with a trim border
                Vector3 at = Vector3.Lerp(shoulder, elbow, 0.5f) + new Vector3(0.061f * sd, 0, 0.004f);
                Quaternion q = Quaternion.LookRotation(new Vector3(sd, 0, 0), (shoulder - elbow).normalized);
                b.AddBox(at, new Vector3(0.05f, 0.046f, 0.006f), look.trim, q);
                b.AddBox(at + q * new Vector3(0, 0, 0.003f), new Vector3(0.038f, 0.034f, 0.004f), look.paper, q);
            }
            else
            {
                // Wrist pad on the right forearm with a little screen
                Vector3 at = Vector3.Lerp(elbow, wrist, 0.6f) + new Vector3(0.012f * sd, 0, 0.044f);
                Quaternion q = Quaternion.LookRotation(Vector3.forward, -down);
                b.AddBox(at, new Vector3(0.05f, 0.045f, 0.018f), new Color(0.6f, 0.62f, 0.66f), q);
                b.Mat = MeshBuilder.MatLamp;
                b.AddBox(at + q * new Vector3(0, 0.002f, 0.01f), new Vector3(0.034f, 0.026f, 0.003f), new Color(0.3f, 0.85f, 0.95f), q);
                b.Mat = MeshBuilder.MatPaint;
            }
            // Cuff and a mitten glove with a thumb
            Ring(b, wrist, down, 0.056f, 0.03f, look.trim);
            b.Mat = MeshBuilder.MatRubber;
            Color glove = new Color(0.3f, 0.31f, 0.33f);
            Vector3 hand = wrist + down * 0.052f + new Vector3(0, 0, 0.004f);
            RoundBox(b, hand, new Vector3(0.033f, 0.05f, 0.043f), 2.4f, glove, Quaternion.FromToRotation(Vector3.up, down), 0f, 16, 10);
            if (!lite) Limb(b, hand + new Vector3(-0.022f * sd, 0.018f, 0.028f), hand + new Vector3(-0.03f * sd, -0.014f, 0.046f), 0.016f, 0.014f, glove);
            b.Mat = MeshBuilder.MatMatte;
        }

        static void AddLeg(MeshBuilder b, Look look, int side, Vector3 off)
        {
            float sd = side;
            Vector3 hip = new Vector3(0.09f * sd, -0.95f, 0) + off;
            Vector3 knee = new Vector3(0.098f * sd, -1.062f, 0.016f) + off;
            Vector3 ankle = new Vector3(0.1f * sd, -1.13f, 0.004f) + off;
            b.Mat = MeshBuilder.MatMatte;
            Limb(b, hip, knee, 0.08f, 0.069f, look.suit);
            Limb(b, knee, ankle, 0.068f, 0.062f, look.suit);
            b.Mat = MeshBuilder.MatPaint;
            RoundBox(b, knee + new Vector3(0.004f * sd, 0.006f, 0.052f), new Vector3(0.05f, 0.046f, 0.024f), 3f, look.trim, Quaternion.Euler(-10f, 0, 0), 0f, 14, 8);  // knee pad
            Ring(b, ankle + new Vector3(0, -0.004f, 0), Vector3.up, 0.066f, 0.026f, Shade(look.suit, 0.75f));                                       // boot cuff
            // A chunky moon boot: white upper, a coloured toe cap and a dark tread
            Color bootCol = new Color(0.88f, 0.89f, 0.9f);
            RoundBox(b, new Vector3(0.1f * sd, -1.15f, 0.026f) + off, new Vector3(0.074f, 0.044f, 0.1f), 2.8f, bootCol, Quaternion.identity, 0f, 18, 10);
            RoundBox(b, new Vector3(0.1f * sd, -1.158f, 0.096f) + off, new Vector3(0.064f, 0.034f, 0.036f), 2.6f, look.trim, Quaternion.identity, 0f, 14, 8);
            b.Mat = MeshBuilder.MatRubber;
            RoundBox(b, new Vector3(0.1f * sd, -1.188f, 0.028f) + off, new Vector3(0.079f, 0.013f, 0.106f), 4f, new Color(0.17f, 0.17f, 0.18f), Quaternion.identity, 0f, 18, 8);
            b.Mat = MeshBuilder.MatMatte;
        }

        /// <summary>The whole Ardling, or (limbs = false) just the helmet, body, backpack and hat.</summary>
        public static Mesh Body(Look look, bool limbs = true)
        {
            string key = $"{look.suit}|{look.trim}|{look.hat}|{look.paper}|{limbs}|{lite}";
            if (bodies.TryGetValue(key, out var cached)) return cached;
            var b = new MeshBuilder();
            Color white = new Color(0.94f, 0.95f, 0.96f), metal = new Color(0.78f, 0.79f, 0.82f), dark = new Color(0.2f, 0.21f, 0.23f);

            // Helmet: glossy shell, a stripe over the crown, ear pieces, a lamp, the blacked-out visor and its rim
            b.Mat = MeshBuilder.MatGloss;
            b.AddSphere(HelmetC, HelmetR, white, S(44), S(26));
            b.Mat = MeshBuilder.MatPaint;
            int s = b.v.Count;
            b.AddFrustum(0.032f, -0.032f, HelmetR * 1.01f, HelmetR * 1.01f, look.trim, S(40), angFrom: -150f * Mathf.Deg2Rad, angTo: 26f * Mathf.Deg2Rad);
            b.Transform(s, Quaternion.Euler(0, 0, 90f), HelmetC);
            foreach (int side in new[] { -1, 1 })
            {
                Quaternion q = Quaternion.Euler(0, 0, -90f * side);
                Vector3 at = HelmetC + new Vector3(side * (HelmetR - 0.006f), -0.025f, -0.02f);
                s = b.v.Count;
                b.AddFrustum(0.024f, -0.024f, 0.072f, 0.078f, look.trim, S(24), capTop: true);
                b.Transform(s, q, at);
                if (lite) continue;
                b.Mat = MeshBuilder.MatMetal;
                s = b.v.Count;
                b.AddFrustum(0.034f, 0.022f, 0.044f, 0.05f, metal, 20, capTop: true);
                b.Transform(s, q, at);
                b.Mat = MeshBuilder.MatPaint;
            }
            b.Mat = MeshBuilder.MatPaint;
            if (!lite)
            {
            s = b.v.Count;
            b.Mat = MeshBuilder.MatMetal;
            b.AddFrustum(0.06f, -0.01f, 0.024f, 0.026f, metal, 14, capTop: true);
            b.Transform(s, Quaternion.Euler(90f, 0, 0), HelmetC + new Vector3(0.262f, 0.12f, -0.035f));
            s = b.v.Count;
            b.Mat = MeshBuilder.MatLamp;
            b.AddFrustum(0.064f, 0.058f, 0.02f, 0.02f, new Color(1f, 0.95f, 0.8f), 14, capTop: true);
            b.Transform(s, Quaternion.Euler(90f, 0, 0), HelmetC + new Vector3(0.262f, 0.12f, -0.035f));
            }
            float th = Mathf.PI * 0.34f;
            s = b.v.Count;
            b.Mat = MeshBuilder.MatGlass;
            b.AddSphere(Vector3.zero, HelmetR * 1.025f, new Color(0.015f, 0.016f, 0.02f), S(36), lite ? 4 : 12, th);
            Squash(b, s, Quaternion.Euler(84f, 0, 0), new Vector3(1.12f, 0.86f, 1f));
            s = b.v.Count;
            b.Mat = MeshBuilder.MatMetal;
            float ry = HelmetR * 1.025f * Mathf.Cos(th), rr = HelmetR * 1.025f * Mathf.Sin(th);
            b.AddFrustum(ry + 0.012f, ry - 0.018f, rr * 1.03f, rr * 1.06f, metal, S(36));
            Squash(b, s, Quaternion.Euler(84f, 0, 0), new Vector3(1.12f, 0.86f, 1f));

            // Neck seal: a metal ring with a coloured band
            b.AddFrustum(-0.585f, -0.64f, 0.152f, 0.168f, metal, S(28), capTop: true);
            b.Mat = MeshBuilder.MatPaint;
            b.AddFrustum(-0.62f, -0.645f, 0.172f, 0.174f, look.trim, S(28));

            // Torso: a soft, slightly tapered suit, darker low down where it creases
            b.Mat = MeshBuilder.MatMatte;
            s = b.v.Count;
            RoundBox(b, new Vector3(0, -0.79f, 0), new Vector3(0.205f, 0.2f, 0.17f), 2.3f, look.suit, Quaternion.identity, 0.1f, 30, 18);
            for (int i = s; i < b.v.Count; i++) b.c[i] = Shade(look.suit, Mathf.Lerp(0.8f, 1f, Mathf.InverseLerp(-0.98f, -0.84f, b.v[i].y)));
            // Belt with a buckle and two pouches
            b.Mat = MeshBuilder.MatPaint;
            s = b.v.Count;
            b.AddFrustum(0.022f, -0.022f, 1f, 1f, look.trim, S(32));
            ScaleMove(b, s, new Vector3(0.183f, 1f, 0.161f), new Vector3(0, -0.895f, 0));
            b.Mat = MeshBuilder.MatMetal;
            b.AddBox(new Vector3(0, -0.895f, 0.162f), new Vector3(0.05f, 0.036f, 0.012f), metal, Quaternion.identity);
            b.Mat = MeshBuilder.MatMatte;
            if (!lite) foreach (int side in new[] { -1, 1 })
                RoundBox(b, new Vector3(0.148f * side, -0.905f, 0.075f), new Vector3(0.04f, 0.036f, 0.026f), 3f, Shade(look.suit, 0.72f), Quaternion.Euler(0, 38f * side, 0), 0f, 14, 8);
            // Chest control box with buttons and lights, and two ribbed hoses up to the neck seal
            b.Mat = MeshBuilder.MatPaint;
            Quaternion tilt = Quaternion.Euler(-12f, 0, 0);
            RoundBox(b, new Vector3(0, -0.758f, 0.162f), new Vector3(0.078f, 0.048f, 0.028f), 4f, new Color(0.62f, 0.64f, 0.67f), tilt, 0f, 18, 10);
            if (!lite)
            {
            b.Mat = MeshBuilder.MatLamp;
            b.AddBox(new Vector3(-0.04f, -0.748f, 0.19f), new Vector3(0.02f, 0.02f, 0.012f), new Color(0.35f, 1f, 0.5f), tilt);
            b.AddBox(new Vector3(-0.012f, -0.748f, 0.19f), new Vector3(0.02f, 0.02f, 0.012f), new Color(1f, 0.6f, 0.2f), tilt);
            b.Mat = MeshBuilder.MatPaint;
            b.AddSphere(new Vector3(0.022f, -0.752f, 0.19f), 0.011f, new Color(0.85f, 0.2f, 0.18f), 10, 6);
            b.AddSphere(new Vector3(0.048f, -0.752f, 0.19f), 0.011f, new Color(0.2f, 0.45f, 0.85f), 10, 6);
            b.AddBox(new Vector3(0, -0.774f, 0.187f), new Vector3(0.11f, 0.006f, 0.008f), dark, tilt);
            b.Mat = MeshBuilder.MatRubber;
            foreach (int side in new[] { -1, 1 })
                Hose(b, new Vector3(0.07f * side, -0.752f, 0.17f), new Vector3(0.16f * side, -0.73f, 0.19f), new Vector3(0.135f * side, -0.63f, 0.1f), 0.016f, dark);
            }

            // Backpack: a rounded case with a coloured band, vents and two tanks
            b.Mat = MeshBuilder.MatPaint;
            RoundBox(b, new Vector3(0, -0.78f, -0.212f), new Vector3(0.145f, 0.165f, 0.068f), 4f, white, Quaternion.identity, 0f, 22, 14);
            RoundBox(b, new Vector3(0, -0.665f, -0.212f), new Vector3(0.149f, 0.013f, 0.072f), 6f, look.trim, Quaternion.identity, 0f, 22, 6);
            if (!lite) for (int k = 0; k < 3; k++) b.AddBox(new Vector3(0, -0.75f - 0.028f * k, -0.282f), new Vector3(0.09f, 0.01f, 0.006f), dark, Quaternion.identity);
            b.Mat = MeshBuilder.MatMetal;
            if (!lite) foreach (int side in new[] { -1, 1 })
            {
                int tanks = b.v.Count;
                b.AddFrustum(-0.67f, -0.91f, 0.038f, 0.038f, metal, 16, capTop: true, capBottom: true);
                ScaleMove(b, tanks, Vector3.one, new Vector3(0.1f * side, 0, -0.29f));
                b.AddSphere(new Vector3(0.1f * side, -0.67f, -0.29f), 0.038f, metal, 16, 8, Mathf.PI * 0.5f);
            }

            // Arms, gloves, legs, boots
            if (limbs)
                foreach (int side in new[] { -1, 1 }) { AddArm(b, look, side, Vector3.zero); AddLeg(b, look, side, Vector3.zero); }

            AddHat(b, look);
            var m = b.ToMesh("Ardling");
            bodies[key] = m;
            return m;
        }

        // ---------------------------------------------------------------- work clothes (milestone 25)

        public const int RoleNone = 0, RoleWork = 1, RoleOffice = 2, RoleVisitor = 3;
        static readonly Dictionary<string, Mesh> gear = new Dictionary<string, Mesh>();

        /// <summary>
        /// What a crowd Ardling wears over the suit for their job (body frame): workers a hard hat over the helmet, a hi-vis
        /// vest with reflective bands and a tool belt; office staff a lanyard with a badge; visitors a camera on a strap.
        /// </summary>
        public static Mesh Gear(int role, int variant)
        {
            string key = $"gear{role}|{variant % 4}";
            if (gear.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            Color silver = new Color(0.82f, 0.84f, 0.86f), dark = new Color(0.2f, 0.21f, 0.23f), brown = new Color(0.42f, 0.29f, 0.17f);
            switch (role)
            {
                case RoleWork:
                {
                    Color hat = new[] { new Color(0.96f, 0.78f, 0.1f), new Color(0.92f, 0.93f, 0.94f), new Color(0.95f, 0.45f, 0.1f), new Color(0.15f, 0.35f, 0.7f) }[variant % 4];
                    Color vest = variant % 2 == 0 ? new Color(0.82f, 0.95f, 0.12f) : new Color(1f, 0.48f, 0.08f);
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddSphere(new Vector3(0, -0.07f, 0), 0.215f, hat, 28, 10, Mathf.PI * 0.5f);
                    int s = b.v.Count;
                    b.AddFrustum(-0.062f, -0.082f, 0.232f, 0.268f, hat, 28, capTop: true, capBottom: true);
                    b.AddBox(new Vector3(0, 0.125f, 0), new Vector3(0.045f, 0.05f, 0.36f), hat, Quaternion.Euler(-4f, 0, 0));
                    b.Mat = MeshBuilder.MatMatte;
                    RoundBox(b, new Vector3(0, -0.775f, 0.005f), new Vector3(0.219f, 0.156f, 0.184f), 2.6f, vest, Quaternion.identity, 0.08f, 26, 14);
                    b.Mat = MeshBuilder.MatGloss;
                    foreach (float y in new[] { -0.735f, -0.83f }) RoundBox(b, new Vector3(0, y, 0.005f), new Vector3(0.224f, 0.012f, 0.189f), 4f, silver, Quaternion.identity, 0f, 26, 6);
                    b.Mat = MeshBuilder.MatMatte;
                    RoundBox(b, new Vector3(0, -0.905f, 0), new Vector3(0.192f, 0.026f, 0.17f), 4f, brown, Quaternion.identity, 0f, 24, 6);
                    foreach (int side in new[] { -1, 1 })
                        RoundBox(b, new Vector3(0.15f * side, -0.93f, 0.09f), new Vector3(0.045f, 0.05f, 0.03f), 3f, Shade(brown, 0.85f), Quaternion.Euler(0, 35f * side, 0), 0f, 12, 8);
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddBox(new Vector3(0.17f, -0.97f, 0.1f), new Vector3(0.012f, 0.09f, 0.012f), silver, Quaternion.Euler(0, 0, 8f));   // a screwdriver
                    break;
                }
                case RoleOffice:
                {
                    Color strap = new[] { new Color(0.12f, 0.3f, 0.6f), new Color(0.6f, 0.15f, 0.15f), new Color(0.15f, 0.45f, 0.25f), new Color(0.2f, 0.2f, 0.22f) }[variant % 4];
                    b.Mat = MeshBuilder.MatMatte;
                    foreach (int side in new[] { -1, 1 })
                    {
                        Vector3 a = new Vector3(0.1f * side, -0.635f, 0.11f), c = new Vector3(0.012f * side, -0.8f, 0.185f);
                        b.AddBox((a + c) * 0.5f, new Vector3(0.012f, (c - a).magnitude, 0.004f), strap, Quaternion.FromToRotation(Vector3.up, (c - a).normalized));
                    }
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(new Vector3(0, -0.83f, 0.19f), new Vector3(0.05f, 0.068f, 0.004f), new Color(0.95f, 0.95f, 0.93f), Quaternion.Euler(-6f, 0, 0));
                    b.AddBox(new Vector3(0, -0.812f, 0.193f), new Vector3(0.05f, 0.016f, 0.002f), strap, Quaternion.Euler(-6f, 0, 0));
                    break;
                }
                case RoleVisitor:
                {
                    b.Mat = MeshBuilder.MatMatte;
                    Vector3 a = new Vector3(0.13f, -0.64f, 0.12f), c = new Vector3(-0.17f, -0.9f, 0.12f);
                    b.AddBox((a + c) * 0.5f, new Vector3(0.014f, (c - a).magnitude, 0.005f), dark, Quaternion.FromToRotation(Vector3.up, (c - a).normalized));
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(new Vector3(-0.17f, -0.92f, 0.15f), new Vector3(0.07f, 0.05f, 0.045f), dark, Quaternion.identity);
                    b.Mat = MeshBuilder.MatGlass;
                    int s = b.v.Count;
                    b.AddFrustum(0.03f, 0f, 0.016f, 0.019f, new Color(0.05f, 0.06f, 0.08f), 12, capTop: true);
                    b.Transform(s, Quaternion.Euler(90f, 0, 0), new Vector3(-0.17f, -0.92f, 0.172f));
                    break;
                }
            }
            m = b.ToMesh("Ardling gear");
            gear[key] = m;
            return m;
        }

        /// <summary>What a crowd Ardling carries in the right hand (in the right arm's frame, from the shoulder): a toolbox, a
        /// clipboard or a souvenir bag. Null for none.</summary>
        public static Mesh HandItem(int role, int variant)
        {
            if (role == RoleNone) return null;
            string key = $"hand{role}|{variant % 4}";
            if (gear.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            Vector3 hand = new Vector3(0.085f, -0.27f, 0.055f);
            switch (role)
            {
                case RoleWork:
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(hand + new Vector3(0.01f, -0.1f, 0), new Vector3(0.07f, 0.075f, 0.16f), variant % 2 == 0 ? new Color(0.75f, 0.12f, 0.1f) : new Color(0.12f, 0.3f, 0.65f), Quaternion.identity);
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddBox(hand + new Vector3(0.01f, -0.055f, 0), new Vector3(0.012f, 0.03f, 0.09f), new Color(0.3f, 0.31f, 0.33f), Quaternion.identity);
                    break;
                case RoleOffice:
                    b.Mat = MeshBuilder.MatMatte;
                    b.AddBox(hand + new Vector3(0.02f, -0.06f, 0.03f), new Vector3(0.012f, 0.17f, 0.125f), new Color(0.48f, 0.34f, 0.2f), Quaternion.Euler(0, 0, 6f));
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(hand + new Vector3(0.027f, -0.065f, 0.03f), new Vector3(0.004f, 0.14f, 0.105f), new Color(0.96f, 0.96f, 0.94f), Quaternion.Euler(0, 0, 6f));
                    break;
                case RoleVisitor:
                    b.Mat = MeshBuilder.MatPaint;
                    Color bag = new[] { new Color(0.85f, 0.3f, 0.2f), new Color(0.2f, 0.5f, 0.75f), new Color(0.95f, 0.75f, 0.2f), new Color(0.35f, 0.65f, 0.35f) }[variant % 4];
                    b.AddBox(hand + new Vector3(0.012f, -0.1f, 0), new Vector3(0.04f, 0.12f, 0.1f), bag, Quaternion.identity);
                    b.AddBox(hand + new Vector3(0.012f, -0.1f, 0.051f), new Vector3(0.03f, 0.05f, 0.002f), Color.white, Quaternion.identity);
                    break;
            }
            m = b.ToMesh("Ardling hand item");
            gear[key] = m;
            return m;
        }

        /// <summary>A superellipsoid: n = 2 is an ellipsoid, a higher n a box with rounded edges.
        /// 'taper' widens the top and narrows the bottom.</summary>
        static void RoundBox(MeshBuilder b, Vector3 center, Vector3 half, float n, Color col, Quaternion rot, float taper = 0f, int seg = 20, int rings = 12)
        {
            int s = b.v.Count;
            b.AddSphere(Vector3.zero, 1f, col, S(seg), S(rings));
            for (int i = s; i < b.v.Count; i++)
            {
                Vector3 d = b.v[i];
                float sum = Mathf.Pow(Mathf.Abs(d.x / half.x), n) + Mathf.Pow(Mathf.Abs(d.y / half.y), n) + Mathf.Pow(Mathf.Abs(d.z / half.z), n);
                Vector3 p = d * Mathf.Pow(Mathf.Max(sum, 1e-9f), -1f / n);
                Vector3 g = new Vector3(Mathf.Sign(p.x) * Mathf.Pow(Mathf.Abs(p.x / half.x), n - 1) / half.x,
                    Mathf.Sign(p.y) * Mathf.Pow(Mathf.Abs(p.y / half.y), n - 1) / half.y,
                    Mathf.Sign(p.z) * Mathf.Pow(Mathf.Abs(p.z / half.z), n - 1) / half.z);
                if (g.sqrMagnitude < 1e-12f) g = d;
                if (taper != 0f) { float k = 1f + taper * p.y / half.y; p.x *= k; p.z *= k; }
                b.v[i] = center + rot * p;
                b.n[i] = rot * g.normalized;
            }
        }

        /// <summary>A tapered tube with rounded ends, from a (radius ra) to c (radius rc).</summary>
        static void Limb(MeshBuilder b, Vector3 a, Vector3 c, float ra, float rc, Color col)
        {
            int s = b.v.Count;
            b.AddFrustum((c - a).magnitude, 0, rc, ra, col, S(18));
            Align(b, s, a, c - a);
            b.AddSphere(a, ra, col, S(18), lite ? 3 : 9);
            b.AddSphere(c, rc, col, S(18), lite ? 3 : 9);
        }

        /// <summary>A raised ring round a tube (cuffs, folds).</summary>
        static void Ring(MeshBuilder b, Vector3 center, Vector3 axis, float r, float h, Color col)
        {
            int s = b.v.Count;
            b.AddFrustum(h * 0.5f, 0, r * 0.97f, r * 1.07f, col, S(18));
            b.AddFrustum(0, -h * 0.5f, r * 1.07f, r * 0.97f, col, S(18));
            Align(b, s, center, axis);
        }

        static void Bellows(MeshBuilder b, Vector3 center, Vector3 axis, float r, int count, float spacing, Color col)
        {
            for (int k = 0; k < count; k++) Ring(b, center + axis * ((k - (count - 1) * 0.5f) * spacing), axis, r, spacing * 0.95f, col);
        }

        /// <summary>A ribbed hose along a curve from a, bending toward m, to c.</summary>
        static void Hose(MeshBuilder b, Vector3 a, Vector3 m, Vector3 c, float r, Color col)
        {
            const int N = 16;
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N, u = 1 - t;
                b.AddSphere(u * u * a + 2 * u * t * m + t * t * c, r, col, 10, 6);
            }
        }

        static void AddHat(MeshBuilder b, Look look)
        {
            Color c = Color.Lerp(look.suit, look.paper, 0.35f), dark = Color.Lerp(look.suit, Color.black, 0.5f);
            Vector3 top = Vector3.zero;
            b.Mat = MeshBuilder.MatMatte;
            switch (look.hat)
            {
                case 1: // beanie with a pompom
                {
                    int s = b.v.Count;
                    b.AddSphere(Vector3.zero, HelmetR * 1.05f, c, 32, 10, Mathf.PI * 0.36f);
                    ScaleMove(b, s, Vector3.one, HelmetC);
                    b.AddFrustum(HelmetC.y + HelmetR * 1.05f * Mathf.Cos(Mathf.PI * 0.36f) + 0.02f, HelmetC.y + HelmetR * 1.05f * Mathf.Cos(Mathf.PI * 0.36f) - 0.035f,
                        HelmetR * 1.05f * Mathf.Sin(Mathf.PI * 0.36f) + 0.012f, HelmetR * 1.05f * Mathf.Sin(Mathf.PI * 0.36f) + 0.02f, dark, 32);
                    b.AddSphere(top + new Vector3(0, 0.04f, 0), 0.055f, Color.white, 14, 8);
                    break;
                }
                case 2: // party hat, tilted
                {
                    int s = b.v.Count;
                    b.AddFrustum(0.3f, 0f, 0.01f, 0.11f, c, 24, capBottom: true);
                    b.AddSphere(new Vector3(0, 0.31f, 0), 0.03f, look.paper, 10, 6);
                    b.Transform(s, Quaternion.Euler(0, 0, -16f), new Vector3(0.05f, -0.03f, 0));
                    break;
                }
                case 3: // propeller cap
                {
                    int s = b.v.Count;
                    b.AddSphere(Vector3.zero, HelmetR * 1.04f, c, 32, 8, Mathf.PI * 0.3f);
                    ScaleMove(b, s, Vector3.one, HelmetC);
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddFrustum(0.08f, -0.01f, 0.008f, 0.008f, new Color(0.8f, 0.8f, 0.82f), 8);
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(new Vector3(0, 0.08f, 0), new Vector3(0.28f, 0.008f, 0.045f), look.paper, Quaternion.Euler(0, 25f, 0));
                    b.AddSphere(new Vector3(0, 0.085f, 0), 0.016f, dark, 8, 5);
                    break;
                }
                case 4: // antenna with a glowing tip
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddFrustum(0.2f, -0.01f, 0.006f, 0.01f, new Color(0.8f, 0.8f, 0.82f), 8);
                    b.Mat = MeshBuilder.MatLamp;
                    b.AddSphere(new Vector3(0, 0.215f, 0), 0.028f, look.paper, 12, 8);
                    break;
                case 5: // wide-brim hat
                    b.AddFrustum(-0.06f, -0.075f, 0.36f, 0.37f, c, 36, capTop: true, capBottom: true);
                    b.AddFrustum(0.06f, -0.06f, 0.15f, 0.2f, c, 28, capTop: true);
                    b.AddFrustum(-0.02f, -0.05f, 0.19f, 0.205f, dark, 28);
                    break;
                case 6: // crown
                {
                    Color gold = new Color(1f, 0.78f, 0.3f);
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddFrustum(0.02f, -0.06f, 0.15f, 0.14f, gold, 28);
                    for (int k = 0; k < 6; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, 60f * k, 0);
                        b.AddBox(q * new Vector3(0, 0.05f, 0.145f), new Vector3(0.05f, 0.07f, 0.012f), gold, q * Quaternion.Euler(0, 0, 45f));
                    }
                    b.Mat = MeshBuilder.MatGlass;
                    b.AddSphere(new Vector3(0, -0.02f, 0.15f), 0.018f, new Color(0.8f, 0.05f, 0.1f), 8, 6);
                    break;
                }
                case 7: // a flower on the side of the helmet
                {
                    Vector3 at = HelmetC + new Vector3(0.24f, 0.17f, 0.06f);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * Mathf.PI * 2f / 5f;
                        b.AddSphere(at + Quaternion.Euler(0, 0, -50f) * new Vector3(Mathf.Cos(a) * 0.04f, 0, Mathf.Sin(a) * 0.04f), 0.03f, look.paper, 10, 6);
                    }
                    b.AddSphere(at + new Vector3(0.01f, 0.01f, 0), 0.025f, new Color(1f, 0.85f, 0.2f), 10, 6);
                    break;
                }
                case 8: // top hat
                    b.AddFrustum(-0.045f, -0.06f, 0.2f, 0.2f, new Color(0.1f, 0.1f, 0.11f), 28, capTop: true, capBottom: true);
                    b.AddFrustum(0.2f, -0.05f, 0.13f, 0.12f, new Color(0.1f, 0.1f, 0.11f), 28, capTop: true);
                    b.AddFrustum(0.0f, -0.04f, 0.124f, 0.122f, c, 28);
                    break;
            }
            b.Mat = MeshBuilder.MatPaint;
        }

        static void Align(MeshBuilder b, int start, Vector3 at, Vector3 dir)
        {
            b.Transform(start, Quaternion.FromToRotation(Vector3.up, dir.normalized), at);
        }

        static void ScaleMove(MeshBuilder b, int start, Vector3 scale, Vector3 at)
        {
            for (int i = start; i < b.v.Count; i++)
            {
                b.v[i] = Vector3.Scale(b.v[i], scale) + at;
                b.n[i] = new Vector3(b.n[i].x / scale.x, b.n[i].y / scale.y, b.n[i].z / scale.z).normalized;
            }
        }

        static void Shift(MeshBuilder b, int start, Vector3 by) { for (int i = start; i < b.v.Count; i++) b.v[i] += by; }

        /// <summary>Turn the visor cap (made around +Y) to face forward, squash it wider than tall and put it on the helmet.</summary>
        static void Squash(MeshBuilder b, int start, Quaternion turn, Vector3 scale)
        {
            for (int i = start; i < b.v.Count; i++)
            {
                Vector3 p = turn * b.v[i];
                Vector3 n = turn * b.n[i];
                // keep the visor on the sphere's surface after squashing: scale x/y, then push z back out
                Vector3 q = new Vector3(p.x * scale.x, p.y * scale.y, 0);
                float rr = HelmetR * 1.025f;
                q.z = Mathf.Sqrt(Mathf.Max(rr * rr - q.x * q.x - q.y * q.y, 0.0001f)) * Mathf.Sign(p.z);
                b.v[i] = q + HelmetC;
                b.n[i] = q.normalized;
            }
        }

        // ---------------------------------------------------------------- the sticky note

        public const float NoteSize = 0.2f;
        static Mesh noteMesh;
        static readonly Dictionary<int, Material> noteMats = new Dictionary<int, Material>();
        static readonly Dictionary<int, Texture2D> noteTex = new Dictionary<int, Texture2D>();
        static Shader noteShader;

        /// <summary>The note stuck on the visor: a small square card facing +Z (its own textured material).</summary>
        public static GameObject AddNote(Transform parent, Look look)
        {
            if (noteMesh == null)
            {
                noteMesh = new Mesh { name = "Note" };
                float h = NoteSize * 0.5f;
                noteMesh.vertices = new[] { new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0) };
                noteMesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
                noteMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                noteMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                noteMesh.RecalculateBounds();
            }
            var go = new GameObject("Note");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = HelmetC + new Vector3(0, -0.005f, HelmetR * 1.025f + 0.004f);
            go.transform.localRotation = Quaternion.Euler(0, 0, look.noteTilt);
            go.AddComponent<MeshFilter>().sharedMesh = noteMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = NoteMaterial(look);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static Material NoteMaterial(Look look)
        {
            int key = look.face * 100 + System.Array.IndexOf(Papers, look.paper);
            if (noteMats.TryGetValue(key, out var m)) return m;
            if (noteShader == null) noteShader = Resources.Load<Shader>("Shaders/RocketNote");
            m = new Material(noteShader) { name = "Note " + key };
            m.SetTexture("_MainTex", FaceTexture(look.face, look.paper));
            noteMats[key] = m;
            return m;
        }

        /// <summary>A sticky note with a face drawn in marker (also used as the crew's picture in the HUD).</summary>
        public static Texture2D FaceTexture(int face, Color paper)
        {
            int key = face * 100 + System.Array.IndexOf(Papers, paper);
            if (noteTex.TryGetValue(key, out var t)) return t;
            const int N = 128;
            t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Face " + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[N * N];
            Color ink = new Color(0.07f, 0.07f, 0.09f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);   // -1..1, y up
                    // Paper: the sticky strip at the top is a touch darker, faint fibres, a soft edge shadow
                    Color c = paper * (p.y > 0.72f ? 0.93f : 1f) * (0.97f + 0.03f * Mathf.PerlinNoise(x * 0.31f, y * 0.07f));
                    float edge = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
                    c *= Mathf.Lerp(1f, 0.86f, Mathf.InverseLerp(0.9f, 1f, edge));
                    float d = FaceDistance(face, p, out bool fill, out Color fillCol);
                    float aa = 2.2f / N;
                    float a = Mathf.Clamp01((0.055f + aa - d) / (2f * aa));
                    if (fill) c = Color.Lerp(c, fillCol, Mathf.Clamp01((aa - d) / (2f * aa) + 0.5f));
                    c = Color.Lerp(c, ink, a);
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true, false);
            noteTex[key] = t;
            return t;
        }

        // Signed-ish distance to the marker strokes of a face (strokes ~0.055 wide); 'fill' areas use fillCol.
        static float FaceDistance(int face, Vector2 p, out bool fill, out Color fillCol)
        {
            fill = false; fillCol = Color.black;
            Vector2 le = new Vector2(-0.36f, 0.18f), re = new Vector2(0.36f, 0.18f);
            float d = 9f;
            switch (face)
            {
                case 0: // smile
                    d = Mathf.Min(Dot(p, le, 0.09f), Dot(p, re, 0.09f), Arc(p, new Vector2(0, 0.05f), 0.48f, 200f, 340f));
                    break;
                case 1: // big grin (open mouth)
                    d = Mathf.Min(Dot(p, le, 0.09f), Dot(p, re, 0.09f));
                    { float m = HalfDisc(p, new Vector2(0, -0.2f), 0.4f); if (m < 0) { fill = true; fillCol = new Color(0.55f, 0.12f, 0.15f); } d = Mathf.Min(d, Mathf.Abs(m)); }
                    break;
                case 2: // happy, eyes closed
                    d = Mathf.Min(Arc(p, le + new Vector2(0, -0.06f), 0.13f, 20f, 160f), Arc(p, re + new Vector2(0, -0.06f), 0.13f, 20f, 160f), Arc(p, new Vector2(0, 0.0f), 0.45f, 205f, 335f));
                    break;
                case 3: // wink
                    d = Mathf.Min(Dot(p, le, 0.09f), Seg(p, re + new Vector2(-0.13f, 0), re + new Vector2(0.13f, 0.02f)), Arc(p, new Vector2(-0.05f, 0.05f), 0.45f, 210f, 330f));
                    break;
                case 4: // surprised
                    d = Mathf.Min(Ring(p, le, 0.12f), Ring(p, re, 0.12f), Ring(p, new Vector2(0, -0.38f), 0.12f));
                    break;
                case 5: // determined
                    d = Mathf.Min(Mathf.Min(Dot(p, le + new Vector2(0, -0.05f), 0.08f), Dot(p, re + new Vector2(0, -0.05f), 0.08f)),
                        Mathf.Min(Seg(p, new Vector2(-0.55f, 0.42f), new Vector2(-0.2f, 0.3f)), Seg(p, new Vector2(0.55f, 0.42f), new Vector2(0.2f, 0.3f))),
                        Seg(p, new Vector2(-0.28f, -0.38f), new Vector2(0.28f, -0.38f)));
                    break;
                case 6: // sleepy, with a little z
                    d = Mathf.Min(Mathf.Min(Seg(p, le + new Vector2(-0.13f, 0), le + new Vector2(0.13f, 0)), Seg(p, re + new Vector2(-0.13f, 0), re + new Vector2(0.13f, 0))),
                        Arc(p, new Vector2(0, -0.12f), 0.22f, 215f, 325f),
                        Mathf.Min(Seg(p, new Vector2(0.5f, 0.75f), new Vector2(0.68f, 0.75f)), Seg(p, new Vector2(0.68f, 0.75f), new Vector2(0.5f, 0.55f)), Seg(p, new Vector2(0.5f, 0.55f), new Vector2(0.68f, 0.55f))));
                    break;
                case 7: // cheeky, tongue out
                    d = Mathf.Min(Dot(p, le, 0.09f), Dot(p, re, 0.09f), Seg(p, new Vector2(-0.35f, -0.25f), new Vector2(0.35f, -0.25f)));
                    { float tg = Vector2.Distance(p, new Vector2(0.1f, -0.36f)) - 0.13f; if (tg < 0 && p.y < -0.25f) { fill = true; fillCol = new Color(0.95f, 0.45f, 0.55f); d = Mathf.Min(d, 0.2f); } else if (p.y < -0.25f) d = Mathf.Min(d, Mathf.Abs(tg)); }
                    break;
                case 8: // cool shades and a smirk
                {
                    float gl = Mathf.Min(Box(p, le, new Vector2(0.2f, 0.12f)), Box(p, re, new Vector2(0.2f, 0.12f)));
                    if (gl < 0) { fill = true; fillCol = new Color(0.07f, 0.07f, 0.09f); }
                    d = Mathf.Min(Mathf.Abs(gl), Seg(p, le + new Vector2(0.2f, 0.05f), re + new Vector2(-0.2f, 0.05f)), Arc(p, new Vector2(0.12f, -0.05f), 0.32f, 225f, 320f));
                    break;
                }
                case 9: // worried
                    d = Mathf.Min(Mathf.Min(Dot(p, le, 0.08f), Dot(p, re, 0.08f)),
                        Mathf.Min(Seg(p, new Vector2(-0.52f, 0.36f), new Vector2(-0.22f, 0.44f)), Seg(p, new Vector2(0.52f, 0.36f), new Vector2(0.22f, 0.44f))),
                        Wave(p));
                    break;
            }
            return d;
        }

        static float Dot(Vector2 p, Vector2 c, float r) => Mathf.Max(Vector2.Distance(p, c) - r, 0f) - 0.055f + 0.0001f;
        static float Ring(Vector2 p, Vector2 c, float r) => Mathf.Abs(Vector2.Distance(p, c) - r);
        static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
        static float Arc(Vector2 p, Vector2 c, float r, float a0, float a1)
        {
            Vector2 q = p - c;
            float ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
            if (ang < 0) ang += 360f;
            if (ang >= a0 && ang <= a1) return Mathf.Abs(q.magnitude - r);
            Vector2 e0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
            Vector2 e1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
            return Mathf.Min(Vector2.Distance(p, e0), Vector2.Distance(p, e1));
        }
        static float HalfDisc(Vector2 p, Vector2 c, float r)
        {
            // flat top at c.y, round bottom: negative inside
            Vector2 q = p - c;
            float round = q.magnitude - r;
            float flat = q.y;
            return Mathf.Max(round, flat);
        }
        static float Box(Vector2 p, Vector2 c, Vector2 h)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x) - h.x, Mathf.Abs(p.y - c.y) - h.y);
            return Mathf.Max(q.x, q.y);
        }
        static float Wave(Vector2 p)
        {
            float best = 9f;
            for (int i = 0; i < 24; i++)
            {
                float x0 = -0.3f + 0.6f * i / 24f, x1 = -0.3f + 0.6f * (i + 1) / 24f;
                Vector2 a = new Vector2(x0, -0.35f + 0.05f * Mathf.Sin(x0 * 22f)), b = new Vector2(x1, -0.35f + 0.05f * Mathf.Sin(x1 * 22f));
                best = Mathf.Min(best, Seg(p, a, b));
            }
            return best;
        }
    }
}
