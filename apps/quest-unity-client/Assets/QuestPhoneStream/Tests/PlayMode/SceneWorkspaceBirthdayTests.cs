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
            // Fine detail replaces many separate sphere/cube primitives with a
            // few properly shaped meshes; geometry quality matters more than count.
            Assert.That(created.DecorationCount, Is.InRange(115, 150),
                "Detailed birthday should pool petals instead of spawning dozens of primitive balls");
            int bouquetMeshes = 0, stringMeshes = 0, icingMeshes = 0, candleMeshes = 0, frameMeshes = 0;
            int galleryBackdrops = 0, welcomeArches = 0, diningRunners = 0;
            var ribbons = 0;
            foreach (var filter in _room.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.StartsWith("Birthday_Ribbon_")) continue;
                ribbons++;
                Assert.IsNotNull(filter.sharedMesh);
                Assert.AreEqual(38, filter.sharedMesh.vertexCount);
                Assert.IsNull(filter.GetComponent<Collider>(), "Ribbons must not block VR walking");
            }
            Assert.AreEqual(3, ribbons, "Keep one batched ribbon per welcome / cabinet / gallery");
            foreach(var f in _room.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!f.name.StartsWith("Birthday_Detail_"))continue;
                Assert.IsNotNull(f.sharedMesh);
                Assert.IsNull(f.GetComponent<Collider>(),"Premium decorative mesh must not block XR movement");
                var mr=f.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr);
                Assert.AreEqual(f.sharedMesh.subMeshCount,mr.sharedMaterials.Length,
                    "Every pooled mesh submesh needs exactly one shared material");
                if(f.name.Contains("RoseBouquet"))
                { bouquetMeshes++; Assert.Greater(f.sharedMesh.vertexCount,300); }
                if(f.name.Contains("BalloonSatinStrings")) stringMeshes++;
                if(f.name.Contains("CakeIcingScallops")) icingMeshes++;
                if(f.name.Contains("CakeThreeCandles")) candleMeshes++;
                if(f.name.Contains("PhotoFrameMoulding")) frameMeshes++;
                if(f.name.Contains("PhotoWallFabricBackdrop")) galleryBackdrops++;
                if(f.name.Contains("WelcomeTextileArch")) welcomeArches++;
                if(f.name.Contains("DiningTextileAndMacarons")) diningRunners++;
            }
            Assert.AreEqual(5,bouquetMeshes,"Five fine floral clusters, including gallery corners");
            Assert.AreEqual(1,galleryBackdrops);
            Assert.AreEqual(1,welcomeArches);
            Assert.AreEqual(1,diningRunners);
            Assert.AreEqual(3,stringMeshes,"Each balloon group has one pooled curved-string mesh");
            Assert.AreEqual(1,icingMeshes,"Scalloped frosting uses one mesh");
            Assert.AreEqual(1,candleMeshes,"Three candles use one mesh with visible flames");
            Assert.AreEqual(3,frameMeshes,"Each photo frame has actual moulding with open center");
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
