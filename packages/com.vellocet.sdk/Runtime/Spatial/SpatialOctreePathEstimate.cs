// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

namespace Vellocet.Sdk.Spatial
{
    public readonly struct SpatialOctreePathEstimate
    {
        public readonly float DirectDistance;
        public readonly float PathLength;
        public readonly float RouteExcessRatio;
        public readonly int WaypointCount;

        public SpatialOctreePathEstimate(float directDistance, float pathLength, float routeExcessRatio,
            int waypointCount)
        {
            DirectDistance = directDistance;
            PathLength = pathLength;
            RouteExcessRatio = routeExcessRatio;
            WaypointCount = waypointCount;
        }
    }
}