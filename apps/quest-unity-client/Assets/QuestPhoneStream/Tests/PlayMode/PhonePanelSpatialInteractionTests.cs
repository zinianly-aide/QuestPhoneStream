using NUnit.Framework;
using UnityEngine;

namespace QuestPhoneStream.Tests
{
    public class PhonePanelSpatialInteractionTests
    {
        [Test]
        public void ResetPosePlacesWorldSpacePanelInFrontOfCamera()
        {
            var cameraGo = new GameObject("Camera");
            var root = new GameObject("PhonePanelRoot");
            var interaction = root.AddComponent<PhonePanelSpatialInteraction>();
            interaction.defaultDistance = 1.5f;
            cameraGo.transform.position = new Vector3(2f, 1.6f, 3f);
            cameraGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            interaction.ResetPose(cameraGo.AddComponent<Camera>());

            Assert.That(Vector3.Distance(root.transform.position, cameraGo.transform.position), Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(root.transform.parent, Is.Null);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraGo);
        }

        [Test]
        public void ScreenAndGrabCollidersAreSeparateAndMutuallyExclusive()
        {
            var root = new GameObject("PhonePanelRoot");
            var screen = new GameObject("PhoneScreen");
            var frame = new GameObject("GrabHandle");
            screen.transform.SetParent(root.transform);
            frame.transform.SetParent(root.transform);
            var interaction = root.AddComponent<PhonePanelSpatialInteraction>();
            interaction.screenCollider = screen.AddComponent<BoxCollider>();
            interaction.grabCollider = frame.AddComponent<BoxCollider>();

            Assert.AreNotSame(interaction.screenCollider, interaction.grabCollider);
            Assert.IsTrue(interaction.TryBeginScreenTouch());
            Assert.IsFalse(interaction.TryBeginGrab("controller", Vector3.zero, Quaternion.identity));
            interaction.EndScreenTouch();
            Assert.IsTrue(interaction.TryBeginGrab("controller", Vector3.zero, Quaternion.identity));
            Assert.IsFalse(interaction.TryBeginScreenTouch());
            Object.DestroyImmediate(root);
        }

        [Test]
        public void TwoHandTransformClampsUniformScaleAndReturnsToSingleHand()
        {
            var root = new GameObject("PhonePanelRoot");
            var interaction = root.AddComponent<PhonePanelSpatialInteraction>();
            interaction.minScale = 0.5f;
            interaction.maxScale = 2.5f;
            Assert.IsTrue(interaction.TryBeginGrab("left", Vector3.zero, Quaternion.identity));
            Assert.IsTrue(interaction.TryBeginGrab("right", Vector3.right, Quaternion.identity));
            Assert.AreEqual(PhonePanelSpatialInteraction.InteractionState.TwoHandTransform, interaction.State);

            interaction.UpdateGrab("right", Vector3.right * 10f, Quaternion.identity);
            Assert.That(root.transform.localScale.x, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(root.transform.localScale.y, Is.EqualTo(root.transform.localScale.x).Within(0.0001f));
            interaction.EndGrab("right");
            Assert.AreEqual(PhonePanelSpatialInteraction.InteractionState.OneHandGrab, interaction.State);
            interaction.ClearInteractionState();
            Assert.AreEqual(PhonePanelSpatialInteraction.InteractionState.Idle, interaction.State);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void DisableClearsActiveInteraction()
        {
            var root = new GameObject("PhonePanelRoot");
            var interaction = root.AddComponent<PhonePanelSpatialInteraction>();
            interaction.TryBeginGrab("left", Vector3.zero, Quaternion.identity);
            root.SetActive(false);
            Assert.AreEqual(PhonePanelSpatialInteraction.InteractionState.Idle, interaction.State);
            Object.DestroyImmediate(root);
        }
    }
}
