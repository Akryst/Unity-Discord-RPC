#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class UERPSettings : EditorWindow
{
    private Vector2 scrollPos;
    private Texture2D logoTexture;

    private bool enableRPC = true;
    private bool showProjectName = true;
    private bool showSceneName = true;
    private bool showSelection = true;
    private string customProjectName = "";

    private bool button1Enabled = true;
    private string button1Label = "Add me on VCC";
    private string button1URL = "https://vpm.akryst.moe";

    private bool button2Enabled = false;
    private string button2Label = "GitHub";
    private string button2URL = "";

    private float updateInterval = 2f;
    private bool autoReconnect = true;
    private bool debugLogging = false;

    private GUIStyle headerStyle;
    private GUIStyle sectionStyle;
    private GUIStyle boxStyle;
    private GUIStyle grayLabelStyle;

    [MenuItem("Window/Discord RPC Settings")]
    public static void ShowWindow()
    {
        var window = GetWindow<UERPSettings>("Discord RPC");
        window.minSize = new Vector2(450, 650);
        window.Show();
    }

    private void OnEnable()
    {
        LoadSettings();
        LoadLogo();
        InitStyles();
    }

    private void LoadLogo()
    {
        string[] guids = AssetDatabase.FindAssets("logo t:Texture2D", new[] { "Packages/com.akryst.unity-discord-rpc" });
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    private void InitStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                margin = new RectOffset(0, 0, 10, 5)
            };
        }

        if (sectionStyle == null)
        {
            sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 10, 5)
            };
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };
        }

        if (grayLabelStyle == null)
        {
            grayLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.gray }
            };
        }
    }

    private void OnGUI()
    {
        InitStyles();

        DrawHeader();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawMainToggle();
        EditorGUI.BeginDisabledGroup(!enableRPC);

        DrawDisplayOptions();
        DrawCustomText();
        DrawButtons();
        DrawAdvancedSettings();

        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);
        DrawActionButtons();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.BeginHorizontal(GUILayout.Height(60));

        if (logoTexture != null)
        {
            GUILayout.Label(logoTexture, GUILayout.Width(48), GUILayout.Height(48));
            GUILayout.Space(10);
        }

        EditorGUILayout.BeginVertical();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("Discord Rich Presence", headerStyle);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Akryst", EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
        {
            Application.OpenURL("https://akryst.moe");
        }
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

        GUILayout.Label(" // ", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

        if (GUILayout.Button("Discord", EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
        {
            Application.OpenURL("https://discord.akryst.moe");
        }
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

        GUILayout.FlexibleSpace();

        // Status indicator
        GUILayout.Label(UERP.IsConnected ? "🟢" : "🔴", GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void DrawMainToggle()
    {
        EditorGUILayout.BeginVertical(boxStyle);

        enableRPC = EditorGUILayout.Toggle(new GUIContent("Enable Rich Presence", "Toggle Discord RPC on/off"), enableRPC);

        if (!enableRPC)
        {
            EditorGUILayout.HelpBox("Discord Rich Presence is currently disabled", MessageType.Warning);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawDisplayOptions()
    {
        GUILayout.Space(5);
        EditorGUILayout.LabelField("Display Options", sectionStyle);

        EditorGUILayout.BeginVertical(boxStyle);
        showProjectName = EditorGUILayout.ToggleLeft(new GUIContent("Show Project Name", "Display your project name in Discord"), showProjectName);
        showSceneName = EditorGUILayout.ToggleLeft(new GUIContent("Show Scene Name", "Display current scene name"), showSceneName);
        showSelection = EditorGUILayout.ToggleLeft(new GUIContent("Show Selected Object", "Display the GameObject/asset you're editing"), showSelection);
        EditorGUILayout.EndVertical();
    }

    private void DrawCustomText()
    {
        GUILayout.Space(5);
        EditorGUILayout.LabelField("Customization", sectionStyle);

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Custom Project Name (optional)", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Leave empty to use actual project name", grayLabelStyle);
        customProjectName = EditorGUILayout.TextField(customProjectName);
        EditorGUILayout.EndVertical();
    }

    private void DrawButtons()
    {
        GUILayout.Space(5);
        EditorGUILayout.LabelField("Clickable Buttons", sectionStyle);

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.HelpBox("Discord allows up to 2 clickable buttons in your status", MessageType.Info);

        GUILayout.Space(5);
        DrawButton1();
        GUILayout.Space(10);
        DrawButton2();

        EditorGUILayout.EndVertical();
    }

    private void DrawButton1()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        button1Enabled = EditorGUILayout.ToggleLeft("Button 1", button1Enabled, EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(!button1Enabled);
        EditorGUI.indentLevel++;
        button1Label = EditorGUILayout.TextField("Label", button1Label);
        button1URL = EditorGUILayout.TextField("URL", button1URL);
        EditorGUI.indentLevel--;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawButton2()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        button2Enabled = EditorGUILayout.ToggleLeft("Button 2", button2Enabled, EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(!button2Enabled);
        EditorGUI.indentLevel++;
        button2Label = EditorGUILayout.TextField("Label", button2Label);
        button2URL = EditorGUILayout.TextField("URL", button2URL);
        EditorGUI.indentLevel--;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawAdvancedSettings()
    {
        GUILayout.Space(5);
        EditorGUILayout.LabelField("Advanced", sectionStyle);

        EditorGUILayout.BeginVertical(boxStyle);

        updateInterval = EditorGUILayout.Slider(
            new GUIContent("Update Interval", "How often to update Discord (seconds)"),
            updateInterval, 1f, 10f);
        EditorGUILayout.LabelField($"Updates every {updateInterval:F1} seconds", grayLabelStyle);

        GUILayout.Space(5);

        autoReconnect = EditorGUILayout.ToggleLeft(
            new GUIContent("Auto-reconnect", "Automatically reconnect if Discord restarts"),
            autoReconnect);

        debugLogging = EditorGUILayout.ToggleLeft(
            new GUIContent("Debug Logging", "Show detailed logs in Console"),
            debugLogging);

        EditorGUILayout.EndVertical();
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
        if (GUILayout.Button("Apply Settings", GUILayout.Height(30), GUILayout.Width(120)))
        {
            SaveSettings();
            UERP.ApplySettings();
            ShowNotification(new GUIContent("✓ Settings applied!"));
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);

        if (GUILayout.Button("Reset to Defaults", GUILayout.Height(30), GUILayout.Width(140)))
        {
            if (EditorUtility.DisplayDialog("Reset Settings", "Reset all settings to default values?", "Yes", "Cancel"))
            {
                ResetToDefaults();
                ShowNotification(new GUIContent("✓ Settings reset!"));
            }
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
    }

    private void LoadSettings()
    {
        enableRPC = EditorPrefs.GetBool("UERP_Enabled", true);
        showProjectName = EditorPrefs.GetBool("UERP_ShowProject", true);
        showSceneName = EditorPrefs.GetBool("UERP_ShowScene", true);
        showSelection = EditorPrefs.GetBool("UERP_ShowSelection", true);
        customProjectName = EditorPrefs.GetString("UERP_CustomProject", "");

        button1Enabled = EditorPrefs.GetBool("UERP_Button1_Enabled", true);
        button1Label = EditorPrefs.GetString("UERP_Button1_Label", "Add me on VCC");
        button1URL = EditorPrefs.GetString("UERP_Button1_URL", "https://vpm.akryst.moe");

        button2Enabled = EditorPrefs.GetBool("UERP_Button2_Enabled", false);
        button2Label = EditorPrefs.GetString("UERP_Button2_Label", "GitHub");
        button2URL = EditorPrefs.GetString("UERP_Button2_URL", "");

        updateInterval = EditorPrefs.GetFloat("UERP_UpdateInterval", 2f);
        autoReconnect = EditorPrefs.GetBool("UERP_AutoReconnect", true);
        debugLogging = EditorPrefs.GetBool("UERP_DebugLogging", false);
    }

    private void SaveSettings()
    {
        EditorPrefs.SetBool("UERP_Enabled", enableRPC);
        EditorPrefs.SetBool("UERP_ShowProject", showProjectName);
        EditorPrefs.SetBool("UERP_ShowScene", showSceneName);
        EditorPrefs.SetBool("UERP_ShowSelection", showSelection);
        EditorPrefs.SetString("UERP_CustomProject", customProjectName);

        EditorPrefs.SetBool("UERP_Button1_Enabled", button1Enabled);
        EditorPrefs.SetString("UERP_Button1_Label", button1Label);
        EditorPrefs.SetString("UERP_Button1_URL", button1URL);

        EditorPrefs.SetBool("UERP_Button2_Enabled", button2Enabled);
        EditorPrefs.SetString("UERP_Button2_Label", button2Label);
        EditorPrefs.SetString("UERP_Button2_URL", button2URL);

        EditorPrefs.SetFloat("UERP_UpdateInterval", updateInterval);
        EditorPrefs.SetBool("UERP_AutoReconnect", autoReconnect);
        EditorPrefs.SetBool("UERP_DebugLogging", debugLogging);
    }

    private void ResetToDefaults()
    {
        enableRPC = true;
        showProjectName = true;
        showSceneName = true;
        showSelection = true;
        customProjectName = "";

        button1Enabled = true;
        button1Label = "Add me on VCC";
        button1URL = "https://vpm.akryst.moe";

        button2Enabled = false;
        button2Label = "GitHub";
        button2URL = "";

        updateInterval = 2f;
        autoReconnect = true;
        debugLogging = false;

        SaveSettings();
    }
}
#endif