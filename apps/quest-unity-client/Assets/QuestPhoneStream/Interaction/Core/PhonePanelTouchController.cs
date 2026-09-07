using UnityEngine;

namespace QuestPhoneStream.Interaction
{
    public sealed class PhonePanelTouchController : MonoBehaviour
    {
        [Tooltip("A MonoBehaviour implementing IPhonePanelTouchMapper (PanelInputMapper in the current runtime).")]
        public MonoBehaviour mappingProvider;
        public PhonePanelManipulator manipulator;
        private bool _active;
        private InteractionSourceType _source;
        private Vector2 _startUv;
        private Vector2 _lastUv;
        private float _startTime;

        public bool Process(PointerEvent pointer)
        {
            var mapper = mappingProvider as IPhonePanelTouchMapper;
            if (mapper == null || mapper.IsInputBlocked) { Clear(); return false; }
            // A backend may report release after the pointer has left the collider.
            // Finish from the last valid UV so a press never leaks into the next interaction.
            if (pointer.phase == InteractionPhase.PressEnd && _active && _source == pointer.source)
            {
                mapper.TryMapWorldPointToUv(pointer.worldPosition, pointer.worldNormal, out _lastUv);
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
        public void Clear() { _active = false; manipulator?.EndScreenTouch(); }
        private void OnDisable() => Clear();
    }
}
