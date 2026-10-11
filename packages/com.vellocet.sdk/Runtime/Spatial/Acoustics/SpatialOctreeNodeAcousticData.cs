// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;

namespace Vellocet.Sdk.Spatial.Acoustics
{
    [Serializable]
    public struct SpatialOctreeNodeAcousticData
    {
        public const int ExteriorFlag = 1 << 0;
        public const int TransitionFlag = 1 << 1;
        public const int ThinBoundaryFlag = 1 << 2;

        public int Flags;
        public int ClusterId;
        [Range(0f, 1f)] public float Openness;
        [Range(0f, 1f)] public float Aperture;
        [Range(0f, 1f)] public float BoundaryProximity;

        public SpatialOctreeNodeAcousticData(int flags, int clusterId, float openness, float aperture,
            float boundaryProximity)
        {
            Flags = flags;
            ClusterId = clusterId;
            Openness = Mathf.Clamp01(openness);
            Aperture = Mathf.Clamp01(aperture);
            BoundaryProximity = Mathf.Clamp01(boundaryProximity);
        }

        public bool IsExterior => (Flags & ExteriorFlag) != 0;
        public bool IsTransition => (Flags & TransitionFlag) != 0;
        public bool HasThinBoundary => (Flags & ThinBoundaryFlag) != 0;
    }
}