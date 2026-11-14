using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    /// <summary>A ground cable between two landed vessels (by id).</summary>
    [Serializable]
    public class BaseLink
    {
        public int a, b;
    }

    /// <summary>
    /// Milestone 22: bases. Landed vessels joined by ground cables (an astronaut on EVA carries the cable from one to the
    /// other, up to 60 m) form a base. Linked modules share electricity, supplies and ore evenly; fuel and monopropellant
    /// flow from the base's own tanks into any landed ship plugged in (a refuelling depot), never the other way.
    /// Crew can move between linked modules without going outside. A cable comes loose if a module lifts off or
    /// rolls too far away.
    /// </summary>
    public static class Bases
    {
        public const double MaxCable = 60;          // m between the modules' centres
        public const double Slack = 5;              // a cable stretched past MaxCable by this much comes loose

        public static double Distance(VesselSim a, VesselSim b) => Vector3d.Distance(a.pos, b.pos);

        static bool Usable(VesselSim v) => v != null && !v.destroyed && v.landed && !v.IsEva;

        public static bool Linked(Universe u, VesselSim v) => v != null && u.links.Exists(l => l.a == v.id || l.b == v.id);

        /// <summary>The base a vessel belongs to (all modules joined to it by cables, itself first), or just itself.</summary>
        public static List<VesselSim> GroupOf(Universe u, VesselSim v)
        {
            var group = new List<VesselSim> { v };
            if (v == null) return group;
            for (int i = 0; i < group.Count; i++)
            {
                int id = group[i].id;
                foreach (var l in u.links)
                {
                    int other = l.a == id ? l.b : l.b == id ? l.a : 0;
                    if (other == 0) continue;
                    var o = u.Find(other);
                    if (o != null && !group.Contains(o)) group.Add(o);
                }
            }
            return group;
        }

        /// <summary>Every base (groups of two or more linked modules).</summary>
        public static List<List<VesselSim>> Groups(Universe u)
        {
            var seen = new HashSet<VesselSim>();
            var all = new List<List<VesselSim>>();
            foreach (var l in u.links)
            {
                var v = u.Find(l.a);
                if (v == null || seen.Contains(v)) continue;
                var g = GroupOf(u, v);
                foreach (var x in g) seen.Add(x);
                if (g.Count > 1) all.Add(g);
            }
            return all;
        }

        /// <summary>Why two vessels can't be cabled together, or null if they can.</summary>
        public static string CanConnect(Universe u, VesselSim a, VesselSim b)
        {
            if (a == b) return "That's the same module.";
            if (!Usable(a) || !Usable(b)) return "Both modules have to be landed.";
            if (a.body != b.body) return "They're on different worlds.";
            if (Distance(a, b) > MaxCable) return $"Too far apart: the cable reaches {MaxCable:0} m, they're {Distance(a, b):0} m apart.";
            if (u.links.Exists(l => (l.a == a.id && l.b == b.id) || (l.a == b.id && l.b == a.id))) return "They're already connected.";
            return null;
        }

        public static string Connect(Universe u, VesselSim a, VesselSim b)
        {
            string why = CanConnect(u, a, b);
            if (why != null) return why;
            u.links.Add(new BaseLink { a = a.id, b = b.id });
            return null;
        }

        /// <summary>Unplugs every cable from a module. Returns how many came out.</summary>
        public static int Unplug(Universe u, VesselSim v) => u.links.RemoveAll(l => l.a == v.id || l.b == v.id);

        /// <summary>Drops cables whose modules are gone, flying, on another world or pulled too far apart. Returns what happened.</summary>
        public static List<string> Validate(Universe u)
        {
            var notes = new List<string>();
            for (int i = u.links.Count - 1; i >= 0; i--)
            {
                var l = u.links[i];
                VesselSim a = u.Find(l.a), b = u.Find(l.b);
                bool ok = Usable(a) && Usable(b) && a.body == b.body && Distance(a, b) <= MaxCable + Slack;
                if (ok) continue;
                u.links.RemoveAt(i);
                if (a != null && b != null && !a.destroyed && !b.destroyed) notes.Add($"The cable between {a.name} and {b.name} came loose.");
            }
            return notes;
        }

        // ---------------------------------------------------------------- sharing

        static bool IsShip(VesselSim v) => v.parts.Exists(p => p.def.IsEngine && !p.def.IsJet);

        /// <summary>Evens out what linked modules hold. Cheap: call it every frame.</summary>
        public static void Share(Universe u)
        {
            if (u.links.Count == 0) return;
            foreach (var g in Groups(u))
            {
                var parts = g.SelectMany(v => v.parts).ToList();
                Even(parts, p => p.def.ecCapacity, p => p.charge, (p, x) => p.charge = x);
                Even(parts, p => p.def.supplyCapacity, p => p.supplies, (p, x) => p.supplies = x);
                Even(parts, p => p.def.oreCapacity, p => p.ore, (p, x) => p.ore = x);
                // Fuel: evened out among the base's own tanks, then poured into plugged-in ships until they're full
                var store = g.Where(v => !IsShip(v)).SelectMany(v => v.parts).ToList();
                var ships = g.Where(IsShip).SelectMany(v => v.parts).ToList();
                foreach (var (cap, get, set) in new (Func<PartState, double>, Func<PartState, double>, Action<PartState, double>)[]
                {
                    (p => p.def.kind == PartKind.Solid ? 0 : p.def.propellant, p => p.propellant, (p, x) => p.propellant = x),
                    (p => p.def.mono, p => p.mono, (p, x) => p.mono = x),
                })
                {
                    Even(store, cap, get, set);
                    double have = store.Sum(get), room = ships.Sum(p => Math.Max(0, cap(p) - get(p)));
                    double move = Math.Min(have, room);
                    if (move <= 1e-9) continue;
                    foreach (var p in ships) { double r = Math.Max(0, cap(p) - get(p)); set(p, get(p) + move * r / room); }
                    double left = have - move, storeCap = store.Sum(cap);
                    foreach (var p in store) set(p, storeCap > 0 ? cap(p) / storeCap * left : 0);
                }
                foreach (var v in g) v.RecomputeMass();
            }
        }

        static void Even(List<PartState> parts, Func<PartState, double> cap, Func<PartState, double> get, Action<PartState, double> set)
        {
            double total = 0, capacity = 0;
            foreach (var p in parts) { double c = cap(p); if (c <= 0) continue; total += get(p); capacity += c; }
            if (capacity <= 0) return;
            double f = Math.Min(1, total / capacity);
            foreach (var p in parts) { double c = cap(p); if (c > 0) set(p, c * f); }
        }

        // ---------------------------------------------------------------- crew and summaries

        /// <summary>Moves one astronaut from one linked module to a free seat in another (through the connecting tunnel).</summary>
        public static string MoveCrew(Universe u, VesselSim from, VesselSim to)
        {
            if (!GroupOf(u, from).Contains(to)) return "Those modules aren't connected.";
            var src = from.parts.FirstOrDefault(p => p.crew.Count > 0);
            if (src == null) return $"Nobody is in {from.name}.";
            var dst = to.parts.FirstOrDefault(p => p.def.crew > p.crew.Count);
            if (dst == null) return $"No free seat in {to.name}.";
            string who = src.crew[src.crew.Count - 1];
            src.crew.RemoveAt(src.crew.Count - 1);
            dst.crew.Add(who);
            var m = u.career.roster.Find(c => c.name == who);
            if (m != null) m.vesselId = to.id;
            return $"{who} walked over to {to.name}.";
        }

        public struct Summary
        {
            public int modules, crew, seats;
            public double ec, ecCap, supplies, ore, oreCap, fuel, fuelCap, labPerDay, supplyDays;
            public bool lab, greenhouse, antenna, solar;
        }

        public static Summary Summarize(List<VesselSim> g)
        {
            var s = new Summary { modules = g.Count };
            double use = 0, grow = 0;
            foreach (var v in g)
            {
                s.labPerDay += v.labRate;
                foreach (var p in v.parts)
                {
                    s.crew += p.crew.Count;
                    s.seats += p.def.crew;
                    s.ec += p.charge; s.ecCap += p.def.ecCapacity;
                    s.supplies += p.supplies;
                    s.ore += p.ore; s.oreCap += p.def.oreCapacity;
                    if (p.def.kind != PartKind.Solid) { s.fuel += p.propellant; s.fuelCap += p.def.propellant; }
                    if (p.def.labRate > 0) s.lab = true;
                    if (p.def.supplyGrow > 0) { s.greenhouse = true; grow += p.def.supplyGrow; }
                    if (p.def.kind == PartKind.Antenna) s.antenna = true;
                    if (p.def.kind == PartKind.Solar) s.solar = true;
                }
            }
            use = s.crew - grow;
            s.supplyDays = use > 0 ? s.supplies / use : double.PositiveInfinity;
            return s;
        }

        /// <summary>A base's name: its hub's vessel name, or the oldest module's.</summary>
        public static string NameOf(List<VesselSim> g)
        {
            var hub = g.FirstOrDefault(v => v.parts.Exists(p => p.def.id == "base.hub"));
            return (hub ?? g.OrderBy(v => v.id).First()).name;
        }
    }
}
