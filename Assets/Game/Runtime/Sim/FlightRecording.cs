using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 28: a recording of a flight for the replay. Every tenth of a second it keeps where the ship was, which
    /// way it faced, which engines were firing and how hard, and how far its chutes and legs were out. Whenever the ship
    /// changes shape (a stage drops, a chute comes out, it docks or a different ship is flown) it keeps a copy of the
    /// whole ship, so the replay can show it as it was. Long flights thin out: past the limit every other sample goes.
    /// </summary>
    public class FlightRecording
    {
        public struct Frame
        {
            public double ut;
            public Vector3d pos;          // body-relative, inertial
            public Quaternion rot;
            public byte body;             // index in the system's list
            public short snap;            // which copy of the ship
            public uint fire;             // engines firing (bit k: the snapshot's k-th engine)
            public byte throttle, chute, legs;
            public bool destroyed;
        }

        public class Snap
        {
            public VesselSave save;
            public int[] engines;         // part indexes of its engines, in bit order
            public string name;
        }

        public const int MaxFrames = 20000;
        public readonly List<Frame> frames = new List<Frame>();
        public readonly List<Snap> snaps = new List<Snap>();
        double gap = 0.1, lastUt = double.NegativeInfinity;
        long lastSig;

        public int Count => frames.Count;
        public double StartUt => frames.Count > 0 ? frames[0].ut : 0;
        public double EndUt => frames.Count > 0 ? frames[frames.Count - 1].ut : 0;
        public bool Watchable => frames.Count >= 20;

        /// <summary>What the ship looks like, as a number: changes when its parts, deployed chutes or which ship it is change.</summary>
        public static long SignatureOf(VesselSim v) => Signature(v);
        static long Signature(VesselSim v)
        {
            if (v?.parts == null) return 0;   // a ship being taken apart (the end of a flight in a rocket room)
            long s = v.id * 1000003L + v.parts.Count * 7919L;
            foreach (var p in v.parts)
            {
                if (p?.def == null) continue;
                if (p.def.kind == PartKind.Parachute && p.chuteDeployed && !p.chuteFailed) s += 31L * (p.index + 1);
                if (p.dockedTo >= 0) s += 131L * (p.index + 1);
            }
            return s;
        }

        /// <summary>Called each frame of the flight: keeps a sample if a tenth of a second (more, after thinning) has gone by.</summary>
        public void Sample(VesselSim v, double ut, IList<CelestialBody> bodies, bool force = false)
        {
            if (v == null || v.body == null) return;
            if (!force && ut - lastUt < gap && !v.destroyed) return;
            if (frames.Count > 0 && frames[frames.Count - 1].destroyed && v.destroyed) return;   // the end is already kept
            lastUt = ut;
            long sig = Signature(v);
            if (snaps.Count == 0 || sig != lastSig)
            {
                lastSig = sig;
                var engines = new List<int>();
                foreach (var p in v.parts) if (p.def.IsEngine && engines.Count < 32) engines.Add(p.index);
                snaps.Add(new Snap { save = v.ToSave(), engines = engines.ToArray(), name = v.name });
            }
            var snap = snaps[snaps.Count - 1];
            uint fire = 0;
            float thr = 0, chute = 0, legs = 0;
            for (int k = 0; k < snap.engines.Length; k++)
            {
                var p = v.FindPart(snap.engines[k]);
                if (p != null && p.thrustFrac > 0.001f) { fire |= 1u << k; thr = Mathf.Max(thr, p.thrustFrac); }
            }
            foreach (var p in v.parts)
            {
                if (p.def.kind == PartKind.Parachute && p.chuteDeployed) chute = Mathf.Max(chute, p.chuteOpen);
                legs = Mathf.Max(legs, p.legDeploy);
            }
            int bi = bodies.IndexOf(v.body);
            frames.Add(new Frame
            {
                ut = ut, pos = v.pos, rot = v.rot, body = (byte)Math.Max(0, bi), snap = (short)(snaps.Count - 1), fire = fire,
                throttle = (byte)(Mathf.Clamp01(thr) * 255), chute = (byte)(Mathf.Clamp01(chute) * 255), legs = (byte)(Mathf.Clamp01(legs) * 255), destroyed = v.destroyed,
            });
            if (frames.Count > MaxFrames) Thin();
        }

        /// <summary>Drops every other sample (keeping the first and last, and each one where the ship changed), and samples half as often.</summary>
        void Thin()
        {
            var kept = new List<Frame>(frames.Count / 2 + 2);
            for (int i = 0; i < frames.Count; i++)
            {
                bool edge = i == 0 || i == frames.Count - 1 || frames[i].snap != frames[i - 1].snap || frames[i].body != frames[i - 1].body || frames[i].destroyed;
                if (edge || i % 2 == 0) kept.Add(frames[i]);
            }
            frames.Clear();
            frames.AddRange(kept);
            gap *= 2;
        }

        /// <summary>The sample at or just before a time (by searching), and how far it is to the next one.</summary>
        public int IndexAt(double ut)
        {
            int lo = 0, hi = frames.Count - 1;
            if (hi < 0) return -1;
            if (ut <= frames[0].ut) return 0;
            if (ut >= frames[hi].ut) return hi;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (frames[mid].ut <= ut) lo = mid; else hi = mid;
            }
            return lo;
        }

        /// <summary>Where the ship was at a time: between two samples it moves smoothly (unless it changed shape or world there).</summary>
        public Frame At(double ut)
        {
            int i = IndexAt(ut);
            var a = frames[i];
            if (i >= frames.Count - 1) return a;
            var b = frames[i + 1];
            if (a.snap != b.snap || a.body != b.body || b.ut <= a.ut) return a;
            float k = (float)((ut - a.ut) / (b.ut - a.ut));
            k = Mathf.Clamp01(k);
            var f = a;
            f.ut = ut;
            f.pos = Vector3d.Lerp(a.pos, b.pos, k);
            f.rot = Quaternion.Slerp(a.rot, b.rot, k);
            f.throttle = (byte)Mathf.Lerp(a.throttle, b.throttle, k);
            f.chute = (byte)Mathf.Lerp(a.chute, b.chute, k);
            f.legs = (byte)Mathf.Lerp(a.legs, b.legs, k);
            return f;
        }

        /// <summary>The ship's velocity around a time (body-relative, inertial), from the samples either side.</summary>
        public Vector3d VelocityAt(double ut)
        {
            int i = IndexAt(ut);
            if (frames.Count < 2) return Vector3d.zero;
            int j = Math.Min(frames.Count - 1, i + 1);
            if (j == i) { i = Math.Max(0, j - 1); }
            var a = frames[i]; var b = frames[j];
            if (b.ut <= a.ut || a.body != b.body) return Vector3d.zero;
            return (b.pos - a.pos) / (b.ut - a.ut);
        }
    }
}
