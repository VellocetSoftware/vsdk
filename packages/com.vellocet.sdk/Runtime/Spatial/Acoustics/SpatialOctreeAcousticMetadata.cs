// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vellocet.Sdk.Spatial.Acoustics
{
    public static class SpatialOctreeAcousticMetadata
    {
        private const float ClusterPortalThreshold = 0.42f;
        private const float WidePortalThreshold = 0.62f;
        private const float ThinBoundaryNodeSize = 1.35f;
        private const float ReferencePortalSpan = 2f;
        private const float ReferenceMinimumPortalSpan = 1.2f;
        private const float BoundsEpsilon = 0.01f;

        public static float[] BuildNeighborApertures(
            SpatialOctreeNodeData[] nodes,
            int[] neighborStarts,
            int[] neighborIndices)
        {
            var apertures = new float[neighborIndices?.Length ?? 0];
            if (nodes == null || neighborStarts == null || neighborIndices == null ||
                neighborStarts.Length != nodes.Length + 1)
                return apertures;

            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                var start = neighborStarts[nodeIndex];
                var end = neighborStarts[nodeIndex + 1];
                for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                    apertures[edgeIndex] = ComputePortalAperture(nodes, nodeIndex, neighborIndices[edgeIndex]);
            }

            return apertures;
        }

        public static SpatialOctreeNodeAcousticData[] Build(
            SpatialOctreeNodeData[] nodes,
            int[] neighborStarts,
            int[] neighborIndices,
            float[] neighborApertures,
            Bounds graphBounds)
        {
            var metadata = new SpatialOctreeNodeAcousticData[nodes?.Length ?? 0];
            if (nodes == null || nodes.Length == 0 || neighborStarts == null || neighborIndices == null ||
                neighborApertures == null || neighborApertures.Length != neighborIndices.Length)
                return metadata;

            var exterior = BuildExteriorFlags(nodes, neighborStarts, neighborIndices, graphBounds);
            var clusters = BuildClusters(nodes, neighborStarts, neighborIndices, neighborApertures, exterior);
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
                metadata[nodeIndex] = BuildNodeMetadata(nodes, neighborStarts, neighborIndices,
                    neighborApertures, exterior, clusters, nodeIndex);

            return metadata;
        }

        private static SpatialOctreeNodeAcousticData BuildNodeMetadata(
            SpatialOctreeNodeData[] nodes,
            int[] neighborStarts,
            int[] neighborIndices,
            float[] neighborApertures,
            bool[] exterior,
            int[] clusters,
            int nodeIndex)
        {
            if ((uint)nodeIndex >= (uint)nodes.Length || !nodes[nodeIndex].IsTraversable)
                return new SpatialOctreeNodeAcousticData(0, -1, 0f, 0f, 1f);

            var exteriorNeighborCount = 0;
            var apertureSum = 0f;
            var widePortalCount = 0;
            var neighborCount = 0;
            var openFaceMask = 0;
            var start = neighborStarts[nodeIndex];
            var end = neighborStarts[nodeIndex + 1];
            for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                var neighbor = neighborIndices[edgeIndex];
                if ((uint)neighbor >= (uint)nodes.Length || !nodes[neighbor].IsTraversable)
                    continue;

                neighborCount++;
                openFaceMask |= ResolveFaceBit(nodes[nodeIndex].Bounds, nodes[neighbor].Bounds);
                if (exterior[neighbor] != exterior[nodeIndex])
                    exteriorNeighborCount++;

                var aperture = Mathf.Clamp01(neighborApertures[edgeIndex]);
                apertureSum += aperture;
                if (aperture >= WidePortalThreshold)
                    widePortalCount++;
            }

            var openFaceCount = CountBits(openFaceMask);
            var faceCoverage = openFaceCount / 6f;
            var averageAperture = neighborCount > 0 ? apertureSum / neighborCount : 0f;
            var widePortalRatio = neighborCount > 0 ? widePortalCount / (float)neighborCount : 0f;
            var closedFaceRatio = Mathf.Clamp01(1f - faceCoverage);
            var openness = exterior[nodeIndex]
                ? 1f
                : Mathf.Clamp01(averageAperture * 0.55f + faceCoverage * 0.35f + widePortalRatio * 0.1f);

            var flags = exterior[nodeIndex] ? SpatialOctreeNodeAcousticData.ExteriorFlag : 0;
            if (exteriorNeighborCount > 0)
                flags |= SpatialOctreeNodeAcousticData.TransitionFlag;
            if (closedFaceRatio >= 0.35f && LargestAxis(nodes[nodeIndex].Bounds.size) <= ThinBoundaryNodeSize)
                flags |= SpatialOctreeNodeAcousticData.ThinBoundaryFlag;

            var clusterId = (uint)nodeIndex < (uint)clusters.Length ? clusters[nodeIndex] : -1;
            return new SpatialOctreeNodeAcousticData(flags, clusterId, openness, averageAperture,
                closedFaceRatio);
        }

        private static bool[] BuildExteriorFlags(
            SpatialOctreeNodeData[] nodes,
            int[] neighborStarts,
            int[] neighborIndices,
            Bounds graphBounds)
        {
            var exterior = new bool[nodes.Length];
            var queue = new Queue<int>();
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                if (!nodes[nodeIndex].IsTraversable ||
                    Mathf.Abs(nodes[nodeIndex].Bounds.max.y - graphBounds.max.y) > BoundsEpsilon)
                    continue;

                exterior[nodeIndex] = true;
                queue.Enqueue(nodeIndex);
            }

            // A node is exterior when it has an unobstructed vertical chain to the top of the bake volume.
            // Descending only through Y-facing portals prevents doors and windows from flooding an entire room.
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var start = neighborStarts[current];
                var end = neighborStarts[current + 1];
                for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    var neighbor = neighborIndices[edgeIndex];
                    if ((uint)neighbor >= (uint)nodes.Length || exterior[neighbor] ||
                        !nodes[neighbor].IsTraversable ||
                        ResolveSharedFaceAxis(nodes[current].Bounds, nodes[neighbor].Bounds) != 1 ||
                        nodes[neighbor].Bounds.center.y >= nodes[current].Bounds.center.y - BoundsEpsilon)
                        continue;

                    exterior[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }

            return exterior;
        }

        private static int[] BuildClusters(
            SpatialOctreeNodeData[] nodes,
            int[] neighborStarts,
            int[] neighborIndices,
            float[] neighborApertures,
            bool[] exterior)
        {
            var clusters = new int[nodes.Length];
            Array.Fill(clusters, -1);
            var clusterId = 0;
            var queue = new Queue<int>();
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                if (!nodes[nodeIndex].IsTraversable || clusters[nodeIndex] >= 0)
                    continue;

                clusters[nodeIndex] = clusterId;
                queue.Enqueue(nodeIndex);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var start = neighborStarts[current];
                    var end = neighborStarts[current + 1];
                    for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                    {
                        var neighbor = neighborIndices[edgeIndex];
                        if ((uint)neighbor >= (uint)nodes.Length || clusters[neighbor] >= 0 ||
                            !nodes[neighbor].IsTraversable || exterior[neighbor] != exterior[current] ||
                            (!exterior[current] && neighborApertures[edgeIndex] < ClusterPortalThreshold))
                            continue;

                        clusters[neighbor] = clusterId;
                        queue.Enqueue(neighbor);
                    }
                }

                clusterId++;
            }

            return clusters;
        }

        internal static float ComputePortalAperture(
            SpatialOctreeNodeData[] nodes,
            int fromNodeIndex,
            int toNodeIndex)
        {
            if (!TryGetPortalBounds(nodes, fromNodeIndex, toNodeIndex, out var portalMin, out var portalMax,
                    out var axis))
                return 0f;

            var portalSize = Vector3.Max(Vector3.zero, portalMax - portalMin);
            var uAxis = (axis + 1) % 3;
            var vAxis = (axis + 2) % 3;
            var u = Mathf.Max(0f, portalSize[uAxis]);
            var v = Mathf.Max(0f, portalSize[vAxis]);
            if (u <= 0.0001f || v <= 0.0001f)
                return 0f;

            var areaSpan = Mathf.Sqrt(u * v) / ReferencePortalSpan;
            var minimumSpan = Mathf.Min(u, v) / ReferenceMinimumPortalSpan;
            return Mathf.Clamp01(Mathf.Min(areaSpan, minimumSpan));
        }

        private static bool TryGetPortalBounds(
            SpatialOctreeNodeData[] nodes,
            int fromNodeIndex,
            int toNodeIndex,
            out Vector3 portalMin,
            out Vector3 portalMax,
            out int axis)
        {
            portalMin = default;
            portalMax = default;
            axis = 0;
            if ((uint)fromNodeIndex >= (uint)nodes.Length || (uint)toNodeIndex >= (uint)nodes.Length)
                return false;

            var fromBounds = nodes[fromNodeIndex].Bounds;
            var toBounds = nodes[toNodeIndex].Bounds;
            portalMin = Vector3.Max(fromBounds.min, toBounds.min);
            portalMax = Vector3.Min(fromBounds.max, toBounds.max);
            axis = ResolveSharedFaceAxis(fromBounds, toBounds);

            const float epsilon = 0.0001f;
            return portalMax.x + epsilon >= portalMin.x &&
                   portalMax.y + epsilon >= portalMin.y &&
                   portalMax.z + epsilon >= portalMin.z;
        }

        internal static int ResolveSharedFaceAxis(Bounds a, Bounds b)
        {
            var dx = Mathf.Min(Mathf.Abs(a.max.x - b.min.x), Mathf.Abs(b.max.x - a.min.x));
            var dy = Mathf.Min(Mathf.Abs(a.max.y - b.min.y), Mathf.Abs(b.max.y - a.min.y));
            var dz = Mathf.Min(Mathf.Abs(a.max.z - b.min.z), Mathf.Abs(b.max.z - a.min.z));
            if (dx <= dy && dx <= dz)
                return 0;
            return dy <= dz ? 1 : 2;
        }

        private static int ResolveFaceBit(Bounds from, Bounds to)
        {
            var axis = ResolveSharedFaceAxis(from, to);
            var positive = to.center[axis] >= from.center[axis];
            return 1 << (axis * 2 + (positive ? 1 : 0));
        }

        private static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }

            return count;
        }

        private static float LargestAxis(Vector3 size)
        {
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }
    }
}