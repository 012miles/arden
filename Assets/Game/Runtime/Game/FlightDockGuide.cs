using System;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 27 (captaincarl24's pick): the docking guide. Close to a targeted ship with a free port, a scope shows how your
    /// port lines up with theirs: the dot is where you are off their port's line (slide to bring it to the middle), the
    /// ring is which way your port faces (turn to centre it), with the closing speed against a comfortable one and a line
    /// of advice. A box marks their port in the view.
    /// </summary>
    public partial class FlightMode
    {
        public string dockHint;

        void DrawDockGuide(float W, float H)
        {
            dockHint = null;
            if (mapView || dockMine == null || targetVessel == null || targetVessel.destroyed || dockDist > 400) return;
            if (!active.PortFaceWorld(dockMine, out var pm, out var nm) || !targetVessel.PortFaceWorld(dockTheirs, out var pt, out var nt)) return;
            var cam = root.farCam;

            // the box round their port, in the view
            Vector3 portRender = (pt - renderRel).ToFloat();
            Vector3 sp = cam.WorldToScreenPoint(portRender);
            if (sp.z > 0)
            {
                float x = sp.x / Ui.Scale, y = (Screen.height - sp.y) / Ui.Scale;
                float s = Mathf.Clamp(900f / (float)Math.Max(dockDist, 1), 18f, 70f);
                var col = new Color(0.45f, 1f, 0.6f, 0.95f);
                Bracket(new Rect(x - s, y - s, s * 2, s * 2), col);
                Ui.Text(new Rect(x - 90, y + s + 2, 180, 20), $"their port  {Ui.Dist(dockDist)}", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperCenter });
            }

            // the scope: their port's line seen from your side, the screen's right and up laid onto it
            Vector3d right = Vector3d.FromFloat(cam.transform.right), up = Vector3d.FromFloat(cam.transform.up);
            Vector3d rgt = (right - nt * Vector3d.Dot(right, nt));
            if (rgt.sqrMagnitude < 1e-6) rgt = up - nt * Vector3d.Dot(up, nt);
            rgt = rgt.normalized;
            Vector3d upv = Vector3d.Cross(rgt, nt).normalized;
            if (Vector3d.Dot(upv, up) < 0) upv = upv * -1;
            Vector3d d = pm - pt;
            Vector3d lat = d - nt * Vector3d.Dot(d, nt);
            double lx = Vector3d.Dot(lat, rgt), ly = Vector3d.Dot(lat, upv);
            Vector3d mis = nm + nt;                    // zero when your port faces straight back at theirs
            double ax = Vector3d.Dot(mis, rgt), ay = Vector3d.Dot(mis, upv);

            float size = 190;
            var r = new Rect(W - size - 24, H * 0.5f - size * 0.5f - 60, size, size + 104);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 10, r.y + 6, r.width - 20, 22), "Docking guide", Ui.Header);
            var scope = new Rect(r.x + 15, r.y + 32, size - 30, size - 30);
            var line = new Color(0.55f, 0.7f, 0.85f, 0.6f);
            Ui.Line(new Rect(scope.x, scope.center.y, scope.width, 1), line);
            Ui.Line(new Rect(scope.center.x, scope.y, 1, scope.height), line);
            Bracket(new Rect(scope.center.x - 14, scope.center.y - 14, 28, 28), new Color(0.45f, 1f, 0.6f, 0.7f));
            // how far off the line: squeezed so 0.2 m and 20 m both show
            float S(double v) => (float)(Math.Sign(v) * Math.Min(1, Math.Log10(1 + Math.Abs(v) * 5) / Math.Log10(101)));
            bool lined = dockLateral < 0.5, facing = dockAngle < VesselSim.DockMaxAngle;
            var dotCol = lined ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.7f, 0.35f);
            Vector2 c = scope.center, half = scope.size * 0.5f;
            Vector2 dp = c + new Vector2(S(lx) * half.x, -S(ly) * half.y);
            Ui.Line(new Rect(dp.x - 5, dp.y - 5, 10, 10), dotCol);
            Vector2 ap = c + new Vector2((float)Math.Max(-1, Math.Min(1, ax * 1.4)) * half.x, -(float)Math.Max(-1, Math.Min(1, ay * 1.4)) * half.y);
            var ringCol = facing ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.4f);
            Ui.Line(new Rect(ap.x - 11, ap.y - 11, 22, 2), ringCol);
            Ui.Line(new Rect(ap.x - 11, ap.y + 9, 22, 2), ringCol);
            Ui.Line(new Rect(ap.x - 11, ap.y - 11, 2, 22), ringCol);
            Ui.Line(new Rect(ap.x + 9, ap.y - 11, 2, 22), ringCol);

            float ty = scope.yMax + 6;
            double good = Stations.GoodSpeed(dockDist);
            bool okSpeed = dockClosing >= -0.05 && dockClosing <= good * 1.6 + 0.1;
            string cs(bool ok) => ok ? "#7dff8a" : "#ffb070";
            Ui.Text(new Rect(r.x + 10, ty, r.width - 20, 20), $"<color={cs(okSpeed)}>{(dockClosing >= 0 ? "closing" : "moving away")} {Math.Abs(dockClosing):0.00} m/s</color> (aim {good:0.0})", Ui.Small);
            Ui.Text(new Rect(r.x + 10, ty + 18, r.width - 20, 20), $"<color={cs(lined)}>off line {dockLateral:0.00} m</color>  <color={cs(facing)}>{dockAngle:0} deg</color>", Ui.Small);
            dockHint = Stations.DockHint(dockDist, dockLateral, dockAngle, dockClosing);
            Ui.Text(new Rect(r.x + 10, ty + 38, r.width - 20, 36), $"<b>{dockHint}</b>", Ui.Small);
        }

        static void Bracket(Rect r, Color c)
        {
            float k = Mathf.Max(6f, r.width * 0.28f), t = 2f;
            Ui.Line(new Rect(r.x, r.y, k, t), c); Ui.Line(new Rect(r.x, r.y, t, k), c);
            Ui.Line(new Rect(r.xMax - k, r.y, k, t), c); Ui.Line(new Rect(r.xMax - t, r.y, t, k), c);
            Ui.Line(new Rect(r.x, r.yMax - t, k, t), c); Ui.Line(new Rect(r.x, r.yMax - k, t, k), c);
            Ui.Line(new Rect(r.xMax - k, r.yMax - t, k, t), c); Ui.Line(new Rect(r.xMax - t, r.yMax - k, t, k), c);
        }
    }
}
