// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using Vertex.Unity;
using Vertex.VSig;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Maps;
using Vellocet.Framework.Runtime.Spatial;
using Vellocet.Sdk.Tooling;
using Vellocet.Sdk.VSig;
using Vellocet.Sdk.VSig.Runtime;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>
    ///     Build-time allowlist for addon scenes. Bundles may contain data and supported entity markers,
    ///     but cannot introduce or reference arbitrary game behaviours or ScriptableObjects.
    /// </summary>
    internal static class AddonScenePolicy
    {
        private const int MaximumGameObjects = 50000;
        private const int MaximumComponents = 200000;
        private const int MaximumComponentsPerObject = 64;
        private const int MaximumHierarchyDepth = 64;
        private const int MaximumDependencies = 8192;
        private const int MaximumDependencyObjects = 32768;
        private const int MaximumTextureDimension = 4096;
        private const long MaximumTexturePixels = 64L * 1024L * 1024L;
        private const int MaximumMeshVertices = 1000000;
        private const long MaximumMeshVerticesTotal = 10L * 1024L * 1024L;
        private const long MaximumAudioSamples = 2L * 60L * 60L * 48000L * 2L;
        private const int MaximumTextAssetBytes = 8 * 1024 * 1024;
        private const int MaximumSerializedArrayElements = 65536;
        private const int MaximumOcclusionDataBytes = 8 * 1024 * 1024;
        private const int MaximumSerializedStringCharacters = 8192;
        private const int MaximumAnimationCurveKeys = 4096;

        private static readonly Dictionary<string, string> AllowedNamedComponents = new(StringComparer.Ordinal)
        {
            ["UnityEngine.MultiplayerRolesData"] = "UnityEngine.MultiplayerModule",
            ["Unity.AI.Navigation.NavMeshSurface"] = "Unity.AI.Navigation",
            ["Unity.AI.Navigation.NavMeshModifier"] = "Unity.AI.Navigation",
            ["Unity.AI.Navigation.NavMeshModifierVolume"] = "Unity.AI.Navigation",
            ["Unity.AI.Navigation.NavMeshLink"] = "Unity.AI.Navigation",
            ["UnityEngine.Rendering.Volume"] = "Unity.RenderPipelines.Core.Runtime",
            ["UnityEngine.Rendering.ProbeVolume"] = "Unity.RenderPipelines.Core.Runtime",
            ["UnityEngine.Rendering.ProbeVolumePerSceneData"] = "Unity.RenderPipelines.Core.Runtime",
            ["UnityEngine.Rendering.Universal.DecalProjector"] = "Unity.RenderPipelines.Universal.Runtime",
            ["UnityEngine.Rendering.Universal.UniversalAdditionalCameraData"] =
                "Unity.RenderPipelines.Universal.Runtime",
            ["UnityEngine.Rendering.Universal.UniversalAdditionalLightData"] =
                "Unity.RenderPipelines.Universal.Runtime",
            ["UnityEngine.Splines.SplineContainer"] = "Unity.Splines",
            ["TMPro.TextMeshPro"] = "Unity.TextMeshPro",
            ["UnityEngine.VFX.VisualEffect"] = "UnityEngine.VFXModule"
        };


        public static bool TryValidate(Scene scene,
            IReadOnlyList<MapCompiler.RuntimeDependency> runtimeDependencies, out string error, Object[] playerDependencies = null)
        {
            error = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "Compiled addon scene is not loaded for its security audit.";
                return false;
            }

            var budget = new AuditBudget { OcclusionDataPath = string.IsNullOrEmpty(scene.path) ? null :
                Path.Combine(Path.GetDirectoryName(scene.path), Path.GetFileNameWithoutExtension(scene.path),
                    "OcclusionCullingData.asset").Replace('\\', '/') };
            foreach (var root in scene.GetRootGameObjects())
                if (!TryAuditObject(root.transform, 1, budget, true, out error))
                    return false;

            if (budget.Markers > VSigSecurityLimits.MaximumSceneMarkers || budget.VSigRuntimes > 1)
            {
                error = "Addon scene exceeds its marker or VSig-runtime limit.";
                return false;
            }
            if (playerDependencies != null)
            {
                if (playerDependencies.Length > MaximumDependencyObjects) { error = "Addon exceeds its dependency-object limit."; return false; }
                foreach (var asset in playerDependencies)
                    if (!TryAuditAsset(asset, AssetDatabase.GetAssetPath(asset), budget, out error)) return false;
                return true;
            }
            return TryAuditDependencies(scene.path, runtimeDependencies, budget, out error);
        }

        private static bool TryAuditObject(Transform transform, int depth, AuditBudget budget,
            bool countSceneMarkers, out string error)
        {
            error = null;
            if (!transform || depth > MaximumHierarchyDepth || ++budget.GameObjects > MaximumGameObjects)
            {
                error = "Addon hierarchy is invalid, oversized, or too deep.";
                return false;
            }
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
            {
                error = $"GameObject '{BuildPath(transform)}' contains a missing script.";
                return false;
            }

            var components = transform.gameObject.GetComponents<Component>();
            if (components.Length > MaximumComponentsPerObject)
            {
                error = $"GameObject '{BuildPath(transform)}' has too many components.";
                return false;
            }

            foreach (var component in components)
            {
                var objectPath = BuildPath(transform);
                if (!component || !IsAllowedComponent(component.GetType()))
                {
                    error = $"GameObject '{objectPath}' contains forbidden component " +
                            $"'{(component ? component.GetType().AssemblyQualifiedName : "<missing>")}'.";
                    return false;
                }
                if (++budget.Components > MaximumComponents ||
                    !TryAuditSerializedData(component,
                        $"{objectPath} ({component.GetType().FullName})", out error,
                        component is VSigSceneRuntime))
                {
                    error ??= "Addon scene exceeds its component limit.";
                    return false;
                }

                if (countSceneMarkers && component is EntityMarker &&
                    ++budget.Markers > VSigSecurityLimits.MaximumSceneMarkers)
                {
                    error = "Addon scene exceeds its entity-marker limit.";
                    return false;
                }
                if (component is VSigSceneRuntime runtime)
                {
                    if (countSceneMarkers && ++budget.VSigRuntimes > 1)
                    {
                        error = "Addon scene contains more than one VSig runtime.";
                        return false;
                    }
                    if (runtime.HasProgram && !TryAuditVSig(runtime, BuildPath(transform), out error))
                        return false;
                }

                if (component is MeshFilter filter && filter.sharedMesh &&
                    !TryCountMesh(filter.sharedMesh, budget, out error) ||
                    component is SkinnedMeshRenderer skinned && skinned.sharedMesh &&
                    !TryCountMesh(skinned.sharedMesh, budget, out error) ||
                    component is MeshCollider collider && collider.sharedMesh &&
                    !TryCountMesh(collider.sharedMesh, budget, out error))
                    return false;
            }

            for (var i = 0; i < transform.childCount; i++)
                if (!TryAuditObject(transform.GetChild(i), depth + 1, budget, countSceneMarkers, out error))
                    return false;
            return true;
        }

        internal static bool IsAllowedComponent(Type type)
        {
            if (type == null)
                return false;
            if (typeof(Transform).IsAssignableFrom(type) || IsAllowedMarkerType(type) ||
                type == typeof(MapBinding) || type == typeof(VSigSceneRuntime) ||
                type == typeof(MapRuntimeDependencies) || type == typeof(SpatialOctreeBakeSettingsSource) ||
                type == typeof(ToolVisual) || type == typeof(Vertex.Unity.VertexObjectIdentity) ||
                type == typeof(Vertex.Unity.VertexMapResources))
                return true;

            var fullName = type.FullName ?? string.Empty;
            var assembly = type.Assembly.GetName().Name ?? string.Empty;
            if (!string.IsNullOrEmpty(SdkGame.Current.AudioRuntimeCapability) && (type == typeof(AudioSource) || type == typeof(AudioListener) || type == typeof(AudioReverbZone) || type == typeof(AudioLowPassFilter) || type == typeof(AudioHighPassFilter) || type == typeof(AudioDistortionFilter) || type == typeof(AudioReverbFilter) || type == typeof(AudioChorusFilter) || type == typeof(AudioEchoFilter))) return false;
            if (SdkGame.Current.AllowsComponent(fullName, assembly)) return true;
            if (AllowedNamedComponents.TryGetValue(fullName, out var expectedAssembly))
                return string.Equals(assembly, expectedAssembly, StringComparison.Ordinal);
            if (!string.Equals(assembly, "UnityEngine", StringComparison.Ordinal) &&
                !assembly.StartsWith("UnityEngine.", StringComparison.Ordinal))
                return false;

            return typeof(Renderer).IsAssignableFrom(type) || type == typeof(MeshFilter) ||
                   typeof(Collider).IsAssignableFrom(type) || typeof(Collider2D).IsAssignableFrom(type) ||
                   type == typeof(Rigidbody) || type == typeof(Rigidbody2D) ||
                   typeof(Joint).IsAssignableFrom(type) || typeof(Joint2D).IsAssignableFrom(type) ||
                   type == typeof(ConstantForce) || type == typeof(ConstantForce2D) ||
                   type == typeof(Light) || type == typeof(ReflectionProbe) ||
                   type == typeof(LightProbeGroup) || type == typeof(LODGroup) ||
                   type == typeof(OcclusionArea) || type == typeof(OcclusionPortal) ||
                   type == typeof(Terrain) || type == typeof(ParticleSystem) ||
                   type == typeof(AudioSource) || type == typeof(AudioReverbZone) ||
                   type == typeof(WindZone) || type == typeof(Camera) ||
                   type == typeof(FlareLayer) || type == typeof(Skybox) ||
                   type == typeof(Projector) || type == typeof(NavMeshObstacle) ||
                   type == typeof(TextMesh);
        }

        private static bool IsAllowedMarkerType(Type type)
        {
            if (type.IsAbstract || !typeof(EntityMarker).IsAssignableFrom(type) ||
                !SdkGame.Current.MarkerAssemblies.Contains(type.Assembly.GetName().Name ?? string.Empty))
                return false;

            return Attribute.IsDefined(type, typeof(EntityClassAttribute), false) &&
                   Attribute.IsDefined(type, typeof(EntityAuthoringAttribute), false);
        }

        private static bool TryAuditSerializedData(Object target, string context, out string error,
            bool allowManagedReferences = false, bool allowOcclusionData = false)
        {
            error = null;
            try
            {
                using var serialized = new SerializedObject(target);
                var property = serialized.GetIterator();
                var enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    if (allowOcclusionData && property.propertyPath == "m_PVSData" &&
                        property.isArray && property.arrayElementType == "byte")
                    {
                        if (property.arraySize > MaximumOcclusionDataBytes)
                        {
                            error = $"Occlusion data on '{context}' exceeds its byte limit.";
                            return false;
                        }
                        enterChildren = false;
                        continue;
                    }
                    // The typed graph validator checks these arrays together, including indices and memory budgets.
                    if (target is SpatialOctreeAsset && property.isArray && property.propertyPath is
                        "nodes" or "acousticData" or "neighborStarts" or "neighborIndices" or "neighborApertures" or
                        "traversableNodeIndices" or "routingRegions" or "routingNeighborStarts" or "routingNeighborIndices")
                    {
                        enterChildren = false;
                        continue;
                    }
                    if (target is Vertex.Unity.VertexMapResources resources && property.propertyPath == "sources")
                    {
                        if (property.arraySize > Vertex.Contracts.BuildProtocol.MaximumBytes)
                            throw new InvalidDataException("Map resources exceed their compiled byte limit.");
                        _ = resources.Read();
                        enterChildren = false;
                        continue;
                    }
                    if (target is VSigSceneRuntime && property.propertyPath == "vertexProgram")
                    {
                        if (property.arraySize > Vertex.VSig.VSigCompiledProgram.MaximumCompiledBytes)
                        {
                            error = $"VSig program on '{context}' exceeds its compiled byte limit.";
                            return false;
                        }
                        // Audited by GetVertexProgram below; do not walk each serialized byte as a property.
                        enterChildren = false;
                        continue;
                    }
                    var hasPersistentCalls = property.isArray && property.arraySize > 0 &&
                                             property.propertyPath.EndsWith(
                                                 ".m_PersistentCalls.m_Calls",
                                                 StringComparison.Ordinal);
                    if ((!allowManagedReferences &&
                         property.propertyType == SerializedPropertyType.ManagedReference) ||
                        property.propertyType == SerializedPropertyType.ExposedReference || hasPersistentCalls)
                    {
                        error = $"Serialized property '{property.propertyPath}' on '{context}' contains " +
                                "an executable or dynamic reference.";
                        return false;
                    }
                    if (property.isArray && property.propertyType != SerializedPropertyType.String &&
                        property.arraySize > MaximumSerializedArrayElements)
                    {
                        error = $"Serialized array on '{context}' exceeds the element limit.";
                        return false;
                    }
                    if (property.propertyType == SerializedPropertyType.String &&
                        (property.stringValue?.Length ?? 0) > MaximumSerializedStringCharacters)
                    {
                        error = $"Serialized string on '{context}' exceeds the length limit.";
                        return false;
                    }
                    if (property.propertyType == SerializedPropertyType.AnimationCurve &&
                        property.animationCurveValue is { length: > MaximumAnimationCurveKeys })
                    {
                        error = $"Animation curve on '{context}' exceeds the key limit.";
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = $"Serialized data on '{context}' could not be audited: {ex.Message}";
                return false;
            }
        }

        private static bool TryAuditVSig(VSigSceneRuntime runtime, string context, out string error)
        {
            try
            {
                if (Vertex.VSig.VSigSecurityLimits.TryValidateProgram(runtime.GetVertexProgram().ToProgram(), out error))
                    return true;
                error = $"VSig program on '{context}' failed its security audit: {error}";
                return false;
            }
            catch (Exception ex)
            {
                error = $"VSig program on '{context}' could not be audited: {ex.Message}";
                return false;
            }
        }

        private static bool TryAuditDependencies(string scenePath,
            IReadOnlyList<MapCompiler.RuntimeDependency> runtimeDependencies,
            AuditBudget budget, out string error)
        {
            error = null;
            var paths = AssetDatabase.GetDependencies(scenePath, true)
                .Concat((runtimeDependencies ?? Array.Empty<MapCompiler.RuntimeDependency>())
                    .Select(value => AssetDatabase.GUIDToAssetPath(value.Guid)))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                // Scene objects were audited above. Unity does not load scene assets through LoadAllAssetsAtPath.
                .Where(path => !string.Equals(path, scenePath, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (paths.Length > MaximumDependencies)
            {
                error = "Addon scene exceeds its dependency limit.";
                return false;
            }

            foreach (var path in paths)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null || assets.Length == 0)
                {
                    error = $"Addon dependency '{path}' could not be loaded.";
                    return false;
                }
                foreach (var asset in assets)
                {
                    // URP adds editor-only version metadata to materials; the player dependency audit still checks the build.
                    var assetType = asset is MonoScript script ? script.GetClass() : asset ? asset.GetType() : null;
                    if (assetType?.FullName == "UnityEditor.Rendering.Universal.AssetVersion" &&
                        assetType.Assembly.GetName().Name == "Unity.RenderPipelines.Universal.Editor") continue;
                    // Import diagnostics are editor metadata attached to otherwise valid assets.
                    // The player dependency audit below still rejects them if they reach a build.
                    if (assetType?.FullName == "UnityEditor.AssetImporters.ImportLog" &&
                        assetType.Assembly == typeof(AssetImporter).Assembly) continue;
                    // Importer scripts describe source assets; player dependencies are audited separately.
                    if (asset is MonoScript && assetType != null &&
                        typeof(AssetImporter).IsAssignableFrom(assetType)) continue;
                    if (!asset || ++budget.DependencyObjects > MaximumDependencyObjects)
                    {
                        error = "Addon exceeds its dependency-object limit.";
                        return false;
                    }
                    if (!TryAuditAsset(asset, path, budget, out error))
                        return false;
                }
            }
            return true;
        }

        private static bool TryAuditAsset(Object asset, string path, AuditBudget budget, out string error)
        {
            error = null;
            switch (asset)
            {
                case SceneAsset:
                case DefaultAsset:
                case PhysicsMaterial:
                case PhysicsMaterial2D:
                case TerrainData:
                case NavMeshData:
                case LightingDataAsset:
                case LightingSettings:
                case LightmapParameters:
                case LightProbes:
                case Font:
                    return true;
                case MonoScript script when IsAllowedSerializedType(script.GetClass()):
                    return true;
                case MonoScript:
                    error = $"Addon depends on forbidden script '{path}'.";
                    return false;
                case TextAsset text when text.bytes.LongLength <= MaximumTextAssetBytes:
                    return true;
                case TextAsset:
                    error = $"Text dependency '{path}' exceeds the byte limit.";
                    return false;
                case Material material:
                    var shaderPath = material.shader ? AssetDatabase.GetAssetPath(material.shader) : string.Empty;
                    if (!material.shader || !IsAllowedShaderPath(shaderPath))
                    {
                        error = $"Material '{path}' uses a disallowed or missing shader '{(material.shader ? material.shader.name : "<missing>")}' at '{shaderPath}'.";
                        return false;
                    }
                    return TryAuditSerializedData(material, path, out error);
                case Mesh mesh:
                    return TryCountMesh(mesh, budget, out error);
                case Texture texture:
                    return TryCountTexture(texture, path, budget, out error);
                case Sprite sprite:
                    return !sprite.texture || TryCountTexture(sprite.texture, path, budget, out error);
                case AudioClip clip:
                    if (!string.IsNullOrEmpty(SdkGame.Current.AudioRuntimeCapability))
                    { error = "Unity AudioClip is not valid runtime content. Convert it to a Stereoform event and rebuild."; return false; }
                    if (!budget.AudioClips.Add(clip))
                        return true;
                    var samples = (long)clip.samples * Math.Max(1, clip.channels);
                    if (samples <= 0 || budget.AudioSamples > MaximumAudioSamples - samples)
                    {
                        error = $"Audio dependency '{path}' exceeds the sample limit.";
                        return false;
                    }
                    budget.AudioSamples += samples;
                    return true;
                case Shader when IsAllowedShaderPath(path):
                    return true;
                case Shader:
                    error = $"Addon uses a disallowed custom shader '{path}'.";
                    return false;
                case GameObject prefab:
                    return TryAuditObject(prefab.transform, 1, budget, false, out error);
                case Component component when IsAllowedComponent(component.GetType()):
                    return TryAuditSerializedData(component, path, out error);
                case Object when asset.GetType().FullName == "UnityEngine.VFX.VisualEffectAsset" &&
                                 asset.GetType().Assembly.GetName().Name == "UnityEngine.VFXModule":
                    return TryAuditSerializedData(asset, path, out error);
                case SpatialOctreeAsset spatial:
                    if (!SpatialOctreePerformanceBudget.TryValidate(spatial, SdkGame.Current.SpatialBakeSettings, out error) ||
                        !spatial.TryValidateData(out error)) return false;
                    return TryAuditSerializedData(spatial, path, out error);
                case ScriptableObject scriptable when IsAllowedSerializedType(scriptable.GetType()):
                    return TryAuditSerializedData(scriptable, path, out error);
                // Unity exposes baked occlusion data as Object, without a public managed asset type.
                case Object when asset.GetType() == typeof(Object) &&
                                 string.Equals(path, budget.OcclusionDataPath, StringComparison.Ordinal):
                    return TryAuditSerializedData(asset, path, out error, allowOcclusionData: true);
                default:
                    error = $"Addon depends on unsupported asset type '{asset.GetType().FullName}' at '{path}'.";
                    return false;
            }
        }

        private static bool IsAllowedSerializedType(Type type)
        {
            return (!string.IsNullOrEmpty(SdkGame.Current.AudioRuntimeCapability) && (type == typeof(Vellocet.Stereoform.AudioEventReference) || type == typeof(Vellocet.Stereoform.AudioBusReference) || type == typeof(Vellocet.Stereoform.AudioEnvironmentReference))) || type == typeof(SpatialOctreeAsset) || type == typeof(Vellocet.Sdk.Audio.AddonAudioCue) || IsAllowedComponent(type) ||
                   IsAllowedVolumeType(type);
        }

        private static bool IsAllowedVolumeType(Type type)
        {
            if (type == null)
                return false;
            var assembly = type.Assembly.GetName().Name ?? string.Empty;
            if (type.FullName is "UnityEngine.Rendering.VolumeProfile" or "UnityEngine.Rendering.ProbeVolumeBakingSet")
                return string.Equals(assembly, "Unity.RenderPipelines.Core.Runtime", StringComparison.Ordinal);

            for (var current = type; current != null; current = current.BaseType)
                if (string.Equals(current.FullName, "UnityEngine.Rendering.VolumeComponent",
                        StringComparison.Ordinal))
                    return string.Equals(assembly, "Unity.RenderPipelines.Core.Runtime", StringComparison.Ordinal) ||
                           string.Equals(assembly, "Unity.RenderPipelines.Universal.Runtime", StringComparison.Ordinal);
            return false;
        }

        private static bool TryCountTexture(Texture texture, string path, AuditBudget budget, out string error)
        {
            error = null;
            if (!texture || !budget.Textures.Add(texture))
                return true;
            var slices = texture switch
            {
                Texture2D => 1L,
                Texture2DArray array => array.depth,
                Texture3D volume => volume.depth,
                Cubemap => 6L,
                CubemapArray array => (long)array.cubemapCount * 6L,
                _ => 0L
            };
            var facePixels = (long)texture.width * texture.height;
            if (texture.width <= 0 || texture.height <= 0 ||
                texture.width > MaximumTextureDimension || texture.height > MaximumTextureDimension ||
                slices <= 0 || facePixels <= 0 || slices > MaximumTexturePixels / facePixels ||
                budget.TexturePixels > MaximumTexturePixels - facePixels * slices)
            {
                error = $"Texture dependency '{path}' exceeds addon texture limits.";
                return false;
            }
            budget.TexturePixels += facePixels * slices;
            return true;
        }

        private static bool TryCountMesh(Mesh mesh, AuditBudget budget, out string error)
        {
            error = null;
            if (!mesh || !budget.Meshes.Add(mesh))
                return true;
            if (mesh.vertexCount < 0 || mesh.vertexCount > MaximumMeshVertices ||
                budget.MeshVertices > MaximumMeshVerticesTotal - mesh.vertexCount)
            {
                error = $"Mesh '{mesh.name}' exceeds addon mesh limits.";
                return false;
            }
            budget.MeshVertices += mesh.vertexCount;
            return true;
        }

        private static bool IsAllowedShaderPath(string path)
        {
            return path.StartsWith("Packages/com.unity.", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Packages/com.vellocet.sdk/", StringComparison.OrdinalIgnoreCase) ||
                   path.Equals("Packages/com.vellocet.vertex/Runtime/VertexTerrainBlend.shader", StringComparison.Ordinal) ||
                   path.StartsWith("Library/unity default resources", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Resources/unity_builtin_extra", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPath(Transform transform)
        {
            var names = new Stack<string>();
            for (var current = transform; current; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        private sealed class AuditBudget
        {
            public readonly HashSet<AudioClip> AudioClips = new();
            public readonly HashSet<Mesh> Meshes = new();
            public readonly HashSet<Texture> Textures = new();
            public string OcclusionDataPath;
            public int Components;
            public int DependencyObjects;
            public int GameObjects;
            public int Markers;
            public int VSigRuntimes;
            public long AudioSamples;
            public long MeshVertices;
            public long TexturePixels;
        }
    }
}
#endif
