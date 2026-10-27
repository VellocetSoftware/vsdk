// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapBakeArtifactFileMirror
    {
        internal static bool CopyAuthoringToCompiled(string sourceScenePath, string compiledScenePath,
            out Result result, out string message)
        {
            if (!TryBuildContext(sourceScenePath, compiledScenePath, sourceScenePath,
                    out var context, out result, out message))
                return false;

            var options = new MirrorOptions(true, false);
            if (!Mirror(context, options, out result, out message))
                return false;

            AssetDatabase.ImportAsset(compiledScenePath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        internal static bool CopyCompiledToAuthoring(string compiledScenePath, string sourceScenePath,
            out Result result, out string message)
        {
            if (!TryBuildContext(compiledScenePath, sourceScenePath, sourceScenePath,
                    out var context, out result, out message))
                return false;

            return Mirror(context, MirrorOptions.GeneratedBakeArtifactsOnly, out result, out message);
        }

        private static bool TryBuildContext(string fromScenePath, string toScenePath, string authoringScenePath,
            out MirrorContext context,
            out Result result, out string message)
        {
            context = default;
            result = new Result();
            message = string.Empty;

            fromScenePath = MapEditorPathUtility.NormalizeAssetPath(fromScenePath);
            toScenePath = MapEditorPathUtility.NormalizeAssetPath(toScenePath);
            if (string.IsNullOrWhiteSpace(fromScenePath) || string.IsNullOrWhiteSpace(toScenePath))
            {
                message = "Source or target scene path is missing.";
                return false;
            }

            context = new MirrorContext(fromScenePath, toScenePath, authoringScenePath);
            return true;
        }

        private static bool Mirror(MirrorContext context, MirrorOptions options, out Result result,
            out string message)
        {
            result = new Result();
            message = string.Empty;

            var fromFolder = MapEditorPathUtility.GetSceneDataFolderAssetPath(context.FromScenePath);
            var toFolder = MapEditorPathUtility.GetSceneDataFolderAssetPath(context.ToScenePath);
            if (string.IsNullOrWhiteSpace(fromFolder) || string.IsNullOrWhiteSpace(toFolder))
                return true;

            var fromAbsoluteFolder = MapEditorPathUtility.ToAbsolutePath(fromFolder);
            if (!Directory.Exists(fromAbsoluteFolder))
                return true;

            Directory.CreateDirectory(MapEditorPathUtility.ToAbsolutePath(toFolder));
            AssetDatabase.ImportAsset(toFolder, ImportAssetOptions.ForceUpdate);

            foreach (var sourceAbsolutePath in Directory.GetFiles(fromAbsoluteFolder, "*",
                         SearchOption.AllDirectories))
            {
                if (sourceAbsolutePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = Path.GetFileName(sourceAbsolutePath);
                if (!options.CopyWholeSceneDataFolder && !IsBakeArtifact(fileName, context.FromSceneName))
                    continue;

                var relativePath = MapEditorPathUtility.MakeRelativePath(fromAbsoluteFolder, sourceAbsolutePath);
                var sourceAssetPath = MapEditorPathUtility.NormalizeAssetPath(Path.Combine(fromFolder, relativePath));
                if (IsSharedPayload(fileName, context.FromSceneName))
                {
                    if (!MovePayloadToSharedFolder(sourceAssetPath, relativePath, context,
                            out var sharedPath, out message))
                        return false;

                    // Moving preserves the GUID, including references inside LightingData and APV assets.
                    // The old path no longer resolves to a GUID; baking-set remapping needs the new path.
                    result.MapAsset(sharedPath, sharedPath);
                    result.Relocated++;
                    continue;
                }

                var targetRelativePath = RenameBakeArtifactPath(relativePath, context.FromSceneName,
                    context.ToSceneName);
                var targetAssetPath =
                    MapEditorPathUtility.NormalizeAssetPath(Path.Combine(toFolder, targetRelativePath));

                if (!CopyAsset(sourceAssetPath, targetAssetPath, out message))
                    return false;

                AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceUpdate);

                result.MapAsset(sourceAssetPath, targetAssetPath);
                result.Copied++;
            }

            // All probe payloads must exist before reconnecting the copied baking set.
            foreach (var pair in result.AssetMap)
            {
                if (!IsProbeVolumeBakingSet(Path.GetFileName(pair.Key), context.FromSceneName))
                    continue;
                if (result.Relocated > 0)
                    MapProbeVolumeUtility.RefreshStreamableAssetPaths(AssetDatabase.LoadMainAssetAtPath(pair.Key));
                if (!MapProbeVolumeUtility.TryPrepareBakingSetAsset(pair.Value, context.ToSceneGuid,
                        result.AssetMap, out message))
                    return false;
            }

            if (options.DeleteStaleTargetFiles)
                result.Deleted = DeleteStaleBakeArtifacts(toFolder, context.ToSceneName, result);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return true;
        }

        private static bool MovePayloadToSharedFolder(string sourcePath, string relativePath,
            MirrorContext context, out string sharedPath, out string message)
        {
            sharedPath = string.Empty;
            message = string.Empty;
            // Payloads live with the compiled scene, which is replaced as a whole on every compile; an authoring
            // scene that keeps its lighting references them by GUID, so nothing is duplicated or left behind.
            var compiledScenePath = context.FromScenePath == context.AuthoringScenePath ? context.ToScenePath : context.FromScenePath;
            var folder = MapEditorPathUtility.GetSceneDataFolderAssetPath(compiledScenePath);
            sharedPath = MapEditorPathUtility.NormalizeAssetPath(Path.Combine(folder, relativePath));
            if (sharedPath == sourcePath) return true;
            if (!MapEditorPathUtility.EnsureAssetFolder(Path.GetDirectoryName(sharedPath)))
            {
                message = $"Could not create compiled bake folder: {folder}";
                return false;
            }

            sharedPath = AssetDatabase.GenerateUniqueAssetPath(sharedPath);
            message = AssetDatabase.MoveAsset(sourcePath, sharedPath);
            return string.IsNullOrEmpty(message);
        }

        private static bool CopyAsset(string sourceAssetPath, string targetAssetPath, out string message)
        {
            message = string.Empty;
            var sourceAbsolutePath = MapEditorPathUtility.ToAbsolutePath(sourceAssetPath);
            var targetAbsolutePath = MapEditorPathUtility.ToAbsolutePath(targetAssetPath);
            if (!File.Exists(sourceAbsolutePath))
            {
                message = $"Bake artifact is missing: {sourceAssetPath}";
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetAbsolutePath) ?? string.Empty);
            if (File.Exists(targetAbsolutePath))
            {
                File.Copy(sourceAbsolutePath, targetAbsolutePath, true);
                return true;
            }

            if (AssetDatabase.CopyAsset(sourceAssetPath, targetAssetPath))
                return true;

            try
            {
                File.Copy(sourceAbsolutePath, targetAbsolutePath, true);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Could not copy bake artifact '{sourceAssetPath}' to '{targetAssetPath}': {ex.Message}";
                return false;
            }
        }

        private static int DeleteStaleBakeArtifacts(string folder, string sceneName, Result result)
        {
            var absoluteFolder = MapEditorPathUtility.ToAbsolutePath(folder);
            if (!Directory.Exists(absoluteFolder))
                return 0;

            var deleted = 0;
            foreach (var absolutePath in Directory.GetFiles(absoluteFolder, "*", SearchOption.AllDirectories))
            {
                if (absolutePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = Path.GetFileName(absolutePath);
                if (!IsBakeArtifact(fileName, sceneName))
                    continue;

                // Shared payloads may be referenced by scenes outside this sync; copying metadata never deletes them.
                if (IsSharedPayload(fileName, sceneName))
                    continue;

                var relativePath = MapEditorPathUtility.MakeRelativePath(absoluteFolder, absolutePath);
                var assetPath = MapEditorPathUtility.NormalizeAssetPath(Path.Combine(folder, relativePath));
                if (result.ContainsTargetPath(assetPath))
                    continue;

                if (AssetDatabase.DeleteAsset(assetPath))
                    deleted++;
            }

            return deleted;
        }

        private static bool IsSharedPayload(string fileName, string sceneName)
        {
            if (!IsBakeArtifact(fileName, sceneName))
                return false;

            var extension = Path.GetExtension(fileName);
            return extension.Equals(".exr", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".hdr", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".cubemap", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bytes", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBakeArtifact(string fileName, string sceneName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            if (string.Equals(fileName, "LightingData.asset", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "OcclusionCullingData.asset", StringComparison.OrdinalIgnoreCase))
                return true;

            if (fileName.StartsWith("Lightmap-", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("ReflectionProbe-", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("NavMesh", StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrWhiteSpace(sceneName) &&
                   fileName.StartsWith(sceneName + " Baking Set", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProbeVolumeBakingSet(string fileName, string sceneName)
        {
            return !string.IsNullOrWhiteSpace(fileName) &&
                   fileName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(sceneName) &&
                   fileName.StartsWith(sceneName + " Baking Set", StringComparison.OrdinalIgnoreCase);
        }

        private static string RenameBakeArtifact(string fileName, string fromSceneName, string toSceneName)
        {
            if (!string.IsNullOrWhiteSpace(fromSceneName) &&
                !string.IsNullOrWhiteSpace(toSceneName) &&
                fileName.StartsWith(fromSceneName + " Baking Set", StringComparison.OrdinalIgnoreCase))
                return toSceneName + fileName.Substring(fromSceneName.Length);

            return fileName;
        }

        private static string RenameBakeArtifactPath(string relativePath, string fromSceneName, string toSceneName)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            var segments = MapEditorPathUtility.NormalizeAssetPath(relativePath).Split('/');
            for (var i = 0; i < segments.Length; i++)
                segments[i] = RenameBakeArtifact(segments[i], fromSceneName, toSceneName);

            return string.Join("/", segments);
        }

        internal sealed class Result
        {
            private readonly HashSet<string> _targetPaths = new(StringComparer.OrdinalIgnoreCase);

            internal Dictionary<string, string> AssetMap { get; } = new(StringComparer.OrdinalIgnoreCase);
            internal int Copied { get; set; }
            internal int Relocated { get; set; }
            internal int Deleted { get; set; }

            internal void MapAsset(string sourceAssetPath, string targetAssetPath)
            {
                AssetMap[sourceAssetPath] = targetAssetPath;
                _targetPaths.Add(targetAssetPath);
            }

            internal bool ContainsTargetPath(string targetAssetPath)
            {
                return _targetPaths.Contains(targetAssetPath);
            }
        }

        private readonly struct MirrorContext
        {
            internal MirrorContext(string fromScenePath, string toScenePath, string authoringScenePath)
            {
                FromScenePath = fromScenePath;
                ToScenePath = toScenePath;
                FromSceneName = Path.GetFileNameWithoutExtension(fromScenePath);
                ToSceneName = Path.GetFileNameWithoutExtension(toScenePath);
                ToSceneGuid = AssetDatabase.AssetPathToGUID(toScenePath);
                AuthoringScenePath = MapEditorPathUtility.NormalizeAssetPath(authoringScenePath);
            }

            internal string FromScenePath { get; }
            internal string ToScenePath { get; }
            internal string FromSceneName { get; }
            internal string ToSceneName { get; }
            internal string ToSceneGuid { get; }
            internal string AuthoringScenePath { get; }
        }

        private readonly struct MirrorOptions
        {
            internal static MirrorOptions GeneratedBakeArtifactsOnly => new(false, true);

            internal MirrorOptions(bool copyWholeSceneDataFolder, bool deleteStaleTargetFiles)
            {
                CopyWholeSceneDataFolder = copyWholeSceneDataFolder;
                DeleteStaleTargetFiles = deleteStaleTargetFiles;
            }

            internal bool CopyWholeSceneDataFolder { get; }
            internal bool DeleteStaleTargetFiles { get; }
        }
    }
}
#endif
