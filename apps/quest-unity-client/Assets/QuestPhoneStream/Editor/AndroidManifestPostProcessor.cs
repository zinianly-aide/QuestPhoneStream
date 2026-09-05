using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace QuestPhoneStream.Editor
{
    /// <summary>
    /// Injects required attributes into the generated AndroidManifest.xml after Unity creates
    /// the Gradle project:
    ///   1. oculus.software.overlay_keyboard feature — without this, TouchScreenKeyboard.Open()
    ///      logs "Oculus overlay keyboard is disabled" and no system keyboard appears on Quest.
    ///   2. android:usesCleartextTraffic="true" on &lt;application&gt; — without this, VideoPlayer
    ///      (which uses Android's native MediaExtractor) fails with error -10000 on HTTP URLs,
    ///      even though UnityWebRequest works via insecureHttpOption.
    ///
    /// We use IPostGenerateGradleAndroidProject instead of a static Assets/Plugins/Android/AndroidManifest.xml
    /// because a partial custom manifest breaks Unity's manifest merger (missing android:name on activity).
    /// </summary>
    public class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        private const string OverlayKeyboardFeature = "oculus.software.overlay_keyboard";
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[AndroidManifestPostProcessor] Manifest not found: {manifestPath}");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);

            var manifest = doc.DocumentElement;
            if (manifest == null)
            {
                Debug.LogWarning("[AndroidManifestPostProcessor] Manifest has no root element");
                return;
            }

            bool changed = false;

            // 1. Ensure <application android:usesCleartextTraffic="true">
            var applicationElements = manifest.GetElementsByTagName("application");
            if (applicationElements.Count > 0)
            {
                var app = applicationElements[0];
                var cleartextAttr = app.Attributes?["usesCleartextTraffic", AndroidNs]
                                     ?? app.Attributes?["android:usesCleartextTraffic"];
                if (cleartextAttr == null)
                {
                    var attr = doc.CreateAttribute("android", "usesCleartextTraffic", AndroidNs);
                    attr.Value = "true";
                    app.Attributes.Append(attr);
                    changed = true;
                    Debug.Log("[AndroidManifestPostProcessor] Added android:usesCleartextTraffic=\"true\" to <application>");
                }
                else
                {
                    Debug.Log("[AndroidManifestPostProcessor] usesCleartextTraffic already present, skipping");
                }
            }
            else
            {
                Debug.LogWarning("[AndroidManifestPostProcessor] No <application> element found in manifest");
            }

            // 2. Ensure <uses-feature android:name="oculus.software.overlay_keyboard" />
            bool featureExists = false;
            var features = manifest.GetElementsByTagName("uses-feature");
            foreach (XmlNode feature in features)
            {
                var nameAttr = feature.Attributes?["name", AndroidNs]
                                ?? feature.Attributes?["android:name"];
                if (nameAttr?.Value == OverlayKeyboardFeature)
                {
                    featureExists = true;
                    break;
                }
            }

            if (!featureExists)
            {
                var featureElement = doc.CreateElement("uses-feature");

                var nameAttribute = doc.CreateAttribute("android", "name", AndroidNs);
                nameAttribute.Value = OverlayKeyboardFeature;
                featureElement.Attributes.Append(nameAttribute);

                var requiredAttribute = doc.CreateAttribute("android", "required", AndroidNs);
                requiredAttribute.Value = "false";
                featureElement.Attributes.Append(requiredAttribute);

                manifest.AppendChild(featureElement);
                changed = true;
                Debug.Log("[AndroidManifestPostProcessor] Added oculus.software.overlay_keyboard to AndroidManifest.xml");
            }
            else
            {
                Debug.Log("[AndroidManifestPostProcessor] overlay_keyboard feature already present, skipping");
            }

            if (changed)
            {
                doc.Save(manifestPath);
                Debug.Log("[AndroidManifestPostProcessor] Manifest updated and saved");
            }
        }
    }
}
