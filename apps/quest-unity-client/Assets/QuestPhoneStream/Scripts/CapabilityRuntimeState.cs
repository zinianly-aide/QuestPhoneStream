using System;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class CapabilityRuntimeState
    {
        public string name;
        public bool available;
        public bool authorized;
        public bool active;
        public string transport;

        public CapabilityRuntimeState(string name, bool available, bool authorized, bool active, string transport)
        {
            this.name = name;
            this.available = available;
            this.authorized = authorized;
            this.active = active;
            this.transport = transport;
        }
    }

    public static class CapabilityRuntimeStateFactory
    {
        public static CapabilityRuntimeState[] ForQuest(
            bool screenAdvertised,
            bool controlAdvertised,
            bool mediaAvailable,
            bool mediaAuthorized,
            bool mediaActive,
            bool peerConnected,
            bool videoFrameReceived,
            bool controlOpen)
        {
            return new[] {
                new CapabilityRuntimeState("display.publish", screenAdvertised, peerConnected, videoFrameReceived, "WebRTC video"),
                new CapabilityRuntimeState("display.control", controlAdvertised, controlOpen, controlOpen, "DataChannel"),
                new CapabilityRuntimeState("media.catalog", mediaAvailable, mediaAuthorized, mediaActive, "HTTP"),
                new CapabilityRuntimeState("display.consume", true, peerConnected, videoFrameReceived, "WebRTC video"),
                new CapabilityRuntimeState("display.control.consume", true, controlOpen, controlOpen, "DataChannel")
            };
        }
    }
}
