// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapRendererOptimizationUtility
    {
        private static readonly StaticEditorFlags NormalizedStaticFlags =
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic;

        internal static bool Optimize(Scene scene, out string message)
        {
            message = string.Empty;
            if (!scene.IsValid())
            {
                message = "Compiled scene is invalid.";
                return false;
            }

            var renderers = CollectRenderers(scene);
            var summary = new Summary(renderers.Count);
            var dirty = NormalizeMovingBrushHierarchies(scene, ref summary);

            foreach (var renderer in renderers)
            {
                if (!renderer)
                    continue;

                dirty |= NormalizeRenderer(renderer, ref summary);
            }

            if (dirty)
                EditorSceneManager.MarkSceneDirty(scene);

            message = summary.BuildMessage();
            return true;
        }

        private static bool NormalizeMovingBrushHierarchies(Scene scene, ref Summary summary)
        {
            var changed = false;
            var normalizedRoots = new HashSet<int>();
            var sceneRoots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < sceneRoots.Length; rootIndex++)
            {
                var sceneRoot = sceneRoots[rootIndex];
                if (!sceneRoot)
                    continue;

                var behaviours = sceneRoot.GetComponentsInChildren<MonoBehaviour>(true);
                for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                {
                    if (behaviours[behaviourIndex] is not IMovingBrushEntityMarker movingBrush ||
                        !movingBrush.BrushTransform)
                        continue;

                    var brushRoot = movingBrush.BrushTransform;
                    if (!normalizedRoots.Add(brushRoot.GetInstanceID()))
                        continue;

                    var transforms = brushRoot.GetComponentsInChildren<Transform>(true);
                    for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                    {
                        var current = transforms[transformIndex];
                        if (!current || GameObjectUtility.GetStaticEditorFlags(current.gameObject) == 0)
                            continue;

                        GameObjectUtility.SetStaticEditorFlags(current.gameObject, 0);
                        summary.DynamicStaticFlagsCleared++;
                        changed = true;
                    }

                    var brushRenderers = brushRoot.GetComponentsInChildren<Renderer>(true);
                    for (var rendererIndex = 0; rendererIndex < brushRenderers.Length; rendererIndex++)
                    {
                        var renderer = brushRenderers[rendererIndex];
                        if (!renderer || !renderer.staticShadowCaster)
                            continue;

                        renderer.staticShadowCaster = false;
                        summary.DynamicStaticShadowCastersCleared++;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static List<Renderer> CollectRenderers(Scene scene)
        {
            return scene.GetRootGameObjects()
                .Where(root => root)
                .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer)
                .ToList();
        }

        private static bool NormalizeRenderer(Renderer renderer, ref Summary summary)
        {
            var changed = false;
            if (HasRuntimeMotionRisk(renderer.transform))
            {
                if (renderer is MeshRenderer mesh && mesh.receiveGI != ReceiveGI.LightProbes)
                { mesh.receiveGI = ReceiveGI.LightProbes; changed = true; }
                if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.Object)
                { renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object; changed = true; }
                var currentFlags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if (currentFlags != 0)
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
                    summary.DynamicStaticFlagsCleared++;
                    changed = true;
                }

                if (renderer.staticShadowCaster)
                {
                    renderer.staticShadowCaster = false;
                    summary.DynamicStaticShadowCastersCleared++;
                    changed = true;
                }

                return changed;
            }

            var triggerVolume = renderer.GetComponentsInParent<EntityMarker>(true)
                .Any(marker => marker.EntityClassName.StartsWith("trigger_", System.StringComparison.Ordinal));
            if ((renderer.gameObject.isStatic || triggerVolume) && ShouldNormalizeStaticFlags(renderer))
            {
                if (!renderer.staticShadowCaster)
                {
                    renderer.staticShadowCaster = true;
                    summary.StaticShadowCastersEnabled++;
                    changed = true;
                }

                var currentFlags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                var desiredFlags = triggerVolume
                    ? (currentFlags | StaticEditorFlags.OccludeeStatic) & ~StaticEditorFlags.OccluderStatic
                    : currentFlags | NormalizedStaticFlags;
                if (currentFlags != desiredFlags)
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, desiredFlags);
                    summary.StaticFlagsNormalized++;
                    changed = true;
                }
            }

            if (renderer.gameObject.isStatic &&
                renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
            {
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                summary.MotionVectorsDisabled++;
                changed = true;
            }

            if (IsPureFxRenderer(renderer))
            {
                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    summary.FxShadowsDisabled++;
                    changed = true;
                }

                if (renderer.receiveShadows)
                {
                    renderer.receiveShadows = false;
                    summary.FxShadowsDisabled++;
                    changed = true;
                }

                if (renderer.reflectionProbeUsage != ReflectionProbeUsage.Off)
                {
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    summary.FxProbeUsageNormalized++;
                    changed = true;
                }

                if (renderer.lightProbeUsage != LightProbeUsage.Off)
                {
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    summary.FxProbeUsageNormalized++;
                    changed = true;
                }

                if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
                {
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    summary.MotionVectorsDisabled++;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool ShouldNormalizeStaticFlags(Renderer renderer)
        {
            return renderer is MeshRenderer;
        }

        private static bool IsPureFxRenderer(Renderer renderer)
        {
            return renderer is ParticleSystemRenderer or TrailRenderer or LineRenderer;
        }

        internal static bool HasRuntimeMotionRisk(Transform transform)
        {
            var current = transform;
            while (current)
            {
                if (HasRuntimeMotionRisk(current.gameObject))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private static bool HasRuntimeMotionRisk(GameObject gameObject)
        {
            if (!gameObject)
                return false;

            if (gameObject.GetComponent<Animator>() ||
                gameObject.GetComponent<Animation>() ||
                gameObject.GetComponent<Rigidbody>() ||
                gameObject.GetComponent<Rigidbody2D>() ||
                gameObject.GetComponent<SkinnedMeshRenderer>() ||
                gameObject.GetComponent<ParticleSystem>() ||
                gameObject.GetComponent<NavMeshObstacle>() ||
                gameObject.GetComponent<PlayableDirector>() ||
                gameObject.GetComponent<IConstraint>() != null)
                return true;

            var components = gameObject.GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (!component)
                    continue;

                if (component is IMovingBrushEntityMarker or Vellocet.Sdk.Entities.DamageableMarker)
                    return true;
            }

            return false;
        }

        private struct Summary
        {
            internal Summary(int scanned)
            {
                Scanned = scanned;
                DynamicStaticFlagsCleared = 0;
                DynamicStaticShadowCastersCleared = 0;
                StaticShadowCastersEnabled = 0;
                StaticFlagsNormalized = 0;
                MotionVectorsDisabled = 0;
                FxShadowsDisabled = 0;
                FxProbeUsageNormalized = 0;
            }

            internal readonly int Scanned;
            internal int DynamicStaticFlagsCleared;
            internal int DynamicStaticShadowCastersCleared;
            internal int StaticShadowCastersEnabled;
            internal int StaticFlagsNormalized;
            internal int MotionVectorsDisabled;
            internal int FxShadowsDisabled;
            internal int FxProbeUsageNormalized;

            internal string BuildMessage()
            {
                return
                    $"Renderer optimization scanned {Scanned} renderer(s): " +
                    $"{DynamicStaticFlagsCleared} dynamic static-flag cleanup(s), " +
                    $"{DynamicStaticShadowCastersCleared} dynamic static-shadow cleanup(s), " +
                    $"{StaticShadowCastersEnabled} static shadow caster(s) enabled, " +
                    $"{StaticFlagsNormalized} static-flag normalization(s), " +
                    $"{MotionVectorsDisabled} motion-vector optimization(s), " +
                    $"{FxShadowsDisabled} FX shadow cleanup change(s), " +
                    $"{FxProbeUsageNormalized} FX probe cleanup change(s).";
            }
        }
    }
}
#endif
