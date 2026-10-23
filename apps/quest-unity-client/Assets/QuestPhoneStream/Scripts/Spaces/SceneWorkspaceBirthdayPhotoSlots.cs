using System;
using System.IO;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    /// <summary>Only numbered photos from the Quest app-private folder; never URLs or arbitrary paths.</summary>
    public static class SceneWorkspaceBirthdayPhotoSlots
    {
        public const int MaxSlot = 3;
        public const long MaxFileBytes = 4L * 1024L * 1024L;
        public static string PrivateDirectoryOverrideForTests;
        public static string DirectoryPath =>
            PrivateDirectoryOverrideForTests ??
            Path.Combine(Application.persistentDataPath, "SceneWorkspaces", "Photos");
        public static bool ValidSlot(int slot) => slot >= 0 && slot <= MaxSlot;
        public static string SlotFileName(int slot, bool png = false)
        {
            if (slot < 1 || slot > MaxSlot) throw new ArgumentOutOfRangeException(nameof(slot));
            return "slot-0" + slot + (png ? ".png" : ".jpg");
        }
        public static string FindSlotFile(int slot)
        {
            if (slot < 1 || slot > MaxSlot) return null;
            foreach (var png in new[] {false, true})
            {
                var path = Path.Combine(DirectoryPath, SlotFileName(slot, png));
                if (!File.Exists(path)) continue;
                var size = new FileInfo(path).Length;
                if (size > 0 && size <= MaxFileBytes) return path;
            }
            return null;
        }
        public static bool TryApply(Renderer renderer, int slot)
        {
            if (!renderer || slot <= 0 || slot > MaxSlot) return false;
            var path = FindSlotFile(slot);
            if (string.IsNullOrEmpty(path)) return false;
            Texture2D tex = null;
            Material material = null;
            try
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false) ||
                    tex.width < 16 || tex.height < 16 || tex.width > 2048 || tex.height > 2048)
                    throw new InvalidDataException("Local photo must be JPG/PNG, 16-2048 pixels");
                material = SceneWorkspaceBirthdayMaterial.Create(Color.white);
                if (!material) throw new InvalidOperationException("Missing photo material");
                material.mainTexture = tex;
                renderer.material = material;
                // Material and texture are owned by this renderer and released on destruction.
                var owner = renderer.GetComponent<SceneWorkspaceBirthdayPhotoOwner>();
                if (!owner) owner = renderer.gameObject.AddComponent<SceneWorkspaceBirthdayPhotoOwner>();
                owner.Replace(material, tex);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Spaces] local birthday photo invalid, slot " + slot + ": " + ex.Message);
                if (material) UnityEngine.Object.Destroy(material);
                if (tex) UnityEngine.Object.Destroy(tex);
                return false;
            }
        }
        public static void Reset(Renderer renderer)
        {
            if (!renderer) return;
            var owner = renderer.GetComponent<SceneWorkspaceBirthdayPhotoOwner>();
            if (owner) owner.Clear();
            var material = SceneWorkspaceBirthdayMaterial.Create(new Color(.29f, .23f, .19f));
            if (!material) return;
            renderer.material = material;
            if (!owner) owner = renderer.gameObject.AddComponent<SceneWorkspaceBirthdayPhotoOwner>();
            owner.Replace(material, null);
        }
    }

    /// <summary>Keep decoded private photo textures and materials owned by their renderer.</summary>
    public sealed class SceneWorkspaceBirthdayPhotoOwner : MonoBehaviour
    {
        Material _material;
        Texture2D _texture;
        public void Replace(Material material, Texture2D texture)
        {
            Clear(); _material = material; _texture = texture;
        }
        public void Clear()
        {
            if (_material) { if (Application.isPlaying) Destroy(_material); else DestroyImmediate(_material); }
            if (_texture) { if (Application.isPlaying) Destroy(_texture); else DestroyImmediate(_texture); }
            _material = null; _texture = null;
        }
        void OnDestroy() { Clear(); }
    }
}
