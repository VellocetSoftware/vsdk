// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using Newtonsoft.Json;

namespace Vellocet.Sdk.Addons
{
    public static class AddonHelper
    {
        public static string GetManifestPath(string addonRoot)
        {
            return string.IsNullOrWhiteSpace(addonRoot)
                ? null
                : Path.Combine(addonRoot, Constants.AddonManifestFileName);
        }

        public static void WriteManifest(string addonRoot, AddonManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(addonRoot))
                throw new ArgumentException("Addon root is required.", nameof(addonRoot));
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            Directory.CreateDirectory(addonRoot);
            var manifestPath = GetManifestPath(addonRoot);
            var json = JsonConvert.SerializeObject(manifest, Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            File.WriteAllText(manifestPath, json + Environment.NewLine);
        }

        public static bool TryLoad(string addonRoot, out AddonManifest manifest, out string error)
        {
            return TryLoad(addonRoot, true, out manifest, out error);
        }

        internal static bool TryLoadForValidation(string addonRoot, out AddonManifest manifest,
            out string error)
        {
            return TryLoad(addonRoot, false, out manifest, out error);
        }

        private static bool TryLoad(string addonRoot, bool validateDistributionMetadata,
            out AddonManifest manifest, out string error)
        {
            manifest = null;
            error = null;
            var manifestPath = GetManifestPath(addonRoot);
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                error = "Addon root is missing.";
                return false;
            }

            if (!File.Exists(manifestPath))
            {
                error = $"Addon manifest '{Constants.AddonManifestFileName}' is missing.";
                return false;
            }

            try
            {
                var text = File.ReadAllText(manifestPath);
                if (string.IsNullOrWhiteSpace(text))
                {
                    error = $"Addon manifest '{Constants.AddonManifestFileName}' is empty.";
                    return false;
                }

                manifest = JsonConvert.DeserializeObject<AddonManifest>(text);
                if (manifest == null)
                {
                    error = $"Addon manifest '{Constants.AddonManifestFileName}' is invalid JSON.";
                    return false;
                }

                if (manifest.schemaVersion != AddonManifest.CurrentSchemaVersion)
                {
                    error =
                        $"Unsupported addon schema version '{manifest.schemaVersion}'. Expected " +
                        $"'{AddonManifest.CurrentSchemaVersion}'. Rebuild the addon with this SDK version.";
                    manifest = null;
                    return false;
                }

                var declaredAddonId = manifest.id;
                if (!AddonIdPolicy.TryNormalize(declaredAddonId, out var canonicalAddonId) ||
                    !string.Equals(declaredAddonId, canonicalAddonId, StringComparison.Ordinal))
                {
                    error = "Invalid addon id. Use its canonical lowercase ASCII form, such as 'author.package'.";
                    manifest = null;
                    return false;
                }
                manifest.id = canonicalAddonId;

                manifest.requires ??= Array.Empty<string>();
                for (var i = 0; i < manifest.requires.Length; i++)
                {
                    var declaredDependency = manifest.requires[i];
                    if (!AddonIdPolicy.TryNormalize(declaredDependency, out var dependency) ||
                        !string.Equals(declaredDependency, dependency, StringComparison.Ordinal) ||
                        string.Equals(dependency, manifest.id, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"Invalid addon dependency '{manifest.requires[i]}'.";
                        manifest = null;
                        return false;
                    }

                    manifest.requires[i] = dependency;
                }

                if (manifest.contents != null)
                    foreach (var content in manifest.contents)
                    {
                        if (content == null)
                            continue;

                        var declaredContentId = content.id;
                        string canonicalContentId;
                        var isCanonical = string.Equals(content.kind, AddonManifest.ModContentKind,
                            StringComparison.OrdinalIgnoreCase)
                            ? AddonIdPolicy.TryNormalize(declaredContentId, out canonicalContentId) &&
                              string.Equals(declaredContentId, canonicalContentId, StringComparison.Ordinal)
                            : AddonFilePolicy.TryGetCanonicalContentId(declaredContentId,
                                out canonicalContentId);
                        if (!isCanonical)
                        {
                            error = $"Invalid content id '{declaredContentId}'. Use its canonical lowercase ASCII form.";
                            manifest = null;
                            return false;
                        }

                        content.id = canonicalContentId;
                    }

                if (validateDistributionMetadata &&
                    !AddonDistributionPolicy.TryValidateManifestMetadata(manifest, false,
                        out var metadataError))
                {
                    error = $"Invalid addon distribution metadata: {metadataError}";
                    manifest = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                manifest = null;
                error = $"Failed to read addon manifest: {ex.Message}";
                return false;
            }
        }

        public static string ResolvePath(string addonRoot, string relativePath)
        {
            return TryResolvePath(addonRoot, relativePath, out var fullPath, out _)
                ? fullPath
                : null;
        }

        public static bool TryResolvePath(string addonRoot, string relativePath, out string fullPath,
            out string normalizedRelativePath)
        {
            fullPath = null;
            normalizedRelativePath = null;
            if (string.IsNullOrWhiteSpace(addonRoot) ||
                !AddonFilePolicy.TryNormalizeSafeRelativePath(relativePath, out normalizedRelativePath))
                return false;

            try
            {
                var rootFull = Path.GetFullPath(addonRoot);
                var combined = Path.GetFullPath(Path.Combine(rootFull,
                    normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsUnderRoot(rootFull, combined))
                {
                    normalizedRelativePath = null;
                    return false;
                }

                fullPath = combined;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                normalizedRelativePath = null;
                return false;
            }
        }

        private static bool IsUnderRoot(string root, string candidate)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate))
                return false;

            var normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedCandidate =
                Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedRoot, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
                return true;

            return normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
