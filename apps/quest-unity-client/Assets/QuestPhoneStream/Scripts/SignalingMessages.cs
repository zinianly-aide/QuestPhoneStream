using System;
using UnityEngine;

namespace QuestPhoneStream
{
    [Serializable]
    public class SignalMessage
    {
        public string type;
        public string token;
        public string role;
        public string deviceId;
        public string sessionId;
        public string androidDeviceId;
        public string questDeviceId;
        public string from;
        public string to;
        public string sdp;
        public IceCandidateDto candidate;
        public long timestamp;
    }

    [Serializable]
    public class IceCandidateDto
    {
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }

    [Serializable]
    public class ControlCommandDto
    {
        public string version = "1.0";
        public string type;
        public string sessionId;
        public string deviceId;
        public int x;
        public int y;
        public int startX;
        public int startY;
        public int endX;
        public int endY;
        public int durationMs = 100;
        public string text = "";
        public long timestamp;

        public string ToJson()
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return JsonUtility.ToJson(this);
        }
    }
}

