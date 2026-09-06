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
            var defaults = MediaVideoProfile.From(items[0]);
            Assert.AreEqual(ProjectionMode.Flat, defaults.projection);
            Assert.AreEqual(360, defaults.fov);
            Assert.AreEqual(StereoMode.Mono, defaults.stereo);
            Assert.AreEqual(EyeOrder.Lr, defaults.eyeOrder);
            StringAssert.Contains("/v1/media/media_a/play-token", MediaUrlBuilder.PlayToken("http://phone:8788", "media_a"));
            StringAssert.Contains("cap=short", MediaUrlBuilder.Content("http://phone:8788", "media_a", "short"));
        }

        [Test]
        public void MetadataMapsAllVrModesAndEyeOrders()
        {
            var items = MediaCatalogJson.Parse("[{\"id\":\"vr\",\"projection\":\"equirectangular\",\"fov\":180,\"stereo\":\"sbs\",\"eyeOrder\":\"rl\"}]");
            var profile = MediaVideoProfile.From(items[0]);
            Assert.AreEqual(ProjectionMode.Equirectangular, profile.projection);
            Assert.AreEqual(180, profile.fov);
            Assert.AreEqual(StereoMode.Sbs, profile.stereo);
            Assert.AreEqual(EyeOrder.Rl, profile.eyeOrder);
        }

        [Test]
        public void PanoramicRotationMatchesUnitySkyboxYawSign()
        {
            Assert.That(VrMediaRenderer.CameraYawForForward(Vector3.left), Is.EqualTo(-90f).Within(0.001f));
            Assert.That(VrMediaRenderer.PanoramicRotationForForward(Vector3.forward), Is.EqualTo(0f).Within(0.001f));
            Assert.That(VrMediaRenderer.PanoramicRotationForForward(Vector3.right), Is.EqualTo(90f).Within(0.001f));
            Assert.That(VrMediaRenderer.PanoramicRotationForForward(Vector3.back), Is.EqualTo(180f).Within(0.001f));
            Assert.That(VrMediaRenderer.PanoramicRotationForForward(Vector3.left), Is.EqualTo(270f).Within(0.001f));
            Assert.That(VrMediaRenderer.PanoramicRotationForForward(new Vector3(1f, 4f, 0f)), Is.EqualTo(90f).Within(0.001f));
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
            Assert.IsFalse(playback.vrRenderer.IsVrVisible);
            Assert.IsFalse(playback.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator VrRendererReusesOneSurfaceAndReturnsToFlat()
        {
            var flatObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flatObject.transform.SetParent(_object.transform, false);
            var flat = _object.AddComponent<FlatMediaRenderer>();
            flat.targetRenderer = flatObject.GetComponent<Renderer>();
            var vr = _object.AddComponent<VrMediaRenderer>();
            vr.Initialize(null, flat);
            vr.vrBackend = VrBackend.SphereCustom;
            var texture = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32);
            texture.Create();
            vr.Apply(texture, ProjectionMode.Equirectangular, 360, StereoMode.Sbs, EyeOrder.Lr);
            Assert.IsTrue(vr.IsVrVisible);
            Assert.AreEqual(1, _object.GetComponentsInChildren<VrMediaRenderer>(true).Length);
            vr.Apply(texture, ProjectionMode.Equirectangular, 180, StereoMode.Mono, EyeOrder.Rl);
            Assert.IsTrue(vr.IsVrVisible);
            vr.Apply(texture, ProjectionMode.Flat, 360, StereoMode.Mono, EyeOrder.Lr);
            Assert.IsFalse(vr.IsVrVisible);
            Assert.IsTrue(flat.targetRenderer.enabled);
            vr.Release();
            texture.Release();
            Object.Destroy(texture);
            Object.Destroy(flatObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnityPanoramicBackendUsesSkyboxForAllFlatAndStereoModes()
        {
            var originalSkybox = RenderSettings.skybox;
            var flatObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flatObject.transform.SetParent(_object.transform, false);
            var flat = _object.AddComponent<FlatMediaRenderer>();
            flat.targetRenderer = flatObject.GetComponent<Renderer>();
            var cameraObject = new GameObject("xr camera");
            cameraObject.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            var vr = _object.AddComponent<VrMediaRenderer>();
            vr.Initialize(camera, flat);
            vr.vrBackend = VrBackend.UnityPanoramic;
            var texture = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32);
            texture.Create();
            Material panoramicMaterial = null;

            try
            {
                Assert.IsNotNull(Shader.Find("Skybox/Panoramic"));
                var modes = new[]
                {
                    new { Fov = 360, Stereo = StereoMode.Mono },
                    new { Fov = 360, Stereo = StereoMode.Sbs },
                    new { Fov = 180, Stereo = StereoMode.Mono },
                    new { Fov = 180, Stereo = StereoMode.Sbs }
                };
                foreach (var mode in modes)
                {
                    vr.Apply(texture, ProjectionMode.Equirectangular, mode.Fov, mode.Stereo, EyeOrder.Lr);
                    Assert.IsTrue(vr.IsVrVisible);
                    Assert.IsTrue(vr.IsPanoramicVisible);
                    Assert.IsFalse(flat.targetRenderer.enabled);
                    Assert.AreSame(texture, RenderSettings.skybox.GetTexture("_MainTex"));
                    panoramicMaterial = RenderSettings.skybox;
                    Assert.AreEqual(mode.Fov == 180 ? 1f : 0f, RenderSettings.skybox.GetFloat("_ImageType"));
                    Assert.AreEqual(mode.Stereo == StereoMode.Sbs ? 1f : 0f, RenderSettings.skybox.GetFloat("_Layout"));
                }
                Assert.That(RenderSettings.skybox.GetFloat("_Rotation"), Is.EqualTo(30f).Within(0.001f));

                vr.Apply(texture, ProjectionMode.Flat, 360, StereoMode.Mono, EyeOrder.Lr);
                Assert.IsFalse(vr.IsVrVisible);
                Assert.IsFalse(vr.IsPanoramicVisible);
                Assert.AreSame(originalSkybox, RenderSettings.skybox);
                Assert.IsNotNull(panoramicMaterial);
                Assert.IsNull(panoramicMaterial.GetTexture("_MainTex"));
                Assert.IsTrue(flat.targetRenderer.enabled);
            }
            finally
            {
                vr.Release();
                Assert.AreSame(originalSkybox, RenderSettings.skybox);
                texture.Release();
                Object.Destroy(texture);
                Object.Destroy(flatObject);
                Object.Destroy(cameraObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ManualPanoramicRecenterUpdatesMaterialRotation()
        {
            var originalSkybox = RenderSettings.skybox;
            var flatObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flatObject.transform.SetParent(_object.transform, false);
            var flat = _object.AddComponent<FlatMediaRenderer>();
            flat.targetRenderer = flatObject.GetComponent<Renderer>();
            var cameraObject = new GameObject("xr camera");
            var camera = cameraObject.AddComponent<Camera>();
            var vr = _object.AddComponent<VrMediaRenderer>();
            vr.Initialize(camera, flat);
            vr.vrBackend = VrBackend.UnityPanoramic;
            var texture = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32);
            texture.Create();

            try
            {
                camera.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                vr.Apply(texture, ProjectionMode.Equirectangular, 360, StereoMode.Mono, EyeOrder.Lr);
                var firstRotation = RenderSettings.skybox.GetFloat("_Rotation");
                camera.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Assert.IsTrue(vr.RecenterPanoramic());
                var secondRotation = RenderSettings.skybox.GetFloat("_Rotation");
                Assert.AreNotEqual(firstRotation, secondRotation);
                Assert.That(secondRotation, Is.EqualTo(90f).Within(0.001f));
            }
            finally
            {
                vr.Release();
                Assert.AreSame(originalSkybox, RenderSettings.skybox);
                texture.Release();
                Object.Destroy(texture);
                Object.Destroy(flatObject);
                Object.Destroy(cameraObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PanoramicFailureKeepsFlatRendererAndOriginalSkybox()
        {
            var originalSkybox = RenderSettings.skybox;
            var flatObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flatObject.transform.SetParent(_object.transform, false);
            var flat = _object.AddComponent<FlatMediaRenderer>();
            flat.targetRenderer = flatObject.GetComponent<Renderer>();
            var cameraObject = new GameObject("xr camera");
            var camera = cameraObject.AddComponent<Camera>();
            var invalidMaterial = new Material(Shader.Find("Unlit/Color"));
            var vr = _object.AddComponent<VrMediaRenderer>();
            vr.Initialize(camera, flat, null, invalidMaterial);
            vr.vrBackend = VrBackend.UnityPanoramic;
            var texture = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32);
            texture.Create();

            try
            {
                vr.Apply(texture, ProjectionMode.Equirectangular, 360, StereoMode.Mono, EyeOrder.Lr);
                Assert.IsFalse(vr.IsVrVisible);
                Assert.IsFalse(vr.IsPanoramicVisible);
                Assert.IsTrue(flat.targetRenderer.enabled);
                Assert.AreSame(texture, flat.targetRenderer.material.mainTexture);
                Assert.AreSame(originalSkybox, RenderSettings.skybox);
            }
            finally
            {
                vr.Release();
                Object.Destroy(invalidMaterial);
                texture.Release();
                Object.Destroy(texture);
                Object.Destroy(flatObject);
                Object.Destroy(cameraObject);
            }

            yield return null;
        }
    }
}
