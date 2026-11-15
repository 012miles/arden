using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 40 (stage three of captaincarl24's "add the rest of all the systems in the doc"): rogue drones, our own
    /// enemies. Maintenance drones left at the outposts and wrecks have gone haywire: they hover at home, notice an Ardling
    /// who comes close (crouching gets you closer), chase, zap, give up if you get far enough from their home, and drift
    /// back. Seven kinds, from the little scout to the warden that guards the big outposts. Knock them out (the zapper,
    /// thrown things) and they drop what they were carrying.
    /// </summary>
    /// <remarks>
    /// Milestone 42 (captaincarl24: "a new enemy which are like mutants of ardlings ... also like boss fights"): the
    /// Shiftlings. Something grew in the old wrecks that copies Ardlings. A Shiftling walks about an outpost or wreck in an
    /// ordinary suit (in a room, sometimes wearing a teammate's look), and only when you're close does it turn: the suit
    /// darkens, the visor cracks open on a glow, and it comes at you. Lurkers are Shiftlings gone feral, fast on all fours.
    /// The Hollow, the thing at the heart of each wreck, is a boss: it slams the ground (everyone near is hurt), calls its
    /// Shiftlings once it's hurt, and goes faster and angrier near the end. They walk (no rotors), the rest is as drones.
    /// </remarks>
    public enum DroneKind { Scout, Hauler, Dart, Sparker, Mimic, Leaker, Warden, Shiftling, Lurker, Hollow }
    public enum DroneState { Idle, Alert, Chase, Attack, GiveUp, Home, Down }

    public class DroneDef
    {
        public DroneKind kind;
        public string name, about;
        public float health, speed, sight, reach, hit, every, drain, aura;
        public int reward;          // funds (valuable worth) dropped as a drone core
        public Color color;
        public float size = 0.5f;
        public bool walker, boss;   // milestone 42: on its feet (no hovering); a boss (phases, slams, a big health bar)
        public string drop = "dronecore";
    }

    [Serializable]
    public class Drone
    {
        public int uid;
        public DroneKind kind;
        public string body, home;      // the world, and the place it guards
        public double x, y, z;         // body-fixed position (m), hovering
        public double hx, hy, hz;      // home (body-fixed)
        public float health, cool, lost, alertT, maxHealth;
        public DroneState state;
        public string target;          // the Ardling it's after
        public string look;            // milestone 42: the suit a Shiftling wears while it passes as crew (a look code)
        public int phase, slams;       // milestone 42, a boss: which phase (0-2), and how many slams so far (for the views)
        public float slamT, summonT, t;
        public Vector3d Pos { get => new Vector3d(x, y, z); set { x = value.x; y = value.y; z = value.z; } }
        public Vector3d Home { get => new Vector3d(hx, hy, hz); set { hx = value.x; hy = value.y; hz = value.z; } }
        public DroneDef Def => Drones.Get(kind);
    }

    public static class Drones
    {
        static Color C(float r, float g, float b) => new Color(r, g, b);

        public static readonly DroneDef[] All =
        {
            new DroneDef { kind = DroneKind.Scout, name = "Scout drone", health = 30, speed = 4, sight = 32, reach = 2.2f, hit = 5, every = 1.5f, reward = 60, color = C(0.85f, 0.75f, 0.2f), size = 0.45f,
                about = "Small and nosy. Zaps." },
            new DroneDef { kind = DroneKind.Hauler, name = "Hauler drone", health = 120, speed = 2, sight = 26, reach = 2.6f, hit = 16, every = 2.5f, reward = 160, color = C(0.55f, 0.55f, 0.6f), size = 0.9f,
                about = "Slow, tough, and it hits hard." },
            new DroneDef { kind = DroneKind.Dart, name = "Dart drone", health = 18, speed = 8, sight = 40, reach = 1.8f, hit = 4, every = 1f, reward = 70, color = C(0.9f, 0.35f, 0.25f), size = 0.35f,
                about = "Very fast, very fragile." },
            new DroneDef { kind = DroneKind.Sparker, name = "Sparker drone", health = 40, speed = 3.5f, sight = 30, reach = 2.4f, hit = 5, every = 1.8f, drain = 25, reward = 110, color = C(0.3f, 0.65f, 1f), size = 0.5f,
                about = "Its zap saps your stamina." },
            new DroneDef { kind = DroneKind.Mimic, name = "Mimic crate", health = 50, speed = 3, sight = 5, reach = 2.2f, hit = 10, every = 1.5f, reward = 140, color = C(0.6f, 0.45f, 0.3f), size = 0.55f,
                about = "Looks just like a loot crate. Until it doesn't." },
            new DroneDef { kind = DroneKind.Leaker, name = "Leaker drone", health = 60, speed = 2.5f, sight = 28, reach = 2.4f, hit = 4, every = 1.5f, aura = 2.5f, reward = 120, color = C(0.55f, 0.85f, 0.25f), size = 0.6f,
                about = "Leaks something nasty: standing near it hurts." },
            new DroneDef { kind = DroneKind.Warden, name = "Warden", health = 400, speed = 3, sight = 45, reach = 10f, hit = 10, every = 2.2f, reward = 900, color = C(0.75f, 0.2f, 0.85f), size = 1.4f,
                about = "Guards the big outposts. Zaps from a distance and calls a scout." },
            new DroneDef { kind = DroneKind.Shiftling, name = "Shiftling", health = 45, speed = 4.4f, sight = 6, reach = 1.6f, hit = 8, every = 1.3f, reward = 220, color = C(0.42f, 0.16f, 0.55f), size = 1.2f,
                walker = true, drop = "shiftsample", about = "Looks like one of the crew. Isn't. Waits until you're close, then turns." },
            new DroneDef { kind = DroneKind.Lurker, name = "Lurker", health = 30, speed = 6.2f, sight = 16, reach = 1.4f, hit = 5, every = 0.9f, reward = 160, color = C(0.3f, 0.12f, 0.4f), size = 0.8f,
                walker = true, drop = "shiftsample", about = "A Shiftling gone feral: on all fours and very fast, but it doesn't take much." },
            new DroneDef { kind = DroneKind.Hollow, name = "The Hollow", health = 1500, speed = 2.6f, sight = 40, reach = 4.2f, hit = 18, every = 2.4f, reward = 6000, color = C(0.25f, 0.06f, 0.32f), size = 4.2f,
                walker = true, boss = true, drop = "hollowheart", about = "What grew in the wreck. Slams the ground, calls its Shiftlings, and gets angrier as it weakens." },
        };

        /// <summary>Milestone 42, the Hollow: how far its slam reaches, how hard it hits, how often (by phase), how often it calls Shiftlings.</summary>
        public const float SlamRadius = 8f, SlamHit = 16f, SummonEvery = 14f, BossLeash = 140f;
        public static float SlamEvery(int phase) => phase >= 2 ? 3.2f : 5f;

        /// <summary>A boss's phase from its health: 0 (fresh), 1 (hurt: calls Shiftlings), 2 (nearly down: faster, slams more).</summary>
        public static int PhaseOf(Drone d) => d.health > d.Def.health * 0.66f ? 0 : d.health > d.Def.health * 0.33f ? 1 : 2;

        /// <summary>The Hollow's health for a team: tougher for each player after the first.</summary>
        public static float BossHealth(int players) => Get(DroneKind.Hollow).health * (1f + 0.5f * Math.Max(0, players - 1));

        public static DroneDef Get(DroneKind k) => All.FirstOrDefault(d => d.kind == k) ?? All[0];

        public const float Hover = 1.6f, Leash = 70f, GiveUpAfter = 8f, Heal = 2f, Wake = 800f;

        /// <summary>Who guards a place: 1-3 drones at a random place, a warden and two scouts at the big outposts.</summary>
        public static List<DroneKind> Guards(string placeId, int seed)
        {
            var rng = new System.Random(seed ^ Landmarks.StableHash(placeId ?? "x"));
            var list = new List<DroneKind>();
            if (placeId != null && IsLair(placeId)) { list.Add(DroneKind.Hollow); list.Add(DroneKind.Shiftling); list.Add(DroneKind.Shiftling); return list; }   // milestone 42
            if (placeId != null && !placeId.StartsWith("site.")) { list.Add(DroneKind.Warden); list.Add(DroneKind.Scout); list.Add(DroneKind.Scout); list.Add(DroneKind.Shiftling); return list; }
            int n = 1 + rng.Next(3);
            DroneKind[] pool = { DroneKind.Scout, DroneKind.Scout, DroneKind.Scout, DroneKind.Dart, DroneKind.Sparker, DroneKind.Hauler, DroneKind.Mimic, DroneKind.Leaker,
                                 DroneKind.Shiftling, DroneKind.Shiftling, DroneKind.Lurker };   // (milestone 42)
            for (int i = 0; i < n; i++) list.Add(pool[rng.Next(pool.Length)]);
            return list;
        }

        /// <summary>Milestone 42: the wrecks are where the Hollows grew.</summary>
        public static bool IsLair(string placeId) => placeId == "tal.lander" || placeId == "ochre.wreck" || placeId == "rime.wreck";

        public static (string who, float dmg, float drain)? Step(Drone d, float dt, IList<(string name, Vector3d pos, bool crouch)> targets, double surfaceR)
        {
            bool summon = false;
            return Step(d, dt, targets, surfaceR, null, ref summon);
        }

        /// <summary>
        /// One drone's step (all in the world's turning frame, metres). targets: the Ardlings on foot here (name, position,
        /// crouching). Returns the Ardling it hit this step (and how hard), or null. A boss's slam hits everyone near: those go
        /// in 'slam' (when given), and 'summon' says it's time to call Shiftlings.
        /// </summary>
        public static (string who, float dmg, float drain)? Step(Drone d, float dt, IList<(string name, Vector3d pos, bool crouch)> targets, double surfaceR,
            List<(string who, float dmg)> slam, ref bool summon)
        {
            var def = d.Def;
            if (d.state == DroneState.Down) return null;
            d.t += dt;
            d.cool = Mathf.Max(0, d.cool - dt);
            Vector3d pos = d.Pos, home = d.Home;
            (string name, Vector3d pos, bool crouch)? tgt = null;
            if (d.target != null) foreach (var t in targets) if (t.name == d.target) tgt = t;
            if (tgt == null) d.target = null;
            // noticing: the nearest Ardling inside its sight (crouching halves it)
            if (d.target == null && d.state != DroneState.GiveUp)
            {
                double best = double.MaxValue;
                foreach (var t in targets)
                {
                    double dist = (t.pos - pos).magnitude, sight = def.sight * (t.crouch ? 0.5 : 1.0);
                    if (dist < sight && dist < best) { best = dist; tgt = t; }
                }
                if (tgt != null) { d.target = tgt.Value.name; d.state = DroneState.Alert; d.alertT = d.kind == DroneKind.Shiftling ? 0.9f : def.boss ? 2f : 1.5f; }   // (a moment to react; a Shiftling turning, a boss rising)
            }
            (string, float, float)? hit = null;
            switch (d.state)
            {
                case DroneState.Idle:
                    if (def.walker && !def.boss)
                    {
                        // milestone 42: a Shiftling strolls round its place like one of the crew
                        Vector3d up = home.normalized, e = Vector3d.Cross(new Vector3d(0, 1, 0), up);
                        if (e.magnitude < 1e-6) e = new Vector3d(1, 0, 0);
                        e = e.normalized;
                        Vector3d n = Vector3d.Cross(up, e).normalized;
                        double a = d.uid * 1.7 + d.t * 0.11;
                        pos = Toward(pos, home + (e * Math.Cos(a) + n * Math.Sin(a * 1.3)) * 6, def.speed * 0.2f * dt);
                    }
                    else pos = Toward(pos, home, def.speed * 0.3f * dt);   // hover round home, bobbing
                    d.health = Mathf.Min(MaxHealth(d), d.health + Heal * dt);
                    break;
                case DroneState.Alert:
                    d.alertT -= dt;
                    if (d.alertT <= 0) d.state = DroneState.Chase;
                    break;
                case DroneState.Chase:
                case DroneState.Attack:
                {
                    if (tgt == null) { d.state = DroneState.Home; break; }
                    var tp = tgt.Value.pos;
                    double dist = (tp - pos).magnitude;
                    double leash = def.boss ? BossLeash : Leash;
                    if ((tp - home).magnitude > leash) { d.lost += dt; if (d.lost > GiveUpAfter * 0.25f) { d.state = DroneState.GiveUp; d.target = null; d.lost = 0; break; } }
                    else if (dist > def.sight * 1.8) { d.lost += dt; if (d.lost > GiveUpAfter) { d.state = DroneState.GiveUp; d.target = null; d.lost = 0; break; } }
                    else d.lost = 0;
                    float speed = def.speed * (def.boss && d.phase >= 2 ? 1.5f : 1f);
                    if (dist > def.reach * 0.8) { d.state = DroneState.Chase; pos = Toward(pos, tp, speed * dt); }
                    else d.state = DroneState.Attack;
                    if (dist <= def.reach && d.cool <= 0) { d.cool = def.every; hit = (tgt.Value.name, def.hit, def.drain); }
                    break;
                }
                case DroneState.GiveUp:
                case DroneState.Home:
                    pos = Toward(pos, home, def.speed * dt);
                    if ((pos.normalized - home.normalized).magnitude * surfaceR < 1.5) d.state = DroneState.Idle;   // (along the ground: it hovers above its home)
                    d.health = Mathf.Min(MaxHealth(d), d.health + Heal * dt);
                    break;
            }
            // milestone 42, a boss: its phase; the slam (everyone near is hurt) and calling its Shiftlings while it fights
            if (def.boss)
            {
                d.phase = PhaseOf(d);
                if (d.state == DroneState.Chase || d.state == DroneState.Attack)
                {
                    d.slamT -= dt;
                    if (d.slamT <= 0)
                    {
                        d.slamT = SlamEvery(d.phase);
                        d.slams++;
                        foreach (var t in targets) if ((t.pos - pos).magnitude < SlamRadius) slam?.Add((t.name, SlamHit));
                    }
                    if (d.phase >= 1) { d.summonT -= dt; if (d.summonT <= 0) { d.summonT = SummonEvery; summon = true; } }
                }
                else { d.slamT = Mathf.Max(d.slamT, 2.5f); d.summonT = Mathf.Max(d.summonT, 4f); }
            }
            // hover above the ground (milestone 42: walkers stand on it)
            pos = pos.normalized * (surfaceR + (def.walker ? 0 : Hover + def.size * 0.5));
            d.Pos = pos;
            // a leaker hurts anyone close, whatever it's doing
            if (def.aura > 0 && hit == null)
                foreach (var t in targets) if ((t.pos - pos).magnitude < 4) { hit = (t.name, def.aura * dt, 0f); break; }
            return hit;
        }

        static Vector3d Toward(Vector3d from, Vector3d to, double step)
        {
            Vector3d d = to - from;
            double m = d.magnitude;
            return m <= step || m < 1e-6 ? to : from + d / m * step;
        }

        /// <summary>Full health for this one (a boss's is set for the team when it wakes, and kept).</summary>
        public static float MaxHealth(Drone d) => d.Def.boss ? Math.Max(d.Def.health, d.maxHealth) : d.Def.health;

        /// <summary>Hits a drone; true if that knocked it out.</summary>
        public static bool Damage(Drone d, float amount)
        {
            if (d.state == DroneState.Down) return false;
            d.health -= amount;
            if (d.health > 0) { if (d.state == DroneState.Idle) d.state = DroneState.Chase; return false; }
            d.health = 0;
            d.state = DroneState.Down;
            d.target = null;
            return true;
        }
    }
}
