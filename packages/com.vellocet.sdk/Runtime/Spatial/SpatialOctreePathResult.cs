// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;

namespace Vellocet.Sdk.Spatial
{
    public struct SpatialOctreePathResult
    {
        public byte Success;
        public int StartNode;
        public int EndNode;
        public byte SameComponent;
        public float DirectDistance;
        public float PathLength;
        public float RouteExcessRatio;
        public int WaypointCount;
        public int ExteriorNodeCount;
        public float AverageOpenness;
        public float MinimumAperture;
        public int TransitionNodeCount;
        public byte StartExterior;
        public byte EndExterior;
        public byte StartTransition;
        public byte EndTransition;
        public SpatialOctreePathFailureReason FailureReason;
        public int SearchIterations;

        public static SpatialOctreePathResult Failed => new()
        {
            Success = 0,
            StartNode = -1,
            EndNode = -1,
            SameComponent = 0,
            DirectDistance = 0f,
            PathLength = 0f,
            RouteExcessRatio = 0f,
            WaypointCount = 0,
            ExteriorNodeCount = 0,
            AverageOpenness = 0f,
            MinimumAperture = 0f,
            TransitionNodeCount = 0,
            StartExterior = 0,
            EndExterior = 0,
            StartTransition = 0,
            EndTransition = 0,
            FailureReason = SpatialOctreePathFailureReason.InvalidGraph,
            SearchIterations = 0
        };

        public static SpatialOctreePathResult CreateFailure(float directDistance, int startNode, int endNode,
            bool sameComponent,
            SpatialOctreePathFailureReason failureReason = SpatialOctreePathFailureReason.Unreachable,
            int searchIterations = 0)
        {
            return new SpatialOctreePathResult
            {
                Success = 0,
                StartNode = startNode,
                EndNode = endNode,
                SameComponent = sameComponent ? (byte)1 : (byte)0,
                DirectDistance = Mathf.Max(0f, directDistance),
                PathLength = 0f,
                RouteExcessRatio = 0f,
                WaypointCount = 0,
                ExteriorNodeCount = 0,
                AverageOpenness = 0f,
                MinimumAperture = 0f,
                TransitionNodeCount = 0,
                StartExterior = 0,
                EndExterior = 0,
                StartTransition = 0,
                EndTransition = 0,
                FailureReason = failureReason,
                SearchIterations = Mathf.Max(0, searchIterations)
            };
        }

        public bool Succeeded => Success != 0;
        public bool IsSameComponent => SameComponent != 0;
        public bool UsesExterior => ExteriorNodeCount > 0;
        public bool EndpointsExterior => StartExterior != 0 && EndExterior != 0;
        public bool UsesTransition => TransitionNodeCount > 0 || StartTransition != 0 || EndTransition != 0;
    }
}