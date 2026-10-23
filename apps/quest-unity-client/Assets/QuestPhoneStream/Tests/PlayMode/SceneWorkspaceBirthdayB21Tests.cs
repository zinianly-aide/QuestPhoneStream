using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Spaces;

namespace QuestPhoneStream.Tests
{
    public sealed class SceneWorkspaceBirthdayB21Tests
    {
        string _photos;
        GameObject _root;
        [SetUp]
        public void Setup()
        {
            _photos=Path.Combine(Path.GetTempPath(),"qps-b21-test-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_photos);
            SceneWorkspaceBirthdayPhotoSlots.PrivateDirectoryOverrideForTests=_photos;
            _root=new GameObject("B21_Photo_And_Style_Test");
        }
        [TearDown]
        public void Cleanup()
        {
            if(_root)UnityEngine.Object.DestroyImmediate(_root);
            SceneWorkspaceBirthdayPhotoSlots.PrivateDirectoryOverrideForTests=null;
            if(Directory.Exists(_photos))Directory.Delete(_photos,true);
        }
        static SceneObjectRecord Frame(int style,int slot) => new SceneObjectRecord {
            id="b21_photo",assetId="birthday/frame",anchorId="hefu-DiningSideboard",
            kind="birthday",editable=true,birthdayStyle=style,birthdayPhotoSlot=slot,
            localPosition=new Vector3(.1f,.8f,.2f)
        };
        [Test]
        public void B21PaletteAndPhotoSlotRequireKnownValues()
        {
            Assert.AreEqual(4,SceneWorkspaceBirthdayAssets.StyleCount);
            Assert.AreNotEqual(SceneWorkspaceBirthdayAssets.Accent(0),SceneWorkspaceBirthdayAssets.Accent(3));
            var scene=SceneWorkspaceStore.Create("Birthday","cad-bundle","hefu/home.bundle");
            scene.decorationPreset="birthday-v1";
            scene.objects.Add(Frame(3,2));
            Assert.DoesNotThrow(()=>SceneWorkspaceValidation.RequireValid(scene));
            scene.objects[0].birthdayStyle=4;
            Assert.Throws<ArgumentException>(()=>SceneWorkspaceValidation.RequireValid(scene));
            scene.objects[0].birthdayStyle=0;
            scene.objects[0].birthdayPhotoSlot=4;
            Assert.Throws<ArgumentException>(()=>SceneWorkspaceValidation.RequireValid(scene));
            scene.objects[0].birthdayPhotoSlot=1;
            scene.objects[0].assetId="birthday/gift";
            Assert.Throws<ArgumentException>(()=>SceneWorkspaceValidation.RequireValid(scene));
        }
        [Test]
        public void PrivateSlotsRejectInvalidNamesAndOversizedFiles()
        {
            Assert.IsNull(SceneWorkspaceBirthdayPhotoSlots.FindSlotFile(0));
            Assert.IsNull(SceneWorkspaceBirthdayPhotoSlots.FindSlotFile(4));
            Assert.AreEqual("slot-02.png",SceneWorkspaceBirthdayPhotoSlots.SlotFileName(2,true));
            Assert.Throws<ArgumentOutOfRangeException>(()=>SceneWorkspaceBirthdayPhotoSlots.SlotFileName(4));
            File.WriteAllBytes(Path.Combine(_photos,"slot-01.jpg"),new byte[10]);
            Assert.IsNotNull(SceneWorkspaceBirthdayPhotoSlots.FindSlotFile(1));
            using(var fs=new FileStream(Path.Combine(_photos,"slot-02.jpg"),FileMode.Create))
                fs.SetLength(SceneWorkspaceBirthdayPhotoSlots.MaxFileBytes+1);
            Assert.IsNull(SceneWorkspaceBirthdayPhotoSlots.FindSlotFile(2));
        }
        [Test]
        public void LoadedLocalPngAppearsOnPhotoSurfaceAndPersistsSlotMetadata()
        {
            var tex=new Texture2D(32,32,TextureFormat.RGBA32,false);
            try
            {
                var color=new Color32[32*32];
                for(int i=0;i<color.Length;i++)color[i]=new Color32(190,120,150,255);
                tex.SetPixels32(color);tex.Apply();
                File.WriteAllBytes(Path.Combine(_photos,"slot-01.png"),tex.EncodeToPNG());
                var furniture=new GameObject("DiningSideboard");
                furniture.transform.SetParent(_root.transform,false);
                var anchor=furniture.AddComponent<SceneWorkspaceObject>();
                anchor.persistentId="hefu-DiningSideboard";
                var record=Frame(2,1);
                var frame=SceneWorkspaceBirthdayAssets.Create(furniture.transform,record);
                Renderer surface=null;
                foreach(var r in frame.GetComponentsInChildren<Renderer>())
                    if(r.name.Contains("LocalPhotoSurface")){surface=r;break;}
                Assert.IsNotNull(surface);
                Assert.IsNotNull(surface.sharedMaterial.mainTexture);
                Assert.AreEqual(32,surface.sharedMaterial.mainTexture.width);
                Assert.AreEqual("birthday/frame",record.assetId);
                var restored=JsonUtility.FromJson<SceneObjectRecord>(JsonUtility.ToJson(record));
                Assert.AreEqual(2,restored.birthdayStyle);
                Assert.AreEqual(1,restored.birthdayPhotoSlot);
                record.birthdayPhotoSlot=0;
                SceneWorkspaceBirthdayAssets.ApplyPresentation(frame,record);
                Assert.IsNull(surface.sharedMaterial.mainTexture);
            }
            finally {UnityEngine.Object.DestroyImmediate(tex);}
        }
    }
}
