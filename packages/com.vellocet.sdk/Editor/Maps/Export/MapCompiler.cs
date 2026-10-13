// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Maps;
using UnityPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class MapCompiler
    {
        public static Result Compile(SceneAsset sceneAsset, string mapId, string sceneAddress, string contentKind,
            string outputScenePath = null, CompileOptions options = null)
        {
            var result = new Result
            {
                AssetUsageReport = new MapAssetUsageReport()
            };

            if (!SdkUnityVersionGuard.TryValidateForOperation("SDK map compilation", out var versionError))
            {
                result.Error = versionError;
                return result;
            }

            if (!TryValidateBuildConfiguration(options, "Final map compilation", out var buildConfigurationError))
            {
                result.Error = buildConfigurationError;
                return result;
            }

            if (!sceneAsset)
            {
                result.Error = "No scene asset was provided.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(mapId))
            {
                result.Error = "Map id is required.";
                return result;
            }

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (!settings)
            {
                result.Error = "Addressables settings not found.";
                return result;
            }

            var sourceScenePath = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrWhiteSpace(sourceScenePath) || !File.Exists(sourceScenePath))
            {
                result.Error = "Could not resolve the source scene path.";
                return result;
            }

            if (string.Equals(sourceScenePath, outputScenePath?.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                result.Error = "Compiled scene path must be different from the authoring scene path.";
                return result;
            }

            if (!SharedAssetAddressableEditorUtility.TryValidateSdkContentReady(out var contentReadinessError))
            {
                result.Error = contentReadinessError;
                return result;
            }

            SharedAssetAddressableEditorUtility.EnsureSdkSharedAssets(settings);
            var sharedGroup = SharedAssetAddressableEditorUtility.EnsureSharedGroup(settings);
            if (!sharedGroup)
            {
                result.Error = "Could not create or locate the shared Base addressables group.";
                return result;
            }

            AssetDatabase.SaveAssets();

            var compiledScenePath = outputScenePath;
            var usesTempOutput = string.IsNullOrWhiteSpace(compiledScenePath);
            if (usesTempOutput)
            {
                Directory.CreateDirectory(Constants.TempCompiledSceneDir);
                compiledScenePath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(Constants.TempCompiledSceneDir, Path.GetFileName(sourceScenePath)));
                result.TempFolder = Constants.TempCompiledSceneDir;
            }
            else
            {
                compiledScenePath = compiledScenePath.Replace('\\', '/');
                var outputDirectory = Path.GetDirectoryName(compiledScenePath);
                if (string.IsNullOrWhiteSpace(outputDirectory))
                {
                    result.Error = "Compiled scene output directory is invalid.";
                    return result;
                }

                Directory.CreateDirectory(outputDirectory);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(compiledScenePath))
                    AssetDatabase.DeleteAsset(compiledScenePath);
            }

            FileUtil.CopyFileOrDirectory(sourceScenePath, compiledScenePath);
            AssetDatabase.ImportAsset(compiledScenePath);

            result.CompiledScenePath = compiledScenePath;

            var sourceSceneGuid = AssetDatabase.AssetPathToGUID(sourceScenePath);
            var sourceSceneHash = MapCompileSession.ComputeFileSha256(sourceScenePath);
            var receipt = new MapCompilationReceipt
            {
                schemaVersion = MapCompilationReceipt.CurrentSchemaVersion,
                mapId = mapId,
                mapTitle = ObjectNames.NicifyVariableName(mapId),
                sceneAddress = string.IsNullOrWhiteSpace(sceneAddress) ? mapId : sceneAddress.Trim(),
                contentKind = string.IsNullOrWhiteSpace(contentKind)
                    ? MapCompilationReceipt.AddonContentKind
                    : contentKind.Trim(),
                createdUtc = DateTime.UtcNow.ToString("o"),
                compilerVersion = ReadSdkVersion(),
                unityVersion = Application.unityVersion,
                sourceScenePath = sourceScenePath,
                sourceSceneGuid = sourceSceneGuid ?? string.Empty,
                sourceSceneHash = sourceSceneHash,
                sourceDependenciesHash = MapSourceRevision.Compute(sourceScenePath),
                audioRuntime = SdkGame.Current.AudioRuntimeCapability,
                skipLightingBake = options?.SkipLightingBake ?? false,
                skipSpatialOctreeBake = options?.SkipSpatialOctreeBake ?? false,
                lightmapQuality = MapLightingSettingsUtility.GetDisplayName(
                    options?.LightmapQuality ?? MapLightmapQuality.Medium)
            };

            var session = new MapCompileSession(options, receipt, compiledScenePath);
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            MapAuthoringBakeArtifactSyncUtility.SkippedLightingArtifacts skippedLighting = null;

            try
            {
                session.Report(0.02f, $"Copying authoring scene to '{compiledScenePath}'.");
                if (!PrepareSkippedLightingArtifacts(sourceScenePath, compiledScenePath, options, session,
                        out skippedLighting, out var preserveError))
                {
                    result.Error = preserveError;
                    result.Receipt = session.FinalizeReceipt();
                    return result;
                }

                if (!TryOpenSceneForCompile(compiledScenePath, session, out var scene, out var openSceneError))
                {
                    result.Error = openSceneError;
                    result.Receipt = session.FinalizeReceipt();
                    return result;
                }

                if (!ApplySkippedLightingToCompiledScene(scene, skippedLighting,
                        session, "apply-skipped-lighting-before-compile-passes",
                        out var skippedLightingApplyError))
                {
                    result.Error = skippedLightingApplyError;
                    result.Receipt = session.FinalizeReceipt();
                    return result;
                }

                result.MusicSet = MapSettingsAuthoring.Compile(scene);
                session.Record("map-settings", true, "Music set: " + MapMusicSets.Name(result.MusicSet));

                var sharedGuids = SharedAssetAddressableEditorUtility.BuildSharedGuidSet(sharedGroup);
                var context = new MapCompileContext(sceneAsset, sourceScenePath, compiledScenePath, settings, options,
                    result, receipt)
                {
                    Scene = scene,
                    SharedGroup = sharedGroup,
                    SharedGuids = sharedGuids
                };

                foreach (var pass in MapCompilePassFactory.Create(context))
                {
                    if (session.RunPass(pass, context, out var passError))
                        continue;

                    result.Error = passError;
                    result.Receipt = session.FinalizeReceipt();
                    return result;
                }

                if (!ApplySkippedLightingToCompiledScene(scene, skippedLighting,
                        session, "restore-skipped-lighting-after-compile-passes",
                        out skippedLightingApplyError))
                {
                    result.Error = skippedLightingApplyError;
                    result.Receipt = session.FinalizeReceipt();
                    return result;
                }

                session.Report(0.985f, "Saving compiled scene.");
                options?.BeforeSaveScene?.Invoke(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    result.Error = $"Compiled scene could not be saved: {compiledScenePath}";
                    result.Receipt = session.FinalizeReceipt();
                    return result;
                }

                AssetDatabase.ImportAsset(compiledScenePath, ImportAssetOptions.ForceUpdate);

                if (string.Equals(receipt.contentKind, MapCompilationReceipt.AddonContentKind,
                        StringComparison.OrdinalIgnoreCase) &&
                    !AddonScenePolicy.TryValidate(scene, result.RuntimeDependencies, out var policyError))
                {
                    result.Error = $"Compiled addon scene failed its safety policy: {policyError}";
                    session.Record("validate-addon-scene-policy", false, policyError);
                    result.Receipt = session.FinalizeReceipt();
                    return result;
                }

                if (string.Equals(receipt.contentKind, MapCompilationReceipt.AddonContentKind,
                        StringComparison.OrdinalIgnoreCase))
                    session.Record("validate-addon-scene-policy", true,
                        "Scene components, dependencies, and resource budgets passed.");

                if (!SyncBakedArtifactsToAuthoring(context, session, out var syncError))
                {
                    result.Error = syncError;
                    result.Receipt = session.FinalizeReceipt();
                    return result;
                }

                result.CompiledScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(compiledScenePath);
                result.Receipt = session.FinalizeReceipt();
                if (!result.CompiledScene)
                {
                    result.Success = false;
                    result.Error = "Compiled scene asset could not be reloaded.";
                }
                else if (!MapSpatialGraphArtifacts.TryValidateCompiledResult(result, mapId,
                             receipt.sceneAddress, out var spatialValidationMessage))
                {
                    result.Success = false;
                    result.Error = $"Compiled spatial graph validation failed: {spatialValidationMessage}";
                    session.Record("validate-spatial-graph-artifact", false, spatialValidationMessage);
                    result.Receipt = session.FinalizeReceipt();
                }
                else
                {
                    session.Record("validate-spatial-graph-artifact", true, spatialValidationMessage);
                    result.Receipt = session.FinalizeReceipt();
                    result.Success = true;
                }

                session.Report(1f, result.Success
                    ? "Compilation finished successfully."
                    : "Compilation finished with errors.");
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                session.Log($"unexpected-error: {ex.Message}");
                session.Record("unexpected-error", false, ex.Message);
                result.Receipt = session.FinalizeReceipt();
                return result;
            }
            finally
            {
                var shouldReopenAuthoringScene = skippedLighting != null;
                try
                {
                    EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
                }
                catch (Exception ex)
                {
                    session.Log($"restore-scene-setup: {ex.Message}");
                }

                if (shouldReopenAuthoringScene)
                {
                    var reopened = ReopenAuthoringSceneAfterSkippedLighting(sourceScenePath, out var reopenMessage);
                    session.Record("reopen-authoring-scene-after-skipped-lighting", reopened, reopenMessage);
                    session.Log($"reopen-authoring-scene-after-skipped-lighting: {reopenMessage}");
                    if (!reopened)
                    {
                        result.Success = false;
                        result.Error = reopenMessage;
                    }

                    result.Receipt = session.FinalizeReceipt();
                }
            }
        }

        private static bool ReopenAuthoringSceneAfterSkippedLighting(string sourceScenePath, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(sourceScenePath) || !File.Exists(sourceScenePath))
            {
                message = $"Could not reopen authoring scene after skipped lighting compile: {sourceScenePath}";
                return false;
            }

            try
            {
                AssetDatabase.ImportAsset(sourceScenePath, ImportAssetOptions.ForceUpdate);
                var authoringScene = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Single);
                if (!authoringScene.IsValid())
                {
                    message = $"Could not reopen authoring scene after skipped lighting compile: {sourceScenePath}";
                    return false;
                }

                EditorSceneManager.SetActiveScene(authoringScene);
                message = "Reopened authoring scene after skipped lighting compile.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Could not reopen authoring scene after skipped lighting compile: {ex.Message}";
                return false;
            }
        }

        private static bool PrepareSkippedLightingArtifacts(string sourceScenePath, string compiledScenePath,
            CompileOptions options, MapCompileSession session,
            out MapAuthoringBakeArtifactSyncUtility.SkippedLightingArtifacts artifacts, out string error)
        {
            artifacts = null;
            error = string.Empty;
            if (!(options?.SkipLightingBake ?? false))
                return true;

            session.Report(0.025f, "Copying existing authoring bake artifacts into skipped-lighting compiled scene.");
            var succeeded = MapAuthoringBakeArtifactSyncUtility.PrepareSkippedLighting(
                sourceScenePath, compiledScenePath, out artifacts, out var message);
            session.Record(MapAuthoringBakeArtifactSyncUtility.PreserveSkippedLightingPassName, succeeded, message);
            session.Log($"{MapAuthoringBakeArtifactSyncUtility.PreserveSkippedLightingPassName}: {message}");

            if (succeeded)
                return true;

            error = message;
            return false;
        }

        private static bool ApplySkippedLightingToCompiledScene(Scene scene,
            MapAuthoringBakeArtifactSyncUtility.SkippedLightingArtifacts artifacts, MapCompileSession session,
            string passName, out string error)
        {
            error = string.Empty;
            if (artifacts == null)
                return true;

            var succeeded = artifacts.ApplyTo(scene, out var message);
            session.Record(passName, succeeded, message);
            session.Log($"{passName}: {message}");
            if (succeeded)
                return true;

            error = message;
            return false;
        }

        private static bool SyncBakedArtifactsToAuthoring(MapCompileContext context,
            MapCompileSession session, out string error)
        {
            error = string.Empty;
            if (!(context.Options?.SyncBakedArtifactsToAuthoring ?? true))
                return true;

            if (context.Options?.SkipLightingBake ?? false)
            {
                const string message =
                    "Skipped authoring bake artifact sync because lighting bake was skipped. Existing authoring lighting data was left unchanged.";
                session.Record(MapAuthoringBakeArtifactSyncUtility.PassName, true, message);
                session.Log($"{MapAuthoringBakeArtifactSyncUtility.PassName}: {message}");
                return true;
            }

            session.Report(0.99f, "Applying compiled bake artifacts back to authoring scene.");
            var succeeded = MapAuthoringBakeArtifactSyncUtility.Sync(context, out var syncMessage);
            session.Record(MapAuthoringBakeArtifactSyncUtility.PassName, succeeded, syncMessage);
            session.Log($"{MapAuthoringBakeArtifactSyncUtility.PassName}: {syncMessage}");

            if (succeeded)
                return true;

            error = syncMessage;
            return false;
        }

        private static bool TryOpenSceneForCompile(string scenePath, MapCompileSession session, out Scene scene,
            out string error)
        {
            scene = default;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                error = "Compiled scene path is missing.";
                return false;
            }

            CancelOutstandingEditorBakes(session);

            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                return scene.IsValid();
            }
            catch (Exception firstOpenException)
            {
                session.Log($"open-compiled-scene-first-attempt: {firstOpenException.Message}");
            }

            try
            {
                // Reset editor scene state before retrying the compiled-scene open.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            catch (Exception resetException)
            {
                error = $"Could not reset editor scene state before opening compiled scene: {resetException.Message}";
                return false;
            }

            CancelOutstandingEditorBakes(session);

            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (scene.IsValid())
                    return true;

                error = $"Compiled scene could not be opened: {scenePath}";
                return false;
            }
            catch (Exception retryOpenException)
            {
                error = $"Could not open compiled scene '{scenePath}': {retryOpenException.Message}";
                return false;
            }
        }

        private static void CancelOutstandingEditorBakes(MapCompileSession session)
        {
            try
            {
                if (Lightmapping.isRunning)
                {
                    Lightmapping.Cancel();
                    session.Log("Canceled active lightmapping task before scene switch.");
                }
            }
            catch (Exception ex)
            {
                session.Log($"lightmapping-cancel-warning: {ex.Message}");
            }

            try
            {
                if (StaticOcclusionCulling.isRunning)
                {
                    StaticOcclusionCulling.Cancel();
                    session.Log("Canceled active occlusion bake task before scene switch.");
                }
            }
            catch (Exception ex)
            {
                session.Log($"occlusion-cancel-warning: {ex.Message}");
            }
        }

        private static string ReadSdkVersion()
        {
            try
            {
                var packageJsonPath = Constants.RootPath + "/package.json";
                var registered = UnityPackageInfo.FindForAssetPath(packageJsonPath) ??
                                 UnityPackageInfo.FindForPackageName(Constants.PackageId);
                if (!string.IsNullOrWhiteSpace(registered?.version))
                    return registered.version.Trim();

                var packageManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(packageJsonPath);
                if (packageManifest)
                {
                    var serialized = JsonUtility.FromJson<PackageManifestInfo>(packageManifest.text);
                    if (!string.IsNullOrWhiteSpace(serialized?.version))
                        return serialized.version.Trim();
                }
            }
            catch
            {
                // The release validator reports a clear repair instruction when version lookup fails.
            }

            return "unknown";
        }

        internal static bool TryValidateBuildConfiguration(CompileOptions options, string operation,
            out string error)
        {
            error = null;
            if (!(options?.RequireNonDevelopmentBuild ?? false) || !EditorUserBuildSettings.development)
                return true;

            error =
                $"{(string.IsNullOrWhiteSpace(operation) ? "Final map build" : operation)} requires Unity's Development Build option to be disabled.";
            return false;
        }

        public sealed class Result
        {
            public bool Success { get; set; }
            public string Error { get; set; }
            public SceneAsset CompiledScene { get; set; }
            public string CompiledScenePath { get; set; }
            public string TempFolder { get; set; }
            public List<RuntimeDependency> RuntimeDependencies { get; } = new();
            public MapAssetUsageReport AssetUsageReport { get; set; }
            public MapCompilationReceipt Receipt { get; set; }
            public MapMusicSet MusicSet { get; set; }
            public MapPackageAssets PackageAssets { get; set; }

            internal void AddRuntimeDependency(string guid, string address)
            {
                guid = guid?.Trim();
                if (string.IsNullOrWhiteSpace(guid))
                    return;

                for (var i = 0; i < RuntimeDependencies.Count; i++)
                    if (string.Equals(RuntimeDependencies[i].Guid, guid, StringComparison.OrdinalIgnoreCase))
                        return;

                RuntimeDependencies.Add(new RuntimeDependency(
                    guid,
                    address?.Trim() ?? string.Empty));
            }
        }

        public readonly struct RuntimeDependency
        {
            public RuntimeDependency(string guid, string address)
            {
                Guid = guid;
                Address = address;
            }

            public string Guid { get; }
            public string Address { get; }
        }

        public sealed class CompileOptions
        {
            public Vertex.Contracts.GameBakePolicy BakePolicy { get; set; }
            public MapLightmapQuality LightmapQuality { get; set; } = MapLightmapQuality.Medium;
            public bool RequireNonDevelopmentBuild { get; set; }
            public bool SkipLightingBake { get; set; }
            public bool SkipSpatialOctreeBake { get; set; }
            public bool ConfigureToolVisuals { get; set; } = true;
            public bool PruneGeneratedMeshes { get; set; } = true;
            public bool FinalizeLegacyGeometry { get; set; } = true;
            public TextAsset ScriptSource { get; set; }
            public bool SyncBakedArtifactsToAuthoring { get; set; } = true;
            public Action<float, string> ReportProgress { get; set; }
            public Action<string> Log { get; set; }
            public Action<Scene> BeforeSaveScene { get; set; }
        }

        [Serializable]
        private sealed class PackageManifestInfo
        {
            public string version;
        }
    }
}
#endif
