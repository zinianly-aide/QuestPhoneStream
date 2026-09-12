using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class ObjectScanIntrinsics
    {
        public bool valid;
        public float fx;
        public float fy;
        public float cx;
        public float cy;
        public int sensorWidth;
        public int sensorHeight;
        public float lensOffsetX;
        public float lensOffsetY;
    }

    [Serializable]
    public sealed class ObjectScanFrameMetadata
    {
        public int index;
        public string image;
        public long timestampMs;
        public int width;
        public int height;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public ObjectScanIntrinsics intrinsics;
    }

    [Serializable]
    public sealed class ObjectScanManifest
    {
        public string version = "qps-object-scan-poc-v1";
        public string sessionId;
        public string createdUtc;
        public string cameraSource = "camera.rgb";
        public string coordinateSystem = "unity-world-y-up-z-forward";
        public int frameCount;
        public List<ObjectScanFrameMetadata> frames = new List<ObjectScanFrameMetadata>();
    }

    public static class ObjectScanSamplingPolicy
    {
        public static bool ShouldCapture(
            bool hasPrevious,
            Pose previous,
            Pose current,
            float secondsSincePrevious,
            float minSeconds,
            float minTranslationMeters,
            float minRotationDegrees,
            bool force = false)
        {
            if (force || !hasPrevious) return true;
            if (secondsSincePrevious < Mathf.Max(0f, minSeconds)) return false;

            var translation = Vector3.Distance(previous.position, current.position);
            var rotation = Quaternion.Angle(previous.rotation, current.rotation);
            return translation >= Mathf.Max(0f, minTranslationMeters) ||
                   rotation >= Mathf.Max(0f, minRotationDegrees);
        }
    }
}
