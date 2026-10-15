// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using Vellocet.Sdk.Audio;
using Vellocet.Sdk.Maps;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Addons
{
    public enum AddonPackageValidationMode
    {
        Standard = 0,
        Release = 1
    }

    /// <summary>
    ///     Validates package consistency and declared artifact integrity at export, import, and publication boundaries.
    ///     Integrity hashes do not authenticate the package author.
    /// </summary>
    public static class AddonPackageValidator
    {
        public static bool TryValidate(string addonRoot, AddonPackageValidationMode mode,
            out AddonManifest manifest,
            out string error)
        {
            try
            {
                return TryValidateCore(addonRoot, mode, out manifest, out error);
            }
            catch (Exception ex)
            {
                manifest = null;
                error = $"Failed to validate addon package: {ex.Message}";
                return false;
            }
        }

        public static bool TryValidatePackageShape(string addonRoot, CancellationToken cancellationToken,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(addonRoot) || !Directory.Exists(addonRoot))
            {
                error = "Addon folder does not exist.";
                return false;
            }

            var directoryCount = 0;
            var fileCount = 0;
            var totalBytes = 0L;
            var observedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var rootInfo = new DirectoryInfo(addonRoot);
                if (IsReparsePoint(rootInfo.Attributes))
                {
                    error = "Addon root cannot be a symbolic link or reparse point.";
                    return false;
                }

                // Walk one level at a time so a reparse point is rejected before enumeration can
                // descend through it. SearchOption.AllDirectories may follow directory links on
                // some runtimes before the caller has a chance to inspect their attributes.
                var pending = new Stack<DirectoryInfo>();
                pending.Push(rootInfo);
                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var current = pending.Pop();
                    foreach (var directory in current.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (IsReparsePoint(directory.Attributes))
                        {
                            error = $"Addon directory '{directory.Name}' is a symbolic link or reparse point.";
                            return false;
                        }

                        directoryCount++;
                        if (directoryCount > AddonFilePolicy.MaximumDirectoriesPerPackage)
                        {
                            error = "Addon package exceeds the supported directory-count limit.";
                            return false;
                        }

                        var relativeDirectoryPath = GetPackageRelativePath(addonRoot, directory.FullName);
                        if (!AddonFilePolicy.TryNormalizeSafeDirectoryPath(relativeDirectoryPath,
                                out var normalizedDirectoryPath) ||
                            !string.Equals(relativeDirectoryPath, normalizedDirectoryPath,
                                StringComparison.Ordinal))
                        {
                            error = $"Addon directory path '{relativeDirectoryPath}' is not canonical or portable.";
                            return false;
                        }

                        if (!observedPaths.Add(normalizedDirectoryPath))
                        {
                            error = "Addon package contains a case-insensitive path collision at " +
                                    $"'{normalizedDirectoryPath}'.";
                            return false;
                        }

                        pending.Push(directory);
                    }

                    foreach (var file in current.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (IsReparsePoint(file.Attributes))
                        {
                            error = $"Addon file '{file.Name}' is a symbolic link or reparse point.";
                            return false;
                        }

                        // Finder metadata is not addon content. The exporter uses the same policy,
                        // and tolerating it here keeps a package valid if Finder recreates the file.
                        if (SdkContentPathUtility.ShouldSkipFile(file.FullName))
                            continue;

                        fileCount++;
                        if (fileCount > AddonFilePolicy.MaximumFilesPerPackage ||
                            !AddonFilePolicy.TryAccumulate(ref totalBytes, file.Length,
                                AddonFilePolicy.MaximumPackageBytes))
                        {
                            error = "Addon package exceeds the supported file-count or size limit.";
                            return false;
                        }

                        var relativeFilePath = GetPackageRelativePath(addonRoot, file.FullName);
                        if (!SdkGame.Current.SupportsExternalMods && AddonGamePolicy.IsCodeFile(relativeFilePath))
                        {
                            error = $"{SdkGame.Current.DisplayName} does not support external code: '{relativeFilePath}'.";
                            return false;
                        }
                        if (!AddonFilePolicy.TryValidateFileSize(relativeFilePath, file.Length,
                                out var fileError))
                        {
                            error = $"Addon file '{relativeFilePath}' was rejected: {fileError}";
                            return false;
                        }

                        if (!observedPaths.Add(relativeFilePath))
                        {
                            error = "Addon package contains a case-insensitive path collision at " +
                                    $"'{relativeFilePath}'.";
                            return false;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                error = $"Addon package could not be inspected: {ex.Message}";
                return false;
            }

            return true;
        }

        private static bool TryValidateCore(string addonRoot, AddonPackageValidationMode mode,
            out AddonManifest manifest, out string error)
        {
            manifest = null;
            error = null;
            if (string.IsNullOrWhiteSpace(addonRoot) || !Directory.Exists(addonRoot))
            {
                error = "Addon folder does not exist.";
                return false;
            }

            if (!TryValidatePackageShape(addonRoot, CancellationToken.None, out error))
                return false;

            if (!AddonHelper.TryLoadForValidation(addonRoot, out manifest, out error) || manifest == null)
                return false;

            if (!AddonGamePolicy.TryValidate(manifest, SdkGame.Current, out error))
                return false;

            if (!Enum.IsDefined(typeof(AddonPackageValidationMode), mode))
            {
                error = $"Unknown package validation mode '{mode}'.";
                return false;
            }

            if (!AddonDistributionPolicy.TryValidateManifestMetadata(manifest,
                    mode == AddonPackageValidationMode.Release, out error))
                return false;

            if (string.IsNullOrWhiteSpace(manifest.id))
            {
                error = "Addon manifest id is missing.";
                return false;
            }

            if (!TryValidateOptionalFile(addonRoot, manifest.addonThumbnail, "Addon thumbnail", out error))
                return false;

            if (!DateTimeOffset.TryParse(manifest.createdUtc, out _))
            {
                error = "Addon manifest createdUtc is missing or invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.unityVersion))
            {
                error = "Addon manifest unityVersion is missing.";
                return false;
            }

            var contents = manifest.contents ?? Array.Empty<AddonManifest.ContentEntry>();
            if (contents.Length == 0)
            {
                error = "Addon manifest does not contain any content entries.";
                return false;
            }

            var contentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var content in contents)
            {
                if (content == null || string.IsNullOrWhiteSpace(content.kind) ||
                    string.IsNullOrWhiteSpace(content.id))
                {
                    error = "Addon manifest contains an incomplete content entry.";
                    return false;
                }

                if (!contentIds.Add(content.id))
                {
                    error = $"Addon content id '{content.id}' is declared more than once.";
                    return false;
                }

                if (string.Equals(content.kind, AddonManifest.MapContentKind,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryValidateMap(addonRoot, manifest, content, out error))
                        return false;
                }
                else if (string.Equals(content.kind, AddonManifest.ModContentKind,
                             StringComparison.OrdinalIgnoreCase) &&
                         !AddonModContentPolicy.TryResolveManifest(addonRoot, content, out _, out error))
                {
                    return false;
                }
                else if (string.Equals(content.kind, AddonManifest.AudioContentKind,
                             StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryValidateAudio(addonRoot, manifest, content, out error))
                        return false;
                }
                else if (string.Equals(content.kind, AddonManifest.MediaContentKind, StringComparison.OrdinalIgnoreCase))
                {
                    try { AddonMedia.Read(addonRoot, manifest, content); }
                    catch (Exception exception) { error = exception.Message; return false; }
                }
                else if (!string.Equals(content.kind, AddonManifest.ModContentKind,
                             StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Addon content kind '{content.kind}' is not supported.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidateAudio(string addonRoot, AddonManifest manifest,
            AddonManifest.ContentEntry content, out string error)
        {
            error = null;
            if (SdkGame.Current.AudioRuntimeCapability == AddonAudioBanks.Capability)
            {
                try { AddonAudioBanks.Validate(addonRoot, manifest, content); return true; }
                catch (Exception ex) { error = "Invalid Stereoform audio: " + ex.Message; return false; }
            }
            var audio = content.audio;
            if (audio == null)
            {
                error = $"Audio content '{content.id}' is missing its audio payload.";
                return false;
            }

            if (!AddonFilePolicy.TryNormalizePackageFilePath(audio.catalog, out var normalizedCatalog) ||
                !string.Equals(Path.GetExtension(normalizedCatalog), ".json", StringComparison.OrdinalIgnoreCase) ||
                !AddonHelper.TryResolvePath(addonRoot, normalizedCatalog, out var catalogPath, out _) ||
                !File.Exists(catalogPath))
            {
                error = $"Audio content '{content.id}' has a missing or invalid catalog.";
                return false;
            }

            var files = audio.files ?? Array.Empty<AddonManifest.PlatformFile>();
            if (files.Length < 2)
            {
                error = $"Audio content '{content.id}' has an incomplete file manifest.";
                return false;
            }

            var catalogDirectory = NormalizeDirectory(Path.GetDirectoryName(normalizedCatalog));
            var filesByPath = new Dictionary<string, AddonManifest.PlatformFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                if (file == null || file.size <= 0 || !AddonFilePolicy.IsValidSha256(file.sha256) ||
                    !AddonFilePolicy.TryNormalizePackageFilePath(file.path, out var normalizedPath) ||
                    !filesByPath.TryAdd(normalizedPath, file) ||
                    !IsInDirectory(normalizedPath, catalogDirectory) ||
                    !AddonHelper.TryResolvePath(addonRoot, normalizedPath, out var absolutePath, out _) ||
                    !File.Exists(absolutePath))
                {
                    error = $"Audio content '{content.id}' contains a missing or invalid file.";
                    return false;
                }

                var info = new FileInfo(absolutePath);
                if (info.Length != file.size)
                {
                    error = $"Audio content '{content.id}' file '{normalizedPath}' has the wrong size.";
                    return false;
                }

                if (!TryValidateFileSha256(absolutePath, file.sha256,
                        $"Audio content '{content.id}' file '{normalizedPath}'", out error))
                    return false;

                if (!string.Equals(normalizedPath, normalizedCatalog, StringComparison.OrdinalIgnoreCase) &&
                    (!AddonAudioPolicy.IsSupportedMediaExtension(Path.GetExtension(normalizedPath)) ||
                     info.Length > AddonAudioPolicy.MaximumSourceFileBytes))
                {
                    error = $"Audio content '{content.id}' file '{normalizedPath}' is not a supported source file.";
                    return false;
                }
            }

            if (!filesByPath.ContainsKey(normalizedCatalog))
            {
                error = $"Audio content '{content.id}' catalog is not included in its file manifest.";
                return false;
            }

            AddonAudioCatalog catalog;
            try
            {
                catalog = JsonUtility.FromJson<AddonAudioCatalog>(File.ReadAllText(catalogPath));
            }
            catch (Exception ex)
            {
                error = $"Audio content '{content.id}' catalog could not be read: {ex.Message}";
                return false;
            }

            if (catalog == null || !AddonAudioPolicy.IsSupportedSchemaVersion(catalog.schemaVersion) ||
                catalog.cues == null || catalog.cues.Length == 0 ||
                catalog.cues.Length > AddonAudioPolicy.MaximumCueCount)
            {
                error = $"Audio content '{content.id}' catalog is empty, oversized, or uses an unsupported schema.";
                return false;
            }

            var cueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cue in catalog.cues)
            {
                if (cue == null || !AddonAudioPolicy.TryNormalizeCueId(manifest.id, cue.id, out var cueId) ||
                    !cueIds.Add(cueId) ||
                    !AddonAudioPolicy.TryParseTemplate(cue.template, out var template) ||
                    !AddonAudioPolicy.IsTemplateSupported(catalog.schemaVersion, template) ||
                    !AddonAudioPolicy.TryResolveGain(catalog.schemaVersion, cue.gain, out _) ||
                    !AddonAudioPolicy.TryNormalizeMediaPath(cue.media, out var mediaRelative))
                {
                    error = $"Audio content '{content.id}' contains an invalid or duplicate cue.";
                    return false;
                }

                var mediaPath = string.IsNullOrEmpty(catalogDirectory)
                    ? mediaRelative
                    : catalogDirectory + "/" + mediaRelative;
                if (!filesByPath.ContainsKey(mediaPath))
                {
                    error = $"Audio cue '{cueId}' references undeclared media '{mediaRelative}'.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidateMap(string addonRoot, AddonManifest manifest,
            AddonManifest.ContentEntry content, out string error)
        {
            error = null;
            var map = content.map;
            if (map == null)
            {
                error = $"Map '{content.id}' is missing its map payload.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(map.scene))
            {
                error = $"Map '{content.id}' is missing its scene key.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(map.defaultModeId) &&
                !ModeIdPolicy.TryNormalize(map.defaultModeId, out map.defaultModeId))
            {
                error = $"Map '{content.id}' has invalid default mode id '{map.defaultModeId}'.";
                return false;
            }

            if (!Maps.MapMusicSets.TryParse(map.musicSet, out _))
            {
                error = $"Map '{content.id}' has an unknown music set '{map.musicSet}'.";
                return false;
            }

            if (!TryValidateOptionalFile(addonRoot, map.thumbnail, $"Map '{content.id}' thumbnail", out error))
                return false;

            var platforms = map.platforms ?? Array.Empty<AddonManifest.PlatformCatalog>();
            if (platforms.Length == 0)
            {
                error = $"Map '{content.id}' does not declare any platform artifacts.";
                return false;
            }

            var platformNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var platform in platforms)
            {
                if (platform == null || string.IsNullOrWhiteSpace(platform.name))
                {
                    error = $"Map '{content.id}' contains an incomplete platform declaration.";
                    return false;
                }

                if (!platformNames.Add(platform.name.Trim()))
                {
                    error = $"Map '{content.id}' declares platform '{platform.name}' more than once.";
                    return false;
                }

                if (!TryValidatePlatform(addonRoot, content.id, platform, out error))
                    return false;
            }

            if (string.IsNullOrWhiteSpace(map.compiledReceipt))
            {
                error = $"Map '{content.id}' is missing its compilation receipt.";
                return false;
            }

            return TryValidateReceipt(addonRoot, manifest, content, out error);
        }

        private static bool TryValidatePlatform(string addonRoot, string mapId,
            AddonManifest.PlatformCatalog platform, out string error)
        {
            error = null;
            if (!AddonDistributionPolicy.TryGetExpectedRole(platform.name, out var expectedRole))
            {
                error = $"Map '{mapId}' declares unsupported platform '{platform.name}'.";
                return false;
            }

            if (!string.Equals(platform.role, expectedRole, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Map '{mapId}' platform '{platform.name}' must use the '{expectedRole}' role.";
                return false;
            }

            if (!AddonFilePolicy.TryNormalizePackageFilePath(platform.catalog, out var normalizedCatalog) ||
                !AddonHelper.TryResolvePath(addonRoot, normalizedCatalog, out var catalogPath, out _))
            {
                error = $"Map '{mapId}' platform '{platform.name}' has an invalid catalog path.";
                return false;
            }

            if (!File.Exists(catalogPath))
            {
                error = $"Map '{mapId}' platform '{platform.name}' is missing catalog '{normalizedCatalog}'.";
                return false;
            }

            var files = platform.files ?? Array.Empty<AddonManifest.PlatformFile>();
            if (files.Length == 0)
            {
                error = $"Map '{mapId}' platform '{platform.name}' has no file manifest.";
                return false;
            }

            var catalogDirectory = NormalizeDirectory(Path.GetDirectoryName(normalizedCatalog));
            var filesByPath = new Dictionary<string, AddonManifest.PlatformFile>(StringComparer.OrdinalIgnoreCase);
            var hasBundle = false;
            foreach (var file in files)
            {
                if (file == null ||
                    !AddonFilePolicy.TryNormalizePackageFilePath(file.path, out var normalizedPath))
                {
                    error = $"Map '{mapId}' platform '{platform.name}' contains an invalid file path.";
                    return false;
                }

                if (!filesByPath.TryAdd(normalizedPath, file))
                {
                    error = $"Map '{mapId}' platform '{platform.name}' declares '{normalizedPath}' more than once.";
                    return false;
                }

                if (!string.Equals(NormalizeDirectory(Path.GetDirectoryName(normalizedPath)), catalogDirectory,
                        StringComparison.OrdinalIgnoreCase))
                {
                    error =
                        $"Map '{mapId}' platform '{platform.name}' file '{normalizedPath}' is outside its platform folder.";
                    return false;
                }

                if (file.size <= 0 || !AddonFilePolicy.IsValidSha256(file.sha256))
                {
                    error = $"Map '{mapId}' platform '{platform.name}' has invalid metadata for '{normalizedPath}'.";
                    return false;
                }

                if (!AddonHelper.TryResolvePath(addonRoot, normalizedPath, out var absolutePath, out _) ||
                    !File.Exists(absolutePath))
                {
                    error = $"Map '{mapId}' platform '{platform.name}' is missing '{normalizedPath}'.";
                    return false;
                }

                var info = new FileInfo(absolutePath);
                if (info.Length != file.size)
                {
                    error = $"Map '{mapId}' platform '{platform.name}' file '{normalizedPath}' has the wrong size.";
                    return false;
                }

                if (!TryValidateFileSha256(absolutePath, file.sha256,
                        $"Map '{mapId}' platform '{platform.name}' file '{normalizedPath}'", out error))
                    return false;

                hasBundle |= string.Equals(Path.GetExtension(normalizedPath), ".bundle",
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!filesByPath.ContainsKey(normalizedCatalog))
            {
                error = $"Map '{mapId}' platform '{platform.name}' catalog is not included in its file manifest.";
                return false;
            }

            if (!hasBundle)
            {
                error = $"Map '{mapId}' platform '{platform.name}' does not contain an asset bundle.";
                return false;
            }

            return true;
        }

        private static bool TryValidateReceipt(string addonRoot, AddonManifest manifest,
            AddonManifest.ContentEntry content, out string error)
        {
            error = null;
            if (!AddonHelper.TryResolvePath(addonRoot, content.map.compiledReceipt, out var receiptPath, out _) ||
                !File.Exists(receiptPath))
            {
                error = $"Map '{content.id}' compilation receipt is missing or has an invalid path.";
                return false;
            }

            if (!MapCompilationReceiptUtility.TryLoad(receiptPath, out var receipt) || receipt == null)
            {
                error = $"Map '{content.id}' compilation receipt is invalid.";
                return false;
            }

            if (!MapCompilationReceipt.IsSupportedSchemaVersion(receipt.schemaVersion))
            {
                error =
                    $"Map '{content.id}' compilation receipt schema '{receipt.schemaVersion}' is not supported. Expected '{MapCompilationReceipt.CurrentSchemaVersion}'.";
                return false;
            }

            if (!MapCompilationReceiptUtility.HasValidSeal(receipt))
            {
                error = $"Map '{content.id}' compilation receipt failed content-digest validation.";
                return false;
            }

            if (!string.Equals(receipt.contentKind, MapCompilationReceipt.AddonContentKind,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(receipt.mapId, content.id, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(receipt.sceneAddress, content.map.scene, StringComparison.Ordinal))
            {
                error = $"Map '{content.id}' compilation receipt identity does not match the manifest.";
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
                error = $"Map '{content.id}' compilation receipt distribution metadata does not match the manifest.";
                return false;
            }

            if (string.Equals(manifest.distributionProfile, AddonDistributionPolicy.ReleaseProfile,
                    StringComparison.OrdinalIgnoreCase) && receipt.skipSpatialOctreeBake)
            {
                error = $"Map '{content.id}' release receipt cannot skip the spatial octree bake.";
                return false;
            }

            var receiptPlatforms = (receipt.platforms ?? Array.Empty<MapCompilationReceipt.PlatformArtifact>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.name))
                .ToArray();
            var manifestPlatforms = content.map.platforms ?? Array.Empty<AddonManifest.PlatformCatalog>();
            if (receiptPlatforms.Length != manifestPlatforms.Length)
            {
                error = $"Map '{content.id}' compilation receipt platform count does not match the manifest.";
                return false;
            }

            foreach (var manifestPlatform in manifestPlatforms)
            {
                var receiptPlatform = receiptPlatforms.FirstOrDefault(value =>
                    string.Equals(value.name, manifestPlatform.name, StringComparison.OrdinalIgnoreCase));
                if (receiptPlatform == null ||
                    !PathsEqual(receiptPlatform.catalog, manifestPlatform.catalog) ||
                    !string.Equals(receiptPlatform.role, manifestPlatform.role, StringComparison.OrdinalIgnoreCase) ||
                    !FilesMatch(receiptPlatform.files, manifestPlatform.files))
                {
                    error =
                        $"Map '{content.id}' compilation receipt does not match platform '{manifestPlatform.name}'.";
                    return false;
                }
            }

            return true;
        }

        private static bool FilesMatch(MapCompilationReceipt.ArtifactFile[] receiptFiles,
            AddonManifest.PlatformFile[] manifestFiles)
        {
            var receiptByPath = new Dictionary<string, MapCompilationReceipt.ArtifactFile>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var file in receiptFiles ?? Array.Empty<MapCompilationReceipt.ArtifactFile>())
                if (file == null || !AddonFilePolicy.TryNormalizePackageFilePath(file.path, out var path) ||
                    !receiptByPath.TryAdd(path, file))
                    return false;

            var manifestByPath = new Dictionary<string, AddonManifest.PlatformFile>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifestFiles ?? Array.Empty<AddonManifest.PlatformFile>())
                if (file == null || !AddonFilePolicy.TryNormalizePackageFilePath(file.path, out var path) ||
                    !manifestByPath.TryAdd(path, file))
                    return false;

            if (receiptByPath.Count != manifestByPath.Count)
                return false;

            foreach (var pair in manifestByPath)
                if (!receiptByPath.TryGetValue(pair.Key, out var receiptFile) ||
                    receiptFile.size != pair.Value.size ||
                    !string.Equals(receiptFile.sha256, pair.Value.sha256, StringComparison.OrdinalIgnoreCase))
                    return false;

            return true;
        }

        private static bool TryValidateOptionalFile(string addonRoot, string relativePath, string label,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(relativePath))
                return true;

            if (!AddonHelper.TryResolvePath(addonRoot, relativePath, out var fullPath, out _) ||
                !File.Exists(fullPath))
            {
                error = $"{label} is missing or has an invalid path.";
                return false;
            }

            return true;
        }

        private static bool TryValidateFileSha256(string absolutePath, string expectedSha256,
            string label, out string error)
        {
            error = null;
            var before = new FileInfo(absolutePath);
            if (!before.Exists)
            {
                error = $"{label} is missing.";
                return false;
            }

            var lengthBefore = before.Length;
            var writeTimeBefore = before.LastWriteTimeUtc;
            if (!SdkFileHashUtility.TryComputeSha256(absolutePath, out var actualSha256))
            {
                error = $"{label} could not be read for SHA-256 validation. " +
                        "Wait for any build, copy, or sync operation using the package to finish, then retry.";
                return false;
            }

            var after = new FileInfo(absolutePath);
            if (!after.Exists || after.Length != lengthBefore || after.LastWriteTimeUtc != writeTimeBefore)
            {
                error = $"{label} changed while its SHA-256 was being validated. " +
                        "Wait for the package build, copy, or sync operation to finish, then retry.";
                return false;
            }

            if (string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                return true;

            error = $"{label} does not match the SHA-256 recorded in addon.json. " +
                    "Rebuild the add-on package in the Vellocet SDK; the generated file and addon.json " +
                    "must come from the same completed build.";
            return false;
        }

        private static bool PathsEqual(string a, string b)
        {
            return AddonFilePolicy.TryNormalizePackageFilePath(a, out var canonicalA) &&
                   AddonFilePolicy.TryNormalizePackageFilePath(b, out var canonicalB) &&
                   string.Equals(a, canonicalA, StringComparison.Ordinal) &&
                   string.Equals(b, canonicalB, StringComparison.Ordinal) &&
                   string.Equals(canonicalA, canonicalB, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectory(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static bool IsInDirectory(string path, string directory)
        {
            var normalizedDirectory = NormalizeDirectory(directory);
            return string.IsNullOrEmpty(normalizedDirectory) ||
                   string.Equals(path, normalizedDirectory, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(normalizedDirectory + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetPackageRelativePath(string root, string path)
        {
            var relativePath = Path.GetRelativePath(root, path);
            return Path.DirectorySeparatorChar == '\\'
                ? relativePath.Replace('\\', '/')
                : relativePath;
        }

        private static bool IsReparsePoint(FileAttributes attributes)
        {
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
    }
}
