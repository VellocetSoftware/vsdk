// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Addons
{
    /// <summary>Context passed to an editor exporter for one source in an Add-on Project.</summary>
    public sealed class AddonContentExportContext
    {
        internal AddonContentExportContext(AddonAuthoringProject project, string addonRoot, bool release)
        {
            Project = project;
            AddonRoot = addonRoot ?? string.Empty;
            IsRelease = release;
        }

        public AddonAuthoringProject Project { get; }
        public string AddonId => Project ? Project.AddonId : string.Empty;
        public string AddonRoot { get; }
        public bool IsRelease { get; }
    }

    /// <summary>
    ///     Editor extension point for an authoring asset that produces one supported addon manifest entry.
    ///     A new runtime content kind still requires matching manifest, validation, delivery, and loader support.
    /// </summary>
    public interface IAddonContentSourceExporter
    {
        Type SourceType { get; }
        string ContentKind { get; }
        string DisplayName { get; }

        bool TryValidate(Object source, AddonContentExportContext context, out string error);

        bool TryExport(Object source, AddonContentExportContext context,
            out AddonManifest.ContentEntry content, out string[] requiredAddonIds, out string error);
    }

    internal static class AddonContentSourceExporterRegistry
    {
        private static IReadOnlyList<IAddonContentSourceExporter> _exporters;

        internal static IReadOnlyList<IAddonContentSourceExporter> Exporters =>
            _exporters ??= DiscoverExporters();

        internal static bool TryResolve(Object source, out IAddonContentSourceExporter exporter,
            out string error)
        {
            exporter = null;
            error = null;
            if (!source)
            {
                error = "Content source is missing.";
                return false;
            }

            var sourceType = source.GetType();
            var candidates = Exporters
                .Where(value => value?.SourceType != null && value.SourceType.IsAssignableFrom(sourceType))
                .OrderBy(value => value.SourceType == sourceType ? 0 : 1)
                .ThenBy(value => value.GetType().FullName, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 0)
            {
                error = $"No add-on content exporter supports '{sourceType.Name}'.";
                return false;
            }

            var bestRank = candidates[0].SourceType == sourceType ? 0 : 1;
            if (candidates.Count(value => (value.SourceType == sourceType ? 0 : 1) == bestRank) > 1)
            {
                error = $"More than one add-on content exporter supports '{sourceType.Name}'.";
                return false;
            }

            exporter = candidates[0];
            return true;
        }

        internal static void Invalidate()
        {
            _exporters = null;
        }

        private static IReadOnlyList<IAddonContentSourceExporter> DiscoverExporters()
        {
            var result = new List<IAddonContentSourceExporter>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IAddonContentSourceExporter>()
                         .Where(value => value is { IsAbstract: false, IsInterface: false })
                         .OrderBy(value => value.FullName, StringComparer.Ordinal))
                try
                {
                    if (Activator.CreateInstance(type) is IAddonContentSourceExporter exporter)
                        result.Add(exporter);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Add-on Content] Could not create exporter '{type.FullName}': " +
                                   exception.Message);
                }

            return result;
        }
    }
}
#endif