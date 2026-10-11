// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Collections.Generic;
using UnityEngine;

namespace Vellocet.Sdk.Spatial
{
    /// <summary>
    ///     Marks a collider hierarchy as runtime-moving spatial obstruction. Marked colliders are excluded from the
    ///     immutable bake and applied through the graph's live blocked-leaf overlay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialDynamicOccluder : MonoBehaviour
    {
        public void AppendColliders(List<Collider> output)
        {
            if (output == null)
                return;

            var colliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                if (colliders[i])
                    output.Add(colliders[i]);
        }
    }
}