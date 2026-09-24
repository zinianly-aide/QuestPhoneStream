using System;

namespace QuestPhoneStream
{
    /// <summary>
    /// Common source gate for future Spatial messages. Spatial messages are
    /// accepted only from the Android peer selected for the active session.
    /// </summary>
    public static class SpatialPeerIsolation
    {
        public static bool IsSpatialMessageType(string type) =>
            type == "hello" || type == "capabilities" || type == "subscription";

        public static bool Accept(SpatialEnvelope message, string selectedAndroid, string activeAndroid, string activeSession)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.source)) return false;
            if (string.IsNullOrWhiteSpace(selectedAndroid) || string.IsNullOrWhiteSpace(activeAndroid)) return false;
            if (!string.Equals(message.source, selectedAndroid, StringComparison.Ordinal)) return false;
            if (!string.Equals(message.source, activeAndroid, StringComparison.Ordinal)) return false;
            return string.IsNullOrWhiteSpace(activeSession) ||
                string.Equals(message.sessionId, activeSession, StringComparison.Ordinal);
        }

        public static bool AcceptCapability(string source, string selectedAndroid, string activeAndroid, string activeSession, string sessionId) =>
            Accept(new SpatialEnvelope { type = "capabilities", source = source, sessionId = sessionId }, selectedAndroid, activeAndroid, activeSession);

        public static bool AcceptSubscription(string source, string selectedAndroid, string activeAndroid, string activeSession, string sessionId) =>
            Accept(new SpatialEnvelope { type = "subscription", source = source, sessionId = sessionId }, selectedAndroid, activeAndroid, activeSession);

        public static bool AcceptHello(string source, string selectedAndroid, string activeAndroid, string activeSession, string sessionId) =>
            Accept(new SpatialEnvelope { type = "hello", source = source, sessionId = sessionId }, selectedAndroid, activeAndroid, activeSession);
    }
}
