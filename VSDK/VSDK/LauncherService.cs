// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VSDK;

internal sealed class LauncherService(LauncherPaths paths)
{
    private const string ExpectedPackageLicense = "SEE LICENSE IN LICENSE.txt";
    private const string ExpectedPackageName = "com.vellocet.sdk";

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
        var packageReadmeFile = packageDirectory is null ? null : Path.Combine(packageDirectory, "README.md");
        var packageChangelogFile = packageDirectory is null ? null : Path.Combine(packageDirectory, "CHANGELOG.md");

        var requiredChecks = new[]
        {
            new CheckResult("Distribution license", File.Exists(distributionLicenseFile),
                distributionLicenseFile),
            new CheckResult("SDK package directory", packageDirectory is not null,
                packageDirectory ?? $"Missing. Expected: {string.Join(", ", Paths.PackageDirectoryCandidates)}"),
            new CheckResult("SDK package manifest", packageManifest.Exists,
                packageManifest.Path ?? "Missing SDKPackage/package.json."),
            new CheckResult("SDK package manifest parse", packageManifest.IsParsed,
                packageManifest.IsParsed
                    ? $"{packageManifest.Name ?? "unknown"} {packageManifest.Version ?? "unknown"}"
                    : packageManifest.ParseError ?? "Package manifest parse failed."),
            new CheckResult("SDK package identity", packageManifest.HasExpectedName,
                packageManifest.HasExpectedName
                    ? ExpectedPackageName
                    : $"Expected {ExpectedPackageName}, got {packageManifest.Name ?? "unknown"}."),
            new CheckResult("SDK package version", packageManifest.HasValidVersion,
                packageManifest.HasValidVersion
                    ? packageManifest.Version!
                    : $"Expected a stable major.minor.patch version, got {packageManifest.Version ?? "missing"}."),
            new CheckResult("Required Unity version", packageManifest.HasUnityRequirement,
                packageManifest.HasUnityRequirement
                    ? $"Unity {packageManifest.RequiredUnityVersion} exactly"
                    : packageManifest.UnityRequirementError ??
                      "Missing package.json unity and/or unityRelease metadata."),
            new CheckResult("SDK package license declaration", packageManifest.HasExpectedLicenseDeclaration,
                packageManifest.HasExpectedLicenseDeclaration
                    ? packageManifest.License!
                    : $"Expected '{ExpectedPackageLicense}', got {packageManifest.License ?? "missing"}."),
            new CheckResult("SDK package content schema declaration", packageManifest.HasContentSchemaVersion,
                packageManifest.HasContentSchemaVersion
                    ? $"Schema v{packageManifest.ContentSchemaVersion}"
                    : "Missing positive integer package.json vellocetSdkContentSchemaVersion metadata."),
            new CheckResult("SDK package license file", packageLicenseFile is not null && File.Exists(packageLicenseFile),
                packageLicenseFile ?? "Missing SDKPackage/LICENSE.txt."),
            new CheckResult("SDK package README", packageReadmeFile is not null && File.Exists(packageReadmeFile),
                packageReadmeFile ?? "Missing SDKPackage/README.md."),
            new CheckResult("SDK package changelog",
                packageChangelogFile is not null && File.Exists(packageChangelogFile),
                packageChangelogFile ?? "Missing SDKPackage/CHANGELOG.md."),
            new CheckResult("SDK content directory", contentDirectory is not null,
                contentDirectory ?? $"Missing. Expected: {string.Join(", ", Paths.ContentDirectoryCandidates)}"),
            new CheckResult("SDK content manifest", contentManifest.Exists,
                contentManifest.Path ?? "Missing SDKContent/sdk-content-manifest.json."),
            new CheckResult("SDK content manifest parse", contentManifest.IsParsed,
                contentManifest.IsParsed
                    ? $"Schema v{contentManifest.SchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}, entries: {contentManifest.EntryCount}"
                    : contentManifest.ParseError ?? "Content manifest parse failed."),
            new CheckResult("SDK content schema",
                contentManifest.MatchesSchema(packageManifest.ContentSchemaVersion),
                contentManifest.MatchesSchema(packageManifest.ContentSchemaVersion)
                    ? $"Schema v{packageManifest.ContentSchemaVersion}"
                    : $"Package declares schema " +
                      $"v{packageManifest.ContentSchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "missing"}; " +
                      $"content contains v{contentManifest.SchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "missing"}."),
            new CheckResult("SDK content entries", contentManifest.HasEntries,
                contentManifest.HasEntries
                    ? $"{contentManifest.EntryCount} entries"
                    : contentManifest.HasEntriesArray
                        ? "The content manifest contains no entries."
                        : "The content manifest is missing its entries array."),
            new CheckResult("SDK content Assets folder",
                contentAssetsDirectory is not null && Directory.Exists(contentAssetsDirectory),
                contentAssetsDirectory ?? "Content root missing.")
        };

        var isReady = requiredChecks.All(check => check.Passed);

        return new LauncherStatusSnapshot(
            isReady,
            BuildSummary(isReady, requiredChecks),
            BuildChecklist(requiredChecks),
            BuildUnityRequirement(packageManifest),
            BuildGuide(Paths.InstallRoot, packageManifest, isReady),
            BuildDiagnostics(Paths.InstallRoot, packageDirectory, packageManifest, contentDirectory, contentManifest));
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

    private static string BuildSummary(bool isReady, IReadOnlyList<CheckResult> requiredChecks)
    {
        var passedCount = requiredChecks.Count(check => check.Passed);
        var totalCount = requiredChecks.Count;

        return isReady
            ? $"Distribution verified ({passedCount}/{totalCount} required checks). SDK setup can proceed in Unity."
            : $"Distribution incomplete ({passedCount}/{totalCount} required checks). Resolve missing items before setup.";
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

    private static string BuildChecklist(IEnumerable<CheckResult> checks)
    {
        return string.Join(Environment.NewLine, checks.Select(check =>
        {
            var prefix = check.Passed ? "[OK]" : "[FAIL]";
            return $"{prefix} {check.Label}: {check.Detail}";
        }));
    }

    private static string BuildUnityRequirement(PackageInspection packageManifest)
    {
        return packageManifest.HasUnityRequirement
            ? $"Required Unity: {packageManifest.RequiredUnityVersion} (use this exact Editor version)"
            : "Required Unity: Unknown (package compatibility metadata is missing)";
    }

    private static string BuildGuide(string installRoot, PackageInspection packageManifest, bool isReady)
    {
        var lines = new List<string>
        {
            packageManifest.HasUnityRequirement
                ? $"Before installing, open the project with Unity {packageManifest.RequiredUnityVersion} exactly."
                : "Before installing, resolve the missing Unity version metadata shown in the checklist.",
            "Install the package once; Unity opens the SDK tools and guides the remaining setup."
        };

        if (!isReady)
        {
            lines.Add(
                "Setup is blocked until all required distribution checks are marked [OK] in the checklist.");
            lines.Add(string.Empty);
        }

        lines.Add(packageManifest.HasUnityRequirement
            ? $"1. In Unity Hub, open your project with Editor {packageManifest.RequiredUnityVersion}."
            : "1. Open your Unity project after resolving the required Editor version.");
        lines.Add(
            $"2. Add package via Window > Package Manager > + > Add package from disk...{Environment.NewLine}   Path: {packageManifest.Path ?? "Missing SDKPackage/package.json"}");
        lines.Add(
            $"3. Unity opens SDK Workbench and SDK Map Exporter automatically.{Environment.NewLine}" +
            $"   In the setup prompt, select this VSDK install folder: {installRoot}");
        lines.Add(
            "4. In SDK Workbench, click Create New Map… or Prepare Active Scene. The SDK configures the map contract and Probe Volumes.");
        lines.Add(
            "5. Add entity markers, test in Play Mode, then compile or export from SDK Map Exporter.");
        lines.Add(
            "Need the setup prompt again? Use Tools > Vellocet > SDK > Welcome & Setup.");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildDiagnostics(
        string installRoot,
        string? packageDirectory,
        PackageInspection packageManifest,
        string? contentDirectory,
        ContentManifestInspection contentManifest)
    {
        var lines = new List<string>
        {
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

        return string.Join(Environment.NewLine, lines);
    }

    private sealed record CheckResult(string Label, bool Passed, string Detail);

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

internal sealed record LauncherStatusSnapshot(
    bool IsReady,
    string Summary,
    string Checklist,
    string UnityRequirement,
    string Guide,
    string Diagnostics);
