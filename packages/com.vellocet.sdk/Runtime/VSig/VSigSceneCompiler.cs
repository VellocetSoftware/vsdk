using Vertex.Unity;
// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.VSig.Runtime;
using Vertex.VSig;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.VSig
{
    /// <summary>Unity asset/scene adapter for the Vertex language. No language rules live here.</summary>
    public static class VSigSceneCompiler
    {
        /// <summary>Use the authored Vertex map identity; legacy Unity scenes use their saved scene GUID.</summary>
        public static Guid ResolveMapId(UnityEngine.SceneManagement.Scene scene, Guid legacySceneId)
        {
            // OnEnable runs before Unity sets isLoaded; the deserialized hierarchy is already available.
            if (!scene.IsValid()) throw new ArgumentException("VSig needs a valid scene.");
            var identities = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Vertex.Unity.VertexObjectIdentity>(true))
                .Select(identity => identity.MapId).Distinct().Take(2).ToArray();
            if (identities.Length > 1 || identities.Length == 1 && identities[0] == Guid.Empty)
                throw new ArgumentException("A VSig scene must contain one Vertex map with a valid map ID.");
            return identities.Length == 1 ? identities[0] : legacySceneId;
        }

        public static VSigProgram Parse(string source, IList<VSigDiagnostic> diagnostics,
            IReadOnlyList<EntityMarker> markers = null, string sourcePath = null)
        {
            if (!TrySnapshot(markers, diagnostics, out var scene)) return new VSigProgram();
            try
            {
                var sources = CaptureSources(source, sourcePath, markers, out var hosts);
                var assemblies = new Dictionary<Guid, Vertex.Contracts.AssemblyInterface>();
                var connections = new List<VSigOwnedConnection>();
                foreach (var unityScene in (markers ?? Array.Empty<EntityMarker>()).Where(m => m).Select(m => m.gameObject.scene).Distinct())
                    foreach (var data in unityScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VertexEntityAuthoring>(true)))
                    {
                        var record = data.Read();
                        if (data.TryGetComponent<VertexObjectIdentity>(out var identity) && identity.ObjectId != record.ObjectId)
                            throw new ArgumentException("Assembly metadata does not match its map object identity.");
                        if (record.Assembly != null) assemblies.Add(record.ObjectId, record.Assembly);
                        connections.AddRange(record.Connections.Select(row => new VSigOwnedConnection { OwnerId = record.ObjectId, Connection = row }));
                    }
                return new VSigSceneInput { Sources = sources, Entities = scene.Entities.Values.ToArray(), Hosts = hosts,
                    EntryPath = AuthoredPath(string.IsNullOrEmpty(sourcePath) ? "__map.vsig" : sourcePath),
                    AuthoringObjectIds = (markers ?? Array.Empty<EntityMarker>()).Where(m => m).Select(m => m.Guid).ToArray(),
                    Assemblies = assemblies, Connections = connections, Globals = VSigGlobalEvents.Catalog }.Parse(diagnostics);
            }
            catch (ArgumentException exception)
            {
                diagnostics?.Add(new VSigDiagnostic(VSigDiagnosticSeverity.Error, 0,
                    "VSIG_SOURCE_SNAPSHOT", exception.Message));
                return new VSigProgram();
            }
        }

        public static void Validate(VSigProgram program, IReadOnlyList<EntityMarker> markers,
            List<VSigDiagnostic> diagnostics)
        {
            if (!ValidateLimits(program, diagnostics) || !TrySnapshot(markers, diagnostics, out var scene)) return;
            VSigProgramValidator.Validate(program, scene.Entities.Values.ToArray(), diagnostics, VSigGlobalEvents.Catalog);
        }

        public static Dictionary<string, List<EntityMarker>> ResolveAliases(VSigProgram program,
            IReadOnlyList<EntityMarker> markers, IList<VSigDiagnostic> diagnostics)
        {
            var result = new Dictionary<string, List<EntityMarker>>(StringComparer.OrdinalIgnoreCase);
            if (!ValidateLimits(program, diagnostics) || !TrySnapshot(markers, diagnostics, out var scene)) return result;
            var byId = scene.Entities.ToDictionary(pair => pair.Value.Id, pair => pair.Key);
            foreach (var pair in VSigAliasResolver.Resolve(program, scene.Entities.Values.ToArray(), diagnostics))
                result.Add(pair.Key, pair.Value.Select(entity => byId[entity.Id]).ToList());
            return result;
        }

        public static string BuildAlias(EntityMarker marker)
        {
            var name = marker ? (string.IsNullOrWhiteSpace(marker.name) ? marker.GetType().Name : marker.name) : "entity";
            if (marker && marker.TryGetComponent<Vertex.Unity.VertexObjectIdentity>(out var identity)) name = identity.AuthoredName;
            if (name.EndsWith("Marker", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 6);
            return EntitySignalNameUtility.Normalize(name);
        }

        private static bool ValidateLimits(VSigProgram program, IList<VSigDiagnostic> diagnostics)
        {
            if (program == null) return false;
            if (VSigSecurityLimits.TryValidateProgram(program, out var error)) return true;
            diagnostics?.Add(new VSigDiagnostic(VSigDiagnosticSeverity.Error, 0, "VSIG_PROGRAM_REJECTED", error));
            return false;
        }

        private static bool TrySnapshot(IReadOnlyList<EntityMarker> markers, IList<VSigDiagnostic> diagnostics,
            out VSigSceneSnapshot scene)
        {
            try
            {
                // This snapshot is used only for compilation; serialized programs bind existing entity GUIDs.
                var mapId = VSigSceneSnapshot.ImportedMapId(markers);
                scene = new VSigSceneSnapshot(mapId == Guid.Empty ? Guid.NewGuid() : mapId, markers, null);
                return true;
            }
            catch (ArgumentException exception)
            {
                scene = null;
                diagnostics?.Add(new VSigDiagnostic(VSigDiagnosticSeverity.Error, 0, "VSIG_SCENE_INVALID", exception.Message));
                return false;
            }
        }

        private const string StagedScriptRoot = Vertex.Unity.UnityBuildProtocol.AssetRoot + "/Input/Scripts/";
        private static string AuthoredPath(string path) => path != null && path.StartsWith(StagedScriptRoot, StringComparison.Ordinal)
            ? path.Substring(StagedScriptRoot.Length) : path;

        private static VSigSourceSet CaptureSources(string source, string sourcePath,
            IReadOnlyList<EntityMarker> markers, out List<VSigHostedModule> hosts)
        {
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            var characters = 0;
            void Add(string path, string text)
            {
                if (!VSigSourceSet.TryNormalize(path, out path))
                    throw new ArgumentException("VSig sources must use project-relative asset paths.");
                if (files.ContainsKey(path)) return;
                if (text.Length > VSigSourceSet.MaximumSourceCharacters ||
                    characters > VSigSourceSet.MaximumTotalCharacters - text.Length || files.Count >= VSigSourceSet.MaximumSources)
                    throw new ArgumentException("VSig sources exceed the snapshot size or file-count limit.");
                files.Add(path, text);
                characters += text.Length;
                pending.Enqueue(path);
            }
            Add(string.IsNullOrEmpty(sourcePath) ? "__map.vsig" : sourcePath, source ?? string.Empty);
            hosts = new List<VSigHostedModule>();
            foreach (var marker in markers ?? Array.Empty<EntityMarker>())
            {
                if (marker is not LogicScriptMarker host || !host) continue;
                string libraryPath = null;
                if (host.Library)
                {
#if UNITY_EDITOR
                    libraryPath = AssetDatabase.GetAssetPath(host.Library);
#endif
                    if (string.IsNullOrEmpty(libraryPath)) libraryPath = $"__hosted/{host.Guid:N}.vsiglib";
                    Add(libraryPath, host.Library.text);
                }
                var binding = new VSigHostedModule
                {
                    EntityId = host.Guid, LibraryPath = AuthoredPath(libraryPath), ModuleName = host.ModuleName?.Trim(),
                    Alias = host.Alias, BindToHierarchy = host.BindToHierarchy,
                    Location = new VSigSourceSpan(host.gameObject.scene.path, 0)
                };
                if ((host.Arguments?.Length ?? 0) > VSigSecurityLimits.MaximumArgumentsPerCall)
                    throw new ArgumentException($"Script host '{host.name}' has too many module arguments.");
                foreach (var argument in host.Arguments ?? Array.Empty<LogicScriptMarker.Argument>())
                {
                    // Blank inspector rows are not arguments; partial rows are diagnosed by Vertex.
                    if (argument == null || (string.IsNullOrWhiteSpace(argument.Name) &&
                                             string.IsNullOrWhiteSpace(argument.Expression))) continue;
                    binding.Arguments.Add(new VSigNamedArgument
                    { Name = argument.Name, Expression = argument.Expression, Location = binding.Location });
                }
                hosts.Add(binding);
            }
#if UNITY_EDITOR
            // Capture only reachable, imported Unity script assets. The parser never reads the filesystem.
            var attempted = new HashSet<string>(StringComparer.Ordinal);
            while (pending.Count > 0)
            {
                var path = pending.Dequeue();
                var document = VSigParser.ParseDocument(files[path], null, AuthoredPath(path));
                foreach (var import in document.Imports)
                    foreach (var logical in VSigSourceSet.GetImportCandidates(AuthoredPath(path), import.LibraryPath))
                    {
                        var candidate = path.StartsWith(StagedScriptRoot, StringComparison.Ordinal) ? StagedScriptRoot + logical : logical;
                        if (files.ContainsKey(candidate)) break;
                        if (!attempted.Add(candidate) ||
                            (!candidate.StartsWith("Assets/", StringComparison.Ordinal) &&
                             !candidate.StartsWith("Packages/", StringComparison.Ordinal)) ||
                            (!candidate.EndsWith(".vsig", StringComparison.OrdinalIgnoreCase) &&
                             !candidate.EndsWith(".vsiglib", StringComparison.OrdinalIgnoreCase))) continue;
                        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(candidate);
                        if (!asset || !string.Equals(AssetDatabase.GetAssetPath(asset), candidate, StringComparison.Ordinal)) continue;
                        Add(candidate, asset.text);
                        break;
                    }
            }
#endif
            // Keep authoring paths in compiled diagnostics/traces, including project-root import fallbacks.
            return new VSigSourceSet(files.ToDictionary(pair => AuthoredPath(pair.Key), pair => pair.Value, StringComparer.Ordinal));
        }
    }
}
