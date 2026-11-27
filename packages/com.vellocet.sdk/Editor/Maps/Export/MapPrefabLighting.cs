// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities.Base;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class MapPrefabLighting
    {
        public static Func<EntityMarker, (GameObject Prefab, Matrix4x4 LocalToWorld)> ResolvePrefab { get; set; }

        internal static Dictionary<EntityMarker, Light[]> Prepare(Scene scene)
        {
            var staged = new Dictionary<EntityMarker, Light[]>();
            foreach (var marker in Markers(scene).ToArray())
            {
                marker.SetBakedPrefabLightSlots(null);
                if (!marker.gameObject.activeInHierarchy || ResolvePrefab == null)
                    continue;
                var (prefab, localToWorld) = ResolvePrefab(marker);
                if (!prefab)
                    continue;

                var sourceLights = prefab.GetComponentsInChildren<Light>(true);
                Light[] copies = null;
                for (var i = 0; i < sourceLights.Length; i++)
                {
                    var source = sourceLights[i];
                    if (source.lightmapBakeType != LightmapBakeType.Baked || !source.enabled ||
                        !IsActiveUnderPrefab(source.transform, prefab.transform))
                        continue;

                    var host = new GameObject(source.name + " (Baked prefab light)");
                    SceneManager.MoveGameObjectToScene(host, scene);
                    host.transform.SetParent(marker.transform, false);
                    var lightToWorld = localToWorld * prefab.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                    var rotation = marker.transform.rotation * Quaternion.Inverse(prefab.transform.rotation) * source.transform.rotation;
                    host.transform.SetPositionAndRotation(lightToWorld.GetColumn(3), rotation);
                    var copy = host.AddComponent<Light>();
                    EditorUtility.CopySerialized(source, copy);
                    copy.bakingOutput = default;
                    if (source.TryGetComponent<UniversalAdditionalLightData>(out var additionalData))
                        EditorUtility.CopySerialized(additionalData, copy.GetUniversalAdditionalLightData());
                    (copies ??= new Light[sourceLights.Length])[i] = copy;
                }

                if (copies != null)
                    staged.Add(marker, copies);
            }

            return staged;
        }

        internal static int Complete(Dictionary<EntityMarker, Light[]> staged)
        {
            var count = 0;
            foreach (var (marker, lights) in staged)
            {
                var slots = new List<int> { lights.Length };
                for (var i = 0; i < lights.Length; i++)
                {
                    var light = lights[i];
                    if (!light)
                        continue;
                    var output = light.bakingOutput;
                    if (output.isBaked && output.lightmapBakeType == LightmapBakeType.Baked)
                        slots.Add(i);
                    else
                        Object.DestroyImmediate(light.gameObject);
                }

                if (slots.Count == 1)
                    continue;
                marker.SetBakedPrefabLightSlots(slots.ToArray());
                count += slots.Count - 1;
            }

            return count;
        }

        internal static IEnumerable<EntityMarker> Markers(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EntityMarker>(true));

        private static bool IsActiveUnderPrefab(Transform node, Transform root)
        {
            for (var current = node; current != root; current = current.parent)
                if (!current.gameObject.activeSelf)
                    return false;
            return true;
        }
    }
}
