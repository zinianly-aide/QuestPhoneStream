using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    /// <summary>
    /// Explicit one-time LAN offer of a simplified room preview. The secret must
    /// be entered on the receiver; this transport is HTTP, NOT encrypted. No
    /// background broadcast, no files outside the user's chosen snapshot.
    /// </summary>
    public sealed class SceneWorkspacePreviewShare : IDisposable
    {
        readonly object _gate = new object();
        TcpListener _listener;
        string _payload;
        string _token;
        DateTime _expires;
        bool _used;
        public string Address { get; private set; }
        public string Token { get { lock (_gate) return _token; } }
        public bool Available { get { lock (_gate) return !_used && _listener != null && DateTime.UtcNow < _expires; } }
        public const int MaxJsonBytes = 1024 * 1024;
        public const int LifetimeSeconds = 180;

        public string Start(SceneWorkspacePreviewExport.Document scene)
        {
            if(scene==null || scene.objects==null || scene.objects.Count==0)
                throw new ArgumentException("No selected preview scene");
            Stop();
            var json=JsonUtility.ToJson(scene);
            if(Encoding.UTF8.GetByteCount(json)>MaxJsonBytes)throw new InvalidDataException("Preview too large");
            var key=new byte[24];
            using(var rng=RandomNumberGenerator.Create()) rng.GetBytes(key);
            lock(_gate)
            {
                _token=Convert.ToBase64String(key).TrimEnd('=').Replace('+','-').Replace('/','_');
                _payload=json;
                _expires=DateTime.UtcNow.AddSeconds(LifetimeSeconds);
                _used=false;
                _listener=new TcpListener(IPAddress.Any,0);
                _listener.Start(3);
                Address=FindPrivateIp()+":"+((IPEndPoint)_listener.LocalEndpoint).Port;
                ThreadPool.QueueUserWorkItem(_=>Serve());
            }
            return Address;
        }
        static string FindPrivateIp()
        {
            foreach(var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if(nic.OperationalStatus!=OperationalStatus.Up || nic.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;
                foreach(var a in nic.GetIPProperties().UnicastAddresses)
                {
                    var ip=a.Address;
                    if(ip.AddressFamily!=AddressFamily.InterNetwork)continue;
                    var b=ip.GetAddressBytes();
                    if(b[0]==10 || (b[0]==172&&b[1]>=16&&b[1]<=31) || (b[0]==192&&b[1]==168))
                        return ip.ToString();
                }
            }
            // Editor tests can reach the loopback listener even without a LAN.
            return "127.0.0.1";
        }
        void Serve()
        {
            while(true)
            {
                TcpListener server;
                lock(_gate)server=_listener;
                if(server==null) return;
                if(DateTime.UtcNow>=_expires){Stop();return;}
                try
                {
                    if(!server.Pending()){Thread.Sleep(80);continue;}
                    using(var client=server.AcceptTcpClient())Handle(client);
                }
                catch(SocketException){return;}
                catch(ObjectDisposedException){return;}
                catch(Exception e){Debug.LogWarning("[ScenePreviewShare] "+e.GetType().Name);}
            }
        }
        void Handle(TcpClient client)
        {
            client.ReceiveTimeout=3500;client.SendTimeout=3500;
            var stream=client.GetStream();
            var header=new byte[3072];int count=0;bool complete=false;
            while(count<header.Length)
            {
                int n=stream.Read(header,count,1);
                if(n<=0)break;
                count+=n;
                if(count>=4 && header[count-4]==13 && header[count-3]==10 &&
                   header[count-2]==13 && header[count-1]==10){complete=true;break;}
            }
            if(!complete){Send(stream,400,"Bad Request", "");return;}
            var raw=Encoding.ASCII.GetString(header,0,count);
            var rows=raw.Split(new[]{"\r\n"},StringSplitOptions.None);
            if(rows.Length<2 || rows[0]!="GET /v1/room-preview HTTP/1.1")
            {Send(stream,404,"Not Found","");return;}
            string supplied="";
            foreach(var line in rows)
                if(line.StartsWith("Authorization: Bearer ",StringComparison.OrdinalIgnoreCase))
                    supplied=line.Substring("Authorization: Bearer ".Length).Trim();
            string payload=null;
            lock(_gate)
            {
                if(!_used && _listener!=null && DateTime.UtcNow<_expires &&
                   EqualSecret(supplied,_token))
                { _used=true;payload=_payload; }
            }
            if(payload==null){Send(stream,401,"Unauthorized","");return;}
            Send(stream,200,"OK",payload);
            Stop();
        }
        static bool EqualSecret(string a,string b)
        {
            if(a==null || b==null || a.Length!=b.Length || b.Length!=32)return false;
            int mismatch=0;
            for(int i=0;i<b.Length;i++)mismatch|=a[i]^b[i];
            return mismatch==0;
        }
        static void Send(Stream stream,int code,string status,string body)
        {
            var data=Encoding.UTF8.GetBytes(body);
            var headers=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" "+status+"\r\n"+
                "Content-Type: application/json; charset=utf-8\r\n"+
                "Cache-Control: no-store\r\nConnection: close\r\n"+
                "Content-Length: "+data.Length+"\r\n\r\n");
            stream.Write(headers,0,headers.Length);
            if(data.Length>0)stream.Write(data,0,data.Length);
            stream.Flush();
        }
        public void Stop()
        {
            lock(_gate)
            {
                _used=true;
                var old=_listener;_listener=null;
                if(old!=null)try{old.Stop();}catch(SocketException){}
            }
        }
        public void Dispose(){Stop();}
    }
}
