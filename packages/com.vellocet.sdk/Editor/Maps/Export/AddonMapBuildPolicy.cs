// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;
using Vellocet.Sdk.Addons;

namespace Vellocet.Sdk.Editor.Maps
{
    public enum AddonMapBuildProfile
    {
        Development = 0,
        Release = 1
    }

    /// <summary>
    ///     Map-package targets, roles and release requirements.
    /// </summary>
    public static class AddonMapBuildPolicy
    {
        private static readonly ReadOnlyCollection<BuildTarget> ReleaseTargetsValue =
            Array.AsReadOnly(new[]
            {
                BuildTarget.StandaloneWindows64,
                BuildTarget.StandaloneOSX,
                BuildTarget.StandaloneLinux64
            });

        public static IReadOnlyList<BuildTarget> ReleaseTargets => ReleaseTargetsValue;

        public static bool RequiresNonDevelopmentBuild(AddonMapBuildProfile profile)
        {
            return profile == AddonMapBuildProfile.Release;
        }

        public static string DistributionProfile(AddonMapBuildProfile profile)
        {
            return profile == AddonMapBuildProfile.Release
                ? AddonDistributionPolicy.ReleaseProfile
                : AddonDistributionPolicy.DevelopmentProfile;
        }

        public static string DistributionPolicy(AddonMapBuildProfile profile)
        {
            return profile == AddonMapBuildProfile.Release
                ? AddonDistributionPolicy.ReleasePolicyV1
                : string.Empty;
        }

        public static bool TryNormalizeTargets(IReadOnlyList<BuildTarget> targets,
            out IReadOnlyList<BuildTarget> normalizedTargets, out string error)
        {
            normalizedTargets = Array.Empty<BuildTarget>();
            error = null;
            if (targets == null || targets.Count == 0)
            {
                error = "Select at least one build target.";
                return false;
            }

            var requested = new HashSet<BuildTarget>();
            foreach (var target in targets)
            {
                if (!IsSupportedTarget(target))
                {
                    error = $"Map package builds do not support target '{target}'.";
                    return false;
                }

                if (!requested.Add(target))
                {
                    error = $"Build target '{target}' was selected more than once.";
                    return false;
                }
            }

            normalizedTargets = ReleaseTargetsValue.Where(requested.Contains).ToArray();
            return true;
        }

        public static bool TryValidateReleaseRequest(AddonMapBuildRequest request, out string error)
        {
            error = null;
            if (request == null)
            {
                error = "Build request is required.";
                return false;
            }

            if (!TryNormalizeTargets(request.Targets, out var targets, out error))
                return false;

            if (targets.Count != ReleaseTargetsValue.Count ||
                ReleaseTargetsValue.Any(required => !targets.Contains(required)))
            {
                error =
                    "Release packages require Windows Client, macOS Client, and Linux Dedicated Server.";
                return false;
            }

            if (request.SkipLightingBake || request.SkipSpatialOctreeBake ||
                !request.ConfigureToolVisuals || !request.PruneGeneratedMeshes)
            {
                error =
                    "Release packages require full lighting and spatial octree bakes, finalized tool visuals, and pruned generated meshes.";
                return false;
            }

            if (!AreReleaseTargetsSupported(out var missingTargets))
            {
                error = $"Release packages require all desktop build modules. Missing: {missingTargets}.";
                return false;
            }

            return true;
        }

        public static bool AreReleaseTargetsSupported(out string missingTargets)
        {
            var missing = ReleaseTargetsValue
                .Where(target => !IsBuildTargetSupported(target))
                .Select(PlatformDisplayLabel)
                .ToArray();
            missingTargets = string.Join(", ", missing);
            return missing.Length == 0;
        }

        public static bool IsBuildTargetSupported(BuildTarget target)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            return BuildPipeline.IsBuildTargetSupported(group, target);
        }

        public static string PlatformName(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows64 => AddonDistributionPolicy.WindowsClientPlatform,
                BuildTarget.StandaloneOSX => AddonDistributionPolicy.MacOSClientPlatform,
                BuildTarget.StandaloneLinux64 => AddonDistributionPolicy.LinuxServerPlatform,
                _ => target.ToString()
            };
        }

        public static string PlatformRole(BuildTarget target)
        {
            return target == BuildTarget.StandaloneLinux64
                ? AddonDistributionPolicy.ServerRole
                : AddonDistributionPolicy.ClientRole;
        }

        public static string PlatformDisplayLabel(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows64 => "Windows Client",
                BuildTarget.StandaloneOSX => "macOS Client",
                BuildTarget.StandaloneLinux64 => "Linux Dedicated Server",
                _ => target.ToString()
            };
        }

        private static bool IsSupportedTarget(BuildTarget target)
        {
            return target is BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneOSX or
                BuildTarget.StandaloneLinux64;
        }
    }
}
#endif