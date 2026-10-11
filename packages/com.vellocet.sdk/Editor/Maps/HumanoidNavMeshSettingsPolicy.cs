// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>
    ///     Keeps the SDK project's default Humanoid NavMesh agent aligned with the runtime player motor.
    ///     Unity stores these values in ProjectSettings/NavMeshAreas.asset rather than in the package itself,
    ///     so the package must synchronize the host project before it creates or bakes map surfaces.
    /// </summary>
    [InitializeOnLoad]
    internal static class HumanoidNavMeshSettingsPolicy
    {
        internal const string AgentName = "Humanoid";
        internal const int AgentTypeId = 0;
        internal const float AgentRadius = 0.5f;
        internal const float AgentHeight = 2f;
        internal const float AgentSlope = 45f;
        internal const float AgentClimb = 0.35f;
        internal const float LedgeDropHeight = 8f;
        internal const float MaxJumpAcrossDistance = 1f;
        internal const float MinRegionArea = 2f;
        internal const bool ManualCellSize = false;
        internal const float CellSize = AgentRadius / 3f;
        internal const bool ManualTileSize = false;
        internal const int TileSize = 256;
        internal const bool BuildHeightMesh = false;
        internal const int MaxJobWorkers = 0;
        internal const bool PreserveTilesOutsideBounds = false;

        private const string ProjectSettingsPath = "ProjectSettings/NavMeshAreas.asset";

        static HumanoidNavMeshSettingsPolicy()
        {
            EditorApplication.delayCall += SynchronizeAfterDomainReload;
        }

        internal static bool Ensure(out bool changed, out string error)
        {
            changed = false;
            error = string.Empty;

            var projectSettings = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsPath).FirstOrDefault();
            if (!projectSettings)
            {
                error = $"Unable to load {ProjectSettingsPath}.";
                return false;
            }

            var serialized = new SerializedObject(projectSettings);
            var settings = serialized.FindProperty("m_Settings");
            var settingNames = serialized.FindProperty("m_SettingNames");
            if (settings is not { isArray: true })
            {
                error = $"{ProjectSettingsPath} does not expose m_Settings.";
                return false;
            }

            var humanoidIndex = FindAgentIndex(settings, AgentTypeId);
            if (humanoidIndex < 0)
            {
                error = $"{ProjectSettingsPath} has no Humanoid agent with type ID {AgentTypeId}.";
                return false;
            }

            var humanoid = settings.GetArrayElementAtIndex(humanoidIndex);
            if (!TryGetFloat(humanoid, "agentRadius", out var radius) ||
                !TryGetFloat(humanoid, "agentHeight", out var height) ||
                !TryGetFloat(humanoid, "agentSlope", out var slope) ||
                !TryGetFloat(humanoid, "agentClimb", out var climb) ||
                !TryGetFloat(humanoid, "ledgeDropHeight", out var dropHeight) ||
                !TryGetFloat(humanoid, "maxJumpAcrossDistance", out var jumpDistance) ||
                !TryGetFloat(humanoid, "minRegionArea", out var minRegionArea) ||
                !TryGetBool(humanoid, "manualCellSize", out var manualCellSize) ||
                !TryGetFloat(humanoid, "cellSize", out var cellSize) ||
                !TryGetBool(humanoid, "manualTileSize", out var manualTileSize) ||
                !TryGetInt(humanoid, "tileSize", out var tileSize) ||
                !TryGetBool(humanoid, "buildHeightMesh", out var buildHeightMesh) ||
                !TryGetInt(humanoid, "maxJobWorkers", out var maxJobWorkers) ||
                !TryGetBool(humanoid, "preserveTilesOutsideBounds", out var preserveTilesOutsideBounds))
            {
                error = $"{ProjectSettingsPath} has an unsupported Humanoid settings layout.";
                return false;
            }

            if (settingNames is not { isArray: true } || humanoidIndex >= settingNames.arraySize)
            {
                error = $"{ProjectSettingsPath} does not expose the Humanoid agent name.";
                return false;
            }

            changed |= SetFloat(radius, AgentRadius);
            changed |= SetFloat(height, AgentHeight);
            changed |= SetFloat(slope, AgentSlope);
            changed |= SetFloat(climb, AgentClimb);
            changed |= SetFloat(dropHeight, LedgeDropHeight);
            changed |= SetFloat(jumpDistance, MaxJumpAcrossDistance);
            changed |= SetFloat(minRegionArea, MinRegionArea);
            changed |= SetBool(manualCellSize, ManualCellSize);
            changed |= SetFloat(cellSize, CellSize);
            changed |= SetBool(manualTileSize, ManualTileSize);
            changed |= SetInt(tileSize, TileSize);
            changed |= SetBool(buildHeightMesh, BuildHeightMesh);
            changed |= SetInt(maxJobWorkers, MaxJobWorkers);
            changed |= SetBool(preserveTilesOutsideBounds, PreserveTilesOutsideBounds);

            var name = settingNames.GetArrayElementAtIndex(humanoidIndex);
            if (name.propertyType != SerializedPropertyType.String)
            {
                error = $"{ProjectSettingsPath} has an unsupported Humanoid agent-name layout.";
                return false;
            }

            if (name.stringValue != AgentName)
            {
                name.stringValue = AgentName;
                changed = true;
            }

            if (!changed)
                return true;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(projectSettings);
            AssetDatabase.SaveAssetIfDirty(projectSettings);
            return true;
        }

        internal static bool IsCurrent(out string mismatch)
        {
            mismatch = string.Empty;
            var projectSettings = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsPath).FirstOrDefault();
            if (!projectSettings)
            {
                mismatch = $"Unable to load {ProjectSettingsPath}.";
                return false;
            }

            var serialized = new SerializedObject(projectSettings);
            var settings = serialized.FindProperty("m_Settings");
            var settingNames = serialized.FindProperty("m_SettingNames");
            if (settings is not { isArray: true })
            {
                mismatch = $"{ProjectSettingsPath} does not expose m_Settings.";
                return false;
            }

            var humanoidIndex = FindAgentIndex(settings, AgentTypeId);
            if (humanoidIndex < 0)
            {
                mismatch = $"Humanoid agent type {AgentTypeId} is missing.";
                return false;
            }

            var humanoid = settings.GetArrayElementAtIndex(humanoidIndex);
            if (settingNames is not { isArray: true } || humanoidIndex >= settingNames.arraySize)
            {
                mismatch = "Humanoid agent name is missing.";
                return false;
            }

            var name = settingNames.GetArrayElementAtIndex(humanoidIndex);
            if (name.propertyType != SerializedPropertyType.String || name.stringValue != AgentName)
            {
                mismatch = $"Humanoid agent name is '{name.stringValue}', expected '{AgentName}'.";
                return false;
            }

            return MatchesFloat(humanoid, "agentRadius", AgentRadius, ref mismatch) &&
                   MatchesFloat(humanoid, "agentHeight", AgentHeight, ref mismatch) &&
                   MatchesFloat(humanoid, "agentSlope", AgentSlope, ref mismatch) &&
                   MatchesFloat(humanoid, "agentClimb", AgentClimb, ref mismatch) &&
                   MatchesFloat(humanoid, "ledgeDropHeight", LedgeDropHeight, ref mismatch) &&
                   MatchesFloat(humanoid, "maxJumpAcrossDistance", MaxJumpAcrossDistance, ref mismatch) &&
                   MatchesFloat(humanoid, "minRegionArea", MinRegionArea, ref mismatch) &&
                   MatchesBool(humanoid, "manualCellSize", ManualCellSize, ref mismatch) &&
                   MatchesFloat(humanoid, "cellSize", CellSize, ref mismatch) &&
                   MatchesBool(humanoid, "manualTileSize", ManualTileSize, ref mismatch) &&
                   MatchesInt(humanoid, "tileSize", TileSize, ref mismatch) &&
                   MatchesBool(humanoid, "buildHeightMesh", BuildHeightMesh, ref mismatch) &&
                   MatchesInt(humanoid, "maxJobWorkers", MaxJobWorkers, ref mismatch) &&
                   MatchesBool(humanoid, "preserveTilesOutsideBounds", PreserveTilesOutsideBounds, ref mismatch);
        }

        private static void SynchronizeAfterDomainReload()
        {
            EditorApplication.delayCall -= SynchronizeAfterDomainReload;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!Ensure(out _, out var error))
                Debug.LogError($"[Vellocet SDK] Could not enforce Humanoid NavMesh settings: {error}");
        }

        private static int FindAgentIndex(SerializedProperty settings, int agentTypeId)
        {
            for (var index = 0; index < settings.arraySize; index++)
            {
                var id = settings.GetArrayElementAtIndex(index).FindPropertyRelative("agentTypeID");
                if (id is { propertyType: SerializedPropertyType.Integer } && id.intValue == agentTypeId)
                    return index;
            }

            return -1;
        }

        private static bool TryGetFloat(SerializedProperty parent, string name, out SerializedProperty property)
        {
            property = parent.FindPropertyRelative(name);
            return property is { propertyType: SerializedPropertyType.Float };
        }

        private static bool TryGetBool(SerializedProperty parent, string name, out SerializedProperty property)
        {
            property = parent.FindPropertyRelative(name);
            return property != null &&
                   (property.propertyType == SerializedPropertyType.Boolean ||
                    property.propertyType == SerializedPropertyType.Integer);
        }

        private static bool TryGetInt(SerializedProperty parent, string name, out SerializedProperty property)
        {
            property = parent.FindPropertyRelative(name);
            return property is { propertyType: SerializedPropertyType.Integer };
        }

        private static bool SetFloat(SerializedProperty property, float value)
        {
            if (Mathf.Approximately(property.floatValue, value))
                return false;

            property.floatValue = value;
            return true;
        }

        private static bool SetBool(SerializedProperty property, bool value)
        {
            if (GetBool(property) == value)
                return false;

            if (property.propertyType == SerializedPropertyType.Boolean)
                property.boolValue = value;
            else
                property.intValue = value ? 1 : 0;
            return true;
        }

        private static bool GetBool(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Boolean
                ? property.boolValue
                : property.intValue != 0;
        }

        private static bool SetInt(SerializedProperty property, int value)
        {
            if (property.intValue == value)
                return false;

            property.intValue = value;
            return true;
        }

        private static bool MatchesFloat(SerializedProperty parent, string name, float expected, ref string mismatch)
        {
            if (!TryGetFloat(parent, name, out var property))
            {
                mismatch = $"Humanoid setting '{name}' is missing.";
                return false;
            }

            if (Mathf.Approximately(property.floatValue, expected))
                return true;

            mismatch = $"Humanoid setting '{name}' is {property.floatValue}, expected {expected}.";
            return false;
        }

        private static bool MatchesBool(SerializedProperty parent, string name, bool expected, ref string mismatch)
        {
            if (!TryGetBool(parent, name, out var property))
            {
                mismatch = $"Humanoid setting '{name}' is missing.";
                return false;
            }

            if (GetBool(property) == expected)
                return true;

            mismatch = $"Humanoid setting '{name}' is {GetBool(property)}, expected {expected}.";
            return false;
        }

        private static bool MatchesInt(SerializedProperty parent, string name, int expected, ref string mismatch)
        {
            if (!TryGetInt(parent, name, out var property))
            {
                mismatch = $"Humanoid setting '{name}' is missing.";
                return false;
            }

            if (property.intValue == expected)
                return true;

            mismatch = $"Humanoid setting '{name}' is {property.intValue}, expected {expected}.";
            return false;
        }
    }
}
