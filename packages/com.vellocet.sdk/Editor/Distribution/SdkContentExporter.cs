#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using Vellocet.Sdk;
using Vellocet.Sdk.Utilities;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Distribution
{
    internal static class SdkContentExporter
    {
        private const string ExportStampFileName = "sdk-content-export-stamp.json";

        internal static ExportResult ExportToOutputRoot(string outputRootRepositoryRelative)
        {
            var result = new ExportResult
            {
                OutputRootPath = SdkArtifactPathUtility.NormalizeRepositoryRelative(outputRootRepositoryRelative),
                ContentRootPath = SdkArtifactPathUtility.NormalizeRepositoryRelative(outputRootRepositoryRelative)
            };

            try
            {
                if (string.IsNullOrWhiteSpace(result.OutputRootPath))
                {
                    result.Errors.Add("Output root is required.");
                    return result;
                }

                var outputRootAbsolute = SdkArtifactPathUtility.ToAbsolutePath(result.OutputRootPath);
                var contentRootAbsolute = outputRootAbsolute;
                var manifestAbsolute = Path.Combine(contentRootAbsolute, Constants.SdkContentManifestFileName);
                SdkArtifactPathUtility.EnsurePathWithinRepository(outputRootAbsolute);
                Directory.CreateDirectory(contentRootAbsolute);
                SdkMaterialProxyExporter.BeginSession(result.Warnings);

                var sourceCatalog = SdkContentSourceCatalog.Build();
                result.SourceFileCount = sourceCatalog.Entries.Count;
                result.SourceFingerprint = sourceCatalog.Fingerprint;
                result.Errors.AddRange(sourceCatalog.Errors);
                if (!sourceCatalog.Succeeded)
                    return result;

                var manifest = new SdkContentManifest
                {
                    schemaVersion = Constants.SdkContentSchemaVersion,
                    exportedAtUtc = DateTime.UtcNow.ToString("O"),
                    entries = new List<SdkContentManifestEntry>()
                };

                var previousEntries = SdkContentExportCache.LoadPreviousEntries(manifestAbsolute);
                var entriesByPath = new Dictionary<string, SdkContentManifestEntry>(StringComparer.OrdinalIgnoreCase);
                var sourcePaths = new HashSet<string>(
                    sourceCatalog.Entries.Select(entry => entry.InstalledRelativePath),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var sourceEntry in sourceCatalog.Entries)
                    SyncIntoContent(sourceEntry, contentRootAbsolute, previousEntries, entriesByPath, result);

                if (result.Errors.Count > 0)
                    return result;

                DeleteStaleOutputFiles(contentRootAbsolute, sourcePaths, result);
                SdkContentExportCache.PruneEmptyDirectories(contentRootAbsolute);

                manifest.entries = entriesByPath.Values
                    .OrderBy(entry => entry.relativePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                File.WriteAllText(manifestAbsolute, JsonUtility.ToJson(manifest, true));
                result.EntryCount = manifest.entries.Count;

                var stamp = new ExportStamp
                {
                    exportedAtUtc = manifest.exportedAtUtc,
                    unityVersion = Application.unityVersion,
                    productName = Application.productName,
                    schemaVersion = manifest.schemaVersion,
                    entryCount = manifest.entries.Count,
                    sourceFileCount = sourceCatalog.Entries.Count,
                    sourceFingerprint = sourceCatalog.Fingerprint
                };

                File.WriteAllText(Path.Combine(outputRootAbsolute, ExportStampFileName),
                    JsonUtility.ToJson(stamp, true));

                var proxyStats = SdkMaterialProxyExporter.GetSessionStats();
                result.ProxyCacheHitCount = proxyStats.CacheHitCount;
                result.ProxyCacheMissCount = proxyStats.CacheMissCount;
                result.GeneratedProxyPayloadCount = proxyStats.GeneratedProxyCount;
                result.ProxyBypassedManagedShaderCount = proxyStats.BypassedManagedShaderCount;
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.ToString());
            }
            finally
            {
                SdkMaterialProxyExporter.EndSession();
            }

            return result;
        }

        internal static string BuildSummary(ExportResult result)
        {
            if (result == null)
                return "No SDK content export result.";

            var lines = new List<string>
            {
                $"Succeeded: {(result.Succeeded ? "yes" : "no")}",
                $"Output Root: {result.OutputRootPath}",
                $"Content Root: {result.ContentRootPath}",
                $"Entries: {result.EntryCount}",
                $"Source Files: {result.SourceFileCount}",
                $"Source Fingerprint: {result.SourceFingerprint}",
                $"Copied Files: {result.CopiedFileCount}",
                $"Unchanged Files: {result.UnchangedFileCount}",
                $"Deleted Files: {result.DeletedFileCount}",
                $"Proxied Materials: {result.ProxiedMaterialCount}",
                $"Proxied Materials (Copied): {result.ProxiedMaterialCopiedCount}",
                $"Proxied Materials (Unchanged): {result.ProxiedMaterialUnchangedCount}",
                $"Proxy Cache Hits: {result.ProxyCacheHitCount}",
                $"Proxy Cache Misses: {result.ProxyCacheMissCount}",
                $"Generated Proxy Payloads: {result.GeneratedProxyPayloadCount}",
                $"Proxy Bypassed (Managed Shader Exported): {result.ProxyBypassedManagedShaderCount}",
                $"Warnings: {result.Warnings.Count}",
                $"Errors: {result.Errors.Count}"
            };

            if (result.ProxiedMaterialSamples.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Proxied Material Samples:");
                lines.AddRange(result.ProxiedMaterialSamples);
            }

            if (result.Warnings.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Warnings:");
                lines.AddRange(result.Warnings);
            }

            if (result.Errors.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Errors:");
                lines.AddRange(result.Errors);
            }

            return string.Join("\n", lines);
        }

        internal static ValidationResult ValidateOutputRoot(string outputRootRepositoryRelative)
        {
            var result = new ValidationResult
            {
                OutputRootPath = SdkArtifactPathUtility.NormalizeRepositoryRelative(outputRootRepositoryRelative),
                ContentRootPath = SdkArtifactPathUtility.NormalizeRepositoryRelative(outputRootRepositoryRelative)
            };

            try
            {
                if (string.IsNullOrWhiteSpace(result.OutputRootPath))
                {
                    result.Errors.Add("Output root is required.");
                    return result;
                }

                var outputRootAbsolute = SdkArtifactPathUtility.ToAbsolutePath(result.OutputRootPath);
                var contentRootAbsolute = outputRootAbsolute;
                var manifestAbsolute = Path.Combine(contentRootAbsolute, Constants.SdkContentManifestFileName);
                var stampAbsolute = Path.Combine(outputRootAbsolute, ExportStampFileName);
                SdkArtifactPathUtility.EnsurePathWithinRepository(outputRootAbsolute);

                if (!Directory.Exists(contentRootAbsolute))
                {
                    result.Errors.Add($"SDK content root is missing: {contentRootAbsolute}");
                    return result;
                }

                var manifest = SdkContentExportCache.ReadJsonFile<SdkContentManifest>(manifestAbsolute,
                    "content manifest", result.Errors);
                var stamp = SdkContentExportCache.ReadJsonFile<ExportStamp>(stampAbsolute, "export stamp",
                    result.Errors);
                if (manifest == null || stamp == null)
                    return result;

                result.ManifestEntryCount = manifest.entries?.Count ?? 0;

                if (manifest.schemaVersion != Constants.SdkContentSchemaVersion)
                    result.Errors.Add(
                        $"Manifest schema mismatch. Expected {Constants.SdkContentSchemaVersion}, got {manifest.schemaVersion}.");

                if (stamp.schemaVersion != Constants.SdkContentSchemaVersion)
                    result.Errors.Add(
                        $"Stamp schema mismatch. Expected {Constants.SdkContentSchemaVersion}, got {stamp.schemaVersion}.");

                if (stamp.entryCount != result.ManifestEntryCount)
                    result.Errors.Add(
                        $"Stamp entry count mismatch. Stamp={stamp.entryCount}, Manifest={result.ManifestEntryCount}.");

                if (!string.Equals(stamp.exportedAtUtc, manifest.exportedAtUtc, StringComparison.Ordinal))
                    result.Warnings.Add("Stamp timestamp does not match manifest timestamp.");

                var sourceCatalog = SdkContentSourceCatalog.Build();
                result.SourceFileCount = sourceCatalog.Entries.Count;
                result.SourceFingerprint = sourceCatalog.Fingerprint;
                result.Errors.AddRange(sourceCatalog.Errors);
                if (!sourceCatalog.Succeeded)
                    return result;

                if (string.IsNullOrWhiteSpace(stamp.sourceFingerprint) || stamp.sourceFileCount <= 0)
                {
                    result.Errors.Add(
                        "SDK content export stamp predates source-freshness validation. Rebuild the SDK distribution.");
                }
                else
                {
                    if (stamp.sourceFileCount != sourceCatalog.Entries.Count)
                        result.Errors.Add(
                            $"SDK content is stale. Exported source files={stamp.sourceFileCount}, " +
                            $"current source files={sourceCatalog.Entries.Count}. Rebuild the SDK distribution.");

                    if (!string.Equals(stamp.sourceFingerprint, sourceCatalog.Fingerprint,
                            StringComparison.OrdinalIgnoreCase))
                        result.Errors.Add(
                            "SDK content is stale because its source fingerprint does not match the current " +
                            "managed asset roots. Rebuild the SDK distribution.");
                }

                if (manifest.entries == null || manifest.entries.Count == 0)
                {
                    result.Errors.Add("SDK content manifest contains no entries.");
                    return result;
                }

                var expectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in manifest.entries)
                {
                    var normalizedPath = SdkContentPathUtility.NormalizeProjectRelative(entry?.relativePath);
                    if (string.IsNullOrWhiteSpace(normalizedPath))
                    {
                        result.Errors.Add("Manifest entry contains an empty or invalid path.");
                        continue;
                    }

                    if (!normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Errors.Add($"Manifest entry is not under Assets/: {normalizedPath}");
                        continue;
                    }

                    if (!SdkContentPathUtility.IsInstalledSdkAssetPath(normalizedPath))
                    {
                        result.Errors.Add($"Manifest entry is outside managed SDK roots: {normalizedPath}");
                        continue;
                    }

                    if (!SdkContentPathUtility.TryMapInstalledToSourcePath(normalizedPath, out _))
                    {
                        result.Errors.Add($"Manifest entry has no SDK source mapping: {normalizedPath}");
                        continue;
                    }

                    if (!expectedPaths.Add(normalizedPath))
                    {
                        result.Errors.Add($"Manifest has duplicate entry: {normalizedPath}");
                        continue;
                    }

                    if (!SdkContentPathUtility.TryResolveContainedPath(contentRootAbsolute, normalizedPath,
                            out var absolutePath))
                    {
                        result.Errors.Add($"Manifest entry escaped content root: {normalizedPath}");
                        continue;
                    }

                    if (!File.Exists(absolutePath))
                    {
                        result.Errors.Add($"SDK content file is missing: {normalizedPath}");
                        continue;
                    }

                    if (entry.bytes < 0)
                    {
                        result.Errors.Add($"Manifest entry has invalid byte count: {normalizedPath}");
                        continue;
                    }

                    var actualBytes = new FileInfo(absolutePath).Length;
                    if (actualBytes != entry.bytes)
                    {
                        result.Errors.Add(
                            $"SDK content file size mismatch for '{normalizedPath}'. Expected {entry.bytes}, got {actualBytes}.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(entry.sha256))
                    {
                        result.Errors.Add($"Manifest entry is missing sha256: {normalizedPath}");
                        continue;
                    }

                    if (!SdkFileHashUtility.TryComputeSha256(absolutePath, out var actualHash))
                    {
                        result.Errors.Add($"Failed to hash SDK content file: {normalizedPath}");
                        continue;
                    }

                    if (!string.Equals(actualHash, entry.sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Errors.Add($"SDK content hash mismatch for '{normalizedPath}'.");
                        continue;
                    }

                    result.ValidatedFileCount++;
                }

                foreach (var expectedPath in expectedPaths)
                {
                    var pairedPath = expectedPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                        ? expectedPath[..^".meta".Length]
                        : expectedPath + ".meta";
                    if (!expectedPaths.Contains(pairedPath))
                        result.Errors.Add(
                            $"Manifest entry is missing its Unity asset/meta pair: {expectedPath}");
                }

                foreach (var file in Directory.EnumerateFiles(contentRootAbsolute, "*", SearchOption.AllDirectories))
                {
                    if (SdkContentPathUtility.ShouldSkipFile(file))
                        continue;

                    var relative = Path.GetRelativePath(contentRootAbsolute, file).Replace('\\', '/');
                    if (!relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.Equals(relative, Constants.SdkContentManifestFileName,
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.Equals(relative, ExportStampFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!expectedPaths.Contains(relative))
                        result.Warnings.Add($"Untracked file exists in content package: {relative}");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.ToString());
            }

            return result;
        }

        internal static string BuildValidationSummary(ValidationResult result)
        {
            if (result == null)
                return "No SDK content validation result.";

            var lines = new List<string>
            {
                $"Succeeded: {(result.Succeeded ? "yes" : "no")}",
                $"Output Root: {result.OutputRootPath}",
                $"Content Root: {result.ContentRootPath}",
                $"Manifest Entries: {result.ManifestEntryCount}",
                $"Validated Files: {result.ValidatedFileCount}",
                $"Current Source Files: {result.SourceFileCount}",
                $"Current Source Fingerprint: {result.SourceFingerprint}",
                $"Warnings: {result.Warnings.Count}",
                $"Errors: {result.Errors.Count}"
            };

            if (result.Warnings.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Warnings:");
                lines.AddRange(result.Warnings);
            }

            if (result.Errors.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Errors:");
                lines.AddRange(result.Errors);
            }

            return string.Join("\n", lines);
        }

        private static void SyncIntoContent(SdkContentSourceCatalog.SourceEntry sourceEntry,
            string contentRootAbsolute,
            IReadOnlyDictionary<string, SdkContentManifestEntry> previousEntries,
            IDictionary<string, SdkContentManifestEntry> entriesByPath,
            ExportResult result)
        {
            if (sourceEntry == null || !File.Exists(sourceEntry.SourceAbsolutePath))
            {
                result.Errors.Add(
                    $"SDK source file disappeared during export: {sourceEntry?.SourceRelativePath ?? "<unknown>"}");
                return;
            }

            var sourceAbsolutePath = sourceEntry.SourceAbsolutePath;
            var sourceRelativePath = sourceEntry.SourceRelativePath;
            var relativePath = sourceEntry.InstalledRelativePath;

            if (!SdkContentPathUtility.TryResolveContainedPath(contentRootAbsolute, relativePath,
                    out var destinationAbsolutePath))
            {
                result.Errors.Add($"Content path escaped output root: {relativePath}");
                return;
            }

            var sourceTicks = sourceEntry.SourceLastWriteUtcTicks;
            if (!SdkMaterialProxyExporter.TryBuildProxyMaterial(sourceRelativePath,
                    out var proxyPayload,
                    out var proxyError))
            {
                result.Errors.Add(proxyError);
                return;
            }

            if (proxyPayload.HasValue)
            {
                if (!SdkContentExportCache.TryComputeSha256(proxyPayload.Bytes, out var proxiedSha256))
                {
                    result.Errors.Add($"Failed to hash proxied material payload: {relativePath}");
                    return;
                }

                var proxiedSize = proxyPayload.Bytes.LongLength;
                if (SdkContentExportCache.CanReuseTransformedEntry(previousEntries, relativePath, proxiedSize,
                        proxiedSha256,
                        destinationAbsolutePath))
                {
                    entriesByPath[relativePath] = new SdkContentManifestEntry
                    {
                        relativePath = relativePath,
                        sha256 = proxiedSha256,
                        bytes = proxiedSize,
                        sourceLastWriteUtcTicks = sourceTicks
                    };
                    result.UnchangedFileCount++;
                    result.RecordProxiedMaterial(relativePath, false);
                    return;
                }

                var proxyDestinationDir = Path.GetDirectoryName(destinationAbsolutePath);
                if (!string.IsNullOrWhiteSpace(proxyDestinationDir))
                    Directory.CreateDirectory(proxyDestinationDir);

                File.WriteAllBytes(destinationAbsolutePath, proxyPayload.Bytes);
                entriesByPath[relativePath] = new SdkContentManifestEntry
                {
                    relativePath = relativePath,
                    sha256 = proxiedSha256,
                    bytes = proxiedSize,
                    sourceLastWriteUtcTicks = sourceTicks
                };
                result.CopiedFileCount++;
                result.RecordProxiedMaterial(relativePath, true);
                return;
            }

            var sourceSize = sourceEntry.SourceBytes;
            if (SdkContentExportCache.CanReusePreviousEntry(previousEntries, relativePath, sourceSize, sourceTicks,
                    sourceEntry.SourceSha256,
                    destinationAbsolutePath,
                    out var previousSha))
            {
                entriesByPath[relativePath] = new SdkContentManifestEntry
                {
                    relativePath = relativePath,
                    sha256 = previousSha,
                    bytes = sourceSize,
                    sourceLastWriteUtcTicks = sourceTicks
                };
                result.UnchangedFileCount++;
                return;
            }

            var destinationDir = Path.GetDirectoryName(destinationAbsolutePath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
                Directory.CreateDirectory(destinationDir);

            File.Copy(sourceAbsolutePath, destinationAbsolutePath, true);

            entriesByPath[relativePath] = new SdkContentManifestEntry
            {
                relativePath = relativePath,
                sha256 = sourceEntry.SourceSha256,
                bytes = sourceSize,
                sourceLastWriteUtcTicks = sourceTicks
            };
            result.CopiedFileCount++;
        }

        private static void DeleteStaleOutputFiles(string contentRootAbsolute, ISet<string> sourcePaths,
            ExportResult result)
        {
            if (!Directory.Exists(contentRootAbsolute))
                return;

            foreach (var file in Directory.EnumerateFiles(contentRootAbsolute, "*", SearchOption.AllDirectories))
            {
                if (SdkContentPathUtility.ShouldSkipFile(file))
                    continue;

                var relative = Path.GetRelativePath(contentRootAbsolute, file).Replace('\\', '/');
                if (!relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(relative, Constants.SdkContentManifestFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(relative, ExportStampFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (sourcePaths.Contains(relative))
                    continue;

                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    result.DeletedFileCount++;
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Failed to delete stale SDK content file '{relative}': {ex.Message}");
                }
            }
        }

        [Serializable]
        private sealed class ExportStamp
        {
            public string exportedAtUtc;
            public string unityVersion;
            public string productName;
            public int schemaVersion;
            public int entryCount;
            public int sourceFileCount;
            public string sourceFingerprint;
        }

        internal sealed class ExportResult
        {
            internal List<string> Errors { get; } = new();
            internal string OutputRootPath { get; set; } = string.Empty;
            internal string ContentRootPath { get; set; } = string.Empty;
            internal List<string> Warnings { get; } = new();
            internal int CopiedFileCount { get; set; }
            internal int UnchangedFileCount { get; set; }
            internal int DeletedFileCount { get; set; }
            internal int EntryCount { get; set; }
            internal int SourceFileCount { get; set; }
            internal string SourceFingerprint { get; set; } = string.Empty;
            internal int ProxiedMaterialCount { get; private set; }
            internal int ProxiedMaterialCopiedCount { get; private set; }
            internal int ProxiedMaterialUnchangedCount { get; private set; }
            internal int ProxyCacheHitCount { get; set; }
            internal int ProxyCacheMissCount { get; set; }
            internal int GeneratedProxyPayloadCount { get; set; }
            internal int ProxyBypassedManagedShaderCount { get; set; }
            internal List<string> ProxiedMaterialSamples { get; } = new();
            internal bool Succeeded => Errors.Count == 0;

            internal void RecordProxiedMaterial(string relativePath, bool copied)
            {
                ProxiedMaterialCount++;
                if (copied)
                    ProxiedMaterialCopiedCount++;
                else
                    ProxiedMaterialUnchangedCount++;

                if (!string.IsNullOrWhiteSpace(relativePath) &&
                    ProxiedMaterialSamples.Count < 10 &&
                    !ProxiedMaterialSamples.Contains(relativePath))
                    ProxiedMaterialSamples.Add(relativePath);
            }
        }

        internal sealed class ValidationResult
        {
            internal List<string> Errors { get; } = new();
            internal string OutputRootPath { get; set; } = string.Empty;
            internal string ContentRootPath { get; set; } = string.Empty;
            internal List<string> Warnings { get; } = new();
            internal int ManifestEntryCount { get; set; }
            internal int ValidatedFileCount { get; set; }
            internal int SourceFileCount { get; set; }
            internal string SourceFingerprint { get; set; } = string.Empty;
            internal bool Succeeded => Errors.Count == 0;
        }
    }

    internal static class SdkMaterialProxyExporter
    {
        private const string ConfigProjectRelativePath = "ProjectSettings/VellocetSdkMaterialProxy.json";
        private const string TempScratchFolder = Constants.TempSceneDir + "/MaterialProxyScratch";
        private const string InternalErrorShaderName = "Hidden/InternalErrorShader";
        private const int ProxyAlgorithmVersion = 4;
        private const string CacheRelativeRoot = "Library/VellocetSdk/MaterialProxyCache";

        private static readonly string[] DefaultFallbackShaderNames =
        {
            "Universal Render Pipeline/Lit",
            "Standard"
        };

        private static readonly string[] DefaultAllowShaderNames =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
            "Standard",
            "Standard (Specular setup)",
            "Sprites/Default",
            "UI/Default",
            "TextMeshPro/Distance Field",
            "TextMeshPro/Mobile/Distance Field"
        };

        private static readonly string[] DefaultAllowShaderPathPrefixes =
        {
            "Packages/com.unity.",
            "Packages/com.vellocet.sdk/"
        };

        private static readonly string[] DefaultForceProxyShaderPathPrefixes =
        {
            "Assets/"
        };

        internal static readonly string[] BaseMapSourceCandidates =
        {
            "_BaseMap",
            "_Basemap",
            "_MainTex",
            "_MainTexture",
            "_mainTexture",
            "_AlbedoMap",
            "_Albedo",
            "_BaseTexture",
            "_BaseTex",
            "_ColorMap",
            "_DiffuseMap",
            "_Texture0",
            "_MainText"
        };

        private static readonly string[] NormalMapSourceCandidates =
        {
            "_BumpMap",
            "_NormalMap",
            "_Normal",
            "_NormalTexture"
        };

        private static readonly string[] MetallicMapSourceCandidates =
        {
            "_MetallicGlossMap",
            "_MetallicMap",
            "_MaskMap",
            "_Mask",
            "_METALNESS_MAP"
        };

        private static readonly string[] OcclusionMapSourceCandidates =
        {
            "_OcclusionMap",
            "_AOMap",
            "_Occlusion",
            "_AmbientOcclusionMap"
        };

        private static readonly string[] EmissionMapSourceCandidates =
        {
            "_EmissionMap",
            "_EmissiveMap",
            "_EmissiveTexture"
        };

        private static readonly string[] NonBaseMapNameHints =
        {
            "normal",
            "bump",
            "metal",
            "smooth",
            "rough",
            "spec",
            "mask",
            "occlusion",
            "ao",
            "emiss",
            "height",
            "parallax",
            "detail",
            "shadow",
            "hatch",
            "cube",
            "reflection",
            "cloud",
            "noise"
        };

        private static readonly string[] PreferredBaseMapNameHints =
        {
            "base",
            "albedo",
            "main",
            "diffuse",
            "color"
        };

        private static MaterialProxyConfig _sessionConfig;
        private static Shader _sessionFallbackShader;
        private static string _sessionFallbackShaderName;
        private static string _sessionConfigFingerprint;
        private static bool _sessionInitialized;
        private static int _sessionCacheHitCount;
        private static int _sessionCacheMissCount;
        private static int _sessionGeneratedProxyCount;
        private static int _sessionBypassedManagedShaderCount;

        private static readonly Dictionary<string, ProxyMaterialPayload> SessionPayloadCache =
            new(StringComparer.OrdinalIgnoreCase);

        internal static void BeginSession(List<string> warnings)
        {
            _sessionConfig = LoadConfig(warnings);
            _sessionFallbackShader = ResolveFallbackShader(_sessionConfig, warnings, out _sessionFallbackShaderName);
            _sessionConfigFingerprint = ComputeConfigFingerprint(_sessionConfig);
            _sessionCacheHitCount = 0;
            _sessionCacheMissCount = 0;
            _sessionGeneratedProxyCount = 0;
            _sessionBypassedManagedShaderCount = 0;
            SessionPayloadCache.Clear();

            if (_sessionConfig.EnableProxyCache)
                EnsureCacheRootExists();

            _sessionInitialized = true;
        }

        internal static void EndSession()
        {
            _sessionConfig = null;
            _sessionFallbackShader = null;
            _sessionFallbackShaderName = string.Empty;
            _sessionConfigFingerprint = string.Empty;
            _sessionInitialized = false;
            SessionPayloadCache.Clear();

            if (AssetDatabase.IsValidFolder(TempScratchFolder))
                AssetDatabase.DeleteAsset(TempScratchFolder);

            if (AssetDatabase.IsValidFolder(Constants.TempSceneDir))
            {
                var tempRootAbsolute = SdkContentPathUtility.ToAbsolutePath(Constants.TempSceneDir);
                if (Directory.Exists(tempRootAbsolute) &&
                    !Directory.EnumerateFileSystemEntries(tempRootAbsolute).Any())
                    AssetDatabase.DeleteAsset(Constants.TempSceneDir);
            }
        }

        internal static SessionStats GetSessionStats()
        {
            return new SessionStats(_sessionCacheHitCount, _sessionCacheMissCount, _sessionGeneratedProxyCount,
                _sessionBypassedManagedShaderCount);
        }

        internal static bool TryBuildProxyMaterial(string relativePath, out ProxyMaterialPayload payload,
            out string error)
        {
            payload = default;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(relativePath) ||
                !relativePath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!_sessionInitialized)
                BeginSession(null);

            var sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(relativePath);
            if (!sourceMaterial)
                return true;

            if (!ShouldProxyMaterial(sourceMaterial, _sessionConfig, out var sourceShaderName))
                return true;

            if (!_sessionFallbackShader)
            {
                error =
                    $"Material proxy export failed for '{relativePath}': no fallback shader could be resolved. Configure '{ConfigProjectRelativePath}'.";
                return false;
            }

            if (SessionPayloadCache.TryGetValue(relativePath, out payload) && payload.HasValue)
                return true;

            var cacheKey = BuildCacheKey(relativePath, sourceMaterial, sourceShaderName);
            if (_sessionConfig.EnableProxyCache &&
                TryReadCachedPayload(cacheKey, out var cachedBytes))
            {
                payload = new ProxyMaterialPayload(cachedBytes, sourceShaderName, _sessionFallbackShaderName);
                SessionPayloadCache[relativePath] = payload;
                _sessionCacheHitCount++;
                return true;
            }

            if (!TryCreateProxyBytes(sourceMaterial, relativePath, _sessionFallbackShader, out var proxyBytes,
                    out error))
                return false;

            payload = new ProxyMaterialPayload(proxyBytes, sourceShaderName, _sessionFallbackShaderName);
            SessionPayloadCache[relativePath] = payload;
            _sessionCacheMissCount++;
            _sessionGeneratedProxyCount++;
            if (_sessionConfig.EnableProxyCache)
                WriteCachedPayload(cacheKey, proxyBytes);
            return true;
        }

        private static bool TryCreateProxyBytes(Material sourceMaterial, string relativePath, Shader fallbackShader,
            out byte[] bytes, out string error)
        {
            bytes = null;
            error = string.Empty;

            var proxyName = Path.GetFileNameWithoutExtension(relativePath);
            if (string.IsNullOrWhiteSpace(proxyName))
                proxyName = sourceMaterial.name;

            var proxyMaterial = new Material(fallbackShader)
            {
                name = proxyName
            };

            try
            {
                if (!TryConfigureProxyMaterial(sourceMaterial, proxyMaterial, relativePath, out error))
                    return false;

                if (!TrySerializeMaterial(proxyMaterial, relativePath, out bytes, out error))
                    return false;

                return true;
            }
            finally
            {
                if (proxyMaterial)
                    Object.DestroyImmediate(proxyMaterial);
            }
        }

        internal static bool TryConfigureProxyMaterial(Material source, Material proxy, string relativePath,
            out string error)
        {
            error = string.Empty;
            if (!source || !source.shader || !proxy || !proxy.shader)
            {
                error =
                    $"Material proxy export failed for '{relativePath}': source and fallback materials must have valid shaders.";
                return false;
            }

            if (!_sessionInitialized)
                BeginSession(null);

            var proxyOverride = FindProxyOverride(source);

            // Do not bulk-copy source shader properties into fallback shader materials.
            // Cross-shader property blobs can include unsupported fields and break serialization.
            proxy.renderQueue = source.renderQueue;
            proxy.enableInstancing = source.enableInstancing;
            proxy.doubleSidedGI = source.doubleSidedGI;
            proxy.globalIlluminationFlags = source.globalIlluminationFlags;
            proxy.shaderKeywords = Array.Empty<string>();
            SanitizeKeywords(proxy);

            CopyTextureAlias(source, proxy, "_BaseMap", BaseMapSourceCandidates);
            CopyColorAlias(source, proxy, "_BaseColor", "_AlbedoTint", "_BaseColor", "_Color", "_Tint", "_MainColor");
            CopyTextureAlias(source, proxy, "_BumpMap", NormalMapSourceCandidates);
            CopyFloatAlias(source, proxy, "_BumpScale", "_BumpScale", "_NormalScale");
            CopyTextureAlias(source, proxy, "_MetallicGlossMap", MetallicMapSourceCandidates);
            CopyFloatAlias(source, proxy, "_Metallic", "_Metallic", "_Metalness");
            CopyFloatAlias(source, proxy, "_Smoothness", "_Smoothness", "_Glossiness", "_Gloss");
            CopyTextureAlias(source, proxy, "_OcclusionMap", OcclusionMapSourceCandidates);
            CopyFloatAlias(source, proxy, "_OcclusionStrength", "_OcclusionStrength", "_AOStrength");
            CopyTextureAlias(source, proxy, "_EmissionMap", EmissionMapSourceCandidates);
            CopyColorAlias(source, proxy, "_EmissionColor", "_EmissionColor", "_EmissiveColor", "_Emission");
            CopyFloatAlias(source, proxy, "_Cutoff", "_Cutoff", "_AlphaCutoff");

            // Populate properties used by the Standard fallback shader.
            CopyTextureAlias(source, proxy, "_MainTex", BaseMapSourceCandidates);
            CopyColorAlias(source, proxy, "_Color", "_Color", "_BaseColor", "_Tint", "_MainColor");

            if (IsFloatProperty(proxy, "_Surface"))
            {
                var isTransparent = IsFloatProperty(source, "_Surface")
                    ? source.GetFloat("_Surface") > 0.5f
                    : source.renderQueue >= (int)RenderQueue.Transparent;
                proxy.SetFloat("_Surface", isTransparent ? 1f : 0f);
            }

            if (IsFloatProperty(proxy, "_AlphaClip"))
            {
                var alphaClip = IsFloatProperty(source, "_AlphaClip")
                    ? source.GetFloat("_AlphaClip") >= 0.5f
                    : source.IsKeywordEnabled("_ALPHATEST_ON") ||
                      source.GetTag("RenderType", false) == "TransparentCutout";
                proxy.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            }

            if (proxy.HasProperty("_BaseMap") && !proxy.GetTexture("_BaseMap"))
            {
                if (TryFindLikelyBaseTexture(source, out var texture, out var sourceProperty))
                {
                    proxy.SetTexture("_BaseMap", texture);
                    proxy.SetTextureScale("_BaseMap", source.GetTextureScale(sourceProperty));
                    proxy.SetTextureOffset("_BaseMap", source.GetTextureOffset(sourceProperty));
                }
                else if (source.mainTexture)
                {
                    proxy.SetTexture("_BaseMap", source.mainTexture);
                    proxy.SetTextureScale("_BaseMap", source.mainTextureScale);
                    proxy.SetTextureOffset("_BaseMap", source.mainTextureOffset);
                }
            }

            if (proxy.HasProperty("_MainTex") && !proxy.GetTexture("_MainTex") && proxy.HasProperty("_BaseMap"))
            {
                var baseMap = proxy.GetTexture("_BaseMap");
                if (baseMap)
                {
                    proxy.SetTexture("_MainTex", baseMap);
                    proxy.SetTextureScale("_MainTex", proxy.GetTextureScale("_BaseMap"));
                    proxy.SetTextureOffset("_MainTex", proxy.GetTextureOffset("_BaseMap"));
                }
            }

            if (!TryApplyProxyOverride(proxy, proxyOverride, relativePath, out error))
                return false;

            NormalizeEmissionKeyword(proxy);
            NormalizeProxyMaterial(proxy);
            return true;
        }

        internal static void NormalizeProxyMaterial(Material material)
        {
            if (!material || !material.shader)
                return;

            // Match the derived state URP's inspector/postprocessor would otherwise write after distribution.
            switch (material.shader.name)
            {
                case "Universal Render Pipeline/Lit":
                    BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
                    break;
                case "Universal Render Pipeline/Simple Lit":
                    BaseShaderGUI.SetMaterialKeywords(material, SimpleLitGUI.SetMaterialKeywords);
                    break;
                case "Universal Render Pipeline/Unlit":
                    BaseShaderGUI.SetMaterialKeywords(material);
                    break;
                default:
                    SanitizeKeywords(material);
                    break;
            }
        }

        private static ProxyMaterialOverride FindProxyOverride(Material source)
        {
            if (!source || !source.shader || _sessionConfig?.ProxyOverrides == null ||
                _sessionConfig.ProxyOverrides.Count == 0)
                return null;

            var shaderPath = NormalizePath(AssetDatabase.GetAssetPath(source.shader));
            if (string.IsNullOrWhiteSpace(shaderPath))
                return null;

            for (var i = 0; i < _sessionConfig.ProxyOverrides.Count; i++)
            {
                var candidate = _sessionConfig.ProxyOverrides[i];
                if (candidate != null && string.Equals(candidate.SourceShaderPath, shaderPath,
                        StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return null;
        }

        private static bool TryApplyProxyOverride(Material proxy, ProxyMaterialOverride proxyOverride,
            string relativePath, out string error)
        {
            error = string.Empty;
            if (proxyOverride == null)
                return true;

            var baseMapAssetPath = NormalizePath(proxyOverride.BaseMapAssetPath);
            if (string.IsNullOrWhiteSpace(baseMapAssetPath) ||
                !SdkContentPathUtility.IsSourceSdkAssetPath(baseMapAssetPath) ||
                !SdkContentPathUtility.TryMapSourceToInstalledPath(baseMapAssetPath, out _))
            {
                error =
                    $"Material proxy export failed for '{relativePath}': proxy override for shader " +
                    $"'{proxyOverride.SourceShaderPath}' must use an SDK-managed base map, got " +
                    $"'{proxyOverride.BaseMapAssetPath}'.";
                return false;
            }

            var baseMap = AssetDatabase.LoadAssetAtPath<Texture>(baseMapAssetPath);
            if (!baseMap)
            {
                error =
                    $"Material proxy export failed for '{relativePath}': proxy override base map could not be " +
                    $"loaded at '{baseMapAssetPath}'.";
                return false;
            }

            var assignedBaseMap = false;
            if (IsTextureProperty(proxy, "_BaseMap"))
            {
                proxy.SetTexture("_BaseMap", baseMap);
                proxy.SetTextureScale("_BaseMap", Vector2.one);
                proxy.SetTextureOffset("_BaseMap", Vector2.zero);
                assignedBaseMap = true;
            }

            if (IsTextureProperty(proxy, "_MainTex"))
            {
                proxy.SetTexture("_MainTex", baseMap);
                proxy.SetTextureScale("_MainTex", Vector2.one);
                proxy.SetTextureOffset("_MainTex", Vector2.zero);
                assignedBaseMap = true;
            }

            if (!assignedBaseMap)
            {
                error =
                    $"Material proxy export failed for '{relativePath}': fallback shader '{proxy.shader.name}' " +
                    "does not expose a supported base-map property.";
                return false;
            }

            if (proxyOverride.ClearSourceNormalMap && IsTextureProperty(proxy, "_BumpMap"))
            {
                proxy.SetTexture("_BumpMap", null);
                if (IsFloatProperty(proxy, "_BumpScale"))
                    proxy.SetFloat("_BumpScale", 1f);
            }

            if (proxyOverride.ForceTransparent)
            {
                if (IsFloatProperty(proxy, "_Surface"))
                    proxy.SetFloat("_Surface", 1f);

                proxy.renderQueue = (int)RenderQueue.Transparent;
            }

            if (proxyOverride.OverrideBaseColorAlpha)
            {
                ApplyColorAlpha(proxy, "_BaseColor", proxyOverride.BaseColorAlpha);
                ApplyColorAlpha(proxy, "_Color", proxyOverride.BaseColorAlpha);
            }

            return true;
        }

        private static void ApplyColorAlpha(Material material, string propertyName, float alpha)
        {
            if (!IsColorProperty(material, propertyName))
                return;

            var color = material.GetColor(propertyName);
            color.a = Mathf.Clamp01(alpha);
            material.SetColor(propertyName, color);
        }

        private static void NormalizeEmissionKeyword(Material proxy)
        {
            var hasEmissionMap = proxy.HasProperty("_EmissionMap") && proxy.GetTexture("_EmissionMap");
            var hasEmissionColor = proxy.HasProperty("_EmissionColor") &&
                                   proxy.GetColor("_EmissionColor").maxColorComponent > 0.0001f;

            if (hasEmissionMap || hasEmissionColor)
                proxy.EnableKeyword("_EMISSION");
            else
                proxy.DisableKeyword("_EMISSION");
        }

        private static void CopyTextureAlias(Material source, Material proxy, string targetProperty,
            params string[] sourceCandidates)
        {
            if (!IsTextureProperty(proxy, targetProperty))
                return;

            for (var i = 0; i < sourceCandidates.Length; i++)
            {
                var sourceProperty = sourceCandidates[i];
                if (!IsTextureProperty(source, sourceProperty))
                    continue;

                var texture = source.GetTexture(sourceProperty);
                if (!texture)
                    continue;

                proxy.SetTexture(targetProperty, texture);
                proxy.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
                proxy.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
                return;
            }
        }

        private static bool TryFindLikelyBaseTexture(Material source, out Texture texture, out string sourceProperty)
        {
            texture = null;
            sourceProperty = string.Empty;
            if (!source)
                return false;

            if (TryGetTextureFromCandidates(source, BaseMapSourceCandidates, out texture, out sourceProperty))
                return true;

            var textureProperties = source.GetTexturePropertyNames();
            if (textureProperties == null || textureProperties.Length == 0)
                return false;

            // First pass: prefer albedo-like names and reject known non-base maps.
            for (var i = 0; i < textureProperties.Length; i++)
            {
                var candidate = textureProperties[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (!IsTextureProperty(source, candidate))
                    continue;

                var candidateTexture = source.GetTexture(candidate);
                if (!candidateTexture)
                    continue;

                if (HasAnyNameHint(candidate, NonBaseMapNameHints))
                    continue;

                if (HasAnyNameHint(candidate, PreferredBaseMapNameHints))
                {
                    texture = candidateTexture;
                    sourceProperty = candidate;
                    return true;
                }
            }

            // Second pass: first non-null texture that does not look like a non-base map.
            for (var i = 0; i < textureProperties.Length; i++)
            {
                var candidate = textureProperties[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (!IsTextureProperty(source, candidate))
                    continue;

                var candidateTexture = source.GetTexture(candidate);
                if (!candidateTexture)
                    continue;

                if (HasAnyNameHint(candidate, NonBaseMapNameHints))
                    continue;

                texture = candidateTexture;
                sourceProperty = candidate;
                return true;
            }

            return false;
        }

        private static bool TryGetTextureFromCandidates(Material source, IReadOnlyList<string> candidates,
            out Texture texture, out string sourceProperty)
        {
            texture = null;
            sourceProperty = string.Empty;
            if (!source || candidates == null || candidates.Count == 0)
                return false;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (!IsTextureProperty(source, candidate))
                    continue;

                var candidateTexture = source.GetTexture(candidate);
                if (!candidateTexture)
                    continue;

                texture = candidateTexture;
                sourceProperty = candidate;
                return true;
            }

            return false;
        }

        private static bool HasAnyNameHint(string propertyName, IReadOnlyList<string> hints)
        {
            if (string.IsNullOrWhiteSpace(propertyName) || hints == null || hints.Count == 0)
                return false;

            for (var i = 0; i < hints.Count; i++)
            {
                var hint = hints[i];
                if (string.IsNullOrWhiteSpace(hint))
                    continue;

                if (propertyName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static void CopyColorAlias(Material source, Material proxy, string targetProperty,
            params string[] sourceCandidates)
        {
            if (!IsColorProperty(proxy, targetProperty))
                return;

            for (var i = 0; i < sourceCandidates.Length; i++)
            {
                var sourceProperty = sourceCandidates[i];
                if (!TryGetPropertyType(source, sourceProperty, out var propertyType))
                    continue;

                if (propertyType == ShaderPropertyType.Color)
                {
                    proxy.SetColor(targetProperty, source.GetColor(sourceProperty));
                    return;
                }

                if (propertyType == ShaderPropertyType.Vector)
                {
                    var value = source.GetVector(sourceProperty);
                    proxy.SetColor(targetProperty, new Color(value.x, value.y, value.z, value.w));
                    return;
                }
            }
        }

        private static void CopyFloatAlias(Material source, Material proxy, string targetProperty,
            params string[] sourceCandidates)
        {
            if (!IsFloatProperty(proxy, targetProperty))
                return;

            for (var i = 0; i < sourceCandidates.Length; i++)
            {
                var sourceProperty = sourceCandidates[i];
                if (!IsFloatProperty(source, sourceProperty))
                    continue;

                proxy.SetFloat(targetProperty, source.GetFloat(sourceProperty));
                return;
            }
        }

        private static bool IsTextureProperty(Material material, string propertyName)
        {
            return TryGetPropertyType(material, propertyName, out var propertyType) &&
                   propertyType == ShaderPropertyType.Texture;
        }

        private static bool IsColorProperty(Material material, string propertyName)
        {
            return TryGetPropertyType(material, propertyName, out var propertyType) &&
                   (propertyType == ShaderPropertyType.Color || propertyType == ShaderPropertyType.Vector);
        }

        private static bool IsFloatProperty(Material material, string propertyName)
        {
            return TryGetPropertyType(material, propertyName, out var propertyType) &&
                   (propertyType == ShaderPropertyType.Float || propertyType == ShaderPropertyType.Range);
        }

        private static bool TryGetPropertyType(Material material, string propertyName,
            out ShaderPropertyType propertyType)
        {
            propertyType = default;
            if (!material || !material.shader || string.IsNullOrWhiteSpace(propertyName))
                return false;

            var propertyIndex = material.shader.FindPropertyIndex(propertyName);
            if (propertyIndex < 0)
                return false;

            propertyType = material.shader.GetPropertyType(propertyIndex);
            return true;
        }

        private static bool TrySerializeMaterial(Material material, string relativePath, out byte[] bytes,
            out string error)
        {
            bytes = null;
            error = string.Empty;

            if (!EnsureScratchFolder())
            {
                error = $"Material proxy export failed: could not create scratch folder '{TempScratchFolder}'.";
                return false;
            }

            var fileName = Path.GetFileName(relativePath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "ProxyMaterial.mat";

            var tempAssetPath = $"{TempScratchFolder}/{fileName}";
            if (AssetDatabase.LoadMainAssetAtPath(tempAssetPath))
                AssetDatabase.DeleteAsset(tempAssetPath);

            var createdAsset = false;
            try
            {
                AssetDatabase.CreateAsset(material, tempAssetPath);
                createdAsset = true;

                var absolutePath = SdkContentPathUtility.ToAbsolutePath(tempAssetPath);
                if (!File.Exists(absolutePath))
                {
                    error = $"Material proxy export failed: scratch material file missing '{absolutePath}'.";
                    return false;
                }

                bytes = File.ReadAllBytes(absolutePath);
                return bytes is { Length: > 0 };
            }
            catch (Exception ex)
            {
                error = $"Material proxy export failed while serializing '{relativePath}': {ex.Message}";
                return false;
            }
            finally
            {
                if (createdAsset)
                    AssetDatabase.DeleteAsset(tempAssetPath);
            }
        }

        private static void SanitizeKeywords(Material material)
        {
            if (!material || !material.shader)
                return;

            var currentKeywords = material.shaderKeywords;
            if (currentKeywords == null || currentKeywords.Length == 0)
                return;

            var allowed = material.shader.keywordSpace.keywords
                .Select(keyword => keyword.name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.Ordinal);

            if (allowed.Count == 0)
            {
                material.shaderKeywords = Array.Empty<string>();
                return;
            }

            var filtered = currentKeywords
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword) && allowed.Contains(keyword))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (filtered.Length != currentKeywords.Length ||
                filtered.Where((keyword, index) =>
                        !string.Equals(keyword, currentKeywords[index], StringComparison.Ordinal))
                    .Any())
                material.shaderKeywords = filtered;
        }

        private static string BuildCacheKey(string relativePath, Material sourceMaterial, string sourceShaderName)
        {
            var normalizedPath = NormalizePath(relativePath);
            var sourceFingerprint = BuildAssetFingerprint(normalizedPath);
            var parentFingerprint = BuildParentFingerprint(sourceMaterial);
            var proxyOverrideFingerprint = BuildProxyOverrideDependencyFingerprint(sourceMaterial);
            var keyRaw =
                $"{ProxyAlgorithmVersion}|{normalizedPath}|{sourceShaderName}|{_sessionFallbackShaderName}|{_sessionConfigFingerprint}|{sourceFingerprint}|{parentFingerprint}|{proxyOverrideFingerprint}";
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(keyRaw);
            var hashBytes = sha.ComputeHash(bytes);
            return ToLowerHex(hashBytes);
        }

        private static string BuildAssetFingerprint(string relativeAssetPath)
        {
            if (string.IsNullOrWhiteSpace(relativeAssetPath))
                return "missing";

            var absolutePath = SdkContentPathUtility.ToAbsolutePath(relativeAssetPath);
            if (!File.Exists(absolutePath))
                return "missing";

            var info = new FileInfo(absolutePath);
            return $"{info.LastWriteTimeUtc.Ticks}:{info.Length}";
        }

        private static string BuildParentFingerprint(Material sourceMaterial)
        {
            if (!sourceMaterial || !sourceMaterial.parent)
                return "none";

            var parentPath = NormalizePath(AssetDatabase.GetAssetPath(sourceMaterial.parent));
            if (string.IsNullOrWhiteSpace(parentPath))
                return "none";

            var parentGuid = AssetDatabase.AssetPathToGUID(parentPath);
            var parentAssetFingerprint = BuildAssetFingerprint(parentPath);
            return $"{parentGuid}:{parentAssetFingerprint}";
        }

        private static string BuildProxyOverrideDependencyFingerprint(Material sourceMaterial)
        {
            var proxyOverride = FindProxyOverride(sourceMaterial);
            if (proxyOverride == null)
                return "none";

            var assetPath = NormalizePath(proxyOverride.BaseMapAssetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return "missing";

            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var assetFingerprint = BuildAssetFingerprint(assetPath);
            var metaFingerprint = BuildAssetFingerprint(assetPath + ".meta");
            return $"{assetPath}:{assetGuid}:{assetFingerprint}:{metaFingerprint}";
        }

        private static string GetCacheRootAbsolute()
        {
            return Path.Combine(SdkContentPathUtility.GetProjectRoot(),
                CacheRelativeRoot.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureCacheRootExists()
        {
            var root = GetCacheRootAbsolute();
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);
        }

        private static bool TryReadCachedPayload(string cacheKey, out byte[] payloadBytes)
        {
            payloadBytes = null;
            if (string.IsNullOrWhiteSpace(cacheKey))
                return false;

            var root = GetCacheRootAbsolute();
            var path = Path.Combine(root, cacheKey + ".bin");
            if (!File.Exists(path))
                return false;

            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0)
                    return false;

                payloadBytes = bytes;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteCachedPayload(string cacheKey, byte[] payloadBytes)
        {
            if (string.IsNullOrWhiteSpace(cacheKey) || payloadBytes == null || payloadBytes.Length == 0)
                return;

            try
            {
                EnsureCacheRootExists();
                var root = GetCacheRootAbsolute();
                var path = Path.Combine(root, cacheKey + ".bin");
                var tempPath = path + ".tmp";
                File.WriteAllBytes(tempPath, payloadBytes);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tempPath, path);
            }
            catch
            {
                // Cache failures must never block export.
            }
        }

        private static string ComputeConfigFingerprint(MaterialProxyConfig config)
        {
            if (config == null)
                return "missing";

            var proxyOverrides = config.ProxyOverrides
                .OrderBy(value => value.SourceShaderPath, StringComparer.OrdinalIgnoreCase)
                .Select(value => string.Join(":",
                    value.SourceShaderPath,
                    value.BaseMapAssetPath,
                    value.ClearSourceNormalMap ? "1" : "0",
                    value.ForceTransparent ? "1" : "0",
                    value.OverrideBaseColorAlpha ? "1" : "0",
                    value.BaseColorAlpha.ToString("R", CultureInfo.InvariantCulture)));
            var raw = string.Join("|",
                string.Join(",", config.FallbackShaderNames.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                string.Join(",", config.AllowShaderNames.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                string.Join(",",
                    config.AllowShaderPathPrefixes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                string.Join(",",
                    config.ForceProxyShaderPathPrefixes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                string.Join(",", proxyOverrides),
                config.AllowUnknownBuiltInShaders ? "1" : "0",
                config.EnableProxyCache ? "1" : "0");
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(raw);
            return ToLowerHex(sha.ComputeHash(bytes));
        }

        private static string ToLowerHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var chars = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (var i = 0; i < bytes.Length; i++)
            {
                var value = bytes[i];
                chars[i * 2] = hex[value >> 4];
                chars[i * 2 + 1] = hex[value & 0x0F];
            }

            return new string(chars);
        }

        private static bool EnsureScratchFolder()
        {
            return EnsureAssetFolder(Constants.TempSceneDir) && EnsureAssetFolder(TempScratchFolder);
        }

        private static bool EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return false;

            var normalized = folderPath.Replace('\\', '/').Trim().TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
                return true;

            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
                return false;

            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var child = segments[i];
                var candidate = current + "/" + child;
                if (!AssetDatabase.IsValidFolder(candidate))
                    AssetDatabase.CreateFolder(current, child);
                current = candidate;
            }

            return AssetDatabase.IsValidFolder(normalized);
        }

        private static bool ShouldProxyMaterial(Material sourceMaterial, MaterialProxyConfig config,
            out string sourceShaderName)
        {
            var shader = sourceMaterial.shader;
            sourceShaderName = shader ? shader.name : "<missing>";
            if (!shader)
                return true;

            if (string.Equals(sourceShaderName, InternalErrorShaderName, StringComparison.Ordinal))
                return true;

            var shaderPath = NormalizePath(AssetDatabase.GetAssetPath(shader));
            // If the shader asset itself is exported as managed SDK content, keep the material as-is.
            // Proxying is only needed when shader compatibility cannot be guaranteed at runtime.
            if (!string.IsNullOrWhiteSpace(shaderPath) && SdkContentPathUtility.IsSdkAssetPath(shaderPath))
            {
                _sessionBypassedManagedShaderCount++;
                return false;
            }

            if (MatchesAnyPrefix(shaderPath, config.ForceProxyShaderPathPrefixes))
                return true;

            if (config.AllowShaderNames.Contains(sourceShaderName))
                return false;

            if (MatchesAnyPrefix(shaderPath, config.AllowShaderPathPrefixes))
                return false;

            if (string.IsNullOrWhiteSpace(shaderPath))
                return !config.AllowUnknownBuiltInShaders;

            return true;
        }

        private static Shader ResolveFallbackShader(MaterialProxyConfig config, List<string> warnings,
            out string selectedShaderName)
        {
            selectedShaderName = string.Empty;
            if (config == null || config.FallbackShaderNames.Count == 0)
                return null;

            foreach (var shaderName in config.FallbackShaderNames)
            {
                if (string.IsNullOrWhiteSpace(shaderName))
                    continue;

                var shader = Shader.Find(shaderName);
                if (!shader)
                    continue;

                selectedShaderName = shaderName;
                return shader;
            }

            warnings?.Add(
                $"Material proxy fallback shader could not be resolved. Checked: {string.Join(", ", config.FallbackShaderNames)}.");
            return null;
        }

        private static MaterialProxyConfig LoadConfig(List<string> warnings)
        {
            var config = MaterialProxyConfig.CreateDefault();
            var configAbsolute = Path.Combine(SdkContentPathUtility.GetProjectRoot(),
                ConfigProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(configAbsolute))
                return config;

            try
            {
                var json = File.ReadAllText(configAbsolute);
                var parsed = JsonUtility.FromJson<MaterialProxyConfigJson>(json);
                if (parsed == null)
                {
                    warnings?.Add(
                        $"Material proxy config is invalid JSON at '{ConfigProjectRelativePath}'. Using defaults.");
                    return config;
                }

                config.Apply(parsed);
                return config;
            }
            catch (Exception ex)
            {
                warnings?.Add(
                    $"Failed to read material proxy config '{ConfigProjectRelativePath}'. Using defaults. ({ex.Message})");
                return config;
            }
        }

        private static bool MatchesAnyPrefix(string value, IReadOnlyList<string> prefixes)
        {
            if (string.IsNullOrWhiteSpace(value) || prefixes == null || prefixes.Count == 0)
                return false;

            for (var i = 0; i < prefixes.Count; i++)
            {
                var prefix = prefixes[i];
                if (string.IsNullOrWhiteSpace(prefix))
                    continue;

                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim();
        }

        [Serializable]
        private sealed class MaterialProxyConfigJson
        {
            public string[] fallbackShaderNames;
            public string[] allowShaderNames;
            public string[] allowShaderPathPrefixes;
            public string[] forceProxyShaderPathPrefixes;
            public ProxyMaterialOverrideJson[] proxyOverrides;
            public bool allowUnknownBuiltInShaders = true;
            public bool enableProxyCache = true;
        }

        [Serializable]
        private sealed class ProxyMaterialOverrideJson
        {
            public string sourceShaderPath;
            public string baseMapAssetPath;
            public bool clearSourceNormalMap;
            public bool forceTransparent;
            public bool overrideBaseColorAlpha;
            public float baseColorAlpha = 1f;
        }

        private sealed class MaterialProxyConfig
        {
            internal HashSet<string> AllowShaderNames { get; private set; } = new(StringComparer.Ordinal);
            internal List<string> AllowShaderPathPrefixes { get; private set; } = new();
            internal List<string> ForceProxyShaderPathPrefixes { get; private set; } = new();
            internal List<string> FallbackShaderNames { get; private set; } = new();
            internal List<ProxyMaterialOverride> ProxyOverrides { get; private set; } = new();
            internal bool AllowUnknownBuiltInShaders { get; private set; } = true;
            internal bool EnableProxyCache { get; private set; } = true;

            internal static MaterialProxyConfig CreateDefault()
            {
                var config = new MaterialProxyConfig();
                config.FallbackShaderNames = NormalizeValues(DefaultFallbackShaderNames);
                config.AllowShaderNames = new HashSet<string>(NormalizeValues(DefaultAllowShaderNames),
                    StringComparer.Ordinal);
                config.AllowShaderPathPrefixes = NormalizeValues(DefaultAllowShaderPathPrefixes);
                config.ForceProxyShaderPathPrefixes = NormalizeValues(DefaultForceProxyShaderPathPrefixes);
                config.ProxyOverrides = new List<ProxyMaterialOverride>();
                config.AllowUnknownBuiltInShaders = true;
                config.EnableProxyCache = true;
                return config;
            }

            internal void Apply(MaterialProxyConfigJson json)
            {
                if (json == null)
                    return;

                if (json.fallbackShaderNames != null && json.fallbackShaderNames.Length > 0)
                    FallbackShaderNames = NormalizeValues(json.fallbackShaderNames);

                if (json.allowShaderNames != null && json.allowShaderNames.Length > 0)
                    AllowShaderNames = new HashSet<string>(NormalizeValues(json.allowShaderNames),
                        StringComparer.Ordinal);

                if (json.allowShaderPathPrefixes != null && json.allowShaderPathPrefixes.Length > 0)
                    AllowShaderPathPrefixes = NormalizeValues(json.allowShaderPathPrefixes);

                if (json.forceProxyShaderPathPrefixes != null && json.forceProxyShaderPathPrefixes.Length > 0)
                    ForceProxyShaderPathPrefixes = NormalizeValues(json.forceProxyShaderPathPrefixes);

                if (json.proxyOverrides != null)
                    ProxyOverrides = NormalizeOverrides(json.proxyOverrides);

                AllowUnknownBuiltInShaders = json.allowUnknownBuiltInShaders;
                EnableProxyCache = json.enableProxyCache;
            }

            private static List<ProxyMaterialOverride> NormalizeOverrides(
                IEnumerable<ProxyMaterialOverrideJson> values)
            {
                var overridesByShaderPath = new Dictionary<string, ProxyMaterialOverride>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var value in values ?? Array.Empty<ProxyMaterialOverrideJson>())
                {
                    if (value == null)
                        continue;

                    var sourceShaderPath = NormalizePath(value.sourceShaderPath);
                    if (string.IsNullOrWhiteSpace(sourceShaderPath))
                        continue;

                    overridesByShaderPath[sourceShaderPath] = new ProxyMaterialOverride(
                        sourceShaderPath,
                        NormalizePath(value.baseMapAssetPath),
                        value.clearSourceNormalMap,
                        value.forceTransparent,
                        value.overrideBaseColorAlpha,
                        Mathf.Clamp01(value.baseColorAlpha));
                }

                return overridesByShaderPath.Values
                    .OrderBy(value => value.SourceShaderPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            private static List<string> NormalizeValues(IEnumerable<string> values)
            {
                return values?
                    .Select(value => value?.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();
            }
        }

        private sealed class ProxyMaterialOverride
        {
            internal ProxyMaterialOverride(string sourceShaderPath, string baseMapAssetPath,
                bool clearSourceNormalMap, bool forceTransparent, bool overrideBaseColorAlpha,
                float baseColorAlpha)
            {
                SourceShaderPath = sourceShaderPath ?? string.Empty;
                BaseMapAssetPath = baseMapAssetPath ?? string.Empty;
                ClearSourceNormalMap = clearSourceNormalMap;
                ForceTransparent = forceTransparent;
                OverrideBaseColorAlpha = overrideBaseColorAlpha;
                BaseColorAlpha = baseColorAlpha;
            }

            internal string SourceShaderPath { get; }
            internal string BaseMapAssetPath { get; }
            internal bool ClearSourceNormalMap { get; }
            internal bool ForceTransparent { get; }
            internal bool OverrideBaseColorAlpha { get; }
            internal float BaseColorAlpha { get; }
        }

        internal readonly struct ProxyMaterialPayload
        {
            internal ProxyMaterialPayload(byte[] bytes, string sourceShaderName, string fallbackShaderName)
            {
                Bytes = bytes;
                SourceShaderName = sourceShaderName ?? string.Empty;
                FallbackShaderName = fallbackShaderName ?? string.Empty;
            }

            internal byte[] Bytes { get; }
            internal string SourceShaderName { get; }
            internal string FallbackShaderName { get; }
            internal bool HasValue => Bytes is { Length: > 0 };
        }

        internal readonly struct SessionStats
        {
            internal SessionStats(int cacheHitCount, int cacheMissCount, int generatedProxyCount,
                int bypassedManagedShaderCount)
            {
                CacheHitCount = cacheHitCount;
                CacheMissCount = cacheMissCount;
                GeneratedProxyCount = generatedProxyCount;
                BypassedManagedShaderCount = bypassedManagedShaderCount;
            }

            internal int CacheHitCount { get; }
            internal int CacheMissCount { get; }
            internal int GeneratedProxyCount { get; }
            internal int BypassedManagedShaderCount { get; }
        }
    }
}
#endif
