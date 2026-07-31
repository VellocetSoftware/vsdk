// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

namespace VSDK;

internal sealed class LauncherPaths
{
    private const int MaxParentSearchDepth = 8;

    private static readonly string[] DistributionFolderMarkers =
    {
        "SDKContent",
        "SDKPackage"
    };

    public LauncherPaths(string executableDirectory)
    {
        ExecutableDirectory = Path.GetFullPath(executableDirectory);
        InstallRoot = DetectInstallRoot(ExecutableDirectory);

        PackageDirectoryCandidates =
        [
            Path.Combine(InstallRoot, "SDKPackage")
        ];

        ContentDirectoryCandidates =
        [
            Path.Combine(InstallRoot, "SDKContent")
        ];

    }

    public string ExecutableDirectory { get; }
    public string InstallRoot { get; }
    public IReadOnlyList<string> PackageDirectoryCandidates { get; }
    public IReadOnlyList<string> ContentDirectoryCandidates { get; }

    public static string? ResolveFirstExistingDirectory(IEnumerable<string> candidates)
    {
        foreach (var path in candidates)
            if (Directory.Exists(path))
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

        return false;
    }
}
