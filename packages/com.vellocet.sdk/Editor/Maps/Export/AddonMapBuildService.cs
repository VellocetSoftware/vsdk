// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Editor.Addons;
using Vellocet.Sdk.Editor.Audio;
using Vellocet.Sdk.Editor.Tools;
using Vellocet.Sdk.Maps;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor.Maps
{
    public sealed class AddonMapBuildRequest
    {
        public SceneAsset SceneAsset { get; set; }
        public string ScenePath { get; set; }
        public string PackageOutputRoot { get; set; }
        public string AddonId { get; set; }
        public string AddonTitle { get; set; }
        public string MapId { get; set; }
        public string MapTitle { get; set; }
        public string DefaultModeId { get; set; }
        public string AddonAuthor { get; set; }
        public string AddonVersion { get; set; }
        public Texture2D MapImage { get; set; }
        public Texture2D AddonThumbnail { get; set; }
        public IReadOnlyList<string> RequiredAddonIds { get; set; } = Array.Empty<string>();
        public bool CleanOutput { get; set; } = true;
        public IReadOnlyList<BuildTarget> Targets { get; set; } = Array.Empty<BuildTarget>();
        public MapLightmapQuality LightmapQuality { get; set; } = MapLightmapQuality.Medium;
        public bool SkipLightingBake { get; set; }
        public bool SkipSpatialOctreeBake { get; set; }
        public bool ConfigureToolVisuals { get; set; } = true;
        public bool PruneGeneratedMeshes { get; set; } = true;
        public AddonMapBuildProfile Profile { get; set; } = AddonMapBuildProfile.Development;
        public MapCompiler.Result Compiled { get; set; }
    }

    public sealed class AddonMapBuildResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string BuildId { get; set; }
        public AddonMapBuildProfile Profile { get; set; }
        public AddonMapArtifactLayout Layout { get; set; }
        public IReadOnlyList<BuildTarget> SuccessfulPlatforms { get; set; }
        public IReadOnlyList<Exporter.Result> SuccessfulPlatformBuilds { get; set; }
    }

    public static class AddonMapBuildService
    {
        public static AddonMapBuildResult Build(AddonMapBuildRequest request, IMapBuildProgressSink progress = null)
        {
            if (request == null)
                return new AddonMapBuildResult { Error = "Build request is required." };

            if (!SdkUnityVersionGuard.TryValidateForOperation("SDK add-on package build", out var versionError))
                return new AddonMapBuildResult { Error = versionError };

            if (!SdkPackageOutputPathPolicy.TryResolveWritable(request.PackageOutputRoot, out var normalizedRoot,
                    out var exportRootError))
                return new AddonMapBuildResult { Error = exportRootError };

            request.PackageOutputRoot = normalizedRoot;
            // Every package is assembled in a fresh staging directory before promotion.
            request.CleanOutput = true;
            if (!Enum.IsDefined(typeof(AddonMapBuildProfile), request.Profile))
                return new AddonMapBuildResult { Error = $"Unknown add-on build profile '{request.Profile}'." };

            var releaseArtwork = request.Profile == AddonMapBuildProfile.Release;
            // Vertex exports can acquire Workshop artwork in the game's publishing UI.
            if (!AddonThumbnailBuildUtility.TryValidateProjectAsset(request.AddonThumbnail,
                    releaseArtwork && request.Compiled == null, releaseArtwork, out var thumbnailError))
                return new AddonMapBuildResult { Error = thumbnailError, Profile = request.Profile };

            if (!AddonMapBuildPolicy.TryNormalizeTargets(request.Targets, out var requestedTargets,
                    out var targetError))
                return new AddonMapBuildResult { Error = targetError };

            request.Targets = requestedTargets;
            if (request.Profile == AddonMapBuildProfile.Release &&
                !AddonMapBuildPolicy.TryValidateReleaseRequest(request, out var releasePolicyError))
                return new AddonMapBuildResult { Error = releasePolicyError, Profile = request.Profile };

            request.MapId = MapBuildArtifactPlanner.NormalizeMapId(request.MapId);
            request.AddonId = AddonIdPolicy.NormalizeOrEmpty(request.AddonId);
            request.DefaultModeId = MapBuildArtifactPlanner.NormalizeDefaultModeId(request.DefaultModeId);
            try { request.DefaultModeId = SdkGame.Current.ResolveMapMode(request.MapId, request.DefaultModeId); }
            catch (ArgumentException error) { return new AddonMapBuildResult { Error = error.Message, Profile = request.Profile }; }

            if (string.IsNullOrWhiteSpace(request.MapId))
                return new AddonMapBuildResult { Error = "Map id is required." };

            if (string.IsNullOrWhiteSpace(request.AddonId))
                return new AddonMapBuildResult
                {
                    Error = "Add-on ID is required and must be a lowercase dotted ID such as 'author.package'."
                };

            if (string.IsNullOrWhiteSpace(request.DefaultModeId))
                return new AddonMapBuildResult
                {
                    Error = "Default mode ID is required and must be a valid built-in or dotted custom ID."
                };

            var finalLayout = MapBuildArtifactPlanner.PlanAddon(request.PackageOutputRoot, request.AddonId,
                request.MapId);
            if (string.IsNullOrWhiteSpace(finalLayout.AddonRoot) || string.IsNullOrWhiteSpace(finalLayout.MapRoot))
                return new AddonMapBuildResult { Error = "Could not resolve a safe add-on package layout." };

            var buildId = AddonManifestBuildUtility.CreateBuildId();
            var isRelease = request.Profile == AddonMapBuildProfile.Release;
            var stagingRoot = Path.Combine(request.PackageOutputRoot, $".{request.AddonId}.staging-{buildId}");
            var buildLayout = MapBuildArtifactPlanner.PlanAddon(stagingRoot, request.AddonId, request.MapId);

            try
            {
                Directory.CreateDirectory(stagingRoot);
            }
            catch (Exception ex)
            {
                return new AddonMapBuildResult
                {
                    Error = $"Could not create the package staging folder: {ex.Message}",
                    BuildId = buildId,
                    Profile = request.Profile,
                    Layout = finalLayout
                };
            }

            var preserveStaging = false;
            try
            {
                if (!TrySeedExistingContentExceptMap(finalLayout.AddonRoot, buildLayout.AddonRoot,
                        request.MapId, out var seedError))
                    return new AddonMapBuildResult
                    {
                        Error = seedError,
                        BuildId = buildId,
                        Profile = request.Profile,
                        Layout = finalLayout
                    };

                var result = BuildPackage(request, buildLayout, requestedTargets, buildId, progress);
                result.Layout = finalLayout;
                if (!result.Success)
                    return result;

                progress?.Report(0.995f, isRelease
                    ? "Finalizing validated release package."
                    : "Finalizing validated development package.");
                progress?.Log("Replacing package output transactionally.");
                if (!AddonPackagePromoter.TryPromote(buildLayout.AddonRoot, finalLayout.AddonRoot, buildId,
                        out var publishError, out preserveStaging))
                {
                    result.Success = false;
                    result.Error = publishError;
                    return result;
                }

                progress?.Report(1f, isRelease ? "Release package complete." : "Development package complete.");
                progress?.Log(isRelease
                    ? "Release package built successfully."
                    : "Development package built successfully.");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Map Builder] Add-on package build failed unexpectedly.\n{ex}");
                return new AddonMapBuildResult
                {
                    Error = $"Add-on package build failed unexpectedly: {ex.Message}",
                    BuildId = buildId,
                    Profile = request.Profile,
                    Layout = finalLayout,
                    SuccessfulPlatforms = Array.Empty<BuildTarget>(),
                    SuccessfulPlatformBuilds = Array.Empty<Exporter.Result>()
                };
            }
            finally
            {
                if (!preserveStaging)
                    AddonPackagePromoter.CleanupStaging(stagingRoot, progress);
            }
        }

        private static AddonMapBuildResult BuildPackage(AddonMapBuildRequest request,
            AddonMapArtifactLayout layout, IReadOnlyList<BuildTarget> requestedTargets, string buildId,
            IMapBuildProgressSink progress)
        {
            var anySuccess = false;
            var successfulPlatforms = new List<BuildTarget>();
            var successfulExports = new List<Exporter.Result>();
            var failureReasons = new List<string>();
            var originalTarget = EditorUserBuildSettings.activeBuildTarget;
            var originalGroup = BuildPipeline.GetBuildTargetGroup(originalTarget);
            var originalStandaloneSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            var originalBuildProfile = BuildProfile.GetActiveBuildProfile();
            string restoreBuildTargetError = null;
            MapCompiler.Result sharedCompileResult = null;

            AddonMapBuildResult Fail(string error)
            {
                return new AddonMapBuildResult
                {
                    Error = error,
                    BuildId = buildId,
                    Profile = request.Profile,
                    Layout = layout,
                    SuccessfulPlatforms = successfulPlatforms,
                    SuccessfulPlatformBuilds = successfulExports
                };
            }

            try
            {
                progress?.Report(0.05f, "Preparing package build.");
                sharedCompileResult = CompileSceneOnce(request, progress);
                if (sharedCompileResult is not { Success: true })
                {
                    var compileError = string.IsNullOrWhiteSpace(sharedCompileResult?.Error)
                        ? "Map compilation failed."
                        : sharedCompileResult.Error;
                    return Fail(compileError);
                }

                if (!MapSpatialGraphArtifacts.TryValidateCompiledResult(sharedCompileResult, request.MapId,
                        request.MapId, out var spatialValidationMessage))
                    return Fail(
                        $"Shared compile did not produce a packageable spatial graph: {spatialValidationMessage}");

                progress?.Log(spatialValidationMessage);

                // Addon builds use Unity's platform profiles rather than inheriting game-specific
                // settings from whichever asset-backed Build Profile happens to be active.
                BuildProfile.SetActiveBuildProfile(null);

                BuildForTargets(request, layout, sharedCompileResult, progress, successfulPlatforms, successfulExports,
                    failureReasons, ref anySuccess);
            }
            finally
            {
                try
                {
                    // Addon content builds use Unity's platform profile so a custom profile for another
                    // platform cannot leave the target's Build button disabled for Addressables/SBP.
                    BuildProfile.SetActiveBuildProfile(null);
                    EditorUserBuildSettings.standaloneBuildSubtarget = originalStandaloneSubtarget;
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(originalGroup, originalTarget))
                    {
                        restoreBuildTargetError =
                            "Failed to restore the original Unity build target after the package build.";
                    }
                    else
                    {
                        BuildProfile.SetActiveBuildProfile(originalBuildProfile);
                        if (BuildProfile.GetActiveBuildProfile() != originalBuildProfile)
                            restoreBuildTargetError =
                                "Failed to restore the original Unity Build Profile after the package build.";
                    }
                }
                catch (Exception ex)
                {
                    restoreBuildTargetError =
                        $"Failed to restore build target state after the package build: {ex.Message}";
                }

                CleanupTempCompileArtifacts(sharedCompileResult?.TempFolder, progress);
            }

            if (!string.IsNullOrWhiteSpace(restoreBuildTargetError))
                return Fail(restoreBuildTargetError);

            if (!anySuccess)
            {
                var details = failureReasons.Count == 0
                    ? "No targets were built successfully."
                    : "No targets were built successfully.\n\n" + string.Join("\n", failureReasons);
                return Fail(details);
            }

            var requireAllTargets = request.Profile == AddonMapBuildProfile.Release;
            if (requireAllTargets && successfulPlatforms.Count != requestedTargets.Count)
            {
                var missing = requestedTargets
                    .Where(target => !successfulPlatforms.Contains(target))
                    .Select(AddonMapBuildPolicy.PlatformDisplayLabel)
                    .ToArray();
                var missingMessage = missing.Length > 0 ? string.Join(", ", missing) : "unknown";
                var details = $"Release package requires every platform to succeed. Missing: {missingMessage}.";
                if (failureReasons.Count > 0)
                    details += "\n\n" + string.Join("\n", failureReasons);

                return Fail(details);
            }

            progress?.Log(
                "Writing add-on thumbnail, in-game map image, compilation receipt, manifest, and build report.");
            var addonThumbnailRelativePath = CopyThumbnailIntoPackage(
                request.AddonThumbnail, layout.AddonRoot, layout.AddonRoot);
            var mapImageRelativePath = CopyThumbnailIntoPackage(
                request.MapImage, layout.MapRoot, layout.AddonRoot);
            if (!TryBuildPlatformArtifacts(layout.AddonRoot, successfulExports, out var platformArtifacts,
                    out var artifactsError))
                return Fail(artifactsError);

            progress?.Log("Building owned source audio and resolving shared add-on cue dependencies.");
            if (!AddonAudioBuildPipeline.TryBuild(request, layout, successfulPlatforms,
                    out var audioContent, out var audioDependencies, out var audioError))
                return Fail(audioError);

            if (!TryWriteCompilationReceipt(request, layout, platformArtifacts, successfulExports, buildId,
                    out var receiptRelativePath, out var receiptError))
                return Fail(receiptError);

            if (!TryWriteAddonManifest(request, layout, platformArtifacts, audioContent, addonThumbnailRelativePath,
                    audioDependencies, mapImageRelativePath, receiptRelativePath, buildId, successfulExports,
                    MapMusicSets.Name(sharedCompileResult.MusicSet), out var manifestError))
                return Fail(manifestError);
            WriteExportReport(request, layout.MapRoot, successfulExports);

            progress?.Report(0.99f, "Validating package integrity.");
            progress?.Log("Validating package structure, platform matrix, file sizes, and SHA-256 hashes.");
            var validationMode = request.Profile == AddonMapBuildProfile.Release
                ? AddonPackageValidationMode.Release
                : AddonPackageValidationMode.Standard;
            if (!AddonPackageValidator.TryValidate(layout.AddonRoot, validationMode, out _,
                    out var validationError))
                return Fail($"Package validation failed: {validationError}");

            return new AddonMapBuildResult
            {
                Success = true,
                BuildId = buildId,
                Profile = request.Profile,
                Layout = layout,
                SuccessfulPlatforms = successfulPlatforms,
                SuccessfulPlatformBuilds = successfulExports
            };
        }

        private static void BuildForTargets(AddonMapBuildRequest request, AddonMapArtifactLayout layout,
            MapCompiler.Result sharedCompileResult, IMapBuildProgressSink progress,
            List<BuildTarget> successfulPlatforms,
            List<Exporter.Result> successfulExports, List<string> failureReasons, ref bool anySuccess)
        {
            var targets = request.Targets ?? Array.Empty<BuildTarget>();
            if (targets.Count == 0)
                return;

            const float exportStart = 0.4f;
            const float exportTotalSpan = 0.58f;
            var perTargetSpan = exportTotalSpan / targets.Count;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var progressAnchor = exportStart + perTargetSpan * i;
                var progressSpan = Mathf.Clamp(perTargetSpan * 0.96f, 0.08f, 0.4f);
                if (!BuildForPlatform(request, layout, sharedCompileResult, target, progressAnchor, progressSpan,
                        progress, out var export, out var failureReason))
                {
                    var group = BuildPipeline.GetBuildTargetGroup(target);
                    var reason = !string.IsNullOrWhiteSpace(failureReason)
                        ? $"- {AddonMapBuildPolicy.PlatformDisplayLabel(target)}: {failureReason}"
                        : !BuildPipeline.IsBuildTargetSupported(group, target)
                            ? $"- {AddonMapBuildPolicy.PlatformDisplayLabel(target)}: Build support module is not installed in this Unity editor."
                            : $"- {AddonMapBuildPolicy.PlatformDisplayLabel(target)}: Failed during the platform content build. See Console for details.";
                    progress?.Log(reason.TrimStart('-', ' '));
                    failureReasons.Add(reason);
                    continue;
                }

                anySuccess = true;
                successfulPlatforms.Add(target);
                successfulExports.Add(export);
                progress?.Log($"[{AddonMapBuildPolicy.PlatformDisplayLabel(target)}] Platform build succeeded.");
            }
        }

        private static bool BuildForPlatform(AddonMapBuildRequest request, AddonMapArtifactLayout layout,
            MapCompiler.Result sharedCompileResult, BuildTarget target, float progressAnchor, float progressSpan,
            IMapBuildProgressSink progress,
            out Exporter.Result exportResult, out string failureReason)
        {
            exportResult = null;
            failureReason = null;
            var label = AddonMapBuildPolicy.PlatformDisplayLabel(target);
            progress?.Log($"[{label}] Switching build target.");
            progress?.Report(progressAnchor, $"[{label}] Switching build target.");

            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError(
                    $"[Map Builder] Build support is not installed for target: {target}. Install this module in Unity Hub > Installs > Add modules.");
                failureReason = "Build support module is not installed in this Unity editor.";
                return false;
            }

            var desiredSubtarget = target == BuildTarget.StandaloneLinux64
                ? StandaloneBuildSubtarget.Server
                : StandaloneBuildSubtarget.Player;
            if (target == BuildTarget.StandaloneLinux64)
                progress?.Log($"[{label}] Using dedicated server subtarget.");

            EditorUserBuildSettings.standaloneBuildSubtarget = desiredSubtarget;
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                Debug.LogError($"[Map Builder] Failed to switch to build target: {target}");
                failureReason = "Failed to switch active build target.";
                return false;
            }

            var outDir = MapBuildArtifactPlanner.ComputePlatformOutputDirectory(layout.MapRoot, target);
            if (request.CleanOutput && Directory.Exists(outDir))
                try
                {
                    Directory.Delete(outDir, true);
                }
                catch (Exception e)
                {
                    failureReason = $"Could not clean existing output '{outDir}': {e.Message}";
                    Debug.LogError($"[Map Builder] {failureReason}");
                    return false;
                }

            if (!TryConfigureContentBuildState(target, desiredSubtarget, request.Profile,
                    out var namedBuildTarget, out var originalScriptingBackend,
                    out var originalDevelopmentBuild, out failureReason))
            {
                Debug.LogError($"[Map Builder] {failureReason}");
                return false;
            }

            string buildStateRestoreError = null;
            try
            {
                var buildMode = EditorUserBuildSettings.development ? "development" : "non-development";
                progress?.Log(
                    $"[{label}] Unity platform Build Profile activated with a {buildMode}, Mono-backed content build.");
                progress?.Report(progressAnchor + 0.02f, $"[{label}] Building platform content.");
                progress?.Log($"[{label}] Platform build started.");

                exportResult = Exporter.ExportMap(
                    request.SceneAsset,
                    request.MapId,
                    layout.AddonRoot,
                    target,
                    CreateCompileOptions(request, progressAnchor, progressSpan, label, progress),
                    sharedCompileResult,
                    (localProgress, message) => progress?.Report(
                        Mathf.Clamp01(progressAnchor + localProgress * progressSpan),
                        $"[{label}] {message}"),
                    message => progress?.Log($"[{label}] {message}"));
            }
            finally
            {
                var restoreErrors = new List<string>();
                if (!TryRestoreScriptingBackend(namedBuildTarget, originalScriptingBackend,
                        out var backendRestoreError))
                    restoreErrors.Add(backendRestoreError);
                if (AddonMapBuildPolicy.RequiresNonDevelopmentBuild(request.Profile) &&
                    !TrySetDevelopmentBuild(originalDevelopmentBuild, out var developmentRestoreError))
                    restoreErrors.Add(developmentRestoreError);

                if (restoreErrors.Count > 0)
                {
                    buildStateRestoreError = string.Join(" ", restoreErrors);
                    Debug.LogError($"[Map Builder] {buildStateRestoreError}");
                }
            }

            if (!string.IsNullOrWhiteSpace(buildStateRestoreError))
            {
                failureReason = buildStateRestoreError;
                return false;
            }

            if (exportResult is { Success: true })
                return true;

            failureReason = string.IsNullOrWhiteSpace(exportResult?.Error)
                ? "Platform content build failed."
                : exportResult.Error.Trim();
            return false;
        }

        private static bool TryConfigureContentBuildState(BuildTarget target,
            StandaloneBuildSubtarget desiredSubtarget, AddonMapBuildProfile profile,
            out NamedBuildTarget namedBuildTarget, out ScriptingImplementation originalScriptingBackend,
            out bool originalDevelopmentBuild, out string error)
        {
            namedBuildTarget = desiredSubtarget == StandaloneBuildSubtarget.Server
                ? NamedBuildTarget.Server
                : NamedBuildTarget.Standalone;
            originalScriptingBackend = default;
            originalDevelopmentBuild = EditorUserBuildSettings.development;
            var backendCaptured = false;
            var developmentBuildCaptured = false;

            try
            {
                // Unity 6 keeps an asset-backed Build Profile active when the classic target setting changes.
                // If that profile targets another platform, SBP reports only "Unable to build with the
                // current configuration." The platform profile follows the switched target and avoids
                // leaking game-specific profile settings into built add-on bundles.
                BuildProfile.SetActiveBuildProfile(null);
                EditorUserBuildSettings.standaloneBuildSubtarget = desiredSubtarget;

                if (BuildProfile.GetActiveBuildProfile() != null ||
                    EditorUserBuildSettings.activeBuildTarget != target ||
                    EditorUserBuildSettings.standaloneBuildSubtarget != desiredSubtarget)
                {
                    error =
                        $"Unity did not activate the required platform build state ({target}/{desiredSubtarget}).";
                    return false;
                }

                originalDevelopmentBuild = EditorUserBuildSettings.development;
                developmentBuildCaptured = true;
                if (AddonMapBuildPolicy.RequiresNonDevelopmentBuild(profile) &&
                    !TrySetDevelopmentBuild(false, out var developmentBuildError))
                    throw new InvalidOperationException(developmentBuildError);

                // Addressables only needs a valid player configuration to build bundles. Inheriting an
                // IL2CPP backend can disable SBP when cross-building (for example, Windows IL2CPP on
                // macOS or Linux IL2CPP without a cross-toolchain), even though no player is produced.
                originalScriptingBackend = PlayerSettings.GetScriptingBackend(namedBuildTarget);
                backendCaptured = true;
                PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.Mono2x);
                if (PlayerSettings.GetScriptingBackend(namedBuildTarget) != ScriptingImplementation.Mono2x)
                    throw new InvalidOperationException(
                        $"Unity did not activate the Mono scripting backend for {namedBuildTarget.TargetName}.");

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to configure the Unity content-build state: {ex.Message}";
                if (backendCaptured &&
                    !TryRestoreScriptingBackend(namedBuildTarget, originalScriptingBackend,
                        out var restoreError))
                    error += $" {restoreError}";
                if (developmentBuildCaptured && AddonMapBuildPolicy.RequiresNonDevelopmentBuild(profile) &&
                    !TrySetDevelopmentBuild(originalDevelopmentBuild, out var developmentRestoreError))
                    error += $" {developmentRestoreError}";

                return false;
            }
        }

        internal static bool TrySetDevelopmentBuild(bool developmentBuild, out string error)
        {
            try
            {
                if (EditorUserBuildSettings.development != developmentBuild)
                    EditorUserBuildSettings.development = developmentBuild;
                if (EditorUserBuildSettings.development != developmentBuild)
                {
                    error = developmentBuild
                        ? "Unity did not restore the Development Build option."
                        : "Unity did not disable the Development Build option for the final map build.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = developmentBuild
                    ? $"Failed to restore the Development Build option: {ex.Message}"
                    : $"Failed to disable the Development Build option for the final map build: {ex.Message}";
                return false;
            }
        }

        private static bool TryRestoreScriptingBackend(NamedBuildTarget namedBuildTarget,
            ScriptingImplementation backend, out string error)
        {
            try
            {
                PlayerSettings.SetScriptingBackend(namedBuildTarget, backend);
                if (PlayerSettings.GetScriptingBackend(namedBuildTarget) != backend)
                {
                    error = $"Unity did not restore the {namedBuildTarget.TargetName} scripting backend.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to restore the {namedBuildTarget.TargetName} scripting backend: {ex.Message}";
                return false;
            }
        }

        private static MapCompiler.CompileOptions CreateCompileOptions(AddonMapBuildRequest request,
            float progressAnchor, float progressSpan, string scopeLabel, IMapBuildProgressSink progress)
        {
            return new MapCompiler.CompileOptions
            {
                LightmapQuality = request.LightmapQuality,
                RequireNonDevelopmentBuild = AddonMapBuildPolicy.RequiresNonDevelopmentBuild(request.Profile),
                SkipLightingBake = request.SkipLightingBake,
                SkipSpatialOctreeBake = request.SkipSpatialOctreeBake,
                ConfigureToolVisuals = request.ConfigureToolVisuals,
                PruneGeneratedMeshes = request.PruneGeneratedMeshes,
                ReportProgress = (value, message) => progress?.Report(
                    Mathf.Clamp01(progressAnchor + value * progressSpan),
                    string.IsNullOrWhiteSpace(scopeLabel) ? message : $"[{scopeLabel}] {message}"),
                Log = message => progress?.Log(
                    string.IsNullOrWhiteSpace(scopeLabel) ? message : $"[{scopeLabel}] {message}")
            };
        }

        private static MapCompiler.Result CompileSceneOnce(AddonMapBuildRequest request, IMapBuildProgressSink progress)
        {
            if (request.Compiled != null)
            {
                if (!request.Compiled.Success || !request.Compiled.CompiledScene || request.Compiled.PackageAssets == null)
                    return new MapCompiler.Result { Error = "Precompiled packaging requires an audited scene and ownership plan." };
                return request.Compiled;
            }
            progress?.Report(0.08f, "Compiling authoring scene once for selected platforms.");
            progress?.Log("Compiling authoring scene once for selected platforms.");

            var requireNonDevelopmentBuild = AddonMapBuildPolicy.RequiresNonDevelopmentBuild(request.Profile);
            var originalDevelopmentBuild = EditorUserBuildSettings.development;
            if (requireNonDevelopmentBuild && !TrySetDevelopmentBuild(false, out var buildConfigurationError))
            {
                if (!TrySetDevelopmentBuild(originalDevelopmentBuild, out var restoreAfterFailureError))
                    buildConfigurationError += $" {restoreAfterFailureError}";
                return new MapCompiler.Result { Error = buildConfigurationError };
            }

            MapCompiler.Result compileResult = null;
            string restoreError = null;
            try
            {
                compileResult = MapCompiler.Compile(
                    request.SceneAsset,
                    request.MapId,
                    request.MapId,
                    MapCompilationReceipt.AddonContentKind,
                    null,
                    CreateCompileOptions(request, 0.1f, 0.26f, "Compile", progress));
            }
            finally
            {
                if (requireNonDevelopmentBuild &&
                    !TrySetDevelopmentBuild(originalDevelopmentBuild, out restoreError))
                    Debug.LogError($"[Map Builder] {restoreError}");
            }

            if (!string.IsNullOrWhiteSpace(restoreError))
            {
                compileResult ??= new MapCompiler.Result();
                compileResult.Success = false;
                compileResult.Error = restoreError;
            }

            if (compileResult is { Success: true })
                progress?.Log("Shared compile succeeded; reusing the compiled scene for all platform builds.");

            return compileResult;
        }

        private static void CleanupTempCompileArtifacts(string tempFolder, IMapBuildProgressSink progress)
        {
            if (string.IsNullOrWhiteSpace(tempFolder))
                return;

            try
            {
                if (AssetDatabase.IsValidFolder(tempFolder))
                    AssetDatabase.DeleteAsset(tempFolder);
                else if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
            catch (Exception ex)
            {
                progress?.Log($"Shared compile cleanup warning: {ex.Message}");
                Debug.LogWarning($"[Map Builder] Cleanup of temp folder failed: {ex.Message}");
            }
        }

        internal static bool TrySeedExistingContentExceptMap(string sourceRoot, string destinationRoot,
            string replacedMapId, out string error)
        {
            error = null;
            var normalizedMapId = ContentIdUtility.Sanitize(replacedMapId);
            if (string.IsNullOrWhiteSpace(normalizedMapId))
            {
                error = "The map being replaced has an invalid id.";
                return false;
            }

            var replacedMapPrefix = $"{Constants.MapsFolder.Trim('/', '\\')}/{normalizedMapId}/";
            return AddonPackageStagingUtility.TryCopyExistingPackage(sourceRoot, destinationRoot,
                relativePath => relativePath.StartsWith(replacedMapPrefix, StringComparison.OrdinalIgnoreCase),
                out error);
        }

        private static bool TryWriteAddonManifest(AddonMapBuildRequest request, AddonMapArtifactLayout layout,
            IReadOnlyList<PlatformArtifactData> platformArtifacts, AddonManifest.ContentEntry audioContent,
            string addonThumbnailRelativePath,
            IReadOnlyList<string> audioDependencies, string mapImageRelativePath, string receiptRelativePath,
            string buildId, IReadOnlyList<Exporter.Result> exports, string musicSet, out string error)
        {
            error = null;
            var platformEntries = (platformArtifacts ?? Array.Empty<PlatformArtifactData>())
                .Where(artifact => !string.IsNullOrWhiteSpace(artifact?.RelativeCatalogPath))
                .Select(artifact => new AddonManifest.PlatformCatalog
                {
                    name = artifact.PlatformName,
                    role = artifact.PlatformRole,
                    catalog = artifact.RelativeCatalogPath,
                    files = artifact.Files
                })
                .ToArray();

            AddonHelper.TryLoad(layout.AddonRoot, out var existingManifest, out _);
            var mapContent = new AddonManifest.ContentEntry
            {
                kind = AddonManifest.MapContentKind,
                id = request.MapId,
                title = request.MapTitle,
                map = new AddonManifest.MapContent
                {
                    scene = request.MapId,
                    defaultModeId = MapBuildArtifactPlanner.NormalizeDefaultModeId(request.DefaultModeId),
                    musicSet = musicSet,
                    thumbnail = mapImageRelativePath,
                    compiledReceipt = receiptRelativePath,
                    platforms = platformEntries
                }
            };
            var contents = MergeMapContent(existingManifest, mapContent);
            contents = MergeMapAudioContent(contents, request.MapId, audioContent);
            var hasSharedContent = contents.Length > 1;

            var manifest = new AddonManifest
            {
                id = request.AddonId,
                title = !string.IsNullOrWhiteSpace(request.AddonTitle)
                    ? request.AddonTitle.Trim()
                    : hasSharedContent && !string.IsNullOrWhiteSpace(existingManifest?.title)
                        ? existingManifest.title
                        : request.MapTitle,
                author = string.IsNullOrWhiteSpace(request.AddonAuthor) ? string.Empty : request.AddonAuthor.Trim(),
                addonThumbnail = string.IsNullOrWhiteSpace(addonThumbnailRelativePath)
                    ? existingManifest?.addonThumbnail
                    : addonThumbnailRelativePath,
                version = string.IsNullOrWhiteSpace(request.AddonVersion) ? string.Empty : request.AddonVersion.Trim(),
                requires = MergeRequirements(request.AddonId, existingManifest?.requires,
                    (request.RequiredAddonIds ?? Array.Empty<string>()).Concat(
                        audioDependencies ?? Array.Empty<string>())),
                contents = contents
            };
            var sdkVersion = (exports ?? Array.Empty<Exporter.Result>())
                .Select(export => export?.CompilationReceipt?.compilerVersion)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            AddonManifestBuildUtility.ApplyGeneratedMetadata(manifest, request.Profile, buildId, sdkVersion);

            if (!TryRefreshMapReceiptMetadata(layout.AddonRoot, manifest, out error))
                return false;

            AddonHelper.WriteManifest(layout.AddonRoot, manifest);
            return true;
        }

        internal static string[] MergeRequirements(string addonId, IEnumerable<string> existing,
            IEnumerable<string> discovered)
        {
            var owner = AddonIdPolicy.NormalizeOrEmpty(addonId);
            return (existing ?? Array.Empty<string>())
                .Concat(discovered ?? Array.Empty<string>())
                .Select(AddonIdPolicy.NormalizeOrEmpty)
                .Where(value => !string.IsNullOrWhiteSpace(value) &&
                                !string.Equals(value, owner, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static AddonManifest.ContentEntry[] MergeMapContent(AddonManifest existingManifest,
            AddonManifest.ContentEntry replacement)
        {
            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));

            var replacementId = ContentIdUtility.Sanitize(replacement.id);
            return (existingManifest?.contents ?? Array.Empty<AddonManifest.ContentEntry>())
                .Where(content => content != null &&
                                  (!string.Equals(content.kind, AddonManifest.MapContentKind,
                                       StringComparison.OrdinalIgnoreCase) ||
                                   !string.Equals(ContentIdUtility.Sanitize(content.id), replacementId,
                                       StringComparison.OrdinalIgnoreCase)))
                .Concat(new[] { replacement })
                .ToArray();
        }

        internal static AddonManifest.ContentEntry[] MergeMapAudioContent(
            IEnumerable<AddonManifest.ContentEntry> existingContents, string mapId,
            AddonManifest.ContentEntry replacement)
        {
            var audioContentId = ContentIdUtility.Sanitize(mapId + "_audio");
            var preserved = (existingContents ?? Array.Empty<AddonManifest.ContentEntry>())
                .Where(content => content != null &&
                                  (!string.Equals(content.kind, AddonManifest.AudioContentKind,
                                       StringComparison.OrdinalIgnoreCase) ||
                                   !string.Equals(ContentIdUtility.Sanitize(content.id), audioContentId,
                                       StringComparison.OrdinalIgnoreCase)));
            return replacement == null ? preserved.ToArray() : preserved.Concat(new[] { replacement }).ToArray();
        }

        internal static bool TryRefreshMapReceiptMetadata(string addonRoot, AddonManifest manifest,
            out string error)
        {
            error = null;
            foreach (var content in (manifest?.contents ?? Array.Empty<AddonManifest.ContentEntry>())
                     .Where(content => content != null &&
                                       string.Equals(content.kind, AddonManifest.MapContentKind,
                                           StringComparison.OrdinalIgnoreCase)))
            {
                if (content.map == null ||
                    !AddonHelper.TryResolvePath(addonRoot, content.map.compiledReceipt,
                        out var receiptPath, out _) ||
                    !MapCompilationReceiptUtility.TryLoad(receiptPath, out var receipt) ||
                    !MapCompilationReceiptUtility.HasValidSeal(receipt))
                {
                    error = $"Map '{content.id}' has no valid compilation receipt to reseal.";
                    return false;
                }

                if (!string.Equals(receipt.contentKind, MapCompilationReceipt.AddonContentKind,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(receipt.mapId, content.id, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(receipt.sceneAddress, content.map.scene, StringComparison.Ordinal))
                {
                    error = $"Map '{content.id}' compilation receipt identity does not match the add-on manifest.";
                    return false;
                }

                receipt.buildId = manifest.buildId;
                receipt.distributionProfile = manifest.distributionProfile;
                receipt.distributionPolicy = manifest.distributionPolicy;
                receipt.compilerVersion = manifest.sdkVersion;
                receipt.unityVersion = manifest.unityVersion;
                MapCompilationReceiptUtility.Seal(receipt);
                File.WriteAllText(receiptPath, MapCompilationReceiptUtility.Serialize(receipt));
            }

            return true;
        }

        private static bool TryBuildPlatformArtifacts(string addonRoot, IReadOnlyList<Exporter.Result> exports,
            out IReadOnlyList<PlatformArtifactData> artifacts, out string error)
        {
            artifacts = Array.Empty<PlatformArtifactData>();
            error = null;

            if (string.IsNullOrWhiteSpace(addonRoot))
            {
                error = "Add-on root is missing.";
                return false;
            }

            if (exports == null || exports.Count == 0)
            {
                error = "No successful platform build outputs were found.";
                return false;
            }

            var result = new List<PlatformArtifactData>(exports.Count);
            foreach (var export in exports.Where(value => value != null))
            {
                var platformName = AddonMapBuildPolicy.PlatformName(export.Target);
                if (string.IsNullOrWhiteSpace(export.OutputDirectory) || !Directory.Exists(export.OutputDirectory))
                {
                    error = $"Platform build output folder is missing for '{platformName}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(export.CatalogPath) || !File.Exists(export.CatalogPath))
                {
                    error = $"Catalog binary is missing for '{platformName}'.";
                    return false;
                }

                var relativeCatalog = MapBuildArtifactPlanner.MakeRelativePath(addonRoot, export.CatalogPath);
                if (string.IsNullOrWhiteSpace(relativeCatalog))
                {
                    error = $"Catalog path could not be normalized for '{platformName}'.";
                    return false;
                }

                var files = BuildPlatformFiles(addonRoot, export.OutputDirectory);
                if (files.Length == 0)
                {
                    error = $"No platform files were discovered for '{platformName}'.";
                    return false;
                }

                result.Add(new PlatformArtifactData
                {
                    PlatformName = platformName,
                    PlatformRole = AddonMapBuildPolicy.PlatformRole(export.Target),
                    RelativeCatalogPath = relativeCatalog,
                    Files = files
                });
            }

            if (result.Count == 0)
            {
                error = "No platform artifacts were produced from build results.";
                return false;
            }

            artifacts = result
                .OrderBy(value => value.PlatformName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return true;
        }

        private static AddonManifest.PlatformFile[] BuildPlatformFiles(string addonRoot, string platformDirectory)
        {
            if (string.IsNullOrWhiteSpace(addonRoot) || string.IsNullOrWhiteSpace(platformDirectory) ||
                !Directory.Exists(platformDirectory))
                return Array.Empty<AddonManifest.PlatformFile>();

            var files = Directory.GetFiles(platformDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var results = new List<AddonManifest.PlatformFile>(files.Length);
            foreach (var filePath in files)
            {
                var relativePath = MapBuildArtifactPlanner.MakeRelativePath(addonRoot, filePath);
                if (string.IsNullOrWhiteSpace(relativePath))
                    continue;
                if (!AddonFilePolicy.TryNormalizePackageFilePath(relativePath, out var normalizedRelativePath))
                {
                    Debug.LogWarning(
                        $"[Map Builder] Skipping unsupported add-on file type in the package manifest: '{relativePath}'.");
                    continue;
                }

                var info = new FileInfo(filePath);
                results.Add(new AddonManifest.PlatformFile
                {
                    path = normalizedRelativePath,
                    size = info.Exists ? info.Length : 0L,
                    sha256 = TryComputeArtifactSha256(filePath)
                });
            }

            return results.ToArray();
        }

        private static string TryComputeArtifactSha256(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return string.Empty;

            try
            {
                return SdkFileHashUtility.TryComputeSha256(filePath, out var sha256) ? sha256 : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string CopyThumbnailIntoPackage(Texture2D thumbnail, string destinationRoot, string addonRoot)
        {
            if (!thumbnail)
                return null;

            var sourcePath = AssetDatabase.GetAssetPath(thumbnail);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return null;

            TryCreateDirectory(destinationRoot);
            var extension = Path.GetExtension(sourcePath);
            var destinationPath = Path.Combine(destinationRoot, $"thumbnail{extension}");
            File.Copy(sourcePath, destinationPath, true);
            return MapBuildArtifactPlanner.MakeRelativePath(addonRoot, destinationPath);
        }

        private static bool TryWriteCompilationReceipt(AddonMapBuildRequest request, AddonMapArtifactLayout layout,
            IReadOnlyList<PlatformArtifactData> platformArtifacts, IReadOnlyList<Exporter.Result> exports,
            string buildId, out string receiptRelativePath, out string error)
        {
            receiptRelativePath = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(layout.AddonRoot) || string.IsNullOrWhiteSpace(layout.MapRoot))
            {
                error = "Add-on artifact layout is invalid.";
                return false;
            }

            if (exports == null)
            {
                error = "No platform build results are available for writing the compilation receipt.";
                return false;
            }

            var receipt = exports
                .Select(export => export?.CompilationReceipt)
                .FirstOrDefault(compilationReceipt => compilationReceipt != null);
            if (receipt == null)
            {
                error = "Compilation receipt data is missing from the platform build results.";
                return false;
            }

            receipt.schemaVersion = MapCompilationReceipt.CurrentSchemaVersion;
            receipt.mapId = request.MapId;
            receipt.mapTitle = request.MapTitle;
            receipt.sceneAddress = request.MapId;
            receipt.contentKind = MapCompilationReceipt.AddonContentKind;
            receipt.distributionProfile = AddonMapBuildPolicy.DistributionProfile(request.Profile);
            receipt.distributionPolicy = AddonMapBuildPolicy.DistributionPolicy(request.Profile);
            receipt.buildId = buildId;

            if (!MapSpatialGraphArtifacts.TryValidateReceipt(receipt, request.MapId, request.MapId,
                    out var spatialReceiptError))
            {
                error = $"Compilation receipt spatial graph validation failed: {spatialReceiptError}";
                return false;
            }

            receipt.platforms = (platformArtifacts ?? Array.Empty<PlatformArtifactData>())
                .Select(artifact =>
                {
                    return new MapCompilationReceipt.PlatformArtifact
                    {
                        name = artifact.PlatformName,
                        role = artifact.PlatformRole,
                        catalog = string.IsNullOrWhiteSpace(artifact.RelativeCatalogPath)
                            ? string.Empty
                            : artifact.RelativeCatalogPath,
                        files = (artifact.Files ?? Array.Empty<AddonManifest.PlatformFile>())
                            .Select(file => new MapCompilationReceipt.ArtifactFile
                            {
                                path = file.path,
                                size = file.size,
                                sha256 = file.sha256
                            })
                            .ToArray()
                    };
                })
                .ToArray();

            MapCompilationReceiptUtility.Seal(receipt);

            TryCreateDirectory(layout.MapRoot);
            File.WriteAllText(layout.ReceiptPath, MapCompilationReceiptUtility.Serialize(receipt));
            receiptRelativePath = MapBuildArtifactPlanner.MakeRelativePath(layout.AddonRoot, layout.ReceiptPath);
            if (string.IsNullOrWhiteSpace(receiptRelativePath))
            {
                error = "Compilation receipt path could not be normalized.";
                return false;
            }

            return true;
        }

        private static void WriteExportReport(AddonMapBuildRequest request, string mapRoot,
            IReadOnlyList<Exporter.Result> exports)
        {
            if (string.IsNullOrWhiteSpace(mapRoot) || exports == null || exports.Count == 0)
                return;

            var assetUsage = exports
                .Select(export => export?.AssetUsageReport)
                .FirstOrDefault(report => report != null);
            var catalogs = exports
                .Select(export => export?.CatalogPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            var bundles = exports
                .SelectMany(export => export?.BundlePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            var receipt = exports
                .Select(export => export?.CompilationReceipt)
                .FirstOrDefault(compilationReceipt => compilationReceipt != null);

            MapExportReportWriter.Write(mapRoot, request.MapId, request.ScenePath, assetUsage, catalogs, bundles,
                receipt);
        }

        private static void TryCreateDirectory(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private sealed class PlatformArtifactData
        {
            internal string PlatformName { get; set; }
            internal string PlatformRole { get; set; }
            internal string RelativeCatalogPath { get; set; }
            internal AddonManifest.PlatformFile[] Files { get; set; } = Array.Empty<AddonManifest.PlatformFile>();
        }
    }
}
#endif
