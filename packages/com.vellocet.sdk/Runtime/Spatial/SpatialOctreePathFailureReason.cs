// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

namespace Vellocet.Sdk.Spatial
{
    public enum SpatialOctreePathFailureReason : byte
    {
        None = 0,
        InvalidGraph = 1,
        InvalidStart = 2,
        InvalidEnd = 3,
        DifferentComponent = 4,
        BudgetExceeded = 5,
        DynamicObstruction = 6,
        Unreachable = 7
    }
}