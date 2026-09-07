using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace QuestPhoneStream.Editor
{
    /// <summary>Applies Quest-specific generated Android manifest requirements.</summary>
    public class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        private const string OverlayKeyboardFeature = "oculus.software.overlay_keyboard";
        private const string PassthroughFeature = "com.oculus.feature.PASSTHROUGH";
        private const string HeadsetCameraPermission = "horizonos.permission.HEADSET_CAMERA";
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

            var changed = false;
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
                    Debug.Log("[AndroidManifestPostProcessor] Added android:usesCleartextTraffic=\"true\"");
                }
                else if (!string.Equals(cleartextAttr.Value, "true", System.StringComparison.OrdinalIgnoreCase))
                {
                    cleartextAttr.Value = "true";
                    changed = true;
                    Debug.Log("[AndroidManifestPostProcessor] Changed android:usesCleartextTraffic to true");
                }
            }
            else
            {
                Debug.LogWarning("[AndroidManifestPostProcessor] No <application> element found in manifest");
            }

            if (!HasNamedElement(manifest, "uses-feature", OverlayKeyboardFeature))
            {
                var featureElement = doc.CreateElement("uses-feature");
                AppendAndroidAttribute(doc, featureElement, "name", OverlayKeyboardFeature);
                AppendAndroidAttribute(doc, featureElement, "required", "false");
                manifest.AppendChild(featureElement);
                changed = true;
                Debug.Log("[AndroidManifestPostProcessor] Added oculus.software.overlay_keyboard");
            }

            // AI Vision is optional for the rest of QuestPhoneStream, so advertise
            // passthrough support without making the whole app uninstallable on a
            // device/runtime that does not expose it.
            if (!HasNamedElement(manifest, "uses-feature", PassthroughFeature))
            {
                var passthroughElement = doc.CreateElement("uses-feature");
                AppendAndroidAttribute(doc, passthroughElement, "name", PassthroughFeature);
                AppendAndroidAttribute(doc, passthroughElement, "required", "false");
                manifest.AppendChild(passthroughElement);
                changed = true;
                Debug.Log("[AndroidManifestPostProcessor] Added com.oculus.feature.PASSTHROUGH");
            }

            // Passthrough Camera Access requires this explicit Horizon OS permission.
            // Declaring it does not authorize access; QuestVisionService still requests
            // user permission at runtime before camera.rgb can become authorized/active.
            if (!HasNamedElement(manifest, "uses-permission", HeadsetCameraPermission))
            {
                var permissionElement = doc.CreateElement("uses-permission");
                AppendAndroidAttribute(doc, permissionElement, "name", HeadsetCameraPermission);
                manifest.AppendChild(permissionElement);
                changed = true;
                Debug.Log("[AndroidManifestPostProcessor] Added HEADSET_CAMERA permission declaration");
            }

            if (changed)
            {
                doc.Save(manifestPath);
                Debug.Log("[AndroidManifestPostProcessor] Manifest updated and saved");
            }
        }

        private static bool HasNamedElement(XmlElement manifest, string tagName, string expectedName)
        {
            var nodes = manifest.GetElementsByTagName(tagName);
            foreach (XmlNode node in nodes)
            {
                var nameAttr = node.Attributes?["name", AndroidNs] ?? node.Attributes?["android:name"];
                if (nameAttr?.Value == expectedName) return true;
            }
            return false;
        }

        private static void AppendAndroidAttribute(XmlDocument doc, XmlElement element, string name, string value)
        {
            var attribute = doc.CreateAttribute("android", name, AndroidNs);
            attribute.Value = value;
            element.Attributes.Append(attribute);
        }
    }
}
