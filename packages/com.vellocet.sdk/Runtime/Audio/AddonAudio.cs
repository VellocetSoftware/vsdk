// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;

namespace Vellocet.Sdk.Audio
{
    /// <summary>The host-authored playback template used to play add-on-owned media.</summary>
    public enum AddonAudioTemplate
    {
        Announcer = 0,
        Sfx2D = 1,
        Sfx3D = 2,
        AmbientLoop = 3,
        MusicLoop = 4,
        Ambience2DLoop = 5
    }

    /// <summary>Defines which add-on owns the media behind an authored cue reference.</summary>
    public enum AddonAudioCueOrigin
    {
        /// <summary>The current add-on owns and ships the selected source audio.</summary>
        Embedded = 0,

        /// <summary>Another add-on owns the media; the current add-on declares only a dependency.</summary>
        AddonDependency = 1
    }

    /// <summary>Platform-neutral catalog written beside add-on-owned source audio.</summary>
    [Serializable]
    public sealed class AddonAudioCatalog
    {
        public const int CurrentSchemaVersion = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public Cue[] cues = Array.Empty<Cue>();
        public string audioRuntime;
        public string bank;

        [Serializable]
        public sealed class Cue
        {
            public string id;
            public string media;
            public string eventId;
            public double duration;
            public string template;
            public float gain = 1f;
        }
    }

    public static class AddonAudioPolicy
    {
        public const int MaximumCueCount = 256;
        public const long MaximumSourceFileBytes = 32L * 1024L * 1024L;
        public const long MaximumDecodedBytesPerContent = 256L * 1024L * 1024L;

        public const string AnnouncerTemplate = "announcer";
        public const string Sfx2DTemplate = "sfx2d";
        public const string Sfx3DTemplate = "sfx3d";
        public const string AmbientLoopTemplate = "ambientLoop";
        public const string MusicLoopTemplate = "musicLoop";
        public const string Ambience2DLoopTemplate = "ambience2dLoop";

        public static bool IsSupportedSchemaVersion(int schemaVersion)
        {
            return schemaVersion == AddonAudioCatalog.CurrentSchemaVersion;
        }

        public static bool IsTemplateSupported(int schemaVersion, AddonAudioTemplate template)
        {
            return IsSupportedSchemaVersion(schemaVersion) && Enum.IsDefined(typeof(AddonAudioTemplate), template);
        }

        public static bool TryNormalizeCueId(string addonId, string cueId, out string normalized)
        {
            normalized = null;
            var owner = AddonIdPolicy.NormalizeOrEmpty(addonId);
            var candidate = cueId?.Trim().ToLowerInvariant();
            if (Vertex.Contracts.AssetCatalog.TryParseKey(candidate, out var contentOwner, out _) && contentOwner == owner)
            { normalized = candidate; return true; }
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(candidate) ||
                candidate.Length > 160 || !candidate.StartsWith(owner + ".", StringComparison.Ordinal))
                return false;

            var segments = candidate.Split('.');
            if (segments.Length < 3)
                return false;
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                    return false;
                for (var i = 0; i < segment.Length; i++)
                {
                    var value = segment[i];
                    if (!char.IsLetterOrDigit(value) && value != '_' && value != '-')
                        return false;
                }
            }

            normalized = candidate;
            return true;
        }

        public static bool TryParseTemplate(string value, out AddonAudioTemplate template)
        {
            switch (value?.Trim())
            {
                case AnnouncerTemplate:
                    template = AddonAudioTemplate.Announcer;
                    return true;
                case Sfx2DTemplate:
                    template = AddonAudioTemplate.Sfx2D;
                    return true;
                case Sfx3DTemplate:
                    template = AddonAudioTemplate.Sfx3D;
                    return true;
                case AmbientLoopTemplate:
                    template = AddonAudioTemplate.AmbientLoop;
                    return true;
                case MusicLoopTemplate:
                    template = AddonAudioTemplate.MusicLoop;
                    return true;
                case Ambience2DLoopTemplate:
                    template = AddonAudioTemplate.Ambience2DLoop;
                    return true;
                default:
                    template = default;
                    return false;
            }
        }

        public static string ToCatalogValue(AddonAudioTemplate template)
        {
            return template switch
            {
                AddonAudioTemplate.Announcer => AnnouncerTemplate,
                AddonAudioTemplate.Sfx2D => Sfx2DTemplate,
                AddonAudioTemplate.Sfx3D => Sfx3DTemplate,
                AddonAudioTemplate.AmbientLoop => AmbientLoopTemplate,
                AddonAudioTemplate.MusicLoop => MusicLoopTemplate,
                AddonAudioTemplate.Ambience2DLoop => Ambience2DLoopTemplate,
                _ => string.Empty
            };
        }

        public static bool TryNormalizeMediaPath(string value, out string normalized)
        {
            normalized = value?.Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 240 ||
                normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.Contains(":", StringComparison.Ordinal) ||
                !IsSupportedMediaExtension(Path.GetExtension(normalized)))
            {
                normalized = null;
                return false;
            }

            foreach (var segment in normalized.Split('/'))
                if (string.IsNullOrWhiteSpace(segment) || segment is "." or "..")
                {
                    normalized = null;
                    return false;
                }

            return true;
        }

        public static bool IsSupportedMediaExtension(string extension)
        {
            return string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLoop(AddonAudioTemplate template)
        {
            return template is AddonAudioTemplate.AmbientLoop or AddonAudioTemplate.MusicLoop or
                AddonAudioTemplate.Ambience2DLoop;
        }

        public static bool IsSpatial(AddonAudioTemplate template)
        {
            return template is AddonAudioTemplate.Sfx3D or AddonAudioTemplate.AmbientLoop;
        }

        public static bool TryResolveGain(int schemaVersion, float value, out float gain)
        {
            if (!IsSupportedSchemaVersion(schemaVersion))
            {
                gain = 0f;
                return false;
            }

            if (!float.IsFinite(value) || value < 0f || value > 1f)
            {
                gain = 0f;
                return false;
            }

            gain = value;
            return true;
        }
    }
}
