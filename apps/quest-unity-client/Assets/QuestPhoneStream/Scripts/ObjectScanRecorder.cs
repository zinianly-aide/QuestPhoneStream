using System;
using System.IO;
using UnityEngine;

namespace QuestPhoneStream
{
    /// <summary>
    /// G0 object-scan recorder: saves RGB JPEG frames plus timestamp, exact PCA camera
    /// pose and intrinsics. It intentionally stays local to the Quest filesystem; LAN
    /// upload/reconstruction is a later gate.
    /// </summary>
    public sealed class ObjectScanRecorder : MonoBehaviour
    {
        public QuestVisionService vision;
        [Range(50, 100)] public int jpegQuality = 92;
        [Range(10, 120)] public int maxFrames = 60;
        [Min(0f)] public float minSecondsBetweenFrames = 0.35f;
        [Min(0f)] public float minTranslationMeters = 0.06f;
        [Min(0f)] public float minRotationDegrees = 8f;
        public bool autoStopAtMaxFrames = true;

        private IObjectScanCalibrationProvider _calibration;
        private ObjectScanManifest _manifest;
        private string _sessionPath;
        private string _framesPath;
        private bool _scanning;
        private bool _ownsCamera;
        private bool _hasPreviousPose;
        private Pose _previousPose;
        private float _lastCaptureAt = float.NegativeInfinity;

        public bool IsScanning => _scanning;
        public int FrameCount => _manifest?.frameCount ?? 0;
        public string SessionPath => _sessionPath;
        public string StateText { get; private set; } = "Idle";

        private void Awake() => EnsureDependencies();

        private void Update()
        {
            if (!_scanning) return;
            if (_manifest.frameCount >= Mathf.Max(1, maxFrames))
            {
                if (autoStopAtMaxFrames) StopScan();
                return;
            }
            TryCaptureCandidate(force: false);
        }

        public void RequestCameraAndStart(Action<bool> completion = null)
        {
            EnsureDependencies();
            if (vision == null) { completion?.Invoke(false); return; }
            if (vision.IsAuthorized) { completion?.Invoke(StartScan()); return; }
            vision.RequestPermission(granted => completion?.Invoke(granted && StartScan()));
        }

        public bool StartScan()
        {
            if (_scanning) return true;
            EnsureDependencies();
            if (vision == null)
            {
                StateText = "Vision service unavailable";
                return false;
            }
            if (!vision.IsAuthorized)
            {
                StateText = "Camera permission required";
                return false;
            }

            var wasActive = vision.IsActive;
            if (!wasActive && !vision.StartCamera())
            {
                StateText = "Camera unavailable";
                return false;
            }
            _ownsCamera = !wasActive;
            _calibration?.Refresh();
            if (_calibration == null || !_calibration.IsReady)
            {
                if (_ownsCamera) vision.StopCamera();
                _ownsCamera = false;
                StateText = "PCA pose/intrinsics unavailable";
                return false;
            }

            var sessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _sessionPath = Path.Combine(Application.persistentDataPath, "ObjectScans", sessionId);
            _framesPath = Path.Combine(_sessionPath, "frames");
            Directory.CreateDirectory(_framesPath);
            _manifest = new ObjectScanManifest
            {
                sessionId = sessionId,
                createdUtc = DateTime.UtcNow.ToString("O")
            };
            _hasPreviousPose = false;
            _lastCaptureAt = float.NegativeInfinity;
            _scanning = true;
            StateText = "Scanning";
            WriteManifest();
            return true;
        }

        public bool CaptureNow() => _scanning && TryCaptureCandidate(force: true);

        public void StopScan()
        {
            if (!_scanning && _manifest == null) return;
            _scanning = false;
            WriteManifest();
            if (_ownsCamera && vision != null) vision.StopCamera();
            _ownsCamera = false;
            StateText = _manifest == null ? "Idle" : $"Saved {_manifest.frameCount} frames";
        }

        private bool TryCaptureCandidate(bool force)
        {
            if (_calibration == null || !_calibration.TryRead(out var pose, out var intrinsics))
            {
                StateText = "Waiting for PCA calibration";
                return false;
            }

            var now = Time.unscaledTime;
            if (!ObjectScanSamplingPolicy.ShouldCapture(
                    _hasPreviousPose,
                    _previousPose,
                    pose,
                    now - _lastCaptureAt,
                    minSecondsBetweenFrames,
                    minTranslationMeters,
                    minRotationDegrees,
                    force))
                return false;

            var frame = vision?.CaptureSingleFrame();
            if (frame?.texture == null)
            {
                StateText = "Waiting for RGB frame";
                return false;
            }
            var jpg = frame.EncodeJpg(jpegQuality);
            if (jpg == null || jpg.Length == 0)
            {
                StateText = "JPEG encode failed";
                return false;
            }

            var index = _manifest.frameCount;
            var imageName = index.ToString("D6") + ".jpg";
            File.WriteAllBytes(Path.Combine(_framesPath, imageName), jpg);
            _manifest.frames.Add(new ObjectScanFrameMetadata
            {
                index = index,
                image = "frames/" + imageName,
                timestampMs = frame.timestamp,
                width = frame.width,
                height = frame.height,
                cameraPosition = pose.position,
                cameraRotation = pose.rotation,
                intrinsics = intrinsics
            });
            _manifest.frameCount = _manifest.frames.Count;
            _previousPose = pose;
            _hasPreviousPose = true;
            _lastCaptureAt = now;
            StateText = $"Scanning · {_manifest.frameCount}/{Mathf.Max(1, maxFrames)}";
            WriteManifest();

            if (_manifest.frameCount >= Mathf.Max(1, maxFrames) && autoStopAtMaxFrames)
                StopScan();
            return true;
        }

        private void EnsureDependencies()
        {
            if (vision == null) vision = GetComponent<QuestVisionService>() ?? FindFirstObjectByType<QuestVisionService>();
            if (_calibration == null)
                _calibration = new MetaPassthroughCalibrationProvider(vision != null ? vision.gameObject : gameObject);
        }

        private void WriteManifest()
        {
            if (_manifest == null || string.IsNullOrEmpty(_sessionPath)) return;
            Directory.CreateDirectory(_sessionPath);
            File.WriteAllText(Path.Combine(_sessionPath, "manifest.json"), JsonUtility.ToJson(_manifest, true));
        }

        private void OnDestroy()
        {
            if (_scanning || _ownsCamera) StopScan();
        }
    }

    internal static class ObjectScanBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            foreach (var signaling in UnityEngine.Object.FindObjectsOfType<QuestSignalingClient>())
                if (signaling.GetComponent<ObjectScanRecorder>() == null)
                    signaling.gameObject.AddComponent<ObjectScanRecorder>();
        }
    }
}
