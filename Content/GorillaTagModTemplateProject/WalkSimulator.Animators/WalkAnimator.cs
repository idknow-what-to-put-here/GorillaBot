using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator.Animators
{
    public class WalkAnimator : AnimatorBase
    {
        public enum WalkAnimationMode
        {
            Default,
            Smooth,
            Tall,
            Short
        }

        [SerializeField] public float speed = 4f;
        [SerializeField] private float height = 0.4f;
        [SerializeField] private float raycastDistanceMultiplier = 0.5f;
        [SerializeField] private WalkAnimationMode animationMode = WalkAnimationMode.Short;

        [Range(15f, 75f)] // works?
        [SerializeField] public float jumpAngleDegrees = 45f;

        private float targetHeight;
        private bool hasJumped;
        private bool onJumpCooldown;
        private float jumpTime;
        private float walkCycleTime = 0f;

        public override void Animate()
        {
            MoveBody();
            AnimateHands();
        }

        private void Update()
        {
            if (!Plugin.Instance.Enabled)
            {
                return;
            }

            if (!hasJumped && rig.onGround && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                JumpMain();
            }

            if ((hasJumped && !rig.onGround) || Time.time - jumpTime > 1f)
            {
                onJumpCooldown = false;
            }

            if (rig.onGround && !onJumpCooldown)
            {
                hasJumped = false;
            }
        }

        public void JumpMain()
        {
            hasJumped = true;
            onJumpCooldown = true;
            jumpTime = Time.time;
            rig.active = false;
            rigidbody.AddForce(Vector3.up * 225f * Player.Instance.scale, ForceMode.Impulse);
        }
        public void JumpPath(float requiredHeight)
        {
            hasJumped = true;
            onJumpCooldown = true;
            jumpTime = Time.time;
            rig.active = false;

            float gravity = Mathf.Abs(Physics.gravity.y);
            float jumpVelocity = Mathf.Sqrt(2 * gravity * requiredHeight);

            rigidbody.AddForce(Vector3.up * jumpVelocity * Player.Instance.scale, ForceMode.Impulse);
        }
        public void JumpPath(Vector3 start, Vector3 target, float jumpAngle = 45f)
        {
            hasJumped = true;
            onJumpCooldown = true;
            jumpTime = Time.time;
            rig.active = false;

            float g = Mathf.Abs(Physics.gravity.y);
            Vector3 horizontalDisplacement = new Vector3(target.x - start.x, 0, target.z - start.z);
            float D = horizontalDisplacement.magnitude;
            float y = target.y - start.y;

            jumpAngle = Mathf.Clamp(jumpAngle, 15f, 75f);
            float theta = jumpAngle * Mathf.Deg2Rad;
            float cosTheta = Mathf.Cos(theta);
            float sinTheta = Mathf.Sin(theta);

            if (D < 0.1f)
            {
                float jumpVelocity = Mathf.Sqrt(2 * g * Mathf.Max(1f, y));
                rigidbody.AddForce(Vector3.up * jumpVelocity * Player.Instance.scale, ForceMode.Impulse);
                return;
            }

            float denominator = (D * Mathf.Tan(theta) - y);
            if (denominator <= 0)
            {
                float minAngle = Mathf.Atan((y + 0.1f) / D) * Mathf.Rad2Deg;
                theta = Mathf.Clamp(minAngle, 15f, 75f) * Mathf.Deg2Rad;
                cosTheta = Mathf.Cos(theta);
                sinTheta = Mathf.Sin(theta);
                denominator = (D * Mathf.Tan(theta) - y);
            }

            float v = Mathf.Sqrt((g * D * D) / (2 * cosTheta * cosTheta * denominator));
            Vector3 jumpDir = horizontalDisplacement.normalized;
            Vector3 initialVelocity = jumpDir * v * cosTheta + Vector3.up * v * sinTheta;
            rigidbody.AddForce(initialVelocity * Player.Instance.scale, ForceMode.Impulse);
        }

        public void MoveBody()
        {
            rig.active = rig.onGround && !hasJumped;
            rig.useGravity = !rig.onGround;

            if (!rig.onGround)
            {
                return;
            }

            float minHeight, maxHeight, modeSpeedMultiplier;
            AdjustParametersBasedOnMode(out minHeight, out maxHeight, out modeSpeedMultiplier);

            float cycleSpeed = NotMoving ? 2f * Mathf.PI : walkCycleTime * 2f * Mathf.PI * modeSpeedMultiplier;
            float heightOffset = Extensions.Map(Mathf.Sin(cycleSpeed), -1f, 1f, minHeight, maxHeight);

            targetHeight = heightOffset;
            Vector3 targetPosition = rig.lastGroundPosition + Vector3.up * targetHeight * Player.Instance.scale;

            Vector3 movementDirection = body.TransformDirection(InputHandler.inputDirectionNoY);
            movementDirection.y = 0f;

            if (Vector3.Dot(rig.lastNormal, Vector3.up) > 0.3f)
            {
                movementDirection = Vector3.ProjectOnPlane(movementDirection, rig.lastNormal);
            }

            movementDirection *= Player.Instance.scale;
            float currentSpeed = IsSprinting ? (speed * 3f) : speed;
            targetPosition += movementDirection * currentSpeed / 10f;

            rig.targetPosition = targetPosition;
        }

        private void AdjustParametersBasedOnMode(out float minHeight, out float maxHeight, out float modeSpeedMultiplier)
        {
            float crouchMultiplier = Keyboard.current.ctrlKey.isPressed ? 0.6f : 1f;

            switch (animationMode)
            {
                case WalkAnimationMode.Smooth:
                    minHeight = 0.52f * crouchMultiplier;
                    maxHeight = 0.53f * crouchMultiplier;
                    modeSpeedMultiplier = 0.8f;
                    break;
                case WalkAnimationMode.Tall:
                    minHeight = 0.58f * crouchMultiplier;
                    maxHeight = 0.74f * crouchMultiplier;
                    modeSpeedMultiplier = 1.2f;
                    break;
                case WalkAnimationMode.Short:
                    minHeight = 0.34f * crouchMultiplier;
                    maxHeight = 0.41f * crouchMultiplier;
                    modeSpeedMultiplier = 1.0f;
                    break;
                default:
                    minHeight = 0.5f * crouchMultiplier;
                    maxHeight = 0.55f * crouchMultiplier;
                    modeSpeedMultiplier = 1.0f;
                    break;
            }
        }

        private void AnimateHands()
        {
            leftHand.lookAt = leftHand.targetPosition + body.forward;
            rightHand.lookAt = rightHand.targetPosition + body.forward;

            leftHand.up = body.right;
            rightHand.up = -body.right;

            if (!rig.onGround)
            {
                AnimateHandsInAir();
            }
            else
            {
                AnimateHandsOnGround();
            }
        }

        private void AnimateHandsInAir()
        {
            leftHand.grounded = false;
            rightHand.grounded = false;
            Vector3 handOffset = Vector3.up * 0.2f * Player.Instance.scale;
            leftHand.targetPosition = leftHand.DefaultPosition;
            rightHand.targetPosition = rightHand.DefaultPosition + handOffset;
        }

        private void AnimateHandsOnGround()
        {
            UpdateHitInfo(leftHand);
            UpdateHitInfo(rightHand);

            if (NotMoving)
            {
                leftHand.targetPosition = leftHand.hit;
                rightHand.targetPosition = rightHand.hit;
            }
            else
            {
                if (!leftHand.grounded && !rightHand.grounded)
                {
                    leftHand.grounded = true;
                    leftHand.lastSnap = leftHand.hit;
                    leftHand.targetPosition = leftHand.hit;
                    rightHand.lastSnap = rightHand.hit;
                    rightHand.targetPosition = rightHand.hit;
                }
                AnimateHand(leftHand, rightHand);
                AnimateHand(rightHand, leftHand);
            }
        }

        private void UpdateHitInfo(HandDriver hand)
        {
            float scale = Player.Instance.scale;
            float raycastDistance = raycastDistanceMultiplier * scale;
            Vector3 smoothedGroundPosition = rig.smoothedGroundPosition;
            Vector3 groundNormal = rig.lastNormal;

            float forwardInfluence = Mathf.Abs(Vector3.Dot(InputHandler.inputDirectionNoY, Vector3.forward));
            float handOffsetInfluence = Extensions.Map(forwardInfluence, 0f, 1f, 0.4f, 0.5f);

            Vector3 handOffsetDirection = body.TransformDirection(InputHandler.inputDirectionNoY * handOffsetInfluence);
            handOffsetDirection.y = 0f;
            handOffsetDirection *= scale;

            Vector3 raycastStartPoint = Vector3.ProjectOnPlane(hand.DefaultPosition - smoothedGroundPosition + handOffsetDirection, groundNormal);
            raycastStartPoint += smoothedGroundPosition + groundNormal * 0.3f * scale;

            if (!Physics.Raycast(raycastStartPoint, -groundNormal, out RaycastHit hitInfo, raycastDistance, Player.Instance.locomotionEnabledLayers))
            {
                if (NotMoving)
                {
                    hand.targetPosition = hand.DefaultPosition;
                }
            }
            else
            {
                hand.hit = hitInfo.point;
                hand.normal = hitInfo.normal;
                hand.lookAt = hand.transform.position + body.forward;
            }
        }

        private void AnimateHand(HandDriver hand, HandDriver otherHand)
        {
            float forwardInfluence = Mathf.Abs(Vector3.Dot(InputHandler.inputDirectionNoY, Vector3.forward));
            float verticalInfluence = Vector3.Dot(rig.lastNormal, Vector3.up);
            float stepHeightInfluence = Extensions.Map(verticalInfluence, 0f, 1f, 0.1f, 0.6f);
            float stepLengthInfluence = Extensions.Map(forwardInfluence, 0f, 1f, 0.5f, 1.25f);

            stepLengthInfluence *= stepHeightInfluence * Player.Instance.scale;
            float stepHeight = GetStepHeight();

            float distanceSinceLastStep = otherHand.hit.Distance(otherHand.lastSnap) / stepLengthInfluence;

            if (otherHand.grounded && distanceSinceLastStep >= 1f)
            {
                hand.targetPosition = hand.hit;
                hand.lastSnap = hand.hit;
                hand.grounded = true;
                otherHand.grounded = false;
            }
            else
            {
                if (otherHand.grounded)
                {
                    walkCycleTime = distanceSinceLastStep;
                    hand.targetPosition = Vector3.Slerp(hand.lastSnap, hand.hit, walkCycleTime);
                    hand.targetPosition += hand.normal * stepHeight * Mathf.Sin(walkCycleTime);
                    hand.grounded = false;
                }
            }

            if (hand.targetPosition.Distance(hand.DefaultPosition) > 1f)
            {
                hand.targetPosition = hand.DefaultPosition;
            }
        }

        private float GetStepHeight()
        {
            float scale = Player.Instance.scale;
            switch (animationMode)
            {
                case WalkAnimationMode.Smooth:
                    return 0.15f * scale;
                case WalkAnimationMode.Tall:
                    return 0.38f * scale;
                case WalkAnimationMode.Short:
                    return 0.04f * scale;
                default:
                    return 0.2f * scale;
            }
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
        }

        private bool IsSprinting
        {
            get { return Keyboard.current.leftShiftKey.isPressed; }
        }

        private bool NotMoving
        {
            get { return InputHandler.inputDirectionNoY == Vector3.zero; }
        }
    }
}
