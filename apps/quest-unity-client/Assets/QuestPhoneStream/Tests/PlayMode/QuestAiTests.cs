using NUnit.Framework;

namespace QuestPhoneStream.Tests
{
    public sealed class QuestAiTests
    {
        [Test]
        public void ParsesDirectStructuredVisionResponse()
        {
            var json = "{\"text\":\"cup\",\"objects\":[{\"label\":\"cup\",\"bbox\":{\"x\":0.1,\"y\":0.2,\"width\":0.3,\"height\":0.4},\"confidence\":0.92}],\"actions\":[]}";
            Assert.IsTrue(QuestAiResponseParser.TryParseTransportResponse(json, out var result));
            Assert.AreEqual("cup", result.text);
            Assert.AreEqual(1, result.objects.Length);
            Assert.AreEqual("cup", result.objects[0].label);
            Assert.That(result.objects[0].confidence, Is.EqualTo(0.92f).Within(0.001f));
        }

        [Test]
        public void ParsesOpenAiCompatibleContentWrapper()
        {
            var json = "{\"choices\":[{\"message\":{\"content\":\"```json\\n{\\\"text\\\":\\\"ok\\\",\\\"objects\\\":[],\\\"actions\\\":[{\\\"type\\\":\\\"label\\\",\\\"label\\\":\\\"desk\\\",\\\"confidence\\\":0.8}]}\\n```\"}}]}";
            Assert.IsTrue(QuestAiResponseParser.TryParseTransportResponse(json, out var result));
            Assert.AreEqual("ok", result.text);
            Assert.AreEqual(1, result.actions.Length);
            Assert.AreEqual("desk", result.actions[0].label);
        }

        [Test]
        public void RejectsMalformedTransportResponse()
        {
            Assert.IsFalse(QuestAiResponseParser.TryParseTransportResponse("not-json", out _));
            Assert.IsFalse(QuestAiResponseParser.TryParseTransportResponse("{\"choices\":[]}", out _));
        }

        [Test]
        public void AiCapabilityIsSeparateFromCameraAuthorization()
        {
            var registry = CapabilityRegistry.CreateQuestDefaults();
            registry.UpdateState("camera.rgb", available: true, authorized: false, active: false);
            registry.UpdateState("ai.vision", available: true, authorized: true, active: true);
            var ai = System.Array.Find(registry.All(), item => item.name == "ai.vision");
            var camera = System.Array.Find(registry.All(), item => item.name == "camera.rgb");
            Assert.IsTrue(ai.state.active);
            Assert.IsFalse(camera.state.authorized);
        }
    }
}
