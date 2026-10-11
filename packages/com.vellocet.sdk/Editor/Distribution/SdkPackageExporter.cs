#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Vellocet.Sdk;

namespace Vellocet.Sdk.Editor.Distribution
{
    internal static class SdkPackageExporter
    {
        private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".hg",
            ".svn",
            "bin",
            "Library",
            "obj",
            "Unity.Collections.BurstCompatibilityGen" // Unity's test generator, not its runtime attributes.
        };

        private static readonly HashSet<string> IgnoredFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ".DS_Store",
            "Thumbs.db"
        };

        private static readonly HashSet<string> PackageLiteratureExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".doc",
            ".docx",
            ".epub",
            ".htm",
            ".html",
            ".markdown",
            ".md",
            ".pdf",
            ".rtf"
        };

        internal static PackageExportResult ExportToOutputRoot(string outputRootRepositoryRelative)
        {
            var result = new PackageExportResult
            {
                OutputRootPath = SdkArtifactPathUtility.NormalizeRepositoryRelative(outputRootRepositoryRelative),
                PackageId = Constants.PackageId
            };

            try
            {
                if (string.IsNullOrWhiteSpace(result.OutputRootPath))
                {
                    result.Errors.Add("Output root is required.");
                    return result;
                }

                var outputRootAbsolute = SdkArtifactPathUtility.ToAbsolutePath(result.OutputRootPath);
                SdkArtifactPathUtility.EnsurePathWithinRepository(outputRootAbsolute);
                Directory.CreateDirectory(outputRootAbsolute);

                if (!CleanOutputRoot(outputRootAbsolute, result))
                    return result;

                if (!TryResolvePackageSourceRoot(out var packageSourceAbsolute, out var sourceError))
                {
                    result.Errors.Add(sourceError);
                    return result;
                }

                result.PackageSourceRootPath = SdkArtifactPathUtility.ToRepositoryRelativePath(packageSourceAbsolute);

                var packageJsonAbsolute = Path.Combine(packageSourceAbsolute, "package.json");
                if (!TryReadPackageMetadata(packageJsonAbsolute, result, out var packageVersion))
                    return result;

                if (!ValidateRequiredPackageFiles(packageSourceAbsolute, result))
                    return result;

                result.PackageVersion = packageVersion;

                foreach (var sourceFile in EnumeratePackageFiles(packageSourceAbsolute))
                {
                    var relative = ToRelativePath(packageSourceAbsolute, sourceFile);
                    var destination = Path.Combine(outputRootAbsolute,
                        relative.Replace('/', Path.DirectorySeparatorChar));
                    var destinationDirectory = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(destinationDirectory))
                        Directory.CreateDirectory(destinationDirectory);

                    File.Copy(sourceFile, destination, true);
                    File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(sourceFile));
                    result.CopiedFileCount++;
                }

                if (!File.Exists(Path.Combine(outputRootAbsolute, "package.json")))
                {
                    result.Errors.Add("SDK package export did not produce package.json.");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.ToString());
            }

            return result;
        }

        internal static string BuildSummary(PackageExportResult result)
        {
            if (result == null)
                return "No SDK package export result.";

            var lines = new List<string>
            {
                $"Succeeded: {(result.Succeeded ? "yes" : "no")}",
                $"Output Root: {result.OutputRootPath}",
                $"Package ID: {result.PackageId}",
                $"Package Version: {result.PackageVersion}",
                $"Package License: {result.PackageLicense}",
                $"Required Unity Version: {result.RequiredUnityVersion}",
                $"Package Source Root: {result.PackageSourceRootPath}",
                $"Copied Files: {result.CopiedFileCount}",
                $"Cleaned Output Entries: {result.CleanedOutputEntryCount}",
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

        private static bool TryResolvePackageSourceRoot(out string packageSourceAbsolute, out string error)
        {
            packageSourceAbsolute = string.Empty;
            error = string.Empty;

            var repositoryLocalPackage = SdkArtifactPathUtility.ToAbsolutePath("LocalPackages/" + Constants.PackageId);
            if (Directory.Exists(repositoryLocalPackage))
            {
                packageSourceAbsolute = repositoryLocalPackage;
                return true;
            }

            error = $"Could not resolve SDK package source for {Constants.PackageId}: {repositoryLocalPackage}";
            return false;
        }

        private static bool TryReadPackageMetadata(
            string packageJsonAbsolute,
            PackageExportResult result,
            out string packageVersion)
        {
            packageVersion = "unknown";
            if (!File.Exists(packageJsonAbsolute))
            {
                result.Errors.Add($"SDK package metadata is missing: {packageJsonAbsolute}");
                return false;
            }

            try
            {
                var json = File.ReadAllText(packageJsonAbsolute);
                var packageInfo = JsonUtility.FromJson<PackageManifest>(json);
                if (packageInfo == null)
                {
                    result.Errors.Add($"SDK package metadata is invalid JSON: {packageJsonAbsolute}");
                    return false;
                }

                if (!string.Equals(packageInfo.name, Constants.PackageId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(
                        $"SDK package metadata name mismatch. Expected {Constants.PackageId}, got {packageInfo.name}.");
                    return false;
                }

                if (!IsStablePackageVersion(packageInfo.version))
                {
                    result.Errors.Add(
                        $"SDK package metadata must contain a stable major.minor.patch version: {packageJsonAbsolute}");
                    return false;
                }

                packageVersion = packageInfo.version.Trim();

                if (!string.Equals(packageInfo.documentationUrl?.Trim(), Constants.OfficialDocumentationUrl,
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        $"SDK package documentationUrl must be '{Constants.OfficialDocumentationUrl}': " +
                        packageJsonAbsolute);
                    return false;
                }

                if (!string.Equals(packageInfo.license?.Trim(), "SEE LICENSE IN LICENSE.txt",
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        $"SDK package metadata must declare 'SEE LICENSE IN LICENSE.txt': {packageJsonAbsolute}");
                    return false;
                }

                result.PackageLicense = packageInfo.license.Trim();

                var requiredUnityVersion = BuildRequiredUnityVersion(packageInfo.unity, packageInfo.unityRelease);
                if (string.IsNullOrWhiteSpace(requiredUnityVersion))
                {
                    result.Errors.Add(
                        $"SDK package metadata must declare both unity and unityRelease: {packageJsonAbsolute}");
                    return false;
                }

                result.RequiredUnityVersion = requiredUnityVersion;
                if (!string.Equals(requiredUnityVersion, Constants.RequiredUnityVersion,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(
                        $"SDK package Unity requirement mismatch. package.json requires {requiredUnityVersion}, " +
                        $"but the SDK runtime requires {Constants.RequiredUnityVersion}.");
                    return false;
                }

                if (!string.Equals(requiredUnityVersion, Application.unityVersion,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(
                        $"SDK package Unity requirement mismatch. Package requires {requiredUnityVersion}, " +
                        $"but the SDK is being built with {Application.unityVersion}.");
                    return false;
                }

                if (packageInfo.vellocetSdkContentSchemaVersion != Constants.SdkContentSchemaVersion)
                {
                    result.Errors.Add(
                        "SDK package content schema mismatch. package.json declares " +
                        $"{packageInfo.vellocetSdkContentSchemaVersion}, but the SDK runtime requires " +
                        $"{Constants.SdkContentSchemaVersion}.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to read SDK package metadata from '{packageJsonAbsolute}': {ex.Message}");
                return false;
            }
        }

        private static bool CleanOutputRoot(string outputRootAbsolute, PackageExportResult result)
        {
            try
            {
                var deletedEntries = 0;
                if (Directory.Exists(outputRootAbsolute))
                {
                    foreach (var file in Directory.GetFiles(outputRootAbsolute, "*", SearchOption.TopDirectoryOnly))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        deletedEntries++;
                    }

                    foreach (var directory in Directory.GetDirectories(outputRootAbsolute, "*",
                                 SearchOption.TopDirectoryOnly))
                    {
                        Directory.Delete(directory, true);
                        deletedEntries++;
                    }
                }

                result.CleanedOutputEntryCount = deletedEntries;
                return true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to clean package output root '{outputRootAbsolute}': {ex.Message}");
                return false;
            }
        }

        private static string BuildRequiredUnityVersion(string unity, string unityRelease)
        {
            if (string.IsNullOrWhiteSpace(unity) || string.IsNullOrWhiteSpace(unityRelease))
                return string.Empty;

            return $"{unity.Trim()}.{unityRelease.Trim()}";
        }

        private static bool ValidateRequiredPackageFiles(string packageSourceAbsolute, PackageExportResult result)
        {
            var requiredFiles = new[]
            {
                "LICENSE.txt"
            };
            foreach (var fileName in requiredFiles)
            {
                var path = Path.Combine(packageSourceAbsolute, fileName);
                if (!File.Exists(path))
                    result.Errors.Add($"SDK package is missing required distribution file: {path}");
            }

            return result.Errors.Count == 0;
        }

        private static bool IsStablePackageVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var parts = value.Trim().Split('.');
            if (parts.Length != 3)
                return false;

            foreach (var part in parts)
            {
                if (part.Length == 0 || (part.Length > 1 && part[0] == '0'))
                    return false;

                foreach (var character in part)
                    if (character is < '0' or > '9')
                        return false;
            }

            return true;
        }

        private static IEnumerable<string> EnumeratePackageFiles(string packageRootAbsolute)
        {
            var rootWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(packageRootAbsolute));
            foreach (var file in Directory.EnumerateFiles(packageRootAbsolute, "*", SearchOption.AllDirectories))
            {
                var relative = file[rootWithSeparator.Length..];
                if (ShouldSkipPackagePath(relative))
                    continue;

                yield return file;
            }
        }

        internal static bool ShouldSkipPackagePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return true;

            var fileName = Path.GetFileName(relativePath);
            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                fileName = Path.GetFileNameWithoutExtension(fileName);
            if (IgnoredFileNames.Contains(fileName))
                return true;

            if (Path.GetExtension(fileName) is ".pdb" or ".tt" ||
                fileName is "Microsoft.CodeAnalysis.xml" or "Microsoft.CodeAnalysis.CSharp.xml")
                return true;

            if (IsPackageLiteratureFile(fileName))
                return true;

            var parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return true;

            foreach (var part in parts)
            {
                var name = part.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileNameWithoutExtension(part) : part;
                if (IgnoredDirectoryNames.Contains(name) || name.EndsWith("~", StringComparison.Ordinal) ||
                    name.Equals("Tests", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(".Tests.", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".PerformanceTests", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(".PerformanceTests.", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsPackageLiteratureFile(string fileName)
        {
            var candidate = fileName;
            if (string.Equals(Path.GetExtension(candidate), ".meta", StringComparison.OrdinalIgnoreCase))
                candidate = Path.GetFileNameWithoutExtension(candidate);

            return !candidate.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase) &&
                   !candidate.StartsWith("LICENCE", StringComparison.OrdinalIgnoreCase) &&
                   !candidate.StartsWith("NOTICE", StringComparison.OrdinalIgnoreCase) &&
                   !candidate.StartsWith("COPYING", StringComparison.OrdinalIgnoreCase) &&
                   !candidate.StartsWith("Third Party Notices", StringComparison.OrdinalIgnoreCase) &&
                   !candidate.StartsWith("THIRD-PARTY", StringComparison.OrdinalIgnoreCase) &&
                   !candidate.StartsWith("THIRD_PARTY", StringComparison.OrdinalIgnoreCase) &&
                   !candidate.StartsWith("OFL", StringComparison.OrdinalIgnoreCase) &&
                   PackageLiteratureExtensions.Contains(Path.GetExtension(candidate));
        }

        private static string ToRelativePath(string rootAbsolute, string fileAbsolute)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(rootAbsolute));
            return Path.GetFullPath(fileAbsolute)[root.Length..].Replace('\\', '/');
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

        [Serializable]
        private sealed class PackageManifest
        {
            public string name;
            public string version;
            public string unity;
            public string unityRelease;
            public string documentationUrl;
            public string license;
            public int vellocetSdkContentSchemaVersion;
        }

        internal sealed class PackageExportResult
        {
            internal List<string> Errors { get; } = new();
            internal string OutputRootPath { get; set; } = string.Empty;
            internal string PackageId { get; set; } = string.Empty;
            internal string PackageVersion { get; set; } = "unknown";
            internal string PackageLicense { get; set; } = "unknown";
            internal string RequiredUnityVersion { get; set; } = "unknown";
            internal string PackageSourceRootPath { get; set; } = string.Empty;
            internal List<string> Warnings { get; } = new();
            internal int CopiedFileCount { get; set; }
            internal int CleanedOutputEntryCount { get; set; }
            internal bool Succeeded => Errors.Count == 0;
        }
    }
}
#endif
