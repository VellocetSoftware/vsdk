// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Editor.Tools;

namespace Vellocet.Sdk.Editor.Addons
{
    public sealed class AddonContentBuildRequest
    {
        public AddonAuthoringProject Project { get; set; }
        public string PackageOutputRoot { get; set; }
        public AddonMapBuildProfile Profile { get; set; } = AddonMapBuildProfile.Development;
    }

    public sealed class AddonContentBuildResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string BuildId { get; set; }
        public string AddonRoot { get; set; }
        public AddonManifest Manifest { get; set; }
    }

    /// <summary>
    ///     Builds non-map authoring sources into an add-on package. Existing map and VMod entries remain in the
    ///     package, and the complete staged package must validate before it replaces the previous output.
    /// </summary>
    public static class AddonContentBuildService
    {
        public static bool TryValidateRequest(AddonContentBuildRequest request, out string error)
        {
            error = null;
            if (request == null)
            {
                error = "Build request is required.";
                return false;
            }

            if (!SdkUnityVersionGuard.TryValidateForOperation("SDK content add-on build", out error))
                return false;

            if (!AddonAuthoringProjectPolicy.TryValidateIdentity(request.Project, out error))
                return false;

            if (!Enum.IsDefined(typeof(AddonMapBuildProfile), request.Profile))
            {
                error = $"Unknown add-on build profile '{request.Profile}'.";
                return false;
            }

            if (!SdkPackageOutputPathPolicy.TryResolve(request.PackageOutputRoot, out _, out error))
                return false;

            var isRelease = request.Profile == AddonMapBuildProfile.Release;
            if (!AddonThumbnailBuildUtility.TryValidateProjectAsset(request.Project.AddonThumbnail,
                    isRelease, isRelease, out error))
                return false;

            var sources = request.Project.ContentSources;
            if (sources.Length == 0)
            {
                error = "Add at least one content source to the Add-on Project.";
                return false;
            }

            var context = new AddonContentExportContext(request.Project, string.Empty,
                request.Profile == AddonMapBuildProfile.Release);
            var sourceIds = new HashSet<int>();
            for (var index = 0; index < sources.Length; index++)
            {
                var source = sources[index];
                if (!source)
                {
                    error = $"Content source at position {index + 1} is missing.";
                    return false;
                }

                if (!sourceIds.Add(source.GetInstanceID()))
                {
                    error = $"Content source '{source.name}' is listed more than once.";
                    return false;
                }

                if (!AddonContentSourceExporterRegistry.TryResolve(source, out var exporter,
                        out var exporterError))
                {
                    error = $"Content source '{source.name}': {exporterError}";
                    return false;
                }

                if (!exporter.TryValidate(source, context, out var sourceError))
                {
                    error = $"Content source '{source.name}': {sourceError}";
                    return false;
                }
            }

            return true;
        }

        public static AddonContentBuildResult Build(AddonContentBuildRequest request)
        {
            if (!TryValidateRequest(request, out var requestError))
                return new AddonContentBuildResult { Error = requestError };

            if (!SdkPackageOutputPathPolicy.TryResolveWritable(request.PackageOutputRoot,
                    out var packageOutputRoot,
                    out var exportError))
                return new AddonContentBuildResult { Error = exportError };

            var addonId = request.Project.AddonId;
            var finalAddonRoot = Path.Combine(packageOutputRoot, addonId);
            var buildId = AddonManifestBuildUtility.CreateBuildId();
            var stagingContainer = Path.Combine(packageOutputRoot, $".{addonId}.staging-{buildId}");
            var stagingAddonRoot = Path.Combine(stagingContainer, addonId);
            var preserveStaging = false;

            try
            {
                Directory.CreateDirectory(stagingContainer);
                if (!AddonPackageStagingUtility.TryCopyExistingPackage(finalAddonRoot, stagingAddonRoot,
                        null, out var stagingError))
                    return Failure(stagingError, buildId, finalAddonRoot);

                Directory.CreateDirectory(stagingAddonRoot);
                AddonManifest existing = null;
                var existingManifestPath = AddonHelper.GetManifestPath(stagingAddonRoot);
                if (File.Exists(existingManifestPath) &&
                    !AddonHelper.TryLoad(stagingAddonRoot, out existing, out var loadError))
                    return Failure(loadError, buildId, finalAddonRoot);

                if (existing != null &&
                    !string.Equals(existing.id, addonId, StringComparison.OrdinalIgnoreCase))
                    return Failure($"Existing package belongs to '{existing.id}', not '{addonId}'.",
                        buildId, finalAddonRoot);

                var context = new AddonContentExportContext(request.Project, stagingAddonRoot,
                    request.Profile == AddonMapBuildProfile.Release);
                var replacements = new List<AddonManifest.ContentEntry>();
                var discoveredRequirements = new List<string>();
                var replacementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var source in request.Project.ContentSources)
                {
                    if (!AddonContentSourceExporterRegistry.TryResolve(source, out var exporter,
                            out var resolveError))
                        return Failure(resolveError, buildId, finalAddonRoot);

                    if (!exporter.TryExport(source, context, out var content, out var requirements,
                            out var sourceError))
                        return Failure($"Content source '{source.name}': {sourceError}", buildId, finalAddonRoot);

                    if (content == null || string.IsNullOrWhiteSpace(content.kind) ||
                        string.IsNullOrWhiteSpace(content.id))
                        return Failure($"Exporter '{exporter.DisplayName}' returned an incomplete content entry.",
                            buildId, finalAddonRoot);

                    if (!string.Equals(content.kind, exporter.ContentKind, StringComparison.OrdinalIgnoreCase))
                        return Failure($"Exporter '{exporter.DisplayName}' returned content kind " +
                                       $"'{content.kind}' instead of '{exporter.ContentKind}'.",
                            buildId, finalAddonRoot);

                    content.id = ContentIdUtility.Sanitize(content.id);
                    if (string.IsNullOrWhiteSpace(content.id) || !replacementIds.Add(content.id))
                        return Failure($"Content ID '{content.id}' is invalid or used more than once.",
                            buildId, finalAddonRoot);

                    replacements.Add(content);
                    discoveredRequirements.AddRange(requirements ?? Array.Empty<string>());
                }

                var preserved = (existing?.contents ?? Array.Empty<AddonManifest.ContentEntry>())
                    .Where(value => value != null &&
                                    !replacementIds.Contains(ContentIdUtility.Sanitize(value.id)));
                var contents = preserved.Concat(replacements).ToArray();
                var thumbnail = CopyThumbnail(request.Project.AddonThumbnail, stagingAddonRoot,
                    existing?.addonThumbnail);
                if (request.Profile == AddonMapBuildProfile.Release && string.IsNullOrWhiteSpace(thumbnail))
                    return Failure("Release content add-ons require a PNG or JPEG add-on thumbnail.",
                        buildId, finalAddonRoot);

                var manifest = new AddonManifest
                {
                    id = addonId,
                    title = request.Project.Title,
                    author = request.Project.Author,
                    addonThumbnail = thumbnail,
                    version = request.Project.Version,
                    requires = AddonMapBuildService.MergeRequirements(addonId,
                        existing?.requires,
                        request.Project.RequiredAddonIds.Concat(discoveredRequirements)),
                    contents = contents
                };
                AddonManifestBuildUtility.ApplyGeneratedMetadata(manifest, request.Profile, buildId);

                if (!AddonMapBuildService.TryRefreshMapReceiptMetadata(stagingAddonRoot, manifest,
                        out var receiptError))
                    return Failure(receiptError, buildId, finalAddonRoot);

                AddonHelper.WriteManifest(stagingAddonRoot, manifest);
                var validationMode = request.Profile == AddonMapBuildProfile.Release
                    ? AddonPackageValidationMode.Release
                    : AddonPackageValidationMode.Standard;
                if (!AddonPackageValidator.TryValidate(stagingAddonRoot, validationMode, out manifest,
                        out var validationError))
                    return Failure(validationError, buildId, finalAddonRoot);

                if (!AddonPackagePromoter.TryPromote(stagingAddonRoot, finalAddonRoot, buildId,
                        out var publishError, out preserveStaging))
                    return Failure(publishError, buildId, finalAddonRoot);

                return new AddonContentBuildResult
                {
                    Success = true,
                    BuildId = buildId,
                    AddonRoot = finalAddonRoot,
                    Manifest = manifest
                };
            }
            catch (Exception exception)
            {
                Debug.LogError("[Add-on Content] Build failed.\n" + exception);
                return Failure($"Content add-on build failed: {exception.Message}", buildId, finalAddonRoot);
            }
            finally
            {
                if (!preserveStaging)
                    AddonPackagePromoter.CleanupStaging(stagingContainer, null);
            }
        }

        public static string ResolveAddonRoot(AddonAuthoringProject project, string exportRoot)
        {
            var normalizedRoot = SdkPathDefaults.NormalizePath(exportRoot);
            return !project || string.IsNullOrWhiteSpace(project.AddonId) ||
                   string.IsNullOrWhiteSpace(normalizedRoot)
                ? string.Empty
                : Path.Combine(normalizedRoot, project.AddonId);
        }

        private static string CopyThumbnail(Texture2D thumbnail, string addonRoot, string existingPath)
        {
            if (!thumbnail)
                return existingPath;

            var sourcePath = AssetDatabase.GetAssetPath(thumbnail);
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            var fileName = "thumbnail" + extension;
            File.Copy(sourcePath, Path.Combine(addonRoot, fileName), true);
            return fileName;
        }

        private static AddonContentBuildResult Failure(string error, string buildId, string addonRoot)
        {
            return new AddonContentBuildResult
            {
                Error = string.IsNullOrWhiteSpace(error) ? "Content add-on build failed." : error,
                BuildId = buildId,
                AddonRoot = addonRoot
            };
        }
    }
}
#endif