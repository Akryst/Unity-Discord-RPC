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
    private const float updateInterval = 2f;
    private static bool isBuilding = false;
    private static string buildStateText = "";
    private static System.Threading.CancellationTokenSource startCts;

    static UERP()
    {
        DelayStart();
        EditorApplication.quitting += Cleanup;
        AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
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
        if (isInitialized) return;

        try
        {
            client = new DiscordRpcClient(applicationId);
            client.Logger = new UnityLogger();
            client.Initialize();

            long elapsed = (long)Math.Max(0, EditorAnalyticsSessionInfo.elapsedTime);
            startTimestamp = DateTimeOffset.Now.AddMilliseconds(-elapsed).ToUnixTimeSeconds();

            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;

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

            if (Time.realtimeSinceStartup >= nextUpdateTime)
            {
                nextUpdateTime = Time.realtimeSinceStartup + updateInterval;

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

            client.SetPresence(new RichPresence
            {
                Details = Application.productName,
                State = isBuilding ? buildStateText : sceneName,
                Timestamps = new Timestamps { Start = DateTime.UtcNow.AddSeconds(-(DateTimeOffset.Now.ToUnixTimeSeconds() - startTimestamp)) },
                Assets = new Assets
                {
                    LargeImageKey = "unity",
                    LargeImageText = "Unity " + Application.unityVersion,
                    SmallImageKey = playMode ? "play" : "edit",
                    SmallImageText = isBuilding ? buildStateText : (playMode ? "Playing" : "Editing")
                }
            });
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