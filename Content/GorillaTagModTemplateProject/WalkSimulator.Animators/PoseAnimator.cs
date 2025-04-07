using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Rigging;

namespace WalkSimulator.Animators
{
    public class PoseAnimator : AnimatorBase
    {
        [SerializeField] private Vector3 offsetLeft;
        [SerializeField] private Vector3 lookAtLeft = Vector3.forward;
        [SerializeField] private Vector3 offsetRight;
        [SerializeField] private Vector3 lookAtRight = Vector3.forward;
        [SerializeField] private float zRotationLeft;
        [SerializeField] private float zRotationRight;

        private HandDriver main;
        private HandDriver secondary;
        private Vector3 eulerAngles;

        public override void Animate()
        {
            rig.headDriver.turn = false;
            AnimateBody();
            AnimateHands();
        }

        private void Update()
        {
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                (main, secondary) = (secondary, main); // Swap main and secondary
            }

            if (Keyboard.current.rKey.isPressed)
            {
                RotateHand();
            }
            else
            {
                PositionHand();
            }

            Vector3 lookAtDirection = main.isLeft ? lookAtLeft : lookAtRight;
            float zRotation = main.isLeft ? zRotationLeft : zRotationRight;
            float rotationDirection = main.isLeft ? -1f : 1f;

            main.up = Quaternion.AngleAxis(zRotation * rotationDirection, lookAtDirection) *
                      head.up;

            main.trigger = Mouse.current.leftButton.isPressed;
            main.grip = Mouse.current.rightButton.isPressed;
            main.primary = Mouse.current.backButton.isPressed ||
                           Keyboard.current.leftBracketKey.isPressed;
            main.secondary = Mouse.current.forwardButton.isPressed ||
                             Keyboard.current.rightBracketKey.isPressed;
        }

        private void RotateHand()
        {
            float mouseYDelta = Mouse.current.delta.value.y / 10f;
            float mouseXDelta = Mouse.current.delta.value.x / 10f;
            float scrollDelta = Mouse.current.scroll.ReadValue().y / 5f;

            eulerAngles.x -= mouseYDelta;
            eulerAngles.x = Mathf.Clamp(NormalizeAngle(eulerAngles.x), -85f, 85f);

            eulerAngles.y += mouseXDelta;
            eulerAngles.y = Mathf.Clamp(NormalizeAngle(eulerAngles.y), -85f, 85f);

            if (main.isLeft)
            {
                lookAtLeft = Quaternion.Euler(eulerAngles) * head.forward;
                zRotationLeft += scrollDelta;
            }
            else
            {
                lookAtRight = Quaternion.Euler(eulerAngles) * head.forward;
                zRotationRight += scrollDelta;
            }
        }

        private void PositionHand()
        {
            Vector3 offset = main.isLeft ? offsetLeft : offsetRight;
            float scrollDelta = Mouse.current.scroll.ReadValue().y / 1000f;

            offset.z += scrollDelta;

            if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                offset.z += 0.1f;
            }

            if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                offset.z -= 0.1f;
            }

            offset.z = Mathf.Clamp(offset.z, -0.25f, 0.75f);

            offset.x += Mouse.current.delta.ReadValue().x / 1000f;
            offset.x = Mathf.Clamp(offset.x, -0.5f, 0.5f);

            offset.y += Mouse.current.delta.ReadValue().y / 1000f;
            offset.y = Mathf.Clamp(offset.y, -0.5f, 0.5f);

            if (main.isLeft)
            {
                offsetLeft = offset;
            }
            else
            {
                offsetRight = offset;
            }
        }

        private void AnimateBody()
        {
            rig.active = true;
            rig.useGravity = false;
            rig.targetPosition = body.position;
        }

        private void AnimateHands()
        {
            float handDirection = main.isLeft ? -1f : 1f;
            Vector3 offset = main.isLeft ? offsetLeft : offsetRight;
            Vector3 lookAtDirection = main.isLeft ? lookAtLeft : lookAtRight;

            main.targetPosition =
                body.TransformPoint(new Vector3(handDirection * 0.2f, 0.1f, 0.3f) + offset);
            main.lookAt = main.targetPosition + lookAtDirection;
            main.hideControllerTransform = false;
        }

        public override void Setup()
        {
            base.Start();
            HeadDriver.Instance.LockCursor = true;
            main = rightHand;
            secondary = leftHand;
            offsetLeft = Vector3.zero;
            lookAtLeft = head.forward;
            offsetRight = Vector3.zero;
            lookAtRight = head.forward;

            secondary.targetPosition =
                secondary.DefaultPosition + Vector3.up * 0.2f * GorillaLocomotion.GTPlayer.Instance.scale;
            secondary.lookAt = secondary.targetPosition + head.forward;
            secondary.up = body.right * (main.isLeft ? -1f : 1f);
        }

        // Helper function to normalize angles
        private float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
