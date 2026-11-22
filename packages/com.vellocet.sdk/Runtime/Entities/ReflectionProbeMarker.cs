using System;
using UnityEngine;
using UnityEngine.Rendering;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("env_reflection_probe")]
    [EntityAuthoring("Environment", EntityPlacementKind.Volume, "Reflection Probe Zone",
        Description = "Baked reflections within this area. Resize to cover a room. Rotated zones use their enclosing axis-aligned box in Unity.")]
    [RequireComponent(typeof(ReflectionProbe)), DisallowMultipleComponent]
    public sealed class ReflectionProbeMarker : EntityMarker, ICenteredBoundsEntityMarker
    {
        public enum CaptureResolution { Pixels64 = 64, Pixels128 = 128, Pixels256 = 256, Pixels512 = 512, Pixels1024 = 1024 }

        [SerializeField, Tooltip("Cubemap resolution per face. Higher resolutions use more bake time and memory.")]
        private CaptureResolution resolution = CaptureResolution.Pixels128;
        [SerializeField, Min(0), Tooltip("Reflection brightness multiplier.")]
        private float intensity = 1;
        [SerializeField, Min(0), Tooltip("Distance in metres over which the probe blends at the edge of its area.")]
        private float blendDistance = 1;
        [SerializeField, Min(0), Tooltip("Higher values take priority where reflection areas overlap.")]
        private int importance;
        [SerializeField, Tooltip("Use box projection for room-sized reflections.")]
        private bool boxProjection = true;

        public Vector3 BoundsSize => new(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (!TryGetComponent<ReflectionProbe>(out var probe)) return;
            // Unity probe sizes are world-space, axis-aligned and independent of Transform scale.
            var x = transform.TransformVector(Vector3.right);
            var y = transform.TransformVector(Vector3.up);
            var z = transform.TransformVector(Vector3.forward);
            probe.size = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            probe.center = Vector3.zero;
            probe.mode = ReflectionProbeMode.Baked;
            probe.hdr = true;
            probe.resolution = (int)resolution;
            probe.intensity = intensity;
            probe.blendDistance = blendDistance;
            probe.importance = importance;
            probe.boxProjection = boxProjection;
            probe.enabled = enabled;
        }
#endif
    }
}
