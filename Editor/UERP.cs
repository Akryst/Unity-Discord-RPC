#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.Threading.Tasks;
using Discord;
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
    private const long applicationId = 1458858322596855908L;
    private static Discord.Discord discord;
    private static long startTimestamp;
    private static bool playMode = false;
    private static bool isInitialized = false;
    private static float nextUpdateTime = 0f;
    private static string lastSceneName = "";
    private static string lastProjectName = "";
    private const float updateInterval = 2f;
    private static bool isBuilding = false;
    private static string buildStateText = "";

    #region Initialization
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
            await Task.Delay(delay);

            if (!DiscordRunning())
            {
                Debug.LogWarning("[UERP] Discord not running. Rich Presence disabled.");
                return;
            }

            Init();
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
            discord = new Discord.Discord(applicationId, (long)CreateFlags.Default);
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
    #endregion

    #region Update
    private static void Update()
    {
        try
        {
            discord?.RunCallbacks();

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
            Debug.LogError("[UERP] Callback error: " + e.Message);
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
        if (discord == null) return;

        try
        {
            string sceneName = EditorSceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) sceneName = "Untitled Scene";

            var activity = new Activity
            {
                Details = Application.productName,
                State = isBuilding ? buildStateText : sceneName,
                Timestamps = { Start = startTimestamp },
                Assets =
                {
                    LargeImage = "unity",
                    LargeText = "Unity " + Application.unityVersion,
                    SmallImage = playMode ? "play" : "edit",
                    SmallText = isBuilding ? buildStateText : (playMode ? "Playing" : "Editing")
                }
            };

            discord.GetActivityManager().UpdateActivity(activity, result =>
            {
                if (result != Result.Ok)
                    Debug.LogWarning($"[UERP] Update failed: {result}");
            });
        }
        catch (Exception e)
        {
            Debug.LogError("[UERP] Update error: " + e.Message);
        }
    }
    #endregion

    #region Build State
    internal static void SetBuildState(bool building, string stateText = "")
    {
        isBuilding = building;
        buildStateText = stateText;
        UpdateActivity();
    }
    #endregion

    #region Cleanup
    private static void Cleanup()
    {
        if (discord == null) return;

        try
        {
            EditorApplication.update -= Update;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            EditorApplication.quitting -= Cleanup;
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;

            discord.Dispose();
            discord = null;
            isInitialized = false;

            Debug.Log("[UERP] Cleaned up");
        }
        catch (Exception e)
        {
            Debug.LogError("[UERP] Cleanup error: " + e.Message);
        }
    }
    #endregion

    #region Discord Detection
    private static bool DiscordRunning()
    {
        string[] processNames = { "Discord", "DiscordPTB", "DiscordCanary", "discord" };
        foreach (string name in processNames)
        {
            Process[] procs = Process.GetProcessesByName(name);
            bool found = procs.Length > 0;
            foreach (Process p in procs) p.Dispose();
            if (found) return true;
        }
        return false;
    }
    #endregion
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