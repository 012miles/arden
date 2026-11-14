using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    public partial class FlightMode
    {
        static readonly Color TrajColor = new Color(0.35f, 0.85f, 1f, 0.95f);
        static readonly Color TrajColor2 = new Color(0.95f, 0.45f, 0.95f, 0.95f);
        static readonly Color PlanColor = new Color(1f, 0.6f, 0.15f, 0.95f);
        static readonly Color MoonPathColor = new Color(0.6f, 0.6f, 0.65f, 0.5f);

        public CelestialBody mapFocus;
        float mapYaw = 20f, mapPitch = 30f, mapDist = 2_500_000f;
        const float MaxMapDist = 8e10f;
        OrbitLine[] trajLines, planLines;
        readonly Dictionary<CelestialBody, OrbitLine> bodyPaths = new Dictionary<CelestialBody, OrbitLine>();
        readonly List<(Vector3 world, double t)> pickSamples = new List<(Vector3, double)>();
        readonly List<Vector3> pts = new List<Vector3>();
        int dragHandle = -1; // 0..5 dv handles, 6 = node time
        Vector2 dragLast;
        double closestDist = double.NaN, closestTime, closestRel;
        double plannedClosestDist = double.NaN, plannedClosestTime, plannedClosestRel;
        float closestTimer;
        static readonly Color VesselPathColor = new Color(0.55f, 0.9f, 0.55f, 0.7f);
        static readonly Color TargetPathColor = new Color(0.85f, 0.5f, 1f, 0.9f);
        static readonly Color ClosestColor = new Color(1f, 0.55f, 0.85f);
        readonly List<OrbitLine> vesselLines = new List<OrbitLine>();

        void EnsureMapObjects()
        {
            if (trajLines != null) return;
            trajLines = new OrbitLine[3];
            planLines = new OrbitLine[3];
            for (int i = 0; i < 3; i++)
            {
                trajLines[i] = new OrbitLine(i == 0 ? TrajColor : TrajColor2);
                planLines[i] = new OrbitLine(PlanColor);
            }
            foreach (var b in root.bodies)
                if (b.parent != null) bodyPaths[b] = new OrbitLine(MoonPathColor);
        }

        void HideMapObjects()
        {
            if (trajLines == null) return;
            foreach (var l in trajLines) l.Hide();
            foreach (var l in planLines) l.Hide();
            foreach (var l in bodyPaths.Values) l.Hide();
            foreach (var l in vesselLines) l.Hide();
            foreach (var l in commLines) l.Hide();
            foreach (var l in netLines) l.Hide();
            foreach (var o in oreOverlays.Values) o.Hide();
            foreach (var o in regionOverlays.Values) o.Hide();
        }

        public bool showOreMap, showRegionMap, showCommMap;
        readonly Dictionary<CelestialBody, OreOverlay> oreOverlays = new Dictionary<CelestialBody, OreOverlay>();
        readonly Dictionary<CelestialBody, RegionOverlay> regionOverlays = new Dictionary<CelestialBody, RegionOverlay>();

        /// <summary>O: cycles the map layers: the ore map (what survey scanners have mapped), the regions, the comms network, then off.</summary>
        public void ToggleOreMap()
        {
            if (!showOreMap && !showRegionMap && !showCommMap) SetMapLayer(true, false);
            else if (showOreMap) SetMapLayer(false, true);
            else if (showRegionMap) SetMapLayer(false, false, true);
            else SetMapLayer(false, false);
        }

        public void SetMapLayer(bool ore, bool regions, bool comms = false)
        {
            showOreMap = ore;
            showRegionMap = regions;
            showCommMap = comms;
            if ((ore || regions || comms) && !mapView) mapView = true;
            Show(ore ? "Ore map: yellow-red = more ore, dark = unmapped" : regions ? "Region map: science counts per region"
                : comms ? "Comms map: every link the network has now" : "Map layers off", 3f);
        }

        readonly List<OrbitLine> netLines = new List<OrbitLine>();

        /// <summary>
        /// The comms map: every working link between relays, the active vessel and the ground stations (the launch site
        /// station and the two deep space stations), coloured by strength.
        /// </summary>
        void DrawCommNetwork()
        {
            int k = 0;
            if (showCommMap)
            {
                var home = root.home;
                var nodes = new List<(Vector3d pos, double power, int kind)>(); // kind 0 vessel, 1 relay, 2 ground, 3 deep ground
                double p0 = CommNet.PowerOf(active);
                if (p0 > 0) nodes.Add((active.AbsolutePosition(ut), p0, 0));
                foreach (var v in U.vessels) if (v != active && CommNet.IsRelay(v)) nodes.Add((v.AbsolutePosition(ut), CommNet.PowerOf(v), 1));
                nodes.Add((CommNet.GroundStation(home, ut), CommNet.GroundPower, 2));
                foreach (var d in CommNet.DeepSpaceStations(home, ut)) nodes.Add((d, CommNet.GroundPower, 3));
                bool Far(Vector3d p) => (p - home.AbsolutePosition(ut)).magnitude > home.SoiRadius;
                for (int i = 0; i < nodes.Count; i++)
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        var a = nodes[i]; var b = nodes[j];
                        if (a.kind >= 2 && b.kind >= 2) continue; // stations don't talk to each other here
                        if ((a.kind == 3 && !Far(b.pos)) || (b.kind == 3 && !Far(a.pos))) continue;
                        double s = CommNet.Strength((a.pos - b.pos).magnitude, CommNet.Range(a.power, b.power));
                        if (s <= 0 || !CommNet.LineOfSight(a.pos, b.pos, root.bodies, ut)) continue;
                        if (k >= netLines.Count) netLines.Add(new OrbitLine(Color.green));
                        var line = netLines[k++];
                        line.SetColor(s > 0.5 ? new Color(0.4f, 1f, 0.5f, 0.6f) : s > 0.2 ? new Color(1f, 0.85f, 0.3f, 0.6f) : new Color(1f, 0.4f, 0.3f, 0.6f));
                        pts.Clear();
                        pts.Add((a.pos - originAbs).ToFloat());
                        pts.Add((b.pos - originAbs).ToFloat());
                        line.Show(pts, mapDist * 0.0022f);
                    }
            }
            for (; k < netLines.Count; k++) netLines[k].Hide();
        }


        void DrawRegionOverlays()
        {
            foreach (var b in root.bodies)
            {
                if (b.isStar) continue; // no regions on the sun
                if (!regionOverlays.TryGetValue(b, out var ov))
                {
                    if (!showRegionMap) continue;
                    regionOverlays[b] = ov = new RegionOverlay(b, b.IsPlanet ? 64 : 48);
                }
                if (!showRegionMap) { ov.Hide(); continue; }
                ov.Show((b.AbsolutePosition(renderUt) - originAbs).ToFloat(), b.RotationAt(renderUt));
            }
        }

        void DrawOreOverlays()
        {
            foreach (var b in root.bodies)
            {
                if (b.isStar) continue; // nor ore
                if (!oreOverlays.TryGetValue(b, out var ov))
                {
                    if (!showOreMap) continue;
                    oreOverlays[b] = ov = new OreOverlay(b, b.IsPlanet ? 48 : 36);
                }
                if (!showOreMap) { ov.Hide(); continue; }
                ov.Show(U.survey, (b.AbsolutePosition(renderUt) - originAbs).ToFloat(), b.RotationAt(renderUt));
            }
        }

        readonly List<OrbitLine> commLines = new List<OrbitLine>();

        /// <summary>The active vessel's link home, hop by hop: green strong, yellow weak, red barely there.</summary>
        void DrawCommLines()
        {
            int k = 0;
            if (commLink != null && commLink.Connected)
                for (int i = 0; i + 1 < commLink.points.Count; i++)
                {
                    if (k >= commLines.Count) commLines.Add(new OrbitLine(Color.green));
                    var line = commLines[k++];
                    double s = i < commLink.strengths.Count ? commLink.strengths[i] : 1;
                    line.SetColor(s > 0.5 ? new Color(0.4f, 1f, 0.5f, 0.85f) : s > 0.2 ? new Color(1f, 0.85f, 0.3f, 0.85f) : new Color(1f, 0.4f, 0.3f, 0.85f));
                    pts.Clear();
                    Vector3 a = i == 0 ? Vector3.zero : (commLink.points[i] - originAbs).ToFloat();
                    pts.Add(a);
                    pts.Add((commLink.points[i + 1] - originAbs).ToFloat());
                    line.Show(pts, mapDist * 0.0035f);
                }
            for (; k < commLines.Count; k++) commLines[k].Hide();
        }

        /// <summary>Other vessels worth an orbit line: not debris (unless targeted), not landed, not in the air.</summary>
        bool ShowsOrbit(VesselSim u) => u != active && !u.destroyed && !u.landed && (!u.isDebris || u == targetVessel) && u.Altitude > u.RailsFloor;

        /// <summary>When a vessel on an open path (a wanderer passing through, say) leaves the sphere of influence.</summary>
        double TimeToLeave(Orbit o, double soi)
        {
            double lo = ut, hi = ut + 3600;
            double tp = o.TimeToPeriapsis(ut);
            if (!double.IsNaN(tp) && tp > 0) { lo = ut + tp; hi = lo + 3600; }
            for (int i = 0; i < 40 && o.RadiusAt(hi) < soi; i++) { lo = hi; hi += (hi - ut) * 1.5 + 3600; }
            for (int i = 0; i < 50; i++) { double mid = (lo + hi) / 2; if (o.RadiusAt(mid) < soi) lo = mid; else hi = mid; }
            return hi;
        }

        void DrawVesselOrbits()
        {
            int k = 0;
            float width = mapDist * 0.0022f;
            foreach (var u in U.vessels)
            {
                if (!ShowsOrbit(u)) continue;
                if (k >= vesselLines.Count) vesselLines.Add(new OrbitLine(VesselPathColor));
                var line = vesselLines[k++];
                line.SetColor(u == targetVessel ? TargetPathColor : VesselPathColor);
                var o = Orbit.FromState(u.pos, u.vel, u.body.mu, ut);
                double span = o.IsElliptic && o.Apoapsis < u.body.SoiRadius ? o.Period : TimeToLeave(o, u.body.SoiRadius) - ut;
                var samples = o.SamplePath(ut, ut + span, o.IsElliptic ? 240 : 320);
                Vector3d anchor = u.body.AbsolutePosition(renderUt);
                pts.Clear();
                foreach (var (p, _) in samples)
                {
                    double r = p.magnitude;
                    if (r > u.body.SoiRadius) break;
                    pts.Add((anchor + p - originAbs).ToFloat());
                }
                if (pts.Count > 1) line.Show(pts, width); else line.Hide();
            }
            for (; k < vesselLines.Count; k++) vesselLines[k].Hide();
        }

        public void SetMapView(CelestialBody focus, float dist, float yaw = 20f, float pitch = 30f)
        {
            mapFocus = focus;
            mapDist = dist;
            mapYaw = yaw;
            mapPitch = pitch;
        }

        void CycleMapFocus()
        {
            int i = root.bodies.IndexOf(mapFocus);
            mapFocus = root.bodies[(i + 1) % root.bodies.Count];
            mapDist = (float)(mapFocus.radius * (mapFocus.IsPlanet || mapFocus.isStar ? 3.4 : 6));
            Show($"Map focus: {mapFocus.name}");
        }

        /// <summary>Where a segment is drawn: around its body's current position, or the moon's position at closest approach for future flybys.</summary>
        Vector3d SegmentAnchor(TrajectorySegment s)
        {
            if (s.body.parent == null || s.body.IsHomeWorld) return s.body.AbsolutePosition(renderUt); // the sun, or home (always at the origin)
            if (s.body == active.body) return s.body.AbsolutePosition(renderUt);
            double tPe = s.tStart;
            if (!s.orbit.IsElliptic || s.orbit.TimeToPeriapsis(s.tStart) < s.tEnd - s.tStart)
                tPe = s.tStart + (s.orbit.IsElliptic ? s.orbit.TimeToPeriapsis(s.tStart) : Math.Max(0, s.orbit.TimeToPeriapsis(s.tStart)));
            if (double.IsNaN(tPe)) tPe = s.tStart;
            return s.body.AbsolutePosition(tPe);
        }

        void DrawSegments(List<TrajectorySegment> segs, OrbitLine[] lines, bool collectPick)
        {
            float width = mapDist * 0.0032f;
            for (int i = 0; i < lines.Length; i++)
            {
                if (i >= segs.Count) { lines[i].Hide(); continue; }
                var s = segs[i];
                // A path round the sun is millions of km across: zoomed in near a planet it would only cut the view in two.
                if (s.body.isStar && active.body != s.body && mapDist < 1e9f) { lines[i].Hide(); continue; }
                double t0 = s.tStart, t1 = s.tEnd;
                if (s.end == SegmentEnd.None && s.orbit.IsElliptic) t1 = t0 + s.orbit.Period;
                var samples = s.orbit.SamplePath(t0, t1, 300);
                Vector3d anchor = SegmentAnchor(s);
                pts.Clear();
                foreach (var (p, t) in samples)
                {
                    Vector3 w = (anchor + p - originAbs).ToFloat();
                    pts.Add(w);
                    if (collectPick && i == 0) pickSamples.Add((w, t));
                }
                lines[i].Show(pts, width);
            }
        }

        void UpdateMap()
        {
            if (trajLines == null) return;
            if (active != null) UpdateClosestApproach();
            if (!mapView || active == null) { HideMapObjects(); return; }
            pickSamples.Clear();
            DrawSegments(trajectory, trajLines, true);
            if (node != null && node.valid) DrawSegments(node.planned, planLines, false);
            else foreach (var l in planLines) l.Hide();

            foreach (var kv in bodyPaths)
            {
                var b = kv.Key;
                // A planet's path round the sun only when zoomed out to the sun's scale (up close its long chords miss it).
                if (b.IsPlanet && mapDist < b.SoiRadius * 3) { kv.Value.Hide(); continue; }
                pts.Clear();
                Vector3d c = b.parent.AbsolutePosition(renderUt);
                for (int i = 0; i <= 180; i++)
                {
                    double th = 2 * Math.PI * i / 180;
                    pts.Add((c + (b.orbitX * Math.Cos(th) - b.orbitZ * Math.Sin(th)) * b.orbitRadius - originAbs).ToFloat());
                }
                kv.Value.Show(pts, mapDist * (b.IsPlanet ? 0.003f : 0.002f));
            }

            DrawVesselOrbits();
            DrawCommLines();
            DrawCommNetwork();
            DrawOreOverlays();
            DrawRegionOverlays();
        }

        /// <summary>Closest approach to the target (a moon, or a vessel orbiting the same body) on the current path.</summary>
        void UpdateClosestApproach()
        {
            closestTimer -= Time.unscaledDeltaTime;
            if (closestTimer > 0) return;
            closestTimer = 0.4f;
            closestDist = double.NaN;
            plannedClosestDist = double.NaN;
            if (trajectory.Count == 0) return;
            // After a planned maneuver, too: that's how you set up a rendezvous.
            if (targetVessel != null && !targetVessel.landed && node != null && node.valid && node.planned.Count > 0 && TargetOrbit(out var pb, out var po))
            {
                var ps = node.planned.Find(s => s.body == pb);
                double pspan = Math.Max(ps?.orbit.IsElliptic == true ? ps.orbit.Period : 3600, po.IsElliptic ? po.Period : OpenSpan(po, pb)) * 1.05;
                if (ps != null && Patcher.ClosestApproach(ps, pb, po, node.ut, pspan, out double pd, out double pt, out double prel))
                { plannedClosestDist = pd; plannedClosestTime = pt; plannedClosestRel = prel; }
            }
            if (target != null && !trajectory.Exists(s => s.body == target))
            {
                Patcher.ClosestApproach(trajectory[0], target, out double d, out double t);
                if (!double.IsInfinity(d)) { closestDist = d; closestTime = t; closestRel = 0; }
            }
            else if (targetVessel != null && !targetVessel.landed && TargetOrbit(out var tb, out var to))
            {
                var seg = trajectory.Find(s => s.body == tb);
                double span = Math.Max(seg?.orbit.IsElliptic == true ? seg.orbit.Period : 3600, to.IsElliptic ? to.Period : OpenSpan(to, tb)) * 1.05;
                if (seg != null && Patcher.ClosestApproach(seg, tb, to, ut, span, out double d, out double t, out double rel))
                { closestDist = d; closestTime = t; closestRel = rel; }
            }
        }

        /// <summary>How far ahead to look for a target on an open path: until it leaves (two days at most).</summary>
        double OpenSpan(Orbit o, CelestialBody b) => Math.Max(3600, Math.Min(TimeToLeave(o, b.SoiRadius) - ut, 2 * 86400));

        bool TargetOrbit(out CelestialBody b, out Orbit o)
        {
            b = targetVessel.body;
            o = Orbit.FromState(targetVessel.pos, targetVessel.vel, b.mu, ut);
            return true;
        }

        // ---------------------------------------------------------------- overlay (IMGUI)

        Vector2? ToGui(Vector3 world)
        {
            Vector3 sp = root.farCam.WorldToScreenPoint(world);
            if (sp.z <= 0) return null;
            return new Vector2(sp.x / Ui.Scale, (Screen.height - sp.y) / Ui.Scale);
        }

        bool Marker(Vector3 world, string label, Color c, bool clickable = false)
        {
            var g = ToGui(world);
            if (!g.HasValue) return false;
            var p = g.Value;
            if (p.x < -10 || p.y < -10 || p.x > Ui.W + 10 || p.y > Ui.H + 10) return false;   // off the screen: nothing to draw (or click)
            if (Ui.OverPanel(new Rect(p.x - 4, p.y - 4, 8, 8))) return false; // hidden behind a HUD panel
            Ui.Line(new Rect(p.x - 4, p.y - 4, 8, 8), c);
            // Labels that would land on one already drawn step down underneath it; past a few, only the dot shows.
            float w = Mathf.Min(clickable ? 200f : 260f, Ui.Bold.CalcSize(new GUIContent(label)).x + 6f);
            Rect r = default;
            bool placed = false;
            for (int k = 0; k < 10 && !placed; k++)
            {
                // right of the dot, then left of it, one row further down each time round
                float x = k % 2 == 0 ? p.x + 8 : p.x - 8 - w;
                r = new Rect(x, p.y - 10 + (k / 2) * 19, w, 20);
                placed = !mapLabels.Exists(o => o.Overlaps(r)) && !Ui.OverPanel(r);
            }
            if (!placed) return false;
            mapLabels.Add(r);
            var old = GUI.color;
            GUI.color = c;
            bool clicked = false;
            if (clickable) clicked = Ui.Press(new Rect(r.x, r.y - 1, w, 22), label, Ui.Bold);
            else Ui.Text(r, label, Ui.Bold);
            GUI.color = old;
            return clicked;
        }

        readonly List<Rect> mapLabels = new List<Rect>();

        void DrawMapOverlay()
        {
            float W = Ui.W;
            mapLabels.Clear();
            // Bodies (click a moon to target it)
            foreach (var b in root.bodies)
            {
                Vector3 w = (b.AbsolutePosition(renderUt) - originAbs).ToFloat();
                bool isTarget = target == b;
                // Zoomed out so far that a moon sits on top of its planet: leave its name off (unless it's the target or home)
                if (!isTarget && b.parent != null && !b.parent.isStar && b != active.body)
                {
                    var gm = ToGui(w); var gp = ToGui((b.parent.AbsolutePosition(renderUt) - originAbs).ToFloat());
                    if (gm.HasValue && gp.HasValue && (gm.Value - gp.Value).magnitude < 26f) continue;
                }
                string lbl = b.name + (isTarget ? "  [target]" : (b.parent != null && b != active.body ? "  (click to target)" : ""));
                if (Marker(w + Vector3.up * (float)b.radius * 1.15f, lbl, isTarget ? new Color(0.8f, 0.45f, 1f) : Color.white, b.parent != null && b != active.body))
                    SetTarget(b);
            }
            Marker(Vector3.zero, active.name, new Color(1f, 0.85f, 0.3f));

            // Milestone 27: places spotted on the world in focus: a "?" until you land beside one, then its name
            if (mapFocus != null && !mapFocus.isStar && !mapFocus.IsGasGiant)
            {
                Vector3 bc = (mapFocus.AbsolutePosition(renderUt) - originAbs).ToFloat();
                Vector3 camPos = root.farCam.transform.position;
                foreach (var l in Landmarks.On(mapFocus))
                {
                    if (!U.career.spotted.Contains(l.id)) continue;
                    Vector3d p = mapFocus.AbsolutePosition(renderUt) + mapFocus.FixedToInertial(l.dir * (mapFocus.radius + Terrain.Height(mapFocus, l.dir)), renderUt);
                    Vector3 w = (p - originAbs).ToFloat();
                    if (Vector3.Dot((w - bc).normalized, camPos - w) < 0) continue;     // round the far side
                    bool found = U.career.visited.Contains(l.id);
                    Marker(w, found ? l.name : "?", found ? new Color(0.55f, 1f, 0.65f) : new Color(0.5f, 0.9f, 1f));
                }
                // milestone 34: outposts and wrecks to scavenge, always shown (greyed once searched)
                foreach (var l in Outposts.On(mapFocus))
                {
                    Vector3d p = mapFocus.AbsolutePosition(renderUt) + mapFocus.FixedToInertial(l.dir * (mapFocus.radius + Terrain.Height(mapFocus, l.dir)), renderUt);
                    Vector3 w = (p - originAbs).ToFloat();
                    if (Vector3.Dot((w - bc).normalized, camPos - w) < 0) continue;
                    bool searched = U.career.scavenged.Contains(l.id);
                    Marker(w, $"⌂ {l.name}", searched ? new Color(0.6f, 0.62f, 0.65f) : new Color(1f, 0.78f, 0.35f));
                }
            }

            // Apsides and events along your path come before other vessels' names
            for (int i = 0; i < trajectory.Count; i++)
            {
                var s = trajectory[i];
                var o = s.orbit;
                Vector3d anchor = SegmentAnchor(s);
                string bn = s.body.name;
                double peT = s.tStart + (o.IsElliptic ? o.TimeToPeriapsis(s.tStart) : Math.Max(0, o.TimeToPeriapsis(s.tStart)));
                if (!double.IsNaN(peT) && peT <= s.tEnd && o.Periapsis > s.body.radius * 0.2)
                    Marker((anchor + o.P * o.Periapsis - originAbs).ToFloat(), $"{(i > 0 ? bn + " " : "")}Pe {Ui.Dist(o.Periapsis - s.body.radius)}", new Color(0.5f, 0.85f, 1f));
                if (o.IsElliptic && o.Apoapsis < s.body.SoiRadius)
                    Marker((anchor + o.P * (-o.Apoapsis) - originAbs).ToFloat(), $"Ap {Ui.Dist(o.Apoapsis - s.body.radius)}", new Color(0.5f, 0.85f, 1f));
                if (s.end == SegmentEnd.EnterSoi)
                {
                    o.StateAt(s.tEnd, out var r, out _);
                    Marker((anchor + r - originAbs).ToFloat(), $"{s.next.name} encounter in {Ui.Time(s.tEnd - ut)}", new Color(1f, 0.6f, 1f));
                    // ghost of the moon where it will be
                    Vector3d ghost = s.next.AbsolutePosition(s.tEnd);
                    Marker((ghost - originAbs).ToFloat(), $"{s.next.name} at encounter", new Color(0.75f, 0.75f, 0.8f));
                }
                else if (s.end == SegmentEnd.ExitSoi)
                {
                    o.StateAt(s.tEnd, out var r, out _);
                    Marker((anchor + r - originAbs).ToFloat(), $"Leave {bn}'s SOI in {Ui.Time(s.tEnd - ut)}", new Color(1f, 0.6f, 1f));
                }
                else if (s.end == SegmentEnd.Impact)
                {
                    o.StateAt(s.tEnd, out var r, out _);
                    Marker((anchor + r - originAbs).ToFloat(), $"Impact {bn} in {Ui.Time(s.tEnd - ut)}", new Color(1f, 0.4f, 0.3f));
                }
            }

            // Other vessels (click a name to target it). Debris labels only show when zoomed in.
            foreach (var u in U.vessels)
            {
                if (u == active || u.destroyed) continue;
                bool isT = u == targetVessel;
                if (u.isDebris && !isT && mapDist > 60_000) continue;
                Vector3 w = (u.body.AbsolutePosition(renderUt) + u.pos - originAbs).ToFloat();
                Color c = isT ? TargetPathColor : u.isDebris ? new Color(0.62f, 0.62f, 0.62f) : VesselPathColor;
                if (Marker(w, u.name + (isT ? "  [target]" : ""), c, true)) SetTarget(u);
            }

            // Where you and the target vessel will be at closest approach
            if (targetVessel != null && !double.IsNaN(closestDist) && TargetOrbit(out var tbody, out var torb))
            {
                var seg = trajectory.Find(s => s.body == tbody);
                if (seg != null)
                {
                    Vector3d anchor = tbody.AbsolutePosition(renderUt);
                    seg.orbit.StateAt(closestTime, out var rMe, out _);
                    torb.StateAt(closestTime, out var rT, out _);
                    Marker((anchor + rMe - originAbs).ToFloat(), $"Closest {Ui.Dist(closestDist)} in {Ui.Time(closestTime - ut)}", ClosestColor);
                    Marker((anchor + rT - originAbs).ToFloat(), $"{targetVessel.name} then", TargetPathColor);
                }
            }

            // The ground station last: the least important label when they crowd together
            Marker((CommNet.GroundStation(root.home, renderUt) - originAbs).ToFloat(), "Ground station", new Color(0.4f, 1f, 0.5f));

            DrawNodeGizmo();
            // Burns waiting in the queue, where they'll happen
            foreach (var ln in laterNodes)
                if (ln.valid && ln.body != null)
                    Marker((ln.body.AbsolutePosition(renderUt) + ln.pos - originAbs).ToFloat(), $"Next burn {ln.DeltaV:0} m/s in {Ui.Time(ln.ut - ut)}", new Color(0.45f, 0.75f, 1f));
            HandleMapClicks();

            // Map info panel
            string transfer = TransferLine(), aeroLine = AeroLine();
            float extra = (showOreMap ? 22 : 0) + (showRegionMap ? 44 : 0) + (showCommMap ? 22 : 0) + (transfer != null ? 24 : 0) + (aeroLine != null ? 22 : 0);
            var ir = new Rect(W / 2 - 230, Ui.H - 330 - extra, 460, 64 + extra);
            Ui.Box(ir);
            Ui.Text(new Rect(ir.x + 10, ir.y + 6, ir.width - 214, 20), $"Focus {mapFocus.name} (TAB) · click your orbit to plan a burn", Ui.Small);
            if (Ui.Press(new Rect(ir.xMax - 204, ir.y + 4, 58, 20), "Ore (O)", showOreMap ? Ui.ButtonSmallSel : Ui.ButtonSmall)) SetMapLayer(!showOreMap, false);
            if (Ui.Press(new Rect(ir.xMax - 142, ir.y + 4, 64, 20), "Regions", showRegionMap ? Ui.ButtonSmallSel : Ui.ButtonSmall)) SetMapLayer(false, !showRegionMap);
            if (Ui.Press(new Rect(ir.xMax - 74, ir.y + 4, 66, 20), "Comms", showCommMap ? Ui.ButtonSmallSel : Ui.ButtonSmall)) SetMapLayer(false, false, !showCommMap);
            if (showCommMap)
                Ui.Text(new Rect(ir.x + 10, ir.y + 50, ir.width - 20, 20),
                    "Links: <color=#66ff80>strong</color>, <color=#ffd84d>weak</color>, <color=#ff6650>faint</color>. Deep space stations track craft past 80 Mm.", Ui.Small);
            if (showRegionMap)
            {
                // Legend for the focused world: a swatch and a name per region, flowing onto a second line
                var names = Regions.Names(mapFocus);
                float lx = ir.x + 10, ly = ir.y + 50;
                for (int i = 0; i < names.Length; i++)
                {
                    float w = Ui.Small.CalcSize(new GUIContent(names[i])).x + 26;
                    if (lx + w > ir.xMax - 8) { lx = ir.x + 10; ly += 20; }
                    Ui.Line(new Rect(lx, ly + 5, 11, 11), Regions.ColorOf(mapFocus, i));
                    Ui.Text(new Rect(lx + 15, ly, w, 20), names[i], Ui.Small);
                    lx += w;
                }
            }
            if (showOreMap)
            {
                double cov = U.survey.Coverage(mapFocus);
                Vector3d under = active.body.InertialToFixed(active.pos, ut);
                string here = U.survey.IsScanned(active.body, under) ? $"under you ~{Math.Round(active.OreUnder(ut) * 100):0}%" : "under you: not mapped";
                Ui.Text(new Rect(ir.x + 10, ir.y + 50, ir.width - 20, 20),
                    $"<color=#ffd060>Ore</color> yellow-red, none blue, unmapped dark.  {mapFocus.name} {cov * 100:0}% mapped, {here}", Ui.Small);
            }
            string tgt = !HasTarget ? "No target. Click a moon's or a vessel's name to target it." :
                targetVessel != null ? (double.IsNaN(closestDist)
                    ? $"Target {targetVessel.name}: {Ui.Dist(Vector3d.Distance(TargetAbsPos(ut), active.AbsolutePosition(ut)))} away ({targetVessel.Situation(ut)})"
                    : $"Target {targetVessel.name}: closest {Ui.Dist(closestDist)} in {Ui.Time(closestTime - ut)} at {closestRel:0} m/s") :
                trajectory.Exists(s => s.body == target) ? $"Target {target.name}: encounter on this path!" :
                !double.IsNaN(closestDist) ? $"Target {target.name}: closest approach {Ui.Dist(closestDist)} in {Ui.Time(closestTime - ut)}" :
                $"Target {target.name}";
            Ui.Text(new Rect(ir.x + 10, ir.y + 28, ir.width - 20, 20), tgt, Ui.Label);
            if (aeroLine != null)
            {
                // The aerobraking forecast for the next dip into an atmosphere
                float ay = ir.yMax - (transfer != null ? 46 : 24);
                Color ac = aero.outcome == AeroOutcome.Captured || aero.outcome == AeroOutcome.Lands ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.6f, 0.45f);
                var old = GUI.color; GUI.color = ac;
                Ui.Text(new Rect(ir.x + 10, ay, ir.width - 20, 20), aeroLine, Ui.Small);
                GUI.color = old;
            }
            if (transfer != null)
            {
                // The transfer planner: target another planet while orbiting yours
                bool aeroBtn = CruiseTarget && target.HasAtmosphere;
                Ui.Text(new Rect(ir.x + 10, ir.yMax - 24, ir.width - (aeroBtn ? 222 : 132), 20), transfer, Ui.Small);
                if (TransferButton != null && Ui.Press(new Rect(ir.xMax - 118, ir.yMax - 25, 110, 20), TransferButton, Ui.ButtonSmall)) TransferButtonPressed();
                if (aeroBtn && Ui.Press(new Rect(ir.xMax - 208, ir.yMax - 25, 86, 20), "Aerocapture", Ui.ButtonSmall)) PlanAerocapture();
            }
        }

        Vector3 NodeWorld()
        {
            var s = trajectory.Find(x => node.ut >= x.tStart && node.ut <= x.tEnd) ?? (trajectory.Count > 0 ? trajectory[0] : null);
            Vector3d anchor = s != null ? SegmentAnchor(s) : node.body.AbsolutePosition(renderUt);
            return (anchor + node.pos - originAbs).ToFloat();
        }

        (Vector2 center, Vector2[] dirs) NodeHandles()
        {
            var dirs = new Vector2[6];
            Vector3 nw = NodeWorld();
            var c = ToGui(nw);
            if (!c.HasValue) return (Vector2.zero, null);
            ManeuverNode.Frame(node.pos, node.vel, out var pro, out var nor, out var rad);
            Vector3d[] world = { pro, -pro, nor, -nor, rad, -rad };
            float scale = mapDist * 0.05f;
            for (int i = 0; i < 6; i++)
            {
                var e = ToGui(nw + world[i].ToFloat() * scale);
                Vector2 d = e.HasValue ? e.Value - c.Value : Vector2.zero;
                dirs[i] = d.sqrMagnitude > 4f ? d.normalized : Vector2.zero;
            }
            return (c.Value, dirs);
        }

        static readonly MeshFactory.MarkerKind[] HandleKinds =
        {
            MeshFactory.MarkerKind.Prograde, MeshFactory.MarkerKind.Retrograde, MeshFactory.MarkerKind.Normal,
            MeshFactory.MarkerKind.AntiNormal, MeshFactory.MarkerKind.RadialOut, MeshFactory.MarkerKind.RadialIn,
        };

        void DrawNodeGizmo()
        {
            if (node == null || !node.valid) return;
            var (c, dirs) = NodeHandles();
            if (dirs == null) return;
            Ui.Line(new Rect(c.x - 7, c.y - 7, 14, 14), new Color(0.3f, 0.55f, 1f));
            Ui.Text(new Rect(c.x + 10, c.y + 6, 220, 20), $"dv {node.DeltaV:0.0} m/s  in {Ui.Time(node.ut - ut)}", Ui.Small);
            for (int i = 0; i < 6; i++)
            {
                if (dirs[i] == Vector2.zero) continue;
                Vector2 h = c + dirs[i] * 46f;
                Color col = MeshFactory.MarkerColor(HandleKinds[i]);
                for (int k = 1; k < 8; k++) Ui.Line(new Rect(c.x + dirs[i].x * k * 5 - 1, c.y + dirs[i].y * k * 5 - 1, 2, 2), col);
                Ui.Line(new Rect(h.x - 7, h.y - 7, 14, 14), dragHandle == i ? Color.white : col);
            }
        }

        void HandleMapClicks()
        {
            var e = Event.current;
            if (e.type == EventType.MouseUp && e.button == 0) { dragHandle = -1; return; }
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (Ui.MouseOverUI(In.MousePosition)) return;
                Vector2 m = e.mousePosition;
                if (node != null && node.valid)
                {
                    var (c, dirs) = NodeHandles();
                    if (dirs != null)
                    {
                        for (int i = 0; i < 6; i++)
                            if (dirs[i] != Vector2.zero && (c + dirs[i] * 46f - m).magnitude < 12f) { dragHandle = i; dragLast = m; e.Use(); return; }
                        if ((c - m).magnitude < 10f) { dragHandle = 6; dragLast = m; e.Use(); return; }
                    }
                }
                double t = NearestPathTime(m, 14f);
                if (!double.IsNaN(t))
                {
                    if (node == null) AddNode(t); else { node.ut = Math.Max(ut + 5, t); node.Update(trajectory); }
                    e.Use();
                }
            }
            if (e.type == EventType.MouseDrag && e.button == 0 && dragHandle >= 0 && node != null)
            {
                Vector2 m = e.mousePosition, delta = m - dragLast;
                dragLast = m;
                if (dragHandle == 6)
                {
                    double t = NearestPathTime(m, 1e9f);
                    if (!double.IsNaN(t)) node.ut = Math.Max(ut + 5, t);
                }
                else
                {
                    var (_, dirs) = NodeHandles();
                    if (dirs != null && dirs[dragHandle] != Vector2.zero)
                    {
                        double rate = e.shift ? 0.05 : 0.5;
                        double change = Vector2.Dot(delta, dirs[dragHandle]) * rate;
                        switch (dragHandle)
                        {
                            case 0: node.prograde += change; break;
                            case 1: node.prograde -= change; break;
                            case 2: node.normal += change; break;
                            case 3: node.normal -= change; break;
                            case 4: node.radial += change; break;
                            case 5: node.radial -= change; break;
                        }
                    }
                }
                node.Update(trajectory);
                e.Use();
            }
        }

        double NearestPathTime(Vector2 mouse, float maxPx)
        {
            double best = double.NaN;
            float bestD = maxPx;
            foreach (var (w, t) in pickSamples)
            {
                if (t < ut + 5) continue;
                var g = ToGui(w);
                if (!g.HasValue) continue;
                float d = (g.Value - mouse).magnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }
    }
}
