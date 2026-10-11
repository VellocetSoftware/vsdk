// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Tooling;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapProBuilderFinalizationUtility
    {
        private const string ColliderMaterialPath =
            "Packages/com.unity.probuilder/Content/Resources/Materials/Collider.mat";

        private const string GeneratedMeshFolderName = "GeneratedMeshes";

        internal static bool Finalize(Scene scene, string compiledScenePath, out string message)
        {
            return Finalize(scene, compiledScenePath, FinalizationOptions.Default, out message);
        }

        internal static bool Finalize(Scene scene, string compiledScenePath, FinalizationOptions options,
            out string message)
        {
            message = string.Empty;
            if (!scene.IsValid())
            {
                message = "Compiled scene is invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(compiledScenePath))
            {
                message = "Compiled scene path is missing.";
                return false;
            }

            EnsureGeneratedMeshFolder(compiledScenePath, out var generatedMeshFolderPath);

            var summary = new Summary();
            var remappedMeshes = new Dictionary<MeshContentKey, Mesh>();
            var remappedBySourceMesh = new Dictionary<SourceMeshKey, Mesh>();
            var referencedGeneratedMeshPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dirty = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                if (options.ConfigureToolVisuals)
                {
                    var renderers = root.GetComponentsInChildren<Renderer>(true);
                    for (var i = 0; i < renderers.Length; i++)
                    {
                        var renderer = renderers[i];
                        if (!renderer)
                            continue;

                        if (!UsesColliderMaterial(renderer))
                            continue;

                        if (EnsureToolVisual(renderer))
                        {
                            summary.ToolVisualsConfigured++;
                            dirty = true;
                        }
                    }
                }
                else
                {
                    summary.ToolVisualsSkipped = true;
                }

                var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
                for (var i = 0; i < meshFilters.Length; i++)
                {
                    var meshFilter = meshFilters[i];
                    if (!meshFilter || !meshFilter.sharedMesh)
                        continue;

                    if (!TryExternalizeEmbeddedProBuilderMesh(
                            meshFilter.sharedMesh,
                            generatedMeshFolderPath,
                            remappedMeshes,
                            remappedBySourceMesh,
                            referencedGeneratedMeshPaths,
                            MeshUsage.Render,
                            out var externalMesh,
                            out var created))
                        continue;

                    meshFilter.sharedMesh = externalMesh;
                    summary.MeshReferencesRemapped++;
                    if (created)
                        summary.RenderMeshAssetsCreated++;
                    dirty = true;
                }

                var meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
                for (var i = 0; i < meshColliders.Length; i++)
                {
                    var meshCollider = meshColliders[i];
                    if (!meshCollider || !meshCollider.sharedMesh)
                        continue;

                    if (!TryExternalizeEmbeddedProBuilderMesh(
                            meshCollider.sharedMesh,
                            generatedMeshFolderPath,
                            remappedMeshes,
                            remappedBySourceMesh,
                            referencedGeneratedMeshPaths,
                            MeshUsage.Collider,
                            out var externalMesh,
                            out var created))
                        continue;

                    meshCollider.sharedMesh = externalMesh;
                    summary.MeshReferencesRemapped++;
                    if (created)
                        summary.ColliderMeshAssetsCreated++;
                    dirty = true;
                }
            }

            if (options.PruneStaleGeneratedMeshes)
            {
                if (PruneUnreferencedGeneratedMeshes(generatedMeshFolderPath, referencedGeneratedMeshPaths,
                        ref summary))
                    dirty = true;
            }
            else
            {
                summary.MeshPruneSkipped = true;
            }

            if (dirty)
            {
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
            }

            message = summary.BuildMessage();
            return true;
        }

        private static bool EnsureToolVisual(Renderer renderer)
        {
            var gameObject = renderer.gameObject;
            if (!gameObject)
                return false;

            var changed = false;
            var toolVisual = gameObject.GetComponent<ToolVisual>();
            if (!toolVisual)
            {
                toolVisual = gameObject.AddComponent<ToolVisual>();
                changed = true;
            }

            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer < 0)
                ignoreRaycastLayer = 2;
            if (gameObject.layer != ignoreRaycastLayer)
            {
                gameObject.layer = ignoreRaycastLayer;
                changed = true;
            }

            toolVisual.Refresh();
            return changed;
        }

        private static bool UsesColliderMaterial(Renderer renderer)
        {
            if (!renderer)
                return false;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return false;

            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (!material)
                    continue;

                var materialPath = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrWhiteSpace(materialPath))
                    continue;

                materialPath = materialPath.Replace('\\', '/');
                if (string.Equals(materialPath, ColliderMaterialPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool TryExternalizeEmbeddedProBuilderMesh(Mesh mesh, string generatedMeshFolderPath,
            IDictionary<MeshContentKey, Mesh> remappedMeshes,
            IDictionary<SourceMeshKey, Mesh> remappedBySourceMesh,
            ISet<string> referencedGeneratedMeshPaths,
            MeshUsage usage, out Mesh externalMesh,
            out bool created)
        {
            externalMesh = null;
            created = false;

            if (!mesh || !IsEmbeddedProBuilderMesh(mesh))
                return false;

            var sourceKey = new SourceMeshKey(mesh.GetInstanceID(), usage);
            if (remappedBySourceMesh != null &&
                remappedBySourceMesh.TryGetValue(sourceKey, out externalMesh) &&
                externalMesh)
            {
                TrackReferencedGeneratedMesh(externalMesh, referencedGeneratedMeshPaths);
                return true;
            }

            var key = BuildMeshContentKey(mesh, usage);
            if (remappedMeshes.TryGetValue(key, out externalMesh) && externalMesh)
            {
                if (remappedBySourceMesh != null) remappedBySourceMesh[sourceKey] = externalMesh;
                TrackReferencedGeneratedMesh(externalMesh, referencedGeneratedMeshPaths);
                return true;
            }

            var meshAssetName = BuildMeshAssetName(mesh.name, key.HashHex, usage);
            var assetPath = $"{generatedMeshFolderPath}/{SanitizeFileName(meshAssetName)}.asset";
            externalMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (externalMesh)
            {
                if (SetMeshReadableState(externalMesh, usage != MeshUsage.Render))
                    EditorUtility.SetDirty(externalMesh);
                remappedMeshes[key] = externalMesh;
                if (remappedBySourceMesh != null) remappedBySourceMesh[sourceKey] = externalMesh;
                TrackReferencedGeneratedMesh(externalMesh, referencedGeneratedMeshPaths);
                return true;
            }

            var clone = Object.Instantiate(mesh);
            clone.name = meshAssetName;
            AssetDatabase.CreateAsset(clone, assetPath);
            externalMesh = clone;
            if (SetMeshReadableState(externalMesh, usage != MeshUsage.Render))
                EditorUtility.SetDirty(externalMesh);

            remappedMeshes[key] = externalMesh;
            if (remappedBySourceMesh != null) remappedBySourceMesh[sourceKey] = externalMesh;
            TrackReferencedGeneratedMesh(externalMesh, referencedGeneratedMeshPaths);
            created = true;
            return true;
        }

        private static void TrackReferencedGeneratedMesh(Mesh mesh, ISet<string> referencedGeneratedMeshPaths)
        {
            if (!mesh || referencedGeneratedMeshPaths == null)
                return;

            var assetPath = AssetDatabase.GetAssetPath(mesh)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(assetPath))
                referencedGeneratedMeshPaths.Add(assetPath);
        }

        private static bool SetMeshReadableState(Mesh mesh, bool isReadable)
        {
            if (!mesh)
                return false;

            var serializedObject = new SerializedObject(mesh);
            var readableProperty = serializedObject.FindProperty("m_IsReadable");
            if (readableProperty == null)
                return false;

            if (readableProperty.boolValue == isReadable)
                return false;

            readableProperty.boolValue = isReadable;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static MeshContentKey BuildMeshContentKey(Mesh mesh, MeshUsage usage)
        {
            var builder = new StringBuilder(2048);
            builder.Append((int)usage).Append('|');
            builder.Append(mesh.vertexCount).Append('|');
            builder.Append(mesh.subMeshCount).Append('|');

            AppendBounds(builder, mesh.bounds);
            AppendVector3Array(builder, mesh.vertices);
            AppendVector3Array(builder, mesh.normals);
            AppendVector4Array(builder, mesh.tangents);
            AppendColorArray(builder, mesh.colors);
            AppendVector2Array(builder, mesh.uv);
            AppendVector2Array(builder, mesh.uv2);
            AppendVector2Array(builder, mesh.uv3);
            AppendVector2Array(builder, mesh.uv4);

            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                builder.Append("sub|").Append((int)mesh.GetTopology(i)).Append('|');
                var indices = mesh.GetIndices(i);
                for (var j = 0; j < indices.Length; j++)
                    builder.Append(indices[j]).Append(',');
                builder.Append('|');
            }

            return new MeshContentKey(Hash128.Compute(builder.ToString()).ToString(), usage);
        }

        private static string BuildMeshAssetName(string meshName, string hashHex, MeshUsage usage)
        {
            if (string.IsNullOrWhiteSpace(meshName))
                meshName = "pb_mesh";

            meshName = NormalizeMeshBaseName(meshName);
            meshName = SanitizeFileName(meshName);
            if (meshName.Length > 32)
                meshName = meshName.Substring(0, 32);

            var shortHash = string.IsNullOrWhiteSpace(hashHex)
                ? "unknown"
                : hashHex.Substring(0, Mathf.Min(12, hashHex.Length));

            return usage switch
            {
                MeshUsage.Render => meshName + "__" + shortHash + "__render",
                MeshUsage.Collider => meshName + "__" + shortHash + "__collider",
                _ => meshName
            };
        }

        private static string NormalizeMeshBaseName(string meshName)
        {
            if (string.IsNullOrWhiteSpace(meshName))
                return "pb_mesh";

            const string cloneToken = "(Clone)";
            while (meshName.IndexOf(cloneToken, StringComparison.OrdinalIgnoreCase) >= 0)
                meshName = meshName.Replace(cloneToken, string.Empty, StringComparison.OrdinalIgnoreCase);

            meshName = meshName.Trim('_', '-', ' ');
            return string.IsNullOrWhiteSpace(meshName) ? "pb_mesh" : meshName;
        }

        private static void AppendBounds(StringBuilder builder, Bounds bounds)
        {
            builder.Append("b|");
            AppendVector3(builder, bounds.center);
            AppendVector3(builder, bounds.extents);
        }

        private static void AppendVector2Array(StringBuilder builder, Vector2[] values)
        {
            builder.Append("v2|").Append(values?.Length ?? 0).Append('|');
            if (values == null)
                return;

            for (var i = 0; i < values.Length; i++)
                AppendVector2(builder, values[i]);
        }

        private static void AppendVector3Array(StringBuilder builder, Vector3[] values)
        {
            builder.Append("v3|").Append(values?.Length ?? 0).Append('|');
            if (values == null)
                return;

            for (var i = 0; i < values.Length; i++)
                AppendVector3(builder, values[i]);
        }

        private static void AppendVector4Array(StringBuilder builder, Vector4[] values)
        {
            builder.Append("v4|").Append(values?.Length ?? 0).Append('|');
            if (values == null)
                return;

            for (var i = 0; i < values.Length; i++)
                AppendVector4(builder, values[i]);
        }

        private static void AppendColorArray(StringBuilder builder, Color[] values)
        {
            builder.Append("c|").Append(values?.Length ?? 0).Append('|');
            if (values == null)
                return;

            for (var i = 0; i < values.Length; i++)
            {
                var color = values[i];
                builder.Append(BitConverter.SingleToInt32Bits(color.r)).Append(',');
                builder.Append(BitConverter.SingleToInt32Bits(color.g)).Append(',');
                builder.Append(BitConverter.SingleToInt32Bits(color.b)).Append(',');
                builder.Append(BitConverter.SingleToInt32Bits(color.a)).Append(';');
            }
        }

        private static void AppendVector2(StringBuilder builder, Vector2 value)
        {
            builder.Append(BitConverter.SingleToInt32Bits(value.x)).Append(',');
            builder.Append(BitConverter.SingleToInt32Bits(value.y)).Append(';');
        }

        private static void AppendVector3(StringBuilder builder, Vector3 value)
        {
            builder.Append(BitConverter.SingleToInt32Bits(value.x)).Append(',');
            builder.Append(BitConverter.SingleToInt32Bits(value.y)).Append(',');
            builder.Append(BitConverter.SingleToInt32Bits(value.z)).Append(';');
        }

        private static void AppendVector4(StringBuilder builder, Vector4 value)
        {
            builder.Append(BitConverter.SingleToInt32Bits(value.x)).Append(',');
            builder.Append(BitConverter.SingleToInt32Bits(value.y)).Append(',');
            builder.Append(BitConverter.SingleToInt32Bits(value.z)).Append(',');
            builder.Append(BitConverter.SingleToInt32Bits(value.w)).Append(';');
        }

        private static bool IsEmbeddedProBuilderMesh(Mesh mesh)
        {
            if (!mesh)
                return false;

            if (!mesh.name.StartsWith("pb_Mesh", StringComparison.OrdinalIgnoreCase))
                return false;

            var assetPath = AssetDatabase.GetAssetPath(mesh);
            return string.IsNullOrWhiteSpace(assetPath);
        }

        private static void EnsureGeneratedMeshFolder(string compiledScenePath, out string generatedMeshFolderPath)
        {
            var compiledSceneDirectory = Path.GetDirectoryName(compiledScenePath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(compiledSceneDirectory))
            {
                generatedMeshFolderPath = Constants.TempCompiledSceneDir + "/" + GeneratedMeshFolderName;
                return;
            }

            generatedMeshFolderPath = compiledSceneDirectory + "/" + GeneratedMeshFolderName;
            if (AssetDatabase.IsValidFolder(generatedMeshFolderPath))
                return;

            EnsureFolder(compiledSceneDirectory, GeneratedMeshFolderName);
        }

        private static void EnsureFolder(string parentFolder, string childFolderName)
        {
            if (AssetDatabase.IsValidFolder(parentFolder + "/" + childFolderName))
                return;

            AssetDatabase.CreateFolder(parentFolder, childFolderName);
        }

        private static bool PruneUnreferencedGeneratedMeshes(string generatedMeshFolderPath,
            ISet<string> referencedGeneratedMeshPaths, ref Summary summary)
        {
            if (string.IsNullOrWhiteSpace(generatedMeshFolderPath) ||
                !AssetDatabase.IsValidFolder(generatedMeshFolderPath))
                return false;

            var deletedAny = false;
            var guids = AssetDatabase.FindAssets("t:Mesh", new[] { generatedMeshFolderPath });
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i])?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                if (referencedGeneratedMeshPaths != null && referencedGeneratedMeshPaths.Contains(assetPath))
                    continue;

                if (!AssetDatabase.DeleteAsset(assetPath))
                    continue;

                summary.GeneratedMeshAssetsPruned++;
                deletedAny = true;
            }

            return deletedAny;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Mesh";

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidChar, '_');

            return value;
        }

        internal readonly struct FinalizationOptions
        {
            internal FinalizationOptions(bool configureToolVisuals, bool pruneStaleGeneratedMeshes)
            {
                ConfigureToolVisuals = configureToolVisuals;
                PruneStaleGeneratedMeshes = pruneStaleGeneratedMeshes;
            }

            internal bool ConfigureToolVisuals { get; }
            internal bool PruneStaleGeneratedMeshes { get; }
            internal static FinalizationOptions Default => new(true, true);
        }

        private struct Summary
        {
            internal int ToolVisualsConfigured;
            internal int RenderMeshAssetsCreated;
            internal int ColliderMeshAssetsCreated;
            internal int MeshReferencesRemapped;
            internal int GeneratedMeshAssetsPruned;
            internal bool ToolVisualsSkipped;
            internal bool MeshPruneSkipped;

            internal string BuildMessage()
            {
                var builder = new StringBuilder(256);
                if (ToolVisualsSkipped)
                    builder.Append("Skipped tool visual configuration, ");
                else
                    builder.Append($"Configured {ToolVisualsConfigured} tool visual renderer(s), ");

                builder.Append(
                    $"created {RenderMeshAssetsCreated} render mesh asset(s) and {ColliderMeshAssetsCreated} collider mesh asset(s), ");
                if (MeshPruneSkipped)
                    builder.Append("skipped stale generated mesh pruning, ");
                else
                    builder.Append($"pruned {GeneratedMeshAssetsPruned} stale generated mesh asset(s), ");

                builder.Append($"and remapped {MeshReferencesRemapped} ProBuilder mesh reference(s).");
                return builder.ToString();
            }
        }

        private enum MeshUsage
        {
            Render,
            Collider
        }

        private readonly struct MeshContentKey : IEquatable<MeshContentKey>
        {
            internal MeshContentKey(string hashHex, MeshUsage usage)
            {
                HashHex = hashHex ?? string.Empty;
                Usage = usage;
            }

            internal string HashHex { get; }
            private MeshUsage Usage { get; }

            public bool Equals(MeshContentKey other)
            {
                return string.Equals(HashHex, other.HashHex, StringComparison.Ordinal) && Usage == other.Usage;
            }

            public override bool Equals(object obj)
            {
                return obj is MeshContentKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((HashHex != null ? HashHex.GetHashCode() : 0) * 397) ^ (int)Usage;
                }
            }
        }

        private readonly struct SourceMeshKey : IEquatable<SourceMeshKey>
        {
            internal SourceMeshKey(int sourceInstanceId, MeshUsage usage)
            {
                SourceInstanceId = sourceInstanceId;
                Usage = usage;
            }

            private int SourceInstanceId { get; }
            private MeshUsage Usage { get; }

            public bool Equals(SourceMeshKey other)
            {
                return SourceInstanceId == other.SourceInstanceId && Usage == other.Usage;
            }

            public override bool Equals(object obj)
            {
                return obj is SourceMeshKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SourceInstanceId * 397) ^ (int)Usage;
                }
            }
        }
    }
}
#endif