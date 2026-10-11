// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;

namespace Vellocet.Sdk.Spatial.Acoustics
{
    internal struct SpatialOctreeAcousticNodeJobData
    {
        public Vector3 Center;
        public Vector3 Extents;
        public int FirstChildIndex;
        public int ChildCount;
        public int ComponentId;
        public int TraversableIndex;
        public int RoutingRegionId;
        public int AcousticFlags;
        public int AcousticClusterId;
        public float AcousticOpenness;
        public float PortalAperture;
        public byte IsLeaf;
        public byte IsTraversable;
        public byte IsExterior;
    }
}