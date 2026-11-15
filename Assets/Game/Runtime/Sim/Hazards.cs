using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    /// <summary>
    /// Milestone 39: random things that happen while you're out there, our own set for a spaceflight game. Where you are
    /// decides what can happen: in thick air on the ground, lightning, dust devils, gusts and heat waves; on airless ground,
    /// meteor showers and the strange shimmer that lifts loose things; in space (and on airless ground), solar flares; in
    /// space, micrometeoroids and comms blackouts. One at a time, every few minutes.
    /// </summary>
    public enum HazardKind { None, Lightning, DustDevil, Gust, HeatWave, MeteorShower, SolarFlare, Shimmer, Micrometeoroids, Blackout }

    public class HazardDef
    {
        public HazardKind kind;
        public string name, warn, start, end;
        public float duration, warnSecs, weight;
        public bool thickAir, anyAir, airless, surface, space;
    }

    public static class Hazards
    {
        public static readonly HazardDef[] All =
        {
            new HazardDef { kind = HazardKind.Lightning, name = "Thunderstorm", duration = 35, weight = 3, thickAir = true, surface = true,
                start = "A thunderstorm rolls in: lightning! Get aboard, or keep away from your ship.", end = "The storm moves on." },
            new HazardDef { kind = HazardKind.DustDevil, name = "Dust devil", duration = 40, weight = 3, anyAir = true, surface = true,
                start = "A dust devil is spinning this way! It flings loose things (and Ardlings) about.", end = "The dust devil has blown itself out." },
            new HazardDef { kind = HazardKind.Gust, name = "Strong wind", duration = 25, weight = 2, anyAir = true, surface = true,
                start = "Strong wind! Loose things blow about, and walking against it is tiring.", end = "The wind drops." },
            new HazardDef { kind = HazardKind.HeatWave, name = "Heat wave", duration = 60, weight = 2, anyAir = true, surface = true,
                start = "A heat wave: Ardlings outside get hungry fast, and engines run hot.", end = "The heat wave passes." },
            new HazardDef { kind = HazardKind.MeteorShower, name = "Meteor shower", duration = 45, weight = 3, airless = true, surface = true,
                start = "Meteor shower! Rocks are falling from the sky. Some might be worth picking up afterwards...", end = "The meteor shower is over." },
            new HazardDef { kind = HazardKind.Shimmer, name = "Strange shimmer", duration = 18, weight = 1, airless = true, surface = true,
                start = "A strange shimmer in the air nearby... loose things are lifting off the ground!", end = "The shimmer fades. Everything drops back down." },
            new HazardDef { kind = HazardKind.SolarFlare, name = "Solar flare", duration = 40, warnSecs = 10, weight = 2, airless = true, surface = true, space = true,
                warn = "Solar flare in 10 seconds: Ardlings outside should get inside a ship!", start = "Solar flare! Radiation hurts anyone outside.", end = "The solar flare has passed." },
            new HazardDef { kind = HazardKind.Micrometeoroids, name = "Micrometeoroids", duration = 25, weight = 3, space = true,
                start = "Micrometeoroids! Tiny specks of rock are pinging off the hull.", end = "Clear of the micrometeoroids." },
            new HazardDef { kind = HazardKind.Blackout, name = "Comms blackout", duration = 30, weight = 2, space = true,
                start = "Comms blackout: interference has cut the signal for half a minute.", end = "Signal's back." },
        };

        /// <summary>Real seconds before the first, and between, hazards (half as often again on Easy).</summary>
        public const float FirstGap = 240, MinGap = 200, MaxGap = 480;

        public static HazardDef Get(HazardKind k) => All.FirstOrDefault(h => h.kind == k);

        /// <summary>
        /// What can happen here. thick: air you could breathe (lightning needs it); air: any air at all; surface: on or near
        /// the ground; space: above the air, off the ground.
        /// </summary>
        public static List<HazardDef> Eligible(bool thick, bool air, bool surface, bool space)
        {
            return All.Where(h =>
                (h.surface && surface && (h.thickAir ? thick : h.anyAir ? air : h.airless ? !air : true)) ||
                (h.space && space)).ToList();
        }

        /// <summary>Picks one of what can happen here, by weight (the same for the same seed), or null.</summary>
        public static HazardDef Pick(int seed, bool thick, bool air, bool surface, bool space)
        {
            var list = Eligible(thick, air, surface, space);
            if (list.Count == 0) return null;
            var rng = new Random(seed);
            double r = rng.NextDouble() * list.Sum(h => h.weight);
            foreach (var h in list) { r -= h.weight; if (r <= 0) return h; }
            return list[list.Count - 1];
        }
    }
}
