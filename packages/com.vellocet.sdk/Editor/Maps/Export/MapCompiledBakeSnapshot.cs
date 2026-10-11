// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    internal sealed class MapCompiledBakeSnapshot
    {
        private MapCompiledBakeSnapshot()
        {
        }

        internal int LightmapCount => Lightmaps?.Length ?? 0;
        // Unity's default lighting is a built-in placeholder, not a bake artifact that can be copied.
        internal bool HasLightingData => LightingDataPath != null &&
            (LightingDataPath.StartsWith("Assets/", StringComparison.Ordinal) ||
             LightingDataPath.StartsWith("Packages/", StringComparison.Ordinal));

        private string LightingSettingsPath { get; set; }
        private string LightingDataPath { get; set; }
        private string LightProbesPath { get; set; }
        private string ProbeVolumeBakingSetPath { get; set; }
        private LightmapsMode LightmapsMode { get; set; }
        private ColorSpace BakedColorSpace { get; set; }
        private LightmapSlot[] Lightmaps { get; set; } = Array.Empty<LightmapSlot>();
        private string OcclusionDataPath { get; set; }
        private string NavigationDataPath { get; set; }
        private Dictionary<string, LightmapBakeRecord> Renderers { get; set; } = new();
        private Dictionary<string, LightmapBakeRecord> Terrains { get; set; } = new();
        private Dictionary<string, ReflectionProbeBakeRecord> ReflectionProbes { get; set; } = new();

        internal static MapCompiledBakeSnapshot Capture(Scene compiledScene,
            IReadOnlyDictionary<string, string> assetMap)
        {
            return new MapCompiledBakeSnapshot
            {
                LightingSettingsPath = MapAssetPath(
                    AssetDatabase.GetAssetPath(Lightmapping.GetLightingSettingsForScene(compiledScene)), assetMap),
                LightingDataPath = MapAssetPath(
                    AssetDatabase.GetAssetPath(Lightmapping.GetLightingDataAssetForScene(compiledScene)), assetMap),
                LightProbesPath = MapAssetPath(AssetDatabase.GetAssetPath(LightmapSettings.lightProbes), assetMap),
                ProbeVolumeBakingSetPath = MapAssetPath(
                    AssetDatabase.GetAssetPath(MapProbeVolumeUtility.GetBakedData(compiledScene)), assetMap),
                LightmapsMode = LightmapSettings.lightmapsMode,
#pragma warning disable CS0618
                BakedColorSpace = LightmapSettings.bakedColorSpace,
#pragma warning restore CS0618
                Lightmaps = CaptureLightmaps(assetMap),
                OcclusionDataPath = MapAssetPath(AssetDatabase.GetAssetPath(MapSceneUtility.GetOcclusionData()),
                    assetMap),
                NavigationDataPath = MapAssetPath(AssetDatabase.GetAssetPath(
                    MapSceneUtility.GetNavigationData(compiledScene)), assetMap),
                Renderers = CaptureRenderers(compiledScene),
                Terrains = CaptureTerrains(compiledScene),
                ReflectionProbes = CaptureReflectionProbes(compiledScene, assetMap)
            };
        }

        internal ApplySummary Apply(Scene authoringScene)
        {
            return Apply(authoringScene, ApplyOptions.All);
        }

        internal ApplySummary Apply(Scene authoringScene, ApplyOptions options)
        {
            var summary = new ApplySummary();

            if (options.ApplyLighting)
            {
                var lightingSettings = LoadAsset<LightingSettings>(LightingSettingsPath);
                if (lightingSettings)
                    Lightmapping.SetLightingSettingsForScene(authoringScene, lightingSettings);

                LightmapSettings.lightmapsMode = LightmapsMode;
#pragma warning disable CS0618
                LightmapSettings.bakedColorSpace = BakedColorSpace;
#pragma warning restore CS0618
                var lightmaps = BuildLightmaps(Lightmaps);
                LightmapSettings.lightmaps = lightmaps;
                summary.LightmapSlotsApplied = lightmaps.Length;

                var probes = LoadAsset<LightProbes>(LightProbesPath);
                if (probes)
                    LightmapSettings.lightProbes = probes;

                var lightingData = LoadAsset<LightingDataAsset>(LightingDataPath);
                if (lightingData)
                {
                    Lightmapping.SetLightingDataAssetForScene(authoringScene, lightingData);
                    summary.LightingDataApplied = true;
                }

                summary.RenderersApplied = ApplyRenderers(authoringScene, Renderers);
                summary.TerrainsApplied = ApplyTerrains(authoringScene, Terrains);
                summary.ReflectionProbesApplied = ApplyReflectionProbes(authoringScene, ReflectionProbes);
                if (!string.IsNullOrWhiteSpace(ProbeVolumeBakingSetPath))
                    MapProbeVolumeUtility.ApplyBakedData(authoringScene, ProbeVolumeBakingSetPath);
            }

            if (options.ApplyOcclusion)
            {
                var occlusionData = LoadAsset<Object>(OcclusionDataPath);
                summary.OcclusionDataApplied = occlusionData && MapSceneUtility.SetOcclusionData(occlusionData);
            }

            if (options.ApplyNavigation)
            {
                var sceneContext = MapSceneUtility.EnsureSceneContract(authoringScene, true, out _);
                var navMeshData = LoadAsset<Object>(NavigationDataPath);
                summary.NavigationApplied = sceneContext.NavigationSurface &&
                                            navMeshData &&
                                            MapSceneUtility.SetNavigationData(authoringScene, navMeshData);
            }

            EditorSceneManager.MarkSceneDirty(authoringScene);
            return summary;
        }

        private static LightmapSlot[] CaptureLightmaps(IReadOnlyDictionary<string, string> assetMap)
        {
            var lightmaps = LightmapSettings.lightmaps;
            if (lightmaps == null || lightmaps.Length == 0)
                return Array.Empty<LightmapSlot>();

            return lightmaps
                .Select(lightmap => new LightmapSlot(
                    MapAssetPath(AssetDatabase.GetAssetPath(lightmap?.lightmapColor), assetMap),
                    MapAssetPath(AssetDatabase.GetAssetPath(lightmap?.lightmapDir), assetMap),
                    MapAssetPath(AssetDatabase.GetAssetPath(lightmap?.shadowMask), assetMap)))
                .ToArray();
        }

        private static LightmapData[] BuildLightmaps(IReadOnlyList<LightmapSlot> slots)
        {
            if (slots == null || slots.Count == 0)
                return Array.Empty<LightmapData>();

            var lightmaps = new LightmapData[slots.Count];
            for (var i = 0; i < slots.Count; i++)
                lightmaps[i] = new LightmapData
                {
                    lightmapColor = LoadAsset<Texture2D>(slots[i].ColorPath),
                    lightmapDir = LoadAsset<Texture2D>(slots[i].DirectionPath),
                    shadowMask = LoadAsset<Texture2D>(slots[i].ShadowMaskPath)
                };

            return lightmaps;
        }

        private static Dictionary<string, LightmapBakeRecord> CaptureRenderers(Scene scene)
        {
            var records = new Dictionary<string, LightmapBakeRecord>();
            foreach (var renderer in CollectSceneComponents<Renderer>(scene))
                records[GetComponentKey(renderer)] = new LightmapBakeRecord(
                    renderer.lightmapIndex,
                    renderer.lightmapScaleOffset,
                    renderer.realtimeLightmapIndex,
                    renderer.realtimeLightmapScaleOffset);

            return records;
        }

        private static int ApplyRenderers(Scene scene, IReadOnlyDictionary<string, LightmapBakeRecord> records)
        {
            if (records == null || records.Count == 0)
                return 0;

            var applied = 0;
            foreach (var renderer in CollectSceneComponents<Renderer>(scene))
            {
                if (!records.TryGetValue(GetComponentKey(renderer), out var record))
                    continue;

                renderer.lightmapIndex = record.LightmapIndex;
                renderer.lightmapScaleOffset = record.LightmapScaleOffset;
                renderer.realtimeLightmapIndex = record.RealtimeLightmapIndex;
                renderer.realtimeLightmapScaleOffset = record.RealtimeLightmapScaleOffset;
                EditorUtility.SetDirty(renderer);
                applied++;
            }

            return applied;
        }

        private static Dictionary<string, LightmapBakeRecord> CaptureTerrains(Scene scene)
        {
            var records = new Dictionary<string, LightmapBakeRecord>();
            foreach (var terrain in CollectSceneComponents<Terrain>(scene))
                records[GetComponentKey(terrain)] = new LightmapBakeRecord(
                    terrain.lightmapIndex,
                    terrain.lightmapScaleOffset,
                    terrain.realtimeLightmapIndex,
                    terrain.realtimeLightmapScaleOffset);

            return records;
        }

        private static int ApplyTerrains(Scene scene, IReadOnlyDictionary<string, LightmapBakeRecord> records)
        {
            if (records == null || records.Count == 0)
                return 0;

            var applied = 0;
            foreach (var terrain in CollectSceneComponents<Terrain>(scene))
            {
                if (!records.TryGetValue(GetComponentKey(terrain), out var record))
                    continue;

                terrain.lightmapIndex = record.LightmapIndex;
                terrain.lightmapScaleOffset = record.LightmapScaleOffset;
                terrain.realtimeLightmapIndex = record.RealtimeLightmapIndex;
                terrain.realtimeLightmapScaleOffset = record.RealtimeLightmapScaleOffset;
                EditorUtility.SetDirty(terrain);
                applied++;
            }

            return applied;
        }

        private static Dictionary<string, ReflectionProbeBakeRecord> CaptureReflectionProbes(Scene scene,
            IReadOnlyDictionary<string, string> assetMap)
        {
            var records = new Dictionary<string, ReflectionProbeBakeRecord>();
            foreach (var probe in CollectSceneComponents<ReflectionProbe>(scene))
                records[GetComponentKey(probe)] = new ReflectionProbeBakeRecord(
                    MapAssetPath(AssetDatabase.GetAssetPath(probe.bakedTexture), assetMap),
                    MapAssetPath(AssetDatabase.GetAssetPath(probe.customBakedTexture), assetMap));

            return records;
        }

        private static int ApplyReflectionProbes(Scene scene,
            IReadOnlyDictionary<string, ReflectionProbeBakeRecord> records)
        {
            if (records == null || records.Count == 0)
                return 0;

            var applied = 0;
            foreach (var probe in CollectSceneComponents<ReflectionProbe>(scene))
            {
                if (!records.TryGetValue(GetComponentKey(probe), out var record))
                    continue;

                probe.bakedTexture = LoadAsset<Texture>(record.BakedTexturePath);
                probe.customBakedTexture = LoadAsset<Texture>(record.CustomBakedTexturePath);
                EditorUtility.SetDirty(probe);
                applied++;
            }

            return applied;
        }

        private static T LoadAsset<T>(string assetPath) where T : Object
        {
            return string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

        private static string MapAssetPath(string assetPath, IReadOnlyDictionary<string, string> assetMap)
        {
            assetPath = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(assetPath))
                return string.Empty;

            return assetMap != null && assetMap.TryGetValue(assetPath, out var mapped) ? mapped : assetPath;
        }

        private static string GetComponentKey(Component component)
        {
            if (!component)
                return string.Empty;

            return $"{component.GetType().FullName}|{BuildTransformPath(component.transform)}";
        }

        private static string BuildTransformPath(Transform transform)
        {
            if (!transform)
                return string.Empty;

            var segments = new Stack<string>();
            for (var current = transform; current; current = current.parent)
                segments.Push($"{current.name}[{current.GetSiblingIndex()}]");

            return string.Join("/", segments);
        }

        private static IEnumerable<T> CollectSceneComponents<T>(Scene scene) where T : Component
        {
            return MapSceneTraversalUtility.GetComponents<T>(scene);
        }

        internal struct ApplySummary
        {
            internal bool LightingDataApplied { get; set; }
            internal int LightmapSlotsApplied { get; set; }
            internal bool OcclusionDataApplied { get; set; }
            internal bool NavigationApplied { get; set; }
            internal int RenderersApplied { get; set; }
            internal int TerrainsApplied { get; set; }
            internal int ReflectionProbesApplied { get; set; }

            internal string BuildMessage(MapBakeArtifactFileMirror.Result mirror, int lightmapCount)
            {
                var lighting = LightingDataApplied ? $"{lightmapCount} lightmap slot(s)" : "no lighting data";
                var navigation = NavigationApplied ? "navmesh" : "no navmesh";
                var occlusion = OcclusionDataApplied ? "occlusion" : "no occlusion";
                return
                    $"Applied compiled bake artifacts to the authoring scene ({lighting}, {navigation}, {occlusion}; " +
                    $"{RenderersApplied} renderer(s), {TerrainsApplied} terrain(s), " +
                    $"{ReflectionProbesApplied} reflection probe(s); " +
                    $"{mirror.Copied} artifact file(s) copied, {mirror.Deleted} stale removed).";
            }

            internal string BuildLightingPreservationMessage(MapBakeArtifactFileMirror.Result mirror,
                int lightmapCount)
            {
                var lighting = LightingDataApplied
                    ? $"{lightmapCount} lightmap slot(s)"
                    : "no lighting data";
                return
                    $"Applied preserved authoring lighting to the skipped-lighting compiled scene ({lighting}; " +
                    $"{RenderersApplied} renderer(s), {TerrainsApplied} terrain(s), " +
                    $"{ReflectionProbesApplied} reflection probe(s); " +
                    $"{mirror?.Copied ?? 0} artifact file(s) copied).";
            }
        }

        internal readonly struct ApplyOptions
        {
            internal static ApplyOptions All => new(true, true, true);
            internal static ApplyOptions LightingOnly => new(true, false, false);

            private ApplyOptions(bool applyLighting, bool applyNavigation, bool applyOcclusion)
            {
                ApplyLighting = applyLighting;
                ApplyNavigation = applyNavigation;
                ApplyOcclusion = applyOcclusion;
            }

            internal bool ApplyLighting { get; }
            internal bool ApplyNavigation { get; }
            internal bool ApplyOcclusion { get; }
        }

        private readonly struct LightmapSlot
        {
            internal LightmapSlot(string colorPath, string directionPath, string shadowMaskPath)
            {
                ColorPath = colorPath ?? string.Empty;
                DirectionPath = directionPath ?? string.Empty;
                ShadowMaskPath = shadowMaskPath ?? string.Empty;
            }

            internal string ColorPath { get; }
            internal string DirectionPath { get; }
            internal string ShadowMaskPath { get; }
        }

        private readonly struct LightmapBakeRecord
        {
            internal LightmapBakeRecord(int lightmapIndex, Vector4 lightmapScaleOffset, int realtimeLightmapIndex,
                Vector4 realtimeLightmapScaleOffset)
            {
                LightmapIndex = lightmapIndex;
                LightmapScaleOffset = lightmapScaleOffset;
                RealtimeLightmapIndex = realtimeLightmapIndex;
                RealtimeLightmapScaleOffset = realtimeLightmapScaleOffset;
            }

            internal int LightmapIndex { get; }
            internal Vector4 LightmapScaleOffset { get; }
            internal int RealtimeLightmapIndex { get; }
            internal Vector4 RealtimeLightmapScaleOffset { get; }
        }

        private readonly struct ReflectionProbeBakeRecord
        {
            internal ReflectionProbeBakeRecord(string bakedTexturePath, string customBakedTexturePath)
            {
                BakedTexturePath = bakedTexturePath ?? string.Empty;
                CustomBakedTexturePath = customBakedTexturePath ?? string.Empty;
            }

            internal string BakedTexturePath { get; }
            internal string CustomBakedTexturePath { get; }
        }
    }
}
#endif
