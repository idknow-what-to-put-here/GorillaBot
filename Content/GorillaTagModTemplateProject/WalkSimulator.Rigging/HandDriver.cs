using System;
using System.Collections;
using GorillaLocomotion;
using UnityEngine;
using WalkSimulator.Animators;
using WalkSimulator.Patches;

namespace WalkSimulator.Rigging
{
    public class HandDriver : MonoBehaviour
    {
        public bool grip;
        public bool trigger;
        public bool primary;
        public bool secondary;
        public bool isLeft { get; private set; } // Make settable only internally
        public bool grounded;
        public bool hideControllerTransform = true;
        public Vector3 targetPosition;
        public Vector3 lookAt;
        public Vector3 up;
        public float followRate = 0.1f;
        public Vector3 hit;
        public Vector3 lastSnap;
        public Vector3 normal;

        private VRMap handMap;
        private Vector3 defaultOffset;
        private Transform head;
        private Transform body;
        private Transform controller;

        public Vector3 DefaultPosition
        {
            get { return body.TransformPoint(defaultOffset); }
        }

        public void Init(bool isLeft)
        {
            this.isLeft = isLeft; // Use 'this' for clarity
            defaultOffset = new Vector3(isLeft ? (-0.25f) : 0.25f, -0.45f, 0.2f);
            head = Rig.Instance.head;
            body = Rig.Instance.body;

            if (GorillaLocomotion.Player.Instance == null)
            {
                Debug.LogError("GorillaLocomotion.Player.Instance is null in HandDriver.Init");
                enabled = false; // Disable if dependencies are missing
                return;
            }

            controller = isLeft
                             ? GorillaLocomotion.Player.Instance.leftControllerTransform
                             : GorillaLocomotion.Player.Instance.rightControllerTransform;

            if (GorillaTagger.Instance == null || GorillaTagger.Instance.offlineVRRig == null)
            {
                Debug.LogError("GorillaTagger.Instance or offlineVRRig is null in HandDriver.Init");
                enabled = false;
                return;
            }

            handMap = isLeft ? GorillaTagger.Instance.offlineVRRig.leftHand
                             : GorillaTagger.Instance.offlineVRRig.rightHand;

            base.transform.position = DefaultPosition;
            targetPosition = DefaultPosition;
            lastSnap = DefaultPosition;
            hit = DefaultPosition;
            up = Vector3.up;
        }

        private void FixedUpdate()
        {
            base.transform.position =
                Vector3.Lerp(base.transform.position, targetPosition, followRate);
            base.transform.LookAt(lookAt, up);

            controller.position = hideControllerTransform ? body.position
                                                          : base.transform.position;

            // Use a local variable to avoid repeated property access
            bool isLeftHand = isLeft;
            FingerPatch.forceLeftGrip = isLeftHand ? grip : FingerPatch.forceLeftGrip;
            FingerPatch.forceLeftPrimary = isLeftHand ? primary : FingerPatch.forceLeftPrimary;
            FingerPatch.forceLeftSecondary =
                isLeftHand ? secondary : FingerPatch.forceLeftSecondary;
            FingerPatch.forceLeftTrigger = isLeftHand ? trigger : FingerPatch.forceLeftTrigger;

            FingerPatch.forceRightGrip = !isLeftHand ? grip : FingerPatch.forceRightGrip;
            FingerPatch.forceRightPrimary =
                !isLeftHand ? primary : FingerPatch.forceRightPrimary;
            FingerPatch.forceRightSecondary =
                !isLeftHand ? secondary : FingerPatch.forceRightSecondary;
            FingerPatch.forceRightTrigger =
                !isLeftHand ? trigger : FingerPatch.forceRightTrigger;
        }

        private void OnEnable()
        {
            Logging.Debug("  Current animator:", Rig.Instance?.Animator?.name ?? "null");
            Logging.Debug("  body:", body == null);

            if (body == null || Rig.Instance?.Animator == null)
            {
                Debug.LogWarning("HandDriver not properly initialized, disabling.");
                enabled = false; // Disable if dependencies are missing
                return;
            }

            Logging.Debug("  Enabling HandDriver", base.name);
            base.transform.position = DefaultPosition;
            targetPosition = DefaultPosition;

            try
            {
                handMap.overrideTarget = base.transform;
            }
            catch (Exception ex)
            {
                Logging.Exception(ex);
                Debug.LogError("Error setting overrideTarget: " + ex.Message);
            }
        }

        private IEnumerator Disable(Action<HandDriver> onDisable)
        {
            base.transform.position = DefaultPosition;
            yield return new WaitForSeconds(0.1f);
            handMap.overrideTarget = null;
            base.enabled = false;
            onDisable?.Invoke(this); // Use the null-conditional operator
        }

        public void Reset()
        {
            grip = trigger = primary = secondary = false; // Chained assignment
            targetPosition = DefaultPosition;
            lookAt = targetPosition + body.forward;
            up = isLeft ? body.right : (-body.right);
            hideControllerTransform = true;
            grounded = false;
        }
    }
}
