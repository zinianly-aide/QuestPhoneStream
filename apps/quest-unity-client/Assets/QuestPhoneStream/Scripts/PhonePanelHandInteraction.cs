using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace QuestPhoneStream
{
    /// <summary>
    /// Minimal XR Hands bridge for PhonePanel only: index-tip poke belongs to the
    /// screen, while an index/thumb pinch near the frame belongs to panel grabbing.
    /// It intentionally does not feed the Spatial Protocol hand-telemetry path.
    /// </summary>
    public sealed class PhonePanelHandInteraction : MonoBehaviour
    {
        [Min(0.001f)] public float pokeDistance = 0.018f;
        [Min(0.001f)] public float pinchDistance = 0.028f;
        public PhonePanelSpatialInteraction panel;
        public PanelInputMapper inputMapper;

        private readonly List<XRHandSubsystem> _subsystems = new List<XRHandSubsystem>();
        private XRHandSubsystem _subsystem;
        private readonly Dictionary<string, bool> _poking = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _pinching = new Dictionary<string, bool>();

        private void Update()
        {
            var subsystem = ResolveSubsystem();
            if (subsystem == null || panel == null || inputMapper == null)
            {
                CancelAll();
                return;
            }
            ProcessHand("left-hand", subsystem.leftHand);
            ProcessHand("right-hand", subsystem.rightHand);
        }

        private void ProcessHand(string id, XRHand hand)
        {
            if (!hand.isTracked || !TryGetJointPoses(hand, out var wrist, out var index, out var thumb))
            {
                EndPoke(id);
                EndPinch(id);
                return;
            }

            var pinching = Vector3.Distance(index.position, thumb.position) <= pinchDistance;
            if (pinching && IsNear(panel.grabCollider, index.position, pokeDistance * 2f))
            {
                EndPoke(id);
                if (!IsActive(_pinching, id))
                {
                    if (panel.TryBeginGrab(id, index.position, index.rotation)) SetActive(_pinching, id, true);
                }
                if (IsActive(_pinching, id)) panel.UpdateGrab(id, index.position, index.rotation);
                return;
            }
            EndPinch(id);

            // A pinch away from the frame is the hand-ray equivalent of trigger.
            // Close-range contacts below continue through the direct finger-poke path.
            if (pinching && TryMapHandRay(wrist.position, index.position, out var rayUv))
            {
                UpdatePoke(id, rayUv);
                return;
            }

            if (!TryMapPoke(index.position, out var uv))
            {
                EndPoke(id);
                return;
            }
            UpdatePoke(id, uv);
        }

        private bool TryMapPoke(Vector3 tipPosition, out Vector2 uv)
        {
            uv = default;
            if (!IsNear(panel.screenCollider, tipPosition, pokeDistance)) return false;
            var normal = panel.transform.forward;
            // Start just in front of the panel and raycast towards it so MeshCollider
            // texture coordinates stay the single source of Android mapping truth.
            var start = tipPosition + normal * 0.04f;
            return inputMapper.TryMapHitToUv(new Ray(start, -normal), out uv);
        }

        private bool TryMapHandRay(Vector3 wristPosition, Vector3 indexPosition, out Vector2 uv)
        {
            var direction = indexPosition - wristPosition;
            if (direction.sqrMagnitude < 0.0001f)
            {
                uv = default;
                return false;
            }
            return inputMapper.TryMapHitToUv(new Ray(indexPosition, direction.normalized), out uv);
        }

        private static bool IsNear(Collider collider, Vector3 point, float distance)
        {
            if (collider == null) return false;
            return Vector3.Distance(collider.ClosestPoint(point), point) <= distance;
        }

        private static bool TryGetJointPoses(XRHand hand, out Pose wrist, out Pose index, out Pose thumb)
        {
            return hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out wrist) &&
                   hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out index) &&
                   hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out thumb);
        }

        private void UpdatePoke(string id, Vector2 uv)
        {
            if (!IsActive(_poking, id))
            {
                if (inputMapper.TryBeginExternalTouch(id, uv)) SetActive(_poking, id, true);
            }
            else
            {
                inputMapper.UpdateExternalTouch(id, uv);
            }
        }

        private XRHandSubsystem ResolveSubsystem()
        {
            if (_subsystem != null && _subsystem.running) return _subsystem;
            _subsystems.Clear();
            SubsystemManager.GetSubsystems(_subsystems);
            foreach (var candidate in _subsystems)
            {
                if (candidate != null && candidate.running)
                {
                    _subsystem = candidate;
                    return _subsystem;
                }
            }
            _subsystem = null;
            return null;
        }

        private void EndPoke(string id)
        {
            if (!IsActive(_poking, id)) return;
            inputMapper.EndExternalTouch(id);
            SetActive(_poking, id, false);
        }

        private void EndPinch(string id)
        {
            if (!IsActive(_pinching, id)) return;
            panel.EndGrab(id);
            SetActive(_pinching, id, false);
        }

        private void CancelAll()
        {
            foreach (var id in new[] { "left-hand", "right-hand" })
            {
                EndPoke(id);
                EndPinch(id);
            }
        }

        private static bool IsActive(Dictionary<string, bool> values, string id) => values.TryGetValue(id, out var active) && active;
        private static void SetActive(Dictionary<string, bool> values, string id, bool active) => values[id] = active;

        private void OnDisable() => CancelAll();
    }
}
