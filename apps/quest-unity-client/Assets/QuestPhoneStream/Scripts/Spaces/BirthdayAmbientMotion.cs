using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    /// <summary>
    /// B2.7 gentle ambience. Drives only 3 grouped balloon roots and the 2
    /// already-existing warm lights. No per-frame allocations, new lights,
    /// physics colliders, textures, particles or changes to saved scene poses.
    /// </summary>
    public sealed class BirthdayAmbientMotion : MonoBehaviour
    {
        struct Cluster
        {
            public Transform transform;
            public Vector3 initialPosition;
            public Quaternion initialRotation;
        }
        struct Glow
        {
            public Light light;
            public float initialIntensity;
        }
        readonly List<Cluster> _clusters = new List<Cluster>(3);
        readonly List<Glow> _glows = new List<Glow>(2);
        public bool MotionEnabled { get; private set; } = true;
        public int ClusterCount => _clusters.Count;
        public int GlowCount => _glows.Count;

        public void RegisterCluster(Transform cluster)
        {
            if (!cluster) return;
            foreach (var existing in _clusters)
                if (existing.transform == cluster) return;
            _clusters.Add(new Cluster
            {
                transform = cluster,
                initialPosition = cluster.localPosition,
                initialRotation = cluster.localRotation
            });
        }
        public void RegisterGlow(Light light)
        {
            if (!light) return;
            foreach (var existing in _glows)
                if (existing.light == light) return;
            _glows.Add(new Glow { light = light, initialIntensity = light.intensity });
        }
        public void SetMotionEnabled(bool enabled)
        {
            MotionEnabled = enabled;
            if (!enabled) Restore();
        }
        public void Tick(float seconds)
        {
            if (!MotionEnabled || !isActiveAndEnabled) return;
            // Uses only local transforms of generated decorations, never a CAD
            // furniture root or an editable SceneWorkspaceObject transform.
            for (int i = 0; i < _clusters.Count; i++)
            {
                var entry = _clusters[i];
                if (!entry.transform) continue;
                var wave = seconds * (0.62f + i * 0.08f) + i * 1.36f;
                entry.transform.localPosition = entry.initialPosition + new Vector3(
                    0.012f * Mathf.Sin(wave),
                    0.004f * Mathf.Sin(wave * 0.72f),
                    0.006f * Mathf.Sin(wave * 0.86f));
                entry.transform.localRotation = entry.initialRotation *
                    Quaternion.Euler(0, 0, 1.4f * Mathf.Sin(wave * 0.8f));
            }
            for (int i = 0; i < _glows.Count; i++)
            {
                var entry = _glows[i];
                if (!entry.light) continue;
                // Warm, slow candle pulse using existing lights only. Never
                // change enabled (the user controls that via ToggleLights).
                entry.light.intensity = entry.initialIntensity *
                    (0.985f + 0.015f * Mathf.Sin(seconds * 1.2f + i));
            }
        }
        void Update() { Tick(Time.time); }
        public void Restore()
        {
            foreach (var entry in _clusters)
            {
                if (!entry.transform) continue;
                entry.transform.localPosition = entry.initialPosition;
                entry.transform.localRotation = entry.initialRotation;
            }
            foreach (var entry in _glows)
                if (entry.light) entry.light.intensity = entry.initialIntensity;
        }
        void OnDisable() { Restore(); }
        void OnDestroy() { Restore(); }
    }
}
