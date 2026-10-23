using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    /// <summary>
    /// A serialized Resources material keeps the lightweight decor shader in Quest APKs.
    /// Shader.Find alone works in the Editor but the Android player may strip its shader.
    /// </summary>
    public static class SceneWorkspaceBirthdayMaterial
    {
        public const string ResourcePath = "Birthday/BirthdayDecor";
        public const string ShaderName = "QuestPhoneStream/BirthdayDecor";
        public static Material Template => Resources.Load<Material>(ResourcePath);
        public static Material Create(Color color)
        {
            var prefab = Template;
            if (prefab)
            {
                var result = new Material(prefab);
                result.color = color;
                return result;
            }
            Debug.LogError("[Spaces] Birthday Resources material missing from package: " + ResourcePath);
            // Fail visibly rather than discarding the entire room scene.
            var shader = Shader.Find(ShaderName) ?? Shader.Find("Sprites/Default");
            if (shader) return new Material(shader) { color = color };
            return null;
        }
    }
}
