using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using UnityEngine;

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
