// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using UnityEditor.PackageManager;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Editor.Maps;

namespace Vellocet.Sdk.Editor.Addons
{
    /// <summary>Keeps exporter-owned manifest fields identical across every SDK build path.</summary>
    internal static class AddonManifestBuildUtility
    {
        internal static string CreateBuildId()
        {
            return Guid.NewGuid().ToString("N");
        }

        internal static void ApplyGeneratedMetadata(
            AddonManifest manifest,
            AddonMapBuildProfile profile,
            string buildId,
            string sdkVersion = null,
            DateTime? createdUtc = null)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (!Enum.IsDefined(typeof(AddonMapBuildProfile), profile))
                throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown add-on build profile.");
            if (!Guid.TryParseExact(buildId, "N", out _))
                throw new ArgumentException("Build ID must be a GUID in N format.", nameof(buildId));

            var timestamp = (createdUtc ?? DateTime.UtcNow).ToUniversalTime();
            manifest.schemaVersion = AddonManifest.CurrentSchemaVersion;
            manifest.gameId = SdkGame.Current.Id;
            manifest.distributionProfile = AddonMapBuildPolicy.DistributionProfile(profile);
            manifest.distributionPolicy = AddonMapBuildPolicy.DistributionPolicy(profile);
            manifest.buildId = buildId;
            manifest.createdUtc = timestamp.ToString("o");
            manifest.sdkVersion = string.IsNullOrWhiteSpace(sdkVersion) ? ResolveSdkVersion() : sdkVersion.Trim();
            manifest.unityVersion = Application.unityVersion;
        }

        internal static string ResolveSdkVersion()
        {
            var package = PackageInfo.FindForAssembly(typeof(AddonManifestBuildUtility).Assembly);
            return string.IsNullOrWhiteSpace(package?.version) ? "unknown" : package.version;
        }
    }
}
#endif
