// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class AddonPackagePromoter
    {
        internal static bool TryPromote(string stagingAddonRoot, string finalAddonRoot, string buildId,
            out string error, out bool preserveStaging)
        {
            error = null;
            preserveStaging = false;
            if (string.IsNullOrWhiteSpace(stagingAddonRoot) || !Directory.Exists(stagingAddonRoot) ||
                string.IsNullOrWhiteSpace(finalAddonRoot))
            {
                error = "Validated package staging output is missing.";
                return false;
            }

            var stagingContainer = Path.GetDirectoryName(stagingAddonRoot);
            if (string.IsNullOrWhiteSpace(stagingContainer))
            {
                error = "Package staging container could not be resolved.";
                return false;
            }

            var backupRoot = Path.Combine(stagingContainer, $".previous-{buildId}");
            var movedPrevious = false;
            try
            {
                if (Directory.Exists(backupRoot))
                {
                    error = $"Package backup path already exists: '{backupRoot}'.";
                    return false;
                }

                if (Directory.Exists(finalAddonRoot))
                {
                    Directory.Move(finalAddonRoot, backupRoot);
                    movedPrevious = true;
                }

                Directory.Move(stagingAddonRoot, finalAddonRoot);
            }
            catch (Exception ex)
            {
                var restoreError = string.Empty;
                if (movedPrevious && !Directory.Exists(finalAddonRoot) && Directory.Exists(backupRoot))
                    try
                    {
                        Directory.Move(backupRoot, finalAddonRoot);
                    }
                    catch (Exception restoreException)
                    {
                        preserveStaging = true;
                        restoreError =
                            $" The previous package remains recoverable at '{backupRoot}', but automatic restoration failed: {restoreException.Message}";
                    }

                error = $"Could not replace the package output: {ex.Message}{restoreError}";
                return false;
            }

            return true;
        }

        internal static void CleanupStaging(string stagingRoot, IMapBuildProgressSink progress)
        {
            if (string.IsNullOrWhiteSpace(stagingRoot) || !Directory.Exists(stagingRoot))
                return;

            var folderName = Path.GetFileName(stagingRoot);
            if (string.IsNullOrWhiteSpace(folderName) || !folderName.Contains(".staging-", StringComparison.Ordinal))
            {
                progress?.Log($"Skipped cleanup for unexpected staging path '{stagingRoot}'.");
                return;
            }

            try
            {
                Directory.Delete(stagingRoot, true);
            }
            catch (Exception ex)
            {
                progress?.Log($"Package staging cleanup warning: {ex.Message}");
                Debug.LogWarning(
                    $"[Map Builder] Could not remove package staging folder '{stagingRoot}': {ex.Message}");
            }
        }
    }
}
#endif