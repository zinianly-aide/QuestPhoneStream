using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace QuestPhoneStream.Tests
{
    public class XrUiRigTests
    {
        [UnityTest]
        public IEnumerator BootstrapCreatesOneInputChainWithTwoRays()
        {
            var root = new GameObject("XR test");
            var cameraObject = new GameObject("Camera without tag");
            var camera = cameraObject.AddComponent<Camera>();
            var receiver = root.AddComponent<QuestWebRtcReceiver>();
            receiver.enabled = false; // This test exercises the rig, not native WebRTC initialization.
            var rig = root.AddComponent<QuestXrUiRig>();
            rig.actionAsset = Resources.Load<InputActionAsset>("QuestUi");
            try
            {
                rig.Initialize(camera, receiver);
                var origin = rig.Origin;
                rig.Initialize(camera, receiver);
                Assert.AreSame(origin, rig.Origin);
                Assert.AreSame(camera, origin.Camera);
                Assert.IsNotNull(rig.UiEvents.GetComponent<XRUIInputModule>());
                var rays = origin.GetComponentsInChildren<XRRayInteractor>();
                Assert.AreEqual(2, rays.Length);
                foreach (var ray in rays) Assert.IsTrue(ray.enableUIInteraction);
                Assert.IsTrue(rig.Actions.FindAction("Open Settings").enabled);
                Assert.Greater(rig.Actions.FindAction("LeftHand UI Click").bindings.Count, 0);
                Assert.Greater(rig.Actions.FindAction("RightHand UI Point Position").bindings.Count, 0);
                yield return null;
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
