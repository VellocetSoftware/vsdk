// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.VSig.Runtime;
using Vertex.Unity;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapDeadContentUtility
    {
        internal static bool Strip(Scene scene, out string message)
        {
            message = string.Empty;
            if (!scene.IsValid())
            {
                message = "Compiled scene is invalid.";
                return false;
            }

            var transforms = CollectTransforms(scene);
            if (transforms.Count == 0)
            {
                message = "No dead scene content was found.";
                return true;
            }

            var summary = new Summary(transforms.Count);
            var dirty = false;

            foreach (var runtime in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VSigSceneRuntime>(true)))
                runtime.BakeSceneIdentity();

            foreach (var transform in transforms)
            {
                if (!transform)
                    continue;

                var gameObject = transform.gameObject;
                if (!gameObject)
                    continue;

                var missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingScripts > 0)
                {
                    var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
                    summary.MissingScriptsRemoved += removed;
                    dirty |= removed > 0;
                }

                if (ShouldStripEditorOnly(gameObject))
                {
                    Object.DestroyImmediate(gameObject);
                    summary.EditorOnlyObjectsRemoved++;
                    dirty = true;
                    continue;
                }

                if (gameObject.TryGetComponent<VertexObjectIdentity>(out var identity))
                {
                    if (gameObject.TryGetComponent<EntityMarker>(out var marker)) marker.PreserveAuthoredGuid();
                    Object.DestroyImmediate(identity);
                    summary.IdentitiesRemoved++;
                    dirty = true;
                }

                if (ShouldStripInactiveEmptyLeaf(gameObject))
                {
                    Object.DestroyImmediate(gameObject);
                    summary.EmptyHelpersRemoved++;
                    dirty = true;
                }
            }

            if (dirty)
                EditorSceneManager.MarkSceneDirty(scene);

            message = summary.BuildMessage();
            return true;
        }

        private static List<Transform> CollectTransforms(Scene scene)
        {
            return scene.GetRootGameObjects()
                .Where(root => root)
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform)
                .OrderByDescending(GetDepth)
                .ToList();
        }

        private static int GetDepth(Transform transform)
        {
            var depth = 0;
            var current = transform;
            while (current && current.parent)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private static bool ShouldStripEditorOnly(GameObject gameObject)
        {
            return gameObject.CompareTag("EditorOnly");
        }

        private static bool ShouldStripInactiveEmptyLeaf(GameObject gameObject)
        {
            if (gameObject.activeSelf)
                return false;

            if (gameObject.transform.childCount > 0)
                return false;

            if (gameObject.isStatic)
                return false;

            if (!string.Equals(gameObject.tag, "Untagged"))
                return false;

            if (gameObject.layer != 0)
                return false;

            var components = gameObject.GetComponents<Component>();
            return components.Length == 1 && components[0] is Transform;
        }

        private struct Summary
        {
            internal Summary(int scanned)
            {
                Scanned = scanned;
                MissingScriptsRemoved = 0;
                EditorOnlyObjectsRemoved = 0;
                EmptyHelpersRemoved = 0;
                IdentitiesRemoved = 0;
            }

            internal readonly int Scanned;
            internal int MissingScriptsRemoved;
            internal int EditorOnlyObjectsRemoved;
            internal int EmptyHelpersRemoved;
            internal int IdentitiesRemoved;

            internal string BuildMessage()
            {
                return
                    $"Dead-content cleanup scanned {Scanned} object(s): " +
                    $"{MissingScriptsRemoved} missing script component(s) removed, " +
                    $"{EditorOnlyObjectsRemoved} EditorOnly object(s) removed, " +
                    $"{IdentitiesRemoved} Vertex authoring identity component(s) removed, " +
                    $"{EmptyHelpersRemoved} inactive empty helper object(s) removed.";
            }
        }
    }
}
#endif
