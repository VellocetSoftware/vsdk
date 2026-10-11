// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using UnityEngine;

namespace Vellocet.Sdk.Editor.Tools
{
    internal static class SdkPackageOutputPathPolicy
    {
        private const string LiveAddonsFolderName = "addons";
        private const string DataLayoutMarkerFileName = "data-layout.json";

        internal static bool IsReservedRuntimeDataRoot(string value)
        {
            var normalized = SdkPathDefaults.NormalizePath(value);
            var runtimeDataRoot = SdkPathDefaults.NormalizePath(
                Application.persistentDataPath);
            var hasMarkedRuntimeDataRoot = TryFindProductDataRoot(normalized, out var markedRuntimeDataRoot);

            if (IsLiveAddonsRoot(normalized, runtimeDataRoot) ||
                IsLiveAddonsRoot(normalized, markedRuntimeDataRoot))
                return false;

            return IsSameOrDescendant(normalized, runtimeDataRoot) || hasMarkedRuntimeDataRoot;
        }

        private static bool TryFindProductDataRoot(string path, out string dataRoot)
        {
            dataRoot = string.Empty;
            var current = path;
            while (!string.IsNullOrWhiteSpace(current))
            {
                var markerPath = Path.Combine(current, DataLayoutMarkerFileName);
                try
                {
                    if (File.Exists(markerPath) && HasProductDataFormat(File.ReadAllText(markerPath)))
                    {
                        dataRoot = SdkPathDefaults.NormalizePath(current);
                        return true;
                    }
                }
                catch
                {
                    // An unreadable ancestor is not enough evidence to classify the selected folder.
                }

                var parent = Directory.GetParent(current)?.FullName;
                if (string.IsNullOrWhiteSpace(parent) || PathsEqual(parent, current))
                    break;

                current = parent;
            }

            return false;
        }

        private static bool HasProductDataFormat(string marker)
        {
            if (string.IsNullOrWhiteSpace(marker))
                return false;

            const string formatProperty = "\"format\"";
            const string dataVersionMarker = "-data/v";
            var propertyIndex = marker.IndexOf(formatProperty, StringComparison.OrdinalIgnoreCase);
            if (propertyIndex < 0)
                return false;

            var valueStart = marker.IndexOf('"', propertyIndex + formatProperty.Length);
            if (valueStart < 0)
                return false;
            var valueEnd = marker.IndexOf('"', valueStart + 1);
            if (valueEnd <= valueStart + 1)
                return false;

            var format = marker.Substring(valueStart + 1, valueEnd - valueStart - 1).Trim();
            var versionIndex = format.LastIndexOf(dataVersionMarker, StringComparison.OrdinalIgnoreCase);
            return versionIndex > 0 &&
                   int.TryParse(format.Substring(versionIndex + dataVersionMarker.Length), out var version) &&
                   version > 0;
        }

        private static bool IsLiveAddonsRoot(string candidate, string dataRoot)
        {
            return !string.IsNullOrWhiteSpace(dataRoot) &&
                   PathsEqual(candidate, Path.Combine(dataRoot, LiveAddonsFolderName));
        }

        internal static bool TryResolve(string value, out string normalizedPath, out string error)
        {
            normalizedPath = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Choose a package output root before building an add-on.";
                return false;
            }

            if (!Path.IsPathRooted(value.Trim()))
            {
                error = "The package output root must be an absolute path selected with the folder picker.";
                return false;
            }

            normalizedPath = SdkPathDefaults.NormalizePath(value);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                error = "The selected package output root is invalid.";
                return false;
            }

            if (!Directory.Exists(normalizedPath))
            {
                error = "The selected package output root no longer exists. Choose it again.";
                return false;
            }

            var fileSystemRoot = SdkPathDefaults.NormalizePath(Path.GetPathRoot(normalizedPath));
            if (PathsEqual(normalizedPath, fileSystemRoot))
            {
                error = "A drive or filesystem root cannot be used as the package output root.";
                return false;
            }

            if (IsReservedRuntimeDataRoot(normalizedPath))
            {
                error = "A Unity runtime-data folder is not a valid package output root. " +
                        "Choose a dedicated folder for built add-ons.";
                return false;
            }

            var projectRoot = SdkPathDefaults.NormalizePath(Path.GetDirectoryName(Application.dataPath));
            if (IsSameOrDescendant(normalizedPath, projectRoot))
            {
                error = "Choose a package output root outside the Unity project. Building inside the project can " +
                        "trigger asset imports and leave generated files in source control.";
                return false;
            }

            return true;
        }

        internal static bool TryResolveWritable(string value, out string normalizedPath, out string error)
        {
            if (!TryResolve(value, out normalizedPath, out error))
                return false;

            var probePath = Path.Combine(normalizedPath, $".vellocet-write-probe-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(probePath, "write-access-check");
                File.Delete(probePath);
                return true;
            }
            catch (Exception ex)
            {
                error = $"The selected package output root is not writable: {ex.Message}";
                return false;
            }
            finally
            {
                TryDeleteProbe(probePath);
            }
        }

        private static void TryDeleteProbe(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // The original write-access error is more useful than cleanup failure details.
            }
        }

        private static bool IsSameOrDescendant(string candidate, string root)
        {
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(root))
                return false;

            if (PathsEqual(candidate, root))
                return true;

            var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                    Path.DirectorySeparatorChar;
            return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return string.Equals(
                left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}