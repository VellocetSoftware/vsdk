// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapSceneTraversalUtility
    {
        internal static T[] GetComponents<T>(Scene scene, bool includeInactive = true) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return Array.Empty<T>();

            var roots = scene.GetRootGameObjects();
            if (roots == null || roots.Length == 0)
                return Array.Empty<T>();

            var results = new List<T>(64);
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (!root)
                    continue;
                if (!includeInactive && !root.activeInHierarchy)
                    continue;

                var components = root.GetComponentsInChildren<T>(includeInactive);
                if (components == null || components.Length == 0)
                    continue;

                for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    var component = components[componentIndex];
                    if (component)
                        results.Add(component);
                }
            }

            return results.Count == 0 ? Array.Empty<T>() : results.ToArray();
        }

        internal static Component[] GetComponents(Scene scene, Type componentType, bool includeInactive = true)
        {
            if (!scene.IsValid() || !scene.isLoaded || componentType == null ||
                !typeof(Component).IsAssignableFrom(componentType))
                return Array.Empty<Component>();

            var roots = scene.GetRootGameObjects();
            if (roots == null || roots.Length == 0)
                return Array.Empty<Component>();

            var results = new List<Component>(64);
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (!root)
                    continue;
                if (!includeInactive && !root.activeInHierarchy)
                    continue;

                var components = root.GetComponentsInChildren(componentType, includeInactive);
                if (components == null || components.Length == 0)
                    continue;

                for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
                    if (components[componentIndex] is Component component && component)
                        results.Add(component);
            }

            return results.Count == 0 ? Array.Empty<Component>() : results.ToArray();
        }
    }
}
#endif