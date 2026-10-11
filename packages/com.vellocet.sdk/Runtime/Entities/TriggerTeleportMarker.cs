// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("trigger_teleport")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Volume, "Trigger Teleport")]
    public sealed partial class TriggerTeleportMarker :
        TriggerVolumeMarkerBase<TriggerTeleportMarker.Input, TriggerTeleportMarker.Output>
    {
        public enum Input
        {
            Enable,
            Disable,
            Toggle,
            Teleport
        }

        public enum Output
        {
            OnStartTouch,
            OnStartTouchAll,
            OnEndTouch,
            OnEndTouchAll,
            OnTeleport,
            OnEnabled,
            OnDisabled
        }

        [field: Header("Destination")]
        [field: SerializeField]
        public TeleportDestinationMarker Destination { get; private set; }

        [field: Tooltip("Face teleported objects and players along the destination's arrow direction.")]
        [field: SerializeField]
        public bool RotateToDestination { get; private set; } = true;

        [field: Tooltip("Keep current movement velocity through the teleport.")]
        [field: SerializeField]
        public bool PreserveVelocity { get; private set; }

        [field: Tooltip("Rotate preserved velocity from this trigger's facing into the destination facing.")]
        [field: SerializeField]
        public bool RotateVelocity { get; private set; } = true;

        [field: Header("Filter")]
        [field: SerializeField]
        public string RequiredTag { get; private set; } = string.Empty;


        [field: SerializeField]
        public TriggerActivatorMask AllowedActivators { get; private set; } = TriggerActivatorMask.Participants;

        [field: Header("Start State")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

        protected override Color GizmoFillColor => new(0.67f, 0.28f, 1f, 0.14f);
        protected override Color GizmoWireColor => new(0.67f, 0.28f, 1f, 0.92f);

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, true);
        }
    }
}