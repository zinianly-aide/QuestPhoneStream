namespace QuestPhoneStream
{
    public enum ConnectionState
    {
        Disconnected, WebSocketConnecting, WebSocketConnected, Registering, Registered,
        SessionRequesting, Negotiating, PeerConnected, MediaConnected,
        AuthFailed, DeviceOffline, SessionFailed, SignalingFailed, IceFailed, MediaFailed
    }

    public static class ConnectionStatus
    {
        public static bool IsFailure(ConnectionState state) => (int)state >= (int)ConnectionState.AuthFailed;
        public static string Text(ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.WebSocketConnecting: return "Connecting to signaling...";
                case ConnectionState.WebSocketConnected: return "Signaling socket open";
                case ConnectionState.Registering: return "Registering Quest...";
                case ConnectionState.Registered: return "Quest registered";
                case ConnectionState.SessionRequesting: return "Requesting session...";
                case ConnectionState.Negotiating: return "Negotiating WebRTC...";
                case ConnectionState.PeerConnected: return "Peer connected; waiting for video...";
                case ConnectionState.MediaConnected: return "Connected: video received";
                case ConnectionState.AuthFailed: return "Authentication failed";
                case ConnectionState.DeviceOffline: return "Phone is offline; reconnect when ready";
                case ConnectionState.SessionFailed: return "Session request failed";
                case ConnectionState.SignalingFailed: return "WebSocket disconnected or unavailable";
                case ConnectionState.IceFailed: return "ICE negotiation failed";
                case ConnectionState.MediaFailed: return "Media connection failed or timed out";
                default: return "Disconnected";
            }
        }
    }
}
