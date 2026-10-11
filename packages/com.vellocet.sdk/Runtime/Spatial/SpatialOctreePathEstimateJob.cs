// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using Unity.Collections;
using Unity.Jobs;

namespace Vellocet.Sdk.Spatial
{
    public struct SpatialOctreePathEstimateJob : IDisposable
    {
        private NativeArray<float> _gScores;
        private NativeArray<float> _fScores;
        private NativeArray<int> _cameFrom;
        private NativeArray<int> _searchIds;
        private NativeArray<int> _closedIds;
        private NativeArray<int> _open;
        private NativeArray<int> _heapIndices;
        private NativeArray<int> _regionCameFrom;
        private NativeArray<int> _regionVisitIds;
        private NativeArray<int> _regionAllowedIds;
        private NativeArray<int> _regionQueue;
        private NativeArray<SpatialOctreePathQuery> _ownedQueries;
        private SpatialOctreeGraph _scratchOwner;

        internal SpatialOctreePathEstimateJob(
            JobHandle handle,
            NativeArray<SpatialOctreePathResult> results,
            NativeArray<float> gScores,
            NativeArray<float> fScores,
            NativeArray<int> cameFrom,
            NativeArray<int> searchIds,
            NativeArray<int> closedIds,
            NativeArray<int> open,
            NativeArray<int> heapIndices,
            NativeArray<int> regionCameFrom,
            NativeArray<int> regionVisitIds,
            NativeArray<int> regionAllowedIds,
            NativeArray<int> regionQueue,
            SpatialOctreeGraph scratchOwner)
        {
            Handle = handle;
            Results = results;
            _gScores = gScores;
            _fScores = fScores;
            _cameFrom = cameFrom;
            _searchIds = searchIds;
            _closedIds = closedIds;
            _open = open;
            _heapIndices = heapIndices;
            _regionCameFrom = regionCameFrom;
            _regionVisitIds = regionVisitIds;
            _regionAllowedIds = regionAllowedIds;
            _regionQueue = regionQueue;
            _ownedQueries = default;
            _scratchOwner = scratchOwner;
        }

        public JobHandle Handle { get; private set; }
        public NativeArray<SpatialOctreePathResult> Results { get; private set; }

        public void Complete()
        {
            Handle.Complete();
        }

        internal void TakeQueryOwnership(NativeArray<SpatialOctreePathQuery> queries)
        {
            _ownedQueries = queries;
        }

        public void Dispose()
        {
            Complete();
            if (Results.IsCreated)
                Results.Dispose();
            if (_scratchOwner != null)
            {
                _scratchOwner.ReleaseSharedPathScratch();
                _scratchOwner = null;
            }
            else
            {
                if (_gScores.IsCreated)
                    _gScores.Dispose();
                if (_fScores.IsCreated)
                    _fScores.Dispose();
                if (_cameFrom.IsCreated)
                    _cameFrom.Dispose();
                if (_searchIds.IsCreated)
                    _searchIds.Dispose();
                if (_closedIds.IsCreated)
                    _closedIds.Dispose();
                if (_open.IsCreated)
                    _open.Dispose();
                if (_heapIndices.IsCreated)
                    _heapIndices.Dispose();
                if (_regionCameFrom.IsCreated)
                    _regionCameFrom.Dispose();
                if (_regionVisitIds.IsCreated)
                    _regionVisitIds.Dispose();
                if (_regionAllowedIds.IsCreated)
                    _regionAllowedIds.Dispose();
                if (_regionQueue.IsCreated)
                    _regionQueue.Dispose();
            }

            if (_ownedQueries.IsCreated)
                _ownedQueries.Dispose();
            Results = default;
            _gScores = default;
            _fScores = default;
            _cameFrom = default;
            _searchIds = default;
            _closedIds = default;
            _open = default;
            _heapIndices = default;
            _regionCameFrom = default;
            _regionVisitIds = default;
            _regionAllowedIds = default;
            _regionQueue = default;
            _ownedQueries = default;
            Handle = default;
        }
    }
}