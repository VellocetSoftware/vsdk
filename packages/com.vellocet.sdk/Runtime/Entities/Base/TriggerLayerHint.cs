// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;

namespace Vellocet.Sdk.Entities.Base
{
    public enum TriggerLayerHintMode
    {
        AutoFromActivators = 0,
        SdkLayerSet = 1,
        All = 2
    }

    [Flags]
    public enum SdkLayerSet
    {
        None = 0,
        Pawn = 1 << 0,
        Hitbox = 1 << 1,
        Item = 1 << 2,
        Default = 1 << 3,
        Shield = 1 << 4,
        FirstPerson = 1 << 5,
        Trigger = 1 << 6,
        Ragdoll = 1 << 7,
        Participants = Pawn | Hitbox
    }
}