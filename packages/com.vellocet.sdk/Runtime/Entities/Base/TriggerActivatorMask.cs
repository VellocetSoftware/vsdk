// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;

namespace Vellocet.Sdk.Entities.Base
{
    [Flags]
    public enum TriggerActivatorMask
    {
        None = 0,
        Humans = 1 << 0,
        Bots = 1 << 1,
        UnassignedPawns = 1 << 2,
        NonPawns = 1 << 3,
        Participants = Humans | Bots,
        AllPawns = Humans | Bots | UnassignedPawns,
        Everything = Humans | Bots | UnassignedPawns | NonPawns
    }
}