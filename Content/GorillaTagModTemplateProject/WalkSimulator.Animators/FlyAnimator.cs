<<<<<<< HEAD
﻿using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator.Animators
{
    public class FlyAnimator : AnimatorBase
    {
        [SerializeField] private float speed = 1f;
        [SerializeField] private float minSpeed = 0f;
        [SerializeField] private float maxSpeed = 5f;

        public override void Animate()
        {
            AnimateBody();
            AnimateHands();
        }

        private void Update()
        {
            speed += Mouse.current.scroll.ReadValue().y / 1000f;
            speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        }

        private void AnimateBody()
        {
            rig.active = true;
            rig.useGravity = false;
            rig.targetPosition = body.TransformPoint(InputHandler.inputDirection * speed);
        }

        private void AnimateHands()
        {
            float followRate = Extensions.Map(speed, minSpeed, maxSpeed, 0f, 1f);
            leftHand.followRate = followRate;
            rightHand.followRate = followRate;

            leftHand.targetPosition = leftHand.DefaultPosition;
            rightHand.targetPosition = rightHand.DefaultPosition;

            leftHand.lookAt = leftHand.targetPosition + body.forward;
            rightHand.lookAt = rightHand.targetPosition + body.forward;

            leftHand.up = body.right;
            rightHand.up = -body.right;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            leftHand.followRate = 0.1f;
            rightHand.followRate = 0.1f;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
        }
    }
}
=======
﻿using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator.Animators
{
    public class FlyAnimator : AnimatorBase
    {
        [SerializeField] private float speed = 1f;
        [SerializeField] private float minSpeed = 0f;
        [SerializeField] private float maxSpeed = 5f;

        public override void Animate()
        {
            AnimateBody();
            AnimateHands();
        }

        private void Update()
        {
            speed += Mouse.current.scroll.ReadValue().y / 1000f;
            speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        }

        private void AnimateBody()
        {
            rig.active = true;
            rig.useGravity = false;
            rig.targetPosition = body.TransformPoint(InputHandler.inputDirection * speed);
        }

        private void AnimateHands()
        {
            float followRate = Extensions.Map(speed, minSpeed, maxSpeed, 0f, 1f);
            leftHand.followRate = followRate;
            rightHand.followRate = followRate;

            leftHand.targetPosition = leftHand.DefaultPosition;
            rightHand.targetPosition = rightHand.DefaultPosition;

            leftHand.lookAt = leftHand.targetPosition + body.forward;
            rightHand.lookAt = rightHand.targetPosition + body.forward;

            leftHand.up = body.right;
            rightHand.up = -body.right;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            leftHand.followRate = 0.1f;
            rightHand.followRate = 0.1f;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
        }
    }
}
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
