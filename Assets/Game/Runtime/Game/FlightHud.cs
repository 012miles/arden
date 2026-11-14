using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class FlightMode
    {
        static readonly (SasMode mode, string label)[] SasButtons =
        {
            (SasMode.Stability, "Hold"), (SasMode.Prograde, "Pro"), (SasMode.Retrograde, "Retro"),
            (SasMode.Normal, "Norm"), (SasMode.AntiNormal, "Anti-N"), (SasMode.RadialOut, "Rad+"),
            (SasMode.RadialIn, "Rad-"), (SasMode.Maneuver, "Mnv"), (SasMode.Target, "Tgt"), (SasMode.AntiTarget, "Anti-T"),
        };

        readonly List<(MeshFactory.MarkerKind, Vector3d)> markerScratch = new List<(MeshFactory.MarkerKind, Vector3d)>();
        readonly List<(string, string)> telemetry = new List<(string, string)>();
        readonly List<string> ctlLines = new List<string>();
        readonly List<(SasMode, string)> sasScratch = new List<(SasMode, string)>();

        /// <summary>The key list shows for the first 25 s of a flight, then tucks away until F1 (milestone 24).</summary>
        bool HelpVisible => showHelp || Time.unscaledTime < helpUntil;
        float helpUntil;

        /// <summary>Extra telemetry rows ("More"): on by default for engineers.</summary>
        static int hudMore = -2;
        static bool HudMore
        {
            get { if (hudMore == -2) hudMore = PlayerPrefs.GetInt("hudMore", -1); return hudMore < 0 ? Words.Engineer : hudMore == 1; }
            set { hudMore = value ? 1 : 0; PlayerPrefs.SetInt("hudMore", hudMore); PlayerPrefs.Save(); }
        }

        static string SasLabel(SasMode m, string eng)
        {
            if (Words.Engineer) return eng;
            if (Words.Easy) return m == SasMode.Stability ? "Steady" : m == SasMode.Prograde ? "Forward" : m == SasMode.Retrograde ? "Backward" : eng;
            switch (m)
            {
                case SasMode.Prograde: return "Fwd";
                case SasMode.Retrograde: return "Back";
                case SasMode.RadialOut: return "Out";
                case SasMode.RadialIn: return "In";
                case SasMode.Maneuver: return "Plan";
                case SasMode.AntiTarget: return "Away";
                default: return eng;
            }
        }

        /// <summary>Scenery screenshots: no HUD at all.</summary>
        /// <summary>The flight's interface hidden: the same switch as everywhere else now (Ui.Hidden, F1).</summary>
        public static bool HideHud { get => Ui.Hidden; set => Ui.Hidden = value; }

        /// <summary>Where the height-and-speed box is (the tutorial points at it).</summary>
        public Rect TelemetryRect { get; private set; }

        public void OnGUI()
        {
            if (active == null || HideHud) return;
            if (replay != null) { DrawReplay(Ui.W, Ui.H); return; }
            if (photoMode) { DrawPhoto(Ui.W, Ui.H); return; }
            if (showPractice) { DrawPractice(Ui.W, Ui.H); return; }
            if (rollout != null) { DrawRollout(); return; }
            if (inside && !mapView)
            {
                // milestone 34: in the cabin, just its prompts (and the team, and the pause menu)
                DrawInteriorHud(Ui.W, Ui.H);
                if (Coop.Active) DrawCoopFlight(Ui.W, Ui.H);
                if (paused && !showHowTo && !showBadges) DrawEndPanel("Paused", "");
                return;
            }
            DrawCountdown(Ui.W);
            DrawLandingGuide(Ui.W);
            DrawLandmarkGuide(Ui.W);
            float W = Ui.W, H = Ui.H;
            DrawDockGuide(W, H);
            DrawWeatherLine(W, H);
            DrawStory(W, H);
            if (Coop.Active) DrawCoopFlight(W, H);
            DrawScavenge(W, H);   // milestone 34: an outpost or wreck to search, in any game
            DrawInteriorHud(W, H);   // milestone 34: the cabin's prompts
            var v = active;
            var body = v.body;
            Orbit orbit = trajectory.Count > 0 ? trajectory[0].orbit : null;

            // ---- Telemetry (milestone 24: the basics, the rest under "More", in the player's words)
            double alt = v.Altitude;
            Vector3d up = v.pos.normalized;
            double vs = Vector3d.Dot(v.vel, up);
            bool easy = Words.Easy, eng = Words.Engineer, more = HudMore && !easy;
            telemetry.Clear();
            void Row(string k, string val, bool extra = false) { if (!extra || more) telemetry.Add((k, val)); }
            // Low down (where results count per region), the region underneath too
            string region = v.landed || alt < (body.HasAtmosphere ? 18_000 : 60_000) ? Regions.Under(v, ut) : null;
            Row(Words.T("Body", "World", "Where"), region != null ? $"{body.name} · {region}" : body.name);
            Row(Words.T("Altitude", "Altitude", "Height"), Ui.Dist(alt));
            double radar = alt < 60_000 ? v.RadarAltitude(ut) : double.NaN;
            if (eng) Row("Radar altitude", alt < 60_000 ? Ui.Dist(v.landed ? Math.Max(0, radar) : radar) : "--");
            else if (!v.landed && radar < 5000) Row(Words.T("", "Above ground", "Above ground"), Ui.Dist(radar));
            Row(Words.T("Vertical speed", "Climbing at", "Going"),
                easy ? (Math.Abs(vs) < 1 ? "not up or down" : vs > 0 ? $"up, {vs:0} m/s" : $"down, {-vs:0} m/s") : $"{vs:+0;-0;0} m/s");
            if (orbit != null && !(easy && v.landed))
            {
                double ap = orbit.Apoapsis - body.radius, pe = orbit.Periapsis - body.radius;
                Row(Words.T("Apoapsis", "Highest point", "Top of orbit"),
                    orbit.IsElliptic && orbit.Apoapsis < body.SoiRadius ? (easy ? Ui.Dist(ap) : $"{Ui.Dist(ap)}  in {Ui.Time(orbit.TimeToApoapsis(ut))}") : Words.T("escaping", "escaping", "flying away"));
                Row(Words.T("Periapsis", "Lowest point", "Bottom of orbit"),
                    pe < 0 ? (easy ? "<color=#ff9070>under the ground</color>" : $"<color=#ff9070>{Ui.Dist(pe)}</color>") : easy ? Ui.Dist(pe) : $"{Ui.Dist(pe)}  in {Ui.Time(orbit.TimeToPeriapsis(ut))}");
            }
            else if (!easy) { Row(Words.T("Apoapsis", "Highest point", ""), "--"); Row(Words.T("Periapsis", "Lowest point", ""), "--"); }
            Row(Words.T("Pressure", "Air pressure", "Air"), body.HasAtmosphere ? $"{body.PressureAtm(alt) * 101.3:0.00} kPa" : Words.T("vacuum", "none (vacuum)", "none"), true);
            Row(Words.T("Dyn. pressure", "Air push", "Air push"), $"{v.dynPressure / 1000:0.0} kPa", true);
            if (body.HasAtmosphere && alt < body.atmosphereHeight) Row(Words.T("Mach", "Speed of sound", ""), Words.Engineer ? $"{v.Mach:0.00}" : $"{v.Mach:0.0}x", true);
            Row(Words.T("G-force", "G-force", "G-force"), $"{v.gForce:0.0} g", true);
            Vector3d srfV = v.SurfaceRelVelocity;
            if (srfV.magnitude > 10 && body.HasAtmosphere && alt < body.atmosphereHeight && !v.HasDriveWheels)
            {
                Vector3 uL = Quaternion.Inverse(v.rot) * srfV.normalized.ToFloat();
                float aoa = Mathf.Atan2(-uL.z, uL.y) * Mathf.Rad2Deg, slip = Mathf.Atan2(uL.x, uL.y) * Mathf.Rad2Deg;
                Row(Words.T("AoA / sideslip", "Nose / side angle", ""), $"{aoa:0.0} / {slip:0.0} deg", true);
            }
            else Row(Words.T("AoA / sideslip", "Nose / side angle", ""), "--", true);
            Vector3d nose = v.Axis;
            double pitchDeg = Math.Asin(Math.Max(-1, Math.Min(1, Vector3d.Dot(nose, up)))) * 180 / Math.PI;
            Vector3d horiz = nose - up * Vector3d.Dot(nose, up);
            double hdg = Math.Atan2(Vector3d.Dot(horiz, CelestialBody.EastAt(up)), Vector3d.Dot(horiz, CelestialBody.NorthAt(up))) * 180 / Math.PI;
            if (hdg < 0) hdg += 360;
            Row(Words.T("Pitch / heading", "Pitch / heading", ""), horiz.magnitude < 0.02 ? $"{pitchDeg:0} deg / --" : $"{pitchDeg:0} deg / {hdg:000} deg", true);
            string status = v.destroyed ? $"<color=#ff7060>{Words.T("Destroyed", "Destroyed", "Broken")}</color>" : v.landed ? Words.T("Landed", "Landed", "On the ground") :
                v.WheelsDown && v.RadarAltitude(ut) < 0.3 ? (v.brakesOn ? Words.T("Rolling (brakes on)", "Rolling (brakes on)", "Parked") : "Rolling") :
                body.HasAtmosphere && alt < body.atmosphereHeight ? "Flying" :
                orbit != null && orbit.IsElliptic && orbit.Periapsis - body.radius > body.SafeAltitude && orbit.Apoapsis < body.SoiRadius ? Words.T("In orbit", "In orbit", "Going round!") :
                orbit != null && !orbit.IsElliptic ? Words.T("Escaping", "Escaping", "Flying away") : Words.T("Sub-orbital", "Not in orbit yet", "Falling back");
            Row(Words.T("Status", "Status", "Doing"), status);

            var tr = new Rect(10, 10, 300, telemetry.Count * 21 + 16 + (easy ? 0 : 20));
            TelemetryRect = tr;
            Ui.Box(tr);
            {
                float x = tr.x + 12, y = tr.y + 8, lw = easy ? 104 : 120;
                foreach (var (k, val) in telemetry)
                {
                    Ui.Text(new Rect(x, y, lw, 20), k, Ui.Small);
                    Ui.Text(new Rect(x + lw, y - 1, tr.width - lw - 20, 20), val, Ui.Bold);
                    y += 21;
                }
                if (!easy && Ui.Press(new Rect(tr.xMax - 70, tr.yMax - 24, 60, 18), HudMore ? "Less" : "More", Ui.ButtonSmall)) HudMore = !HudMore;
            }

            // ---- Clock and warp
            var cr = new Rect(W / 2 - 150, 10, 300, 52);
            Ui.Box(cr);
            Ui.Text(new Rect(cr.x, cr.y + 4, cr.width, 22), $"T+ {Ui.Time(Math.Max(0, ut - v.launchUt))}   <size=11>UT {Ui.Time(ut)}</size>", Ui.Center);
            string warpText = warp == 1 ? Words.T("Warp 1x", "Warp 1x", "Normal speed") : (railsWarp ? $"Warp {warp:N0}x{(eng ? " (on rails)" : "")}" : $"Physics warp {warp}x");
            if (warpTargetUt.HasValue) warpText += $"  - to {Words.Say(warpLabel ?? "the maneuver")} in {Ui.Time(warpTargetUt.Value - ut)}";
            else warpText += Words.T("   ·   events (click)", "   ·   coming up (click)", "   ·   click: what's next");
            Ui.Text(new Rect(cr.x, cr.y + 26, cr.width, 20), warpText, Ui.Hint);
            // The clock opens the list of what's coming up.
            if (Ui.Press(cr, GUIContent.none, GUIStyle.none)) { showEvents = !showEvents; if (showEvents) showVessels = showScience = false; }
            int others = U.vessels.Count - 1;
            if (Ui.Press(new Rect(cr.xMax + 6, cr.y + 12, 120, 28), $"{Words.T("Vessels", "Vessels", "Ships")} ({others}) {In.Name(K.V)}", showVessels ? Ui.ButtonSmallSel : Ui.ButtonSmall)) showVessels = !showVessels;
            // milestone 34: the keys list has its own button (F1 hides the whole interface now)
            if (Ui.Press(new Rect(cr.xMax + 132, cr.y + 12, 64, 28), "Keys", HelpVisible ? Ui.ButtonSmallSel : Ui.ButtonSmall)) { showHelp = !HelpVisible; helpUntil = 0; }
            int held = 0;
            foreach (var p in v.parts) held += p.science.Count;
            if (Ui.Press(new Rect(cr.x - 126, cr.y + 12, 120, 28), held > 0 ? $"Science ({held})" : "Science", showScience ? Ui.ButtonSmallSel : Ui.ButtonSmall)) { showScience = !showScience; if (showScience) showEvents = false; }
            if (v.HasMiningKit && Ui.Press(new Rect(cr.x - 252, cr.y + 12, 120, 28), "Mining", showMining ? Ui.ButtonSmallSel : Ui.ButtonSmall)) showMining = !showMining;
            // milestone 34: hide the interface, left of Science (clear of the stage list), if it fits beside the flight panel
            float hx = cr.x - (v.HasMiningKit ? 358 : 232);
            if (hx >= 310 && Ui.Press(new Rect(hx, cr.y + 12, 100, 28), $"Hide UI ({In.Name(K.F1)})", Ui.ButtonSmall)) Ui.ToggleHidden();

            // ---- Controls / gauges (bottom left): four bars, then only the lines that matter right now
            bool probe = v.IsControllable && v.CrewCount == 0;
            double ecCap = v.EcCapacity, ec = v.EcStored;
            ctlLines.Clear();
            string noCtl = !v.IsControllable ? Words.T("no command part", "no command part", "nothing to fly it") : v.CrewCount == 0 && v.EcStored < 1e-3 ? Words.T("probe out of power", "probe out of power", "battery empty") : Words.T("nobody aboard", "nobody aboard", "nobody inside");
            if (!v.HasControl) ctlLines.Add($"<color=#ff9070>{Words.T("No control", "No control", "Can't steer")} ({noCtl})</color>");
            if (commLink != null && (eng || probe || !commLink.Connected) && !(easy && !probe))
            {
                string sigCol = !commLink.Connected ? (probe ? "#ff8070" : "#c0c8d0") : commLink.signal > 0.5 ? "#7dff8a" : "#ffd060";
                string partial = probe && !commLink.Connected ? Words.T("  (probe: partial control)", "  (probe: partial control)", ": can't steer much") : "";
                ctlLines.Add($"<color={sigCol}>{commLink.Describe}</color>{partial}{(easy ? "" : $"   <size=11>{ec:0}/{ecCap:0} {Words.T("EC", "power", "")}</size>")}");
            }
            if (autopilotOn) ctlLines.Add($"{Words.T("Autopilot", "Autopilot", "Autopilot")}: {Words.Say(autopilot.status)}");
            string roverLine = Driving(v) ? RoverLine(v) : null;
            if (roverLine != null) ctlLines.Add(roverLine);
            else if (more) ctlLines.Add($"Thrust {v.thrustNow:0} kN   Mass {v.mass:0.00} t   {Words.T("Mono", "Thruster fuel", "")} {v.MonoLeft * 1000:0} kg");
            if (more && eng && v.HasControl) ctlLines.Add($"SAS: <b>{v.sas}</b>  <size=11>({Crew.TierModes(v.SasTier)})</size>");
            var kr = new Rect(10, 0, 300, 8 + 4 * 22 + ctlLines.Count * 21 + 46);
            kr.y = H - 10 - kr.height;

            DrawTargetBox(new Rect(10, tr.yMax + 8, 300, 72));
            var baseGroup = ActiveBase();
            if (baseGroup != null) DrawBasePanel(new Rect(10, kr.y - 8 - BasePanelH, 300, BasePanelH), baseGroup);
            if (v.IsEva) DrawEvaPanel(10, tr.yMax + (HasTarget ? 88 : 8));
            else DrawDockingPanel(10, tr.yMax + (HasTarget ? 88 : 8), baseGroup != null ? kr.y - 16 - BasePanelH : kr.y - 8);

            Ui.Box(kr);
            {
                float by = kr.y + 8;
                void BarRow(string label, float frac, Color col, string right)
                {
                    Ui.Text(new Rect(kr.x + 12, by, 100, 20), label, Ui.Small);
                    Ui.Bar(new Rect(kr.x + 92, by + 4, 146, 12), frac, col);
                    if (right != null) Ui.Text(new Rect(kr.x + 244, by - 1, 56, 20), right, Ui.Small);
                    by += 22;
                }
                BarRow(Words.T("Throttle", "Throttle", "Power"), (float)v.throttle, new Color(0.35f, 0.8f, 1f), $"{v.throttle * 100:0}%");
                BarRow(Words.T("Fuel (stage)", "Fuel (stage)", "Fuel"), v.ActiveFuelFraction(), new Color(0.95f, 0.65f, 0.2f), easy ? $"{v.ActiveFuelFraction() * 100:0}%" : null);
                var hot = v.Hottest;
                float heat = hot != null ? hot.HeatFraction : 0;
                BarRow(Words.T("Heat", "Heat", "Hot?"), heat, heat > 0.8f ? new Color(1f, 0.25f, 0.15f) : new Color(1f, 0.55f, 0.25f),
                    hot == null ? "" : easy ? (heat > 0.8f ? "<color=#ff6050><b>HOT!</b></color>" : heat > 0.5f ? "warm" : "ok") : Words.Temp(hot.temp));
                BarRow(Words.T("Power", "Battery", "Battery"), ecCap > 0 ? (float)(ec / ecCap) : 0, ec < 0.1 * ecCap ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.85f, 0.3f),
                    ecCap <= 0 ? "" : easy ? $"{ec / ecCap * 100:0}%" : $"{v.ecRate:+0.00;-0.00}/s");
                foreach (var l in ctlLines) { Ui.Text(new Rect(kr.x + 12, by, 280, 20), l, Ui.Small); by += 21; }
                var buttons = new List<(string, bool, Action)> { (Words.T("SAS", "SAS", "Steady"), v.sas != SasMode.Off, ToggleSas) };
                if (!easy) buttons.Add(("RCS", v.rcsOn, ToggleRcs));
                buttons.Add((Words.T("Gear", "Gear", "Legs"), v.gearDown, () => FireGroup(Groups.Gear)));
                buttons.Add(("Lights", v.lightsOn, () => FireGroup(Groups.Lights)));
                buttons.Add(("Brakes", v.brakesOn, () => FireGroup(Groups.Brakes)));
                float bw = (280f - 3f * buttons.Count) / (buttons.Count + 1), tx = kr.x + 10;
                foreach (var (label, on, act) in buttons)
                {
                    if (Ui.Press(new Rect(tx, kr.yMax - 40, bw, 30), label, on ? Ui.ButtonSmallSel : Ui.ButtonSmall)) act();
                    tx += bw + 3;
                }
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.35f, 0.3f);
                if (Ui.Press(new Rect(tx + 2, kr.yMax - 40, bw - 2, 30), Words.T("Abort", "Abort", "Eject"), Ui.ButtonSmall)) DoAbort();
                GUI.backgroundColor = oldBg;
            }

            DrawNavball(W, H, v);
            float stagesBottom = DrawStages(W, v);
            float rightY = stagesBottom + 10;
            if (node != null && node.valid) { float mh = laterNodes.Count > 0 ? 286 : 264; DrawManeuverPanel(new Rect(W - 390, rightY, 380, mh)); rightY += mh + 10; }
            else if (trajectory.Count > 0 && !v.landed && v.hasLaunched && !Words.Easy)
            {
                if (Ui.Press(new Rect(W - 390, stagesBottom + 8, 380, 28), Words.T("Add maneuver at next apoapsis (or click your orbit in the map)", "Plan a burn at the highest point (or click your orbit in the map)", ""), Ui.ButtonSmall))
                    AddNodeAtNextApsis();
                rightY += 30;
            }
            DrawGuide(W, rightY);

            if (mapView) DrawMapOverlay();
            if (U.career.IsCareer) DrawContractTracker(W, H);

            float popTop = 70;   // where pop-up panels and messages start: under the help bar when it's showing
            if (HelpVisible && !paused)   // (milestone 32: the pause menu used to sit on top of it)
            {
                string N(K k) => In.Name(k);
                string warpKeys = $"{N(K.Comma)} {N(K.Period)} {N(K.Slash)}";
                string[] lines = Words.Easy && !v.IsEva && !Driving(v) ? new[]
                {
                    $"{N(K.Space)} start / next step   {N(K.LeftShift)} more power   {N(K.LeftCtrl)} less   {N(K.W)}{N(K.A)}{N(K.S)}{N(K.D)} steer   {N(K.T)} keep steady   {N(K.M)} map   {N(K.F1)} hide this  {N(K.F2)} photo",
                } : v.IsEva ? new[]
                {
                    $"EVA on the ground: {N(K.W)}/{N(K.S)} walk  {N(K.A)}/{N(K.D)} turn (first person: step sideways)  {N(K.LeftCtrl)} run  {N(K.Space)} jump  {N(K.C)} first/third person  {N(K.LeftShift)} jetpack up  {N(K.G)} flag  {N(K.B)} climb in",
                    $"EVA in space: {N(K.W)}/{N(K.S)} {N(K.A)}/{N(K.D)} {N(K.LeftShift)}/{N(K.LeftCtrl)} jetpack  {N(K.Q)}/{N(K.E)} turn  {N(K.T)} hold attitude   ·   {N(K.M)} map  {N(K.V)} vessels  {N(K.LeftBracket)} {N(K.RightBracket)} switch  {warpKeys} warp  {N(K.F5)}/{N(K.F9)} save/load  {N(K.F1)} hide  {N(K.F2)} photo",
                } : Driving(v) ? new[]
                {
                    $"Rover: {N(K.W)}/{N(K.S)} drive  {N(K.A)}/{N(K.D)} steer  {N(K.B)} brakes  {N(K.Q)}/{N(K.E)} roll  {N(K.T)} SAS  {N(K.U)} lights  {N(K.Space)} stage  {N(K.LeftShift)}/{N(K.LeftCtrl)} throttle (engines)",
                    $"{N(K.M)}/{N(K.O)} map/layers  {N(K.LeftBracket)} {N(K.RightBracket)} switch  {N(K.V)} vessels  {warpKeys} warp (park with {N(K.B)} first)  {N(K.F5)}/{N(K.F9)} save/load  ESC menu  {N(K.F1)} hide  {N(K.F2)} photo",
                } : new[]
                {
                    $"{N(K.Space)} stage  {N(K.LeftShift)}/{N(K.LeftCtrl)} throttle  {N(K.Z)}/{N(K.X)} full/cut  {N(K.W)}{N(K.A)}{N(K.S)}{N(K.D)} {N(K.Q)}{N(K.E)} steer  {N(K.T)} SAS  {N(K.R)} RCS  {N(K.H)}{N(K.N)}{N(K.I)}{N(K.K)}{N(K.J)}{N(K.L)} translate  1-0 groups  {N(K.Backspace)} abort  {N(K.F5)}/{N(K.F9)} save/load",
                    $"{N(K.G)} gear  {N(K.U)} lights  {N(K.B)} brakes  {N(K.Y)} deploy  {N(K.M)}/{N(K.O)} map/layers  {N(K.LeftBracket)} {N(K.RightBracket)} switch  {N(K.V)} vessels  {warpKeys} warp  {N(K.P)} autopilot  {N(K.F)} thermal  ESC menu  {N(K.F1)} hide  {N(K.F2)} photo",
                };
                // As wide as it can be between the telemetry (left) and the stages (right); the lines wrap on narrow windows.
                float hw = Mathf.Min(400, W / 2 - 400);
                float tw = hw * 2 - 16, lh = 0;
                foreach (var l in lines) lh += Ui.Hint.CalcHeight(new GUIContent(l), tw) + 2;
                var hr = new Rect(W / 2 - hw, 68, hw * 2, lh + 12);
                Ui.Box(hr);
                float ly = hr.y + 6;
                foreach (var l in lines)
                {
                    float h = Ui.Hint.CalcHeight(new GUIContent(l), tw);
                    Ui.Text(new Rect(hr.x + 8, ly, tw, h), l, Ui.Hint);
                    ly += h + 2;
                }
                popTop = hr.yMax + 6;
            }

            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            {
                var c = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(messageUntil - Time.unscaledTime));
                // Below any open pop-up panel, so the panel doesn't cover it.
                float my = popTop + 2;
                if (showVessels) my = Mathf.Max(my, popTop + 388);
                if (showScience) my = Mathf.Max(my, popTop + 368);
                if (showEvents) my = Mathf.Max(my, popTop + 50 + Mathf.Min(events.Count, 9) * 30);
                if (showMining && v.HasMiningKit) my = Mathf.Max(my, popTop + 330);
                float mh = Mathf.Max(60, Ui.BigCenter.CalcHeight(new GUIContent(Words.Say(message)), 800));   // long messages wrap instead of being cut off
                Ui.Text(new Rect(W / 2 - 400, my, 800, mh), Words.Say(message), Ui.BigCenter);
                GUI.color = c;
            }

            // Pop-up panels last, so they sit on top of everything else.
            if (showVessels) { showEvents = false; DrawVesselList(new Rect(W / 2 - 210, popTop, 420, 380)); }
            if (showEvents) DrawEvents(new Rect(W / 2 - 225, popTop, 450, 0));
            if (showScience) DrawSciencePanel(new Rect(320, popTop, 440, 360));
            if (showMining && v.HasMiningKit) DrawMiningPanel(showScience ? 770 : 320, popTop);

            if (v.destroyed) DrawEndPanel("Vessel destroyed", v.destroyReason);
            else if (paused && !showHowTo && !showBadges) DrawEndPanel("Paused", "");
            if (showBadges && !showHowTo)
            {
                float bw = Mathf.Min(920, W - 60), bh = Mathf.Min(600, H - 100);
                if (!Badges.DrawScreen(new Rect(W / 2 - bw / 2, H / 2 - bh / 2, bw, bh), U.career)) showBadges = false;
            }
            // The guide sits over everything (milestone 23)
            if (showHowTo)
            {
                float gw = Mathf.Min(980, W - 80), gh = Mathf.Min(470, H - 120);
                if (!HowToPlay.Draw(new Rect(W / 2 - gw / 2, H / 2 - gh / 2, gw, gh), ref howToTopic)) showHowTo = false;
            }
        }

        bool showHowTo, showBadges;
        int howToTopic;
        public void ShowHowTo(int topic) { showHowTo = topic >= 0; if (topic >= 0) howToTopic = topic; }

        void DrawNavball(float W, float H, VesselSim v)
        {
            const float size = 210;
            var panel = new Rect(W / 2 - 200, H - size - 52, 400, size + 46);
            Ui.Box(panel);

            // Speed readout (click to change mode). Switches to Target by itself when a target vessel is within 5 km.
            if (!speedModeManual)
                speedMode = targetVessel != null && Vector3d.Distance(TargetAbsPos(ut), v.AbsolutePosition(ut)) < 5000 ? SpeedMode.Target
                    : (v.body.HasAtmosphere && v.Altitude < 30_000) || v.landed || v.OnWheels || v.RadarAltitude(ut) < 300 ? SpeedMode.Surface : SpeedMode.Orbit; // near any ground, speed over it
            double spd = speedMode == SpeedMode.Surface ? v.SurfaceRelVelocity.magnitude :
                         speedMode == SpeedMode.Orbit ? v.vel.magnitude :
                         HasTarget ? (v.AbsoluteVelocity(ut) - TargetAbsVel(ut)).magnitude : 0;
            string label = speedMode == SpeedMode.Target && !HasTarget ? "Target: none" : $"{speedMode}  {spd:0.0} m/s";
            if (Ui.Press(new Rect(panel.x + 98, panel.y + 6, 210, 26), label, Ui.Button))
            {
                speedModeManual = true;
                speedMode = (SpeedMode)(((int)speedMode + 1) % 3);
                if (speedMode == SpeedMode.Surface) speedModeManual = false; // back to automatic
            }

            var ballRect = new Rect(panel.x + 98, panel.y + 38, size, size);
            markerScratch.Clear();
            void Add(MeshFactory.MarkerKind k, SasMode m) { var d = v.DirectionFor(m); if (d.HasValue) markerScratch.Add((k, d.Value)); }
            Add(MeshFactory.MarkerKind.Prograde, SasMode.Prograde);
            Add(MeshFactory.MarkerKind.Retrograde, SasMode.Retrograde);
            Add(MeshFactory.MarkerKind.Normal, SasMode.Normal);
            Add(MeshFactory.MarkerKind.AntiNormal, SasMode.AntiNormal);
            Add(MeshFactory.MarkerKind.RadialOut, SasMode.RadialOut);
            Add(MeshFactory.MarkerKind.RadialIn, SasMode.RadialIn);
            Add(MeshFactory.MarkerKind.Maneuver, SasMode.Maneuver);
            Add(MeshFactory.MarkerKind.Target, SasMode.Target);
            Add(MeshFactory.MarkerKind.AntiTarget, SasMode.AntiTarget);
            root.navball.Draw(ballRect, markerScratch);

            // SAS buttons on the left (milestone 24): hold, forward and back always; the others only when this ship
            // can use them (pilot or probe level, a plan, a target). Young players get three big ones.
            sasScratch.Clear();
            foreach (var (mode, text) in SasButtons)
            {
                bool core = mode == SasMode.Stability || mode == SasMode.Prograde || mode == SasMode.Retrograde;
                if (!core && (Words.Easy || !v.CanHold(mode))) continue;
                if ((mode == SasMode.Maneuver || mode == SasMode.Target || mode == SasMode.AntiTarget) && !v.DirectionFor(mode).HasValue) continue;
                sasScratch.Add((mode, SasLabel(mode, text)));
            }
            for (int i = 0; i < sasScratch.Count; i++)
            {
                var (mode, text) = sasScratch[i];
                var br = Words.Easy ? new Rect(panel.x + 6, panel.y + 40 + i * 58, 89, 52) : new Rect(panel.x + 6 + (i % 2) * 45, panel.y + 40 + (i / 2) * 40, 44, 36);
                var style = v.sas == mode ? Ui.ButtonSmallSel : Ui.ButtonSmall;
                var oldCol = GUI.color;
                if (!v.CanHold(mode)) GUI.color = new Color(1f, 1f, 1f, 0.3f); // still clickable: it says what's needed
                if (Ui.Press(br, text, style)) SetSas(mode);
                GUI.color = oldCol;
            }
            string sasName = Words.T("SAS", "SAS", "Steady");
            if (Ui.Press(new Rect(panel.x + 6, panel.y + 6, 89, 26), v.sas == SasMode.Off ? $"{sasName} off" : $"{sasName} on", v.sas == SasMode.Off ? Ui.ButtonSmall : Ui.ButtonSmallSel))
                v.sas = v.sas == SasMode.Off ? SasMode.Stability : SasMode.Off;

            // Gauges on the right: atmosphere and heat
            float gx = panel.xMax - 62;
            Ui.Text(new Rect(gx - 6, panel.y + 8, 70, 20), Words.T("Air  Heat", "Air  Heat", "Air  Hot"), Ui.Small);
            float gh = size - 10;
            float air = (float)v.body.PressureAtm(v.Altitude);
            float hf = v.Hottest != null ? v.Hottest.HeatFraction : 0;
            DrawVBar(new Rect(gx, panel.y + 40, 16, gh), Mathf.Pow(air, 0.3f), new Color(0.45f, 0.75f, 1f));
            DrawVBar(new Rect(gx + 30, panel.y + 40, 16, gh), hf, hf > 0.8f ? new Color(1f, 0.25f, 0.15f) : new Color(1f, 0.6f, 0.25f));
        }

        /// <summary>Distance, closing speed and closest approach to the target.</summary>
        void DrawTargetBox(Rect r)
        {
            if (!HasTarget) return;
            var v = active;
            Vector3d rel = TargetAbsPos(ut) - v.AbsolutePosition(ut);
            Vector3d relV = v.AbsoluteVelocity(ut) - TargetAbsVel(ut);
            double dist = rel.magnitude - (target != null ? target.radius : 0);
            double closing = rel.magnitude > 1e-6 ? Vector3d.Dot(relV, rel.normalized) : 0;
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 12, r.y + 6, r.width - 24, 20), $"Target  <b>{TargetName}</b>", Ui.Label);
            Ui.Text(new Rect(r.x + 12, r.y + 26, r.width - 24, 20),
                $"{Ui.Dist(dist)}   rel {relV.magnitude:0.0} m/s   {(closing >= 0 ? "closing" : "opening")} {Math.Abs(closing):0.0} m/s", Ui.Small);
            string ca = double.IsNaN(closestDist) ? "" : closestTime - ut < 1 ? $"Closest {Ui.Dist(closestDist)} right now" : $"Closest {Ui.Dist(closestDist)} in {Ui.Time(closestTime - ut)}";
            if (Ui.Press(new Rect(r.xMax - 64, r.y + 6, 54, 20), "Clear", Ui.ButtonSmall)) { target = null; targetVessel = null; }
            Ui.Text(new Rect(r.x + 12, r.y + 46, r.width - 24, 20), ca, Ui.Small);
        }

        /// <summary>Docking alignment with the target's port, docked ports (undock), and the crew aboard (transfer).</summary>
        void DrawDockingPanel(float x, float y, float maxY)
        {
            var v = active;
            var crewParts = new List<PartState>(v.CrewParts());
            var docked = new List<PartState>();
            foreach (var p in v.DockedPorts()) { var o = v.FindPart(p.dockedTo); if (o != null && p.parent == o.index) docked.Add(p); }
            bool showDock = dockMine != null && targetVessel != null && !targetVessel.destroyed;   // (the sweep caught a target cleared mid-frame)
            bool showCrew = v.CrewCount > 0;
            int broken = v.parts.FindAll(p => p.broken).Count;
            if (!showDock && docked.Count == 0 && !showCrew && broken == 0) return;
            float h = 30 + (showDock ? 86 : 0) + docked.Count * 28 + (showCrew ? 22 + crewParts.Count * 40 : 0) + (broken > 0 ? 34 : 0);
            h = Mathf.Min(h, maxY - y);
            var r = new Rect(x, y, 300, h);
            Ui.Box(r);
            float cy = r.y + 6;
            bool holding = docked.Count > 0 && docked.TrueForAll(p => p.def.kind == PartKind.Rock || p.def.kind == PartKind.Grabber);
            Ui.Text(new Rect(r.x + 12, cy, 200, 22), showDock ? "Docking" : docked.Count > 0 ? (holding ? "Holding" : "Docked") : "Crew", Ui.Header);
            cy += 24;
            if (showDock)
            {
                bool lined = dockAngle < VesselSim.DockMaxAngle && dockLateral < 0.5;
                bool slow = dockClosing < VesselSim.DockMaxSpeed;
                string c(bool ok) => ok ? "#7dff8a" : "#ffb070";
                Ui.Text(new Rect(r.x + 12, cy, 280, 20), $"{dockMine.def.name} to <b>{targetVessel.name}</b>", Ui.Small);
                Ui.Text(new Rect(r.x + 12, cy + 18, 280, 20),
                    $"<color={c(lined)}>{Ui.Dist(dockDist)}  off-axis {dockLateral:0.00} m  angle {dockAngle:0} deg</color>", Ui.Small);
                Ui.Text(new Rect(r.x + 12, cy + 36, 280, 20),
                    $"<color={c(slow)}>{(dockClosing >= 0 ? "closing" : "opening")} {Math.Abs(dockClosing):0.00} m/s</color>   latches within {VesselSim.DockRange:0.0} m", Ui.Small);
                if (Ui.Press(new Rect(r.x + 12, cy + 58, 130, 22), v.sas == SasMode.DockAlign ? "Aligning (SAS)" : "Align ports (SAS)", v.sas == SasMode.DockAlign ? Ui.ButtonSmallSel : Ui.ButtonSmall))
                    SetSas(v.sas == SasMode.DockAlign ? SasMode.Stability : SasMode.DockAlign);
                if (Ui.Press(new Rect(r.x + 150, cy + 58, 138, 22), v.rcsOn ? "RCS on: H/N I/K J/L" : "RCS off (R)", v.rcsOn ? Ui.ButtonSmallSel : Ui.ButtonSmall)) ToggleRcs();
                cy += 86;
            }
            foreach (var p in docked)
            {
                bool rock = p.def.kind == PartKind.Rock || p.def.kind == PartKind.Grabber;
                string other = p.dockedName ?? v.FindPart(p.dockedTo)?.dockedName ?? "vessel";
                Ui.Text(new Rect(r.x + 12, cy + 3, 190, 20), rock ? $"<b>{other}</b>  <size=11>({v.FindPart(p.def.kind == PartKind.Rock ? p.index : p.dockedTo)?.def.dryMass:0} t)</size>" : $"Docked: <b>{other}</b>", Ui.Small);
                if (Ui.Press(new Rect(r.xMax - 82, cy, 70, 22), rock ? "Release" : "Undock", Ui.ButtonSmall)) { Undock(p); return; }
                cy += 28;
            }
            if (showCrew)
            {
                string sup = !v.UsesSupplies ? ""
                    : v.starvingSince >= 0 ? $"   <color=#ff6050><b>OUT OF SUPPLIES</b>: {Ui.Time(Math.Max(0, v.starvingSince + Supplies.Grace - ut))} left</color>"
                    : $"   supplies <color={(v.SupplyDaysLeft() < 1 ? "#ffb050" : "#9fe0a0")}>{Supplies.DaysText(v.SupplyDaysLeft())}</color>{(v.greenhouseOn ? " (greenhouse)" : "")}";
                Ui.Text(new Rect(r.x + 12, cy + 2, 280, 20), $"Crew aboard: {v.CrewCount}{sup}", Ui.Small);
                cy += 22;
                for (int i = 0; i < crewParts.Count && cy < r.yMax - 30; i++)
                {
                    var p = crewParts[i];
                    Ui.Text(new Rect(r.x + 12, cy, 200, 20), $"<b>{p.def.name}</b> {p.crew.Count}/{p.def.crew}", Ui.Small);
                    string people = p.crew.Count == 0 ? "empty" : string.Join(", ", p.crew.Select(n => $"{(p.crew.Count > 1 ? n.Split(' ')[0] : n)} <color=#9fc6e8>{Crew.Tag(v.CrewInfo(n))}</color>"));
                    Ui.Text(new Rect(r.x + 12, cy + 17, crewParts.Count > 1 ? 150 : 130, 20), people, Ui.Small);
                    if (crewParts.Count == 1 && p.crew.Count > 0 && Ui.Press(new Rect(r.xMax - 158, cy + 4, 72, 26), $"Inside {In.Name(K.Tab)}", Ui.ButtonSmall)) { EnterInterior(); return; }   // milestone 34
                    if (p.crew.Count > 0 && Ui.Press(new Rect(r.xMax - (crewParts.Count > 1 ? 134 : 82), cy + 4, crewParts.Count > 1 ? 48 : 70, 26), "EVA", Ui.ButtonSmall))
                    {
                        GoEva(p);
                        return;
                    }
                    if (crewParts.Count > 1)
                    {
                        PartState to = null;
                        for (int k = 1; k < crewParts.Count && to == null; k++)
                        {
                            var q = crewParts[(i + k) % crewParts.Count];
                            if (q.crew.Count < q.def.crew) to = q;
                        }
                        GUI.enabled = p.crew.Count > 0 && to != null;
                        if (Ui.Press(new Rect(r.xMax - 82, cy + 4, 70, 26), "Move 1 >", Ui.ButtonSmall)) TransferCrew(p, to);
                        GUI.enabled = true;
                    }
                    cy += 40;
                }
            }
            if (broken > 0)
                Ui.Text(new Rect(r.x + 12, cy + 2, 280, 32), $"<color=#ff9070>{broken} broken landing leg{(broken > 1 ? "s" : "")}</color>: an engineer on EVA can fix {(broken > 1 ? "them" : "it")}.", Ui.Small);
        }

        /// <summary>The astronaut: jetpack fuel, controls, climbing back in, flags, and repairs or repacking within reach.</summary>
        void DrawEvaPanel(float x, float y)
        {
            var v = active;
            string who = v.EvaName ?? "Astronaut";
            var m = v.CrewInfo(who);
            var jobs = v.WorkInReach(U.vessels);
            VesselSim boardTo = null;
            foreach (var u in U.vessels) if (v.BoardingPart(u) != null) { boardTo = u; break; }
            float h = 150 + (v.landed ? 30 : 0) + jobs.Count * 30 + CableRows(new Rect(x, y, 300, 0), 0, false);
            var r = new Rect(x, y, 300, h);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 12, r.y + 6, 200, 22), "EVA", Ui.Header);
            Ui.Text(new Rect(r.x + 60, r.y + 8, 200, 20), $"<b>{who}</b>  <color=#9fc6e8>{Crew.Tag(m)}</color>", Ui.Label);
            var face = Ardlings.LookFor(who);   // their sticky note
            GUI.DrawTexture(new Rect(r.xMax - 40, r.y + 6, 30, 30), Ardlings.FaceTexture(face.face, face.paper));
            float cy = r.y + 32;
            float fuel = (float)(v.MonoLeft / Math.Max(1e-6, PartCatalog.EvaSuit.mono));
            Ui.Text(new Rect(r.x + 12, cy, 70, 20), "Jetpack", Ui.Small);
            Ui.Bar(new Rect(r.x + 80, cy + 4, 150, 12), fuel, fuel < 0.2f ? new Color(1f, 0.35f, 0.25f) : new Color(0.5f, 0.85f, 1f));
            Ui.Text(new Rect(r.x + 236, cy - 1, 60, 20), $"{fuel * 100:0}%", Ui.Small);
            cy += 22;
            Ui.Text(new Rect(r.x + 12, cy, 280, 36), v.landed ? (firstPerson ? "W/S walk, A/D step, mouse looks, CTRL run, SPACE jump, C third person" : "W/S walk, A/D turn, CTRL run, SPACE jump, C first person") : "W/S A/D and SHIFT/CTRL fly the jetpack, Q/E turn, T holds attitude", Ui.Small);
            cy += 36;
            GUI.enabled = boardTo != null;
            if (Ui.Press(new Rect(r.x + 12, cy, 276, 26), boardTo != null ? $"Climb into {boardTo.name} (B)" : "Board: get within 3 m of a hatch", Ui.ButtonSmall)) { BoardNearest(); GUI.enabled = true; return; }
            GUI.enabled = true;
            cy += 30;
            if (v.landed)
            {
                if (Ui.Press(new Rect(r.x + 12, cy, 276, 26), "Plant a flag (G)", Ui.ButtonSmall)) PlantFlag();
                cy += 30;
            }
            cy += CableRows(r, cy, true);
            foreach (var (vv, p) in jobs)
            {
                string label = p.broken ? $"Fix the {p.def.name} on {vv.name}{(m.role == CrewRole.Engineer ? "" : " (engineer)")}" : $"Repack the {p.def.name} on {vv.name}";
                GUI.enabled = !p.broken || m.role == CrewRole.Engineer;
                if (Ui.Press(new Rect(r.x + 12, cy, 276, 26), label, Ui.ButtonSmall)) { if (p.broken) RepairPart(vv, p); else RepackChute(vv, p); }
                GUI.enabled = true;
                cy += 30;
            }
            Ui.Text(new Rect(r.x + 12, r.yMax - 24, 280, 20), "Science button: EVA report, surface sample", Ui.Small);
        }

        bool showScience;
        Vector2 scienceScroll;
        public void ShowSciencePanel(bool on) => showScience = on;
        public void ShowVesselList(bool on) => showVessels = on;

        /// <summary>Experiments aboard: run them here, send results home (needs a deployed antenna) or keep them for recovery.</summary>
        void DrawSciencePanel(Rect r)
        {
            var v = active;
            var c = U.career;
            Ui.Box(r);
            var sit = Science.SituationOf(v, ut);
            Ui.Text(new Rect(r.x + 12, r.y + 6, 200, 22), "Science", Ui.Header);
            Ui.Text(new Rect(r.x + 100, r.y + 9, r.width - 140, 20), $"Here: {Science.Describe(sit)} {(sit <= Situation.FlyingHigh && sit >= Situation.FlyingLow ? "over " : sit == Situation.Landed ? "of " : "")}{v.body.name}" +
                (Science.Regional(sit) ? $", {Regions.Under(v, ut)}" : ""), Ui.Small);
            if (Ui.Press(new Rect(r.xMax - 34, r.y + 6, 24, 22), "x", Ui.ButtonSmall)) showScience = false;
            var list = Lab.On(v);
            bool radio = Lab.CanTransmit(v);
            var view = new Rect(r.x + 6, r.y + 34, r.width - 12, r.height - 64);
            scienceScroll = GUI.BeginScrollView(view, scienceScroll, new Rect(0, 0, view.width - 18, Math.Max(view.height - 4, list.Count * 62)));
            float y = 0;
            if (list.Count == 0) Ui.Text(new Rect(6, 4, view.width - 30, 40), "No experiments aboard. Add a Thermometer or other science part (or bring crew for crew reports).", Ui.Small);
            foreach (var (p, e) in list)
            {
                var d = Lab.Held(p, e);
                Ui.Text(new Rect(6, y + 2, 300, 20), $"<b>{e.name}</b>  <size=11>{p.def.name}</size>", Ui.Label);
                if (d != null)
                {
                    float worth = c.Remaining(d.subject, Career.CapOf(d, root.bodies));
                    float tf = Lab.TransmitFactor(v, d);
                    Ui.Text(new Rect(6, y + 21, view.width - 170, 20), d.title, Ui.Small);
                    Ui.Text(new Rect(6, y + 38, view.width - 165, 20), $"<color=#9fe8a8>{worth:0.0} sci home, {worth * tf:0.0} if sent{(tf > d.transmit + 1e-4f ? $" ({tf * 100:0}%, scientist)" : "")}</color>", Ui.Small);
                    GUI.enabled = radio;
                    if (Ui.Press(new Rect(view.width - 154, y + 10, 62, 26), "Send", Ui.ButtonSmall)) { TransmitData(p, d); GUI.enabled = true; break; }
                    GUI.enabled = true;
                    if (Ui.Press(new Rect(view.width - 86, y + 10, 64, 26), "Discard", Ui.ButtonSmall)) { Lab.Discard(p, d); break; }
                }
                else if (p.used)
                {
                    bool sci = v.BestLevel(CrewRole.Scientist) >= 0;
                    Ui.Text(new Rect(6, y + 22, view.width - 120, 20), sci ? "Used: your scientist can clean it out" : "Used: needs a scientist aboard to clean it out", Ui.Small);
                    GUI.enabled = sci;
                    if (Ui.Press(new Rect(view.width - 86, y + 10, 64, 26), "Reset", Ui.Button)) Show(Lab.Reset(v, p), 3f);
                    GUI.enabled = true;
                }
                else
                {
                    float worth = Lab.Preview(c, v, e, ut, p);
                    Ui.Text(new Rect(6, y + 22, view.width - 120, 20), worth < 0 ? "Nothing to measure here" : worth < 0.05f ? "Nothing new to learn here" : $"Worth up to {worth:0.0} science here", Ui.Small);
                    GUI.enabled = worth >= 0;
                    if (Ui.Press(new Rect(view.width - 86, y + 10, 64, 26), "Run", Ui.Button)) RunExperiment(p, e);
                    GUI.enabled = true;
                }
                y += 62;
            }
            GUI.EndScrollView();
            Ui.Text(new Rect(r.x + 12, r.yMax - 26, r.width - 24, 20),
                radio ? $"{commLink?.Describe ?? "Signal"}: results can be sent home ({VesselSim.TransmitEc:0} EC each)." :
                        !v.connected ? "No signal: keep results and recover the vessel, or get in range of the ground or a relay." : "Not enough power to transmit.", Ui.Small);
        }

        bool showMining;
        public void ShowMiningPanel(bool on) => showMining = on;

        /// <summary>Ore under you, drills, converters (Off / Fuel / Mono), core heat and radiators, power, and the survey scanner.</summary>
        void DrawMiningPanel(float x, float y0)
        {
            var v = active;
            var convs = v.parts.FindAll(p => p.def.convertRate > 0);
            bool drills = v.parts.Exists(p => p.def.drillRate > 0), scanner = v.parts.Exists(p => p.def.scanHalfAngle > 0);
            bool machines = drills || convs.Count > 0;
            float h = 86 + (v.OreCapacity > 0 ? 24 : 0) + (drills ? 22 : 0) + convs.Count * 46 + (machines ? 70 : 0) + (scanner ? 22 : 0);
            var r = new Rect(x, y0, 430, h);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 12, r.y + 6, 200, 22), "Mining", Ui.Header);
            int eng = v.BestLevel(CrewRole.Engineer);
            if (machines) Ui.Text(new Rect(r.x + 100, r.y + 9, 280, 20), eng >= 0 ? $"<color=#9fc6e8>Engineer aboard: +{Crew.MiningBonus(eng) * 100:0}% output</color>" : "No engineer aboard (they add 20-70% output)", Ui.Small);
            if (Ui.Press(new Rect(r.xMax - 34, r.y + 6, 24, 22), "x", Ui.ButtonSmall)) showMining = false;
            float cy = r.y + 32;

            // Ore under the vessel: exact with a drill on the ground, approximate where the survey has mapped it.
            var body = v.body;
            Vector3d under = body.InertialToFixed(v.pos, ut);
            double c = v.OreUnder(ut);
            bool mapped = U.survey.IsScanned(body, under);
            string here = v.landed && drills ? $"Ore here: <b>{Ore.Pct(c)}</b>" :
                mapped ? $"Ore {(v.landed ? "here" : "below")}: <b>~{Math.Round(c * 100):0}%</b> (mapped)" :
                $"Ore {(v.landed ? "here" : "below")}: <color=#c0c8d0>not mapped</color>";
            Ui.Text(new Rect(r.x + 12, cy, 260, 20), here, Ui.Label);
            Ui.Text(new Rect(r.x + 250, cy + 2, 170, 20), $"{body.name} mapped {U.survey.Coverage(body) * 100:0}%", Ui.Small);
            cy += 24;
            if (v.OreCapacity > 0)
            {
                Ui.Text(new Rect(r.x + 12, cy, 60, 20), "Ore", Ui.Small);
                Ui.Bar(new Rect(r.x + 60, cy + 4, 170, 12), (float)(v.OreStored / v.OreCapacity), new Color(0.75f, 0.52f, 0.3f));
                Ui.Text(new Rect(r.x + 238, cy - 1, 190, 20), $"{v.OreStored:0.00} / {v.OreCapacity:0.0} t  {(v.oreRate > 0 ? $"+{v.oreRate * 3600:0.0} t/h" : "")}", Ui.Small);
                cy += 24;
            }
            if (drills)
            {
                Ui.Text(new Rect(r.x + 12, cy, 410, 20), $"Drills: {(string.IsNullOrEmpty(v.drillNote) ? "idle" : v.drillNote)}", Ui.Small);
                cy += 22;
            }
            foreach (var p in convs)
            {
                Ui.Text(new Rect(r.x + 12, cy, 150, 20), $"<b>{p.def.name}</b>", Ui.Small);
                float bx = r.x + 170;
                foreach (var (label, mode) in new[] { ("Off", 0), ("Fuel", 1), ("Monoprop", 2) })
                {
                    float bw = mode == 2 ? 84 : 52;
                    if (Ui.Press(new Rect(bx, cy - 2, bw, 22), label, p.convMode == mode ? Ui.ButtonSmallSel : Ui.ButtonSmall)) { v.SetConverter(p, mode); Show($"{p.def.name}: {(mode == 0 ? "off" : mode == 1 ? "making fuel" : "making monopropellant")}", 2f); }
                    bx += bw + 4;
                }
                v.convNotes.TryGetValue(p.index, out string note);
                string rate = p.convMode == 1 && v.fuelRate > 0 ? $"  +{v.fuelRate * 3600:0.0} t/h fuel" : p.convMode == 2 && v.monoRate > 0 ? $"  +{v.monoRate * 3600:0.0} t/h mono" : "";
                Ui.Text(new Rect(r.x + 24, cy + 21, 400, 20), (note ?? "Off") + rate, Ui.Small);
                cy += 46;
            }
            if (machines)
            {
                // Core heat: machines slow down above 700 K and stop at 1000 K.
                float hf = Mathf.InverseLerp((float)Ore.AmbientTemp, (float)Ore.HotStop, (float)v.coreTemp);
                Color hc = v.coreTemp > Ore.HotStart ? new Color(1f, 0.3f, 0.2f) : v.coreTemp > 550 ? new Color(1f, 0.65f, 0.25f) : new Color(0.45f, 0.8f, 1f);
                Ui.Text(new Rect(r.x + 12, cy, 60, 20), "Heat", Ui.Small);
                Ui.Bar(new Rect(r.x + 60, cy + 4, 170, 12), hf, hc);
                Ui.Line(new Rect(r.x + 60 + 170 * Mathf.InverseLerp((float)Ore.AmbientTemp, (float)Ore.HotStop, (float)Ore.HotStart), cy + 2, 2, 16), new Color(1, 1, 1, 0.6f));
                Ui.Text(new Rect(r.x + 238, cy - 1, 190, 20), $"{v.coreTemp:0} K  {(v.heatEff < 0.999 ? $"<color=#ff9070>{v.heatEff * 100:0}% speed</color>" : "full speed")}", Ui.Small);
                cy += 22;
                int rads = v.parts.FindAll(p => p.def.coolRate > 0).Count, folded = v.parts.FindAll(p => p.def.coolRate > 0 && p.def.IsDeployable && p.legDeploy < 0.9f).Count;
                Ui.Text(new Rect(r.x + 12, cy, 410, 20),
                    $"Making {v.heatMade:0} kW, shedding {v.heatShed:0} kW (up to {v.coolCapacity:0} kW at 600 K)   {(rads == 0 ? "no radiators" : $"{rads} radiator{(rads > 1 ? "s" : "")}{(folded > 0 ? $", {folded} folded (Y)" : "")}")}", Ui.Small);
                cy += 22;
                double sun = v.SolarOutput(ut);
                Ui.Text(new Rect(r.x + 12, cy, 410, 20), $"Power: mining uses {v.miningEc:0.0} EC/s · solar {sun:0.0} EC/s · stored {v.EcStored:0}/{v.EcCapacity:0} EC", Ui.Small);
                cy += 26;
            }
            if (scanner)
            {
                Ui.Text(new Rect(r.x + 12, cy, 410, 20), $"Scanner: {(string.IsNullOrEmpty(v.scanNote) ? "idle" : v.scanNote)}   <size=11>(ore map: O)</size>", Ui.Small);
                cy += 22;
            }
            Ui.Text(new Rect(r.x + 12, r.yMax - 24, r.width - 24, 20), "Y unfolds drills, radiators, scanners. Drills need to be landed.", Ui.Small);
        }

        /// <summary>Career: active contracts and money, bottom right.</summary>
        void DrawContractTracker(float W, float H)
        {
            var c = U.career;
            var act = new List<Contract>(c.Active);
            float h = 30 + act.Count * 38;
            var r = new Rect(W - 390, H - 10 - h, 380, h);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 12, r.y + 6, r.width - 24, 20), $"<b>Contracts</b>   funds {c.funds:N0}   science {c.science:0.0}   rep {c.reputation:0}", Ui.Label);
            float y = r.y + 30;
            if (act.Count == 0) Ui.Text(new Rect(r.x + 12, y - 4, r.width - 24, 20), "", Ui.Small);
            foreach (var k in act)
            {
                Ui.Text(new Rect(r.x + 12, y, r.width - 24, 20), $"<color=#ffd060>o</color> {Words.Say(k.title)}", Ui.Small);
                Ui.Text(new Rect(r.x + 24, y + 17, r.width - 36, 20), $"{k.reward:N0} funds · due in {Ui.Time(Math.Max(0, k.deadline - ut))}", Ui.Small);
                y += 38;
            }
        }

        bool showEvents;
        public void ShowEvents(bool on) { showEvents = on; if (on) showVessels = showScience = false; }
        List<FlightEvent> events = new List<FlightEvent>();
        float eventsTimer;

        /// <summary>What's coming up (soonest first), each with a button to warp there.</summary>
        void DrawEvents(Rect r)
        {
            eventsTimer -= Time.unscaledDeltaTime;
            if (eventsTimer <= 0)
            {
                eventsTimer = 0.5f;
                events = FlightEvents.Build(active, trajectory, node, targetVessel != null ? targetVessel.name : target?.name,
                    double.IsNaN(closestDist) ? double.NaN : closestTime, closestDist, U.vessels, root.bodies, ut, laterNodes);
            }
            int rows = Mathf.Min(events.Count, 9);
            r.height = 38 + Mathf.Max(1, rows) * 30 + 6;
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 12, r.y + 6, 200, 22), Words.T("Coming up", "Coming up", "What's next"), Ui.Header);
            if (Ui.Press(new Rect(r.xMax - 30, r.y + 8, 22, 20), "x", Ui.ButtonSmall)) showEvents = false;
            if (events.Count == 0) Ui.Text(new Rect(r.x + 12, r.y + 38, r.width - 24, 24), "Nothing planned. Set a maneuver or a target.", Ui.Small);
            double half = node != null && node.valid ? ManeuverNode.BurnTime(active, node.DeltaV) / 2 : 0;
            if (double.IsInfinity(half)) half = 0;
            for (int i = 0; i < rows; i++)
            {
                var e = events[i];
                float y = r.y + 36 + i * 30;
                Ui.Text(new Rect(r.x + 12, y + 4, 280, 20), Words.Say(e.title), Ui.Small);
                Ui.Text(new Rect(r.x + 296, y + 4, 90, 20), Ui.Time(e.ut - ut), Ui.Label);
                double lead = FlightEvents.Lead(e, half);
                GUI.enabled = e.ut - lead > ut + 1;
                if (Ui.Press(new Rect(r.xMax - 64, y + 2, 54, 24), "Warp", Ui.ButtonSmall))
                {
                    if (e.kind == 0) WarpToNode(); else WarpTo(e.ut - lead, e.title);
                }
                GUI.enabled = true;
            }
        }

        Vector2 vesselScroll;
        bool vesselListDebris;

        /// <summary>Every vessel in the universe: switch to it or target it.</summary>
        void DrawVesselList(Rect r)
        {
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 12, r.y + 6, 200, 22), "Vessels", Ui.Header);
            if (Ui.Press(new Rect(r.xMax - 150, r.y + 6, 110, 22), vesselListDebris ? "Hide debris" : "Show debris", Ui.ButtonSmall)) vesselListDebris = !vesselListDebris;
            if (Ui.Press(new Rect(r.xMax - 34, r.y + 6, 24, 22), "x", Ui.ButtonSmall)) showVessels = false;
            var list = new List<VesselSim>();
            foreach (var u in U.vessels) if (u != active && !u.destroyed && (vesselListDebris || !u.isDebris || u == targetVessel)) list.Add(u);
            list.Sort((a, b) => Distance(a).CompareTo(Distance(b)));
            var view = new Rect(r.x + 6, r.y + 34, r.width - 12, r.height - 40);
            vesselScroll = GUI.BeginScrollView(view, vesselScroll, new Rect(0, 0, view.width - 18, Math.Max(view.height - 4, list.Count * 50)));
            float y = 0;
            if (list.Count == 0) Ui.Text(new Rect(6, 4, view.width - 30, 40), "No other vessels yet. Launch something and leave it in orbit, then come back with a second ship.", Ui.Small);
            foreach (var u in list)
            {
                bool isT = u == targetVessel;
                Ui.Text(new Rect(6, y + 2, 250, 20), $"<b>{u.name}</b>{(u.isDebris ? "  <size=11>(debris)</size>" : u.IsRock ? $"  <size=11>({u.parts[0].def.dryMass:0} t)</size>" : "")}", Ui.Label);
                Ui.Text(new Rect(6, y + 22, 260, 20), $"{u.Situation(ut)}   {Ui.Dist(Distance(u))}", Ui.Small);
                if (Ui.Press(new Rect(view.width - 150, y + 6, 64, 28), isT ? "Untarget" : "Target", isT ? Ui.ButtonSmallSel : Ui.ButtonSmall)) SetTarget(u);
                GUI.enabled = (!u.isDebris || u.IsControllable) && !u.IsFlag && !u.IsRock;
                if (Ui.Press(new Rect(view.width - 82, y + 6, 60, 28), "Fly", Ui.ButtonSmall)) { SwitchTo(u); showVessels = false; }
                GUI.enabled = true;
                y += 50;
            }
            GUI.EndScrollView();
        }

        double Distance(VesselSim u) => Vector3d.Distance(u.AbsolutePosition(ut), active.AbsolutePosition(ut));

        static void DrawVBar(Rect r, float frac, Color c)
        {
            Ui.Line(r, new Color(1, 1, 1, 0.1f));
            float h = r.height * Mathf.Clamp01(frac);
            Ui.Line(new Rect(r.x, r.yMax - h, r.width, h), c);
        }

        float DrawStages(float W, VesselSim v)
        {
            float sh = 58 + Math.Max(1, reports.Count) * 42;
            var sr = new Rect(W - 390, 10, 380, sh);
            Ui.Box(sr);
            Ui.Text(new Rect(sr.x + 12, sr.y + 6, 300, 22), Words.T("Stages", "Stages", "Steps"), Ui.Header);
            float sy = sr.y + 32;
            double totalDv = 0;
            double pressure = v.body.PressureAtm(v.Altitude);
            foreach (var rep in reports)
            {
                double dvNow = rep.dvVac + (rep.dvAsl - rep.dvVac) * pressure;
                totalDv += dvNow;
                bool current = rep.number == v.nextStage;
                string mark = current ? "> " : (rep.number == v.nextStage + 1 ? "next " : "");
                Ui.Text(new Rect(sr.x + 12, sy, 360, 20), $"{mark}<b>{Words.T("Stage", "Stage", "Step")} {rep.number}</b>  {Words.Say(rep.label)}", current ? Ui.Label : Ui.Small);
                if (rep.hasEngines) Ui.Text(new Rect(sr.x + 24, sy + 19, 340, 20), Words.T($"dv {dvNow:0} m/s   TWR {rep.twrVac:0.00}   {Ui.Time(rep.burnTime)}",
                    $"speed budget {dvNow:0} m/s   push/weight {rep.twrVac:0.0}   burns {Ui.Time(rep.burnTime)}", $"engine runs for {Ui.Time(rep.burnTime)}"), Ui.Small);
                sy += 42;
            }
            if (reports.Count == 0) Ui.Text(new Rect(sr.x + 12, sy, 300, 20), Words.T("No stages left", "No stages left", "No steps left"), Ui.Small);
            Ui.Text(new Rect(sr.x + 12, sr.yMax - 26, 340, 20), Words.T($"Total dv left: <b>{totalDv:0} m/s</b>", $"Speed budget left: <b>{totalDv:0} m/s</b>", $"Rocket power left: <b>{totalDv:0}</b>"), Ui.Label);
            return sr.yMax;
        }

        void DrawManeuverPanel(Rect r)
        {
            Ui.Box(r);
            var v = active;
            double burn = ManeuverNode.BurnTime(v, node.DeltaV);
            double toNode = node.ut - ut;
            double startIn = toNode - (double.IsInfinity(burn) ? 0 : burn / 2);
            Ui.Text(new Rect(r.x + 12, r.y + 6, 200, 22), "Maneuver", Ui.Header);
            if (Ui.Press(new Rect(r.xMax - 70, r.y + 6, 60, 22), "Delete", Ui.ButtonSmall)) { DeleteNode(); return; }
            bool burning = startIn < 0;
            double remaining = node.Remaining(v, ut, burning).magnitude;
            Ui.Text(new Rect(r.x + 12, r.y + 30, r.width - 24, 20),
                $"dv {(burning ? remaining : node.DeltaV):0.0} m/s   burn {(double.IsInfinity(burn) ? "no thrust!" : Ui.Time(burn))}", Ui.Bold);
            Ui.Text(new Rect(r.x + 12, r.y + 50, r.width - 24, 20),
                startIn > 0 ? $"Start burn in {Ui.Time(startIn)} (node in {Ui.Time(toNode)})" : "<color=#ffd060>BURN NOW</color> - point at the blue marker (8)", Ui.Label);

            float y = r.y + 76;
            DvRow(ref node.prograde, "Prograde", new Color(1f, 0.85f, 0.15f), r, ref y);
            DvRow(ref node.normal, "Normal", new Color(0.95f, 0.35f, 0.95f), r, ref y);
            DvRow(ref node.radial, "Radial out", new Color(0.35f, 0.9f, 1f), r, ref y);

            float bx = r.x + 12;
            Ui.Text(new Rect(bx, y + 4, 40, 20), "Time", Ui.Small);
            bx += 44;
            foreach (var (txt, d) in new[] { ("-1m", -60.0), ("-10s", -10.0), ("+10s", 10.0), ("+1m", 60.0) })
            {
                if (Ui.Press(new Rect(bx, y + 2, 48, 22), txt, Ui.ButtonSmall)) { node.ut = Math.Max(ut + 5, node.ut + d); node.Update(trajectory); }
                bx += 52;
            }
            if (trajectory.Count > 0 && trajectory[0].orbit.IsElliptic && Ui.Press(new Rect(bx, y + 2, 92, 22), "+1 orbit", Ui.ButtonSmall))
            {
                node.ut += trajectory[0].orbit.Period;
                node.Update(trajectory);
            }
            y += 28;
            if (Ui.Press(new Rect(r.x + 12, y + 2, 170, 26), "Warp to maneuver", Ui.Button)) WarpToNode();
            if (Ui.Press(new Rect(r.x + 190, y + 2, 178, 26), "Point at it (SAS)", Ui.Button)) SetSas(SasMode.Maneuver);
            y += 32;
            if (Ui.Press(new Rect(r.x + 12, y + 2, 356, 26), autoBurn ? "Auto-burn ON (the computer flies it; X stops)" : "Auto-burn: let the computer fly it", autoBurn ? Ui.ButtonSel : Ui.Button)) ToggleAutoBurn();
            if (laterNodes.Count > 0)
            {
                var nx = laterNodes[0];
                Ui.Text(new Rect(r.x + 12, y + 32, r.width - 24, 20), $"Then: {nx.DeltaV:0} m/s in {Ui.Time(nx.ut - ut)}" + (laterNodes.Count > 1 ? $" (+{laterNodes.Count - 1} more)" : ""), Ui.Small);
            }

            // Result summary
            string res = PlannedSummary();
            if (res != null) Ui.Text(new Rect(r.x + 12, r.yMax + 4, r.width - 24, 40), res, Ui.Small);
        }

        void DvRow(ref double value, string name, Color c, Rect r, ref float y)
        {
            var old = GUI.color;
            GUI.color = c;
            Ui.Text(new Rect(r.x + 12, y + 3, 80, 20), name, Ui.Small);
            GUI.color = old;
            float bx = r.x + 88;
            foreach (var (txt, d) in new[] { ("-10", -10.0), ("-1", -1.0), ("-0.1", -0.1) })
            {
                if (Ui.Press(new Rect(bx, y + 1, 34, 22), txt, Ui.ButtonSmall)) { value += d; node.Update(trajectory); }
                bx += 36;
            }
            Ui.Text(new Rect(bx + 2, y + 2, 60, 20), $"{value:0.0}", Ui.Bold);
            bx += 62;
            foreach (var (txt, d) in new[] { ("+0.1", 0.1), ("+1", 1.0), ("+10", 10.0) })
            {
                if (Ui.Press(new Rect(bx, y + 1, 34, 22), txt, Ui.ButtonSmall)) { value += d; node.Update(trajectory); }
                bx += 36;
            }
            y += 26;
        }

        string PlannedSummary()
        {
            if (node == null || !node.valid || node.planned.Count == 0) return null;
            if (targetVessel != null && !double.IsNaN(plannedClosestDist))
                return $"After the burn: closest approach to {targetVessel.name} {Ui.Dist(plannedClosestDist)}, in {Ui.Time(plannedClosestTime - ut)}, at {plannedClosestRel:0} m/s";
            var p0 = node.planned[0];
            var b = p0.body;
            if (p0.end == SegmentEnd.EnterSoi && node.planned.Count > 1)
            {
                var s1 = node.planned[1];
                double pe = s1.orbit.Periapsis - s1.body.radius;
                return pe > 0 ? $"After the burn: {s1.body.name} encounter, closest {Ui.Dist(pe)}, in {Ui.Time(p0.tEnd - ut)}"
                              : $"After the burn: impact on {s1.body.name} in {Ui.Time(p0.tEnd - ut)}";
            }
            if (p0.end == SegmentEnd.ExitSoi) return $"After the burn: escape {b.name}'s gravity in {Ui.Time(p0.tEnd - ut)}";
            if (p0.end == SegmentEnd.Impact) return $"After the burn: impact on {b.name}";
            return $"After the burn: Ap {Ui.Dist(p0.orbit.Apoapsis - b.radius)}, Pe {Ui.Dist(p0.orbit.Periapsis - b.radius)}";
        }

        void DrawEndPanel(string title, string detail)
        {
            if (Coop.Active) { DrawCoopPause(Ui.W, Ui.H); return; }
            float W = Ui.W, H = Ui.H;
            float ph = paused && !active.destroyed ? 560 : active.destroyed && CanReplay ? 382 : 346;
            var r = new Rect(W / 2 - 190, Mathf.Min(H / 2 - ph / 2, H - 266 - ph), 380, ph);   // clear of the navball
            Ui.Box(r);
            Ui.Text(new Rect(r.x, r.y + 12, r.width, 30), title, Ui.BigCenter);
            Ui.Text(new Rect(r.x + 16, r.y + 46, r.width - 32, 40), Words.Say(detail), Ui.Center);
            float y = r.y + 92;
            if (paused && !active.destroyed)
            {
                float hw0 = (r.width - 88) / 2;
                if (Ui.Press(new Rect(r.x + 40, y, hw0, 30), "Resume", Ui.Button)) paused = false;
                if (Ui.Press(new Rect(r.x + 48 + hw0, y, hw0, 30), "Photo mode (F2)", Ui.Button)) { paused = false; TogglePhoto(); return; }
                y += 36;
                if (Ui.Press(new Rect(r.x + 40, y, hw0, 30), $"Hide UI ({In.Name(K.F1)})", Ui.Button)) { paused = false; Ui.ToggleHidden(); return; }
                if (Ui.Press(new Rect(r.x + 48 + hw0, y, hw0, 30), "Definitions", Ui.Button)) Glossary.Open = true;
                y += 36;
                float vol = FlightAudio.Volume;
                Ui.Text(new Rect(r.x + 40, y + 5, 130, 22), $"Sound: {(vol < 0.01f ? "off" : $"{vol * 100:0}%")}", Ui.Label);
                if (Ui.Press(new Rect(r.xMax - 150, y, 50, 28), "-", Ui.Button)) FlightAudio.Volume = Mathf.Round((vol - 0.1f) * 10) / 10;
                if (Ui.Press(new Rect(r.xMax - 94, y, 50, 28), "+", Ui.Button)) FlightAudio.Volume = Mathf.Round((vol + 0.1f) * 10) / 10;
                y += 36;
                float mus = GameAudio.MusicVolume;
                Ui.Text(new Rect(r.x + 40, y + 5, 130, 22), $"Music: {(mus < 0.01f ? "off" : $"{mus * 100:0}%")}", Ui.Label);
                if (Ui.Press(new Rect(r.xMax - 150, y, 50, 28), "-", Ui.Button)) GameAudio.MusicVolume = Mathf.Round((mus - 0.1f) * 10) / 10;
                if (Ui.Press(new Rect(r.xMax - 94, y, 50, 28), "+", Ui.Button)) GameAudio.MusicVolume = Mathf.Round((mus + 0.1f) * 10) / 10;
                y += 36;
                float hw = (r.width - 88) / 2;
                if (Ui.Press(new Rect(r.x + 40, y, hw, 30), "Quicksave (F5)", Ui.Button)) { QuickSave(); paused = false; }
                GUI.enabled = HasQuicksave;
                if (Ui.Press(new Rect(r.x + 48 + hw, y, hw, 30), "Quickload (F9)", Ui.Button)) { QuickLoad(); GUI.enabled = true; return; }
                GUI.enabled = true;
                y += 36;
                bool guideOn = Guide.Showing(U.career.guide);
                if (Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), guideOn ? "Hide the first-flight guide" : "Show the first-flight guide", Ui.Button))
                {
                    U.career.guide = guideOn ? Guide.Closed : active.hasLaunched ? Guide.Climb : Guide.Ignite;
                    U.Save();
                    paused = false;
                }
                y += 36;
                Ui.WordsPicker(r.x + 40, y, (r.width - 88) / 3);
                y += 36;
                float hw2 = (r.width - 88) / 2;
                if (Ui.Press(new Rect(r.x + 40, y, hw2, 30), "How to play", Ui.Button)) showHowTo = true;
                if (Ui.Press(new Rect(r.x + 48 + hw2, y, hw2, 30), $"Badges ({U.career.badges.Count})", Ui.Button)) showBadges = true;
                y += 36;
                GUI.enabled = CanReplay;
                if (Ui.Press(new Rect(r.x + 40, y, hw2, 30), "Watch replay", Ui.Button)) { StartReplay(); GUI.enabled = true; return; }
                GUI.enabled = PracticeAllowed;
                if (Ui.Press(new Rect(r.x + 48 + hw2, y, hw2, 30), PracticeAllowed ? "Practice tools" : "Practice (sandbox)", Ui.Button)) { paused = false; showPractice = true; GUI.enabled = true; return; }
                GUI.enabled = true;
                y += 36;
            }
            if (active.destroyed && CanReplay)
            {
                if (Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), "Watch the replay", Ui.Button)) { StartReplay(); return; }
                y += 36;
            }
            if (active.destroyed && HasQuicksave)
            {
                if (Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), "Load the quicksave (F9)", Ui.Button)) { QuickLoad(); return; }
                y += 36;
            }
            if (CanRevert)
            {
                if (Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), "Revert to launch", Ui.Button)) { root.Revert(design, launchRunway); return; }
                y += 36;
            }
            if (CanRecover)
            {
                if (Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), $"Recover {active.name}", Ui.Button)) { Recover(); return; }
                y += 36;
            }
            else if (active.destroyed && U.vessels.Count > 1)
            {
                if (Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), "Fly another vessel (V)", Ui.Button)) showVessels = true;
                y += 36;
            }
            if (Ui.Press(new Rect(r.x + 40, y, r.width - 80, 30), active.destroyed || active.landed ? "Back to the builder" : "Back to the builder (it stays out there)", Ui.Button))
                root.EnterHangar();
        }
    }
}
