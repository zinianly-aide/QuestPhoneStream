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
        private Vector2 _startUv;
        private Vector2 _lastUv;
        private float _startTime;
        public bool IsActive => _active;
        private IPanelSurfaceInputHandler _handler;
        public IPanelSurfaceInputHandler Handler => _handler;
        public void SetHandler(IPanelSurfaceInputHandler handler) { Clear(); _handler = handler; }

        public bool Process(PointerEvent pointer)
        {
            if (pointer.phase == InteractionPhase.PressCancel) { Clear(); return true; }
            if (_handler != null)
            {
                if (manipulator != null && manipulator.IsGrabActive) { Clear(); return false; }
                var handled = _handler.Process(pointer);
                if (handled && pointer.phase == InteractionPhase.PressBegin) { _active = true; manipulator?.BeginScreenTouch(); }
                if (pointer.phase == InteractionPhase.PressEnd) { _active = false; manipulator?.EndScreenTouch(); }
                return handled;
            }
            var mapper = mappingProvider as IPhonePanelTouchMapper;
            if (mapper == null || mapper.IsInputBlocked) { Clear(); return false; }
            // A backend may report release after the pointer has left the collider.
            // Finish from the last valid UV so a press never leaks into the next interaction.
            if (pointer.phase == InteractionPhase.PressEnd && _active && _source == pointer.source)
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
                _active = true; _source = pointer.source; _startUv = _lastUv = uv; _startTime = Time.unscaledTime;
                manipulator?.BeginScreenTouch();
            }
            else if (_active && _source == pointer.source && pointer.phase == InteractionPhase.PressMove) _lastUv = uv;
            return true;
        }

        public void End()
        {
            if (!_active) return;
            _active = false;
            var mapper = mappingProvider as IPhonePanelTouchMapper;
            if (mapper == null) { Clear(); return; }
            var start = mapper.MapUvToAndroidPixels(_startUv); var end = mapper.MapUvToAndroidPixels(_lastUv);
            var dx = end.x - start.x; var dy = end.y - start.y;
            if (dx * dx + dy * dy >= mapper.SwipeThresholdPixels * mapper.SwipeThresholdPixels)
                mapper.SendSwipe(start, end, Mathf.Clamp(Mathf.RoundToInt((Time.unscaledTime - _startTime) * 1000f), 100, 2000));
            else mapper.SendClick(start);
            manipulator?.EndScreenTouch();
        }
        public void Clear() { _handler?.Cancel(); _active = false; manipulator?.EndScreenTouch(); }
        protected virtual void OnDisable() => Clear();
    }
}
