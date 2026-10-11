// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;

namespace Vellocet.Sdk.Addons
{
    /// <summary>Shared path contract for server-mod content inside an addon package.</summary>
    public static class AddonModContentPolicy
    {
        public static bool TryResolveManifest(string addonRoot, AddonManifest.ContentEntry content,
            out string manifestPath, out string error)
        {
            manifestPath = null;
            error = null;
            var contentId = string.IsNullOrWhiteSpace(content?.id) ? "<missing>" : content.id;
            var relativePath = content?.mod?.manifest;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                error = $"Mod '{contentId}' is missing its mod manifest path.";
                return false;
            }

            if (!AddonHelper.TryResolvePath(addonRoot, relativePath, out manifestPath, out _) ||
                !string.Equals(Path.GetFileName(manifestPath), AddonManifest.ModManifestFileName,
                    StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(manifestPath))
            {
                manifestPath = null;
                error = $"Mod '{contentId}' manifest is missing or has an invalid path.";
                return false;
            }

            return true;
        }
    }
}