using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public enum SpeedMode { Surface, Orbit, Target }

    /// <summary>
    /// Flying: fixed-step simulation, time warp, staging, SOI changes, maneuver nodes and camera.
    /// The HUD lives in FlightHud.cs and the map view in FlightMap.cs.
    /// </summary>
    public partial class FlightMode
    {
        const double Step = 0.02;
        static readonly int[] RailsLevels = { 1, 5, 10, 50, 100, 1000, 10000, 100000 };
        static readonly int[] PhysicsLevels = { 1, 2, 3, 4 };
        static readonly Color SpaceColor = new Color(0.008f, 0.01f, 0.018f);
        static readonly Color SkyColor = new Color(0.36f, 0.58f, 0.9f);
        static readonly Color DuskColor = new Color(0.85f, 0.5f, 0.32f);
        float ambientNow = 0.24f;
        static readonly Color DeepSky = new Color(0.07f, 0.13f, 0.32f);

        readonly GameRoot root;
        CelestialBody Home => root.home;

        // Other vessels within this distance fly with full physics alongside the active one; farther ones coast on rails.
        const double LoadRange = 2500, ViewRange = 30_000;

        public VesselSim active;
        readonly List<VesselSim> loaded = new List<VesselSim>(); // other vessels on full physics this frame
        public int LoadedCount => loaded.Count;
        readonly Dictionary<VesselSim, VesselView> views = new Dictionary<VesselSim, VesselView>();
        readonly List<VesselSim> staleViews = new List<VesselSim>();
        readonly List<Boom> booms = new List<Boom>();
        bool showVessels;

        Universe U => root.universe;
        /// <summary>Universe time: it carries on from flight to flight.</summary>
        public double ut { get => root.universe.ut; set => root.universe.ut = value; }
        public double prevUt;
        double acc;
        float alpha = 1f;
        public int warp = 1;
        public bool railsWarp;
        public bool mapView;
        bool showHelp, paused, destroyHandled, orbitAnnounced, landedAnnounced, thermalView;
        float camYaw = 180f, camPitch = 8f, camDist = 40f;
        public FlightAutopilot autopilot = new FlightAutopilot();
        public bool autopilotOn;
        string message;
        float messageUntil;
        VesselDesign design;
        List<StageReport> reports = new List<StageReport>();
        float reportTimer, trajTimer;
        CelestialBody lastBody;

        public List<TrajectorySegment> trajectory = new List<TrajectorySegment>();
        public ManeuverNode node;
        bool nodeBurnStarted;
        public CelestialBody target;
        public VesselSim targetVessel;
        /// <summary>Scripted RCS translation (autotest); null = keyboard.</summary>
        public Vector3? translateOverride;
        double? warpTargetUt;
        public bool IsWarpingToTarget => warpTargetUt.HasValue;
        public bool WarpingToTarget => warpTargetUt.HasValue;
        SpeedMode speedMode = SpeedMode.Surface;
        bool speedModeManual;

        Vector3d renderRel;  // interpolated position relative to the active body
        Vector3d originAbs;  // floating origin, absolute coordinates
        Quaternion renderRot = Quaternion.identity;
        double renderUt;

        class Boom { public GameObject go; public CelestialBody body; public Vector3d rel; public float t, size; }

        public FlightMode(GameRoot root) { this.root = root; }

        // ---------------------------------------------------------------- lifecycle

        bool launchRunway;

        /// <summary>Launches a new vessel from the pad or runway into the persistent universe.</summary>
        public void Begin(VesselDesign d, bool runway = false, int pad = 0)
        {
            ClearScene();
            recorder = new FlightRecording();
            if (rollout != null) { if (rollout.crawler != null) UnityEngine.Object.Destroy(rollout.crawler); rollout = null; }
            countdown = -1f;
            SiteView?.SetPadArms(-1, 0f);   // every pad's arms back in
            helpUntil = Time.unscaledTime + 25f;
            design = d.Clone();
            launchRunway = runway;
            pad = Mathf.Clamp(pad, 0, LaunchSite.Pads.Length - 1);
            string cleared = ClearSpawnArea(runway, pad);
            active = runway ? VesselSim.CreateOnRunway(design, Home, ut) : VesselSim.CreateOnPad(design, Home, ut, pad);
            active.launchUt = ut;
            U.Add(active);
            int seats = 0;
            foreach (var p in active.parts) if (p.def.kind == PartKind.Command) seats += p.def.crew;
            int flew = U.career.AssignCrew(active);
            if (seats > 0)
                cleared = (cleared != null ? cleared + " " : "") + (flew == 0 ? "No astronauts available: the capsule is empty." :
                    $"Crew: {string.Join(", ", active.CrewAboard().Select(m => $"{m.name} ({Crew.Tag(m)})"))}.");
            StartFlight();
            camYaw = 180f;
            camPitch = 6f;
            camDist = Mathf.Clamp(active.Length * 1.5f + 10f, 15f, 150f);
            string pre = cleared != null ? cleared + " " : "";
            if (runway)
            {
                camYaw = 255f;
                camPitch = 10f;
                camDist = Mathf.Clamp(active.Length * 1.6f + 8f, 14f, 120f);
                Show(design.IsRover ? $"{pre}{design.name} is on the runway. W drives (and lets the brakes off), A/D steer, B brakes." : $"{pre}{design.name} is on the runway. SPACE starts the engine, B releases the brakes, S pulls the nose up.", 7f);
            }
            else
            {
                Show($"{pre}{design.name} is on {LaunchSite.Pads[pad].name.ToLowerInvariant()}. Press SPACE to launch.", 6f);
                if (RolloutOn && (!Universe.IsAutotest || ForceRollout)) StartRollout(pad);
            }
        }

        /// <summary>Takes control of a vessel that's already in the universe.</summary>
        public void BeginExisting(VesselSim v)
        {
            ClearScene();
            design = null;
            active = v;
            StartFlight();
            camYaw = 180f;
            camPitch = 6f;
            camDist = v.IsEva ? 6f : Mathf.Clamp(active.Length * 1.5f + 10f, 15f, 150f);
            Show($"Flying {v.name}: {v.Situation(ut)}", 5f);
        }

        float wanderTimer, autosaveTimer;
        bool supplyWarned;

        void StartFlight()
        {
            wanderTimer = 0;
            if (audio == null) audio = new FlightAudio(root.farCam.gameObject);
            U.SpawnRescues(root.bodies);
            commTimer = 0;
            wasConnected = true;
            prevUt = ut;
            acc = 0;
            SetWarpOne();
            mapView = paused = destroyHandled = false;
            ResetFlags();
            lastBody = active.body;
            active.prevPos = active.pos;
            active.prevRot = active.rot;
            views[active] = VesselView.Create(active);
            autopilot = new FlightAutopilot();
            autopilotOn = false;
            node = null;
            target = null;
            targetVessel = null;
            warpTargetUt = null;
            speedModeManual = false;
            mapFocus = active.body;
            mapDist = (float)(active.body.radius * (active.body.IsPlanet || active.body.isStar ? 3.4 : 6));
            EnsureMapObjects();
            reportTimer = 0;
            trajTimer = 0;
        }

        /// <summary>Announcements already true for this vessel (don't cheer "ORBIT!" when you switch to a satellite).</summary>
        void ResetFlags()
        {
            orbitAnnounced = !active.landed && active.Situation(ut).StartsWith("Orbiting");
            landedAnnounced = active.landed;
        }

        /// <summary>Recovers anything parked where the new vessel will appear. Returns a note for the player, or null.</summary>
        string ClearSpawnArea(bool runway, int pad = 0)
        {
            Vector3d spot = runway ? LaunchSite.DirectionAt(Home, LaunchSite.RunwayStartEast, LaunchSite.RunwayNorth) : LaunchSite.PadDirection(Home, pad);
            var names = new List<string>();
            for (int i = U.vessels.Count - 1; i >= 0; i--)
            {
                var u = U.vessels[i];
                if (u.body != Home || !u.landed) continue;
                double d = Vector3d.Angle(Home.InertialToFixed(u.pos, ut), spot) * Home.radius;
                if (d < 80) { names.Add(U.career.Recover(u, ut, root.bodies)); U.Remove(u); }
            }
            return names.Count == 0 ? null : $"Cleared the {(runway ? "runway" : "pad")}: {string.Join("; ", names)}.";
        }

        /// <summary>Leaves flight: the active vessel and everything else stay in the universe (wrecks are cleared).</summary>
        public void Exit()
        {
            for (int i = U.vessels.Count - 1; i >= 0; i--) if (U.vessels[i].destroyed) U.vessels.RemoveAt(i);
            ClearScene();
            active = null;
            audio?.Stop();
        }

        FlightAudio audio;

        void ClearScene()
        {
            foreach (var lr in cableLines.Values) if (lr != null) UnityEngine.Object.Destroy(lr.gameObject);
            cableLines.Clear();
            if (carryLine != null) { UnityEngine.Object.Destroy(carryLine.gameObject); carryLine = null; }
            cableFrom = 0;
            foreach (var v in views.Values) v.Destroy();
            views.Clear();
            loaded.Clear();
            foreach (var b in booms) UnityEngine.Object.Destroy(b.go);
            booms.Clear();
            if (guideBeam != null) guideBeam.SetActive(false);
            ClearExplore();
            remote = false;
            if (photoMode) TogglePhoto();
            EndReplay();
            showPractice = false;
            countdown = -1f;
            HideMapObjects();
        }

        public bool CanRevert => design != null && root.CanRevert;

        /// <summary>Recovers the active vessel if it's sitting on Arden (back to the builder).</summary>
        public bool CanRecover => active != null && !active.destroyed && active.landed && active.body == Home;

        public void Recover()
        {
            if (!CanRecover) return;
            string msg = U.career.Recover(active, ut, root.bodies);
            U.Remove(active);
            root.EnterHangar();
            root.hangar.Notice(msg);
        }

        public void Notice(string s) => Show(s, 6f);

        void Show(string s, float seconds = 3f, bool important = false)
        {
            // An important message (a crew lost) stays up its full time; everyday notices wait behind it
            if (!important && Time.unscaledTime < importantUntil) return;
            message = s;
            messageUntil = Time.unscaledTime + seconds;
            CoopSay(s);
            if (important) importantUntil = messageUntil;
        }

        float importantUntil;

        // ---------------------------------------------------------------- public controls (also used by AutoTest)

        public void EnableAutopilot()
        {
            if (active == null || active.destroyed) return;
            if (CoopBlock(CAct.Steer, "autopilot")) return;
            if (active.body != Home) { Show("The autopilot only flies to orbit from Arden."); return; }
            if (active.HasWheels) { Show("The autopilot flies rockets, not planes. You're the pilot!"); return; }
            if (BlockedNoSignal("start the autopilot")) return;
            autopilot = new FlightAutopilot();
            if (!active.landed) autopilot.phase = FlightAutopilot.Phase.Ascent;
            autopilotOn = true;
            Show("Autopilot: flying to an 80 km orbit. Any steering key takes over.", 4f);
        }

        bool RailsAllowed() => active.CanRailsWarp() && !(autopilotOn && autopilot.TimeUntilBurn(active, ut) <= Step);

        public void RequestWarp(int wanted)
        {
            bool rails = RailsAllowed();
            int[] levels = rails ? RailsLevels : PhysicsLevels;
            int best = 1;
            foreach (int l in levels) if (l <= wanted) best = l;
            warp = best;
            railsWarp = rails && warp > 1;
        }

        public float CamPitch => camPitch;

        public void SetCamera(float yaw, float pitch, float dist)
        {
            camYaw = yaw;
            camPitch = pitch;
            camDist = dist;
        }

        /// <summary>Puts the camera behind the active vessel looking along a world direction (e.g. toward a target).</summary>
        public void LookAlong(Vector3d dirWorld, float dist, float yawOffset = 0f, float pitchOffset = 0f)
        {
            Vector3 up = active.pos.normalized.ToFloat();
            Vector3 north = Vector3.ProjectOnPlane(Vector3.up, up);
            if (north.sqrMagnitude < 1e-4f) north = Vector3.ProjectOnPlane(Vector3.right, up);
            north.Normalize();
            Vector3 east = Vector3.Cross(up, north);
            Vector3 d = -dirWorld.normalized.ToFloat();
            camPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(Vector3.Dot(d, up), -1f, 1f)) * Mathf.Rad2Deg + pitchOffset, -85f, 85f);
            camYaw = Mathf.Atan2(Vector3.Dot(d, east), Vector3.Dot(d, north)) * Mathf.Rad2Deg + yawOffset;
            camDist = dist;
        }

        public void SetTarget(CelestialBody b)
        {
            if (CoopBlock(CAct.Navigate, "target", 0, b?.name)) return;
            target = target == b ? null : b;
            targetVessel = null;
            Show(target != null ? $"Target: {target.name}" : "Target cleared");
        }

        public void SetTarget(VesselSim v)
        {
            if (v == active) return;
            targetVessel = targetVessel == v ? null : v;
            target = null;
            closestTimer = 0;
            Show(targetVessel != null ? $"Target: {targetVessel.name}" : "Target cleared");
        }

        public bool HasTarget => target != null || (targetVessel != null && !targetVessel.destroyed);
        public string TargetName => targetVessel != null ? targetVessel.name : target?.name;
        public Vector3d TargetAbsPos(double t) => targetVessel != null ? targetVessel.AbsolutePosition(t) : target.AbsolutePosition(t);
        public Vector3d TargetAbsVel(double t) => targetVessel != null ? targetVessel.AbsoluteVelocity(t) : target.AbsoluteVelocity(t);

        /// <summary>Makes another vessel the active one. The old one stays where it is (on rails once out of range).</summary>
        public void SwitchTo(VesselSim v)
        {
            if (v == null || v == active || v.destroyed || !U.vessels.Contains(v)) return;
            var old = active;
            old.driveInput = 0; // a rover left behind stops driving
            if (old.destroyed)
            {
                U.Remove(old);
                RemoveView(old);
                if (targetVessel == v) targetVessel = null;
            }
            else
            {
                old.inputPitch = old.inputYaw = old.inputRoll = 0;
                old.inputTranslate = Vector3.zero;
                old.steerDirection = old.maneuverDir = old.targetDir = old.relVelOverride = null;
                if (!old.landed && old.Altitude < old.RailsFloor) old.awake = true;
                if (targetVessel == v) targetVessel = old; // handy for meeting back up
            }
            active = v;
            active.awake = false;
            commTimer = 0;
            wasConnected = true;
            if (!views.ContainsKey(v)) views[v] = VesselView.Create(v);
            lastBody = v.body;
            autopilotOn = false;
            node = null;
            nodeBurnStarted = false;
            warpTargetUt = null;
            destroyHandled = false;
            SetWarpOne();
            ResetFlags();
            camDist = v.IsEva ? 6f : Mathf.Clamp(v.Length * 1.5f + 10f, 15f, 150f);
            mapFocus = v.body;
            trajTimer = 0;
            reportTimer = 0;
            Show($"Now flying {v.name}", 2.5f);
            U.Save();
        }

        /// <summary>Next/previous vessel within physics range, nearest first ([ and ]).</summary>
        void CycleNearby(int dir)
        {
            var near = new List<VesselSim>(loaded);
            near.RemoveAll(u => u.destroyed);
            if (near.Count == 0) { Show("No other vessels nearby (within 2.5 km). Open the vessel list (V) to switch further."); return; }
            near.Sort((a, b) => Vector3d.Distance(a.pos, active.pos).CompareTo(Vector3d.Distance(b.pos, active.pos)));
            SwitchTo(dir > 0 ? near[0] : near[near.Count - 1]);
        }

        public void AddNode(double atUt, double prograde = 0, double normal = 0, double radial = 0)
        {
            if (CoopBlock(CAct.Navigate, "nodeset", 0, string.Join(";", new[] { atUt, prograde, normal, radial }.Select(x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))))) return;
            node = new ManeuverNode { ut = Math.Max(atUt, ut + 5), prograde = prograde, normal = normal, radial = radial };
            nodeBurnStarted = false;
            node.Update(trajectory);
            NodeComputer.UpdateChain(node, laterNodes);
        }

        /// <summary>Burns queued after the current node (each planned from the path the one before leaves).</summary>
        public readonly List<ManeuverNode> laterNodes = new List<ManeuverNode>();

        /// <summary>Queues a burn after the current node.</summary>
        public void AddLaterNode(double atUt, double prograde, double normal, double radial)
        {
            laterNodes.Add(new ManeuverNode { ut = atUt, prograde = prograde, normal = normal, radial = radial });
            laterNodes.Sort((a, b) => a.ut.CompareTo(b.ut));
            NodeComputer.UpdateChain(node, laterNodes);
        }

        /// <summary>Deletes the current node; the next queued one (if any) takes its place.</summary>
        public void DeleteNode()
        {
            if (CoopBlock(CAct.Navigate, "nodedel")) return;
            node = null;
            nodeBurnStarted = false;
            if (laterNodes.Count > 0)
            {
                node = laterNodes[0];
                laterNodes.RemoveAt(0);
                RefreshTrajectory();
                // A speed match is re-aimed from where we really are (the burn before it is never perfect)
                var mt = node.matchTarget;
                if (mt != null && !mt.destroyed && U.vessels.Contains(mt) && mt.body == active.body)
                {
                    var again = NodeComputer.MatchAt(active, trajectory, Orbit.FromState(mt.pos, mt.vel, mt.body.mu, ut), ut);
                    if (again != null) { again.matchTarget = mt; node = again; }
                }
                node.Update(trajectory);
                NodeComputer.UpdateChain(node, laterNodes);
            }
            if (node == null) autoBurn = false;
            if (node == null && active != null && active.sas == SasMode.Maneuver) active.sas = SasMode.Stability;
        }

        /// <summary>Forgets every planned burn.</summary>
        public void ClearNodes()
        {
            laterNodes.Clear();
            Internal(DeleteNode);
        }

        /// <summary>The flight computer flies the nodes: warps to each, points, burns, and moves on to the next.</summary>
        public bool autoBurn;

        public void ToggleAutoBurn()
        {
            if (autoBurn) { autoBurn = false; active.throttle = 0; Show("Auto-burn off. Throttle cut.", 3f); return; }
            if (node == null || !node.valid) { Show("Plan a maneuver first."); return; }
            if (!active.HasControl) { Show("No control: the flight computer can't fly this vessel."); return; }
            if (BlockedNoSignal("fly its burns")) return;
            if (!active.CanHold(SasMode.Maneuver)) { Show($"The flight computer needs SAS that can hold a maneuver ({SasNeeds(Crew.TierNeeded(SasMode.Maneuver))})."); return; }
            if (double.IsInfinity(ManeuverNode.BurnTime(active, node.DeltaV))) { Show("No engine ready: stage one first (SPACE)."); return; }
            autoBurn = true;
            active.sas = SasMode.Maneuver;
            Show($"Auto-burn: flying {1 + laterNodes.Count} burn{(laterNodes.Count > 0 ? "s" : "")}, even out of contact. X cuts it.", 4f);
        }

        /// <summary>Warps on rails until 'at', then says what's there.</summary>
        public void WarpTo(double at, string label)
        {
            if (at <= ut + 1) { Show("That's now."); return; }
            if (!active.CanRailsWarp()) { Show(active.EnginesWouldBurn() ? "Cut the throttle (X) to time-warp." : "Can't time-warp here (in the air, or near the ground)."); return; }
            warpTargetUt = at;
            warpLabel = label;
        }

        string warpLabel;

        public void WarpToNode()
        {
            warpLabel = null;
            if (node == null || !node.valid) return;
            double burn = ManeuverNode.BurnTime(active, node.DeltaV);
            if (double.IsInfinity(burn)) burn = 0;
            warpTargetUt = node.ut - burn / 2 - 20;
            if (warpTargetUt <= ut) { warpTargetUt = null; Show("Already at the node."); }
        }

        /// <summary>Recomputes the predicted path (and the node's planned path).</summary>
        public void RefreshTrajectory()
        {
            if (active == null || active.landed || active.destroyed) { trajectory.Clear(); return; }
            trajectory = Patcher.Predict(active.body, active.pos, active.vel, ut, 3);
            // Freeze the plan once the burn has begun: re-planning from the half-burned orbit would add the whole
            // burn again and the "remaining" never reaches zero.
            if (node != null && !nodeBurnStarted && ut < node.ut - 0.5) node.Update(trajectory);
            if (node != null) NodeComputer.UpdateChain(node, laterNodes);
        }

        // ---------------------------------------------------------------- frame update

        public void Update()
        {
            if (active == null) return;
            if (replay != null)
            {
                float rdt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                HandleReplayInput();
                if (replay != null)
                {
                    AdvanceReplay(rdt);
                    ReplayViews();
                    UpdateCamera();
                    audio?.Update(active, true, false, false, rdt);
                    UpdateBooms(Time.unscaledDeltaTime);
                    return;
                }
            }
            UpdatePhotoCapture();
            HandleInput();
            if (active == null || !root.inFlight) return; // a failed quickload went back to the builder
            if (remote) CoopClientTick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));      // milestone 30: the host's flight
            if (rollout != null) AdvanceRollout(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            else if (!remote && ((!paused && !photoMode) || Coop.Active))           // (in a rocket room the flight goes on for everyone)
            {
                DriveWarpToTarget(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                Simulate(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                if (active != null && active.hasLaunched) recorder.Sample(active, ut, root.bodies);     // milestone 28: for the replay
                UpdateCountdown(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                UpdateRecovery(Time.unscaledDeltaTime);
            }
            CoopHostTick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            trajTimer -= Time.unscaledDeltaTime;
            if (trajTimer <= 0 || mapView || railsWarp)
            {
                RefreshTrajectory();
                trajTimer = 0.2f;
            }
            UpdateSteeringTargets();
            UpdateComms(Time.unscaledDeltaTime);
            if (!remote) UpdateCareer(Time.unscaledDeltaTime);
            UpdateAeroForecast(Time.unscaledDeltaTime);
            UpdateGuide();
            if (active.deathNote != null)
            {
                // The crew died: stop warping and make sure it's seen
                warpTargetUt = null; warpLabel = null; autoBurn = false;
                SetWarpOne();
                Show(active.deathNote, 8f, true);
                active.deathNote = null;
            }
            // A day's supplies left: say so once (per ship, per shortage)
            if (active.UsesSupplies && active.starvingSince < 0 && active.SupplyDaysLeft() < 1) { if (!supplyWarned) { supplyWarned = true; Show($"Supplies for less than a day! Resupply {active.name}, or bring the crew home.", 6f); } }
            else supplyWarned = false;
            autosaveTimer += Time.unscaledDeltaTime;
            if (Settings.AutosaveMinutes > 0 && autosaveTimer > Settings.AutosaveMinutes * 60f && !active.destroyed) { autosaveTimer = 0; U.Save(); }
            UpdateBases();
            UpdateViews();
            PlaceRolloutCrawler();
            UpdateLandingGuide();
            UpdateLandmarkGuide();
            UpdateGroundFx();
            UpdateWeatherLine(Time.unscaledDeltaTime);
            UpdateCableViews();
            UpdateCamera();
            UpdateMap();
            audio?.Update(active, paused || photoMode, mapView, railsWarp, Time.unscaledDeltaTime);
            UpdateMusic();
            UpdateBooms(Time.unscaledDeltaTime);
            reportTimer -= Time.unscaledDeltaTime;
            if (reportTimer <= 0)
            {
                reports = active.StageReports();
                reportTimer = 0.25f;
            }
        }

        void HandleInput()
        {
            if (rollout != null)
            {
                if (In.Down(K.Space) || In.Down(K.Escape)) EndRollout();
                active.inputPitch = active.inputYaw = active.inputRoll = active.driveInput = 0;
                return;
            }
            if (In.Down(K.F2) && !active.destroyed && !mapView && !paused) TogglePhoto();
            if (photoMode) { HandlePhoto(); return; }
            if (In.Down(K.Escape)) paused = !paused;
            if (In.Down(K.V)) showVessels = !showVessels;
            if (In.Down(K.F5) && !Coop.Active) QuickSave();
            if (In.Down(K.F9) && !Coop.Active) { QuickLoad(); return; }
            var v = active;
            if (paused || v.destroyed)
            {
                v.inputPitch = v.inputYaw = v.inputRoll = v.driveInput = 0;
                return;
            }

            if (v.IsEva) HandleEvaKeys(v);
            else HandleVesselKeys(v);
            if (In.Down(K.T)) ToggleSas();

            if (In.Down(K.Period)) ChangeWarp(+1);
            if (In.Down(K.Comma)) ChangeWarp(-1);
            if (In.Down(K.Slash) && !CoopBlock(CAct.Navigate, "warp", 0)) { SetWarpOne(); warpTargetUt = null; }
            if (In.Down(K.M)) mapView = !mapView;
            if (In.Down(K.O)) ToggleOreMap();
            if (In.Down(K.F1)) { showHelp = !HelpVisible; helpUntil = 0; }
            if (In.Down(K.F)) { thermalView = !thermalView; Show(thermalView ? "Thermal view on" : "Thermal view off"); }
            if (In.Down(K.Delete) && node != null) { DeleteNode(); Show("Maneuver deleted"); }
            if (In.Down(K.Tab) && mapView) CycleMapFocus();
            bool canSwitch = !Coop.Active || (!remote && Coop.Current.CanDo(CAct.Steer));
            if (In.Down(K.RightBracket) && canSwitch) CycleNearby(+1);
            if (In.Down(K.LeftBracket) && canSwitch) CycleNearby(-1);

            bool overUi = Ui.MouseOverUI(In.MousePosition);
            if (In.RightMouse)
            {
                var d = In.MouseDelta;
                if (mapView) { mapYaw += d.x * 0.25f; mapPitch = Mathf.Clamp(mapPitch - d.y * 0.25f, -89f, 89f); }
                else { camYaw += d.x * 0.25f; camPitch = Mathf.Clamp(camPitch - d.y * 0.25f, -85f, 85f); }
            }
            float sc = In.Scroll;
            if (Mathf.Abs(sc) > 0.01f && !overUi)
            {
                float f = sc > 0 ? 1f / 1.15f : 1.15f;
                if (mapView) mapDist = Mathf.Clamp(mapDist * f, (float)mapFocus.radius * 1.3f, MaxMapDist); // out to the whole sun system
                else camDist = Mathf.Clamp(camDist * f, 2.5f, 5000f);
            }
        }

        /// <summary>
        /// Astronaut on EVA. On the ground: W/S walk, A/D turn, SPACE jump, SHIFT jetpack up. Floating: W/S A/D and
        /// SHIFT/CTRL fly the jetpack, Q/E turn. B boards the nearest hatch.
        /// </summary>
        /// <summary>Scripted walking (x = turn, y = forward) for the autotest; null = keyboard.</summary>
        public Vector2? walkOverride;

        void HandleEvaKeys(VesselSim v)
        {
            v.inputPitch = v.inputYaw = v.inputRoll = 0;
            v.rcsOn = true;
            if (autopilotOn) StopAutopilot();
            float W = In.Held(K.W) ? 1 : 0, S = In.Held(K.S) ? 1 : 0, A = In.Held(K.A) ? 1 : 0, D = In.Held(K.D) ? 1 : 0;
            float up = In.Held(K.LeftShift) ? 1 : 0, down = In.Held(K.LeftCtrl) ? 1 : 0;
            if (v.landed)
            {
                v.inputPitch = walkOverride?.y ?? W - S;
                v.inputYaw = walkOverride?.x ?? D - A;
                if (In.Down(K.Space)) v.evaJump = true;
                v.inputTranslate = new Vector3(0, up, 0);
            }
            else
            {
                v.inputTranslate = translateOverride ?? new Vector3(D - A, up - down, W - S);
                v.inputRoll = (In.Held(K.Q) ? 1 : 0) - (In.Held(K.E) ? 1 : 0);
            }
            if (v.inputTranslate != Vector3.zero && railsWarp) SetWarpOne();
            if (In.Down(K.B)) BoardNearest();
            if (In.Down(K.G)) PlantFlag();
        }

        void HandleVesselKeys(VesselSim v)
        {
            if (Coop.Active && !Coop.Current.CanDo(CAct.Steer))
            {
                var c = Coop.Current;
                if (!remote)
                {
                    bool fresh = Time.unscaledTime - c.inTime < 1.5f;
                    v.inputPitch = fresh ? c.inPitch : 0; v.inputYaw = fresh ? c.inYaw : 0; v.inputRoll = fresh ? c.inRoll : 0;
                    v.inputTranslate = fresh ? c.inTranslate : Vector3.zero; v.driveInput = fresh ? c.inDrive : 0;
                    if (fresh && c.inThrottle >= 0) v.throttle = c.inThrottle;
                }
                HandleActionKeys(v);
                return;
            }
            int ws = (In.Held(K.W) ? 1 : 0) - (In.Held(K.S) ? 1 : 0);
            // A rover on the ground: W/S drive (and let the brakes off), A/D steer. In the air they fly it as usual.
            bool driving = Driving(v);
            v.driveInput = driving ? ws : 0;
            v.inputPitch = driving ? 0 : ws;
            if (driving && ws != 0 && v.brakesOn) { v.brakesOn = false; Show("Brakes off"); }
            v.inputYaw = (In.Held(K.D) ? 1 : 0) - (In.Held(K.A) ? 1 : 0);
            if (driveOverride.HasValue && driving) { v.driveInput = driveOverride.Value.y; v.inputYaw = driveOverride.Value.x; }
            v.inputRoll = (In.Held(K.Q) ? 1 : 0) - (In.Held(K.E) ? 1 : 0);
            if (autopilotOn && (v.inputPitch != 0 || v.inputYaw != 0 || v.inputRoll != 0)) StopAutopilot();

            float dt = Time.unscaledDeltaTime;
            double oldThrottle = v.throttle;
            if (In.Held(K.LeftShift)) v.throttle = Math.Min(1, v.throttle + dt * 0.8);
            if (In.Held(K.LeftCtrl)) v.throttle = Math.Max(0, v.throttle - dt * 0.8);
            if (In.Down(K.Z)) v.throttle = 1;
            if (In.Down(K.X)) { v.throttle = 0; if (autoBurn) { autoBurn = false; Show("Auto-burn off.", 2f); } }
            if (v.throttle > oldThrottle && BlockedNoSignal("throttle up")) v.throttle = oldThrottle;
            if (v.throttle != oldThrottle && autopilotOn) StopAutopilot();
            if (v.throttle > oldThrottle && railsWarp) SetWarpOne();

            HandleActionKeys(v);
            v.inputTranslate = translateOverride ?? new Vector3(
                (In.Held(K.L) ? 1 : 0) - (In.Held(K.J) ? 1 : 0),
                (In.Held(K.H) ? 1 : 0) - (In.Held(K.N) ? 1 : 0),
                (In.Held(K.I) ? 1 : 0) - (In.Held(K.K) ? 1 : 0));
            if (v.inputTranslate != Vector3.zero && railsWarp) SetWarpOne();
            if (In.Down(K.P))
            {
                if (autopilotOn) { if (!CoopBlock(CAct.Steer, "autopilot")) StopAutopilot(); }
                else EnableAutopilot();
            }
        }

        /// <summary>Staging, action groups, abort: the flight engineer's keys (every player can try; the job check decides).</summary>
        void HandleActionKeys(VesselSim v)
        {
            if (In.Down(K.Space) && !TryCountdown()) Stage();
            if (In.Down(K.R)) ToggleRcs();
            for (int g = 0; g < 10; g++) if (In.Down(In.Digits[g])) FireGroup(g);
            if (In.Down(K.G)) FireGroup(Groups.Gear);
            if (In.Down(K.U)) FireGroup(Groups.Lights);
            if (In.Down(K.B)) FireGroup(Groups.Brakes);
            if (In.Down(K.Y)) FireGroup(Groups.Deploy);
            if (In.Down(K.Backspace)) DoAbort();
        }

        // ---------------------------------------------------------------- EVA

        /// <summary>Sends the first person in a part out of the hatch and takes control of them.</summary>
        public VesselSim GoEva(PartState p)
        {
            if (active == null || p == null || p.crew.Count == 0) return null;
            if (Coop.Active && remote) { Show("Spacewalks are done on the host's game in a rocket room.", 3f); return null; }
            string who = p.crew[0];
            var eva = active.GoEva(who, ut, out string why);
            if (eva == null) { Show(why, 3f); return null; }
            var parent = active;
            U.Add(eva);
            views[eva] = VesselView.Create(eva);
            var m = U.career.roster.Find(c => c.name == who);
            if (m != null) m.vesselId = eva.id;
            U.career.SetRecord(Records.FirstEva);
            SwitchTo(eva);
            audio?.Clunk(0.3f); // the hatch
            targetVessel = parent;
            camDist = 6f;
            camPitch = 12f;
            Show(eva.landed ? $"{who} is out. W/S walk, A/D turn, SPACE jump, G plants a flag, B climbs back in." :
                              $"{who} is out. W/S A/D SHIFT/CTRL fly the jetpack, Q/E turn, B climbs back in.", 6f);
            return eva;
        }

        /// <summary>The astronaut climbs into the nearest hatch with a free seat (within 3 m).</summary>
        public bool BoardNearest()
        {
            var eva = active;
            if (eva == null || !eva.IsEva) return false;
            VesselSim best = null;
            double bd = double.MaxValue;
            foreach (var u in U.vessels)
            {
                var p = eva.BoardingPart(u);
                if (p == null) continue;
                double d = eva.DistanceToPart(u, p);
                if (d < bd) { bd = d; best = u; }
            }
            if (best == null) { Show("No hatch with a free seat within 3 m.", 2.5f); return false; }
            string who = eva.EvaName;
            if (!eva.Board(best, out string msg)) { Show(msg, 2.5f); return false; }
            var m = U.career.roster.Find(c => c.name == who);
            if (m != null) m.vesselId = best.id;
            SwitchTo(best);
            audio?.Clunk(0.35f);
            U.Remove(eva);
            RemoveView(eva);
            loaded.Remove(eva);
            if (targetVessel == eva) targetVessel = null;
            Show(msg, 3f);
            return true;
        }

        /// <summary>The astronaut plants a flag a step ahead (it stays there as a marker in the vessel list).</summary>
        public VesselSim PlantFlag()
        {
            var eva = active;
            if (eva == null || !eva.IsEva) return null;
            var flag = eva.PlantFlag(ut, out string why);
            if (flag == null) { Show(why, 2.5f); return null; }
            U.Add(flag);
            views[flag] = VesselView.Create(flag);
            if (eva.body != Home) U.career.SetRecord(eva.body.name == "Vesk" ? Records.VeskFlag : Records.TalFlag);
            Show($"{eva.EvaName} planted a flag on {eva.body.name}.", 3f);
            U.Save();
            return flag;
        }

        public string RepairPart(VesselSim v, PartState p) { string m = active.Repair(v, p); Show(m, 3.5f); return m; }
        public string RepackChute(VesselSim v, PartState p) { string m = active.Repack(v, p); Show(m, 3.5f); return m; }

        void StopAutopilot()
        {
            autopilotOn = false;
            active.steerDirection = null;
            Show("Autopilot off. You have control.");
        }

        void AddNodeAtNextApsis()
        {
            if (trajectory.Count == 0) { Show("No orbit to plan on (still on the ground?)"); return; }
            var o = trajectory[0].orbit;
            double t = o.IsElliptic ? ut + o.TimeToApoapsis(ut) : ut + Math.Max(60, o.TimeToPeriapsis(ut));
            if (double.IsNaN(t)) t = ut + 120;
            AddNode(t);
            Show("Maneuver node added at the next apoapsis. Drag its handles in the map (M).", 4f);
        }

        public void SetSas(SasMode m)
        {
            if (CoopBlock(CAct.Steer, "sas", (int)m)) return;
            if (m == SasMode.DockAlign && !active.dockAlignDir.HasValue)
            {
                Show("Target a vessel with a matching free docking port first.");
                return;
            }
            if ((m == SasMode.Maneuver && (node == null || !node.valid)) || ((m == SasMode.Target || m == SasMode.AntiTarget) && !HasTarget))
            {
                Show(m == SasMode.Maneuver ? "No maneuver node (Add maneuver, or click your orbit in the map)" : "No target (click a moon or a vessel in the map, or use the vessel list)");
                return;
            }
            if (!active.CanHold(m))
            {
                Show($"SAS can't hold {m} here: it needs {SasNeeds(Crew.TierNeeded(m))}. This vessel has {active.SasSource}.", 4f);
                return;
            }
            active.sas = m;
            Show("SAS: " + m);
        }

        /// <summary>What it takes to hold a mode of this tier: "a pilot (any level) or a probe core".</summary>
        static string SasNeeds(int tier) => tier switch
        {
            <= 1 => "a pilot or a probe core",
            2 => "a level 1 pilot or a probe core",
            3 => "a level 2 pilot or a Vireo core",
            _ => "a level 3 pilot or a Vireo core",
        };

        void ChangeWarp(int delta)
        {
            if (CoopBlock(CAct.Navigate, "warp", delta)) return;
            bool rails = RailsAllowed();
            int[] levels = rails ? RailsLevels : PhysicsLevels;
            int idx = 0;
            for (int i = 0; i < levels.Length; i++) if (levels[i] <= warp) idx = i;
            int next = Mathf.Clamp(idx + delta, 0, levels.Length - 1);
            if (!rails && delta > 0 && next == idx && idx == levels.Length - 1)
                Show(active.EnginesWouldBurn() ? "Cut the throttle (X) to warp faster than 4x." : "Max 4x warp inside the atmosphere or near the surface.");
            warp = levels[next];
            railsWarp = rails && warp > 1;
            if (delta < 0) warpTargetUt = null;
        }

        void SetWarpOne()
        {
            warp = 1;
            railsWarp = false;
        }

        void DriveWarpToTarget(float realDt)
        {
            if (!warpTargetUt.HasValue) return;
            double remaining = warpTargetUt.Value - ut;
            if (remaining <= 0.05)
            {
                warpTargetUt = null;
                SetWarpOne();
                Show(warpLabel != null ? $"Coming up: {warpLabel}" : "At the maneuver. Point at it (8) and burn when the timer hits zero.", 5f);
                warpLabel = null;
                return;
            }
            if (!active.CanRailsWarp()) { warpTargetUt = null; Show("Can't time-warp here."); return; }
            RequestWarp((int)Math.Min(100000, remaining / Math.Max(realDt, 0.008) * 0.4));
        }

        public void Stage()
        {
            if (CoopBlock(CAct.Stage, "stage")) return;
            if (!active.HasStagesLeft) { Show("No stages left."); return; }
            if (BlockedNoSignal("stage")) return;
            int stageNumber = active.nextStage + 1;
            AddDebris(active.ActivateNextStage());
            audio?.Clunk(0.8f);
            if (railsWarp) SetWarpOne();
            Show($"Stage {stageNumber}", 1.5f);
        }

        void AddDebris(List<VesselSim> spawned)
        {
            foreach (var d in spawned)
            {
                d.launchUt = active.launchUt;
                U.Add(d);
                views[d] = views[active].SplitOff(d);
            }
        }

        public void FireGroup(int g)
        {
            if (active == null || active.destroyed) return;
            if (CoopBlock(CAct.Systems, "group", g)) return;
            var pieces = active.ToggleGroup(g);
            if (pieces.Count > 0) audio?.Clunk(0.8f);
            AddDebris(pieces);
            if (railsWarp) SetWarpOne();
            string state = g == Groups.Gear ? (active.gearDown ? " down" : " up") : g == Groups.Lights ? (active.lightsOn ? " on" : " off") : g == Groups.Brakes ? (active.brakesOn ? " on" : " off") : "";
            Show(g < 10 ? $"Action group {Groups.Name(g)}" : Groups.Name(g) + state, 1.5f);
        }

        public void DoAbort()
        {
            if (active == null || active.destroyed) return;
            if (CoopBlock(CAct.Stage, "abort")) return;
            autopilotOn = false;
            active.steerDirection = null;
            AddDebris(active.Abort());
            SetWarpOne();
            Show("ABORT! Stage to jettison the tower, then deploy the parachute.", 5f);
        }

        void ToggleSas()
        {
            if (CoopBlock(CAct.Steer, "sastoggle")) return;
            active.sas = active.sas == SasMode.Off ? SasMode.Stability : SasMode.Off;
            Show("SAS " + (active.sas == SasMode.Off ? "off" : "on"));
        }

        void ToggleRcs()
        {
            if (CoopBlock(CAct.Systems, "rcs")) return;
            active.rcsOn = !active.rcsOn;
            Show(active.rcsOn ? (active.MonoLeft > 0 ? "RCS on" : "RCS on (no monopropellant!)") : "RCS off");
        }

        // ---------------------------------------------------------------- simulation

        void Simulate(float realDt)
        {
            if (active.destroyed) return;
            if (railsWarp && !RailsAllowed()) SetWarpOne();

            if (railsWarp)
            {
                double dt = realDt * warp;
                if (autopilotOn) dt = Math.Min(dt, Math.Max(Step, autopilot.TimeUntilBurn(active, ut)));
                if (warpTargetUt.HasValue) dt = Math.Min(dt, Math.Max(0.0, warpTargetUt.Value - ut));
                active.prevPos = active.pos;
                active.prevRot = active.rot;
                prevUt = ut;
                var ev = active.RailsAdvance(dt, ut, out double used);
                active.WarpSystems(used, ut); // power, drills, converters and scanners keep working in warp
                // Everything else goes on rails too; anything in the air when warp starts is lost, as in the classics.
                for (int i = U.vessels.Count - 1; i >= 0; i--)
                {
                    var u = U.vessels[i];
                    if (u == active || u.destroyed) continue;
                    u.awake = false;
                    u.BackgroundAdvance(used, ut, true);
                    if (u.destroyed) LoseVessel(u);
                }
                ut += used;
                if (ev == RailsEvent.Atmosphere) { SetWarpOne(); warpTargetUt = null; Show("Entering the atmosphere. Warp stopped.", 3f); }
                else if (ev == RailsEvent.NearSurface) { SetWarpOne(); warpTargetUt = null; Show($"Close to {active.body.name}'s surface. Warp stopped.", 3f); }
                else if (ev == RailsEvent.SoiChange && warp > 1000) warp = 1000;
                if (autopilotOn)
                {
                    if (autopilot.Update(active, ut)) Internal(Stage);
                    if (autopilot.TimeUntilBurn(active, ut) <= Step || !active.CanRailsWarp()) SetWarpOne();
                }
                alpha = 1f;
                acc = 0;
                PostStep();
                return;
            }

            int sub = warp;
            acc += realDt;
            int guard = 0;
            while (acc >= Step && guard++ < 8)
            {
                foreach (var u in U.vessels) { u.prevPos = u.pos; u.prevRot = u.rot; }
                prevUt = ut;
                for (int k = 0; k < sub && !active.destroyed; k++)
                {
                    if (autopilotOn && autopilot.Update(active, ut)) Internal(Stage);
                    if (autopilotOn && autopilot.phase == FlightAutopilot.Phase.Done)
                    {
                        autopilotOn = false;
                        active.steerDirection = null;
                        active.sas = SasMode.Prograde;
                        Show("Autopilot: in orbit. SAS is holding prograde. Controls are yours.", 6f);
                    }
                    StepAll(Step);
                }
                acc -= Step;
            }
            if (guard >= 8) acc = 0;
            alpha = (float)(acc / Step);
        }

        /// <summary>Nearby vessels (and unloaded ones still in the air) fly with full physics; the rest coast on rails.</summary>
        bool InPhysics(VesselSim u) => (u.awake && !u.landed) || (u.body == active.body && Vector3d.Distance(u.pos, active.pos) < LoadRange);

        /// <summary>Part explosions this step (milestone 23): a boom where each part was, and the sound of it.</summary>
        void FlushExplosions(VesselSim v)
        {
            if (v.explosions.Count == 0) return;
            foreach (var lp in v.explosions)
            {
                Vector3d w = v.pos + Vector3d.Rotate(v.rot, Vector3d.FromFloat(lp - v.com));
                SpawnBoom(v.body, w, 9f);
            }
            v.explosions.Clear();
        }

        void StepAll(double dt)
        {
            active.Step(dt, ut);
            FlushExplosions(active);
            loaded.Clear();
            for (int i = U.vessels.Count - 1; i >= 0; i--)
            {
                var u = U.vessels[i];
                if (u == active || u.destroyed) continue;
                if (InPhysics(u))
                {
                    if (u.body == active.body && Vector3d.Distance(u.pos, active.pos) < LoadRange) loaded.Add(u);
                    u.Step(dt, ut);
                    FlushExplosions(u);
                    u.lastEvent = null;
                    if (u.awake && (u.landed || u.Altitude > u.RailsFloor + 100)) u.awake = false;
                }
                else u.BackgroundAdvance(dt, ut, false);
                if (u.destroyed) LoseVessel(u);
            }
            ut += dt;
            CollectBreakage(active);
            for (int i = loaded.Count - 1; i >= 0; i--) CollectBreakage(loaded[i]);
            CheckCollisions();
            CheckDocking();
            PostStep();
        }

        /// <summary>Parts that tore off this step become their own pieces in the universe.</summary>
        void CollectBreakage(VesselSim u)
        {
            if (u.pendingSpawns.Count == 0) return;
            foreach (var d in u.pendingSpawns)
            {
                d.launchUt = u.launchUt;
                U.Add(d);
                if (views.TryGetValue(u, out var vv)) views[d] = vv.SplitOff(d);
            }
            u.pendingSpawns.Clear();
            if (u != active && u.lastEvent != null && !u.isDebris) Show($"{u.name}: {u.lastEvent}", 4f);
        }

        float dockNoteUntil, bumpNoteUntil;

        /// <summary>Ships bump into each other (and can be wrecked by a hard hit) instead of passing through.</summary>
        void CheckCollisions()
        {
            if (active.destroyed || loaded.Count == 0) return;
            var near = loaded.ToArray();   // a wreck leaves 'loaded' as we go
            for (int i = 0; i < near.Length; i++)
            {
                var u = near[i];
                if (u.destroyed || u.IsFlag) continue;
                if (Collisions.Resolve(active, u, out string crash))
                {
                    if (crash != null) { Show(active.destroyed ? active.destroyReason : $"{u.name} was wrecked in the crash", 4f); audio?.Boom(0); }
                    else if (Time.unscaledTime > bumpNoteUntil && active.lastEvent != null && active.lastEvent.StartsWith("Bumped")) { audio?.Clank(0.4f); bumpNoteUntil = Time.unscaledTime + 1.5f; }
                    if (railsWarp) SetWarpOne();
                }
                for (int j = i + 1; j < near.Length; j++)
                    if (!near[j].destroyed && !near[j].IsFlag) Collisions.Resolve(u, near[j], out _);
            }
            foreach (var u in near) if (u.destroyed && loaded.Contains(u)) LoseVessel(u);
        }

        /// <summary>Latches the active vessel onto any loaded vessel whose port it has just met properly.</summary>
        void CheckDocking()
        {
            if (active.destroyed) return;
            active.RearmAllExcept(loaded);
            if (loaded.Count == 0) return;
            for (int i = loaded.Count - 1; i >= 0; i--)
            {
                var u = loaded[i];
                if (u.destroyed) continue;
                bool grabbed = false;
                string note = null;
                if (u.IsRock) grabbed = active.TryGrab(u, out note);
                if (grabbed || (!u.IsRock && active.TryDock(u, out note)))
                {
                    string rockName = u.IsRock ? u.name : null;
                    U.Remove(u);
                    RemoveView(u);
                    loaded.RemoveAt(i);
                    if (targetVessel == u) targetVessel = null;
                    RemoveView(active);
                    views[active] = VesselView.Create(active);
                    if (active.sas == SasMode.DockAlign) active.sas = SasMode.Stability;
                    active.inputTranslate = Vector3.zero;
                    if (rockName != null) { U.career.OnGrabbed(active, rockName); Show($"Grabbed {rockName}: {active.mass:0.0} t now. Push gently, it's heavy.", 4f); }
                    else
                    {
                        active.careerFlags |= VFlags.Docked;
                        U.career.OnDocked(active);
                    }
                    audio?.Clank();
                    SetWarpOne();
                    trajTimer = 0;
                    reportTimer = 0;
                    U.Save();
                    return;
                }
                if (note != null && Time.unscaledTime > dockNoteUntil) { Show(note, 2.5f); dockNoteUntil = Time.unscaledTime + 3f; }
            }
        }

        // ---------------------------------------------------------------- comms

        public CommLink commLink;
        float commTimer, noSignalNoteUntil;
        bool wasConnected = true;

        /// <summary>Works out the active vessel's link home twice a second (relays and ground station, line of sight).</summary>
        void UpdateComms(float dt)
        {
            commTimer -= dt;
            if (commTimer > 0) return;
            commTimer = 0.5f;
            commLink = CommNet.Connect(active, U.vessels, root.bodies, ut);
            active.connected = commLink.Connected;
            active.signal = commLink.signal;
            bool probe = active.IsControllable && active.CrewCount == 0;
            if (probe && wasConnected && !active.connected) Show("Signal lost: the probe can't throttle up or stage until it's back in contact.", 4f);
            if (probe && !wasConnected && active.connected) Show("Signal restored.", 2f);
            wasConnected = active.connected;
        }

        /// <summary>True (and explains why) when a probe without a signal tries to take a new command.</summary>
        bool BlockedNoSignal(string what)
        {
            if (!active.PartialControl) return false;
            if (Time.unscaledTime > noSignalNoteUntil) { Show($"No signal: the probe can't {what}. Get in range of the ground station or a relay.", 3f); noSignalNoteUntil = Time.unscaledTime + 3f; }
            return true;
        }

        float careerTimer;
        readonly Queue<string> newsQueue = new Queue<string>();
        float newsUntil;

        /// <summary>What the active vessel has achieved (records, contracts), satellite and deadline checks, and the news ticker.</summary>
        void UpdateCareer(float dt)
        {
            var c = U.career;
            var v = active;
            if (!v.destroyed)
            {
                int f = v.careerFlags;
                if (v.hasLaunched) f |= VFlags.Launched;
                string sit = v.Situation(ut);
                bool home = v.body == Home;
                if (home && Home.HasAtmosphere && v.Altitude > Home.atmosphereHeight) f |= VFlags.Space;
                if (home && sit.StartsWith("Orbiting")) { f |= VFlags.OrbitHome; if (v.CrewCount > 0) f |= VFlags.CrewedOrbit; }
                if (v.body.isStar) f |= VFlags.SoiSun; // out of Arden's pull, going round the sun
                else if (!home)
                {
                    // Per world: in its sphere of influence, orbiting it, landed on it, driven 500 m on it
                    int soi, orbit, land, drove;
                    switch (v.body.name)
                    {
                        case "Vesk": soi = VFlags.SoiVesk; orbit = VFlags.OrbitVesk; land = VFlags.LandedVesk; drove = VFlags.DroveVesk; break;
                        case "Ochre": soi = VFlags.SoiOchre; orbit = VFlags.OrbitOchre; land = VFlags.LandedOchre; drove = 0; break;
                        case "Mote": soi = VFlags.SoiMote | VFlags.SoiOchre; orbit = 0; land = VFlags.LandedMote; drove = 0; break;
                        case "Brume": soi = VFlags.SoiBrume; orbit = VFlags.OrbitBrume; land = 0; drove = 0; break;
                        case "Rime": soi = VFlags.SoiRime | VFlags.SoiBrume; orbit = 0; land = VFlags.LandedRime; drove = 0; break;
                        case "Pyre": soi = VFlags.SoiPyre | VFlags.SoiBrume; orbit = 0; land = VFlags.LandedPyre; drove = 0; break;
                        default: soi = VFlags.SoiTal; orbit = VFlags.OrbitTal; land = VFlags.LandedTal; drove = VFlags.DroveTal; break;
                    }
                    f |= soi;
                    if (sit.StartsWith("Orbiting")) f |= orbit;
                    if (v.landed && v.hasLaunched) f |= land;
                    if (v.drivenOn == v.body.name && v.driven >= 500) f |= drove;
                }
                v.careerFlags = f;
                c.OnFlags(f);
                c.CheckVessel(v, ut);
            }
            careerTimer -= dt;
            if (careerTimer <= 0)
            {
                careerTimer = 1f;
                Achievements.Check(U, active, ut);
                UpdateStory();
                c.CheckSatellites(U.vessels, ut);
                c.CheckWanderers(U.vessels, ut);
                wanderTimer -= 1f;
                if (wanderTimer <= 0)
                {
                    wanderTimer = 10f;
                    foreach (var n in U.TendWanderers(root.bodies)) c.news.Add(n);
                    // A wanderer that left (even one we were following) takes its model and target marker with it.
                    for (int i = loaded.Count - 1; i >= 0; i--) if (!U.vessels.Contains(loaded[i])) { RemoveView(loaded[i]); loaded.RemoveAt(i); }
                    if (targetVessel != null && !U.vessels.Contains(targetVessel)) targetVessel = null;
                }
                c.CheckBases(U.vessels, ut, U);
                c.CheckStations(U.vessels, ut);
                Landmarks.Update(U, active, ut);
                c.CheckSurvey(U.survey, root.bodies);
                c.CheckDeadlines(ut);
            }
            foreach (var n in c.news) newsQueue.Enqueue(n);
            c.news.Clear();
            if (newsQueue.Count > 0 && Time.unscaledTime > newsUntil)
            {
                Show(newsQueue.Dequeue(), 4f);
                newsUntil = Time.unscaledTime + 4f;
            }
        }

        // ---------------------------------------------------------------- science (HUD actions)

        public string RunExperiment(PartState p, ExperimentDef e) { if (CoopBlock(CAct.Science, "run", p.index, e.id)) return ""; string m = Lab.Run(U.career, active, p, e, ut); Show(m, 4f); return m; }
        public string TransmitData(PartState p, ScienceData d) { if (CoopBlock(CAct.Science, "send", p.index, p.science.IndexOf(d).ToString())) return ""; string m = Lab.Transmit(U.career, active, p, d, root.bodies); Show(m, 4f); return m; }

        /// <summary>Releases one of the active vessel's docked ports; the other side becomes a vessel again.</summary>
        public VesselSim Undock(PartState port)
        {
            if (active == null || active.destroyed) return null;
            var piece = active.Undock(port);
            if (piece == null) return null;
            piece.launchUt = active.launchUt;
            U.Add(piece);
            audio?.Clank(0.6f);
            RemoveView(active);
            views[active] = VesselView.Create(active);
            views[piece] = VesselView.Create(piece);
            targetVessel = piece;
            target = null;
            reportTimer = 0;
            trajTimer = 0;
            Show(piece.IsRock ? $"Released {piece.name}." : $"Undocked from {piece.name}. Back off gently with RCS (H/N).", 4f);
            U.Save();
            return piece;
        }

        public string TransferCrew(PartState from, PartState to)
        {
            string msg = active.TransferCrew(from, to);
            Show(msg, 3f);
            return msg;
        }

        // Docking readout for the HUD (and the autotest): my port, the target port, and how they line up.
        public PartState dockMine, dockTheirs;
        public double dockDist, dockAxial, dockLateral, dockAngle, dockClosing;

        void UpdateDockingInfo()
        {
            dockMine = dockTheirs = null;
            active.dockAlignDir = null;
            if (targetVessel == null || targetVessel.destroyed || targetVessel.body != active.body) return;
            if (Vector3d.Distance(targetVessel.pos, active.pos) > 5000) return;
            if (!active.BestPortPair(targetVessel, out dockMine, out dockTheirs)) return;
            active.PortFaceWorld(dockMine, out var pm, out var nm);
            targetVessel.PortFaceWorld(dockTheirs, out var pt, out var nt);
            Vector3d d = pm - pt;
            dockDist = d.magnitude;
            dockAxial = Vector3d.Dot(d, nt);
            dockLateral = (d - nt * dockAxial).magnitude;
            dockAngle = Vector3d.Angle(nm, -nt) * 180 / Math.PI;
            Vector3d relV = active.vel - targetVessel.vel;
            dockClosing = dockDist > 1e-6 ? -Vector3d.Dot(relV, d / dockDist) : 0;
            // Point the nose so my port faces theirs (works for a port on the nose or on the tail).
            active.PortFace(dockMine, out _, out var nl);
            active.dockAlignDir = nt * (nl.y >= 0 ? -1 : 1);
        }

        /// <summary>A vessel other than the active one was destroyed: boom if it's in view, a message if it mattered.</summary>
        void LoseVessel(VesselSim u)
        {
            if (u.body == active.body && Vector3d.Distance(u.pos, active.pos) < ViewRange) SpawnBoom(u.body, u.pos, Mathf.Clamp(u.Length * 2f, 6f, 30f));
            if (!u.isDebris) Show(u.destroyReason, 5f);
            U.career.OnVesselLost(u);
            if (targetVessel == u) targetVessel = null;
            RemoveView(u);
            U.Remove(u);
            loaded.Remove(u);
        }

        void PostStep()
        {
            var v = active;
            if (v.lastEvent != null) { Show(v.lastEvent, 3f); v.lastEvent = null; }
            if (v.body != lastBody)
            {
                lastBody = v.body;
                mapFocus = v.body;
                trajTimer = 0;
            }
            if (v.destroyed && !destroyHandled)
            {
                destroyHandled = true;
                Achievements.OnDestroyed(U, v, v.SurfaceRelVelocity.magnitude);
                U.career.OnVesselLost(v);
                SpawnBoom(v.body, v.pos, Mathf.Clamp(v.Length * 2.5f, 8f, 40f));
                views[v].SetVisible(false);
                autopilotOn = false;
                SetWarpOne();
                Show(v.destroyReason, 8f);
                return;
            }
            if (!v.landed && !orbitAnnounced && v.body == Home && v.Altitude > Home.atmosphereHeight)
            {
                var o = Orbit.FromState(v.pos, v.vel, Home.mu, ut);
                if (o.IsElliptic && o.Periapsis - Home.radius > Home.atmosphereHeight)
                {
                    orbitAnnounced = true;
                    Show($"ORBIT! Apoapsis {Ui.Dist(o.Apoapsis - Home.radius)}, periapsis {Ui.Dist(o.Periapsis - Home.radius)}", 6f);
                }
            }
            if (v.Altitude > 200) landedAnnounced = false;
            if (v.hasLaunched && v.landed && !landedAnnounced)
            {
                landedAnnounced = true;
                if (!v.IsEva) audio?.Clunk(0.45f);
                if (v.body == Home)
                {
                    double dist = Vector3d.Angle(Home.InertialToFixed(v.pos, ut), LaunchSite.Direction) * Home.radius;
                    if (LaunchSite.OnBarge(Home, Home.InertialToFixed(v.pos, ut))) Show("Landed on the barge! Right on the deck in the harbour.", 8f);
                    else if (Terrain.IsWater(Home, Home.InertialToFixed(v.pos, ut))) Show(orbitAnnounced ? $"Splashdown after reaching orbit! Bobbing in the sea {Ui.Dist(dist)} from the pad." : $"Splashdown! Bobbing in the sea {Ui.Dist(dist)} from the pad.", 8f);
                    else Show(orbitAnnounced ? $"Landed safely after reaching orbit! {Ui.Dist(dist)} from the pad." : $"Landed safely, {Ui.Dist(dist)} from the pad.", 8f);
                }
                else Show($"Touchdown on {v.body.name}! You made it.", 8f);
            }
            if (warpTargetUt.HasValue && ut >= warpTargetUt.Value) { warpTargetUt = null; SetWarpOne(); }
        }

        /// <summary>Feeds SAS the maneuver and target directions, and finishes a burn when it's done.</summary>
        void UpdateSteeringTargets()
        {
            var v = active;
            if (targetVessel != null && (targetVessel.destroyed || !U.vessels.Contains(targetVessel))) targetVessel = null;
            v.targetDir = HasTarget ? (TargetAbsPos(ut) - v.AbsolutePosition(ut)).normalized : (Vector3d?)null;
            // In Target speed mode, prograde/retrograde follow the velocity relative to the target (for rendezvous).
            v.relVelOverride = speedMode == SpeedMode.Target && HasTarget ? v.AbsoluteVelocity(ut) - TargetAbsVel(ut) : (Vector3d?)null;
            UpdateDockingInfo();
            if (v.sas == SasMode.DockAlign && !v.dockAlignDir.HasValue) v.sas = SasMode.Stability;
            // The pilot moved out, or the probe core ran flat: SAS drops back to what's left.
            if (v.sas != SasMode.Off && v.HasControl && !v.CanHold(v.sas))
            {
                if (!autopilotOn) Show($"SAS back to hold: {v.sas} needs {SasNeeds(Crew.TierNeeded(v.sas))}.", 3f);
                v.sas = SasMode.Stability;
            }
            if (node == null || !node.valid)
            {
                v.maneuverDir = null;
                return;
            }
            double burn = ManeuverNode.BurnTime(v, node.DeltaV);
            bool window = !double.IsInfinity(burn) && ut > node.ut - burn / 2 - 3;
            bool autoDone = false;
            Vector3d? autoDir = null;
            if (autoBurn)
            {
                var mt = node.matchTarget;
                var mo = mt != null && !mt.destroyed && mt.body == v.body ? Orbit.FromState(mt.pos, mt.vel, mt.body.mu, ut) : null;
                var st = NodeComputer.Fly(v, node, ut, nodeBurnStarted, mo);
                if (v.CanHold(SasMode.Maneuver)) v.sas = SasMode.Maneuver;
                if (!st.window && !warpTargetUt.HasValue && node.ut - ut > burn / 2 + 60 && v.CanRailsWarp()) WarpToNode();
                // Burns handed over while there was a signal still run without one: the computer flies them from memory.
                v.throttle = st.throttle;
                if (v.throttle > 0) nodeBurnStarted = true;
                autoDone = st.done;
                autoDir = st.dir;
            }
            if (window && v.thrustNow > 0) nodeBurnStarted = true;
            Vector3d rem = node.Remaining(v, ut, window);
            // The computer steers where it means to burn (for a speed match, against the target's own velocity)
            v.maneuverDir = autoDir ?? (rem.magnitude > 1e-6 ? rem.normalized : (Vector3d?)null);
            if (nodeBurnStarted && (autoBurn ? autoDone : (rem.magnitude < 0.4 || Vector3d.Dot(rem, node.dvWorld) < 0)))
            {
                v.throttle = 0;
                bool more = laterNodes.Count > 0, flying = autoBurn;
                Internal(DeleteNode);
                Show(more && flying ? "Burn done. On to the next one." : flying ? "All burns done. Throttle cut." : "Maneuver complete. Throttle cut.", 4f);
            }
        }

        void RemoveView(VesselSim u)
        {
            if (views.TryGetValue(u, out var vv)) { vv.Destroy(); views.Remove(u); }
        }

        void SpawnBoom(CelestialBody b, Vector3d rel, float size)
        {
            if (active != null) audio?.Boom(b == active.body ? Vector3d.Distance(rel, active.pos) : double.MaxValue);
            var go = new GameObject("Explosion");
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Explosion();
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Additive;
            booms.Add(new Boom { go = go, body = b, rel = rel, size = size });
            // Fire, flung debris and (in air) smoke (milestone 23)
            if (active != null && b == active.body)
            {
                double alt = rel.magnitude - b.radius;
                Smoke.Burst((b.AbsolutePosition(renderUt) + rel - originAbs).ToFloat(), size * 0.8f, b.HasAtmosphere && b.PressureAtm(alt) > 0.02);
            }
        }

        void UpdateBooms(float dt)
        {
            for (int i = booms.Count - 1; i >= 0; i--)
            {
                var b = booms[i];
                b.t += dt;
                float k = b.t / 0.4f;                  // a quick flash; the fireball and smoke are particles (Smoke.Burst)
                if (k >= 1f) { UnityEngine.Object.Destroy(b.go); booms.RemoveAt(i); continue; }
                b.go.transform.position = (b.body.AbsolutePosition(renderUt) + b.rel - originAbs).ToFloat();
                b.go.transform.localScale = Vector3.one * b.size * (0.25f + 0.55f * Mathf.Sqrt(k)) * (1f - k);
            }
        }

        // ---------------------------------------------------------------- rendering

        void UpdateViews()
        {
            renderUt = prevUt + (ut - prevUt) * alpha;
            renderRel = rollout != null ? rollout.rel : Vector3d.Lerp(active.prevPos, active.pos, alpha);   // (rolling out: on the crawler)
            renderRot = rollout != null ? active.rot : Quaternion.Slerp(active.prevRot, active.rot, alpha);
            originAbs = active.body.AbsolutePosition(renderUt) + renderRel;
            double pressure = active.body.PressureAtm(active.Altitude);
            if (views.TryGetValue(active, out var av)) av.Sync(active, Vector3.zero, renderRot, pressure, thermalView);
            // Other vessels get a model while they're within view range of the active one.
            foreach (var d in U.vessels)
            {
                if (d == active) continue;
                bool show = !d.destroyed && d.body == active.body && Vector3d.Distance(d.pos, active.pos) < ViewRange;
                if (!show) { RemoveView(d); continue; }
                if (!views.TryGetValue(d, out var dv)) views[d] = dv = VesselView.Create(d);
                Vector3d abs = d.body.AbsolutePosition(renderUt) + Vector3d.Lerp(d.prevPos, d.pos, alpha);
                dv.Sync(d, (abs - originAbs).ToFloat(), Quaternion.Slerp(d.prevRot, d.rot, alpha), d.body.PressureAtm(d.Altitude), thermalView);
            }
            staleViews.Clear();
            foreach (var k in views.Keys) if (k != active && !U.vessels.Contains(k)) staleViews.Add(k);
            foreach (var k in staleViews) RemoveView(k);
            foreach (var bv in root.bodyViews) bv.Sync(originAbs, renderUt, originAbs);
            Smoke.SetAnchor((active.body.AbsolutePosition(renderUt) - originAbs).ToFloat(), active.body.RotationAt(renderUt));
            {
                var gb = active.body;
                Vector3d upD = renderRel.normalized;
                double groundR = gb.radius + LaunchSite.SurfaceHeight(gb, gb.InertialToFixed(upD, renderUt).normalized);
                Smoke.SetGround((upD * groundR - renderRel).ToFloat(), upD.ToFloat());
            }
            GameRoot.SunNow = PhotoSun(Sun.DirectionFrom(originAbs, renderUt).ToFloat()); // the sun moves through the year (photo mode can turn the light)
            root.sun.transform.rotation = Quaternion.LookRotation(-GameRoot.SunNow);
            root.navball.Orient(active);
        }

        void UpdateCamera()
        {
            var cam = root.farCam;
            if (mapView)
            {
                root.nearCam.enabled = false;
                cam.cullingMask = (1 << GameRoot.LayerFar) | (1 << GameRoot.LayerMap);
                Vector3 targetPos = (mapFocus.AbsolutePosition(renderUt) - originAbs).ToFloat();
                Quaternion q = Quaternion.Euler(mapPitch, mapYaw, 0f);
                cam.transform.position = targetPos + q * new Vector3(0, 0, -mapDist);
                cam.transform.rotation = Quaternion.LookRotation(targetPos - cam.transform.position, Vector3.up);
                cam.nearClipPlane = Mathf.Max(50f, mapDist * 0.01f);
                cam.farClipPlane = mapDist * 50f + 1e8f;
                cam.backgroundColor = Color.black;
                SkyView.SunTint = Color.white;
                SkyView.StarExposure = 1f;
                RenderEnv.SetExposure(0f);
                root.sky?.Sync(cam, 1f);
                WeatherFx.Update(active.body, renderRel, ut, Vector3.zero, Vector3.up, Vector3.zero, null, originAbs, renderUt, Time.unscaledDeltaTime, false);
                return;
            }

            root.nearCam.enabled = true;
            cam.cullingMask = 1 << GameRoot.LayerFar;
            Vector3 up = renderRel.normalized.ToFloat();
            Vector3 north = Vector3.ProjectOnPlane(Vector3.up, up);
            if (north.sqrMagnitude < 1e-4f) north = Vector3.ProjectOnPlane(Vector3.right, up);
            north.Normalize();
            Vector3 east = Vector3.Cross(up, north);
            float yaw = camYaw * Mathf.Deg2Rad, pitch = camPitch * Mathf.Deg2Rad;
            Vector3 dir = (north * Mathf.Cos(yaw) + east * Mathf.Sin(yaw)) * Mathf.Cos(pitch) + up * Mathf.Sin(pitch);
            // Near the ground, keep the camera above it: below, you'd only see the underside of the world.
            var cb = replay != null ? replay.body : active.body;
            Vector3 pivot = photoMode ? photoOffset : Vector3.zero;      // photo mode: the camera's free to fly round
            Vector3d pivotRel = renderRel + Vector3d.FromFloat(pivot);
            double viewAlt = renderRel.magnitude - cb.radius;
            if (viewAlt < Terrain.MaxHeight(cb) + camDist + 30 + pivot.magnitude)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3d camRel = pivotRel + Vector3d.FromFloat(dir * camDist);
                    double ground = cb.radius + LaunchSite.SurfaceHeight(cb, cb.InertialToFixed(camRel, renderUt).normalized);
                    double sinNeed = (ground + 1.2 - pivotRel.magnitude) / camDist;
                    if (Mathf.Sin(pitch) >= sinNeed - 1e-4) break;
                    pitch = sinNeed >= 0.99 ? 1.45f : (float)Math.Asin(sinNeed);
                    camPitch = pitch * Mathf.Rad2Deg;
                    dir = (north * Mathf.Cos(yaw) + east * Mathf.Sin(yaw)) * Mathf.Cos(pitch) + up * Mathf.Sin(pitch);
                }
            }
            cam.transform.position = pivot + dir * camDist;
            cam.transform.rotation = Quaternion.LookRotation(-dir, up);
            float fov = photoMode ? photoFov : 60f;
            if (cam.fieldOfView != fov) { cam.fieldOfView = fov; root.nearCam.fieldOfView = fov; }
            cam.nearClipPlane = 5f;
            cam.farClipPlane = 1e9f;

            var b = cb;
            float air = b.HasAtmosphere ? Mathf.Clamp01((float)Math.Pow(b.Density(Math.Max(0, viewAlt)) / b.seaLevelDensity, 0.3)) : 0f;
            float sunUp = Vector3.Dot(up, GameRoot.SunNow);
            float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.2f, 0.25f, sunUp));
            // Warmer near sunrise and sunset, deep blue high up where the air thins.
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(sunUp - 0.05f) / 0.3f);
            // Milestone 20: the sky is drawn by each world's air shell; light, reflections and exposure follow the
            // sun through the air (see RenderEnv).
            cam.backgroundColor = Color.black;
            Vector3d eyeRel = renderRel + Vector3d.FromFloat(cam.transform.position);
            RenderEnv.UpdateWorld(b, eyeRel, (-renderRel).ToFloat(), GameRoot.SunNow, root.sun, true);
            RenderEnv.UpdateShine(b, b.AbsolutePosition(renderUt) + eyeRel, renderUt, root.bodies);
            Vector3 tSun = RenderEnv.SunTransmittance(b, eyeRel, GameRoot.SunNow);
            float tAvg = (tSun.x + tSun.y + tSun.z) / 3f;
            SkyView.SunTint = tAvg > 1e-4f ? new Color(Mathf.Min(tSun.x / tAvg, 3f), Mathf.Min(tSun.y / tAvg, 3f), Mathf.Min(tSun.z / tAvg, 3f)) : Color.white;
            SkyView.StarExposure = 1f + RenderEnv.Exposure * 0.5f;
            root.sky?.Sync(cam, 1f - Mathf.Clamp01(air * day * 6f));
            Mats.Lit.SetFloat("_Ambient", 0.24f);
            // Milestone 27: the weather where the camera is (haze, dimmer light, rain or dust, dust devils)
            WeatherFx.Update(b, eyeRel, renderUt, cam.transform.position, up, replay != null ? Vector3.zero : active.SurfaceRelVelocity.ToFloat(), root.sun, originAbs, renderUt, Time.unscaledDeltaTime, true);
            if (replay != null) ReplayCamera(cam, up);
        }
    }
}
