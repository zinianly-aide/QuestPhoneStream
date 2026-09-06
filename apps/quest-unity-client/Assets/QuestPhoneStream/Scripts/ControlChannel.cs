using System.Text;
using Unity.WebRTC;
using UnityEngine;

namespace QuestPhoneStream
{
    public sealed class ControlChannel : MonoBehaviour
    {
        public QuestSignalingClient signaling;
        public string channelName = "control";

        private RTCDataChannel _channel;
        public bool IsOpen => _channel != null && _channel.ReadyState == RTCDataChannelState.Open;
        public string StateLabel => _channel == null ? "None" : _channel.ReadyState.ToString();

        public void Attach(RTCDataChannel channel)
        {
            ResetChannel();
            _channel = channel;
            _channel.OnOpen = HandleOpen;
            _channel.OnClose = HandleClose;
            signaling?.ReportCapabilityState("display.control", active: IsOpen);
            Debug.Log("[QuestPhoneStream] Control DataChannel attached");
        }

        public void ResetChannel()
        {
            signaling?.ReportCapabilityState("display.control", active: false);
            if (_channel != null)
            {
                _channel.OnOpen = null;
                _channel.OnClose = null;
                _channel.Close();
                _channel.Dispose();
                _channel = null;
            }
        }

        private void HandleOpen()
        {
            signaling?.ReportCapabilityState("display.control", active: true);
            Debug.Log("[QuestPhoneStream] Control DataChannel open");
        }

        private void HandleClose()
        {
            signaling?.ReportCapabilityState("display.control", active: false);
            Debug.Log("[QuestPhoneStream] Control DataChannel closed");
        }

        private void OnDestroy() { ResetChannel(); }

        public void SendClick(int x, int y)
        {
            Send(new ControlCommandDto
            {
                type = "click",
                sessionId = signaling.sessionId,
                deviceId = signaling.questDeviceId,
                x = x,
                y = y,
                durationMs = 80
            });
        }

        public void SendSwipe(int startX, int startY, int endX, int endY, int durationMs)
        {
            Send(new ControlCommandDto
            {
                type = "swipe",
                sessionId = signaling.sessionId,
                deviceId = signaling.questDeviceId,
                startX = startX,
                startY = startY,
                endX = endX,
                endY = endY,
                durationMs = durationMs
            });
        }

        public void SendBack()
        {
            Send(new ControlCommandDto
            {
                type = "back",
                sessionId = signaling.sessionId,
                deviceId = signaling.questDeviceId
            });
        }

        public void SendText(string text)
        {
            Send(new ControlCommandDto
            {
                type = "text_input",
                sessionId = signaling.sessionId,
                deviceId = signaling.questDeviceId,
                text = text
            });
        }

        private void Send(ControlCommandDto command)
        {
            string json = command.ToJson();
            if (_channel != null && _channel.ReadyState == RTCDataChannelState.Open)
            {
                _channel.Send(Encoding.UTF8.GetBytes(json));
                Debug.Log($"[QuestPhoneStream] Control sent: type={command.type} x={command.x} y={command.y}");
            }
            else
            {
                signaling?.ReportCapabilityState("display.control", active: false);
                Debug.LogWarning("[QuestPhoneStream] Control channel is not open");
            }
        }
    }
}
