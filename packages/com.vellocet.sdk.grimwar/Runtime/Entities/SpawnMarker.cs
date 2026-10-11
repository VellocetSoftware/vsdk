// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.States;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("info_player_spawn")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Point, "Spawn Point")]
    public sealed class SpawnMarker : DirectionalPointMarker<SpawnMarker.Input, SpawnMarker.Output>
    {
        public enum Input
        {
            Kill = 0,
            Enable = 1,
            Disable = 2,
            Toggle = 3
        }

        public enum Output
        {
            OnSpawn = 0,
            OnEnabled = 1,
            OnDisabled = 2
        }

        [field: Header("Spawn")]
        [field: Tooltip("Team assigned to this spawn point.")]
        [field: SerializeField]
        public Team Team { get; private set; } = Team.Union;

        [field: Tooltip("If true, this spawn point starts disabled until enabled by input.")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddState("state", nameof(SpawnEntityState));
            builder.AddProperty("team", EntitySignalValueType.Enum, true, true, nameof(Team));
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, true);
        }
    }
}