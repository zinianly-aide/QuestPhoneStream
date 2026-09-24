using UnityEngine;

namespace QuestPhoneStream.Interaction.Backends.XRI
{
    /// <summary>
    /// Keeps bare-hand window manipulation off the content surface. Surface pinches
    /// belong to Poke/touch; panel manipulation starts only near the visible frame handle.
    /// </summary>
    public static class HandGrabPolicy
    {
        public static bool TryGetVisibleHandlePoint(Renderer handleRenderer, Vector3 position, float maxDistance,
            out Vector3 closest, out float distance)
        {
            closest = default;
            distance = float.MaxValue;
            if (handleRenderer == null || !handleRenderer.enabled || !handleRenderer.gameObject.activeInHierarchy || maxDistance <= 0f)
                return false;

            closest = handleRenderer.bounds.ClosestPoint(position);
            distance = Vector3.Distance(position, closest);
            return distance <= maxDistance;
        }
    }
}
