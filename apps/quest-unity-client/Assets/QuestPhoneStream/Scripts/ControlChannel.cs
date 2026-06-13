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

        public void Attach(RTCDataChannel channel)
        {
            _channel = channel;
            Debug.Log("[QuestPhoneStream] Control DataChannel attached");
        }

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
            }
            else
            {
                Debug.LogWarning($"[QuestPhoneStream] Control channel is not open. Command: {json}");
            }
        }
    }
}

