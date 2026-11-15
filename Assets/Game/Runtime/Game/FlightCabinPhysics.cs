using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 43 (captaincarl24: "add collision in the interior", "add zero gravity and stuff and we gotta like grab onto
    /// stuff to move around when in space", "if the ships falling upside down we're pushed to the back"): the cabin follows
    /// what the ship feels (VesselSim.feltLocal). Standing on the pad or pushed by the engines, the floor is down and you
    /// walk (slower under heavy g, not at all past 3 g). Upside down, on its side or tumbling, you fall that way and slide
    /// about. Coasting, you're weightless: you drift, and to move you grab on (near a wall, the floor, the ceiling, the
    /// furniture or a yellow handrail) and push off the way you're looking. The desk, the consoles, the seats, the locker
    /// and the bunks are solid. Planes and rovers keep their floor (their cabins are built level), but still float.
    /// </summary>
    public partial class FlightMode
    {
        enum CabinMove { Walk, Pinned, Falling, Floating, Holding }
        CabinMove cabinMove;
        Vector3 ivel;
        float cabinG;   // how hard the pull is (in g)
        readonly List<(Vector3 c, Vector3 half, float top)> obstacles = new List<(Vector3, Vector3, float)>();
        readonly List<Vector3> handholds = new List<Vector3>();
        const float BodyR = 0.26f, BodyH = 1.2f, RoomH = 2.1f;

        /// <summary>For tours: how the Ardling's moving in the cabin, and where.</summary>
        public string TestCabinMove => $"{cabinMove} at {ipos.x:0.00},{ipos.y:0.00},{ipos.z:0.00} ({cabinG:0.0} g)";
        public void TestCabinPush(Vector3 v) => ivel = v;

        /// <summary>The solid furniture and the handrails, for the cabin just built (the same places FlightCabin builds them).</summary>
        void BuildCabinCollision(bool hab, float front)
        {
            obstacles.Clear(); handholds.Clear(); ivel = Vector3.zero;
            float width = hab ? 2.0f : 1.6f;
            obstacles.Add((new Vector3(0, 0, front - 0.42f), new Vector3(width / 2f, 0, 0.3f), 0.9f));                    // the desk (and its screens)
            float side = hab ? roomW / 2 - 0.35f : 0.98f, consoleZ = hab ? roomL / 2 - 0.95f : roomR - 1.9f;
            foreach (int s in new[] { -1, 1 }) obstacles.Add((new Vector3(s * side, 0, consoleZ), new Vector3(0.14f, 0, 0.23f), 0.62f));   // the side consoles
            foreach (var st in seats) obstacles.Add((st, new Vector3(0.27f, 0, 0.3f), 0.95f));                              // the seats
            float hl = hab ? roomL / 2 : 0, hw = hab ? roomW / 2 : 0;
            Vector3 lk;
            if (roomRound) { float a = 225f * Mathf.Deg2Rad, r = roomR * Mathf.Cos(Mathf.PI / 12f) - 0.25f; lk = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r); }
            else lk = new Vector3(hw - 0.55f, 0, -hl + 0.25f);
            obstacles.Add((lk, new Vector3(0.34f, 0, 0.34f), 1.9f));                                                      // the locker
            if (hab)
            {
                obstacles.Add((new Vector3(-hw + 0.4f, 0, -hl + 1.3f), new Vector3(0.4f, 0, 0.78f), 1.3f));                // the bunks
                obstacles.Add((new Vector3(hw - 0.4f, 0, -hl + 1.3f), new Vector3(0.4f, 0, 0.78f), 1.3f));
                obstacles.Add((new Vector3(hw - 0.55f, 0, -0.4f), new Vector3(0.38f, 0, 0.2f), 0.85f));                    // the exercise bike
                obstacles.Add((new Vector3(0, 0, -0.4f), new Vector3(0.42f, 0, 0.42f), 0.6f));                            // the table
            }
            // yellow handrails to hold on to when weightless: round the walls at chest height, and across the ceiling
            Color rail = new Color(0.95f, 0.78f, 0.15f);
            if (roomRound)
            {
                foreach (float deg in new[] { 60f, 120f, 165f, 195f, 240f, 300f })
                {
                    float a = deg * Mathf.Deg2Rad, r = roomR * Mathf.Cos(Mathf.PI / 12f) - 0.08f;
                    Vector3 at = new Vector3(Mathf.Sin(a) * r, 1.15f, Mathf.Cos(a) * r);
                    var q = Quaternion.LookRotation(new Vector3(at.x, 0, at.z).normalized, Vector3.up);
                    Piece("Handrail", at, q, b => { b.Mat = MeshBuilder.MatGloss; b.AddBox(Vector3.zero, new Vector3(0.5f, 0.035f, 0.035f), rail, Quaternion.identity); foreach (int s in new[] { -1, 1 }) b.AddBox(new Vector3(s * 0.23f, 0, 0.04f), new Vector3(0.03f, 0.03f, 0.07f), rail * 0.8f, Quaternion.identity); });
                    handholds.Add(at);
                }
                foreach (float x in new[] { -0.7f, 0.7f })
                {
                    Vector3 at = new Vector3(x, RoomH - 0.1f, 0);
                    Piece("Ceiling rail", at, Quaternion.identity, b => { b.Mat = MeshBuilder.MatGloss; b.AddBox(Vector3.zero, new Vector3(0.035f, 0.035f, 1.4f), rail, Quaternion.identity); });
                    handholds.Add(at); handholds.Add(at + new Vector3(0, 0, 0.6f)); handholds.Add(at - new Vector3(0, 0, 0.6f));
                }
            }
            else
            {
                for (int k = 0; k < 4; k++)
                    foreach (int s in new[] { -1, 1 })
                    {
                        Vector3 at = new Vector3(s * (hw - 0.08f), 1.15f, -hl + 0.9f + k * (roomL - 1.8f) / 3f);
                        Piece("Handrail", at, Quaternion.LookRotation(new Vector3(s, 0, 0), Vector3.up), b => { b.Mat = MeshBuilder.MatGloss; b.AddBox(Vector3.zero, new Vector3(0.5f, 0.035f, 0.035f), rail, Quaternion.identity); });
                        handholds.Add(at);
                    }
                for (int k = 0; k < 3; k++) { Vector3 at = new Vector3(0, RoomH - 0.1f, -hl + 1.5f + k * (roomL - 3f) / 2f); Piece("Ceiling rail", at, Quaternion.identity, b => { b.Mat = MeshBuilder.MatGloss; b.AddBox(Vector3.zero, new Vector3(1.2f, 0.035f, 0.035f), rail, Quaternion.identity); }); handholds.Add(at); }
            }
        }

        /// <summary>Which way you're pulled in the cabin (m/s², cabin frame), and whether this cabin keeps its floor (planes, rovers).</summary>
        Vector3 CabinPull(out bool levelOnly)
        {
            levelOnly = active != null && active.parts.Exists(p => p.def.kind == PartKind.Gear || p.def.kind == PartKind.Wheel || p.def.kind == PartKind.Wing);
            if (active == null) return new Vector3(0, -9.81f, 0);
            Vector3 g = -active.feltLocal;
            if (levelOnly) g = new Vector3(0, -g.magnitude, 0);   // (built level: the pull only presses you down, or lets go)
            return g;
        }

        /// <summary>One frame of moving about the cabin: walking, being thrown about, or floating and pushing off.</summary>
        void CabinStep(float dt)
        {
            Vector3 g = CabinPull(out _);
            float gm = g.magnitude;
            cabinG = gm / 9.81f;
            var look = Quaternion.Euler(-ipitch, iyaw, 0);
            Vector3 fwdFlat = Quaternion.Euler(0, iyaw, 0) * Vector3.forward, rightFlat = Quaternion.Euler(0, iyaw, 0) * Vector3.right;
            float wIn = (In.Held(K.W) ? 1 : 0) - (In.Held(K.S) ? 1 : 0), dIn = (In.Held(K.D) ? 1 : 0) - (In.Held(K.A) ? 1 : 0);
            bool fast = In.Held(K.LeftCtrl);
            bool weightless = gm < 1.0f;
            bool floorDown = gm > 1e-3f && Vector3.Dot(g / gm, Vector3.down) > 0.8f;
            if (!weightless && floorDown && ipos.y <= 0.03f && ivel.y <= 0.6f)
            {
                // walking: heavier under thrust; the sideways part of the pull (leaning, speeding up) makes you stagger that way
                float speed = (fast ? 2.6f : 1.5f) / Mathf.Max(1f, Mathf.Sqrt(cabinG));
                if (cabinG > 3f) speed = 0f;
                Vector3 move = fwdFlat * wIn + rightFlat * dIn;
                if (move.sqrMagnitude > 1) move.Normalize();
                Vector3 slide = Vector3.ProjectOnPlane(g, Vector3.up) * 0.25f;
                Vector3 want = move * speed + slide;
                ivel = Vector3.Lerp(new Vector3(ivel.x, 0, ivel.z), want, Mathf.Clamp01(dt * 10f));
                ipos += ivel * dt;
                ipos.y = 0f;
                cabinMove = speed <= 0 ? CabinMove.Pinned : CabinMove.Walk;
            }
            else if (!weightless)
            {
                // pulled somewhere else (upside down, on its side, tumbling, or still in the air after being thrown): you fall
                ivel += g * dt;
                ivel *= 1f - 0.3f * dt;
                ipos += ivel * dt;
                cabinMove = CabinMove.Falling;
            }
            else
            {
                // weightless: you drift; holding on, W/A/S/D push off the way you look (SPACE pushes harder, up toward where you look)
                bool holding = NearSurface(0.4f) || NearHandhold(0.85f);
                ivel *= 1f - 0.04f * dt;
                if (holding)
                {
                    Vector3 push = look * Vector3.forward * wIn + rightFlat * dIn;
                    if (In.Down(K.Space)) push += look * Vector3.forward * 1.8f;
                    if (push.sqrMagnitude > 0.01f) ivel = push.normalized * (fast || In.Down(K.Space) ? 2.4f : 1.3f);
                    else ivel = Vector3.Lerp(ivel, Vector3.zero, Mathf.Clamp01(dt * 6f));   // holding on: you come to a stop
                }
                ipos += ivel * dt;
                cabinMove = holding ? CabinMove.Holding : CabinMove.Floating;
            }
            CollideCabin();
        }

        /// <summary>The floor, the ceiling, the walls and the furniture: you stop at them (and bounce a little when thrown).</summary>
        void CollideCabin()
        {
            float top = RoomH - BodyH - 0.05f;
            if (ipos.y < 0f) { ipos.y = 0f; if (ivel.y < 0) ivel.y = -ivel.y * 0.2f; ivel.x *= 0.85f; ivel.z *= 0.85f; }
            if (ipos.y > top) { ipos.y = top; if (ivel.y > 0) ivel.y = -ivel.y * 0.2f; }
            if (roomRound)
            {
                var flat = new Vector2(ipos.x, ipos.z);
                float lim = roomR * Mathf.Cos(Mathf.PI / 12f) - BodyR - 0.1f;
                if (flat.magnitude > lim)
                {
                    Vector2 n = flat.normalized;
                    flat = n * lim; ipos.x = flat.x; ipos.z = flat.y;
                    float into = ivel.x * n.x + ivel.z * n.y;
                    if (into > 0) { ivel.x -= n.x * into * 1.2f; ivel.z -= n.y * into * 1.2f; }
                }
            }
            else
            {
                float lx = roomW / 2 - BodyR - 0.1f, lz = roomL / 2 - BodyR - 0.1f;
                if (Mathf.Abs(ipos.x) > lx) { ipos.x = Mathf.Sign(ipos.x) * lx; ivel.x *= -0.2f; }
                if (Mathf.Abs(ipos.z) > lz) { ipos.z = Mathf.Sign(ipos.z) * lz; ivel.z *= -0.2f; }
            }
            foreach (var (c, half, h) in obstacles)
            {
                if (ipos.y >= h - 0.02f) continue;   // above it (floating over, or standing on it)
                float dx = ipos.x - c.x, dz = ipos.z - c.z;
                float px = half.x + BodyR - Mathf.Abs(dx), pz = half.z + BodyR - Mathf.Abs(dz);
                if (px <= 0 || pz <= 0) continue;
                // came down onto it from above: you land on top
                if (cabinMove == CabinMove.Falling && ivel.y < 0 && ipos.y > h - 0.25f) { ipos.y = h; ivel.y = 0; continue; }
                if (px < pz) { ipos.x += Mathf.Sign(dx == 0 ? 1 : dx) * px; if (ivel.x * dx < 0) ivel.x *= -0.1f; }
                else { ipos.z += Mathf.Sign(dz == 0 ? 1 : dz) * pz; if (ivel.z * dz < 0) ivel.z *= -0.1f; }
            }
        }

        bool NearSurface(float d)
        {
            if (ipos.y < d || ipos.y > RoomH - BodyH - d) return true;
            if (roomRound) { if (new Vector2(ipos.x, ipos.z).magnitude > roomR * Mathf.Cos(Mathf.PI / 12f) - BodyR - 0.1f - d) return true; }
            else if (Mathf.Abs(ipos.x) > roomW / 2 - BodyR - 0.1f - d || Mathf.Abs(ipos.z) > roomL / 2 - BodyR - 0.1f - d) return true;
            foreach (var (c, half, h) in obstacles)
                if (ipos.y < h + d && Mathf.Abs(ipos.x - c.x) < half.x + BodyR + d && Mathf.Abs(ipos.z - c.z) < half.z + BodyR + d) return true;
            return false;
        }

        bool NearHandhold(float d)
        {
            Vector3 chest = ipos + Vector3.up * 0.75f;
            foreach (var h in handholds) if ((h - chest).magnitude < d) return true;
            return false;
        }

        /// <summary>The line at the bottom of the cabin's screen for how you're moving.</summary>
        string CabinMoveHint()
        {
            switch (cabinMove)
            {
                case CabinMove.Pinned: return $"Pressed to the floor: {cabinG:0.0} g";
                case CabinMove.Falling: return cabinG > 0.2f ? "Thrown about: the ship's pulling you that way" : "Falling";
                case CabinMove.Floating: return "Weightless and drifting: get near a wall or a yellow handrail to grab on";
                case CabinMove.Holding: return "Holding on: W/A/S/D push off the way you look, SPACE pushes harder";
                default: return cabinG > 1.3f ? $"Heavy: {cabinG:0.0} g" : null;
            }
        }
    }
}
