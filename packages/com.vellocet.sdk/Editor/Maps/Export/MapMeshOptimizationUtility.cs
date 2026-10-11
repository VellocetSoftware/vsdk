// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vertex.Unity;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapMeshOptimizationUtility
    {
        internal static bool Optimize(Scene scene, out string message)
        {
            message = string.Empty;
            if (!scene.IsValid())
            {
                message = "Compiled scene is invalid.";
                return false;
            }

            var meshColliders = CollectMeshColliders(scene);
            if (meshColliders.Count == 0)
            {
                message = "No MeshCollider components required optimization.";
                return true;
            }

            var summary = new Summary(meshColliders.Count);
            var dirty = false;

            foreach (var meshCollider in meshColliders)
            {
                if (!meshCollider)
                    continue;

                var outcome = OptimizeMeshCollider(meshCollider);
                switch (outcome.Kind)
                {
                    case OutcomeKind.ForcedConvex:
                        summary.ForcedConvex++;
                        dirty = true;
                        break;
                    case OutcomeKind.ReplacedWithBox:
                        summary.ReplacedWithBox++;
                        dirty = true;
                        break;
                    case OutcomeKind.Unchanged:
                        summary.Unchanged++;
                        break;
                    default:
                        message = outcome.Message;
                        return false;
                }
            }

            if (dirty)
                EditorSceneManager.MarkSceneDirty(scene);

            message = summary.BuildMessage();
            return true;
        }

        private static List<MeshCollider> CollectMeshColliders(Scene scene)
        {
            var result = new List<MeshCollider>(64);
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (!root)
                    continue;

                result.AddRange(root.GetComponentsInChildren<MeshCollider>(true));
            }

            return result;
        }

        private static Outcome OptimizeMeshCollider(MeshCollider meshCollider)
        {
            var objectPath = BuildObjectPath(meshCollider.transform);
            var rigidbody = meshCollider.attachedRigidbody;
            // Vertex has already resolved Auto/manual collision from the authored solid. Applying
            // the legacy approximate box heuristic here can fill intentionally authored openings.
            if (meshCollider.GetComponent<VertexObjectIdentity>())
            {
                // Disabled/inactive colliders may not have a live physics attachment yet.
                rigidbody = rigidbody ? rigidbody : meshCollider.GetComponentInParent<Rigidbody>(true);
                if (rigidbody && !rigidbody.isKinematic && !meshCollider.convex)
                    return Outcome.Error($"'{objectPath}' uses Vertex Exact collision on a dynamic Rigidbody. " +
                        "Choose convex/primitive proxies in Vertex or make the entity kinematic.");
                return Outcome.Unchanged($"Preserved Vertex-authored collision on '{objectPath}'.");
            }
            var requiresConvex = rigidbody != null;
            if (requiresConvex && !meshCollider.convex)
                return EnsureConvex(meshCollider, objectPath, "Rigidbody compatibility");

            if (ShouldReplaceWithBox(meshCollider))
                return ReplaceWithBoxCollider(meshCollider, objectPath);

            return Outcome.Unchanged($"Left MeshCollider on '{objectPath}' unchanged.");
        }

        private static Outcome EnsureConvex(MeshCollider meshCollider, string objectPath, string reason)
        {
            if (meshCollider.convex)
                return Outcome.Unchanged($"MeshCollider on '{objectPath}' was already convex.");

            if (!meshCollider.sharedMesh)
                return Outcome.Error($"'{objectPath}' is missing a shared mesh and cannot be made convex.");

            meshCollider.convex = true;
            return Outcome.ForcedConvex($"Forced convex MeshCollider on '{objectPath}' for {reason}.");
        }

        private static Outcome ReplaceWithBoxCollider(MeshCollider meshCollider, string objectPath)
        {
            var gameObject = meshCollider.gameObject;
            if (!gameObject)
                return Outcome.Error("MeshCollider is missing its GameObject.");

            var existingBox = gameObject.GetComponent<BoxCollider>();
            if (existingBox)
            {
                Object.DestroyImmediate(meshCollider, true);
                return Outcome.ReplacedWithBox(
                    $"Removed MeshCollider from '{objectPath}' and kept the existing BoxCollider.");
            }

            if (!TryResolveBoxShape(meshCollider, out var center, out var size))
                return Outcome.Error(
                    $"'{objectPath}' could not be replaced with a BoxCollider because its local bounds could not be resolved.");

            var box = gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            box.isTrigger = meshCollider.isTrigger;
            box.enabled = meshCollider.enabled;
            box.sharedMaterial = meshCollider.sharedMaterial;

            Object.DestroyImmediate(meshCollider, true);
            return Outcome.ReplacedWithBox(
                $"Replaced MeshCollider on '{objectPath}' with a BoxCollider.");
        }

        private static bool ShouldReplaceWithBox(MeshCollider meshCollider)
        {
            if (!meshCollider || !meshCollider.enabled)
                return false;
            if (meshCollider.isTrigger)
                return false;
            if (meshCollider.attachedRigidbody)
                return false;
            if (!meshCollider.gameObject.isStatic)
                return false;
            if (HasNegativeLossyScale(meshCollider.transform))
                return false;
            if (meshCollider.TryGetComponent<SkinnedMeshRenderer>(out _))
                return false;
            if (!meshCollider.TryGetComponent<Renderer>(out var renderer) || !renderer.enabled)
                return false;
            if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length != 1)
                return false;
            if (!meshCollider.sharedMesh)
                return false;
            if (IsEmbeddedProBuilderMesh(meshCollider.sharedMesh))
                return false;
            if (!HasAxisAlignedWorldTransform(meshCollider.transform))
                return false;

            return IsBoxLikeMesh(meshCollider.sharedMesh);
        }

        private static bool TryResolveBoxShape(MeshCollider meshCollider, out Vector3 center, out Vector3 size)
        {
            center = Vector3.zero;
            size = Vector3.one;

            if (!meshCollider)
                return false;

            if (meshCollider.sharedMesh)
            {
                var meshBounds = meshCollider.sharedMesh.bounds;
                if (IsUsableBounds(meshBounds))
                {
                    center = meshBounds.center;
                    size = meshBounds.size;
                    return true;
                }
            }

            if (meshCollider.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh)
            {
                var meshBounds = meshFilter.sharedMesh.bounds;
                if (IsUsableBounds(meshBounds))
                {
                    center = meshBounds.center;
                    size = meshBounds.size;
                    return true;
                }
            }

            if (meshCollider.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer))
            {
                var meshBounds = skinnedMeshRenderer.localBounds;
                if (IsUsableBounds(meshBounds))
                {
                    center = meshBounds.center;
                    size = meshBounds.size;
                    return true;
                }
            }

            return false;
        }

        private static bool IsBoxLikeMesh(Mesh mesh)
        {
            if (!mesh || !mesh.isReadable)
                return false;

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            if (vertices == null || vertices.Length < 8 || triangles == null || triangles.Length < 36)
                return false;

            var bounds = mesh.bounds;
            if (!IsUsableBounds(bounds))
                return false;

            if (!HasAxisAlignedTriangleNormals(vertices, triangles))
                return false;

            if (!HasBoxLikeVolume(mesh, bounds))
                return false;

            return VerticesStayNearBoundsFaces(vertices, bounds);
        }

        private static bool HasBoxLikeVolume(Mesh mesh, Bounds bounds)
        {
            var boundsVolume = bounds.size.x * bounds.size.y * bounds.size.z;
            if (boundsVolume <= 0.0001f)
                return false;

            if (!TryComputeMeshVolume(mesh, out var meshVolume))
                return false;

            var fillRatio = meshVolume / boundsVolume;
            return fillRatio >= 0.85f;
        }

        private static bool TryComputeMeshVolume(Mesh mesh, out float volume)
        {
            volume = 0f;
            if (!mesh || !mesh.isReadable)
                return false;

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            if (vertices == null || triangles == null || triangles.Length < 12)
                return false;

            var signedVolume = 0d;
            var validTriangleCount = 0;
            for (var i = 0; i <= triangles.Length - 3; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var cross = Vector3.Cross(b, c);
                var tetrahedron = Vector3.Dot(a, cross) / 6d;
                if (Math.Abs(tetrahedron) <= 0.0000001d)
                    continue;

                validTriangleCount++;
                signedVolume += tetrahedron;
            }

            if (validTriangleCount == 0)
                return false;

            volume = Mathf.Abs((float)signedVolume);
            return volume > 0.0001f;
        }

        private static bool HasAxisAlignedTriangleNormals(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles)
        {
            var total = 0;
            var axisAligned = 0;

            for (var i = 0; i <= triangles.Count - 3; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude <= 0.000001f)
                    continue;

                total++;
                normal.Normalize();
                var maxDot = Mathf.Max(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                if (maxDot >= 0.985f)
                    axisAligned++;
            }

            if (total == 0)
                return false;

            return axisAligned / (float)total >= 0.9f;
        }

        private static bool VerticesStayNearBoundsFaces(IEnumerable<Vector3> vertices, Bounds bounds)
        {
            var extents = bounds.extents;
            var center = bounds.center;
            var total = 0;
            var proximitySum = 0f;
            var maxInteriorDepth = 0f;

            foreach (var vertex in vertices)
            {
                var depthX = NormalizedInteriorDepth(vertex.x, center.x, extents.x);
                var depthY = NormalizedInteriorDepth(vertex.y, center.y, extents.y);
                var depthZ = NormalizedInteriorDepth(vertex.z, center.z, extents.z);
                var nearestFaceDepth = Mathf.Min(depthX, depthY, depthZ);
                proximitySum += nearestFaceDepth;
                maxInteriorDepth = Mathf.Max(maxInteriorDepth, nearestFaceDepth);
                total++;
            }

            if (total == 0)
                return false;

            var averageDepth = proximitySum / total;
            return averageDepth <= 0.12f && maxInteriorDepth <= 0.3f;
        }

        private static float NormalizedInteriorDepth(float value, float center, float extent)
        {
            if (extent <= 0.0001f)
                return 0f;

            var normalized = Mathf.Abs(value - center) / extent;
            return Mathf.Clamp01(1f - normalized);
        }

        private static bool IsUsableBounds(Bounds bounds)
        {
            return bounds.size is { x: > 0.0001f, y: > 0.0001f, z: > 0.0001f };
        }

        private static bool HasNegativeLossyScale(Transform transform)
        {
            if (!transform)
                return false;

            var scale = transform.lossyScale;
            return scale.x < 0f || scale.y < 0f || scale.z < 0f;
        }

        private static bool HasAxisAlignedWorldTransform(Transform transform)
        {
            if (!transform)
                return false;

            return IsAxisAlignedVector(transform.right) &&
                   IsAxisAlignedVector(transform.up) &&
                   IsAxisAlignedVector(transform.forward);
        }

        private static bool IsAxisAlignedVector(Vector3 vector)
        {
            if (vector.sqrMagnitude <= 0.000001f)
                return false;

            vector.Normalize();
            var maxDot = Mathf.Max(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
            return maxDot >= 0.9995f;
        }

        private static bool IsEmbeddedProBuilderMesh(Mesh mesh)
        {
            if (!mesh)
                return false;

            return mesh.name.StartsWith("pb_Mesh", StringComparison.OrdinalIgnoreCase);
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
                ForcedConvex = 0;
                ReplacedWithBox = 0;
                Unchanged = 0;
            }

            internal readonly int Scanned;
            internal int ForcedConvex;
            internal int ReplacedWithBox;
            internal int Unchanged;

            internal string BuildMessage()
            {
                return
                    $"Mesh optimization scanned {Scanned} MeshCollider(s): " +
                    $"{ForcedConvex} forced convex, {ReplacedWithBox} replaced with BoxCollider, " +
                    $"{Unchanged} unchanged.";
            }
        }

        private enum OutcomeKind
        {
            Unchanged,
            ForcedConvex,
            ReplacedWithBox,
            Error
        }

        private readonly struct Outcome
        {
            private Outcome(OutcomeKind kind, string message)
            {
                Kind = kind;
                Message = message ?? string.Empty;
            }

            internal OutcomeKind Kind { get; }
            internal string Message { get; }

            internal static Outcome Unchanged(string message)
            {
                return new Outcome(OutcomeKind.Unchanged, message);
            }

            internal static Outcome ForcedConvex(string message)
            {
                return new Outcome(OutcomeKind.ForcedConvex, message);
            }

            internal static Outcome ReplacedWithBox(string message)
            {
                return new Outcome(OutcomeKind.ReplacedWithBox, message);
            }

            internal static Outcome Error(string message)
            {
                return new Outcome(OutcomeKind.Error, message);
            }
        }
    }
}
#endif
