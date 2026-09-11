using NUnit.Framework;

namespace QuestPhoneStream.Tests
{
    public sealed class P3DiagnosticsTests
    {
        [Test]
        public void DisplayTextIncludesUnifiedP2AndP3Metrics()
        {
            var snapshot = new QuestDiagnosticsSnapshot
            {
                handState = "Left + Right",
                spatialFastOpen = true,
                spatialReliableOpen = true,
                anchorState = "2 anchors · 1 subscribers · reliable",
                anchorCount = 2,
                anchorSubscribers = 1,
                depthState = "Active",
                depthSubscribers = 1,
                interactionState = "Subscribed",
                interactionSubscribers = 1,
                sixDofState = "Unavailable · external provider required",
                gaussianState = "1200 splats · POC",
                gaussianSplatCount = 1200,
                gaussianLoadMs = 42
            };

            var text = snapshot.ToDisplayText();
            StringAssert.Contains("Spatial Data Plane", text);
            StringAssert.Contains("Reliable channel: open", text);
            StringAssert.Contains("Anchors:", text);
            StringAssert.Contains("Depth:", text);
            StringAssert.Contains("Interaction:", text);
            StringAssert.Contains("6DoF:", text);
            StringAssert.Contains("3DGS:", text);
            StringAssert.Contains("1200", text);
            StringAssert.Contains("42 ms", text);
        }
    }
}
