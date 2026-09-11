using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace QuestPhoneStream.Tests
{
    public sealed class HandTrackingTests
    {
        [Test]
        public void JointLayoutContainsTwentySixOpenXrJoints()
        {
            Assert.AreEqual(26, HandTrackingProvider.JointIds.Length);
            Assert.AreEqual(26, HandTrackingProvider.JointIds.Distinct().Count());
            CollectionAssert.Contains(HandTrackingProvider.JointIds, XRHandJointID.Wrist);
            CollectionAssert.Contains(HandTrackingProvider.JointIds, XRHandJointID.Palm);
            CollectionAssert.Contains(HandTrackingProvider.JointIds, XRHandJointID.ThumbTip);
            CollectionAssert.Contains(HandTrackingProvider.JointIds, XRHandJointID.LittleTip);
        }

        [Test]
        public void HandFrameRoundTripsTrackingAndJointValidity()
        {
            var frame = new SpatialHandFrame
            {
                streamId = "hand-sub",
                sequence = 7,
                timestamp = 1234,
                hand = "left",
                valid = true,
                wrist = SpatialCoordinateConverter.ToCanonicalPose(Vector3.one, Quaternion.identity, 1234),
                joints = new[]
                {
                    new SpatialHandJoint
                    {
                        id = XRHandJointID.Wrist.ToString(),
                        valid = true,
                        position = new SpatialVector3 { x = 1, y = 2, z = -3 },
                        orientation = new SpatialQuaternion { w = 1 }
                    },
                    new SpatialHandJoint { id = XRHandJointID.IndexTip.ToString(), valid = false }
                }
            };

            var decoded = SpatialHandFrame.FromJson(frame.ToJson());
            Assert.AreEqual("xr.hand.pose", decoded.capability);
            Assert.AreEqual("left", decoded.hand);
            Assert.AreEqual(7, decoded.sequence);
            Assert.IsTrue(decoded.valid);
            Assert.AreEqual(2, decoded.joints.Length);
            Assert.IsTrue(decoded.joints[0].valid);
            Assert.IsFalse(decoded.joints[1].valid);
            Assert.AreEqual("local", decoded.wrist.space);
        }

        [Test]
        public void HandCapabilityCannotBecomeActiveBeforeRuntimeAvailability()
        {
            var registry = CapabilityRegistry.CreateQuestDefaults();
            var initial = Array.Find(registry.All(), value => value.name == "xr.hand.pose");
            Assert.IsNotNull(initial);
            Assert.IsFalse(initial.state.available);
            Assert.IsFalse(initial.state.active);

            Assert.IsTrue(registry.UpdateState("xr.hand.pose", available: true, authorized: true, active: true));
            var active = Array.Find(registry.All(), value => value.name == "xr.hand.pose");
            Assert.IsTrue(active.state.available);
            Assert.IsTrue(active.state.authorized);
            Assert.IsTrue(active.state.active);

            Assert.IsTrue(registry.UpdateState("xr.hand.pose", available: false));
            var unavailable = Array.Find(registry.All(), value => value.name == "xr.hand.pose");
            Assert.IsFalse(unavailable.state.available);
            Assert.IsFalse(unavailable.state.active);
        }
    }
}
