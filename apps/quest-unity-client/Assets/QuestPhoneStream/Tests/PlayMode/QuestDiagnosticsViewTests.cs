using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using QuestPhoneStream;

namespace QuestPhoneStream.Tests
{
    public sealed class QuestDiagnosticsViewTests
    {
        [Test]
        public void SelectorsFilterOnlyTheirReadOnlySnapshotCategories()
        {
            var snapshot = new QuestDiagnosticsSnapshot {
                nsdState = "Searching",
                questPresenceState = "Published",
                questPresencePort = 45123,
                wirelessAdbState = "Enabled",
                peerState = "Connected",
                videoWidth = 1920,
                videoHeight = 1080,
                xrActive = true,
                anchorState = "Ready"
            };
            var discovery = snapshot.ToDisplayText(QuestDiagnosticsView.Discovery);
            StringAssert.Contains("Quest mDNS: Published (port 45123)", discovery);
            StringAssert.Contains("Wireless ADB: Enabled", discovery);
            StringAssert.Contains("WebRTC peer: Connected", discovery);
            StringAssert.DoesNotContain("Resolution: 1920x1080", discovery);
            var video = snapshot.ToDisplayText(QuestDiagnosticsView.Streaming);
            StringAssert.Contains("Resolution: 1920x1080", video);
            StringAssert.DoesNotContain("Quest mDNS:", video);
            var spatial = snapshot.ToDisplayText(QuestDiagnosticsView.Spatial);
            StringAssert.Contains("OpenXR active: True", spatial);
            StringAssert.Contains("Anchors: Ready", spatial);
            StringAssert.DoesNotContain("WebRTC peer:", spatial);
            StringAssert.Contains("Capabilities", snapshot.ToDisplayText(QuestDiagnosticsView.Capabilities));
            StringAssert.Contains("Quest mDNS:", snapshot.ToDisplayText());
        }

        [Test]
        public void HudSelectionChangesVisibleSectionAndRejectsInvalidView()
        {
            var canvasObject = new GameObject("DevCanvas", typeof(RectTransform), typeof(Canvas));
            var host = new GameObject("DevHudHost");
            try
            {
                var diagnostic = host.AddComponent<QuestDiagnostics>();
                var hud = host.AddComponent<DeveloperHud>();
                hud.Initialize(canvasObject.GetComponent<Canvas>(), diagnostic, null, () => {});
                hud.Show();
                hud.SelectView(QuestDiagnosticsView.Streaming);
                Assert.AreEqual(QuestDiagnosticsView.Streaming, hud.SelectedView);
                var labels = canvasObject.GetComponentsInChildren<Text>(true);
                Assert.Greater(labels.Length, 5);
                var selected = 0;
                foreach (var button in canvasObject.GetComponentsInChildren<Button>(true))
                    if (button.name == "Button_Streaming") selected++;
                Assert.AreEqual(1, selected);
                hud.SelectView((QuestDiagnosticsView)99);
                Assert.AreEqual(QuestDiagnosticsView.All, hud.SelectedView);
                Assert.AreEqual(QuestDiagnosticsView.All, DeveloperHud.SafeView(-1));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(canvasObject);
                PlayerPrefs.DeleteKey("QuestPhoneStream_DiagnosticsView_v1");
            }
        }
    }
}
