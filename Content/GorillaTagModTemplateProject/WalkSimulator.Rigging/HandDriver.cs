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
        public bool UpdateHandRotation = true;
        public bool grip;
        public bool trigger;
        public bool primary;
        public bool secondary;
        public bool isLeft { get; private set; }
        public bool grounded;
        public bool hideControllerTransform = true;
        public Vector3 targetPosition;
        public Vector3 lookAt;
        public Vector3 up;
        public float followRate = 0.1f;
        public Vector3 hit;
        public Vector3 lastSnap;
        public Vector3 normal;

        private Quaternion currentRotationOffset = Quaternion.identity;
        private Quaternion targetRotationOffset = Quaternion.identity;
        public float rotationChangeInterval = 2f;
        public float rotationSlerpSpeed = 2f;

        private VRMap handMap;
        private Vector3 defaultOffset;
        private Transform head;
        private Transform body;
        public Transform controller;

        public Vector3 DefaultPosition
        {
            get { return body.TransformPoint(defaultOffset); }
        }

        public void Init(bool isLeft)
        {
            this.isLeft = isLeft;
            defaultOffset = new Vector3(isLeft ? (-0.25f) : 0.25f, -0.45f, 0.2f);
            head = Rig.Instance.head;
            body = Rig.Instance.body;

            if (GorillaLocomotion.GTPlayer.Instance == null)
            {
                Debug.LogError("GorillaLocomotion.GTPlayer.Instance is null in HandDriver.Init");
                enabled = false;
                return;
            }

            controller = isLeft ? GorillaLocomotion.GTPlayer.Instance.leftControllerTransform : GorillaLocomotion.GTPlayer.Instance.rightControllerTransform;

            if (GorillaTagger.Instance == null || GorillaTagger.Instance.offlineVRRig == null)
            {
                Debug.LogError("GorillaTagger.Instance or offlineVRRig is null in HandDriver.Init");
                enabled = false;
                return;
            }

            handMap = isLeft ? GorillaTagger.Instance.offlineVRRig.leftHand : GorillaTagger.Instance.offlineVRRig.rightHand;

            transform.position = DefaultPosition;
            targetPosition = DefaultPosition;
            lastSnap = DefaultPosition;
            hit = DefaultPosition;
            up = Vector3.up;
        }

        private void FixedUpdate()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, followRate);

            currentRotationOffset = Quaternion.Slerp(currentRotationOffset, targetRotationOffset, Time.fixedDeltaTime * rotationSlerpSpeed);
            Quaternion baseRotation = Quaternion.LookRotation(lookAt - transform.position, up);
            transform.rotation = baseRotation * currentRotationOffset;

            controller.position = hideControllerTransform ? body.position : transform.position;

            bool isLeftHand = isLeft;
            FingerPatch.forceLeftGrip = isLeftHand ? grip : FingerPatch.forceLeftGrip;
            FingerPatch.forceLeftPrimary = isLeftHand ? primary : FingerPatch.forceLeftPrimary;
            FingerPatch.forceLeftSecondary = isLeftHand ? secondary : FingerPatch.forceLeftSecondary;
            FingerPatch.forceLeftTrigger = isLeftHand ? trigger : FingerPatch.forceLeftTrigger;

            FingerPatch.forceRightGrip = !isLeftHand ? grip : FingerPatch.forceRightGrip;
            FingerPatch.forceRightPrimary = !isLeftHand ? primary : FingerPatch.forceRightPrimary;
            FingerPatch.forceRightSecondary = !isLeftHand ? secondary : FingerPatch.forceRightSecondary;
            FingerPatch.forceRightTrigger = !isLeftHand ? trigger : FingerPatch.forceRightTrigger;
        }

        private void OnEnable()
        {
            Logging.Debug("  Current animator:", Rig.Instance?.Animator?.name ?? "null");
            Logging.Debug("  body:", body == null);

            if (body == null || Rig.Instance?.Animator == null)
            {
                Debug.LogWarning("HandDriver not properly initialized, disabling.");
                enabled = false;
                return;
            }

            Logging.Debug("  Enabling HandDriver", name);
            transform.position = DefaultPosition;
            targetPosition = DefaultPosition;

            try
            {
                handMap.overrideTarget = transform;
            }
            catch (Exception ex)
            {
                Logging.Exception(ex);
                Debug.LogError("Error setting overrideTarget: " + ex.Message);
            }

            if (UpdateHandRotation)
            {
                StartCoroutine(UpdateRotationOffset());
            }
        }

        private IEnumerator UpdateRotationOffset()
        {
            while (true)
            {
                float randomX = UnityEngine.Random.Range(-30f, 30f);
                float randomY = UnityEngine.Random.Range(-30f, 30f);
                float randomZ = UnityEngine.Random.Range(-30f, 30f);
                targetRotationOffset = Quaternion.Euler(randomX, randomY, randomZ);
                yield return new WaitForSeconds(rotationChangeInterval);
            }
        }

        private IEnumerator Disable(Action<HandDriver> onDisable)
        {
            transform.position = DefaultPosition;
            yield return new WaitForSeconds(0.1f);
            handMap.overrideTarget = null;
            enabled = false;
            onDisable?.Invoke(this);
        }

        public void Reset()
        {
            grip = trigger = primary = secondary = false;
            targetPosition = DefaultPosition;
            lookAt = targetPosition + body.forward;
            up = isLeft ? body.right : (-body.right);
            hideControllerTransform = true;
            grounded = false;
        }
    }
}
