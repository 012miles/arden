using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 26 (captaincarl24's picks): a real countdown and booster recovery.
    ///
    /// Countdown: on a pad, SPACE starts "T-10": a beep each second, the service tower's swing arms pull away (T-8 to
    /// T-5), water floods the pad and steam rolls out (from T-6), and at zero the first stage fires. SPACE again goes at
    /// once. Settings has Countdown On/Off.
    ///
    /// Recovery: coming down over Arden near the space centre, a landing guide shows the nearest place to land (the barge
    /// or a pad), how far and which way, and where the ship will come down if it keeps going; a tall beam of light marks the
    /// spot. A spent stage that lands on the barge or a pad is recovered for its value by itself.
    /// </summary>
    public partial class FlightMode
    {
        public static bool ForceCountdown;
        public static bool CountdownOn
        {
            get { try { return PlayerPrefs.GetInt("countdown", 1) == 1; } catch { return true; } }
            set { try { PlayerPrefs.SetInt("countdown", value ? 1 : 0); } catch { } }
        }

        float countdown = -1f;
        int countdownPad = -1, lastBeep;
        bool steamStarted;

        BodyView SiteView => root.bodyViews.FirstOrDefault(b => b != null && b.SiteTransform != null);

        /// <summary>SPACE pressed: start (or cut short) the countdown. False when it doesn't apply and the stage should fire.</summary>
        bool TryCountdown()
        {
            if (CoopBlock(CAct.Stage, "stage")) return true;      // a rocket room: the flight engineer's job
            if (countdown >= 0f) { Liftoff(); return true; }
            if (!CountdownOn || (Universe.IsAutotest && !ForceCountdown)) return false;
            var v = active;
            if (v == null || v.body != Home || !v.landed || v.hasLaunched || launchRunway || v.nextStage != 0) return false;
            int pad = LaunchSite.PadAt(Home, Home.InertialToFixed(v.pos, ut));
            if (pad < 0) return false;
            if (WindHold(pad)) return true;
            countdown = 10f;
            countdownPad = pad;
            lastBeep = 11;
            steamStarted = false;
            Show("Countdown started. SPACE again to launch now.", 3f);
            return true;
        }

        /// <summary>For the screenshot tour: start the countdown as if SPACE was pressed.</summary>
        public bool StartCountdownNow() => TryCountdown();

        /// <summary>For the screenshot tour: put the ship over the harbour, falling, so the landing guide shows.</summary>
        public void DropOverHarbour(double height, double sideways)
        {
            var bg = LaunchSite.Barge;
            Vector3d dir = LaunchSite.DirectionAt(Home, (bg.e0 + bg.e1) / 2 - sideways, (bg.n0 + bg.n1) / 2 + sideways * 0.5);
            Vector3d up = Home.FixedToInertial(dir, ut);
            active.landed = false;
            active.hasLaunched = true;
            active.pos = up * (Home.radius + height);
            active.vel = Home.SurfaceVelocity(active.pos) - up * 60;
            active.prevPos = active.pos;
        }

        void UpdateCountdown(float dt)
        {
            if (countdown < 0f) return;
            if (active == null || active.destroyed || active.hasLaunched) { countdown = -1f; return; }
            countdown -= dt;
            int sec = Mathf.CeilToInt(countdown);
            if (sec < lastBeep && sec >= 1) { lastBeep = sec; audio?.Clunk(0.16f); }
            SiteView?.SetPadArms(countdownPad, Mathf.Clamp01((8f - countdown) / 3f));
            if (!steamStarted && countdown <= 6f)
            {
                steamStarted = true;
                Vector3d dir = LaunchSite.PadDirection(Home, countdownPad);
                Vector3d abs = Home.AbsolutePosition(renderUt) + Home.FixedToInertial(dir * (Home.radius + LaunchSite.PadHeight), ut);
                Smoke.Steam((abs - originAbs).ToFloat(), Home.FixedToInertial(dir, ut).ToFloat(), 14f, 12f);
            }
            if (countdown <= 0f) Liftoff();
        }

        void Liftoff()
        {
            countdown = -1f;
            SiteView?.SetPadArms(countdownPad, 1f);
            Internal(Stage);
            audio?.Whoosh(0.7f);
        }

        /// <summary>The big T-minus number at the top of the screen.</summary>
        void DrawCountdown(float W)
        {
            if (countdown < 0f) return;
            int sec = Mathf.Max(0, Mathf.CeilToInt(countdown));
            var r = new Rect(W * 0.5f - 160, 112, 320, 96);
            GUI.Box(r, GUIContent.none, Ui.Panel);
            GUI.Label(new Rect(r.x, r.y + 6, r.width, 56), sec > 0 ? $"T-{sec}" : "LIFTOFF", Ui.BigCenter);
            string what = countdown > 8f ? "Final checks" : countdown > 5f ? "Swing arms pulling away" : countdown > 3f ? "Water on the pad" : "Engines starting";
            GUI.Label(new Rect(r.x, r.y + 60, r.width, 22), $"{what}   ·   SPACE: go now", new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter });
        }

        // ---------------------------------------------------------------- recovery

        GameObject guideBeam;
        float recoverTimer;
        string guideTarget;
        double guideDist, guideMiss;
        float guideBearing;

        /// <summary>Where to land near the space centre: the barge, then each pad.</summary>
        static (string name, double e, double n)[] LandingSpots()
        {
            var b = LaunchSite.Barge;
            var list = new System.Collections.Generic.List<(string, double, double)> { ("the barge", (b.e0 + b.e1) / 2, (b.n0 + b.n1) / 2) };
            foreach (var p in LaunchSite.Pads) list.Add((p.name.ToLowerInvariant(), p.e, p.n));
            return list.ToArray();
        }

        /// <summary>Once a frame: is the guide wanted, where's the nearest spot, where will we come down.</summary>
        void UpdateLandingGuide()
        {
            guideTarget = null;
            var v = active;
            bool want = v != null && !v.destroyed && v.body == Home && v.hasLaunched && !v.landed && !v.IsEva && v.Altitude < 40_000;
            if (want)
            {
                Vector3d up = v.pos.normalized;
                Vector3d vs = v.SurfaceRelVelocity;
                double vUp = Vector3d.Dot(vs, up);
                if (vUp > -3) want = false;
                else
                {
                    Vector3d fixedDir = Home.InertialToFixed(up, ut);
                    Vector3d east = CelestialBody.EastAt(LaunchSite.Direction), north = CelestialBody.NorthAt(LaunchSite.Direction);
                    Vector3d off = (fixedDir - LaunchSite.Direction * Vector3d.Dot(fixedDir, LaunchSite.Direction)) * Home.radius;
                    double e = Vector3d.Dot(off, east), n = Vector3d.Dot(off, north);
                    // where it comes down if it keeps falling as it is (no air): a guide, not a promise
                    double h = System.Math.Max(0, v.RadarAltitude(ut)), g = Home.mu / (v.pos.magnitude * v.pos.magnitude);
                    double fall = -vUp, t = (fall + System.Math.Sqrt(fall * fall + 2 * g * h)) / g;
                    Vector3d vFixed = Home.InertialToFixed(vs - up * vUp, ut);
                    double ve = Vector3d.Dot(vFixed, east), vn = Vector3d.Dot(vFixed, north);
                    double pe = e + ve * t, pn = n + vn * t;
                    double best = double.MaxValue;
                    foreach (var s in LandingSpots())
                    {
                        double d = System.Math.Sqrt((s.e - pe) * (s.e - pe) + (s.n - pn) * (s.n - pn));
                        if (d < best) { best = d; guideTarget = s.name; guideDist = System.Math.Sqrt((s.e - e) * (s.e - e) + (s.n - n) * (s.n - n)); guideMiss = d; guideBearing = Mathf.Atan2((float)(s.e - e), (float)(s.n - n)) * Mathf.Rad2Deg; PlaceBeam(s.e, s.n); }
                    }
                    if (guideDist > 60_000) guideTarget = null;
                }
            }
            if (guideBeam != null && guideBeam.activeSelf != (guideTarget != null)) guideBeam.SetActive(guideTarget != null);
        }

        void PlaceBeam(double e, double n)
        {
            if (guideBeam == null)
            {
                var b = new MeshBuilder();
                b.AddFrustum(700f, 0f, 3f, 7f, new Color(1f, 0.62f, 0.2f, 0.32f), 16);
                guideBeam = new GameObject("Landing beam");
                guideBeam.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Beam");
                var mr = guideBeam.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.Additive;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Vector3d dir = LaunchSite.DirectionAt(Home, e, n);
            double top = LaunchSite.SurfaceHeight(Home, dir);
            Vector3d abs = Home.AbsolutePosition(renderUt) + Home.FixedToInertial(dir * (Home.radius + top), ut);
            Vector3 upI = Home.FixedToInertial(dir, ut).ToFloat();
            guideBeam.transform.SetPositionAndRotation((abs - originAbs).ToFloat(), Quaternion.FromToRotation(Vector3.up, upI));
        }

        void DrawLandingGuide(float W)
        {
            if (guideTarget == null) return;
            var r = new Rect(W * 0.5f - 190, GuideY, 380, 70);
            GUI.Box(r, GUIContent.none, Ui.Panel);
            GUI.Label(new Rect(r.x + 64, r.y + 8, r.width - 74, 24), $"Land on {guideTarget}: {Ui.Dist(guideDist)}", Ui.Bold);
            string miss = guideMiss < 60 ? "on target" : $"you'll come down {Ui.Dist(guideMiss)} off";
            GUI.Label(new Rect(r.x + 64, r.y + 36, r.width - 74, 22), Words.T($"Ballistic landing point: {miss}", $"Where you're heading: {miss}", $"Keep it {(guideMiss < 60 ? "steady" : "closer")}: {miss}"), Ui.Small);
            // an arrow toward it, turned to match the camera (whose yaw the flight camera counts toward the west)
            float a = -(guideBearing) - camYaw + 180f;
            var c = new Vector2(r.x + 32, r.y + 35);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(a, c * Ui.Scale);
            GUI.Label(new Rect(c.x - 20, c.y - 22, 40, 40), "▲", Ui.BigCenter);
            GUI.matrix = m;
        }

        /// <summary>Spent stages that land on the barge or a pad come home by themselves (once a second).</summary>
        void UpdateRecovery(float dt)
        {
            recoverTimer -= dt;
            if (recoverTimer > 0) return;
            recoverTimer = 1f;
            for (int i = U.vessels.Count - 1; i >= 0; i--)
            {
                var u = U.vessels[i];
                if (u == active || u.destroyed || !u.landed || !u.hasLaunched || u.body != Home || u.CrewCount > 0) continue;
                Vector3d f = Home.InertialToFixed(u.pos, ut);
                bool barge = LaunchSite.OnBarge(Home, f);
                if (!barge && LaunchSite.PadAt(Home, f) < 0) continue;
                string note = U.career.Recover(u, ut, root.bodies);
                RemoveView(u);
                U.Remove(u);
                Show($"{(barge ? "From the barge: " : "From the pad: ")}{note}", 6f);
            }
        }
    }
}
