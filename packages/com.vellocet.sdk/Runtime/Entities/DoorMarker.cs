// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.States;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_door")]
    [EntityAuthoring("World", EntityPlacementKind.Volume, "Door")]
    public sealed class DoorMarker : MovingBrushEntityMarker<DoorMarker.Input, DoorMarker.Output>
    {
        public enum Input
        {
            Open,
            Close,
            Toggle,
            Lock,
            Unlock
        }

        /// <summary>Local-space directions relative to THIS marker's transform.</summary>
        public enum LocalDirection
        {
            Up,
            Down,
            Left,
            Right,
            Front,
            Back
        }

        public enum Output
        {
            OnOpen,
            OnClose
        }

        [field: Header("Movement")]
        [field: Tooltip("Local direction (relative to this marker) that the door moves when opening.")]
        [field: SerializeField]
        public LocalDirection MoveDirection { get; private set; } = LocalDirection.Front;

        [field: Tooltip("Units per second the door moves between closed and open.")]
        [field: SerializeField]
        [field: Min(0.01f)]
        public float Speed { get; private set; } = 6f;

        [field: Tooltip(
            "Signed travel inset. Positive values leave this much of the door in-frame; negative values move beyond the measured door length.")]
        [field: SerializeField]
        public float Lip { get; private set; }

        [field: Header("Behavior")]
        [field: Tooltip("Automatically close after this many seconds once fully open. -1 = never auto close.")]
        [field: SerializeField]
        [field: Min(-1f)]
        public float ResetDelay { get; private set; } = -1f;

        [field: Header("Collision Damage")]
        [field: Tooltip(
            "Damage dealt when this moving door drives into a damageable target. Separating and tangential contacts do not deal damage. 0 disables collision damage.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float Damage { get; private set; }

        [field: Tooltip("Minimum seconds between collision-damage events against the same target.")]
        [field: SerializeField]
        [field: Min(0.01f)]
        public float DamageCooldownSeconds { get; private set; } = 0.25f;

        [field: Header("Start State")]
        [field: Tooltip("If true, the door starts locked and ignores open/close/toggle until unlocked.")]
        [field: SerializeField]
        public bool StartLocked { get; private set; }

        [field: Tooltip("If true, the door starts open.")]
        [field: SerializeField]
        public bool StartOpen { get; private set; }

        [field: Header("Links")]
        [field: Tooltip("Optional linked doors to act with (future use; routing can handle cross-entity logic).")]
        [field: SerializeField]
        public DoorMarker[] LinkedDoors { get; private set; } = Array.Empty<DoorMarker>();

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Point the arrow in the direction the door moves when opening.
            var pos = transform.position;
            var rot = Quaternion.LookRotation(GetWorldOpenDirection(), Vector3.up);
            var size = HandleUtility.GetHandleSize(pos) * 0.75f;

            using (new Handles.DrawingScope(Color.orange))
            {
                Handles.ArrowHandleCap(0, pos, rot, size, EventType.Repaint);
            }
        }
#endif

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddState("state", nameof(DoorEntityState));
            builder.AddProperty("is_open", EntitySignalValueType.Bool, true, true);
            builder.AddProperty("is_locked", EntitySignalValueType.Bool, true, true);
        }

        /// <summary>
        ///     World-space opening direction from MoveDirection using THIS marker's local axes.
        /// </summary>
        public Vector3 GetWorldOpenDirection()
        {
            var t = transform;
            return MoveDirection switch
            {
                LocalDirection.Up => t.up,
                LocalDirection.Down => -t.up,
                LocalDirection.Left => -t.right,
                LocalDirection.Right => t.right,
                LocalDirection.Front => t.forward,
                LocalDirection.Back => -t.forward,
                _ => Vector3.zero
            };
        }
    }
}