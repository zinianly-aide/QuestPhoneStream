using System;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace QuestPhoneStream
{
    /// <summary>
    /// CPU-path Passthrough Camera Access provider.
    ///
    /// Why this exists: MRUK's PassthroughCameraAccess component drives the GPU texture
    /// path (CameraPlay -> CameraSetNativeTexture -> PcaGpuProviderVulkan::initializeFromUnity).
    /// On Unity 2022.3 the native lib cannot obtain the Unity graphics interfaces and aborts
    /// with "Unsupported graphics API 0", no matter if the player runs Vulkan or GLES.
    ///
    /// The shared library also exports a CPU capture API (CameraAcquireLatestCpuImage /
    /// CameraReleaseLatestCpuImage / CameraPlay / CameraStop) that never touches Unity
    /// graphics. This provider uses only those entry points, so the crash path is avoided
    /// entirely. Frames are delivered as raw RGBA32 CPU buffers (width * height * 4).
    /// </summary>
    public sealed class QuestPcaCpuProvider : IQuestVisionProvider
    {
        public const string HeadsetCameraPermission = "horizonos.permission.HEADSET_CAMERA";

        [StructLayout(LayoutKind.Sequential)]
        private struct MrukCameraIntrinsics
        {
            public Vector2 focalLength;
            public Vector2 principalPoint;
            public Vector3 lensTranslation;
            public Quaternion lensRotation;
            public Vector2Int sensorResolution;
        }

        [DllImport("mrutilitykitshared")]
        private static extern bool CameraPlay(int eyeIndex, ref int width, ref int height, ref MrukCameraIntrinsics intrinsics, int maxFramerate);

        [DllImport("mrutilitykitshared")]
        private static extern IntPtr CameraAcquireLatestCpuImage(int eyeIndex, ref long timestampMicrosecondsRealtime, ref long timestampNsMonotonic);

        [DllImport("mrutilitykitshared")]
        private static extern void CameraReleaseLatestCpuImage(int eyeIndex);

        [DllImport("mrutilitykitshared")]
        private static extern void CameraStop(int eyeIndex);

        private const int EyeIndex = 0;
        private const int DefaultWidth = 1280;
        private const int DefaultHeight = 960;
        private const int MaxFramerate = 30;

        private Texture2D _texture;
        private bool _playing;
        private long _lastTimestampMicros;

        public bool IsAvailable => true;

        public bool IsAuthorized
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(HeadsetCameraPermission);
#else
                return false;
#endif
            }
        }

        public bool IsActive => _playing && IsAuthorized;

        public string StateText => !IsAvailable ? "Unavailable (PCA provider not present)" :
            !IsAuthorized ? "Permission required" : IsActive ? "Active" : "Ready";

        public void Refresh() { }

        public void RequestPermission(Action<bool> completion)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(HeadsetCameraPermission))
            {
                completion?.Invoke(true);
                return;
            }
            var callbacks = new PermissionCallbacks();
            var completed = false;
            Action<bool> finish = granted =>
            {
                if (completed) return;
                completed = true;
                completion?.Invoke(granted);
            };
            callbacks.PermissionGranted += _ => finish(true);
            callbacks.PermissionDenied += _ => finish(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => finish(false);
            Permission.RequestUserPermission(HeadsetCameraPermission, callbacks);
#else
            completion?.Invoke(false);
#endif
        }

        public bool StartCapture()
        {
            if (_playing) return true;
            if (!IsAuthorized) return false;

            int w = DefaultWidth;
            int h = DefaultHeight;
            var intrinsics = default(MrukCameraIntrinsics);
            try
            {
                if (!CameraPlay(EyeIndex, ref w, ref h, ref intrinsics, MaxFramerate))
                {
                    Debug.LogError("[PcaCpu] CameraPlay failed: native returned false.");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PcaCpu] CameraPlay threw: " + e);
                return false;
            }

            _texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _playing = true;
            Debug.Log($"[PcaCpu] CameraPlay OK: {w}x{h} cameraIndex={EyeIndex}");
            return true;
        }

        public void StopCapture()
        {
            if (!_playing) return;
            try
            {
                CameraStop(EyeIndex);
            }
            catch (Exception e)
            {
                Debug.LogError("[PcaCpu] CameraStop threw: " + e);
            }
            _playing = false;
            if (_texture != null)
            {
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }
        }

        public QuestVisionFrame CaptureFrame()
        {
            if (!IsActive || _texture == null) return null;

            long tsRealtime = 0;
            long tsMonotonic = 0;
            IntPtr buffer = IntPtr.Zero;
            try
            {
                buffer = CameraAcquireLatestCpuImage(EyeIndex, ref tsRealtime, ref tsMonotonic);
                if (buffer == IntPtr.Zero) return null;

                var bytes = new byte[_texture.width * _texture.height * 4];
                Marshal.Copy(buffer, bytes, 0, bytes.Length);
                _texture.LoadRawTextureData(bytes);
                _texture.Apply(false, false);
                _lastTimestampMicros = tsRealtime;
            }
            catch (Exception e)
            {
                Debug.LogError("[PcaCpu] CaptureFrame threw: " + e);
                return null;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    try { CameraReleaseLatestCpuImage(EyeIndex); }
                    catch (Exception e) { Debug.LogError("[PcaCpu] Release threw: " + e); }
                }
            }

            var copy = CopyTexture(_texture);
            if (copy == null) return null;

            return new QuestVisionFrame
            {
                texture = copy,
                width = copy.width,
                height = copy.height,
                timestamp = _lastTimestampMicros / 1000L
            };
        }

        private static Texture2D CopyTexture(Texture source)
        {
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                copy.Apply(false, false);
                return copy;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }
    }
}
