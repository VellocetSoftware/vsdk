// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapExportAddressablesProfilePolicy
    {
        private const string ProfileName = "MapExport";
        private const string RemoteBuildPath = "Library/MapExporterBuild/[BuildTarget]";
        private const string RemoteLoadPath = "Addons/maps/<map-id>/[BuildTarget]";
        private const string LocalBuildPath = "Library/MapExporterBuild/[BuildTarget]";
        private const string LocalLoadPath = "Addons/maps/<map-id>/[BuildTarget]";

        internal static bool Ensure(AddressableAssetSettings settings, bool activate, out string profileId)
        {
            profileId = null;
            if (!settings || settings.profileSettings == null)
                return false;

            var profiles = settings.profileSettings;
            profileId = profiles.GetProfileId(ProfileName);
            var changed = false;
            if (string.IsNullOrWhiteSpace(profileId))
            {
                profileId = profiles.AddProfile(ProfileName, settings.activeProfileId);
                changed = true;
            }

            changed |= EnsureVariable(profiles, Constants.RemoteBuildVar, RemoteBuildPath);
            changed |= EnsureVariable(profiles, Constants.RemoteLoadVar, RemoteLoadPath);
            changed |= EnsureVariable(profiles, Constants.LocalBuildVar, LocalBuildPath);
            changed |= EnsureVariable(profiles, Constants.LocalLoadVar, LocalLoadPath);
            changed |= SetValueIfChanged(profiles, profileId, Constants.RemoteBuildVar, RemoteBuildPath);
            changed |= SetValueIfChanged(profiles, profileId, Constants.RemoteLoadVar, RemoteLoadPath);
            changed |= SetValueIfChanged(profiles, profileId, Constants.LocalBuildVar, LocalBuildPath);
            changed |= SetValueIfChanged(profiles, profileId, Constants.LocalLoadVar, LocalLoadPath);

            if (activate && !string.Equals(settings.activeProfileId, profileId, StringComparison.Ordinal))
            {
                settings.activeProfileId = profileId;
                changed = true;
            }

            if (changed)
            {
                MarkGroupsDirty(settings);
                EditorUtility.SetDirty(settings);
            }

            return changed;
        }

        private static bool EnsureVariable(AddressableAssetProfileSettings profiles, string variableName,
            string defaultValue)
        {
            if (profiles == null || string.IsNullOrWhiteSpace(variableName))
                return false;

            var names = profiles.GetVariableNames();
            if (names != null && names.Contains(variableName))
                return false;

            profiles.CreateValue(variableName, defaultValue ?? string.Empty);
            return true;
        }

        private static bool SetValueIfChanged(AddressableAssetProfileSettings profiles, string profileId,
            string variableName, string value)
        {
            if (profiles == null || string.IsNullOrWhiteSpace(profileId) ||
                string.IsNullOrWhiteSpace(variableName))
                return false;

            var current = profiles.GetValueById(profileId, variableName);
            if (string.Equals(current, value, StringComparison.Ordinal))
                return false;

            profiles.SetValue(profileId, variableName, value ?? string.Empty);
            return true;
        }

        internal static void MarkGroupsDirty(AddressableAssetSettings settings)
        {
            if (!settings || settings.groups == null)
                return;

            foreach (var group in settings.groups)
                if (group)
                    EditorUtility.SetDirty(group);
        }
    }
}
#endif