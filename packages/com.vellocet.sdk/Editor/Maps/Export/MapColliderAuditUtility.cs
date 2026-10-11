// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapColliderAuditUtility
    {
        private const int ExpensiveStaticMeshTriangleThreshold = 256;

        internal static bool Audit(Scene scene, out string message)
        {
            message = string.Empty;
            if (!scene.IsValid())
            {
                message = "Compiled scene is invalid.";
                return false;
            }

            var colliders = CollectEnabledColliders(scene);
            if (colliders.Count == 0)
            {
                message = "No enabled colliders were found in the compiled scene.";
                return true;
            }

            var summary = new Summary(colliders.Count);
            var invalidDynamicMeshColliders = new List<string>(4);

            foreach (var collider in colliders)
            {
                if (!collider)
                    continue;

                if (HasNegativeLossyScale(collider.transform))
                    summary.NegativeScaleColliders++;

                if (collider is MeshCollider meshCollider)
                {
                    summary.MeshColliders++;

                    if (meshCollider.isTrigger)
                        summary.TriggerMeshColliders++;

                    if (meshCollider.attachedRigidbody && !meshCollider.convex)
                        if (invalidDynamicMeshColliders.Count < 3)
                            invalidDynamicMeshColliders.Add(BuildObjectPath(meshCollider.transform));

                    if (!meshCollider.attachedRigidbody &&
                        meshCollider.gameObject.isStatic &&
                        !meshCollider.convex &&
                        !meshCollider.isTrigger)
                    {
                        summary.StaticNonConvexMeshColliders++;

                        var sharedMesh = meshCollider.sharedMesh;
                        if (sharedMesh && (sharedMesh.triangles?.Length ?? 0) / 3 >=
                            ExpensiveStaticMeshTriangleThreshold)
                            summary.ExpensiveStaticMeshColliders++;
                    }

                    continue;
                }

                summary.PrimitiveColliders++;
            }

            if (invalidDynamicMeshColliders.Count > 0)
            {
                var plural = invalidDynamicMeshColliders.Count == 1 ? string.Empty : "s";
                message =
                    $"Collider audit failed: found {invalidDynamicMeshColliders.Count} Rigidbody MeshCollider{plural} that are still non-convex: {string.Join(", ", invalidDynamicMeshColliders)}.";
                return false;
            }

            message = summary.BuildMessage();
            return true;
        }

        private static List<Collider> CollectEnabledColliders(Scene scene)
        {
            return scene.GetRootGameObjects()
                .Where(root => root)
                .SelectMany(root => root.GetComponentsInChildren<Collider>(true))
                .Where(collider => collider && collider.enabled)
                .ToList();
        }

        private static bool HasNegativeLossyScale(Transform transform)
        {
            if (!transform)
                return false;

            var scale = transform.lossyScale;
            return scale.x < 0f || scale.y < 0f || scale.z < 0f;
        }

        private static string BuildObjectPath(Transform transform)
        {
            if (!transform)
                return "<missing>";

            var parts = new Stack<string>();
            var current = transform;
            while (current)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }

        private struct Summary
        {
            internal Summary(int scanned)
            {
                Scanned = scanned;
                MeshColliders = 0;
                PrimitiveColliders = 0;
                StaticNonConvexMeshColliders = 0;
                ExpensiveStaticMeshColliders = 0;
                NegativeScaleColliders = 0;
                TriggerMeshColliders = 0;
            }

            internal readonly int Scanned;
            internal int MeshColliders;
            internal int PrimitiveColliders;
            internal int StaticNonConvexMeshColliders;
            internal int ExpensiveStaticMeshColliders;
            internal int NegativeScaleColliders;
            internal int TriggerMeshColliders;

            internal string BuildMessage()
            {
                return
                    $"Collider audit scanned {Scanned} enabled collider(s): " +
                    $"{MeshColliders} MeshCollider(s), " +
                    $"{PrimitiveColliders} primitive collider(s), " +
                    $"{StaticNonConvexMeshColliders} static non-convex mesh collider(s) left, " +
                    $"{ExpensiveStaticMeshColliders} expensive static mesh collider(s), " +
                    $"{NegativeScaleColliders} collider(s) on negative scale, " +
                    $"{TriggerMeshColliders} trigger mesh collider(s).";
            }
        }
    }
}
#endif