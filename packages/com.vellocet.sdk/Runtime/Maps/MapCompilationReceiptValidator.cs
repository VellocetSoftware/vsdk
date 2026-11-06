using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Vellocet.Sdk.Addons;

namespace Vellocet.Sdk.Maps
{
    public static class MapCompilationReceiptValidator
    {
        public enum IdentityMismatch { None, ContentKind, MapId, SceneAddress }

        // Authoring uses exact identities; runtime accepts case differences in map IDs and content kinds.
        // A null expected scene address leaves that check to the caller's artifact layout rules.
        public static IdentityMismatch GetIdentityMismatch(MapCompilationReceipt receipt, string contentKind,
            string mapId, string sceneAddress, StringComparison identityComparison)
        {
            if (!string.Equals(receipt.contentKind, contentKind, identityComparison))
                return IdentityMismatch.ContentKind;
            if (!string.Equals(receipt.mapId, mapId, identityComparison))
                return IdentityMismatch.MapId;
            if (sceneAddress != null && !string.Equals(receipt.sceneAddress, sceneAddress, StringComparison.Ordinal))
                return IdentityMismatch.SceneAddress;
            return IdentityMismatch.None;
        }

        public static bool Validate(MapDefinition map, out string error)
        {
            error = null;
            if (!map)
            {
                error = "MapDefinition is null.";
                return false;
            }

            if (!string.Equals(map.contentKind, MapCompilationReceipt.BaseContentKind,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = $"contentKind is not '{MapCompilationReceipt.BaseContentKind}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(map.compiledScenePath))
            {
                error = "compiledScenePath is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(map.compilationReceiptPath))
            {
                error = "compilationReceiptPath is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(map.compilationSeal))
            {
                error = "compilationSeal is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(map.id) || string.IsNullOrWhiteSpace(map.sceneAddress))
            {
                error = "map id or scene address is missing.";
                return false;
            }

            if (map.sceneReference == null || !map.sceneReference.RuntimeKeyIsValid())
            {
                error = "sceneReference is missing or invalid.";
                return false;
            }

            if (!MapMusicSets.IsValid(map.musicSet))
            {
                error = "Map music set is invalid. Correct the Map Settings override and rebuild the map.";
                return false;
            }

            if (!Application.isEditor)
                return true;

            if (!File.Exists(map.compilationReceiptPath))
            {
                error = "compilation receipt file is missing.";
                return false;
            }

            if (!MapCompilationReceiptUtility.TryLoad(map.compilationReceiptPath, out var receipt) || receipt == null)
            {
                error = "compilation receipt could not be loaded.";
                return false;
            }

            if (!MapCompilationReceipt.IsSupportedSchemaVersion(receipt.schemaVersion))
            {
                error =
                    $"compilation receipt schema '{receipt.schemaVersion}' is not supported; expected '{MapCompilationReceipt.CurrentSchemaVersion}'.";
                return false;
            }

            if (!MapCompilationReceiptUtility.HasValidSeal(receipt))
            {
                error = "compilation receipt seal is invalid.";
                return false;
            }

            if (!string.Equals(receipt.seal, map.compilationSeal, StringComparison.OrdinalIgnoreCase))
            {
                error = "compilation receipt seal does not match the MapDefinition.";
                return false;
            }

            error = GetIdentityMismatch(receipt, MapCompilationReceipt.BaseContentKind, map.id,
                map.sceneAddress, StringComparison.OrdinalIgnoreCase) switch
            {
                IdentityMismatch.ContentKind => "compilation receipt content kind is invalid for base maps.",
                IdentityMismatch.MapId => "compilation receipt map id does not match.",
                IdentityMismatch.SceneAddress => "compilation receipt scene address does not match.",
                _ => null
            };
            return error == null;
        }

        public static bool Validate(string addonRoot, AddonManifest manifest, AddonManifest.ContentEntry mapEntry,
            string expectedMapId, string platformName, out string error)
        {
            error = null;
            if (manifest == null || mapEntry?.map == null)
            {
                error = "Compilation receipt validation requires a manifest and map payload.";
                return false;
            }

            if (manifest.schemaVersion != AddonManifest.CurrentSchemaVersion)
            {
                error =
                    $"Addon schema '{manifest.schemaVersion}' is not supported; expected '{AddonManifest.CurrentSchemaVersion}'.";
                return false;
            }

            if (!MapMusicSets.TryParse(mapEntry.map.musicSet, out _))
            {
                error = $"Map '{expectedMapId}' has an unknown music set '{mapEntry.map.musicSet}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(mapEntry.map.compiledReceipt))
            {
                error = "Map is missing its compilation receipt.";
                return false;
            }

            if (!AddonHelper.TryResolvePath(addonRoot, mapEntry.map.compiledReceipt, out var receiptPath, out _))
            {
                error = "Compilation receipt path is invalid.";
                return false;
            }

            if (!MapCompilationReceiptUtility.TryLoad(receiptPath, out var receipt) || receipt == null)
            {
                error = "Compilation receipt could not be loaded.";
                return false;
            }

            if (!MapCompilationReceipt.IsSupportedSchemaVersion(receipt.schemaVersion))
            {
                error =
                    $"Compilation receipt schema '{receipt.schemaVersion}' is not supported; expected '{MapCompilationReceipt.CurrentSchemaVersion}'.";
                return false;
            }

            if (!MapCompilationReceiptUtility.HasValidSeal(receipt))
            {
                error = "Compilation receipt seal is invalid.";
                return false;
            }

            if (!string.Equals(receipt.contentKind, MapCompilationReceipt.AddonContentKind,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = $"Compilation receipt content kind '{receipt.contentKind}' is not valid for addon maps.";
                return false;
            }

            if (!string.Equals(receipt.mapId, expectedMapId, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Compilation receipt map id '{receipt.mapId}' does not match '{expectedMapId}'.";
                return false;
            }

            if (!string.Equals(receipt.sceneAddress, mapEntry.map.scene, StringComparison.Ordinal))
            {
                error = "Compilation receipt scene address does not match the addon manifest.";
                return false;
            }

            if (!string.Equals(receipt.buildId, manifest.buildId, StringComparison.Ordinal) ||
                !string.Equals(receipt.distributionProfile, manifest.distributionProfile,
                    StringComparison.Ordinal) ||
                !string.Equals(receipt.distributionPolicy, manifest.distributionPolicy,
                    StringComparison.Ordinal) ||
                !string.Equals(receipt.compilerVersion, manifest.sdkVersion, StringComparison.Ordinal) ||
                !string.Equals(receipt.unityVersion, manifest.unityVersion, StringComparison.Ordinal))
            {
                error = "Compilation receipt distribution metadata does not match the addon manifest.";
                return false;
            }

            var manifestPlatform = FindManifestPlatform(mapEntry.map.platforms, platformName);
            if (manifestPlatform == null)
            {
                error = $"Addon manifest is missing a platform entry for '{platformName}'.";
                return false;
            }

            var receiptPlatform = FindReceiptPlatform(receipt.platforms, platformName);
            if (receiptPlatform == null)
            {
                error = $"Compilation receipt is missing a platform entry for '{platformName}'.";
                return false;
            }

            if (!PathsEqual(receiptPlatform.catalog, manifestPlatform.catalog))
            {
                error = "Compilation receipt catalog path does not match the addon manifest.";
                return false;
            }

            if (!string.Equals(receiptPlatform.role, manifestPlatform.role, StringComparison.OrdinalIgnoreCase))
            {
                error = "Compilation receipt platform role does not match the addon manifest.";
                return false;
            }

            if (!FilesMatch(receiptPlatform.files, manifestPlatform.files))
            {
                error = "Compilation receipt file hashes do not match the addon manifest.";
                return false;
            }

            return true;
        }

        private static AddonManifest.PlatformCatalog FindManifestPlatform(AddonManifest.PlatformCatalog[] platforms,
            string platformName)
        {
            return platforms?
                .FirstOrDefault(platform =>
                    platform != null &&
                    string.Equals(platform.name, platformName, StringComparison.OrdinalIgnoreCase));
        }

        private static MapCompilationReceipt.PlatformArtifact FindReceiptPlatform(
            MapCompilationReceipt.PlatformArtifact[] platforms, string platformName)
        {
            return platforms?
                .FirstOrDefault(platform =>
                    platform != null &&
                    string.Equals(platform.name, platformName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool FilesMatch(MapCompilationReceipt.ArtifactFile[] receiptFiles,
            AddonManifest.PlatformFile[] manifestFiles)
        {
            if (!TryBuildFileMap(receiptFiles, file => file.path, out var receiptByPath) ||
                !TryBuildFileMap(manifestFiles, file => file.path, out var manifestByPath))
                return false;
            if (receiptByPath.Count != manifestByPath.Count)
                return false;

            foreach (var pair in manifestByPath)
            {
                if (!receiptByPath.TryGetValue(pair.Key, out var receiptFile))
                    return false;

                var manifestFile = pair.Value;
                if (receiptFile.size != manifestFile.size)
                    return false;
                if (!string.Equals(receiptFile.sha256, manifestFile.sha256, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static bool TryBuildFileMap<TFile>(IEnumerable<TFile> files,
            Func<TFile, string> pathSelector, out Dictionary<string, TFile> filesByPath)
            where TFile : class
        {
            filesByPath = new Dictionary<string, TFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files ?? Array.Empty<TFile>())
            {
                if (file == null)
                    return false;
                var path = pathSelector(file);
                if (!AddonFilePolicy.TryNormalizePackageFilePath(path, out var canonicalPath) ||
                    !string.Equals(path, canonicalPath, StringComparison.Ordinal) ||
                    !filesByPath.TryAdd(canonicalPath, file))
                    return false;
            }

            return true;
        }

        private static bool PathsEqual(string a, string b)
        {
            return AddonFilePolicy.TryNormalizePackageFilePath(a, out var canonicalA) &&
                   AddonFilePolicy.TryNormalizePackageFilePath(b, out var canonicalB) &&
                   string.Equals(a, canonicalA, StringComparison.Ordinal) &&
                   string.Equals(b, canonicalB, StringComparison.Ordinal) &&
                   string.Equals(canonicalA, canonicalB, StringComparison.OrdinalIgnoreCase);
        }
    }
}