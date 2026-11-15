using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// The title screen: continue the last game, start a new one (career or sandbox, with a name), load or delete saved
    /// games, and settings. Behind it (milestone 23) the camera drifts round the space center on a sunny morning: the
    /// last rocket built stands on the pad, with the assembly building, the campus and the airport beyond.
    /// </summary>
    public class MenuMode
    {
        readonly GameRoot root;
        enum Page { Main, New, Load, Settings, Credits, Help, Controls, Single, Updates, Defs }
        Page page;
        List<SlotInfo> slots = new List<SlotInfo>();
        string newName = "", confirmDelete;
        GameMode newMode = GameMode.Career;
        bool newSupplies = true;
        Vector2 loadScroll;
        static GUIStyle selCentered;
        double t0;
        float enterTime;
        VesselView padRocket;
        float rocketHeight = 20f;

        public MenuMode(GameRoot root) { this.root = root; }

        /// <summary>For screenshot tours: hide the menu, and put the camera anywhere round the space center
        /// (eye east, north, height, then target east, north, height, in metres from the pad).</summary>
        public bool hideGui;
        public double[] shot;
        /// <summary>For screenshot tours: seconds added to the title screen's clock (half a day shows the site at night).</summary>
        public double timeShift;

        public void Enter()
        {
            page = Page.Main;
            slots = SaveSlots.List();
            confirmDelete = null;
            t0 = MorningAt(root.home, 1.9e6);
            enterTime = Time.unscaledTime;
            Leave();
        }

        /// <summary>The first time after <paramref name="from"/> that the sun stands about 32 degrees up over the pad, climbing.</summary>
        static double MorningAt(CelestialBody home, double from)
        {
            double Elev(double t)
            {
                Vector3d up = home.FixedToInertial(LaunchSite.Direction, t).normalized;
                return Vector3d.Dot(up, Sun.DirectionFrom(home.AbsolutePosition(t), t));
            }
            double target = System.Math.Sin(32 * System.Math.PI / 180);
            double prev = Elev(from);
            for (double t = from + 30; t < from + home.rotationPeriod * 1.05; t += 30)
            {
                double e = Elev(t);
                if (prev < target && e >= target) return t;
                prev = e;
            }
            return from;
        }

        /// <summary>Takes the rocket off the pad (leaving the title screen).</summary>
        public void Leave()
        {
            padRocket?.Destroy();
            padRocket = null;
            centre = false;
        }

        // ---------------------------------------------------------------- the space centre, clickable (milestone 25)

        /// <summary>The space centre seen from the air, each building labelled; clicking one goes where it leads.</summary>
        public bool centre;
        float centreYaw = 205f, centreDist = 1250f;
        string centreHover;

        struct Place { public string name, does; public double e, n; public float h; public System.Action go; }

        Place[] Places()
        {
            var h = root.hangar;
            System.Action Builder(System.Action then) => () => { root.EnterHangar(); then?.Invoke(); };
            var list = new List<Place>
            {
                new Place { name = "Assembly building", does = "Build rockets", e = 0, n = 340, h = 92, go = Builder(() => h.PlaneView(false)) },
                new Place { name = "Airport hangar", does = "Build planes", e = -300, n = 460, h = 34, go = Builder(() => h.PlaneView(true)) },
                new Place { name = "Mission control", does = "Missions and contracts", e = 170, n = 140, h = 26, go = Builder(() => h.ShowMissions(true)) },
                new Place { name = "Tracking station", does = "Your ships out there", e = -200, n = 1100, h = 34, go = Builder(() => h.ShowVessels(true)) },
                new Place { name = "Research campus", does = "The tech tree", e = -1228, n = 190, h = 30, go = Builder(() => h.ShowTech(true)) },
                new Place { name = "Headquarters", does = "Facilities and upgrades", e = -181, n = 263, h = 30, go = Builder(() => h.ShowCenter(true)) },
                new Place { name = "Astronaut complex", does = "Your crew", e = 150, n = 320, h = 22, go = Builder(() => h.ShowCrew(true)) },
                new Place { name = "Visitor centre", does = "Badges you've earned", e = 655, n = 1218, h = 40, go = Builder(() => h.ShowBadges(true)) },
                new Place { name = "Supply store", does = "Food, kit, your locker; sell finds", e = -48, n = 307, h = 14, go = () => StorePanel.Open = true },   // milestone 38
            };
            for (int i = 0; i < LaunchSite.Pads.Length; i++)
            {
                int pad = i;
                var pd = LaunchSite.Pads[i];
                list.Add(new Place { name = pd.name, does = "Launch from here", e = pd.e, n = pd.n, h = 48, go = Builder(() => h.SetLaunchPad(pad)) });
            }
            return list.ToArray();
        }

        public void OpenCentre()
        {
            centre = true;
            shot = null;
            hideGui = false;
        }

        /// <summary>Moves the camera round the space centre: right-drag turns, scroll zooms, it drifts slowly on its own.</summary>
        void UpdateCentreCamera()
        {
            if (In.RightMouse) centreYaw += In.MouseDelta.x * 0.2f;
            else centreYaw += Time.unscaledDeltaTime * 1.2f;
            float sc = In.Scroll;
            if (Mathf.Abs(sc) > 0.01f) centreDist = Mathf.Clamp(centreDist * (sc > 0 ? 1f / 1.12f : 1.12f), 500f, 3200f);
            double a = centreYaw * Mathf.Deg2Rad, cx = -150, cn = 420;
            shot = new double[] { cx + System.Math.Sin(a) * centreDist, cn + System.Math.Cos(a) * centreDist, centreDist * 0.48, cx, cn, 0 };
            if (In.EscapeDown && !StorePanel.Open) { root.EnterHangar(); }
        }

        void DrawCentre(float W, float H)
        {
            var bar = new Rect(W * 0.5f - 380, 14, 760, 54);
            GUI.Box(bar, GUIContent.none, Ui.Panel);
            Ui.Text(new Rect(bar.x + 16, bar.y + 8, 420, 24), "SPACE CENTRE", Ui.Header);
            bool first = root.universe != null && root.universe.vessels.Count == 0;
            Ui.Text(new Rect(bar.x + 16, bar.y + 30, 420, 20), first ? "Click the <b>Assembly building</b> to build your first rocket." : "Click a building to go in. Right-drag to look round, scroll to zoom.", Ui.Small);
            if (Ui.Press(new Rect(bar.xMax - 300, bar.y + 10, 130, 34), "Title screen", Ui.Button)) { centre = false; root.ShowMenu(); return; }
            if (Ui.Press(new Rect(bar.xMax - 162, bar.y + 10, 146, 34), "Rocket builder", Ui.ButtonGo)) { root.EnterHangar(); return; }
            BodyView site = null;
            foreach (var bv in root.bodyViews) if (bv.SiteTransform != null) site = bv;
            if (site == null) return;
            var cam = root.farCam;
            Vector2 mouse = new Vector2(In.MousePosition.x / Ui.Scale, (Screen.height - In.MousePosition.y) / Ui.Scale);
            string hover = null;
            // where each label goes: above its building, nudged up until it clears the labels already placed (nearest first)
            var shown = new List<(Place p, Rect r, float fx, float fy, float depth)>();
            foreach (var p in Places())
            {
                Vector3 top = site.SiteWorld(p.e, p.n, p.h), foot = site.SiteWorld(p.e, p.n, 0);
                Vector3 sp = cam.WorldToScreenPoint(top), sf = cam.WorldToScreenPoint(foot);
                if (sp.z <= 0) continue;
                float x = sp.x / Ui.Scale, y = (Screen.height - sp.y) / Ui.Scale - 30, fy = (Screen.height - sf.y) / Ui.Scale;
                if (x < 0 || x > W || y < 70 || y > H) continue;
                shown.Add((p, new Rect(x - 80, y, 160, 28), x, fy, sp.z));
            }
            shown.Sort((a, c) => a.depth.CompareTo(c.depth));
            var placed = new List<Rect>();
            for (int i = 0; i < shown.Count; i++)
            {
                var r = shown[i].r;
                for (int k = 0; k < 12; k++)
                {
                    bool clash = false;
                    foreach (var q in placed) if (q.Overlaps(new Rect(r.x - 4, r.y - 3, r.width + 8, r.height + 6))) { clash = true; r.y = q.y - r.height - 6; break; }
                    if (!clash) break;
                }
                placed.Add(r);
                shown[i] = (shown[i].p, r, shown[i].fx, shown[i].fy, shown[i].depth);
            }
            foreach (var (p, r0, fx, fy, _) in shown)
            {
                // a thin line down to the building, then the label (bigger, with what it does, under the mouse)
                var was = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                GUI.DrawTexture(new Rect(fx - 1, r0.yMax, 2, Mathf.Max(0, fy - r0.yMax)), Texture2D.whiteTexture);
                GUI.color = was;
                bool over = centreHover == p.name;
                var r = over ? new Rect(r0.center.x - 110, r0.y - 18, 220, 46) : r0;
                if (r.Contains(mouse)) hover = p.name;
                if (Ui.Press(r, over ? $"{p.name}\n<size=11>{p.does}</size>" : p.name, over ? Ui.ButtonSmallSel : Ui.ButtonSmall)) { p.go(); return; }
            }
            centreHover = hover;
        }

        /// <summary>The last design built, standing on the pad (a model only, not part of any game).</summary>
        void PlaceRocket(BodyView site)
        {
            var t = site.SiteTransform;
            if (t == null) return;
            var d = DesignStore.LoadLast() ?? VesselDesign.Default();
            padRocket = VesselView.FromNodes(d.nodes, "Title rocket", false);
            var go = padRocket.root;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { lo = Mathf.Min(lo, r.bounds.min.y); hi = Mathf.Max(hi, r.bounds.max.y); }
            if (lo > hi) { lo = 0; hi = 10; }
            rocketHeight = Mathf.Max(6f, hi - lo);
            go.transform.SetParent(t, false);
            go.transform.localPosition = new Vector3(0, (float)LaunchSite.PadHeight - lo, 0);
            go.transform.localRotation = Quaternion.identity;
        }

        public void ShowPage(string name)
        {
            page = name == "single" ? Page.Single : name == "new" ? Page.New : name == "load" ? Page.Load : name == "settings" ? Page.Settings : name == "credits" ? Page.Credits : name == "help" ? Page.Help : name == "controls" ? Page.Controls : name == "updates" ? Page.Updates : name == "defs" ? Page.Defs : Page.Main;
            slots = SaveSlots.List();
            if (page == Page.New) newName = $"Game {slots.Count + 1}";
        }

        /// <summary>
        /// The space center from a little way south of the pad, swinging slowly from side to side, the pad to the right
        /// of the menu. Time runs a few times faster than real, so the clouds drift and the shadows creep.
        /// </summary>
        public void Update()
        {
            float real = Time.unscaledTime - enterTime;
            double t = t0 + real * 4.0 + timeShift;
            var home = root.home;
            Vector3d homeAbs = home.AbsolutePosition(t);
            Vector3d up = home.FixedToInertial(LaunchSite.Direction, t).normalized;
            Vector3d north = home.FixedToInertial(CelestialBody.NorthAt(LaunchSite.Direction), t).normalized;
            Vector3d east = home.FixedToInertial(CelestialBody.EastAt(LaunchSite.Direction), t).normalized;
            Vector3d padAbs = homeAbs + up * (home.radius + LaunchSite.PadHeight);
            BodyView siteView = null;
            foreach (var bv in root.bodyViews)
            {
                bv.Sync(padAbs, t, padAbs);
                if (bv.SiteTransform != null) siteView = bv;
            }
            if (padRocket == null && siteView != null) PlaceRocket(siteView);

            if (centre) UpdateCentreCamera();
            // Camera: south of the pad, looking north past the rocket to the assembly building and campus
            float swing = -36f + 14f * Mathf.Sin(real * 0.022f);
            double a = swing * Mathf.Deg2Rad;
            double dist = 70 + rocketHeight * 2.6, height = 9 + rocketHeight * 0.4;
            Vector3d h = -north * System.Math.Cos(a) + east * System.Math.Sin(a);
            Vector3 camPos = (h * dist + up * height).ToFloat();
            Vector3 target = (up * (rocketHeight * 0.5) + north * 40).ToFloat();
            Vector3 upF = up.ToFloat();
            Vector3 look = (target - camPos).normalized;
            Vector3 right = Vector3.Cross(upF, look).normalized;
            look = (look - right * 0.36f).normalized;    // the pad sits right of centre, clear of the menu
            if (shot != null)
            {
                camPos = (east * shot[0] + north * shot[1] + up * shot[2]).ToFloat();
                look = ((east * shot[3] + north * shot[4] + up * shot[5]).ToFloat() - camPos).normalized;
            }
            var cam = root.farCam;
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.LookRotation(look, upF);
            cam.nearClipPlane = 5f;
            cam.farClipPlane = 1e9f;
            cam.cullingMask = 1 << GameRoot.LayerFar;
            cam.backgroundColor = Color.black;
            root.nearCam.enabled = true;

            Vector3d sunDir = Sun.DirectionFrom(homeAbs, t);
            GameRoot.SunNow = sunDir.ToFloat();
            root.sun.transform.rotation = Quaternion.LookRotation(-GameRoot.SunNow);
            Vector3d eyeRel = padAbs - homeAbs + Vector3d.FromFloat(camPos);
            RenderEnv.UpdateWorld(home, eyeRel, (homeAbs - padAbs).ToFloat(), GameRoot.SunNow, root.sun, true);
            RenderEnv.UpdateShine(home, homeAbs + eyeRel, t, root.bodies);
            Vector3 tSun = RenderEnv.SunTransmittance(home, eyeRel, GameRoot.SunNow);
            float tAvg = (tSun.x + tSun.y + tSun.z) / 3f;
            SkyView.SunTint = tAvg > 1e-4f ? new Color(Mathf.Min(tSun.x / tAvg, 3f), Mathf.Min(tSun.y / tAvg, 3f), Mathf.Min(tSun.z / tAvg, 3f)) : Color.white;
            SkyView.StarExposure = 1f + RenderEnv.Exposure * 0.5f;
            float sunUp = Vector3.Dot(upF, GameRoot.SunNow);
            float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.2f, 0.25f, sunUp));
            root.sky?.Sync(cam, 1f - Mathf.Clamp01(day * 6f));
            Mats.Lit.SetFloat("_Ambient", 0.24f);
            GameAudio.I?.Set(Mood.Title, sunUp > 0 ? Ambience.SiteDay : Ambience.SiteNight);
        }

        static Texture2D shade;

        public void OnGUI()
        {
            if (hideGui) return;
            float W = Ui.W, H = Ui.H;
            if (centre) { DrawCentre(W, H); return; }
            // A soft dark wash down the left, so the menu reads against a bright sky
            if (shade == null)
            {
                shade = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int i = 0; i < 256; i++) { float u = i / 255f; shade.SetPixel(i, 0, new Color(0.02f, 0.04f, 0.08f, 0.7f * Mathf.Pow(1f - u, 1.5f))); }
                shade.Apply();
            }
            GUI.DrawTexture(new Rect(0, 0, Mathf.Max(820f, W * 0.55f), H), shade);
            // milestone 34: the logo higher up, the menu as one column of big buttons, what's new on the right
            float x = 64, y = Mathf.Max(54f, H * 0.1f);
            Ui.Text(new Rect(x, y, 1000, 64), "ARDEN SPACE PROGRAM", Ui.Hero);
            Ui.Line(new Rect(x + 3, y + 66, 120, 3), Ui.Accent);
            var tag = new GUIStyle(Ui.Label) { fontSize = 16 };
            tag.normal.textColor = new Color(0.88f, 0.93f, 0.97f);
            Ui.Shadowed(new Rect(x + 3, y + 74, 600, 24), "Build rockets. Fly them. Reach the giant.", tag);
            y += 120;
            switch (page)
            {
                case Page.Main: DrawMain(x, y, W, H); break;
                case Page.Single: DrawSingle(x, y); break;
                case Page.New: DrawNew(x, y, W, H); break;
                case Page.Load: DrawLoad(x, y, W, H); break;
                case Page.Settings: DrawSettings(x, y); break;
                case Page.Credits: DrawCredits(x, y); break;
                case Page.Help: if (!HowToPlay.Draw(new Rect(x, y, Mathf.Min(980, W - x - 40), Mathf.Min(470, H - y - 40)), ref helpTopic)) ShowPage("main"); break;
                case Page.Controls: DrawControls(x, y, W, H); break;
                case Page.Updates: if (!UpdateLog.DrawPage(new Rect(x, y, Mathf.Min(820, W - x - 40), Mathf.Min(640, H - y - 50)))) ShowPage("main"); break;
                case Page.Defs: if (!Glossary.DrawPage(new Rect(x, y, Mathf.Min(980, W - x - 40), Mathf.Min(640, H - y - 50)))) ShowPage("main"); break;
            }
            var foot = new GUIStyle(Ui.Small);
            foot.normal.textColor = new Color(0.85f, 0.9f, 0.95f);
            Ui.Shadowed(new Rect(x, H - 34, 600, 20), $"Saves: {slots.Count}   ·   designs are shared between games", foot);
            int baked = PlanetBaker.Baked;
            string worlds = baked < 8 ? $"drawing the worlds ({baked}/8)   ·   " : "";
            Ui.Shadowed(new Rect(W - 620, H - 34, 490, 20), $"{worlds}Update {UpdateLog.Latest.n}   ·   version {Application.version}", foot.ApplyRight());
            // milestone 35: the credits, tucked away in the corner (they also roll when you finish the story)
            var link = new GUIStyle(foot) { alignment = TextAnchor.MiddleCenter };
            link.hover.textColor = Ui.Accent;
            if (page == Page.Main && Ui.Press(new Rect(W - 120, H - 36, 90, 24), "Credits", link)) EndCredits.Start();
        }

        /// <summary>
        /// The main title menu (milestone 34, after looking at how good game menus work): Continue first and biggest, saying
        /// exactly which game it opens; then one column of buttons, each with a line saying what it's for; the newest
        /// update's highlights on the right.
        /// </summary>
        void DrawMain(float x, float y, float W, float H)
        {
            string last = SaveSlots.Last;
            var lastInfo = slots.FirstOrDefault(s => s.id == last);
            const float w = 390, h = 52, gap = 8;
            if (lastInfo != null)
            {
                if (Ui.MenuItem(new Rect(x, y, w, 62), $"Continue  ▸  {lastInfo.name}", Describe(lastInfo), true)) { root.OpenSlot(last); return; }
            }
            else if (Ui.MenuItem(new Rect(x, y, w, 62), "Start playing  ▸", "A new campaign: run your own space program", true)) { newMode = GameMode.Career; newSupplies = true; ShowPage("new"); return; }
            y += 62 + gap + 6;
            if (Ui.MenuItem(new Rect(x, y, w, h), "Singleplayer", "A campaign, a sandbox, or a saved game")) ShowPage("single");
            y += h + gap;
            if (Ui.MenuItem(new Rect(x, y, w, h), "Multiplayer", "Build and fly together: up to 4 players")) { root.ShowMission(); return; }
            y += h + gap;
            if (Ui.MenuItem(new Rect(x, y, w, h), "How to play", "Guides for building, flying and more")) ShowPage("help");
            y += h + gap;
            if (Ui.MenuItem(new Rect(x, y, w, h), "Definitions", "What every game word means")) ShowPage("defs");
            y += h + gap;
            if (Ui.MenuItem(new Rect(x, y, w, h), "Settings", "Words, interface, sound, graphics, controls")) ShowPage("settings");
            y += h + gap;
            if (Ui.MenuItem(new Rect(x, y, w, h), "Update log", UpdateLog.Unread ? $"<color=#ffd060>New:</color> update {UpdateLog.Latest.n}, {UpdateLog.Latest.name.ToLowerInvariant()}" : $"Everything new, up to update {UpdateLog.Latest.n}")) ShowPage("updates");
            y += h + gap;
            if (Ui.MenuItem(new Rect(x, y, w, 40), "Quit", null)) Application.Quit();   // (milestone 35: Credits is a small link in the corner now)
            // what's new, bottom right
            float cw = 430, ch = Mathf.Min(330, H - 200);
            if (UpdateLog.DrawCard(new Rect(W - cw - 40, H - ch - 56, cw, ch), 6)) ShowPage("updates");
        }

        /// <summary>Singleplayer: a campaign, a sandbox or a saved game. (Mission design lives under Multiplayer: a room on your own works too.)</summary>
        void DrawSingle(float x, float y)
        {
            const float w = 390, h = 56, gap = 8;
            Ui.Text(new Rect(x, y - 6, 400, 30), "SINGLEPLAYER", Ui.Header);
            y += 30;
            if (Ui.MenuItem(new Rect(x, y, w, h), "Campaign", "Contracts pay for rockets, science unlocks parts")) { newMode = GameMode.Career; newSupplies = true; ShowPage("new"); }
            y += h + gap;
            if (Ui.MenuItem(new Rect(x, y, w, h), "Sandbox", "Every part, no money: build and fly anything")) { newMode = GameMode.Sandbox; newSupplies = false; ShowPage("new"); }
            y += h + gap;
            GUI.enabled = slots.Count > 0;
            if (Ui.MenuItem(new Rect(x, y, w, h), "Load a game", slots.Count > 0 ? $"{slots.Count} saved game{(slots.Count == 1 ? "" : "s")}" : "No saved games yet")) ShowPage("load");
            GUI.enabled = true;
            y += h + gap + 8;
            if (Ui.Press(new Rect(x, y, 140, 40), "Back", Ui.Button) || In.EscapeDown) ShowPage("main");
        }

        static GUIStyle blurbStyle;
        /// <summary>A line or two beside a title button, on a dark backing so it reads over the bright sky.</summary>
        static void Blurb(Rect r, string text)
        {
            blurbStyle ??= new GUIStyle(Ui.Small) { wordWrap = true };
            var c = new GUIContent(text);
            float tw = Mathf.Min(r.width, blurbStyle.CalcSize(c).x + 2);
            float th = blurbStyle.CalcHeight(c, tw);
            Ui.Box(new Rect(r.x - 8, r.y - 4, tw + 16, th + 8));
            Ui.Text(new Rect(r.x, r.y, tw, th), text, blurbStyle);
        }

        static string Describe(SlotInfo s)
        {
            string mode = !s.chosen ? "new" : s.mode == GameMode.Career ? $"career, {s.funds:N0} funds, {s.science:0} science" : "sandbox";
            return $"{mode} · day {s.ut / 86400 + 1:0} · {s.vessels} vessel{(s.vessels == 1 ? "" : "s")} · {s.saved:MMM d, HH:mm}";
        }

        void DrawNew(float x, float y, float W, float H)
        {
            var r = new Rect(x, y, 640, 396);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 16, r.y + 12, 300, 24), newMode == GameMode.Career ? "New campaign" : "New sandbox", Ui.Header);
            Ui.Text(new Rect(r.x + 16, r.y + 286, 60, 22), "Words", Ui.Label);
            Ui.WordsPicker(r.x + 80, r.y + 282, 100);
            Ui.Text(new Rect(r.x + 396, r.y + 286, 230, 40), Words.Blurbs[(int)Words.Level], Ui.Small);
            Ui.Text(new Rect(r.x + 16, r.y + 48, 60, 24), "Name", Ui.Label);
            newName = GUI.TextField(new Rect(r.x + 80, r.y + 44, 300, 28), newName, 30, Ui.Field);
            float bw = 290;
            selCentered ??= new GUIStyle(Ui.RowSel) { alignment = TextAnchor.MiddleCenter };
            if (Ui.Press(new Rect(r.x + 16, r.y + 90, bw, 40), "Campaign", newMode == GameMode.Career ? selCentered : Ui.Button)) { newMode = GameMode.Career; newSupplies = true; }
            if (Ui.Press(new Rect(r.x + 334, r.y + 90, bw, 40), "Sandbox", newMode == GameMode.Sandbox ? selCentered : Ui.Button)) { newMode = GameMode.Sandbox; newSupplies = false; }
            Ui.Text(new Rect(r.x + 16, r.y + 136, bw, 100), "Run a space program: contracts pay for rockets, science unlocks parts in the tech tree, records and reputation open bigger missions. A guide walks you through the first flight.", Ui.Small);
            Ui.Text(new Rect(r.x + 334, r.y + 136, bw, 100), "Every part, no money, no contracts: build and fly anything, anywhere.", Ui.Small);
            newSupplies = GUI.Toggle(new Rect(r.x + 16, r.y + 244, 600, 24), newSupplies, newSupplies ? "  Crew need supplies (food, water, air): they die if they run out" : "  Crew need supplies (off: no limit on long trips)", Ui.Label);
            if (Ui.Press(new Rect(r.xMax - 176, r.yMax - 48, 160, 34), "Start  ▸", Ui.ButtonGo)) { root.NewGame(newName, newMode, newSupplies); return; }
            if (Ui.Press(new Rect(r.x + 16, r.yMax - 48, 100, 34), "Back", Ui.Button)) ShowPage("single");
        }

        void DrawLoad(float x, float y, float W, float H)
        {
            var r = new Rect(x, y, 700, Mathf.Min(H - y - 60, 120 + slots.Count * 58));
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 16, r.y + 12, 300, 24), "Load game", Ui.Header);
            var view = new Rect(r.x + 8, r.y + 44, r.width - 16, r.height - 100);
            loadScroll = GUI.BeginScrollView(view, loadScroll, new Rect(0, 0, view.width - 20, Math.Max(view.height - 4, slots.Count * 58)));
            float yy = 0;
            foreach (var s in slots.ToList())
            {
                Ui.Text(new Rect(8, yy + 4, 380, 22), $"<b>{s.name}</b>", Ui.Label);
                Ui.Text(new Rect(8, yy + 26, 460, 20), Describe(s), Ui.Small);
                if (confirmDelete == s.id)
                {
                    Ui.Text(new Rect(view.width - 290, yy + 14, 90, 22), "Delete it?", Ui.Small);
                    if (Ui.Press(new Rect(view.width - 200, yy + 10, 70, 28), "Yes", Ui.ButtonSmall)) { SaveSlots.Delete(s.id); confirmDelete = null; slots = SaveSlots.List(); GUI.EndScrollView(); return; }
                    if (Ui.Press(new Rect(view.width - 124, yy + 10, 70, 28), "No", Ui.ButtonSmall)) confirmDelete = null;
                }
                else
                {
                    if (Ui.Press(new Rect(view.width - 200, yy + 10, 80, 28), "Load", Ui.Button)) { GUI.EndScrollView(); root.OpenSlot(s.id); return; }
                    if (Ui.Press(new Rect(view.width - 112, yy + 10, 80, 28), "Delete", Ui.ButtonSmall)) confirmDelete = s.id;
                }
                yy += 58;
            }
            GUI.EndScrollView();
            if (Ui.Press(new Rect(r.x + 16, r.yMax - 48, 100, 34), "Back", Ui.Button)) ShowPage("single");
        }

        /// <summary>Who made the star map, textures, music and sounds (several are CC-BY: they must be credited).</summary>
        void DrawCredits(float x, float y)
        {
            var r = new Rect(x, y, 660, 520);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 16, r.y + 12, 300, 24), "Credits", Ui.Header);
            Ui.Text(new Rect(r.x + 16, r.y + 48, r.width - 32, 410), "Arden Space Program, made by captaincarl24.\n\nStar map by NASA/Goddard Space Flight Center Scientific Visualization Studio, from Gaia DR2 (ESA/Gaia/DPAC). Textures from ambientCG (CC0). Music: \"Space\" by lasercheese, \"ArcLight\" by Matthew Pablo, \"Beyond the Stars\" and \"Drifting Beyond the Stars\" by Hitctrl, \"Space Atmosphere\" by Alexandr Zhelanov (all CC-BY 3.0); \"Another August\" by cynicmusic, \"Exploration Theme\" by CleytonKauffman, \"At Home\" by Wolfgang_, \"Out There\" and \"Galactic Temple\" by yd, \"First Light Particles\" by Yoiyami (CC0). Sounds: explosions by Michel Baradari and the Blender Foundation, hangar hum by gryc (CC-BY 3.0); rumble by theMinesAreShakin, engine by gmason, wind by aquinn and remaxim, thrusters by EZduzziteh, metal by BMacZero, crickets by Wolfgang_, launch by qubodup, interface by Kenney (CC0). Found on OpenGameArt.org.", Ui.Small);
            if (Ui.Press(new Rect(r.x + 16, r.yMax - 48, 100, 34), "Back", Ui.Button)) ShowPage("main");
        }

        int helpTopic;
        /// <summary>For the screenshot tour: the guide open at a topic.</summary>
        public void TourHelp(int topic) { helpTopic = topic; ShowPage("help"); }
        K? rebinding;

        /// <summary>Every game key and the key it's on: click one, then press the new key (Esc cancels).</summary>
        void DrawControls(float x, float y, float W, float H)
        {
            var r = new Rect(x, y, Mathf.Min(900, W - x - 40), Mathf.Min(500, H - y - 30));
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 16, r.y + 12, 300, 24), "Controls", Ui.Header);
            Ui.Text(new Rect(r.x + 120, r.y + 15, r.width - 140, 20), rebinding.HasValue ? $"Press the new key for \"{Keybinds.Actions.First(a => a.key == rebinding.Value).what}\" (Esc cancels)" : "Click a key to change it.", Ui.Small);
            if (rebinding.HasValue)
            {
                if (In.EscapeDown) rebinding = null;
                else
                {
                    string pressed = In.AnyKeyDown();
                    if (pressed != null) { Keybinds.Set(rebinding.Value, pressed); rebinding = null; }
                }
            }
            int cols = 3, rows = (Keybinds.Actions.Length + cols - 1) / cols;
            float cw = (r.width - 32) / cols, rh = Mathf.Min(28, (r.height - 110) / rows);
            for (int i = 0; i < Keybinds.Actions.Length; i++)
            {
                var a = Keybinds.Actions[i];
                float cx = r.x + 16 + (i / rows) * cw, cy = r.y + 46 + (i % rows) * rh;
                bool waiting = rebinding == a.key;
                if (Ui.Press(new Rect(cx, cy, 64, rh - 4), waiting ? "..." : In.Name(a.key), waiting ? Ui.ButtonSmallSel : Ui.ButtonSmall)) rebinding = a.key;
                Ui.Text(new Rect(cx + 70, cy + 3, cw - 76, rh), a.what, Ui.Small);
            }
            var clash = Keybinds.Clashes();
            if (clash.Count > 0) Ui.Text(new Rect(r.x + 140, r.yMax - 44, r.width - 300, 40), "<color=#ffb050>Shared keys: " + string.Join("; ", clash.Take(2)) + "</color>", Ui.Small);
            if (Ui.Press(new Rect(r.x + 16, r.yMax - 48, 100, 34), "Back", Ui.Button)) { rebinding = null; ShowPage("settings"); }
            if (Ui.Press(new Rect(r.xMax - 150, r.yMax - 48, 134, 34), "Reset all", Ui.Button)) { Keybinds.ResetAll(); rebinding = null; }
        }

        void DrawSettings(float x, float y)
        {
            var r = new Rect(x, y, 700, 624);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 16, r.y + 12, 300, 24), "Settings", Ui.Header);
            float yy = r.y + 52;
            // How the game talks (milestone 24)
            Ui.Text(new Rect(r.x + 16, yy + 4, 200, 22), "Words", Ui.Label);
            Ui.WordsPicker(r.x + 220, yy, 96);
            Ui.Text(new Rect(r.x + 220, yy + 30, 320, 20), Words.Blurbs[(int)Words.Level], Ui.Small);
            yy += 72;
            // Interface size
            Ui.Text(new Rect(r.x + 16, yy + 4, 200, 22), "Interface size", Ui.Label);
            float bx = r.x + 220;
            foreach (float s in new[] { 1f, 0.9f, 0.8f, 0.7f })
            {
                if (Ui.Press(new Rect(bx, yy, 84, 28), s >= 1f ? "Auto" : $"{s * 100:0}%", Mathf.Abs(Settings.UiScale - s) < 0.01f ? Ui.ButtonSmallSel : Ui.ButtonSmall)) Settings.UiScale = s;
                bx += 88;
            }
            Ui.Text(new Rect(bx + 6, yy + 4, r.xMax - bx - 20, 22), "Auto fits your screen", Ui.Small);
            yy += 44;
            // Panels and definitions (milestone 34)
            Ui.Text(new Rect(r.x + 16, yy + 4, 200, 22), "Panels", Ui.Label);
            bx = r.x + 220;
            string[] looks = { "Solid", "Normal", "See-through" };
            for (int i = 0; i < 3; i++)
            {
                if (Ui.Press(new Rect(bx, yy, 104, 28), looks[i], Settings.PanelLook == i ? Ui.ButtonSmallSel : Ui.ButtonSmall)) Settings.PanelLook = i;
                bx += 108;
            }
            yy += 44;
            Ui.Text(new Rect(r.x + 16, yy + 4, 200, 22), "Definitions on hover", Ui.Label);
            bx = r.x + 220;
            foreach (float d in new[] { 0f, 1f, 2f })
            {
                if (Ui.Press(new Rect(bx, yy, 84, 28), d <= 0 ? "Off" : $"{d:0} s", Mathf.Abs(Settings.DefsDelay - d) < 0.01f ? Ui.ButtonSmallSel : Ui.ButtonSmall)) Settings.DefsDelay = d;
                bx += 88;
            }
            Ui.Text(new Rect(bx + 6, yy + 4, r.xMax - bx - 20, 22), "Rest the mouse on a word", Ui.Small);
            yy += 44;
            // Sound
            float vol = FlightAudio.Volume;
            Ui.Text(new Rect(r.x + 16, yy + 4, 200, 22), $"Sound: {(vol < 0.01f ? "off" : $"{vol * 100:0}%")}", Ui.Label);
            if (Ui.Press(new Rect(r.x + 220, yy, 60, 28), "-", Ui.Button)) FlightAudio.Volume = Mathf.Round((vol - 0.1f) * 10) / 10;
            if (Ui.Press(new Rect(r.x + 284, yy, 60, 28), "+", Ui.Button)) FlightAudio.Volume = Mathf.Round((vol + 0.1f) * 10) / 10;
            yy += 44;
            // Music (under the master sound volume)
            float mus = GameAudio.MusicVolume;
            Ui.Text(new Rect(r.x + 16, yy + 4, 200, 22), $"Music: {(mus < 0.01f ? "off" : $"{mus * 100:0}%")}", Ui.Label);
            if (Ui.Press(new Rect(r.x + 220, yy, 60, 28), "-", Ui.Button)) GameAudio.MusicVolume = Mathf.Round((mus - 0.1f) * 10) / 10;
            if (Ui.Press(new Rect(r.x + 284, yy, 60, 28), "+", Ui.Button)) GameAudio.MusicVolume = Mathf.Round((mus + 0.1f) * 10) / 10;
            yy += 44;
            // Autosave
            Ui.Text(new Rect(r.x + 16, yy + 4, 200, 22), "Autosave in flight", Ui.Label);
            bx = r.x + 220;
            foreach (int m in new[] { 0, 2, 5, 10 })
            {
                if (Ui.Press(new Rect(bx, yy, 74, 28), m == 0 ? "Off" : $"{m} min", Settings.AutosaveMinutes == m ? Ui.ButtonSmallSel : Ui.ButtonSmall)) Settings.AutosaveMinutes = m;
                bx += 78;
            }
            yy += 44;
            // Roll-out to the pad (milestone 25)
            Ui.Text(new Rect(r.x + 16, yy + 4, 200, 22), "Roll-out to the pad", Ui.Label);
            if (Ui.Press(new Rect(r.x + 220, yy, 74, 28), "On", FlightMode.RolloutOn ? Ui.ButtonSmallSel : Ui.ButtonSmall)) FlightMode.RolloutOn = true;
            if (Ui.Press(new Rect(r.x + 298, yy, 74, 28), "Off", !FlightMode.RolloutOn ? Ui.ButtonSmallSel : Ui.ButtonSmall)) FlightMode.RolloutOn = false;
            Ui.Text(new Rect(r.x + 400, yy + 4, 120, 22), "Countdown", Ui.Label);
            if (Ui.Press(new Rect(r.x + 520, yy, 74, 28), "On", FlightMode.CountdownOn ? Ui.ButtonSmallSel : Ui.ButtonSmall)) FlightMode.CountdownOn = true;
            if (Ui.Press(new Rect(r.x + 598, yy, 74, 28), "Off", !FlightMode.CountdownOn ? Ui.ButtonSmallSel : Ui.ButtonSmall)) FlightMode.CountdownOn = false;
            yy += 44;
            // The story (milestone 28)
            Ui.Text(new Rect(r.x + 16, yy + 4, 200, 22), "Story missions", Ui.Label);
            if (Ui.Press(new Rect(r.x + 220, yy, 74, 28), "On", FlightMode.StoryOn ? Ui.ButtonSmallSel : Ui.ButtonSmall)) FlightMode.StoryOn = true;
            if (Ui.Press(new Rect(r.x + 298, yy, 74, 28), "Off", !FlightMode.StoryOn ? Ui.ButtonSmallSel : Ui.ButtonSmall)) FlightMode.StoryOn = false;
            yy += 44;
            // Graphics quality (Low suits older laptops)
            Ui.Text(new Rect(r.x + 16, yy + 4, 200, 22), "Graphics", Ui.Label);
            bx = r.x + 220;
            string[] q = { "Low", "Medium", "High" };
            for (int i = 0; i < 3; i++)
            {
                if (Ui.Press(new Rect(bx, yy, 96, 28), q[i], Settings.Graphics == i ? Ui.ButtonSmallSel : Ui.ButtonSmall)) { Settings.Graphics = i; RenderEnv.ApplyQuality(i); }
                bx += 100;
            }
            yy += 44;
            Ui.Text(new Rect(r.x + 16, yy, r.width - 32, 40), "The game also saves whenever you go back to the builder, and F5 / F9 quicksave and quickload during a flight.", Ui.Small);
            yy += 44;
            if (Ui.Press(new Rect(r.x + 16, r.yMax - 48, 100, 34), "Back", Ui.Button)) ShowPage("main");
            if (Ui.Press(new Rect(r.x + 126, r.yMax - 48, 130, 34), "Controls...", Ui.Button)) ShowPage("controls");
        }
    }

    /// <summary>Player settings, kept between games (Unity's PlayerPrefs).</summary>
    public static class Settings
    {
        static float? uiScale;
        public static float UiScale
        {
            // 1 = Auto (as big as fits); smaller makes more room. Sizes above 100% (before milestone 32) overlapped, so they read as Auto.
            get { if (!uiScale.HasValue) uiScale = Universe.IsAutotest ? 1f : Mathf.Clamp(PlayerPrefs.GetFloat("uiScale", 1f), 0.6f, 1f); return uiScale.Value; }
            set { uiScale = Mathf.Clamp(value, 0.6f, 1f); if (!Universe.IsAutotest) { PlayerPrefs.SetFloat("uiScale", uiScale.Value); PlayerPrefs.Save(); } }
        }

        /// <summary>0 Low, 1 Medium, 2 High.</summary>
        public static int Graphics
        {
            get => PlayerPrefs.GetInt("graphics", 2);
            set { PlayerPrefs.SetInt("graphics", Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); }
        }

        public static int AutosaveMinutes
        {
            get => PlayerPrefs.GetInt("autosaveMin", 5);
            set { PlayerPrefs.SetInt("autosaveMin", Mathf.Clamp(value, 0, 60)); PlayerPrefs.Save(); }
        }

        /// <summary>Seconds the mouse rests on a word before its definition shows (0 = off). Milestone 34.</summary>
        public static float DefsDelay
        {
            get { if (!defsDelay.HasValue) defsDelay = Universe.IsAutotest ? 2f : PlayerPrefs.GetFloat("defsDelay", 2f); return defsDelay.Value; }
            set { defsDelay = Mathf.Clamp(value, 0f, 5f); if (!Universe.IsAutotest) { PlayerPrefs.SetFloat("defsDelay", defsDelay.Value); PlayerPrefs.Save(); } }
        }
        static float? defsDelay;

        /// <summary>How see-through the panels are: 0 solid, 1 normal, 2 clear. Milestone 34.</summary>
        public static int PanelLook
        {
            get { if (!panelLook.HasValue) panelLook = Universe.IsAutotest ? 1 : Mathf.Clamp(PlayerPrefs.GetInt("panelLook", 1), 0, 2); return panelLook.Value; }
            set { panelLook = Mathf.Clamp(value, 0, 2); if (!Universe.IsAutotest) { PlayerPrefs.SetInt("panelLook", panelLook.Value); PlayerPrefs.Save(); } Ui.Rebuild(); }
        }
        static int? panelLook;
    }

    static class StyleExt
    {
        public static GUIStyle ApplyLeft(this GUIStyle s) => new GUIStyle(s) { alignment = TextAnchor.MiddleLeft };
        public static GUIStyle ApplyRight(this GUIStyle s) => new GUIStyle(s) { alignment = TextAnchor.MiddleRight };
    }
}
