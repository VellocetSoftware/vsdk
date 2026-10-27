using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class MapPlayableBoundsBaker
    {
        public static void Bake(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var overrides = roots.SelectMany(root => root.GetComponentsInChildren<MapSettingsMarker>(true))
                .Where(marker => marker.OverridesPlayableBounds).ToArray();
            if (overrides.Length > 1) throw new InvalidOperationException("Only one map settings entity may override playable bounds.");
            Bounds? bounds = null;
            if (overrides.Length == 1) bounds = overrides[0].PlayableBounds;
            else
            {
                Physics.SyncTransforms();
                foreach (var collider in roots.SelectMany(root => root.GetComponentsInChildren<Collider>(false)))
                {
                    if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody || collider.GetComponentInParent<EntityMarker>()) continue;
                    var candidate = collider.bounds;
                    if (candidate.size == Vector3.zero) continue;
                    if (!bounds.HasValue) bounds = candidate;
                    else { var combined = bounds.Value; combined.Encapsulate(candidate); bounds = combined; }
                }
                if (bounds.HasValue)
                {
                    var combined = bounds.Value;
                    // Planar terrain still needs a volume; the gameplay margin is applied separately.
                    combined.size = Vector3.Max(combined.size, Vector3.one);
                    bounds = combined;
                }
            }
            var existing = roots.SelectMany(root => root.GetComponentsInChildren<MapRuntimeDependencies>(true)).ToArray();
            var host = existing.FirstOrDefault();
            if (!host)
            {
                var go = new GameObject("Map runtime dependencies");
                SceneManager.MoveGameObjectToScene(go, scene);
                host = go.AddComponent<MapRuntimeDependencies>();
            }
            foreach (var dependency in existing) dependency.SetPlayableBounds(null);
            host.SetPlayableBounds(bounds);
            EditorUtility.SetDirty(host);
        }
    }
}
