// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VSDK;

internal sealed class LauncherService(LauncherPaths paths)
{
    internal const string DocumentationUrl = "https://developer.vellocetsoftware.com/wiki/Vellocet_SDK";

    private const string ExpectedPackageLicense = "SEE LICENSE IN LICENSE.txt";
    private const string ExpectedPackageName = "com.vellocet.sdk";

    private static readonly HashSet<string> LiteratureExtensions = new(StringComparer.OrdinalIgnoreCase)
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

    private static readonly Regex StableSemanticVersionPattern = new(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex UnityVersionPattern = new(
        @"^\d{4}\.\d+$",
        RegexOptions.CultureInvariant);

    private static readonly Regex UnityReleasePattern = new(
        @"^\d+(?:a|b|f|p)\d+$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public LauncherPaths Paths { get; } = paths;

    public LauncherStatusSnapshot GetStatusSnapshot()
    {
        var packageDirectory = LauncherPaths.ResolveFirstExistingDirectory(Paths.PackageDirectoryCandidates);
        var contentDirectory = LauncherPaths.ResolveFirstExistingDirectory(Paths.ContentDirectoryCandidates);
        var packageManifest = InspectPackageManifest(packageDirectory);
        var contentManifest = InspectContentManifest(contentDirectory);
        var contentAssetsDirectory = contentDirectory is null ? null : Path.Combine(contentDirectory, "Assets");
        var distributionLicenseFile = Path.Combine(Paths.InstallRoot, "LICENSE.txt");
        var packageLicenseFile = packageDirectory is null ? null : Path.Combine(packageDirectory, "LICENSE.txt");
        var packageLiterature = InspectPackageLiterature(packageDirectory);

        var requiredChecks = new[]
        {
            new LauncherCheck("Distribution license", File.Exists(distributionLicenseFile),
                distributionLicenseFile),
            new LauncherCheck("SDK package directory", packageDirectory is not null,
                packageDirectory ?? $"Missing. Expected: {string.Join(", ", Paths.PackageDirectoryCandidates)}"),
            new LauncherCheck("SDK package manifest", packageManifest.Exists,
                packageManifest.Path ?? "Missing SDKPackage/package.json."),
            new LauncherCheck("SDK package manifest parse", packageManifest.IsParsed,
                packageManifest.IsParsed
                    ? $"{packageManifest.Name ?? "unknown"} {packageManifest.Version ?? "unknown"}"
                    : packageManifest.ParseError ?? "Package manifest parse failed."),
            new LauncherCheck("SDK package identity", packageManifest.HasExpectedName,
                packageManifest.HasExpectedName
                    ? ExpectedPackageName
                    : $"Expected {ExpectedPackageName}, got {packageManifest.Name ?? "unknown"}."),
            new LauncherCheck("SDK package version", packageManifest.HasValidVersion,
                packageManifest.HasValidVersion
                    ? packageManifest.Version!
                    : $"Expected a stable major.minor.patch version, got {packageManifest.Version ?? "missing"}."),
            new LauncherCheck("Required Unity version", packageManifest.HasUnityRequirement,
                packageManifest.HasUnityRequirement
                    ? $"Unity {packageManifest.RequiredUnityVersion} exactly"
                    : packageManifest.UnityRequirementError ??
                      "Missing package.json unity and/or unityRelease metadata."),
            new LauncherCheck("SDK package license declaration", packageManifest.HasExpectedLicenseDeclaration,
                packageManifest.HasExpectedLicenseDeclaration
                    ? packageManifest.License!
                    : $"Expected '{ExpectedPackageLicense}', got {packageManifest.License ?? "missing"}."),
            new LauncherCheck("SDK package content schema declaration", packageManifest.HasContentSchemaVersion,
                packageManifest.HasContentSchemaVersion
                    ? $"Schema v{packageManifest.ContentSchemaVersion}"
                    : "Missing positive integer package.json vellocetSdkContentSchemaVersion metadata."),
            new LauncherCheck("SDK package license file", packageLicenseFile is not null && File.Exists(packageLicenseFile),
                packageLicenseFile ?? "Missing SDKPackage/LICENSE.txt."),
            new LauncherCheck("Wiki-only documentation policy", packageLiterature.IsCompliant,
                packageLiterature.BuildDetail()),
            new LauncherCheck("SDK content directory", contentDirectory is not null,
                contentDirectory ?? $"Missing. Expected: {string.Join(", ", Paths.ContentDirectoryCandidates)}"),
            new LauncherCheck("SDK content manifest", contentManifest.Exists,
                contentManifest.Path ?? "Missing SDKContent/sdk-content-manifest.json."),
            new LauncherCheck("SDK content manifest parse", contentManifest.IsParsed,
                contentManifest.IsParsed
                    ? $"Schema v{contentManifest.SchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}, entries: {contentManifest.EntryCount}"
                    : contentManifest.ParseError ?? "Content manifest parse failed."),
            new LauncherCheck("SDK content schema",
                contentManifest.MatchesSchema(packageManifest.ContentSchemaVersion),
                contentManifest.MatchesSchema(packageManifest.ContentSchemaVersion)
                    ? $"Schema v{packageManifest.ContentSchemaVersion}"
                    : $"Package declares schema " +
                      $"v{packageManifest.ContentSchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "missing"}; " +
                      $"content contains v{contentManifest.SchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "missing"}."),
            new LauncherCheck("SDK content entries", contentManifest.HasEntries,
                contentManifest.HasEntries
                    ? $"{contentManifest.EntryCount} entries"
                    : contentManifest.HasEntriesArray
                        ? "The content manifest contains no entries."
                        : "The content manifest is missing its entries array."),
            new LauncherCheck("SDK content Assets folder",
                contentAssetsDirectory is not null && Directory.Exists(contentAssetsDirectory),
                contentAssetsDirectory ?? "Content root missing.")
        };

        var isReady = requiredChecks.All(check => check.Passed);

        return new LauncherStatusSnapshot(
            isReady,
            BuildSummary(isReady, requiredChecks),
            requiredChecks,
            packageManifest.Version,
            packageManifest.RequiredUnityVersion,
            packageManifest.ContentSchemaVersion,
            contentManifest.EntryCount,
            Paths.InstallRoot,
            packageManifest.Path,
            BuildDiagnostics(Paths.InstallRoot, packageDirectory, packageManifest, contentDirectory, contentManifest,
                requiredChecks));
    }

    private static PackageInspection InspectPackageManifest(string? packageDirectory)
    {
        if (string.IsNullOrWhiteSpace(packageDirectory))
            return PackageInspection.Missing();

        var manifestPath = Path.Combine(packageDirectory, "package.json");
        if (!File.Exists(manifestPath))
            return PackageInspection.Missing(manifestPath);

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var name = root.TryGetProperty("name", out var nameProperty) &&
                       nameProperty.ValueKind == JsonValueKind.String
                ? nameProperty.GetString()
                : null;

            var version = root.TryGetProperty("version", out var versionProperty) &&
                          versionProperty.ValueKind == JsonValueKind.String
                ? versionProperty.GetString()
                : null;

            var unity = root.TryGetProperty("unity", out var unityProperty) &&
                        unityProperty.ValueKind == JsonValueKind.String
                ? unityProperty.GetString()
                : null;

            var unityRelease = root.TryGetProperty("unityRelease", out var unityReleaseProperty) &&
                               unityReleaseProperty.ValueKind == JsonValueKind.String
                ? unityReleaseProperty.GetString()
                : null;

            var license = root.TryGetProperty("license", out var licenseProperty) &&
                          licenseProperty.ValueKind == JsonValueKind.String
                ? licenseProperty.GetString()
                : null;

            var contentSchemaVersion =
                root.TryGetProperty("vellocetSdkContentSchemaVersion", out var contentSchemaProperty) &&
                contentSchemaProperty.ValueKind == JsonValueKind.Number &&
                contentSchemaProperty.TryGetInt32(out var parsedContentSchemaVersion)
                    ? parsedContentSchemaVersion
                    : (int?)null;

            TryBuildRequiredUnityVersion(unity, unityRelease, out var requiredUnityVersion,
                out var unityRequirementError);

            return PackageInspection.FromParsed(
                manifestPath,
                name,
                version,
                unity,
                unityRelease,
                license,
                contentSchemaVersion,
                requiredUnityVersion,
                unityRequirementError);
        }
        catch (Exception ex)
        {
            return PackageInspection.ParseFailed(manifestPath, ex.Message);
        }
    }

    private static ContentManifestInspection InspectContentManifest(string? contentDirectory)
    {
        if (string.IsNullOrWhiteSpace(contentDirectory))
            return ContentManifestInspection.Missing();

        var manifestPath = Path.Combine(contentDirectory, "sdk-content-manifest.json");
        if (!File.Exists(manifestPath))
            return ContentManifestInspection.Missing(manifestPath);

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var schemaVersion = root.TryGetProperty("schemaVersion", out var schemaProperty) &&
                                schemaProperty.ValueKind == JsonValueKind.Number &&
                                schemaProperty.TryGetInt32(out var value)
                ? value
                : (int?)null;

            var hasEntriesArray = root.TryGetProperty("entries", out var entriesProperty) &&
                                  entriesProperty.ValueKind == JsonValueKind.Array;
            var entryCount = 0;
            if (hasEntriesArray)
                entryCount = entriesProperty.GetArrayLength();

            return ContentManifestInspection.FromParsed(manifestPath, schemaVersion, hasEntriesArray, entryCount);
        }
        catch (Exception ex)
        {
            return ContentManifestInspection.ParseFailed(manifestPath, ex.Message);
        }
    }

    private static string BuildSummary(bool isReady, LauncherCheck[] requiredChecks)
    {
        var passedCount = requiredChecks.Count(check => check.Passed);
        var totalCount = requiredChecks.Length;
        var failedCount = totalCount - passedCount;

        return isReady
            ? $"Package metadata, content schema, and managed assets are consistent ({passedCount}/{totalCount} checks)."
            : $"VSDK found {failedCount} required distribution {(failedCount == 1 ? "issue" : "issues")}. " +
              "Review the highlighted checks before using the SDK in Unity.";
    }

    private static bool TryBuildRequiredUnityVersion(
        string? unity,
        string? unityRelease,
        out string? requiredUnityVersion,
        out string? error)
    {
        requiredUnityVersion = null;
        error = null;

        var normalizedUnity = unity?.Trim();
        var normalizedRelease = unityRelease?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUnity) || string.IsNullOrWhiteSpace(normalizedRelease))
        {
            error = "Both package.json unity and unityRelease metadata are required.";
            return false;
        }

        if (!UnityVersionPattern.IsMatch(normalizedUnity))
        {
            error = $"Invalid package.json unity value: {normalizedUnity}.";
            return false;
        }

        if (!UnityReleasePattern.IsMatch(normalizedRelease))
        {
            error = $"Invalid package.json unityRelease value: {normalizedRelease}.";
            return false;
        }

        requiredUnityVersion = $"{normalizedUnity}.{normalizedRelease}";
        return true;
    }

    private static bool IsStableSemanticVersion(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && StableSemanticVersionPattern.IsMatch(value.Trim());
    }

    private static PackageLiteratureInspection InspectPackageLiterature(string? packageDirectory)
    {
        if (string.IsNullOrWhiteSpace(packageDirectory))
            return PackageLiteratureInspection.Failed("SDK package directory is missing.");

        try
        {
            var files = Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories)
                .Where(IsPackageLiteraturePath)
                .Select(path => Path.GetRelativePath(packageDirectory, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return PackageLiteratureInspection.FromFiles(files);
        }
        catch (Exception ex)
        {
            return PackageLiteratureInspection.Failed($"Could not inspect package documentation policy: {ex.Message}");
        }
    }

    private static bool IsPackageLiteraturePath(string path)
    {
        var candidate = path;
        if (string.Equals(Path.GetExtension(candidate), ".meta", StringComparison.OrdinalIgnoreCase))
            candidate = Path.GetFileNameWithoutExtension(candidate);

        return LiteratureExtensions.Contains(Path.GetExtension(candidate));
    }

    private static string BuildDiagnostics(
        string installRoot,
        string? packageDirectory,
        PackageInspection packageManifest,
        string? contentDirectory,
        ContentManifestInspection contentManifest,
        IReadOnlyList<LauncherCheck> checks)
    {
        var lines = new List<string>
        {
            $"Official Documentation: {DocumentationUrl}",
            $"Install Root: {installRoot}",
            $"SDK Package Directory: {packageDirectory ?? "Missing"}",
            $"SDK Package Manifest: {packageManifest.Path ?? "Missing"}",
            $"SDK Package Manifest Parsed: {(packageManifest.IsParsed ? "Yes" : "No")}",
            $"SDK Package Name: {packageManifest.Name ?? "Unknown"}",
            $"SDK Package Version: {packageManifest.Version ?? "Unknown"}",
            $"SDK Package License: {packageManifest.License ?? "Unknown"}",
            $"SDK Package Content Schema: {packageManifest.ContentSchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "Unknown"}",
            $"SDK Package Unity: {packageManifest.Unity ?? "Unknown"}",
            $"SDK Package Unity Release: {packageManifest.UnityRelease ?? "Unknown"}",
            $"Required Unity Version: {packageManifest.RequiredUnityVersion ?? "Unknown"}",
            $"SDK Content Directory: {contentDirectory ?? "Missing"}",
            $"SDK Content Manifest: {contentManifest.Path ?? "Missing"}",
            $"SDK Content Manifest Parsed: {(contentManifest.IsParsed ? "Yes" : "No")}",
            $"SDK Content Manifest Schema: {contentManifest.SchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "Unknown"}",
            $"SDK Content Manifest Entries: {contentManifest.EntryCount}"
        };

        if (!string.IsNullOrWhiteSpace(packageManifest.ParseError))
            lines.Add($"Package Manifest Parse Error: {packageManifest.ParseError}");
        if (!string.IsNullOrWhiteSpace(packageManifest.UnityRequirementError))
            lines.Add($"Unity Requirement Error: {packageManifest.UnityRequirementError}");
        if (!string.IsNullOrWhiteSpace(contentManifest.ParseError))
            lines.Add($"Content Manifest Parse Error: {contentManifest.ParseError}");

        lines.Add(string.Empty);
        lines.Add("Required Checks:");
        lines.AddRange(checks.Select(check =>
            $"[{(check.Passed ? "OK" : "FAIL")}] {check.Label}: {check.Detail}"));

        return string.Join(Environment.NewLine, lines);
    }

    private sealed record PackageLiteratureInspection(IReadOnlyList<string> Files, string? Error)
    {
        public bool IsCompliant => Error is null && Files.Count == 0;

        public string BuildDetail()
        {
            if (!string.IsNullOrWhiteSpace(Error))
                return Error;

            if (Files.Count == 0)
                return $"No packaged guides. Current guidance: {DocumentationUrl}";

            const int displayLimit = 4;
            var listedFiles = string.Join(", ", Files.Take(displayLimit));
            var remainder = Files.Count > displayLimit ? $" (+{Files.Count - displayLimit} more)" : string.Empty;
            return $"Remove packaged documentation: {listedFiles}{remainder}";
        }

        public static PackageLiteratureInspection Failed(string error)
        {
            return new PackageLiteratureInspection(Array.Empty<string>(), error);
        }

        public static PackageLiteratureInspection FromFiles(IReadOnlyList<string> files)
        {
            return new PackageLiteratureInspection(files, null);
        }
    }

    private sealed record PackageInspection(
        bool Exists,
        bool IsParsed,
        string? Path,
        string? Name,
        string? Version,
        string? Unity,
        string? UnityRelease,
        string? License,
        int? ContentSchemaVersion,
        string? RequiredUnityVersion,
        string? UnityRequirementError,
        string? ParseError)
    {
        public bool HasExpectedName =>
            IsParsed && string.Equals(Name, ExpectedPackageName, StringComparison.OrdinalIgnoreCase);

        public bool HasValidVersion => IsParsed && IsStableSemanticVersion(Version);

        public bool HasUnityRequirement => IsParsed && !string.IsNullOrWhiteSpace(RequiredUnityVersion);

        public bool HasExpectedLicenseDeclaration =>
            IsParsed && string.Equals(License?.Trim(), ExpectedPackageLicense, StringComparison.Ordinal);

        public bool HasContentSchemaVersion => IsParsed && ContentSchemaVersion > 0;

        public static PackageInspection Missing(string? path = null)
        {
            return new PackageInspection(
                false,
                false,
                path,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        public static PackageInspection FromParsed(
            string path,
            string? name,
            string? version,
            string? unity,
            string? unityRelease,
            string? license,
            int? contentSchemaVersion,
            string? requiredUnityVersion,
            string? unityRequirementError)
        {
            return new PackageInspection(
                true,
                true,
                path,
                name,
                version,
                unity,
                unityRelease,
                license,
                contentSchemaVersion,
                requiredUnityVersion,
                unityRequirementError,
                null);
        }

        public static PackageInspection ParseFailed(string path, string parseError)
        {
            return new PackageInspection(
                true,
                false,
                path,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                parseError);
        }
    }

    private sealed record ContentManifestInspection(
        bool Exists,
        bool IsParsed,
        string? Path,
        int? SchemaVersion,
        bool HasEntriesArray,
        int EntryCount,
        string? ParseError)
    {
        public bool MatchesSchema(int? expectedSchemaVersion)
        {
            return IsParsed && expectedSchemaVersion > 0 && SchemaVersion == expectedSchemaVersion;
        }

        public bool HasEntries => IsParsed && HasEntriesArray && EntryCount > 0;

        public static ContentManifestInspection Missing(string? path = null)
        {
            return new ContentManifestInspection(false, false, path, null, false, 0, null);
        }

        public static ContentManifestInspection FromParsed(
            string path,
            int? schemaVersion,
            bool hasEntriesArray,
            int entryCount)
        {
            return new ContentManifestInspection(
                true,
                true,
                path,
                schemaVersion,
                hasEntriesArray,
                entryCount,
                null);
        }

        public static ContentManifestInspection ParseFailed(string path, string parseError)
        {
            return new ContentManifestInspection(true, false, path, null, false, 0, parseError);
        }
    }
}

internal sealed record LauncherCheck(string Label, bool Passed, string Detail);

internal sealed record LauncherStatusSnapshot(
    bool IsReady,
    string Summary,
    IReadOnlyList<LauncherCheck> Checks,
    string? PackageVersion,
    string? RequiredUnityVersion,
    int? ContentSchemaVersion,
    int ContentEntryCount,
    string InstallRoot,
    string? PackageManifestPath,
    string Diagnostics)
{
    public int PassedCheckCount => Checks.Count(check => check.Passed);
    public int FailedCheckCount => Checks.Count - PassedCheckCount;
}
