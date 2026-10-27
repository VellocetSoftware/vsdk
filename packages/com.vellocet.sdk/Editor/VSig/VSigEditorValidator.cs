// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using Vertex.VSig;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Entities;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.VSig;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.VSig
{
    public static class VSigEditorValidator
    {
        public static EntityMarker[] GetSceneMarkers(Scene scene, bool includeInactive)
        {
            var mode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
            var all = Object.FindObjectsByType<EntityMarker>(mode, FindObjectsSortMode.None);
            var result = new List<(EntityMarker Marker, string Path)>(all.Length);
            for (var i = 0; i < all.Length; i++)
            {
                var marker = all[i];
                if (marker && marker.gameObject.scene.handle == scene.handle)
                    result.Add((marker, GetHierarchyPath(marker.transform)));
            }

            result.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
            var markers = new EntityMarker[result.Count];
            for (var i = 0; i < markers.Length; i++) markers[i] = result[i].Marker;
            return markers;
        }

        public static string GenerateRefBlock(Scene scene, bool includeInactive)
        {
            var markers = GetSceneMarkers(scene, includeInactive);
            return string.Join("\n", GenerateRefLines(markers, false));
        }

        public static string GenerateScaffold(Scene scene, bool includeInactive)
        {
            var markers = GetSceneMarkers(scene, includeInactive);
            var lines = new List<string>(markers.Length * 4 + 16)
            {
                "# VSig scaffold",
                "# Keep refs you use, remove anything you do not need.",
                string.Empty
            };

            var refs = GenerateRefLines(markers, true);
            lines.AddRange(refs);

            if (refs.Count > 0)
                lines.Add(string.Empty);

            lines.Add("# Map lifecycle");
            lines.Add("once on map.ready:");
            lines.Add("    # Initialize persistent map state here.");
            lines.Add(string.Empty);
            lines.Add("on round.live:");
            lines.Add("    # Start round logic here.");
            lines.Add(string.Empty);
            lines.Add("on round.post_round:");
            lines.Add("    # Cancel tasks and present results here.");

            return string.Join("\n", lines).TrimEnd() + "\n";
        }

        private static List<string> GenerateRefLines(IReadOnlyList<EntityMarker> markers, bool includeContracts)
        {
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var conflictedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = new List<string>(markers.Count);
            for (var i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                if (marker is LogicScriptMarker)
                    continue;
                var alias = VSigSceneCompiler.BuildAlias(marker);
                var typeName = marker.EntityClassName;
                if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(typeName))
                    continue;

                var key = $"{alias}:{typeName}";
                if (!emitted.Add(key))
                    continue;

                if (!usedAliases.Add(alias))
                {
                    if (conflictedAliases.Add(alias))
                        lines.Add(
                            $"# Name conflict: '{alias}' is used by multiple entity classes. Rename one GameObject.");
                    continue;
                }

                lines.Add($"ref {alias}: {typeName}");
                if (!includeContracts)
                    continue;

                var contract = marker.SignalContract;
                AddContractComment(lines, "outputs", EntitySignalContractFormatter.Names(contract.Outputs));
                AddContractComment(lines, "inputs", EntitySignalContractFormatter.Names(contract.Inputs));
                AddContractComment(lines, "properties", EntitySignalContractFormatter.Names(contract.Properties));
                AddContractComment(lines, "state", EntitySignalContractFormatter.Names(contract.States));
            }

            return lines;
        }

        private static void AddContractComment(List<string> lines, string label, string[] names)
        {
            if (names == null || names.Length == 0)
                return;

            lines.Add($"#   {label}: {string.Join(", ", names)}");
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (!transform)
                return string.Empty;

            var path = transform.name;
            while (transform.parent)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        public sealed class Result
        {
            public readonly List<VSigDiagnostic> Diagnostics = new();

            public bool HasErrors
            {
                get
                {
                    for (var i = 0; i < Diagnostics.Count; i++)
                        if (Diagnostics[i]?.Severity == VSigDiagnosticSeverity.Error)
                            return true;

                    return false;
                }
            }
        }
    }
}
#endif
