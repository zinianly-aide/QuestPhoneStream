using System;
using UnityEngine;

namespace QuestPhoneStream.Interaction
{
    public enum InteractionSourceType { LeftHand, RightHand, LeftController, RightController }
    public enum PointerModality { Poke, Ray }
    public enum InteractionPhase { HoverEnter, HoverMove, HoverExit, PressBegin, PressMove, PressEnd }
    public enum GrabPhase { Begin, Update, End }

    [Flags]
    public enum InteractionCapabilities
    {
        None = 0,
        Ray = 1 << 0,
        Poke = 1 << 1,
        Grab = 1 << 2,
        DistanceGrab = 1 << 3,
        TwoHandTransform = 1 << 4,
        HandTracking = 1 << 5,
        Controller = 1 << 6
    }

    public readonly struct PointerEvent
    {
        public readonly InteractionSourceType source;
        public readonly PointerModality modality;
        public readonly InteractionPhase phase;
        public readonly Vector3 worldPosition;
        public readonly Vector3 worldNormal;
        public readonly Collider target;
        public readonly string targetId;

        public PointerEvent(InteractionSourceType source, PointerModality modality, InteractionPhase phase,
            Vector3 worldPosition, Vector3 worldNormal, Collider target, string targetId)
        {
            this.source = source; this.modality = modality; this.phase = phase;
            this.worldPosition = worldPosition; this.worldNormal = worldNormal;
            this.target = target; this.targetId = targetId;
        }
    }

    public readonly struct TransformEvent
    {
        public readonly GrabPhase phase;
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public readonly float uniformScale;
        public readonly int activeGrabCount;
        public readonly InteractionSourceType primarySource;
        public readonly InteractionSourceType secondarySource;

        public TransformEvent(GrabPhase phase, Vector3 position, Quaternion rotation, float uniformScale,
            int activeGrabCount, InteractionSourceType primarySource, InteractionSourceType secondarySource)
        {
            this.phase = phase; this.position = position; this.rotation = rotation;
            this.uniformScale = uniformScale; this.activeGrabCount = activeGrabCount;
            this.primarySource = primarySource; this.secondarySource = secondarySource;
        }
    }

    public sealed class InteractionBackendContext
    {
        public GameObject panelRoot;
        public Collider screenCollider;
        public Collider grabCollider;
        public Camera camera;
        public object runtimeDependencies;
    }

    public interface IPhonePanelTouchMapper
    {
        bool IsInputBlocked { get; }
        int SwipeThresholdPixels { get; }
        bool TryMapWorldPointToUv(Vector3 worldPosition, Vector3 worldNormal, out Vector2 uv);
        Vector2Int MapUvToAndroidPixels(Vector2 uv);
        void SendClick(Vector2Int point);
        void SendSwipe(Vector2Int start, Vector2Int end, int durationMs);
    }
}
