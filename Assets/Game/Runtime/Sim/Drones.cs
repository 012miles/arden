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
    public enum DroneKind { Scout, Hauler, Dart, Sparker, Mimic, Leaker, Warden }
    public enum DroneState { Idle, Alert, Chase, Attack, GiveUp, Home, Down }

    public class DroneDef
    {
        public DroneKind kind;
        public string name, about;
        public float health, speed, sight, reach, hit, every, drain, aura;
        public int reward;          // funds (valuable worth) dropped as a drone core
        public Color color;
        public float size = 0.5f;
    }

    [Serializable]
    public class Drone
    {
        public int uid;
        public DroneKind kind;
        public string body, home;      // the world, and the place it guards
        public double x, y, z;         // body-fixed position (m), hovering
        public double hx, hy, hz;      // home (body-fixed)
        public float health, cool, lost, alertT;
        public DroneState state;
        public string target;          // the Ardling it's after
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
        };

        public static DroneDef Get(DroneKind k) => All.FirstOrDefault(d => d.kind == k) ?? All[0];

        public const float Hover = 1.6f, Leash = 70f, GiveUpAfter = 8f, Heal = 2f, Wake = 800f;

        /// <summary>Who guards a place: 1-3 drones at a random place, a warden and two scouts at the big outposts.</summary>
        public static List<DroneKind> Guards(string placeId, int seed)
        {
            var rng = new System.Random(seed ^ Landmarks.StableHash(placeId ?? "x"));
            var list = new List<DroneKind>();
            if (placeId != null && !placeId.StartsWith("site.")) { list.Add(DroneKind.Warden); list.Add(DroneKind.Scout); list.Add(DroneKind.Scout); return list; }
            int n = 1 + rng.Next(3);
            DroneKind[] pool = { DroneKind.Scout, DroneKind.Scout, DroneKind.Scout, DroneKind.Dart, DroneKind.Sparker, DroneKind.Hauler, DroneKind.Mimic, DroneKind.Leaker };
            for (int i = 0; i < n; i++) list.Add(pool[rng.Next(pool.Length)]);
            return list;
        }

        /// <summary>
        /// One drone's step (all in the world's turning frame, metres). targets: the Ardlings on foot here (name, position,
        /// crouching). Returns the Ardling it hit this step (and how hard), or null.
        /// </summary>
        public static (string who, float dmg, float drain)? Step(Drone d, float dt, IList<(string name, Vector3d pos, bool crouch)> targets, double surfaceR)
        {
            var def = d.Def;
            if (d.state == DroneState.Down) return null;
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
                if (tgt != null) { d.target = tgt.Value.name; d.state = DroneState.Alert; d.alertT = 1.5f; }   // (a moment to react)
            }
            (string, float, float)? hit = null;
            switch (d.state)
            {
                case DroneState.Idle:
                    // hover round home, bobbing
                    pos = Toward(pos, home, def.speed * 0.3f * dt);
                    d.health = Mathf.Min(def.health, d.health + Heal * dt);
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
                    if ((tp - home).magnitude > Leash) { d.lost += dt; if (d.lost > GiveUpAfter * 0.25f) { d.state = DroneState.GiveUp; d.target = null; d.lost = 0; break; } }
                    else if (dist > def.sight * 1.8) { d.lost += dt; if (d.lost > GiveUpAfter) { d.state = DroneState.GiveUp; d.target = null; d.lost = 0; break; } }
                    else d.lost = 0;
                    if (dist > def.reach * 0.8) { d.state = DroneState.Chase; pos = Toward(pos, tp, def.speed * dt); }
                    else d.state = DroneState.Attack;
                    if (dist <= def.reach && d.cool <= 0) { d.cool = def.every; hit = (tgt.Value.name, def.hit, def.drain); }
                    break;
                }
                case DroneState.GiveUp:
                case DroneState.Home:
                    pos = Toward(pos, home, def.speed * dt);
                    if ((pos.normalized - home.normalized).magnitude * surfaceR < 1.5) d.state = DroneState.Idle;   // (along the ground: it hovers above its home)
                    d.health = Mathf.Min(def.health, d.health + Heal * dt);
                    break;
            }
            // hover above the ground
            pos = pos.normalized * (surfaceR + Hover + def.size * 0.5);
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
