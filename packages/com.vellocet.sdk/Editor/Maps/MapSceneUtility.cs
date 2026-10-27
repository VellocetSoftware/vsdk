// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities.Base;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class MapSceneUtility
    {
        public static void UnpackPrefabInstances(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject))
                        PrefabUtility.UnpackPrefabInstance(transform.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        public enum BakeState
        {
            Missing = 0,
            Pending = 1,
            Ready = 2
        }

        private const string NavMeshSurfaceTypeName = "Unity.AI.Navigation.NavMeshSurface";
        private const string NavigationRootName = "Navigation";
        private const string DirectionalLightName = "Directional Light";
        private const string DefaultMainCameraName = "Main Camera";
        private const string MainCameraTag = "MainCamera";
        private const int CollectAllObjects = 0;
        private const int PhysicsColliderGeometry = 1;
        private const int WalkableArea = 0;
        private static readonly Quaternion DefaultDirectionalLightRotation = Quaternion.Euler(50f, -30f, 0f);
        private static readonly Dictionary<Type, MethodInfo> BuildNavMeshMethodCache = new();
        private static readonly Dictionary<Type, PropertyInfo> NavMeshDataPropertyCache = new();
        private static MethodInfo _occlusionComputeMethod;
        private static bool _occlusionComputeMethodResolved;
        private static Type _navMeshSurfaceType;
        private static bool _navMeshSurfaceTypeResolved;
        private static PropertyInfo _occlusionCullingSettingsProperty;
        private static bool _occlusionCullingSettingsPropertyResolved;

        public static bool TryGetContext(Scene scene, out MapSceneContext context)
        {
            context = default;
            if (!scene.IsValid())
                return false;

            context = BuildContext(scene);
            return true;
        }

        public static MapSceneContext EnsureSceneContract(Scene scene, bool registerUndo, out string summary)
        {
            summary = string.Empty;
            if (!scene.IsValid())
                return default;

            var touched = new List<string>();
            if (!HumanoidNavMeshSettingsPolicy.Ensure(out var settingsChanged, out var settingsError))
                throw new InvalidOperationException(
                    $"Could not enforce Humanoid NavMesh settings: {settingsError}");

            if (settingsChanged)
                touched.Add("Enforced optimal Humanoid NavMesh agent settings");

            var navigationRoot = FindNavigationRoot(scene);
            var navigationSurface = FindNavigationSurface(scene);
            if (!navigationRoot && navigationSurface)
                navigationRoot = navigationSurface.gameObject;

            if (!navigationRoot)
            {
                navigationRoot = new GameObject(NavigationRootName);
                SceneManager.MoveGameObjectToScene(navigationRoot, scene);
                if (registerUndo)
                    Undo.RegisterCreatedObjectUndo(navigationRoot, "Create Navigation Root");
                touched.Add($"Created {NavigationRootName} root");
            }
            else if (!string.Equals(navigationRoot.name, NavigationRootName, StringComparison.Ordinal))
            {
                if (registerUndo)
                    Undo.RecordObject(navigationRoot, "Rename Navigation Root");
                navigationRoot.name = NavigationRootName;
                touched.Add($"Renamed navigation root to {NavigationRootName}");
            }

            navigationSurface = EnsureNavMeshSurface(navigationRoot, registerUndo, touched);
            EnforceAdditionalHumanoidSurfaces(scene, navigationSurface, registerUndo, touched);
            RemoveDefaultMainCameras(scene, registerUndo, touched);
            var directionalLight = EnsureDirectionalLight(scene, registerUndo, touched);

            if (navigationRoot.transform.GetSiblingIndex() != 0)
                navigationRoot.transform.SetSiblingIndex(0);

            if (directionalLight && directionalLight.transform.parent == null &&
                directionalLight.transform.GetSiblingIndex() == 0)
                directionalLight.transform.SetSiblingIndex(1);

            if (navigationRoot)
                EditorUtility.SetDirty(navigationRoot);
            if (directionalLight)
                EditorUtility.SetDirty(directionalLight);

            if (touched.Count > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            summary = touched.Count == 0
                ? "Scene already matches the required map contract."
                : string.Join("\n", touched.Distinct(StringComparer.Ordinal));

            return BuildContext(scene);
        }

        /// <summary>
        ///     Enforces the SDK's complete Humanoid NavMeshSurface contract. This is the single repair path used
        ///     by map setup, map baking, and base-game validation.
        /// </summary>
        public static bool TryEnforceHumanoidNavigationSurface(
            Component surface,
            bool registerUndo,
            out bool changed,
            out string error)
        {
            changed = false;
            error = string.Empty;
            if (!surface || !string.Equals(surface.GetType().FullName, NavMeshSurfaceTypeName,
                    StringComparison.Ordinal))
            {
                error = "The target component is not a supported NavMeshSurface.";
                return false;
            }

            if (surface is not Behaviour behaviour)
            {
                error = "The NavMeshSurface is not an enableable Unity behaviour.";
                return false;
            }

            var parent = behaviour.transform.parent;
            if (parent && !parent.gameObject.activeInHierarchy)
            {
                error =
                    $"Humanoid NavMeshSurface '{BuildHierarchyPath(behaviour.transform)}' is under an inactive " +
                    "parent. Activate its hierarchy before baking navigation.";
                return false;
            }

            var serialized = new SerializedObject(surface);
            serialized.Update();
            if (!TryGetNavigationSurfaceProperties(serialized, out var properties, out error))
                return false;

            var serializedChanged = !AreNavigationSurfacePropertiesCurrent(properties, out _);
            var behaviourChanged = !behaviour.enabled;
            var gameObjectChanged = !behaviour.gameObject.activeSelf;

            if ((serializedChanged || behaviourChanged) && registerUndo)
                Undo.RecordObject(surface, "Enforce Humanoid Navigation Surface");
            if (gameObjectChanged && registerUndo)
                Undo.RecordObject(behaviour.gameObject, "Enable Humanoid Navigation Surface");

            if (serializedChanged)
            {
                SetNavigationSurfaceProperties(properties);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (behaviourChanged)
                behaviour.enabled = true;
            if (gameObjectChanged)
                behaviour.gameObject.SetActive(true);

            changed = serializedChanged || behaviourChanged || gameObjectChanged;
            if (!changed)
                return true;

            EditorUtility.SetDirty(surface);
            if (gameObjectChanged)
                EditorUtility.SetDirty(behaviour.gameObject);
            return true;
        }

        /// <summary>Checks whether a NavMeshSurface matches every SDK-controlled Humanoid setting.</summary>
        public static bool TryValidateHumanoidNavigationSurface(Component surface, out string mismatch)
        {
            mismatch = string.Empty;
            if (!surface || !string.Equals(surface.GetType().FullName, NavMeshSurfaceTypeName,
                    StringComparison.Ordinal))
            {
                mismatch = "The target component is not a supported NavMeshSurface.";
                return false;
            }

            if (surface is not Behaviour behaviour)
            {
                mismatch = "The NavMeshSurface is not an enableable Unity behaviour.";
                return false;
            }

            if (!behaviour.enabled || !behaviour.gameObject.activeInHierarchy)
            {
                mismatch = $"Humanoid NavMeshSurface '{BuildHierarchyPath(behaviour.transform)}' must be enabled and active.";
                return false;
            }

            var serialized = new SerializedObject(surface);
            serialized.Update();
            return TryGetNavigationSurfaceProperties(serialized, out var properties, out mismatch) &&
                   AreNavigationSurfacePropertiesCurrent(properties, out mismatch);
        }

        /// <summary>
        ///     Checks whether the compiler can normalize this surface. Current values may drift because the
        ///     compiled copy is repaired before its navigation bake.
        /// </summary>
        public static bool CanEnforceHumanoidNavigationSurface(Component surface, out string error)
        {
            error = string.Empty;
            if (!surface || !string.Equals(surface.GetType().FullName, NavMeshSurfaceTypeName,
                    StringComparison.Ordinal))
            {
                error = "The target component is not a supported NavMeshSurface.";
                return false;
            }

            if (surface is not Behaviour behaviour)
            {
                error = "The NavMeshSurface is not an enableable Unity behaviour.";
                return false;
            }

            var parent = behaviour.transform.parent;
            if (parent && !parent.gameObject.activeInHierarchy)
            {
                error =
                    $"Humanoid NavMeshSurface '{BuildHierarchyPath(behaviour.transform)}' is under an inactive " +
                    "parent that the compiler cannot safely activate.";
                return false;
            }

            var serialized = new SerializedObject(surface);
            serialized.Update();
            return TryGetNavigationSurfaceProperties(serialized, out _, out error);
        }

        public static BakeState GetNavigationState(Scene scene)
        {
            if (!scene.IsValid())
                return BakeState.Missing;

            var context = BuildContext(scene);
            return EvaluateNavigationState(scene, context.NavigationSurface);
        }

        public static BakeState GetLightingState(Scene scene)
        {
            if (!scene.IsValid())
                return BakeState.Missing;

            return EvaluateLightingState();
        }

        public static BakeState GetOcclusionState(Scene scene)
        {
            if (!scene.IsValid())
                return BakeState.Missing;

            return EvaluateOcclusionState();
        }

        public static bool HasBakedLighting()
        {
            var lightingData = Lightmapping.lightingDataAsset;
            if (!lightingData)
                return false;

            var path = AssetDatabase.GetAssetPath(lightingData);
            return !string.IsNullOrWhiteSpace(path);
        }

        public static bool HasBakedOcclusion()
        {
            // RendererScene's live PVS size can still be zero immediately after a batch scene load.
            // The persisted scene reference is authoritative for bake completion and reloads.
            var data = GetOcclusionData();
            return data && EditorUtility.IsPersistent(data) && File.Exists(AssetDatabase.GetAssetPath(data));
        }

        public static bool HasBakedNavMesh(Scene scene)
        {
            return HasBakedNavMesh(FindNavigationSurface(scene));
        }

        private static bool HasBakedNavMesh(Component surface)
        {
            return GetNavigationData(surface) != null;
        }

        public static Object GetNavigationData(Scene scene)
        {
            return !scene.IsValid() ? null : GetNavigationData(FindNavigationSurface(scene));
        }

        public static bool SetNavigationData(Scene scene, Object navMeshData)
        {
            if (!scene.IsValid() || !navMeshData)
                return false;

            var context = EnsureSceneContract(scene, true, out _);
            return SetNavigationData(context.NavigationSurface, navMeshData);
        }

        public static Object GetOcclusionData()
        {
            var settings = GetOcclusionCullingSettings();
            if (!settings)
                return null;

            var property = new SerializedObject(settings).FindProperty("m_OcclusionCullingData");
            return property is { propertyType: SerializedPropertyType.ObjectReference } &&
                   property.objectReferenceValue
                ? property.objectReferenceValue
                : null;
        }

        public static bool SetOcclusionData(Object occlusionData)
        {
            var settings = GetOcclusionCullingSettings();
            if (!settings || !occlusionData)
                return false;

            var serialized = new SerializedObject(settings);
            var property = serialized.FindProperty("m_OcclusionCullingData");
            if (property is not { propertyType: SerializedPropertyType.ObjectReference })
                return false;

            property.objectReferenceValue = occlusionData;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            return true;
        }

        public static bool BakeNavigation(Scene scene, out string message)
        {
            MapSceneContext context;
            try
            {
                context = EnsureSceneContract(scene, true, out _);
            }
            catch (Exception exception)
            {
                message = $"Navigation settings could not be enforced: {exception.Message}";
                return false;
            }

            if (!context.NavigationSurface)
            {
                message = "NavMeshSurface could not be created. Install the AI Navigation package to bake navigation.";
                return false;
            }

            var method = GetBuildNavMeshMethod(context.NavigationSurface.GetType());
            if (method == null)
            {
                message = "NavMeshSurface.BuildNavMesh is not available in this Unity editor version.";
                return false;
            }

            method.Invoke(context.NavigationSurface, null);
            var data = GetNavigationData(context.NavigationSurface);
            if (!data)
            {
                message = "Navigation bake produced no NavMesh data. Check the walkable geometry.";
                return false;
            }
            if (!EditorUtility.IsPersistent(data))
            {
                var folder = MapEditorPathUtility.GetSceneDataFolderAssetPath(scene.path);
                if (string.IsNullOrEmpty(folder))
                {
                    message = "Save the scene before baking navigation.";
                    return false;
                }
                Directory.CreateDirectory(MapEditorPathUtility.ToAbsolutePath(folder));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath(folder + "/Navigation.asset"));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            message = "Navigation bake completed.";
            return true;
        }

        public static bool BakeLightingBlocking(Scene scene, out string message)
        {
            if (!PrepareLightingBake(scene, out message))
                return false;

            bool started;
            using (var logScope = new MapLightingBakeDiagnosticsUtility.LightmapperLogScope())
            {
                started = Lightmapping.Bake();
                if (logScope.HasNonFiniteOutput)
                {
                    message = logScope.BuildFailureMessage();
                    return false;
                }
            }

            if (started)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                message = "Lighting bake completed.";
                return true;
            }

            message = "Lighting bake could not be started.";
            return false;
        }

        private static bool PrepareLightingBake(Scene scene, out string message)
        {
            EnsureSceneContract(scene, true, out _);
            if (Lightmapping.isRunning)
            {
                message = "Lighting bake is already running.";
                return false;
            }

            if (!MapProbeVolumeUtility.ConfigureForLightingBake(scene, scene.path, out message))
                return false;

            if (!EnsureLightingBakeOutputDirectory(scene, out message))
                return false;

            if (!MapLightingBakeDiagnosticsUtility.ValidateBakeInputs(scene, out message))
                return false;

            return true;
        }

        private static bool EnsureLightingBakeOutputDirectory(Scene scene, out string message)
        {
            message = string.Empty;
            var scenePath = scene.path;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                message = "Lighting bake requires a saved scene path.";
                return false;
            }

            var sceneDataAssetPath = MapEditorPathUtility.GetSceneDataFolderAssetPath(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDataAssetPath))
            {
                message = $"Lighting bake output path is invalid: {scenePath}";
                return false;
            }

            var sceneDataAbsolutePath = MapEditorPathUtility.ToAbsolutePath(sceneDataAssetPath);

            try
            {
                Directory.CreateDirectory(sceneDataAbsolutePath);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Could not prepare lighting bake output folder '{sceneDataAssetPath}': {ex.Message}";
                return false;
            }
        }

        public static bool BakeOcclusion(Scene scene, out string message)
        {
            EnsureSceneContract(scene, true, out _);
            var method = GetOcclusionComputeMethod();
            if (method == null)
            {
                message = "Occlusion bake is not available in this Unity editor version.";
                return false;
            }

            GameObject bakeVolume = null;
            var rendererMaterials = new List<(MeshRenderer Renderer, Material[] Materials)>();
            var emptyMeshFlags = new List<(GameObject Host, StaticEditorFlags Flags)>();
            try
            {
                // Unity renders extra material slots using the last submesh, but Umbra indexes each
                // slot as a distinct submesh. Limit only its bake input, then restore every visual pass.
                foreach (var renderer in MapSceneTraversalUtility.GetComponents<MeshRenderer>(scene, true))
                {
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (!mesh || mesh.vertexCount == 0 || mesh.subMeshCount == 0 ||
                        !mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Position))
                    {
                        // Empty imported sections may contain only UVs. Umbra still requests positions
                        // for static renderers and asserts when copying that absent vertex channel.
                        var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                        if (flags != 0)
                        {
                            emptyMeshFlags.Add((renderer.gameObject, flags));
                            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
                        }
                        continue;
                    }
                    var materials = renderer.sharedMaterials;
                    if (materials.Length <= mesh.subMeshCount) continue;
                    rendererMaterials.Add((renderer, materials));
                    renderer.sharedMaterials = materials.Take(mesh.subMeshCount).ToArray();
                }

                // Nodraw can leave only a flat floor. Unity's automatic view volume then has zero height.
                if (!MapSceneTraversalUtility.GetComponents<OcclusionArea>(scene, false).Any() &&
                    !MapSceneTraversalUtility.GetComponents<Terrain>(scene, false).Any())
                {
                    var renderers = MapSceneTraversalUtility.GetComponents<Renderer>(scene, false)
                        .Where(renderer => renderer.enabled && (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) &
                            (StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic)) != 0).ToArray();
                    if (renderers.Length > 0)
                    {
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                        if (bounds.size.x == 0 || bounds.size.y == 0 || bounds.size.z == 0)
                        {
                            bounds.Expand(2 * Mathf.Max(1, StaticOcclusionCulling.smallestOccluder));
                            bakeVolume = new GameObject("Occlusion bake bounds");
                            SceneManager.MoveGameObjectToScene(bakeVolume, scene);
                            var area = bakeVolume.AddComponent<OcclusionArea>();
                            area.center = bounds.center; area.size = bounds.size;
                        }
                    }
                }
                string loggedError = null;
                Application.LogCallback captureError = (text, _, type) =>
                {
                    if (type is LogType.Error or LogType.Exception or LogType.Assert)
                        loggedError ??= text;
                };
                object outcome;
                Application.logMessageReceived += captureError;
                try { outcome = method.Invoke(null, null); }
                finally { Application.logMessageReceived -= captureError; }
                if (loggedError != null)
                {
                    message = "Occlusion baker reported an error: " + loggedError;
                    return false;
                }
                if (outcome is false)
                {
                    message = "Occlusion bake failed. Check the static geometry and bake bounds.";
                    return false;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                message = "Occlusion bake completed.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Occlusion bake failed: {ex.InnerException?.Message ?? ex.Message}";
                return false;
            }
            finally
            {
                foreach (var (renderer, materials) in rendererMaterials)
                    if (renderer) renderer.sharedMaterials = materials;
                foreach (var (host, flags) in emptyMeshFlags)
                    if (host) GameObjectUtility.SetStaticEditorFlags(host, flags);
                if (bakeVolume) Object.DestroyImmediate(bakeVolume);
            }
        }

        public static string DescribeContract(MapSceneContext context)
        {
            if (context.IsSceneContractSatisfied)
                return "Ready";

            var issues = new List<string>();
            if (!context.HasNavigationSetup)
                issues.Add("Navigation");
            else if (!context.HasConfiguredNavigation)
                issues.Add("Navigation Settings");
            if (!context.HasDirectionalLight)
                issues.Add("Directional Light");
            if (context.HasMainCamera)
                issues.Add("Main Camera");

            return issues.Count == 0
                ? "Missing"
                : $"Missing ({string.Join(", ", issues)})";
        }

        private static MapSceneContext BuildContext(Scene scene)
        {
            var navigationSurface = FindNavigationSurface(scene);
            var navigationRoot = FindNavigationRoot(scene);
            if (!navigationRoot && navigationSurface)
                navigationRoot = navigationSurface.gameObject;

            var directionalLights = FindDirectionalLights(scene);
            var directionalLight = directionalLights.FirstOrDefault(IsUsableDirectionalLight) ??
                                   directionalLights.FirstOrDefault();
            var mainCameras = FindDefaultMainCameras(scene);

            return new MapSceneContext(scene, navigationRoot, navigationSurface, directionalLight,
                directionalLights.Length, directionalLights.Count(IsUsableDirectionalLight),
                mainCameras.FirstOrDefault(),
                mainCameras.Length);
        }

        private static GameObject FindNavigationRoot(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            return roots.FirstOrDefault(root =>
                root && string.Equals(root.name, NavigationRootName, StringComparison.Ordinal));
        }

        private static Component FindNavigationSurface(Scene scene)
        {
            var surfaceType = FindNavMeshSurfaceType();
            if (surfaceType == null)
                return null;

            return MapSceneTraversalUtility.GetComponents(scene, surfaceType)
                .FirstOrDefault(component => component);
        }

        private static Light[] FindDirectionalLights(Scene scene)
        {
            if (!scene.IsValid())
                return Array.Empty<Light>();

            return MapSceneTraversalUtility.GetComponents<Light>(scene)
                .Where(light => light && light.type == LightType.Directional)
                .ToArray();
        }

        private static Camera[] FindSceneCameras(Scene scene)
        {
            if (!scene.IsValid())
                return Array.Empty<Camera>();

            return MapSceneTraversalUtility.GetComponents<Camera>(scene);
        }

        private static Camera[] FindDefaultMainCameras(Scene scene)
        {
            return FindSceneCameras(scene)
                .Where(IsDefaultMainCamera)
                .ToArray();
        }

        private static Component EnsureNavMeshSurface(GameObject navigationRoot, bool registerUndo,
            ICollection<string> touched)
        {
            if (!navigationRoot)
                return null;

            var surfaceType = FindNavMeshSurfaceType();
            if (surfaceType == null)
                throw new InvalidOperationException(
                    "AI Navigation does not expose NavMeshSurface. Install the SDK's required AI Navigation package.");

            var surface = navigationRoot.GetComponent(surfaceType);
            if (!surface)
            {
                surface = registerUndo
                    ? Undo.AddComponent(navigationRoot, surfaceType)
                    : navigationRoot.AddComponent(surfaceType);
                touched?.Add("Added NavMeshSurface");
            }

            if (!TryEnforceHumanoidNavigationSurface(surface, registerUndo, out var changed, out var error))
                throw new InvalidOperationException(error);

            if (changed)
                touched?.Add("Enforced optimal Humanoid NavMeshSurface settings");
            return surface;
        }

        private static void EnforceAdditionalHumanoidSurfaces(
            Scene scene,
            Component primarySurface,
            bool registerUndo,
            ICollection<string> touched)
        {
            var surfaceType = FindNavMeshSurfaceType();
            if (surfaceType == null)
                return;

            var changedCount = 0;
            var surfaces = MapSceneTraversalUtility.GetComponents(scene, surfaceType);
            for (var index = 0; index < surfaces.Length; index++)
            {
                var surface = surfaces[index];
                if (!surface || surface == primarySurface || !IsHumanoidNavigationSurface(surface))
                    continue;

                if (!TryEnforceHumanoidNavigationSurface(surface, registerUndo, out var changed, out var error))
                    throw new InvalidOperationException(error);
                if (changed)
                    changedCount++;
            }

            if (changedCount > 0)
                touched?.Add($"Enforced optimal settings on {changedCount} additional Humanoid NavMeshSurface(s)");
        }

        private static bool IsHumanoidNavigationSurface(Component surface)
        {
            if (!surface)
                return false;

            var serialized = new SerializedObject(surface);
            var agentType = serialized.FindProperty("m_AgentTypeID");
            return agentType is { propertyType: SerializedPropertyType.Integer } &&
                   agentType.intValue == HumanoidNavMeshSettingsPolicy.AgentTypeId;
        }

        private static Light EnsureDirectionalLight(Scene scene, bool registerUndo, ICollection<string> touched)
        {
            var directionalLight = FindDirectionalLights(scene).FirstOrDefault();
            if (!directionalLight)
            {
                var lightObject = new GameObject(DirectionalLightName);
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                if (registerUndo)
                    Undo.RegisterCreatedObjectUndo(lightObject, "Create Directional Light");

                directionalLight = registerUndo
                    ? Undo.AddComponent<Light>(lightObject)
                    : lightObject.AddComponent<Light>();
                directionalLight.type = LightType.Directional;
                directionalLight.intensity = 1f;
                directionalLight.shadows = LightShadows.Soft;
                lightObject.transform.rotation = DefaultDirectionalLightRotation;
                touched?.Add($"Created {DirectionalLightName}");
                return directionalLight;
            }

            var changed = false;
            if (!directionalLight.gameObject.activeSelf)
            {
                if (registerUndo)
                    Undo.RecordObject(directionalLight.gameObject, "Enable Directional Light");

                directionalLight.gameObject.SetActive(true);
                changed = true;
            }

            if (!directionalLight.enabled)
            {
                if (registerUndo)
                    Undo.RecordObject(directionalLight, "Enable Directional Light");

                directionalLight.enabled = true;
                changed = true;
            }

            if (changed)
                touched?.Add($"Enabled directional light '{directionalLight.name}'");

            return FindDirectionalLights(scene).FirstOrDefault(IsUsableDirectionalLight) ?? directionalLight;
        }

        private static void RemoveDefaultMainCameras(Scene scene, bool registerUndo, ICollection<string> touched)
        {
            var cameras = FindDefaultMainCameras(scene);
            if (cameras.Length == 0)
                return;

            for (var i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];
                if (!camera)
                    continue;

                if (registerUndo)
                    Undo.DestroyObjectImmediate(camera.gameObject);
                else
                    Object.DestroyImmediate(camera.gameObject);
            }

            touched?.Add(cameras.Length == 1
                ? "Removed default main camera"
                : $"Removed {cameras.Length} default main cameras");
        }

        private static bool TryGetNavigationSurfaceProperties(
            SerializedObject serialized,
            out NavigationSurfaceProperties properties,
            out string error)
        {
            properties = new NavigationSurfaceProperties();
            if (!TryGetIntegerProperty(serialized, "m_AgentTypeID", out properties.AgentTypeId, out error) ||
                !TryGetEnumProperty(serialized, "m_CollectObjects", out properties.CollectObjects, out error) ||
                !TryGetLayerMaskProperty(serialized, "m_LayerMask", out properties.LayerMask, out error) ||
                !TryGetEnumProperty(serialized, "m_UseGeometry", out properties.UseGeometry, out error) ||
                !TryGetIntegerProperty(serialized, "m_DefaultArea", out properties.DefaultArea, out error) ||
                !TryGetBooleanProperty(serialized, "m_GenerateLinks", out properties.GenerateLinks, out error) ||
                !TryGetBooleanProperty(serialized, "m_IgnoreNavMeshAgent", out properties.IgnoreAgent, out error) ||
                !TryGetBooleanProperty(serialized, "m_IgnoreNavMeshObstacle", out properties.IgnoreObstacle,
                    out error) ||
                !TryGetBooleanProperty(serialized, "m_OverrideTileSize", out properties.OverrideTileSize,
                    out error) ||
                !TryGetIntegerProperty(serialized, "m_TileSize", out properties.TileSize, out error) ||
                !TryGetBooleanProperty(serialized, "m_OverrideVoxelSize", out properties.OverrideVoxelSize,
                    out error) ||
                !TryGetFloatProperty(serialized, "m_VoxelSize", out properties.VoxelSize, out error) ||
                !TryGetFloatProperty(serialized, "m_MinRegionArea", out properties.MinRegionArea, out error) ||
                !TryGetBooleanProperty(serialized, "m_BuildHeightMesh", out properties.BuildHeightMesh, out error))
                return false;

            error = string.Empty;
            return true;
        }

        private static bool AreNavigationSurfacePropertiesCurrent(
            NavigationSurfaceProperties properties,
            out string mismatch)
        {
            mismatch = string.Empty;
            return MatchesInteger(properties.AgentTypeId, HumanoidNavMeshSettingsPolicy.AgentTypeId,
                       "Agent Type", ref mismatch) &&
                   MatchesInteger(properties.CollectObjects, CollectAllObjects, "Collect Objects", ref mismatch) &&
                   MatchesInteger(properties.LayerMask, SdkGame.Current.NavigationMask, "Include Layers", ref mismatch) &&
                   MatchesInteger(properties.UseGeometry, PhysicsColliderGeometry, "Use Geometry", ref mismatch) &&
                   MatchesInteger(properties.DefaultArea, WalkableArea, "Default Area", ref mismatch) &&
                   MatchesBoolean(properties.GenerateLinks, true, "Generate Links", ref mismatch) &&
                   MatchesBoolean(properties.IgnoreAgent, true, "Ignore NavMesh Agent", ref mismatch) &&
                   MatchesBoolean(properties.IgnoreObstacle, true, "Ignore NavMesh Obstacle", ref mismatch) &&
                   MatchesBoolean(properties.OverrideTileSize, false, "Override Tile Size", ref mismatch) &&
                   MatchesInteger(properties.TileSize, HumanoidNavMeshSettingsPolicy.TileSize, "Tile Size",
                       ref mismatch) &&
                   MatchesBoolean(properties.OverrideVoxelSize, false, "Override Voxel Size", ref mismatch) &&
                   MatchesFloat(properties.VoxelSize, HumanoidNavMeshSettingsPolicy.CellSize, "Voxel Size",
                       ref mismatch) &&
                   MatchesFloat(properties.MinRegionArea, HumanoidNavMeshSettingsPolicy.MinRegionArea,
                       "Minimum Region Area", ref mismatch) &&
                   MatchesBoolean(properties.BuildHeightMesh, HumanoidNavMeshSettingsPolicy.BuildHeightMesh,
                       "Build Height Mesh", ref mismatch);
        }

        private static void SetNavigationSurfaceProperties(NavigationSurfaceProperties properties)
        {
            properties.AgentTypeId.intValue = HumanoidNavMeshSettingsPolicy.AgentTypeId;
            properties.CollectObjects.intValue = CollectAllObjects;
            properties.LayerMask.intValue = SdkGame.Current.NavigationMask;
            properties.UseGeometry.intValue = PhysicsColliderGeometry;
            properties.DefaultArea.intValue = WalkableArea;
            properties.GenerateLinks.boolValue = true;
            properties.IgnoreAgent.boolValue = true;
            properties.IgnoreObstacle.boolValue = true;
            properties.OverrideTileSize.boolValue = false;
            properties.TileSize.intValue = HumanoidNavMeshSettingsPolicy.TileSize;
            properties.OverrideVoxelSize.boolValue = false;
            properties.VoxelSize.floatValue = HumanoidNavMeshSettingsPolicy.CellSize;
            properties.MinRegionArea.floatValue = HumanoidNavMeshSettingsPolicy.MinRegionArea;
            properties.BuildHeightMesh.boolValue = HumanoidNavMeshSettingsPolicy.BuildHeightMesh;
        }

        private static bool TryGetIntegerProperty(
            SerializedObject serialized,
            string name,
            out SerializedProperty property,
            out string error)
        {
            property = serialized.FindProperty(name);
            if (property is { propertyType: SerializedPropertyType.Integer })
            {
                error = string.Empty;
                return true;
            }

            error = $"NavMeshSurface does not expose the required integer setting '{name}'.";
            return false;
        }

        private static bool TryGetEnumProperty(
            SerializedObject serialized,
            string name,
            out SerializedProperty property,
            out string error)
        {
            property = serialized.FindProperty(name);
            if (property != null &&
                (property.propertyType == SerializedPropertyType.Enum ||
                 property.propertyType == SerializedPropertyType.Integer))
            {
                error = string.Empty;
                return true;
            }

            error = $"NavMeshSurface does not expose the required enum setting '{name}'.";
            return false;
        }

        private static bool TryGetLayerMaskProperty(
            SerializedObject serialized,
            string name,
            out SerializedProperty property,
            out string error)
        {
            property = serialized.FindProperty(name);
            if (property != null &&
                (property.propertyType == SerializedPropertyType.LayerMask ||
                 property.propertyType == SerializedPropertyType.Integer))
            {
                error = string.Empty;
                return true;
            }

            error = $"NavMeshSurface does not expose the required layer-mask setting '{name}'.";
            return false;
        }

        private static bool TryGetBooleanProperty(
            SerializedObject serialized,
            string name,
            out SerializedProperty property,
            out string error)
        {
            property = serialized.FindProperty(name);
            if (property is { propertyType: SerializedPropertyType.Boolean })
            {
                error = string.Empty;
                return true;
            }

            error = $"NavMeshSurface does not expose the required boolean setting '{name}'.";
            return false;
        }

        private static bool TryGetFloatProperty(
            SerializedObject serialized,
            string name,
            out SerializedProperty property,
            out string error)
        {
            property = serialized.FindProperty(name);
            if (property is { propertyType: SerializedPropertyType.Float })
            {
                error = string.Empty;
                return true;
            }

            error = $"NavMeshSurface does not expose the required float setting '{name}'.";
            return false;
        }

        private static bool MatchesInteger(
            SerializedProperty property,
            int expected,
            string label,
            ref string mismatch)
        {
            if (property.intValue == expected)
                return true;

            mismatch = $"{label} is {property.intValue}, expected {expected}.";
            return false;
        }

        private static bool MatchesBoolean(
            SerializedProperty property,
            bool expected,
            string label,
            ref string mismatch)
        {
            if (property.boolValue == expected)
                return true;

            mismatch = $"{label} is {property.boolValue}, expected {expected}.";
            return false;
        }

        private static bool MatchesFloat(
            SerializedProperty property,
            float expected,
            string label,
            ref string mismatch)
        {
            if (Mathf.Approximately(property.floatValue, expected))
                return true;

            mismatch = $"{label} is {property.floatValue}, expected {expected}.";
            return false;
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (!transform)
                return "<missing>";

            var names = new Stack<string>();
            for (var current = transform; current; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        private static Type FindNavMeshSurfaceType()
        {
            if (_navMeshSurfaceTypeResolved)
                return _navMeshSurfaceType;

            _navMeshSurfaceType = Type.GetType($"{NavMeshSurfaceTypeName}, Unity.AI.Navigation") ??
                                  AppDomain.CurrentDomain.GetAssemblies()
                                      .Where(assembly => assembly != null && !assembly.IsDynamic)
                                      .Select(assembly => assembly.GetType(NavMeshSurfaceTypeName, false))
                                      .FirstOrDefault(type => type != null);
            _navMeshSurfaceTypeResolved = true;
            return _navMeshSurfaceType;
        }

        private static bool IsDefaultMainCamera(Camera camera)
        {
            if (!camera)
                return false;

            return camera.CompareTag(MainCameraTag) ||
                   string.Equals(camera.name, DefaultMainCameraName, StringComparison.Ordinal);
        }

        private static bool IsUsableDirectionalLight(Light light)
        {
            return light && light.enabled && light.gameObject.activeInHierarchy;
        }

        private static BakeState EvaluateNavigationState(Scene scene, Component navigationSurface)
        {
            if (!scene.IsValid())
                return BakeState.Missing;

            if (!HumanoidNavMeshSettingsPolicy.IsCurrent(out _) ||
                !TryValidateHumanoidNavigationSurface(navigationSurface, out _))
                return BakeState.Missing;

            return HasBakedNavMesh(navigationSurface) ? BakeState.Ready : BakeState.Missing;
        }

        private static BakeState EvaluateLightingState()
        {
            if (Lightmapping.isRunning)
                return BakeState.Pending;

            return HasBakedLighting() ? BakeState.Ready : BakeState.Missing;
        }

        private static BakeState EvaluateOcclusionState()
        {
            return HasBakedOcclusion() ? BakeState.Ready : BakeState.Missing;
        }

        private static MethodInfo GetOcclusionComputeMethod()
        {
            if (_occlusionComputeMethodResolved)
                return _occlusionComputeMethod;

            _occlusionComputeMethod = typeof(StaticOcclusionCulling).GetMethod("Compute");
            _occlusionComputeMethodResolved = true;
            return _occlusionComputeMethod;
        }

        private static PropertyInfo GetNavMeshDataProperty(Type surfaceType)
        {
            if (surfaceType == null)
                return null;

            if (NavMeshDataPropertyCache.TryGetValue(surfaceType, out var property))
                return property;

            property = surfaceType.GetProperty("navMeshData");
            NavMeshDataPropertyCache[surfaceType] = property;
            return property;
        }

        private static Object GetNavigationData(Component surface)
        {
            if (!surface)
                return null;

            var property = GetNavMeshDataProperty(surface.GetType());
            if (property == null)
                return null;

            try
            {
                return property.GetValue(surface, null) as Object;
            }
            catch
            {
                return null;
            }
        }

        private static bool SetNavigationData(Component surface, Object navMeshData)
        {
            if (!surface || !navMeshData)
                return false;

            var property = GetNavMeshDataProperty(surface.GetType());
            if (property == null || !property.CanWrite)
                return false;

            try
            {
                navMeshData.hideFlags = HideFlags.None;
                property.SetValue(surface, navMeshData, null);
                EditorUtility.SetDirty(surface);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Object GetOcclusionCullingSettings()
        {
            if (!_occlusionCullingSettingsPropertyResolved)
            {
                _occlusionCullingSettingsProperty = typeof(StaticOcclusionCulling).GetProperty(
                    "occlusionCullingSettings",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                _occlusionCullingSettingsPropertyResolved = true;
            }

            try
            {
                return _occlusionCullingSettingsProperty?.GetValue(null, null) as Object;
            }
            catch
            {
                return null;
            }
        }

        private static MethodInfo GetBuildNavMeshMethod(Type surfaceType)
        {
            if (surfaceType == null)
                return null;

            if (BuildNavMeshMethodCache.TryGetValue(surfaceType, out var method))
                return method;

            method = surfaceType.GetMethod("BuildNavMesh");
            BuildNavMeshMethodCache[surfaceType] = method;
            return method;
        }

        public readonly struct MapSceneContext
        {
            public MapSceneContext(Scene scene, GameObject navigationRoot, Component navigationSurface,
                Light directionalLight, int directionalLightCount, int enabledDirectionalLightCount, Camera mainCamera,
                int mainCameraCount)
            {
                Scene = scene;
                NavigationRoot = navigationRoot;
                NavigationSurface = navigationSurface;
                DirectionalLight = directionalLight;
                DirectionalLightCount = directionalLightCount;
                EnabledDirectionalLightCount = enabledDirectionalLightCount;
                MainCamera = mainCamera;
                MainCameraCount = mainCameraCount;
            }

            public Scene Scene { get; }
            public GameObject NavigationRoot { get; }
            public Component NavigationSurface { get; }
            public Light DirectionalLight { get; }
            public int DirectionalLightCount { get; }
            public int EnabledDirectionalLightCount { get; }
            public Camera MainCamera { get; }
            public int MainCameraCount { get; }
            public bool HasNavigationSetup => NavigationSurface != null;
            public bool HasConfiguredNavigation =>
                NavigationSurface && TryValidateHumanoidNavigationSurface(NavigationSurface, out _);
            public bool HasDirectionalLight => EnabledDirectionalLightCount > 0;
            public bool HasMainCamera => MainCameraCount > 0;
            public bool IsSceneContractSatisfied => HasConfiguredNavigation && HasDirectionalLight && !HasMainCamera;
        }

        private sealed class NavigationSurfaceProperties
        {
            public SerializedProperty AgentTypeId;
            public SerializedProperty CollectObjects;
            public SerializedProperty LayerMask;
            public SerializedProperty UseGeometry;
            public SerializedProperty DefaultArea;
            public SerializedProperty GenerateLinks;
            public SerializedProperty IgnoreAgent;
            public SerializedProperty IgnoreObstacle;
            public SerializedProperty OverrideTileSize;
            public SerializedProperty TileSize;
            public SerializedProperty OverrideVoxelSize;
            public SerializedProperty VoxelSize;
            public SerializedProperty MinRegionArea;
            public SerializedProperty BuildHeightMesh;
        }
    }
}
#endif
