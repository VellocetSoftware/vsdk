// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Spatial;
using Vellocet.Sdk.Editor.VSig;
using Vellocet.Sdk.Maps;
using Vellocet.Sdk.Utilities;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    internal sealed class MapCompileContext
    {
        internal MapCompileContext(SceneAsset sceneAsset, string sourceScenePath, string compiledScenePath,
            AddressableAssetSettings settings, MapCompiler.CompileOptions options, MapCompiler.Result result,
            MapCompilationReceipt receipt)
        {
            SceneAsset = sceneAsset;
            SourceScenePath = sourceScenePath;
            CompiledScenePath = compiledScenePath;
            Settings = settings;
            Options = options;
            Result = result;
            Receipt = receipt;
        }

        internal SceneAsset SceneAsset { get; }
        internal string SourceScenePath { get; }
        internal string CompiledScenePath { get; }
        internal AddressableAssetSettings Settings { get; }
        internal MapCompiler.CompileOptions Options { get; }
        internal MapCompiler.Result Result { get; }
        internal MapCompilationReceipt Receipt { get; }
        internal Scene Scene { get; set; }
        internal AddressableAssetGroup SharedGroup { get; set; }
        internal HashSet<string> SharedGuids { get; set; }
    }

    internal interface IMapCompilePass
    {
        string Name { get; }
        float Progress { get; }
        string StartMessage { get; }
        MapCompilePassResult Execute(MapCompileContext context);
    }

    internal sealed class MapCompileSession
    {
        private readonly string _compiledScenePath;
        private readonly MapCompiler.CompileOptions _options;
        private readonly List<MapCompilationReceipt.CompilePassRecord> _passRecords;
        private readonly MapCompilationReceipt _receipt;

        internal MapCompileSession(MapCompiler.CompileOptions options, MapCompilationReceipt receipt,
            string compiledScenePath)
        {
            _options = options;
            _receipt = receipt;
            _compiledScenePath = compiledScenePath;
            _passRecords = new List<MapCompilationReceipt.CompilePassRecord>();
        }

        internal MapCompilationReceipt FinalizeReceipt()
        {
            _receipt.sourceSceneHash = ComputeFileSha256(_receipt.sourceScenePath);
            _receipt.compiledScenePath = _compiledScenePath ?? string.Empty;
            _receipt.compiledSceneHash = ComputeFileSha256(_compiledScenePath);
            _receipt.passes = _passRecords.ToArray();
            _receipt.contentRevision = MapCompilationReceiptUtility.ComputeContentRevision(_receipt);
            return _receipt;
        }

        internal void Report(float progress, string message)
        {
            _options?.ReportProgress?.Invoke(Mathf.Clamp01(progress), message ?? string.Empty);
            Log(message);
        }

        internal void Log(string message)
        {
            if (_options == null || string.IsNullOrWhiteSpace(message))
                return;

            _options.Log?.Invoke(message.Trim());
        }

        internal void Record(string name, bool succeeded, string message)
        {
            _passRecords.Add(new MapCompilationReceipt.CompilePassRecord
            {
                name = name,
                status = succeeded ? "ok" : "error",
                message = message
            });
        }

        internal bool RunPass(IMapCompilePass pass, MapCompileContext context, out string error)
        {
            Report(pass.Progress, pass.StartMessage);
            var stopwatch = Stopwatch.StartNew();
            var outcome = pass.Execute(context);
            stopwatch.Stop();
            var message = AppendDuration(outcome.Message, stopwatch.Elapsed);
            Record(pass.Name, outcome.Success, message);
            Log($"{pass.Name}: {message}");
            error = outcome.Success ? string.Empty : message;
            return outcome.Success;
        }

        internal static string ComputeFileSha256(string path)
        {
            return SdkFileHashUtility.TryComputeSha256(path, out var sha256) ? sha256 : string.Empty;
        }

        private static string AppendDuration(string message, TimeSpan elapsed)
        {
            var text = string.IsNullOrWhiteSpace(message) ? "Completed." : message.Trim();
            return $"{text} [duration={FormatDuration(elapsed)}]";
        }

        private static string FormatDuration(TimeSpan elapsed)
        {
            var totalMilliseconds = Math.Max(0d, elapsed.TotalMilliseconds);
            if (totalMilliseconds < 1000d)
                return ((long)Math.Round(totalMilliseconds)).ToString(CultureInfo.InvariantCulture) + "ms";

            return elapsed.TotalSeconds.ToString(totalMilliseconds < 10000d ? "0.00" : "0.0",
                CultureInfo.InvariantCulture) + "s";
        }
    }

    internal static class MapCompilePassFactory
    {
        private const string VSigRuntimeObjectName = "VSigRuntime";

        internal static IEnumerable<IMapCompilePass> Create(MapCompileContext context)
        {
            var rewriteSharedReferences = string.Equals(context.Receipt.contentKind,
                MapCompilationReceipt.AddonContentKind, StringComparison.OrdinalIgnoreCase);

            yield return new DelegateMapCompilePass(
                "prepare-scene",
                0.12f,
                "Preparing compiled scene contract.",
                ctx =>
                {
                    MapSceneUtility.UnpackPrefabInstances(ctx.Scene);
                    var sceneContext = MapSceneUtility.EnsureSceneContract(ctx.Scene, true, out var summary);
                    if (ctx.Options?.BakePolicy != null) VertexBakePolicy.Apply(ctx.Scene, ctx.Options.BakePolicy);
                    if (!sceneContext.IsSceneContractSatisfied)
                        return MapCompilePassResult.Fail(
                            $"Map scene contract is incomplete after auto-setup: {MapSceneUtility.DescribeContract(sceneContext)}.");

                    return MapCompilePassResult.Pass(string.IsNullOrWhiteSpace(summary)
                        ? "Scene contract verified."
                        : summary);
                });

            yield return new DelegateMapCompilePass(
                "optimize-mesh-colliders",
                0.22f,
                "Optimizing mesh colliders.",
                ctx => MapMeshOptimizationUtility.Optimize(ctx.Scene, out var message)
                    ? MapCompilePassResult.Pass(message)
                    : MapCompilePassResult.Fail(message));

            yield return new DelegateMapCompilePass(
                "audit-colliders",
                0.28f,
                "Auditing remaining collider complexity.",
                ctx => MapColliderAuditUtility.Audit(ctx.Scene, out var message)
                    ? MapCompilePassResult.Pass(message)
                    : MapCompilePassResult.Fail(message));

            yield return new DelegateMapCompilePass(
                "optimize-renderers",
                0.34f,
                "Normalizing renderer and static settings.",
                ctx => MapRendererOptimizationUtility.Optimize(ctx.Scene, out var message)
                    ? MapCompilePassResult.Pass(message)
                    : MapCompilePassResult.Fail(message));

            yield return new DelegateMapCompilePass(
                "weave-shared-assets",
                0.42f,
                rewriteSharedReferences
                    ? "Rewriting shared SDK asset references."
                    : "Auditing shared SDK asset references for base content.",
                ctx =>
                {
                    var assetUsage = MapAssetUsageAnalyzer.Analyze(ctx.Scene, ctx.Settings, ctx.SharedGuids,
                        rewriteSharedReferences);
                    ctx.Result.AssetUsageReport = assetUsage;

                    if (assetUsage.HasMisconfiguredSdkAssets)
                        return MapCompilePassResult.Fail(
                            "One or more SDK assets would be copied into the package instead of resolved from the shared Base Addressables group.");

                    if (rewriteSharedReferences)
                        return MapCompilePassResult.Pass(assetUsage.sharedAssets.Length > 0
                            ? $"Rewrote {assetUsage.sharedAssets.Length} shared asset reference group(s)."
                            : "No shared asset rewrites were required.");

                    return MapCompilePassResult.Pass(assetUsage.sharedAssets.Length > 0
                        ? $"Verified {assetUsage.sharedAssets.Length} shared asset reference group(s) for base content without rewriting scene references."
                        : "No shared asset reuse was detected.");
                });

            yield return new DelegateMapCompilePass(
                "apply-lighting-settings",
                0.5f,
                context.Options?.SkipLightingBake ?? false
                    ? "Skipping lighting settings because lighting bake is disabled."
                    : "Applying selected lightmap quality.",
                ctx =>
                {
                    if (ctx.Options?.SkipLightingBake ?? false)
                    {
                        ctx.Receipt.lightingSettingsPath = string.Empty;
                        return MapCompilePassResult.Pass("Lighting settings were skipped.");
                    }

                    if (!MapLightingSettingsUtility.Apply(
                            ctx.Options?.LightmapQuality ?? MapLightmapQuality.Medium, out var settingsPath,
                            out var message))
                        return MapCompilePassResult.Fail(message);

                    ctx.Receipt.lightingSettingsPath = settingsPath ?? string.Empty;
                    EditorSceneManager.MarkSceneDirty(ctx.Scene);
                    return MapCompilePassResult.Pass(message);
                });

            yield return new DelegateMapCompilePass(
                "configure-probe-volumes",
                0.53f,
                context.Options?.SkipLightingBake ?? false
                    ? "Skipping Adaptive Probe Volume setup because lighting bake is disabled."
                    : "Configuring Adaptive Probe Volume coverage.",
                ctx =>
                {
                    if (ctx.Options?.SkipLightingBake ?? false)
                        return MapCompilePassResult.Pass("Adaptive Probe Volume setup was skipped.");

                    return MapProbeVolumeUtility.ConfigureForLightingBake(ctx.Scene, ctx.CompiledScenePath,
                        out var message)
                        ? MapCompilePassResult.Pass(message)
                        : MapCompilePassResult.Fail(message);
                });

            yield return new DelegateMapCompilePass(
                "bake-spatial-octree",
                0.56f,
                context.Options?.SkipSpatialOctreeBake ?? false
                    ? "Skipping spatial octree bake."
                    : "Baking spatial octree.",
                ctx => ctx.Options?.SkipSpatialOctreeBake ?? false
                    ? SkipSpatialOctree(ctx)
                    : BakeSpatialOctree(ctx));

            yield return new DelegateMapCompilePass(
                "bake-navigation",
                0.6f,
                "Baking navigation.",
                ctx => MapSceneUtility.BakeNavigation(ctx.Scene, out var message)
                    ? MapCompilePassResult.Pass(message)
                    : MapCompilePassResult.Fail(message));

            yield return new DelegateMapCompilePass(
                "bake-lighting",
                0.74f,
                context.Options?.SkipLightingBake ?? false ? "Skipping lighting bake." : "Baking lighting.",
                ctx =>
                {
                    if (ctx.Options?.SkipLightingBake ?? false)
                        return MapCompilePassResult.Pass("Lighting bake was skipped.");

                    return MapSceneUtility.BakeLightingBlocking(ctx.Scene, out var message)
                        ? MapCompilePassResult.Pass(message)
                        : MapCompilePassResult.Fail(message);
                });

            yield return new DelegateMapCompilePass(
                "bake-occlusion",
                0.86f,
                "Baking occlusion culling.",
                ctx => MapSceneUtility.BakeOcclusion(ctx.Scene, out var message)
                    ? MapCompilePassResult.Pass(message)
                    : MapCompilePassResult.Fail(message));

            if (context.Options?.FinalizeLegacyGeometry ?? true)
            {
                yield return new DelegateMapCompilePass(
                    "strip-probuilder",
                    0.91f,
                    "Stripping ProBuilder components.",
                    ctx => StripProBuilderScene(ctx.Scene, out var message)
                        ? MapCompilePassResult.Pass(message)
                        : MapCompilePassResult.Fail(message));

                yield return new DelegateMapCompilePass(
                    "finalize-probuilder-artifacts",
                    0.95f,
                    "Finalizing ProBuilder-derived meshes and tool visuals.",
                    ctx => MapProBuilderFinalizationUtility.Finalize(
                        ctx.Scene,
                        ctx.CompiledScenePath,
                        new MapProBuilderFinalizationUtility.FinalizationOptions(
                            ctx.Options?.ConfigureToolVisuals ?? true,
                            ctx.Options?.PruneGeneratedMeshes ?? true),
                        out var message)
                        ? MapCompilePassResult.Pass(message)
                        : MapCompilePassResult.Fail(message));

            }

            yield return new DelegateMapCompilePass(
                "compile-vsig",
                0.965f,
                "Compiling VSig scene signals.",
                ctx =>
                {
                    var runtimeRoot = EnsureRuntimeRoot(ctx.Scene);
                    if (!runtimeRoot)
                        return MapCompilePassResult.Fail("Map runtime root could not be created.");

                    var options = new VSigCompiledSceneInjectionOptions(
                        runtimeRoot.transform,
                        VSigRuntimeObjectName);
                    if (ctx.Options?.ScriptSource)
                        return VSigCompiledSceneInjector.InjectSource(ctx.Scene, ctx.Options.ScriptSource, ctx.Receipt,
                            out var sourceMessage, options)
                            ? MapCompilePassResult.Pass(sourceMessage)
                            : MapCompilePassResult.Fail(sourceMessage);
                    return VSigCompiledSceneInjector.Inject(ctx.Scene, ctx.SourceScenePath, ctx.Receipt, options,
                        out var message)
                        ? MapCompilePassResult.Pass(message)
                        : MapCompilePassResult.Fail(message);
                });

            yield return new DelegateMapCompilePass(
                "strip-dead-content",
                0.97f,
                "Removing dead editor-only scene content.",
                ctx => MapDeadContentUtility.Strip(ctx.Scene, out var message)
                    ? MapCompilePassResult.Pass(message)
                    : MapCompilePassResult.Fail(message));

            yield return new DelegateMapCompilePass(
                "validate-runtime-dependencies",
                0.98f,
                "Validating compiled scene runtime dependencies.",
                ctx => ValidateRuntimeDependencies(ctx, out var message)
                    ? MapCompilePassResult.Pass(message)
                    : MapCompilePassResult.Fail(message));
        }

        private static bool ValidateRuntimeDependencies(MapCompileContext context, out string message)
        {
            if (context?.Options?.SkipSpatialOctreeBake ?? false)
                return MapSpatialGraphArtifacts.TryValidateSkippedSceneRuntimeDependency(
                    context.Scene, out message);

            return MapSpatialGraphArtifacts.TryValidateSceneRuntimeDependency(context?.Scene ?? default,
                context?.Receipt?.mapId, context?.Receipt?.sceneAddress, out _, out message);
        }

        private static bool StripProBuilderScene(Scene scene, out string message)
        {
            if (!scene.IsValid())
            {
                message = "Compiled scene is invalid.";
                return false;
            }

            var components = FindProBuilderComponents(scene);
            if (components.Length == 0)
            {
                message = "No ProBuilder runtime components were found.";
                return true;
            }

            var removed = 0;
            foreach (var component in components)
            {
                if (!component)
                    continue;

                Object.DestroyImmediate(component, true);
                removed++;
            }

            if (removed > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            var remaining = FindProBuilderComponents(scene);
            if (remaining.Length > 0)
            {
                message =
                    $"Failed to remove all ProBuilder components. {remaining.Length} component(s) still remain in the compiled scene.";
                return false;
            }

            message = removed > 0
                ? $"Removed {removed} ProBuilder component(s)."
                : "No ProBuilder runtime components were found.";
            return true;
        }

        private static Component[] FindProBuilderComponents(Scene scene)
        {
            if (!scene.IsValid())
                return Array.Empty<Component>();

            return scene.GetRootGameObjects()
                .Where(root => root)
                .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Where(component => component && IsProBuilderRuntimeComponent(component.GetType()))
                .ToArray();
        }

        private static bool IsProBuilderRuntimeComponent(Type type)
        {
            var fullName = type?.FullName;
            return !string.IsNullOrWhiteSpace(fullName) &&
                   fullName.StartsWith("UnityEngine.ProBuilder.", StringComparison.Ordinal);
        }

        private static MapCompilePassResult BakeSpatialOctree(MapCompileContext context)
        {
            if (context == null || !context.Scene.IsValid())
                return MapCompilePassResult.Fail("Compiled scene is invalid.");

            var mapId = ContentIdUtility.Sanitize(context.Receipt.mapId);
            if (string.IsNullOrWhiteSpace(mapId))
                return MapCompilePassResult.Fail("Map id is required for spatial octree baking.");

            var assetPath = MapSpatialGraphArtifacts.ResolveAssetPath(context.CompiledScenePath, mapId);
            if (string.IsNullOrWhiteSpace(assetPath))
                return MapCompilePassResult.Fail("Spatial octree asset path could not be resolved.");

            var address = MapSpatialGraphArtifacts.ResolveAddress(mapId);
            if (string.IsNullOrWhiteSpace(address))
                return MapCompilePassResult.Fail("Spatial octree address could not be resolved.");

            try
            {
                var bakedAsset = SpatialOctreeBaker.BakeCompiledScene(
                    context.Scene,
                    mapId,
                    context.Receipt.sceneAddress,
                    assetPath,
                    address,
                    out var reusedCachedAsset, context.Options?.BakePolicy != null ? Vellocet.Sdk.Spatial.SpatialOctreeBakeSettings.Default : (Vellocet.Sdk.Spatial.SpatialOctreeBakeSettings?)null);

                if (!bakedAsset)
                    return MapCompilePassResult.Fail("Spatial octree baker did not return a baked asset.");

                if (!MapSpatialGraphArtifacts.TryValidateBakedAsset(bakedAsset, mapId,
                        context.Receipt.sceneAddress, out var validationMessage))
                    return MapCompilePassResult.Fail(validationMessage);

                context.Receipt.spatialGraphAddress = address;

                if (!MapSpatialGraphArtifacts.TryRegisterCompiledDependency(context.Result, bakedAsset, address,
                        out var artifact, out var registrationSummary))
                    return MapCompilePassResult.Fail(registrationSummary);

                if (!MapSpatialGraphArtifacts.TryAssignRuntimeDependency(context.Scene, bakedAsset,
                        out var assignmentSummary))
                    return MapCompilePassResult.Fail(assignmentSummary);

                var action = reusedCachedAsset ? "Reused cached" : "Baked";
                return MapCompilePassResult.Pass(
                    $"{action} spatial octree '{artifact.AssetPath}' with address '{address}'. " +
                    $"nodes={bakedAsset.Nodes.Length}, traversable={bakedAsset.TraversableLeafCount}, " +
                    $"settings={SpatialOctreeBaker.DescribeSettings(bakedAsset.BakeSettings)}. " +
                    $"{validationMessage} {registrationSummary} {assignmentSummary}");
            }
            catch (Exception ex)
            {
                return MapCompilePassResult.Fail(ex.Message);
            }
        }

        private static MapCompilePassResult SkipSpatialOctree(MapCompileContext context)
        {
            if (context == null || !context.Scene.IsValid())
                return MapCompilePassResult.Fail("Compiled scene is invalid.");

            context.Receipt.spatialGraphAddress = string.Empty;
            return MapSpatialGraphArtifacts.TryClearRuntimeDependency(context.Scene, out var message)
                ? MapCompilePassResult.Pass($"Spatial octree bake was skipped. {message}")
                : MapCompilePassResult.Fail(message);
        }

        private static GameObject EnsureRuntimeRoot(Scene scene)
        {
            return MapSpatialGraphArtifacts.EnsureRuntimeRoot(scene);
        }
    }

    internal readonly struct MapCompilePassResult
    {
        internal MapCompilePassResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        internal bool Success { get; }
        internal string Message { get; }

        internal static MapCompilePassResult Pass(string message)
        {
            return new MapCompilePassResult(true, message);
        }

        internal static MapCompilePassResult Fail(string message)
        {
            return new MapCompilePassResult(false, message);
        }
    }

    internal sealed class DelegateMapCompilePass : IMapCompilePass
    {
        private readonly Func<MapCompileContext, MapCompilePassResult> _execute;

        internal DelegateMapCompilePass(string name, float progress, string startMessage,
            Func<MapCompileContext, MapCompilePassResult> execute)
        {
            Name = name ?? string.Empty;
            Progress = progress;
            StartMessage = startMessage ?? string.Empty;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public string Name { get; }
        public float Progress { get; }
        public string StartMessage { get; }

        public MapCompilePassResult Execute(MapCompileContext context)
        {
            return _execute(context);
        }
    }
}
#endif
