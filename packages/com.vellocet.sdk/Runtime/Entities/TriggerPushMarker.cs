// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities
{
    [EntityClass("trigger_push")]
    [EntityAuthoring("Triggers", EntityPlacementKind.Volume, "Trigger Push")]
    public sealed partial class TriggerPushMarker :
        TriggerVolumeMarkerBase<TriggerPushMarker.Input, TriggerPushMarker.Output>
    {
        public enum Input
        {
            Enable,
            Disable,
            Toggle,
            Push,
            SetSpeed
        }

        public enum Output
        {
            OnStartTouch,
            OnStartTouchAll,
            OnEndTouch,
            OnEndTouchAll,
            OnPush,
            OnEnabled,
            OnDisabled
        }

        public enum PushMode
        {
            Continuous,
            OnEnter
        }

        [field: Header("Filter")]
        [field: Tooltip("Optional tag required on an activating collider.")]
        [field: SerializeField]
        public string RequiredTag { get; private set; } = string.Empty;


        [field: Tooltip("Kinds of collider that may activate this trigger.")]
        [field: SerializeField]
        public TriggerActivatorMask AllowedActivators { get; private set; } = TriggerActivatorMask.Participants;

        [field: Header("Push")]
        [field: Tooltip("Continuous applies acceleration while occupied; On Enter applies one velocity impulse.")]
        [field: SerializeField]
        public PushMode Mode { get; private set; } = PushMode.Continuous;

        [field: Tooltip("Acceleration per second or one-shot velocity impulse along Push Angles.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float Speed { get; private set; } = 12f;

        [field: Tooltip("Local push angles in degrees: X is pitch (-90 = up), Y is yaw (90 = right). " +
                        "Zero follows the marker's forward axis.")]
        [field: SerializeField]
        public Vector2 PushAngles { get; private set; }

        [field: Tooltip("Remove downward velocity before applying an upward push.")]
        [field: SerializeField]
        public bool ClearDownwardVelocity { get; private set; } = true;

        [field: Header("Start State")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

        public Vector3 LocalPushDirection =>
            (Quaternion.Euler(PushAngles.x, PushAngles.y, 0f) * Vector3.forward).normalized;

        public Vector3 WorldDirection => transform.TransformDirection(LocalPushDirection).normalized;

        protected override Color GizmoFillColor => new(0.25f, 0.75f, 1f, 0.14f);
        protected override Color GizmoWireColor => new(0.25f, 0.75f, 1f, 0.92f);

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("set_speed", EntitySignalValueType.Float);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, true);
            builder.AddProperty("speed", EntitySignalValueType.Float, true, true);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var size = GetBoundsXYZ();
            var center = transform.position + transform.rotation * new Vector3(0f, size.y * 0.5f, 0f);
            var direction = WorldDirection;
            var arrowUp = ResolveArrowUp(direction);
            var rotation = Quaternion.LookRotation(direction, arrowUp);
            var handleSize = HandleUtility.GetHandleSize(center) * 0.9f;

            using (new Handles.DrawingScope(GizmoWireColor))
            {
                Handles.ArrowHandleCap(0, center, rotation, handleSize, EventType.Repaint);
            }
        }

        private static Vector3 ResolveArrowUp(Vector3 direction)
        {
            if (Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.98f)
                return Vector3.up;
            if (Mathf.Abs(Vector3.Dot(direction, Vector3.forward)) < 0.98f)
                return Vector3.forward;
            return Vector3.right;
        }
#endif
    }
}