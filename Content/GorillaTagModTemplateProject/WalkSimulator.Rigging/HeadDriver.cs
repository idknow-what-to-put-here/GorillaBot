<<<<<<< HEAD
﻿using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WalkSimulator.Rigging
{
    public class HeadDriver : MonoBehaviour
    {
        public static HeadDriver Instance { get; private set; }

        public bool LockCursor
        {
            get { return _lockCursor; }
            set
            {
                _lockCursor = value;
                Cursor.lockState = _lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !_lockCursor;
            }
        }

        public Transform thirpyTarget;
        public Transform head;
        public GameObject cameraObject;
        public GameObject cameraTransform;
        public bool turn = true;
        public bool FirstPerson
        {
            get { return overrideCam.enabled; }
            set { overrideCam.enabled = value; }
        }

        private Vector3 offset = Vector3.zero;
        private Camera overrideCam;
        private bool _lockCursor;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // Handle duplicate instances
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Cinemachine3rdPersonFollow cinemachine3rdPersonFollow =
                FindObjectOfType<Cinemachine3rdPersonFollow>();

            if (cinemachine3rdPersonFollow == null)
            {
                Debug.LogError("Cinemachine3rdPersonFollow not found.");
                enabled = false;
                return;
            }

            thirpyTarget = cinemachine3rdPersonFollow.VirtualCamera.Follow;

            Camera mainCamera = cinemachine3rdPersonFollow.GetComponentInParent<Camera>();
            if (mainCamera == null)
            {
                Debug.LogError("Main camera not found.");
                enabled = false;
                return;
            }

            cameraObject = new GameObject("WalkSim First Person Camera");
            overrideCam = cameraObject.AddComponent<Camera>();
            overrideCam.fieldOfView = 90f;
            overrideCam.nearClipPlane = mainCamera.nearClipPlane;
            overrideCam.farClipPlane = mainCamera.farClipPlane;
            overrideCam.targetDisplay = mainCamera.targetDisplay;
            overrideCam.cullingMask = mainCamera.cullingMask;
            overrideCam.depth = mainCamera.depth + 1f;
            overrideCam.enabled = false;
        }

        private void LateUpdate()
        {
            if (GorillaLocomotion.GTPlayer.Instance == null ||
                GorillaLocomotion.GTPlayer.Instance.headCollider == null || head == null)
            {
                return; // Exit if required components are missing
            }

            cameraObject.transform.position =
                GorillaLocomotion.GTPlayer.Instance.headCollider.transform.TransformPoint(
                    offset);
            cameraObject.transform.forward = head.forward;

            if (turn)
            {
                float mouseXDelta = Mouse.current.delta.value.x / 10f;
                float mouseYDelta = Mouse.current.delta.value.y / 10f;

                GorillaLocomotion.GTPlayer.Instance.Turn(mouseXDelta);

                Vector3 eulerAngles = GorillaTagger.Instance.offlineVRRig.headConstraint.eulerAngles;
                eulerAngles.x -= mouseYDelta;

                // Normalize the angle to be between -180 and 180
                if (eulerAngles.x > 180f)
                {
                    eulerAngles.x -= 360f;
                }

                eulerAngles.x = Mathf.Clamp(eulerAngles.x, -60f, 60f);
                GorillaTagger.Instance.offlineVRRig.headConstraint.eulerAngles =
                    eulerAngles;

                eulerAngles.y += 90f;
                thirpyTarget.localEulerAngles = new Vector3(eulerAngles.x, 0f, 0f);
                GorillaLocomotion.GTPlayer.Instance.headCollider.transform.localEulerAngles =
                    new Vector3(eulerAngles.x, 0f, 0f);
            }
        }

        private void OverrideHeadMovement()
        {
            Logging.Debug("Overriding head movement");
            head = GorillaTagger.Instance.offlineVRRig.head.rigTarget;
        }

        internal void ToggleCam()
        {
            FirstPerson = !FirstPerson; // Use the property
        }

        private void OnEnable()
        {
            Logging.Debug("Enabled");
            if (Rig.Instance?.Animator == null)
            {
                Debug.LogWarning("Animator is null, HeadDriver may not function correctly.");
                return;
            }
            LockCursor = true;
            OverrideHeadMovement();
        }

        private void OnDisable()
        {
            if (head == null)
            {
                return;
            }
            LockCursor = false;
            GorillaTagger.Instance.offlineVRRig.head.rigTarget = head;
        }
    }
}
=======
﻿using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WalkSimulator.Rigging
{
    public class HeadDriver : MonoBehaviour
    {
        public static HeadDriver Instance { get; private set; }

        public bool LockCursor
        {
            get { return _lockCursor; }
            set
            {
                _lockCursor = value;
                Cursor.lockState = _lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !_lockCursor;
            }
        }

        public Transform thirpyTarget;
        public Transform head;
        public GameObject cameraObject;
        public GameObject cameraTransform;
        public bool turn = true;
        public bool FirstPerson
        {
            get { return overrideCam.enabled; }
            set { overrideCam.enabled = value; }
        }

        private Vector3 offset = Vector3.zero;
        private Camera overrideCam;
        private bool _lockCursor;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // Handle duplicate instances
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Cinemachine3rdPersonFollow cinemachine3rdPersonFollow =
                FindObjectOfType<Cinemachine3rdPersonFollow>();

            if (cinemachine3rdPersonFollow == null)
            {
                Debug.LogError("Cinemachine3rdPersonFollow not found.");
                enabled = false;
                return;
            }

            thirpyTarget = cinemachine3rdPersonFollow.VirtualCamera.Follow;

            Camera mainCamera = cinemachine3rdPersonFollow.GetComponentInParent<Camera>();
            if (mainCamera == null)
            {
                Debug.LogError("Main camera not found.");
                enabled = false;
                return;
            }

            cameraObject = new GameObject("WalkSim First Person Camera");
            overrideCam = cameraObject.AddComponent<Camera>();
            overrideCam.fieldOfView = 90f;
            overrideCam.nearClipPlane = mainCamera.nearClipPlane;
            overrideCam.farClipPlane = mainCamera.farClipPlane;
            overrideCam.targetDisplay = mainCamera.targetDisplay;
            overrideCam.cullingMask = mainCamera.cullingMask;
            overrideCam.depth = mainCamera.depth + 1f;
            overrideCam.enabled = false;
        }

        private void LateUpdate()
        {
            if (GorillaLocomotion.GTPlayer.Instance == null ||
                GorillaLocomotion.GTPlayer.Instance.headCollider == null || head == null)
            {
                return; // Exit if required components are missing
            }

            cameraObject.transform.position =
                GorillaLocomotion.GTPlayer.Instance.headCollider.transform.TransformPoint(
                    offset);
            cameraObject.transform.forward = head.forward;

            if (turn)
            {
                float mouseXDelta = Mouse.current.delta.value.x / 10f;
                float mouseYDelta = Mouse.current.delta.value.y / 10f;

                GorillaLocomotion.GTPlayer.Instance.Turn(mouseXDelta);

                Vector3 eulerAngles = GorillaTagger.Instance.offlineVRRig.headConstraint.eulerAngles;
                eulerAngles.x -= mouseYDelta;

                // Normalize the angle to be between -180 and 180
                if (eulerAngles.x > 180f)
                {
                    eulerAngles.x -= 360f;
                }

                eulerAngles.x = Mathf.Clamp(eulerAngles.x, -60f, 60f);
                GorillaTagger.Instance.offlineVRRig.headConstraint.eulerAngles =
                    eulerAngles;

                eulerAngles.y += 90f;
                thirpyTarget.localEulerAngles = new Vector3(eulerAngles.x, 0f, 0f);
                GorillaLocomotion.GTPlayer.Instance.headCollider.transform.localEulerAngles =
                    new Vector3(eulerAngles.x, 0f, 0f);
            }
        }

        private void OverrideHeadMovement()
        {
            Logging.Debug("Overriding head movement");
            head = GorillaTagger.Instance.offlineVRRig.head.rigTarget;
        }

        internal void ToggleCam()
        {
            FirstPerson = !FirstPerson; // Use the property
        }

        private void OnEnable()
        {
            Logging.Debug("Enabled");
            if (Rig.Instance?.Animator == null)
            {
                Debug.LogWarning("Animator is null, HeadDriver may not function correctly.");
                return;
            }
            LockCursor = true;
            OverrideHeadMovement();
        }

        private void OnDisable()
        {
            if (head == null)
            {
                return;
            }
            LockCursor = false;
            GorillaTagger.Instance.offlineVRRig.head.rigTarget = head;
        }
    }
}
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
