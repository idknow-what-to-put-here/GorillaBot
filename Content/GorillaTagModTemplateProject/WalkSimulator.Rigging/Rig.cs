using GorillaLocomotion;
using UnityEngine;
using WalkSimulator.Animators;

namespace WalkSimulator.Rigging
{
    public class Rig : MonoBehaviour
    {
        public static Rig Instance { get; private set; }
        public Vector3 smoothedGroundPosition { get; set; }

        public AnimatorBase Animator
        {
            get { return _animator; }
            set
            {
                if (_animator != null && value != _animator)
                {
                    _animator.Cleanup();
                }
                _animator = value;

                if (_animator != null)
                {
                    _animator.enabled = true;
                    _animator.Setup();
                }

                leftHand.enabled = _animator != null;
                rightHand.enabled = _animator != null;
                headDriver.enabled = _animator != null;
            }
        }

        public Transform head;
        public Transform body;
        public HeadDriver headDriver;
        public HandDriver leftHand;
        public HandDriver rightHand;
        public Rigidbody rigidbody;
        public Vector3 targetPosition;
        public Vector3 lastNormal;
        public Vector3 lastGroundPosition;
        public bool onGround;
        public bool active;
        public bool useGravity = true;

        public float scale = 1f;
        private float speed = 4f;
        private float raycastLength = 1.3f;
        private float raycastRadius = 0.3f;
        private Vector3 raycastOffset = new Vector3(0f, 0.4f, 0f);
        private AnimatorBase _animator;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (GorillaLocomotion.Player.Instance == null)
            {
                Debug.LogError("GorillaLocomotion.Player.Instance is null in Rig.Awake");
                enabled = false;
                return;
            }

            head = GorillaLocomotion.Player.Instance.headCollider.transform;
            body = GorillaLocomotion.Player.Instance.bodyCollider.transform;
            rigidbody = GorillaLocomotion.Player.Instance.bodyCollider.attachedRigidbody;

            leftHand = new GameObject("WalkSim Left Hand Driver").AddComponent<HandDriver>();
            leftHand.Init(true);
            leftHand.enabled = false;

            rightHand = new GameObject("WalkSim Right Hand Driver").AddComponent<HandDriver>();
            rightHand.Init(false);
            rightHand.enabled = false;

            headDriver = new GameObject("WalkSim Head Driver").AddComponent<HeadDriver>();
            headDriver.enabled = false;
        }

        private void FixedUpdate()
        {
            if (GorillaLocomotion.Player.Instance == null)
            {
                return;
            }

            scale = GorillaLocomotion.Player.Instance.scale;
            smoothedGroundPosition = Vector3.Lerp(smoothedGroundPosition, lastGroundPosition, 0.2f);
            OnGroundRaycast();

            Animator?.Animate();
            Move();
        }

        private void Move()
        {
            if (!active)
            {
                return;
            }

            rigidbody.velocity = Vector3.Lerp(rigidbody.velocity, (targetPosition - body.position) * speed, 1f);

            if (!useGravity)
            {
                rigidbody.AddForce(-Physics.gravity * rigidbody.mass * scale);
            }
        }

        private void OnGroundRaycast()
        {
            float scaledRaycastLength = raycastLength * scale;
            Vector3 raycastStartPoint = body.TransformPoint(raycastOffset);
            float scaledRaycastRadius = raycastRadius * scale;

            bool hasRaycastHit = Physics.Raycast(raycastStartPoint, Vector3.down, out RaycastHit raycastHit, scaledRaycastLength, GorillaLocomotion.Player.Instance.locomotionEnabledLayers);

            bool hasSpherecastHit = Physics.SphereCast(raycastStartPoint, scaledRaycastRadius, Vector3.down, out RaycastHit spherecastHit, scaledRaycastLength, GorillaLocomotion.Player.Instance.locomotionEnabledLayers);

            RaycastHit groundHit;

            if (hasRaycastHit && hasSpherecastHit)
            {
                groundHit = (raycastHit.distance <= spherecastHit.distance) ? raycastHit : spherecastHit;
            }
            else if (hasSpherecastHit)
            {
                groundHit = spherecastHit;
            }
            else if (hasRaycastHit)
            {
                groundHit = raycastHit;
            }
            else
            {
                onGround = false;
                return;
            }

            lastNormal = groundHit.normal;
            onGround = true;
            lastGroundPosition = groundHit.point;
            lastGroundPosition.x = body.position.x;
            lastGroundPosition.z = body.position.z;
        }

    }
}
