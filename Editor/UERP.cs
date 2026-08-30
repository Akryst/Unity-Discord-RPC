#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.Threading.Tasks;
using DiscordRPC;
using DiscordRPC.Logging;
using Debug = UnityEngine.Debug;
using System.Diagnostics;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if VRC_SDK_VRCSDK3
using VRC.SDKBase.Editor.BuildPipeline;
#endif

[InitializeOnLoad]
public static class UERP
{
    private const string applicationId = "1458858322596855908";
    private static DiscordRpcClient client;
    private static long startTimestamp;
    private static bool playMode = false;
    private static bool isInitialized = false;
    private static bool isCleaningUp = false;
    private static float nextUpdateTime = 0f;
    private static string lastSceneName = "";
    private static string lastProjectName = "";
    private static bool isBuilding = false;
    private static string buildStateText = "";
    private static System.Threading.CancellationTokenSource startCts;

    public static UERPConfig Config { get; private set; }

    public static bool IsConnected => isInitialized && client != null && client.IsInitialized;

    public class UERPConfig
    {
        public bool enabled = true;
        public bool showProjectName = true;
        public bool showSceneName = true;
        public bool showSelection = true;
        public string customProjectName = "";
        public bool button1Enabled = true;
        public string button1Label = "Add me on VCC";
        public string button1URL = "https://vpm.akryst.moe";
        public bool button2Enabled = false;
        public string button2Label = "";
        public string button2URL = "";
        public float updateInterval = 2f;
    }

    public static void ApplySettings()
    {
        LoadConfig();
        if (isInitialized)
        {
            if (!Config.enabled)
            {
                client?.ClearPresence();
            }
            else
            {
                UpdateActivity();
            }
        }
    }

    static UERP()
    {
        LoadConfig();
        DelayStart();
        EditorApplication.quitting += Cleanup;
        AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
    }

    private static void LoadConfig()
    {
        Config = new UERPConfig
        {
            enabled = EditorPrefs.GetBool("UERP_Enabled", true),
            showProjectName = EditorPrefs.GetBool("UERP_ShowProject", true),
            showSceneName = EditorPrefs.GetBool("UERP_ShowScene", true),
            showSelection = EditorPrefs.GetBool("UERP_ShowSelection", true),
            customProjectName = EditorPrefs.GetString("UERP_CustomProject", ""),
            button1Enabled = EditorPrefs.GetBool("UERP_Button1_Enabled", true),
            button1Label = EditorPrefs.GetString("UERP_Button1_Label", "Add me on VCC"),
            button1URL = EditorPrefs.GetString("UERP_Button1_URL", "https://vpm.akryst.moe"),
            button2Enabled = EditorPrefs.GetBool("UERP_Button2_Enabled", false),
            button2Label = EditorPrefs.GetString("UERP_Button2_Label", ""),
            button2URL = EditorPrefs.GetString("UERP_Button2_URL", ""),
            updateInterval = EditorPrefs.GetFloat("UERP_UpdateInterval", 2f)
        };
    }

    private static async void DelayStart(int delay = 1000)
    {
        try
        {
            startCts = new System.Threading.CancellationTokenSource();
            await Task.Delay(delay, startCts.Token);

            if (!DiscordRunning())
            {
                Debug.LogWarning("[UERP] Discord not running. Rich Presence disabled.");
                return;
            }

            Init();
        }
        catch (System.Threading.Tasks.TaskCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            Debug.LogError("[UERP] Start error: " + e.Message);
        }
    }

    private static void Init()
    {
        if (isInitialized || !Config.enabled) return;

        try
        {
            client = new DiscordRpcClient(applicationId, autoEvents: false);
            client.Logger = new UnityLogger();
            client.Initialize();

            long elapsed = (long)Math.Max(0, EditorAnalyticsSessionInfo.elapsedTime);
            startTimestamp = DateTimeOffset.Now.AddMilliseconds(-elapsed).ToUnixTimeSeconds();

            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
            Selection.selectionChanged += OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            isInitialized = true;
            lastSceneName = EditorSceneManager.GetActiveScene().name;
            lastProjectName = Application.productName;

            Debug.Log("[UERP] Discord Rich Presence initialized");

            UpdateActivity();
        }
        catch (Exception e)
        {
            Debug.LogError("[UERP] Initialization failed: " + e.Message);
        }
    }

    private static void Update()
    {
        if (!isInitialized || client == null) return;

        try
        {
            client.Invoke();

            if (!Config.enabled)
            {
                if (client.CurrentPresence != null)
                {
                    client.ClearPresence();
                }
                return;
            }

            if (Time.realtimeSinceStartup >= nextUpdateTime)
            {
                nextUpdateTime = Time.realtimeSinceStartup + Config.updateInterval;

                string currentScene = EditorSceneManager.GetActiveScene().name;
                string currentProject = Application.productName;

                if (currentScene != lastSceneName || currentProject != lastProjectName)
                {
                    lastSceneName = currentScene;
                    lastProjectName = currentProject;
                    UpdateActivity();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[UERP] Update error: " + e.Message);
        }
    }

    private static void OnSceneChanged(UnityEngine.SceneManagement.Scene current, UnityEngine.SceneManagement.Scene next)
    {
        UpdateActivity();
    }

    private static void OnSelectionChanged()
    {
        UpdateActivity();
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        bool isPlaying = EditorApplication.isPlaying;
        if (isPlaying != playMode)
        {
            playMode = isPlaying;
            UpdateActivity();
        }
    }

    private static void UpdateActivity()
    {
        if (client == null || !client.IsInitialized) return;

        try
        {
            string sceneName = EditorSceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) sceneName = "Untitled Scene";

            string detailsText;
            string stateText;
            string smallImageText;

            string projectName = Config.showProjectName
                ? (string.IsNullOrEmpty(Config.customProjectName) ? Application.productName : Config.customProjectName)
                : "Unity Editor";

            if (isBuilding)
            {
                detailsText = projectName;
                stateText = buildStateText;
                smallImageText = buildStateText;
            }
            else if (Selection.activeObject is Material mat && Config.showSelection)
            {
                detailsText = projectName;
                stateText = $"Editing material: {mat.name}";
                smallImageText = Config.showSceneName ? $"{sceneName}" : "Editing";
            }
            else if (Selection.activeObject is Mesh mesh && Config.showSelection)
            {
                detailsText = projectName;
                stateText = $"Editing mesh: {mesh.name}";
                smallImageText = Config.showSceneName ? $"{sceneName}" : "Editing";
            }
            else if (Selection.activeGameObject != null && Config.showSelection)
            {
                var go = Selection.activeGameObject;
                var renderer = go.GetComponent<Renderer>();

                detailsText = projectName;

                if (renderer != null && renderer.sharedMaterials.Length > 0)
                {
                    stateText = $"Editing {go.name}";
                }
                else
                {
                    stateText = $"Editing {go.name}";
                }

                smallImageText = Config.showSceneName ? $"Scene: {sceneName}" : "Editing";
            }
            else
            {
                detailsText = projectName;
                stateText = Config.showSceneName ? sceneName : "Editing";
                smallImageText = playMode ? "Playing" : "Editing";
            }

            var buttons = new System.Collections.Generic.List<DiscordRPC.Button>();
            if (Config.button1Enabled && !string.IsNullOrEmpty(Config.button1URL))
                buttons.Add(new DiscordRPC.Button { Label = Config.button1Label, Url = Config.button1URL });
            if (Config.button2Enabled && !string.IsNullOrEmpty(Config.button2URL))
                buttons.Add(new DiscordRPC.Button { Label = Config.button2Label, Url = Config.button2URL });

            var presence = new RichPresence();

            if (Config.showProjectName && !string.IsNullOrEmpty(detailsText))
            {
                presence.Details = detailsText;
            }

            if (!string.IsNullOrEmpty(stateText))
            {
                presence.State = stateText;
            }

            presence.Assets = new Assets
            {
                LargeImageKey = "unity",
                LargeImageText = "Unity " + Application.unityVersion,
                SmallImageKey = playMode ? "play" : "edit",
                SmallImageText = smallImageText
            };

            if (buttons.Count > 0)
            {
                presence.Buttons = buttons.ToArray();
            }

            presence.Timestamps = new Timestamps { Start = DateTime.UtcNow.AddSeconds(-(DateTimeOffset.Now.ToUnixTimeSeconds() - startTimestamp)) };

            client.SetPresence(presence);
        }
        catch (Exception e)
        {
            Debug.LogError("[UERP] Update error: " + e.Message);
        }
    }

    internal static void SetBuildState(bool building, string stateText = "")
    {
        isBuilding = building;
        buildStateText = stateText;
        UpdateActivity();
    }

    private static void Cleanup()
    {
        if (isCleaningUp || client == null) return;

        lock (typeof(UERP))
        {
            if (isCleaningUp || client == null) return;
            isCleaningUp = true;
        }

        try
        {
            startCts?.Cancel();
            startCts?.Dispose();

            EditorApplication.update -= Update;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.quitting -= Cleanup;
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;

            client?.ClearPresence();
            client?.Dispose();
            client = null;
            isInitialized = false;

            Debug.Log("[UERP] Cleaned up");
        }
        catch (Exception e)
        {
            Debug.LogError("[UERP] Cleanup error: " + e.Message);
        }
        finally
        {
            isCleaningUp = false;
        }
    }

    private static bool DiscordRunning()
    {
        string[] processNames = { "Discord", "DiscordPTB", "DiscordCanary", "discord" };
        foreach (string name in processNames)
        {
            Process[] procs = Process.GetProcessesByName(name);
            try
            {
                if (procs.Length > 0) return true;
            }
            finally
            {
                foreach (Process p in procs) p?.Dispose();
            }
        }
        return false;
    }

    private class UnityLogger : DiscordRPC.Logging.ILogger
    {
        public DiscordRPC.Logging.LogLevel Level { get; set; } = DiscordRPC.Logging.LogLevel.Warning;

        public void Trace(string message, params object[] args)
        {
            if (Level <= DiscordRPC.Logging.LogLevel.Trace)
                Debug.Log($"[UERP] {string.Format(message, args)}");
        }

        public void Info(string message, params object[] args)
        {
            if (Level <= DiscordRPC.Logging.LogLevel.Info)
                Debug.Log($"[UERP] {string.Format(message, args)}");
        }

        public void Warning(string message, params object[] args)
        {
            if (Level <= DiscordRPC.Logging.LogLevel.Warning)
                Debug.LogWarning($"[UERP] {string.Format(message, args)}");
        }

        public void Error(string message, params object[] args)
        {
            if (Level <= DiscordRPC.Logging.LogLevel.Error)
                Debug.LogError($"[UERP] {string.Format(message, args)}");
        }
    }
}

class UERPBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        UERP.SetBuildState(true, "Building Player");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        UERP.SetBuildState(false);
    }
}

#if VRC_SDK_VRCSDK3
class UERPVRCCallback : IVRCSDKBuildRequestedCallback, IVRCSDKPostprocessAvatarCallback
{
    public int callbackOrder => 0;

    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        UERP.SetBuildState(true, requestedBuildType == VRCSDKRequestedBuildType.Avatar
            ? "Uploading Avatar to VRChat"
            : "Uploading World to VRChat");
        return true;
    }

    public void OnPostprocessAvatar()
    {
        UERP.SetBuildState(false);
    }
}
#endif

#endif