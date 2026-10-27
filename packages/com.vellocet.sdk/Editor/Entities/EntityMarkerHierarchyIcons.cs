// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.VSig;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Editor.Entities
{
    /// <summary>
    ///     Draw a small icon in the Hierarchy for GameObjects that have an EntityMarker,
    ///     using the same icon path as the gizmo (marker.IconPath). Falls back to the prefab icon.
    /// </summary>
    [InitializeOnLoad]
    internal static class EntityMarkerHierarchyIcons
    {
        private static readonly string ModuleIconPath = Constants.IconsPath + "/module.png";
        private static readonly Texture2D FallbackIcon;
        private static readonly Dictionary<string, Texture2D> IconCache = new();

        static EntityMarkerHierarchyIcons()
        {
            FallbackIcon = (Texture2D)EditorGUIUtility.IconContent("Prefab Icon").image;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            EditorApplication.projectChanged += IconCache.Clear;
        }

        private static void OnHierarchyGUI(int instanceId, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint) return;
#if UNITY_6000_3_OR_NEWER
            var go = EditorUtility.EntityIdToObject(instanceId) as GameObject;
#else
            var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#endif
            if (!go)
                return;

            if (!TryGetHierarchyIcon(go, out var icon))
                return;

            var r = new Rect(selectionRect.xMax - 18f, selectionRect.y + 1f, 16f, 16f);
            GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit, true);
        }

        private static bool TryGetHierarchyIcon(GameObject gameObject, out Texture2D icon)
        {
            if (IsModuleRoot(gameObject))
            {
                icon = GetIcon(ModuleIconPath);
                return true;
            }

            if (gameObject.TryGetComponent<EntityMarker>(out var marker))
            {
                icon = GetIcon(marker.IconPath);
                return true;
            }

            icon = null;
            return false;
        }

        private static bool IsModuleRoot(GameObject gameObject)
        {
            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
                return false;

            return VSigRouteModelCache.Get(scene).MarkerIdsByModuleRootId.ContainsKey(gameObject.transform.GetInstanceID());
        }

        private static Texture2D GetIcon(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
                return FallbackIcon;

            if (IconCache.TryGetValue(iconPath, out var icon) && icon)
                return icon;

            icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath) ?? FallbackIcon;
            IconCache[iconPath] = icon;
            return icon;
        }
    }
}
#endif