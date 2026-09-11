using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Interaction;

namespace QuestPhoneStream.Tests.PlayMode
{
    public sealed class ScreenGeometryTests
    {
        [Test]
        public void SurfaceAspect_SwapsPortraitAndLandscapeWithoutRotatingPanelRoot()
        {
            var root = new GameObject("SpatialPanelRoot");
            var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "Surface";
            surface.transform.SetParent(root.transform, false);
            surface.transform.localScale = new Vector3(.72f, 1.6f, 1f);
            var initialRootRotation = root.transform.rotation;

            try
            {
                var shell = root.AddComponent<SpatialPanelShell>();
                shell.Initialize(surface.transform, surface.GetComponent<Collider>(), null, null);

                shell.SetSurfaceAspect(576, 1280);
                Assert.That(surface.transform.localScale.x, Is.EqualTo(.72f).Within(.001f));
                Assert.That(surface.transform.localScale.y, Is.EqualTo(1.6f).Within(.001f));

                shell.SetSurfaceAspect(1280, 576);
                Assert.That(surface.transform.localScale.x, Is.EqualTo(1.6f).Within(.001f));
                Assert.That(surface.transform.localScale.y, Is.EqualTo(.72f).Within(.001f));
                Assert.That(root.transform.rotation, Is.EqualTo(initialRootRotation));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SurfaceAspect_PreservesSurfaceScaleSigns()
        {
            var root = new GameObject("SpatialPanelRoot");
            var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.transform.SetParent(root.transform, false);
            surface.transform.localScale = new Vector3(-.72f, 1.6f, 1f);

            try
            {
                var shell = root.AddComponent<SpatialPanelShell>();
                shell.Initialize(surface.transform, surface.GetComponent<Collider>(), null, null);
                shell.SetSurfaceAspect(1280, 576);

                Assert.Less(surface.transform.localScale.x, 0f);
                Assert.Greater(surface.transform.localScale.y, 0f);
                Assert.That(Mathf.Abs(surface.transform.localScale.x / surface.transform.localScale.y),
                    Is.EqualTo(1280f / 576f).Within(.002f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
