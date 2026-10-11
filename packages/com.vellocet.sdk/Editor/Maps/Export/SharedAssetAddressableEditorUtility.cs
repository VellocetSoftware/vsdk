// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Vellocet.Sdk.Editor.ContentLinking;
using Vellocet.Sdk.Editor.Tools;
using Vellocet.Sdk.Utilities;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class SharedAssetAddressableEditorUtility
    {
        private const string ChangeVersionStateKey =
            "Vellocet.Sdk.Editor.SharedAssets.ChangeVersion";

        private const string LastNormalizedVersionStateKey =
            "Vellocet.Sdk.Editor.SharedAssets.LastNormalizedVersion";

        private const string LastNormalizedSettingsPathStateKey =
            "Vellocet.Sdk.Editor.SharedAssets.LastNormalizedSettingsPath";

        private const string AddressablesDataRoot = "Assets/AddressableAssetsData/";

        private static bool _missingContentSetupWarningLogged;

        public static bool EnsureSdkSharedAssets(AddressableAssetSettings settings)
        {
            return EnsureSdkSharedAssets(settings, false);
        }

        internal static bool TryValidateSdkContentReady(out string error)
        {
            error = string.Empty;
            if (SdkHost.IsBaseProject)
                return true;

            if (!SdkContentOwnershipStore.StateExists)
            {
                error =
                    "SDK content setup is incomplete. Open SDK Workbench and run Initialize SDK Content before compiling maps.";
                return false;
            }

            return SdkContentOwnershipStore.TryValidateState(out error);
        }

        internal static bool EnsureSdkSharedAssets(AddressableAssetSettings settings, bool force)
        {
            if (!settings)
                return false;

            var group = EnsureSharedGroup(settings, out var createdGroup);
            if (!group)
                return false;

            if (!createdGroup && !force && !ShouldRunNormalization(settings))
                return false;

            var ownedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!SdkHost.IsBaseProject)
            {
                if (!SdkContentOwnershipStore.StateExists)
                {
                    if (!_missingContentSetupWarningLogged)
                    {
                        Debug.LogWarning(
                            "[SDK Content] Setup has not been initialized yet: " +
                            SdkContentOwnershipStore.StateProjectRelativePath +
                            ". The Base Addressables group was left unchanged. Run Initialize SDK Content before compiling maps.");
                        _missingContentSetupWarningLogged = true;
                    }

                    return createdGroup;
                }

                if (!SdkContentOwnershipStore.TryGetOwnedAssetPaths(out ownedAssetPaths))
                {
                    Debug.LogError(
                        "[SDK Content] Ownership state is invalid: " +
                        SdkContentOwnershipStore.StateProjectRelativePath +
                        ". Shared Addressables were left unchanged to avoid claiming project-owned assets.");
                    return createdGroup;
                }

                _missingContentSetupWarningLogged = false;
            }
            else
            {
                var roots = Constants.SdkSourceAssetRoots.Where(AssetDatabase.IsValidFolder).ToArray();
                if (roots.Length > 0)
                    ownedAssetPaths.UnionWith(AssetDatabase.FindAssets("", roots)
                        .Select(AssetDatabase.GUIDToAssetPath).Where(path => !AssetDatabase.IsValidFolder(path)));
                foreach (var guid in AssetDatabase.FindAssets("t:VertexAssetCatalogProfile"))
                {
                    var profile = AssetDatabase.LoadAssetAtPath<VertexAssetCatalogProfile>(AssetDatabase.GUIDToAssetPath(guid));
                    ownedAssetPaths.UnionWith(profile.EnumerateAssets()
                        .Where(asset => asset.Rule.ownership == "base" && asset.Path.StartsWith("Assets/", StringComparison.Ordinal))
                        .Select(asset => asset.Path));
                }
            }
            ownedAssetPaths.Add(VertexDeveloperTextureImporter.AssetPath);
            var changed = EnsureOwnedAssetEntries(settings, group, ownedAssetPaths);
            MarkNormalizationApplied(settings);
            return createdGroup || changed;
        }

        public static AddressableAssetGroup EnsureSharedGroup(AddressableAssetSettings settings)
        {
            return EnsureSharedGroup(settings, out _);
        }

        private static AddressableAssetGroup EnsureSharedGroup(AddressableAssetSettings settings,
            out bool createdGroup)
        {
            createdGroup = false;
            if (!settings)
                return null;

            var group = settings.FindGroup(Constants.BaseGroupName);
            if (!group)
            {
                group = settings.CreateGroup(Constants.BaseGroupName, false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                createdGroup = true;
            }

            if (!group.HasSchema<BundledAssetGroupSchema>())
                group.AddSchema<BundledAssetGroupSchema>();
            if (!group.HasSchema<ContentUpdateGroupSchema>())
                group.AddSchema<ContentUpdateGroupSchema>();

            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            if (bundle != null)
            {
                bundle.BuildPath.SetVariableByName(settings, Constants.LocalBuildVar);
                bundle.LoadPath.SetVariableByName(settings, Constants.LocalLoadVar);
                bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                bundle.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
                bundle.IncludeInBuild = true;
            }

            var contentUpdate = group.GetSchema<ContentUpdateGroupSchema>();
            if (contentUpdate != null)
                contentUpdate.StaticContent = true;

            return group;
        }

        private static bool EnsureOwnedAssetEntries(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            IEnumerable<string> ownedAssetPaths)
        {
            if (!settings || !group)
                return false;

            var normalizedPaths = new HashSet<string>(
                (ownedAssetPaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path) && (path == VertexDeveloperTextureImporter.AssetPath ||
                    !SdkHost.IsBaseProject || SdkGame.Current.AllowsSourceAsset(path)))
                .Select(path => path.Replace('\\', '/').Trim()),
                StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var path in normalizedPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (AssetDatabase.IsValidFolder(path))
                    continue;
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (assetType == null || assetType == typeof(DefaultAsset) || assetType == typeof(MonoScript) || assetType == typeof(SceneAsset))
                    continue;

                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrWhiteSpace(guid))
                    continue;

                var entry = settings.FindAssetEntry(guid);
                if (entry == null)
                {
                    entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    changed = true;
                }

                if (entry.parentGroup != group)
                {
                    settings.MoveEntry(entry, group, false, false);
                    changed = true;
                }

                var stableAddress = SharedAssetAddressUtility.CreateAddress(path);
                if (!string.IsNullOrWhiteSpace(stableAddress))
                    changed |= SetAssetEntryAddress(entry, stableAddress);
            }

            foreach (var entry in group.entries.ToArray())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.guid))
                {
                    changed |= RemoveAssetEntry(settings, entry?.guid);
                    continue;
                }

                var path = entry.AssetPath?.Replace('\\', '/').Trim();
                if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path) ||
                    !normalizedPaths.Contains(path))
                {
                    changed |= RemoveAssetEntry(settings, entry.guid);
                    continue;
                }

                var stableAddress = SharedAssetAddressUtility.CreateAddress(path);
                if (!string.IsNullOrWhiteSpace(stableAddress))
                    changed |= SetAssetEntryAddress(entry, stableAddress);
            }

            return changed;
        }

        public static HashSet<string> BuildSharedGuidSet(AddressableAssetGroup group)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (!group)
                return set;

            foreach (var entry in group.entries)
                if (!string.IsNullOrWhiteSpace(entry.guid))
                    set.Add(entry.guid);

            return set;
        }

        private static bool RemoveAssetEntry(AddressableAssetSettings settings, string guid)
        {
            if (!settings || string.IsNullOrWhiteSpace(guid))
                return false;

            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
                return false;

            settings.RemoveAssetEntry(guid);
            return true;
        }

        private static bool SetAssetEntryAddress(AddressableAssetEntry entry, string address)
        {
            if (entry == null || string.IsNullOrWhiteSpace(address) ||
                string.Equals(entry.address, address, StringComparison.Ordinal))
                return false;

            entry.address = address;
            return true;
        }

        private static bool ShouldRunNormalization(AddressableAssetSettings settings)
        {
            var currentVersion = SessionState.GetInt(ChangeVersionStateKey, 0);
            var lastVersion = SessionState.GetInt(LastNormalizedVersionStateKey, -1);
            if (currentVersion != lastVersion)
                return true;

            var settingsPath = AssetDatabase.GetAssetPath(settings) ?? string.Empty;
            var lastSettingsPath = SessionState.GetString(LastNormalizedSettingsPathStateKey, string.Empty);
            return !string.Equals(settingsPath, lastSettingsPath, StringComparison.OrdinalIgnoreCase);
        }

        private static void MarkNormalizationApplied(AddressableAssetSettings settings)
        {
            SessionState.SetInt(LastNormalizedVersionStateKey, SessionState.GetInt(ChangeVersionStateKey, 0));
            SessionState.SetString(LastNormalizedSettingsPathStateKey,
                AssetDatabase.GetAssetPath(settings) ?? string.Empty);
        }

        private static void MarkRelevantProjectChange()
        {
            var next = SessionState.GetInt(ChangeVersionStateKey, 0) + 1;
            SessionState.SetInt(ChangeVersionStateKey, next);
        }

        private static bool IsRelevantPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var normalized = path.Replace('\\', '/').Trim();
            if (normalized == VertexDeveloperTextureImporter.AssetPath)
                return true;
            if (SdkHost.IsBaseProject && normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return true;

            if (normalized.StartsWith(AddressablesDataRoot, StringComparison.OrdinalIgnoreCase))
                return true;

            return SdkContentPathUtility.IsSdkAssetPath(normalized) ||
                   normalized.StartsWith(Constants.SdkManagedContentRoot + "/",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, Constants.SdkManagedContentRoot + ".meta",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsRelevantPath(IEnumerable<string> paths)
        {
            if (paths == null)
                return false;

            return paths.Any(IsRelevantPath);
        }

        internal static void NotifyRelevantAssetChanges(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsRelevantPath(importedAssets) ||
                ContainsRelevantPath(deletedAssets) ||
                ContainsRelevantPath(movedAssets) ||
                ContainsRelevantPath(movedFromAssetPaths))
                MarkRelevantProjectChange();
        }
    }

    internal sealed class SharedAssetAddressableChangePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            SharedAssetAddressableEditorUtility.NotifyRelevantAssetChanges(
                importedAssets,
                deletedAssets,
                movedAssets,
                movedFromAssetPaths);
        }
    }
}
#endif
