using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream.Interaction
{
    // SDK-neutral pose solver. A ray grab owns a depth and a local attachment,
    // not a frame-to-frame controller position delta.
    public sealed class PanelGrabSolver
    {
        private sealed class Grab
        {
            public InteractionSourceType source;
            public Pose origin;
            public float depth;
            public Vector3 Point => origin.position + origin.rotation * Vector3.forward * depth;
        }
        private readonly List<Grab> _grabs = new List<Grab>();
        private Vector3 _basePosition, _localOffset, _baseMidpoint, _baseDirection;
        private Quaternion _baseRotation, _originRotation;
        private float _baseScale, _baseDistance;
        public int Count => _grabs.Count;
        public bool Contains(InteractionSourceType source) => _grabs.Exists(g => g.source == source);
        public void Begin(InteractionSourceType source, Pose origin, Vector3 hit, Transform panel)
        {
            if (Contains(source) || Count == 2) return;
            _grabs.Add(new Grab { source = source, origin = origin, depth = Vector3.Distance(origin.position, hit) });
            Capture(panel);
        }
        public void SetOrigin(InteractionSourceType source, Pose origin)
        {
            var grab = _grabs.Find(g => g.source == source);
            if (grab != null) grab.origin = origin;
        }
        public void End(InteractionSourceType source, Transform panel)
        {
            _grabs.RemoveAll(g => g.source == source);
            Capture(panel); // Rebase the remaining attachment at the actual clamped panel pose.
        }
        public void Clear() => _grabs.Clear();
        private void Capture(Transform panel)
        {
            if (Count == 0) return;
            _basePosition = panel.position; _baseRotation = panel.rotation;
            _baseScale = Mathf.Max(1e-4f, Mathf.Abs(panel.localScale.x));
            _originRotation = _grabs[0].origin.rotation;
            _localOffset = Quaternion.Inverse(_baseRotation) * (panel.position - _grabs[0].Point);
            if (Count == 2)
            {
                _baseMidpoint = (_grabs[0].Point + _grabs[1].Point) * .5f;
                _baseDirection = _grabs[1].Point - _grabs[0].Point;
                _baseDistance = Mathf.Max(.01f, _baseDirection.magnitude);
            }
        }
        public TransformEvent Evaluate(Transform panel, GrabPhase phase, float minScale, float maxScale)
        {
            if (Count == 0) return new TransformEvent(phase, panel.position, panel.rotation, panel.localScale.x, 0, default, default);
            var a = _grabs[0];
            var safeBaseScale = Mathf.Max(1e-4f, _baseScale);
            if (Count == 1)
            {
                var rotation = a.origin.rotation * Quaternion.Inverse(_originRotation) * _baseRotation;
                return new TransformEvent(phase, a.Point + rotation * _localOffset, rotation, _baseScale, 1, a.source, a.source);
            }
            var b = _grabs[1]; var direction = b.Point - a.Point;
            var scale = Mathf.Clamp(safeBaseScale * Mathf.Max(.01f, direction.magnitude) / _baseDistance, minScale, maxScale);
            var delta = direction.sqrMagnitude < .0001f || _baseDirection.sqrMagnitude < .0001f
                ? Quaternion.identity : Quaternion.FromToRotation(_baseDirection, direction);
            var position = (a.Point + b.Point) * .5f + delta * (_basePosition - _baseMidpoint) * (scale / safeBaseScale);
            return new TransformEvent(phase, position, delta * _baseRotation, scale, 2, a.source, b.source);
        }
    }
}
