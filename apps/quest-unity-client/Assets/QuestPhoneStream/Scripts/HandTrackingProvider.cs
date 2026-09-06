using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class SpatialHandJoint
    {
        public string id;
        public bool valid;
        public SpatialVector3 position;
        public SpatialQuaternion orientation;
    }

    [Serializable]
    public sealed class SpatialHandFrame
    {
        public string v = SpatialWire.Version;
        public string capability = "xr.hand.pose";
        public string streamId;
        public long sequence;
        public long timestamp;
        public string space = "local";
        public string hand;
        public bool valid;
        public SpatialPose wrist;
        public SpatialHandJoint[] joints = Array.Empty<SpatialHandJoint>();

        public string ToJson() => JsonUtility.ToJson(this);
        public static SpatialHandFrame FromJson(string json) => JsonUtility.FromJson<SpatialHandFrame>(json);
    }

    public sealed class HandTrackingProvider
    {
        private static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();

        public static readonly XRHandJointID[] JointIds =
        {
            XRHandJointID.Wrist, XRHandJointID.Palm,
            XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip,
            XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip,
            XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip,
            XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip,
            XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip
        };

        private XRHandSubsystem _subsystem;
        public bool IsAvailable => ResolveSubsystem() != null;
        public bool LeftTracked => _subsystem != null && _subsystem.running && _subsystem.leftHand.isTracked;
        public bool RightTracked => _subsystem != null && _subsystem.running && _subsystem.rightHand.isTracked;
        public string StateText => !IsAvailable ? "Unavailable" : LeftTracked && RightTracked ? "Left + Right" : LeftTracked ? "Left" : RightTracked ? "Right" : "Ready · not tracked";

        public void Refresh() => ResolveSubsystem();

        public bool TryCapture(string handName, string streamId, long sequence, out SpatialHandFrame frame)
        {
            frame = null;
            var subsystem = ResolveSubsystem();
            if (subsystem == null) return false;
            var hand = string.Equals(handName, "left", StringComparison.OrdinalIgnoreCase) ? subsystem.leftHand : subsystem.rightHand;
            if (!hand.isTracked) return false;

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var joints = new SpatialHandJoint[JointIds.Length];
            SpatialPose wrist = null;
            for (var i = 0; i < JointIds.Length; i++)
            {
                var id = JointIds[i];
                var valid = hand.GetJoint(id).TryGetPose(out var pose);
                SpatialVector3 position = null;
                SpatialQuaternion orientation = null;
                if (valid)
                {
                    position = SpatialCoordinateConverter.ToCanonicalPosition(pose.position);
                    orientation = SpatialCoordinateConverter.ToCanonicalRotation(pose.rotation);
                    if (id == XRHandJointID.Wrist)
                        wrist = new SpatialPose { space = "local", timestamp = timestamp, position = position, orientation = orientation };
                }
                joints[i] = new SpatialHandJoint
                {
                    id = id.ToString(),
                    valid = valid,
                    position = position,
                    orientation = orientation
                };
            }

            frame = new SpatialHandFrame
            {
                streamId = streamId,
                sequence = sequence,
                timestamp = timestamp,
                space = "local",
                hand = handName,
                valid = wrist != null,
                wrist = wrist,
                joints = joints
            };
            return true;
        }

        private XRHandSubsystem ResolveSubsystem()
        {
            if (_subsystem != null && _subsystem.running) return _subsystem;
            Subsystems.Clear();
            SubsystemManager.GetSubsystems(Subsystems);
            _subsystem = null;
            foreach (var subsystem in Subsystems)
            {
                if (subsystem != null && subsystem.running)
                {
                    _subsystem = subsystem;
                    break;
                }
            }
            return _subsystem;
        }
    }
}
