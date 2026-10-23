using System;
using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Spaces;

namespace QuestPhoneStream.Tests
{
    public sealed class SceneWorkspaceBirthdayTests
    {
        GameObject _room;
        static readonly string[] Names = {
            "EntranceShoeCabinet", "DiningSideboard", "Move_Dining_Table",
            "Move_Sofa", "Move_Coffee_Table"
        };
        [SetUp]
        public void Setup()
        {
            _room = new GameObject("TestRoom");
            foreach(var n in Names)
            {
                var objectRoot=new GameObject(n);
                objectRoot.transform.SetParent(_room.transform,false);
                var tag=objectRoot.AddComponent<SceneWorkspaceObject>();
                tag.persistentId="hefu-"+n;
                tag.editable=true;
                var mesh=GameObject.CreatePrimitive(PrimitiveType.Cube);
                mesh.transform.SetParent(objectRoot.transform,false);
                mesh.transform.localScale=new Vector3(1f,.7f,1f);
            }
        }
        [TearDown] public void TearDown(){if(_room)UnityEngine.Object.DestroyImmediate(_room);}

        [Test]
        public void BirthdayDecorUsesFiveRealFurnitureAnchorsAndDoesNotMoveThem()
        {
            var before=new Vector3[Names.Length];
            for(int i=0;i<Names.Length;i++)before[i]=_room.transform.GetChild(i).position;
            var scene=SceneWorkspaceStore.Create("HefuVR Birthday","cad-bundle","hefu/home.bundle");
            scene.decorationPreset="birthday-v1";
            Assert.IsTrue(SceneWorkspaceBirthday.HasRequiredFurniture(_room.transform));
            var created=SceneWorkspaceBirthday.Build(_room.transform,scene);
            Assert.AreEqual(5,created.AnchorCount);
            Assert.Greater(created.DecorationCount,50);
            for(int i=0;i<Names.Length;i++)
                Assert.AreEqual(before[i],_room.transform.GetChild(i).position);
            foreach(var c in _room.GetComponentsInChildren<Collider>(true))
                if(c.name.Contains("Cake") || c.name.Contains("Balloons_") || c.name.Contains("Gift"))
                    Assert.IsFalse(c.enabled,"Birthday decorations must not block walking");
            Assert.IsTrue(created.LightsOn);
            created.ToggleLights();
            Assert.IsFalse(created.LightsOn);
        }
        [Test]
        public void BirthdayRequiresSupportedPrivateCADSourceAndAllAnchors()
        {
            var original=SceneWorkspaceStore.Create("HefuVR","cad-bundle","hefu/home.bundle");
            original.decorationPreset="birthday-v1";
            _room.transform.GetChild(0).gameObject.SetActive(false);
            Assert.IsFalse(SceneWorkspaceBirthday.HasRequiredFurniture(_room.transform));
            Assert.Throws<InvalidOperationException>(()=>SceneWorkspaceBirthday.Build(_room.transform,original));
            _room.transform.GetChild(0).gameObject.SetActive(true);
            Assert.IsTrue(SceneWorkspaceBirthday.HasRequiredFurniture(_room.transform));
            original.assetKey="other/model.bundle";
            Assert.Throws<ArgumentException>(()=>SceneWorkspaceValidation.RequireValid(original));
        }
        [Test]
        public void PresetPersistsAndOriginalIsUnchanged()
        {
            var original=SceneWorkspaceStore.Create("HefuVR","cad-bundle","hefu/home.bundle");
            var duplicate=JsonUtility.FromJson<SceneWorkspaceRecord>(JsonUtility.ToJson(original));
            duplicate.id=Guid.NewGuid().ToString("N");
            duplicate.decorationPreset="birthday-v1";
            duplicate.displayName="HefuVR Birthday";
            SceneWorkspaceValidation.RequireValid(duplicate);
            Assert.IsTrue(string.IsNullOrEmpty(original.decorationPreset));
            Assert.AreEqual("birthday-v1",JsonUtility.FromJson<SceneWorkspaceRecord>(
                JsonUtility.ToJson(duplicate)).decorationPreset);
        }
    }
}
