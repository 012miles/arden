using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rocket
{
    /// <summary>
    /// Entry point. Builds cameras, light and planet, then switches between the builder and flight.
    /// Two stacked cameras: "far" (planet, map) with a huge range, "near" (vessel, ground, buildings).
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        public const int LayerFar = 8, LayerMap = 9;
        // Mid-morning from the south-east as seen from the pad, so the default camera sees the lit side.
        public static readonly Vector3 SunDirection = new Vector3(0.75f, -0.48f, -0.45f).normalized; // the builder's light
        /// <summary>Toward the sun right now, for the light, sky and panels (set each frame in flight).</summary>
        public static Vector3 SunNow = SunDirection;
        public static GameRoot I;

        public Camera farCam, nearCam;
        public Light sun;
        public CelestialBody home, moon;
        public System.Collections.Generic.List<CelestialBody> bodies;
        public System.Collections.Generic.List<BodyView> bodyViews = new System.Collections.Generic.List<BodyView>();
        public Navball navball;
        public SkyView sky;
        public HangarMode hangar;
        public FlightMode flight;
        public MenuMode menu;
        public MissionMode mission;     // milestone 29: the mission design game
        public bool inMenu, inMission;
        public VesselDesign currentDesign;
        public bool inFlight;
        public Universe universe;
        string revertSnapshot; // universe as it was just before the current launch

        void Awake()
        {
            I = this;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 144;
            Physics.simulationMode = SimulationMode.Script;
            Mats.Init();
            bodies = CelestialBody.CreateSystem();
            home = bodies[0];
            moon = bodies[1];
            BuildCameras();
            BuildSun();
            foreach (var b in bodies) bodyViews.Add(new BodyView(b, b == home));
            sky = new SkyView();
            PlanetBaker.Start(bodies, home, moon);   // sharp maps of every world, baked in the background
            var argv = Environment.GetCommandLineArgs();
            bool menuTest = argv.Contains("-autotestMenu");
            SaveSlots.Root = Application.persistentDataPath;
            // The game and its company name were renamed: bring an older data folder's saves and designs across, once
            if (!Universe.IsAutotest)
            {
                try
                {
                    int copied = SaveSlots.CopyFromOlderVersions(Application.persistentDataPath, out string from);
                    if (copied > 0) Debug.Log($"Copied {copied} files from the older data folder {from}");
                }
                catch (Exception e) { Debug.LogWarning("Couldn't copy the old saves: " + e.Message); }
            }
            // The single save from before save slots becomes "Game 1"
            if (!Universe.IsAutotest)
                SaveSlots.Migrate(Path.Combine(Application.persistentDataPath, "universe.json"), Path.Combine(Application.persistentDataPath, "quicksave.json"));
            universe = Universe.IsAutotest && !menuTest ? Universe.Load(bodies) : new Universe();
            // Autotests always start fresh: sandbox, unless the career demo was asked for.
            if (Universe.IsAutotest && !menuTest)
            {
                var args = Environment.GetCommandLineArgs();
                universe.career.Start(args.Any(a => a == "-autotestCareer" || a == "-autotestCrew" || a == "-autotestSizes") ? GameMode.Career : GameMode.Sandbox);
                if (!args.Contains("-autotestSizes")) universe.career.guide = Guide.Closed; // only the window-size tour shows the guide
                universe.wanderersOn = false;   // the wanderer tour places its own
                universe.career.suppliesOn = args.Contains("-autotestSupplies");   // older tours fly for days without supplies in mind
            }
            new GameAudio(farCam.gameObject);   // music, ambience and interface sounds
            navball = new Navball();
            hangar = new HangarMode(this);
            flight = new FlightMode(this);
            menu = new MenuMode(this);
            mission = new MissionMode(this);
            currentDesign = DesignStore.LoadLast() ?? VesselDesign.Default();
            if (Universe.IsAutotest && !menuTest) EnterHangar();
            else ShowMenu();
            if (Environment.GetCommandLineArgs().Contains("-autotest")) gameObject.AddComponent<AutoTest>();
        }

        void BuildCameras()
        {
            var farGo = new GameObject("FarCamera") { tag = "MainCamera" };
            farCam = farGo.AddComponent<Camera>();
            farCam.clearFlags = CameraClearFlags.SolidColor;
            farCam.backgroundColor = Color.black;
            farCam.nearClipPlane = 5f;
            farCam.farClipPlane = 1e9f;
            farCam.fieldOfView = 60f;
            farCam.cullingMask = 1 << LayerFar;
            farGo.AddComponent<AudioListener>();
            var farData = farCam.GetUniversalAdditionalCameraData();
            farData.renderType = CameraRenderType.Base;
            farData.renderPostProcessing = true;      // tone mapping, bloom and exposure over the whole camera stack
            farData.renderShadows = false;            // planets are lit by the shaders; only the near scene casts shadows

            var nearGo = new GameObject("NearCamera");
            nearGo.transform.SetParent(farGo.transform, false);
            nearCam = nearGo.AddComponent<Camera>();
            nearCam.nearClipPlane = 0.2f;
            nearCam.farClipPlane = 150_000f;
            nearCam.fieldOfView = 60f;
            nearCam.cullingMask = 1 << 0;
            var nearData = nearCam.GetUniversalAdditionalCameraData();
            nearData.renderType = CameraRenderType.Overlay;
            nearData.renderShadows = true;
            nearData.renderPostProcessing = true;     // milestone 28: post effects run on the last camera of the stack (this one)
            RenderEnv.Init();
            farData.cameraStack.Add(nearCam);
        }

        void BuildSun()
        {
            var go = new GameObject("Sun");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = RenderEnv.SunLight;
            sun.color = RenderEnv.SunColor;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            go.transform.rotation = Quaternion.LookRotation(-SunDirection);
        }

        void Update()
        {
            PlanetBaker.Update();
            Coop.Current?.Update();          // milestone 30: a rocket room
            GameAudio.I?.Update();
            if (inMission) mission.Update();
            else if (inMenu) menu.Update();
            else
            {
                menu.Leave();
                if (inFlight) flight.Update();
                else hangar.Update();
            }
        }

        void OnGUI()
        {
            Ui.Begin();
            // A soft click for any press on the interface
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && Ui.MouseOverUI(In.MousePosition)) GameAudio.I?.Click();
            if (inMission) { mission.OnGUI(); return; }
            if (inMenu) menu.OnGUI();
            else if (inFlight) { flight.OnGUI(); if (!FlightMode.HideHud && !flight.photoMode) Tutorial.DrawFlight(flight); }
            else hangar.OnGUI();
            if (!inMenu && universe != null && !FlightMode.HideHud && !(inFlight && flight != null && flight.photoMode)) Badges.DrawToast(universe.career, Ui.W);   // achievements (milestone 23)
        }

        void SetWorldVisible(bool v)
        {
            foreach (var bv in bodyViews) bv.SetVisible(v);
            sky?.SetVisible(v);
            navball.SetActive(v);
        }

        /// <summary>The title screen (saves the game being played first).</summary>
        public void ShowMenu()
        {
            if (inFlight) flight.Exit();
            inFlight = false;
            if (!inMenu) universe.Save();
            hangar.Exit();
            inMenu = true;
            SetWorldVisible(true);
            navball.SetActive(false);
            menu.Enter();
        }

        /// <summary>The mission design game, from the title screen (milestone 29).</summary>
        public void ShowMission()
        {
            if (inFlight) flight.Exit();
            inFlight = false;
            hangar.Exit();               // (the builder's hall would stand in front of the camera)
            menu.Leave();
            inMenu = false;
            inMission = true;
            SetWorldVisible(true);
            navball.SetActive(false);
            nearCam.enabled = true;
            mission.Enter();
        }

        // ---- milestone 30: a rocket room
        Universe coopSavedUniverse;
        string coopSavedSlot;
        bool coopStashed;

        /// <summary>The save a campaign room's host keeps its space program in.</summary>
        public static string CoopSlotName(string room) => "Multiplayer: " + room;

        /// <summary>Into a rocket room, the hangar, each player's job. A sandbox room plays in a fresh sandbox (nothing saved);
        /// a campaign room plays the host's own saved space program ("Multiplayer: room name"), which the other players see.</summary>
        public void EnterCoop(NetRoom room)
        {
            if (!coopStashed) { coopSavedUniverse = universe; coopSavedSlot = SaveSlots.Current; coopStashed = true; }
            bool camp = room.State.campaign;
            SaveSlots.Current = null;
            universe = new Universe();
            universe.Reset(camp ? GameMode.Career : GameMode.Sandbox);
            if (camp && room.IsHost)
            {
                string slotName = CoopSlotName(room.State.room);
                var found = SaveSlots.List().FirstOrDefault(s => s.name == slotName && s.mode == GameMode.Career);
                if (found != null) { SaveSlots.Current = found.id; universe = Universe.Load(bodies); }
                else { SaveSlots.Current = SaveSlots.Create(slotName); universe.career.suppliesOn = false; }
                if (!universe.career.IsCareer) universe.Reset(GameMode.Career);
            }
            universe.wanderersOn = false;
            universe.career.guide = Guide.Closed;     // (the first-flight guide is for playing alone)
            Coop.Current = new Coop(this, room);
            mission.LeaveKeepRoom();
            inMission = false;
            inMenu = false;
            menu.Leave();
            if (camp && room.IsHost) universe.Save();
            currentDesign = camp ? VesselDesign.FirstFlight() : VesselDesign.Default();
            Coop.Current.UseRoomDesign();
            EnterHangar();
            hangar.spectate = !Coop.Current.Has(Coop.Builder);
        }

        public void LeaveCoop()
        {
            var c = Coop.Current;
            Coop.Current = null;
            c?.room.Stop();
            if (inFlight) flight.Exit();
            inFlight = false;
            hangar.Exit();
            hangar.spectate = false;
            if (c != null && c.IsHost && c.Campaign && SaveSlots.Current != null) universe.Save();   // the team's space program, kept for next time
            if (coopStashed) { universe = coopSavedUniverse; SaveSlots.Current = coopSavedSlot; coopStashed = false; }
            inMission = false;
            inMenu = true;
            SetWorldVisible(true);
            navball.SetActive(false);
            menu.Enter();
        }

        /// <summary>A player (not the host) joins the host's flight.</summary>
        public void BeginRemoteFlight(VesselSave save)
        {
            hangar.Exit();
            menu.Leave();
            inMenu = false;
            inFlight = true;
            SetWorldVisible(true);
            flight.BeginRemote(save);
        }

        public void LeaveMission()
        {
            mission.Leave();
            inMission = false;
            inMenu = true;
            SetWorldVisible(true);
            navball.SetActive(false);
            menu.Enter();
        }

        /// <summary>The space centre from the air, its buildings clickable (milestone 25).</summary>
        public void ShowCentre()
        {
            ShowMenu();
            menu.OpenCentre();
        }

        /// <summary>Plays a saved game.</summary>
        public void OpenSlot(string id)
        {
            SaveSlots.Current = id;
            SaveSlots.Last = id;
            universe = Universe.Load(bodies);
            LeaveMenu(DesignStore.LoadLast() ?? VesselDesign.Default());
        }

        /// <summary>Starts a new saved game.</summary>
        public void NewGame(string name, GameMode mode, bool supplies)
        {
            string id = SaveSlots.Create(name);
            SaveSlots.Current = id;
            SaveSlots.Last = id;
            universe = new Universe();
            universe.Reset(mode);
            universe.career.suppliesOn = supplies;
            if (Universe.IsAutotest) universe.wanderersOn = false;
            universe.Save();
            LeaveMenu(mode == GameMode.Career ? VesselDesign.FirstFlight() : VesselDesign.Default());
        }

        void LeaveMenu(VesselDesign design)
        {
            inMenu = false;
            menu.Leave();
            nearCam.enabled = true;
            currentDesign = design;
            EnterHangar();
        }

        public void EnterHangar()
        {
            inMenu = false;
            menu.Leave();
            flight.Exit();
            inFlight = false;
            universe.Save();
            SetWorldVisible(false);
            Mats.Lit.SetFloat("_Ambient", 0.24f); // the flight dims it at night
            SunNow = SunDirection;                 // and the builder has its own fixed light
            sun.transform.rotation = Quaternion.LookRotation(-SunDirection);
            hangar.Enter(currentDesign);
        }

        public void Launch(VesselDesign design, bool runway = false, int pad = 0)
        {
            currentDesign = design.Clone();
            DesignStore.SaveLast(currentDesign);
            revertSnapshot = universe.ToJson();
            universe.career.ChargeLaunch(currentDesign);
            hangar.Exit();
            menu.Leave();
            inMenu = false;
            inFlight = true;
            SetWorldVisible(true);
            flight.Begin(currentDesign, runway, pad);
        }

        /// <summary>Takes control of a vessel that's already out there (from the vessel list).</summary>
        public void Fly(VesselSim v)
        {
            if (v == null || !universe.vessels.Contains(v)) return;
            revertSnapshot = null;
            hangar.Exit();
            inFlight = true;
            SetWorldVisible(true);
            flight.BeginExisting(v);
        }

        public bool CanRevert => revertSnapshot != null;

        /// <summary>Puts the universe back the way it was before this launch and launches again.</summary>
        public void Revert(VesselDesign design, bool runway)
        {
            if (revertSnapshot == null) return;
            flight.Exit();
            universe.LoadJson(revertSnapshot, bodies);
            universe.career.ChargeLaunch(design);
            flight.Begin(design, runway);
        }

        void OnApplicationQuit()
        {
            if (inFlight) flight.Exit();
            if (!inMenu) universe.Save();
        }
    }

    public static class DesignStore
    {
        static string Dir => Path.Combine(Application.persistentDataPath, "Designs");
        const string LastName = "_last";

        static string Safe(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return string.IsNullOrWhiteSpace(s) ? "Untitled" : s.Trim();
        }

        public static string Save(VesselDesign d)
        {
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, Safe(d.name) + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(d, true));
            return path;
        }

        public static VesselDesign Load(string name)
        {
            try
            {
                string path = Path.Combine(Dir, Safe(name) + ".json");
                if (!File.Exists(path)) return null;
                var d = JsonUtility.FromJson<VesselDesign>(File.ReadAllText(path));
                if (d == null || d.parts == null) return null;
                d.parts.RemoveAll(id => PartCatalog.Get(id) == null);
                return d;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Couldn't load design: " + e.Message);
                return null;
            }
        }

        public static string[] List()
        {
            if (!Directory.Exists(Dir)) return new string[0];
            return Directory.GetFiles(Dir, "*.json").Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != LastName).OrderBy(n => n).ToArray();
        }

        public static void SaveLast(VesselDesign d)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, LastName + ".json"), JsonUtility.ToJson(d, true));
            }
            catch (Exception e) { Debug.LogWarning("Couldn't save last design: " + e.Message); }
        }

        public static VesselDesign LoadLast()
        {
            var d = Load(LastName);
            return d != null && d.parts.Count > 0 ? d : null;
        }
    }
}
