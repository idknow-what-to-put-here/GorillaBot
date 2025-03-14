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
using UnityEngine.Networking;
using System.Text;
using BepInEx.Configuration;
using static GorillaBot.WalkSimulator.Bot.PlayerFollower;
using UnityEngine.InputSystem.XR;
using System.Xml;
using Newtonsoft.Json;
using static OVRPlugin;
using static UnityEngine.UI.DefaultControls;
using UnityEngine.AI;
using WalkSimulator;
using static GorillaBot.WalkSimulator.Bot.PlayerFollowerUtils;
using System.Threading.Tasks;

namespace GorillaBot.WalkSimulator.Bot
{
    public class PlayerFollower : MonoBehaviour
    {
        private PlayerFollowerGUI gui;
        public DashboardServer dashboardServer;
        public MovementRecorder movementRecorder;
        public ConfigEntry<string> DiscordWebhookUrl;

        private const float TARGET_PROXIMITY_THRESHOLD = 0.5f;
        private const float MAX_TURN_SPEED = 15f;
        private const float TURN_SPEED_DIVISOR = 10f;
        private const float ROTATION_THRESHOLD = 1f;
        private const float JUMP_HEIGHT_THRESHOLD = 0.6f;
        private const float JUMP_PREPARATION_DISTANCE = 1.2f;
        //private const float WAYPOINT_UPDATE_INTERVAL = 5.0f;

        public bool followPlayerEnabled;
        public bool followPathEnabled;
        public Player currentPlayer;
        public WalkAnimator walkAnimator;
        public ManualLogSource logger;
        public LineRenderers lineRenderers;
        private List<Vector3> pathPositions;

        // Line configuration
        public Color pathColor = Color.red;
        public Color directionColor = Color.yellow;
        public float lineAlpha = 1f;
        public float pathLineWidth = 0.05f;
        public float directionLineWidth = 0.05f;
        public bool showPath = true;
        public bool showDirection = true;

        // Jump
        private bool isPreparingToJump = false;
        public Vector3 jumpTarget;
        public bool waitingForJumpStart = true;
        public Vector3 jumpWaypointStart;

        // Tagging
        public bool isTagging = false;
        private float tagDuration = 60f;
        public float tagTimer = 0f;
        public Player taggedPlayer;

        // Hand
        public PlayerFollowerGUI.HandButton selectedHandButton = PlayerFollowerGUI.HandButton.Grip;
        public PlayerFollowerGUI.ActivationPoint activationPoint = PlayerFollowerGUI.ActivationPoint.OnReachGround;
        public float handDownDuration = 0.5f;
        public float buttonHoldDuration = 1.0f;
        public float handUpDuration = 0.5f;
        public float handGroundDistance = 0.5f;

        // Object Scanning
        public bool avoidObjects = false;
        public List<GameObject> activeObjects = new List<GameObject>();
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
        };

        // Flee
        public bool fleeEnabled;
        public float FLEE_RADIUS = 10f;

        // NavMesh
        private NavMeshData navMeshData;
        private NavMeshDataInstance navMeshInstance;
        private List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        private Bounds bounds;
        public bool isSelectingPathPoints = false;
        public List<Vector3> selectedPoints = new List<Vector3>();

        private void Awake()
        {
            gui = new PlayerFollowerGUI(this);
            movementRecorder = new MovementRecorder(this);

            // Initialize Components
            logger = BepInEx.Logging.Logger.CreateLogSource("WalkSimulator");
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

            string presetDir = GetPresetDirectory();
            if (!Directory.Exists(presetDir))
            {
                Directory.CreateDirectory(presetDir);
                logger.LogInfo($"Created preset directory: {presetDir}");
            }
            //RefreshPresetFiles();

            // Initialize Animator
            if (Rig.Instance.Animator == null || !(Rig.Instance.Animator is WalkAnimator))
            {
                Rig.Instance.Animator = new GameObject("WalkAnimator").AddComponent<WalkAnimator>();
            }
            walkAnimator = (WalkAnimator)Rig.Instance.Animator;

            // Initialize LineRenderers
            GameObject lrObj = new GameObject("LineRenderers");
            lineRenderers = lrObj.AddComponent<LineRenderers>();
            lineRenderers.Initialize("PathLine", "DirectionLine", pathColor, directionColor, lineAlpha, pathLineWidth, directionLineWidth);

            pathPositions = new List<Vector3>();

            InitializeHardcodedPresets();


            dashboardServer = new DashboardServer(this);
            dashboardServer.Initialize();
        }
        public void ScanActiveObjects()
        {
            activeObjects.Clear();
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (GameObject root in rootObjects)
            {
                Stack<Transform> stack = new Stack<Transform>();
                stack.Push(root.transform);

                while (stack.Count > 0)
                {
                    Transform current = stack.Pop();
                    if (current.gameObject.activeInHierarchy)
                    {
                        if (current == transform || current == Rig.Instance.body) continue;

                        bool isObstacle = obstaclePaths.Any(path => current.transform.IsChildOf(GameObject.Find(path)?.transform));

                        if (isObstacle)
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
        private void OnGUI()
        {
            gui.OnGUI();
        }
        private void FixedUpdate()
        {
            movementRecorder.FixedUpdate();
            Transform localBody = Rig.Instance.body;

            /*
            if (Time.time - lastWaypointUpdateTime >= WAYPOINT_UPDATE_INTERVAL)
            {
                UpdateWaypoints();
                lastWaypointUpdateTime = Time.time;
            }
            */
            if (fleeEnabled)
            {
                FleeFromTaggers();
            }
            if (followPlayerEnabled && currentPlayer != null)
            {
                //MoveToTargetPlayerWithWaypoints();
                MoveToTargetPlayer();
            }
            else if (followPathEnabled && lineRenderers.pathPositions.Count > 0)
            {
                MoveAlongPath();
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

            if (isTagging)
            {
                if (taggedPlayer != null)
                {
                    //MoveToTaggedPlayerWithWaypoints();
                    MoveToTaggedPlayer();
                    tagTimer -= Time.fixedDeltaTime;
                    if (tagTimer <= 0)
                    {
                        StopTagging();
                    }
                }
                else
                {
                    StopTagging();
                }
            }

            scanTimer += Time.deltaTime;
            if (scanTimer >= scanInterval)
            {
                ScanActiveObjects();
                scanTimer = 0f;
            }
        }
        private void OnDestroy()
        {
            if (lineRenderers != null)
            {
                if (lineRenderers.pathLine.GameObject != null) Destroy(lineRenderers.pathLine.GameObject);
                if (lineRenderers.directionLine.GameObject != null) Destroy(lineRenderers.directionLine.GameObject);
            }
            if (dashboardServer != null)
            {
                dashboardServer.Shutdown();
            }
            /*
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
            */
        }
        private void OnApplicationQuit()
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
        #region Movement
        #region Tagging
        public void StartTagging()
        {
            taggedPlayer = GetRandomNonTaggedPlayer();

            if (taggedPlayer != null)
            {
                isTagging = true;
                tagTimer = tagDuration;
                logger.LogInfo($"Starting tag on player: {taggedPlayer.NickName}");
            }
            else
            {
                logger.LogWarning("No non-tagged players found to tag.");
            }
        }
        public void StopTagging()
        {
            isTagging = false;
            taggedPlayer = null;
            StopMovement();
            logger.LogInfo("Tagging stopped.");
        }
        private void MoveToTaggedPlayer()
        {
            if (taggedPlayer == null)
            {
                StopTagging();
                return;
            }
            var (targetTransform, localBody) = GetPlayerTransforms(taggedPlayer);
            if (targetTransform == null || localBody == null) { return; }
            lineRenderers.UpdateLineRenderers(localBody, targetTransform);
            UpdateMovement(localBody, targetTransform);
        }
        public Player GetRandomNonTaggedPlayer()
        {
            Player[] playerList = PhotonNetwork.PlayerList;

            if (playerList == null || playerList.Length == 0)
            {
                return null;
            }

            List<Player> availablePlayers = new List<Player>();

            foreach (Player player in playerList)
            {
                var vrrig = GorillaGameManager.instance.FindPlayerVRRig(player);
                if (vrrig != null && !PlayerIsTagged(vrrig) && player != PhotonNetwork.LocalPlayer)
                {
                    availablePlayers.Add(player);
                }
            }

            if (availablePlayers.Count == 0)
            {
                return null;
            }

            return availablePlayers[UnityEngine.Random.Range(0, availablePlayers.Count)];
        }
        public Player GetRandomPlayer(bool includeSelf)
        {
            Player[] playerList = PhotonNetwork.PlayerList;

            if (playerList == null || playerList.Length == 0)
            {
                return null;
            }

            List<Player> availablePlayers = new List<Player>(playerList);

            if (!includeSelf)
            {
                availablePlayers.Remove(PhotonNetwork.LocalPlayer);
            }

            if (availablePlayers.Count == 0)
            {
                return null;
            }

            return availablePlayers[UnityEngine.Random.Range(0, availablePlayers.Count)];
        }
        public static bool PlayerIsTagged(VRRig who)
        {
            if (who == null || who.mainSkin == null || who.mainSkin.material == null)
            {
                return false;
            }

            string text = who.mainSkin.material.name.ToLower();
            return text.Contains("fected") || text.Contains("it") || text.Contains("stealth") || !who.nameTagAnchor.activeSelf;
        }
        public static List<NetPlayer> InfectedList()
        {
            List<NetPlayer> list = new List<NetPlayer>();
            string text = GorillaGameManager.instance.GameModeName().ToLower();
            if (text.Contains("infection") || text.Contains("tag"))
            {
                GorillaTagManager component = GameObject.Find("GT Systems/GameModeSystem/Gorilla Tag Manager").GetComponent<GorillaTagManager>();
                bool isCurrentlyTag = component.isCurrentlyTag;
                if (isCurrentlyTag)
                {
                    list.Add(component.currentIt);
                }
                else
                {
                    foreach (NetPlayer netPlayer in component.currentInfected)
                    {
                        list.Add(netPlayer);
                    }
                }
            }
            return list;
        }
        #endregion
        #region Following
        public void ToggleFollowing(Player player)
        {
            followPathEnabled = false;
            if (currentPlayer == player && followPlayerEnabled)
            {
                StopFollowing();
            }
            else
            {
                followPlayerEnabled = true;
                currentPlayer = player;
                logger.LogInfo($"Starting Following on player: {player.NickName}");
                StopPathing();
            }
            lineRenderers.pathLine.Renderer.enabled = followPlayerEnabled;
            lineRenderers.directionLine.Renderer.enabled = followPlayerEnabled;
        }
        private void MoveToTargetPlayer()
        {
            var (targetTransform, localBody) = GetPlayerTransforms(currentPlayer);
            if (targetTransform == null || localBody == null) return;
            lineRenderers.UpdateLineRenderers(localBody, targetTransform);
            UpdateMovement(localBody, targetTransform);
        }
        public void StopFollowing()
        {
            followPlayerEnabled = false;
            currentPlayer = null;
            lineRenderers.DisableLineRenderers();
            logger.LogInfo("Following stopped.");
        }
        #endregion
        #region Pathing
        private void MoveAlongPath()
        {
            Transform localBody = Rig.Instance.body;
            if (localBody == null) return;
            Vector3 targetPoint = lineRenderers.pathPositions[0];
            lineRenderers.UpdateLineRenderersForPath(localBody, targetPoint);
            Vector3 directionToTarget = targetPoint - localBody.position;
            directionToTarget.y = 0f;
            float distanceToTarget = directionToTarget.magnitude;
            if (ShouldJump(localBody.position, targetPoint))
            {
                if (!isPreparingToJump)
                {
                    isPreparingToJump = true;
                    jumpTarget = targetPoint;
                }
                if (distanceToTarget <= JUMP_PREPARATION_DISTANCE)
                {
                    TriggerJump();
                }
            }
            if (distanceToTarget < TARGET_PROXIMITY_THRESHOLD)
            {
                lineRenderers.pathPositions.RemoveAt(0);
                if (lineRenderers.pathPositions.Count == 0)
                {
                    StopMovement();
                    followPathEnabled = false;
                    return;
                }
                isPreparingToJump = false;
                targetPoint = lineRenderers.pathPositions[0];
                directionToTarget = targetPoint - localBody.position;
                directionToTarget.y = 0f;
                distanceToTarget = directionToTarget.magnitude;
            }
            Vector3 finalDirection = avoidObjects ? AvoidObstacles(localBody.position, directionToTarget.normalized) : directionToTarget.normalized;
            Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * finalDirection;
            StartMovement(localDirection);
            TurnTowardsTargetPosition(localBody, targetPoint);
        }
        public void StopPathing()
        {
            followPathEnabled = false;
            InputHandler.inputDirectionNoY = Vector3.zero;
            Transform localBody = Rig.Instance.body;
            lineRenderers.ClearPath(localBody);
            logger.LogInfo("Pathing stopped.");
            Task.Run(async () => await dashboardServer.UpdateServerState());
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
        private void FleeFromTaggers()
        {
            Transform localBody = Rig.Instance.body;
            if (localBody == null) return;

            List<Vector3> taggedPlayerPositions = new List<Vector3>();
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                var vrrig = GorillaGameManager.instance.FindPlayerVRRig(player);
                if (vrrig != null && PlayerIsTagged(vrrig))
                {
                    taggedPlayerPositions.Add(vrrig.transform.position);
                }
            }

            if (taggedPlayerPositions.Count == 0)
            {
                StopMovement();
                return;
            }

            Vector3 fleeDirection = Vector3.zero;
            foreach (Vector3 pos in taggedPlayerPositions)
            {
                Vector3 toTagger = localBody.position - pos;
                float distance = toTagger.magnitude;
                if (distance < FLEE_RADIUS)
                {
                    fleeDirection += toTagger.normalized * (1 / Mathf.Max(distance, 0.1f));
                }
            }

            if (fleeDirection == Vector3.zero)
            {
                StopMovement();
                return;
            }

            fleeDirection.Normalize();
            fleeDirection = AvoidObstaclesTaggers(localBody.position, fleeDirection);

            Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * fleeDirection;
            lineRenderers.UpdateLineRenderersForPath(localBody, fleeDirection);
            StartMovement(localDirection);
            TurnTowardsTargetPosition(localBody, localBody.position + fleeDirection);
        }
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

            //lineRenderers.UpdateLineRenderersForPath(localBody, localBody.position + finalDirection * 5f);

            Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * finalDirection;
            //Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * directionToTarget.normalized;
            StartMovement(localDirection);
            TurnTowardsTarget(targetTransform, localBody);
        }
        private void StopMovement()
        {
            walkAnimator.enabled = false;
            InputHandler.inputDirectionNoY = Vector3.zero;
        }
        private void StartMovement(Vector3 localDirection)
        {
            walkAnimator.enabled = true;
            InputHandler.inputDirectionNoY = new Vector3(localDirection.x, 0f, localDirection.z);
        }
        #endregion
        #region MovementUtils
        #region Jump
        private bool ShouldJump(Vector3 currentPosition, Vector3 targetPosition)
        {
            float heightDifference = targetPosition.y - currentPosition.y;
            return heightDifference > JUMP_HEIGHT_THRESHOLD;
        }
        private void TriggerJump()
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
            //logger.LogInfo($"Got Players Pos");

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

                GorillaLocomotion.Player.Instance.Turn(turnAmount);
            }
        }
        private void TurnTowardsTargetPosition(Transform localBody, Vector3 targetPoint)
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

                GorillaLocomotion.Player.Instance.Turn(turnAmount);
            }
        }
        private Vector3 AvoidObstaclesTaggers(Vector3 currentPosition, Vector3 desiredDirection)
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
        private Vector3 AvoidObstacles(Vector3 currentPosition, Vector3 desiredDirection)
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
            /*
            gui.hardcodedPresets = new Dictionary<string, PathPreset>
            {
                {
                    "test",
                    new PathPreset("test", new List<Vector3>
                    {
                        new Vector3(10f, 0f, 10f),
                        new Vector3(20f, 0f, 10f),
                        new Vector3(20f, 0f, 20f),
                        new Vector3(10f, 0f, 20f)
                    })
                },
            };
            */
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
        public void PathfindTo(Vector3 destination)
        {
            if (!isSelectingPathPoints)
            {
                BuildNavMesh();
                isSelectingPathPoints = true;
                selectedPoints.Clear();
                selectedPoints.Add(GorillaLocomotion.Player.Instance.bodyCollider.transform.position);
                logger.LogInfo("Started path point selection. Click points to create path");

                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        selectedPoints.AddRange(path.corners);
                        lineRenderers.pathPositions = new List<Vector3>(selectedPoints);
                        lineRenderers.UpdatePathLineRenderer();
                        logger.LogInfo("Initial path calculated. Modify if needed.");
                    }
                }
            }
            else
            {
                isSelectingPathPoints = false;
                selectedPoints.Add(destination);

                lineRenderers.pathPositions = new List<Vector3>(selectedPoints);
                lineRenderers.UpdatePathLineRenderer();
                followPathEnabled = true;
                logger.LogInfo("Path created through selected points.");
            }
        }
        public void AddPathPoint(Vector3 point)
        {
            if (isSelectingPathPoints)
            {
                selectedPoints.Add(point);
                lineRenderers.pathPositions = new List<Vector3>(selectedPoints);
                lineRenderers.UpdatePathLineRenderer();
                logger.LogInfo($"Added path point: {point}");
            }
        }
        private void BuildNavMesh()
        {
            var colliders = FindObjectsOfType<Collider>();
            sources.Clear();
            bounds = new Bounds();

            activeObjects.Clear();

            foreach (var collider in colliders)
            {
                if (!collider.enabled) continue;

                if (!activeObjects.Contains(collider.gameObject))
                {
                    activeObjects.Add(collider.gameObject);
                }

                NavMeshBuildSource source = new NavMeshBuildSource();

                if (collider is BoxCollider box)
                {
                    source.shape = NavMeshBuildSourceShape.Box;
                    source.size = Vector3.Scale(box.size, box.transform.lossyScale);
                    source.transform = Matrix4x4.TRS(box.transform.TransformPoint(box.center), box.transform.rotation, Vector3.one);
                }
                else if (collider is SphereCollider sphere)
                {
                    source.shape = NavMeshBuildSourceShape.Sphere;
                    float scale = Mathf.Max(sphere.transform.lossyScale.x, sphere.transform.lossyScale.y, sphere.transform.lossyScale.z);
                    source.size = new Vector3(sphere.radius * 2 * scale, sphere.radius * 2 * scale, sphere.radius * 2 * scale);
                    source.transform = Matrix4x4.TRS(sphere.transform.TransformPoint(sphere.center), sphere.transform.rotation, Vector3.one);
                }
                else if (collider is CapsuleCollider capsule)
                {
                    source.shape = NavMeshBuildSourceShape.Capsule;
                    float scale = Mathf.Max(capsule.transform.lossyScale.x, capsule.transform.lossyScale.z);
                    source.size = new Vector3(capsule.radius * 2 * scale, capsule.height * capsule.transform.lossyScale.y, capsule.radius * 2 * scale);
                    source.transform = Matrix4x4.TRS(capsule.transform.TransformPoint(capsule.center), capsule.transform.rotation, Vector3.one);
                }
                else if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
                {
                    try
                    {
                        source.shape = NavMeshBuildSourceShape.Box;
                        var bounds = meshCollider.bounds;
                        source.size = bounds.size;
                        source.transform = Matrix4x4.TRS(bounds.center, meshCollider.transform.rotation, Vector3.one);
                        var topSource = new NavMeshBuildSource
                        {
                            shape = NavMeshBuildSourceShape.Box,
                            size = new Vector3(bounds.size.x, 0.1f, bounds.size.z),
                            transform = Matrix4x4.TRS(
                                new Vector3(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z),
                                meshCollider.transform.rotation,
                                Vector3.one
                            ),
                            area = 0
                        };
                        sources.Add(topSource);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning($"Failed to process MeshCollider {meshCollider.name}: {e.Message}");
                        continue;
                    }
                }
                else
                {
                    continue;
                }

                source.area = 0;
                sources.Add(source);
                bounds.Encapsulate(collider.bounds);
            }
            avoidObjects = true;
            bounds.Expand(10.0f);
            var buildSettings = NavMesh.GetSettingsByID(0);
            buildSettings.agentHeight = 2.0f;
            buildSettings.agentRadius = 1.0f;
            buildSettings.agentClimb = 0.5f;
            buildSettings.minRegionArea = 0.1f;
            buildSettings.overrideVoxelSize = true;
            buildSettings.voxelSize = 0.17f;

            navMeshData = NavMeshBuilder.BuildNavMeshData(buildSettings, sources, bounds, Vector3.zero, Quaternion.identity
            );

            if (navMeshData != null)
            {
                if (navMeshInstance.valid) { navMeshInstance.Remove(); }
                navMeshInstance = NavMesh.AddNavMeshData(navMeshData);
                logger.LogInfo("NavMesh built successfully with object avoidance enabled");
            }
            else
            {
                logger.LogError("Failed to build NavMesh");
            }
        }
    }
}