// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vellocet.Sdk.Editor.VSig
{
    public enum VSigMapSourceStatus
    {
        Found,
        NotFound,
        Multiple,
        InvalidScenePath
    }

    public static class VSigMapSourceUtility
    {
        internal const string DefaultSourceContent = "# VSig script\n";

        public static bool TryFindForScene(string scenePath, out TextAsset source, out string sourcePath)
        {
            return FindForScene(scenePath, out source, out sourcePath, out _) == VSigMapSourceStatus.Found;
        }

        public static bool TryEnsureForScene(string scenePath, out TextAsset source, out string sourcePath,
            out bool created, out string error)
        {
            source = null;
            sourcePath = null;
            created = false;
            error = null;

            if (!TryGetExpectedPathForScene(scenePath, out sourcePath))
            {
                error = $"Could not resolve a VSig path for scene '{scenePath}'.";
                return false;
            }

            try
            {
                if (!File.Exists(sourcePath))
                {
                    File.WriteAllText(sourcePath, DefaultSourceContent);
                    created = true;
                }

                AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
                source = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
                if (source)
                    return true;

                error = $"VSig file could not be imported at '{sourcePath}'.";
                return false;
            }
            catch (Exception exception)
            {
                error = $"Could not create VSig file at '{sourcePath}': {exception.Message}";
                return false;
            }
        }

        public static VSigMapSourceStatus FindForScene(string scenePath, out TextAsset source, out string sourcePath,
            out string expectedPath)
        {
            source = null;
            sourcePath = null;
            expectedPath = null;

            if (!TryGetExpectedPathForScene(scenePath, out expectedPath))
                return VSigMapSourceStatus.InvalidScenePath;

            if (TryLoad(expectedPath, out source, out sourcePath))
                return VSigMapSourceStatus.Found;

            var folder = Path.GetDirectoryName(expectedPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(folder))
                return VSigMapSourceStatus.InvalidScenePath;
            if (!AssetDatabase.IsValidFolder(folder))
                return VSigMapSourceStatus.NotFound;

            var paths = AssetDatabase.FindAssets("t:TextAsset", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => string.Equals(Path.GetExtension(path), ".vsig", StringComparison.OrdinalIgnoreCase) &&
                               !path.Split('/').Any(part => part.EndsWith(".backups", StringComparison.OrdinalIgnoreCase)) &&
                               !File.Exists(Path.ChangeExtension(path, ".unity")))
                .ToArray();
            // Prefer the scene's sibling script over captured Vertex sources in subfolders.
            var siblings = paths.Where(path => string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'),
                folder, StringComparison.OrdinalIgnoreCase)).ToArray();
            TextAsset candidate = null;
            string candidatePath = null;
            foreach (var path in siblings.Length > 0 ? siblings : paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (!asset)
                    continue;

                if (candidate)
                    return VSigMapSourceStatus.Multiple;

                candidate = asset;
                candidatePath = path;
            }

            source = candidate;
            sourcePath = candidatePath;
            return source ? VSigMapSourceStatus.Found : VSigMapSourceStatus.NotFound;
        }

        public static bool TryGetExpectedPathForScene(string scenePath, out string expectedPath)
        {
            expectedPath = null;
            if (string.IsNullOrWhiteSpace(scenePath))
                return false;

            var folder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(sceneName))
                return false;

            expectedPath = $"{folder}/{sceneName}.vsig";
            return true;
        }

        private static bool TryLoad(string path, out TextAsset source, out string sourcePath)
        {
            sourcePath = path;
            source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (source)
                return true;

            sourcePath = null;
            return false;
        }
    }
}
#endif
