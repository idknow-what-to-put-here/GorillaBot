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
        private readonly PlayerFollower follower;

        // GUI Configuration
        private const int WINDOW_WIDTH = 350;
        private const int WINDOW_HEIGHT = 450;
        private const int COLOR_PICKER_SIZE = 200;
        private Rect windowRect = new Rect(20, 20, WINDOW_WIDTH, WINDOW_HEIGHT);
        private bool showColorPicker = false;
        private bool editingPathColor = true;
        private Vector2 scrollPosition;
        private bool showPresetManager = false;

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
        private readonly string[] tabNames = new string[] { "Line Settings", "Players", "Path Destinations", "Misc", "Logs" };

        // Preset management
        private string presetName = "DefaultPreset";
        public string[] presetFiles;
        private Vector2 presetScrollPosition;
        private Rect presetWindowRect = new Rect(100, 100, 400, 500);
        private Vector2 hardcodedPresetScrollPosition = Vector2.zero;
        public Dictionary<string, PathPreset> hardcodedPresets;
        public IEnumerable<string> HardcodedPresetNames => hardcodedPresets.Keys;

        // Replays & Presets sub-tabs.
        private int replayPresetTabIndex = 0;
        private readonly string[] replayPresetTabNames = new string[] { "Replays", "Presets" };
        private string saveReplayFilename = "";

        // Loging
        public static List<string> logMessages = new List<string>();
        private Vector2 logScrollPosition;

        // Styling
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle sliderLabelStyle;
        private Color originalColorWhenPickerOpened;

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
        private bool isLeftHanded = true;

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

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            sectionStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };

            sliderLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };

            windowRect = GUILayout.Window(0, windowRect, DrawWindow, "Player Follower", GUI.skin.window);

            if (showColorPicker)
            {
                Rect colorPickerRect = new Rect(
                    windowRect.x + windowRect.width + 10,
                    windowRect.y,
                    COLOR_PICKER_SIZE,
                    COLOR_PICKER_SIZE
                );
                GUILayout.Window(1, colorPickerRect, DrawColorPicker, "Color Picker");
            }

            if (showPresetManager)
            {
                presetWindowRect = GUILayout.Window(2, presetWindowRect, DrawPresetManagerWindow, "Preset Manager", GUI.skin.window);
            }
        }
        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
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
            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        private void DrawLineSettings()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Line Settings", headerStyle);

            // Path Line Settings
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

            // Direction Line Settings
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

            // Global Line Settings
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
            #region Misc
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Misc", headerStyle);
            if (GUILayout.Button("Refresh Objects"))
            {
                follower.ScanActiveObjects();
            }
            if (!follower.avoidObjects)
            {
                if (GUILayout.Button("Start Avoid Objects"))
                {
                    follower.ScanActiveObjects();
                    follower.avoidObjects = true;
                }
            }
            else
            {
                if (GUILayout.Button("Stop Avoid Objects"))
                {
                    follower.avoidObjects = false;
                }
            }

            if (GUILayout.Button("Send Logs"))
            {
                string logs = Logging.GetFullLogText();
                //if (!string.IsNullOrEmpty(logs))
                {
                    GameObject senderObj = new GameObject("WebhookSender");
                    WebhookSender sender = senderObj.AddComponent<WebhookSender>();
                    sender.Initialize(follower.DiscordWebhookUrl.Value, logs);
                }
            }

            if (GUILayout.Button("GrabAllIDS"))
            {
                GrabAllIDS();
            }
            GUILayout.EndVertical();
            #endregion
            #region Collisions
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Collision Detection", headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);

            string statusText = "System Status: ";
            if (follower.objectsInitialized) { statusText += "Initialized"; }
            else { statusText += "Not Fully Initialized"; }
            GUILayout.Label(statusText);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Tracking {follower.activeObjects.Count} objects", GUILayout.Width(200));

            GUILayout.EndHorizontal();

            if (!follower.objectsInitialized)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Missing Components:");
                if (Rig.Instance == null) GUILayout.Label("Rig Instance");
                if (Rig.Instance != null && Rig.Instance.leftHand.gameObject == null) GUILayout.Label("Left Hand");
                if (Rig.Instance != null && Rig.Instance.rightHand.gameObject == null) GUILayout.Label("Right Hand");
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Current Collision Status:");

            if (follower.collisionState == "No Collisions")
            {
                GUILayout.Label("No Collisions Detected", GUILayout.Height(25));
            }
            else if (follower.collisionState == "Objects not initialized")
            {
                GUILayout.Label("System Not Initialized", GUILayout.Height(25));
            }
            else
            {
                string[] collisions = follower.collisionState.Split(',');
                GUILayout.Label("Collisions Detected:");

                GUILayout.BeginVertical(GUI.skin.box);
                foreach (string collision in collisions)
                {
                    GUILayout.Label("* " + collision.Trim());
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Initialize System", GUILayout.Height(30))) { follower.InitializeObjects(); }
            if (GUILayout.Button("Force Collision Check", GUILayout.Height(30))) { follower.CheckCollisions(); }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            #endregion
            #region Movement Recorder
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Movement Recorder", headerStyle);

            if (!follower.movementRecorder.isRecording && !follower.movementRecorder.isReplaying)
            {
                if (GUILayout.Button("Start Recording"))
                {
                    follower.movementRecorder.StartRecording();
                }
                if (GUILayout.Button("Start Replay"))
                {
                    follower.movementRecorder.StartReplay();
                }
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
            if (GUILayout.Button("Open Preset Manager"))
            {
                showPresetManager = true;
            }
            GUILayout.EndVertical();
            #endregion
            #region Flee
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Flee Settings", headerStyle);

            GUILayout.Label($"Flee Radius : {follower.FLEE_RADIUS:F1}");
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
            #endregion
            #region Hand
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Hand Settings", headerStyle);

            GUILayout.Label("Activation Point:");
            GUILayout.BeginHorizontal();
            foreach (ActivationPoint point in Enum.GetValues(typeof(ActivationPoint)))
            {
                if (GUILayout.Toggle(follower.activationPoint == point, point.ToString()))
                {
                    follower.activationPoint = point;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Hand Down Duration: {follower.handDownDuration:F1}s");
            follower.handDownDuration = GUILayout.HorizontalSlider(follower.handDownDuration, 0.1f, 2f);

            GUILayout.Label($"Button Hold Duration: {follower.buttonHoldDuration:F1}s");
            follower.buttonHoldDuration = GUILayout.HorizontalSlider(follower.buttonHoldDuration, 0.1f, 3f);

            GUILayout.Label($"Hand Up Duration: {follower.handUpDuration:F1}s");
            follower.handUpDuration = GUILayout.HorizontalSlider(follower.handUpDuration, 0.1f, 2f);

            GUILayout.Label("Activation Button(s):");
            GUILayout.BeginHorizontal();
            foreach (HandButton button in Enum.GetValues(typeof(HandButton)))
            {
                bool active = (follower.selectedHandButton & button) != 0;
                bool newActive = GUILayout.Toggle(active, button.ToString());
                if (newActive != active)
                {
                    if (newActive) { follower.selectedHandButton |= button; }
                    else { follower.selectedHandButton &= ~button; }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Select Hand:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(isLeftHanded, "Left-Handed"))
            {
                isLeftHanded = true;
            }
            if (GUILayout.Toggle(!isLeftHanded, "Right-Handed"))
            {
                isLeftHanded = false;
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Perform Hand"))
            {
                follower.PerformHand(isLeftHanded);
            }

            GUILayout.EndVertical();
            #endregion
            #region Tag
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Tag Settings", headerStyle);

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
    }
}
