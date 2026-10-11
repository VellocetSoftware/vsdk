// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Spatial
{
    internal static class SpatialDynamicOccluderCollection
    {
        internal static List<Collider> Collect(Scene scene, SpatialOctreeBakeSettings settings)
        {
            var output = new List<Collider>();
            if (!scene.IsValid() || !scene.isLoaded)
                return output;

            settings = settings.Sanitize();
            var seen = new HashSet<int>();
            var scratch = new List<Collider>();
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var root = roots[rootIndex];
                if (!root)
                    continue;

                var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                    if (behaviours[behaviourIndex] is IMovingBrushEntityMarker marker && marker.BrushTransform)
                        AppendHierarchy(marker.BrushTransform, output, seen, settings);

                var explicitOccluders = root.GetComponentsInChildren<SpatialDynamicOccluder>(true);
                for (var occluderIndex = 0; occluderIndex < explicitOccluders.Length; occluderIndex++)
                {
                    scratch.Clear();
                    explicitOccluders[occluderIndex].AppendColliders(scratch);
                    for (var colliderIndex = 0; colliderIndex < scratch.Count; colliderIndex++)
                        TryAppend(scratch[colliderIndex], output, seen, settings);
                }

                var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
                for (var bodyIndex = 0; bodyIndex < rigidbodies.Length; bodyIndex++)
                {
                    var body = rigidbodies[bodyIndex];
                    if (body)
                        AppendHierarchy(body.transform, output, seen, settings);
                }
            }

            return output;
        }

        private static void AppendHierarchy(Transform root, List<Collider> output, HashSet<int> seen,
            SpatialOctreeBakeSettings settings)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                TryAppend(colliders[colliderIndex], output, seen, settings);
        }

        private static void TryAppend(Collider collider, List<Collider> output, HashSet<int> seen,
            SpatialOctreeBakeSettings settings)
        {
            if (!collider || !seen.Add(collider.GetInstanceID()) || collider.isTrigger ||
                (settings.OccluderMask.value & (1 << collider.gameObject.layer)) == 0)
                return;

            output.Add(collider);
        }
    }
}