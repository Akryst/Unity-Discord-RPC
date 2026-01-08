#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.Threading.Tasks;
using Discord;
using Debug = UnityEngine.Debug;
using System.Diagnostics;

// Unity Editor Rich Presence for Discord
// Based on MarshMello0's code: https://github.com/MarshMello0/Editor-Rich-Presence
// Enhanced by Akryst

[InitializeOnLoad]
public static class UERP
{
    private const string applicationId = "1458858322596855908";
    private static Discord.Discord discord;
    private static long startTimestamp;
    private static bool playMode = false;
    private static bool isInitialized = false;
    private static float nextUpdateTime = 0f;
    private static string lastSceneName = "";
    private static string lastProjectName = "";
    private const float updateInterval = 2f;

    #region Initialization
    static UERP()
    {
        DelayStart();
        EditorApplication.quitting += Cleanup;
    }

    private static async void DelayStart(int delay = 1000)
    {
        await Task.Delay(delay);
        
        if (!DiscordRunning())
        {
            Debug.LogWarning("[UERP] Discord not running. Rich Presence disabled.");
            return;
        }
        
        Init();
    }

    private static void Init()
    {
        if (isInitialized) return;

        try
        {
            discord = new Discord.Discord(long.Parse(applicationId), (long)CreateFlags.Default);
            startTimestamp = DateTimeOffset.Now.AddMilliseconds(-EditorAnalyticsSessionInfo.elapsedTime).ToUnixTimeSeconds();
            
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
            
            // Verificar cambios periódicamente
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
        if (EditorApplication.isPlaying != playMode)
        {
            playMode = EditorApplication.isPlaying;
            UpdateActivity();
        }
    }

    private static void UpdateActivity()
    {
        if (discord == null) return;

        try
        {
            var activity = new Activity
            {
                State = EditorSceneManager.GetActiveScene().name,
                Details = Application.productName,
                Timestamps = { Start = startTimestamp },
                Assets =
                {
                    LargeImage = "unity",
                    LargeText = "Unity " + Application.unityVersion,
                    SmallImage = playMode ? "play" : "edit",
                    SmallText = playMode ? "Playing" : "Editing"
                }
            };

            discord.GetActivityManager().UpdateActivity(activity, result =>
            {
                if (result == Result.Ok)
                {
                    Debug.Log($"[UERP] Updated: {activity.Details} - {activity.State}");
                }
                else
                {
                    Debug.LogWarning($"[UERP] Update failed: {result}");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError("[UERP] Update error: " + e.Message);
        }
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
        Process[] processes = Process.GetProcessesByName("Discord");
        
        if (processes.Length == 0)
            processes = Process.GetProcessesByName("DiscordPTB");
            
        if (processes.Length == 0)
            processes = Process.GetProcessesByName("DiscordCanary");

        return processes.Length > 0;
    }
    #endregion
}
#endif