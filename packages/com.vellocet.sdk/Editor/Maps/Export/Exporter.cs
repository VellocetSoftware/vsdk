// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class Exporter
    {
        private static readonly string[] RequiredProfileVariables =
            { Constants.RemoteBuildVar, Constants.RemoteLoadVar, Constants.LocalBuildVar, Constants.LocalLoadVar };

        public sealed class Result
        {
            public bool Success { get; set; }
            public string Error { get; set; }
            public BuildTarget Target { get; set; }
            public string OutputDirectory { get; set; }
            public string CatalogPath { get; set; }
            public string[] BundlePaths { get; set; } = Array.Empty<string>();
            public MapAssetUsageReport AssetUsageReport { get; set; }
            public MapCompilationReceipt CompilationReceipt { get; set; }
            public Vertex.Contracts.PackageAsset[] AssetInventory { get; set; }
        }

        #region API

        public static Result ExportMap(SceneAsset sceneAsset, string mapName, string addonsRoot, BuildTarget target,
            MapCompiler.CompileOptions compileOptions = null, MapCompiler.Result precompiledCompile = null,
            Action<float, string> reportProgress = null, Action<string> log = null)
        {
            var result = new Result
            {
                Target = target
            };

            if (!SdkUnityVersionGuard.TryValidateForOperation("SDK map platform build", out var versionError))
            {
                result.Error = versionError;
                return result;
            }

            if (!MapCompiler.TryValidateBuildConfiguration(compileOptions, "Release map package build",
                    out var buildConfigurationError))
            {
                result.Error = buildConfigurationError;
                return result;
            }

            if (!sceneAsset || string.IsNullOrEmpty(mapName) || string.IsNullOrEmpty(addonsRoot))
            {
                Debug.LogError("[Map Builder] Invalid input.");
                result.Error = "Invalid input.";
                return result;
            }

            var mapId = MapBuildArtifactPlanner.NormalizeMapId(mapName);
            if (string.IsNullOrWhiteSpace(mapId))
            {
                Debug.LogError("[Map Builder] Invalid map ID.");
                result.Error = "Invalid map id.";
                return result;
            }

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (!settings)
            {
                Debug.LogError("[Map Builder] Addressables settings not found.");
                result.Error = "Addressables settings not found.";
                return result;
            }

            var profiles = settings.profileSettings;
            if (profiles == null)
            {
                Debug.LogError("[Map Builder] Profile settings not found.");
                result.Error = "Profile settings not found.";
                return result;
            }

            if (!TryValidateExportProfileVariables(profiles, out var profileValidationError))
            {
                Debug.LogError($"[Map Builder] {profileValidationError}");
                result.Error = profileValidationError;
                return result;
            }

            var originalProfileId = settings.activeProfileId;
            var originalRemoteCatalog = settings.BuildRemoteCatalog;
            MapExportAddressablesProfilePolicy.Ensure(settings, true, out var profileId);
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();

            var origRemoteBuild = profiles.GetValueById(profileId, Constants.RemoteBuildVar);
            var origRemoteLoad = profiles.GetValueById(profileId, Constants.RemoteLoadVar);
            var origLocalBuild = profiles.GetValueById(profileId, Constants.LocalBuildVar);
            var origLocalLoad = profiles.GetValueById(profileId, Constants.LocalLoadVar);

            AddressableAssetGroup tempGroup = null;
            string tempSceneFolder = null;
            var buildScene = sceneAsset;
            var ownsTempSceneFolder = precompiledCompile == null;
            var movedEntries = new List<MovedEntry>();
            var addedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var disabledGroups = new List<GroupIncludeState>();
            var packageAssets = precompiledCompile?.PackageAssets;

            void AddAssetToTempGroup(string guid, string address)
            {
                if (string.IsNullOrWhiteSpace(guid)) return;
                if (packageAssets?.ReferenceInstalled(guid, address) == true) return;
                if (!addedGuids.Add(guid)) return;

                var existing = settings.FindAssetEntry(guid);
                if (existing != null && existing.parentGroup != null)
                    movedEntries.Add(new MovedEntry
                    {
                        Guid = guid,
                        OriginalGroup = existing.parentGroup,
                        OriginalAddress = existing.address,
                        OriginalLabels = existing.labels?.ToArray() ?? Array.Empty<string>()
                    });

                var e = settings.CreateOrMoveEntry(guid, tempGroup, false, false);
                if (!string.IsNullOrWhiteSpace(address))
                    e.address = address;
            }

            try
            {
                MapCompiler.Result compileResult;
                if (precompiledCompile != null)
                {
                    compileResult = precompiledCompile;
                    Report(reportProgress, log, 0.05f, "Using precompiled scene.");
                }
                else
                {
                    Report(reportProgress, log, 0.05f, "Compiling authoring scene.");
                    compileResult = MapCompiler.Compile(sceneAsset, mapId, mapId,
                        MapCompilationReceipt.AddonContentKind, null, compileOptions);
                }

                result.AssetUsageReport = compileResult.AssetUsageReport;
                result.CompilationReceipt = compileResult.Receipt;

                if (!compileResult.Success)
                {
                    result.Error = string.IsNullOrWhiteSpace(compileResult.Error)
                        ? "Map compilation failed."
                        : compileResult.Error;
                    Debug.LogError($"[Map Builder] {result.Error}");
                    return result;
                }

                if (!MapSpatialGraphArtifacts.TryValidateCompiledResult(compileResult, mapId, mapId,
                        out var spatialValidationMessage))
                {
                    result.Error = $"Compiled map is missing a packageable spatial graph: {spatialValidationMessage}";
                    Debug.LogError($"[Map Builder] {result.Error}");
                    return result;
                }

                log?.Invoke(spatialValidationMessage);

                if (compileResult.CompiledScene) buildScene = compileResult.CompiledScene;
                if (!string.IsNullOrEmpty(compileResult.TempFolder)) tempSceneFolder = compileResult.TempFolder;
                if (!buildScene && !string.IsNullOrWhiteSpace(compileResult.CompiledScenePath))
                    buildScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(compileResult.CompiledScenePath);
                if (!buildScene)
                {
                    result.Error = "Compiled scene asset could not be resolved for the platform build.";
                    Debug.LogError($"[Map Builder] {result.Error}");
                    return result;
                }

                var computedLoad =
                    $"{Constants.AddonsToken}/{Constants.MapsFolder}/{mapId}/{PlatformFolderName(target)}";

                Report(reportProgress, log, 0.62f, "Configuring Addressables build group.");

                profiles.SetValue(profileId, Constants.RemoteBuildVar, Constants.ScratchBuild);
                profiles.SetValue(profileId, Constants.RemoteLoadVar, computedLoad);
                profiles.SetValue(profileId, Constants.LocalBuildVar, Constants.ScratchBuild);
                profiles.SetValue(profileId, Constants.LocalLoadVar, computedLoad);

                settings.BuildRemoteCatalog = true;
                settings.RemoteCatalogLoadPath.SetVariableByName(settings, Constants.RemoteLoadVar);
                settings.RemoteCatalogBuildPath.SetVariableByName(settings, Constants.RemoteBuildVar);

                var scratch = profiles.EvaluateString(profileId, Constants.ScratchBuild);
                SafeCleanDirectory(scratch);

                var groupName = $"{Constants.TempGroupPrefix}{mapId}";
                RemoveGroupByName(settings, groupName);
                tempGroup = settings.CreateGroup(groupName, false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

                var bag = tempGroup.GetSchema<BundledAssetGroupSchema>() ??
                          tempGroup.AddSchema<BundledAssetGroupSchema>();
                bag.BuildPath.SetVariableByName(settings, Constants.LocalBuildVar);
                bag.LoadPath.SetVariableByName(settings, Constants.LocalLoadVar);
                bag.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                bag.IncludeAddressInCatalog = true;
                bag.IncludeGUIDInCatalog = true;
                bag.IncludeLabelsInCatalog = true;
                bag.IncludeInBuild = true;

                var scenePath = AssetDatabase.GetAssetPath(buildScene);
                var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);

                AddAssetToTempGroup(
                    sceneGuid,
                    mapId);

                foreach (var dependency in compileResult.RuntimeDependencies)
                    AddAssetToTempGroup(dependency.Guid, dependency.Address);

                Report(reportProgress, log, 0.7f, "Collecting runtime map dependencies.");
                var runtimeDependencies = CollectRuntimeDependencies(scenePath, settings);
                if (runtimeDependencies.MissingAddresses.Count > 0)
                {
                    result.Error = BuildMissingRuntimeDependencyError(runtimeDependencies.MissingAddresses);
                    Debug.LogError($"[Map Builder] {result.Error}");
                    return result;
                }

                foreach (var dependency in runtimeDependencies.ResolvedEntries)
                    AddAssetToTempGroup(dependency.Guid, dependency.Address);

                if (packageAssets != null)
                    foreach (var path in packageAssets.PackagedDependencies(new[] { scenePath }
                                 .Concat(compileResult.RuntimeDependencies.Select(d => AssetDatabase.GUIDToAssetPath(d.Guid)))
                                 .Concat(runtimeDependencies.ResolvedEntries.Select(d => AssetDatabase.GUIDToAssetPath(d.Guid)))))
                    {
                        var guid = AssetDatabase.AssetPathToGUID(path);
                        AddAssetToTempGroup(guid, "vertex/asset/" + guid);
                    }

                log?.Invoke(
                    $"Runtime dependencies: {compileResult.RuntimeDependencies.Count} compiled, {runtimeDependencies.ResolvedEntries.Count} scanned, {runtimeDependencies.MissingAddresses.Count} missing.");

                // Map builds should include only the temporary build group. Disable other bundled groups temporarily.
                foreach (var group in settings.groups)
                {
                    if (!group || group == tempGroup) continue;

                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    if (schema == null) continue;

                    disabledGroups.Add(new GroupIncludeState
                    {
                        Schema = schema,
                        IncludeInBuild = schema.IncludeInBuild
                    });

                    schema.IncludeInBuild = false;
                }

                Report(reportProgress, log, 0.8f, "Building Addressables content.");
                if (!MapCompiler.TryValidateBuildConfiguration(compileOptions, "Release map package build",
                        out buildConfigurationError))
                {
                    result.Error = buildConfigurationError;
                    Debug.LogError($"[Map Builder] {result.Error}");
                    return result;
                }

                AddressablesPlayerBuildResult buildResult;
                packageAssets?.BeginBuild();
                try { AddressableAssetSettings.BuildPlayerContent(out buildResult); }
                finally { packageAssets?.EndBuild(); }
                if (buildResult == null || !string.IsNullOrEmpty(buildResult.Error))
                {
                    var error = $"Addressables build failed: {buildResult?.Error}";
                    Debug.LogError($"[Map Builder] {error}");
                    return new Result
                    {
                        Target = target,
                        AssetUsageReport = result.AssetUsageReport,
                        CompilationReceipt = result.CompilationReceipt,
                        Error = error
                    };
                }

                var outDir = Path.Combine(addonsRoot, Constants.MapsFolder, mapId, PlatformFolderName(target));
                result.OutputDirectory = outDir;
                Report(reportProgress, log, 0.92f, "Copying built catalogs and bundles.");
                if (!PackageFromScratch(settings, profileId, outDir, out var catalogPath, out var bundlePaths))
                {
                    result.Error = "Failed to copy built addressable content.";
                    return result;
                }

                result.CatalogPath = catalogPath;
                result.BundlePaths = bundlePaths;
                packageAssets?.Audit(buildResult, outDir);
                result.AssetInventory = packageAssets?.Inventory;
                result.Success = true;

                Report(reportProgress, log, 1f, "Platform build completed.");
                Debug.Log($"[Map Builder] Built '{mapId}' for {target} at {outDir}");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Map Builder] Unexpected error.\n{e}");
                result.Error = e.Message;
                return result;
            }
            finally
            {
                foreach (var moved in movedEntries)
                    TryRun(() =>
                        {
                            if (moved == null || moved.OriginalGroup == null)
                                return;

                            var e = settings.FindAssetEntry(moved.Guid);
                            if (e == null)
                                return;

                            settings.MoveEntry(e, moved.OriginalGroup, false, false);
                            e.address = moved.OriginalAddress;

                            var current = e.labels?.ToArray() ?? Array.Empty<string>();
                            foreach (var label in current)
                                e.SetLabel(label, false, true);
                            foreach (var label in moved.OriginalLabels ?? Array.Empty<string>())
                                e.SetLabel(label, true, true);
                        }, $"Could not restore moved entry '{moved?.Guid}'");

                foreach (var state in disabledGroups)
                    TryRun(() =>
                    {
                        if (state?.Schema != null)
                            state.Schema.IncludeInBuild = state.IncludeInBuild;
                    }, "Could not restore group include flag");

                if (tempGroup)
                    TryRun(() => settings.RemoveGroup(tempGroup), "Could not remove temporary build group");

                if (origRemoteBuild != null) profiles.SetValue(profileId, Constants.RemoteBuildVar, origRemoteBuild);
                if (origRemoteLoad != null) profiles.SetValue(profileId, Constants.RemoteLoadVar, origRemoteLoad);
                if (origLocalBuild != null) profiles.SetValue(profileId, Constants.LocalBuildVar, origLocalBuild);
                if (origLocalLoad != null) profiles.SetValue(profileId, Constants.LocalLoadVar, origLocalLoad);

                settings.BuildRemoteCatalog = originalRemoteCatalog;
                if (!string.IsNullOrWhiteSpace(originalProfileId))
                    settings.activeProfileId = originalProfileId;
                settings.RemoteCatalogLoadPath.SetVariableByName(settings, Constants.RemoteLoadVar);
                settings.RemoteCatalogBuildPath.SetVariableByName(settings, Constants.RemoteBuildVar);

                EditorUtility.SetDirty(settings);

                if (sceneSetup is { Length: > 0 })
                    EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);

                if (ownsTempSceneFolder && !string.IsNullOrEmpty(tempSceneFolder))
                    try
                    {
                        if (AssetDatabase.IsValidFolder(tempSceneFolder))
                            AssetDatabase.DeleteAsset(tempSceneFolder);
                        else if (Directory.Exists(tempSceneFolder))
                            Directory.Delete(tempSceneFolder, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Map Builder] Cleanup of temp folder failed: {ex.Message}");
                    }

                TryRun(() =>
                {
                    var scratch = profiles.EvaluateString(profileId, Constants.ScratchBuild);
                    if (Directory.Exists(scratch))
                        Directory.Delete(scratch, true);
                }, "Could not clean scratch build folder");
            }
        }

        private sealed class MovedEntry
        {
            public string Guid;
            public string OriginalAddress;
            public AddressableAssetGroup OriginalGroup;
            public string[] OriginalLabels;
        }

        private sealed class GroupIncludeState
        {
            public bool IncludeInBuild;
            public BundledAssetGroupSchema Schema;
        }

        #endregion

        #region Helpers

        private static bool TryValidateExportProfileVariables(AddressableAssetProfileSettings profileSettings,
            out string error)
        {
            error = string.Empty;
            if (profileSettings == null)
            {
                error = "Addressables profile settings are missing.";
                return false;
            }

            var names = profileSettings.GetVariableNames() ?? new List<string>();
            var missing = RequiredProfileVariables
                .Where(name => !names.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (missing.Length == 0)
                return true;

            error =
                $"Addressables profile is missing required variables: {string.Join(", ", missing)}. Restore the default Addressables profile variables before building.";
            return false;
        }

        private static void RemoveGroupByName(AddressableAssetSettings settings, string groupName)
        {
            if (settings == null || string.IsNullOrWhiteSpace(groupName))
                return;

            while (settings.FindGroup(groupName) is { } stale)
                settings.RemoveGroup(stale);
        }

        private static void TryRun(Action action, string warningPrefix)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Map Builder] {warningPrefix}: {ex.Message}");
            }
        }

        private static void Report(Action<float, string> progress, Action<string> log, float value, string message)
        {
            progress?.Invoke(Mathf.Clamp01(value), message ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(message))
                log?.Invoke(message.Trim());
        }

        private static bool PackageFromScratch(AddressableAssetSettings settings, string profileId,
            string outputDirectory, out string catalogPath, out string[] bundlePaths)
        {
            catalogPath = null;
            bundlePaths = Array.Empty<string>();
            var scratch = settings.profileSettings.EvaluateString(profileId, Constants.ScratchBuild);
            if (string.IsNullOrEmpty(scratch) || !Directory.Exists(scratch))
            {
                Debug.LogError($"[Map Builder] Scratch build path not found: '{scratch}'.");
                return false;
            }

            var catalogs = Directory.GetFiles(scratch, "catalog*.bin", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (catalogs.Length == 0)
                catalogs = Directory.GetFiles(scratch, "*.bin", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if (catalogs.Length > 1)
                Debug.LogWarning(
                    $"[Map Builder] Multiple catalog binaries found; using deterministic first: '{Path.GetFileName(catalogs[0])}'.");

            var bin = catalogs.FirstOrDefault();
            if (bin == null)
            {
                Debug.LogError("[Map Builder] No binary catalog (.bin) found in scratch build output.");
                return false;
            }

            SafeCleanDirectory(outputDirectory);

            var bundles = Directory.GetFiles(scratch, "*.bundle", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (bundles.Length == 0)
            {
                Debug.LogError("[Map Builder] No asset bundles were found in scratch build output.");
                return false;
            }

            foreach (var src in bundles)
                SafeCopy(src, Path.Combine(outputDirectory, Path.GetFileName(src)));

            catalogPath = Path.Combine(outputDirectory, Path.GetFileName(bin));
            SafeCopy(bin, catalogPath);
            var hashPath = bin + ".hash";
            if (File.Exists(hashPath))
                SafeCopy(hashPath, Path.Combine(outputDirectory, Path.GetFileName(hashPath)));

            bundlePaths = bundles
                .Select(src => Path.Combine(outputDirectory, Path.GetFileName(src)))
                .ToArray();
            Debug.Log($"[Map Builder] Copied {bundles.Length} bundle(s) + catalog to {outputDirectory}");
            return true;
        }

        private static void SafeCopy(string src, string dest)
        {
            if (string.IsNullOrEmpty(src) || !File.Exists(src))
                throw new FileNotFoundException($"Source file missing: {src}");

            var dir = Path.GetDirectoryName(dest);
            if (string.IsNullOrEmpty(dir))
                throw new ArgumentException($"Invalid destination path: {dest}");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.Copy(src, dest, true);
        }

        private static void SafeCleanDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch
            {
                // ignored
            }

            Directory.CreateDirectory(path);
        }

        public static string PlatformFolderName(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows => "StandaloneWindows32",
                BuildTarget.StandaloneWindows64 => "StandaloneWindows64",
                BuildTarget.StandaloneOSX => "StandaloneOSX",
                BuildTarget.StandaloneLinux64 => "StandaloneLinux64",
                _ => target.ToString()
            };
        }

        private static RuntimeDependencyResolution CollectRuntimeDependencies(string scenePath,
            AddressableAssetSettings settings)
        {
            var result = new RuntimeDependencyResolution();
            if (settings == null || string.IsNullOrWhiteSpace(scenePath))
                return result;

            var claims = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            Scene scene = default;
            var opened = false;

            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                opened = true;

                CollectMapBindingAddressClaims(scene, claims);
                CollectEntityPrefabAddressClaims(scene, claims);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Runtime dependency scan failed: " + ex.Message, ex);
            }
            finally
            {
                if (opened && scene.IsValid())
                    EditorSceneManager.CloseScene(scene, true);
            }

            foreach (var claim in claims.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (TryResolveAddressToGuid(settings, claim.Key, out var guid))
                {
                    result.ResolvedEntries.Add(new RuntimeResolvedDependency(
                        guid,
                        claim.Key,
                        claim.Value.OrderBy(source => source, StringComparer.Ordinal).ToArray()));
                    continue;
                }

                result.MissingAddresses.Add(new RuntimeMissingDependency(
                    claim.Key,
                    claim.Value.OrderBy(source => source, StringComparer.Ordinal).ToArray()));
            }

            return result;
        }

        private static void CollectMapBindingAddressClaims(Scene scene, Dictionary<string, HashSet<string>> claims)
        {
            if (!scene.IsValid() || claims == null)
                return;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                var bindings = root.GetComponentsInChildren<MapBinding>(true);
                foreach (var binding in bindings)
                {
                    if (!binding || binding.Entries == null)
                        continue;

                    foreach (var entry in binding.Entries)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                            continue;

                        RegisterAddressClaim(
                            claims,
                            entry.key.Trim(),
                            $"MapBinding:{GetHierarchyPath(binding.transform)}");
                    }
                }
            }
        }

        private static void CollectEntityPrefabAddressClaims(Scene scene, Dictionary<string, HashSet<string>> claims)
        {
            if (!scene.IsValid() || claims == null)
                return;

            var entityClasses = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                var components = root.GetComponentsInChildren<Component>(true);
                foreach (var component in components)
                {
                    if (!component)
                        continue;

                    if (!TryReadStringMember(component, "EntityClassName", out var entityClassName))
                        continue;

                    entityClassName = entityClassName?.Trim();
                    if (!string.IsNullOrWhiteSpace(entityClassName))
                        entityClasses.Add(entityClassName);
                }
            }

            if (entityClasses.Count == 0)
                return;

            var byEntityClass = ResolveEntityPrefabAddressesByClass();
            foreach (var entityClass in entityClasses)
            {
                if (!byEntityClass.TryGetValue(entityClass, out var address) || string.IsNullOrWhiteSpace(address))
                    continue;

                RegisterAddressClaim(claims, address.Trim(), $"Entity:{entityClass}");
            }
        }

        private static Dictionary<string, string> ResolveEntityPrefabAddressesByClass()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var registryType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly =>
                        assembly?.GetType("Vellocet.Framework.Runtime.Entities.SDK.Base.EntityRegistry", false))
                    .FirstOrDefault(type => type != null);
                if (registryType == null)
                    return result;

                var getRulesMethod = registryType.GetMethod("GetRules",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                var rules = getRulesMethod?.Invoke(null, null) as IEnumerable;
                if (rules == null)
                    return result;

                foreach (var rule in rules)
                {
                    if (rule == null)
                        continue;

                    if (!TryReadStringMember(rule, "EntityClassName", out var entityClassName))
                        continue;
                    if (!TryReadStringMember(rule, "PrefabAddress", out var prefabAddress))
                        continue;

                    entityClassName = entityClassName?.Trim();
                    prefabAddress = prefabAddress?.Trim();
                    if (string.IsNullOrWhiteSpace(entityClassName) || string.IsNullOrWhiteSpace(prefabAddress))
                        continue;

                    result[entityClassName] = prefabAddress;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Map Builder] Could not resolve entity prefab addresses: {ex.Message}");
            }

            return result;
        }

        private static bool TryResolveAddressToGuid(AddressableAssetSettings settings, string address, out string guid)
        {
            guid = string.Empty;
            if (settings == null || string.IsNullOrWhiteSpace(address))
                return false;

            if (address.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                address.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                var directGuid = AssetDatabase.AssetPathToGUID(address);
                if (!string.IsNullOrWhiteSpace(directGuid))
                {
                    guid = directGuid;
                    return true;
                }
            }

            AddressableAssetEntry match = null;
            foreach (var group in settings.groups)
            {
                if (!group || group.entries == null)
                    continue;

                foreach (var entry in group.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.address))
                        continue;

                    if (!string.Equals(entry.address, address, StringComparison.Ordinal))
                        continue;

                    if (match == null || string.Compare(entry.guid, match.guid, StringComparison.Ordinal) < 0)
                        match = entry;
                }
            }

            if (match == null || string.IsNullOrWhiteSpace(match.guid))
                return false;

            guid = match.guid;
            return true;
        }

        private static bool TryReadStringMember(object source, string memberName, out string value)
        {
            value = null;
            if (source == null || string.IsNullOrWhiteSpace(memberName))
                return false;

            var type = source.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (property?.PropertyType == typeof(string))
            {
                value = property.GetValue(source) as string;
                return true;
            }

            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field?.FieldType == typeof(string))
            {
                value = field.GetValue(source) as string;
                return true;
            }

            return false;
        }

        private static void RegisterAddressClaim(Dictionary<string, HashSet<string>> claims, string address,
            string source)
        {
            if (claims == null || string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(source))
                return;

            if (!claims.TryGetValue(address, out var sources))
            {
                sources = new HashSet<string>(StringComparer.Ordinal);
                claims.Add(address, sources);
            }

            sources.Add(source);
        }

        private static string BuildMissingRuntimeDependencyError(
            IReadOnlyList<RuntimeMissingDependency> missingDependencies)
        {
            if (missingDependencies == null || missingDependencies.Count == 0)
                return "Runtime dependency resolution failed with unknown missing entries.";

            var lines = new List<string>
            {
                $"Runtime dependency resolution failed. Missing {missingDependencies.Count} addressable key(s):"
            };

            foreach (var missing in missingDependencies.Take(12))
            {
                var sourceText = missing.Sources is { Length: > 0 }
                    ? string.Join(", ", missing.Sources)
                    : "unknown source";
                lines.Add($"- '{missing.Address}' ({sourceText})");
            }

            if (missingDependencies.Count > 12)
                lines.Add($"- ... and {missingDependencies.Count - 12} more");

            lines.Add("Ensure these keys exist in Addressables or as valid asset paths.");
            return string.Join("\n", lines);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (!transform)
                return "<missing>";

            var parts = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }

        private sealed class RuntimeDependencyResolution
        {
            internal readonly List<RuntimeMissingDependency> MissingAddresses = new();
            internal readonly List<RuntimeResolvedDependency> ResolvedEntries = new();
        }

        private readonly struct RuntimeResolvedDependency
        {
            internal RuntimeResolvedDependency(string guid, string address, string[] sources)
            {
                Guid = guid;
                Address = address;
                Sources = sources ?? Array.Empty<string>();
            }

            internal string Guid { get; }
            internal string Address { get; }
            internal string[] Sources { get; }
        }

        private readonly struct RuntimeMissingDependency
        {
            internal RuntimeMissingDependency(string address, string[] sources)
            {
                Address = address;
                Sources = sources ?? Array.Empty<string>();
            }

            internal string Address { get; }
            internal string[] Sources { get; }
        }

        #endregion
    }
}
#endif
