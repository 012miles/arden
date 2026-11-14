using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 35 (captaincarl24: "have more realistic crashes instead of a circle"): a rocket that's destroyed breaks
    /// apart. Every part flies off on its own (thrown outward by the blast, carrying the rocket's speed), tumbles, falls,
    /// bounces and skids to a stop on the ground, scorched black; the bigger pieces trail smoke. The fireball is a
    /// cluster of bursts going off one after another instead of one round flash. (Only how it looks: the game itself
    /// already counts the rocket as lost.)
    /// </summary>
    public partial class FlightMode
    {
        class Chunk
        {
            public GameObject go;
            public CelestialBody body;
            public Vector3d rel, vel;     // planet-fixed: position and velocity over the ground (the world turns under the camera too)
            public Vector3 spin;
            public float t, smokeT, size;
            public bool resting, smokes;
        }

        readonly List<Chunk> chunks = new List<Chunk>();
        static MaterialPropertyBlock scorchBlock;

        /// <summary>Breaks a vessel's model into flying, falling pieces (instead of hiding it).</summary>
        void Shatter(VesselSim v, float violence)
        {
            if (v == null || !views.TryGetValue(v, out var view) || view == null) return;
            Vector3 centre = view.root != null ? view.root.transform.TransformPoint(v.com) : Vector3.zero;
            var pieces = view.DetachParts();
            views.Remove(v);
            if (scorchBlock == null)
            {
                scorchBlock = new MaterialPropertyBlock();
                scorchBlock.SetColor("_Tint", new Color(0.82f, 0.78f, 0.74f));
                scorchBlock.SetFloat("_Char", 0.5f);   // scorched, but still recognisable
                scorchBlock.SetFloat("_Ambient", 0.24f);
            }
            var rng = new System.Random(v.id * 7919 + chunks.Count);
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            Vector3 up = (v.pos.normalized).ToFloat();
            // the biggest pieces smoke (not all of them: a big rocket has a lot of parts)
            var bySize = pieces.OrderByDescending(g => Size(g)).ToList();
            double alt0 = v.pos.magnitude - v.body.radius;
            bool air = v.body.HasAtmosphere && v.body.PressureAtm(alt0) > 0.02;
            for (int i = 0; i < Math.Min(4, bySize.Count) && air; i++) Smoke.Trail(bySize[i].transform, Mathf.Clamp(Size(bySize[i]) * 0.5f, 0.6f, 2.5f), 3.5f);   // thin trails from the big pieces
            for (int i = 0; i < bySize.Count; i++)
            {
                var go = bySize[i];
                Vector3 off = go.transform.position - centre;
                Vector3 dir = off.sqrMagnitude > 0.01f ? off.normalized : UnityEngine.Random.onUnitSphere;
                dir = (dir + UnityEngine.Random.insideUnitSphere * 0.6f + up * R(0.2f, 0.9f)).normalized;
                float speed = R(3f, 16f) * violence;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial == Mats.Lit) r.SetPropertyBlock(scorchBlock);
                foreach (var l in go.GetComponentsInChildren<Light>()) l.enabled = false;
                chunks.Add(new Chunk
                {
                    go = go, body = v.body, size = Size(go), smokes = false,
                    rel = v.body.InertialToFixed(v.pos + Vector3d.FromFloat(off), ut),
                    vel = v.body.InertialToFixed(v.SurfaceRelVelocity * (v.landed ? 0 : 0.35) + Vector3d.FromFloat(dir * speed), ut),
                    spin = new Vector3(R(-260f, 260f), R(-260f, 260f), R(-260f, 260f)) * Mathf.Clamp01(violence),
                });
            }
        }

        static float Size(GameObject g)
        {
            var mf = g.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.magnitude : 0.5f;
        }

        /// <summary>The pieces flying, falling, bouncing and coming to rest; smoke from the big ones for a few seconds.</summary>
        void UpdateChunks(float dt)
        {
            if (chunks.Count == 0 || active == null) return;
            dt = Mathf.Min(dt, 0.05f);
            for (int i = chunks.Count - 1; i >= 0; i--)
            {
                var c = chunks[i];
                c.t += dt;
                if (c.go == null || c.t > 90f || c.body != active.body) { if (c.go != null) UnityEngine.Object.Destroy(c.go); chunks.RemoveAt(i); continue; }
                var b = c.body;
                Vector3d up = c.rel.normalized;
                if (!c.resting)
                {
                    double r = c.rel.magnitude;
                    c.vel -= up * (b.mu / (r * r)) * dt;                                   // gravity
                    double alt = r - b.radius;
                    if (b.HasAtmosphere) c.vel *= 1.0 - Math.Min(0.9, 0.35 * b.Density(Math.Max(0, alt)) / Math.Max(1e-6, b.seaLevelDensity) * dt);   // air drag
                    c.rel += c.vel * dt;
                    double ground = b.radius + LaunchSite.SurfaceHeight(b, c.rel.normalized) + c.size * 0.2;
                    if (c.rel.magnitude < ground)
                    {
                        c.rel = c.rel.normalized * ground;
                        up = c.rel.normalized;
                        double vn = Vector3d.Dot(c.vel, up);
                        if (vn < 0) c.vel -= up * vn * 1.35;                                    // a bounce that keeps a third
                        c.vel *= 0.55;                                                          // and skids
                        c.spin *= 0.55f;
                        if (c.vel.magnitude < 0.7) { c.resting = true; c.vel = Vector3d.zero; }
                    }
                }
                Vector3 pos = (b.AbsolutePosition(renderUt) + b.FixedToInertial(c.rel, renderUt) - originAbs).ToFloat();
                c.go.transform.position = pos;
                if (!c.resting) c.go.transform.rotation = Quaternion.Euler(c.spin * dt) * c.go.transform.rotation;
                if (c.smokes && c.t < 4f)
                {
                    c.smokeT -= dt;
                    if (c.smokeT <= 0)
                    {
                        c.smokeT = 0.18f;
                        double alt = c.rel.magnitude - b.radius;   // (unused: trails now)
                        Smoke.Burst(pos, Mathf.Clamp(c.size * 0.7f, 0.8f, 4f), b.HasAtmosphere && b.PressureAtm(alt) > 0.02);
                    }
                }
            }
        }

        /// <summary>For tours: how many pieces, and where the first is from the camera.</summary>
        public string ChunkInfo()
        {
            if (chunks.Count == 0) return "no pieces";
            var c = chunks[0];
            var cam = root.farCam.transform.position;
            return $"{chunks.Count} pieces, {chunks.Count(x => x.resting)} resting; first at {(c.go != null ? (c.go.transform.position - cam).magnitude : -1):0.0} m from the camera, active={c.go?.activeInHierarchy}, height above ground {c.rel.magnitude - c.body.radius - LaunchSite.SurfaceHeight(c.body, c.rel.normalized):0.0} m";
        }

        void ClearChunks()
        {
            foreach (var c in chunks) if (c.go != null) UnityEngine.Object.Destroy(c.go);
            chunks.Clear();
        }
    }
}
