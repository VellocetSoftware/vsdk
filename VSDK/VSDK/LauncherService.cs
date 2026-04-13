using System.Text.Json;

namespace VSDK;

internal sealed class LauncherService(LauncherPaths paths)
{
    public LauncherPaths Paths { get; } = paths;

    public LauncherStatusSnapshot GetStatusSnapshot()
    {
        var packageDirectory = LauncherPaths.ResolveFirstExistingDirectory(Paths.PackageDirectoryCandidates);
        var contentDirectory = LauncherPaths.ResolveFirstExistingDirectory(Paths.ContentDirectoryCandidates);
        var documentationFile = LauncherPaths.ResolveFirstExistingFile(Paths.DocumentationFileCandidates);

        var packageFiles = GetUnityPackages(packageDirectory);
        var selectedPackage = packageFiles
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        var manifest = InspectManifest(contentDirectory);
        var contentAssetsDirectory = contentDirectory is null ? null : Path.Combine(contentDirectory, "Assets");

        var requiredChecks = new[]
        {
            new CheckResult("SDK package directory", packageDirectory is not null,
                packageDirectory ?? $"Missing. Expected one of: {string.Join(", ", Paths.PackageDirectoryCandidates)}",
                true),
            new CheckResult("SDK package file (.unitypackage)", selectedPackage is not null,
                selectedPackage ?? "No .unitypackage file found in SDK package directory.", true),
            new CheckResult("SDK content directory", contentDirectory is not null,
                contentDirectory ?? $"Missing. Expected one of: {string.Join(", ", Paths.ContentDirectoryCandidates)}",
                true),
            new CheckResult("SDK content manifest", manifest.Exists,
                manifest.Path ?? "Missing sdk-content-manifest.json.", true),
            new CheckResult("SDK content manifest parse", manifest.IsParsed,
                manifest.IsParsed
                    ? $"Schema v{manifest.SchemaVersion?.ToString() ?? "unknown"}, entries: {manifest.EntryCount}"
                    : manifest.ParseError ?? "Manifest parse failed.",
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
            BuildGuide(selectedPackage, contentDirectory, isReady),
            BuildDiagnostics(Paths.InstallRoot, packageDirectory, packageFiles, selectedPackage, contentDirectory,
                manifest,
                documentationFile));
    }

    private static string[] GetUnityPackages(string? packageDirectory)
    {
        if (string.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory)) return [];

        return Directory
            .EnumerateFiles(packageDirectory, "*.unitypackage", SearchOption.AllDirectories)
            .ToArray();
    }

    private static ManifestInspection InspectManifest(string? contentDirectory)
    {
        if (string.IsNullOrWhiteSpace(contentDirectory)) return ManifestInspection.Missing();

        var manifestPath = Path.Combine(contentDirectory, "sdk-content-manifest.json");
        if (!File.Exists(manifestPath)) return ManifestInspection.Missing(manifestPath);

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

            return ManifestInspection.FromParsed(manifestPath, schemaVersion, entryCount);
        }
        catch (Exception ex)
        {
            return ManifestInspection.ParseFailed(manifestPath, ex.Message);
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

    private static string BuildGuide(string? selectedPackage, string? contentDirectory, bool isReady)
    {
        var lines = new List<string>
        {
            "Primary objective: import the SDK package, link SDK content, then open SDK tools."
        };

        if (!isReady)
        {
            lines.Add(
                "Setup is blocked until all required distribution checks are marked [OK] in the checklist.");
            lines.Add(string.Empty);
        }

        lines.Add("1. Open your Unity project.");
        lines.Add(
            $"2. Import package via Assets > Import Package > Custom Package...{Environment.NewLine}   Path: {selectedPackage ?? "Missing .unitypackage file"}");
        lines.Add(
            $"3. Link content via Tools > Vellocet > SDK > Link SDK Content, then click Sync Now.{Environment.NewLine}   Content path: {contentDirectory ?? "Missing SDKContent directory"}");
        lines.Add("4. Open Tools > Vellocet > SDK > Editor.");
        lines.Add("5. In the SDK window, open Map > Validate and resolve all errors before export.");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildDiagnostics(
        string installRoot,
        string? packageDirectory,
        IReadOnlyList<string> packageFiles,
        string? selectedPackage,
        string? contentDirectory,
        ManifestInspection manifest,
        string? documentationFile)
    {
        var lines = new List<string>
        {
            $"Install Root: {installRoot}",
            $"SDK Package Directory: {packageDirectory ?? "Missing"}",
            $"SDK Package Files Found: {packageFiles.Count}",
            $"Selected SDK Package: {selectedPackage ?? "Missing"}",
            $"SDK Content Directory: {contentDirectory ?? "Missing"}",
            $"SDK Content Manifest: {manifest.Path ?? "Missing"}",
            $"SDK Content Manifest Parsed: {(manifest.IsParsed ? "Yes" : "No")}",
            $"SDK Content Manifest Schema: {manifest.SchemaVersion?.ToString() ?? "Unknown"}",
            $"SDK Content Manifest Entries: {manifest.EntryCount}",
            $"Documentation: {documentationFile ?? "Missing"}"
        };

        if (!string.IsNullOrWhiteSpace(manifest.ParseError)) lines.Add($"Manifest Parse Error: {manifest.ParseError}");

        return string.Join(Environment.NewLine, lines);
    }

    private sealed record CheckResult(string Label, bool Passed, string Detail, bool Required);

    private sealed record ManifestInspection(
        bool Exists,
        bool IsParsed,
        string? Path,
        int? SchemaVersion,
        int EntryCount,
        string? ParseError)
    {
        public static ManifestInspection Missing(string? path = null)
        {
            return new ManifestInspection(false, false, path, null, 0, null);
        }

        public static ManifestInspection FromParsed(string path, int? schemaVersion, int entryCount)
        {
            return new ManifestInspection(true, true, path, schemaVersion, entryCount, null);
        }

        public static ManifestInspection ParseFailed(string path, string parseError)
        {
            return new ManifestInspection(true, false, path, null, 0, parseError);
        }
    }
}

internal sealed record LauncherStatusSnapshot(
    bool IsReady,
    string Summary,
    string Checklist,
    string Guide,
    string Diagnostics);