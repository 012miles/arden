using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rocket
{
    /// <summary>Where the ore is, and the constants for drilling, refining and machine heat.</summary>
    public static class Ore
    {
        public const double AmbientTemp = 300, NominalDelta = 300;   // machine cooling is rated at 300 K above ambient
        public const double HotStart = 700, HotStop = 1000;          // machines slow down above 700 K and stop at 1000 K
        public const double ReferenceConc = 0.10;                    // drill rates are quoted at 10% ore
        public const double FuelYield = 0.8, MonoYield = 0.6;        // tonnes of product per tonne of ore
        public const double HeatCapPerTonne = 3e5;                   // J/K per tonne of machinery
        public const double FoldedRadiator = 0.2;                    // a folded radiator panel still sheds a fifth

        /// <summary>
        /// Ore concentration (0..~0.25) at a planet-fixed direction. Smooth deposits from noise; none under Arden's seas;
        /// Tal's dark plains (the "seas" you can see on it) are richer.
        /// </summary>
        public static double Concentration(CelestialBody b, Vector3d fixedDir)
        {
            Vector3 d = fixedDir.normalized.ToFloat();
            float n = Noise.Fbm(d * 3.1f, 4, b.seed + 901);
            float x = Mathf.InverseLerp(0.47f, 0.70f, n);
            if (x <= 0) return 0;
            double c = x * x;
            if (b.style == BodyStyle.Earthlike)
            {
                float h = Terrain.ArdenField(b, d);                        // the same field the planet is painted with
                if (h < 0.025f) return 0;                                  // seas and beaches
                return c * 0.10;
            }
            float maria = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.47f, 0.40f, Noise.Fbm(d * 1.3f, 3, b.seed + 5)));
            return c * 0.16 * (0.55 + 0.9 * maria);
        }

        /// <summary>Machines run at full speed up to 700 K, slowing to a stop at 1000 K.</summary>
        public static double HeatEfficiency(double t) => t <= HotStart ? 1 : t >= HotStop ? 0 : (HotStop - t) / (HotStop - HotStart);

        /// <summary>Survey scanners work above the air (or 4% of the radius on airless worlds) and up to three radii.</summary>
        public static void ScanBand(CelestialBody b, out double minAlt, out double maxAlt)
        {
            minAlt = b.HasAtmosphere ? b.atmosphereHeight : 0.04 * b.radius;
            maxAlt = 3 * b.radius;
        }

        /// <summary>Half-width of the mapped swath (deg): a third of the way to the horizon, up to the scanner's limit.</summary>
        public static double ScanHalfAngle(CelestialBody b, double alt, double maxDeg)
        {
            double horizon = Math.Acos(b.radius / (b.radius + Math.Max(0, alt))) * 180 / Math.PI;
            return Math.Max(2, Math.Min(maxDeg, horizon / 3));
        }

        public static string Pct(double c) => $"{c * 100:0.0}%";
        public static string Km(double m) => m >= 1e6 ? $"{m / 1e6:0.##} Mm" : $"{m / 1000:0} km";
    }

    [Serializable]
    public class SurveySave
    {
        public string body, bits;
    }

    /// <summary>Which parts of each world have been mapped by survey scanners: a 5-degree latitude/longitude grid.</summary>
    public class OreSurvey
    {
        public const int Lon = 72, Lat = 36;
        const double Cell = 5.0;
        readonly Dictionary<string, bool[]> cells = new Dictionary<string, bool[]>();
        public int version;   // bumps whenever new ground is mapped (the map overlay redraws)
        static Vector3d[] centers;

        static Vector3d Center(int i, int j) => CelestialBody.DirectionFromLatLon(-90 + (j + 0.5) * Cell, -180 + (i + 0.5) * Cell);

        static void EnsureCenters()
        {
            if (centers != null) return;
            centers = new Vector3d[Lon * Lat];
            for (int j = 0; j < Lat; j++) for (int i = 0; i < Lon; i++) centers[j * Lon + i] = Center(i, j);
        }

        public static void LatLon(Vector3d fixedDir, out double latDeg, out double lonDeg)
        {
            var d = fixedDir.normalized;
            latDeg = Math.Asin(Math.Max(-1, Math.Min(1, d.y))) * 180 / Math.PI;
            lonDeg = Math.Atan2(-d.z, d.x) * 180 / Math.PI;
        }

        public static int CellOf(Vector3d fixedDir)
        {
            LatLon(fixedDir, out double lat, out double lon);
            int j = Math.Max(0, Math.Min(Lat - 1, (int)Math.Floor((lat + 90) / Cell)));
            int i = ((int)Math.Floor((lon + 180) / Cell) % Lon + Lon) % Lon;
            return j * Lon + i;
        }

        bool[] Grid(CelestialBody b)
        {
            if (!cells.TryGetValue(b.name, out var g)) cells[b.name] = g = new bool[Lon * Lat];
            return g;
        }

        public bool IsScanned(CelestialBody b, Vector3d fixedDir) => cells.TryGetValue(b.name, out var g) && g[CellOf(fixedDir)];
        public bool CellScanned(CelestialBody b, int k) => cells.TryGetValue(b.name, out var g) && g[k];

        /// <summary>Maps every cell whose centre is within 'halfAngleDeg' of a planet-fixed direction (and the cell it's in).</summary>
        public void Mark(CelestialBody b, Vector3d fixedDir, double halfAngleDeg)
        {
            EnsureCenters();
            var g = Grid(b);
            var d = fixedDir.normalized;
            bool changed = false;
            int c0 = CellOf(d);
            if (!g[c0]) { g[c0] = true; changed = true; }
            LatLon(d, out double lat, out double lon);
            double cosMax = Math.Cos(halfAngleDeg * Math.PI / 180);
            int j0 = Math.Max(0, (int)Math.Floor((lat - halfAngleDeg + 90) / Cell) - 1), j1 = Math.Min(Lat - 1, (int)Math.Floor((lat + halfAngleDeg + 90) / Cell) + 1);
            for (int j = j0; j <= j1; j++)
            {
                double rowLat = -90 + (j + 0.5) * Cell;
                double cosLat = Math.Max(0.02, Math.Cos(rowLat * Math.PI / 180));
                double span = Math.Min(180, halfAngleDeg / cosLat + Cell);
                int i0 = (int)Math.Floor((lon - span + 180) / Cell), i1 = (int)Math.Floor((lon + span + 180) / Cell);
                if (i1 - i0 >= Lon) { i0 = 0; i1 = Lon - 1; }
                for (int ii = i0; ii <= i1; ii++)
                {
                    int i = (ii % Lon + Lon) % Lon, k = j * Lon + i;
                    if (g[k] || Vector3d.Dot(centers[k], d) < cosMax) continue;
                    g[k] = true;
                    changed = true;
                }
            }
            if (changed) version++;
        }

        /// <summary>Share of the world's surface that's been mapped (area-weighted).</summary>
        public double Coverage(CelestialBody b)
        {
            if (!cells.TryGetValue(b.name, out var g)) return 0;
            double have = 0, all = 0;
            for (int j = 0; j < Lat; j++)
            {
                double w = Math.Cos((-90 + (j + 0.5) * Cell) * Math.PI / 180);
                for (int i = 0; i < Lon; i++) { all += w; if (g[j * Lon + i]) have += w; }
            }
            return have / all;
        }

        public void Clear() { cells.Clear(); version++; }

        public List<SurveySave> ToSave()
        {
            var list = new List<SurveySave>();
            foreach (var kv in cells)
            {
                var sb = new StringBuilder(Lon * Lat / 4);
                for (int k = 0; k < kv.Value.Length; k += 4)
                {
                    int nib = 0;
                    for (int q = 0; q < 4; q++) if (k + q < kv.Value.Length && kv.Value[k + q]) nib |= 1 << q;
                    sb.Append("0123456789abcdef"[nib]);
                }
                list.Add(new SurveySave { body = kv.Key, bits = sb.ToString() });
            }
            return list;
        }

        public void Load(List<SurveySave> saves)
        {
            cells.Clear();
            version++;
            if (saves == null) return;
            foreach (var s in saves)
            {
                if (string.IsNullOrEmpty(s?.body) || s.bits == null) continue;
                var g = new bool[Lon * Lat];
                for (int c = 0; c < s.bits.Length; c++)
                {
                    int nib = Convert.ToInt32(s.bits[c].ToString(), 16);
                    for (int q = 0; q < 4; q++) { int k = c * 4 + q; if (k < g.Length) g[k] = (nib & (1 << q)) != 0; }
                }
                cells[s.body] = g;
            }
        }
    }

    /// <summary>
    /// Ship systems each step: electricity (solar, alternators, probe and wheel draw), and the mining kit — drills fill
    /// ore tanks, converters refine ore into fuel or monopropellant, survey scanners map the ground, and the machines'
    /// heat goes into a shared core that radiators cool. Everything runs the same loaded, unloaded and in time warp.
    /// </summary>
    public partial class VesselSim
    {
        public OreSurvey survey;                 // set by the universe: where scanners record what they map
        public double coreTemp = Ore.AmbientTemp;
        // Last step's summary, for the HUD
        public double oreRate, fuelRate, monoRate, heatMade, heatShed, coolCapacity, heatEff = 1, oreHere = -1, miningEc, engineerBonus;
        public string drillNote = "", scanNote = "";
        public readonly Dictionary<int, string> convNotes = new Dictionary<int, string>();
        double scanAccum;
        Vector3d oreCacheDir;
        double oreCacheValue = -1;
        readonly List<PartState> drillsOn = new List<PartState>(), convOn = new List<PartState>(), scanOn = new List<PartState>();

        public double OreStored { get { double s = 0; foreach (var p in parts) s += p.ore; return s; } }
        public double OreCapacity { get { double s = 0; foreach (var p in parts) s += p.def.oreCapacity; return s; } }
        public bool HasMiningKit => parts.Exists(p => p.def.oreCapacity > 0 || p.def.IsMachine || p.def.scanHalfAngle > 0 || p.def.coolRate > 0);

        /// <summary>Ore concentration under the vessel right now.</summary>
        public double OreUnder(double ut)
        {
            Vector3d dir = body.InertialToFixed(pos, ut).normalized;
            if (oreCacheValue >= 0 && Vector3d.Dot(dir, oreCacheDir) > 1 - 1e-12) return oreCacheValue;
            oreCacheDir = dir;
            oreCacheValue = Ore.Concentration(body, dir);
            return oreCacheValue;
        }

        /// <summary>How far above the ground (m) a drill's fully extended bit ends; negative = in the ground.</summary>
        public double DrillTipHeight(PartState p, double ut)
        {
            var d = p.def;
            Vector3 tipL = p.localPos + p.localRot * new Vector3(d.radialSize.x * 0.5f, -d.radialSize.y * 0.5f - d.drillReach, 0);
            Vector3d w = pos + Vector3d.Rotate(rot, Vector3d.FromFloat(tipL - com));
            double ground = body.radius + LaunchSite.SurfaceHeight(body, body.InertialToFixed(w.normalized, ut));
            return w.magnitude - ground;
        }

        /// <summary>Time-warp upkeep for the active vessel (it moves on rails without stepping).</summary>
        public void WarpSystems(double dt, double ut) { wheelUse = 0; UpdateSystems(dt, ut); RecomputeMass(); }

        void UpdateSystems(double dt, double t0)
        {
            if (dt <= 0) return;
            double tEnd = t0 + dt;
            UpdateSupplies(dt, tEnd);
            UpdateLab(dt);
            bool anyStore = false;
            foreach (var p in parts) if (p.def.ecCapacity > 0) { anyStore = true; break; }
            double prod = SolarOutput(tEnd), baseUse = wheelUse * WheelEcPerKnms;
            bool machines = false;
            foreach (var p in parts)
            {
                if (p.def.alternator > 0) prod += p.def.alternator * p.thrustFrac;
                if (p.def.ecIdle > 0) baseUse += p.def.ecIdle;
                if (p.lightOn) baseUse += LightEc;
                if (p.def.IsMachine || p.def.scanHalfAngle > 0) machines = true;
            }
            if (!machines)
            {
                coreTemp = Ore.AmbientTemp;
                miningEc = oreRate = fuelRate = monoRate = heatMade = heatShed = 0;
                ecRate = anyStore ? prod - baseUse : 0;
                if (anyStore) AddCharge(ecRate * dt);
                return;
            }

            // ---- which machines can work, before power and heat
            drillsOn.Clear(); convOn.Clear(); scanOn.Clear(); convNotes.Clear();
            drillNote = ""; scanNote = "";
            double conc = -1, oreRoom = 0, stored = 0;
            foreach (var p in parts) { stored += p.ore; oreRoom += Math.Max(0, p.def.oreCapacity - p.ore); }
            foreach (var p in parts)
            {
                var d = p.def;
                p.load = 0;
                if (d.drillRate > 0)
                {
                    if (p.legDeploy < 0.9f) { Note(ref drillNote, "Unfold the drills (Y)"); continue; }
                    if (!landed) { Note(ref drillNote, "Land to drill"); continue; }
                    double tip = DrillTipHeight(p, tEnd);
                    if (tip > 0.5) { Note(ref drillNote, $"Drill can't reach the ground (bit ends {tip:0.0} m up)"); continue; }
                    if (conc < 0) conc = OreUnder(tEnd);
                    if (conc < 1e-4) { Note(ref drillNote, "No ore here"); continue; }
                    if (oreRoom <= 1e-9) { Note(ref drillNote, d.oreCapacity > 0 || OreCapacity > 0 ? "Ore tanks full" : "No ore tank to fill"); continue; }
                    drillsOn.Add(p);
                }
                else if (d.convertRate > 0)
                {
                    if (p.convMode == 0) { convNotes[p.index] = "Off"; continue; }
                    double room = p.convMode == 1 ? FuelRoom() : MonoRoom();
                    if (room <= 1e-9) { convNotes[p.index] = p.convMode == 1 ? "Fuel tanks full" : "Monoprop tanks full"; continue; }
                    if (stored <= 1e-9 && drillsOn.Count == 0 && !AnyDrillCouldRun()) { convNotes[p.index] = "No ore aboard"; continue; }
                    convOn.Add(p);
                }
                else if (d.scanHalfAngle > 0)
                {
                    Ore.ScanBand(body, out double lo, out double hi);
                    double alt = Altitude;
                    if (p.legDeploy < 0.9f) Note(ref scanNote, "Unfold the scanner (Y)");
                    else if (landed || alt < lo) Note(ref scanNote, $"Too low to map: works from {Ore.Km(lo)} up");
                    else if (alt > hi) Note(ref scanNote, $"Too high to map: works up to {Ore.Km(hi)}");
                    else if (survey == null) Note(ref scanNote, "Nothing to record the map");
                    else scanOn.Add(p);
                }
            }

            // ---- power: machines share whatever the batteries and panels can give
            double demand = 0;
            foreach (var p in drillsOn) demand += p.def.machineEc;
            foreach (var p in convOn) demand += p.def.machineEc;
            foreach (var p in scanOn) demand += p.def.machineEc;
            double avail = EcStored + (prod - baseUse) * dt;
            double powerFrac = demand > 0 ? Math.Max(0, Math.Min(1, avail / (demand * dt))) : 1;

            // ---- heat: machines slow down when the core runs hot
            double cap = 0, k = 0, qMax = 0; // J/K, W/K, W
            foreach (var p in parts)
            {
                var d = p.def;
                if (d.IsMachine) { cap += d.dryMass * Ore.HeatCapPerTonne; k += d.passiveCool; }
                if (d.coolRate > 0) k += d.coolRate * (d.IsDeployable && p.legDeploy < 0.9f ? Ore.FoldedRadiator : 1);
            }
            coolCapacity = k;                                   // kW at 300 K over ambient
            k = Math.Max(1e-3, k * 1000 / Ore.NominalDelta);     // W/K
            cap = Math.Max(1e5, cap);
            foreach (var p in drillsOn) qMax += p.def.heatOut * 1000 * powerFrac;
            foreach (var p in convOn) qMax += p.def.heatOut * 1000 * powerFrac;
            double tau = cap / k;
            bool settled = dt > 2 * tau;
            double tUse = settled ? SteadyCoreTemp(qMax, k) : coreTemp;
            double eff = Ore.HeatEfficiency(tUse);

            // ---- drilling (an engineer aboard gets more out of the same power and heat)
            double ecUsed = 0, heat = 0, drilled = 0, fuelMade = 0, monoMade = 0;
            engineerBonus = drillsOn.Count + convOn.Count > 0 ? Crew.MiningBonus(BestLevel(CrewRole.Engineer)) : 0;
            double boost = 1 + engineerBonus;
            if (drillsOn.Count > 0)
            {
                double want = 0;
                foreach (var p in drillsOn) want += p.def.drillRate * (conc / Ore.ReferenceConc) * dt * powerFrac * eff * boost;
                drilled = Math.Min(want, oreRoom);
                double frac = want > 0 ? drilled / want : 0;
                foreach (var p in drillsOn)
                {
                    p.load = (float)(powerFrac * eff * frac);
                    ecUsed += p.def.machineEc * powerFrac * frac;
                    heat += p.def.heatOut * 1000 * powerFrac * eff * frac;
                }
                AddOre(drilled);
                drillNote = eff < 0.999 && eff > 0 ? $"Drilling, slowed by heat ({eff * 100:0}%)" : eff <= 0 ? "Too hot to drill: add radiators" :
                    powerFrac < 0.999 ? $"Drilling, short of power ({powerFrac * 100:0}%)" : frac < 0.999 ? "Ore tanks full" : "Drilling";
            }

            // ---- refining
            foreach (var p in convOn)
            {
                double want = p.def.convertRate * dt * powerFrac * eff * boost;
                bool fuel = p.convMode == 1;
                double yield = fuel ? Ore.FuelYield : Ore.MonoYield;
                double room = fuel ? FuelRoom() : MonoRoom();
                double use = Math.Min(want, Math.Min(OreStored, room / yield));
                double frac = want > 0 ? Math.Max(0, use / want) : 0;
                if (use > 0)
                {
                    TakeOre(use);
                    if (fuel) { AddFuel(use * yield); fuelMade += use * yield; }
                    else { AddMono(use * yield); monoMade += use * yield; }
                }
                p.load = (float)(powerFrac * eff * frac);
                ecUsed += p.def.machineEc * powerFrac * frac;
                heat += p.def.heatOut * 1000 * powerFrac * eff * frac;
                string what = fuel ? "fuel" : "monoprop";
                convNotes[p.index] = eff <= 0 ? "Too hot: add radiators" : powerFrac <= 1e-6 ? "No power" :
                    frac < 0.02 ? (OreStored <= 1e-9 ? "Waiting for ore" : $"{(fuel ? "Fuel" : "Monoprop")} tanks full") :
                    eff < 0.999 ? $"Making {what}, slowed by heat ({eff * 100:0}%)" : powerFrac < 0.999 ? $"Making {what}, short of power ({powerFrac * 100:0}%)" : $"Making {what}";
            }

            // ---- survey scanners
            if (scanOn.Count > 0)
            {
                foreach (var p in scanOn) { p.load = (float)powerFrac; ecUsed += p.def.machineEc * powerFrac; }
                if (powerFrac > 0.5) { Scan(dt, t0); scanNote = $"Mapping {body.name}: {survey.Coverage(body) * 100:0}% done"; }
                else scanNote = "Scanner short of power";
            }

            // ---- electricity
            miningEc = ecUsed;
            ecRate = prod - baseUse - ecUsed;
            if (anyStore) AddCharge(ecRate * dt);
            else ecRate = 0;

            // ---- core temperature (exact for constant heat; settled when warping past several time constants)
            double tEq = Ore.AmbientTemp + heat / k;
            coreTemp = settled ? tEq : tEq + (coreTemp - tEq) * Math.Exp(-dt / tau);
            heatMade = heat / 1000;
            heatShed = k * (coreTemp - Ore.AmbientTemp) / 1000;
            heatEff = eff;
            oreRate = drilled / dt;
            fuelRate = fuelMade / dt;
            monoRate = monoMade / dt;
            if (drilled > 0) careerFlags |= VFlags.Mined;
            if (fuelMade + monoMade > 0) careerFlags |= VFlags.Refined;
        }

        static void Note(ref string note, string text) { if (string.IsNullOrEmpty(note)) note = text; }

        bool AnyDrillCouldRun()
        {
            if (!landed) return false;
            foreach (var p in parts) if (p.def.drillRate > 0 && p.legDeploy >= 0.9f) return true;
            return false;
        }

        /// <summary>Core temperature where the heat made (slowed by running hot) equals what the radiators shed.</summary>
        static double SteadyCoreTemp(double qMax, double k)
        {
            double lo = Ore.AmbientTemp, hi = Math.Max(Ore.HotStop, Ore.AmbientTemp + qMax / k);
            for (int i = 0; i < 50; i++)
            {
                double mid = (lo + hi) / 2;
                if (mid - Ore.AmbientTemp - qMax * Ore.HeatEfficiency(mid) / k > 0) hi = mid; else lo = mid;
            }
            return (lo + hi) / 2;
        }

        /// <summary>Marks the ground under the vessel: along its orbit when coasting through a long step, else every 2 s.</summary>
        void Scan(double dt, double t0)
        {
            double maxDeg = 0;
            foreach (var p in scanOn) maxDeg = Math.Max(maxDeg, p.def.scanHalfAngle);
            Ore.ScanBand(body, out double lo, out double hi);
            if (rails != null && dt > 2)
            {
                double r = Math.Max(body.radius, pos.magnitude);
                double w = vel.magnitude / r + body.AngularVelocity;
                double half = Ore.ScanHalfAngle(body, r - body.radius, maxDeg) * Math.PI / 180;
                double step = Math.Max(1, half * 0.7 / Math.Max(1e-6, w));
                int n = (int)Math.Min(4000, Math.Ceiling(dt / step));
                for (int i = 0; i <= n; i++)
                {
                    double t = t0 + dt * i / Math.Max(1, n);
                    rails.StateAt(t, out var rp, out _);
                    double alt = rp.magnitude - body.radius;
                    if (alt < lo || alt > hi) continue;
                    survey.Mark(body, body.InertialToFixed(rp, t), Ore.ScanHalfAngle(body, alt, maxDeg));
                }
                scanAccum = 0;
                return;
            }
            scanAccum += dt;
            if (scanAccum < 2) return;
            scanAccum = 0;
            survey.Mark(body, body.InertialToFixed(pos, t0 + dt), Ore.ScanHalfAngle(body, Altitude, maxDeg));
        }

        // ---- moving ore, fuel and monopropellant around the vessel (any tank, regardless of decouplers)

        double FuelRoom() { double s = 0; foreach (var p in parts) if (p.def.kind == PartKind.Tank) s += Math.Max(0, p.def.propellant - p.propellant); return s; }
        double MonoRoom() { double s = 0; foreach (var p in parts) if (p.def.mono > 0) s += Math.Max(0, p.def.mono - p.mono); return s; }

        void AddOre(double t)
        {
            double room = 0;
            foreach (var p in parts) room += Math.Max(0, p.def.oreCapacity - p.ore);
            if (room <= 0 || t <= 0) return;
            double k = Math.Min(1, t / room);
            foreach (var p in parts) p.ore += Math.Max(0, p.def.oreCapacity - p.ore) * k;
        }

        void TakeOre(double t)
        {
            double have = OreStored;
            if (have <= 0 || t <= 0) return;
            double k = Math.Min(1, t / have);
            foreach (var p in parts) p.ore = k >= 1 ? 0 : p.ore * (1 - k);
        }

        void AddFuel(double t)
        {
            double room = FuelRoom();
            if (room <= 0 || t <= 0) return;
            double k = Math.Min(1, t / room);
            foreach (var p in parts) if (p.def.kind == PartKind.Tank) p.propellant += Math.Max(0, p.def.propellant - p.propellant) * k;
        }

        void AddMono(double t)
        {
            double room = MonoRoom();
            if (room <= 0 || t <= 0) return;
            double k = Math.Min(1, t / room);
            foreach (var p in parts) if (p.def.mono > 0) p.mono += Math.Max(0, p.def.mono - p.mono) * k;
        }

        /// <summary>Cycles a converter: off, fuel, monopropellant.</summary>
        public void SetConverter(PartState p, int mode) { if (p != null && p.def.convertRate > 0) p.convMode = Math.Max(0, Math.Min(2, mode)); }
    }
}
