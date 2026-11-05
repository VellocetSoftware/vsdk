#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Editor.Maps
{
    public sealed class MapEntityInventory
    {
        public MapEntityInventory(string scenePath, Dictionary<Guid, string> entityClassByGuid)
        {
            ScenePath = scenePath;
            EntityClassByGuid = entityClassByGuid;
        }

        public string ScenePath { get; }
        public IReadOnlyDictionary<Guid, string> EntityClassByGuid { get; }
        public int Count => EntityClassByGuid.Count;
    }

    /// <summary>
    ///     Guards the boundary around the SDK compiler. Its cleanup pass is allowed to remove editor-only
    ///     content, but missing MonoBehaviours and authored entity identity are never disposable content.
    /// </summary>
    public static class MapEntityIntegrityValidator
    {
        private const int MaximumReportedProblems = 12;

        /// <summary>
        ///     Captures a scene without loading it beside any of its authored or compiled counterparts.
        ///     Entity marker IDs are stable within a map, so opening two copies of the same map additively can
        ///     make the editor duplicate-ID guard regenerate one copy before it can be inspected.
        /// </summary>
        public static bool TryCaptureInIsolation(string scenePath, out MapEntityInventory inventory,
            out string error)
        {
            inventory = null;
            error = null;
            if (!TryValidateScenePath(scenePath, out error))
                return false;

            var loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (loadedScene.IsValid() && loadedScene.isLoaded && SceneManager.sceneCount == 1)
                return TryCapture(scenePath, out inventory, out error);

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || !scene.isLoaded || !scene.isDirty)
                    continue;

                error =
                    $"Cannot isolate map scene '{scenePath}' while loaded scene '{scene.path}' has unsaved changes. Save all open scenes before compiling.";
                return false;
            }

            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            var canRestorePreviousSetup = Array.Exists(previousSetup, setup => setup.isLoaded);
            var succeeded = false;
            try
            {
                var isolatedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!isolatedScene.IsValid() || !isolatedScene.isLoaded)
                    error = $"Map scene could not be opened in isolation for entity-integrity validation: {scenePath}";
                else
                    succeeded = TryCapture(scenePath, out inventory, out error);
            }
            catch (Exception exception)
            {
                error = $"Could not inspect map scene '{scenePath}' in isolation: {exception.Message}";
            }

            try
            {
                if (canRestorePreviousSetup)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            catch (Exception exception)
            {
                inventory = null;
                error = $"Could not restore the editor scene setup after validating '{scenePath}': {exception.Message}";
                return false;
            }

            return succeeded;
        }

        public static bool TryCapture(string scenePath, out MapEntityInventory inventory, out string error)
        {
            inventory = null;
            error = null;
            if (!TryValidateScenePath(scenePath, out error))
                return false;

            var scene = SceneManager.GetSceneByPath(scenePath);
            var openedForValidation = false;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    openedForValidation = true;
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    error = $"Map scene could not be opened for entity-integrity validation: {scenePath}";
                    return false;
                }

                var problems = new List<string>();
                var entityClassByGuid = new Dictionary<Guid, string>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (!root)
                        continue;

                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (!transform)
                            continue;

                        var missingCount =
                            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                        if (missingCount > 0)
                            AddProblem(problems,
                                $"'{BuildHierarchyPath(transform)}' has {missingCount} missing script component(s)");
                    }

                    foreach (var marker in root.GetComponentsInChildren<EntityMarker>(true))
                    {
                        if (!marker)
                            continue;

                        var guid = marker.Guid;
                        var entityClass = marker.EntityClassName ?? string.Empty;
                        if (guid == Guid.Empty)
                        {
                            AddProblem(problems,
                                $"entity marker '{BuildHierarchyPath(marker.transform)}' has no valid GUID");
                            continue;
                        }

                        if (entityClassByGuid.TryGetValue(guid, out var previousClass))
                        {
                            AddProblem(problems,
                                $"entity marker '{BuildHierarchyPath(marker.transform)}' duplicates GUID {guid:N} " +
                                $"(previous class '{previousClass}')");
                            continue;
                        }

                        entityClassByGuid.Add(guid, entityClass);
                    }
                }

                if (problems.Count > 0)
                {
                    error =
                        $"Map entity-integrity validation failed for '{scenePath}'. The compiler will not delete or compile around broken components:\n" +
                        string.Join("\n", problems.Select(problem => $"- {problem}"));
                    return false;
                }

                inventory = new MapEntityInventory(scenePath, entityClassByGuid);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not inspect map scene '{scenePath}': {exception.Message}";
                return false;
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static bool TryCompare(
            MapEntityInventory authored,
            MapEntityInventory compiled,
            out string error)
        {
            error = null;
            if (authored == null || compiled == null)
            {
                error = "Authored and compiled entity inventories are required.";
                return false;
            }

            var problems = new List<string>();
            foreach (var pair in authored.EntityClassByGuid)
            {
                if (!compiled.EntityClassByGuid.TryGetValue(pair.Key, out var compiledClass))
                {
                    // The compiler consumes map settings into MapDefinition before removing the marker.
                    if (pair.Value == MapSettingsMarker.EntityId)
                        continue;
                    AddProblem(problems,
                        $"marker {pair.Key:N} ('{pair.Value}') disappeared from the compiled scene");
                    continue;
                }

                if (!string.Equals(pair.Value, compiledClass, StringComparison.Ordinal))
                    AddProblem(problems,
                        $"marker {pair.Key:N} changed class from '{pair.Value}' to '{compiledClass}'");
            }

            foreach (var pair in compiled.EntityClassByGuid)
                if (!authored.EntityClassByGuid.ContainsKey(pair.Key))
                    AddProblem(problems,
                        $"compiled scene introduced marker {pair.Key:N} ('{pair.Value}') not present in authoring");

            if (problems.Count == 0)
                return true;

            error =
                $"Compiled map entity inventory differs from '{authored.ScenePath}' " +
                $"(authored={authored.Count}, compiled={compiled.Count}):\n" +
                string.Join("\n", problems.Select(problem => $"- {problem}"));
            return false;
        }

        private static void AddProblem(List<string> problems, string problem)
        {
            if (problems.Count < MaximumReportedProblems)
                problems.Add(problem);
            else if (problems.Count == MaximumReportedProblems)
                problems.Add("additional problems omitted");
        }

        private static bool TryValidateScenePath(string scenePath, out string error)
        {
            error = null;
            if (!string.IsNullOrWhiteSpace(scenePath))
                return true;

            error = "Map scene path is empty.";
            return false;
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (!transform)
                return "<missing>";

            var names = new Stack<string>();
            for (var current = transform; current; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }
    }
}
#endif