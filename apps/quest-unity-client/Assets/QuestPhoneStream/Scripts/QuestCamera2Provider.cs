using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace QuestPhoneStream
{
    /// <summary>
    /// Camera2-path Passthrough Camera Access provider for Quest 3S.
    ///
    /// MRUK's PassthroughCameraAccess GPU texture path aborts on Unity 2022.3
    /// ("Unsupported graphics API 0") and its CPU capture API is Android-disabled.
    /// The headset camera (device "50" on Quest 3S) is, however, a plain Android
    /// Camera2 device once horizonos.permission.HEADSET_CAMERA is granted.
    ///
    /// This provider opens that camera through a small Java plugin (Camera2 +
    /// ImageReader YUV_420_888 -> RGBA) and delivers frames as CPU byte[].
    /// </summary>
    public sealed class QuestCamera2Provider : IQuestVisionProvider
    {
        public const string HeadsetCameraPermission = "horizonos.permission.HEADSET_CAMERA";
        private const string CameraId = "50";
        private const int RequestWidth = 1280;
        private const int RequestHeight = 960;

        private AndroidJavaObject _camera;
        private Texture2D _texture;
        private bool _requestedActive;

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

        public bool IsActive => _camera != null && IsRunning();

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
            if (IsActive) return true;
            if (!IsAuthorized) return false;

            try
            {
                _camera = GetBridge().CallStatic<AndroidJavaObject>("getInstance", GetActivity());
                if (_camera == null)
                {
                    Debug.LogError("[Cam2] getInstance returned null");
                    return false;
                }

                var ok = _camera.Call<bool>("open", CameraId, RequestWidth, RequestHeight);
                if (!ok)
                {
                    var err = _camera.Call<string>("lastError");
                    Debug.LogError("[Cam2] open failed: " + err);
                    _camera = null;
                    return false;
                }

                _requestedActive = true;
                Debug.Log("[Cam2] camera open OK " + Width() + "x" + Height());
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Cam2] StartCapture threw: " + e);
                _camera = null;
                return false;
            }
        }

        public void StopCapture()
        {
            _requestedActive = false;
            if (_camera == null) return;
            try
            {
                _camera.Call("close");
            }
            catch (Exception e)
            {
                Debug.LogError("[Cam2] close threw: " + e);
            }
            _camera = null;
            if (_texture != null)
            {
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }
        }

        public QuestVisionFrame CaptureFrame()
        {
            if (!IsActive || _camera == null) return null;

            byte[] rgba;
            int w;
            int h;
            try
            {
                // Unity IL2CPP cannot JNI-convert Java byte[]; use sbyte[] then bit-copy.
                var rgbaS = _camera.Call<sbyte[]>("capture");
                if (rgbaS == null) return null;
                rgba = new byte[rgbaS.Length];
                Buffer.BlockCopy(rgbaS, 0, rgba, 0, rgbaS.Length);
                w = _camera.Call<int>("width");
                h = _camera.Call<int>("height");
                if (w <= 0 || h <= 0 || rgba.Length != w * h * 4) return null;
            }
            catch (Exception e)
            {
                Debug.LogError("[Cam2] capture threw: " + e);
                return null;
            }

            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null) UnityEngine.Object.Destroy(_texture);
                _texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            }
            _texture.LoadRawTextureData(rgba);
            _texture.Apply(false, false);

            var copy = CopyTexture(_texture);
            if (copy == null) return null;

            return new QuestVisionFrame
            {
                texture = copy,
                width = copy.width,
                height = copy.height,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        private bool IsRunning()
        {
            try { return _camera != null && _camera.Call<bool>("isRunning"); }
            catch { return false; }
        }

        private int Width()
        {
            try { return _camera.Call<int>("width"); }
            catch { return 0; }
        }

        private int Height()
        {
            try { return _camera.Call<int>("height"); }
            catch { return 0; }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass GetBridge()
        {
            return new AndroidJavaClass("com.questphonestream.camera.QuestHeadsetCamera");
        }

        private static AndroidJavaObject GetActivity()
        {
            return new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
        }
#else
        private static AndroidJavaClass GetBridge()
        {
            throw new PlatformNotSupportedException("Camera2 provider requires Android");
        }

        private static AndroidJavaObject GetActivity()
        {
            throw new PlatformNotSupportedException("Camera2 provider requires Android");
        }
#endif

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
