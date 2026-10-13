// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Audio;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor.Audio
{
    /// <summary>
    ///     Copies author-owned WAV, Ogg Vorbis, or MP3 files into an addon and writes the platform-neutral
    ///     cue catalog consumed by the game-side Wwise Audio Input bridge.
    /// </summary>
    internal static class AddonAudioBuildPipeline
    {
        internal static bool TryBuild(AddonMapBuildRequest request, AddonMapArtifactLayout layout,
            IReadOnlyList<BuildTarget> successfulTargets, out AddonManifest.ContentEntry audioContent,
            out string[] requiredAddonIds, out string error)
        {
            audioContent = null;
            requiredAddonIds = Array.Empty<string>();
            error = null;
            var scenePath = string.IsNullOrWhiteSpace(request.ScenePath)
                ? AssetDatabase.GetAssetPath(request.SceneAsset)
                : request.ScenePath;
            var cues = AssetDatabase.GetDependencies(scenePath, true)
                .Where(path => !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AddonAudioCue>())
                .Where(value => value)
                .OrderBy(value => value.CueId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (cues.Length == 0)
                return TryRemoveGeneratedMapAudio(layout.AddonRoot, request.MapId, out error);

            if (!TryResolveMapCues(request.AddonId, cues, out var embedded, out requiredAddonIds, out error))
                return false;
            // Vertex's content catalog packages these source files once for maps and scripts together.
            embedded = embedded.Where(cue => !cue.CueId.StartsWith("asset:", StringComparison.Ordinal)).ToArray();
            if (embedded.Length == 0)
                return TryRemoveGeneratedMapAudio(layout.AddonRoot, request.MapId, out error);

            return TryBuildCues(request.AddonId, ContentIdUtility.Sanitize(request.MapId + "_audio"),
                request.MapTitle + " Audio", layout.AddonRoot, embedded, successfulTargets, out audioContent,
                out error);
        }

        internal static bool TryRemoveGeneratedMapAudio(string addonRoot, string mapId, out string error)
        {
            error = null;
            var contentId = ContentIdUtility.Sanitize(mapId + "_audio");
            if (string.IsNullOrWhiteSpace(addonRoot) || string.IsNullOrWhiteSpace(contentId))
            {
                error = "Add-on output folder and map ID are required to clean generated audio.";
                return false;
            }

            try
            {
                var audioRoot = Path.GetFullPath(Path.Combine(addonRoot, "audio"));
                var contentDirectory = Path.GetFullPath(Path.Combine(audioRoot, contentId));
                var rootPrefix = audioRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                 Path.DirectorySeparatorChar;
                if (!contentDirectory.StartsWith(rootPrefix, StringComparison.Ordinal))
                {
                    error = "Generated map-audio cleanup resolved outside the add-on audio folder.";
                    return false;
                }

                if (Directory.Exists(contentDirectory))
                    Directory.Delete(contentDirectory, true);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Could not remove stale generated map audio: {ex.Message}";
                return false;
            }
        }

        internal static bool TryResolveMapCues(string addonId, IReadOnlyList<AddonAudioCue> sourceCues,
            out AddonAudioCue[] embeddedCues, out string[] requiredAddonIds, out string error)
        {
            embeddedCues = Array.Empty<AddonAudioCue>();
            requiredAddonIds = Array.Empty<string>();
            error = null;
            var cues = (sourceCues ?? Array.Empty<AddonAudioCue>())
                .Where(value => value)
                .OrderBy(value => value.CueId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var duplicates = cues
                .GroupBy(value => value.CueId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicates != null)
            {
                error = $"Multiple AddonAudioCue assets use cue ID '{duplicates.Key}'. Cue IDs must be unique.";
                return false;
            }

            var embedded = new List<AddonAudioCue>(cues.Length);
            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ownerAddonId = AddonIdPolicy.NormalizeOrEmpty(addonId);
            foreach (var cue in cues)
            {
                if (!cue.IsDependencyReference)
                {
                    embedded.Add(cue);
                    continue;
                }

                var dependencyAddonId = cue.DependencyAddonId;
                if (string.IsNullOrWhiteSpace(dependencyAddonId))
                {
                    error = $"Audio cue '{cue.name}' must specify the add-on that owns its media.";
                    return false;
                }

                if (string.Equals(dependencyAddonId, ownerAddonId, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Audio cue '{cue.CueId}' names the current add-on as a dependency. " +
                            "Use Embedded when this add-on owns the media.";
                    return false;
                }

                if (!AddonAudioPolicy.TryNormalizeCueId(dependencyAddonId, cue.CueId, out _))
                {
                    error = $"Audio cue '{cue.CueId}' must be owned by dependency add-on " +
                            $"'{dependencyAddonId}'.";
                    return false;
                }

                dependencies.Add(dependencyAddonId);
            }

            embeddedCues = embedded.ToArray();
            requiredAddonIds = dependencies.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
            return true;
        }

        internal static bool TryBuildCues(string addonId, string contentId, string title, string addonRoot,
            IReadOnlyList<AddonAudioCue> sourceCues, IReadOnlyList<BuildTarget> successfulTargets,
            out AddonManifest.ContentEntry audioContent, out string error)
        {
            _ = successfulTargets; // Source audio and its Wwise Audio Input contract are platform-neutral.
            audioContent = null;
            error = null;
            addonId = AddonIdPolicy.NormalizeOrEmpty(addonId);
            contentId = ContentIdUtility.Sanitize(contentId);
            var cues = (sourceCues ?? Array.Empty<AddonAudioCue>())
                .Where(value => value)
                .GroupBy(value => value.CueId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(value => value.CueId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (string.IsNullOrWhiteSpace(addonId) || string.IsNullOrWhiteSpace(contentId) ||
                string.IsNullOrWhiteSpace(addonRoot) || cues.Length == 0)
            {
                error = "Add-on ID, audio content ID, output folder, and at least one cue are required.";
                return false;
            }

            if (cues.Length > AddonAudioPolicy.MaximumCueCount)
            {
                error = $"Audio content exceeds the {AddonAudioPolicy.MaximumCueCount} cue limit.";
                return false;
            }

            var sources = new List<SourceRecord>(cues.Length);
            for (var index = 0; index < cues.Length; index++)
            {
                var cue = cues[index];
                if (cue.IsDependencyReference)
                {
                    error = $"Audio cue '{cue.CueId}' is a dependency reference and cannot be embedded in " +
                            "an AddonAudioCollection.";
                    return false;
                }

                if (!AddonAudioPolicy.TryNormalizeCueId(addonId, cue.CueId, out _))
                {
                    error = $"Audio cue '{cue.name}' must use an ID owned by add-on '{addonId}'.";
                    return false;
                }

                var sourcePath = AssetDatabase.GUIDToAssetPath(cue.SourceAssetGuid);
                var extension = Path.GetExtension(sourcePath)?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) ||
                    !AddonAudioPolicy.IsSupportedMediaExtension(extension))
                {
                    error = $"Audio cue '{cue.CueId}' must reference a WAV, Ogg Vorbis, or MP3 source.";
                    return false;
                }

                var sourceInfo = new FileInfo(sourcePath);
                if (sourceInfo.Length <= 0 || sourceInfo.Length > AddonAudioPolicy.MaximumSourceFileBytes)
                {
                    error =
                        $"Audio cue '{cue.CueId}' exceeds the {AddonAudioPolicy.MaximumSourceFileBytes} byte source limit.";
                    return false;
                }

                var mediaName = BuildMediaBaseName(cue.CueId, index) + extension;
                sources.Add(new SourceRecord(cue, sourcePath, "sounds/" + mediaName));
            }

            if (SdkGame.Current.AudioRuntimeCapability == AddonAudioBanks.Capability)
            {
                try { audioContent = StereoformBankBuild.Build(addonId, contentId, title, addonRoot, cues); return true; }
                catch (Exception ex) { error = "Stereoform bank compilation failed: " + ex.Message; return false; }
            }

            try
            {
                var contentDirectory = Path.Combine(addonRoot, "audio", contentId);
                if (Directory.Exists(contentDirectory))
                    Directory.Delete(contentDirectory, true);
                var soundsDirectory = Path.Combine(contentDirectory, "sounds");
                Directory.CreateDirectory(soundsDirectory);

                foreach (var source in sources)
                    File.Copy(source.SourcePath,
                        Path.Combine(contentDirectory,
                            source.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar)), true);

                var catalog = new AddonAudioCatalog
                {
                    schemaVersion = AddonAudioCatalog.CurrentSchemaVersion,
                    cues = sources.Select(source => new AddonAudioCatalog.Cue
                    {
                        id = source.Cue.CueId,
                        media = source.MediaRelativePath,
                        template = AddonAudioPolicy.ToCatalogValue(source.Cue.Template),
                        gain = source.Cue.Gain
                    }).ToArray()
                };
                var catalogPath = Path.Combine(contentDirectory, "audio.json");
                File.WriteAllText(catalogPath, JsonUtility.ToJson(catalog, true), new UTF8Encoding(false));

                audioContent = new AddonManifest.ContentEntry
                {
                    kind = AddonManifest.AudioContentKind,
                    id = contentId,
                    title = string.IsNullOrWhiteSpace(title) ? contentId : title.Trim(),
                    audio = new AddonManifest.AudioContent
                    {
                        catalog = MakeRelative(addonRoot, catalogPath),
                        files = BuildFiles(addonRoot, contentDirectory)
                    }
                };
                return true;
            }
            catch (Exception ex)
            {
                error = $"Add-on source-audio build failed: {ex.Message}";
                return false;
            }
        }

        private static AddonManifest.PlatformFile[] BuildFiles(string addonRoot, string directory)
        {
            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Where(value => !value.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Select(value =>
                {
                    var info = new FileInfo(value);
                    SdkFileHashUtility.TryComputeSha256(value, out var hash);
                    return new AddonManifest.PlatformFile
                    {
                        path = MakeRelative(addonRoot, value),
                        size = info.Length,
                        sha256 = hash
                    };
                }).ToArray();
        }

        private static string MakeRelative(string root, string path)
        {
            return Path.GetRelativePath(root, path).Replace('\\', '/');
        }

        private static string BuildMediaBaseName(string cueId, int index)
        {
            var leaf = cueId?.Split('.').LastOrDefault() ?? "cue";
            var safe = new string(leaf.Select(value => char.IsLetterOrDigit(value) || value is '_' or '-'
                ? char.ToLowerInvariant(value)
                : '_').ToArray()).Trim('_');
            return $"{index:D3}_{(string.IsNullOrWhiteSpace(safe) ? "cue" : safe)}";
        }

        private sealed class SourceRecord
        {
            public SourceRecord(AddonAudioCue cue, string sourcePath, string mediaRelativePath)
            {
                Cue = cue;
                SourcePath = sourcePath;
                MediaRelativePath = mediaRelativePath;
            }

            public AddonAudioCue Cue { get; }
            public string SourcePath { get; }
            public string MediaRelativePath { get; }
        }
    }
}
#endif
