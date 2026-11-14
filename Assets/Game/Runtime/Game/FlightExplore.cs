using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 27 in flight: the guide to places you've spotted, dust from engines near the ground, prints and tracks,
    /// the weather line, and the countdown holding for strong wind.
    /// </summary>
    public partial class FlightMode
    {
        // ---------------------------------------------------------------- the landmark guide

        Landmark lmTarget;
        double lmDist;
        float lmBearing;
        GameObject lmBeam;

        /// <summary>Once a frame: the nearest place spotted but not yet found on this world, if it's within reach.</summary>
        void UpdateLandmarkGuide()
        {
            lmTarget = null;
            var v = active;
            if (v != null && !v.destroyed && v.body != null && !mapView && v.Altitude < 120_000)
            {
                var b = v.body;
                Vector3d here = b.InertialToFixed(v.pos, ut).normalized;
                var c = U.career;
                var l = Landmarks.Nearest(b, here, out double m, x => c.spotted.Contains(x.id) && !c.visited.Contains(x.id));
                if (l != null && m < 250_000 && v.hasLaunched && (!v.landed || m < 20_000))
                {
                    lmTarget = l; lmDist = m;
                    Vector3d e = CelestialBody.EastAt(here), n = CelestialBody.NorthAt(here);
                    lmBearing = Mathf.Atan2((float)Vector3d.Dot(l.dir - here, e), (float)Vector3d.Dot(l.dir - here, n)) * Mathf.Rad2Deg;
                }
            }
            bool beam = lmTarget != null && lmDist < 80_000;
            if (beam)
            {
                if (lmBeam == null)
                {
                    var mb = new MeshBuilder();
                    mb.AddFrustum(2500f, 0f, 12f, 30f, new Color(0.3f, 0.85f, 1f, 0.22f), 16);
                    lmBeam = new GameObject("Landmark beam");
                    lmBeam.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Landmark beam");
                    var mr = lmBeam.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Mats.Additive;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var b = active.body;
                double h = Terrain.Height(b, lmTarget.dir);
                Vector3d abs = b.AbsolutePosition(renderUt) + b.FixedToInertial(lmTarget.dir * (b.radius + h), renderUt);
                lmBeam.transform.SetPositionAndRotation((abs - originAbs).ToFloat(), Quaternion.FromToRotation(Vector3.up, b.FixedToInertial(lmTarget.dir, renderUt).ToFloat()));
            }
            if (lmBeam != null && lmBeam.activeSelf != beam) lmBeam.SetActive(beam);
        }

        void DrawLandmarkGuide(float W)
        {
            if (lmTarget == null || guideTarget != null || countdown >= 0f) return;
            var r = new Rect(W * 0.5f - 190, GuideY, 380, 66);
            GUI.Box(r, GUIContent.none, Ui.Panel);
            GUI.Label(new Rect(r.x + 64, r.y + 8, r.width - 74, 24), $"<color=#7fe0ff>?</color>  Something to find: {Ui.Dist(lmDist)}", Ui.Bold);
            GUI.Label(new Rect(r.x + 64, r.y + 34, r.width - 74, 22), lmDist < lmTarget.reach ? "You're there: land to find out what it is" : $"Land within {Ui.Dist(lmTarget.reach)} of the blue beam", Ui.Small);
            float a = -lmBearing - camYaw + 180f;
            var c0 = new Vector2(r.x + 32, r.y + 33);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(a, c0 * Ui.Scale);
            GUI.Label(new Rect(c0.x - 20, c0.y - 22, 40, 40), "▲", Ui.BigCenter);
            GUI.matrix = m;
        }

        // ---------------------------------------------------------------- dust, prints and tracks

        Vector3d? lastPrint;
        bool printLeft;
        readonly Dictionary<int, Vector3d> lastTrack = new Dictionary<int, Vector3d>();

        BodyView ViewOf(CelestialBody b) => root.bodyViews.FirstOrDefault(x => x != null && x.body == b);

        void UpdateGroundFx()
        {
            var v = active;
            var b = v.body;
            bool solid = !v.destroyed && !b.isStar && !b.IsGasGiant;
            var bv = solid ? ViewOf(b) : null;

            // engine dust
            float strength = 0f;
            Vector3 at = Vector3.zero, upF = Vector3.up;
            Color col = Color.white;
            bool air = b.HasAtmosphere && b.PressureAtm(v.Altitude) > 0.005;
            float size = Mathf.Max(1.5f, v.Length * 0.25f);
            if (solid && v.thrustNow > 0 && !mapView && v.hasLaunched)
            {
                double radar = v.RadarAltitude(ut);
                double reach = 12 + 3 * size;
                if (radar < reach)
                {
                    double g = b.mu / v.pos.sqrMagnitude;
                    float twr = (float)(v.thrustNow / System.Math.Max(1e-6, v.mass * g));
                    strength = Mathf.Clamp01(twr * 0.8f) * (float)(1 - radar / reach);
                    Vector3d upD = renderRel.normalized;
                    Vector3d fixedDir = b.InertialToFixed(upD, renderUt).normalized;
                    double surf = LaunchSite.SurfaceHeight(b, fixedDir), terr = Terrain.Height(b, fixedDir);
                    if (surf - terr > 0.05) strength = 0f;       // pads and runways: concrete (the countdown has its steam)
                    bool water = Terrain.IsWater(b, fixedDir) && surf <= 0.5;
                    col = water ? new Color(0.92f, 0.95f, 1f) : Color.Lerp(MeshFactory.BodyColor(b, fixedDir.ToFloat()), Color.white, 0.15f);
                    at = (upD * (b.radius + System.Math.Max(surf, 0)) - renderRel).ToFloat();
                    upF = upD.ToFloat();
                    if (water) air = true;
                }
            }
            GroundFx.Dust(strength, at, upF, col, air, size);

            // boot prints and tyre tracks
            bool onGround = v.landed || (v.WheelsDown && v.RadarAltitude(ut) < 0.5);
            if (solid && onGround && bv != null)
            {
                Vector3d f = b.InertialToFixed(v.pos, ut);
                Vector3d fd = f.normalized;
                bool onStructure = LaunchSite.SurfaceHeight(b, fd) - Terrain.Height(b, fd) > 0.05 || Terrain.IsWater(b, fd);
                if (v.IsEva)
                {
                    if (lastPrint.HasValue && (f - lastPrint.Value).magnitude > 0.42)
                    {
                        Vector3d fwd = f - lastPrint.Value;
                        fwd -= fd * Vector3d.Dot(fwd, fd);
                        if (!onStructure && fwd.sqrMagnitude > 1e-6)
                        {
                            Vector3d side = Vector3d.Cross(fd, fwd.normalized) * (printLeft ? 0.11 : -0.11);
                            Vector3d pd = (f + side).normalized;
                            GroundFx.Add(b, pd, bv.DrawnHeight(pd), bv.PatchVersion, fwd, 0.3f, 0.13f, GroundFx.MarkColor(b, pd), true);
                            printLeft = !printLeft;
                        }
                        lastPrint = f;
                    }
                    else if (!lastPrint.HasValue) lastPrint = f;
                }
                else
                {
                    lastPrint = null;
                    foreach (var p in v.parts)
                    {
                        if (p.def.kind != PartKind.Wheel || p.broken) continue;
                        Vector3 local = p.localPos + p.localRot * new Vector3(p.def.strutLength, 0, 0) - v.com;
                        Vector3d w = b.InertialToFixed(v.pos + Vector3d.Rotate(v.rot, Vector3d.FromFloat(local)), ut);
                        Vector3d wd = w.normalized;
                        if (w.magnitude - (b.radius + Terrain.Height(b, wd)) > p.def.wheelRadius + 0.4) { lastTrack.Remove(p.index); continue; }   // in the air
                        if (lastTrack.TryGetValue(p.index, out var prev))
                        {
                            Vector3d step = w - prev;
                            if (step.magnitude > 0.5)
                            {
                                Vector3d mid = ((w + prev) * 0.5).normalized;
                                if (!onStructure && step.magnitude < 6)
                                {
                                    Color tc = GroundFx.MarkColor(b, mid);
                                    if (lastTrack.Count % 2 == 0) tc *= 0.92f;
                                    GroundFx.Add(b, mid, bv.DrawnHeight(mid), bv.PatchVersion, step, (float)step.magnitude + 0.04f, p.def.wheelRadius * 0.75f, tc, false);
                                }
                                lastTrack[p.index] = w;
                            }
                        }
                        else lastTrack[p.index] = w;
                    }
                }
            }
            else { lastPrint = null; lastTrack.Clear(); }
            GroundFx.Sync(b, renderUt, originAbs, bv != null ? (System.Func<Vector3d, double>)bv.DrawnHeight : null, bv != null ? bv.PatchVersion : 0);
        }

        // ---------------------------------------------------------------- weather

        string weatherLine;
        float weatherTimer;

        void UpdateWeatherLine(float dt)
        {
            weatherTimer -= dt;
            if (weatherTimer > 0) return;
            weatherTimer = 1f;
            weatherLine = null;
            var v = active;
            if (v == null || v.destroyed || !Weather.On || !Weather.Has(v.body) || v.Altitude > 20_000) return;
            Vector3d up = v.body.InertialToFixed(v.pos, ut).normalized;
            weatherLine = Weather.At(v.body, up, ut).Describe(up);
        }

        void DrawWeatherLine(float W, float H)
        {
            if (weatherLine == null || mapView) return;
            var r = new Rect(W * 0.5f - 170, H - 292, 340, 24);
            GUI.Box(r, GUIContent.none, Ui.Panel);
            GUI.Label(new Rect(r.x + 10, r.y + 2, r.width - 20, 20), weatherLine, new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter });
        }

        float windHoldUntil;

        /// <summary>Starting a countdown: holds once if the wind at the pad is over the limit (SPACE again goes anyway).</summary>
        bool WindHold(int pad)
        {
            if (!Weather.On || Time.unscaledTime < windHoldUntil) return false;
            var w = Weather.At(Home, LaunchSite.PadDirection(Home, pad), ut);
            if (w.windSpeed <= Weather.LaunchWindLimit) return false;
            windHoldUntil = Time.unscaledTime + 8f;
            Show($"Countdown held: wind {w.windSpeed:0} m/s at the pad (the limit is {Weather.LaunchWindLimit:0}). SPACE again to launch anyway.", 6f);
            return true;
        }

        void ClearExplore()
        {
            if (lmBeam != null) lmBeam.SetActive(false);
            lmTarget = null;
            lastPrint = null;
            lastTrack.Clear();
            GroundFx.StopDust();
            WeatherFx.Stop();
        }
    }
}
