// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vellocet.Sdk.Spatial
{
    [Serializable]
    public struct SpatialOctreeRoutingRegionData
    {
        public Vector3 Center;
        public int ComponentId;
        public int NodeCount;

        public SpatialOctreeRoutingRegionData(Vector3 center, int componentId, int nodeCount)
        {
            Center = center;
            ComponentId = componentId;
            NodeCount = Mathf.Max(0, nodeCount);
        }
    }

    /// <summary>Builds the coarse routing graph used to constrain fine octree searches.</summary>
    internal static class SpatialOctreeRoutingMetadata
    {
        internal static void Build(
            SpatialOctreeNodeData[] nodes,
            int[] neighborStarts,
            int[] neighborIndices,
            int targetDepth,
            out int[] traversableNodeIndices,
            out SpatialOctreeRoutingRegionData[] regions,
            out int[] regionNeighborStarts,
            out int[] regionNeighborIndices)
        {
            if (nodes == null || nodes.Length == 0 || neighborStarts == null ||
                neighborStarts.Length != nodes.Length + 1 || neighborIndices == null)
            {
                traversableNodeIndices = Array.Empty<int>();
                regions = Array.Empty<SpatialOctreeRoutingRegionData>();
                regionNeighborStarts = new[] { 0 };
                regionNeighborIndices = Array.Empty<int>();
                return;
            }

            targetDepth = Mathf.Max(0, targetDepth);
            var traversable = new List<int>();
            var ancestorKeys = new int[nodes.Length];
            Array.Fill(ancestorKeys, -1);
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                var node = nodes[nodeIndex];
                node.TraversableIndex = -1;
                node.RoutingRegionId = -1;
                if (node.IsTraversable)
                {
                    node.TraversableIndex = traversable.Count;
                    traversable.Add(nodeIndex);
                    ancestorKeys[nodeIndex] = ResolveAncestor(nodes, nodeIndex, targetDepth);
                }

                nodes[nodeIndex] = node;
            }

            traversableNodeIndices = traversable.ToArray();
            var regionList = new List<SpatialOctreeRoutingRegionData>();
            var queue = new Queue<int>();
            for (var traversableIndex = 0; traversableIndex < traversableNodeIndices.Length; traversableIndex++)
            {
                var seed = traversableNodeIndices[traversableIndex];
                if (nodes[seed].RoutingRegionId >= 0)
                    continue;

                var regionId = regionList.Count;
                var ancestorKey = ancestorKeys[seed];
                var componentId = nodes[seed].ComponentId;
                var centerSum = Vector3.zero;
                var nodeCount = 0;
                AssignRegion(nodes, seed, regionId);
                queue.Enqueue(seed);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    centerSum += nodes[current].Bounds.center;
                    nodeCount++;
                    var start = neighborStarts[current];
                    var end = neighborStarts[current + 1];
                    for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                    {
                        var neighbor = neighborIndices[edgeIndex];
                        if ((uint)neighbor >= (uint)nodes.Length ||
                            !nodes[neighbor].IsTraversable ||
                            nodes[neighbor].RoutingRegionId >= 0 ||
                            nodes[neighbor].ComponentId != componentId ||
                            ancestorKeys[neighbor] != ancestorKey)
                            continue;

                        AssignRegion(nodes, neighbor, regionId);
                        queue.Enqueue(neighbor);
                    }
                }

                regionList.Add(new SpatialOctreeRoutingRegionData(
                    nodeCount > 0 ? centerSum / nodeCount : nodes[seed].Bounds.center,
                    componentId,
                    nodeCount));
            }

            regions = regionList.ToArray();
            BuildRegionAdjacency(nodes, traversableNodeIndices, neighborStarts, neighborIndices, regions.Length,
                out regionNeighborStarts, out regionNeighborIndices);
        }

        private static int ResolveAncestor(SpatialOctreeNodeData[] nodes, int nodeIndex, int targetDepth)
        {
            var current = nodeIndex;
            while ((uint)current < (uint)nodes.Length && nodes[current].Depth > targetDepth)
            {
                var parent = nodes[current].ParentIndex;
                if ((uint)parent >= (uint)nodes.Length)
                    break;
                current = parent;
            }

            return current;
        }

        private static void AssignRegion(SpatialOctreeNodeData[] nodes, int nodeIndex, int regionId)
        {
            var node = nodes[nodeIndex];
            node.RoutingRegionId = regionId;
            nodes[nodeIndex] = node;
        }

        private static void BuildRegionAdjacency(
            SpatialOctreeNodeData[] nodes,
            int[] traversableNodeIndices,
            int[] neighborStarts,
            int[] neighborIndices,
            int regionCount,
            out int[] starts,
            out int[] indices)
        {
            var edgeSet = new HashSet<ulong>();
            for (var traversableIndex = 0; traversableIndex < traversableNodeIndices.Length; traversableIndex++)
            {
                var nodeIndex = traversableNodeIndices[traversableIndex];
                var fromRegion = nodes[nodeIndex].RoutingRegionId;
                var start = neighborStarts[nodeIndex];
                var end = neighborStarts[nodeIndex + 1];
                for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    var neighbor = neighborIndices[edgeIndex];
                    if ((uint)neighbor >= (uint)nodes.Length)
                        continue;

                    var toRegion = nodes[neighbor].RoutingRegionId;
                    if (fromRegion < 0 || toRegion < 0 || fromRegion == toRegion)
                        continue;

                    var min = Mathf.Min(fromRegion, toRegion);
                    var max = Mathf.Max(fromRegion, toRegion);
                    edgeSet.Add(((ulong)(uint)min << 32) | (uint)max);
                }
            }

            var edges = new List<ulong>(edgeSet);
            edges.Sort();
            var degrees = new int[regionCount];
            for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                var edge = edges[edgeIndex];
                degrees[(int)(edge >> 32)]++;
                degrees[(int)(edge & uint.MaxValue)]++;
            }

            starts = new int[regionCount + 1];
            for (var regionIndex = 0; regionIndex < regionCount; regionIndex++)
                starts[regionIndex + 1] = starts[regionIndex] + degrees[regionIndex];

            indices = new int[starts[^1]];
            var cursors = new int[regionCount];
            Array.Copy(starts, cursors, regionCount);
            for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                var edge = edges[edgeIndex];
                var a = (int)(edge >> 32);
                var b = (int)(edge & uint.MaxValue);
                indices[cursors[a]++] = b;
                indices[cursors[b]++] = a;
            }

            for (var regionIndex = 0; regionIndex < regionCount; regionIndex++)
                Array.Sort(indices, starts[regionIndex], starts[regionIndex + 1] - starts[regionIndex]);
        }
    }
}