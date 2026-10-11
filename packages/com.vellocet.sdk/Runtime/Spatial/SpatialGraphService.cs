// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Spatial
{
    public static class SpatialGraphService
    {
        private static AsyncOperationHandle<SpatialOctreeAsset> _assetHandle;

        public static SpatialOctreeAsset CurrentAsset { get; private set; }
        public static SpatialOctreeGraph CurrentGraph { get; private set; }
        public static bool IsLoaded => CurrentAsset && CurrentGraph != null && CurrentGraph.IsUsable;
        public static string LastLoadFailureReason { get; private set; } = string.Empty;

        public static bool TryGetCurrentGraph(out SpatialOctreeGraph graph)
        {
            graph = CurrentGraph;
            return CurrentAsset && graph != null && graph.IsUsable;
        }

        public static bool AppendDiagnosticsReport(List<string> output)
        {
            if (output == null)
                return false;

            if (!TryGetCurrentGraph(out var graph))
            {
                output.Add("no usable spatial graph is loaded.");
                return false;
            }

            var settings = CurrentAsset.BakeSettings.Sanitize();
            var nodeCount = graph.NodeCount;
            var traversableCount = graph.TraversableNodeCount;
            var neighborEdgeCount = graph.NeighborEdgeCount;
            var acoustic = graph.GetAcousticMetadataSummary();
            output.Add(
                $"spatial graph: map='{CurrentAsset.MapId}' scene='{CurrentAsset.SceneAddress}' boundsCenter={FormatVector(CurrentAsset.Bounds.center)} boundsSize={FormatVector(CurrentAsset.Bounds.size)} geometryHash={CurrentAsset.GeometryHash}");
            output.Add(
                $"bake settings: minNodeSize={settings.MinNodeSize:0.###} maxOpenNodeSize={settings.MaxOpenNodeSize:0.###} maxDepth={settings.MaxDepth} boundsPadding={settings.BoundsPadding:0.###} occluderMask={settings.OccluderMask.value} triggerInteraction={settings.TriggerInteraction} includeInactive={settings.IncludeInactive}");
            output.Add(
                $"size: nodes={nodeCount} traversable={traversableCount} neighborEdges={neighborEdgeCount} " +
                $"routingRegions={graph.RoutingRegionCount} estimatedRuntimeCoreBytes={graph.EstimatedRuntimeCoreBytes}");
            output.Add(
                $"dynamic overlay: occluders={graph.DynamicOccluderCount} blockedNodes={graph.DynamicBlockedNodeCount}");
            output.Add(
                $"acoustic: metadata={(acoustic.Baked ? "baked" : "runtime")} clusters={acoustic.ClusterCount} exterior={acoustic.ExteriorNodeCount} transition={acoustic.TransitionNodeCount} thin={acoustic.ThinBoundaryNodeCount} avgOpen={acoustic.AverageOpenness:0.###} avgAperture={acoustic.AverageAperture:0.###}");
            output.Add(
                "validation: skipped in runtime diagnostics; use the octree bake/validation tools for full graph validation.");
            return true;
        }

        public static async Task<bool> LoadForMapSceneAsync(Scene scene, CancellationToken ct = default)
        {
            Clear();
            LastLoadFailureReason = string.Empty;

            if (!TryResolveSceneDependency(scene, out var sceneSpatialReference, out var resolveFailure))
                return Fail(resolveFailure);

            return await TryLoadFromReferenceAsync(scene, sceneSpatialReference, ct);
        }

        private static bool TryResolveSceneDependency(Scene scene, out AssetReference reference, out string failure)
        {
            reference = null;
            failure = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                failure = "loaded map scene is invalid or not loaded";
                return false;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                var dependencies = root.GetComponentsInChildren<MapRuntimeDependencies>(true);
                foreach (var component in dependencies)
                    if (component && component.TryGetSpatialGraphReference(out reference))
                        return true;
            }

            failure = $"scene '{scene.name}' has no MapRuntimeDependencies with a valid spatialGraphReference";
            return false;
        }

        private static async Task<bool> TryLoadFromReferenceAsync(Scene scene, AssetReference reference,
            CancellationToken ct)
        {
            if (reference == null || !reference.RuntimeKeyIsValid())
                return Fail("spatialGraphReference is null or has an invalid runtime key");

            _assetHandle = reference.LoadAssetAsync<SpatialOctreeAsset>();
            return await FinalizeLoadAsync(scene, ct);
        }

        private static async Task<bool> FinalizeLoadAsync(Scene scene, CancellationToken ct)
        {
            try
            {
                while (!_assetHandle.IsDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                ReleaseLoadedGraph();
                return Fail("spatial graph load was canceled");
            }
            catch (Exception ex)
            {
                ReleaseLoadedGraph();
                return Fail($"Addressables failed to load spatial graph: {ex.Message}");
            }

            var asset = _assetHandle.Result;
            if (!asset)
            {
                ReleaseLoadedGraph();
                return Fail("Addressables returned no SpatialOctreeAsset");
            }

            if (!asset.IsUsable)
            {
                var reason =
                    $"SpatialOctreeAsset '{asset.name}' is not usable (version={asset.Version}/{SpatialOctreeAsset.CurrentVersion}, nodes={asset.Nodes.Length}, neighborStarts={asset.NeighborStarts.Length}, neighborIndices={asset.NeighborIndices.Length})";
                ReleaseLoadedGraph();
                return Fail(reason);
            }

            if (!asset.TryValidateData(out var validationMessage))
            {
                ReleaseLoadedGraph();
                return Fail($"SpatialOctreeAsset '{asset.name}' failed structural validation: " +
                            validationMessage);
            }

            if (!SpatialOctreePerformanceBudget.TryValidate(asset, out var budgetMessage))
            {
                ReleaseLoadedGraph();
                return Fail($"SpatialOctreeAsset '{asset.name}' failed performance budgets: {budgetMessage}");
            }

            CurrentAsset = asset;
            var dynamicOccluders = SpatialDynamicOccluderCollection.Collect(scene, asset.BakeSettings);
            CurrentGraph = new SpatialOctreeGraph(asset, dynamicOccluders);
            if (CurrentGraph.IsUsable)
                return true;

            ReleaseLoadedGraph();
            return Fail($"SpatialOctreeGraph could not be built from asset '{asset.name}'");
        }

        public static void Clear()
        {
            ReleaseLoadedGraph();
            LastLoadFailureReason = string.Empty;
        }

        private static void ReleaseLoadedGraph()
        {
            CurrentGraph?.Dispose();
            CurrentGraph = null;
            CurrentAsset = null;

            if (_assetHandle.IsValid())
                Addressables.Release(_assetHandle);
            _assetHandle = default;
        }

        private static bool Fail(string reason)
        {
            LastLoadFailureReason = string.IsNullOrWhiteSpace(reason) ? "unknown spatial graph load failure" : reason;
            return false;
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.##},{value.y:0.##},{value.z:0.##})";
        }
    }
}