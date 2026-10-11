// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using UnityEngine;

namespace Vellocet.Sdk.Utilities
{
    public static class SdkContentPathUtility
    {
        public static string NormalizeInputPath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
                return string.Empty;

            var trimmed = inputPath.Trim().Trim('"');
            try
            {
                if (File.Exists(trimmed))
                    return Path.GetDirectoryName(Path.GetFullPath(trimmed)) ?? string.Empty;

                return Path.GetFullPath(trimmed);
            }
            catch
            {
                return trimmed.Replace('\\', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
            }
        }

        public static string NormalizeProjectRelative(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Replace('\\', '/').Trim().TrimStart('/');
            return normalized.Contains("..", StringComparison.Ordinal) ? string.Empty : normalized;
        }

        public static bool IsInstalledSdkAssetPath(string projectRelativePath)
        {
            return IsUnderAnyRoot(projectRelativePath, Constants.SdkInstalledAssetRoots) &&
                   TryMapInstalledToSourcePath(projectRelativePath, out _);
        }

        public static bool IsSourceSdkAssetPath(string projectRelativePath)
        {
            return IsUnderAnyRoot(projectRelativePath, Constants.SdkSourceAssetRoots) &&
                   SdkGame.Current.AllowsSourceAsset(NormalizeProjectRelative(projectRelativePath));
        }

        public static bool IsSdkAssetPath(string projectRelativePath)
        {
            return IsInstalledSdkAssetPath(projectRelativePath) || IsSourceSdkAssetPath(projectRelativePath);
        }

        public static bool TryMapSourceToInstalledPath(string sourcePath, out string installedPath)
        {
            return TryMapAssetPath(sourcePath, true, out installedPath);
        }

        public static bool TryMapInstalledToSourcePath(string installedPath, out string sourcePath)
        {
            return TryMapAssetPath(installedPath, false, out sourcePath);
        }

        private static bool TryMapAssetPath(string path, bool sourceToInstalled, out string mappedPath)
        {
            mappedPath = string.Empty;
            var normalizedPath = NormalizeProjectRelative(path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return false;

            foreach (var mapping in Constants.SdkAssetRootMappings)
            {
                var fromRoot = sourceToInstalled ? mapping.SourceRoot : mapping.InstalledRoot;
                var toRoot = sourceToInstalled ? mapping.InstalledRoot : mapping.SourceRoot;
                if (TryMapUnderRoot(normalizedPath, fromRoot, toRoot, out mappedPath))
                {
                    if (SdkGame.Current.AllowsSourceAsset(sourceToInstalled ? normalizedPath : mappedPath)) return true;
                    mappedPath = string.Empty;
                    return false;
                }
            }

            return false;
        }

        private static bool TryMapUnderRoot(string path, string fromRoot, string toRoot, out string mappedPath)
        {
            mappedPath = string.Empty;
            var normalizedFrom = NormalizeProjectRelative(fromRoot).TrimEnd('/');
            var normalizedTo = NormalizeProjectRelative(toRoot).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalizedFrom) || string.IsNullOrWhiteSpace(normalizedTo))
                return false;

            if (string.Equals(path, normalizedFrom, StringComparison.OrdinalIgnoreCase))
            {
                mappedPath = normalizedTo;
                return true;
            }

            if (string.Equals(path, normalizedFrom + ".meta", StringComparison.OrdinalIgnoreCase))
            {
                mappedPath = normalizedTo + ".meta";
                return true;
            }

            if (!path.StartsWith(normalizedFrom + "/", StringComparison.OrdinalIgnoreCase))
                return false;

            mappedPath = normalizedTo + path[normalizedFrom.Length..];
            return true;
        }

        private static bool IsUnderAnyRoot(string projectRelativePath, string[] roots)
        {
            var normalizedPath = NormalizeProjectRelative(projectRelativePath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return false;

            foreach (var root in roots ?? Array.Empty<string>())
            {
                var normalizedRoot = NormalizeProjectRelative(root);
                if (string.IsNullOrWhiteSpace(normalizedRoot))
                    continue;

                if (normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(normalizedPath, normalizedRoot + ".meta", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName
                   ?? throw new InvalidOperationException("Project root could not be resolved.");
        }

        public static string ToAbsolutePath(string projectRelativePath)
        {
            var normalized = NormalizeProjectRelative(projectRelativePath);
            return Path.Combine(GetProjectRoot(), normalized.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string ToProjectRelativePath(string absolutePath)
        {
            var projectRoot = EnsureTrailingSeparator(GetProjectRoot());
            var normalizedAbsolute = Path.GetFullPath(absolutePath);
            if (!normalizedAbsolute.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Path is outside project root: {absolutePath}");

            var relative = normalizedAbsolute[projectRoot.Length..];
            return relative.Replace('\\', '/');
        }

        public static void EnsurePathWithinProject(string absolutePath)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(GetProjectRoot()));
            var path = Path.GetFullPath(absolutePath);
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Path is outside project root: {absolutePath}");
        }

        public static bool TryResolveContainedPath(string rootAbsolute, string relativePath,
            out string resolvedAbsolute)
        {
            resolvedAbsolute = string.Empty;
            if (string.IsNullOrWhiteSpace(rootAbsolute) || string.IsNullOrWhiteSpace(relativePath))
                return false;

            try
            {
                var normalizedRelative = NormalizeProjectRelative(relativePath);
                if (string.IsNullOrWhiteSpace(normalizedRelative))
                    return false;

                var combined = Path.Combine(rootAbsolute, normalizedRelative.Replace('/', Path.DirectorySeparatorChar));
                var fullPath = Path.GetFullPath(combined);
                var rootWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(rootAbsolute));
                if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                    return false;

                resolvedAbsolute = fullPath;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool ShouldSkipFile(string absolutePath)
        {
            var fileName = Path.GetFileName(absolutePath);
            return string.Equals(fileName, ".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(Path.GetExtension(fileName), ".meta", StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                return path;

            return path + Path.DirectorySeparatorChar;
        }
    }
}