using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 23 damage: parts char as they heat up (it stays: metal isn't perfect), every part has a pressure limit
    /// as well as its heat limit, and parts holding fuel don't just burn up, they explode: the blast heats and can set
    /// off the tanks next to them a moment later, so one burst fuel line can chain through a stage.
    /// </summary>
    public partial class VesselSim
    {
        /// <summary>Explosions this step (vessel frame), for the flight to draw and sound.</summary>
        public readonly List<Vector3> explosions = new List<Vector3>();
        readonly List<(int index, double at)> fuses = new List<(int, double)>();
        double damageClock;

        /// <summary>The dynamic pressure a part can take in the airflow (kPa), unless it's tucked inside a fairing.</summary>
        public static float PressureLimit(PartDef d)
        {
            if (d.maxPressure > 0) return d.maxPressure;
            switch (d.kind)
            {
                // Generous enough for ordinary flying (a fast plane low down sees ~100 kPa, a brisk rocket ~70);
                // a rocket that floors it through thick air, or a capsule slammed in steeply, finds the limit
                case PartKind.NoseCone: case PartKind.Fairing: case PartKind.FairingHalf: return 240f;
                case PartKind.HeatShield: return 300f;
                case PartKind.Command: return 170f;
                case PartKind.Tank: case PartKind.MonoTank: return 140f;
                case PartKind.Engine: case PartKind.Solid: return 150f;
                case PartKind.Wing: case PartKind.ControlSurface: case PartKind.Fins: return 180f;
                default: return 145f;
            }
        }

        static bool HoldsFuel(PartState p) =>
            (p.def.propellant > 0 && p.propellant > 0.08 * p.def.propellant) || (p.def.mono > 0 && p.mono > 0.08 * p.def.mono) || (p.def.kind == PartKind.Solid && p.propellant > 0.05);

        /// <summary>Charring, pressure limits and pending chain explosions, every physics step.</summary>
        void UpdateDamage(double dt, double qk)
        {
            damageClock += dt;
            foreach (var p in parts)
            {
                // Heat leaves its mark: soot and scorching build up above ~650 K (faster on ablative shields)
                double hot = (p.temp - 650) / Math.Max(1, p.def.maxTemp - 650);
                if (hot > 0) p.charred = Mathf.Min(1f, p.charred + (float)(dt * hot * (p.def.kind == PartKind.HeatShield ? 0.25 : 0.09)));
            }
            // Too much air pressure on an exposed part, for more than half a second, crushes it
            if (qk > 120)
                for (int i = parts.Count - 1; i >= 0 && !destroyed; i--)
                {
                    var p = parts[i];
                    float limit = PressureLimit(p.def);
                    if (p.enclosed || qk <= limit) { p.overPressure = 0; continue; }
                    p.overPressure += (float)dt;
                    if (p.overPressure < 0.5f) continue;
                    if (HoldsFuel(p)) Explode(p, $"{p.def.name} burst in the airflow ({qk:0} kPa, it takes {limit:0} kPa)");
                    else if (p.def.kind == PartKind.Command || p.parent < 0) Destroy($"{p.def.name} was crushed by the airflow ({qk:0} kPa, it takes {limit:0} kPa)");
                    else BreakOff(p, $"{p.def.name} was torn away by the airflow ({qk:0} kPa, it takes {limit:0} kPa)");
                    return;
                }
            else foreach (var p in parts) p.overPressure = 0;

            // Chain reactions: tanks the last blast set off
            for (int k = fuses.Count - 1; k >= 0 && !destroyed; k--)
            {
                if (fuses[k].at > damageClock) continue;
                var p = Find(fuses[k].index);
                fuses.RemoveAt(k);
                if (p != null) Explode(p, $"{p.def.name} exploded in the chain reaction");
            }
        }

        /// <summary>
        /// A part blows up: its neighbours take the heat (fuel-filled ones go off a moment later), anything hanging
        /// off it is thrown clear as debris, and if it held up the whole vessel (its root or command part), that's the end.
        /// </summary>
        public void Explode(PartState p, string why)
        {
            if (destroyed || !parts.Contains(p)) return;
            explosions.Add(p.Center);
            lastEvent = why;
            // The blast: heat the neighbours; full tanks next door catch
            var near = new List<PartState>();
            var parent = p.parent >= 0 ? Find(p.parent) : null;
            if (parent != null) near.Add(parent);
            if (topo != null && p.index < topo.children.Length) foreach (int c in topo.children[p.index]) { var cp = Find(c); if (cp != null) near.Add(cp); }
            var rng = new System.Random(p.index * 7919 + parts.Count);
            foreach (var n in near)
            {
                n.temp += 380;
                n.charred = Mathf.Min(1f, n.charred + 0.35f);
                if (HoldsFuel(n) && rng.NextDouble() < 0.75 && !fuses.Exists(f => f.index == n.index))
                    fuses.Add((n.index, damageClock + 0.15 + 0.3 * rng.NextDouble()));
            }
            if (p.def.kind == PartKind.Command && parts.FindAll(x => x.def.kind == PartKind.Command).Count <= 1 || p.parent < 0 || Find(p.parent) == null)
            {
                Destroy(why);
                return;
            }
            // Throw off what hangs below/beside it, then lose the part itself
            if (topo != null && p.index < topo.children.Length)
                foreach (int c in new List<int>(topo.children[p.index]))
                {
                    var cp = Find(c);
                    if (cp != null) BreakOff(cp, why);
                }
            parts.Remove(p);
            RebuildTopology();
            RecomputeMass();
            lastEvent = why;
        }
    }
}
