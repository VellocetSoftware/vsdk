using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Maps;
using Vertex.Contracts;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class MapSettingsAuthoring
    {
        public static MapMusicSet Compile(Scene scene)
        {
            var marker = Read(scene);
            if (!marker) return MapMusicSets.Default;
            var musicSet = MapMusicSets.Resolve(marker.MusicSet);
            var prefab = PrefabUtility.GetOutermostPrefabInstanceRoot(marker.gameObject);
            if (prefab)
                PrefabUtility.UnpackPrefabInstance(prefab, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            UnityEngine.Object.DestroyImmediate(marker.gameObject);
            return musicSet;
        }

        public static MapSettingsMarker Read(Scene scene)
        {
            var markers = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<MapSettingsMarker>(true)).ToArray();
            RequireAtMostOne(markers.Length);
            if (markers.Length == 0) return null;
            if (markers[0].transform.childCount != 0)
                throw new InvalidDataException("Map Settings cannot own scene objects. Move its children outside it.");
            MapMusicSets.Name(markers[0].MusicSet);
            return markers[0];
        }

        public static MapMusicSet Read(MapAsset map)
        {
            var settings = map.Containers.Where(node => node.Entity is { } entity &&
                entity.SdkId == Constants.PackageId && entity.TypeId == MapSettingsMarker.EntityId).ToArray();
            RequireAtMostOne(settings.Length);
            if (settings.Length == 0) return MapMusicSets.Default;
            var value = settings[0].Entity.Properties.SingleOrDefault(property => property.Id == "music_set")?.Value;
            if (value == null) return MapMusicSets.Default;
            if (value is not (int or long or double) ||
                Convert.ToDouble(value) != Math.Truncate(Convert.ToDouble(value)) ||
                Convert.ToDouble(value) < int.MinValue || Convert.ToDouble(value) > int.MaxValue)
                throw new InvalidDataException("Invalid Music Set on the map's Map Settings entity.");
            return MapMusicSets.Resolve((MapMusicSet)Convert.ToInt32(value));
        }

        private static void RequireAtMostOne(int count)
        {
            if (count > 1)
                throw new InvalidDataException($"A map supports at most one Map Settings entity; found {count}.");
        }
    }
}
