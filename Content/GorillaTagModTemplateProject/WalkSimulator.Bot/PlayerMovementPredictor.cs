using BepInEx.Logging;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace WalkSimulator.Bot
{
    public class PlayerMovementPredictor
    {
        private Dictionary<Player, PlayerMovementData> playerMovementHistory = new Dictionary<Player, PlayerMovementData>();
        private const int HISTORY_SIZE = 15;
        private const float PREDICTION_TIME = 0.5f;
        private const float MIN_VELOCITY_THRESHOLD = 0.1f;
        private const float DIRECTION_CHANGE_THRESHOLD = 30f; // degrees
        private const float MOMENTUM_DECAY = 0.95f;
        private const float GRAVITY = -9.8f;

        private PlayerFollower playerFollower;

        public PlayerMovementPredictor(PlayerFollower follower)
        {
            playerFollower = follower;
        }

        public class PlayerMovementData
        {
            public Queue<Vector3> positionHistory = new Queue<Vector3>();
            public Queue<Vector3> velocityHistory = new Queue<Vector3>();
            public Queue<float> timeHistory = new Queue<float>();
            public Queue<float> directionHistory = new Queue<float>();

            public Vector3 lastPosition;
            public Vector3 currentVelocity;
            public Vector3 acceleration;
            public Vector3 previousVelocity;
            public float lastUpdateTime;
            public float averageSpeed;
            public Vector3 movementPattern;
            public bool isJumping;
            public float jumpStartTime;
            public Vector3 jumpStartVelocity;
            public bool isGrounded = true;
            public float lastGroundTime;

            public Vector3 preferredDirection;
            public float directionConsistency;
            public bool isChangingDirection;
            public float lastDirectionChange;
            public Vector3 momentum;

            public PlayerMovementData()
            {
                lastUpdateTime = Time.time;
                lastGroundTime = Time.time;
            }
        }

        public void UpdatePlayerMovement(Player player, Vector3 currentPosition)
        {
            if (!playerMovementHistory.ContainsKey(player))
            {
                playerMovementHistory[player] = new PlayerMovementData();
            }

            PlayerMovementData data = playerMovementHistory[player];
            float currentTime = Time.time;
            float deltaTime = currentTime - data.lastUpdateTime;

            if (deltaTime > 0.016f) // update at most 60 times per sec
            {
                Vector3 newVelocity = Vector3.zero;
                if (data.positionHistory.Count > 0)
                {
                    newVelocity = (currentPosition - data.lastPosition) / deltaTime;
                }

                data.previousVelocity = data.currentVelocity;
                data.acceleration = (newVelocity - data.currentVelocity) / deltaTime;
                data.currentVelocity = newVelocity;

                data.momentum = Vector3.Lerp(data.momentum, data.currentVelocity, 1f - MOMENTUM_DECAY);
                UpdateGroundState(data, currentPosition, deltaTime);
                UpdateDirectionTracking(data, newVelocity);
                UpdateMovementPattern(data);

                data.positionHistory.Enqueue(currentPosition);
                data.velocityHistory.Enqueue(newVelocity);
                data.timeHistory.Enqueue(currentTime);

                while (data.positionHistory.Count > HISTORY_SIZE)
                {
                    data.positionHistory.Dequeue();
                    data.velocityHistory.Dequeue();
                    data.timeHistory.Dequeue();
                }

                data.lastPosition = currentPosition;
                data.lastUpdateTime = currentTime;
            }
        }
        private void UpdateGroundState(PlayerMovementData data, Vector3 currentPosition, float deltaTime)
        {
            bool wasGrounded = data.isGrounded;
            data.isGrounded = data.currentVelocity.y > -2f && data.currentVelocity.y < 2f;

            if (wasGrounded && !data.isGrounded && data.currentVelocity.y > 1f)
            {
                // jump detected
                data.isJumping = true;
                data.jumpStartTime = Time.time;
                data.jumpStartVelocity = data.currentVelocity;
            }
            else if (!wasGrounded && data.isGrounded)
            {
                // landing detected
                data.isJumping = false;
                data.lastGroundTime = Time.time;
            }
        }
        private void UpdateDirectionTracking(PlayerMovementData data, Vector3 newVelocity)
        {
            if (newVelocity.magnitude > MIN_VELOCITY_THRESHOLD)
            {
                Vector3 horizontalVelocity = new Vector3(newVelocity.x, 0, newVelocity.z);
                if (horizontalVelocity.magnitude > MIN_VELOCITY_THRESHOLD)
                {
                    float currentDirection = Mathf.Atan2(horizontalVelocity.z, horizontalVelocity.x) * Mathf.Rad2Deg;

                    if (data.directionHistory.Count > 0)
                    {
                        float lastDirection = data.directionHistory.Last();
                        float angleDiff = Mathf.DeltaAngle(lastDirection, currentDirection);

                        if (Mathf.Abs(angleDiff) > DIRECTION_CHANGE_THRESHOLD)
                        {
                            data.isChangingDirection = true;
                            data.lastDirectionChange = Time.time;
                        }
                        else if (Time.time - data.lastDirectionChange > 0.5f)
                        {
                            data.isChangingDirection = false;
                        }
                    }

                    data.directionHistory.Enqueue(currentDirection);
                    if (data.directionHistory.Count > 5)
                    {
                        data.directionHistory.Dequeue();
                    }

                    CalculateDirectionConsistency(data);
                }
            }
        }
        private void CalculateDirectionConsistency(PlayerMovementData data)
        {
            if (data.directionHistory.Count < 3) return;

            float[] directions = data.directionHistory.ToArray();
            Vector2 averageDirection = Vector2.zero;

            foreach (float dir in directions)
            {
                averageDirection += new Vector2(Mathf.Cos(dir * Mathf.Deg2Rad), Mathf.Sin(dir * Mathf.Deg2Rad));
            }

            averageDirection /= directions.Length;
            float avgAngle = Mathf.Atan2(averageDirection.y, averageDirection.x) * Mathf.Rad2Deg;
            data.preferredDirection = new Vector3(Mathf.Cos(avgAngle * Mathf.Deg2Rad), 0, Mathf.Sin(avgAngle * Mathf.Deg2Rad));
            data.directionConsistency = averageDirection.magnitude; // 0-1, 1 = very consistent
        }
        private void UpdateMovementPattern(PlayerMovementData data)
        {
            if (data.velocityHistory.Count < 5) return;

            Vector3[] velocities = data.velocityHistory.ToArray();
            Vector3 pattern = Vector3.zero;

            for (int i = 1; i < velocities.Length; i++)
            {
                pattern += velocities[i] - velocities[i - 1];
            }

            data.movementPattern = pattern / (velocities.Length - 1);

            float totalSpeed = 0f;
            foreach (Vector3 vel in velocities)
            {
                totalSpeed += vel.magnitude;
            }
            data.averageSpeed = totalSpeed / velocities.Length;
        }
        public Vector3 PredictPlayerPosition(Player player, float predictionTime = PREDICTION_TIME)
        {
            if (!playerMovementHistory.ContainsKey(player))
            {
                var vrrig = GorillaGameManager.instance.FindPlayerVRRig(player);
                return vrrig != null ? vrrig.transform.position : Vector3.zero;
            }

            if (!playerFollower.isPredictionEnabled)
            {
                return playerMovementHistory[player].lastPosition;
            }

            PlayerMovementData data = playerMovementHistory[player];

            if (data.currentVelocity.magnitude < MIN_VELOCITY_THRESHOLD)
            {
                return data.lastPosition;
            }

            Vector3 predictedPosition = data.lastPosition;
            Vector3 currentVel = data.currentVelocity;
            Vector3 currentAccel = data.acceleration;

            if (data.isJumping)
            {
                predictedPosition = PredictJumpTrajectory(data, predictionTime);
            }
            else if (data.isChangingDirection)
            {
                predictedPosition = PredictDirectionChange(data, predictionTime);
            }
            else if (data.directionConsistency > 0.7f)
            {
                predictedPosition = PredictConsistentMovement(data, predictionTime);
            }
            else
            {
                Vector3 momentum = data.momentum * MOMENTUM_DECAY;
                predictedPosition = data.lastPosition + (currentVel * predictionTime) + (0.5f * currentAccel * predictionTime * predictionTime) + (momentum * predictionTime * 0.3f);
            }

            return predictedPosition;
        }
        private Vector3 PredictJumpTrajectory(PlayerMovementData data, float predictionTime)
        {
            float jumpTime = Time.time - data.jumpStartTime;
            float totalPredictionTime = jumpTime + predictionTime;

            Vector3 horizontalVel = new Vector3(data.jumpStartVelocity.x, 0, data.jumpStartVelocity.z);
            Vector3 horizontalPos = data.lastPosition + horizontalVel * predictionTime;

            float verticalPos = data.lastPosition.y + (data.jumpStartVelocity.y * predictionTime) + (0.5f * GRAVITY * predictionTime * predictionTime);

            return new Vector3(horizontalPos.x, verticalPos, horizontalPos.z);
        }
        private Vector3 PredictDirectionChange(PlayerMovementData data, float predictionTime)
        {
            float changeAmount = Mathf.Clamp01((Time.time - data.lastDirectionChange) / 1f);

            Vector3 currentDirection = data.currentVelocity.normalized;
            Vector3 preferredDirection = data.preferredDirection;

            Vector3 blendedDirection = Vector3.Slerp(currentDirection, preferredDirection, changeAmount);
            Vector3 adjustedVelocity = blendedDirection * data.currentVelocity.magnitude * 0.8f;

            return data.lastPosition + adjustedVelocity * predictionTime;
        }
        private Vector3 PredictConsistentMovement(PlayerMovementData data, float predictionTime)
        {
            Vector3 consistentVelocity = data.preferredDirection * data.averageSpeed;
            Vector3 momentumInfluence = data.momentum * 0.4f;

            Vector3 combinedVelocity = Vector3.Lerp(data.currentVelocity, consistentVelocity + momentumInfluence, 0.6f);

            return data.lastPosition + combinedVelocity * predictionTime;
        }
        public Vector3 GetSmoothedVelocity(Player player)
        {
            if (!playerMovementHistory.ContainsKey(player) ||
                playerMovementHistory[player].velocityHistory.Count < 3)
            {
                return Vector3.zero;
            }

            PlayerMovementData data = playerMovementHistory[player];
            Vector3[] velocities = data.velocityHistory.ToArray();

            Vector3 smoothedVelocity = Vector3.zero;
            float totalWeight = 0f;

            for (int i = 0; i < velocities.Length; i++)
            {
                float weight = (i + 1f) / velocities.Length;
                smoothedVelocity += velocities[i] * weight;
                totalWeight += weight;
            }

            return totalWeight > 0 ? smoothedVelocity / totalWeight : Vector3.zero;
        }
        public Vector3 GetPredictedInterceptionPoint(Player player, Vector3 interceptorPosition, float interceptorSpeed)
        {
            if (!playerMovementHistory.ContainsKey(player))
            {
                var vrrig = GorillaGameManager.instance.FindPlayerVRRig(player);
                return vrrig != null ? vrrig.transform.position : Vector3.zero;
            }

            if (!playerFollower.isPredictionEnabled)
            {
                 return playerMovementHistory[player].lastPosition;
            }

            PlayerMovementData data = playerMovementHistory[player];
            Vector3 targetPos = data.lastPosition;
            Vector3 targetVel = GetSmoothedVelocity(player);

            float bestTime = 0f;
            Vector3 bestPoint = targetPos;

            for (float t = 0.1f; t <= 3f; t += 0.1f)
            {
                Vector3 futureTargetPos = PredictPlayerPosition(player, t);
                float distanceToTarget = Vector3.Distance(interceptorPosition, futureTargetPos);
                float timeToReach = distanceToTarget / interceptorSpeed;

                if (Mathf.Abs(timeToReach - t) < 0.2f) // change its way to close
                {
                    bestTime = t;
                    bestPoint = futureTargetPos;
                    break;
                }
            }

            return bestPoint;
        }
        public bool IsPlayerLikelyToJump(Player player)
        {
            if (!playerMovementHistory.ContainsKey(player)) return false;

            if (!playerFollower.isPredictionEnabled)
            {
                return false;
            }

            PlayerMovementData data = playerMovementHistory[player];
            return data.currentVelocity.y > 1f || (data.isGrounded && Time.time - data.lastGroundTime > 0.5f && data.currentVelocity.magnitude > 2f);
        }
        public void CleanupDisconnectedPlayers()
        {
            List<Player> playersToRemove = new List<Player>();

            foreach (var kvp in playerMovementHistory)
            {
                if (!PhotonNetwork.PlayerList.Contains(kvp.Key))
                {
                    playersToRemove.Add(kvp.Key);
                }
            }

            foreach (Player player in playersToRemove)
            {
                playerMovementHistory.Remove(player);
            }
        }
    }
}