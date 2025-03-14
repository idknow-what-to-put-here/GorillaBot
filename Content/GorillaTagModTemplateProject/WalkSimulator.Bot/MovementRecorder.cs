using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SpatialTracking;
using WalkSimulator;
using WalkSimulator.Rigging;

namespace WalkSimulator.Bot
{
    public class MovementRecorder : MonoBehaviour
    {
        #region Nested Types

        [Serializable]
        public class FrameData
        {
            public float timestamp;
            public SerializableVector3 bodyPosition;
            public SerializableQuaternion bodyRotation;
            public SerializableVector3 headPosition;
            public SerializableQuaternion headRotation;
            public SerializableVector3 leftHandPosition;
            public SerializableQuaternion leftHandRotation;
            public SerializableVector3 rightHandPosition;
            public SerializableQuaternion rightHandRotation;
            public SerializableVector3 inputDirection;
            public bool leftGrip;
            public bool leftTrigger;
            public bool leftPrimary;
            public bool leftSecondary;
            public bool rightGrip;
            public bool rightTrigger;
            public bool rightPrimary;
            public bool rightSecondary;
        }

        [Serializable]
        public struct SerializableVector3
        {
            public float x, y, z;

            public SerializableVector3(Vector3 v)
            {
                x = v.x;
                y = v.y;
                z = v.z;
            }

            public Vector3 ToVector3() => new Vector3(x, y, z);
        }

        [Serializable]
        public struct SerializableQuaternion
        {
            public float x, y, z, w;

            public SerializableQuaternion(Quaternion q)
            {
                x = q.x;
                y = q.y;
                z = q.z;
                w = q.w;
            }

            public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
        }

        [Serializable]
        public class ReplayData
        {
            public List<FrameData> frames = new List<FrameData>();
            public string recordingDate;
        }

        #endregion

        private ReplayData currentReplay = new ReplayData();
        private float recordingStartTime;
        public bool isRecording { get; private set; } = false;
        public bool isReplaying { get; private set; } = false;
        private int currentReplayFrame;
        private float replayStartTime;

        private readonly List<Behaviour> trackingComponents = new List<Behaviour>();
        private Transform virtualTargetTransform;
        private readonly PlayerFollower follower;

        public string ReplayFolder => Path.Combine(BepInEx.Paths.GameRootPath, "PlayerFollower", "Replays");

        public MovementRecorder(PlayerFollower follower)
        {
            this.follower = follower ?? throw new ArgumentNullException(nameof(follower));
            GameObject virtualTarget = new GameObject("VirtualReplayTarget");
            virtualTargetTransform = virtualTarget.transform;

            if (!Directory.Exists(ReplayFolder))
            {
                Directory.CreateDirectory(ReplayFolder);
            }
        }
        public void FixedUpdate()
        {
            if (isRecording)
            {
                RecordFrame();
            }
            else if (isReplaying && currentReplayFrame < currentReplay.frames.Count)
            {
                ReplayFrame();
            }
        }
        public void StartRecording()
        {
            if (isRecording || isReplaying) { return; }

            currentReplay = new ReplayData { recordingDate = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") };
            recordingStartTime = Time.time;
            isRecording = true;
            follower.logger.LogInfo("Started recording movement.");
        }
        public void StopRecording()
        {
            if (!isRecording) { return; }

            isRecording = false;
            isReplaying = false;
            string filename = $"replay_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            SaveRecording(filename);
            follower.logger.LogInfo($"Stopped recording. Captured {currentReplay.frames.Count} frames and saved to {filename}.");
        }
        public void SaveRecording(string filename)
        {
            if (currentReplay.frames.Count == 0)
            {
                follower.logger.LogWarning("No frames to save.");
                return;
            }

            try
            {
                string filePath = Path.Combine(ReplayFolder, filename);
                string json = JsonConvert.SerializeObject(currentReplay, Formatting.Indented);
                File.WriteAllText(filePath, json);
                follower.logger.LogInfo($"Saved recording to {filePath}.");
            }
            catch (Exception ex)
            {
                follower.logger.LogError($"Error saving recording: {ex.Message}");
            }
        }
        public void LoadReplay(string filename)
        {
            try
            {
                string filePath = Path.Combine(ReplayFolder, filename);
                if (!File.Exists(filePath))
                {
                    follower.logger.LogError($"Replay file not found: {filePath}");
                    return;
                }

                string json = File.ReadAllText(filePath);
                currentReplay = JsonConvert.DeserializeObject<ReplayData>(json);
                follower.logger.LogInfo($"Loaded replay from {filePath} with {currentReplay.frames.Count} frames.");
            }
            catch (Exception ex)
            {
                follower.logger.LogError($"Error loading replay: {ex.Message}");
            }
        }
        public string[] GetSavedReplays()
        {
            try
            {
                if (!Directory.Exists(ReplayFolder))
                {
                    Directory.CreateDirectory(ReplayFolder);
                    return Array.Empty<string>();
                }

                return Directory.GetFiles(ReplayFolder, "replay_*.json", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).ToArray();
            }
            catch (Exception ex)
            {
                follower.logger.LogError($"Error getting saved replays: {ex.Message}");
                return Array.Empty<string>();
            }
        }
        public void StartReplay()
        {
            if (isRecording || isReplaying || currentReplay.frames.Count == 0) { return; }

            TrackedPoseDriver[] poseDrivers = Rig.Instance.GetComponentsInChildren<TrackedPoseDriver>();
            foreach (var driver in poseDrivers)
            {
                driver.enabled = false;
                trackingComponents.Add(driver);
            }

            SetRigTransform(currentReplay.frames[0]);

            isRecording = false;
            isReplaying = true;
            currentReplayFrame = 1;
            replayStartTime = Time.time;
            follower.logger.LogInfo("Started replay with teleport to starting position.");

            follower.StopFollowing();
            follower.followPlayerEnabled = false;
        }
        public void StopReplay()
        {
            if (!isReplaying) { return; }

            foreach (var component in trackingComponents)
            {
                if (component != null) { component.enabled = true; }
            }
            trackingComponents.Clear();

            isReplaying = false;
            isRecording = false;
            follower.StopFollowing();
            follower.logger.LogInfo("Stopped replay.");
        }
        private void RecordFrame()
        {
            var frame = new FrameData
            {
                timestamp = Time.time - recordingStartTime,
                bodyPosition = new SerializableVector3(Rig.Instance.body.position),
                bodyRotation = new SerializableQuaternion(Rig.Instance.body.rotation),
                headPosition = new SerializableVector3(Rig.Instance.head.position),
                headRotation = new SerializableQuaternion(Rig.Instance.head.rotation),
                leftHandPosition = new SerializableVector3(Rig.Instance.leftHand.transform.position),
                leftHandRotation = new SerializableQuaternion(Rig.Instance.leftHand.transform.rotation),
                rightHandPosition = new SerializableVector3(Rig.Instance.rightHand.transform.position),
                rightHandRotation = new SerializableQuaternion(Rig.Instance.rightHand.transform.rotation),
                inputDirection = new SerializableVector3(InputHandler.inputDirectionNoY),
                leftGrip = Rig.Instance.leftHand.grip,
                leftTrigger = Rig.Instance.leftHand.trigger,
                leftPrimary = Rig.Instance.leftHand.primary,
                leftSecondary = Rig.Instance.leftHand.secondary,
                rightGrip = Rig.Instance.rightHand.grip,
                rightTrigger = Rig.Instance.rightHand.trigger,
                rightPrimary = Rig.Instance.rightHand.primary,
                rightSecondary = Rig.Instance.rightHand.secondary,
            };

            currentReplay.frames.Add(frame);
        }
        private void ReplayFrame()
        {
            if (currentReplayFrame >= currentReplay.frames.Count)
            {
                StopReplay();
                return;
            }

            float currentTime = Time.time - replayStartTime;
            FrameData frame = currentReplay.frames[currentReplayFrame];

            if (currentTime >= frame.timestamp)
            {
                virtualTargetTransform.position = frame.bodyPosition.ToVector3();
                virtualTargetTransform.rotation = frame.bodyRotation.ToQuaternion();
                Rig.Instance.body.position = virtualTargetTransform.position;
                Rig.Instance.body.rotation = virtualTargetTransform.rotation;
                follower.UpdateMovement(Rig.Instance.body, virtualTargetTransform);

                Rig.Instance.head.position = frame.headPosition.ToVector3();
                Rig.Instance.head.rotation = frame.headRotation.ToQuaternion();

                Rig.Instance.leftHand.transform.position = frame.leftHandPosition.ToVector3();
                Rig.Instance.leftHand.transform.rotation = frame.leftHandRotation.ToQuaternion();

                Rig.Instance.rightHand.transform.position = frame.rightHandPosition.ToVector3();
                Rig.Instance.rightHand.transform.rotation = frame.rightHandRotation.ToQuaternion();

                Rig.Instance.leftHand.grip = frame.leftGrip;
                Rig.Instance.leftHand.trigger = frame.leftTrigger;
                Rig.Instance.leftHand.primary = frame.leftPrimary;
                Rig.Instance.leftHand.secondary = frame.leftSecondary;

                Rig.Instance.rightHand.grip = frame.rightGrip;
                Rig.Instance.rightHand.trigger = frame.rightTrigger;
                Rig.Instance.rightHand.primary = frame.rightPrimary;
                Rig.Instance.rightHand.secondary = frame.rightSecondary;

                currentReplayFrame++;
            }
        }
        private void SetRigTransform(FrameData frame)
        {
            Rig.Instance.body.position = frame.bodyPosition.ToVector3();
            Rig.Instance.body.rotation = frame.bodyRotation.ToQuaternion();

            Rig.Instance.head.position = frame.headPosition.ToVector3();
            Rig.Instance.head.rotation = frame.headRotation.ToQuaternion();

            Rig.Instance.leftHand.transform.position = frame.leftHandPosition.ToVector3();
            Rig.Instance.leftHand.transform.rotation = frame.leftHandRotation.ToQuaternion();

            Rig.Instance.rightHand.transform.position = frame.rightHandPosition.ToVector3();
            Rig.Instance.rightHand.transform.rotation = frame.rightHandRotation.ToQuaternion();
        }
    }
}
