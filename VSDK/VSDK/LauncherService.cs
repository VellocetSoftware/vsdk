using System.Text.Json;

namespace VSDK;

internal sealed class LauncherService(LauncherPaths paths)
{
    private const string ExpectedPackageName = "com.vellocet.sdk";

    public LauncherPaths Paths { get; } = paths;

    public LauncherStatusSnapshot GetStatusSnapshot()
    {
        var packageDirectory = LauncherPaths.ResolveFirstExistingDirectory(Paths.PackageDirectoryCandidates);
        var contentDirectory = LauncherPaths.ResolveFirstExistingDirectory(Paths.ContentDirectoryCandidates);
        var documentationFile = LauncherPaths.ResolveFirstExistingFile(Paths.DocumentationFileCandidates);

        var packageManifest = InspectPackageManifest(packageDirectory);
        var contentManifest = InspectContentManifest(contentDirectory);
        var contentAssetsDirectory = contentDirectory is null ? null : Path.Combine(contentDirectory, "Assets");

        var requiredChecks = new[]
        {
            new CheckResult("SDK package directory", packageDirectory is not null,
                packageDirectory ?? $"Missing. Expected: {string.Join(", ", Paths.PackageDirectoryCandidates)}",
                true),
            new CheckResult("SDK package manifest", packageManifest.Exists,
                packageManifest.Path ?? "Missing SDKPackage/package.json.", true),
            new CheckResult("SDK package manifest parse", packageManifest.IsParsed,
                packageManifest.IsParsed
                    ? $"{packageManifest.Name ?? "unknown"} {packageManifest.Version ?? "unknown"}"
                    : packageManifest.ParseError ?? "Package manifest parse failed.",
                true),
            new CheckResult("SDK package identity", packageManifest.HasExpectedName,
                packageManifest.HasExpectedName
                    ? ExpectedPackageName
                    : $"Expected {ExpectedPackageName}, got {packageManifest.Name ?? "unknown"}.",
                true),
            new CheckResult("SDK content directory", contentDirectory is not null,
                contentDirectory ?? $"Missing. Expected: {string.Join(", ", Paths.ContentDirectoryCandidates)}",
                true),
            new CheckResult("SDK content manifest", contentManifest.Exists,
                contentManifest.Path ?? "Missing SDKContent/sdk-content-manifest.json.", true),
            new CheckResult("SDK content manifest parse", contentManifest.IsParsed,
                contentManifest.IsParsed
                    ? $"Schema v{contentManifest.SchemaVersion?.ToString() ?? "unknown"}, entries: {contentManifest.EntryCount}"
                    : contentManifest.ParseError ?? "Content manifest parse failed.",
                true),
            new CheckResult("SDK content Assets folder",
                contentAssetsDirectory is not null && Directory.Exists(contentAssetsDirectory),
                contentAssetsDirectory ?? "Content root missing.", true)
        };

        var optionalChecks = new[]
        {
            new CheckResult("Documentation file", documentationFile is not null,
                documentationFile ?? "Optional. No Docs/index.html, README.txt, or README.md found.", false)
        };

        var allChecks = requiredChecks.Concat(optionalChecks).ToArray();
        var isReady = requiredChecks.All(check => check.Passed);

        return new LauncherStatusSnapshot(
            isReady,
            BuildSummary(isReady, requiredChecks),
            BuildChecklist(allChecks),
            BuildGuide(Paths.InstallRoot, packageManifest.Path, isReady),
            BuildDiagnostics(Paths.InstallRoot, packageDirectory, packageManifest, contentDirectory, contentManifest,
                documentationFile));
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

            return PackageInspection.FromParsed(manifestPath, name, version);
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

            var entryCount = 0;
            if (root.TryGetProperty("entries", out var entriesProperty) &&
                entriesProperty.ValueKind == JsonValueKind.Array)
                entryCount = entriesProperty.GetArrayLength();

            return ContentManifestInspection.FromParsed(manifestPath, schemaVersion, entryCount);
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

    private static string BuildChecklist(IEnumerable<CheckResult> checks)
    {
        return string.Join(Environment.NewLine, checks.Select(check =>
        {
            var prefix = check.Required
                ? check.Passed ? "[OK]" : "[MISSING]"
                : check.Passed
                    ? "[OK]"
                    : "[OPTIONAL]";
            return $"{prefix} {check.Label}: {check.Detail}";
        }));
    }

    private static string BuildGuide(string installRoot, string? packageManifestPath, bool isReady)
    {
        var lines = new List<string>
        {
            "Install the package once; Unity opens the SDK tools and guides the remaining setup."
        };

        if (!isReady)
        {
            lines.Add(
                "Setup is blocked until all required distribution checks are marked [OK] in the checklist.");
            lines.Add(string.Empty);
        }

        lines.Add("1. Open your Unity project.");
        lines.Add(
            $"2. Add package via Window > Package Manager > + > Add package from disk...{Environment.NewLine}   Path: {packageManifestPath ?? "Missing SDKPackage/package.json"}");
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
        ContentManifestInspection contentManifest,
        string? documentationFile)
    {
        var lines = new List<string>
        {
            $"Install Root: {installRoot}",
            $"SDK Package Directory: {packageDirectory ?? "Missing"}",
            $"SDK Package Manifest: {packageManifest.Path ?? "Missing"}",
            $"SDK Package Manifest Parsed: {(packageManifest.IsParsed ? "Yes" : "No")}",
            $"SDK Package Name: {packageManifest.Name ?? "Unknown"}",
            $"SDK Package Version: {packageManifest.Version ?? "Unknown"}",
            $"SDK Content Directory: {contentDirectory ?? "Missing"}",
            $"SDK Content Manifest: {contentManifest.Path ?? "Missing"}",
            $"SDK Content Manifest Parsed: {(contentManifest.IsParsed ? "Yes" : "No")}",
            $"SDK Content Manifest Schema: {contentManifest.SchemaVersion?.ToString() ?? "Unknown"}",
            $"SDK Content Manifest Entries: {contentManifest.EntryCount}",
            $"Documentation: {documentationFile ?? "Missing"}"
        };

        if (!string.IsNullOrWhiteSpace(packageManifest.ParseError))
            lines.Add($"Package Manifest Parse Error: {packageManifest.ParseError}");
        if (!string.IsNullOrWhiteSpace(contentManifest.ParseError))
            lines.Add($"Content Manifest Parse Error: {contentManifest.ParseError}");

        return string.Join(Environment.NewLine, lines);
    }

    private sealed record CheckResult(string Label, bool Passed, string Detail, bool Required);

    private sealed record PackageInspection(
        bool Exists,
        bool IsParsed,
        string? Path,
        string? Name,
        string? Version,
        string? ParseError)
    {
        public bool HasExpectedName =>
            IsParsed && string.Equals(Name, ExpectedPackageName, StringComparison.OrdinalIgnoreCase);

        public static PackageInspection Missing(string? path = null)
        {
            return new PackageInspection(false, false, path, null, null, null);
        }

        public static PackageInspection FromParsed(string path, string? name, string? version)
        {
            return new PackageInspection(true, true, path, name, version, null);
        }

        public static PackageInspection ParseFailed(string path, string parseError)
        {
            return new PackageInspection(true, false, path, null, null, parseError);
        }
    }

    private sealed record ContentManifestInspection(
        bool Exists,
        bool IsParsed,
        string? Path,
        int? SchemaVersion,
        int EntryCount,
        string? ParseError)
    {
        public static ContentManifestInspection Missing(string? path = null)
        {
            return new ContentManifestInspection(false, false, path, null, 0, null);
        }

        public static ContentManifestInspection FromParsed(string path, int? schemaVersion, int entryCount)
        {
            return new ContentManifestInspection(true, true, path, schemaVersion, entryCount, null);
        }

        public static ContentManifestInspection ParseFailed(string path, string parseError)
        {
            return new ContentManifestInspection(true, false, path, null, 0, parseError);
        }
    }
}

internal sealed record LauncherStatusSnapshot(
    bool IsReady,
    string Summary,
    string Checklist,
    string Guide,
    string Diagnostics);
