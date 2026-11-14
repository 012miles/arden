using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rocket
{
    /// <summary>
    /// Attitude ball. A textured sphere seen by its own orthographic camera (layer 10) into a render texture.
    /// The ball turns so the point under the centre reticle is where the nose points; screen-up is the
    /// vessel's top side. Markers (prograde, normal, maneuver...) are projected onto it.
    /// </summary>
    public class Navball
    {
        public const int Layer = 10;
        const float BallRadius = 0.86f;
        readonly Camera cam;
        readonly Transform ball;
        readonly RenderTexture rt;
        readonly Texture2D bezel;
        readonly Dictionary<MeshFactory.MarkerKind, Texture2D> icons = new Dictionary<MeshFactory.MarkerKind, Texture2D>();
        Quaternion ballRot = Quaternion.identity;
        Vector3d east, up, south;

        public Navball()
        {
            rt = new RenderTexture(384, 384, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "NavballRT" };
            var rig = new GameObject("NavballRig");
            var b = new GameObject("Navball") { layer = Layer };
            b.transform.SetParent(rig.transform, false);
            b.transform.localScale = Vector3.one * BallRadius;
            b.AddComponent<MeshFilter>().sharedMesh = MeshFactory.NavballMesh();
            var mat = new Material(Resources.Load<Shader>("Shaders/RocketNavball")) { mainTexture = MeshFactory.NavballTexture() };
            var mr = b.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ball = b.transform;

            var cg = new GameObject("NavballCamera");
            cg.transform.SetParent(rig.transform, false);
            cg.transform.localPosition = new Vector3(0, 0, -3f);
            cam = cg.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 10f;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.065f, 0.09f, 1f);
            cam.targetTexture = rt;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;

            bezel = MeshFactory.BezelTexture();
            foreach (MeshFactory.MarkerKind k in System.Enum.GetValues(typeof(MeshFactory.MarkerKind)))
                icons[k] = MeshFactory.MarkerTexture(k);
        }

        public void SetActive(bool on) => cam.enabled = on;

        Vector3 Local(Vector3d w) => new Vector3((float)Vector3d.Dot(w, east), (float)Vector3d.Dot(w, up), (float)Vector3d.Dot(w, south));

        public void Orient(VesselSim v)
        {
            up = v.pos.normalized;
            east = CelestialBody.EastAt(up);
            south = -CelestialBody.NorthAt(up);
            Vector3 ly = Local(Vector3d.Rotate(v.rot, Vector3d.up));
            Vector3 lz = Local(Vector3d.Rotate(v.rot, new Vector3d(0, 0, 1)));
            Quaternion mv = Quaternion.LookRotation(lz, ly);
            ballRot = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Inverse(mv);
            ball.localRotation = ballRot;
        }

        /// <summary>Where a world direction lands on the ball, in unit-circle coords (y up), or null if on the far side.</summary>
        public Vector2? Project(Vector3d dir)
        {
            Vector3 c = ballRot * Local(dir.normalized);
            if (c.z > 0.02f) return null;
            return new Vector2(c.x, c.y);
        }

        public void Draw(Rect r, IList<(MeshFactory.MarkerKind kind, Vector3d dir)> markers)
        {
            GUI.DrawTexture(r, rt);
            GUI.DrawTexture(r, bezel);
            Vector2 center = r.center;
            float rad = r.width * 0.5f * BallRadius;
            var old = GUI.color;
            foreach (var m in markers)
            {
                var p = Project(m.dir);
                if (!p.HasValue) continue;
                Vector2 sp = center + new Vector2(p.Value.x, -p.Value.y) * rad;
                GUI.color = MeshFactory.MarkerColor(m.kind);
                const float s = 26f;
                GUI.DrawTexture(new Rect(sp.x - s / 2, sp.y - s / 2, s, s), icons[m.kind]);
            }
            // Fixed reticle: the vessel's nose.
            GUI.color = new Color(1f, 0.6f, 0.1f);
            Ui.Line(new Rect(center.x - 30, center.y - 1.5f, 20, 3), GUI.color);
            Ui.Line(new Rect(center.x + 10, center.y - 1.5f, 20, 3), GUI.color);
            Ui.Line(new Rect(center.x - 3, center.y - 3, 6, 6), GUI.color);
            Ui.Line(new Rect(center.x - 1.5f, center.y + 3, 3, 10), GUI.color);
            GUI.color = old;
        }
    }
}
