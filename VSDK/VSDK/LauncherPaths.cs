namespace VSDK;

internal sealed class LauncherPaths
{
    private const int MaxParentSearchDepth = 8;

    private static readonly string[] DistributionFolderMarkers =
    {
        "SDKContent",
        "VellocetSDKContent",
        "SDKPackage",
        "UnityPackage",
        "Docs",
        "Samples"
    };

    public LauncherPaths(string executableDirectory)
    {
        ExecutableDirectory = Path.GetFullPath(executableDirectory);
        InstallRoot = DetectInstallRoot(ExecutableDirectory);

        PackageDirectoryCandidates =
        [
            Path.Combine(InstallRoot, "SDKPackage"),
            Path.Combine(InstallRoot, "UnityPackage")
        ];

        ContentDirectoryCandidates =
        [
            Path.Combine(InstallRoot, "SDKContent"),
            Path.Combine(InstallRoot, "VellocetSDKContent")
        ];

        DocumentationFileCandidates =
        [
            Path.Combine(InstallRoot, "Docs", "index.html"),
            Path.Combine(InstallRoot, "README.txt"),
            Path.Combine(InstallRoot, "README.md")
        ];
    }

    public string ExecutableDirectory { get; }
    public string InstallRoot { get; }
    public IReadOnlyList<string> PackageDirectoryCandidates { get; }
    public IReadOnlyList<string> ContentDirectoryCandidates { get; }
    public IReadOnlyList<string> DocumentationFileCandidates { get; }

    public static string? ResolveFirstExistingDirectory(IEnumerable<string> candidates)
    {
        foreach (var path in candidates)
            if (Directory.Exists(path))
                return path;

        return null;
    }

    public static string? ResolveFirstExistingFile(IEnumerable<string> candidates)
    {
        foreach (var path in candidates)
            if (File.Exists(path))
                return path;

        return null;
    }

    private static string DetectInstallRoot(string startingDirectory)
    {
        var currentDirectory = new DirectoryInfo(startingDirectory);
        for (var depth = 0; depth < MaxParentSearchDepth && currentDirectory is not null; depth++)
        {
            if (TryResolveDistributionRoot(currentDirectory.FullName, out var distributionRoot))
                return distributionRoot;

            currentDirectory = currentDirectory.Parent;
        }

        return startingDirectory;
    }

    private static bool TryResolveDistributionRoot(string candidateRoot, out string distributionRoot)
    {
        if (LooksLikeDistributionRoot(candidateRoot))
        {
            distributionRoot = candidateRoot;
            return true;
        }

        var nestedBuildRoot = Path.Combine(candidateRoot, "Build");
        if (LooksLikeDistributionRoot(nestedBuildRoot))
        {
            distributionRoot = nestedBuildRoot;
            return true;
        }

        distributionRoot = string.Empty;
        return false;
    }

    private static bool LooksLikeDistributionRoot(string path)
    {
        if (!Directory.Exists(path)) return false;

        foreach (var marker in DistributionFolderMarkers)
            if (Directory.Exists(Path.Combine(path, marker)))
                return true;

        return File.Exists(Path.Combine(path, "README.txt")) ||
               File.Exists(Path.Combine(path, "README.md"));
    }
}