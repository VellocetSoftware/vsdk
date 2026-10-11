// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;

namespace Vellocet.Sdk.Spatial
{
    [Serializable]
    public struct SpatialOctreeNodeData
    {
        public Bounds Bounds;
        public int ParentIndex;
        public int FirstChildIndex;
        public int ChildCount;
        public int Depth;
        public int ComponentId;
        public int TraversableIndex;
        public int RoutingRegionId;
        public bool IsLeaf;
        public bool IsBlocked;

        public bool IsTraversable => IsLeaf && !IsBlocked;
    }
}