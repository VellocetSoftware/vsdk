// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Addons
{
    /// <summary>
    ///     Source-controlled authoring definition for one addon package. Build destinations and local build
    ///     preferences remain editor-local so this asset can be shared by a team.
    /// </summary>
    [CreateAssetMenu(fileName = "AddonProject", menuName = "Vellocet/SDK/Add-on Project", order = 100)]
    public sealed class AddonAuthoringProject : ScriptableObject
    {
        [SerializeField] private string addonId = string.Empty;
        [SerializeField] private string title = "New Add-on";
        [SerializeField] private string author = string.Empty;
        [SerializeField] private string version = "1.0.0";
        [SerializeField] private Texture2D addonThumbnail;
        [SerializeField] private string[] requiredAddonIds = Array.Empty<string>();
        [SerializeField] private Object[] contentSources = Array.Empty<Object>();

        public string AddonId => AddonIdPolicy.NormalizeOrEmpty(addonId);
        public string Title => title?.Trim() ?? string.Empty;
        public string Author => author?.Trim() ?? string.Empty;
        public string Version => version?.Trim() ?? string.Empty;
        public Texture2D AddonThumbnail => addonThumbnail;
        public string[] RequiredAddonIds => requiredAddonIds ?? Array.Empty<string>();
        public Object[] ContentSources => contentSources ?? Array.Empty<Object>();
    }

    public static class AddonAuthoringProjectPolicy
    {
        public static bool TryValidateIdentity(AddonAuthoringProject project, out string error)
        {
            error = null;
            if (!project)
            {
                error = "Select an Add-on Project.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(project.AddonId))
            {
                error = "Add-on ID must be a lowercase dotted ID such as 'author.package'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(project.Title))
            {
                error = "Add-on title is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(project.Version))
            {
                error = "Add-on version is required.";
                return false;
            }

            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < project.RequiredAddonIds.Length; index++)
            {
                var dependency = AddonIdPolicy.NormalizeOrEmpty(project.RequiredAddonIds[index]);
                if (string.IsNullOrWhiteSpace(dependency))
                {
                    error = $"Required add-on ID at position {index + 1} is invalid.";
                    return false;
                }

                if (string.Equals(dependency, project.AddonId, StringComparison.OrdinalIgnoreCase))
                {
                    error = "An add-on cannot require itself.";
                    return false;
                }

                if (!dependencies.Add(dependency))
                {
                    error = $"Required add-on '{dependency}' is listed more than once.";
                    return false;
                }
            }

            return true;
        }
    }
}