// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

namespace Vellocet.Sdk.Spatial
{
    public readonly struct StaticWorldVisibilityEstimate
    {
        public readonly bool HasRoute;
        public readonly bool SameComponent;
        public readonly bool LikelyVisible;
        public readonly float DirectDistance;
        public readonly float RouteDistance;
        public readonly float RouteExcessRatio;
        public readonly int WaypointCount;

        public StaticWorldVisibilityEstimate(SpatialOctreePathResult result, bool likelyVisible)
        {
            HasRoute = result.Succeeded;
            SameComponent = result.IsSameComponent;
            LikelyVisible = likelyVisible;
            DirectDistance = result.DirectDistance;
            RouteDistance = result.PathLength;
            RouteExcessRatio = result.RouteExcessRatio;
            WaypointCount = result.WaypointCount;
        }
    }
}