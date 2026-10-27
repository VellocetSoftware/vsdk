// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using UnityEngine.AddressableAssets;
using Vellocet.Framework.Runtime.Spatial;

namespace Vellocet.Sdk.Maps
{
    [DisallowMultipleComponent]
    public sealed class MapRuntimeDependencies : MonoBehaviour
    {
        [SerializeField] private AssetReferenceT<SpatialOctreeAsset> spatialGraphReference;

        [SerializeField, HideInInspector] private bool hasPlayableBounds;
        [SerializeField, HideInInspector] private Bounds playableBounds;

        public bool TryGetPlayableBounds(out Bounds bounds)
        {
            bounds = playableBounds;
            return hasPlayableBounds && ValidPlayableBounds(bounds);
        }

        public static bool ValidPlayableBounds(Bounds bounds) => Finite(bounds.center) && Finite(bounds.size) && Finite(bounds.min) && Finite(bounds.max) &&
            bounds.size.x > 0 && bounds.size.y > 0 && bounds.size.z > 0;
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

#if UNITY_EDITOR
        public void SetPlayableBounds(Bounds? bounds)
        {
            if (bounds.HasValue && !ValidPlayableBounds(bounds.Value))
                throw new System.ArgumentException("Playable bounds must be finite and have a positive size.");
            hasPlayableBounds = bounds.HasValue;
            playableBounds = bounds ?? default;
        }
#endif

        public bool TryGetSpatialGraphReference(out AssetReference reference)
        {
            reference = spatialGraphReference;
            return spatialGraphReference != null && spatialGraphReference.RuntimeKeyIsValid();
        }
    }
}