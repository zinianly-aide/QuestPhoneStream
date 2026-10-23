using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    /// <summary>
    /// Shares material instances across the pieces of one editable birthday decoration.
    /// Rendering variation is via MaterialPropertyBlock, not Renderer.material copies.
    /// Instances live only as long as this object and never leak across scene reloads.
    /// </summary>
    public sealed class SceneWorkspaceBirthdayMaterialPool : MonoBehaviour
    {
        readonly Dictionary<Color32, Material> _materials = new Dictionary<Color32, Material>();
        public int MaterialCount => _materials.Count;

        public Material Get(Color color)
        {
            var key = (Color32)color;
            if (_materials.TryGetValue(key, out var material) && material) return material;
            material = SceneWorkspaceBirthdayMaterial.Create(color);
            if (!material) return null;
            bool gold = color.r > .7f && color.g > .48f && color.g < .68f && color.b < .40f;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", gold ? .72f : 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", gold ? .75f : .33f);
            _materials[key] = material;
            return material;
        }

        void OnDestroy()
        {
            foreach (var material in _materials.Values)
                if (material)
                {
                    if (Application.isPlaying) Destroy(material);
                    else DestroyImmediate(material);
                }
            _materials.Clear();
        }
    }
}
