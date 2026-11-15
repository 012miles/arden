using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 42 (captaincarl24: "add more props inside the spaceship"): more of the cabin to use. A snack box (food), a
    /// hot-drink maker (stamina), a swinging-ball desk toy, a guitar, a crew photo, a poster of the worlds (a fact each look),
    /// a mirror, a toolbox (a repair kit, once a flight) and a handheld game. Each fits the capsule's round wall or the
    /// habitat's long walls, clear of the windows, seats and hatch.
    /// </summary>
    public partial class FlightMode
    {
        float snackReadyAt, drinkReadyAt;
        bool toolboxTaken;
        int gameBest, factIndex;

        static readonly string[] WorldFacts =
        {
            "Arden: home. Its seas are real water now: capsules float upright.",
            "Tal: the big moon. Low gravity, long jumps, and something in the Lost Lander's wreck.",
            "Vesk: the small icy moon. Land gently: there's no air to slow you.",
            "Ochre: the red desert world. Thin air: parachutes help, engines finish the job.",
            "Brume: the gas giant. No ground at all: orbit it, or visit Rime and Pyre.",
            "Mote: Ochre's little moon. You can almost jump off it.",
        };

        void BuildMoreProps(bool hab, float h)
        {
            snackReadyAt = drinkReadyAt = 0; toolboxTaken = false;
            float hl = hab ? roomL / 2 : 0, hw = hab ? roomW / 2 : 0;
            Vector3 OnWall(float angleDeg, float y, float inset)
            {
                float a = angleDeg * Mathf.Deg2Rad;
                float r = roomR * Mathf.Cos(Mathf.PI / 12f) - inset;
                return new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
            }
            Quaternion FaceIn(Vector3 at) => Quaternion.LookRotation(new Vector3(at.x, 0, at.z).normalized, Vector3.up);
            // where each goes: (round capsule angle, height) or (habitat wall side, along, height)
            Vector3 Spot(float angle, float y, float inset, int side, float along) =>
                roomRound ? OnWall(angle, y, inset) : new Vector3(side * (hw - inset), y, Mathf.Clamp(along, -hl + 0.6f, hl - 0.9f));
            Quaternion Face(Vector3 at, int side) => roomRound ? FaceIn(at) : Quaternion.LookRotation(new Vector3(side, 0, 0), Vector3.up);
            string me = MyArdlingName;

            // the snack box: a little drawer of food pouches
            Vector3 sb = Spot(32, 0.62f, 0.12f, 1, -0.6f);
            var sbq = Face(sb, 1);
            Piece("Snack box", sb, sbq, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(Vector3.zero, new Vector3(0.32f, 0.2f, 0.16f), C(0.93f, 0.62f, 0.2f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0, -0.082f), new Vector3(0.26f, 0.13f, 0.006f), C(0.98f, 0.9f, 0.7f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.03f, -0.088f), new Vector3(0.1f, 0.02f, 0.012f), C(0.3f, 0.3f, 0.32f), Quaternion.identity);
                for (int i = 0; i < 3; i++) b.AddBox(new Vector3(-0.08f + i * 0.08f, 0.13f, 0), new Vector3(0.06f, 0.07f, 0.03f), new[] { C(0.9f, 0.3f, 0.3f), C(0.4f, 0.75f, 0.35f), C(0.35f, 0.55f, 0.9f) }[i], Quaternion.identity);
            });
            Label(sb + Vector3.up * 0.16f + sbq * new Vector3(0, 0, -0.09f), sbq, "SNACKS", 0.018f, C(0.25f, 0.2f, 0.1f));
            AddProp("snacks", "Snack box", sb, () => Time.time < snackReadyAt ? "Empty for now (it refills in a minute)" : "E: grab a snack (food)", () =>
            {
                if (Time.time < snackReadyAt) { KitSound("ui_error"); return; }
                snackReadyAt = Time.time + 60f;
                var v = VitalsFor(me);
                if (v != null) v.hunger = Mathf.Min(Survival.Max, v.hunger + 20f);
                Show(new[] { "Crunchy space crackers. Food +20.", "A squeeze of berry paste. Food +20.", "Freeze-dried noodle cube. Food +20." }[UnityEngine.Random.Range(0, 3)], 2f);
            }, 0.15f);

            // the hot-drink maker: a little machine with a steaming cup
            Vector3 dm = Spot(122, 0.62f, 0.14f, -1, -0.6f);
            var dmq = Face(dm, -1);
            Piece("Drink maker", dm, dmq, b =>
            {
                b.Mat = MeshBuilder.MatGloss;
                b.AddBox(new Vector3(0, 0.06f, 0), new Vector3(0.2f, 0.32f, 0.18f), C(0.22f, 0.24f, 0.27f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.02f, -0.06f), new Vector3(0.14f, 0.12f, 0.08f), C(0.08f, 0.08f, 0.09f), Quaternion.identity);
                b.Mat = MeshBuilder.MatPaint;
                b.AddFrustum(0.06f, -0.04f, 0.028f, 0.024f, C(0.95f, 0.95f, 0.93f), 12, false, true);
                b.AddBox(new Vector3(0.06f, 0.18f, -0.092f), new Vector3(0.02f, 0.02f, 0.006f), C(0.3f, 1f, 0.45f), Quaternion.identity);
            });
            AddProp("drinkmaker", "Drink maker", dm + Vector3.up * 0.06f, () => Time.time < drinkReadyAt ? "Brewing..." : "E: make a hot drink (stamina)", () =>
            {
                if (Time.time < drinkReadyAt) { KitSound("ui_error"); return; }
                drinkReadyAt = Time.time + 45f;
                var v = VitalsFor(me);
                if (v != null) { v.stamina = Survival.Max; v.tired = false; }
                PuffAt(dm + Vector3.up * 0.15f);
                Show("A hot cup of cocoa. Stamina full.", 2f);
            }, 0.13f);

            // the desk toy: five balls on strings; set the end one going and it knocks on down the row
            Vector3 dt0 = roomRound ? new Vector3(-0.45f, 0.465f, roomR - 0.62f) : new Vector3(-0.55f, 0.465f, hl - 0.62f);
            Piece("Desk toy frame", dt0, Quaternion.identity, b =>
            {
                b.Mat = MeshBuilder.MatGloss;
                b.AddBox(new Vector3(0, 0.005f, 0), new Vector3(0.16f, 0.01f, 0.07f), C(0.15f, 0.15f, 0.17f), Quaternion.identity);
                foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) b.AddBox(new Vector3(sx * 0.075f, 0.06f, sz * 0.03f), new Vector3(0.006f, 0.11f, 0.006f), C(0.7f, 0.7f, 0.72f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.115f, 0), new Vector3(0.16f, 0.005f, 0.065f), C(0.7f, 0.7f, 0.72f), Quaternion.identity);
            });
            var balls = new List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                var piv = new GameObject("Ball " + i) { layer = InteriorLayer };
                piv.transform.SetParent(room.transform, false);
                piv.transform.localPosition = dt0 + new Vector3(-0.04f + i * 0.02f, 0.115f, 0);
                Piece("Ball", Vector3.zero, Quaternion.identity, b => { b.Mat = MeshBuilder.MatGloss; b.AddBox(new Vector3(0, -0.04f, 0), new Vector3(0.002f, 0.08f, 0.002f), C(0.9f, 0.9f, 0.9f), Quaternion.identity); b.AddSphere(new Vector3(0, -0.085f, 0), 0.0098f, C(0.8f, 0.82f, 0.86f), 10, 7); }, false, piv.transform);
                balls.Add(piv.transform);
            }
            var toyProp = AddProp("cradle", "Desk toy", dt0 + Vector3.up * 0.06f, () => "E: set the balls swinging", null, 0.1f);
            toyProp.use = () => { toyProp.vel = 1f; toyProp.state = 0; KitSound("clink", 0.3f); };
            toyProp.animate = (pp, dtt) =>
            {
                if (pp.vel <= 0.01f) { foreach (var bt in balls) bt.localRotation = Quaternion.identity; return; }
                pp.state += dtt * 7f;
                pp.vel *= 1f - 0.06f * dtt;
                float s = Mathf.Sin(pp.state), a = 40f * pp.vel;
                balls[0].localRotation = Quaternion.Euler(0, 0, s > 0 ? -s * a : 0);
                balls[4].localRotation = Quaternion.Euler(0, 0, s < 0 ? -s * a : 0);
                for (int i = 1; i < 4; i++) balls[i].localRotation = Quaternion.identity;
                if (pp.vel < 0.05f) pp.vel = 0;
            };
            toyProp.moving = balls[0];

            // the guitar on the wall
            Vector3 gt = Spot(296, 0.72f, 0.08f, -1, 0.9f);
            var gtq = Face(gt, -1);
            Piece("Guitar", gt, gtq, b =>
            {
                b.Mat = MeshBuilder.MatGloss;
                b.AddSphere(new Vector3(0, -0.12f, 0), 0.11f, C(0.75f, 0.42f, 0.18f), 14, 8);
                b.AddSphere(new Vector3(0, 0.0f, 0), 0.085f, C(0.75f, 0.42f, 0.18f), 14, 8);
                b.AddBox(new Vector3(0, 0.22f, 0), new Vector3(0.035f, 0.36f, 0.02f), C(0.32f, 0.2f, 0.12f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.42f, 0), new Vector3(0.05f, 0.07f, 0.02f), C(0.2f, 0.12f, 0.08f), Quaternion.identity);
                b.Mat = MeshBuilder.MatMatte;
                b.AddSphere(new Vector3(0, -0.07f, -0.05f), 0.028f, C(0.08f, 0.06f, 0.05f), 10, 6);
                for (int i = 0; i < 4; i++) b.AddBox(new Vector3(-0.012f + i * 0.008f, 0.12f, -0.012f), new Vector3(0.0015f, 0.5f, 0.0015f), C(0.85f, 0.85f, 0.8f), Quaternion.identity);
            });
            AddProp("guitar", "Guitar", gt, () => "E: strum a few chords", () =>
            {
                KitSound("ui_confirm", 0.4f);
                Show(new[] { "You strum a few chords. The crew hum along.", "A little tune about the stars.", "Strum, strum. Not bad for a pilot." }[UnityEngine.Random.Range(0, 3)], 2.5f);
            }, 0.2f);

            // the crew photo, and a poster of the worlds
            Vector3 ph = Spot(205, 1.15f, 0.04f, 1, 1.2f);
            var phq = Face(ph, 1);
            Piece("Crew photo", ph, phq, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(Vector3.zero, new Vector3(0.24f, 0.18f, 0.012f), C(0.45f, 0.3f, 0.18f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0, -0.007f), new Vector3(0.21f, 0.15f, 0.004f), C(0.55f, 0.75f, 0.95f), Quaternion.identity);
                b.AddBox(new Vector3(0, -0.05f, -0.009f), new Vector3(0.21f, 0.05f, 0.004f), C(0.45f, 0.65f, 0.35f), Quaternion.identity);
                for (int i = 0; i < 3; i++) b.AddSphere(new Vector3(-0.06f + i * 0.06f, -0.01f, -0.012f), 0.022f, C(0.95f, 0.95f, 0.95f), 10, 6);
            });
            AddProp("crewphoto", "Crew photo", ph, () => "E: look at the photo", () => Show("The crew on launch day, squinting in the sun by the pad.", 2.5f), 0.14f);

            Vector3 po = Spot(282, 1.1f, 0.03f, 1, -1.4f);
            var poq = Face(po, 1);
            Piece("Poster", po, poq, b =>
            {
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(Vector3.zero, new Vector3(0.36f, 0.5f, 0.004f), C(0.06f, 0.07f, 0.14f), Quaternion.identity);
                Color[] w = { C(0.3f, 0.55f, 0.9f), C(0.75f, 0.74f, 0.7f), C(0.85f, 0.92f, 1f), C(0.85f, 0.45f, 0.25f), C(0.85f, 0.75f, 0.55f) };
                for (int i = 0; i < w.Length; i++) b.AddSphere(new Vector3(-0.11f + (i % 2) * 0.2f, 0.18f - i * 0.085f, -0.004f), 0.025f + 0.008f * (i == 4 ? 2 : 0), w[i], 12, 8);
            });
            Label(po + Vector3.up * 0.28f + poq * new Vector3(0, 0, -0.005f), poq, "THE WORLDS OF ARDEN", 0.016f, C(0.85f, 0.88f, 1f));
            AddProp("poster", "Poster", po, () => "E: read a fact off the poster", () => { Show(WorldFacts[factIndex % WorldFacts.Length], 4f); factIndex++; }, 0.24f);

            // the mirror
            Vector3 mr = Spot(318, 1.0f, 0.04f, -1, -1.2f);
            var mrq = Face(mr, -1);
            Piece("Mirror", mr, mrq, b =>
            {
                b.Mat = MeshBuilder.MatGloss;
                b.AddBox(Vector3.zero, new Vector3(0.22f, 0.3f, 0.012f), C(0.3f, 0.3f, 0.32f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0, -0.007f), new Vector3(0.19f, 0.27f, 0.003f), C(0.75f, 0.8f, 0.85f), Quaternion.identity);
            });
            AddProp("mirror", "Mirror", mr, () => "E: check your helmet in the mirror", () => Show($"Looking sharp, {me?.Split(' ')[0] ?? "pilot"}. Visor note on straight.", 2.5f), 0.16f);

            // the toolbox: a repair kit, once a flight
            Vector3 tb = Spot(160, 0.08f, 0.3f, 1, 0.3f);
            tb.y = 0.08f;
            var tbq = Face(tb, 1);
            Piece("Toolbox", tb, tbq, b =>
            {
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(Vector3.zero, new Vector3(0.36f, 0.16f, 0.18f), C(0.8f, 0.12f, 0.1f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.1f, 0), new Vector3(0.2f, 0.02f, 0.03f), C(0.2f, 0.2f, 0.22f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.0f, -0.091f), new Vector3(0.05f, 0.03f, 0.006f), C(0.75f, 0.75f, 0.78f), Quaternion.identity);
            });
            AddProp("toolbox", "Toolbox", tb + Vector3.up * 0.1f, () => toolboxTaken ? "The toolbox: you've taken its repair kit" : "E: take a repair kit from the toolbox", () =>
            {
                if (toolboxTaken) { KitSound("ui_error"); return; }
                if (PackAdd(me, "repairkit", out string msg)) { toolboxTaken = true; Show("A repair kit, into your backpack.", 2f); }
                else Show(msg, 2f);
            }, 0.2f);

            // the handheld game, on the console
            Vector3 hg = roomRound ? new Vector3(0.5f, 0.465f, roomR - 0.6f) : new Vector3(0.6f, 0.465f, hl - 0.6f);
            Piece("Handheld game", hg, Quaternion.Euler(-10f, 20f, 0), b =>
            {
                b.Mat = MeshBuilder.MatGloss;
                b.AddBox(new Vector3(0, 0.008f, 0), new Vector3(0.12f, 0.016f, 0.07f), C(0.35f, 0.35f, 0.85f), Quaternion.identity);
                b.AddBox(new Vector3(0, 0.017f, -0.004f), new Vector3(0.06f, 0.002f, 0.042f), C(0.25f, 0.55f, 0.3f), Quaternion.identity);
                b.AddBox(new Vector3(-0.045f, 0.018f, 0), new Vector3(0.016f, 0.004f, 0.016f), C(0.15f, 0.15f, 0.17f), Quaternion.identity);
                b.AddSphere(new Vector3(0.045f, 0.018f, 0.008f), 0.006f, C(0.9f, 0.25f, 0.25f), 8, 5);
                b.AddSphere(new Vector3(0.045f, 0.018f, -0.01f), 0.006f, C(0.25f, 0.45f, 0.9f), 8, 5);
            });
            AddProp("handheld", "Handheld game", hg + Vector3.up * 0.02f, () => gameBest > 0 ? $"E: play  (best {gameBest})" : "E: play a game", () =>
            {
                int score = UnityEngine.Random.Range(40, 1000);
                bool best = score > gameBest;
                if (best) gameBest = score;
                KitSound(best ? "ui_confirm" : "ui_click", 0.4f);
                Show(best ? $"Score {score}: a new best!" : $"Score {score}. Your best is {gameBest}.", 2f);
            }, 0.09f);
        }
    }
}
