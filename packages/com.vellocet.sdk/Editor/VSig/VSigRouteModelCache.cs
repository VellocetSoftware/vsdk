// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Collections.Generic;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Editor.VSig
{
    /// <summary>Shared authoring routes, rebuilt after edits rather than during every GUI event.</summary>
    [InitializeOnLoad]
    public static class VSigRouteModelCache
    {
        private static readonly Dictionary<(int Scene, bool IncludeInactive), VSigRouteModel> Models = new();
        private static readonly ProfilerMarker BuildMarker = new("VSig.Editor.BuildRouteModel");

        static VSigRouteModelCache()
        {
            EditorApplication.hierarchyChanged += Clear;
            EditorApplication.projectChanged += Clear;
            Undo.undoRedoPerformed += Clear;
            EditorSceneManager.sceneClosed += SceneClosed;
            EditorSceneManager.sceneSaved += Invalidate;
            ObjectChangeEvents.changesPublished += ObjectsChanged;
        }

        public static VSigRouteModel Get(Scene scene, bool includeInactive = true)
        {
            if (!scene.IsValid() || !scene.isLoaded) return VSigRouteModel.Empty(scene, null);
            var key = (scene.handle, includeInactive);
            if (Models.TryGetValue(key, out var model)) return model;
            using (BuildMarker.Auto()) model = VSigRouteModelBuilder.Build(scene, includeInactive);
            Models[key] = model;
            return model;
        }

        public static void Invalidate(Scene scene)
        {
            Models.Remove((scene.handle, true));
            Models.Remove((scene.handle, false));
        }

        internal static void Clear() => Models.Clear();
        private static void SceneClosed(Scene scene) => Invalidate(scene);

        private static void ObjectsChanged(ref ObjectChangeEventStream changes)
        {
            for (var i = 0; i < changes.length; i++)
            {
                if (changes.GetEventType(i) != ObjectChangeKind.ChangeGameObjectOrComponentProperties) continue;
                changes.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);
#if UNITY_6000_3_OR_NEWER
                var target = EditorUtility.EntityIdToObject(change.instanceId);
#else
                var target = EditorUtility.InstanceIDToObject(change.instanceId);
#endif
                // Moving geometry only changes wire endpoints, which are read directly when drawn.
                if (target is EntityMarker marker) Invalidate(marker.gameObject.scene);
                else if (target is GameObject gameObject) Invalidate(gameObject.scene);
            }
        }
    }

    internal sealed class VSigRouteSourceImports : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var paths in new[] { imported, deleted, moved, movedFrom })
                foreach (var path in paths)
                    if (path.EndsWith(".vsig", System.StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".vsiglib", System.StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    {
                        VSigRouteModelCache.Clear();
                        return;
                    }
        }
    }
}
