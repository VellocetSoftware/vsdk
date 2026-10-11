// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using Vertex.VSig;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.VSig;

namespace Vellocet.Sdk.Editor.VSig
{
    public static class VSigRouteModelBuilder
    {
        public static bool TryFindSceneSource(Scene scene, out TextAsset source)
        {
            source = null;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
                return false;

            return VSigMapSourceUtility.TryFindForScene(scene.path, out source, out _);
        }

        public static VSigRouteModel Build(Scene scene, bool includeInactiveMarkers)
        {
            if (!TryFindSceneSource(scene, out var source))
                return VSigRouteModel.Empty(scene, null);

            var markers = VSigEditorValidator.GetSceneMarkers(scene, includeInactiveMarkers);
            return Build(scene, source, markers);
        }

        public static VSigRouteModel Build(Scene scene, TextAsset source, IReadOnlyList<EntityMarker> markers)
        {
            var diagnostics = new List<VSigDiagnostic>();
            if (!source)
                return VSigRouteModel.Empty(scene, diagnostics);

            var sourcePath = AssetDatabase.GetAssetPath(source);
            var program = VSigSceneCompiler.Parse(source.text, diagnostics, markers, sourcePath);
            if (program == null)
                return VSigRouteModel.Empty(scene, diagnostics);

            var refs = VSigSceneCompiler.ResolveAliases(program, markers, diagnostics);
            var routes = BuildRoutes(program, refs);
            var markerIdsByModuleRoot = BuildModuleRootMarkerMap(scene, program, refs, markers);
            return new VSigRouteModel(scene, source, program, diagnostics, routes, markerIdsByModuleRoot);
        }

        private static List<VSigRouteModel.Route> BuildRoutes(VSigProgram program,
            IReadOnlyDictionary<string, List<EntityMarker>> refs)
        {
            var routes = new List<VSigRouteModel.Route>(64);
            var procedures = BuildProcedureIndex(program);

            foreach (var handler in program.Handlers)
            {
                if (!refs.TryGetValue(handler.SourceAlias, out var sources) || sources == null || sources.Count == 0)
                    continue;

                var calls = new List<VSigCallStatement>(8);
                CollectCalls(handler.Statements, procedures, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    calls);

                foreach (var call in calls)
                {
                    if (!refs.TryGetValue(call.TargetAlias, out var targets) || targets == null || targets.Count == 0)
                        continue;

                    for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                    {
                        var source = sources[sourceIndex];
                        if (!source)
                            continue;

                        for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                        {
                            var target = targets[targetIndex];
                            if (!target)
                                continue;

                            routes.Add(new VSigRouteModel.Route(source, target, handler.SourceAlias, handler.OutputName,
                                call.TargetAlias, call.InputName));
                        }
                    }
                }
            }

            return routes;
        }

        private static Dictionary<string, VSigProcedureDeclaration> BuildProcedureIndex(VSigProgram program)
        {
            var result = new Dictionary<string, VSigProcedureDeclaration>(StringComparer.OrdinalIgnoreCase);
            foreach (var procedure in program.Procedures)
            {
                if (procedure == null || string.IsNullOrWhiteSpace(procedure.Name))
                    continue;

                result[VSigNameUtility.ProcedureKey(procedure.InstanceAlias, procedure.Name)] = procedure;
            }

            return result;
        }

        private static void CollectCalls(IReadOnlyList<VSigStatement> statements,
            IReadOnlyDictionary<string, VSigProcedureDeclaration> procedures, ISet<string> callStack,
            ICollection<VSigCallStatement> output)
        {
            if (statements == null)
                return;

            foreach (var statement in statements)
                switch (statement)
                {
                    case VSigCallStatement call:
                        output.Add(call);
                        break;
                    case VSigProcedureCallStatement procedureCall:
                        FollowProcedureCall(procedureCall, procedures, callStack, output);
                        break;
                    case VSigIfStatement conditional:
                        CollectCalls(conditional.Then, procedures, callStack, output);
                        CollectCalls(conditional.Else, procedures, callStack, output);
                        break;
                    case VSigMatchStatement match:
                        foreach (var item in match.Cases)
                            CollectCalls(item.Statements, procedures, callStack, output);
                        break;
                    case VSigScheduleStatement schedule:
                        CollectCalls(schedule.Statements, procedures, callStack, output);
                        break;
                }
        }

        private static void FollowProcedureCall(VSigProcedureCallStatement procedureCall,
            IReadOnlyDictionary<string, VSigProcedureDeclaration> procedures, ISet<string> callStack,
            ICollection<VSigCallStatement> output)
        {
            if (procedureCall == null || string.IsNullOrWhiteSpace(procedureCall.ProcedureName))
                return;

            var key = VSigNameUtility.ProcedureKey(procedureCall.TargetAlias, procedureCall.ProcedureName);
            if (!procedures.TryGetValue(key, out var procedure) || !callStack.Add(key))
                return;

            try
            {
                CollectCalls(procedure.Statements, procedures, callStack, output);
            }
            finally
            {
                callStack.Remove(key);
            }
        }

        private static Dictionary<int, int[]> BuildModuleRootMarkerMap(Scene scene, VSigProgram program,
            IReadOnlyDictionary<string, List<EntityMarker>> refs, IReadOnlyList<EntityMarker> markers)
        {
            var result = new Dictionary<int, int[]>();
            if (program == null || program.Uses.Count == 0)
                return result;

            var transformByPath = BuildTransformPathMap(scene);
            var markersById = new Dictionary<Guid, EntityMarker>();
            foreach (var marker in markers ?? Array.Empty<EntityMarker>())
                if (marker) markersById.TryAdd(marker.Guid, marker);
            foreach (var use in program.Uses)
            {
                if (string.IsNullOrWhiteSpace(use?.Alias))
                    continue;

                var normalizedPath = NormalizePath(use.RootPath);
                Transform root = null;
                if (Guid.TryParse(use.RootAnchorMarkerGuid, out var anchor) && markersById.TryGetValue(anchor, out var owner))
                {
                    root = owner.transform;
                    while (root && !string.IsNullOrEmpty(normalizedPath) &&
                           !string.Equals(NormalizePath(GetHierarchyPath(root)), normalizedPath, StringComparison.OrdinalIgnoreCase))
                        root = root.parent;
                }
                else if (!string.IsNullOrEmpty(normalizedPath)) transformByPath.TryGetValue(normalizedPath, out root);
                if (!root) continue;

                var markerIds = CollectMarkerIdsForInstanceAlias(use.Alias, refs);
                if (markerIds.Length == 0)
                    continue;

                result[root.GetInstanceID()] = markerIds;
            }

            return result;
        }

        private static int[] CollectMarkerIdsForInstanceAlias(string instanceAlias,
            IReadOnlyDictionary<string, List<EntityMarker>> refs)
        {
            var ids = new List<int>();
            var seen = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(instanceAlias))
                return ids.ToArray();

            foreach (var kvp in refs)
            {
                if (!IsInstanceScopedAlias(instanceAlias, kvp.Key))
                    continue;

                var markers = kvp.Value;
                if (markers == null)
                    continue;

                foreach (var marker in markers)
                {
                    if (!marker)
                        continue;

                    var id = marker.GetInstanceID();
                    if (seen.Add(id))
                        ids.Add(id);
                }
            }

            return ids.ToArray();
        }

        private static bool IsInstanceScopedAlias(string instanceAlias, string candidateAlias)
        {
            if (string.IsNullOrWhiteSpace(instanceAlias) || string.IsNullOrWhiteSpace(candidateAlias))
                return false;

            return candidateAlias.Length > instanceAlias.Length + 1 &&
                   candidateAlias.StartsWith(instanceAlias, StringComparison.OrdinalIgnoreCase) &&
                   candidateAlias[instanceAlias.Length] == '_';
        }

        private static Dictionary<string, Transform> BuildTransformPathMap(Scene scene)
        {
            var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                RegisterRecursive(root.transform, map);
            }

            return map;
        }

        private static void RegisterRecursive(Transform transform, IDictionary<string, Transform> map)
        {
            if (!transform)
                return;

            map[NormalizePath(GetHierarchyPath(transform))] = transform;
            for (var i = 0; i < transform.childCount; i++)
                RegisterRecursive(transform.GetChild(i), map);
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

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var parts = path.Replace('\\', '/').Split('/');
            for (var i = 0; i < parts.Length; i++)
                parts[i] = EntitySignalNameUtility.Normalize(parts[i]);
            return string.Join("/", parts);
        }
    }

    public sealed class VSigRouteModel
    {
        public VSigRouteModel(Scene scene, TextAsset source, VSigProgram program, List<VSigDiagnostic> diagnostics,
            List<Route> routes, Dictionary<int, int[]> markerIdsByModuleRootId)
        {
            Scene = scene;
            Source = source;
            Program = program;
            Diagnostics = diagnostics ?? new List<VSigDiagnostic>();
            Routes = routes ?? new List<Route>();
            MarkerIdsByModuleRootId = markerIdsByModuleRootId ?? new Dictionary<int, int[]>();
        }

        public Scene Scene { get; }
        public TextAsset Source { get; }
        public VSigProgram Program { get; }
        public IReadOnlyList<VSigDiagnostic> Diagnostics { get; }
        public IReadOnlyList<Route> Routes { get; }
        public IReadOnlyDictionary<int, int[]> MarkerIdsByModuleRootId { get; }

        public bool HasSource => Source;

        public bool HasErrors
        {
            get
            {
                foreach (var diagnostic in Diagnostics)
                    if (diagnostic?.Severity == VSigDiagnosticSeverity.Error)
                        return true;

                return false;
            }
        }

        public static VSigRouteModel Empty(Scene scene, List<VSigDiagnostic> diagnostics)
        {
            return new VSigRouteModel(scene, null, null, diagnostics ?? new List<VSigDiagnostic>(), new List<Route>(),
                new Dictionary<int, int[]>());
        }

        public readonly struct Route
        {
            public Route(EntityMarker source, EntityMarker target, string sourceAlias, string outputName,
                string targetAlias, string inputName)
            {
                Source = source;
                Target = target;
                SourceAlias = sourceAlias ?? string.Empty;
                OutputName = outputName ?? string.Empty;
                TargetAlias = targetAlias ?? string.Empty;
                InputName = inputName ?? string.Empty;
            }

            public EntityMarker Source { get; }
            public EntityMarker Target { get; }
            public string SourceAlias { get; }
            public string OutputName { get; }
            public string TargetAlias { get; }
            public string InputName { get; }
        }
    }
}
#endif
