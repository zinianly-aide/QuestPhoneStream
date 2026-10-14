using System;
using UnityEngine;

namespace QuestPhoneStream.Interaction
{
    public interface IPanelSurfaceInputHandler
    {
        bool Process(PointerEvent pointer);
        void Cancel();
    }

    public static class PanelSurfaceInputFactory
    {
        public static IPanelSurfaceInputHandler Screen(string platform, IPhonePanelTouchMapper mapper, Func<bool> authorized) =>
            platform == "android" ? (IPanelSurfaceInputHandler)new AndroidScreenSurfaceInput(mapper, authorized) :
            platform == "macos" ? (IPanelSurfaceInputHandler)new MacScreenSurfaceInput(mapper, authorized) :
            new ViewOnlySurfaceInput();
    }

    public class ViewOnlySurfaceInput : IPanelSurfaceInputHandler
    {
        public virtual bool Process(PointerEvent pointer) => false;
        public virtual void Cancel() { }
    }

    // Media playback/seek stays with the media UI; no remote control dependency.
    public sealed class MediaSurfaceInput : ViewOnlySurfaceInput { }

    public class AndroidScreenSurfaceInput : IPanelSurfaceInputHandler
    {
        private readonly IPhonePanelTouchMapper _mapper;
        private readonly Func<bool> _authorized;
        private bool _active;
        private InteractionSourceType _source;
        private PointerModality _modality;
        private Vector2 _start, _last;
        private float _began;

        public AndroidScreenSurfaceInput(IPhonePanelTouchMapper mapper, Func<bool> authorized = null)
        { _mapper = mapper; _authorized = authorized; }

        public virtual bool Process(PointerEvent pointer)
        {
            if (_mapper == null || _mapper.IsInputBlocked || (_authorized != null && !_authorized()))
            {
                Cancel();
                return false;
            }

            if (pointer.phase == InteractionPhase.PressCancel)
            {
                if (!Owns(pointer)) return false;
                Cancel();
                return true;
            }

            if (pointer.phase == InteractionPhase.PressEnd)
            {
                if (!Owns(pointer)) return false;
                if (_mapper.TryMapWorldPointToUv(pointer.worldPosition, pointer.worldNormal, out var end))
                    _last = end;
                var startPixels = _mapper.MapUvToAndroidPixels(_start);
                var endPixels = _mapper.MapUvToAndroidPixels(_last);
                var duration = Mathf.Clamp(Mathf.RoundToInt((Time.unscaledTime - _began) * 1000), 100, 2000);
                Cancel();
                if ((endPixels - startPixels).sqrMagnitude >= _mapper.SwipeThresholdPixels * _mapper.SwipeThresholdPixels)
                    _mapper.SendSwipe(startPixels, endPixels, duration);
                else
                    _mapper.SendClick(startPixels);
                return true;
            }

            if (!_mapper.TryMapWorldPointToUv(pointer.worldPosition, pointer.worldNormal, out var uv)) return false;
            if (pointer.phase == InteractionPhase.PressBegin)
            {
                if (_active) return false;
                _active = true;
                _source = pointer.source;
                _modality = pointer.modality;
                _start = _last = uv;
                _began = Time.unscaledTime;
                return true;
            }

            if (Owns(pointer) && pointer.phase == InteractionPhase.PressMove)
            {
                _last = uv;
                return true;
            }
            return false;
        }

        private bool Owns(PointerEvent pointer) =>
            _active && pointer.source == _source && pointer.modality == _modality;

        public void Cancel()
        {
            _active = false;
            _source = default;
            _modality = default;
            _start = _last = default;
        }
    }

    // Identical panel coordinate semantics; macOS processes click events only
    // after explicit local user opt-in and Accessibility permission.
    public sealed class MacScreenSurfaceInput : AndroidScreenSurfaceInput
    {
        public MacScreenSurfaceInput(IPhonePanelTouchMapper mapper, Func<bool> authorized = null)
            : base(mapper, authorized) { }

        public override bool Process(PointerEvent pointer)
        {
            var processed = base.Process(pointer);
            // Diagnose real input phases, not hover/move (hundreds per second).
            if (pointer.phase == InteractionPhase.PressBegin ||
                pointer.phase == InteractionPhase.PressEnd)
                Debug.Log($"[QPS-MacInput] phase={pointer.phase} accepted={processed}");
            return processed;
        }
    }
}
