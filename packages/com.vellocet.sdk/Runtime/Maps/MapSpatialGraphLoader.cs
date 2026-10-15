using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using Vellocet.Framework.Runtime.Spatial;

namespace Vellocet.Sdk.Maps
{
    public static class MapSpatialGraphLoader
    {
        public static Task<bool> LoadForMapSceneAsync(Scene scene, CancellationToken cancellationToken = default) =>
            LoadForMapSceneAsync(scene, null, cancellationToken);

        public static Task<bool> LoadForMapSceneAsync(Scene scene,
            Func<AssetReference, IResourceLocation> resolveLocation, CancellationToken cancellationToken = default)
        {
            if (scene.IsValid() && scene.isLoaded)
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var dependencies in root.GetComponentsInChildren<MapRuntimeDependencies>(true))
                        if (dependencies && dependencies.TryGetSpatialGraphReference(out var reference))
                            return resolveLocation == null
                                ? SpatialGraphService.LoadAsync(scene, reference, cancellationToken)
                                : SpatialGraphService.LoadLocationAsync(scene, resolveLocation(reference), cancellationToken);

            SpatialGraphService.Clear($"Scene '{scene.name}' has no usable spatial graph dependency; rebuild the map with the current SDK.");
            return Task.FromResult(false);
        }
    }
}
