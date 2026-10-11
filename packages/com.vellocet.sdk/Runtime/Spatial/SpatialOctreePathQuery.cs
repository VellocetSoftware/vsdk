// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;

namespace Vellocet.Sdk.Spatial
{
    public struct SpatialOctreePathQuery
    {
        public Vector3 From;
        public Vector3 To;

        public SpatialOctreePathQuery(Vector3 from, Vector3 to)
        {
            From = from;
            To = to;
        }
    }
}