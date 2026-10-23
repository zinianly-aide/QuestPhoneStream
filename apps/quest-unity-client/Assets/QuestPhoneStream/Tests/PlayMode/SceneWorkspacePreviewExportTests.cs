using System;
using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Spaces;

namespace QuestPhoneStream.Tests
{
    public sealed class SceneWorkspacePreviewExportTests
    {
        [Test]
        public void DifferentRoomExportsAreMeterBasedAndNotHefuSpecific()
        {
            var root=new GameObject("IndependentOffice");
            try
            {
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name="office_wall";
                wall.transform.SetParent(root.transform,false);
                wall.transform.localPosition=new Vector3(3,1.3f,-2);
                wall.transform.localScale=new Vector3(6,2.6f,.12f);
                var doc=SceneWorkspacePreviewExport.Capture(root.transform,"Independent Office","quest-workspace-proxy");
                Assert.AreEqual("qps-room-preview-v1",doc.format);
                Assert.AreEqual(1,doc.schemaVersion);
                Assert.AreEqual(1f,doc.metersPerUnit);
                Assert.AreEqual("wall",doc.objects[0].kind);
                Assert.AreEqual(1,doc.objects.Count);
                Assert.That(doc.objects[0].size.x,Is.EqualTo(6f).Within(.01));
                Assert.IsFalse(doc.name.Contains("Hefu"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void NoVisibleGeometryCannotBecomeAValidExport()
        {
            var empty=new GameObject("EmptyTestWorld");
            try{Assert.Throws<InvalidOperationException>(()=>SceneWorkspacePreviewExport.Capture(empty.transform,"Empty","test"));}
            finally {UnityEngine.Object.DestroyImmediate(empty);}
        }
    }
}
