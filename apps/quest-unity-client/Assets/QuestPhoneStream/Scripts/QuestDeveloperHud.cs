using UnityEngine;
using UnityEngine.UI;

namespace QuestPhoneStream
{
    /// <summary>Small XR-facing diagnostics panel for P2 data planes and vision services.</summary>
    public sealed class QuestDeveloperHud : MonoBehaviour
    {
        public QuestWebRtcReceiver receiver;
        public QuestSignalingClient signaling;
        public float refreshHz = 4f;

        private SpatialTelemetryService _telemetry;
        private SpatialDataPlaneHub _dataPlane;
        private QuestVisionService _vision;
        private QuestAiClient _ai;
        private SpatialHandTrackingService _hands;
        private Canvas _canvas;
        private Text _text;
        private float _nextRefresh;

        private void Start()
        {
            if (receiver == null) receiver = GetComponent<QuestWebRtcReceiver>();
            if (signaling == null) signaling = receiver != null ? receiver.signaling : GetComponent<QuestSignalingClient>();
            if (PlayerPrefs.GetInt("QuestPhoneStream_DeveloperHud", Debug.isDebugBuild ? 1 : 0) == 0) return;
            EnsureServices();
            BuildHud();
            Refresh();
        }

        private void Update()
        {
            if (_text == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 1f / Mathf.Max(1f, refreshHz);
            EnsureServices();
            Refresh();
        }

        private void EnsureServices()
        {
            if (receiver == null) return;
            if (_telemetry == null) _telemetry = receiver.GetComponent<SpatialTelemetryService>();
            if (_dataPlane == null) _dataPlane = receiver.GetComponent<SpatialDataPlaneHub>();
            if (_vision == null) _vision = receiver.GetComponent<QuestVisionService>();
            if (_ai == null) _ai = receiver.GetComponent<QuestAiClient>();
            if (_hands == null) _hands = receiver.GetComponent<SpatialHandTrackingService>();
        }

        private void BuildHud()
        {
            var camera = receiver != null ? receiver.xrCamera : null;
            if (camera == null) return;
            var go = new GameObject("DeveloperHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = new Vector3(-0.42f, 0.28f, 0.85f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.001f;
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 500;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(720, 300);

            var textGo = new GameObject("Stats", typeof(RectTransform), typeof(Text), typeof(Outline));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 8);
            textRect.offsetMax = new Vector2(-12, -8);
            _text = textGo.GetComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _text.fontSize = 25;
            _text.alignment = TextAnchor.UpperLeft;
            _text.color = Color.white;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = textGo.GetComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.effectColor = new Color(0, 0, 0, 0.9f);
        }

        private void Refresh()
        {
            if (_text == null) return;
            var source = signaling != null && !string.IsNullOrWhiteSpace(signaling.androidDeviceId)
                ? signaling.androidDeviceId : "—";
            var poseHz = _telemetry != null ? _telemetry.PoseStreamHz : 0f;
            var dropped = _dataPlane != null ? _dataPlane.DroppedFrames : 0;
            var sequence = _dataPlane != null ? _dataPlane.LastSequence : -1;
            var cameraState = _vision != null ? _vision.CameraState : "Unavailable";
            var aiState = _ai == null ? "Unavailable" : _ai.IsRequestActive ? "Requesting" : _ai.CanRequest ? "Ready" : "Not configured";
            var latency = _ai != null ? _ai.LastLatencyMs : 0;
            var handState = _hands != null ? _hands.HandTrackingState : "Unavailable";

            _text.text =
                "QuestPhoneStream DEV\n" +
                $"Source: {source}\n" +
                $"Pose: {poseHz:0} Hz   dropped: {dropped}   last seq: {sequence}\n" +
                $"Camera: {cameraState}\n" +
                $"AI: {aiState}   latency: {latency} ms\n" +
                $"Hands: {handState}";
        }
    }

    internal static class QuestDeveloperHudBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var receiver in UnityEngine.Object.FindObjectsOfType<QuestWebRtcReceiver>())
            {
                var hud = receiver.GetComponent<QuestDeveloperHud>() ?? receiver.gameObject.AddComponent<QuestDeveloperHud>();
                hud.receiver = receiver;
                hud.signaling = receiver.signaling;
            }
        }
    }
}
