using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuestPhoneStream.Tests
{
    public class MediaPlaybackTests
    {
        private GameObject _object;

        [SetUp]
        public void SetUp() { _object = new GameObject("media playback test"); }

        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(_object); yield return null; }

        [Test]
        public void CatalogJsonAndUrlsAreStable()
        {
            var items = MediaCatalogJson.Parse("[{\"id\":\"media_a\",\"name\":\"demo.mp4\",\"mimeType\":\"video/mp4\",\"size\":12,\"seekable\":true}]");
            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("media_a", items[0].id);
            StringAssert.Contains("/v1/media/media_a/play-token", MediaUrlBuilder.PlayToken("http://phone:8788", "media_a"));
            StringAssert.Contains("cap=short", MediaUrlBuilder.Content("http://phone:8788", "media_a", "short"));
        }

        [UnityTest]
        public IEnumerator StopReleasesMediaResourcesAndLeavesPhoneMode()
        {
            var playback = _object.AddComponent<MediaPlaybackController>();
            yield return null;
            playback.Stop();
            Assert.AreEqual(MediaPlaybackState.Idle, playback.State);
            Assert.IsFalse(playback.IsMediaMode);
            Assert.IsNull(playback.renderer.RenderTexture);
        }
    }
}
