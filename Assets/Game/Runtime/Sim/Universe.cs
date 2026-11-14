using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rocket
{
    [Serializable]
    class UniverseSave
    {
        public int version = 1;
        public double ut;
        public int nextId = 1;
        public List<VesselSave> vessels = new List<VesselSave>();
        public CareerSave career = new CareerSave();
        public List<SurveySave> survey = new List<SurveySave>();
        public double nextWandererUt;
        public int wanderersSeen;
        public List<BaseLink> links = new List<BaseLink>();
    }

    [Serializable]
    public class QuickSaveFile
    {
        public int activeId;
        public double ut;
        public string vesselName;
        public string universe;
    }

    /// <summary>
    /// Everything that persists between flights: universe time and every vessel still in existence (crewed
    /// ships, probes, spent stages left in orbit, landers). Saved as JSON in the game's data folder.
    /// </summary>
    public class Universe
    {
        public double ut;
        public int nextId = 1;
        public readonly List<VesselSim> vessels = new List<VesselSim>();
        public readonly Career career = new Career();
        public readonly OreSurvey survey = new OreSurvey();
        /// <summary>Ground cables between landed modules (milestone 22): see Bases.</summary>
        public readonly List<BaseLink> links = new List<BaseLink>();
        /// <summary>When the next wanderer may arrive, and how many have come so far (for their names).</summary>
        public double nextWandererUt;
        public int wanderersSeen;
        /// <summary>Off for the autotests that don't want rocks drifting through their screenshots.</summary>
        public bool wanderersOn = true;

        public static bool IsAutotest => Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest") >= 0;
        /// <summary>The current game's save (a save slot), or the old single save when no slot is picked (autotests).</summary>
        public static string SavePath => SaveSlots.Current != null ? SaveSlots.UniversePath(SaveSlots.Current)
            : Path.Combine(Application.persistentDataPath, IsAutotest ? "autotest_universe.json" : "universe.json");

        public VesselSim Add(VesselSim v)
        {
            if (v.id <= 0) v.id = nextId++;
            v.survey = survey;
            v.career = career;
            if (!vessels.Contains(v)) vessels.Add(v);
            return v;
        }

        public void Remove(VesselSim v) => vessels.Remove(v);

        public VesselSim Find(int id) => vessels.Find(v => v.id == id);

        public string ToJson()
        {
            var s = new UniverseSave { ut = ut, nextId = nextId, career = career.ToSave(), survey = survey.ToSave(), nextWandererUt = nextWandererUt, wanderersSeen = wanderersSeen };
            foreach (var v in vessels) if (!v.destroyed) s.vessels.Add(v.ToSave());
            foreach (var l in links) s.links.Add(new BaseLink { a = l.a, b = l.b });
            return JsonUtility.ToJson(s);
        }

        /// <summary>Replaces this universe's contents with a saved one (vessels whose parts are gone are dropped).</summary>
        public void LoadJson(string json, IList<CelestialBody> bodies)
        {
            var s = JsonUtility.FromJson<UniverseSave>(json);
            vessels.Clear();
            ut = s?.ut ?? 0;
            nextId = Math.Max(1, s?.nextId ?? 1);
            career.Load(s?.career);
            survey.Load(s?.survey);
            nextWandererUt = s?.nextWandererUt ?? 0;
            wanderersSeen = s?.wanderersSeen ?? 0;
            links.Clear();
            if (s?.links != null) foreach (var l in s.links) links.Add(new BaseLink { a = l.a, b = l.b });
            if (s?.vessels == null) return;
            foreach (var vs in s.vessels)
            {
                var v = VesselSim.FromSave(vs, bodies, ut);
                if (v == null) continue;
                v.survey = survey;
                v.career = career;
                vessels.Add(v);
                nextId = Math.Max(nextId, v.id + 1);
            }
        }

        /// <summary>A quicksave: this universe plus which vessel was being flown.</summary>
        public string ToQuickSave(VesselSim active) => JsonUtility.ToJson(new QuickSaveFile
        {
            activeId = active != null ? active.id : -1, ut = ut, vesselName = active != null ? active.name : "", universe = ToJson(),
        });

        /// <summary>Loads a quicksave into this universe. Returns the vessel to fly (or null when the file is no good).</summary>
        public VesselSim LoadQuickSave(string json, IList<CelestialBody> bodies)
        {
            QuickSaveFile q = null;
            try { q = JsonUtility.FromJson<QuickSaveFile>(json); } catch (Exception e) { Debug.LogWarning("Bad quicksave: " + e.Message); }
            if (q == null || string.IsNullOrEmpty(q.universe)) return null;
            LoadJson(q.universe, bodies);
            return Find(q.activeId) ?? vessels.Find(x => x.IsControllable && !x.destroyed);
        }

        public void Save()
        {
            if (!IsAutotest && SaveSlots.Current == null) return;   // no game picked yet (the title screen)
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(SavePath, ToJson());
            }
            catch (Exception e) { Debug.LogWarning("Couldn't save the universe: " + e.Message); }
        }

        public static Universe Load(IList<CelestialBody> bodies)
        {
            var u = new Universe();
            try
            {
                if ((!IsAutotest || SaveSlots.Current != null) && File.Exists(SavePath)) u.LoadJson(File.ReadAllText(SavePath), bodies);
            }
            catch (Exception e) { Debug.LogWarning("Couldn't load the universe: " + e.Message); }
            return u;
        }

        /// <summary>Puts the stranded capsule of each newly accepted rescue contract into its orbit.</summary>
        public void SpawnRescues(IList<CelestialBody> bodies)
        {
            var home = bodies[0];
            foreach (var c in new List<Contract>(career.PendingRescues))
            {
                string first = c.crewName.Split(' ')[0];
                var d = VesselDesign.Stack($"{first}'s capsule", "port.sm", "pod.wren");
                DesignOps.AttachSurfaceAt(d, d.nodes.FindIndex(n => n.id == "pod.wren"), 0.5f, 0f, "sup.s", 1, false);
                double r = home.radius + c.altitude, sp = Math.Sqrt(home.mu / r), ph = (c.id * 2.399) % (2 * Math.PI);
                var pos = new Vector3d(r * Math.Cos(ph), 0, -r * Math.Sin(ph));
                var vel = new Vector3d(-sp * Math.Sin(ph), 0, -sp * Math.Cos(ph));
                var s = VesselSim.CreateFree(d, home, pos, vel, Quaternion.LookRotation(pos.normalized.ToFloat(), vel.normalized.ToFloat()));
                foreach (var p in s.parts) p.crew.Clear();
                s.parts.Find(p => p.def.id == "pod.wren").crew.Add(c.crewName);
                s.hasLaunched = true;
                s.throttle = 0;
                s.RecomputeMass();
                Add(s);
                c.vesselId = s.id;
            }
        }

        /// <summary>
        /// Wanderers come and go: one that has left Arden's sphere of influence (and isn't held by anyone) is gone for good,
        /// and while fewer than two are about, a new one arrives every couple of days. Returns news for the player.
        /// </summary>
        public List<string> TendWanderers(IList<CelestialBody> bodies)
        {
            var news = new List<string>();
            var home = bodies[0];
            foreach (var v in vessels.ToArray())
                if (v.IsRock && !v.destroyed && v.body.isStar) { vessels.Remove(v); news.Add($"{v.name} has left Arden's space."); }
            var live = new List<string>();
            foreach (var v in vessels) if (!v.destroyed) live.AddRange(v.HeldRockNames());
            career.liveWanderers.Clear();
            career.liveWanderers.AddRange(live);
            int free = 0;
            foreach (var v in vessels) if (v.IsRock && !v.destroyed) free++;
            if (wanderersOn && free < Wanderers.MaxAtOnce && ut >= nextWandererUt)
            {
                var rng = new System.Random(career.seed * 31 + wanderersSeen * 7907 + 17);
                var w = Wanderers.Spawn(home, ut, rng, ++wanderersSeen);
                Add(w);
                career.liveWanderers.Add(w.name);
                nextWandererUt = ut + Wanderers.Interval * (0.75 + 0.5 * rng.NextDouble());
                if (wanderersSeen > 1) news.Add($"New wanderer spotted: {w.name}, {w.parts[0].def.dryMass:0} t, coming in from {Ui0(w.pos.magnitude - home.radius)} up.");
            }
            return news;
        }

        static string Ui0(double m) => m >= 1e6 ? $"{m / 1e6:0} Mm" : $"{m / 1000:0} km";

        /// <summary>Empties the universe and starts a new game in the given mode.</summary>
        public void Reset(GameMode mode)
        {
            vessels.Clear();
            ut = 0;
            nextId = 1;
            nextWandererUt = 0;
            wanderersSeen = 0;
            survey.Clear();
            career.Start(mode);
        }
    }
}
