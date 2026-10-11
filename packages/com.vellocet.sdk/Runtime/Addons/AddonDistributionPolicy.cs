// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Vellocet.Sdk.Addons
{
    /// <summary>
    ///     Stable distribution metadata shared by exporters, validators, uploaders, and runtime consumers.
    ///     Policy ids are versioned so future release matrices do not silently change existing packages.
    /// </summary>
    public static class AddonDistributionPolicy
    {
        public const string DevelopmentProfile = "development";
        public const string ReleaseProfile = "release";
        public const string ReleasePolicyV1 = "release-v1";

        public const string ClientRole = "client";
        public const string ServerRole = "server";

        public const string WindowsClientPlatform = "StandaloneWindows64";
        public const string MacOSClientPlatform = "StandaloneOSX";
        public const string LinuxServerPlatform = "StandaloneLinux64";

        private static readonly ReadOnlyCollection<string> ReleasePlatformNamesValue =
            Array.AsReadOnly(new[]
            {
                WindowsClientPlatform,
                MacOSClientPlatform,
                LinuxServerPlatform
            });

        public static IReadOnlyList<string> ReleasePlatformNames => ReleasePlatformNamesValue;

        public static bool IsKnownProfile(string profile)
        {
            return string.Equals(profile, DevelopmentProfile, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile, ReleaseProfile, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRelease(string profile)
        {
            return string.Equals(profile, ReleaseProfile, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeProfile(string profile)
        {
            if (string.Equals(profile, ReleaseProfile, StringComparison.OrdinalIgnoreCase))
                return ReleaseProfile;
            if (string.Equals(profile, DevelopmentProfile, StringComparison.OrdinalIgnoreCase))
                return DevelopmentProfile;
            return string.IsNullOrWhiteSpace(profile) ? string.Empty : profile.Trim().ToLowerInvariant();
        }

        public static bool TryGetExpectedRole(string platformName, out string role)
        {
            role = null;
            if (string.Equals(platformName, WindowsClientPlatform, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(platformName, MacOSClientPlatform, StringComparison.OrdinalIgnoreCase))
            {
                role = ClientRole;
                return true;
            }

            if (string.Equals(platformName, LinuxServerPlatform, StringComparison.OrdinalIgnoreCase))
            {
                role = ServerRole;
                return true;
            }

            return false;
        }

        public static bool TryValidateManifestMetadata(AddonManifest manifest, bool requireRelease,
            out string error)
        {
            error = null;
            if (manifest == null)
            {
                error = "Addon manifest is missing.";
                return false;
            }

            if (manifest.schemaVersion != AddonManifest.CurrentSchemaVersion)
            {
                error =
                    $"Addon schema '{manifest.schemaVersion}' is not supported. Expected '{AddonManifest.CurrentSchemaVersion}'. Rebuild the package with this SDK version.";
                return false;
            }

            var profile = NormalizeProfile(manifest.distributionProfile);
            if (!IsKnownProfile(profile))
            {
                error = $"Addon distribution profile '{manifest.distributionProfile}' is not supported.";
                return false;
            }

            if (!Guid.TryParseExact(manifest.buildId, "N", out _))
            {
                error = "Addon manifest buildId is missing or invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.sdkVersion))
            {
                error = "Addon manifest sdkVersion is missing.";
                return false;
            }

            if (!IsRelease(profile))
            {
                if (requireRelease)
                {
                    error = "This is a development package. Build a Release Package for public distribution.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(manifest.distributionPolicy))
                {
                    error = "Development packages must not declare a release distribution policy.";
                    return false;
                }

                return true;
            }

            if (!string.Equals(manifest.distributionPolicy, ReleasePolicyV1, StringComparison.Ordinal))
            {
                error = $"Release package policy '{manifest.distributionPolicy}' is not supported.";
                return false;
            }

            if (string.Equals(manifest.sdkVersion, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                error =
                    "Release package SDK version could not be resolved. Reinstall or repair the SDK before exporting.";
                return false;
            }

            foreach (var content in manifest.contents ?? Array.Empty<AddonManifest.ContentEntry>())
            {
                if (content == null)
                    continue;

                if (string.Equals(content.kind, AddonManifest.MapContentKind, StringComparison.OrdinalIgnoreCase))
                {
                    if (content.map == null)
                    {
                        error = $"Release map '{content.id}' is missing its map payload.";
                        return false;
                    }

                    if (!TryValidateReleasePlatforms(content.map.platforms, out var platformError))
                    {
                        error = $"Release map '{content.id}': {platformError}";
                        return false;
                    }
                }
                else if (string.Equals(content.kind, AddonManifest.AudioContentKind,
                             StringComparison.OrdinalIgnoreCase) &&
                         !TryValidateReleaseAudioContent(content.audio, out var audioError))
                {
                    error = $"Release audio '{content.id}': {audioError}";
                    return false;
                }
            }

            return true;
        }

        public static bool TryValidateReleaseAudioContent(AddonManifest.AudioContent audio,
            out string error)
        {
            error = null;
            if (audio == null || string.IsNullOrWhiteSpace(audio.catalog))
            {
                error = "the platform-neutral audio catalog is missing.";
                return false;
            }

            if (audio.files == null || audio.files.Length < 2)
            {
                error = "the source-audio file manifest is incomplete.";
                return false;
            }

            return true;
        }

        public static bool TryValidateReleasePlatforms(AddonManifest.PlatformCatalog[] platforms, out string error)
        {
            error = null;
            if (platforms == null || platforms.Length == 0)
            {
                error = "no platform artifacts were declared.";
                return false;
            }

            var byName = new Dictionary<string, AddonManifest.PlatformCatalog>(StringComparer.OrdinalIgnoreCase);
            foreach (var platform in platforms)
            {
                if (platform == null || string.IsNullOrWhiteSpace(platform.name))
                {
                    error = "a platform declaration is incomplete.";
                    return false;
                }

                if (!byName.TryAdd(platform.name.Trim(), platform))
                {
                    error = $"platform '{platform.name}' is declared more than once.";
                    return false;
                }
            }

            if (byName.Count != ReleasePlatformNamesValue.Count)
            {
                error =
                    "the release platform matrix must contain exactly Windows Client, macOS Client, and Linux Dedicated Server.";
                return false;
            }

            foreach (var requiredName in ReleasePlatformNamesValue)
            {
                if (!byName.TryGetValue(requiredName, out var platform))
                {
                    error = $"required platform '{requiredName}' is missing.";
                    return false;
                }

                TryGetExpectedRole(requiredName, out var expectedRole);
                if (!string.Equals(platform.role, expectedRole, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"platform '{requiredName}' must use the '{expectedRole}' role.";
                    return false;
                }
            }

            return true;
        }
    }
}