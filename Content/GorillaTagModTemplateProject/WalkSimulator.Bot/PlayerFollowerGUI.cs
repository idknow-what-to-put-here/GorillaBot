using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Collections.Generic;
using WalkSimulator.Rigging;
using System.IO;
using System;
using static WalkSimulator.Bot.PlayerFollower;
using static WalkSimulator.Bot.PlayerFollowerUtils;
using System.Text.RegularExpressions;
using System.Reflection;
using GorillaNetworking;

namespace WalkSimulator.Bot
{
    public class PlayerFollowerGUI : MonoBehaviour
    {
        #region Fields and Properties
        private readonly PlayerFollower follower;

        // GUI Configuration
        private const int WINDOW_WIDTH = 550;
        private const int WINDOW_HEIGHT = 675;
        private const int COLOR_PICKER_SIZE = 200;
        private Rect windowRect = new Rect(20, 20, WINDOW_WIDTH, WINDOW_HEIGHT);
        private bool showColorPicker = false;
        private bool editingPathColor = true;
        private Vector2 scrollPosition;
        private bool showPresetManager = false;

        // Theme
        public enum Theme { Dark, VeryDark, Light, Space, Purple, Oreo, RGB, Solarized, Neon, Forest }
        public static Theme currentTheme = Theme.Dark;
        private Theme lastAppliedTheme = (Theme)(-1);
        private Color windowColor;
        private Color buttonColor;
        private Color textFieldColor;
        private Color labelTextColor;

        // Preset colors
        private readonly Color[] colorPresets = new Color[]
        {
            Color.red, Color.green, Color.blue, Color.yellow, Color.cyan,
            Color.magenta, Color.white, new Color(1f, 0.5f, 0f), // Orange
            new Color(0.5f, 0f, 1f), // Purple
            new Color(0f, 1f, 0.5f)  // Mint
        };

        // Tab management
        private int selectedTab = 0;
        private readonly string[] tabNames = new string[] { "Line Settings", "Players", "Path", "Misc", "Logs" };

        // Preset management
        private string presetName = "DefaultPreset";
        public string[] presetFiles;
        private Vector2 presetScrollPosition;
        private Rect presetWindowRect = new Rect(100, 100, 400, 500);
        private Vector2 hardcodedPresetScrollPosition = Vector2.zero;
        public Dictionary<string, PathPreset> hardcodedPresets;
        public IEnumerable<string> HardcodedPresetNames => hardcodedPresets.Keys;

        // Replays & Presets sub-tabs
        private int replayPresetTabIndex = 0;
        private readonly string[] replayPresetTabNames = new string[] { "Replays", "Presets" };

        // Logging
        public static List<string> logMessages = new List<string>();
        private Vector2 logScrollPosition;

        // Styling
        private GUIStyle windowStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle verticalScrollbarStyle;
        private GUIStyle verticalScrollbarThumbStyle;
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle sliderLabelStyle;
        private Color originalColorWhenPickerOpened;

        // Constants
        public static float buttonHeight = 30f;
        public static int cornerRadius = 8;

        // Hand
        [Flags]
        public enum HandButton
        {
            Grip = 1 << 0, // 1
            Trigger = 1 << 1, // 2
            Primary = 1 << 2, // 4
            Secondary = 1 << 3  // 8
        }
        public enum ActivationPoint { OnReachGround, MidHold, OnRelease }

        // TTS Fields
        private string ttsInputText = "";
        private bool isSpeaking = false;
        private AudioClip currentTTSClip;
        #endregion

        public PlayerFollowerGUI(PlayerFollower follower)
        {
            this.follower = follower;
        }

        public void AddLogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            logMessages.Add($"[{timestamp}] {message}");
        }
        public void OnGUI()
        {
            follower.RefreshPresetFiles();
            ApplyTheme();
            InitializeStyles();
            DrawMainWindow();
            DrawColorPicker();
            DrawPresetManager();
        }

        #region GUI Initialization
        private void ApplyTheme()
        {
            if (currentTheme != lastAppliedTheme)
            {
                switch (currentTheme)
                {
                    case Theme.Dark:
                        windowColor = new Color(0.05f, 0.05f, 0.05f);
                        buttonColor = new Color(0.1f, 0.1f, 0.1f);
                        textFieldColor = new Color(0.1f, 0.1f, 0.1f);
                        labelTextColor = Color.white;
                        break;
                    case Theme.VeryDark:
                        windowColor = new Color(0.02f, 0.02f, 0.02f);
                        buttonColor = new Color(0.03f, 0.03f, 0.03f);
                        textFieldColor = new Color(0.03f, 0.03f, 0.03f);
                        labelTextColor = Color.white;
                        break;
                    case Theme.Light:
                        windowColor = new Color(0.8f, 0.8f, 0.8f);
                        buttonColor = new Color(0.9f, 0.9f, 0.9f);
                        textFieldColor = new Color(0.9f, 0.9f, 0.9f);
                        labelTextColor = Color.black;
                        break;
                    case Theme.Space:
                        windowColor = new Color(0f, 0f, 0.2f);
                        buttonColor = new Color(0f, 0f, 0.3f);
                        textFieldColor = new Color(0f, 0f, 0.3f);
                        labelTextColor = Color.cyan;
                        break;
                    case Theme.Purple:
                        windowColor = new Color(0.3f, 0f, 0.3f);
                        buttonColor = new Color(0.4f, 0f, 0.4f);
                        textFieldColor = new Color(0.4f, 0f, 0.4f);
                        labelTextColor = Color.white;
                        break;
                    case Theme.Oreo:
                        windowColor = Color.black;
                        buttonColor = new Color(0.2f, 0.2f, 0.2f);
                        textFieldColor = new Color(0.2f, 0.2f, 0.2f);
                        labelTextColor = Color.white;
                        break;
                    case Theme.RGB:
                        windowColor = Color.red;
                        buttonColor = Color.green;
                        textFieldColor = Color.blue;
                        labelTextColor = Color.green;
                        break;
                    case Theme.Solarized:
                        windowColor = new Color(0.0f, 0.168f, 0.211f);
                        buttonColor = new Color(0.282f, 0.337f, 0.396f);
                        textFieldColor = new Color(0.282f, 0.337f, 0.396f);
                        labelTextColor = new Color(0.976f, 0.976f, 0.949f);
                        break;
                    case Theme.Neon:
                        windowColor = Color.black;
                        buttonColor = new Color(0.0f, 1.0f, 1.0f);
                        textFieldColor = new Color(1.0f, 0.0f, 1.0f);
                        labelTextColor = new Color(1.0f, 1.0f, 0.0f);
                        break;
                    case Theme.Forest:
                        windowColor = new Color(0.133f, 0.545f, 0.133f);
                        buttonColor = new Color(0.180f, 0.624f, 0.180f);
                        textFieldColor = new Color(0.180f, 0.624f, 0.180f);
                        labelTextColor = new Color(0.0f, 0.392f, 0.0f);
                        break;
                }

                windowStyle = null;
                buttonStyle = null;
                labelStyle = null;
                textFieldStyle = null;
                lastAppliedTheme = currentTheme;
            }
        }
        private void InitializeStyles()
        {
            if (windowStyle == null)
            {
                windowStyle = new GUIStyle(GUI.skin.window);
                Texture2D windowTex = GUIHelper.MakeRoundedRectTexture(64, 64, windowColor, cornerRadius);
                windowStyle.normal.background = windowTex;
                windowStyle.active.background = windowTex;
                windowStyle.hover.background = windowTex;
                windowStyle.focused.background = windowTex;
                windowStyle.onNormal.background = windowTex;
                windowStyle.onActive.background = windowTex;
                windowStyle.onHover.background = windowTex;
                windowStyle.onFocused.background = windowTex;
                windowStyle.normal.textColor = labelTextColor;
                windowStyle.padding.top = 30;
                windowStyle.fontSize = 24;
                windowStyle.fontStyle = FontStyle.Bold;
                windowStyle.border = new RectOffset(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
            }

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                Texture2D btnTex = GUIHelper.MakeRoundedRectTexture(64, 64, buttonColor, cornerRadius);
                buttonStyle.normal.background = btnTex;
                buttonStyle.active.background = btnTex;
                buttonStyle.hover.background = btnTex;
                buttonStyle.focused.background = btnTex;
                buttonStyle.onNormal.background = btnTex;
                buttonStyle.onActive.background = btnTex;
                buttonStyle.onHover.background = btnTex;
                buttonStyle.onFocused.background = btnTex;
                buttonStyle.normal.textColor = labelTextColor;
                buttonStyle.fontStyle = FontStyle.Bold;
                buttonStyle.border = new RectOffset(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.normal.textColor = labelTextColor;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.alignment = TextAnchor.MiddleCenter;
            }

            if (textFieldStyle == null)
            {
                textFieldStyle = new GUIStyle(GUI.skin.textField);
                Texture2D textFieldTex = GUIHelper.MakeTex(2, 2, textFieldColor);
                textFieldStyle.normal.background = textFieldTex;
                textFieldStyle.active.background = textFieldTex;
                textFieldStyle.hover.background = textFieldTex;
                textFieldStyle.focused.background = textFieldTex;
                textFieldStyle.onNormal.background = textFieldTex;
                textFieldStyle.onActive.background = textFieldTex;
                textFieldStyle.onHover.background = textFieldTex;
                textFieldStyle.onFocused.background = textFieldTex;
                textFieldStyle.normal.textColor = labelTextColor;
                textFieldStyle.alignment = TextAnchor.MiddleCenter;
            }

            if (verticalScrollbarStyle == null)
            {
                verticalScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
                verticalScrollbarStyle.fixedWidth = 12;
                verticalScrollbarStyle.normal.background = GUIHelper.MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f));
            }

            if (verticalScrollbarThumbStyle == null)
            {
                verticalScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
                verticalScrollbarThumbStyle.fixedWidth = 12;
                verticalScrollbarThumbStyle.normal.background = GUIHelper.MakeTex(2, 2, new Color(0.3f, 0.3f, 0.3f));
            }

            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(labelStyle);
                headerStyle.fontSize = 16;
                headerStyle.fontStyle = FontStyle.Bold;
                headerStyle.alignment = TextAnchor.MiddleCenter;
            }

            if (sectionStyle == null)
            {
                sectionStyle = new GUIStyle(GUI.skin.box);
                sectionStyle.padding = new RectOffset(10, 10, 10, 10);
                sectionStyle.margin = new RectOffset(0, 0, 5, 5);
            }

            if (sliderLabelStyle == null)
            {
                sliderLabelStyle = new GUIStyle(labelStyle);
                sliderLabelStyle.fontSize = 12;
                sliderLabelStyle.alignment = TextAnchor.MiddleLeft;
            }

            GUI.skin.verticalScrollbar = verticalScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = verticalScrollbarThumbStyle;
        }
        private void DrawMainWindow()
        {
            float designWidth = 1920f;
            float designHeight = 1080f;
            float scaleX = Screen.width / designWidth;
            float scaleY = Screen.height / designHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1));

            windowRect = GUI.Window(0, windowRect, DrawWindow, "Player Follower", windowStyle);
        }
        private void DrawColorPicker()
        {
            if (showColorPicker)
            {
                Rect colorPickerRect = new Rect(
                    windowRect.x + windowRect.width + 10,
                    windowRect.y,
                    COLOR_PICKER_SIZE,
                    COLOR_PICKER_SIZE
                );
                GUI.Window(1, colorPickerRect, DrawColorPicker, "Color Picker", windowStyle);
            }
        }
        private void DrawPresetManager()
        {
            if (showPresetManager)
            {
                presetWindowRect = GUI.Window(2, presetWindowRect, DrawPresetManagerWindow, "Preset Manager", windowStyle);
            }
        }
        #endregion
        #region Window Drawing
        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                bool isSelected = (selectedTab == i);
                Color origColor = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = buttonColor;
                if (GUI.Button(new Rect(10 + i * ((WINDOW_WIDTH - 20) / tabNames.Length), 45, (WINDOW_WIDTH - 20) / tabNames.Length, 30), tabNames[i], buttonStyle))
                {
                    selectedTab = i;
                }
                GUI.backgroundColor = origColor;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(40);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            switch (selectedTab)
            {
                case 0: DrawLineSettings(); break;
                case 1: DrawPlayerList(); break;
                case 2: DrawPathDestinations(); break;
                case 3: DrawMisc(); break;
                case 4: DrawLogsTab(); break;
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUI.Button(new Rect(10, WINDOW_HEIGHT - 27, WINDOW_WIDTH - 20, 30), $"Theme: {currentTheme}", buttonStyle))
            {
                currentTheme = (Theme)(((int)currentTheme + 1) % Enum.GetValues(typeof(Theme)).Length);
                ApplyTheme();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        private void DrawLineSettings()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Line Settings", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);
            follower.showPath = GUILayout.Toggle(follower.showPath, "Show Path Line");
            if (follower.showPath)
            {
                if (GUILayout.Button($"Path Color: #{follower.lineRenderers.ColorToHex(follower.pathColor)}"))
                {
                    showColorPicker = true;
                    editingPathColor = true;
                    originalColorWhenPickerOpened = follower.pathColor;
                }
                GUILayout.Label($"Path Width: {follower.pathLineWidth:F2}", sliderLabelStyle);
                follower.pathLineWidth = GUILayout.HorizontalSlider(follower.pathLineWidth, 0.01f, 0.2f);
                follower.lineRenderers.pathLine.Renderer.startWidth = follower.pathLineWidth;
                follower.lineRenderers.pathLine.Renderer.endWidth = follower.pathLineWidth;
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            follower.showDirection = GUILayout.Toggle(follower.showDirection, "Show Direction Line");
            if (follower.showDirection)
            {
                if (GUILayout.Button($"Direction Color: #{follower.lineRenderers.ColorToHex(follower.directionColor)}"))
                {
                    showColorPicker = true;
                    editingPathColor = false;
                    originalColorWhenPickerOpened = follower.directionColor;
                }
                GUILayout.Label($"Direction Width: {follower.directionLineWidth:F2}", sliderLabelStyle);
                follower.directionLineWidth = GUILayout.HorizontalSlider(follower.directionLineWidth, 0.01f, 0.2f);
                follower.lineRenderers.directionLine.Renderer.startWidth = follower.directionLineWidth;
                follower.lineRenderers.directionLine.Renderer.endWidth = follower.directionLineWidth;
            }
            GUILayout.EndVertical();

            GUILayout.Label($"Line Transparency: {follower.lineAlpha:F2}", sliderLabelStyle);
            follower.lineAlpha = GUILayout.HorizontalSlider(follower.lineAlpha, 0f, 1f);
            follower.lineRenderers.lineAlpha = follower.lineAlpha;
            follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
            GUILayout.EndVertical();

            DrawColorPresets();
        }
        private void DrawColorPresets()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Color Presets", headerStyle);

            const int presetsPerRow = 5;
            for (int i = 0; i < colorPresets.Length; i += presetsPerRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = 0; j < presetsPerRow && i + j < colorPresets.Length; j++)
                {
                    Color preset = colorPresets[i + j];
                    GUI.backgroundColor = preset;
                    if (GUILayout.Button("", GUILayout.Width(30), GUILayout.Height(30)))
                    {
                        if (editingPathColor)
                        {
                            follower.pathColor = preset;
                            follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
                        }
                        else
                        {
                            follower.directionColor = preset;
                            follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
        private void DrawPlayerList()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Players", headerStyle);

            foreach (Player player in PhotonNetwork.PlayerList)
            {
                DrawPlayerEntry(player);
            }

            GUILayout.EndVertical();
        }
        private void DrawPlayerEntry(Player player)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            bool isCurrentlyFollowing = follower.currentPlayer == player && follower.followPlayerEnabled;
            GUI.backgroundColor = isCurrentlyFollowing ? Color.green : Color.white;

            GUILayout.Label(player.NickName, GUILayout.MinHeight(30f));

            string buttonText = isCurrentlyFollowing ? "Stop Following" : "Follow";
            if (GUILayout.Button(buttonText, GUILayout.Width(100)))
            {
                follower.ToggleFollowing(player);
            }

            if (isCurrentlyFollowing && GUILayout.Button("Clear Path", GUILayout.Width(80)))
            {
                follower.StopPathing();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }
        private void DrawPathDestinations()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Path Destinations", headerStyle);

            Transform GetLocalBody()
            {
                Transform body = Rig.Instance.body;
                return body;
            }
            void AddPointAndUpdate(Vector3 point)
            {
                follower.lineRenderers.pathPositions.Add(point);
                follower.lineRenderers.UpdatePathLineRenderer();
            }

            if (GUILayout.Button("Add Current Position Waypoint"))
            {
                Transform localBody = GetLocalBody();
                if (localBody == null) return;
                AddPointAndUpdate(localBody.position);
            }

            if (GUILayout.Button("Add Forward Waypoint"))
            {
                Transform localBody = GetLocalBody();
                if (localBody == null) return;

                Vector3 newWaypoint;
                int count = follower.lineRenderers.pathPositions.Count;
                if (count == 0)
                {
                    newWaypoint = localBody.position + localBody.forward * 2f;
                }
                else
                {
                    newWaypoint = follower.lineRenderers.pathPositions[count - 1] + localBody.forward * 2f;
                }
                AddPointAndUpdate(newWaypoint);
            }

            if (GUILayout.Button(follower.waitingForJumpStart ? "Set Jump Start" : "Set Jump End"))
            {
                Transform localBody = GetLocalBody();
                if (localBody == null) return;

                if (follower.waitingForJumpStart)
                {
                    follower.jumpWaypointStart = localBody.position;
                    follower.waitingForJumpStart = false;
                    follower.logger.LogInfo("Jump start set: " + follower.jumpWaypointStart);
                }
                else
                {
                    Vector3 jumpWaypointEnd = localBody.position;
                    follower.waitingForJumpStart = true;
                    follower.logger.LogInfo("Jump end set: " + jumpWaypointEnd);

                    float jumpAngle = follower.walkAnimator.jumpAngleDegrees;
                    List<Vector3> jumpArc = follower.GenerateJumpArc(follower.jumpWaypointStart, jumpWaypointEnd, jumpAngle);

                    follower.lineRenderers.pathPositions.AddRange(jumpArc);
                    follower.lineRenderers.UpdatePathLineRenderer();
                }
            }

            if (GUILayout.Button("Clear Jump Points", GUILayout.Width(120)))
            {
                follower.waitingForJumpStart = true;
                follower.jumpWaypointStart = Vector3.zero;
                follower.logger.LogInfo("Jump points cleared");
            }

            if (GUILayout.Button("Remove Last Waypoint"))
            {
                var positions = follower.lineRenderers.pathPositions;
                if (positions.Count > 0)
                {
                    positions.RemoveAt(positions.Count - 1);
                    follower.lineRenderers.UpdatePathLineRenderer();
                }
            }

            if (GUILayout.Button("Start Path Following"))
            {
                if (follower.lineRenderers.pathPositions.Count > 0)
                {
                    follower.StartPathing();
                }
            }

            if (GUILayout.Button("Stop Path Following"))
            {
                follower.StopPathing();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Open Preset Manager"))
            {
                showPresetManager = true;
            }

            GUILayout.EndVertical();
        }
        #region Presets
        private void DrawPresetManagerWindow(int windowID)
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Replays & Presets", headerStyle);

            replayPresetTabIndex = GUILayout.Toolbar(replayPresetTabIndex, replayPresetTabNames);
            switch (replayPresetTabIndex)
            {
                case 0:
                    DrawReplaysTab();
                    break;
                case 1:
                    DrawPresetsTab();
                    break;
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        private void DrawReplaysTab()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Replay Manager", headerStyle);

            GUILayout.Label("Load Saved Replays", headerStyle);
            string[] savedReplays = follower.movementRecorder.GetSavedReplays();
            if (savedReplays.Length == 0)
            {
                GUILayout.Label("No saved replays found.");
            }
            else
            {
                Vector2 replayScrollPos = Vector2.zero;
                replayScrollPos = GUILayout.BeginScrollView(replayScrollPos, GUILayout.Height(150));
                foreach (var replay in savedReplays)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Path.GetFileNameWithoutExtension(replay));
                    if (GUILayout.Button("Load", GUILayout.Width(60)))
                    {
                        follower.movementRecorder.LoadReplay(replay);
                        follower.movementRecorder.StartReplay();
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }
        private void DrawPresetsTab()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Preset Management", headerStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Preset Name:", GUILayout.Width(80));
            presetName = GUILayout.TextField(presetName);
            if (GUILayout.Button("Save Preset", GUILayout.Width(100)))
            {
                follower.SavePreset(presetName);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Refresh Presets"))
            {
                follower.RefreshPresetFiles();
            }

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginVertical(GUILayout.Width(180));
                GUILayout.Label("Available Presets:", headerStyle);
                presetScrollPosition = GUILayout.BeginScrollView(presetScrollPosition, GUILayout.Height(150));
                if (presetFiles != null && presetFiles.Length > 0)
                {
                    foreach (string presetFile in presetFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(presetFile);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(fileName);
                        if (GUILayout.Button("Load", GUILayout.Width(60)))
                        {
                            follower.LoadPreset(fileName);
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.Label("No presets found.");
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUILayout.Width(180));
                GUILayout.Label("Hardcoded Presets:", headerStyle);
                hardcodedPresetScrollPosition = GUILayout.BeginScrollView(hardcodedPresetScrollPosition, GUILayout.Height(150));
                if (HardcodedPresetNames != null)
                {
                    foreach (string hardcodedPreset in HardcodedPresetNames)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(hardcodedPreset);
                        if (GUILayout.Button("Load", GUILayout.Width(60)))
                        {
                            follower.LoadHardcodedPreset(hardcodedPreset);
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.Label("No hardcoded presets found.");
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("Code Generation", headerStyle);
            if (GUILayout.Button("Generate Hardcoded Preset Code"))
            {
                string generatedCode = follower.GenerateAllPresetsCode();
                if (!string.IsNullOrEmpty(generatedCode))
                {
                    GUIUtility.systemCopyBuffer = generatedCode;
                    follower.logger.LogInfo("Generated preset code and copied to clipboard!");
                    follower.logger.LogInfo("Also saved to GeneratedPresetCode.cs in the preset directory");
                }
                else
                {
                    follower.logger.LogError("Failed to generate preset code. Check the logs for details.");
                }
            }
            GUILayout.EndVertical();
        }
        #endregion
        private void DrawMisc()
        {
            #region Object Management
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Object Management", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Object Scanning", headerStyle);
            
            if (GUILayout.Button("Refresh Objects"))
            {
                follower.ScanActiveObjects();
            }

            GUILayout.BeginHorizontal();
            if (!follower.avoidObjects)
            {
                if (GUILayout.Button("Start Object Avoidance"))
                {
                    follower.ScanActiveObjects();
                    follower.avoidObjects = true;
                }
            }
            else
            {
                if (GUILayout.Button("Stop Object Avoidance"))
                {
                    follower.avoidObjects = false;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Active Objects: {follower.activeObjects.Count}");
            GUILayout.Label($"Blacklisted Objects: {follower.blacklistedObjects.Count}");
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            #endregion

            #region Collision Detection
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Collision Detection", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);
            string statusText = "System Status: ";
            statusText += follower.objectsInitialized ? "Initialized" : "Not Initialized";
            GUILayout.Label(statusText);

            if (!follower.objectsInitialized)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Missing Components:");
                if (Rig.Instance == null) GUILayout.Label("• Rig Instance");
                if (Rig.Instance != null && Rig.Instance.leftHand.gameObject == null) GUILayout.Label("• Left Hand");
                if (Rig.Instance != null && Rig.Instance.rightHand.gameObject == null) GUILayout.Label("• Right Hand");
                GUILayout.EndVertical();
            }

            GUILayout.Space(5);
            GUILayout.Label("Current Collisions:");
            if (follower.collisionState == "No Collisions")
            {
                GUILayout.Label("• No Collisions Detected");
            }
            else if (follower.collisionState == "Objects not initialized")
            {
                GUILayout.Label("• System Not Initialized");
            }
            else
            {
                string[] collisions = follower.collisionState.Split(',');
                foreach (string collision in collisions)
                {
                    GUILayout.Label($"• {collision.Trim()}");
                }
            }

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Initialize System", GUILayout.Height(30))) 
            { 
                follower.InitializeObjects(); 
            }
            if (GUILayout.Button("Force Collision Check", GUILayout.Height(30))) 
            { 
                follower.CheckCollisions(); 
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            #endregion

            #region Movement Recorder
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Movement Recorder", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);
            if (!follower.movementRecorder.isRecording && !follower.movementRecorder.isReplaying)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Start Recording"))
                {
                    follower.movementRecorder.StartRecording();
                }
                if (GUILayout.Button("Start Replay"))
                {
                    follower.movementRecorder.StartReplay();
                }
                GUILayout.EndHorizontal();
            }
            else if (follower.movementRecorder.isRecording)
            {
                GUILayout.Label("Recording in progress...");
                if (GUILayout.Button("Stop Recording"))
                {
                    follower.movementRecorder.StopRecording();
                }
            }
            else if (follower.movementRecorder.isReplaying)
            {
                GUILayout.Label("Replaying recording...");
                if (GUILayout.Button("Stop Replay"))
                {
                    follower.movementRecorder.StopReplay();
                }
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Open Preset Manager"))
            {
                showPresetManager = true;
            }
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            #endregion

            #region Gun
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Gun Settings", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);
            follower.gunEnabled = GUILayout.Toggle(follower.gunEnabled, "Enable Gun");

            if (follower.gunEnabled)
            {
                GUILayout.Label($"Gun Range: {follower.gunRange:F1}");
                follower.gunRange = GUILayout.HorizontalSlider(follower.gunRange, 10f, 200f);

                GUILayout.Space(5);
                GUILayout.Label("Shoot with:");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Mouse Click") || UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                {
                    follower.ShootGun();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            #endregion

            // todo: use the box to blacklist areas for the bot cant go
            #region Box Mode
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Box Mode", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);
            follower.boxShootingMode = GUILayout.Toggle(follower.boxShootingMode, "Enable Box Mode");
            if (follower.boxShootingMode)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Set Point 1"))
                {
                    follower.SetBoxPoint1();
                }
                if (GUILayout.Button("Set Point 2"))
                {
                    follower.SetBoxPoint2();
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(5);
                GUILayout.Label($"Box Display Duration: {follower.boxDisplayDuration:F1}s");
                follower.boxDisplayDuration = GUILayout.HorizontalSlider(follower.boxDisplayDuration, 0.5f, 30f);
            }
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            #endregion

            #region Flee
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Flee Settings", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Flee Radius: {follower.FLEE_RADIUS:F1}");
            follower.FLEE_RADIUS = GUILayout.HorizontalSlider(follower.FLEE_RADIUS, 0.1f, 30f);

            bool newFleeEnabled = GUILayout.Toggle(follower.fleeEnabled, "Flee from Taggers");
            if (newFleeEnabled != follower.fleeEnabled)
            {
                follower.fleeEnabled = newFleeEnabled;
                if (newFleeEnabled)
                {
                    follower.StartFleeing();
                    follower.followPlayerEnabled = false;
                    follower.followPathEnabled = false;
                    follower.isTagging = false;
                }
                else
                {
                    follower.StopFleeing();
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            #endregion

            #region Tag
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Tag Settings", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);
            if (!follower.isTagging)
            {
                if (GUILayout.Button("Start Tag"))
                {
                    follower.StartTagging();
                }
            }
            else
            {
                GUILayout.Label($"Tagging {follower.taggedPlayer?.NickName} for {follower.tagTimer:F1} seconds");
                if (GUILayout.Button("Stop Tag"))
                {
                    follower.StopTagging();
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            #endregion

            #region Text-to-Speech
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Text-to-Speech", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Enter text to speak:");
            ttsInputText = GUILayout.TextField(ttsInputText, GUILayout.Height(30));
            
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Speak Text", GUILayout.Height(35)))
            {
                if (!string.IsNullOrWhiteSpace(ttsInputText))
                {
                    _ = SpeakTextAsync(ttsInputText);
                }
            }
            
            if (GUILayout.Button("Stop Speaking", GUILayout.Height(35)))
            {
                StopCurrentTTS();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("Quick Phrases:", headerStyle);
            
            string[] quickPhrases = { "Hello everyone!", "Follow me!", "Good game!", "Let's go!", "Nice job!" };
            for (int i = 0; i < quickPhrases.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(quickPhrases[i]))
                {
                    _ = SpeakTextAsync(quickPhrases[i]);
                }
                if (i + 1 < quickPhrases.Length && GUILayout.Button(quickPhrases[i + 1]))
                {
                    _ = SpeakTextAsync(quickPhrases[i + 1]);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            if (isSpeaking)
            {
                GUILayout.Label("🔊 Currently speaking...", labelStyle);
            }
            else
            {
                GUILayout.Label("🔇 Ready to speak", labelStyle);
            }
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            #endregion

            #region Debug Tools
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Debug Tools", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);
            if (GUILayout.Button("Send Logs"))
            {
                string logs = Logging.GetFullLogText();
                GameObject senderObj = new GameObject("WebhookSender");
                WebhookSender sender = senderObj.AddComponent<WebhookSender>();
                sender.Initialize(follower.DiscordWebhookUrl.Value, logs);
            }

            if (GUILayout.Button("Grab All Player IDs"))
            {
                GrabAllIDS();
            }

            GUILayout.Space(10);
            GUILayout.Label("Movement Prediction", headerStyle);
            follower.isPredictionEnabled = GUILayout.Toggle(follower.isPredictionEnabled, "Enable Prediction");

            GUILayout.EndVertical();
            GUILayout.EndVertical();
            #endregion
        }
        public static void GrabAllIDS()
        {
            string text = "=======================PLAYER INFO!=========================";

            foreach (Photon.Realtime.Player players in PhotonNetwork.PlayerList)
            {
                string playerName = players.NickName;
                string playerID = players.UserId;

                text += $"\nName: {playerName}, ID: {playerID}\n\n";
            }

            text += "\n==========================================================\n";

            File.AppendAllText("a.txt", text);
        }
        private void DrawLogsTab()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Logs", headerStyle);

            logScrollPosition = GUILayout.BeginScrollView(logScrollPosition, GUILayout.Height(350));
            foreach (string log in logMessages)
            {
                GUILayout.Label(log);
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }
        private void DrawColorPicker(int windowID)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            Color currentColor = editingPathColor ? follower.pathColor : follower.directionColor;
            float red = currentColor.r;
            float green = currentColor.g;
            float blue = currentColor.b;

            GUILayout.Label($"Red: {red:F2}", sliderLabelStyle);
            red = GUILayout.HorizontalSlider(red, 0f, 1f);
            GUILayout.Label($"Green: {green:F2}", sliderLabelStyle);
            green = GUILayout.HorizontalSlider(green, 0f, 1f);
            GUILayout.Label($"Blue: {blue:F2}", sliderLabelStyle);
            blue = GUILayout.HorizontalSlider(blue, 0f, 1f);

            Color newColor = new Color(red, green, blue, currentColor.a);
            if (editingPathColor)
            {
                follower.pathColor = newColor;
                follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
            }
            else
            {
                follower.directionColor = newColor;
                follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
            }

            Rect previewRect = GUILayoutUtility.GetRect(50, 50);
            GUI.backgroundColor = newColor;
            GUI.Box(previewRect, "");
            GUI.backgroundColor = Color.white;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close")) showColorPicker = false;
            if (GUILayout.Button("Cancel"))
            {
                if (editingPathColor) follower.pathColor = originalColorWhenPickerOpened;
                else follower.directionColor = originalColorWhenPickerOpened;
                follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
                showColorPicker = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        #region TTS Methods
        private void EnsureTTSManagerExists()
        {
            if (WalkSimulator.Bot.TTS.SpeechToTextManager.instance == null)
            {
                GameObject ttsManagerObj = new GameObject("SpeechToTextManager");
                ttsManagerObj.AddComponent<WalkSimulator.Bot.TTS.SpeechToTextManager>();
                UnityEngine.Object.DontDestroyOnLoad(ttsManagerObj);
            }
        }
        private async System.Threading.Tasks.Task SpeakTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || isSpeaking) return;

            try
            {
                isSpeaking = true;
                
                EnsureTTSManagerExists();
                
                var clip = await WalkSimulator.Bot.TTS.SpeechToTextManager.instance.TextToSpeech(text);
                
                if (clip != null)
                {
                    currentTTSClip = clip;
                    WalkSimulator.Bot.TTS.SpeechToTextManager.instance.PlayTTSAudio(clip);                
                    await System.Threading.Tasks.Task.Delay((int)(clip.length * 1000));
                }
                else
                {
                    AddLogMessage("TTS: Failed to generate audio clip");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"TTS Error: {e.Message}");
                AddLogMessage($"TTS Error: {e.Message}");
            }
            finally
            {
                isSpeaking = false;
                currentTTSClip = null;
            }
        }
        private void StopCurrentTTS() // works?
        {
            if (isSpeaking && GorillaTagger.Instance?.myRecorder != null)
            {
                GorillaTagger.Instance.myRecorder.StopRecording();
                isSpeaking = false;
                currentTTSClip = null;
            }
        }
        #endregion
        #endregion
    }
}
