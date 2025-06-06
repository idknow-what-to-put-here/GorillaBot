using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

namespace WalkSimulator.Bot
{
    public class PlayerFollowerUtils
    {
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

            public void DrawJumpArc(List<Vector3> arcPoints, Color color, float width)
            {
                if (arcPoints == null || arcPoints.Count < 2) return;

                LineRenderer arcRenderer = GetOrCreateLineRenderer("JumpArcLine");
                arcRenderer.startWidth = width;
                arcRenderer.endWidth = width;
                arcRenderer.startColor = color;
                arcRenderer.endColor = color;
                arcRenderer.positionCount = arcPoints.Count;
                arcRenderer.SetPositions(arcPoints.ToArray());
                arcRenderer.enabled = true;

                StartCoroutine(FadeOutJumpArc(arcRenderer));
            }

            private IEnumerator FadeOutJumpArc(LineRenderer arcRenderer)
            {
                float duration = 1f;
                float elapsed = 0f;
                Color startColor = arcRenderer.startColor;
                Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                    Color newColor = new Color(startColor.r, startColor.g, startColor.b, alpha);
                    arcRenderer.startColor = newColor;
                    arcRenderer.endColor = newColor;
                    yield return null;
                }

                arcRenderer.enabled = false;
            }
<<<<<<< HEAD
            public void UpdateLineRenderersToPosition(Transform fromTransform, Vector3 toPosition)
            {
                if (fromTransform == null) return;

                Vector3 startPosition = fromTransform.position;
                Vector3 endPosition = toPosition;

                if (pathLine.Renderer != null)
                {
                    pathLine.Renderer.SetPosition(0, startPosition);
                    pathLine.Renderer.SetPosition(1, endPosition);
                    pathLine.Renderer.enabled = true;
                }

                if (directionLine.Renderer != null)
                {
                    Vector3 direction = (endPosition - startPosition).normalized;
                    Vector3 directionEnd = startPosition + direction * 2f;

                    directionLine.Renderer.SetPosition(0, startPosition);
                    directionLine.Renderer.SetPosition(1, directionEnd);
                    directionLine.Renderer.enabled = true;
                }
=======
            public void UpdateLineRenderersToPosition(Transform fromTransform, Vector3 toPosition)
            {
                if (fromTransform == null) return;

                Vector3 startPosition = fromTransform.position;
                Vector3 endPosition = toPosition;

                if (pathLine.Renderer != null)
                {
                    pathLine.Renderer.SetPosition(0, startPosition);
                    pathLine.Renderer.SetPosition(1, endPosition);
                    pathLine.Renderer.enabled = true;
                }

                if (directionLine.Renderer != null)
                {
                    Vector3 direction = (endPosition - startPosition).normalized;
                    Vector3 directionEnd = startPosition + direction * 2f;

                    directionLine.Renderer.SetPosition(0, startPosition);
                    directionLine.Renderer.SetPosition(1, directionEnd);
                    directionLine.Renderer.enabled = true;
                }
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
            }
            private LineRenderer GetOrCreateLineRenderer(string name)
            {
                Transform existingRenderer = transform.Find(name);
                if (existingRenderer != null)
                {
                    return existingRenderer.GetComponent<LineRenderer>();
                }

                GameObject newRenderer = new GameObject(name);
                newRenderer.transform.SetParent(transform);
                LineRenderer lineRenderer = newRenderer.AddComponent<LineRenderer>();
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.useWorldSpace = true;
                return lineRenderer;
            }
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
        public class Tagging 
        {
            public static Player GetRandomNonTaggedPlayer()
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
        }
        public class Archived
        {
            /*
             *         public static int[] bones = new int[] {
                4, 3, 5, 4, 19, 18, 20, 19, 3, 18, 21, 20, 22, 21, 25, 21, 29, 21, 31, 29, 27, 25, 24, 22, 6, 5, 7, 6, 10, 6, 14, 6, 16, 14, 12, 10, 9, 7
            };

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
}
