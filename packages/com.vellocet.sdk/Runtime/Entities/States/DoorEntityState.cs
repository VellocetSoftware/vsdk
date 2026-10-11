// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities.States
{
    public readonly struct DoorEntityState : IEntityStateSnapshot
    {
        public DoorEntityState(bool isOpen, bool isLocked)
        {
            IsOpen = isOpen;
            IsLocked = isLocked;
        }

        public string StateType => nameof(DoorEntityState);
        public bool IsOpen { get; }
        public bool IsLocked { get; }
    }
}