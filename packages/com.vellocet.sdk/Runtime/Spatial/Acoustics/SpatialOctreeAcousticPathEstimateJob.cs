// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Vellocet.Sdk.Spatial.Acoustics
{
    [BurstCompile]
    internal struct SpatialOctreeAcousticPathEstimateJob : IJob
    {
        private const float ClosestLeafVerticalPenalty = 3.5f;
        private const float ClosestLeafUpwardPenalty = 2f;
        private const float ClosestLeafNodeSizePenalty = 0.0025f;
        private const float ExteriorPathPenaltyScale = 5f;
        private const float PortalConstrictionPenaltyScale = 6f;
        private const int MinimumCorridorSearchBudget = 2048;

        [ReadOnly] public NativeArray<SpatialOctreePathQuery> Queries;
        [ReadOnly] public NativeArray<SpatialOctreeAcousticNodeJobData> Nodes;
        [ReadOnly] public NativeArray<int> TraversableNodeIndices;
        [ReadOnly] public NativeArray<int> NeighborStarts;
        [ReadOnly] public NativeArray<int> NeighborIndices;
        [ReadOnly] public NativeArray<float> NeighborApertures;
        [ReadOnly] public NativeArray<byte> DynamicBlockedNodes;
        [ReadOnly] public NativeArray<SpatialOctreeRoutingRegionData> RoutingRegions;
        [ReadOnly] public NativeArray<int> RoutingNeighborStarts;
        [ReadOnly] public NativeArray<int> RoutingNeighborIndices;

        [WriteOnly] public NativeArray<SpatialOctreePathResult> Results;

        public NativeArray<float> GScores;
        public NativeArray<float> FScores;
        public NativeArray<int> CameFrom;
        public NativeArray<int> SearchIds;
        public NativeArray<int> ClosedIds;
        public NativeArray<int> Open;
        public NativeArray<int> HeapIndices;
        public NativeArray<int> RegionCameFrom;
        public NativeArray<int> RegionVisitIds;
        public NativeArray<int> RegionAllowedIds;
        public NativeArray<int> RegionQueue;
        public int QueryCount;
        public int SearchIdBase;
        public int MaxPathSearchIterations;
        public int DynamicBlockedCount;

        public void Execute()
        {
            for (var queryIndex = 0; queryIndex < QueryCount; queryIndex++)
                ExecuteQuery(queryIndex);
        }

        private void ExecuteQuery(int queryIndex)
        {
            var query = Queries[queryIndex];
            var direct = Vector3.Distance(query.From, query.To);
            var hasStartNode = TryFindClosestTraversableLeaf(query.From, out var startNode);
            var hasEndNode = TryFindClosestTraversableLeaf(query.To, out var endNode);
            if (!hasStartNode || !hasEndNode)
            {
                var reason = !hasStartNode
                    ? SpatialOctreePathFailureReason.InvalidStart
                    : SpatialOctreePathFailureReason.InvalidEnd;
                Results[queryIndex] = SpatialOctreePathResult.CreateFailure(direct, startNode, endNode,
                    HasSameComponent(startNode, endNode), reason);
                return;
            }

            var sameComponent = HasSameComponent(startNode, endNode);
            if (!sameComponent)
            {
                Results[queryIndex] = SpatialOctreePathResult.CreateFailure(direct, startNode, endNode, false,
                    SpatialOctreePathFailureReason.DifferentComponent);
                return;
            }

            var fromPoint = ClosestPoint(Nodes[startNode], query.From);
            var toPoint = ClosestPoint(Nodes[endNode], query.To);
            direct = Vector3.Distance(fromPoint, toPoint);
            var firstSearchId = SearchIdBase + queryIndex * 3 + 1;
            var corridorId = firstSearchId;
            var hasCorridor = TryBuildRoutingCorridor(startNode, endNode, corridorId);
            var totalIterations = 0;
            var corridorBudget = Mathf.Min(MaxPathSearchIterations,
                Mathf.Max(MinimumCorridorSearchBudget, MaxPathSearchIterations / 4));

            var outcome = PathSearchOutcome.Exhausted;
            var nodePathLength = 0f;
            var waypointCount = 0;
            var exteriorNodeCount = 0;
            var averageOpenness = 0f;
            var minimumAperture = 0f;
            var transitionNodeCount = 0;
            if (hasCorridor)
            {
                outcome = TryFindPathLength(startNode, endNode, fromPoint, toPoint, firstSearchId + 1,
                    corridorBudget, corridorId, true, out nodePathLength, out waypointCount,
                    out exteriorNodeCount, out averageOpenness, out minimumAperture, out transitionNodeCount,
                    out var corridorIterations);
                totalIterations += corridorIterations;
            }

            if (outcome != PathSearchOutcome.Success)
            {
                var remainingBudget = Mathf.Max(0, MaxPathSearchIterations - totalIterations);
                if (remainingBudget > 0)
                {
                    outcome = TryFindPathLength(startNode, endNode, fromPoint, toPoint, firstSearchId + 2,
                        remainingBudget, corridorId, false, out nodePathLength, out waypointCount,
                        out exteriorNodeCount, out averageOpenness, out minimumAperture,
                        out transitionNodeCount, out var fallbackIterations);
                    totalIterations += fallbackIterations;
                }
                else
                {
                    outcome = PathSearchOutcome.BudgetExceeded;
                }
            }

            if (outcome != PathSearchOutcome.Success)
            {
                var reason = outcome == PathSearchOutcome.BudgetExceeded
                    ? SpatialOctreePathFailureReason.BudgetExceeded
                    : DynamicBlockedCount > 0
                        ? SpatialOctreePathFailureReason.DynamicObstruction
                        : SpatialOctreePathFailureReason.Unreachable;
                Results[queryIndex] = SpatialOctreePathResult.CreateFailure(direct, startNode, endNode,
                    sameComponent, reason, totalIterations);
                return;
            }

            var pathLength = Mathf.Max(direct, nodePathLength);
            var excess = Mathf.Max(0f, pathLength - direct);
            var ratio = direct > 0.001f ? Mathf.Clamp01(excess / direct) : 0f;
            Results[queryIndex] = new SpatialOctreePathResult
            {
                Success = 1,
                StartNode = startNode,
                EndNode = endNode,
                SameComponent = 1,
                DirectDistance = direct,
                PathLength = pathLength,
                RouteExcessRatio = ratio,
                WaypointCount = waypointCount,
                ExteriorNodeCount = exteriorNodeCount,
                AverageOpenness = averageOpenness,
                MinimumAperture = minimumAperture,
                TransitionNodeCount = transitionNodeCount,
                StartExterior = Nodes[startNode].IsExterior,
                EndExterior = Nodes[endNode].IsExterior,
                StartTransition = IsTransitionNode(startNode) ? (byte)1 : (byte)0,
                EndTransition = IsTransitionNode(endNode) ? (byte)1 : (byte)0,
                FailureReason = SpatialOctreePathFailureReason.None,
                SearchIterations = totalIterations
            };
        }

        private bool TryBuildRoutingCorridor(int startNode, int endNode, int corridorId)
        {
            var startRegion = Nodes[startNode].RoutingRegionId;
            var endRegion = Nodes[endNode].RoutingRegionId;
            if ((uint)startRegion >= (uint)RoutingRegions.Length ||
                (uint)endRegion >= (uint)RoutingRegions.Length ||
                RegionQueue.Length < RoutingRegions.Length)
                return false;

            var read = 0;
            var write = 0;
            RegionQueue[write++] = startRegion;
            RegionVisitIds[startRegion] = corridorId;
            RegionCameFrom[startRegion] = -1;
            var found = startRegion == endRegion;
            while (read < write && !found)
            {
                var current = RegionQueue[read++];
                var start = RoutingNeighborStarts[current];
                var end = RoutingNeighborStarts[current + 1];
                for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    var neighbor = RoutingNeighborIndices[edgeIndex];
                    if ((uint)neighbor >= (uint)RoutingRegions.Length ||
                        RegionVisitIds[neighbor] == corridorId ||
                        RoutingRegions[neighbor].ComponentId != RoutingRegions[startRegion].ComponentId)
                        continue;

                    RegionVisitIds[neighbor] = corridorId;
                    RegionCameFrom[neighbor] = current;
                    RegionQueue[write++] = neighbor;
                    if (neighbor == endRegion)
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
                return false;

            var region = endRegion;
            while (region >= 0)
            {
                MarkAllowedRegionAndNeighbors(region, corridorId);
                region = RegionCameFrom[region];
            }

            return true;
        }

        private void MarkAllowedRegionAndNeighbors(int region, int corridorId)
        {
            RegionAllowedIds[region] = corridorId;
            var start = RoutingNeighborStarts[region];
            var end = RoutingNeighborStarts[region + 1];
            for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                var neighbor = RoutingNeighborIndices[edgeIndex];
                if ((uint)neighbor < (uint)RegionAllowedIds.Length)
                    RegionAllowedIds[neighbor] = corridorId;
            }
        }

        private bool TryFindClosestTraversableLeaf(Vector3 position, out int nodeIndex)
        {
            if (TryFindContainingLeaf(position, out nodeIndex) && IsNodeTraversable(nodeIndex))
                return true;

            nodeIndex = -1;
            var bestDistance = float.PositiveInfinity;
            FixedList512Bytes<int> stack = default;
            stack.Add(0);
            while (stack.Length > 0)
            {
                var stackIndex = stack.Length - 1;
                var candidateIndex = stack[stackIndex];
                stack.RemoveAt(stackIndex);
                if ((uint)candidateIndex >= (uint)Nodes.Length)
                    continue;

                var candidate = Nodes[candidateIndex];
                if (ScoreBoundsDistance(candidate, position) >= bestDistance)
                    continue;
                if (candidate.IsLeaf == 0)
                {
                    var firstChild = candidate.FirstChildIndex;
                    if (firstChild < 0)
                        continue;

                    var endChild = Mathf.Min(firstChild + candidate.ChildCount, Nodes.Length);
                    if (stack.Length + endChild - firstChild > stack.Capacity)
                        return TryFindClosestTraversableLeafLinear(position, out nodeIndex);

                    var preferredChild = firstChild;
                    if (position.x >= candidate.Center.x)
                        preferredChild += 1;
                    if (position.y >= candidate.Center.y)
                        preferredChild += 2;
                    if (position.z >= candidate.Center.z)
                        preferredChild += 4;
                    for (var childIndex = firstChild; childIndex < endChild; childIndex++)
                        if (childIndex != preferredChild)
                            stack.Add(childIndex);
                    if ((uint)preferredChild < (uint)endChild)
                        stack.Add(preferredChild);
                    continue;
                }

                if (!IsNodeTraversable(candidateIndex))
                    continue;

                var score = ScoreTraversableCandidate(candidate, position);
                if (score >= bestDistance)
                    continue;

                bestDistance = score;
                nodeIndex = candidateIndex;
            }

            return nodeIndex >= 0;
        }

        private bool TryFindClosestTraversableLeafLinear(Vector3 position, out int nodeIndex)
        {
            nodeIndex = -1;
            var bestDistance = float.PositiveInfinity;
            for (var traversableIndex = 0; traversableIndex < TraversableNodeIndices.Length; traversableIndex++)
            {
                var candidateIndex = TraversableNodeIndices[traversableIndex];
                if (!IsNodeTraversable(candidateIndex))
                    continue;

                var score = ScoreTraversableCandidate(Nodes[candidateIndex], position);
                if (score >= bestDistance)
                    continue;

                bestDistance = score;
                nodeIndex = candidateIndex;
            }

            return nodeIndex >= 0;
        }

        private bool TryFindContainingLeaf(Vector3 position, out int nodeIndex)
        {
            nodeIndex = -1;
            if (Nodes.Length == 0 || !Contains(Nodes[0], position))
                return false;

            var current = 0;
            while ((uint)current < (uint)Nodes.Length)
            {
                var node = Nodes[current];
                if (node.IsLeaf != 0)
                {
                    nodeIndex = current;
                    return true;
                }

                var firstChild = node.FirstChildIndex;
                if (firstChild < 0)
                    return false;

                var endChild = firstChild + node.ChildCount;
                var preferredChild = firstChild;
                if (position.x >= node.Center.x)
                    preferredChild += 1;
                if (position.y >= node.Center.y)
                    preferredChild += 2;
                if (position.z >= node.Center.z)
                    preferredChild += 4;

                if ((uint)preferredChild < (uint)Nodes.Length && preferredChild < endChild &&
                    Contains(Nodes[preferredChild], position))
                {
                    current = preferredChild;
                    continue;
                }

                var foundChild = -1;
                for (var child = firstChild; child < endChild && (uint)child < (uint)Nodes.Length; child++)
                    if (Contains(Nodes[child], position))
                    {
                        foundChild = child;
                        break;
                    }

                if (foundChild < 0)
                    return false;
                current = foundChild;
            }

            return false;
        }

        private PathSearchOutcome TryFindPathLength(
            int startNode,
            int endNode,
            Vector3 from,
            Vector3 to,
            int searchId,
            int iterationBudget,
            int corridorId,
            bool restrictToCorridor,
            out float pathLength,
            out int waypointCount,
            out int exteriorNodeCount,
            out float averageOpenness,
            out float minimumAperture,
            out int transitionNodeCount,
            out int iterations)
        {
            ResetPathOutputs(out pathLength, out waypointCount, out exteriorNodeCount, out averageOpenness,
                out minimumAperture, out transitionNodeCount);
            iterations = 0;
            if (iterationBudget <= 0)
                return PathSearchOutcome.BudgetExceeded;

            var openCount = 0;
            InitializeNode(startNode, searchId);
            var startSearchIndex = Nodes[startNode].TraversableIndex;
            GScores[startSearchIndex] = 0f;
            FScores[startSearchIndex] = Heuristic(startNode, endNode);
            Open[openCount++] = startNode;

            while (openCount > 0)
            {
                if (iterations >= iterationBudget)
                    return PathSearchOutcome.BudgetExceeded;
                iterations++;

                var current = PopLowestOpenNode(ref openCount);
                var currentSearchIndex = Nodes[current].TraversableIndex;
                if (ClosedIds[currentSearchIndex] == searchId)
                    continue;

                if (current == endNode)
                {
                    pathLength = ReconstructPathLength(current, from, to, startNode, endNode,
                        out waypointCount, out exteriorNodeCount, out averageOpenness, out minimumAperture,
                        out transitionNodeCount);
                    return PathSearchOutcome.Success;
                }

                ClosedIds[currentSearchIndex] = searchId;
                var neighborStart = NeighborStarts[current];
                var neighborEnd = NeighborStarts[current + 1];
                for (var edgeIndex = neighborStart; edgeIndex < neighborEnd; edgeIndex++)
                {
                    var neighbor = NeighborIndices[edgeIndex];
                    if (!IsNodeTraversable(neighbor))
                        continue;

                    var neighborSearchIndex = Nodes[neighbor].TraversableIndex;
                    if ((uint)neighborSearchIndex >= (uint)SearchIds.Length ||
                        ClosedIds[neighborSearchIndex] == searchId)
                        continue;
                    if (restrictToCorridor && !IsRegionAllowed(Nodes[neighbor].RoutingRegionId, corridorId))
                        continue;

                    if (SearchIds[neighborSearchIndex] != searchId)
                        InitializeNode(neighbor, searchId);

                    var tentative = GScores[currentSearchIndex] +
                                    ComputeTraversalEdgeCost(current, neighbor, edgeIndex, startNode, endNode);
                    if (tentative >= GScores[neighborSearchIndex])
                        continue;

                    CameFrom[neighborSearchIndex] = current;
                    GScores[neighborSearchIndex] = tentative;
                    FScores[neighborSearchIndex] = tentative + Heuristic(neighbor, endNode);
                    if (!TryPushOrUpdateOpenNode(ref openCount, neighbor))
                        return PathSearchOutcome.ScratchOverflow;
                }
            }

            return PathSearchOutcome.Exhausted;
        }

        private static void ResetPathOutputs(
            out float pathLength,
            out int waypointCount,
            out int exteriorNodeCount,
            out float averageOpenness,
            out float minimumAperture,
            out int transitionNodeCount)
        {
            pathLength = 0f;
            waypointCount = 0;
            exteriorNodeCount = 0;
            averageOpenness = 0f;
            minimumAperture = 0f;
            transitionNodeCount = 0;
        }

        private bool IsRegionAllowed(int regionId, int corridorId)
        {
            return (uint)regionId < (uint)RegionAllowedIds.Length && RegionAllowedIds[regionId] == corridorId;
        }

        private bool IsNodeTraversable(int nodeIndex)
        {
            return (uint)nodeIndex < (uint)Nodes.Length && Nodes[nodeIndex].IsTraversable != 0 &&
                   ((uint)nodeIndex >= (uint)DynamicBlockedNodes.Length || DynamicBlockedNodes[nodeIndex] == 0);
        }

        private void InitializeNode(int node, int searchId)
        {
            var searchIndex = Nodes[node].TraversableIndex;
            SearchIds[searchIndex] = searchId;
            ClosedIds[searchIndex] = 0;
            HeapIndices[searchIndex] = -1;
            GScores[searchIndex] = float.PositiveInfinity;
            FScores[searchIndex] = float.PositiveInfinity;
            CameFrom[searchIndex] = -1;
        }

        private bool TryPushOrUpdateOpenNode(ref int openCount, int node)
        {
            var searchIndex = Nodes[node].TraversableIndex;
            var heapIndex = HeapIndices[searchIndex];
            if (heapIndex >= 0 && heapIndex < openCount && Open[heapIndex] == node)
            {
                SiftOpenUp(heapIndex);
                return true;
            }

            if (openCount >= Open.Length)
                return false;

            heapIndex = openCount++;
            Open[heapIndex] = node;
            HeapIndices[searchIndex] = heapIndex;
            SiftOpenUp(heapIndex);
            return true;
        }

        private int PopLowestOpenNode(ref int openCount)
        {
            var bestNode = Open[0];
            HeapIndices[Nodes[bestNode].TraversableIndex] = -1;
            openCount--;
            if (openCount > 0)
            {
                var replacement = Open[openCount];
                Open[0] = replacement;
                HeapIndices[Nodes[replacement].TraversableIndex] = 0;
                SiftOpenDown(openCount, 0);
            }

            return bestNode;
        }

        private void SiftOpenUp(int index)
        {
            while (index > 0)
            {
                var parent = (index - 1) >> 1;
                var node = Open[index];
                var parentNode = Open[parent];
                if (!HasHigherOpenPriority(node, parentNode))
                    return;

                Open[index] = parentNode;
                HeapIndices[Nodes[parentNode].TraversableIndex] = index;
                Open[parent] = node;
                HeapIndices[Nodes[node].TraversableIndex] = parent;
                index = parent;
            }
        }

        private void SiftOpenDown(int openCount, int index)
        {
            while (true)
            {
                var left = index * 2 + 1;
                if (left >= openCount)
                    return;

                var right = left + 1;
                var best = left;
                if (right < openCount && HasHigherOpenPriority(Open[right], Open[left]))
                    best = right;

                var node = Open[index];
                var child = Open[best];
                if (!HasHigherOpenPriority(child, node))
                    return;

                Open[index] = child;
                HeapIndices[Nodes[child].TraversableIndex] = index;
                Open[best] = node;
                HeapIndices[Nodes[node].TraversableIndex] = best;
                index = best;
            }
        }

        private bool HasHigherOpenPriority(int a, int b)
        {
            var aIndex = Nodes[a].TraversableIndex;
            var bIndex = Nodes[b].TraversableIndex;
            var fA = FScores[aIndex];
            var fB = FScores[bIndex];
            if (!Mathf.Approximately(fA, fB))
                return fA < fB;
            return GScores[aIndex] > GScores[bIndex];
        }

        private float ReconstructPathLength(
            int current,
            Vector3 from,
            Vector3 to,
            int startNode,
            int endNode,
            out int waypointCount,
            out int exteriorNodeCount,
            out float averageOpenness,
            out float minimumAperture,
            out int transitionNodeCount)
        {
            waypointCount = 0;
            exteriorNodeCount = 0;
            averageOpenness = 0f;
            minimumAperture = 1f;
            transitionNodeCount = 0;
            while (current >= 0)
            {
                Open[waypointCount++] = current;
                averageOpenness += Mathf.Clamp01(Nodes[current].AcousticOpenness);
                if (Nodes[current].IsExterior != 0)
                    exteriorNodeCount++;
                if (IsTransitionNode(current))
                    transitionNodeCount++;
                current = CameFrom[Nodes[current].TraversableIndex];
            }

            if (waypointCount > 0)
                averageOpenness = Mathf.Clamp01(averageOpenness / waypointCount);
            if (waypointCount <= 1)
                return Vector3.Distance(from, to);

            var length = 0f;
            var previousPoint = from;
            for (var pathIndex = waypointCount - 1; pathIndex > 0; pathIndex--)
            {
                var fromNode = Open[pathIndex];
                var toNode = Open[pathIndex - 1];
                var edgeIndex = FindEdgeIndex(fromNode, toNode);
                var aperture = GetEdgeAperture(edgeIndex);
                minimumAperture = Mathf.Min(minimumAperture, aperture);
                var portalPoint = ResolvePortalPoint(fromNode, toNode, previousPoint);
                length += Vector3.Distance(previousPoint, portalPoint);
                length += Vector3.Distance(Nodes[fromNode].Center, Nodes[toNode].Center) *
                          (ComputePortalConstrictionPenalty(edgeIndex) +
                           ComputeExteriorPenalty(fromNode, toNode, startNode, endNode));
                previousPoint = portalPoint;
            }

            length += Vector3.Distance(previousPoint, to);
            return length;
        }

        private int FindEdgeIndex(int fromNode, int toNode)
        {
            var start = NeighborStarts[fromNode];
            var end = NeighborStarts[fromNode + 1];
            for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                if (NeighborIndices[edgeIndex] == toNode)
                    return edgeIndex;
            return -1;
        }

        private float GetEdgeAperture(int edgeIndex)
        {
            return (uint)edgeIndex < (uint)NeighborApertures.Length
                ? Mathf.Clamp01(NeighborApertures[edgeIndex])
                : 0f;
        }

        private float Heuristic(int a, int b)
        {
            return Vector3.Distance(Nodes[a].Center, Nodes[b].Center);
        }

        private float ComputeTraversalEdgeCost(int fromNode, int toNode, int edgeIndex, int startNode, int endNode)
        {
            var baseCost = Vector3.Distance(Nodes[fromNode].Center, Nodes[toNode].Center);
            return baseCost * (1f + ComputePortalConstrictionPenalty(edgeIndex) +
                               ComputeExteriorPenalty(fromNode, toNode, startNode, endNode));
        }

        private float ComputePortalConstrictionPenalty(int edgeIndex)
        {
            var constriction = 1f - GetEdgeAperture(edgeIndex);
            return constriction * constriction * PortalConstrictionPenaltyScale;
        }

        private float ComputeExteriorPenalty(int fromNode, int toNode, int startNode, int endNode)
        {
            if (Nodes[fromNode].IsExterior == 0 && Nodes[toNode].IsExterior == 0)
                return 0f;
            return Nodes[startNode].IsExterior != 0 && Nodes[endNode].IsExterior != 0
                ? 0f
                : ExteriorPathPenaltyScale;
        }

        private bool IsTransitionNode(int nodeIndex)
        {
            return (uint)nodeIndex < (uint)Nodes.Length &&
                   (Nodes[nodeIndex].AcousticFlags & SpatialOctreeNodeAcousticData.TransitionFlag) != 0;
        }

        private bool HasSameComponent(int a, int b)
        {
            if ((uint)a >= (uint)Nodes.Length || (uint)b >= (uint)Nodes.Length)
                return false;
            var componentId = Nodes[a].ComponentId;
            return componentId >= 0 && componentId == Nodes[b].ComponentId;
        }

        private static bool Contains(in SpatialOctreeAcousticNodeJobData node, Vector3 position)
        {
            var min = node.Center - node.Extents;
            var max = node.Center + node.Extents;
            return position.x >= min.x && position.x <= max.x &&
                   position.y >= min.y && position.y <= max.y &&
                   position.z >= min.z && position.z <= max.z;
        }

        private static float ScoreTraversableCandidate(in SpatialOctreeAcousticNodeJobData node, Vector3 position)
        {
            return ScoreBoundsDistance(node, position) +
                   (node.Extents * 2f).sqrMagnitude * ClosestLeafNodeSizePenalty;
        }

        private static float ScoreBoundsDistance(in SpatialOctreeAcousticNodeJobData node, Vector3 position)
        {
            var min = node.Center - node.Extents;
            var max = node.Center + node.Extents;
            var clamped = new Vector3(
                Mathf.Clamp(position.x, min.x, max.x),
                Mathf.Clamp(position.y, min.y, max.y),
                Mathf.Clamp(position.z, min.z, max.z));
            var delta = clamped - position;
            var horizontalDistanceSqr = delta.x * delta.x + delta.z * delta.z;
            var verticalDistanceSqr = delta.y * delta.y;
            var upwardDistanceSqr = delta.y > 0f ? verticalDistanceSqr : 0f;
            return horizontalDistanceSqr +
                   verticalDistanceSqr * ClosestLeafVerticalPenalty +
                   upwardDistanceSqr * ClosestLeafUpwardPenalty;
        }

        private Vector3 ResolvePortalPoint(int fromNode, int toNode, Vector3 previousPoint)
        {
            if (!TryGetPortalBounds(fromNode, toNode, out var portalMin, out var portalMax))
                return (Nodes[fromNode].Center + Nodes[toNode].Center) * 0.5f;
            return new Vector3(
                Mathf.Clamp(previousPoint.x, portalMin.x, portalMax.x),
                Mathf.Clamp(previousPoint.y, portalMin.y, portalMax.y),
                Mathf.Clamp(previousPoint.z, portalMin.z, portalMax.z));
        }

        private bool TryGetPortalBounds(int fromNode, int toNode, out Vector3 portalMin, out Vector3 portalMax)
        {
            portalMin = default;
            portalMax = default;
            if ((uint)fromNode >= (uint)Nodes.Length || (uint)toNode >= (uint)Nodes.Length)
                return false;
            var from = Nodes[fromNode];
            var to = Nodes[toNode];
            portalMin = Vector3.Max(from.Center - from.Extents, to.Center - to.Extents);
            portalMax = Vector3.Min(from.Center + from.Extents, to.Center + to.Extents);
            const float epsilon = 0.0001f;
            return portalMax.x + epsilon >= portalMin.x &&
                   portalMax.y + epsilon >= portalMin.y &&
                   portalMax.z + epsilon >= portalMin.z;
        }

        private static Vector3 ClosestPoint(in SpatialOctreeAcousticNodeJobData node, Vector3 position)
        {
            var min = node.Center - node.Extents;
            var max = node.Center + node.Extents;
            return new Vector3(
                Mathf.Clamp(position.x, min.x, max.x),
                Mathf.Clamp(position.y, min.y, max.y),
                Mathf.Clamp(position.z, min.z, max.z));
        }

        private enum PathSearchOutcome : byte
        {
            Success,
            Exhausted,
            BudgetExceeded,
            ScratchOverflow
        }
    }
}