// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vellocet.Sdk.Spatial.Acoustics
{
    internal sealed class SpatialOctreeAcousticGraphData
    {
        private readonly float[] _edgeApertures;
        private readonly int[] _neighborIndices;
        private readonly int[] _neighborStarts;
        private readonly SpatialOctreeNodeAcousticData[] _nodes;

        private SpatialOctreeAcousticGraphData(SpatialOctreeNodeAcousticData[] nodes, float[] edgeApertures,
            int[] neighborStarts, int[] neighborIndices, bool baked)
        {
            _nodes = nodes ?? Array.Empty<SpatialOctreeNodeAcousticData>();
            _edgeApertures = edgeApertures ?? Array.Empty<float>();
            _neighborStarts = neighborStarts ?? Array.Empty<int>();
            _neighborIndices = neighborIndices ?? Array.Empty<int>();
            Baked = baked;
        }

        public bool Baked { get; }

        public static SpatialOctreeAcousticGraphData Create(SpatialOctreeAsset asset, SpatialOctreeNodeData[] nodes,
            int[] neighborStarts, int[] neighborIndices)
        {
            if (asset != null && asset.HasAcousticData)
                return new SpatialOctreeAcousticGraphData(asset.AcousticDataArray, asset.NeighborApertureArray,
                    neighborStarts, neighborIndices, true);

            var edgeApertures = SpatialOctreeAcousticMetadata.BuildNeighborApertures(nodes, neighborStarts,
                neighborIndices);
            return new SpatialOctreeAcousticGraphData(
                SpatialOctreeAcousticMetadata.Build(nodes, neighborStarts, neighborIndices, edgeApertures,
                    asset != null ? asset.Bounds : default), edgeApertures, neighborStarts, neighborIndices, false);
        }

        public SpatialOctreeNodeAcousticData GetNodeData(int nodeIndex)
        {
            return (uint)nodeIndex < (uint)_nodes.Length ? _nodes[nodeIndex] : default;
        }

        public bool IsExterior(int nodeIndex)
        {
            return GetNodeData(nodeIndex).IsExterior;
        }

        public bool IsTransition(int nodeIndex)
        {
            return GetNodeData(nodeIndex).IsTransition;
        }

        public SpatialOctreeAcousticSummary GetSummary(SpatialOctreeNodeData[] coreNodes,
            int traversableNodeCount)
        {
            var exterior = 0;
            var transition = 0;
            var thin = 0;
            var opennessSum = 0f;
            var apertureSum = 0f;
            var clusterIds = new HashSet<int>();
            for (var i = 0; i < _nodes.Length && i < coreNodes.Length; i++)
            {
                if (!coreNodes[i].IsTraversable)
                    continue;

                var data = _nodes[i];
                if (data.IsExterior)
                    exterior++;
                if (data.IsTransition)
                    transition++;
                if (data.HasThinBoundary)
                    thin++;
                if (data.ClusterId >= 0)
                    clusterIds.Add(data.ClusterId);

                opennessSum += data.Openness;
                apertureSum += data.Aperture;
            }

            var traversable = Mathf.Max(1, traversableNodeCount);
            return new SpatialOctreeAcousticSummary(
                Baked,
                exterior,
                transition,
                thin,
                clusterIds.Count,
                opennessSum / traversable,
                apertureSum / traversable);
        }

        public int CountExteriorNodes(IReadOnlyList<int> nodePath)
        {
            if (nodePath == null)
                return 0;

            var count = 0;
            foreach (var nodeIndex in nodePath)
                if (IsExterior(nodeIndex))
                    count++;

            return count;
        }

        public float ComputeAverageOpenness(IReadOnlyList<int> nodePath)
        {
            if (nodePath == null)
                return 0f;

            var count = 0;
            var sum = 0f;
            foreach (var nodeIndex in nodePath)
            {
                if ((uint)nodeIndex >= (uint)_nodes.Length)
                    continue;

                sum += _nodes[nodeIndex].Openness;
                count++;
            }

            return count > 0 ? Mathf.Clamp01(sum / count) : 0f;
        }

        public float ComputeMinimumAperture(IReadOnlyList<int> nodePath)
        {
            if (nodePath == null || nodePath.Count <= 1)
                return 1f;

            var minimum = 1f;
            var count = 0;
            for (var pathIndex = 0; pathIndex < nodePath.Count - 1; pathIndex++)
            {
                if (!TryFindEdge(nodePath[pathIndex], nodePath[pathIndex + 1], out var edgeIndex))
                    continue;

                minimum = Mathf.Min(minimum, GetEdgeAperture(edgeIndex));
                count++;
            }

            return count > 0 ? Mathf.Clamp01(minimum) : 0f;
        }

        public float GetEdgeAperture(int edgeIndex)
        {
            return (uint)edgeIndex < (uint)_edgeApertures.Length
                ? Mathf.Clamp01(_edgeApertures[edgeIndex])
                : 0f;
        }

        public bool TryFindEdge(int fromNodeIndex, int toNodeIndex, out int edgeIndex)
        {
            edgeIndex = -1;
            if ((uint)(fromNodeIndex + 1) >= (uint)_neighborStarts.Length)
                return false;

            var start = _neighborStarts[fromNodeIndex];
            var end = _neighborStarts[fromNodeIndex + 1];
            var found = Array.BinarySearch(_neighborIndices, start, end - start, toNodeIndex);
            if (found < 0)
                return false;

            edgeIndex = found;
            return true;
        }

        public int CountTransitionNodes(IReadOnlyList<int> nodePath)
        {
            if (nodePath == null)
                return 0;

            var count = 0;
            foreach (var nodeIndex in nodePath)
                if (IsTransition(nodeIndex))
                    count++;

            return count;
        }
    }

    public readonly struct SpatialOctreeAcousticSummary
    {
        public readonly bool Baked;
        public readonly int ExteriorNodeCount;
        public readonly int TransitionNodeCount;
        public readonly int ThinBoundaryNodeCount;
        public readonly int ClusterCount;
        public readonly float AverageOpenness;
        public readonly float AverageAperture;

        public SpatialOctreeAcousticSummary(bool baked, int exteriorNodeCount, int transitionNodeCount,
            int thinBoundaryNodeCount, int clusterCount, float averageOpenness, float averageAperture)
        {
            Baked = baked;
            ExteriorNodeCount = Mathf.Max(0, exteriorNodeCount);
            TransitionNodeCount = Mathf.Max(0, transitionNodeCount);
            ThinBoundaryNodeCount = Mathf.Max(0, thinBoundaryNodeCount);
            ClusterCount = Mathf.Max(0, clusterCount);
            AverageOpenness = Mathf.Clamp01(averageOpenness);
            AverageAperture = Mathf.Clamp01(averageAperture);
        }
    }
}