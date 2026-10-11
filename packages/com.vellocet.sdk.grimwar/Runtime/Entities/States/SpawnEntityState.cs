// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities.States
{
    public readonly struct SpawnEntityState : IEntityStateSnapshot
    {
        public SpawnEntityState(Team team, bool isEnabled)
        {
            Team = team;
            IsEnabled = isEnabled;
        }

        public string StateType => nameof(SpawnEntityState);
        public Team Team { get; }
        public bool IsEnabled { get; }
    }
}