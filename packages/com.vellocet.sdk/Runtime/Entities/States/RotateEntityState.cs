// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities.States
{
    public readonly struct RotateEntityState : IEntityStateSnapshot
    {
        public RotateEntityState(bool isOpen, bool isLocked, float speed, float angle)
        {
            IsOpen = isOpen;
            IsLocked = isLocked;
            Speed = speed;
            Angle = angle;
        }

        public string StateType => nameof(RotateEntityState);
        public bool IsOpen { get; }
        public bool IsLocked { get; }
        public float Speed { get; }
        public float Angle { get; }
    }
}