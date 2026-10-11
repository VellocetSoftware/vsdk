// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Maps;
using Vellocet.Sdk.Spatial;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    public readonly struct MapSpatialGraphArtifact
    {
        public MapSpatialGraphArtifact(string assetPath, string guid, string address, SpatialOctreeAsset asset)
        {
            AssetPath = assetPath ?? string.Empty;
            Guid = guid ?? string.Empty;
            Address = address ?? string.Empty;
            Asset = asset;
        }

        public string AssetPath { get; }
        public string Guid { get; }
        public string Address { get; }
        public SpatialOctreeAsset Asset { get; }
    }

    public static class MapSpatialGraphArtifacts
    {
        public const string RuntimeRootName = "MapRuntime";
        public const string AddressPrefix = "spatial/";
        public const string AssetSuffix = "_SpatialOctree.asset";

        private const string SpatialGraphReferencePropertyName = "spatialGraphReference";

        public static string ResolveAssetPath(string compiledScenePath, string mapId)
        {
            var compiledRoot = string.IsNullOrWhiteSpace(compiledScenePath)
                ? null
                : Path.GetDirectoryName(compiledScenePath)?.Replace('\\', '/');
            return ResolveAssetPathFromCompiledRoot(compiledRoot, mapId);
        }

        public static string ResolveAssetPathFromCompiledRoot(string compiledRoot, string mapId)
        {
            var normalizedMapId = ContentIdUtility.Sanitize(mapId);
            var normalizedRoot = NormalizeAssetPath(compiledRoot)?.TrimEnd('/');
            return string.IsNullOrWhiteSpace(normalizedRoot) || string.IsNullOrWhiteSpace(normalizedMapId)
                ? null
                : $"{normalizedRoot}/{normalizedMapId}{AssetSuffix}";
        }

        public static string ResolveAddress(string mapId)
        {
            var normalizedMapId = ContentIdUtility.Sanitize(mapId);
            return string.IsNullOrWhiteSpace(normalizedMapId) ? null : $"{AddressPrefix}{normalizedMapId}";
        }

        public static GameObject EnsureRuntimeRoot(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            var roots = scene.GetRootGameObjects();
            var primary = roots.FirstOrDefault(root => root && string.Equals(root.name, RuntimeRootName,
                StringComparison.Ordinal));
            if (!primary)
            {
                primary = new GameObject(RuntimeRootName);
                SceneManager.MoveGameObjectToScene(primary, scene);
            }

            foreach (var duplicate in roots.Where(root => root && root != primary &&
                                                          string.Equals(root.name, RuntimeRootName,
                                                              StringComparison.Ordinal)))
            {
                while (duplicate.transform.childCount > 0)
                    duplicate.transform.GetChild(0).SetParent(primary.transform, false);

                Object.DestroyImmediate(duplicate);
            }

            return primary;
        }

        public static bool TryRegisterCompiledDependency(MapCompiler.Result result, SpatialOctreeAsset asset,
            string expectedAddress, out MapSpatialGraphArtifact artifact, out string message)
        {
            artifact = default;
            if (result == null)
            {
                message = "Map compiler result is missing while registering the spatial graph.";
                return false;
            }

            if (!TryResolveArtifact(asset, expectedAddress, out artifact, out message))
                return false;

            result.AddRuntimeDependency(artifact.Guid, artifact.Address);
            message = $"Registered spatial graph runtime dependency '{artifact.Address}' -> '{artifact.AssetPath}'.";
            return true;
        }

        public static bool TryAssignRuntimeDependency(Scene scene, SpatialOctreeAsset asset, out string message)
        {
            message = string.Empty;
            if (!scene.IsValid())
            {
                message = "Compiled scene is invalid while assigning runtime dependencies.";
                return false;
            }

            if (!asset)
            {
                message = "Spatial octree asset is missing while assigning runtime dependencies.";
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(assetGuid))
            {
                message = $"Spatial octree asset guid could not be resolved from '{assetPath}'.";
                return false;
            }

            var dependencies = EnsureRuntimeDependenciesComponent(scene);
            if (!dependencies)
            {
                message = "MapRuntimeDependencies component could not be created.";
                return false;
            }

            if (!SetAssetReferenceGuid(dependencies, assetGuid, SpatialGraphReferencePropertyName))
            {
                message =
                    "MapRuntimeDependencies.spatialGraphReference could not be assigned. The serialized AssetReference layout may have changed.";
                return false;
            }

            EditorUtility.SetDirty(dependencies);
            EditorSceneManager.MarkSceneDirty(scene);

            message =
                $"Assigned scene runtime dependency '{RuntimeRootName}.spatialGraphReference' -> '{assetPath}'.";
            return true;
        }

        public static bool TryClearRuntimeDependency(Scene scene, out string message)
        {
            if (!scene.IsValid())
            {
                message = "Compiled scene is invalid while clearing the spatial graph dependency.";
                return false;
            }

            var cleared = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                foreach (var dependency in root.GetComponentsInChildren<MapRuntimeDependencies>(true))
                {
                    if (!dependency)
                        continue;
                    if (!TryGetAssetReferenceGuid(dependency, SpatialGraphReferencePropertyName, out var assetGuid))
                    {
                        message =
                            "MapRuntimeDependencies.spatialGraphReference could not be inspected. The serialized AssetReference layout may have changed.";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(assetGuid))
                        continue;
                    if (!SetAssetReferenceGuid(dependency, string.Empty, SpatialGraphReferencePropertyName))
                    {
                        message =
                            "MapRuntimeDependencies.spatialGraphReference could not be cleared. The serialized AssetReference layout may have changed.";
                        return false;
                    }

                    EditorUtility.SetDirty(dependency);
                    cleared++;
                }
            }

            if (cleared > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            message = cleared > 0
                ? $"Cleared {cleared} stale spatial graph reference(s); the cached asset was retained for a later bake."
                : "No spatial graph runtime reference was present; any cached asset was retained for a later bake.";
            return true;
        }

        public static bool TryValidateSkippedSceneRuntimeDependency(Scene scene, out string message)
        {
            if (!scene.IsValid())
            {
                message = "Compiled scene is invalid.";
                return false;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                foreach (var dependency in root.GetComponentsInChildren<MapRuntimeDependencies>(true))
                {
                    if (!dependency)
                        continue;
                    if (!TryGetAssetReferenceGuid(dependency, SpatialGraphReferencePropertyName, out var assetGuid))
                    {
                        message =
                            "MapRuntimeDependencies.spatialGraphReference could not be inspected. The serialized AssetReference layout may have changed.";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(assetGuid))
                        continue;

                    message =
                        "Spatial octree bake was skipped, but the compiled scene still has a spatialGraphReference.";
                    return false;
                }
            }

            message = "Spatial octree bake was skipped and the compiled scene has no stale spatial graph reference.";
            return true;
        }

        public static bool TryValidateSceneRuntimeDependency(Scene scene, string expectedMapId,
            string expectedSceneAddress, out MapSpatialGraphArtifact artifact, out string message)
        {
            artifact = default;
            message = string.Empty;
            if (!scene.IsValid())
            {
                message = "Compiled scene is invalid.";
                return false;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                var dependencies = root.GetComponentsInChildren<MapRuntimeDependencies>(true);
                foreach (var dependency in dependencies)
                {
                    if (!dependency)
                        continue;

                    if (!TryGetAssetReferenceGuid(dependency, SpatialGraphReferencePropertyName, out var assetGuid) ||
                        string.IsNullOrWhiteSpace(assetGuid))
                        continue;

                    var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                    var asset = AssetDatabase.LoadAssetAtPath<SpatialOctreeAsset>(assetPath);
                    var expectedAddress = ResolveAddress(expectedMapId);
                    if (!TryResolveArtifact(asset, expectedAddress, out artifact, out message))
                        return false;
                    if (!TryValidateBakedAsset(asset, expectedMapId, expectedSceneAddress, out message))
                        return false;

                    message = "Compiled scene has a valid spatial graph runtime dependency.";
                    return true;
                }
            }

            message = "Compiled scene has no MapRuntimeDependencies with a valid spatialGraphReference.";
            return false;
        }

        public static bool TryValidateCompiledResult(MapCompiler.Result result, string expectedMapId,
            string expectedSceneAddress, out string message)
        {
            message = string.Empty;
            if (result == null)
            {
                message = "Compile result is missing.";
                return false;
            }

            if (!TryValidateReceipt(result.Receipt, expectedMapId, expectedSceneAddress, out message))
                return false;

            if (result.Receipt.skipSpatialOctreeBake)
            {
                var skippedAddress = ResolveAddress(expectedMapId);
                if (result.RuntimeDependencies.Any(candidate =>
                        string.Equals(candidate.Address, skippedAddress, StringComparison.Ordinal)))
                {
                    message =
                        $"Compile result skipped the spatial octree bake but registered dependency '{skippedAddress}'.";
                    return false;
                }

                message = "Compile result intentionally omitted the spatial octree for development iteration.";
                return true;
            }

            var expectedAddress = ResolveAddress(expectedMapId);
            var dependency = result.RuntimeDependencies.FirstOrDefault(candidate =>
                string.Equals(candidate.Address, expectedAddress, StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(dependency.Guid))
            {
                message =
                    $"Compile result is missing the spatial graph runtime dependency '{expectedAddress}'.";
                return false;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(dependency.Guid);
            var asset = AssetDatabase.LoadAssetAtPath<SpatialOctreeAsset>(assetPath);
            if (!TryResolveArtifact(asset, expectedAddress, out var artifact, out message))
                return false;

            return TryValidateBakedAsset(artifact.Asset, expectedMapId, expectedSceneAddress, out message);
        }

        public static bool TryValidateReceipt(MapCompilationReceipt receipt, string expectedMapId,
            string expectedSceneAddress, out string message)
        {
            message = string.Empty;
            if (receipt == null)
            {
                message = "Compilation receipt is missing.";
                return false;
            }

            var normalizedMapId = ContentIdUtility.Sanitize(expectedMapId);
            if (!string.Equals(receipt.mapId, normalizedMapId, StringComparison.OrdinalIgnoreCase))
            {
                message = $"Compilation receipt map id is '{receipt.mapId}', expected '{normalizedMapId}'.";
                return false;
            }

            if (!string.Equals(receipt.sceneAddress, expectedSceneAddress, StringComparison.Ordinal))
            {
                message =
                    $"Compilation receipt scene address is '{receipt.sceneAddress}', expected '{expectedSceneAddress}'.";
                return false;
            }

            var bakePass = (receipt.passes ?? Array.Empty<MapCompilationReceipt.CompilePassRecord>())
                .FirstOrDefault(pass => string.Equals(pass?.name, "bake-spatial-octree", StringComparison.Ordinal));
            if (bakePass == null)
            {
                message = "Compilation receipt has no bake-spatial-octree pass.";
                return false;
            }

            if (!string.Equals(bakePass.status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                message = $"Compilation receipt spatial graph bake failed: {bakePass.message}";
                return false;
            }

            if (receipt.skipSpatialOctreeBake)
            {
                if (!string.IsNullOrWhiteSpace(receipt.spatialGraphAddress))
                {
                    message =
                        "Compilation receipt skipped the spatial octree bake but still declares a spatialGraphAddress.";
                    return false;
                }

                message = "Compilation receipt records an intentionally skipped spatial octree bake.";
                return true;
            }

            var expectedAddress = ResolveAddress(normalizedMapId);
            if (!string.Equals(receipt.spatialGraphAddress, expectedAddress, StringComparison.Ordinal))
            {
                message =
                    $"Compilation receipt spatialGraphAddress is '{receipt.spatialGraphAddress}', expected '{expectedAddress}'.";
                return false;
            }

            message = $"Compilation receipt declares spatial graph '{expectedAddress}'.";
            return true;
        }

        public static bool TryValidateBakedAsset(SpatialOctreeAsset asset, string expectedMapId,
            string expectedSceneAddress, out string message)
        {
            if (!asset)
            {
                message = "Spatial octree asset is missing or unloadable.";
                return false;
            }

            if (!asset.IsUsable)
            {
                message =
                    $"Spatial octree asset is not usable (version={asset.Version}/{SpatialOctreeAsset.CurrentVersion}, nodes={asset.Nodes.Length}, neighborStarts={asset.NeighborStarts.Length}, neighborIndices={asset.NeighborIndices.Length}).";
                return false;
            }

            if (!asset.TryValidateData(out var validationMessage))
            {
                message = $"Spatial octree structural validation failed: {validationMessage}";
                return false;
            }

            if (!SpatialOctreePerformanceBudget.TryValidate(asset, out var budgetMessage))
            {
                message = $"Spatial octree performance budget validation failed: {budgetMessage}";
                return false;
            }

            var normalizedMapId = ContentIdUtility.Sanitize(expectedMapId);
            if (!string.Equals(asset.MapId, normalizedMapId, StringComparison.OrdinalIgnoreCase))
            {
                message = $"Spatial octree map id is '{asset.MapId}', expected '{normalizedMapId}'.";
                return false;
            }

            if (!string.Equals(asset.SceneAddress, expectedSceneAddress, StringComparison.Ordinal))
            {
                message = $"Spatial octree scene address is '{asset.SceneAddress}', expected '{expectedSceneAddress}'.";
                return false;
            }

            message =
                $"Spatial octree asset is valid: map='{asset.MapId}', scene='{asset.SceneAddress}', " +
                $"nodes={asset.Nodes.Length}, traversable={asset.TraversableLeafCount}, {budgetMessage}.";
            return true;
        }

        private static bool TryResolveArtifact(SpatialOctreeAsset asset, string expectedAddress,
            out MapSpatialGraphArtifact artifact, out string message)
        {
            artifact = default;
            if (!asset)
            {
                message = "Spatial octree asset is missing or unloadable.";
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                message = $"Spatial octree asset path could not be resolved for '{asset.name}'.";
                return false;
            }

            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(assetGuid))
            {
                message = $"Spatial octree asset guid could not be resolved from '{assetPath}'.";
                return false;
            }

            var actualAddress = ResolveAddress(asset.MapId);
            if (!string.IsNullOrWhiteSpace(expectedAddress) &&
                !string.Equals(actualAddress, expectedAddress, StringComparison.Ordinal))
            {
                message = $"Spatial octree address is '{actualAddress}', expected '{expectedAddress}'.";
                return false;
            }

            artifact = new MapSpatialGraphArtifact(assetPath, assetGuid, actualAddress, asset);
            message = $"Spatial graph artifact resolved: '{actualAddress}' -> '{assetPath}'.";
            return true;
        }

        private static MapRuntimeDependencies EnsureRuntimeDependenciesComponent(Scene scene)
        {
            var runtimeRoot = EnsureRuntimeRoot(scene);
            if (!runtimeRoot)
                return null;

            var primary = runtimeRoot.GetComponent<MapRuntimeDependencies>();
            if (!primary)
                primary = runtimeRoot.AddComponent<MapRuntimeDependencies>();

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                var dependencies = root.GetComponentsInChildren<MapRuntimeDependencies>(true);
                foreach (var candidate in dependencies)
                {
                    if (!candidate || candidate == primary)
                        continue;

                    Object.DestroyImmediate(candidate);
                }
            }

            return primary;
        }

        private static bool SetAssetReferenceGuid(Object target, string assetGuid, string propertyName)
        {
            if (!target || string.IsNullOrWhiteSpace(propertyName))
                return false;

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            var guidProperty = property?.FindPropertyRelative("m_AssetGUID");
            if (guidProperty == null || guidProperty.propertyType != SerializedPropertyType.String)
                return false;

            guidProperty.stringValue = assetGuid ?? string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool TryGetAssetReferenceGuid(Object target, string propertyName, out string assetGuid)
        {
            assetGuid = string.Empty;
            if (!target || string.IsNullOrWhiteSpace(propertyName))
                return false;

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            var guidProperty = property?.FindPropertyRelative("m_AssetGUID");
            if (guidProperty == null || guidProperty.propertyType != SerializedPropertyType.String)
                return false;

            assetGuid = guidProperty.stringValue;
            return true;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/');
        }
    }
}
#endif