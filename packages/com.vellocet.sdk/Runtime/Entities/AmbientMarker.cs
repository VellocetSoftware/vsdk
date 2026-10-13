// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Audio;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Tooling;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("env_ambient")]
    [EntityAuthoring("Audio", EntityPlacementKind.Point, "Ambient")]
    [DisallowMultipleComponent]
    public sealed class AmbientMarker : EntityMarker<AmbientMarker.Input, AmbientMarker.Output>,
        IToolVolumeVisualProvider
    {
        public enum Input
        {
            Play,
            Stop,
            Toggle,
            SetEventName,
            SetCueId
        }

        public enum Output
        {
            OnPlayed,
            OnStopped
        }

        public enum PlacementMode
        {
            Point = 0,
            Volumetric = 1
        }

        public const float DefaultAudibleDistance = 10f;
        public const string EventNamePrefix = "Play_amb";

        [field: Header("Audio")]
        [field: SerializeField, Tooltip("Optional game audio event; overrides Event Name. Add-on cues still take priority.")]
        public Vellocet.Stereoform.AudioEventReference AudioEvent { get; private set; }
        [field: SerializeField] public bool Loop { get; private set; } = true;

        [field: Tooltip("Named game audio event posted by this ambient marker.")]
        [field: AmbientEventName]
        [field: SerializeField]
        public string EventName { get; private set; } = string.Empty;

        [field: Tooltip(
            "Overrides Event Name. Embedded cues include audio; dependency cues use audio from a required add-on.")]
        [field: SerializeField]
        public AddonAudioCue CustomCue { get; private set; }

        [field: Tooltip(
            "Emitter radius in metres; default 10. Use 0 for a point source. The Wwise event controls falloff beyond this radius.")]
        [field: Min(0f)]
        [field: SerializeField]
        public float AudibleDistance { get; private set; } = DefaultAudibleDistance;

        [field: Tooltip(
            "Point plays at the marker. Volumetric uses its rotation and scale as a box: audio follows the nearest surface outside, then stops moving and plays in 2D inside.")]
        [field: SerializeField]
        public PlacementMode Mode { get; private set; } = PlacementMode.Point;

        [field: Tooltip("Play when the entity starts on the client.")]
        [field: SerializeField]
        public bool PlayOnStart { get; private set; } = true;

        [field: Tooltip("Stop when the entity is disabled or destroyed.")]
        [field: SerializeField]
        public bool StopOnDisable { get; private set; } = true;

        public bool IsVolumetric => Mode == PlacementMode.Volumetric;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            AudibleDistance = float.IsFinite(AudibleDistance)
                ? Mathf.Max(0f, AudibleDistance)
                : DefaultAudibleDistance;
        }
#endif

        public bool TryGetToolVolumeVisual(out ToolVolumeVisualData data)
        {
            if (!IsVolumetric || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                data = default;
                return false;
            }

            var size = GetVolumetricSize();
            var center = new Vector3(0f, size.y * 0.5f, 0f);
            var fill = new Color(0.15f, 0.95f, 0.75f, 0.12f);
            var wire = new Color(0.15f, 0.95f, 0.75f, 0.9f);
            data = new ToolVolumeVisualData(Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one),
                center, size, fill, wire);
            return true;
        }

        /// <summary>
        ///     Returns the world-space size of the authored volumetric box. The marker position is the
        ///     bottom-center of the box, matching every other SDK brush volume.
        /// </summary>
        public Vector3 GetVolumetricSize()
        {
            var scale = transform.lossyScale;
            return new Vector3(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z));
        }

        public Vector3 GetVolumetricCenter()
        {
            var size = GetVolumetricSize();
            return transform.position + transform.rotation * new Vector3(0f, size.y * 0.5f, 0f);
        }

        /// <summary>
        ///     Tests whether a world-space point is inside the authored oriented box. A non-negative tolerance
        ///     expands the box and is intended for stateful boundary hysteresis at runtime.
        /// </summary>
        public bool ContainsVolumetricPoint(Vector3 worldPosition, float tolerance = 0f)
        {
            var size = GetVolumetricSize();
            var localPosition = Quaternion.Inverse(transform.rotation) * (worldPosition - transform.position);
            tolerance = float.IsFinite(tolerance) ? Mathf.Max(0f, tolerance) : 0f;
            return ContainsVolumetricLocalPoint(localPosition, size, tolerance);
        }

        /// <summary>
        ///     Resolves the closest point on the authored oriented-box boundary.
        /// </summary>
        public Vector3 ResolveVolumetricBoundaryPosition(Vector3 worldPosition)
        {
            var size = GetVolumetricSize();
            var inverseRotation = Quaternion.Inverse(transform.rotation);
            var localPosition = inverseRotation * (worldPosition - transform.position);
            var halfX = size.x * 0.5f;
            var halfZ = size.z * 0.5f;

            var closestLocal = new Vector3(
                Mathf.Clamp(localPosition.x, -halfX, halfX),
                Mathf.Clamp(localPosition.y, 0f, size.y),
                Mathf.Clamp(localPosition.z, -halfZ, halfZ));

            if (ContainsVolumetricLocalPoint(localPosition, size, 0f))
            {
                var closestDistance = localPosition.x + halfX;
                closestLocal.x = -halfX;

                var distance = halfX - localPosition.x;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestLocal = localPosition;
                    closestLocal.x = halfX;
                }

                distance = localPosition.y;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestLocal = localPosition;
                    closestLocal.y = 0f;
                }

                distance = size.y - localPosition.y;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestLocal = localPosition;
                    closestLocal.y = size.y;
                }

                distance = localPosition.z + halfZ;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestLocal = localPosition;
                    closestLocal.z = -halfZ;
                }

                if (halfZ - localPosition.z < closestDistance)
                {
                    closestLocal = localPosition;
                    closestLocal.z = halfZ;
                }
            }

            return transform.position + transform.rotation * closestLocal;
        }

        private static bool ContainsVolumetricLocalPoint(Vector3 localPosition, Vector3 size, float tolerance)
        {
            var halfX = size.x * 0.5f + tolerance;
            var halfZ = size.z * 0.5f + tolerance;
            return localPosition.x >= -halfX && localPosition.x <= halfX &&
                   localPosition.y >= -tolerance && localPosition.y <= size.y + tolerance &&
                   localPosition.z >= -halfZ && localPosition.z <= halfZ;
        }

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            foreach (var input in Inputs) builder.AddInput(input, authority: EntitySignalAuthority.Client);
            foreach (var output in Outputs) builder.AddOutput(output, authority: EntitySignalAuthority.Client);
            builder.AddInput("kill", EntitySignalValueType.None, EntitySignalAuthority.Client);
            builder.AddOutput("killed", EntitySignalValueType.None, EntitySignalAuthority.Client);
            builder.AddProperty("event_name", EntitySignalValueType.String, true, false, authority: EntitySignalAuthority.Client);
            builder.AddProperty("cue_id", EntitySignalValueType.String, true, false, authority: EntitySignalAuthority.Client);
            builder.AddProperty("audible_distance", EntitySignalValueType.Float, true, false, authority: EntitySignalAuthority.Client);
        }
    }
}
