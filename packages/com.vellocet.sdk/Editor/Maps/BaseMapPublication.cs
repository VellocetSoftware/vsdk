using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using Vellocet.Sdk.Maps;
using Vellocet.Content.Editor;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class BaseMapPublication
    {
        public const string EditorOnlyGroupName = "Editor Only Maps";
        public const string DisabledGroupName = "Disabled Maps";

        public static bool Synchronize(AddressableAssetSettings settings, MapDefinition map, string playerGroup,
            string definitionAddress, string sceneAddress, string thumbnailAddress = null)
        {
            if (!settings || !map || map.IsAddon || map.IsDraft) return false;
            var published = map.Enabled && !map.IsDraft;
            if (published && !BaseMapAuthoring.TryValidateCompiled(map, out var error))
                throw new InvalidDataException(map.id + ": " + error);
            var destination = AddressablesContentUtility.GetOrCreateGroup(settings, !published ? DisabledGroupName : map.EditorOnly ? EditorOnlyGroupName : playerGroup,
                published && !map.EditorOnly);
            var changed = false;
            if (!settings.GetLabels().Contains(Constants.MapLabel)) { settings.AddLabel(Constants.MapLabel); changed = true; }
            var definitionGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(map));
            var definition = Entry(definitionGuid, definitionAddress, destination);
            if (definition != null && definition.labels.Contains(Constants.MapLabel) != published)
            { definition.SetLabel(Constants.MapLabel, published, false, false); changed = true; }

            // A draft or copied definition must never retarget another map's compiled scene.
            var ownsOutput = !string.IsNullOrEmpty(map.compiledScenePath) &&
                map.compiledScenePath.StartsWith(Path.GetDirectoryName(AssetDatabase.GetAssetPath(map)).Replace('\\', '/') + "/",
                    StringComparison.OrdinalIgnoreCase);
            if (ownsOutput && AssetDatabase.LoadAssetAtPath<SceneAsset>(map.compiledScenePath))
            {
                Entry(AssetDatabase.AssetPathToGUID(map.compiledScenePath), sceneAddress, destination);
                var root = Path.GetDirectoryName(map.compiledScenePath).Replace('\\', '/') + "/";
                foreach (var path in AssetDatabase.GetDependencies(map.compiledScenePath, true)
                             .Where(path => path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && path != map.compiledScenePath))
                {
                    var existing = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                    Entry(AssetDatabase.AssetPathToGUID(path), existing?.address, destination);
                }
                var spatial = MapSpatialGraphArtifacts.ResolveAssetPath(map.compiledScenePath, map.id);
                if (File.Exists(spatial)) Entry(AssetDatabase.AssetPathToGUID(spatial), MapSpatialGraphArtifacts.ResolveAddress(map.id), destination);
                if (map.sceneAddress != sceneAddress)
                { map.sceneAddress = sceneAddress; EditorUtility.SetDirty(map); changed = true; }
            }

            var thumbnail = map.thumbnailReference?.AssetGUID;
            if (!string.IsNullOrEmpty(thumbnail))
            {
                var existing = settings.FindAssetEntry(thumbnail);
                var sharedAddress = SharedAssetAddressUtility.CreateAddress(AssetDatabase.GUIDToAssetPath(thumbnail));
                AddressableAssetGroup thumbnailGroup;
                if (existing != null && !string.IsNullOrEmpty(sharedAddress) && existing.address == sharedAddress)
                    thumbnailGroup = SharedAssetAddressableEditorUtility.EnsureSharedGroup(settings);
                else if (existing != null && existing.parentGroup.Name != playerGroup &&
                         existing.parentGroup.Name != EditorOnlyGroupName && existing.parentGroup.Name != DisabledGroupName)
                    thumbnailGroup = existing.parentGroup;
                else
                {
                    var users = BaseMapAuthoring.Definitions().Where(other => other.Enabled && !other.IsDraft &&
                        other.thumbnailReference?.AssetGUID == thumbnail).ToArray();
                    thumbnailGroup = users.Any(other => !other.EditorOnly) ? AddressablesContentUtility.GetOrCreateGroup(settings, playerGroup, true) :
                        users.Length > 0 ? AddressablesContentUtility.GetOrCreateGroup(settings, EditorOnlyGroupName, false) : destination;
                }
                Entry(thumbnail, existing?.address ?? thumbnailAddress, thumbnailGroup);
            }
            var logo = map.logoReference?.AssetGUID;
            if (!string.IsNullOrEmpty(logo))
            {
                var existing = settings.FindAssetEntry(logo);
                var sharedAddress = SharedAssetAddressUtility.CreateAddress(AssetDatabase.GUIDToAssetPath(logo));
                AddressableAssetGroup logoGroup;
                if (existing != null && !string.IsNullOrEmpty(sharedAddress) && existing.address == sharedAddress)
                    logoGroup = SharedAssetAddressableEditorUtility.EnsureSharedGroup(settings);
                else if (existing != null && existing.parentGroup.Name != playerGroup &&
                         existing.parentGroup.Name != EditorOnlyGroupName && existing.parentGroup.Name != DisabledGroupName)
                    logoGroup = existing.parentGroup;
                else
                {
                    var users = BaseMapAuthoring.Definitions().Where(other => other.Enabled && !other.IsDraft &&
                        other.logoReference?.AssetGUID == logo).ToArray();
                    logoGroup = users.Any(other => !other.EditorOnly) ? AddressablesContentUtility.GetOrCreateGroup(settings, playerGroup, true) :
                        users.Length > 0 ? AddressablesContentUtility.GetOrCreateGroup(settings, EditorOnlyGroupName, false) : destination;
                }
                Entry(logo, existing?.address ?? map.id + "/logo", logoGroup);
            }
            if (!string.IsNullOrEmpty(map.authoringScenePath) && map.authoringScenePath != map.compiledScenePath)
            {
                var source = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(map.authoringScenePath));
                if (source != null) { settings.RemoveAssetEntry(source.guid, false); changed = true; }
            }
            if (changed) EditorUtility.SetDirty(settings);
            return changed;

            AddressableAssetEntry Entry(string guid, string address, AddressableAssetGroup group)
            {
                if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) return null;
                var entry = settings.FindAssetEntry(guid);
                if (entry == null) { entry = settings.CreateOrMoveEntry(guid, group, false, false); changed = true; }
                else if (entry.parentGroup != group) { settings.MoveEntry(entry, group, false, false); changed = true; }
                if (!string.IsNullOrEmpty(address) && entry.address != address)
                { entry.SetAddress(address, false); changed = true; }
                return entry;
            }
        }

    }
}
