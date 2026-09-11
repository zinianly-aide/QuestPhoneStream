using NUnit.Framework;
using UnityEngine;

namespace QuestPhoneStream.Tests
{
    public sealed class ObjectScanPocTests
    {
        [Test]
        public void SamplingPolicy_CapturesFirstFrame()
        {
            Assert.IsTrue(ObjectScanSamplingPolicy.ShouldCapture(
                false, default, new Pose(Vector3.zero, Quaternion.identity),
                0f, 0.35f, 0.06f, 8f));
        }

        [Test]
        public void SamplingPolicy_RejectsNearlyDuplicatePose()
        {
            var previous = new Pose(Vector3.zero, Quaternion.identity);
            var current = new Pose(new Vector3(0.01f, 0f, 0f), Quaternion.Euler(0f, 2f, 0f));
            Assert.IsFalse(ObjectScanSamplingPolicy.ShouldCapture(
                true, previous, current, 1f, 0.35f, 0.06f, 8f));
        }

        [Test]
        public void SamplingPolicy_AcceptsOrbitRotationWithoutLargeTranslation()
        {
            var previous = new Pose(Vector3.zero, Quaternion.identity);
            var current = new Pose(new Vector3(0.01f, 0f, 0f), Quaternion.Euler(0f, 12f, 0f));
            Assert.IsTrue(ObjectScanSamplingPolicy.ShouldCapture(
                true, previous, current, 1f, 0.35f, 0.06f, 8f));
        }

        [Test]
        public void Manifest_SerializesReconstructionInputs()
        {
            var manifest = new ObjectScanManifest { sessionId = "scan-test", createdUtc = "2026-09-11T00:00:00Z" };
            manifest.frames.Add(new ObjectScanFrameMetadata
            {
                index = 0,
                image = "frames/000000.jpg",
                timestampMs = 42,
                width = 1280,
                height = 960,
                cameraPosition = new Vector3(1f, 2f, 3f),
                cameraRotation = Quaternion.identity,
                intrinsics = new ObjectScanIntrinsics
                {
                    valid = true,
                    fx = 700f,
                    fy = 701f,
                    cx = 640f,
                    cy = 480f,
                    sensorWidth = 1280,
                    sensorHeight = 960
                }
            });
            manifest.frameCount = 1;

            var json = JsonUtility.ToJson(manifest);
            StringAssert.Contains("qps-object-scan-poc-v1", json);
            StringAssert.Contains("000000.jpg", json);
            StringAssert.Contains("\"fx\":700", json);
            StringAssert.Contains("cameraPosition", json);
        }
    }
}
