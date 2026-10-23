using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Spaces;

namespace QuestPhoneStream.Tests
{
    public sealed class SceneWorkspaceBirthdayB2Tests
    {
        GameObject _root;
        string _dataRoot;
        [SetUp]
        public void Setup()
        {
            _root=new GameObject("B2TestRoom");
            _dataRoot=Path.Combine(Path.GetTempPath(),"qps-birthday-b2-"+Guid.NewGuid().ToString("N"));
        }
        [TearDown]
        public void Cleanup()
        {
            if(_root)UnityEngine.Object.DestroyImmediate(_root);
            if(Directory.Exists(_dataRoot))Directory.Delete(_dataRoot,true);
        }
        static SceneObjectRecord Record(string type="cake",string anchor="Move_Dining_Table")
        {
            return new SceneObjectRecord{
                id="birthday1",assetId="birthday/"+type,kind="birthday",editable=true,
                anchorId="hefu-"+anchor,localPosition=new Vector3(.2f,.77f,.14f),
                localRotation=Quaternion.Euler(0,30,0),localScale=Vector3.one
            };
        }
        [Test]
        public void BirthdayAssetPaletteOnlyAcceptsKnownKindsAndFurnitureAnchors()
        {
            foreach(var type in SceneWorkspaceBirthdayAssets.Items)
            {
                var record=Record(type);
                var scene=SceneWorkspaceStore.Create("Birthday","cad-bundle","hefu/home.bundle");
                scene.decorationPreset="birthday-v1";
                scene.objects.Add(record);
                Assert.DoesNotThrow(()=>SceneWorkspaceValidation.RequireValid(scene));
            }
            Assert.IsFalse(SceneWorkspaceBirthdayAssets.IsSupported("birthday/other"));
            Assert.IsFalse(SceneWorkspaceBirthdayAssets.IsAnchor("hefu-Move_Master_Bed"));
            var invalid=SceneWorkspaceStore.Create("Birthday","cad-bundle","hefu/home.bundle");
            invalid.decorationPreset="birthday-v1";
            invalid.objects.Add(Record("../../unsafe"));
            Assert.Throws<ArgumentException>(()=>SceneWorkspaceValidation.RequireValid(invalid));
            invalid.objects.Clear();
            invalid.objects.Add(Record("cake","Move_Master_Bed"));
            Assert.Throws<ArgumentException>(()=>SceneWorkspaceValidation.RequireValid(invalid));
            invalid.decorationPreset="";
            invalid.objects.Clear();
            invalid.objects.Add(Record());
            Assert.Throws<ArgumentException>(()=>SceneWorkspaceValidation.RequireValid(invalid));
        }
        [Test]
        public void EditableDecorationStaysAttachedToMovingFurnitureAndCanBeRaySelected()
        {
            var anchor=new GameObject("Move_Dining_Table");
            anchor.transform.SetParent(_root.transform,false);
            anchor.transform.position=new Vector3(-6f,0f,-2.5f);
            var tag=anchor.AddComponent<SceneWorkspaceObject>();
            tag.persistentId="hefu-Move_Dining_Table";
            var record=Record();
            var decor=SceneWorkspaceBirthdayAssets.Create(anchor.transform,record);
            Assert.IsTrue(decor.GetComponent<SceneWorkspaceObject>().editable);
            Assert.AreEqual(record.localPosition,decor.transform.localPosition);
            var childCollider=decor.GetComponentInChildren<Collider>();
            Assert.IsTrue(childCollider.isTrigger);
            var before=decor.transform.position;
            anchor.transform.position+=new Vector3(1f,0f,2f);
            Assert.Less(Vector3.Distance(before+new Vector3(1f,0f,2f),decor.transform.position),.001f);
            var bounds=decor.GetComponentInChildren<Renderer>().bounds;
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(new Ray(bounds.center+Vector3.up*1f,Vector3.down),
                out RaycastHit hit,2f,~0,QueryTriggerInteraction.Collide));
            Assert.AreEqual(decor.GetComponent<SceneWorkspaceObject>(),
                hit.collider.GetComponentInParent<SceneWorkspaceObject>());
        }
        [Test]
        public void SceneRecordRoundTripsBirthdayFurnitureRelativePoseInPrivateStore()
        {
            var store=new SceneWorkspaceStore(_dataRoot);
            var scene=SceneWorkspaceStore.Create("HefuVR Birthday","cad-bundle","hefu/home.bundle");
            scene.decorationPreset="birthday-v1";
            scene.objects.Add(Record("gift","DiningSideboard"));
            store.SaveScene(scene);
            var restored=store.LoadScene(scene.id);
            Assert.AreEqual("birthday-v1",restored.decorationPreset);
            Assert.AreEqual(scene.objects[0].anchorId,restored.objects[0].anchorId);
            Assert.AreEqual(scene.objects[0].localPosition,restored.objects[0].localPosition);
            Assert.IsTrue(restored.objects[0].editable);
        }
    }
}
