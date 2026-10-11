// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Audio;
using Vellocet.Sdk.Editor.Addons;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Audio
{
    internal sealed class AddonAudioCollectionExporter : IAddonContentSourceExporter
    {
        public Type SourceType => typeof(AddonAudioCollection);
        public string ContentKind => AddonManifest.AudioContentKind;
        public string DisplayName => "Audio collection";

        public bool TryValidate(Object source, AddonContentExportContext context, out string error)
        {
            error = null;
            if (source is not AddonAudioCollection collection)
            {
                error = "Audio collection source is missing.";
                return false;
            }

            if (context?.Project == null || string.IsNullOrWhiteSpace(context.AddonId))
            {
                error = "Add-on Project identity is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(collection.AddonId) &&
                !string.Equals(collection.AddonId, context.AddonId, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Audio collection '{collection.name}' belongs to '{collection.AddonId}', not " +
                        $"'{context.AddonId}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(collection.ResolveContentId(context.AddonId)))
            {
                error = $"Audio collection '{collection.name}' has an invalid content ID.";
                return false;
            }

            var cues = collection.Cues;
            if (cues.Length == 0)
            {
                error = $"Audio collection '{collection.name}' needs at least one cue.";
                return false;
            }

            if (cues.Length > AddonAudioPolicy.MaximumCueCount)
            {
                error = $"Audio collection '{collection.name}' exceeds the " +
                        $"{AddonAudioPolicy.MaximumCueCount} cue limit.";
                return false;
            }

            var duplicate = cues.Where(value => value)
                .GroupBy(value => value.CueId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                error = $"Audio cue ID '{duplicate.Key}' appears more than once in '{collection.name}'.";
                return false;
            }

            for (var index = 0; index < cues.Length; index++)
            {
                var cue = cues[index];
                if (!cue)
                {
                    error = $"Audio collection '{collection.name}' has an empty cue at position {index + 1}.";
                    return false;
                }

                if (cue.IsDependencyReference)
                {
                    error = $"Audio cue '{cue.CueId}' is a dependency reference. Collections can only ship " +
                            "audio owned by this add-on.";
                    return false;
                }

                if (!AddonAudioPolicy.TryNormalizeCueId(context.AddonId, cue.CueId, out _))
                {
                    error = $"Audio cue '{cue.name}' needs an ID owned by '{context.AddonId}'.";
                    return false;
                }

                var sourcePath = AssetDatabase.GUIDToAssetPath(cue.SourceAssetGuid);
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) ||
                    !AddonAudioPolicy.IsSupportedMediaExtension(Path.GetExtension(sourcePath)))
                {
                    error = $"Audio cue '{cue.CueId}' needs a WAV, Ogg Vorbis, or MP3 source.";
                    return false;
                }

                var file = new FileInfo(sourcePath);
                if (file.Length <= 0 || file.Length > AddonAudioPolicy.MaximumSourceFileBytes)
                {
                    error = $"Audio cue '{cue.CueId}' has an empty or oversized source file.";
                    return false;
                }
            }

            return true;
        }

        public bool TryExport(Object source, AddonContentExportContext context,
            out AddonManifest.ContentEntry content, out string[] requiredAddonIds, out string error)
        {
            content = null;
            requiredAddonIds = Array.Empty<string>();
            if (!TryValidate(source, context, out error))
                return false;

            var collection = (AddonAudioCollection)source;
            return AddonAudioBuildPipeline.TryBuildCues(context.AddonId,
                collection.ResolveContentId(context.AddonId), collection.Title, context.AddonRoot,
                collection.Cues, Array.Empty<BuildTarget>(), out content, out error);
        }
    }
}
#endif