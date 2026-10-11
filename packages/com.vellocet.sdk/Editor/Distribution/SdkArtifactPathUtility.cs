using System;
using System.IO;
using UnityEngine;

namespace Vellocet.Sdk.Editor.Distribution
{
    public static class SdkArtifactPathUtility
    {
        public static string NormalizeRepositoryRelative(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Replace('\\', '/').Trim().TrimStart('/');
            return normalized.Contains("..", StringComparison.Ordinal) ? string.Empty : normalized;
        }

        public static string GetRepositoryRoot()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                              ?? throw new InvalidOperationException("Project root could not be resolved.");

            return Path.GetFullPath(Path.Combine(projectRoot, ".."));
        }

        public static string ToAbsolutePath(string repositoryRelativePath)
        {
            var normalized = NormalizeRepositoryRelative(repositoryRelativePath);
            return Path.GetFullPath(Path.Combine(
                GetRepositoryRoot(),
                normalized.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static string ToRepositoryRelativePath(string absolutePath)
        {
            var repositoryRoot = EnsureTrailingSeparator(GetRepositoryRoot());
            var normalizedAbsolute = Path.GetFullPath(absolutePath);
            if (!normalizedAbsolute.StartsWith(repositoryRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Path is outside repository root: {absolutePath}");

            return normalizedAbsolute[repositoryRoot.Length..].Replace('\\', '/');
        }

        public static void EnsurePathWithinRepository(string absolutePath)
        {
            var repositoryRoot = EnsureTrailingSeparator(GetRepositoryRoot());
            var path = Path.GetFullPath(absolutePath);
            if (!path.StartsWith(repositoryRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Path is outside repository root: {absolutePath}");
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
    }
}
