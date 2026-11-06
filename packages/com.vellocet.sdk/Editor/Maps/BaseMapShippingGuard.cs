using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using Vellocet.Content.Editor;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class BaseMapShippingGuard
    {
        public static bool Normalize(AddressableAssetSettings settings, string mapsRoot, bool persistChanges = true)
        {
            if (!settings) return false;
            var definitions = BaseMapAuthoring.Definitions().ToArray();
            var sources = new HashSet<string>(definitions.Select(map => map.authoringScenePath), StringComparer.OrdinalIgnoreCase);
            var compiled = new HashSet<string>(definitions.Select(map => map.compiledScenePath), StringComparer.OrdinalIgnoreCase);
            var changed = AddressablesContentUtility.RemoveAssetEntries(settings, entry =>
                sources.Contains(entry.AssetPath) || IsMapScene(entry.AssetPath, mapsRoot) && !compiled.Contains(entry.AssetPath));
            if (changed && persistChanges) AddressablesContentUtility.Save(settings);
            return changed;
        }

        public static void ValidateBuild(ICollection<string> issues, AddressableAssetSettings settings, string mapsRoot)
        {
            var definitions = BaseMapAuthoring.Definitions().ToArray();
            var sources = new HashSet<string>(definitions.Select(map => map.authoringScenePath), StringComparer.OrdinalIgnoreCase);
            var compiled = new HashSet<string>(definitions.Select(map => map.compiledScenePath), StringComparer.OrdinalIgnoreCase);
            foreach (var scene in EditorBuildSettings.scenes.Where(scene => scene != null && scene.enabled))
                if (sources.Contains(scene.path) || compiled.Contains(scene.path) || IsMapScene(scene.path, mapsRoot))
                    issues.Add($"Build settings include base map scene '{scene.path}'. Base maps must ship via compiled Addressables only.");
            if (!settings) { issues.Add("Addressables settings are missing."); return; }
            foreach (var group in settings.groups.Where(group => group))
            foreach (var entry in group.entries.Where(entry => entry != null))
                if (sources.Contains(entry.AssetPath) || IsMapScene(entry.AssetPath, mapsRoot) && !compiled.Contains(entry.AssetPath))
                    issues.Add($"Addressables group '{group.Name}' includes uncompiled base map scene '{entry.AssetPath}'.");
        }

        private static bool IsMapScene(string path, string mapsRoot) =>
            !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(mapsRoot) &&
            path.Replace('\\', '/').StartsWith(mapsRoot.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
    }
}
