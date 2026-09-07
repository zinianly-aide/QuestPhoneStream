using System.Linq;
using NUnit.Framework;

namespace QuestPhoneStream.Tests
{
    public sealed class SpatialMediaRoutingTests
    {
        [Test]
        public void SixDofMetadataRoundTripsAndRoutesAwayFromVideoPlayer()
        {
            const string json = "[{\"id\":\"six\",\"name\":\"scene.v3c\",\"mimeType\":\"application/octet-stream\",\"size\":10,\"seekable\":true,\"projection\":\"flat\",\"fov\":360,\"stereo\":\"mono\",\"eyeOrder\":\"lr\",\"spatialFormat\":\"v3c\",\"manifestUrl\":\"manifest.json\",\"referenceSpace\":\"local\",\"spatialBounds\":{\"centerX\":1,\"centerY\":2,\"centerZ\":3,\"sizeX\":4,\"sizeY\":5,\"sizeZ\":6}}]";
            var item = MediaCatalogJson.Parse(json).Single();
            Assert.AreEqual(MediaRouteKind.SixDof, item.Route);
            Assert.IsTrue(item.IsSixDof);
            Assert.AreEqual("manifest.json", item.manifestUrl);
            Assert.AreEqual("local", item.referenceSpace);
            Assert.AreEqual(6f, item.spatialBounds.sizeZ);
        }

        [Test]
        public void GaussianMetadataRoundTripsAndRoutesToPocRenderer()
        {
            const string json = "[{\"id\":\"gs\",\"name\":\"scene.ply\",\"mimeType\":\"application/x-ply\",\"size\":10,\"seekable\":true,\"spatialFormat\":\"ply-splat\",\"manifestUrl\":\"\",\"referenceSpace\":\"local\"}]";
            var item = MediaCatalogJson.Parse(json).Single();
            Assert.AreEqual(MediaRouteKind.GaussianSplat, item.Route);
            Assert.IsTrue(item.IsGaussianSplat);
            Assert.AreEqual("3DGS POC", item.RouteLabel);
        }

        [Test]
        public void NormalMediaStillRoutesToVideo()
        {
            const string json = "[{\"id\":\"video\",\"name\":\"clip.mp4\",\"mimeType\":\"video/mp4\",\"size\":10,\"seekable\":true,\"spatialFormat\":\"\"}]";
            Assert.AreEqual(MediaRouteKind.Video, MediaCatalogJson.Parse(json).Single().Route);
        }

        [Test]
        public void RelativeManifestFallsBackToAuthorizedContent()
        {
            Assert.AreEqual(
                "fallback",
                MediaUrlBuilder.ResolveManifest("http://phone:8788", "assets/manifest.json", "fallback"));
            Assert.AreEqual(
                "https://cdn.example.test/scene/manifest.json",
                MediaUrlBuilder.ResolveManifest("http://phone:8788", "https://cdn.example.test/scene/manifest.json", "fallback"));
            Assert.AreEqual(
                "fallback",
                MediaUrlBuilder.ResolveManifest("http://phone:8788", "javascript:alert(1)", "fallback"));
        }

        [Test]
        public void DestroyedInteractionTargetClearsTrackedSelection()
        {
            var tracker = new SpatialInteractionTracker();
            tracker.Update("left", "cube", true).ToArray();
            var reset = tracker.RemoveTarget("cube");
            Assert.AreEqual(1, reset.Count);
            Assert.AreEqual("left", reset[0].hand);
            Assert.IsTrue(reset[0].wasPressed);
            Assert.IsNull(tracker.PreviousTarget("left"));
        }

        [Test]
        public void GaussianSourceValidationRejectsNonNetworkCustomSchemes()
        {
            Assert.IsFalse(GaussianSplatPocRenderer.TryValidateSource("javascript:alert(1)", out _, out _));
            Assert.IsTrue(GaussianSplatPocRenderer.TryValidateSource("http://phone:8788/v1/media/gs/content?cap=x", out _, out _));
        }
    }
}
