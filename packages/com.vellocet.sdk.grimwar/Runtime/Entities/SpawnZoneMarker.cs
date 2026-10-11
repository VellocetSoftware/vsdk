// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("trigger_spawn_zone")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Volume, "Spawn Zone")]
    [DisallowMultipleComponent]
    public sealed class SpawnZoneMarker : TriggerVolumeMarkerBase<SpawnZoneMarker.Input, SpawnZoneMarker.Output>
    {
        public enum Input
        {
            Enable,
            Disable,
            Toggle,
            ForceRespawnOccupants
        }

        public enum Output
        {
            OnStartTouch,
            OnEndTouch,
            OnEnabled,
            OnDisabled
        }

        [field: Tooltip("Participants outside these teams are ignored by this spawn zone.")]
        [field: SerializeField]
        public TeamMask AllowedTeams { get; private set; } = TeamMask.All;

        [field: Header("Behavior")]
        [field: Tooltip("Start disabled until explicitly enabled.")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

        protected override Color GizmoFillColor => new(0.31f, 0.92f, 0.41f, 0.14f);
        protected override Color GizmoWireColor => new(0.31f, 0.92f, 0.41f, 0.9f);
    }
}