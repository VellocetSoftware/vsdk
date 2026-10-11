// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor.Addons
{
    internal static class AddonPackageStagingUtility
    {
        internal static bool TryCopyExistingPackage(string sourceRoot, string destinationRoot,
            Func<string, bool> shouldSkipRelativePath, out string error)
        {
            return TryCopyPackage(sourceRoot, destinationRoot, shouldSkipRelativePath, true, out error);
        }

        internal static bool TryCopyPackageForManifestRepair(string sourceRoot, string destinationRoot,
            Func<string, bool> shouldSkipRelativePath, out string error)
        {
            return TryCopyPackage(sourceRoot, destinationRoot, shouldSkipRelativePath, false, out error);
        }

        private static bool TryCopyPackage(string sourceRoot, string destinationRoot,
            Func<string, bool> shouldSkipRelativePath, bool requireValidManifest, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
                return true;

            if (requireValidManifest && !AddonHelper.TryLoad(sourceRoot, out _, out var manifestError))
            {
                error = $"Existing add-on '{sourceRoot}' cannot be updated: {manifestError}";
                return false;
            }

            try
            {
                var sourceInfo = new DirectoryInfo(sourceRoot);
                if (IsLink(sourceInfo))
                {
                    error = "Existing add-on root cannot be a symbolic link or reparse point.";
                    return false;
                }

                var pending = new Stack<DirectoryInfo>();
                pending.Push(sourceInfo);
                while (pending.Count > 0)
                {
                    var directory = pending.Pop();
                    foreach (var child in directory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        if (IsLink(child))
                        {
                            error = $"Existing add-on directory '{child.Name}' is a symbolic link or reparse point.";
                            return false;
                        }

                        pending.Push(child);
                    }

                    foreach (var file in directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                    {
                        if (IsLink(file))
                        {
                            error = $"Existing add-on file '{file.Name}' is a symbolic link or reparse point.";
                            return false;
                        }

                        if (SdkContentPathUtility.ShouldSkipFile(file.FullName))
                            continue;

                        var relativePath = Path.GetRelativePath(sourceRoot, file.FullName).Replace('\\', '/');
                        if (shouldSkipRelativePath?.Invoke(relativePath) == true)
                            continue;

                        var destinationPath = Path.Combine(destinationRoot,
                            relativePath.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationRoot);
                        File.Copy(file.FullName, destinationPath, true);
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not stage the existing add-on: {exception.Message}";
                return false;
            }
        }

        private static bool IsLink(FileSystemInfo info)
        {
            return (info.Attributes & FileAttributes.ReparsePoint) != 0;
        }
    }
}
#endif