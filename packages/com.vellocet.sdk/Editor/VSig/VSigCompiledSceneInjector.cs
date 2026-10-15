// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using Vertex.VSig;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Maps;
using Vellocet.Sdk.Utilities;
using Vellocet.Sdk.VSig;
using Vellocet.Sdk.VSig.Runtime;
using Object = UnityEngine.Object;
using VSigCompiledProgram = Vertex.VSig.VSigCompiledProgram;

namespace Vellocet.Sdk.Editor.VSig
{
    public readonly struct VSigCompiledSceneInjectionOptions
    {
        public VSigCompiledSceneInjectionOptions(Transform runtimeParent, string runtimeObjectName)
        {
            RuntimeParent = runtimeParent;
            RuntimeObjectName = runtimeObjectName;
        }

        public Transform RuntimeParent { get; }
        public string RuntimeObjectName { get; }
    }

    public static class VSigCompiledSceneInjector
    {
        private const string DefaultRuntimeObjectName = "[VSig Runtime]";

        public static bool Inject(Scene scene, string sourceScenePath, MapCompilationReceipt receipt,
            out string message)
        {
            return Inject(scene, sourceScenePath, receipt, default, out message);
        }

        public static bool Inject(Scene scene, string sourceScenePath, MapCompilationReceipt receipt,
            VSigCompiledSceneInjectionOptions options, out string message)
        {
            message = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                message = "Compiled scene is invalid.";
                return false;
            }

            var markers = CollectMarkers(scene);
            if (!TryValidateMarkerEntityIds(markers, out message))
                return false;

            var sourceStatus = VSigMapSourceUtility.FindForScene(sourceScenePath, out var source, out _,
                out var expectedPath);
            if (sourceStatus == VSigMapSourceStatus.Multiple)
            {
                message = $"Multiple VSig files were found for '{sourceScenePath}'. Name the intended source '{expectedPath}'.";
                return false;
            }

            if (sourceStatus != VSigMapSourceStatus.Found)
            {
                var hasCompiledProgram = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<VSigSceneRuntime>(true)).Any(runtime => runtime.HasProgram);
                if (markers.Count == 0 && !hasCompiledProgram)
                {
                    RemoveExistingRuntimes(scene, options);
                    ClearReceipt(receipt);
                    message = "No entity markers or VSig source were found.";
                    return true;
                }

                message =
                    $"No VSig file was found next to '{sourceScenePath}'. The scene has {markers.Count} entity marker(s) or an existing compiled program.";
                return false;
            }

            Guid.TryParse(AssetDatabase.AssetPathToGUID(sourceScenePath), out var sceneId);
            return InjectSource(scene, source, receipt, out message, options, sceneId);
        }

        /// <summary>Compile an imported VSig asset directly into a generated Vertex scene.</summary>
        public static bool InjectSource(Scene scene, TextAsset source, MapCompilationReceipt receipt,
            out string message, VSigCompiledSceneInjectionOptions options = default, Guid legacySceneId = default)
        {
            message = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded || !source)
            {
                message = "A loaded scene and imported VSig source are required.";
                return false;
            }
            var sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith(".vsig", StringComparison.OrdinalIgnoreCase))
            {
                message = "VSig source must be an imported .vsig asset.";
                return false;
            }
            var markers = CollectMarkers(scene);
            if (!TryValidateMarkerEntityIds(markers, out message)) return false;

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source.text, diagnostics, markers, sourcePath);
            VSigSceneCompiler.Validate(program, markers, diagnostics);
            var referenced = new HashSet<EntityMarker>(VSigSceneCompiler.ResolveAliases(program, markers, diagnostics).Values.SelectMany(values => values));
            var referencedLights = referenced.OfType<Vellocet.Sdk.Entities.LightMarker>().ToArray();
            foreach (var light in referencedLights)
                if (light.GetComponent<Light>().type == LightType.Rectangle)
                {
                    message = $"Area light '{light.name}' is referenced by VSig. Area lights are baked-only; choose Point or Spot for scripting.";
                    return false;
                }

            var errors = diagnostics
                .Where(diagnostic => diagnostic?.Severity == VSigDiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.ToString())
                .ToArray();
            if (errors.Length > 0)
            {
                message = $"VSig validation failed for '{sourcePath}':\n- {string.Join("\n- ", errors)}";
                return false;
            }

            VSigCompiledProgram compiled;
            try
            {
                compiled = VSigCompiledProgram.FromProgram(program);
                // Complete bounded serialization before replacing any previously embedded program.
                compiled.Serialize();
            }
            catch (ArgumentException exception)
            {
                message = $"VSig compilation failed for '{sourcePath}': {exception.Message}";
                return false;
            }
            Guid mapId;
            try
            {
                mapId = VSigSceneCompiler.ResolveMapId(scene, legacySceneId);
                if (mapId == Guid.Empty) throw new ArgumentException("VSig needs a saved source scene or an imported Vertex map identity.");
            }
            catch (ArgumentException error)
            {
                message = error.Message;
                return false;
            }
            RemoveExistingRuntimes(scene, options);
            foreach (var light in markers.OfType<Vellocet.Sdk.Entities.LightMarker>())
                light.SetScriptReferenced(referencedLights.Contains(light));
            foreach (var label in markers.OfType<Vellocet.Sdk.Entities.LabelMarker>())
                label.SetScriptReferenced(referenced.Contains(label));
            foreach (var particle in markers.OfType<Vellocet.Sdk.Entities.ParticleMarker>())
                particle.SetScriptReferenced(referenced.Contains(particle));
            var host = FindOrCreateRuntimeHost(scene, options);
            var runtime = host.AddComponent<VSigSceneRuntime>();
            runtime.Configure(compiled, stableMapId: mapId);
            EditorUtility.SetDirty(runtime);
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            UpdateReceipt(receipt, sourcePath, compiled);

            message =
                $"Embedded compiled VSig from '{sourcePath}' ({program.Refs.Count} refs, {program.Uses.Count} modules, {program.StateExports.Count} states, {program.Handlers.Count} handlers, {program.Procedures.Count} procedures).";
            return true;
        }

        private static GameObject FindOrCreateRuntimeHost(Scene scene, VSigCompiledSceneInjectionOptions options)
        {
            var objectName = ResolveRuntimeObjectName(options);
            var parent = ResolveRuntimeParent(scene, options);

            if (parent)
                for (var i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child && string.Equals(child.name, objectName, StringComparison.Ordinal))
                        return child.gameObject;
                }
            else
                foreach (var root in scene.GetRootGameObjects())
                    if (root && string.Equals(root.name, objectName, StringComparison.Ordinal))
                        return root;

            var host = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(host, scene);
            if (parent)
                host.transform.SetParent(parent, false);
            return host;
        }

        private static List<EntityMarker> CollectMarkers(Scene scene)
        {
            var result = new List<EntityMarker>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                result.AddRange(root.GetComponentsInChildren<EntityMarker>(true).Where(marker => marker));
            }

            return result;
        }

        private static bool TryValidateMarkerEntityIds(IEnumerable<EntityMarker> markers, out string message)
        {
            var invalid = (markers ?? Array.Empty<EntityMarker>())
                .Where(marker => marker)
                .Select(marker => new
                {
                    MarkerType = marker.GetType().Name,
                    EntityId = marker.EntityClassName
                })
                .Where(item => !EntityIdConvention.IsCanonical(item.EntityId))
                .Select(item => $"{item.MarkerType} ('{item.EntityId ?? "<missing>"}')")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (invalid.Length == 0)
            {
                message = string.Empty;
                return true;
            }

            message =
                $"Scene contains marker entity IDs that are not {EntityIdConvention.Description}: {string.Join(", ", invalid)}.";
            return false;
        }

        private static void RemoveExistingRuntimes(Scene scene, VSigCompiledSceneInjectionOptions options)
        {
            var objectName = ResolveRuntimeObjectName(options);
            var parent = ResolveRuntimeParent(scene, options);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root)
                    continue;

                var runtimes = root.GetComponentsInChildren<VSigSceneRuntime>(true);
                for (var i = 0; i < runtimes.Length; i++)
                {
                    var runtime = runtimes[i];
                    if (!runtime)
                        continue;

                    if (IsOwnedRuntimeHost(runtime.transform, parent, objectName))
                    {
                        Object.DestroyImmediate(runtime.gameObject);
                        continue;
                    }

                    Object.DestroyImmediate(runtime);
                }
            }
        }

        private static bool IsOwnedRuntimeHost(Transform runtimeTransform, Transform parent, string objectName)
        {
            if (!runtimeTransform)
                return false;

            if (string.Equals(runtimeTransform.name, DefaultRuntimeObjectName, StringComparison.Ordinal) &&
                runtimeTransform.parent == null)
                return true;

            return string.Equals(runtimeTransform.name, objectName, StringComparison.Ordinal) &&
                   runtimeTransform.parent == parent;
        }

        private static Transform ResolveRuntimeParent(Scene scene, VSigCompiledSceneInjectionOptions options)
        {
            return options.RuntimeParent && options.RuntimeParent.gameObject.scene.handle == scene.handle
                ? options.RuntimeParent
                : null;
        }

        private static string ResolveRuntimeObjectName(VSigCompiledSceneInjectionOptions options)
        {
            return string.IsNullOrWhiteSpace(options.RuntimeObjectName)
                ? DefaultRuntimeObjectName
                : options.RuntimeObjectName.Trim();
        }

        private static void UpdateReceipt(MapCompilationReceipt receipt, string sourcePath,
            VSigCompiledProgram compiledProgram)
        {
            if (receipt == null)
                return;

            receipt.vsigSourcePath = sourcePath ?? string.Empty;
            receipt.vsigSourceHash = SdkFileHashUtility.TryComputeSha256(sourcePath, out var sourceHash)
                ? sourceHash
                : string.Empty;
            receipt.vsigProgramHash = ComputeProgramHash(compiledProgram);
        }

        private static void ClearReceipt(MapCompilationReceipt receipt)
        {
            if (receipt == null)
                return;

            receipt.vsigSourcePath = string.Empty;
            receipt.vsigSourceHash = string.Empty;
            receipt.vsigProgramHash = string.Empty;
        }

        private static string ComputeProgramHash(VSigCompiledProgram compiledProgram)
        {
            if (compiledProgram == null)
                return string.Empty;

            using var stream = new MemoryStream(compiledProgram.Serialize());
            return SdkFileHashUtility.TryComputeSha256(stream, out var hash) ? hash : string.Empty;
        }
    }
}
#endif
