using System;
using UnityEngine;
using UnityEngine.Video;

namespace QuestPhoneStream
{
    public enum MediaPlaybackState { Idle, Preparing, Playing, Paused, Buffering, Error, Ended }

    public sealed class MediaPlaybackController : MonoBehaviour
    {
        public VideoPlayer videoPlayer;
        public FlatMediaRenderer renderer;
        public VrMediaRenderer vrRenderer;
        public FlatMediaPanelController flatPanelController;
        public Renderer phoneScreenRenderer;
        public MediaPlaybackState State { get; private set; } = MediaPlaybackState.Idle;
        public bool IsMediaMode { get; private set; }
        public MediaVideoProfile Profile { get; private set; } = MediaVideoProfile.Default;
        public double CurrentTime => videoPlayer == null ? 0 : videoPlayer.time;
        public double Duration => videoPlayer == null ? 0 : videoPlayer.length;
        public event Action<MediaPlaybackState> StateChanged;

        private int _generation;
        private VideoPlayer.EventHandler _prepareHandler;
        private VideoPlayer.ErrorEventHandler _errorHandler;
        private VideoPlayer.EventHandler _endedHandler;

        private void Awake()
        {
            if (videoPlayer == null) videoPlayer = gameObject.GetComponent<VideoPlayer>() ?? gameObject.AddComponent<VideoPlayer>();
            if (renderer == null) renderer = gameObject.GetComponent<FlatMediaRenderer>() ?? gameObject.AddComponent<FlatMediaRenderer>();
            if (vrRenderer == null) vrRenderer = gameObject.GetComponent<VrMediaRenderer>() ?? gameObject.AddComponent<VrMediaRenderer>();
            if (flatPanelController == null) flatPanelController = gameObject.GetComponent<FlatMediaPanelController>();
            vrRenderer.Initialize(null, renderer);
            videoPlayer.playOnAwake = false;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        }

        public void PlayUrl(string url) => PlayUrl(url, MediaVideoProfile.Default);

        public void PlayUrl(string url, MediaVideoProfile profile)
        {
            if (videoPlayer == null || string.IsNullOrWhiteSpace(url)) { SetState(MediaPlaybackState.Error); return; }
            var generation = ++_generation;
            Profile = profile.Normalize();
            IsMediaMode = true;
            gameObject.SetActive(true);
            flatPanelController?.SetProjection(Profile.projection);
            if (phoneScreenRenderer != null) phoneScreenRenderer.enabled = false;
            DetachHandlers();
            videoPlayer.Stop();
            vrRenderer?.Release();
            renderer.Release();
            videoPlayer.url = url;
            _prepareHandler = player => {
                if (generation != _generation || player != videoPlayer || !IsMediaMode) return;
                renderer.Prepare((int)player.width, (int)player.height);
                flatPanelController?.SetVideoDimensions((int)player.width, (int)player.height);
                flatPanelController?.SetProjection(Profile.projection);
                player.targetTexture = renderer.RenderTexture;
                renderer.SetTexture(renderer.RenderTexture);
                vrRenderer?.Apply(renderer.RenderTexture, Profile.projection, Profile.fov, Profile.stereo, Profile.eyeOrder);
                player.Play();
                SetState(MediaPlaybackState.Playing);
            };
            _errorHandler = (player, message) => { if (generation == _generation && player == videoPlayer) SetState(MediaPlaybackState.Error); };
            _endedHandler = player => { if (generation == _generation && player == videoPlayer && IsMediaMode) SetState(MediaPlaybackState.Ended); };
            videoPlayer.prepareCompleted += _prepareHandler;
            videoPlayer.errorReceived += _errorHandler;
            videoPlayer.loopPointReached += _endedHandler;
            SetState(MediaPlaybackState.Preparing);
            videoPlayer.Prepare();
        }

        public void Pause() { if (videoPlayer != null && videoPlayer.isPlaying) { videoPlayer.Pause(); SetState(MediaPlaybackState.Paused); } }
        public void Resume() { if (videoPlayer != null && videoPlayer.isPrepared) { videoPlayer.Play(); SetState(MediaPlaybackState.Playing); } }
        public void Stop()
        {
            ++_generation;
            DetachHandlers();
            videoPlayer?.Stop();
            vrRenderer?.Release();
            renderer?.Release();
            flatPanelController?.SetProjection(ProjectionMode.Flat);
            IsMediaMode = false;
            if (phoneScreenRenderer != null) phoneScreenRenderer.enabled = true;
            if (renderer?.targetRenderer != null) renderer.targetRenderer.enabled = true;
            gameObject.SetActive(false);
            SetState(MediaPlaybackState.Idle);
        }

        public void ApplyProfile(MediaVideoProfile profile)
        {
            Profile = profile.Normalize();
            flatPanelController?.SetProjection(Profile.projection);
            if (!IsMediaMode || renderer?.RenderTexture == null) return;
            vrRenderer?.Apply(renderer.RenderTexture, Profile.projection, Profile.fov, Profile.stereo, Profile.eyeOrder);
        }

        public void Seek(double seconds)
        {
            if (videoPlayer == null || !videoPlayer.isPrepared) return;
            videoPlayer.time = Math.Max(0, Math.Min(seconds, videoPlayer.length));
        }

        public void SetVolume(float volume)
        {
            if (videoPlayer == null) return;
            videoPlayer.SetDirectAudioVolume(0, Mathf.Clamp01(volume));
        }

        public void SetPhoneScreenMode() { Stop(); IsMediaMode = false; gameObject.SetActive(false); }

        private void SetState(MediaPlaybackState state) { State = state; StateChanged?.Invoke(state); }

        private void DetachHandlers()
        {
            if (videoPlayer == null) return;
            if (_prepareHandler != null) videoPlayer.prepareCompleted -= _prepareHandler;
            if (_errorHandler != null) videoPlayer.errorReceived -= _errorHandler;
            if (_endedHandler != null) videoPlayer.loopPointReached -= _endedHandler;
            _prepareHandler = null; _errorHandler = null; _endedHandler = null;
        }

        private void OnDestroy()
        {
            DetachHandlers();
            vrRenderer?.Release();
            renderer?.Release();
        }
    }
}
