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

        public bool TryGetSpatialGraphReference(out AssetReference reference)
        {
            reference = spatialGraphReference;
            return spatialGraphReference != null && spatialGraphReference.RuntimeKeyIsValid();
        }
    }
}