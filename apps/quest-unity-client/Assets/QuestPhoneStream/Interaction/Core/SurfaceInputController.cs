using UnityEngine;

namespace QuestPhoneStream.Interaction
{
    public class SurfaceInputController : MonoBehaviour
    {
        [Tooltip("A MonoBehaviour implementing IPhonePanelTouchMapper (PanelInputMapper in the current runtime).")]
        public MonoBehaviour mappingProvider;
        public SpatialPanelManipulator manipulator;
        private bool _active;
        private InteractionSourceType _source;
        private PointerModality _modality;
        private Vector2 _startUv;
        private Vector2 _lastUv;
        private float _startTime;
        public bool IsActive => _active;
        private IPanelSurfaceInputHandler _handler;
        public IPanelSurfaceInputHandler Handler => _handler;
        public void SetHandler(IPanelSurfaceInputHandler handler) { Clear(); _handler = handler; }

        public bool Process(PointerEvent pointer)
        {
            if (pointer.phase == InteractionPhase.PressCancel)
            {
                if (!Owns(pointer)) return false;
                Clear();
                return true;
            }

            if (_handler != null)
            {
                if (manipulator != null && manipulator.IsGrabActive)
                {
                    if (Owns(pointer)) Clear();
                    return false;
                }

                if (_active && !Owns(pointer)) return false;
                if (_active && pointer.phase == InteractionPhase.PressBegin) return false;

                if (pointer.phase == InteractionPhase.PressEnd && Owns(pointer))
                {
                    try { return _handler.Process(pointer); }
                    finally { ReleaseOwner(); }
                }

                var handled = _handler.Process(pointer);
                if (handled && pointer.phase == InteractionPhase.PressBegin)
                    BeginOwner(pointer);
                return handled;
            }

            var mapper = mappingProvider as IPhonePanelTouchMapper;
            if (mapper == null || mapper.IsInputBlocked) { Clear(); return false; }

            // A backend may report release after the pointer has left the collider.
            // Finish from the last valid UV so a press never leaks into the next interaction.
            if (pointer.phase == InteractionPhase.PressEnd && Owns(pointer))
            {
                if (mapper.TryMapWorldPointToUv(pointer.worldPosition, pointer.worldNormal, out var releaseUv))
                    _lastUv = releaseUv;
                End();
                return true;
            }

            if (!mapper.TryMapWorldPointToUv(pointer.worldPosition, pointer.worldNormal, out var uv)) return false;
            if (pointer.phase == InteractionPhase.PressBegin)
            {
                if (_active || (manipulator != null && manipulator.IsGrabActive)) return false;
                _startUv = _lastUv = uv;
                _startTime = Time.unscaledTime;
                BeginOwner(pointer);
            }
            else if (Owns(pointer) && pointer.phase == InteractionPhase.PressMove)
            {
                _lastUv = uv;
            }
            return true;
        }

        public void End()
        {
            if (!_active) return;
            var mapper = mappingProvider as IPhonePanelTouchMapper;
            if (mapper == null) { Clear(); return; }

            var start = mapper.MapUvToAndroidPixels(_startUv);
            var end = mapper.MapUvToAndroidPixels(_lastUv);
            var duration = Mathf.Clamp(Mathf.RoundToInt((Time.unscaledTime - _startTime) * 1000f), 100, 2000);
            var dx = end.x - start.x;
            var dy = end.y - start.y;
            ReleaseOwner();

            if (dx * dx + dy * dy >= mapper.SwipeThresholdPixels * mapper.SwipeThresholdPixels)
                mapper.SendSwipe(start, end, duration);
            else
                mapper.SendClick(start);
        }

        public void Clear()
        {
            _handler?.Cancel();
            _active = false;
            _source = default;
            _modality = default;
            manipulator?.EndScreenTouch();
        }

        private bool Owns(PointerEvent pointer) =>
            _active && _source == pointer.source && _modality == pointer.modality;

        private void BeginOwner(PointerEvent pointer)
        {
            _active = true;
            _source = pointer.source;
            _modality = pointer.modality;
            manipulator?.BeginScreenTouch();
        }

        private void ReleaseOwner()
        {
            _active = false;
            _source = default;
            _modality = default;
            manipulator?.EndScreenTouch();
        }

        protected virtual void OnDisable() => Clear();
    }
}
