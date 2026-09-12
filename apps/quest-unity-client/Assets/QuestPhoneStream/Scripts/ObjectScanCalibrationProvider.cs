using System;
using System.Reflection;
using UnityEngine;

namespace QuestPhoneStream
{
    public interface IObjectScanCalibrationProvider
    {
        bool IsReady { get; }
        void Refresh();
        bool TryRead(out Pose pose, out ObjectScanIntrinsics intrinsics);
    }

    /// <summary>
    /// Compile-safe bridge to MRUK PassthroughCameraAccess. The POC branch does not
    /// take a hard package dependency yet; when MRUK is present this binds to the
    /// official GetCameraPose() and Intrinsics surface through reflection.
    /// </summary>
    public sealed class MetaPassthroughCalibrationProvider : IObjectScanCalibrationProvider
    {
        private const string DiscoveryKey = "object.scan.pca.calibration";
        private readonly GameObject _owner;
        private Component _component;
        private Type _componentType;
        private MethodInfo _getCameraPose;
        private PropertyInfo _intrinsics;

        public MetaPassthroughCalibrationProvider(GameObject owner)
        {
            _owner = owner;
            Refresh();
        }

        public bool IsReady => _component != null && _getCameraPose != null && _intrinsics != null;

        public void Refresh()
        {
            if (IsReady) return;
            _componentType = OptionalProviderDiscovery.ResolveType(
                DiscoveryKey,
                type => type.Name == "PassthroughCameraAccess" && typeof(MonoBehaviour).IsAssignableFrom(type));
            if (_componentType == null) return;

            _component = (_owner != null ? _owner.GetComponent(_componentType) : null) ??
                         UnityEngine.Object.FindObjectOfType(_componentType) as Component;
            if (_component == null) return;

            _getCameraPose = _componentType.GetMethod(
                "GetCameraPose", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            _intrinsics = _componentType.GetProperty("Intrinsics", BindingFlags.Instance | BindingFlags.Public);
        }

        public bool TryRead(out Pose pose, out ObjectScanIntrinsics intrinsics)
        {
            pose = default;
            intrinsics = null;
            if (!IsReady) Refresh();
            if (!IsReady) return false;

            try
            {
                var poseValue = _getCameraPose.Invoke(_component, null);
                if (!(poseValue is Pose cameraPose)) return false;
                var intrinsicsValue = _intrinsics.GetValue(_component);
                if (intrinsicsValue == null) return false;

                if (!TryReadVector2(intrinsicsValue, "FocalLength", out var focal) ||
                    !TryReadVector2(intrinsicsValue, "PrincipalPoint", out var principal) ||
                    !TryReadResolution(intrinsicsValue, "SensorResolution", out var resolution))
                    return false;

                var lens = Vector2.zero;
                TryReadVector2(intrinsicsValue, "LensOffset", out lens);
                pose = cameraPose;
                intrinsics = new ObjectScanIntrinsics
                {
                    valid = true,
                    fx = focal.x,
                    fy = focal.y,
                    cx = principal.x,
                    cy = principal.y,
                    sensorWidth = resolution.x,
                    sensorHeight = resolution.y,
                    lensOffsetX = lens.x,
                    lensOffsetY = lens.y
                };
                return intrinsics.sensorWidth > 0 && intrinsics.sensorHeight > 0 &&
                       intrinsics.fx > 0f && intrinsics.fy > 0f;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[QuestPhoneStream] Object scan calibration unavailable: " + error.Message);
                return false;
            }
        }

        private static bool TryReadVector2(object source, string name, out Vector2 value)
        {
            value = default;
            var member = ReadMember(source, name);
            if (member is Vector2 vector)
            {
                value = vector;
                return true;
            }
            if (member is Vector2Int integer)
            {
                value = integer;
                return true;
            }
            return false;
        }

        private static bool TryReadResolution(object source, string name, out Vector2Int value)
        {
            value = default;
            var member = ReadMember(source, name);
            if (member is Vector2Int integer)
            {
                value = integer;
                return true;
            }
            if (member is Vector2 vector)
            {
                value = new Vector2Int(Mathf.RoundToInt(vector.x), Mathf.RoundToInt(vector.y));
                return true;
            }
            return false;
        }

        private static object ReadMember(object source, string name)
        {
            var type = source.GetType();
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property != null) return property.GetValue(source);
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            return field?.GetValue(source);
        }
    }
}
