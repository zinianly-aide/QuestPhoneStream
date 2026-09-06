using UnityEngine;

namespace QuestPhoneStream
{
    /// <summary>
    /// P2 diagnostics adapter consumed by the Developer Tools page. It no longer
    /// creates or attaches a head-locked canvas at runtime.
    /// </summary>
    public sealed class QuestDeveloperHud : MonoBehaviour
    {
        public QuestWebRtcReceiver receiver;
        public QuestSignalingClient signaling;

        private SpatialTelemetryService _telemetry;
        private SpatialDataPlaneHub _dataPlane;
        private QuestVisionService _vision;
        private QuestAiClient _ai;
        private SpatialHandTrackingService _hands;

        public void Initialize(QuestWebRtcReceiver value, QuestSignalingClient signalingClient)
        {
            receiver = value;
            signaling = signalingClient;
            ResolveServices();
        }

        public string CaptureText()
        {
            ResolveServices();
            var source = signaling != null && !string.IsNullOrWhiteSpace(signaling.androidDeviceId)
                ? signaling.androidDeviceId : "—";
            var poseHz = _telemetry != null ? _telemetry.PoseStreamHz : 0f;
            var dropped = _dataPlane != null ? _dataPlane.DroppedFrames : 0;
            var sequence = _dataPlane != null ? _dataPlane.LastSequence : -1;
            var cameraState = _vision != null ? _vision.CameraState : "Unavailable";
            var aiState = _ai == null ? "Unavailable" : _ai.IsRequestActive ? "Requesting" : _ai.CanRequest ? "Ready" : "Not configured";
            var latency = _ai != null ? _ai.LastLatencyMs : 0;
            var handState = _hands != null ? _hands.HandTrackingState : "Unavailable";

            return
                "\n\nP2 Spatial\n" +
                $"Source: {source}\n" +
                $"Pose: {poseHz:0} Hz   dropped: {dropped}   last seq: {sequence}\n" +
                $"Camera: {cameraState}\n" +
                $"AI: {aiState}   latency: {latency} ms\n" +
                $"Hands: {handState}";
        }

        private void ResolveServices()
        {
            if (receiver == null) return;
            if (_telemetry == null) _telemetry = receiver.GetComponent<SpatialTelemetryService>();
            if (_dataPlane == null) _dataPlane = receiver.GetComponent<SpatialDataPlaneHub>();
            if (_vision == null) _vision = receiver.GetComponent<QuestVisionService>();
            if (_ai == null) _ai = receiver.GetComponent<QuestAiClient>();
            if (_hands == null) _hands = receiver.GetComponent<SpatialHandTrackingService>();
        }
    }
}
