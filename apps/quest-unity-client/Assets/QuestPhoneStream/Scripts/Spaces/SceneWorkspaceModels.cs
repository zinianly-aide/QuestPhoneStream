using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    [Serializable]
    public sealed class SceneObjectRecord
    {
        public string id;
        public string assetId;
        // For B2 decorations, the anchored Hefu furniture id. Empty for regular objects.
        public string anchorId;
        public string label;
        public string kind = "furniture";
        public bool editable = true;
        public bool deleted;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
    }

    [Serializable]
    public sealed class SceneWorkspaceRecord
    {
        public int schemaVersion = 1;
        public string id;
        public string displayName;
        public string sourceType = "blank"; // blank, cad-bundle, room-scan
        public string assetKey;
        public string decorationPreset = ""; // empty or birthday-v1
        public int revision = 1;
        public long createdUtcMs;
        public long modifiedUtcMs;
        public float ceilingMeters = 2.70f;
        public bool isDeleted;
        public List<SceneObjectRecord> objects = new List<SceneObjectRecord>();
    }

    [Serializable]
    public sealed class SceneWorkspaceIndex
    {
        public int schemaVersion = 1;
        public string activeSceneId;
        public List<SceneWorkspaceRecord> scenes = new List<SceneWorkspaceRecord>();
    }

    public static class SceneWorkspaceValidation
    {
        public static bool SafeId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 80) return false;
            foreach (char c in value)
                if (!(char.IsLetterOrDigit(c) && c < 128) && c != '-' && c != '_') return false;
            return true;
        }

        public static void RequireValid(SceneWorkspaceRecord scene)
        {
            if (scene == null || scene.schemaVersion != 1 || !SafeId(scene.id))
                throw new ArgumentException("Invalid scene identity/schema");
            if (string.IsNullOrWhiteSpace(scene.displayName) || scene.displayName.Length > 100 ||
                string.IsNullOrWhiteSpace(scene.sourceType))
                throw new ArgumentException("Invalid scene metadata");
            if (scene.sourceType != "blank" && scene.sourceType != "cad-bundle" &&
                scene.sourceType != "room-scan")
                throw new ArgumentException("Unrecognized scene source");
            if (!string.IsNullOrEmpty(scene.decorationPreset) &&
                (scene.decorationPreset != "birthday-v1" || scene.sourceType != "cad-bundle" ||
                 scene.assetKey != "hefu/home.bundle"))
                throw new ArgumentException("Invalid decoration preset");
            if (!float.IsNaN(scene.ceilingMeters) && scene.ceilingMeters >= 1.8f &&
                scene.ceilingMeters <= 8.0f) { }
            else throw new ArgumentException("Invalid ceiling height");
            if (scene.objects == null || scene.objects.Count > 4096)
                throw new ArgumentException("Too many objects");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in scene.objects)
            {
                if (item == null || !SafeId(item.id) || !ids.Add(item.id) ||
                    item.assetId == null || item.assetId.Length > 256 ||
                    !ValidVector(item.localPosition) || !ValidVector(item.localScale) ||
                    item.localScale.x <= 0 || item.localScale.y <= 0 || item.localScale.z <= 0 ||
                    item.localScale.x > 20 || item.localScale.y > 20 || item.localScale.z > 20 ||
                    !ValidQuaternion(item.localRotation) ||
                    (item.assetId.StartsWith(SceneWorkspaceBirthdayAssets.Prefix, StringComparison.Ordinal) &&
                     (scene.decorationPreset != "birthday-v1" ||
                      !SceneWorkspaceBirthdayAssets.IsSupported(item.assetId) ||
                      !SceneWorkspaceBirthdayAssets.IsAnchor(item.anchorId) ||
                      item.kind != "birthday" || !item.editable)))
                    throw new ArgumentException("Invalid scene object");
            }
        }

        static bool ValidVector(Vector3 v) => Valid(v.x) && Valid(v.y) && Valid(v.z) &&
            Mathf.Abs(v.x) <= 2000 && Mathf.Abs(v.y) <= 2000 && Mathf.Abs(v.z) <= 2000;
        static bool ValidQuaternion(Quaternion v) => Valid(v.x) && Valid(v.y) && Valid(v.z) && Valid(v.w) &&
            Mathf.Abs(v.x) <= 1.001f && Mathf.Abs(v.y) <= 1.001f && Mathf.Abs(v.z) <= 1.001f && Mathf.Abs(v.w) <= 1.001f;
        static bool Valid(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
