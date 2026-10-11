// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("item_pickup")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Point, "Pickup")]
    public sealed class PickupMarker : EntityMarker<PickupMarker.Input, PickupMarker.Output>
    {
        public enum Input
        {
            Spawn,
            Despawn,
            Respawn
        }

        public enum Output
        {
            OnPickedUp,
            OnSpawned
        }

        public enum PickupKit
        {
            AmmoSmall,
            AmmoMedium,
            AmmoLarge,
            HealthSmall,
            HealthMedium,
            HealthLarge,
            Skull
        }

        [field: Header("Pickup")]
        [field: Tooltip("Pickup kit variant.")]
        [field: SerializeField]
        public PickupKit Kit { get; private set; } = PickupKit.HealthMedium;

        [field: Header("Spawning")]
        [field: Tooltip("Should the pickup spawn automatically when the entity spawns?")]
        [field: SerializeField]
        public bool AutoSpawn { get; private set; } = true;

        public int ResolvePickupValue()
        {
            return Kit switch
            {
                PickupKit.AmmoSmall => 25,
                PickupKit.HealthSmall => 25,
                PickupKit.AmmoMedium => 50,
                PickupKit.HealthMedium => 50,
                PickupKit.AmmoLarge => 100,
                PickupKit.HealthLarge => 100,
                PickupKit.Skull => 1,
                _ => 0
            };
        }
    }
}