// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;

namespace Vellocet.Sdk.Spatial
{
    [Serializable]
    public struct SpatialOctreeBakeSettings
    {
        private const int DefaultLayerMask = 1 << 0;

        public LayerMask OccluderMask;
        public QueryTriggerInteraction TriggerInteraction;
        public float MinNodeSize;
        public float MaxOpenNodeSize;
        public float BoundsPadding;
        public int MaxDepth;
        public int RoutingRegionDepth;
        public int MaxPathSearchIterations;
        public int MaxBakedNodeCount;
        public int MaxBakedNeighborEdgeCount;
        public int MaxRoutingRegionCount;

        [Tooltip("Maximum allowed cold-bake duration before map compilation fails.")]
        public int MaxBakeMilliseconds;

        public int MaxEstimatedRuntimeBytes;
        public bool IncludeInactive;

        public static SpatialOctreeBakeSettings Default => new()
        {
            OccluderMask = DefaultLayerMask,
            TriggerInteraction = QueryTriggerInteraction.Ignore,
            MinNodeSize = 1f,
            MaxOpenNodeSize = 10f,
            BoundsPadding = 2f,
            MaxDepth = 8,
            RoutingRegionDepth = 4,
            MaxPathSearchIterations = 65536,
            MaxBakedNodeCount = 500000,
            MaxBakedNeighborEdgeCount = 1500000,
            MaxRoutingRegionCount = 32768,
            MaxBakeMilliseconds = 90000,
            MaxEstimatedRuntimeBytes = 128 * 1024 * 1024,
            IncludeInactive = false
        };

        public SpatialOctreeBakeSettings Sanitize()
        {
            var settings = this;
            if (settings.OccluderMask.value == 0)
                settings.OccluderMask = DefaultLayerMask;
            if (settings.TriggerInteraction != QueryTriggerInteraction.Collide &&
                settings.TriggerInteraction != QueryTriggerInteraction.Ignore &&
                settings.TriggerInteraction != QueryTriggerInteraction.UseGlobal)
                settings.TriggerInteraction = QueryTriggerInteraction.Ignore;

            settings.MinNodeSize = Mathf.Clamp(settings.MinNodeSize, 0.25f, 16f);
            settings.MaxOpenNodeSize = settings.MaxOpenNodeSize <= 0f
                ? Default.MaxOpenNodeSize
                : Mathf.Clamp(settings.MaxOpenNodeSize, settings.MinNodeSize, 64f);
            settings.BoundsPadding = Mathf.Clamp(settings.BoundsPadding, 0f, 64f);
            settings.MaxDepth = Mathf.Clamp(settings.MaxDepth, 1, 14);
            settings.RoutingRegionDepth = settings.RoutingRegionDepth <= 0
                ? Mathf.Min(Default.RoutingRegionDepth, settings.MaxDepth)
                : Mathf.Clamp(settings.RoutingRegionDepth, 1, settings.MaxDepth);
            settings.MaxPathSearchIterations = settings.MaxPathSearchIterations <= 0
                ? Default.MaxPathSearchIterations
                : Mathf.Clamp(settings.MaxPathSearchIterations, 1024, 1000000);
            settings.MaxBakedNodeCount = settings.MaxBakedNodeCount <= 0
                ? Default.MaxBakedNodeCount
                : Mathf.Clamp(settings.MaxBakedNodeCount, 1024, 4000000);
            settings.MaxBakedNeighborEdgeCount = settings.MaxBakedNeighborEdgeCount <= 0
                ? Default.MaxBakedNeighborEdgeCount
                : Mathf.Clamp(settings.MaxBakedNeighborEdgeCount, 1024, 12000000);
            settings.MaxRoutingRegionCount = settings.MaxRoutingRegionCount <= 0
                ? Default.MaxRoutingRegionCount
                : Mathf.Clamp(settings.MaxRoutingRegionCount, 64, 262144);
            settings.MaxBakeMilliseconds = settings.MaxBakeMilliseconds <= 0
                ? Default.MaxBakeMilliseconds
                : Mathf.Clamp(settings.MaxBakeMilliseconds, 1000, 30 * 60 * 1000);
            settings.MaxEstimatedRuntimeBytes = settings.MaxEstimatedRuntimeBytes <= 0
                ? Default.MaxEstimatedRuntimeBytes
                : Mathf.Clamp(settings.MaxEstimatedRuntimeBytes, 8 * 1024 * 1024, 1024 * 1024 * 1024);
            return settings;
        }
    }

    /// <summary>Centralized serialized/runtime memory and count limits for baked spatial graphs.</summary>
    public static class SpatialOctreePerformanceBudget
    {
        private const int SerializedNodeBytes = 56;
        private const int SerializedAcousticNodeBytes = 20;
        private const int NativePathNodeBytes = 64;
        private const int DynamicNodeOverlayBytes = 1;
        private const int CompactManagedSearchBytes = 28;
        private const int CompactNativeSearchBytes = 28;
        private const int RoutingRegionBytes = 40;
        private const int RoutingScratchBytes = 32;

        public static long EstimateRuntimeCoreBytes(int nodeCount, int traversableCount, int neighborEdgeCount,
            int routingRegionCount, int routingNeighborEdgeCount)
        {
            nodeCount = Mathf.Max(0, nodeCount);
            traversableCount = Mathf.Max(0, traversableCount);
            neighborEdgeCount = Mathf.Max(0, neighborEdgeCount);
            routingRegionCount = Mathf.Max(0, routingRegionCount);
            routingNeighborEdgeCount = Mathf.Max(0, routingNeighborEdgeCount);

            var nodes = (long)nodeCount *
                        (SerializedNodeBytes + SerializedAcousticNodeBytes + NativePathNodeBytes +
                         DynamicNodeOverlayBytes + sizeof(int) * 2);
            var traversable = (long)traversableCount *
                              (sizeof(int) * 2 + CompactManagedSearchBytes + CompactNativeSearchBytes);
            var graphEdges = (long)neighborEdgeCount * (sizeof(int) * 2 + sizeof(float) * 2);
            var routing = (long)routingRegionCount *
                          (RoutingRegionBytes + RoutingScratchBytes + sizeof(int) * 2) +
                          (long)routingNeighborEdgeCount * sizeof(int) * 2;
            return nodes + traversable + graphEdges + routing;
        }

        public static bool TryValidate(SpatialOctreeAsset asset, out string message)
        {
            return TryValidate(asset, asset ? asset.BakeSettings : default, out message);
        }

        public static bool TryValidate(SpatialOctreeAsset asset, SpatialOctreeBakeSettings settings,
            out string message)
        {
            if (!asset)
            {
                message = "Spatial octree asset is missing.";
                return false;
            }

            settings = settings.Sanitize();
            var nodeCount = asset.Nodes.Length;
            var edgeCount = asset.NeighborIndices.Length;
            var regionCount = asset.RoutingRegions.Length;
            if (nodeCount > settings.MaxBakedNodeCount)
            {
                message = $"Node budget exceeded: {nodeCount} > {settings.MaxBakedNodeCount}.";
                return false;
            }

            if (edgeCount > settings.MaxBakedNeighborEdgeCount)
            {
                message = $"Neighbor-edge budget exceeded: {edgeCount} > {settings.MaxBakedNeighborEdgeCount}.";
                return false;
            }

            if (regionCount > settings.MaxRoutingRegionCount)
            {
                message = $"Routing-region budget exceeded: {regionCount} > {settings.MaxRoutingRegionCount}.";
                return false;
            }

            var estimatedBytes = EstimateRuntimeCoreBytes(nodeCount, asset.TraversableLeafCount, edgeCount,
                regionCount, asset.RoutingNeighborIndices.Length);
            if (estimatedBytes > settings.MaxEstimatedRuntimeBytes)
            {
                message = $"Estimated runtime core-array budget exceeded: {estimatedBytes} > " +
                          $"{settings.MaxEstimatedRuntimeBytes} bytes.";
                return false;
            }

            message = $"nodes={nodeCount}/{settings.MaxBakedNodeCount}, " +
                      $"edges={edgeCount}/{settings.MaxBakedNeighborEdgeCount}, " +
                      $"regions={regionCount}/{settings.MaxRoutingRegionCount}, " +
                      $"estimatedRuntimeBytes={estimatedBytes}/{settings.MaxEstimatedRuntimeBytes}";
            return true;
        }
    }
}