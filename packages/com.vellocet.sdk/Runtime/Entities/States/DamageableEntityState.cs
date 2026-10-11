// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities.States
{
    public readonly struct DamageableEntityState : IEntityStateSnapshot
    {
        public DamageableEntityState(float health, bool isEnabled)
        {
            Health = health;
            IsEnabled = isEnabled;
        }

        public string StateType => nameof(DamageableEntityState);
        public float Health { get; }
        public bool IsEnabled { get; }
    }
}