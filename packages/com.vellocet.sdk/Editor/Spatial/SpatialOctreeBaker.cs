// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Logging;
using Vellocet.Sdk.Spatial;
using Vellocet.Sdk.Spatial.Acoustics;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Vellocet.Sdk.Editor.Spatial
{
    public static class SpatialOctreeBaker
    {
        private const string DefaultOutputFolder = "Assets/GameContent/Spatial";
        private const string BakeAlgorithmVersion = "v9-solid-cell-pruning";

        public static SpatialOctreeAsset BakeActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            var mapId = Path.GetFileNameWithoutExtension(scene.path);
            return BakeScene(scene, mapId, scene.path, null, null, out _);
        }

        public static SpatialOctreeAsset BakeCompiledScene(Scene scene, string mapId, string sceneAddress,
            string outputAssetPath, string address)
        {
            return BakeScene(scene, mapId, sceneAddress, outputAssetPath, address, out _);
        }

        public static SpatialOctreeAsset BakeCompiledScene(Scene scene, string mapId, string sceneAddress,
            string outputAssetPath, string address, out bool reusedCachedAsset, SpatialOctreeBakeSettings? settingsOverride = null)
        {
            return BakeScene(scene, mapId, sceneAddress, outputAssetPath, address, out reusedCachedAsset, settingsOverride);
        }

        private static SpatialOctreeAsset BakeScene(Scene scene, string mapId, string sceneAddress,
            string outputAssetPath, string requestedAddress, out bool reusedCachedAsset, SpatialOctreeBakeSettings? settingsOverride = null)
        {
            reusedCachedAsset = false;
            if (!scene.IsValid())
                throw new InvalidOperationException("No active scene is available for spatial octree baking.");

            var settings = settingsOverride?.Sanitize() ?? ResolveSettings(scene);
            Physics.SyncTransforms();
            var colliders = CollectColliders(scene, settings);
            var rootBounds = CalculateRootBounds(scene, colliders, settings);
            var geometryHash = ComputeGeometryHash(colliders, settings, rootBounds, out var cacheable);
            var assetPath = string.IsNullOrWhiteSpace(outputAssetPath)
                ? ResolveAssetPath(scene, mapId)
                : outputAssetPath.Replace('\\', '/');
            EnsureAssetFolder(Path.GetDirectoryName(assetPath) ?? DefaultOutputFolder);

            var asset = AssetDatabase.LoadAssetAtPath<SpatialOctreeAsset>(assetPath);
            if (cacheable && CanReuseCachedAsset(asset, mapId, sceneAddress, geometryHash, settings))
            {
                if (asset.UpdateBakeSettings(settings))
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                }

                RegisterAddressable(assetPath, mapId, requestedAddress, out var cachedAddress);
                SdkConsoleBridge.Write(
                    $"[SpatialOctreeBaker] Reused cached '{assetPath}' nodes={asset.Nodes.Length}, traversable={asset.TraversableLeafCount}, address='{cachedAddress}', settings={DescribeSettings(settings)}.");
                reusedCachedAsset = true;
                return asset;
            }

            var bakeTimer = Stopwatch.StartNew();
            var bakeBudget = new BakeBudgetGuard(settings, bakeTimer);
            var mutableNodes = new List<MutableNode>(1024);
            using var overlapTester = new CellOverlapTester();
            CreateNode(rootBounds, -1, 0, settings, colliders, mutableNodes, overlapTester, bakeBudget);
            var subdivisionMilliseconds = bakeTimer.ElapsedMilliseconds;

            var nodes = FinalizeNodes(mutableNodes);
            BuildNeighbors(nodes, out var neighborStarts, out var neighborIndices,
                settings.MaxBakedNeighborEdgeCount);
            var componentCount = AssignComponents(nodes, neighborStarts, neighborIndices);
            SpatialOctreeRoutingMetadata.Build(nodes, neighborStarts, neighborIndices, settings.RoutingRegionDepth,
                out var traversableNodeIndices, out var routingRegions, out var routingNeighborStarts,
                out var routingNeighborIndices);
            var neighborApertures = SpatialOctreeAcousticMetadata.BuildNeighborApertures(nodes, neighborStarts,
                neighborIndices);
            var acousticData = SpatialOctreeAcousticMetadata.Build(nodes, neighborStarts, neighborIndices,
                neighborApertures, rootBounds);
            var traversableLeafCount = traversableNodeIndices.Length;
            EnforceBakeBudgets(settings, nodes.Length, traversableLeafCount, neighborIndices.Length,
                routingRegions.Length, routingNeighborIndices.Length, bakeTimer.ElapsedMilliseconds);

            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<SpatialOctreeAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            asset.SetBakedData(
                mapId,
                sceneAddress,
                scene.path,
                geometryHash,
                settings,
                rootBounds,
                nodes,
                neighborStarts,
                neighborIndices,
                neighborApertures,
                traversableNodeIndices,
                routingRegions,
                routingNeighborStarts,
                routingNeighborIndices,
                traversableLeafCount,
                acousticData);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            RegisterAddressable(assetPath, mapId, requestedAddress, out var address);
            var coldBakeMilliseconds = bakeTimer.ElapsedMilliseconds;

            SdkConsoleBridge.Write(
                $"[SpatialOctreeBaker] Baked '{assetPath}' nodes={nodes.Length}, traversable={traversableLeafCount}, " +
                $"components={componentCount}, regions={routingRegions.Length}, edges={neighborIndices.Length}, " +
                $"coldBakeMs={coldBakeMilliseconds}, subdivisionMs={subdivisionMilliseconds}, address='{address}', " +
                $"settings={DescribeSettings(settings)}.");
            return asset;
        }

        public static SpatialOctreeBakeSettings ResolveSettings(Scene scene)
        {
            var sources = MapSceneTraversalUtility.GetComponents<SpatialOctreeBakeSettingsSource>(scene);
            foreach (var source in sources)
                if (source)
                    return source.Settings;

            return SpatialOctreeBakeSettings.Default.Sanitize();
        }

        public static string DescribeSettings(SpatialOctreeBakeSettings settings)
        {
            settings = settings.Sanitize();
            return $"mask={settings.OccluderMask.value}, trigger={settings.TriggerInteraction}, " +
                   $"minNode={settings.MinNodeSize:0.###}, maxOpenNode={settings.MaxOpenNodeSize:0.###}, " +
                   $"padding={settings.BoundsPadding:0.###}, maxDepth={settings.MaxDepth}, " +
                   $"routingDepth={settings.RoutingRegionDepth}, pathBudget={settings.MaxPathSearchIterations}, " +
                   $"includeInactive={settings.IncludeInactive}";
        }

        internal static List<Collider> CollectColliders(Scene scene, SpatialOctreeBakeSettings settings)
        {
            var colliders = MapSceneTraversalUtility.GetComponents<Collider>(scene, settings.IncludeInactive);
            var dynamicRoots = CollectDynamicOccluderRoots(scene);
            var output = new List<Collider>(colliders.Length);
            foreach (var collider in colliders)
            {
                if (!collider || !collider.enabled)
                    continue;
                if (!settings.IncludeInactive && !collider.gameObject.activeInHierarchy)
                    continue;
                if (settings.TriggerInteraction == QueryTriggerInteraction.Ignore && collider.isTrigger)
                    continue;
                if ((settings.OccluderMask.value & (1 << collider.gameObject.layer)) == 0)
                    continue;
                if (IsDynamicCollider(collider, dynamicRoots))
                    continue;

                output.Add(collider);
            }

            return output;
        }

        private static HashSet<Transform> CollectDynamicOccluderRoots(Scene scene)
        {
            var roots = new HashSet<Transform>();
            var behaviours = MapSceneTraversalUtility.GetComponents<MonoBehaviour>(scene);
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IMovingBrushEntityMarker marker && marker.BrushTransform)
                    roots.Add(marker.BrushTransform);

            var explicitOccluders = MapSceneTraversalUtility.GetComponents<SpatialDynamicOccluder>(scene);
            for (var i = 0; i < explicitOccluders.Length; i++)
                if (explicitOccluders[i])
                    roots.Add(explicitOccluders[i].transform);

            return roots;
        }

        private static bool IsDynamicCollider(Collider collider, HashSet<Transform> dynamicRoots)
        {
            if (!collider)
                return false;
            if (collider.attachedRigidbody)
                return true;

            var transform = collider.transform;
            foreach (var root in dynamicRoots)
                if (root && (transform == root || transform.IsChildOf(root)))
                    return true;

            return false;
        }

        private static Bounds CalculateRootBounds(Scene scene, IReadOnlyList<Collider> occluders,
            SpatialOctreeBakeSettings settings)
        {
            var hasBounds = false;
            var bounds = default(Bounds);

            foreach (var occluder in occluders)
                EncapsulateBounds(occluder.bounds, ref bounds, ref hasBounds);

            if (!hasBounds)
                EncapsulateFallbackColliderBounds(scene, settings, ref bounds, ref hasBounds);

            if (!hasBounds)
                EncapsulateFallbackRendererBounds(scene, settings, ref bounds, ref hasBounds);

            if (!hasBounds)
                return new Bounds(Vector3.zero, Vector3.one * Mathf.Max(settings.MinNodeSize, 16f));

            var size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) +
                       settings.BoundsPadding * 2f;
            size = Mathf.Max(size, settings.MinNodeSize);
            return new Bounds(bounds.center, Vector3.one * size);
        }

        private static void EncapsulateFallbackColliderBounds(Scene scene, SpatialOctreeBakeSettings settings,
            ref Bounds bounds, ref bool hasBounds)
        {
            var sceneColliders = MapSceneTraversalUtility.GetComponents<Collider>(scene, settings.IncludeInactive);
            foreach (var collider in sceneColliders)
            {
                if (!ShouldIncludeFallbackBoundsObject(collider, scene, settings))
                    continue;

                EncapsulateBounds(collider.bounds, ref bounds, ref hasBounds);
            }
        }

        private static void EncapsulateFallbackRendererBounds(Scene scene, SpatialOctreeBakeSettings settings,
            ref Bounds bounds, ref bool hasBounds)
        {
            var renderers = MapSceneTraversalUtility.GetComponents<Renderer>(scene, settings.IncludeInactive);
            foreach (var renderer in renderers)
            {
                if (!ShouldIncludeFallbackBoundsObject(renderer, scene, settings))
                    continue;

                EncapsulateBounds(renderer.bounds, ref bounds, ref hasBounds);
            }
        }

        private static bool ShouldIncludeFallbackBoundsObject(Component component, Scene scene,
            SpatialOctreeBakeSettings settings)
        {
            if (!component || component.gameObject.scene != scene)
                return false;
            if (!settings.IncludeInactive && !component.gameObject.activeInHierarchy)
                return false;
            switch (component)
            {
                case Collider collider:
                    if (!collider.enabled)
                        return false;
                    return settings.TriggerInteraction != QueryTriggerInteraction.Ignore || !collider.isTrigger;
                case Renderer renderer:
                    return renderer.enabled;
                default:
                    return false;
            }
        }

        private static void EncapsulateBounds(Bounds candidate, ref Bounds bounds, ref bool hasBounds)
        {
            if (!hasBounds)
            {
                bounds = candidate;
                hasBounds = true;
                return;
            }

            bounds.Encapsulate(candidate);
        }

        private static int CreateNode(
            Bounds bounds,
            int parent,
            int depth,
            SpatialOctreeBakeSettings settings,
            IReadOnlyList<Collider> colliders,
            List<MutableNode> nodes,
            CellOverlapTester overlapTester,
            BakeBudgetGuard bakeBudget)
        {
            bakeBudget.ThrowIfCannotAdd(nodes.Count, 1);
            var index = nodes.Count;
            nodes.Add(default);
            PopulateNode(index, bounds, parent, depth, settings, colliders, nodes, overlapTester, bakeBudget);
            return index;
        }

        private static void PopulateNode(
            int index,
            Bounds bounds,
            int parent,
            int depth,
            SpatialOctreeBakeSettings settings,
            IReadOnlyList<Collider> colliders,
            List<MutableNode> nodes,
            CellOverlapTester overlapTester,
            BakeBudgetGuard bakeBudget)
        {
            var occupied = colliders.Count > 0;
            var largestAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            var targetLeafSize = occupied ? settings.MinNodeSize : settings.MaxOpenNodeSize;
            var leaf = depth >= settings.MaxDepth || largestAxis <= targetLeafSize ||
                       CellOverlapTester.IsFullyBlocked(bounds, colliders);
            nodes[index] = new MutableNode(bounds, parent, depth, leaf, leaf && occupied);
            if (leaf)
                return;

            bakeBudget.ThrowIfCannotAdd(nodes.Count, 8);
            var firstChild = nodes.Count;
            for (var i = 0; i < 8; i++)
                nodes.Add(default);

            var node = nodes[index];
            node.FirstChildIndex = firstChild;
            node.ChildCount = 8;
            nodes[index] = node;

            var childSize = bounds.size * 0.5f;
            var offset = bounds.size * 0.25f;
            for (var i = 0; i < 8; i++)
            {
                var center = bounds.center;
                center.x += (i & 1) == 0 ? -offset.x : offset.x;
                center.y += (i & 2) == 0 ? -offset.y : offset.y;
                center.z += (i & 4) == 0 ? -offset.z : offset.z;
                var childBounds = new Bounds(center, childSize);
                var childColliders = ListPool<Collider>.Get();
                try
                {
                    CollectIntersectingColliders(childBounds, colliders, childColliders, overlapTester, settings);
                    PopulateNode(firstChild + i, childBounds, index, depth + 1, settings, childColliders, nodes,
                        overlapTester, bakeBudget);
                }
                finally
                {
                    ListPool<Collider>.Release(childColliders);
                }
            }
        }

        private static void CollectIntersectingColliders(Bounds bounds, IReadOnlyList<Collider> candidates,
            List<Collider> output, CellOverlapTester overlapTester, SpatialOctreeBakeSettings settings)
        {
            overlapTester.Collect(bounds, candidates, output, settings);
        }

        private static SpatialOctreeNodeData[] FinalizeNodes(IReadOnlyList<MutableNode> mutableNodes)
        {
            var nodes = new SpatialOctreeNodeData[mutableNodes.Count];
            var index = 0;
            foreach (var node in mutableNodes)
                nodes[index++] = new SpatialOctreeNodeData
                {
                    Bounds = node.Bounds,
                    ParentIndex = node.ParentIndex,
                    FirstChildIndex = node.FirstChildIndex,
                    ChildCount = node.ChildCount,
                    Depth = node.Depth,
                    ComponentId = -1,
                    TraversableIndex = -1,
                    RoutingRegionId = -1,
                    IsLeaf = node.IsLeaf,
                    IsBlocked = node.IsBlocked
                };

            return nodes;
        }

        internal static void BuildNeighbors(SpatialOctreeNodeData[] nodes, out int[] starts, out int[] indices,
            int maxDirectedEdgeCount = int.MaxValue)
        {
            var edges = new List<ulong>(Mathf.Max(16, nodes.Length));
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                if (!nodes[nodeIndex].IsTraversable)
                    continue;

                for (var axis = 0; axis < 3; axis++)
                {
                    CollectPositiveFaceNeighbors(nodes, nodeIndex, axis, edges);
                    if ((long)edges.Count * 2 > maxDirectedEdgeCount)
                        throw new InvalidOperationException(
                            "Spatial octree edge budget exceeded while building adjacency: " +
                            $"> {maxDirectedEdgeCount} directed edges.");
                }
            }

            edges.Sort();
            var uniqueCount = 0;
            for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                if (uniqueCount > 0 && edges[edgeIndex] == edges[uniqueCount - 1])
                    continue;
                edges[uniqueCount++] = edges[edgeIndex];
            }

            if (uniqueCount < edges.Count)
                edges.RemoveRange(uniqueCount, edges.Count - uniqueCount);

            var degrees = new int[nodes.Length];
            for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                var edge = edges[edgeIndex];
                degrees[(int)(edge >> 32)]++;
                degrees[(int)(edge & uint.MaxValue)]++;
            }

            starts = new int[nodes.Length + 1];
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
                starts[nodeIndex + 1] = starts[nodeIndex] + degrees[nodeIndex];

            indices = new int[starts[^1]];
            var cursors = new int[nodes.Length];
            Array.Copy(starts, cursors, nodes.Length);
            for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                var edge = edges[edgeIndex];
                var a = (int)(edge >> 32);
                var b = (int)(edge & uint.MaxValue);
                indices[cursors[a]++] = b;
                indices[cursors[b]++] = a;
            }

            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
                Array.Sort(indices, starts[nodeIndex], starts[nodeIndex + 1] - starts[nodeIndex]);
        }

        private static void CollectPositiveFaceNeighbors(
            SpatialOctreeNodeData[] nodes,
            int sourceIndex,
            int axis,
            List<ulong> edges)
        {
            // Climb until the source is in the negative half of an ancestor, then cross to the
            // positive sibling. This locates the only subtree that can touch the source's positive
            // face without rescanning the octree root for every leaf.
            var currentIndex = sourceIndex;
            var axisBit = 1 << axis;
            while ((uint)currentIndex < (uint)nodes.Length)
            {
                var parentIndex = nodes[currentIndex].ParentIndex;
                if ((uint)parentIndex >= (uint)nodes.Length)
                    return;

                var parent = nodes[parentIndex];
                var childOffset = currentIndex - parent.FirstChildIndex;
                if ((uint)childOffset >= (uint)parent.ChildCount)
                    return;

                if ((childOffset & axisBit) == 0)
                {
                    var candidateIndex = parent.FirstChildIndex + (childOffset | axisBit);
                    CollectNegativeFaceLeaves(nodes, candidateIndex, sourceIndex, axis, edges);
                    return;
                }

                currentIndex = parentIndex;
            }
        }

        private static void CollectNegativeFaceLeaves(
            SpatialOctreeNodeData[] nodes,
            int candidateIndex,
            int sourceIndex,
            int axis,
            List<ulong> edges)
        {
            if ((uint)candidateIndex >= (uint)nodes.Length)
                return;

            const float epsilon = 0.001f;
            var candidate = nodes[candidateIndex];
            if (!OverlapsOnProjectedFace(nodes[sourceIndex].Bounds, candidate.Bounds, axis, epsilon))
                return;

            if (candidate.IsLeaf)
            {
                if (!candidate.IsTraversable)
                    return;

                var min = Mathf.Min(sourceIndex, candidateIndex);
                var max = Mathf.Max(sourceIndex, candidateIndex);
                edges.Add(((ulong)(uint)min << 32) | (uint)max);
                return;
            }

            var firstChild = candidate.FirstChildIndex;
            if (firstChild < 0)
                return;

            var axisBit = 1 << axis;
            var endChild = Mathf.Min(firstChild + candidate.ChildCount, nodes.Length);
            for (var childIndex = firstChild; childIndex < endChild; childIndex++)
            {
                var childOffset = childIndex - firstChild;
                if ((childOffset & axisBit) == 0)
                    CollectNegativeFaceLeaves(nodes, childIndex, sourceIndex, axis, edges);
            }
        }

        private static bool OverlapsOnProjectedFace(Bounds a, Bounds b, int axis, float epsilon)
        {
            return axis switch
            {
                0 => Overlaps(a.min.y, a.max.y, b.min.y, b.max.y, epsilon) &&
                     Overlaps(a.min.z, a.max.z, b.min.z, b.max.z, epsilon),
                1 => Overlaps(a.min.x, a.max.x, b.min.x, b.max.x, epsilon) &&
                     Overlaps(a.min.z, a.max.z, b.min.z, b.max.z, epsilon),
                _ => Overlaps(a.min.x, a.max.x, b.min.x, b.max.x, epsilon) &&
                     Overlaps(a.min.y, a.max.y, b.min.y, b.max.y, epsilon)
            };
        }

        private static bool Overlaps(float aMin, float aMax, float bMin, float bMax, float epsilon)
        {
            return aMin < bMax - epsilon && bMin < aMax - epsilon;
        }

        private static int AssignComponents(SpatialOctreeNodeData[] nodes, int[] starts, int[] indices)
        {
            var component = 0;
            var queue = new Queue<int>();
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                if (!nodes[nodeIndex].IsTraversable || nodes[nodeIndex].ComponentId >= 0)
                    continue;

                var node = nodes[nodeIndex];
                node.ComponentId = component;
                nodes[nodeIndex] = node;
                queue.Enqueue(nodeIndex);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var start = starts[current];
                    var end = starts[current + 1];
                    for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
                    {
                        var neighbor = indices[edgeIndex];
                        if (nodes[neighbor].ComponentId >= 0)
                            continue;

                        var neighborNode = nodes[neighbor];
                        neighborNode.ComponentId = component;
                        nodes[neighbor] = neighborNode;
                        queue.Enqueue(neighbor);
                    }
                }

                component++;
            }

            return component;
        }

        private static void EnforceBakeBudgets(
            SpatialOctreeBakeSettings settings,
            int nodeCount,
            int traversableCount,
            int neighborEdgeCount,
            int routingRegionCount,
            int routingNeighborEdgeCount,
            long elapsedMilliseconds)
        {
            settings = settings.Sanitize();
            if (nodeCount > settings.MaxBakedNodeCount)
                throw new InvalidOperationException(
                    $"Spatial octree node budget exceeded: {nodeCount} > {settings.MaxBakedNodeCount}.");
            if (neighborEdgeCount > settings.MaxBakedNeighborEdgeCount)
                throw new InvalidOperationException(
                    $"Spatial octree edge budget exceeded: {neighborEdgeCount} > " +
                    $"{settings.MaxBakedNeighborEdgeCount}.");
            if (routingRegionCount > settings.MaxRoutingRegionCount)
                throw new InvalidOperationException(
                    $"Spatial octree routing-region budget exceeded: {routingRegionCount} > " +
                    $"{settings.MaxRoutingRegionCount}.");

            var estimatedRuntimeBytes = SpatialOctreePerformanceBudget.EstimateRuntimeCoreBytes(nodeCount,
                traversableCount, neighborEdgeCount, routingRegionCount, routingNeighborEdgeCount);
            if (estimatedRuntimeBytes > settings.MaxEstimatedRuntimeBytes)
                throw new InvalidOperationException(
                    $"Spatial octree estimated runtime budget exceeded: {estimatedRuntimeBytes} > " +
                    $"{settings.MaxEstimatedRuntimeBytes} bytes.");
            if (elapsedMilliseconds > settings.MaxBakeMilliseconds)
                throw new InvalidOperationException(
                    $"Spatial octree cold-bake budget exceeded: {elapsedMilliseconds}ms > " +
                    $"{settings.MaxBakeMilliseconds}ms.");
        }

        private static string ResolveAssetPath(Scene scene, string mapId)
        {
            var name = !string.IsNullOrWhiteSpace(mapId) ? mapId : Path.GetFileNameWithoutExtension(scene.path);
            name = MakeSafeFileName(name);

            return $"{DefaultOutputFolder}/{name}_SpatialOctree.asset";
        }

        private static void EnsureAssetFolder(string folder)
        {
            var normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            var parts = normalized.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string MakeSafeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value?.Trim() ?? "SpatialOctree");
            for (var i = 0; i < builder.Length; i++)
                if (Array.IndexOf(invalid, builder[i]) >= 0)
                    builder[i] = '_';
            return builder.Length > 0 ? builder.ToString() : "SpatialOctree";
        }

        internal static string ComputeGeometryHash(IReadOnlyList<Collider> colliders,
            SpatialOctreeBakeSettings settings,
            Bounds rootBounds, out bool cacheable)
        {
            cacheable = true;
            var builder = new StringBuilder();
            builder.Append(BakeAlgorithmVersion).Append('|')
                .Append(settings.OccluderMask.value).Append('|')
                .Append(settings.TriggerInteraction).Append('|')
                .Append(settings.MinNodeSize).Append('|')
                .Append(settings.MaxOpenNodeSize).Append('|')
                .Append(settings.BoundsPadding).Append('|')
                .Append(settings.MaxDepth).Append('|')
                .Append(settings.RoutingRegionDepth).Append('|')
                .Append(settings.IncludeInactive).AppendLine();
            AppendBounds(builder, rootBounds);
            builder.AppendLine();

            foreach (var collider in colliders)
            {
                if (!collider)
                    continue;

                var transform = collider.transform;
                builder.Append(GetHierarchyPath(transform)).Append('|')
                    .Append(collider.GetType().FullName).Append('|')
                    .Append(collider.gameObject.layer).Append('|')
                    .Append(collider.isTrigger).Append('|');
                AppendMatrix4x4(builder, transform.localToWorldMatrix);
                builder.Append('|');
                AppendBounds(builder, collider.bounds);
                builder.Append('|');
                cacheable &= AppendColliderShapeFingerprint(builder, collider);
                builder.AppendLine();
            }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private static bool CanReuseCachedAsset(SpatialOctreeAsset asset, string mapId, string sceneAddress,
            string geometryHash, SpatialOctreeBakeSettings settings)
        {
            if (!asset || !asset.IsUsable || !asset.TryValidateData(out _) ||
                !SpatialOctreePerformanceBudget.TryValidate(asset, settings, out _))
                return false;

            var normalizedMapId = ContentIdUtility.Sanitize(mapId);
            return string.Equals(asset.MapId, normalizedMapId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(asset.SceneAddress, sceneAddress ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(asset.GeometryHash, geometryHash, StringComparison.OrdinalIgnoreCase);
        }

        private static bool AppendColliderShapeFingerprint(StringBuilder builder, Collider collider)
        {
            switch (collider)
            {
                case BoxCollider box:
                    AppendVector3(builder, box.center);
                    builder.Append('|');
                    AppendVector3(builder, box.size);
                    return true;
                case SphereCollider sphere:
                    AppendVector3(builder, sphere.center);
                    builder.Append('|').Append(BitConverter.SingleToInt32Bits(sphere.radius));
                    return true;
                case CapsuleCollider capsule:
                    AppendVector3(builder, capsule.center);
                    builder.Append('|')
                        .Append(BitConverter.SingleToInt32Bits(capsule.radius)).Append('|')
                        .Append(BitConverter.SingleToInt32Bits(capsule.height)).Append('|')
                        .Append(capsule.direction);
                    return true;
                case CharacterController character:
                    AppendVector3(builder, character.center);
                    builder.Append('|')
                        .Append(BitConverter.SingleToInt32Bits(character.radius)).Append('|')
                        .Append(BitConverter.SingleToInt32Bits(character.height)).Append('|')
                        .Append(BitConverter.SingleToInt32Bits(character.skinWidth));
                    return true;
                case MeshCollider meshCollider:
                    builder.Append(meshCollider.convex).Append('|').Append(meshCollider.cookingOptions).Append('|');
                    return AppendMeshFingerprint(builder, meshCollider.sharedMesh);
                case TerrainCollider terrainCollider:
                    return AppendUnityAssetFingerprint(builder, terrainCollider.terrainData);
                default:
                    builder.Append("unsupported");
                    return false;
            }
        }

        private static bool AppendMeshFingerprint(StringBuilder builder, Mesh mesh)
        {
            if (!mesh)
            {
                builder.Append("<missing-mesh>");
                return false;
            }

            builder.Append(mesh.name).Append('|')
                .Append(mesh.vertexCount).Append('|')
                .Append(mesh.subMeshCount).Append('|');
            AppendBounds(builder, mesh.bounds);

            if (AppendUnityAssetFingerprint(builder, mesh))
                return true;

            if (!mesh.isReadable)
            {
                builder.Append("|unreadable-embedded-mesh");
                return false;
            }

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            builder.Append("|embedded|v=").Append(vertices?.Length ?? 0).Append('|');
            if (vertices != null)
                for (var i = 0; i < vertices.Length; i++)
                    AppendVector3(builder, vertices[i]);

            builder.Append("|t=").Append(triangles?.Length ?? 0).Append('|');
            if (triangles != null)
                for (var i = 0; i < triangles.Length; i++)
                    builder.Append(triangles[i]).Append(',');

            return true;
        }

        private static bool AppendUnityAssetFingerprint(StringBuilder builder, Object asset)
        {
            if (!asset)
            {
                builder.Append("<missing-asset>");
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            builder.Append('|').Append(assetPath).Append('|')
                .Append(AssetDatabase.AssetPathToGUID(assetPath)).Append('|')
                .Append(AssetDatabase.GetAssetDependencyHash(assetPath));
            return true;
        }

        private static void AppendBounds(StringBuilder builder, Bounds bounds)
        {
            AppendVector3(builder, bounds.center);
            builder.Append('|');
            AppendVector3(builder, bounds.size);
        }

        private static void AppendVector3(StringBuilder builder, Vector3 value)
        {
            builder.Append(BitConverter.SingleToInt32Bits(value.x)).Append(',')
                .Append(BitConverter.SingleToInt32Bits(value.y)).Append(',')
                .Append(BitConverter.SingleToInt32Bits(value.z)).Append(';');
        }

        private static void AppendMatrix4x4(StringBuilder builder, Matrix4x4 value)
        {
            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 4; column++)
                builder.Append(BitConverter.SingleToInt32Bits(value[row, column])).Append(',');
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (!transform)
                return string.Empty;

            var stack = new Stack<string>();
            while (transform)
            {
                stack.Push($"{transform.name}[{transform.GetSiblingIndex()}]");
                transform = transform.parent;
            }

            return string.Join("/", stack);
        }

        private static void RegisterAddressable(string assetPath, string mapId, string requestedAddress,
            out string address)
        {
            address = string.IsNullOrWhiteSpace(requestedAddress)
                ? MapSpatialGraphArtifacts.ResolveAddress(
                    string.IsNullOrWhiteSpace(mapId) ? Path.GetFileNameWithoutExtension(assetPath) : mapId)
                : requestedAddress.Trim();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!settings)
                return;

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var group = settings.DefaultGroup;
            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            AssetDatabase.SaveAssets();
        }

        private struct MutableNode
        {
            public readonly Bounds Bounds;
            public readonly int ParentIndex;
            public readonly int Depth;
            public readonly bool IsLeaf;
            public readonly bool IsBlocked;
            public int FirstChildIndex;
            public int ChildCount;

            public MutableNode(Bounds bounds, int parentIndex, int depth, bool isLeaf, bool isBlocked)
            {
                Bounds = bounds;
                ParentIndex = parentIndex;
                Depth = depth;
                IsLeaf = isLeaf;
                IsBlocked = isBlocked;
                FirstChildIndex = -1;
                ChildCount = 0;
            }
        }

        private sealed class BakeBudgetGuard
        {
            private const int TimeCheckNodeInterval = 256;

            private readonly int _maxBakeMilliseconds;
            private readonly int _maxNodeCount;
            private readonly Stopwatch _stopwatch;
            private int _nextTimeCheckNodeCount;

            public BakeBudgetGuard(SpatialOctreeBakeSettings settings, Stopwatch stopwatch)
            {
                settings = settings.Sanitize();
                _maxNodeCount = settings.MaxBakedNodeCount;
                _maxBakeMilliseconds = settings.MaxBakeMilliseconds;
                _stopwatch = stopwatch;
                _nextTimeCheckNodeCount = TimeCheckNodeInterval;
            }

            public void ThrowIfCannotAdd(int currentNodeCount, int additionalNodeCount)
            {
                var requestedNodeCount = (long)currentNodeCount + additionalNodeCount;
                if (requestedNodeCount > _maxNodeCount)
                    throw new InvalidOperationException(
                        "Spatial octree node budget exceeded during subdivision: " +
                        $"{requestedNodeCount} > {_maxNodeCount}.");

                if (requestedNodeCount < _nextTimeCheckNodeCount)
                    return;

                _nextTimeCheckNodeCount = currentNodeCount + TimeCheckNodeInterval;
                var elapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
                if (elapsedMilliseconds > _maxBakeMilliseconds)
                    throw new InvalidOperationException(
                        "Spatial octree cold-bake budget exceeded during subdivision: " +
                        $"{elapsedMilliseconds}ms > {_maxBakeMilliseconds}ms (nodes={currentNodeCount}).");
            }
        }

        internal sealed class CellOverlapTester : IDisposable
        {
            private const int DirectIntersectionThreshold = 12;
            private const int OverlapBufferSize = 8192;

            private static readonly Vector3[] SampleOffsets =
            {
                new(-1f, -1f, -1f),
                new(-1f, -1f, 1f),
                new(-1f, 1f, -1f),
                new(-1f, 1f, 1f),
                new(1f, -1f, -1f),
                new(1f, -1f, 1f),
                new(1f, 1f, -1f),
                new(1f, 1f, 1f),
                Vector3.zero
            };

            private readonly List<Collider> _boundsCandidates = new(64);

            private readonly BoxCollider _box;
            private readonly GameObject _gameObject;
            private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];
            private readonly HashSet<Collider> _overlapSet = new(OverlapBufferSize);
            private readonly Transform _transform;

            public CellOverlapTester()
            {
                _gameObject = new GameObject("SpatialOctreeBakeCellOverlapTester")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _transform = _gameObject.transform;
                _box = _gameObject.AddComponent<BoxCollider>();
            }

            public void Dispose()
            {
                if (_gameObject)
                    Object.DestroyImmediate(_gameObject);
            }

            public void Collect(Bounds bounds, IReadOnlyList<Collider> candidates, List<Collider> output,
                SpatialOctreeBakeSettings settings)
            {
                output.Clear();
                if (candidates == null || candidates.Count == 0)
                    return;

                _boundsCandidates.Clear();
                var supportsDirectSampling = true;
                for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    var candidate = candidates[candidateIndex];
                    if (!candidate || !candidate.bounds.Intersects(bounds))
                        continue;

                    _boundsCandidates.Add(candidate);
                    supportsDirectSampling &= SupportsClosestPointSampling(candidate);
                }

                if (_boundsCandidates.Count == 0)
                    return;

                if (_boundsCandidates.Count <= DirectIntersectionThreshold && supportsDirectSampling)
                {
                    foreach (var collider in _boundsCandidates)
                        if (Intersects(bounds, collider))
                            output.Add(collider);
                    return;
                }

                if (!settings.IncludeInactive &&
                    TryCollectUsingPhysicsOverlap(bounds, _boundsCandidates, output, settings))
                    return;

                foreach (var collider in _boundsCandidates)
                    if (Intersects(bounds, collider))
                        output.Add(collider);
            }

            public static bool IsFullyBlocked(Bounds bounds, IReadOnlyList<Collider> candidates)
            {
                foreach (var collider in candidates)
                {
                    if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                        !SupportsClosestPointSampling(collider) ||
                        !collider.bounds.Contains(bounds.min) || !collider.bounds.Contains(bounds.max))
                        continue;

                    // All corners inside one convex solid prove the whole cell is blocked.
                    // Non-convex bounds and corners in different colliders can enclose open space.
                    var containsCell = true;
                    foreach (var offset in SampleOffsets)
                    {
                        var sample = bounds.center + Vector3.Scale(offset, bounds.extents);
                        if (collider.ClosestPoint(sample).Equals(sample))
                            continue;

                        containsCell = false;
                        break;
                    }

                    if (containsCell)
                        return true;
                }

                return false;
            }

            private bool TryCollectUsingPhysicsOverlap(Bounds bounds, IReadOnlyList<Collider> candidates,
                List<Collider> output, SpatialOctreeBakeSettings settings)
            {
                var hitCount = Physics.OverlapBoxNonAlloc(
                    bounds.center,
                    bounds.extents,
                    _overlapBuffer,
                    Quaternion.identity,
                    settings.OccluderMask,
                    settings.TriggerInteraction);

                if (hitCount >= _overlapBuffer.Length)
                    return false;

                _overlapSet.Clear();
                for (var i = 0; i < hitCount; i++)
                {
                    var hit = _overlapBuffer[i];
                    if (hit)
                        _overlapSet.Add(hit);
                    _overlapBuffer[i] = null;
                }

                foreach (var collider in candidates)
                    if (collider && _overlapSet.Contains(collider))
                        output.Add(collider);

                return true;
            }

            private bool Intersects(Bounds bounds, Collider collider)
            {
                if (!collider || !collider.bounds.Intersects(bounds))
                    return false;

                _transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
                _box.center = Vector3.zero;
                _box.size = bounds.size;

                if (Physics.ComputePenetration(
                        _box,
                        bounds.center,
                        Quaternion.identity,
                        collider,
                        collider.transform.position,
                        collider.transform.rotation,
                        out _,
                        out _))
                    return true;

                // ClosestPoint logs an error for terrain and non-convex mesh colliders. Active colliders of those
                // types take the exact OverlapBox path above; overflow/include-inactive fallback stays conservative.
                if (!SupportsClosestPointSampling(collider))
                    return true;

                var closestToCenter = collider.ClosestPoint(bounds.center);
                if (bounds.Contains(closestToCenter))
                    return true;

                var extents = bounds.extents;
                foreach (var offset in SampleOffsets)
                {
                    var sample = bounds.center + new Vector3(
                        offset.x * extents.x,
                        offset.y * extents.y,
                        offset.z * extents.z);
                    var closest = collider.ClosestPoint(sample);
                    if ((closest - sample).sqrMagnitude <= 0.000001f)
                        return true;
                }

                return false;
            }

            private static bool SupportsClosestPointSampling(Collider collider)
            {
                return collider is BoxCollider ||
                       collider is SphereCollider ||
                       collider is CapsuleCollider ||
                       (collider is MeshCollider meshCollider && meshCollider.convex);
            }
        }
    }
}
