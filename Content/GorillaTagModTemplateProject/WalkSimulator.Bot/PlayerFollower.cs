using BepInEx.Logging;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Collections.Generic;
using WalkSimulator.Animators;
using WalkSimulator.Rigging;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;
using System;
using System.Text;
using BepInEx.Configuration;
using UnityEngine.AI;
using static WalkSimulator.Bot.PlayerFollowerUtils;
using System.Threading.Tasks;

namespace WalkSimulator.Bot
{
    #region StateMachines
    public enum MovementState
    {
        Idle,
        Moving
    }
    public enum PathingState
    {
        Idle,
        FollowingPath,
        Stopped
    }
    public enum FollowingState
    {
        Idle,
        FollowingPlayer,
        Stopped
    }
    public enum TaggingState
    {
        Idle,
        ChasingPlayer,
        Stopped
    }
    public enum FleeState
    {
        Idle,
        Fleeing,
        Stopped
    }
    public class MovementStateMachine
    {
        public MovementState CurrentState { get; private set; }
        private PlayerFollower playerFollower;

        public MovementStateMachine(PlayerFollower follower)
        {
            playerFollower = follower;
            CurrentState = MovementState.Idle;
        }
        public void StartMoving()
        {
            if (CurrentState == MovementState.Idle)
            {
                CurrentState = MovementState.Moving;
                playerFollower.walkAnimator.enabled = true;
            }
        }
        public void StopMoving()
        {
            if (CurrentState == MovementState.Moving)
            {
                CurrentState = MovementState.Idle;
                playerFollower.walkAnimator.enabled = false;
                InputHandler.inputDirectionNoY = Vector3.zero;
            }
        }
        public void UpdateMovement(Vector3 localDirection)
        {
            if (CurrentState == MovementState.Moving)
            {
                InputHandler.inputDirectionNoY = new Vector3(localDirection.x, 0f, localDirection.z);
            }
        }
    }
    public class PathingStateMachine
    {
        public PathingState CurrentState { get; private set; }
        private PlayerFollower playerFollower;

        public PathingStateMachine(PlayerFollower follower)
        {
            playerFollower = follower;
            CurrentState = PathingState.Idle;
        }
        public void StartPathing()
        {
            if (CurrentState == PathingState.Idle || CurrentState == PathingState.Stopped)
            {
                CurrentState = PathingState.FollowingPath;
                playerFollower.followPathEnabled = true;
            }
        }
        public void StopPathing()
        {
            if (CurrentState == PathingState.FollowingPath)
            {
                CurrentState = PathingState.Idle;
                playerFollower.followPathEnabled = false;
                InputHandler.inputDirectionNoY = Vector3.zero;
                Transform localBody = Rig.Instance.body;
                if (localBody != null)
                {
                    playerFollower.lineRenderers.ClearPath(localBody);
                }
                playerFollower.lineRenderers.pathPositions.Clear();
            }
        }
        public void MoveAlongPath()
        {
            if (CurrentState != PathingState.FollowingPath) return;

            Transform localBody = Rig.Instance.body;
            if (localBody == null) return;

            if (playerFollower.lineRenderers.pathPositions.Count == 0)
            {
                StopPathing();
                return;
            }

            Vector3 targetPoint = playerFollower.lineRenderers.pathPositions[0];
            playerFollower.lineRenderers.UpdateLineRenderersForPath(localBody, targetPoint);
            Vector3 directionToTarget = targetPoint - localBody.position;
            directionToTarget.y = 0f;
            float distanceToTarget = directionToTarget.magnitude;

            if (playerFollower.ShouldJump(localBody.position, targetPoint))
            {
                if (!playerFollower.isPreparingToJump)
                {
                    playerFollower.isPreparingToJump = true;
                    playerFollower.jumpTarget = targetPoint;
                }
                if (distanceToTarget <= PlayerFollower.JUMP_PREPARATION_DISTANCE)
                {
                    playerFollower.TriggerJump();
                }
            }

            if (distanceToTarget < PlayerFollower.TARGET_PROXIMITY_THRESHOLD)
            {
                playerFollower.lineRenderers.pathPositions.RemoveAt(0);
                if (playerFollower.lineRenderers.pathPositions.Count == 0)
                {
                    playerFollower.movementStateMachine.StopMoving();
                    StopPathing();
                    return;
                }
                playerFollower.isPreparingToJump = false;
                targetPoint = playerFollower.lineRenderers.pathPositions[0];
                directionToTarget = targetPoint - localBody.position;
                directionToTarget.y = 0f;
                distanceToTarget = directionToTarget.magnitude;
            }

            Vector3 finalDirection = playerFollower.avoidObjects ? 
                playerFollower.AvoidObstacles(localBody.position, directionToTarget.normalized) : 
                directionToTarget.normalized;

            Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * finalDirection;
            playerFollower.movementStateMachine.UpdateMovement(localDirection);
            playerFollower.TurnTowardsTargetPosition(localBody, targetPoint);
        }
    }
    public class FollowingStateMachine
    {
        public FollowingState CurrentState { get; private set; }
        private PlayerFollower playerFollower;

        public FollowingStateMachine(PlayerFollower follower)
        {
            playerFollower = follower;
            CurrentState = FollowingState.Idle;
        }
        public void StartFollowing(Player player)
        {
            if (CurrentState == FollowingState.Idle || CurrentState == FollowingState.Stopped)
            {
                CurrentState = FollowingState.FollowingPlayer;
                playerFollower.followPlayerEnabled = true;
                playerFollower.currentPlayer = player;
                playerFollower.followPathEnabled = false;
            }
        }
        public void StopFollowing()
        {
            if (CurrentState == FollowingState.FollowingPlayer)
            {
                CurrentState = FollowingState.Idle;
                playerFollower.followPlayerEnabled = false;
                playerFollower.currentPlayer = null;
                playerFollower.lineRenderers.DisableLineRenderers();
            }
        }
        public void MoveToTargetPlayer()
        {
            if (CurrentState != FollowingState.FollowingPlayer || playerFollower.currentPlayer == null) return;

            var (targetTransform, localBody) = playerFollower.GetPlayerTransforms(playerFollower.currentPlayer);
            if (targetTransform == null || localBody == null) return;

            Vector3 interceptionPoint;

            if (playerFollower.isPredictionEnabled)
            {
                playerFollower.movementPredictor.UpdatePlayerMovement(playerFollower.currentPlayer, targetTransform.position);

                Vector3 smoothedVelocity = playerFollower.movementPredictor.GetSmoothedVelocity(playerFollower.currentPlayer);
<<<<<<< HEAD
                Vector3 localVelocity = GorillaLocomotion.GTPlayer.Instance.RigidbodyVelocity;
=======
                Vector3 localVelocity = playerFollower.GetLocalPlayerVelocity();
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
                float localSpeed = localVelocity.magnitude;

                interceptionPoint = playerFollower.movementPredictor.GetPredictedInterceptionPoint(
                    playerFollower.currentPlayer,
                    localBody.position,
                    localSpeed > 0 ? localSpeed : 3f
                );

                if (playerFollower.movementPredictor.IsPlayerLikelyToJump(playerFollower.currentPlayer))
                {
                    interceptionPoint = playerFollower.movementPredictor.PredictPlayerPosition(playerFollower.currentPlayer, 1.2f);
                }
            }
            else
            {
                interceptionPoint = targetTransform.position;
            }

            GameObject tempTarget = new GameObject("TempPredictedTarget");
            tempTarget.transform.position = interceptionPoint;

            playerFollower.lineRenderers.UpdateLineRenderers(localBody, tempTarget.transform);
            Vector3 directionToTarget = interceptionPoint - localBody.position;
            directionToTarget.y = 0f;
            float distanceToTarget = directionToTarget.magnitude;

            Vector3 finalDirection = playerFollower.avoidObjects ?
                playerFollower.AvoidObstacles(localBody.position, directionToTarget.normalized) :
                directionToTarget.normalized;

            if (distanceToTarget < PlayerFollower.TARGET_PROXIMITY_THRESHOLD)
            {
                playerFollower.movementStateMachine.StopMoving();
                UnityEngine.Object.Destroy(tempTarget);
                return;
            }

            Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * finalDirection;
            playerFollower.movementStateMachine.UpdateMovement(localDirection);
            playerFollower.TurnTowardsTargetPosition(localBody, interceptionPoint);

            UnityEngine.Object.Destroy(tempTarget);
        }
    }
    public class TaggingStateMachine
    {
        public TaggingState CurrentState { get; private set; }
        private PlayerFollower playerFollower;

        public TaggingStateMachine(PlayerFollower follower)
        {
            playerFollower = follower;
            CurrentState = TaggingState.Idle;
        }
        public void StartTagging()
        {
            if (CurrentState == TaggingState.Idle)
            {
                CurrentState = TaggingState.ChasingPlayer;
                playerFollower.isTagging = true;
                playerFollower.taggedPlayer = PlayerFollowerUtils.Tagging.GetRandomNonTaggedPlayer();
                if (playerFollower.taggedPlayer != null)
                {
                    playerFollower.tagTimer = playerFollower.tagDuration;
                }
            }
        }
        public void StopTagging()
        {
            if (CurrentState == TaggingState.ChasingPlayer)
            {
                CurrentState = TaggingState.Idle;
                playerFollower.isTagging = false;
                playerFollower.taggedPlayer = null;
                playerFollower.movementStateMachine.StopMoving();
            }
        }
        public void MoveToTaggedPlayer()
        {
            if (CurrentState != TaggingState.ChasingPlayer || playerFollower.taggedPlayer == null)
            {
                StopTagging();
                return;
            }

            var (targetTransform, localBody) = playerFollower.GetPlayerTransforms(playerFollower.taggedPlayer);
            if (targetTransform == null || localBody == null) return;

            Vector3 interceptionPoint;

            if (playerFollower.isPredictionEnabled)
            {
                playerFollower.movementPredictor.UpdatePlayerMovement(playerFollower.taggedPlayer, targetTransform.position);

<<<<<<< HEAD
                Vector3 localVelocity = GorillaLocomotion.GTPlayer.Instance.RigidbodyVelocity;
=======
                Vector3 localVelocity = playerFollower.GetLocalPlayerVelocity();
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
                float localSpeed = localVelocity.magnitude;

                interceptionPoint = playerFollower.movementPredictor.GetPredictedInterceptionPoint(
                    playerFollower.taggedPlayer,
                    localBody.position,
                    localSpeed > 0 ? localSpeed * 1.2f : 4f
                );

                if (playerFollower.movementPredictor.IsPlayerLikelyToJump(playerFollower.taggedPlayer))
                {
                    interceptionPoint = playerFollower.movementPredictor.PredictPlayerPosition(playerFollower.taggedPlayer, 1.5f);
                }
            }
            else
            {
<<<<<<< HEAD
=======
                // Fallback: move directly towards the current target position
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
                interceptionPoint = targetTransform.position;
            }

            GameObject tempTarget = new GameObject("TempTagTarget");
            tempTarget.transform.position = interceptionPoint;

            playerFollower.lineRenderers.UpdateLineRenderers(localBody, tempTarget.transform);
            Vector3 directionToTarget = interceptionPoint - localBody.position;
            directionToTarget.y = 0f;
            float distanceToTarget = directionToTarget.magnitude;

            Vector3 finalDirection = playerFollower.avoidObjects ?
                playerFollower.AvoidObstacles(localBody.position, directionToTarget.normalized) :
                directionToTarget.normalized;

            if (distanceToTarget < PlayerFollower.TARGET_PROXIMITY_THRESHOLD)
            {
                playerFollower.movementStateMachine.StopMoving();
                UnityEngine.Object.Destroy(tempTarget);
                return;
            }

            Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * finalDirection;
            playerFollower.movementStateMachine.UpdateMovement(localDirection);
            playerFollower.TurnTowardsTargetPosition(localBody, interceptionPoint);

            UnityEngine.Object.Destroy(tempTarget);
        }
    }
    public class FleeStateMachine
    {
        public FleeState CurrentState { get; private set; }
        private PlayerFollower playerFollower;

        public FleeStateMachine(PlayerFollower follower)
        {
            playerFollower = follower;
            CurrentState = FleeState.Idle;
        }
        public void StartFleeing()
        {
            if (CurrentState == FleeState.Idle)
            {
                CurrentState = FleeState.Fleeing;
                playerFollower.fleeEnabled = true;
            }
        }
        public void StopFleeing()
        {
            if (CurrentState == FleeState.Fleeing)
            {
                CurrentState = FleeState.Idle;
                playerFollower.fleeEnabled = false;
                playerFollower.movementStateMachine.StopMoving();
            }
        }
        public void FleeFromTaggers()
        {
            if (CurrentState != FleeState.Fleeing) return;

            Transform localBody = Rig.Instance.body;
            if (localBody == null) return;

            List<Vector3> taggedPlayerPositions = new List<Vector3>();
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                var vrrig = GorillaGameManager.instance.FindPlayerVRRig(player);
                if (vrrig != null && PlayerFollowerUtils.Tagging.PlayerIsTagged(vrrig))
                {
                    taggedPlayerPositions.Add(vrrig.transform.position);
                }
            }

            if (taggedPlayerPositions.Count == 0)
            {
                playerFollower.movementStateMachine.StopMoving();
                return;
            }

            Vector3 fleeDirection = Vector3.zero;
            foreach (Vector3 pos in taggedPlayerPositions)
            {
                Vector3 toTagger = localBody.position - pos;
                float distance = toTagger.magnitude;
                if (distance < playerFollower.FLEE_RADIUS)
                {
                    fleeDirection += toTagger.normalized * (1 / Mathf.Max(distance, 0.1f));
                }
            }

            if (fleeDirection == Vector3.zero)
            {
                playerFollower.movementStateMachine.StopMoving();
                return;
            }

            fleeDirection.Normalize();
            fleeDirection = playerFollower.AvoidObstaclesTaggers(localBody.position, fleeDirection);

            Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * fleeDirection;
            playerFollower.lineRenderers.UpdateLineRenderersForPath(localBody, fleeDirection);
            playerFollower.movementStateMachine.UpdateMovement(localDirection);
            playerFollower.TurnTowardsTargetPosition(localBody, localBody.position + fleeDirection);
        }
    }
    #endregion
    public class PlayerFollower : MonoBehaviour
    {
        #region Fields and Properties
        // Core Components
        private PlayerFollowerGUI gui;
        public MovementRecorder movementRecorder;
        public ConfigEntry<string> DiscordWebhookUrl;

        // State Machines
        public MovementStateMachine movementStateMachine { get; private set; }
        private PathingStateMachine pathingStateMachine;
        private FollowingStateMachine followingStateMachine;
        private TaggingStateMachine taggingStateMachine;
        private FleeStateMachine fleeStateMachine;
        public PlayerMovementPredictor movementPredictor;

        // Movement Constants
        public const float TARGET_PROXIMITY_THRESHOLD = 0.5f;
        private const float MAX_TURN_SPEED = 15f;
        private const float TURN_SPEED_DIVISOR = 10f;
        private const float ROTATION_THRESHOLD = 1f;
        private const float JUMP_HEIGHT_THRESHOLD = 0.6f;
        public const float JUMP_PREPARATION_DISTANCE = 1.2f;

        // Movement State
        public bool followPlayerEnabled;
        public bool followPathEnabled;
        public Player currentPlayer;
        public WalkAnimator walkAnimator;
        public ManualLogSource logger;
        public LineRenderers lineRenderers;
        private List<Vector3> pathPositions;

        // Line Configuration
        public Color pathColor = Color.red;
        public Color directionColor = Color.yellow;
        public float lineAlpha = 1f;
        public float pathLineWidth = 0.05f;
        public float directionLineWidth = 0.05f;
        public bool showPath = true;
        public bool showDirection = true;

        // Jump Configuration
        public bool isPreparingToJump = false;
        public Vector3 jumpTarget;
        public bool waitingForJumpStart = true;
        public Vector3 jumpWaypointStart;

        // Tagging Configuration
        public bool isTagging = false;
        public float tagDuration = 60f;
        public float tagTimer = 0f;
        public Player taggedPlayer;

        // Hand Configuration
        public PlayerFollowerGUI.HandButton selectedHandButton = PlayerFollowerGUI.HandButton.Grip;
        public PlayerFollowerGUI.ActivationPoint activationPoint = PlayerFollowerGUI.ActivationPoint.OnReachGround;
        public float handDownDuration = 0.5f;
        public float buttonHoldDuration = 1.0f;
        public float handUpDuration = 0.5f;
        public float handGroundDistance = 0.5f;

        // Object Scanning
        public bool avoidObjects = false;
        public List<GameObject> activeObjects = new List<GameObject>();
        public HashSet<GameObject> blacklistedObjects = new HashSet<GameObject>();
        public float scanInterval = 100f;
        private float scanTimer = 0f;
        public float avoidanceRadius = 5f;
        public LayerMask obstacleLayers = -1;
        private readonly string[] obstaclePaths =
        {
            "Environment Objects/LocalObjects_Prefab/Forest/Terrain/SmallTrees/",
            "Environment Objects/LocalObjects_Prefab/Forest/Terrain/campgroundstructure/",
            "Environment Objects/LocalObjects_Prefab/Forest/Terrain/slide/",
            "Environment Objects/LocalObjects_Prefab/Forest/Terrain/pitgeo/",
            "System Scripts/ZoneGraph/City",
            "System Scripts/ZoneGraph/TreeRoom",
<<<<<<< HEAD
            "Environment Objects/LocalObjects_Prefab/Forest/ForestKiosk_Anchor/EndCap_ForestCampground",
            "Environment Objects/LocalObjects_Prefab/Forest/Terrain/campgroundstructure/PincicTable/",
            "Environment Objects/LocalObjects_Prefab/Forest/Terrain/campgroundstructure/stepladderlegs/"
=======
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
        };

        // Flee Configuration
        public bool fleeEnabled;
        public float FLEE_RADIUS = 10f;

        // Collision Detection
        public string collisionState = "No Collisions";
        private Dictionary<GameObject, bool> leftHandCollisions = new Dictionary<GameObject, bool>();
        private Dictionary<GameObject, bool> rightHandCollisions = new Dictionary<GameObject, bool>();
        public bool objectsInitialized = false;
        private readonly string[] blacklistTerms = { };
        private readonly string[] blacklistPaths = { "Player Objects", "System Scripts/ZoneGraph/Tutorial", "System Scripts/ZoneGraph/Forest", "System Scripts/ZoneGraph/Clouds" };
        private Dictionary<string, Transform> pathTransformCache = new Dictionary<string, Transform>();

        // Gun Configuration
        public bool gunEnabled = false;
        public float gunRange = 50f;
        public Color gunRayColor = Color.red;
        public float gunRayDuration = 0.2f;
        public float gunRayWidth = 0.05f;
        private float lastShotTime = 0f;
        public LayerMask gunLayers = -1;
        private LineRenderer gunRayRenderer;

        // Box Configuration
        public bool boxShootingMode = false;
        private Vector3? firstShotPoint = null;
        private List<LineRenderer> boxRenderers = new List<LineRenderer>();
        private Color boxColor = new Color(1f, 0.5f, 0f, 0.5f);
        private float boxLineWidth = 0.05f;
        private const int BOX_EDGES = 12;
        public float boxDisplayDuration = 60f;

        // Prediction
        private float lastCleanupTime = 0f;
<<<<<<< HEAD
        private const float CLEANUP_INTERVAL = 5f;
=======
        private const float CLEANUP_INTERVAL = 5f;
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
        public bool isPredictionEnabled = false;
        #endregion

        #region Init
<<<<<<< HEAD
=======
        public Vector3 GetLocalPlayerVelocity()
        {
            if (GorillaLocomotion.GTPlayer.Instance != null)
            {
                return GorillaLocomotion.GTPlayer.Instance.RigidbodyVelocity;
            }
            return Vector3.zero;
        }
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
        private void Awake()
        {
            Initialize();
            Invoke("InitializeObjects", 1.0f);
        }
        private void OnGUI()
        {
            gui.OnGUI();
        }
        private void FixedUpdate()
        {
            Update();
        }
        private void OnDestroy()
        {
            CleanupResources();
        }
        private void OnApplicationQuit()
        {
            SendLogs();
        }
        private void Initialize()
        {
            // Initialize Components
            gui = new PlayerFollowerGUI(this);
            movementRecorder = new MovementRecorder(this);
            logger = BepInEx.Logging.Logger.CreateLogSource("WalkSimulator");
            
            // Setup Logger
            logger.LogEvent += (logLevel, message) =>
            {
                string logMessage = $"{message}";
                logMessage = logMessage.Replace("[Info   :WalkSimulator]", null);
                logMessage = logMessage.Replace("[Debug   :WalkSimulator]", null);
                logMessage = logMessage.Replace("[Warning   :WalkSimulator]", null);
                logMessage = logMessage.Replace("[Message   :WalkSimulator]", null);
                PlayerFollowerGUI.logMessages.Add(logMessage);
            };
            logger.LogInfo("PlayerFollower plugin loaded!");

            // Initialize Line Renderers
            GameObject lrObj = new GameObject("LineRenderers");
            lineRenderers = lrObj.AddComponent<LineRenderers>();
            lineRenderers.Initialize("PathLine", "DirectionLine", pathColor, directionColor, lineAlpha, pathLineWidth, directionLineWidth);
            pathPositions = new List<Vector3>();

            // Initialize Gun Ray Renderer
            GameObject gunRayObj = new GameObject("GunRayRenderer");
            gunRayRenderer = gunRayObj.AddComponent<LineRenderer>();
            gunRayRenderer.material = new Material(Shader.Find("Sprites/Default"));
            gunRayRenderer.startWidth = gunRayWidth;
            gunRayRenderer.endWidth = gunRayWidth;
            gunRayRenderer.startColor = gunRayColor;
            gunRayRenderer.endColor = gunRayColor;
            gunRayRenderer.positionCount = 2;
            gunRayRenderer.enabled = false;

            // Initialize Box Renderers
            GameObject boxRenderersObj = new GameObject("BoxRenderers");
            for (int i = 0; i < BOX_EDGES; i++)
            {
                GameObject edgeObj = new GameObject($"BoxEdge_{i}");
                edgeObj.transform.SetParent(boxRenderersObj.transform);
                LineRenderer edgeRenderer = edgeObj.AddComponent<LineRenderer>();
                edgeRenderer.material = new Material(Shader.Find("Sprites/Default"));
                edgeRenderer.startWidth = boxLineWidth;
                edgeRenderer.endWidth = boxLineWidth;
                edgeRenderer.startColor = boxColor;
                edgeRenderer.endColor = boxColor;
                edgeRenderer.positionCount = 2;
                edgeRenderer.enabled = false;
                boxRenderers.Add(edgeRenderer);
            }

            // Create Preset Directory
            string presetDir = GetPresetDirectory();
            if (!Directory.Exists(presetDir))
            {
                Directory.CreateDirectory(presetDir);
                logger.LogInfo($"Created preset directory: {presetDir}");
            }

            // Initialize State Machines
            movementStateMachine = new MovementStateMachine(this);
            pathingStateMachine = new PathingStateMachine(this);
            followingStateMachine = new FollowingStateMachine(this);
            taggingStateMachine = new TaggingStateMachine(this);
            fleeStateMachine = new FleeStateMachine(this);
            movementPredictor = new PlayerMovementPredictor(this);

            // Initialize Presets
            InitializeHardcodedPresets();
        }
        private void Update()
        {
            if (Time.time - lastCleanupTime > CLEANUP_INTERVAL)
            {
                movementPredictor.CleanupDisconnectedPlayers();
                lastCleanupTime = Time.time;
            }

            // Update Movement Recorder
            movementRecorder.FixedUpdate();

            // Update Collisions
            if (Rig.Instance != null && Rig.Instance.active)
            {
                CheckCollisions();
            }

            // Update Gun Ray
            if (gunEnabled && gunRayRenderer != null)
            {
                Transform head = Rig.Instance.head;
                if (head != null)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(head.position, head.forward, out hit, gunRange))
                    {
                        gunRayRenderer.SetPosition(0, head.position);
                        gunRayRenderer.SetPosition(1, hit.point);
                    }
                    else
                    {
                        gunRayRenderer.SetPosition(0, head.position);
                        gunRayRenderer.SetPosition(1, head.position + head.forward * gunRange);
                    }
                    gunRayRenderer.enabled = true;
                }
            }
            else if (gunRayRenderer != null)
            {
                gunRayRenderer.enabled = false;
            }

            // Update Movement
            if (fleeEnabled)
            {
                fleeStateMachine.FleeFromTaggers();
            }
            else if (followPlayerEnabled && currentPlayer != null)
            {
                followingStateMachine.MoveToTargetPlayer();
            }
            else if (followPathEnabled && lineRenderers.pathPositions.Count > 0)
            {
                pathingStateMachine.MoveAlongPath();
            }
            else if (lineRenderers.pathPositions.Count > 0)
            {
                lineRenderers.pathLine.Renderer.enabled = true;
                lineRenderers.UpdatePathLineRenderer();
                lineRenderers.directionLine.Renderer.enabled = false;
            }
            else
            {
                lineRenderers.DisableLineRenderers();
            }

            // Update Tagging
            if (isTagging)
            {
                if (taggedPlayer != null)
                {
                    taggingStateMachine.MoveToTaggedPlayer();
                    tagTimer -= Time.fixedDeltaTime;
                    if (tagTimer <= 0)
                    {
                        taggingStateMachine.StopTagging();
                    }
                }
                else
                {
                    taggingStateMachine.StopTagging();
                }
            }

            // Update Scanning
            scanTimer += Time.deltaTime;
            if (scanTimer >= scanInterval)
            {
                ScanActiveObjects();
                scanTimer = 0f;
            }
        }
        public void ScanActiveObjects()
        {
            activeObjects.Clear();
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (GameObject root in rootObjects)
            {
                if (blacklistPaths.Contains(root.name)) continue;

                Stack<Transform> stack = new Stack<Transform>();
                stack.Push(root.transform);

                while (stack.Count > 0)
                {
                    Transform current = stack.Pop();
                    if (current.gameObject.activeInHierarchy)
                    {
                        if (current == transform || (Rig.Instance != null && current == Rig.Instance.body)) continue;
                        if (blacklistedObjects.Contains(current.gameObject)) continue;

                        bool isObstacle = obstaclePaths.Any(path => current.transform.IsChildOf(GameObject.Find(path)?.transform));
                        if (isObstacle && current.GetComponent<Collider>() != null)
                        {
                            activeObjects.Add(current.gameObject);
                        }

                        foreach (Transform child in current)
                        {
                            stack.Push(child);
                        }
                    }
                }
            }
        }
        #endregion
        #region Cleanup Methods
        private void CleanupResources()
        {
            if (lineRenderers != null)
            {
                if (lineRenderers.pathLine.GameObject != null) Destroy(lineRenderers.pathLine.GameObject);
                if (lineRenderers.directionLine.GameObject != null) Destroy(lineRenderers.directionLine.GameObject);
            }
            if (gunRayRenderer != null)
            {
                Destroy(gunRayRenderer.gameObject);
            }
            if (boxRenderers.Count > 0)
            {
                foreach (var renderer in boxRenderers)
                {
                    if (renderer != null)
                    {
                        Destroy(renderer.gameObject);
                    }
                }
                boxRenderers.Clear();
            }
        }
        private void SendLogs()
        {
            if (!string.IsNullOrEmpty(DiscordWebhookUrl.Value))
            {
                string logs = Logging.GetFullLogText();
                if (!string.IsNullOrEmpty(logs))
                {
                    GameObject senderObj = new GameObject("WebhookSender");
                    WebhookSender sender = senderObj.AddComponent<WebhookSender>();
                    sender.Initialize(DiscordWebhookUrl.Value, logs);
                }
            }
        }
        #endregion
        #region Movement
        #region Flee
        public void StartFleeing()
        {
            fleeStateMachine.StartFleeing();
            movementStateMachine.StartMoving();
            logger.LogInfo("Started fleeing from taggers.");
        }
        public void StopFleeing()
        {
            fleeStateMachine.StopFleeing();
            movementStateMachine.StopMoving();
            logger.LogInfo("Stopped fleeing.");
        }
        #endregion
        #region Tagging
        public void StartTagging()
        {
            taggingStateMachine.StartTagging();
            if (taggedPlayer != null)
            {
                logger.LogInfo($"Starting tag on player: {taggedPlayer.NickName}");
                movementStateMachine.StartMoving();
            }
            else
            {
                logger.LogWarning("No non-tagged players found to tag.");
            }
        }
        public void StopTagging()
        {
            taggingStateMachine.StopTagging();
            movementStateMachine.StopMoving();
            logger.LogInfo("Tagging stopped.");
        }
        #endregion
        #region Following
        public void ToggleFollowing(Player player)
        {
            if (currentPlayer == player && followPlayerEnabled)
            {
                followingStateMachine.StopFollowing();
                movementStateMachine.StopMoving();
            }
            else
            {
                followingStateMachine.StartFollowing(player);
                movementStateMachine.StartMoving();
                logger.LogInfo($"Starting Following on player: {player.NickName}");
                pathingStateMachine.StopPathing();
            }
            lineRenderers.pathLine.Renderer.enabled = followPlayerEnabled;
            lineRenderers.directionLine.Renderer.enabled = followPlayerEnabled;
        }
        public void StopFollowing()
        {
            followingStateMachine.StopFollowing();
            movementStateMachine.StopMoving();
            logger.LogInfo("Following stopped.");
        }
        #endregion
        #region Pathing
        public void StopPathing()
        {
            pathingStateMachine.StopPathing();
            movementStateMachine.StopMoving();
            logger.LogInfo("Pathing stopped.");
        }
        public void StartPathing()
        {
            if (lineRenderers.pathPositions.Count > 0)
            {
                pathingStateMachine.StartPathing();
                movementStateMachine.StartMoving();
                logger.LogInfo("Starting path following.");
            }
            else
            {
                logger.LogWarning("No path points to follow.");
            }
        }
        #endregion
        #region Hand
        public void PerformHand(bool isLeftHand)
        {
            StartCoroutine(HandRoutine(isLeftHand));
        }
        private IEnumerator HandRoutine(bool isLeftHand)
        {
            HandDriver hand = isLeftHand ? Rig.Instance.leftHand : Rig.Instance.rightHand;
            Transform body = Rig.Instance.body;
            Vector3 initialPosition = hand.transform.position;
            Vector3 targetPosition = body.position + Vector3.down * handGroundDistance;

            // Move hand down
            float timer = 0;
            while (timer < handDownDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / handDownDuration;
                hand.transform.position = Vector3.Lerp(initialPosition, targetPosition, progress);
                yield return null;
            }

            hand.transform.position = targetPosition;

            switch (activationPoint)
            {
                case PlayerFollowerGUI.ActivationPoint.OnReachGround:
                    SetButtonState(hand, true);
                    yield return new WaitForSeconds(buttonHoldDuration);
                    SetButtonState(hand, false);
                    break;

                case PlayerFollowerGUI.ActivationPoint.MidHold:
                    yield return new WaitForSeconds(buttonHoldDuration / 2);
                    SetButtonState(hand, true);
                    yield return new WaitForSeconds(buttonHoldDuration / 2);
                    SetButtonState(hand, false);
                    break;

                case PlayerFollowerGUI.ActivationPoint.OnRelease:
                    yield return new WaitForSeconds(buttonHoldDuration);
                    SetButtonState(hand, true);
                    break;
            }

            // Move hand up
            timer = 0;
            while (timer < handUpDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / handUpDuration;
                hand.transform.position = Vector3.Lerp(targetPosition, initialPosition, progress);
                yield return null;
            }

            hand.transform.position = initialPosition;

            if (activationPoint == PlayerFollowerGUI.ActivationPoint.OnRelease)
            {
                SetButtonState(hand, false);
            }
        }
        private void SetButtonState(HandDriver hand, bool state)
        {
            if ((selectedHandButton & PlayerFollowerGUI.HandButton.Grip) != 0) { hand.grip = state; }
            if ((selectedHandButton & PlayerFollowerGUI.HandButton.Trigger) != 0) { hand.trigger = state; }
            if ((selectedHandButton & PlayerFollowerGUI.HandButton.Primary) != 0) { hand.primary = state; }
            if ((selectedHandButton & PlayerFollowerGUI.HandButton.Secondary) != 0) { hand.secondary = state; }
        }
        #endregion
        public void UpdateMovement(Transform localBody, Transform targetTransform)
        {
            Vector3 directionToTarget = targetTransform.position - localBody.position;
            directionToTarget.y = 0f;
            float distanceToTarget = directionToTarget.magnitude;

            Vector3 finalDirection = avoidObjects ? AvoidObstacles(localBody.position, directionToTarget.normalized) : directionToTarget.normalized;

            if (distanceToTarget < TARGET_PROXIMITY_THRESHOLD)
            {
                StopMovement();
                return;
            }

            Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * finalDirection;
            StartMovement(localDirection);
            TurnTowardsTarget(targetTransform, localBody);
        }
        public void StopMovement()
        {
            movementStateMachine.StopMoving();
            InputHandler.inputDirectionNoY = Vector3.zero;
        }
        private void StartMovement(Vector3 localDirection)
        {
            movementStateMachine.StartMoving();
            InputHandler.inputDirectionNoY = new Vector3(localDirection.x, 0f, localDirection.z);
        }
        #endregion
        #region MovementUtils
        #region Jump
        public bool ShouldJump(Vector3 currentPosition, Vector3 targetPosition)
        {
            float heightDifference = targetPosition.y - currentPosition.y;
            return heightDifference > JUMP_HEIGHT_THRESHOLD;
        }
        public void TriggerJump()
        {
            if (walkAnimator != null)
            {
                Vector3 currentPosition = transform.position;
                walkAnimator.JumpPath(currentPosition, jumpTarget, walkAnimator.jumpAngleDegrees);
                isPreparingToJump = false;
            }
        }
        public List<Vector3> GenerateJumpArc(Vector3 start, Vector3 end, float jumpAngle)
        {
            List<Vector3> arcPoints = new List<Vector3>();
            Vector3 startPos = start;
            Vector3 endPos = end;

            Vector3 horizontalDisplacement = endPos - startPos;
            horizontalDisplacement.y = 0f;
            float D = horizontalDisplacement.magnitude;
            float y = endPos.y - startPos.y;
            float theta = jumpAngle * Mathf.Deg2Rad;
            float cosTheta = Mathf.Cos(theta);
            float sinTheta = Mathf.Sin(theta);
            float g = Mathf.Abs(Physics.gravity.y);

            float denominator = D * Mathf.Tan(theta) - y;
            int segments = 15;

            if (denominator <= 0 || D < 0.1f)
            {
                float requiredHeight = Mathf.Max(0, y + 1f);
                float jumpVelocity = Mathf.Sqrt(2 * g * requiredHeight);
                float timeUp = jumpVelocity / g;
                float totalTime = timeUp * 2;

                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float currentTime = t * totalTime;
                    float verticalPosition = jumpVelocity * currentTime - 0.5f * g * currentTime * currentTime;
                    Vector3 point = startPos + Vector3.up * verticalPosition;

                    if (D > 0.1f)
                    {
                        Vector3 horizontalDirection = horizontalDisplacement.normalized;
                        point += horizontalDirection * (D * t);
                    }
                    arcPoints.Add(point);
                }
            }
            else
            {
                float v = Mathf.Sqrt(g * D * D / (2 * cosTheta * cosTheta * denominator));
                Vector3 direction = horizontalDisplacement.normalized;
                float timeTotal = D / (v * cosTheta);

                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float currentTime = t * timeTotal;
                    float x = v * cosTheta * currentTime;
                    float currentY = v * sinTheta * currentTime - 0.5f * g * currentTime * currentTime;
                    Vector3 point = startPos + direction * x + Vector3.up * currentY;
                    arcPoints.Add(point);
                }
            }

            return arcPoints;
        }
        #endregion
        public (Transform target, Transform local) GetPlayerTransforms(Player player)
        {
            var targetRigObj = GorillaGameManager.instance.FindPlayerVRRig(player);
            if (targetRigObj == null)
            {
                logger.LogWarning($"No VRRig found for player: {player.NickName}");
<<<<<<< HEAD
                currentPlayer = null;
=======
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
                lineRenderers.DisableLineRenderers();
                return (null, null);
            }
            Transform localBody = Rig.Instance.body;
            if (localBody == null)
            {
                logger.LogWarning("Local player body not found.");
                lineRenderers.DisableLineRenderers();
                return (null, null);
            }

            return (targetRigObj.transform, localBody);
        }
        public void TurnTowardsTarget(Transform targetTransform, Transform localBody)
        {
            Vector3 targetDirection = targetTransform.position - localBody.position;
            targetDirection.y = 0f;

            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            float angleDifference = Quaternion.Angle(localBody.rotation, targetRotation);

            if (angleDifference > ROTATION_THRESHOLD)
            {
                float turnDirection = Vector3.Cross(localBody.forward, targetDirection).y < 0 ? -1 : 1;
                float turnSpeed = Mathf.Min(angleDifference, MAX_TURN_SPEED);
                float turnAmount = turnDirection * turnSpeed / TURN_SPEED_DIVISOR;

                GorillaLocomotion.GTPlayer.Instance.Turn(turnAmount);
            }
        }
        public void TurnTowardsTargetPosition(Transform localBody, Vector3 targetPoint)
        {
            Vector3 targetDirection = targetPoint - localBody.position;
            targetDirection.y = 0f;

            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            float angleDifference = Quaternion.Angle(localBody.rotation, targetRotation);

            if (angleDifference > ROTATION_THRESHOLD)
            {
                float turnDirection = Vector3.Cross(localBody.forward, targetDirection).y < 0 ? -1 : 1;
                float turnSpeed = Mathf.Min(angleDifference, MAX_TURN_SPEED);
                float turnAmount = turnDirection * turnSpeed / TURN_SPEED_DIVISOR;

                GorillaLocomotion.GTPlayer.Instance.Turn(turnAmount);
            }
        }
        public Vector3 AvoidObstaclesTaggers(Vector3 currentPosition, Vector3 desiredDirection)
        {
            Collider[] obstacles = Physics.OverlapSphere(currentPosition, avoidanceRadius, obstacleLayers);
            Vector3 avoidance = Vector3.zero;

            foreach (Collider col in obstacles)
            {
                Vector3 toObstacle = col.transform.position - currentPosition;
                float distance = toObstacle.magnitude;
                if (distance < avoidanceRadius)
                {
                    Vector3 avoidDir = -toObstacle.normalized;
                    avoidance += avoidDir * (avoidanceRadius - distance);
                }
            }

            if (avoidance != Vector3.zero)
            {
                avoidance.Normalize();
                desiredDirection = (desiredDirection + avoidance).normalized;
            }

            return desiredDirection;
        }
        public Vector3 AvoidObstacles(Vector3 currentPosition, Vector3 desiredDirection)
        {
            Vector3 headPosition = Rig.Instance.head.position;
            float forwardRayDistance = 4f;
            float backwardRayDistance = 0.7f;
            RaycastHit hit;

            if (Physics.Raycast(headPosition, desiredDirection, out hit, forwardRayDistance, obstacleLayers))
            {
                if (activeObjects.Contains(hit.collider.gameObject))
                {
                    Collider collider = hit.collider;
                    Vector3 closestPoint = collider.ClosestPoint(headPosition);
                    Vector3 avoidDirection = Vector3.Cross(desiredDirection, Vector3.up).normalized;

                    Vector3 rightDir = closestPoint + avoidDirection * 0.5f - headPosition;
                    Vector3 leftDir = closestPoint - avoidDirection * 0.5f - headPosition;

                    bool rightClear = !Physics.Raycast(headPosition, rightDir.normalized, 2f, obstacleLayers);
                    bool leftClear = !Physics.Raycast(headPosition, leftDir.normalized, 2f, obstacleLayers);

                    if (rightClear || !leftClear)
                    {
                        return (desiredDirection + avoidDirection * 2f).normalized;
                    }
                    else
                    {
                        return (desiredDirection - avoidDirection * 2f).normalized;
                    }
                }
            }

            Vector3[] directions =
            {
                desiredDirection,
                Quaternion.Euler(0, 45, 0) * desiredDirection,
                Quaternion.Euler(0, -45, 0) * desiredDirection,
                -desiredDirection
            };

            foreach (var dir in directions)
            {
                float rayDistance = dir == -desiredDirection ? backwardRayDistance : forwardRayDistance;

                if (Physics.Raycast(headPosition, dir, out hit, rayDistance, obstacleLayers))
                {
                    if (activeObjects.Contains(hit.collider.gameObject))
                    {
                        Collider collider = hit.collider;
                        Vector3 closestPoint = collider.ClosestPoint(headPosition);
                        Vector3 avoidDirection = Vector3.Cross(dir, Vector3.up).normalized;

                        Vector3 rightDir = closestPoint + avoidDirection * 0.5f - headPosition;
                        Vector3 leftDir = closestPoint - avoidDirection * 0.5f - headPosition;

                        return Vector3.Lerp(desiredDirection, desiredDirection + avoidDirection * 2f, 0.5f).normalized;
                    }
                }
            }

            return desiredDirection;
        }
        #region Collisions
        public void CheckCollisions()
        {
            if (!objectsInitialized)
            {
                collisionState = "Objects not initialized";
                return;
            }

            leftHandCollisions.Clear();
            rightHandCollisions.Clear();
            bool anyCollision = false;

            foreach (GameObject obj in activeObjects)
            {
                if (obj == null) continue;

                bool leftHand = IsTouchingObject(Rig.Instance.leftHand.gameObject, obj);
                bool rightHand = IsTouchingObject(Rig.Instance.rightHand.gameObject, obj);

                if (leftHand)
                {
                    leftHandCollisions[obj] = true;
                    anyCollision = true;
                }

                if (rightHand)
                {
                    rightHandCollisions[obj] = true;
                    anyCollision = true;
                }
            }

            UpdateCollisionState();
        }
        private bool IsTouchingObject(GameObject hand, GameObject other)
        {
            if (hand == null || other == null) return false;

            const float maxDistance = 0.3f;
            Vector3 handPosition = hand.transform.position;
            Vector3 otherPosition = other.transform.position;

            float distance = Vector3.Distance(handPosition, otherPosition);
            if (distance <= maxDistance)
            {
                return true;
            }

            Vector3 direction = otherPosition - handPosition;
            if (Physics.Raycast(handPosition, direction.normalized, out RaycastHit hit, maxDistance))
            {
                return hit.collider.gameObject == other || hit.transform.IsChildOf(other.transform);
            }

            return false;
        }
        private void UpdateCollisionState()
        {
            List<string> collisions = new List<string>();

            foreach (var collision in leftHandCollisions)
            {
                if (collision.Value)
                {
                    collisions.Add($"Left Hand → {collision.Key.name}");
                }
            }

            foreach (var collision in rightHandCollisions)
            {
                if (collision.Value)
                {
                    collisions.Add($"Right Hand → {collision.Key.name}");
                }
            }

            collisionState = collisions.Count > 0 ? string.Join(", ", collisions) : "No Collisions";
        }
        public void InitializeObjects()
        {
            try
            {
                if (Rig.Instance != null)
                {
                    ScanActiveObjects();
                    objectsInitialized = (activeObjects.Count > 0 && Rig.Instance.leftHand != null && Rig.Instance.rightHand != null);
                    
                    if (objectsInitialized)
                    {
                        logger.LogInfo($"Successfully initialized objects. Found {activeObjects.Count} active objects to track.");
                    }
                    else
                    {
                        logger.LogWarning("Not all objects could be found or initialized");
                    }
                }
                else
                {
                    logger.LogError("Rig.Instance is null");
                }
            }
            catch (Exception e)
            {
                logger.LogError("Error initializing objects: " + e.Message);
            }
        }
        #endregion
        #endregion
        #region Preset Management
        [Serializable]
        public class PathData
        {
            public List<Vector3> waypoints;
        }
        public class PathPreset
        {
            public string Name { get; set; }
            public List<Vector3> Waypoints { get; set; }

            public PathPreset(string name, List<Vector3> waypoints)
            {
                Name = name;
                Waypoints = new List<Vector3>(waypoints);
            }
        }
        private string GetPresetDirectory()
        {
            return Path.Combine(BepInEx.Paths.GameRootPath, "PlayerFollower", "Paths");
        }
        public void SavePreset(string presetName)
        {
            string presetDir = GetPresetDirectory();
            if (!Directory.Exists(presetDir)) { Directory.CreateDirectory(presetDir); }
            PathData data = new PathData { waypoints = lineRenderers.pathPositions };
            string json = JsonUtility.ToJson(data, true);
            string filePath = Path.Combine(presetDir, presetName + ".json");
            File.WriteAllText(filePath, json);
            logger.LogInfo($"Preset saved to {filePath}");
            RefreshPresetFiles();
        }
        public void LoadPreset(string presetName)
        {
            string filePath = Path.Combine(GetPresetDirectory(), presetName + ".json");
            if (!File.Exists(filePath))
            {
                logger.LogWarning($"Preset file not found: {filePath}");
                return;
            }
            string json = File.ReadAllText(filePath);
            PathData data = JsonUtility.FromJson<PathData>(json);
            if (data != null && data.waypoints != null)
            {
                lineRenderers.pathPositions = new List<Vector3>(data.waypoints);
                lineRenderers.UpdatePathLineRenderer();
                logger.LogInfo($"Preset loaded from {filePath}");
            }
        }
        public void RefreshPresetFiles()
        {
            string presetDir = GetPresetDirectory();
            if (!Directory.Exists(presetDir)) { Directory.CreateDirectory(presetDir); }
            gui.presetFiles = Directory.GetFiles(presetDir, "*.json");
        }
        public void LoadHardcodedPreset(string presetName)
        {
            if (gui.hardcodedPresets.TryGetValue(presetName, out PathPreset preset))
            {
                lineRenderers.pathPositions = new List<Vector3>(preset.Waypoints);
                lineRenderers.UpdatePathLineRenderer();
                logger.LogInfo($"Loaded hardcoded preset: {presetName}");
            }
            else
            {
                logger.LogWarning($"Hardcoded preset not found: {presetName}");
            }
        }
        public string GeneratePresetCode(string presetName)
        {
            string filePath = Path.Combine(GetPresetDirectory(), presetName + ".json");
            if (!File.Exists(filePath))
            {
                logger.LogWarning($"Preset file not found: {filePath}");
                return null;
            }

            string json = File.ReadAllText(filePath);
            PathData data = JsonUtility.FromJson<PathData>(json);

            if (data == null || data.waypoints == null || data.waypoints.Count == 0)
            {
                logger.LogWarning($"Invalid or empty preset data in file: {filePath}");
                return null;
            }

            StringBuilder codeBuilder = new StringBuilder();
            codeBuilder.AppendLine($"// Generated code for preset: {presetName}");
            codeBuilder.AppendLine("{");
            codeBuilder.AppendLine($"    \"{presetName}\",");
            codeBuilder.AppendLine($"    new PathPreset(\"{presetName}\", new List<Vector3>");
            codeBuilder.AppendLine("    {");

            foreach (Vector3 waypoint in data.waypoints)
            {
                codeBuilder.AppendLine($"        new Vector3({waypoint.x}f, {waypoint.y}f, {waypoint.z}f),");
            }

            codeBuilder.AppendLine("    })");
            codeBuilder.AppendLine("},");

            return codeBuilder.ToString();
        }
        public string GenerateAllPresetsCode()
        {
            StringBuilder fullCodeBuilder = new StringBuilder();
            fullCodeBuilder.AppendLine("// Generated code for all presets");
            fullCodeBuilder.AppendLine("hardcodedPresets = new Dictionary<string, PathPreset>");
            fullCodeBuilder.AppendLine("{");

            string presetDir = GetPresetDirectory();
            if (!Directory.Exists(presetDir))
            {
                logger.LogWarning("Preset directory not found");
                return null;
            }

            string[] presetFiles = Directory.GetFiles(presetDir, "*.json");
            foreach (string filePath in presetFiles)
            {
                string presetName = Path.GetFileNameWithoutExtension(filePath);
                string presetCode = GeneratePresetCode(presetName);
                if (presetCode != null)
                {
                    fullCodeBuilder.Append(presetCode);
                }
            }

            fullCodeBuilder.AppendLine("};");

            string codeFilePath = Path.Combine(presetDir, "GeneratedPresetCode.cs");
            File.WriteAllText(codeFilePath, fullCodeBuilder.ToString());
            logger.LogInfo($"Generated code saved to: {codeFilePath}");

            return fullCodeBuilder.ToString();
        }
        private void InitializeHardcodedPresets()
        {
            gui.hardcodedPresets = new Dictionary<string, PathPreset>
            {
                {
                    "GoToComputerRoomMain",
                    new PathPreset("GoToComputerRoomMain", new List<Vector3>
                    {
                        new Vector3(-72.35487f, 2.560988f, -79.96801f),
                        new Vector3(-71.72959f, 2.759356f, -79.41348f),
                        new Vector3(-70.8455f, 3.543792f, -78.74522f),
                        new Vector3(-70.10481f, 3.90039f, -78.35027f),
                        new Vector3(-69.17955f, 4.408913f, -78.16763f),
                        new Vector3(-68.50964f, 4.774348f, -78.00622f),
                        new Vector3(-67.88614f, 5.118788f, -77.99005f),
                        new Vector3(-67.03764f, 5.601111f, -77.89817f),
                        new Vector3(-66.18839f, 6.079124f, -77.80872f),
                        new Vector3(-65.59859f, 6.497375f, -77.6417f),
                        new Vector3(-64.80223f, 6.916295f, -78.01163f),
                        new Vector3(-63.74372f, 7.543442f, -78.52428f),
                        new Vector3(-62.82769f, 8.07548f, -79.17905f),
                        new Vector3(-61.8376f, 8.526892f, -79.95563f),
                        new Vector3(-61.56931f, 8.726213f, -80.31889f),
                        new Vector3(-61.29178f, 8.992293f, -81.18191f),
                        new Vector3(-60.82226f, 9.15828f, -81.49084f),
                        new Vector3(-60.68061f, 9.035937f, -81.14237f),
                        new Vector3(-60.89326f, 9.000212f, -80.95354f),
                        new Vector3(-61.27353f, 8.901952f, -80.49393f),
                        new Vector3(-61.76406f, 9.912112f, -80.32779f),
                        new Vector3(-62.04436f, 10.41658f, -80.22958f),
                        new Vector3(-62.28139f, 10.46475f, -80.14656f),
                        new Vector3(-62.77595f, 10.32142f, -79.8379f),
                        new Vector3(-63.18159f, 10.30063f, -79.14884f),
                    })
                },
                {
                    "GoToComputerRoomMain2",
                    new PathPreset("GoToComputerRoomMain2", new List<Vector3>
                    {
                        new Vector3(-72.35487f, 2.560988f, -79.96801f),
                        new Vector3(-71.72959f, 2.759356f, -79.41348f),
                        new Vector3(-70.8455f, 3.543792f, -78.74522f),
                        new Vector3(-70.10481f, 3.90039f, -78.35027f),
                        new Vector3(-69.17955f, 4.408913f, -78.16763f),
                        new Vector3(-68.50964f, 4.774348f, -78.00622f),
                        new Vector3(-67.88614f, 5.118788f, -77.99005f),
                        new Vector3(-67.03764f, 5.601111f, -77.89817f),
                        new Vector3(-66.18839f, 6.079124f, -77.80872f),
                        new Vector3(-65.59859f, 6.497375f, -77.6417f),
                        new Vector3(-64.80223f, 6.916295f, -78.01163f),
                        new Vector3(-63.74372f, 7.543442f, -78.52428f),
                        new Vector3(-62.82769f, 8.07548f, -79.17905f),
                        new Vector3(-61.8376f, 8.526892f, -79.95563f),
                        new Vector3(-61.56931f, 8.726213f, -80.31889f),
                        new Vector3(-61.29178f, 8.992293f, -81.18191f),
                        new Vector3(-60.82226f, 9.15828f, -81.49084f),
                        new Vector3(-60.68061f, 9.035937f, -81.14237f),
                        new Vector3(-60.89326f, 9.000212f, -80.95354f),
                        new Vector3(-61.27353f, 8.901952f, -80.49393f),
                        new Vector3(-61.76406f, 9.912112f, -80.32779f),
                        new Vector3(-62.04436f, 10.41658f, -80.22958f),
                        new Vector3(-62.28139f, 10.46475f, -80.14656f),
                        new Vector3(-62.77595f, 10.32142f, -79.8379f),
                        new Vector3(-63.18159f, 10.30063f, -79.14884f),
                        new Vector3(-63.81387f, 10.55995f, -78.89856f),
                        new Vector3(-65.01719f, 11.24964f, -78.40349f),
                        new Vector3(-65.71024f, 11.6533f, -78.54861f),
                        new Vector3(-66.68946f, 11.65255f, -78.89753f),
                        new Vector3(-66.81215f, 11.97877f, -79.1282f),
                        new Vector3(-66.65028f, 11.87572f, -79.81818f),
                        new Vector3(-66.46368f, 11.76486f, -80.35046f),
                        new Vector3(-66.37151f, 11.72333f, -80.68318f),
                    })
                },
                {
                    "GoToPlatformMain",
                    new PathPreset("GoToPlatformMain", new List<Vector3>
                    {
                        new Vector3(-56.02302f, 2.381461f, -85.00444f),
                        new Vector3(-55.38683f, 2.677924f, -84.37441f),
                        new Vector3(-54.26675f, 3.527431f, -83.40524f),
                        new Vector3(-52.69836f, 4.619273f, -82.34782f),
                        new Vector3(-52.17909f, 4.953619f, -82.08039f),
                        new Vector3(-51.61621f, 5.327015f, -81.75504f),
                        new Vector3(-50.50991f, 6.063376f, -81.11749f),
                        new Vector3(-49.42637f, 6.822853f, -80.36471f),
                        new Vector3(-48.79836f, 7.242524f, -80.00304f),
                        new Vector3(-48.34003f, 9.453851f, -79.73903f),
                        new Vector3(-47.33639f, 9.142305f, -79.16099f),
                        new Vector3(-46.53186f, 7.470709f, -78.681f),
                        new Vector3(-46.29971f, 7.393697f, -78.12136f),
                        new Vector3(-46.01013f, 7.386148f, -77.11805f),
                        new Vector3(-45.75963f, 7.388598f, -76.76421f),
                        new Vector3(-45.87177f, 7.390213f, -76.38636f),
                        new Vector3(-46.06187f, 8.231427f, -74.52164f),
                        new Vector3(-46.28636f, 8.711465f, -73.8747f),
                        new Vector3(-46.48201f, 9.363704f, -72.9858f),
                        new Vector3(-46.82495f, 10.077f, -72.0348f),
                        new Vector3(-47.12182f, 10.69032f, -71.21152f),
                        new Vector3(-47.38946f, 11.3769f, -70.29014f),
                        new Vector3(-47.7004f, 12.0322f, -69.42803f),
                        new Vector3(-48.05209f, 12.76431f, -68.4529f),
                        new Vector3(-48.16179f, 13.32679f, -67.66391f),
                        new Vector3(-47.6621f, 13.44138f, -67.3676f),
                        new Vector3(-47.19476f, 13.83556f, -66.68616f),
                        new Vector3(-47.40697f, 14.11036f, -65.27734f),
                        new Vector3(-48.86796f, 14.10696f, -64.1051f),
                    })
                },
            };
        }
        #endregion
        public void ShootGun()
        {
            if (!gunEnabled || Time.time - lastShotTime < 0.5f) return;

            lastShotTime = Time.time;
            Transform head = Rig.Instance.head;
            if (head == null) return;

            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, gunRange, gunLayers))
            {
                HashSet<GameObject> connectedObjects = new HashSet<GameObject>();
                Queue<GameObject> objectsToCheck = new Queue<GameObject>();
                objectsToCheck.Enqueue(hit.collider.gameObject);

                while (objectsToCheck.Count > 0)
                {
                    GameObject current = objectsToCheck.Dequeue();
                    if (connectedObjects.Contains(current)) continue;

                    connectedObjects.Add(current);
                    logger.LogInfo($"Hit object: {current.name}");

                    Joint[] joints = current.GetComponents<Joint>();
                    foreach (Joint joint in joints)
                    {
                        if (joint.connectedBody != null && !connectedObjects.Contains(joint.connectedBody.gameObject))
                        {
                            objectsToCheck.Enqueue(joint.connectedBody.gameObject);
                        }
                    }

                    Collider[] childColliders = current.GetComponentsInChildren<Collider>();
                    foreach (Collider childCollider in childColliders)
                    {
                        if (!connectedObjects.Contains(childCollider.gameObject))
                        {
                            objectsToCheck.Enqueue(childCollider.gameObject);
                        }
                    }

                    Transform parent = current.transform.parent;
                    while (parent != null)
                    {
                        Collider parentCollider = parent.GetComponent<Collider>();
                        if (parentCollider != null && !connectedObjects.Contains(parent.gameObject))
                        {
                            objectsToCheck.Enqueue(parent.gameObject);
                        }
                        parent = parent.parent;
                    }
                }

                if (gunRayRenderer != null)
                {
                    gunRayRenderer.SetPosition(0, head.position);
                    gunRayRenderer.SetPosition(1, hit.point);
                    gunRayRenderer.enabled = true;
                    StartCoroutine(DisableGunRayAfterDelay());
                }
            }
            else
            {
                if (gunRayRenderer != null)
                {
                    gunRayRenderer.SetPosition(0, head.position);
                    gunRayRenderer.SetPosition(1, head.position + ray.direction * gunRange);
                    gunRayRenderer.enabled = true;
                    StartCoroutine(DisableGunRayAfterDelay());
                }
            }
        }

        // todo: use the box to blacklist areas for the bot cant go
        public void SetBoxPoint1()
        {
            if (!boxShootingMode) return;

            Transform head = Rig.Instance.head;
            if (head == null) return;

<<<<<<< HEAD
            //firstShotPoint = head.position;
            firstShotPoint = new Vector3(head.position.x, head.position.y - 2, head.position.z);
=======
            firstShotPoint = head.position;
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
            logger.LogInfo($"First box point set at: {firstShotPoint}");
        }
        public void SetBoxPoint2()
        {
            if (!boxShootingMode || !firstShotPoint.HasValue) return;

            Transform head = Rig.Instance.head;
            if (head == null) return;

            Vector3 secondPoint = head.position;
<<<<<<< HEAD
            Vector3 secondPointNEW = new Vector3(secondPoint.x, secondPoint.y - 2, secondPoint.z);
=======
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
            CreateBox(firstShotPoint.Value, secondPoint);
            firstShotPoint = null;
        }
        private void CreateBox(Vector3 point1, Vector3 point2)
        {
            Vector3 min = new Vector3( Mathf.Min(point1.x, point2.x), Mathf.Min(point1.y, point2.y), Mathf.Min(point1.z, point2.z) );
            Vector3 max = new Vector3(Mathf.Max(point1.x, point2.x), Mathf.Max(point1.y, point2.y), Mathf.Max(point1.z, point2.z));

            Vector3[] vertices = new Vector3[8];
            vertices[0] = new Vector3(min.x, min.y, min.z); // Bottom front left
            vertices[1] = new Vector3(max.x, min.y, min.z); // Bottom front right
            vertices[2] = new Vector3(max.x, min.y, max.z); // Bottom back right
            vertices[3] = new Vector3(min.x, min.y, max.z); // Bottom back left
            vertices[4] = new Vector3(min.x, max.y, min.z); // Top front left
            vertices[5] = new Vector3(max.x, max.y, min.z); // Top front right
            vertices[6] = new Vector3(max.x, max.y, max.z); // Top back right
            vertices[7] = new Vector3(min.x, max.y, max.z); // Top back left

            int[,] edges = new int[12, 2] {
                {0, 1}, // Bottom front
                {1, 2}, // Bottom right
                {2, 3}, // Bottom back
                {3, 0}, // Bottom left
                {4, 5}, // Top front
                {5, 6}, // Top right
                {6, 7}, // Top back
                {7, 4}, // Top left
                {0, 4}, // Left front
                {1, 5}, // Right front
                {2, 6}, // Right back
                {3, 7}  // Left back
            };

            for (int i = 0; i < BOX_EDGES; i++)
            {
                LineRenderer edgeRenderer = boxRenderers[i];
                edgeRenderer.SetPosition(0, vertices[edges[i, 0]]);
                edgeRenderer.SetPosition(1, vertices[edges[i, 1]]);
                edgeRenderer.enabled = true;
            }

            logger.LogInfo("Box Vertices:");
            for (int i = 0; i < vertices.Length; i++)
            {
                logger.LogInfo($"Vertex {i}: {vertices[i]}");
            }

            StartCoroutine(DisableBoxAfterDelay());
        }
        private IEnumerator DisableBoxAfterDelay()
        {
            yield return new WaitForSeconds(boxDisplayDuration);
            foreach (var renderer in boxRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }
        private IEnumerator DisableGunRayAfterDelay()
        {
            yield return new WaitForSeconds(gunRayDuration);
            if (gunRayRenderer != null)
            {
                gunRayRenderer.enabled = false;
            }
        }
    }
}