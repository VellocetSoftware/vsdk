// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using Vellocet.Sdk.Addons;

namespace Vellocet.Sdk.Editor.Maps
{
    public readonly struct AddonMapArtifactLayout
    {
        public AddonMapArtifactLayout(string addonRoot, string mapRoot, string manifestPath, string receiptPath,
            string exportReportPath)
        {
            AddonRoot = addonRoot;
            MapRoot = mapRoot;
            ManifestPath = manifestPath;
            ReceiptPath = receiptPath;
            ExportReportPath = exportReportPath;
        }

        public string AddonRoot { get; }
        public string MapRoot { get; }
        public string ManifestPath { get; }
        public string ReceiptPath { get; }
        public string ExportReportPath { get; }
    }

    public static class MapBuildArtifactPlanner
    {
        public static string NormalizeMapId(string value)
        {
            return ContentIdUtility.Sanitize(value);
        }

        public static string NormalizeDefaultModeId(string value)
        {
            return ModeIdPolicy.NormalizeOrEmpty(value);
        }

        public static string ResolveSuggestedPrefix(string modeId)
        {
            if (ModeCatalog.TryGetByCanonicalId(modeId, out var descriptor))
                return MapPrefixPolicy.NormalizeOrEmpty(descriptor.MapIdPrefix);
            return string.Empty;
        }

        public static string BuildMapId(string mapPrefix, string mapSlug)
        {
            var prefix = MapPrefixPolicy.NormalizeOrEmpty(mapPrefix);
            var slug = NormalizeMapId(mapSlug);
            if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(slug))
                return string.Empty;
            if (string.Equals(slug, prefix, StringComparison.Ordinal) ||
                slug.StartsWith(prefix + "_", StringComparison.Ordinal))
                return slug;
            return $"{prefix}_{slug}";
        }

        public static string BuildCanonicalAddonMapId(string defaultModeId, params string[] titleCandidates)
        {
            var modePrefix = ResolveSuggestedPrefix(defaultModeId);
            if (string.IsNullOrWhiteSpace(modePrefix))
                return string.Empty;
            var titleToken = ResolveFirstSanitizedToken(titleCandidates);
            if (string.IsNullOrWhiteSpace(titleToken))
                return modePrefix;
            if (string.Equals(titleToken, modePrefix, StringComparison.Ordinal))
                return titleToken;
            if (titleToken.StartsWith(modePrefix + "_", StringComparison.Ordinal))
                return titleToken;

            return $"{modePrefix}_{titleToken}";
        }

        public static AddonMapArtifactLayout PlanAddon(string addonsRoot, string addonId, string mapId)
        {
            var sanitizedAddonId = AddonIdPolicy.NormalizeOrEmpty(addonId);
            var sanitizedMapId = NormalizeMapId(mapId);
            var addonRoot = string.IsNullOrWhiteSpace(addonsRoot) || string.IsNullOrWhiteSpace(sanitizedAddonId)
                ? null
                : Path.Combine(addonsRoot, sanitizedAddonId);
            var mapRoot = string.IsNullOrWhiteSpace(addonRoot) || string.IsNullOrWhiteSpace(sanitizedMapId)
                ? null
                : Path.Combine(addonRoot, Constants.MapsFolder, sanitizedMapId);
            var manifestPath = string.IsNullOrWhiteSpace(addonRoot) ? null : AddonHelper.GetManifestPath(addonRoot);
            var exportReportPath = string.IsNullOrWhiteSpace(mapRoot)
                ? null
                : Path.Combine(mapRoot, Constants.ExportReportFileName);
            var receiptPath = string.IsNullOrWhiteSpace(mapRoot)
                ? null
                : Path.Combine(mapRoot, Constants.CompilationReceiptFileName);
            return new AddonMapArtifactLayout(addonRoot, mapRoot, manifestPath, receiptPath, exportReportPath);
        }

        public static string ComputePlatformOutputDirectory(string mapRoot, BuildTarget target)
        {
            return string.IsNullOrWhiteSpace(mapRoot)
                ? null
                : Path.Combine(mapRoot, Exporter.PlatformFolderName(target));
        }

        public static string MakeRelativePath(string root, string child)
        {
            var relativePath = MapEditorPathUtility.MakeRelativePath(root, child);
            return string.IsNullOrWhiteSpace(relativePath) ? null : relativePath;
        }

        private static string ResolveFirstSanitizedToken(string[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return null;

            for (var i = 0; i < candidates.Length; i++)
            {
                var normalized = NormalizeMapId(candidates[i]);
                if (!string.IsNullOrWhiteSpace(normalized))
                    return normalized;
            }

            return null;
        }
    }
}
#endif