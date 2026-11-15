using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>
    /// Batch-mode entry points:
    ///   -executeMethod Rocket.EditorTools.ProjectBuilder.CreateScene
    ///   -executeMethod Rocket.EditorTools.ProjectBuilder.BuildWindows
    ///   -executeMethod Rocket.EditorTools.ProjectBuilder.BuildMac [-macArch universal|arm64|x64]
    /// </summary>
    public static class ProjectBuilder
    {
        const string ScenePath = "Assets/Game/Scenes/Main.unity";
        const string BuildPath = "Build/ArdenSpaceProgram.exe";
        const string MacBuildPath = "BuildMac/ArdenSpaceProgram.app";

        [MenuItem("Rocket/Create Main Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Game/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameRoot").AddComponent<GameRoot>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("ROCKET scene created at " + ScenePath);
        }

        static void CommonSettings()
        {
            CreateScene();
            PlayerSettings.productName = SaveSlots.ProductName;
            PlayerSettings.companyName = "captaincarl24";
            PlayerSettings.bundleVersion = "0.45";   // the milestone number: 1.0 at release
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.enableFrameTimingStats = true;   // CPU and GPU frame times for the speed check (milestone 24)
            PlayerSettings.visibleInBackground = true;
            // Drop unused .NET code (System.Xml, System.Data...) so the download stays small.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Low);
        }

        static void Run(string path, BuildTarget target)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"ROCKET BUILD {target} {s.result} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize / (1024 * 1024)}MB time={s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Rocket/Build Windows Player")]
        public static void BuildWindows()
        {
            CommonSettings();
            Run(BuildPath, BuildTarget.StandaloneWindows64);
        }

        /// <summary>
        /// macOS player (Mono backend, so it can be built from Windows). The app is unsigned:
        /// on the Mac, clear the quarantine flag (xattr -cr) before the first launch.
        /// </summary>
        [MenuItem("Rocket/Build Mac Player")]
        public static void BuildMac()
        {
            CommonSettings();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.captaincarl24.ardenspaceprogram");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            var args = Environment.GetCommandLineArgs();
            int ai = Array.IndexOf(args, "-macArch");
            string arch = ai >= 0 && ai + 1 < args.Length ? args[ai + 1].ToLowerInvariant() : "universal";
            string value = arch == "arm64" ? "ARM64" : arch == "x64" ? "x64" : "x64ARM64";
            SetMacArchitecture(value);
            Run(MacBuildPath, BuildTarget.StandaloneOSX);
        }

        static void SetMacArchitecture(string value)
        {
            // UserBuildSettings lives in the Mac support module's editor assembly, so reach it by reflection.
            bool set = false;
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.OSXStandalone.UserBuildSettings", false))
                .FirstOrDefault(x => x != null);
            var prop = t?.GetProperty("architecture", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null && prop.PropertyType.IsEnum)
            {
                try { prop.SetValue(null, Enum.Parse(prop.PropertyType, value)); set = true; }
                catch (Exception e) { Debug.LogWarning("ROCKET mac arch via UserBuildSettings failed: " + e.Message); }
            }
            if (!set) EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", value);
            Debug.Log($"ROCKET mac architecture = {value} (UserBuildSettings: {(set ? prop.GetValue(null) : "n/a")}, platform setting: {EditorUserBuildSettings.GetPlatformSettings("Standalone", "OSXUniversal", "Architecture")})");
        }
    }
}
