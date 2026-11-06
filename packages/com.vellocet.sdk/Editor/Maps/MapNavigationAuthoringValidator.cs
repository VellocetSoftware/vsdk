#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Maps;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>
    ///     Enforces the navigation-surface contract used by the runtime bot traversal system. Preview-scene
    ///     inspection supports both text and binary Unity scenes without disturbing the author's open scenes.
    /// </summary>
    public static class MapNavigationAuthoringValidator
    {
        private const string NavMeshSurfaceTypeName = "Unity.AI.Navigation.NavMeshSurface";
        private const string AgentTypePropertyName = "m_AgentTypeID";
        private const string NavMeshDataPropertyName = "m_NavMeshData";
        private const int HumanoidAgentTypeId = 0;

        public static bool TryValidateSceneInIsolation(string scenePath, out string error)
        {
            return TryValidateSceneInIsolation(scenePath, false, out error);
        }

        public static bool TryValidateCompiledSceneInIsolation(string scenePath, out string error)
        {
            return TryValidateSceneInIsolation(scenePath, true, out error);
        }

        private static bool TryValidateSceneInIsolation(
            string scenePath,
            bool requireBakedData,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                error = "Map scene path is empty.";
                return false;
            }

            Scene previewScene = default;
            try
            {
                previewScene = EditorSceneManager.OpenPreviewScene(scenePath);
                return TryValidateScene(previewScene, scenePath, requireBakedData, out error);
            }
            catch (Exception exception)
            {
                error = $"Could not inspect map navigation in '{scenePath}': {exception.Message}";
                return false;
            }
            finally
            {
                if (previewScene.IsValid())
                    EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        public static bool TryValidateScene(Scene scene, string scenePath, out string error)
        {
            return TryValidateScene(scene, scenePath, false, out error);
        }

        private static bool TryValidateScene(
            Scene scene,
            string scenePath,
            bool requireBakedData,
            out string error)
        {
            error = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = $"Map scene is not loaded: {scenePath}";
                return false;
            }

            var surfaces = FindSurfaces(scene);
            if (surfaces.Count == 0)
            {
                error = $"Map has no NavMeshSurface: {scenePath}";
                return false;
            }

            var humanoidSurfaceCount = 0;
            for (var index = 0; index < surfaces.Count; index++)
            {
                var surface = surfaces[index];
                var serializedSurface = new SerializedObject(surface);
                var agentType = serializedSurface.FindProperty(AgentTypePropertyName);
                if (agentType == null || agentType.propertyType != SerializedPropertyType.Integer)
                {
                    error =
                        $"NavMeshSurface agent type could not be read in '{scenePath}' at " +
                        $"'{BuildHierarchyPath(surface.transform)}'.";
                    return false;
                }

                if (agentType.intValue != HumanoidAgentTypeId)
                    continue;

                humanoidSurfaceCount++;
                string mismatch;
                var surfaceIsValid = requireBakedData
                    ? MapSceneUtility.TryValidateHumanoidNavigationSurface(surface, out mismatch)
                    : MapSceneUtility.CanEnforceHumanoidNavigationSurface(surface, out mismatch);
                if (!surfaceIsValid)
                {
                    error =
                        $"Humanoid NavMeshSurface cannot satisfy the SDK contract: {scenePath} " +
                        $"('{BuildHierarchyPath(surface.transform)}'): {mismatch}";
                    return false;
                }

                if (!requireBakedData)
                    continue;

                var navMeshData = serializedSurface.FindProperty(NavMeshDataPropertyName);
                if (navMeshData == null ||
                    navMeshData.propertyType != SerializedPropertyType.ObjectReference ||
                    !navMeshData.objectReferenceValue)
                {
                    error =
                        $"Compiled Humanoid NavMeshSurface has no baked NavMeshData: {scenePath} " +
                        $"('{BuildHierarchyPath(surface.transform)}').";
                    return false;
                }
            }

            if (humanoidSurfaceCount == 0)
            {
                error = $"Map has no Humanoid NavMeshSurface (agent type {HumanoidAgentTypeId}): {scenePath}";
                return false;
            }

            return true;
        }

        public static bool TryRepairHumanoidNavigationSurfaces(
            Scene scene,
            bool registerUndo,
            out bool changed,
            out string error)
        {
            changed = false;
            error = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "No loaded map scene is available for navigation setup.";
                return false;
            }

            var surfaces = FindSurfaces(scene);
            if (surfaces.Count == 0)
            {
                error = "Scene has no NavMeshSurface after map auto setup.";
                return false;
            }

            var humanoidSurfaceCount = 0;
            for (var index = 0; index < surfaces.Count; index++)
            {
                var surface = surfaces[index];
                var serializedSurface = new SerializedObject(surface);
                var agentType = serializedSurface.FindProperty(AgentTypePropertyName);
                if (agentType == null || agentType.propertyType != SerializedPropertyType.Integer)
                {
                    error =
                        $"NavMeshSurface at '{BuildHierarchyPath(surface.transform)}' does not expose an " +
                        "agent-type setting in this AI Navigation package version.";
                    return false;
                }

                if (agentType.intValue != HumanoidAgentTypeId)
                    continue;

                humanoidSurfaceCount++;
                if (!MapSceneUtility.TryEnforceHumanoidNavigationSurface(
                        surface,
                        registerUndo,
                        out var surfaceChanged,
                        out var surfaceError))
                {
                    error = surfaceError;
                    return false;
                }

                changed |= surfaceChanged;
            }

            if (humanoidSurfaceCount == 0)
            {
                error =
                    $"Scene has no Humanoid NavMeshSurface (agent type {HumanoidAgentTypeId}) after map auto setup.";
                return false;
            }

            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);

            return true;
        }

        private static List<Component> FindSurfaces(Scene scene)
        {
            var surfaces = new List<Component>(1);
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var root = roots[rootIndex];
                if (!root)
                    continue;

                var components = root.GetComponentsInChildren<Component>(true);
                for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    var component = components[componentIndex];
                    if (component &&
                        string.Equals(component.GetType().FullName, NavMeshSurfaceTypeName, StringComparison.Ordinal))
                        surfaces.Add(component);
                }
            }

            return surfaces;
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