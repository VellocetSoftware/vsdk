// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapLightingBakeDiagnosticsUtility
    {
        private const int MaxReportedIssues = 24;

        internal static bool ValidateBakeInputs(Scene scene, out string message)
        {
            var issues = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                message = "Lighting bake input validation requires a loaded scene.";
                return false;
            }

            ValidateTransforms(scene, issues);
            ValidateRenderers(scene, issues);
            ValidateLights(scene, issues);
            ValidateReflectionProbes(scene, issues);
            ValidateTerrains(scene, issues);

            if (issues.Count == 0)
            {
                message = "Lighting bake input validation passed.";
                return true;
            }

            message = $"Lighting bake input validation found {issues.Count} non-finite value(s): " +
                      string.Join("; ", issues.Take(MaxReportedIssues));
            if (issues.Count > MaxReportedIssues)
                message += $"; and {issues.Count - MaxReportedIssues} more.";
            return false;
        }

        private static void ValidateTransforms(Scene scene, ICollection<string> issues)
        {
            foreach (var transform in FindSceneObjects<Transform>(scene))
            {
                AddIfNotFinite(issues, transform, "position", transform.position);
                AddIfNotFinite(issues, transform, "localPosition", transform.localPosition);
                AddIfNotFinite(issues, transform, "rotation", transform.rotation);
                AddIfNotFinite(issues, transform, "localRotation", transform.localRotation);
                AddIfNotFinite(issues, transform, "lossyScale", transform.lossyScale);
                AddIfNotFinite(issues, transform, "localScale", transform.localScale);
            }
        }

        private static void ValidateRenderers(Scene scene, ICollection<string> issues)
        {
            foreach (var renderer in FindSceneObjects<Renderer>(scene))
            {
                if (!renderer.enabled)
                    continue;

                AddIfNotFinite(issues, renderer, "bounds", renderer.bounds);
                AddIfNotFinite(issues, renderer, "lightmapScaleOffset", renderer.lightmapScaleOffset);
                AddIfNotFinite(issues, renderer, "realtimeLightmapScaleOffset", renderer.realtimeLightmapScaleOffset);
                if (TryReadSerializedFloat(renderer, "m_ScaleInLightmap", out var scaleInLightmap))
                    AddIfNotFinite(issues, renderer, "scaleInLightmap", scaleInLightmap);
            }
        }

        private static void ValidateLights(Scene scene, ICollection<string> issues)
        {
            foreach (var light in FindSceneObjects<Light>(scene))
            {
                if (!light.enabled)
                    continue;

                AddIfNotFinite(issues, light, "color", light.color);
                AddIfNotFinite(issues, light, "intensity", light.intensity);
                AddIfNotFinite(issues, light, "bounceIntensity", light.bounceIntensity);
                AddIfNotFinite(issues, light, "range", light.range);
                AddIfNotFinite(issues, light, "spotAngle", light.spotAngle);
                AddIfNotFinite(issues, light, "innerSpotAngle", light.innerSpotAngle);
                AddIfNotFinite(issues, light, "colorTemperature", light.colorTemperature);
                AddIfNotFinite(issues, light, "shadowStrength", light.shadowStrength);
            }
        }

        private static void ValidateReflectionProbes(Scene scene, ICollection<string> issues)
        {
            foreach (var probe in FindSceneObjects<ReflectionProbe>(scene))
            {
                if (!probe.enabled)
                    continue;

                AddIfNotFinite(issues, probe, "center", probe.center);
                AddIfNotFinite(issues, probe, "size", probe.size);
                AddIfNotFinite(issues, probe, "intensity", probe.intensity);
                AddIfNotFinite(issues, probe, "blendDistance", probe.blendDistance);
            }
        }

        private static void ValidateTerrains(Scene scene, ICollection<string> issues)
        {
            foreach (var terrain in FindSceneObjects<Terrain>(scene))
            {
                if (!terrain.enabled)
                    continue;

                AddIfNotFinite(issues, terrain, "lightmapScaleOffset", terrain.lightmapScaleOffset);
                if (terrain.terrainData)
                {
                    AddIfNotFinite(issues, terrain, "terrainData.size", terrain.terrainData.size);
                    AddIfNotFinite(issues, terrain, "terrainData.bounds", terrain.terrainData.bounds);
                }
            }
        }

        private static IEnumerable<T> FindSceneObjects<T>(Scene scene) where T : Component
        {
            return MapSceneTraversalUtility.GetComponents<T>(scene);
        }

        private static void AddIfNotFinite(ICollection<string> issues, Object owner, string property, Bounds value)
        {
            AddIfNotFinite(issues, owner, property + ".center", value.center);
            AddIfNotFinite(issues, owner, property + ".size", value.size);
        }

        private static void AddIfNotFinite(ICollection<string> issues, Object owner, string property, Color value)
        {
            AddIfNotFinite(issues, owner, property + ".r", value.r);
            AddIfNotFinite(issues, owner, property + ".g", value.g);
            AddIfNotFinite(issues, owner, property + ".b", value.b);
            AddIfNotFinite(issues, owner, property + ".a", value.a);
        }

        private static void AddIfNotFinite(ICollection<string> issues, Object owner, string property,
            Quaternion value)
        {
            AddIfNotFinite(issues, owner, property + ".x", value.x);
            AddIfNotFinite(issues, owner, property + ".y", value.y);
            AddIfNotFinite(issues, owner, property + ".z", value.z);
            AddIfNotFinite(issues, owner, property + ".w", value.w);
        }

        private static void AddIfNotFinite(ICollection<string> issues, Object owner, string property, Vector3 value)
        {
            AddIfNotFinite(issues, owner, property + ".x", value.x);
            AddIfNotFinite(issues, owner, property + ".y", value.y);
            AddIfNotFinite(issues, owner, property + ".z", value.z);
        }

        private static void AddIfNotFinite(ICollection<string> issues, Object owner, string property, Vector4 value)
        {
            AddIfNotFinite(issues, owner, property + ".x", value.x);
            AddIfNotFinite(issues, owner, property + ".y", value.y);
            AddIfNotFinite(issues, owner, property + ".z", value.z);
            AddIfNotFinite(issues, owner, property + ".w", value.w);
        }

        private static void AddIfNotFinite(ICollection<string> issues, Object owner, string property, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                issues.Add($"{GetPath(owner)}.{property}={value}");
        }

        private static string GetPath(Object owner)
        {
            if (!owner)
                return "<missing>";

            var component = owner as Component;
            var transform = component ? component.transform : null;
            if (!transform)
                return owner.name;

            var names = new Stack<string>();
            while (transform)
            {
                names.Push(transform.name);
                transform = transform.parent;
            }

            return string.Join("/", names);
        }

        private static bool TryReadSerializedFloat(Object owner, string propertyName, out float value)
        {
            value = default;
            if (!owner || string.IsNullOrWhiteSpace(propertyName))
                return false;

            var property = new SerializedObject(owner).FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Float)
                return false;

            value = property.floatValue;
            return true;
        }

        internal sealed class LightmapperLogScope : IDisposable
        {
            private readonly List<string> _nonFiniteMessages = new();

            internal LightmapperLogScope()
            {
                Application.logMessageReceived += OnLogMessageReceived;
            }

            internal bool HasNonFiniteOutput => _nonFiniteMessages.Count > 0;

            public void Dispose()
            {
                Application.logMessageReceived -= OnLogMessageReceived;
            }

            internal string BuildFailureMessage()
            {
                var uniqueMessages = _nonFiniteMessages
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .Distinct(StringComparer.Ordinal)
                    .Take(MaxReportedIssues)
                    .ToArray();

                var message =
                    $"Lighting bake produced {_nonFiniteMessages.Count} non-finite lightmapper output warning(s): " +
                    string.Join("; ", uniqueMessages);
                if (_nonFiniteMessages.Count > uniqueMessages.Length)
                    message += $"; and {_nonFiniteMessages.Count - uniqueMessages.Length} more.";

                return message + " Treating the bake as failed; inspect emissive materials, baked lights, geometry, " +
                       "and Adaptive Probe Volume inputs before committing generated bake artifacts.";
            }

            private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
            {
                if (string.IsNullOrWhiteSpace(condition))
                    return;

                if (condition.IndexOf("Non finite value", StringComparison.OrdinalIgnoreCase) < 0 &&
                    condition.IndexOf("non-finite", StringComparison.OrdinalIgnoreCase) < 0)
                    return;

                _nonFiniteMessages.Add(condition.Trim());
            }
        }
    }
}
#endif