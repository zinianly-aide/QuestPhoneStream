using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Spaces;

namespace QuestPhoneStream.Tests
{
    public sealed class SceneWorkspaceBirthdayShaderTests
    {
        [Test]
        public void BirthdayMaterialMustBeSerializableResourceNotEditorOnlyShaderFind()
        {
            var baseMaterial = SceneWorkspaceBirthdayMaterial.Template;
            Assert.IsNotNull(baseMaterial, "Resources/Birthday/BirthdayDecor.mat must ship with Android build");
            Assert.IsNotNull(baseMaterial.shader);
            Assert.AreEqual(SceneWorkspaceBirthdayMaterial.ShaderName, baseMaterial.shader.name);
            Assert.IsTrue(baseMaterial.HasProperty("_MainTex"));
            Assert.IsTrue(baseMaterial.HasProperty("_Color"));
        }
        [Test]
        public void BirthdayAndPhotoMaterialPreservesPaletteAndTexture()
        {
            Material m = null;
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                m = SceneWorkspaceBirthdayMaterial.Create(new Color(.7f, .4f, .5f));
                Assert.IsNotNull(m);
                Assert.AreEqual(.7f, m.color.r, .002f);
                m.mainTexture = texture;
                Assert.AreSame(texture, m.mainTexture);
            }
            finally
            {
                if(m) Object.DestroyImmediate(m);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
