// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapProbeVolumeUtility
    {
        private const string ProbeVolumeObjectName = "Adaptive Probe Volume";
        private const string ProbeVolumeTypeName = "UnityEngine.Rendering.ProbeVolume";
        private const string ProbeVolumeBakingSetTypeName = "UnityEngine.Rendering.ProbeVolumeBakingSet";
        private const string ProbeVolumePerSceneDataTypeName = "UnityEngine.Rendering.ProbeVolumePerSceneData";
        private const float RelativePadding = 0.05f;
        private const float MinimumPadding = 5f;
        private const float MinimumAxisSize = 1f;

        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public |
                                                             BindingFlags.NonPublic;

        private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public |
                                                           BindingFlags.NonPublic;

        private static Type _probeVolumeType;
        private static bool _probeVolumeTypeResolved;
        private static Type _probeVolumeBakingSetType;
        private static bool _probeVolumeBakingSetTypeResolved;
        private static readonly Dictionary<Type, PropertyInfo> LightProbeSystemProperties = new();

        internal static bool ConfigureForLightingBake(Scene scene, string scenePath, out string message)
        {
            message = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                message = "Compiled scene is invalid while configuring Adaptive Probe Volumes.";
                return false;
            }

            if (!EnsureAdaptiveProbeVolumesEnabled(out var apvMessage))
            {
                message = apvMessage;
                return false;
            }

            var probeVolumeType = FindProbeVolumeType();
            if (probeVolumeType == null)
            {
                message = "Adaptive Probe Volume setup could not find UnityEngine.Rendering.ProbeVolume.";
                return false;
            }

            if (FindProbeVolumeBakingSetType() == null)
            {
                message =
                    "Adaptive Probe Volume setup could not find UnityEngine.Rendering.ProbeVolumeBakingSet.";
                return false;
            }

            if (!TryCalculateSceneBounds(scene, out var bounds, out var contributorCount))
            {
                message =
                    "Adaptive Probe Volume setup could not find any renderers, terrains, or colliders to bound the map.";
                return false;
            }

            ExpandForBake(ref bounds);
            var probeVolume = EnsureProbeVolume(scene, probeVolumeType, out var createdProbeVolume);
            ConfigureProbeVolume(probeVolume, bounds);

            if (!EnsureSceneBakingSet(scene, scenePath, out var bakingSetPath, out var bakingSetMessage))
            {
                message = bakingSetMessage;
                return false;
            }

            if (!NotifyProbeVolumeSceneSaved(scene, out var sceneDataMessage))
            {
                message = sceneDataMessage;
                return false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            message =
                $"{(createdProbeVolume ? "Created" : "Configured")} '{probeVolume.gameObject.name}' to cover {contributorCount} scene contributor(s): center={Format(bounds.center)}, size={Format(bounds.size)}. " +
                $"{bakingSetMessage} {sceneDataMessage}".Trim();
            return true;
        }

        internal static Object GetBakedData(Scene scene)
        {
            var data = MapSceneTraversalUtility.GetComponents(scene, FindType(ProbeVolumePerSceneDataTypeName))
                .FirstOrDefault();
            return data ? new SerializedObject(data).FindProperty("serializedBakingSet")?.objectReferenceValue : null;
        }

        internal static void ApplyBakedData(Scene scene, string bakingSetPath)
        {
            var bakingSet = AssetDatabase.LoadAssetAtPath(bakingSetPath, FindProbeVolumeBakingSetType());
            if (!bakingSet)
                throw new InvalidOperationException($"Baked probe volume data is missing: {bakingSetPath}");

            if (!ConfigureForLightingBake(scene, scene.path, out var message))
                throw new InvalidOperationException(message);

            var data = MapSceneTraversalUtility.GetComponents(scene, FindType(ProbeVolumePerSceneDataTypeName))
                .OfType<Behaviour>().Single();
            // Registration happens on enable, after the scene GUID and baking set are assigned.
            data.enabled = false;
            var serialized = new SerializedObject(data);
            serialized.FindProperty("serializedBakingSet").objectReferenceValue = bakingSet;
            serialized.FindProperty("sceneGUID").stringValue = AssetDatabase.AssetPathToGUID(scene.path);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            data.enabled = true;
            EditorUtility.SetDirty(data);
        }

        internal static bool TryPrepareBakingSetAsset(string assetPath, string sceneGuid,
            IReadOnlyDictionary<string, string> assetMap, out string message)
        {
            message = string.Empty;
            assetPath = MapEditorPathUtility.NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(sceneGuid))
            {
                message = "Adaptive Probe Volume baking set path or scene guid is missing.";
                return false;
            }

            var bakingSetType = FindProbeVolumeBakingSetType();
            if (bakingSetType == null)
            {
                message =
                    "Adaptive Probe Volume baking set could not be repaired because ProbeVolumeBakingSet is unavailable.";
                return false;
            }

            if (!EnsureSerializedMainObjectNameMatchesFile(assetPath, out message))
                return false;

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var bakingSet = AssetDatabase.LoadAssetAtPath(assetPath, bakingSetType);
            if (!bakingSet)
            {
                message = $"Adaptive Probe Volume baking set could not be loaded: {assetPath}";
                return false;
            }

            bakingSet.name = Path.GetFileNameWithoutExtension(assetPath);
            RemapBakingSetAssets(bakingSet, assetMap);
            if (!TryConfigureBakingSetForScene(bakingSet, sceneGuid, out message))
                return false;

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            message = $"Prepared Adaptive Probe Volume baking set '{assetPath}'.";
            return true;
        }

        private static void RemapBakingSetAssets(Object bakingSet, IReadOnlyDictionary<string, string> assetMap)
        {
            var guidMap = assetMap.ToDictionary(pair => AssetDatabase.AssetPathToGUID(pair.Key),
                pair => AssetDatabase.AssetPathToGUID(pair.Value));
            var serialized = new SerializedObject(bakingSet);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue &&
                    assetMap.TryGetValue(AssetDatabase.GetAssetPath(property.objectReferenceValue), out var target))
                    property.objectReferenceValue = AssetDatabase.LoadAssetAtPath(target,
                        property.objectReferenceValue.GetType());
                else if (property.propertyType == SerializedPropertyType.String)
                {
                    if (property.name == "m_AssetGUID" && guidMap.TryGetValue(property.stringValue, out var guid))
                        property.stringValue = guid;
                    else if (property.name == "m_StreamableAssetPath")
                        foreach (var pair in guidMap)
                            property.stringValue = property.stringValue.Replace(pair.Key, pair.Value);
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RefreshStreamableAssetPaths(bakingSet);
        }

        internal static void RefreshStreamableAssetPaths(Object bakingSet)
        {
            if (!bakingSet)
                return;

            // APV caches filesystem paths outside serialization. ImportAsset does not clear them,
            // and its build processor reads that cache directly when copying streaming payloads.
            RefreshFields(bakingSet);
            if (bakingSet.GetType().GetField("scenarios", InstanceFlags)?.GetValue(bakingSet) is IDictionary scenarios)
                foreach (var scenario in scenarios.Values)
                    RefreshFields(scenario);

            static void RefreshFields(object owner)
            {
                if (owner == null)
                    return;
                foreach (var field in owner.GetType().GetFields(InstanceFlags))
                {
                    var refresh = field.FieldType.GetMethod("RefreshAssetPath", InstanceFlags,
                        null, Type.EmptyTypes, null);
                    if (refresh != null && field.GetValue(owner) is { } stream)
                        refresh.Invoke(stream, null);
                }
            }
        }

        private static bool EnsureSerializedMainObjectNameMatchesFile(string assetPath, out string message)
        {
            message = string.Empty;
            var expectedName = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrWhiteSpace(expectedName))
                return true;

            var absolutePath = MapEditorPathUtility.ToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
                return true;

            string contents;
            try
            {
                contents = File.ReadAllText(absolutePath);
            }
            catch (Exception ex)
            {
                message = $"Could not read Adaptive Probe Volume baking set '{assetPath}': {ex.Message}";
                return false;
            }

            const string marker = "\n  m_Name: ";
            var start = contents.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return true;

            start += marker.Length;
            var end = contents.IndexOf('\n', start);
            if (end < 0)
                end = contents.Length;

            var currentName = contents.Substring(start, end - start).TrimEnd('\r');
            if (string.Equals(currentName, expectedName, StringComparison.Ordinal))
                return true;

            contents = contents.Substring(0, start) + expectedName + contents.Substring(end);
            try
            {
                File.WriteAllText(absolutePath, contents);
                return true;
            }
            catch (Exception ex)
            {
                message =
                    $"Could not repair Adaptive Probe Volume baking set main object name for '{assetPath}': {ex.Message}";
                return false;
            }
        }

        internal static bool IsAdaptiveProbeVolumeEnabled(out string message)
        {
            message = string.Empty;
            var pipelineAsset = GetActiveRenderPipelineAsset();
            if (!pipelineAsset)
            {
                message = "Adaptive Probe Volume setup requires an active Scriptable Render Pipeline asset.";
                return false;
            }

            var lightProbeSystemProperty = GetLightProbeSystemProperty(pipelineAsset);
            if (lightProbeSystemProperty == null)
                return true;

            var value = lightProbeSystemProperty.GetValue(pipelineAsset, null);
            if (value != null && string.Equals(value.ToString(), "ProbeVolumes", StringComparison.Ordinal))
                return true;

            message =
                $"Probe Volumes are disabled on render pipeline asset '{pipelineAsset.name}'.";
            return false;
        }

        internal static bool EnsureAdaptiveProbeVolumesEnabled(out string message)
        {
            if (IsAdaptiveProbeVolumeEnabled(out message))
            {
                message = "Probe Volumes are already enabled.";
                return true;
            }

            var pipelineAsset = GetActiveRenderPipelineAsset();
            if (!pipelineAsset)
                return false;

            var property = GetLightProbeSystemProperty(pipelineAsset);
            if (property == null)
            {
                message =
                    $"Render pipeline asset '{pipelineAsset.name}' does not expose a Light Probe System setting.";
                return false;
            }

            if (!property.CanWrite || !property.PropertyType.IsEnum)
            {
                message =
                    $"The Light Probe System setting on '{pipelineAsset.name}' cannot be changed automatically.";
                return false;
            }

            try
            {
                var probeVolumes = Enum.Parse(property.PropertyType, "ProbeVolumes");
                Undo.RecordObject(pipelineAsset, "Enable Probe Volumes");
                property.SetValue(pipelineAsset, probeVolumes, null);
                EditorUtility.SetDirty(pipelineAsset);
                AssetDatabase.SaveAssets();

                if (!IsAdaptiveProbeVolumeEnabled(out var verificationMessage))
                {
                    message =
                        $"Could not enable Probe Volumes on '{pipelineAsset.name}'. {verificationMessage}";
                    return false;
                }

                message = $"Enabled Probe Volumes on render pipeline asset '{pipelineAsset.name}'.";
                return true;
            }
            catch (Exception ex)
            {
                message =
                    $"Could not enable Probe Volumes on render pipeline asset '{pipelineAsset.name}': {ex.Message}";
                return false;
            }
        }

        private static RenderPipelineAsset GetActiveRenderPipelineAsset()
        {
            return QualitySettings.renderPipeline
                ? QualitySettings.renderPipeline
                : GraphicsSettings.defaultRenderPipeline;
        }

        private static PropertyInfo GetLightProbeSystemProperty(RenderPipelineAsset pipelineAsset)
        {
            var type = pipelineAsset.GetType();
            if (LightProbeSystemProperties.TryGetValue(type, out var property))
                return property;

            property = type.GetProperty("lightProbeSystem", InstanceFlags);
            LightProbeSystemProperties[type] = property;
            return property;
        }

        private static bool TryCalculateSceneBounds(Scene scene, out Bounds bounds, out int contributorCount)
        {
            bounds = default;
            contributorCount = 0;

            foreach (var renderer in MapSceneTraversalUtility.GetComponents<Renderer>(scene))
            {
                if (!renderer || IsIgnored(renderer.transform) || !IsFinite(renderer.bounds))
                    continue;

                Encapsulate(ref bounds, ref contributorCount, renderer.bounds);
            }

            foreach (var terrain in MapSceneTraversalUtility.GetComponents<Terrain>(scene))
            {
                if (!terrain || IsIgnored(terrain.transform) || !terrain.terrainData)
                    continue;

                var terrainBounds = new Bounds(
                    terrain.transform.position + terrain.terrainData.bounds.center,
                    terrain.terrainData.bounds.size);
                if (IsFinite(terrainBounds))
                    Encapsulate(ref bounds, ref contributorCount, terrainBounds);
            }

            if (contributorCount > 0)
                return true;

            foreach (var collider in MapSceneTraversalUtility.GetComponents<Collider>(scene))
            {
                if (!collider || IsIgnored(collider.transform) || !IsFinite(collider.bounds))
                    continue;

                Encapsulate(ref bounds, ref contributorCount, collider.bounds);
            }

            return contributorCount > 0;
        }

        private static void ExpandForBake(ref Bounds bounds)
        {
            var largestAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            var padding = Mathf.Max(MinimumPadding, largestAxis * RelativePadding);
            var size = bounds.size + Vector3.one * (padding * 2f);
            size.x = Mathf.Max(size.x, MinimumAxisSize);
            size.y = Mathf.Max(size.y, MinimumAxisSize);
            size.z = Mathf.Max(size.z, MinimumAxisSize);
            bounds.size = size;
        }

        private static Component EnsureProbeVolume(Scene scene, Type probeVolumeType, out bool created)
        {
            created = false;
            var probeVolume = MapSceneTraversalUtility.GetComponents(scene, probeVolumeType)
                .Where(volume => volume)
                .OrderByDescending(volume => string.Equals(volume.gameObject.name, ProbeVolumeObjectName,
                    StringComparison.Ordinal))
                .FirstOrDefault();

            if (probeVolume)
                return probeVolume;

            var probeObject = new GameObject(ProbeVolumeObjectName);
            SceneManager.MoveGameObjectToScene(probeObject, scene);
            probeVolume = probeObject.AddComponent(probeVolumeType);
            created = true;
            return probeVolume;
        }

        private static void ConfigureProbeVolume(Component probeVolume, Bounds bounds)
        {
            SetEnumField(probeVolume, "mode", "Local");
            SetField(probeVolume, "size", bounds.size);
            SetField(probeVolume, "objectLayerMask", (LayerMask)(-1));
            SetField(probeVolume, "overrideRendererFilters", false);
            SetField(probeVolume, "fillEmptySpaces", false);
            probeVolume.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
            probeVolume.transform.localScale = Vector3.one;

            EditorUtility.SetDirty(probeVolume);
            EditorUtility.SetDirty(probeVolume.gameObject);
        }

        private static bool EnsureSceneBakingSet(Scene scene, string scenePath, out string assetPath,
            out string message)
        {
            assetPath = string.Empty;
            message = string.Empty;

            scenePath = string.IsNullOrWhiteSpace(scenePath)
                ? scene.path
                : MapEditorPathUtility.NormalizeAssetPath(scenePath);
            var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrWhiteSpace(sceneGuid))
            {
                message = $"Adaptive Probe Volume baking set could not resolve a scene guid for '{scenePath}'.";
                return false;
            }

            var bakingSetType = FindProbeVolumeBakingSetType();
            assetPath = GetExpectedBakingSetPath(scenePath, scene.name);
            var bakingSet = AssetDatabase.LoadAssetAtPath(assetPath, bakingSetType);
            if (!bakingSet)
            {
                bakingSet = FindBakingSetForScene(bakingSetType, sceneGuid);
                if (bakingSet)
                {
                    assetPath = AssetDatabase.GetAssetPath(bakingSet);
                }
                else
                {
                    MapEditorPathUtility.EnsureAssetFolder(Path.GetDirectoryName(assetPath));
                    bakingSet = ScriptableObject.CreateInstance(bakingSetType);
                    bakingSet.name = Path.GetFileNameWithoutExtension(assetPath);
                    InvokeInstanceMethod(bakingSet, "SetDefaults");
                    AssetDatabase.CreateAsset(bakingSet, assetPath);
                }
            }

            if (!TryConfigureBakingSetForScene(bakingSet, sceneGuid, out message))
                return false;

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            message = $"Prepared Adaptive Probe Volume baking set '{assetPath}'.";
            return true;
        }

        private static Object FindBakingSetForScene(Type bakingSetType, string sceneGuid)
        {
            if (bakingSetType == null || string.IsNullOrWhiteSpace(sceneGuid))
                return null;

            foreach (var guid in AssetDatabase.FindAssets("t:ProbeVolumeBakingSet"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var bakingSet = AssetDatabase.LoadAssetAtPath(path, bakingSetType);
                if (!bakingSet)
                    continue;

                if (GetSceneGuids(bakingSet).Any(candidate =>
                        string.Equals(candidate, sceneGuid, StringComparison.OrdinalIgnoreCase)))
                    return bakingSet;
            }

            return null;
        }

        private static bool TryConfigureBakingSetForScene(Object bakingSet, string sceneGuid, out string message)
        {
            message = string.Empty;
            var previousSceneGuids = GetSceneGuids(bakingSet);
            var preservedCellList = FindPerSceneCellList(bakingSet, sceneGuid, previousSceneGuids);
            var previousGuid = previousSceneGuids.Contains(sceneGuid) ? sceneGuid : previousSceneGuids.FirstOrDefault();
            TryInvokeInstanceMethod(bakingSet, "GetSceneBakeData", new object[] { previousGuid ?? sceneGuid, false },
                out var preservedBakeData);

            if (!ResetBakingSetSceneList(bakingSet, sceneGuid, preservedBakeData, out message))
                return false;

            RemapPerSceneCellLists(bakingSet, sceneGuid, preservedCellList);
            SetSerializedField(bakingSet, "singleSceneMode", true);
            SetSerializedField(bakingSet, "dialogNoProbeVolumeInSetShown", true);

            if (!ValidateBakingSetScene(bakingSet, sceneGuid, out message))
                return false;

            EditorUtility.SetDirty(bakingSet);
            return true;
        }

        private static bool ResetBakingSetSceneList(Object bakingSet, string sceneGuid, object preservedBakeData,
            out string message)
        {
            message = string.Empty;
            var existingGuids = GetSceneGuids(bakingSet).ToArray();
            foreach (var existingGuid in existingGuids)
                InvokeInstanceMethod(bakingSet, "RemoveScene", existingGuid);

            if (!TryInvokeInstanceMethod(bakingSet, "AddScene", new[] { sceneGuid, preservedBakeData }, out _))
            {
                message =
                    $"Adaptive Probe Volume baking set '{bakingSet.name}' could not attach scene guid '{sceneGuid}'.";
                return false;
            }

            if (TryInvokeInstanceMethod(bakingSet, "GetSceneBakeData", new object[] { sceneGuid, true },
                    out var bakeData) && bakeData != null)
                return true;

            message =
                $"Adaptive Probe Volume baking set '{bakingSet.name}' could not create default bake data for scene guid '{sceneGuid}'.";
            return false;
        }

        private static bool ValidateBakingSetScene(Object bakingSet, string sceneGuid, out string message)
        {
            message = string.Empty;
            if (!GetSceneGuids(bakingSet).Any(candidate =>
                    string.Equals(candidate, sceneGuid, StringComparison.OrdinalIgnoreCase)))
            {
                message =
                    $"Adaptive Probe Volume baking set '{bakingSet.name}' did not attach scene guid '{sceneGuid}'.";
                return false;
            }

            if (TryInvokeInstanceMethod(bakingSet, "GetSceneBakeData", new object[] { sceneGuid, false },
                    out var bakeData) && bakeData != null)
                return true;

            message =
                $"Adaptive Probe Volume baking set '{bakingSet.name}' is missing default bake data for scene guid '{sceneGuid}'.";
            return false;
        }

        private static object FindPerSceneCellList(Object bakingSet, string sceneGuid,
            IEnumerable<string> previousSceneGuids)
        {
            var perSceneCellLists = GetPerSceneCellLists(bakingSet);
            if (perSceneCellLists == null)
                return null;

            if (perSceneCellLists.Contains(sceneGuid))
                return perSceneCellLists[sceneGuid];

            foreach (var previousSceneGuid in previousSceneGuids)
                if (!string.IsNullOrWhiteSpace(previousSceneGuid) &&
                    perSceneCellLists.Contains(previousSceneGuid))
                    return perSceneCellLists[previousSceneGuid];

            return null;
        }

        private static void RemapPerSceneCellLists(Object bakingSet, string sceneGuid, object preservedCellList)
        {
            var perSceneCellLists = GetPerSceneCellLists(bakingSet);
            if (perSceneCellLists == null)
                return;

            perSceneCellLists.Clear();
            perSceneCellLists[sceneGuid] = preservedCellList ?? new List<int>();
        }

        private static IDictionary GetPerSceneCellLists(Object bakingSet)
        {
            return bakingSet.GetType().GetField("perSceneCellLists", InstanceFlags)?.GetValue(bakingSet) as
                IDictionary;
        }

        private static bool NotifyProbeVolumeSceneSaved(Scene scene, out string message)
        {
            var bakingSetType = FindProbeVolumeBakingSetType();
            var method = bakingSetType?.GetMethod("OnSceneSaving", StaticFlags);
            if (method == null)
            {
                message =
                    "Adaptive Probe Volume scene data could not be refreshed because ProbeVolumeBakingSet.OnSceneSaving is unavailable.";
                return false;
            }

            try
            {
                method.Invoke(null, new object[] { scene, scene.path });
                message = "Refreshed Adaptive Probe Volume per-scene bake data.";
                return true;
            }
            catch (Exception ex)
            {
                message =
                    $"Adaptive Probe Volume scene data refresh failed: {ex.InnerException?.Message ?? ex.Message}";
                return false;
            }
        }

        private static void InvokeInstanceMethod(Object target, string methodName)
        {
            target.GetType().GetMethod(methodName, InstanceFlags)?.Invoke(target, null);
        }

        private static void InvokeInstanceMethod(Object target, string methodName, object argument)
        {
            target.GetType().GetMethod(methodName, InstanceFlags, null, new[] { argument.GetType() }, null)
                ?.Invoke(target, new[] { argument });
        }

        private static void InvokeInstanceMethod(Object target, string methodName, object[] arguments)
        {
            TryInvokeInstanceMethod(target, methodName, arguments, out _);
        }

        private static bool TryInvokeInstanceMethod(Object target, string methodName, object[] arguments,
            out object result)
        {
            result = null;
            var method = target.GetType()
                .GetMethods(InstanceFlags)
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, methodName, StringComparison.Ordinal) &&
                    ParametersMatch(candidate.GetParameters(), arguments));
            if (method == null)
                return false;

            result = method.Invoke(target, arguments);
            return true;
        }

        private static bool ParametersMatch(ParameterInfo[] parameters, object[] arguments)
        {
            if (parameters.Length != arguments.Length)
                return false;

            for (var i = 0; i < parameters.Length; i++)
            {
                var argument = arguments[i];
                if (argument == null)
                    continue;

                if (!parameters[i].ParameterType.IsInstanceOfType(argument))
                    return false;
            }

            return true;
        }

        private static void SetSerializedField(Object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, InstanceFlags)?.SetValue(target, value);
        }

        private static void SetField(Component target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceFlags);
            if (field == null)
                return;

            field.SetValue(target, value);
        }

        private static void SetEnumField(Component target, string fieldName, string valueName)
        {
            var field = target.GetType().GetField(fieldName, InstanceFlags);
            if (field == null || !field.FieldType.IsEnum)
                return;

            field.SetValue(target, Enum.Parse(field.FieldType, valueName));
        }

        private static string[] GetSceneGuids(Object bakingSet)
        {
            var property = bakingSet.GetType().GetProperty("sceneGUIDs", InstanceFlags);
            if (property?.GetValue(bakingSet, null) is not IEnumerable values)
                return Array.Empty<string>();

            return values.OfType<string>()
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        private static Type FindProbeVolumeType()
        {
            if (_probeVolumeTypeResolved)
                return _probeVolumeType;

            _probeVolumeType = FindType(ProbeVolumeTypeName);
            _probeVolumeTypeResolved = true;
            return _probeVolumeType;
        }

        private static Type FindProbeVolumeBakingSetType()
        {
            if (_probeVolumeBakingSetTypeResolved)
                return _probeVolumeBakingSetType;

            _probeVolumeBakingSetType = FindType(ProbeVolumeBakingSetTypeName);
            _probeVolumeBakingSetTypeResolved = true;
            return _probeVolumeBakingSetType;
        }

        private static Type FindType(string fullName)
        {
            return Type.GetType($"{fullName}, Unity.RenderPipelines.Core.Runtime") ??
                   AppDomain.CurrentDomain.GetAssemblies()
                       .Where(assembly => assembly != null && !assembly.IsDynamic)
                       .Select(assembly => assembly.GetType(fullName, false))
                       .FirstOrDefault(type => type != null);
        }

        private static string GetExpectedBakingSetPath(string scenePath, string sceneName)
        {
            var sceneDirectory = MapEditorPathUtility.GetSceneDataFolderAssetPath(scenePath);
            var safeSceneName = string.IsNullOrWhiteSpace(sceneName)
                ? Path.GetFileNameWithoutExtension(scenePath)
                : sceneName;
            return $"{sceneDirectory}/{safeSceneName} Baking Set.asset";
        }

        private static bool IsIgnored(Transform transform)
        {
            while (transform)
            {
                if (transform.CompareTag("EditorOnly") ||
                    string.Equals(transform.name, "Navigation", StringComparison.Ordinal) ||
                    string.Equals(transform.name, "MapRuntime", StringComparison.Ordinal) ||
                    string.Equals(transform.name, "VSigRuntime", StringComparison.Ordinal))
                    return true;

                transform = transform.parent;
            }

            return false;
        }

        private static void Encapsulate(ref Bounds bounds, ref int contributorCount, Bounds contributorBounds)
        {
            if (contributorCount == 0)
                bounds = contributorBounds;
            else
                bounds.Encapsulate(contributorBounds);

            contributorCount++;
        }

        private static bool IsFinite(Bounds bounds)
        {
            return IsFinite(bounds.center) && IsFinite(bounds.size) &&
                   bounds.size.x >= 0f && bounds.size.y >= 0f && bounds.size.z >= 0f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }
    }
}
#endif
