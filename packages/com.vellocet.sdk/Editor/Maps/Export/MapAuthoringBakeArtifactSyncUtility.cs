// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapAuthoringBakeArtifactSyncUtility
    {
        private const string SyncPassName = "sync-authoring-baked-artifacts";
        private const string PreservePassName = "preserve-skipped-lighting-artifacts";

        internal static string PassName => SyncPassName;
        internal static string PreserveSkippedLightingPassName => PreservePassName;

        internal static bool PrepareSkippedLighting(string sourceScenePath, string compiledScenePath,
            out SkippedLightingArtifacts artifacts, out string message)
        {
            artifacts = null;
            try
            {
                if (!MapBakeArtifactFileMirror.CopyAuthoringToCompiled(sourceScenePath, compiledScenePath,
                        out var mirror, out message))
                    return false;

                var authoringScene = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Single);
                if (!authoringScene.IsValid())
                {
                    message = $"Could not open authoring scene '{sourceScenePath}' to preserve skipped lighting.";
                    return false;
                }

                var snapshot = MapCompiledBakeSnapshot.Capture(authoringScene, mirror.AssetMap);
                artifacts = new SkippedLightingArtifacts(snapshot, mirror);
                message =
                    $"Copied {mirror.Copied} bake artifact file(s) and captured {snapshot.LightmapCount} lightmap slot(s).";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Could not prepare skipped-lighting artifacts: {ex.Message}";
                return false;
            }
        }

        internal static bool Sync(MapCompileContext context, out string message)
        {
            if (!ValidateContext(context, out message))
                return false;

            if (context.Options?.SkipLightingBake ?? false)
            {
                message = "Lighting bake was skipped; authoring bake artifacts were left unchanged.";
                return true;
            }

            try
            {
                if (!MapBakeArtifactFileMirror.CopyCompiledToAuthoring(context.CompiledScenePath,
                        context.SourceScenePath, out var mirror, out message))
                    return false;

                var snapshot = MapCompiledBakeSnapshot.Capture(context.Scene, mirror.AssetMap);
                var authoringScene = EditorSceneManager.OpenScene(context.SourceScenePath, OpenSceneMode.Single);
                if (!authoringScene.IsValid())
                {
                    message = $"Could not open authoring scene '{context.SourceScenePath}'.";
                    return false;
                }

                var summary = snapshot.Apply(authoringScene);
                if (!EditorSceneManager.SaveScene(authoringScene))
                {
                    message = $"Could not save authoring scene '{context.SourceScenePath}'.";
                    return false;
                }

                message = summary.BuildMessage(mirror, snapshot.LightmapCount);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Authoring bake artifact sync failed: {ex.Message}";
                return false;
            }
        }

        private static bool ValidateContext(MapCompileContext context, out string message)
        {
            message = string.Empty;
            if (context == null)
            {
                message = "Compile context is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(context.SourceScenePath) ||
                string.IsNullOrWhiteSpace(context.CompiledScenePath))
            {
                message = "Source or compiled scene path is missing.";
                return false;
            }

            if (!context.Scene.IsValid())
            {
                message = "Compiled scene is not loaded.";
                return false;
            }

            return true;
        }

        internal sealed class SkippedLightingArtifacts
        {
            private readonly MapBakeArtifactFileMirror.Result _mirror;
            private readonly MapCompiledBakeSnapshot _snapshot;

            internal SkippedLightingArtifacts(MapCompiledBakeSnapshot snapshot, MapBakeArtifactFileMirror.Result mirror)
            {
                _snapshot = snapshot;
                _mirror = mirror;
            }

            internal bool ApplyTo(Scene scene, out string message)
            {
                if (!scene.IsValid())
                {
                    message = "Skipped lighting compile could not apply preserved lighting: compiled scene is invalid.";
                    return false;
                }

                if (_snapshot == null)
                {
                    message = "Skipped lighting compile could not apply preserved lighting: snapshot is missing.";
                    return false;
                }

                try
                {
                    var summary = _snapshot.Apply(scene, MapCompiledBakeSnapshot.ApplyOptions.LightingOnly);
                    message = summary.BuildLightingPreservationMessage(_mirror, _snapshot.LightmapCount);
                    return !_snapshot.HasLightingData || summary.LightingDataApplied;
                }
                catch (Exception ex)
                {
                    message = $"Skipped lighting compile could not apply preserved lighting: {ex.Message}";
                    return false;
                }
            }
        }
    }
}
#endif