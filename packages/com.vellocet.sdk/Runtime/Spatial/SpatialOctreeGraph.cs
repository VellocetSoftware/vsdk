// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Vellocet.Sdk.Spatial.Acoustics;

namespace Vellocet.Sdk.Spatial
{
    public sealed class SpatialOctreeGraph : IDisposable
    {
        public enum TraversablePositionResolveMode
        {
            Failed,
            ExactTraversable,
            LocalProbe,
            NearestLeafFallback
        }

        private const int DirectLineCacheCapacity = 8192;
        private const int DirectObstructionCacheCapacity = 4096;
        private const int DynamicOverlapBufferSize = 64;
        private const int NearestLeafCacheCapacity = 8192;
        private const int RoomEstimateCacheCapacity = 4096;
        private const float ClosestLeafVerticalPenalty = 3.5f;
        private const float ClosestLeafUpwardPenalty = 2f;
        private const float ClosestLeafNodeSizePenalty = 0.0025f;
        private const int HorizontalRoomProbeDirections = 8;
        private const int RoomSampleOriginCount = 3;
        private const float RoomSampleClearanceRadius = 0.12f;
        private const float RoomEstimateCacheResolution = 0.25f;
        private const float DirectObstructionCacheResolution = 0.5f;
        private const float RoomSkyProbeMultiplier = 3f;
        private const float RoomOpenPairSpanScale = 1.25f;
        private const float RoomWidePairSpanScale = 1.65f;
        private const float ObstructionDistanceEpsilon = 0.001f;
        private const float ExteriorPathPenaltyScale = 5f;
        private const float PortalConstrictionPenaltyScale = 6f;
        private const float PortalProbeInsetMin = 0.03f;
        private const float PortalProbeInsetScale = 0.08f;
        private const float PortalSampleOffsetScale = 0.35f;
        private const float EndpointContactClearanceScale = 0.75f;
        private const float EndpointContactClearanceMin = 0.18f;
        private const float EndpointContactClearanceMax = 0.75f;
        private const float EndpointContactClearanceMaxRatio = 0.35f;
        private static readonly float[] RoomSampleHeightOffsets = { 0f, 0.8f, 1.6f };
        private static readonly Vector3[] HorizontalRoomProbeVectors = BuildHorizontalRoomProbeVectors();

        private readonly SpatialOctreeAcousticGraphData _acoustics;
        private readonly SpatialOctreeAsset _asset;

        private readonly int[] _cameFrom;
        private readonly int[] _closedIds;
        private readonly Dictionary<DirectLineCacheKey, bool> _directLineCache = new(DirectLineCacheCapacity);

        private readonly Dictionary<DirectObstructionCacheKey, DirectObstructionEstimate> _directObstructionCache =
            new(DirectObstructionCacheCapacity);

        private readonly List<int> _dynamicBlockedNodeIndices = new();
        private readonly List<Collider> _dynamicOccluders;
        private readonly DynamicOccluderState[] _dynamicOccluderStates;
        private readonly Collider[] _dynamicOverlapBuffer = new Collider[DynamicOverlapBufferSize];
        private readonly List<int> _dynamicTraversalStack = new(128);

        private readonly float[] _fScore;
        private readonly float[] _gScore;
        private readonly int[] _managedHeapIndices;
        private readonly int[] _managedOpen;
        private readonly int[] _managedRegionAllowedIds;
        private readonly int[] _managedRegionCameFrom;
        private readonly int[] _managedRegionQueue;
        private readonly int[] _managedRegionVisitIds;
        private readonly List<int> _nearestNodeTraversalStack = new(128);
        private readonly Dictionary<PositionCacheKey, int> _nearestTraversableLeafCache = new(NearestLeafCacheCapacity);
        private readonly float[] _neighborApertures;
        private readonly int[] _neighborIndices;
        private readonly int[] _neighborStarts;
        private readonly SpatialOctreeNodeData[] _nodes;
        private readonly Dictionary<RoomEstimateCacheKey, float> _roomEstimateCache = new(RoomEstimateCacheCapacity);
        private readonly int[] _routingNeighborIndices;
        private readonly int[] _routingNeighborStarts;
        private readonly SpatialOctreeRoutingRegionData[] _routingRegions;
        private readonly int[] _searchIds;
        private readonly List<SegmentInterval> _segmentIntervals = new(64);
        private readonly SpatialOctreeBakeSettings _settings;
        private readonly int[] _traversableNodeIndices;
        private JobHandle _activePathJobs;
        private bool _disposed;
        private int _lastDynamicRefreshFrame = -1;
        private int _managedRegionSearchId;
        private int _managedSearchId;
        private NativeArray<byte> _nativeDynamicBlockedNodes;
        private NativeArray<float> _nativeNeighborApertures;
        private NativeArray<int> _nativeNeighborIndices;
        private NativeArray<int> _nativeNeighborStarts;
        private NativeArray<SpatialOctreeAcousticNodeJobData> _nativeNodes;
        private NativeArray<int> _nativeRoutingNeighborIndices;
        private NativeArray<int> _nativeRoutingNeighborStarts;
        private NativeArray<SpatialOctreeRoutingRegionData> _nativeRoutingRegions;
        private NativeArray<int> _nativeTraversableNodeIndices;
        private NativeArray<int> _pathJobCameFrom;
        private NativeArray<int> _pathJobClosedIds;
        private NativeArray<float> _pathJobFScores;
        private NativeArray<float> _pathJobGScores;
        private NativeArray<int> _pathJobHeapIndices;
        private NativeArray<int> _pathJobOpen;
        private NativeArray<int> _pathJobRegionAllowedIds;
        private NativeArray<int> _pathJobRegionCameFrom;
        private NativeArray<int> _pathJobRegionQueue;
        private NativeArray<int> _pathJobRegionVisitIds;
        private int _pathJobSearchIdBase;
        private NativeArray<int> _pathJobSearchIds;
        private bool _sharedPathScratchInUse;

        public SpatialOctreeGraph(SpatialOctreeAsset asset) : this(asset, null)
        {
        }

        internal SpatialOctreeGraph(SpatialOctreeAsset asset, List<Collider> dynamicOccluders)
        {
            if (!asset)
                throw new ArgumentNullException(nameof(asset));
            if (!asset.TryValidateData(out var validationMessage))
                throw new ArgumentException($"Spatial octree asset is invalid: {validationMessage}", nameof(asset));

            _asset = asset;
            _settings = asset.BakeSettings.Sanitize();
            _nodes = asset.NodeArray;
            _neighborStarts = asset.NeighborStartArray;
            _neighborIndices = asset.NeighborIndexArray;
            _neighborApertures = asset.NeighborApertureArray;
            _traversableNodeIndices = asset.TraversableNodeIndexArray;
            _routingRegions = asset.RoutingRegionArray;
            _routingNeighborStarts = asset.RoutingNeighborStartArray;
            _routingNeighborIndices = asset.RoutingNeighborIndexArray;
            _dynamicOccluders = dynamicOccluders ?? new List<Collider>();
            _dynamicOccluderStates = new DynamicOccluderState[_dynamicOccluders.Count];
            _acoustics = SpatialOctreeAcousticGraphData.Create(asset, _nodes, _neighborStarts, _neighborIndices);
            var searchLength = Mathf.Max(1, _traversableNodeIndices.Length);
            _gScore = new float[searchLength];
            _fScore = new float[searchLength];
            _cameFrom = new int[searchLength];
            _searchIds = new int[searchLength];
            _closedIds = new int[searchLength];
            _managedOpen = new int[searchLength];
            _managedHeapIndices = new int[searchLength];
            var regionSearchLength = Mathf.Max(1, _routingRegions.Length);
            _managedRegionCameFrom = new int[regionSearchLength];
            _managedRegionVisitIds = new int[regionSearchLength];
            _managedRegionAllowedIds = new int[regionSearchLength];
            _managedRegionQueue = new int[regionSearchLength];
            CreateNativeGraph();
            CreatePathJobScratch();
            RefreshDynamicOccluders(true);
        }

        public bool IsUsable => !_disposed && _asset.IsUsable && _nodes.Length > 0 &&
                                _neighborStarts.Length == _nodes.Length + 1;

        public float MinimumNodeSize => _settings.MinNodeSize;

        public int NodeCount => _nodes.Length;

        public int TraversableNodeCount => _traversableNodeIndices.Length;

        public int NeighborEdgeCount => _neighborIndices.Length;

        public int RoutingRegionCount => _routingRegions.Length;

        public int DynamicOccluderCount => _dynamicOccluders.Count;

        public int DynamicBlockedNodeCount { get; private set; }

        public long EstimatedRuntimeCoreBytes => SpatialOctreePerformanceBudget.EstimateRuntimeCoreBytes(
            _nodes.Length, _traversableNodeIndices.Length, _neighborIndices.Length, _routingRegions.Length,
            _routingNeighborIndices.Length);

        public bool HasBakedAcousticData => _acoustics.Baked;

        public void Dispose()
        {
            if (_disposed)
                return;

            _activePathJobs.Complete();
            if (_nativeNodes.IsCreated)
                _nativeNodes.Dispose();
            if (_nativeNeighborStarts.IsCreated)
                _nativeNeighborStarts.Dispose();
            if (_nativeNeighborIndices.IsCreated)
                _nativeNeighborIndices.Dispose();
            if (_nativeNeighborApertures.IsCreated)
                _nativeNeighborApertures.Dispose();
            if (_nativeTraversableNodeIndices.IsCreated)
                _nativeTraversableNodeIndices.Dispose();
            if (_nativeRoutingRegions.IsCreated)
                _nativeRoutingRegions.Dispose();
            if (_nativeRoutingNeighborStarts.IsCreated)
                _nativeRoutingNeighborStarts.Dispose();
            if (_nativeRoutingNeighborIndices.IsCreated)
                _nativeRoutingNeighborIndices.Dispose();
            if (_nativeDynamicBlockedNodes.IsCreated)
                _nativeDynamicBlockedNodes.Dispose();
            DisposePathJobScratch();
            _disposed = true;
        }

        public SpatialOctreeAcousticSummary GetAcousticMetadataSummary()
        {
            return _acoustics.GetSummary(_nodes, _traversableNodeIndices.Length);
        }

        public SpatialOctreePathEstimateJob SchedulePathEstimates(
            NativeArray<SpatialOctreePathQuery> queries,
            int queryCount,
            Allocator allocator,
            JobHandle dependency = default)
        {
            if (!IsUsable)
                throw new InvalidOperationException("Spatial octree graph is not usable.");
            if (!queries.IsCreated)
                throw new ArgumentException("Query array must be created.", nameof(queries));
            if (queryCount < 0 || queryCount > queries.Length)
                throw new ArgumentOutOfRangeException(nameof(queryCount));

            RefreshDynamicOccluders();
            var results = new NativeArray<SpatialOctreePathResult>(queryCount, allocator,
                NativeArrayOptions.UninitializedMemory);
            var usesSharedScratch = !_sharedPathScratchInUse;
            NativeArray<float> gScores;
            NativeArray<float> fScores;
            NativeArray<int> cameFrom;
            NativeArray<int> searchIds;
            NativeArray<int> closedIds;
            NativeArray<int> open;
            NativeArray<int> heapIndices;
            NativeArray<int> regionCameFrom;
            NativeArray<int> regionVisitIds;
            NativeArray<int> regionAllowedIds;
            NativeArray<int> regionQueue;
            var searchIdBase = 0;

            if (usesSharedScratch)
            {
                _sharedPathScratchInUse = true;
                searchIdBase = ReservePathJobSearchIdBase(queryCount);
                gScores = _pathJobGScores;
                fScores = _pathJobFScores;
                cameFrom = _pathJobCameFrom;
                searchIds = _pathJobSearchIds;
                closedIds = _pathJobClosedIds;
                open = _pathJobOpen;
                heapIndices = _pathJobHeapIndices;
                regionCameFrom = _pathJobRegionCameFrom;
                regionVisitIds = _pathJobRegionVisitIds;
                regionAllowedIds = _pathJobRegionAllowedIds;
                regionQueue = _pathJobRegionQueue;
            }
            else
            {
                var scratchLength = Mathf.Max(1, _nativeTraversableNodeIndices.Length);
                var regionScratchLength = Mathf.Max(1, _nativeRoutingRegions.Length);
                gScores = new NativeArray<float>(scratchLength, allocator, NativeArrayOptions.UninitializedMemory);
                fScores = new NativeArray<float>(scratchLength, allocator, NativeArrayOptions.UninitializedMemory);
                cameFrom = new NativeArray<int>(scratchLength, allocator, NativeArrayOptions.UninitializedMemory);
                searchIds = new NativeArray<int>(scratchLength, allocator);
                closedIds = new NativeArray<int>(scratchLength, allocator);
                open = new NativeArray<int>(scratchLength, allocator, NativeArrayOptions.UninitializedMemory);
                heapIndices = new NativeArray<int>(scratchLength, allocator, NativeArrayOptions.UninitializedMemory);
                regionCameFrom = new NativeArray<int>(regionScratchLength, allocator,
                    NativeArrayOptions.UninitializedMemory);
                regionVisitIds = new NativeArray<int>(regionScratchLength, allocator);
                regionAllowedIds = new NativeArray<int>(regionScratchLength, allocator);
                regionQueue = new NativeArray<int>(regionScratchLength, allocator,
                    NativeArrayOptions.UninitializedMemory);
            }

            var handle = new SpatialOctreeAcousticPathEstimateJob
            {
                Queries = queries,
                Nodes = _nativeNodes,
                TraversableNodeIndices = _nativeTraversableNodeIndices,
                NeighborStarts = _nativeNeighborStarts,
                NeighborIndices = _nativeNeighborIndices,
                NeighborApertures = _nativeNeighborApertures,
                DynamicBlockedNodes = _nativeDynamicBlockedNodes,
                RoutingRegions = _nativeRoutingRegions,
                RoutingNeighborStarts = _nativeRoutingNeighborStarts,
                RoutingNeighborIndices = _nativeRoutingNeighborIndices,
                Results = results,
                GScores = gScores,
                FScores = fScores,
                CameFrom = cameFrom,
                SearchIds = searchIds,
                ClosedIds = closedIds,
                Open = open,
                HeapIndices = heapIndices,
                RegionCameFrom = regionCameFrom,
                RegionVisitIds = regionVisitIds,
                RegionAllowedIds = regionAllowedIds,
                RegionQueue = regionQueue,
                QueryCount = queryCount,
                SearchIdBase = searchIdBase,
                MaxPathSearchIterations = _settings.MaxPathSearchIterations,
                DynamicBlockedCount = DynamicBlockedNodeCount
            }.Schedule(dependency);
            _activePathJobs = JobHandle.CombineDependencies(_activePathJobs, handle);

            return new SpatialOctreePathEstimateJob(handle, results, gScores, fScores, cameFrom, searchIds, closedIds,
                open, heapIndices, regionCameFrom, regionVisitIds, regionAllowedIds, regionQueue,
                usesSharedScratch ? this : null);
        }

        private bool TryFindContainingLeaf(Vector3 position, out int nodeIndex)
        {
            nodeIndex = -1;
            if (!IsUsable || !_asset.Bounds.Contains(position))
                return false;

            var current = 0;
            while (true)
            {
                if ((uint)current >= (uint)_nodes.Length)
                    return false;

                var node = _nodes[current];
                if (node.IsLeaf)
                {
                    nodeIndex = current;
                    return true;
                }

                var firstChild = node.FirstChildIndex;
                if (firstChild < 0)
                    return false;

                var endChild = Mathf.Min(firstChild + node.ChildCount, _nodes.Length);
                var center = node.Bounds.center;
                var preferredChild = firstChild;
                if (position.x >= center.x)
                    preferredChild += 1;
                if (position.y >= center.y)
                    preferredChild += 2;
                if (position.z >= center.z)
                    preferredChild += 4;

                if ((uint)preferredChild < (uint)endChild && _nodes[preferredChild].Bounds.Contains(position))
                {
                    current = preferredChild;
                    continue;
                }

                var foundChild = -1;
                for (var i = firstChild; i < endChild; i++)
                {
                    if (!_nodes[i].Bounds.Contains(position))
                        continue;

                    foundChild = i;
                    break;
                }

                if (foundChild < 0)
                    return false;

                current = foundChild;
            }
        }

        public bool TryFindClosestTraversableLeaf(Vector3 position, out int nodeIndex)
        {
            RefreshDynamicOccluders();
            if (TryFindContainingLeaf(position, out nodeIndex) && IsNodeTraversable(nodeIndex))
                return true;

            var cacheKey = PositionCacheKey.From(position);
            if (_nearestTraversableLeafCache.TryGetValue(cacheKey, out nodeIndex) &&
                (uint)nodeIndex < (uint)_nodes.Length &&
                IsNodeTraversable(nodeIndex))
                return true;

            nodeIndex = -1;
            var bestDistance = float.PositiveInfinity;
            _nearestNodeTraversalStack.Clear();
            _nearestNodeTraversalStack.Add(0);
            while (_nearestNodeTraversalStack.Count > 0)
            {
                var stackIndex = _nearestNodeTraversalStack.Count - 1;
                var candidateIndex = _nearestNodeTraversalStack[stackIndex];
                _nearestNodeTraversalStack.RemoveAt(stackIndex);
                if ((uint)candidateIndex >= (uint)_nodes.Length)
                    continue;

                var node = _nodes[candidateIndex];
                if (ScoreBoundsDistance(node.Bounds, position) >= bestDistance)
                    continue;
                if (!node.IsLeaf)
                {
                    PushNearestSearchChildren(node, position);
                    continue;
                }

                if (!IsNodeTraversable(candidateIndex))
                    continue;

                var score = ScoreTraversableCandidate(node.Bounds, position);

                if (score >= bestDistance)
                    continue;

                bestDistance = score;
                nodeIndex = candidateIndex;
            }

            if (nodeIndex < 0)
                return false;

            CacheNearestTraversableLeaf(cacheKey, nodeIndex);
            return true;
        }

        private void PushNearestSearchChildren(SpatialOctreeNodeData node, Vector3 position)
        {
            var firstChild = node.FirstChildIndex;
            if (firstChild < 0)
                return;

            var endChild = Mathf.Min(firstChild + node.ChildCount, _nodes.Length);
            var preferredChild = firstChild;
            if (position.x >= node.Bounds.center.x)
                preferredChild += 1;
            if (position.y >= node.Bounds.center.y)
                preferredChild += 2;
            if (position.z >= node.Bounds.center.z)
                preferredChild += 4;

            for (var childIndex = firstChild; childIndex < endChild; childIndex++)
                if (childIndex != preferredChild)
                    _nearestNodeTraversalStack.Add(childIndex);
            if ((uint)preferredChild < (uint)endChild)
                _nearestNodeTraversalStack.Add(preferredChild);
        }

        private static float ScoreTraversableCandidate(Bounds bounds, Vector3 position)
        {
            return ScoreBoundsDistance(bounds, position) +
                   bounds.size.sqrMagnitude * ClosestLeafNodeSizePenalty;
        }

        private static float ScoreBoundsDistance(Bounds bounds, Vector3 position)
        {
            var delta = bounds.ClosestPoint(position) - position;
            var horizontalDistanceSqr = delta.x * delta.x + delta.z * delta.z;
            var verticalDistanceSqr = delta.y * delta.y;
            var upwardDistanceSqr = delta.y > 0f ? verticalDistanceSqr : 0f;
            return horizontalDistanceSqr +
                   verticalDistanceSqr * ClosestLeafVerticalPenalty +
                   upwardDistanceSqr * ClosestLeafUpwardPenalty;
        }

        public bool TryGetContainingTraversableLocation(Vector3 position, out int nodeIndex, out int componentId)
        {
            RefreshDynamicOccluders();
            nodeIndex = -1;
            componentId = -1;

            if (!TryFindContainingLeaf(position, out var containingNode))
                return false;

            var node = _nodes[containingNode];
            if (!IsNodeTraversable(containingNode))
                return false;

            nodeIndex = containingNode;
            componentId = node.ComponentId;
            return true;
        }

        public bool TryGetComponentId(Vector3 position, out int componentId)
        {
            componentId = -1;
            if (!TryFindClosestTraversableLeaf(position, out var nodeIndex))
                return false;

            componentId = _nodes[nodeIndex].ComponentId;
            return true;
        }

        public bool TryGetNearestTraversablePosition(Vector3 position, out Vector3 nearestPosition)
        {
            nearestPosition = position;
            if (!TryFindClosestTraversableLeaf(position, out var nodeIndex))
                return false;

            nearestPosition = _nodes[nodeIndex].Bounds.ClosestPoint(position);
            return true;
        }

        public bool TryResolveTraversablePosition(Vector3 position, ReadOnlySpan<Vector3> localProbeOffsets,
            bool allowNearestFallback, out TraversablePositionInfo info)
        {
            RefreshDynamicOccluders();
            info = new TraversablePositionInfo(position, position, -1, -1,
                TraversablePositionResolveMode.Failed);
            if (!IsUsable)
                return false;

            if (TryFindContainingLeaf(position, out var containingNode) && IsNodeTraversable(containingNode))
            {
                var node = _nodes[containingNode];
                info = new TraversablePositionInfo(position, position, containingNode, node.ComponentId,
                    TraversablePositionResolveMode.ExactTraversable);
                return true;
            }

            if (TryResolveLocalTraversablePosition(position, localProbeOffsets, out var localPosition,
                    out var localNode))
            {
                var node = _nodes[localNode];
                info = new TraversablePositionInfo(position, localPosition, localNode, node.ComponentId,
                    TraversablePositionResolveMode.LocalProbe);
                return true;
            }

            if (!allowNearestFallback)
                return false;

            if (!TryFindClosestTraversableLeaf(position, out var nearestNode))
                return false;

            var nearestPosition = _nodes[nearestNode].Bounds.ClosestPoint(position);
            var nearest = _nodes[nearestNode];
            info = new TraversablePositionInfo(position, nearestPosition, nearestNode, nearest.ComponentId,
                TraversablePositionResolveMode.NearestLeafFallback);
            return true;
        }

        public GraphValidationSummary ValidateGraph()
        {
            var leafCount = 0;
            var traversableLeafCount = 0;
            var blockedLeafCount = 0;
            var neighborEdgeCount = 0;
            var invalidNeighborReferences = 0;
            var asymmetricNeighborLinks = 0;
            var blockedNeighborLinks = 0;
            var invalidPortalLinks = 0;
            var minLeafSize = float.PositiveInfinity;
            var maxLeafSize = 0f;
            var leafSizeSum = 0f;
            var components = new HashSet<int>();

            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                if (!node.IsLeaf)
                    continue;

                leafCount++;
                var leafSize = Mathf.Max(node.Bounds.size.x, Mathf.Max(node.Bounds.size.y, node.Bounds.size.z));
                minLeafSize = Mathf.Min(minLeafSize, leafSize);
                maxLeafSize = Mathf.Max(maxLeafSize, leafSize);
                leafSizeSum += leafSize;

                if (node.IsTraversable)
                {
                    traversableLeafCount++;
                    if (node.ComponentId >= 0)
                        components.Add(node.ComponentId);
                }
                else
                {
                    blockedLeafCount++;
                }
            }

            for (var nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex++)
            {
                if ((uint)(nodeIndex + 1) >= (uint)_neighborStarts.Length)
                    break;

                var start = _neighborStarts[nodeIndex];
                var end = _neighborStarts[nodeIndex + 1];
                for (var edge = start; edge < end; edge++)
                {
                    neighborEdgeCount++;
                    if ((uint)edge >= (uint)_neighborIndices.Length)
                    {
                        invalidNeighborReferences++;
                        continue;
                    }

                    var neighborIndex = _neighborIndices[edge];
                    if ((uint)neighborIndex >= (uint)_nodes.Length)
                    {
                        invalidNeighborReferences++;
                        continue;
                    }

                    if (!_nodes[nodeIndex].IsTraversable || !_nodes[neighborIndex].IsTraversable)
                        blockedNeighborLinks++;

                    if (!HasNeighborLink(neighborIndex, nodeIndex))
                        asymmetricNeighborLinks++;

                    if (!HasClearPortal(nodeIndex, neighborIndex))
                        invalidPortalLinks++;
                }
            }

            return new GraphValidationSummary
            {
                NodeCount = _nodes.Length,
                LeafCount = leafCount,
                TraversableLeafCount = traversableLeafCount,
                BlockedLeafCount = blockedLeafCount,
                ComponentCount = components.Count,
                NeighborEdgeCount = neighborEdgeCount,
                InvalidNeighborReferenceCount = invalidNeighborReferences,
                AsymmetricNeighborLinkCount = asymmetricNeighborLinks,
                BlockedNeighborLinkCount = blockedNeighborLinks,
                InvalidPortalLinkCount = invalidPortalLinks,
                MinLeafSize = leafCount > 0 ? minLeafSize : 0f,
                MaxLeafSize = maxLeafSize,
                AverageLeafSize = leafCount > 0 ? leafSizeSum / leafCount : 0f
            };
        }

        public bool TryHasDirectTraversableLine(Vector3 start, Vector3 end, out bool visible)
        {
            RefreshDynamicOccluders();
            visible = false;
            if (!IsUsable)
                return false;

            if (!TryFindContainingLeaf(start, out var startNode) || !IsNodeTraversable(startNode))
                if (!TryFindClosestTraversableLeaf(start, out startNode))
                    return true;
            start = _nodes[startNode].Bounds.ClosestPoint(start);

            if (!TryFindContainingLeaf(end, out var endNode) || !IsNodeTraversable(endNode))
                if (!TryFindClosestTraversableLeaf(end, out endNode))
                    return true;
            end = _nodes[endNode].Bounds.ClosestPoint(end);

            if (startNode == endNode)
            {
                visible = true;
                return true;
            }

            if (!HasSameComponent(startNode, endNode))
            {
                visible = false;
                return true;
            }

            var cacheKey = DirectLineCacheKey.From(startNode, endNode, start, end);
            if (_directLineCache.TryGetValue(cacheKey, out visible))
                return true;

            visible = HasClearLine(start, end, startNode, endNode);
            CacheDirectLineResult(cacheKey, visible);
            return true;
        }

        public DirectObstructionEstimate EstimateDirectObstruction(Vector3 start, Vector3 end)
        {
            RefreshDynamicOccluders();
            var directDistance = Vector3.Distance(start, end);
            if (!IsUsable || directDistance <= 0.001f || _nodes.Length == 0)
                return DirectObstructionEstimate.Empty(directDistance);

            var cacheKey = DirectObstructionCacheKey.From(start, end);
            if (_directObstructionCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var estimate = EstimateExactDirectObstruction(start, end, directDistance);
            CacheDirectObstructionEstimate(cacheKey, estimate);
            return estimate;
        }

        public bool TryFindPath(Vector3 start, Vector3 end, List<Vector3> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();
            if (!TryFindClosestTraversableLeaf(start, out var startNode) ||
                !TryFindClosestTraversableLeaf(end, out var endNode))
                return false;

            if (!HasSameComponent(startNode, endNode))
                return false;

            using var pooledNodePath = SpatialNodePathListPool.Get();
            var nodePath = pooledNodePath.Value;
            if (TryFindPathNodes(startNode, endNode, nodePath, out _) != ManagedPathSearchOutcome.Success)
                return false;

            var startPoint = _nodes[startNode].Bounds.ClosestPoint(start);
            var endPoint = _nodes[endNode].Bounds.ClosestPoint(end);
            BuildRoutedPath(nodePath, startPoint, endPoint, output);

            return output.Count >= 2 && !PathIntersectsBlockedLeaf(output);
        }

        public bool TryEstimateRoute(Vector3 from, Vector3 to, out SpatialOctreePathEstimate estimate)
        {
            estimate = default;
            var result = EstimateRoute(from, to);
            if (!result.Succeeded)
                return false;

            estimate = new SpatialOctreePathEstimate(result.DirectDistance, result.PathLength,
                result.RouteExcessRatio, result.WaypointCount);
            return true;
        }

        public SpatialOctreePathResult EstimateRoute(Vector3 from, Vector3 to)
        {
            var directDistance = Vector3.Distance(from, to);
            if (!TryFindClosestTraversableLeaf(from, out var startNode))
                return SpatialOctreePathResult.CreateFailure(directDistance, -1, -1, false,
                    SpatialOctreePathFailureReason.InvalidStart);

            if (!TryFindClosestTraversableLeaf(to, out var endNode))
                return SpatialOctreePathResult.CreateFailure(directDistance, startNode, -1, false,
                    SpatialOctreePathFailureReason.InvalidEnd);

            var sameComponent = HasSameComponent(startNode, endNode);
            if (!sameComponent)
                return SpatialOctreePathResult.CreateFailure(directDistance, startNode, endNode, false,
                    SpatialOctreePathFailureReason.DifferentComponent);

            var fromPoint = _nodes[startNode].Bounds.ClosestPoint(from);
            var toPoint = _nodes[endNode].Bounds.ClosestPoint(to);
            directDistance = Vector3.Distance(fromPoint, toPoint);

            using var pooledNodePath = SpatialNodePathListPool.Get();
            var nodePath = pooledNodePath.Value;
            var searchOutcome = TryFindPathNodes(startNode, endNode, nodePath, out var searchIterations);
            if (searchOutcome != ManagedPathSearchOutcome.Success)
                return SpatialOctreePathResult.CreateFailure(directDistance, startNode, endNode, sameComponent,
                    searchOutcome == ManagedPathSearchOutcome.BudgetExceeded
                        ? SpatialOctreePathFailureReason.BudgetExceeded
                        : DynamicBlockedNodeCount > 0
                            ? SpatialOctreePathFailureReason.DynamicObstruction
                            : SpatialOctreePathFailureReason.Unreachable,
                    searchIterations);

            using var pooledPath = SpatialPathListPool.Get();
            var path = pooledPath.Value;
            BuildRoutedPath(nodePath, fromPoint, toPoint, path);
            if (PathIntersectsBlockedLeaf(path))
                return SpatialOctreePathResult.CreateFailure(directDistance, startNode, endNode, sameComponent,
                    DynamicBlockedNodeCount > 0
                        ? SpatialOctreePathFailureReason.DynamicObstruction
                        : SpatialOctreePathFailureReason.Unreachable,
                    searchIterations);

            var pathLength = 0f;
            for (var i = 1; i < path.Count; i++)
                pathLength += Vector3.Distance(path[i - 1], path[i]);

            pathLength = Mathf.Max(directDistance, pathLength);
            var traversalPenalty = ComputeNodePathTraversalPenalty(nodePath, startNode, endNode);
            pathLength += traversalPenalty;
            var excess = Mathf.Max(0f, pathLength - directDistance);
            var ratio = directDistance > 0.001f ? Mathf.Clamp01(excess / directDistance) : 0f;
            return new SpatialOctreePathResult
            {
                Success = 1,
                StartNode = startNode,
                EndNode = endNode,
                SameComponent = sameComponent ? (byte)1 : (byte)0,
                DirectDistance = directDistance,
                PathLength = pathLength,
                RouteExcessRatio = ratio,
                WaypointCount = path.Count,
                ExteriorNodeCount = _acoustics.CountExteriorNodes(nodePath),
                AverageOpenness = _acoustics.ComputeAverageOpenness(nodePath),
                MinimumAperture = _acoustics.ComputeMinimumAperture(nodePath),
                TransitionNodeCount = _acoustics.CountTransitionNodes(nodePath),
                StartExterior = _acoustics.IsExterior(startNode) ? (byte)1 : (byte)0,
                EndExterior = _acoustics.IsExterior(endNode) ? (byte)1 : (byte)0,
                StartTransition = _acoustics.IsTransition(startNode) ? (byte)1 : (byte)0,
                EndTransition = _acoustics.IsTransition(endNode) ? (byte)1 : (byte)0,
                FailureReason = SpatialOctreePathFailureReason.None,
                SearchIterations = searchIterations
            };
        }

        public bool TryEstimateRoomSize(Vector3 position, float probeDistance, out float roomSize)
        {
            return TryEstimateRoomSize(position, probeDistance, out roomSize, out _);
        }

        public bool TryEstimateRoomSize(Vector3 position, float probeDistance, out float roomSize,
            out string failureReason)
        {
            roomSize = 0f;
            failureReason = string.Empty;
            if (!IsUsable)
            {
                failureReason = "spatial graph is not usable";
                return false;
            }

            if (_traversableNodeIndices.Length == 0)
            {
                failureReason = "spatial graph has no traversable leaves";
                return false;
            }

            if (!_asset.Bounds.Contains(position))
            {
                failureReason = $"sample position outside graph bounds {_asset.Bounds}";
                return false;
            }

            var radius = Mathf.Max(0.25f, probeDistance);
            var cacheKey = RoomEstimateCacheKey.From(position, radius);
            if (_roomEstimateCache.TryGetValue(cacheKey, out roomSize))
                return true;

            Span<Vector3> sampleOrigins = stackalloc Vector3[RoomSampleOriginCount];
            var sampleCount = BuildRoomSampleOrigins(position, sampleOrigins);
            if (sampleCount == 0)
            {
                failureReason = $"no unblocked acoustic room sample near {position}";
                return false;
            }

            var firstPairSpan = float.PositiveInfinity;
            var secondPairSpan = float.PositiveInfinity;
            var thirdPairSpan = float.PositiveInfinity;
            var maxPairSpan = 0f;
            var pairSpanSum = 0f;
            var openPairCount = 0;
            var widePairCount = 0;
            foreach (var probeVector in HorizontalRoomProbeVectors)
            {
                var pairSpan = MeasureBestRoomPairSpan(sampleOrigins, sampleCount, probeVector, radius);
                InsertSmallestPairSpan(pairSpan, ref firstPairSpan, ref secondPairSpan, ref thirdPairSpan);
                maxPairSpan = Mathf.Max(maxPairSpan, pairSpan);
                pairSpanSum += pairSpan;
                if (pairSpan >= radius * RoomOpenPairSpanScale)
                    openPairCount++;
                if (pairSpan >= radius * RoomWidePairSpanScale)
                    widePairCount++;
            }

            if (!float.IsFinite(thirdPairSpan) || maxPairSpan <= 0.0001f)
            {
                failureReason = $"no open acoustic ray distance from sample position {position}";
                return false;
            }

            var averagePairSpan = pairSpanSum / HorizontalRoomProbeVectors.Length;
            var openFraction = pairSpanSum / (HorizontalRoomProbeVectors.Length * radius * 2f);
            var openPairFraction = openPairCount / (float)HorizontalRoomProbeVectors.Length;
            var widePairFraction = widePairCount / (float)HorizontalRoomProbeVectors.Length;
            var narrowScore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, radius * 1.25f, thirdPairSpan));
            var averageScore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, radius * 1.75f, averagePairSpan));
            var areaScore = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(openFraction), 0.85f));
            var broadHorizontalScore = Mathf.Clamp01(
                areaScore * 0.45f +
                averageScore * 0.25f +
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.65f, openPairFraction)) * 0.15f +
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.45f, widePairFraction)) * 0.15f);
            var upwardSpan = MeasureBestRoomRayDistance(sampleOrigins, sampleCount, Vector3.up,
                radius * RoomSkyProbeMultiplier);
            var verticalScore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, radius * 1.5f, upwardSpan));
            var skyScore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius * 1.25f, radius * 2.5f, upwardSpan));

            roomSize = Mathf.Clamp01(
                narrowScore * 0.18f +
                averageScore * 0.27f +
                areaScore * 0.25f +
                broadHorizontalScore * 0.18f +
                verticalScore * 0.07f +
                skyScore * 0.05f);
            roomSize = Mathf.Max(roomSize, Mathf.Lerp(0.42f, 0.74f, broadHorizontalScore) * broadHorizontalScore);
            roomSize = Mathf.Max(roomSize, Mathf.Lerp(0.58f, 0.82f, skyScore) * skyScore);
            CacheRoomEstimate(cacheKey, roomSize);
            return true;
        }

        private int BuildRoomSampleOrigins(Vector3 position, Span<Vector3> output)
        {
            var count = 0;
            foreach (var heightOffset in RoomSampleHeightOffsets)
                AddRoomSampleOrigin(position + Vector3.up * heightOffset, output, ref count);

            return count;
        }

        private void AddRoomSampleOrigin(Vector3 origin, Span<Vector3> output, ref int count)
        {
            if (Physics.CheckSphere(origin, RoomSampleClearanceRadius, _settings.OccluderMask,
                    _settings.TriggerInteraction))
                return;

            output[count++] = origin;
        }

        private float MeasureBestRoomPairSpan(ReadOnlySpan<Vector3> origins, int originCount, Vector3 direction,
            float maxDistance)
        {
            var best = 0f;
            foreach (var origin in origins[..originCount])
            {
                var span = MeasureRoomRayDistance(origin, direction, maxDistance) +
                           MeasureRoomRayDistance(origin, -direction, maxDistance);
                best = Mathf.Max(best, span);
            }

            return best;
        }

        private float MeasureBestRoomRayDistance(ReadOnlySpan<Vector3> origins, int originCount, Vector3 direction,
            float maxDistance)
        {
            var best = 0f;
            foreach (var origin in origins[..originCount])
                best = Mathf.Max(best, MeasureRoomRayDistance(origin, direction, maxDistance));

            return best;
        }

        private float MeasureRoomRayDistance(Vector3 origin, Vector3 direction, float maxDistance)
        {
            return Physics.Raycast(origin, direction, out var hit, maxDistance, _settings.OccluderMask,
                _settings.TriggerInteraction)
                ? Mathf.Max(0f, hit.distance)
                : maxDistance;
        }

        private static void InsertSmallestPairSpan(float value, ref float first, ref float second, ref float third)
        {
            if (value < first)
            {
                third = second;
                second = first;
                first = value;
                return;
            }

            if (value < second)
            {
                third = second;
                second = value;
                return;
            }

            if (value < third)
                third = value;
        }

        private bool TryResolveLocalTraversablePosition(Vector3 position, ReadOnlySpan<Vector3> localProbeOffsets,
            out Vector3 resolvedPosition, out int nodeIndex)
        {
            resolvedPosition = default;
            nodeIndex = -1;
            if (localProbeOffsets.Length <= 0)
                return false;

            var bestDistance = float.PositiveInfinity;
            var found = false;

            for (var i = 0; i < localProbeOffsets.Length; i++)
            {
                var offset = localProbeOffsets[i];
                if (offset.sqrMagnitude <= 0.000001f)
                    continue;

                var sample = position + offset;
                if (!TryFindContainingLeaf(sample, out var sampleNode) || !IsNodeTraversable(sampleNode))
                    continue;

                var distance = offset.sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                resolvedPosition = sample;
                nodeIndex = sampleNode;
                found = true;
            }

            return found;
        }

        private bool HasNeighborLink(int fromNodeIndex, int toNodeIndex)
        {
            if ((uint)fromNodeIndex >= (uint)_nodes.Length ||
                (uint)(fromNodeIndex + 1) >= (uint)_neighborStarts.Length)
                return false;

            var start = _neighborStarts[fromNodeIndex];
            var end = _neighborStarts[fromNodeIndex + 1];
            for (var i = start; i < end && (uint)i < (uint)_neighborIndices.Length; i++)
                if (_neighborIndices[i] == toNodeIndex)
                    return true;

            return false;
        }

        private ManagedPathSearchOutcome TryFindPathNodes(int startNode, int endNode, List<int> output,
            out int totalIterations)
        {
            output.Clear();
            totalIterations = 0;
            var corridorId = NextManagedRegionSearchId();
            var hasCorridor = TryBuildManagedRoutingCorridor(startNode, endNode, corridorId);
            var corridorBudget = Mathf.Min(_settings.MaxPathSearchIterations,
                Mathf.Max(2048, _settings.MaxPathSearchIterations / 4));
            var outcome = ManagedPathSearchOutcome.Exhausted;
            if (hasCorridor)
            {
                outcome = SearchManagedPathNodes(startNode, endNode, output, corridorBudget, corridorId, true,
                    out var corridorIterations);
                totalIterations += corridorIterations;
            }

            if (outcome == ManagedPathSearchOutcome.Success)
                return outcome;

            var remainingBudget = Mathf.Max(0, _settings.MaxPathSearchIterations - totalIterations);
            if (remainingBudget <= 0)
                return ManagedPathSearchOutcome.BudgetExceeded;

            outcome = SearchManagedPathNodes(startNode, endNode, output, remainingBudget, corridorId, false,
                out var fallbackIterations);
            totalIterations += fallbackIterations;
            return outcome;
        }

        private ManagedPathSearchOutcome SearchManagedPathNodes(
            int startNode,
            int endNode,
            List<int> output,
            int iterationBudget,
            int corridorId,
            bool restrictToCorridor,
            out int iterations)
        {
            output.Clear();
            iterations = 0;
            if (iterationBudget <= 0)
                return ManagedPathSearchOutcome.BudgetExceeded;

            var searchId = NextManagedSearchId();
            var openCount = 0;
            InitializeManagedNode(startNode, searchId);
            var startSearchIndex = _nodes[startNode].TraversableIndex;
            _gScore[startSearchIndex] = 0f;
            _fScore[startSearchIndex] = Heuristic(startNode, endNode);
            PushOrUpdateManagedOpenNode(ref openCount, startNode);

            while (openCount > 0)
            {
                if (iterations >= iterationBudget)
                    return ManagedPathSearchOutcome.BudgetExceeded;
                iterations++;

                var current = PopLowestManagedOpenNode(ref openCount);
                if (current == endNode)
                {
                    ReconstructNodePath(current, output);
                    return ManagedPathSearchOutcome.Success;
                }

                var currentSearchIndex = _nodes[current].TraversableIndex;
                _closedIds[currentSearchIndex] = searchId;
                var neighborStart = _neighborStarts[current];
                var neighborEnd = _neighborStarts[current + 1];
                for (var edgeIndex = neighborStart; edgeIndex < neighborEnd; edgeIndex++)
                {
                    var neighbor = _neighborIndices[edgeIndex];
                    if (!IsNodeTraversable(neighbor))
                        continue;

                    var neighborSearchIndex = _nodes[neighbor].TraversableIndex;
                    if ((uint)neighborSearchIndex >= (uint)_closedIds.Length ||
                        _closedIds[neighborSearchIndex] == searchId ||
                        (restrictToCorridor && !IsManagedRegionAllowed(_nodes[neighbor].RoutingRegionId, corridorId)))
                        continue;

                    if (_searchIds[neighborSearchIndex] != searchId)
                        InitializeManagedNode(neighbor, searchId);

                    var tentative = _gScore[currentSearchIndex] +
                                    ComputeTraversalEdgeCost(current, neighbor, startNode, endNode);
                    if (tentative >= _gScore[neighborSearchIndex])
                        continue;

                    _cameFrom[neighborSearchIndex] = current;
                    _gScore[neighborSearchIndex] = tentative;
                    _fScore[neighborSearchIndex] = tentative + Heuristic(neighbor, endNode);
                    PushOrUpdateManagedOpenNode(ref openCount, neighbor);
                }
            }

            return ManagedPathSearchOutcome.Exhausted;
        }

        private bool TryBuildManagedRoutingCorridor(int startNode, int endNode, int corridorId)
        {
            var startRegion = _nodes[startNode].RoutingRegionId;
            var endRegion = _nodes[endNode].RoutingRegionId;
            if ((uint)startRegion >= (uint)_routingRegions.Length ||
                (uint)endRegion >= (uint)_routingRegions.Length ||
                _managedRegionQueue.Length < _routingRegions.Length)
                return false;

            var read = 0;
            var write = 0;
            _managedRegionQueue[write++] = startRegion;
            _managedRegionVisitIds[startRegion] = corridorId;
            _managedRegionCameFrom[startRegion] = -1;
            var found = startRegion == endRegion;
            while (read < write && !found)
            {
                var current = _managedRegionQueue[read++];
                var start = _routingNeighborStarts[current];
                var end = _routingNeighborStarts[current + 1];
                for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    var neighbor = _routingNeighborIndices[edgeIndex];
                    if ((uint)neighbor >= (uint)_routingRegions.Length ||
                        _managedRegionVisitIds[neighbor] == corridorId ||
                        _routingRegions[neighbor].ComponentId != _routingRegions[startRegion].ComponentId)
                        continue;

                    _managedRegionVisitIds[neighbor] = corridorId;
                    _managedRegionCameFrom[neighbor] = current;
                    _managedRegionQueue[write++] = neighbor;
                    if (neighbor != endRegion)
                        continue;

                    found = true;
                    break;
                }
            }

            if (!found)
                return false;

            for (var region = endRegion; region >= 0; region = _managedRegionCameFrom[region])
                MarkManagedAllowedRegionAndNeighbors(region, corridorId);
            return true;
        }

        private void MarkManagedAllowedRegionAndNeighbors(int region, int corridorId)
        {
            _managedRegionAllowedIds[region] = corridorId;
            var start = _routingNeighborStarts[region];
            var end = _routingNeighborStarts[region + 1];
            for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                var neighbor = _routingNeighborIndices[edgeIndex];
                if ((uint)neighbor < (uint)_managedRegionAllowedIds.Length)
                    _managedRegionAllowedIds[neighbor] = corridorId;
            }
        }

        private bool IsManagedRegionAllowed(int regionId, int corridorId)
        {
            return (uint)regionId < (uint)_managedRegionAllowedIds.Length &&
                   _managedRegionAllowedIds[regionId] == corridorId;
        }

        private void BuildRoutedPath(IReadOnlyList<int> nodePath, Vector3 start, Vector3 end, List<Vector3> output)
        {
            output.Clear();
            if (nodePath == null || nodePath.Count == 0)
                return;

            output.Add(start);
            if (nodePath.Count == 1)
            {
                if ((end - start).sqrMagnitude > 0.000001f)
                    output.Add(end);
                return;
            }

            var previousPoint = start;
            for (var i = 0; i < nodePath.Count - 1; i++)
            {
                var fromNodeIndex = nodePath[i];
                var toNodeIndex = nodePath[i + 1];
                var portalPoint = ResolvePortalPoint(fromNodeIndex, toNodeIndex, previousPoint);
                if ((portalPoint - previousPoint).sqrMagnitude <= 0.000001f)
                    continue;

                output.Add(portalPoint);
                previousPoint = portalPoint;
            }

            if ((end - previousPoint).sqrMagnitude > 0.000001f)
                output.Add(end);
            else if (output.Count < 2)
                output.Add(end);
        }

        private Vector3 ResolvePortalPoint(int fromNodeIndex, int toNodeIndex, Vector3 previousPoint)
        {
            if (!TryGetPortalBounds(fromNodeIndex, toNodeIndex, out var portalMin, out var portalMax))
                return (_nodes[fromNodeIndex].Bounds.center + _nodes[toNodeIndex].Bounds.center) * 0.5f;

            var candidate = new Vector3(
                Mathf.Clamp(previousPoint.x, portalMin.x, portalMax.x),
                Mathf.Clamp(previousPoint.y, portalMin.y, portalMax.y),
                Mathf.Clamp(previousPoint.z, portalMin.z, portalMax.z));
            var axis = ResolveSharedFaceAxis(fromNodeIndex, toNodeIndex);
            if (IsPortalProbeClear(fromNodeIndex, toNodeIndex, candidate, axis))
                return candidate;

            return TryFindClearPortalPoint(fromNodeIndex, toNodeIndex, previousPoint, portalMin, portalMax,
                axis, out var clearPoint)
                ? clearPoint
                : candidate;
        }

        private bool TryGetPortalBounds(int fromNodeIndex, int toNodeIndex, out Vector3 portalMin,
            out Vector3 portalMax)
        {
            portalMin = default;
            portalMax = default;
            if ((uint)fromNodeIndex >= (uint)_nodes.Length || (uint)toNodeIndex >= (uint)_nodes.Length)
                return false;

            var fromBounds = _nodes[fromNodeIndex].Bounds;
            var toBounds = _nodes[toNodeIndex].Bounds;
            portalMin = Vector3.Max(fromBounds.min, toBounds.min);
            portalMax = Vector3.Min(fromBounds.max, toBounds.max);

            const float epsilon = 0.0001f;
            return portalMax.x + epsilon >= portalMin.x &&
                   portalMax.y + epsilon >= portalMin.y &&
                   portalMax.z + epsilon >= portalMin.z;
        }

        private bool HasClearPortal(int fromNodeIndex, int toNodeIndex)
        {
            if (!TryGetPortalBounds(fromNodeIndex, toNodeIndex, out var portalMin, out var portalMax))
                return false;

            var axis = ResolveSharedFaceAxis(fromNodeIndex, toNodeIndex);
            var portalSize = Vector3.Max(Vector3.zero, portalMax - portalMin);
            if (FaceArea(portalSize, axis) <= 0.0001f)
                return false;

            var center = (portalMin + portalMax) * 0.5f;
            return IsPortalProbeClear(fromNodeIndex, toNodeIndex, center, axis) ||
                   TryFindClearPortalPoint(fromNodeIndex, toNodeIndex, center, portalMin, portalMax, axis, out _);
        }

        private bool TryFindClearPortalPoint(int fromNodeIndex, int toNodeIndex, Vector3 preferredPoint,
            Vector3 portalMin, Vector3 portalMax, int axis, out Vector3 point)
        {
            point = default;
            var portalSize = Vector3.Max(Vector3.zero, portalMax - portalMin);
            var center = (portalMin + portalMax) * 0.5f;
            var uAxis = (axis + 1) % 3;
            var vAxis = (axis + 2) % 3;
            var uOffset = Mathf.Max(0f, portalSize[uAxis] * PortalSampleOffsetScale);
            var vOffset = Mathf.Max(0f, portalSize[vAxis] * PortalSampleOffsetScale);
            var bestDistance = float.PositiveInfinity;
            var found = false;

            for (var u = -1; u <= 1; u++)
            for (var v = -1; v <= 1; v++)
            {
                var sample = center;
                sample[uAxis] += u * uOffset;
                sample[vAxis] += v * vOffset;
                if (!IsPortalProbeClear(fromNodeIndex, toNodeIndex, sample, axis))
                    continue;

                var distance = (sample - preferredPoint).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                point = sample;
                found = true;
            }

            return found;
        }

        private bool IsPortalProbeClear(int fromNodeIndex, int toNodeIndex, Vector3 sample, int axis)
        {
            if ((uint)fromNodeIndex >= (uint)_nodes.Length || (uint)toNodeIndex >= (uint)_nodes.Length)
                return false;

            var direction = _nodes[toNodeIndex].Bounds.center[axis] >= _nodes[fromNodeIndex].Bounds.center[axis]
                ? 1f
                : -1f;
            var normal = AxisVector(axis) * direction;
            var inset = Mathf.Max(PortalProbeInsetMin,
                Mathf.Min(_nodes[fromNodeIndex].Bounds.size[axis], _nodes[toNodeIndex].Bounds.size[axis]) *
                PortalProbeInsetScale);
            return !SegmentIntersectsBlockedLeaf(sample - normal * inset, sample + normal * inset);
        }

        private int NextManagedSearchId()
        {
            if (_managedSearchId < int.MaxValue)
                return ++_managedSearchId;

            Array.Fill(_searchIds, 0);
            Array.Fill(_closedIds, 0);
            _managedSearchId = 1;
            return _managedSearchId;
        }

        private int NextManagedRegionSearchId()
        {
            if (_managedRegionSearchId < int.MaxValue)
                return ++_managedRegionSearchId;

            Array.Fill(_managedRegionVisitIds, 0);
            Array.Fill(_managedRegionAllowedIds, 0);
            _managedRegionSearchId = 1;
            return _managedRegionSearchId;
        }

        private void InitializeManagedNode(int node, int searchId)
        {
            var searchIndex = _nodes[node].TraversableIndex;
            _searchIds[searchIndex] = searchId;
            _closedIds[searchIndex] = 0;
            _gScore[searchIndex] = float.PositiveInfinity;
            _fScore[searchIndex] = float.PositiveInfinity;
            _cameFrom[searchIndex] = -1;
            _managedHeapIndices[searchIndex] = -1;
        }

        private void PushOrUpdateManagedOpenNode(ref int openCount, int node)
        {
            var searchIndex = _nodes[node].TraversableIndex;
            var heapIndex = _managedHeapIndices[searchIndex];
            if (heapIndex >= 0 && heapIndex < openCount && _managedOpen[heapIndex] == node)
            {
                SiftManagedOpenUp(heapIndex);
                return;
            }

            heapIndex = openCount++;
            _managedOpen[heapIndex] = node;
            _managedHeapIndices[searchIndex] = heapIndex;
            SiftManagedOpenUp(heapIndex);
        }

        private int PopLowestManagedOpenNode(ref int openCount)
        {
            var bestNode = _managedOpen[0];
            _managedHeapIndices[_nodes[bestNode].TraversableIndex] = -1;
            openCount--;
            if (openCount <= 0)
                return bestNode;

            var replacement = _managedOpen[openCount];
            _managedOpen[0] = replacement;
            _managedHeapIndices[_nodes[replacement].TraversableIndex] = 0;
            SiftManagedOpenDown(openCount, 0);
            return bestNode;
        }

        private void SiftManagedOpenUp(int index)
        {
            while (index > 0)
            {
                var parent = (index - 1) >> 1;
                var node = _managedOpen[index];
                var parentNode = _managedOpen[parent];
                if (!HasHigherManagedOpenPriority(node, parentNode))
                    return;

                _managedOpen[index] = parentNode;
                _managedHeapIndices[_nodes[parentNode].TraversableIndex] = index;
                _managedOpen[parent] = node;
                _managedHeapIndices[_nodes[node].TraversableIndex] = parent;
                index = parent;
            }
        }

        private void SiftManagedOpenDown(int openCount, int index)
        {
            while (true)
            {
                var left = index * 2 + 1;
                if (left >= openCount)
                    return;

                var right = left + 1;
                var best = left;
                if (right < openCount &&
                    HasHigherManagedOpenPriority(_managedOpen[right], _managedOpen[left]))
                    best = right;

                var node = _managedOpen[index];
                var child = _managedOpen[best];
                if (!HasHigherManagedOpenPriority(child, node))
                    return;

                _managedOpen[index] = child;
                _managedHeapIndices[_nodes[child].TraversableIndex] = index;
                _managedOpen[best] = node;
                _managedHeapIndices[_nodes[node].TraversableIndex] = best;
                index = best;
            }
        }

        private bool HasHigherManagedOpenPriority(int a, int b)
        {
            var aIndex = _nodes[a].TraversableIndex;
            var bIndex = _nodes[b].TraversableIndex;
            var fA = _fScore[aIndex];
            var fB = _fScore[bIndex];
            if (!Mathf.Approximately(fA, fB))
                return fA < fB;
            return _gScore[aIndex] > _gScore[bIndex];
        }

        private float Heuristic(int a, int b)
        {
            return Vector3.Distance(_nodes[a].Bounds.center, _nodes[b].Bounds.center);
        }

        private float ComputeTraversalEdgeCost(int fromNodeIndex, int toNodeIndex, int startNode, int endNode)
        {
            var baseCost = Vector3.Distance(_nodes[fromNodeIndex].Bounds.center, _nodes[toNodeIndex].Bounds.center);
            var portalPenalty = ComputePortalConstrictionPenalty(fromNodeIndex, toNodeIndex);
            var exteriorPenalty = ComputeExteriorPenalty(fromNodeIndex, toNodeIndex, startNode, endNode);
            return baseCost * (1f + portalPenalty + exteriorPenalty);
        }

        private float ComputeNodePathTraversalPenalty(IReadOnlyList<int> nodePath, int startNode, int endNode)
        {
            if (nodePath == null || nodePath.Count < 2)
                return 0f;

            var penalty = 0f;
            for (var i = 0; i < nodePath.Count - 1; i++)
            {
                var from = nodePath[i];
                var to = nodePath[i + 1];
                var baseCost = Vector3.Distance(_nodes[from].Bounds.center, _nodes[to].Bounds.center);
                penalty += baseCost * (ComputePortalConstrictionPenalty(from, to) +
                                       ComputeExteriorPenalty(from, to, startNode, endNode));
            }

            return penalty;
        }

        private float ComputePortalConstrictionPenalty(int fromNodeIndex, int toNodeIndex)
        {
            if (!_acoustics.TryFindEdge(fromNodeIndex, toNodeIndex, out var edgeIndex))
                return PortalConstrictionPenaltyScale;

            var constriction = 1f - _acoustics.GetEdgeAperture(edgeIndex);
            return constriction * constriction * PortalConstrictionPenaltyScale;
        }

        private float ComputeExteriorPenalty(int fromNodeIndex, int toNodeIndex, int startNode, int endNode)
        {
            var edgeUsesExterior = _acoustics.IsExterior(fromNodeIndex) || _acoustics.IsExterior(toNodeIndex);
            if (!edgeUsesExterior)
                return 0f;

            var endpointsAreExterior = _acoustics.IsExterior(startNode) && _acoustics.IsExterior(endNode);
            return endpointsAreExterior ? 0f : ExteriorPathPenaltyScale;
        }

        private int ResolveSharedFaceAxis(int fromNodeIndex, int toNodeIndex)
        {
            var a = _nodes[fromNodeIndex].Bounds;
            var b = _nodes[toNodeIndex].Bounds;
            var dx = Mathf.Min(Mathf.Abs(a.max.x - b.min.x), Mathf.Abs(b.max.x - a.min.x));
            var dy = Mathf.Min(Mathf.Abs(a.max.y - b.min.y), Mathf.Abs(b.max.y - a.min.y));
            var dz = Mathf.Min(Mathf.Abs(a.max.z - b.min.z), Mathf.Abs(b.max.z - a.min.z));
            if (dx <= dy && dx <= dz)
                return 0;
            return dy <= dz ? 1 : 2;
        }

        private static float FaceArea(Vector3 size, int axis)
        {
            return axis switch
            {
                0 => Mathf.Max(0f, size.y) * Mathf.Max(0f, size.z),
                1 => Mathf.Max(0f, size.x) * Mathf.Max(0f, size.z),
                _ => Mathf.Max(0f, size.x) * Mathf.Max(0f, size.y)
            };
        }

        private static Vector3 AxisVector(int axis)
        {
            return axis switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward
            };
        }

        private bool HasSameComponent(int a, int b)
        {
            if ((uint)a >= (uint)_nodes.Length || (uint)b >= (uint)_nodes.Length)
                return false;

            var componentId = _nodes[a].ComponentId;
            return componentId >= 0 && componentId == _nodes[b].ComponentId;
        }

        public void InvalidateDynamicOccluders()
        {
            RefreshDynamicOccluders(true);
        }

        private void RefreshDynamicOccluders(bool force = false)
        {
            if (_disposed || !_nativeDynamicBlockedNodes.IsCreated)
                return;

            var currentFrame = Time.frameCount;
            if (!force && Application.isPlaying && _lastDynamicRefreshFrame == currentFrame)
                return;
            _lastDynamicRefreshFrame = currentFrame;

            var changed = force;
            for (var colliderIndex = 0; colliderIndex < _dynamicOccluders.Count; colliderIndex++)
            {
                var collider = _dynamicOccluders[colliderIndex];
                var active = collider && collider.enabled && collider.gameObject.activeInHierarchy;
                var bounds = active ? collider.bounds : default;
                if (_dynamicOccluderStates[colliderIndex].Matches(active, bounds))
                    continue;

                changed = true;
                _dynamicOccluderStates[colliderIndex] = new DynamicOccluderState(active, bounds);
            }

            if (!changed)
                return;

            _activePathJobs.Complete();
            for (var blockedIndex = 0; blockedIndex < _dynamicBlockedNodeIndices.Count; blockedIndex++)
            {
                var nodeIndex = _dynamicBlockedNodeIndices[blockedIndex];
                if ((uint)nodeIndex < (uint)_nativeDynamicBlockedNodes.Length)
                    _nativeDynamicBlockedNodes[nodeIndex] = 0;
            }

            _dynamicBlockedNodeIndices.Clear();
            for (var colliderIndex = 0; colliderIndex < _dynamicOccluders.Count; colliderIndex++)
            {
                var state = _dynamicOccluderStates[colliderIndex];
                var collider = _dynamicOccluders[colliderIndex];
                if (!state.Active || !collider)
                    continue;

                var bounds = state.Bounds;
                bounds.Expand(Mathf.Max(0.02f, _settings.MinNodeSize * 0.04f));
                MarkDynamicBlockedLeaves(collider, bounds);
            }

            DynamicBlockedNodeCount = _dynamicBlockedNodeIndices.Count;
            _directLineCache.Clear();
            _directObstructionCache.Clear();
            _nearestTraversableLeafCache.Clear();
            _roomEstimateCache.Clear();
        }

        private void MarkDynamicBlockedLeaves(Collider collider, Bounds occluderBounds)
        {
            if (_nodes.Length == 0 || !_asset.Bounds.Intersects(occluderBounds))
                return;

            _dynamicTraversalStack.Clear();
            _dynamicTraversalStack.Add(0);
            while (_dynamicTraversalStack.Count > 0)
            {
                var stackIndex = _dynamicTraversalStack.Count - 1;
                var nodeIndex = _dynamicTraversalStack[stackIndex];
                _dynamicTraversalStack.RemoveAt(stackIndex);
                if ((uint)nodeIndex >= (uint)_nodes.Length)
                    continue;

                var node = _nodes[nodeIndex];
                if (!node.Bounds.Intersects(occluderBounds))
                    continue;
                if (node.IsLeaf)
                {
                    if (node.IsTraversable && _nativeDynamicBlockedNodes[nodeIndex] == 0 &&
                        DynamicColliderIntersectsLeaf(collider, node.Bounds))
                    {
                        _nativeDynamicBlockedNodes[nodeIndex] = 1;
                        _dynamicBlockedNodeIndices.Add(nodeIndex);
                    }

                    continue;
                }

                var firstChild = node.FirstChildIndex;
                if (firstChild < 0)
                    continue;
                var endChild = Mathf.Min(firstChild + node.ChildCount, _nodes.Length);
                for (var childIndex = firstChild; childIndex < endChild; childIndex++)
                    _dynamicTraversalStack.Add(childIndex);
            }
        }

        private bool DynamicColliderIntersectsLeaf(Collider collider, Bounds leafBounds)
        {
            if (!collider || !collider.bounds.Intersects(leafBounds))
                return false;

            if (!SupportsClosestPointSampling(collider))
                return UnsupportedDynamicColliderIntersectsLeaf(collider, leafBounds);

            var closestToCenter = collider.ClosestPoint(leafBounds.center);
            if (leafBounds.Contains(closestToCenter))
                return true;

            var closestLeafPoint = leafBounds.ClosestPoint(collider.bounds.center);
            return leafBounds.Contains(collider.ClosestPoint(closestLeafPoint));
        }

        private bool UnsupportedDynamicColliderIntersectsLeaf(Collider collider, Bounds leafBounds)
        {
            var hitCount = Physics.OverlapBoxNonAlloc(
                leafBounds.center,
                leafBounds.extents,
                _dynamicOverlapBuffer,
                Quaternion.identity,
                1 << collider.gameObject.layer,
                _settings.TriggerInteraction);

            var intersects = false;
            for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                var hit = _dynamicOverlapBuffer[hitIndex];
                intersects |= hit == collider;
                _dynamicOverlapBuffer[hitIndex] = null;
            }

            // A saturated non-alloc query may omit the target collider. Stay conservative without allocating.
            return intersects || hitCount >= _dynamicOverlapBuffer.Length;
        }

        private static bool SupportsClosestPointSampling(Collider collider)
        {
            return collider is BoxCollider ||
                   collider is SphereCollider ||
                   collider is CapsuleCollider ||
                   (collider is MeshCollider meshCollider && meshCollider.convex);
        }

        private bool IsNodeDynamicallyBlocked(int nodeIndex)
        {
            return (uint)nodeIndex < (uint)_nativeDynamicBlockedNodes.Length &&
                   _nativeDynamicBlockedNodes[nodeIndex] != 0;
        }

        private bool IsNodeTraversable(int nodeIndex)
        {
            return (uint)nodeIndex < (uint)_nodes.Length && _nodes[nodeIndex].IsTraversable &&
                   !IsNodeDynamicallyBlocked(nodeIndex);
        }


        private void CacheDirectLineResult(DirectLineCacheKey key, bool visible)
        {
            if (_directLineCache.Count >= DirectLineCacheCapacity)
                _directLineCache.Clear();

            _directLineCache[key] = visible;
        }

        private void CacheDirectObstructionEstimate(DirectObstructionCacheKey key,
            DirectObstructionEstimate estimate)
        {
            if (_directObstructionCache.Count >= DirectObstructionCacheCapacity)
                _directObstructionCache.Clear();

            _directObstructionCache[key] = estimate;
        }

        private bool PathIntersectsBlockedLeaf(IReadOnlyList<Vector3> path)
        {
            if (path == null || path.Count < 2)
                return false;

            for (var i = 1; i < path.Count; i++)
                if (SegmentIntersectsBlockedLeaf(path[i - 1], path[i]))
                    return true;

            return false;
        }

        private bool HasClearLine(Vector3 start, Vector3 end, int startNode, int endNode)
        {
            var distance = Vector3.Distance(start, end);
            if (distance <= 0.001f)
                return true;

            return HasContinuousLineTransitions(start, end, startNode, endNode, distance);
        }

        private DirectObstructionEstimate EstimateExactDirectObstruction(Vector3 start, Vector3 end,
            float directDistance)
        {
            _segmentIntervals.Clear();
            CollectSegmentLeafIntervals(0, start, end, _segmentIntervals, true);
            if (_segmentIntervals.Count == 0)
                return DirectObstructionEstimate.Empty(directDistance);

            _segmentIntervals.Sort(static (a, b) => a.Min.CompareTo(b.Min));
            var blockedDistance = 0f;
            var blockedSegments = 0;
            var currentMin = -1f;
            var currentMax = -1f;

            foreach (var interval in _segmentIntervals)
            {
                if (!TryTrimEndpointContactInterval(interval, directDistance, out var intervalMin,
                        out var intervalMax))
                    continue;

                if (intervalMax - intervalMin <= 0.0001f)
                    continue;

                if (currentMin < 0f)
                {
                    currentMin = intervalMin;
                    currentMax = intervalMax;
                    blockedSegments++;
                    continue;
                }

                if (intervalMin <= currentMax + 0.0001f)
                {
                    currentMax = Mathf.Max(currentMax, intervalMax);
                    continue;
                }

                blockedDistance += Mathf.Max(0f, currentMax - currentMin) * directDistance;
                currentMin = intervalMin;
                currentMax = intervalMax;
                blockedSegments++;
            }

            if (currentMin >= 0f)
                blockedDistance += Mathf.Max(0f, currentMax - currentMin) * directDistance;

            _segmentIntervals.Clear();
            return new DirectObstructionEstimate(directDistance, blockedDistance, blockedSegments);
        }

        private bool SegmentIntersectsBlockedLeaf(Vector3 start, Vector3 end)
        {
            if (_nodes.Length == 0 || !_asset.Bounds.Contains(start) || !_asset.Bounds.Contains(end))
                return true;

            return SegmentIntersectsBlockedLeaf(0, start, end);
        }

        private bool SegmentIntersectsBlockedLeaf(int nodeIndex, Vector3 start, Vector3 end)
        {
            if ((uint)nodeIndex >= (uint)_nodes.Length)
                return false;

            var node = _nodes[nodeIndex];
            if (!TryGetSegmentBoundsInterval(start, end, node.Bounds, out var tMin, out var tMax))
                return false;

            if (node.IsLeaf)
                return (node.IsBlocked || IsNodeDynamicallyBlocked(nodeIndex)) && tMax - tMin > 0.0001f;

            var firstChild = node.FirstChildIndex;
            if (firstChild < 0)
                return false;

            var endChild = Mathf.Min(firstChild + node.ChildCount, _nodes.Length);
            for (var i = firstChild; i < endChild; i++)
                if (SegmentIntersectsBlockedLeaf(i, start, end))
                    return true;

            return false;
        }

        private bool HasContinuousLineTransitions(Vector3 start, Vector3 end, int startNode, int endNode,
            float directDistance)
        {
            _segmentIntervals.Clear();
            CollectSegmentLeafIntervals(0, start, end, _segmentIntervals, false);
            if (_segmentIntervals.Count == 0)
                return false;

            _segmentIntervals.Sort(static (a, b) => a.Min.CompareTo(b.Min));

            var previousNode = startNode;
            foreach (var interval in _segmentIntervals)
            {
                if (interval.Max - interval.Min <= 0.0001f)
                    continue;

                var nodeIndex = interval.NodeIndex;
                if ((uint)nodeIndex >= (uint)_nodes.Length)
                    return false;

                if (!IsNodeTraversable(nodeIndex))
                {
                    if (!TryTrimEndpointContactInterval(interval, directDistance, out _, out _))
                        continue;

                    return false;
                }

                if (nodeIndex != previousNode &&
                    !HasContinuousLineSampleTransition(previousNode, nodeIndex))
                    return false;

                previousNode = nodeIndex;
            }

            return previousNode == endNode || HasContinuousLineSampleTransition(previousNode, endNode);
        }

        private void CollectSegmentLeafIntervals(int nodeIndex, Vector3 start, Vector3 end,
            List<SegmentInterval> intervals, bool blockedOnly)
        {
            if ((uint)nodeIndex >= (uint)_nodes.Length)
                return;

            var node = _nodes[nodeIndex];
            if (!TryGetSegmentBoundsInterval(start, end, node.Bounds, out var tMin, out var tMax))
                return;

            if (node.IsLeaf)
            {
                if ((!blockedOnly || node.IsBlocked || IsNodeDynamicallyBlocked(nodeIndex)) &&
                    tMax - tMin > 0.0001f)
                    intervals.Add(new SegmentInterval(tMin, tMax, nodeIndex));
                return;
            }

            var firstChild = node.FirstChildIndex;
            if (firstChild < 0)
                return;

            var endChild = Mathf.Min(firstChild + node.ChildCount, _nodes.Length);
            for (var i = firstChild; i < endChild; i++)
                CollectSegmentLeafIntervals(i, start, end, intervals, blockedOnly);
        }

        private bool TryTrimEndpointContactInterval(SegmentInterval interval, float directDistance,
            out float min, out float max)
        {
            min = interval.Min;
            max = interval.Max;
            if (directDistance <= 0.001f)
                return false;

            // Source and listener samples often sit on a surface; trim only the tiny endpoint contact
            // interval so that contact with the emitter wall does not count as intervening geometry.
            var clearance = ResolveEndpointContactClearanceT(directDistance);
            if (min <= clearance)
                min = Mathf.Max(min, clearance);
            if (max >= 1f - clearance)
                max = Mathf.Min(max, 1f - clearance);

            return max > min;
        }

        private float ResolveEndpointContactClearanceT(float directDistance)
        {
            var clearanceDistance = Mathf.Clamp(
                _settings.MinNodeSize * EndpointContactClearanceScale,
                EndpointContactClearanceMin,
                EndpointContactClearanceMax);
            return Mathf.Min(EndpointContactClearanceMaxRatio, clearanceDistance / directDistance);
        }

        private static bool TryGetSegmentBoundsInterval(Vector3 start, Vector3 end, Bounds bounds, out float tMin,
            out float tMax)
        {
            const float epsilon = 0.0001f;
            var min = bounds.min;
            var max = bounds.max;
            var delta = end - start;
            tMin = 0f;
            tMax = 1f;

            return ClipSegmentAxis(start.x, delta.x, min.x, max.x, ref tMin, ref tMax, epsilon) &&
                   ClipSegmentAxis(start.y, delta.y, min.y, max.y, ref tMin, ref tMax, epsilon) &&
                   ClipSegmentAxis(start.z, delta.z, min.z, max.z, ref tMin, ref tMax, epsilon);
        }

        private static bool ClipSegmentAxis(float start, float delta, float min, float max, ref float tMin,
            ref float tMax, float epsilon)
        {
            if (Mathf.Abs(delta) <= epsilon)
                return start >= min - epsilon && start <= max + epsilon;

            var inverse = 1f / delta;
            var near = (min - start) * inverse;
            var far = (max - start) * inverse;
            if (near > far)
                (near, far) = (far, near);

            tMin = Mathf.Max(tMin, near);
            tMax = Mathf.Min(tMax, far);
            return tMin <= tMax + epsilon;
        }

        private bool HasContinuousLineSampleTransition(int fromNodeIndex, int toNodeIndex)
        {
            if (fromNodeIndex == toNodeIndex)
                return true;

            return (HasNeighborLink(fromNodeIndex, toNodeIndex) || HasNeighborLink(toNodeIndex, fromNodeIndex)) &&
                   HasClearPortal(fromNodeIndex, toNodeIndex);
        }

        private static Vector3[] BuildHorizontalRoomProbeVectors()
        {
            var vectors = new Vector3[HorizontalRoomProbeDirections];
            for (var i = 0; i < vectors.Length; i++)
            {
                var angle = Mathf.PI * i / vectors.Length;
                vectors[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            return vectors;
        }

        private void CacheNearestTraversableLeaf(PositionCacheKey key, int nodeIndex)
        {
            if (_nearestTraversableLeafCache.Count >= NearestLeafCacheCapacity)
                _nearestTraversableLeafCache.Clear();

            _nearestTraversableLeafCache[key] = nodeIndex;
        }

        private void CacheRoomEstimate(RoomEstimateCacheKey key, float roomSize)
        {
            if (_roomEstimateCache.Count >= RoomEstimateCacheCapacity)
                _roomEstimateCache.Clear();

            _roomEstimateCache[key] = roomSize;
        }

        private void ReconstructNodePath(int current, List<int> output)
        {
            output.Clear();
            while (current >= 0)
            {
                output.Add(current);
                var searchIndex = _nodes[current].TraversableIndex;
                current = (uint)searchIndex < (uint)_cameFrom.Length ? _cameFrom[searchIndex] : -1;
            }

            output.Reverse();
        }

        private void CreateNativeGraph()
        {
            _nativeNodes = new NativeArray<SpatialOctreeAcousticNodeJobData>(_nodes.Length, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                var acousticData = _acoustics.GetNodeData(i);
                var center = node.Bounds.center;
                var extents = node.Bounds.extents;
                _nativeNodes[i] = new SpatialOctreeAcousticNodeJobData
                {
                    Center = center,
                    Extents = extents,
                    FirstChildIndex = node.FirstChildIndex,
                    ChildCount = node.ChildCount,
                    ComponentId = node.ComponentId,
                    TraversableIndex = node.TraversableIndex,
                    RoutingRegionId = node.RoutingRegionId,
                    IsLeaf = node.IsLeaf ? (byte)1 : (byte)0,
                    IsTraversable = node.IsTraversable ? (byte)1 : (byte)0,
                    IsExterior = acousticData.IsExterior ? (byte)1 : (byte)0,
                    AcousticFlags = acousticData.Flags,
                    AcousticClusterId = acousticData.ClusterId,
                    AcousticOpenness = acousticData.Openness,
                    PortalAperture = acousticData.Aperture
                };
            }

            _nativeNeighborStarts = new NativeArray<int>(_neighborStarts.Length, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _nativeNeighborStarts.CopyFrom(_neighborStarts);
            _nativeNeighborIndices = new NativeArray<int>(_neighborIndices.Length, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _nativeNeighborIndices.CopyFrom(_neighborIndices);
            _nativeNeighborApertures = new NativeArray<float>(_neighborApertures.Length, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _nativeNeighborApertures.CopyFrom(_neighborApertures);
            _nativeTraversableNodeIndices = new NativeArray<int>(_traversableNodeIndices.Length,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _nativeTraversableNodeIndices.CopyFrom(_traversableNodeIndices);
            _nativeRoutingRegions = new NativeArray<SpatialOctreeRoutingRegionData>(_routingRegions.Length,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _nativeRoutingRegions.CopyFrom(_routingRegions);
            _nativeRoutingNeighborStarts = new NativeArray<int>(_routingNeighborStarts.Length,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _nativeRoutingNeighborStarts.CopyFrom(_routingNeighborStarts);
            _nativeRoutingNeighborIndices = new NativeArray<int>(_routingNeighborIndices.Length,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _nativeRoutingNeighborIndices.CopyFrom(_routingNeighborIndices);
            _nativeDynamicBlockedNodes = new NativeArray<byte>(_nodes.Length, Allocator.Persistent);
        }

        private void CreatePathJobScratch()
        {
            var scratchLength = Mathf.Max(1, _traversableNodeIndices.Length);
            var regionScratchLength = Mathf.Max(1, _routingRegions.Length);
            _pathJobGScores = new NativeArray<float>(scratchLength, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _pathJobFScores = new NativeArray<float>(scratchLength, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _pathJobCameFrom = new NativeArray<int>(scratchLength, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _pathJobSearchIds = new NativeArray<int>(scratchLength, Allocator.Persistent);
            _pathJobClosedIds = new NativeArray<int>(scratchLength, Allocator.Persistent);
            _pathJobOpen = new NativeArray<int>(scratchLength, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _pathJobHeapIndices = new NativeArray<int>(scratchLength, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _pathJobRegionCameFrom = new NativeArray<int>(regionScratchLength, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _pathJobRegionVisitIds = new NativeArray<int>(regionScratchLength, Allocator.Persistent);
            _pathJobRegionAllowedIds = new NativeArray<int>(regionScratchLength, Allocator.Persistent);
            _pathJobRegionQueue = new NativeArray<int>(regionScratchLength, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private void DisposePathJobScratch()
        {
            if (_pathJobGScores.IsCreated)
                _pathJobGScores.Dispose();
            if (_pathJobFScores.IsCreated)
                _pathJobFScores.Dispose();
            if (_pathJobCameFrom.IsCreated)
                _pathJobCameFrom.Dispose();
            if (_pathJobSearchIds.IsCreated)
                _pathJobSearchIds.Dispose();
            if (_pathJobClosedIds.IsCreated)
                _pathJobClosedIds.Dispose();
            if (_pathJobOpen.IsCreated)
                _pathJobOpen.Dispose();
            if (_pathJobHeapIndices.IsCreated)
                _pathJobHeapIndices.Dispose();
            if (_pathJobRegionCameFrom.IsCreated)
                _pathJobRegionCameFrom.Dispose();
            if (_pathJobRegionVisitIds.IsCreated)
                _pathJobRegionVisitIds.Dispose();
            if (_pathJobRegionAllowedIds.IsCreated)
                _pathJobRegionAllowedIds.Dispose();
            if (_pathJobRegionQueue.IsCreated)
                _pathJobRegionQueue.Dispose();
        }

        private int ReservePathJobSearchIdBase(int queryCount)
        {
            queryCount = Mathf.Max(1, queryCount);
            var reservedIds = queryCount * 3 + 1;
            if (_pathJobSearchIdBase <= int.MaxValue - reservedIds - 1)
            {
                var baseId = _pathJobSearchIdBase;
                _pathJobSearchIdBase += reservedIds;
                return baseId;
            }

            _activePathJobs.Complete();
            if (_pathJobSearchIds.IsCreated)
                _pathJobSearchIds.Dispose();
            if (_pathJobClosedIds.IsCreated)
                _pathJobClosedIds.Dispose();

            var scratchLength = Mathf.Max(1, _traversableNodeIndices.Length);
            _pathJobSearchIds = new NativeArray<int>(scratchLength, Allocator.Persistent);
            _pathJobClosedIds = new NativeArray<int>(scratchLength, Allocator.Persistent);
            if (_pathJobRegionVisitIds.IsCreated)
                _pathJobRegionVisitIds.Dispose();
            if (_pathJobRegionAllowedIds.IsCreated)
                _pathJobRegionAllowedIds.Dispose();
            var regionScratchLength = Mathf.Max(1, _routingRegions.Length);
            _pathJobRegionVisitIds = new NativeArray<int>(regionScratchLength, Allocator.Persistent);
            _pathJobRegionAllowedIds = new NativeArray<int>(regionScratchLength, Allocator.Persistent);
            _pathJobSearchIdBase = reservedIds;
            return 0;
        }

        internal void ReleaseSharedPathScratch()
        {
            _sharedPathScratchInUse = false;
        }

        public readonly struct TraversablePositionInfo
        {
            public readonly Vector3 RawPosition;
            public readonly Vector3 ResolvedPosition;
            public readonly int NodeIndex;
            public readonly int ComponentId;
            public readonly TraversablePositionResolveMode Mode;
            public readonly float Displacement;

            public TraversablePositionInfo(
                Vector3 rawPosition,
                Vector3 resolvedPosition,
                int nodeIndex,
                int componentId,
                TraversablePositionResolveMode mode)
            {
                RawPosition = rawPosition;
                ResolvedPosition = resolvedPosition;
                NodeIndex = nodeIndex;
                ComponentId = componentId;
                Mode = mode;
                Displacement = Vector3.Distance(rawPosition, resolvedPosition);
            }

            public bool Succeeded => Mode != TraversablePositionResolveMode.Failed && NodeIndex >= 0;
        }

        public struct GraphValidationSummary
        {
            public int NodeCount;
            public int LeafCount;
            public int TraversableLeafCount;
            public int BlockedLeafCount;
            public int ComponentCount;
            public int NeighborEdgeCount;
            public int InvalidNeighborReferenceCount;
            public int AsymmetricNeighborLinkCount;
            public int BlockedNeighborLinkCount;
            public int InvalidPortalLinkCount;
            public float MinLeafSize;
            public float MaxLeafSize;
            public float AverageLeafSize;

            public bool HasErrors => InvalidNeighborReferenceCount > 0 ||
                                     AsymmetricNeighborLinkCount > 0 ||
                                     BlockedNeighborLinkCount > 0 ||
                                     InvalidPortalLinkCount > 0;
        }

        public readonly struct DirectObstructionEstimate
        {
            public readonly float DirectDistance;
            public readonly float BlockedDistance;
            public readonly int BlockedSegmentCount;

            public DirectObstructionEstimate(float directDistance, float blockedDistance, int blockedSegmentCount)
            {
                DirectDistance = Mathf.Max(0f, directDistance);
                BlockedDistance = Mathf.Max(0f, blockedDistance);
                BlockedSegmentCount = Mathf.Max(0, blockedSegmentCount);
            }

            public float BlockedRatio => DirectDistance > 0.001f
                ? Mathf.Clamp01(BlockedDistance / DirectDistance)
                : 0f;

            public bool HasObstruction => BlockedDistance > ObstructionDistanceEpsilon || BlockedSegmentCount > 0;

            public static DirectObstructionEstimate Empty(float directDistance)
            {
                return new DirectObstructionEstimate(directDistance, 0f, 0);
            }
        }

        private readonly struct DynamicOccluderState
        {
            public readonly bool Active;
            public readonly Bounds Bounds;

            public DynamicOccluderState(bool active, Bounds bounds)
            {
                Active = active;
                Bounds = bounds;
            }

            public bool Matches(bool active, Bounds bounds)
            {
                return Active == active && (!active ||
                                            (Bounds.center == bounds.center && Bounds.size == bounds.size));
            }
        }

        private readonly struct SegmentInterval
        {
            public readonly float Min;
            public readonly float Max;
            public readonly int NodeIndex;

            public SegmentInterval(float min, float max, int nodeIndex)
            {
                Min = Mathf.Clamp01(min);
                Max = Mathf.Clamp01(max);
                NodeIndex = nodeIndex;
            }
        }

        private readonly struct DirectLineCacheKey : IEquatable<DirectLineCacheKey>
        {
            private readonly int _startNode;
            private readonly int _endNode;
            private readonly PositionCacheKey _start;
            private readonly PositionCacheKey _end;

            private DirectLineCacheKey(int startNode, int endNode, PositionCacheKey start, PositionCacheKey end)
            {
                _startNode = startNode;
                _endNode = endNode;
                _start = start;
                _end = end;
            }

            public static DirectLineCacheKey From(int startNode, int endNode, Vector3 start, Vector3 end)
            {
                return new DirectLineCacheKey(startNode, endNode, PositionCacheKey.From(start),
                    PositionCacheKey.From(end));
            }

            public bool Equals(DirectLineCacheKey other)
            {
                return _startNode == other._startNode &&
                       _endNode == other._endNode &&
                       _start.Equals(other._start) &&
                       _end.Equals(other._end);
            }

            public override bool Equals(object obj)
            {
                return obj is DirectLineCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _startNode;
                    hash = (hash * 397) ^ _endNode;
                    hash = (hash * 397) ^ _start.GetHashCode();
                    hash = (hash * 397) ^ _end.GetHashCode();
                    return hash;
                }
            }
        }

        private readonly struct DirectObstructionCacheKey : IEquatable<DirectObstructionCacheKey>
        {
            private readonly int _startX;
            private readonly int _startY;
            private readonly int _startZ;
            private readonly int _endX;
            private readonly int _endY;
            private readonly int _endZ;

            private DirectObstructionCacheKey(int startX, int startY, int startZ, int endX, int endY, int endZ)
            {
                _startX = startX;
                _startY = startY;
                _startZ = startZ;
                _endX = endX;
                _endY = endY;
                _endZ = endZ;
            }

            public static DirectObstructionCacheKey From(Vector3 start, Vector3 end)
            {
                var scale = 1f / DirectObstructionCacheResolution;
                return new DirectObstructionCacheKey(
                    Mathf.RoundToInt(start.x * scale),
                    Mathf.RoundToInt(start.y * scale),
                    Mathf.RoundToInt(start.z * scale),
                    Mathf.RoundToInt(end.x * scale),
                    Mathf.RoundToInt(end.y * scale),
                    Mathf.RoundToInt(end.z * scale));
            }

            public bool Equals(DirectObstructionCacheKey other)
            {
                return _startX == other._startX &&
                       _startY == other._startY &&
                       _startZ == other._startZ &&
                       _endX == other._endX &&
                       _endY == other._endY &&
                       _endZ == other._endZ;
            }

            public override bool Equals(object obj)
            {
                return obj is DirectObstructionCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _startX;
                    hash = (hash * 397) ^ _startY;
                    hash = (hash * 397) ^ _startZ;
                    hash = (hash * 397) ^ _endX;
                    hash = (hash * 397) ^ _endY;
                    hash = (hash * 397) ^ _endZ;
                    return hash;
                }
            }
        }

        private readonly struct PositionCacheKey : IEquatable<PositionCacheKey>
        {
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;

            private PositionCacheKey(int x, int y, int z)
            {
                _x = x;
                _y = y;
                _z = z;
            }

            public static PositionCacheKey From(Vector3 position)
            {
                return new PositionCacheKey(
                    BitConverter.SingleToInt32Bits(position.x),
                    BitConverter.SingleToInt32Bits(position.y),
                    BitConverter.SingleToInt32Bits(position.z));
            }

            public bool Equals(PositionCacheKey other)
            {
                return _x == other._x && _y == other._y && _z == other._z;
            }

            public override bool Equals(object obj)
            {
                return obj is PositionCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _x;
                    hash = (hash * 397) ^ _y;
                    hash = (hash * 397) ^ _z;
                    return hash;
                }
            }
        }

        private readonly struct RoomEstimateCacheKey : IEquatable<RoomEstimateCacheKey>
        {
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;
            private readonly int _radiusBucket;

            private RoomEstimateCacheKey(int x, int y, int z, int radiusBucket)
            {
                _x = x;
                _y = y;
                _z = z;
                _radiusBucket = radiusBucket;
            }

            public static RoomEstimateCacheKey From(Vector3 position, float radius)
            {
                var scale = 1f / RoomEstimateCacheResolution;
                return new RoomEstimateCacheKey(
                    Mathf.RoundToInt(position.x * scale),
                    Mathf.RoundToInt(position.y * scale),
                    Mathf.RoundToInt(position.z * scale),
                    Mathf.RoundToInt(radius * scale));
            }

            public bool Equals(RoomEstimateCacheKey other)
            {
                return _x == other._x &&
                       _y == other._y &&
                       _z == other._z &&
                       _radiusBucket == other._radiusBucket;
            }

            public override bool Equals(object obj)
            {
                return obj is RoomEstimateCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _x;
                    hash = (hash * 397) ^ _y;
                    hash = (hash * 397) ^ _z;
                    hash = (hash * 397) ^ _radiusBucket;
                    return hash;
                }
            }
        }

        private enum ManagedPathSearchOutcome : byte
        {
            Success,
            Exhausted,
            BudgetExceeded
        }

        private sealed class SpatialPathListPool : IDisposable
        {
            private static readonly Stack<List<Vector3>> Pool = new();

            private SpatialPathListPool(List<Vector3> value)
            {
                Value = value;
            }

            public List<Vector3> Value { get; }

            public void Dispose()
            {
                Value.Clear();
                Pool.Push(Value);
            }

            public static SpatialPathListPool Get()
            {
                return new SpatialPathListPool(Pool.Count > 0 ? Pool.Pop() : new List<Vector3>(64));
            }
        }

        private sealed class SpatialNodePathListPool : IDisposable
        {
            private static readonly Stack<List<int>> Pool = new();

            private SpatialNodePathListPool(List<int> value)
            {
                Value = value;
            }

            public List<int> Value { get; }

            public void Dispose()
            {
                Value.Clear();
                Pool.Push(Value);
            }

            public static SpatialNodePathListPool Get()
            {
                return new SpatialNodePathListPool(Pool.Count > 0 ? Pool.Pop() : new List<int>(64));
            }
        }
    }
}