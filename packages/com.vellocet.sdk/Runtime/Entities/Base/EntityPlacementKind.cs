// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;

namespace Vellocet.Sdk.Entities.Base
{
    [Serializable]
    public enum EntityPlacementKind
    {
        Point = 0,
        Volume = 1,
        Logic = 2
    }
}