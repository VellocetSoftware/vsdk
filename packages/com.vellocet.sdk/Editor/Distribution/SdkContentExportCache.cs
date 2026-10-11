#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Vellocet.Sdk;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor.Distribution
{
    internal static class SdkContentExportCache
    {
        internal static bool CanReusePreviousEntry(
            IReadOnlyDictionary<string, SdkContentManifestEntry> previousEntries,
            string relativePath,
            long sourceSize,
            long sourceTicks,
            string sourceSha256,
            string destinationAbsolutePath,
            out string sha256)
        {
            sha256 = string.Empty;
            if (previousEntries == null || !previousEntries.TryGetValue(relativePath, out var previous))
                return false;

            if (previous == null || string.IsNullOrWhiteSpace(previous.sha256))
                return false;

            if (previous.bytes != sourceSize)
                return false;

            if (previous.sourceLastWriteUtcTicks != sourceTicks)
                return false;

            if (!string.Equals(previous.sha256, sourceSha256, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!DoesFileMatch(destinationAbsolutePath, sourceSize, sourceSha256))
                return false;

            sha256 = previous.sha256;
            return true;
        }

        internal static bool CanReuseTransformedEntry(
            IReadOnlyDictionary<string, SdkContentManifestEntry> previousEntries,
            string relativePath,
            long transformedSize,
            string transformedSha256,
            string destinationAbsolutePath)
        {
            if (previousEntries == null || !previousEntries.TryGetValue(relativePath, out var previous))
                return false;

            if (previous == null || string.IsNullOrWhiteSpace(previous.sha256))
                return false;

            if (previous.bytes != transformedSize)
                return false;

            if (!string.Equals(previous.sha256, transformedSha256, StringComparison.OrdinalIgnoreCase))
                return false;

            return DoesFileMatch(destinationAbsolutePath, transformedSize, transformedSha256);
        }

        private static bool DoesFileMatch(string absolutePath, long expectedBytes, string expectedSha256)
        {
            if (!File.Exists(absolutePath) || string.IsNullOrWhiteSpace(expectedSha256))
                return false;

            if (new FileInfo(absolutePath).Length != expectedBytes)
                return false;

            return SdkFileHashUtility.TryComputeSha256(absolutePath, out var actualSha256) &&
                   string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryComputeSha256(byte[] bytes, out string sha256)
        {
            sha256 = string.Empty;
            if (bytes == null || bytes.Length == 0)
                return false;

            using var stream = new MemoryStream(bytes, false);
            return SdkFileHashUtility.TryComputeSha256(stream, out sha256);
        }

        internal static IReadOnlyDictionary<string, SdkContentManifestEntry> LoadPreviousEntries(
            string manifestAbsolute)
        {
            if (!File.Exists(manifestAbsolute))
                return new Dictionary<string, SdkContentManifestEntry>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var previousManifest = JsonUtility.FromJson<SdkContentManifest>(File.ReadAllText(manifestAbsolute));
                if (previousManifest?.entries == null || previousManifest.entries.Count == 0)
                    return new Dictionary<string, SdkContentManifestEntry>(StringComparer.OrdinalIgnoreCase);

                return previousManifest.entries
                    .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.relativePath))
                    .ToDictionary(
                        entry => SdkContentPathUtility.NormalizeProjectRelative(entry.relativePath),
                        entry => entry,
                        StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, SdkContentManifestEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }

        internal static T ReadJsonFile<T>(string absolutePath, string label, List<string> errors) where T : class
        {
            if (!File.Exists(absolutePath))
            {
                errors.Add($"SDK content {label} is missing: {absolutePath}");
                return null;
            }

            try
            {
                var parsed = JsonUtility.FromJson<T>(File.ReadAllText(absolutePath));
                if (parsed != null)
                    return parsed;

                errors.Add($"SDK content {label} is invalid JSON: {absolutePath}");
                return null;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to read SDK content {label}: {absolutePath} ({ex.Message})");
                return null;
            }
        }

        internal static void PruneEmptyDirectories(string rootAbsolute)
        {
            if (!Directory.Exists(rootAbsolute))
                return;

            foreach (var dir in Directory.EnumerateDirectories(rootAbsolute, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                if (!Directory.Exists(dir))
                    continue;

                if (Directory.EnumerateFileSystemEntries(dir).Any())
                    continue;

                Directory.Delete(dir, false);
            }
        }
    }
}
#endif