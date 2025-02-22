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

namespace WalkSimulator
{
    public class PlayerFollower : MonoBehaviour
    {
        private PlayerFollowerGUI gui;
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

        private void Awake()
        {
            gui = new PlayerFollowerGUI(this);

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
            var (targetTransform, localBody) = GetPlayerTransforms();
            if (targetTransform == null || localBody == null) return;
            lineRenderers.UpdateLineRenderers(localBody, targetTransform);
            UpdateMovement(localBody, targetTransform);
        }
        private void StopFollowing()
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
        private void UpdateMovement(Transform localBody, Transform targetTransform)
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

            float denominator = (D * Mathf.Tan(theta) - y);
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
                float v = Mathf.Sqrt((g * D * D) / (2 * cosTheta * cosTheta * denominator));
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
        public (Transform target, Transform local) GetPlayerTransforms()
        {
            return GetPlayerTransforms(currentPlayer);
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

                    Vector3 rightDir = (closestPoint + avoidDirection * 0.5f) - headPosition;
                    Vector3 leftDir = (closestPoint - avoidDirection * 0.5f) - headPosition;

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

                        Vector3 rightDir = (closestPoint + avoidDirection * 0.5f) - headPosition;
                        Vector3 leftDir = (closestPoint - avoidDirection * 0.5f) - headPosition;

                        return Vector3.Lerp(desiredDirection, desiredDirection + avoidDirection * 2f, 0.5f).normalized;
                    }
                }
            }

            return desiredDirection;
        }
        #endregion
        #region Preset Management
        [System.Serializable]
        public class PathData
        {
            public List<Vector3> waypoints;
        }
        private string GetPresetDirectory()
        {
            return Path.Combine(BepInEx.Paths.GameRootPath, "PlayerFollower");
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
        #endregion
    }
    public class PlayerFollowerGUI : MonoBehaviour
    {
        private readonly PlayerFollower follower;

        // GUI Configuration
        private const int WINDOW_WIDTH = 350;
        private const int WINDOW_HEIGHT = 450;
        private const int COLOR_PICKER_SIZE = 200;
        private Rect windowRect = new Rect(20, 20, WINDOW_WIDTH, WINDOW_HEIGHT);
        private bool showColorPicker = false;
        private bool editingPathColor = true;
        private Vector2 scrollPosition;

        // Preset colors
        private readonly Color[] colorPresets = new Color[]
        {
            Color.red, Color.green, Color.blue, Color.yellow, Color.cyan,
            Color.magenta, Color.white, new Color(1f, 0.5f, 0f), // Orange
            new Color(0.5f, 0f, 1f), // Purple
            new Color(0f, 1f, 0.5f)  // Mint
        };

        // Tab management
        private int selectedTab = 0;
        private readonly string[] tabNames = new string[] { "Line Settings", "Players", "Path Destinations", "Misc", "Logs" };

        // Preset management
        private string presetName = "DefaultPreset";
        public string[] presetFiles;
        private Vector2 presetScrollPosition;

        // Loging
        public static List<string> logMessages = new List<string>();
        private Vector2 logScrollPosition;

        // Styling
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle sliderLabelStyle;
        private Color originalColorWhenPickerOpened;

        // Hand
        [System.Flags]
        public enum HandButton
        {
            Grip = 1 << 0, // 1
            Trigger = 1 << 1, // 2
            Primary = 1 << 2, // 4
            Secondary = 1 << 3  // 8
        }
        public enum ActivationPoint { OnReachGround, MidHold, OnRelease }
        private bool isLeftHanded = true;

        public PlayerFollowerGUI(PlayerFollower follower)
        {
            this.follower = follower;
        }
        public void AddLogMessage(string message)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            logMessages.Add($"[{timestamp}] {message}");
        }
        public void OnGUI()
        {
            follower.RefreshPresetFiles();

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            sectionStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };

            sliderLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };

            windowRect = GUILayout.Window(0, windowRect, DrawWindow, "Player Follower", GUI.skin.window);

            if (showColorPicker)
            {
                Rect colorPickerRect = new Rect(
                    windowRect.x + windowRect.width + 10,
                    windowRect.y,
                    COLOR_PICKER_SIZE,
                    COLOR_PICKER_SIZE
                );
                GUILayout.Window(1, colorPickerRect, DrawColorPicker, "Color Picker");
            }
        }
        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0: DrawLineSettings(); break;
                case 1: DrawPlayerList(); break;
                case 2: DrawPathDestinations(); break;
                case 3: DrawMisc(); break;
                case 4: DrawLogsTab(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        private void DrawLineSettings()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Line Settings", headerStyle);

            // Path Line Settings
            GUILayout.BeginVertical(GUI.skin.box);
            follower.showPath = GUILayout.Toggle(follower.showPath, "Show Path Line");
            if (follower.showPath)
            {
                if (GUILayout.Button($"Path Color: #{follower.lineRenderers.ColorToHex(follower.pathColor)}"))
                {
                    showColorPicker = true;
                    editingPathColor = true;
                    originalColorWhenPickerOpened = follower.pathColor;
                }
                GUILayout.Label($"Path Width: {follower.pathLineWidth:F2}", sliderLabelStyle);
                follower.pathLineWidth = GUILayout.HorizontalSlider(follower.pathLineWidth, 0.01f, 0.2f);
                follower.lineRenderers.pathLine.Renderer.startWidth = follower.pathLineWidth;
                follower.lineRenderers.pathLine.Renderer.endWidth = follower.pathLineWidth;
            }
            GUILayout.EndVertical();

            // Direction Line Settings
            GUILayout.BeginVertical(GUI.skin.box);
            follower.showDirection = GUILayout.Toggle(follower.showDirection, "Show Direction Line");
            if (follower.showDirection)
            {
                if (GUILayout.Button($"Direction Color: #{follower.lineRenderers.ColorToHex(follower.directionColor)}"))
                {
                    showColorPicker = true;
                    editingPathColor = false;
                    originalColorWhenPickerOpened = follower.directionColor;
                }
                GUILayout.Label($"Direction Width: {follower.directionLineWidth:F2}", sliderLabelStyle);
                follower.directionLineWidth = GUILayout.HorizontalSlider(follower.directionLineWidth, 0.01f, 0.2f);
                follower.lineRenderers.directionLine.Renderer.startWidth = follower.directionLineWidth;
                follower.lineRenderers.directionLine.Renderer.endWidth = follower.directionLineWidth;
            }
            GUILayout.EndVertical();

            // Global Line Settings
            GUILayout.Label($"Line Transparency: {follower.lineAlpha:F2}", sliderLabelStyle);
            follower.lineAlpha = GUILayout.HorizontalSlider(follower.lineAlpha, 0f, 1f);
            follower.lineRenderers.lineAlpha = follower.lineAlpha;
            follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
            GUILayout.EndVertical();

            DrawColorPresets();
        }
        private void DrawColorPresets()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Color Presets", headerStyle);

            const int presetsPerRow = 5;
            for (int i = 0; i < colorPresets.Length; i += presetsPerRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = 0; j < presetsPerRow && (i + j) < colorPresets.Length; j++)
                {
                    Color preset = colorPresets[i + j];
                    GUI.backgroundColor = preset;
                    if (GUILayout.Button("", GUILayout.Width(30), GUILayout.Height(30)))
                    {
                        if (editingPathColor)
                        {
                            follower.pathColor = preset;
                            follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
                        }
                        else
                        {
                            follower.directionColor = preset;
                            follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
        private void DrawPlayerList()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Players", headerStyle);

            foreach (Player player in PhotonNetwork.PlayerList)
            {
                DrawPlayerEntry(player);
            }

            GUILayout.EndVertical();
        }
        private void DrawPlayerEntry(Player player)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            bool isCurrentlyFollowing = follower.currentPlayer == player && follower.followPlayerEnabled;
            GUI.backgroundColor = isCurrentlyFollowing ? Color.green : Color.white;

            GUILayout.Label(player.NickName, GUILayout.MinHeight(30f));

            string buttonText = isCurrentlyFollowing ? "Stop Following" : "Follow";
            if (GUILayout.Button(buttonText, GUILayout.Width(100)))
            {
                follower.ToggleFollowing(player);
            }

            if (isCurrentlyFollowing && GUILayout.Button("Clear Path", GUILayout.Width(80)))
            {
                follower.StopPathing();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }
        private void DrawPathDestinations()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Path Destinations", headerStyle);

            if (GUILayout.Button("Add Current Position Waypoint"))
            {
                Transform localBody = Rig.Instance.body;
                if (localBody == null) return;
                follower.lineRenderers.pathPositions.Add(localBody.position);
                follower.lineRenderers.UpdatePathLineRenderer();
            }
            if (GUILayout.Button("Add Forward Waypoint"))
            {
                Transform localBody = Rig.Instance.body;
                if (localBody == null) return;
                Vector3 newWaypoint;
                if (follower.lineRenderers.pathPositions.Count == 0)
                {
                    newWaypoint = localBody.position + localBody.forward * 2f;
                }
                else
                {
                    newWaypoint = follower.lineRenderers.pathPositions[follower.lineRenderers.pathPositions.Count - 1] + localBody.forward * 2f;
                }
                follower.lineRenderers.pathPositions.Add(newWaypoint);
                follower.lineRenderers.UpdatePathLineRenderer();
            }
            if (GUILayout.Button(follower.waitingForJumpStart ? "Set Jump Start" : "Set Jump End"))
            {
                Transform localBody = Rig.Instance.body;
                if (localBody == null) return;

                if (follower.waitingForJumpStart)
                {
                    follower.jumpWaypointStart = localBody.position;
                    follower.waitingForJumpStart = false;
                    logMessages.Add("Jump start set: " + follower.jumpWaypointStart);
                }
                else
                {
                    Vector3 jumpWaypointEnd = localBody.position;
                    follower.waitingForJumpStart = true;
                    logMessages.Add("Jump end set: " + jumpWaypointEnd);

                    float jumpAngle = follower.walkAnimator.jumpAngleDegrees;
                    List<Vector3> jumpArc = follower.GenerateJumpArc(follower.jumpWaypointStart, jumpWaypointEnd, jumpAngle);

                    int insertIndex = follower.lineRenderers.pathPositions.Count;
                    follower.lineRenderers.pathPositions.InsertRange(insertIndex, jumpArc);
                    follower.lineRenderers.UpdatePathLineRenderer();
                }
            }
            if (GUILayout.Button("Clear Jump Points", GUILayout.Width(120)))
            {
                follower.waitingForJumpStart = true;
                follower.jumpWaypointStart = Vector3.zero;
                logMessages.Add("Jump points cleared");
            }
            if (GUILayout.Button("Remove Last Waypoint"))
            {
                if (follower.lineRenderers.pathPositions.Count > 0)
                {
                    follower.lineRenderers.pathPositions.RemoveAt(follower.lineRenderers.pathPositions.Count - 1);
                    follower.lineRenderers.UpdatePathLineRenderer();
                }
            }
            if (GUILayout.Button("Start Path Following"))
            {
                if (follower.lineRenderers.pathPositions.Count > 0)
                {
                    follower.followPathEnabled = true;
                }
            }
            if (GUILayout.Button("Stop Path Following"))
            {
                follower.StopPathing();
            }

            GUILayout.Space(10);
            GUILayout.Label("Preset Management", headerStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Preset Name:", GUILayout.Width(80));
            presetName = GUILayout.TextField(presetName);
            if (GUILayout.Button("Save Preset", GUILayout.Width(100)))
            {
                follower.SavePreset(presetName);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Refresh Presets"))
            {
                follower.RefreshPresetFiles();
            }

            GUILayout.Label("Available Presets:");
            presetScrollPosition = GUILayout.BeginScrollView(presetScrollPosition, GUILayout.Height(100));
            if (presetFiles != null && presetFiles.Length > 0)
            {
                foreach (string presetFile in presetFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(presetFile);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(fileName);
                    if (GUILayout.Button("Load", GUILayout.Width(60)))
                    {
                        follower.LoadPreset(fileName);
                    }
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("No presets found.");
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
        private void DrawMisc()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Misc", headerStyle);
            if (GUILayout.Button("Refresh Objects"))
            {
                follower.ScanActiveObjects();
            }
            if (!follower.avoidObjects)
            {
                if (GUILayout.Button("Start Avoid Objects"))
                {
                    follower.ScanActiveObjects();
                    follower.avoidObjects = true;
                }
            }
            else
            {
                if (GUILayout.Button("Stop Avoid Objects"))
                {
                    follower.avoidObjects = false;
                }
            }
            if (GUILayout.Button("Send Logs"))
            {
                string logs = Logging.GetFullLogText();
                //if (!string.IsNullOrEmpty(logs))
                {
                    GameObject senderObj = new GameObject("WebhookSender");
                    WebhookSender sender = senderObj.AddComponent<WebhookSender>();
                    sender.Initialize(follower.DiscordWebhookUrl.Value, logs);
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Flee Settings", headerStyle);

            GUILayout.Label($"Flee Radius : {follower.FLEE_RADIUS:F1}");
            follower.FLEE_RADIUS = GUILayout.HorizontalSlider(follower.FLEE_RADIUS, 0.1f, 30f);

            bool newFleeEnabled = GUILayout.Toggle(follower.fleeEnabled, "Flee from Taggers");
            if (newFleeEnabled != follower.fleeEnabled)
            {
                follower.fleeEnabled = newFleeEnabled;
                if (newFleeEnabled)
                {
                    follower.followPlayerEnabled = false;
                    follower.followPathEnabled = false;
                    follower.isTagging = false;
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Hand Settings", headerStyle);

            GUILayout.Label("Activation Point:");
            GUILayout.BeginHorizontal();
            foreach (ActivationPoint point in Enum.GetValues(typeof(ActivationPoint)))
            {
                if (GUILayout.Toggle(follower.activationPoint == point, point.ToString()))
                {
                    follower.activationPoint = point;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Hand Down Duration: {follower.handDownDuration:F1}s");
            follower.handDownDuration = GUILayout.HorizontalSlider(follower.handDownDuration, 0.1f, 2f);

            GUILayout.Label($"Button Hold Duration: {follower.buttonHoldDuration:F1}s");
            follower.buttonHoldDuration = GUILayout.HorizontalSlider(follower.buttonHoldDuration, 0.1f, 3f);

            GUILayout.Label($"Hand Up Duration: {follower.handUpDuration:F1}s");
            follower.handUpDuration = GUILayout.HorizontalSlider(follower.handUpDuration, 0.1f, 2f);

            GUILayout.Label("Activation Button(s):");
            GUILayout.BeginHorizontal();
            foreach (HandButton button in Enum.GetValues(typeof(HandButton)))
            {
                bool active = (follower.selectedHandButton & button) != 0;
                bool newActive = GUILayout.Toggle(active, button.ToString());
                if (newActive != active)
                {
                    if (newActive) { follower.selectedHandButton |= button; }
                    else { follower.selectedHandButton &= ~button; }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Select Hand:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(isLeftHanded, "Left-Handed"))
            {
                isLeftHanded = true;
            }
            if (GUILayout.Toggle(!isLeftHanded, "Right-Handed"))
            {
                isLeftHanded = false;
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Perform Hand"))
            {
                follower.PerformHand(isLeftHanded);
            }

            GUILayout.EndVertical();

            /*
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label($"Active Objects ({follower.activeObjects.Count})", headerStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Objects"))
            {
                follower.ScanActiveObjects();
            }
            GUILayout.Label($"Scan Interval: {follower.scanInterval}s");
            follower.scanInterval = GUILayout.HorizontalSlider(follower.scanInterval, 0.5f, 5f);
            GUILayout.EndHorizontal();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            foreach (GameObject obj in follower.activeObjects.OrderBy(o => o.name))
            {
                GUILayout.BeginHorizontal(GUI.skin.box);

                GUILayout.Label($"{obj.name}", GUILayout.Width(150));
                string type = obj.GetComponent<Collider>() ? "[Collider]" : "[No Collider]";
                GUILayout.Label(type, GUILayout.Width(80));

                Vector3 pos = obj.transform.position;
                float distance = Vector3.Distance(pos, Rig.Instance.body.position);
                GUILayout.Label($"Dist: {distance:F1}m", GUILayout.Width(80));

                GUILayout.Label($"Layer: {LayerMask.LayerToName(obj.layer)}");

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            */

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Tag Settings", headerStyle);

            if (!follower.isTagging)
            {
                if (GUILayout.Button("Start Tag"))
                {
                    follower.StartTagging();
                }
            }
            else
            {
                GUILayout.Label($"Tagging {follower.taggedPlayer?.NickName} for {follower.tagTimer:F1} seconds");
                if (GUILayout.Button("Stop Tag"))
                {
                    follower.StopTagging();
                }
            }
            GUILayout.EndVertical();
        }
        private void DrawLogsTab()
        {
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Logs", headerStyle);

            logScrollPosition = GUILayout.BeginScrollView(logScrollPosition, GUILayout.Height(350));
            foreach (string log in logMessages)
            {
                GUILayout.Label(log);
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }
        private void DrawColorPicker(int windowID)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            Color currentColor = editingPathColor ? follower.pathColor : follower.directionColor;
            float red = currentColor.r;
            float green = currentColor.g;
            float blue = currentColor.b;

            GUILayout.Label($"Red: {red:F2}", sliderLabelStyle);
            red = GUILayout.HorizontalSlider(red, 0f, 1f);
            GUILayout.Label($"Green: {green:F2}", sliderLabelStyle);
            green = GUILayout.HorizontalSlider(green, 0f, 1f);
            GUILayout.Label($"Blue: {blue:F2}", sliderLabelStyle);
            blue = GUILayout.HorizontalSlider(blue, 0f, 1f);

            Color newColor = new Color(red, green, blue, currentColor.a);
            if (editingPathColor)
            {
                follower.pathColor = newColor;
                follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
            }
            else
            {
                follower.directionColor = newColor;
                follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
            }

            Rect previewRect = GUILayoutUtility.GetRect(50, 50);
            GUI.backgroundColor = newColor;
            GUI.Box(previewRect, "");
            GUI.backgroundColor = Color.white;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close")) showColorPicker = false;
            if (GUILayout.Button("Cancel"))
            {
                if (editingPathColor) follower.pathColor = originalColorWhenPickerOpened;
                else follower.directionColor = originalColorWhenPickerOpened;
                follower.lineRenderers.UpdateLineColors(follower.pathColor, follower.directionColor);
                showColorPicker = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }
    }
    public class LineRenderers : MonoBehaviour
    {
        public class LineRendererConfig
        {
            public readonly GameObject GameObject;
            public readonly LineRenderer Renderer;
            private readonly Material lineMaterial;

            public LineRendererConfig(string name, Color color, float width, float alpha)
            {
                GameObject = new GameObject(name);
                Renderer = GameObject.AddComponent<LineRenderer>();
                Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
                lineMaterial = new Material(shader);
                Color finalColor = new Color(color.r, color.g, color.b, alpha);
                lineMaterial.color = finalColor;
                Renderer.material = lineMaterial;
                Renderer.startWidth = width;
                Renderer.endWidth = width;
                Renderer.enabled = false;
            }

            public void UpdateColor(Color color, float alpha)
            {
                Color finalColor = new Color(color.r, color.g, color.b, alpha);
                Renderer.material.SetColor("_Color", finalColor);
                Renderer.startColor = finalColor;
                Renderer.endColor = finalColor;
            }
        }

        public LineRendererConfig pathLine;
        public LineRendererConfig directionLine;
        public List<Vector3> pathPositions = new List<Vector3>();
        public float lineAlpha;

        public void Initialize(string pathLineName, string directionLineName, Color pathColor, Color directionColor, float alpha, float pathLineWidth, float directionLineWidth)
        {
            lineAlpha = alpha;
            pathLine = new LineRendererConfig(pathLineName, pathColor, pathLineWidth, alpha);
            directionLine = new LineRendererConfig(directionLineName, directionColor, directionLineWidth, alpha);
            directionLine.Renderer.positionCount = 2;
        }

        public void UpdateLineColors(Color pathColor, Color directionColor)
        {
            pathLine.UpdateColor(pathColor, lineAlpha);
            directionLine.UpdateColor(directionColor, lineAlpha);
        }

        public string ColorToHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        public void UpdatePathLineRenderer()
        {
            if (pathLine != null && pathPositions != null)
            {
                pathLine.Renderer.positionCount = pathPositions.Count;
                pathLine.Renderer.SetPositions(pathPositions.ToArray());
            }
        }

        public void UpdateLineRenderers(Transform localBody, Transform targetTransform)
        {
            if (pathLine == null || directionLine == null) { return; }
            pathLine.Renderer.enabled = true;
            directionLine.Renderer.enabled = true;
            if (pathPositions.Count == 0 || Vector3.Distance(pathPositions[pathPositions.Count - 1], localBody.position) >= MIN_DISTANCE_BETWEEN_POINTS)
            {
                pathPositions.Add(localBody.position);
                UpdatePathLineRenderer();
            }
            directionLine.Renderer.SetPosition(0, localBody.position);
            directionLine.Renderer.SetPosition(1, targetTransform.position);
        }

        public void UpdateLineRenderersForPath(Transform localBody, Vector3 targetPoint)
        {
            if (pathLine == null || directionLine == null) { return; }
            pathLine.Renderer.enabled = true;
            directionLine.Renderer.enabled = true;
            List<Vector3> fullPath = new List<Vector3>();
            fullPath.Add(localBody.position);
            fullPath.AddRange(pathPositions);
            pathLine.Renderer.positionCount = fullPath.Count;
            pathLine.Renderer.SetPositions(fullPath.ToArray());
            directionLine.Renderer.SetPosition(0, localBody.position);
            directionLine.Renderer.SetPosition(1, targetPoint);
        }

        public void DisableLineRenderers()
        {
            if (pathLine != null) pathLine.Renderer.enabled = false;
            if (directionLine != null) directionLine.Renderer.enabled = false;
        }

        public void ClearPath(Transform localBody)
        {
            pathPositions.Clear();
            if (localBody != null)
            {
                pathPositions.Add(localBody.position);
                UpdatePathLineRenderer();
            }
        }
        private const float MIN_DISTANCE_BETWEEN_POINTS = 0.1f;
    }
    public class WebhookSender : MonoBehaviour
    {
        private string webhookUrl;
        private string logContent;

        public void Initialize(string url, string logs)
        {
            webhookUrl = url;
            logContent = logs;
            StartCoroutine(SendLogs());
        }

        private IEnumerator SendLogs()
        {
            if (PlayerFollowerGUI.logMessages == null || PlayerFollowerGUI.logMessages.Count == 0)
            {
                Destroy(gameObject);
                yield break;
            }

            StringBuilder contentBuilder = new StringBuilder();
            foreach (string log in PlayerFollowerGUI.logMessages)
            {
                contentBuilder.AppendLine(log);
            }
            string logContent = contentBuilder.ToString();
            string fileName = $"Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            byte[] logBytes = Encoding.UTF8.GetBytes(logContent);

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("content", "logs."),
                new MultipartFormFileSection("file", logBytes, fileName, "text/plain")
            };

            using (UnityWebRequest request = UnityWebRequest.Post(webhookUrl, formData))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to send logs to Discord: {request.error}");
                }
                else
                {
                    Debug.Log("Logs sent to Discord successfully.");
                }
            }

            Destroy(gameObject);
        }
    }
    public class Archived
    {
        /*
         * 
        // Waypoints for Tagging & Following
        private float lastWaypointUpdateTime = 0f;
        private List<Vector3> followingWaypoints = new List<Vector3>();
        private List<Vector3> taggingWaypoints = new List<Vector3>();
        private int currentFollowingWaypointIndex = 0;
        private int currentTaggingWaypointIndex = 0;
         *         #region Tagging & Following With Waypoints
        // idk why i made this
        private void UpdateWaypoints()
        {
            if (followPlayerEnabled && currentPlayer != null)
            {
                var (targetTransform, _) = GetPlayerTransforms();
                if (targetTransform != null)
                {
                    UpdateFollowingWaypoints(targetTransform.position);
                }
            }

            if (isTagging && taggedPlayer != null)
            {
                var (targetTransform, _) = GetPlayerTransforms(taggedPlayer);
                if (targetTransform != null)
                {
                    UpdateTaggingWaypoints(targetTransform.position);
                }
            }
        }
        private void UpdateFollowingWaypoints(Vector3 targetPosition)
        {
            Vector3 currentPosition = Rig.Instance.body.position;
            followingWaypoints.Clear();

            int numWaypoints = 3;
            for (int i = 0; i <= numWaypoints; i++)
            {
                float t = i / (float)numWaypoints;
                Vector3 waypoint = Vector3.Lerp(currentPosition, targetPosition, t);
                if (i != 0 && i != numWaypoints)
                {
                    waypoint += new Vector3(
                        UnityEngine.Random.Range(-0.5f, 0.5f),
                        0,
                        UnityEngine.Random.Range(-0.5f, 0.5f)
                    );
                }

                followingWaypoints.Add(waypoint);
            }

            currentFollowingWaypointIndex = 0;
        }
        private void UpdateTaggingWaypoints(Vector3 targetPosition)
        {
            Vector3 currentPosition = Rig.Instance.body.position;
            taggingWaypoints.Clear();

            int numWaypoints = 2;
            for (int i = 0; i <= numWaypoints; i++)
            {
                float t = i / (float)numWaypoints;
                Vector3 waypoint = Vector3.Lerp(currentPosition, targetPosition, t);

                if (i != 0 && i != numWaypoints)
                {
                    waypoint += new Vector3(
                        UnityEngine.Random.Range(-0.3f, 0.3f),
                        0,
                        UnityEngine.Random.Range(-0.3f, 0.3f)
                    );
                }

                taggingWaypoints.Add(waypoint);
            }

            currentTaggingWaypointIndex = 0;
        }
        private void MoveToTargetPlayerWithWaypoints()
        {
            Transform localBody = Rig.Instance.body;
            if (localBody == null) return;

            if (followingWaypoints.Count == 0)
            {
                var (targetTransform, _) = GetPlayerTransforms();
                if (targetTransform != null)
                {
                    UpdateFollowingWaypoints(targetTransform.position);
                }
                return;
            }

            Vector3 currentWaypoint = followingWaypoints[currentFollowingWaypointIndex];
            Vector3 directionToWaypoint = currentWaypoint - localBody.position;
            directionToWaypoint.y = 0f;
            float distanceToWaypoint = directionToWaypoint.magnitude;

            lineRenderers.UpdateLineRenderersForPath(localBody, currentWaypoint);

            if (distanceToWaypoint < TARGET_PROXIMITY_THRESHOLD)
            {
                currentFollowingWaypointIndex++;
                if (currentFollowingWaypointIndex >= followingWaypoints.Count)
                {
                    var (targetTransform, _) = GetPlayerTransforms();
                    if (targetTransform != null)
                    {
                        UpdateFollowingWaypoints(targetTransform.position);
                    }
                }
                return;
            }

            Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * directionToWaypoint.normalized;
            StartMovement(localDirection);
            TurnTowardsTargetPosition(localBody, currentWaypoint);
        }
        private void MoveToTaggedPlayerWithWaypoints()
        {
            Transform localBody = Rig.Instance.body;
            if (localBody == null) return;

            if (taggingWaypoints.Count == 0)
            {
                var (targetTransform, _) = GetPlayerTransforms(taggedPlayer);
                if (targetTransform != null)
                {
                    UpdateTaggingWaypoints(targetTransform.position);
                }
                return;
            }

            Vector3 currentWaypoint = taggingWaypoints[currentTaggingWaypointIndex];
            Vector3 directionToWaypoint = currentWaypoint - localBody.position;
            directionToWaypoint.y = 0f;
            float distanceToWaypoint = directionToWaypoint.magnitude;

            lineRenderers.UpdateLineRenderersForPath(localBody, currentWaypoint);

            if (distanceToWaypoint < TARGET_PROXIMITY_THRESHOLD)
            {
                currentTaggingWaypointIndex++;
                if (currentTaggingWaypointIndex >= taggingWaypoints.Count)
                {
                    var (targetTransform, _) = GetPlayerTransforms(taggedPlayer);
                    if (targetTransform != null)
                    {
                        UpdateTaggingWaypoints(targetTransform.position);
                    }
                }
                return;
            }

            Vector3 localDirection = Quaternion.Inverse(localBody.rotation) * directionToWaypoint.normalized;
            StartMovement(localDirection);
            TurnTowardsTargetPosition(localBody, currentWaypoint);
        }
        #endregion
        */
    }
}