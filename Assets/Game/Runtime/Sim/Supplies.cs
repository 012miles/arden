using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    /// <summary>
    /// Supplies for long voyages (food, water and air), counted in crew-days: one crew member uses one a day. Capsules
    /// and habitats carry a few days' worth; supply crates and modules carry more; a greenhouse grows more using power.
    /// When a ship runs out, the crew have two days before they die. A game choice (on for new careers).
    /// </summary>
    public static class Supplies
    {
        public const double Day = 86400;
        /// <summary>Mass of one crew-day of supplies (t).</summary>
        public const double MassPerDay = 0.005;
        /// <summary>How long a crew lasts after the supplies run out (s).</summary>
        public const double Grace = 2 * Day;

        /// <summary>Days a design's supplies would last its crew (infinity with no crew; greenhouses included).</summary>
        public static double DaysFor(VesselDesign d, int crewOverride = -1)
        {
            double store = 0, grow = 0;
            int crew = 0;
            foreach (var n in d.nodes)
            {
                var def = PartCatalog.Get(n.id);
                if (def == null) continue;
                store += def.supplyCapacity;
                grow += def.supplyGrow;
                crew += def.crew;
            }
            if (crewOverride >= 0) crew = crewOverride;
            double net = crew - grow;
            return crew <= 0 ? double.PositiveInfinity : net <= 0 ? double.PositiveInfinity : store / net;
        }

        public static string DaysText(double days) => double.IsInfinity(days) ? "no limit" : days >= 2 ? $"{days:0.#} days" : Ui0(days * Day);

        static string Ui0(double s) { long t = (long)s; return t >= 3600 ? $"{t / 3600}h {(t / 60) % 60:00}m" : $"{t / 60}m"; }
    }

    public partial class VesselSim
    {
        /// <summary>When this ship's supplies ran out with crew aboard (-1 while there are supplies).</summary>
        public double starvingSince = -1;
        /// <summary>Set when the crew have just died for lack of supplies (the flight stops warping and says so).</summary>
        public string deathNote;
        /// <summary>Greenhouses that grew food in the last update (for the HUD).</summary>
        public bool greenhouseOn;

        public double SuppliesStored { get { double s = 0; foreach (var p in parts) s += p.supplies; return s; } }
        public double SuppliesCapacity { get { double s = 0; foreach (var p in parts) s += p.def.supplyCapacity; return s; } }

        /// <summary>Whether supplies count for this ship now: the game has them on, there's crew, and they aren't home.</summary>
        public bool UsesSupplies => career != null && career.suppliesOn && CrewCount > 0 && !(landed && body.IsHomeWorld);

        /// <summary>Crew-days used per day, after what the greenhouses grow (negative = more grown than eaten).</summary>
        public double SupplyNetUse { get { double g = 0; foreach (var p in parts) g += p.def.supplyGrow; return CrewCount - (greenhouseOn ? g : 0); } }

        /// <summary>Days until the supplies run out (infinity if they never will).</summary>
        public double SupplyDaysLeft()
        {
            if (!UsesSupplies) return double.PositiveInfinity;
            double net = SupplyNetUse;
            return net <= 0 ? double.PositiveInfinity : SuppliesStored / net;
        }

        void TakeSupplies(double amount)
        {
            double have = SuppliesStored;
            if (have <= 0 || amount <= 0) return;
            double k = Math.Min(1, amount / have);
            foreach (var p in parts) p.supplies -= p.supplies * k;
        }

        public double AddSupplies(double amount)
        {
            double room = 0;
            foreach (var p in parts) room += Math.Max(0, p.def.supplyCapacity - p.supplies);
            if (room <= 0 || amount <= 0) return 0;
            double k = Math.Min(1, amount / room), moved = 0;
            foreach (var p in parts) { double r = Math.Max(0, p.def.supplyCapacity - p.supplies) * k; p.supplies += r; moved += r; }
            return moved;
        }

        /// <summary>
        /// The crew eat (one crew-day each per day), greenhouses grow food while they have power, and a ship that has run
        /// out starts its two-day countdown; when that ends, everyone aboard dies.
        /// </summary>
        void UpdateSupplies(double dt, double tEnd)
        {
            greenhouseOn = false;
            if (!UsesSupplies) { starvingSince = -1; return; }
            double days = dt / Supplies.Day;
            // Greenhouses: grow while powered
            double grow = 0, ec = 0;
            foreach (var p in parts) if (p.def.supplyGrow > 0) { grow += p.def.supplyGrow; ec += p.def.supplyEc; }
            if (grow > 0 && EcStored >= ec * Math.Min(dt, 10))
            {
                double drawn = -AddCharge(-ec * dt);
                double share = ec * dt > 0 ? Math.Min(1, drawn / (ec * dt)) : 1;
                greenhouseOn = share > 0.5;
                grow *= share;
            }
            else grow = 0;
            double eat = CrewCount * days, made = grow * days;
            if (made > eat) AddSupplies(made - eat);
            else TakeSupplies(eat - made);
            if (SuppliesStored > 1e-6) { starvingSince = -1; return; }
            if (starvingSince < 0) { starvingSince = tEnd; lastEvent = "Out of supplies! The crew have two days to get more."; }
            if (tEnd - starvingSince >= Supplies.Grace) CrewDie(tEnd);
        }

        /// <summary>Everyone aboard dies: the supplies ran out and stayed out for two days.</summary>
        void CrewDie(double ut)
        {
            var names = parts.SelectMany(p => p.crew).ToList();
            foreach (var p in parts) p.crew.Clear();
            foreach (var n in names) career?.LoseCrew(n, name, "the supplies ran out");
            starvingSince = -1;
            lastEvent = names.Count == 1 ? $"{names[0]} died: the supplies ran out." : $"The crew of {name} died: the supplies ran out.";
            deathNote = lastEvent;
            RecomputeMass();
        }
    }
}
