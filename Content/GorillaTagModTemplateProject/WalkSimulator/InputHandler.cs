using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine;
using WalkSimulator.Animators;
using WalkSimulator.Menus;
using WalkSimulator.Rigging;
using Valve.VR;

namespace WalkSimulator
{
    public class InputHandler : MonoBehaviour
    {
        public static InputHandler Instance { get; private set; }

        public static Vector3 inputDirection { get; private set; }
        public static Vector3 inputDirectionNoY;
        public static string deviceName { get; private set; } = "";
        public static string devicePrefix { get; private set; } = "";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ValidateDevices();
        }

        private void Update()
        {
            if (Plugin.Instance.Enabled)
            {
                GetInputDirection();

                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    HeadDriver.Instance.LockCursor = !HeadDriver.Instance.LockCursor;
                }

                if (Keyboard.current.cKey.wasPressedThisFrame)
                {
                    HeadDriver.Instance.ToggleCam();
                }

                Plugin.Instance.radialMenu.enabled = Keyboard.current.tabKey.isPressed;
                Plugin.Instance.radialMenu.gameObject.SetActive(Keyboard.current.tabKey.isPressed);
            }
        }

        private void GetInputDirection()
        {
            Vector3 keyboardInput = KeyboardInput();

            if (keyboardInput.magnitude > 0f)
            {
                inputDirection = keyboardInput.normalized;
                inputDirectionNoY = keyboardInput;
                inputDirectionNoY.y = 0f;
                inputDirectionNoY.Normalize();
                return;
            }

            float horizontalAxis;
            float verticalAxis;
            float depthAxis;

            if (Plugin.IsSteam)
            {
                horizontalAxis = SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.axis.x;
                verticalAxis = SteamVR_Actions.gorillaTag_RightJoystick2DAxis.axis.y;
                depthAxis = SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.axis.y;
            }
            else
            {
                UnityEngine.XR.InputDevice leftControllerDevice = ControllerInputPoller.instance.leftControllerDevice;
                UnityEngine.XR.InputDevice rightControllerDevice = ControllerInputPoller.instance.rightControllerDevice;

                Vector2 leftAxis;
                leftControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out leftAxis);

                Vector2 rightAxis;
                rightControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out rightAxis);

                horizontalAxis = leftAxis.x;
                verticalAxis = rightAxis.y;
                depthAxis = leftAxis.y;
            }

            inputDirection = new Vector3(horizontalAxis, verticalAxis, depthAxis).normalized;
            inputDirectionNoY = new Vector3(horizontalAxis, 0f, depthAxis).normalized;
        }

        private Vector3 KeyboardInput()
        {
            float horizontalAxis = 0f;
            float verticalAxis = 0f;
            float depthAxis = 0f;

            if (Keyboard.current.aKey.isPressed)
            {
                horizontalAxis -= 1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                horizontalAxis += 1f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                verticalAxis -= 1f;
            }

            if (Keyboard.current.wKey.isPressed)
            {
                verticalAxis += 1f;
            }

            if (Keyboard.current.ctrlKey.isPressed)
            {
                depthAxis -= 1f;
            }

            if (Keyboard.current.spaceKey.isPressed)
            {
                depthAxis += 1f;
            }

            return new Vector3(horizontalAxis, depthAxis, verticalAxis);
        }

        private void ValidateDevices() { }
    }
}
