// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
using Vellocet.Sdk.Spatial.Acoustics;

namespace Vellocet.Sdk.Spatial
{
    [PreferBinarySerialization]
    public sealed class SpatialOctreeAsset : ScriptableObject
    {
        public const int CurrentVersion = 4;

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private string mapId;
        [SerializeField] private string sceneAddress;
        [SerializeField] private string sourceScenePath;
        [SerializeField] private string geometryHash;
        [SerializeField] private SpatialOctreeBakeSettings bakeSettings;
        [SerializeField] private Bounds bounds;
        [SerializeField] private SpatialOctreeNodeData[] nodes = Array.Empty<SpatialOctreeNodeData>();

        [SerializeField] private SpatialOctreeNodeAcousticData[] acousticData =
            Array.Empty<SpatialOctreeNodeAcousticData>();

        [SerializeField] private int[] neighborStarts = Array.Empty<int>();
        [SerializeField] private int[] neighborIndices = Array.Empty<int>();
        [SerializeField] private float[] neighborApertures = Array.Empty<float>();
        [SerializeField] private int[] traversableNodeIndices = Array.Empty<int>();

        [SerializeField] private SpatialOctreeRoutingRegionData[] routingRegions =
            Array.Empty<SpatialOctreeRoutingRegionData>();

        [SerializeField] private int[] routingNeighborStarts = Array.Empty<int>();
        [SerializeField] private int[] routingNeighborIndices = Array.Empty<int>();
        [SerializeField] private int traversableLeafCount;

        public int Version => version;
        public string MapId => mapId;
        public string SceneAddress => sceneAddress;
        public string SourceScenePath => sourceScenePath;
        public string GeometryHash => geometryHash;
        public SpatialOctreeBakeSettings BakeSettings => bakeSettings;
        public Bounds Bounds => bounds;
        public ReadOnlySpan<SpatialOctreeNodeData> Nodes => nodes;
        public ReadOnlySpan<SpatialOctreeNodeAcousticData> AcousticData => acousticData;
        public ReadOnlySpan<int> NeighborStarts => neighborStarts;
        public ReadOnlySpan<int> NeighborIndices => neighborIndices;
        public ReadOnlySpan<float> NeighborApertures => neighborApertures;
        public ReadOnlySpan<int> TraversableNodeIndices => traversableNodeIndices;
        public ReadOnlySpan<SpatialOctreeRoutingRegionData> RoutingRegions => routingRegions;
        public ReadOnlySpan<int> RoutingNeighborStarts => routingNeighborStarts;
        public ReadOnlySpan<int> RoutingNeighborIndices => routingNeighborIndices;
        public int TraversableLeafCount => traversableLeafCount;
        public bool HasAcousticData => acousticData != null && nodes != null && acousticData.Length == nodes.Length;

        internal SpatialOctreeNodeData[] NodeArray => nodes;
        internal SpatialOctreeNodeAcousticData[] AcousticDataArray => acousticData;
        internal int[] NeighborStartArray => neighborStarts;
        internal int[] NeighborIndexArray => neighborIndices;
        internal float[] NeighborApertureArray => neighborApertures;
        internal int[] TraversableNodeIndexArray => traversableNodeIndices;
        internal SpatialOctreeRoutingRegionData[] RoutingRegionArray => routingRegions;
        internal int[] RoutingNeighborStartArray => routingNeighborStarts;
        internal int[] RoutingNeighborIndexArray => routingNeighborIndices;

        public bool IsUsable => version == CurrentVersion && nodes is { Length: > 0 } &&
                                neighborStarts is { Length: > 1 } &&
                                neighborStarts.Length == nodes.Length + 1 &&
                                neighborIndices != null &&
                                neighborApertures != null &&
                                neighborApertures.Length == neighborIndices.Length &&
                                neighborStarts[0] == 0 &&
                                neighborStarts[^1] == neighborIndices.Length &&
                                traversableNodeIndices != null &&
                                traversableNodeIndices.Length == traversableLeafCount &&
                                routingRegions != null &&
                                routingNeighborStarts != null &&
                                routingNeighborStarts.Length == routingRegions.Length + 1 &&
                                routingNeighborIndices != null &&
                                routingNeighborStarts[0] == 0 &&
                                routingNeighborStarts[^1] == routingNeighborIndices.Length;

        public bool TryValidateData(out string message)
        {
            if (!IsUsable)
            {
                message = "Serialized array layout is incomplete or inconsistent.";
                return false;
            }

            if (!HasAcousticData)
            {
                message = "Per-node acoustic metadata is missing.";
                return false;
            }

            if (traversableLeafCount <= 0)
            {
                message = "Spatial graph has no traversable leaves.";
                return false;
            }

            var actualTraversableCount = 0;
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                var node = nodes[nodeIndex];
                if (node.IsTraversable)
                {
                    actualTraversableCount++;
                    if (node.ComponentId < 0)
                    {
                        message = $"Traversable node {nodeIndex} has no connected component.";
                        return false;
                    }
                }
                else if (node.TraversableIndex >= 0 || node.RoutingRegionId >= 0)
                {
                    message = $"Non-traversable node {nodeIndex} has routing metadata.";
                    return false;
                }

                var start = neighborStarts[nodeIndex];
                var end = neighborStarts[nodeIndex + 1];
                if (start < 0 || end < start || end > neighborIndices.Length)
                {
                    message = $"Neighbor range for node {nodeIndex} is invalid ({start}..{end}).";
                    return false;
                }

                if (!node.IsTraversable && start != end)
                {
                    message = $"Non-traversable node {nodeIndex} has neighbor edges.";
                    return false;
                }

                var previousNeighbor = -1;
                for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    var neighbor = neighborIndices[edgeIndex];
                    if ((uint)neighbor >= (uint)nodes.Length || neighbor == nodeIndex ||
                        !nodes[neighbor].IsTraversable)
                    {
                        message = $"Neighbor edge {edgeIndex} references invalid node {neighbor}.";
                        return false;
                    }

                    if (nodes[neighbor].ComponentId != node.ComponentId)
                    {
                        message = $"Neighbor edge {nodeIndex}->{neighbor} crosses connected components.";
                        return false;
                    }

                    if (neighbor <= previousNeighbor)
                    {
                        message = $"Neighbor range for node {nodeIndex} is not strictly sorted.";
                        return false;
                    }

                    var aperture = neighborApertures[edgeIndex];
                    if (float.IsNaN(aperture) || float.IsInfinity(aperture) || aperture < 0f || aperture > 1f)
                    {
                        message = $"Neighbor edge {edgeIndex} has invalid aperture {aperture}.";
                        return false;
                    }

                    previousNeighbor = neighbor;
                }
            }

            if (actualTraversableCount != traversableLeafCount)
            {
                message = $"Traversable count mismatch: metadata={traversableLeafCount}, " +
                          $"actual={actualTraversableCount}.";
                return false;
            }

            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                var start = neighborStarts[nodeIndex];
                var end = neighborStarts[nodeIndex + 1];
                for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    var neighbor = neighborIndices[edgeIndex];
                    var reciprocal = Array.BinarySearch(neighborIndices, neighborStarts[neighbor],
                        neighborStarts[neighbor + 1] - neighborStarts[neighbor], nodeIndex);
                    if (reciprocal < 0)
                    {
                        message = $"Neighbor edge {nodeIndex}->{neighbor} has no reciprocal edge.";
                        return false;
                    }

                    if (Mathf.Abs(neighborApertures[edgeIndex] - neighborApertures[reciprocal]) > 0.0001f)
                    {
                        message = $"Neighbor edge {nodeIndex}<->{neighbor} has asymmetric aperture metadata.";
                        return false;
                    }
                }
            }

            var regionNodeCounts = new int[routingRegions.Length];
            for (var traversableIndex = 0; traversableIndex < traversableNodeIndices.Length; traversableIndex++)
            {
                var nodeIndex = traversableNodeIndices[traversableIndex];
                if ((uint)nodeIndex >= (uint)nodes.Length || !nodes[nodeIndex].IsTraversable ||
                    nodes[nodeIndex].TraversableIndex != traversableIndex)
                {
                    message = $"Traversable index {traversableIndex} does not map to a valid traversable node.";
                    return false;
                }

                var regionId = nodes[nodeIndex].RoutingRegionId;
                if ((uint)regionId >= (uint)routingRegions.Length)
                {
                    message = $"Traversable node {nodeIndex} references invalid routing region {regionId}.";
                    return false;
                }

                regionNodeCounts[regionId]++;
            }

            for (var regionIndex = 0; regionIndex < routingRegions.Length; regionIndex++)
            {
                if (routingRegions[regionIndex].NodeCount != regionNodeCounts[regionIndex])
                {
                    message = $"Routing region {regionIndex} node count is inconsistent.";
                    return false;
                }

                var start = routingNeighborStarts[regionIndex];
                var end = routingNeighborStarts[regionIndex + 1];
                if (start < 0 || end < start || end > routingNeighborIndices.Length)
                {
                    message = $"Routing neighbor range for region {regionIndex} is invalid ({start}..{end}).";
                    return false;
                }

                var previousNeighbor = -1;
                for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    var neighbor = routingNeighborIndices[edgeIndex];
                    if ((uint)neighbor >= (uint)routingRegions.Length || neighbor == regionIndex ||
                        neighbor <= previousNeighbor ||
                        routingRegions[neighbor].ComponentId != routingRegions[regionIndex].ComponentId)
                    {
                        message = $"Routing edge {edgeIndex} references invalid region {neighbor}.";
                        return false;
                    }


                    previousNeighbor = neighbor;
                }
            }

            for (var regionIndex = 0; regionIndex < routingRegions.Length; regionIndex++)
            {
                var start = routingNeighborStarts[regionIndex];
                var end = routingNeighborStarts[regionIndex + 1];
                for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    var neighbor = routingNeighborIndices[edgeIndex];
                    var reciprocal = Array.BinarySearch(routingNeighborIndices, routingNeighborStarts[neighbor],
                        routingNeighborStarts[neighbor + 1] - routingNeighborStarts[neighbor], regionIndex);
                    if (reciprocal < 0)
                    {
                        message = $"Routing edge {regionIndex}->{neighbor} has no reciprocal edge.";
                        return false;
                    }
                }
            }

            message = $"nodes={nodes.Length}, traversable={traversableNodeIndices.Length}, " +
                      $"edges={neighborIndices.Length}, regions={routingRegions.Length}";
            return true;
        }

        public void SetBakedData(
            string mapId,
            string sceneAddress,
            string sourceScenePath,
            string geometryHash,
            SpatialOctreeBakeSettings bakeSettings,
            Bounds bounds,
            SpatialOctreeNodeData[] nodes,
            int[] neighborStarts,
            int[] neighborIndices,
            float[] neighborApertures,
            int[] traversableNodeIndices,
            SpatialOctreeRoutingRegionData[] routingRegions,
            int[] routingNeighborStarts,
            int[] routingNeighborIndices,
            int traversableLeafCount,
            SpatialOctreeNodeAcousticData[] acousticData = null)
        {
            version = CurrentVersion;
            this.mapId = mapId ?? string.Empty;
            this.sceneAddress = sceneAddress ?? string.Empty;
            this.sourceScenePath = sourceScenePath ?? string.Empty;
            this.geometryHash = geometryHash ?? string.Empty;
            this.bakeSettings = bakeSettings.Sanitize();
            this.bounds = bounds;
            this.nodes = nodes ?? Array.Empty<SpatialOctreeNodeData>();
            this.acousticData = acousticData != null && acousticData.Length == this.nodes.Length
                ? acousticData
                : Array.Empty<SpatialOctreeNodeAcousticData>();
            this.neighborStarts = neighborStarts ?? Array.Empty<int>();
            this.neighborIndices = neighborIndices ?? Array.Empty<int>();
            this.neighborApertures = neighborApertures != null &&
                                     neighborApertures.Length == this.neighborIndices.Length
                ? neighborApertures
                : Array.Empty<float>();
            this.traversableNodeIndices = traversableNodeIndices ?? Array.Empty<int>();
            this.routingRegions = routingRegions ?? Array.Empty<SpatialOctreeRoutingRegionData>();
            this.routingNeighborStarts = routingNeighborStarts ?? Array.Empty<int>();
            this.routingNeighborIndices = routingNeighborIndices ?? Array.Empty<int>();
            this.traversableLeafCount = Mathf.Max(0, traversableLeafCount);
        }

        internal bool UpdateBakeSettings(SpatialOctreeBakeSettings value)
        {
            value = value.Sanitize();
            if (bakeSettings.Equals(value))
                return false;

            bakeSettings = value;
            return true;
        }
    }
}