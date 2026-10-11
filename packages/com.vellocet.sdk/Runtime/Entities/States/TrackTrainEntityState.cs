// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities.States
{
    public readonly struct TrackTrainEntityState : IEntityStateSnapshot
    {
        public TrackTrainEntityState(bool isActive, bool isLocked, bool isReversed, float speed, float progress01)
        {
            IsActive = isActive;
            IsLocked = isLocked;
            IsReversed = isReversed;
            Speed = speed;
            Progress01 = progress01;
        }

        public string StateType => nameof(TrackTrainEntityState);
        public bool IsActive { get; }
        public bool IsLocked { get; }
        public bool IsReversed { get; }
        public float Speed { get; }
        public float Progress01 { get; }
    }
}