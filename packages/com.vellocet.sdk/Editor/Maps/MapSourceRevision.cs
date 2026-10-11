using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class MapSourceRevision
    {
        // Hash source files, not target-specific imported artifacts or build timestamps.
        public static string Compute(string scenePath)
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
                throw new ArgumentException("A saved authoring scene is required.", nameof(scenePath));
            var payload = new StringBuilder();
            foreach (var path in AssetDatabase.GetDependencies(scenePath, true).OrderBy(path => path, StringComparer.Ordinal))
            {
                var file = path;
                if (path.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                    if (package != null) file = package.resolvedPath + path.Substring(package.assetPath.Length);
                }
                if (Directory.Exists(file)) continue;
                if (!File.Exists(file))
                {
                    if (path.StartsWith("Resources/", StringComparison.Ordinal) ||
                        path.StartsWith("Library/", StringComparison.Ordinal)) continue;
                    throw new FileNotFoundException("A map source dependency is missing.", path);
                }
                payload.Append(AssetDatabase.AssetPathToGUID(path)).Append('\n');
                AppendHash(payload, file);
                if (File.Exists(file + ".meta")) AppendHash(payload, file + ".meta");
            }
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload.ToString()), false);
            if (!SdkFileHashUtility.TryComputeSha256(stream, out var hash))
                throw new IOException("Could not hash the map source dependencies.");
            return hash;
        }

        private static void AppendHash(StringBuilder payload, string path)
        {
            if (!SdkFileHashUtility.TryComputeSha256(path, out var hash))
                throw new IOException($"Could not hash map dependency '{path}'.");
            payload.Append(hash).Append('\n');
        }
    }
}
