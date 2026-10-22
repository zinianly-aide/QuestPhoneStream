using System;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;
using System.Collections;
using UnityEngine;
using QuestPhoneStream.Spaces;
namespace QuestPhoneStream.Tests
{
    public sealed class SceneWorkspaceTests
    {
        string _temp;
        SceneWorkspaceStore _store;
        [SetUp]
        public void SetUp()
        {
            _temp=Path.Combine(Path.GetTempPath(),"qps-spaces-test-"+Guid.NewGuid().ToString("N"));
            _store=new SceneWorkspaceStore(_temp);
        }
        [TearDown]
        public void TearDown()
        {
            if(Directory.Exists(_temp))Directory.Delete(_temp,true);
        }
        [Test]
        public void SaveLoadSceneCatalogAndFurnitureRoundtrip()
        {
            var home=SceneWorkspaceStore.Create("HefuVR (private)","cad-bundle","hefu/home.bundle");
            home.ceilingMeters=2.7f;
            home.objects.Add(new SceneObjectRecord{
                id="bed-master-1",assetId="furniture/bed",label="Master Bed",
                localPosition=new Vector3(-3.025f,0,-7.490f),localRotation=Quaternion.Euler(0,-90,0)});
            _store.SaveScene(home);
            var catalog=new SceneWorkspaceIndex{activeSceneId=home.id};catalog.scenes.Add(home);
            _store.SaveCatalog(catalog);
            var restartedStore=new SceneWorkspaceStore(_temp);
            var resumed=restartedStore.LoadScene(home.id);
            var index=restartedStore.LoadCatalog();
            Assert.AreEqual(1,resumed.schemaVersion);
            Assert.AreEqual(2.7f,resumed.ceilingMeters);
            Assert.AreEqual("bed-master-1",resumed.objects[0].id);
            Assert.AreEqual(-3.025f,resumed.objects[0].localPosition.x,.0001f);
            Assert.AreEqual(home.id,index.activeSceneId);
            Assert.AreEqual(1,index.scenes.Count);
        }
        [Test]
        public void InvalidIdsAndTransformsCannotWriteOutsidePrivateDirectory()
        {
            Assert.Throws<ArgumentException>(()=>_store.ScenePath("../../sensitive"));
            Assert.Throws<ArgumentException>(()=>_store.ScenePath("/tmp/not-a-space"));
            var scene=SceneWorkspaceStore.Create("Blank");
            scene.objects.Add(new SceneObjectRecord{ id="same",assetId="primitive/cube" });
            scene.objects.Add(new SceneObjectRecord{ id="same",assetId="primitive/cube" });
            Assert.Throws<ArgumentException>(()=>_store.SaveScene(scene));
            scene.objects.RemoveAt(1);
            scene.objects[0].localScale=new Vector3(1,float.NaN,1);
            Assert.Throws<ArgumentException>(()=>_store.SaveScene(scene));
        }
        [Test]
        public void PreviousVersionBackupRemainsAvailable()
        {
            var scene=SceneWorkspaceStore.Create("Blank");
            _store.SaveScene(scene);
            scene.displayName="Modified";
            _store.SaveScene(scene);
            Assert.IsTrue(File.Exists(_store.ScenePath(scene.id)+".bak"));
            Assert.AreEqual("Modified",_store.LoadScene(scene.id).displayName);
            File.WriteAllText(_store.ScenePath(scene.id),"not valid JSON {");
            Assert.AreEqual("Blank",_store.LoadScene(scene.id).displayName);
        }
        [UnityTest]
        public IEnumerator ManagerSwitchesEnvironmentButPreservesHostAndHandlesMissingPrivateHome()
        {
            SceneWorkspaceManager.PersistentRootOverrideForTests=_temp;
            var host=new GameObject("QuestPhoneStreamHost");
            var mediaSentinel=new GameObject("WebRTC_and_XR_Stay_Alive");
            mediaSentinel.transform.SetParent(host.transform);
            var manager=host.AddComponent<SceneWorkspaceManager>();
            Assert.NotNull(manager.Active);
            Assert.AreEqual("blank",manager.Active.sourceType);
            Assert.NotNull(manager.EnvironmentRoot.Find("Environment_"+manager.Active.id));
            var first=manager.Active;
            var next=manager.CreateBlank("Second room");
            Assert.IsTrue(manager.Select(next.id));
            yield return null;
            Assert.AreEqual(next.id,manager.Active.id);
            Assert.IsTrue(mediaSentinel && mediaSentinel.transform.parent==host.transform);
            Assert.IsTrue(manager.AddPrimitive("cube",new Vector3(.5f,.6f,.7f)));
            Assert.AreEqual(1,manager.Active.objects.Count);
            Assert.IsTrue(manager.Undo());
            yield return null;
            Assert.AreEqual(0,manager.Active.objects.Count);
            Assert.IsTrue(manager.Select(first.id));
            var privateRoom=manager.Catalog.scenes.Find(x=>x.sourceType=="cad-bundle");
            Assert.NotNull(privateRoom);
            Assert.IsFalse(manager.Select(privateRoom.id));
            Assert.AreEqual(first.id,manager.Active.id,"Missing CAD must not replace active scene");
            Assert.IsTrue(manager.LastError.Contains("not installed"));
            Assert.IsTrue(mediaSentinel);
            Assert.IsTrue(manager.DeleteScene(next.id));
            Assert.IsFalse(manager.Select(next.id));
            Assert.IsTrue(manager.RestoreScene(next.id));
            UnityEngine.Object.Destroy(host);
            yield return null;
            SceneWorkspaceManager.PersistentRootOverrideForTests=null;
        }

        [UnityTest]
        public IEnumerator ExplicitFurnitureEditDoesNotClickThroughPanelAndRestoresSavedPose()
        {
            SceneWorkspaceManager.PersistentRootOverrideForTests=_temp;
            var host=new GameObject("QuestAppHost");
            var cameraGo=new GameObject("TestEditorCamera");
            var camera=cameraGo.AddComponent<Camera>();
            cameraGo.transform.position=new Vector3(0,1.40f,0);
            var space=host.AddComponent<SceneWorkspaceManager>();
            Assert.IsTrue(space.AddPrimitive("cube",new Vector3(0,1.4f,1.7f)));
            var editor=host.AddComponent<SceneWorkspaceManipulator>();
            editor.Initialize(space);
            var pointer=new Ray(new Vector3(0,1.4f,0),Vector3.forward);
            Assert.IsFalse(editor.TrySelect(pointer),"Editor must be explicitly enabled");
            editor.Enter(camera);
            var foreground=GameObject.CreatePrimitive(PrimitiveType.Cube);
            foreground.name="PhonePanelScreen_BlockingSelection";
            foreground.transform.position=new Vector3(0,1.4f,.7f);
            foreground.transform.localScale=new Vector3(1f,1f,.07f);
            Physics.SyncTransforms();
            Assert.IsFalse(editor.TrySelect(pointer),"Must not click through phone screen");
            foreground.SetActive(false);
            Physics.SyncTransforms();
            Assert.IsTrue(editor.BeginGrab(pointer));
            var id=space.Active.objects[0].id;
            Assert.AreEqual(id,editor.Selected.persistentId);
            Assert.IsTrue(editor.Drag(new Ray(new Vector3(.2f,1.4f,0),Vector3.forward)));
            Assert.IsTrue(editor.FinishGrab(true));
            Assert.AreEqual(.2f,space.Active.objects[0].localPosition.x,.02f);
            Assert.IsTrue(File.Exists(space.Store.ScenePath(space.Active.id)));
            Assert.IsTrue(editor.Undo());
            Assert.AreEqual(0f,space.Active.objects[0].localPosition.x,.02f);
            Assert.IsTrue(editor.TrySelect(pointer));
            Assert.IsTrue(editor.RotateSelected(15f));
            Assert.IsTrue(editor.ScaleSelected(1.05f));
            Assert.IsTrue(editor.DeleteSelected());
            Assert.IsTrue(space.Active.objects[0].deleted);
            Assert.IsTrue(editor.Undo());
            Assert.IsFalse(space.Active.objects[0].deleted);
            var initialId=space.Active.id;
            var another=space.CreateBlank("Other");
            Assert.IsTrue(space.Select(another.id));
            Assert.IsTrue(space.Select(initialId));
            Assert.AreEqual(1,space.Active.objects.Count);
            Assert.IsFalse(space.Active.objects[0].deleted);
            Assert.AreEqual(15f,space.Active.objects[0].localRotation.eulerAngles.y,1f);
            Assert.IsNotNull(host.GetComponent<SceneWorkspaceManipulator>());
            UnityEngine.Object.Destroy(host);
            UnityEngine.Object.Destroy(cameraGo);
            UnityEngine.Object.Destroy(foreground);
            SceneWorkspaceManager.PersistentRootOverrideForTests=null;
            yield return null;
        }

        [Test]
        public void SceneSourceMustBeRecognized()
        {
            var result=SceneWorkspaceStore.Create("Studio");
            result.sourceType="camera-stream";
            Assert.Throws<ArgumentException>(()=>_store.SaveScene(result));
        }
    }
}
