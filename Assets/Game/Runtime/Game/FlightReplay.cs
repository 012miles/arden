using System;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 28 (captaincarl24's pick): flight replays. The flight is recorded as it goes (FlightRecording); "Watch replay"
    /// (pause menu, or after a crash) freezes the flight and plays it back from movie cameras: the Director (a tower
    /// camera tracking the launch with a long lens, then flybys and chase shots in turn), or Chase, Tower or Flyby by
    /// hand. Slow motion to 8x, a timeline to jump about, and the ship as it was at each moment (stages dropping, chutes
    /// opening, engines lit). A crash ends in its explosion.
    /// </summary>
    public partial class FlightMode
    {
        public FlightRecording recorder = new FlightRecording();

        public enum ReplayCam { Director, Chase, Tower, Flyby }

        public class ReplayState
        {
            public FlightRecording rec;
            public double ut;
            public float speed = 1f;
            public bool playing = true, boomed;
            public ReplayCam cam = ReplayCam.Director;
            public int snapIndex = -1;
            public VesselSim ghost;
            public VesselView ghostView;
            public CelestialBody body;
            public Vector3d pos;
            public Quaternion rot;
            public Vector3d towerFixed;
            public CelestialBody towerBody;
            public Vector3d flybyFixed;
            public bool flybySet;
            public float directorTimer;
            public ReplayCam directorShot = ReplayCam.Tower;
        }

        public ReplayState replay;

        public bool CanReplay => recorder != null && recorder.Watchable && rollout == null;

        public void StartReplay()
        {
            if (!CanReplay) return;
            if (photoMode) TogglePhoto();
            paused = false;
            mapView = false;
            showPractice = false;
            SetWarpOne();
            var r = new ReplayState { rec = recorder, ut = recorder.StartUt };
            // the tower camera: on the ground 160 m from where the recording starts, a little up
            var f0 = recorder.frames[0];
            r.towerBody = root.bodies[f0.body];
            Vector3d d0 = r.towerBody.InertialToFixed(f0.pos, f0.ut).normalized;
            Vector3d e = CelestialBody.EastAt(d0), n = CelestialBody.NorthAt(d0);
            Vector3d td = (d0 + (e * -0.6 + n * -0.8).normalized * (160 / r.towerBody.radius)).normalized;
            r.towerFixed = td * (r.towerBody.radius + LaunchSite.SurfaceHeight(r.towerBody, td) + 2.5);
            replay = r;
            foreach (var v in views.Values) v.SetVisible(false);
            camDist = Mathf.Max(camDist, 25f);
            GroundFx.StopDust();
            Show("Replay: SPACE pauses, ESC goes back to the flight", 3f);
        }

        public void EndReplay()
        {
            if (replay == null) return;
            replay.ghostView?.Destroy();
            replay = null;
            foreach (var v in views.Values) v.SetVisible(true);
            if (views.TryGetValue(active, out var av)) av.SetVisible(!active.destroyed);
            root.farCam.fieldOfView = root.nearCam.fieldOfView = 60f;
        }

        void HandleReplayInput()
        {
            if (In.Down(K.Escape)) { EndReplay(); return; }
            if (In.Down(K.Space)) replay.playing = !replay.playing;
            if (In.RightMouse)
            {
                var d = In.MouseDelta;
                camYaw += d.x * 0.25f;
                camPitch = Mathf.Clamp(camPitch - d.y * 0.25f, -85f, 85f);
            }
            float sc = In.Scroll;
            if (Mathf.Abs(sc) > 0.01f && !Ui.MouseOverUI(In.MousePosition)) camDist = Mathf.Clamp(camDist * (sc > 0 ? 1f / 1.15f : 1.15f), 2.5f, 5000f);
            active.inputPitch = active.inputYaw = active.inputRoll = active.driveInput = 0;
        }

        /// <summary>Moves the replay on: warped stretches of the recording go by at about ten samples a second.</summary>
        void AdvanceReplay(float realDt)
        {
            var r = replay;
            var rec = r.rec;
            if (r.playing)
            {
                int i = rec.IndexAt(r.ut);
                double gapUt = i < rec.Count - 1 ? rec.frames[i + 1].ut - rec.frames[i].ut : 0.1;
                double rate = Math.Max(1, gapUt / 0.1);
                r.ut += realDt * r.speed * rate;
                if (r.ut >= rec.EndUt) { r.ut = rec.EndUt; r.playing = false; }
            }
            var f = rec.At(r.ut);
            r.body = root.bodies[Mathf.Clamp(f.body, 0, root.bodies.Count - 1)];
            r.pos = f.pos;
            r.rot = f.rot;
            // the ship as it was
            if (f.snap != r.snapIndex)
            {
                r.snapIndex = f.snap;
                r.ghostView?.Destroy();
                r.ghost = VesselSim.FromSave(rec.snaps[f.snap].save, root.bodies, f.ut);
                r.ghostView = VesselView.Create(r.ghost);
            }
            var snap = rec.snaps[f.snap];
            float thr = f.throttle / 255f;
            for (int k = 0; k < snap.engines.Length; k++)
            {
                var p = r.ghost.FindPart(snap.engines[k]);
                if (p != null) p.thrustFrac = (f.fire & (1u << k)) != 0 ? Mathf.Max(0.05f, thr) : 0f;
            }
            foreach (var p in r.ghost.parts)
            {
                if (p.def.kind == PartKind.Parachute && p.chuteDeployed) p.chuteOpen = f.chute / 255f;
                if (p.legDeploy > 0 || p.def.kind == PartKind.Leg || p.def.kind == PartKind.Gear) p.legDeploy = f.legs / 255f;
            }
            r.ghost.destroyed = false;
            if (f.destroyed && !r.boomed && r.ut >= rec.EndUt - 1e-6)
            {
                r.boomed = true;
                SpawnBoom(r.body, r.pos, Mathf.Clamp(r.ghost.Length * 2.5f, 8f, 40f));
                r.ghostView.SetVisible(false);
            }
            // the Director picks the shot
            if (r.cam == ReplayCam.Director)
            {
                r.directorTimer -= realDt;
                double fromTower = r.towerBody == r.body ? (r.body.InertialToFixed(r.pos, r.ut) - r.towerFixed).magnitude : double.MaxValue;
                if (r.ut - rec.StartUt < 30 && fromTower < 5000) r.directorShot = ReplayCam.Tower;
                else if (r.directorTimer <= 0 || r.directorShot == ReplayCam.Tower)
                {
                    r.directorShot = r.directorShot == ReplayCam.Flyby ? ReplayCam.Chase : ReplayCam.Flyby;
                    r.directorTimer = r.directorShot == ReplayCam.Flyby ? 6f : 8f;
                    r.flybySet = false;
                }
            }
        }

        ReplayCam ReplayShot => replay.cam == ReplayCam.Director ? replay.directorShot : replay.cam;

        /// <summary>After the normal camera is worked out: a tower or flyby shot puts the camera somewhere fixed, looking at the ship with a long lens.</summary>
        void ReplayCamera(Camera cam, Vector3 up)
        {
            var r = replay;
            var shot = ReplayShot;
            float size = Mathf.Max(4f, r.ghost != null ? r.ghost.Length : 10f);
            Vector3? at = null;
            if (shot == ReplayCam.Tower && r.towerBody == r.body)
                at = (r.body.AbsolutePosition(renderUt) + r.body.FixedToInertial(r.towerFixed, renderUt) - originAbs).ToFloat();
            else if (shot == ReplayCam.Flyby)
            {
                Vector3d fixedPos = r.body.InertialToFixed(r.pos, renderUt);
                Vector3d vel = r.body.InertialToFixed(r.rec.VelocityAt(r.ut), renderUt);
                double off = Mathf.Clamp(size * 3f, 18f, 150f);
                bool far = r.flybySet && (fixedPos - r.flybyFixed).magnitude > off * 6 + vel.magnitude * 4;
                if (!r.flybySet || far)
                {
                    Vector3d upF = fixedPos.normalized;
                    Vector3d dir = vel.sqrMagnitude > 1 ? vel.normalized : CelestialBody.EastAt(upF);
                    Vector3d side = Vector3d.Cross(upF, dir).normalized;
                    double lead = Math.Max(off * 2, vel.magnitude * 2.5);
                    r.flybyFixed = fixedPos + dir * lead + side * off + upF * (off * 0.3);
                    double ground = r.body.radius + LaunchSite.SurfaceHeight(r.body, r.flybyFixed.normalized) + 2;
                    if (r.flybyFixed.magnitude < ground) r.flybyFixed = r.flybyFixed.normalized * ground;
                    r.flybySet = true;
                }
                at = (r.body.AbsolutePosition(renderUt) + r.body.FixedToInertial(r.flybyFixed, renderUt) - originAbs).ToFloat();
            }
            if (!at.HasValue) { if (cam.fieldOfView != 60f) cam.fieldOfView = root.nearCam.fieldOfView = 60f; return; }
            Vector3 p = at.Value;
            float dist = p.magnitude;
            Vector3 camUp = (p - (r.body.AbsolutePosition(renderUt) - originAbs).ToFloat()).normalized;
            cam.transform.position = p;
            cam.transform.rotation = Quaternion.LookRotation(-p / Mathf.Max(dist, 1e-3f), camUp);
            // a long lens: the ship fills a good part of the picture however far it is
            float fov = Mathf.Clamp(2f * Mathf.Atan(size * 1.6f / Mathf.Max(dist, 1f)) * Mathf.Rad2Deg, 1.2f, 60f);
            cam.fieldOfView = root.nearCam.fieldOfView = fov;
        }

        /// <summary>The replay's world: the ship drawn where it was, everything else as it was then.</summary>
        void ReplayViews()
        {
            var r = replay;
            renderUt = r.ut;
            renderRel = r.pos;
            renderRot = r.rot;
            originAbs = r.body.AbsolutePosition(renderUt) + renderRel;
            double pressure = r.body.PressureAtm(renderRel.magnitude - r.body.radius);
            if (r.ghostView != null && !r.boomed) r.ghostView.Sync(r.ghost, Vector3.zero, renderRot, pressure, false);
            foreach (var bv in root.bodyViews) bv.Sync(originAbs, renderUt, originAbs);
            Smoke.SetAnchor((r.body.AbsolutePosition(renderUt) - originAbs).ToFloat(), r.body.RotationAt(renderUt));
            Vector3d upD = renderRel.normalized;
            double groundR = r.body.radius + LaunchSite.SurfaceHeight(r.body, r.body.InertialToFixed(upD, renderUt).normalized);
            Smoke.SetGround((upD * groundR - renderRel).ToFloat(), upD.ToFloat());
            GameRoot.SunNow = Sun.DirectionFrom(originAbs, renderUt).ToFloat();
            root.sun.transform.rotation = Quaternion.LookRotation(-GameRoot.SunNow);
        }

        void DrawReplay(float W, float H)
        {
            var r = replay;
            var rec = r.rec;
            var bar = new Rect(W * 0.5f - 480, H - 104, 960, 92);
            Ui.Box(bar);
            string name = r.snapIndex >= 0 ? rec.snaps[r.snapIndex].name : active.name;
            GUI.Label(new Rect(bar.x + 14, bar.y + 8, 300, 24), $"REPLAY  <size=13>{name}</size>", Ui.Header);
            GUI.Label(new Rect(bar.x + 320, bar.y + 10, 200, 22), $"T+{Ui.Time(r.ut - rec.StartUt)}", Ui.Label);
            // timeline (by sample, so long warps don't swamp it)
            var tl = new Rect(bar.x + 14, bar.y + 40, bar.width - 28, 10);
            Ui.Line(tl, new Color(0.2f, 0.25f, 0.32f));
            int idx = rec.IndexAt(r.ut);
            float frac = rec.Count > 1 ? idx / (float)(rec.Count - 1) : 0;
            Ui.Line(new Rect(tl.x, tl.y, tl.width * frac, tl.height), new Color(0.45f, 0.75f, 1f));
            Ui.Line(new Rect(tl.x + tl.width * frac - 3, tl.y - 4, 6, tl.height + 8), Color.white);
            if (Event.current.type == EventType.MouseDown && new Rect(tl.x, tl.y - 8, tl.width, tl.height + 16).Contains(Event.current.mousePosition))
            {
                int j = Mathf.Clamp(Mathf.RoundToInt((Event.current.mousePosition.x - tl.x) / tl.width * (rec.Count - 1)), 0, rec.Count - 1);
                r.ut = rec.frames[j].ut;
                r.boomed = false;
                r.ghostView?.SetVisible(true);
                r.flybySet = false;
                Event.current.Use();
            }
            float x = bar.x + 14, y = bar.y + 58;
            if (GUI.Button(new Rect(x, y, 84, 28), r.playing ? "Pause" : "Play", Ui.Button))
            {
                if (!r.playing && r.ut >= rec.EndUt - 1e-6) { r.ut = rec.StartUt; r.boomed = false; r.ghostView?.SetVisible(true); }
                r.playing = !r.playing;
            }
            x += 92;
            foreach (float s in new[] { 0.25f, 0.5f, 1f, 2f, 4f, 8f })
            {
                if (GUI.Button(new Rect(x, y, 50, 28), s < 1 ? $"{s:0.##}x" : $"{s:0}x", Mathf.Approximately(r.speed, s) ? Ui.RowSel : Ui.ButtonSmall)) r.speed = s;
                x += 54;
            }
            x += 14;
            foreach (ReplayCam c in Enum.GetValues(typeof(ReplayCam)))
            {
                if (GUI.Button(new Rect(x, y, 84, 28), c.ToString(), r.cam == c ? Ui.RowSel : Ui.ButtonSmall)) { r.cam = c; r.flybySet = false; r.directorTimer = 0; }
                x += 88;
            }
            if (GUI.Button(new Rect(bar.xMax - 150, y, 136, 28), "Back to flight", Ui.Button)) EndReplay();
        }
    }
}
