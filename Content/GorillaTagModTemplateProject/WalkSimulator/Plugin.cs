<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using WalkSimulator.Bot;
using GorillaNetworking;
using HarmonyLib;
using UnityEngine;
using WalkSimulator.Animators;
using WalkSimulator.Menus;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator
{
    [BepInPlugin("com.kylethescientist.gorillatag.walksimulator", "WalkSimulator", "2.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static bool IsSteam { get; protected set; }

        public bool Enabled
        {
            get
            {
                return true;
            }
            protected set
            {
                Plugin._enabled = true;
                base.gameObject.GetOrAddComponent<InputHandler>();
                base.gameObject.GetOrAddComponent<Rig>();
                walkAnimator = base.gameObject.GetOrAddComponent<WalkAnimator>();
                flyAnimator = base.gameObject.GetOrAddComponent<FlyAnimator>();
                handAnimator = base.gameObject.GetOrAddComponent<PoseAnimator>();
                playerFollower = base.gameObject.GetOrAddComponent<PlayerFollower>();
                playerFollower.DiscordWebhookUrl = Config.Bind("Logging", "DiscordWebhookUrl", "", "Discord webhook URL for sending logs");
                if (!radialMenu)
                {
                    radialMenu = UnityEngine.Object.Instantiate<GameObject>(bundle.LoadAsset<GameObject>("Radial Menu")).AddComponent<RadialMenu>();
                }
                walkAnimator.enabled = false;
                flyAnimator.enabled = false;
                handAnimator.enabled = false;
            }
        }
        private void Awake()
        {
            Plugin.Instance = this;
            Logging.Init();
            try
            {
                string text = Paths.ConfigPath + "/BepInEx.cfg";
                string text2 = File.ReadAllText(text);
                text2 = Regex.Replace(text2, "HideManagerGameObject = .+", "HideManagerGameObject = true");
                File.WriteAllText(text, text2);
            }
            catch (Exception ex)
            {
                Logging.Exception(ex);
            }
            bundle = WalkSimulator.Tools.AssetUtils.LoadAssetBundle("WalkSimulator/assetbundle");
        }
        private void Start()
        {
            Enabled = true;
        }

        private void OnEnable()
        {
            HarmonyPatches.ApplyHarmonyPatches();
        }
        private void OnDisable()
        {
            HarmonyPatches.RemoveHarmonyPatches();
        }
        private void OnGameInitialized(object sender, EventArgs e)
        {
            Plugin.IsSteam = ((string)Traverse.Create(PlayFabAuthenticator.instance).Field("platform").GetValue()).ToLower().Contains("steam");
            Enabled = true;
        }
        private void Update()
        {
            foreach (KeyBinding binding in keyBindings)
            {
                binding.CheckInput();
            }
        }
        private void FixedUpdate()
        {
            TryInitializeGamemode();
        }
        public void TryInitializeGamemode()
        {
        }
        private void OnGUI()
        {
            float num = 400f;
            float num2 = 40f;
            float num3 = (float)Screen.width - num - 10f;
            float num4 = 10f;
            GUI.skin.label.fontSize = 20;
            foreach (string text in new List<string>(labels.Keys))
            {
                num4 += num2;
                GUI.Label(new Rect(num3, num4, num, num2), text + ": " + labels[text]);
            }
            foreach (string text2 in new List<string>(sliders.Keys))
            {
                num4 += num2;
                sliders[text2] = GUI.HorizontalSlider(new Rect(num3, num4, num, num2), sliders[text2], 0f, 10f);
                GUI.Label(new Rect(num3 - num, num4, num, num2), text2 + ": " + sliders[text2].ToString());
            }
        }

        private class KeyBinding
        {
            private KeyCode key;
            private Action action;
            private bool wasPressed = false;

            public KeyBinding(KeyCode key, Action action)
            {
                this.key = key;
                this.action = action;
            }

            public void CheckInput()
            {
                if (Input.GetKey(key))
                {
                    if (!wasPressed)
                    {
                        wasPressed = true;
                        action?.Invoke();
                    }
                }
                else
                {
                    wasPressed = false;
                }
            }
        }

        public static Plugin Instance;
        private static bool _enabled = true;
        public AnimatorBase walkAnimator;
        public AnimatorBase flyAnimator;
        public AnimatorBase handAnimator;
        public PlayerFollower playerFollower;
        public AssetBundle bundle;
        public RadialMenu radialMenu;
        private bool setGamemode;
        public Dictionary<string, float> sliders = new Dictionary<string, float>();
        public Dictionary<string, string> labels = new Dictionary<string, string>();
        private List<KeyBinding> keyBindings = new List<KeyBinding>();
    }
=======
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using WalkSimulator.Bot;
using GorillaNetworking;
using HarmonyLib;
using UnityEngine;
using WalkSimulator.Animators;
using WalkSimulator.Menus;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator
{
    [BepInPlugin("com.kylethescientist.gorillatag.walksimulator", "WalkSimulator", "2.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static bool IsSteam { get; protected set; }

        public bool Enabled
        {
            get
            {
                return true;
            }
            protected set
            {
                Plugin._enabled = true;
                base.gameObject.GetOrAddComponent<InputHandler>();
                base.gameObject.GetOrAddComponent<Rig>();
                walkAnimator = base.gameObject.GetOrAddComponent<WalkAnimator>();
                flyAnimator = base.gameObject.GetOrAddComponent<FlyAnimator>();
                handAnimator = base.gameObject.GetOrAddComponent<PoseAnimator>();
                playerFollower = base.gameObject.GetOrAddComponent<PlayerFollower>();
                playerFollower.DiscordWebhookUrl = Config.Bind("Logging", "DiscordWebhookUrl", "", "Discord webhook URL for sending logs");
                if (!radialMenu)
                {
                    radialMenu = UnityEngine.Object.Instantiate<GameObject>(bundle.LoadAsset<GameObject>("Radial Menu")).AddComponent<RadialMenu>();
                }
                walkAnimator.enabled = false;
                flyAnimator.enabled = false;
                handAnimator.enabled = false;
            }
        }
        private void Awake()
        {
            Plugin.Instance = this;
            Logging.Init();
            try
            {
                string text = Paths.ConfigPath + "/BepInEx.cfg";
                string text2 = File.ReadAllText(text);
                text2 = Regex.Replace(text2, "HideManagerGameObject = .+", "HideManagerGameObject = true");
                File.WriteAllText(text, text2);
            }
            catch (Exception ex)
            {
                Logging.Exception(ex);
            }
            bundle = WalkSimulator.Tools.AssetUtils.LoadAssetBundle("WalkSimulator/assetbundle");
        }
        private void Start()
        {
            Enabled = true;
        }

        private void OnEnable()
        {
            HarmonyPatches.ApplyHarmonyPatches();
        }
        private void OnDisable()
        {
            HarmonyPatches.RemoveHarmonyPatches();
        }
        private void OnGameInitialized(object sender, EventArgs e)
        {
            Plugin.IsSteam = ((string)Traverse.Create(PlayFabAuthenticator.instance).Field("platform").GetValue()).ToLower().Contains("steam");
            Enabled = true;
        }
        private void Update()
        {
            foreach (KeyBinding binding in keyBindings)
            {
                binding.CheckInput();
            }
        }
        private void FixedUpdate()
        {
            TryInitializeGamemode();
        }
        public void TryInitializeGamemode()
        {
        }
        private void OnGUI()
        {
            float num = 400f;
            float num2 = 40f;
            float num3 = (float)Screen.width - num - 10f;
            float num4 = 10f;
            GUI.skin.label.fontSize = 20;
            foreach (string text in new List<string>(labels.Keys))
            {
                num4 += num2;
                GUI.Label(new Rect(num3, num4, num, num2), text + ": " + labels[text]);
            }
            foreach (string text2 in new List<string>(sliders.Keys))
            {
                num4 += num2;
                sliders[text2] = GUI.HorizontalSlider(new Rect(num3, num4, num, num2), sliders[text2], 0f, 10f);
                GUI.Label(new Rect(num3 - num, num4, num, num2), text2 + ": " + sliders[text2].ToString());
            }
        }

        private class KeyBinding
        {
            private KeyCode key;
            private Action action;
            private bool wasPressed = false;

            public KeyBinding(KeyCode key, Action action)
            {
                this.key = key;
                this.action = action;
            }

            public void CheckInput()
            {
                if (Input.GetKey(key))
                {
                    if (!wasPressed)
                    {
                        wasPressed = true;
                        action?.Invoke();
                    }
                }
                else
                {
                    wasPressed = false;
                }
            }
        }

        public static Plugin Instance;
        private static bool _enabled = true;
        public AnimatorBase walkAnimator;
        public AnimatorBase flyAnimator;
        public AnimatorBase handAnimator;
        public PlayerFollower playerFollower;
        public AssetBundle bundle;
        public RadialMenu radialMenu;
        private bool setGamemode;
        public Dictionary<string, float> sliders = new Dictionary<string, float>();
        public Dictionary<string, string> labels = new Dictionary<string, string>();
        private List<KeyBinding> keyBindings = new List<KeyBinding>();
    }
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
}