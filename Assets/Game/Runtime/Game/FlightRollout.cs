using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 25 (captaincarl24's pick): a rocket going to a pad rolls out first. It rides a crawler out of its hall's great
    /// door, then (after a short fade, the trip itself skipped) comes up the rocket road to the pad, the camera swinging round
    /// it. No time passes in the world while it does. SPACE, ESC or the Skip button jump straight to the pad.
    /// </summary>
    public partial class FlightMode
    {
        /// <summary>Autotests skip the roll-out unless they ask for it.</summary>
        public static bool ForceRollout;
        public static bool RolloutOn
        {
            get { try { return PlayerPrefs.GetInt("rollout", 1) == 1; } catch { return true; } }
            set { try { PlayerPrefs.SetInt("rollout", value ? 1 : 0); } catch { } }
        }

        class Rollout
        {
            public GameObject crawler;
            public int pad;
            public double fromE, fromN, toE, toN, ue, un;   // where each leg starts and the road's direction
            public float t, liftAboveGround;
            public Vector3d rel;                              // the rocket's place (relative to Arden's centre, inertial) this frame
            public Vector3d groundRel, fwd, up;
            public float fade;                                // 0 clear .. 1 black
        }
        Rollout rollout;
        static Mesh crawlerMesh;

        const float LegA = 6f, LegB = 9f, FadeTime = 0.6f, SpeedA = 4f, SpeedB = 6f;

        void StartRollout(int pad)
        {
            if (active == null || active.body != Home) return;
            var p = LaunchSite.Pads[Mathf.Clamp(pad, 0, LaunchSite.Pads.Length - 1)];
            double de = p.e - p.fromE, dn = p.n - p.fromN, dl = System.Math.Sqrt(de * de + dn * dn);
            crawlerMesh ??= BodyView.CrawlerMesh(false);
            var go = new GameObject("Rollout crawler");
            go.AddComponent<MeshFilter>().sharedMesh = crawlerMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            // the rocket stands on the crawler's deck: as high above the deck as it stands above the pad
            float standsOnPad = (float)(active.Altitude - LaunchSite.PadHeight);
            rollout = new Rollout { crawler = go, pad = pad, ue = de / dl, un = dn / dl, fromE = p.fromE, fromN = p.fromN, toE = p.e, toN = p.n, liftAboveGround = BodyView.CrawlerDeck + standsOnPad, fade = 1f };
            camYaw = RoadBearing() + 28f;   // ahead of the crawler, looking back at the hall door
            camPitch = 9f;
            camDist = Mathf.Clamp(active.Length * 1.7f + 34f, 40f, 170f);
            PlaceRollout();
        }

        /// <summary>Where the crawler is along its two short legs: leaving the hall, then arriving at the pad.</summary>
        void PlaceRollout()
        {
            var r = rollout;
            double e, n;
            if (r.t < LegA)
            {
                double s = 15 + r.t * SpeedA;                            // just out of the door (the crawler is 30 m long), onto the apron
                e = r.fromE + r.ue * s; n = r.fromN + r.un * s;
            }
            else
            {
                double s = -90 + (r.t - LegA) * SpeedB;                  // the last stretch of road before the pad
                e = r.toE + r.ue * s; n = r.toN + r.un * s;
            }
            Vector3d dir = LaunchSite.DirectionAt(Home, e, n);
            Vector3d east = CelestialBody.EastAt(dir), north = CelestialBody.NorthAt(dir);
            r.up = Home.FixedToInertial(dir, ut);
            r.fwd = Home.FixedToInertial(east * r.ue + north * r.un, ut);
            r.groundRel = Home.FixedToInertial(dir * (Home.radius + 0.12), ut);
            r.rel = r.groundRel + r.up * r.liftAboveGround;
            // fade in at the start, out and in again between the legs, out at the end
            float tEnd = LegA + LegB;
            r.fade = Mathf.Max(Mathf.Clamp01(1f - r.t / FadeTime), Mathf.Clamp01(1f - Mathf.Abs(r.t - LegA) / FadeTime), Mathf.Clamp01((r.t - (tEnd - FadeTime)) / FadeTime));
        }

        /// <summary>The camera's bearing along the road (the flight camera measures its yaw toward geographic west).</summary>
        float RoadBearing() => Mathf.Atan2(-(float)rollout.ue, (float)rollout.un) * Mathf.Rad2Deg;

        void AdvanceRollout(float dt)
        {
            bool wasA = rollout.t < LegA;
            rollout.t += dt;
            if (rollout.t >= LegA + LegB) { EndRollout(); return; }
            if (wasA && rollout.t >= LegA) { camYaw = RoadBearing() + 75f; camPitch = 7f; }   // the second leg: from beside the road ahead
            camYaw += dt * 4f;
            PlaceRollout();
        }

        /// <summary>Puts the crawler under the rocket (call after the views have their origin for this frame).</summary>
        void PlaceRolloutCrawler()
        {
            if (rollout == null) return;
            Vector3d abs = Home.AbsolutePosition(renderUt) + rollout.groundRel;
            rollout.crawler.transform.SetPositionAndRotation((abs - originAbs).ToFloat(), Quaternion.LookRotation(rollout.fwd.ToFloat(), rollout.up.ToFloat()));
        }

        void EndRollout()
        {
            if (rollout == null) return;
            if (rollout.crawler != null) Object.Destroy(rollout.crawler);
            int pad = rollout.pad;
            rollout = null;
            camYaw = 180f;
            camPitch = 6f;
            camDist = Mathf.Clamp(active.Length * 1.5f + 10f, 15f, 150f);
            Show($"{active.name} is on {LaunchSite.Pads[pad].name.ToLowerInvariant()}. Press SPACE to launch.", 6f);
        }

        /// <summary>The roll-out's own screen: a caption, a Skip button and the fades.</summary>
        void DrawRollout()
        {
            float W = Ui.W, H = Ui.H;
            var r = rollout;
            string where = LaunchSite.Pads[r.pad].name.ToLowerInvariant();
            string say = r.t < LegA ? Words.T($"Rolling out to {where}", $"Rolling out to {where}", $"Going to {where}")
                                    : Words.T($"Arriving at {where}", $"Arriving at {where}", $"Nearly at {where}");
            var box = new Rect(W * 0.5f - 230, H - 120, 460, 70);
            GUI.Box(box, GUIContent.none, Ui.Panel);
            GUI.Label(new Rect(box.x + 16, box.y + 8, box.width - 150, 30), say, Ui.Title);
            GUI.Label(new Rect(box.x + 16, box.y + 38, box.width - 150, 24), Words.T("SPACE to skip", "SPACE to skip", "Press SPACE to skip"), Ui.Small);
            if (Ui.Press(new Rect(box.xMax - 120, box.y + 16, 104, 38), "Skip  >", Ui.Button)) EndRollout();
            if (r != null && rollout != null && r.fade > 0.001f)
            {
                var was = GUI.color;
                GUI.color = new Color(0, 0, 0, r.fade);
                GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                GUI.color = was;
            }
        }
    }
}
