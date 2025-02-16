using UnityEngine;
using WalkSimulator.Rigging;

namespace WalkSimulator.Animators
{
    public abstract class AnimatorBase : MonoBehaviour
    {
        protected Transform body { get; private set; }
        protected Transform head { get; private set; }
        public Rigidbody rigidbody { get; private set; }
        protected HandDriver leftHand { get; private set; }
        protected HandDriver rightHand { get; private set; }
        protected Rig rig { get; private set; }

        protected virtual void Start()
        {
            Logging.Debug("==START==");

            if (Rig.Instance == null)
            {
                Debug.LogError("Rig.Instance is null in AnimatorBase.Start");
                enabled = false; // Disable if Rig is missing
                return;
            }

            rig = Rig.Instance;
            body = rig.body;
            head = rig.head;
            rigidbody = rig.rigidbody;
            leftHand = rig.leftHand;
            rightHand = rig.rightHand;
        }

        public abstract void Setup();

        public virtual void Cleanup()
        {
            enabled = false;
            rig.active = false;
            rig.useGravity = true;
            rig.headDriver.turn = true;
            leftHand.Reset();
            rightHand.Reset();
        }

        public abstract void Animate();
    }
}
