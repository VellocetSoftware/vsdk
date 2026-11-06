using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class BaseMapArtifactValidator
    {
        public static bool TryValidate(MapDefinition map, out string error, BaseMapValidationOptions options = null)
        {
            if (!BaseMapAuthoring.TryValidateIdentityAndFolder(map, out error)) return false;
            if (map.IsDraft) { error = "Compile the draft map first."; return false; }
            if (!File.Exists(map.compiledScenePath) || map.sceneReference == null ||
                AssetDatabase.GUIDToAssetPath(map.sceneReference.AssetGUID) != map.compiledScenePath)
            { error = "Compiled scene is missing or its reference is stale. Compile the map."; return false; }
            if (!MapCompilationReceiptUtility.TryLoad(map.compilationReceiptPath, out var receipt) ||
                !MapCompilationReceipt.IsSupportedSchemaVersion(receipt.schemaVersion) ||
                !MapCompilationReceiptUtility.HasValidSeal(receipt) ||
                MapCompilationReceiptValidator.GetIdentityMismatch(receipt, MapCompilationReceipt.BaseContentKind,
                    map.id, null, StringComparison.Ordinal) != MapCompilationReceiptValidator.IdentityMismatch.None ||
                receipt.seal != map.compilationSeal || receipt.compiledScenePath != map.compiledScenePath ||
                MapCompilationReceiptUtility.ComputeContentRevision(receipt) != map.contentRevision)
            { error = "Compilation receipt is missing or stale. Compile the map."; return false; }
            if (!string.IsNullOrEmpty(map.authoringScenePath) && !AssetDatabase.LoadAssetAtPath<SceneAsset>(map.authoringScenePath))
            { error = "The authoring scene is missing."; return false; }
            if (!HasCurrentHash(map.compiledScenePath, receipt.compiledSceneHash) ||
                !string.IsNullOrEmpty(map.authoringScenePath) &&
                (map.authoringScenePath != receipt.sourceScenePath || !HasCurrentHash(map.authoringScenePath, receipt.sourceSceneHash)))
            { error = "Scene content changed since compilation. Compile the map."; return false; }
            if (map.contentKind != MapCompilationReceipt.BaseContentKind || string.IsNullOrWhiteSpace(map.sceneAddress))
            { error = "Map content kind or scene address is missing or invalid. Compile the map."; return false; }
            options ??= new BaseMapValidationOptions();
            if (!MapMusicSets.IsValid(map.musicSet))
            { error = "Map music set is invalid. Correct Map Settings and recompile."; return false; }
            if (!string.IsNullOrEmpty(options.SceneAddress) && map.sceneAddress != options.SceneAddress)
            { error = $"sceneAddress is '{map.sceneAddress}', expected '{options.SceneAddress}'."; return false; }
            if (options.RequireStandardLayout)
            {
                var layout = BaseMapArtifactLayout.FromDefinitionPath(AssetDatabase.GetAssetPath(map), map.id);
                if (map.compiledScenePath != layout.CompiledScenePath || map.compilationReceiptPath != layout.ReceiptPath)
                { error = "Compiled scene or receipt does not match the map's artifact layout."; return false; }
            }
            // Compiler identity is the map ID; games may publish the scene under a different runtime address.
            if (receipt.sceneAddress != map.id || !MapSpatialGraphArtifacts.TryValidateReceipt(receipt, map.id, map.id, out error))
            { error = "Compilation receipt spatial graph is invalid: " + error; return false; }
            if (options.ValidateNavigation && !MapNavigationAuthoringValidator.TryValidateCompiledSceneInIsolation(map.compiledScenePath, out error))
            { error = "Compiled navigation is not shippable: " + error; return false; }
            if (options.ValidateAddressables)
            {
                if (!options.Addressables)
                { error = "Addressables settings are missing."; return false; }
                var definitionPath = AssetDatabase.GetAssetPath(map);
                if (!ValidateAddressableEntry(options.Addressables, AssetDatabase.AssetPathToGUID(definitionPath),
                        definitionPath, options.DefinitionAddress, Constants.MapLabel, "MapDefinition", out error) ||
                    !ValidateAddressableEntry(options.Addressables, map.sceneReference.AssetGUID,
                        map.compiledScenePath, options.SceneAddress ?? map.sceneAddress, null, "compiled scene", out error))
                    return false;
            }
            if (receipt.skipSpatialOctreeBake)
            {
                if (!options.AllowSkippedSpatialOctreeBake)
                { error = "Spatial octree bake was skipped. Recompile the map before shipping."; return false; }
            }
            else
            {
                if (!MapSpatialGraphArtifacts.TryResolveDependency(map, out var spatial, out error) ||
                    !MapSpatialGraphArtifacts.TryValidateBakedAsset(spatial.Asset, map.id, map.id, out error))
                    return false;
                if (options.ValidateAddressables && !ValidateAddressableEntry(options.Addressables, spatial.Guid,
                        spatial.AssetPath, spatial.Address, null, "spatial octree", out error))
                    return false;
            }
            error = null;
            return true;
        }

        private static bool HasCurrentHash(string path, string expected) =>
            Utilities.SdkFileHashUtility.TryComputeSha256(path, out var actual) &&
            string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

        private static bool ValidateAddressableEntry(AddressableAssetSettings settings, string guid,
            string expectedPath, string expectedAddress, string requiredLabel, string description, out string error)
        {
            error = null;
            var entry = settings ? settings.FindAssetEntry(guid) : null;
            if (entry == null)
            {
                error = $"{description} is not registered in Addressables: {expectedPath}";
                return false;
            }

            var actualPath = NormalizeAssetPath(entry.AssetPath);
            var normalizedExpectedPath = NormalizeAssetPath(expectedPath);
            if (!string.Equals(actualPath, normalizedExpectedPath, StringComparison.Ordinal))
            {
                error =
                    $"{description} Addressables entry points at '{actualPath}', expected '{normalizedExpectedPath}'.";
                return false;
            }

            if (expectedAddress != null && !string.Equals(entry.address, expectedAddress, StringComparison.Ordinal))
            {
                error = $"{description} Addressables address is '{entry.address}', expected '{expectedAddress}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(requiredLabel) && !entry.labels.Contains(requiredLabel))
            {
                error = $"{description} Addressables entry is missing label '{requiredLabel}'.";
                return false;
            }

            return true;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        }

    }
}
