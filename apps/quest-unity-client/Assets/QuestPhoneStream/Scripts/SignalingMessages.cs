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
        public string negotiationId;
        public string code;
        public string message;
        public string androidDeviceId;
        public string questDeviceId;
        // Spatial messages use source; legacy signaling messages continue to use from.
        public string source;
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

    // Do not serialize the receive union directly: null/unused fields violate the wire schema.
    public static class SignalingWire
    {
        [Serializable] private class Register {
            public string type, token, role, deviceId;
        }
        [Serializable] private class Heartbeat {
            public string type, token, deviceId;
            public long timestamp;
        }
        [Serializable] private class SessionRequest {
            public string type, token, sessionId, androidDeviceId, questDeviceId, negotiationId;
        }
        [Serializable] private class Sdp {
            public string type, token, sessionId, from, to, negotiationId, sdp;
        }
        [Serializable] private class Ice {
            public string type, token, sessionId, from, to, negotiationId;
            public IceCandidateDto candidate;
        }

        public static string Serialize(SignalMessage m)
        {
            switch (m.type)
            {
                case "register":
                    return JsonUtility.ToJson(new Register { type = m.type, token = m.token, role = m.role, deviceId = m.deviceId });
                case "heartbeat":
                    return JsonUtility.ToJson(new Heartbeat { type = m.type, token = m.token, deviceId = m.deviceId, timestamp = m.timestamp });
                case "create_session":
                    return JsonUtility.ToJson(new SessionRequest { type = m.type, token = m.token, sessionId = m.sessionId,
                        androidDeviceId = m.androidDeviceId, questDeviceId = m.questDeviceId, negotiationId = m.negotiationId });
                case "answer":
                case "offer":
                    return JsonUtility.ToJson(new Sdp { type = m.type, token = m.token, sessionId = m.sessionId,
                        from = m.from, to = m.to, negotiationId = m.negotiationId, sdp = m.sdp });
                case "ice":
                    return JsonUtility.ToJson(new Ice { type = m.type, token = m.token, sessionId = m.sessionId,
                        from = m.from, to = m.to, negotiationId = m.negotiationId, candidate = m.candidate });
                default: throw new ArgumentException("Unsupported signaling message type");
            }
        }
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
