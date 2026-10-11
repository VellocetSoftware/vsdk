// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk
{
    public static class SharedAssetAddressUtility
    {
        private const string SdkRootPrefix = "SDK";
        private const string PackagePrefix = "package";

        public static bool IsSdkSharedAssetPath(string assetPath)
        {
            return TryGetCanonicalSdkPath(assetPath, out _);
        }

        public static string CreateAddress(string assetPath)
        {
            var normalized = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            if (normalized.StartsWith(Constants.PackageAssetsPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                var sdkRelative = normalized[(Constants.PackageAssetsPath.Length + 1)..];
                return CombineAddress(SdkRootPrefix, sdkRelative);
            }

            if (TryGetCanonicalSdkPath(normalized, out var canonicalSdkPath))
            {
                var projectRelative = canonicalSdkPath["Assets/".Length..];
                return CombineAddress(SdkRootPrefix, projectRelative);
            }

            if (!normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                return null;

            var parts = normalized.Split('/');
            if (parts.Length < 3)
                return null;

            var packageId = SanitizeSegment(parts[1]);
            var packageRelative = string.Join("/", parts, 2, parts.Length - 2);
            return CombineAddress(SdkRootPrefix, PackagePrefix, packageId, packageRelative);
        }

        private static string CombineAddress(params string[] parts)
        {
            var cleaned = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                var normalized = NormalizeAssetPath(part).Trim('/');
                if (!string.IsNullOrWhiteSpace(normalized))
                    cleaned.Add(normalized);
            }

            return cleaned.Count == 0 ? null : string.Join("/", cleaned);
        }

        private static string SanitizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            return value.Trim().Replace('\\', '/').Replace(' ', '-').ToLowerInvariant();
        }

        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim();
        }

        private static bool TryGetCanonicalSdkPath(string assetPath, out string canonicalPath)
        {
            canonicalPath = string.Empty;
            var normalized = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            if (SdkContentPathUtility.IsSourceSdkAssetPath(normalized))
            {
                canonicalPath = normalized;
                return true;
            }

            return SdkContentPathUtility.TryMapInstalledToSourcePath(normalized, out canonicalPath);
        }
    }
}