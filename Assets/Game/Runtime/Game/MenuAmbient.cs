using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 42 (captaincarl24: "for the main menu screen make it animated like theres rockets taking off, moving cars,
    /// people, etc same thing for building selector"): the space centre behind the title screen and the building picker is
    /// busy. Every half minute or so a rocket lifts off from one of the other pads (steam from the deck, a flame, a column
    /// of exhaust, then the turn east toward orbit); a plane crosses the sky now and then; and around the main pad
    /// technicians walk their rounds while service trucks, tankers and a bus drive the roads.
    /// </summary>
    public partial class MenuMode
    {
        class AmbientLaunch { public VesselView view; public Transform nozzle, flame; public float t, lo, width; public Vector3 pos, east; }
        class Flyover { public GameObject go; public Vector3 a, b; public float t, dur; }

        readonly List<AmbientLaunch> launches = new List<AmbientLaunch>();
        readonly List<Flyover> flyovers = new List<Flyover>();
        float nextLaunch = 4f, nextFlyover = 10f;
        Crowd padCrowd;
        Transform padCrowdSite;
        static Mesh flameMesh;

        /// <summary>For tours: launch one now.</summary>
        public void TestLaunchNow() => nextLaunch = 0f;
        public int AmbientLaunches => launches.Count;

        /// <summary>Every frame on the title screen and the building picker.</summary>
        void UpdateAmbient(BodyView site, Camera cam, float dt)
        {
            var st = site?.SiteTransform;
            if (st == null) return;
            Smoke.SetAnchor(st.position, st.rotation);   // (smoke stays put on the ground as the world turns)
            EnsurePadCrowd(site);
            padCrowd?.SetVisible(true);
            padCrowd?.Update(dt);
            nextLaunch -= dt;
            if (nextLaunch <= 0f) { nextLaunch = Random.Range(28f, 50f); StartLaunch(site, cam); }
            for (int i = launches.Count - 1; i >= 0; i--) if (!StepLaunch(launches[i], st, dt)) { DropLaunch(launches[i]); launches.RemoveAt(i); }
            nextFlyover -= dt;
            if (nextFlyover <= 0f) { nextFlyover = Random.Range(35f, 60f); StartFlyover(site); }
            for (int i = flyovers.Count - 1; i >= 0; i--)
            {
                var f = flyovers[i];
                f.t += dt;
                if (f.go == null || f.t > f.dur) { if (f.go != null) Object.Destroy(f.go); flyovers.RemoveAt(i); continue; }
                f.go.transform.localPosition = Vector3.Lerp(f.a, f.b, f.t / f.dur);
            }
        }

        /// <summary>Off the title screen: the launches and planes go, the pad's crowd stops showing.</summary>
        void ClearAmbient()
        {
            foreach (var l in launches) DropLaunch(l);
            launches.Clear();
            foreach (var f in flyovers) if (f.go != null) Object.Destroy(f.go);
            flyovers.Clear();
            padCrowd?.SetVisible(false);
            nextLaunch = 4f;
        }

        static Vector3 Local(BodyView site, double e, double n, float h) => site.SiteTransform.InverseTransformPoint(site.SiteWorld(e, n, h));

        // ---------------------------------------------------------------- the pad's people and traffic

        void EnsurePadCrowd(BodyView site)
        {
            if (padCrowd != null && padCrowdSite == site.SiteTransform) return;
            padCrowdSite = site.SiteTransform;
            padCrowd = new Crowd(site.SiteTransform, 4242) { Role = Ardlings.RoleWork };
            Vector3 P(double e, double n) => Local(site, e, n, 0);
            // technicians round the pad's apron, and a couple checking the tower
            for (int k = 0; k < 6; k++)
            {
                double a0 = k * System.Math.PI / 3, r = 26 + 6 * (k % 3);
                var loop = new Vector3[6];
                for (int i = 0; i < 6; i++) { double a = a0 + i * 0.35; loop[i] = P(System.Math.Cos(a) * r, System.Math.Sin(a) * r); }
                padCrowd.AddWalker(loop, 1.1f + 0.1f * k);
            }
            padCrowd.AddWalker(new[] { P(14, 6), P(18, 10), P(14, 14), P(10, 10) }, 0.9f);
            padCrowd.AddWalker(new[] { P(-16, -8), P(-8, -16), P(-2, -12), P(-10, -4) }, 1f);
            // service traffic: round the pad's ring road, and out to the assembly building and back
            var ring = new Vector3[12];
            for (int i = 0; i < 12; i++) { double a = i * System.Math.PI / 6; ring[i] = P(System.Math.Cos(a) * 78, System.Math.Sin(a) * 78); }
            padCrowd.AddVehicle("van", ring, 7f);
            padCrowd.AddVehicle("tanker", ring.Skip(6).Concat(ring.Take(6)).ToArray(), 5.5f);
            padCrowd.AddVehicle("truck", ring.Skip(3).Concat(ring.Take(3)).ToArray(), 6.5f);
            padCrowd.AddVehicle("bus", new[] { P(10, 80), P(10, 200), P(14, 290), P(4, 290), P(0, 200), P(0, 80) }, 8f);
            padCrowd.AddVehicle("van", new[] { P(-60, 70), P(-60, 260), P(-56, 270), P(-50, 260), P(-50, 70) }, 9f);
        }

        // ---------------------------------------------------------------- rockets lifting off

        void StartLaunch(BodyView site, Camera cam)
        {
            if (LaunchSite.Pads.Length < 2) return;
            // a pad you can see (not the main one: the last rocket built stands there), or any
            var pads = Enumerable.Range(1, LaunchSite.Pads.Length - 1).ToList();
            var seen = pads.Where(i =>
            {
                var vp = cam.WorldToViewportPoint(site.SiteWorld(LaunchSite.Pads[i].e, LaunchSite.Pads[i].n, 60));
                return vp.z > 0 && vp.x > 0.08f && vp.x < 0.92f && vp.y > 0.05f && vp.y < 0.95f;
            }).ToList();
            var st = site.SiteTransform;
            // none in sight (the title screen looks the other way): a launch from a distant pad off to the right, clear of the menu
            Vector3? far = null;
            // (the title screen's camera is low, and the buildings hide the other pads: always a distant one, in the open sky
            // between the tower and the assembly building)
            if (seen.Count == 0 || !centre)
            {
                var ray = cam.ViewportPointToRay(new Vector3(centre ? Random.Range(0.62f, 0.85f) : Random.Range(0.56f, 0.7f), 0.5f, 0));
                Vector3 camLocal = st.InverseTransformPoint(ray.origin);
                Vector3 h = st.InverseTransformDirection(Vector3.ProjectOnPlane(ray.direction, st.up)).normalized;
                Vector3 at = camLocal + h * (centre ? Random.Range(1700f, 2500f) : Random.Range(1300f, 1900f));
                at.y = 0f;
                far = at;
            }
            var from = seen.Count > 0 ? seen : pads;
            int pad = from[Random.Range(0, from.Count)];
            var designs = new[] { Presets.TwinBooster(), Presets.Orbiter(), Presets.ComsatLauncher(), Presets.BigLift(), Presets.QuadBooster(), Presets.SkyHopper() };
            var d = designs[Random.Range(0, designs.Length)];
            var view = VesselView.FromNodes(d.nodes, "Launch from " + LaunchSite.Pads[pad].name, false);
            var go = view.root;
            float lo = float.MaxValue, hi = float.MinValue, wide = 0.5f;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { lo = Mathf.Min(lo, r.bounds.min.y); hi = Mathf.Max(hi, r.bounds.max.y); wide = Mathf.Max(wide, r.bounds.extents.x); }
            if (lo > hi) { lo = 0; hi = 10; }
            go.transform.SetParent(st, false);
            var p = LaunchSite.Pads[pad];
            var l = new AmbientLaunch { view = view, lo = lo, width = wide };
            l.pos = (far ?? Local(site, p.e, p.n, (float)LaunchSite.PadHeight)) - Vector3.up * lo;
            l.east = (Local(site, p.e + 100, p.n, 0) - Local(site, p.e, p.n, 0)).normalized;
            go.transform.localPosition = l.pos;
            go.transform.localRotation = Quaternion.identity;
            // the flame and the exhaust, under the engines
            var nz = new GameObject("Nozzle").transform;
            nz.SetParent(go.transform, false);
            nz.localPosition = new Vector3(0, lo, 0);
            l.nozzle = nz;
            flameMesh ??= FlameMesh();
            var fg = new GameObject("Flame");
            fg.transform.SetParent(nz, false);
            fg.AddComponent<MeshFilter>().sharedMesh = flameMesh;
            var fr = fg.AddComponent<MeshRenderer>();
            fr.sharedMaterial = Mats.Additive;
            fr.shadowCastingMode = ShadowCastingMode.Off;
            fg.transform.localScale = new Vector3(wide * 0.9f, wide * 5f, wide * 0.9f);
            l.flame = fg.transform;
            Smoke.Exhaust(nz, Mathf.Clamp(wide * 2.2f, 2f, 9f) * (far.HasValue ? 1.8f : 1f), 50f);   // (thicker far off, so it reads)
            Smoke.Steam(far.HasValue ? st.TransformPoint(far.Value) : site.SiteWorld(p.e, p.n, (float)LaunchSite.PadHeight), st.up, Mathf.Clamp(wide * 6f, 8f, 26f), 6f);
            launches.Add(l);
        }

        /// <summary>Up it goes, faster and faster, leaning over to the east after a few seconds. False when it's gone.</summary>
        bool StepLaunch(AmbientLaunch l, Transform st, float dt)
        {
            if (l.view?.root == null) return false;
            l.t += dt;
            float speed = 7f * l.t;
            float tilt = Mathf.Min(65f, Mathf.Max(0f, l.t - 5f) * 3.2f);
            Vector3 axis = Vector3.Cross(Vector3.up, l.east).normalized;
            Vector3 dir = Quaternion.AngleAxis(tilt, axis) * Vector3.up;
            l.pos += dir * speed * dt;
            var tr = l.view.root.transform;
            tr.localPosition = l.pos;
            tr.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            if (l.flame != null) l.flame.localScale = new Vector3(l.width * 0.9f, l.width * (4.6f + 0.8f * Random.value), l.width * 0.9f);
            return l.t < 70f && l.pos.y < 45000f;
        }

        void DropLaunch(AmbientLaunch l)
        {
            if (l.nozzle != null) foreach (var ps in l.nozzle.GetComponentsInChildren<ParticleSystem>()) { ps.transform.SetParent(null, true); ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); Object.Destroy(ps.gameObject, 12f); }   // (the exhaust drifts away)
            l.view?.Destroy();
        }

        /// <summary>A flame: a bright cone pointing down, white-yellow at the nozzle, orange at the tip (unit size: scaled per rocket).</summary>
        static Mesh FlameMesh()
        {
            var b = new MeshBuilder();
            b.AddFrustum(0f, -0.35f, 0.42f, 0.36f, new Color(1f, 0.95f, 0.8f) * 1.4f, 16, false, false);
            b.AddFrustum(-0.35f, -1f, 0.36f, 0.02f, new Color(1f, 0.55f, 0.15f) * 1.2f, 16, false, false);
            return b.ToMesh("Title flame");
        }

        // ---------------------------------------------------------------- planes crossing the sky

        void StartFlyover(BodyView site)
        {
            var st = site.SiteTransform;
            var go = new GameObject("Plane overhead");
            go.transform.SetParent(st, false);
            go.AddComponent<MeshFilter>().sharedMesh = Crowd.VehicleMesh("plane", new Color(0.92f, 0.93f, 0.94f));
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.On;
            bool westToEast = Random.value < 0.5f;
            double n = Random.Range(350f, 900f), h = Random.Range(220f, 420f);
            Vector3 a = Local(site, westToEast ? -2600 : 2600, n, (float)h), b = Local(site, westToEast ? 2600 : -2600, n + Random.Range(-300f, 300f), (float)h);
            go.transform.localPosition = a;
            go.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up) * Quaternion.Euler(0, 0, westToEast ? -4f : 4f);
            flyovers.Add(new Flyover { go = go, a = a, b = b, dur = (b - a).magnitude / 75f });
        }
    }
}
