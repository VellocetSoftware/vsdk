#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Vellocet.Sdk;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor.Distribution
{
    /// <summary>
    ///     Creates one deterministic, validated view of every source file owned by the SDK distribution.
    ///     Export and freshness validation both depend on this catalog so their inclusion rules cannot drift.
    /// </summary>
    internal static class SdkContentSourceCatalog
    {
        private const string FingerprintFormat = "vellocet-sdk-source/v1";
        private const string GitLfsPointerHeader = "version https://git-lfs.github.com/spec/v1";

        internal static CatalogResult Build()
        {
            var result = new CatalogResult();
            var entriesByInstalledPath = new Dictionary<string, SourceEntry>(StringComparer.OrdinalIgnoreCase);
            var sourceRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var mapping in Constants.SdkAssetRootMappings)
            {
                var sourceRoot = SdkContentPathUtility.NormalizeProjectRelative(mapping.SourceRoot).TrimEnd('/');
                var installedRoot = SdkContentPathUtility.NormalizeProjectRelative(mapping.InstalledRoot).TrimEnd('/');
                if (string.IsNullOrWhiteSpace(sourceRoot) || string.IsNullOrWhiteSpace(installedRoot))
                {
                    result.Errors.Add("SDK content contains an empty source or installed root mapping.");
                    continue;
                }

                if (!sourceRoots.Add(sourceRoot))
                {
                    result.Errors.Add($"SDK content source root is mapped more than once: {sourceRoot}");
                    continue;
                }

                var sourceRootAbsolute = SdkContentPathUtility.ToAbsolutePath(sourceRoot);
                if (!Directory.Exists(sourceRootAbsolute))
                {
                    result.Errors.Add($"Required SDK content source root is missing: {sourceRoot}");
                    continue;
                }

                foreach (var sourceAbsolutePath in Directory.EnumerateFiles(
                             sourceRootAbsolute,
                             "*",
                             SearchOption.AllDirectories))
                {
                    if (ShouldSkipCatalogFile(sourceAbsolutePath) ||
                        !SdkGame.Current.AllowsSourceAsset(SdkContentPathUtility.ToProjectRelativePath(sourceAbsolutePath)))
                        continue;

                    AddEntry(sourceAbsolutePath, installedRoot, entriesByInstalledPath, result);
                }
            }

            result.Entries.AddRange(entriesByInstalledPath.Values
                .OrderBy(entry => entry.InstalledRelativePath, StringComparer.OrdinalIgnoreCase));
            ValidateAssetMetaPairsAndGuids(result);
            if (result.Errors.Count == 0)
                result.Fingerprint = ComputeFingerprint(result.Entries);

            return result;
        }

        internal static string ComputeFingerprint(IEnumerable<SourceEntry> entries)
        {
            var canonical = new StringBuilder(FingerprintFormat).Append('\n');
            foreach (var entry in (entries ?? Array.Empty<SourceEntry>())
                     .Where(entry => entry != null)
                     .OrderBy(entry => entry.InstalledRelativePath, StringComparer.OrdinalIgnoreCase))
                canonical.Append(entry.InstalledRelativePath)
                    .Append('\0')
                    .Append(entry.SourceSha256)
                    .Append('\0')
                    .Append(entry.SourceBytes)
                    .Append('\n');

            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static void AddEntry(
            string sourceAbsolutePath,
            string expectedInstalledRoot,
            IDictionary<string, SourceEntry> entriesByInstalledPath,
            CatalogResult result)
        {
            var sourceRelativePath = SdkContentPathUtility.ToProjectRelativePath(sourceAbsolutePath);
            if (!SdkContentPathUtility.IsSourceSdkAssetPath(sourceRelativePath) ||
                !SdkContentPathUtility.TryMapSourceToInstalledPath(sourceRelativePath, out var installedRelativePath) ||
                !installedRelativePath.StartsWith(expectedInstalledRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"SDK content file has no unambiguous destination mapping: {sourceRelativePath}");
                return;
            }

            if (entriesByInstalledPath.ContainsKey(installedRelativePath))
            {
                result.Errors.Add($"Multiple SDK source files map to '{installedRelativePath}'.");
                return;
            }

            if (IsGitLfsPointer(sourceAbsolutePath))
            {
                result.Errors.Add(
                    $"SDK source file is an unresolved Git LFS pointer: {sourceRelativePath}. " +
                    "Fetch the LFS payload before building the SDK distribution.");
                return;
            }

            if (!SdkFileHashUtility.TryComputeSha256(sourceAbsolutePath, out var sourceSha256))
            {
                result.Errors.Add($"Failed to hash SDK source file: {sourceRelativePath}");
                return;
            }

            var sourceInfo = new FileInfo(sourceAbsolutePath);
            entriesByInstalledPath.Add(installedRelativePath, new SourceEntry(
                sourceAbsolutePath,
                sourceRelativePath,
                installedRelativePath,
                sourceSha256,
                sourceInfo.Length,
                sourceInfo.LastWriteTimeUtc.Ticks));
        }

        private static void ValidateAssetMetaPairsAndGuids(CatalogResult result)
        {
            var entriesByPath = result.Entries.ToDictionary(
                entry => entry.InstalledRelativePath,
                entry => entry,
                StringComparer.OrdinalIgnoreCase);
            foreach (var entry in result.Entries)
            {
                if (entry.InstalledRelativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                var metaPath = entry.InstalledRelativePath + ".meta";
                if (!entriesByPath.TryGetValue(metaPath, out var metaEntry))
                {
                    result.Errors.Add(
                        $"SDK source asset is missing its Unity .meta file: {entry.SourceRelativePath}");
                    continue;
                }

                if (!SdkUnityMetaFileUtility.TryReadGuid(metaEntry.SourceAbsolutePath, out var guid))
                {
                    result.Errors.Add($"SDK source asset has an invalid Unity GUID: {metaEntry.SourceRelativePath}");
                    continue;
                }

                if (result.AssetPathsByGuid.TryGetValue(guid, out var existingPath))
                {
                    result.Errors.Add(
                        $"SDK source assets share Unity GUID '{guid}': '{existingPath}' and " +
                        $"'{entry.SourceRelativePath}'.");
                    continue;
                }

                result.AssetPathsByGuid.Add(guid, entry.SourceRelativePath);
            }

            foreach (var metaEntry in result.Entries.Where(entry =>
                         entry.InstalledRelativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)))
            {
                var assetPath = metaEntry.InstalledRelativePath[..^".meta".Length];
                if (!entriesByPath.ContainsKey(assetPath))
                    result.Errors.Add(
                        $"SDK source .meta file is missing its asset: {metaEntry.SourceRelativePath}");
            }
        }

        private static bool IsDirectoryMetaFile(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) ||
                !absolutePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                return false;

            return Directory.Exists(absolutePath[..^".meta".Length]);
        }

        private static bool ShouldSkipCatalogFile(string absolutePath)
        {
            return string.Equals(
                       Path.GetFileName(absolutePath),
                       ".DS_Store",
                       StringComparison.OrdinalIgnoreCase) ||
                   IsDirectoryMetaFile(absolutePath);
        }

        internal static bool IsGitLfsPointer(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                return false;

            try
            {
                var info = new FileInfo(absolutePath);
                if (info.Length <= 0 || info.Length > 1024)
                    return false;

                using var reader = new StreamReader(absolutePath);
                return string.Equals(reader.ReadLine(), GitLfsPointerHeader, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        internal sealed class CatalogResult
        {
            internal List<string> Errors { get; } = new();
            internal List<SourceEntry> Entries { get; } = new();

            internal Dictionary<string, string> AssetPathsByGuid { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            internal string Fingerprint { get; set; } = string.Empty;
            internal bool Succeeded => Errors.Count == 0;
        }

        internal sealed class SourceEntry
        {
            internal SourceEntry(
                string sourceAbsolutePath,
                string sourceRelativePath,
                string installedRelativePath,
                string sourceSha256,
                long sourceBytes,
                long sourceLastWriteUtcTicks)
            {
                SourceAbsolutePath = sourceAbsolutePath;
                SourceRelativePath = sourceRelativePath;
                InstalledRelativePath = installedRelativePath;
                SourceSha256 = sourceSha256;
                SourceBytes = sourceBytes;
                SourceLastWriteUtcTicks = sourceLastWriteUtcTicks;
            }

            internal string SourceAbsolutePath { get; }
            internal string SourceRelativePath { get; }
            internal string InstalledRelativePath { get; }
            internal string SourceSha256 { get; }
            internal long SourceBytes { get; }
            internal long SourceLastWriteUtcTicks { get; }
        }
    }
}
#endif