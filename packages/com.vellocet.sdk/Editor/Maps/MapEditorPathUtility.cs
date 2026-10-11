// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class MapEditorPathUtility
    {
        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
        }

        public static string ToAbsolutePath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : Path.GetFullPath(assetPath.Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar));
        }

        public static string GetSceneDataFolderAssetPath(string scenePath)
        {
            scenePath = NormalizeAssetPath(scenePath);
            if (string.IsNullOrWhiteSpace(scenePath))
                return string.Empty;

            var directory = Path.GetDirectoryName(scenePath);
            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(sceneName)
                ? string.Empty
                : NormalizeAssetPath(Path.Combine(directory, sceneName));
        }

        public static string GetSceneDataFolderAbsolutePath(string scenePath)
        {
            var assetPath = GetSceneDataFolderAssetPath(scenePath);
            return string.IsNullOrWhiteSpace(assetPath) ? string.Empty : ToAbsolutePath(assetPath);
        }

        public static bool EnsureAssetFolder(string folderPath)
        {
            folderPath = NormalizeAssetPath(folderPath);
            if (string.IsNullOrWhiteSpace(folderPath))
                return false;

            if (AssetDatabase.IsValidFolder(folderPath))
                return true;

            Directory.CreateDirectory(ToAbsolutePath(folderPath));
            AssetDatabase.Refresh();
            return AssetDatabase.IsValidFolder(folderPath);
        }

        public static string MakeRelativePath(string root, string child)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(child))
                return string.Empty;

            var rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            var childUri = new Uri(Path.GetFullPath(child));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(childUri).ToString())
                .Replace('\\', '/');
        }

        public static bool PathsEqual(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
                return false;

            var normalizedFirst = Path.GetFullPath(first)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedSecond = Path.GetFullPath(second)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
#endif