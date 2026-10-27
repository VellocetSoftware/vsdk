// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.States;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_tracktrain")]
    [EntityAuthoring("Movement", EntityPlacementKind.Volume, "Track Train")]
    public sealed class TrackTrainMarker : MovingBrushEntityMarker<TrackTrainMarker.Input, TrackTrainMarker.Output>, ISplineEntityMarker
    {
        public enum Input
        {
            Start,
            Stop,
            Toggle,
            Reverse,
            Reset,
            Lock,
            Unlock,
            SetSpeed,
            SetProgress
        }

        public enum Output
        {
            OnStarted,
            OnStopped,
            OnReachedStart,
            OnReachedEnd,
            OnLoop,
            OnReversed,
            OnProgress
        }

        public enum PathEndBehavior
        {
            Stop,
            Loop,
            PingPong
        }

        [field: Header("Path")]
        [field: Tooltip(
            "External spline that defines the train track. Keep it outside the track train hierarchy so the path does not move with the train.")]
        [field: SerializeField]
        public SplineContainer SplineContainer { get; private set; }

        [field: Tooltip("Initial position along the spline, using its normalized 0-1 parameter.")]
        [field: SerializeField]
        [field: Range(0f, 1f)]
        public float StartProgress01 { get; private set; }

        [field: Tooltip("Rotate the train to follow the spline tangent and up direction.")]
        [field: SerializeField]
        public bool FollowSplineRotation { get; private set; } = true;

        [field:
            Tooltip(
                "Euler offset applied after the spline rotation. Use this when the authored model faces a different local axis.")]
        [field: SerializeField]
        public Vector3 RotationOffset { get; private set; }

        [field: Header("Movement")]
        [field: Tooltip("World units per second along the spline. Runtime set_speed accepts a signed value.")]
        [field: SerializeField]
        [field: Min(0.01f)]
        public float Speed { get; private set; } = 4f;

        [field: Tooltip("What happens when the train reaches either end of the spline.")]
        [field: SerializeField]
        public PathEndBehavior EndBehavior { get; private set; } = PathEndBehavior.Stop;

        [field: Header("Collision Damage")]
        [field: Tooltip(
            "Damage dealt when the moving train drives into a damageable target. Separating and tangential contacts do not deal damage. 0 disables it.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float Damage { get; private set; }

        [field: Tooltip("Minimum seconds between collision-damage events against the same target.")]
        [field: SerializeField]
        [field: Min(0.01f)]
        public float DamageCooldownSeconds { get; private set; } = 0.25f;

        [field: Header("Start State")]
        [field: Tooltip("Start moving as soon as the entity initializes.")]
        [field: SerializeField]
        public bool StartActive { get; private set; } = true;

        [field: Tooltip("Start by moving toward spline progress 0 instead of 1.")]
        [field: SerializeField]
        public bool ReverseDirection { get; private set; }

        [field: Tooltip("Ignore movement-control inputs until unlocked. Existing motion is not paused by locking.")]
        [field: SerializeField]
        public bool StartLocked { get; private set; }

        public bool HasExternalSpline => SplineContainer &&
                                         SplineContainer.transform != transform &&
                                         !SplineContainer.transform.IsChildOf(transform);

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("set_speed", EntitySignalValueType.Float);
            builder.AddInput("set_progress", EntitySignalValueType.Float);
            builder.AddOutput("progress", EntitySignalValueType.Float);
            builder.AddProperty("is_active", EntitySignalValueType.Bool, readable: true, writable: false, authority: EntitySignalAuthority.Any);
            builder.AddProperty("is_locked", EntitySignalValueType.Bool, readable: true, writable: false, authority: EntitySignalAuthority.Any);
            builder.AddProperty("is_reversed", EntitySignalValueType.Bool, readable: true, writable: false, authority: EntitySignalAuthority.Any);
            builder.AddProperty("speed", EntitySignalValueType.Float, readable: true, writable: false, authority: EntitySignalAuthority.Any);
            builder.AddProperty("progress", EntitySignalValueType.Float, readable: true, writable: false, authority: EntitySignalAuthority.Any);
            builder.AddState("state", nameof(TrackTrainEntityState));
        }

#if UNITY_EDITOR
        private const int SplineGizmoSegments = 96;
        private const float SplineGizmoWidth = 5f;
        private static readonly Color SplineGizmoColor = new(0.19f, 0.72f, 1f, 0.95f);
        private static readonly HashSet<int> PendingAuthoringSync = new();

        protected override void OnValidate()
        {
            base.OnValidate();
            if (Application.isPlaying)
                return;

            QueueAuthoringSync();
        }

        private void OnDrawGizmos()
        {
            if (!TryEvaluateSplinePosition(SplineContainer, 0f, out var previous))
                return;

            var cachedColor = Handles.color;
            Handles.color = SplineGizmoColor;
            for (var i = 1; i <= SplineGizmoSegments; i++)
            {
                var t = i / (float)SplineGizmoSegments;
                if (!TryEvaluateSplinePosition(SplineContainer, t, out var current))
                    break;

                Handles.DrawAAPolyLine(SplineGizmoWidth, previous, current);
                previous = current;
            }

            Handles.color = cachedColor;
        }

        private void QueueAuthoringSync()
        {
            var instanceId = GetInstanceID();
            if (!PendingAuthoringSync.Add(instanceId))
                return;

            var marker = this;
            EditorApplication.delayCall += () =>
            {
                PendingAuthoringSync.Remove(instanceId);
                if (!marker || Application.isPlaying)
                    return;

                marker.AlignToConfiguredSplinePose();
            };
        }

        private void AlignToConfiguredSplinePose()
        {
            if (!HasExternalSpline ||
                !TryEvaluateSplinePose(
                    SplineContainer,
                    StartProgress01,
                    FollowSplineRotation,
                    RotationOffset,
                    transform.rotation,
                    out var position,
                    out var rotation))
                return;

            var positionChanged = (position - transform.position).sqrMagnitude > 0.000001f;
            var rotationChanged = FollowSplineRotation && Quaternion.Angle(rotation, transform.rotation) > 0.001f;
            if (!positionChanged && !rotationChanged)
                return;

            Undo.RecordObject(transform, "Align Track Train To Spline");
            transform.SetPositionAndRotation(position, rotation);
            EditorUtility.SetDirty(transform);
            EditorUtility.SetDirty(this);
            if (!PrefabUtility.IsPartOfPrefabAsset(this))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
        }

        private static bool TryEvaluateSplinePose(
            SplineContainer spline,
            float progress01,
            bool followRotation,
            Vector3 rotationOffset,
            Quaternion fallbackRotation,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = default;
            rotation = fallbackRotation;
            if (!spline ||
                !spline.Evaluate(
                    Mathf.Clamp01(progress01),
                    out var evaluatedPosition,
                    out var evaluatedTangent,
                    out var evaluatedUp) ||
                !TryConvertToVector3(evaluatedPosition, out position))
                return false;

            if (!followRotation)
                return true;

            if (!TryConvertToVector3(evaluatedTangent, out var forward) ||
                forward.sqrMagnitude <= 0.000001f)
            {
                var t = Mathf.Clamp01(progress01);
                var backT = Mathf.Clamp01(t - 0.002f);
                var frontT = Mathf.Clamp01(t + 0.002f);
                if (frontT <= backT ||
                    !TryEvaluateSplinePosition(spline, backT, out var backPosition) ||
                    !TryEvaluateSplinePosition(spline, frontT, out var frontPosition) ||
                    (frontPosition - backPosition).sqrMagnitude <= 0.000001f)
                    return false;

                forward = frontPosition - backPosition;
            }

            if (!float.IsFinite(rotationOffset.x) ||
                !float.IsFinite(rotationOffset.y) ||
                !float.IsFinite(rotationOffset.z))
                return false;

            var up = TryConvertToVector3(evaluatedUp, out var convertedUp) &&
                     convertedUp.sqrMagnitude > 0.000001f
                ? convertedUp.normalized
                : Vector3.up;
            forward.Normalize();
            if (Vector3.Cross(up, forward).sqrMagnitude <= 0.000001f)
                up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.999f ? Vector3.up : Vector3.right;

            rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(rotationOffset);
            return true;
        }

        private static bool TryEvaluateSplinePosition(SplineContainer spline, float t, out Vector3 position)
        {
            position = default;
            return spline &&
                   spline.Evaluate(Mathf.Clamp01(t), out var evaluatedPosition, out _, out _) &&
                   TryConvertToVector3(evaluatedPosition, out position);
        }

        private static bool TryConvertToVector3(float3 value, out Vector3 vector)
        {
            if (math.any(!math.isfinite(value)))
            {
                vector = default;
                return false;
            }

            vector = value;
            return true;
        }
#endif
    }
}