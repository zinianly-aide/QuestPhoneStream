using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Interaction.Backends.XRI;
using Object = UnityEngine.Object;

namespace QuestPhoneStream.Tests
{
    public class HandGrabPolicyTests
    {
        [Test]
        public void BareHandGrabRequiresVisibleHandleProximity()
        {
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                handle.transform.position = new Vector3(0f, -0.6f, -0.04f);
                handle.transform.localScale = new Vector3(.9f, .08f, .05f);
                Physics.SyncTransforms();
                var renderer = handle.GetComponent<Renderer>();

                Assert.IsFalse(HandGrabPolicy.TryGetVisibleHandlePoint(renderer, Vector3.zero, .045f,
                    out _, out _), "A pinch on the content surface must not start a window grab.");

                var nearHandle = renderer.bounds.center + Vector3.forward * .02f;
                Assert.IsTrue(HandGrabPolicy.TryGetVisibleHandlePoint(renderer, nearHandle, .045f,
                    out var closest, out var distance));
                Assert.LessOrEqual(distance, .045f);
                Assert.That(Vector3.Distance(closest, renderer.bounds.ClosestPoint(nearHandle)), Is.LessThan(.0001f));
            }
            finally
            {
                Object.DestroyImmediate(handle);
            }
        }
    }
}
