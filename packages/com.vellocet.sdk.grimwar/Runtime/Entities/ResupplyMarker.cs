// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_resupply")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Point, "Resupply Locker", AvatarModel = "asset:grimwar.base/988418fe095ea444198938b49c0c02aa.5863650252929523565")]
    [DisallowMultipleComponent]
    public sealed class ResupplyMarker : EntityMarker<ResupplyMarker.Input, ResupplyMarker.Output>
    {
        public enum Input
        {
            Enable,
            Disable,
            Toggle
        }

        public enum Output
        {
            OnResupplied,
            OnEnabled,
            OnDisabled
        }

        [field: Header("Resupply")]
        [field: Tooltip("Living players on these teams can refill health, magazines, and reserve ammo. None disables access for all teams.")]
        [field: SerializeField]
        public TeamMask AllowedTeams { get; private set; } = TeamMask.Order | TeamMask.Union;

        [field: Tooltip("Start unavailable until an Enable or Toggle input is received. The locker remains visible.")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }
    }
}
