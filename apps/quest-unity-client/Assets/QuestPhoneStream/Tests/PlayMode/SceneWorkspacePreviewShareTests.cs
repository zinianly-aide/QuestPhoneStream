using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Spaces;

namespace QuestPhoneStream.Tests
{
    public sealed class SceneWorkspacePreviewShareTests
    {
        static string Get(int port,string secret)
        {
            using(var socket=new TcpClient())
            {
                socket.Connect("127.0.0.1",port);
                socket.ReceiveTimeout=5000;
                var stream=socket.GetStream();
                var msg=Encoding.ASCII.GetBytes("GET /v1/room-preview HTTP/1.1\r\nHost: localhost\r\nAuthorization: Bearer "+secret+"\r\nConnection: close\r\n\r\n");
                stream.Write(msg,0,msg.Length);
                using(var memory=new MemoryStream())
                {
                    var data=new byte[4096];int count;
                    while((count=stream.Read(data,0,data.Length))>0)memory.Write(data,0,count);
                    return Encoding.UTF8.GetString(memory.ToArray());
                }
            }
        }
        [Test]
        public void OneTimeOfferRejectsWrongKeyAndExpiresAfterSuccess()
        {
            var scene=new SceneWorkspacePreviewExport.Document{
                sceneId="synthetic_001",name="Lab",source="synthetic-test"
            };
            scene.objects.Add(new SceneWorkspacePreviewExport.Node {id="wall_1",kind="wall",
                position=Vector3.zero,size=new Vector3(3,2,.1f)});
            using(var offer=new SceneWorkspacePreviewShare())
            {
                string host=offer.Start(scene);
                Assert.AreEqual(32,offer.Token.Length);
                Assert.IsTrue(offer.Available);
                int port=int.Parse(host.Split(':')[1]);
                var wrong=Get(port,new string('A',32));
                Assert.IsTrue(wrong.StartsWith("HTTP/1.1 401"));
                Assert.IsTrue(offer.Available);
                var right=Get(port,offer.Token);
                Assert.IsTrue(right.StartsWith("HTTP/1.1 200"));
                Assert.IsTrue(right.Contains("qps-room-preview-v1"));
                Assert.IsTrue(right.Contains("synthetic_001"));
                Assert.IsFalse(offer.Available);
            }
        }
    }
}
