// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.IO;

namespace Vellocet.Sdk.Addons
{
    public static class AddonFilePolicy
    {
        public const int MaximumFilesPerPackage = 1024;
        public const int MaximumDirectoriesPerPackage = 1024;
        public const int MaximumPathLength = 512;
        public const int MaximumPathSegments = 24;
        public const long MaximumPackageBytes = 8L * 1024L * 1024L * 1024L;
        public const long MaximumSingleFileBytes = 2L * 1024L * 1024L * 1024L;
        public const long MaximumImageBytes = 16L * 1024L * 1024L;
        public const long MaximumJsonBytes = 16L * 1024L * 1024L;
        public const long MaximumManifestBytes = 2L * 1024L * 1024L;
        public const long MaximumSourceFileBytes = 2L * 1024L * 1024L;
        public const long MaximumTextFileBytes = 8L * 1024L * 1024L;

        private static readonly HashSet<string> AllowedPackageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".json",
            ".bin",
            ".hash",
            ".bundle",
            ".cs",
            ".csx",
            ".txt",
            ".md",
            ".wav", ".ogg", ".mp3", ".sfmedia", ".sfevent",
            ".png",
            ".jpg",
            ".jpeg",
            ".gif", ".vertexmesh", ".tga", ".fbx", ".obj", ".mtl", ".glb", ".gltf", ".ttf", ".otf", ".mp4", ".webm", ".mov", ".csv"
        };

        public static bool TryNormalizePackageFilePath(string relativePath, out string normalizedRelativePath)
        {
            if (!TryNormalizeCanonicalRelativePath(relativePath, out normalizedRelativePath))
                return false;

            var extension = Path.GetExtension(normalizedRelativePath);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedPackageExtensions.Contains(extension))
            {
                normalizedRelativePath = null;
                return false;
            }

            return true;
        }

        public static bool TryNormalizeSafeDirectoryPath(string relativePath, out string normalizedRelativePath)
        {
            return TryNormalizeCanonicalRelativePath(relativePath, out normalizedRelativePath);
        }

        public static bool TryNormalizeSafeRelativePath(string relativePath, out string normalizedRelativePath)
        {
            return TryNormalizeCanonicalRelativePath(relativePath, out normalizedRelativePath);
        }

        private static bool TryNormalizeCanonicalRelativePath(string relativePath,
            out string normalizedRelativePath)
        {
            normalizedRelativePath = null;
            if (string.IsNullOrEmpty(relativePath) || relativePath.Length > MaximumPathLength ||
                relativePath[0] == '/' || relativePath[^1] == '/')
                return false;

            var segments = relativePath.Split('/');
            if (segments.Length == 0 || segments.Length > MaximumPathSegments)
                return false;

            foreach (var segment in segments)
            {
                if (string.IsNullOrEmpty(segment) || segment is "." or ".." || segment[^1] == '.' ||
                    IsWindowsDeviceName(segment))
                    return false;

                foreach (var character in segment)
                {
                    var isAsciiLetter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
                    var isDigit = character is >= '0' and <= '9';
                    if (!isAsciiLetter && !isDigit && character is not '_' and not '-' and not '.')
                        return false;
                }
            }

            normalizedRelativePath = relativePath;
            return true;
        }

        private static bool IsWindowsDeviceName(string segment)
        {
            var dotIndex = segment.IndexOf('.');
            var stem = dotIndex >= 0 ? segment.Substring(0, dotIndex) : segment;
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase))
                return true;

            if (stem.Length != 4)
                return false;

            var prefix = stem.Substring(0, 3);
            return (prefix.Equals("COM", StringComparison.OrdinalIgnoreCase) ||
                    prefix.Equals("LPT", StringComparison.OrdinalIgnoreCase)) &&
                   stem[3] is >= '1' and <= '9';
        }

        public static bool IsValidSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalized = value.Trim();
            if (normalized.Length != 64)
                return false;

            foreach (var c in normalized)
            {
                var isDigit = c >= '0' && c <= '9';
                var isLowerHex = c >= 'a' && c <= 'f';
                var isUpperHex = c >= 'A' && c <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex)
                    return false;
            }

            return true;
        }

        public static bool TryGetCanonicalContentId(string value, out string canonicalContentId)
        {
            canonicalContentId = null;
            if (string.IsNullOrEmpty(value) || value.Length > 128)
                return false;

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var isLowerAscii = character is >= 'a' and <= 'z';
                var isDigit = character is >= '0' and <= '9';
                if (!isLowerAscii && !isDigit && character != '_')
                    return false;
            }

            if (value[0] == '_' || value[^1] == '_' || value.Contains("__", StringComparison.Ordinal))
                return false;

            canonicalContentId = value;
            return true;
        }

        public static bool TryValidateFileSize(string relativePath, long bytes, out string error)
        {
            error = null;
            if (!TryNormalizePackageFilePath(relativePath, out var normalized))
            {
                error = "Addon file path is unsafe or has an unsupported extension.";
                return false;
            }

            var fileName = Path.GetFileName(normalized);
            var extension = Path.GetExtension(normalized);
            var maximum = extension.ToLowerInvariant() switch
            {
                ".json" when string.Equals(fileName, Constants.AddonManifestFileName,
                    StringComparison.OrdinalIgnoreCase) => MaximumManifestBytes,
                ".json" or ".hash" or ".sfevent" => MaximumJsonBytes,
                ".cs" or ".csx" => MaximumSourceFileBytes,
                ".txt" or ".md" or ".csv" or ".mtl" => MaximumTextFileBytes,
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".tga" => MaximumImageBytes,
                ".wav" or ".ogg" or ".mp3" or ".sfmedia" => Audio.AddonAudioPolicy.MaximumSourceFileBytes,
                ".bin" or ".bundle" => MaximumSingleFileBytes,
                ".vertexmesh" => Vertex.Contracts.AssetCatalog.MaximumBytes,
                ".fbx" or ".obj" or ".glb" or ".gltf" or ".ttf" or ".otf" or ".mp4" or ".webm" or ".mov" => Vertex.Contracts.ContentManifest.MaximumFileBytes,
                _ => 0L
            };

            if (bytes <= 0)
            {
                error = $"Addon file '{normalized}' must not be empty.";
                return false;
            }
            if (bytes > maximum)
            {
                error = $"Addon file '{normalized}' exceeds its {maximum} byte limit.";
                return false;
            }

            return true;
        }

        public static bool TryAccumulate(ref long total, long bytes, long maximum)
        {
            if (total < 0 || bytes < 0 || maximum < 0 || total > maximum - bytes)
                return false;
            total += bytes;
            return true;
        }
    }
}
