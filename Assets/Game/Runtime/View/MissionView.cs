using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// Milestone 29: the picture behind the mission design screens. The world the mission goes to (the real one, from the
    /// main game) seen from space, with the chosen orbit drawn round it and the spacecraft as a bright dot going round;
    /// and, close up in front, a model of the spacecraft as designed (frame, solar wings or nuclear battery, dish,
    /// instruments, engine), turning slowly. It rebuilds whenever the design changes.
    /// </summary>
    public class MissionView
    {
        readonly GameRoot root;
        CelestialBody body;
        double t0;
        float enterTime;
        LineRenderer orbit;
        GameObject marker, craft;
        string craftSig;
        float craftSize = 2f;

        public MissionView(GameRoot root) { this.root = root; }

        public void Enter()
        {
            enterTime = Time.unscaledTime;
            body = null;
        }

        public void Leave()
        {
            if (orbit != null) Object.Destroy(orbit.gameObject);
            if (marker != null) Object.Destroy(marker);
            if (craft != null) Object.Destroy(craft);
            orbit = null; marker = null; craft = null; craftSig = null;
            root.farCam.fieldOfView = root.nearCam.fieldOfView = 60f;
        }

        /// <summary>Each frame: the world, the orbit and the spacecraft. fly: 0..1 through a mission being flown, or -1.</summary>
        public void Update(MissionPlan plan, MSheet sheet, float fly)
        {
            var b = sheet.body;
            if (b != body)
            {
                body = b;
                // a time when the world is lit from the side as we see it
                t0 = 2.2e6;
            }
            float real = Time.unscaledTime - enterTime;
            double t = t0 + real * 60.0;
            Vector3d bAbs = b.AbsolutePosition(t);
            Vector3d sun = Sun.DirectionFrom(bAbs, t);
            double r = b.radius + sheet.altitudeKm * 1000.0;
            double dist = System.Math.Max(b.radius * 3.4, r * 2.1);
            // look from the sun's side, swung round 55 degrees and a little above, drifting slowly
            Vector3d up = new Vector3d(0, 1, 0);
            Vector3d side = Vector3d.Cross(up, sun).normalized;
            double ang = (55 + 12 * System.Math.Sin(real * 0.05)) * System.Math.PI / 180;
            Vector3d dir = (sun * System.Math.Cos(ang) + side * System.Math.Sin(ang)).normalized;
            dir = (dir + up * 0.32).normalized;
            Vector3d camAbs = bAbs + dir * dist;
            foreach (var bv in root.bodyViews) bv.Sync(camAbs, t, camAbs);
            var cam = root.farCam;
            Vector3 planet = (bAbs - camAbs).ToFloat();
            cam.transform.position = Vector3.zero;
            // the world sits left of centre, the spacecraft model on the right
            Vector3 fwd = planet.normalized;
            Vector3 camRight = Vector3.Cross(Vector3.up, fwd).normalized;
            cam.transform.rotation = Quaternion.LookRotation((fwd + camRight * 0.22f).normalized, Vector3.up);
            cam.nearClipPlane = 5f;
            cam.farClipPlane = 1e12f;
            cam.cullingMask = 1 << GameRoot.LayerFar;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = root.nearCam.fieldOfView = 60f;
            root.nearCam.enabled = true;
            GameRoot.SunNow = sun.ToFloat();
            root.sun.transform.rotation = Quaternion.LookRotation(-GameRoot.SunNow);
            RenderEnv.UpdateWorld(b, camAbs - bAbs, planet, GameRoot.SunNow, root.sun, true);
            RenderEnv.UpdateShine(b, camAbs, t, root.bodies);
            SkyView.SunTint = Color.white;
            SkyView.StarExposure = 1f;
            root.sky?.Sync(cam, 1f);
            Shader.SetGlobalVector("_RkFog", Vector4.zero);

            // the orbit, in the world's own frame
            Vector3d ex = b.FixedToInertial(new Vector3d(1, 0, 0), t), ez = b.FixedToInertial(new Vector3d(0, 0, 1), t);
            Vector3d u = ex, v = ez;
            switch (plan.orbit)
            {
                case MOrbit.Polar: v = up; break;
                case MOrbit.SunSync: u = up; v = Vector3d.Cross(up, sun).normalized; break;
                case MOrbit.High: v = (ez * 0.94 + up * 0.34).normalized; break;
                case MOrbit.Medium: v = (ez * 0.97 + up * 0.24).normalized; break;
            }
            if (orbit == null)
            {
                var go = new GameObject("Mission orbit") { layer = GameRoot.LayerFar };
                orbit = go.AddComponent<LineRenderer>();
                orbit.sharedMaterial = Mats.Unlit;
                orbit.positionCount = 129;
                orbit.useWorldSpace = true;
                orbit.shadowCastingMode = ShadowCastingMode.Off;
                orbit.receiveShadows = false;
                marker = new GameObject("Mission craft marker") { layer = GameRoot.LayerFar };
                var mb = new MeshBuilder();
                mb.AddSphere(Vector3.zero, 1f, new Color(1f, 0.85f, 0.4f), 12, 8);
                marker.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Marker");
                var mr = marker.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.Unlit;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            Color oc = sheet.CanLaunch ? (sheet.warnings.Count == 0 ? new Color(0.45f, 1f, 0.6f) : new Color(1f, 0.82f, 0.35f)) : new Color(1f, 0.45f, 0.4f);
            orbit.startColor = orbit.endColor = oc;
            orbit.widthMultiplier = (float)(b.radius * 0.008);
            for (int i = 0; i <= 128; i++)
            {
                double a = i / 128.0 * System.Math.PI * 2;
                orbit.SetPosition(i, planet + ((u * System.Math.Cos(a) + v * System.Math.Sin(a)) * r).ToFloat());
            }
            double around = fly >= 0 ? fly * System.Math.PI * 2 * 24 : real * 0.35;
            marker.transform.position = planet + ((u * System.Math.Cos(around) + v * System.Math.Sin(around)) * r).ToFloat();
            marker.transform.localScale = Vector3.one * (float)(b.radius * 0.03);

            // the spacecraft close up, in front of the camera to the right
            string sig = JsonUtility.ToJson(plan);
            if (sig != craftSig)
            {
                craftSig = sig;
                if (craft != null) Object.Destroy(craft);
                craft = BuildCraft(plan, out craftSize);
            }
            Transform ct = cam.transform;
            float scale = 3.6f / Mathf.Max(0.5f, craftSize);
            craft.transform.localScale = Vector3.one * scale;
            craft.transform.position = ct.position + ct.forward * 9f + ct.right * 2.7f - ct.up * 0.9f;
            craft.transform.rotation = Quaternion.LookRotation(ct.forward, ct.up) * Quaternion.Euler(18f, real * 14f, 0f);
        }

        /// <summary>A model of the designed spacecraft from simple shapes. Size: roughly how big it is across (m).</summary>
        public static GameObject BuildCraft(MissionPlan plan, out float size)
        {
            var mb = new MeshBuilder();
            var bus = MissionDesign.Part(plan.bus) ?? MissionDesign.Part("bus.box");
            float s = bus.id == "bus.cube" ? 0.35f : bus.id == "bus.big" ? 2.6f : 1.3f;
            size = s * 2.6f;
            var gold = new Color(0.85f, 0.66f, 0.28f);
            var grey = new Color(0.62f, 0.63f, 0.66f);
            var panel = new Color(0.08f, 0.12f, 0.26f);
            mb.Mat = MeshBuilder.MatFoil;
            mb.AddBox(Vector3.zero, new Vector3(s, s * 1.15f, s), gold, Quaternion.identity);
            mb.Mat = MeshBuilder.MatMetal;
            mb.AddBox(new Vector3(0, s * 0.6f, 0), new Vector3(s * 1.04f, s * 0.06f, s * 1.04f), grey, Quaternion.identity);
            // power
            var pw = MissionDesign.Part(plan.power);
            if (pw != null && pw.rtgW > 0)
            {
                mb.AddBox(new Vector3(-s * 0.95f, 0, 0), new Vector3(s * 0.9f, s * 0.04f, s * 0.04f), grey, Quaternion.identity);
                int st = mb.v.Count;
                mb.AddFrustum(s * 0.35f, -s * 0.35f, s * 0.16f, s * 0.16f, new Color(0.3f, 0.3f, 0.32f), 10, true, true);
                for (int i = st; i < mb.v.Count; i++) mb.v[i] = Quaternion.Euler(0, 0, 90) * mb.v[i] + new Vector3(-s * 1.5f, 0, 0);
                for (int i = st; i < mb.n.Count; i++) mb.n[i] = Quaternion.Euler(0, 0, 90) * mb.n[i];
            }
            else if (pw != null)
            {
                int wings = Mathf.Clamp(plan.wings, 1, 4);
                float wl = s * (pw.id == "sol.s" ? 1.2f : pw.id == "sol.xl" ? 3.4f : 2.2f), ww = s * (pw.id == "sol.xl" ? 1.3f : 0.9f);
                size = Mathf.Max(size, s + wl * 2);
                mb.Mat = MeshBuilder.MatGloss;
                for (int k = 0; k < wings; k++)
                {
                    float sideSign = k % 2 == 0 ? 1 : -1;
                    float along = k < 2 ? 0 : (k == 2 ? s * 0.0f : 0);
                    Quaternion q = Quaternion.Euler(0, k >= 2 ? 90 : 0, 0);
                    Vector3 c = q * new Vector3(sideSign * (s * 0.5f + wl * 0.5f + s * 0.15f), along, 0);
                    mb.AddBox(c, q * new Vector3(wl, s * 0.03f, ww), panel, q);
                    mb.Mat = MeshBuilder.MatMetal;
                    mb.AddBox(q * new Vector3(sideSign * (s * 0.5f + s * 0.075f), 0, 0), q * new Vector3(s * 0.15f, s * 0.04f, s * 0.04f), grey, q);
                    mb.Mat = MeshBuilder.MatGloss;
                }
            }
            // antenna
            var ant = MissionDesign.Part(plan.antenna);
            mb.Mat = MeshBuilder.MatMetal;
            if (ant != null && ant.id != "ant.low")
            {
                float dr = ant.id == "ant.deep" ? s * 1.2f : s * 0.6f;
                int st = mb.v.Count;
                mb.AddFrustum(dr * 0.35f, 0, dr, dr * 0.12f, new Color(0.92f, 0.92f, 0.94f), 20, false, false, true);
                for (int i = st; i < mb.v.Count; i++) mb.v[i] += new Vector3(0, s * 0.62f, 0);
                mb.AddBox(new Vector3(0, s * 0.62f + dr * 0.4f, 0), new Vector3(s * 0.04f, dr * 0.8f, s * 0.04f), grey, Quaternion.identity);
            }
            else mb.AddBox(new Vector3(s * 0.3f, s * 0.9f, s * 0.3f), new Vector3(s * 0.03f, s * 0.6f, s * 0.03f), grey, Quaternion.identity);
            // instruments on the bottom face, pointing at the world
            int n = plan.instruments.Count;
            for (int k = 0; k < n; k++)
            {
                var ins = MissionDesign.Part(plan.instruments[k]);
                if (ins == null) continue;
                float a = (k + 0.5f) / Mathf.Max(1, n) * Mathf.PI * 2;
                Vector3 at = new Vector3(Mathf.Cos(a) * s * 0.28f, -s * 0.62f, Mathf.Sin(a) * s * 0.28f);
                Color col = ins.id == "ins.cam" || ins.id == "ins.sharp" ? new Color(0.1f, 0.1f, 0.12f) : ins.id == "ins.mag" ? grey : ins.id == "ins.relay" ? new Color(0.85f, 0.85f, 0.88f) : new Color(0.5f, 0.52f, 0.56f);
                if (ins.id == "ins.mag" || ins.id == "ins.part")
                {
                    mb.AddBox(at + new Vector3(0, 0, s * 0.6f), new Vector3(s * 0.02f, s * 0.02f, s * 1.2f), grey, Quaternion.identity);
                    mb.AddBox(at + new Vector3(0, 0, s * 1.2f), new Vector3(s * 0.08f, s * 0.08f, s * 0.08f), col, Quaternion.identity);
                    continue;
                }
                int st = mb.v.Count;
                float len = ins.id == "ins.sharp" ? s * 0.5f : s * 0.22f, rad = ins.id == "ins.sharp" ? s * 0.13f : s * 0.08f;
                if (ins.id == "ins.alt" || ins.id == "ins.ice") { mb.AddBox(at + new Vector3(0, -s * 0.03f, 0), new Vector3(s * 0.4f, s * 0.05f, s * 0.25f), col, Quaternion.identity); continue; }
                mb.AddFrustum(0, -len, rad, rad * 1.1f, col, 12, false, true);
                for (int i = st; i < mb.v.Count; i++) mb.v[i] += at;
            }
            // engine
            var prop = MissionDesign.Part(plan.propulsion);
            if (prop != null && prop.fuelKg > 0)
            {
                int st = mb.v.Count;
                float nz = prop.id == "prop.s" ? s * 0.08f : prop.id == "prop.l" ? s * 0.3f : s * 0.18f;
                mb.Mat = MeshBuilder.MatDarkMetal;
                mb.AddFrustum(0, -nz * 1.6f, nz * 0.4f, nz, new Color(0.3f, 0.3f, 0.32f), 14, false, false, true);
                for (int i = st; i < mb.v.Count; i++) mb.v[i] = new Vector3(mb.v[i].x, -mb.v[i].y, mb.v[i].z) + new Vector3(0, s * 0.6f, -s * 0.0f) + new Vector3(0, 0, 0);
                for (int i = st; i < mb.n.Count; i++) mb.n[i] = new Vector3(mb.n[i].x, -mb.n[i].y, mb.n[i].z);
                for (int i = st; i < mb.v.Count; i++) mb.v[i] = Quaternion.Euler(90, 0, 0) * mb.v[i] + new Vector3(0, 0, -s * 0.1f);
                for (int i = st; i < mb.n.Count; i++) mb.n[i] = Quaternion.Euler(90, 0, 0) * mb.n[i];
            }
            var go = new GameObject("Mission spacecraft");
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Mission spacecraft");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mats.Lit;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
