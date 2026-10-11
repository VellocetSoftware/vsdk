// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Tools;
using Vellocet.Sdk.Maps;
using MonoScript = UnityEditor.MonoScript;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class MapAssetUsageAnalyzer
    {
        public static MapAssetUsageReport Analyze(Scene scene, AddressableAssetSettings settings,
            HashSet<string> sharedGuids, bool rewriteSharedReferences)
        {
            var report = new MapAssetUsageReport();
            if (!scene.IsValid())
                return report;

            foreach (var root in scene.GetRootGameObjects())
                ProcessGameObjectRecursive(root, settings, sharedGuids, rewriteSharedReferences, report);

            report.FinalizeRecords();
            return report;
        }

        private static void ProcessGameObjectRecursive(GameObject go, AddressableAssetSettings settings,
            HashSet<string> sharedGuids, bool rewriteSharedReferences, MapAssetUsageReport report)
        {
            if (!go)
                return;

            MapBinding binding = null;

            foreach (var comp in go.GetComponents<Component>())
            {
                if (!comp)
                    continue;

                var so = new SerializedObject(comp);
                var iterator = so.GetIterator();
                var changed = false;

                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    var obj = iterator.objectReferenceValue;
                    if (!obj)
                        continue;

                    var assetPath = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrWhiteSpace(assetPath))
                        continue;

                    if (!ShouldTrackReference(obj, assetPath, iterator.propertyPath))
                        continue;

                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (string.IsNullOrWhiteSpace(guid))
                        continue;

                    var location = $"{GetHierarchyPath(go.transform)} :: {comp.GetType().Name}.{iterator.propertyPath}";
                    var isShared = sharedGuids != null && sharedGuids.Contains(guid);
                    var entry = settings?.FindAssetEntry(guid);
                    var isSdkAsset = SdkHost.IsBaseProject
                        ? SharedAssetAddressUtility.IsSdkSharedAssetPath(assetPath)
                        : isShared;

                    if (isShared && entry != null)
                    {
                        var isSubAsset = AssetDatabase.IsSubAsset(obj);
                        var subObjectName = isSubAsset ? obj.name : null;
                        var valueType = MapBinding.RuntimeAssetType(obj.GetType()).AssemblyQualifiedName;
                        report.AddShared(assetPath, entry.address, location, isSdkAsset);

                        if (rewriteSharedReferences)
                        {
                            if (!MapBinding.TryAssign(comp, iterator.propertyPath, obj, apply: false))
                                throw new InvalidDataException("Unsupported shared asset binding: " + location);
                            binding ??= go.GetComponent<MapBinding>() ?? go.AddComponent<MapBinding>();
                            binding.AddEditorEntry(
                                comp.GetType().AssemblyQualifiedName,
                                ComponentIndexOf(go, comp),
                                iterator.propertyPath,
                                entry.address,
                                valueType,
                                subObjectName);
                            iterator.objectReferenceValue = null;
                            changed = true;
                        }

                        continue;
                    }

                    report.AddPackaged(assetPath, location, isSdkAsset);
                }

                if (changed)
                    so.ApplyModifiedPropertiesWithoutUndo();
            }

            for (var i = 0; i < go.transform.childCount; i++)
                ProcessGameObjectRecursive(go.transform.GetChild(i).gameObject, settings, sharedGuids,
                    rewriteSharedReferences, report);
        }

        private static bool ShouldTrackReference(Object obj, string assetPath, string propertyPath)
        {
            if (obj is MonoScript)
                return false;

            if (string.Equals(propertyPath, "m_Script", StringComparison.Ordinal))
                return false;

            var normalizedPath = SharedAssetAddressUtility.NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return false;

            if (normalizedPath.StartsWith("Library/", StringComparison.OrdinalIgnoreCase))
                return false;

            if (normalizedPath.StartsWith("Resources/unity_builtin_extra", StringComparison.OrdinalIgnoreCase))
                return false;

            return normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                   || normalizedPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
        }

        private static int ComponentIndexOf(GameObject host, Component component)
        {
            if (!host || !component)
                return -1;

            var components = host.GetComponents(component.GetType());
            for (var i = 0; i < components.Length; i++)
                if (components[i] == component)
                    return i;

            return -1;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (!transform)
                return "<missing>";

            var parts = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }
    }

    [Serializable]
    public sealed class MapAssetUsageReport
    {
        public AssetRecord[] packagedAssets = Array.Empty<AssetRecord>();
        public AssetRecord[] sharedAssets = Array.Empty<AssetRecord>();

        private readonly Dictionary<string, AssetRecordAccumulator> _packaged = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AssetRecordAccumulator> _shared = new(StringComparer.OrdinalIgnoreCase);

        public bool HasPackagedAssets => packagedAssets.Length > 0;
        public bool HasMisconfiguredSdkAssets => packagedAssets.Any(record => record.sdkAsset);

        public void AddShared(string assetPath, string address, string location, bool sdkAsset)
        {
            AddRecord(_shared, assetPath, address, location, sdkAsset);
        }

        public void AddPackaged(string assetPath, string location, bool sdkAsset)
        {
            AddRecord(_packaged, assetPath, null, location, sdkAsset);
        }

        public void FinalizeRecords()
        {
            sharedAssets = ToRecords(_shared);
            packagedAssets = ToRecords(_packaged);
        }

        private static void AddRecord(Dictionary<string, AssetRecordAccumulator> map, string assetPath, string address,
            string location, bool sdkAsset)
        {
            var key = SharedAssetAddressUtility.NormalizeAssetPath(assetPath);
            if (!map.TryGetValue(key, out var accumulator))
            {
                accumulator = new AssetRecordAccumulator
                {
                    AssetPath = key,
                    Address = address,
                    SDKAsset = sdkAsset
                };
                map.Add(key, accumulator);
            }

            accumulator.ReferenceCount++;
            accumulator.SDKAsset |= sdkAsset;
            if (string.IsNullOrWhiteSpace(accumulator.Address) && !string.IsNullOrWhiteSpace(address))
                accumulator.Address = address;

            if (!string.IsNullOrWhiteSpace(location))
                accumulator.References.Add(location);
        }

        private static AssetRecord[] ToRecords(Dictionary<string, AssetRecordAccumulator> map)
        {
            return map.Values
                .OrderBy(record => record.AssetPath, StringComparer.OrdinalIgnoreCase)
                .Select(record => new AssetRecord
                {
                    assetPath = record.AssetPath,
                    address = record.Address,
                    sdkAsset = record.SDKAsset,
                    referenceCount = record.ReferenceCount,
                    exampleReferences = record.References.Take(5).ToArray()
                })
                .ToArray();
        }

        [Serializable]
        public sealed class AssetRecord
        {
            public string assetPath;
            public string address;
            public bool sdkAsset;
            public int referenceCount;
            public string[] exampleReferences;
        }

        private sealed class AssetRecordAccumulator
        {
            public readonly HashSet<string> References = new(StringComparer.Ordinal);
            public string Address;
            public string AssetPath;
            public int ReferenceCount;
            public bool SDKAsset;
        }
    }
}
#endif
