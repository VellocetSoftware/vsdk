// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;

namespace Vellocet.Sdk.Spatial
{
    /// <summary>
    ///     High-level read-only facade for systems that need approximate static-world spatial reasoning.
    ///     Systems use this as the single static-world authority; exact physics stays in
    ///     hit-scan/projectile/collider-identity systems that require contact data.
    /// </summary>
    public static class StaticWorldSpatialService
    {
        public static bool IsAvailable => SpatialGraphService.TryGetCurrentGraph(out _);

        public static bool TryEstimateStaticVisibility(
            Vector3 from,
            Vector3 to,
            out StaticWorldVisibilityEstimate estimate)
        {
            estimate = default;
            if (!SpatialGraphService.TryGetCurrentGraph(out var graph))
                return false;

            var result = graph.EstimateRoute(from, to);
            if (!graph.TryHasDirectTraversableLine(from, to, out var directVisible))
                return false;

            estimate = new StaticWorldVisibilityEstimate(result, directVisible);
            return true;
        }

        public static bool HasStaticVisibility(Vector3 from, Vector3 to)
        {
            return TryHasDirectStaticVisibility(from, to, out var visible) && visible;
        }

        public static bool TryHasDirectStaticVisibility(Vector3 from, Vector3 to, out bool visible)
        {
            visible = false;
            return SpatialGraphService.TryGetCurrentGraph(out var graph) &&
                   graph.TryHasDirectTraversableLine(from, to, out visible);
        }

        public static bool TryEstimateRoute(Vector3 from, Vector3 to, out SpatialOctreePathResult result)
        {
            result = default;
            if (!SpatialGraphService.TryGetCurrentGraph(out var graph))
                return false;

            result = graph.EstimateRoute(from, to);
            return true;
        }

        public static bool TryGetRoomSize(Vector3 position, float probeDistance, out float roomSize)
        {
            roomSize = 1f;
            return SpatialGraphService.TryGetCurrentGraph(out var graph) &&
                   graph.TryEstimateRoomSize(position, probeDistance, out roomSize);
        }

        public static bool TryGetComponentId(Vector3 position, out int componentId)
        {
            componentId = -1;
            return SpatialGraphService.TryGetCurrentGraph(out var graph) &&
                   graph.TryGetComponentId(position, out componentId);
        }

        public static bool TryGetLeafId(Vector3 position, out int leafId)
        {
            leafId = -1;
            return SpatialGraphService.TryGetCurrentGraph(out var graph) &&
                   graph.TryFindClosestTraversableLeaf(position, out leafId);
        }

        public static bool TryGetContainingTraversableLocation(
            Vector3 position,
            out int leafId,
            out int componentId)
        {
            leafId = -1;
            componentId = -1;
            return SpatialGraphService.TryGetCurrentGraph(out var graph) &&
                   graph.TryGetContainingTraversableLocation(position, out leafId, out componentId);
        }

        public static bool TryGetNearestTraversable(Vector3 position, out Vector3 nearestPosition)
        {
            nearestPosition = position;
            return SpatialGraphService.TryGetCurrentGraph(out var graph) &&
                   graph.TryGetNearestTraversablePosition(position, out nearestPosition);
        }
    }

    /// <summary>
    ///     Physics-backed visibility helpers for gameplay checks that must account for dynamic blockers.
    /// </summary>
    public static class PhysicsVisibilityUtility
    {
        private const int LineOfSightHitBufferSize = 32;

        private static readonly RaycastHit[] LineOfSightHits = new RaycastHit[LineOfSightHitBufferSize];

        public static bool HasClearLineOfSight(Vector3 origin, Vector3 targetPoint, LayerMask blockerMask,
            Transform ignoredRootA = null, Transform ignoredRootB = null)
        {
            var delta = targetPoint - origin;
            var distance = delta.magnitude;
            if (distance <= 0.01f)
                return true;

            return HasClearLineOfSight(origin, delta / distance, distance, blockerMask, ignoredRootA, ignoredRootB);
        }

        public static bool HasClearLineOfSight(Vector3 origin, Vector3 direction, float distance,
            LayerMask blockerMask, Transform ignoredRootA = null, Transform ignoredRootB = null)
        {
            if (distance <= 0.01f)
                return true;

            if (direction.sqrMagnitude <= 0.0001f)
                return true;

            if (blockerMask == 0)
                return true;

            direction.Normalize();
            var hitCount = Physics.RaycastNonAlloc(origin, direction, LineOfSightHits, distance, blockerMask,
                QueryTriggerInteraction.Ignore);
            if (HasBlockingLineOfSightHit(LineOfSightHits, hitCount, ignoredRootA, ignoredRootB))
                return false;

            if (hitCount < LineOfSightHits.Length)
                return true;

            var overflowHits = Physics.RaycastAll(origin, direction, distance, blockerMask,
                QueryTriggerInteraction.Ignore);
            return !HasBlockingLineOfSightHit(overflowHits, overflowHits.Length, ignoredRootA, ignoredRootB);
        }

        private static bool HasBlockingLineOfSightHit(RaycastHit[] hits, int hitCount, Transform ignoredRootA,
            Transform ignoredRootB)
        {
            for (var i = 0; i < hitCount; i++)
            {
                var hitCollider = hits[i].collider;
                if (!hitCollider || IsIgnoredLineOfSightHit(hitCollider, ignoredRootA, ignoredRootB))
                    continue;

                return true;
            }

            return false;
        }

        private static bool IsIgnoredLineOfSightHit(Collider hitCollider, Transform ignoredRootA,
            Transform ignoredRootB)
        {
            if (!hitCollider)
                return true;

            var hitTransform = hitCollider.transform;
            if (!hitTransform)
                return true;

            return IsSameOrChildOf(hitTransform, ignoredRootA) ||
                   IsSameOrChildOf(hitTransform, ignoredRootB);
        }

        private static bool IsSameOrChildOf(Transform candidate, Transform root)
        {
            return root && candidate && (candidate == root || candidate.IsChildOf(root));
        }
    }
}